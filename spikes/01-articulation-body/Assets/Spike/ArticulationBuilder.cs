using System.Collections.Generic;
using UnityEngine;

namespace Spike
{
    /// <summary>
    /// A built creature: root GameObject plus flat arrays for fast per-step access.
    /// </summary>
    public class BuiltCreature
    {
        public GameObject root;
        public ArticulationBody[] bodies;
        public SpikeJointType[] jointTypes;
        public int[] dofOffset;   // index into the flattened DOF space per body
        public int totalDof;
        public float totalMass;
        /// <summary>Mass of the lighter of the two bodies at each joint — [K12 §2.2, p.5].</summary>
        public float[] smallerMassAtJoint;
    }

    public static class ArticulationBuilder
    {
        /// <summary>Layer used for creature colliders so tiles ignore each other.</summary>
        public const int CreatureLayer = 8;

        public static BuiltCreature Build(CreatureSpec spec, Vector3 origin, Transform parent = null)
        {
            int n = spec.PartCount;
            var go = new GameObject[n];
            var bodies = new ArticulationBody[n];
            var jointTypes = new SpikeJointType[n];
            var dofOffset = new int[n];
            var smallerMass = new float[n];

            int dofCursor = 0;

            for (int i = 0; i < n; i++)
            {
                var p = spec.parts[i];
                go[i] = new GameObject($"part{i}");
                go[i].layer = CreatureLayer;

                if (p.parentIndex < 0)
                {
                    if (parent != null) go[i].transform.SetParent(parent, false);
                    go[i].transform.position = origin;
                    go[i].transform.rotation = Quaternion.identity;
                }
                else
                {
                    var pt = go[p.parentIndex].transform;
                    go[i].transform.SetParent(pt, false);
                    // Joint sits on the parent's face; the child box extends outward along local +Z.
                    go[i].transform.localPosition = p.parentAnchorLocal
                                                  + p.attachRotation * new Vector3(0, 0, p.halfExtents.z);
                    go[i].transform.localRotation = p.attachRotation;
                }

                var col = go[i].AddComponent<BoxCollider>();
                col.size = p.halfExtents * 2f;

                var ab = go[i].AddComponent<ArticulationBody>();
                bodies[i] = ab;
                jointTypes[i] = p.jointType;

                // Uniform density so mass tracks volume — keeps the §4.4 mass-scaling meaningful.
                float volume = p.halfExtents.x * p.halfExtents.y * p.halfExtents.z * 8f;
                ab.mass = Mathf.Max(0.05f, volume * 1000f * 0.001f);

                if (p.parentIndex < 0)
                {
                    ab.immovable = false;   // floating base — creature is free in space
                    dofOffset[i] = 0;
                    continue;
                }

                ConfigureJoint(ab, p);

                dofOffset[i] = dofCursor;
                dofCursor += JointTypeInfo.DofCount(p.jointType);

                smallerMass[i] = Mathf.Min(ab.mass, bodies[p.parentIndex].mass);
            }

            float total = 0f;
            for (int i = 0; i < n; i++) total += bodies[i].mass;

            return new BuiltCreature
            {
                root = go[0],
                bodies = bodies,
                jointTypes = jointTypes,
                dofOffset = dofOffset,
                totalDof = dofCursor,
                totalMass = total,
                smallerMassAtJoint = smallerMass,
            };
        }

        static void ConfigureJoint(ArticulationBody ab, PartSpec p)
        {
            ab.matchAnchors = false;
            ab.parentAnchorPosition = p.parentAnchorLocal;
            ab.parentAnchorRotation = p.attachRotation;
            ab.anchorPosition = new Vector3(0, 0, -p.halfExtents.z);
            ab.anchorRotation = Quaternion.identity;

            float lim = p.jointLimitDeg;

            switch (p.jointType)
            {
                case SpikeJointType.Fixed:
                    ab.jointType = ArticulationJointType.FixedJoint;
                    break;

                case SpikeJointType.Hinge:
                case SpikeJointType.Twist:
                    ab.jointType = ArticulationJointType.RevoluteJoint;
                    ab.twistLock = ArticulationDofLock.LimitedMotion;
                    ab.xDrive = MakeDrive(-lim, lim);
                    break;

                case SpikeJointType.HingeTwist:
                case SpikeJointType.TwistHinge:
                case SpikeJointType.Universal:
                    // 2 DOF: spherical with one axis locked out.
                    ab.jointType = ArticulationJointType.SphericalJoint;
                    ab.twistLock = ArticulationDofLock.LimitedMotion;
                    ab.swingYLock = ArticulationDofLock.LimitedMotion;
                    ab.swingZLock = ArticulationDofLock.LockedMotion;
                    ab.xDrive = MakeDrive(-lim, lim);
                    ab.yDrive = MakeDrive(-lim, lim);
                    break;

                case SpikeJointType.Spherical:
                    ab.jointType = ArticulationJointType.SphericalJoint;
                    ab.twistLock = ArticulationDofLock.LimitedMotion;
                    ab.swingYLock = ArticulationDofLock.LimitedMotion;
                    ab.swingZLock = ArticulationDofLock.LimitedMotion;
                    ab.xDrive = MakeDrive(-lim, lim);
                    ab.yDrive = MakeDrive(-lim, lim);
                    ab.zDrive = MakeDrive(-lim, lim);
                    break;
            }
        }

        /// <summary>
        /// Drive configured for position targeting with a force ceiling.
        /// forceLimit is overwritten per-joint by EffectorDriver using the
        /// smaller-connected-mass rule from [K12 §2.2, p.5].
        /// </summary>
        static ArticulationDrive MakeDrive(float lower, float upper) => new ArticulationDrive
        {
            lowerLimit = lower,
            upperLimit = upper,
            stiffness = 800f,
            damping = 40f,
            forceLimit = 1000f,
            target = 0f,
            targetVelocity = 0f,
        };

        public static void Destroy(BuiltCreature c)
        {
            if (c?.root == null) return;
            Object.DestroyImmediate(c.root);
            c.root = null;
        }
    }
}
