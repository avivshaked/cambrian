using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// A long embodied run: does selection find a swimmer and keep one? — DESIGN.md §10 M4, §5A.6b.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question left open by logbook/0016.</b> A random genome swims at 0.485 m/s about one
    /// time in two hundred, so the mechanism works; what is unknown is whether two hundred
    /// simulated seconds was simply too little search, or whether the jointed share falls to zero
    /// however long the run goes. That is a measurement, not a design question, and the only way to
    /// take it is to run until thousands of creatures have been born.
    /// </para>
    /// <para>
    /// <b>Parameterised by environment rather than by code</b>, because Unity's
    /// <c>-executeMethod</c> takes no arguments and a sweep means running one build a dozen times
    /// with a single number changed. Everything that varies is read from the environment and
    /// written into the header of the output, so a result is never separated from the settings that
    /// produced it — the same reason §7 keeps a config hash.
    /// </para>
    /// <para>
    /// <b>Written incrementally, and a runaway is a result.</b> Rows are flushed as they are
    /// produced, so a killed run still leaves everything it measured — what §9 requires of
    /// <c>stats.jsonl</c>, for the same reason. <see cref="PopulationRunawayException"/> is caught
    /// and recorded rather than propagated: D021 makes it a measurement, locating the generous end
    /// of the calibration as precisely as extinction locates the lean end.
    /// </para>
    /// </remarks>
    public static class EvolutionRun
    {
        [MenuItem("Evosim/Run — long evolution run")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            float irradiance = Env("EVOSIM_IRRADIANCE", 48f);
            float budgetSeconds = Env("EVOSIM_SECONDS", 4000f);
            float wallMinutes = Env("EVOSIM_WALL_MINUTES", 30f);
            int reportEvery = (int)Env("EVOSIM_REPORT_EVERY", 200f);
            ulong seed = (ulong)Env("EVOSIM_SEED", 1f);

            // The two halves of what a joint costs to own before it does anything. §5A.10 marks
            // both unmeasured, and LinkCell's own documentation names the failure at each end:
            // "too low and capacity is effectively free again, too high and nothing can afford to
            // move". A calibration sweep found nothing with a joint alive at any irradiance from
            // 64 to 400 W/m2, so which end we are on is the question these make askable.
            float idle = Env("EVOSIM_IDLE", 0.02f);
            float maxPower = Env("EVOSIM_MAXPOWER", 120f);

            // The day/night cycle (D035). Mean-preserving, so amplitude 0 is exactly the acyclic
            // world every earlier number was measured in and the arms of a sweep stay comparable.
            float dayAmplitude = Env("EVOSIM_DAY_AMPLITUDE", 0f);
            float dayLength = Env("EVOSIM_DAY_LENGTH", 200f);

            string outPath = Environment.GetEnvironmentVariable("EVOSIM_OUT");
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(), "..", "runs", "evolution.md"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            var config = new RunConfig
            {
                Light = new LightModel(irradiance, 12f)
                {
                    DayNightAmplitude = dayAmplitude,
                    DayLengthSeconds = dayLength,
                },
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new LinkCell(idle),
                    new NeuralCell(),
                    new PhotosyntheticCell(),
                    new AbsorptiveCell(),
                    new ConsumerCell()),
            };

            config.Genome.MaxLinkPower = maxPower;
            var eco = new Ecosystem(config, seed);

            var report = new StringBuilder();
            report.AppendLine("# Evolution run — " + irradiance.ToString("0") + " W/m2");
            report.AppendLine();
            report.AppendLine(
                "Unity " + Application.unityVersion + " · dt=" + Ecosystem.FixedDt +
                " · metabolic step " + (Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt) +
                " s · seed " + seed + " · idle " + idle + " W/N·m · maxPower " + maxPower +
                " · day ±" + dayAmplitude + " over " + dayLength + " s" +
                " · configHash `" + config.Hash() + "`");
            report.AppendLine();
            report.AppendLine(Header());

            Flush(outPath, report);

            var clock = Stopwatch.StartNew();
            string ending = "budget reached";
            int metabolicSteps = 0;
            double bestSpeedEver = 0d;
            double bestSpeedAt = 0d;

            try
            {
                while (eco.World.ElapsedSeconds < budgetSeconds &&
                       clock.Elapsed.TotalMinutes < wallMinutes)
                {
                    if (!eco.Step()) continue;

                    metabolicSteps++;
                    // When, not only how much. A best that only ever occurs in the opening
                    // seconds is a transient; one that recurs late is a creature.
                    if (eco.MaxSpeed > bestSpeedEver)
                    {
                        bestSpeedEver = eco.MaxSpeed;
                        bestSpeedAt = eco.World.ElapsedSeconds;
                    }

                    if (metabolicSteps % reportEvery != 0) continue;

                    report.AppendLine(Row(eco));
                    Flush(outPath, report);

                    if (eco.World.Living.Count == 0)
                    {
                        ending = "extinct, and the floor could not refill it";
                        break;
                    }
                }
            }
            catch (PopulationRunawayException runaway)
            {
                // D021: not a crash. It locates the generous end of the calibration exactly as
                // extinction locates the lean end, and culling to fit a compute budget would be
                // selection performed by us.
                ending =
                    "RUNAWAY at t=" + runaway.ElapsedSeconds.ToString("0.#") + " s with " +
                    runaway.Population + " alive — light is covering upkeep so completely that " +
                    "nothing has to do anything";
            }

            clock.Stop();

            report.AppendLine();
            report.AppendLine("**Ended:** " + ending + ".");
            report.AppendLine();
            report.AppendLine(
                eco.Steps + " physics steps · " +
                eco.World.ElapsedSeconds.ToString("0.#") + " simulated seconds · " +
                eco.World.Births + " births · " +
                clock.Elapsed.TotalMinutes.ToString("0.#") + " min wall clock (" +
                (eco.World.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds)).ToString("0.#") +
                "x real time).");
            report.AppendLine();
            report.AppendLine(
                "**Fastest creature seen at any point: " + bestSpeedEver.ToString("0.####") +
                " m/s, at t=" + bestSpeedAt.ToString("0.#") + " s.**");

            Flush(outPath, report);
            Debug.Log(report.ToString());

            eco.DestroyAll();
            Physics.simulationMode = previousMode;
            Physics.gravity = previousGravity;
        }

        private static string Row(Ecosystem eco)
        {
            World world = eco.World;

            double spend = 0d, workSpend = 0d, depth = 0d, light = 0d, food = 0d;
            double travelled = 0d, age = 0d;
            int jointed = 0, dof = 0, absorptive = 0;
            int genMin = int.MaxValue, genMax = 0;

            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];

                spend += creature.Lifetime.Expenditure;
                workSpend += creature.Lifetime.Work;
                depth += creature.HeightY;

                // The two incomes, separately. Light is shallow and detritus sinks, so a moving
                // optimum — and therefore any reason to migrate — exists only to the extent that
                // both are worth having. If food income is a rounding error then the best depth is
                // the surface at every hour and a day/night cycle changes when creatures earn, not
                // where they should be (D035).
                light += creature.Lifetime.LightIncome;
                food += creature.Lifetime.FoodIncome;

                // How far a creature has actually moved from where it was born, against the
                // spread it was born into. Selection can only see swimming through this ratio: a
                // trait worth a tenth of a metre in a population scattered over twenty is a trait
                // whose signal is two orders of magnitude under the noise, and no number of
                // generations recovers it.
                travelled += Math.Abs(creature.HeightY - creature.BirthHeightY);
                age += creature.Age;

                // Counted, because "food income is 0%" has two completely different causes and
                // the share cannot tell them apart: nothing is trying to eat detritus, or plenty
                // is trying and there is nothing to eat. Founders draw absorptive one time in
                // four (RandomGenomeOptions.FounderCellTypes), so the first should be false — and
                // an assumption is exactly what wants checking here.
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId != CellTypeIds.Absorptive) continue;

                    absorptive++;
                    break;
                }

                int creatureDof = 0;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    creatureDof += part.JointType.DofCount();
                }

                if (creatureDof > 0) jointed++;
                dof += creatureDof;

                // §5A.6b's instrument: a minimum generation depth above zero means no living
                // creature is a floor spawn, which is the definition of a world running itself.
                if (creature.GenerationDepth < genMin) genMin = creature.GenerationDepth;
                if (creature.GenerationDepth > genMax) genMax = creature.GenerationDepth;
            }

            int alive = world.Living.Count;
            if (alive == 0) genMin = 0;

            // Spread, not only the mean, and it is the statistic a migration would show up in.
            // A population that has settled at one good depth and a population sloshing up and
            // down with the sun have the same mean at the moment you sample them and completely
            // different spreads — which is the same lesson as the mean speed that hid a 78x tail
            // (logbook/0016). Sampled across the population rather than over time, so one row is
            // one snapshot of how vertically spread the world is.
            double meanDepth = alive > 0 ? depth / alive : 0d;
            double variance = 0d;

            for (int i = 0; i < world.Living.Count; i++)
            {
                double d = world.Living[i].HeightY - meanDepth;
                variance += d * d;
            }

            double depthSd = alive > 1 ? Math.Sqrt(variance / (alive - 1)) : 0d;

            double workShare = spend > 0d ? workSpend / spend : 0d;
            double residual = world.EnergyIn > 0d ? 100d * world.AuditResidual / world.EnergyIn : 0d;
            double seconds = Ecosystem.StepsPerMetabolicStep * Ecosystem.FixedDt;

            var c = CultureInfo.InvariantCulture;

            // Built column by column rather than through a positional format string. That string
            // had reached twenty-five indices and desynchronised from its argument list the moment
            // two more measurements were added — a FormatException at the first row, which is the
            // benign version; the malign one is two columns swapping and every number staying
            // plausible. Pairing each header with its own value makes that impossible to express.
            var row = new List<string>
            {
                world.ElapsedSeconds.ToString("0", c),
                alive.ToString(c),
                world.Births.ToString(c),
                world.Deaths.ToString(c),
                "**" + jointed.ToString(c) + "**",
                (alive > 0 ? 100d * jointed / alive : 0d).ToString("0.#", c) + "%",
                (alive > 0 ? (double)dof / alive : 0d).ToString("0.##", c),
                eco.MeanSpeed.ToString("0.####", c),
                eco.MaxSpeed.ToString("0.####", c),
                (eco.WorkThisStep / seconds).ToString("0.##", c),
                (100d * workShare).ToString("0.#", c) + "%",
                "**" + (light + food > 0d ? 100d * food / (light + food) : 0d).ToString("0.##", c) + "%**",
                "**" + absorptive.ToString(c) + "**",
                "**" + world.Nutrients.TotalJoules.ToString("0.#", c) + "**",

                // Density where the creatures actually are, and how much of the world's detritus
                // has already fallen past them. Total joules cannot tell "there is no food" from
                // "the food is forty metres below everything that could eat it", and those two
                // want opposite responses.
                "**" + world.Nutrients.DensityAt((float)meanDepth).ToString("0.####", c) + "**",
                "**" + (world.Nutrients.TotalJoules > 0d
                    ? 100d * world.Nutrients.StockInLayer(world.Nutrients.LayerCount - 1) /
                      world.Nutrients.TotalJoules
                    : 0d).ToString("0.#", c) + "%**",

                meanDepth.ToString("0.#", c),
                "**" + depthSd.ToString("0.##", c) + "**",
                "**" + (alive > 0 ? travelled / alive : 0d).ToString("0.####", c) + "**",
                (alive > 0 ? age / alive : 0d).ToString("0.#", c),
                world.Field.DayFactor.ToString("0.##", c),
                genMin.ToString(c),
                genMax.ToString(c),
                residual.ToString("0.0000", c) + "%",
            };

            if (row.Count != Columns.Length)
            {
                throw new InvalidOperationException(
                    $"{row.Count} values against {Columns.Length} headers. A column was added at " +
                    "one end and not the other, and every row after it would be mislabelled.");
            }

            return "| " + string.Join(" | ", row) + " |";
        }

        /// <summary>Column headers. The single source of the table's shape — see <c>Row</c>.</summary>
        private static readonly string[] Columns =
        {
            "t (s)", "alive", "births", "deaths", "**jointed**", "jointed %", "mean dof",
            "mean m/s", "max m/s", "work J/s", "work share", "**food %**", "**absorpt**",
            "**detritus J**", "**J/m3 here**", "**% on floor**", "depth m", "**depth sd**",
            "**rise m**", "age s", "sun", "gen min", "gen max", "audit",
        };

        private static string Header() =>
            "| " + string.Join(" | ", Columns) + " |" + Environment.NewLine +
            "|" + string.Concat(System.Linq.Enumerable.Repeat("---|", Columns.Length)) + "|";

        private static void Flush(string path, StringBuilder report)
        {
            try
            {
                File.WriteAllText(path, report.ToString());
            }
            catch (IOException)
            {
                // A locked output file must not take the run down with it — the run is the
                // expensive part and the numbers are still in the log.
            }
        }

        private static float Env(string name, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrEmpty(raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }
    }
}
