using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Headless check that a genome becomes a moving articulation: develop, build, actuate,
    /// step, tear down. Run in batchmode via
    /// <c>-executeMethod Evosim.Sim.EditorTools.Milestone1Smoke.Run</c>.
    /// </summary>
    /// <remarks>
    /// The unit tests in Evosim.Core.Tests prove development is right. They cannot prove the
    /// articulation mapping is, because that needs PhysX. This is the smallest thing that
    /// exercises the whole chain without opening the Editor.
    ///
    /// <b>It asserts the creatures are awake.</b> Spike 01's first run reported numbers two
    /// orders of magnitude too good because zero gravity plus no actuation let PhysX sleep
    /// the entire scene, and every timing measured an idle solver. Any physics check here
    /// reports mean speed for that reason — see logbook/0002.
    /// </remarks>
    public static class Milestone1Smoke
    {
        private const int Creatures = 12;
        private const int WarmupSteps = 50;
        private const int MeasureSteps = 400;
        private const float FixedDt = 0.01f;
        private const float TestSineHz = 0.8f;

        /// <summary>Minimum mean speed, m/s, below which the creatures are assumed asleep.</summary>
        private const float AwakeThreshold = 0.001f;

        /// <summary>Metres of disagreement tolerated between the phenotype and what was built.</summary>
        private const float GeometryTolerance = 1e-3f;

        private const int MomentumSteps = 300;

        /// <summary>
        /// Momentum a creature at rest may acquire from its own actuation. Not zero, because
        /// a constraint solver is iterative and leaks a little; small enough that a creature
        /// drifting at 5 cm/s out of nothing is treated as a fault rather than as noise.
        /// </summary>
        /// <summary>
        /// Angular tolerance, m²/s per unit mass. Measured drift with correct actuation spans
        /// 0.0012–0.0175 across twelve runs (six creatures, self-collision off and on). Two
        /// distinct faults have been measured against it: removing the joint reaction torque
        /// gives 3.4–16.9, and uncapped depenetration gave 0.048. A bar at 0.03 sits ~1.7x
        /// above the worst honest run and ~1.6x below the smaller of the two faults.
        /// </summary>
        /// <remarks>
        /// This was 0.1, derived only from the reaction-torque fault, which is enormous. A
        /// bar sized against a huge fault says nothing about a small one, and the depenetration
        /// leak passed underneath it while a creature with 2% of its range of motion out-swam
        /// every creature that worked. The margin here is deliberately narrow: a tolerance wide
        /// enough to be comfortable is wide enough to hide the next exploit.
        /// </remarks>
        private const float AngularTolerance = 0.03f;

        /// <summary>
        /// Linear tolerance, m/s. Separates worse than the angular one — honest drift reaches
        /// 0.086, the depenetration leak reached 0.254, the reaction-torque fault 0.16. A bar
        /// at 0.15 clears the worst honest run by 1.7x and catches both known faults, but with
        /// no room to spare; the angular bar is still the one doing the real work.
        /// </summary>
        private const float ComSpeedTolerance = 0.15f;

        /// <summary>Settling discarded before measuring displacement — DESIGN.md §5.5.</summary>
        private const float SettleSeconds = 1f;

        private const float SwimSeconds = 8f;

        /// <summary>
        /// A creature in water cannot keep accelerating: drag rises with the square of speed,
        /// so every gait has a terminal velocity. Exceeding this means the fluid model is
        /// adding energy rather than removing it.
        /// </summary>
        private const float RunawaySpeed = 25f;

        // A growth ratio — momentum at 2T over momentum at T — was tried as a way to tell a
        // leak from solver drift, on the reasoning that injection accumulates linearly while
        // error random-walks. It is reported below but NOT asserted on, because testing it
        // against the known bug showed it made the check strictly worse: a creature leaking at
        // 3.1 m/s and 5.0 m²/s scored 1.63x, under the threshold, and passed. A leak that
        // saturates is still a leak. Magnitude catches every case the ratio caught and two it
        // did not.

        [MenuItem("Evosim/Milestone 1 — Smoke Test")]
        public static void RunFromMenu() => Execute();

        public static void Run()
        {
            bool ok = Execute();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static string RenderPipelineName()
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            return asset != null ? asset.GetType().Name : "Built-In (no SRP asset assigned)";
        }

        /// <summary>
        /// Confirms parts got a real lit shader. Under a render pipeline mismatch Unity
        /// substitutes the error shader and everything renders magenta — visible instantly on
        /// screen, invisible to a headless run, which is exactly why it is asserted here.
        /// </summary>
        private static bool CheckPartShader(CreatureInstance creature)
        {
            var renderer = creature.Root.GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError("[Evosim] built creature has no MeshRenderer — nothing would draw.");
                return false;
            }

            Shader shader = renderer.sharedMaterial != null ? renderer.sharedMaterial.shader : null;
            if (shader == null || shader.name.StartsWith("Hidden/InternalError"))
            {
                Debug.LogError(
                    $"[Evosim] parts resolved to '{(shader == null ? "null" : shader.name)}' — " +
                    "they would render magenta.");
                return false;
            }

            Debug.Log($"[Evosim] part shader: {shader.name}");
            return true;
        }

        /// <summary>
        /// Mean actuation capacity across a phenotype's links, N·m. Zero if nothing articulates.
        /// </summary>
        /// <remarks>
        /// Reported next to the motion figures because the two are now genuinely independent:
        /// Power is evolved per link (§5A.1), so a creature that barely moves may be weak rather
        /// than stuck, and telling those apart by eye is not possible without this column.
        /// </remarks>
        private static float MeanPower(Phenotype phenotype)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].JointType.DofCount() == 0) continue;
                sum += phenotype.Parts[i].Power;
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>
        /// Total linear and angular momentum of a creature, about its own centre of mass.
        /// </summary>
        private static void Momentum(CreatureInstance creature, out Vector3 linear, out Vector3 angular, out float mass)
        {
            mass = 0f;
            Vector3 com = Vector3.zero;
            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody b = creature.Bodies[i];
                mass += b.mass;
                com += b.worldCenterOfMass * b.mass;
            }
            com /= Mathf.Max(1e-6f, mass);

            linear = Vector3.zero;
            angular = Vector3.zero;

            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody b = creature.Bodies[i];
                Vector3 v = b.linearVelocity;
                Vector3 w = b.angularVelocity;

                linear += v * b.mass;
                angular += Vector3.Cross(b.worldCenterOfMass - com, v * b.mass);

                // Spin term, via the principal axes the inertia tensor is expressed in.
                Quaternion principal = b.transform.rotation * b.inertiaTensorRotation;
                Vector3 wLocal = Quaternion.Inverse(principal) * w;
                angular += principal * Vector3.Scale(b.inertiaTensor, wLocal);
            }
        }

        /// <summary>
        /// Actuation must be INTERNAL. With no gravity, no drag and no contact, nothing
        /// external acts on a creature, so its total momentum cannot change no matter what
        /// its joints do — exactly as you cannot swim by waving your arms in vacuum.
        /// </summary>
        /// <remarks>
        /// This check exists because the first version of <see cref="EffectorDriver"/> failed
        /// it badly: it applied joint torque to the child link and never applied the reaction
        /// to the parent, so every actuated joint manufactured angular momentum from nothing
        /// and creatures span up without bound. On screen that is unmistakable. Headlessly it
        /// looked like PASS, because "finite and moving" is satisfied very well by a creature
        /// spinning at 60 rad/s.
        ///
        /// It is also the cheapest possible guard against the exploit class in DESIGN.md
        /// §11.2 — a search handed free momentum will build its entire gait on it.
        /// </remarks>
        /// <summary>
        /// Runs <paramref name="steps"/> more steps and returns total momentum magnitude,
        /// linear and angular combined, per unit mass.
        /// </summary>
        private static float MeasureMomentumDrift(
            CreatureInstance creature, EffectorDriver driver, float[] scratch, int steps)
        {
            for (int s = 0; s < steps; s++)
            {
                driver.Drive(scratch);
                Physics.Simulate(FixedDt);
            }

            Momentum(creature, out Vector3 p, out Vector3 l, out float mass);
            return (p.magnitude + l.magnitude) / Mathf.Max(1e-6f, mass);
        }

        /// <summary>
        /// Total momentum must stay at zero: nothing external is acting, so anything the
        /// creature gains it manufactured.
        /// </summary>
        /// <param name="selfCollision">
        /// Run with the creature's own parts colliding. This is <b>not</b> a second
        /// configuration of the same test — it is a different claim.
        /// </param>
        /// <remarks>
        /// This check originally ran only with collision off, on the reasoning that contact is
        /// an external force. That reasoning was wrong. Contact between two parts of the
        /// <i>same</i> creature is internal: part A pushes part B and B pushes A back, so the
        /// total is unchanged. It belongs inside the conservation law, and excluding it was
        /// the reason the check could not see depenetration.
        ///
        /// Both runs are asserted, and they fail for different reasons. Collision off isolates
        /// the actuation model — a leak there means joint torque is being applied one-sided.
        /// Collision on adds the solver's overlap resolution — a leak there means PhysX is
        /// handing out velocity to push parts apart, which is free thrust available to any
        /// creature that jams itself, and exactly the class of flaw [U07 §3, p.5] documents a
        /// search discovering and building a gait on.
        /// </remarks>
        private static bool CheckMomentumConservation(StringBuilder report, bool selfCollision)
        {
            bool ok = true;

            FluidEnvironment.ConfigureScene(selfCollision);

            report.AppendLine();
            report.AppendLine(selfCollision
                ? "### Momentum conservation with self-collision — contact is internal too"
                : "### Momentum conservation — actuation must be internal");
            report.AppendLine(selfCollision
                ? "Same law, parts now collide. A creature's own parts pushing each other cannot move its centre of mass."
                : "No gravity, no damping, no contact: |P|/m and |L|/m must stay ~0.");
            report.AppendLine();
            report.AppendLine("| seed | speed of COM m/s | specific ang. momentum m2/s | growth on 2x time | verdict |");
            report.AppendLine("|---|---|---|---|---|");

            for (ulong seed = 1; seed <= 6; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                var driver = new EffectorDriver(creature, FixedDt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                // A constant, one-sided drive is the worst case: an oscillating signal can
                // hide a momentum leak by averaging it away over a cycle.
                for (int i = 0; i < scratch.Length; i++) scratch[i] = 1f;

                // Sampled at T and 2T, because magnitude alone cannot tell a leak from solver
                // error. A model injecting momentum does so at a roughly constant rate, so
                // doubling the time doubles the total — ratio near 2. Constraint-solver error
                // accumulates as a random walk instead, giving roughly sqrt(2) ≈ 1.41.
                // Anything genuinely broken fails both this and the magnitude bar by a wide
                // margin: the deliberately reintroduced bug measured 0.85-2.41 m²/s.
                float half = MeasureMomentumDrift(creature, driver, scratch, MomentumSteps);
                float full = MeasureMomentumDrift(creature, driver, scratch, MomentumSteps);

                Momentum(creature, out Vector3 p, out Vector3 l, out float mass);
                float comSpeed = p.magnitude / Mathf.Max(1e-6f, mass);
                float specificL = l.magnitude / Mathf.Max(1e-6f, mass);

                float growth = half > 1e-9f ? full / half : 1f;
                bool pass = comSpeed < ComSpeedTolerance && specificL < AngularTolerance;

                if (!pass)
                {
                    ok = false;
                    Debug.LogError(
                        $"[Evosim] seed {seed} (self-collision {(selfCollision ? "ON" : "OFF")}): " +
                        $"momentum not conserved — COM speed {comSpeed:0.####} m/s, " +
                        $"specific angular momentum {specificL:0.####} m2/s. " +
                        (selfCollision
                            ? "Contact between a creature's own parts is internal; the solver is manufacturing momentum."
                            : "Actuation is adding momentum from outside the creature."));
                }

                report.AppendLine(
                    $"| {seed} | {comSpeed:0.#####} | {specificL:0.#####} | {growth:0.##}x | " +
                    $"{(pass ? "ok" : "**LEAK**")} |");

                creature.Destroy();
            }

            return ok;
        }

        /// <summary>
        /// Holds one genome fixed and sweeps the depenetration velocity cap, reporting the
        /// momentum it manufactures at each. Diagnostic only — nothing is asserted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Depenetration is a <i>positional</i> correction, not a force: PhysX pushes overlapping
        /// shapes apart by handing them velocity, and that velocity is not paired. So the leak
        /// this attributes is real physics-engine behaviour rather than a bug in this project,
        /// and the only lever over it is the cap.
        /// </para>
        /// <para>
        /// It exists because the alternative, when the momentum check fails, is to widen the
        /// tolerance until it passes — and a tolerance wide enough to be comfortable is wide
        /// enough to hide the next exploit. If the leak falls roughly in proportion to the cap
        /// it is this mechanism and the cap is the fix. If it does not, it is something else and
        /// widening the bar would have buried it.
        /// </para>
        /// </remarks>
        private static void ReportDepenetrationSweep(StringBuilder report, ulong seed)
        {
            float[] caps = { 0.5f, 0.1f, 0.02f, 0.005f };
            float restore = Physics.defaultMaxDepenetrationVelocity;

            report.AppendLine();
            report.AppendLine("### Depenetration attribution");
            report.AppendLine(
                $"Seed {seed}, self-collision ON, constant full drive. The cap is applied to bodies " +
                "at creation, so each row rebuilds the creature. Momentum falling with the cap " +
                "means the leak is overlap resolution and nothing else.");
            report.AppendLine();
            report.AppendLine("| max depenetration m/s | COM speed m/s | specific ang. momentum m2/s |");
            report.AppendLine("|---|---|---|");

            try
            {
                foreach (float cap in caps)
                {
                    FluidEnvironment.ConfigureScene(selfCollision: true);
                    Physics.defaultMaxDepenetrationVelocity = cap;

                    var limits = DevelopmentLimits.Default;
                    Genome genome = GenomeFactory.RandomViable(
                        new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                    CreatureInstance creature =
                        PhenotypeBuilder.Build(Developer.Develop(genome, limits), Vector3.zero);

                    var driver = new EffectorDriver(creature, FixedDt);
                    var scratch = new float[Mathf.Max(1, creature.TotalDof)];
                    for (int i = 0; i < scratch.Length; i++) scratch[i] = 1f;

                    MeasureMomentumDrift(creature, driver, scratch, MomentumSteps * 2);

                    Momentum(creature, out Vector3 p, out Vector3 l, out float mass);
                    report.AppendLine(
                        $"| {cap} | {p.magnitude / Mathf.Max(1e-6f, mass):0.#####} | " +
                        $"{l.magnitude / Mathf.Max(1e-6f, mass):0.#####} |");

                    creature.Destroy();
                }
            }
            catch (System.Exception e)
            {
                report.AppendLine($"| — | sweep threw | {e.GetType().Name}: {e.Message} |");
            }
            finally
            {
                Physics.defaultMaxDepenetrationVelocity = restore;
                FluidEnvironment.ConfigureScene();
            }
        }

        /// <summary>
        /// Puts creatures in water and measures how far they get — DESIGN.md §5.5 fitness,
        /// which is displacement of the centre of mass after discarding settling.
        /// </summary>
        /// <remarks>
        /// Nothing is being selected, so these are random genomes driven by a phase-offset
        /// sine. Most will barely move and that is the correct outcome; the point is that
        /// displacement is now a <i>meaningful</i> number rather than an artefact of how much
        /// torque happened to be applied against nothing.
        ///
        /// The assertion is not "creatures swim" — it is that the fluid model does not
        /// misbehave: speeds stay bounded, nothing goes non-finite, and no creature is
        /// accelerating without limit. A drag model that can add energy is a free-energy
        /// source, and [U07 §2, p.3] documents a published search finding exactly that.
        /// </remarks>
        /// <summary>
        /// Power dissipated by the articulation drives' own damping, in watts.
        /// </summary>
        /// <remarks>
        /// <see cref="PhenotypeBuilder"/> sets <c>damping = 1</c> on every drive so undriven
        /// joints settle instead of ringing. That is a viscous element inside the joint: it
        /// removes energy at <c>damping · ω²</c> per DOF, and it is invisible to both the drag
        /// term and ΔKE. A term this small-looking was not expected to matter, which is exactly
        /// why it needs measuring rather than assuming.
        /// </remarks>
        private static double DriveDampingPower(CreatureInstance creature)
        {
            double total = 0d;

            for (int b = 1; b < creature.Bodies.Length; b++)
            {
                if (creature.DofOffset[b] < 0) continue;

                ArticulationBody body = creature.Bodies[b];
                ArticulationReducedSpace v = body.jointVelocity;
                if (v.dofCount == 0) continue;

                for (int d = 0; d < v.dofCount; d++)
                {
                    float damping =
                        d == 0 ? body.xDrive.damping :
                        d == 1 ? body.yDrive.damping : body.zDrive.damping;

                    total += damping * (double)v[d] * v[d];
                }
            }

            return total;
        }

        /// <summary>Kinetic energy of a creature, translational plus rotational, in joules.</summary>
        private static double KineticEnergy(CreatureInstance creature)
        {
            double total = 0d;

            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody b = creature.Bodies[i];

                total += 0.5d * b.mass * b.linearVelocity.sqrMagnitude;

                // Rotational term in the frame the inertia tensor is diagonal in.
                Quaternion principal = b.transform.rotation * b.inertiaTensorRotation;
                Vector3 w = Quaternion.Inverse(principal) * b.angularVelocity;
                Vector3 I = b.inertiaTensor;

                total += 0.5d * (I.x * w.x * w.x + I.y * w.y * w.y + I.z * w.z * w.z);
            }

            return total;
        }

        /// <summary>
        /// Does the energy budget close? Joints put energy in, drag takes it out, the rest is
        /// kinetic energy — DESIGN.md §5A.2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The first work measurement reported creatures dissipating kilowatts. That is
        /// defensible on its face, because water is dense and a 300 kg limb moving at a few m/s
        /// really does cost that much — but "defensible on its face" describes every broken
        /// measurement this project has produced. Four separate faults reached the point of
        /// being reported as results before something conserved caught them.
        /// </para>
        /// <para>
        /// So: <c>signed joint work = ΔKE + energy dissipated by drag</c>. Nothing else acts.
        /// If this closes, the work figures mean what they claim; if it does not, they are
        /// arithmetic on the wrong quantity and the metabolic model in §5A would be calibrated
        /// against a fiction.
        /// </para>
        /// <para>
        /// Self-collision is off here — contact does work that neither term accounts for, so
        /// leaving it on would test two things and blame the wrong one. That reasoning was
        /// wrong once before, for the momentum check, where contact between a creature's own
        /// parts is <i>internal</i> and belonged inside the law. It is right here for a
        /// different reason: this is an energy balance, not a momentum balance, and contact
        /// dissipates energy internally even when it conserves momentum.
        /// </para>
        /// <para>
        /// The tolerance is loose (10%) because both integrals are first-order rectangles
        /// evaluated at pre-step velocities against a solver that is not. It is sized to catch
        /// a wrong quantity, not to certify an accurate one.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Where does the missing energy go? Sweeps drive strength and reports what fails to
        /// balance, alongside how much of the time joints are pinned against their limits.
        /// </summary>
        /// <remarks>
        /// A joint limit is a hard constraint. A limb arriving at one is stopped by the solver,
        /// and its kinetic energy is destroyed there — a sink that appears in neither the drag
        /// term nor ΔKE. If that is the missing 85%, weakening the drive should collapse the
        /// residual, because a limb that never reaches its limit never slams into one.
        ///
        /// Run as a sweep rather than argued from arithmetic: the estimate that produced this
        /// hypothesis (~470 J per impact, ~13 impacts) lands in the right range, and landing in
        /// the right range is exactly how wrong explanations survive here.
        /// </remarks>
        private static void ReportEnergySinkSweep(StringBuilder report)
        {
            float[] scales = { 0.05f, 0.2f, 0.5f, 1f, 2f };

            FluidEnvironment.ConfigureScene(selfCollision: false);

            report.AppendLine();
            report.AppendLine("### Where the missing energy goes");
            report.AppendLine("Seed 3, evolved link Power multiplied by each factor. 'at limit' is the share");
            report.AppendLine("pinned within 1% of their configured range.");
            report.AppendLine();
            report.AppendLine("| power multiplier | joint work J | drag out J | drive damping J | unaccounted | at limit |");
            report.AppendLine("|---|---|---|---|---|---|");

            foreach (float scale in scales)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(3), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                var driver = new EffectorDriver(creature, FixedDt) { PowerScale = scale };
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                double keStart = KineticEnergy(creature);
                double damped = 0d;
                float t = 0f;
                long atLimit = 0, samples = 0;

                int steps = Mathf.RoundToInt(SwimSeconds / FixedDt);
                for (int s = 0; s < steps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature, FixedDt);
                    Physics.Simulate(FixedDt);
                    driver.Settle();
                    fluid.Settle(creature);
                    t += FixedDt;
                    damped += DriveDampingPower(creature) * FixedDt;

                    for (int b = 1; b < creature.Bodies.Length; b++)
                    {
                        if (creature.DofOffset[b] < 0) continue;

                        ArticulationBody body = creature.Bodies[b];
                        ArticulationReducedSpace q = body.jointPosition;
                        if (q.dofCount == 0) continue;

                        float lower = body.xDrive.lowerLimit * Mathf.Deg2Rad;
                        float upper = body.xDrive.upperLimit * Mathf.Deg2Rad;
                        float span = Mathf.Max(1e-4f, upper - lower);

                        samples++;
                        if (q[0] - lower < 0.01f * span || upper - q[0] < 0.01f * span) atLimit++;
                    }
                }

                double work = driver.SignedWorkJoules;
                double residual = work - ((KineticEnergy(creature) - keStart) + fluid.DissipatedJoules + damped);
                double fraction = System.Math.Abs(work) > 1d ? residual / work : 0d;

                report.AppendLine(
                    $"| {scale} | {work:0.#} | {fluid.DissipatedJoules:0.#} | {damped:0.#} | {fraction:P1} | " +
                    $"{(samples > 0 ? (double)atLimit / samples : 0):P1} |");

                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine(
                "If 'unaccounted' falls with drive strength while 'at limit' falls alongside it, " +
                "the sink is the joint-limit constraint and the drive is far too strong for the " +
                "ranges §4.1 generates. That would make the metabolic cost in §5A.2 dominated by " +
                "a modelling artefact rather than by swimming.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
        }

        /// <summary>
        /// The decisive test for the joint-limit hypothesis: same creature, same drive, wider
        /// limits. A limb that never reaches its limit cannot lose energy to one.
        /// </summary>
        /// <remarks>
        /// Preferred over reasoning about the residual's correlation with limit contact, which
        /// is suggestive but not causal — drive strength moves both, so the correlation is also
        /// consistent with a sink that simply scales with how hard the creature is driven.
        /// Widening the limits moves one and not the other.
        /// </remarks>
        private static void ReportLimitSweep(StringBuilder report)
        {
            float[] widenings = { 1f, 2f, 4f, 20f };

            FluidEnvironment.ConfigureScene(selfCollision: false);

            report.AppendLine();
            report.AppendLine("### Is it the joint limits?");
            report.AppendLine("Seed 3 at 2 N·m/kg throughout. Only the configured joint ranges change.");
            report.AppendLine();
            report.AppendLine("| limits widened | joint work J | drag out J | unaccounted | at limit |");
            report.AppendLine("|---|---|---|---|---|");

            foreach (float widening in widenings)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(3), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                for (int b = 1; b < creature.Bodies.Length; b++)
                {
                    ArticulationBody body = creature.Bodies[b];
                    body.xDrive = Widen(body.xDrive, widening);
                    body.yDrive = Widen(body.yDrive, widening);
                    body.zDrive = Widen(body.zDrive, widening);
                }

                var driver = new EffectorDriver(creature, FixedDt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                double keStart = KineticEnergy(creature);
                double damped = 0d;
                float t = 0f;
                long atLimit = 0, samples = 0;

                int steps = Mathf.RoundToInt(SwimSeconds / FixedDt);
                for (int s = 0; s < steps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature, FixedDt);
                    Physics.Simulate(FixedDt);
                    driver.Settle();
                    fluid.Settle(creature);
                    t += FixedDt;
                    damped += DriveDampingPower(creature) * FixedDt;

                    for (int b = 1; b < creature.Bodies.Length; b++)
                    {
                        if (creature.DofOffset[b] < 0) continue;

                        ArticulationBody body = creature.Bodies[b];
                        ArticulationReducedSpace q = body.jointPosition;
                        if (q.dofCount == 0) continue;

                        float lower = body.xDrive.lowerLimit * Mathf.Deg2Rad;
                        float upper = body.xDrive.upperLimit * Mathf.Deg2Rad;
                        float span = Mathf.Max(1e-4f, upper - lower);

                        samples++;
                        if (q[0] - lower < 0.01f * span || upper - q[0] < 0.01f * span) atLimit++;
                    }
                }

                double work = driver.SignedWorkJoules;
                double residual = work - ((KineticEnergy(creature) - keStart) + fluid.DissipatedJoules + damped);
                double fraction = System.Math.Abs(work) > 1d ? residual / work : 0d;

                report.AppendLine(
                    $"| {widening}x | {work:0.#} | {fluid.DissipatedJoules:0.#} | {fraction:P1} | " +
                    $"{(samples > 0 ? (double)atLimit / samples : 0):P1} |");

                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine(
                "Drive strength is held fixed, so unlike the sweep above this separates the two: " +
                "if 'unaccounted' collapses as the limits widen, the sink is the limit constraint. " +
                "If it does not, the sink scales with drive strength for some other reason and the " +
                "joint-power definition is the next suspect.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
        }

        private static ArticulationDrive Widen(ArticulationDrive drive, float factor)
        {
            drive.lowerLimit *= factor;
            drive.upperLimit *= factor;
            return drive;
        }

        /// <summary>
        /// Does the leftover residual shrink with the timestep? Integration error does; a sink
        /// that is simply not being counted does not.
        /// </summary>
        /// <remarks>
        /// With every known sink accounted and the joint limits widened out of the way, the
        /// balance still leaves 0.5-19% depending on the creature, with the sign varying. That
        /// pattern suggests discretisation rather than physics, but "suggests" is how four
        /// wrong explanations survived earlier in this same investigation. Halving dt is the
        /// direct test.
        /// </remarks>
        private static void ReportTimestepConvergence(StringBuilder report)
        {
            float[] steps = { 0.01f, 0.005f, 0.0025f };
            ulong[] seeds = { 1, 3, 4, 6 };          // the four that fail at dt = 0.01

            FluidEnvironment.ConfigureScene(selfCollision: false);

            report.AppendLine();
            report.AppendLine("### Is the leftover residual numerical?");
            report.AppendLine("Same creatures, same 8 s, limits widened 20x. Only dt changes.");
            report.AppendLine();
            report.AppendLine("| seed | dt=0.01 | dt=0.005 | dt=0.0025 |");
            report.AppendLine("|---|---|---|---|");

            foreach (ulong seed in seeds)
            {
                var row = $"| {seed} |";

                foreach (float dt in steps)
                {
                    var limits = DevelopmentLimits.Default;
                    Genome genome = GenomeFactory.RandomViable(
                        new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                    Phenotype phenotype = Developer.Develop(genome, limits);

                    var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
                    CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                    fluid.ApplyAddedMass(creature);

                    for (int b = 1; b < creature.Bodies.Length; b++)
                    {
                        ArticulationBody body = creature.Bodies[b];
                        body.xDrive = Widen(body.xDrive, 20f);
                        body.yDrive = Widen(body.yDrive, 20f);
                        body.zDrive = Widen(body.zDrive, 20f);
                    }

                    var driver = new EffectorDriver(creature, dt);
                    var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                    double keStart = KineticEnergy(creature);
                    double damped = 0d;
                    float t = 0f;

                    int n = Mathf.RoundToInt(SwimSeconds / dt);
                    for (int i = 0; i < n; i++)
                    {
                        driver.DriveTestSine(t, TestSineHz, scratch);
                        fluid.Apply(creature, dt);
                        Physics.Simulate(dt);
                        driver.Settle();
                        fluid.Settle(creature);
                        t += dt;
                        damped += DriveDampingPower(creature) * dt;
                    }

                    double work = driver.SignedWorkJoules;
                    double residual =
                        work - ((KineticEnergy(creature) - keStart) + fluid.DissipatedJoules + damped);
                    double scale = System.Math.Max(System.Math.Abs(work), 1d);

                    row += $" {residual / scale:P1} |";
                    creature.Destroy();
                }

                report.AppendLine(row);
            }

            report.AppendLine();
            report.AppendLine(
                "Falling roughly with dt means the residual is discretisation and the work " +
                "figures are sound to about the size of the remaining error. Flat means a sink " +
                "is still unaccounted and no energy number should be trusted yet.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
        }

        /// <summary>
        /// Two equal boxes, one hinge, constant torque. The smallest system where the energy
        /// balance can be checked without chaos in the way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The timestep-convergence test on random creatures was invalid: with limits widened
        /// and full drive they are chaotic, so halving dt does not refine one trajectory, it
        /// produces a different one. Two seeds appeared to converge and two to diverge, and
        /// neither meant anything — the spread was between unrelated runs.
        /// </para>
        /// <para>
        /// A single hinge driven by a constant torque has no such sensitivity. If the residual
        /// here falls with dt, the accounting is right and the error is discretisation. If it
        /// is flat and large, the definition of joint power is wrong, and every energy figure
        /// in this project is measuring the wrong quantity.
        /// </para>
        /// </remarks>
        private static bool CheckMinimalEnergyBalance(StringBuilder report)
        {
            float[] steps = { 0.01f, 0.005f, 0.0025f, 0.00125f };
            const float Tolerance = 0.05f;

            FluidEnvironment.ConfigureScene(selfCollision: false);

            report.AppendLine();
            report.AppendLine("### Energy balance on a minimal creature");
            report.AppendLine("Two 0.5 m cubes, one hinge, constant full drive, 4 s. No chaos, so dt refines.");
            report.AppendLine();
            report.AppendLine("| dt | joint work J | ΔKE J | drag out J | damping J | residual | error |");
            report.AppendLine("|---|---|---|---|---|---|---|");

            double finest = 1d;

            foreach (float dt in steps)
            {
                var genome = new Genome { RootIndex = 0 };

                // Fixed, not the default Hinge: the root has no parent so its joint is unused,
                // but Genome.Validate checks every node and a Hinge with no limits is malformed.
                var root = new MorphNode
                {
                    Dimensions = new Float3(0.25f, 0.25f, 0.25f),
                    JointType = JointType.Fixed,
                };
                var child = new MorphNode
                {
                    Dimensions = new Float3(0.25f, 0.25f, 0.25f),
                    JointType = JointType.Hinge,

                    // A joint means link tissue (§5A.1), and torque now comes from the link's
                    // own capacity rather than from a coefficient in the driver. 500 N.m is the
                    // figure the tau.dtheta cross-check below was calibrated against; it is
                    // held here deliberately so that reference number still means what it did.
                    CellTypeId = CellTypeIds.Link,
                    Power = 500f,

                    // Effectively unlimited. At +/-3 rad this test measured exactly 1500 J of
                    // joint work at every timestep — 500 N.m x 3 rad — because constant torque
                    // drives a hinge to its limit and holds it there. That reproduced tau.dtheta
                    // to four figures and so verified the power measurement, but it verified it
                    // against a case where 76% of the energy goes into the limit constraint,
                    // which is the sink this test exists to avoid.
                    JointLimits = new[] { new Float2(-100f, 100f) },
                };

                // Child hangs off the parent's +X face, its own -X face meeting it.
                root.Edges.Add(new MorphEdge
                {
                    Child = 1,
                    ParentAnchor = new Float3(1f, 0f, 0f),
                    ChildAnchor = new Float3(-1f, 0f, 0f),
                });

                genome.Nodes.Add(root);
                genome.Nodes.Add(child);

                Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);

                var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                // Genuinely unlimited, not merely widened. PhysX does not honour a revolute
                // limit past one revolution: at +/-100 rad this stopped dead at exactly
                // 500 N.m x 2*pi = 3141.6 J, having wrapped rather than kept turning. A limit
                // cannot be moved out of the way, only removed.
                for (int b = 1; b < creature.Bodies.Length; b++)
                {
                    ArticulationBody body = creature.Bodies[b];
                    body.twistLock = ArticulationDofLock.FreeMotion;
                    body.swingYLock = ArticulationDofLock.FreeMotion;
                    body.swingZLock = ArticulationDofLock.LockedMotion;
                }

                var driver = new EffectorDriver(creature, dt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];
                for (int i = 0; i < scratch.Length; i++) scratch[i] = 1f;

                double keStart = KineticEnergy(creature);
                double damped = 0d;

                int n = Mathf.RoundToInt(4f / dt);
                for (int i = 0; i < n; i++)
                {
                    driver.Drive(scratch);
                    fluid.Apply(creature, dt);
                    Physics.Simulate(dt);
                    driver.Settle();
                    fluid.Settle(creature);
                    damped += DriveDampingPower(creature) * dt;
                }

                double work = driver.SignedWorkJoules;
                double deltaKe = KineticEnergy(creature) - keStart;
                double residual = work - (deltaKe + fluid.DissipatedJoules + damped);
                double error = System.Math.Abs(residual) / System.Math.Max(System.Math.Abs(work), 1d);
                finest = error;

                report.AppendLine(
                    $"| {dt} | {work:0.#} | {deltaKe:0.#} | {fluid.DissipatedJoules:0.#} | " +
                    $"{damped:0.#} | {residual:0.#} | {error:P2} |");

                creature.Destroy();
            }

            bool ok = finest <= Tolerance;
            if (!ok)
            {
                Debug.LogError(
                    $"[Evosim] minimal energy balance still out by {finest:P1} at the finest " +
                    "timestep. Joint work does not measure the energy the joints deliver, and no " +
                    "metabolic figure derived from it means anything.");
            }

            report.AppendLine();
            report.AppendLine(
                "Only the finest step is asserted, at 5%. A residual that shrinks as dt shrinks " +
                "is discretisation; one that does not is a wrong quantity, and that is the case " +
                "this exists to fail on.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
            return ok;
        }

        private static bool CheckEnergyBalance(StringBuilder report)
        {
            const float Tolerance = 0.10f;
            bool ok = true;

            FluidEnvironment.ConfigureScene(selfCollision: false);

            report.AppendLine();
            report.AppendLine("### Energy balance — work in = ΔKE + drag out");
            report.AppendLine("No contact, no gravity. Whatever the other terms do not account for went into the joint limits.");
            report.AppendLine();
            report.AppendLine("| seed | joint work J | ΔKE J | drag out J | drive damping J | into limits J | share |");
            report.AppendLine("|---|---|---|---|---|---|---|");

            for (ulong seed = 1; seed <= 6; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                var driver = new EffectorDriver(creature, FixedDt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                double keStart = KineticEnergy(creature);
                double damped = 0d;
                float t = 0f;

                int steps = Mathf.RoundToInt(SwimSeconds / FixedDt);
                for (int s = 0; s < steps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature, FixedDt);
                    Physics.Simulate(FixedDt);
                    driver.Settle();
                    fluid.Settle(creature);
                    t += FixedDt;
                    damped += DriveDampingPower(creature) * FixedDt;
                }

                double work = driver.SignedWorkJoules;
                double deltaKe = KineticEnergy(creature) - keStart;
                double dissipated = fluid.DissipatedJoules;
                double residual = work - (deltaKe + dissipated + damped);

                // Relative to the largest term, so a creature that barely moves is not judged
                // against a near-zero denominator.
                double scale = System.Math.Max(
                    System.Math.Max(System.Math.Abs(work), System.Math.Abs(dissipated)),
                    System.Math.Max(System.Math.Abs(deltaKe), 1d));
                double error = System.Math.Abs(residual) / scale;

                // Not asserted, and not an error. This quantity was a FAIL for most of its
                // existence, on the assumption that a residual meant the work measurement was
                // broken. CheckMinimalEnergyBalance settled that it is not: on a single free
                // hinge the same accounting converges to zero as dt shrinks. What is left here
                // is real energy, destroyed by the joint-limit constraint, and the number is a
                // finding about calibration rather than a defect — see §5A.10.
                report.AppendLine(
                    $"| {seed} | {work:0.#} | {deltaKe:0.#} | {dissipated:0.#} | {damped:0.#} | " +
                    $"{residual:0.#} | {error:P1} |");

                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine(
                "**Most of what a creature spends is destroyed by its own joint limits**, not " +
                "delivered to the water. The drive is far stronger than the ranges §4.1 " +
                "generates can absorb, so limbs arrive at their stops at speed and the " +
                "constraint dissipates their kinetic energy. Under §5A this is charged as " +
                "metabolic cost, which is defensible — a real muscle slamming a joint does spend " +
                "the energy — but it means the cost is presently dominated by bang-bang " +
                "actuation rather than by swimming. Link Power is now evolved and billed for (§5A.1), " +
                "so this is a pressure selection can act on rather than a constant to tune — but " +
                "judging it fairly needs the brain graph (Milestone 6), since an open-loop sine " +
                "has no way to decelerate before a stop.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
            return ok;
        }

        private static bool CheckSwimming(StringBuilder report)
        {
            bool ok = true;

            // Set at the START, not the end. This used to be restored only after the loop, so
            // the whole water table silently inherited whatever the momentum check left behind
            // — which was collision OFF. Every displacement figure this table has ever produced
            // was measured with self-collision disabled, including the ones used to argue that
            // enabling it changed nothing. Scene state is global; a check that depends on it
            // has to set it, not assume it.
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });

            report.AppendLine();
            report.AppendLine("### In water — DESIGN.md §5.2 drag, §5.5 displacement");
            report.AppendLine($"{fluid.Config}   {SwimSeconds:0.#} s after {SettleSeconds:0.#} s settling");
            report.AppendLine();
            report.AppendLine("| seed | parts | DOF | displacement m | speed m/s | peak speed m/s | work J | power W | J per metre |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");

            double totalWork = 0d;
            double totalDisplacement = 0d;

            for (ulong seed = 1; seed <= Creatures; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                var driver = new EffectorDriver(creature, FixedDt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                float t = 0f;
                int settleSteps = Mathf.RoundToInt(SettleSeconds / FixedDt);
                int swimSteps = Mathf.RoundToInt(SwimSeconds / FixedDt);

                for (int s = 0; s < settleSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature);
                    Physics.Simulate(FixedDt);
                    driver.Settle();
                    t += FixedDt;
                }

                Vector3 start = FluidEnvironment.CentreOfMass(creature);
                float peak = 0f;
                bool finite = true;

                // Settling is excluded from the energy figures for the same reason it is
                // excluded from displacement: it is the creature sorting itself out, not
                // swimming, and counting it would flatter efficient creatures with slow starts.
                double workAtStart = driver.MechanicalWorkJoules;

                for (int s = 0; s < swimSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature);
                    Physics.Simulate(FixedDt);
                    driver.Settle();
                    t += FixedDt;

                    for (int b = 0; b < creature.Bodies.Length; b++)
                    {
                        float speed = creature.Bodies[b].linearVelocity.magnitude;
                        if (float.IsNaN(speed) || float.IsInfinity(speed)) { finite = false; continue; }
                        if (speed > peak) peak = speed;
                    }
                }

                Vector3 end = FluidEnvironment.CentreOfMass(creature);
                float displacement = Vector3.Distance(end, start);
                float speedAchieved = displacement / SwimSeconds;

                if (!finite)
                {
                    Debug.LogError($"[Evosim] seed {seed}: non-finite velocity in water.");
                    ok = false;
                }

                if (peak > RunawaySpeed)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: peak speed {peak:0.#} m/s in water. Drag should " +
                        "bound this; a body that keeps accelerating means the model is adding energy.");
                    ok = false;
                }

                double work = driver.MechanicalWorkJoules - workAtStart;
                double power = work / SwimSeconds;
                double perMetre = displacement > 1e-4f ? work / displacement : double.NaN;

                totalWork += work;
                totalDisplacement += displacement;

                report.AppendLine(
                    $"| {seed} | {phenotype.PartCount} | {phenotype.TotalDof} | " +
                    $"{displacement:0.###} | {speedAchieved:0.###} | {peak:0.##} | " +
                    $"{work:0.#} | {power:0.#} | {(double.IsNaN(perMetre) ? "—" : perMetre.ToString("0"))} |");

                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine(
                $"Population cost of transport: {totalWork:0.#} J for {totalDisplacement:0.##} m " +
                $"= **{(totalDisplacement > 1e-4 ? totalWork / totalDisplacement : 0):0.#} J/m**.");
            report.AppendLine(
                "First measurement of §5A.10's mechanical work coefficient. Reported, not spent: " +
                "nothing charges for it until Milestone 3, and the exchange rate against a joule " +
                "of sunlight is still unknown. The per-metre column is the one that matters — " +
                "[C18 §3, p.17] warns that energy alone says nothing without a performance level " +
                "to divide it by, and a creature that does nothing is perfectly efficient.");

            FluidEnvironment.ConfigureScene(selfCollision: true);
            return ok;
        }

        /// <summary>
        /// Does self-collision actually do anything, and what does it cost in range of motion?
        /// </summary>
        /// <remarks>
        /// Two boxes hinged at their shared face jam immediately — any rotation drives the
        /// child's corners into the parent. PhysX exempts directly-jointed links from
        /// colliding, so that case is fine, but nothing exempts siblings or a grandchild
        /// swinging back into its grandparent. If those collisions bite hard, creatures lose
        /// the joint travel they need to swim at all, and the fix would be a connector — a
        /// gap or narrow neck between parts — rather than more collision tuning.
        ///
        /// Measured rather than reasoned about: identical joint travel with collision on and
        /// off means the setting is inert, which is itself a finding.
        /// </remarks>
        private static void ReportSelfCollisionEffect(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("### Does self-collision cost range of motion?");
            report.AppendLine("Same creature, same seed, run twice. Joint travel is the total angle");
            report.AppendLine("swept by all DOF — the thing a creature needs in order to swim.");
            report.AppendLine();
            report.AppendLine("| seed | parts | travel OFF rad | travel ON rad | kept | displacement OFF | displacement ON |");
            report.AppendLine("|---|---|---|---|---|---|---|");

            for (ulong seed = 1; seed <= 8; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                RunOnce(phenotype, false, out float travelOff, out float moveOff);
                RunOnce(phenotype, true, out float travelOn, out float moveOn);

                float kept = travelOff > 1e-6f ? travelOn / travelOff : 1f;

                report.AppendLine(
                    $"| {seed} | {phenotype.PartCount} | {travelOff:0.##} | {travelOn:0.##} | " +
                    $"{kept:P0} | {moveOff:0.###} | {moveOn:0.###} |");
            }

            FluidEnvironment.ConfigureScene(selfCollision: true);
        }

        internal static void RunOnce(Phenotype phenotype, bool selfCollision, out float jointTravel, out float displacement)
            => RunOnce(phenotype, selfCollision, SwimSeconds, out jointTravel, out displacement);

        internal static void RunOnce(
            Phenotype phenotype, bool selfCollision, float seconds,
            out float jointTravel, out float displacement)
        {
            FluidEnvironment.ConfigureScene(selfCollision);

            var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
            CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
            fluid.ApplyAddedMass(creature);

            var driver = new EffectorDriver(creature, FixedDt);
            var scratch = new float[Mathf.Max(1, creature.TotalDof)];

            // DofOffset is -1 for the root and for fixed joints — both have nothing to sweep.
            var previous = new float[Mathf.Max(1, creature.TotalDof)];
            for (int b = 0; b < creature.Bodies.Length; b++)
            {
                if (creature.DofOffset[b] < 0) continue;

                ArticulationReducedSpace q = creature.Bodies[b].jointPosition;
                int n = Mathf.Min(q.dofCount, creature.Phenotype.Parts[b].JointType.DofCount());
                for (int d = 0; d < n; d++) previous[creature.DofOffset[b] + d] = q[d];
            }

            Vector3 start = FluidEnvironment.CentreOfMass(creature);
            jointTravel = 0f;
            float t = 0f;

            int steps = Mathf.RoundToInt(seconds / FixedDt);
            for (int s = 0; s < steps; s++)
            {
                driver.DriveTestSine(t, TestSineHz, scratch);
                fluid.Apply(creature);
                Physics.Simulate(FixedDt);
                t += FixedDt;

                for (int b = 0; b < creature.Bodies.Length; b++)
                {
                    if (creature.DofOffset[b] < 0) continue;

                    ArticulationReducedSpace q = creature.Bodies[b].jointPosition;
                    int n = Mathf.Min(q.dofCount, creature.Phenotype.Parts[b].JointType.DofCount());
                    for (int d = 0; d < n; d++)
                    {
                        int idx = creature.DofOffset[b] + d;
                        jointTravel += Mathf.Abs(q[d] - previous[idx]);
                        previous[idx] = q[d];
                    }
                }
            }

            displacement = Vector3.Distance(FluidEnvironment.CentreOfMass(creature), start);
            creature.Destroy();
        }

        private static bool Execute()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var report = new StringBuilder();
            report.AppendLine("=== Milestone 1 smoke test ===");
            report.AppendLine($"Unity {Application.unityVersion}   dt={FixedDt}   {MeasureSteps} steps");
            report.AppendLine($"pipeline: {RenderPipelineName()}");
            report.AppendLine();
            report.AppendLine("| seed | parts | depth | DOF | volume m3 | mirrored | buried | mean speed m/s | joint rate rad/s | mean power N·m | finite |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            bool allOk = true;
            int spawned = 0;

            for (ulong seed = 1; seed <= Creatures; seed++)
            {
                var limits = DevelopmentLimits.Default;
                var rng = new Rng(seed);

                Genome genome = GenomeFactory.RandomViable(rng, RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);

                // Verify the built articulation matches what development asked for, BEFORE
                // stepping physics moves everything. Parts are parented to each other, so the
                // builder has to undo Unity's compounding of parent scale into children — the
                // most error-prone code in the chain, and the unit tests cannot reach it
                // because it is Unity-side.
                float worstPos = 0f, worstSize = 0f;
                for (int b = 0; b < creature.Bodies.Length; b++)
                {
                    PhenotypePart expected = phenotype.Parts[b];
                    Transform actual = creature.Bodies[b].transform;

                    worstPos = Mathf.Max(worstPos,
                        Vector3.Distance(actual.position, expected.Position.ToVector3()));

                    // Size lives on the collider, not the transform — see PhenotypeBuilder.
                    // Checking lossyScale here would pass trivially now that every part
                    // transform is unit scale, so check the thing physics actually uses.
                    Vector3 expectedSize = (expected.HalfExtents * 2f).ToVector3();
                    Vector3 actualSize = creature.Bodies[b].GetComponent<BoxCollider>().size;
                    worstSize = Mathf.Max(worstSize, Mathf.Max(
                        Mathf.Abs(actualSize.x - expectedSize.x),
                        Mathf.Max(
                            Mathf.Abs(actualSize.y - expectedSize.y),
                            Mathf.Abs(actualSize.z - expectedSize.z))));
                }

                if (worstPos > GeometryTolerance || worstSize > GeometryTolerance)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: built geometry does not match the phenotype — " +
                        $"worst position error {worstPos:0.#####} m, worst size error {worstSize:0.#####} m.");
                    allOk = false;
                }

                // A part rendering with the error shader is the "everything is magenta"
                // symptom, and -nographics cannot see it. Check the material instead.
                if (seed == 1 && !CheckPartShader(creature)) allOk = false;

                var driver = new EffectorDriver(creature, FixedDt);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                float t = 0f;
                for (int s = 0; s < WarmupSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;
                }

                double speedSum = 0;
                double jointRateSum = 0;
                int samples = 0;
                int jointSamples = 0;
                bool finite = true;
                bool slept = false;

                for (int s = 0; s < MeasureSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;

                    for (int b = 0; b < creature.Bodies.Length; b++)
                    {
                        Vector3 v = creature.Bodies[b].linearVelocity;
                        if (float.IsNaN(v.x) || float.IsInfinity(v.x) ||
                            float.IsNaN(v.y) || float.IsInfinity(v.y) ||
                            float.IsNaN(v.z) || float.IsInfinity(v.z))
                        {
                            finite = false;
                            continue;
                        }
                        speedSum += v.magnitude;
                        samples++;

                        if (creature.Bodies[b].IsSleeping()) slept = true;

                        // The quantity the drive actually controls. Linear speed conflates
                        // "the joints are not turning" with "the joints are turning and the
                        // creature happens not to translate", and the two mean different things.
                        if (b > 0 && phenotype.Parts[b].JointType.DofCount() > 0)
                        {
                            jointRateSum +=
                                (creature.Bodies[b].angularVelocity -
                                 creature.Bodies[phenotype.Parts[b].ParentIndex].angularVelocity).magnitude;
                            jointSamples++;
                        }
                    }
                }

                float meanSpeed = samples > 0 ? (float)(speedSum / samples) : 0f;
                float meanJointRate = jointSamples > 0 ? (float)(jointRateSum / jointSamples) : 0f;

                // Sleep is a state PhysX reports, not something to infer from a speed, and it is
                // the only one of the two that is a harness fault. A slept scene means the
                // solver stopped and every timing measured nothing (logbook/0002). A creature
                // that is awake and still is a creature whose own body obstructs the joints it
                // grew, which is a real morphological fact and not the harness's business —
                // §5A prices it without needing to forbid it, because a jammed animal pays link
                // upkeep, idle capacity and neurons for degrees of freedom it cannot use, while
                // a plant of the same size pays none of that. Selection strips the links; it
                // does not need immobility declared illegal first.
                //
                // Immobile is also not the same as doomed. A rigid body drifting on a current,
                // carrying photosynthetic and absorptive cells, is a plant — which §5A.3 makes
                // reachable on purpose. So stillness is reported and never asserted on.
                bool awake = !slept;

                int buried = PhenotypeGeometry.BuriedPartPairs(phenotype);
                if (buried > 0)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: {buried} part pair(s) with one centre inside the other.");
                    allOk = false;
                }

                report.AppendLine(
                    $"| {seed} | {phenotype.PartCount} | {phenotype.MaxDepthReached} | " +
                    $"{phenotype.TotalDof} | {phenotype.TotalVolume:0.###} | " +
                    $"{PhenotypeGeometry.MirroredPartCount(phenotype)} | {buried} | " +
                    $"{meanSpeed:0.####} | {meanJointRate:0.####} | {MeanPower(phenotype):0.#} | " +
                    $"{(finite ? "yes" : "**NO**")} |");

                if (!finite)
                {
                    Debug.LogError($"[Evosim] seed {seed}: non-finite velocity — the articulation blew up.");
                    allOk = false;
                }

                if (!awake)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: PhysX put the articulation to sleep — every timing " +
                        "from here measures an idle solver (logbook/0002). Mean joint rate " +
                        $"{meanJointRate:0.######} rad/s across {creature.TotalDof} actuated DOF.");
                    allOk = false;
                }
                else if (creature.TotalDof > 0 && meanJointRate <= AwakeThreshold)
                {
                    // Reported, not a failure. See the note above `awake`.
                    Debug.Log(
                        $"[Evosim] seed {seed}: awake but still — {creature.TotalDof} actuated DOF at " +
                        $"mean link Power {MeanPower(phenotype):0.#} N·m turning at " +
                        $"{meanJointRate:0.######} rad/s. Its own body is in the way of the joints " +
                        "it grew; under §5A that is a cost it pays, not a genome to reject.");
                }

                if (phenotype.PartCount < 1)
                {
                    Debug.LogError($"[Evosim] seed {seed}: developed to nothing.");
                    allOk = false;
                }

                spawned++;
                creature.Destroy();
            }

            if (!CheckMomentumConservation(report, selfCollision: false)) allOk = false;
            bool contactOk = CheckMomentumConservation(report, selfCollision: true);
            if (!contactOk) allOk = false;

            // Only when it fails: the sweep costs four extra builds and answers a question
            // nobody has while the check is passing.
            if (!contactOk) ReportDepenetrationSweep(report, seed: 5);
            if (!CheckMinimalEnergyBalance(report)) allOk = false;
            if (!CheckEnergyBalance(report)) allOk = false;
            ReportEnergySinkSweep(report);
            ReportLimitSweep(report);
            ReportTimestepConvergence(report);
            if (!CheckSwimming(report)) allOk = false;
            // The report is only printed at the very end, so anything that throws here takes
            // every measurement above it down with it — which is how the first run of this
            // diagnostic produced a stack trace and no data at all. Caught, not swallowed:
            // the failure is still an error and still fails the run.
            try
            {
                ReportSelfCollisionEffect(report);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Evosim] self-collision diagnostic failed: {e}");
                report.AppendLine();
                report.AppendLine($"**self-collision diagnostic threw** — {e.GetType().Name}: {e.Message}");
                FluidEnvironment.ConfigureScene(selfCollision: true);
                allOk = false;
            }

            report.AppendLine();
            report.AppendLine(allOk
                ? $"**PASS** — {spawned} creatures built, geometry verified, actuated and torn down."
                : "**FAIL** — see errors above.");
            report.AppendLine();
            report.AppendLine(
                "The first table runs DRY — no fluid, so its mean-speed column is only an " +
                "awake-check that the drive reaches the joints, and its magnitude means " +
                "nothing. Momentum conservation is deliberately measured dry too, because it " +
                "is a statement about actuation being internal and drag would mask a leak. " +
                "The water table is the one with physical content.");

            Debug.Log(report.ToString());

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;

            return allOk;
        }
    }
}
