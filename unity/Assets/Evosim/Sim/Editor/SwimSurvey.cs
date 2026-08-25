using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Can a creature driven by its own genome swim at all, and can it swim <i>somewhere</i>? —
    /// DESIGN.md §4.3, §4.4, §5.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two crossed factors, because the last round of this survey answered half a question.</b>
    /// Founders swim far worse than full random genomes — 1.5 parts against 5.8, a median 125×
    /// lower — which was the hypothesis and was confirmed. It was still not the answer: the
    /// founder <i>best</i> was 0.127 m/s against an embodied world whose fastest creature managed
    /// 0.0075, so morphology explained the median and left the gap (logbook/0018).
    /// </para>
    /// <para>
    /// What was left was that every brain in the world was open-loop. Sensors were declared,
    /// carried in the genome and drawn by founders, and nothing implemented
    /// <see cref="ISensorField"/>, so roughly half of every neuron's inputs read a constant zero.
    /// A creature like that can produce a stroke and cannot aim one — and in an economy whose only
    /// prize is a gradient in depth, undirected motion earns nothing on average while costing real
    /// work.
    /// </para>
    /// <para>
    /// <b>So distance is the wrong statistic on its own</b>, and reporting it alone is what let the
    /// previous round look answerable. A creature that swims fast in a circle and one that swims
    /// slowly upward are the same number in a distance column and opposite numbers in this world.
    /// Net vertical displacement is reported alongside it, signed, positive being toward the light.
    /// </para>
    /// <para>
    /// <b>What this can and cannot show.</b> With no selection, weights are random and no
    /// population should climb on average — a founder is as likely to be wired to dive as to rise.
    /// The blind arm is therefore a control rather than a straw man: it establishes that the two
    /// arms differ in gait and not in measurement, and that sensing does not simply break swimming.
    /// Whether sensing is <i>useful</i> is a question only an evolution run can answer, and that is
    /// the point — it is now a question that has an answer, which it did not before.
    /// </para>
    /// <para>
    /// Measured with no gravity and no contact, so the only thing that can move a creature is
    /// §5.2's drag acting asymmetrically over a stroke — the momentum check in the Milestone 1
    /// smoke test is what establishes that internal forces alone cannot.
    /// </para>
    /// </remarks>
    public static class SwimSurvey
    {
        private const float FixedDt = Ecosystem.FixedDt;
        private const int Population = 200;
        private const float Seconds = 20f;
        private const float TileSpacing = 100f;

        /// <summary>
        /// Depth every creature starts at, metres below the surface.
        /// </summary>
        /// <remarks>
        /// Mid-column rather than zero, and it is not cosmetic. <see cref="SensorChannel.Depth"/>
        /// reads a fraction of the world's depth, so a survey run at the surface pins every
        /// creature's depth sense to 0 and measures the blind arm twice while reporting it as two
        /// arms. It also leaves room to rise or sink without clamping at either end.
        /// </remarks>
        private const float StartDepth = 20f;

        /// <summary>
        /// Peak joint torque the drawn genomes may reach, overridable from the environment.
        /// </summary>
        /// <remarks>
        /// Here so the survey can be run either side of D032's change without editing code.
        /// <c>MaxLinkPower</c> multiplies straight into thrust and into the standing cost of
        /// owning a joint, so lowering it to make joints affordable also makes them weaker —
        /// which is a trade the calibration in logbook/0017 measured only one half of.
        /// Crucially it consumes the same single RNG draw at either setting, so the genomes
        /// sampled are the same genomes with different muscles, and the arms are comparable.
        /// </remarks>
        private static float MaxLinkPower()
        {
            string raw = Environment.GetEnvironmentVariable("EVOSIM_MAXPOWER");

            return !string.IsNullOrEmpty(raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : new RandomGenomeOptions().MaxLinkPower;
        }

        [MenuItem("Evosim/Survey — can anything swim?")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Swim survey ===");
            report.AppendLine($"MaxLinkPower {MaxLinkPower():0.#} N·m");
            report.AppendLine();

            Survey("founder", sensing: false, report);
            Survey("founder", sensing: true, report);
            Survey("randomViable", sensing: false, report);
            Survey("randomViable", sensing: true, report);

            report.AppendLine(
                "Speed says whether a stroke produces thrust. Vertical says whether it produces " +
                "thrust anywhere in particular, which is the only kind this world pays for. " +
                "Unselected, both arms should sit near zero vertical — a founder is as likely to " +
                "be wired to dive as to rise.");

            Debug.Log(report.ToString());
        }

        private static void Survey(string kind, bool sensing, StringBuilder report)
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var config = new RunConfig();
            config.Genome.MaxLinkPower = MaxLinkPower();

            var fluid = new FluidEnvironment(config.Fluid, config.Shapes);
            bool founders = kind == "founder";

            var creatures = new CreatureInstance[Population];
            var drivers = new EffectorDriver[Population];
            var brains = new Brain[Population];
            var sensors = new CreatureSensors[Population];
            var drive = new float[Population][];
            var start = new Vector3[Population];

            int built = 0, jointed = 0, totalParts = 0, totalDof = 0, sensorInputs = 0, inputs = 0;
            int side = Mathf.CeilToInt(Mathf.Sqrt(Population));

            for (int i = 0; i < Population; i++)
            {
                var limits = DevelopmentLimits.Default;
                var rng = new Rng((ulong)(i + 1));

                Genome genome = founders
                    ? GenomeFactory.Founder(rng, config.Genome)
                    : GenomeFactory.RandomViable(rng, config.Genome, limits, minParts: 3);

                Phenotype phenotype = Developer.Develop(genome, limits);
                var origin = new Vector3(
                    (i % side) * TileSpacing, -StartDepth, (i / side) * TileSpacing);

                creatures[i] = PhenotypeBuilder.Build(phenotype, origin, null, config.Shapes);
                fluid.ApplyAddedMass(creatures[i]);

                drivers[i] = new EffectorDriver(creatures[i], FixedDt);
                brains[i] = Brain.For(phenotype, genome.GlobalBrain);
                sensors[i] = new CreatureSensors(creatures[i], config.WorldDepthMetres);
                drive[i] = new float[Mathf.Max(1, brains[i].TotalDof)];
                start[i] = FluidEnvironment.CentreOfMass(creatures[i]);

                built++;
                if (brains[i].TotalDof > 0) jointed++;
                totalParts += phenotype.PartCount;
                totalDof += brains[i].TotalDof;

                // How much of the genome's wiring the sensing arm actually switches on. If this
                // is zero the two arms are the same experiment run twice, which is the failure
                // this project has had twice before under a different name.
                foreach (MorphNode node in genome.Nodes)
                {
                    foreach (NeuronDef neuron in node.Neurons)
                    {
                        foreach (NeuronInput input in neuron.Inputs)
                        {
                            inputs++;
                            if (input.Kind == NeuronInputKind.Sensor) sensorInputs++;
                        }
                    }
                }
            }

            float meanParts = (float)totalParts / Mathf.Max(1, built);
            float meanDof = (float)totalDof / Mathf.Max(1, built);

            int steps = Mathf.RoundToInt(Seconds / FixedDt);

            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < Population; i++)
                {
                    if (sensing) sensors[i].Sample();

                    brains[i].Step(FixedDt, drive[i], sensing ? sensors[i] : null);
                    drivers[i].Drive(drive[i]);
                }

                fluid.Apply(creatures, FixedDt);
                Physics.Simulate(FixedDt);
                fluid.Settle(creatures);

                for (int i = 0; i < Population; i++) drivers[i].Settle();
            }

            // ---- results

            var speeds = new float[Population];
            var vertical = new float[Population];

            for (int i = 0; i < Population; i++)
            {
                Vector3 end = FluidEnvironment.CentreOfMass(creatures[i]);

                speeds[i] = Vector3.Distance(end, start[i]) / Seconds;
                vertical[i] = end.y - start[i].y;
            }

            Array.Sort(speeds);
            Array.Sort(vertical);

            float meanVertical = 0f;
            for (int i = 0; i < Population; i++) meanVertical += vertical[i];
            meanVertical /= Mathf.Max(1, built);

            report.AppendLine(
                $"**{kind} — {(sensing ? "sensing" : "blind")}** — {built} genomes, {jointed} " +
                $"with a joint ({100f * jointed / built:0.#}%), {Seconds} s each. " +
                $"Mean {meanParts:0.##} parts, {meanDof:0.##} dof. " +
                $"{100f * sensorInputs / Mathf.Max(1, inputs):0.#}% of neuron inputs are sensors.");
            report.AppendLine();

            report.AppendLine("| statistic | speed m/s | net rise m |");
            report.AppendLine("|---|---|---|");
            report.AppendLine(
                $"| median | {speeds[Population / 2]:0.#####} | {vertical[Population / 2]:0.####} |");
            report.AppendLine(
                $"| 90th percentile | {speeds[(int)(Population * 0.9f)]:0.#####} | " +
                $"{vertical[(int)(Population * 0.9f)]:0.####} |");
            report.AppendLine(
                $"| best | {speeds[Population - 1]:0.#####} | {vertical[Population - 1]:0.####} |");
            report.AppendLine(
                $"| worst rise | — | {vertical[0]:0.####} |");

            int moving = 0;
            for (int i = 0; i < Population; i++)
            {
                if (speeds[i] > 0.01f) moving++;
            }

            report.AppendLine();
            report.AppendLine(
                $"{moving} of {built} exceeded 1 cm/s ({100f * moving / built:0.#}%). " +
                $"Mean net rise {meanVertical:0.####} m.");
            report.AppendLine();

            for (int i = 0; i < Population; i++) creatures[i].Destroy();

            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }
    }
}
