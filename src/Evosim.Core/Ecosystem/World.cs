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
    /// <b>The population floor is the only thing that creates a creature from nothing</b>
    /// (D021), including at t=0. There is no separate seeding path, so the mechanism that
    /// repopulates a collapsing world is the same one exercised on the very first step — tested
    /// continuously rather than once.
    /// </para>
    /// </remarks>
    public sealed class World
    {
        private readonly List<Organism> _living = new List<Organism>();
        private readonly List<Organism> _dead = new List<Organism>();
        private readonly List<Organism> _born = new List<Organism>();

        /// <summary>This step's ledgers, parallel to <c>_living</c>. Reused, never reallocated.</summary>
        private readonly List<EnergyLedger> _ledgers = new List<EnergyLedger>();

        private long _nextId;
        /// <summary>
        /// Counter behind every per-creature seed. Mixed with <see cref="Seed"/> rather than used
        /// raw — see <see cref="Rng.SeedFor"/> for why consecutive seeds are not independent runs.
        /// </summary>
        private ulong _nextIndex;

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

        /// <summary>Dead matter in the water, and what feeds on it — §5A.2c.</summary>
        public NutrientField Nutrients { get; }

        /// <summary>Simulated seconds since the world began.</summary>
        public double ElapsedSeconds { get; private set; }

        public IReadOnlyList<Organism> Living => _living;

        /// <summary>Creatures ever created by the floor, and ever born to a parent — D021.</summary>
        public long FloorSpawns { get; private set; }
        public long Births { get; private set; }
        public long Deaths { get; private set; }

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

            Field = new LightField(Light, config.WorldAreaSquareMetres, config.LightLayerMetres);
            Nutrients = new NutrientField(
                config.WorldAreaSquareMetres, config.LightLayerMetres,
                config.NutrientSinkMetresPerSecond, config.WorldDepthMetres);
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
            Nutrients.Settle(seconds);
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
                Field.Contribute(creature.HeightY, creature.Phenotype.TotalLitArea);
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
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY),
                    Nutrients.DensityAt(creature.HeightY),
                    creature.PendingWorkJoules, seconds);

                _ledgers[i] = ledger;
                Nutrients.Demand(creature.HeightY, ledger.PoolDrawn);
            }

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];
                creature.Age += seconds;

                float share = Nutrients.ShareAt(creature.HeightY);
                EnergyLedger ledger = _ledgers[i];

                // Recomputed only when the larder is short. Scaling the stored ledger instead
                // would assume intake is linear in density, which it is for a filter feeder and
                // is not for anything with a bite rate that saturates.
                if (share < 1f)
                {
                    // The same work, not more: this replaces the ledger rather than adding to it.
                    ledger = Metabolism.StepAt(
                        creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY),
                        Nutrients.DensityAt(creature.HeightY) * share,
                        creature.PendingWorkJoules, seconds);
                }

                if (ledger.PoolDrawn > 0f) Nutrients.Take(creature.HeightY, ledger.PoolDrawn);

                // Drained here and nowhere else. Both branches above priced the same joules, so
                // this is the one point at which they stop being owed.
                creature.PendingWorkJoules = 0f;

                creature.Energy += ledger.Net;
                creature.Lifetime += ledger;

                // Only sunlight is new energy. What was eaten was already in the world — and what
                // was torn up and not eaten has left it, which is why a food chain shortens.
                EnergyIn += ledger.LightIncome;
                EnergyOut += ledger.Expenditure + ledger.Wasted;

                if (creature.Energy > 0f) continue;

                // Death at exactly zero, not below. A creature carrying negative energy would be
                // a debt the world has no way to settle, and the §5A.2 audit would never close.
                EnergyOut += creature.Energy;
                creature.Energy = 0f;

                // The body becomes detritus where it died — §5A.2c. This is the whole reason
                // anything other than a plant can live, and the reason the doomed half of
                // generation zero is the world's first food rather than merely a waste of seeds.
                Nutrients.Deposit(creature.HeightY, creature.TissueJoules);
                creature.TissueJoules = 0f;

                _living.RemoveAt(i);
                _dead.Add(creature);
                Deaths++;
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
            ulong seed = Rng.SeedFor(Seed, _nextIndex++);

            Genome childGenome = Mutator.Mutate(
                parent.Genome, new Rng(seed), Config.Mutation, Config.CellTypes);

            Phenotype body = Developer.Develop(
                childGenome, Config.Development, null, Config.Shapes);

            float endowment = parent.Genome.Reproduction.OffspringEndowment;
            float tissue = Metabolism.TissueJoules(body, Config);
            float price = endowment + tissue + Config.PerOffspringOverheadJoules;

            if (parent.Energy < price) return false;

            parent.Energy -= price;

            // Endowment and tissue are transferred and stay in the world; the overhead is burned.
            // It is what makes brood size a trait selection can act on at all (§5A.6) — without
            // it, one brood of four and four broods of one are indistinguishable.
            EnergyOut += Config.PerOffspringOverheadJoules;

            Organism child = Admit(
                childGenome, body, BirthKind.Reproduction, seed, parent.Id,
                parent.GenerationDepth + 1, endowment, tissue, parent.HeightY);

            if (child != null) _born.Add(child);
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

                Phenotype body = Developer.Develop(
                    genome, Config.Development, null, Config.Shapes);

                Organism founder = Admit(
                    genome, body, BirthKind.Floor, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules,
                    tissue: Metabolism.TissueJoules(body, Config), heightY: height);

                // A stillborn founder is still an attempt, and counting it keeps the floor's
                // trickle a trickle. Not counting it would let a step retry until something
                // developed, which is the floor quietly selecting for viability.
                FloorSpawns++;
                if (founder != null) _living.Add(founder);
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
        private Organism Admit(
            Genome genome, Phenotype phenotype, BirthKind kind, ulong seed, long parentId,
            int generationDepth, float energy, float tissue, float heightY)
        {
            if (phenotype.PartCount == 0)
            {
                Stillbirths++;

                // The energy still has to balance. A floor spawn's endowment was never created,
                // so nothing is owed; an offspring's was already deducted from its parent, so it
                // leaves the world here and must be recorded as leaving. Its tissue is zero either
                // way — there is no body to have paid for.
                if (kind != BirthKind.Floor) EnergyOut += energy;

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
                StandingWatts = Metabolism.StandingWatts(phenotype, Config),
            };

            // Endowment and body are transferred from the parent, and a founder's are created out
            // of nothing, so only the second is income the world has to account for. Conflating
            // them would let a population manufacture energy by breeding.
            if (kind == BirthKind.Floor) EnergyIn += energy + tissue;

            return creature;
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
