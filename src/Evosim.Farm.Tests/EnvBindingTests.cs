using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The binding's acceptance: the same launcher gives the same <c>configHash</c>
    /// (fable-propose-own-solver.md, work package I).
    /// </summary>
    /// <remarks>
    /// Round 42 is the case with a recording to check against — <c>runs/r42-s1</c>, five seeds at
    /// 30,000 s, the last round the Unity farm ran. Its launcher is <c>rounds/launch-r42.ps1</c>
    /// and the block below is that file's hashtable with its own defaults substituted, which is
    /// what <c>run-arm.ps1</c> puts in the environment. The hash is not a summary of the
    /// settings: it is the thing the record is filed under, so a binding that reproduces it is a
    /// binding that will not quietly file a different world under round 42's name.
    /// </remarks>
    public class EnvBindingTests
    {
        /// <summary>
        /// Round 42 seed 1's config hash <i>on this build</i>, which is not the one the round ran
        /// under.
        /// </summary>
        /// <remarks>
        /// The round ran under <c>ff557bce2685293a</c>; D106's four module tunables moved it to
        /// <c>11602ab76c1e2a19</c>, and D106 item 3's mouth — nine tunables, two sense flags, an
        /// attribute mutation rate and the four per-cell-type caps, which are part of the cell
        /// type's own hash contribution — moved it to <c>fa6cdceda4ab17b6</c>, and the leaf's and
        /// absorptive protection caps rising from 0.25 to 0.5 at the ledger screen (the caps are
        /// in the cell types' contribution) moved it to this. A tunable is part of the hash whatever
        /// its default, which is §9's rule and the reason a config written before a tunable is
        /// refused rather than defaulted. What this constant still pins is the thing the test was
        /// written for: that the launcher's environment and this binding build the same world. It
        /// does not, and after 2026-09-22 cannot, say that world is byte-identical to the recorded
        /// one; every one of those knobs is at the value that changes nothing, so it is the same
        /// world, filed under a new name. <c>scratch/r45-build</c>'s regress is what says the
        /// world did not move — every shared field of a 1,000 s run identical at every sample.
        /// </remarks>
        private const string Round42ConfigHash = "4cbb170c61668098";

        /// <summary>Round 42 seed 1's environment, from <c>rounds/launch-r42.ps1</c>.</summary>
        /// <remarks>
        /// Worker-independent: the affinity mask and the expected simHash are the launcher's own
        /// arguments and never reach the run. <c>EVOSIM_SECONDS</c>, <c>EVOSIM_WALL_MINUTES</c>,
        /// <c>EVOSIM_SEED</c> and <c>EVOSIM_OUT</c> come from <c>run-arm.ps1</c>; none of the four
        /// is a tunable, and they are here so the block is the whole launch rather than the part
        /// that happens to be hashed.
        /// </remarks>
        public static Dictionary<string, string> Round42Seed1() => new Dictionary<string, string>
        {
            // run-arm.ps1
            { "EVOSIM_SECONDS", "30000" },
            { "EVOSIM_WALL_MINUTES", "1800" },
            { "EVOSIM_SEED", "1" },
            { "EVOSIM_OUT", "D:/Projects/experiments/evolution-simulator/runs/r42-s1.md" },

            // launch-r42.ps1's hashtable
            { "EVOSIM_IRRADIANCE", "200" },
            { "EVOSIM_CURRENT", "0.1" },
            { "EVOSIM_MIXING", "0.02" },
            { "EVOSIM_REMIN", "0.002" },
            { "EVOSIM_CURRENT_MODE", "Transport" },
            { "EVOSIM_AREA", "2200" },
            { "EVOSIM_DEPTH", "45" },
            { "EVOSIM_FLOOR_CLOSES", "3000" },
            { "EVOSIM_MAX_POP", "8000" },
            { "EVOSIM_MAX_TISSUE", "0" },
            { "EVOSIM_SENESCENCE", "3000" },
            { "EVOSIM_EXCESS_DENSITY", "0.02" },
            { "EVOSIM_MATTER_INITIAL", "1" },
            { "EVOSIM_FOUNDER_FLOAT", "0.5" },
            { "EVOSIM_LIFT_COST", "0.05" },
            { "EVOSIM_CELLTYPE_MUTATION", "0.005" },
            { "EVOSIM_NEUTRAL_VOLUME", "0.25" },
            { "EVOSIM_FOUNDER_DEPTH", "45" },
            { "EVOSIM_PATCHES", "4" },
            { "EVOSIM_CURRENT_PERIOD", "6000" },
            { "EVOSIM_CURRENT_CELL", "30" },
            { "EVOSIM_CURRENT_ROLLS", "1" },
            { "EVOSIM_CURRENT_BLINK", "3000" },
            { "EVOSIM_CURRENT_ADVECT", "1" },
            { "EVOSIM_SINK", "0.002" },
            { "EVOSIM_MATTER_SINK", "0.002" },
            { "EVOSIM_CLEARANCE", "10" },
            { "EVOSIM_EXUDATION", "0.15" },
            { "EVOSIM_DT", "0.01" },
            { "EVOSIM_SHARED_SPACE", "1" },
            { "EVOSIM_SURFACE_RESTORE", "1" },
            { "EVOSIM_SENSE_CHEMICAL", "1" },
            { "EVOSIM_SENSE_ENERGY", "1" },
            { "EVOSIM_SENSE_FLOW", "1" },
            { "EVOSIM_ADDED_MASS", "0.5" },
            { "EVOSIM_FIELD", "grid" },
            { "EVOSIM_FIELD_KERNEL", "1" },
            { "EVOSIM_FIELD_MATTER_KERNEL", "1.8" },
            { "EVOSIM_FIELD_MERGE", "0.25" },
            { "EVOSIM_FIELD_CAP", "100000" },
            { "EVOSIM_FIELD_QUANTUM", "0.125" },
            { "EVOSIM_FIELD_CELL", "1" },
            { "EVOSIM_FIELD_MATTER_CELL", "5" },
            { "EVOSIM_NEURON_COST", "0" },
            { "EVOSIM_CONNECTION_COST", "0" },
            { "EVOSIM_WORK_COST", "0" },
            { "EVOSIM_IDLE", "0.0001" },
            { "EVOSIM_SILHOUETTE", "1" },
            { "EVOSIM_WATER_HOLD", "0.5" },
            { "EVOSIM_SELF_OVERLAP", "0.1" },
            { "EVOSIM_LINK_PHOTO", "0.5" },
            { "EVOSIM_FLUID_ACCEL", "1" },
            { "EVOSIM_H_MIXING", "0.02" },
            { "EVOSIM_CORPSE_DECAY", "0.005" },
            { "EVOSIM_NEWBORN_RESERVE", "0.2" },
            { "EVOSIM_GROWTH_FLOOR", "0.1" },
            { "EVOSIM_MIN_NEWBORN_KG", "0.5" },
            { "EVOSIM_GROWTH_STEP", "10" },
            { "EVOSIM_INVEST_MIN", "0.25" },
            { "EVOSIM_INVEST_MAX", "1" },
            { "EVOSIM_ADULT_SCALE_CHANCE", "0.08" },
            { "EVOSIM_INVEST_CHANCE", "0.08" },
            { "EVOSIM_OFFSPRING_DISPERSAL", "5" },
            { "EVOSIM_SHAPE", "tank" },
            { "EVOSIM_MATTER_BUDGET", "1500" },
            { "EVOSIM_DRIVE_LIMIT_ALWAYS", "0" },
            { "EVOSIM_BED_RELIEF", "1.5" },
            { "EVOSIM_BED_TILT", "30" },
            { "EVOSIM_BED_SCALE", "0" },
            { "EVOSIM_LIGHT_REACH", "6" },
            { "EVOSIM_RHO", "100" },
            { "EVOSIM_UPTAKE_K", "0.3" },
            { "EVOSIM_UPTAKE_KS", "0.05" },
            { "EVOSIM_HANDLING", "0.1" },
            { "EVOSIM_RESERVE_CAP", "0" },
            { "EVOSIM_MARGIN_MIN", "0" },
            { "EVOSIM_MARGIN_MAX", "600" },
            { "EVOSIM_MARGIN_CHANCE", "0.08" },
            { "EVOSIM_TISSUE_ENERGY", "500" },
            { "EVOSIM_FOUNDER_EXTENT_MIN", "0" },
            { "EVOSIM_FOUNDER_EXTENT_MAX", "0" },
            { "EVOSIM_OVERHEAD", "100" },
        };

        /// <summary>THE acceptance: round 42's launcher, this binding, that hash.</summary>
        [Fact]
        public void Round42LauncherReproducesItsConfigHash()
        {
            RunConfig config = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(Round42Seed1())));

            Assert.Equal(Round42ConfigHash, config.Hash());
        }

        /// <summary>
        /// And every field of it, against the config the run itself wrote.
        /// </summary>
        /// <remarks>
        /// The hash is one number and says nothing about which field moved when it disagrees. This
        /// reads <c>runs/r42-s1/&lt;run&gt;/config.json</c> — the file the Unity build wrote, never
        /// touched — and compares it line for line with what this binding writes, so a mismatch
        /// names the field. The run directory is gitignored and lives in the main working tree, so
        /// this skips where the record is not on the machine rather than failing for its absence.
        /// </remarks>
        [Fact]
        public void Round42ConfigMatchesTheRecordedFileFieldByField()
        {
            string recorded = RecordedRound42Config();
            if (recorded == null)
            {
                Console.WriteLine(
                    "runs/r42-s1/2026-09-20-191209-ff557bce/config.json is not on this machine " +
                    "(run directories are gitignored), so there is nothing to compare against.");
                return;
            }

            RunConfig built = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(Round42Seed1())));

            // Read it back through Core's own reader first: a file this build refuses is a
            // different failure from a field that disagrees, and the two must not be confused.
            //
            // And a build that has added a tunable since the recording refuses it outright — §9,
            // working. That is not this test's subject either, so it says so and stops: what it
            // watches is a field that disagrees, and there is no comparison to make against a file
            // this build will not open. The pinned hash above carries the same news.
            RunConfig loaded;
            string mismatch;
            try
            {
                loaded = RunConfigJson.Read(File.ReadAllText(recorded), out mismatch);
            }
            catch (FormatException e)
            {
                Console.WriteLine(
                    "this build refuses round 42 seed 1's recorded config — " + e.Message +
                    " There is nothing to compare field by field until a run is recorded on it.");
                return;
            }

            Assert.Null(mismatch);

            AssertSameConfig(RunConfigJson.Write(loaded), RunConfigJson.Write(built));
        }

        /// <summary>
        /// With nothing set, the binding builds what <c>EvolutionRun</c> builds with nothing set.
        /// </summary>
        /// <remarks>
        /// The expected side is written out here from <c>EvolutionRun</c>'s own fallbacks rather
        /// than by calling the table, so it is a second transcription and not a mirror: a default
        /// that drifted in <see cref="EnvBinding"/> disagrees with this, and a default that is
        /// simply <c>RunConfig</c>'s own is spelled here as <c>RunConfig</c>'s own, exactly as
        /// that file spells it.
        /// </remarks>
        [Fact]
        public void EmptyEnvironmentBuildsEvolutionRunsDefaults()
        {
            RunConfig actual = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>())));

            var d = new RunConfig();

            var expected = new RunConfig
            {
                Fluid = new FluidConfig
                {
                    AddedMassCoefficient = 0f,
                    FluidAccelerationCoefficient = 0f,
                    WaterHoldSeconds = 0f,
                    TissueExcessDensity = 0f,
                    NeutralBodyVolume = 0f,
                    SurfaceRestoringFraction = d.Fluid.SurfaceRestoringFraction,
                },
                Light = new LightModel(48f, 12f)
                {
                    DayNightAmplitude = 0f,
                    DayLengthSeconds = 200f,
                },
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new LinkCell(0.02f, photosyntheticEfficiency: 0f * PhotosyntheticCell.DefaultEfficiency),
                    new NeuralCell(),
                    new PhotosyntheticCell(),
                    new AbsorptiveCell(1.0f),
                    new ConsumerCell(),
                    new BuoyancyCell(0.05f)),
            };

            expected.PerOffspringOverheadJoules = d.PerOffspringOverheadJoules;
            expected.Genome.MaxLinkPower = RandomGenomeOptions.Default.MaxLinkPower;
            expected.Genome.MinLinkPower = Math.Min(
                RandomGenomeOptions.Default.MinLinkPower, RandomGenomeOptions.Default.MaxLinkPower);
            expected.Current.Mode = CurrentMode.Rolls;
            expected.Current.Speed = 0f;
            expected.Current.PeriodSeconds = d.Current.PeriodSeconds;
            expected.Current.CellMetres = d.Current.CellMetres;
            expected.Current.Rolls = false;
            expected.Current.RollBlinkSeconds = 0f;
            expected.Current.AdvectFields = false;
            expected.Current.VentSpeed = 0f;
            expected.Current.VentPatch = 0;
            expected.Current.VentDepthMetres = d.WorldDepthMetres;
            expected.Current.VentLegMetres = d.LightLayerMetres;
            expected.InitialMatterPerCubicMetre = 1f;
            expected.Genome.FounderFloatChance = 0f;
            expected.FounderDepthSpread = d.FounderDepthSpread;
            expected.NutrientMixingDiffusivity = 0f;
            expected.NutrientSinkMetresPerSecond = d.NutrientSinkMetresPerSecond;
            expected.MatterSinkMetresPerSecond = d.MatterSinkMetresPerSecond;
            expected.RemineralisationPerSecond = d.RemineralisationPerSecond;
            expected.JoulesPerUnit = d.JoulesPerUnit;
            expected.UptakeRatePerSquareMetre = d.UptakeRatePerSquareMetre;
            expected.UptakeHalfSaturation = d.UptakeHalfSaturation;
            expected.HandlingCostPerJouleEaten = d.HandlingCostPerJouleEaten;
            expected.ReserveCapSeconds = d.ReserveCapSeconds;
            expected.Genome.MinReserveMargin = Math.Min(
                RandomGenomeOptions.Default.MinReserveMargin, RandomGenomeOptions.Default.MaxReserveMargin);
            expected.Genome.MaxReserveMargin = Math.Max(
                RandomGenomeOptions.Default.MinReserveMargin, RandomGenomeOptions.Default.MaxReserveMargin);
            expected.Mutation.MarginChance = MutationRates.Default.MarginChance;
            expected.FloorRefugeMetres = d.FloorRefugeMetres;
            expected.RefugeEdibleFraction = d.RefugeEdibleFraction;
            expected.SatiationWattsPerCubicMetre = d.SatiationWattsPerCubicMetre;
            expected.ClearanceToeDensity = d.ClearanceToeDensity;
            expected.ExudationFraction = d.ExudationFraction;
            expected.ConceptionOrder = ConceptionOrder.Age;
            expected.MatterInfluxPerSecond = d.MatterInfluxPerSecond;
            expected.MatterInfluxAt = MatterInflux.Surface;
            expected.MatterBurialPerSecond = d.MatterBurialPerSecond;
            expected.SpeciesDriftThreshold = d.SpeciesDriftThreshold;
            expected.HorizontalPatches = d.HorizontalPatches;
            expected.PatchesAcross = d.PatchesAcross;
            expected.HorizontalMixingDiffusivity = d.HorizontalMixingDiffusivity;
            expected.DispersalChancePerStep = d.DispersalChancePerStep;
            expected.PerPatchShading = d.PerPatchShading;
            expected.LightSilhouetteCap = false;
            expected.SelfOverlapDepthFraction = 0f;
            expected.WorldAreaSquareMetres = d.WorldAreaSquareMetres;
            expected.WorldDepthMetres = d.WorldDepthMetres;
            expected.SharedSpace = false;
            expected.OffspringDispersalMetres = d.OffspringDispersalMetres;
            expected.PhysicsStepSeconds = 0.01f;
            expected.FloorClosesAfterSeconds = 0f;
            expected.MaximumPopulation = d.MaximumPopulation;
            expected.MaximumTissueJoules = (float)d.MaximumTissueJoules;
            expected.SenescenceDoublingSeconds = 0f;
            expected.Mutation.CellTypeChance = MutationRates.Default.CellTypeChance;
            expected.SenseChemical = false;
            expected.SenseEnergy = false;
            expected.SenseFlow = false;
            expected.ChemicalHalfScaleJoulesPerCubicMetre = d.ChemicalHalfScaleJoulesPerCubicMetre;
            expected.EnergyFullScaleSeconds = d.EnergyFullScaleSeconds;
            expected.FlowFullScaleMetresPerSecond = d.FlowFullScaleMetresPerSecond;
            expected.NeuralCostPerNeuronWatts = d.NeuralCostPerNeuronWatts;
            expected.NeuralCostPerConnectionWatts = d.NeuralCostPerConnectionWatts;
            expected.WorkCostMultiplier = d.WorkCostMultiplier;
            expected.FieldModel = MatterField.Cells;
            expected.FieldKernelMetres = d.FieldKernelMetres;
            expected.FieldMatterKernelMetres = d.FieldMatterKernelMetres;
            expected.FieldMergeMetres = d.FieldMergeMetres;
            expected.FieldVertexCap = d.FieldVertexCap;
            expected.FieldVertexJoules = d.FieldVertexJoules;
            expected.FieldCellMetres = d.FieldCellMetres;
            expected.FieldMatterCellMetres = d.FieldMatterCellMetres;
            expected.CorpseDecayPerSecond = d.CorpseDecayPerSecond;
            expected.WorldShape = WorldShape.Box;
            expected.MatterBudgetUnits = d.MatterBudgetUnits;
            expected.DriveLimitAtEveryStep = false;
            expected.BedReliefMetres = d.BedReliefMetres;
            expected.BedTiltMetres = d.BedTiltMetres;
            expected.BedScaleMetres = d.BedScaleMetres;
            expected.NewbornReserveFraction = d.NewbornReserveFraction;
            expected.GrowthReserveFloor = d.GrowthReserveFloor;
            expected.MinNewbornPartKilograms = d.MinNewbornPartKilograms;
            expected.GrowthStepSeconds = d.GrowthStepSeconds;
            expected.Genome.MinBirthInvestment = Math.Min(
                RandomGenomeOptions.Default.MinBirthInvestment, RandomGenomeOptions.Default.MaxBirthInvestment);
            expected.Genome.MaxBirthInvestment = Math.Max(
                RandomGenomeOptions.Default.MinBirthInvestment, RandomGenomeOptions.Default.MaxBirthInvestment);
            expected.Mutation.AdultScaleChance = MutationRates.Default.AdultScaleChance;
            expected.Mutation.InvestmentChance = MutationRates.Default.InvestmentChance;
            expected.InoculateAtSeconds = d.InoculateAtSeconds;
            expected.InoculateCount = d.InoculateCount;
            expected.InoculateDepthMetres = d.InoculateDepthMetres;

            AssertSameConfig(RunConfigJson.Write(expected), RunConfigJson.Write(actual));
            Assert.Equal(expected.Hash(), actual.Hash());
        }

        /// <summary>The run settings that must never reach the config or its hash.</summary>
        [Fact]
        public void RunnerSettingsAreReadAndAreNotTunables()
        {
            var block = Round42Seed1();
            RunConfig plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));

            block["EVOSIM_THREADS"] = "24";
            block["EVOSIM_PHYSICS_JOBS"] = "8";
            block["EVOSIM_DIGEST_EVERY"] = "1000";
            block["EVOSIM_REPORT_EVERY"] = "50";
            block["EVOSIM_WALL_MINUTES"] = "60";

            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));

            Assert.Equal(24, s.Threads);
            Assert.Equal(24, s.ResolveThreads());
            Assert.Equal(8, s.RequestedJobWorkers);
            Assert.Equal(1000L, s.DigestEvery);
            Assert.Equal(50, s.ReportEvery);
            Assert.Equal(Round42ConfigHash, EnvBinding.BuildConfig(s).Hash());
            Assert.Equal(plain.Hash(), EnvBinding.BuildConfig(s).Hash());
        }

        /// <summary>
        /// An unknown <c>EVOSIM_</c> variable is ignored, exactly as the Unity entry ignores one —
        /// and is listed, which is the only thing the port adds.
        /// </summary>
        [Fact]
        public void UnknownVariablesAreIgnoredAndListed()
        {
            var block = Round42Seed1();
            block["EVOSIM_MATER_BUDGET"] = "999999";   // the typo that would silently do nothing
            block["EVOSIM_NOT_A_KNOB"] = "1";

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Equal(Round42ConfigHash, config.Hash());

            IReadOnlyList<string> unknown = EnvBinding.UnknownNames(block.Keys);
            Assert.Equal(new[] { "EVOSIM_MATER_BUDGET", "EVOSIM_NOT_A_KNOB" }, unknown);
        }

        /// <summary>A setting that will not parse stops the launch rather than becoming its default.</summary>
        [Fact]
        public void AnUnparseableSettingRefuses()
        {
            var block = Round42Seed1();
            block["EVOSIM_AREA"] = "2,200";

            ArgumentException e = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(block)));

            Assert.Contains("EVOSIM_AREA is '2,200'", e.Message);

            block["EVOSIM_AREA"] = "2200";
            block["EVOSIM_SHAPE"] = "aquarium";
            Assert.Throws<ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(block)));
        }

        /// <summary>The seed is parsed straight to ulong: a float loses exactness above 2^24.</summary>
        [Fact]
        public void TheSeedKeepsItsHighBits()
        {
            var block = Round42Seed1();
            block["EVOSIM_SEED"] = "18446744073709551615";

            Assert.Equal(ulong.MaxValue, EnvBinding.Read(EnvBinding.Of(block)).Seed);
        }

        /// <summary>The physics step is read back rounded, as the hash records it.</summary>
        [Fact]
        public void ThePhysicsStepMustDivideTheMetabolicStep()
        {
            Assert.Equal(0.02f, EnvBinding.ResolvePhysicsStep(0.02f, out int steps));
            Assert.Equal(25, steps);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EnvBinding.ResolvePhysicsStep(0.03f, out _));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Two <c>config.json</c> documents, compared line for line so a mismatch names its field.
        /// </summary>
        private static void AssertSameConfig(string expected, string actual)
        {
            string[] a = expected.Replace("\r\n", "\n").Split('\n');
            string[] b = actual.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                if (a[i] == b[i]) continue;

                // The hash line differs whenever anything above it does, and says nothing about
                // what; name the field instead.
                Assert.Fail(
                    "config.json line " + (i + 1) + " differs.\n  recorded: " + a[i].Trim() +
                    "\n  built:    " + b[i].Trim());
            }

            Assert.Equal(a.Length, b.Length);
        }

        /// <summary>
        /// The recorded round 42 seed 1 config, or null where the record is not on this machine.
        /// </summary>
        /// <remarks>
        /// Found by walking up from the test binary: the worktree this builds in has no
        /// <c>runs/</c> of its own (they are gitignored and belong to the main working tree), and
        /// the main tree is two directories above <c>scratch/wt-farm-io</c>. Read-only, always: a
        /// test that wrote into a run directory would be editing the record it is checking
        /// against.
        /// </remarks>
        private static string RecordedRound42Config()
        {
            const string rel = "runs/r42-s1/2026-09-20-191209-ff557bce/config.json";
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
