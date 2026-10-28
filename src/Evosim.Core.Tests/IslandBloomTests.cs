using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D251: each fed island's influx on a cycle of its own. The islands' labels, the draws, the
    /// factor, the two ways of laying it, the refusals, and the resume.
    /// </summary>
    /// <remarks>
    /// Read on the bedded tank of <see cref="FedIslandsTests"/>, whose islands are the ones D175 feeds.
    /// A still world isolates the rule: no founders, no sinking, no stirring and no current, so one
    /// step's change to the matter grid is the influx and nothing else.
    /// </remarks>
    public class IslandBloomTests
    {
        private readonly ITestOutputHelper _output;

        public IslandBloomTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;
        private const float Depth = 20f;
        private const int Rings = 4;
        private const float Rate = 1.2f;

        /// <summary>
        /// The bedded tank of <see cref="FedIslandsTests"/>. <paramref name="still"/> turns off everything
        /// else that moves matter; <paramref name="bloom"/> sets the cycles, with the bounds given.
        /// </summary>
        private static RunConfig Bedded(
            bool still, IslandBloom bloom = IslandBloom.None, float periodMin = 2500f, float periodMax = 6000f,
            float amplitudeMin = 0.8f, float amplitudeMax = 1f, bool holds = true)
        {
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Rings,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                BedReliefMetres = 1.5f,
                BedTiltMetres = 6f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 8f,
                MatterIslandCover = 0.25f,
                MatterIslandDepthMetres = 6f,
                MatterSinkMetresPerSecond = 0.05f,
                MatterInfluxPerSecond = Rate,
                MatterInfluxAt = MatterInflux.Islands,
                MatterBurialPerSecond = 0.01f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                Current = new CurrentField
                {
                    Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = 6000f, AdvectFields = true,
                },
                IslandBloom = bloom,
                IslandBloomPeriodMinSeconds = periodMin,
                IslandBloomPeriodMaxSeconds = periodMax,
                IslandBloomAmplitudeMin = amplitudeMin,
                IslandBloomAmplitudeMax = amplitudeMax,
                IslandBloomHoldsTotal = holds,
            };

            if (still)
            {
                config.MinimumPopulation = 0;
                config.MaximumPopulation = 100_000;
                config.MatterBurialPerSecond = 0f;
                config.MatterSinkMetresPerSecond = 0f;
                config.MatterMixingDiffusivity = 0f;
                config.NutrientSinkMetresPerSecond = 0f;
                config.NutrientMixingDiffusivity = 0f;
                config.HorizontalMixingDiffusivity = 0f;
                config.RemineralisationPerSecond = 0f;
                config.Current = null;
            }

            return config;
        }

        private static double[] Cells(GridField grid)
        {
            var cells = new double[grid.CellsX * grid.CellsY * grid.CellsZ];
            int k = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iy = 0; iy < grid.CellsY; iy++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
                cells[k++] = grid.JoulesAt(ix, iy, iz);
            return cells;
        }

        /// <summary>A 12 by 12 box of 1 m cells, 3 layers deep, with no mask and no bed.</summary>
        private static GridField HandGrid() => new GridField(144f, 0f, 3f, 0f, 0f, 1, 1f);

        /// <summary>
        /// A plan whose island columns are exactly <paramref name="columns"/>: the map is 1 on them and 0
        /// elsewhere, and the cover sits just above their share so the threshold falls on a 0.
        /// </summary>
        private static GridField.IslandPlan HandPlan(GridField grid, IReadOnlyCollection<(int, int)> columns)
        {
            var set = new HashSet<(int, int)>(columns);
            float cover = (columns.Count + 0.5f) / (grid.CellsX * grid.CellsZ);
            GridField.IslandPlan plan = grid.PlanIslands(
                (x, z) => set.Contains(((int)Math.Floor(x), (int)Math.Floor(z))) ? 1f : 0f, cover);
            Assert.NotNull(plan);
            Assert.Equal(columns.Count, plan.ColumnCount);
            return plan;
        }

        // ---------------------------------------------------------------------------------
        // The labels
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TwoBlobsTouchingAtACornerAreOneIslandAndTwoAColumnApartAreTwo()
        {
            GridField grid = HandGrid();
            Assert.Equal(12, grid.CellsX);
            Assert.Equal(12, grid.CellsZ);

            // Scan order (ix outer, iz inner) meets the islands in this order:
            //   0: (0,7), alone on the first row;
            //   1: (1,1) and (2,2), touching at a corner of the "\" diagonal;
            //   2: (1,5) and (2,4), touching at a corner of the "/" diagonal;
            //   3: (6,8) and 4: (6,10), a column apart, so two;
            //   5: (11,7), on the far face at the same z as island 0: not joined across the seam.
            var columns = new[]
            {
                (0, 7), (1, 1), (2, 2), (1, 5), (2, 4), (6, 8), (6, 10), (11, 7),
            };

            GridField.IslandPlan plan = HandPlan(grid, columns);

            Assert.Equal(6, plan.IslandCount);
            int[] expected = { 0, 1, 1, 2, 2, 3, 4, 5 };
            for (int i = 0; i < columns.Length; i++)
            {
                Assert.Equal(expected[i], plan.IslandOfColumn(columns[i].Item1, columns[i].Item2));
            }

            // The desert is -1, including the column between the two that are a column apart.
            Assert.Equal(-1, plan.IslandOfColumn(6, 9));
            Assert.Equal(-1, plan.IslandOfColumn(5, 5));

            int[] counts = { 1, 2, 2, 1, 1, 1 };
            for (int k = 0; k < counts.Length; k++) Assert.Equal(counts[k], plan.IslandColumns(k));

            // Centroids are the mean of the column centres, in m, on 1 m cells.
            Assert.Equal(0.5, plan.IslandX(0), 12);
            Assert.Equal(7.5, plan.IslandZ(0), 12);
            Assert.Equal(2.0, plan.IslandX(1), 12);
            Assert.Equal(2.0, plan.IslandZ(1), 12);
            Assert.Equal(2.0, plan.IslandX(2), 12);
            Assert.Equal(5.0, plan.IslandZ(2), 12);
            Assert.Equal(11.5, plan.IslandX(5), 12);

            // The effective radius is that of the disc of the same area.
            Assert.Equal(Math.Sqrt(2.0 / Math.PI), plan.IslandRadius(1), 12);
            Assert.Equal(Math.Sqrt(1.0 / Math.PI), plan.IslandRadius(3), 12);

            // Every plan cell carries its column's island, in the plan's own order: three layers each.
            int[] cellIslands = plan.CopyCellIslands();
            Assert.Equal(plan.CellCount, cellIslands.Length);
            Assert.Equal(3 * columns.Length, cellIslands.Length);
            Assert.Equal(
                new[] { 3, 6, 6, 3, 3, 3 },
                Enumerable.Range(0, 6).Select(k => cellIslands.Count(v => v == k)).ToArray());
        }

        [Fact]
        public void AnIslandAcrossAColumnOfDesertFromTheFaceIsNotWrappedToTheOppositeFace()
        {
            // Columns at ix 0 and ix 11 are on opposite faces; a wrap would have joined them.
            GridField grid = HandGrid();
            GridField.IslandPlan plan = HandPlan(grid, new[] { (0, 3), (11, 3), (3, 0), (3, 11) });
            Assert.Equal(4, plan.IslandCount);
        }

        [Fact]
        public void TheLabelsOfARealSeededWorldAreReported()
        {
            var world = new World(Bedded(still: true), seed: 5);
            GridField.IslandPlan plan = world.IslandInflux;
            Assert.NotNull(plan);
            Assert.True(plan.IslandCount >= 1, "the plan has no island");

            var grid = (GridField)world.Matter;
            _output.WriteLine(FormattableString.Invariant(
                $"seed 5: {plan.IslandCount} islands in {plan.ColumnCount} columns on {grid.CellMetres} m cells"));

            int total = 0;
            for (int k = 0; k < plan.IslandCount; k++)
            {
                total += plan.IslandColumns(k);
                _output.WriteLine(FormattableString.Invariant(
                    $"  island {k}: {plan.IslandColumns(k)} columns, centre ({plan.IslandX(k):0.00}, {plan.IslandZ(k):0.00}) m, radius {plan.IslandRadius(k):0.00} m"));
                Assert.True(plan.IslandColumns(k) > 0);
                Assert.Equal(
                    Math.Sqrt(plan.IslandColumns(k) * (double)grid.CellMetres * grid.CellMetres / Math.PI),
                    plan.IslandRadius(k), 12);
            }

            Assert.Equal(plan.ColumnCount, total);

            // The labels are a function of the plan: a plan made again from the same map is labelled the same.
            GridField.IslandPlan again = grid.PlanIslands(
                world.IslandMap.Fbm, world.Config.MatterIslandCover, world.Config.MatterIslandDepthMetres,
                world.Config.MatterIslandGlassMarginMetres);
            Assert.Equal(plan.IslandCount, again.IslandCount);
            Assert.Equal(plan.CopyCellIslands(), again.CopyCellIslands());
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                Assert.Equal(plan.IslandOfColumn(ix, iz), again.IslandOfColumn(ix, iz));
                Assert.Equal(plan.IsIslandColumn(ix, iz), plan.IslandOfColumn(ix, iz) >= 0);
            }
        }

        // ---------------------------------------------------------------------------------
        // The factor and the draws
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheFactorIsOnePlusAmplitudeTimesTheSineClampedAtZero()
        {
            // P = 4000 s, A = 0.9, phase 0: the peak at a quarter period, the trough at three quarters.
            Assert.Equal(1d, World.IslandBloomFactor(0d, 4000d, 0.9, 0d), 12);
            Assert.Equal(1.9, World.IslandBloomFactor(1000d, 4000d, 0.9, 0d), 12);
            Assert.Equal(0.1, World.IslandBloomFactor(3000d, 4000d, 0.9, 0d), 12);
            Assert.Equal(1d, World.IslandBloomFactor(2000d, 4000d, 0.9, 0d), 9);

            // A phase of pi/2 shifts the peak to t = 0.
            Assert.Equal(1.9, World.IslandBloomFactor(0d, 4000d, 0.9, Math.PI / 2d), 12);

            // Above 1, the trough would go negative and is clamped at 0, and only there.
            Assert.Equal(0d, World.IslandBloomFactor(3000d, 4000d, 1.5, 0d));
            Assert.Equal(2.5, World.IslandBloomFactor(1000d, 4000d, 1.5, 0d), 12);

            // A world's own factor is the formula at its drawn values.
            var world = new World(Bedded(still: true, IslandBloom.Cycles), seed: 5);
            for (int k = 0; k < world.IslandBloomPeriods.Count; k++)
            {
                Assert.Equal(
                    World.IslandBloomFactor(1234.5, world.IslandBloomPeriods[k], world.IslandBloomAmplitudes[k], world.IslandBloomPhases[k]),
                    world.IslandBloomFactorAt(k, 1234.5));
            }
        }

        [Fact]
        public void TheSameSeedGivesTheSameDrawsAndAnotherSeedOthers()
        {
            // One landscape under two world seeds, so the islands are the same and only the cycles move.
            RunConfig Landscape()
            {
                RunConfig c = Bedded(still: true, IslandBloom.Cycles);
                c.SeedOverrides = new[] { "landscape=3" };
                return c;
            }

            var a = new World(Landscape(), seed: 7);
            var b = new World(Landscape(), seed: 7);
            var other = new World(Landscape(), seed: 8);

            Assert.True(a.HasIslandBloom);
            Assert.Equal(a.IslandInflux.IslandCount, a.IslandBloomPeriods.Count);
            Assert.Equal(a.IslandBloomPeriods, b.IslandBloomPeriods);
            Assert.Equal(a.IslandBloomAmplitudes, b.IslandBloomAmplitudes);
            Assert.Equal(a.IslandBloomPhases, b.IslandBloomPhases);

            Assert.Equal(a.IslandBloomPeriods.Count, other.IslandBloomPeriods.Count);
            Assert.NotEqual(a.IslandBloomPeriods, other.IslandBloomPeriods);
            Assert.NotEqual(a.IslandBloomAmplitudes, other.IslandBloomAmplitudes);
            Assert.NotEqual(a.IslandBloomPhases, other.IslandBloomPhases);

            for (int k = 0; k < a.IslandBloomPeriods.Count; k++)
            {
                _output.WriteLine(FormattableString.Invariant(
                    $"island {k}: P {a.IslandBloomPeriods[k]:0.0} s, A {a.IslandBloomAmplitudes[k]:0.000}, phase {a.IslandBloomPhases[k]:0.000} rad"));
                Assert.InRange(a.IslandBloomPeriods[k], 2500d, 6000d);
                Assert.InRange(a.IslandBloomAmplitudes[k], 0.8, 1.0);
                Assert.InRange(a.IslandBloomPhases[k], 0d, 2d * Math.PI);
                Assert.True(a.IslandBloomPhases[k] < 2d * Math.PI);
            }

            // Island k's draws come from the seed and k alone: the first island's are what a direct draw gives.
            var draw = new Rng(Rng.SeedFor(Rng.SeedFor(7UL, World.IslandBloomIndex), 0UL));
            Assert.Equal((double)draw.Range(2500f, 6000f), a.IslandBloomPeriods[0]);
            Assert.Equal((double)draw.Range(0.8f, 1f), a.IslandBloomAmplitudes[0]);
            Assert.Equal((double)draw.NextFloat() * (2d * Math.PI), a.IslandBloomPhases[0]);

            // Off, nothing is drawn.
            var off = new World(Bedded(still: true), seed: 7);
            Assert.False(off.HasIslandBloom);
            Assert.Empty(off.IslandBloomPeriods);
            Assert.Empty(off.IslandBloomFactorsAt(100d));
            Assert.Empty(off.IslandBloomLeafArea());
        }

        // ---------------------------------------------------------------------------------
        // Laying it
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// One still step of a cycling world: what each island's cells gained against what the seeding put
        /// there, and the factors the step should have been laid by.
        /// </summary>
        private (World world, GridField grid, double[] seeded, double[] fed) OneStep(bool holds)
        {
            // Periods of 4 to 8 s, so the factor at t = 1 s is far from its value at t = 0.
            var world = new World(Bedded(still: true, IslandBloom.Cycles, 4f, 8f, 0.8f, 1f, holds), seed: 5);
            var grid = (GridField)world.Matter;
            double[] seeded = Cells(grid);
            world.Step(1f);
            return (world, grid, seeded, Cells(grid));
        }

        [Fact]
        public void HoldingTheTotalLaysExactlyTheJoulesHandedIn()
        {
            (World world, GridField grid, double[] seeded, double[] fed) = OneStep(holds: true);
            GridField.IslandPlan plan = world.IslandInflux;

            Assert.Equal(1d, world.ElapsedSeconds, 12);

            double gained = 0d;
            for (int i = 0; i < seeded.Length; i++) gained += fed[i] - seeded[i];

            double expected = Rate * 1d;
            _output.WriteLine(FormattableString.Invariant(
                $"{plan.IslandCount} islands; gained {gained:R} against {expected:R} handed in, {world.MatterInfluxedTotal:R} counted"));
            Assert.True(Math.Abs(gained - expected) <= 1e-9 * expected, $"the grid gained {gained:R}");
            Assert.True(Math.Abs(world.MatterInfluxedTotal - expected) <= 1e-9 * expected);
            Assert.Equal(0d, world.MatterResidual, 9);

            // Each island's gain per joule it held at t = 0 is its own factor, up to one common constant. The
            // factors are read at the clock after the step's own advance, 1 s, and are not those at 0 s.
            double[] atStep = world.IslandBloomFactorsAt(1d);
            double[] atStart = world.IslandBloomFactorsAt(0d);
            Assert.True(
                Enumerable.Range(0, atStep.Length).Any(k => Math.Abs(atStep[k] - atStart[k]) > 1e-3),
                "the clock before and after the step gave the same factors, so this cannot tell them apart");

            int reference = Array.IndexOf(atStep, atStep.Max());
            Assert.True(atStep[reference] > 0d);
            double[] gainPerJoule = GainPerJoule(plan, grid, seeded, fed);

            for (int k = 0; k < plan.IslandCount; k++)
            {
                Assert.True(
                    Math.Abs(gainPerJoule[k] * atStep[reference] - gainPerJoule[reference] * atStep[k]) <=
                    1e-9 * gainPerJoule[reference] * atStep[reference],
                    $"island {k} gained {gainPerJoule[k]:R} per joule against a factor of {atStep[k]:R}");
            }
        }

        [Fact]
        public void NotHoldingTheTotalLetsItMoveWithTheCycles()
        {
            (World world, GridField grid, double[] seeded, double[] fed) = OneStep(holds: false);
            GridField.IslandPlan plan = world.IslandInflux;
            double[] f = world.IslandBloomFactorsAt(world.ElapsedSeconds);

            // joules x sum(w f) / sum(w), where a cell's seeded stock is proportional to its weight.
            double sumW = 0d, sumWf = 0d, gained = 0d;
            var grid2 = grid;
            int k = 0;
            for (int ix = 0; ix < grid2.CellsX; ix++)
            for (int iy = 0; iy < grid2.CellsY; iy++)
            for (int iz = 0; iz < grid2.CellsZ; iz++, k++)
            {
                gained += fed[k] - seeded[k];
                if (!(seeded[k] > 0d)) continue;
                sumW += seeded[k];
                sumWf += seeded[k] * f[plan.IslandOfColumn(ix, iz)];
            }

            double expected = Rate * 1d * sumWf / sumW;
            _output.WriteLine(FormattableString.Invariant(
                $"gained {gained:R}; expected {expected:R} against {Rate * 1d:R} handed in; counted {world.MatterInfluxedTotal:R}"));
            Assert.True(Math.Abs(gained - expected) <= 1e-9 * Rate, $"the grid gained {gained:R}");
            Assert.Equal(gained, world.MatterInfluxedTotal, 9);
            Assert.True(Math.Abs(gained - Rate) > 1e-6, "the factors happened to average 1, so this does not tell the two apart");
            Assert.Equal(0d, world.MatterResidual, 9);
        }

        private static double[] GainPerJoule(GridField.IslandPlan plan, GridField grid, double[] seeded, double[] fed)
        {
            var sum = new double[plan.IslandCount];
            var gain = new double[plan.IslandCount];
            int k = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iy = 0; iy < grid.CellsY; iy++)
            for (int iz = 0; iz < grid.CellsZ; iz++, k++)
            {
                if (!(seeded[k] > 0d)) continue;
                int island = plan.IslandOfColumn(ix, iz);
                Assert.True(island >= 0, "a cell held matter at t = 0 in the desert");
                sum[island] += seeded[k];
                gain[island] += fed[k] - seeded[k];
            }

            return Enumerable.Range(0, plan.IslandCount).Select(i => gain[i] / sum[i]).ToArray();
        }

        [Fact]
        public void EveryIslandAtFactorZeroLaysNothing()
        {
            GridField grid = HandGrid();
            GridField.IslandPlan plan = HandPlan(grid, new[] { (1, 1), (2, 2), (6, 8), (9, 9) });
            var zeros = new double[plan.IslandCount];

            foreach (bool holds in new[] { true, false })
            {
                double[] before = Cells(grid);
                Assert.Equal(0d, grid.DepositIslandsBloomed(plan, 10d, zeros, holds));
                Assert.Equal(before, Cells(grid));
            }

            // One island at 0 and one at 1 lays the whole of it on the one, when the total is held.
            var half = new double[plan.IslandCount];
            half[1] = 1d;
            double laid = grid.DepositIslandsBloomed(plan, 10d, half, holdsTotal: true);
            Assert.True(Math.Abs(laid - 10d) <= 1e-9 * 10d);
            Assert.Equal(1, plan.IslandOfColumn(6, 8));
            Assert.True(grid.JoulesAt(6, 0, 8) > 0d);
            Assert.Equal(0d, grid.JoulesAt(1, 0, 1));
        }

        [Fact]
        public void WithTheSwitchOffTheStockAfterADepositIsTheExistingLoopsArithmetic()
        {
            // Nine island columns of three cells, each of weight 1f: the loop is joules * 1f / 27.0 per cell.
            GridField grid = HandGrid();
            var columns = new[] { (1, 1), (2, 2), (1, 5), (2, 4), (6, 8), (6, 10), (0, 7), (11, 7), (9, 9) };
            GridField.IslandPlan plan = HandPlan(grid, columns);
            Assert.Equal(27, plan.CellCount);
            Assert.Equal(27d, plan.WeightSum);

            const double Joules = 1.23456789012345;
            double laid = grid.DepositIslands(plan, Joules);

            double each = Joules * 1f / 27d;
            double placed = 0d;
            for (int i = 0; i < 27; i++) placed += each;
            Assert.Equal(placed, laid);

            foreach ((int ix, int iz) in columns)
            {
                for (int iy = 0; iy < 3; iy++) Assert.Equal(each, grid.JoulesAt(ix, iy, iz));
            }
        }

        [Fact]
        public void AWorldOnCyclesWithAmplitudeZeroIsTheWorldWithoutThemToTheBit()
        {
            // f = 1 everywhere, and the on path's arithmetic is the off path's: the same doubles in the same order.
            var off = new World(Bedded(still: true), seed: 5);
            var on = new World(Bedded(still: true, IslandBloom.Cycles, 2500f, 6000f, 0f, 0f), seed: 5);

            for (int i = 0; i < 40; i++)
            {
                off.Step(0.5f);
                on.Step(0.5f);
            }

            Assert.Equal(Cells((GridField)off.Matter), Cells((GridField)on.Matter));
            Assert.Equal(off.MatterInfluxedTotal, on.MatterInfluxedTotal);
        }

        /// <summary>
        /// A bedded tank with a leaf smell and a leaf standing over the first column of every island and one over
        /// the desert, stepped a few times so the leaf fill has gathered them. The band is the far smell's.
        /// </summary>
        private static World LeafWorld(float band)
        {
            RunConfig config = Bedded(still: true, IslandBloom.Cycles);
            config.LeafScent = LeafScentForm.Strength;
            config.IslandFarSmellBandMetres = band;
            var world = new World(config, seed: 5);
            var cellGrid = (GridField)world.Matter;
            GridField.IslandPlan plan = world.IslandInflux;

            var seen = new HashSet<int>();
            for (int ix = 0; ix < cellGrid.CellsX; ix++)
            for (int iz = 0; iz < cellGrid.CellsZ; iz++)
            {
                int island = plan.IslandOfColumn(ix, iz);
                if (island < 0 || !seen.Add(island)) continue;

                world.InoculateAt(
                    Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic),
                    new Float3((ix + 0.5f) * cellGrid.CellMetres, -2f, (iz + 0.5f) * cellGrid.CellMetres));
            }

            Assert.Equal(plan.IslandCount, seen.Count);
            world.InoculateAt(
                Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), new Float3(0.5f * world.TankRadiusMetres, -2f, world.TankRadiusMetres));
            for (int i = 0; i < 4; i++) world.Step(0.5f);
            return world;
        }

        /// <summary>
        /// L_k by brute force, from the sources alone: each source counts towards the island with the least
        /// level distance to its centroid less its radius, the lower label on a tie, and only when that is at
        /// most the band.
        /// </summary>
        private static double[] BruteBandedArea(World world, double band)
        {
            float[] sources = world.CopyLeafSources();
            GridField.IslandPlan plan = world.IslandInflux;
            var area = new double[plan.IslandCount];
            for (int i = 0; i < world.LeafSourceCount; i++)
            {
                int best = -1;
                double least = double.MaxValue;
                for (int k = 0; k < plan.IslandCount; k++)
                {
                    double dx = sources[4 * i] - plan.IslandX(k), dz = sources[4 * i + 2] - plan.IslandZ(k);
                    double edge = Math.Sqrt(dx * dx + dz * dz) - plan.IslandRadius(k);
                    if (edge < least) { least = edge; best = k; }
                }

                if (least <= band) area[best] += sources[4 * i + 3];
            }

            return area;
        }

        [Fact]
        public void TheLeafAreaIsTheBandedSumOfTheSourcesNearestEachIslandsEdge()
        {
            World world = LeafWorld(band: 10f);
            Assert.NotNull(world.LeafScent);
            Assert.True(world.LeafSourceCount > 0, "no leaf was gathered");

            float[] sources = world.CopyLeafSources();
            double all = 0d;
            for (int i = 0; i < world.LeafSourceCount; i++) all += sources[4 * i + 3];

            double[] area = world.IslandBloomLeafArea();
            double[] expected = BruteBandedArea(world, 10d);
            _output.WriteLine(FormattableString.Invariant(
                $"{world.LeafSourceCount} leaf sources, {all:0.###} m2 lit; banded at 10 m per island {string.Join(", ", area.Select(a => a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))}"));
            Assert.Equal(expected, area);
            Assert.All(area, a => Assert.True(a > 0d, "an island with a leaf over it counted none"));
            Assert.True(area.Sum() <= all + 1e-9);

            // A band wide enough for any source counts them all; none at all counts only a source whose
            // nearest edge it stands inside or on, and never more than the band of 10 m did.
            World wide = LeafWorld(band: 1000f);
            Assert.Equal(all, wide.IslandBloomLeafArea().Sum(), 6);
            World narrow = LeafWorld(band: 0f);
            double[] none = narrow.IslandBloomLeafArea();
            Assert.Equal(BruteBandedArea(narrow, 0d), none);
            Assert.True(none.Sum() <= area.Sum() + 1e-9);
        }

        // ---------------------------------------------------------------------------------
        // The resume
        // ---------------------------------------------------------------------------------

        private static byte[] StateOf(World world)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Fact]
        public void AResumeOfAWorldOnCyclesMatchesTheUnbrokenRun()
        {
            // Periods of 40 to 90 s, so the factors move a good deal inside the 180 s the test runs.
            RunConfig Cycling() => Bedded(still: false, IslandBloom.Cycles, 40f, 90f, 0.8f, 1f, holds: true);

            const ulong Seed = 2;
            var unbroken = new World(Cycling(), Seed);
            for (int i = 0; i < 120; i++) unbroken.Step(0.5f);

            byte[] checkpoint = StateOf(unbroken);
            double influxAtCheckpoint = unbroken.MatterInfluxedTotal;

            // A fresh world on the same config and seed, overwritten by the checkpoint: the cycles are
            // drawn again by the construction and nothing of them was written.
            var resumed = new World(Cycling(), Seed);
            using (var buffer = new MemoryStream(checkpoint, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                resumed.ReadState(r);
                Assert.Equal(checkpoint.Length, buffer.Position);
            }

            Assert.Equal(unbroken.IslandBloomPeriods, resumed.IslandBloomPeriods);
            Assert.Equal(StateOf(unbroken), StateOf(resumed));

            for (int i = 0; i < 240; i++)
            {
                unbroken.Step(0.5f);
                resumed.Step(0.5f);
            }

            _output.WriteLine(FormattableString.Invariant(
                $"{unbroken.IslandInflux.IslandCount} islands; influx {influxAtCheckpoint:0.###} at the checkpoint, ") +
                FormattableString.Invariant($"{unbroken.MatterInfluxedTotal:0.###} at the end; alive {unbroken.Living.Count}"));

            Assert.True(unbroken.MatterInfluxedTotal > influxAtCheckpoint, "the influx did not run after the checkpoint");
            Assert.NotEmpty(unbroken.Living);
            Assert.Equal(unbroken.MatterInfluxedTotal, resumed.MatterInfluxedTotal);
            Assert.Equal(unbroken.MatterBuriedTotal, resumed.MatterBuriedTotal);
            Assert.Equal(StateOf(unbroken), StateOf(resumed));
        }

        // ---------------------------------------------------------------------------------
        // The refusals
        // ---------------------------------------------------------------------------------

        [Fact]
        public void CyclesWithoutTheIslandsInfluxAreRefused()
        {
            RunConfig surface = Bedded(still: true, IslandBloom.Cycles);
            surface.MatterInfluxAt = MatterInflux.Surface;
            ArgumentException noRoute = Assert.Throws<ArgumentException>(() => new World(surface, seed: 5));
            _output.WriteLine(noRoute.Message);
            Assert.Contains("IslandBloom is Cycles", noRoute.Message);

            RunConfig dry = Bedded(still: true, IslandBloom.Cycles);
            dry.MatterInfluxPerSecond = 0f;
            ArgumentException noInflux = Assert.Throws<ArgumentException>(() => new World(dry, seed: 5));
            _output.WriteLine(noInflux.Message);
            Assert.Contains("IslandBloom is Cycles", noInflux.Message);

            // Off, the same configs are the worlds they always were.
            surface.IslandBloom = IslandBloom.None;
            Assert.Null(new World(surface, seed: 5).IslandInflux);
        }

        [Theory]
        [InlineData(0f, 6000f)]
        [InlineData(-1f, 6000f)]
        [InlineData(2500f, 0f)]
        [InlineData(2500f, -5f)]
        [InlineData(float.NaN, 6000f)]
        [InlineData(2500f, float.NaN)]
        [InlineData(2500f, float.PositiveInfinity)]
        [InlineData(float.PositiveInfinity, float.PositiveInfinity)]
        [InlineData(6000f, 2500f)]
        public void ABadPeriodIsRefused(float min, float max)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => new World(Bedded(still: true, IslandBloom.Cycles, min, max), seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("period bounds", e.Message);
        }

        [Theory]
        [InlineData(-0.1f, 1f)]
        [InlineData(0.8f, -1f)]
        [InlineData(float.NaN, 1f)]
        [InlineData(0.8f, float.NaN)]
        [InlineData(0.8f, float.PositiveInfinity)]
        [InlineData(1f, 0.8f)]
        public void ABadAmplitudeIsRefused(float min, float max)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => new World(Bedded(still: true, IslandBloom.Cycles, 2500f, 6000f, min, max), seed: 5));
            _output.WriteLine(e.Message);
            Assert.Contains("amplitude bounds", e.Message);
        }

        [Fact]
        public void AnAmplitudeAboveOneIsAllowedAndEqualBoundsAreAPoint()
        {
            var world = new World(Bedded(still: true, IslandBloom.Cycles, 3000f, 3000f, 1.5f, 1.5f), seed: 5);
            Assert.All(world.IslandBloomPeriods, p => Assert.Equal(3000d, p));
            Assert.All(world.IslandBloomAmplitudes, a => Assert.Equal(1.5d, a));
        }
    }
}
