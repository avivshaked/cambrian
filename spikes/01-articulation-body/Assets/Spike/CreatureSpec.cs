using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spike
{
    /// <summary>
    /// Joint types from DESIGN.md §4.1, taken from [K12 §2.1, p.3].
    /// This is NOT the genome — it is a flat parameter struct for the spike only.
    /// </summary>
    public enum SpikeJointType
    {
        Fixed = 0,      // 0 DOF
        Hinge = 1,      // 1 DOF
        Twist = 2,      // 1 DOF
        HingeTwist = 3, // 2 DOF
        TwistHinge = 4, // 2 DOF
        Universal = 5,  // 2 DOF
        Spherical = 6,  // 3 DOF
    }

    public static class JointTypeInfo
    {
        public static int DofCount(SpikeJointType t) => t switch
        {
            SpikeJointType.Fixed => 0,
            SpikeJointType.Hinge => 1,
            SpikeJointType.Twist => 1,
            SpikeJointType.HingeTwist => 2,
            SpikeJointType.TwistHinge => 2,
            SpikeJointType.Universal => 2,
            SpikeJointType.Spherical => 3,
            _ => 0,
        };
    }

    /// <summary>One body part: a box, plus how it attaches to its parent.</summary>
    [Serializable]
    public struct PartSpec
    {
        public int parentIndex;          // -1 for root
        public Vector3 halfExtents;
        public SpikeJointType jointType;
        public Vector3 parentAnchorLocal; // attachment point on parent, in parent local space
        public Quaternion attachRotation;
        public float jointLimitDeg;       // symmetric limit, ± this value
    }

    /// <summary>
    /// A whole creature as a flat array of parts. Parts are ordered so that a
    /// parent always precedes its children (topological order) — the builder
    /// relies on this.
    /// </summary>
    [Serializable]
    public class CreatureSpec
    {
        public List<PartSpec> parts = new();

        public int PartCount => parts.Count;

        public int TotalDof()
        {
            int dof = 0;
            for (int i = 0; i < parts.Count; i++) dof += JointTypeInfo.DofCount(parts[i].jointType);
            return dof;
        }

        /// <summary>
        /// Deterministic pseudo-random creature. Same seed → same spec, always.
        /// Shape is a branching tree, capped at the DESIGN.md §4.2 limits
        /// (16 parts, depth 8).
        /// </summary>
        public static CreatureSpec Random(int seed, int partCount, int maxDepth = 8)
        {
            partCount = Mathf.Clamp(partCount, 1, 16);
            var rng = new System.Random(seed);
            float Next(float a, float b) => (float)(a + rng.NextDouble() * (b - a));

            var spec = new CreatureSpec();
            var depth = new List<int>();

            // Root
            spec.parts.Add(new PartSpec
            {
                parentIndex = -1,
                halfExtents = new Vector3(Next(0.15f, 0.35f), Next(0.15f, 0.35f), Next(0.2f, 0.5f)),
                jointType = SpikeJointType.Fixed,
                parentAnchorLocal = Vector3.zero,
                attachRotation = Quaternion.identity,
                jointLimitDeg = 0f,
            });
            depth.Add(0);

            var jointChoices = new[]
            {
                SpikeJointType.Hinge, SpikeJointType.Twist, SpikeJointType.HingeTwist,
                SpikeJointType.TwistHinge, SpikeJointType.Universal, SpikeJointType.Spherical,
            };

            for (int i = 1; i < partCount; i++)
            {
                // Pick a parent that is not already at max depth
                int parent;
                int guard = 0;
                do { parent = rng.Next(0, spec.parts.Count); guard++; }
                while (depth[parent] >= maxDepth - 1 && guard < 64);

                var pHalf = spec.parts[parent].halfExtents;
                // Attach on a face of the parent box
                int face = rng.Next(0, 6);
                Vector3 anchor = face switch
                {
                    0 => new Vector3(pHalf.x, 0, 0),
                    1 => new Vector3(-pHalf.x, 0, 0),
                    2 => new Vector3(0, pHalf.y, 0),
                    3 => new Vector3(0, -pHalf.y, 0),
                    4 => new Vector3(0, 0, pHalf.z),
                    _ => new Vector3(0, 0, -pHalf.z),
                };

                spec.parts.Add(new PartSpec
                {
                    parentIndex = parent,
                    halfExtents = new Vector3(Next(0.08f, 0.28f), Next(0.08f, 0.28f), Next(0.12f, 0.45f)),
                    jointType = jointChoices[rng.Next(0, jointChoices.Length)],
                    parentAnchorLocal = anchor,
                    attachRotation = Quaternion.Euler(Next(-60f, 60f), Next(-60f, 60f), Next(-60f, 60f)),
                    jointLimitDeg = Next(25f, 75f),
                });
                depth.Add(depth[parent] + 1);
            }

            return spec;
        }

        /// <summary>A fixed-topology chain — used by M6 to probe depth limits.</summary>
        public static CreatureSpec Chain(int length, SpikeJointType joint = SpikeJointType.Hinge)
        {
            var spec = new CreatureSpec();
            for (int i = 0; i < length; i++)
            {
                spec.parts.Add(new PartSpec
                {
                    parentIndex = i - 1,
                    halfExtents = new Vector3(0.15f, 0.15f, 0.3f),
                    jointType = i == 0 ? SpikeJointType.Fixed : joint,
                    parentAnchorLocal = new Vector3(0, 0, 0.3f),
                    attachRotation = Quaternion.identity,
                    jointLimitDeg = 45f,
                });
            }
            return spec;
        }
    }
}
