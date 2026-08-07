using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// A built creature: the GameObjects and articulation bodies for one phenotype.
    /// </summary>
    public sealed class CreatureInstance
    {
        public GameObject Root { get; internal set; }
        public ArticulationBody[] Bodies { get; internal set; }
        public Phenotype Phenotype { get; internal set; }

        /// <summary>Index of the first actuated DOF for each body, or -1 where the joint is fixed.</summary>
        public int[] DofOffset { get; internal set; }

        /// <summary>Every renderer in the creature, and the part each one belongs to.</summary>
        /// <remarks>
        /// Kept so the cell-type view can be toggled without rebuilding — a capsule draws with
        /// three renderers, so there is no one-to-one mapping to recover afterwards.
        /// </remarks>
        public MeshRenderer[] Renderers { get; internal set; }
        public int[] RendererPart { get; internal set; }

        /// <summary>Total actuated degrees of freedom across the creature.</summary>
        public int TotalDof { get; internal set; }

        /// <summary>
        /// Drag panels for each part, built once. Owned by <see cref="FluidEnvironment"/>.
        /// </summary>
        /// <remarks>
        /// A part's local geometry is fixed the moment it is developed, but panels were rebuilt
        /// from the <see cref="PartShape"/> on every part on every step — which §5A.9 measured as
        /// the largest single term in the simulation. Cached here rather than in the environment
        /// because they belong to the creature: they describe its body, and they die with it.
        /// <see cref="DragPanelsPerAxis"/> records the resolution they were built at so that an
        /// environment configured differently rebuilds instead of silently using someone else's.
        /// </remarks>
        public DragPanelSet[] DragPanels { get; internal set; }

        /// <summary><see cref="FluidConfig.PanelsPerAxis"/> that <see cref="DragPanels"/> was built at.</summary>
        public int DragPanelsPerAxis { get; internal set; }

        public void Destroy()
        {
            if (Root == null) return;

            if (Application.isPlaying) Object.Destroy(Root);
            else Object.DestroyImmediate(Root);

            Root = null;
        }
    }

    /// <summary>
    /// Turns a developed <see cref="Phenotype"/> into a PhysX articulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ArticulationBody, not Rigidbody + ConfigurableJoint</b> (DESIGN.md §6.2) — creatures
    /// are articulated kinematic trees, which is what PhysX articulations exist for, and they
    /// are far more stable under the high joint torques evolution will discover.
    /// Spike 01 measured build + teardown of a 10-part creature at 0.335 ms against a 15 ms
    /// budget, so rebuilding per evaluation is affordable and no pooling is needed
    /// (DECISIONS.md D010).
    /// </para>
    /// <para>
    /// The articulation must be constructed parent-first: an ArticulationBody added to a
    /// GameObject whose ancestor already has one becomes a child link, and the ordering is
    /// fixed at add time. <see cref="Developer"/> emits parts in depth-first pre-order for
    /// exactly this reason, so a single forward pass over <see cref="Phenotype.Parts"/> is
    /// correct by construction.
    /// </para>
    /// </remarks>
    public static class PhenotypeBuilder
    {
        /// <summary>
        /// Layer creatures are placed on. Spike 01 disabled collisions entirely via
        /// <c>Physics.IgnoreLayerCollision</c>; tiled creatures in open water never touch,
        /// and self-collision is deliberately not enforced — Sims permitted overlap at
        /// joints, and forbidding it kills too many otherwise viable genomes
        /// (DESIGN.md §4.2). This changes at Milestone 5, when land needs contact.
        /// </summary>
        public const int CreatureLayer = 8;

        /// <summary>
        /// Part density in kg/m³. Water is 1000, and DESIGN.md §5 puts creatures in water
        /// first, so a neutrally buoyant body is the honest default. Until fluid forces land
        /// at Milestone 2 this only sets inertia — but it also sets the mass that §4.4's
        /// effector scaling divides by, so it is not cosmetic.
        /// </summary>
        public const float DensityKgPerM3 = 1000f;

        /// <param name="shapes">
        /// Geometry each part's shape id resolves against. Must be the same registry the
        /// phenotype was developed with and the same one the fluid model is given, since all
        /// three have to agree on how large a part is.
        /// </param>
        public static CreatureInstance Build(
            Phenotype phenotype,
            Vector3 origin,
            Transform parent = null,
            PartShapeRegistry shapes = null)
        {
            if (phenotype == null || phenotype.PartCount == 0)
            {
                throw new System.ArgumentException("Cannot build an empty phenotype.", nameof(phenotype));
            }

            shapes = shapes ?? PartShapeRegistry.Standard;

            var root = new GameObject("Creature");
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = origin;

            var bodies = new ArticulationBody[phenotype.PartCount];
            var transforms = new Transform[phenotype.PartCount];
            var dofOffset = new int[phenotype.PartCount];
            int dofCursor = 0;

            _renderers.Clear();
            _rendererPart.Clear();

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];

                var go = new GameObject($"Part{i:00}_n{part.SourceNode}") { layer = CreatureLayer };

                // EVERY part transform stays at unit scale. Parts are parented to each other
                // so PhysX sees an articulation chain, and Unity compounds a parent's scale
                // into its children — for a child rotated relative to a non-uniformly scaled
                // parent that compounding SHEARS, and no componentwise division undoes it.
                // Size therefore lives on the collider and on a separate visual child, never
                // on the transform that positions the body.
                go.transform.SetParent(part.IsRoot ? root.transform : transforms[part.ParentIndex], false);

                // Development produces poses in creature space; each part is parented to its
                // own parent part, so convert into that parent's frame.
                if (part.IsRoot)
                {
                    go.transform.localPosition = part.Position.ToVector3();
                    go.transform.localRotation = part.Rotation.ToQuaternion();
                }
                else
                {
                    PhenotypePart parentPart = phenotype.Parts[part.ParentIndex];
                    Quaternion inverseParent = Quaternion.Inverse(parentPart.Rotation.ToQuaternion());

                    go.transform.localPosition =
                        inverseParent * (part.Position - parentPart.Position).ToVector3();
                    go.transform.localRotation =
                        inverseParent * part.Rotation.ToQuaternion();
                }

                _partBeingBuilt = i;
                AddColliderAndVisual(go, part, shapes.Resolve(part.ShapeId));

                transforms[i] = go.transform;

                var body = go.AddComponent<ArticulationBody>();
                body.mass = Mathf.Max(0.001f, part.Volume * DensityKgPerM3);

                // PhysX's own damping, zeroed deliberately. Unity defaults angularDamping and
                // jointFriction to 0.05, which is a second velocity-proportional drag acting on
                // top of the fluid model in §5.2 — energy leaving the creature through a channel
                // the design never specified and no fitness or energy figure accounts for.
                //
                // Found by an energy audit: joints were doing ~10x more work than drag was
                // removing, at a ratio that stayed near-constant across a 40x sweep of drive
                // strength. A scale-invariant loss is the signature of a linear damping term,
                // not of anything the creature is doing.
                //
                // §5.2's drag is the only resistance a creature should feel. If the solver needs
                // damping for stability, that is a fluid-model parameter and belongs in
                // FluidConfig where it can be measured, not a hidden engine default.
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.jointFriction = 0f;
                bodies[i] = body;

                if (part.IsRoot)
                {
                    body.immovable = false;
                    dofOffset[i] = -1;
                }
                else
                {
                    ConfigureJoint(body, part, phenotype.Parts[part.ParentIndex]);
                    int dof = part.JointType.DofCount();
                    dofOffset[i] = dof > 0 ? dofCursor : -1;
                    dofCursor += dof;
                }
            }

            return new CreatureInstance
            {
                Root = root,
                Bodies = bodies,
                Phenotype = phenotype,
                DofOffset = dofOffset,
                TotalDof = dofCursor,
                Renderers = _renderers.ToArray(),
                RendererPart = _rendererPart.ToArray(),
            };
        }

        /// <summary>
        /// Paints each part by what it is made of, or restores the plain look — DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A view mode, not the creature's appearance, and the distinction is load-bearing.</b>
        /// §5A.5 makes part colour an <i>evolvable genome field</i> — inert until creatures can
        /// see, and then a channel for camouflage, warning colouration, mimicry and display.
        /// Painting parts by cell type in the ordinary view would spend exactly the channel that
        /// trait needs, and would have to be taken away again when it lands. So it is offered as
        /// a mode that can be turned off, and it is never what a creature looks like.
        /// </para>
        /// <para>
        /// Applied through a <see cref="MaterialPropertyBlock"/> so every part still shares one
        /// material and one draw-call batch. Instancing a material per part would break batching
        /// for a debug view, which is the wrong trade at any population worth watching.
        /// </para>
        /// </remarks>
        public static void ApplyCellTypeColours(
            CreatureInstance creature, bool on, CellTypeRegistry cellTypes = null)
        {
            if (creature?.Renderers == null) return;
            cellTypes = cellTypes ?? CellTypeRegistry.Standard;

            var block = new MaterialPropertyBlock();

            for (int i = 0; i < creature.Renderers.Length; i++)
            {
                MeshRenderer renderer = creature.Renderers[i];
                if (renderer == null) continue;

                Color colour = Color.white;
                if (on)
                {
                    Float3 rgb = cellTypes
                        .Resolve(creature.Phenotype.Parts[creature.RendererPart[i]].CellTypeId)
                        .InspectionColour;

                    colour = new Color(rgb.X, rgb.Y, rgb.Z, 1f);
                }

                renderer.GetPropertyBlock(block);

                // URP Lit reads _BaseColor and the built-in Standard shader reads _Color. Setting
                // both costs nothing and means the view works whichever pipeline resolved.
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private static readonly List<int> _rendererPart = new List<int>();
        private static int _partBeingBuilt;

        /// <summary>
        /// Gives the part a collider matching its shape, and a renderable child that draws the
        /// same solid — DESIGN.md §4.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every dimension here comes from the <see cref="PartShape"/> rather than from a formula
        /// repeated locally. Three things have to agree about how large a part is — the collider
        /// PhysX pushes with, the mesh a viewer sees, and the panels
        /// <see cref="FluidModel"/> pushes on — and only the first two are visible. A collider
        /// sized from an independently-derived radius that drifted would give a creature whose
        /// body and whose hydrodynamics were different objects, and nothing would report it.
        /// </para>
        /// <para>
        /// The visual is a CHILD, so its scale never reaches the transform PhysX positions the
        /// link by — see the note in <see cref="Build"/> on shear.
        /// </para>
        /// </remarks>
        private static void AddColliderAndVisual(GameObject go, PhenotypePart part, PartShape shape)
        {
            EnsureAssets();

            Float3 h = part.HalfExtents;
            Transform t = go.transform;

            switch (shape)
            {
                case SphereShape _:
                {
                    float r = SphereShape.Radius(h);
                    go.AddComponent<SphereCollider>().radius = r;
                    AddMesh(t, _sphereMesh, Vector3.zero, Vector3.one * (2f * r));
                    break;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(h);
                    float span = CapsuleShape.HalfSpan(h);

                    CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;                 // Y, matching CapsuleShape
                    capsule.radius = r;
                    capsule.height = 2f * (span + r);      // Unity's height includes the caps

                    // Drawn as a cylinder plus two spheres rather than as Unity's capsule
                    // primitive. That primitive is a fixed 1 wide by 2 tall, so making it the
                    // right length and the right width needs a non-uniform scale, which
                    // stretches the hemispherical caps into ellipsoids — the rendered part
                    // stops matching its own collider, by an amount that grows the further the
                    // capsule is from twice-as-long-as-wide. Three uniformly-scaled primitives
                    // are exact, and cost two extra renderers on a quarter of parts.
                    if (span > 0f) AddMesh(t, _cylinderMesh, Vector3.zero, new Vector3(2f * r, span, 2f * r));

                    AddMesh(t, _sphereMesh, new Vector3(0f, span, 0f), Vector3.one * (2f * r));
                    AddMesh(t, _sphereMesh, new Vector3(0f, -span, 0f), Vector3.one * (2f * r));
                    break;
                }

                default:
                {
                    var full = new Vector3(
                        2f * Mathf.Abs(h.X), 2f * Mathf.Abs(h.Y), 2f * Mathf.Abs(h.Z));

                    go.AddComponent<BoxCollider>().size = full;
                    AddMesh(t, _cubeMesh, Vector3.zero, full);
                    break;
                }
            }
        }

        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _cylinderMesh;
        private static Material _partMaterial;

        private static void AddMesh(Transform parent, Mesh mesh, Vector3 offset, Vector3 scale)
        {
            var visual = new GameObject("Visual") { layer = CreatureLayer };
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = offset;
            visual.transform.localScale = scale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _partMaterial;

            _renderers.Add(renderer);
            _rendererPart.Add(_partBeingBuilt);
        }

        private static void EnsureAssets()
        {
            if (_cubeMesh != null) return;

            _cubeMesh = PrimitiveMesh(PrimitiveType.Cube);
            _sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
            _cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);

            // Resolve the lit shader by name rather than taking the primitive's material.
            // CreatePrimitive hands back the built-in Standard material, which renders as
            // magenta under URP — the classic "everything is pink" symptom. Looking the
            // shader up keeps Evosim.Sim from depending on the URP assemblies at all.
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            _partMaterial = new Material(shader) { name = "Evosim Part" };
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;

            if (Application.isPlaying) Object.Destroy(temp);
            else Object.DestroyImmediate(temp);

            return mesh;
        }

        /// <summary>
        /// Maps a genome joint type onto an <see cref="ArticulationJointType"/> and its DOF locks.
        /// </summary>
        /// <remarks>
        /// PhysX articulations offer fixed, prismatic, revolute and spherical joints, so the
        /// seven types from [K12 §2.1, p.3] map onto three: fixed, 1-DOF revolute, and
        /// spherical with the unwanted swings locked. Spike 01's M4 confirmed articulations
        /// hold under full-amplitude actuation on all DOF without joint separation.
        ///
        /// KNOWN GAP: a revolute joint rotates about the anchor frame's X axis, so Hinge and
        /// Twist differ only in how that frame is oriented, and mirrored parts
        /// (<see cref="PhenotypePart.Mirrored"/>) need their drive axis flipped to move as a
        /// mirror image rather than in parallel. Neither matters until actuation is driven by
        /// the brain graph rather than a test signal; both are resolved at Milestone 3.
        /// </remarks>
        private static void ConfigureJoint(ArticulationBody body, PhenotypePart part, PhenotypePart parentPart)
        {
            // Express one physical joint frame in both bodies' local coordinates. The child's
            // own frame is used, so its anchor rotation is identity and the parent's carries
            // the relative rotation between the two parts.
            Quaternion relative =
                Quaternion.Inverse(parentPart.Rotation.ToQuaternion()) * part.Rotation.ToQuaternion();

            Quaternion frame = JointFrameRotation(part.JointType);

            body.matchAnchors = false;
            body.anchorPosition = part.ChildAnchorLocal.ToVector3();
            body.anchorRotation = frame;
            body.parentAnchorPosition = part.ParentAnchorLocal.ToVector3();
            body.parentAnchorRotation = relative * frame;

            switch (part.JointType)
            {
                case JointType.Fixed:
                    body.jointType = ArticulationJointType.FixedJoint;
                    break;

                case JointType.Hinge:
                case JointType.Twist:
                    body.jointType = ArticulationJointType.RevoluteJoint;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    break;

                case JointType.HingeTwist:
                case JointType.TwistHinge:
                case JointType.Universal:
                    body.jointType = ArticulationJointType.SphericalJoint;
                    body.swingYLock = ArticulationDofLock.LimitedMotion;
                    body.swingZLock = ArticulationDofLock.LockedMotion;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    body.yDrive = MakeDrive(Limit(part, 1));
                    break;

                case JointType.Spherical:
                    body.jointType = ArticulationJointType.SphericalJoint;
                    body.swingYLock = ArticulationDofLock.LimitedMotion;
                    body.swingZLock = ArticulationDofLock.LimitedMotion;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    body.yDrive = MakeDrive(Limit(part, 1));
                    body.zDrive = MakeDrive(Limit(part, 2));
                    break;
            }
        }

        private static Quaternion JointFrameRotation(JointType type)
        {
            // Revolute rotates about the frame's X axis. Attachment is face-to-face along
            // some axis, so a "twist" should spin about that axis (identity frame) while a
            // "hinge" should bend across it.
            return type == JointType.Hinge ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        }

        private static Float2 Limit(PhenotypePart part, int dof) =>
            dof < part.JointLimits.Length ? part.JointLimits[dof] : new Float2(-1f, 1f);

        private static ArticulationDrive MakeDrive(Float2 limit) => new ArticulationDrive
        {
            lowerLimit = limit.X * Mathf.Rad2Deg,
            upperLimit = limit.Y * Mathf.Rad2Deg,

            // Zero stiffness: DESIGN.md §4.4 applies effector output as TORQUE, not as a
            // position target. Damping is small and non-zero so undriven joints settle
            // instead of ringing.
            stiffness = 0f,
            damping = 1f,
            forceLimit = float.MaxValue,
            target = 0f,
        };
    }
}
