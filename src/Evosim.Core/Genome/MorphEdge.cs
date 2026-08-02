namespace Evosim.Core
{
    /// <summary>
    /// A directed edge in the morphology graph — DESIGN.md §4.1.
    /// </summary>
    /// <remarks>
    /// Every geometric field here is applied <b>cumulatively to the whole child subtree</b>
    /// during development, per [K12 §2.1, p.3]: <i>"they are applied to the entire subtree
    /// of the phenotype graph during its construction."</i> That is what makes the encoding
    /// generative — a single scale change on one edge tapers an entire limb.
    /// </remarks>
    public sealed class MorphEdge
    {
        /// <summary>Index of the target node in <see cref="Genome.Nodes"/>. May point back at the source — cycles are the point.</summary>
        public int Child { get; set; }

        /// <summary>
        /// Attachment point on the parent, in normalised box coordinates: each component in
        /// [-1, 1], where ±1 is a face of the parent's box.
        /// </summary>
        public Float3 ParentAnchor { get; set; } = Float3.Zero;

        /// <summary>Attachment point on the child, in the child's normalised box coordinates.</summary>
        public Float3 ChildAnchor { get; set; } = Float3.Zero;

        /// <summary>Rotation applied to the child subtree at the attachment point.</summary>
        public Quat Orientation { get; set; } = Quat.Identity;

        /// <summary>Per-axis scale, applied cumulatively down the child subtree.</summary>
        public Float3 Scale { get; set; } = Float3.One;

        /// <summary>
        /// Per-axis reflection flags. One, two or three enabled flags produce two, four or
        /// eight mirrored copies of the child ([K12 §2.1, p.3]) — the only source of
        /// bilateral symmetry in this encoding.
        /// </summary>
        public Bool3 Reflect { get; set; } = Bool3.None;

        /// <summary>
        /// Follow this edge only once the source node's recursion budget is spent.
        /// [K12 §2.1, p.3]: it <i>"can be used to represent structures appearing at the end
        /// of chains or repeating units"</i> — hands, fins, tail flukes.
        /// </summary>
        public bool TerminalOnly { get; set; }

        public MorphEdge Clone() => new MorphEdge
        {
            Child = Child,
            ParentAnchor = ParentAnchor,
            ChildAnchor = ChildAnchor,
            Orientation = Orientation,
            Scale = Scale,
            Reflect = Reflect,
            TerminalOnly = TerminalOnly,
        };

        public override string ToString() =>
            $"-> {Child}{(TerminalOnly ? " (terminal)" : "")}{(Reflect.EnabledCount > 0 ? $" x{Reflect.CopyCount}" : "")}";
    }
}
