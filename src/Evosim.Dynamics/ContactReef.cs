using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The reefs' rock as a contact: one sphere against the rock's signed distance, pushed by the
    /// bed's spring-damper with the bed's material — <c>logbook/specs/reef-spec.md</c> §2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One law for the three faces of a mushroom.</b> <see cref="ReefGeometry"/>'s distance is
    /// exact outside the rock (a smooth minimum in the fillet under the cap), and its gradient is
    /// the outward normal: straight up over the cap's table, so a body resting there is held as on a
    /// floor; straight down under the cap, so a body pressed against the underside is pushed down as
    /// the surface clamp pushes up; outward from the stem, as the glass pushes in. The overlap is
    /// the sphere's radius less the distance, and the push is <see cref="ContactLaw.PairPush"/> on
    /// it along the normal, the bed's arithmetic in <see cref="ContactBed"/> with a different
    /// surface.
    /// </para>
    /// <para>
    /// <b>Defined however deep the sphere is.</b> Inside the rock the distance is negative and its
    /// gradient points to the nearest face, so a body thrown into a stem is pushed out of it by the
    /// shortest way rather than through it; one whose root is deeper than its own radius is the
    /// divergence guard's (<see cref="Divergence"/>), as a body under the bed is.
    /// </para>
    /// <para>
    /// <b>Deterministic and allocation-free</b>: a pure function of the body's committed sphere and
    /// the rock, so the thread count stays invisible.
    /// </para>
    /// </remarks>
    public static class ContactReef
    {
        /// <summary>
        /// The rock's push on one sphere, N, or <see cref="Vec3.Zero"/> when it is clear: a body's
        /// under the body sphere, a link's under per-part contact (D114).
        /// </summary>
        public static Vec3 Push(
            Vec3 centre, double radius, Vec3 velocity, double mass,
            SolverConfig config, double stiffness, double damping)
        {
            ReefGeometry reefs = config.Reefs;

            double distance = reefs.SignedDistance(
                centre.X, centre.Y, centre.Z, out _, out ReefGeometry.Distance at);

            double penetration = radius - distance;
            if (!(penetration > 0)) return Vec3.Zero;

            var gradient = new Vec3(at.Gx, at.Gy, at.Gz);
            double length = gradient.Magnitude;
            Vec3 normal = length > 1e-12 ? gradient * (1.0 / length) : Vec3.UnitY;

            double approach = Vec3.Dot(velocity, normal);

            return normal * ContactLaw.PairPush(
                stiffness * penetration - damping * approach, mass, approach, config);
        }
    }
}
