using System;

namespace Evosim.Core
{
    /// <summary>
    /// A reserve a sensor can read — what <see cref="SensorChannel.Energy"/> reports, §4.4.
    /// </summary>
    /// <remarks>
    /// <b>An interface rather than the <see cref="Organism"/> itself, and for one reason.</b> The
    /// simulator's sampler wants the organism and gets it; the Milestone 1 smoke test wants a
    /// reserve it can move between samples, and it cannot build one — <see cref="Organism.Energy"/>
    /// is <c>internal set</c> so that nothing outside the world's own economy can pay a creature,
    /// which is a rule worth keeping. So the seam is one property wide, <see cref="Organism"/>
    /// implements it by already having it, and the test supplies a stand-in that varies.
    /// </remarks>
    public interface IReserveSource
    {
        /// <summary>Seconds of life left at the current burn rate; <c>+∞</c> at zero burn.</summary>
        float SecondsOfReserve { get; }
    }

    /// <summary>
    /// One living creature and its account — DESIGN.md §5A.2, §5A.6.
    /// </summary>
    /// <remarks>
    /// The genome and the developed body are held together with the energy, because under §5A a
    /// creature is not evaluated and scored — it is solvent or it is not, and solvency is the only
    /// thing about it that changes.
    /// </remarks>
    public sealed class Organism : IReserveSource
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

        /// <summary>
        /// The body as it is now: <see cref="AdultPhenotype"/> scaled to
        /// <see cref="BodyFraction"/> — fable-propose-growth.md rule 4 (2026-09-08).
        /// </summary>
        /// <remarks>
        /// <b>Everything that prices this creature reads this and not the adult.</b> Upkeep is per
        /// cubic metre, light income is lit area, feeding is clearance per cubic metre of
        /// absorptive tissue, drag is panels over the surface — so a half-grown body earns and
        /// spends as the body it actually is, with nothing having to know that growth exists.
        /// Replaced wholesale on a growth step rather than edited in place, so a reader may hold
        /// it for the length of a step.
        /// </remarks>
        public Phenotype Phenotype { get; internal set; }

        /// <summary>
        /// The body this creature is growing towards, developed once at birth at the genome's
        /// <see cref="Genome.AdultScale"/> — rule 4.
        /// </summary>
        /// <remarks>
        /// <b>Developed once, at full size, so that the small-part pruning rule judges the
        /// adult.</b> Developing a newborn at its own size would prune the parts it was about to
        /// grow, and the creature would reach adulthood with a different body plan from the one
        /// its genome describes — a body that changes shape as it grows rather than size. Held for
        /// the creature's whole life because every growth step scales from it: a chain of
        /// scalings from the current body would compound its own rounding.
        /// </remarks>
        public Phenotype AdultPhenotype { get; internal set; }

        /// <summary>
        /// How much of its adult body this creature has built, 0 to 1 — rule 4.
        /// </summary>
        /// <remarks>
        /// <b>A volume fraction, not a length one.</b> It is <see cref="TissueJoules"/> over
        /// <see cref="AdultTissueJoules"/>, which is what makes growth a transfer the audit can
        /// see: the fraction is a readout of the energy ledger rather than a second account beside
        /// it. The linear scale the body is built at is its cube root.
        /// </remarks>
        public float BodyFraction { get; internal set; } = 1f;

        /// <summary>Embodied energy of the finished adult body, J — what growth is aiming at.</summary>
        /// <remarks>
        /// Cached at birth for the reason <see cref="StandingWatts"/> used to be: it is
        /// <see cref="Metabolism.TissueJoules"/> over every part of a body that does not change,
        /// and growth reads it on every step of every growing creature.
        /// </remarks>
        public float AdultTissueJoules { get; internal set; }

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

        /// <summary>Standing cost in watts, refreshed whenever the body or the wear changes.</summary>
        /// <remarks>
        /// <b>This remark used to say the body does not change. It does now</b>
        /// (fable-propose-growth.md rule 4, 2026-09-08). Three places write it: birth, the
        /// metabolic step — which takes it from the ledger it has just computed, so senescence is
        /// already in it (D038) — and the growth step, which recomputes it from the newly scaled
        /// body at this creature's own age. Left at its birth value it would make
        /// <see cref="SecondsOfReserve"/>, and therefore §4.4's energy sensor, more optimistic the
        /// larger a creature grew.
        /// </remarks>
        public float StandingWatts { get; internal set; }

        /// <summary>Everything this creature has earned and spent since birth.</summary>
        public EnergyLedger Lifetime { get; internal set; }

        /// <summary>
        /// Energy embodied in this body, in joules — what it cost to build and what it is worth
        /// dead. DESIGN.md §5A.2c.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Held separately from <see cref="Energy"/> because it is not spendable.</b> A
        /// starving creature cannot metabolise its own body, so this sits outside the reserve
        /// that death-at-zero watches. There is growth in this design and still no shrinking
        /// (fable-propose-growth.md rule 5): the account only ever fills.
        /// </para>
        /// <para>
        /// <b>It moves three ways now, not two.</b> In when a parent builds it, in again on every
        /// growth step out of this creature's own reserve, and out into the water when it dies.
        /// Growth is a transfer between two accounts the audit already sums, so the books close
        /// for the reason a birth's did: nothing is created, only moved. And it is always
        /// <see cref="Metabolism.TissueJoules"/> of <see cref="Phenotype"/> — derived from the
        /// body rather than accumulated beside it, so the two cannot drift.
        /// </para>
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

        /// <summary>Position along the ring, m — D083. Read from the simulator with the height.</summary>
        /// <remarks>
        /// The patch centre until the simulator has reported, and in a world with no physics for
        /// its whole life. The cell field never reads it; the vertex field reads nothing else.
        /// </remarks>
        public float X { get; internal set; }

        /// <summary>Position across the box, m — D083. See <see cref="X"/>.</summary>
        public float Z { get; internal set; }

        /// <summary>Where this creature feeds, deposits and is priced — the one address both fields read.</summary>
        public FieldPoint Point => new FieldPoint(new Float3(X, HeightY, Z), Patch);

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
        /// <b>Cached because a body's cell types cannot change.</b> Growth moves a body's size and
        /// nothing else (fable-propose-growth.md rule 4), so what a creature is made of is still
        /// fixed from birth to death, and the alternative is a loop over every part of every living
        /// creature on every metabolic step purely to decide whether an instrument should record
        /// it. Cached once, at the one moment the plan is built.
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
        /// and the whole body is not. Refreshed on a growth step with the body it measures: a
        /// mouth grows with the rest of the creature.
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
        /// <b>The price, where it used to be an estimate</b> (fable-propose-growth.md rule 2,
        /// 2026-09-08). The old gate stood the parent's own tissue in for the child's, because a
        /// child was born at full size and what that cost could not be known until the mutated
        /// genome had been developed. A parent now spends a fraction of its own body plus the
        /// litter's overhead, and it knows both. The one way it pays less is a child whose share
        /// buys more body than it has adult to build: that surplus stays with the parent.
        /// </para>
        /// <para>
        /// <b>A half-grown body's gate is a half-grown body's.</b> The investment is a fraction of
        /// this creature's current tissue, so a newborn's threshold is small — and it still almost
        /// never breeds before it is grown, because growth runs first and takes its reserve down
        /// to <see cref="RunConfig.GrowthReserveFloor"/> of its tissue every step. Grow first and
        /// breed later falls out of the ordering rather than being a rule anybody wrote.
        /// </para>
        /// <para>
        /// <b>Leaving tissue out of the gate is not a small mistake.</b> With it omitted, every
        /// solvent creature clears a gate it cannot actually pay, mutates and develops a genome,
        /// discovers it is unaffordable and discards it — once per creature per step, for the
        /// whole run. The test suite went from 18 seconds to not finishing.
        /// </para>
        /// </remarks>
        public float ReproductionThreshold(float perOffspringOverheadJoules) =>
            Genome.Reproduction.CostJoules(TissueJoules, perOffspringOverheadJoules);

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
