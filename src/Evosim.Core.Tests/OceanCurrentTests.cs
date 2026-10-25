using System;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D204's open ocean current, <see cref="CurrentMode.Ocean"/>: the checks of
    /// <c>logbook/specs/open-ocean-current.md</c> that a field can answer on its own, and the
    /// plumbing that carries it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read on a square 120 m a side</b> in four patches, 25 m deep, the knob at round 61's
    /// 0.1 m/s over a 6,000 s period, on a flat bed and on a square floor
    /// (<see cref="BedShape.ForBox"/>). Smaller than the open square's 300 m because the floor fits
    /// itself over every half-metre column, which at 300 m takes tens of seconds a floor; the
    /// properties are the same at any side, since the vectors are whole waves along it. The open
    /// square's own RMS and bound are read once, in a test traited slow. The grid's books are read
    /// on a 60 m square, where a grid of 2 m cells is small enough to step for five minutes of water.
    /// </para>
    /// <para>
    /// <b>The properties hold by construction, which is why they are measured</b>, as
    /// <see cref="BedStreamsTests"/> says of the streams: the ocean is a different flat field
    /// carried through the same map, and nothing about the map can be checked by reading the
    /// flat field.
    /// </para>
    /// </remarks>
    public class OceanCurrentTests
    {
        private readonly ITestOutputHelper _output;

        public OceanCurrentTests(ITestOutputHelper output) => _output = output;

        private const float Side = 120f;
        private const int Patches = 4;
        private const int Across = 2;
        private const float Depth = 25f;
        private const float Speed = 0.1f;
        private const float Period = 6000f;
        private const float Relief = 8f;
        private const ulong RunSeed = 20261019UL;

        private static BedShape Floor(
            float side = Side, float depth = Depth, float relief = Relief,
            float shoreDepth = 0f, float shoreFade = 0f) =>
            BedShape.ForBox(
                side, side, depth, relief, 0f, Rng.SeedFor(RunSeed, World.BedShapeIndex),
                shoreDepth, shoreFade);

        private static CurrentField Ocean(
            BedShape bed, float speed = Speed, float side = Side, int patches = Patches,
            int across = Across, float depth = Depth, CurrentMode mode = CurrentMode.Ocean)
        {
            var field = new CurrentField
            {
                Mode = mode,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                side / across, patches, depth, Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: across, shape: WorldShape.Box, bed: bed);

            return field;
        }

        private static double FloorY(BedShape bed, double x, double z, float depth = Depth) =>
            bed == null ? -depth : bed.FloorY(x, z);

        /// <summary>A point in the water, uniform over the square, held clear of the floor and the surface.</summary>
        private static (float X, float Y, float Z) Interior(
            Rng rng, BedShape bed, double inset, float side = Side, float depth = Depth)
        {
            float x = rng.NextFloat() * side;
            float z = rng.NextFloat() * side;

            double floor = FloorY(bed, x, z, depth);
            double y = floor + inset + rng.NextFloat() * (0d - floor - 2d * inset);

            return (x, (float)y, z);
        }

        /// <summary>A point uniform over the water's volume: uniform in the square's prism, kept if it is above the floor.</summary>
        private static (float X, float Y, float Z) InVolume(Rng rng, BedShape bed, float side = Side)
        {
            while (true)
            {
                float x = rng.NextFloat() * side;
                float z = rng.NextFloat() * side;
                float y = -rng.NextFloat() * Depth;

                if (y >= FloorY(bed, x, z)) return (x, y, z);
            }
        }

        private static double Rms(CurrentField field, BedShape bed, int samples, ulong seed, float side = Side)
        {
            var rng = new Rng(seed);
            double sum = 0d;

            for (int i = 0; i < samples; i++)
            {
                (float x, float y, float z) = InVolume(rng, bed, side);
                double t = rng.NextFloat() * 200d * Period;

                double speed = field.VelocityAt(x, y, z, t).Magnitude;
                sum += speed * speed;
            }

            return Math.Sqrt(sum / samples);
        }

        // Built once for the class: a floor fits itself over every half-metre column of the square.
        private static readonly BedShape Shaped = Floor();

        private static readonly BedShape[] Floors = { null, Shaped };

        private static string Name(BedShape bed) => bed == null ? "flat" : "floor";

        // ------------------------------------------------------------------ the mode set

        [Fact]
        public void TheVectorTableKeepsItsRule()
        {
            // The rule the table was drawn by (CurrentField.Ocean.cs): 25 distinct vectors in the
            // half-plane, 2 ≤ |(m, n)| ≤ 10, one of each ± pair, none on an axis below 5, and no
            // direction left more than 20° from its neighbours.
            int[] ms = Table("OceanWavesX");
            int[] ns = Table("OceanWavesZ");

            Assert.Equal(25, ms.Length);
            Assert.Equal(25, ns.Length);

            var directions = new double[25];

            for (int i = 0; i < 25; i++)
            {
                int m = ms[i], n = ns[i];
                double norm = Math.Sqrt(m * m + n * n);

                Assert.True(m > 0 || (m == 0 && n > 0), $"({m}, {n}) is not in the half-plane");
                Assert.InRange(norm, 2d, 10d);
                Assert.False(norm < 5d && (m == 0 || n == 0), $"({m}, {n}) is a jet on an axis at |k| {norm:0.00}");

                for (int j = 0; j < i; j++)
                {
                    Assert.False(ms[j] == m && ns[j] == n, $"({m}, {n}) twice");
                    Assert.False(ms[j] == -m && ns[j] == -n, $"({m}, {n}) and its negative");
                }

                if (i > 0)
                {
                    double before = Math.Sqrt(ms[i - 1] * ms[i - 1] + ns[i - 1] * ns[i - 1]);
                    Assert.True(norm >= before, "the table is in order of |(m, n)|, which the rates rise with");
                }

                double degrees = Math.Atan2(n, m) * 180d / Math.PI;
                directions[i] = degrees < 0d ? degrees + 180d : degrees;
            }

            Array.Sort(directions);
            double widest = directions[0] + 180d - directions[24];
            for (int i = 1; i < 25; i++) widest = Math.Max(widest, directions[i] - directions[i - 1]);

            _output.WriteLine($"widest gap between directions {widest:0.0}°");
            Assert.True(widest <= 20d, $"a gap of {widest:0.0}° between directions");
        }

        private static int[] Table(string name) =>
            (int[])typeof(CurrentField)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

        // ------------------------------------------------------------------ checks 1 and 2

        [Fact]
        public void TheOceanIsDivergenceFree()
        {
            const double H = 0.05;

            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed);
                double rms = Rms(field, bed, 2000, 3UL);

                var rng = new Rng(7UL);
                double worst = 0d, worstGradient = 0d;

                for (int i = 0; i < 1000; i++)
                {
                    (float x, float y, float z) = Interior(rng, bed, inset: 4d * H);
                    double t = rng.NextFloat() * 40d * Period;

                    double dudx = (Vx(field, x + H, y, z, t) - Vx(field, x - H, y, z, t)) / (2d * H);
                    double dvdy = (Vy(field, x, y + H, z, t) - Vy(field, x, y - H, z, t)) / (2d * H);
                    double dwdz = (Vz(field, x, y, z + H, t) - Vz(field, x, y, z - H, t)) / (2d * H);

                    worst = Math.Max(worst, Math.Abs(dudx + dvdy + dwdz));
                    worstGradient = Math.Max(worstGradient, Math.Abs(dudx) + Math.Abs(dvdy) + Math.Abs(dwdz));
                }

                _output.WriteLine(
                    $"{Name(bed)}: worst |div u| {worst:0.0000e+0} /s over 1,000 points, gradients up to " +
                    $"{worstGradient:0.0000e+0} /s, RMS {rms:0.0000} m/s");

                Assert.True(worst < 1e-3 * rms, $"{Name(bed)}: worst divergence {worst:0.0000e+0} /s");
            }
        }

        private static double Vx(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).X;

        private static double Vy(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Y;

        private static double Vz(CurrentField f, double x, double y, double z, double t) =>
            f.VelocityAt((float)x, (float)y, (float)z, t).Z;

        [Fact]
        public void NothingFlowsThroughTheFloorOrTheSurface()
        {
            // The layers are still at the bed, so the water there is not only tangential but
            // still; and the map's off-diagonals carry a factor y, so at the waterline the
            // vertical is exactly the flat field's, which is exactly zero.
            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed);
                var rng = new Rng(11UL);
                double worstAtFloor = 0d, surface = 0d;

                for (int i = 0; i < 500; i++)
                {
                    float x = rng.NextFloat() * Side;
                    float z = rng.NextFloat() * Side;
                    double t = rng.NextFloat() * 40d * Period;

                    Float3 atFloor = field.VelocityAt(x, (float)FloorY(bed, x, z), z, t);
                    worstAtFloor = Math.Max(worstAtFloor, atFloor.Magnitude);

                    Float3 top = field.VelocityAt(x, 0f, z, t);
                    Assert.Equal(0f, top.Y);
                    Assert.Equal(0f, field.VelocityAt(x, 2f, z, t).Y);
                    surface += (double)top.X * top.X + (double)top.Z * top.Z;
                }

                surface = Math.Sqrt(surface / 500);

                _output.WriteLine(
                    $"{Name(bed)}: fastest water at the floor {worstAtFloor:0.0000e+0} m/s; " +
                    $"RMS at the surface {surface:0.0000} m/s");

                Assert.True(worstAtFloor < 1e-6, $"{Name(bed)}: water moving at the floor at {worstAtFloor} m/s");

                // The coordinator's choice of 2026-10-19: the layers move most at the surface.
                Assert.True(surface > Speed, $"{Name(bed)}: the surface moves at {surface} m/s RMS");
            }
        }

        // ------------------------------------------------------------------ check 3

        [Fact]
        public void TheSeamsJoin()
        {
            // The field is periodic in whole waves and folded before it is read, so the far side of
            // a seam is the same water, to the rounding a fold of a float leaves.
            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed);
                var rng = new Rng(13UL);
                double worst = 0d, worstPotential = 0d;

                for (int i = 0; i < 400; i++)
                {
                    (float x, float y, float z) = Interior(rng, bed, inset: 0.1d);
                    double t = rng.NextFloat() * 40d * Period;

                    Float3 v = field.VelocityAt(x, y, z, t);
                    Float3 a = field.PotentialAt(x, y, z, t);

                    foreach ((float dx, float dz) in new[] { (Side, 0f), (-Side, 0f), (0f, Side), (0f, -Side), (Side, Side) })
                    {
                        worst = Math.Max(worst, (field.VelocityAt(x + dx, y, z + dz, t) - v).Magnitude);
                        worstPotential = Math.Max(worstPotential, (field.PotentialAt(x + dx, y, z + dz, t) - a).Magnitude);
                    }
                }

                // Both edges of a seam, read from either side.
                float y0 = -0.5f * Depth;
                foreach (float along in new[] { 10f, 60f, 110f })
                {
                    worst = Math.Max(worst, (field.VelocityAt(0f, y0, along, 1234d) - field.VelocityAt(Side, y0, along, 1234d)).Magnitude);
                    worst = Math.Max(worst, (field.VelocityAt(along, y0, 0f, 1234d) - field.VelocityAt(along, y0, Side, 1234d)).Magnitude);
                }

                _output.WriteLine(
                    $"{Name(bed)}: worst velocity across a seam {worst:0.0000e+0} m/s, potential {worstPotential:0.0000e+0} m²/s");

                Assert.True(worst < 1e-4 * Speed, $"{Name(bed)}: the seam parts by {worst} m/s");
                Assert.True(worstPotential < 1e-3, $"{Name(bed)}: the potential parts by {worstPotential} m²/s");
            }
        }

        // ------------------------------------------------------------------ check 4

        [Fact]
        public void TheRmsSpeedOverTheWaterIsTheKnob() =>
            ReadTheKnob(Floors, Side, Patches, Across, samples: 20000);

        [Fact]
        [Trait("Category", "Slow")]
        public void TheRmsSpeedOverTheOpenSquareIsTheKnob() =>
            // The open square itself, 300 m a side in nine patches: its floor alone takes tens of
            // seconds to fit, which is why the class reads a smaller square.
            ReadTheKnob(new[] { null, Floor(side: 300f) }, 300f, 9, 3, samples: 40000);

        private void ReadTheKnob(BedShape[] floors, float side, int patches, int across, int samples)
        {
            // The scale is worked exactly in time per point and walked over the columns (BuildOcean),
            // so this reading is a check by sampling: uniform over the water's volume and over
            // 200 periods of the clock.
            foreach (BedShape bed in floors)
            foreach (float speed in new[] { 0.05f, 0.1f })
            {
                CurrentField field = Ocean(bed, speed, side, patches, across);

                double rms = Rms(field, bed, samples, 5UL, side);
                double fastest = 0d;

                var rng = new Rng(9UL);
                for (int i = 0; i < samples; i++)
                {
                    (float x, float y, float z) = InVolume(rng, bed, side);
                    fastest = Math.Max(fastest, field.VelocityAt(x, y, z, rng.NextFloat() * 200d * Period).Magnitude);
                }

                _output.WriteLine(
                    $"{Name(bed)}, {side} m square, at {speed} m/s: RMS {rms:0.0000} m/s ({rms / speed:0.0%} of the knob), " +
                    $"fastest sample {fastest:0.0000} m/s, bound {field.MaximumTransportSpeed:0.0000} m/s " +
                    $"({field.MaximumTransportSpeed / fastest:0.00}x the fastest sample)");

                Assert.Equal((double)speed, rms, 0.03 * speed);
                Assert.True(
                    field.MaximumTransportSpeed > fastest,
                    $"the bound {field.MaximumTransportSpeed} m/s is under the fastest sample {fastest} m/s");
            }
        }

        // ------------------------------------------------------------------ check 7

        [Fact]
        public void NothingFlowsRoundTheSquareAsAWhole()
        {
            // The flux through a whole section across the square is the circulation of the
            // potential round its edge: zero along the floor, zero along the surface, and the two
            // seams cancel. So the net flow through every section, and the mean velocity over the
            // square, is zero. Read here with the midpoint rule over each section.
            const int Columns = 300;
            const int Heights = 40;

            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed);
                double worstShare = 0d;

                foreach (double t in new[] { 0d, 777d, 31337d })
                foreach (float at in new[] { 0f, 37.5f, 101f })
                {
                    double netX = 0d, grossX = 0d, netZ = 0d, grossZ = 0d;

                    for (int i = 0; i < Columns; i++)
                    {
                        float across = (float)((i + 0.5d) * Side / Columns);

                        double floorX = FloorY(bed, at, across);
                        double floorZ = FloorY(bed, across, at);

                        for (int j = 0; j < Heights; j++)
                        {
                            double share = (j + 0.5d) / Heights;

                            float yx = (float)(floorX * (1d - share));
                            double ux = field.VelocityAt(at, yx, across, t).X * -floorX / Heights;
                            netX += ux;
                            grossX += Math.Abs(ux);

                            float yz = (float)(floorZ * (1d - share));
                            double uz = field.VelocityAt(across, yz, at, t).Z * -floorZ / Heights;
                            netZ += uz;
                            grossZ += Math.Abs(uz);
                        }
                    }

                    worstShare = Math.Max(worstShare, Math.Max(Math.Abs(netX) / grossX, Math.Abs(netZ) / grossZ));
                }

                _output.WriteLine($"{Name(bed)}: the largest net flux through a section is {worstShare:0.0000e+0} of the gross");

                Assert.True(worstShare < 1e-3, $"{Name(bed)}: a section carries {worstShare:0.0%} of its gross flux net");
            }
        }

        // ------------------------------------------------------------------ what the grid and the bodies read

        [Fact]
        public void TheCurlOfThePotentialIsTheVelocity()
        {
            const double H = 0.02;

            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed, speed: 0.37f);
                var rng = new Rng(23UL);
                double sumSquared = 0d, sumReference = 0d, worst = 0d;
                int n = 0;

                for (int i = 0; i < 500; i++)
                {
                    (float x, float y, float z) = Interior(rng, bed, inset: 4d * H);
                    double t = rng.NextFloat() * 40d * Period;

                    Float3 ax1 = field.PotentialAt((float)(x + H), y, z, t);
                    Float3 ax0 = field.PotentialAt((float)(x - H), y, z, t);
                    Float3 ay1 = field.PotentialAt(x, (float)(y + H), z, t);
                    Float3 ay0 = field.PotentialAt(x, (float)(y - H), z, t);
                    Float3 az1 = field.PotentialAt(x, y, (float)(z + H), t);
                    Float3 az0 = field.PotentialAt(x, y, (float)(z - H), t);

                    double perMetre = 1d / (2d * H);

                    var curl = new Float3(
                        (float)(((ay1.Z - ay0.Z) - (az1.Y - az0.Y)) * perMetre),
                        (float)(((az1.X - az0.X) - (ax1.Z - ax0.Z)) * perMetre),
                        (float)(((ax1.Y - ax0.Y) - (ay1.X - ay0.X)) * perMetre));

                    Float3 v = field.VelocityAt(x, y, z, t);
                    Float3 difference = curl - v;

                    sumSquared += (double)difference.Magnitude * difference.Magnitude;
                    sumReference += (double)v.Magnitude * v.Magnitude;
                    worst = Math.Max(worst, difference.Magnitude);
                    n++;
                }

                double rmsError = Math.Sqrt(sumSquared / n);
                double rms = Math.Sqrt(sumReference / n);

                _output.WriteLine(
                    $"{Name(bed)}: curl of A against u, RMS error {rmsError / rms:0.000%}, worst {worst / rms:0.00%} of the RMS");

                Assert.True(rmsError < 1e-3 * rms, $"{Name(bed)}: RMS error {rmsError / rms:0.000%}");
                Assert.True(worst < 1e-2 * rms, $"{Name(bed)}: worst error {worst / rms:0.00%}");
            }
        }

        [Fact]
        public void ThePotentialVanishesAtTheFloor()
        {
            // What keeps the grid's floor faces dry: every component of the pulled-back potential
            // carries A_y, and every layer is zero at the bed. A point in the rock reads the floor's
            // own water, so it is zero to the rounding of the map's stretch (ỹ = −d·D/d lands on −D
            // within an ulp, and a layer a hair above the bed is a hair from zero); on a flat bed it
            // is exact. A float on the floor can sit an ulp above it, in the water, and reads that:
            // about 2e-6 m at 25 m, so the potential a few millionths of its mid-water size.
            foreach (BedShape bed in Floors)
            {
                CurrentField field = Ocean(bed);
                var rng = new Rng(29UL);
                double worstInRock = 0d, worstOnFloor = 0d;

                for (int i = 0; i < 300; i++)
                {
                    float x = rng.NextFloat() * Side;
                    float z = rng.NextFloat() * Side;
                    double t = rng.NextFloat() * 40d * Period;
                    double floor = FloorY(bed, x, z);

                    worstInRock = Math.Max(worstInRock, field.PotentialAt(x, (float)(floor - 0.5d), z, t).Magnitude);
                    worstOnFloor = Math.Max(worstOnFloor, field.PotentialAt(x, (float)floor, z, t).Magnitude);
                }

                double typical = field.PotentialAt(60f, -0.5f * Depth, 60f, 0d).Magnitude;

                _output.WriteLine(
                    $"{Name(bed)}: largest potential in the rock {worstInRock:0.0000e+0}, on the float floor " +
                    $"{worstOnFloor:0.0000e+0}, against {typical:0.0000} m²/s mid-water");

                if (bed == null) Assert.Equal(0d, worstInRock);
                Assert.True(worstInRock < 1e-12, $"{Name(bed)}: {worstInRock} m²/s in the rock");
                Assert.True(worstOnFloor < 1e-5 * typical, $"{Name(bed)}: {worstOnFloor} m²/s on the floor");
            }
        }

        [Fact]
        public void TheColumnPathIsTheDirectPathToTheBit()
        {
            // The grid samples the potential through precomputed columns; a column taken at a
            // seam is folded as the direct path folds its point, so the two read one floor.
            BedShape bed = Shaped;
            CurrentField field = Ocean(bed);
            var rng = new Rng(31UL);

            for (int i = 0; i < 400; i++)
            {
                float x = i < 20 ? (i % 2 == 0 ? 0f : Side) : rng.NextFloat() * Side;
                float z = i < 20 ? rng.NextFloat() * Side : i < 40 ? Side : rng.NextFloat() * Side;
                float y = -rng.NextFloat() * Depth;
                double t = rng.NextFloat() * 40d * Period;

                CurrentField.BedColumn column = field.ColumnAt(x, z);

                Assert.Equal(field.PotentialAt(x, y, z, t), field.PotentialAt(x, y, z, t, column));
            }
        }

        [Fact]
        public void ThePinnedFieldAnswersTheSameBits()
        {
            // A pin fills the ocean's slots for both groupings of the clock, and then sampling is a
            // pure read; it changes no answer.
            foreach (BedShape bed in Floors)
            {
                CurrentField pinned = Ocean(bed);
                CurrentField free = Ocean(bed);
                var rng = new Rng(37UL);

                for (int step = 0; step < 20; step++)
                {
                    double seconds = rng.NextFloat() * 40d * Period;

                    pinned.PinInstant(seconds);

                    try
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            (float x, float y, float z) = Interior(rng, bed, inset: 0.1d);

                            Assert.Equal(free.VelocityAt(x, y, z, seconds), pinned.VelocityAt(x, y, z, seconds));
                            Assert.Equal(free.AccelerationAt(x, y, z, seconds), pinned.AccelerationAt(x, y, z, seconds));
                            Assert.Equal(free.PotentialAt(x, y, z, seconds), pinned.PotentialAt(x, y, z, seconds));
                        }

                        Assert.Throws<InvalidOperationException>(() => pinned.VelocityAt(1f, -1f, 1f, seconds + 1d));
                    }
                    finally
                    {
                        pinned.UnpinInstant();
                    }
                }
            }
        }

        [Fact]
        public void TheAccelerationAgreesWithAStencilOnTheSameWater()
        {
            // The analytic route, flat and through the map and through the shore's fade, against a
            // central difference of the velocity along the path, as BedStreamsTests holds the
            // streams'. The faded floor is a shallow square whose rises reach into the fade band.
            BedShape faded = Floor(depth: 8f, relief: 6f, shoreDepth: 1f, shoreFade: 6f);

            foreach ((BedShape bed, float depth) in new[] { ((BedShape)null, Depth), (Shaped, Depth), (faded, 8f) })
            {
                CurrentField field = Ocean(bed, depth: depth);
                var rng = new Rng(19UL);

                double sumSquared = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d, sumFine = 0d;
                int n = 0, inBand = 0;

                for (int i = 0; i < 400; i++)
                {
                    (float x, float y, float z) = Interior(rng, bed, inset: 0.3d, depth: depth);
                    double t = rng.NextFloat() * 40d * Period;

                    double water = -FloorY(bed, x, z, depth);
                    if (bed == faded && water > 1d && water < 7d) inBand++;

                    Float3 analytic = field.AccelerationAt(x, y, z, t);
                    Float3 stencil = Stencil(field, x, y, z, t, 0.05f, 0.01d);
                    Float3 finer = Stencil(field, x, y, z, t, 0.0125f, 0.0025d);

                    Float3 difference = analytic - stencil;
                    Float3 fineDifference = analytic - finer;

                    sumFine += (double)fineDifference.Magnitude * fineDifference.Magnitude;
                    sumSquared += (double)difference.Magnitude * difference.Magnitude;
                    sumReference += (double)stencil.Magnitude * stencil.Magnitude;
                    worst = Math.Max(worst, fineDifference.Magnitude);
                    worstMagnitude = Math.Max(worstMagnitude, finer.Magnitude);
                    n++;
                }

                double rmsError = Math.Sqrt(sumSquared / n);
                double rmsReference = Math.Sqrt(sumReference / n);
                double rmsFine = Math.Sqrt(sumFine / n);
                string name = bed == faded ? "faded" : Name(bed);

                _output.WriteLine(
                    $"{name}: against the 0.05 m stencil {rmsError / rmsReference:0.00%} RMS, against a stencil " +
                    $"four times finer {rmsFine / rmsReference:0.00%}, worst {worst / worstMagnitude:0.00%}" +
                    (bed == faded ? $"; {inBand} of {n} points over columns in the fade's band" : ""));

                Assert.True(rmsFine < 0.005 * rmsReference, $"{name}: RMS disagreement {rmsFine / rmsReference:0.00%}");
                Assert.True(worst < 0.02 * worstMagnitude, $"{name}: worst disagreement {worst / worstMagnitude:0.00%}");
                if (bed == faded) Assert.True(inBand > 10, $"only {inBand} points over the fade's band");
            }
        }

        private static Float3 Stencil(
            CurrentField field, float x, float y, float z, double t, float h, double dt)
        {
            Float3 u = field.VelocityAt(x, y, z, t);
            float perMetre = 1f / (2f * h);

            Float3 total =
                (field.VelocityAt(x + h, y, z, t) - field.VelocityAt(x - h, y, z, t)) * (perMetre * u.X) +
                (field.VelocityAt(x, y + h, z, t) - field.VelocityAt(x, y - h, z, t)) * (perMetre * u.Y) +
                (field.VelocityAt(x, y, z + h, t) - field.VelocityAt(x, y, z - h, t)) * (perMetre * u.Z);

            return total +
                   (field.VelocityAt(x, y, z, t + dt) - field.VelocityAt(x, y, z, t - dt)) *
                   (float)(1d / (2d * dt));
        }

        // ------------------------------------------------------------------ the grid's books

        [Fact]
        public void AUniformConcentrationStaysUniformInTheOcean()
        {
            // BedGridTests' check, in the square: the face fluxes come from the potential's edge
            // circulations, so a uniform concentration stays uniform and every cell's six fluxes
            // sum to zero, across the seams as everywhere else.
            const float SmallSide = 60f;
            const float SmallDepth = 10f;
            const float Cell = 2f;
            const float Dt = 0.5f;
            const int Steps = 600;

            foreach (bool floored in new[] { false, true })
            {
                BedShape bed = floored ? Floor(SmallSide, SmallDepth, 4f) : null;
                var field = new GridField(
                    SmallSide * SmallSide, 0f, SmallDepth, 0f, 0f, 4, Cell, 2, WorldShape.Box, 0f, bed);
                CurrentField current = Ocean(bed, side: SmallSide, patches: 4, across: 2, depth: SmallDepth);

                field.SeedUniform(1f);
                double initial = field.Recount();

                (int substeps, _, double discrete, double analytic, _, double worstNet) =
                    field.MeasureFaceFluxes(current, 0d, Dt);

                for (int step = 1; step <= Steps; step++)
                {
                    field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
                }

                (double min, double max) = Spread(field);
                double drift = (field.Recount() - initial) / initial;
                double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

                _output.WriteLine(
                    $"{(floored ? "floor" : "flat")}: density {min:0.000000} to {max:0.000000}, total drift " +
                    $"{drift:0.0e+0}, {substeps} substeps, face speeds {discrete:0.0000} / {analytic:0.0000} m/s");

                Assert.True(worst < 1e-9, $"floor {floored}: the density ran {min:R} to {max:R}");
                Assert.True(Math.Abs(drift) < 1e-9, $"floor {floored}: the total drifted by {drift:R}");
                Assert.Equal(0d, worstNet);
            }
        }

        private static (double Min, double Max) Spread(GridField field)
        {
            double volume = field.CellVolume;
            double min = double.MaxValue, max = double.MinValue;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz)) continue;

                double density = field.JoulesAt(ix, iy, iz) / volume;
                min = Math.Min(min, density);
                max = Math.Max(max, density);
            }

            return (min, max);
        }

        // ------------------------------------------------------------------ the plumbing

        [Fact]
        public void OnlyTheOceanFollowsTheSquaresFloor()
        {
            BedShape bed = Shaped;

            // The ocean takes the floor; the box's other fields refuse it while the water moves,
            // and still water over it is a world under any mode.
            Assert.Same(bed, Ocean(bed).Bed);
            Assert.Throws<ArgumentException>(() => Ocean(bed, mode: CurrentMode.Transport));
            Assert.Throws<ArgumentException>(() => Ocean(bed, mode: CurrentMode.Rolls));
            Assert.Null(Ocean(bed, speed: 0f, mode: CurrentMode.Transport).Bed);

            // A mode set after the box re-chooses the floor.
            CurrentField field = Ocean(bed, speed: 0f, mode: CurrentMode.Transport);
            field.Mode = CurrentMode.Ocean;
            Assert.Same(bed, field.Bed);
            field.Mode = CurrentMode.Transport;
            Assert.Null(field.Bed);

            // A tank's floor is not the square's, and a floor of another size does not span it.
            BedShape tankFloor = new BedShape(20f, Depth, 4f, 0f, 0f, 5UL);
            Assert.Throws<ArgumentException>(() => Ocean(tankFloor));
            Assert.Throws<ArgumentException>(() => Ocean(Floor(side: 60f)));
        }

        [Fact]
        public void TheHeaderNamesTheOcean()
        {
            Assert.Equal("0.1 m/s RMS ocean, 6000 s period, advecting the fields", Ocean(null).ToString());
        }
    }
}
