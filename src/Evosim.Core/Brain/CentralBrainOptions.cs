namespace Evosim.Core
{
    /// <summary>
    /// The settings of D146's one brain: which brain a run's creatures carry, how a founder's is
    /// wired, and how mutation grows it. Group <c>brain</c> of <c>config.json</c>.
    /// </summary>
    /// <remarks>
    /// Every rate here is unmeasured (§5A.10) and a first guess, recorded in
    /// <c>logbook/specs/brain-v2-build.md</c> with its reason. The founder's weights are scaled by
    /// the square root of how many links an output starts with, so an output's sum starts at about
    /// the size of one input whatever the body's size: a large body does not start saturated.
    /// </remarks>
    public sealed class CentralBrainOptions
    {
        /// <summary>
        /// Which brain the run's creatures carry. <see cref="Core.BrainModel.PerNode"/>, the
        /// default, is every run before D146. <c>EVOSIM_BRAIN_MODEL</c>.
        /// </summary>
        [Tunable("brain")]
        public BrainModel BrainModel { get; set; } = BrainModel.PerNode;

        /// <summary>
        /// The share of a body's port and output pairs a founder carries a link for. 1, the
        /// default, is the proposal's "every input port is connected to every output unit from
        /// birth".
        /// </summary>
        [Tunable("brain")]
        public float FounderLinkShare { get; set; } = 1f;

        /// <summary>
        /// A founder link's standard deviation times the square root of an output's starting
        /// links: LeCun's normal initialisation for a tanh unit at 1 (D147), so an output's summed
        /// input starts at about the size of one input.
        /// </summary>
        [Tunable("brain")]
        public float FounderWeightScale { get; set; } = 1f;

        /// <summary>
        /// The standard deviation of a founder output's bias. 0, the default, is the standard practice
        /// and the owner's ruling of 2026-10-02 (D147): a founder moves only when its senses push an
        /// output past the joint's threshold, so its behaviour comes from its senses from the start.
        /// </summary>
        [Tunable("brain")]
        public float FounderBiasStdDev { get; set; }

        /// <summary>The chance, per link per birth, that its weight takes a step.</summary>
        [Tunable("brain")]
        public float WeightChance { get; set; } = 0.1f;

        /// <summary>The standard deviation of a weight's step.</summary>
        [Tunable("brain")]
        public float WeightStdDev { get; set; } = 0.1f;

        /// <summary>The chance, per unit per birth, that its bias takes a step.</summary>
        [Tunable("brain")]
        public float BiasChance { get; set; } = 0.1f;

        /// <summary>The standard deviation of a bias's step.</summary>
        [Tunable("brain")]
        public float BiasStdDev { get; set; } = 0.1f;

        /// <summary>
        /// The chance per birth of a new link: from any port or unit to any hidden unit or output,
        /// a loop included (the owner, 2026-10-01: "yes").
        /// </summary>
        [Tunable("brain")]
        public float AddLinkChance { get; set; } = 0.3f;

        /// <summary>The standard deviation of a new link's weight.</summary>
        [Tunable("brain")]
        public float NewLinkStdDev { get; set; } = 0.3f;

        /// <summary>
        /// The chance per birth that a link is split by a new hidden unit: weight 1 on the way in,
        /// the old weight on the way out.
        /// </summary>
        [Tunable("brain")]
        public float AddUnitChance { get; set; } = 0.1f;

        /// <summary>The chance per birth that one link is removed.</summary>
        [Tunable("brain")]
        public float RemoveLinkChance { get; set; } = 0.05f;

        /// <summary>The chance per birth that one hidden unit is removed with its links.</summary>
        [Tunable("brain")]
        public float RemoveUnitChance { get; set; } = 0.02f;

        /// <summary>The most hidden units a brain may hold. A guard, not a price: thinking is free.</summary>
        [Tunable("brain")]
        public int MaxHiddenUnits { get; set; } = 128;

        public CentralBrainOptions Clone() => (CentralBrainOptions)MemberwiseClone();
    }
}
