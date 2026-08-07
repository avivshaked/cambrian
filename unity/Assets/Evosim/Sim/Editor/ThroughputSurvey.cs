using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// How many creatures can this machine simulate at once? — DESIGN.md §5A.9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §5A.9's population ceiling comes from Spike 01, which measured 128 creatures at
    /// 1.945 ms/step and then applied a guessed 3x penalty for "fluid drag, self-collision and
    /// brain evaluation, all of which now exist or are coming". Two of those three now exist,
    /// and parts are no longer all boxes. A guessed penalty against a measurement taken under a
    /// different configuration is not a measurement, and §5A.9 is what the whole ecosystem's
    /// feasibility rests on — so it is worth taking again against what actually runs.
    /// </para>
    /// <para>
    /// <b>Three things differ from the spike, and they push in opposite directions.</b>
    /// Self-collision adds broadphase pairs and contact solving. Fluid drag adds a managed C#
    /// pass over every panel of every part, every step — which is our code, not PhysX's, and is
    /// the part that scales with shape: a capsule at the default resolution emits about 1.7x the
    /// panels of a box. Against that, spheres and capsules are cheaper colliders than boxes.
    /// The net is not predictable from the parts, which is why it is measured.
    /// </para>
    /// <para>
    /// Drag and physics are timed separately. If the wall turns out to be our own drag loop
    /// rather than PhysX, that is a completely different problem with a completely different
    /// fix — panel resolution is a config knob, PhysX solver iterations are not — and a single
    /// combined figure could not tell the two apart.
    /// </para>
    /// <para>
    /// Creatures are tiled 100 m apart (§6.3) so they never touch. That is the real arrangement,
    /// and it is also what keeps them in separate PhysX solver islands, which is where §5A.9's
    /// claim about parallel scaling comes from. A run where they overlapped would measure
    /// something the simulator never does.
    /// </para>
    /// </remarks>
    public static class ThroughputSurvey
    {
        private static readonly int[] Populations = { 1, 8, 32, 64, 128, 256, 512 };

        private const float FixedDt = 0.01f;
        private const int WarmupSteps = 50;
        private const int MeasureSteps = 200;
        private const float TestSineHz = 1.2f;

        /// <summary>Metres between creatures — §6.3. Far enough that they never interact.</summary>
        private const float TileSpacing = 100f;

        [MenuItem("Evosim/Survey — How many creatures fit in real time?")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;

            var report = new StringBuilder();
            report.AppendLine("=== Throughput survey ===");
            report.AppendLine(
                $"Unity {Application.unityVersion}   dt={FixedDt}   " +
                $"{WarmupSteps} warmup + {MeasureSteps} measured steps");
            report.AppendLine("Self-collision ON, §5.2 drag applied, mixed shapes — the real configuration.");
            report.AppendLine();

            report.AppendLine(
                "| creatures | parts | DOF | panels | ms/step | drag ms | physics ms | " +
                "us per creature | real-time factor |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|");

            double baselinePerCreature = 0;

            foreach (int population in Populations)
            {
                Measure(population, report, ref baselinePerCreature);
            }

            report.AppendLine();
            report.AppendLine(
                "Real-time factor is simulated seconds per wall-clock second: above 1 means the " +
                "world runs faster than the clock. Per-creature cost falling as population rises " +
                "is PhysX parallelising across solver islands, which is §5A.9's central claim — " +
                "if it stays flat, that claim is wrong and the population ceiling is lower than " +
                "stated.");

            ReportShapeMix(report);
            ReportPanelCost(report);

            Debug.Log(report.ToString());

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }

        private static void Measure(int population, StringBuilder report, ref double baselinePerCreature)
        {
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var fluid = new FluidEnvironment(new FluidConfig { AddedMassCoefficient = 1f });
            var creatures = new CreatureInstance[population];
            var drivers = new EffectorDriver[population];
            var scratch = new float[population][];

            int totalParts = 0, totalDof = 0, totalPanels = 0;
            int side = Mathf.CeilToInt(Mathf.Sqrt(population));

            for (int i = 0; i < population; i++)
            {
                // Seeds cycle rather than restart, so a larger population is a superset of a
                // smaller one plus more of the same distribution — not a different mix of
                // creatures. Otherwise the sweep would confound population with body size.
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng((ulong)(i + 1)), RandomGenomeOptions.Default, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                var origin = new Vector3((i % side) * TileSpacing, 0f, (i / side) * TileSpacing);

                creatures[i] = PhenotypeBuilder.Build(phenotype, origin);
                fluid.ApplyAddedMass(creatures[i]);

                drivers[i] = new EffectorDriver(creatures[i], FixedDt);
                scratch[i] = new float[Mathf.Max(1, creatures[i].TotalDof)];

                totalParts += phenotype.PartCount;
                totalDof += creatures[i].TotalDof;
                totalPanels += PanelCount(phenotype);
            }

            float t = 0f;
            for (int s = 0; s < WarmupSteps; s++)
            {
                for (int i = 0; i < population; i++)
                {
                    drivers[i].DriveTestSine(t, TestSineHz, scratch[i]);
                    fluid.Apply(creatures[i]);
                }
                Physics.Simulate(FixedDt);
                t += FixedDt;
            }

            var drag = new Stopwatch();
            var physics = new Stopwatch();
            var total = Stopwatch.StartNew();

            for (int s = 0; s < MeasureSteps; s++)
            {
                drag.Start();
                for (int i = 0; i < population; i++)
                {
                    drivers[i].DriveTestSine(t, TestSineHz, scratch[i]);
                    fluid.Apply(creatures[i]);
                }
                drag.Stop();

                physics.Start();
                Physics.Simulate(FixedDt);
                physics.Stop();

                t += FixedDt;
            }

            total.Stop();

            double msPerStep = total.Elapsed.TotalMilliseconds / MeasureSteps;
            double dragMs = drag.Elapsed.TotalMilliseconds / MeasureSteps;
            double physicsMs = physics.Elapsed.TotalMilliseconds / MeasureSteps;
            double usPerCreature = msPerStep * 1000.0 / population;
            double realTime = FixedDt * 1000.0 / msPerStep;

            if (population == 1) baselinePerCreature = usPerCreature;

            string scaling = baselinePerCreature > 0
                ? $" ({usPerCreature / baselinePerCreature:0.00}x)"
                : "";

            report.AppendLine(
                $"| {population} | {totalParts} | {totalDof} | {totalPanels} | {msPerStep:0.###} | " +
                $"{dragMs:0.###} | {physicsMs:0.###} | {usPerCreature:0.#}{scaling} | " +
                $"{realTime:0.##}x |");

            for (int i = 0; i < population; i++) creatures[i].Destroy();
        }

        /// <summary>Panels one creature's parts present to the fluid each step.</summary>
        /// <remarks>
        /// Reported because it is the size of the managed loop, and unlike part count it depends
        /// on which shapes a creature happens to have grown. A population that drifts towards
        /// capsules gets slower for a reason nothing else in this table would show.
        /// </remarks>
        private static int PanelCount(Phenotype phenotype)
        {
            var scratch = new System.Collections.Generic.List<DragPanel>();
            int count = 0;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                scratch.Clear();
                PartShapeRegistry.Standard
                    .Resolve(part.ShapeId)
                    .AddPanels(part.HalfExtents, FluidConfig.DragOnly.PanelsPerAxis, scratch);

                count += scratch.Count;
            }

            return count;
        }

        private static void ReportShapeMix(StringBuilder report)
        {
            const int Seeds = 200;

            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (string id in PartShapeRegistry.Standard.Ids()) counts[id] = 0;

            int parts = 0;
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3);

                foreach (PhenotypePart part in Developer.Develop(genome, limits).Parts)
                {
                    counts[part.ShapeId]++;
                    parts++;
                }
            }

            report.AppendLine();
            report.AppendLine("### What the population is made of");
            report.AppendLine($"{Seeds} random viable genomes. Attributes the numbers above to a body mix.");
            report.AppendLine();
            report.AppendLine("| shape | parts | share |");
            report.AppendLine("|---|---|---|");

            foreach (var kv in counts)
            {
                report.AppendLine($"| {kv.Key} | {kv.Value} | {(float)kv.Value / parts:P1} |");
            }
        }

        private static void ReportPanelCost(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("### Drag cost per shape, at the default resolution");
            report.AppendLine(
                $"Panels a single part emits at PanelsPerAxis={FluidConfig.DragOnly.PanelsPerAxis}. " +
                "The drag loop is linear in this.");
            report.AppendLine();
            report.AppendLine("| shape | panels |");
            report.AppendLine("|---|---|");

            var scratch = new System.Collections.Generic.List<DragPanel>();
            var h = new Float3(0.2f, 0.5f, 0.2f);

            foreach (string id in PartShapeRegistry.Standard.Ids())
            {
                scratch.Clear();
                PartShapeRegistry.Standard
                    .Resolve(id)
                    .AddPanels(h, FluidConfig.DragOnly.PanelsPerAxis, scratch);

                report.AppendLine($"| {id} | {scratch.Count} |");
            }

            report.AppendLine();
            report.AppendLine(
                "Panel resolution is a FluidConfig knob and goes into the config hash, so it can " +
                "be lowered if this is the wall — but it is a physics parameter, not a " +
                "performance one: fewer panels is a coarser fluid model, and §5.3 already flags " +
                "the model as the exploitable part of the simulation.");
        }
    }
}
