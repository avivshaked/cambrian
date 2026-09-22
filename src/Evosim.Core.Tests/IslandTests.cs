using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D109: the matter seeded as islands and deserts by a seeded noise map, founders planted in
    /// them, and the light shaded by the same map.
    /// </summary>
    public class IslandTests
    {
        private readonly ITestOutputHelper _output;

        public IslandTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;   // a 20 × 20 m box
        private const float Depth = 10f;

        [Fact]
        public void TheNoiseIsAPropertyOfTheSeedAndStaysInRange()
        {
            var a = new GradientNoise(seed: 7UL, wavelength: 10f);
            var b = new GradientNoise(seed: 7UL, wavelength: 10f);
            var c = new GradientNoise(seed: 8UL, wavelength: 10f);

            int differ = 0;
            float lo = float.MaxValue, hi = float.MinValue;
            float loF = float.MaxValue, hiF = float.MinValue;

            for (int i = 0; i < 40; i++)
            for (int j = 0; j < 40; j++)
            {
                float x = i * 1.37f + 0.2f, z = j * 0.91f + 0.5f;

                Assert.Equal(a.Sample(x, z), b.Sample(x, z));
                Assert.Equal(a.Fbm(x, z), b.Fbm(x, z));
                if (a.Sample(x, z) != c.Sample(x, z)) differ++;

                float s = a.Sample(x, z);
                lo = Math.Min(lo, s); hi = Math.Max(hi, s);

                float f = a.Fbm(x, z);
                loF = Math.Min(loF, f); hiF = Math.Max(hiF, f);
            }

            _output.WriteLine($"one octave in [{lo:0.###}, {hi:0.###}], three in [{loF:0.###}, {hiF:0.###}], {differ} of 1600 differ across seeds");

            Assert.True(differ > 1500, "two seeds should give two maps");
            Assert.InRange(lo, -1.2f, 0f);
            Assert.InRange(hi, 0f, 1.2f);
            Assert.InRange(loF, -1.2f, 0f);
            Assert.InRange(hiF, 0f, 1.2f);

            // The lattice points of one octave are zero, by construction; a map that is zero
            // everywhere would pass the range check, so ask for spread too.
            Assert.True(hi - lo > 0.8f, "the map should span most of its range over a 40-wavelength sweep");
        }

        [Fact]
        public void TheIslandsHoldExactlyTheBudgetOverTheCoverAsked()
        {
            var field = new GridField(Area, 0f, Depth, 0f, 0f, 1, 2f);   // 10 × 5 × 10 cells
            var map = new GradientNoise(seed: 3UL, wavelength: 8f);

            float threshold = field.SeedIslands(totalJoules: 1000d, map: map.Fbm, cover: 0.25f);

            int columns = field.CellsX * field.CellsZ;
            int islands = 0;
            double total = 0d;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                double column = 0d;
                for (int iy = 0; iy < field.CellsY; iy++) column += field.JoulesAt(ix, iy, iz);
                total += column;
                if (column > 0d) islands++;

                // Uniform down the column: every cell of an island column holds the same.
                double first = field.JoulesAt(ix, 0, iz);
                for (int iy = 1; iy < field.CellsY; iy++) Assert.Equal(first, field.JoulesAt(ix, iy, iz), 9);
            }

            _output.WriteLine($"threshold {threshold:0.###}, {islands} of {columns} columns are islands, total {total:0.######}");

            Assert.Equal(1000d, total, 6);
            Assert.Equal(1000d, field.TotalJoules, 6);

            // A quarter of 100 columns, to within the rounding of the quantile.
            Assert.InRange(islands, 24, 26);

            // A plateau: most island columns hold the same, the fullest no more than the mean
            // island column by more than the shore's ramp allows.
            Assert.True(field.MaxColumnStock() >= 1000d / islands - 1e-6, "an island's centre holds at least the mean island column");
            Assert.True(field.MaxColumnStock() < 1.5d * 1000d / islands, "a plateau: the fullest column is near the mean island column");

            // Cut at a depth: the same islands, the matter in the top layers only, so the density
            // rises by the column over the depth. 3 m on 2 m cells is two layers (the cell whose
            // top is at 2 m lies above 3 m).
            var shallow = new GridField(Area, 0f, Depth, 0f, 0f, 1, 2f);
            shallow.SeedIslands(totalJoules: 1000d, map: map.Fbm, cover: 0.25f, depthMetres: 3f);
            Assert.Equal(1000d, shallow.TotalJoules, 6);

            double top = 0d, below = 0d;
            for (int ix = 0; ix < shallow.CellsX; ix++)
            for (int iz = 0; iz < shallow.CellsZ; iz++)
            {
                for (int iy = 0; iy < shallow.CellsY; iy++)
                {
                    if (iy < 2) top += shallow.JoulesAt(ix, iy, iz); else below += shallow.JoulesAt(ix, iy, iz);
                }

                // The same islands, the same column totals, the top cell five halves as dense.
                Assert.Equal(field.JoulesAt(ix, 0, iz) * shallow.CellsY / 2d, shallow.JoulesAt(ix, 0, iz), 6);
            }

            Assert.Equal(1000d, top, 6);
            Assert.Equal(0d, below, 9);
            Assert.Equal(field.MaxColumnStock(), shallow.MaxColumnStock(), 6);
        }

        private static RunConfig IslandWorld(float wavelength, float cover, float shade, float drift = 0f, float budget = 1000f) =>
            new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = 1,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                MatterBudgetUnits = budget,
                MatterIslandWavelengthMetres = wavelength,
                MatterIslandCover = cover,
                LightShadeDepth = shade,
                LightShadeDriftMetresPerHour = drift,
            };

        [Fact]
        public void AWorldSeedsItsIslandsFromItsSeedAndShadesItsLightByTheSameMap()
        {
            var world = new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0.8f), seed: 5);

            Assert.NotNull(world.IslandMap);
            Assert.Equal(1000d, world.Matter.TotalJoules, 5);

            // The shade map: darkest 0.2 of the light, brightest all of it, and the world's
            // incident watts scaled by its mean.
            float lo = 1f, hi = 0f;
            for (int i = 0; i < 20; i++)
            for (int j = 0; j < 20; j++)
            {
                float s = world.ShadeAt(i + 0.5f, j + 0.5f);
                lo = Math.Min(lo, s); hi = Math.Max(hi, s);
            }

            _output.WriteLine($"shade in [{lo:0.###}, {hi:0.###}], mean {world.MeanShade:0.###}, threshold {world.IslandThreshold:0.###}");

            // The darkest desert is 1 − depth to a float's rounding; the islands are lit in full.
            Assert.InRange(lo, 0.19f, 0.7f);
            Assert.Equal(1f, hi, 5);
            Assert.InRange(world.MeanShade, lo, hi);
            Assert.Equal(100f * Area * world.MeanShade, world.Field.IncidentWatts, 3);

            // The same seed is the same world; another seed is another map. Read over the whole
            // grid rather than at one point, since a point on an island reads 1 on any seed.
            var again = new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0.8f), seed: 5);
            var other = new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0.8f), seed: 6);
            Assert.Equal(world.IslandThreshold, again.IslandThreshold);
            Assert.Equal(ShadeSum(world), ShadeSum(again));
            Assert.NotEqual(ShadeSum(world), ShadeSum(other));

            // The drift: the map changes with the clock only when asked.
            var drifting = new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0.8f, drift: 100f), seed: 5);
            double before = ShadeSum(drifting);
            for (int step = 0; step < 200; step++) drifting.Step(0.5f);   // 100 s: 2.8 m of drift
            Assert.NotEqual(before, ShadeSum(drifting));
            for (int step = 0; step < 200; step++) world.Step(0.5f);
            Assert.Equal(ShadeSum(world), ShadeSum(again));
        }

        /// <summary>The shade summed over a 20 × 20 grid of columns: one number for a map.</summary>
        private static double ShadeSum(World world)
        {
            double sum = 0d;
            for (int i = 0; i < 20; i++)
            for (int j = 0; j < 20; j++) sum += world.ShadeAt(i + 0.5f, j + 0.5f);
            return sum;
        }

        [Fact]
        public void TheWorldWithNeitherIsTheRecordedWorld()
        {
            var world = new World(IslandWorld(wavelength: 0f, cover: 0.1f, shade: 0f), seed: 5);

            Assert.Null(world.IslandMap);
            Assert.Equal(1f, world.MeanShade);
            Assert.Equal(1f, world.ShadeAt(3f, 3f));
            Assert.Equal(100f * Area, world.Field.IncidentWatts, 3);
            Assert.Equal(1000d, world.Matter.TotalJoules, 5);

            // Uniform: every live column holds the same.
            var grid = (GridField)world.Matter;
            double first = grid.ColumnStockAt(1f, 1f);
            Assert.Equal(first, grid.ColumnStockAt(15f, 17f), 9);
        }

        [Fact]
        public void TheThreeRefusals()
        {
            // Islands without a budget: nothing to place.
            var refused = Assert.Throws<ArgumentException>(
                () => new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0f, budget: 0f), seed: 1));
            Assert.Contains("need a matter budget", refused.Message);

            // A shade map without islands: no map to shade by.
            refused = Assert.Throws<ArgumentException>(
                () => new World(IslandWorld(wavelength: 0f, cover: 0.25f, shade: 0.5f), seed: 1));
            Assert.Contains("needs MatterIslandWavelengthMetres", refused.Message);

            // A drift without a map.
            refused = Assert.Throws<ArgumentException>(
                () => new World(IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0f, drift: 10f), seed: 1));
            Assert.Contains("needs a shade map", refused.Message);

            // Islands on the cell field, which has no columns.
            RunConfig cells = IslandWorld(wavelength: 8f, cover: 0.25f, shade: 0f);
            cells.FieldModel = MatterField.Cells;
            cells.SharedSpace = false;
            refused = Assert.Throws<ArgumentException>(() => new World(cells, seed: 1));
            Assert.Contains("need a grid field", refused.Message);
        }
    }
}
