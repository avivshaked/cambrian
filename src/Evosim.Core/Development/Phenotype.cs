using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// A developed creature: always a tree, mapping cleanly onto a PhysX articulation
    /// (DESIGN.md §4.2).
    /// </summary>
    public sealed class Phenotype
    {
        private readonly List<PhenotypePart> _parts = new List<PhenotypePart>();

        public IReadOnlyList<PhenotypePart> Parts => _parts;

        /// <summary>Limits development ran under. Retained so a phenotype can say why it stopped.</summary>
        public DevelopmentLimits Limits { get; internal set; } = DevelopmentLimits.Default;

        /// <summary>Subtrees dropped because the part would have fallen below the minimum volume.</summary>
        public int PrunedForVolume { get; internal set; }

        /// <summary>Subtrees dropped because <see cref="DevelopmentLimits.MaxDepth"/> was reached.</summary>
        public int PrunedForDepth { get; internal set; }

        /// <summary>Subtrees dropped because <see cref="DevelopmentLimits.MaxParts"/> was reached.</summary>
        public int PrunedForParts { get; internal set; }

        public int PartCount => _parts.Count;

        /// <summary>
        /// True when development stopped early for any reason. Not an error — a genome that
        /// encodes more creature than the caps allow is normal and is simply truncated. It
        /// is worth recording because a population that is mostly truncated means the caps,
        /// not selection, are choosing the body plans.
        /// </summary>
        public bool WasTruncated => PrunedForVolume > 0 || PrunedForDepth > 0 || PrunedForParts > 0;

        public float TotalVolume
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _parts.Count; i++) sum += _parts[i].Volume;
                return sum;
            }
        }

        public int MaxDepthReached
        {
            get
            {
                int max = 0;
                for (int i = 0; i < _parts.Count; i++)
                {
                    if (_parts[i].Depth > max) max = _parts[i].Depth;
                }
                return max;
            }
        }

        /// <summary>Total actuated degrees of freedom — one effector each (DESIGN.md §4.4).</summary>
        public int TotalDof
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _parts.Count; i++) sum += _parts[i].JointType.DofCount();
                return sum;
            }
        }

        /// <summary>
        /// Total lit area, m² — the shadow this creature casts, and its claim on the world's
        /// light. DESIGN.md §5A.2b.
        /// </summary>
        /// <remarks>
        /// <b>This is a projected area, not a surface area, and that is what makes it a shadow.</b>
        /// Each part's <see cref="PhenotypePart.LitArea"/> is a quarter of its surface, which by
        /// Cauchy's formula is exactly its orientation-averaged projected area — so summing them
        /// gives the mean area the creature blocks when seen from above. That is precisely the
        /// quantity <see cref="LightField"/> needs, and it is the same number the creature earns
        /// on, which is what makes shading self-consistent: nothing can collect light it does not
        /// also deny to whatever is below it.
        ///
        /// Accumulated as parts are added, because it is read once per creature per step and parts
        /// are never removed.
        /// </remarks>
        public float TotalLitArea { get; private set; }

        internal PhenotypePart Add(PhenotypePart part)
        {
            part.Index = _parts.Count;
            _parts.Add(part);
            TotalLitArea += part.LitArea;
            return part;
        }

        /// <summary>Indices of the children of <paramref name="partIndex"/>.</summary>
        public IEnumerable<int> ChildrenOf(int partIndex)
        {
            for (int i = partIndex + 1; i < _parts.Count; i++)
            {
                if (_parts[i].ParentIndex == partIndex) yield return i;
            }
        }

        public override string ToString() =>
            $"Phenotype({PartCount} parts, depth {MaxDepthReached}, {TotalDof} DOF{(WasTruncated ? ", truncated" : "")})";
    }
}
