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

        /// <summary>Total actuated degrees of freedom across the creature.</summary>
        public int TotalDof { get; internal set; }

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

        public static CreatureInstance Build(Phenotype phenotype, Vector3 origin, Transform parent = null)
        {
            if (phenotype == null || phenotype.PartCount == 0)
            {
                throw new System.ArgumentException("Cannot build an empty phenotype.", nameof(phenotype));
            }

            var root = new GameObject("Creature");
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = origin;

            var bodies = new ArticulationBody[phenotype.PartCount];
            var transforms = new Transform[phenotype.PartCount];
            var dofOffset = new int[phenotype.PartCount];
            int dofCursor = 0;

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

                Vector3 fullExtents = (part.HalfExtents * 2f).ToVector3();
                go.AddComponent<BoxCollider>().size = fullExtents;
                AddVisual(go.transform, fullExtents);

                transforms[i] = go.transform;

                var body = go.AddComponent<ArticulationBody>();
                body.mass = Mathf.Max(0.001f, part.Volume * DensityKgPerM3);
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
            };
        }

        private static Mesh _cubeMesh;
        private static Material _partMaterial;

        /// <summary>
        /// Adds the renderable box as a CHILD of the body transform, so its scale never
        /// reaches the transform PhysX positions the link by.
        /// </summary>
        private static void AddVisual(Transform parent, Vector3 fullExtents)
        {
            if (_cubeMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;

                if (Application.isPlaying) Object.Destroy(temp);
                else Object.DestroyImmediate(temp);

                // Resolve the lit shader by name rather than taking the primitive's material.
                // CreatePrimitive hands back the built-in Standard material, which renders as
                // magenta under URP — the classic "everything is pink" symptom. Looking the
                // shader up keeps Evosim.Sim from depending on the URP assemblies at all.
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Standard");

                _partMaterial = new Material(shader) { name = "Evosim Part" };
            }

            var visual = new GameObject("Visual") { layer = CreatureLayer };
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = fullExtents;
            visual.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = _partMaterial;
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
