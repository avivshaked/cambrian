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
            Assert.Throws<ArgumentOutOfRangeException>(() => config.FoundingTricklePoolCount = RunConfig.MaxTricklePool + 1);
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = "ABC");
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = new string('A', 64));
            Assert.Throws<ArgumentException>(() => config.FoundingTricklePoolHash = null);

            config.FoundingTricklePoolShare = 1f;
            config.FoundingTricklePoolCount = 1000;
            config.FoundingTricklePoolHash = AnyHash;
            Assert.Equal(1000, RunConfig.MaxTricklePool);
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

        // ------------------------------------------------------------------ the world's brain

        /// <summary>
        /// <see cref="Config"/>'s world under D146's one brain, the neural price at 0 as a Central
        /// world requires.
        /// </summary>
        private static RunConfig CentralConfig(float share, int count)
        {
            RunConfig config = Config(share, count, AnyHash);
            config.NeuralCostPerNeuronWatts = 0f;
            config.NeuralCostPerConnectionWatts = 0f;
            config.Brain.BrainModel = BrainModel.Central;
            return config;
        }

        /// <summary>A pool genome under D146's one brain, wired to its own body.</summary>
        private static Genome Wired(string file, RunConfig config)
        {
            Genome genome = Load(file);
            CentralWiring.Convert(genome);
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
            genome.Brain = CentralWiring.Effective(
                genome.Brain, body, new Rng(77), config.SensorPool(), config.Brain, founder: true);
            Assert.Empty(genome.Brain.Validate());
            return genome;
        }

        /// <summary>
        /// Every pool founder of a world stepped <paramref name="steps"/> times that is alive at
        /// the end of its birth step, with the pool index it was copied from.
        /// </summary>
        private static List<(int index, Organism founder)> PoolFounders(World world, int steps)
        {
            var found = new List<(int, Organism)>();
            for (int i = 0; i < steps; i++)
            {
                world.Step(MetabolicStep);

                var living = new Dictionary<long, Organism>();
                foreach (Organism o in world.Living) living[o.Id] = o;

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.Source != FounderSource.Pool) continue;
                    if (living.TryGetValue(evt.Id, out Organism founder)) found.Add((evt.PoolIndex, founder));
                }
            }

            return found;
        }

        /// <summary>
        /// The owner's ruling of 2026-10-04: in a Central world an old per-node pool genome arrives
        /// with the world's brain, converted as a lottery founder is: no neurons, and every joint's
        /// two outputs wired fresh. The pool's own genomes are not touched.
        /// </summary>
        [Fact]
        public void ACentralWorldConvertsAnOldPoolGenomeAtArrival()
        {
            IReadOnlyList<Genome> pool = Pool();
            string[] before = { GenomeJson.Write(pool[0]), GenomeJson.Write(pool[1]) };

            var world = new World(CentralConfig(share: 1f, count: 2), seed: 4, tricklePool: pool);
            List<(int index, Organism founder)> founders = PoolFounders(world, 800);

            _output.WriteLine($"{founders.Count} pool founders alive at their birth step; pool {world.PoolSpawns}");
            Assert.NotEmpty(founders);

            foreach ((int index, Organism o) in founders)
            {
                Assert.NotSame(pool[index], o.Genome);
                Assert.NotNull(o.Genome.Brain);
                Assert.Empty(o.Genome.GlobalBrain);
                Assert.All(o.Genome.Nodes, n => Assert.Empty(n.Neurons));
                Assert.Equal(2 * Brain.For(o.Phenotype).TotalDof, o.Genome.Brain.Outputs.Count);
            }

            // The jointed genome's founders are wired, not merely emptied.
            Assert.Contains(founders, f => f.index == 1 && f.founder.Genome.Brain.Links.Count > 0);

            Assert.Null(pool[0].Brain);
            Assert.Null(pool[1].Brain);
            Assert.Equal(before[0], GenomeJson.Write(pool[0]));
            Assert.Equal(before[1], GenomeJson.Write(pool[1]));
        }

        /// <summary>
        /// The conversion is the lottery founder's and draws only from the founder's own brain
        /// stream: a pool converted at arrival is, row for row, the world handed the same pool
        /// already converted (an empty central brain, which the world wires on arrival), and the
        /// trickle's own stream still predicts every count.
        /// </summary>
        [Fact]
        public void ConversionAtArrivalIsTheLotteryFoundersAndMovesNoOtherDraw()
        {
            const ulong Seed = 9UL;
            const int Steps = 800;

            var preconverted = new List<Genome>();
            foreach (Genome g in Pool())
            {
                Genome c = g.Clone();
                CentralWiring.Convert(c);
                preconverted.Add(c);
            }

            var atArrival = new World(CentralConfig(share: 0.5f, count: 2), Seed, Pool());
            var before = new World(CentralConfig(share: 0.5f, count: 2), Seed, preconverted);

            List<string> a = Rows(atArrival, Steps);
            List<string> b = Rows(before, Steps);

            Assert.Contains(a, row => row.Contains("\"src\":\"pool\""));
            Assert.Equal(a, b);

            var stream = new Rng(Rng.SeedFor(Seed, World.TrickleIndex));
            long trickle = 0;
            long pool = 0;
            for (int i = 0; i < Steps; i++)
            {
                if ((i + 1) * MetabolicStep < Closes) continue;

                int count = World.TrickleCount(stream, (double)Rate * MetabolicStep);
                for (int k = 0; k < count; k++)
                {
                    trickle++;
                    if (stream.NextFloat() < 0.5f)
                    {
                        stream.NextFloat();
                        pool++;
                    }
                }
            }

            Assert.Equal(trickle, atArrival.TrickleSpawns);
            Assert.Equal(pool, atArrival.PoolSpawns);
        }

        /// <summary>
        /// A pool genome that already carries a central brain keeps it as it is: wired to the
        /// body it develops and never mutated, so the founder's genome writes the pool's bytes.
        /// </summary>
        [Fact]
        public void ACentralPoolGenomeKeepsItsBrain()
        {
            RunConfig config = CentralConfig(share: 1f, count: 2);
            Genome wired = Wired("growth-ledger-genome.json", config);
            string expected = GenomeJson.Write(wired);
            Assert.NotEmpty(wired.Brain.Links);

            var world = new World(config, seed: 4, tricklePool: new[] { Load("r45s1-stomach-100.json"), wired });
            List<(int index, Organism founder)> founders = PoolFounders(world, 800);

            int copies = 0;
            foreach ((int index, Organism o) in founders)
            {
                if (index != 1) continue;
                Assert.NotSame(wired, o.Genome);
                Assert.Equal(expected, GenomeJson.Write(o.Genome));
                copies++;
            }

            Assert.True(copies > 0, "no founder copied the central pool genome");
            Assert.Equal(expected, GenomeJson.Write(wired));
        }

        /// <summary>
        /// A per-node world refuses a central pool genome at construction: it would breed central
        /// children in a world whose config says per-node. The same pool is a Central world's.
        /// </summary>
        [Fact]
        public void APerNodeWorldRefusesACentralPoolGenome()
        {
            Genome central = Load("growth-ledger-genome.json");
            CentralWiring.Convert(central);
            var pool = new[] { Load("r45s1-stomach-100.json"), central };

            var e = Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0.5f, count: 2, hash: AnyHash), 1, pool));
            Assert.Contains("Trickle pool genome 01 (pool/01.json) carries a central brain", e.Message);

            // Named and unused is refused too: the pool the config pins has to be one the world can run.
            Assert.Throws<ArgumentException>(
                () => new World(Config(share: 0f, count: 2, hash: AnyHash), 1, pool));

            Assert.NotNull(new World(CentralConfig(share: 0.5f, count: 2), 1, pool));
        }

        // ------------------------------------------------------------------ the weights

        /// <summary>
        /// The weighted search on its own: across an even grid of uniforms each genome takes a
        /// share proportional to its weight, the intervals are closed below and open above, and
        /// the two ends of [0, 1) land on the first and last genome.
        /// </summary>
        [Fact]
        public void TheWeightedSearchGivesEachGenomeItsShareOfTheUniform()
        {
            double[] weights = { 1d, 2d, 0.5d, 6.5d };
            double[] cumulative = Cumulative(weights);

            const int Grid = 1 << 16;
            var counts = new int[weights.Length];
            for (int k = 0; k < Grid; k++) counts[World.WeightedPoolIndex(cumulative, k / (float)Grid)]++;

            for (int i = 0; i < weights.Length; i++)
            {
                double share = (double)counts[i] / Grid;
                Assert.InRange(share, weights[i] / 10d - 1d / Grid, weights[i] / 10d + 1d / Grid);
            }

            Assert.Equal(0, World.WeightedPoolIndex(cumulative, 0f));
            Assert.Equal(3, World.WeightedPoolIndex(cumulative, 1f - 1f / 16777216f));

            // Weights 1, 1, 2: a quarter and a half are the boundaries, and each belongs above.
            double[] edges = Cumulative(new[] { 1d, 1d, 2d });
            Assert.Equal(0, World.WeightedPoolIndex(edges, 0.2499f));
            Assert.Equal(1, World.WeightedPoolIndex(edges, 0.25f));
            Assert.Equal(2, World.WeightedPoolIndex(edges, 0.5f));
        }

        private static double[] Cumulative(double[] weights)
        {
            var sums = new double[weights.Length];
            double total = 0d;
            for (int i = 0; i < weights.Length; i++) sums[i] = total += weights[i];
            return sums;
        }

        /// <summary>
        /// A world with a weighted pool, walked against a fresh copy of the trickle's stream: the
        /// draws are the unweighted pool's in number (the weights read the same second uniform),
        /// the founders the world admits are the predicted picks in order, and the picks follow
        /// the weights (a nine-to-one pool, read with a generous tolerance).
        /// </summary>
        [Fact]
        public void AWeightedPoolPicksByItsWeightsOnTheSameUniform()
        {
            double[] weights = { 1d, 9d, 0.0001d };
            (List<int> drawn, List<int> admitted, World world) = DrawAndAdmit(weights, seed: 21UL);

            int heavy = 0;
            foreach (int index in drawn) if (index == 1) heavy++;
            double share = (double)heavy / drawn.Count;

            _output.WriteLine(
                $"{drawn.Count} pool draws, {admitted.Count} admitted; genome 1 took {share:0.000} " +
                $"(weight share {9d / 10.0001d:0.000})");

            Assert.True(drawn.Count >= 200, "too few pool draws to read a share");
            Assert.InRange(share, 0.8d, 0.97d);
            Assert.Equal(weights, world.TricklePoolWeights);
        }

        /// <summary>
        /// Without weights the pick is the expression every recorded pool world drew with,
        /// <c>min(N − 1, (int)(u · N))</c> in single precision, on a pool of three so that the
        /// product is not exact: the admitted founders are its picks, in order.
        /// </summary>
        [Fact]
        public void AnUnweightedPoolPicksByTheRecordedExpression()
        {
            (List<int> drawn, _, World world) = DrawAndAdmit(null, seed: 21UL);

            Assert.Equal(3, new HashSet<int>(drawn).Count);
            Assert.Null(world.TricklePoolWeights);
        }

        /// <summary>
        /// A share-1 world over a pool of three, stepped 1,600 times beside a fresh copy of the
        /// trickle's stream that predicts every pool pick; asserts the counts and that the
        /// admitted founders' indices are the predicted picks, in order and one for one (this
        /// small world refuses none of these founders, so every draw queues a row).
        /// </summary>
        private (List<int> drawn, List<int> admitted, World world) DrawAndAdmit(double[] weights, ulong seed)
        {
            const int Steps = 1600;
            var pool = new[] { Load("r45s1-stomach-100.json"), Load("growth-ledger-genome.json"), Load("r45s1-stomach-100.json") };
            var world = new World(Config(share: 1f, count: 3, hash: AnyHash), seed, pool, weights);

            var stream = new Rng(Rng.SeedFor(seed, World.TrickleIndex));
            double[] cumulative = weights == null ? null : Cumulative(weights);
            var drawn = new List<int>();
            var admitted = new List<int>();

            for (int i = 0; i < Steps; i++)
            {
                world.Step(MetabolicStep);
                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind == LineageEventKind.Birth && evt.Source == FounderSource.Pool) admitted.Add(evt.PoolIndex);
                }

                if (world.ElapsedSeconds < Closes) continue;

                int count = World.TrickleCount(stream, (double)Rate * MetabolicStep);
                for (int k = 0; k < count; k++)
                {
                    // The share's uniform: always below a share of 1, and taken all the same.
                    stream.NextFloat();
                    drawn.Add(cumulative == null
                        ? Math.Min(pool.Length - 1, (int)(stream.NextFloat() * pool.Length))
                        : World.WeightedPoolIndex(cumulative, stream.NextFloat()));
                }
            }

            Assert.Equal(drawn.Count, world.PoolSpawns);
            Assert.Equal(drawn, admitted);

            _output.WriteLine($"{drawn.Count} drawn, {admitted.Count} admitted");
            return (drawn, admitted, world);
        }

        /// <summary>
        /// Weights the pool cannot carry are refused at construction: a count that is not the
        /// pool's, weights with no pool, and a weight that is 0, negative, not a number or infinite.
        /// </summary>
        [Fact]
        public void WeightsThePoolCannotCarryAreRefused()
        {
            RunConfig config = Config(share: 0.5f, count: 2, hash: AnyHash);

            var shortList = Assert.Throws<ArgumentException>(() => new World(config, 1, Pool(), new[] { 1d }));
            Assert.Contains("1 trickle pool weights and a pool of 2 genomes", shortList.Message);

            var noPool = Assert.Throws<ArgumentException>(() => new World(Config(), 1, null, new[] { 1d }));
            Assert.Contains("a pool of 0 genomes", noPool.Message);

            foreach (double bad in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
            {
                var e = Assert.Throws<ArgumentException>(() => new World(config, 1, Pool(), new[] { 1d, bad }));
                Assert.Contains("Trickle pool weight 01", e.Message);
            }

            Assert.Equal(new[] { 1d, 3d }, new World(config, 1, Pool(), new[] { 1d, 3d }).TricklePoolWeights);
            Assert.Null(new World(config, 1, Pool()).TricklePoolWeights);
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
