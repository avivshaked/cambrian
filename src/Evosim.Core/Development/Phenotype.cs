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

        /// <summary>
        /// A copy of this body at <paramref name="linear"/> times its size on every axis — what a
        /// creature below its adult size actually is, fable-propose-growth.md rule 4 (2026-09-08).
        /// </summary>
        /// <param name="linear">
        /// Length ratio, not volume ratio. A body holding a fraction <c>f</c> of its adult volume
        /// is this at <c>f^(1/3)</c>, and the caller does that conversion because it is the one
        /// holding the tissue ledger.
        /// </param>
        /// <param name="shapes">
        /// Geometry to re-measure the scaled parts with. Defaults to
        /// <see cref="PartShapeRegistry.Standard"/>; a run using custom shapes must pass its own,
        /// exactly as <see cref="Developer.Develop"/> demands.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>The developer's scale folding is the model, and this is deliberately the same
        /// arithmetic.</b> Half-extents, position and both anchors are lengths and scale by
        /// <paramref name="linear"/>; volume and surface are re-measured by the part's own shape
        /// rather than multiplied by a power, so a shape whose volume is not a simple cube of its
        /// extents stays honest. Nothing else about a part changes: a growing creature keeps its
        /// plan, its cell types, its joints and its neurons, and only its size moves.
        /// </para>
        /// <para>
        /// <b>What is deliberately not applied: the two limits.</b> Development thickens a thin
        /// part to <see cref="DevelopmentLimits.MinPartHalfExtent"/> and prunes a subtree outside
        /// the volume bounds, and neither may happen here. The child develops once, at its adult
        /// size, so that the pruning rule judges the adult and a newborn does not lose parts it
        /// would have grown (rule 4); and thickening would break the one identity growth rests on,
        /// that a body's tissue joules are its adult's times its body fraction. A newborn too
        /// small to build is refused at conception by <see cref="RunConfig.MinNewbornPartKilograms"/>
        /// instead, which is a decision about the world rather than a silent repair of a body.
        /// </para>
        /// <para>
        /// The pruning counters are carried across unchanged: they record what development did to
        /// the plan, which is a fact about the genome and not about how grown the creature is.
        /// </para>
        /// </remarks>
        public Phenotype Scaled(float linear, PartShapeRegistry shapes = null)
        {
            if (!(linear > 0f) || float.IsInfinity(linear))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(linear), linear,
                    "A body scale must be finite and positive. Zero is not a small creature, it " +
                    "is a body with no extent, no volume and no anchors.");
            }

            shapes = shapes ?? PartShapeRegistry.Standard;

            var scaled = new Phenotype
            {
                Limits = Limits.Clone(),
                PrunedForVolume = PrunedForVolume,
                PrunedForDepth = PrunedForDepth,
                PrunedForParts = PrunedForParts,
            };

            for (int i = 0; i < _parts.Count; i++)
            {
                PhenotypePart part = _parts[i];
                Float3 halfExtents = part.HalfExtents * linear;
                PartShape shape = shapes.Resolve(part.ShapeId);

                scaled.Add(new PhenotypePart
                {
                    ParentIndex = part.ParentIndex,
                    SourceNode = part.SourceNode,
                    Depth = part.Depth,
                    HalfExtents = halfExtents,
                    Position = part.Position * linear,
                    Rotation = part.Rotation,
                    Mirrored = part.Mirrored,
                    CellTypeId = part.CellTypeId,
                    ShapeId = part.ShapeId,
                    Volume = shape.Volume(halfExtents),
                    SurfaceArea = shape.SurfaceArea(halfExtents),
                    JointType = part.JointType,
                    JointLimits = part.JointLimits,

                    // Torque capacity and lift are not lengths and are not scaled. Power is what
                    // the genome says a link can push with and lift is kg/m³ of displaced water,
                    // so both already mean the same thing at any size — and both are billed per
                    // step, so a half-grown body pays its adult's bill for them. Whether that is
                    // the world we want is a question for a round, not for a copy constructor.
                    Power = part.Power,
                    Lift = part.Lift,
                    ParentAnchorLocal = part.ParentAnchorLocal * linear,
                    ChildAnchorLocal = part.ChildAnchorLocal * linear,
                    Neurons = part.Neurons,
                });
            }

            return scaled;
        }

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
