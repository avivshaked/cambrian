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
    /// <b>A third number since D098:</b> <see cref="ReserveMargin"/>, what the parent keeps
    /// rather than what it spends. The first two decide the size and the price of a litter; that
    /// one decides how solvent the parent is when it walks away, and it is the only one of the
    /// three that can make a lineage breed later than it could afford to.
    /// </para>
    /// <para>
    /// That axis is r/K selection, and it is exactly the sort of thing the ecosystem should be
    /// able to discover rather than be told. In a productive, empty world the many-and-feeble
    /// strategy establishes fastest; under predation or scarcity, few-and-rich survives the
    /// search for the first meal. Neither is written in as better.
    /// </para>
    /// <para>
    /// <b>These are whole-creature traits, not per-part ones.</b> They live on the genome beside
    /// <see cref="Genome.AdultScale"/> rather than on a <see cref="MorphNode"/>, because a
    /// morph node may be instantiated many times by recursion and reproduction happens once per
    /// creature.
    /// </para>
    /// </remarks>
    public struct ReproductionTraits
    {
        /// <summary>Offspring produced per reproduction event. At least one.</summary>
        public int BroodSize;

        /// <summary>
        /// What a reproduction event costs, as a fraction of the parent's own tissue value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A fraction rather than a number of joules, since fable-propose-growth.md (2026-09-08).</b>
        /// The retired <c>OffspringEndowment</c> bought an offspring <i>time</i> and nothing else,
        /// because a child was born at its parent's size and the body was priced separately. Its
        /// own remark said what would happen once growth existed: the same number would decide how
        /// big the child gets to be, and the trade-off would sharpen. It does. A parent banks this
        /// much of its own body value, spends all of it on the litter, and each child's share is
        /// its whole start — the body it is born with plus its first reserve, split by
        /// <see cref="RunConfig.NewbornReserveFraction"/> so that no lineage can set its
        /// children's reserve to zero and pocket the difference.
        /// </para>
        /// <para>
        /// <b>Relative to the parent, so it stays a strategy at every body size.</b> A number of
        /// joules means one thing to a 30 kg body and another to a 300 kg one, so a lineage that
        /// grew would have to re-evolve the same reproductive decision it already had. A fraction
        /// travels.
        /// </para>
        /// </remarks>
        public float BirthInvestment;

        /// <summary>
        /// Reserve a parent keeps back after a birth, in seconds of its own standing cost —
        /// D098 §3.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A third evolved number, and the one that decides when rather than how much.</b>
        /// <see cref="BroodSize"/> and <see cref="BirthInvestment"/> between them say what a
        /// litter costs and how it is divided; neither says anything about what the parent is
        /// left standing in. A creature that breeds the instant it can afford to walks out of
        /// every birth with nothing, and the next unlucky step kills it. One that holds a
        /// fortnight of upkeep in hand breeds later and survives the dark. Which of those wins
        /// is a property of the world — how variable the income is, how long a drought lasts —
        /// and is exactly the sort of thing that should be discovered here rather than declared
        /// in <see cref="RunConfig"/>.
        /// </para>
        /// <para>
        /// <b>In seconds of standing cost, not in joules.</b> The same reasoning as the
        /// investment's: a number of joules means one thing to a small body and another to a
        /// large one, so a lineage that grew would have to re-evolve the caution it already had.
        /// Multiplied by <see cref="Organism.StandingWatts"/> it is a span of time the body can
        /// survive earning nothing, which is what <see cref="Organism.SecondsOfReserve"/> already
        /// reports to the creature's own Energy sensor — the gene and the sense are in the same
        /// unit.
        /// </para>
        /// <para>
        /// Zero is legal and is the default: it is the world as it stood before D098, where the
        /// gate was the price alone.
        /// </para>
        /// </remarks>
        public float ReserveMargin;

        /// <summary>
        /// How the parent pays for a child — the owner's ruling of 2026-09-24 on round 47's
        /// dissection. Serialised by name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="ReproductionMode.Lump"/> is the world as recorded: the whole price is taken
        /// from the reserve at the moment of conception, which binds a small body whose reserve
        /// never holds the overhead before senescence closes its window. Every founder and every
        /// stored genome is a lump breeder.
        /// </para>
        /// <para>
        /// <see cref="ReproductionMode.Gestation"/> pays as it goes: on every metabolic step whose
        /// net income is positive, <see cref="GestationShare"/> of that net moves from the reserve
        /// into <see cref="Organism.GestationJoules"/>, and the child is conceived from that
        /// account once it holds the child's price. Upkeep never draws on the account.
        /// </para>
        /// </remarks>
        public ReproductionMode Mode;

        /// <summary>
        /// The share of each step's positive net income a gestating parent moves into its
        /// gestation account, in (0, 1]. Read only in <see cref="ReproductionMode.Gestation"/>.
        /// </summary>
        /// <remarks>
        /// Carried on a lump breeder too, so that a mutation that flips the mode starts from the
        /// share the lineage last walked rather than from a constant. Zero is legal only in
        /// <see cref="ReproductionMode.Lump"/>: a gestating parent that banks nothing never breeds.
        /// </remarks>
        public float GestationShare;

        /// <summary>
        /// Total energy a reproduction event costs the parent, in joules.
        /// </summary>
        /// <param name="parentTissueJoules">
        /// The parent's own embodied energy — <see cref="Organism.TissueJoules"/>. Passed rather
        /// than stored because these traits are a recipe and the body is a creature: two
        /// creatures grown from one genome, one of them half-grown, do not owe the same amount.
        /// </param>
        /// <param name="perOffspringOverhead">
        /// Fixed cost per offspring on top of its share — gestation, division, the tissue
        /// itself. A world constant and deliberately not evolved: a creature allowed to set its
        /// own overhead would set it to zero, and then brood size would be free and every
        /// lineage would converge on the largest brood it could express. This is the term that
        /// makes a big brood genuinely costlier per head.
        /// </param>
        /// <remarks>
        /// Also the reproduction threshold, which is why §5A.6 does not need one as a separate
        /// constant: a creature reproduces once it holds this much. Deriving it means a creature
        /// that evolves a larger brood automatically waits longer for it, with nothing to keep in
        /// sync. Note what the litter no longer multiplies: the investment is spent whole however
        /// many ways it is divided, so brood size decides how big each child is rather than how
        /// much the event costs. The overhead is the only per-head term, and is what keeps a brood
        /// of forty from being free.
        /// <para>
        /// The parameter and the answer are doubles since 2026-09-22, because the tissue they are
        /// asked about is one (<see cref="Organism.TissueJoules"/>). The traits themselves stay
        /// floats: they are genome, and a gene's width is a fact about the genome format.
        /// </para>
        /// </remarks>
        public double CostJoules(double parentTissueJoules, float perOffspringOverhead) =>
            BirthInvestment * parentTissueJoules + (double)BroodSize * perOffspringOverhead;

        /// <summary>
        /// The litter's price under a config's overhead rule — the owner's ruling of 2026-09-24:
        /// the overhead scales with the child, above a floor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The overhead per child is <see cref="RunConfig.OverheadFor"/> of the tissue a child is
        /// born with, which cannot be known before the mutant is developed. So this is the
        /// estimate the gate needs: each child's tissue is taken at its share less the newborn
        /// reserve, <c>BirthInvestment × parentTissue / BroodSize × (1 − NewbornReserveFraction)</c>,
        /// which is the most a child can be born with. The exact price is asked again at
        /// conception.
        /// </para>
        /// <para>
        /// At <see cref="RunConfig.PerOffspringOverheadPerTissueJoule"/> 0 this is exactly
        /// <see cref="CostJoules(double, float)"/> at the floor, bit for bit, so every recorded
        /// config breeds at the gate it was recorded at.
        /// </para>
        /// </remarks>
        public double CostJoules(double parentTissueJoules, RunConfig config)
        {
            if (!(config.PerOffspringOverheadPerTissueJoule > 0f))
            {
                return CostJoules(parentTissueJoules, config.PerOffspringOverheadJoules);
            }

            double share = BirthInvestment * parentTissueJoules / BroodSize;
            double childTissue = share * (1d - config.NewbornReserveFraction);
            return BirthInvestment * parentTissueJoules +
                   (double)BroodSize * config.OverheadFor(childTissue);
        }

        public ReproductionTraits Clone() => this;

        public override string ToString() =>
            System.FormattableString.Invariant(
                $"brood {BroodSize} at {BirthInvestment:0.###} of tissue, keeping {ReserveMargin:0.#} s of standing cost, ") +
            (Mode == ReproductionMode.Gestation
                ? System.FormattableString.Invariant($"gestating {GestationShare:0.###} of net")
                : "paid in one lump");
    }

    /// <summary>How a parent pays for a child — see <see cref="ReproductionTraits.Mode"/>.</summary>
    /// <remarks>
    /// Serialised by name (the genome file and nothing else), so the order here is free; Lump is
    /// first so that <c>default</c> is the recorded behaviour.
    /// </remarks>
    public enum ReproductionMode
    {
        /// <summary>The whole price from the reserve at conception. Every world before 2026-09-24.</summary>
        Lump = 0,

        /// <summary>Paid as it goes into <see cref="Organism.GestationJoules"/>, conceived from it.</summary>
        Gestation = 1,
    }
}
