using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// How far a mouth is from a body's parts, measured on the true shape — D275, <c>RunConfig.BiteNeedsMouth</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The mouth reaches a part when a ball of the mouth's radius at the mouth point touches the part's core plus its
    /// margin.</b> The gap is the distance from the point to the part's convex core (GJK, <see cref="ConvexCore.Distance(Vec3, double[], Mat3, Vec3, Vec3, double[], Mat3, Vec3, out double, out Vec3, out Vec3)"/>,
    /// unchanged) less the part's margin, and <c>gap / radius</c> at most 1 is a touch. A point inside the core has the
    /// gap <c>-margin</c>, so a mouth buried in a part reads well under 1.
    /// </para>
    /// <para>
    /// <b>The pose is the one <c>Simulation.ObserveMouths</c> poses D214's mouth points with</b>: each link's
    /// <see cref="Creature.Position"/> and <see cref="Creature.RotationMatrix"/> as the physics left them, and the
    /// solver's own phenotype. Link <i>i</i> is part <i>i</i>. The cores, margins and hulls are the solver's tables
    /// (<see cref="Creature.LinkCoreHalf"/>, <see cref="Creature.LinkMargin"/>, <see cref="Creature.LinkCoreHull"/>),
    /// built only on a true contact shape; a body without them is not measured.
    /// </para>
    /// <para>
    /// <b>Serial, allocation-free and deterministic</b>: a fixed order of mouths and parts, the earlier pair winning a
    /// tie. Nothing here writes to a body.
    /// </para>
    /// </remarks>
    public static class MouthTouch
    {
        /// <summary>
        /// The signed gap, metres, from a world point to link <paramref name="link"/> of <paramref name="body"/>: the
        /// distance to the link's core less its margin, or <c>-margin</c> when the point is inside the core. Across a
        /// seam it is measured the short way round, as the contact pair is.
        /// </summary>
        /// <exception cref="InvalidOperationException">The body carries no true-shape cores.</exception>
        public static double Gap(Creature body, int link, Vec3 point, SolverConfig config)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (body.LinkCoreHalf == null || body.LinkMargin == null)
            {
                throw new InvalidOperationException(
                    "The body carries no true-shape cores; the mouth's gap is measured on the true shape (D275).");
            }

            Vec3 rel = point - Vec3.Read(body.Position, 3 * link);
            if (config.Seamed) rel = Contacts.ShortWay(rel, config);

            return GapAt(body, link, rel);
        }

        /// <summary>The gap from a point at <paramref name="rel"/> from the link's centre.</summary>
        private static double GapAt(Creature body, int link, Vec3 rel)
        {
            // The point is a core of its own at rel, the part's core sits at the origin: the pair routine, as the
            // theatre's PointApart calls it with the roles the other way round.
            bool apart = ConvexCore.Distance(
                Vec3.Zero, null, Mat3.Identity, rel,
                Vec3.Read(body.LinkCoreHalf, 3 * link),
                body.LinkCoreHull == null ? null : body.LinkCoreHull[link],
                Mat3.Read(body.RotationMatrix, 9 * link),
                Vec3.Zero,
                out double d, out _, out _);

            double margin = body.LinkMargin[link];
            return apart ? d - margin : -margin;
        }

        /// <summary>
        /// The smallest <c>gap / radius</c> over every eating part of <paramref name="mouthBody"/> and every part of
        /// <paramref name="other"/>, with the pair that gives it.
        /// </summary>
        /// <param name="mouthBody">The body whose mouths are measured.</param>
        /// <param name="other">The body whose parts are bitten.</param>
        /// <param name="config">The solver's configuration, for the seam.</param>
        /// <param name="mouthLink">The eating part of <paramref name="mouthBody"/> that wins, or -1.</param>
        /// <param name="targetPart">The part of <paramref name="other"/> it is measured to, or -1.</param>
        /// <returns>
        /// The ratio; at most 1 is a touch. NaN when nothing could be measured (the other body carries no cores, the
        /// body has no eating part with a usable radius, or the plan and the solver disagree), and positive infinity
        /// when every pair was farther than the radius from the part's bounding ball, which is cheaper than GJK and
        /// cannot reach.
        /// </returns>
        /// <remarks>
        /// A part is skipped when its bounding ball (<see cref="Creature.LinkReach"/>, which holds every point of core
        /// plus margin), grown by the radius, does not reach the mouth point: its gap is then above the radius and its
        /// ratio above 1, so the pair that wins when one reaches is never one that was skipped.
        /// </remarks>
        public static float Reach(
            Creature mouthBody, Creature other, SolverConfig config, out int mouthLink, out int targetPart)
        {
            if (mouthBody == null) throw new ArgumentNullException(nameof(mouthBody));
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (config == null) throw new ArgumentNullException(nameof(config));

            mouthLink = -1;
            targetPart = -1;

            Phenotype phenotype = mouthBody.Phenotype;
            if (other.LinkCoreHalf == null || other.LinkMargin == null) return float.NaN;
            if (mouthBody.Links != phenotype.PartCount) return float.NaN;

            double[] reach = other.LinkReach;
            bool seamed = config.Seamed;
            bool any = false;
            float best = float.PositiveInfinity;

            for (int i = 0; i < mouthBody.Links; i++)
            {
                if (!MouthPoint.IsEatingPart(phenotype.Parts[i])) continue;

                float radius = MouthPoint.Radius(phenotype, i);
                if (!(radius > 0f)) continue;
                any = true;

                Float3 local = MouthPoint.Local(phenotype, i);
                Vec3 point = Vec3.Read(mouthBody.Position, 3 * i) +
                             Mat3.Read(mouthBody.RotationMatrix, 9 * i) * new Vec3(local.X, local.Y, local.Z);

                for (int q = 0; q < other.Links; q++)
                {
                    Vec3 rel = point - Vec3.Read(other.Position, 3 * q);
                    if (seamed) rel = Contacts.ShortWay(rel, config);

                    double within = reach[q] + radius;
                    if (rel.SqrMagnitude > within * within) continue;

                    float ratio = (float)(GapAt(other, q, rel) / radius);
                    if (!(ratio < best)) continue;

                    best = ratio;
                    mouthLink = i;
                    targetPart = q;
                }
            }

            return any ? best : float.NaN;
        }
    }
}
