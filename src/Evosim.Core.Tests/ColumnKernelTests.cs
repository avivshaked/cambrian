using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The column kernel (<see cref="GridField.SampleByColumn"/>): the tank's edges sampled a
    /// column at a time, with the eddy phases bought once per column, held against the per-edge
    /// hoisted path edge by edge and bit for bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Edges, and not only faces.</b> A face is the sum of four edges, so two paths could agree
    /// in every face and still differ in an edge whose difference cancelled. The kernel's claim is
    /// per edge, so every edge is compared, as raw bits: a tolerance would pass a reassociated
    /// product, and a reassociated product is a different realisation of every seed.
    /// </para>
    /// <para>
    /// <b>Four floors, because the kernel has four branches.</b> A flat floor, where the x and z
    /// edges skip the eddy sum; a shaped one, where every component reads it through the map; a
    /// beach, where the shore's fade is computed per column and the shoal's still water is exact
    /// zeros; and reefs, whose fades the grid applies after the kernel.
    /// </para>
    /// </remarks>
    // Parallelism.Threads is a static: every test class that sets it shares this collection, so xUnit runs them one at a time.
    [Collection("Parallelism.Threads")]
    public class ColumnKernelTests
    {
        private readonly ITestOutputHelper _output;

        public ColumnKernelTests(ITestOutputHelper output) => _output = output;

        private const int Patches = 4;
        private const float Period = 6000f;
        private const ulong RunSeed = 20261002UL;

        // The open tank: StreamsHoistTests' 400 m² and 60 m.
        private const float Area = 400f;
        private const float Depth = 60f;

        // The beach: BedBeachTests' small one, 6 m deep, a shoal a metre down and a 3 m fade.
        private const float BeachDepth = 6f;
        private const float Shore = 1f;
        private const float ShoreFade = 3f;

        private static float RadiusOf(float area) => TankGeometry.RadiusFor(area);

        /// <summary>A grid factory and the water it is carried by, for one floor.</summary>
        private static (Func<GridField> Grid, CurrentField Water) Floor(string floor)
        {
            switch (floor)
            {
                case "flat":
                    return (() => TankGrid(Area, Depth, null), Water(Area, Depth, null));

                case "shaped":
                {
                    var bed = new BedShape(
                        RadiusOf(Area), Depth, 12f, 3f, 2f * RadiusOf(Area),
                        Rng.SeedFor(RunSeed, World.BedShapeIndex));
                    Assert.True(bed.HasRelief);
                    return (() => TankGrid(Area, Depth, bed), Water(Area, Depth, bed));
                }

                case "shore":
                {
                    var bed = new BedShape(
                        RadiusOf(Area), BeachDepth, 0.5f, 12f, 0f,
                        Rng.SeedFor(RunSeed, World.BedShapeIndex), Shore, ShoreFade);
                    Assert.True(bed.HasShore);
                    return (() => TankGrid(Area, BeachDepth, bed), Water(Area, BeachDepth, bed));
                }

                case "reefs":
                {
                    ReefGeometry reefs = ReefTank.Reefs();
                    return (() => ReefTank.Grid(reefs), ReefTank.Streams(reefs));
                }

                default:
                    throw new ArgumentException(floor, nameof(floor));
            }
        }

        private static GridField TankGrid(float area, float depth, BedShape bed) =>
            new GridField(
                area, 0f, depth, 0f, 0f, Patches, 1f,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: RadiusOf(area),
                bed: bed);

        private static CurrentField Water(float area, float depth, BedShape bed)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = 0.1f,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(area / Patches), Patches, depth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: RadiusOf(area),
                bed: bed);

            return field;
        }

        private static void SameBits(double expected, double actual, string what)
        {
            if (BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual))
            {
                return;
            }

            Assert.Fail(
                $"{what}: the per-edge path gave {expected:R} and the column kernel {actual:R}.");
        }

        /// <summary>Every entry of two arrays to the bit; returns how many were non-zero.</summary>
        private static int SameArray(double[] expected, double[] actual, string what)
        {
            Assert.Equal(expected.Length, actual.Length);

            int carrying = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                {
                    Assert.Fail(
                        $"{what} {i}: the per-edge path gave {expected[i]:R} and the column " +
                        $"kernel {actual[i]:R}.");
                }

                if (expected[i] != 0d) carrying++;
            }

            return carrying;
        }

        /// <summary>Every edge of the three lattices; returns the carrying count of each.</summary>
        private static (int X, int Y, int Z) SameEdges(GridField edge, GridField kernel, string when)
        {
            (double[] ex, double[] ey, double[] ez) = edge.EdgesForReading();
            (double[] kx, double[] ky, double[] kz) = kernel.EdgesForReading();

            return (
                SameArray(ex, kx, $"x edge at {when}"),
                SameArray(ey, ky, $"y edge at {when}"),
                SameArray(ez, kz, $"z edge at {when}"));
        }

        private static int SameFaces(GridField edge, GridField kernel, string when)
        {
            (double[] eEast, double[] eLower, double[] eFront) = edge.FaceFluxesForReading();
            (double[] kEast, double[] kLower, double[] kFront) = kernel.FaceFluxesForReading();

            return SameArray(eEast, kEast, $"east face at {when}") +
                   SameArray(eLower, kLower, $"lower face at {when}") +
                   SameArray(eFront, kFront, $"front face at {when}");
        }

        /// <summary>
        /// One pass of edges at each of four clocks, with nothing moved: every edge and every face
        /// flux, and the readings computed from them.
        /// </summary>
        [Theory]
        [InlineData("flat")]
        [InlineData("shaped")]
        [InlineData("shore")]
        [InlineData("reefs")]
        public void TheColumnKernelSamplesEveryEdgeToTheBit(string floor)
        {
            (Func<GridField> make, CurrentField water) = Floor(floor);

            GridField kernel = make();
            GridField edge = make();

            Assert.True(kernel.SampleByColumn);
            edge.SampleByColumn = false;

            foreach (double seconds in new[] { 0d, 137.5d, 4321.25d, 59999.75d })
            {
                var fast = kernel.MeasureFaceFluxes(water, seconds, 0.5f);
                var slow = edge.MeasureFaceFluxes(water, seconds, 0.5f);

                Assert.Equal(slow.Substeps, fast.Substeps);
                Assert.Equal(slow.OpenFaces, fast.OpenFaces);
                SameBits(slow.LargestOutflow, fast.LargestOutflow, $"largest outflow at {seconds} s");
                SameBits(slow.DiscreteRms, fast.DiscreteRms, $"discrete rms at {seconds} s");
                SameBits(slow.WorstNetFlux, fast.WorstNetFlux, $"worst net flux at {seconds} s");

                (int x, int y, int z) = SameEdges(edge, kernel, $"{seconds} s");
                int faces = SameFaces(edge, kernel, $"{seconds} s");

                // A run of zeros agrees with anything, so each lattice has to have carried water.
                // On a flat floor the x and z edges are the overturning alone, so this is also
                // what shows that the kernel's shortcut there read the overturning.
                Assert.True(x > 500 && y > 500 && z > 500, $"{floor} at {seconds} s carried x {x}, y {y}, z {z}");
                Assert.True(faces > 1000, $"{floor} at {seconds} s carried {faces} faces");

                _output.WriteLine(
                    $"{floor,-6} t {seconds,9:0.00} s: edges x {x}, y {y}, z {z} and {faces} faces identical");
            }

            // The kernel ran, once a pass, and the per-edge grid never took it.
            Assert.Equal(4L, kernel.ColumnPasses);
            Assert.Equal(0L, edge.ColumnPasses);
        }

        /// <summary>
        /// Three hundred steps of a stirred stock carried both ways, the kernel's on two threads
        /// and the per-edge path's on one: the edges at seven instants along the way and every
        /// cell's stock at the end.
        /// </summary>
        [Theory]
        [InlineData("flat")]
        [InlineData("shaped")]
        [InlineData("shore")]
        [InlineData("reefs")]
        public void TheColumnKernelCarriesTheSameStockToTheBit(string floor)
        {
            const int Steps = 300;
            const float Dt = 0.5f;
            const double Start = 1234.5d;

            (Func<GridField> make, CurrentField water) = Floor(floor);

            GridField kernel = make();
            GridField edge = make();
            edge.SampleByColumn = false;

            // A stock that is not uniform, since a uniform one is the case the scheme is built to
            // leave alone: a floor of 1 J/m³ and a scatter of deposits in about one live cell in
            // seven, the same into both grids.
            kernel.SeedUniform(1f);
            edge.SeedUniform(1f);

            var rng = new Rng(Rng.SeedFor(RunSeed, 7UL));
            float h = kernel.CellMetres;
            int stirred = 0;

            for (int ix = 0; ix < kernel.CellsX; ix++)
            for (int iy = 0; iy < kernel.CellsY; iy++)
            for (int iz = 0; iz < kernel.CellsZ; iz++)
            {
                if (!kernel.IsLive(ix, iy, iz)) continue;
                if (rng.NextFloat() >= 1f / 7f) continue;

                double amount = 1d + 49d * rng.NextFloat();
                var at = new FieldPoint(
                    new Float3((ix + 0.5f) * h, -(iy + 0.5f) * h, (iz + 0.5f) * h), 0);

                kernel.DepositExact(at, amount);
                edge.DepositExact(at, amount);
                stirred++;
            }

            Assert.True(stirred > 100, $"only {stirred} cells were stirred");

            int was = Parallelism.Threads;
            int instants = 0;

            try
            {
                for (int step = 0; step < Steps; step++)
                {
                    double seconds = Start + step * (double)Dt;

                    // The kernel splits its columns across two threads; the per-edge path runs on
                    // one. The answer is one expression per edge either way.
                    Parallelism.Threads = 2;
                    kernel.Advect(water, seconds, Dt, kernel.PatchWidthMetres);

                    Parallelism.Threads = 1;
                    edge.Advect(water, seconds, Dt, edge.PatchWidthMetres);

                    if (step % 50 == 0 || step == Steps - 1)
                    {
                        (int x, int y, int z) = SameEdges(edge, kernel, $"step {step}");
                        Assert.True(x > 500 && y > 500 && z > 500, $"{floor} at step {step} carried x {x}, y {y}, z {z}");
                        instants++;
                    }
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }

            Assert.Equal(7, instants);
            Assert.True(kernel.ColumnPasses >= Steps, $"the kernel took {kernel.ColumnPasses} passes in {Steps} steps");
            Assert.Equal(0L, edge.ColumnPasses);

            int cells = 0;
            double least = double.MaxValue, most = 0d;

            for (int ix = 0; ix < kernel.CellsX; ix++)
            for (int iy = 0; iy < kernel.CellsY; iy++)
            for (int iz = 0; iz < kernel.CellsZ; iz++)
            {
                if (!kernel.IsLive(ix, iy, iz)) continue;

                double joules = kernel.JoulesAt(ix, iy, iz);
                SameBits(edge.JoulesAt(ix, iy, iz), joules, $"{floor} cell {ix},{iy},{iz} after {Steps} steps");

                least = Math.Min(least, joules);
                most = Math.Max(most, joules);
                cells++;
            }

            // The stock is still far from uniform, so the comparison was of a field with
            // gradients in it and not of the case the scheme leaves alone.
            Assert.True(most > 4d * least, $"the stock spans only {least:R} to {most:R} J");

            _output.WriteLine(
                $"{floor,-6}: {cells} live cells identical after {Steps} steps, edges identical at {instants} instants, " +
                $"{kernel.ColumnPasses} kernel passes");
        }
    }
}
