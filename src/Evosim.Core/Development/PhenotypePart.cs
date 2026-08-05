using System;

namespace Evosim.Core
{
    /// <summary>
    /// One developed body part: a box with a joint to its parent. DESIGN.md §4.2.
    /// </summary>
    /// <remarks>
    /// Parts are emitted in depth-first pre-order, so <see cref="ParentIndex"/> is always
    /// less than <see cref="Index"/>. Evosim.Sim relies on that when building an
    /// <c>ArticulationBody</c> chain, which must be constructed parent-first.
    /// </remarks>
    public sealed class PhenotypePart
    {
        /// <summary>Position in this part list.</summary>
        public int Index { get; internal set; }

        /// <summary>Parent's index, or -1 for the root.</summary>
        public int ParentIndex { get; internal set; }

        /// <summary>Index of the <see cref="MorphNode"/> this part grew from. Many parts may share one node.</summary>
        public int SourceNode { get; internal set; }

        /// <summary>Tree depth, root at 0.</summary>
        public int Depth { get; internal set; }

        /// <summary>Box half-extents in metres, with cumulative scale applied. Always positive.</summary>
        public Float3 HalfExtents { get; internal set; }

        /// <summary>Position in creature-local space.</summary>
        public Float3 Position { get; internal set; }

        /// <summary>Orientation in creature-local space. Always a proper rotation.</summary>
        public Quat Rotation { get; internal set; }

        /// <summary>
        /// True when an odd number of reflections was applied on the path to this part.
        /// </summary>
        /// <remarks>
        /// A box is symmetric under the axis flip used to recover a proper rotation, so
        /// geometry is unaffected — but joint axis conventions are not, and a mirrored limb
        /// needs its hinge axis flipped to move as a mirror image rather than in parallel.
        /// Evosim.Sim consumes this.
        /// </remarks>
        public bool Mirrored { get; internal set; }

        /// <summary>What this part is made of — <see cref="CellType.Id"/>, DESIGN.md §5A.1.</summary>
        public string CellTypeId { get; internal set; } = CellTypeIds.Structural;

        /// <summary>Joint to the parent. Meaningless at the root, where it is <see cref="JointType.Fixed"/>.</summary>
        public JointType JointType { get; internal set; } = JointType.Fixed;

        /// <summary>Peak joint torque in newton-metres — links only. See <see cref="MorphNode.Power"/>.</summary>
        public float Power { get; internal set; }

        /// <summary>Min/max per DOF, in radians.</summary>
        public Float2[] JointLimits { get; internal set; } = Array.Empty<Float2>();

        /// <summary>Joint anchor in the parent's local space, in metres. Zero at the root.</summary>
        public Float3 ParentAnchorLocal { get; internal set; }

        /// <summary>Joint anchor in this part's local space, in metres. Zero at the root.</summary>
        public Float3 ChildAnchorLocal { get; internal set; }

        /// <summary>
        /// This part's local brain — the neuron definitions of <see cref="SourceNode"/>.
        /// Shared, not copied: every part grown from one node runs the same controller
        /// definition over its own state, which is what makes a recursive chain a CPG
        /// (DESIGN.md §4.3).
        /// </summary>
        public NeuronDef[] Neurons { get; internal set; } = Array.Empty<NeuronDef>();

        public float Volume => HalfExtents.BoxVolume;

        public bool IsRoot => ParentIndex < 0;

        public override string ToString() =>
            $"#{Index} node={SourceNode} d={Depth} half={HalfExtents} {JointType}{(Mirrored ? " mirrored" : "")}";
    }
}
