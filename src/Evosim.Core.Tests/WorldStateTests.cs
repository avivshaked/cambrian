using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Core's half of a checkpoint: a world written to bytes and read back into a fresh world of
    /// the same config and seed is the same world, and goes on being the same world when both are
    /// stepped (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two assertions, and they catch different faults. Writing the restored world's state again
    /// and comparing the bytes catches a field that is written and read back wrong. Stepping both
    /// worlds on and comparing what they do catches a field that is not written at all, which the
    /// byte comparison cannot see because neither side has it.
    /// </para>
    /// <para>
    /// Neither one is the acceptance. The acceptance is a farm run to a second, a restore at that
    /// second and a run on to a later one, with the two runs' output files compared byte for byte;
    /// this is the same shape at a scale a test can afford, without the solver.
    /// </para>
    /// </remarks>
    public class WorldStateTests
    {
        /// <summary>
        /// A world small enough to step quickly and busy enough to have state: light, a floor that
        /// fires and draws on the world's own streams, and both fields stirring.
        /// </summary>
        private static RunConfig Stage() => new RunConfig
        {
            MinimumPopulation = 12,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
        };

        private static byte[] StateOf(World world)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static World Restored(RunConfig config, ulong seed, byte[] state)
        {
            var world = new World(config, seed);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);

                // The reader must consume exactly what the writer wrote. A reader that stops short
                // is a section nobody reads; one that runs off the end throws before this.
                Assert.Equal(state.Length, buffer.Position);
            }

            return world;
        }

        private static World Stepped(RunConfig config, ulong seed, float seconds, float dt = 0.5f)
        {
            var world = new World(config, seed);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);

            for (float t = 0f; t < seconds; t += dt) world.Step(dt);

            // A world with nothing in it round-trips perfectly and proves nothing, so the fixture
            // asserts it is a world: bodies alive, the floor having fired, energy having moved.
            Assert.NotEmpty(world.Living);
            Assert.True(world.FloorSpawns > 0, "the floor never fired, so no founding stream ran");
            Assert.True(world.EnergyIn > 0d, "no light was ever captured");

            return world;
        }

        /// <summary>Everything a report row is built out of, in one list, in a fixed order.</summary>
        private static List<double> Readings(World world)
        {
            var readings = new List<double>
            {
                world.ElapsedSeconds,
                world.Living.Count,
                world.Births,
                world.Deaths,
                world.FloorSpawns,
                world.Stillbirths,
                world.Diverged,
                world.EnergyIn,
                world.EnergyOut,
                world.StandingJoules,
                world.AuditResidual,
                world.MatterResidual,
                world.StandingMatterUnits,
                world.Nutrients.TotalJoules,
                world.Matter.TotalJoules,
                world.DetritusDepositedTotal,
                world.DetritusTakenTotal,
                world.BurntTotal,
                world.RemineralisedTotal,
                world.MatterInfluxedTotal,
                world.MatterBuriedTotal,
                world.PhotosyntheticSteps,
                world.UptakeLimitedSteps,
                world.ConceptionsUnderMassFloor,
                world.SecondsSinceFloorFired,
            };

            // And every body, by id, so a difference in one creature's reserve is not averaged
            // away by a thousand that agree.
            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];

                readings.Add(creature.Id);
                readings.Add(creature.Energy);
                readings.Add(creature.Age);
                readings.Add(creature.BodyFraction);
                readings.Add(creature.TissueJoules);
                readings.Add(creature.GenerationDepth);
                readings.Add(creature.Phenotype.PartCount);
                readings.Add(creature.ModuleStarvedSeconds);

                // D106's counts, which are the one part of a body's plan that is not in its
                // genome: restore them wrongly and the creature comes back a different shape.
                if (creature.ModuleCounts != null)
                {
                    foreach (int count in creature.ModuleCounts) readings.Add(count);
                }

                // D106's other two, for the same reason. A lost part is a plan change no count can
                // express — restore it wrongly and the body comes back whole — and a part's health
                // is state that decides whether it is about to come off at all.
                readings.Add(creature.LostPartPaths == null ? 0 : creature.LostPartPaths.Count);

                if (creature.PartHealth != null)
                {
                    foreach (float health in creature.PartHealth) readings.Add(health);
                }

                // And the health each part has lost, which the Damage sense reports: a brain
                // wired to it drives on the number, so a restore that drops it is a body that
                // swims differently from its first step (round 45 seed 2's resume, 2026-09-23).
                if (creature.PartDamage != null)
                {
                    foreach (float lost in creature.PartDamage) readings.Add(lost);
                }
            }

            return readings;
        }

        // ------------------------------------------------------------------ the round trip

        /// <summary>
        /// The same round trip asked of a world whose bodies have moved off their genomes' plans —
        /// D106's module counts and drop clock, which <see cref="Readings"/> above reads for every
        /// creature but which <see cref="Stepped"/>'s determinate leaf never moves.
        /// </summary>
        [Fact]
        public void AWorldOfIndeterminateBodiesRestoresTheirCountsAndTheirClocks()
        {
            RunConfig Modules() => new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 2_000,
                WorldAreaSquareMetres = 100f,
                WorldDepthMetres = 20f,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                ModuleAddReserveSeconds = 20f,
                ModuleDropReserveSeconds = 0f,
                ModuleDropAfterSeconds = 60f,
            };

            RunConfig config = Modules();
            var world = new World(config, seed: 11);
            world.Inoculate(Fixtures.IndeterminateLeaf(maxModules: 4), count: 3, heightY: -5f);

            for (int round = 0; round < 40; round++)
            {
                for (int t = 0; t < 30; t++) world.Step(1f);
                world.ApplyModuleRule(30f);
            }

            // A world worth round-tripping: the bodies are not the shape their genomes describe,
            // and one of them is part way through a drop clock that has not run out.
            Assert.Equal(9L, world.ModuleAdds);
            Assert.True(world.ModulesStanding > 0L, "no body ever added a module");

            config.ModuleAddReserveSeconds = 0f;
            config.ModuleDropReserveSeconds = 1e9f;
            world.ApplyModuleRule(30f);
            Assert.Equal(0L, world.ModuleDrops);
            Assert.True(world.Living[0].ModuleStarvedSeconds > 0f, "the drop clock never started");

            byte[] first = StateOf(world);
            World restored = Restored(Modules(), 11, first);

            Assert.Equal(Readings(world), Readings(restored));
            Assert.Equal(first, StateOf(restored));

            // And the counters themselves, which are the world's and not a body's.
            Assert.Equal(world.ModuleAdds, restored.ModuleAdds);
            Assert.Equal(world.ModuleDrops, restored.ModuleDrops);
            Assert.Equal(world.ModuleAddsRefused, restored.ModuleAddsRefused);
            Assert.Equal(world.ModuleAddsRefusedForShape, restored.ModuleAddsRefusedForShape);
            Assert.Equal(world.ModuleAddsRefusedForReserve, restored.ModuleAddsRefusedForReserve);
            Assert.Equal(world.ModuleAddsRefusedForShape + world.ModuleAddsRefusedForReserve,
                         world.ModuleAddsRefused);
            Assert.Equal(world.ModulesStanding, restored.ModulesStanding);
        }

        /// <summary>
        /// The same round trip asked of a world the mouth has been through — D106 items 3 and 4.
        /// A wounded body's health, a maimed body's lost part, and the six counters.
        /// </summary>
        /// <remarks>
        /// <b>A maimed body is the case the byte comparison alone cannot catch.</b> Health is a
        /// fraction per part and would round-trip perfectly even if the plan came back whole,
        /// because a body of two parts and a body of one both carry an array the reader believes.
        /// What pins it is the part count in <see cref="Readings"/>: restoring the paths wrongly
        /// re-grows the limb, and the restored body is a different animal.
        /// </remarks>
        [Fact]
        public void AWoundedWorldRestoresItsHealthItsLostPartsAndItsCounters()
        {
            RunConfig Bitten() => new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 2_000,
                WorldAreaSquareMetres = 100f,
                WorldDepthMetres = 20f,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                HealthPerCubicMetre = 100f,
                CorpseDecayPerSecond = 0.0001f,
            };

            RunConfig config = Bitten();
            var world = new World(config, seed: 11);

            world.Inoculate(Fixtures.MouthSpine(2), count: 1, heightY: -5f);
            world.Inoculate(Fixtures.MouthSpine(1), count: 1, heightY: -5f);
            world.Inoculate(Fixtures.ArmedBox(attack: 1f), count: 1, heightY: -5f);

            Organism maimed = world.Living[0];
            Organism wounded = world.Living[1];
            Organism claw = world.Living[2];

            // One body loses a limb, which is a plan change; the other is left part way down its
            // health, which is state on a body whose plan never moved.
            for (int step = 0; step < 100 && maimed.Phenotype.PartCount > 1; step++)
            {
                world.SetContacts(new List<CreatureContact>
                {
                    new CreatureContact(claw.Id, 0, maimed.Id, 1),
                });

                world.ApplyMouth(1f);
            }

            world.SetContacts(new List<CreatureContact>
            {
                new CreatureContact(claw.Id, 0, wounded.Id, 0),
            });

            world.ApplyMouth(0.5f);

            // A world worth round-tripping: one body short of a limb, one short of its health, a
            // corpse in the water and the counters standing at something other than zero.
            Assert.Equal(1, maimed.Phenotype.PartCount);
            Assert.Equal(1, maimed.LostPartPaths.Count);
            Assert.True(wounded.PartHealth[0] > 0f && wounded.PartHealth[0] < 1f);
            Assert.Equal(1L, world.PartsKilled);
            Assert.Single(world.Corpses);

            byte[] first = StateOf(world);
            World restored = Restored(Bitten(), 11, first);

            Assert.Equal(Readings(world), Readings(restored));
            Assert.Equal(first, StateOf(restored));

            Assert.Equal(1, restored.Living[0].Phenotype.PartCount);
            Assert.Equal(wounded.PartHealth[0], restored.Living[1].PartHealth[0]);

            // The lost health is what the wounded body senses, and it came back null until
            // StateVersion 6: every restored wounded body sensed nothing.
            Assert.NotNull(wounded.PartDamage);
            Assert.True(wounded.PartDamage[0] > 0f);
            Assert.NotNull(restored.Living[1].PartDamage);
            Assert.Equal(wounded.PartDamage[0], restored.Living[1].PartDamage[0]);

            Assert.Equal(world.PartsKilled, restored.PartsKilled);
            Assert.Equal(world.BodiesEaten, restored.BodiesEaten);
            Assert.Equal(world.CorpsesFromKills, restored.CorpsesFromKills);
            Assert.Equal(world.UnitsEaten, restored.UnitsEaten);
            Assert.Equal(world.CorpsesEaten, restored.CorpsesEaten);
            Assert.Equal(world.HealingJoules, restored.HealingJoules);
        }

        [Fact]
        public void ARestoredWorldWritesTheSameStateAgain()
        {
            RunConfig config = Stage();
            World world = Stepped(config, seed: 11, seconds: 60f);

            Assert.NotEmpty(world.Living);

            byte[] first = StateOf(world);
            byte[] second = StateOf(Restored(Stage(), 11, first));

            Assert.Equal(first.Length, second.Length);
            Assert.Equal(first, second);
        }

        [Fact]
        public void ARestoredWorldReadsTheSameAsTheOneItCameFrom()
        {
            RunConfig config = Stage();
            World world = Stepped(config, seed: 11, seconds: 60f);

            World restored = Restored(Stage(), 11, StateOf(world));

            Assert.Equal(Readings(world), Readings(restored));
        }

        [Fact]
        public void ARestoredWorldGoesOnBeingTheSameWorld()
        {
            RunConfig config = Stage();
            World world = Stepped(config, seed: 11, seconds: 60f);

            World restored = Restored(Stage(), 11, StateOf(world));

            // Far enough that a forgotten counter, a stale field cell or a random stream left at
            // its founding position has somewhere to show itself: births, deaths and the floor all
            // happen inside this window.
            for (int i = 0; i < 240; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(Readings(world), Readings(restored));
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void AnotherSeedIsRefused()
        {
            byte[] state = StateOf(Stepped(Stage(), seed: 11, seconds: 10f));

            var other = new World(Stage(), seed: 12);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                Assert.Throws<InvalidDataException>(() => other.ReadState(r));
            }
        }

        [Fact]
        public void AStateThisBuildDoesNotKnowIsRefused()
        {
            byte[] state = StateOf(Stepped(Stage(), seed: 11, seconds: 10f));

            // The version sits immediately after the four-byte section tag.
            state[4] = (byte)(World.StateVersion + 1);

            var world = new World(Stage(), seed: 11);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                Assert.Throws<InvalidDataException>(() => world.ReadState(r));
            }
        }

        [Fact]
        public void ASectionTagThatDoesNotMatchIsRefused()
        {
            byte[] state = StateOf(Stepped(Stage(), seed: 11, seconds: 10f));

            state[0] = (byte)'X';

            var world = new World(Stage(), seed: 11);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                Assert.Throws<InvalidDataException>(() => world.ReadState(r));
            }
        }
    }

    /// <summary>
    /// The random source's own state, which is what makes a restored world take the same next
    /// draw rather than a plausible one.
    /// </summary>
    public class RngStateTests
    {
        [Fact]
        public void ARestoredStreamDrawsTheSameNumbersAgain()
        {
            var source = new Rng(seed: 12345, sequence: 7);
            for (int i = 0; i < 100; i++) source.NextFloat();

            var copy = new Rng(seed: 999, sequence: 7);
            copy.RestoreState(
                source.State, source.Increment, source.HasSpareGaussian, source.SpareGaussian);

            for (int i = 0; i < 1000; i++) Assert.Equal(source.NextFloat(), copy.NextFloat());
        }

        [Fact]
        public void TheSpareGaussianIsCarriedToo()
        {
            var source = new Rng(seed: 12345, sequence: 7);

            // An odd number of draws, so the pair's second value is held rather than spent. A
            // restore that forgets it is one Gaussian out of step forever afterwards, which no
            // count of matching floats would show.
            source.Gaussian();
            Assert.True(source.HasSpareGaussian);

            var copy = new Rng(seed: 999, sequence: 7);
            copy.RestoreState(
                source.State, source.Increment, source.HasSpareGaussian, source.SpareGaussian);

            Assert.True(copy.HasSpareGaussian);

            for (int i = 0; i < 100; i++) Assert.Equal(source.Gaussian(), copy.Gaussian());
        }

        [Fact]
        public void AStateCannotBeRestoredIntoAnotherStream()
        {
            var source = new Rng(seed: 12345, sequence: 7);
            var other = new Rng(seed: 12345, sequence: 8);

            Assert.Throws<ArgumentException>(() => other.RestoreState(
                source.State, source.Increment, source.HasSpareGaussian, source.SpareGaussian));
        }
    }
}
