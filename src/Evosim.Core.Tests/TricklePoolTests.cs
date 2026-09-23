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
    /// D117, the founding trickle's pool of evolved bodies: the draw, the label, the refusals and
    /// the checkpoint's carriage of the two new pieces of state. The farm's half (the files, the
    /// hash, the resume refusal, the header and the column) is in
    /// <c>Evosim.Farm.Tests.TricklePoolFarmTests</c>.
    /// </summary>
    public class TricklePoolTests
    {
        private readonly ITestOutputHelper _output;

        public TricklePoolTests(ITestOutputHelper output) => _output = output;

        private const float MetabolicStep = 0.5f;
        private const float Rate = 0.4f;
        private const float Closes = 50f;

        /// <summary>A hash of the right shape; Core never recomputes it (the farm does).</summary>
        private static readonly string AnyHash = new string('c', 64);

        /// <summary>The two fixtures, both genome format 8, in a fixed order.</summary>
        private static IReadOnlyList<Genome> Pool() => new[]
        {
            Load("r45s1-stomach-100.json"),
            Load("growth-ledger-genome.json"),
        };

        private static Genome Load(string file) =>
            GenomeJson.Read(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "fixtures", "pool", file)));

        /// <summary>FoundingTrickleTests' small world, with or without a pool named.</summary>
        private static RunConfig Config(float share = 0f, int count = 0, string hash = "")
        {
            var config = new RunConfig
            {
                Light = new LightModel(120f, 12f),
                MinimumPopulation = 20,
                MaximumPopulation = 100_000,
                FloorClosesAfterSeconds = Closes,
                FoundingTricklePerSecond = Rate,
            };

            config.FoundingTricklePoolCount = count;
            config.FoundingTricklePoolHash = hash;
            config.FoundingTricklePoolShare = share;
            return config;
        }

        /// <summary>Every lineage row a world queues over <paramref name="steps"/> steps, in order.</summary>
        private static List<string> Rows(World world, int steps)
        {
            var rows = new List<string>();
            for (int i = 0; i < steps; i++)
            {
                world.Step(MetabolicStep);
                foreach (LineageEvent evt in world.DrainLineageEvents()) rows.Add(evt.ToJson());
            }

            return rows;
        }

        // ------------------------------------------------------------------ share 0 is the record

        /// <summary>
        /// A world with a pool named and a share of 0 takes exactly the draws the trickle build
        /// took: its lineage, its counts and its living are the same as a world with no pool at
        /// all, whether the pool is handed to it or not.
        /// </summary>
        [Fact]
        public void AShareOfZeroWithAPoolNamedIsTheRecordedWorld()
        {
            const int Steps = 800;
            const ulong Seed = 9UL;

            var recorded = new World(Config(), Seed);
            var named = new World(Config(share: 0f, count: 2, hash: AnyHash), Seed, Pool());
            var unhanded = new World(Config(share: 0f, count: 2, hash: AnyHash), Seed);

            List<string> a = Rows(recorded, Steps);
            List<string> b = Rows(named, Steps);
            List<string> c = Rows(unhanded, Steps);

            _output.WriteLine(
                $"{a.Count} lineage rows; trickle {recorded.TrickleSpawns}, floor " +
                $"{recorded.FloorSpawns}, alive {recorded.Living.Count}");

            Assert.True(recorded.TrickleSpawns > 0, "the trickle never ran, so nothing was compared");
            Assert.Equal(a, b);
            Assert.Equal(a, c);

            foreach (World w in new[] { named, unhanded })
            {
                Assert.Equal(recorded.TrickleSpawns, w.TrickleSpawns);
                Assert.Equal(recorded.FloorSpawns, w.FloorSpawns);
                Assert.Equal(0L, w.PoolSpawns);
                Assert.Equal(recorded.Living.Count, w.Living.Count);
                Assert.Equal(recorded.AuditResidual, w.AuditResidual);

                for (int i = 0; i < recorded.Living.Count; i++)
                {
                    Assert.Equal(recorded.Living[i].Id, w.Living[i].Id);
                    Assert.Equal(recorded.Living[i].Energy, w.Living[i].Energy);
                }
            }

            foreach (string row in b) Assert.DoesNotContain("\"pool\"", row);
        }

        // ------------------------------------------------------------------ share 1 and the label

        /// <summary>
        /// At a share of 1 every trickle founder is an exact copy of a pool genome: its row reads
        /// <c>k: "f"</c>, <c>src: "pool"</c> and a pool index inside the pool, no parent and
        /// generation 0, and its genome is the pool's own. The floor's founders are untouched.
        /// </summary>
        [Fact]
        public void AShareOfOneMakesEveryTrickleFounderAPoolCopy()
        {
            IReadOnlyList<Genome> pool = Pool();
            var world = new World(Config(share: 1f, count: 2, hash: AnyHash), seed: 4, tricklePool: pool);

            int poolRows = 0;
            int floorRows = 0;
            var indices = new HashSet<int>();

            for (int i = 0; i < 800; i++)
            {
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism o in world.Living) living[o.Id] = o;

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.BirthKind != BirthKind.Floor) continue;

                    string json = evt.ToJson();
                    Assert.Contains("\"k\":\"f\"", json);
                    Assert.NotEqual(FounderSource.Trickle, evt.Source);

                    if (evt.Source == FounderSource.Pool)
                    {
                        Assert.Contains("\"src\":\"pool\"", json);
                        Assert.Contains(FormattableString.Invariant($"\"pool\":{evt.PoolIndex}"), json);
                        Assert.InRange(evt.PoolIndex, 0, pool.Count - 1);
                        Assert.Equal(-1L, evt.ParentId);
                        Assert.Equal(0, evt.GenerationDepth);
                        if (living.TryGetValue(evt.Id, out Organism founder))
                        {
                            Assert.Same(pool[evt.PoolIndex], founder.Genome);
                        }

                        indices.Add(evt.PoolIndex);
                        poolRows++;
                    }
                    else
                    {
                        Assert.Equal(FounderSource.Floor, evt.Source);
                        Assert.Contains("\"src\":\"floor\"", json);
                        Assert.DoesNotContain("\"pool\"", json);
                        Assert.Equal(-1, evt.PoolIndex);
                        floorRows++;
                    }
                }
            }

            _output.WriteLine(
                $"pool rows {poolRows} (indices {string.Join(",", indices)}), floor rows " +
                $"{floorRows}; trickle {world.TrickleSpawns}, pool {world.PoolSpawns}");

            Assert.True(poolRows > 0, "the pool admitted nobody");
            Assert.True(floorRows > 0, "the floor founded nobody");
            Assert.Equal(2, indices.Count);
            Assert.Equal(world.TrickleSpawns, world.PoolSpawns);
        }

        /// <summary>
        /// The draw at a share between: each founder the Poisson count asks for takes one more
        /// uniform, and a pool founder one more after it, all from the trickle's own stream. A
        /// fresh stream at the trickle's seed, walked the same way, predicts both counts exactly.
        /// </summary>
        [Fact]
        public void TheShareIsDrawnFromTheTricklesOwnStream()
        {
            const float Share = 0.5f;
            const int Steps = 800;
            const ulong Seed = 11UL;

            var world = new World(Config(share: Share, count: 2, hash: AnyHash), Seed, Pool());
            var stream = new Rng(Rng.SeedFor(Seed, World.TrickleIndex));

            long trickle = 0;
            long pool = 0;

            for (int i = 0; i < Steps; i++)
            {
                world.Step(MetabolicStep);
                world.DrainLineageEvents();
                if (world.ElapsedSeconds < Closes) continue;

                int count = World.TrickleCount(stream, (double)Rate * MetabolicStep);
                for (int k = 0; k < count; k++)
                {
                    trickle++;
                    if (stream.NextFloat() < Share)
                    {
                        stream.NextFloat();
                        pool++;
                    }
                }
            }

            _output.WriteLine(
                $"trickle {world.TrickleSpawns} (stream {trickle}), pool {world.PoolSpawns} " +
                $"(stream {pool})");

            Assert.True(pool > 0 && pool < trickle);
            Assert.Equal(trickle, world.TrickleSpawns);
            Assert.Equal(pool, world.PoolSpawns);
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void AShareWithNoPoolNamedIsRefused()
        {
            var noCount = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0.1f, count: 0, hash: ""), 1));
            Assert.Contains("FoundingTricklePoolShare is 0.1 and the pool is not named", noCount.Message);

            var noHash = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0.1f, count: 2, hash: ""), 1, Pool()));
            Assert.Contains("FoundingTricklePoolHash empty", noHash.Message);
        }

        [Fact]
        public void AShareWithTheTrickleOffIsRefused()
        {
            RunConfig config = Config(share: 0.1f, count: 2, hash: AnyHash);
            config.FoundingTricklePerSecond = 0f;

            var e = Assert.Throws<ArgumentException>(() => new World(config, 1, Pool()));
            Assert.Contains("FoundingTricklePerSecond is 0", e.Message);
        }

        [Fact]
        public void APoolThatIsNotTheOneTheConfigNamesIsRefused()
        {
            var unhanded = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0.1f, count: 2, hash: AnyHash), 1));
            Assert.Contains("the world was handed 0", unhanded.Message);

            var shortPool = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0f, count: 3, hash: AnyHash), 1, Pool()));
            Assert.Contains("handed a pool of 2 genomes and the config names 3", shortPool.Message);

            var unpinned = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0f, count: 2, hash: ""), 1));
            Assert.Contains("FoundingTricklePoolCount is 2 and FoundingTricklePoolHash is empty", unpinned.Message);

            var hashOfNothing = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0f, count: 0, hash: AnyHash), 1));
            Assert.Contains("A hash of no pool names nothing", hashOfNothing.Message);
        }

        [Fact]
        public void TheSettersRefuseAShareCountOrHashOutOfShape()
        {
            var config = new RunConfig();

            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolShare = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolShare = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolShare = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolCount = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolCount = 101);
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = "ABC");
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = new string('A', 64));
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = null);

            config.FoundingTricklePoolShare = 1f;
            config.FoundingTricklePoolCount = 100;
            config.FoundingTricklePoolHash = AnyHash;
        }

        /// <summary>
        /// D111, asked of a pool genome as <see cref="World.Inoculate"/> asks it of an inoculant:
        /// a node floating off its centre in a world that does not price it is refused at
        /// construction, and the same genome is admitted once the price is set.
        /// </summary>
        [Fact]
        public void APoolGenomeWithAnUnpricedOffsetIsRefused()
        {
            Genome offset = Load("growth-ledger-genome.json");
            offset.Nodes[0].BuoyancyOffset = 0.1f;
            var pool = new[] { Load("r45s1-stomach-100.json"), offset };

            var e = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0.5f, count: 2, hash: AnyHash), 1, pool));
            Assert.Contains("Trickle pool genome 01 (pool/01.json)", e.Message);
            Assert.Contains("BuoyancyOffset 0.1 in a world that does not price it", e.Message);

            RunConfig priced = Config(share: 0.5f, count: 2, hash: AnyHash);
            priced.BuoyancyOffsetWattsPerCubicMetre = 1f;
            Assert.NotNull(new World(priced, 1, pool));
        }

        // ------------------------------------------------------------------ the checkpoint

        /// <summary>
        /// A world with a pool round-trips through its state: the pool count and a queued pool
        /// row's index are carried, and the restored world carries on exactly as the original.
        /// A world without a pool writes the trickle build's bytes (the state's length is the
        /// same with a share of 0 and no pool named).
        /// </summary>
        [Fact]
        public void APoolWorldRoundTripsThroughItsState()
        {
            RunConfig config = Config(share: 0.5f, count: 2, hash: AnyHash);
            const ulong Seed = 13UL;

            var original = new World(config, Seed, Pool());

            // Stepped without draining until the pool has admitted twice, so the state carries a
            // queue of rows with pool rows among them (a refused pool founder queues none, and
            // the rows compared below say whether one was carried).
            for (int i = 0; i < 2_000 && original.PoolSpawns < 2; i++) original.Step(MetabolicStep);

            Assert.True(original.PoolSpawns >= 2, "the pool admitted fewer than two");

            byte[] state = StateOf(original);
            var restored = new World(config, Seed, Pool());

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            Assert.Equal(original.PoolSpawns, restored.PoolSpawns);
            Assert.Equal(original.TrickleSpawns, restored.TrickleSpawns);

            List<string> after = Rows(original, 300);
            Assert.Equal(after, Rows(restored, 300));
            Assert.Equal(original.PoolSpawns, restored.PoolSpawns);
            Assert.Contains(after, row => row.Contains("\"src\":\"pool\""));
            _output.WriteLine($"{after.Count} rows after the restore; pool {original.PoolSpawns}");

            // The layout of a world with no pool is the trickle build's: a pool named at share 0
            // adds its count and nothing else, and no pool adds nothing at all.
            var none = new World(Config(), Seed);
            var named = new World(Config(share: 0f, count: 2, hash: AnyHash), Seed);
            Assert.Equal(StateOf(none).Length + sizeof(long), StateOf(named).Length);
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
    }
}
