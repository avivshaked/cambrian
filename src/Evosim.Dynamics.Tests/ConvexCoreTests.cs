using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D228's convex cores and GJK: the core and margin of each shape and the bound that
    /// <see cref="Creature.LinkReach"/> puts on them, the support function, and the distance between
    /// two cores against a brute-force minimum and against cases with known answers.
    /// </summary>
    public sealed class ConvexCoreTests
    {
        // ------------------------------------------------------------ cores and margins

        [Fact]
        public void TheCoreAndMarginOfEachShapeFollowTheShapesOwnFormulas()
        {
            var h = new Float3(0.13f, 0.4f, 0.21f);

            Assert.Equal(CoreKind.Point, ConvexCore.Of(new SphereShape(), h, out Vec3 half, out double margin));
            Assert.Equal(Vec3.Zero, half);
            Assert.Equal((0.13 + 0.4 + 0.21) / 3.0, margin, 6);

            // A capsule: its radius is the mean of |hx| and |hz|, its half-span what is left of |hy|.
            Assert.Equal(CoreKind.Segment, ConvexCore.Of(new CapsuleShape(), h, out half, out margin));
            Assert.Equal(0.17, margin, 6);
            Assert.Equal(0.0, half.X);
            Assert.Equal(0.23, half.Y, 6);
            Assert.Equal(0.0, half.Z);

            // A capsule shorter than its radius is a sphere of that radius.
            ConvexCore.Of(new CapsuleShape(), new Float3(0.3f, 0.1f, 0.3f), out half, out margin);
            Assert.Equal(Vec3.Zero, half);
            Assert.Equal(0.3, margin, 6);

            // A box: shrunk by its smallest half-extent, which is the margin; one core extent is 0.
            Assert.Equal(CoreKind.Box, ConvexCore.Of(new BoxShape(), h, out half, out margin));
            Assert.Equal(0.13, margin, 6);
            Assert.Equal(0.0, half.X, 6);
            Assert.Equal(0.27, half.Y, 6);
            Assert.Equal(0.08, half.Z, 6);

            // A flat plate's core is its rectangle.
            ConvexCore.Of(new BoxShape(), new Float3(0.5f, 0.05f, 0.5f), out half, out margin);
            Assert.Equal(0.05, margin, 6);
            Assert.Equal(0.45, half.X, 6);
            Assert.Equal(0.0, half.Y, 6);
            Assert.Equal(0.45, half.Z, 6);

            // Negative half-extents read by size.
            ConvexCore.Of(new BoxShape(), new Float3(-0.5f, 0.05f, -0.5f), out half, out margin);
            Assert.Equal(0.05, margin, 6);
            Assert.Equal(0.45, half.X, 6);
        }

        [Fact]
        public void AShapeWithoutACoreIsRefusedByName()
        {
            var e = Assert.Throws<ArgumentException>(
                () => ConvexCore.Of(new OddShape(), new Float3(1, 1, 1), out _, out _));
            Assert.Contains("odd", e.Message);
        }

        /// <summary>A shape the true-shape contact has no core for.</summary>
        private sealed class OddShape : PartShape
        {
            public override string Id => "odd";
            public override float Volume(Float3 halfExtents) => 1f;
            public override Float3 SurfacePoint(Float3 anchor, Float3 halfExtents) => anchor;
            public override bool ContainsPoint(Float3 local, Float3 halfExtents) => true;
            public override void AddPanels(Float3 halfExtents, int resolution, List<DragPanel> into) { }
        }

        [Fact]
        public void EveryPointOfCorePlusMarginLiesWithinTheLinkReach()
        {
            // The half-diagonal ball is the broadphase, so it has to hold every shape. The farthest point of
            // core plus margin is a core corner and the margin along it.
            var rng = new Rng(2281);
            PartShape[] shapes = { new SphereShape(), new CapsuleShape(), new BoxShape() };

            for (int n = 0; n < 6000; n++)
            {
                PartShape shape = shapes[n % 3];
                var h = new Float3(
                    rng.Range(0.01f, 1.5f) * (rng.Chance(0.2f) ? -1f : 1f),
                    rng.Range(0.01f, 1.5f) * (rng.Chance(0.2f) ? -1f : 1f),
                    rng.Range(0.01f, 1.5f) * (rng.Chance(0.2f) ? -1f : 1f));

                // Thin plates and sticks, as well as blocks.
                if (n % 5 == 0) h = new Float3(h.X, h.Y * 0.05f, h.Z);
                if (n % 7 == 0) h = new Float3(h.X * 0.05f, h.Y, h.Z * 0.05f);

                ConvexCore.Of(shape, h, out Vec3 half, out double margin);

                double reach = Math.Sqrt((double)h.X * h.X + (double)h.Y * h.Y + (double)h.Z * h.Z);
                double farthest = half.Magnitude + margin;

                Assert.True(
                    farthest <= reach + 1e-9,
                    $"{shape.Id} h=({h.X}, {h.Y}, {h.Z}): core {half.Magnitude} + margin {margin} = {farthest} > reach {reach}");
            }
        }

        // ------------------------------------------------------------ support

        [Fact]
        public void TheSupportPointTakesTheSignOfEachLocalComponentAndZeroReadsPlus()
        {
            var half = new Vec3(1, 2, 3);
            var centre = new Vec3(10, 20, 30);

            Assert.Equal(new Vec3(11, 18, 33), ConvexCore.Support(half, Mat3.Identity, centre, new Vec3(1, -1, 1)));
            Assert.Equal(new Vec3(9, 22, 27), ConvexCore.Support(half, Mat3.Identity, centre, new Vec3(-1, 1, -1)));

            // A component of 0 reads as +, whichever way the others point, and so does -0.
            Assert.Equal(new Vec3(11, 22, 33), ConvexCore.Support(half, Mat3.Identity, centre, new Vec3(0, 0, 0)));
            Assert.Equal(new Vec3(9, 22, 33), ConvexCore.Support(half, Mat3.Identity, centre, new Vec3(-5, -0.0, 0)));

            // A degenerate core: the segment's farthest point is an end, the point's is itself.
            Assert.Equal(new Vec3(0, 2, 0), ConvexCore.Support(new Vec3(0, 2, 0), Mat3.Identity, Vec3.Zero, new Vec3(0, 3, 0)));
            Assert.Equal(new Vec3(5, 5, 5), ConvexCore.Support(Vec3.Zero, Mat3.Identity, new Vec3(5, 5, 5), new Vec3(1, 2, 3)));
        }

        [Fact]
        public void TheSupportPointMaximisesTheDirectionOverTheCornersAtAnyTurn()
        {
            var rng = new Rng(77);

            for (int n = 0; n < 500; n++)
            {
                Vec3 half = new Vec3(rng.Range(0, 1), rng.Range(0, 1), rng.Range(0, 1));
                Mat3 r = RandomTurn(rng);
                Vec3 centre = new Vec3(rng.Range(-2, 2), rng.Range(-2, 2), rng.Range(-2, 2));
                Vec3 d = new Vec3(rng.Range(-1, 1), rng.Range(-1, 1), rng.Range(-1, 1));

                double best = double.NegativeInfinity;
                foreach (Vec3 corner in Corners(half)) best = Math.Max(best, Vec3.Dot(centre + r * corner, d));

                Assert.Equal(best, Vec3.Dot(ConvexCore.Support(half, r, centre, d), d), 12);
            }
        }

        // ------------------------------------------------------------ GJK against brute force

        [Fact]
        public void TheDistanceBetweenCoresMatchesABruteForceMinimumToAMicrometre()
        {
            var rng = new Rng(20261025);
            int separated = 0, meeting = 0;
            double worst = 0;

            for (int n = 0; n < 2500; n++)
            {
                Vec3 halfA = RandomHalf(rng), halfB = RandomHalf(rng);
                Mat3 rA = RandomTurn(rng), rB = RandomTurn(rng);
                Vec3 cA = Vec3.Zero;

                // From inside each other to well apart.
                double reach = 0.25 + 2.0 * rng.NextFloat();
                Vec3 dir = new Vec3(rng.Range(-1, 1), rng.Range(-1, 1), rng.Range(-1, 1)).Normalized;
                Vec3 cB = dir * reach;

                double brute = BruteForce(halfA, rA, cA, halfB, rB, cB);
                bool apart = ConvexCore.Distance(
                    halfA, rA, cA, halfB, rB, cB, out double d, out Vec3 pa, out Vec3 pb);

                if (brute > 1e-5)
                {
                    Assert.True(apart, $"pair {n}: apart by {brute} and GJK says it meets");
                    Assert.True(Math.Abs(d - brute) <= 1e-6, $"pair {n}: GJK {d:R}, brute force {brute:R}");

                    // The points are in the cores, and they are d apart.
                    Assert.True(PointToBox(pa, halfA, rA, cA) <= 1e-9, $"pair {n}: point A is off core A");
                    Assert.True(PointToBox(pb, halfB, rB, cB) <= 1e-9, $"pair {n}: point B is off core B");
                    Assert.Equal(d, (pa - pb).Magnitude, 9);

                    worst = Math.Max(worst, Math.Abs(d - brute));
                    separated++;
                }
                else if (brute < 1e-12)
                {
                    Assert.False(apart, $"pair {n}: the cores meet and GJK says {d} apart");
                    meeting++;
                }
            }

            Assert.True(separated > 600 && meeting > 300, $"the draw gave {separated} apart and {meeting} meeting");
            Assert.True(worst <= 1e-6);
        }

        [Fact]
        public void CoresJustTouchingAndJustApartAreToldApart()
        {
            // Two boxes face to face, the second 1e-4 above the first: apart by 1e-4, the closest points on
            // the two faces.
            var halfA = new Vec3(0.3, 0.2, 0.4);
            var halfB = new Vec3(0.5, 0.1, 0.2);

            Assert.True(ConvexCore.Distance(
                halfA, Mat3.Identity, Vec3.Zero, halfB, Mat3.Identity, new Vec3(0.1, 0.3 + 1e-4, 0.05),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(1e-4, d, 9);
            Assert.Equal(0.2, pa.Y, 9);
            Assert.Equal(0.2 + 1e-4, pb.Y, 9);

            // Exactly touching is a meeting.
            Assert.False(ConvexCore.Distance(
                halfA, Mat3.Identity, Vec3.Zero, halfB, Mat3.Identity, new Vec3(0.1, 0.3, 0.05),
                out d, out _, out _));
            Assert.True(d <= ConvexCore.Touching);
        }

        // ------------------------------------------------------------ GJK with known answers

        [Fact]
        public void PointAgainstPointIsTheDistanceBetweenThem()
        {
            Assert.True(ConvexCore.Distance(
                Vec3.Zero, Mat3.Identity, Vec3.Zero, Vec3.Zero, Mat3.Identity, new Vec3(3, 4, 0),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(5.0, d, 12);
            Assert.Equal(Vec3.Zero, pa);
            Assert.Equal(new Vec3(3, 4, 0), pb);

            // The same place is a meeting.
            Assert.False(ConvexCore.Distance(
                Vec3.Zero, Mat3.Identity, new Vec3(1, 1, 1), Vec3.Zero, Mat3.Identity, new Vec3(1, 1, 1),
                out d, out _, out _));
        }

        [Fact]
        public void PointAgainstSegmentIsToTheNearestPointOrEnd()
        {
            // The point beside the segment's middle.
            Assert.True(ConvexCore.Distance(
                new Vec3(0, 1, 0), Mat3.Identity, Vec3.Zero, Vec3.Zero, Mat3.Identity, new Vec3(2, 0.4, 0),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(2.0, d, 12);
            Assert.Equal(0.4, pa.Y, 12);
            Assert.Equal(2.0, pb.X, 12);
            Assert.Equal(0.4, pb.Y, 12);

            // The point past the segment's end.
            Assert.True(ConvexCore.Distance(
                new Vec3(0, 1, 0), Mat3.Identity, Vec3.Zero, Vec3.Zero, Mat3.Identity, new Vec3(2, 3, 0),
                out d, out pa, out pb));
            Assert.Equal(Math.Sqrt(8), d, 12);
            Assert.Equal(1.0, pa.Y, 12);
            Assert.Equal(0.0, pa.X, 12);
        }

        [Fact]
        public void SegmentAgainstSkewSegmentIsTheDistanceBetweenTheirLines()
        {
            // One along x, one along y, 0.3 apart in z and crossing in plan at (0.2, 0).
            Assert.True(ConvexCore.Distance(
                new Vec3(1, 0, 0), Mat3.Identity, Vec3.Zero,
                new Vec3(0, 1, 0), Mat3.Identity, new Vec3(0.2, 0, 0.3),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(0.3, d, 12);
            Assert.Equal(0.2, pa.X, 12);
            Assert.Equal(0.0, pa.Y, 12);
            Assert.Equal(0.0, pa.Z, 12);
            Assert.Equal(0.3, pb.Z, 12);

            // Parallel segments side by side, overlapping in length: any point pair is as near; the distance is exact.
            Assert.True(ConvexCore.Distance(
                new Vec3(1, 0, 0), Mat3.Identity, Vec3.Zero,
                new Vec3(1, 0, 0), Mat3.Identity, new Vec3(0.5, 0.7, 0),
                out d, out _, out _));
            Assert.Equal(0.7, d, 12);

            // Collinear segments with a gap: end to end.
            Assert.True(ConvexCore.Distance(
                new Vec3(1, 0, 0), Mat3.Identity, Vec3.Zero,
                new Vec3(1, 0, 0), Mat3.Identity, new Vec3(3, 0, 0),
                out d, out pa, out pb));
            Assert.Equal(1.0, d, 12);
            Assert.Equal(new Vec3(1, 0, 0), pa);
            Assert.Equal(new Vec3(2, 0, 0), pb);
        }

        [Fact]
        public void BoxAgainstBoxIsFaceToFaceVertexToVertexOrEdgeToEdge()
        {
            var one = new Vec3(1, 1, 1);

            // Face to face, the second shifted so the faces overlap: the gap.
            Assert.True(ConvexCore.Distance(
                one, Mat3.Identity, Vec3.Zero, one, Mat3.Identity, new Vec3(0.5, 2.5, -0.5),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(0.5, d, 12);
            Assert.Equal(1.0, pa.Y, 12);
            Assert.Equal(1.5, pb.Y, 12);

            // Corner to corner.
            Assert.True(ConvexCore.Distance(
                one, Mat3.Identity, Vec3.Zero, one, Mat3.Identity, new Vec3(3, 3, 3),
                out d, out pa, out pb));
            Assert.Equal(Math.Sqrt(3), d, 12);
            Assert.Equal(new Vec3(1, 1, 1), pa);
            Assert.Equal(new Vec3(2, 2, 2), pb);

            // Edge to edge: the second turned 45 degrees about y and set at the first's corner edge.
            Mat3 turned = QuatD.FromAxisAngle(Vec3.UnitY, Math.PI / 4).ToMatrix();
            double reach = Math.Sqrt(2.0);
            Assert.True(ConvexCore.Distance(
                one, Mat3.Identity, Vec3.Zero, one, turned, new Vec3(1 + reach + 0.25, 0, 0),
                out d, out pa, out pb));
            Assert.Equal(0.25, d, 12);
            Assert.Equal(1.0, pa.X, 12);
            Assert.Equal(1.25, pb.X, 12);
        }

        [Fact]
        public void RectangleAgainstRectangleIsTheGapAndTwoCrossedRectanglesMeet()
        {
            var plate = new Vec3(0.45, 0, 0.45);

            // Parallel and overlapping, 0.1 apart.
            Assert.True(ConvexCore.Distance(
                plate, Mat3.Identity, Vec3.Zero, plate, Mat3.Identity, new Vec3(0.2, 0.1, 0.1),
                out double d, out Vec3 pa, out Vec3 pb));
            Assert.Equal(0.1, d, 12);
            Assert.Equal(0.1, pb.Y - pa.Y, 12);

            // The same plane and overlapping: they meet.
            Assert.False(ConvexCore.Distance(
                plate, Mat3.Identity, Vec3.Zero, plate, Mat3.Identity, new Vec3(0.2, 0, 0.1),
                out _, out _, out _));

            // A cross: one flat, one standing through it.
            Mat3 standing = QuatD.FromAxisAngle(Vec3.UnitX, Math.PI / 2).ToMatrix();
            Assert.False(ConvexCore.Distance(
                plate, Mat3.Identity, Vec3.Zero, plate, standing, new Vec3(0.1, 0, 0),
                out _, out _, out _));

            // The standing one lifted clear of the flat one: the gap from its lowest edge.
            Assert.True(ConvexCore.Distance(
                plate, Mat3.Identity, Vec3.Zero, plate, standing, new Vec3(0.1, 0.45 + 0.2, 0),
                out d, out _, out _));
            Assert.Equal(0.2, d, 12);
        }

        // ------------------------------------------------------------ helpers

        private static Vec3 RandomHalf(Rng rng)
        {
            // Points, segments, rectangles and boxes: a component is 0 about a third of the time.
            double x = rng.Chance(0.35f) ? 0 : rng.Range(0.05f, 0.7f);
            double y = rng.Chance(0.35f) ? 0 : rng.Range(0.05f, 0.7f);
            double z = rng.Chance(0.35f) ? 0 : rng.Range(0.05f, 0.7f);
            return new Vec3(x, y, z);
        }

        private static Mat3 RandomTurn(Rng rng)
        {
            Vec3 axis = new Vec3(rng.Range(-1, 1), rng.Range(-1, 1), rng.Range(-1, 1)).Normalized;
            return QuatD.FromAxisAngle(axis, rng.Range(0f, 6.28f)).ToMatrix();
        }

        private static IEnumerable<Vec3> Corners(Vec3 half)
        {
            for (int k = 0; k < 8; k++)
            {
                yield return new Vec3(
                    (k & 1) == 0 ? half.X : -half.X,
                    (k & 2) == 0 ? half.Y : -half.Y,
                    (k & 4) == 0 ? half.Z : -half.Z);
            }
        }

        /// <summary>The distance from a point to a (possibly degenerate) box: clamp in its own frame.</summary>
        private static double PointToBox(Vec3 p, Vec3 half, Mat3 r, Vec3 c)
        {
            Vec3 local = r.TransposedTimes(p - c);
            Vec3 clamped = new Vec3(
                Math.Max(-half.X, Math.Min(half.X, local.X)),
                Math.Max(-half.Y, Math.Min(half.Y, local.Y)),
                Math.Max(-half.Z, Math.Min(half.Z, local.Z)));
            return (local - clamped).Magnitude;
        }

        /// <summary>
        /// The least distance between two solids that are convex polytopes, by sampling: it is reached at a
        /// vertex or along an edge of one of them (an edge against the other solid, or a vertex against it), so
        /// each core's twelve edges are walked at 201 points, and the distance of every point to the other
        /// solid is exact (a clamp in its frame). The distance along an edge to a convex solid is itself convex,
        /// so a ternary search closes the best bracket of each edge to the last digit.
        /// </summary>
        private static double BruteForce(Vec3 halfA, Mat3 rA, Vec3 cA, Vec3 halfB, Mat3 rB, Vec3 cB) =>
            Math.Min(
                EdgesAgainst(halfA, rA, cA, halfB, rB, cB),
                EdgesAgainst(halfB, rB, cB, halfA, rA, cA));

        private static double EdgesAgainst(Vec3 half, Mat3 r, Vec3 c, Vec3 otherHalf, Mat3 otherR, Vec3 otherC)
        {
            double best = double.PositiveInfinity;
            const int samples = 200;

            for (int axis = 0; axis < 3; axis++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    double u = (corner & 1) == 0 ? 1 : -1;
                    double v = (corner & 2) == 0 ? 1 : -1;

                    Vec3 EdgePoint(double t)
                    {
                        Vec3 local = axis == 0 ? new Vec3(t * half.X, u * half.Y, v * half.Z)
                                   : axis == 1 ? new Vec3(u * half.X, t * half.Y, v * half.Z)
                                   : new Vec3(u * half.X, v * half.Y, t * half.Z);
                        return c + r * local;
                    }

                    // The coarse scan, then the bracket around its best.
                    double bestT = -1, bestD = double.PositiveInfinity;
                    for (int s = 0; s <= samples; s++)
                    {
                        double t = -1.0 + 2.0 * s / samples;
                        double d = PointToBox(EdgePoint(t), otherHalf, otherR, otherC);
                        if (d < bestD) { bestD = d; bestT = t; }
                    }

                    double lo = Math.Max(-1, bestT - 2.0 / samples), hi = Math.Min(1, bestT + 2.0 / samples);
                    for (int it = 0; it < 80; it++)
                    {
                        double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                        if (PointToBox(EdgePoint(m1), otherHalf, otherR, otherC) <
                            PointToBox(EdgePoint(m2), otherHalf, otherR, otherC)) hi = m2; else lo = m1;
                    }

                    best = Math.Min(best, Math.Min(bestD, PointToBox(EdgePoint(0.5 * (lo + hi)), otherHalf, otherR, otherC)));
                }
            }

            return best;
        }
    }
}
