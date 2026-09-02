using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The world exceeded its population ceiling — DESIGN.md §5A.7, D021.
    /// </summary>
    /// <remarks>
    /// Its own type so a sweep harness can catch it and record "this configuration exploded" as
    /// a result rather than as a crash. A runaway is a measurement: it locates one end of the
    /// transition in §5A.6b just as precisely as extinction locates the other.
    /// </remarks>
    public sealed class PopulationRunawayException : Exception
    {
        public int Population { get; }
        public double ElapsedSeconds { get; }

        public PopulationRunawayException(string message, int population, double elapsedSeconds)
            : base(message)
        {
            Population = population;
            ElapsedSeconds = elapsedSeconds;
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
        /// Births and deaths since the last <see cref="DrainLineageEvents"/> — pure
        /// instrumentation for <c>lineage.jsonl</c> (see <see cref="LineageEvent"/>). Swapped out
        /// rather than copied-and-cleared on drain, so a report interval with nothing to report
        /// costs nothing but reading a reference.
        /// </summary>
        private List<LineageEvent> _lineageEvents = new List<LineageEvent>();

        /// <summary>This step's ledgers, parallel to <c>_living</c>. Reused, never reallocated.</summary>
        private readonly List<EnergyLedger> _ledgers = new List<EnergyLedger>();

        private long _nextId;
        /// <summary>
        /// Counter behind every per-creature seed. Mixed with <see cref="Seed"/> rather than used
        /// raw — see <see cref="Rng.SeedFor"/> for why consecutive seeds are not independent runs.
        /// </summary>
        private ulong _nextIndex;

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
        public NutrientField Nutrients { get; }

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
        public NutrientField Matter { get; }

        /// <summary>Total matter in the world, free and locked up. Conserved — D048.</summary>
        public double StandingMatter => Matter.TotalJoules + MatterInBodies;

        /// <summary>Matter locked up in living tissue, awaiting its owner's death.</summary>
        public double MatterInBodies { get; private set; }

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

        /// <summary>Simulated seconds since the world began.</summary>
        public double ElapsedSeconds { get; private set; }

        public IReadOnlyList<Organism> Living => _living;

        /// <summary>Every species ever founded, oldest first by id — D057. See <see cref="SpeciesFounder"/>.</summary>
        public IReadOnlyDictionary<uint, SpeciesFounder> Species => _species;

        /// <summary>Creatures ever created by the floor, and ever born to a parent — D021.</summary>
        public long FloorSpawns { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }

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
                return sum;
            }
        }

        /// <summary>How far §5A.2's books are from balancing, in joules. Should be ~0.</summary>
        public double AuditResidual => EnergyIn - EnergyOut - StandingJoules;

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

            Field = new LightField(
                Light, config.WorldAreaSquareMetres, config.LightLayerMetres,
                patchCount, config.PerPatchShading > 0f);

            Nutrients = new NutrientField(
                config.WorldAreaSquareMetres, config.LightLayerMetres,
                config.NutrientSinkMetresPerSecond, config.WorldDepthMetres,
                config.FloorRefugeMetres, config.RefugeEdibleFraction, patchCount);

            // No refuge: nobody grazes matter, it is drawn at conception rather than eaten — D055.
            Matter = new NutrientField(
                config.WorldAreaSquareMetres, config.LightLayerMetres,
                config.MatterSinkMetresPerSecond, config.WorldDepthMetres,
                refugeMetres: 0f, refugeEdibleFraction: 0f, patchCount: patchCount);

            // Seeded uniformly — across every patch as well as every layer, D061 — and never
            // created again. Everything after this is redistribution: reproduction takes it out
            // of a cell, death puts it back into one. Deposit's 3-arg (patch-explicit) overload
            // is called directly rather than the pre-D061 one, so this loop needs no guard of its
            // own: it is correct at K=1 (one patch, same total deposited as before D061 existed)
            // and at K>1 alike.
            if (config.InitialMatterPerCubicMetre > 0f)
            {
                float perCell = config.InitialMatterPerCubicMetre * Matter.LayerVolume;
                for (int i = 0; i < Matter.LayerCount; i++)
                {
                    float depth = -((i + 0.5f) * Matter.LayerMetres);
                    for (int patch = 0; patch < patchCount; patch++)
                    {
                        Matter.Deposit(depth, perCell, patch);
                    }
                }
            }

            Seed = seed;
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

            if (float.IsNaN(heightY) || float.IsInfinity(heightY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(heightY), heightY,
                    $"Creature {creature.Id} has a non-finite height, so the solver has already " +
                    "diverged and every income derived from depth would be meaningless.");
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

        /// <summary>Advances the world by one step.</summary>
        /// <param name="seconds">Step length. Large steps are fine — this is not a solver.</param>
        public void Step(float seconds)
        {
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));

            ElapsedSeconds += seconds;
            SecondsSinceFloorFired += seconds;

            // Before anything reads the light, and from the absolute clock rather than a delta —
            // a sun advanced by accumulating steps drifts out of phase with the world that is
            // paying for it, and would present as a slow trend nobody chose (§5A.4).
            Field.Advance(ElapsedSeconds);

            Metabolise(seconds);

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
            Matter.Mix(seconds, Config.MatterMixingDiffusivity, Config.HorizontalMixingDiffusivity);

            // D066. Carried after it is stirred, in the same step and against the same clock the
            // bodies feel — diffusion is now the residual and advection the transport. A no-op
            // unless CurrentField.AdvectFields is on, so every run before D066 is untouched. The
            // patch width is the field's own, sqrt(area / K), which is what the horizontal Mix
            // pass above already diffuses across: one geometry, not two.
            Nutrients.Advect(Config.Current, ElapsedSeconds, seconds, Nutrients.PatchWidthMetres);
            Matter.Advect(Config.Current, ElapsedSeconds, seconds, Matter.PatchWidthMetres);

            Reproduce();
            EnforceFloor();
            EnforceCeiling();
        }

        /// <remarks>
        /// §5A.7's photosynthetic mat, caught rather than culled — D021. Every step past the
        /// ceiling costs more than the last, so a loop that only noticed would still be a loop
        /// that never returned; and culling to fit a budget would be selection performed by us,
        /// hiding a calibration failure behind a population number we chose.
        /// </remarks>
        private void EnforceCeiling()
        {
            if (_living.Count <= Config.MaximumPopulation) return;

            throw new PopulationRunawayException(
                $"Population reached {_living.Count}, above the ceiling of " +
                $"{Config.MaximumPopulation}, at t={ElapsedSeconds:0.#} s after {Births} births. " +
                "This is §5A.7's photosynthetic mat: light is covering upkeep, so nothing has to " +
                "do anything and every creature can afford to breed. The ratio in §5A.2 is too " +
                "generous — lower the surface irradiance or raise cell upkeep. It is not culled, " +
                "because culling to fit a compute budget is selection performed by us and would " +
                "hide this behind a population number we chose.",
                _living.Count, ElapsedSeconds);
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

                EnergyLedger ledger = Metabolism.StepAt(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch),
                    Nutrients.EdibleDensityAt(creature.HeightY, creature.Patch),
                    creature.PendingWorkJoules, seconds, creature.Age);

                _ledgers[i] = ledger;
                Nutrients.Demand(creature.HeightY, ledger.PoolDrawn, creature.Patch);
            }

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];

                // Read before it advances, because the recompute below has to price the same
                // step the loop above priced. Ageing a creature mid-step would make the short-
                // larder branch a different creature from the full-larder one.
                float age = creature.Age;
                creature.Age += seconds;

                float share = Nutrients.ShareAt(creature.HeightY, creature.Patch);
                EnergyLedger ledger = _ledgers[i];

                // Recomputed only when the larder is short. Scaling the stored ledger instead
                // would assume intake is linear in density, which it is for a filter feeder and
                // is not for anything with a bite rate that saturates.
                if (share < 1f)
                {
                    // The same work, not more: this replaces the ledger rather than adding to it.
                    ledger = Metabolism.StepAt(
                        creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY, creature.Patch),
                        Nutrients.EdibleDensityAt(creature.HeightY, creature.Patch) * share,
                        creature.PendingWorkJoules, seconds, age);
                }

                if (ledger.PoolDrawn > 0f) Nutrients.Take(creature.HeightY, ledger.PoolDrawn, creature.Patch);

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

                // Only sunlight is new energy. What was eaten was already in the world — and what
                // was torn up and not eaten has left it, which is why a food chain shortens.
                EnergyIn += ledger.LightIncome;
                EnergyOut += ledger.Expenditure + ledger.Wasted;

                // Turnover — D052. A living body gives back a fraction of what it holds, in
                // proportion to what it spent staying alive this step, at its own depth rather
                // than only at death. Capped at what is still locked: a body cannot excrete
                // matter it does not have. LockedMatter is already 0 for a floor founder, so the
                // cap alone keeps founders from excreting matter they never held.
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
                        Matter.Deposit(creature.HeightY, excreted, creature.Patch);
                        creature.LockedMatter -= excreted;
                        MatterInBodies -= excreted;
                        ExcretedTotal += excreted;
                    }
                }

                if (creature.Energy > 0f) continue;

                // Death at exactly zero, not below. A creature carrying negative energy would be
                // a debt the world has no way to settle, and the §5A.2 audit would never close.
                EnergyOut += creature.Energy;
                creature.Energy = 0f;

                // The body becomes detritus where it died — §5A.2c. This is the whole reason
                // anything other than a plant can live, and the reason the doomed half of
                // generation zero is the world's first food rather than merely a waste of seeds.
                Nutrients.Deposit(creature.HeightY, creature.TissueJoules, creature.Patch);

                // Whatever matter is still locked returns to the layer the body died in, and
                // sinks from there — which is why the deep is rich and the surface is not.
                // LockedMatter (D052) is what remains after a lifetime of excretion, or the full
                // price paid at conception if the knob is off; either way it is already 0 for a
                // floor founder, which never paid and so never owes anything back.
                if (creature.LockedMatter > 0f)
                {
                    Matter.Deposit(creature.HeightY, creature.LockedMatter, creature.Patch);
                    MatterInBodies -= creature.LockedMatter;
                    creature.LockedMatter = 0f;
                }

                creature.TissueJoules = 0f;

                _living.RemoveAt(i);
                _dead.Add(creature);
                Deaths++;

                // §5A.6 kills at exactly zero energy and nothing else — Organism.cs's DeathCause
                // remark ("the only cause the design has") is the reason this reads one constant
                // rather than branching: senescence (D038) raises upkeep until this fires sooner,
                // it does not open a second way to die.
                _lineageEvents.Add(LineageEvent.Death(ElapsedSeconds, creature.Id, DeathCause.Starved));
            }
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
        private void Disperse()
        {
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
            CurrentField current = Config.Current;
            if (current == null || !current.Rolls || PatchCount <= 1 || current.Speed <= 0f) return;

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
        /// </remarks>
        private void Reproduce()
        {
            // Collected first and appended after, so an offspring cannot itself reproduce on the
            // step it was born — which it could if the list were grown while being walked, and
            // which would make brood size compound within a single step.
            _born.Clear();

            for (int i = 0; i < _living.Count; i++)
            {
                Organism parent = _living[i];

                float gate = parent.ReproductionThreshold(Config.PerOffspringOverheadJoules);
                if (gate <= 0f || parent.Energy < gate) continue;

                for (int n = 0; n < parent.Genome.Reproduction.BroodSize; n++)
                {
                    if (!Conceive(parent)) break;
                }
            }

            for (int i = 0; i < _born.Count; i++)
            {
                _living.Add(_born[i]);
                Births++;
            }
        }

        /// <summary>
        /// Makes one offspring if the parent can afford it. False means it could not, and the
        /// rest of the brood is abandoned.
        /// </summary>
        private bool Conceive(Organism parent)
        {
            // Before anything expensive. See CheapestPossibleChildMatter: if the parent's layer
            // cannot afford the smallest child that could exist, no mutation of this genome can
            // be afforded either, and building one to find that out is the dominant cost in a
            // matter-limited world.
            if ((Config.MatterPerTissueJoule > 0f || Config.MatterPerCreature > 0f) &&
                Matter.StockInLayer(Matter.LayerOf(parent.HeightY), parent.Patch) < CheapestPossibleChildMatter)
            {
                ConceptionsBlockedByMatter++;
                return false;
            }

            ulong seed = Rng.SeedFor(Seed, _nextIndex++);

            Genome childGenome = Mutator.Mutate(
                parent.Genome, new Rng(seed), Config.Mutation, Config.CellTypes, Config.Genome);

            Phenotype body = Developer.Develop(
                childGenome, Config.Development, null, Config.Shapes);

            float endowment = parent.Genome.Reproduction.OffspringEndowment;
            float tissue = Metabolism.TissueJoules(body, Config);
            float price = endowment + tissue + Config.PerOffspringOverheadJoules;

            if (parent.Energy < price) return false;

            // Energy is necessary and, from D048, no longer sufficient. Tissue is matter, and a
            // parent with sunlight to spare and nothing dissolved in the water around it does not
            // breed. Drawn from the parent's own layer, so success at a depth depletes that
            // depth — the negative feedback the world previously had nowhere at all.
            // D065 adds the fixed term. Two terms, one price: everything downstream — the stock
            // check, the Take, LockedMatter, and therefore excretion and the death deposit — sees
            // a single number and carries the fixed part without knowing it is there.
            float matterPrice = Config.MatterPerTissueJoule * tissue + Config.MatterPerCreature;

            if (matterPrice > 0f)
            {
                // Checked before taking, not by taking. NutrientField.Take is a partial-take API:
                // it removes min(asked, stock) and returns that. Calling it and bailing when the
                // return is short removes the partial amount and then drops it on the floor, which
                // leaks matter on every blocked conception — 132 units of 24,000 in a 400 s test,
                // and it leaks fastest exactly when matter is scarce enough to matter.
                if (Matter.StockInLayer(Matter.LayerOf(parent.HeightY), parent.Patch) < matterPrice)
                {
                    ConceptionsBlockedByMatter++;
                    return false;
                }

                Matter.Take(parent.HeightY, matterPrice, parent.Patch);
                MatterInBodies += matterPrice;
            }

            parent.Energy -= price;

            // Endowment and tissue are transferred and stay in the world; the overhead is burned.
            // It is what makes brood size a trait selection can act on at all (§5A.6) — without
            // it, one brood of four and four broods of one are indistinguishable.
            EnergyOut += Config.PerOffspringOverheadJoules;

            Organism child = Admit(
                childGenome, body, BirthKind.Reproduction, seed, parent.Id,
                parent.GenerationDepth + 1, endowment, tissue, parent.HeightY, parent,
                patch: parent.Patch);

            if (child != null)
            {
                // What the layer was just charged for this body — D052's starting balance, and
                // (with ExcretionPerJoule at 0) the only value LockedMatter will ever hold, which
                // is exactly what death paid out before this decision existed.
                child.LockedMatter = matterPrice;
                _born.Add(child);
            }

            return true;
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

                Genome genome = GenomeFactory.Founder(rng, Config.Genome);

                // Placed through the lit zone rather than at the surface. Starting everything at
                // depth zero would hand generation zero the best light in the world and make the
                // §5A.2 calibration read as more generous than it is.
                float height = -rng.Range(0f, Config.FounderDepthSpread);

                // D061. A second, independent seed slot, drawn only when there is more than one
                // patch to land in — the CLAUDE.md guard: any new Rng draw on a path that runs at
                // K=1 breaks bit-identity, so this is skipped entirely rather than drawn and
                // discarded. A separate draw rather than one more call against `rng` above: reusing
                // it would make where a founder lands depend on how many draws GenomeFactory.Founder
                // happened to make, coupling two things D061 wants independent of each other.
                int patch = 0;
                if (PatchCount > 1)
                {
                    ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                    patch = new Rng(patchSeed).Range(PatchCount);
                }

                Phenotype body = Developer.Develop(
                    genome, Config.Development, null, Config.Shapes);

                Organism founder = Admit(
                    genome, body, BirthKind.Floor, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules,
                    tissue: Metabolism.TissueJoules(body, Config), heightY: height, parent: null,
                    patch: patch);

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
        /// <see cref="Organism.LockedMatter"/> (never paid, so it never owes anything back — same
        /// as a floor founder), generation depth 0 and no parent, so it founds its own species
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
        public void Inoculate(Genome genome, int count, float heightY)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            for (int i = 0; i < count; i++)
            {
                ulong seed = Rng.SeedFor(Seed, _nextIndex++);

                // D061. Same second-seed-slot pattern as EnforceFloor, for the same reason: drawn
                // only when there is more than one patch to land in, so a K=1 assay is untouched
                // and the D060 assay's own guarantees (same seed stream, replays identically) are
                // extended rather than disturbed.
                int patch = 0;
                if (PatchCount > 1)
                {
                    ulong patchSeed = Rng.SeedFor(Seed, _nextIndex++);
                    patch = new Rng(patchSeed).Range(PatchCount);
                }

                Phenotype body = Developer.Develop(genome, Config.Development, null, Config.Shapes);

                Organism creature = Admit(
                    genome, body, BirthKind.Inoculation, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules,
                    tissue: Metabolism.TissueJoules(body, Config), heightY: heightY, parent: null,
                    patch: patch);

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
        private Organism Admit(
            Genome genome, Phenotype phenotype, BirthKind kind, ulong seed, long parentId,
            int generationDepth, float energy, float tissue, float heightY, Organism parent,
            int patch)
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
                Energy = energy,
                TissueJoules = tissue,
                HeightY = heightY,
                BirthHeightY = heightY,
                Patch = patch,
                StandingWatts = Metabolism.StandingWatts(phenotype, Config),
            };

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
            // means.
            _lineageEvents.Add(LineageEvent.Birth(
                ElapsedSeconds, creature.Id, parentId, kind, generationDepth, creature.SpeciesId,
                HasAbsorptive(phenotype), phenotype.TotalDof > 0, patch));

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
