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

        /// <summary>
        /// How much of a creature's volume is two parts occupying the same space, split by
        /// whether those parts are joined to each other.
        /// </summary>
        public struct OverlapReport
        {
            /// <summary>Overlap between a part and its own parent. Expected, and permitted by DESIGN.md §4.2.</summary>
            public float JointedVolume;

            /// <summary>Overlap between parts that are not joined. Two solids passing through each other.</summary>
            public float UnjointedVolume;

            /// <summary>Total creature volume, counting each part once.</summary>
            public float TotalVolume;

            /// <summary>Unjointed overlap as a fraction of total volume.</summary>
            public float UnjointedFraction => TotalVolume > 1e-9f ? UnjointedVolume / TotalVolume : 0f;

            public override string ToString() =>
                $"jointed {JointedVolume:0.####} m3, unjointed {UnjointedVolume:0.####} m3 ({UnjointedFraction:P1})";
        }

        /// <summary>
        /// Estimates interpenetration volume between every pair of parts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="BuriedPartPairs"/> asks only whether one part's <i>centre</i> lies inside
        /// another, which two boxes can avoid while still slicing deeply through each other.
        /// It was too weak a proxy to support any claim that overlap had been dealt with.
        /// </para>
        /// <para>
        /// The distinction that actually matters is whether the two parts are joined. Overlap
        /// at a joint is deliberate — Sims permitted it and DESIGN.md §4.2 keeps it, because
        /// forbidding it rejects too many viable genomes, and a joint whose parts merely touch
        /// looks like a gap. Overlap between parts that are <i>not</i> connected is two solids
        /// passing through each other, which is what reads as impossible.
        /// </para>
        /// <para>
        /// Estimated by sampling the smaller box of each pair on a regular grid and counting
        /// samples inside the larger. A grid rather than random points so the result is
        /// reproducible; <paramref name="samplesPerAxis"/> cubed samples per pair, so accuracy
        /// costs cubically and 8 is already 512 per pair.
        /// </para>
        /// </remarks>
        public static OverlapReport MeasureOverlap(Phenotype phenotype, int samplesPerAxis = 8)
        {
            var report = new OverlapReport();
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;

            for (int i = 0; i < parts.Count; i++) report.TotalVolume += parts[i].Volume;

            int n = samplesPerAxis < 2 ? 2 : samplesPerAxis;

            for (int a = 0; a < parts.Count; a++)
            {
                for (int b = a + 1; b < parts.Count; b++)
                {
                    PhenotypePart inner = parts[a].Volume <= parts[b].Volume ? parts[a] : parts[b];
                    PhenotypePart outer = ReferenceEquals(inner, parts[a]) ? parts[b] : parts[a];

                    int hits = 0;
                    for (int x = 0; x < n; x++)
                    {
                        float fx = (2f * (x + 0.5f) / n - 1f) * inner.HalfExtents.X;
                        for (int y = 0; y < n; y++)
                        {
                            float fy = (2f * (y + 0.5f) / n - 1f) * inner.HalfExtents.Y;
                            for (int z = 0; z < n; z++)
                            {
                                float fz = (2f * (z + 0.5f) / n - 1f) * inner.HalfExtents.Z;

                                Float3 world = inner.Position + inner.Rotation.Rotate(new Float3(fx, fy, fz));
                                if (outer.ContainsPoint(world)) hits++;
                            }
                        }
                    }

                    if (hits == 0) continue;

                    float overlap = inner.Volume * hits / (float)(n * n * n);
                    bool jointed = parts[a].ParentIndex == parts[b].Index
                                || parts[b].ParentIndex == parts[a].Index;

                    if (jointed) report.JointedVolume += overlap;
                    else report.UnjointedVolume += overlap;
                }
            }

            return report;
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
