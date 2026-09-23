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
    /// <c>_born</c>, <c>_conceptionOrder</c> and <c>_conceptionSurplus</c> are filled from
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
        public const int StateVersion = 9;

        /// <summary>
        /// Writes the whole of the world's own state.
        /// </summary>
        public void WriteState(BinaryWriter w)
        {
            StateIo.Tag(w, "WRLD");
            w.Write(StateVersion);
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

            // D117's count, only in a world whose config names a pool, so that every world
            // without one writes the trickle build's bytes and stays version 9. The reader asks
            // the same config, which a restore takes from the run it continues.
            if (Config.FoundingTricklePoolCount > 0) w.Write(PoolSpawns);

            // Where the sun stands. See LightField.RestoreDayFactor.
            w.Write(Field.DayFactor);

            // ---- the conception generator, which a Shuffled world draws from every step

            WriteRng(w, _conceptionRng);

            // ---- the trickle's generator, which draws every step once the floor has closed

            WriteRng(w, _trickleRng);

            // ---- the population

            StateIo.Tag(w, "LIVE");
            w.Write(_living.Count);
            for (int i = 0; i < _living.Count; i++) WriteOrganism(w, _living[i]);

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
            }

            // ---- the two undrained instrument queues
            //
            // Both are drained by the harness at a sample, and a checkpoint is not a sample, so
            // whatever is standing in them belongs to the resumed run's first sample. Dropped,
            // they would be rows the original wrote and the restore did not.

            StateIo.Tag(w, "LNGE");
            w.Write(_lineageEvents.Count);
            for (int i = 0; i < _lineageEvents.Count; i++) WriteLineage(w, _lineageEvents[i]);

            StateIo.Tag(w, "ABSD");
            w.Write(_absorptiveDeaths.Count);
            for (int i = 0; i < _absorptiveDeaths.Count; i++) WriteAbsorptive(w, _absorptiveDeaths[i]);

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
            if (version != StateVersion)
            {
                throw new InvalidDataException(
                    "The checkpoint's world state is version " + version + " and this build " +
                    "reads version " + StateVersion + ". A checkpoint is refused rather than " +
                    "read with a field guessed at, under the rule the config reader follows.");
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
            PoolSpawns = Config.FoundingTricklePoolCount > 0 ? r.ReadInt64() : 0L;

            Field.RestoreDayFactor(r.ReadSingle());

            ReadRng(r, _conceptionRng);
            ReadRng(r, _trickleRng);

            StateIo.Tag(r, "LIVE");
            int living = r.ReadInt32();
            _living.Clear();
            for (int i = 0; i < living; i++) _living.Add(ReadOrganism(r));

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
            _corpses.Clear();
            for (int i = 0; i < corpses; i++)
            {
                long id = r.ReadInt64();
                Float3 position = StateIo.ReadFloat3(r);
                int patch = r.ReadInt32();
                double joules = r.ReadDouble();

                var corpse = new Corpse(id, position, patch, joules)
                {
                    AgeSeconds = r.ReadDouble(),
                };

                _corpses.Add(corpse);
            }

            StateIo.Tag(r, "LNGE");
            int events = r.ReadInt32();
            _lineageEvents.Clear();
            for (int i = 0; i < events; i++) _lineageEvents.Add(ReadLineage(r));

            StateIo.Tag(r, "ABSD");
            int deaths = r.ReadInt32();
            _absorptiveDeaths.Clear();
            for (int i = 0; i < deaths; i++) _absorptiveDeaths.Add(ReadAbsorptive(r));

            ReadField(r, Nutrients);
            ReadField(r, Matter);

            StateIo.Tag(r, "WEND");
        }

        // ------------------------------------------------------------------ one creature

        private void WriteOrganism(BinaryWriter w, Organism creature)
        {
            w.Write(creature.Id);
            w.Write(creature.ParentId);
            w.Write(creature.GenerationDepth);
            w.Write(creature.BirthSeed);
            w.Write(creature.SpeciesId);

            w.Write(creature.Energy);
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
            bool isAdult = ReferenceEquals(creature.Phenotype, creature.AdultPhenotype);
            w.Write(isAdult);
            w.Write(creature.Phenotype.ScaledBy);

            if (!isAdult && creature.Phenotype.ScaledBy == 1f)
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

            // The health each part has lost over its life, in the same order and with the same
            // meaning of 0 — and it is state the next step reads, not a diagnostic: the Damage
            // sense reports it, so a brain wired to that channel drives on it. Left out until
            // StateVersion 6 (2026-09-23), when a resume of round 45 seed 2 restored every
            // wounded body sensing nothing and the two jointed ones among sixteen parted from
            // the run at the first sample (CheckpointFidelity found it; logbook/0114).
            float[] damage = creature.PartDamage;
            w.Write(damage == null ? 0 : damage.Length);

            if (damage != null)
            {
                for (int i = 0; i < damage.Length; i++) w.Write(damage[i]);
            }

            w.Write(GenomeJson.Write(creature.Genome, indent: false, id: creature.Id));
        }

        private Organism ReadOrganism(BinaryReader r)
        {
            var creature = new Organism
            {
                Id = r.ReadInt64(),
                ParentId = r.ReadInt64(),
                GenerationDepth = r.ReadInt32(),
                BirthSeed = r.ReadUInt64(),
                SpeciesId = r.ReadUInt32(),

                Energy = r.ReadDouble(),
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

        private static void WriteLineage(BinaryWriter w, LineageEvent e)
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

            // D117's pool index, written only on a pool founder's row, which is what lets this
            // stay version 9: every row a world without a pool queues is byte for byte what the
            // trickle build wrote, and a pool row can only be in a stream this build wrote.
            if (e.Source == FounderSource.Pool) w.Write(e.PoolIndex);

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
        }

        private static LineageEvent ReadLineage(BinaryReader r)
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
            int poolIndex = source == FounderSource.Pool ? r.ReadInt32() : -1;

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
                    poolIndex)
                : LineageEvent.Death(seconds, id, cause);
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
