using System.Runtime.CompilerServices;
using Evosim.Core;

// The bed's exact path is internal so that its test can hold the early-out against it
// (ContactBedBoundTests). The attribute names an assembly Unity does not have, which costs
// nothing there.
[assembly: InternalsVisibleTo("Evosim.Dynamics.Tests")]

namespace Evosim.Dynamics
{
    /// <summary>
    /// The shaped sea bed as a contact: one bounding sphere against a height field, pushed by
    /// the same spring-damper the flat slab uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not the farm's collider, and the difference is the whole reason D092 has a guard.</b>
    /// <c>SeaFloor</c> hands PhysX a <c>MeshCollider</c> over a half-metre lattice of
    /// <see cref="BedShape.FloorY"/>, with a backstop box under it, and every part of every body
    /// collides against that mesh. This reads the same map analytically at one point — the
    /// bounding sphere's centre — and pushes the whole body along the map's own normal. It
    /// cannot tunnel the way a mesh can, because there is no mesh and no swept test: the force
    /// grows with the depth of the overlap and is defined however deep the body is. What it
    /// cannot do is rest a long body across a ridge: the sphere sees one height, so a body wider
    /// than the relief's wavelength floats above the ridge it should be draped over. That is the
    /// price of the bounding sphere and it is the same price the creature-creature push pays.
    /// </para>
    /// <para>
    /// <b>The distance is the first-order perpendicular one, not the vertical one.</b> A vertical
    /// gap over a 30° ramp overstates the clearance by 15%, and a sphere resting on a slope would
    /// sit that much too low and be pushed straight up rather than off the hill. Dividing the
    /// vertical gap by <c>sqrt(1 + |grad h|^2)</c> is the plane-distance to the tangent plane at
    /// the column under the centre, which is exact for a plane — which is what the tilt is — and
    /// first-order for the cosine bands.
    /// </para>
    /// <para>
    /// <b>Clear of the highest floor the place could have, and no cosine is taken</b> (2026-10-03,
    /// <c>logbook/specs/farm-physics-speed-read.md</c> P3). Until then every link of every body
    /// paid the twelve modes' cosines and sines on every step to find, nearly always, that it was
    /// metres above the rock. <see cref="ClearOfAnyFloor"/> asks first, from
    /// <see cref="BedShape.HeightUpperBound"/> and <see cref="BedShape.SteepestSlopeBound"/>,
    /// whether the sphere is clear of any floor the map could put under it, and only a sphere it
    /// cannot clear goes on to the exact test. The bound is sound in the doubles, so where it
    /// fires the exact path would have returned <see cref="Vec3.Zero"/> too, and the push is the
    /// same bits; <c>ContactBedBoundTests</c> holds that over many places and several beds.
    /// </para>
    /// <para>
    /// <b>Deterministic and allocation-free.</b> <see cref="BedShape.HeightAndGradient"/> is a
    /// sum of twelve cosines and a plane, evaluated from the body's own state, so two bodies on
    /// two threads asking the same question get the same answer and neither writes anything.
    /// </para>
    /// </remarks>
    public static class ContactBed
    {
        /// <summary>
        /// The bed's push on one body, N, or <see cref="Vec3.Zero"/> when the sphere is clear of
        /// the rock.
        /// </summary>
        /// <param name="body">The body, read at its committed contact sphere.</param>
        /// <param name="config">The world, for its bed and <see cref="ContactLaw"/>'s bounds.</param>
        /// <param name="stiffness">The spring, N/m — <c>m * omega^2</c>, the caller's.</param>
        /// <param name="damping">The damper, N·s/m — <c>2 * zeta * m * omega</c>, the caller's.</param>
        public static Vec3 Push(
            Creature body, SolverConfig config, double stiffness, double damping) =>
            Push(
                body.ContactCentre, body.ContactRadius, body.ContactVelocity, body.TotalMass,
                config, stiffness, damping);

        /// <summary>
        /// The bed's push on one sphere, N: a body's under the body sphere, a link's under
        /// per-part contact (D114). The same arithmetic in the same order either way.
        /// </summary>
        /// <param name="centre">The sphere's committed centre.</param>
        /// <param name="radius">Its radius, metres.</param>
        /// <param name="velocity">Its velocity, for the damper.</param>
        /// <param name="mass">What it carries against the rock, kg: the body's, or the link's.</param>
        public static Vec3 Push(
            Vec3 centre, double radius, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping)
        {
            if (ClearOfAnyFloor(centre, radius, config.Bed)) return Vec3.Zero;

            return PushExact(centre, radius, velocity, mass, config, stiffness, damping);
        }

        // The early-out's own allowance for rounding, relative to the size of the numbers it
        // subtracts: far above what the exact path's four roundings can move, far below a
        // millimetre at any place in a tank.
        private const double ClearSlack = 1e-9;

        /// <summary>
        /// Whether a sphere is clear of any floor the bed could put under it, by a margin no
        /// rounding of the exact test can close: where this is true, the exact test's
        /// penetration is not positive and the push is <see cref="Vec3.Zero"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why it is sound.</b> The exact test pushes when <c>r - (y - f) / s</c> is positive,
        /// with <c>f</c> the floor under the centre and <c>s = sqrt(1 + |grad h|^2)</c>. The floor is
        /// at most <c>c = -depth + HeightUpperBound</c> and <c>s</c> is at most
        /// <c>1 + SteepestSlopeBound</c>, so <c>y - c &gt;= r (1 + slope bound)</c> gives
        /// <c>(y - f) / s &gt;= r</c>. The margin over that, a billionth of the numbers' own size,
        /// is what carries it from the real numbers to the doubles.
        /// </para>
        /// <para>
        /// <b>False whenever anything is not a number</b>, so a body the solver is losing takes the
        /// exact path and comes out of it exactly as it did, NaNs and all; and false for a negative
        /// radius, which no sphere has and the argument above does not cover.
        /// </para>
        /// </remarks>
        public static bool ClearOfAnyFloor(Vec3 centre, double radius, BedShape bed)
        {
            double ceiling = -(double)bed.DepthMetres + bed.HeightUpperBound(centre.X, centre.Z);
            double reach = radius * (1.0 + bed.SteepestSlopeBound);
            double clear = centre.Y - ceiling - reach;

            return radius >= 0 &&
                   clear > ClearSlack * (System.Math.Abs(centre.Y) + System.Math.Abs(ceiling) + reach);
        }

        /// <summary>
        /// <see cref="Push(Vec3, double, Vec3, double, SolverConfig, double, double)"/> without
        /// the early-out: the twelve modes sampled at the centre, every time.
        /// </summary>
        internal static Vec3 PushExact(
            Vec3 centre, double radius, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping)
        {
            BedShape bed = config.Bed;

            bed.HeightAndGradient(
                centre.X, centre.Z, out double height, out double gradientX, out double gradientZ);

            double floorY = -(double)bed.DepthMetres + height;

            // The scale that turns a vertical gap into a perpendicular one, and the same scale
            // that normalises the map's normal (-dh/dx, 1, -dh/dz).
            double slope = System.Math.Sqrt(
                1.0 + gradientX * gradientX + gradientZ * gradientZ);

            double penetration = radius - (centre.Y - floorY) / slope;
            if (penetration <= 0) return Vec3.Zero;

            Vec3 normal = new Vec3(-gradientX, 1.0, -gradientZ) * (1.0 / slope);
            double approach = Vec3.Dot(velocity, normal);

            return normal * ContactLaw.PairPush(
                stiffness * penetration - damping * approach, mass, approach, config);
        }
    }
}
