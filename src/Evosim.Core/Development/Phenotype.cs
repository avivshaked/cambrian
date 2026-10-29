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

        /// <summary>
        /// Subtrees dropped because a part's far corner lay past
        /// <see cref="DevelopmentLimits.MaxBodyReachMetres"/>. Zero in every world with the bound off.
        /// </summary>
        public int PrunedForReach { get; internal set; }

        public int PartCount => _parts.Count;

        /// <summary>
        /// True when development stopped early for any reason. Not an error — a genome that
        /// encodes more creature than the caps allow is normal and is simply truncated. It
        /// is worth recording because a population that is mostly truncated means the caps,
        /// not selection, are choosing the body plans.
        /// </summary>
        public bool WasTruncated =>
            PrunedForVolume > 0 || PrunedForDepth > 0 || PrunedForParts > 0 || PrunedForReach > 0;

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
        /// D128: the genome's <see cref="Genome.LifespanScale"/>, carried so the bill can price
        /// repair from the body alone. Set by <see cref="Developer"/> and copied by every copy.
        /// </summary>
        public float LifespanScale { get; internal set; } = 1f;

        /// <summary>
        /// D146: the genome's <see cref="Genome.Brain"/>, the one brain of the central model, or
        /// null for the per-part neurons. Shared with the genome, not copied: it is read once, when
        /// <c>Brain.For</c> builds the creature's network. Set by <see cref="Developer"/> and
        /// carried by every copy.
        /// </summary>
        public CentralBrain Brain { get; internal set; }

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

        // D110. The rest-pose hull's faces as outward area vectors, kept from MeasureSilhouette,
        // and the square of the length ratio this body stands at against them. Shared by
        // reference with every Scaled copy rather than copied, because a copy is made at every
        // growth step of every body and the faces do not change shape with size.
        private Double3[] _hullFaces;
        private double _hullFaceScale = 1.0;

        /// <summary>How many faces the rest-pose hull carries — six when the box stood in.</summary>
        public int HullFaceCount => _hullFaces?.Length ?? 0;

        /// <summary>
        /// The lit area this body's parts present in a pose, m² — <c>Σ LitArea_i · e_i</c>. D110.
        /// </summary>
        /// <param name="exposure">
        /// One factor a part, as <see cref="Organism.PartExposure"/> holds them. Must be one a part.
        /// </param>
        /// <remarks>
        /// Summed in part order in float, the order <see cref="Metabolism"/> bills the parts in.
        /// </remarks>
        public float ExposedLitArea(float[] exposure)
        {
            if (exposure == null) throw new System.ArgumentNullException(nameof(exposure));
            if (exposure.Length != _parts.Count)
            {
                throw new System.ArgumentException(
                    "An exposure array names one factor a part; this one has " + exposure.Length +
                    " for a body of " + _parts.Count + " parts, so it describes another plan.",
                    nameof(exposure));
            }

            float sum = 0f;
            for (int i = 0; i < _parts.Count; i++) sum += _parts[i].LitArea * exposure[i];
            return sum;
        }

        /// <summary>
        /// The whole body's shadow in a pose, m²: its rest-pose hull projected onto the plane
        /// normal to <paramref name="upInBody"/>, the world's up brought into the body's own frame.
        /// D110's cap, as <see cref="SilhouetteArea"/> is D099's.
        /// </summary>
        /// <remarks>
        /// The one-sided sum over the faces, so a face turned away from the light counts nothing
        /// and a closed hull's sum is its shadow. Where the hull fell back to the box, the box's
        /// three face pairs stand in, as they do for the surface.
        /// </remarks>
        public float ShadowArea(Float3 upInBody)
        {
            if (_hullFaces == null || _hullFaces.Length == 0) return 0f;

            double shadow = ConvexHull.ProjectedArea(
                _hullFaces, new Double3(upInBody.X, upInBody.Y, upInBody.Z));
            return (float)(shadow * _hullFaceScale);
        }

        /// <summary>
        /// <see cref="LitAreaFactor(bool)"/> in a pose: <c>min(1, ShadowArea / ExposedLitArea)</c>
        /// with the cap on, 1 without it. D110.
        /// </summary>
        public float LitAreaFactor(float[] exposure, Float3 upInBody, bool capOn)
        {
            if (!capOn) return 1f;

            float exposed = ExposedLitArea(exposure);
            if (!(exposed > 0f)) return 1f;

            float factor = ShadowArea(upInBody) / exposed;
            return factor < 1f ? factor : 1f;
        }

        /// <summary>
        /// The lit area this body earns on and casts as its shadow in a pose, m² —
        /// <see cref="ExposedLitArea"/> times <see cref="LitAreaFactor(float[], Float3, bool)"/>.
        /// D110's shading side, and the same quantity <see cref="Metabolism"/> bills the parts on.
        /// </summary>
        public float EffectiveLitArea(float[] exposure, Float3 upInBody, bool capOn) =>
            ExposedLitArea(exposure) * LitAreaFactor(exposure, upInBody, capOn);

        /// <summary>
        /// Measures <see cref="SilhouetteArea"/> over the parts as they now stand. Called once by
        /// <see cref="Developer.Develop"/> when the body is complete.
        /// </summary>
        internal void MeasureSilhouette()
        {
            SilhouetteFellBackToBox = 0;
            _hullFaceScale = 1.0;

            if (_parts.Count == 0)
            {
                SilhouetteArea = 0f;
                _hullFaces = System.Array.Empty<Double3>();
                return;
            }

            var corners = new List<Double3>(_parts.Count * 8);
            for (int i = 0; i < _parts.Count; i++) ConvexHull.AppendPartCorners(_parts[i], corners);

            var faces = new List<Double3>();
            double surface = ConvexHull.SurfaceArea(corners, out bool fellBack, out _, faces);
            if (fellBack) SilhouetteFellBackToBox = 1;

            SilhouetteArea = (float)(surface / 4.0);
            _hullFaces = faces.ToArray();
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
        /// <see cref="PartShapeRegistry.Catalogue"/>, which answers geometry for every shape the build
        /// knows (D233); a run passes its own world list, exactly as <see cref="Developer.Develop"/> does.
        /// </param>
        /// <param name="scalePower">
        /// D241: scale each part's <see cref="PhenotypePart.Power"/> by the cube of
        /// <paramref name="linear"/>, so a juvenile's joints are as strong as its muscle is. False, the
        /// default, copies the adult's Power across, which is every recorded world. A world's callers go
        /// through <see cref="ScaledFor"/>, which reads <see cref="RunConfig.JuvenileStrengthScales"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>The developer's scale folding is the model, and this is deliberately the same
        /// arithmetic.</b> Half-extents, position and both anchors are lengths and scale by
        /// <paramref name="linear"/>; volume and surface are re-measured by the part's own shape
        /// rather than multiplied by a power, so a shape whose volume is not a simple cube of its
        /// extents stays honest. The one exception is a family part's surface (D233): it has no closed
        /// formula and costs a measurement of thousands of points, so the copy shares the adult's
        /// <see cref="FamilyGeometry"/> and takes the area as its reference times the square of the
        /// length ratio, which is exact for a uniform scaling. Nothing else about a part changes: a growing creature keeps its
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
        public Phenotype Scaled(float linear, PartShapeRegistry shapes = null, bool scalePower = false)
        {
            if (!(linear > 0f) || float.IsInfinity(linear))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(linear), linear,
                    "A body scale must be finite and positive. Zero is not a small creature, it " +
                    "is a body with no extent, no volume and no anchors.");
            }

            // A scale of exactly 1 is this body. Every caller takes the cube root of a volume
            // fraction, and a fraction within a float's rounding of 1 (a body one growth step
            // from adult, a newborn whose investment rounds to whole) gives a linear scale that
            // rounds to exactly 1f — and a copy at 1f has every number the adult has and none of
            // its identity: World.Grow's ReferenceEquals reads it as not adult, and the
            // checkpoint writer, whose contract is "the adult or one Scaled copy of it", refused
            // it (round 45's first launch, 2026-09-22, three seeds at their first checkpoint
            // with a bitten or grown body in the crowd). Returning the body itself changes no
            // number downstream: the tissue of a copy at 1f and of the adult are the same
            // arithmetic on the same extents.
            if (linear == 1f) return this;

            shapes = shapes ?? PartShapeRegistry.Catalogue;

            var scaled = new Phenotype
            {
                Limits = Limits.Clone(),
                PrunedForVolume = PrunedForVolume,
                PrunedForDepth = PrunedForDepth,
                PrunedForParts = PrunedForParts,
                PrunedForReach = PrunedForReach,

                // An area, so it scales by the square of a length — no rebuild of the hull. Every
                // corner of every part moves by the same factor about the same origin, so the
                // scaled cloud's hull is the same hull scaled, and its surface is exactly this.
                // Re-measuring would give the same number a rounding apart and cost a hull per
                // birth and per growth step.
                SilhouetteArea = SilhouetteArea * linear * linear,
                SilhouetteFellBackToBox = SilhouetteFellBackToBox,

                // D110's faces, by the same argument: the same hull at a length ratio, so the
                // same faces at its square, and nothing is built.
                _hullFaces = _hullFaces,
                _hullFaceScale = _hullFaceScale * linear * linear,

                // What this copy was made at, so a checkpoint can make it again rather than
                // infer it from a tissue fraction that does not carry it. See ScaledBy.
                ScaledBy = linear,
                LifespanScale = LifespanScale,
                Brain = Brain,
            };

            for (int i = 0; i < _parts.Count; i++)
            {
                PhenotypePart part = _parts[i];
                Float3 halfExtents = part.HalfExtents * linear;
                PartShape shape = shapes.Resolve(part.ShapeId);
                float familyScale = part.Family != null ? part.FamilyScale * linear : 1f;

                scaled.Add(new PhenotypePart
                {
                    ParentIndex = part.ParentIndex,
                    SourceNode = part.SourceNode,
                    Depth = part.Depth,
                    HalfExtents = halfExtents,
                    Position = part.Position * linear,

                    // D113: a length like the position it is measured from, so the support cost a
                    // growing body pays is on the distance its grown parts stand at.
                    DistanceFromRoot = part.DistanceFromRoot * linear,
                    Rotation = part.Rotation,
                    Mirrored = part.Mirrored,
                    ReflectedDrive = part.ReflectedDrive,
                    CellTypeId = part.CellTypeId,
                    ShapeId = part.ShapeId,

                    // D233. A family part shares its geometry with the adult it was scaled from and
                    // reads its panels through a scale, so growth measures no surface: the area is
                    // the reference area times the square of the length ratio, and nothing is built.
                    // Volume is the shape's, at the size and with the form, as a named part's is.
                    Form = part.Form,
                    Family = part.Family,
                    FamilyScale = familyScale,
                    Volume = shape.Volume(halfExtents, part.Form),
                    SurfaceArea = part.Family != null
                        ? part.Family.ReferenceSurfaceArea * (familyScale * familyScale)
                        : shape.SurfaceArea(halfExtents),
                    JointType = part.JointType,
                    JointLimits = part.JointLimits,

                    // Lift is kg/m³ of displaced water, so it means the same thing at any size, and
                    // it is billed per step, so a half-grown body pays its adult's bill for it.
                    // Power is the genome's peak torque of a link. With scalePower off (every
                    // recorded world) it is copied across, so a juvenile has its adult's torque on
                    // smaller limbs, and pays its adult's idle bill for it. With scalePower on
                    // (D241, RunConfig.JuvenileStrengthScales) it scales as muscle does: force with
                    // cross-section (L²) and moment arm with L, so torque with L³, which is the mass
                    // fraction. The idle bill reads Power, so it falls with it. Whether that is the
                    // world we want is a question for a round, not for a copy constructor.
                    Power = scalePower ? part.Power * linear * linear * linear : part.Power,
                    Drive = part.Drive,
                    Lift = part.Lift,

                    // D106 item 3's four, on the same argument one field along: attack, intake and
                    // protection are rates per square metre and toughness is health per cubic
                    // metre, so all four already mean the same thing at any size. What a growing
                    // body gains is the area and the volume they are multiplied by, which is the
                    // half-extents above.
                    Attack = part.Attack,
                    Intake = part.Intake,
                    Protection = part.Protection,
                    Toughness = part.Toughness,

                    // D111: a fraction of the half-extent, so it means the same at any size.
                    BuoyancyOffset = part.BuoyancyOffset,
                    ParentAnchorLocal = part.ParentAnchorLocal * linear,
                    ChildAnchorLocal = part.ChildAnchorLocal * linear,
                    Neurons = part.Neurons,
                    PathId = part.PathId,
                    Smells = part.Smells,
                });
            }

            return scaled;
        }

        /// <summary>
        /// <see cref="Scaled"/> as the world's rules ask for it: the shapes are the config's, and each
        /// part's Power scales with the cube of the length ratio when
        /// <see cref="RunConfig.JuvenileStrengthScales"/> is on (D241). Every caller in the world and the
        /// farm goes through this, so a body is made one way at birth, at a growth step, at a checkpoint
        /// and in the harness.
        /// </summary>
        public Phenotype ScaledFor(RunConfig config, float linear)
        {
            if (config == null) throw new System.ArgumentNullException(nameof(config));
            return Scaled(linear, config.Shapes, config.JuvenileStrengthScales);
        }

        /// <summary>
        /// A copy of this body with the parts <paramref name="drop"/> marks, and everything
        /// hanging from them, gone — D106 item 1's kill and <c>logbook/specs/mouth-spec.md</c>
        /// rule 4.
        /// </summary>
        /// <param name="drop">
        /// One entry per part; true removes that part and its whole subtree. A dropped root gives
        /// a body of no parts, which is a death and not a body (<c>World.KillPart</c> handles it
        /// before it gets here).
        /// </param>
        /// <param name="map">
        /// Filled with, for each part of the copy, its index in this body. The inverse of what
        /// <see cref="Developer.MatchParts"/> produces, and the same thing the harness's rebuild
        /// consumes.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>A surgery and not a development.</b> Development expresses a genome at a set of
        /// counts; a part that was bitten off is not expressible that way — nothing in the genome
        /// says "this occurrence of this node is gone" — so the body is cut instead. What makes
        /// the cut safe is that a part's geometry is absolute in the creature's own frame, so
        /// removing one moves nothing: the survivors keep their positions, their rotations and
        /// their anchors exactly, and only the indices close up.
        /// </para>
        /// <para>
        /// <b>Pre-order is what makes one forward pass enough.</b>
        /// <see cref="PhenotypePart.ParentIndex"/> is always below <see cref="PhenotypePart.Index"/>
        /// (<see cref="PhenotypePart"/>'s remarks), so a part's parent has already been decided by
        /// the time the part is reached and a subtree falls by inheritance rather than by search.
        /// </para>
        /// <para>
        /// <b>The silhouette is re-measured and the lit area is not.</b> The lit area is a sum over
        /// parts and <see cref="Add"/> accumulates it, so it comes out right by construction; the
        /// hull is a property of the whole cloud and a body with a limb missing casts a smaller
        /// shadow, which it must, because it also earns on it (<see cref="EffectiveLitArea"/>).
        /// </para>
        /// </remarks>
        public Phenotype WithoutSubtrees(bool[] drop, out int[] map)
        {
            if (drop == null) throw new System.ArgumentNullException(nameof(drop));

            var keep = new bool[_parts.Count];
            int kept = 0;

            for (int i = 0; i < _parts.Count; i++)
            {
                int parent = _parts[i].ParentIndex;
                keep[i] = (parent < 0 || keep[parent]) && !(i < drop.Length && drop[i]);
                if (keep[i]) kept++;
            }

            var cut = new Phenotype
            {
                Limits = Limits.Clone(),
                PrunedForVolume = PrunedForVolume,
                PrunedForDepth = PrunedForDepth,
                PrunedForParts = PrunedForParts,
                PrunedForReach = PrunedForReach,
                ScaledBy = ScaledBy,
                LifespanScale = LifespanScale,
                Brain = Brain,
            };

            map = new int[kept];

            var moved = new int[_parts.Count];
            for (int i = 0; i < moved.Length; i++) moved[i] = -1;

            for (int i = 0; i < _parts.Count; i++)
            {
                if (!keep[i]) continue;

                PhenotypePart part = _parts[i];
                int at = cut.PartCount;

                moved[i] = at;
                map[at] = i;

                cut.Add(new PhenotypePart
                {
                    ParentIndex = part.ParentIndex < 0 ? -1 : moved[part.ParentIndex],
                    SourceNode = part.SourceNode,
                    Depth = part.Depth,
                    HalfExtents = part.HalfExtents,
                    Position = part.Position,
                    DistanceFromRoot = part.DistanceFromRoot,
                    Rotation = part.Rotation,
                    Mirrored = part.Mirrored,
                    ReflectedDrive = part.ReflectedDrive,
                    CellTypeId = part.CellTypeId,
                    ShapeId = part.ShapeId,
                    Form = part.Form,
                    Family = part.Family,
                    FamilyScale = part.FamilyScale,
                    Volume = part.Volume,
                    SurfaceArea = part.SurfaceArea,
                    JointType = part.JointType,
                    JointLimits = part.JointLimits,
                    Power = part.Power,
                    Drive = part.Drive,
                    Lift = part.Lift,
                    Attack = part.Attack,
                    Intake = part.Intake,
                    Protection = part.Protection,
                    Toughness = part.Toughness,
                    BuoyancyOffset = part.BuoyancyOffset,
                    ParentAnchorLocal = part.ParentAnchorLocal,
                    ChildAnchorLocal = part.ChildAnchorLocal,
                    Neurons = part.Neurons,
                    PathId = part.PathId,
                    Smells = part.Smells,
                });
            }

            cut.MeasureSilhouette();
            return cut;
        }

        /// <summary>
        /// D220, contact eating: the part at <paramref name="partIndex"/> and everything that hangs from it, as a body
        /// of its own, re-rooted at that part.
        /// </summary>
        /// <param name="partIndex">The part that becomes the new root.</param>
        /// <param name="map">
        /// Filled with, for each part of the copy, its index in this body: ascending, and starting at
        /// <paramref name="partIndex"/>. The direction <see cref="WithoutSubtrees"/> reports and
        /// <see cref="Developer.MatchParts"/> produces.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>A surgery, as <see cref="WithoutSubtrees"/> is, and the same one seen from the other side.</b> A part's
        /// geometry is absolute in the creature's own frame, so cutting a limb moves nothing: the copy keeps every
        /// part's position, rotation and extents, and only the indices close up. What changes is the new root: it
        /// has no parent, so its joint, its anchors and its torque capacity are those of a root (a fixed joint, zero
        /// anchors, no power), and every depth is counted from it. Cutting at the root is the whole body, unchanged.
        /// </para>
        /// <para>
        /// This body and the copy cut off it partition it: the copy's parts and
        /// <c>WithoutSubtrees(drop)</c> with only <paramref name="partIndex"/> marked together name every part
        /// once.
        /// </para>
        /// </remarks>
        public Phenotype SubtreeAt(int partIndex, out int[] map)
        {
            if (partIndex < 0 || partIndex >= _parts.Count)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(partIndex), partIndex, "A body of " + _parts.Count + " parts has no part " + partIndex + ".");
            }

            var member = new bool[_parts.Count];
            member[partIndex] = true;
            int count = 1;

            for (int i = partIndex + 1; i < _parts.Count; i++)
            {
                int parent = _parts[i].ParentIndex;
                if (parent >= partIndex && member[parent])
                {
                    member[i] = true;
                    count++;
                }
            }

            map = new int[count];
            int at = 0;
            for (int i = partIndex; i < _parts.Count; i++) if (member[i]) map[at++] = i;

            return Select(map);
        }

        /// <summary>
        /// A copy holding only the parts <paramref name="map"/> names, in that order: part <c>k</c> of the copy is
        /// part <c>map[k]</c> of this body. D220, contact eating.
        /// </summary>
        /// <param name="map">
        /// Strictly ascending part indices. The first is the copy's root, and every other entry's parent is also in
        /// the list: a body is a tree, and a part with no parent but the root is not a part of it.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>One function for every cut of remains, and a checkpoint's way back.</b> A cut-off limb is this body
        /// selected at a subtree; the remains of a remains are that selected again; and a checkpoint stores the
        /// body a remains was cut from and one composed map, which selects the same parts from the same source.
        /// Selecting twice gives the copy selecting once with the maps composed gives, to the bit, because each part
        /// is copied from its source part by itself and a re-rooting depends on that part alone.
        /// </para>
        /// <para>
        /// A part whose parent is not in the list is re-rooted only if it is the first; one that is not the first
        /// refuses the call. A part that is already a root is copied as it is.
        /// </para>
        /// </remarks>
        public Phenotype Select(int[] map)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            if (map.Length == 0) throw new System.ArgumentException("A selection of no parts is not a body.", nameof(map));

            var moved = new int[_parts.Count];
            for (int i = 0; i < moved.Length; i++) moved[i] = -1;

            for (int k = 0; k < map.Length; k++)
            {
                int s = map[k];
                if (s < 0 || s >= _parts.Count || (k > 0 && s <= map[k - 1]))
                {
                    throw new System.ArgumentException(
                        "A selection names strictly ascending parts of the body; entry " + k + " is " + s + ".", nameof(map));
                }

                moved[s] = k;
            }

            int rootDepth = _parts[map[0]].Depth;

            var cut = new Phenotype
            {
                Limits = Limits.Clone(),
                PrunedForVolume = PrunedForVolume,
                PrunedForDepth = PrunedForDepth,
                PrunedForParts = PrunedForParts,
                PrunedForReach = PrunedForReach,
                ScaledBy = ScaledBy,
                LifespanScale = LifespanScale,
                Brain = Brain,
            };

            for (int k = 0; k < map.Length; k++)
            {
                PhenotypePart part = _parts[map[k]];
                bool asRoot = k == 0 && part.ParentIndex >= 0;

                int parent = part.ParentIndex < 0 || asRoot ? -1 : moved[part.ParentIndex];
                if (k > 0 && parent < 0)
                {
                    throw new System.ArgumentException(
                        "Part " + map[k] + " is selected without its parent " + part.ParentIndex + ".", nameof(map));
                }

                cut.Add(new PhenotypePart
                {
                    ParentIndex = parent,
                    SourceNode = part.SourceNode,
                    Depth = part.Depth - rootDepth,
                    HalfExtents = part.HalfExtents,
                    Position = part.Position,
                    DistanceFromRoot = part.DistanceFromRoot,
                    Rotation = part.Rotation,
                    Mirrored = part.Mirrored,
                    ReflectedDrive = part.ReflectedDrive,
                    CellTypeId = part.CellTypeId,
                    ShapeId = part.ShapeId,
                    Form = part.Form,
                    Family = part.Family,
                    FamilyScale = part.FamilyScale,
                    Volume = part.Volume,
                    SurfaceArea = part.SurfaceArea,
                    JointType = asRoot ? JointType.Fixed : part.JointType,
                    JointLimits = asRoot ? System.Array.Empty<Float2>() : part.JointLimits,
                    Power = asRoot ? 0f : part.Power,
                    Drive = asRoot ? default(DriveGenes) : part.Drive,
                    Lift = part.Lift,
                    Attack = part.Attack,
                    Intake = part.Intake,
                    Protection = part.Protection,
                    Toughness = part.Toughness,
                    BuoyancyOffset = part.BuoyancyOffset,
                    ParentAnchorLocal = asRoot ? default(Float3) : part.ParentAnchorLocal,
                    ChildAnchorLocal = asRoot ? default(Float3) : part.ChildAnchorLocal,
                    Neurons = part.Neurons,
                    PathId = part.PathId,
                    Smells = part.Smells,
                });
            }

            cut.MeasureSilhouette();
            return cut;
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
