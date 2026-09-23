using System;

namespace Evosim.Core
{
    /// <summary>Which kind of <see cref="LineageEvent"/> this is.</summary>
    public enum LineageEventKind
    {
        Birth = 0,
        Death = 1,

        /// <summary>
        /// One part taken off a living body by a bite — D106 item 1, written by
        /// <c>World.KillPart</c>. A kill of a root is a kill row <i>and</i> the
        /// <see cref="DeathCause.Eaten"/> death row it has always been, so a reader sees the event
        /// and the death; a kill of any other part leaves a living body and no death row at all,
        /// which is the case round 45's J3 needs and the one nothing recorded before this.
        /// </summary>
        Kill = 2,
    }

    /// <summary>
    /// Which of the two founder sources a <see cref="BirthKind.Floor"/> birth came from — D115,
    /// <c>logbook/specs/founding-trickle-spec.md</c> §1.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="BirthKind"/> rather than a new kind, because a trickle founder is a
    /// founder in every respect the record already reads by <c>k: "f"</c> (no parent, generation
    /// 0, its matter booked as influx) and the scorers that gate on <c>"f"</c> should keep
    /// reading it as one. What a read needs apart is which door it came through, and that is the
    /// row's <c>src</c> field.
    /// </remarks>
    public enum FounderSource
    {
        /// <summary>Not a founder: a birth to a parent, an inoculant, a death or a kill.</summary>
        None = 0,

        /// <summary>The population floor, to <see cref="RunConfig.MinimumPopulation"/> until it closes.</summary>
        Floor = 1,

        /// <summary>The founding trickle, after the floor has closed.</summary>
        Trickle = 2,

        /// <summary>
        /// The founding trickle's pool (D117): an exact copy of an evolved genome the launcher
        /// named, admitted in place of a lottery draw. A trickle founder in every count, and
        /// labelled apart on its row, which also carries the pool index.
        /// </summary>
        Pool = 3,
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

        /// <summary>
        /// Birth only — whether any part of the developed body is
        /// <see cref="CellTypeIds.Photosynthetic"/>, the same phenotype test that sets
        /// <see cref="Organism.HasPhotosyntheticTissue"/>. D063's producer clause asks for an
        /// inherited photosynthetic line, and a parent chain can only be walked from a flag the
        /// birth row carries.
        /// </summary>
        public bool HasPhotosynthetic { get; }

        /// <summary>
        /// Birth only — the horizontal cell the creature was born into, <see cref="Organism.Patch"/>
        /// — D061. Always 0 whenever <see cref="RunConfig.HorizontalPatches"/> is 1, and only
        /// meaningful above that; carried unconditionally because it is one small int and a
        /// column added only sometimes is worse than one that reads 0 when it does not apply.
        /// </summary>
        public int Patch { get; }

        /// <summary>
        /// Birth only — what share of its adult body this creature was born with,
        /// <see cref="Organism.BodyFraction"/> at birth. fable-propose-growth.md rule 9.
        /// </summary>
        /// <remarks>
        /// <b>The number the litter and the investment together decide, recorded where the
        /// decision landed.</b> Both dials can be read off a stored genome, but what a particular
        /// child was actually born at depends on its parent's body at that instant, and nothing
        /// else in the record carries a parent's tissue value at the moment it bred. 1 for
        /// anything born at full size.
        /// </remarks>
        public float BirthFraction { get; }

        /// <summary>Birth only — the genome's <see cref="Genome.AdultScale"/>, rule 9.</summary>
        /// <remarks>
        /// Carried on the row rather than left to a snapshot, because the question the dial exists
        /// for is whether a lineage's size drifts, and that is a question about every birth in a
        /// parent chain rather than about the survivors a snapshot happens to catch.
        /// </remarks>
        public float AdultScale { get; }

        /// <summary>
        /// Birth only — the genome's <see cref="ReproductionTraits.ReserveMargin"/>, in seconds
        /// of standing cost. D098 §3.
        /// </summary>
        /// <remarks>
        /// On the row for the same reason the adult scale is: whether a population grows more
        /// cautious is a question about every birth in a parent chain, and a snapshot only ever
        /// carries the survivors. It is also the one of the three that predicts who is about to
        /// die — a lineage walking its margin to zero is a lineage spending its way to the edge,
        /// and that is visible here a long time before it is visible in the population count.
        /// </remarks>
        public float ReserveMargin { get; }

        /// <summary>
        /// Birth only — how many nodes of the genome are
        /// <see cref="ModuleGrowth.Indeterminate"/>. D106 item 2, rule 8's <c>ind</c>.
        /// </summary>
        /// <remarks>
        /// <b>The one place the gene can be read per birth.</b> A snapshot carries the genome and
        /// therefore the gene, but a snapshot is the survivors at an instant; round 44 asks
        /// whether an indeterminate line founds and is kept, which is a question about every
        /// birth in a parent chain (logbook/0113's H1 and H2). 0 for every body in the record and
        /// for every founder, which is the point: a nonzero row is a mutation that happened.
        /// </remarks>
        public int IndeterminateNodes { get; }

        /// <summary>
        /// Birth only — whether any node of the genome carries <see cref="MorphNode.Attack"/>
        /// above zero. D106 item 3, rule 8's <c>atk</c>.
        /// </summary>
        /// <remarks>
        /// <b>Of the genome and not of the developed body</b>, unlike <see cref="HasAbsorptive"/>
        /// and its two neighbours, and the difference is deliberate. Round 45 asks when attack
        /// <i>appears in a lineage</i> and whether protection follows it (logbook's J4), which is a
        /// question about what is being inherited; a node whose part development pruned would read
        /// 0 on a body test and would hide exactly the generations in which the gene was being
        /// carried without being expressed. A flag and not a count, because one armed node is what
        /// makes a lineage armed.
        /// </remarks>
        public bool HasAttack { get; }

        /// <summary>
        /// Birth only — whether any node carries <see cref="MorphNode.Intake"/> above zero.
        /// See <see cref="HasAttack"/>. Nonzero on every consumer node from the founding lottery,
        /// which is the spec's rule 1.
        /// </summary>
        public bool HasIntake { get; }

        /// <summary>
        /// Birth only — whether any node carries <see cref="MorphNode.Protection"/> above zero.
        /// See <see cref="HasAttack"/>.
        /// </summary>
        public bool HasProtection { get; }

        /// <summary>
        /// Birth only — for a founder, whether the floor or the trickle spawned it (D115);
        /// <see cref="FounderSource.None"/> on every other row.
        /// </summary>
        public FounderSource Source { get; }

        /// <summary>
        /// Birth only — for a <see cref="FounderSource.Pool"/> founder, the index of the pool
        /// genome it is a copy of (D117, <c>pool/NN.json</c>); -1 on every other row.
        /// </summary>
        public int PoolIndex { get; }

        /// <summary>Death only — why the creature left the population.</summary>
        public DeathCause Cause { get; }

        /// <summary>
        /// Kill only — the body whose part did the damage that finished this one, or -1 when
        /// nothing in this step's contact list can be named for it.
        /// </summary>
        /// <remarks>
        /// <b>The last hand on the part, not the owner of the wound.</b> Health is a pool a crowd
        /// can drain from several sides over many steps and the row carries one id, so this is the
        /// attacker whose blow took the part to zero in the step it came off — which is what a
        /// reader can honestly ask of a record that keeps no per-blow ledger. It is -1 for the
        /// second and any later part a body loses in one step, because taking the first one
        /// rebuilds the body and the part indices the attribution was keyed on are gone with it.
        /// </remarks>
        public long AttackerId { get; }

        /// <summary>Kill only — whether the loss took the body, and not merely a limb.</summary>
        /// <remarks>
        /// True for a root, for the only part of a one-part body, and for the case where the
        /// developer pruned everything that was left: all three end in <see cref="Bury"/> with
        /// <see cref="DeathCause.Eaten"/> and a death row follows this one. False is the case
        /// nothing else in the record can show — a body that lost a limb and walked away.
        /// </remarks>
        public bool RootLost { get; }

        /// <summary>Kill only — parts the body lost, the bitten one and everything under it.</summary>
        public int PartsLost { get; }

        /// <summary>Kill only — the tissue that left the body with the part, in joules.</summary>
        public double TissueJoulesLost { get; }

        /// <summary>
        /// Kill only — the share of the one reserve that left with the part, in joules. With
        /// <see cref="TissueJoulesLost"/> it is exactly what the corpse (or the water) was given.
        /// </summary>
        public double ReserveJoulesLost { get; }

        /// <summary>
        /// Kill only — the index of the part that reached zero health, in the victim's body as it
        /// stood when the part came off; -1 on a row that cannot say. The kill row's <c>part</c>.
        /// </summary>
        /// <remarks>
        /// 0 is the root. The index is the one <see cref="Phenotype.Parts"/> used at the kill, and
        /// the rebuild that follows renumbers the survivors, so it is read against the body as it
        /// stood before the kill (a snapshot's <c>lostPaths</c> after it no longer lists it by
        /// this number). Round 46's K9b asks it.
        /// </remarks>
        public int PartIndex { get; }

        /// <summary>
        /// Kill only — the index of the attacker's part that dealt the finishing blow, in the
        /// attacker's body as it stood at the blow; -1 whenever <see cref="AttackerId"/> is -1.
        /// The kill row's <c>byPart</c>.
        /// </summary>
        /// <remarks>
        /// It is the contact list's own part for the attacker (<see cref="CreatureContact"/>'s
        /// <c>PartA</c> or <c>PartB</c>): the nearest part under the recorded contact, the
        /// touching link under contact per part. It is kept and dropped with the attacker's id in
        /// the same entry, so the two are always a pair.
        /// </remarks>
        public int AttackerPartIndex { get; }

        private LineageEvent(
            LineageEventKind kind, double elapsedSeconds, long id, long parentId,
            BirthKind birthKind, int generationDepth, uint speciesId,
            bool hasAbsorptive, bool hasJoint, bool hasPhotosynthetic, int patch,
            float birthFraction, float adultScale, float reserveMargin, int indeterminateNodes,
            bool hasAttack, bool hasIntake, bool hasProtection,
            DeathCause cause,
            long attackerId, bool rootLost, int partsLost,
            double tissueJoulesLost, double reserveJoulesLost,
            FounderSource source = FounderSource.None,
            int partIndex = -1, int attackerPartIndex = -1, int poolIndex = -1)
        {
            Source = source;
            PartIndex = partIndex;
            AttackerPartIndex = attackerPartIndex;
            PoolIndex = poolIndex;
            AttackerId = attackerId;
            RootLost = rootLost;
            PartsLost = partsLost;
            TissueJoulesLost = tissueJoulesLost;
            ReserveJoulesLost = reserveJoulesLost;
            IndeterminateNodes = indeterminateNodes;
            HasAttack = hasAttack;
            HasIntake = hasIntake;
            HasProtection = hasProtection;
            BirthFraction = birthFraction;
            AdultScale = adultScale;
            ReserveMargin = reserveMargin;
            Kind = kind;
            ElapsedSeconds = elapsedSeconds;
            Id = id;
            ParentId = parentId;
            BirthKind = birthKind;
            GenerationDepth = generationDepth;
            SpeciesId = speciesId;
            HasAbsorptive = hasAbsorptive;
            HasJoint = hasJoint;
            HasPhotosynthetic = hasPhotosynthetic;
            Patch = patch;
            Cause = cause;
        }

        public static LineageEvent Birth(
            double elapsedSeconds, long id, long parentId, BirthKind birthKind,
            int generationDepth, uint speciesId, bool hasAbsorptive, bool hasJoint,
            bool hasPhotosynthetic, int patch, float birthFraction, float adultScale,
            float reserveMargin, int indeterminateNodes,
            bool hasAttack, bool hasIntake, bool hasProtection,
            FounderSource source = FounderSource.None, int poolIndex = -1) =>
            new LineageEvent(
                LineageEventKind.Birth, elapsedSeconds, id, parentId, birthKind, generationDepth,
                speciesId, hasAbsorptive, hasJoint, hasPhotosynthetic, patch,
                birthFraction, adultScale, reserveMargin, indeterminateNodes,
                hasAttack, hasIntake, hasProtection, default,
                attackerId: -1, rootLost: false, partsLost: 0,
                tissueJoulesLost: 0d, reserveJoulesLost: 0d, source: source,
                poolIndex: source == FounderSource.Pool ? poolIndex : -1);

        public static LineageEvent Death(double elapsedSeconds, long id, DeathCause cause) =>
            new LineageEvent(
                LineageEventKind.Death, elapsedSeconds, id, parentId: -1, birthKind: default,
                generationDepth: 0, speciesId: 0, hasAbsorptive: false, hasJoint: false,
                hasPhotosynthetic: false, patch: 0, birthFraction: 0f, adultScale: 0f,
                reserveMargin: 0f, indeterminateNodes: 0,
                hasAttack: false, hasIntake: false, hasProtection: false, cause: cause,
                attackerId: -1, rootLost: false, partsLost: 0,
                tissueJoulesLost: 0d, reserveJoulesLost: 0d);

        /// <summary>
        /// One part off one body — D106 item 1. <paramref name="indeterminateNodes"/> is the
        /// victim's own <c>Organism.IndeterminateNodes</c>, carried here for the same reason the
        /// birth row carries it: whether the gene that regrows a part is what survives losing one
        /// is a question about the body that was bitten, and a snapshot only holds survivors.
        /// <paramref name="partIndex"/> and <paramref name="attackerPartIndex"/> are
        /// <see cref="PartIndex"/> and <see cref="AttackerPartIndex"/>, -1 where unknown.
        /// </summary>
        public static LineageEvent Kill(
            double elapsedSeconds, long victimId, long attackerId, bool rootLost, int partsLost,
            double tissueJoulesLost, double reserveJoulesLost, int indeterminateNodes,
            int partIndex = -1, int attackerPartIndex = -1) =>
            new LineageEvent(
                LineageEventKind.Kill, elapsedSeconds, victimId, parentId: -1, birthKind: default,
                generationDepth: 0, speciesId: 0, hasAbsorptive: false, hasJoint: false,
                hasPhotosynthetic: false, patch: 0, birthFraction: 0f, adultScale: 0f,
                reserveMargin: 0f, indeterminateNodes: indeterminateNodes,
                hasAttack: false, hasIntake: false, hasProtection: false, cause: default,
                attackerId: attackerId, rootLost: rootLost, partsLost: partsLost,
                tissueJoulesLost: tissueJoulesLost, reserveJoulesLost: reserveJoulesLost,
                partIndex: partIndex, attackerPartIndex: attackerPartIndex);

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

        /// <summary>A founder row's <c>src</c> — D115's two doors and D117's pool.</summary>
        private static string SourceCode(FounderSource source)
        {
            switch (source)
            {
                case FounderSource.Floor: return "floor";
                case FounderSource.Trickle: return "trickle";
                case FounderSource.Pool: return "pool";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source), source, "New FounderSource, new lineage.jsonl code — add " +
                        "one rather than let a founder fall through unlabelled.");
            }
        }

        /// <summary>
        /// Short code for <see cref="Cause"/>. Two values: the ecology's one, and the solver's.
        /// </summary>
        private static string Code(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Starved: return "starved";
                case DeathCause.Diverged: return "diverged";
                case DeathCause.Eaten: return "eaten";
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
                    .Field("jnt", HasJoint ? 1 : 0)
                    .Field("pho", HasPhotosynthetic ? 1 : 0)
                    .Field("pt", Patch)
                    .Field("bf", BirthFraction)
                    .Field("as", AdultScale)
                    .Field("rm", ReserveMargin)
                    .Field("ind", IndeterminateNodes)

                    // D106 item 3, rule 8's three. Appended at the end for the reason every field
                    // here is: a reader written against an older row keeps working, and a row
                    // without them is a birth recorded before the mouth existed.
                    .Field("atk", HasAttack ? 1 : 0)
                    .Field("ink", HasIntake ? 1 : 0)
                    .Field("prt", HasProtection ? 1 : 0);

                // D115. On a founder's row only, and on every founder's row from this build, so
                // a read separates the floor's founders from the trickle's without a config in
                // hand; appended at the end for the reason the three above are.
                if (Source != FounderSource.None)
                {
                    w.Field("src", SourceCode(Source));
                }

                // D117. On a pool founder's row only, so the floor's and the trickle's rows are
                // byte for byte what they were: the index of the pool file it is a copy of.
                if (Source == FounderSource.Pool)
                {
                    w.Field("pool", PoolIndex);
                }
            }
            else if (Kind == LineageEventKind.Kill)
            {
                // "e" is the event and "k" is a birth's own kind ("f", "r", "i"), so the kill's
                // code goes where "b" and "d" go rather than into "k", which already means
                // something else on the only rows that carry it. Every reader in the repository
                // gates on "e" first — the two PowerShell scorers on a regex that matches [bd]
                // and every Python read on row["e"] — so a kill row is skipped by all of them and
                // counted by none.
                w.Field("e", "k")
                    .Field("t", ElapsedSeconds)
                    .Field("id", Id)
                    .Field("by", AttackerId)
                    .Field("root", RootLost ? 1 : 0)
                    .Field("parts", PartsLost)
                    .Field("tj", TissueJoulesLost)
                    .Field("rj", ReserveJoulesLost)
                    .Field("ind", IndeterminateNodes)

                    // Round 46's K9b: which part came off, and which of the attacker's parts was
                    // touching it. Appended at the end, so a reader written against the older
                    // kill row keeps working, and a row without them was recorded before them.
                    .Field("part", PartIndex)
                    .Field("byPart", AttackerPartIndex);
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
