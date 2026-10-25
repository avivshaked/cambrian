using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What kind of core a part's shape reduces to (<see cref="ConvexCore.Of"/>).
    /// </summary>
    public enum CoreKind
    {
        /// <summary>A sphere: the point at its centre.</summary>
        Point,

        /// <summary>A capsule: a segment along its local Y.</summary>
        Segment,

        /// <summary>A box: the box less its smallest half-extent on every side. A flat plate's is its rectangle.</summary>
        Box,

        /// <summary>
        /// A family part's: the convex hull of the 98 points of its contact core (<see cref="Superellipsoid.ContactCore(Float3, out double, out double, out double)"/>),
        /// held in a flat array of three doubles a point beside the link's zero half-extents.
        /// </summary>
        Hull,
    }

    /// <summary>
    /// A part as a convex core with a margin, and the distance between two cores by GJK — D228,
    /// <c>logbook/specs/d228-d229-build-plan.md</c> sections 3 and 4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One representation for all three shapes.</b> A core is a box of half-extents <c>(hx, hy, hz)</c> in
    /// the part's own frame with any of them 0: all 0 is a point, one nonzero is a segment, two a
    /// rectangle. Its support function is then one line, whatever the shape, and the part is the core
    /// swept by a ball of the margin's radius. The ball of the part's half-diagonal stays the broadphase,
    /// and <see cref="Creature.LinkReach"/> bounds every point of core plus margin (a test holds that).
    /// </para>
    /// <para>
    /// <b>Small, branch-light and allocation-free</b>, so that the card's kernel can take it line for line:
    /// the routines work on <see cref="Vec3"/> and <see cref="Mat3"/> by value, and GJK keeps its simplex in
    /// one stack buffer of 40 doubles.
    /// </para>
    /// <para>
    /// <b>GJK, as a distance routine.</b> The simplex holds up to four points of the Minkowski difference
    /// <c>A - B</c>, each with the points of A and B that made it. Each pass takes the point <c>v</c> of the
    /// simplex's hull nearest the origin (<see cref="Reduce"/>), drops the vertices the nearest point does
    /// not use, asks for the difference's support point <c>w</c> along <c>-v</c>, and adds it.
    /// </para>
    /// <para>
    /// <b>Termination, any one of:</b> (1) <c>|v|</c> is at most <see cref="Touching"/>, 1 nm: the cores
    /// intersect or touch, and the routine says so. (2) <c>w</c> is a vertex already held. (3) The gap
    /// <c>|v|^2 - v.w</c>, which is never negative in exact arithmetic and bounds the distance's error by
    /// <c>gap / |v|</c>, is at most <see cref="Tolerance"/> times <c>|v|</c>: the distance is known to 0.1 nm.
    /// (4) The pass count reaches <see cref="MaxIterations"/>, 32. Cores are polytopes, whose support
    /// points are corners, so (3) or (2) is reached in a handful of passes and the cap is a backstop.
    /// </para>
    /// <para>
    /// <b>Degenerate simplices.</b> A core with a zero extent makes the difference flat or a line. A
    /// collinear triangle (its normal's square under 1e-20 of its edges' squares' product) is closed on its
    /// three edges instead of its interior; a tetrahedron whose test plane has the fourth point on it
    /// (a flat tetrahedron) counts every face as a candidate and takes the nearest; and a tetrahedron with
    /// the origin inside every face's plane is the intersection verdict. Deterministic: no randomness, no
    /// allocation, a fixed order of every test.
    /// </para>
    /// </remarks>
    public static class ConvexCore
    {
        /// <summary>The most passes GJK makes.</summary>
        public const int MaxIterations = 32;

        /// <summary>The distance error at which GJK stops, metres.</summary>
        public const double Tolerance = 1e-10;

        /// <summary>A distance at or under this is an intersection, metres.</summary>
        public const double Touching = 1e-9;

        // The simplex buffer: the difference's points, then A's, then B's, three doubles a vertex, four
        // vertices; then the four weights of the nearest point.
        private const int W = 0, A = 12, B = 24, L = 36;

        /// <summary>
        /// A part's core and margin from its shape and half-extents (the plan, section 3). A shape other
        /// than the three standard ones is refused by name.
        /// </summary>
        /// <param name="shape">The part's shape.</param>
        /// <param name="h">Its half-extents.</param>
        /// <param name="half">The core's half-extents in the part's frame.</param>
        /// <param name="margin">The ball swept round the core, metres.</param>
        public static CoreKind Of(PartShape shape, Float3 h, out Vec3 half, out double margin)
        {
            switch (shape)
            {
                case SphereShape _:
                    half = Vec3.Zero;
                    margin = SphereShape.Radius(h);
                    return CoreKind.Point;

                case CapsuleShape _:
                    half = new Vec3(0, CapsuleShape.HalfSpan(h), 0);
                    margin = CapsuleShape.Radius(h);
                    return CoreKind.Segment;

                case BoxShape _:
                {
                    double ax = Math.Abs((double)h.X), ay = Math.Abs((double)h.Y), az = Math.Abs((double)h.Z);
                    double m = ax < ay ? (ax < az ? ax : az) : (ay < az ? ay : az);
                    half = new Vec3(ax - m, ay - m, az - m);
                    margin = m;
                    return CoreKind.Box;
                }
            }

            throw new ArgumentException(
                "The true-shape contact (D228) has a core for the sphere, the capsule and the box, and the " +
                "part's shape is '" + (shape == null ? "null" : shape.Id) + "'.", nameof(shape));
        }

        /// <summary>
        /// A family part's core and margin (D233), written into <paramref name="hull"/> without allocating:
        /// the part's <see cref="FamilyGeometry"/> core at the reference size, each point times
        /// <see cref="PhenotypePart.FamilyScale"/>, and the margin likewise. The half-extents are 0, since a
        /// hull is not a box.
        /// </summary>
        /// <param name="part">A part with a <see cref="PhenotypePart.Family"/>.</param>
        /// <param name="hull">Three doubles for each of <see cref="Superellipsoid.ContactCorePoints"/> points.</param>
        public static CoreKind OfFamily(PhenotypePart part, double[] hull, out Vec3 half, out double margin)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (part.Family == null)
            {
                throw new ArgumentException("A hull core is for a part with a family geometry.", nameof(part));
            }

            if (hull == null || hull.Length != 3 * Superellipsoid.ContactCorePoints)
            {
                throw new ArgumentException(
                    "A hull holds three doubles for each of " + Superellipsoid.ContactCorePoints + " points.", nameof(hull));
            }

            double scale = part.FamilyScale;
            double[] reference = part.Family.ReferenceCorePoints;
            for (int k = 0; k < hull.Length; k++) hull[k] = reference[k] * scale;

            half = Vec3.Zero;
            margin = part.Family.ReferenceCoreMargin * scale;
            return CoreKind.Hull;
        }

        /// <summary>
        /// The core's farthest point along <paramref name="direction"/> in the world frame. A local
        /// component of the direction that is 0 (or not a number) reads as +.
        /// </summary>
        /// <param name="half">The core's half-extents in its own frame.</param>
        /// <param name="rotation">The core's world turn, row-major.</param>
        /// <param name="centre">The core's centre in the world.</param>
        /// <param name="direction">Any nonzero direction; need not be a unit vector.</param>
        public static Vec3 Support(Vec3 half, Mat3 rotation, Vec3 centre, Vec3 direction)
        {
            // The direction in the core's frame, R^T d.
            Vec3 local = rotation.TransposedTimes(direction);

            Vec3 corner = new Vec3(
                local.X < 0 ? -half.X : half.X,
                local.Y < 0 ? -half.Y : half.Y,
                local.Z < 0 ? -half.Z : half.Z);

            return centre + rotation * corner;
        }

        /// <summary>
        /// The core's farthest point along <paramref name="direction"/> when the core may be a hull: with a
        /// null <paramref name="hull"/> it is the box overload, line for line; with one it is the hull point
        /// of the largest dot product with the direction taken in the core's frame, the first of the fixed
        /// order winning a tie (and the first point when the direction is not a number), then turned and moved.
        /// </summary>
        /// <param name="half">The box core's half-extents; not read when there is a hull.</param>
        /// <param name="hull">Three doubles a point, or null.</param>
        public static Vec3 Support(Vec3 half, double[] hull, Mat3 rotation, Vec3 centre, Vec3 direction)
        {
            if (hull == null) return Support(half, rotation, centre, direction);

            Vec3 local = rotation.TransposedTimes(direction);

            int best = 0;
            double bestDot = local.X * hull[0] + local.Y * hull[1] + local.Z * hull[2];
            for (int k = 3; k + 2 < hull.Length; k += 3)
            {
                double dot = local.X * hull[k] + local.Y * hull[k + 1] + local.Z * hull[k + 2];
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = k;
                }
            }

            return centre + rotation * new Vec3(hull[best], hull[best + 1], hull[best + 2]);
        }

        /// <summary>
        /// The distance between two cores and the nearest point of each, or false when they intersect or
        /// touch (the distance is then at most <see cref="Touching"/>, and the points are the last the
        /// routine held).
        /// </summary>
        /// <returns>True when the cores are apart by more than <see cref="Touching"/>.</returns>
        public static bool Distance(
            Vec3 halfA, Mat3 rotationA, Vec3 centreA,
            Vec3 halfB, Mat3 rotationB, Vec3 centreB,
            out double distance, out Vec3 pointA, out Vec3 pointB) =>
            Distance(
                halfA, null, rotationA, centreA,
                halfB, null, rotationB, centreB,
                out distance, out pointA, out pointB);

        /// <summary>
        /// <see cref="Distance(Vec3, Mat3, Vec3, Vec3, Mat3, Vec3, out double, out Vec3, out Vec3)"/> with
        /// each side's core possibly a hull (null for a box core). The one path: the box overload calls this
        /// with two nulls.
        /// </summary>
        /// <remarks>
        /// <b>GJK on a hull.</b> The routine asks a core for its support point only, so a polytope with 98
        /// vertices needs nothing else. Termination (1), the nearest point of the simplex within
        /// <see cref="Touching"/> of the origin, reads the simplex alone. (2), a support point already held, is an
        /// exact comparison of the difference's three numbers, and the hull's support point is a deterministic
        /// function of the direction and the turn (the first of the largest dot products), so the same feature
        /// returns the same bits. (3), the gap <c>|v|^2 - v.w</c> against <see cref="Tolerance"/> times <c>|v|</c>,
        /// holds for any convex set, since <c>w</c> is its support point along <c>-v</c>. A hull of 98 points
        /// that approximates a ball has more nearby features than a box's eight corners, so it may take more
        /// passes to close; (4), the cap of <see cref="MaxIterations"/>, ends the routine with the nearest point
        /// of the last simplex, which is at least the true distance, so a penetration read from it is never too
        /// large. A hull whose points are all nearly one point (an even ball) closes at once by (3).
        /// </remarks>
        public static bool Distance(
            Vec3 halfA, double[] hullA, Mat3 rotationA, Vec3 centreA,
            Vec3 halfB, double[] hullB, Mat3 rotationB, Vec3 centreB,
            out double distance, out Vec3 pointA, out Vec3 pointB)
        {
            Span<double> s = stackalloc double[40];

            // Each core contains its own centre, so coincident centres are an intersection.
            Vec3 first = centreB - centreA;
            if (first.SqrMagnitude <= Touching * Touching)
            {
                distance = 0;
                pointA = centreA;
                pointB = centreB;
                return false;
            }

            int n = 0;
            AddVertex(s, ref n,
                Support(halfA, hullA, rotationA, centreA, first),
                Support(halfB, hullB, rotationB, centreB, -first));

            // The point nearest the origin of the simplex's hull; the first vertex alone to begin with.
            s[L] = 1.0;
            Vec3 v = Vertex(s, W, 0);
            bool inside = false;

            int pass = 0;
            for (; pass < MaxIterations; pass++)
            {
                if (!Reduce(s, ref n, out v)) { inside = true; break; }

                double vv = v.SqrMagnitude;
                if (vv <= Touching * Touching) break;

                // The difference's support point along -v: A's along -v, B's along +v.
                Vec3 wa = Support(halfA, hullA, rotationA, centreA, -v);
                Vec3 wb = Support(halfB, hullB, rotationB, centreB, v);
                Vec3 w = wa - wb;

                // No progress possible: the support point is a vertex already held, or the gap is closed.
                if (vv - Vec3.Dot(v, w) <= Tolerance * Math.Sqrt(vv)) break;
                if (Held(s, n, w)) break;

                AddVertex(s, ref n, wa, wb);
            }

            // The cap was reached with a vertex added and not yet reduced: reduce it, so the weights are current.
            if (pass == MaxIterations && !Reduce(s, ref n, out v)) inside = true;

            if (inside)
            {
                distance = 0;
                pointA = centreA;
                pointB = centreB;
                return false;
            }

            // The nearest points are the weights' combination of A's and B's.
            Vec3 pa = Vec3.Zero, pb = Vec3.Zero;
            for (int k = 0; k < n; k++)
            {
                double lam = s[L + k];
                pa += Vertex(s, A, k) * lam;
                pb += Vertex(s, B, k) * lam;
            }

            pointA = pa;
            pointB = pb;
            distance = (pa - pb).Magnitude;
            return distance > Touching;
        }

        // ------------------------------------------------------------ the simplex

        private static Vec3 Vertex(Span<double> s, int block, int k) =>
            new Vec3(s[block + 3 * k], s[block + 3 * k + 1], s[block + 3 * k + 2]);

        private static void Put(Span<double> s, int block, int k, Vec3 p)
        {
            s[block + 3 * k] = p.X;
            s[block + 3 * k + 1] = p.Y;
            s[block + 3 * k + 2] = p.Z;
        }

        private static void AddVertex(Span<double> s, ref int n, Vec3 pa, Vec3 pb)
        {
            Put(s, A, n, pa);
            Put(s, B, n, pb);
            Put(s, W, n, pa - pb);
            n++;
        }

        private static bool Held(Span<double> s, int n, Vec3 w)
        {
            for (int k = 0; k < n; k++)
            {
                if (s[W + 3 * k] == w.X && s[W + 3 * k + 1] == w.Y && s[W + 3 * k + 2] == w.Z) return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the point of the simplex's hull nearest the origin, writes its weights, and drops every
        /// vertex it does not use. False when the simplex is a tetrahedron holding the origin.
        /// </summary>
        private static bool Reduce(Span<double> s, ref int n, out Vec3 v)
        {
            if (n == 1)
            {
                s[L] = 1.0;
            }
            else if (n == 2)
            {
                Segment(Vertex(s, W, 0), Vertex(s, W, 1), out double a, out double b);
                s[L] = a;
                s[L + 1] = b;
            }
            else if (n == 3)
            {
                Triangle(Vertex(s, W, 0), Vertex(s, W, 1), Vertex(s, W, 2),
                    out double a, out double b, out double c);
                s[L] = a;
                s[L + 1] = b;
                s[L + 2] = c;
            }
            else
            {
                if (!Tetrahedron(s)) { v = Vec3.Zero; return false; }
            }

            // Keep the vertices the nearest point uses, in order.
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                double lam = s[L + k];
                if (!(lam > 0)) continue;

                if (m != k)
                {
                    Put(s, W, m, Vertex(s, W, k));
                    Put(s, A, m, Vertex(s, A, k));
                    Put(s, B, m, Vertex(s, B, k));
                    s[L + m] = lam;
                }
                m++;
            }
            n = m;

            Vec3 sum = Vec3.Zero;
            for (int k = 0; k < n; k++) sum += Vertex(s, W, k) * s[L + k];
            v = sum;
            return true;
        }

        // ------------------------------------------------------------ nearest point of a simplex to the origin

        /// <summary>The weights of the point of segment a-b nearest the origin.</summary>
        private static void Segment(Vec3 a, Vec3 b, out double la, out double lb)
        {
            Vec3 ab = b - a;
            double len2 = Vec3.Dot(ab, ab);
            double t = len2 > 0 ? -Vec3.Dot(a, ab) / len2 : 0.0;

            if (!(t > 0)) { la = 1.0; lb = 0.0; }
            else if (t >= 1.0) { la = 0.0; lb = 1.0; }
            else { la = 1.0 - t; lb = t; }
        }

        /// <summary>
        /// The weights of the point of triangle a-b-c nearest the origin: the Voronoi regions of the
        /// triangle's vertices, edges and face, as Ericson's closest point on a triangle. A triangle with
        /// no area is closed on its edges.
        /// </summary>
        private static void Triangle(Vec3 a, Vec3 b, Vec3 c, out double la, out double lb, out double lc)
        {
            Vec3 ab = b - a, ac = c - a;
            Vec3 normal = Vec3.Cross(ab, ac);

            if (normal.SqrMagnitude <= 1e-20 * Vec3.Dot(ab, ab) * Vec3.Dot(ac, ac) || !(Vec3.Dot(ab, ab) > 0))
            {
                Edges(a, b, c, out la, out lb, out lc);
                return;
            }

            // The origin's offsets from a, b and c.
            double d1 = -Vec3.Dot(ab, a), d2 = -Vec3.Dot(ac, a);
            if (d1 <= 0 && d2 <= 0) { la = 1; lb = 0; lc = 0; return; }

            double d3 = -Vec3.Dot(ab, b), d4 = -Vec3.Dot(ac, b);
            if (d3 >= 0 && d4 <= d3) { la = 0; lb = 1; lc = 0; return; }

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                double t = d1 / (d1 - d3);
                la = 1 - t; lb = t; lc = 0;
                return;
            }

            double d5 = -Vec3.Dot(ab, c), d6 = -Vec3.Dot(ac, c);
            if (d6 >= 0 && d5 <= d6) { la = 0; lb = 0; lc = 1; return; }

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                double t = d2 / (d2 - d6);
                la = 1 - t; lb = 0; lc = t;
                return;
            }

            double va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            {
                double t = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                la = 0; lb = 1 - t; lc = t;
                return;
            }

            double denom = 1.0 / (va + vb + vc);
            double bv = vb * denom, cv = vc * denom;
            la = 1.0 - bv - cv; lb = bv; lc = cv;
        }

        /// <summary>The nearest point of a triangle taken on its three edges, in the order ab, bc, ca.</summary>
        private static void Edges(Vec3 a, Vec3 b, Vec3 c, out double la, out double lb, out double lc)
        {
            Segment(a, b, out double p, out double q);
            double best = (a * p + b * q).SqrMagnitude;
            la = p; lb = q; lc = 0;

            Segment(b, c, out p, out q);
            double d = (b * p + c * q).SqrMagnitude;
            if (d < best) { best = d; la = 0; lb = p; lc = q; }

            Segment(c, a, out p, out q);
            d = (c * p + a * q).SqrMagnitude;
            if (d < best) { la = q; lb = 0; lc = p; }
        }

        /// <summary>
        /// Writes the weights of the nearest point of the tetrahedron in the buffer's four vertices.
        /// False when the origin is inside it. A face is a candidate when the origin is on the far side of
        /// its plane from the fourth vertex, or when that vertex is on the plane (a flat tetrahedron).
        /// </summary>
        private static bool Tetrahedron(Span<double> s)
        {
            Vec3 p0 = Vertex(s, W, 0), p1 = Vertex(s, W, 1), p2 = Vertex(s, W, 2), p3 = Vertex(s, W, 3);

            double best = double.PositiveInfinity;
            double w0 = 0, w1 = 0, w2 = 0, w3 = 0;
            bool any = false;

            // Faces (0,1,2) | 3, (0,2,3) | 1, (0,3,1) | 2, (1,3,2) | 0.
            for (int face = 0; face < 4; face++)
            {
                Vec3 a, b, c, d;
                if (face == 0) { a = p0; b = p1; c = p2; d = p3; }
                else if (face == 1) { a = p0; b = p2; c = p3; d = p1; }
                else if (face == 2) { a = p0; b = p3; c = p1; d = p2; }
                else { a = p1; b = p3; c = p2; d = p0; }

                Vec3 normal = Vec3.Cross(b - a, c - a);
                double signOrigin = -Vec3.Dot(a, normal);
                double signFourth = Vec3.Dot(d - a, normal);
                if (signOrigin * signFourth > 0) continue;

                Triangle(a, b, c, out double la, out double lb, out double lc);
                double dist = (a * la + b * lb + c * lc).SqrMagnitude;
                if (!(dist < best))
                {
                    any = true;
                    continue;
                }

                best = dist;
                any = true;
                if (face == 0) { w0 = la; w1 = lb; w2 = lc; w3 = 0; }
                else if (face == 1) { w0 = la; w1 = 0; w2 = lb; w3 = lc; }
                else if (face == 2) { w0 = la; w1 = lc; w2 = 0; w3 = lb; }
                else { w0 = 0; w1 = la; w2 = lc; w3 = lb; }
            }

            if (!any) return false;

            s[L] = w0;
            s[L + 1] = w1;
            s[L + 2] = w2;
            s[L + 3] = w3;
            return true;
        }
    }
}
