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
    /// needs a solver. The mechanical work term is the only physical quantity in §5A.2's ledger,
    /// and it is supplied by the caller — zero for anything that is not actuating, which is every
    /// plant and every founder without a link. So the calibration question §5A.2 calls the knob
    /// that decides everything can be swept in milliseconds instead of stepped through PhysX at
    /// 6.4 ms per step (§5A.9). Milestone 4 hands the real work figures in; the arithmetic does
    /// not change.
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

        private long _nextId;
        private ulong _nextSeed;

        public RunConfig Config { get; }
        public LightModel Light { get; }

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
        /// Energy held by creatures that starved, waiting to become nutrient. Always zero for now.
        /// </summary>
        /// <remarks>
        /// §5A.6 returns tissue to the nutrient pool on death, and §5A.0b's argument that the
        /// doomed half of generation zero <i>is</i> the primordial soup depends on it. The pool
        /// does not exist yet (Phase 2), so this is carried as an explicit named quantity rather
        /// than quietly dropped: a creature starves at exactly zero energy, so the tissue owed to
        /// the world is its body, not its reserve, and pretending otherwise would leave the §5A.2
        /// audit closing for the wrong reason.
        /// </remarks>
        public double UnrecycledTissueJoules { get; private set; }

        public World(RunConfig config, LightModel light = null, ulong seed = 1)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Light = light ?? new LightModel();
            Field = new LightField(Light, config.WorldAreaSquareMetres, config.LightLayerMetres);
            _nextSeed = seed;
        }

        /// <summary>Advances the world by one step.</summary>
        /// <param name="seconds">Step length. Large steps are fine — this is not a solver.</param>
        public void Step(float seconds)
        {
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));

            ElapsedSeconds += seconds;
            SecondsSinceFloorFired += seconds;

            Metabolise(seconds);
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
        /// <b>Two passes, because light is finite and shared</b> (§5A.2b). Every creature's shadow
        /// must be known before anyone's income can be, so the field is filled first and solved,
        /// and only then is anybody paid. A single pass would give whoever the list happened to
        /// walk first the undiminished sun, making income depend on iteration order — which is
        /// the kind of fault that produces a perfectly plausible number.
        /// </remarks>
        private void Metabolise(float seconds)
        {
            Field.Clear();
            for (int i = 0; i < _living.Count; i++)
            {
                Organism creature = _living[i];
                Field.Contribute(creature.HeightY, creature.Phenotype.TotalLitArea);
            }
            Field.Solve();

            for (int i = _living.Count - 1; i >= 0; i--)
            {
                Organism creature = _living[i];
                creature.Age += seconds;

                EnergyLedger ledger = Metabolism.StepAt(
                    creature.Phenotype, Config, Field.IrradianceAt(creature.HeightY),
                    nutrientDensity: 0f, workJoules: 0f, seconds: seconds);

                creature.Energy += ledger.Net;
                creature.Lifetime += ledger;

                EnergyIn += ledger.Income;
                EnergyOut += ledger.Expenditure;

                if (creature.Energy > 0f) continue;

                // Death at exactly zero, not below. A creature carrying negative energy would be
                // a debt the world has no way to settle, and the §5A.2 audit would never close.
                EnergyOut += creature.Energy;
                creature.Energy = 0f;

                UnrecycledTissueJoules += 0.0;

                _living.RemoveAt(i);
                _dead.Add(creature);
                Deaths++;
            }
        }

        private void Reproduce()
        {
            // Collected first and appended after, so an offspring cannot itself reproduce on the
            // step it was born — which it could if the list were grown while being walked, and
            // which would make brood size compound within a single step.
            _born.Clear();

            for (int i = 0; i < _living.Count; i++)
            {
                Organism parent = _living[i];

                float cost = parent.ReproductionThreshold(Config.PerOffspringOverheadJoules);
                if (cost <= 0f || parent.Energy < cost) continue;

                parent.Energy -= cost;

                // The overhead is spent, not transferred: it is what makes brood size a trait
                // selection can act on at all (§5A.6). Without it, one brood of four and four
                // broods of one are indistinguishable and brood size selects for nothing.
                EnergyOut +=
                    parent.Genome.Reproduction.BroodSize * Config.PerOffspringOverheadJoules;

                for (int n = 0; n < parent.Genome.Reproduction.BroodSize; n++)
                {
                    Organism child = Conceive(parent);
                    if (child != null) _born.Add(child);
                }
            }

            for (int i = 0; i < _born.Count; i++)
            {
                _living.Add(_born[i]);
                Births++;
            }
        }

        private Organism Conceive(Organism parent)
        {
            ulong seed = _nextSeed++;

            Genome child = Mutator.Mutate(
                parent.Genome, new Rng(seed), Config.Mutation, Config.CellTypes);

            return Admit(
                child, BirthKind.Reproduction, seed, parent.Id, parent.GenerationDepth + 1,
                parent.Genome.Reproduction.OffspringEndowment, parent.HeightY);
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
                ulong seed = _nextSeed++;
                var rng = new Rng(seed);

                Genome genome = GenomeFactory.Founder(rng, Config.Genome);

                // Placed through the lit zone rather than at the surface. Starting everything at
                // depth zero would hand generation zero the best light in the world and make the
                // §5A.2 calibration read as more generous than it is.
                float height = -rng.Range(0f, Config.FounderDepthSpread);

                Organism founder = Admit(
                    genome, BirthKind.Floor, seed, parentId: -1, generationDepth: 0,
                    energy: Config.FounderEnergyJoules, heightY: height);

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
            Genome genome, BirthKind kind, ulong seed, long parentId, int generationDepth,
            float energy, float heightY)
        {
            Phenotype phenotype = Developer.Develop(
                genome, Config.Development, null, Config.Shapes);

            if (phenotype.PartCount == 0)
            {
                Stillbirths++;

                // The energy still has to balance. A floor spawn's endowment was never created,
                // so nothing is owed; an offspring's was already deducted from its parent, so it
                // leaves the world here and must be recorded as leaving.
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
                HeightY = heightY,
                StandingWatts = Metabolism.StandingWatts(phenotype, Config),
            };

            // Endowment is transferred from the parent and floor energy is created, so only the
            // second is income the world has to account for. Conflating them would let a
            // population manufacture energy by breeding.
            if (kind == BirthKind.Floor) EnergyIn += energy;

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
