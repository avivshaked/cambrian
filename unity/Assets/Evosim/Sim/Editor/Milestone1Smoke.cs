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
        private const float MomentumTolerance = 0.05f;

        /// <summary>Settling discarded before measuring displacement — DESIGN.md §5.5.</summary>
        private const float SettleSeconds = 1f;

        private const float SwimSeconds = 8f;

        /// <summary>
        /// A creature in water cannot keep accelerating: drag rises with the square of speed,
        /// so every gait has a terminal velocity. Exceeding this means the fluid model is
        /// adding energy rather than removing it.
        /// </summary>
        private const float RunawaySpeed = 25f;

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
        private static bool CheckMomentumConservation(StringBuilder report)
        {
            bool ok = true;
            report.AppendLine();
            report.AppendLine("### Momentum conservation — actuation must be internal");
            report.AppendLine("No gravity, no damping, no contact: |P|/m and |L|/m must stay ~0.");
            report.AppendLine();
            report.AppendLine("| seed | speed of COM m/s | specific ang. momentum m2/s | verdict |");
            report.AppendLine("|---|---|---|---|");

            for (ulong seed = 1; seed <= 6; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                var driver = new EffectorDriver(creature);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                // A constant, one-sided drive is the worst case: an oscillating signal can
                // hide a momentum leak by averaging it away over a cycle.
                for (int i = 0; i < scratch.Length; i++) scratch[i] = 1f;

                float t = 0f;
                for (int s = 0; s < MomentumSteps; s++)
                {
                    driver.Drive(scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;
                }

                Momentum(creature, out Vector3 p, out Vector3 l, out float mass);
                float comSpeed = p.magnitude / Mathf.Max(1e-6f, mass);
                float specificL = l.magnitude / Mathf.Max(1e-6f, mass);

                bool pass = comSpeed < MomentumTolerance && specificL < MomentumTolerance;
                if (!pass)
                {
                    ok = false;
                    Debug.LogError(
                        $"[Evosim] seed {seed}: momentum not conserved — COM speed {comSpeed:0.####} m/s, " +
                        $"specific angular momentum {specificL:0.####} m2/s. Actuation is adding " +
                        "momentum from outside the creature.");
                }

                report.AppendLine(
                    $"| {seed} | {comSpeed:0.#####} | {specificL:0.#####} | {(pass ? "ok" : "**LEAK**")} |");

                creature.Destroy();
            }

            return ok;
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
        private static bool CheckSwimming(StringBuilder report)
        {
            bool ok = true;
            var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });

            report.AppendLine();
            report.AppendLine("### In water — DESIGN.md §5.2 drag, §5.5 displacement");
            report.AppendLine($"{fluid.Config}   {SwimSeconds:0.#} s after {SettleSeconds:0.#} s settling");
            report.AppendLine();
            report.AppendLine("| seed | parts | DOF | displacement m | speed m/s | peak speed m/s |");
            report.AppendLine("|---|---|---|---|---|---|");

            for (ulong seed = 1; seed <= Creatures; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                CreatureInstance creature = PhenotypeBuilder.Build(phenotype, Vector3.zero);
                fluid.ApplyAddedMass(creature);

                var driver = new EffectorDriver(creature);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                float t = 0f;
                int settleSteps = Mathf.RoundToInt(SettleSeconds / FixedDt);
                int swimSteps = Mathf.RoundToInt(SwimSeconds / FixedDt);

                for (int s = 0; s < settleSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;
                }

                Vector3 start = FluidEnvironment.CentreOfMass(creature);
                float peak = 0f;
                bool finite = true;

                for (int s = 0; s < swimSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature);
                    Physics.Simulate(FixedDt);
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

                report.AppendLine(
                    $"| {seed} | {phenotype.PartCount} | {phenotype.TotalDof} | " +
                    $"{displacement:0.###} | {speedAchieved:0.###} | {peak:0.##} |");

                creature.Destroy();
            }

            return ok;
        }

        private static bool Execute()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            Physics.gravity = Vector3.zero;
            Physics.IgnoreLayerCollision(
                PhenotypeBuilder.CreatureLayer, PhenotypeBuilder.CreatureLayer, true);

            var report = new StringBuilder();
            report.AppendLine("=== Milestone 1 smoke test ===");
            report.AppendLine($"Unity {Application.unityVersion}   dt={FixedDt}   {MeasureSteps} steps");
            report.AppendLine($"pipeline: {RenderPipelineName()}");
            report.AppendLine();
            report.AppendLine("| seed | parts | depth | DOF | volume m3 | mirrored | buried | mean speed m/s | finite |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");

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

                var driver = new EffectorDriver(creature);
                var scratch = new float[Mathf.Max(1, creature.TotalDof)];

                float t = 0f;
                for (int s = 0; s < WarmupSteps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;
                }

                double speedSum = 0;
                int samples = 0;
                bool finite = true;

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
                    }
                }

                float meanSpeed = samples > 0 ? (float)(speedSum / samples) : 0f;
                bool awake = meanSpeed > AwakeThreshold || creature.TotalDof == 0;

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
                    $"{meanSpeed:0.####} | {(finite ? "yes" : "**NO**")} |");

                if (!finite)
                {
                    Debug.LogError($"[Evosim] seed {seed}: non-finite velocity — the articulation blew up.");
                    allOk = false;
                }

                if (!awake)
                {
                    Debug.LogError(
                        $"[Evosim] seed {seed}: mean speed {meanSpeed:0.######} m/s with " +
                        $"{creature.TotalDof} actuated DOF — bodies are asleep or the drive is not reaching them.");
                    allOk = false;
                }

                if (phenotype.PartCount < 1)
                {
                    Debug.LogError($"[Evosim] seed {seed}: developed to nothing.");
                    allOk = false;
                }

                spawned++;
                creature.Destroy();
            }

            if (!CheckMomentumConservation(report)) allOk = false;
            if (!CheckSwimming(report)) allOk = false;

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
