using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D159's planting path (<see cref="World.Immigrate"/>): an immigrant from another world of the
    /// group is placed and endowed as a founder is, recorded as an immigrant with the world and
    /// the creature it came from, refused quietly and counted, and carried through a checkpoint;
    /// and a version-14 world, which had no immigrants, is read exactly.
    /// </summary>
    public class ImmigrationTests
    {
        private readonly ITestOutputHelper _output;

        public ImmigrationTests(ITestOutputHelper output) => _output = output;

        private const float MetabolicStep = 0.5f;
        private const float Endowment = 600f;

        /// <summary>A quiet world: no floor, so every body in it is one the test put there.</summary>
        private static RunConfig Quiet() => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            FounderEndowmentSeconds = Endowment,
        };

        /// <summary>WorldStateTests' busy world, with the endowment on: a floor that fires, light, both fields.</summary>
        private static RunConfig Busy() => new RunConfig
        {
            MinimumPopulation = 12,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            FounderEndowmentSeconds = Endowment,
        };

        /// <summary>A placer that says yes or no and counts which door was asked.</summary>
        private sealed class CountingPlacement : IBodyPlacement
        {
            public bool Room = true;
            public int FounderReservations;
            public int OffspringReservations;
            public int Committed;
            public int Released;
            public float LastFounderHeightY = float.NaN;

            public bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch)
            {
                patch = 0;
                if (!Room) return false;
                OffspringReservations++;
                return true;
            }

            public bool TryReserveFounder(Phenotype adult, ref float heightY, out int patch)
            {
                patch = 0;
                LastFounderHeightY = heightY;
                if (!Room) return false;
                FounderReservations++;
                return true;
            }

            public int PatchOf(Organism creature) => creature.Patch;
            public void Commit(long creatureId) => Committed++;
            public void Release() => Released++;
        }

        // ------------------------------------------------------------------ the planting

        /// <summary>
        /// An immigrant lands through the founders' door with a founder's start (the purse at its
        /// birth fraction and the endowment), carries the generation depth it came with and a copy
        /// of the genome, and its row is an immigrant's naming the world and the creature.
        /// </summary>
        [Fact]
        public void AnImmigrantIsPlantedAsAFounderIs()
        {
            RunConfig config = Quiet();
            config.SharedSpace = true;
            var placer = new CountingPlacement();
            var world = new World(config, seed: 5) { Placement = placer };

            Genome genome = Fixtures.SingleLeaf();
            long id = world.Immigrate(genome, fromWorld: 2, fromId: 41, generationDepth: 7);

            Assert.True(id >= 0, "the immigrant was refused");
            Assert.Single(world.Living);
            Organism o = world.Living[0];

            Assert.Equal(id, o.Id);
            Assert.Equal(-1, o.ParentId);
            Assert.Equal(7, o.GenerationDepth);
            Assert.NotSame(genome, o.Genome);

            // The founders' door, and a founder's depth: drawn over the spread, never below it.
            Assert.Equal(1, placer.FounderReservations);
            Assert.Equal(0, placer.OffspringReservations);
            Assert.Equal(1, placer.Committed);
            Assert.InRange(o.HeightY, -config.FounderDepthSpread, 0f);
            Assert.Equal(placer.LastFounderHeightY, o.HeightY);

            // A founder's start: the purse at the birth fraction, and the endowment of its body.
            double endowment = world.FounderEndowmentFor(o.Phenotype);
            Assert.True(endowment > 0d);
            Assert.Equal(config.FounderEnergyJoules * o.BodyFraction + endowment, o.Energy, 9);

            Assert.Equal(1L, world.Immigrated);
            Assert.Equal(0L, world.ImmigrantsRefused);
            Assert.Equal(0L, world.FloorSpawns);
            Assert.Equal(0L, world.Inoculated);

            var rows = new List<LineageEvent>(world.DrainLineageEvents());
            Assert.Single(rows);
            LineageEvent row = rows[0];
            Assert.Equal(BirthKind.Immigration, row.BirthKind);
            Assert.Equal(2, row.ImmigrantFromWorld);
            Assert.Equal(41L, row.ImmigrantFromId);
            Assert.Equal(FounderSource.None, row.Source);

            string json = row.ToJson();
            _output.WriteLine(json);
            Assert.Contains("\"k\":\"m\"", json);
            Assert.Contains("\"p\":-1", json);
            Assert.Contains("\"g\":7", json);
            Assert.Contains("\"from\":2", json);
            Assert.Contains("\"fid\":41", json);
            Assert.Contains("\"endow\":", json);
            Assert.DoesNotContain("\"src\"", json);
        }

        /// <summary>No other row carries D159's two fields, whatever is passed for them.</summary>
        [Fact]
        public void OnlyAnImmigrantsRowNamesWhereItCameFrom()
        {
            LineageEvent founder = LineageEvent.Birth(
                1d, 3, -1, BirthKind.Floor, 0, 0, false, false, false, 0, 1f, 1f, 0f, 0,
                false, false, false, FounderSource.Floor,
                immigrantFromWorld: 1, immigrantFromId: 9);

            Assert.Equal(-1, founder.ImmigrantFromWorld);
            Assert.Equal(-1L, founder.ImmigrantFromId);
            Assert.DoesNotContain("\"from\"", founder.ToJson());
            Assert.DoesNotContain("\"fid\"", founder.ToJson());
        }

        // ------------------------------------------------------------------ the refusals

        /// <summary>
        /// A body under the newborn mass floor and a world with no room both refuse an immigrant
        /// without throwing: -1, one count in <see cref="World.ImmigrantsRefused"/>, nothing
        /// alive, no row, no joule created, and none of the founders' counters moved.
        /// </summary>
        [Fact]
        public void ARefusedImmigrantIsCountedQuietlyAndNothingElse()
        {
            RunConfig heavy = Quiet();
            heavy.MinNewbornPartKilograms = 1_000f;
            var underFloor = new World(heavy, seed: 5);

            Assert.Equal(-1L, underFloor.Immigrate(Fixtures.SingleLeaf(), 0, 12, 3));
            AssertRefusedOnce(underFloor);
            Assert.Equal(ImmigrantRefusal.UnderMassFloor, underFloor.LastImmigrantRefusal);

            RunConfig shared = Quiet();
            shared.SharedSpace = true;
            var placer = new CountingPlacement { Room = false };
            var full = new World(shared, seed: 5) { Placement = placer };

            Assert.Equal(-1L, full.Immigrate(Fixtures.SingleLeaf(), 0, 12, 3));
            AssertRefusedOnce(full);
            Assert.Equal(ImmigrantRefusal.NoRoom, full.LastImmigrantRefusal);

            Assert.Equal(0L, full.CrowdedStillbirths);
            Assert.Equal(0, placer.Committed);

            // A planted one clears it.
            placer.Room = true;
            Assert.True(full.Immigrate(Fixtures.SingleLeaf(), 0, 13, 3) >= 0);
            Assert.Equal(ImmigrantRefusal.None, full.LastImmigrantRefusal);
        }

        private static void AssertRefusedOnce(World world)
        {
            Assert.Equal(1L, world.ImmigrantsRefused);
            Assert.Equal(0L, world.Immigrated);
            Assert.Empty(world.Living);
            Assert.Empty(world.DrainLineageEvents());
            Assert.Equal(0d, world.EnergyIn);
            Assert.Equal(0L, world.FoundersUnderMassFloor);
            Assert.Equal(0L, world.FloorSpawns);
        }

        /// <summary>
        /// D159's species rule (an immigrant keeps its home species, tagged with its world) is
        /// not built, so a world with species on refuses an immigrant loudly rather than found a
        /// new species for it; with species off it is planted as species 0.
        /// </summary>
        [Fact]
        public void AWorldWithSpeciesOnRefusesAnImmigrant()
        {
            RunConfig config = Quiet();
            config.SpeciesDriftThreshold = 0.5f;
            var world = new World(config, seed: 5);

            var refused = Assert.Throws<InvalidOperationException>(
                () => world.Immigrate(Fixtures.SingleLeaf(), 1, 4, 0));
            Assert.Contains("D159", refused.Message);
            Assert.Empty(world.Living);
            Assert.Equal(0L, world.ImmigrantsRefused);

            var off = new World(Quiet(), seed: 5);
            off.Immigrate(Fixtures.SingleLeaf(), 1, 4, 0);
            Assert.Equal(0u, off.Living[0].SpeciesId);
        }

        // ------------------------------------------------------------------ the books and replay

        /// <summary>
        /// Both books close over a world that takes an immigrant every ten seconds beside its
        /// floor: an immigrant is created from nothing, as a founder is, and both books say so.
        /// </summary>
        [Fact]
        public void BothBooksCloseOverImmigrants()
        {
            World world = Exchanged(Busy(), seed: 3, steps: 2_000);

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, floor {world.FloorSpawns}, " +
                $"immigrated {world.Immigrated}, refused {world.ImmigrantsRefused}; audit " +
                $"{world.AuditResidual:R} of {world.EnergyIn:0} J in; matter {world.MatterResidual:R}");

            Assert.True(world.Immigrated > 50, "too few immigrants to say the books close over them");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * (world.MatterInitialTotal + world.MatterInfluxedTotal),
                $"matter residual {world.MatterResidual:R}");
        }

        /// <summary>The same seed and the same immigrants at the same seconds are the same world.</summary>
        [Fact]
        public void AWorldWithImmigrantsReplays()
        {
            World a = Exchanged(Busy(), seed: 9, steps: 600);
            World b = Exchanged(Busy(), seed: 9, steps: 600);

            Assert.Equal(Rows(a), Rows(b));
            Assert.Equal(StateOf(a), StateOf(b));
        }

        // ------------------------------------------------------------------ the checkpoint

        /// <summary>
        /// A world with immigrants round-trips: both counters and a queued immigrant's row, with
        /// its world and creature, are carried, the restore writes the same bytes again, and the
        /// two go on as one world, immigrants and all.
        /// </summary>
        [Fact]
        public void AWorldWithImmigrantsRoundTripsThroughItsState()
        {
            World original = Exchanged(Busy(), seed: 13, steps: 400, drain: false);
            Assert.True(original.Immigrated > 0);

            byte[] state = StateOf(original);
            World restored = Restored(Busy(), 13, state);

            Assert.Equal(original.Immigrated, restored.Immigrated);
            Assert.Equal(original.ImmigrantsRefused, restored.ImmigrantsRefused);
            Assert.Equal(World.StateVersion, restored.StateVersionRead);
            Assert.Equal(state, StateOf(restored));

            List<string> queued = Rows(original), queuedAgain = Rows(restored);
            Assert.Contains(queued, r => r.Contains("\"k\":\"m\"") && r.Contains("\"from\":1"));
            Assert.Equal(queued, queuedAgain);

            for (int step = 0; step < 400; step++)
            {
                StepWithExchange(original, step);
                StepWithExchange(restored, step);
            }

            Assert.Equal(Rows(original), Rows(restored));
            Assert.Equal(StateOf(original), StateOf(restored));
        }

        /// <summary>
        /// A version-14 stream, round 57's, is read exactly: version 15 adds only the two
        /// immigration counters, and a world that wrote version 14 could plant no immigrant, so
        /// both are 0 in it. The restored world writes the bytes of the world it came from at
        /// version 15, and goes on as that world. A world that has had an immigrant is refused at
        /// version 14 rather than written as one that had none.
        /// </summary>
        [Fact]
        public void AVersionFourteenStreamIsReadExactly()
        {
            Assert.Equal(18, World.StateVersion);
            Assert.Equal(17, World.PreCardPairStateVersion);
            Assert.Equal(15, World.PreLeafStateVersion);
            Assert.Equal(14, World.PreImmigrationStateVersion);

            World world = new World(Busy(), seed: 11);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);
            for (int step = 0; step < 120; step++) world.Step(MetabolicStep);
            Assert.True(world.FloorSpawns > 0);

            // Against version 15, the layout that added the counters; 17 adds round 59's blocks.
            byte[] older = StateOf(world, World.PreImmigrationStateVersion);
            byte[] current = StateOf(world, World.PreLeafStateVersion);
            Assert.Equal(current.Length - 2 * sizeof(long), older.Length);

            World restored = Restored(Busy(), 11, older);
            Assert.Equal(World.PreImmigrationStateVersion, restored.StateVersionRead);
            Assert.Equal(0L, restored.Immigrated);
            Assert.Equal(0L, restored.ImmigrantsRefused);
            Assert.Equal(current, StateOf(restored, World.PreLeafStateVersion));

            for (int step = 0; step < 200; step++)
            {
                world.Step(MetabolicStep);
                restored.Step(MetabolicStep);
            }

            Assert.Equal(Rows(world), Rows(restored));
            Assert.Equal(StateOf(world), StateOf(restored));

            world.Immigrate(Fixtures.SingleLeaf(), 0, 1, 0);
            Assert.ThrowsAny<Exception>(() => StateOf(world, World.PreImmigrationStateVersion));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// A world stepped <paramref name="steps"/> half-seconds with one immigrant every ten
        /// seconds, a copy of a living body's genome named as coming from world 1.
        /// </summary>
        private static World Exchanged(RunConfig config, ulong seed, int steps, bool drain = true)
        {
            var world = new World(config, seed);
            world.Inoculate(Fixtures.SingleLeaf(), count: 4, heightY: -5f);

            for (int step = 0; step < steps; step++)
            {
                StepWithExchange(world, step);
                if (drain) world.DrainLineageEvents();
            }

            return world;
        }

        /// <summary>
        /// One step, then, every twentieth, one immigrant: the genome of the oldest living body,
        /// standing in for a neighbour's, or the leaf in an empty world.
        /// </summary>
        private static void StepWithExchange(World world, int step)
        {
            world.Step(MetabolicStep);
            if (step % 20 != 19) return;

            Genome genome = world.Living.Count > 0 ? world.Living[0].Genome : Fixtures.SingleLeaf();
            long fromId = world.Living.Count > 0 ? world.Living[0].Id + 1_000_000 : 1_000_000;
            int depth = world.Living.Count > 0 ? world.Living[0].GenerationDepth + 1 : 0;
            world.Immigrate(genome, fromWorld: 1, fromId: fromId, generationDepth: depth);
        }

        private static List<string> Rows(World world)
        {
            var rows = new List<string>();
            foreach (LineageEvent e in world.DrainLineageEvents()) rows.Add(e.ToJson());
            return rows;
        }

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

        /// <summary>The world at an older layout, through the internal writer the tests use.</summary>
        private static byte[] StateOf(World world, int layout)
        {
            System.Reflection.MethodInfo writer = typeof(World).GetMethod(
                "WriteState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(BinaryWriter), typeof(int) }, null);

            Assert.NotNull(writer);

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    try
                    {
                        writer.Invoke(world, new object[] { w, layout });
                    }
                    catch (System.Reflection.TargetInvocationException e) when (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }

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
                Assert.Equal(state.Length, buffer.Position);
            }

            return world;
        }
    }
}
