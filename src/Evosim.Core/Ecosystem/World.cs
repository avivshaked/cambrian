using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The world exceeded a runaway ceiling. DESIGN.md §5A.7, D021, and fable-propose-growth.md
    /// rule 9's tissue ceiling.
    /// </summary>
    /// <remarks>
    /// Its own type so a sweep harness can catch it and record "this configuration exploded" as
    /// a result rather than as a crash. A runaway is a measurement: it locates one end of the
    /// transition in §5A.6b just as precisely as extinction locates the other.
    /// <para>
    /// Two ceilings can throw this, and <see cref="Ceiling"/> says which one did. Growth made
    /// the count ceiling alone unreliable: a child is born at a median third of its adult body,
    /// so 5,000 bodies can be a third of the biomass that count was calibrated against, and the
    /// same count no longer means the same photosynthetic mat it used to (the owner's ruling of
    /// 2026-09-09, after logbook/0081). <see cref="Population"/> and <see cref="TissueJoules"/>
    /// are both always populated, whichever ceiling fired, so a catcher never has to re-read the
    /// world to get the reading the message did not need.
    /// </para>
    /// </remarks>
    public sealed class PopulationRunawayException : Exception
    {
        public int Population { get; }

        /// <summary>Standing tissue joules across the living at the moment this was thrown.</summary>
        public double TissueJoules { get; }

        public double ElapsedSeconds { get; }

        /// <summary>Which ceiling fired: <c>"population"</c> or <c>"tissue"</c>.</summary>
        public string Ceiling { get; }

        public PopulationRunawayException(
            string message, int population, double tissueJoules, double elapsedSeconds, string ceiling)
            : base(message)
        {
            Population = population;
            TissueJoules = tissueJoules;
            ElapsedSeconds = elapsedSeconds;
            Ceiling = ceiling;
        }
    }

    /// <summary>
    /// The ecosystem loop: creatures earn, spend, breed and starve — DESIGN.md §5A.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here scores anything.</b> There is no fitness function, no selection step and
    /// no ranking. A creature persists while it is solvent and stops when it is not, and that is
    /// the entire selective mechanism (§5A.0, D017).
    /// </para>
    /// <para>
    /// <b>No physics, deliberately, and this is what makes the design testable at all.</b>
    /// Photosynthetic income depends on lit area and depth; upkeep depends on tissue; neither
    /// needs a solver. Height and mechanical work are the only physical quantities in §5A.2's
    /// ledger and both arrive through <see cref="Observe"/>, so the calibration question §5A.2
    /// calls the knob that decides everything can be swept in milliseconds instead of stepped
    /// through PhysX at 6.4 ms per step (§5A.9).
    /// </para>
    /// <para>
    /// <b>A world with nothing calling <see cref="Observe"/> is a world of stationary
    /// organisms for whom swimming is free</b>, and that is what every number in §5A.2b was
    /// measured against. It remains a legitimate configuration — it is the fast sweep — but it is
    /// a different world from the embodied one, and results from the two are not interchangeable.
    /// </para>
    /// <para>
    /// <b>The population floor is the only endogenous thing that creates a creature from
    /// nothing</b> (D021), including at t=0. There is no separate seeding path, so the mechanism
    /// that repopulates a collapsing world is the same one exercised on the very first step —
    /// tested continuously rather than once. <see cref="Inoculate"/> is the one deliberate
    /// exception, and it is exogenous by design: D060's invasion assay is a labeled hand that
    /// builds an experimental condition at a chosen instant, never something the world does on
    /// its own.
    /// </para>
    /// </remarks>
    public sealed class World
    {
        private readonly List<Organism> _living = new List<Organism>();
        private readonly List<Organism> _dead = new List<Organism>();
        private readonly List<Organism> _born = new List<Organism>();

        /// <summary>
        /// Bodies that have died and not yet finished leaking into the water. Rule 6 of
        /// <c>fable-propose-grid.md</c>. Always empty at
        /// <see cref="RunConfig.CorpseDecayPerSecond"/> 0, which is every run on file.
        /// </summary>
        private readonly List<Corpse> _corpses = new List<Corpse>();

        /// <summary>
        /// Births and deaths since the last <see cref="DrainLineageEvents"/> — pure
        /// instrumentation for <c>lineage.jsonl</c> (see <see cref="LineageEvent"/>). Swapped out
        /// rather than copied-and-cleared on drain, so a report interval with nothing to report
        /// costs nothing but reading a reference.
        /// </summary>
        private List<LineageEvent> _lineageEvents = new List<LineageEvent>();

        /// <summary>This step's ledgers, parallel to <c>_living</c>. Reused, never reallocated.</summary>
        private readonly List<EnergyLedger> _ledgers = new List<EnergyLedger>();

        /// <summary>
        /// Final rows for absorptive creatures that have died since the last
        /// <see cref="CollectAbsorptiveLog"/> — the one place a dead creature's terminal budget
        /// survives long enough to be written.
        /// </summary>
        /// <remarks>
        /// A queue rather than a write, for <see cref="LineageEvent"/>'s reason: §6.1 forbids
        /// <c>UnityEngine</c> here and nothing in this assembly touches disk. Values, not
        /// references — a reference would either keep a dead body alive or read fields the death
        /// path has already zeroed.
        /// </remarks>
        private readonly List<AbsorptiveSample> _absorptiveDeaths = new List<AbsorptiveSample>();

        /// <summary>Death rows dropped because the buffer was full — see <see cref="AbsorptiveLogRowCap"/>.</summary>
        private int _absorptiveDeathsDropped;

        private long _nextId;
        /// <summary>
        /// Counter behind every per-creature seed. Mixed with <see cref="Seed"/> rather than used
        /// raw — see <see cref="Rng.SeedFor"/> for why consecutive seeds are not independent runs.
        /// </summary>
        private ulong _nextIndex;

        /// <summary>
        /// The index <see cref="_conceptionRng"/>'s seed is drawn at — reserved, and never handed
        /// to <see cref="_nextIndex"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="_nextIndex"/> numbers genomes: a founder, an offspring, an inoculum and a
        /// patch draw each take the next one. The conception-order stream is not a genome and must
        /// not advance that counter, because a world that drew one extra index at construction
        /// would hand every creature after it a different seed and stop replaying the record.
        /// Reserved at the far end of the range rather than at 0, which is a real genome index on
        /// the first step of every run.
        /// </remarks>
        private const ulong ConceptionOrderIndex = ulong.MaxValue - 1UL;

        /// <summary>
        /// The index D077's placement stream is seeded at — reserved beside
        /// <see cref="ConceptionOrderIndex"/> and never handed to <see cref="_nextIndex"/>.
        /// </summary>
        /// <remarks>
        /// Declared here rather than where it is used because this is the register of reserved
        /// indices: the stream itself belongs to <c>Evosim.Sim</c>'s <c>Ecosystem</c>, which owns
        /// the coordinates a placement is drawn in, and two files each picking their own "far end
        /// of the range" is how two streams end up being one. Placement draws from its own stream
        /// for <see cref="ConceptionOrderIndex"/>'s reason: where a body lands must not change
        /// which genome anything else is given.
        /// </remarks>
        public const ulong PlacementIndex = ulong.MaxValue - 2UL;

        /// <summary>
        /// The streams behind D083's two vertex fields — where an emitted vertex lands, which
        /// floor vertex is buried, and every step of the mixing walk. Their own, so a field knob
        /// perturbs no other draw in the world; constructed for every vertex world and drawn from
        /// by nothing in a cell world.
        /// </summary>
        public const ulong DetritusFieldIndex = ulong.MaxValue - 3UL;
        public const ulong MatterFieldIndex = ulong.MaxValue - 4UL;

        /// <summary>
        /// The stream behind <see cref="CurrentMode.Transport"/>'s phases. Its own, so that the
        /// water a seed gets is decided by the seed and by nothing a knob elsewhere perturbs; it
        /// takes no draw at all in a <see cref="CurrentMode.Rolls"/> world, because the field is
        /// never built there.
        /// </summary>
        public const ulong CurrentFieldIndex = ulong.MaxValue - 5UL;

        /// <summary>
        /// The stream behind <see cref="ConceptionOrder.Shuffled"/> — D072. Constructed for every
        /// world and drawn from by none but a shuffled one.
        /// </summary>
        /// <remarks>
        /// Its own stream rather than a share of the per-creature seeds, so that turning the knob
        /// on changes the walk order and nothing else: under <see cref="ConceptionOrder.Age"/> not
        /// a single draw is taken from it, and a default run is step for step what it always was.
        /// </remarks>
        private readonly Rng _conceptionRng;

        /// <summary>
        /// Indices into <c>_living</c>, permuted each step under
        /// <see cref="ConceptionOrder.Shuffled"/>. Reused rather than reallocated, in the manner
        /// of <c>_ledgers</c> — this runs once per metabolic step for the life of a run.
        /// </summary>
        private int[] _conceptionOrder = Array.Empty<int>();

        /// <summary>
        /// Each living body's energy surplus above its breeding gate, filled once per step under
        /// <see cref="ConceptionOrder.Reserve"/> — D073, logbook/0057.
        /// </summary>
        /// <remarks>
        /// Indexed by position in <c>_living</c> rather than by rank, because the comparer reads it
        /// by the index it is handed. Kept and reused for <see cref="_conceptionOrder"/>'s reason.
        /// </remarks>
        private float[] _conceptionSurplus = Array.Empty<float>();

        /// <summary>
        /// <see cref="ConceptionOrder.Reserve"/>'s ordering. Held rather than built each step, in
        /// the manner of everything else this walk touches.
        /// </summary>
        private readonly IComparer<int> _byReserve;

        /// <summary>
        /// Species registry — D057. Founding genome and founding time, keyed by
        /// <see cref="Organism.SpeciesId"/>. Empty for the life of a run whose
        /// <see cref="RunConfig.SpeciesDriftThreshold"/> is 0.
        /// </summary>
        private readonly Dictionary<uint, SpeciesFounder> _species = new Dictionary<uint, SpeciesFounder>();

        /// <summary>
        /// Counter behind every species id, assigned in the same world-step order as everything
        /// else here — D057 — so that <c>(genome, seed, configHash)</c> replays it exactly.
        /// </summary>
        private uint _nextSpeciesId;

        /// <summary>The seed this world was constructed with. Every creature's seed derives from it.</summary>
        public ulong Seed { get; }

        public RunConfig Config { get; }

        /// <summary>How much light reaches each depth — <see cref="RunConfig.Light"/>.</summary>
        /// <remarks>
        /// Read from the config rather than accepted alongside it. Passing it separately let a
        /// world run at an irradiance its own <c>configHash</c> knew nothing about, which is §7's
        /// exact failure and went unnoticed through the whole §5A.2b sweep (logbook/0013). One
        /// source, so the two cannot disagree.
        /// </remarks>
        public LightModel Light => Config.Light;

        /// <summary>
        /// How this step's light was divided — §5A.2b. Rebuilt every step.
        /// </summary>
        /// <remarks>
        /// The world's carrying capacity lives here rather than in a population number: the sun's
        /// aperture is finite, so <see cref="LightField.IncidentWatts"/> bounds total income no
        /// matter how many creatures there are. Exposed because a sweep wants to report how much
        /// of the incident power the population is actually capturing, which is the honest measure
        /// of how full a world is.
        /// </remarks>
        public LightField Field { get; }

        /// <summary>
        /// Horizontal cells per layer, K ≥ 1 — <see cref="RunConfig.HorizontalPatches"/>, clamped
        /// the same way the D060/D061-era knobs are (cast to int, floored at 1 so a stray
        /// fractional or non-positive config value cannot construct a zero-patch field). D061.
        /// </summary>
        private int PatchCount => Math.Max(1, (int)Config.HorizontalPatches);

        /// <summary>Dead matter in the water, and what feeds on it — §5A.2c.</summary>
        public IMatterField Nutrients { get; }

        /// <summary>The world's stock of matter, by depth layer — D048.</summary>
        /// <remarks>
        /// <para>
        /// A <see cref="NutrientField"/> by construction because the mechanics are identical —
        /// depth layers, sinking, mixing — and a second copy of that arithmetic is how two things
        /// obliged to agree drift apart. <b>Its unit is matter, not joules.</b> The type's own
        /// vocabulary says joules throughout; here every such quantity is matter, and the two are
        /// never added.
        /// </para>
        /// <para>
        /// <b>Deliberately absent from <see cref="StandingJoules"/>.</b> §5A.2's audit is a hard
        /// equality over energy, and matter is not energy — folding this in would make the books
        /// balance by counting a different substance, which is precisely the failure the audit
        /// exists to catch.
        /// </para>
        /// </remarks>
        public IMatterField Matter { get; }

        /// <summary>
        /// Total matter in the world, free and locked up — D048. Conserved until D074's budget is
        /// opened; after that it is <see cref="MatterInitialTotal"/> plus what has flowed in and
        /// minus what has been buried.
        /// </summary>
        /// <remarks>
        /// The meaning has not changed — this is still everything the world holds — but it is no
        /// longer a constant, and anything that asserted its constancy was asserting D048 rather
        /// than reading an invariant. The invariant that survives is the identity in
        /// <see cref="MatterInfluxedTotal"/>'s remarks, which reduces to the old one at influx and
        /// burial 0.
        /// </remarks>
        public double StandingMatter => Matter.TotalJoules + MatterInBodies + CorpseMatter;

        /// <summary>Matter locked up in living tissue, awaiting its owner's death.</summary>
        public double MatterInBodies { get; private set; }

        /// <summary>
        /// Bodies that have died and are still handing their tissue and matter back to the water,
        /// oldest first. Rule 6 of <c>fable-propose-grid.md</c>. Read-only from outside: a corpse
        /// is founded by <see cref="Bury"/> and emptied by the world's own pass, and nothing else
        /// may move a joule of it without the audit noticing.
        /// </summary>
        /// <remarks>
        /// Empty for the whole life of a run with <see cref="RunConfig.CorpseDecayPerSecond"/> at
        /// 0, which is every run on file: at 0 a death deposits at once and founds nothing.
        /// </remarks>
        public IReadOnlyList<Corpse> Corpses => _corpses;

        /// <summary>Tissue still held by the dead, J. A third standing account beside the water and the living.</summary>
        public double CorpseJoules
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _corpses.Count; i++) sum += _corpses[i].Joules;
                return sum;
            }
        }

        /// <summary>Matter still held by the dead. Part of D074's identity, like the two accounts beside it.</summary>
        public double CorpseMatter
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _corpses.Count; i++) sum += _corpses[i].Matter;
                return sum;
            }
        }

        /// <summary>
        /// What <see cref="RunConfig.InitialMatterPerCubicMetre"/> seeded the world with at construction —
        /// D074. The whole of <see cref="StandingMatter"/> before anything happened.
        /// </summary>
        /// <remarks>
        /// Recorded rather than recomputed, because the seeding loop is the only moment it can be
        /// read cleanly: one step later the fields have settled and mixed, and while both of those
        /// conserve, reading a "starting stock" off a world that has already run is the kind of
        /// inference the audit exists to make unnecessary.
        /// </remarks>
        public double MatterInitialTotal { get; }

        /// <summary>
        /// Every unit of matter D074's influx has ever added to the world. Cumulative and never
        /// decremented; 0 for the whole life of a run with
        /// <see cref="RunConfig.MatterInfluxPerSecond"/> at 0, which is every run before it.
        /// </summary>
        /// <remarks>
        /// <b>With <see cref="MatterBuriedTotal"/> this is the matter audit.</b>
        /// <c>MatterInitialTotal + MatterInfluxedTotal − MatterBuriedTotal ==
        /// Matter.TotalJoules + MatterInBodies</c> at every step, because everything else matter
        /// does — settling, mixing, advection, remineralisation, conception locking it away and
        /// death giving it back — moves it between cells and bodies without creating or destroying
        /// any. These two counters are the only holes in that wall, which is exactly why they are
        /// counted separately rather than folded into a single net figure: a net number that
        /// happens to be right cannot tell a doubled influx from a doubled burial.
        /// </remarks>
        public double MatterInfluxedTotal { get; private set; }

        /// <summary>
        /// Every unit of matter D074's burial has ever removed from the world. Cumulative; see
        /// <see cref="MatterInfluxedTotal"/> for the identity the two close.
        /// </summary>
        public double MatterBuriedTotal { get; private set; }

        /// <summary>
        /// Everything excretion (D052) has ever moved from bodies into the field, J. Cumulative
        /// and never decremented — a report reads it as a rate by differencing two samples, the
        /// same trick <see cref="FloorSpawns"/> already asks of a caller.
        /// </summary>
        /// <remarks>
        /// Internal to the transfer <see cref="StandingMatter"/> already accounts for: this does
        /// not change what is conserved, only makes the D052 flux itself visible instead of only
        /// its before-and-after balance — the pre-round-8 experiment contract's excretion flux
        /// column needed a counter that did not exist yet.
        /// </remarks>
        public double ExcretedTotal { get; private set; }

        /// <summary>
        /// Every joule a dead body has ever put into <see cref="Nutrients"/> — cumulative and
        /// never decremented; a report reads it as a rate by differencing two samples.
        /// </summary>
        /// <remarks>
        /// Added for the detritus-flux instrument (logbook/0050's closing, the
        /// <c>fable-propose-detritus-flux</c> proposal): round 14's lines ate a stock whose income
        /// could only be inferred from the slope of <see cref="NutrientField.TotalJoules"/> with no
        /// grazer on it. With <see cref="DetritusExudedTotal"/> and
        /// <see cref="DetritusTakenTotal"/> the three are a measurement, and
        /// <c>DetritusDepositedTotal + DetritusExudedTotal - DetritusTakenTotal ==
        /// Nutrients.TotalJoules</c> at every step, because settling, mixing, advection and
        /// remineralisation all conserve.
        /// <para>
        /// With <see cref="RunConfig.CorpseDecayPerSecond"/> above 0 this counts the instalments a
        /// corpse pays and not the death that founded it, which is what keeps that identity true:
        /// crediting the water with a whole body while the corpse still holds it would open the
        /// identity for as long as the corpse lasted.
        /// </para>
        /// </remarks>
        public double DetritusDepositedTotal { get; private set; }

        /// <summary>
        /// Every joule a *living* body has ever released into <see cref="Nutrients"/> — D070's
        /// exudation. Cumulative; see <see cref="DetritusDepositedTotal"/>.
        /// </summary>
        /// <remarks>
        /// <b>Its own counter, not folded into the deposits.</b> D070 exists because dead tissue
        /// alone feeds the second trophic level at about 1% of primary production, and the whole
        /// question the first arm has to answer is how much of the field's income the new route
        /// supplies. One combined counter would show the field's income rise and say nothing about
        /// which half rose. Zero for the whole life of a run with
        /// <see cref="RunConfig.ExudationFraction"/> at 0, which is every run before this one.
        /// </remarks>
        public double DetritusExudedTotal { get; private set; }

        /// <summary>
        /// Every joule feeding has ever taken out of <see cref="Nutrients"/> — its only outflow.
        /// Cumulative; see <see cref="DetritusDepositedTotal"/>.
        /// </summary>
        public double DetritusTakenTotal { get; private set; }

        /// <summary>
        /// Takes that returned less than the ledger asked for, running total. Zero by
        /// construction while availability is frozen for the consumption pass and every
        /// rationed draw is capped at its share; a nonzero count means the allocation has a
        /// hole, and the creature was credited what it got rather than what it planned.
        /// </summary>
        public long PoolShortTakes { get; private set; }

        /// <summary>
        /// The matter the living actually hold, summed body by body. Equal to
        /// <see cref="MatterInBodies"/> whenever nothing has been charged for a body that does
        /// not exist -- the invariant the Astra review's R2 found broken (2026-09-07). O(n): an
        /// instrument for tests and diagnostics, not for the step.
        /// </summary>
        public double MatterInLivingBodies
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].LockedMatter;
                return sum;
            }
        }

        /// <summary>
        /// Matter the smallest child physically expressible would cost — D048. A strict lower
        /// bound, cached because it depends only on config.
        /// </summary>
        /// <remarks>
        /// <c>Conceive</c> pays for a full <c>Mutator.Mutate</c> and <c>Developer.Develop</c>
        /// before it knows the child's tissue, and therefore before it can price the child in
        /// matter. In a world where matter binds that is almost all wasted: the first probe ran
        /// **944 blocked conceptions per birth**, 2.3 million mutate-and-develop pairs built and
        /// thrown away, and it is why that run reached t=2,100 rather than its 4,000 s budget.
        ///
        /// Tissue is <c>Σ volume × TissueEnergyPerCubicMetre</c> and a viable body has at least
        /// one part, so the cheapest cell type at the smallest legal part volume bounds every
        /// possible child from below. A layer that cannot afford *that* cannot afford anything,
        /// which makes this an exact test rather than a heuristic — no conception is refused that
        /// the full check would have allowed.
        /// </remarks>
        private float CheapestPossibleChildMatter
        {
            get
            {
                if (_cheapestChildMatter < 0f)
                {
                    float cheapestPerCubicMetre = float.MaxValue;
                    foreach (string id in Config.CellTypes.Ids())
                    {
                        float rate = Config.CellTypes.Resolve(id).TissueEnergyPerCubicMetre;
                        if (rate < cheapestPerCubicMetre) cheapestPerCubicMetre = rate;
                    }

                    // D065's fixed term is added outside the tissue product, not folded into it:
                    // it is what a body costs *before* anything is proportional to its size, so
                    // the smallest possible child still cannot be cheaper than this.
                    _cheapestChildMatter = Config.MatterPerTissueJoule *
                        Config.Development.MinPartVolume * cheapestPerCubicMetre +
                        Config.MatterPerCreature;
                }

                return _cheapestChildMatter;
            }
        }

        private float _cheapestChildMatter = -1f;

        /// <summary>Conceptions refused for want of matter rather than energy — D048.</summary>
        /// <remarks>
        /// The only number that says whether matter is binding at all. A world where this stays
        /// zero has the mechanism switched on and doing nothing, which reads in every other
        /// column exactly like a world that does not have it.
        /// </remarks>
        public long ConceptionsBlockedByMatter { get; private set; }

        /// <summary>
        /// Conceptions refused after the gate passed, because the take came up short — D083's
        /// second amendment. Zero on a cell field by construction; on a vertex field a
        /// rounding's worth, and anything more is a fault in the take.
        /// </summary>
        public long ConceptionsShortOfMatter { get; private set; }

        /// <summary>
        /// Growth steps at which matter, rather than energy or the adult body, was what bound —
        /// fable-propose-growth.md rule 5.
        /// </summary>
        /// <remarks>
        /// <b>A count of bodies-times-steps, not of creatures.</b> One body held short for a
        /// thousand steps and a thousand bodies held short once read the same here, so it is read
        /// against the population the way <c>mat blk</c> and <c>crowded</c> are (CLAUDE.md). What
        /// it answers is the question growth adds to a matter-limited world: whether bodies are
        /// small because their lineages chose small, or because the water would not pay for the
        /// rest of them.
        /// </remarks>
        public long GrowthShortOfMatter { get; private set; }

        /// <summary>
        /// Conceptions refused because a newborn part would have been lighter than
        /// <see cref="RunConfig.MinNewbornPartKilograms"/> — rule 3.
        /// </summary>
        /// <remarks>
        /// Not a stillbirth and not a crowded refusal: the genome is fine, the world has room, and
        /// the parent could pay. What it cannot do is put a body that light into the solver, and
        /// every divergence on record was a newborn. A lineage whose investment over its litter
        /// sits under the floor is childless, which is a selection pressure with a column of its
        /// own so that it cannot be mistaken for the world simply being poor.
        /// </remarks>
        public long ConceptionsUnderMassFloor { get; private set; }

        /// <summary>
        /// Floor draws refused because the founder's body would have been under
        /// <see cref="RunConfig.MinNewbornPartKilograms"/> — rule 3 applied to rule 7.
        /// </summary>
        /// <remarks>
        /// Its own counter rather than a share of <see cref="ConceptionsUnderMassFloor"/>, because
        /// the two say different things about the world. A conception refused is a lineage that
        /// cannot breed; a floor draw refused is the founding lottery rejecting a ticket, and it
        /// counts against <see cref="FloorSpawns"/> rather than against any parent. Reading the
        /// two as one number would hide a founder draw that had become mostly refusals behind a
        /// population that was breeding perfectly well.
        /// </remarks>
        public long FoundersUnderMassFloor { get; private set; }

        /// <summary>Mean <see cref="Genome.AdultScale"/> over the living — rule 9. NaN when empty.</summary>
        /// <remarks>
        /// <b>The three dials and the body scale, computed on demand rather than tracked.</b> They
        /// are read once per report row and a running mean would have to be maintained on every
        /// birth, death and growth step — four accumulators kept in step with three events, for a
        /// number nothing in the economy reads. NaN rather than 0 on an empty world: 0 is a real
        /// value for none of these, and a reader will average whatever it is given.
        /// </remarks>
        public float MeanAdultScale => MeanOverLiving(c => c.Genome.AdultScale);

        /// <summary>Mean <see cref="ReproductionTraits.BirthInvestment"/> over the living. NaN when empty.</summary>
        public float MeanBirthInvestment => MeanOverLiving(c => c.Genome.Reproduction.BirthInvestment);

        /// <summary>Mean <see cref="ReproductionTraits.BroodSize"/> over the living. NaN when empty.</summary>
        public float MeanBroodSize => MeanOverLiving(c => c.Genome.Reproduction.BroodSize);

        /// <summary>Mean <see cref="Organism.BodyFraction"/> over the living. NaN when empty.</summary>
        /// <remarks>
        /// How grown the population is, which is the reading rule 9 asks for beside the dials: a
        /// world of adults and a world of perpetual juveniles have the same population count and
        /// the same birth rate, and only this tells them apart.
        /// </remarks>
        public float MeanBodyFraction => MeanOverLiving(c => c.BodyFraction);

        private float MeanOverLiving(Func<Organism, float> of)
        {
            if (_living.Count == 0) return float.NaN;

            double sum = 0.0;
            for (int i = 0; i < _living.Count; i++) sum += of(_living[i]);
            return (float)(sum / _living.Count);
        }

        /// <summary>Simulated seconds since the world began.</summary>
        public double ElapsedSeconds { get; private set; }

        public IReadOnlyList<Organism> Living => _living;

        /// <summary>
        /// Who decides whether a body has room to exist — D077. Null in every tiled world, and in
        /// every test that has no coordinates to offer.
        /// </summary>
        /// <remarks>
        /// Read only when <see cref="RunConfig.SharedSpace"/> is on, so a world that has one
        /// attached and the switch off is the world on file, unchanged. See
        /// <see cref="IBodyPlacement"/> for why the world asks rather than knows.
        /// </remarks>
        public IBodyPlacement Placement { get; set; }

        /// <summary>
        /// Births refused for want of room — D077's crowded stillbirth. 0 unless
        /// <see cref="RunConfig.SharedSpace"/> is on.
        /// </summary>
        /// <remarks>
        /// A separate count from <see cref="Stillbirths"/>, which is a development failure: this
        /// one is a world that is full, and the two would be read for completely different things.
        /// The parent keeps its energy and its matter — nothing was built — so a crowded refusal
        /// is a birth that did not happen rather than one that failed, and no lineage row is
        /// written for it.
        /// </remarks>
        public long CrowdedStillbirths { get; private set; }

        /// <summary>Every species ever founded, oldest first by id — D057. See <see cref="SpeciesFounder"/>.</summary>
        public IReadOnlyDictionary<uint, SpeciesFounder> Species => _species;

        /// <summary>Creatures ever created by the floor, and ever born to a parent — D021.</summary>
        public long FloorSpawns { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }

        /// <summary>
        /// Creatures killed by <see cref="KillDiverged"/> — bodies the solver blew up. Included in
        /// <see cref="Deaths"/>, and 0 for every healthy run.
        /// </summary>
        /// <remarks>
        /// An instrument, not a demography. A run whose <c>diverged</c> column ever leaves 0 has
        /// had a creature removed by arithmetic rather than by selection, and every trait share
        /// computed after it is missing that lineage for a reason nothing ecological explains. It
        /// is reported per run precisely so that "one body in seventeen hundred, once" and "the
        /// physics is unstable at this step" cannot be mistaken for each other.
        /// </remarks>
        public long Diverged { get; private set; }

        /// <summary>
        /// Creatures ever created by <see cref="Inoculate"/> — D060's invasion assay. Zero for the
        /// life of a run that never calls it.
        /// </summary>
        public long Inoculated { get; private set; }

        /// <summary>Simulated seconds since the floor last had to intervene.</summary>
        /// <remarks>
        /// Reported alongside generation depth rather than instead of it. On its own it is nearly
        /// binary; its value is that it dates the moment a world stopped needing us.
        /// </remarks>
        public double SecondsSinceFloorFired { get; private set; }

        /// <summary>Total energy that has entered the world as light, and left as metabolism.</summary>
        /// <remarks>
        /// §5A.2's audit: sun in, metabolism out, everything else conserved. Kept as doubles
        /// because a run accumulates these over millions of steps and a float would stop
        /// registering small additions long before the run ended — which is the failure mode
        /// where an energy audit silently becomes decorative.
        /// </remarks>
        public double EnergyIn { get; private set; }
        public double EnergyOut { get; private set; }

        /// <summary>
        /// Everything the world holds right now: reserves, bodies and detritus, in joules.
        /// </summary>
        /// <remarks>
        /// The middle term of §5A.2's audit, which is a hard equality rather than a plausibility
        /// check: <c>EnergyIn − EnergyOut == Standing</c>, always, to floating-point. Sunlight and
        /// founders are the only sources; metabolism and reproductive overhead the only sinks;
        /// everything else — endowment, tissue, feeding, death — moves energy between the three
        /// accounts below without changing the total. A creature that finds free energy in the
        /// physics or in our arithmetic breaks this and nothing else has to notice it.
        /// </remarks>
        public double StandingJoules
        {
            get
            {
                double sum = Nutrients.TotalJoules;
                for (int i = 0; i < _living.Count; i++)
                {
                    sum += _living[i].Energy + _living[i].TissueJoules;
                }

                // The fourth account, and only ever nonzero where a corpse exists: a body's
                // tissue is standing energy from the death that founds the corpse to the step the
                // last of it is deposited. The loop runs zero times at CorpseDecayPerSecond 0, so
                // this line adds nothing at all to a world on file.
                sum += CorpseJoules;
                return sum;
            }
        }

        /// <summary>How far §5A.2's books are from balancing, in joules. Should be ~0.</summary>
        public double AuditResidual => EnergyIn - EnergyOut - StandingJoules;

        /// <summary>
        /// The living bodies' tissue alone, in joules. <see cref="StandingJoules"/> without the
        /// reserve energy or the detritus account.
        /// </summary>
        /// <remarks>
        /// Read by <see cref="EnforceCeiling"/> against <see cref="RunConfig.MaximumTissueJoules"/>.
        /// Growth made a body count stop measuring biomass (rule 9): a newborn's tissue is a
        /// fraction of its adult target, so this reads what the count ceiling was built to read
        /// and no longer does on its own.
        /// </remarks>
        public double StandingTissueJoules
        {
            get
            {
                double sum = 0d;
                for (int i = 0; i < _living.Count; i++) sum += _living[i].TissueJoules;
                return sum;
            }
        }

        public World(RunConfig config, ulong seed = 1)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));

            if (config.Light == null)
            {
                throw new ArgumentException(
                    "RunConfig.Light is null, so the world has no primary energy input and " +
                    "nothing in it can live.", nameof(config));
            }

            // D061: PatchCount reads Config.HorizontalPatches, so it is valid from this point on
            // (Config was just assigned above) and every field below is built with the same K.
            int patchCount = PatchCount;

            ValidateVent(config, patchCount);
            ValidateMatterInflux(config, patchCount);

            Field = new LightField(
                Light, config.WorldAreaSquareMetres, config.LightLayerMetres,
                patchCount, config.PerPatchShading > 0f);

            if (config.FieldModel == MatterField.Vertices)
            {
                // D083. A vertex is somewhere, so the bodies must be too: the tiled world has no
                // horizontal coordinates for a body to feed at, and defaulting them would put
                // every creature at its patch's centre and call that a position.
                if (!config.SharedSpace)
                {
                    throw new ArgumentException(
                        "FieldModel is Vertices but SharedSpace is false. The vertex field reads " +
                        "a body's position, which only the shared volume has.",
                        nameof(config));
                }

                Nutrients = new VertexField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.NutrientSinkMetresPerSecond, config.WorldDepthMetres,
                    config.FloorRefugeMetres, config.RefugeEdibleFraction, patchCount,
                    config.FieldKernelMetres, config.FieldMergeMetres, config.FieldVertexCap,
                    config.FieldVertexJoules, Rng.SeedFor(seed, DetritusFieldIndex));

                // Its own reach: the cell's volume, so the matter gate binds where it bound in
                // the base world (RunConfig.FieldMatterKernelMetres).
                Matter = new VertexField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.MatterSinkMetresPerSecond, config.WorldDepthMetres,
                    0f, 0f, patchCount,
                    config.FieldMatterKernelMetres, config.FieldMergeMetres, config.FieldVertexCap,
                    config.FieldVertexJoules, Rng.SeedFor(seed, MatterFieldIndex));
            }
            else if (config.FieldModel == MatterField.Grid)
            {
                // fable-propose-grid.md, and D083's reason unchanged: a cell of the grid is a
                // place, so the bodies must have places. The tiled world gives a body a depth and
                // a patch index and nothing else, and defaulting the rest would put every creature
                // at its patch's centre and call that a position.
                if (!config.SharedSpace)
                {
                    throw new ArgumentException(
                        "FieldModel is Grid but SharedSpace is false. The grid field reads a " +
                        "body's position, which only the shared volume has.",
                        nameof(config));
                }

                // D084 said the sideways rate equals the vertical one because the walk is the
                // same in every direction, and a cubic cell has no axis to prefer. The trap this
                // refusal closes is a provenance one rather than a physical one: GridField would
                // accept a horizontal rate of 0 and then stir sideways at its vertical rate
                // anyway, so a run header could record h-mix 0 for a world that mixes sideways at
                // 0.02. Refusing here keeps the header's h-mix token equal to what the detritus
                // actually does on every axis.
                if (config.HorizontalMixingDiffusivity != config.NutrientMixingDiffusivity)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"FieldModel is Grid, but HorizontalMixingDiffusivity is ") +
                        FormattableString.Invariant(
                            $"{config.HorizontalMixingDiffusivity} m2/s against a vertical ") +
                        FormattableString.Invariant(
                            $"NutrientMixingDiffusivity of {config.NutrientMixingDiffusivity} m2/s. ") +
                        "A grid's cells are cubes and D084 ruled the two axes equal, so the two " +
                        "knobs must agree or the run header would name a sideways rate the world " +
                        "does not run.",
                        nameof(config));
                }

                Nutrients = new GridField(
                    config.WorldAreaSquareMetres, config.NutrientSinkMetresPerSecond,
                    config.WorldDepthMetres, config.FloorRefugeMetres, config.RefugeEdibleFraction,
                    patchCount, config.FieldCellMetres);

                // Its own, coarser cell: matter is drawn in whole conceptions rather than grazed,
                // and a metre of water cannot afford a child (RunConfig.FieldMatterCellMetres).
                Matter = new GridField(
                    config.WorldAreaSquareMetres, config.MatterSinkMetresPerSecond,
                    config.WorldDepthMetres, 0f, 0f, patchCount, config.FieldMatterCellMetres);
            }
            else
            {
                Nutrients = new NutrientField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.NutrientSinkMetresPerSecond, config.WorldDepthMetres,
                    config.FloorRefugeMetres, config.RefugeEdibleFraction, patchCount);

                // No refuge: nobody grazes matter, it is drawn at conception rather than eaten — D055.
                Matter = new NutrientField(
                    config.WorldAreaSquareMetres, config.LightLayerMetres,
                    config.MatterSinkMetresPerSecond, config.WorldDepthMetres,
                    refugeMetres: 0f, refugeEdibleFraction: 0f, patchCount: patchCount);
            }

            // Seeded uniformly — across every patch as well as every layer, D061 — and never
            // created again. Everything after this is redistribution: reproduction takes it out
            // of a cell, death puts it back into one. Deposit's 3-arg (patch-explicit) overload
            // is called directly rather than the pre-D061 one, so this loop needs no guard of its
            // own: it is correct at K=1 (one patch, same total deposited as before D061 existed)
            // and at K>1 alike.
            if (Matter is VertexField matterVertices)
            {
                // D083. A lattice holding the same total the cells would, spaced for one quantum
                // per vertex; nothing here when the density is 0, exactly like the cells.
                matterVertices.SeedUniform(config.InitialMatterPerCubicMetre);
            }
            else if (Matter is GridField matterGrid)
            {
                // The same total again, one share per cell. The grid's cells tile the box exactly
                // (GridField refuses a cell size that does not), so this is the cells' own seed
                // read at a finer scale and not an approximation of it.
                matterGrid.SeedUniform(config.InitialMatterPerCubicMetre);
            }
            else if (config.InitialMatterPerCubicMetre > 0f)
            {
                float perCell = config.InitialMatterPerCubicMetre * Matter.LayerVolume;
                for (int i = 0; i < Matter.LayerCount; i++)
                {
                    float depth = -((i + 0.5f) * Matter.LayerMetres);
                    for (int patch = 0; patch < patchCount; patch++)
                    {
                        // The cell field's own signature: this branch is the cells' alone.
                        ((NutrientField)Matter).Deposit(depth, perCell, patch);
                    }
                }
            }

            // D074. The stock the matter identity is measured against, read here because here is
            // the only place it is unambiguous — see MatterInitialTotal. Before D074 this number
            // was StandingMatter for the whole life of the run.
            MatterInitialTotal = Matter.TotalJoules;

            // D067. The vent's legs are defined by a volume flux and need no width, but the drag a
            // creature feels in one is a velocity and does. The field is told once, here, from the
            // same geometry the fields themselves were built with — sqrt(area / K) — so a world
            // cannot end up with two patch widths that disagree.
            // D086's grid and the transport field want the whole box, not only the width: the
            // length is the width times the patch count and the floor is at minus the depth, and
            // the seed is what makes one round's five seeds five draws of the water as well as of
            // the genome. Same geometry the fields were built with, for the same reason.
            config.Current?.SetBox(
                Nutrients.PatchWidthMetres, patchCount, config.WorldDepthMetres,
                Rng.SeedFor(seed, CurrentFieldIndex));

            Seed = seed;

            // Built for every world, drawn from only by a shuffled one — see ConceptionOrderIndex.
            // Constructing an Rng takes no draw from anything else, so an Age world is unchanged
            // by its existence.
            _conceptionRng = new Rng(Rng.SeedFor(seed, ConceptionOrderIndex));

            // Likewise for every world and used by none but a Reserve one — an object, not a draw.
            _byReserve = new ReserveComparer(this);
        }

        /// <summary>
        /// Descending energy surplus, ties by list index ascending — <see cref="ConceptionOrder.Reserve"/>'s
        /// order (D073, logbook/0057).
        /// </summary>
        /// <remarks>
        /// The fallback to the index is not tidiness. It makes the comparison a *total* order over
        /// distinct indices, and a total order has exactly one sorted arrangement whichever sort
        /// produced it — where two equal surpluses left to tie would be arranged by whatever
        /// <see cref="Array.Sort{T}(T[], int, int, IComparer{T})"/>'s introsort happens to do this
        /// runtime. That is the same hazard <see cref="Rng"/> exists for: an algorithm nobody
        /// promised not to change, standing between a seed and a run.
        /// </remarks>
        private sealed class ReserveComparer : IComparer<int>
        {
            private readonly World _world;

            public ReserveComparer(World world) => _world = world;

            public int Compare(int a, int b)
            {
                int bySurplus = _world._conceptionSurplus[b].CompareTo(_world._conceptionSurplus[a]);
                return bySurplus != 0 ? bySurplus : a.CompareTo(b);
            }
        }

        /// <summary>
        /// The three things about a vent that only the world can check — D067.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A <see cref="CurrentField"/> is handed heights and a patch index and nothing else</b>,
        /// so it cannot discover where the floor is, how many patches there are or how thick a
        /// layer is. Each of those is load-bearing for the vent: a plume that stops above the floor
        /// is the trapdoor D067 exists to close, an out-of-range vent patch would silently be some
        /// other patch, and a leg that is not a whole number of layers makes the flux across a face
        /// a fraction of a cell that does not exist. So the config states them and the world refuses
        /// to be built when they disagree, rather than running a different experiment from the one
        /// the file names.
        /// </para>
        /// <para>
        /// <b>All three are skipped entirely while the vent is off</b>, which is every run before
        /// D067: the defaults are then simply unread, and a config that never asked for a vent
        /// cannot be refused because of one.
        /// </para>
        /// </remarks>
        private static void ValidateVent(RunConfig config, int patchCount)
        {
            CurrentField current = config.Current;
            if (current == null || !(current.VentSpeed > 0f)) return;

            if (current.VentPatch >= patchCount)
            {
                throw new ArgumentException(
                    $"Current.VentPatch is {current.VentPatch} but there are only {patchCount} " +
                    "patches, so the plume would rise in a patch this world does not have. It is " +
                    "validated rather than wrapped, because a vent that quietly relocates when " +
                    "HorizontalPatches changes makes two configs name the same world.",
                    nameof(config));
            }

            if (current.VentDepthMetres != config.WorldDepthMetres)
            {
                throw new ArgumentException(
                    $"Current.VentDepthMetres is {current.VentDepthMetres} m and WorldDepthMetres " +
                    $"is {config.WorldDepthMetres} m. The vent draws from the floor and the field " +
                    "cannot see where the floor is, so the two have to be stated to agree — a " +
                    "plume that stops short of the bottom is the trapdoor D067 exists to close.",
                    nameof(config));
            }

            double layers = current.VentLegMetres / (double)config.LightLayerMetres;
            if (!(layers >= 1d) || Math.Abs(layers - Math.Round(layers)) > 1e-6d)
            {
                throw new ArgumentException(
                    $"Current.VentLegMetres is {current.VentLegMetres} m, which is not a whole " +
                    $"number of {config.LightLayerMetres} m layers. Discrete continuity holds only " +
                    "when a leg is made of whole cells: the flux across a face is a fraction of a " +
                    "cell's volume, and a leg that ends halfway through a layer makes that " +
                    "fraction describe nothing. A leg of no layers at all is not a circulation.",
                    nameof(config));
            }
        }

        /// <summary>
        /// Refuses a D074 influx aimed at a vent this world has no room for, before the first step
        /// rather than at the deposit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="MatterInflux.Vent"/> reads <see cref="CurrentField.VentPatch"/> and
        /// <see cref="CurrentField.VentDepthMetres"/> off the same fields D067's plume uses, so a
        /// config with no <see cref="RunConfig.Current"/> at all has nowhere to name, and a patch
        /// index past <c>K</c> would either throw inside the deposit or — worse, if anything ever
        /// wrapped it — put the world's whole matter income in a patch nobody asked for.
        /// <see cref="ValidateVent"/>'s own patch check does not cover this: it returns early
        /// while the plume is off, and an influx at the vent's coordinates is a perfectly sensible
        /// world with no plume in it (a cold seep).
        /// </para>
        /// <para>
        /// Nothing is checked while the influx is 0 or lands at the surface, which is every run
        /// before D074: a world that never asked for a vent influx cannot be refused because of
        /// one, the same rule <see cref="ValidateVent"/> follows.
        /// </para>
        /// </remarks>
        private static void ValidateMatterInflux(RunConfig config, int patchCount)
        {
            if (!(config.MatterInfluxPerSecond > 0f)) return;
            if (config.MatterInfluxAt != MatterInflux.Vent) return;

            CurrentField current = config.Current;
            if (current == null)
            {
                throw new ArgumentException(
                    "MatterInfluxAt is Vent and MatterInfluxPerSecond is " +
                    $"{config.MatterInfluxPerSecond}, but RunConfig.Current is null, so there is " +
                    "no vent patch or vent depth to deposit at. The influx borrows D067's " +
                    "coordinates rather than carrying its own.",
                    nameof(config));
            }

            if (current.VentPatch >= patchCount)
            {
                throw new ArgumentException(
                    $"MatterInfluxAt is Vent and Current.VentPatch is {current.VentPatch}, but " +
                    $"there are only {patchCount} patches. The world's entire matter income would " +
                    "be deposited in a patch this world does not have.",
                    nameof(config));
            }
        }

        /// <summary>
        /// Reports where a creature is and what it spent moving — DESIGN.md §5A.2, §10 M4.
        /// </summary>
        /// <param name="creature">A living organism of this world.</param>
        /// <param name="heightY">Height of its centre of mass, metres. Y is up.</param>
        /// <param name="workJoules">
        /// Mechanical work done at its joints since the last call. Accumulated, not replaced —
        /// physics steps many times per metabolic step.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>This is the entire seam between the physics and the economy, and it points one
        /// way.</b> §6.1 forbids <c>UnityEngine</c> in this assembly, so the world cannot reach
        /// into PhysX to ask where anything is; the simulator pushes both measurements in and the
        /// world never knows a solver exists. The same world runs with nothing calling this, which
        /// is what every calibration in §5A.2b was measured against — a population that cannot
        /// move and for which swimming is free.
        /// </para>
        /// <para>
        /// <b>Work is added rather than assigned</b> because the two clocks differ: physics
        /// integrates at 0.01 s and the economy steps far more slowly, so one metabolic step is
        /// the sum of many strokes. <c>Metabolise</c> drains it.
        /// </para>
        /// <para>
        /// Negative work is refused. <see cref="EffectorDriver"/> reports the unsigned integral
        /// precisely because a joint driven <i>by</i> the water is doing negative work at the
        /// actuator, and billing that as income would be a free-energy source of exactly the kind
        /// §11.2 exists to catch — the creature would evolve to be pushed around.
        /// </para>
        /// </remarks>
        public void Observe(Organism creature, float heightY, float workJoules)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            if (!HeightIsInTheWorld(heightY, Config.WorldDepthMetres))
            {
                string what = float.IsNaN(heightY) || float.IsInfinity(heightY)
                    ? "a non-finite height"
                    : $"a height of {heightY:g4} m in a world {Config.WorldDepthMetres:0.#} m deep";
                throw new ArgumentOutOfRangeException(
                    nameof(heightY), heightY,
                    $"Creature {creature.Id} has {what}, so the solver has already diverged " +
                    "and every income derived from depth would be meaningless.");
            }

            if (workJoules < 0f || float.IsNaN(workJoules))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workJoules), workJoules,
                    "Mechanical work must be unsigned. A negative cost is an income, and an " +
                    "income for being moved by the water is a free-energy source (§11.2).");
            }

            creature.HeightY = heightY;
            creature.PendingWorkJoules += workJoules;
        }

        /// <summary>
        /// Whether a height is one the sea could hold, or one only a diverged solver produces —
        /// the bound <see cref="Observe(Organism, float, float)"/> refuses and the harness's
        /// divergence check kills at.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Finite is not enough.</b> <c>r31-s3</c> ended at 11,533.5 s with an
        /// <c>ArgumentOutOfRangeException</c> out of <see cref="LightField.Contribute"/>: a root
        /// the solver had thrown to a finite height so large that the layer index
        /// <c>(int)(-heightY / LayerMetres)</c> overflowed passed the non-finite test here and in
        /// the harness, and took the arm down where a NaN body would have been dumped and killed
        /// as a counted death (logbook/0077). The bound is the world's box with room: above, the
        /// world's depth past the surface — D050 stops upward net force at y = 0, so a body above
        /// it is coasting on momentum and never gets far; below, twice the depth past the floor,
        /// which has been a collider since D077's bed (round 5b's sinkers at −131 m in a 60 m
        /// world, logbook/0040, are inside it). A body outside that box is not somewhere in the
        /// sea, and a body that tunnelled through the bed and kept sinking is a solver fault too.
        /// </para>
        /// <para>NaN fails both comparisons, so the non-finite case is inside this one.</para>
        /// </remarks>
        public static bool HeightIsInTheWorld(float heightY, float worldDepthMetres) =>
            heightY <= worldDepthMetres && heightY >= -3f * worldDepthMetres;

        /// <summary>
        /// <see cref="Observe(Organism, float, float)"/> with the whole centre of mass — D083.
        /// The vertex field feeds a body where it is; the cell field reads only the height.
        /// </summary>
        public void Observe(Organism creature, Float3 centre, float workJoules)
        {
            Observe(creature, centre.Y, workJoules);

            if (float.IsNaN(centre.X) || float.IsInfinity(centre.X) ||
                float.IsNaN(centre.Z) || float.IsInfinity(centre.Z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(centre), centre,
                    $"Creature {creature.Id} has a non-finite horizontal position, so the solver " +
                    "has already diverged.");
            }

            creature.X = centre.X;
            creature.Z = centre.Z;
        }

        /// <summary>Advances the world by one step.</summary>
        /// <param name="seconds">Step length. Large steps are fine — this is not a solver.</param>
        public void Step(float seconds)
        {
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));

            ElapsedSeconds += seconds;
            SecondsSinceFloorFired += seconds;

            // D077. Before anything reads a patch — before the light is advanced, before feeding,
            // before conception — because in a shared volume a patch is a region and a creature is
            // in whichever one its body is in. Nothing is drawn from the RNG stream here: this
            // replaces two lotteries (Disperse, AdvectBodies) with a read of where things already
            // are, which is the whole of D077's second rule.
            ReadPatchesFromPositions();

            // Before anything reads the light, and from the absolute clock rather than a delta —
            // a sun advanced by accumulating steps drifts out of phase with the world that is
            // paying for it, and would present as a slow trend nobody chose (§5A.4).
            Field.Advance(ElapsedSeconds);

            Metabolise(seconds);

            // fable-propose-growth.md rule 5. After feeding and upkeep, so a body invests what
            // this step actually left it; before Reproduce, so growth has first claim on the
            // reserve and a creature below its adult size almost never clears the breeding gate.
            // Before the fields' own transport passes for the reason feeding is: what a body draws
            // this step comes out of the water as it stood when the step began.
            Grow();

            // D061. After Metabolise, so this step's feeding and shading were priced at each
            // creature's patch as it stood when the step began; before Reproduce, so an
            // offspring inherits the patch its parent ends this step in rather than the one it
            // started it in. Skips its own RNG draw entirely when there is nowhere to disperse to
            // or nothing asks for it — see Disperse's own remarks for the K=1 bit-identity guard.
            Disperse();

            // D066. Beside Disperse and under the same rules: after Metabolise so this step was
            // priced where the creature stood, before Reproduce so an offspring is born in the
            // patch its parent ends the step in. Draws nothing at all when the rolls are off.
            AdvectBodies(seconds);

            // D074. Before the field settles, so a unit deposited at the surface starts sinking on
            // the step it arrives rather than a step later — and, with burial after the whole
            // transport pass below, so nothing that arrives at the surface can be buried in the
            // same step it entered the world.
            DepositMatterInflux(seconds);

            // Rule 6 of fable-propose-grid.md. Before the fields' own passes, so a joule a corpse
            // hands over this step sinks, mixes and drifts on the same step it arrives. That
            // is the rule DepositMatterInflux is placed by above, and the rule a death has
            // always obeyed, since Metabolise runs before all of this. Returns before touching
            // anything when there is no corpse, which is every world at CorpseDecayPerSecond 0.
            StepCorpses(seconds);

            Nutrients.Settle(seconds);
            Matter.Settle(seconds);

            // The floor's only outflow besides a creature resident there eating it (D051): first-
            // order decay back into the layer above, before that layer is stirred.
            Nutrients.Remineralise(seconds, Config.NutrientRemineralisationPerSecond);
            Matter.Remineralise(seconds, Config.MatterRemineralisationPerSecond);

            // Stirred after it sinks, in the same step. The two are opposed — one carries detritus
            // down and the other spreads it back through the column — and whether the world has a
            // nutrient gradient or a line on the floor is the balance between them (D036). D061
            // adds a horizontal pass alongside the vertical one, throttled by its own knob — see
            // NutrientField.Mix's remarks for why it is a separate, far slower rate.
            Nutrients.Mix(seconds, Config.NutrientMixingDiffusivity, Config.HorizontalMixingDiffusivity);

            // The matter grid stirs at its own rate on every axis. HorizontalMixingDiffusivity is
            // one knob for two substances, and it belongs to the detritus: it is what the run
            // header's h-mix token names, and World's Grid branch refuses a world where it
            // disagrees with the vertical detritus rate. A cube has no preferred axis, so the
            // matter field's sideways rate is its own vertical one, and that number is stated in
            // RunConfig.MatterMixingDiffusivity rather than in a second knob nobody sets. The cell
            // and vertex fields keep the shared knob exactly as they always had it.
            Matter.Mix(
                seconds, Config.MatterMixingDiffusivity,
                Matter is GridField ? Config.MatterMixingDiffusivity : Config.HorizontalMixingDiffusivity);

            // D066. Carried after it is stirred, in the same step and against the same clock the
            // bodies feel — diffusion is now the residual and advection the transport. A no-op
            // unless CurrentField.AdvectFields is on, so every run before D066 is untouched. The
            // patch width is the field's own, sqrt(area / K), which is what the horizontal Mix
            // pass above already diffuses across: one geometry, not two.
            Nutrients.Advect(Config.Current, ElapsedSeconds, seconds, Nutrients.PatchWidthMetres);
            Matter.Advect(Config.Current, ElapsedSeconds, seconds, Matter.PatchWidthMetres);

            // D074. After everything that moves matter within the world, so what the floor holds
            // when burial is charged is what settling, mixing and advection actually left there —
            // and after the influx above, so the deposit is not buried on arrival.
            BuryMatter(seconds);

            // D083. After every pass that moves or removes a vertex, so what a body reads next
            // step is a field whose drifted-together vertices are one and whose count fits its
            // budget. A cell field has nothing to do here.
            Nutrients.Cull();
            Matter.Cull();

            Reproduce();
            EnforceFloor();
            EnforceCeiling();
        }

        /// <summary>
        /// D074's influx: one step's worth of free matter into the world, at
        /// <see cref="RunConfig.MatterInfluxAt"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returns before touching anything at an influx of 0</b>, which is every run before
        /// D074 — no draw, no deposit, no counter moved, so those worlds are bit-identical. The
        /// same shape D052's excretion and D070's exudation are written in.
        /// </para>
        /// <para>
        /// <b><see cref="MatterInflux.Surface"/> is a total, not a total per patch.</b> The knob
        /// says what the world receives, so K patches share one deposit rather than each getting
        /// one — otherwise raising <c>HorizontalPatches</c> would silently raise the world's
        /// matter income, and D061's patch count is meant to divide a world rather than multiply
        /// it.
        /// </para>
        /// <para>
        /// <b><see cref="MatterInflux.Vent"/> reads D067's coordinates rather than carrying its
        /// own.</b> A second pair of "where is the vent" fields is a second thing to keep in
        /// agreement with <see cref="RunConfig.WorldDepthMetres"/>, and
        /// <see cref="ValidateMatterInflux"/> has already refused the world in which those
        /// coordinates name nothing.
        /// </para>
        /// </remarks>
        private void DepositMatterInflux(float seconds)
        {
            float rate = Config.MatterInfluxPerSecond;
            if (!(rate > 0f)) return;

            double amount = (double)rate * seconds;

            // D083. Vertices are founded rather than joules deposited: whole quanta, at random
            // positions inside the plume's bottom layer or along the surface, from the field's
            // own stream. What is counted is what was emitted; the remainder waits in the
            // field's bank and is not yet in the world.
            if (Matter is VertexField vertices)
            {
                float width = Matter.PatchWidthMetres;
                float half = 0.5f * Matter.LayerMetres;
                float emitted;

                if (Config.MatterInfluxAt == MatterInflux.Vent)
                {
                    CurrentField plume = Config.Current;
                    emitted = vertices.Emit(
                        amount,
                        new Float3((plume.VentPatch + 0.5f) * width, -plume.VentDepthMetres + half, 0.5f * width),
                        new Float3(0.5f * width, half, 0.5f * width));
                }
                else
                {
                    float length = width * PatchCount;
                    emitted = vertices.Emit(
                        amount,
                        new Float3(0.5f * length, 0f, 0.5f * width),
                        new Float3(0.5f * length, 0f, 0.5f * width));
                }

                MatterInfluxedTotal += emitted;
                return;
            }

            // The same two boxes the vertex branch emits into, landing on the cells they cover
            // rather than founding quanta inside them. Every joule handed in is in the world on
            // the step it arrives, so what is counted is exactly what was added. The surface case
            // spreads over the whole top layer rather than one column per patch: the vertex field
            // scatters its quanta across the whole surface box, and a grid that dropped the same
            // influx into K centre cells would be a different world wearing the same knob.
            if (Matter is GridField grid)
            {
                float gridWidth = Matter.PatchWidthMetres;
                float gridHalf = 0.5f * Matter.LayerMetres;

                if (Config.MatterInfluxAt == MatterInflux.Vent)
                {
                    CurrentField plume = Config.Current;
                    MatterInfluxedTotal += grid.DepositBox(
                        amount,
                        new Float3((plume.VentPatch + 0.5f) * gridWidth, -plume.VentDepthMetres + gridHalf, 0.5f * gridWidth),
                        new Float3(0.5f * gridWidth, gridHalf, 0.5f * gridWidth));
                    return;
                }

                float gridLength = gridWidth * PatchCount;
                MatterInfluxedTotal += grid.DepositBox(
                    amount,
                    new Float3(0.5f * gridLength, -gridHalf, 0.5f * gridWidth),
                    new Float3(0.5f * gridLength, gridHalf, 0.5f * gridWidth));
                return;
            }

            if (Config.MatterInfluxAt == MatterInflux.Vent)
            {
                CurrentField vent = Config.Current;
                float all = (float)amount;
                if (!(all > 0f)) return;

                ((NutrientField)Matter).Deposit(-vent.VentDepthMetres, all, vent.VentPatch);
                MatterInfluxedTotal += all;
                return;
            }

            int patches = PatchCount;

            // The float the field is actually handed, not the double it was derived from: what is
            // counted has to be what was deposited, or the identity in MatterInfluxedTotal's
            // remarks drifts by a rounding per step and the audit stops being able to catch a real
            // fault. Deposit's 3-arg overload for D061's reason — correct at K=1 and at K>1 alike.
            float per = (float)(amount / patches);
            if (!(per > 0f)) return;

            // Through the interface, so the cells and the grid share this line: both put one
            // patch's share into that patch at the surface, the grid into the patch's centre
            // column. The same call the cast used to make, so the cell worlds on file are
            // untouched.
            for (int patch = 0; patch < patches; patch++) Matter.Deposit(0f, per, patch);

            MatterInfluxedTotal += (double)per * patches;
        }

        /// <summary>
        /// Carries every corpse one step and takes its decay out of it. Rule 6 of
        /// <c>fable-propose-grid.md</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returns before touching anything when there is no corpse</b>, which is every world
        /// at <see cref="RunConfig.CorpseDecayPerSecond"/> 0: nothing is founded there, so the
        /// list is empty and this method is a length check. The same shape D052's excretion,
        /// D070's exudation and D074's influx are written in, and the reason the cell, vertex and
        /// grid worlds on file replay bit for bit.
        /// </para>
        /// <para>
        /// <b>It moves like the water and not like a body.</b> A corpse sinks at the detritus
        /// field's own <see cref="IMatterField.SinkMetresPerSecond"/> and rides
        /// <see cref="RunConfig.Current"/> under exactly the condition
        /// <see cref="VertexField.Advect"/> uses: a current that exists and has
        /// <see cref="CurrentField.AdvectFields"/> on. It rides because it is detritus that has
        /// not dissolved yet, and detritus that drifted differently from the water around it
        /// would be a second transport model nobody chose. It wraps on x and z round D077's
        /// rings and clamps to the surface and the floor, which is what a vertex does.
        /// </para>
        /// <para>
        /// <b>The horizontal leg needs a world with positions.</b> A tiled world never calls
        /// <see cref="Observe(Organism, Float3, float)"/>, so a body's x and z stay wherever
        /// <c>Admit</c> put them and nothing in that world reads them. Drifting a corpse across
        /// them there would move it between patches on the strength of a coordinate the world
        /// does not use, so a tiled world's corpse keeps the patch its body died in and only
        /// sinks. The grid and the vertex fields both refuse a tiled world anyway, so this is
        /// only ever a cell world someone turned the knob on in.
        /// </para>
        /// <para>
        /// <b>Decay is a fraction of what is left, so the knob is a half-life</b>
        /// (<c>ln 2 / rate</c>: 0.005/s is 139 s). Clamped at 1 for a step long enough to ask for
        /// more than the corpse holds, the same clamp <see cref="BuryMatter"/> makes. What is
        /// handed over is a float, and the corpse's own account is reduced by exactly that float,
        /// so the transfer is exact in both books rather than exact in one and rounded in the
        /// other.
        /// </para>
        /// <para>
        /// <b>The last crumb is deposited rather than left to halve for ever.</b> Below 1e-6 J and
        /// 1e-6 matter the remainder goes in whole and the corpse is removed. Both stocks have to
        /// be under, since one of them can empty a step before the other. Without a floor a
        /// corpse would live for the length of the run, and the standing account would carry
        /// thousands of objects holding nothing.
        /// </para>
        /// <para>
        /// <b><see cref="DetritusDepositedTotal"/> counts the instalments, not the death.</b> Its
        /// own identity is <c>deposited + exuded − taken == Nutrients.TotalJoules</c>, so counting
        /// a whole body at the death would credit the water with joules the corpse still holds
        /// and break it for as long as the corpse lasted.
        /// </para>
        /// </remarks>
        private void StepCorpses(float seconds)
        {
            if (_corpses.Count == 0) return;

            float sink = Nutrients.SinkMetresPerSecond * seconds;
            CurrentField current = Config.Current;
            bool drifts = Config.SharedSpace && current != null && current.AdvectFields;

            float length = Nutrients.PatchWidthMetres * PatchCount;
            float width = Nutrients.PatchWidthMetres;
            float depth = Config.WorldDepthMetres;

            double fraction = (double)Config.CorpseDecayPerSecond * seconds;
            if (fraction > 1.0) fraction = 1.0;

            int kept = 0;
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse corpse = _corpses[i];
                corpse.AgeSeconds += seconds;

                Float3 p = corpse.Position;
                float x = p.X;
                float y = p.Y - sink;
                float z = p.Z;

                if (drifts)
                {
                    // A corpse carries a position, so under CurrentMode.Transport it rides the
                    // water where it actually is rather than the water at its patch's centre.
                    // The rolls take the patch, which is all that field is a function of, so
                    // every run in the record drifts on the same arithmetic it always did.
                    Float3 v = current.Mode == CurrentMode.Transport
                        ? current.VelocityAt(p.X, p.Y, p.Z, ElapsedSeconds)
                        : current.VelocityAt(p.Y, ElapsedSeconds, corpse.Patch, PatchCount);
                    x = WrapAxis(x + v.X * seconds, length);
                    y += v.Y * seconds;
                    z = WrapAxis(z + v.Z * seconds, width);
                }

                if (y > 0f) y = 0f;
                else if (y < -depth) y = -depth;

                corpse.Position = new Float3(x, y, z);
                if (drifts) corpse.Patch = PatchOfX(x, width);

                // The last instalment: everything that is left, in one go. Both stocks have to be
                // under the floor value, because one empties before the other. A body that
                // excreted its whole price away (D052) dies with matter at 0 and tissue at 100 J,
                // and a corpse dropped on the strength of the empty half would take the full half
                // with it. A step long enough to ask for the whole remainder is the same case.
                bool last = fraction >= 1.0 ||
                    (corpse.Joules < 1e-6 && corpse.Matter < 1e-6);

                FieldPoint at = corpse.Point;

                if (corpse.Joules > 0d)
                {
                    float given = (float)(last ? corpse.Joules : corpse.Joules * fraction);
                    if (given > 0f)
                    {
                        Nutrients.Deposit(at, given);
                        corpse.Joules -= given;
                        DetritusDepositedTotal += given;
                    }
                }

                if (corpse.Matter > 0d)
                {
                    float given = (float)(last ? corpse.Matter : corpse.Matter * fraction);
                    if (given > 0f)
                    {
                        Matter.Deposit(at, given);
                        corpse.Matter -= given;
                    }
                }

                if (last)
                {
                    // The corpse leaves. A field takes a float and the corpse's own account is a
                    // double, so the last instalment can leave half an ulp of what it carried
                    // behind, either way. At the floor value that is under 1e-13, and at a step
                    // long enough to empty a full corpse it is half an ulp of the body's own
                    // tissue. It is released rather than carried by an object that would otherwise
                    // halve for ever. This is the float quantisation every deposit in the world
                    // already has; naming it is the point, since an audit read to 1e-6 of the sun's
                    // whole input cannot see it and an unnamed rounding is how a leak hides.
                    corpse.Joules = 0d;
                    corpse.Matter = 0d;
                    continue;
                }

                _corpses[kept++] = corpse;
            }

            if (kept < _corpses.Count) _corpses.RemoveRange(kept, _corpses.Count - kept);
        }

        /// <summary>The ring's patch for a world x: D077's rule, <c>floor(x / W) mod K</c>.</summary>
        private int PatchOfX(float x, float patchWidthMetres)
        {
            int patch = (int)Math.Floor(x / patchWidthMetres);
            patch %= PatchCount;
            if (patch < 0) patch += PatchCount;
            return patch;
        }

        /// <summary>A coordinate folded back onto a ring of the given extent. <see cref="GridField"/>'s own.</summary>
        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * (float)Math.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        /// <summary>
        /// D074's burial: a fraction of every patch's floor-layer free matter, out of the world
        /// for good.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Free matter on the floor and nothing else.</b> Not detritus — that is
        /// <see cref="Nutrients"/>, a different substance with its own floor term in
        /// <c>Remineralise</c> — and not <see cref="MatterInBodies"/>, because burying the matter
        /// locked in a living creature would take it out of a body that is still standing on it,
        /// and the identity would have no way to describe what happened.
        /// </para>
        /// <para>
        /// <b>Counted as the field's own before-and-after</b>, not as the draw's own return.
        /// The cell field's <c>Take</c> subtracted a double and handed back a float copy of it, so
        /// a floor whose stock is smaller than the request would lose slightly more than the
        /// counter recorded — a rounding, and exactly the kind of rounding an identity asserted
        /// "to the rounding" stops being able to distinguish from a leak. The draw now goes
        /// through <see cref="IMatterField.TakeFromLayer"/> so that a third representation needs
        /// no branch here; the float <c>wanted</c> and the before-and-after count are unchanged,
        /// which is what keeps every cell world on file replaying bit for bit.
        /// </para>
        /// <para>
        /// The fraction is clamped at 1 for a step long enough to ask for more than the floor
        /// holds; <c>Take</c> caps at the stock anyway, but a clamp says what a whole-floor step
        /// means rather than leaving it to the cap.
        /// </para>
        /// </remarks>
        private void BuryMatter(float seconds)
        {
            float rate = Config.MatterBurialPerSecond;
            if (!(rate > 0f)) return;

            double fraction = (double)rate * seconds;
            if (fraction > 1d) fraction = 1d;
            if (!(fraction > 0d)) return;

            // D083. Each vertex resting on the floor leaves whole with this probability: the
            // same expected rate as the cells' fraction, one vertex at a time.
            if (Matter is VertexField vertices)
            {
                MatterBuriedTotal += vertices.BuryFloor(fraction);
                return;
            }

            int floor = Matter.LayerCount - 1;

            for (int patch = 0; patch < PatchCount; patch++)
            {
                double before = Matter.StockInLayer(floor, patch);
                if (!(before > 0d)) continue;

                float wanted = (float)(before * fraction);
                if (!(wanted > 0f)) continue;

                Matter.TakeFromLayer(floor, patch, wanted);
                MatterBuriedTotal += before - Matter.StockInLayer(floor, patch);
            }
        }

        /// <remarks>
        /// §5A.7's photosynthetic mat, caught rather than culled — D021. Every step past the
        /// ceiling costs more than the last, so a loop that only noticed would still be a loop
        /// that never returned; and culling to fit a budget would be selection performed by us,
        /// hiding a calibration failure behind a population number we chose.
        /// <para>
        /// Two ceilings, both checked every step. <see cref="RunConfig.MaximumPopulation"/> is the
        /// original one; <see cref="RunConfig.MaximumTissueJoules"/> is rule 9's addition,
        /// because growth broke the count as a biomass proxy. A newborn is grown from a fraction
        /// of its adult body, so a world of 5,000 newborns can hold a third of the tissue a world
        /// of 5,000 adults did. The tissue ceiling defaults to 0, off, so a config written before
        /// this rule still describes the world it ran, and at 0 the tissue sum below is skipped
        /// rather than taken and compared against zero every step.
        /// </para>
        /// </remarks>
        private void EnforceCeiling()
        {
            bool overPopulation = _living.Count > Config.MaximumPopulation;

            // Summed only when the tissue ceiling is on, or the population ceiling has already
            // fired and the run is ending anyway. Every config on file before rule 9 has this
            // off, and this runs once a step: summing every living body's tissue on top of the
            // count check would be a real cost across thousands of creatures for an instrument
            // almost nothing reads.
            bool tissueCeilingOn = Config.MaximumTissueJoules > 0d;
            double tissueJoules = tissueCeilingOn || overPopulation ? StandingTissueJoules : 0d;
            bool overTissue = tissueCeilingOn && tissueJoules > Config.MaximumTissueJoules;

            if (!overPopulation && !overTissue) return;

            string message = overPopulation
                ? FormattableString.Invariant($"Population reached {_living.Count}, above the ceiling of ") +
                  FormattableString.Invariant(
                      $"{Config.MaximumPopulation}, at t={ElapsedSeconds:0.#} s after {Births} births. ") +
                  "This is §5A.7's photosynthetic mat: light is covering upkeep, so nothing has to " +
                  "do anything and every creature can afford to breed. The ratio in §5A.2 is too " +
                  "generous — lower the surface irradiance or raise cell upkeep. It is not culled, " +
                  "because culling to fit a compute budget is selection performed by us and would " +
                  "hide this behind a population number we chose."
                : FormattableString.Invariant($"Standing tissue reached {tissueJoules:0} J, above the ") +
                  FormattableString.Invariant(
                      $"ceiling of {Config.MaximumTissueJoules:0} J, at t={ElapsedSeconds:0.#} s after ") +
                  FormattableString.Invariant($"{Births} births, with {_living.Count} bodies alive. ") +
                  "This is the same photosynthetic mat §5A.7 names, read by biomass rather than by " +
                  "count. Growth means a body count alone can miss it: a world of small, fast-growing " +
                  "bodies can stay under the population ceiling while still holding more tissue than " +
                  "the light or matter budget was calibrated for. It is not culled, for the same " +
                  "reason the count ceiling is not.";

            throw new PopulationRunawayException(
                message, _living.Count, tissueJoules, ElapsedSeconds,
                overPopulation ? "population" : "tissue");
        }

        /// <remarks>
        /// <para>
        /// <b>Three passes, because both resources are finite and shared</b> (§5A.2b, §5A.2c).
        /// Every creature's shadow must be known before anyone's income can be, and every
        /// creature's appetite before anyone is fed. A single pass would give whoever the list
        /// happened to walk first the undiminished sun and an unemptied larder, making income
        /// depend on iteration order — the kind of fault that produces a perfectly plausible
        /// number.
        /// </para>
        /// <para>
        /// The appetite pass costs a second evaluation of the metabolic step per creature, since
        /// what a body would take is exactly what <see cref="Metabolism"/> says it takes at the
        /// unrationed density. Estimating it more cheaply would mean a second expression of the
        /// same quantity, and two expressions of one quantity is how they come to disagree.
        /// </para>
        /// </remarks>
        private void Metabolise(float seconds)
        {
            Field.Clear();
            Nutrients.ClearDemand();

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                Field.Contribute(creature.HeightY, creature.Phenotype.TotalLitArea, creature.Patch);
            }
            Field.Solve();

            // Appetite. Priced at the full local density, so this is what each creature would eat
            // if it were alone — which is the quantity a proportional share has to be taken of.
            // Kept, because it is also the answer whenever the larder turns out to be full.
            while (_ledgers.Count < _living.Count) _ledgers.Add(default);

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                float density = Nutrients.EdibleDensityAt(creature.Point);

                EnergyLedger ledger = Metabolism.StepAt(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch),
                    density, creature.PendingWorkJoules, seconds, creature.Age);

                // The absorptive log's capture, taken where the number is — one field write, on
                // the pass that already read it, and only for the creatures the file records
                // (AbsorptiveSample). It has to be here rather than at report time: the field is
                // emptied by Take, settled, mixed and advected between this instant and the next
                // sample, so asking again later would produce a plausible density that is not the
                // one this creature was priced against. The rationed branch below overwrites it
                // with what it actually re-priced.
                if (creature.HasAbsorptiveTissue) creature.LastDensityHere = density;

                _ledgers[i] = ledger;
                Nutrients.Demand(creature.Point, ledger.PoolDrawn);
            }

            // Every share and every rationed price in the pass below is taken from what the
            // cells hold now, not from what earlier meals in the same walk leave behind.
            Nutrients.FreezeAvailability();

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];

                // Read before it advances, because the recompute below has to price the same
                // step the loop above priced. Ageing a creature mid-step would make the short-
                // larder branch a different creature from the full-larder one.
                float age = creature.Age;
                creature.Age += seconds;

                float share = Nutrients.ShareAt(creature.Point);
                EnergyLedger ledger = _ledgers[i];

                // Recomputed only when the larder is short. Scaling the stored ledger instead
                // would assume intake is linear in density, which it is for a filter feeder and
                // is not for anything with a bite rate that saturates.
                if (share < 1f)
                {
                    float rationed =
                        Nutrients.FrozenEdibleDensityAt(creature.Point) * share;

                    // The same work, not more: this replaces the ledger rather than adding to it.
                    ledger = Metabolism.StepAt(
                        creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch),
                        rationed, creature.PendingWorkJoules, seconds, age);

                    // A share is a fraction of the demand, and scaling the density delivers
                    // exactly that only for an intake linear in density. A saturating mouth
                    // (the satiation cap, D062) re-saturates at the rationed density and would
                    // draw its whole demand whatever its share, so the cell would be overdrawn
                    // and the last feeders' takes would come up short. The allowance is the
                    // bound the demand pass promised, and no feeder draws past it.
                    float allowance = share * _ledgers[i].PoolDrawn;
                    if (ledger.PoolDrawn > allowance) ledger = ledger.WithPoolDrawn(allowance);

                    // What the world actually fed it, replacing the appetite pass's unrationed
                    // reading. Already share-multiplied — AbsorptiveSample.DensityHere says so,
                    // because a reader that multiplied again would halve a scarce world twice.
                    if (creature.HasAbsorptiveTissue) creature.LastDensityHere = rationed;
                }

                if (ledger.PoolDrawn > 0f)
                {
                    float taken = Nutrients.Take(creature.Point, ledger.PoolDrawn);
                    DetritusTakenTotal += taken;

                    // Credited what it got. Take is a partial-take API and returns what was
                    // there; a ledger that kept its planned income when the water gave less
                    // was creating energy (the Astra review's R1). Unreachable while the
                    // allowance above holds, and counted so that a hole shows rather than hides.
                    if (taken < ledger.PoolDrawn)
                    {
                        PoolShortTakes++;
                        ledger = ledger.WithPoolDrawn(taken);
                    }
                }

                if (creature.HasAbsorptiveTissue)
                {
                    creature.LastShare = share;
                    creature.LastLedger = ledger;
                    creature.LastStepSeconds = seconds;
                }

                // Drained here and nowhere else. Both branches above priced the same joules, so
                // this is the one point at which they stop being owed.
                creature.PendingWorkJoules = 0f;

                // Refreshed from the ledger rather than left at its birth value, because under
                // senescence the cost of doing nothing is not a property of the body alone
                // (D038). Free: upkeep and neural are exactly what StandingWatts recomputes, and
                // they were just computed. Without this a creature's SecondsOfReserve — and
                // §4.4's Energy sensor, when it exists — would grow more optimistic the closer it
                // came to starving.
                creature.StandingWatts = (ledger.Upkeep + ledger.Neural) / seconds;

                creature.Energy += ledger.Net;
                creature.Lifetime += ledger;

                // D070. What the body released to the water this step, put where the body is.
                // Net already carried the deduction, so this is the other half of a transfer that
                // is complete only once the field holds it — and it happens *before* the death
                // check below, so a creature that exudes and then starves in the same step gives
                // the water both: this step's release, and its tissue.
                if (ledger.Exuded > 0f)
                {
                    Nutrients.Deposit(creature.Point, ledger.Exuded);
                    DetritusExudedTotal += ledger.Exuded;
                }

                // Only sunlight is new energy. What was eaten was already in the world — and what
                // was torn up and not eaten has left it, which is why a food chain shortens.
                EnergyIn += ledger.LightIncome;
                EnergyOut += ledger.Expenditure + ledger.Wasted;

                // Turnover — D052. A living body gives back a fraction of what it holds, in
                // proportion to what it spent staying alive this step, at its own depth rather
                // than only at death. Capped at what is still locked: a body cannot excrete
                // matter it does not have. A floor founder starts at 0 and holds only what its
                // own growth has since bought (rule 5), so the cap alone still keeps a body from
                // excreting matter it never held.
                if (Config.ExcretionPerJoule > 0f && creature.LockedMatter > 0f)
                {
                    // D065 (amended): the fixed matter cost is machinery mass and leaves only
                    // with the body. Excretion drains the tissue share alone; death deposits the
                    // rest. At MatterPerCreature = 0 this is exactly the old expression.
                    float excretable = Math.Max(0f, creature.LockedMatter - Config.MatterPerCreature);
                    float excreted = Math.Min(
                        excretable, Config.ExcretionPerJoule * ledger.Upkeep);

                    if (excreted > 0f)
                    {
                        Matter.Deposit(creature.Point, excreted);
                        creature.LockedMatter -= excreted;
                        MatterInBodies -= excreted;
                        ExcretedTotal += excreted;
                    }
                }

                if (creature.Energy > 0f) continue;

                // §5A.6 kills at exactly zero energy and nothing else — the ecology has one cause
                // of death, and senescence (D038) raises upkeep until this fires sooner rather
                // than opening a second way to die. DeathCause.Diverged is not a second way
                // either: it is the solver failing, and it enters through KillDiverged below.
                Bury(creature, i, DeathCause.Starved);
            }
        }

        /// <summary>
        /// One step of growth for every body below its adult size — fable-propose-growth.md
        /// rule 5 (2026-09-08).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Growth is a transfer, and both books close because it is only ever a transfer.</b>
        /// Energy moves out of <see cref="Organism.Energy"/> and into
        /// <see cref="Organism.TissueJoules"/>, and §5A.2's audit sums both, so nothing is created
        /// or destroyed and <see cref="AuditResidual"/> never learns that growth happened. Matter
        /// moves out of <see cref="Matter"/> and into <see cref="Organism.LockedMatter"/> with
        /// <see cref="MatterInBodies"/> credited in the same breath, which is exactly what
        /// conception already does, so D074's identity closes for the same reason.
        /// </para>
        /// <para>
        /// <b>Three bounds, and the smallest wins.</b> What the reserve can spare above
        /// <see cref="RunConfig.GrowthReserveFloor"/> of the body it already has; what the water
        /// within reach can pay for at <see cref="RunConfig.MatterPerTissueJoule"/>; and what is
        /// left of the adult. A body that cannot draw the matter grows as far as the matter it
        /// could get and no further (rule 5's last sentence), and <see cref="GrowthShortOfMatter"/>
        /// is the column that says so.
        /// </para>
        /// <para>
        /// <b>The body is authoritative and the ledger is charged what the body cost.</b> The new
        /// tissue figure is re-measured with <see cref="Metabolism.TissueJoules"/> on the scaled
        /// phenotype rather than accumulated from the increments, so the number the world holds
        /// and the number the body is worth cannot drift apart over ten thousand steps of
        /// rounding — and the reserve is debited exactly that difference, so the transfer is still
        /// a transfer.
        /// </para>
        /// <para>
        /// <b>Growth is not a rate, which is why this takes no step length.</b> A body invests
        /// everything above the floor every time this runs, so how fast it reaches its adult size
        /// is set by what it can earn and what the water will sell it, not by a growth constant
        /// nobody would know how to choose. The consequence is that the step length does change
        /// how many bites the same growth is taken in, and that is the honest situation: rule 5
        /// gave the world an appetite, not a schedule.
        /// </para>
        /// <para>
        /// <b>Matter is charged for the target and tissue is booked for the actual</b>, since the
        /// scaled body is re-measured after the matter has been bought; the two differ by the
        /// rounding of a cube root, both identities close, and the rates above are honoured to
        /// that rounding.
        /// </para>
        /// <para>
        /// <b>Nothing here draws from an <see cref="Rng"/>.</b> Growth is arithmetic on a body and
        /// a cell, so a world with growth switched off by every creature already being adult steps
        /// through exactly the trajectory it would have without this pass.
        /// </para>
        /// </remarks>
        private void Grow()
        {
            float matterRate = Config.MatterPerTissueJoule;
            float reserveFloor = Config.GrowthReserveFloor;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (creature.BodyFraction >= 1f) continue;

                float adultTissue = creature.AdultTissueJoules;
                float remaining = adultTissue - creature.TissueJoules;
                if (!(remaining > 0f)) continue;

                // What the reserve can spare. The floor is a fraction of the body this creature
                // already has, so a large body keeps a large buffer and a newborn keeps a small
                // one — the same proportion of the same thing at every size.
                float target = creature.Energy - reserveFloor * creature.TissueJoules;
                if (target > remaining) target = remaining;
                if (!(target > 0f)) continue;

                bool matterBound = false;
                float paidMatter = 0f;

                if (matterRate > 0f)
                {
                    // The gate before the take, for the reason conception has one: a partial take
                    // that is then abandoned leaks matter out of the world, and it leaks fastest
                    // exactly when matter is scarce enough to matter.
                    float affordable = (float)(Matter.ReachableStock(creature.Point) / matterRate);
                    if (affordable < target)
                    {
                        target = affordable;
                        matterBound = true;
                    }

                    if (target > 0f)
                    {
                        float asked = matterRate * target;
                        paidMatter = Matter.Take(creature.Point, asked);

                        // Grown only as far as it paid. A vertex field's take can come up a
                        // rounding short of what its gate promised (D083's second amendment), and
                        // booking the growth regardless would create tissue from nothing exactly
                        // as booking a conception's price did.
                        if (paidMatter < asked)
                        {
                            matterBound = true;
                            target = paidMatter / matterRate;
                        }
                    }
                }

                if (matterBound) GrowthShortOfMatter++;
                if (!(target > 0f))
                {
                    if (paidMatter > 0f) Matter.Deposit(creature.Point, paidMatter);
                    continue;
                }

                float fraction = (creature.TissueJoules + target) / adultTissue;
                if (fraction > 1f) fraction = 1f;

                // The cube root, because the fraction is a volume and the parts are scaled by a
                // length. Phenotype.Scaled says why the adult is the thing scaled from.
                Phenotype grown = fraction >= 1f
                    ? creature.AdultPhenotype
                    : creature.AdultPhenotype.Scaled(
                        (float)Math.Pow(fraction, 1d / 3d), Config.Shapes);

                float actual = fraction >= 1f ? adultTissue : Metabolism.TissueJoules(grown, Config);
                float spend = actual - creature.TissueJoules;

                // The one case this refuses: a body whose re-measured tissue costs a hair more
                // than the reserve holds. It is reachable only when the increment is already down
                // in the rounding, and growing anyway would take a creature to a negative reserve
                // — a death by bookkeeping rather than by starvation, which §5A.6 does not have.
                // The matter goes back where it came from, as a refused conception's does.
                if (spend > creature.Energy)
                {
                    if (paidMatter > 0f) Matter.Deposit(creature.Point, paidMatter);
                    continue;
                }

                creature.Energy -= spend;
                creature.TissueJoules = actual;
                creature.Phenotype = grown;
                creature.BodyFraction = actual >= adultTissue ? 1f : actual / adultTissue;

                if (paidMatter > 0f)
                {
                    creature.LockedMatter += paidMatter;
                    MatterInBodies += paidMatter;
                }

                // The two cached readings of a body that has just changed size. The standing cost
                // is asked at this creature's own age so that growing does not quietly reset its
                // senescence (D038); the absorptive volume is instrumentation, and a mouth that
                // grew must be measured as the mouth it now is.
                creature.StandingWatts = Metabolism.StandingWatts(grown, Config, creature.Age);
                creature.AbsorptiveVolume = AbsorptiveVolumeOf(grown);
            }
        }

        /// <summary>Volume of <see cref="CellTypeIds.Absorptive"/> tissue in a body, m³.</summary>
        /// <remarks>
        /// One walk, two callers — <c>Admit</c> at birth and <see cref="Grow"/> whenever the body
        /// changes size. Written once because the two must agree about what a mouth is, which is
        /// the reason <see cref="HasAbsorptive"/> exists in this file rather than in two.
        /// </remarks>
        private static float AbsorptiveVolumeOf(Phenotype phenotype)
        {
            float volume = 0f;
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == CellTypeIds.Absorptive) volume += parts[i].Volume;
            }
            return volume;
        }

        /// <summary>
        /// Removes a creature from the population and settles its books — the only place a body
        /// leaves the world.
        /// </summary>
        /// <param name="creature">The body leaving the population.</param>
        /// <param name="index">Its position in <c>_living</c>, which the caller already knows.</param>
        /// <param name="cause">What the lineage row will say.</param>
        /// <remarks>
        /// <b>One copy of the deposit logic, reached by both causes.</b> Starvation walks the
        /// population and finds this at the bottom of the metabolic loop;
        /// <see cref="KillDiverged"/> arrives from outside <see cref="Step"/> with a body the
        /// physics has already destroyed. A second copy of "deposit the tissue, return the
        /// matter, count the death" for the second caller is exactly how the two would come to
        /// disagree about the audit, and the audit is the one thing here that cannot be allowed
        /// to drift.
        /// </remarks>
        private void Bury(Organism creature, int index, DeathCause cause)
        {
            // The absorptive log's final row, taken before the death path zeroes anything:
            // this is the terminal budget, and the reserve it records is the (negative)
            // overdraft that killed the creature rather than the 0 the next line writes.
            // Starvation is the only cause the ecology has, so cause of death discriminates
            // nothing among these rows — this row is what does.
            if (creature.HasAbsorptiveTissue) BufferAbsorptiveDeath(creature);

            // Death at exactly zero, not below. A creature carrying negative energy would be
            // a debt the world has no way to settle, and the §5A.2 audit would never close.
            // A diverged body is generally solvent, so this is where its whole reserve leaves
            // the world — the same line, carrying a much larger number.
            EnergyOut += creature.Energy;
            creature.Energy = 0f;

            // Rule 6 of fable-propose-grid.md: above zero the body leaves a corpse instead, and
            // the two deposits below happen in instalments from wherever the corpse has drifted
            // to. Nothing else about the death changes. The reserve still leaves the world on
            // the line above, the matter still leaves MatterInBodies, and the lineage row is the
            // same row. Guarded on the knob rather than written as a general path, so at 0 the
            // deposits below run exactly as they always have and every world on file replays.
            if (Config.CorpseDecayPerSecond > 0f &&
                (creature.TissueJoules > 0f || creature.LockedMatter > 0f))
            {
                // A body with nothing to give founds nothing: a floor founder that never paid for
                // itself and developed into no tissue would otherwise leave an empty object for
                // the pass to carry and drop.
                _corpses.Add(new Corpse(
                    creature.Id, creature.Point.Position, creature.Patch,
                    creature.TissueJoules, creature.LockedMatter));

                // The matter is out of the body from this instant and in the corpse, so
                // StandingMatter's three accounts add to what its two added to before. The
                // tissue needs no counterpart line: StandingJoules reads a living body's tissue
                // and a corpse's joules into the same total, and the body's own is zeroed on the
                // line after.
                MatterInBodies -= creature.LockedMatter;
                creature.LockedMatter = 0f;
                creature.TissueJoules = 0f;
            }
            else
            {
                // The body becomes detritus where it died — §5A.2c. This is the whole reason
                // anything other than a plant can live, and the reason the doomed half of
                // generation zero is the world's first food rather than merely a waste of seeds.
                // HeightY is the last height Observe accepted, and Observe refuses a non-finite one
                // — so this is the last *finite* depth even when the body's own transform is NaN.
                Nutrients.Deposit(creature.Point, creature.TissueJoules);
                if (creature.TissueJoules > 0f) DetritusDepositedTotal += creature.TissueJoules;

                // Whatever matter is still locked returns to the layer the body died in, and
                // sinks from there — which is why the deep is rich and the surface is not.
                // LockedMatter (D052) is what remains after a lifetime of excretion and growth:
                // the price paid at conception, plus everything rule 5's growth bought, less
                // everything excretion gave back. A floor founder starts at 0 because it never
                // paid a conception price, and since growth exists it does not stay there — it
                // owes back exactly the matter its own growth took out of the water, and no more.
                if (creature.LockedMatter > 0f)
                {
                    Matter.Deposit(creature.Point, creature.LockedMatter);
                    MatterInBodies -= creature.LockedMatter;
                    creature.LockedMatter = 0f;
                }

                creature.TissueJoules = 0f;
            }

            _living.RemoveAt(index);
            _dead.Add(creature);
            Deaths++;

            _lineageEvents.Add(LineageEvent.Death(ElapsedSeconds, creature.Id, cause));
        }

        /// <summary>
        /// Kills a creature whose articulation has diverged, as a death rather than as a crash —
        /// the divergence spec after logbook/0056.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a death and not an exception.</b> <c>r20q-s1</c> lost 15,345 simulated seconds
        /// of a 20,000 s arm because one body's articulation exploded: PhysX refused
        /// <c>{NaN, NaN, NaN}</c> forces for its three parts nine steps running, and then
        /// <see cref="Observe"/> saw the non-finite height and took the run down. One creature in
        /// seventeen hundred is not a reason to censor an arm — but it is also not something to
        /// swallow silently, which is why the count is reported and the harness dumps the body's
        /// last finite state before calling this.
        /// </para>
        /// <para>
        /// <b>The books still close.</b> The tissue is deposited and the matter returned at
        /// <see cref="Organism.HeightY"/> — the last height <see cref="Observe"/> accepted, since
        /// it refuses a non-finite one — so §5A.2's audit and the matter identity see exactly what
        /// a starvation of the same body would have moved. The physics is what failed; the
        /// economy is not allowed to lose track of a joule over it.
        /// </para>
        /// <para>
        /// Called from outside <see cref="Step"/>, between physics steps, which is safe for the
        /// same reason <see cref="Observe"/> is: it touches no <see cref="Rng"/> stream and takes
        /// no branch any other creature's step depends on.
        /// </para>
        /// </remarks>
        public void KillDiverged(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            int index = _living.IndexOf(creature);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"Creature {creature.Id} is not living in this world, so there is nothing to " +
                    "kill. A body that diverged after it had already died means the scene and " +
                    "the population have come apart.",
                    nameof(creature));
            }

            Diverged++;
            Bury(creature, index, DeathCause.Diverged);
        }

        /// <summary>
        /// Moves creatures between adjacent patches — D061. A metapopulation-style throttle
        /// rather than continuous advection (D061's rejected alternative): each living creature
        /// draws once, in list order, and the whole method is skipped whenever there is nowhere
        /// to disperse to or nothing asks for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Guarded behind both halves so K=1 stays bit-identical.</b> Even a positive
        /// <see cref="RunConfig.DispersalChancePerStep"/> draws nothing when
        /// <see cref="PatchCount"/> is 1 — there is no adjacent patch to move to, and the method
        /// returns before touching <see cref="Rng"/> at all — so turning that knob on alone
        /// cannot perturb a K=1 run's trajectory. Symmetrically, K&gt;1 with the chance at its
        /// default of 0 draws nothing either: patches that never exchange creatures is a
        /// legitimate D061 configuration in its own right (isolated columns), not an oversight.
        /// </para>
        /// <para>
        /// <b>One seed per creature, one draw, split three ways.</b> Each living creature — walked
        /// in <c>_living</c>'s own order, which is deterministic for a given population state —
        /// draws <c>Rng.SeedFor(Seed, _nextIndex++)</c> exactly as a founder or an offspring does,
        /// and spends a single <see cref="Rng.NextFloat"/> on it: the bottom
        /// <see cref="RunConfig.DispersalChancePerStep"/>/2 of [0, 1) moves the creature to the
        /// patch behind it, the next equal-sized slice moves it to the patch ahead, and the
        /// remainder — everything from the chance upward — leaves it where it was. One float
        /// split three ways is exactly as unbiased as a chance draw followed by a direction draw
        /// and costs half the RNG stream.
        /// </para>
        /// <para>
        /// <b>The ring, both directions</b> — <c>(patch + 1) % PatchCount</c> and
        /// <c>(patch - 1 + PatchCount) % PatchCount</c>, the same wraparound
        /// <see cref="NutrientField.Mix"/>'s horizontal pass uses, so a creature can reach every
        /// patch by a sequence of single steps and no patch is architecturally an edge.
        /// </para>
        /// <para>
        /// Run after <see cref="Metabolise"/> and before <see cref="Reproduce"/>: this step's
        /// feeding and shading were already priced at the patch each creature held when the step
        /// began, and an offspring conceived this step inherits the patch its parent ends the
        /// step in.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Sets every living creature's patch from where its body is — D077's second rule.
        /// </summary>
        /// <remarks>
        /// A whole-population pass once per metabolic step, which is 2 Hz against the physics'
        /// 100, and one dictionary lookup per creature inside the placer. It costs what
        /// <see cref="Disperse"/> cost and draws nothing, where <see cref="Disperse"/> drew once
        /// per creature per step.
        /// </remarks>
        private void ReadPatchesFromPositions()
        {
            if (!Config.SharedSpace || Placement == null) return;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                creature.Patch = Placement.PatchOf(creature);
            }
        }

        private void Disperse()
        {
            // D077. Retired, not merely disabled: with the box literal a creature's patch is read
            // from where its root is, and a lottery that moved the index without moving the body
            // would put a creature in one patch's water while it swam in another's. Returned from
            // before the draw, so nothing is taken from the RNG stream — the same guard the K=1
            // case below makes, for the same reason.
            if (Config.SharedSpace) return;

            if (!(Config.DispersalChancePerStep > 0f) || PatchCount <= 1) return;

            float chance = Config.DispersalChancePerStep;
            float half = chance * 0.5f;
            int patches = PatchCount;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];

                ulong seed = Rng.SeedFor(Seed, _nextIndex++);
                float draw = new Rng(seed).NextFloat();

                if (draw < half)
                {
                    creature.Patch = (creature.Patch - 1 + patches) % patches;
                }
                else if (draw < chance)
                {
                    creature.Patch = (creature.Patch + 1) % patches;
                }
            }
        }

        /// <summary>
        /// Carries creatures sideways with the roll they are in — D066's body half, the
        /// counterpart of <see cref="NutrientField.Advect"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Off is off.</b> Without <see cref="CurrentField.Rolls"/>, more than one patch and a
        /// positive speed, this returns before touching <see cref="Rng"/> at all — so every run
        /// before D066 replays bit for bit, the same guarantee <see cref="Disperse"/> makes for
        /// D061 and for the same reason: a knob that perturbs the RNG stream when it is off
        /// invalidates every result on file.
        /// </para>
        /// <para>
        /// <b>One extra draw per creature per step when it is on</b>, split three ways exactly as
        /// <see cref="Disperse"/> splits its own — the bottom slice crosses to the patch behind,
        /// the next to the patch ahead, the rest stays. That is the cost of the mechanism and it is
        /// the same order as dispersal's; it is documented rather than avoided because the
        /// alternative, reusing dispersal's draw, would make two independent mechanisms correlated
        /// in a way nobody could see in the output.
        /// </para>
        /// <para>
        /// <b>Both boundaries, and only outward.</b> A creature leaves through the face the water
        /// is leaving through. Its own right-hand face carries it to <c>k+1</c> when
        /// <see cref="CurrentField.CrossingDirection"/> there is +1; its left-hand face — the one
        /// its neighbour <c>k-1</c> owns — carries it to <c>k-1</c> when the direction there is
        /// -1. Under the roll's alternating parity those two are the same statement: a patch that
        /// is an up-leg exports at the surface through both faces, and a down-leg imports through
        /// both, which is exactly what <see cref="NutrientField.Advect"/> does with the stock
        /// beside it. Asking only about the creature's own face instead would have carried bodies
        /// one way while the detritus went the other, and D066's whole claim is that the two travel
        /// together.
        /// </para>
        /// </remarks>
        private void AdvectBodies(float seconds)
        {
            // D077. Retired for Disperse's reason: in a shared volume the water already carries
            // bodies — FluidEnvironment applies the current's horizontal drag to every part every
            // physics step — so a second, index-level transport would be the same flow counted
            // twice, once as a force and once as a lottery. Before any draw.
            if (Config.SharedSpace) return;

            CurrentField current = Config.Current;
            if (current == null || PatchCount <= 1) return;

            // D067. The vent moves bodies along its legs on exactly the same terms the roll moves
            // them along its own, so the guard asks whether *either* flow is running rather than
            // only the roll. Speed belongs to the roll and VentSpeed to the vent — a world with a
            // vent and still water still advects, and a world with neither still draws nothing.
            bool rolls = current.Rolls && current.Speed > 0f;
            if (!rolls && !current.VentActive(PatchCount)) return;

            int patches = PatchCount;
            float width = Nutrients.PatchWidthMetres;
            if (!(width > 0f)) return;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                int patch = creature.Patch;
                int behind = (patch - 1 + patches) % patches;

                // Outward through the right-hand face, and outward through the left-hand one,
                // which belongs to the patch behind.
                double ahead = current.CrossingDirection(creature.HeightY, ElapsedSeconds, patch, patches) > 0
                    ? current.HorizontalCrossingFraction(
                        creature.HeightY, ElapsedSeconds, patch, patches, seconds, width)
                    : 0d;

                double back = current.CrossingDirection(creature.HeightY, ElapsedSeconds, behind, patches) < 0
                    ? current.HorizontalCrossingFraction(
                        creature.HeightY, ElapsedSeconds, behind, patches, seconds, width)
                    : 0d;

                ulong seed = Rng.SeedFor(Seed, _nextIndex++);
                float draw = new Rng(seed).NextFloat();

                if (draw < back) creature.Patch = behind;
                else if (draw < back + ahead) creature.Patch = (patch + 1) % patches;
            }
        }

        /// <remarks>
        /// <para>
        /// <b>A brood is truncated rather than refused</b> — §5A.2c. An offspring's body has to be
        /// built out of the parent's reserve, and what a body costs is not known until the mutated
        /// genome has been developed, so the affordable prefix of the brood is born and the rest
        /// is not. Refusing the whole brood instead would make a slightly-too-expensive mutation
        /// cost a lineage every offspring rather than one, which is a selection pressure invented
        /// by the accounting.
        /// </para>
        /// <para>
        /// The threshold gate is still checked first, on the part of the cost that <i>is</i> known
        /// in advance. Without it every solvent creature would mutate and develop a genome on
        /// every step just to discover it could not pay for it — the same work, at the cost of
        /// most of the run.
        /// </para>
        /// <para>
        /// <b>The order of this walk is a world rule, and until D072 it was an accident.</b>
        /// <see cref="Conceive"/> draws a child's matter from the parent's own layer at the moment
        /// that parent is walked, so when a layer's stock covers fewer children than there are
        /// solvent parents, whoever is walked first takes it. <c>_living</c> is birth-ordered, so
        /// that was always the oldest — a queue nothing in DESIGN.md asked for, selecting for
        /// outliving it rather than for fecundity (logbook/0056).
        /// <see cref="RunConfig.ConceptionOrder"/> names the walk;
        /// <see cref="ConceptionOrder.Age"/> is the queue and the default, so the record replays.
        /// </para>
        /// <para>
        /// <b>And the walk is where the energy economy gets its grip, or fails to</b> — D073,
        /// logbook/0057. Whoever is walked first is the only place a parent's reserve can decide
        /// anything about its fecundity: the gate below is a threshold, not a ranking, so two
        /// solvent bodies breed alike however far apart their books are.
        /// <see cref="ConceptionOrder.Reserve"/> walks them richest first, which is what the
        /// queue was doing by accident, with age standing in for income.
        /// </para>
        /// </remarks>
        private void Reproduce()
        {
            // Collected first and appended after, so an offspring cannot itself reproduce on the
            // step it was born — which it could if the list were grown while being walked, and
            // which would make brood size compound within a single step.
            _born.Clear();

            switch (Config.ConceptionOrder)
            {
                case ConceptionOrder.Shuffled:
                {
                    PermuteConceptionOrder();

                    for (int i = 0; i < _living.Count; i++) Brood(_living[_conceptionOrder[i]]);
                    break;
                }

                case ConceptionOrder.Reserve:
                {
                    // Only the solvent are ranked, so this walk is shorter than the other two —
                    // the ones it leaves out are the ones Brood would have turned away anyway.
                    int solvent = RankConceptionOrderByReserve();

                    for (int i = 0; i < solvent; i++) Brood(_living[_conceptionOrder[i]]);
                    break;
                }

                default:
                {
                    // Age, and every run before D072: _living's own birth order, untouched.
                    for (int i = 0; i < _living.Count; i++) Brood(_living[i]);
                    break;
                }
            }

            // Appended in walk order, so under Shuffled and Reserve this step's ordering also
            // decides the order the children sit in for the next one. That is not a second
            // decision to make: _living's order is only ever read by this walk, and the next step
            // orders whatever it finds afresh.
            for (int i = 0; i < _born.Count; i++)
            {
                _living.Add(_born[i]);
                Births++;
            }
        }

        /// <summary>
        /// Whether any part of a body is lighter than <see cref="RunConfig.MinNewbornPartKilograms"/>
        /// — fable-propose-growth.md rule 3.
        /// </summary>
        /// <remarks>
        /// Mass is <see cref="RunConfig.PartDensityKilogramsPerCubicMetre"/> times volume, which is
        /// the mass the harness gives the articulation body. Asked of the newborn and never of the
        /// adult: what the solver has to carry is the body that will exist.
        /// </remarks>
        private bool IsUnderTheMassFloor(Phenotype body)
        {
            float floor = Config.MinNewbornPartKilograms;
            if (!(floor > 0f)) return false;

            float minVolume = floor / RunConfig.PartDensityKilogramsPerCubicMetre;
            IReadOnlyList<PhenotypePart> parts = body.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Volume < minVolume) return true;
            }

            return false;
        }

        /// <summary>One parent's turn: the solvency gate, then the brood behind it.</summary>
        /// <remarks>
        /// <para>
        /// Lifted out of <see cref="Reproduce"/> so the two orders share one body and cannot drift
        /// apart — the walk is what D072 varies, and nothing else is.
        /// </para>
        /// <para>
        /// <b>The gate is the investment, since fable-propose-growth.md rule 2.</b> A parent breeds
        /// when its reserve holds <see cref="ReproductionTraits.BirthInvestment"/> of its own
        /// tissue value plus the litter's overhead, and it spends exactly that. The litter no
        /// longer multiplies what the event costs, only how many ways it is divided — which is
        /// what makes brood size a decision about the size of a child rather than about the price
        /// of a reproduction.
        /// </para>
        /// </remarks>
        private void Brood(Organism parent)
        {
            float gate = parent.ReproductionThreshold(Config.PerOffspringOverheadJoules);
            if (gate <= 0f || parent.Energy < gate) return;

            for (int n = 0; n < parent.Genome.Reproduction.BroodSize; n++)
            {
                // A sibling refused under the mass floor is skipped, not the end of the litter.
                // The floor is a property of the body that was drawn, and the next sibling is a
                // fresh draw of a fresh mutation, so a brood of three with one dwarfed sibling
                // yields two children rather than one. An energy or matter shortfall is different
                // in kind: it is a property of the parent and is still true for every sibling
                // behind this one, so it ends the brood as it always has.
                if (Conceive(parent) == Conception.Refused) break;
            }
        }

        /// <summary>
        /// Fills <see cref="_conceptionOrder"/> with a fresh uniformly random permutation of
        /// <c>_living</c>'s indices — D072, logbook/0056.
        /// </summary>
        /// <remarks>
        /// Fisher–Yates, from <see cref="_conceptionRng"/> alone, so the permutation is a function
        /// of <c>(seed, config, step count)</c> and a shuffled run replays exactly like every other
        /// run here (§7). The array grows and is kept; it is never shrunk, because a world that
        /// halves its population and grows back would otherwise reallocate on every recovery.
        /// </remarks>
        private void PermuteConceptionOrder()
        {
            int count = _living.Count;
            if (_conceptionOrder.Length < count) _conceptionOrder = new int[count];

            for (int i = 0; i < count; i++) _conceptionOrder[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int j = _conceptionRng.Range(i + 1);

                int swap = _conceptionOrder[i];
                _conceptionOrder[i] = _conceptionOrder[j];
                _conceptionOrder[j] = swap;
            }
        }

        /// <summary>
        /// Fills the front of <see cref="_conceptionOrder"/> with the solvent parents' indices,
        /// richest first, and returns how many there are — D073, logbook/0057.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Energy buys fecundity.</b> A body's surplus is what it holds above the price of the
        /// child it is asking for, and the layer's matter goes down that list until it runs out.
        /// The gate is the one <see cref="Brood"/> applies, computed here once per body and used
        /// both to reject the insolvent and to rank the rest — the same rule read twice would be
        /// two rules waiting to disagree.
        /// </para>
        /// <para>
        /// <b>One step's ranking, not a running auction.</b> Every surplus is read before the walk
        /// begins, so a parent that has just bred — and is now poorer than the body behind it —
        /// keeps its place for the rest of the step. Re-ranking after each birth would be a
        /// different world rule and a sort per birth; D073 asked for this one.
        /// </para>
        /// </remarks>
        private int RankConceptionOrderByReserve()
        {
            int count = _living.Count;
            if (_conceptionOrder.Length < count) _conceptionOrder = new int[count];
            if (_conceptionSurplus.Length < count) _conceptionSurplus = new float[count];

            int solvent = 0;

            for (int i = 0; i < count; i++)
            {
                Organism parent = _living[i];

                float gate = parent.ReproductionThreshold(Config.PerOffspringOverheadJoules);
                if (gate <= 0f || parent.Energy < gate) continue;

                _conceptionSurplus[i] = parent.Energy - gate;
                _conceptionOrder[solvent++] = i;
            }

            Array.Sort(_conceptionOrder, 0, solvent, _byReserve);

            return solvent;
        }

        /// <summary>How one attempt at one offspring ended.</summary>
        /// <remarks>
        /// Three outcomes rather than a bool, because two of the failures mean opposite things to
        /// the litter behind them. <see cref="Refused"/> is the parent's own shortfall and ends
        /// the brood; <see cref="UnderFloor"/> is a fact about the body just drawn and ends only
        /// this sibling (fable-propose-growth.md rule 3, as amended in review 2026-09-08).
        /// </remarks>
        private enum Conception
        {
            /// <summary>A child was admitted, or a stillbirth was counted and settled.</summary>
            Born,

            /// <summary>The parent could not afford it. The rest of the brood is abandoned.</summary>
            Refused,

            /// <summary>This body was under the mass floor. The next sibling still draws.</summary>
            UnderFloor,
        }

        /// <summary>
        /// Makes one offspring if the parent can afford it and the body it drew can be built.
        /// </summary>
        private Conception Conceive(Organism parent)
        {
            // Before anything expensive. See CheapestPossibleChildMatter: if the parent's layer
            // cannot afford the smallest child that could exist, no mutation of this genome can
            // be afforded either, and building one to find that out is the dominant cost in a
            // matter-limited world.
            if ((Config.MatterPerTissueJoule > 0f || Config.MatterPerCreature > 0f) &&
                Matter.ReachableStock(parent.Point) < CheapestPossibleChildMatter)
            {
                ConceptionsBlockedByMatter++;
                return Conception.Refused;
            }

            ulong seed = Rng.SeedFor(Seed, _nextIndex++);

            Genome childGenome = Mutator.Mutate(
                parent.Genome, new Rng(seed), Config.Mutation, Config.CellTypes, Config.Genome,
                Config.SensorPool());

            Phenotype body = Developer.Develop(
                childGenome, Config.Development, null, Config.Shapes);

            // A body of no parts is a stillbirth (§4.5's extinction-by-shrinking; Admit counts it
            // and settles the energy). Read here rather than further down because everything
            // between this line and the birth has to know that there is no body to size, price in
            // matter or place.
            bool stillborn = body.PartCount == 0;

            // fable-propose-growth.md rules 2 and 3. The parent spends a fraction of its own body
            // value on the litter, each child gets an equal share, and the share is the child's
            // whole start: the body it is born with plus its first reserve, split by a world
            // constant so that no lineage can set its children's reserve to zero.
            ReproductionTraits traits = parent.Genome.Reproduction;
            float share = traits.BirthInvestment * parent.TissueJoules / traits.BroodSize;

            float adultTissue = Metabolism.TissueJoules(body, Config);

            // The whole share is capped, not only the body it buys (rule 3, corrected in the
            // review of 2026-09-08). A parent may not buy a creature larger than its genome
            // describes, so the ceiling is the share that exactly fills the adult body once the
            // reserve has been taken out of it. Capping only the body left a child whose adult
            // scale had mutated well below its parent's born full grown and holding a reserve
            // sized to the parent's body: a windfall paid for being small, which is a pressure
            // nobody chose. Everything above the capped share stays with the parent. The other
            // end is not clamped. A share worth almost nothing makes an almost-nothing child,
            // and the mass floor below is what refuses that.
            float reserveFraction = Config.NewbornReserveFraction;

            if (!stillborn && adultTissue > 0f && reserveFraction < 1f)
            {
                float cap = adultTissue / (1f - reserveFraction);
                if (share > cap) share = cap;
            }

            float reserve = share * reserveFraction;
            float bodyValue = share - reserve;

            float fraction = adultTissue > 0f ? bodyValue / adultTissue : 1f;
            if (fraction > 1f) fraction = 1f;

            Phenotype newborn = stillborn || fraction >= 1f
                ? body
                : body.Scaled((float)Math.Pow(fraction, 1d / 3d), Config.Shapes);

            float tissue = stillborn || fraction >= 1f
                ? adultTissue
                : Metabolism.TissueJoules(newborn, Config);

            // Rule 3's other edge, and it is a refusal rather than a smaller child. Every
            // divergence on record was a newborn and the lightest link among them weighed
            // 0.143 kg, so a body the physics cannot carry must not be built at all. A genome
            // whose investment over its litter sits under the floor therefore never breeds, which
            // is something selection can see. The parent keeps its reserve: nothing was spent and
            // no lineage row is written, exactly as for a crowded refusal. The sibling behind
            // this one still draws, because the next mutation may not be under the floor.
            if (!stillborn && IsUnderTheMassFloor(newborn))
            {
                ConceptionsUnderMassFloor++;
                return Conception.UnderFloor;
            }

            float price = tissue + reserve + Config.PerOffspringOverheadJoules;

            if (parent.Energy < price) return Conception.Refused;

            // Energy is necessary and, from D048, no longer sufficient. Tissue is matter, and a
            // parent with sunlight to spare and nothing dissolved in the water around it does not
            // breed. Drawn from the parent's own layer, so success at a depth depletes that
            // depth — the negative feedback the world previously had nowhere at all.
            // D065 adds the fixed term. Two terms, one price: everything downstream — the stock
            // check, the Take, LockedMatter, and therefore excretion and the death deposit — sees
            // a single number and carries the fixed part without knowing it is there.
            float matterPrice = Config.MatterPerTissueJoule * tissue + Config.MatterPerCreature;

            // Checked before taking, not by taking. NutrientField.Take is a partial-take API:
            // it removes min(asked, stock) and returns that. Calling it and bailing when the
            // return is short removes the partial amount and then drops it on the floor, which
            // leaks matter on every blocked conception — 132 units of 24,000 in a 400 s test,
            // and it leaks fastest exactly when matter is scarce enough to matter.
            // A stillbirth is charged no matter: the fixed term (D065) is machinery mass with no
            // body to sit in, and charging it here put it into MatterInBodies with no owner and no
            // death to return it -- the Astra review's R2 (2026-09-07). The energy rule is
            // unchanged: the parent pays the child's start and the overhead, and both leave the
            // world in Admit, exactly as before.
            if (!stillborn && matterPrice > 0f &&
                Matter.ReachableStock(parent.Point) < matterPrice)
            {
                ConceptionsBlockedByMatter++;
                return Conception.Refused;
            }

            // D077. The last gate, and deliberately after every solvency check and before the
            // first thing that is spent: a body with nowhere to go costs the parent nothing, so
            // the order is "can it be afforded, then can it be placed", never the reverse. A
            // refusal here is a crowded stillbirth — counted, and otherwise as though the parent
            // had simply not bred this step. Last also so that a successful reservation is always
            // followed by a Commit or a Release: nothing between here and Admit can refuse.
            //
            // Skipped for a body of no parts: Admit is about to call that stillborn for the older
            // reason (§4.5's extinction-by-shrinking), and reserving room for a body that will
            // never exist would leave a reservation to be released again for nothing.
            int childPatch = parent.Patch;

            // The depth the child is admitted at. Its parent's, as it has always been, and the
            // placer may raise it — a parent resting on a solid sea bed breeds beside itself, not
            // into the rock (scratch/floor-spec.md rule 2). A local rather than the expression
            // inline at Admit so that the height the body is built at and the height the economy
            // charges are the same number; in the tiled world nothing touches it and the
            // expression is parent.HeightY exactly, as before.
            float childHeight = parent.HeightY;

            // The adult and not the newborn, reversed 2026-09-10. What the placer is asked for
            // used to be room for the body that is about to exist, on the reasoning that a child
            // born at a fifth of its adult volume needs a little over half the clearance and that
            // asking for the adult's would refuse births into water a child fits in perfectly
            // well. Since D087 that is the wrong end of the creature's life: the child does not
            // stay that size. It grows into its adult body in place, up to twentyfold, inside a
            // spot reserved for the body it was born as, and its neighbours grow towards it at the
            // same time. Round 33's seed 2 threw three bodies to NaN in one contact cluster at
            // 3,066.5 s, and the step-after-resize instrument peaked at 25 cm of engine
            // displacement in that arm: bodies being shoved on a growth step (logbook/0082). So
            // the world reserves the room the lineage will actually need. In a crowded
            // neighbourhood that refuses more births, and those refusals are counted as crowded
            // stillbirths, which is the honest reading: the room was not there.
            bool shared = Config.SharedSpace && Placement != null && newborn.PartCount > 0;

            if (shared && !Placement.TryReserveOffspring(parent, body, ref childHeight, out childPatch))
            {
                CrowdedStillbirths++;
                return Conception.Refused;
            }

            if (!stillborn && matterPrice > 0f)
            {
                // What the water gave, not what was asked. The cell field's gate made the two
                // equal by construction; a vertex field's take can fall a rounding short of
                // what its gate promised, and the first build booked the price regardless,
                // which created matter from nothing (D083's second amendment, logbook/0074).
                // A take short by more than a rounding is a refusal: the matter goes back
                // where it came from, the reservation is dropped, and the count says so.
                float taken = Matter.Take(parent.Point, matterPrice);

                if (taken < matterPrice * (1f - 1e-4f))
                {
                    if (taken > 0f) Matter.Deposit(parent.Point, taken);
                    if (shared) Placement.Release();
                    ConceptionsBlockedByMatter++;
                    ConceptionsShortOfMatter++;
                    return Conception.Refused;
                }

                MatterInBodies += taken;
                matterPrice = taken;
            }

            parent.Energy -= price;

            // The child's body and its first reserve are transferred and stay in the world; the
            // overhead is burned. It is paid per offspring, so it does not by itself tell one
            // brood of four from four broods of one (corrected 2026-09-07); the gate above does,
            // by asking the parent to hold the whole litter's investment at once. What the
            // overhead does is make an offspring cost more than the energy it carries (§5A.6).
            EnergyOut += Config.PerOffspringOverheadJoules;

            Organism child = Admit(
                childGenome, newborn, BirthKind.Reproduction, seed, parent.Id,
                parent.GenerationDepth + 1, reserve, tissue, childHeight, parent,
                patch: childPatch, adultPhenotype: body, adultTissue: adultTissue);

            // D077. The reservation belongs to a creature now, or to nobody. Admit cannot
            // actually refuse a body with parts, so the Release below is a belt rather than a
            // brace — and it is the branch that keeps the invariant true by construction instead
            // of by reading Admit.
            if (shared)
            {
                if (child != null) Placement.Commit(child.Id);
                else Placement.Release();
            }

            if (child != null)
            {
                // What the layer was just charged for this body — D052's starting balance. It
                // is a balance and no longer a constant: excretion draws it down, and since
                // fable-propose-growth.md rule 5 every growth step adds the matter the new tissue
                // was bought with, so a body that reaches adulthood owes the water far more at
                // death than it was charged at conception.
                child.LockedMatter = matterPrice;
                _born.Add(child);

                // Realised fecundity, counted where a birth actually happens rather than
                // reconstructed later from lineage.jsonl's parent column — the absorptive log
                // has to carry it per row, and a stillbirth (Admit returning null) is not a
                // child however much energy it cost. Pure instrumentation: nothing branches on it.
                parent.Children++;
                parent.LastChildSeconds = ElapsedSeconds;
            }

            return Conception.Born;
        }

        /// <remarks>
        /// Fresh founders rather than descendants of survivors, and a trickle rather than a cohort
        /// — D021. Choosing who repopulates would be selection performed by us, and a cohort
        /// spawned together tends to die together, manufacturing a boom-and-bust that is an
        /// artefact of this method rather than a property of the world.
        /// </remarks>
        private void EnforceFloor()
        {
            // Config.FloorClosesAfterSeconds (0 = never) — D021 wants the founding cohort but
            // nothing after it. This runs before the population check so a world that has already
            // crashed to zero past the threshold is left at zero rather than rescued.
            if (Config.FloorClosesAfterSeconds > 0f && ElapsedSeconds >= Config.FloorClosesAfterSeconds)
            {
                return;
            }

            if (_living.Count >= Config.MinimumPopulation) return;

            SecondsSinceFloorFired = 0.0;

            int wanted = Math.Min(
                Config.MinimumPopulation - _living.Count,
                Math.Max(1, Config.FloorSpawnsPerStep));

            for (int i = 0; i < wanted; i++)
            {
                ulong seed = Rng.SeedFor(Seed, _nextIndex++);
                var rng = new Rng(seed);

                Genome genome = GenomeFactory.Founder(rng, Config.Genome, Config.SensorPool());

                // Placed through the lit zone rather than at the surface. Starting everything at
                // depth zero would hand generation zero the best light in the world and make the
                // §5A.2 calibration read as more generous than it is.
                //
                // The draw runs to the full spread, which in the reference world is the world's
                // own depth (EVOSIM_FOUNDER_DEPTH 60 in a 60 m world), so a founder can be drawn
                // at the very bottom. The placer raises it clear of a solid sea bed when there is
                // one — see IBodyPlacement.TryReserveFounder — rather than the draw being narrowed
                // here, because how much room a body needs is a fact about the body and the
                // world's geometry, neither of which Core has.
                float height = -rng.Range(0f, Config.FounderDepthSpread);

                // D061. A second, independent seed slot, drawn only when there is more than one
                // patch to land in — the CLAUDE.md guard: any new Rng draw on a path that runs at
                // K=1 breaks bit-identity, so this is skipped entirely rather than drawn and
                // discarded. A separate draw rather than one more call against `rng` above: reusing
                // it would make where a founder lands depend on how many draws GenomeFactory.Founder
                // happened to make, coupling two things D061 wants independent of each other.
                // D077. The draw is retired in a shared volume — a founder's patch is wherever
                // the placement below put it, and drawing an index as well would give the world
                // two answers to the same question. Written as one condition rather than nested,
                // so the tiled branch is the same expression it has always been.
                int patch = 0;
                if (!Config.SharedSpace && PatchCount > 1)
                {
                    ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                    patch = new Rng(patchSeed).Range(PatchCount);
                }

                Phenotype adult = Developer.Develop(
                    genome, Config.Development, null, Config.Shapes);

                // fable-propose-growth.md rule 7: a founder is born as a child is, at its own
                // genome's birth fraction, so the founding lottery runs under the same rule as
                // every birth rather than seeding the world with adults nobody paid for. The
                // reserve the floor gives it is scaled the same way, so a founder placed at a
                // fifth of its adult body arrives with a fifth of the purse — otherwise the floor
                // would hand the smallest bodies the largest head starts.
                bool admissible = NewbornFrom(
                    genome, adult, out Phenotype body, out float tissue, out float birthFraction);

                // Rule 3 applies to a founder's body too, and the floor's answer is simply to
                // draw again next step. Counted rather than retried here, for the same reason a
                // stillborn founder is counted: a floor that redrew until something fitted would
                // be selecting for viability instead of sampling the genome space, and rule 3
                // exists because every divergence on record was a newborn.
                if (!admissible)
                {
                    FoundersUnderMassFloor++;
                    FloorSpawns++;
                    continue;
                }

                bool shared = Config.SharedSpace && Placement != null && body.PartCount > 0;

                // D077. A world with no room left refuses a founder exactly as it refuses a
                // birth, and the attempt is still counted — for the trickle's sake, per the
                // remark below, and because a floor that retried until something fitted would be
                // packing the world rather than sampling it.
                if (shared && !Placement.TryReserveFounder(adult, ref height, out patch))
                {
                    CrowdedStillbirths++;
                    FloorSpawns++;
                    continue;
                }

                Organism founder = Admit(
                    genome, body, BirthKind.Floor, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules * birthFraction,
                    tissue: tissue, heightY: height, parent: null,
                    patch: patch, adultPhenotype: adult,
                    adultTissue: Metabolism.TissueJoules(adult, Config));

                if (shared)
                {
                    if (founder != null) Placement.Commit(founder.Id);
                    else Placement.Release();
                }

                // A stillborn founder is still an attempt, and counting it keeps the floor's
                // trickle a trickle. Not counting it would let a step retry until something
                // developed, which is the floor quietly selecting for viability.
                FloorSpawns++;
                if (founder != null) _living.Add(founder);
            }
        }

        /// <summary>
        /// Injects <paramref name="count"/> copies of <paramref name="genome"/> at
        /// <paramref name="heightY"/> — D060's invasion assay. A hand that builds the experimental
        /// condition, labeled as such: the assay can never answer the endogenous question of
        /// whether the world's own mutation supply finds a consumer, only whether one persists
        /// once placed. Call it once, at whatever simulated time a pre-registration names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Follows <see cref="EnforceFloor"/>'s accounting exactly, because an inoculant is a
        /// second way energy enters the world from nothing — not a third.</b> Each copy is
        /// admitted with <see cref="RunConfig.FounderEnergyJoules"/>, zero
        /// <see cref="Organism.LockedMatter"/> at admission, the same as a floor founder, after
        /// which its own growth buys matter and owes that back at death (rule 5, 2026-09-08);
        /// generation depth 0 and no parent, so it founds its own species
        /// under D057 exactly as a floor founder does (<see cref="AssignSpecies"/> branches on
        /// <c>parent == null</c>, not on <see cref="BirthKind"/>).
        /// </para>
        /// <para>
        /// <b>The genome itself is never mutated.</b> Every copy develops the identical stored
        /// genome — the point is to introduce a verified lineage rather than wait for one to arrive
        /// by chance, so a mutated copy would not be the genome the caller verified.
        /// </para>
        /// <para>
        /// <b>Seeds derive from the world's own seed stream</b>, the same
        /// <c>Rng.SeedFor(Seed, _nextIndex++)</c> a floor spawn draws, so a run replays identically
        /// from <c>(genome, seed, configHash, inoculation)</c> — nothing here reaches for an
        /// independent source of randomness. The seed labels the birth (<see
        /// cref="Organism.BirthSeed"/>) rather than driving anything: there is no mutation to seed
        /// and <paramref name="heightY"/> is fixed rather than drawn, unlike a floor founder's
        /// scattered depth.
        /// </para>
        /// </remarks>
        /// <param name="genome">Copied verbatim into every inoculant. Not mutated.</param>
        /// <param name="count">How many copies to admit. Stillbirths still consume a seed and are
        /// still counted in <see cref="Inoculated"/>, matching the floor's own accounting.</param>
        /// <param name="heightY">World height, metres, every copy is placed at.</param>
        /// <exception cref="ArgumentException">
        /// The genome's newborn body would be under <see cref="RunConfig.MinNewbornPartKilograms"/>
        /// (rule 3). Thrown before anything is admitted, so the world is untouched.
        /// </exception>
        public void Inoculate(Genome genome, int count, float heightY)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            // Rule 3, asked once and before anything is spent. Every copy is the same genome
            // developed the same way, so the answer is the same for all of them; asking inside
            // the loop would burn a seed and leave the world half inoculated on the way to the
            // same exception. Loud rather than quiet, unlike the floor's own draw: an assay names
            // one genome deliberately, so a body the physics cannot carry is a mistake in the
            // pre-registration, and silently placing nothing would leave an experiment reporting
            // an inoculation that never happened.
            if (count > 0 && !NewbornFrom(
                    genome,
                    Developer.Develop(genome, Config.Development, null, Config.Shapes),
                    out Phenotype preflight, out _, out float preflightFraction))
            {
                throw new ArgumentException(
                    "inoculant is under the newborn mass floor: its lightest part weighs " +
                    FormattableString.Invariant(
                        $"{LightestPartKilograms(preflight):0.####} kg at its birth fraction of ") +
                    FormattableString.Invariant($"{preflightFraction:0.###}, and the floor is ") +
                    FormattableString.Invariant(
                        $"{Config.MinNewbornPartKilograms:0.####} kg ") +
                    "(fable-propose-growth.md rule 3). Raise the genome's birth investment, " +
                    "lower its brood size, or lower MinNewbornPartKilograms.",
                    nameof(genome));
            }

            for (int i = 0; i < count; i++)
            {
                ulong seed = Rng.SeedFor(Seed, _nextIndex++);

                // D061. Same second-seed-slot pattern as EnforceFloor, for the same reason: drawn
                // only when there is more than one patch to land in, so a K=1 assay is untouched
                // and the D060 assay's own guarantees (same seed stream, replays identically) are
                // extended rather than disturbed.
                // D077. Retired in a shared volume for EnforceFloor's reason, and written the
                // same way so the two paths cannot drift apart.
                int patch = 0;
                if (!Config.SharedSpace && PatchCount > 1)
                {
                    ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                    patch = new Rng(patchSeed).Range(PatchCount);
                }

                Phenotype adult = Developer.Develop(genome, Config.Development, null, Config.Shapes);

                // fable-propose-growth.md rule 7, exactly as EnforceFloor applies it: an
                // inoculant has no parent either, so it is born at its own genome's birth
                // fraction with the founder's purse scaled the same way. An assay that placed
                // adults into a world of growing bodies would be measuring a hand rather than a
                // lineage.
                // Refused above, once, before the loop: the preflight and this call see the
                // same genome and the same config, so this cannot come back false here.
                NewbornFrom(genome, adult, out Phenotype body, out float tissue,
                    out float birthFraction);

                bool shared = Config.SharedSpace && Placement != null && body.PartCount > 0;

                // Per copy, not per call: the placer may raise one inoculant clear of the sea bed
                // and the next must still be asked for the depth the caller named. The assay's
                // own guarantee is that every copy is put at heightY, and a copy that cannot be
                // is a fact about the floor rather than a new depth for the whole cohort.
                float placedHeight = heightY;

                if (shared && !Placement.TryReserveFounder(adult, ref placedHeight, out patch))
                {
                    CrowdedStillbirths++;
                    Inoculated++;
                    continue;
                }

                Organism creature = Admit(
                    genome, body, BirthKind.Inoculation, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules * birthFraction,
                    tissue: tissue, heightY: placedHeight, parent: null,
                    patch: patch, adultPhenotype: adult,
                    adultTissue: Metabolism.TissueJoules(adult, Config));

                if (shared)
                {
                    if (creature != null) Placement.Commit(creature.Id);
                    else Placement.Release();
                }

                // A stillborn inoculant is still an attempt — see EnforceFloor's identical remark.
                Inoculated++;
                if (creature != null) _living.Add(creature);
            }
        }

        /// <summary>Stillbirths — genomes that developed into no parts at all.</summary>
        /// <remarks>
        /// Worth counting rather than discarding silently. A lineage reaches this by drifting off
        /// either end of the size range (§4.5, <see cref="DevelopmentLimits.MaxPartVolume"/>), so
        /// a rising stillbirth rate says mutation is pushing bodies past what development will
        /// build — which looks, in a population count, exactly like ordinary mortality.
        /// </remarks>
        public long Stillbirths { get; private set; }

        /// <summary>
        /// The body a parentless creature is born with — fable-propose-growth.md rule 7.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A founder and an inoculant have no parent to take a share from, so they take it from
        /// themselves.</b> The birth fraction is the one every child of this genome would get,
        /// <c>BirthInvestment / BroodSize</c> less the newborn reserve, capped at the whole adult —
        /// so a lineage that starts small starts small however it entered the world, and the
        /// founding lottery is run under the rule the world actually has rather than under the old
        /// one where everything arrived grown.
        /// </para>
        /// <para>
        /// A stillbirth passes straight through: a body of no parts has no size to be born at, and
        /// <c>Admit</c> is about to refuse it for the older reason.
        /// </para>
        /// </remarks>
        /// <param name="birthFraction">
        /// What was realised, not what was asked for. The reserve is scaled by this, so it has to
        /// be the fraction the body actually came out at.
        /// </param>
        /// <returns>
        /// False when the body would arrive under <see cref="RunConfig.MinNewbornPartKilograms"/>.
        /// Rule 3 is a fact about what the solver can carry, and the solver does not care whether
        /// a body had a parent, so a founder and an inoculant are held to it exactly as a child
        /// is (added in the review of 2026-09-08; rule 7 already said founders are born as
        /// children are, and the first build applied the floor only to conceptions). A stillbirth
        /// passes as true: there is no body to weigh, and <c>Admit</c> refuses it for the older
        /// reason a line below.
        /// </returns>
        private bool NewbornFrom(
            Genome genome, Phenotype adult,
            out Phenotype newborn, out float tissue, out float birthFraction)
        {
            float adultTissue = Metabolism.TissueJoules(adult, Config);
            ReproductionTraits traits = genome.Reproduction;

            float wanted = traits.BirthInvestment / traits.BroodSize *
                           (1f - Config.NewbornReserveFraction);

            if (adult.PartCount == 0)
            {
                newborn = adult;
                tissue = adultTissue;
                birthFraction = 1f;
                return true;
            }

            if (!(wanted > 0f) || wanted >= 1f)
            {
                newborn = adult;
                tissue = adultTissue;
                birthFraction = 1f;
            }
            else
            {
                newborn = adult.Scaled((float)Math.Pow(wanted, 1d / 3d), Config.Shapes);
                tissue = Metabolism.TissueJoules(newborn, Config);
                birthFraction = adultTissue > 0f ? tissue / adultTissue : 1f;
            }

            return !IsUnderTheMassFloor(newborn);
        }

        /// <summary>
        /// The mass of the lightest part of a body, kilograms. For refusal messages only.
        /// </summary>
        private static float LightestPartKilograms(Phenotype body)
        {
            IReadOnlyList<PhenotypePart> parts = body.Parts;
            if (parts.Count == 0) return 0f;

            float lightest = float.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                float mass = parts[i].Volume * RunConfig.PartDensityKilogramsPerCubicMetre;
                if (mass < lightest) lightest = mass;
            }

            return lightest;
        }

        /// <summary>
        /// Develops a genome and turns it into a creature, or refuses it. Null means stillborn.
        /// </summary>
        /// <remarks>
        /// <b>A body of no parts would otherwise be immortal and free.</b> With nothing to price,
        /// its income and its upkeep are both exactly zero, so its energy never moves and the
        /// death-at-zero rule in §5A.6 never fires — a creature that costs nothing, does nothing
        /// and cannot die, occupying a slot against the population floor forever. It is reachable
        /// today: §4.5's extinction-by-shrinking prunes the root as readily as any other node.
        /// </remarks>
        /// <param name="parent">
        /// The parent, for <see cref="AssignSpecies"/> — null for a floor founder or an inoculant
        /// (<see cref="Inoculate"/>), neither of which has one. Distinct from
        /// <paramref name="parentId"/> (which both also set to -1) because species assignment
        /// needs the actual organism, not just its id, to read its current
        /// <see cref="Organism.SpeciesId"/>.
        /// </param>
        /// <param name="patch">
        /// The horizontal cell this creature is born into — D061. The parent's own patch for a
        /// reproduction (offspring inherit it, drawn nowhere), and a uniform draw from the
        /// world's seed stream for a floor founder or an inoculant, guarded behind
        /// <see cref="PatchCount"/> &gt; 1 at each call site.
        /// </param>
        /// <param name="adultPhenotype">
        /// The body this creature grows towards — fable-propose-growth.md rule 4. The same object
        /// as <paramref name="phenotype"/> for anything born at full size, and the unscaled
        /// development for anything born smaller.
        /// </param>
        /// <param name="adultTissue">
        /// <see cref="Metabolism.TissueJoules"/> of <paramref name="adultPhenotype"/>. Passed
        /// rather than measured here because every caller has just computed it to decide how big
        /// the newborn is, and measuring it twice is a walk over every part for nothing.
        /// </param>
        private Organism Admit(
            Genome genome, Phenotype phenotype, BirthKind kind, ulong seed, long parentId,
            int generationDepth, float energy, float tissue, float heightY, Organism parent,
            int patch, Phenotype adultPhenotype, float adultTissue)
        {
            if (phenotype.PartCount == 0)
            {
                Stillbirths++;

                // The energy still has to balance. A floor spawn's and an inoculation's endowment
                // were never created (see the EnergyIn credit below), so nothing is owed; an
                // offspring's was already deducted from its parent, so it leaves the world here and
                // must be recorded as leaving. Its tissue is zero either way — there is no body to
                // have paid for.
                if (kind == BirthKind.Reproduction) EnergyOut += energy;

                return null;
            }

            var creature = new Organism
            {
                Id = _nextId++,
                ParentId = parentId,
                GenerationDepth = generationDepth,
                BirthSeed = seed,
                Genome = genome,
                Phenotype = phenotype,
                AdultPhenotype = adultPhenotype,
                AdultTissueJoules = adultTissue,

                // Derived from the two tissue figures rather than passed alongside them, so the
                // body fraction is a readout of the energy ledger and cannot disagree with it.
                BodyFraction = adultTissue > 0f && tissue < adultTissue ? tissue / adultTissue : 1f,
                Energy = energy,
                TissueJoules = tissue,
                HeightY = heightY,
                BirthHeightY = heightY,
                Patch = patch,

                // D083. Beside the parent until the simulator reports where the body actually
                // is, and at the patch's centre for a body with no parent — the cell field never
                // reads these, and in a shared volume the next Observe overwrites them.
                X = parent != null ? parent.X : (patch + 0.5f) * Nutrients.PatchWidthMetres,
                Z = parent != null ? parent.Z : 0.5f * Nutrients.PatchWidthMetres,
                StandingWatts = Metabolism.StandingWatts(phenotype, Config),
            };

            // One pass over the parts, at the one moment a body plan is built. Growth changes a
            // body's size and never what it is made of (fable-propose-growth.md rule 4), so the
            // two flags below are fixed from birth to death; only the volume moves, and
            // <see cref="Grow"/> refreshes that where it changes it. The alternative is the
            // per-creature per-step loop the absorptive log would otherwise need just to decide
            // whether to record a creature at all.
            bool photosynthetic = false;
            IReadOnlyList<PhenotypePart> admitted = phenotype.Parts;
            for (int i = 0; i < admitted.Count; i++)
            {
                if (admitted[i].CellTypeId == CellTypeIds.Photosynthetic) photosynthetic = true;
            }

            creature.AbsorptiveVolume = AbsorptiveVolumeOf(phenotype);
            creature.HasAbsorptiveTissue = HasAbsorptive(phenotype);
            creature.HasPhotosyntheticTissue = photosynthetic;

            // Endowment and body are transferred from the parent, and a founder's or an
            // inoculant's are created out of nothing, so only those two are income the world has
            // to account for. Conflating any of this with reproduction would let a population
            // manufacture energy by breeding.
            if (kind == BirthKind.Floor || kind == BirthKind.Inoculation) EnergyIn += energy + tissue;

            // D057. After the stillbirth check, not before: a genome that never became a creature
            // has no species to found or inherit, and computing one would be wasted work on top
            // of the mutate-and-develop pass that already found it unviable.
            AssignSpecies(creature, parent);

            // The lineage-events instrument (pre-round-8, LITERATURE-REVIEW.md §9 item 9). After
            // AssignSpecies, not before, so the row carries the species the birth actually landed
            // in rather than a default. Stillbirths never reach here — the check above returns
            // null first — so this is exactly "an id was assigned", which is what a lineage row
            // means. The photosynthetic flag is the same local the pass above computed for
            // Organism.HasPhotosyntheticTissue, passed rather than recomputed, so a row and the
            // creature it describes can never disagree about what the body is made of.
            _lineageEvents.Add(LineageEvent.Birth(
                ElapsedSeconds, creature.Id, parentId, kind, generationDepth, creature.SpeciesId,
                HasAbsorptive(phenotype), phenotype.TotalDof > 0, photosynthetic, patch,
                creature.BodyFraction, genome.AdultScale));

            return creature;
        }

        /// <summary>
        /// Whether any part of a developed body is <see cref="CellTypeIds.Absorptive"/> — the same
        /// test <c>EvolutionRun.cs</c>'s report already applies per creature, reused here rather
        /// than reinvented so the two never disagree about what "absorptive" means.
        /// </summary>
        private static bool HasAbsorptive(Phenotype phenotype)
        {
            IReadOnlyList<PhenotypePart> parts = phenotype.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == CellTypeIds.Absorptive) return true;
            }
            return false;
        }

        /// <summary>
        /// Hands over every <see cref="LineageEvent"/> queued since the last call, and forgets
        /// them — the pre-round-8 instrument's drain API, meant to be called once per report row
        /// alongside <see cref="TakeDead"/>.
        /// </summary>
        /// <remarks>
        /// Swaps the list out for a fresh one rather than copying into a new list and clearing the
        /// old one (<see cref="TakeDead"/>'s pattern): draining is on the hot report path and most
        /// intervals have plenty of events, so a swap avoids copying every element for no reason.
        /// A quiet interval costs nothing but a reference read via <see cref="Array.Empty{T}"/>.
        /// <b>Never called by anything in <see cref="World"/> itself</b> — draining only empties a
        /// list, never touches <see cref="Rng"/>, <see cref="ElapsedSeconds"/> or any economy
        /// state, so a world stepped with this called every report row and one where it is never
        /// called at all step through bit-identical trajectories.
        /// </remarks>
        public IReadOnlyList<LineageEvent> DrainLineageEvents()
        {
            if (_lineageEvents.Count == 0) return Array.Empty<LineageEvent>();

            List<LineageEvent> taken = _lineageEvents;
            _lineageEvents = new List<LineageEvent>();
            return taken;
        }

        /// <summary>
        /// Most rows one call to <see cref="CollectAbsorptiveLog"/> will produce for the living,
        /// and the depth of the death-row buffer.
        /// </summary>
        /// <remarks>
        /// <b>A cap, not a knob.</b> A stomach bloom of tens of thousands would make this file the
        /// largest thing a run writes — one row per eater per sample against
        /// <c>lineage.jsonl</c>'s one row per creature ever — and the reading the instrument
        /// exists for (where were they, what did they see, what did they earn) is served by two
        /// thousand of them as well as by all of them. It is deliberately not a
        /// <see cref="RunConfig"/> tunable: it changes nothing about the world, so it has no
        /// business in <c>configHash</c>.
        /// </remarks>
        public const int AbsorptiveLogRowCap = 2000;

        /// <summary>
        /// Buffers one creature's final <see cref="AbsorptiveSample"/> — called from the death
        /// path in <c>Metabolise</c>, before anything is zeroed.
        /// </summary>
        private void BufferAbsorptiveDeath(Organism creature)
        {
            if (_absorptiveDeaths.Count >= AbsorptiveLogRowCap)
            {
                _absorptiveDeathsDropped++;
                return;
            }

            _absorptiveDeaths.Add(AbsorptiveSample.For(creature, ElapsedSeconds, dead: true));
        }

        /// <summary>
        /// Living creatures whose developed phenotype carries at least one photosynthetic part —
        /// the producers, counted the way the eaters already are.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The report could count the stomachs and not the leaves</b> (the Sol/GPT review of
        /// 2026-09-03, finding 2). Every trophic reading this project takes is a ratio with
        /// producers in the denominator — how many eaters a standing crop of producers supports,
        /// whether a bloom is the crop growing or the grazers failing — and the denominator was
        /// never written down. <c>alive</c> is not it: a world can be all leaves, all stomachs or
        /// a mixture, and those three read identically in every column the report had.
        /// </para>
        /// <para>
        /// <b>Phenotype, not genome.</b> <see cref="Organism.HasPhotosyntheticTissue"/> is set
        /// once, at <c>Admit</c>, from the parts a body actually developed — so a genome carrying
        /// a photosynthetic node whose subtree was pruned below <c>minPartVolume</c> is counted
        /// here as what it grew into rather than what it encodes. That is the exact gap
        /// logbook/0048's dissection found between <c>snapshots/</c> and the living population.
        /// </para>
        /// <para>
        /// <b>Pure instrumentation</b>, like <see cref="CollectAbsorptiveLog"/>: one pass over
        /// <c>_living</c> reading a flag, no <see cref="Rng"/>, no clock, no economy state. A
        /// world whose producers are counted every sample and one where this is never called take
        /// bit-identical trajectories.
        /// </para>
        /// </remarks>
        public int CountPhotosynthetic()
        {
            int count = 0;
            for (int i = 0; i < _living.Count; i++)
            {
                if (_living[i].HasPhotosyntheticTissue) count++;
            }

            return count;
        }

        /// <summary>
        /// Appends one row per living creature with absorptive tissue, then every death row
        /// buffered since the last call, and returns how many rows were left out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The living are enumerated, the dead are drained</b>, and the asymmetry is the whole
        /// shape of the instrument: a living creature can be re-read at the next sample, and a
        /// dead one has exactly one moment at which its terminal budget exists. Meant to be called
        /// once per report row, beside <see cref="DrainLineageEvents"/> and
        /// <see cref="TakeDead"/>.
        /// </para>
        /// <para>
        /// <b>The first <see cref="AbsorptiveLogRowCap"/> by id.</b> <c>_living</c> is appended to
        /// and removed from in place and ids come from a monotonic counter, so list order is id
        /// order and taking the head of the list is taking the oldest eaters — the ones with a
        /// history worth reading — rather than an arbitrary slice.
        /// </para>
        /// <para>
        /// <b>Pure instrumentation.</b> Reads world state and empties one buffer; touches no
        /// <see cref="Rng"/>, no clock and no economy state, so a world whose log is collected
        /// every sample and one where this is never called take bit-identical trajectories.
        /// </para>
        /// </remarks>
        /// <param name="into">
        /// Rows are appended; the list is not cleared first. A creature born since the last
        /// metabolic step has no ledger yet and is left out rather than written as zeros — see the
        /// comment on the skip.
        /// </param>
        /// <returns>
        /// Living creatures past the cap, plus death rows dropped because the buffer was full. Zero
        /// in every run that has never had <see cref="AbsorptiveLogRowCap"/> eaters at once.
        /// </returns>
        public int CollectAbsorptiveLog(List<AbsorptiveSample> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            int written = 0;
            int truncated = _absorptiveDeathsDropped;
            _absorptiveDeathsDropped = 0;

            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                if (!creature.HasAbsorptiveTissue) continue;

                // Born since the last metabolic step, so it has no ledger and no density yet.
                // Skipped rather than written as zeros: 0 J/m³ is a real density and 0 W is a
                // real budget, and a row of them would be indistinguishable from a starving
                // creature in empty water — the exact trap `flt m`'s em-dash was added after. It
                // appears at the next sample, one step old, with everything real.
                if (creature.LastStepSeconds <= 0f) continue;

                if (written >= AbsorptiveLogRowCap)
                {
                    truncated++;
                    continue;
                }

                into.Add(AbsorptiveSample.For(creature, ElapsedSeconds, dead: false));
                written++;
            }

            for (int i = 0; i < _absorptiveDeaths.Count; i++) into.Add(_absorptiveDeaths[i]);
            _absorptiveDeaths.Clear();

            return truncated;
        }

        /// <summary>
        /// Assigns <see cref="Organism.SpeciesId"/> at birth — D057. The only place this project
        /// writes that property; nothing else may read it but a report (Organism's own remarks).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Three cases, in the order D057 states them.</b> The threshold off is the fast path
        /// its own doc comment promises — species 0, no registry touched, no distance computed.
        /// A floor founder has no lineage to measure from and simply founds its own. Everyone
        /// else is compared once against their parent's species' founding genome: within θ,
        /// inherit; past it, found — the child's own genome becomes the new reference, exactly as
        /// D057 specifies.
        /// </para>
        /// <para>
        /// Nothing here touches <see cref="Rng"/> or reads <see cref="ElapsedSeconds"/> for
        /// anything but a founding timestamp, so it changes no draw any other system depends on —
        /// the whole reason D057 can be pure instrumentation rather than a second thing to keep in
        /// sync with the mutation stream.
        /// </para>
        /// </remarks>
        private void AssignSpecies(Organism creature, Organism parent)
        {
            if (Config.SpeciesDriftThreshold <= 0f)
            {
                creature.SpeciesId = 0;
                return;
            }

            if (parent == null)
            {
                FoundSpecies(creature);
                return;
            }

            if (!_species.TryGetValue(parent.SpeciesId, out SpeciesFounder founder))
            {
                // Every species that can be read while the threshold is on was itself founded by
                // this method, so a miss here is a bookkeeping bug — a creature carrying a species
                // id nothing registered — rather than a data condition callers should absorb.
                throw new InvalidOperationException(
                    $"Creature {parent.Id} carries species {parent.SpeciesId}, which has no " +
                    "founder on record.");
            }

            float distance = SpeciesDistance.Between(
                creature.Genome, founder.Genome,
                Config.SpeciesCellTypeWeight, Config.SpeciesTopologyWeight,
                Config.SpeciesParameterWeight, Config.SpeciesBrainWeight);

            if (distance > Config.SpeciesDriftThreshold)
            {
                FoundSpecies(creature);
            }
            else
            {
                creature.SpeciesId = parent.SpeciesId;
            }
        }

        private void FoundSpecies(Organism creature)
        {
            uint id = _nextSpeciesId++;
            creature.SpeciesId = id;
            _species[id] = new SpeciesFounder(creature.Genome, ElapsedSeconds);
        }

        /// <summary>Creatures that have died, oldest first. Cleared by <see cref="TakeDead"/>.</summary>
        public IReadOnlyList<Organism> Dead => _dead;

        /// <summary>Hands over the dead and forgets them, so a long run does not grow without bound.</summary>
        public List<Organism> TakeDead()
        {
            var taken = new List<Organism>(_dead);
            _dead.Clear();
            return taken;
        }
    }
}
