using Evosim.Core;

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
