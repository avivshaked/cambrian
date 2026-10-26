using System;
using System.Collections.Generic;
using System.IO;

namespace Evosim.Core
{
    /// <summary>
    /// The world, written down and put back — Core's half of a checkpoint
    /// (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reconstruct, then overwrite.</b> A restore builds a <see cref="World"/> from the same
    /// <see cref="RunConfig"/> and the same seed the run was launched with, which rebuilds every
    /// structure that is a function of those two and of nothing else — the light model, the
    /// current's amplitudes and phases, the sea floor, the fields' geometry and masks, the patch
    /// map, the conception generator's stream. Then this reads back the state those structures
    /// have accumulated: the population, the stocks, the counters and the two generators that
    /// have been drawn from. Nothing that can be derived is written, and nothing that has been
    /// drawn from is derived.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here, and why each is safe.</b> <c>_ledgers</c>,
    /// <c>_born</c>, <c>_conceptionOrder</c>, <c>_conceptionSurplus</c>, Grow's <c>_juveniles</c>
    /// the intake's <c>_mouths</c>, <c>_mouthRates</c> and <c>_intakeDemand</c>, and Metabolise's
    /// pre-pass (<c>_turnShare</c> and its four siblings) are filled from
    /// scratch inside <see cref="Step"/> before anything reads them. <c>_dead</c> is an
    /// accumulator the farm never drains and never reads — it exists for
    /// <see cref="TakeDead"/>, which nothing in the farm calls — so a resumed run starts it
    /// empty and every number the run writes is unchanged. The fields' demand, availability and
    /// sinking buffers belong to one metabolic step. <see cref="Field"/>'s shading is rebuilt by
    /// the next step's own solve, and only its day factor is carried.
    /// </para>
    /// <para>
    /// <b>A genome is written as its own JSON and that is not a lapse.</b> Everything numeric
    /// here is bits. A genome is a structure with a version, a validator and a reader that
    /// refuses rather than defaults, and <see cref="GenomeJson"/> writes every float with the
    /// round-trip format, so a genome written and read back is the same genome to the bit. A
    /// second binary genome serializer would be a second thing to move on every format bump —
    /// and this project has bumped the genome format three times in a fortnight.
    /// </para>
    /// </remarks>
    public sealed partial class World
    {
        /// <summary>The layout this build writes and the only one it reads.</summary>
        /// <remarks>
        /// 2 since 2026-09-22: a body's reserve, tissue and adult tissue are doubles, so this
        /// reader takes eight bytes each where a version-1 stream wrote four, and every field
        /// after the first creature would be a plausible number read out of the middle of another
        /// one. The version is what turns that into a refusal.
        /// </remarks>
        /// <remarks>
        /// 3 with the module gene (D106 item 2, 2026-09-22): a creature carries its per-node
        /// module counts, rule 3's starvation clock and its plan revision, and three cumulative
        /// counters join the world's. A body's parts are a property of its history now, so a
        /// version-2 stream restores a plant that had grown eight leaves as one that had grown
        /// none — a plausible creature that nobody simulated, which is what a version refuses.
        /// </remarks>
        /// <remarks>
        /// 4 with the mouth (D106 items 1 and 3, 2026-09-22): a creature carries the share of its
        /// health pool each part still holds and the developer's path to every part it has lost to
        /// a bite, and six cumulative counters join the world's. Both are properties of a body's
        /// history that nothing else can restore — a version-3 stream would put a half-eaten
        /// animal back whole, which is the same fault the module counts' bump was made for.
        /// </remarks>
        /// <remarks>
        /// 5 with the refusal split (2026-09-22 night, after round 44's read): the module rule's
        /// refusals are counted by reason, and the two cumulative counters join the world's so
        /// that a continued run's windows are the unbroken run's. Round 45's checkpoints are
        /// version 4 and are refused by this build; the farm program that wrote them
        /// (`4300278`) reads them.
        /// </remarks>
        /// <remarks>
        /// 7 with light by exposure (D110, 2026-09-23): a pending row of the absorptive log carries
        /// the body's lit area in its pose beside the orientation-averaged one, four bytes a row
        /// that a version-6 reader would take from the next row's time. The exposure array itself
        /// is not written; the harness fills it from the solver's rotations before the world reads.
        /// </remarks>
        /// <remarks>
        /// 8 with the support cost (D113, 2026-09-23): a pending row of the absorptive log carries
        /// the body's support watts after its exposed area, four bytes more a row, for version 7's
        /// reason. A part's distance from the root is not written: it is re-measured when the body
        /// is developed again, as its volume is.
        /// </remarks>
        /// <remarks>
        /// 9 with the founding trickle (D115, 2026-09-23): the world carries the trickle's own
        /// generator and its cumulative spawn count, and every queued lineage row carries its
        /// founder source. A version-8 stream has none of the three, and a resumed trickle whose
        /// stream restarted from its seed would draw the unbroken run's founders a second time.
        /// </remarks>
        /// <remarks>
        /// 10 with reproduction paid as it goes (2026-09-24): every creature carries its gestation
        /// account, every queued birth row its mode and share, and the world its two cumulative
        /// gestation counters. A version-9 stream has none of them.
        /// </remarks>
        /// <remarks>
        /// 11 with the contact record (2026-09-25): every creature carries
        /// <see cref="Organism.PartContact"/> after its damage. The flag was sticky until a plan
        /// change, so it was history and not a step's reading, and a version-10 stream put every
        /// body back touching nothing; round 48's resume parted from the run at its first sample
        /// in the jointed bodies whose brains read the channel. Version 10 is still read, as
        /// <see cref="LossyStateVersion"/>: round 48's checkpoints are version 10, and the
        /// theatre's story mode opens them as the cousins they already are.
        /// </remarks>
        /// <remarks>
        /// D123 (round 49) made both records the last metabolic step's alone and left the layout as
        /// 11 had it. The physics steps after a restore read that step's record before the next
        /// metabolic step rewrites it, so it is saved exactly as before; what moved is the rule
        /// that fills it, and <c>coreHash</c> is what says so.
        /// </remarks>
        /// <remarks>
        /// 12 with round 49's two instruments (2026-09-25): every queued lineage row carries the
        /// gestation account and the reserve a body died with and a founder's four landing
        /// readings, so a row queued before a checkpoint is the row the unbroken run writes. They
        /// are records and not state, and the farm drains the queue before every checkpoint, so a
        /// version-11 stream would restore the same world; it is refused all the same, as every
        /// layout this build does not write is, because the harness's half of the file is keyed on
        /// the world's version. Nothing on disk that is read again was written at 11.
        /// </remarks>
        /// <remarks>
        /// 13 with round 51's build (D127, D128; 2026-09-27): every creature carries its ageing
        /// clock and the hazard accumulated along it after its contact record, every corpse its
        /// first joules and whether it has settled, and the world its count of deaths by age. A
        /// version-12 stream carries none of them and is refused; every genome in it is format 9
        /// and refused anyway.
        /// </remarks>
        /// <remarks>
        /// 14 with round 57's build (2026-10-06, <c>logbook/specs/r57-build/scent-restore.md</c>):
        /// the world carries <see cref="ScentFilledCorpses"/> after its corpses, and a restore fills
        /// the scent from that prefix, so a module's remains appended after the step's fill are
        /// smelt by neither the live run nor the resumed one until the next fill. A version-13
        /// stream has no count and is refused, in a world with the scent and without it; every
        /// config that wrote one is refused by round 57's tunables anyway.
        /// </remarks>
        /// <remarks>
        /// 15 with round 58's exchange (D159, 2026-10-09): the world carries
        /// <see cref="Immigrated"/> and <see cref="ImmigrantsRefused"/> after its deaths by age,
        /// and an immigrant's queued lineage row the world and the creature it came from. Version
        /// 14 is still read, exactly, as <see cref="PreImmigrationStateVersion"/>.
        /// </remarks>
        /// <remarks>
        /// 16 with round 59's build (D167, 2026-10-12): the world carries the sources of the leaf
        /// smell's last fill after its absorptive deaths, and a restore fills the leaf field from
        /// them, so the restored field is the live one cell for cell whatever the growth step
        /// changed after the fill. Version 15 is still read, exactly, as
        /// <see cref="PreLeafStateVersion"/>, in a world without the leaf smell.
        /// </remarks>
        /// <remarks>
        /// 17 with round 59's grip (D162, D165, step 5): each organism carries the grip price it
        /// owes and the grip joules it has paid, and the world its grip total; the harness's
        /// bodies carry their touch of the rock and their grips (<c>Creature.WriteState</c>).
        /// Version 16 is refused by name (<see cref="GriplessStateVersion"/>). Version 15 is
        /// still read, exactly, in a world with neither the leaf smell, the touch of the rock
        /// nor the anchoring cell.
        /// </remarks>
        /// <remarks>
        /// 18 with round 60's build: each body in the harness's half carries what the card holds in
        /// two parts, its link-sphere centres' floats and rests and its root's float and carry
        /// (<c>Creature.WriteState</c>), so a card resume packs the pair the run held and not one
        /// split afresh from a sum (round 59's B3). The world's own half did not move. Version 17
        /// is still read, as <see cref="PreCardPairStateVersion"/>.
        /// </remarks>
        /// <remarks>
        /// 19 with D206 (2026-10-19): the spent units each body and each corpse holds, and the
        /// store's three counters. The harness's half did not move. Version 18 is still read, as
        /// <see cref="PreHeldStateVersion"/>.
        /// </remarks>
        /// <remarks>
        /// 20 with the intake's origin books: each corpse carries the id of the body whose bite made it
        /// (<see cref="Corpse.KilledBy"/>), each body the joules its mouths kept from its own kills, another's
        /// and carrion, each death row queued before a checkpoint those three, and the world its six totals
        /// and the second its books begin at (<see cref="IntakeOriginBooksFromSeconds"/>). The harness's half
        /// did not move. Version 19 is still read, as <see cref="PreOriginStateVersion"/>, and so is every
        /// layout 19 was: each reads the corpse killer as -1 (unknown), every count as 0, and sets the books'
        /// start to the checkpoint's own second, so a read can tell that the books cover part of the world.
        /// </remarks>
        public const int StateVersion = 20;

        /// <summary>
        /// Version 19, D206's layout, which this build reads as exactly what it held, with the origin books
        /// unknown: every corpse's killer reads -1, every body's and the world's counts 0, and
        /// <see cref="IntakeOriginBooksFromSeconds"/> is the checkpoint's second. That is a labelled
        /// absence and not a guess: the world carries the second the books begin at.
        /// </summary>
        public const int PreOriginStateVersion = 19;

        /// <summary>
        /// Version 18, round 60's to round 62's dial screens' layout, which this build reads as
        /// exactly what it holds: it differs from 19 only by D206's store, and a world that wrote
        /// it had no rule that could put a unit in one, so every body and corpse holds 0. That is a
        /// fact about the world read and not a default standing in for a missing field.
        /// </summary>
        public const int PreHeldStateVersion = 18;

        /// <summary>
        /// The first layout whose harness half carries each body's card pairs (round 60): the
        /// harness reads its half by this, and not by <see cref="StateVersion"/>, so a world layout
        /// that moves alone leaves the harness's reading where it was.
        /// </summary>
        public const int CardPairStateVersion = 18;

        /// <summary>
        /// Version 17, round 59's and round 60's screens' layout, which this build reads as exactly
        /// what it holds: it differs from 18 only by each body's card pairs, which a CPU-engine body
        /// never has and which a restore from 17 rebuilds from the poses, as the build that wrote it
        /// did. A card world resumed from it is therefore that build's resume, the rest of each link
        /// centre at zero, and parts from the run as round 59's B3 did.
        /// </summary>
        public const int PreCardPairStateVersion = 17;

        /// <summary>
        /// Version 16, the layout of round 59's build before its grip was checkpointed, refused
        /// by name. It may hold a body's touch of the rock without that record in its stream, and
        /// read with this build's reader that is a misread rather than a refusal. No run wrote
        /// it: only the build's own probes did.
        /// </summary>
        public const int GriplessStateVersion = 16;

        /// <summary>
        /// Version 15, round 58's layout, which this build reads as exactly what it holds in a
        /// world without round 59's additions: it differs from 17 only by the leaf sources, the
        /// mouth count and the grip's joules, and a world that wrote it had none of the three. A
        /// world with the leaf smell, the touch of the rock or the anchoring cell refuses it,
        /// since what those carry across a step is not in it.
        /// </summary>
        public const int PreLeafStateVersion = 15;

        /// <summary>
        /// Version 14, round 57's layout, which this build reads as exactly what it holds: it
        /// differs from 15 only by the two immigration counters, and a world that wrote version 14
        /// had no way to plant an immigrant, so both are 0 in it. That is a fact about the world
        /// read and not a default standing in for a missing field. No immigrant's lineage row can
        /// be in such a stream either, and every other row is laid out as 15 lays it out.
        /// </summary>
        public const int PreImmigrationStateVersion = 14;

        /// <summary>Whether this build reads a world state of the given layout.</summary>
        public static bool ReadsStateVersion(int version) =>
            version == StateVersion || version == PreOriginStateVersion || version == PreHeldStateVersion ||
            version == PreCardPairStateVersion || version == PreLeafStateVersion ||
            version == PreImmigrationStateVersion || version == LossyStateVersion;

        /// <summary>
        /// The one older layout this build still reads: version 10, the layout of round 48's
        /// checkpoints. It is read with every creature's contact record null, which is what the
        /// build that wrote it restored, so a world read from it is a cousin of the run and not
        /// its continuation; the farm refuses to resume from one unless told to take a cousin
        /// (<c>EVOSIM_ALLOW_SOURCE_MISMATCH</c>).
        /// </summary>
        public const int LossyStateVersion = 10;

        /// <summary>
        /// The layout <see cref="ReadState"/> last read: <see cref="StateVersion"/> for a world
        /// that has never been restored or was restored from this build's stream, and
        /// <see cref="LossyStateVersion"/> for one restored from a round 48 checkpoint.
        /// </summary>
        /// <remarks>
        /// Public because the harness's own layout moved with this one (the farm's
        /// <c>Checkpoint.Version</c> 6) and the harness reads its half of the same file after
        /// this has read its own, so this is how it knows which half it holds.
        /// </remarks>
        public int StateVersionRead { get; private set; } = StateVersion;

        /// <summary>
        /// Whether this world's bodies carry a touch of the rock or a grip across a step (D162),
        /// which the harness writes in each body's state from version 17 and no older layout holds.
        /// </summary>
        private bool HoldsTouchOrGrip =>
            Config.SenseBedTouch || Config.CellTypes.Contains(CellTypeIds.Anchoring);

        /// <summary>
        /// Writes the whole of the world's own state.
        /// </summary>
        public void WriteState(BinaryWriter w) => WriteState(w, StateVersion);

        /// <summary>
        /// Writes the world in a layout this build reads: <see cref="StateVersion"/>, or
        /// <see cref="PreImmigrationStateVersion"/> or <see cref="LossyStateVersion"/> for the
        /// tests that hold the readers of the older layouts to what the builds before wrote.
        /// Nothing in a run writes the older ones.
        /// </summary>
        internal void WriteState(BinaryWriter w, int layout)
        {
            if (!ReadsStateVersion(layout))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layout), layout,
                    "This build writes version " + StateVersion + " and, for a test, versions " +
                    PreOriginStateVersion + ", " + PreHeldStateVersion + ", " + PreCardPairStateVersion + ", " + PreLeafStateVersion + ", " + PreImmigrationStateVersion + " and " + LossyStateVersion +
                    ". Version " + GriplessStateVersion + " is refused by name.");
            }

            // Version 14 has no place for an immigrant, so a world that holds one is refused
            // rather than written as a stream that would read back as a world without one.
            if (layout < PreLeafStateVersion && (Immigrated > 0 || ImmigrantsRefused > 0))
            {
                throw new InvalidOperationException(
                    "This world has had immigrants, and version " + layout + " cannot record them.");
            }

            // Nor has version 15 a place for the leaf smell's sources (D167), the touch of the
            // rock or the grip (D162).
            if (layout < PreCardPairStateVersion && LeafScent != null)
            {
                throw new InvalidOperationException(
                    "This world has the leaf smell, and version " + layout + " cannot record its sources.");
            }

            // Nor has version 18 a place for D206's store.
            if (layout < PreOriginStateVersion && Config.PlantsKeepMatter)
            {
                throw new InvalidOperationException(
                    "This world keeps its plants' matter (D206), and version " + layout + " cannot record the store.");
            }

            if (layout < PreCardPairStateVersion && HoldsTouchOrGrip)
            {
                throw new InvalidOperationException(
                    "This world has the touch of the rock or the anchoring cell, and version " + layout +
                    " cannot record them.");
            }

            StateIo.Tag(w, "WRLD");
            w.Write(layout);
            w.Write(Seed);

            // ---- the clock, the counters and the two books

            w.Write(ElapsedSeconds);
            w.Write(_nextId);
            w.Write(_nextIndex);
            w.Write(_nextSpeciesId);

            w.Write(EnergyIn);
            w.Write(EnergyOut);
            w.Write(MatterInfluxedTotal);
            w.Write(MatterBuriedTotal);

            w.Write(DetritusDepositedTotal);
            w.Write(DetritusExudedTotal);
            w.Write(DetritusTakenTotal);
            w.Write(DetritusReturnedTotal);
            w.Write(BurntTotal);
            w.Write(RemineralisedTotal);
            w.Write(ReserveTrimmedTotal);

            w.Write(PoolShortTakes);
            w.Write(FixationShortTakes);
            w.Write(UptakeLimitedSteps);
            w.Write(PhotosyntheticSteps);
            w.Write(ConceptionsUnderMassFloor);
            w.Write(ConceptionsUnderMargin);
            w.Write(FoundersUnderMassFloor);

            w.Write(FloorSpawns);
            w.Write(Births);
            w.Write(Deaths);
            w.Write(Diverged);
            w.Write(Inoculated);
            w.Write(Stillbirths);
            w.Write(SelfOverlapStillbirths);
            w.Write(CrowdedStillbirths);
            w.Write(SecondsSinceFloorFired);
            w.Write(_absorptiveDeathsDropped);

            // D106 item 2's three, beside the counters they are read against.
            w.Write(ModuleAdds);
            w.Write(ModuleDrops);
            w.Write(ModuleAddsRefused);
            w.Write(ModuleAddsRefusedForShape);
            w.Write(ModuleAddsRefusedForReserve);

            // D106 items 1, 3 and 4's six, beside the module gene's three and for their reason:
            // a run continued from a checkpoint writes the same cumulative columns the unbroken
            // run would have, so a counter that restarted at zero would put a step in every
            // window a reader differences.
            w.Write(PartsKilled);
            w.Write(BodiesEaten);
            w.Write(CorpsesFromKills);
            w.Write(UnitsEaten);
            w.Write(CorpsesEaten);
            w.Write(HealingJoules);

            // D115's count, beside the floor's for the same reason the mouth's six are here.
            w.Write(TrickleSpawns);

            // The ruling of 2026-09-24's two cumulative counters, version 10.
            w.Write(GestationBirths);
            w.Write(GestatedTotal);

            // D117's count, only in a world whose config names a pool, so that every world
            // without one writes the trickle build's bytes and stays version 9. The reader asks
            // the same config, which a restore takes from the run it continues.
            if (Config.FoundingTricklePoolCount > 0) w.Write(PoolSpawns);

            // D124's two counters, for the pool count's reason and the same way: only in a world
            // whose config turns the founder cap on, so every other world writes the bytes it
            // wrote before and the layout stays version 12. The reader asks the same config.
            if (Config.FounderReserveCapFraction > 0f)
            {
                w.Write(FoundersCapped);
                w.Write(FounderJoulesCapped);
            }

            // D128's count, version 13.
            if (layout >= 13) w.Write(DeathsByAge);

            // D159's two, version 15.
            if (layout >= 15)
            {
                w.Write(Immigrated);
                w.Write(ImmigrantsRefused);
            }

            // Where the sun stands. See LightField.RestoreDayFactor.
            w.Write(Field.DayFactor);

            // ---- the conception generator, which a Shuffled world draws from every step

            WriteRng(w, _conceptionRng);

            // ---- the trickle's generator, which draws every step once the floor has closed

            WriteRng(w, _trickleRng);

            // ---- the population

            StateIo.Tag(w, "LIVE");
            w.Write(_living.Count);
            for (int i = 0; i < _living.Count; i++) WriteOrganism(w, _living[i], layout);

            // D220: the remains lying in the water, after the living and in id order, each as an organism record plus
            // its window's start and the part map that selects its body from the one it was cut from. Only in a world
            // whose bites feed, so every other world writes the bytes it wrote before and the layout stays version 20.
            // The reader asks the same config.
            if (Config.ContactEating)
            {
                StateIo.Tag(w, "RMNS");
                w.Write(_remains.Count);
                for (int i = 0; i < _remains.Count; i++) WriteOrganism(w, _remains[i], layout);
            }

            // ---- the species registry

            StateIo.Tag(w, "SPEC");
            w.Write(_species.Count);
            foreach (KeyValuePair<uint, SpeciesFounder> entry in _species)
            {
                w.Write(entry.Key);
                w.Write(entry.Value.FoundedAtSeconds);
                w.Write(GenomeJson.Write(entry.Value.Genome, indent: false));
            }

            // ---- the corpses

            StateIo.Tag(w, "CRPS");
            w.Write(_corpses.Count);
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse corpse = _corpses[i];
                w.Write(corpse.CreatureId);
                StateIo.WriteFloat3(w, corpse.Position);
                w.Write(corpse.Patch);
                w.Write(corpse.Joules);
                w.Write(corpse.AgeSeconds);

                // D127's two, version 13: what the corpse was worth when founded, and whether it
                // lies at rest on a reef's cap or the bed.
                if (layout >= 13)
                {
                    w.Write(corpse.FirstJoules);
                    w.Write(corpse.Settled);

                    // And what a picture needs to draw the corpse as the body it was: its plan
                    // (the genome is the record's, by the creature's id), its size and its pose.
                    w.Write(corpse.ScaledBy);
                    int[] counts = corpse.ModuleCounts;
                    w.Write(counts == null ? 0 : counts.Length);
                    if (counts != null)
                    {
                        for (int k = 0; k < counts.Length; k++) w.Write(counts[k]);
                    }

                    List<int[]> lost = corpse.LostPartPaths;
                    w.Write(lost == null ? 0 : lost.Count);
                    if (lost != null)
                    {
                        for (int k = 0; k < lost.Count; k++)
                        {
                            w.Write(lost[k].Length);
                            for (int s = 0; s < lost[k].Length; s++) w.Write(lost[k][s]);
                        }
                    }

                    w.Write(corpse.HasPose);
                    w.Write(corpse.Rotation.X);
                    w.Write(corpse.Rotation.Y);
                    w.Write(corpse.Rotation.Z);
                    w.Write(corpse.Rotation.W);
                }

                // D206's store, version 19.
                if (layout >= 19) w.Write(corpse.SpentUnits);

                // Whose bite made it, version 20.
                if (layout >= 20) w.Write(corpse.KilledBy);
            }

            // How many of them the scent's last fill covered, version 14 (scent-restore.md).
            if (layout >= 14) w.Write(_scentFilledCorpses);

            // ---- the two undrained instrument queues
            //
            // Both are drained by the harness at a sample, and a checkpoint is not a sample, so
            // whatever is standing in them belongs to the resumed run's first sample. Dropped,
            // they would be rows the original wrote and the restore did not.

            StateIo.Tag(w, "LNGE");
            w.Write(_lineageEvents.Count);
            bool capped = Config.FounderReserveCapFraction > 0f;
            bool corpseLandings = Config.FoundersFollowCorpses;
            bool lifespans = LifespanRecorded;
            bool climb = Config.GrowthAdultSeconds > 0f;
            for (int i = 0; i < _lineageEvents.Count; i++) WriteLineage(w, _lineageEvents[i], layout, capped, corpseLandings, lifespans, Config.ContactEating, climb);

            StateIo.Tag(w, "ABSD");
            w.Write(_absorptiveDeaths.Count);
            for (int i = 0; i < _absorptiveDeaths.Count; i++) WriteAbsorptive(w, _absorptiveDeaths[i]);

            // D220: the foundings the farm has not drained, which belong to the resumed run's first step as the
            // lineage rows do.
            if (Config.ContactEating)
            {
                StateIo.Tag(w, "RFND");
                w.Write(_remainsFoundings.Count);
                for (int i = 0; i < _remainsFoundings.Count; i++)
                {
                    RemainsFounding founding = _remainsFoundings[i];
                    w.Write(founding.RemainsId);
                    w.Write(founding.FromBodyId);
                    w.Write(founding.Map.Length);
                    for (int k = 0; k < founding.Map.Length; k++) w.Write(founding.Map[k]);

                    // Not founding.SourcePrior: it describes the source's pending plan map, which is transient and not
                    // in a checkpoint (Organism.PartMapFromPreviousPlan), and the farm drains the foundings in the
                    // step they are made, so a checkpoint it writes holds none.
                }
            }

            // D167: the sources the leaf field's last fill took, four floats each. None in a world
            // without the leaf smell.
            if (layout >= 16)
            {
                StateIo.Tag(w, "LEAF");
                w.Write(_leafSourceCount);
                for (int i = 0; i < 4 * _leafSourceCount; i++) w.Write(_leafSources[i]);
            }

            // D162, D165: what the grips have cost, version 17. 0 in a world without the cell.
            if (layout >= 17)
            {
                StateIo.Tag(w, "GRIP");
                w.Write(GripJoulesTotal);
            }

            // D206's three counters, version 19. 0 in a world without the rule.
            if (layout >= 19)
            {
                StateIo.Tag(w, "HELD");
                w.Write(HeldTotal);
                w.Write(RechargedTotal);
                w.Write(HeldReleasedTotal);
            }

            // The intake's origin books, version 20: the world's six totals.
            if (layout >= 20)
            {
                StateIo.Tag(w, "ORIG");
                w.Write(IntakeOwnKillJoules);
                w.Write(IntakeOtherKillJoules);
                w.Write(IntakeCarrionJoules);
                w.Write(MouthedIntakeOwnKillJoules);
                w.Write(MouthedIntakeOtherKillJoules);
                w.Write(MouthedIntakeCarrionJoules);
                w.Write(IntakeOriginBooksFromSeconds);
            }

            // D212's total, only in a world whose corpses spoil, so that every world without the dial
            // writes the bytes it wrote before and the layout stays version 19. The clock itself needs
            // nothing: a corpse's age is already in its record. The reader asks the same config.
            if (Config.CorpseHalfLifeSeconds > 0f) w.Write(CorpseSpoiledTotal);

            // D219's four totals, only in a world whose bites feed, so every world without the dial writes the
            // bytes it wrote before and the layout stays version 20. The reader asks the same config.
            if (Config.ContactEating)
            {
                w.Write(BiteKeptFromLeavesJoules);
                w.Write(BiteKeptFromOthersJoules);
                w.Write(BiteWasteJoules);
                w.Write(HealTissueJoules);

                // D220's three: the joules kept from remains, and the remains closed with the joules burnt at the close.
                w.Write(BiteKeptFromRemainsJoules);
                w.Write(RemainsClosed);
                w.Write(RemainsClosedJoules);
                w.Write(RemainsFounded);

                // D224's total, only with the bite taking the store too, so a world without it writes the bytes it wrote.
                if (Config.BiteTakesStore) w.Write(BiteStoreTakenJoules);

                // D227's two, only with the damage dial off 1, so a world at 1 writes the bytes it wrote.
                if (Config.BiteDamageScale != 1f)
                {
                    w.Write(MouthBlows);
                    w.Write(MouthBlowsFinishing);
                }

                // D232's seven, only with the kill swallowed into a gut, so a world without it writes the bytes it wrote.
                if (Config.KillSwallowsBody)
                {
                    w.Write(KillsSwallowed);
                    w.Write(SwallowedJoules);
                    w.Write(GutDigestedJoules);
                    w.Write(GutWasteJoules);
                    w.Write(BiteCapCutJoules);
                    w.Write(GutCapCutJoules);
                    w.Write(GutCapBound);
                }
            }

            // ---- the water

            WriteField(w, Nutrients);
            WriteField(w, Matter);

            StateIo.Tag(w, "WEND");
        }

        /// <summary>
        /// Puts the world back. Call on a world freshly constructed from the same config and seed.
        /// </summary>
        public void ReadState(BinaryReader r)
        {
            StateIo.Tag(r, "WRLD");

            int version = r.ReadInt32();
            if (version == GriplessStateVersion)
            {
                throw new InvalidDataException(
                    "The checkpoint's world state is version " + GriplessStateVersion + ", written by " +
                    "round 59's build before its grip and its touch of the rock were checkpointed. " +
                    "It may hold a body whose touch record it does not carry, so it is refused " +
                    "rather than misread. No run wrote it.");
            }

            if (!ReadsStateVersion(version))
            {
                throw new InvalidDataException(
                    "The checkpoint's world state is version " + version + " and this build " +
                    "reads version " + StateVersion + " (version " + PreOriginStateVersion + ", without the intake's origin books; version " + PreHeldStateVersion + ", without D206's store; version " + PreCardPairStateVersion + ", without the card's pairs; versions " + PreLeafStateVersion + " and " +
                    PreImmigrationStateVersion + ", without round 59's additions, and version " +
                    LossyStateVersion + ", without the contact record). A checkpoint is refused " +
                    "rather than read with a field guessed at, under the rule the config reader follows.");
            }

            if (version < PreCardPairStateVersion && HoldsTouchOrGrip)
            {
                throw new InvalidDataException(
                    "The checkpoint is version " + version + ", which carries no touch of the rock and " +
                    "no grip, and this world has the touch of the rock or the anchoring cell (D162).");
            }

            ulong seed = r.ReadUInt64();
            if (seed != Seed)
            {
                throw new InvalidDataException(
                    "The checkpoint was taken in a world seeded " + seed + " and this one is " +
                    "seeded " + Seed + ". Every per-creature seed derives from it, so this is a " +
                    "different world and not a different moment in this one.");
            }

            ElapsedSeconds = r.ReadDouble();
            _nextId = r.ReadInt64();
            _nextIndex = r.ReadUInt64();
            _nextSpeciesId = r.ReadUInt32();

            EnergyIn = r.ReadDouble();
            EnergyOut = r.ReadDouble();
            MatterInfluxedTotal = r.ReadDouble();
            MatterBuriedTotal = r.ReadDouble();

            DetritusDepositedTotal = r.ReadDouble();
            DetritusExudedTotal = r.ReadDouble();
            DetritusTakenTotal = r.ReadDouble();
            DetritusReturnedTotal = r.ReadDouble();
            BurntTotal = r.ReadDouble();
            RemineralisedTotal = r.ReadDouble();
            ReserveTrimmedTotal = r.ReadDouble();

            PoolShortTakes = r.ReadInt64();
            FixationShortTakes = r.ReadInt64();
            UptakeLimitedSteps = r.ReadInt64();
            PhotosyntheticSteps = r.ReadInt64();
            ConceptionsUnderMassFloor = r.ReadInt64();
            ConceptionsUnderMargin = r.ReadInt64();
            FoundersUnderMassFloor = r.ReadInt64();

            FloorSpawns = r.ReadInt64();
            Births = r.ReadInt64();
            Deaths = r.ReadInt64();
            Diverged = r.ReadInt64();
            Inoculated = r.ReadInt64();
            Stillbirths = r.ReadInt64();
            SelfOverlapStillbirths = r.ReadInt64();
            CrowdedStillbirths = r.ReadInt64();
            SecondsSinceFloorFired = r.ReadDouble();
            _absorptiveDeathsDropped = r.ReadInt32();

            ModuleAdds = r.ReadInt64();
            ModuleDrops = r.ReadInt64();
            ModuleAddsRefused = r.ReadInt64();
            ModuleAddsRefusedForShape = r.ReadInt64();
            ModuleAddsRefusedForReserve = r.ReadInt64();

            PartsKilled = r.ReadInt64();
            BodiesEaten = r.ReadInt64();
            CorpsesFromKills = r.ReadInt64();
            UnitsEaten = r.ReadDouble();
            CorpsesEaten = r.ReadInt64();
            HealingJoules = r.ReadDouble();

            TrickleSpawns = r.ReadInt64();

            GestationBirths = r.ReadInt64();
            GestatedTotal = r.ReadDouble();
            PoolSpawns = Config.FoundingTricklePoolCount > 0 ? r.ReadInt64() : 0L;

            // D124's two counters, read under the config the writer asked (see Write).
            if (Config.FounderReserveCapFraction > 0f)
            {
                FoundersCapped = r.ReadInt64();
                FounderJoulesCapped = r.ReadDouble();
            }
            else
            {
                FoundersCapped = 0L;
                FounderJoulesCapped = 0d;
            }

            // D128's count, version 13; a version-10 stream comes back with none.
            DeathsByAge = version >= 13 ? r.ReadInt64() : 0L;

            // D159's two, version 15. An older stream reads 0 for both, and 0 is what it holds
            // rather than a guess: no build that wrote version 14 or earlier could plant an
            // immigrant (PreImmigrationStateVersion).
            if (version >= 15)
            {
                Immigrated = r.ReadInt64();
                ImmigrantsRefused = r.ReadInt64();
            }
            else
            {
                Immigrated = 0L;
                ImmigrantsRefused = 0L;
            }

            Field.RestoreDayFactor(r.ReadSingle());

            ReadRng(r, _conceptionRng);
            ReadRng(r, _trickleRng);

            StateIo.Tag(r, "LIVE");
            int living = r.ReadInt32();
            _living.Clear();
            for (int i = 0; i < living; i++) _living.Add(ReadOrganism(r, version, remains: false));

            _remains.Clear();
            if (Config.ContactEating)
            {
                StateIo.Tag(r, "RMNS");
                int standing = r.ReadInt32();
                for (int i = 0; i < standing; i++) _remains.Add(ReadOrganism(r, version, remains: true));
            }

            StateIo.Tag(r, "SPEC");
            int species = r.ReadInt32();
            _species.Clear();
            for (int i = 0; i < species; i++)
            {
                uint id = r.ReadUInt32();
                double founded = r.ReadDouble();
                Genome genome = GenomeJson.Read(r.ReadString());
                _species[id] = new SpeciesFounder(genome, founded);
            }

            StateIo.Tag(r, "CRPS");
            int corpses = r.ReadInt32();
            CorpsesChanged();
            _corpses.Clear();
            for (int i = 0; i < corpses; i++)
            {
                long id = r.ReadInt64();
                Float3 position = StateIo.ReadFloat3(r);
                int patch = r.ReadInt32();
                double joules = r.ReadDouble();
                double age = r.ReadDouble();

                // A version-10 stream has neither of D127's two: the corpse comes back worth what
                // it holds and in the water, which is what the build that wrote it had.
                double first = version >= 13 ? r.ReadDouble() : joules;
                bool settled = version >= 13 && r.ReadBoolean();

                var corpse = new Corpse(id, position, patch, joules, first)
                {
                    AgeSeconds = age,
                    Settled = settled,
                };

                if (version >= 13)
                {
                    corpse.ScaledBy = r.ReadSingle();

                    int countLength = r.ReadInt32();
                    if (countLength > 0)
                    {
                        var counts = new int[countLength];
                        for (int k = 0; k < countLength; k++) counts[k] = r.ReadInt32();
                        corpse.ModuleCounts = counts;
                    }

                    int lostCount = r.ReadInt32();
                    if (lostCount > 0)
                    {
                        var lost = new List<int[]>(lostCount);
                        for (int k = 0; k < lostCount; k++)
                        {
                            var path = new int[r.ReadInt32()];
                            for (int s = 0; s < path.Length; s++) path[s] = r.ReadInt32();
                            lost.Add(path);
                        }

                        corpse.LostPartPaths = lost;
                    }

                    bool hasPose = r.ReadBoolean();
                    float qx = r.ReadSingle(), qy = r.ReadSingle(), qz = r.ReadSingle(), qw = r.ReadSingle();
                    corpse.RestorePose(new Quat(qx, qy, qz, qw), hasPose);
                }

                // Version 18 had no store, and no rule that could fill one.
                if (version >= 19) corpse.SpentUnits = r.ReadDouble();

                // An older layout has no killer: the corpse comes back as -1, which the books' start
                // second (IntakeOriginBooksFromSeconds) labels as unknown and not as carrion.
                if (version >= 20) corpse.KilledBy = r.ReadInt64();

                _corpses.Add(corpse);
            }

            // Version 14's count. A version-10 stream has none, and its restore fills from every
            // corpse, which is what the build that wrote it did.
            int filled = version >= 14 ? r.ReadInt32() : corpses;
            if (filled < 0 || filled > corpses)
            {
                throw new InvalidDataException(
                    "The checkpoint says the scent's last fill covered " + filled + " corpses, and " +
                    "it holds " + corpses + ". The fill covers a prefix of the corpses, so the file " +
                    "is damaged or was written by another layout.");
            }

            _scentFilledCorpses = filled;

            StateIo.Tag(r, "LNGE");
            int events = r.ReadInt32();
            _lineageEvents.Clear();
            bool capped = Config.FounderReserveCapFraction > 0f;
            bool corpseLandings = Config.FoundersFollowCorpses;
            bool lifespans = LifespanRecorded;
            bool climb = Config.GrowthAdultSeconds > 0f;
            for (int i = 0; i < events; i++) _lineageEvents.Add(ReadLineage(r, version, capped, corpseLandings, lifespans, Config.ContactEating, climb));

            StateIo.Tag(r, "ABSD");
            int deaths = r.ReadInt32();
            _absorptiveDeaths.Clear();
            for (int i = 0; i < deaths; i++) _absorptiveDeaths.Add(ReadAbsorptive(r));

            _remainsFoundings = new List<RemainsFounding>();
            if (Config.ContactEating)
            {
                StateIo.Tag(r, "RFND");
                int foundings = r.ReadInt32();
                for (int i = 0; i < foundings; i++)
                {
                    long remainsId = r.ReadInt64();
                    long fromBodyId = r.ReadInt64();
                    var map = new int[r.ReadInt32()];
                    for (int k = 0; k < map.Length; k++) map[k] = r.ReadInt32();
                    _remainsFoundings.Add(new RemainsFounding(remainsId, fromBodyId, map));
                }
            }

            if (version >= 16)
            {
                StateIo.Tag(r, "LEAF");
                int sources = r.ReadInt32();
                if (sources < 0 || (sources > 0 && LeafScent == null))
                {
                    throw new InvalidDataException(
                        "The checkpoint holds " + sources + " leaf smell sources, and this world " +
                        (LeafScent == null ? "has no leaf smell." : "cannot hold a negative count."));
                }

                if (_leafSources.Length < 4 * sources) _leafSources = new float[4 * sources];
                for (int i = 0; i < 4 * sources; i++) _leafSources[i] = r.ReadSingle();
                _leafSourceCount = sources;
            }
            else
            {
                if (LeafScent != null)
                {
                    throw new InvalidDataException(
                        "The checkpoint is version " + version + ", which carries no leaf smell sources, " +
                        "and this world has the leaf smell (D167). Its field cannot be restored exactly.");
                }

                _leafSourceCount = 0;
            }

            // Version 15 had no grip, and a world restored from it held none (refused above).
            if (version >= 17)
            {
                StateIo.Tag(r, "GRIP");
                GripJoulesTotal = r.ReadDouble();
            }

            if (version >= 19)
            {
                StateIo.Tag(r, "HELD");
                HeldTotal = r.ReadDouble();
                RechargedTotal = r.ReadDouble();
                HeldReleasedTotal = r.ReadDouble();
            }

            // An older layout carries no origin books: the corpse killers read -1 (set above), every count
            // is 0, and the books begin at this checkpoint's second.
            if (version >= 20)
            {
                StateIo.Tag(r, "ORIG");
                IntakeOwnKillJoules = r.ReadDouble();
                IntakeOtherKillJoules = r.ReadDouble();
                IntakeCarrionJoules = r.ReadDouble();
                MouthedIntakeOwnKillJoules = r.ReadDouble();
                MouthedIntakeOtherKillJoules = r.ReadDouble();
                MouthedIntakeCarrionJoules = r.ReadDouble();
                IntakeOriginBooksFromSeconds = r.ReadDouble();
            }
            else
            {
                IntakeOwnKillJoules = IntakeOtherKillJoules = IntakeCarrionJoules = 0d;
                MouthedIntakeOwnKillJoules = MouthedIntakeOtherKillJoules = MouthedIntakeCarrionJoules = 0d;
                IntakeOriginBooksFromSeconds = ElapsedSeconds;
            }

            // D212's total, under the config the writer asked (see Write).
            CorpseSpoiledTotal = Config.CorpseHalfLifeSeconds > 0f ? r.ReadDouble() : 0d;

            // D219's four totals, under the config the writer asked (see Write).
            if (Config.ContactEating)
            {
                BiteKeptFromLeavesJoules = r.ReadDouble();
                BiteKeptFromOthersJoules = r.ReadDouble();
                BiteWasteJoules = r.ReadDouble();
                HealTissueJoules = r.ReadDouble();
                BiteKeptFromRemainsJoules = r.ReadDouble();
                RemainsClosed = r.ReadInt64();
                RemainsClosedJoules = r.ReadDouble();
                RemainsFounded = r.ReadInt64();
                BiteStoreTakenJoules = Config.BiteTakesStore ? r.ReadDouble() : 0d;

                // D227's two, under the config the writer asked (see Write).
                if (Config.BiteDamageScale != 1f)
                {
                    MouthBlows = r.ReadInt64();
                    MouthBlowsFinishing = r.ReadInt64();
                }
                else
                {
                    MouthBlows = 0L;
                    MouthBlowsFinishing = 0L;
                }

                // D232's seven, under the config the writer asked (see Write).
                if (Config.KillSwallowsBody)
                {
                    KillsSwallowed = r.ReadInt64();
                    SwallowedJoules = r.ReadDouble();
                    GutDigestedJoules = r.ReadDouble();
                    GutWasteJoules = r.ReadDouble();
                    BiteCapCutJoules = r.ReadDouble();
                    GutCapCutJoules = r.ReadDouble();
                    GutCapBound = r.ReadInt64();
                }
                else
                {
                    KillsSwallowed = 0L;
                    SwallowedJoules = 0d;
                    GutDigestedJoules = 0d;
                    GutWasteJoules = 0d;
                    BiteCapCutJoules = 0d;
                    GutCapCutJoules = 0d;
                    GutCapBound = 0L;
                }
            }
            else
            {
                BiteKeptFromLeavesJoules = 0d;
                BiteKeptFromOthersJoules = 0d;
                BiteWasteJoules = 0d;
                HealTissueJoules = 0d;
                BiteKeptFromRemainsJoules = 0d;
                RemainsClosed = 0L;
                RemainsClosedJoules = 0d;
                RemainsFounded = 0L;
                BiteStoreTakenJoules = 0d;
                MouthBlows = 0L;
                MouthBlowsFinishing = 0L;
                KillsSwallowed = 0L;
                SwallowedJoules = 0d;
                GutDigestedJoules = 0d;
                GutWasteJoules = 0d;
                BiteCapCutJoules = 0d;
                GutCapCutJoules = 0d;
                GutCapBound = 0L;
            }

            ReadField(r, Nutrients);
            ReadField(r, Matter);

            StateIo.Tag(r, "WEND");

            // Derived from the corpses just read, as the step fills it: from the prefix the live
            // run's last fill covered, so remains appended after it are left out here as there.
            FillScent();

            // D167: from the sources the last fill took, as the live field was.
            LeafScent?.FillSources(_leafSources, _leafSourceCount);

            StateVersionRead = version;
        }

        // ------------------------------------------------------------------ one creature

        private void WriteOrganism(BinaryWriter w, Organism creature, int layout)
        {
            w.Write(creature.Id);
            w.Write(creature.ParentId);
            w.Write(creature.GenerationDepth);
            w.Write(creature.BirthSeed);
            w.Write(creature.SpeciesId);

            w.Write(creature.Energy);
            w.Write(creature.GestationJoules);
            w.Write(creature.TissueJoules);
            w.Write(creature.AdultTissueJoules);
            w.Write(creature.BodyFraction);
            w.Write(creature.Age);
            w.Write(creature.HeightY);
            w.Write(creature.BirthHeightY);
            w.Write(creature.X);
            w.Write(creature.Z);
            w.Write(creature.Patch);
            w.Write(creature.PendingWorkJoules);
            w.Write(creature.StandingWatts);
            w.Write(creature.AbsorptiveVolume);
            w.Write(creature.HasAbsorptiveTissue);
            w.Write(creature.HasPhotosyntheticTissue);
            w.Write(creature.Children);
            w.Write(creature.LastChildSeconds);
            w.Write(creature.LastDensityHere);
            w.Write(creature.LastShare);
            w.Write(creature.LastStepSeconds);

            WriteLedger(w, creature.Lifetime);
            WriteLedger(w, creature.LastLedger);

            // How the body is built back: the adult is developed from the genome, and this is
            // the one call that turns it into the body this creature has. See Phenotype.ScaledBy.
            // D220: a body of remains may be a limb cut off the body this describes, so it is the adult when the scale
            // it was cut at is whole, and the map below selects it from that body.
            bool remains = creature.IsRemains;
            bool isAdult = remains
                ? creature.Phenotype.ScaledBy == 1f
                : ReferenceEquals(creature.Phenotype, creature.AdultPhenotype);
            w.Write(isAdult);
            w.Write(creature.Phenotype.ScaledBy);

            if (!remains && !isAdult && creature.Phenotype.ScaledBy == 1f)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id} has a body that is neither its adult nor a ") +
                    "scaled copy of it. Every body in this world is one or the other — the " +
                    "developer runs once, at the adult's size — so this is a code path that has " +
                    "appeared since the checkpoint was written, and a checkpoint that guessed " +
                    "would restore a creature nobody simulated.");
            }

            // D106 item 2, and it goes before the genome because the genome is where the reader
            // develops the body: the counts are half of what decides how many parts that body
            // has, so they have to be in hand by then. A length of 0 is a creature that has never
            // moved a count, which is every body in the record and every determinate lineage.
            int[] counts = creature.ModuleCounts;
            w.Write(counts == null ? 0 : counts.Length);
            if (counts != null)
            {
                for (int i = 0; i < counts.Length; i++) w.Write(counts[i]);
            }

            w.Write(creature.ModuleStarvedSeconds);
            w.Write(creature.PlanRevision);

            // D106 item 1, and it goes here for the counts' reason: the paths are the other half
            // of what decides which parts the reader's development ends up with, so they have to
            // be in hand before the genome is read. A length of 0 is a body that has never been
            // bitten, which is every body in the record.
            List<int[]> lost = creature.LostPartPaths;
            w.Write(lost == null ? 0 : lost.Count);

            if (lost != null)
            {
                for (int i = 0; i < lost.Count; i++)
                {
                    int[] path = lost[i];
                    w.Write(path.Length);
                    for (int step = 0; step < path.Length; step++) w.Write(path[step]);
                }
            }

            // D106 item 3. A share per part, in the body's own part order, and a length of 0 is a
            // body with every part whole. Written after the paths, because what the reader has to
            // do with it — check it against the part count of the body it has just developed and
            // pruned — needs that body to exist.
            float[] health = creature.PartHealth;
            w.Write(health == null ? 0 : health.Length);

            if (health != null)
            {
                for (int i = 0; i < health.Length; i++) w.Write(health[i]);
            }

            // The health each part lost on the last metabolic step (D123; before round 49, since
            // the body's plan last changed), in the same order and with the same meaning of 0 —
            // and it is state the next step reads, not a diagnostic: the Damage sense reports it
            // on every physics step until the next metabolic step rewrites it, so a brain wired
            // to that channel drives on it. The layout did not move with D123. Left out until
            // StateVersion 6 (2026-09-23), when a resume of round 45 seed 2 restored every
            // wounded body sensing nothing and the two jointed ones among sixteen parted from
            // the run at the first sample (CheckpointFidelity found it; logbook/0114).
            float[] damage = creature.PartDamage;
            w.Write(damage == null ? 0 : damage.Length);

            if (damage != null)
            {
                for (int i = 0; i < damage.Length; i++) w.Write(damage[i]);
            }

            // What each part touched on the last metabolic step, in the same order and with the
            // same meaning of 0: the Contact sense reports it, and the physics steps after a
            // restore read it before the next metabolic step rewrites it. Until D123 the flag
            // stayed set until a plan change; either way it is state the next step reads, so the
            // layout did not move with D123. Left out until StateVersion 11 (2026-09-25), when a
            // resume of round 48 put every body back touching nothing and the jointed ones
            // whose brains read the channel parted from the run at the first sample.
            if (layout >= 11)
            {
                bool[] contact = creature.PartContact;
                w.Write(contact == null ? 0 : contact.Length);

                if (contact != null)
                {
                    for (int i = 0; i < contact.Length; i++) w.Write(contact[i]);
                }
            }

            // D128's clock and the hazard accumulated along it, version 13. Neither can be read
            // back off the body: births and wounds push the clock ahead of the age.
            if (layout >= 13)
            {
                w.Write(creature.AgeClock);
                w.Write(creature.AgeHazard);
            }

            // What the body's mouths have kept, version 16: a count nothing else holds.
            if (layout >= 16) w.Write(creature.MouthJoules);

            // The grip price owed and the grip joules paid, version 17 (D162, D165).
            if (layout >= 17)
            {
                w.Write(creature.PendingGripJoules);
                w.Write(creature.GripJoules);
            }

            // D206's store, version 19.
            if (layout >= 19) w.Write(creature.SpentUnits);

            // The mouths' kept joules by the origin of the corpse, version 20.
            if (layout >= 20)
            {
                w.Write(creature.MouthJoulesOwnKill);
                w.Write(creature.MouthJoulesOtherKill);
                w.Write(creature.MouthJoulesCarrion);
            }

            // D219: what the body's biting parts have kept, only in a world whose bites feed.
            if (Config.ContactEating) w.Write(creature.BiteKeptJoules);

            // D232: what the body holds in its gut, only in a world whose kills are swallowed, so every other world
            // writes the bytes it wrote before. A body of remains writes its 0.
            if (Config.KillSwallowsBody) w.Write(creature.GutJoules);

            // D220: the window's start and the map that selects the remains' parts from the body they were cut from;
            // a length of 0 is the whole body.
            if (remains)
            {
                w.Write(creature.RemainsSince);
                int[] selection = creature.RemainsMap;
                w.Write(selection == null ? 0 : selection.Length);
                if (selection != null)
                {
                    for (int i = 0; i < selection.Length; i++) w.Write(selection[i]);
                }
            }

            w.Write(GenomeJson.Write(creature.Genome, indent: false, id: creature.Id));
        }

        private Organism ReadOrganism(BinaryReader r, int version, bool remains)
        {
            var creature = new Organism
            {
                Id = r.ReadInt64(),
                ParentId = r.ReadInt64(),
                GenerationDepth = r.ReadInt32(),
                BirthSeed = r.ReadUInt64(),
                SpeciesId = r.ReadUInt32(),

                Energy = r.ReadDouble(),
                GestationJoules = r.ReadDouble(),
                TissueJoules = r.ReadDouble(),
                AdultTissueJoules = r.ReadDouble(),
                BodyFraction = r.ReadSingle(),
                Age = r.ReadSingle(),
                HeightY = r.ReadSingle(),
                BirthHeightY = r.ReadSingle(),
                X = r.ReadSingle(),
                Z = r.ReadSingle(),
                Patch = r.ReadInt32(),
                PendingWorkJoules = r.ReadSingle(),
                StandingWatts = r.ReadSingle(),
                AbsorptiveVolume = r.ReadSingle(),
                HasAbsorptiveTissue = r.ReadBoolean(),
                HasPhotosyntheticTissue = r.ReadBoolean(),
                Children = r.ReadInt32(),
                LastChildSeconds = r.ReadDouble(),
                LastDensityHere = r.ReadSingle(),
                LastShare = r.ReadSingle(),
                LastStepSeconds = r.ReadSingle(),
            };

            creature.Lifetime = ReadLedger(r);
            creature.LastLedger = ReadLedger(r);

            bool isAdult = r.ReadBoolean();
            float scale = r.ReadSingle();

            int countLength = r.ReadInt32();
            int[] counts = countLength > 0 ? new int[countLength] : null;
            for (int i = 0; i < countLength; i++) counts[i] = r.ReadInt32();

            creature.ModuleCounts = counts;
            creature.ModuleStarvedSeconds = r.ReadSingle();
            creature.PlanRevision = r.ReadInt32();

            int lostCount = r.ReadInt32();
            List<int[]> lost = lostCount > 0 ? new List<int[]>(lostCount) : null;

            for (int i = 0; i < lostCount; i++)
            {
                var path = new int[r.ReadInt32()];
                for (int step = 0; step < path.Length; step++) path[step] = r.ReadInt32();
                lost.Add(path);
            }

            creature.LostPartPaths = lost;

            int healthCount = r.ReadInt32();
            float[] health = healthCount > 0 ? new float[healthCount] : null;
            for (int i = 0; i < healthCount; i++) health[i] = r.ReadSingle();

            creature.PartHealth = health;

            int damageCount = r.ReadInt32();
            float[] damage = damageCount > 0 ? new float[damageCount] : null;
            for (int i = 0; i < damageCount; i++) damage[i] = r.ReadSingle();

            creature.PartDamage = damage;

            // A version-10 stream has no contact record, and the body comes back touching
            // nothing, which is what the build that wrote it restored. See LossyStateVersion.
            bool[] contact = null;

            if (version >= 11)
            {
                int contactCount = r.ReadInt32();
                contact = contactCount > 0 ? new bool[contactCount] : null;
                for (int i = 0; i < contactCount; i++) contact[i] = r.ReadBoolean();
            }

            creature.PartContact = contact;

            // D128's two, version 13; a version-10 body comes back unaged, as its build had it.
            if (version >= 13)
            {
                creature.AgeClock = r.ReadDouble();
                creature.AgeHazard = r.ReadDouble();
            }

            // Version 15 had no count, and a body restored from it reads 0, as its build had none.
            if (version >= 16) creature.MouthJoules = r.ReadDouble();

            // Version 15 had no grip, and a world that wrote it could not grip.
            if (version >= 17)
            {
                creature.PendingGripJoules = r.ReadDouble();
                creature.GripJoules = r.ReadDouble();
            }

            // Version 18 had no store, and no rule that could fill one.
            if (version >= 19) creature.SpentUnits = r.ReadDouble();

            if (version >= 20)
            {
                creature.MouthJoulesOwnKill = r.ReadDouble();
                creature.MouthJoulesOtherKill = r.ReadDouble();
                creature.MouthJoulesCarrion = r.ReadDouble();
            }

            if (Config.ContactEating) creature.BiteKeptJoules = r.ReadDouble();

            // D232, under the config the writer asked (see WriteOrganism).
            if (Config.KillSwallowsBody) creature.GutJoules = r.ReadDouble();

            int[] selection = null;
            if (remains)
            {
                creature.IsRemains = true;
                creature.RemainsSince = r.ReadDouble();

                int selectionLength = r.ReadInt32();
                if (selectionLength > 0)
                {
                    selection = new int[selectionLength];
                    for (int i = 0; i < selectionLength; i++) selection[i] = r.ReadInt32();
                }

                creature.RemainsMap = selection;
            }

            Genome genome = GenomeJson.Read(r.ReadString());
            creature.Genome = genome;

            // Developed once, at the genome's own adult scale, exactly as birth develops it —
            // Developer is a pure function of the genome, the limits, the shapes and (since D106)
            // the body's own module counts — and then cut by whatever it has lost, which is the
            // one thing no development can express. DevelopPlan is the same call the module rule
            // and the kill both make, so a restored body is the body the run was stepping.
            Phenotype adult = DevelopPlan(creature, counts, out _);

            creature.AdultPhenotype = adult;
            creature.Phenotype = isAdult ? adult : adult.Scaled(scale, Config.Shapes);

            // D220: remains cut off a body are that body, selected. Both phenotypes name the limb, as they did when
            // it was cut.
            if (selection != null)
            {
                creature.Phenotype = creature.Phenotype.Select(selection);
                creature.AdultPhenotype = creature.Phenotype;
            }

            if (health != null && health.Length != creature.Phenotype.PartCount)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id}: the checkpoint holds {health.Length} part ") +
                    FormattableString.Invariant(
                        $"healths and its body develops to {creature.Phenotype.PartCount} parts. ") +
                    "A body's health array is indexed by its part index, so the two disagreeing " +
                    "means the development this build performs is not the one that was saved — " +
                    "a restored creature would be wounded in the wrong places.");
            }

            // The contact record is indexed the same way, and asked the same question for the
            // same reason: a flag on the wrong index is a touch felt by the wrong part.
            if (contact != null && contact.Length != creature.Phenotype.PartCount)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Creature {creature.Id}: the checkpoint holds {contact.Length} contact ") +
                    FormattableString.Invariant(
                        $"flags and its body develops to {creature.Phenotype.PartCount} parts. ") +
                    "A body's contact record is indexed by its part index, so the two disagreeing " +
                    "means the development this build performs is not the one that was saved — " +
                    "a restored creature would feel a touch on the wrong part.");
            }

            // Cached at birth and therefore cached again here — see Organism.IndeterminateNodes.
            creature.IndeterminateNodes = IndeterminateNodesOf(genome);
            ReadAttributeFlags(creature, creature.Phenotype);

            return creature;
        }

        // ------------------------------------------------------------------ the small records

        private static void WriteLedger(BinaryWriter w, EnergyLedger ledger)
        {
            w.Write(ledger.LightIncome);
            w.Write(ledger.FoodIncome);
            w.Write(ledger.PoolDrawn);
            w.Write(ledger.LightCapacity);
            w.Write(ledger.Upkeep);
            w.Write(ledger.Neural);
            w.Write(ledger.Work);
            w.Write(ledger.Exuded);
            w.Write(ledger.Handling);
        }

        private static EnergyLedger ReadLedger(BinaryReader r) =>
            new EnergyLedger(
                r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void WriteLineage(BinaryWriter w, LineageEvent e, int layout, bool capped, bool corpseLandings, bool lifespans, bool contactEating, bool climb)
        {
            w.Write((int)e.Kind);
            w.Write(e.ElapsedSeconds);
            w.Write(e.Id);
            w.Write(e.ParentId);
            w.Write((int)e.BirthKind);
            w.Write(e.GenerationDepth);
            w.Write(e.SpeciesId);
            w.Write(e.HasAbsorptive);
            w.Write(e.HasJoint);
            w.Write(e.HasPhotosynthetic);
            w.Write(e.Patch);
            w.Write(e.BirthFraction);
            w.Write(e.AdultScale);
            w.Write(e.ReserveMargin);
            w.Write(e.IndeterminateNodes);
            w.Write(e.HasAttack);
            w.Write(e.HasIntake);
            w.Write(e.HasProtection);
            w.Write((int)e.Cause);

            // D115, on every row: version 9 is the first to carry it, so there is no older
            // layout to stay byte-compatible with.
            w.Write((int)e.Source);

            // Version 10: the birth row's mode and share, on every row as the row carries them;
            // and, merged the same day, the founder's endowment (D122) and the bud fields (D119),
            // on every row too, so that a row queued before a checkpoint and written after the
            // restore is byte for byte the row the unbroken run would have written.
            w.Write((int)e.ReproductionMode);
            w.Write(e.GestationShare);
            w.Write(e.EndowmentJoules);
            w.Write(e.BudCells != null);
            if (e.BudCells != null)
            {
                w.Write(e.BudCells);
                w.Write(e.BudsExpressed);
            }

            // D117's pool index, written only on a pool founder's row, which is what lets this
            // stay version 9: every row a world without a pool queues is byte for byte what the
            // trickle build wrote, and a pool row can only be in a stream this build wrote.
            if (e.Source == FounderSource.Pool) w.Write(e.PoolIndex);

            // Version 12, round 49's two instruments, on every row as version 10's fields are:
            // the gestation account and the reserve a body died with (0 on every other row), and
            // a founder's four landing readings (NaN on every row that has none, and a NaN goes
            // through the stream bit for bit). The lossy layout is written without them.
            if (layout >= 12)
            {
                w.Write(e.GestationJoulesAtDeath);
                w.Write(e.ReserveJoulesAtDeath);
                w.Write(e.LandingSnowDensity);
                w.Write(e.LandingSnowColumnDensity);
                w.Write(e.LandingMatterDensity);
                w.Write(e.LandingMatterColumnDensity);
            }

            // The death row's three origin counts, version 20, on a death row only.
            if (layout >= 20 && e.Kind == LineageEventKind.Death)
            {
                w.Write(e.IntakeOwnKillJoules);
                w.Write(e.IntakeOtherKillJoules);
                w.Write(e.IntakeCarrionJoules);
            }

            // D219: a death row's bite-kept joules, only in a world whose bites feed.
            if (contactEating && e.Kind == LineageEventKind.Death) w.Write(e.BiteKeptJoules);

            // D220: the joules a closed remains burnt, on that row only. A founding row's second body id rides in
            // the parent field every row writes.
            if (contactEating && e.Kind == LineageEventKind.RemainsClosed) w.Write(e.RemainsJoules);

            // D124's cut, on every row (0 on all but a cut founder's), and only in a world whose
            // config turns the cap on: every other world's rows are the bytes they were, and the
            // layout stays version 12. The reader asks the same config.
            if (capped) w.Write(e.CapCutJoules);

            // D129's landing reading, on every row (NaN on all but a mouth founder's), and only in
            // a world whose mouths follow the corpses, as D124's cut is: every other world's rows
            // are the bytes they were, and the layout stays version 13. The reader asks the same
            // config.
            if (corpseLandings)
            {
                w.Write(e.LandingCorpseJoules);
                w.Write(e.LandingCorpseMeanJoules);
            }

            // D128's gene on a birth row, the same way: only in a world where it can differ.
            if (lifespans) w.Write(e.LifespanScale);

            // D241's gene on a birth row, the same way: only in a world with the climb, so every other
            // world's rows are the bytes they were. The reader asks the same config.
            if (climb) w.Write(e.GrowthReserveSeconds);

            // D159's two, written only on an immigrant's row, for the pool index's reason: every
            // row a world without immigrants queues is the bytes version 14 wrote, and an
            // immigrant's row can only be in a stream of version 15.
            if (e.Kind == LineageEventKind.Birth && e.BirthKind == BirthKind.Immigration)
            {
                w.Write(e.ImmigrantFromWorld);
                w.Write(e.ImmigrantFromId);
            }

            // The kill's own five, appended and written only on a kill row — which is what lets
            // this stay version 4. A birth and a death are byte for byte what the mouth build
            // wrote, so every checkpoint on disk still restores; a version-4 stream can only carry
            // a kill row if the build that wrote it had kill rows, and that build reads them here.
            if (e.Kind != LineageEventKind.Kill) return;

            w.Write(e.AttackerId);
            w.Write(e.RootLost);
            w.Write(e.PartsLost);
            w.Write(e.TissueJoulesLost);
            w.Write(e.ReserveJoulesLost);

            // The kill row's part and byPart (round 46's K9b) are not written, and a restored
            // kill row reads -1 for both, which the row defines as "cannot say". Writing them
            // would take StateVersion to 10 and refuse every checkpoint on disk, round 46's
            // included, for a queue that the farm drains to lineage.jsonl before every
            // checkpoint (Program.WriteCheckpoint), so no farm checkpoint carries a kill row.
            // A birth row's bud and budx (the owner's ruling of 2026-09-24) are written, from
            // version 10, above.
        }

        private static LineageEvent ReadLineage(BinaryReader r, int version, bool capped, bool corpseLandings, bool lifespans, bool contactEating, bool climb)
        {
            var kind = (LineageEventKind)r.ReadInt32();
            double seconds = r.ReadDouble();
            long id = r.ReadInt64();
            long parentId = r.ReadInt64();
            var birthKind = (BirthKind)r.ReadInt32();
            int generationDepth = r.ReadInt32();
            uint speciesId = r.ReadUInt32();
            bool absorptive = r.ReadBoolean();
            bool joint = r.ReadBoolean();
            bool photosynthetic = r.ReadBoolean();
            int patch = r.ReadInt32();
            float birthFraction = r.ReadSingle();
            float adultScale = r.ReadSingle();
            float reserveMargin = r.ReadSingle();
            int indeterminateNodes = r.ReadInt32();
            bool attack = r.ReadBoolean();
            bool intake = r.ReadBoolean();
            bool protection = r.ReadBoolean();
            var cause = (DeathCause)r.ReadInt32();
            var source = (FounderSource)r.ReadInt32();
            var reproductionMode = (ReproductionMode)r.ReadInt32();
            float gestationShare = r.ReadSingle();
            double endowmentJoules = r.ReadDouble();
            string budCells = null;
            int budsExpressed = 0;
            if (r.ReadBoolean())
            {
                budCells = r.ReadString();
                budsExpressed = r.ReadInt32();
            }
            int poolIndex = source == FounderSource.Pool ? r.ReadInt32() : -1;

            // Version 12's six; a version-10 row has none, and reads as a row that carries none.
            double accountAtDeath = 0d, reserveAtDeath = 0d;
            float landingSnow = float.NaN, landingSnowColumn = float.NaN;
            float landingMatter = float.NaN, landingMatterColumn = float.NaN;

            if (version >= 12)
            {
                accountAtDeath = r.ReadDouble();
                reserveAtDeath = r.ReadDouble();
                landingSnow = r.ReadSingle();
                landingSnowColumn = r.ReadSingle();
                landingMatter = r.ReadSingle();
                landingMatterColumn = r.ReadSingle();
            }

            double intakeOwn = 0d, intakeOther = 0d, intakeCarrion = 0d;
            if (version >= 20 && kind == LineageEventKind.Death)
            {
                intakeOwn = r.ReadDouble();
                intakeOther = r.ReadDouble();
                intakeCarrion = r.ReadDouble();
            }

            double biteKept = contactEating && kind == LineageEventKind.Death ? r.ReadDouble() : 0d;
            double remainsJoules = contactEating && kind == LineageEventKind.RemainsClosed ? r.ReadDouble() : 0d;

            double capCut = capped ? r.ReadDouble() : 0d;
            float landingCorpse = corpseLandings ? r.ReadSingle() : float.NaN;
            float landingCorpseMean = corpseLandings ? r.ReadSingle() : float.NaN;
            float lifespan = lifespans ? r.ReadSingle() : float.NaN;
            float growthReserve = climb ? r.ReadSingle() : float.NaN;

            int fromWorld = -1;
            long fromId = -1;
            if (kind == LineageEventKind.Birth && birthKind == BirthKind.Immigration)
            {
                fromWorld = r.ReadInt32();
                fromId = r.ReadInt64();
            }

            if (kind == LineageEventKind.RemainsFounded) return LineageEvent.RemainsFoundedFrom(seconds, id, parentId);
            if (kind == LineageEventKind.RemainsClosed) return LineageEvent.RemainsClosedAt(seconds, id, remainsJoules);

            if (kind == LineageEventKind.Kill)
            {
                long attackerId = r.ReadInt64();
                bool rootLost = r.ReadBoolean();
                int partsLost = r.ReadInt32();
                double tissueLost = r.ReadDouble();
                double reserveLost = r.ReadDouble();

                return LineageEvent.Kill(
                    seconds, id, attackerId, rootLost, partsLost, tissueLost, reserveLost,
                    indeterminateNodes);
            }

            return kind == LineageEventKind.Birth
                ? LineageEvent.Birth(
                    seconds, id, parentId, birthKind, generationDepth, speciesId,
                    absorptive, joint, photosynthetic, patch, birthFraction, adultScale,
                    reserveMargin, indeterminateNodes, attack, intake, protection, source,
                    poolIndex, endowmentJoules, budCells, budsExpressed,
                    reproductionMode, gestationShare,
                    landingSnow, landingSnowColumn, landingMatter, landingMatterColumn,
                    capCut, landingCorpse, landingCorpseMean, lifespan,
                    fromWorld, fromId, growthReserve)
                : LineageEvent.Death(
                    seconds, id, cause, accountAtDeath, reserveAtDeath, intakeOwn, intakeOther, intakeCarrion, biteKept);
        }

        private static void WriteAbsorptive(BinaryWriter w, AbsorptiveSample s)
        {
            w.Write(s.ElapsedSeconds);
            w.Write(s.Id);
            w.Write(s.Age);
            w.Write(s.GenerationDepth);
            w.Write(s.Patch);
            w.Write(s.HeightY);
            w.Write(s.Volume);
            w.Write(s.AbsorptiveVolume);
            w.Write(s.LitArea);
            w.Write(s.PartCount);
            w.Write(s.Mixotroph);
            w.Write(s.Energy);
            w.Write(s.TissueJoules);
            w.Write(s.BirthInvestment);
            w.Write(s.DensityHere);
            w.Write(s.Share);
            w.Write(s.FoodWatts);
            w.Write(s.LightWatts);
            w.Write(s.UpkeepWatts);
            w.Write(s.ExudedWatts);
            w.Write(s.NetWatts);
            w.Write(s.Children);
            w.Write(s.LastChildSeconds);
            w.Write(s.Dead);
            w.Write(s.ExposedArea);
            w.Write(s.SupportWatts);
        }

        private static AbsorptiveSample ReadAbsorptive(BinaryReader r) =>
            new AbsorptiveSample(
                r.ReadDouble(), r.ReadInt64(), r.ReadSingle(), r.ReadInt32(), r.ReadInt32(),
                r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadInt32(),
                r.ReadBoolean(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle(),
                r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
                r.ReadInt32(), r.ReadDouble(), r.ReadBoolean(), r.ReadSingle(), r.ReadSingle());

        // ------------------------------------------------------------------ generators, fields

        internal static void WriteRng(BinaryWriter w, Rng rng)
        {
            w.Write(rng.State);
            w.Write(rng.Increment);
            w.Write(rng.HasSpareGaussian);
            w.Write(rng.SpareGaussian);
        }

        internal static void ReadRng(BinaryReader r, Rng rng)
        {
            ulong state = r.ReadUInt64();
            ulong increment = r.ReadUInt64();
            bool hasSpare = r.ReadBoolean();
            float spare = r.ReadSingle();

            rng.RestoreState(state, increment, hasSpare, spare);
        }

        /// <summary>
        /// Dispatches to the field's own writer.
        /// </summary>
        /// <remarks>
        /// By concrete type rather than through <see cref="IMatterField"/>, so that the interface
        /// — which is the seam between the world and its water, and which a test double may
        /// implement — does not grow two members that have nothing to do with what a field is
        /// for. A representation this does not know is refused by name rather than skipped.
        /// </remarks>
        private static void WriteField(BinaryWriter w, IMatterField field)
        {
            switch (field)
            {
                case GridField grid: grid.WriteState(w); return;
                case VertexField vertices: vertices.WriteState(w); return;
                case NutrientField cells: cells.WriteState(w); return;

                default:
                    throw new InvalidOperationException(
                        "A checkpoint cannot write a " + field.GetType().Name + ": the three " +
                        "representations it knows are the cells, the vertices and the grid. A " +
                        "fourth needs a writer of its own rather than a silent omission.");
            }
        }

        private static void ReadField(BinaryReader r, IMatterField field)
        {
            switch (field)
            {
                case GridField grid: grid.ReadState(r); return;
                case VertexField vertices: vertices.ReadState(r); return;
                case NutrientField cells: cells.ReadState(r); return;

                default:
                    throw new InvalidOperationException(
                        "A checkpoint cannot read a " + field.GetType().Name + ".");
            }
        }
    }
}
