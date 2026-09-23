using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The rock in the grid — <c>logbook/specs/reef-spec.md</c> §4, tests 1 to 3: the mask and
    /// its intervals, uniform water staying uniform around three reefs, and snow settling onto a
    /// cap's table above it and onto the floor below it.
    /// </summary>
    public class ReefGridTests
    {
        private readonly ITestOutputHelper _output;

        public ReefGridTests(ITestOutputHelper output) => _output = output;

        /// <summary>The horizontal distance from a column's centre to the nearest reef's axis.</summary>
        private static double AxisDistance(ReefGeometry reefs, GridField grid, int ix, int iz)
        {
            double x = (ix + 0.5d) * grid.CellMetres, z = (iz + 0.5d) * grid.CellMetres;
            double best = double.MaxValue;

            for (int i = 0; i < reefs.Count; i++)
            {
                double dx = x - reefs.CentreX(i), dz = z - reefs.CentreZ(i);
                best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        [Fact]
        public void TheMaskCutsTheRockAndSplitsTheCapColumns()
        {
            ReefGeometry reefs = ReefTank.Reefs();
            GridField plain = ReefTank.Grid(null);
            GridField rocky = ReefTank.Grid(reefs);

            int rock = 0, capColumns = 0, stemColumns = 0;

            for (int ix = 0; ix < plain.CellsX; ix++)
            for (int iz = 0; iz < plain.CellsZ; iz++)
            {
                for (int iy = 0; iy < plain.CellsY; iy++)
                {
                    bool inRock = reefs.Inside(
                        (ix + 0.5d) * plain.CellMetres, -((iy + 0.5d) * plain.CellMetres), (iz + 0.5d) * plain.CellMetres);

                    Assert.Equal(plain.IsLive(ix, iy, iz) && !inRock, rocky.IsLive(ix, iy, iz));
                    if (plain.IsLive(ix, iy, iz) && inRock) rock++;
                }

                if (!plain.ColumnIsLive(ix, iz)) continue;

                // The plain grid is one interval from the top to the floor everywhere.
                Assert.Equal(1, plain.LiveIntervalCount(ix, iz));

                double rho = AxisDistance(reefs, rocky, ix, iz);

                if (rho > 1.3d && rho < 1.9d)
                {
                    // Under the cap's flat, clear of the stem and its fillet: water 0 to 3 m,
                    // rock 3 to 5 m, water 5 m to the floor.
                    capColumns++;
                    Assert.Equal(2, rocky.LiveIntervalCount(ix, iz));
                    Assert.Equal((0, 2), rocky.LiveInterval(ix, iz, 0));
                    Assert.Equal((5, 11), rocky.LiveInterval(ix, iz, 1));
                    Assert.Equal(11, rocky.LowestLiveLayer(ix, iz));
                    Assert.Equal(2, rocky.RestingLayer(ix, 0, iz));
                    Assert.Equal(11, rocky.RestingLayer(ix, 6, iz));
                    Assert.Equal(-1, rocky.RestingLayer(ix, 3, iz));
                }
                else if (rho < 0.7d)
                {
                    // Through the stem: water over the cap only, and the floor is the cap's top.
                    stemColumns++;
                    Assert.Equal(1, rocky.LiveIntervalCount(ix, iz));
                    Assert.Equal((0, 2), rocky.LiveInterval(ix, iz, 0));
                    Assert.Equal(2, rocky.LowestLiveLayer(ix, iz));
                }
                else if (rho > 3.5d)
                {
                    Assert.Equal(1, rocky.LiveIntervalCount(ix, iz));
                    Assert.Equal(plain.LowestLiveLayer(ix, iz), rocky.LowestLiveLayer(ix, iz));
                }
            }

            // The rock's volume within the tank, by the shape: three caps (a flat disc of radius
            // r − t/2 and a half-torus rim) and three stems from the underside to the floor.
            double a = ReefTank.CapRadius - 0.5d * ReefTank.Thickness, h = 0.5d * ReefTank.Thickness;
            // The rim by Pappus: a half disc of radius h swept round a ring of radius a.
            double cap = Math.PI * a * a * ReefTank.Thickness + Math.PI * Math.PI * h * h * a + (4d / 3d) * Math.PI * h * h * h;
            double stem = Math.PI * ReefTank.Stem * ReefTank.Stem * (ReefTank.Depth - ReefTank.CapDepth - ReefTank.Thickness);
            double shape = 3d * (cap + stem);

            _output.WriteLine(
                $"{plain.LiveCellCount} live cells without the rock, {rocky.LiveCellCount} with it: {rock} cells " +
                $"of rock against {shape:0.0} m³ by the shape ({rock / shape:0.000}); {capColumns} columns split " +
                $"by a cap, {stemColumns} through a stem");

            Assert.Equal(plain.LiveCellCount - rock, rocky.LiveCellCount);
            Assert.True(capColumns >= 3 && stemColumns >= 3);
            Assert.True(Math.Abs(rock - shape) < 0.25d * shape, "the rock's cell count is far from its volume");
        }

        [Fact]
        public void WithNoReefTheMaskIsTheRecordedOne()
        {
            // Test 1's last clause: a grid built with no reefs, the recorded way, and one handed an
            // explicit null, and a world at ReefCount 0, carry the same mask and one interval a
            // column. The crowd fixture's regress (test 7) is the caller's.
            GridField recorded = new GridField(
                ReefTank.Area, 0f, ReefTank.Depth, 0f, 0f, ReefTank.Patches, ReefTank.Cell,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: ReefTank.Radius);
            GridField nulled = ReefTank.Grid(null);

            Assert.Null(nulled.Reefs);
            Assert.Equal(recorded.LiveCellCount, nulled.LiveCellCount);

            for (int ix = 0; ix < recorded.CellsX; ix++)
            for (int iz = 0; iz < recorded.CellsZ; iz++)
            {
                Assert.Equal(recorded.LowestLiveLayer(ix, iz), nulled.LowestLiveLayer(ix, iz));
                Assert.Equal(recorded.ColumnIsLive(ix, iz) ? 1 : 0, nulled.LiveIntervalCount(ix, iz));

                for (int iy = 0; iy < recorded.CellsY; iy++)
                {
                    Assert.Equal(recorded.IsLive(ix, iy, iz), nulled.IsLive(ix, iy, iz));
                }
            }

            RunConfig config = ReefTank.Config(count: 0);
            config.Current = new CurrentField { Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = ReefTank.Period, AdvectFields = true };
            var world = new World(config, seed: 3);

            Assert.Null(world.Reefs);
            Assert.Null(world.Field.Reefs);
            Assert.Null(config.Current.Reefs);
            Assert.Null(((GridField)world.Matter).Reefs);
        }

        [Fact]
        public void AWorldWithReefsBuildsThemIntoEverySystem()
        {
            RunConfig config = ReefTank.Config();
            config.Current = new CurrentField { Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = ReefTank.Period, AdvectFields = true };
            var world = new World(config, seed: 3);

            Assert.NotNull(world.Reefs);
            Assert.Equal(3, world.Reefs.Count);
            Assert.Same(world.Reefs, world.Field.Reefs);
            Assert.Same(world.Reefs, config.Current.Reefs);
            Assert.Same(world.Reefs, ((GridField)world.Matter).Reefs);

            var again = new World(ReefTank.Config(), seed: 3);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(world.Reefs.CentreX(i), again.Reefs.CentreX(i));
                Assert.Equal(world.Reefs.CentreZ(i), again.Reefs.CentreZ(i));
            }
        }

        [Fact]
        public void AUniformConcentrationStaysUniformAroundTheReefs()
        {
            // Test 2, the beach's method: uniform water carried 600 s by the faded streams at the
            // campaign's mixing stays uniform to the rounding, the total holds, and so does every
            // cap column's.
            const int Steps = 1200;
            const float Dt = 0.5f;
            const float Mixing = 0.02f;

            ReefGeometry reefs = ReefTank.Reefs();
            GridField field = ReefTank.Grid(reefs);
            CurrentField current = ReefTank.Streams(reefs);

            field.SeedUniform(1f);
            double initial = field.Recount();

            var capStart = new Dictionary<(int, int), double>();
            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (field.ColumnIsLive(ix, iz) && field.LiveIntervalCount(ix, iz) == 2) capStart[(ix, iz)] = Column(field, ix, iz);
            }

            Assert.True(capStart.Count > 0);

            (int substeps, _, double discrete, double analytic, _, double worstNet) =
                field.MeasureFaceFluxes(current, 0d, Dt);

            for (int step = 1; step <= Steps; step++)
            {
                field.Mix(Dt, Mixing);
                field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
            }

            double min = double.MaxValue, max = double.MinValue;
            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz))
                {
                    Assert.Equal(0d, field.JoulesAt(ix, iy, iz));
                    continue;
                }

                double density = field.JoulesAt(ix, iy, iz) / field.CellVolume;
                min = Math.Min(min, density);
                max = Math.Max(max, density);
            }

            double worstColumn = 0d;
            foreach (var entry in capStart)
            {
                worstColumn = Math.Max(worstColumn, Math.Abs(Column(field, entry.Key.Item1, entry.Key.Item2) - entry.Value) / entry.Value);
            }

            double drift = (field.Recount() - initial) / initial;
            double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

            _output.WriteLine(
                $"{field.LiveCellCount} live cells, {capStart.Count} cap columns: density {min:R} to {max:R} after " +
                $"{Steps} steps (worst |c−1| {worst:0.0e+0}), total moved {drift:0.0e+0}, worst cap column moved " +
                $"{worstColumn:0.0e+0} of itself; {substeps} substeps, face speeds {discrete:0.0000} / {analytic:0.0000} m/s, " +
                $"worst net flux {worstNet:0.0e+0}");

            Assert.True(worst < 1e-9, $"the density ran {min:R} to {max:R}");
            Assert.True(Math.Abs(drift) < 1e-12, $"the total moved by {drift:0.0e+0}");
            Assert.True(worstColumn < 1e-9, $"a cap column's total moved by {worstColumn:0.0e+0}");
            Assert.Equal(0d, worstNet);
        }

        private static double Column(GridField field, int ix, int iz)
        {
            double sum = 0d;
            for (int iy = 0; iy < field.CellsY; iy++) sum += field.JoulesAt(ix, iy, iz);
            return sum;
        }

        [Fact]
        public void SnowSettlesOntoTheCapAboveItAndTheFloorBelowIt()
        {
            // Test 3: snow seeded over a cap ends on the cap's top cell; snow seeded under it ends
            // on the floor; nothing in rock; the total held.
            ReefGeometry reefs = ReefTank.Reefs();
            GridField field = ReefTank.Grid(reefs, sink: 0.5f);

            field.SeedUniform(1f);
            double initial = field.Recount();

            var above = new Dictionary<(int, int), double>();
            var below = new Dictionary<(int, int), double>();

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.ColumnIsLive(ix, iz) || field.LiveIntervalCount(ix, iz) != 2) continue;

                (int top0, int bottom0) = field.LiveInterval(ix, iz, 0);
                (int top1, int bottom1) = field.LiveInterval(ix, iz, 1);
                double a = 0d, b = 0d;
                for (int iy = top0; iy <= bottom0; iy++) a += field.JoulesAt(ix, iy, iz);
                for (int iy = top1; iy <= bottom1; iy++) b += field.JoulesAt(ix, iy, iz);
                above[(ix, iz)] = a;
                below[(ix, iz)] = b;
            }

            for (int step = 0; step < 1200; step++) field.Settle(0.5f);

            double rock = 0d, resting = 0d, floating = 0d, worstAbove = 0d, worstBelow = 0d;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            for (int iy = 0; iy < field.CellsY; iy++)
            {
                double stock = field.JoulesAt(ix, iy, iz);
                if (!field.IsLive(ix, iy, iz)) { rock += stock; continue; }
                if (field.RestingLayer(ix, iy, iz) == iy) resting += stock;
                else floating += stock;
            }

            foreach (var entry in above)
            {
                (int ix, int iz) = entry.Key;
                (_, int capTop) = field.LiveInterval(ix, iz, 0);
                (_, int floor) = field.LiveInterval(ix, iz, 1);

                worstAbove = Math.Max(worstAbove, Math.Abs(field.JoulesAt(ix, capTop, iz) - entry.Value) / entry.Value);
                worstBelow = Math.Max(worstBelow, Math.Abs(field.JoulesAt(ix, floor, iz) - below[entry.Key]) / below[entry.Key]);
            }

            _output.WriteLine(
                $"after 600 s: {resting / initial:0.0000%} resting, {floating / initial:0.0000%} still falling, {rock} J " +
                $"in the rock; over {above.Count} cap columns the cap's top cell holds what fell above it to " +
                $"{worstAbove:0.0e+0} and the floor what fell below to {worstBelow:0.0e+0}; total moved " +
                $"{(field.Recount() - initial) / initial:0.0e+0}");

            Assert.Equal(0d, rock);
            Assert.True(resting > 0.99d * initial);
            Assert.True(worstAbove < 1e-2, $"the cap's table holds {worstAbove} off what fell on it");
            Assert.True(worstBelow < 1e-2, $"the floor under the cap holds {worstBelow} off what fell there");
            Assert.Equal(initial, field.Recount(), 6);
        }

        [Fact]
        public void ThePrecomputedFadeIsTheSameWaterToTheBit()
        {
            // The grid multiplies the hoisted potential by a per-edge fade; the direct path takes
            // the faded potential from the current. Face for face, every bit.
            ReefGeometry reefs = ReefTank.Reefs();
            CurrentField current = ReefTank.Streams(reefs);

            GridField precomputed = ReefTank.Grid(reefs);
            GridField direct = ReefTank.Grid(reefs);
            direct.PrecomputeStreamsTerms = false;

            int carried = 0;

            foreach (double seconds in new[] { 0d, 137.5d, 4321.25d })
            {
                var fast = precomputed.MeasureFaceFluxes(current, seconds, 0.5f);
                var slow = direct.MeasureFaceFluxes(current, seconds, 0.5f);

                Assert.Equal(slow.Substeps, fast.Substeps);
                Assert.Equal(BitConverter.DoubleToInt64Bits(slow.DiscreteRms), BitConverter.DoubleToInt64Bits(fast.DiscreteRms));
                Assert.Equal(BitConverter.DoubleToInt64Bits(slow.LargestOutflow), BitConverter.DoubleToInt64Bits(fast.LargestOutflow));

                (double[] fe, double[] fl, double[] ff) = precomputed.FaceFluxesForReading();
                (double[] se, double[] sl, double[] sf) = direct.FaceFluxesForReading();

                carried += Same(se, fe) + Same(sl, fl) + Same(sf, ff);
            }

            Assert.True(carried > 1000, $"only {carried} faces carried anything");
            _output.WriteLine($"{carried} carrying faces identical over three clocks");
        }

        [Fact]
        public void TheRockShutsFaces()
        {
            // The faces beside the rock are shut by the mask (the uniform test is what shows that
            // nothing crosses them); here the open faces are counted with and without the rock.
            ReefGeometry reefs = ReefTank.Reefs();
            GridField field = ReefTank.Grid(reefs);
            GridField plain = ReefTank.Grid(null);

            int open = field.MeasureFaceFluxes(ReefTank.Streams(reefs), 1234.5d, 0.5f).OpenFaces;
            int plainOpen = plain.MeasureFaceFluxes(ReefTank.Streams(null), 1234.5d, 0.5f).OpenFaces;

            _output.WriteLine($"{open} open faces with the rock, {plainOpen} without");
            Assert.True(open < plainOpen);
        }

        private static int Same(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            int carried = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]));
                if (expected[i] != 0d) carried++;
            }

            return carried;
        }
    }
}
