using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The grid masked below the floor — D092, <c>logbook/specs/bed-spec.md</c> item 9, and the
    /// constant-field check <c>logbook/specs/transport-conserves-spec.md</c> makes on the flat one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No new cells and no new branches.</b> The bed adds one condition to the mask a tank
    /// already carries, and everything that runs over live cells follows from it. So what these
    /// tests ask is whether the consequences actually followed: whether the count matches the
    /// volume the floor leaves, whether a column's lowest live cell is the one above its own
    /// floor, whether settling stops there, and whether the transporter still keeps uniform water
    /// uniform when the array it runs on is a stair-stepped bowl rather than a cylinder.
    /// </para>
    /// <para>
    /// <b>Read at two footprints for two reasons.</b> The mask and the settling are read at
    /// 400 m², round 38's own tank, where the relief is metres and moves the floor by several
    /// cells; the constant-field check is read at 100 m², where the array is a quarter the size
    /// and 1,200 steps of the transporter is seconds rather than half a minute.
    /// </para>
    /// </remarks>
    public class BedGridTests
    {
        private readonly ITestOutputHelper _output;

        public BedGridTests(ITestOutputHelper output) => _output = output;

        private const float Depth = 60f;
        private const int Patches = 4;
        private const float Period = 6000f;
        private const ulong RunSeed = 20260915UL;

        private static float RadiusOf(float area) => TankGeometry.RadiusFor(area);

        /// <summary>
        /// A bed with as much relief as the 30° bound allows at this footprint: the dial is asked
        /// for well above what can fit, so the bound decides and the floor moves by as many cells
        /// as the geometry permits. <see cref="BedShapeTests"/> has the table.
        /// </summary>
        private static BedShape Bed(float area, float tilt = 3f, ulong seed = RunSeed) =>
            new BedShape(
                RadiusOf(area), Depth, 12f, tilt, 2f * RadiusOf(area),
                Rng.SeedFor(seed, World.BedShapeIndex));

        private static GridField Grid(float area, float cell, BedShape bed, float sink = 0f) =>
            new GridField(
                area, sink, Depth, 0f, 0f, Patches, cell,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: RadiusOf(area),
                bed: bed);

        private static CurrentField Streams(float area, BedShape bed, float speed = 0.1f)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(area / Patches), Patches, Depth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: RadiusOf(area),
                bed: bed);

            return field;
        }

        /// <summary>min, max and sd/mean of the density over the live cells.</summary>
        private static (double Min, double Max, double Mean, double Cv) Spread(GridField field)
        {
            double volume = field.CellVolume;
            double min = double.MaxValue, max = double.MinValue, sum = 0d, square = 0d;
            int n = 0;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz)) continue;

                double density = field.JoulesAt(ix, iy, iz) / volume;
                if (density < min) min = density;
                if (density > max) max = density;
                sum += density;
                square += density * density;
                n++;
            }

            double mean = sum / n;
            return (min, max, mean, Math.Sqrt(Math.Max(0d, square / n - mean * mean)) / mean);
        }

        [Fact]
        public void TheMaskStopsAtTheFloor()
        {
            // Spec item 9's first clause, and spec item 5's: the map is mean-zero over the disc,
            // so the water it leaves is the flat tank's to within the cells the stair-step cuts.
            const float Area = 400f;

            foreach (float cell in new[] { 1f, 5f })
            {
                BedShape bed = Bed(Area);
                GridField sloped = Grid(Area, cell, bed);
                GridField flat = Grid(Area, cell, null);

                double columns = 0d;
                double analytic = 0d;

                for (int ix = 0; ix < sloped.CellsX; ix++)
                for (int iz = 0; iz < sloped.CellsZ; iz++)
                {
                    double cx = (ix + 0.5d) * cell;
                    double cz = (iz + 0.5d) * cell;
                    if (!TankGeometry.Inside(cx, cz, RadiusOf(Area))) continue;

                    columns++;
                    analytic += -bed.FloorY(cx, cz) * cell * cell;
                }

                double live = sloped.LiveVolumeCubicMetres;

                _output.WriteLine(
                    $"{cell} m cells: {sloped.LiveCellCount} live of {sloped.CellCount} " +
                    $"({live:0} m³) against the flat tank's {flat.LiveCellCount} " +
                    $"({flat.LiveVolumeCubicMetres:0} m³) and the floor's own integral " +
                    $"{analytic:0} m³ — {(live - analytic) / analytic:0.00%} out, " +
                    $"{(live - analytic) / (columns * cell * cell * cell):0.000} of a cell layer");

                // Within one cell layer over the footprint, which is the resolution a mask that
                // asks about a cell's centre can have.
                Assert.True(
                    Math.Abs(live - analytic) < columns * cell * cell * cell,
                    $"the mask is {Math.Abs(live - analytic):0} m³ from the floor's own integral");

                // And every column's lowest live cell is the one above its own floor, with the
                // cell below it under the floor — the mask's own rule, read back one column at a
                // time rather than in aggregate.
                for (int ix = 0; ix < sloped.CellsX; ix++)
                for (int iz = 0; iz < sloped.CellsZ; iz++)
                {
                    int lowest = sloped.LowestLiveLayer(ix, iz);
                    if (lowest < 0) continue;

                    double floor = sloped.FloorYAtColumn(ix, iz);

                    Assert.True(
                        -((lowest + 0.5d) * cell) > floor,
                        $"column {ix},{iz}'s lowest live cell is below its floor");

                    if (lowest + 1 < sloped.CellsY)
                    {
                        Assert.False(sloped.IsLive(ix, lowest + 1, iz));
                        Assert.True(-((lowest + 1.5d) * cell) <= floor);
                    }
                }
            }
        }

        [Fact]
        public void AtReliefZeroTheMaskAndTheArithmeticAreTheFlatTanks()
        {
            // Spec item 12 on the grid's side. A BedShape with no relief is not held at all
            // (GridField.Bed is null), so what is asserted here is the consequence: the same mask,
            // the same seeded total, and the same stock after a run of every operator the grid has.
            const float Area = 100f;
            const float Cell = 1f;

            var level = new BedShape(RadiusOf(Area), Depth, 0f, 0f, 0f, 1UL);

            GridField withBed = Grid(Area, Cell, level, sink: 0.02f);
            GridField without = Grid(Area, Cell, null, sink: 0.02f);

            Assert.Null(withBed.Bed);
            Assert.Equal(without.LiveCellCount, withBed.LiveCellCount);

            CurrentField a = Streams(Area, null);
            CurrentField b = Streams(Area, null);

            withBed.SeedUniform(1f);
            without.SeedUniform(1f);

            for (int step = 1; step <= 40; step++)
            {
                double t = step * 0.5d;

                withBed.Settle(0.5f);
                withBed.Mix(0.5f, 0.02f);
                withBed.Advect(a, t, 0.5f, withBed.PatchWidthMetres);

                without.Settle(0.5f);
                without.Mix(0.5f, 0.02f);
                without.Advect(b, t, 0.5f, without.PatchWidthMetres);
            }

            for (int ix = 0; ix < withBed.CellsX; ix++)
            for (int iy = 0; iy < withBed.CellsY; iy++)
            for (int iz = 0; iz < withBed.CellsZ; iz++)
            {
                Assert.Equal(without.IsLive(ix, iy, iz), withBed.IsLive(ix, iy, iz));
                Assert.Equal(without.JoulesAt(ix, iy, iz), withBed.JoulesAt(ix, iy, iz));
            }

            _output.WriteLine(
                $"{withBed.LiveCellCount} live cells and {withBed.Recount():0.000000} J, cell for " +
                "cell identical to the flat tank after 40 steps of settling, mixing and transport");
        }

        [Fact]
        public void TheRefugeStockIsTheFlatLayerAndFollowsAShapedFloor()
        {
            // Spec item 5's report clause. `refuge J` has always read
            // StockInLayer(LayerCount - 1, 0), which on a shaped tank is a layer under all but the
            // deepest hollow — so the report would read almost nothing while the sediment sat in
            // the columns above it. GridField.RefugeStock reads each column's own floor cell
            // instead, and the two things asserted here are the two the column needs: that on a
            // flat grid it is the old expression to the bit, and that on a shaped one it is every
            // live floor cell there is.
            const float Area = 400f;
            const float Cell = 1f;

            GridField flat = Grid(Area, Cell, null, sink: 0.5f);
            flat.SeedUniform(1f);
            for (int step = 0; step < 1200; step++) flat.Settle(0.5f);

            for (int patch = 0; patch < Patches; patch++)
            {
                Assert.Equal(flat.StockInLayer(flat.LayerCount - 1, patch), flat.RefugeStock(patch));
            }

            BedShape bed = Bed(Area);
            GridField shaped = Grid(Area, Cell, bed, sink: 0.5f);
            shaped.SeedUniform(1f);
            for (int step = 0; step < 1200; step++) shaped.Settle(0.5f);

            // Every live floor cell, counted here the long way round — over the columns, from the
            // array rather than from the method under test — so the two can disagree.
            double byHand = 0d;

            for (int ix = 0; ix < shaped.CellsX; ix++)
            for (int iz = 0; iz < shaped.CellsZ; iz++)
            {
                int lowest = shaped.LowestLiveLayer(ix, iz);
                if (lowest >= 0) byHand += shaped.JoulesAt(ix, lowest, iz);
            }

            double byMethod = 0d;
            for (int patch = 0; patch < Patches; patch++) byMethod += shaped.RefugeStock(patch);

            double lastLayer = 0d;
            for (int patch = 0; patch < Patches; patch++)
            {
                lastLayer += shaped.StockInLayer(shaped.LayerCount - 1, patch);
            }

            _output.WriteLine(
                $"shaped tank: the floor cells hold {byMethod:0.0} J of {shaped.Recount():0.0} J, " +
                $"where the array's last layer holds {lastLayer:0.0} J — " +
                $"{lastLayer / Math.Max(1e-9d, byMethod):0.0%} of it");

            Assert.Equal(byHand, byMethod, 6);
            Assert.True(byMethod > 0.99d * shaped.Recount(), "the settled stock is not on the floor");
            Assert.True(
                lastLayer < 0.2d * byMethod,
                "the array's last layer already holds the floor's stock, so this reading is not " +
                "testing anything the old one did not do");
        }

        [Fact]
        public void SettlingStopsAtTheColumnsOwnFloor()
        {
            // Spec item 9's second clause: detritus that reaches the lowest live cell of its
            // column stays there, which is what makes the floor's low spots the places stock
            // gathers in. The flat tank's rule, applied to a floor that is no longer flat.
            const float Area = 400f;
            const float Cell = 1f;

            BedShape bed = Bed(Area);
            GridField field = Grid(Area, Cell, bed, sink: 0.5f);

            field.SeedUniform(1f);
            double initial = field.Recount();

            // Long enough that everything has reached the floor: 0.5 m/s over 60 m is 120 s, and
            // the fraction is capped at half a cell a step.
            for (int step = 0; step < 1200; step++) field.Settle(0.5f);

            double onTheFloor = 0d, aboveIt = 0d, inTheRock = 0d;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                int lowest = field.LowestLiveLayer(ix, iz);

                for (int iy = 0; iy < field.CellsY; iy++)
                {
                    double stock = field.JoulesAt(ix, iy, iz);

                    if (lowest < 0 || !field.IsLive(ix, iy, iz)) inTheRock += stock;
                    else if (iy == lowest) onTheFloor += stock;
                    else aboveIt += stock;
                }
            }

            _output.WriteLine(
                $"after 600 s of settling: {onTheFloor / initial:0.0%} on the floor, " +
                $"{aboveIt / initial:0.0%} still in the water, {inTheRock:0.000000} J in the rock; " +
                $"the total moved by {(field.Recount() - initial) / initial:0.0e+0}");

            Assert.Equal(0d, inTheRock);
            Assert.True(onTheFloor > 0.99d * initial, $"only {onTheFloor / initial:0.0%} reached the floor");
            Assert.Equal(initial, field.Recount(), 6);
        }

        [Fact]
        public void TheHollowsHoldTheMost()
        {
            // What the round reads, and the reading itself: the detritus each column's floor cell
            // holds against that column's floor height. Reported rather than gated at a
            // correlation, but the sign is asserted — a floor that gathered stock on its rises
            // would mean the mask and the settling disagree about which way is down.
            // 100 m² and a sink ten times the campaign's, so that the mechanism is visible inside
            // a test that takes seconds: at 0.02 m/s a parcel falls 12 m in the 600 s window and
            // most of the water never reaches any floor at all, which reads the clock rather than
            // the floor. The sign is the claim; the campaign's own rate is a run's reading.
            const float Area = 100f;
            const float Cell = 1f;

            BedShape bed = Bed(Area);
            GridField field = Grid(Area, Cell, bed, sink: 0.2f);
            CurrentField current = Streams(Area, bed);

            field.SeedUniform(1f);

            for (int step = 1; step <= 1200; step++)
            {
                double t = step * 0.5d;
                field.Settle(0.5f);
                field.Mix(0.5f, 0.02f);
                field.Advect(current, t, 0.5f, field.PatchWidthMetres);
            }

            (float[] floors, double[] stock) = field.ColumnFloorAndFloorStock();

            double meanFloor = 0d, meanStock = 0d;
            for (int i = 0; i < floors.Length; i++)
            {
                meanFloor += floors[i] / floors.Length;
                meanStock += stock[i] / floors.Length;
            }

            double cov = 0d, varFloor = 0d, varStock = 0d;
            for (int i = 0; i < floors.Length; i++)
            {
                double f = floors[i] - meanFloor;
                double s = stock[i] - meanStock;
                cov += f * s;
                varFloor += f * f;
                varStock += s * s;
            }

            double r = cov / Math.Sqrt(varFloor * varStock);

            _output.WriteLine(
                $"{floors.Length} live columns, floors from {Min(floors):0.00} to {Max(floors):0.00} m; " +
                $"the floor cell holds {meanStock:0.0} J on average, and the correlation between " +
                $"the floor's height and what its cell holds is r = {r:0.000} " +
                $"(negative means the low places hold the most)");

            Assert.True(r < 0d, $"the rises hold the most: r = {r:0.000}");
        }

        private static double Min(float[] values)
        {
            double m = double.MaxValue;
            foreach (float v in values) m = Math.Min(m, v);
            return m;
        }

        private static double Max(float[] values)
        {
            double m = double.MinValue;
            foreach (float v in values) m = Math.Max(m, v);
            return m;
        }

        [Fact]
        public void AUniformConcentrationStaysUniformOnTheSlopedGrid()
        {
            // The check transport-conserves-spec.md makes on the flat grid, on the sloped one: a
            // uniform dissolved concentration carried by incompressible water in a closed
            // container stays uniform, and conservation alone does not imply it. The scheme takes
            // its face fluxes from the potential and every edge that touches rock is zero, so the
            // stair-stepped floor is a wall like the glass — but a pullback that had the potential
            // and the velocity describing slightly different fields would show up here and
            // nowhere else.
            const int Steps = 1200;
            const float Dt = 0.5f;
            const float Area = 100f;

            _output.WriteLine(
                "cell  mixing  bed      |  min        max        sd/mean  max|c-1|  total err  " +
                "substeps  discrete/analytic RMS");

            foreach (float cell in new[] { 1f, 5f })
            foreach (bool bedded in new[] { false, true })
            {
                float diffusivity = cell == 1f ? 0.02f : 2f;

                BedShape bed = bedded ? Bed(Area) : null;
                GridField field = Grid(Area, cell, bed);
                CurrentField current = Streams(Area, bed);

                field.SeedUniform(1f);
                double initial = field.Recount();

                // The resolution reading transport-conserves-spec.md asks of every new container:
                // the scheme's own face-normal speeds against the analytic field's at the same
                // faces. They part at a boundary, where the discrete field is tangential to the
                // mask's stair-step and the analytic one to the real surface — and a sloped floor
                // is a great deal more stair-step than a flat one.
                (int substeps, _, double discrete, double analytic, _, double worstNet) =
                    field.MeasureFaceFluxes(current, 0d, Dt);

                for (int step = 1; step <= Steps; step++)
                {
                    field.Mix(Dt, diffusivity);
                    field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                }

                (double min, double max, double mean, double cv) = Spread(field);
                double drift = (field.Recount() - initial) / initial;
                double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

                _output.WriteLine(
                    $"{cell,4} {diffusivity,7:0.##} {(bedded ? "sloped" : "flat  "),-8} | " +
                    $"{min,-10:0.000000} {max,-10:0.000000} {cv,-8:0.0%} {worst,-9:0.0e+0} " +
                    $"{drift,9:0.0e+0}  {substeps,8}  {discrete:0.0000} / {analytic:0.0000} m/s " +
                    $"({discrete / analytic:0.00}x)");

                Assert.Equal(1.0, mean, 9);
                Assert.True(
                    worst < 1e-9,
                    $"{cell} m cells, bed {bedded}: the density ran {min:R} to {max:R} where a " +
                    "uniform field must stay at 1");

                // The scheme's own invariant, on the sloped mask: every live cell's six signed
                // fluxes are built from twelve edge values, each appearing twice with opposite
                // sign.
                Assert.Equal(0d, worstNet);
            }
        }

        [Fact]
        public void ThePatchIsCarriedWholeAndStaysOutOfTheRock()
        {
            // Conservation and positivity on the sloped mask, with a patch rather than a uniform
            // field so that there is something for the transporter to be wrong about.
            const float Area = 100f;
            const float Cell = 1f;

            BedShape bed = Bed(Area);
            GridField field = Grid(Area, Cell, bed, sink: 0.02f);
            CurrentField current = Streams(Area, bed);

            var rng = new Rng(53UL);

            for (int i = 0; i < 200; i++)
            {
                float r = (float)(RadiusOf(Area) * 0.6d * Math.Sqrt(rng.NextFloat()));
                double theta = 2d * Math.PI * rng.NextFloat();

                field.Deposit(
                    new FieldPoint(
                        new Float3(
                            (float)(RadiusOf(Area) + r * Math.Cos(theta)),
                            -10f - rng.NextFloat() * 20f,
                            (float)(RadiusOf(Area) + r * Math.Sin(theta))),
                        0),
                    100f);
            }

            double initial = field.Recount();

            for (int step = 1; step <= 400; step++)
            {
                double t = step * 0.5d;
                field.Settle(0.5f);
                field.Mix(0.5f, 0.02f);
                field.Advect(current, t, 0.5f, field.PatchWidthMetres);
            }

            double inTheRock = 0d, lowest = 0d;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                double stock = field.JoulesAt(ix, iy, iz);
                lowest = Math.Min(lowest, stock);
                if (!field.IsLive(ix, iy, iz)) inTheRock += stock;
            }

            double drift = (field.Recount() - initial) / initial;

            _output.WriteLine(
                $"200 deposits carried 200 s over a sloped floor: total moved by {drift:0.0e+0}, " +
                $"{inTheRock:0.000000} J in the rock, the emptiest cell holds {lowest:0.000000} J");

            Assert.Equal(0d, inTheRock);
            Assert.True(lowest >= 0d, $"a cell went negative: {lowest:R}");
            Assert.True(Math.Abs(drift) < 1e-12, $"the total moved by {drift:0.0e+0}");
        }

        [Fact]
        public void TheGridRefusesABedItIsNotShapedLike()
        {
            // One geometry reaches the fields, the water and the placer, or the mask and the
            // collider are two different floors — TankRadiusMetres' own rule, applied to the bed.
            BedShape bed = Bed(100f);

            Assert.Throws<ArgumentException>(
                () => new GridField(
                    100f, 0f, Depth, 0f, 0f, Patches, 1f, 1, WorldShape.Box, 0f, bed));

            Assert.Throws<ArgumentException>(
                () => new GridField(
                    400f, 0f, Depth, 0f, 0f, Patches, 1f, 1, WorldShape.Tank,
                    RadiusOf(400f), bed));

            Assert.Throws<ArgumentException>(
                () => new GridField(
                    100f, 0f, 30f, 0f, 0f, Patches, 1f, 1, WorldShape.Tank,
                    RadiusOf(100f), bed));

            var current = new CurrentField { Mode = CurrentMode.Transport, Speed = 0.1f };

            Assert.Throws<ArgumentException>(
                () => current.SetBox(5f, Patches, Depth, 1UL, 1, WorldShape.Box, 0f, bed));

            Assert.Throws<ArgumentException>(
                () => current.SetBox(
                    5f, Patches, Depth, 1UL, 1, WorldShape.Tank, RadiusOf(400f), bed));
        }
    }
}
