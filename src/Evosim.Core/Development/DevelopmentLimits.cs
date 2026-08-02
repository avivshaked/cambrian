namespace Evosim.Core
{
    /// <summary>
    /// The guard rails from DESIGN.md §4.2. Part of the config hash (§7).
    /// </summary>
    public sealed class DevelopmentLimits
    {
        /// <summary>Hard cap on parts in the developed phenotype. DESIGN.md §4.2 proposes 16.</summary>
        public int MaxParts { get; set; } = 16;

        /// <summary>Hard cap on tree depth, root at depth 0. DESIGN.md §4.2 proposes 8.</summary>
        public int MaxDepth { get; set; } = 8;

        /// <summary>
        /// Minimum box volume for a part, in m³. [K12 §2.3, p.7]: <i>"the volume of each body
        /// part must be larger than the specified threshold as extremely small body parts
        /// cause instability in the physical engine."</i>
        /// </summary>
        /// <remarks>
        /// Cumulative scale is multiplicative down a subtree, so a scale of 0.5 on a
        /// recursive edge shrinks parts geometrically. Without this floor, a long chain
        /// reaches sub-millimetre boxes and the solver misbehaves in ways that look like
        /// evolved behaviour.
        /// </remarks>
        public float MinPartVolume { get; set; } = 1e-4f;

        public static DevelopmentLimits Default => new DevelopmentLimits();

        public DevelopmentLimits Clone() => new DevelopmentLimits
        {
            MaxParts = MaxParts,
            MaxDepth = MaxDepth,
            MinPartVolume = MinPartVolume,
        };

        public override string ToString() =>
            $"maxParts={MaxParts} maxDepth={MaxDepth} minVolume={MinPartVolume:0.######}";
    }
}
