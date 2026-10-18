using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D190: the desert, the columns outside the islands' influx plan, buries at
    /// <see cref="RunConfig.MatterBurialDesertPerSecond"/> and the islands at
    /// <see cref="RunConfig.MatterBurialPerSecond"/>; below 0 the desert rate is off and the world is
    /// the recorded one.
    /// </summary>
    public class DesertBurialTests
    {
        private readonly ITestOutputHelper _output;

        public DesertBurialTests(ITestOutputHelper output) => _output = output;

        private const float Area = 2000f;
        private const float Depth = 20f;

        /// <summary>A still tank with islands on 2 m matter cells, fed on its islands, and the two burial rates.</summary>
        private static RunConfig Tank(float burial, float desert, float influx = 1f)
        {
            return new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = 4,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 10f,
                MatterIslandCover = 0.2f,
                MatterIslandDepthMetres = 6f,
                MatterInfluxPerSecond = influx,
                MatterInfluxAt = MatterInflux.Islands,
                MatterBurialPerSecond = burial,
                MatterBurialDesertPerSecond = desert,
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                MatterSinkMetresPerSecond = 0f,
                MatterMixingDiffusivity = 0f,
                NutrientSinkMetresPerSecond = 0f,
                NutrientMixingDiffusivity = 0f,
                HorizontalMixingDiffusivity = 0f,
                RemineralisationPerSecond = 0f,
                Current = null,
            };
        }

        /// <summary>Matter on every floor cell: a box over the bottom metre of the whole tank.</summary>
        private static void CoverTheFloor(GridField grid)
        {
            float span = 2f * grid.TankRadiusMetres;
            grid.DepositBox(500d, new Float3(0.5f * span, -Depth + 0.5f, 0.5f * span), new Float3(0.5f * span, 0.5f, 0.5f * span));
        }

        private static double FloorCell(GridField grid, int ix, int iz) => grid.JoulesAt(ix, grid.LowestLiveLayer(ix, iz), iz);

        [Fact]
        public void AnIslandColumnBuriesAtTheIslandRateAndADesertColumnAtTheDesertRate()
        {
            var world = new World(Tank(0.001f, 0.02f), seed: 3);
            var grid = (GridField)world.Matter;
            GridField.IslandPlan plan = world.IslandInflux;
            CoverTheFloor(grid);

            var before = new double[grid.CellsX, grid.CellsZ];
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
                if (grid.LowestLiveLayer(ix, iz) >= 0) before[ix, iz] = FloorCell(grid, ix, iz);

            double total = grid.TotalJoules;
            double taken = grid.BuryFloorByIslands(plan, 0.001, 0.02);

            int islands = 0, desert = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                if (grid.LowestLiveLayer(ix, iz) < 0) continue;
                Assert.True(before[ix, iz] > 0d);
                double kept = FloorCell(grid, ix, iz) / before[ix, iz];
                if (plan.IsIslandColumn(ix, iz))
                {
                    islands++;
                    Assert.Equal(1d - 0.001, kept, 12);
                }
                else
                {
                    desert++;
                    Assert.Equal(1d - 0.02, kept, 12);
                }
            }

            _output.WriteLine($"{islands} island columns, {desert} desert columns; {taken:F6} taken of {total:F3}");
            Assert.Equal(plan.ColumnCount, islands);
            Assert.True(desert > islands);
            Assert.Equal(total - taken, grid.TotalJoules, 9);
        }

        [Fact]
        public void EqualRatesBuryWhatTheOldBurialBuries()
        {
            var old = new World(Tank(0.01f, -1f), seed: 5);
            var split = new World(Tank(0.01f, 0.01f), seed: 5);
            CoverTheFloor((GridField)old.Matter);
            CoverTheFloor((GridField)split.Matter);

            for (int i = 0; i < 200; i++)
            {
                old.Step(0.1f);
                split.Step(0.1f);
            }

            var a = (GridField)old.Matter;
            var b = (GridField)split.Matter;
            _output.WriteLine($"buried {old.MatterBuriedTotal:R} and {split.MatterBuriedTotal:R}; stock {a.TotalJoules:R} and {b.TotalJoules:R}");
            Assert.True(old.MatterBuriedTotal > 1d);
            // The old draw asks for a float of each patch's floor times the fraction, so the two
            // agree to a float's rounding, about one part in ten million a step, not to the bit.
            Assert.InRange(Math.Abs(old.MatterBuriedTotal - split.MatterBuriedTotal), 0d, 1e-6 * old.MatterBuriedTotal);
            for (int ix = 0; ix < a.CellsX; ix++)
            for (int iz = 0; iz < a.CellsZ; iz++)
            {
                int lowest = a.LowestLiveLayer(ix, iz);
                if (lowest < 0) continue;
                double x = a.JoulesAt(ix, lowest, iz), y = b.JoulesAt(ix, lowest, iz);
                Assert.InRange(Math.Abs(x - y), 0d, 1e-6 * Math.Max(1e-12, Math.Abs(x)));
            }
        }

        [Fact]
        public void ADesertRateMovesTheHashAndOffIsTheDefault()
        {
            Assert.Equal(-1f, new RunConfig().MatterBurialDesertPerSecond);
            Assert.NotEqual(new RunConfig().Hash(), new RunConfig { MatterBurialDesertPerSecond = 0.005f }.Hash());
            Assert.NotEqual(new RunConfig { MatterBurialDesertPerSecond = 0.005f }.Hash(), new RunConfig { MatterBurialDesertPerSecond = 0.02f }.Hash());
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { MatterBurialDesertPerSecond = float.NaN });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { MatterBurialDesertPerSecond = float.PositiveInfinity });
        }

        [Fact]
        public void ADesertRateWithoutTheIslandsInfluxIsRefused()
        {
            var e = Assert.Throws<ArgumentException>(() => new World(Tank(0.001f, 0.005f, influx: 0f), seed: 3));
            _output.WriteLine(e.Message);
            Assert.Contains("MatterBurialDesertPerSecond", e.Message);

            // Off, the same world is built.
            Assert.NotNull(new World(Tank(0.001f, -1f, influx: 0f), seed: 3));
        }

        [Fact]
        public void TheBooksCloseWithADesertRate()
        {
            var world = new World(Tank(0.001f, 0.02f), seed: 3);
            CoverTheFloor((GridField)world.Matter);
            double start = world.Matter.TotalJoules;
            for (int i = 0; i < 100; i++) world.Step(0.1f);

            double expected = start + world.MatterInfluxedTotal - world.MatterBuriedTotal;
            _output.WriteLine($"stock {world.Matter.TotalJoules:R}, start {start:R} + in {world.MatterInfluxedTotal:R} - buried {world.MatterBuriedTotal:R}");
            Assert.True(world.MatterBuriedTotal > 0d);
            Assert.Equal(expected, world.Matter.TotalJoules, 6);
        }
    }
}
