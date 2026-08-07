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
        public float HeightY { get; internal set; }

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
        /// What <see cref="SensorChannel.Energy"/> reports: seconds of life left at the current
        /// burn rate — §4.4.
        /// </summary>
        public float SecondsOfReserve =>
            StandingWatts > 1e-9f ? Energy / StandingWatts : float.PositiveInfinity;

        /// <summary>Joules this creature must hold before it can reproduce — §5A.6.</summary>
        /// <remarks>
        /// Derived from the creature's own evolved traits rather than configured, so a lineage
        /// that evolves a larger brood waits longer for it automatically and there is no separate
        /// constant to keep in sync.
        /// </remarks>
        public float ReproductionThreshold(float perOffspringOverheadJoules) =>
            Genome.Reproduction.CostJoules(perOffspringOverheadJoules);

        public override string ToString() =>
            $"#{Id} gen {GenerationDepth}, {Energy:0.#} J, {Age:0.#} s, {Phenotype.PartCount} parts";
    }

    /// <summary>Why a creature left the population — §5A.6, and the lineage record in §9.</summary>
    public enum DeathCause
    {
        /// <summary>Ran out of energy. The only cause the design has.</summary>
        Starved = 0,
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
    }
}
