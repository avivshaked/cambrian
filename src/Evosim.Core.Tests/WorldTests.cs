using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>The ecosystem loop — DESIGN.md §5A, D021, D022.</summary>
    public class WorldTests
    {
        private readonly ITestOutputHelper _output;

        public WorldTests(ITestOutputHelper output) => _output = output;

        private static World Run(RunConfig config, LightModel light, float seconds, float dt = 1f)
        {
            if (light != null) config.Light = light;
            var world = new World(config, seed: 1);
            for (float t = 0f; t < seconds; t += dt) world.Step(dt);
            return world;
        }

        [Fact]
        public void DifferentSeedsGiveDifferentFounderPopulations()
        {
            // The guard for logbook/0019. World issued per-creature seeds from a counter started
            // at its own seed, so run 1 drew founders from seeds 1..40 and run 2 from 2..41 —
            // thirty-nine of the same forty genomes. Every "consistent across three seeds" claim
            // made here was three runs of nearly one experiment, and nothing said so.
            //
            // Asserted on the genomes rather than on the seeds, because the seeds are an
            // implementation detail and the thing that has to differ is the biology.
            var one = FounderGenomes(seed: 1);
            var two = FounderGenomes(seed: 2);

            Assert.NotEmpty(one);
            Assert.Equal(one.Count, two.Count);

            int shared = 0;
            for (int i = 0; i < one.Count; i++)
            {
                if (two.Contains(one[i])) shared++;
            }

            Assert.True(
                shared * 4 < one.Count,
                $"seeds 1 and 2 share {shared} of {one.Count} founder genomes. Consecutive " +
                "seeds are not independent runs, and a replication across them proves much " +
                "less than it looks like it does.");
        }

        /// <summary>Serialized founder genomes of a freshly seeded world, in birth order.</summary>
        private static List<string> FounderGenomes(ulong seed)
        {
            var config = new RunConfig { Light = new LightModel(100f, 12f) };
            var world = new World(config, seed);

            // Enough steps for the floor to fill toward MinimumPopulation. One step spawns only
            // FloorSpawnsPerStep creatures, and a two-genome sample is not a population.
            for (int i = 0; i < 60; i++) world.Step(1f);

            var genomes = new List<string>();
            foreach (Organism creature in world.Living) genomes.Add(GenomeJson.Write(creature.Genome));
            return genomes;
        }

        [Fact]
        public void TheFloorIsWhatCreatesGenerationZero()
        {
            // There is no separate seeding path — at t=0 the population is zero, the floor fires,
            // and the world exists. One mechanism, exercised continuously rather than an
            // initialisation that runs once and is therefore tested once.
            var world = new World(new RunConfig());

            Assert.Empty(world.Living);

            world.Step(1f);

            Assert.NotEmpty(world.Living);
            Assert.True(world.FloorSpawns > 0);
            Assert.Equal(0, world.Births);
        }

        [Fact]
        public void TheFloorTricklesRatherThanFillingAtOnce()
        {
            // A cohort spawned together tends to die together, which manufactures a boom-and-bust
            // oscillation that is an artefact of the refill rule rather than anything the world
            // is doing.
            var config = new RunConfig { MinimumPopulation = 40, FloorSpawnsPerStep = 2 };
            var world = new World(config);

            world.Step(1f);
            Assert.True(world.Living.Count <= 2, $"filled to {world.Living.Count} in one step");
        }

        [Fact]
        public void EveryFounderIsGenerationZeroAndHasNoParent()
        {
            var world = Run(new RunConfig(), new LightModel(), seconds: 20f);

            foreach (Organism creature in world.Living)
            {
                if (creature.ParentId >= 0) continue;

                Assert.Equal(0, creature.GenerationDepth);
                Assert.InRange(creature.Phenotype.PartCount, 1, 2);
            }
        }

        [Fact]
        public void ADarkWorldStarvesAndTheFloorNeverStopsFiring()
        {
            // The failure D021 exists to make visible. With no light nothing can earn, so the
            // population is held up entirely by us — and the population count alone would look
            // perfectly healthy while that was true.
            var config = new RunConfig { MinimumPopulation = 20 };
            var world = Run(config, new LightModel(1e-6f, 1f), seconds: 400f);

            WorldSample sample = WorldStats.Sample(world);

            _output.WriteLine(sample.ToString());
            _output.WriteLine($"floor spawns {sample.FloorSpawns}, births {sample.Births}");

            Assert.Equal(0, sample.MinDepth);

            // A dark world cannot go one step without the floor, let alone a hundred.
            Assert.False(sample.IsSelfSustaining(quietSeconds: 100));
            Assert.True(sample.FloorSpawns > sample.Births, "a dark world should be floor-fed");
            Assert.True(world.Deaths > 0, "nothing died in a world with no light");
        }

        [Fact]
        public void FloorClosesAfterSecondsDefaultZeroNeverCloses()
        {
            // Bit-identical to the floor's behaviour before this knob existed: 0 means never, so
            // a dark world that keeps crashing keeps getting rescued indefinitely rather than
            // being allowed to reach zero.
            var config = new RunConfig { MinimumPopulation = 20 };
            Assert.Equal(0f, config.FloorClosesAfterSeconds);

            var world = Run(config, new LightModel(1e-6f, 1f), seconds: 200f);
            long spawnsAt200 = world.FloorSpawns;

            for (int i = 0; i < 200; i++) world.Step(1f);

            Assert.True(world.FloorSpawns > spawnsAt200, "the floor should still be firing past t=200");
            Assert.NotEmpty(world.Living);
        }

        [Fact]
        public void FloorClosesAfterSecondsFoundsOnceThenNeverRescuesAgain()
        {
            // The founding cohort must survive the knob. One step of 0.5 s puts ElapsedSeconds at
            // 0.5, below the 1 s threshold, so the floor still fires and places founders — there
            // is no other way for a creature to enter this world (World's remarks). Every step
            // after that crosses the threshold, and a dark world that would otherwise be propped
            // up forever is instead allowed to starve to zero: D021's "never again", enforced by
            // this knob rather than by the world's own biology.
            var config = new RunConfig
            {
                MinimumPopulation = 20,
                FloorClosesAfterSeconds = 1f,
                Light = new LightModel(1e-6f, 1f),
            };
            var world = new World(config);

            world.Step(0.5f);
            Assert.NotEmpty(world.Living);
            Assert.True(world.FloorSpawns > 0);

            long spawnsAtClose = world.FloorSpawns;

            for (int i = 0; i < 800; i++) world.Step(0.5f);

            Assert.Equal(spawnsAtClose, world.FloorSpawns);
            Assert.Empty(world.Living); // allowed to crash to zero and stay there.
        }

        [Fact]
        public void FloorClosesAfterSecondsReachesTheFloorSpawnArithmetic()
        {
            // Same seed, same crash, only the knob differs — so any difference in the floor-spawn
            // counter is this setting reaching EnforceFloor rather than something else diverging
            // between the two worlds.
            var dark = new LightModel(1e-6f, 1f);

            var open = new World(
                new RunConfig { MinimumPopulation = 20, Light = dark, FloorClosesAfterSeconds = 0f },
                seed: 7);
            var closes = new World(
                new RunConfig { MinimumPopulation = 20, Light = dark, FloorClosesAfterSeconds = 1f },
                seed: 7);

            for (int i = 0; i < 800; i++)
            {
                open.Step(0.5f);
                closes.Step(0.5f);
            }

            Assert.True(
                open.FloorSpawns > closes.FloorSpawns,
                $"open floor spawned {open.FloorSpawns}, closing floor spawned {closes.FloorSpawns}");
        }

        [Fact]
        public void EnergyIsConservedAcrossTheWholeRun()
        {
            // §5A.2's audit, and the reason it is worth having: under endogenous selection there
            // is no bad score to discard into, so free energy is free food and a creature that
            // finds any will take the world over. The books have to close.
            // Deliberately indifferent to whether the world explodes, starves or persists: the
            // books must close in all three cases, and tying this to a calibration nobody has
            // measured yet would make it fail for reasons that have nothing to do with energy.
            var config = new RunConfig { MinimumPopulation = 30, MaximumPopulation = 600 };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            // Three accounts now, not one: what creatures hold in reserve, what is locked up in
            // their bodies, and what is lying in the water as detritus (§5A.2c). Endowment,
            // tissue, feeding and death all move energy between them and none of them creates
            // any, so the same equality has to hold across a whole food web as across a pond of
            // plants.
            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine(
                $"in {world.EnergyIn:0.###} out {world.EnergyOut:0.###} " +
                $"standing {world.StandingJoules:0.###} (of which detritus {world.Nutrients.TotalJoules:0.###})");
            _output.WriteLine($"residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"energy is not conserved: {residual:0.###} J unaccounted for");
        }

        [Fact]
        public void DetritusFluxCountersReconcileWithTheField()
        {
            // The detritus-flux instrument (fable-propose-detritus-flux), as D098 leaves it.
            // The charged field has four ways in — dead tissue, exudation, faeces, and the
            // reserve cap's trim, which is counted with exudation — and two ways out: feeding,
            // and the bacteria. Settling, mixing and advection all conserve, so the counters must
            // bracket the standing stock exactly at any moment in any world. If a future
            // mechanism moves joules across the field's boundary without touching a counter,
            // this is what fails.
            var config = new RunConfig { MinimumPopulation = 30, MaximumPopulation = 600 };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double deposited = world.DetritusDepositedTotal;
            double returned = world.DetritusReturnedTotal;
            double remineralised = world.RemineralisedTotal;
            double taken = world.DetritusTakenTotal;
            double standing = world.Nutrients.TotalJoules;

            _output.WriteLine(
                $"deposited {deposited:0.###} faeces {returned:0.###} remineralised " +
                $"{remineralised:0.###} taken {taken:0.###} standing {standing:0.###}");

            double income = deposited + returned;

            Assert.True(deposited > 0, "nothing died in 300 s — the counter was never exercised");
            Assert.True(
                Math.Abs(income - taken - remineralised - standing) / Math.Max(1.0, income) < 1e-4,
                $"deposited + faeces - taken - remineralised = " +
                $"{income - taken - remineralised:0.###} J but the field holds {standing:0.###} J");
        }

        [Fact]
        public void EnergyIsConservedWithExudationRunning()
        {
            // D070: exudation moves joules from a body's reserve into the nutrient field, and
            // StandingJoules already sums both accounts whole — so §5A.2's equality needs no new
            // term and this is what proves it rather than asserting it. Run at 0.1, an order
            // above nothing and below the 0.15 screen, because a leak would show at any dose and
            // this one guarantees a living producer releases on almost every step.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                ExudationFraction = 0.1f,
            };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine(
                $"in {world.EnergyIn:0.###} out {world.EnergyOut:0.###} " +
                $"standing {world.StandingJoules:0.###} exuded {world.DetritusExudedTotal:0.###} " +
                $"residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                world.DetritusExudedTotal > 0,
                "nothing was exuded in 300 s — the mechanism was never exercised");
            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"exudation opened a hole in the energy audit: {residual:0.###} J unaccounted for");
        }

        [Fact]
        public void ExudationLeavesTheWorldBitIdenticalWhenItIsOff()
        {
            // The D052/D055 shape, tested rather than claimed: a knob whose default changes any
            // earlier result silently invalidates every arm this campaign has already scored.
            // Two worlds from one seed, one built without the property mentioned at all and one
            // with it set to its own default, must agree on every stat a report reads.
            var untouched = new World(Config(), seed: 7);
            var explicitlyOff = new World(Config(exudation: 0f), seed: 7);

            for (int i = 0; i < 200; i++)
            {
                untouched.Step(1f);
                explicitlyOff.Step(1f);
            }

            _output.WriteLine(
                $"untouched: {untouched.Living.Count} alive, {untouched.EnergyIn:R} in, " +
                $"{untouched.Nutrients.TotalJoules:R} detritus");

            Assert.Equal(0d, untouched.DetritusExudedTotal);
            Assert.Equal(0d, explicitlyOff.DetritusExudedTotal);

            Assert.Equal(untouched.Living.Count, explicitlyOff.Living.Count);
            Assert.Equal(untouched.Births, explicitlyOff.Births);
            Assert.Equal(untouched.Deaths, explicitlyOff.Deaths);
            Assert.Equal(untouched.EnergyIn, explicitlyOff.EnergyIn);
            Assert.Equal(untouched.EnergyOut, explicitlyOff.EnergyOut);
            Assert.Equal(untouched.Nutrients.TotalJoules, explicitlyOff.Nutrients.TotalJoules);
            Assert.Equal(untouched.DetritusDepositedTotal, explicitlyOff.DetritusDepositedTotal);
            Assert.Equal(untouched.DetritusTakenTotal, explicitlyOff.DetritusTakenTotal);
            Assert.Equal(untouched.Config.Hash(), explicitlyOff.Config.Hash());

            static RunConfig Config(float? exudation = null)
            {
                var config = new RunConfig { MinimumPopulation = 30, MaximumPopulation = 600 };

                // 120 W/m2 rather than 400 since fable-propose-growth.md (2026-09-08): a
                // reproduction costs a fraction of the parent's body where it cost a whole one
                // plus an endowment, so the same light carries several times the head-count and
                // both worlds met the ceiling before the 200 steps were up.
                config.Light = new LightModel(120f, 12f);
                if (exudation.HasValue) config.ExudationFraction = exudation.Value;
                return config;
            }
        }

        [Fact]
        public void DetritusFluxCountersReconcileWithTheFieldWhileProducersExude()
        {
            // The sibling of DetritusFluxCountersReconcileWithTheField, with D070's income
            // switched on: the identity gains exudation's term and nothing else. If exudation
            // deposited into the field without touching its counter, or touched the counter
            // without depositing, this is what fails; the audit alone would not, since both
            // mistakes are internal to StandingJoules.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                ExudationFraction = 0.15f,
            };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double deposited = world.DetritusDepositedTotal;
            double exuded = world.DetritusExudedTotal;
            double returned = world.DetritusReturnedTotal;
            double remineralised = world.RemineralisedTotal;
            double taken = world.DetritusTakenTotal;
            double standing = world.Nutrients.TotalJoules;

            _output.WriteLine(
                $"deposited {deposited:0.###} exuded {exuded:0.###} faeces {returned:0.###} " +
                $"remineralised {remineralised:0.###} taken {taken:0.###} standing {standing:0.###}");

            double income = deposited + exuded + returned;

            Assert.True(deposited > 0, "nothing died in 300 s — the deposit counter was never exercised");
            Assert.True(exuded > 0, "nothing was exuded in 300 s — the new counter was never exercised");

            Assert.True(
                Math.Abs(income - taken - remineralised - standing) / Math.Max(1.0, income) < 1e-4,
                $"deposited + exuded + faeces - taken - remineralised = " +
                $"{income - taken - remineralised:0.###} J but the field holds {standing:0.###} J");
        }

        [Fact]
        public void EnergyIsConservedWithRemineralisationRunning()
        {
            // D098's leg 8 books its own outflow: the joules that leave Nutrients as the
            // bacteria's heat are added to EnergyOut in the same statement that moves them, and
            // the units arrive in the spent field. Run aggressively rather than at a realistic
            // rate — if a half-transfer existed it would show fastest here.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                RemineralisationPerSecond = 0.01f,
            };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine(
                $"in {world.EnergyIn:0.###} out {world.EnergyOut:0.###} " +
                $"standing {world.StandingJoules:0.###} residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"remineralisation opened a hole in the energy audit: {residual:0.###} J unaccounted for");
        }

        [Fact]
        public void NoCreatureEverHoldsNegativeEnergy()
        {
            // A creature carrying a debt is one the world has no way to settle, and the audit
            // above would never close again.
            var world = new World(new RunConfig { MinimumPopulation = 30, MaximumPopulation = 600 });

            for (int i = 0; i < 400; i++)
            {
                try { world.Step(1f); }
                catch (PopulationRunawayException) { break; }

                foreach (Organism creature in world.Living) Assert.True(creature.Energy >= 0f);
            }
        }

        [Fact]
        public void AMarginDelaysTheFirstBirthAndIsExactlyWhatTheParentKeeps()
        {
            // D098 §3, both halves of it: a parent that keeps a margin breeds later than the same
            // parent that keeps none, and what it is left holding afterwards is the margin.
            //
            // The two arms are one world. Pinning the founder range to a single value still draws
            // one number per founder, so the genome stream is identical across the arms and the
            // only difference between them is what that number is — which is the comparison
            // CLAUDE.md's wingspan rule says a per-step change can never be given.
            (double firstBirth, long underMargin, float kept, float owed) Arm(float margin)
            {
                var config = new RunConfig
                {
                    MinimumPopulation = 30,
                    MaximumPopulation = 5000,
                    Light = new LightModel(300f, 12f),
                    Genome = new RandomGenomeOptions
                    {
                        MinReserveMargin = margin, MaxReserveMargin = margin,
                    },
                };

                var world = new World(config, seed: 5);

                for (int i = 0; i < 1200; i++)
                {
                    try { world.Step(1f); }
                    catch (PopulationRunawayException) { break; }

                    long parentId = -1;
                    foreach (LineageEvent evt in world.DrainLineageEvents())
                    {
                        if (evt.Kind != LineageEventKind.Birth) continue;
                        if (evt.BirthKind != BirthKind.Reproduction) continue;

                        Assert.Equal(margin, evt.ReserveMargin);
                        parentId = evt.ParentId;
                        break;
                    }

                    if (parentId < 0) continue;

                    // Read in the same step the birth happened in, before the next step's
                    // metabolism touches the reserve. A parent that has just paid for its whole
                    // litter holds the gate less the price, and the gate is the price plus the
                    // margin — so this is the margin, and anything less is the gate leaking.
                    foreach (Organism parent in world.Living)
                    {
                        if (parent.Id != parentId) continue;
                        return (world.ElapsedSeconds, world.ConceptionsUnderMargin,
                                parent.Energy, margin * parent.StandingWatts);
                    }

                    // The parent died in the same step it bred. Rare, and not this test's
                    // question — keep looking.
                }

                return (double.PositiveInfinity, world.ConceptionsUnderMargin, 0f, 0f);
            }

            var eager = Arm(0f);
            var cautious = Arm(600f);

            _output.WriteLine(
                $"first birth: {eager.firstBirth:0} s keeping nothing, {cautious.firstBirth:0} s " +
                $"keeping 600 s — the cautious parent held {cautious.kept:0.##} J against the " +
                $"{cautious.owed:0.##} J its margin asks for");

            Assert.True(eager.firstBirth < double.PositiveInfinity, "the eager world never bred at all");
            Assert.True(
                cautious.firstBirth > eager.firstBirth,
                $"a 600 s margin did not delay the first birth: {cautious.firstBirth} s against " +
                $"{eager.firstBirth} s");

            Assert.True(cautious.owed > 0f, "600 s of a real standing cost is not nothing");
            Assert.True(
                cautious.kept >= 0.99f * cautious.owed,
                $"the parent kept {cautious.kept} J where its margin asks for {cautious.owed} J");

            // And the gate refuses nothing the threshold did not already refuse. The two are one
            // expression (Organism.ReproductionThreshold), so this counter reads 0 while they
            // agree and is the instrument that says so — a nonzero here means a price has been
            // changed on one side of the pair and not the other, which is how the fixed matter
            // term went wrong in D065. It is not a measure of how hard the margin bites.
            Assert.Equal(0L, eager.underMargin);
            Assert.Equal(0L, cautious.underMargin);
        }

        [Fact]
        public void ReproductionCostsExactlyWhatTheGenomeSays()
        {
            // investment * body + n * overhead — §5A.6, §5A.2c and fable-propose-growth.md rule 2.
            // The overhead is spent rather than transferred, which is what makes brood size a
            // trait selection can act on: without it, one brood of four and four broods of one are
            // the same transaction. The investment term is what stops offspring size being free,
            // and it is the parent's own body because that is what it is spending a share of.
            //
            // That the world charges this correctly is proven by EnergyIsConservedAcrossTheWholeRun
            // rather than here — a reproduction priced wrong would not close the books. This checks
            // only that a creature's threshold is its own genome's and its own body's number and
            // not a global one.
            var world = Run(new RunConfig { MinimumPopulation = 20 }, new LightModel(), seconds: 30f);

            Assert.NotEmpty(world.Living);

            foreach (Organism creature in world.Living)
            {
                ReproductionTraits traits = creature.Genome.Reproduction;

                // Plus D098 §3's margin: the price is what the litter costs, the margin is what
                // the parent will not be left without, and the threshold is the sum. In seconds
                // of this body's own standing cost, so it is this creature's number twice over.
                Assert.Equal(
                    traits.BirthInvestment * creature.TissueJoules + traits.BroodSize * 25f +
                    traits.ReserveMargin * creature.StandingWatts,
                    creature.ReproductionThreshold(25f), 3);

                // A larger brood must cost more, or brood size is a free parameter and every
                // lineage converges on the largest one it can express.
                Assert.True(creature.ReproductionThreshold(50f) > creature.ReproductionThreshold(25f));

                // And a body must cost something to build, or offspring size is the free
                // parameter instead and every lineage converges on the largest of those.
                Assert.True(creature.TissueJoules > 0f, "a body with no worth is a body with no price");
            }
        }

        [Fact]
        public void AWorldCanReproduceAndReachGenerationOne()
        {
            // The mechanism, not the calibration — finding the real ratio is the sweep in §5A.6b
            // and belongs in a harness rather than an assertion. All this shows is that the loop
            // is capable of producing a birth at all, so a sweep that finds nothing is telling us
            // about the ratio rather than about a broken loop.
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 400 };
            config.Light = new LightModel(4000f, 40f);
            var world = new World(config, seed: 1);

            WorldSample sample = default;
            bool exploded = false;

            try
            {
                for (int i = 0; i < 3000; i++)
                {
                    world.Step(1f);
                    if (i % 50 != 49) continue;

                    sample = WorldStats.Sample(world);
                    if (sample.MaxDepth > 0) break;
                }
            }
            catch (PopulationRunawayException e)
            {
                exploded = true;
                _output.WriteLine($"runaway: {e.Population} at t={e.ElapsedSeconds:0.#}");
            }

            _output.WriteLine(sample.ToString());
            _output.WriteLine($"floor {sample.FloorSpawns} births {sample.Births} deaths {sample.Deaths}");

            Assert.True(exploded || sample.MaxDepth > 0, "no lineage ever reached generation 1");
        }

        [Fact]
        public void AnOverfedWorldStopsLoudlyRatherThanBeingCulled()
        {
            // §5A.7's photosynthetic mat. It does not go extinct, it explodes — and culling to
            // fit a compute budget would be selection performed by us, hiding a calibration
            // failure behind a population number we chose.
            //
            // This was found by a test hanging rather than by design: the ceiling was written
            // into D021 and never implemented, so the first genuinely over-lit world ran until
            // it was killed by hand.
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 300 };
            config.Light = new LightModel(50000f, 60f);
            var world = new World(config, seed: 1);

            var thrown = Assert.Throws<PopulationRunawayException>(() =>
            {
                for (int i = 0; i < 5000; i++) world.Step(1f);
            });

            _output.WriteLine(thrown.Message);

            Assert.True(thrown.Population > config.MaximumPopulation);
            Assert.Contains("§5A.2", thrown.Message);
        }

        [Fact]
        public void ATissueCeilingStopsTheWorldEvenUnderThePopulationCeiling()
        {
            // fable-propose-growth.md rule 9, the owner's ruling of 2026-09-09. Growth means a
            // body count no longer measures biomass: a newborn is grown from a fraction of its
            // adult body, so a world can sit under RunConfig.MaximumPopulation and still be the
            // photosynthetic mat §5A.7 describes, read by tissue rather than by count. Population
            // is set far out of reach so the tissue ceiling is the only thing that can fire.
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 100_000 };
            config.Light = new LightModel(50000f, 60f);
            var world = new World(config, seed: 1);

            // Let the floor finish founding before reading a number to build the ceiling from.
            for (int i = 0; i < 15; i++) world.Step(1f);

            double tissueAfterFounding = world.StandingTissueJoules;
            Assert.True(tissueAfterFounding > 0d, "founders should already carry tissue");

            // Set below what is already standing, so the very next step is already over it.
            config.MaximumTissueJoules = tissueAfterFounding * 0.5d;

            var thrown = Assert.Throws<PopulationRunawayException>(() => world.Step(1f));

            _output.WriteLine(thrown.Message);
            Assert.Equal("tissue", thrown.Ceiling);
            Assert.True(thrown.TissueJoules > config.MaximumTissueJoules);
            Assert.True(thrown.Population <= config.MaximumPopulation);
        }

        [Fact]
        public void ATissueCeilingOfZeroNeverFires()
        {
            // The default. A config.json written before this field existed is refused on load
            // (§9), but one written by this build with the field left at 0 must still replay the
            // world it always ran, with no new way for it to stop. Reruns the overfed scenario above,
            // which stands on a great deal of tissue by the time it ends, and checks that what
            // fired was still the population ceiling and never the tissue one.
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 300 };
            config.Light = new LightModel(50000f, 60f);
            Assert.Equal(0d, config.MaximumTissueJoules);
            var world = new World(config, seed: 1);

            var thrown = Assert.Throws<PopulationRunawayException>(() =>
            {
                for (int i = 0; i < 5000; i++) world.Step(1f);
            });

            Assert.Equal("population", thrown.Ceiling);
        }

        [Fact]
        public void DepthStatisticsDescribeTheDistributionAndNotJustTheMean()
        {
            // A takeover and a healthy world have the same mean, so the spread is what is
            // reported. Checked against the real population rather than a synthetic one — a
            // fixture with hand-set depths would test the sorting and not the reading.
            var config = new RunConfig { MinimumPopulation = 25, MaximumPopulation = 400 };
            config.Light = new LightModel(4000f, 40f);
            var world = new World(config);

            var depths = new List<int>();
            for (int i = 0; i < 1500; i++)
            {
                try { world.Step(1f); }
                catch (PopulationRunawayException) { break; }

                depths.Clear();
                foreach (Organism creature in world.Living) depths.Add(creature.GenerationDepth);

                if (depths.Count > 4 && depths[depths.Count - 1] != depths[0]) break;
            }

            depths.Clear();
            foreach (Organism creature in world.Living) depths.Add(creature.GenerationDepth);

            WorldSample sample = WorldStats.Sample(world);
            depths.Sort();

            _output.WriteLine($"{depths.Count} living, depths {depths[0]}..{depths[depths.Count - 1]}");

            Assert.Equal(depths[0], sample.MinDepth);
            Assert.Equal(depths[depths.Count - 1], sample.MaxDepth);
            Assert.Equal(depths[depths.Count / 2], sample.MedianDepth);
            Assert.InRange(sample.MeanDepth, sample.MinDepth, sample.MaxDepth);
        }

        [Fact]
        public void ASampleIsExactlyOneLineOfJson()
        {
            // One row must be one line, or an embedded newline makes every row after it
            // unreadable (§9).
            var world = Run(new RunConfig(), new LightModel(), seconds: 30f);
            string row = WorldStats.Sample(world).ToJson();

            _output.WriteLine(row);

            Assert.DoesNotContain('\n', row);
            Assert.DoesNotContain('\r', row);
            Assert.NotNull(Json.Parse(row));
        }

        [Fact]
        public void TheSameSeedGivesTheSameWorld()
        {
            // §7. Without this, no sweep of the calibration ratio means anything, because two
            // runs differing only in the knob could differ for any other reason too.
            string First(ulong seed)
            {
                var world = new World(new RunConfig(), seed);
                for (int i = 0; i < 200; i++) world.Step(1f);
                return WorldStats.Sample(world).ToJson();
            }

            Assert.Equal(First(7), First(7));
            Assert.NotEqual(First(7), First(8));
        }

        // ------------------------------------------------------------ D098: one substance

        /// <summary>
        /// A world whose water holds spent matter, with the economy's knobs at the spec's values.
        /// </summary>
        /// <remarks>
        /// Every test below sets only what it is about and reads the rest from here, so a change
        /// to a default shows up as one failure with a name rather than as twelve.
        /// </remarks>
        private static RunConfig Economy(float spentPerCubicMetre = 1f) =>
            new RunConfig
            {
                Light = new LightModel(200f, 12f),
                InitialMatterPerCubicMetre = spentPerCubicMetre,
            };

        /// <summary>One body, alone, with no floor and no breeding: a leg measured on its own.</summary>
        private static World OneLeaf(RunConfig config, out Organism creature, float heightY = -1f)
        {
            config.MinimumPopulation = 0;
            config.PerOffspringOverheadJoules = 1e9f; // nothing can afford a child

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(), count: 1, heightY: heightY);

            creature = world.Living[0];
            return world;
        }

        /// <summary>Both identities, each against the scale of its own book.</summary>
        /// <remarks>
        /// <b>Relative, because both totals are sums of floats over thousands of steps.</b> A
        /// world holding 24,000 units and 90 kJ cannot close either book to an absolute 1e-9, and
        /// a test that asks it to is measuring float width rather than a leg. The matter book is
        /// read at 1e-6, which is what the campaign's own reports are read at (CLAUDE.md's
        /// <c>mat resid</c> rule); the energy book is read at 1e-5, because a body's reserve is a
        /// float that every step adds to and subtracts from and a few hundred steps of it drift
        /// by about 2e-6 of the light that went in.
        /// </remarks>
        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-5 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:R} J in");

            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.StandingMatterUnits),
                $"matter residual {world.MatterResidual:R} of {world.StandingMatterUnits:R} units");
        }

        /// <summary>A one-part box of one cell type, in the shape <see cref="AbsorptiveBlob"/> uses.</summary>
        private static Genome OneBox(string cellTypeId)
        {
            var g = new Genome
            {
                Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f },
            };

            g.Nodes.Add(new MorphNode
            {
                CellTypeId = cellTypeId,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });

            g.RootIndex = 0;
            return g;
        }

        /// <summary>A leaf: one photosynthetic box, which is the only body that runs leg 1.</summary>
        private static Genome Leaf() => OneBox(CellTypeIds.Photosynthetic);

        /// <summary>A mouth: one absorptive box, which is the only body that runs leg 3.</summary>
        private static Genome Mouth() => OneBox(CellTypeIds.Absorptive);

        /// <summary>The standard types with an absorptive cell that keeps half of what it clears.</summary>
        /// <remarks>
        /// The default yield is 1, at which a mouth wastes nothing and the faeces leg has nothing
        /// to move — so a test about faeces has to ask for a feeder that loses something.
        /// </remarks>
        private static CellTypeRegistry HalfYieldRegistry() =>
            new CellTypeRegistry(
                new StructuralCell(),
                new LinkCell(),
                new NeuralCell(),
                new PhotosyntheticCell(),
                new AbsorptiveCell(1f, 4f, 0.5f),
                new ConsumerCell());

        // ---- leg 1: fixation

        [Fact]
        public void FixationTakesExactlyWhatItCreditsOverRho()
        {
            // The leg in one step. What the body's ledger says it fixed is what left the spent
            // field, to the last float over rho — the two halves of one transfer, which is what
            // makes the matter identity a check on the energy audit rather than a second story.
            World world = OneLeaf(Economy(), out Organism creature);

            double spentBefore = world.Matter.TotalJoules;
            double inBefore = world.EnergyIn;

            world.Step(1f);

            double fixedJoules = world.EnergyIn - inBefore;
            double unitsTaken = spentBefore - world.Matter.TotalJoules;
            double burnt = world.BurntTotal;

            _output.WriteLine(
                $"fixed {fixedJoules:0.######} J, spent field {spentBefore:0.######} -> " +
                $"{world.Matter.TotalJoules:0.######} units, burnt {burnt:0.######} units back");

            Assert.True(fixedJoules > 0d, "nothing was fixed, so the leg was not exercised");

            // The burn of the same step puts spent units back, so the field's move is the
            // difference of the two legs and the assertion has to name both.
            double expected = fixedJoules / world.Config.JoulesPerUnit - burnt;
            Assert.True(
                Math.Abs(expected - unitsTaken) <= 1e-6 * Math.Max(1d, world.Matter.TotalJoules),
                $"the spent field moved {unitsTaken:R} where the ledger says {expected:R}");

            AssertBooksClose(world);
        }

        [Fact]
        public void LightBindsInRichWaterAndUptakeBindsInStrippedWater()
        {
            // min(L, U x rho), read at the two ends. Rich water gives the pre-D098 light leg
            // exactly; water stripped to a hundredth of the half-saturation gives a hundredth of
            // the uptake, and the leaf fixes that instead.
            World rich = OneLeaf(Economy(spentPerCubicMetre: 10f), out Organism fed);
            World poor = OneLeaf(Economy(spentPerCubicMetre: 5e-4f), out Organism starved);

            // Measured from here, because a founder's own start is booked as income too (leg 9)
            // and reading EnergyIn whole would be reading the experimenter's hand.
            double richBefore = rich.EnergyIn;
            double poorBefore = poor.EnergyIn;

            rich.Step(1f);
            poor.Step(1f);

            double richFixed = rich.EnergyIn - richBefore;
            double poorFixed = poor.EnergyIn - poorBefore;

            _output.WriteLine(
                $"rich: {richFixed:0.######} J fixed, uptake bound on " +
                $"{rich.UptakeLimitedSteps} of {rich.PhotosyntheticSteps} steps; " +
                $"poor: {poorFixed:0.######} J, {poor.UptakeLimitedSteps} of " +
                $"{poor.PhotosyntheticSteps}");

            Assert.Equal(1L, rich.PhotosyntheticSteps);
            Assert.Equal(0L, rich.UptakeLimitedSteps);

            Assert.Equal(1L, poor.PhotosyntheticSteps);
            Assert.Equal(1L, poor.UptakeLimitedSteps);
            Assert.True(poorFixed < richFixed * 0.1d, "stripped water fed the leaf anyway");

            // And rich water is the plain light leg: the whole of what the cell offered.
            // Read off the ledger, not off LightModel.IrradianceAt, which is the water's
            // irradiance and not the body's — LightField shades a body by what is above it,
            // so recomputing the capacity by hand measures the shading and not the leg.
            // Lifetime rather than LastLedger: World only records LastLedger for a body with
            // absorptive tissue, and this one is a leaf. Over one step the two are the same sum.
            Assert.Equal(fed.Lifetime.LightCapacity, (float)richFixed, 4);
            Assert.False(fed.Lifetime.UptakeLimited, "rich water bound the leaf anyway");
            Assert.NotNull(starved);
        }

        [Fact]
        public void UptakeOffIsThePreD098LightLegExactly()
        {
            // The knob at 0 is "unbounded uptake": the ceiling is never computed, so the cell
            // fixes its whole light capacity. It still books a unit of spent matter for every
            // rho joules it fixes, so the leg is off and the substance is not.
            RunConfig rich = Economy(spentPerCubicMetre: 10f);
            rich.UptakeRatePerSquareMetre = 0f;

            World world = OneLeaf(rich, out Organism creature);
            double before = world.EnergyIn;
            world.Step(1f);
            double fixedJoules = world.EnergyIn - before;

            // Lifetime rather than LastLedger, which World records for absorptive bodies only.
            float capacity = creature.Lifetime.LightCapacity;
            _output.WriteLine($"capacity {capacity:0.######} J, fixed {fixedJoules:0.######} J");

            Assert.True(capacity > 0f, "the leaf was in the dark, so the knob proves nothing");
            Assert.Equal(capacity, (float)fixedJoules, 4);
            Assert.False(creature.Lifetime.UptakeLimited);
            Assert.Equal(0L, world.UptakeLimitedSteps);
            Assert.Equal(0L, world.FixationShortTakes);
            AssertBooksClose(world);

            // And the same knob over water with nothing in it fixes nothing, because the leg is
            // off and the substance is not: the rationing pass over the spent field finds no
            // share to give and zeroes the income before any take is attempted. That is why
            // FixationShortTakes stays at 0 here too — the shortfall is priced, not taken.
            RunConfig empty = Economy(spentPerCubicMetre: 0f);
            empty.UptakeRatePerSquareMetre = 0f;

            World dry = OneLeaf(empty, out Organism starved);
            double dryBefore = dry.EnergyIn;
            dry.Step(1f);

            _output.WriteLine(
                $"dry: fixed {dry.EnergyIn - dryBefore:0.######} J, " +
                $"short takes {dry.FixationShortTakes}");

            Assert.Equal(0d, dry.EnergyIn - dryBefore, 9);
            Assert.True(starved.Lifetime.LightCapacity > 0f, "the dry leaf was in the dark");
            AssertBooksClose(dry);
        }

        // ---- leg 2: burning

        [Fact]
        public void BurningDepositsExactlyWhatItSpendsOverRho()
        {
            // Every joule spent is a charged unit burnt, and the spent unit goes into the water
            // where the body is. D052's excretion was this leg with a knob; it is now the rate
            // 1 / rho and has none.
            World world = OneLeaf(Economy(), out Organism creature);

            double outBefore = world.EnergyOut;
            double burntBefore = world.BurntTotal;

            world.Step(1f);

            double spent = world.EnergyOut - outBefore;
            double returned = world.BurntTotal - burntBefore;

            _output.WriteLine($"burnt {spent:0.######} J, returned {returned:0.######} units");

            Assert.True(spent > 0d, "nothing was spent, so the leg was not exercised");
            Assert.Equal(spent / world.Config.JoulesPerUnit, returned, 9);
        }

        [Fact]
        public void ABodyCannotBurnWhatItDoesNotHoldAndDiesAtZero()
        {
            // The clamp that replaced the negative-reserve-then-check shape. A body in the dark
            // spends its reserve and stops at exactly 0; it never carries a debt the world has no
            // way to settle, and it dies on the step it runs out.
            RunConfig config = Economy();
            config.Light = new LightModel(1e-9f, 1f);

            World world = OneLeaf(config, out Organism creature);

            int steps = 0;
            while (world.Living.Count > 0 && steps < 100_000)
            {
                world.Step(1f);
                steps++;
                foreach (Organism c in world.Living) Assert.True(c.Energy >= 0f, "a body went into debt");
            }

            _output.WriteLine($"starved after {steps} s; audit {world.AuditResidual:R}");

            Assert.Empty(world.Living);
            Assert.Equal(1L, world.Deaths);
            Assert.Equal(0f, creature.Energy);
            AssertBooksClose(world);
        }

        // ---- leg 3: eating

        [Fact]
        public void FaecesReachTheChargedFieldAndNeverTheAuditsOutflow()
        {
            // What a mouth tears up and does not keep used to leave the world as heat. It is
            // charged matter, so it goes back into the water where the feeder is, and the
            // transfer loss that shortens a food chain is now somebody else's meal.
            var config = new RunConfig
            {
                Light = new LightModel(1e-9f, 1f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                CellTypes = HalfYieldRegistry(),
            };

            config.RemineralisationPerSecond = 0f; // so the step's only outflow is the burn

            var world = new World(config, seed: 3);
            world.Inoculate(Mouth(), count: 1, heightY: -1f);

            // The meal is put in the water by hand, which no leg of the economy does, so both
            // books open by exactly that deposit before the step under test runs. What is
            // asserted below is that the step did not move them further.
            world.Nutrients.Deposit(world.Living[0].Point, 5_000f);

            double auditBefore = world.AuditResidual;
            double matterBefore = world.MatterResidual;
            double returnedBefore = world.DetritusReturnedTotal;
            double outBefore = world.EnergyOut;
            double burntBefore = world.BurntTotal;
            Organism feeder = world.Living[0];

            world.Step(1f);

            EnergyLedger ledger = feeder.LastLedger;
            double returned = world.DetritusReturnedTotal - returnedBefore;

            _output.WriteLine(
                $"drew {ledger.PoolDrawn:0.######} J, kept {ledger.FoodIncome:0.######} J, " +
                $"returned {returned:0.######} J; out {world.EnergyOut - outBefore:0.######} J");

            Assert.True(ledger.PoolDrawn > 0f, "the mouth drew nothing, so the leg was not exercised");
            Assert.True(ledger.Wasted > 0f, "the yield was 1, so there was nothing to return");
            Assert.Equal(ledger.Wasted, returned, 4);

            // The outflow is the burn alone: the faeces are still in the world.
            Assert.Equal(
                (world.BurntTotal - burntBefore) * world.Config.JoulesPerUnit,
                world.EnergyOut - outBefore, 4);

            // Both books are open by the hand-seeded meal and by nothing else, so what is asked
            // of them is that the step did not move them, not that they close.
            Assert.Equal(auditBefore, world.AuditResidual, 3);
            Assert.Equal(matterBefore, world.MatterResidual, 3);
        }

        [Fact]
        public void HandlingIsChargedOnTheDrawAndIsBurnt()
        {
            // Eating is not free. The cost is on what the mouth cleared, not on what it kept, so
            // a feeder in water it cannot assimilate still pays — and it is an expenditure like
            // any other, which means it burns and returns its spent unit.
            var withCost = new RunConfig
            {
                Light = new LightModel(1e-9f, 1f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                HandlingCostPerJouleEaten = 0.25f,
            };

            var free = new RunConfig
            {
                Light = new LightModel(1e-9f, 1f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                HandlingCostPerJouleEaten = 0f,
            };

            EnergyLedger Feed(RunConfig config)
            {
                var world = new World(config, seed: 3);
                world.Inoculate(Mouth(), count: 1, heightY: -1f);
                world.Nutrients.Deposit(world.Living[0].Point, 5_000f);
                world.Step(1f);
                return world.Living[0].LastLedger;
            }

            EnergyLedger paid = Feed(withCost);
            EnergyLedger unpaid = Feed(free);

            _output.WriteLine(
                $"handling {paid.Handling:0.######} J on a draw of {paid.PoolDrawn:0.######} J; " +
                $"free world's handling {unpaid.Handling:0.######} J");

            Assert.Equal(0f, unpaid.Handling);
            Assert.Equal(0.25f * paid.PoolDrawn, paid.Handling, 4);
            Assert.True(paid.Expenditure > unpaid.Expenditure, "handling never reached the expenditure");
            Assert.Equal(paid.Upkeep + paid.Neural + paid.Work + paid.Handling, paid.Expenditure, 5);
        }

        // ---- legs 5 and 6: a child, and growth

        [Fact]
        public void AChildMovesItsPriceFromTheParentAndBurnsTheOverheadAlone()
        {
            // The price is tissue + reserve + overhead. The first two are charged matter handed
            // over and stay in the world; the third is burnt at the parent's point. Nothing is
            // drawn from any field, which is the whole of what D098 took away from conception.
            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                MinimumPopulation = 0,
                InitialMatterPerCubicMetre = 50f,
            };

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(), count: 1, heightY: -0.5f);

            double residual = world.MatterResidual;
            long births = 0;

            for (int step = 0; step < 4_000 && births == 0; step++)
            {
                double outBefore = world.EnergyOut;
                double burntBefore = world.BurntTotal;

                world.Step(1f);

                births = world.Births;
                if (births == 0) continue;

                // The overhead is burnt once per birth, on top of whatever the metabolic step
                // burnt; the identity below is what says nothing else left.
                double spent = world.EnergyOut - outBefore;
                double returned = world.BurntTotal - burntBefore;

                _output.WriteLine(
                    $"a birth at t={world.ElapsedSeconds:0} s: {spent:0.###} J out, " +
                    $"{returned:0.###} units returned, residual {world.MatterResidual:R}");

                Assert.True(
                    spent >= config.PerOffspringOverheadJoules,
                    "the overhead was not burnt");
                Assert.Equal(spent / config.JoulesPerUnit, returned, 6);
            }

            Assert.True(births > 0, "nothing bred, so the leg was never exercised");
            Assert.Equal(residual, world.MatterResidual, 4);
            AssertBooksClose(world);
        }

        [Fact]
        public void GrowthDrawsNothingFromAnyField()
        {
            // Rule 5's transfer, under one substance: reserve into tissue, and both are summed by
            // StandingJoules, so neither identity learns that growth happened.
            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                InitialMatterPerCubicMetre = 50f,
            };

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(), count: 1, heightY: -0.5f);

            Organism creature = world.Living[0];
            float tissueBefore = creature.TissueJoules;

            for (int step = 0; step < 300; step++) world.Step(1f);

            _output.WriteLine(
                $"tissue {tissueBefore:0.####} -> {creature.TissueJoules:0.####} J, fraction " +
                $"{creature.BodyFraction:0.####}, residual {world.MatterResidual:R}");

            Assert.True(creature.TissueJoules > tissueBefore, "nothing grew");
            AssertBooksClose(world);
        }

        // ---- leg 7: death

        [Fact]
        public void ACorpseCarriesTissueAndReserveTogether()
        {
            // A diverged body is generally solvent, and its savings used to be booked out as
            // heat. Charged matter does not evaporate: the corpse is worth what the body was.
            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                InitialMatterPerCubicMetre = 50f,
                CorpseDecayPerSecond = 0.01f,
            };

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(), count: 1, heightY: -0.5f);

            for (int step = 0; step < 200; step++) world.Step(1f);

            Organism victim = world.Living[0];
            float reserve = victim.Energy;
            float tissue = victim.TissueJoules;

            Assert.True(reserve > 0f, "the body had no reserve, so the test measures half a rule");

            double outBefore = world.EnergyOut;
            world.KillDiverged(victim);

            _output.WriteLine(
                $"buried {tissue:0.####} J of tissue and {reserve:0.####} J of reserve into a " +
                $"corpse of {world.CorpseJoules:0.####} J");

            Assert.Single(world.Corpses);
            Assert.Equal(tissue + reserve, world.CorpseJoules, 4);
            Assert.Equal(outBefore, world.EnergyOut, 6);
            AssertBooksClose(world);
        }

        // ---- leg 8: remineralisation

        [Fact]
        public void RemineralisationMovesTheFractionAndBooksTheHeat()
        {
            // The bacteria: charged matter in the water decays into spent matter in the same
            // place, the joules leave the world and the units arrive. Measured over a step with
            // nothing alive in it, so the only thing moving is the leg.
            var config = new RunConfig
            {
                Light = new LightModel(1e-9f, 1f),
                MinimumPopulation = 0,
                InitialMatterPerCubicMetre = 0f,
                RemineralisationPerSecond = 0.01f,
                NutrientSinkMetresPerSecond = 0f,
                MatterSinkMetresPerSecond = 0f,
                NutrientMixingDiffusivity = 0f,
                MatterMixingDiffusivity = 0f,
            };

            var world = new World(config, seed: 3);
            world.Nutrients.Deposit(FieldPoint.At(-10.5f, 0), 10_000f);

            // Seeded by hand, so both books open by that deposit and what matters is that the
            // leg below moves neither of them further.
            double auditBefore = world.AuditResidual;
            double matterBefore = world.MatterResidual;

            double chargedBefore = world.Nutrients.TotalJoules;
            double outBefore = world.EnergyOut;

            world.Step(1f);

            double moved = chargedBefore - world.Nutrients.TotalJoules;
            double expected = chargedBefore * (1d - Math.Exp(-(double)config.RemineralisationPerSecond * 1d));

            _output.WriteLine(
                $"moved {moved:0.######} J of {chargedBefore:0.###}; spent field holds " +
                $"{world.Matter.TotalJoules:0.######} units");

            Assert.Equal(expected, moved, 4);
            Assert.Equal(moved, world.RemineralisedTotal, 4);
            Assert.Equal(moved, world.EnergyOut - outBefore, 4);
            Assert.Equal(moved / config.JoulesPerUnit, world.Matter.TotalJoules, 6);
            Assert.Equal(auditBefore, world.AuditResidual, 4);
            Assert.Equal(matterBefore, world.MatterResidual, 4);
        }

        // ---- leg 9: founders

        [Fact]
        public void AFounderIsAnInfluxInBothBooks()
        {
            // The experimenter's hand is a source, and the books say so in both units: the joules
            // are created and the same over rho is credited as matter flowing in, so a founding
            // lottery takes nothing out of the water and opens neither identity.
            var config = new RunConfig
            {
                Light = new LightModel(1e-9f, 1f),
                MinimumPopulation = 0,
                InitialMatterPerCubicMetre = 1f,
            };

            var world = new World(config, seed: 3);

            double inBefore = world.EnergyIn;
            double influxBefore = world.MatterInfluxedTotal;
            double spentBefore = world.Matter.TotalJoules;

            world.Inoculate(Leaf(), count: 1, heightY: -1f);

            double created = world.EnergyIn - inBefore;
            double credited = world.MatterInfluxedTotal - influxBefore;

            _output.WriteLine(
                $"created {created:0.######} J, credited {credited:0.######} units of influx");

            Assert.True(created > 0d, "nothing was created, so the leg was not exercised");
            Assert.Equal(created / config.JoulesPerUnit, credited, 6);
            Assert.Equal(spentBefore, world.Matter.TotalJoules, 9);
            AssertBooksClose(world);
        }

        // ---- leg 10: the reserve cap

        [Fact]
        public void TheReserveCapTrimsAHoardIntoTheWaterAndIsOffAtZero()
        {
            // The lever: above the cap a body's savings go into the larder while it still lives,
            // instead of being burnt as senescence upkeep and leaving the world as heat.
            RunConfig Config(float cap) => new RunConfig
            {
                Light = new LightModel(400f, 12f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = 1e9f,
                InitialMatterPerCubicMetre = 50f,
                ReserveCapSeconds = cap,
            };

            World capped = OneLeafWorld(Config(60f));
            World uncapped = OneLeafWorld(Config(0f));

            for (int step = 0; step < 600; step++)
            {
                capped.Step(1f);
                uncapped.Step(1f);
            }

            Organism held = capped.Living[0];

            _output.WriteLine(
                $"capped: reserve {held.Energy:0.####} J against a cap of " +
                $"{60f * held.StandingWatts:0.####} J, trimmed {capped.ReserveTrimmedTotal:0.####} J; " +
                $"uncapped: {uncapped.Living[0].Energy:0.####} J, trimmed " +
                $"{uncapped.ReserveTrimmedTotal:0.####} J");

            Assert.Equal(0d, uncapped.ReserveTrimmedTotal);
            Assert.True(capped.ReserveTrimmedTotal > 0d, "the cap never fired");
            Assert.True(held.Energy <= 60f * held.StandingWatts + 1e-3f, "the reserve ran past its cap");

            // The trim is charged matter released by a living body, so it is counted with D070's
            // exudation and it is in the water rather than gone.
            Assert.True(capped.DetritusExudedTotal >= capped.ReserveTrimmedTotal);
            AssertBooksClose(capped);
        }

        private static World OneLeafWorld(RunConfig config)
        {
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(), count: 1, heightY: -0.5f);
            return world;
        }

        // ---- the two identities, over a world with every leg running

        [Fact]
        public void AGridTankWithEveryLegRunningClosesBothBooks()
        {
            // The whole economy at once, in the campaign's shape: a tank on a grid, fixation
            // bound by uptake, handling, exudation, remineralisation, corpses, growth, breeding
            // and deaths. Either identity alone can be closed by a leg that did one half of a
            // transfer; the pair cannot.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 6f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                WorldDepthMetres = 20f,
                HorizontalPatches = 4f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 1f,
                MatterBudgetUnits = 400f,
                FloorClosesAfterSeconds = 500f,

                RemineralisationPerSecond = 5e-4f,
                HandlingCostPerJouleEaten = 0.1f,
                ReserveCapSeconds = 600f,
                ExudationFraction = 0.15f,
                CorpseDecayPerSecond = 0.01f,
                SenescenceDoublingSeconds = 3000f,

                NutrientMixingDiffusivity = 0.2f,
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                NutrientSinkMetresPerSecond = 0.002f,
                MatterSinkMetresPerSecond = 0.002f,
            };

            var world = new World(config, seed: 5);

            for (int step = 0; step < 6_000; step++) world.Step(0.5f);

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, " +
                $"corpses {world.Corpses.Count}; fixed {world.EnergyIn:0} J, burnt " +
                $"{world.BurntTotal:0.###} units, remineralised {world.RemineralisedTotal:0.###} J, " +
                $"faeces {world.DetritusReturnedTotal:0.###} J, trimmed " +
                $"{world.ReserveTrimmedTotal:0.###} J; uptake bound on {world.UptakeLimitedSteps} " +
                $"of {world.PhotosyntheticSteps} photosynthetic body-steps; " +
                $"audit {world.AuditResidual:R} of {world.EnergyIn:0}, matter " +
                $"{world.MatterResidual:R} of {world.MatterInitialTotal:0}");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.True(world.Deaths > 0, "nothing died, so the corpse leg never ran");
            Assert.True(world.BurntTotal > 0d, "nothing burnt");
            Assert.True(world.RemineralisedTotal > 0d, "remineralisation never fired");
            Assert.True(world.PhotosyntheticSteps > 0, "nothing photosynthesised");

            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:R} J in");

            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R} of {world.MatterInitialTotal:R} units");
        }

        [Fact]
        public void SuccessAtADepthStripsThatDepth()
        {
            // The feedback the world had nowhere, moved from conception to fixation: earning
            // somewhere makes that somewhere worse. Founders are scattered through the lit zone
            // and fix there, so the layers they occupy must end up poorer in spent matter than
            // the ones they do not.
            RunConfig config = Economy();
            config.MatterMixingDiffusivity = 0f;   // isolate the draw from the stirring
            config.MatterSinkMetresPerSecond = 0f; // and from the falling

            var world = new World(config, seed: 1);
            double[] before = LayerMatter(world);
            for (float t = 0f; t < 600f; t += 1f) world.Step(1f);
            double[] after = LayerMatter(world);

            int stripped = 0, untouched = 0;
            for (int i = 0; i < after.Length; i++)
            {
                if (after[i] < before[i] - 1e-9) stripped++;
                else untouched++;
            }

            _output.WriteLine(
                $"{stripped} layers depleted, {untouched} untouched, {world.Births} births, " +
                $"uptake bound on {world.UptakeLimitedSteps} of {world.PhotosyntheticSteps} steps");

            Assert.True(
                stripped > 0,
                "no layer lost matter — fixation is not drawing from where the body is");
            Assert.True(
                untouched > 0,
                "every layer was depleted equally, which means the draw is not local and the " +
                "gradient the economy exists to create cannot form");
        }

        private static double[] LayerMatter(World world)
        {
            var layers = new double[world.Matter.LayerCount];
            for (int i = 0; i < layers.Length; i++) layers[i] = world.Matter.StockInLayer(i);
            return layers;
        }

        // ------------------------------------------------------------ D055: seabed refuge

        [Fact]
        public void AWorldWithNoFloorRefugeIsBitIdenticalToOneThatNeverHeardOfTheKnob()
        {
            // Default 0, and 0 has to mean bit-identical rather than nearly — every result on
            // file was measured against a fully grazeable floor, and a default that perturbed
            // anything would mean none of them describe a world that still exists (D031 is why
            // that is not a thing to do twice deliberately). "Never heard of the knob" and
            // "explicitly told 0" are the same RunConfig value, so any divergence here is a bug
            // in how the refuge reaches the field, not in the biology.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var samples = new System.Text.StringBuilder();
                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    samples.AppendLine(WorldStats.Sample(world).ToJson());
                }
                return samples.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(300f, 12f) };
            var explicitZero = new RunConfig { Light = new LightModel(300f, 12f), FloorRefugeMetres = 0f };

            Assert.Equal(0f, unset.FloorRefugeMetres);

            Assert.Equal(Trajectory(unset), Trajectory(explicitZero));
        }

        [Fact]
        public void EnergyIsConservedWithAFloorRefugeRunning()
        {
            // D055's own copy of D051's guard: the refuge only changes what Demand/Take will
            // register, never what Deposit/Settle/Mix/Remineralise move, so §5A.2's audit must
            // close exactly as it does with the knob off.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                FloorRefugeMetres = 1f,
            };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine(
                $"in {world.EnergyIn:0.###} out {world.EnergyOut:0.###} " +
                $"standing {world.StandingJoules:0.###} residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"a floor refuge opened a hole in the energy audit: {residual:0.###} J unaccounted for");
        }

        // ------------------------------------------------------------ D062: the satiation cap

        [Fact]
        public void SatiationAndClearanceToeAtDefaultAreBitIdenticalToNeverHearingOfEitherKnob()
        {
            // Same shape as AWorldWithNoFloorRefugeIsBitIdenticalToOneThatNeverHeardOfTheKnob:
            // "never set" and "explicitly 0" are the same RunConfig value, so any divergence in
            // the trajectory is a bug in how the cap and the toe reach AbsorptiveCell.Acquire via
            // CellContext, not in the biology.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var samples = new System.Text.StringBuilder();
                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    samples.AppendLine(WorldStats.Sample(world).ToJson());
                }
                return samples.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(300f, 12f) };
            var explicitZero = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                SatiationWattsPerCubicMetre = 0f,
                ClearanceToeDensity = 0f,
            };

            Assert.Equal(0f, unset.SatiationWattsPerCubicMetre);
            Assert.Equal(0f, unset.ClearanceToeDensity);
            Assert.Equal(Trajectory(unset), Trajectory(explicitZero));
        }

        // ------------------------------------------------------------ Arm C: the refuge fraction

        [Fact]
        public void ARefugeWithFractionZeroIsBitIdenticalToD055sHardRefuge()
        {
            // "Never set" and "explicitly 0" are the same RunConfig value for RefugeEdibleFraction,
            // exactly as they are for FloorRefugeMetres itself (D055) — so a refuge-1 world with the
            // fraction unset must reproduce what the D055 tests already expect of a hard refuge:
            // feeding sees nothing in the refuge layer, however much detritus piles up there.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var samples = new System.Text.StringBuilder();
                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    samples.AppendLine(WorldStats.Sample(world).ToJson());
                }
                return samples.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(300f, 12f), FloorRefugeMetres = 1f };
            var explicitZero = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                FloorRefugeMetres = 1f,
                RefugeEdibleFraction = 0f,
            };

            Assert.Equal(0f, unset.RefugeEdibleFraction);
            Assert.Equal(Trajectory(unset), Trajectory(explicitZero));
        }

        [Fact]
        public void TheRefugeFractionExposesExactlyFractionTimesDensityAndCannotBeDoubleDippedInOneCall()
        {
            // Field-level, not a live world: an impulse into the floor layer, read back through
            // exactly the API feeding uses — EdibleDensityAt, Demand/ShareAt, Take.
            const float worldArea = 100f;
            const float layerMetres = 1f;
            const float worldDepth = 10f;
            const float fraction = 0.3f;

            var field = new NutrientField(
                worldArea: worldArea, layerMetres: layerMetres, sinkMetresPerSecond: 0f,
                worldDepth: worldDepth, refugeMetres: 1f, refugeEdibleFraction: fraction);

            float floorHeightY = -(worldDepth - 0.5f); // the refuge layer, whatever the depth
            field.Deposit(floorHeightY, 10_000f);

            double trueDensity = field.DensityAt(floorHeightY);
            double edibleDensity = field.EdibleDensityAt(floorHeightY);

            Fixtures.AssertClose((float)(trueDensity * fraction), (float)edibleDensity, 1e-3f);

            // A single Take must never remove more than `fraction` of what the layer held just
            // before the call — the self-limiting form the RefugeEdibleFraction doc commits to.
            double preTakeStock = field.StockInLayer(field.LayerCount - 1);
            float taken = field.Take(floorHeightY, joules: 1_000_000f); // demand far beyond the edible share

            double postTakeStock = field.StockInLayer(field.LayerCount - 1);

            _output.WriteLine(
                $"pre {preTakeStock:0.###} J, took {taken:0.###} J, post {postTakeStock:0.###} J, " +
                $"floor at {(1f - fraction) * 100f:0}% or above is untouchable in one call");

            Assert.True(
                postTakeStock >= preTakeStock * (1.0 - fraction) - 1e-3,
                $"one Take call removed more than the {fraction:P0} edible share: {preTakeStock} -> {postTakeStock}");

            Fixtures.AssertClose((float)(preTakeStock * fraction), taken, 1e-2f);
        }

        [Fact]
        public void TheRefugeFractionIsInertWithNoRefugeLayers()
        {
            // FloorRefugeMetres = 0 means no layer is ever a refuge (IsRefuge is false
            // everywhere), so a positive RefugeEdibleFraction has nothing to apply to — the
            // field must read exactly as a fully-open one.
            var field = new NutrientField(
                worldArea: 100f, layerMetres: 1f, sinkMetresPerSecond: 0f, worldDepth: 10f,
                refugeMetres: 0f, refugeEdibleFraction: 0.5f);

            Assert.Equal(0, field.RefugeLayerCount);

            float floorHeightY = -9.5f;
            field.Deposit(floorHeightY, 5_000f);

            Fixtures.AssertClose(field.DensityAt(floorHeightY), field.EdibleDensityAt(floorHeightY), 1e-6f);

            double stockBefore = field.StockInLayer(field.LayerCount - 1);
            float taken = field.Take(floorHeightY, joules: 1_000_000f); // far more than the stock holds

            Fixtures.AssertClose((float)stockBefore, taken, 1e-3f);
            Fixtures.AssertClose(0f, (float)field.StockInLayer(field.LayerCount - 1), 1e-3f);
        }

        // ------------------------------------------------------------ D057: species accounting

        [Fact]
        public void SettingTheDriftThresholdToZeroIsBitIdenticalToNeverHearingOfTheKnob()
        {
            // Same shape as AWorldWithNoFloorRefugeIsBitIdenticalToOneThatNeverHeardOfTheKnob:
            // "never set" and "explicitly 0" are the same RunConfig value, so any divergence in
            // the trajectory is a bug in how the threshold reaches Admit, not in the biology.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var samples = new System.Text.StringBuilder();
                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    samples.AppendLine(WorldStats.Sample(world).ToJson());
                }
                return samples.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(300f, 12f) };
            var explicitZero = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                SpeciesDriftThreshold = 0f,
            };

            Assert.Equal(0f, unset.SpeciesDriftThreshold);
            Assert.Equal(Trajectory(unset), Trajectory(explicitZero));

            // The fast path itself: with the knob off, nothing is ever compared and every
            // creature — founder or offspring — reads species 0.
            var direct = new World(unset, seed: 5);
            for (int i = 0; i < 60; i++) direct.Step(1f);

            Assert.NotEmpty(direct.Living);
            Assert.Empty(direct.Species);
            foreach (Organism creature in direct.Living) Assert.Equal(0u, creature.SpeciesId);
        }

        [Fact]
        public void AChildOneSmallParameterMutationAwayStaysInItsParentsSpeciesAtAGenerousThreshold()
        {
            var config = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 20,
                MaximumPopulation = 400,

                // Generous: even repeated scalar drift across a run this short has no realistic
                // path to a distance this large.
                SpeciesDriftThreshold = 20f,
            };

            // Every operator that could change topology or cell type is switched off, so every
            // birth here really is "one small parameter mutation" and nothing else — only the
            // scalar-perturbation operator fires, and lightly (half the default spread).
            config.Mutation.ScalarChance = 1f;
            config.Mutation.ScalarStdDev = 0.05f;
            config.Mutation.AddNodeChance = 0f;
            config.Mutation.AddEdgeChance = 0f;
            config.Mutation.RemoveEdgeChance = 0f;
            config.Mutation.AddNeuronChance = 0f;
            config.Mutation.RemoveNeuronChance = 0f;
            config.Mutation.RewireInputChance = 0f;
            config.Mutation.NeuronOpChance = 0f;
            config.Mutation.JointTypeChance = 0f;
            config.Mutation.FlagChance = 0f;
            config.Mutation.RecursiveLimitChance = 0f;
            config.Mutation.ShapeChance = 0f;
            config.Mutation.CellTypeChance = 0f;
            config.Mutation.BroodSizeChance = 0f;
            config.Mutation.InvestmentChance = 0f;
            config.Mutation.AdultScaleChance = 0f;

            var world = new World(config, seed: 7);

            // Every id's species, recorded the first time it is seen alive — which is always no
            // later than the step it was born, since Reproduce() runs before EnforceFloor and a
            // parent that reproduced this step is still in Living when this loop reads it. That
            // makes a parent's species readable here even on a later step, after it has died.
            var seenSpecies = new Dictionary<long, uint>();
            int checkedChildren = 0;

            // With almost every operator switched off, light this generous covers upkeep for
            // nearly everybody — §5A.7's photosynthetic mat — so a runaway is an ordinary way for
            // this window to end and not a reason to discard what was already checked.
            try
            {
                for (int i = 0; i < 400; i++)
                {
                    world.Step(1f);

                    foreach (Organism creature in world.Living)
                    {
                        if (creature.ParentId >= 0 &&
                            seenSpecies.TryGetValue(creature.ParentId, out uint parentSpecies))
                        {
                            Assert.Equal(parentSpecies, creature.SpeciesId);
                            checkedChildren++;
                        }

                        seenSpecies[creature.Id] = creature.SpeciesId;
                    }
                }
            }
            catch (PopulationRunawayException)
            {
                // Not the claim under test — see the remark above.
            }

            Assert.True(checkedChildren > 0, "no reproduction happened in this window to check");
        }

        [Fact]
        public void SpeciesIdsReplayIdenticallyForTheSameConfigAndSeed()
        {
            RunConfig Config() => new RunConfig
            {
                // 90 W/m2 rather than 300 since fable-propose-growth.md (2026-09-08): cheaper
                // reproduction carries several times the head-count at the same light, and the
                // world met its ceiling before the two trajectories could be compared.
                Light = new LightModel(90f, 12f),
                MinimumPopulation = 20,
                MaximumPopulation = 300,
                SpeciesDriftThreshold = 2f,
            };

            List<(long Id, uint SpeciesId)> Trajectory()
            {
                var world = new World(Config(), seed: 11);
                var rows = new List<(long, uint)>();

                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    foreach (Organism creature in world.Living) rows.Add((creature.Id, creature.SpeciesId));
                }

                return rows;
            }

            List<(long Id, uint SpeciesId)> first = Trajectory();
            List<(long Id, uint SpeciesId)> second = Trajectory();

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void FloorFoundersEachGetADistinctSpecies()
        {
            var config = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 10,
                FloorSpawnsPerStep = 10,
                SpeciesDriftThreshold = 1f,
            };

            var world = new World(config, seed: 3);
            world.Step(1f); // one step is enough for the whole founding cohort to spawn

            Assert.True(world.Living.Count > 1, "need more than one founder to test distinctness");

            var speciesIds = new HashSet<uint>();
            foreach (Organism creature in world.Living)
            {
                // Still all founders, not reproduction — Reproduce() runs before EnforceFloor,
                // and the population was 0 before this step's floor fired, so nothing could
                // have reproduced yet.
                Assert.Equal(0, creature.GenerationDepth);
                Assert.True(
                    speciesIds.Add(creature.SpeciesId),
                    $"species {creature.SpeciesId} repeated among founders");
            }

            Assert.Equal(world.Living.Count, speciesIds.Count);
        }

        // ------------------------------------------------------------ D060: invasion assay

        /// <summary>The genome the assay injects — a single absorptive box, deterministic to
        /// develop, in the shape of <c>EnergyKnobTests.Leaf</c>.</summary>
        private static Genome AbsorptiveBlob()
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Absorptive,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            g.RootIndex = 0;
            return g;
        }

        /// <summary>
        /// Mirrors the one guard EvolutionRun.cs (the harness) wraps <see cref="World.Inoculate"/>
        /// in: fire once, the first time <see cref="World.ElapsedSeconds"/> crosses
        /// <see cref="RunConfig.InoculateAtSeconds"/>, and never when that is 0 — D060.
        /// </summary>
        private static void MaybeInoculate(World world, RunConfig config, Genome genome, ref bool fired)
        {
            if (fired || config.InoculateAtSeconds <= 0f) return;
            if (world.ElapsedSeconds < config.InoculateAtSeconds) return;

            world.Inoculate(genome, (int)config.InoculateCount, -config.InoculateDepthMetres);
            fired = true;
        }

        [Fact]
        public void InoculateAtSecondsDefaultZeroMeansTheAssayNeverFires()
        {
            // Bit-identical to a world that never heard of D060 at all — every result on file
            // was measured without an inoculation, and a default that fired anything would mean
            // none of them describe a world that still exists (D031 is why that is not a thing
            // to do twice deliberately). The guard here is the one EvolutionRun.cs wraps
            // World.Inoculate in; wiring it up and leaving the knob at its default must be
            // indistinguishable from never having wired it up at all.
            string Trajectory(bool wireTheGuard)
            {
                var config = new RunConfig { Light = new LightModel(300f, 12f) };
                Assert.Equal(0f, config.InoculateAtSeconds);

                var world = new World(config, seed: 5);
                bool fired = false;
                var samples = new System.Text.StringBuilder();

                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    if (wireTheGuard) MaybeInoculate(world, config, AbsorptiveBlob(), ref fired);
                    samples.AppendLine(WorldStats.Sample(world).ToJson());
                }

                Assert.False(fired, "the guard fired despite InoculateAtSeconds being 0");
                Assert.Equal(0, world.Inoculated);
                return samples.ToString();
            }

            Assert.Equal(Trajectory(wireTheGuard: false), Trajectory(wireTheGuard: true));
        }

        [Fact]
        public void InoculateCreditsExactlyWhatItCreatesAndTheAuditStillCloses()
        {
            // D060's own copy of D051/D052/D055's guard. An inoculant is income created from
            // nothing, exactly like a floor founder (World's own remarks on EnergyIn) — so the
            // credit at the moment of the call must be exact, and §5A.2's audit must still close
            // across a run that uses it.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                Light = new LightModel(300f, 12f),
            };
            var world = new World(config, seed: 1);

            for (int i = 0; i < 100; i++) world.Step(1f);

            Genome genome = AbsorptiveBlob();
            Phenotype adult = Developer.Develop(genome, config.Development, null, config.Shapes);
            float adultTissue = Metabolism.TissueJoules(adult, config);

            // fable-propose-growth.md rule 7: an inoculant is born as a child is, at its own
            // genome's birth fraction, with the founder's purse scaled the same way. So the
            // credit is that fraction of both terms rather than the whole of either — the rule
            // written out here rather than read off the creature, since reading the creature
            // would be asking the code under test what it did.
            float wanted = genome.Reproduction.BirthInvestment / genome.Reproduction.BroodSize *
                           (1f - config.NewbornReserveFraction);
            Phenotype newborn = adult.Scaled((float)Math.Pow(wanted, 1d / 3d), config.Shapes);
            float tissue = Metabolism.TissueJoules(newborn, config);
            float expectedCredit = 5 * (config.FounderEnergyJoules * (tissue / adultTissue) + tissue);

            double energyInBefore = world.EnergyIn;
            int livingBefore = world.Living.Count;

            world.Inoculate(genome, count: 5, heightY: -50f);

            Assert.Equal(5, world.Inoculated);
            Assert.Equal(livingBefore + 5, world.Living.Count);
            Fixtures.AssertClose(
                expectedCredit, (float)(world.EnergyIn - energyInBefore), expectedCredit * 1e-4f);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine($"residual {residual:0.######} ({residual / scale:P4})");
            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"an inoculation opened a hole in the energy audit: {residual:0.###} J unaccounted for");
        }

        [Fact]
        public void InoculatedCreaturesFoundTheirOwnSpeciesWhenTheDriftThresholdIsOnAndReadZeroWhenItIsOff()
        {
            // Same shape as FloorFoundersEachGetADistinctSpecies: an inoculant has no parent, so
            // AssignSpecies takes the parent == null branch and founds fresh, exactly as a floor
            // founder does — the species machinery cannot tell the two apart, by D057's own
            // design (it switches on BirthKind nowhere).
            Genome genome = AbsorptiveBlob();

            var on = new World(new RunConfig { SpeciesDriftThreshold = 1f }, seed: 3);
            on.Inoculate(genome, count: 5, heightY: -20f);

            Assert.Equal(5, on.Living.Count);
            var speciesIds = new HashSet<uint>();
            foreach (Organism creature in on.Living)
            {
                Assert.Equal(-1, creature.ParentId);
                Assert.Equal(0, creature.GenerationDepth);
                Assert.True(
                    speciesIds.Add(creature.SpeciesId),
                    $"species {creature.SpeciesId} repeated among inoculants");
            }
            Assert.Equal(5, speciesIds.Count);

            // Threshold off: the fast path, species 0 for everyone, no registry touched.
            var off = new World(new RunConfig { SpeciesDriftThreshold = 0f }, seed: 3);
            off.Inoculate(genome, count: 5, heightY: -20f);

            Assert.Empty(off.Species);
            foreach (Organism creature in off.Living) Assert.Equal(0u, creature.SpeciesId);
        }

        [Fact]
        public void InoculationReplaysIdenticallyForTheSameConfigAndSeed()
        {
            // §7. A pre-registered assay is only worth running once per condition if a second
            // run of the same (genome, seed, configHash) would have told the pre-registration
            // nothing new.
            RunConfig Config() => new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 20,
                MaximumPopulation = 300,
            };

            string Trajectory()
            {
                var world = new World(Config(), seed: 11);
                var samples = new System.Text.StringBuilder();

                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        world.Step(1f);
                        samples.AppendLine(WorldStats.Sample(world).ToJson());
                    }

                    world.Inoculate(AbsorptiveBlob(), count: 5, heightY: -30f);
                    samples.AppendLine($"inoculated:{world.Inoculated}");

                    for (int i = 0; i < 200; i++)
                    {
                        world.Step(1f);
                        samples.AppendLine(WorldStats.Sample(world).ToJson());
                    }
                }
                catch (PopulationRunawayException e)
                {
                    samples.AppendLine($"runaway:{e.Population}@{e.ElapsedSeconds:0.#}");
                }

                return samples.ToString();
            }

            Assert.Equal(Trajectory(), Trajectory());
        }
    }
}
