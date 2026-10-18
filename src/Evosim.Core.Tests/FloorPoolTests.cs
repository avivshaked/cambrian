using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The floor's pool share (<see cref="RunConfig.FoundingFloorPoolShare"/>, the owner's ruling of
    /// 2026-10-04): at 0 the floor of the recorded build, at 1 every floor founder a pool copy, the
    /// weights followed, the floor's own stream and the trickle's left alone, the refusals, the
    /// Central conversion and the checkpoint. The farm's half (the variable and the header) is in
    /// <c>Evosim.Farm.Tests.TricklePoolFarmTests</c>.
    /// </summary>
    public class FloorPoolTests
    {
        private readonly ITestOutputHelper _output;

        public FloorPoolTests(ITestOutputHelper output) => _output = output;

        private const float MetabolicStep = 0.5f;
        private const float Rate = 0.4f;
        private const float Closes = 50f;

        /// <summary>A hash of the right shape; Core never recomputes it (the farm does).</summary>
        private static readonly string AnyHash = new string('c', 64);

        private static IReadOnlyList<Genome> Pool() => new[]
        {
            Load("r45s1-stomach-100.json"),
            Load("growth-ledger-genome.json"),
        };

        private static Genome Load(string file) =>
            GenomeJson.Read(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "fixtures", "pool", file)));

        /// <summary>TricklePoolTests' small world, with the floor's share as well.</summary>
        private static RunConfig Config(
            float floorShare = 0f, float trickleShare = 0f, int count = 0, string hash = "")
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
            config.FoundingTricklePoolShare = trickleShare;
            config.FoundingFloorPoolShare = floorShare;
            return config;
        }

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

        private static string Digest(List<string> rows)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows)));
                return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>The floor's two uniforms for attempt <paramref name="attempt"/>, as the world draws them.</summary>
        private static Rng FloorStream(ulong seed, long attempt) =>
            new Rng(Rng.SeedFor(Rng.SeedFor(seed, World.FloorPoolIndex), (ulong)attempt));

        // ------------------------------------------------------------------ share 0 is the record

        /// <summary>
        /// At a share of 0 the floor takes exactly the draws the build before the share took: the
        /// lineage of 100 s (the floor's 50 s and the trickle's first 50) hashes to the digests
        /// recorded on that build (round 55's branch at <c>2e68532</c>, 2026-10-04), for a world
        /// with no pool and for one with a weighted pool at a trickle share of 0.5. A lineage row
        /// carries no config hash, so the digest is the realisation and nothing else.
        /// </summary>
        [Fact]
        public void AShareOfZeroIsTheRecordedFloor()
        {
            var plain = new World(Config(), 9UL);
            List<string> a = Rows(plain, 200);
            _output.WriteLine($"no pool: {a.Count} rows, floor {plain.FloorSpawns}, trickle {plain.TrickleSpawns}");
            Assert.Equal(41L, plain.FloorSpawns);
            Assert.Equal("df36d3eb432ba03ad24f5cee9a46e3e8a33ea3393855e01abcf26aab81f1764d", Digest(a));

            var pooled = new World(
                Config(floorShare: 0f, trickleShare: 0.5f, count: 2, hash: AnyHash), 9UL, Pool(), new[] { 1d, 3d });
            List<string> b = Rows(pooled, 200);
            _output.WriteLine($"weighted pool: {b.Count} rows, floor {pooled.FloorSpawns}, trickle {pooled.TrickleSpawns}");
            Assert.Equal(41L, pooled.FloorSpawns);
            Assert.Equal("53e255cccffd49c17d784784443fc1e661eb38812b28d6a621d2c6a676e5a457", Digest(b));
        }

        // ------------------------------------------------------------------ share 1 and the label

        /// <summary>
        /// At a share of 1 every floor founder is a pool copy: its row reads <c>k: "f"</c>,
        /// <c>src: "pool"</c> and a pool index, at a time before the floor closes, and it is
        /// counted in the floor's total and the pool's, never the trickle's. The trickle's founders
        /// after the close are the lottery's (its own share is 0).
        /// </summary>
        [Fact]
        public void AShareOfOneMakesEveryFloorFounderAPoolCopy()
        {
            IReadOnlyList<Genome> pool = Pool();
            var world = new World(Config(floorShare: 1f, count: 2, hash: AnyHash), seed: 4, tricklePool: pool);

            int poolRows = 0;
            int trickleRows = 0;
            var indices = new HashSet<int>();

            for (int i = 0; i < 300; i++)
            {
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism o in world.Living) living[o.Id] = o;

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.BirthKind != BirthKind.Floor) continue;

                    string json = evt.ToJson();
                    Assert.Contains("\"k\":\"f\"", json);
                    Assert.NotEqual(FounderSource.Floor, evt.Source);

                    if (evt.ElapsedSeconds < Closes)
                    {
                        Assert.Equal(FounderSource.Pool, evt.Source);
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
                        Assert.Equal(FounderSource.Trickle, evt.Source);
                        trickleRows++;
                    }
                }
            }

            _output.WriteLine(
                $"floor pool rows {poolRows} (indices {string.Join(",", indices)}), trickle rows " +
                $"{trickleRows}; floor {world.FloorSpawns}, trickle {world.TrickleSpawns}, pool {world.PoolSpawns}");

            Assert.True(poolRows > 0, "the floor admitted no pool founder");
            Assert.True(trickleRows > 0, "the trickle founded nobody");
            Assert.Equal(2, indices.Count);
            Assert.Equal(world.FloorSpawns, world.PoolSpawns);
        }

        // ------------------------------------------------------------------ the weights and the streams

        /// <summary>
        /// A weighted pool at the floor, share 1, walked against fresh copies of the floor's stream
        /// keyed by each attempt: the admitted founders are the predicted picks in order, one for
        /// one (this world refuses none of them), and the picks follow the weights (a nine-to-one
        /// pool, read with the trickle test's tolerance).
        /// </summary>
        [Fact]
        public void AWeightedPoolAtTheFloorFollowsTheWeights()
        {
            const ulong Seed = 21UL;
            double[] weights = { 1d, 9d, 0.0001d };
            var pool = new[] { Load("r45s1-stomach-100.json"), Load("growth-ledger-genome.json"), Load("r45s1-stomach-100.json") };

            RunConfig config = Config(floorShare: 1f, count: 3, hash: AnyHash);
            config.MinimumPopulation = 400;
            config.FloorSpawnsPerStep = 10;
            var world = new World(config, Seed, pool, weights);

            var admitted = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                world.Step(MetabolicStep);
                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind == LineageEventKind.Birth && evt.Source == FounderSource.Pool) admitted.Add(evt.PoolIndex);
                }
            }

            double[] cumulative = new double[weights.Length];
            double total = 0d;
            for (int i = 0; i < weights.Length; i++) cumulative[i] = total += weights[i];

            var drawn = new List<int>();
            for (long k = 0; k < world.FloorSpawns; k++)
            {
                Rng rng = FloorStream(Seed, k);
                rng.NextFloat();
                drawn.Add(World.WeightedPoolIndex(cumulative, rng.NextFloat()));
            }

            int heavy = 0;
            foreach (int index in drawn) if (index == 1) heavy++;
            double share = (double)heavy / drawn.Count;

            _output.WriteLine(
                $"{drawn.Count} floor draws, {admitted.Count} admitted; genome 1 took {share:0.000} " +
                $"(weight share {9d / 10.0001d:0.000})");

            Assert.Equal(world.FloorSpawns, world.PoolSpawns);
            Assert.Equal(0L, world.TrickleSpawns);
            Assert.True(drawn.Count >= 200, "too few floor draws to read a share");
            Assert.Equal(drawn, admitted);
            Assert.InRange(share, 0.8d, 0.97d);
        }

        /// <summary>
        /// At a share between, the floor's founders are predicted by the floor's stream alone and
        /// the trickle's by the trickle's stream alone, as if the floor's share did not exist: the
        /// floor draws nothing from the trickle's stream.
        /// </summary>
        [Fact]
        public void TheFloorDrawsFromItsOwnStreamAndLeavesTheTricklesAlone()
        {
            const float FloorShare = 0.5f;
            const float TrickleShare = 0.5f;
            const ulong Seed = 11UL;
            const int Steps = 800;

            var world = new World(
                Config(floorShare: FloorShare, trickleShare: TrickleShare, count: 2, hash: AnyHash), Seed, Pool());

            for (int i = 0; i < Steps; i++)
            {
                world.Step(MetabolicStep);
                world.DrainLineageEvents();
            }

            long floorPool = 0;
            for (long k = 0; k < world.FloorSpawns; k++)
            {
                if (FloorStream(Seed, k).NextFloat() < FloorShare) floorPool++;
            }

            var stream = new Rng(Rng.SeedFor(Seed, World.TrickleIndex));
            long trickle = 0;
            long tricklePool = 0;
            for (int i = 0; i < Steps; i++)
            {
                if ((i + 1) * MetabolicStep < Closes) continue;

                int count = World.TrickleCount(stream, (double)Rate * MetabolicStep);
                for (int k = 0; k < count; k++)
                {
                    trickle++;
                    if (stream.NextFloat() < TrickleShare)
                    {
                        stream.NextFloat();
                        tricklePool++;
                    }
                }
            }

            _output.WriteLine(
                $"floor {world.FloorSpawns} (pool {floorPool}), trickle {world.TrickleSpawns} " +
                $"(stream {trickle}, pool {tricklePool}), pool {world.PoolSpawns}");

            Assert.True(floorPool > 0 && floorPool < world.FloorSpawns);
            Assert.True(tricklePool > 0);
            Assert.Equal(trickle, world.TrickleSpawns);
            Assert.Equal(floorPool + tricklePool, world.PoolSpawns);
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void AShareWithNoPoolIsRefused()
        {
            var noCount = Assert.Throws<ArgumentException>(
                () => new World(Config(floorShare: 0.5f), 1));
            Assert.Contains("FoundingFloorPoolShare is 0.5 and the pool is not named", noCount.Message);

            var noHash = Assert.Throws<ArgumentException>(
                () => new World(Config(floorShare: 0.5f, count: 2, hash: ""), 1, Pool()));
            Assert.Contains("FoundingTricklePoolHash empty", noHash.Message);

            var unhanded = Assert.Throws<ArgumentException>(
                () => new World(Config(floorShare: 0.5f, count: 2, hash: AnyHash), 1));
            Assert.Contains("FoundingFloorPoolShare is 0.5 and the config names a pool of 2", unhanded.Message);

            RunConfig noFloor = Config(floorShare: 0.5f, count: 2, hash: AnyHash);
            noFloor.MinimumPopulation = 0;
            var e = Assert.Throws<ArgumentException>(() => new World(noFloor, 1, Pool()));
            Assert.Contains("MinimumPopulation is 0", e.Message);

            // The floor's share needs no trickle share and no trickle: the pool is named for it alone.
            RunConfig floorOnly = Config(floorShare: 0.5f, count: 2, hash: AnyHash);
            floorOnly.FoundingTricklePerSecond = 0f;
            Assert.NotNull(new World(floorOnly, 1, Pool()));
        }

        [Fact]
        public void TheSetterRefusesAShareOutOfRange()
        {
            var config = new RunConfig();

            Assert.Equal(0f, config.FoundingFloorPoolShare);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingFloorPoolShare = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingFloorPoolShare = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingFloorPoolShare = float.NaN);

            config.FoundingFloorPoolShare = 1f;
            Assert.Equal(1f, config.FoundingFloorPoolShare);
        }

        // ------------------------------------------------------------------ the world's brain

        /// <summary>
        /// In a Central world an old per-node pool genome drawn at the floor arrives with the
        /// world's brain, converted as at the trickle: no neurons, every joint's two outputs wired.
        /// The pool's own genomes are not touched.
        /// </summary>
        [Fact]
        public void ACentralWorldConvertsAnOldPoolGenomeAtTheFloor()
        {
            IReadOnlyList<Genome> pool = Pool();
            string[] before = { GenomeJson.Write(pool[0]), GenomeJson.Write(pool[1]) };

            RunConfig config = Config(floorShare: 1f, count: 2, hash: AnyHash);
            config.NeuralCostPerNeuronWatts = 0f;
            config.NeuralCostPerConnectionWatts = 0f;
            config.Brain.BrainModel = BrainModel.Central;
            var world = new World(config, seed: 4, tricklePool: pool);

            var founders = new List<(int index, Organism founder)>();
            for (int i = 0; i < 90; i++)
            {
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism o in world.Living) living[o.Id] = o;

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.Source != FounderSource.Pool) continue;
                    Assert.True(evt.ElapsedSeconds < Closes, "a pool founder arrived after the floor closed");
                    if (living.TryGetValue(evt.Id, out Organism founder)) founders.Add((evt.PoolIndex, founder));
                }
            }

            _output.WriteLine($"{founders.Count} floor pool founders alive at their birth step; pool {world.PoolSpawns}");
            Assert.NotEmpty(founders);

            foreach ((int index, Organism o) in founders)
            {
                Assert.NotSame(pool[index], o.Genome);
                Assert.NotNull(o.Genome.Brain);
                Assert.Empty(o.Genome.GlobalBrain);
                Assert.All(o.Genome.Nodes, n => Assert.Empty(n.Neurons));
                Assert.Equal(2 * Brain.For(o.Phenotype).TotalDof, o.Genome.Brain.Outputs.Count);
            }

            Assert.Contains(founders, f => f.index == 1 && f.founder.Genome.Brain.Links.Count > 0);

            Assert.Null(pool[0].Brain);
            Assert.Null(pool[1].Brain);
            Assert.Equal(before[0], GenomeJson.Write(pool[0]));
            Assert.Equal(before[1], GenomeJson.Write(pool[1]));
        }

        // ------------------------------------------------------------------ the checkpoint

        /// <summary>
        /// The floor's stream is keyed by the floor's count, which the state carries, so a world
        /// written while the floor is still founding and restored carries on row for row, and the
        /// state is no longer than a share-0 world's.
        /// </summary>
        [Fact]
        public void AFloorShareWorldRoundTripsThroughItsState()
        {
            RunConfig config = Config(floorShare: 0.5f, count: 2, hash: AnyHash);
            const ulong Seed = 13UL;

            var original = new World(config, Seed, Pool());
            for (int i = 0; i < 10; i++) original.Step(MetabolicStep);

            Assert.True(original.PoolSpawns > 0, "no pool founder before the state was written");
            Assert.True(original.ElapsedSeconds < Closes);

            byte[] state = StateOf(original);
            var restored = new World(config, Seed, Pool());

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            long poolBefore = original.PoolSpawns;
            List<string> after = Rows(original, 200);
            Assert.Equal(after, Rows(restored, 200));
            Assert.Equal(original.PoolSpawns, restored.PoolSpawns);
            Assert.Equal(original.FloorSpawns, restored.FloorSpawns);
            Assert.True(original.PoolSpawns > poolBefore, "no pool founder after the restore");

            var zero = new World(Config(floorShare: 0f, count: 2, hash: AnyHash), Seed, Pool());
            Assert.Equal(StateOf(zero).Length, StateOf(new World(config, Seed, Pool())).Length);
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
