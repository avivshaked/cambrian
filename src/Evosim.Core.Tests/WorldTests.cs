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
    }
}
