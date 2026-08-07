namespace Evosim.Core
{
    /// <summary>
    /// How often each variation operator fires — DESIGN.md §4.5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of these is unmeasured (§5A.10), which is why they live in
    /// <see cref="RunConfig"/> and go into its hash rather than sitting as constants in
    /// <see cref="Mutator"/>. Mutation rate is the single knob most likely to decide whether a
    /// run produces anything: too low and nothing new appears, too high and no lineage survives
    /// long enough to accumulate an adaptation.
    /// </para>
    /// <para>
    /// <b>Grafting is absent.</b> §4.5 lists it as the design's only recombination, and
    /// reproduction is asexual (§5A.6), so there is no second parent for it to draw from. It is
    /// not implemented rather than implemented and disabled, because a disabled operator reads
    /// as a decision that was made; this one is an open question waiting on review round 3.
    /// </para>
    /// </remarks>
    public sealed class MutationRates
    {
        /// <summary>
        /// Chance each individual scalar — a dimension, an anchor, a weight — is perturbed.
        /// </summary>
        /// <remarks>
        /// Per scalar, not per genome, so a large creature accumulates more change per birth
        /// than a small one. That is deliberate: it keeps the amount of variation proportional
        /// to how much there is to vary, rather than diluting it across a growing body.
        /// </remarks>
        [Tunable("mutation")]
        public float ScalarChance { get; set; } = 0.08f;

        /// <summary>Standard deviation of a scalar perturbation, as a fraction of the value.</summary>
        /// <remarks>
        /// Proportional rather than absolute, so the same rate is sensible for a 0.15 m
        /// half-extent and a 120 N·m link capacity. An absolute step would be imperceptible on
        /// one and catastrophic on the other.
        /// </remarks>
        [Tunable("mutation")]
        public float ScalarStdDev { get; set; } = 0.15f;

        /// <summary>Chance per birth that a node is duplicated into the genome.</summary>
        /// <remarks>
        /// The copy arrives at <see cref="NewNodeHalfExtent"/> rather than at its source's size,
        /// so a duplication is nearly neutral on the birth it happens: it adds a part too small
        /// to change much, which may then grow if it turns out to be worth anything. Arriving
        /// full-size made every duplication a large jump, and a large jump in a co-adapted body
        /// is almost always worse than what it replaced (§2).
        /// </remarks>
        [Tunable("mutation")]
        public float AddNodeChance { get; set; } = 0.04f;

        /// <summary>
        /// Half-extent a duplicated node starts at, metres. Just above extinction.
        /// </summary>
        [Tunable("mutation")]
        public float NewNodeHalfExtent { get; set; } = 0.03f;

        /// <summary>
        /// A node whose mean half-extent falls below this is removed from the genome, metres.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This replaces a removal rate, and the difference is that removal is no longer
        /// blind.</b> A per-node removal chance deletes useful and useless nodes alike, so
        /// selection has to keep re-winning structure it already won; worse, its strength is a
        /// number that decides how large a genome may be, which is not mutation's decision to
        /// make. Here a node disappears only by shrinking to nothing, and shrinking is something
        /// selection can prevent: a node doing useful work is held large, and one doing nothing
        /// feels no pressure, drifts, and falls out. Extinction reaches exactly the nodes nothing
        /// is holding up.
        /// </para>
        /// <para>
        /// That is also what dissolves genome bloat rather than suppressing it. Nodes past what
        /// <see cref="DevelopmentLimits.MaxParts"/> expresses are unexpressed <i>because</i>
        /// nothing selects on them — so they are precisely the ones that drift to extinction. No
        /// price and no cap required.
        /// </para>
        /// <para>
        /// The value is not a new invented constant: development already refuses to grow a part
        /// below <see cref="DevelopmentLimits.MinPartVolume"/>, which for a cube is a half-extent
        /// near 0.023 m. A node shrunk past the point where it would produce a viable part is the
        /// natural definition of extinct.
        /// </para>
        /// <para>
        /// Note what it does <i>not</i> fix: size is perturbed proportionally, so log-size does a
        /// random walk and the threshold is an absorbing barrier — given long enough and no
        /// selection, every node is eventually absorbed. That is the intended behaviour, not a
        /// flaw, and it is why <see cref="AddNodeChance"/> still exists. What sets equilibrium is
        /// now the balance between duplication and drift-to-extinction, which selection can move;
        /// under a removal rate it could not.
        /// </para>
        /// </remarks>
        [Tunable("mutation")]
        public float NodeExtinctionHalfExtent { get; set; } = 0.02f;

        /// <summary>
        /// Hard ceiling on genome nodes. A backstop, not the mechanism.
        /// </summary>
        /// <remarks>
        /// Extinction by shrinking is what actually controls size; this only bounds the worst
        /// case, so that a mis-set threshold degrades into a bounded genome rather than an
        /// out-of-memory. It should never fire in a healthy run — measured genome size settles
        /// near 39 over 100,000 births. A limit doing real work every generation is a limit
        /// hiding a missing pressure.
        /// </remarks>
        [Tunable("mutation")]
        public int MaxNodes { get; set; } = 64;
        [Tunable("mutation")]
        public float AddEdgeChance { get; set; } = 0.06f;
        [Tunable("mutation")]
        public float RemoveEdgeChance { get; set; } = 0.05f;
        [Tunable("mutation")]
        public float AddNeuronChance { get; set; } = 0.05f;
        [Tunable("mutation")]
        public float RemoveNeuronChance { get; set; } = 0.04f;

        /// <summary>Chance a neuron input is repointed at something else.</summary>
        [Tunable("mutation")]
        public float RewireInputChance { get; set; } = 0.05f;

        /// <summary>Chance a neuron's operator changes.</summary>
        [Tunable("mutation")]
        public float NeuronOpChance { get; set; } = 0.03f;

        /// <summary>Chance a node's joint type changes — and with it its degrees of freedom.</summary>
        /// <remarks>
        /// Only reachable on a link (§5A.1), and the operator repairs the joint limit array and
        /// capacity to match, because a joint type changed without them is an invalid genome.
        /// </remarks>
        [Tunable("mutation")]
        public float JointTypeChance { get; set; } = 0.04f;

        /// <summary>Chance an edge's reflect flag or terminal-only flag is toggled.</summary>
        [Tunable("mutation")]
        public float FlagChance { get; set; } = 0.03f;

        /// <summary>Chance a node's recursion depth changes by one.</summary>
        [Tunable("mutation")]
        public float RecursiveLimitChance { get; set; } = 0.03f;

        /// <summary>Chance a part changes its geometry — §4.1.</summary>
        /// <remarks>
        /// Rarer than a scalar tweak and commoner than a cell-type change. Shape is a large,
        /// discontinuous jump — a flat paddle becoming a ball loses every bit of thrust it was
        /// producing — so it should not be routine; but unlike cell type it does not change what
        /// a lineage <i>is</i>, only how it is built, so it need not be as scarce.
        /// </remarks>
        [Tunable("mutation")]
        public float ShapeChance { get; set; } = 0.02f;

        /// <summary>Chance a part changes what it is made of — §5A.3.</summary>
        /// <remarks>
        /// <b>Deliberately the rarest operator here, and it has to be.</b> It is one of the two
        /// bridges across the predator valley — the route by which a herbivore's descendant
        /// becomes a carnivore once there is finally something worth eating — so it must be
        /// possible. But a cell type that flips often is not a trait, it is noise, and no lineage
        /// can specialise around a body plan whose parts keep changing what they do.
        ///
        /// <b>Read this as a per-node rate and it compounds.</b> A body has around eight parts,
        /// so a per-node chance of 0.006 changes something in roughly one birth in twenty —
        /// measured at 4.8%, which is not scarce at all. At 0.001 it is about one birth in a
        /// hundred and twenty, which is rare enough that a lineage can hold a strategy while
        /// still finding the bridge eventually.
        ///
        /// ⚠ "Very scarce" is the requirement; this value is a guess (§5A.10).
        ///
        /// <b>This is the only one.</b> <c>RunConfig</c> carried a second knob for the same thing,
        /// <c>CellTypeMutationChance</c>, at ten times this value and with nothing reading it —
        /// setting it changed the hash, so two runs looked different in the record and were
        /// identical in fact. Which is §7 failing in the direction it least wants to (logbook/0013).
        /// </remarks>
        [Tunable("mutation")]
        public float CellTypeChance { get; set; } = 0.001f;

        /// <summary>Chance brood size changes by one — §5A.6.</summary>
        [Tunable("mutation")]
        public float BroodSizeChance { get; set; } = 0.05f;

        /// <summary>Chance offspring endowment is perturbed — §5A.6.</summary>
        [Tunable("mutation")]
        public float EndowmentChance { get; set; } = 0.08f;

        /// <summary>Largest brood a mutation may produce.</summary>
        /// <remarks>
        /// A ceiling rather than a cost, because the cost already exists: a brood of a thousand
        /// is priced out by the per-offspring overhead long before this bites. This exists so a
        /// runaway cannot allocate unbounded offspring in a single step and take the process
        /// down with it — a guard on the implementation, not a pressure on evolution.
        /// </remarks>
        [Tunable("mutation")]
        public int MaxBroodSize { get; set; } = 64;

        public static MutationRates Default => new MutationRates();
    }
}
