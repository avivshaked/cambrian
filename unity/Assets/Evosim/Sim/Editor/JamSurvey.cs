using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// How much of a random population jams itself once its own parts can collide?
    /// </summary>
    /// <remarks>
    /// <para>
    /// DESIGN.md §4.2 permits parts to overlap, following Sims, on the grounds that enforcing
    /// non-overlap "kills too many viable genomes." §11.2 wants self-collision vibration caught
    /// as an exploit, which requires collision to be on. The two only coexist if turning
    /// collision on leaves most of the population able to move.
    /// </para>
    /// <para>
    /// That is a measurement, not an argument, and eight seeds cannot settle it — three of the
    /// eight in the Milestone 1 smoke test lost most of their range of motion, which is either
    /// a tail or a majority depending on a sample far too small to say.
    /// </para>
    /// <para>
    /// The number that decides it: the fraction of creatures retaining most of their joint
    /// travel. If jamming is a minority behaviour, selection removes it on its own — jamming no
    /// longer pays, so a jammed creature is simply a bad swimmer. If it is a majority, §4.2's
    /// overlap policy has to change and parts need gaps or connectors between them, which is a
    /// change to the encoding rather than to the physics configuration.
    /// </para>
    /// </remarks>
    public static class JamSurvey
    {
        private const int Seeds = 200;
        private const float Seconds = 4f;
        private const float FixedDt = 0.01f;

        /// <summary>Below this fraction of free-swinging travel, a creature counts as jammed.</summary>
        /// <remarks>
        /// Arbitrary, and the histogram is reported in full so the choice can be second-guessed.
        /// Half is the point where a creature has lost more range of motion than it kept.
        /// </remarks>
        private const float JammedBelow = 0.5f;

        [MenuItem("Evosim/Survey — How much of a population jams?")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;

            var report = new StringBuilder();
            report.AppendLine("=== Jamming survey ===");
            report.AppendLine($"{Seeds} random viable genomes, {Seconds:0.#} s each, dt={FixedDt}");
            report.AppendLine("Joint travel = total angle swept by every DOF. 'kept' compares");
            report.AppendLine("self-collision on against the same creature with it off.");
            report.AppendLine();

            var buckets = new int[5];              // >90, 70-90, 50-70, 25-50, <25
            var bucketNames = new[] { "90-100%+", "70-90%", "50-70%", "25-50%", "under 25%" };

            int jammed = 0;
            int measured = 0;
            int noTravel = 0;
            double keptTotal = 0;

            double dispOffTotal = 0;
            double dispOnTotal = 0;

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                Milestone1Smoke.RunOnce(phenotype, false, Seconds, out float travelOff, out float dispOff);
                Milestone1Smoke.RunOnce(phenotype, true, Seconds, out float travelOn, out float dispOn);

                // A creature that barely moves with collision OFF has no baseline to lose, so
                // its ratio is noise. Counted separately rather than averaged in — this is the
                // same silent-skip that made the momentum check blind to low-DOF creatures.
                if (travelOff < 1f)
                {
                    noTravel++;
                    continue;
                }

                float kept = travelOn / travelOff;
                measured++;
                keptTotal += kept;
                dispOffTotal += dispOff;
                dispOnTotal += dispOn;

                if (kept < JammedBelow) jammed++;

                if (kept >= 0.9f) buckets[0]++;
                else if (kept >= 0.7f) buckets[1]++;
                else if (kept >= 0.5f) buckets[2]++;
                else if (kept >= 0.25f) buckets[3]++;
                else buckets[4]++;
            }

            report.AppendLine("| range of motion kept | creatures | share |");
            report.AppendLine("|---|---|---|");
            for (int i = 0; i < buckets.Length; i++)
            {
                float share = measured > 0 ? (float)buckets[i] / measured : 0f;
                report.AppendLine($"| {bucketNames[i]} | {buckets[i]} | {share:P1} |");
            }

            report.AppendLine();
            report.AppendLine($"measured: {measured} of {Seeds}");
            report.AppendLine(
                $"excluded (barely actuated even with collision off): {noTravel} — " +
                "these have no baseline to lose, so their ratio would be noise");
            report.AppendLine($"mean kept: {(measured > 0 ? keptTotal / measured : 0):P1}");
            report.AppendLine(
                $"jammed (kept below {JammedBelow:P0}): {jammed} of {measured} " +
                $"({(measured > 0 ? (float)jammed / measured : 0):P1})");
            report.AppendLine();
            report.AppendLine(
                $"mean displacement — collision off {(measured > 0 ? dispOffTotal / measured : 0):0.###} m, " +
                $"on {(measured > 0 ? dispOnTotal / measured : 0):0.###} m");
            report.AppendLine();
            report.AppendLine(
                "Reading it: a minority jamming needs no change — jamming no longer pays, so a " +
                "jammed creature is just a bad swimmer and selection removes it. A majority " +
                "jamming means DESIGN.md §4.2's overlap policy is the problem and parts need " +
                "gaps between them, which is an encoding change.");

            ReportRuntimeOverlap(report);

            Debug.Log(report.ToString());

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }

        /// <summary>
        /// How deeply do parts sit inside each other <i>while moving</i>, and which pairs?
        /// </summary>
        /// <remarks>
        /// <para>
        /// A part inside its own parent and a part inside an unrelated part look identical on
        /// screen and mean opposite things. The first is permitted by §4.2 and is what makes
        /// joints movable at all — PhysX exempts directly-jointed links from colliding, so a
        /// child must be free to swing into its parent. The second is collision failing.
        /// </para>
        /// <para>
        /// <see cref="PhenotypeGeometry.MeasureOverlap"/> already separates these, but only on
        /// the developed phenotype, before anything moves. It reported unjointed overlap at a
        /// mean of 0.03% of volume — and parts were still visibly passing through each other,
        /// because the overlap was being created at runtime by joints sweeping parts together.
        /// A static measurement cannot see a dynamic fault; that mistake has been made once
        /// already here.
        /// </para>
        /// <para>
        /// Depth is reported relative to the smaller part's shortest half-extent, so 100% means
        /// one box is buried in the other by its own half-width — genuinely nested rather than
        /// touching.
        /// </para>
        /// </remarks>
        /// <summary>
        /// The part's own smallest half-width, in metres — what an overlap depth is measured
        /// against so that "buried" means the same thing for every shape.
        /// </summary>
        /// <remarks>
        /// Not the smallest half-extent. A sphere's radius is the <i>mean</i> of the three
        /// (§4.1), so on an elongated set of extents the shortest one is far smaller than
        /// anything the part actually has — which would inflate every sphere's reported overlap
        /// for a reason having nothing to do with how deeply it was overlapping.
        /// </remarks>
        private static float HalfThickness(PhenotypePart part)
        {
            Float3 h = part.HalfExtents;

            switch (part.ShapeId)
            {
                case ShapeIds.Sphere:
                    return SphereShape.Radius(h);

                case ShapeIds.Capsule:
                    return CapsuleShape.Radius(h);

                default:
                    return Mathf.Min(Mathf.Abs(h.X), Mathf.Min(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));
            }
        }

        private static void ReportRuntimeOverlap(StringBuilder report)
        {
            const int OverlapSeeds = 40;

            FluidEnvironment.ConfigureScene(selfCollision: true);

            float worstJointed = 0f, worstUnjointed = 0f;
            double sumJointed = 0, sumUnjointed = 0;
            int seedsWithUnjointed = 0;
            ulong worstUnjointedSeed = 0;

            for (ulong seed = 1; seed <= OverlapSeeds; seed++)
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

                // Collider, not BoxCollider: parts are boxes, spheres or capsules (§4.1), and
                // asking for the concrete type would silently hand back null for two shapes out
                // of three — reporting an overlap-free population that had simply not been
                // measured. ComputePenetration is shape-agnostic, so nothing below cares.
                var colliders = new Collider[creature.Bodies.Length];
                for (int i = 0; i < creature.Bodies.Length; i++)
                {
                    colliders[i] = creature.Bodies[i].GetComponent<Collider>();
                }

                float seedJointed = 0f, seedUnjointed = 0f;
                float t = 0f;

                int steps = Mathf.RoundToInt(Seconds / FixedDt);
                for (int s = 0; s < steps; s++)
                {
                    driver.DriveTestSine(t, TestSineHz, scratch);
                    fluid.Apply(creature);
                    Physics.Simulate(FixedDt);
                    t += FixedDt;

                    // Sampling every 10th step: penetration changes over joint timescales, not
                    // per step, and ComputePenetration over every pair every step dominates
                    // the run time.
                    if (s % 10 != 0) continue;

                    for (int a = 0; a < colliders.Length; a++)
                    {
                        for (int b = a + 1; b < colliders.Length; b++)
                        {
                            if (colliders[a] == null || colliders[b] == null) continue;

                            Transform ta = colliders[a].transform;
                            Transform tb = colliders[b].transform;

                            if (!Physics.ComputePenetration(
                                    colliders[a], ta.position, ta.rotation,
                                    colliders[b], tb.position, tb.rotation,
                                    out _, out float distance))
                            {
                                continue;
                            }

                            float smallest = Mathf.Min(
                                HalfThickness(phenotype.Parts[a]),
                                HalfThickness(phenotype.Parts[b]));

                            float fraction = distance / Mathf.Max(1e-5f, smallest);

                            bool jointed =
                                phenotype.Parts[a].ParentIndex == b ||
                                phenotype.Parts[b].ParentIndex == a;

                            if (jointed) seedJointed = Mathf.Max(seedJointed, fraction);
                            else seedUnjointed = Mathf.Max(seedUnjointed, fraction);
                        }
                    }
                }

                sumJointed += seedJointed;
                sumUnjointed += seedUnjointed;
                worstJointed = Mathf.Max(worstJointed, seedJointed);

                if (seedUnjointed > worstUnjointed)
                {
                    worstUnjointed = seedUnjointed;
                    worstUnjointedSeed = seed;
                }

                if (seedUnjointed > 0.05f) seedsWithUnjointed++;

                creature.Destroy();
            }

            report.AppendLine();
            report.AppendLine("### Runtime interpenetration, by pair type");
            report.AppendLine($"{OverlapSeeds} creatures, measured while moving. Depth as a share of the");
            report.AppendLine("smaller part's own half-thickness: 100% is one part buried by its own half-width.");
            report.AppendLine();
            report.AppendLine("| pair type | permitted? | mean worst depth | worst seen |");
            report.AppendLine("|---|---|---|---|");
            report.AppendLine(
                $"| parent/child | yes — §4.2, and required for the joint to move | " +
                $"{sumJointed / OverlapSeeds:P1} | {worstJointed:P1} |");
            report.AppendLine(
                $"| unrelated parts | no — this is collision failing | " +
                $"{sumUnjointed / OverlapSeeds:P1} | {worstUnjointed:P1} (seed {worstUnjointedSeed}) |");
            report.AppendLine();
            report.AppendLine(
                $"creatures with unrelated parts overlapping more than 5%: {seedsWithUnjointed} of {OverlapSeeds}");
            report.AppendLine();
            report.AppendLine(
                "Reading it: a large parent/child number with a near-zero unrelated number means " +
                "the boxes-inside-boxes on screen are the permitted kind, and the fix is a " +
                "connector at the joint — a visual change, not a physics fault. A large " +
                "unrelated number means collision is still not doing its job and no encoding " +
                "change should be attempted until it is.");
        }

        private const float TestSineHz = 0.8f;
    }
}
