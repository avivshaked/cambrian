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
            // The detritus-flux instrument (fable-propose-detritus-flux): dead tissue is the
            // field's only income and feeding its only outflow — settling, mixing, advection and
            // remineralisation all conserve — so the two cumulative counters must bracket the
            // standing stock exactly, at any moment, in any world. If a future mechanism moves
            // joules across the field's boundary without touching a counter, this is what fails.
            var config = new RunConfig { MinimumPopulation = 30, MaximumPopulation = 600 };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 300; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double deposited = world.DetritusDepositedTotal;
            double taken = world.DetritusTakenTotal;
            double standing = world.Nutrients.TotalJoules;
            _output.WriteLine($"deposited {deposited:0.###} taken {taken:0.###} standing {standing:0.###}");

            Assert.True(deposited > 0, "nothing died in 300 s — the counter was never exercised");
            Assert.True(
                Math.Abs(deposited - taken - standing) / Math.Max(1.0, deposited) < 1e-4,
                $"deposited - taken = {deposited - taken:0.###} J but the field holds {standing:0.###} J");
        }

        [Fact]
        public void EnergyIsConservedWithRemineralisationRunning()
        {
            // D051: Remineralise is a transfer within Nutrients.TotalJoules, which StandingJoules
            // already sums whole, so the audit needs no new term. Run it aggressively rather than
            // at a realistic rate — if any leak existed it would show fastest here.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                NutrientRemineralisationPerSecond = 0.01f,
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
        public void ReproductionCostsExactlyWhatTheGenomeSays()
        {
            // n * (e + overhead + body) — §5A.6 and §5A.2c. The overhead is spent rather than
            // transferred, which is what makes brood size a trait selection can act on: without
            // it, one brood of four and four broods of one are the same transaction. The body term
            // is what stops offspring size being free, and is an estimate from the parent's own
            // tissue since the offspring does not exist yet.
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

                Assert.Equal(
                    traits.BroodSize *
                        (traits.OffspringEndowment + 25f + creature.TissueJoules),
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

        // ------------------------------------------------------------ D048: matter

        private static RunConfig MatterWorld(float perTissueJoule, float initialPerCubicMetre) =>
            new RunConfig
            {
                Light = new LightModel(200f, 12f),
                MatterPerTissueJoule = perTissueJoule,
                InitialMatterPerCubicMetre = initialPerCubicMetre,
            };

        [Fact]
        public void MatterNeverEntersTheEnergyAudit()
        {
            // §5A.2's audit is a hard equality over joules. Matter is a different substance, and
            // folding it in would let the books balance by counting the wrong thing — the exact
            // failure the audit exists to catch. The residual must be blind to it.
            World without = Run(MatterWorld(0f, 1f), null, seconds: 300f);
            World with = Run(MatterWorld(0.5f, 1f), null, seconds: 300f);

            _output.WriteLine($"matter off: residual {without.AuditResidual:0.######} J");
            _output.WriteLine($"matter on : residual {with.AuditResidual:0.######} J, " +
                              $"standing matter {with.StandingMatter:0.##}");

            double scale = Math.Max(1d, with.EnergyIn);
            Assert.True(
                Math.Abs(with.AuditResidual) / scale < 1e-6,
                $"matter opened a hole in the energy audit: {with.AuditResidual} J");
        }

        [Fact]
        public void MatterIsConservedBecauseNothingCreatesIt()
        {
            // Seeded once and thereafter only moved: reproduction takes it out of a layer, death
            // puts it back. A drift here is matter being mined out of nothing, which is how a
            // nutrient limit stops limiting anything.
            var config = MatterWorld(0.5f, 1f);
            var world = new World(config, seed: 1);

            double atStart = 0d;
            for (float t = 0f; t < 400f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}), " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} — something creates or destroys it");
        }

        [Fact]
        public void MatterIsConservedWithRemineralisationRunning()
        {
            // D051's matter-side knob, mirroring MatterIsConservedBecauseNothingCreatesIt: the
            // leak is an internal transfer within Matter.TotalJoules, which StandingMatter already
            // sums whole, so this must drift no more than the knob-off case does.
            var config = MatterWorld(0.5f, 1f);
            config.MatterRemineralisationPerSecond = 0.01f;
            var world = new World(config, seed: 1);

            double atStart = 0d;
            for (float t = 0f; t < 400f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}), " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} with remineralisation running — something creates or destroys it");
        }

        // ------------------------------------------------------------ D065: fixed matter cost

        [Fact]
        public void MatterIsConservedWithAFixedPerCreatureCost()
        {
            // The same guard as MatterIsConservedBecauseNothingCreatesIt, run over the leg D065
            // adds. The fixed term is charged at conception and has to come back the same way the
            // proportional one does — through excretion while alive and the death deposit — so
            // free matter + MatterInBodies (both summed by StandingMatter) must still equal what
            // the column was seeded with. A fixed term that were charged and not locked would
            // leak on every birth, and leak fastest in exactly the crowded world it exists to
            // bound.
            var config = MatterWorld(0.5f, 1f);
            config.MatterPerCreature = 3f;
            var world = new World(config, seed: 1);

            double atStart = 0d;
            for (float t = 0f; t < 400f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}), " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(world.Births > 0, "nothing bred, so the fixed term was never charged");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} with a fixed per-creature cost — something creates or destroys it");
        }

        [Fact]
        public void MatterIsStillConservedWithTheFixedCostAndExcretionTogether()
        {
            // The two knobs meet on one field: excretion drains LockedMatter, and from D065 that
            // balance starts higher than the tissue price. Run together because the cap is
            // min(locked, rate·upkeep) — if the fixed term reached the Take but not LockedMatter,
            // this is where the mismatch shows as a drift rather than as a wrong number nobody
            // reads.
            var config = MatterWorld(0.5f, 1f);
            config.MatterPerCreature = 3f;
            config.ExcretionPerJoule = 0.05f;
            var world = new World(config, seed: 1);

            double atStart = 0d;
            for (float t = 0f; t < 400f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}), " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} with the fixed cost and excretion together");
        }

        [Fact]
        public void ExcretionNeverDrainsTheFixedMatterTerm()
        {
            // The amendment recorded in the D065 comment above World's excretion block:
            // excretable = max(0, LockedMatter - MatterPerCreature). The rate here is well above
            // MatterIsStillConservedWithTheFixedCostAndExcretionTogether's 0.05 — that test only
            // checks conservation and never demonstrates the tissue share actually hits the
            // floor — chosen so it is enough to drain a creature's whole tissue share well inside
            // the run: if the cap were missing or wrong, LockedMatter would run straight through
            // the fixed term of 3 rather than stopping at it. Sampled every step rather than just
            // at the end, because a floor that is breached mid-run and happens to recover before
            // t=600 would pass a final-value check and still be wrong. Conservation is checked
            // the same way the sibling test does — a floor that leaks would show there before it
            // showed here.
            var config = MatterWorld(0.5f, 1f);
            config.MatterPerCreature = 3f;
            config.ExcretionPerJoule = 5f;

            var world = new World(config, seed: 1);

            double atStart = 0d;
            float minLocked = float.MaxValue;

            for (float t = 0f; t < 600f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;

                foreach (Organism creature in world.Living)
                {
                    if (creature.ParentId < 0) continue; // founders never paid the fixed term

                    Assert.True(
                        creature.LockedMatter >= config.MatterPerCreature - 1e-4f,
                        $"a living creature's LockedMatter fell to {creature.LockedMatter} at " +
                        $"t={t}, below the fixed term of {config.MatterPerCreature} — excretion " +
                        "drained machinery mass");

                    if (creature.LockedMatter < minLocked) minLocked = creature.LockedMatter;
                }
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"min LockedMatter observed: {minLocked:0.####} (fixed term " +
                $"{config.MatterPerCreature}), matter {atStart:0.##} -> {world.StandingMatter:0.##} " +
                $"(drift {drift:0.######}), {world.Births} births");

            Assert.True(world.Births > 0, "nothing bred, so the fixed term was never charged");

            Assert.True(
                minLocked < config.MatterPerCreature + 1e-3f,
                $"LockedMatter never approached the fixed term (min {minLocked} vs fixed " +
                $"{config.MatterPerCreature}) — this excretion rate never exercised the floor, " +
                "so the test above proves nothing");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} while excretion was capped at the fixed term");
        }

        [Fact]
        public void AChildLocksTheProportionalPricePlusTheFixedOne()
        {
            // D065's arithmetic, read off the creatures themselves. Excretion is off, so
            // LockedMatter cannot have moved since conception and every living child must hold
            // exactly what it was charged. Founders are skipped: they never paid, so they hold 0
            // and would drag the assertion onto a creature the rule does not describe.
            var config = MatterWorld(perTissueJoule: 0.5f, initialPerCubicMetre: 5f);
            config.MatterPerCreature = 3f;
            Assert.Equal(0f, config.ExcretionPerJoule);

            var world = new World(config, seed: 1);
            for (float t = 0f; t < 300f; t += 1f) world.Step(1f);

            Assert.True(world.Births > 0, "nothing bred, so LockedMatter was never set on anything");

            int checkedCount = 0;
            foreach (Organism creature in world.Living)
            {
                if (creature.ParentId < 0) continue;

                Fixtures.AssertClose(
                    config.MatterPerTissueJoule * creature.TissueJoules + config.MatterPerCreature,
                    creature.LockedMatter,
                    1e-3f);
                checkedCount++;
            }

            Assert.True(checkedCount > 0, "no reproduction-born creature survived to check");
        }

        [Fact]
        public void AFixedCostAloneStillCharges()
        {
            // The proportional term at 0 and the fixed term alone: the early-out guard used to
            // read MatterPerTissueJoule only, so a world priced purely per creature would have
            // skipped straight past the stock check and bred for free. Every child must hold
            // exactly the fixed amount, and matter must still be conserved.
            var config = MatterWorld(perTissueJoule: 0f, initialPerCubicMetre: 5f);
            config.MatterPerCreature = 2f;

            var world = new World(config, seed: 1);
            double atStart = 0d;
            for (float t = 0f; t < 300f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            Assert.True(world.Births > 0, "nothing bred");

            int checkedCount = 0;
            foreach (Organism creature in world.Living)
            {
                if (creature.ParentId < 0) continue;
                Fixtures.AssertClose(2f, creature.LockedMatter, 1e-3f);
                checkedCount++;
            }

            Assert.True(checkedCount > 0, "no reproduction-born creature survived to check");

            double drift = world.StandingMatter - atStart;
            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} with only a fixed per-creature cost");
        }

        [Fact]
        public void TheCheapestPossibleChildIncludesTheFixedCost()
        {
            // CheapestPossibleChildMatter is a private lower bound on any child's price, and the
            // contract in its own remark is that no conception is refused that the full check
            // would have allowed. Read here through the only public consequence it has: a layer
            // holding less than the fixed term alone can afford no child at all, so a world with
            // no matter in it must block every conception rather than let one through the
            // early-out. Paired with a zero-fixed-cost control, because a world that blocks
            // everything for some other reason would pass the first half alone.
            var barren = MatterWorld(perTissueJoule: 0f, initialPerCubicMetre: 0f);
            barren.MatterPerCreature = 1f;
            var blocked = new World(barren, seed: 1);
            for (float t = 0f; t < 200f; t += 1f) blocked.Step(1f);

            var free = MatterWorld(perTissueJoule: 0f, initialPerCubicMetre: 0f);
            var unblocked = new World(free, seed: 1);
            for (float t = 0f; t < 200f; t += 1f) unblocked.Step(1f);

            _output.WriteLine(
                $"fixed 1 in an empty column: {blocked.Births} births, " +
                $"{blocked.ConceptionsBlockedByMatter} blocked; " +
                $"fixed 0: {unblocked.Births} births, {unblocked.ConceptionsBlockedByMatter} blocked");

            Assert.Equal(0f, new RunConfig().MatterPerCreature);

            Assert.True(
                unblocked.ConceptionsBlockedByMatter == 0,
                "the control world blocked a conception on matter with both knobs at 0");

            Assert.True(unblocked.Births > 0, "the control world never bred, so it proves nothing");

            Assert.True(
                blocked.ConceptionsBlockedByMatter > 0,
                "a column with no matter in it allowed conceptions despite a fixed per-creature cost — " +
                "the cheapest-child bound does not include the fixed term");
        }

        [Fact]
        public void NutrientRemineralisationPerSecondReachesTheArithmetic()
        {
            // This project's house rule (CLAUDE.md): before concluding a parameter matters, prove
            // it reached the thing it configures. Two worlds, identical but for the knob, stepped
            // long enough for detritus to reach the floor — their floor stocks must differ, or the
            // config value never made it past the RunConfig field it lives in.
            var off = new RunConfig
            {
                Light = new LightModel(400f, 12f), MinimumPopulation = 30, MaximumPopulation = 600,
            };
            var on = new RunConfig
            {
                Light = new LightModel(400f, 12f), MinimumPopulation = 30, MaximumPopulation = 600,
                NutrientRemineralisationPerSecond = 0.01f,
            };

            var worldOff = new World(off, seed: 7);
            var worldOn = new World(on, seed: 7);

            // Caught rather than avoided by tuning irradiance down: this test only needs the two
            // worlds to have run identically long enough for detritus to reach the floor, and a
            // runaway (D021) is a population outcome unrelated to what it is checking.
            try { for (int i = 0; i < 600; i++) worldOff.Step(1f); }
            catch (PopulationRunawayException) { }
            try { for (int i = 0; i < 600; i++) worldOn.Step(1f); }
            catch (PopulationRunawayException) { }

            double floorOff = worldOff.Nutrients.StockInLayer(worldOff.Nutrients.LayerCount - 1);
            double floorOn = worldOn.Nutrients.StockInLayer(worldOn.Nutrients.LayerCount - 1);

            _output.WriteLine($"floor stock: rate 0 -> {floorOff:0.###} J, rate 0.01 -> {floorOn:0.###} J");

            Assert.NotEqual(floorOff, floorOn);
        }

        [Fact]
        public void AWorldWithNoMatterCannotBreedHoweverMuchLightItHas()
        {
            // The whole point of D048 in one assertion. Sunlight is not sufficient: a parent with
            // energy to spare and nothing dissolved around it does not reproduce. Before this,
            // light alone bought everything and no creature's success ever cost the world a
            // finite thing.
            World rich = Run(MatterWorld(0.5f, 5f), null, seconds: 400f);
            World barren = Run(MatterWorld(0.5f, 0f), null, seconds: 400f);

            _output.WriteLine($"matter 5/m3: {rich.Births} births, {rich.ConceptionsBlockedByMatter} blocked");
            _output.WriteLine($"matter 0/m3: {barren.Births} births, {barren.ConceptionsBlockedByMatter} blocked");

            Assert.True(
                barren.Births == 0,
                $"a world with no matter produced {barren.Births} births");
            Assert.True(
                barren.ConceptionsBlockedByMatter > 0,
                "nothing even tried to breed, so this proves nothing about matter");
            Assert.True(
                rich.Births > 0,
                $"the same world with matter produced no births either — light is the binding " +
                $"constraint here and this test measures nothing");
        }

        [Fact]
        public void SuccessAtADepthStripsThatDepth()
        {
            // The feedback the world had nowhere: reproducing somewhere makes that somewhere
            // worse. Founders are scattered through the lit zone and breed there, so the layers
            // they occupy must end up poorer in matter than the ones they do not.
            var config = MatterWorld(0.5f, 1f);
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
                $"{stripped} layers depleted, {untouched} untouched, " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(world.Births > 0, "nothing bred, so nothing could have stripped anything");
            Assert.True(
                stripped > 0,
                "no layer lost matter — reproduction is not drawing from where the parent is");
            Assert.True(
                untouched > 0,
                "every layer was depleted equally, which means the draw is not local and the " +
                "gradient D048 exists to create cannot form");
        }

        private static double[] LayerMatter(World world)
        {
            var layers = new double[world.Matter.LayerCount];
            for (int i = 0; i < layers.Length; i++) layers[i] = world.Matter.StockInLayer(i);
            return layers;
        }

        // ------------------------------------------------------------ D052: excretion

        [Fact]
        public void ExcretionPerJouleDefaultZeroLeavesLockedMatterUnchangedFromConception()
        {
            // Bit-identical to the world before this knob existed: with the rate at 0, a body's
            // LockedMatter must never move except at conception (set to the price paid) and at
            // death (drained to 0) — which is exactly what the field held before D052, computed
            // fresh from TissueJoules at the moment of death rather than tracked across a
            // lifetime.
            var config = MatterWorld(perTissueJoule: 0.5f, initialPerCubicMetre: 5f);
            Assert.Equal(0f, config.ExcretionPerJoule);

            var world = new World(config, seed: 1);
            for (float t = 0f; t < 300f; t += 1f) world.Step(1f);

            Assert.True(world.Births > 0, "nothing bred, so LockedMatter was never set on anything");

            int checkedCount = 0;
            foreach (Organism creature in world.Living)
            {
                if (creature.ParentId < 0) continue; // founders never held matter to begin with

                Fixtures.AssertClose(
                    config.MatterPerTissueJoule * creature.TissueJoules, creature.LockedMatter, 1e-3f);
                checkedCount++;
            }

            Assert.True(checkedCount > 0, "no reproduction-born creature survived to check");
        }

        [Fact]
        public void ExcretionMovesExactlyWhatItDebitsInAQuietStep()
        {
            // Isolated from every other mover of matter — sink, mixing and remineralisation are
            // all at their bit-identical-default of 0 already (MatterWorld does not set them) —
            // so whatever the field gains and MatterInBodies loses in one step with no births and
            // no deaths is excretion and nothing else. Reproduction is let run long enough to
            // give some living creatures a nonzero LockedMatter, then frozen (an overhead no
            // parent can ever afford) so a birth-free, death-free step can be found and measured.
            var config = MatterWorld(perTissueJoule: 0.5f, initialPerCubicMetre: 5f);
            config.ExcretionPerJoule = 0.02f;
            config.MinimumPopulation = 20;

            var world = new World(config, seed: 3);
            for (int i = 0; i < 200; i++) world.Step(1f);

            Assert.True(world.Births > 0, "nothing bred, so nothing has locked matter to excrete");

            // Freeze reproduction from here on — the same RunConfig instance the world already
            // holds, so this reaches Reproduce() on the very next step with no other channel for
            // a creature to appear or disappear except death.
            config.PerOffspringOverheadJoules = 1e9f;

            for (int i = 0; i < 500; i++)
            {
                long birthsBefore = world.Births, deathsBefore = world.Deaths;

                var lockedBefore = new Dictionary<long, float>();
                var upkeepBefore = new Dictionary<long, float>();
                foreach (Organism c in world.Living)
                {
                    lockedBefore[c.Id] = c.LockedMatter;
                    upkeepBefore[c.Id] = c.Lifetime.Upkeep;
                }

                double matterInBodiesBefore = world.MatterInBodies;
                double totalMatterBefore = world.Matter.TotalJoules;

                world.Step(1f);

                if (world.Births != birthsBefore || world.Deaths != deathsBefore) continue;

                double expected = 0d;
                foreach (Organism c in world.Living)
                {
                    if (!lockedBefore.TryGetValue(c.Id, out float locked) || locked <= 0f) continue;

                    float upkeepThisStep = c.Lifetime.Upkeep - upkeepBefore[c.Id];
                    expected += Math.Min(locked, config.ExcretionPerJoule * upkeepThisStep);
                }

                if (expected <= 0d) continue; // no locked-matter creature paid upkeep this step

                double matterInBodiesFell = matterInBodiesBefore - world.MatterInBodies;
                double fieldRose = world.Matter.TotalJoules - totalMatterBefore;

                _output.WriteLine(
                    $"expected {expected:0.######}: MatterInBodies fell {matterInBodiesFell:0.######}, " +
                    $"field rose {fieldRose:0.######}");

                Assert.True(
                    Math.Abs(matterInBodiesFell - expected) < 1e-4,
                    "MatterInBodies did not fall by exactly what excretion moved");
                Assert.True(
                    Math.Abs(fieldRose - expected) < 1e-4,
                    "the field did not gain exactly what excretion moved");
                return;
            }

            Assert.Fail("never found a quiet (birth-free, death-free) step with excretion to measure");
        }

        [Fact]
        public void ExcretedTotalIncrementsByExactlyWhatExcretionDebitsFromBodies()
        {
            // Same isolation and the same freeze-then-measure shape as
            // ExcretionMovesExactlyWhatItDebitsInAQuietStep, checked against World.ExcretedTotal
            // instead of the field and MatterInBodies — the pre-round-8 experiment contract's
            // excretion-flux column reads this counter as a delta between two samples, and that
            // delta has to equal the debit exactly or the column would misreport the flux it
            // exists to show.
            var config = MatterWorld(perTissueJoule: 0.5f, initialPerCubicMetre: 5f);
            config.ExcretionPerJoule = 0.02f;
            config.MinimumPopulation = 20;

            var world = new World(config, seed: 3);
            for (int i = 0; i < 200; i++) world.Step(1f);

            Assert.True(world.Births > 0, "nothing bred, so nothing has locked matter to excrete");

            // Freeze reproduction from here on, exactly as the sibling test does, so a quiet step
            // with no other channel for LockedMatter to appear or disappear can be found.
            config.PerOffspringOverheadJoules = 1e9f;

            for (int i = 0; i < 500; i++)
            {
                long birthsBefore = world.Births, deathsBefore = world.Deaths;

                var lockedBefore = new Dictionary<long, float>();
                var upkeepBefore = new Dictionary<long, float>();
                foreach (Organism c in world.Living)
                {
                    lockedBefore[c.Id] = c.LockedMatter;
                    upkeepBefore[c.Id] = c.Lifetime.Upkeep;
                }

                double excretedTotalBefore = world.ExcretedTotal;

                world.Step(1f);

                if (world.Births != birthsBefore || world.Deaths != deathsBefore) continue;

                double expected = 0d;
                foreach (Organism c in world.Living)
                {
                    if (!lockedBefore.TryGetValue(c.Id, out float locked) || locked <= 0f) continue;

                    float upkeepThisStep = c.Lifetime.Upkeep - upkeepBefore[c.Id];
                    expected += Math.Min(locked, config.ExcretionPerJoule * upkeepThisStep);
                }

                if (expected <= 0d) continue; // no locked-matter creature paid upkeep this step

                double excretedTotalRose = world.ExcretedTotal - excretedTotalBefore;

                _output.WriteLine(
                    $"expected {expected:0.######}: ExcretedTotal rose {excretedTotalRose:0.######}");

                Assert.True(
                    Math.Abs(excretedTotalRose - expected) < 1e-4,
                    "ExcretedTotal did not rise by exactly what excretion debited from bodies");
                return;
            }

            Assert.Fail("never found a quiet (birth-free, death-free) step with excretion to measure");
        }

        [Fact]
        public void MatterIsConservedWithExcretionRunning()
        {
            // D052's own copy of D051's guard: excretion is an internal transfer from
            // MatterInBodies into Matter.TotalJoules, both of which StandingMatter already sums
            // whole, so this must drift no more than the knob-off case does.
            var config = MatterWorld(0.5f, 1f);
            config.ExcretionPerJoule = 0.05f;
            var world = new World(config, seed: 1);

            double atStart = 0d;
            for (float t = 0f; t < 400f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;
            }

            double drift = world.StandingMatter - atStart;

            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}), " +
                $"{world.Births} births, {world.ConceptionsBlockedByMatter} blocked");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} with excretion running — something creates or destroys it");
        }

        [Fact]
        public void ExcretionCapsAtWhatTheBodyStillHolds()
        {
            // A rate absurd enough to demand, in one step, far more than any body could ever
            // hold — so the min(locked, rate·upkeep) cap is what actually fires rather than the
            // formula's uncapped term. LockedMatter must land at exactly 0 and never go negative,
            // and StandingMatter must still be conserved: the cap means "excrete less than the
            // formula asks for", not "excrete for free".
            var config = MatterWorld(perTissueJoule: 0.5f, initialPerCubicMetre: 5f);
            config.ExcretionPerJoule = 1e6f;
            var world = new World(config, seed: 4);

            double atStart = 0d;
            bool everHitZero = false;

            for (float t = 0f; t < 300f; t += 1f)
            {
                world.Step(1f);
                if (t == 0f) atStart = world.StandingMatter;

                foreach (Organism creature in world.Living)
                {
                    Assert.True(creature.LockedMatter >= 0f, "LockedMatter went negative — the cap failed");
                    if (creature.ParentId >= 0 && creature.LockedMatter == 0f) everHitZero = true;
                }
            }

            Assert.True(world.Births > 0, "nothing bred, so nothing ever had matter to cap");
            Assert.True(
                everHitZero,
                "no reproduction-born creature's LockedMatter was ever driven to exactly 0 — " +
                "the cap was never exercised, and this test proves nothing about it");

            double drift = world.StandingMatter - atStart;
            _output.WriteLine(
                $"matter {atStart:0.##} -> {world.StandingMatter:0.##} (drift {drift:0.######}) " +
                $"at an excretion rate large enough to hit the cap on every locked body");

            Assert.True(
                Math.Abs(drift) / Math.Max(1d, atStart) < 1e-6,
                $"matter drifted by {drift} once the excretion cap started firing");
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
            config.Mutation.EndowmentChance = 0f;

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
                Light = new LightModel(300f, 12f),
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
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
            float tissue = Metabolism.TissueJoules(body, config);
            float expectedCredit = 5 * (config.FounderEnergyJoules + tissue);

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
