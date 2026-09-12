using System;

namespace Evosim.Core
{
    /// <summary>
    /// The tank's arithmetic, in one place: the radius that keeps an area an area, the test for
    /// being in the water, and the rings that stand in for the box's patches.
    /// <c>logbook/specs/tank-spec.md</c>, <c>fable-propose-aquarium.md</c> ruling 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four classes need this and none of them may own it.</b> <see cref="World"/>,
    /// <see cref="CurrentField"/>, <see cref="GridField"/> and <c>Evosim.Sim</c>'s
    /// <c>SharedVolume</c> each have to answer "is this point in the water" and "which patch is
    /// it in" with the same number, and the box's own arithmetic is repeated in all four for the
    /// reason <see cref="CurrentField.PatchOfXZ"/> records — a field knows nothing about a placer.
    /// The tank's is not repeated, because the ring boundaries are square roots rather than a
    /// division and four copies of a square root are four chances to round differently.
    /// </para>
    /// <para>
    /// <b>The radius is derived, never configured.</b> <c>R = sqrt(area/π)</c>, so
    /// <see cref="RunConfig.WorldAreaSquareMetres"/> keeps its meaning across the two shapes: it
    /// is the sun's aperture and the denominator of every density (DESIGN §5A.2b), and a tank at
    /// 100 m² receives exactly what the 20 × 5 m box received. A radius knob would let a config
    /// name a footprint its own area contradicts, which is the hazard
    /// <see cref="CurrentField.PatchWidthMetres"/> is kept out of the hash for.
    /// </para>
    /// <para>
    /// <b>The bounding square is <c>[0, 2R) × [0, 2R)</c> with the axis at <c>(R, R)</c></b>, so
    /// every consumer of a length and a width sees a rectangle and the storage stays an array.
    /// Nothing here folds a coordinate: the tank has a wall where the box has a seam, so there is
    /// no ring to take a shortest way round.
    /// </para>
    /// </remarks>
    public static class TankGeometry
    {
        /// <summary>The radius of a tank of this footprint, m: <c>sqrt(area/π)</c>.</summary>
        /// <remarks>
        /// 100 m² is 5.642 m and 400 m² is 11.284 m — the two footprints the aquarium proposal's
        /// first two rounds run at.
        /// </remarks>
        public static float RadiusFor(float areaSquareMetres)
        {
            if (!(areaSquareMetres > 0f) || float.IsInfinity(areaSquareMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(areaSquareMetres), areaSquareMetres,
                    "A tank's radius comes from its footprint, which is positive and finite.");
            }

            return (float)Math.Sqrt(areaSquareMetres / Math.PI);
        }

        /// <summary>The squared distance from the axis at <c>(R, R)</c>, m².</summary>
        /// <remarks>
        /// Squared, because every caller compares it against <c>R²</c> or scales it by the ring
        /// count, and a square root taken here would be taken back immediately in both.
        /// </remarks>
        public static double SquaredRadiusAt(double x, double z, double radius)
        {
            double dx = x - radius;
            double dz = z - radius;
            return dx * dx + dz * dz;
        }

        /// <summary>Whether a horizontal position is inside the glass: <c>(x−R)² + (z−R)² ≤ R²</c>.</summary>
        public static bool Inside(double x, double z, double radius) =>
            SquaredRadiusAt(x, z, radius) <= radius * radius;

        /// <summary>
        /// Whether a horizontal position is at least <paramref name="clearance"/> inside the
        /// glass: <c>(x−R)² + (z−R)² ≤ (R − clearance)²</c>.
        /// <c>logbook/specs/wall-clearance-spec.md</c> (Astra review F2).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The three-argument form above is exactly this one at clearance 0</b> — a point
        /// asked to be inside the circle itself, with nothing held back from the glass. This
        /// overload is what <c>Evosim.Sim.SharedVolume.Free</c> needed and did not have: the
        /// birth gate tested a candidate's centre against the water and every other body's
        /// sphere, never the candidate's own bounding radius against the glass, so a body drawn
        /// one centimetre inside the circle with a half-metre bounding sphere was accepted with
        /// a corner outside it — the review's arithmetic probe found a corner outside the disc
        /// in 208 of 1,000 unit-cube founders drawn over the full circle, no physics involved.
        /// </para>
        /// <para>
        /// <b>A clearance at or above the radius refuses every point, the axis included.</b>
        /// There is no longer any point that can hold that much distance from a wall this
        /// close, so the axis is not a special case here the way it is in
        /// <see cref="Inside(double, double, double)"/> and <see cref="RingOf"/> — those read a
        /// boundary case as "still water"; this one
        /// reads a clearance that has eaten the whole tank as "no water at all".
        /// </para>
        /// </remarks>
        public static bool Inside(double x, double z, double radius, double clearance)
        {
            if (clearance >= radius) return false;

            double effectiveRadius = radius - clearance;
            return SquaredRadiusAt(x, z, radius) <= effectiveRadius * effectiveRadius;
        }

        /// <summary>
        /// Which ring of equal area a horizontal position falls in, 0 at the axis and
        /// <paramref name="rings"/>−1 at the glass.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Rings of equal area, not of equal width.</b> Ring <c>i</c> holds
        /// <c>R·sqrt(i/K) ≤ r &lt; R·sqrt((i+1)/K)</c>, so every patch is <c>area/K</c> square
        /// metres exactly as the box's patches are, and the report's per-patch bins stay
        /// comparable — the whole point of a bin is that two of them can be read against each
        /// other. Rings of equal width would make the outer bins four and five times the inner
        /// ones and every per-patch density a readout of the geometry.
        /// </para>
        /// <para>
        /// The index is <c>floor(K·(r/R)²)</c>, which is the inverse of that boundary and needs
        /// no square root. A point on or outside the glass reads the last ring rather than
        /// throwing: float error at the wall is not a world event, and the callers that care
        /// about being outside test <see cref="Inside"/> for themselves.
        /// </para>
        /// </remarks>
        public static int RingOf(double x, double z, double radius, int rings)
        {
            if (rings < 1) return 0;
            if (!(radius > 0d)) return 0;

            double fraction = SquaredRadiusAt(x, z, radius) / (radius * radius);
            int ring = (int)(fraction * rings);

            if (ring < 0) return 0;
            return ring >= rings ? rings - 1 : ring;
        }

        /// <summary>
        /// The mid-radius of ring <paramref name="ring"/>, m — half way between its two
        /// boundaries.
        /// </summary>
        /// <remarks>
        /// What the depth-and-patch overloads address in a tank: a patch is an annulus, so
        /// "at this depth in this patch" has no single answer, and the point on the <c>θ = 0</c>
        /// ray at this radius is the tank's version of the box's centre column
        /// (<see cref="GridField"/>'s <c>CentreCell</c>).
        /// </remarks>
        public static float MidRadiusOf(int ring, float radius, int rings)
        {
            if (rings < 1) rings = 1;
            if (ring < 0) ring = 0;
            if (ring >= rings) ring = rings - 1;

            double inner = Math.Sqrt((double)ring / rings);
            double outer = Math.Sqrt((double)(ring + 1) / rings);

            return (float)(radius * 0.5d * (inner + outer));
        }
    }
}
