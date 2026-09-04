namespace Evosim.Core
{
    /// <summary>
    /// How a creature spends surplus energy on offspring — DESIGN.md §5A.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two evolved numbers rather than one, and the pairing is the whole point. A creature that
    /// could only choose <i>how much</i> to invest would have nothing to select on: four
    /// offspring endowed with E cost the same as four separate reproductions endowed with E, so
    /// brood size would differ from serial reproduction only in timing. Splitting the decision
    /// into "how many" and "how well provisioned" is what makes it a strategy — the same
    /// surplus buys one well-fed offspring or eight feeble ones, and which of those wins is a
    /// property of the world rather than of this file.
    /// </para>
    /// <para>
    /// That axis is r/K selection, and it is exactly the sort of thing the ecosystem should be
    /// able to discover rather than be told. In a productive, empty world the many-and-feeble
    /// strategy establishes fastest; under predation or scarcity, few-and-rich survives the
    /// search for the first meal. Neither is written in as better.
    /// </para>
    /// <para>
    /// <b>These are whole-creature traits, not per-part ones.</b> They live on the genome beside
    /// <see cref="Genome.GlobalBrain"/> rather than on a <see cref="MorphNode"/>, because a
    /// morph node may be instantiated many times by recursion and reproduction happens once per
    /// creature.
    /// </para>
    /// <para>
    /// <b>Declared before it is used.</b> Reproduction itself lands at Milestone 5. These fields
    /// exist now so that §9's serialization format does not change when it does — the same
    /// reasoning that put the unimplemented sensor channels in <see cref="SensorChannel"/> early.
    /// </para>
    /// </remarks>
    public struct ReproductionTraits
    {
        /// <summary>Offspring produced per reproduction event. At least one.</summary>
        public int BroodSize;

        /// <summary>
        /// Energy each offspring starts with, in joules, paid out of the parent's reserve.
        /// </summary>
        /// <remarks>
        /// Until growth exists (§5A.6 defers it) an offspring is born full-size, so endowment
        /// buys it <i>time</i> rather than body: how long it can search before it starves. With
        /// growth the same number would also decide how big it gets to be, and the trade-off
        /// would sharpen considerably. Worth remembering when reading early runs — a
        /// many-and-feeble strategy is being tested under conditions unusually kind to it.
        /// </remarks>
        public float OffspringEndowment;

        /// <summary>
        /// Total energy a reproduction event costs the parent, in joules.
        /// </summary>
        /// <param name="perOffspringOverhead">
        /// Fixed cost per offspring on top of its endowment — gestation, division, the tissue
        /// itself. A world constant and deliberately not evolved: a creature allowed to set its
        /// own overhead would set it to zero, and then brood size would be free and every
        /// lineage would converge on the largest brood it could express. This is the term that
        /// makes a big brood genuinely costlier per head.
        /// </param>
        /// <remarks>
        /// Also the reproduction threshold, which is why §5A.6 does not need one as a separate
        /// constant: a creature reproduces once it holds this much above whatever reserve it
        /// keeps for itself. Deriving it means a creature that evolves a larger brood
        /// automatically waits longer for it, with nothing to keep in sync.
        /// </remarks>
        public float CostJoules(float perOffspringOverhead) =>
            BroodSize * (OffspringEndowment + perOffspringOverhead);

        public ReproductionTraits Clone() => this;

        public override string ToString() =>
            System.FormattableString.Invariant($"brood {BroodSize} x {OffspringEndowment:0.#} J");
    }
}
