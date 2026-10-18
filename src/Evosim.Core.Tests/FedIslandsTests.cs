using System;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D175: D074's influx landing on D109's islands, and D074's burial taken at each column's own
    /// floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read on a bedded tank</b>, because that is where both rules differ from what they
    /// replace: the island columns hold fewer live cells where the floor rises, and the array's last
    /// layer is water only under the deepest hollow. A flat grid is read once, for the burial's
    /// promise that it changes nothing there.
    /// </para>
    /// <para>
    /// <b>A still world isolates a rule.</b> No founders, no sinking, no stirring, no current and no
    /// remineralisation, so one step's change to the matter grid is the rule's and nothing else's.
    /// The identity and the resume are read in a living world with the water moving.
    /// </para>
    /// </remarks>
    public class FedIslandsTests
    {
        private readonly ITestOutputHelper _output;

        public FedIslandsTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;
        private const float Depth = 20f;
        private const int Rings = 4;

        /// <summary>
        /// A bedded tank with islands on 2 m matter cells, fed on its islands and buried at its
        /// floor. <paramref name="still"/> turns off everything else that moves matter.
        /// </summary>
        private static RunConfig Bedded(float influx, float burial, bool still, bool islands = true)
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
                MatterIslandWavelengthMetres = islands ? 8f : 0f,
                MatterIslandCover = 0.25f,
                MatterIslandDepthMetres = 6f,
                MatterSinkMetresPerSecond = 0.05f,
                MatterInfluxPerSecond = influx,
                MatterInfluxAt = MatterInflux.Islands,
                MatterBurialPerSecond = burial,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                Current = new CurrentField
                {
                    Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = 6000f, AdvectFields = true,
                },
            };

            if (still)
            {
                config.MinimumPopulation = 0;
                config.MaximumPopulation = 100_000;
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

        // ---------------------------------------------------------------------------------
        // The influx
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheInfluxLandsOnlyInTheIslandsInProportionToTheirStockAtTimeZero()
        {
            const float Rate = 1.2f;
            const float Seconds = 1f;

            var world = new World(Bedded(Rate, burial: 0f, still: true), seed: 5);
            var grid = (GridField)world.Matter;

            Assert.NotNull(world.IslandInflux);
            double[] seeded = Cells(grid);

            world.Step(Seconds);
            double[] fed = Cells(grid);

            // Every cell that held nothing at t = 0 holds nothing now; every cell that held matter
            // gained the same share of what it held, the step's influx over the budget.
            double share = Rate * Seconds / (double)world.MatterInitialTotal;
            int islandCells = 0, deserts = 0;
            double gained = 0d, worst = 0d;

            for (int i = 0; i < seeded.Length; i++)
            {
                double d = fed[i] - seeded[i];
                gained += d;

                if (seeded[i] == 0d)
                {
                    deserts++;
                    Assert.Equal(0d, fed[i]);
                    continue;
                }

                islandCells++;
                double ratio = d / seeded[i];
                worst = Math.Max(worst, Math.Abs(ratio / share - 1d));
            }

            // The islands are where the plan says, and the plan is the seeding's.
            int bottomCells = 0, liveColumns = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                int lowest = grid.LowestLiveLayer(ix, iz);
                if (lowest < 0) continue;
                liveColumns++;

                // Below the island depth (6 m on 2 m cells is three layers) nothing arrived.
                for (int iy = 3; iy <= lowest; iy++)
                {
                    bottomCells++;
                    Assert.Equal(0d, grid.JoulesAt(ix, iy, iz));
                }
            }

            _output.WriteLine(FormattableString.Invariant(
                $"{islandCells} island cells in {world.IslandInflux.ColumnCount} of {liveColumns} live columns, {deserts} empty cells; ") +
                FormattableString.Invariant(
                $"gained {gained:R} against {world.MatterInfluxedTotal:R} counted; worst share error {worst:0.0e+0}; ") +
                FormattableString.Invariant($"{bottomCells} cells below the island depth untouched"));

            Assert.Equal(world.IslandInflux.CellCount, islandCells);
            Assert.True(world.IslandInflux.ColumnCount > 0 && world.IslandInflux.ColumnCount < liveColumns / 2,
                "the islands are a minority of the columns");
            Assert.True(worst < 1e-9, $"a cell's gain strayed {worst:R} from its share of the seeding");
            Assert.Equal(Rate * Seconds, world.MatterInfluxedTotal, 9);
            Assert.Equal(world.MatterInfluxedTotal, gained, 9);
            Assert.Equal(0d, world.MatterResidual, 9);
        }

        [Fact]
        public void TheIslandPlanIsTheSeedingsShapeAndIgnoresTheLiveField()
        {
            // The plan is made from the map, the cover, the depth and the mask. Two worlds on one
            // seed hold the same plan whatever has happened to the water since; laying a total by
            // the plan on an empty copy of the grid gives the seeding back cell for cell.
            var world = new World(Bedded(1.2f, burial: 0f, still: true), seed: 5);
            var grid = (GridField)world.Matter;
            double[] seeded = Cells(grid);

            // Disturb the live field, then make the plan again: it must not move.
            grid.Deposit(new FieldPoint(new Float3(world.TankRadiusMetres, -1f, world.TankRadiusMetres), 0), 500f);
            GridField.IslandPlan again = grid.PlanIslands(
                world.IslandMap.Fbm, world.Config.MatterIslandCover, world.Config.MatterIslandDepthMetres);

            Assert.Equal(world.IslandInflux.CellCount, again.CellCount);
            Assert.Equal(world.IslandInflux.ColumnCount, again.ColumnCount);
            Assert.Equal(world.IslandInflux.WeightSum, again.WeightSum);
            Assert.Equal(world.IslandThreshold, again.Threshold);

            // An empty grid of the same geometry, laid with the budget by a plan of its own: the
            // seeding, to the bit.
            var empty = new GridField(
                Area, 0f, Depth, 0f, 0f, Rings, 2f,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: world.TankRadiusMetres, bed: world.Bed);
            GridField.IslandPlan own = empty.PlanIslands(
                world.IslandMap.Fbm, world.Config.MatterIslandCover, world.Config.MatterIslandDepthMetres);
            double laid = empty.DepositIslands(own, world.Config.MatterBudgetUnits);

            Assert.Equal(seeded, Cells(empty));
            Assert.Equal(world.MatterInitialTotal, laid);

            // A plan is laid only on the field that made it.
            Assert.Throws<ArgumentException>(() => grid.DepositIslands(own, 1d));
        }

        [Fact]
        public void AnIslandsInfluxWithNoIslandsIsRefusedAtConstruction()
        {
            ArgumentException noIslands = Assert.Throws<ArgumentException>(
                () => new World(Bedded(1.2f, burial: 0f, still: true, islands: false), seed: 5));
            _output.WriteLine(noIslands.Message);
            Assert.Contains("no islands are seeded", noIslands.Message);

            // On the cell field there are no columns to name.
            var cells = new RunConfig { MatterInfluxPerSecond = 1f, MatterInfluxAt = MatterInflux.Islands };
            ArgumentException noGrid = Assert.Throws<ArgumentException>(() => new World(cells, seed: 5));
            _output.WriteLine(noGrid.Message);
            Assert.Contains("only the grid field", noGrid.Message);

            // At influx 0 the route names nothing and nothing is refused or planned, as for the vent.
            var off = new World(Bedded(0f, burial: 0f, still: true, islands: false), seed: 5);
            Assert.Null(off.IslandInflux);

            // The surface and the vent are untouched by the islands: no plan is made for them.
            RunConfig surface = Bedded(1.2f, burial: 0f, still: true);
            surface.MatterInfluxAt = MatterInflux.Surface;
            Assert.Null(new World(surface, seed: 5).IslandInflux);
        }

        // ---------------------------------------------------------------------------------
        // The burial
        // ---------------------------------------------------------------------------------

        [Fact]
        public void BurialOnABeddedGridTakesFromEachColumnsLowestLiveCell()
        {
            // Uniform water, a quarter buried in one still second: every column's lowest live cell
            // keeps three quarters and every other cell keeps all of it.
            const float Rate = 0.25f;

            var world = new World(Bedded(0f, burial: Rate, still: true, islands: false), seed: 5);
            var grid = (GridField)world.Matter;
            double[] before = Cells(grid);

            world.Step(1f);

            int floors = 0, aboveTheLastLayer = 0;
            double floorBefore = 0d;
            int k = 0;

            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iy = 0; iy < grid.CellsY; iy++)
            for (int iz = 0; iz < grid.CellsZ; iz++, k++)
            {
                double now = grid.JoulesAt(ix, iy, iz);
                int lowest = grid.LowestLiveLayer(ix, iz);

                if (lowest >= 0 && iy == lowest)
                {
                    floors++;
                    floorBefore += before[k];
                    if (lowest < grid.CellsY - 1) aboveTheLastLayer++;
                    Assert.True(before[k] > 0d, "the floor cell was seeded");
                    Assert.Equal(before[k] * (1d - Rate), now, 6);
                }
                else
                {
                    Assert.Equal(before[k], now);
                }
            }

            _output.WriteLine(FormattableString.Invariant(
                $"{floors} floor cells, {aboveTheLastLayer} of them above the array's last layer; ") +
                FormattableString.Invariant($"buried {world.MatterBuriedTotal:R} of {floorBefore:R} on the floor"));

            // The bed has to move the floor for the test to mean anything: the old rule read the
            // last layer, which is water in a minority of these columns.
            Assert.True(aboveTheLastLayer > floors / 2, "the bed left most floors on the last layer");
            Assert.Equal(floorBefore * Rate, world.MatterBuriedTotal, 6);
            Assert.Equal(0d, world.MatterResidual, 9);
        }

        [Fact]
        public void AFlatGridBuriesExactlyAsTheLayerRuleDid()
        {
            // The rule D074 ran on the grid until D175, transcribed: a patch's last-layer stock,
            // a float draw of its fraction, the draw spread over the layer, and the counter moved by
            // the field's own before and after. On a flat box and a flat tank the per-column floor
            // must take the same doubles from the same cells in the same order.
            foreach (bool tank in new[] { false, true })
            {
                GridField Make() => tank
                    ? new GridField(
                        Area, 0.05f, Depth, 0f, 0f, Rings, 2f,
                        patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: TankGeometry.RadiusFor(Area))
                    : new GridField(Area, 0.05f, Depth, 0f, 0f, Rings, 2f);

                GridField old = Make(), now = Make();
                var map = new GradientNoise(seed: 11UL, wavelength: 8f);

                foreach (GridField g in new[] { old, now })
                {
                    g.SeedIslands(1000d, map.Fbm, cover: 0.3f);
                    for (int s = 0; s < 30; s++) g.Settle(0.5f);
                }

                Assert.Equal(Cells(old), Cells(now));

                double buriedOld = 0d, buriedNow = 0d;
                const double Fraction = 0.05;
                int floor = old.LayerCount - 1;

                for (int step = 0; step < 40; step++)
                {
                    for (int patch = 0; patch < Rings; patch++)
                    {
                        double before = old.StockInLayer(floor, patch);
                        if (before > 0d)
                        {
                            float wanted = (float)(before * Fraction);
                            if (wanted > 0f)
                            {
                                old.TakeFromLayer(floor, patch, wanted);
                                buriedOld += before - old.StockInLayer(floor, patch);
                            }
                        }

                        double from = now.FloorStock(patch);
                        if (from > 0d)
                        {
                            float wanted = (float)(from * Fraction);
                            if (wanted > 0f)
                            {
                                now.TakeFromFloor(patch, wanted);
                                buriedNow += from - now.FloorStock(patch);
                            }
                        }
                    }

                    old.Settle(0.5f);
                    now.Settle(0.5f);
                }

                _output.WriteLine(FormattableString.Invariant(
                    $"{(tank ? "tank" : "box ")}: buried {buriedNow:R} by the floor rule, {buriedOld:R} by the layer rule"));

                Assert.True(buriedOld > 0d, "nothing was buried");
                Assert.Equal(buriedOld, buriedNow);
                Assert.Equal(old.TotalJoules, now.TotalJoules);
                Assert.Equal(Cells(old), Cells(now));
            }
        }

        // ---------------------------------------------------------------------------------
        // The identity and the resume, in a living world
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheMatterIdentityClosesWithTheIslandsFedAndTheFloorBuried()
        {
            var world = new World(Bedded(1.2f, burial: 0.01f, still: false), seed: 2);
            for (int i = 0; i < 600; i++) world.Step(0.5f);

            double scale = world.MatterInitialTotal + world.MatterInfluxedTotal;

            _output.WriteLine(FormattableString.Invariant(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}; initial {world.MatterInitialTotal:0.###} ") +
                FormattableString.Invariant(
                $"+ in {world.MatterInfluxedTotal:0.###} - buried {world.MatterBuriedTotal:0.###}; residual {world.MatterResidual:R}"));

            Assert.NotEmpty(world.Living);
            Assert.True(world.StandingJoulesInBodies > 0d, "no matter was held in a body");
            // The counter also books what the floor's founders were made of (D098's leg 9), so the
            // islands' share is at least the rate times the clock.
            Assert.True(world.MatterInfluxedTotal >= 1.2d * 300d - 1e-3, "the influx fell short of its rate");
            Assert.True(world.MatterBuriedTotal > 0d, "the burial never fired");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * scale,
                FormattableString.Invariant($"identity open by {world.MatterResidual:R}"));
        }

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
        public void AResumeWithTheIslandsFedMatchesTheUnbrokenRun()
        {
            const ulong Seed = 2;
            var unbroken = new World(Bedded(1.2f, burial: 0.01f, still: false), Seed);
            for (int i = 0; i < 120; i++) unbroken.Step(0.5f);

            byte[] checkpoint = StateOf(unbroken);
            double influxAtCheckpoint = unbroken.MatterInfluxedTotal;

            // A fresh world on the same config and seed, overwritten by the checkpoint: the plan is
            // rebuilt by the construction and nothing of it was written.
            var resumed = new World(Bedded(1.2f, burial: 0.01f, still: false), Seed);
            using (var buffer = new MemoryStream(checkpoint, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                resumed.ReadState(r);
                Assert.Equal(checkpoint.Length, buffer.Position);
            }

            Assert.Equal(StateOf(unbroken), StateOf(resumed));

            for (int i = 0; i < 240; i++)
            {
                unbroken.Step(0.5f);
                resumed.Step(0.5f);
            }

            _output.WriteLine(FormattableString.Invariant(
                $"influx {influxAtCheckpoint:0.###} at the checkpoint, {unbroken.MatterInfluxedTotal:0.###} at the end; ") +
                FormattableString.Invariant($"alive {unbroken.Living.Count}, buried {unbroken.MatterBuriedTotal:0.###}"));

            Assert.True(unbroken.MatterInfluxedTotal > influxAtCheckpoint, "the influx did not run after the checkpoint");
            Assert.NotEmpty(unbroken.Living);
            Assert.Equal(unbroken.MatterInfluxedTotal, resumed.MatterInfluxedTotal);
            Assert.Equal(unbroken.MatterBuriedTotal, resumed.MatterBuriedTotal);
            Assert.Equal(StateOf(unbroken), StateOf(resumed));
        }
    }
}
