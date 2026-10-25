using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Per-part contact on the true shape — D228, <c>logbook/specs/d228-d229-build-plan.md</c> sections 3
    /// and 4. <see cref="Contacts.ApplyPerPart"/>'s law with the narrowphase changed: a pair of parts is
    /// tested as two convex cores with margins by GJK, and the bed, the reefs, the coral and the glass are
    /// tested on the core's support point toward the surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The broadphase is the old one.</b> The ball of each part's half-diagonal (<see cref="Creature.LinkReach"/>)
    /// is still what the grid enters, still the first test of a pair, and still the early-out of every
    /// surface: where it finds no push there is none, because core plus margin lies within it.
    /// </para>
    /// <para>
    /// <b>Exactly opposite pairs.</b> Both parts of a pair are computed with the one of the lower
    /// <c>(body id, link index)</c> as the first core, from the same committed numbers in the same order,
    /// and the other side takes the negation. So the two normals, penetrations and contact points are equal
    /// to the bit, whichever body's loop does the work and at any thread count.
    /// </para>
    /// <para>
    /// <b>Two levels of the switch.</b> At <see cref="ContactShapeForm.TrueShape"/> the force acts at the
    /// link's centre as a linear force, with no torque, and the damper reads the committed centre velocity.
    /// At <see cref="ContactShapeForm.TrueShapeAtPoint"/> it acts at the touch's contact point: the lever
    /// <c>r</c> is the point less the link's committed centre, the damper reads the point's velocity
    /// <c>v + spin x r</c> against the other side's at the same point (0 for a surface), the force goes in
    /// as a linear force as before and the torque <c>r x F</c>, capped by <see cref="Anchor.CapTorque"/>
    /// touch by touch, goes into the link's torque. Every line of the first level is unchanged at it.
    /// </para>
    /// <para>
    /// <b>Every vertex of a core.</b> At the second level a link touches the bed, a reef, the coral or the glass
    /// at every distinct vertex of its core (1, 2 or 4 of them), each with its own spring and damper (not
    /// divided among them), and not at the one support point, which would put a resting plate's whole weight on one corner and
    /// rock it from corner to corner. Pairs between bodies keep their single GJK point. A surface hands back the
    /// vertices' summed push, their push-weighted contact point and the moment <c>sum (p_k - centre) x F_k</c>; the
    /// friction is computed once from the sum, and the touch's torque is capped once.
    /// </para>
    /// <para>
    /// <b>Each vertex's spring and damper read the mass at that point.</b> A corner of a turning body is lighter
    /// than its centre: along the touch's normal <c>n</c> a push at the lever <c>r</c> meets
    /// <c>m_eff = 1 / (1/m + (r x n) . I^-1 (r x n))</c> (<see cref="EffectiveMass"/>), about a third of <c>m</c>
    /// for a plate's corner, and two such dampers on an edge together passed the explicit damper's limit of 2 in
    /// <c>c dt / m_eff</c>. So a vertex's stiffness is <c>m_eff w^2</c>, its damping <c>2 z m_eff w</c>, and the mass
    /// handed to <see cref="ContactLaw.PairPush"/> is <c>m_eff</c>. The friction keeps the link's mass. The single
    /// support point of <see cref="ContactShapeForm.TrueShape"/> and every pair keep <c>m</c>.
    /// </para>
    /// <para>
    /// <b>A pair's frame.</b> A pair's contact point is kept in the pair's own frame, the first core's centre
    /// at the origin and the second's at minus the sphere test's difference. A lever is the point less that
    /// side's centre in that frame, so it is the same number whichever body's loop does the work.
    /// </para>
    /// <para>
    /// <b>Not the recorded path.</b> The recorded sphere path is <see cref="Contacts"/> and is untouched:
    /// this class runs only when <see cref="SolverConfig.ContactShape"/> is the true shape.
    /// </para>
    /// </remarks>
    public static class ContactTrueShape
    {
        /// <summary>
        /// Whether two parts touch on their true shapes, and where: the pair routine, computed with
        /// part A first (the one of the lower body id and link). A's normal is the returned one; B's is
        /// its negation.
        /// </summary>
        /// <param name="centreB">
        /// B's centre in A's frame, already moved the short way round a seam by the caller.
        /// <see cref="Apply"/> passes A at the origin and B at minus the sphere test's difference.
        /// </param>
        /// <param name="radiusA">A's bounding radius, for the deep case.</param>
        /// <param name="normalAB">Unit normal from B's core toward A's.</param>
        /// <param name="penetration">The margins less the cores' distance; at least the margins' sum when the cores meet.</param>
        /// <param name="point">The contact point, in the world.</param>
        /// <returns>False when the penetration is not positive.</returns>
        internal static bool Pair(
            Vec3 centreA, Mat3 rotationA, Vec3 halfA, double marginA, double radiusA,
            Vec3 centreB, Mat3 rotationB, Vec3 halfB, double marginB, double radiusB,
            out Vec3 normalAB, out double penetration, out Vec3 point)
        {
            if (ConvexCore.Distance(
                halfA, rotationA, centreA, halfB, rotationB, centreB,
                out double d, out Vec3 pa, out Vec3 pb))
            {
                // Apart: along the closest points, from B toward A.
                Vec3 gap = pa - pb;
                normalAB = gap * (1.0 / d);
                penetration = marginA + marginB - d;

                // Each surface point is the closest core point moved out by its margin toward the other.
                point = ((pa - normalAB * marginA) + (pb + normalAB * marginB)) * 0.5;
                return penetration > 0;
            }

            // The cores meet: the centres' difference, as today, and a push that never weakens as the pair
            // goes deeper than the margins' sum.
            Vec3 between = centreA - centreB;
            double distance = between.Magnitude;
            normalAB = distance > 1e-9 ? between * (1.0 / distance) : Vec3.UnitY;

            double sum = marginA + marginB;
            double deep = radiusA + radiusB - distance;
            penetration = deep > sum ? deep : sum;

            // The point between the centres at the ratio of the margins.
            double along = sum > 0 ? marginA / sum : 0.5;
            point = centreA - between * along;
            return penetration > 0;
        }

        /// <summary>One link's core support point along a direction, in the world.</summary>
        internal static Vec3 CoreSupport(Creature body, int link, Vec3 centre, Vec3 direction) =>
            ConvexCore.Support(
                Vec3.Read(body.LinkCoreHalf, 3 * link),
                Mat3.Read(body.LinkContactRotation, 9 * link),
                centre, direction);

        /// <summary>
        /// The per-part contact pass on the true shape: <see cref="Contacts.ApplyPerPart"/>'s force law, with
        /// the narrowphase of the class remarks.
        /// </summary>
        internal static void Apply(
            Creature body, int index, ContactGrid grid, SolverConfig config, ref int[] scratch)
        {
            if (!grid.PerPart || !body.LinkContactActive || body.LinkContactRotation == null)
            {
                throw new InvalidOperationException(
                    "True-shape contact was asked of a grid built over body spheres, or of a body built " +
                    "without link spheres and turns. Both are set from SolverConfig.ContactPerPart and " +
                    "ContactShape, which have to be set before the first creature is built and not changed after.");
            }

            bool instrument = config.ContactInstrument;
            if (instrument) body.BeginContactStep();

            bool touch = body.RockTouched != null;

            // The level that acts at the contact point. At the other level nothing below is read for it: the
            // spin is zero, the torque is never summed and Fext's torque rows are not written.
            bool atPoint = config.ContactShape.AtPoint();

            int first = grid.FirstRow(index);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 centre = Vec3.Read(body.LinkContactCentre, 3 * i);
                double radius = body.LinkContactRadius[i];
                Vec3 velocity = Vec3.Read(body.LinkContactVelocity, 3 * i);
                double mass = body.Mass[i];
                double margin = body.LinkMargin[i];

                Vec3 spin = atPoint ? Vec3.Read(body.LinkContactSpin, 3 * i) : Vec3.Zero;
                double inertia = atPoint ? body.SmallestInertia[i] : 0;

                Vec3 force = Vec3.Zero;
                Vec3 torque = Vec3.Zero;
                double rockPush = 0;
                if (touch) body.RockTouched[i] = false;

                if (config.CreatureContact)
                {
                    int n = grid.NeighbourLinks(first + i, ref scratch);
                    grid.Watch.NoteCandidates(n);
                    for (int k = 0; k < n; k++)
                    {
                        int row = scratch[k];
                        Creature other = grid.At(grid.RowBody(row));
                        if (!other.ContactActive) continue;

                        int j = grid.RowLink(row);

                        // The canonical order: the lower (body id, link) is the first core.
                        bool firstIsThis = body.Id < other.Id || (body.Id == other.Id && i < j);

                        Vec3 otherCentre = Vec3.Read(other.LinkContactCentre, 3 * j);
                        double otherRadius = other.LinkContactRadius[j];

                        Vec3 centreA = firstIsThis ? centre : otherCentre;
                        Vec3 centreB = firstIsThis ? otherCentre : centre;
                        double radiusA = firstIsThis ? radius : otherRadius;
                        double radiusB = firstIsThis ? otherRadius : radius;

                        // The broadphase: today's sphere test, read in the canonical order.
                        Vec3 between = centreA - centreB;
                        if (config.Seamed) between = Contacts.ShortWay(between, config);
                        double distance = between.Magnitude;
                        if (radiusA + radiusB - distance <= 0) continue;

                        Creature bodyA = firstIsThis ? body : other;
                        Creature bodyB = firstIsThis ? other : body;
                        int linkA = firstIsThis ? i : j;
                        int linkB = firstIsThis ? j : i;

                        // The narrowphase runs in the pair's own frame: the first core at the origin and the
                        // second at minus the sphere test's difference. No absolute coordinate enters GJK, so
                        // the card's kernel, which carries its centres as a float and a rest, takes the same
                        // numbers (src/Evosim.Farm.Gpu/CLAUDE.md), and the double kernel matches this to the bit.
                        if (!Pair(
                            Vec3.Zero, Mat3.Read(bodyA.LinkContactRotation, 9 * linkA),
                            Vec3.Read(bodyA.LinkCoreHalf, 3 * linkA), bodyA.LinkMargin[linkA], radiusA,
                            Vec3.Zero - between, Mat3.Read(bodyB.LinkContactRotation, 9 * linkB),
                            Vec3.Read(bodyB.LinkCoreHalf, 3 * linkB), bodyB.LinkMargin[linkB], radiusB,
                            out Vec3 normalAB, out double penetration, out Vec3 pairPoint))
                        {
                            continue;
                        }

                        if (instrument) body.NoteOverlapPart(other.Id, i, j);

                        Vec3 normal = firstIsThis ? normalAB : -normalAB;

                        double otherMass = other.Mass[j];
                        double reduced = mass * otherMass / (mass + otherMass);

                        double stiffness = reduced * config.ContactOmega * config.ContactOmega;
                        double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

                        // Each side's centre in the pair's frame, and so each side's lever to pairPoint (also in it).
                        // The same four numbers whichever body's loop does the work.
                        Vec3 minusBetween = Vec3.Zero - between;
                        Vec3 lever = pairPoint - (firstIsThis ? Vec3.Zero : minusBetween);

                        Vec3 otherVelocity = Vec3.Read(other.LinkContactVelocity, 3 * j);
                        Vec3 thisVelocity = velocity;
                        if (atPoint)
                        {
                            Vec3 otherLever = pairPoint - (firstIsThis ? minusBetween : Vec3.Zero);
                            thisVelocity = VelocityAt(velocity, spin, lever, true);
                            otherVelocity = VelocityAt(otherVelocity, Vec3.Read(other.LinkContactSpin, 3 * j), otherLever, true);
                        }

                        double approach = Vec3.Dot(thisVelocity - otherVelocity, normal);

                        Vec3 push = normal * ContactLaw.PairPush(
                            stiffness * penetration - damping * approach,
                            reduced, approach, config);

                        force += push;
                        if (atPoint) torque += TouchTorque(lever, push, inertia, radius, config);
                    }
                }

                double bedStiffness = mass * config.ContactOmega * config.ContactOmega;
                double bedDamping = 2.0 * config.ContactDampingRatio * mass * config.ContactOmega;

                // The bed. The bounding sphere is the early-out; the support point toward the rock is the test.
                // Each surface hands back its push with the support point it was read at and the contact point
                // (the point is where the level that acts at the contact point puts the force).
                if (config.Bed == null)
                {
                    Vec3 up = FlatBedTouch(
                        body, i, centre, radius, margin, velocity, mass, config, bedStiffness, bedDamping,
                        out Vec3 support, out Vec3 bedPoint, out Vec3 bedMoment, spin, atPoint);

                    if (up.Y > 0)
                    {
                        Rock(up, bedPoint, bedMoment, centre, velocity, spin, atPoint, mass, inertia, radius, config, ref force, ref torque);

                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch) NoteRock(body, i, support, margin, up, ref rockPush);
                    }
                }
                else
                {
                    Vec3 rock = HeightBedTouch(
                        body, i, centre, radius, margin, velocity, mass, config, bedStiffness, bedDamping,
                        out Vec3 support, out Vec3 bedPoint, out Vec3 bedMoment, spin, atPoint);

                    if (rock.X != 0 || rock.Y != 0 || rock.Z != 0)
                    {
                        Rock(rock, bedPoint, bedMoment, centre, velocity, spin, atPoint, mass, inertia, radius, config, ref force, ref torque);
                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch) NoteRock(body, i, support, margin, rock, ref rockPush);
                    }
                }

                // The reefs and the coral.
                if (config.Reefs != null || config.Coral != null)
                {
                    Vec3 reef = ReefTouch(
                        body, i, centre, radius, margin, velocity, mass, config, bedStiffness, bedDamping,
                        out Vec3 support, out Vec3 reefPoint, out Vec3 reefMoment, spin, atPoint);

                    if (reef.X != 0 || reef.Y != 0 || reef.Z != 0)
                    {
                        Rock(reef, reefPoint, reefMoment, centre, velocity, spin, atPoint, mass, inertia, radius, config, ref force, ref torque);
                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch) NoteRock(body, i, support, margin, reef, ref rockPush);
                    }
                }

                // The glass. Not rock: it sets no touch.
                if (config.TankRadiusMetres > 0)
                {
                    Vec3 glass = GlassTouch(
                        body, i, centre, radius, margin, velocity, mass, config, bedStiffness, bedDamping,
                        out Vec3 support, out Vec3 glassPoint, out Vec3 glassMoment, spin, atPoint);

                    if (glass.X != 0 || glass.Y != 0 || glass.Z != 0)
                    {
                        Rock(glass, glassPoint, glassMoment, centre, velocity, spin, atPoint, mass, inertia, radius, config, ref force, ref torque);

                        if (instrument) body.TouchedBedOrGlass = true;
                    }
                }

                force = ContactLaw.BodyPush(force, mass, config);

                if (force.X != 0 || force.Y != 0 || force.Z != 0)
                {
                    // A one-link body's force is spread by m (1/M), as the body sphere's is; its torque likewise.
                    double share = body.Links == 1 ? body.Mass[0] * (1.0 / body.TotalMass) : 1.0;

                    Vec3 applied = body.Links == 1 ? force * share : force;

                    Vec3.Add(body.Fext, 6 * i + 3, applied);

                    // The torques are summed already capped, touch by touch. BodyPush bounds the force and the
                    // cap bounds each torque; the one does not scale the other.
                    if (atPoint) Vec3.Add(body.Fext, 6 * i, body.Links == 1 ? torque * share : torque);
                }
            }
        }

        // ------------------------------------------------------------ the world's surfaces on the true shape
        //
        // Each takes the bounding sphere as the early-out (where it finds no push there is none), then the
        // direction to the surface at the link's centre, then the core's support point along it, and runs the
        // recorded routine for that surface on a sphere at the support point with the margin for its radius.
        // Each returns the push (zero when there is none), the support point it was read at, and the contact
        // point: the support point moved along the direction by the margin.
        //
        // At the level that acts at the contact point (atPoint) each reads the damper's velocity at that point,
        // v + spin x (point - centre), and the geometric early-out reads none: the bounding sphere is tested
        // with a still link, so that it vetoes only what the geometry vetoes and not a damper that the point's
        // own velocity would have turned. With atPoint false the velocity is the centre's, the early-out reads
        // it as the level at the centre always did, and every line is that level's.

        /// <summary>
        /// The velocity of the point at <paramref name="lever"/> from a link's centre: <c>v + spin x r</c>
        /// when <paramref name="atPoint"/>, the centre's velocity otherwise (no spin is read).
        /// </summary>
        internal static Vec3 VelocityAt(Vec3 velocity, Vec3 spin, Vec3 lever, bool atPoint) =>
            atPoint ? velocity + Vec3.Cross(spin, lever) : velocity;

        /// <summary>One touch's torque about the link's centre: <c>r x F</c>, capped by <see cref="Anchor.CapTorque"/>.</summary>
        internal static Vec3 TouchTorque(Vec3 lever, Vec3 force, double inertia, double radius, SolverConfig config) =>
            Anchor.CapTorque(Vec3.Cross(lever, force), inertia, radius, config);

        /// <summary>
        /// One touch of the bed, a reef, the coral or the glass: its push into the link's force, D137's friction
        /// on it (computed once, from the push, read at the contact point's velocity when
        /// <paramref name="atPoint"/>) and, at that level, the torque of the touch about the centre, capped once:
        /// the vertices' <paramref name="moment"/> <c>sum (p_k - centre) x F_k</c> plus the friction's own lever
        /// (the contact point less the centre) times the friction.
        /// </summary>
        internal static void Rock(
            Vec3 push, Vec3 point, Vec3 moment, Vec3 centre, Vec3 velocity, Vec3 spin, bool atPoint, double mass,
            double inertia, double radius, SolverConfig config, ref Vec3 force, ref Vec3 torque)
        {
            force += push;

            // D137. Not entered at 0, as in the recorded contact.
            Vec3 slide = Vec3.Zero;
            if (config.BedFriction > 0)
            {
                slide = ContactLaw.Friction(push, VelocityAt(velocity, spin, point - centre, atPoint), mass, config);
                force += slide;
            }

            if (atPoint)
            {
                torque += Anchor.CapTorque(moment + Vec3.Cross(point - centre, slide), inertia, radius, config);
            }
        }

        // ------------------------------------------------------------ the core's vertices (the level at the point)
        //
        // A rectangle, a segment or a point of a core touches a surface at every one of its distinct vertices,
        // not at the one support point: a plate lying flat carries its weight on its four corners, and a single
        // corner would rock it from corner to corner under the torque r x F for as long as it rests.

        /// <summary>The number of distinct vertices of a core: one for each sign choice over the axes of positive half-extent (1, 2 or 4, or 8 for a solid box).</summary>
        internal static int CoreVertexCount(Vec3 half) =>
            1 << ((half.X > 0 ? 1 : 0) + (half.Y > 0 ? 1 : 0) + (half.Z > 0 ? 1 : 0));

        /// <summary>
        /// One of the eight sign patterns of a core, as a world vertex <c>centre + R (+-hx, +-hy, +-hz)</c>: bit 0
        /// of <paramref name="pattern"/> is a minus on x, bit 1 on y, bit 2 on z. False, with nothing written,
        /// for a pattern with a minus on an axis of no half-extent, which repeats another's vertex. The patterns
        /// 0 to 7 in order give each distinct vertex once, in a fixed order.
        /// </summary>
        internal static bool CoreVertex(int pattern, Vec3 half, Mat3 rotation, Vec3 centre, out Vec3 vertex)
        {
            vertex = centre;

            bool minusX = (pattern & 1) != 0, minusY = (pattern & 2) != 0, minusZ = (pattern & 4) != 0;
            if ((minusX && !(half.X > 0)) || (minusY && !(half.Y > 0)) || (minusZ && !(half.Z > 0))) return false;

            // The corner is built as ConvexCore.Support builds it, so that a vertex that is the support point is the same bits.
            Vec3 corner = new Vec3(
                minusX ? -half.X : half.X,
                minusY ? -half.Y : half.Y,
                minusZ ? -half.Z : half.Z);

            vertex = centre + rotation * corner;
            return true;
        }

        /// <summary>
        /// The mass a push at <paramref name="lever"/> from the link's centre, along <paramref name="normal"/>, meets:
        /// <c>m_eff = 1 / (1/m + (r x n) . I^-1 (r x n))</c>, with <c>I^-1 = R diag(1/I) R^T</c> the inverse inertia in
        /// the world. At most <paramref name="mass"/>, and exactly it when <c>r x n</c> is zero. Written as
        /// <c>m / (1 + m q)</c>, the same number, so that a vanishing <c>q</c> leaves <c>m</c> to the bit.
        /// </summary>
        /// <param name="inertia">The link's principal inertias, about its origin in its own axes (<see cref="Creature.InertiaLocal"/>).</param>
        /// <param name="rotation">The link's committed turn (<see cref="Creature.LinkContactRotation"/>).</param>
        internal static double EffectiveMass(Vec3 lever, Vec3 normal, Vec3 inertia, Mat3 rotation, double mass)
        {
            // r x n in the link's own axes: R^T (r x n), so that I^-1 is the diagonal.
            Vec3 local = rotation.TransposedTimes(Vec3.Cross(lever, normal));

            double turning =
                (inertia.X > 0 ? local.X * local.X / inertia.X : 0.0) +
                (inertia.Y > 0 ? local.Y * local.Y / inertia.Y : 0.0) +
                (inertia.Z > 0 ? local.Z * local.Z / inertia.Z : 0.0);

            return mass / (1.0 + mass * turning);
        }

        internal const int KindFlatBed = 0, KindHeightBed = 1, KindReef = 2, KindGlass = 3;

        /// <summary>
        /// A surface's own sphere test on a sphere of the margin's radius at a vertex: the recorded routine of
        /// that surface, with the damper reading the velocity of <paramref name="point"/> (the vertex moved by the
        /// margin along the surface's direction) when <paramref name="atPoint"/>.
        /// </summary>
        internal static Vec3 SphereAt(
            int kind, Vec3 vertex, Vec3 point, Vec3 centre, double margin, Vec3 velocity, Vec3 spin, bool atPoint,
            double mass, SolverConfig config, double stiffness, double damping)
        {
            switch (kind)
            {
                case KindFlatBed: return FlatSphere(vertex, point, centre, margin, velocity, spin, atPoint, mass, config, stiffness, damping);
                case KindHeightBed: return HeightSphere(vertex, point, centre, margin, velocity, spin, atPoint, mass, config, stiffness, damping);
                case KindReef: return ReefSphere(vertex, point, centre, margin, velocity, spin, atPoint, mass, config, stiffness, damping);
                default: return GlassSphere(vertex, point, centre, margin, velocity, spin, atPoint, mass, config, stiffness, damping);
            }
        }

        /// <summary>
        /// The touch of a link with a surface on every distinct vertex of its core, each with its own spring and
        /// damper (the law's stiffness and damping at the mass the vertex meets, <see cref="EffectiveMass"/>:
        /// <paramref name="stiffness"/> and <paramref name="damping"/>, which are for the link's mass, are scaled by
        /// <c>m_eff / m</c>, and <c>m_eff</c> is the mass the push's cap reads). Not divided among the vertices.
        /// </summary>
        /// <param name="direction">The direction to the surface, the one the single support point is read along.</param>
        /// <param name="support">The deepest vertex (the largest push, the first in the vertex order on a tie); the link's centre with none.</param>
        /// <param name="point">The push-weighted contact point <c>sum |F_k| p_k / sum |F_k|</c>; one vertex's own point, bit for bit, when only one pushes.</param>
        /// <param name="moment">The pushes' moment about the centre, <c>sum (p_k - centre) x F_k</c>, not capped.</param>
        /// <returns>The pushes' sum.</returns>
        private static Vec3 Vertices(
            int kind, Creature body, int link, Vec3 centre, Vec3 direction, double margin, Vec3 velocity, Vec3 spin,
            double mass, SolverConfig config, double stiffness, double damping,
            out Vec3 support, out Vec3 point, out Vec3 moment)
        {
            Vec3 half = Vec3.Read(body.LinkCoreHalf, 3 * link);
            Mat3 rotation = Mat3.Read(body.LinkContactRotation, 9 * link);

            // The touch's push direction, read once for every vertex: minus the direction to the surface (straight up
            // for the flat bed, the height bed's normal, minus the reef's direction, the glass's inward radial).
            Vec3 pushDirection = -direction;
            Vec3 inertia = Vec3.Read(body.InertiaLocal, 3 * link);

            Vec3 total = Vec3.Zero;
            Vec3 weighted = Vec3.Zero;
            double weight = 0;
            double deepest = 0;
            int pushing = 0;
            Vec3 only = centre;

            support = centre;
            moment = Vec3.Zero;

            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (!CoreVertex(pattern, half, rotation, centre, out Vec3 vertex)) continue;

                Vec3 vertexPoint = vertex + direction * margin;

                // The mass this vertex pushes against: a corner of a turning body is lighter than its centre, so the
                // spring, the damper and the cap are those of the mass at the point, along the touch's one normal.
                double effective = EffectiveMass(vertexPoint - centre, pushDirection, inertia, rotation, mass);
                double scale = effective / mass;

                Vec3 push = SphereAt(
                    kind, vertex, vertexPoint, centre, margin, velocity, spin, true, effective, config,
                    stiffness * scale, damping * scale);

                double size = push.Magnitude;
                if (!(size > 0)) continue;

                pushing++;
                total += push;
                weighted += vertexPoint * size;
                weight += size;
                moment += Vec3.Cross(vertexPoint - centre, push);
                only = vertexPoint;

                if (size > deepest)
                {
                    deepest = size;
                    support = vertex;
                }
            }

            point = pushing > 1 ? weighted * (1.0 / weight) : (pushing == 1 ? only : centre);
            return total;
        }

        // Each surface comes in three parts: the touch (the early-out, the direction, then the single support
        // point or, at the level at the point, every vertex), the sphere test at one point that both read, and a
        // wrapper that drops the moment, for a caller that wants the push and the points only.

        /// <summary>The flat slab: straight down. The push is straight up.</summary>
        internal static Vec3 FlatBed(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            Vec3 spin = default, bool atPoint = false) =>
            FlatBedTouch(
                body, link, centre, radius, margin, velocity, mass, config, stiffness, damping,
                out support, out point, out _, spin, atPoint);

        /// <summary>The flat slab's touch. <paramref name="moment"/> is the pushes' moment about the centre, zero unless <paramref name="atPoint"/>.</summary>
        internal static Vec3 FlatBedTouch(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            out Vec3 moment, Vec3 spin = default, bool atPoint = false)
        {
            support = centre;
            point = centre;
            moment = Vec3.Zero;

            double bounded = -config.WorldDepthMetres - (centre.Y - radius);
            if (!(bounded > 0)) return Vec3.Zero;

            Vec3 direction = new Vec3(0, -1, 0);
            if (atPoint)
            {
                return Vertices(
                    KindFlatBed, body, link, centre, direction, margin, velocity, spin, mass, config,
                    stiffness, damping, out support, out point, out moment);
            }

            support = CoreSupport(body, link, centre, direction);
            point = support + direction * margin;

            return FlatSphere(support, point, centre, margin, velocity, spin, false, mass, config, stiffness, damping);
        }

        private static Vec3 FlatSphere(
            Vec3 support, Vec3 point, Vec3 centre, double margin, Vec3 velocity, Vec3 spin, bool atPoint,
            double mass, SolverConfig config, double stiffness, double damping)
        {
            double below = -config.WorldDepthMetres - (support.Y - margin);
            if (!(below > 0)) return Vec3.Zero;

            Vec3 at = VelocityAt(velocity, spin, point - centre, atPoint);

            double up = ContactLaw.PairPush(
                stiffness * below - damping * at.Y, mass, at.Y, config);

            return new Vec3(0, up, 0);
        }

        /// <summary>The height map: minus the bed's normal under the centre.</summary>
        internal static Vec3 HeightBed(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            Vec3 spin = default, bool atPoint = false) =>
            HeightBedTouch(
                body, link, centre, radius, margin, velocity, mass, config, stiffness, damping,
                out support, out point, out _, spin, atPoint);

        /// <summary>The height map's touch.</summary>
        internal static Vec3 HeightBedTouch(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            out Vec3 moment, Vec3 spin = default, bool atPoint = false)
        {
            support = centre;
            point = centre;
            moment = Vec3.Zero;

            Vec3 bounded = ContactBed.Push(
                centre, radius, atPoint ? Vec3.Zero : velocity, mass, config, stiffness, damping);
            if (bounded.X == 0 && bounded.Y == 0 && bounded.Z == 0) return Vec3.Zero;

            Vec3 direction = -ContactBed.NormalUnder(centre, config);
            if (atPoint)
            {
                return Vertices(
                    KindHeightBed, body, link, centre, direction, margin, velocity, spin, mass, config,
                    stiffness, damping, out support, out point, out moment);
            }

            support = CoreSupport(body, link, centre, direction);
            point = support + direction * margin;

            return HeightSphere(support, point, centre, margin, velocity, spin, false, mass, config, stiffness, damping);
        }

        private static Vec3 HeightSphere(
            Vec3 support, Vec3 point, Vec3 centre, double margin, Vec3 velocity, Vec3 spin, bool atPoint,
            double mass, SolverConfig config, double stiffness, double damping) =>
            ContactBed.Push(
                support, margin, VelocityAt(velocity, spin, point - centre, atPoint), mass, config, stiffness, damping);

        /// <summary>
        /// The reefs and the coral: minus the rock's distance gradient at the centre. The bounding sphere's
        /// push already runs along that gradient's unit vector, so its direction is the one read.
        /// </summary>
        internal static Vec3 Reef(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            Vec3 spin = default, bool atPoint = false) =>
            ReefTouch(
                body, link, centre, radius, margin, velocity, mass, config, stiffness, damping,
                out support, out point, out _, spin, atPoint);

        /// <summary>The reefs' and the coral's touch.</summary>
        internal static Vec3 ReefTouch(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            out Vec3 moment, Vec3 spin = default, bool atPoint = false)
        {
            support = centre;
            point = centre;
            moment = Vec3.Zero;

            Vec3 bounded = ContactReef.Push(
                centre, radius, atPoint ? Vec3.Zero : velocity, mass, config, stiffness, damping);
            if (bounded.X == 0 && bounded.Y == 0 && bounded.Z == 0) return Vec3.Zero;

            Vec3 direction = -(bounded * (1.0 / bounded.Magnitude));
            if (atPoint)
            {
                return Vertices(
                    KindReef, body, link, centre, direction, margin, velocity, spin, mass, config,
                    stiffness, damping, out support, out point, out moment);
            }

            support = CoreSupport(body, link, centre, direction);
            point = support + direction * margin;

            return ReefSphere(support, point, centre, margin, velocity, spin, false, mass, config, stiffness, damping);
        }

        private static Vec3 ReefSphere(
            Vec3 support, Vec3 point, Vec3 centre, double margin, Vec3 velocity, Vec3 spin, bool atPoint,
            double mass, SolverConfig config, double stiffness, double damping) =>
            ContactReef.Push(
                support, margin, VelocityAt(velocity, spin, point - centre, atPoint), mass, config, stiffness, damping);

        /// <summary>The glass, a cylinder about the vertical axis: the outward radial direction. The push runs inward.</summary>
        internal static Vec3 Glass(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            Vec3 spin = default, bool atPoint = false) =>
            GlassTouch(
                body, link, centre, radius, margin, velocity, mass, config, stiffness, damping,
                out support, out point, out _, spin, atPoint);

        /// <summary>The glass's touch.</summary>
        internal static Vec3 GlassTouch(
            Creature body, int link, Vec3 centre, double radius, double margin, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping, out Vec3 support, out Vec3 point,
            out Vec3 moment, Vec3 spin = default, bool atPoint = false)
        {
            support = centre;
            point = centre;
            moment = Vec3.Zero;

            double x = centre.X - config.TankAxisX;
            double z = centre.Z - config.TankAxisZ;
            double radial = System.Math.Sqrt(x * x + z * z);
            if (!(radial + radius - config.TankRadiusMetres > 0)) return Vec3.Zero;

            Vec3 outward = radial > 1e-9 ? new Vec3(x / radial, 0, z / radial) : new Vec3(-1, 0, 0);
            if (atPoint)
            {
                return Vertices(
                    KindGlass, body, link, centre, outward, margin, velocity, spin, mass, config,
                    stiffness, damping, out support, out point, out moment);
            }

            support = CoreSupport(body, link, centre, outward);
            point = support + outward * margin;

            return GlassSphere(support, point, centre, margin, velocity, spin, false, mass, config, stiffness, damping);
        }

        private static Vec3 GlassSphere(
            Vec3 support, Vec3 point, Vec3 centre, double margin, Vec3 velocity, Vec3 spin, bool atPoint,
            double mass, SolverConfig config, double stiffness, double damping)
        {
            double sx = support.X - config.TankAxisX;
            double sz = support.Z - config.TankAxisZ;
            double supportRadial = System.Math.Sqrt(sx * sx + sz * sz);
            double outside = supportRadial + margin - config.TankRadiusMetres;
            if (!(outside > 0)) return Vec3.Zero;

            Vec3 inward = supportRadial > 1e-9
                ? new Vec3(-sx / supportRadial, 0, -sz / supportRadial)
                : Vec3.UnitX;

            double approach = Vec3.Dot(VelocityAt(velocity, spin, point - centre, atPoint), inward);

            return inward * ContactLaw.PairPush(
                stiffness * outside - damping * approach, mass, approach, config);
        }

        // Contacts.NoteRock, the same record on the support sphere: the touch, the rock's normal, and the
        // sphere's deepest point along it.
        private static void NoteRock(Creature body, int i, Vec3 centre, double radius, Vec3 push, ref double kept)
        {
            double size = push.Magnitude;
            if (!(size > kept)) return;

            kept = size;
            Vec3 normal = push * (1.0 / size);
            body.RockTouched[i] = true;
            Vec3.Write(body.RockNormal, 3 * i, normal);
            Vec3.Write(body.RockPoint, 3 * i, centre - normal * radius);
        }
    }
}
