using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The sea floor's height map — D092, <c>logbook/specs/bed-spec.md</c> items 1 to 5a and 12.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured on a lattice of this test's own</b>, finer than the half-metre one the map is
    /// fitted on and offset from it, so that "mean zero over the disc" and "the range is the dial"
    /// are second opinions rather than restatements of the construction's own arithmetic.
    /// </para>
    /// <para>
    /// <b>Read at the round's own geometry.</b> 400 m² is a disc of radius 11.28 m and 60 m deep,
    /// which is round 38's tank; the relief and the tilt are read at the values the round is
    /// pencilled in at, 4 m and 10 m, so the numbers in this output are the numbers the round will
    /// run with rather than a tidy demonstration.
    /// </para>
    /// </remarks>
    public class BedShapeTests
    {
        private readonly ITestOutputHelper _output;

        public BedShapeTests(ITestOutputHelper output) => _output = output;

        private const float Area = 400f;
        private const float Depth = 60f;
        private const float Relief = 4f;
        private const float Tilt = 10f;

        private static float Radius => TankGeometry.RadiusFor(Area);

        private static BedShape Bed(
            float relief = Relief, float tilt = Tilt, ulong seed = 1UL, float scale = 0f) =>
            new BedShape(Radius, Depth, relief, tilt, scale, Rng.SeedFor(seed, World.BedShapeIndex));

        /// <summary>
        /// Walks a lattice of this test's own over the disc and returns the map's mean, its range
        /// and its steepest slope.
        /// </summary>
        private static (double Mean, double Low, double High, double Steepest, int Columns)
            Measure(BedShape bed, double step = 0.17d)
        {
            double radius = bed.RadiusMetres;
            int side = (int)Math.Ceiling(2d * radius / step);

            double sum = 0d, low = double.MaxValue, high = double.MinValue, steepest = 0d;
            int columns = 0;

            for (int ix = 0; ix < side; ix++)
            {
                double x = (ix + 0.31d) * step;

                for (int iz = 0; iz < side; iz++)
                {
                    double z = (iz + 0.71d) * step;
                    if (!TankGeometry.Inside(x, z, radius)) continue;

                    bed.HeightAndGradient(x, z, out double h, out double gx, out double gz);

                    sum += h;
                    if (h < low) low = h;
                    if (h > high) high = h;

                    double slope = Math.Sqrt(gx * gx + gz * gz);
                    if (slope > steepest) steepest = slope;

                    columns++;
                }
            }

            return (sum / columns, low, high, steepest, columns);
        }

        [Fact]
        public void TheMapIsMeanZeroOverTheDisc()
        {
            // Spec item 5: the water volume and the seeded matter density stay round 38's, which
            // is a statement about this number and nothing else. The construction subtracts the
            // mean it measures on its own half-metre lattice, so what is asserted here is that
            // the subtraction survives being read on a different one — a map whose mean depended
            // on the lattice would move the world's volume every time the cell size changed.
            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
            {
                BedShape bed = Bed(seed: seed);
                (double mean, double low, double high, _, int columns) = Measure(bed);

                _output.WriteLine(
                    $"seed {seed}: mean {mean:0.0000} m over {columns} columns, " +
                    $"map {low:0.000} to {high:0.000} m, " +
                    $"mean/range {Math.Abs(mean) / (high - low):0.000%}");

                Assert.True(
                    Math.Abs(mean) < 0.02d * (high - low),
                    $"seed {seed}: the mean is {mean:0.0000} m on a range of {high - low:0.000} m");
            }
        }

        [Fact]
        public void TheRangeIsTheDialUnlessTheSlopeBoundBinds()
        {
            // Spec item 3's first clause. The dial is the range of the cosine bands over the disc,
            // and the tilt is not in it: two dials that shared one range would each be a readout
            // of the other.
            _output.WriteLine("seed  dial  bands' range  whole map  steepest  binds  hollows ridges");

            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
            {
                BedShape bed = Bed(seed: seed);
                (_, double low, double high, double steepest, _) = Measure(bed);

                _output.WriteLine(
                    $"{seed,4}  {Relief,4}  {bed.RangeMetres,12:0.000}  {high - low,9:0.000}  " +
                    $"{Math.Atan(steepest) * 180d / Math.PI,7:0.0}°  {bed.SlopeBoundBinds,5}  " +
                    $"{bed.Hollows,7} {bed.Ridges,6}");

                if (!bed.SlopeBoundBinds)
                {
                    Assert.Equal(Relief, bed.RangeMetres, 3);
                }
                else
                {
                    Assert.True(
                        bed.RangeMetres < Relief,
                        "the bound bound and yet the range is the whole dial");
                }
            }
        }

        [Fact]
        public void NoBandSlopeIsSteeperThanThirtyDegreesAndTheTiltIsItsOwnRamp()
        {
            // Spec item 3 on the bands alone, and item 5a's tilt as a ramp of its own (the owner's
            // ruling of 2026-09-15): the two are allowed to add, and the total is a reading. The
            // whole map is measured on this test's own lattice, which is finer than the fit's, so
            // the total the class reports has to agree with what a finer lattice finds, and the
            // bands' own bound has to hold at the class's reading.
            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
            foreach ((float relief, float tilt) in new[] { (4f, 10f), (12f, 0f), (4f, 2f), (4f, 6f) })
            {
                BedShape bed = Bed(relief, tilt, seed);
                (_, _, _, double steepest, _) = Measure(bed);

                double total = Math.Atan(steepest) * 180d / Math.PI;
                double bands = bed.SteepestSlopeRadians * 180d / Math.PI;
                double reported = bed.SteepestTotalSlopeRadians * 180d / Math.PI;
                double tiltAlone = Math.Atan(tilt / (2d * Radius)) * 180d / Math.PI;

                _output.WriteLine(
                    $"seed {seed} relief {relief} tilt {tilt}: bands {bands:0.0}°, total {reported:0.0}° " +
                    $"reported and {total:0.0}° on the fine lattice (the tilt alone is {tiltAlone:0.0}°), " +
                    $"range {bed.RangeMetres:0.000} m, bound {(bed.SlopeBoundBinds ? "binds" : "clear")}");

                // The bands' bound, at the class's own reading and with the half degree a finer
                // lattice can add.
                Assert.True(bands < 30.5d, $"seed {seed} at relief {relief}: the bands read {bands:0.0}°");

                // The total is at most the two added (gradients add as vectors, so it is usually
                // less), and the fine lattice agrees with the reported total to a reading's
                // resolution: the class reads its steepest on the half-metre column lattice and
                // the third band's wavelength is under a metre, so a lattice three times finer
                // can find up to a tenth more tangent at the steepest column (seed 5 at tilt 10
                // read 41.6° here against 39.6° reported, a 7% tangent). The bands' own bound
                // holds 5% under for the same reason (SlopeFitMargin) and the assertion above it
                // allows the half degree that leaves.
                double added = Math.Atan(Math.Tan(bands * Math.PI / 180d) + tilt / (2d * Radius)) * 180d / Math.PI;
                Assert.True(reported <= added + 0.01d, $"seed {seed}: the total {reported:0.0}° is more than the bands and the ramp added");
                Assert.True(total >= reported - 0.5d, $"seed {seed}: fine lattice {total:0.0}° under reported {reported:0.0}°");
                Assert.True(Math.Tan(total * Math.PI / 180d) <= 1.1d * Math.Tan(reported * Math.PI / 180d),
                    $"seed {seed}: fine lattice {total:0.0}° against reported {reported:0.0}°, more than a tenth in tangent");
            }

            // The tilt's own bound: a ramp over 25° is refused whatever the relief, and one just
            // under it is not. 30 m across a 22.6 m tank is 53°; 14 m is 31.8°; 13 m is 29.9°
            // (the cap moved from 25° to 30° on the evening of 2026-09-15, D093).
            ArgumentOutOfRangeException thrown =
                Assert.Throws<ArgumentOutOfRangeException>(() => Bed(4f, 30f));
            _output.WriteLine(thrown.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(4f, 14f));
            Assert.True(Bed(4f, 13f).HasRelief);
        }

        [Fact]
        public void TheTiltDoesNotEatTheRelief()
        {
            // The point of bounding the two apart: a tilt of 6 m (a 14.9° ramp on this tank)
            // leaves the bands' range and the hollow count where a tilt of 0 has them, within a
            // few percent on the range (the fit is the same fit; only the mean-zero shift and the
            // hollow rule's neighbourhood see the ramp) and within one hollow per seed on average.
            double range0 = 0d, range6 = 0d;
            int hollows0 = 0, hollows6 = 0;

            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
            {
                BedShape flat = Bed(12f, 0f, seed);
                BedShape tilted = Bed(12f, 6f, seed);
                range0 += flat.RangeMetres / 5d;
                range6 += tilted.RangeMetres / 5d;
                hollows0 += flat.Hollows;
                hollows6 += tilted.Hollows;

                _output.WriteLine(
                    $"seed {seed}: tilt 0 range {flat.RangeMetres:0.000} m, {flat.Hollows} hollows; " +
                    $"tilt 6 range {tilted.RangeMetres:0.000} m, {tilted.Hollows} hollows, " +
                    $"total slope {tilted.SteepestTotalSlopeRadians * 180d / Math.PI:0.0}°");
            }

            Assert.Equal(range0, range6, range0 * 0.05d);
            Assert.True(Math.Abs(hollows0 - hollows6) <= 5, $"hollows {hollows0} against {hollows6} over five seeds");
        }

        /// <summary>
        /// What the 30° bound leaves for the relief, at this footprint — the number the round's
        /// dials have to be chosen from.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Spec item 3 caps the relief at this footprint and this is the table that says by
        /// how much.</b> A sum of cosines with a range <c>R</c> over a largest wavelength <c>λ</c>
        /// has a steepest slope of about <c>4R/λ</c> — the maximum over fourteen thousand
        /// columns, not the RMS — so at the default scale of a third of a 22.6 m tank the 30°
        /// bound caps the relief near a metre. The tilt no longer eats that budget (it is a ramp
        /// bounded on its own, the owner's ruling of 2026-09-15), which the table shows by the
        /// range and the hollow count holding down each column. Nothing here is asserted beyond
        /// the fit doing what it says; the table is the reading, and the round's dials are the
        /// owner's.
        /// </para>
        /// </remarks>
        [Fact]
        public void WhatTheSlopeBoundLeavesForTheRelief()
        {
            _output.WriteLine(
                $"a tank of {2d * Radius:0.00} m across, {Depth} m deep; the dial is 12 m so the " +
                "bound always decides");
            _output.WriteLine("scale m  tilt m  tilt°   range m  bands°  total°  hollows  ridges");

            foreach (float scale in new[] { 7.52f, 11.28f, 15f, 22.57f })
            foreach (float tilt in new[] { 0f, 2f, 6f, 10f })
            {
                int hollows = 0, ridges = 0;
                double range = 0d, steepest = 0d, total = 0d;

                foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
                {
                    BedShape bed = Bed(12f, tilt, seed, scale);
                    hollows += bed.Hollows;
                    ridges += bed.Ridges;
                    range += bed.RangeMetres / 5d;
                    steepest += bed.SteepestSlopeRadians * 180d / Math.PI / 5d;
                    total += bed.SteepestTotalSlopeRadians * 180d / Math.PI / 5d;
                }

                _output.WriteLine(
                    $"{scale,7:0.00}  {tilt,6}  {Math.Atan(tilt / (2d * Radius)) * 180d / Math.PI,5:0.0}  " +
                    $"{range,7:0.000}  {steepest,6:0.0}  {total,6:0.0}  {hollows / 5d,7:0.0}  {ridges / 5d,6:0.0}");
            }
        }

        [Fact]
        public void ReliefZeroAndTiltZeroIsExactlyFlat()
        {
            // Spec item 12, and the reason it is an equality rather than a tolerance: every
            // recorded world has to replay to the bit, and a floor that was flat to 1e-9 would be
            // a different world from a flat floor in exactly the way PhysX makes visible.
            BedShape bed = Bed(relief: 0f, tilt: 0f);

            Assert.False(bed.HasRelief);

            var rng = new Rng(29UL);

            for (int i = 0; i < 200; i++)
            {
                double x = rng.NextFloat() * 2d * Radius;
                double z = rng.NextFloat() * 2d * Radius;

                Assert.Equal(0d, bed.Height(x, z));
                Assert.Equal(-(double)Depth, bed.FloorY(x, z));

                (double gx, double gz) = bed.Gradient(x, z);
                Assert.Equal(0d, gx);
                Assert.Equal(0d, gz);

                (double xx, double xz, double zz) = bed.Hessian(x, z);
                Assert.Equal(0d, xx);
                Assert.Equal(0d, xz);
                Assert.Equal(0d, zz);
            }
        }

        [Fact]
        public void OneSeedReplaysAndTwoSeedsAreTwoFloors()
        {
            // Spec item 1's last clause. Five seeds of a round have to be five draws of the floor
            // as well as of the genome, or they are not replicates (CurrentField.SetBox's remarks
            // make the same point about the water).
            BedShape first = Bed(seed: 7UL);
            BedShape again = Bed(seed: 7UL);
            BedShape other = Bed(seed: 8UL);

            var rng = new Rng(31UL);
            double largestDifference = 0d;

            for (int i = 0; i < 500; i++)
            {
                double x = rng.NextFloat() * 2d * Radius;
                double z = rng.NextFloat() * 2d * Radius;

                Assert.Equal(first.Height(x, z), again.Height(x, z));

                largestDifference = Math.Max(
                    largestDifference, Math.Abs(first.Height(x, z) - other.Height(x, z)));
            }

            _output.WriteLine(
                $"seed 7 replays to the bit; seed 8 differs by up to {largestDifference:0.000} m " +
                $"on a range of {first.RangeMetres:0.000} m");

            Assert.True(
                largestDifference > 0.25d * first.RangeMetres,
                $"two seeds differ by only {largestDifference:0.000} m");
        }

        [Fact]
        public void TheGradientsAreTheMapsOwn()
        {
            // A second derivation of a function is the kind of code that is wrong quietly, and
            // every one of the water's terms is built on these: the map's Jacobian is the
            // gradient and the velocity's Jacobian is the Hessian.
            BedShape bed = Bed();

            // A central difference's truncation error is h² times the third derivative over 6,
            // and the bands' third derivative grew when the tilt stopped eating their budget (the
            // bound is on the bands alone now, so at this relief they take the whole 30°): at
            // h = 0.01 the worst gradient error read 1.08e-4 against a 1e-4 tolerance. Halving h
            // quarters it and leaves the rounding error (1e-16 · 60 m / h) three orders under.
            var rng = new Rng(37UL);
            const double H = 0.005d;
            double worstGradient = 0d, worstHessian = 0d;

            for (int i = 0; i < 300; i++)
            {
                double x = Radius + (rng.NextFloat() - 0.5d) * Radius;
                double z = Radius + (rng.NextFloat() - 0.5d) * Radius;

                bed.HeightGradientAndHessian(
                    x, z, out _, out double gx, out double gz,
                    out double xx, out double xz, out double zz);

                double dx = (bed.Height(x + H, z) - bed.Height(x - H, z)) / (2d * H);
                double dz = (bed.Height(x, z + H) - bed.Height(x, z - H)) / (2d * H);

                worstGradient = Math.Max(
                    worstGradient, Math.Max(Math.Abs(dx - gx), Math.Abs(dz - gz)));

                (double gxx, double gxz) = Numeric(bed, x, z, H);
                (double gzx, double gzz) = NumericZ(bed, x, z, H);

                worstHessian = Math.Max(
                    worstHessian,
                    Math.Max(
                        Math.Abs(gxx - xx),
                        Math.Max(Math.Abs(0.5d * (gxz + gzx) - xz), Math.Abs(gzz - zz))));
            }

            _output.WriteLine(
                $"worst gradient error {worstGradient:0.0000e+0} against slopes of order " +
                $"{Math.Tan(bed.SteepestSlopeRadians):0.000}; worst Hessian error " +
                $"{worstHessian:0.0000e+0}");

            Assert.True(worstGradient < 1e-4d, $"gradient error {worstGradient:0.0000e+0}");
            Assert.True(worstHessian < 1e-3d, $"Hessian error {worstHessian:0.0000e+0}");
        }

        private static (double XX, double XZ) Numeric(BedShape bed, double x, double z, double h)
        {
            bed.HeightAndGradient(x + h, z, out _, out double ax, out double az);
            bed.HeightAndGradient(x - h, z, out _, out double bx, out double bz);
            return ((ax - bx) / (2d * h), (az - bz) / (2d * h));
        }

        private static (double ZX, double ZZ) NumericZ(BedShape bed, double x, double z, double h)
        {
            bed.HeightAndGradient(x, z + h, out _, out double ax, out double az);
            bed.HeightAndGradient(x, z - h, out _, out double bx, out double bz);
            return ((ax - bx) / (2d * h), (az - bz) / (2d * h));
        }

        [Fact]
        public void TheShallowArcIsWhereTheTiltSays()
        {
            // Spec item 5a: a tilt along one diameter, with the shallow arc a shore. Read by
            // averaging the map over the half of the disc the tilt points into against the other
            // half, so that the bands average out and the plane does not.
            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL })
            {
                BedShape bed = Bed(relief: 0f, tilt: Tilt, seed: seed);

                double towards = 0d, away = 0d;
                int n = 0, m = 0;

                double cos = Math.Cos(bed.TiltDirectionRadians);
                double sin = Math.Sin(bed.TiltDirectionRadians);

                for (int ix = 0; ix < 200; ix++)
                {
                    double x = (ix + 0.5d) * 2d * Radius / 200d;

                    for (int iz = 0; iz < 200; iz++)
                    {
                        double z = (iz + 0.5d) * 2d * Radius / 200d;
                        if (!TankGeometry.Inside(x, z, Radius)) continue;

                        double along = (x - Radius) * cos + (z - Radius) * sin;
                        double h = bed.Height(x, z);

                        if (along > 0d) { towards += h; n++; }
                        else { away += h; m++; }
                    }
                }

                double shallow = towards / n;
                double deep = away / m;

                _output.WriteLine(
                    $"seed {seed}: tilt at {bed.TiltDirectionRadians:0.000} rad, " +
                    $"the arc it points into averages {shallow:0.000} m and the far side " +
                    $"{deep:0.000} m — a difference of {shallow - deep:0.000} m on a dial of {Tilt}");

                // Positive means the floor rises that way: the shallow arc.
                Assert.True(shallow > 0d && deep < 0d, "the tilt does not fall the way theta says");

                // Half the dial is the mean of a plane over a half disc against the other, scaled
                // by the centroid of a half disc at 4/(3pi) of the radius: 0.42 of the dial.
                Assert.Equal(0.424d * Tilt, shallow - deep, 0.05d * Tilt);
            }
        }

        [Fact]
        public void TheHollowsAreCounted()
        {
            // Spec item 4: two to four hollows a body can lie in, at least one ridge, the rest
            // gentle, measured on the generated map so the smoke can say it. Reported rather than
            // gated at a number, because how many basins a seed draws is the seed's business and
            // a test that demanded three would be testing the RNG; what is asserted is that the
            // instrument answers and that a flat bed has none.
            _output.WriteLine("seed  hollows  ridges  range     tilt  scale   steepest");

            int withHollows = 0;

            // Read with no tilt, because a hollow is a closed basin and a ramp has none: on a
            // floor that falls one way everywhere, water and detritus leave every dimple
            // downhill, and the count says so. What that costs the round is
            // WhatTheSlopeBoundLeavesForTheRelief's table.
            foreach (ulong seed in new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL })
            {
                BedShape bed = Bed(relief: 12f, tilt: 0f, seed: seed);

                _output.WriteLine(
                    $"{seed,4}  {bed.Hollows,7}  {bed.Ridges,6}  {bed.RangeMetres,7:0.000} m  " +
                    $"{bed.TiltMetres,4} m  {bed.ScaleMetres,5:0.0} m  " +
                    $"{bed.SteepestSlopeRadians * 180d / Math.PI,6:0.0}°");

                if (bed.Hollows > 0) withHollows++;
            }

            Assert.True(withHollows >= 3, $"only {withHollows} seeds of five drew a hollow at all");

            BedShape flat = Bed(relief: 0f, tilt: 0f);
            Assert.Equal(0, flat.Hollows);
            Assert.Equal(0, flat.Ridges);
        }

        [Fact]
        public void TheRefusals()
        {
            // Every one of them is a world that would otherwise be built and quietly be something
            // else: a negative dial, a floor that reaches the lit band or the surface, a disc with
            // no area.
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(relief: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(tilt: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(scale: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BedShape(Radius, Depth, float.NaN, 0f, 0f, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BedShape(Radius, Depth, float.PositiveInfinity, 0f, 0f, 1UL));

            // The floor reaching the surface: the beach, which is a round of its own.
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(relief: 55f, tilt: 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Bed(relief: 4f, tilt: 112f));

            // And a tank that is not one.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BedShape(0f, Depth, Relief, Tilt, 0f, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BedShape(Radius, 0f, Relief, Tilt, 0f, 1UL));

            // What is allowed: the flat bed at any depth, and a relief that clears the bound.
            Assert.False(new BedShape(Radius, Depth, 0f, 0f, 0f, 1UL).HasRelief);
            Assert.True(Bed().HasRelief);
        }

        [Fact]
        public void TheWorldRefusesABedInABox()
        {
            // D092's ruling in the shape World states it: the bed is the tank's, because the box
            // is periodic on both horizontal axes and a height map on a ring would have to meet
            // itself at two seams.
            var config = new RunConfig
            {
                WorldShape = WorldShape.Box,
                BedReliefMetres = 2f,
            };

            ArgumentException thrown = Assert.Throws<ArgumentException>(() => new World(config));
            Assert.Contains("bed is the tank's", thrown.Message);

            _output.WriteLine(thrown.Message);
        }
    }
}
