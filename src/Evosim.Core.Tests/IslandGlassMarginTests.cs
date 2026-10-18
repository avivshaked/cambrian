using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D185: no island column within <see cref="RunConfig.MatterIslandGlassMarginMetres"/> of the
    /// glass, the seeding and the influx alike, and nothing changed at a margin of 0.
    /// </summary>
    /// <remarks>
    /// The last test is a read rather than a check: how far D109's islands lean toward the glass over
    /// fifty seeds, with the margin and without it. Round 60's screens found most of the island matter
    /// within 40 m of the glass on three maps of three (<c>scratch/owner/r60s-islands-against-glass/</c>),
    /// and the read asks whether that is the seeds' chance or a habit of the noise and the threshold
    /// over a disc, which a margin would only move inward.
    /// </remarks>
    public class IslandGlassMarginTests
    {
        private readonly ITestOutputHelper _output;

        public IslandGlassMarginTests(ITestOutputHelper output) => _output = output;

        private const float Area = 2000f;
        private const float Depth = 20f;
        private const float Margin = 5f;

        /// <summary>A still tank with islands on 2 m matter cells, fed on its islands at <paramref name="influx"/>.</summary>
        private static RunConfig Tank(float margin, float influx = 0f, bool islands = true, WorldShape shape = WorldShape.Tank)
        {
            return new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = shape,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = 4,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = islands ? 10f : 0f,
                MatterIslandCover = 0.2f,
                MatterIslandDepthMetres = 6f,
                MatterIslandGlassMarginMetres = margin,
                MatterInfluxPerSecond = influx,
                MatterInfluxAt = MatterInflux.Islands,
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

        private static double GlassGap(GridField grid, int ix, int iz)
        {
            double r = grid.TankRadiusMetres;
            double dx = (ix + 0.5) * grid.CellMetres - r, dz = (iz + 0.5) * grid.CellMetres - r;
            return r - Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Each live column's matter, summed down the column.</summary>
        private static Dictionary<(int, int), double> Columns(GridField grid)
        {
            var columns = new Dictionary<(int, int), double>();
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                int lowest = grid.LowestLiveLayer(ix, iz);
                if (lowest < 0) continue;
                double sum = 0d;
                for (int iy = 0; iy <= lowest; iy++) sum += grid.JoulesAt(ix, iy, iz);
                columns[(ix, iz)] = sum;
            }

            return columns;
        }

        [Fact]
        public void NoIslandColumnLiesWithinTheMarginAndTheCoverIsAShareOfWhatRemains()
        {
            var world = new World(Tank(Margin), seed: 3);
            var grid = (GridField)world.Matter;
            Dictionary<(int, int), double> columns = Columns(grid);

            int eligible = 0, islands = 0, near = 0;
            foreach (KeyValuePair<(int, int), double> c in columns)
            {
                bool inside = GlassGap(grid, c.Key.Item1, c.Key.Item2) >= Margin;
                if (inside) eligible++;
                if (c.Value > 0d)
                {
                    islands++;
                    if (!inside) near++;
                }
            }

            _output.WriteLine($"{columns.Count} live columns, {eligible} at least {Margin} m inside the glass, {islands} island columns, {near} of them nearer");
            Assert.Equal(0, near);
            Assert.Equal(islands, world.IslandInflux == null ? islands : world.IslandInflux.ColumnCount);

            // The threshold leaves (1 - cover) of the eligible columns below it.
            int expected = eligible - (int)Math.Floor((1d - world.Config.MatterIslandCover) * eligible);
            Assert.InRange(islands, expected - 1, expected);
            Assert.Equal(world.Config.MatterBudgetUnits, world.MatterInitialTotal, 3);
        }

        [Fact]
        public void TheInfluxLandsOffTheGlassToo()
        {
            var world = new World(Tank(Margin, influx: 1.2f), seed: 3);
            var grid = (GridField)world.Matter;
            Dictionary<(int, int), double> before = Columns(grid);

            world.Step(1f);
            Dictionary<(int, int), double> after = Columns(grid);

            double gained = 0d;
            foreach (KeyValuePair<(int, int), double> c in after)
            {
                double d = c.Value - before[c.Key];
                gained += d;
                if (GlassGap(grid, c.Key.Item1, c.Key.Item2) < Margin) Assert.Equal(0d, d);
            }

            Assert.True(gained > 0d, "the influx laid nothing");
            Assert.Equal(0d, world.MatterResidual, 9);
        }

        [Fact]
        public void AMarginOfZeroIsTheRecordedSeeding()
        {
            // The default config, the margin never set, against one set to 0 explicitly: one plan, one
            // seeding, cell for cell, and the same hash.
            RunConfig unset = Tank(0f);
            unset.MatterIslandGlassMarginMetres = 0f;
            var plain = new World(Tank(0f), seed: 3);
            var zero = new World(unset, seed: 3);

            Assert.Equal(Columns((GridField)plain.Matter), Columns((GridField)zero.Matter));
            Assert.Equal(plain.IslandThreshold, zero.IslandThreshold);
            Assert.Equal(0f, new RunConfig().MatterIslandGlassMarginMetres);

            // A margin changes the plan, and the hash says so.
            var margined = new World(Tank(Margin), seed: 3);
            Assert.NotEqual(Columns((GridField)plain.Matter), Columns((GridField)margined.Matter));
            RunConfig hashedPlain = Tank(0f), hashedMargined = Tank(Margin);
            hashedPlain.Current = new CurrentField();
            hashedMargined.Current = new CurrentField();
            Assert.NotEqual(hashedPlain.Hash(), hashedMargined.Hash());
        }

        [Fact]
        public void AMarginIsRefusedWithoutIslandsInABoxAndWhenItLeavesNoColumn()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { MatterIslandGlassMarginMetres = -1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { MatterIslandGlassMarginMetres = float.NaN });

            ArgumentException noIslands = Assert.Throws<ArgumentException>(() => new World(Tank(Margin, islands: false), seed: 3));
            Assert.Contains("needs islands", noIslands.Message);

            float radius = TankGeometry.RadiusFor(Area);
            ArgumentException tooWide = Assert.Throws<ArgumentException>(() => new World(Tank(radius + 1f), seed: 3));
            Assert.Contains("leaves no live column", tooWide.Message);

            var box = new GridField(400f, 0f, Depth, 0f, 0f, 1, 2f);
            var map = new GradientNoise(3UL, 10f);
            ArgumentException boxed = Assert.Throws<ArgumentException>(() => box.PlanIslands(map.Fbm, 0.2f, 6f, Margin));
            Assert.Contains("needs a tank", boxed.Message);
        }

        /// <summary>
        /// The lean read: fifty seeds of D109's plan at both of round 60's tank sizes, on a flat tank
        /// so that a column's matter is its plateau weight alone. For each band of distance from the
        /// glass, the share of the island columns and of the island matter in it, against the share
        /// of the live columns in it, which a placement that knew nothing of the glass gives on
        /// average.
        /// </summary>
        [Fact]
        [Trait("Category", "Slow")]
        public void FiftySeedsOfIslandsAgainstTheGlass()
        {
            var sizes = new[]
            {
                (Name: "88,000 m2, wavelength 90 m, cover 0.05", Area: 88000f, Wavelength: 90f, Cover: 0.05f),
                (Name: "22,000 m2, wavelength 60 m, cover 0.10", Area: 22000f, Wavelength: 60f, Cover: 0.1f),
            };
            var runs = new[]
            {
                (Margin: 0f, Bands: new[] { (0d, 20d), (20d, 40d) }),
                (Margin: 30f, Bands: new[] { (30d, 50d), (50d, 70d) }),
            };
            const int Seeds = 50;

            foreach (var size in sizes)
            {
                float radius = TankGeometry.RadiusFor(size.Area);
                foreach (var run in runs)
                {
                    int bands = run.Bands.Length;
                    var columnShare = new double[bands][];
                    var matterShare = new double[bands][];
                    for (int b = 0; b < bands; b++)
                    {
                        columnShare[b] = new double[Seeds];
                        matterShare[b] = new double[Seeds];
                    }

                    double[] water = null;
                    int nearest = int.MaxValue;

                    for (int s = 1; s <= Seeds; s++)
                    {
                        var grid = new GridField(size.Area, 0f, 25f, 0f, 0f, 1, 5f,
                            patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: radius);
                        var map = new GradientNoise(Rng.SeedFor((ulong)s, World.IslandMapIndex), size.Wavelength);
                        GridField.IslandPlan plan = grid.PlanIslands(map.Fbm, size.Cover, 12f, run.Margin);
                        grid.DepositIslands(plan, 1000d);

                        Dictionary<(int, int), double> columns = Columns(grid);
                        if (water == null)
                        {
                            water = new double[bands];
                            foreach (var key in columns.Keys)
                            {
                                double g = GlassGap(grid, key.Item1, key.Item2);
                                for (int b = 0; b < bands; b++)
                                    if (g >= run.Bands[b].Item1 && g < run.Bands[b].Item2) water[b]++;
                            }

                            for (int b = 0; b < bands; b++) water[b] /= columns.Count;
                        }

                        int islandColumns = 0;
                        double total = 0d;
                        var inBand = new double[bands];
                        var matterInBand = new double[bands];
                        foreach (KeyValuePair<(int, int), double> c in columns)
                        {
                            if (!(c.Value > 0d)) continue;
                            islandColumns++;
                            total += c.Value;
                            double g = GlassGap(grid, c.Key.Item1, c.Key.Item2);
                            nearest = Math.Min(nearest, (int)Math.Floor(g));
                            for (int b = 0; b < bands; b++)
                            {
                                if (g >= run.Bands[b].Item1 && g < run.Bands[b].Item2)
                                {
                                    inBand[b]++;
                                    matterInBand[b] += c.Value;
                                }
                            }
                        }

                        for (int b = 0; b < bands; b++)
                        {
                            columnShare[b][s - 1] = inBand[b] / islandColumns;
                            matterShare[b][s - 1] = matterInBand[b] / total;
                        }
                    }

                    // The water's share in a band is over every live column; with a margin the
                    // islands may lie only inside it, so its fair share is over the eligible columns.
                    double eligible = 1d;
                    if (run.Margin > 0f)
                    {
                        var grid = new GridField(size.Area, 0f, 25f, 0f, 0f, 1, 5f,
                            patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: radius);
                        Dictionary<(int, int), double> all = Columns(grid);
                        int inside = 0;
                        foreach (var key in all.Keys) if (GlassGap(grid, key.Item1, key.Item2) >= run.Margin) inside++;
                        eligible = (double)inside / all.Count;
                    }

                    _output.WriteLine($"{size.Name}, radius {radius:0.0} m, margin {run.Margin:0} m, {Seeds} seeds; nearest island column {nearest} m from the glass");
                    for (int b = 0; b < bands; b++)
                    {
                        Array.Sort(columnShare[b]);
                        Array.Sort(matterShare[b]);
                        double fair = water[b] / eligible;
                        _output.WriteLine(FormattableString.Invariant(
                            $"  {run.Bands[b].Item1,2:0}-{run.Bands[b].Item2,2:0} m: water {water[b]:0.000} of all, {fair:0.000} of the eligible; ") +
                            FormattableString.Invariant(
                            $"island columns mean {Mean(columnShare[b]):0.000} (median {columnShare[b][Seeds / 2]:0.000}, 10-90% {columnShare[b][Seeds / 10]:0.000}-{columnShare[b][Seeds * 9 / 10]:0.000}); ") +
                            FormattableString.Invariant(
                            $"matter mean {Mean(matterShare[b]):0.000} (median {matterShare[b][Seeds / 2]:0.000}, 10-90% {matterShare[b][Seeds / 10]:0.000}-{matterShare[b][Seeds * 9 / 10]:0.000})"));
                    }

                    if (run.Margin > 0f) Assert.True(nearest >= (int)run.Margin - 1, $"an island column {nearest} m from the glass under a {run.Margin} m margin");
                }
            }
        }

        private static double Mean(double[] values)
        {
            double sum = 0d;
            foreach (double v in values) sum += v;
            return sum / values.Length;
        }
    }
}
