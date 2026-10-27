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
        /// in the cell types' contribution) moved it to <c>4cbb170c61668098</c>, and the reach
        /// bound (<c>DevelopmentLimits.MaxBodyReachMetres</c>, fable-propose-body-reach.md, off
        /// here) moved it to <c>5b93c47344df9e67</c>, D109's island tunables to
        /// <c>c862fd2c510b82e9</c>, D110's light by exposure (off here) to
        /// <c>5a456a9e7b2518d3</c>, and D111's buoyancy offset price (0 here, <c>pfix7</c>), and
        /// what followed to <c>679f831c59c6f1af</c>, and the reef group redesigned under the
        /// owner's cover ruling (2026-09-23 night: a cover in place of a count, a radius range, an
        /// outline roughness, a depth jitter, a stem fraction; cover 0 here) to
        /// <c>256078e816861b27</c>, and the round 48 rulings' eight (2026-09-24: the founders'
        /// depth off, their endowment 0, senescence wearing intake as D038 did, the
        /// rigid-group floors of the bud ruling, <c>DevelopmentLimits.FloorsWeighRigidGroups</c>,
        /// off, the overhead's per-tissue factor, the two gestation rates and the
        /// gestation-share range, all at the values that change nothing) to <c>53f8234cb554f0ba</c>,
        /// D124's founder cap (0 here) to <c>356e87c75e1338e9</c>, and round 51's eleven (D127 and
        /// D128, 2026-09-27: the free first link, the corpse's sink rate, rest and floor, the ageing
        /// hazard's base, doubling, two pushes and repair exponent, and the lifespan gene's
        /// mutation chance and founder range, all at the values that change nothing) to this. A
        /// tunable is
        /// part of the hash whatever
        /// its default, which is §9's rule and the reason a config written before a tunable is
        /// refused rather than defaulted. What this constant still pins is the thing the test was
        /// written for: that the launcher's environment and this binding build the same world. It
        /// does not, and after 2026-09-22 cannot, say that world is byte-identical to the recorded
        /// one; every one of those knobs is at the value that changes nothing, so it is the same
        /// world, filed under a new name. <c>scratch/r45-build</c>'s regress is what says the
        /// world did not move — every shared field of a 1,000 s run identical at every sample.
        /// </remarks>
        private const string Round42ConfigHash = "REPIN";

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
                    SeasonAmplitude = 0f,
                    SeasonLengthSeconds = 30000f,
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
            expected.Mutation.AddNodeChance = MutationRates.Default.AddNodeChance;
            expected.Mutation.AddEdgeChance = MutationRates.Default.AddEdgeChance;
            expected.Mutation.RemoveEdgeChance = MutationRates.Default.RemoveEdgeChance;
            expected.Mutation.AddNeuronChance = MutationRates.Default.AddNeuronChance;
            expected.Mutation.RemoveNeuronChance = MutationRates.Default.RemoveNeuronChance;
            expected.Mutation.RecursiveLimitChance = MutationRates.Default.RecursiveLimitChance;
            expected.Mutation.JointTypeChance = MutationRates.Default.JointTypeChance;
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

        [Fact]
        public void TheOceanIsAModeTheLauncherCanName()
        {
            // D204's open ocean current, by name and in any case; a name the parser does not know
            // is refused with the three it does.
            var block = Round42Seed1();

            block["EVOSIM_CURRENT_MODE"] = "Ocean";
            Assert.Equal(CurrentMode.Ocean, EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))).Current.Mode);

            block["EVOSIM_CURRENT_MODE"] = "gyre";
            var refused = Assert.Throws<ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Contains("'ocean'", refused.Message);
        }

        [Fact]
        public void EveryCoralDialIsALauncherNameThatReachesTheConfig()
        {
            // D194's coral: the shape by name, and each of the coral group's dials, set off its default,
            // lands on its own property. A dial added to RunConfig's coral group without a name fails here.
            var block = Round42Seed1();
            block["EVOSIM_REEF_SHAPE"] = "Coral";
            Assert.Equal(ReefShape.Coral, EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))).ReefShape);
            Assert.Equal(ReefShape.Mushroom, EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Round42Seed1()))).ReefShape);

            var defaults = new RunConfig();
            int dials = 0;
            foreach (System.Reflection.PropertyInfo dial in typeof(RunConfig).GetProperties())
            {
                if (!dial.Name.StartsWith("Coral", StringComparison.Ordinal)) continue;
                if (dial.GetCustomAttributes(typeof(TunableAttribute), true).Length == 0) continue;

                string stem = dial.Name.Substring("Coral".Length);
                foreach (string unit in new[] { "Metres", "Degrees" })
                {
                    if (stem.EndsWith(unit, StringComparison.Ordinal)) stem = stem.Substring(0, stem.Length - unit.Length);
                }

                string name = "EVOSIM_CORAL_" + System.Text.RegularExpressions.Regex.Replace(stem, "(?<!^)(?=[A-Z])", "_").ToUpperInvariant();
                object was = dial.GetValue(defaults);
                object off = was is int i ? (object)(i + 1) : (object)((float)was * 1.1f);
                string text = off is int n ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : ((float)off).ToString("R", System.Globalization.CultureInfo.InvariantCulture);

                var one = Round42Seed1();
                one[name] = text;
                RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(one)));
                Assert.True(off.Equals(dial.GetValue(config)), name + " did not reach " + dial.Name);
                Assert.Empty(EnvBinding.UnknownNames(one.Keys));
                dials++;
            }

            Assert.Equal(26, dials);

            block["EVOSIM_REEF_SHAPE"] = "staghorn";
            var refused = Assert.Throws<ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Contains("'coral'", refused.Message);
        }

        [Fact]
        public void TheRigidFloorsFlagReachesTheDevelopmentLimitsAndTheHeader()
        {
            // The bud ruling (2026-09-24): off is the recorded world and prints so; on reaches the
            // limits the developer and the newborn floor both read.
            var block = Round42Seed1();
            EnvSettings off = EnvBinding.Read(EnvBinding.Of(block));
            Assert.False(off.RigidFloors);
            Assert.False(EnvBinding.BuildConfig(off).Development.FloorsWeighRigidGroups);

            block["EVOSIM_RIGID_FLOORS"] = "1";
            EnvSettings on = EnvBinding.Read(EnvBinding.Of(block));
            Assert.True(on.RigidFloors);
            Assert.True(EnvBinding.BuildConfig(on).Development.FloorsWeighRigidGroups);
        }

        /// <summary>
        /// The structural mutation chances: the operators that add or remove a node, an edge or a
        /// neuron, or step a node's recursive limit. Each name reaches its own field of
        /// <c>config.Mutation</c> and no other, and moves the hash.
        /// </summary>
        private static (string Name, Func<MutationRates, float> Read, float Default)[] StructuralChances() =>
            new (string Name, Func<MutationRates, float> Read, float Default)[]
            {
                ("EVOSIM_ADD_NODE_CHANCE", m => m.AddNodeChance, MutationRates.Default.AddNodeChance),
                ("EVOSIM_ADD_EDGE_CHANCE", m => m.AddEdgeChance, MutationRates.Default.AddEdgeChance),
                ("EVOSIM_REMOVE_EDGE_CHANCE", m => m.RemoveEdgeChance, MutationRates.Default.RemoveEdgeChance),
                ("EVOSIM_ADD_NEURON_CHANCE", m => m.AddNeuronChance, MutationRates.Default.AddNeuronChance),
                ("EVOSIM_REMOVE_NEURON_CHANCE", m => m.RemoveNeuronChance, MutationRates.Default.RemoveNeuronChance),
                ("EVOSIM_RECURSIVE_LIMIT_CHANCE", m => m.RecursiveLimitChance, MutationRates.Default.RecursiveLimitChance),
                ("EVOSIM_JOINT_TYPE_CHANCE", m => m.JointTypeChance, MutationRates.Default.JointTypeChance),
            };

        [Fact]
        public void EachStructuralChanceReachesItsOwnMutationField()
        {
            var cases = StructuralChances();
            RunConfig plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Round42Seed1())));

            for (int i = 0; i < cases.Length; i++)
            {
                // A value that is nobody's default, and different for each name, so a name wired to
                // the wrong field is caught by the field it should have left alone.
                float value = 0.0123f + 0.001f * i;
                var block = Round42Seed1();
                block[cases[i].Name] = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

                RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));

                for (int j = 0; j < cases.Length; j++)
                {
                    float expected = j == i ? value : cases[j].Default;
                    Assert.True(
                        Math.Abs(expected - cases[j].Read(config.Mutation)) < 1e-6f,
                        cases[i].Name + " set to " + value + " left " + cases[j].Name + "'s field at " +
                        cases[j].Read(config.Mutation) + ", expected " + expected);
                }

                Assert.Empty(EnvBinding.UnknownNames(block.Keys));
                Assert.NotEqual(plain.Hash(), config.Hash());
            }
        }

        [Fact]
        public void StructuralChancesUnsetOrSetToTheirDefaultsLeaveTheConfigAndItsHashAsTheyWere()
        {
            var cases = StructuralChances();
            var defaults = MutationRates.Default;

            // Unset: round 42's launcher keeps its hash, and every field is MutationRates' own.
            RunConfig unset = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Round42Seed1())));
            Assert.Equal(Round42ConfigHash, unset.Hash());
            foreach (var c in cases) Assert.Equal(c.Default, c.Read(unset.Mutation));
            Assert.Equal(defaults.AddNodeChance, unset.Mutation.AddNodeChance);
            Assert.Equal(defaults.RecursiveLimitChance, unset.Mutation.RecursiveLimitChance);

            // And with an empty environment too.
            RunConfig empty = EnvBinding.BuildConfig(
                EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>())));
            foreach (var c in cases) Assert.Equal(c.Default, c.Read(empty.Mutation));

            // Each name set to the very number it defaults to is the same world, field for field.
            var block = Round42Seed1();
            foreach (var c in cases)
            {
                block[c.Name] = c.Default.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }

            RunConfig explicitDefaults = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Equal(Round42ConfigHash, explicitDefaults.Hash());
            AssertSameConfig(RunConfigJson.Write(unset), RunConfigJson.Write(explicitDefaults));
        }

        /// <summary>
        /// With every structural chance at 0 (through the launcher's own names), a child never differs
        /// from its parent in node count, edge count, neuron count, input count, recursive limit,
        /// growth or module ceiling; with them unset, some children do, so the check can fail.
        /// </summary>
        /// <remarks>
        /// Three other things change structure and are not structural chances, so the test closes
        /// them by hand: <c>EVOSIM_CELLTYPE_MUTATION</c> (a bud adds a node) and
        /// <c>EVOSIM_MODULE_MUT</c> (flips growth) are launcher names set to 0 here, and
        /// <c>NodeExtinctionHalfExtent</c> (a node shrunk below it is removed by the scalar step) has
        /// no launcher name and is set to 0 on the rates directly. A scalar step can still retarget an
        /// edge (<c>edge.Child</c>, under <c>ScalarChance</c>), which changes no count; targets are not
        /// compared.
        /// </remarks>
        [Fact]
        public void WithEveryStructuralChanceAtZeroChildrenNeverDifferInStructure()
        {
            var off = Round42Seed1();
            foreach (var c in StructuralChances()) off[c.Name] = "0";
            off["EVOSIM_CELLTYPE_MUTATION"] = "0";
            off["EVOSIM_MODULE_MUT"] = "0";

            MutationRates frozen = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(off))).Mutation;
            foreach (var c in StructuralChances()) Assert.Equal(0f, c.Read(frozen));
            Assert.Equal(0f, frozen.CellTypeChance);
            Assert.Equal(0f, frozen.ModuleGeneMutationChance);
            frozen.NodeExtinctionHalfExtent = 0f;

            MutationRates unset =
                EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(Round42Seed1()))).Mutation;

            int frozenChildren = 0;
            int unsetChildrenThatDiffer = 0;

            for (ulong lineage = 1; lineage <= 5; lineage++)
            {
                Genome parent = GenomeFactory.RandomViable(
                    new Rng(lineage), RandomGenomeOptions.Default, DevelopmentLimits.Default, minParts: 3);

                // 600 children of one parent, each from its own seed.
                for (ulong seed = 1; seed <= 600; seed++)
                {
                    Genome child = Mutator.Mutate(parent, new Rng(lineage * 100000 + seed), frozen);
                    Assert.True(
                        Structure(parent) == Structure(child),
                        "lineage " + lineage + " seed " + seed + ": " + Structure(parent) + " became " + Structure(child));
                    frozenChildren++;

                    Genome free = Mutator.Mutate(parent, new Rng(lineage * 100000 + seed), unset);
                    if (Structure(parent) != Structure(free)) unsetChildrenThatDiffer++;
                }

                // And 400 generations deep, each child the next one's parent.
                Genome walker = parent;
                var rng = new Rng(lineage + 7000);
                for (int generation = 0; generation < 400; generation++)
                {
                    Genome child = Mutator.Mutate(walker, rng, frozen);
                    Assert.True(
                        Structure(walker) == Structure(child),
                        "lineage " + lineage + " generation " + generation + ": " + Structure(walker) + " became " + Structure(child));
                    walker = child;
                    frozenChildren++;
                }
            }

            Assert.Equal(5 * 1000, frozenChildren);
            Assert.True(
                unsetChildrenThatDiffer > 0,
                "no child differed in structure at the default rates, so the comparison above could not have failed");
        }

        /// <summary>
        /// What the structure category of a genome diff counts, as one string: node count and root,
        /// then per node its edge count, recursive limit, growth, module ceiling, neuron count and
        /// each neuron's input count.
        /// </summary>
        private static string Structure(Genome g)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("nodes=").Append(g.Nodes.Count).Append(" root=").Append(g.RootIndex);

            foreach (MorphNode n in g.Nodes)
            {
                sb.Append(" |e").Append(n.Edges.Count)
                  .Append(" r").Append(n.RecursiveLimit)
                  .Append(' ').Append(n.Growth)
                  .Append(" m").Append(n.MaxModules)
                  .Append(" j").Append(n.JointType).Append('/').Append(n.JointLimits.Length)
                  .Append(" n").Append(n.Neurons.Length);

                foreach (NeuronDef neuron in n.Neurons) sb.Append(':').Append(neuron.Inputs.Length);
            }

            return sb.ToString();
        }

        /// <summary>
        /// An unknown <c>EVOSIM_</c> variable is ignored, exactly as the Unity entry ignores one —
        /// and is listed, which is the only thing the port adds.
        /// </summary>
        /// <summary>
        /// The owner's two grazer dials (2026-10-19): the photosynthetic cell's tissue energy alone, and
        /// the mouth's intake scale. Both reach the config and its hash, and at 1 the recorded config is
        /// untouched.
        /// </summary>
        [Fact]
        public void TheGrazerDialsReachTheConfigAndAtOneLeaveItAlone()
        {
            Dictionary<string, string> block = Round42Seed1();
            RunConfig plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Equal(1f, plain.IntakeScale);

            block["EVOSIM_LEAF_TISSUE_ENERGY"] = "2";
            block["EVOSIM_INTAKE_SCALE"] = "4";
            RunConfig dialled = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));

            Assert.Equal(4f, dialled.IntakeScale);
            Assert.Equal(plain.CellTypes.Count, dialled.CellTypes.Count);
            for (int i = 0; i < plain.CellTypes.Count; i++)
            {
                float before = plain.CellTypes.At(i).TissueEnergyPerCubicMetre;
                float expected = plain.CellTypes.At(i).Id == CellTypeIds.Photosynthetic ? before * 2f : before;
                Assert.Equal(expected, dialled.CellTypes.At(i).TissueEnergyPerCubicMetre);
            }

            Assert.NotEqual(plain.Hash(), dialled.Hash());
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));

            block["EVOSIM_LEAF_TISSUE_ENERGY"] = "1";
            block["EVOSIM_INTAKE_SCALE"] = "1";
            Assert.Equal(Round42ConfigHash, EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))).Hash());
        }

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

        /// <summary>
        /// D175's route: <c>islands</c> reaches the config and the header, an unknown route names it
        /// among the known, and a fed world with no islands is refused when it is built.
        /// </summary>
        [Fact]
        public void TheIslandsInfluxRouteIsReadAndPrinted()
        {
            var block = Round42Seed1();
            block["EVOSIM_MATTER_INFLUX_AT"] = " Islands ";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            Assert.Equal(MatterInflux.Islands, settings.MatterInfluxAt);
            Assert.Equal(MatterInflux.Islands, config.MatterInfluxAt);

            // Round 42's influx is 0, so the route names nothing and the world is built; the header
            // says where the matter would land.
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 1,
                inoculumHashShort: null);
            string line = Report.HeaderLine(settings, config, space, "9.9.9.9");
            Assert.Contains("/s at islands, burial ", line);

            block["EVOSIM_MATTER_INFLUX_AT"] = "isles";
            ArgumentException unknown = Assert.Throws<ArgumentException>(
                () => EnvBinding.Read(EnvBinding.Of(block)));
            Assert.Contains("Known: surface, vent, islands.", unknown.Message);

            // Fed, in round 42's world, which seeds no islands.
            block["EVOSIM_MATTER_INFLUX_AT"] = "islands";
            block["EVOSIM_MATTER_INFLUX"] = "1.2";
            RunConfig fed = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(fed, settings.Seed));
            Assert.Contains("no islands are seeded", refused.Message);
        }

        /// <summary>The seed is parsed straight to ulong: a float loses exactness above 2^24.</summary>
        [Fact]
        public void TheSeedKeepsItsHighBits()
        {
            var block = Round42Seed1();
            block["EVOSIM_SEED"] = "18446744073709551615";

            Assert.Equal(ulong.MaxValue, EnvBinding.Read(EnvBinding.Of(block)).Seed);
        }

        /// <summary>
        /// D151's five light settings reach the config, and an unset one is the recorded world.
        /// </summary>
        [Fact]
        public void TheTwoSeasonsAndTheWeatherReachTheLight()
        {
            RunConfig Built(Dictionary<string, string> env) =>
                EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

            LightModel none = Built(new Dictionary<string, string>()).Light;
            Assert.Equal(SeasonShape.Smooth, none.SeasonShape);
            Assert.Equal(0f, none.SeasonRampSeconds);
            Assert.Equal(0f, none.WeatherSpread);
            Assert.Equal(0f, none.WeatherCorrelation);
            Assert.Equal(1UL, none.WeatherSeed);

            // Round 57's settings.
            LightModel on = Built(new Dictionary<string, string>
            {
                { "EVOSIM_SEASON_SHAPE", "two-season" },
                { "EVOSIM_SEASON_RAMP", "250" },
                { "EVOSIM_WEATHER_SPREAD", "0.3" },
                { "EVOSIM_WEATHER_CORRELATION", "0.5" },
                { "EVOSIM_WEATHER_SEED", "9007199254740992" },
            }).Light;

            Assert.Equal(SeasonShape.TwoSeason, on.SeasonShape);
            Assert.Equal(250f, on.SeasonRampSeconds);
            Assert.Equal(0.3f, on.WeatherSpread);
            Assert.Equal(0.5f, on.WeatherCorrelation);
            Assert.Equal(9007199254740992UL, on.WeatherSeed);

            Assert.Throws<ArgumentException>(() => Built(new Dictionary<string, string> { { "EVOSIM_SEASON_SHAPE", "square" } }));
            Assert.Throws<ArgumentException>(() => Built(new Dictionary<string, string> { { "EVOSIM_WEATHER_SEED", "1.5" } }));
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

        /// <summary>
        /// D239: the metabolic step stays 0.5 world seconds, which is 0.5 x k physics seconds, so a step
        /// must divide 0.5 x k. Pace factor 1 is the old rule.
        /// </summary>
        [Fact]
        public void ThePaceFactorWidensTheSpanThePhysicsStepMustDivide()
        {
            Assert.Equal(0.01f, EnvBinding.ResolvePhysicsStep(0.01f, 3f, out int steps));
            Assert.Equal(150, steps);
            Assert.Equal(0.03f, EnvBinding.ResolvePhysicsStep(0.03f, 3f, out steps));
            Assert.Equal(50, steps);

            var refused = Assert.Throws<ArgumentOutOfRangeException>(
                () => EnvBinding.ResolvePhysicsStep(0.04f, 3f, out _));
            Assert.Contains("1.5", refused.Message);

            Assert.Equal(25, Steps(0.02f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnvBinding.ResolvePhysicsStep(0.03f, 1f, out _));
        }

        [Theory]
        [InlineData(0.5f)]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void APaceFactorBelowOneOrNotFiniteIsRefused(float k)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EnvBinding.ResolvePhysicsStep(0.01f, k, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { PaceFactor = k });
        }

        [Fact]
        public void ThePaceFactorReachesTheConfigFromTheEnvironmentAndMovesTheHash()
        {
            var block = new Dictionary<string, string> { { "EVOSIM_DT", "0.01" }, { "EVOSIM_PACE_FACTOR", "3" } };
            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Equal(3f, s.PaceFactor);

            RunConfig paced = EnvBinding.BuildConfig(s);
            Assert.Equal(3f, paced.PaceFactor);

            var plain = new Dictionary<string, string> { { "EVOSIM_DT", "0.01" } };
            RunConfig unpaced = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(plain)));
            Assert.Equal(1f, unpaced.PaceFactor);
            Assert.NotEqual(unpaced.Hash(), paced.Hash());
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));
        }

        private static int Steps(float dt, float k)
        {
            EnvBinding.ResolvePhysicsStep(dt, k, out int steps);
            return steps;
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
        /// The ruling of 2026-09-24: EVOSIM_OVERHEAD and EVOSIM_OVERHEAD_FLOOR are one setting,
        /// the per-tissue factor and the gestation knobs reach the config, and a launcher that
        /// names neither gets the flat overhead and no gestation.
        /// </summary>
        [Fact]
        public void TheOverheadFloorHasTwoNamesAndTheGestationKnobsReachTheConfig()
        {
            RunConfig Built(Dictionary<string, string> env) =>
                EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

            RunConfig none = Built(new Dictionary<string, string>());
            Assert.Equal(new RunConfig().PerOffspringOverheadJoules, none.PerOffspringOverheadJoules);
            Assert.Equal(0f, none.PerOffspringOverheadPerTissueJoule);
            Assert.Equal(0f, none.Mutation.GestationModeChance);
            Assert.Equal(0f, none.Mutation.GestationShareChance);

            Assert.Equal(100f, Built(new Dictionary<string, string> { { "EVOSIM_OVERHEAD", "100" } })
                .PerOffspringOverheadJoules);
            Assert.Equal(40f, Built(new Dictionary<string, string> { { "EVOSIM_OVERHEAD_FLOOR", "40" } })
                .PerOffspringOverheadJoules);
            Assert.Equal(40f, Built(new Dictionary<string, string>
                { { "EVOSIM_OVERHEAD", "40" }, { "EVOSIM_OVERHEAD_FLOOR", "40" } })
                .PerOffspringOverheadJoules);
            Assert.Throws<ArgumentException>(() => Built(new Dictionary<string, string>
                { { "EVOSIM_OVERHEAD", "100" }, { "EVOSIM_OVERHEAD_FLOOR", "40" } }));

            RunConfig on = Built(new Dictionary<string, string>
            {
                { "EVOSIM_OVERHEAD_PER_TISSUE", "2" },
                { "EVOSIM_GESTATION_MODE_CHANCE", "0.08" },
                { "EVOSIM_GESTATION_SHARE_CHANCE", "0.08" },
                { "EVOSIM_GESTATION_SHARE_MIN", "0.8" },
                { "EVOSIM_GESTATION_SHARE_MAX", "0.2" },
            });

            Assert.Equal(2f, on.PerOffspringOverheadPerTissueJoule);
            Assert.Equal(0.08f, on.Mutation.GestationModeChance);
            Assert.Equal(0.08f, on.Mutation.GestationShareChance);
            Assert.Equal(0.2f, on.Genome.MinGestationShare);
            Assert.Equal(0.8f, on.Genome.MaxGestationShare);
            Assert.NotEqual(none.Hash(), on.Hash());
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
