using System;

namespace Evosim.Core
{
    /// <summary>Which kind of <see cref="LineageEvent"/> this is.</summary>
    public enum LineageEventKind
    {
        Birth = 0,
        Death = 1,
    }

    /// <summary>
    /// One birth or one death, queued by <see cref="World"/> for a harness to drain — the
    /// pre-round-8 instrument LITERATURE-REVIEW.md §9 item 9 asks for: per-creature birth/death
    /// rows to compute consumer generation time, boom-bust period and lineage persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A queue, not a file write.</b> §6.1 forbids <c>UnityEngine</c> in this assembly for the
    /// same reason it has no dependencies at all — Evosim.Core's tests run in under a second
    /// because nothing here touches disk. So <see cref="World"/> only records what happened;
    /// <c>EvolutionRun.cs</c> (the harness) drains the queue after each report row and appends it
    /// to <c>lineage.jsonl</c> through the <c>JsonlWriter</c> that already exists for it.
    /// </para>
    /// <para>
    /// <b>Pure instrumentation, always on, no knob.</b> Recording an event reads world state — it
    /// never writes any, consults <see cref="Rng"/>, or influences a branch anything else takes —
    /// so a world stepped with the queue drained every step and one where
    /// <see cref="World.DrainLineageEvents"/> is never called once produce bit-identical
    /// trajectories in everything but this list's own contents. There is nothing here for a
    /// config hash to disagree about.
    /// </para>
    /// <para>
    /// One struct rather than two event types sharing an interface: a birth and a death queued
    /// together in arrival order is what a reader needs (did this creature die before or after
    /// that one was born), and a discriminated union in a single <c>readonly struct</c> gets that
    /// ordering for free while costing one allocation per event rather than a boxed value plus a
    /// type tag. The birth-only and death-only fields sit unused on the other kind — a handful of
    /// bytes, not worth a second queue to keep in step with this one.
    /// </para>
    /// </remarks>
    public readonly struct LineageEvent
    {
        public LineageEventKind Kind { get; }
        public double ElapsedSeconds { get; }
        public long Id { get; }

        /// <summary>Birth only. -1 for a floor spawn or an inoculant — neither has a parent.</summary>
        public long ParentId { get; }

        /// <summary>Birth only — how the creature entered the population, D021/D060.</summary>
        public BirthKind BirthKind { get; }

        /// <summary>Birth only — reproduction events since the founder, <see cref="Organism.GenerationDepth"/>.</summary>
        public int GenerationDepth { get; }

        /// <summary>Birth only — D057's clade id. 0 whenever species accounting is off.</summary>
        public uint SpeciesId { get; }

        /// <summary>Birth only — whether any part of the developed body is <see cref="CellTypeIds.Absorptive"/>.</summary>
        public bool HasAbsorptive { get; }

        /// <summary>Birth only — whether the developed body has any actuated joint (<c>Phenotype.TotalDof &gt; 0</c>).</summary>
        public bool HasJoint { get; }

        /// <summary>Death only — why the creature left the population.</summary>
        public DeathCause Cause { get; }

        private LineageEvent(
            LineageEventKind kind, double elapsedSeconds, long id, long parentId,
            BirthKind birthKind, int generationDepth, uint speciesId,
            bool hasAbsorptive, bool hasJoint, DeathCause cause)
        {
            Kind = kind;
            ElapsedSeconds = elapsedSeconds;
            Id = id;
            ParentId = parentId;
            BirthKind = birthKind;
            GenerationDepth = generationDepth;
            SpeciesId = speciesId;
            HasAbsorptive = hasAbsorptive;
            HasJoint = hasJoint;
            Cause = cause;
        }

        public static LineageEvent Birth(
            double elapsedSeconds, long id, long parentId, BirthKind birthKind,
            int generationDepth, uint speciesId, bool hasAbsorptive, bool hasJoint) =>
            new LineageEvent(
                LineageEventKind.Birth, elapsedSeconds, id, parentId, birthKind, generationDepth,
                speciesId, hasAbsorptive, hasJoint, default);

        public static LineageEvent Death(double elapsedSeconds, long id, DeathCause cause) =>
            new LineageEvent(
                LineageEventKind.Death, elapsedSeconds, id, parentId: -1, birthKind: default,
                generationDepth: 0, speciesId: 0, hasAbsorptive: false, hasJoint: false, cause);

        /// <summary>One-letter code for <see cref="BirthKind"/> — "f" floor, "r" reproduction, "i" inoculation.</summary>
        private static string Code(BirthKind kind)
        {
            switch (kind)
            {
                case Evosim.Core.BirthKind.Floor: return "f";
                case Evosim.Core.BirthKind.Reproduction: return "r";
                case Evosim.Core.BirthKind.Inoculation: return "i";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind), kind, "New BirthKind, new lineage.jsonl code — add one " +
                        "rather than let a birth event fall through unlabelled.");
            }
        }

        /// <summary>Short code for <see cref="Cause"/>. Currently one value — Organism.cs's own remark.</summary>
        private static string Code(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Starved: return "starved";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(cause), cause, "New DeathCause, new lineage.jsonl code — add one " +
                        "rather than let a death event fall through unlabelled.");
            }
        }

        /// <summary>
        /// One line of <c>lineage.jsonl</c>. Compact: one row must be one line (§9), and short —
        /// the working estimate is 40,000 births an hour, so a genome-sized row would defeat the
        /// point of a row that is not a genome.
        /// </summary>
        public string ToJson()
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();

            if (Kind == LineageEventKind.Birth)
            {
                w.Field("e", "b")
                    .Field("t", ElapsedSeconds)
                    .Field("id", Id)
                    .Field("p", ParentId)
                    .Field("k", Code(BirthKind))
                    .Field("g", GenerationDepth)
                    .Field("s", (long)SpeciesId)
                    .Field("abs", HasAbsorptive ? 1 : 0)
                    .Field("jnt", HasJoint ? 1 : 0);
            }
            else
            {
                w.Field("e", "d")
                    .Field("t", ElapsedSeconds)
                    .Field("id", Id)
                    .Field("c", Code(Cause));
            }

            w.EndObject();
            return w.ToString();
        }
    }
}
