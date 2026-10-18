using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The bed's early-out (<see cref="ContactBed.ClearOfAnyFloor"/>, 2026-10-03) never fires
    /// where the exact test pushes, and the push with it is the push without it, to the bit.
    /// </summary>
    /// <remarks>
    /// Seven beds: the flat one, the tests' recorded floor (relief 1.5 m, tilt 30 m), the
    /// divergence tests' small tank, the placement tests' low relief, the beach of rounds 49 and
    /// 50 (a shoal 1 m under the surface), a steep short-wave floor, and a tilt alone. On each,
    /// places inside the disc and past the glass, at every height from inside the rock to well
    /// above the crest, with radii from nothing to three metres, and a band of places hugging the
    /// highest column the lattice finds, where a bound that was not sound would show first.
    /// </remarks>
    public sealed class ContactBedBoundTests
    {
        private readonly ITestOutputHelper _out;

        public ContactBedBoundTests(ITestOutputHelper output) => _out = output;

        public static TheoryData<string> Beds => new TheoryData<string>
        {
            "flat", "recorded", "divergence", "placement", "beach", "steep", "tilt",
        };

        private static ulong Stable(string name)
        {
            ulong h = 0;
            foreach (char c in name) h = h * 31 + c;
            return h;
        }

        private static BedShape Bed(string name)
        {
            float tank = R42Tank.Radius;

            switch (name)
            {
                case "flat": return new BedShape(tank, 45f, 0f, 0f, 0f, RunFixture.BedSeed);
                case "recorded": return new BedShape(tank, 45f, 1.5f, 30f, 0f, RunFixture.BedSeed);
                case "divergence": return new BedShape(26f, 45f, 1.5f, 30f, 0f, RunFixture.BedSeed);
                case "placement": return new BedShape(tank, 45f, 0.5f, 30f, 0f, Rng.SeedFor(3UL, 99UL));
                case "beach":
                    return new BedShape(
                        tank, 45f, 1.5f, 30f, 0f, Rng.SeedFor(1UL, World.BedShapeIndex), 1f, 15f);
                case "steep": return new BedShape(tank, 45f, 6f, 10f, 8f, Rng.SeedFor(7UL, World.BedShapeIndex));
                case "tilt": return new BedShape(tank, 45f, 0f, 20f, 0f, Rng.SeedFor(5UL, World.BedShapeIndex));
                default: throw new ArgumentException(name);
            }
        }

        [Theory]
        [MemberData(nameof(Beds))]
        public void TheEarlyOutNeverFiresWhereTheExactTestPushes(string name)
        {
            BedShape bed = Bed(name);
            var config = new SolverConfig { Bed = bed, WorldDepthMetres = bed.DepthMetres };

            double radius = bed.RadiusMetres;
            var rng = new Rng(20261003UL + Stable(name));

            // The highest column on a half-metre lattice over the disc: the crest the band of
            // places below hugs.
            double crestX = radius, crestZ = radius, crest = double.MinValue;
            for (double x = 0; x <= 2 * radius; x += 0.5)
            for (double z = 0; z <= 2 * radius; z += 0.5)
            {
                double dx = x - radius, dz = z - radius;
                if (dx * dx + dz * dz > radius * radius) continue;
                double h = bed.Height(x, z);
                if (h > crest) { crest = h; crestX = x; crestZ = z; }
            }

            long fired = 0, pushed = 0, samples = 0;
            double tightest = double.MaxValue;

            for (int i = 0; i < 200_000; i++)
            {
                double x, z;

                if (i % 4 == 0)
                {
                    // Within a couple of metres of the crest.
                    x = crestX + (rng.NextFloat() - 0.5) * 4.0;
                    z = crestZ + (rng.NextFloat() - 0.5) * 4.0;
                }
                else
                {
                    // Anywhere out to 1.3 radii from the axis, past the glass included.
                    double angle = rng.NextFloat() * 2.0 * Math.PI;
                    double at = 1.3 * radius * Math.Sqrt(rng.NextFloat());
                    x = radius + at * Math.Cos(angle);
                    z = radius + at * Math.Sin(angle);
                }

                double r = i % 10 == 0 ? 0.0 : 3.0 * rng.NextFloat();
                double floor = bed.FloorY(x, z);

                // From a metre inside the rock to a few metres above the crest's reach; one in
                // eight far up the column, where nearly every link of a run is.
                double y = i % 8 == 0
                    ? floor + 40.0 * rng.NextFloat()
                    : floor - 1.0 + (r * (1.0 + bed.SteepestSlopeBound) + 4.0) * rng.NextFloat();

                var centre = new Vec3(x, y, z);
                var velocity = new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5);

                bool clear = ContactBed.ClearOfAnyFloor(centre, r, bed);
                Vec3 exact = ContactBed.PushExact(centre, r, velocity, 2.0, config, 800.0, 80.0);
                Vec3 push = ContactBed.Push(centre, r, velocity, 2.0, config, 800.0, 80.0);

                bool nonZero = exact.X != 0 || exact.Y != 0 || exact.Z != 0;

                Assert.False(
                    clear && nonZero,
                    FormattableString.Invariant(
                        $"{name}: the early-out fired at ({x:R}, {y:R}, {z:R}) radius {r:R}, where the exact test pushes {exact.Y:R}"));

                Assert.Equal(BitConverter.DoubleToInt64Bits(exact.X), BitConverter.DoubleToInt64Bits(push.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Y), BitConverter.DoubleToInt64Bits(push.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Z), BitConverter.DoubleToInt64Bits(push.Z));

                samples++;
                if (clear) fired++;
                if (nonZero) pushed++;

                // How close to the rock the early-out ever let a sphere go, in its own terms.
                if (clear) tightest = Math.Min(tightest, (y - floor) - r);
            }

            _out.WriteLine(FormattableString.Invariant(
                $"{name}: {samples} places, early-out {fired}, exact push {pushed}; crest {crest:0.###} m above the mean floor; slope bound {bed.SteepestSlopeBound:0.####}; closest a cleared sphere came to its own floor {tightest:0.###} m"));

            Assert.True(fired > samples / 10, "the early-out hardly fired, so this tests little");
            Assert.True(pushed > samples / 20, "the exact test hardly pushed, so this tests little");
        }

        [Theory]
        [MemberData(nameof(Beds))]
        public void TheBoundsHoldAgainstTheMapAtEveryPlace(string name)
        {
            BedShape bed = Bed(name);
            double radius = bed.RadiusMetres;
            var rng = new Rng(20261004UL + Stable(name));

            double closest = double.MaxValue, steepest = 0;

            for (int i = 0; i < 200_000; i++)
            {
                double x = radius + (rng.NextFloat() - 0.5) * 3.0 * radius;
                double z = radius + (rng.NextFloat() - 0.5) * 3.0 * radius;

                double bound = bed.HeightUpperBound(x, z);

                bed.HeightAndGradient(x, z, out double h, out double gx, out double gz);
                bed.HeightGradientAndHessian(x, z, out double h2, out double gx2, out double gz2, out _, out _, out _);

                Assert.True(bound >= h, FormattableString.Invariant($"{name}: height {h:R} over its bound {bound:R} at ({x:R}, {z:R})"));
                Assert.True(bound >= h2, FormattableString.Invariant($"{name}: height {h2:R} over its bound {bound:R}"));
                Assert.True(bound >= bed.Height(x, z));

                double slope = Math.Sqrt(gx * gx + gz * gz);
                double slope2 = Math.Sqrt(gx2 * gx2 + gz2 * gz2);
                Assert.True(bed.SteepestSlopeBound >= slope, FormattableString.Invariant($"{name}: slope {slope:R} over its bound {bed.SteepestSlopeBound:R}"));
                Assert.True(bed.SteepestSlopeBound >= slope2);

                closest = Math.Min(closest, bound - h);
                steepest = Math.Max(steepest, slope);
            }

            // Not a number in, no bound out, on a shaped bed: the exact test there pushes NaN and
            // the early-out must leave it to. The flat bed reads no place at all, so the two agree
            // there whatever the place is.
            var config = new SolverConfig { Bed = bed, WorldDepthMetres = bed.DepthMetres };
            Vec3[] odd =
            {
                new Vec3(double.NaN, -20, radius), new Vec3(radius, double.NaN, radius),
                new Vec3(radius, -20, double.PositiveInfinity), new Vec3(double.NegativeInfinity, -20, radius),
                new Vec3(radius, double.PositiveInfinity, radius),
            };

            foreach (Vec3 centre in odd)
            {
                bool clear = ContactBed.ClearOfAnyFloor(centre, 1.0, bed);
                Vec3 exact = ContactBed.PushExact(centre, 1.0, Vec3.Zero, 2.0, config, 800.0, 80.0);
                Assert.False(clear && (exact.X != 0 || exact.Y != 0 || exact.Z != 0 || double.IsNaN(exact.Y)));
                if (bed.HasRelief && !double.IsInfinity(centre.Y)) Assert.False(clear, centre.ToString());
            }

            if (bed.HasRelief) Assert.True(double.IsNaN(bed.HeightUpperBound(double.NaN, 0)));

            _out.WriteLine(FormattableString.Invariant(
                $"{name}: closest the height came to its bound {closest:0.####} m; steepest slope seen {steepest:0.####} against the bound {bed.SteepestSlopeBound:0.####}"));
        }
    }
}
