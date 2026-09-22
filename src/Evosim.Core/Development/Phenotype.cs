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
        /// The whole body's shadow, m² — the orientation-averaged projected area of its convex
        /// hull, which is a quarter of that hull's surface by the same Cauchy formula a part
        /// uses. D099, 2026-09-19. DESIGN.md §5A.2b.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a body needs one at all.</b> <see cref="TotalLitArea"/> adds the parts up as
        /// though none of them stood in front of another, which is true of a body laid out in
        /// the open and false of a body folded into a ball. Round 41c grew sixteen links into a
        /// knot a metre across and billed the sun for sixteen links' worth of sunlit surface out
        /// of one silhouette (logbook/0107). The hull is the convex shape the body actually
        /// blocks the light with, and nothing inside it can be lit.
        /// </para>
        /// <para>
        /// Measured once, when development finishes, over every part's oriented-box corners. A
        /// sphere and a capsule contribute their half-extent box, which over-states them on the
        /// safe side: the cap can only be looser for a round body.
        /// </para>
        /// </remarks>
        public float SilhouetteArea { get; private set; }

        /// <summary>
        /// How many times the hull degenerated and the axis-aligned box stood in for it — 0 or 1
        /// for a developed body, carried through <see cref="Scaled"/>.
        /// </summary>
        /// <remarks>
        /// A one-part body, or any body whose corners are coplanar or collinear, has no hull with
        /// an interior. The bounding box is the honest stand-in and it is counted rather than
        /// substituted silently, because a population that is mostly falling back is measuring
        /// something other than what this claims to measure.
        /// </remarks>
        public int SilhouetteFellBackToBox { get; private set; }

        /// <summary>
        /// The length ratio this body was made at by <see cref="Scaled"/>, or 1 for a body the
        /// developer built.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Recorded so that a body can be rebuilt and not merely re-measured.</b> A living
        /// creature's body is always either the developed adult itself or exactly one call of
        /// <c>adult.Scaled(s)</c>, and <see cref="Organism.BodyFraction"/> does not recover
        /// <c>s</c>: the fraction is re-measured from the scaled body's own tissue, so it differs
        /// from the volume ratio the caller asked for by whatever the shapes' re-measurement and
        /// the float rounding did. A checkpoint that stored the fraction and scaled by its cube
        /// root would restore a body a rounding away from the one it saved, and a rounding is a
        /// different realisation from the next step on (CLAUDE.md's butterfly note).
        /// </para>
        /// <para>
        /// It is the argument this copy was made with, relative to whatever it was made from, and
        /// not an accumulated product — nothing in the world scales a body that is already
        /// scaled, because <c>World.Grow</c> scales the adult at every growth step and
        /// <see cref="Organism.AdultPhenotype"/>'s remark says why.
        /// </para>
        /// <para>
        /// Pure bookkeeping: nothing in the economy reads it and it is in no hash.
        /// </para>
        /// </remarks>
        public float ScaledBy { get; private set; } = 1f;

        /// <summary>
        /// What a body's summed lit area has to be multiplied by before it earns or shades:
        /// <c>min(1, SilhouetteArea / TotalLitArea)</c> with the cap on, 1 without it.
        /// </summary>
        /// <param name="capOn">
        /// <see cref="RunConfig.LightSilhouetteCap"/>. False is every world before D099 and the
        /// default, so every recorded config still describes the world it ran.
        /// </param>
        /// <remarks>
        /// <b>Never above 1.</b> A single round part's hull is drawn round its bounding box and
        /// so has more surface than the part does; the cap is a ceiling on what a body may claim,
        /// not a correction that can hand it more.
        /// </remarks>
        public float LitAreaFactor(bool capOn)
        {
            if (!capOn) return 1f;
            if (!(TotalLitArea > 0f)) return 1f;

            float factor = SilhouetteArea / TotalLitArea;
            return factor < 1f ? factor : 1f;
        }

        /// <summary>
        /// The lit area this body earns on and casts as its shadow, m² — <see cref="TotalLitArea"/>
        /// times <see cref="LitAreaFactor"/>.
        /// </summary>
        /// <remarks>
        /// <b>Both sides or neither.</b> The light field is contributed this and each part earns
        /// on its share of it, so a body still denies below it exactly what it collects, which is
        /// the property §5A.2b's shading rests on.
        /// </remarks>
        public float EffectiveLitArea(bool capOn) => TotalLitArea * LitAreaFactor(capOn);

        /// <summary>
        /// Measures <see cref="SilhouetteArea"/> over the parts as they now stand. Called once by
        /// <see cref="Developer.Develop"/> when the body is complete.
        /// </summary>
        internal void MeasureSilhouette()
        {
            SilhouetteFellBackToBox = 0;

            if (_parts.Count == 0)
            {
                SilhouetteArea = 0f;
                return;
            }

            var corners = new List<Double3>(_parts.Count * 8);
            for (int i = 0; i < _parts.Count; i++) ConvexHull.AppendPartCorners(_parts[i], corners);

            double surface = ConvexHull.SurfaceArea(corners, out bool fellBack, out _);
            if (fellBack) SilhouetteFellBackToBox = 1;

            SilhouetteArea = (float)(surface / 4.0);
        }

        /// <summary>
        /// How many pairs of parts stand inside each other — a part and its own parent excluded,
        /// and only counting a pair buried deeper than <paramref name="depthFraction"/> of the
        /// smaller part's thinnest half-extent. The owner's ruling of 2026-09-19.
        /// </summary>
        /// <param name="depthFraction">
        /// <see cref="RunConfig.SelfOverlapDepthFraction"/>. The threshold is relative to the
        /// smaller box, so the answer is the same for a body at any size — see
        /// <see cref="BoxOverlap.DepthThreshold"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Why the parent is excluded.</b> Overlap at a joint is deliberate (DESIGN.md §4.2,
        /// and <see cref="PhenotypeGeometry.MeasureOverlap"/> has reported it separately since
        /// the jamming measurements), and PhysX never collides two directly jointed links, so
        /// such a pair costs nothing and hides nothing. What this counts is the pairs the solver
        /// does collide and cannot separate: round 41c's knot and round 41d's sprawl, where a
        /// body grown into itself paid the physics for a contact on every step of its life
        /// (logbook/0107).
        /// </para>
        /// <para>
        /// <b>A depth and not a touch.</b> Two boxes that share a face intersect by a rounding,
        /// and refusing a body for that would refuse most plans that fit together at all. The
        /// fraction is what separates a seam from a part buried in another.
        /// </para>
        /// <para>
        /// Measured on demand rather than cached: a body is at most
        /// <see cref="DevelopmentLimits.MaxParts"/> parts, so this is at most a hundred and
        /// twenty separating-axis tests, and the world asks it once per birth.
        /// </para>
        /// </remarks>
        public int SelfOverlappingPairs(float depthFraction)
        {
            if (_parts.Count < 2) return 0;

            var boxes = new BoxOverlap.Obb[_parts.Count];
            for (int i = 0; i < _parts.Count; i++) boxes[i] = BoxOverlap.Obb.From(_parts[i]);

            int count = 0;
            for (int i = 0; i < _parts.Count; i++)
            {
                for (int j = i + 1; j < _parts.Count; j++)
                {
                    if (_parts[j].ParentIndex == i || _parts[i].ParentIndex == j) continue;

                    if (!BoxOverlap.Obb.Intersect(boxes[i], boxes[j], out float depth)) continue;
                    if (depth > BoxOverlap.DepthThreshold(boxes[i], boxes[j], depthFraction)) count++;
                }
            }

            return count;
        }

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

                // An area, so it scales by the square of a length — no rebuild of the hull. Every
                // corner of every part moves by the same factor about the same origin, so the
                // scaled cloud's hull is the same hull scaled, and its surface is exactly this.
                // Re-measuring would give the same number a rounding apart and cost a hull per
                // birth and per growth step.
                SilhouetteArea = SilhouetteArea * linear * linear,
                SilhouetteFellBackToBox = SilhouetteFellBackToBox,

                // What this copy was made at, so a checkpoint can make it again rather than
                // infer it from a tissue fraction that does not carry it. See ScaledBy.
                ScaledBy = linear,
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
