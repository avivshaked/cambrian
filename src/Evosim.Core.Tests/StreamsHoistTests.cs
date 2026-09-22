using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D105's hoist: the streams' potential split into what a lattice point's column decides,
    /// what its depth decides and what the clock decides, and the three bought once each instead
    /// of once per sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A tolerance would pass the fault this exists to catch.</b> The saving is a
    /// reassociation risk and nothing else: <c>a·b·c·d</c> is <c>((a·b)·c)·d</c> in IEEE
    /// arithmetic and a prefix may be lifted out while a suffix may not, so a hoist that lifts
    /// the wrong factor gives an answer that is right to fifteen figures and a different double.
    /// A different double in a face flux is a different realisation of every seed — CLAUDE.md's
    /// butterfly rule — so everything here is compared bit for bit, as
    /// <c>BedGridTests.ThePrecomputedBedIsTheSameWaterToTheBit</c> compares D092's.
    /// </para>
    /// <para>
    /// <b>Both floors.</b> On a flat one the depth phase is a function of the height alone and one
    /// row of depth terms serves the tank; on a shaped one the map reads each column at its own
    /// <c>ŷ = y·D/d</c> and the table is two-dimensional. They are different code and both are
    /// asked.
    /// </para>
    /// </remarks>
    public class StreamsHoistTests
    {
        private readonly ITestOutputHelper _output;

        public StreamsHoistTests(ITestOutputHelper output) => _output = output;

        private const float Depth = 60f;
        private const int Patches = 4;
        private const float Period = 6000f;
        private const ulong RunSeed = 20260915UL;

        private static float RadiusOf(float area) => TankGeometry.RadiusFor(area);

        private static BedShape Bed(float area, float tilt = 3f) =>
            new BedShape(
                RadiusOf(area), Depth, 12f, tilt, 2f * RadiusOf(area),
                Rng.SeedFor(RunSeed, World.BedShapeIndex));

        private static GridField Grid(float area, float cell, BedShape bed) =>
            new GridField(
                area, 0f, Depth, 0f, 0f, Patches, cell,
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

        private static void SameBits(double expected, double actual, string what)
        {
            if (BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual))
            {
                return;
            }

            Assert.Fail(
                $"{what}: the direct path gave {expected:R} and the hoisted one {actual:R} — " +
                "same to the eye and not to the bit.");
        }

        private static void SameBits(Float3 expected, Float3 actual, string what)
        {
            SameBits(expected.X, actual.X, what + " x");
            SameBits(expected.Y, actual.Y, what + " y");
            SameBits(expected.Z, actual.Z, what + " z");
        }

        /// <summary>
        /// The point sampler, flat floor and shaped, at several hundred places and three clocks.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheHoistedPotentialIsTheDirectOneToTheBit(bool shaped)
        {
            const float Area = 400f;

            BedShape bed = shaped ? Bed(Area) : null;
            CurrentField current = Streams(Area, bed);

            float radius = RadiusOf(Area);
            var rng = new Rng(Rng.SeedFor(RunSeed, 11UL));

            int carrying = 0, faces = 0;

            foreach (double seconds in new[] { 0d, 137.5d, 4321.25d })
            {
                current.PinInstant(seconds);

                try
                {
                    for (int i = 0; i < 400; i++)
                    {
                        // Uniform over the disc, and over the whole water column including the
                        // two faces — a hoist that got the clamp or the face case wrong would
                        // otherwise never be asked about it.
                        float r = (float)(Math.Sqrt(rng.Range(0f, 1f)) * radius);
                        float theta = rng.Range(0f, (float)(2.0 * Math.PI));

                        float x = (float)(radius + r * Math.Cos(theta));
                        float z = (float)(radius + r * Math.Sin(theta));
                        float y = -rng.Range(-1f, Depth + 1f);

                        CurrentField.StreamsColumn column = current.ColumnOf(x, z);

                        Float3 direct, hoisted;

                        if (shaped)
                        {
                            CurrentField.BedColumn floor = current.ColumnAt(x, z);

                            direct = current.PotentialAt(x, y, z, seconds, floor);
                            hoisted = current.PotentialAt(
                                column, floor, current.DepthOf(floor, y), y, seconds);
                        }
                        else
                        {
                            direct = current.PotentialAt(x, y, z, seconds);
                            hoisted = current.PotentialAt(column, current.DepthOf(y), seconds);
                        }

                        SameBits(direct, hoisted, $"potential at {x},{y},{z} t {seconds}");

                        if (direct.X != 0f || direct.Y != 0f || direct.Z != 0f) carrying++;
                        else faces++;
                    }
                }
                finally
                {
                    current.UnpinInstant();
                }
            }

            // A run of zeros agrees with anything, so the test says what it compared.
            Assert.True(carrying > 800, $"only {carrying} of 1200 samples carried anything");

            _output.WriteLine(
                (shaped ? "shaped" : "flat  ") +
                $" floor: {carrying} carrying samples identical, {faces} at a face or outside");
        }

        /// <summary>
        /// A default column or depth is refused rather than read as the axis at the waterline,
        /// and so is a sample taken without a pinned clock.
        /// </summary>
        [Fact]
        public void TheHoistedPotentialRefusesWhatItCannotAnswerFor()
        {
            const float Area = 400f;

            CurrentField current = Streams(Area, null);
            float radius = RadiusOf(Area);

            CurrentField.StreamsColumn column = current.ColumnOf(radius * 1.3f, radius);
            CurrentField.StreamsDepth depth = current.DepthOf(-20d);

            Assert.True(column.Sampled);
            Assert.True(depth.Sampled);

            // No pin.
            Assert.Throws<InvalidOperationException>(
                () => current.PotentialAt(column, depth, 0d));

            current.PinInstant(0d);

            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => current.PotentialAt(default, depth, 0d));
                Assert.Throws<InvalidOperationException>(
                    () => current.PotentialAt(column, default, 0d));

                // A flat floor handed a bed column, which is a caller precomputing against
                // another world.
                Assert.Throws<InvalidOperationException>(
                    () => current.PotentialAt(column, default, depth, -20f, 0d));

                // And the wrong clock.
                Assert.Throws<InvalidOperationException>(
                    () => current.PotentialAt(column, depth, 500d));
            }
            finally
            {
                current.UnpinInstant();
            }
        }

        /// <summary>
        /// The grid's own face fluxes and its carried stock, with the hoist on and off.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThePrecomputedStreamsAreTheSameWaterToTheBit(bool shaped)
        {
            const float Area = 100f;
            const float Cell = 1f;

            BedShape bed = shaped ? Bed(Area) : null;
            CurrentField current = Streams(Area, bed);

            GridField precomputed = Grid(Area, Cell, bed);
            GridField direct = Grid(Area, Cell, bed);

            Assert.True(precomputed.PrecomputeStreamsTerms);
            direct.PrecomputeStreamsTerms = false;

            foreach (double seconds in new[] { 0d, 137.5d, 4321.25d })
            {
                var fast = precomputed.MeasureFaceFluxes(current, seconds, 0.5f);
                var slow = direct.MeasureFaceFluxes(current, seconds, 0.5f);

                Assert.Equal(slow.Substeps, fast.Substeps);
                Assert.Equal(slow.OpenFaces, fast.OpenFaces);
                SameBits(slow.LargestOutflow, fast.LargestOutflow, "largest outflow");
                SameBits(slow.DiscreteRms, fast.DiscreteRms, "discrete rms");
                SameBits(slow.AnalyticRms, fast.AnalyticRms, "analytic rms");
                SameBits(slow.WorstNetFlux, fast.WorstNetFlux, "worst net flux");

                (double[] fastEast, double[] fastLower, double[] fastFront) =
                    precomputed.FaceFluxesForReading();
                (double[] slowEast, double[] slowLower, double[] slowFront) =
                    direct.FaceFluxesForReading();

                int carried = 0;
                carried += SameFaces(slowEast, fastEast, "east");
                carried += SameFaces(slowLower, fastLower, "lower");
                carried += SameFaces(slowFront, fastFront, "front");

                Assert.True(carried > 2000, $"only {carried} faces carried anything at {seconds} s");

                _output.WriteLine(
                    (shaped ? "shaped" : "flat  ") +
                    $" floor, t {seconds,9:0.00} s: {carried} carrying faces identical");
            }

            // And the step itself, substep loop and all.
            GridField movedFast = Grid(Area, Cell, bed);
            GridField movedSlow = Grid(Area, Cell, bed);
            movedSlow.PrecomputeStreamsTerms = false;

            movedFast.SeedUniform(1f);
            movedSlow.SeedUniform(1f);

            float patchWidth = (float)Math.Sqrt(Area / Patches);

            for (int step = 0; step < 4; step++)
            {
                movedFast.Advect(current, 0.5d * step, 0.5f, patchWidth);
                movedSlow.Advect(current, 0.5d * step, 0.5f, patchWidth);
            }

            int cells = 0;

            for (int ix = 0; ix < movedFast.CellsX; ix++)
            for (int iy = 0; iy < movedFast.CellsY; iy++)
            for (int iz = 0; iz < movedFast.CellsZ; iz++)
            {
                if (!movedFast.IsLive(ix, iy, iz)) continue;

                SameBits(
                    movedSlow.JoulesAt(ix, iy, iz), movedFast.JoulesAt(ix, iy, iz),
                    $"cell {ix},{iy},{iz}");

                cells++;
            }

            Assert.True(cells > 4000, $"only {cells} live cells carried the step");

            _output.WriteLine(
                (shaped ? "shaped" : "flat  ") + $" floor: {cells} live cells identical after 4 steps");
        }

        private static int SameFaces(double[] slow, double[] fast, string what)
        {
            Assert.Equal(slow.Length, fast.Length);

            int carried = 0;

            for (int i = 0; i < slow.Length; i++)
            {
                SameBits(slow[i], fast[i], $"{what} face {i}");
                if (slow[i] != 0d) carried++;
            }

            return carried;
        }
    }
}
