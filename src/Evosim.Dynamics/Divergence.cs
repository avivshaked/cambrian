using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The four guards the farm asks of every body at the metabolic cadence, as a pure test that
    /// names the one that fired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The kill is the caller's.</b> In the farm a diverged body is a counted
    /// <c>DeathCause.Diverged</c> death through <c>World.KillDiverged</c>, which empties the
    /// organism, returns its matter and closes both books — none of which this assembly knows
    /// anything about. So this answers the question and does nothing else, and the farm loop
    /// (package G) routes the answer through Core. The solver's own
    /// <c>Alive = false</c> on a non-finite body stays where it is as the backstop: it stops a
    /// body the caller has not yet looked at from being stepped again, and stops it from
    /// poisoning the contact grid, and the caller finds it the next time it asks.
    /// </para>
    /// <para>
    /// <b>The order is the farm's</b>, because the reason a dump carries is the first guard that
    /// fired and not the worst one: the root's height and horizontal finiteness first, then the
    /// tank's radius, then the bed, then the other links. A body that is both below the rock and
    /// non-finite reads as non-finite in the farm too.
    /// </para>
    /// <para>
    /// <b>Three of the four reasons are new text.</b> The farm passes null for the height, the
    /// radius and the link guards and a sentence only for the bed's, because the dump's
    /// <c>divergenceReason</c> was appended for D092 and nobody went back. The bed's wording
    /// here is the farm's, character for character, so a reader of both records reads one
    /// sentence; the other three say which guard fired rather than nothing at all.
    /// </para>
    /// </remarks>
    public static class Divergence
    {
        /// <summary>How far past the glass a root may be before the radius guard fires, m.</summary>
        /// <remarks>
        /// One metre, the farm's: an ordinary depenetration at the rim is centimetres, so the
        /// margin can never be an ordinary contact and the guard can be read as "the solver threw
        /// this body through the wall".
        /// </remarks>
        public const double GlassMarginMetres = 1.0;

        /// <summary>
        /// Whether a body is one the world can still hold, and which guard says otherwise.
        /// </summary>
        /// <param name="body">The body, read at the state it now stands in.</param>
        /// <param name="config">The world's depth, its glass and its bed.</param>
        /// <param name="reason">The guard that fired, or null.</param>
        /// <returns>True when the body must be killed.</returns>
        public static bool Diverged(Creature body, SolverConfig config, out string reason) =>
            Diverged(body, config, body.ContactRadius, out reason);

        /// <summary>
        /// <see cref="Diverged(Creature, SolverConfig, out string)"/> with the bounding radius
        /// stated rather than taken from the committed contact sphere.
        /// </summary>
        /// <remarks>
        /// <b>The farm's margin is not the same radius.</b> <c>CheckFinite</c> reads
        /// <c>Body.Radius</c>, which is <c>SharedVolume.BoundingRadius(phenotype)</c> — a
        /// property of the developed body, refreshed at a resize and not at a step. The default
        /// overload passes <see cref="Creature.ContactRadius"/>, which is the live bounding
        /// sphere the contact grid uses and moves as the joints move. The two differ by however
        /// much a body can fold, so a farm loop that wants the farm's number passes it here.
        /// </remarks>
        public static bool Diverged(
            Creature body, SolverConfig config, double boundingRadius, out string reason)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (config == null) throw new ArgumentNullException(nameof(config));

            reason = null;

            double rootX = body.Position[0];
            double rootY = body.Position[1];
            double rootZ = body.Position[2];

            float depth = (float)config.WorldDepthMetres;

            // Guard 1, the height and the root's horizontal finiteness. Core's own bound, called
            // rather than restated: a height is in the world when it is under the surface by the
            // world's depth and over the floor by twice it, and a NaN fails both comparisons.
            // The cast is what the farm's float world would have done anyway; a double past
            // float's range becomes an infinity, which the bound refuses.
            if (!World.HeightIsInTheWorld((float)rootY, depth))
            {
                reason = double.IsNaN(rootY) || double.IsInfinity(rootY)
                    ? "a non-finite root height"
                    : FormattableString.Invariant(
                        $"a root height of {rootY:g4} m in a world {config.WorldDepthMetres:0.#} m deep");

                return true;
            }

            double horizontal = rootX + rootZ;

            if (double.IsNaN(horizontal) || double.IsInfinity(horizontal))
            {
                reason = "a non-finite root position";
                return true;
            }

            // Guard 2, the glass. The root's bound only, exactly as the height's is: a link
            // legitimately hangs away from its root, so asking this of a leaf would kill a
            // healthy body brushing the wall.
            if (config.TankRadiusMetres > 0)
            {
                double dx = rootX - config.TankAxisX;
                double dz = rootZ - config.TankAxisZ;
                double limit = config.TankRadiusMetres + GlassMarginMetres;

                if (dx * dx + dz * dz > limit * limit)
                {
                    reason = FormattableString.Invariant(
                        $"outside the glass at {System.Math.Sqrt(dx * dx + dz * dz):0.###} m ") +
                        FormattableString.Invariant(
                            $"from the axis of a tank {config.TankRadiusMetres:0.##} m in radius");

                    return true;
                }
            }

            // Guard 3, the rock — D092. After the horizontal test, because the map has to be
            // read at a place and a non-finite place is the test above's business. The margin is
            // the body's own bounding radius rather than a fixed metre: a body resting in a
            // hollow touches the rock at its own radius, so anything deeper than that is a body
            // inside it and not a body on it.
            BedShape bed = config.Bed;

            if (bed != null && bed.HasRelief)
            {
                double floorY = bed.FloorY(rootX, rootZ);

                if (rootY < floorY - boundingRadius)
                {
                    reason = FormattableString.Invariant(
                        $"below the bed by {floorY - rootY:0.###} m at x {rootX:0.##}, z {rootZ:0.##}");

                    return true;
                }
            }

            // Guard 4, every other link. The farm asks this of the links' positions; this asks it
            // of their spins and velocities too, which is the set Creature.IsFinite already reads
            // and the set the solver's own backstop kills on — so the guard and the backstop
            // cannot disagree about what a lost body is.
            for (int b = 1; b < body.Links; b++)
            {
                double sum =
                    body.Position[3 * b] + body.Position[3 * b + 1] + body.Position[3 * b + 2] +
                    body.Spin[3 * b] + body.Spin[3 * b + 1] + body.Spin[3 * b + 2] +
                    body.Velocity[3 * b] + body.Velocity[3 * b + 1] + body.Velocity[3 * b + 2];

                if (double.IsNaN(sum) || double.IsInfinity(sum))
                {
                    reason = FormattableString.Invariant($"link {b} is not finite");
                    return true;
                }
            }

            // The root's own spin and velocity, which guard 1 does not read and guard 4's loop
            // starts past. A root that is finite in place and not in motion is a body one step
            // from the first three guards, and the solver's backstop would take it anyway.
            double rootMotion =
                body.Spin[0] + body.Spin[1] + body.Spin[2] +
                body.Velocity[0] + body.Velocity[1] + body.Velocity[2];

            if (double.IsNaN(rootMotion) || double.IsInfinity(rootMotion))
            {
                reason = "link 0 is not finite";
                return true;
            }

            return false;
        }
    }
}
