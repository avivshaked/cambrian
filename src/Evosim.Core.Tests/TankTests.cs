using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The tank — <c>fable-propose-aquarium.md</c> ruling 1, <c>logbook/specs/tank-spec.md</c>:
    /// the geometry, the grid's mask, the rings that replace the box's patches, the four worlds
    /// the shape refuses, and ruling 2's matter budget.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The invariant first.</b> <see cref="WorldShape.Box"/> is the default and at that default
    /// nothing moves: the same cell count, the same seeding, the same arithmetic. Every test in
    /// this file that touches a tank has a box beside it, because a shape that was reached by
    /// accident would be the most expensive kind of bug this project can have — a world quietly
    /// unlike the one its config names.
    /// </para>
    /// <para>
    /// <b>The water itself is <see cref="StreamsTests"/>.</b> Here the water only has to exist; there
    /// it has to be divergence-free, tangential at the glass and at the speed the knob says.
    /// </para>
    /// </remarks>
    public class TankTests
    {
        private readonly ITestOutputHelper _output;

        public TankTests(ITestOutputHelper output) => _output = output;

        // The aquarium's first round: 100 m2 over four rings, 60 m deep, which is a radius of
        // 5.642 m and a bounding square of 11.284 m. Neither the 1 m detritus cell nor the 5 m
        // matter cell divides that, which is the point of the mask.
        private const float Area = 100f;
        private const int Rings = 4;
        private const float Depth = 60f;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static GridField Tank(float cell, float sink = 0f, float refuge = 0f) =>
            new GridField(
                Area, sink, Depth, refuge, 0f, Rings, cell,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

        // ---------------------------------------------------------------------------------
        // The geometry
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheRadiusKeepsTheAreaAnArea()
        {
            // The whole reason the radius is derived rather than configured: a tank of 100 m2 has
            // the same footprint the 20 by 5 m box had, so the sun's aperture and the denominator
            // of every density are untouched by the change of shape.
            foreach (float area in new[] { 100f, 400f, 144f })
            {
                float r = TankGeometry.RadiusFor(area);
                Assert.Equal(area, (float)(Math.PI * r * r), 3);
            }

            _output.WriteLine(
                $"100 m2 is r {TankGeometry.RadiusFor(100f):0.000} m, " +
                $"400 m2 is r {TankGeometry.RadiusFor(400f):0.000} m");
        }

        [Fact]
        public void TheRingsAreOfEqualArea()
        {
            // Ten thousand points uniform over the disc should land in K rings in equal numbers,
            // because the rings are equal in area by construction. A ring boundary read as a
            // fraction of the radius rather than of the area would put four times as much water in
            // the outer ring as in the inner one and every per-patch density would be a readout of
            // the geometry.
            var rng = new Rng(101UL);
            var counts = new int[Rings];

            for (int i = 0; i < 10_000; i++)
            {
                double r = Radius * Math.Sqrt(rng.NextFloat());
                double theta = 2d * Math.PI * rng.NextFloat();

                counts[TankGeometry.RingOf(
                    Radius + r * Math.Cos(theta), Radius + r * Math.Sin(theta), Radius, Rings)]++;
            }

            _output.WriteLine($"rings: {string.Join(", ", counts)} of 10,000");

            foreach (int count in counts)
            {
                Assert.InRange(count, 10_000 / Rings - 400, 10_000 / Rings + 400);
            }
        }

        [Fact]
        public void TheAxisIsRingZeroAndTheGlassIsTheLast()
        {
            Assert.Equal(0, TankGeometry.RingOf(Radius, Radius, Radius, Rings));
            Assert.Equal(Rings - 1, TankGeometry.RingOf(2f * Radius, Radius, Radius, Rings));

            // Past the glass reads the last ring rather than throwing: float error at the wall is
            // not a world event, and the callers that care test Inside for themselves.
            Assert.Equal(Rings - 1, TankGeometry.RingOf(3f * Radius, Radius, Radius, Rings));
            Assert.True(TankGeometry.Inside(Radius, Radius, Radius));
            Assert.False(TankGeometry.Inside(2f * Radius + 0.01f, Radius, Radius));
        }

        [Fact]
        public void InsideWithClearanceKeepsBackFromTheGlass()
        {
            // wall-clearance-spec.md: Free asks whether a whole sphere clears the glass, not
            // just its centre point, so Inside gains a clearance and the existing three-argument
            // form is exactly its clearance-0 case.
            double r = Radius;

            // A point 0.49 m short of the glass with a 0.5 m clearance demanded is 0.01 m too
            // close: out.
            Assert.False(TankGeometry.Inside(2d * r - 0.49d, r, r, 0.5d));

            // 0.51 m short of the glass clears a 0.5 m clearance by a centimetre: in.
            Assert.True(TankGeometry.Inside(2d * r - 0.51d, r, r, 0.5d));

            // The axis is inside for any clearance below the radius — nowhere is further from
            // the glass than the axis is.
            Assert.True(TankGeometry.Inside(r, r, r, 0.5d));
            Assert.True(TankGeometry.Inside(r, r, r, r - 0.001d));

            // Clearance at or above the radius refuses everything, the axis included: there is
            // no point left that can hold that much distance from a wall this close.
            Assert.False(TankGeometry.Inside(r, r, r, r));
            Assert.False(TankGeometry.Inside(r, r, r, r + 1d));

            // Clearance 0 is exactly the existing three-argument form's case.
            Assert.Equal(
                TankGeometry.Inside(r + 0.3d, r, r),
                TankGeometry.Inside(r + 0.3d, r, r, 0d));

            _output.WriteLine(
                $"R {r:0.000}: R-0.49 at clearance 0.5 -> " +
                $"{TankGeometry.Inside(2d * r - 0.49d, r, r, 0.5d)}, " +
                $"R-0.51 at clearance 0.5 -> {TankGeometry.Inside(2d * r - 0.51d, r, r, 0.5d)}");
        }

        // ---------------------------------------------------------------------------------
        // The mask
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheMaskHoldsTheDiscToWithinOneRingOfCells()
        {
            foreach (float cell in new[] { 1f, 2f, 5f })
            {
                GridField field = Tank(cell);

                double expected = Math.PI * Radius * Radius / (cell * cell);
                double perLayer = (double)field.LiveCellCount / field.CellsY;

                // The rim is where a mask and a circle disagree, and it is one cell wide: the
                // count is out by at most the cells the circumference passes through, which is
                // 2*pi*R/cell of them.
                double rim = 2d * Math.PI * Radius / cell;

                _output.WriteLine(
                    $"cell {cell} m: {field.CellsX}x{field.CellsY}x{field.CellsZ} = {field.CellCount} cells, " +
                    $"{field.LiveCellCount} live ({perLayer:0.0} a layer against {expected:0.0}), " +
                    $"live volume {field.LiveVolumeCubicMetres:0} m3 against {Math.PI * Radius * Radius * Depth:0}");

                Assert.True(
                    Math.Abs(perLayer - expected) <= rim,
                    $"{perLayer:0.0} live cells a layer against {expected:0.0} expected, rim {rim:0.0}");
            }
        }

        [Fact]
        public void ABoxIsUnmaskedAndItsCellCountIsWhatItAlwaysWas()
        {
            // The campaign's own box: 100 m2 over four patches in a row is 20 m by 5 m by 60 m,
            // which on 1 m cells is 6,000 of them and every one of them water.
            var box = new GridField(100f, 0f, 60f, 0f, 0f, 4, 1f);

            Assert.Equal(WorldShape.Box, box.Shape);
            Assert.Equal(6_000, box.CellCount);
            Assert.Equal(6_000, box.LiveCellCount);
            Assert.Equal(6_000d, box.LiveVolumeCubicMetres, 6);
            Assert.True(box.IsLive(0, 0, 0));
            Assert.True(box.IsLive(19, 59, 4));
        }

        [Fact]
        public void SeedingFillsTheWaterAndNotTheCorners()
        {
            GridField field = Tank(1f);
            field.SeedUniform(1f);

            // The seeded total is the density times the water that exists, not times the bounding
            // square: stock in a dead cell could never be reached, mixed or advected, and would
            // sit outside every sum for the life of the run.
            Assert.Equal(field.LiveVolumeCubicMetres, field.TotalJoules, 6);
            Assert.Equal(field.TotalJoules, field.Recount(), 6);

            // A corner of the bounding square is dry.
            Assert.Equal(0d, field.JoulesAt(0, 0, 0));
            Assert.False(field.IsLive(0, 0, 0));
        }

        [Fact]
        public void StirringAndCarryingKeepTheTotalOnTheMask()
        {
            // The spec's conservation check: a thousand metabolic steps of mixing and advection at
            // the campaign's current, with nothing entering or leaving. A face between a live cell
            // and a dead one is glass, and water that crossed it would be water leaving the tank —
            // which on a running total would look exactly like a leak in the audit.
            GridField field = Tank(1f, sink: 0.002f);
            field.SeedUniform(1f);

            double seeded = field.TotalJoules;

            var current = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = 0.3f,
                PeriodSeconds = 6000f,
                AdvectFields = true,
            };

            current.SetBox(
                (float)Math.Sqrt(Area / Rings), Rings, Depth, seed: 5UL,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius);

            for (int step = 0; step < 1_000; step++)
            {
                field.Settle(0.5f);
                field.Mix(0.5f, 0.02f, 0.02f);
                field.Advect(current, step * 0.5, 0.5f, field.PatchWidthMetres);
            }

            double recounted = field.Recount();

            _output.WriteLine(
                $"seeded {seeded:0.000000}, standing {recounted:0.000000} after 1,000 steps " +
                $"of settle, mix and advect: {(recounted - seeded) / seeded:0.0e+0} relative");

            Assert.Equal(seeded, recounted, 6);

            // And nothing has been carried into the dry corners, which is the thing a mask can get
            // wrong while still conserving a total.
            double dry = 0d;
            for (int ix = 0; ix < field.CellsX; ix++)
            {
                for (int iz = 0; iz < field.CellsZ; iz++)
                {
                    if (field.IsLive(ix, 0, iz)) continue;

                    for (int iy = 0; iy < field.CellsY; iy++) dry += field.JoulesAt(ix, iy, iz);
                }
            }

            Assert.Equal(0d, dry);
        }

        [Fact]
        public void APointOutsideTheGlassReadsTheNearestWater()
        {
            GridField field = Tank(1f);
            field.SeedUniform(1f);

            // A body is stopped by a collider and the streams have no radial flow at the wall, so a
            // point past the circle is arithmetic rather than an event — and it must land in water
            // rather than in a dead cell, where a deposit would be lost to every sum.
            foreach (double theta in new[] { 0d, 1d, 2.5d, 4d, 5.8d })
            {
                var p = new FieldPoint(
                    new Float3(
                        (float)(Radius + (Radius + 3d) * Math.Cos(theta)),
                        -10f,
                        (float)(Radius + (Radius + 3d) * Math.Sin(theta))),
                    Rings - 1);

                Assert.True(field.DensityAt(p) > 0f, $"a point past the glass at theta {theta} read dry water");

                field.Deposit(p, 5f);
            }

            Assert.Equal(field.TotalJoules, field.Recount(), 6);
            Assert.Equal(field.LiveVolumeCubicMetres + 25d, field.TotalJoules, 6);
        }

        [Fact]
        public void APatchIsARingAndTheBinsAreEqual()
        {
            GridField field = Tank(1f);
            field.SeedUniform(1f);

            double[] byRing = new double[Rings];
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int layer = 0; layer < field.CellsY; layer++)
                {
                    byRing[ring] += field.StockInLayer(layer, ring);
                }
            }

            _output.WriteLine(
                $"per ring: {string.Join(", ", Array.ConvertAll(byRing, v => v.ToString("0")))} " +
                $"of {field.TotalJoules:0}");

            // Every live cell is in exactly one ring, so the rings sum to the whole; and the rings
            // are of equal area, so at a uniform seed they hold about the same. "About" is a cell
            // of slop at each boundary, which on a 1 m cell in a 5.6 m tank is a few per cent.
            double sum = 0d;
            foreach (double v in byRing) sum += v;
            Assert.Equal(field.TotalJoules, sum, 6);

            foreach (double v in byRing)
            {
                Assert.InRange(v, 0.8 * field.TotalJoules / Rings, 1.25 * field.TotalJoules / Rings);
            }
        }

        // ---------------------------------------------------------------------------------
        // The world
        // ---------------------------------------------------------------------------------

        private static RunConfig TankWorld() => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            WorldShape = WorldShape.Tank,
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = Area,
            HorizontalPatches = Rings,
            WorldDepthMetres = Depth,
            FieldCellMetres = 1f,
            FieldMatterCellMetres = 5f,
            NutrientMixingDiffusivity = 0.02f,
            HorizontalMixingDiffusivity = 0.02f,
            MatterMixingDiffusivity = 0.02f,
            Current = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = 0.1f,
                PeriodSeconds = 6000f,
                AdvectFields = true,
                VentDepthMetres = Depth,
            },
        };

        [Fact]
        public void ATankIsBuiltAtTheRadiusItsAreaImplies()
        {
            var world = new World(TankWorld(), seed: 3);

            Assert.Equal(Radius, world.TankRadiusMetres, 4);
            Assert.Equal(WorldShape.Box, new RunConfig().WorldShape);
            Assert.Equal(0f, new World(new RunConfig(), seed: 3).TankRadiusMetres);

            var detritus = (GridField)world.Nutrients;
            var matter = (GridField)world.Matter;

            _output.WriteLine(
                $"tank r {world.TankRadiusMetres:0.000} m, {Depth} m deep: " +
                $"detritus {detritus.LiveCellCount} live of {detritus.CellCount}, " +
                $"matter {matter.LiveCellCount} live of {matter.CellCount}; " +
                $"matter standing {world.StandingMatter:0.0} units");

            Assert.Equal(WorldShape.Tank, detritus.Shape);
            Assert.Equal(WorldShape.Tank, matter.Shape);
            Assert.True(detritus.LiveCellCount < detritus.CellCount, "the mask kept every cell");
            Assert.True(matter.LiveCellCount < matter.CellCount, "the matter mask kept every cell");
        }

        [Fact]
        public void ATankRunsAndBothBooksClose()
        {
            RunConfig config = TankWorld();
            config.MatterInfluxPerSecond = 0.05f;
            config.MatterBurialPerSecond = 0.001f;

            var world = new World(config, seed: 7);

            for (int step = 0; step < 2_000; step++) world.Step(0.5f);

            double identity = world.MatterInitialTotal + world.MatterInfluxedTotal -
                world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in; " +
                $"matter identity {identity:R} of {world.MatterInitialTotal:0}");

            // The same two hard equalities every other world is held to. A mask that leaked would
            // show up here first: stock carried into a dead cell is stock the world still counts
            // and nothing can ever reach.
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * world.MatterInitialTotal,
                $"matter identity {identity:R}");

            Assert.Equal(world.Nutrients.TotalJoules, ((GridField)world.Nutrients).Recount(), 6);
            Assert.Equal(world.Matter.TotalJoules, ((GridField)world.Matter).Recount(), 6);
        }

        [Fact]
        public void ATankRefusesTheFourWorldsItCannotDescribe()
        {
            RunConfig cells = TankWorld();
            cells.FieldModel = MatterField.Cells;
            Assert.Contains(
                "WorldShape is Tank and FieldModel is Cells",
                Assert.Throws<ArgumentException>(() => new World(cells, seed: 1)).Message);

            // The fifth, which is not on the spec's list and is refused for the list's own reason:
            // a vertex field in a tank would wrap a quantum at a seam that is glass.
            RunConfig vertices = TankWorld();
            vertices.FieldModel = MatterField.Vertices;
            Assert.Contains(
                "WorldShape is Tank and FieldModel is Vertices",
                Assert.Throws<ArgumentException>(() => new World(vertices, seed: 1)).Message);

            RunConfig layout = TankWorld();
            layout.PatchesAcross = 2f;
            Assert.Contains(
                "WorldShape is Tank and PatchesAcross is 2",
                Assert.Throws<ArgumentException>(() => new World(layout, seed: 1)).Message);

            RunConfig lottery = TankWorld();
            lottery.DispersalChancePerStep = 0.01f;
            Assert.Contains(
                "WorldShape is Tank and DispersalChancePerStep",
                Assert.Throws<ArgumentException>(() => new World(lottery, seed: 1)).Message);

            RunConfig rolls = TankWorld();
            rolls.Current.Mode = CurrentMode.Rolls;
            rolls.Current.Speed = 0.1f;
            Assert.Contains(
                "the current is Rolls",
                Assert.Throws<ArgumentException>(() => new World(rolls, seed: 1)).Message);

            // And still water under the rolls is not refused, because at 0 m/s the two fields are
            // the same water and a launcher that never set a current must still be able to run a
            // tank.
            RunConfig still = TankWorld();
            still.Current.Mode = CurrentMode.Rolls;
            still.Current.Speed = 0f;
            _ = new World(still, seed: 1);
        }

        // ---------------------------------------------------------------------------------
        // Ruling 2: the matter budget
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ABudgetOfZeroIsTheDensityRuleExactly()
        {
            // The invariant, in the one place a reader would look for it: the knob's default is a
            // world that never heard of it.
            Assert.Equal(0f, new RunConfig().MatterBudgetUnits);

            var untouched = new World(new RunConfig(), seed: 11);
            var named = new World(new RunConfig { MatterBudgetUnits = 0f }, seed: 11);

            Assert.Equal(untouched.MatterInitialTotal, named.MatterInitialTotal, 6);
            Assert.Equal(untouched.StandingMatter, named.StandingMatter, 6);
        }

        [Fact]
        public void ABudgetSetsTheTotalAndTheAreaNoLongerDoes()
        {
            // Ruling 2's whole point. Four times the footprint at the same budget is a quarter of
            // the density and the same number of units in the world, which is what makes dilution
            // a dial rather than a population cut.
            RunConfig small = TankWorld();
            small.MatterBudgetUnits = 6_000f;

            RunConfig large = TankWorld();
            large.WorldAreaSquareMetres = 4f * Area;
            large.MatterBudgetUnits = 6_000f;

            var tight = new World(small, seed: 3);
            var dilute = new World(large, seed: 3);

            double tightDensity = tight.MatterInitialTotal / ((GridField)tight.Matter).LiveVolumeCubicMetres;
            double diluteDensity = dilute.MatterInitialTotal / ((GridField)dilute.Matter).LiveVolumeCubicMetres;

            _output.WriteLine(
                $"{Area} m2: {tight.MatterInitialTotal:0.0} units at {tightDensity:0.0000}/m3; " +
                $"{4f * Area} m2: {dilute.MatterInitialTotal:0.0} units at {diluteDensity:0.0000}/m3 " +
                $"({tightDensity / diluteDensity:0.00}x thinner)");

            Assert.Equal(6_000d, tight.MatterInitialTotal, 0);
            Assert.Equal(6_000d, dilute.MatterInitialTotal, 0);
            Assert.True(tightDensity > 3.5 * diluteDensity, "four times the water was not four times thinner");
        }

        [Fact]
        public void ABudgetWorksInABoxToo()
        {
            // The knob is ruling 2 and the shape is ruling 1, and they are separate rulings: a box
            // held at a total is a legal world and the one a reader would reach for to separate
            // the dilution from the container.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                HorizontalPatches = 4f,
                WorldDepthMetres = 60f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 5f,
                MatterBudgetUnits = 6_000f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
            };

            var world = new World(config, seed: 3);

            Assert.Equal(6_000d, world.MatterInitialTotal, 0);
            Assert.Equal(6_000d, world.StandingMatter, 0);
        }
    }
}
