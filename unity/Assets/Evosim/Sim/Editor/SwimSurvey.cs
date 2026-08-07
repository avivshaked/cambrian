using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Can a creature driven by its own genome swim at all? — DESIGN.md §4.3, §5.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Separating two very different problems.</b> With the brain evaluator wired in, jointed
    /// creatures survive far longer than they did under the shared test sine — but mean speed in an
    /// embodied run is still about 0.0002 m/s, which is nothing, and the jointed share still
    /// declines. That has two possible causes and they need opposite responses:
    /// </para>
    /// <list type="number">
    /// <item><description><b>Random central pattern generators are bad swimmers.</b> Expected, and
    /// exactly what selection is for — the answer is a longer run, not a code change.</description></item>
    /// <item><description><b>Something structurally prevents thrust.</b> Then no amount of
    /// evolution helps, because the trait being selected for is unreachable — which is the
    /// situation the shared sine created and this was meant to end.</description></item>
    /// </list>
    /// <para>
    /// The two are distinguished by the <i>distribution</i> rather than the mean. If the best of a
    /// few hundred random genomes swims measurably, the mechanism works and the rest is search. If
    /// the best is also nothing, the mechanism does not.
    /// </para>
    /// <para>
    /// Measured as displacement of the centre of mass with no gravity and no contact, so the only
    /// thing that can move a creature is §5.2's drag acting asymmetrically over a stroke — the
    /// momentum check in the Milestone 1 smoke test is what establishes that internal forces alone
    /// cannot.
    /// </para>
    /// </remarks>
    public static class SwimSurvey
    {
        private const float FixedDt = Ecosystem.FixedDt;
        private const int Population = 200;
        private const float Seconds = 20f;
        private const float TileSpacing = 100f;

        [MenuItem("Evosim/Survey — can anything swim?")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var config = new RunConfig();
            var fluid = new FluidEnvironment(config.Fluid, config.Shapes);

            var creatures = new CreatureInstance[Population];
            var drivers = new EffectorDriver[Population];
            var brains = new Brain[Population];
            var drive = new float[Population][];
            var start = new Vector3[Population];

            int built = 0, jointed = 0;
            int side = Mathf.CeilToInt(Mathf.Sqrt(Population));

            for (int i = 0; i < Population; i++)
            {
                var limits = DevelopmentLimits.Default;
                Genome genome = GenomeFactory.RandomViable(
                    new Rng((ulong)(i + 1)), config.Genome, limits, minParts: 3);

                Phenotype phenotype = Developer.Develop(genome, limits);
                var origin = new Vector3((i % side) * TileSpacing, 0f, (i / side) * TileSpacing);

                creatures[i] = PhenotypeBuilder.Build(phenotype, origin, null, config.Shapes);
                fluid.ApplyAddedMass(creatures[i]);

                drivers[i] = new EffectorDriver(creatures[i], FixedDt);
                brains[i] = Brain.For(phenotype, genome.GlobalBrain);
                drive[i] = new float[Mathf.Max(1, brains[i].TotalDof)];
                start[i] = FluidEnvironment.CentreOfMass(creatures[i]);

                built++;
                if (brains[i].TotalDof > 0) jointed++;
            }

            int steps = Mathf.RoundToInt(Seconds / FixedDt);

            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < Population; i++)
                {
                    brains[i].Step(FixedDt, drive[i]);
                    drivers[i].Drive(drive[i]);
                }

                fluid.Apply(creatures, FixedDt);
                Physics.Simulate(FixedDt);
                fluid.Settle(creatures);

                for (int i = 0; i < Population; i++) drivers[i].Settle();
            }

            // ---- results

            var speeds = new float[Population];
            var report = new StringBuilder();

            report.AppendLine("=== Swim survey ===");
            report.AppendLine(
                $"{built} random genomes ({jointed} with a joint), {Seconds} s each, " +
                $"driven by their own brain. Unity {Application.unityVersion}, dt={FixedDt}.");
            report.AppendLine();

            for (int i = 0; i < Population; i++)
            {
                float distance = Vector3.Distance(
                    FluidEnvironment.CentreOfMass(creatures[i]), start[i]);

                speeds[i] = distance / Seconds;
            }

            Array.Sort(speeds);

            report.AppendLine("| statistic | m/s | body lengths per second |");
            report.AppendLine("|---|---|---|");
            report.AppendLine($"| median | {speeds[Population / 2]:0.#####} | — |");
            report.AppendLine($"| 90th percentile | {speeds[(int)(Population * 0.9f)]:0.#####} | — |");
            report.AppendLine($"| best | {speeds[Population - 1]:0.#####} | — |");

            int moving = 0;
            for (int i = 0; i < Population; i++)
            {
                if (speeds[i] > 0.01f) moving++;
            }

            report.AppendLine();
            report.AppendLine(
                $"**{moving} of {built} exceeded 1 cm/s** ({100f * moving / built:0.#}%).");
            report.AppendLine();
            report.AppendLine(
                "A best of essentially zero means the mechanism does not work and no amount of " +
                "selection will help, because the trait being selected for is unreachable. A best " +
                "that is measurably above the median means the mechanism works and what is missing " +
                "is search — which is the difference between a code change and a longer run.");

            Debug.Log(report.ToString());

            for (int i = 0; i < Population; i++) creatures[i].Destroy();

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }
    }
}
