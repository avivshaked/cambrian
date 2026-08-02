using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// A node in the morphology graph — DESIGN.md §4.1. One node can become many parts.
    /// </summary>
    public sealed class MorphNode
    {
        /// <summary>Box half-extents, before any accumulated scale from the path that reached this node.</summary>
        public Float3 Dimensions { get; set; } = new Float3(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// Joint connecting a part grown from this node to its parent. Ignored at the root,
        /// which has no parent. Mutable, with limits resampled to the new DOF count (§4.1).
        /// </summary>
        public JointType JointType { get; set; } = JointType.Hinge;

        /// <summary>Min/max per DOF, in radians. Length should match <see cref="JointType"/>'s DOF count.</summary>
        public Float2[] JointLimits { get; set; } = Array.Empty<Float2>();

        /// <summary>
        /// How many times this node may occur along one path before its recursion is
        /// considered spent. At that point only <see cref="MorphEdge.TerminalOnly"/> edges
        /// are followed (DESIGN.md §4.2).
        /// </summary>
        public int RecursiveLimit { get; set; } = 1;

        /// <summary>The node's local brain. Duplicated with the node — DESIGN.md §4.3.</summary>
        public NeuronDef[] Neurons { get; set; } = Array.Empty<NeuronDef>();

        /// <summary>Outgoing edges.</summary>
        public List<MorphEdge> Edges { get; } = new List<MorphEdge>();

        public MorphNode Clone()
        {
            var clone = new MorphNode
            {
                Dimensions = Dimensions,
                JointType = JointType,
                JointLimits = (Float2[])JointLimits.Clone(),
                RecursiveLimit = RecursiveLimit,
                Neurons = new NeuronDef[Neurons.Length],
            };

            for (int i = 0; i < Neurons.Length; i++) clone.Neurons[i] = Neurons[i].Clone();
            for (int i = 0; i < Edges.Count; i++) clone.Edges.Add(Edges[i].Clone());

            return clone;
        }

        /// <summary>
        /// Resizes <see cref="JointLimits"/> to match the current joint type, keeping
        /// existing entries where possible and filling new ones with
        /// <paramref name="fill"/>. Called after a joint-type mutation (DESIGN.md §4.5).
        /// </summary>
        public void ResampleJointLimits(Float2 fill)
        {
            int dof = JointType.DofCount();
            if (JointLimits.Length == dof) return;

            var resized = new Float2[dof];
            for (int i = 0; i < dof; i++)
            {
                resized[i] = i < JointLimits.Length ? JointLimits[i] : fill;
            }
            JointLimits = resized;
        }

        public override string ToString() =>
            $"{JointType} {Dimensions} rec={RecursiveLimit} edges={Edges.Count} neurons={Neurons.Length}";
    }
}
