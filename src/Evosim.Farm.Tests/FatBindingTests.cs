using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D245 and D247 on the farm: the launcher names that reach the config (the energy sense's squash, the reserve cap's
    /// form, the fat cell and its room), the header's tokens for each, and the card's copy of the squash against the
    /// host's.
    /// </summary>
    public sealed class FatBindingTests
    {
        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        private static Dictionary<string, string> Round42With(params (string Name, string Value)[] settings)
        {
            Dictionary<string, string> block = EnvBindingTests.Round42Seed1();
            foreach ((string name, string value) in settings) block[name] = value;
            return block;
        }

        private static string Header(Dictionary<string, string> block)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 24,
                inoculumHashShort: null);

            return Report.HeaderLine(settings, config, space, "9.9.9.9");
        }

        // ------------------------------------------------------------------------ off

        [Fact]
        public void UnsetEverythingIsOffAndTheHeaderReadsAsItAlwaysDid()
        {
            RunConfig config = Built(EnvBindingTests.Round42Seed1());

            Assert.Equal(EnergySenseSquash.Tanh, config.EnergySenseSquash);
            Assert.Equal(30f, config.EnergyHalfScaleSeconds);
            Assert.Equal(ReserveCapForm.Seconds, config.ReserveCapForm);
            Assert.False(config.CellTypes.Contains(CellTypeIds.Fat));
            Assert.Equal(CellTypeRegistry.Standard.Count, config.CellTypes.Count);

            string header = Header(EnvBindingTests.Round42Seed1());
            Assert.Contains(", reserveCap off, margin ", header);
            Assert.Contains(" J/m3, energy 3000 s, flow ", header);
            Assert.DoesNotContain("fat upkeep", header);
            Assert.DoesNotContain("body+store", header);
        }

        // ------------------------------------------------------------------------ D245

        [Fact]
        public void TheHalfScaleSquashAndItsSecondsReachTheConfigAndTheHeaderNamesThem()
        {
            var block = Round42With(("EVOSIM_ENERGY_SQUASH", "halfscale"), ("EVOSIM_ENERGY_HALF_SCALE", "45"));
            RunConfig config = Built(block);

            Assert.Equal(EnergySenseSquash.HalfScale, config.EnergySenseSquash);
            Assert.Equal(45f, config.EnergyHalfScaleSeconds);
            Assert.NotEqual(Built(EnvBindingTests.Round42Seed1()).Hash(), config.Hash());
            Assert.Contains(" J/m3, energy half 45 s, flow ", Header(block));

            // Unset seconds are the ruling's 30, and the tanh keeps its full scale in the header.
            Assert.Equal(30f, Built(Round42With(("EVOSIM_ENERGY_SQUASH", "halfscale"))).EnergyHalfScaleSeconds);
            Assert.Contains(" J/m3, energy half 30 s, flow ", Header(Round42With(("EVOSIM_ENERGY_SQUASH", "halfscale"))));
            Assert.Contains(" J/m3, energy 600 s, flow ", Header(Round42With(("EVOSIM_ENERGY_SQUASH", "tanh"), ("EVOSIM_ENERGY_FULL_SCALE", "600"))));

            // Case does not matter; a word that is neither is refused.
            Assert.Equal(EnergySenseSquash.HalfScale, Built(Round42With(("EVOSIM_ENERGY_SQUASH", "HalfScale"))).EnergySenseSquash);
            Assert.ThrowsAny<Exception>(() => Built(Round42With(("EVOSIM_ENERGY_SQUASH", "log"))));
            Assert.ThrowsAny<Exception>(() => Built(Round42With(("EVOSIM_ENERGY_HALF_SCALE", "0"))));
        }

        // ------------------------------------------------------------------------ D229, D247

        [Fact]
        public void TheCapFormReachesTheConfigAndTheHeaderNamesItWithS()
        {
            var store = Round42With(("EVOSIM_RESERVE_CAP", "3000"), ("EVOSIM_RESERVE_CAP_FORM", "bodyandstore"));
            RunConfig config = Built(store);

            Assert.Equal(ReserveCapForm.BodyAndStore, config.ReserveCapForm);
            Assert.Equal(3000f, config.ReserveCapSeconds);
            Assert.Contains(", reserveCap 3000 s body+store, margin ", Header(store));

            // The recorded form with a cap keeps its recorded token.
            var seconds = Round42With(("EVOSIM_RESERVE_CAP", "3000"));
            Assert.Equal(ReserveCapForm.Seconds, Built(seconds).ReserveCapForm);
            Assert.Contains(", reserveCap 3000 s, margin ", Header(seconds));

            // The form with no cap to read is built (the binding does not judge) and the world refuses it.
            RunConfig refused = Built(Round42With(("EVOSIM_RESERVE_CAP_FORM", "bodyandstore")));
            Assert.Throws<ArgumentException>(() => new World(refused, 1UL));

            Assert.ThrowsAny<Exception>(() => Built(Round42With(("EVOSIM_RESERVE_CAP_FORM", "percubic"))));
        }

        [Fact]
        public void FatIsAppendedLastWithItsOwnPricesWhateverTheTissueEnergyOverrideIsAndTheHeaderNamesThem()
        {
            // Round 42 sets EVOSIM_TISSUE_ENERGY, which writes one price into every type already in the table.
            Assert.Equal("500", EnvBindingTests.Round42Seed1()["EVOSIM_TISSUE_ENERGY"]);

            var block = Round42With(("EVOSIM_FAT", "1"));
            RunConfig config = Built(block);

            Assert.Equal(CellTypeRegistry.Standard.Count + 1, config.CellTypes.Count);
            Assert.Equal(CellTypeRegistry.Standard.Count, config.CellTypes.IndexOf(CellTypeIds.Fat));

            CellType fat = config.CellTypes.Resolve(CellTypeIds.Fat);
            Assert.Equal(0.5f, fat.UpkeepWattsPerCubicMetre);
            Assert.Equal(2000f, fat.TissueEnergyPerCubicMetre);
            Assert.Equal(50000f, fat.StoreRoomJoulesPerCubicMetre);
            Assert.Equal(500f, config.CellTypes.Resolve(CellTypeIds.Structural).TissueEnergyPerCubicMetre);

            string header = Header(block);
            Assert.Contains(", reserveCap off, fat upkeep 0.5 W/m3 tissue 2000 J/m3 room 50000 J/m3, margin ", header);

            var room = Round42With(("EVOSIM_FAT", "1"), ("EVOSIM_FAT_ROOM", "40000"));
            Assert.Equal(40000f, Built(room).CellTypes.Resolve(CellTypeIds.Fat).StoreRoomJoulesPerCubicMetre);
            Assert.Contains(" room 40000 J/m3, margin ", Header(room));

            // The room alone, without the switch, adds nothing.
            Assert.False(Built(Round42With(("EVOSIM_FAT_ROOM", "40000"))).CellTypes.Contains(CellTypeIds.Fat));

            // Both of D165's and D247's appended types can share a table, anchoring first.
            RunConfig both = Built(Round42With(("EVOSIM_FAT", "1"), ("EVOSIM_ANCHORING", "1")));
            Assert.Equal(CellTypeRegistry.Standard.Count, both.CellTypes.IndexOf(CellTypeIds.Anchoring));
            Assert.Equal(CellTypeRegistry.Standard.Count + 1, both.CellTypes.IndexOf(CellTypeIds.Fat));
        }

        // ------------------------------------------------------------------------ the stats row

        /// <summary>One cube of fat, 0.25 m a side.</summary>
        private static Genome FatBox()
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(0.25f, 0.25f, 0.25f),
                JointType = JointType.Fixed,
                RecursiveLimit = 1,
                CellTypeId = CellTypeIds.Fat,
                Power = 0f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));

            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Nodes.Add(node);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 10f };
            return genome;
        }

        private static JsonNode LastRow(string name, RunConfig config, bool withFat)
        {
            string scratch = SimulationTests.Scratch(name);
            var world = new World(config, 1UL);
            if (withFat) world.Inoculate(FatBox(), count: 1, heightY: -2.5f);
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
            string statsPath;

            using (dir)
            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            {
                for (int i = 0; i < 20; i++) sim.Step();

                var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                new Sampler().Write(sim, dir, report.Columns);
                statsPath = Path.Combine(dir.Path, "stats.jsonl");
            }

            string[] rows = JsonlWriter.ReadRows(statsPath);
            return Json.Parse(rows[rows.Length - 1]);
        }

        [Fact]
        public void TheStatsRowCarriesTheCapAndFatColumnsOnlyWithTheirSwitches()
        {
            string[] cap = { "reserveTrimmed", "atCap" };
            string[] store = { "marginAboveCap" };
            string[] fat = { "fatCarriers", "fatVolumeShare" };
            string[] gut = { "gutWaitedAtCap", "gutLargest" };

            void Expect(JsonNode row, bool hasCap, bool hasStore, bool hasFat, bool hasGut)
            {
                foreach (string c in cap) Assert.Equal(hasCap, row.Has(c));
                foreach (string c in store) Assert.Equal(hasStore, row.Has(c));
                foreach (string c in fat) Assert.Equal(hasFat, row.Has(c));
                foreach (string c in gut) Assert.Equal(hasGut, row.Has(c));
            }

            // Nothing on: the row of every world before it.
            RunConfig off = RemainsFixtures.Config(contactEating: true);
            Expect(LastRow("fat-cols-off", off, withFat: false), false, false, false, false);

            // D098's cap alone: the trim and the bodies at the cap.
            RunConfig seconds = RemainsFixtures.Config(contactEating: true);
            seconds.ReserveCapSeconds = 600f;
            Expect(LastRow("fat-cols-seconds", seconds, withFat: false), true, false, false, false);

            // Fat in the table alone: the carriers and the share, with no cap columns.
            RunConfig table = RemainsFixtures.Config(contactEating: true);
            table.CellTypes = CellTypeRegistry.Standard.Appending(new FatCell());
            JsonNode tableRow = LastRow("fat-cols-table", table, withFat: true);
            Expect(tableRow, false, false, true, false);
            Assert.Equal(1.0, tableRow["fatCarriers"].AsDouble());
            Assert.Equal(1.0, tableRow["fatVolumeShare"].AsDouble(), 9);

            // Everything on, with the gut: all seven.
            RunConfig all = RemainsFixtures.Config(contactEating: true);
            all.ReserveCapSeconds = 600f;
            all.ReserveCapForm = ReserveCapForm.BodyAndStore;
            all.KillSwallowsBody = true;
            all.CellTypes = CellTypeRegistry.Standard.Appending(new FatCell());
            JsonNode allRow = LastRow("fat-cols-all", all, withFat: true);
            Expect(allRow, true, true, true, true);
            Assert.Equal(1.0, allRow["fatCarriers"].AsDouble());
            Assert.Equal(0.0, allRow["marginAboveCap"].AsDouble());
            Assert.Equal(0.0, allRow["gutLargest"].AsDouble());

            // The body-and-store form without the gut has no gut columns.
            RunConfig noGut = RemainsFixtures.Config(contactEating: true);
            noGut.ReserveCapSeconds = 600f;
            noGut.ReserveCapForm = ReserveCapForm.BodyAndStore;
            Expect(LastRow("fat-cols-nogut", noGut, withFat: false), true, true, false, false);
        }

        // ------------------------------------------------------------------------ the card's copy

        private static MethodInfo SquashOf(Type step)
        {
            MethodInfo method = step.GetMethod("Squash", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(method);
            Assert.Equal(4, method.GetParameters().Length);
            return method;
        }

        /// <summary>
        /// The kernel's <c>Squash</c> against the host's <c>CreatureSenses.SquashSeconds</c>, in both modes, at a spread
        /// of seconds from the newborn's to the founder's decades and past it, and at the non-finite answers. Found by
        /// reflection, as the guards on the card's maths are.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheCardsSquashAndTheHostsGiveTheSameFloatInBothModes(bool single)
        {
            Type step = single ? typeof(Evosim.Farm.Gpu.Sgl.WholeStep) : typeof(Evosim.Farm.Gpu.Dbl.WholeStep);
            MethodInfo card = SquashOf(step);

            float[] spread =
            {
                0f, -0f, 1e-6f, 0.001f, 0.5f, 1f, 5f, 15f, 29.999f, 30f, 30.001f, 45f, 100f, 300f, 1234.5f, 3000f, 6000f,
                30000f, 1e6f, 1e9f, 3.4e38f, -1e-6f, -1f, -15f, -45f, -300f, -3000f, -1e9f, -3.4e38f,
                float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            };

            foreach (EnergySenseSquash mode in new[] { EnergySenseSquash.Tanh, EnergySenseSquash.HalfScale })
            {
                foreach ((float fullScale, float halfScale) in new[] { (3000f, 30f), (600f, 45f), (1e-6f, 1e-6f), (3000f, 7.25f) })
                {
                    foreach (float seconds in spread)
                    {
                        float host = CreatureSenses.SquashSeconds(seconds, mode, fullScale, halfScale);
                        float onCard = (float)card.Invoke(
                            null, new object[] { seconds, fullScale, mode == EnergySenseSquash.HalfScale ? 1 : 0, halfScale });

                        // The half-scale expression is divisions and an addition and agrees to the bit in both flavours. The tanh
                        // is untouched: the double kernel's agrees to the bit, and the single kernel's, which takes the card's
                        // own float tanh (a new realisation of every seed, by design), may part from System.Math by an ulp or two.
                        int ulps = Math.Abs(BitConverter.SingleToInt32Bits(host) - BitConverter.SingleToInt32Bits(onCard));
                        bool same = ulps == 0 || (single && mode == EnergySenseSquash.Tanh && ulps <= 2) ||
                                    (float.IsNaN(host) && float.IsNaN(onCard));
                        Assert.True(
                            same,
                            $"{(single ? "single" : "double")} kernel, {mode}, S {seconds:R}, full {fullScale:R}, half {halfScale:R}: " +
                            $"host {host:R} against card {onCard:R}");
                    }
                }
            }
        }

        [Fact]
        public void TheCardsConstantsCarryTheModeAndTheSecondsFromTheSolverConfig()
        {
            (_, SolverConfig solver) = GpuTank.Build(perPart: true, reefs: false);

            // Off: the recorded squash, and the ruling's seconds sit unread beside it.
            var off = new Evosim.Farm.Gpu.GpuWorld(solver);
            Assert.Equal(0, off.EnergyHalfMode);
            Assert.Equal(30f, off.EnergyHalfK);
            Assert.Equal((float)Math.Max(1e-6, solver.EnergyFullScaleSeconds), off.EnergyScale);

            solver.EnergySenseSquash = EnergySenseSquash.HalfScale;
            solver.EnergyHalfScaleSeconds = 12.5;
            var on = new Evosim.Farm.Gpu.GpuWorld(solver);
            Assert.Equal(1, on.EnergyHalfMode);
            Assert.Equal(12.5f, on.EnergyHalfK);
            Assert.Equal(off.EnergyScale, on.EnergyScale);
        }
    }
}
