using System;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D270's carried leaf smell and current sense, the config's half: the six tunables, the three sensor channels
    /// and their numbers, the pool's order under each form, the setters' refusals, the world's refusals, and the
    /// round trip through the config file. The grid, the step and the readings are other tests.
    /// </summary>
    public class LeafPlumeConfigTests
    {
        private readonly ITestOutputHelper _output;

        public LeafPlumeConfigTests(ITestOutputHelper output) => _output = output;

        // ------------------------------------------------------------------ the defaults

        [Fact]
        public void TheDefaultsAreTheSpecsAndTheSwitchesAreOff()
        {
            var config = new RunConfig();
            Assert.Equal(LeafPlumeForm.None, config.LeafPlume);
            Assert.Equal(5f, config.LeafPlumeCellMetres);
            Assert.Equal(3000f, config.LeafPlumeLifetimeSeconds);
            Assert.Equal(1f, config.LeafPlumeHalfScale);
            Assert.False(config.CurrentSense);
            Assert.Equal(0.01f, config.CurrentSenseHalfSpeedMetresPerSecond);
        }

        [Fact]
        public void TheSixTunablesAreInTheSenseGroupAndEachMovesTheHash()
        {
            string plain = new RunConfig().Hash();
            var moved = new[]
            {
                new RunConfig { LeafPlume = LeafPlumeForm.Strength },
                new RunConfig { LeafPlume = LeafPlumeForm.Gradient },
                new RunConfig { LeafPlumeCellMetres = 10f },
                new RunConfig { LeafPlumeLifetimeSeconds = 1500f },
                new RunConfig { LeafPlumeHalfScale = 2f },
                new RunConfig { CurrentSense = true },
                new RunConfig { CurrentSenseHalfSpeedMetresPerSecond = 0.02f },
            };

            foreach (RunConfig c in moved) Assert.NotEqual(plain, c.Hash());
            Assert.Equal(moved.Length, moved.Select(c => c.Hash()).Distinct().Count());

            foreach (string name in new[]
                     {
                         "LeafPlume", "LeafPlumeCellMetres", "LeafPlumeLifetimeSeconds", "LeafPlumeHalfScale",
                         "CurrentSense", "CurrentSenseHalfSpeedMetresPerSecond",
                     })
            {
                TunableEntry entry = ConfigSchema.Of(new RunConfig()).Single(e => e.Path == name);
                Assert.Equal("sense", entry.Group);
            }
        }

        [Fact]
        public void TheSixSurviveAJsonRoundTrip()
        {
            var config = new RunConfig
            {
                LeafPlume = LeafPlumeForm.Gradient,
                LeafPlumeCellMetres = 10f,
                LeafPlumeLifetimeSeconds = 1500f,
                LeafPlumeHalfScale = 0.25f,
                CurrentSense = true,
                CurrentSenseHalfSpeedMetresPerSecond = 0.02f,
            };

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config), out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());
            Assert.Equal(LeafPlumeForm.Gradient, back.LeafPlume);
            Assert.Equal(10f, back.LeafPlumeCellMetres);
            Assert.Equal(1500f, back.LeafPlumeLifetimeSeconds);
            Assert.Equal(0.25f, back.LeafPlumeHalfScale);
            Assert.True(back.CurrentSense);
            Assert.Equal(0.02f, back.CurrentSenseHalfSpeedMetresPerSecond);
            Assert.Equal(config.SensorPool(), back.SensorPool());
        }

        // ------------------------------------------------------------------ the channels

        [Fact]
        public void TheChannelNumbersAreFixedForGood()
        {
            // A port id hashes the enum's integer, so these never move once a genome carries them.
            Assert.Equal(19, (int)SensorChannel.LeafPlume);
            Assert.Equal(20, (int)SensorChannel.LeafPlumeGradient);
            Assert.Equal(21, (int)SensorChannel.GroundCurrent);

            // The anchoring pair keeps 17 and 18.
            Assert.Equal(17, (int)SensorChannel.Gripping);
            Assert.Equal(18, (int)SensorChannel.GripPull);
        }

        [Fact]
        public void TheIndexCountsAndTheSmellsAreAsSpecified()
        {
            Assert.Equal(1, SensorChannel.LeafPlume.IndexCount());
            Assert.Equal(3, SensorChannel.LeafPlumeGradient.IndexCount());
            Assert.Equal(3, SensorChannel.GroundCurrent.IndexCount());

            Assert.True(SensorChannel.LeafPlume.IsSmell());
            Assert.True(SensorChannel.LeafPlumeGradient.IsSmell());
            Assert.False(SensorChannel.GroundCurrent.IsSmell());

            // The smells that were smells still are, and nothing else became one.
            Assert.True(SensorChannel.LeafScent.IsSmell());
            Assert.True(SensorChannel.LeafScentDirection.IsSmell());
            Assert.True(SensorChannel.Scent.IsSmell());
            Assert.True(SensorChannel.Chemical.IsSmell());
            Assert.False(SensorChannel.Flow.IsSmell());
            Assert.False(SensorChannel.Gripping.IsSmell());
        }

        // ------------------------------------------------------------------ the pool

        [Fact]
        public void WithEverySwitchOffThePoolIsTheDefaultPoolItself()
        {
            Assert.Same(SensorChannels.DefaultPool, new RunConfig().SensorPool());

            // Turned on and off again, the cache is dropped and the answer is the same array.
            var config = new RunConfig();
            _ = config.SensorPool();
            config.LeafPlume = LeafPlumeForm.Strength;
            config.CurrentSense = true;
            Assert.NotSame(SensorChannels.DefaultPool, config.SensorPool());
            config.LeafPlume = LeafPlumeForm.None;
            config.CurrentSense = false;
            Assert.Same(SensorChannels.DefaultPool, config.SensorPool());
        }

        [Fact]
        public void ThePoolsOrderUnderEachForm()
        {
            SensorChannel[] plain = SensorChannels.DefaultPool;

            Assert.Equal(
                plain.Concat(new[] { SensorChannel.LeafPlume }),
                new RunConfig { LeafPlume = LeafPlumeForm.Strength }.SensorPool());

            Assert.Equal(
                plain.Concat(new[] { SensorChannel.LeafPlume, SensorChannel.LeafPlumeGradient }),
                new RunConfig { LeafPlume = LeafPlumeForm.Gradient }.SensorPool());

            Assert.Equal(
                plain.Concat(new[] { SensorChannel.GroundCurrent }),
                new RunConfig { CurrentSense = true }.SensorPool());

            Assert.Equal(
                plain.Concat(new[] { SensorChannel.LeafPlume, SensorChannel.GroundCurrent }),
                new RunConfig { LeafPlume = LeafPlumeForm.Strength, CurrentSense = true }.SensorPool());

            Assert.Equal(
                plain.Concat(new[]
                {
                    SensorChannel.LeafPlume, SensorChannel.LeafPlumeGradient, SensorChannel.GroundCurrent,
                }),
                new RunConfig { LeafPlume = LeafPlumeForm.Gradient, CurrentSense = true }.SensorPool());
        }

        [Fact]
        public void TheNewChannelsComeAfterTheLeafSmellAndTheAnchoringPair()
        {
            var table = CellTypeRegistry.Standard.Appending(new AnchoringCell(brainHold: true));
            var config = new RunConfig
            {
                CellTypes = table,
                SenseBedTouch = true,
                LeafScent = LeafScentForm.Direction,
                LeafPlume = LeafPlumeForm.Gradient,
                CurrentSense = true,
            };

            SensorChannel[] pool = config.SensorPool();
            Assert.Equal(SensorChannels.DefaultPool.Length + 7, pool.Length);
            Assert.Equal(SensorChannel.BedTouch, pool[pool.Length - 7]);
            Assert.Equal(SensorChannel.LeafScentDirection, pool[pool.Length - 6]);
            Assert.Equal(SensorChannel.Gripping, pool[pool.Length - 5]);
            Assert.Equal(SensorChannel.GripPull, pool[pool.Length - 4]);
            Assert.Equal(SensorChannel.LeafPlume, pool[pool.Length - 3]);
            Assert.Equal(SensorChannel.LeafPlumeGradient, pool[pool.Length - 2]);
            Assert.Equal(SensorChannel.GroundCurrent, pool[pool.Length - 1]);
        }

        [Fact]
        public void TheInputsAPartCarriesAreFourRealisticAndSevenOverpowered()
        {
            // The design counts inputs, not channels: the strength 1, the gradient 3, the current 3.
            int Inputs(RunConfig c) => c.SensorPool().Skip(SensorChannels.DefaultPool.Length).Sum(ch => ch.IndexCount());

            Assert.Equal(4, Inputs(new RunConfig { LeafPlume = LeafPlumeForm.Strength, CurrentSense = true }));
            Assert.Equal(7, Inputs(new RunConfig { LeafPlume = LeafPlumeForm.Gradient, CurrentSense = true }));
        }

        // ------------------------------------------------------------------ the setters

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void TheLifetimeTheHalfScaleAndTheHalfSpeedRefuseWhatIsNotFiniteAndAboveZero(float bad)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { LeafPlumeLifetimeSeconds = bad });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { LeafPlumeHalfScale = bad });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { CurrentSenseHalfSpeedMetresPerSecond = bad });

            var config = new RunConfig();
            Assert.Throws<ArgumentOutOfRangeException>(() => config.LeafPlumeLifetimeSeconds = bad);

            // A refused value leaves the old one.
            Assert.Equal(3000f, config.LeafPlumeLifetimeSeconds);
            Assert.Equal(1f, config.LeafPlumeHalfScale);
            Assert.Equal(0.01f, config.CurrentSenseHalfSpeedMetresPerSecond);
        }

        // ------------------------------------------------------------------ the world's refusals

        /// <summary>A box on the grid field in the shared volume, with the plume as given.</summary>
        private static RunConfig Box(LeafPlumeForm plume)
        {
            return new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Box,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 6400f,
                HorizontalPatches = 4,
                WorldDepthMetres = 20f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                MatterBudgetUnits = 1000f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                LeafPlume = plume,
            };
        }

        [Theory]
        [InlineData(LeafPlumeForm.Strength)]
        [InlineData(LeafPlumeForm.Gradient)]
        public void ThePlumeOnAGridInTheSharedBoxIsAccepted(LeafPlumeForm form)
        {
            Assert.NotNull(new World(Box(form), seed: 5));
        }

        [Fact]
        public void ThePlumeOffAGridOrOutOfSharedSpaceIsRefused()
        {
            RunConfig cells = Box(LeafPlumeForm.Strength);
            cells.FieldModel = MatterField.Cells;
            ArgumentException a = Assert.Throws<ArgumentException>(() => new World(cells, seed: 5));
            _output.WriteLine(a.Message);
            Assert.Contains("LeafPlume", a.Message);
            Assert.Contains("D270", a.Message);
            Assert.Contains("FieldModel Grid", a.Message);

            RunConfig tiled = Box(LeafPlumeForm.Gradient);
            tiled.SharedSpace = false;
            ArgumentException b = Assert.Throws<ArgumentException>(() => new World(tiled, seed: 5));
            _output.WriteLine(b.Message);
            Assert.Contains("SharedSpace", b.Message);

            // Off, the same configs build as they always did.
            cells.LeafPlume = LeafPlumeForm.None;
            Assert.NotNull(new World(cells, seed: 5));
        }

        [Fact]
        public void ThePlumeInATankIsRefusedAndTheMessageNamesD270()
        {
            RunConfig tank = Box(LeafPlumeForm.Strength);
            tank.WorldShape = WorldShape.Tank;
            tank.WorldAreaSquareMetres = 400f;
            tank.HorizontalPatches = 4;

            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(tank, seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("D270", e.Message);
            Assert.Contains("Tank", e.Message);

            // The same tank without the plume is the world it was.
            tank.LeafPlume = LeafPlumeForm.None;
            Assert.NotNull(new World(tank, seed: 5));
        }

        [Fact]
        public void TheDiscFarSmellWithThePlumeIsRefusedAndTheMessageNamesD270()
        {
            RunConfig both = Box(LeafPlumeForm.Strength);
            both.LeafScent = LeafScentForm.Strength;
            both.ReadAcrossSeams = true;
            both.MatterIslandWavelengthMetres = 8f;
            both.MatterIslandCover = 0.25f;
            both.MatterIslandDepthMetres = 6f;
            both.MatterInfluxPerSecond = 1.2f;
            both.MatterInfluxAt = MatterInflux.Islands;
            both.IslandFarSmell = IslandFarSmell.Leaves;
            both.IslandFarSmellBandMetres = 3f;
            both.IslandFarSmellReachMetres = 4f;

            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(both, seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("D270", e.Message);
            Assert.Contains("IslandFarSmell", e.Message);

            // The far smell alone, on the same world, is built.
            both.LeafPlume = LeafPlumeForm.None;
            Assert.NotNull(new World(both, seed: 5));
        }
    }
}
