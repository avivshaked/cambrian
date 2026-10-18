using System;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D192: <see cref="RunConfig.SeedOverrides"/>'s <c>landscape</c> entry draws the bed, the reefs and
    /// the islands apart from the world's seed, and with no entry the landscape is the world seed's, as
    /// every recorded world's.
    /// </summary>
    public class LandscapeSeedTests
    {
        private readonly ITestOutputHelper _output;

        public LandscapeSeedTests(ITestOutputHelper output) => _output = output;

        /// <summary>A bedded tank with reefs and islands on 2 m matter cells, fed on its islands.</summary>
        private static RunConfig Landscape(int landscapeSeed)
        {
            return new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 2000f,
                HorizontalPatches = 4,
                WorldDepthMetres = 20f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                BedReliefMetres = 1.5f,
                BedTiltMetres = 6f,
                ReefCover = 0.05f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 8f,
                MatterIslandCover = 0.25f,
                MatterIslandDepthMetres = 6f,
                MatterInfluxPerSecond = 1f,
                MatterInfluxAt = MatterInflux.Islands,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                SeedOverrides = landscapeSeed >= 0 ? new[] { "landscape=" + landscapeSeed } : Array.Empty<string>(),
            };
        }

        /// <summary>Every live column's floor layer, and every cell's matter, at founding.</summary>
        private static (int[] Floors, double[] Cells, bool[] Islands) Ground(World world)
        {
            var grid = (GridField)world.Matter;
            var floors = new int[grid.CellsX * grid.CellsZ];
            var islands = new bool[grid.CellsX * grid.CellsZ];
            var cells = new double[grid.CellsX * grid.CellsZ * grid.LayerCount];
            int n = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                int column = ix * grid.CellsZ + iz;
                floors[column] = grid.LowestLiveLayer(ix, iz);
                islands[column] = world.IslandInflux.IsIslandColumn(ix, iz);
                for (int iy = 0; iy < grid.LayerCount; iy++) cells[n++] = grid.JoulesAt(ix, iy, iz);
            }

            return (floors, cells, islands);
        }

        private static (double X, double Z)[] Reefs(World world) =>
            Enumerable.Range(0, world.Reefs.Count).Select(r => (world.Reefs.CentreX(r), world.Reefs.CentreZ(r))).ToArray();

        [Fact]
        public void AFixedLandscapeSeedGivesOneLandscapeUnderTwoWorldSeeds()
        {
            var a = new World(Landscape(42), seed: 6101);
            var b = new World(Landscape(42), seed: 6102);
            var ga = Ground(a);
            var gb = Ground(b);

            _output.WriteLine($"{ga.Islands.Count(x => x)} island columns, {a.Reefs.Count} reefs; landscape seeds {a.LandscapeSeed} and {b.LandscapeSeed}");
            Assert.Equal(42UL, a.LandscapeSeed);
            Assert.Equal(42UL, b.LandscapeSeed);
            Assert.True(ga.Islands.Count(x => x) > 0);
            Assert.True(a.Reefs.Count > 0);
            Assert.Equal(ga.Floors, gb.Floors);
            Assert.Equal(ga.Islands, gb.Islands);
            Assert.Equal(ga.Cells, gb.Cells);
            Assert.Equal(Reefs(a), Reefs(b));

            // The life is the world seed's own: the founders, who arrive on the first step, differ.
            a.Step(1f);
            b.Step(1f);
            Assert.NotEmpty(a.Living);
            Assert.NotEqual(
                a.Living.Select(o => GenomeJson.Write(o.Genome)).ToArray(),
                b.Living.Select(o => GenomeJson.Write(o.Genome)).ToArray());
        }

        [Fact]
        public void MinusOneIsTheWorldSeedsLandscape()
        {
            var unset = new World(Landscape(-1), seed: 6101);
            var named = new World(Landscape(6101), seed: 6101);
            var other = new World(Landscape(-1), seed: 6102);

            Assert.Equal(6101UL, unset.LandscapeSeed);
            Assert.Equal(Ground(unset).Cells, Ground(named).Cells);
            Assert.Equal(Ground(unset).Floors, Ground(named).Floors);
            Assert.Equal(Reefs(unset), Reefs(named));
            unset.Step(1f);
            named.Step(1f);
            Assert.NotEmpty(unset.Living);
            Assert.Equal(
                unset.Living.Select(o => GenomeJson.Write(o.Genome)).ToArray(),
                named.Living.Select(o => GenomeJson.Write(o.Genome)).ToArray());

            // Unset, two world seeds are two landscapes, as before.
            Assert.NotEqual(Ground(unset).Islands, Ground(other).Islands);
        }

        [Fact]
        public void TheSeedMovesTheHashAndNoneIsTheDefault()
        {
            Assert.Empty(new RunConfig().SeedOverrides);
            Assert.NotEqual(new RunConfig().Hash(), new RunConfig { SeedOverrides = new[] { "landscape=7" } }.Hash());
            Assert.NotEqual(
                new RunConfig { SeedOverrides = new[] { "landscape=7" } }.Hash(),
                new RunConfig { SeedOverrides = new[] { "landscape=8" } }.Hash());

            // One spelling: spaces and leading zeros do not make another config.
            var spelled = new RunConfig { SeedOverrides = new[] { " landscape = 007 " } };
            Assert.Equal(new[] { "landscape=7" }, spelled.SeedOverrides);
            Assert.Equal(new RunConfig { SeedOverrides = new[] { "landscape=7" } }.Hash(), spelled.Hash());
            Assert.True(spelled.TrySeedOverride("landscape", out ulong seed));
            Assert.Equal(7UL, seed);
            Assert.False(new RunConfig().TrySeedOverride("landscape", out _));
        }

        [Theory]
        [InlineData("dice=3")]
        [InlineData("landscape")]
        [InlineData("landscape=")]
        [InlineData("landscape=-1")]
        [InlineData("landscape=1.5")]
        [InlineData("landscape=9007199254740993")]
        [InlineData("=4")]
        public void AnUnknownNameOrABadValueIsRefused(string entry)
        {
            var e = Assert.Throws<ArgumentException>(() => new RunConfig { SeedOverrides = new[] { entry } });
            _output.WriteLine(e.Message);
        }

        [Fact]
        public void ADuplicateOrANullIsRefusedAndALoadRefusesAnUnknownName()
        {
            Assert.Throws<ArgumentException>(() => new RunConfig { SeedOverrides = new[] { "landscape=1", "landscape=2" } });
            Assert.Throws<ArgumentNullException>(() => new RunConfig { SeedOverrides = null });

            string text = RunConfigJson.Write(new RunConfig { SeedOverrides = new[] { "landscape=7" } });
            Assert.Contains("\"landscape=7\"", text);
            Assert.Equal(new[] { "landscape=7" }, RunConfigJson.Read(text).SeedOverrides);
            // The setter's refusal, through the loader's reflection.
            Exception e = Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(text.Replace("\"landscape=7\"", "\"founders=7\"")));
            Assert.Contains("'founders' is not a seed category", (e.InnerException ?? e).Message);
        }
    }
}
