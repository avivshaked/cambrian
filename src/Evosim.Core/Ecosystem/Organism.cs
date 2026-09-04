using System;

namespace Evosim.Core
{
    /// <summary>
    /// One living creature and its account — DESIGN.md §5A.2, §5A.6.
    /// </summary>
    /// <remarks>
    /// The genome and the developed body are held together with the energy, because under §5A a
    /// creature is not evaluated and scored — it is solvent or it is not, and solvency is the only
    /// thing about it that changes.
    /// </remarks>
    public sealed class Organism
    {
        /// <summary>Identity within a run. Also the row key in <c>lineage.jsonl</c> (§9).</summary>
        public long Id { get; internal set; }

        /// <summary>Id of the parent, or -1 for a founder spawned by the population floor.</summary>
        public long ParentId { get; internal set; } = -1;

        /// <summary>
        /// Reproduction events between this creature and the founder it descends from — §5A.6b.
        /// </summary>
        /// <remarks>
        /// Zero for a founder. This is the run's central instrument: minimum depth across the
        /// living is above zero exactly when no living creature is a floor spawn, which is the
        /// definition of a world running itself. It is free because reproduction is asexual and
        /// mutation-only, so a birth is one mutation event and this is a counter — which also
        /// makes it a measure of genetic distance from the founder.
        /// </remarks>
        public int GenerationDepth { get; internal set; }

        /// <summary>Seed the parent's genome was mutated with. The birth, in 8 bytes (§9).</summary>
        public ulong BirthSeed { get; internal set; }

        public Genome Genome { get; internal set; }
        public Phenotype Phenotype { get; internal set; }

        /// <summary>Joules in reserve. Death at zero (§5A.6).</summary>
        public float Energy { get; internal set; }

        /// <summary>Simulated seconds since birth.</summary>
        public float Age { get; internal set; }

        /// <summary>World height, metres. Y is up, so the surface is 0.</summary>
        /// <remarks>
        /// Written by <see cref="World.Observe"/> from the simulator, or inherited from the parent
        /// at birth for a world with no physics attached. It decides both of a creature's incomes —
        /// light falls off upward-to-downward and detritus sinks — so it is the one number that
        /// makes swimming worth doing.
        /// </remarks>
        public float HeightY { get; internal set; }

        /// <summary>Where this creature started, metres. Set once, at birth.</summary>
        /// <remarks>
        /// <b>Kept so that "did it move" is answerable at all.</b> Depth decides both incomes, and
        /// a creature inherits its parent's depth — so at any instant the population's depth
        /// distribution is mostly a record of where things were *born*, not of where they swam to.
        /// Those two are confounded in <see cref="HeightY"/> and separable only against this.
        ///
        /// It is the denominator of the question logbook/0021 asks: selection can only act on
        /// swimming to the extent that swimming moves a creature further than the spread it was
        /// born into. If birth depth varies over twenty metres and a lifetime of swimming is worth
        /// a tenth of one, the trait is invisible to selection however many generations run.
        /// </remarks>
        public float BirthHeightY { get; internal set; }

        /// <summary>
        /// Mechanical work done at the joints since the last metabolic step, in joules.
        /// </summary>
        /// <remarks>
        /// <b>Accumulated by <see cref="World.Observe"/> and consumed exactly once</b>, in
        /// <c>Metabolise</c>, which zeroes it. Physics steps far more often than the economy does,
        /// so this is a sum over many solver steps; and it must be drained rather than read,
        /// because a work term counted twice is an energy cost invented by the bookkeeping and a
        /// term never drained is a creature billed forever for one stroke.
        ///
        /// Zero for anything the simulator has not reported on — every plant, and every creature
        /// in a world running the economy alone.
        /// </remarks>
        public float PendingWorkJoules { get; internal set; }

        /// <summary>Standing cost in watts, cached at birth — the body does not change.</summary>
        /// <remarks>
        /// Recomputing it every step would be the single largest cost in a population loop that
        /// is otherwise arithmetic, and it cannot change: growth does not exist yet (§5A.6), so a
        /// creature's body is fixed from birth to death.
        /// </remarks>
        public float StandingWatts { get; internal set; }

        /// <summary>Everything this creature has earned and spent since birth.</summary>
        public EnergyLedger Lifetime { get; internal set; }

        /// <summary>
        /// Energy embodied in this body, in joules — what it cost to build and what it is worth
        /// dead. DESIGN.md §5A.2c.
        /// </summary>
        /// <remarks>
        /// <b>Held separately from <see cref="Energy"/> because it is not spendable.</b> A
        /// starving creature cannot metabolise its own body — there is no growth in this design
        /// and therefore no shrinking either (§5A.6) — so this sits outside the reserve that
        /// death-at-zero watches, and moves exactly twice: in when a parent builds it, out into
        /// the nutrient pool when it dies. Two movements of one number is what keeps the food web
        /// inside §5A.2's audit instead of alongside it.
        /// </remarks>
        public float TissueJoules { get; internal set; }

        /// <summary>
        /// Which horizontal cell of the world this creature occupies — D061. 0 for every creature
        /// whenever <see cref="RunConfig.HorizontalPatches"/> is 1 (the field this class carried
        /// before D061 existed). Set once at birth — <see cref="World"/>'s
        /// <c>Admit</c> — and changed only by <see cref="World"/>'s dispersal step
        /// (<see cref="RunConfig.DispersalChancePerStep"/>); an offspring otherwise inherits its
        /// parent's patch.
        /// </summary>
        public int Patch { get; internal set; }

        /// <summary>
        /// Matter still locked in this body, in <see cref="World.Matter"/>'s units — D048, D052,
        /// D065.
        /// </summary>
        /// <remarks>
        /// Set once at birth to the whole price the parent paid for this body
        /// (<see cref="RunConfig.MatterPerTissueJoule"/> × tissue, plus D065's fixed
        /// <see cref="RunConfig.MatterPerCreature"/>) and falls from there as the
        /// body excretes (<see cref="RunConfig.ExcretionPerJoule"/>); death returns whatever is
        /// left. Zero for a floor founder — a founder's tissue was never priced in matter, so it
        /// has none to give back, and both the excretion cap and the death payout read correctly
        /// with no special case for it.
        /// </remarks>
        public float LockedMatter { get; internal set; }

        /// <summary>
        /// Which clade this creature belongs to — D057. 0 for every creature whenever
        /// <see cref="RunConfig.SpeciesDriftThreshold"/> is 0; otherwise assigned once, at birth,
        /// by <see cref="World"/> and never touched again.
        /// </summary>
        /// <remarks>
        /// <b>Pure instrumentation — D057 is explicit that nothing may read this except a
        /// report.</b> No branch in <see cref="World"/>'s economy, in <see cref="Mutator"/>, or
        /// anywhere else consults it; it exists so a run can be asked "how many species" and "how
        /// long did this one last" after the fact, and for no other reason. Grep for reads of
        /// this property outside <c>World</c> and a report before trusting a change near it.
        /// </remarks>
        public uint SpeciesId { get; internal set; }

        /// <summary>
        /// Whether any part of the developed body is <see cref="CellTypeIds.Absorptive"/> —
        /// cached at birth by <see cref="World"/>'s <c>Admit</c>.
        /// </summary>
        /// <remarks>
        /// <b>Cached because the body cannot change.</b> There is no growth in this design
        /// (§5A.6), so a creature's cell types are fixed from birth to death, and the alternative
        /// is a loop over every part of every living creature on every metabolic step purely to
        /// decide whether an instrument should record it. Cached once, at the one moment the body
        /// is built, alongside the tissue price and the standing cost.
        /// </remarks>
        public bool HasAbsorptiveTissue { get; internal set; }

        /// <summary>
        /// Whether any part is <see cref="CellTypeIds.Photosynthetic"/> — cached with
        /// <see cref="HasAbsorptiveTissue"/>, and what makes an eater a mixotroph rather than a
        /// pure stomach.
        /// </summary>
        public bool HasPhotosyntheticTissue { get; internal set; }

        /// <summary>Volume of <see cref="CellTypeIds.Absorptive"/> tissue alone, m³. Cached at birth.</summary>
        /// <remarks>
        /// Separate from <see cref="Phenotype.TotalVolume"/> because a mixotroph's mouth is the
        /// part of it that eats: the D062 clearance model prices intake per cubic metre of
        /// absorptive tissue, so this is the number a break-even reading has to be taken against
        /// and the whole body is not.
        /// </remarks>
        public float AbsorptiveVolume { get; internal set; }

        /// <summary>Children this creature has actually produced — §5A.6's <c>Conceive</c>.</summary>
        /// <remarks>
        /// <b>Realised fecundity, not brood size.</b> <see cref="ReproductionTraits.BroodSize"/>
        /// is what a genome asks for; this is what it could afford, which is the only one of the
        /// two that bears on whether a lineage persists. Pure instrumentation — nothing in the
        /// economy branches on it.
        /// </remarks>
        public int Children { get; internal set; }

        /// <summary>
        /// World time the last child was born, s — <see cref="float.NaN"/> until there is one.
        /// </summary>
        /// <remarks>
        /// NaN rather than 0 or -1: t=0 is a real instant and a negative sentinel is a number a
        /// reader will average. Serialized as JSON <c>null</c> (<see cref="AbsorptiveSample"/>).
        /// </remarks>
        public double LastChildSeconds { get; internal set; } = double.NaN;

        /// <summary>
        /// The nutrient density the last metabolic step actually priced this creature's food at,
        /// J/m³ — captured by <c>World.Metabolise</c> for <see cref="AbsorptiveSample"/>.
        /// </summary>
        /// <remarks>
        /// <b>Captured rather than recomputed, and this is the point of the instrument.</b> The
        /// world knows the density, the share and the whole ledger at the instant it charges a
        /// creature for a step; asking again later would mean a second evaluation of
        /// <see cref="Metabolism.StepAt"/> per creature per sample, against a different field
        /// state, producing a plausible number that is not the one the creature was fed on.
        /// Written only for creatures with <see cref="HasAbsorptiveTissue"/> — everything else
        /// reads the zero it was born with, and nothing logs it.
        /// </remarks>
        public float LastDensityHere { get; internal set; }

        /// <summary>The demand-share the field granted at the last step, 0–1.</summary>
        public float LastShare { get; internal set; }

        /// <summary>The last metabolic step's ledger, whole. See <see cref="LastDensityHere"/>.</summary>
        public EnergyLedger LastLedger { get; internal set; }

        /// <summary>Length of the step <see cref="LastLedger"/> covers, s. 0 before the first.</summary>
        /// <remarks>
        /// Stored rather than assumed, because the harness sets the metabolic step from
        /// <c>EVOSIM_DT</c> and a watt figure divided by the wrong step is off by whatever factor
        /// that knob moved — which is exactly the kind of plausible-looking error §7's
        /// reproducibility rule exists to make impossible.
        /// </remarks>
        public float LastStepSeconds { get; internal set; }

        /// <summary>
        /// What <see cref="SensorChannel.Energy"/> reports: seconds of life left at the current
        /// burn rate — §4.4.
        /// </summary>
        public float SecondsOfReserve =>
            StandingWatts > 1e-9f ? Energy / StandingWatts : float.PositiveInfinity;

        /// <summary>Joules this creature must hold before it is worth attempting to reproduce — §5A.6.</summary>
        /// <remarks>
        /// <para>
        /// Derived from the creature's own evolved traits rather than configured, so a lineage
        /// that evolves a larger brood waits longer for it automatically and there is no separate
        /// constant to keep in sync.
        /// </para>
        /// <para>
        /// <b>An estimate, not the price.</b> Since §5A.2c a parent also builds each offspring's
        /// body, and what that costs is unknown until the mutated genome has been developed. This
        /// stands in <see cref="TissueJoules"/> — the parent's own body — because offspring are
        /// mutated copies and are nearly always close to the parent's size. Whoever passes this
        /// gate still pays the true price or is refused, so the estimate cannot buy anything; it
        /// only decides whether developing a genome is worth trying.
        /// </para>
        /// <para>
        /// <b>Leaving tissue out of the gate is not a small mistake.</b> With it omitted, every
        /// solvent creature clears a gate it cannot actually pay, mutates and develops a genome,
        /// discovers it is unaffordable and discards it — once per creature per step, for the
        /// whole run. The test suite went from 18 seconds to not finishing.
        /// </para>
        /// </remarks>
        public float ReproductionThreshold(float perOffspringOverheadJoules) =>
            Genome.Reproduction.CostJoules(perOffspringOverheadJoules + TissueJoules);

        public override string ToString() =>
            FormattableString.Invariant(
                $"#{Id} gen {GenerationDepth}, {Energy:0.#} J, {Age:0.#} s, {Phenotype.PartCount} parts");
    }

    /// <summary>Why a creature left the population — §5A.6, and the lineage record in §9.</summary>
    public enum DeathCause
    {
        /// <summary>Ran out of energy. The only cause the ecology has.</summary>
        Starved = 0,

        /// <summary>
        /// The solver diverged: the body's position or a part's velocity stopped being finite,
        /// and <see cref="World.KillDiverged"/> removed it.
        /// </summary>
        /// <remarks>
        /// <b>Not an ecological cause, and it must never be read as one.</b> Nothing about the
        /// creature's budget decided this — the articulation exploded and the fluid model relayed
        /// NaN velocities into NaN drag, which PhysX then refused nine steps running before
        /// <see cref="World.Observe"/> saw the non-finite height and took the whole run down
        /// (logbook/0056's censored <c>r20q-s1</c>). A run that reports any of these has lost a
        /// creature to arithmetic rather than to selection, and the number belongs in the report
        /// beside the audit for exactly that reason: it is an instrument reading, not a death
        /// rate. The body is still deposited and its matter still returned, because the world's
        /// books have to close whatever the solver did.
        /// </remarks>
        Diverged = 1,
    }

    /// <summary>How a creature entered the population. Never conflated — DESIGN.md §5A.6, D021.</summary>
    /// <remarks>
    /// <b>The distinction the whole run rests on.</b> A world topped up by the floor and a world
    /// sustaining itself produce identical population curves, identical birth counts and
    /// identical death counts. The only thing that tells them apart is which mechanism made each
    /// creature, so it is recorded per creature rather than inferred from aggregates.
    /// </remarks>
    public enum BirthKind
    {
        /// <summary>Spawned by the population floor because the world was under-populated.</summary>
        Floor = 0,

        /// <summary>Born to a parent that could afford it. What a living world produces.</summary>
        Reproduction = 1,

        /// <summary>
        /// Injected by <see cref="World.Inoculate"/> — D060's invasion assay. Shares the floor's
        /// energy accounting (created from nothing, not owed back), but is never the floor: it is
        /// a hand building the experimental condition at a chosen instant, not the mechanism that
        /// keeps a world populated.
        /// </summary>
        Inoculation = 2,
    }

    /// <summary>
    /// One entry in <see cref="World"/>'s species registry — D057. The genome a clade is
    /// measured from, and when it was founded.
    /// </summary>
    /// <remarks>
    /// Holds the founding genome itself rather than a distance summary, because
    /// <see cref="SpeciesDistance"/> needs the actual genome to compare a new child against and
    /// nothing cheaper would do. That is affordable only because species are expected to number
    /// far below the living population — a genome is roughly 5 KB (<see cref="Mutator"/>'s own
    /// remarks), a species is a clade that persists across many births, and D057 itself expects
    /// this table to stay small relative to <see cref="World.Living"/>. If a run's species count
    /// ever approached its creature count, this assumption would need revisiting before the
    /// registry's memory did.
    /// </remarks>
    public readonly struct SpeciesFounder
    {
        public Genome Genome { get; }
        public double FoundedAtSeconds { get; }

        public SpeciesFounder(Genome genome, double foundedAtSeconds)
        {
            Genome = genome;
            FoundedAtSeconds = foundedAtSeconds;
        }
    }
}
