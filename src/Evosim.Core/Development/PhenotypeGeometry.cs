using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Geometric measurements over a developed creature. Pure maths, so it can be asserted
    /// on headlessly rather than judged by eye.
    /// </summary>
    public static class PhenotypeGeometry
    {
        /// <summary>
        /// True when <paramref name="point"/> lies inside the box of <paramref name="part"/>.
        /// </summary>
        public static bool ContainsPoint(this PhenotypePart part, Float3 point)
        {
            // Into the part's own frame, where the box is axis-aligned.
            Float3 local = part.Rotation.Conjugate.Rotate(point - part.Position);

            return System.Math.Abs(local.X) <= part.HalfExtents.X
                && System.Math.Abs(local.Y) <= part.HalfExtents.Y
                && System.Math.Abs(local.Z) <= part.HalfExtents.Z;
        }

        /// <summary>
        /// Pairs of parts where one part's centre lies inside the other's box — a part
        /// substantially buried in another rather than merely touching it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Overlap at a joint is allowed on purpose (DESIGN.md §4.2): Sims permitted it, and
        /// forbidding it rejects too many otherwise viable genomes. A part whose <i>centre</i>
        /// is inside another part is past that — it reads as a box inside a box, which is
        /// visibly impossible for solid objects and undercuts the goal of creatures that look
        /// like animals.
        /// </para>
        /// <para>
        /// It also stops being cosmetic at Milestone 2. Fluid forces are computed per part, so
        /// two coincident parts collect drag and thrust twice for one body's worth of volume.
        /// That is a physics exploit in the sense of DESIGN.md §11.2, and one a search would
        /// find quickly, since stacking parts in one place would be free propulsion.
        /// </para>
        /// <para>
        /// A centre-in-box test is deliberately crude — it is a cheap proxy for
        /// interpenetration volume, not a measure of it. It exists to be tracked and driven
        /// down, not to be exact.
        /// </para>
        /// </remarks>
        public static int BuriedPartPairs(Phenotype phenotype)
        {
            int count = 0;
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;

            for (int a = 0; a < parts.Count; a++)
            {
                for (int b = a + 1; b < parts.Count; b++)
                {
                    if (parts[a].ContainsPoint(parts[b].Position) ||
                        parts[b].ContainsPoint(parts[a].Position))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>Parts grown through an odd number of reflections — one half of a mirrored pair.</summary>
        public static int MirroredPartCount(Phenotype phenotype)
        {
            int count = 0;
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].Mirrored) count++;
            }
            return count;
        }
    }
}
