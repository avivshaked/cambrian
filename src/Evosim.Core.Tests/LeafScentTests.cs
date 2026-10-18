using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D167's smell of living leaves, and its tank's rule that only a bite leaves food: the field,
    /// its sources, its restore, its cost, and the unbitten burn.
    /// </summary>
    public class LeafScentTests
    {
        private readonly ITestOutputHelper _out;

        public LeafScentTests(ITestOutputHelper output) => _out = output;

        private static RunConfig Lit(LeafScentForm form = LeafScentForm.Strength) => new RunConfig
        {
            MinimumPopulation = 12,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            LeafScent = form,
        };

        /// <summary>A dark world where everything starves, with corpses as objects.</summary>
        private static RunConfig Dark(bool unbitten) => new RunConfig
        {
            Light = new LightModel(1e-6f, 1f),
            MinimumPopulation = 20,
            WorldDepthMetres = 20f,
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = 0.001f,
            UnbittenDeathsLeaveNoCorpse = unbitten,
        };

        // ------------------------------------------------------------------ the dial

        [Fact]
        public void TheLeafSmellIsOffUntilItsDialIsOnAndThenComesLastInThePool()
        {
            SensorChannel[] plain = new RunConfig().SensorPool();
            Assert.DoesNotContain(SensorChannel.LeafScent, plain);
            Assert.DoesNotContain(SensorChannel.LeafScentDirection, plain);
            Assert.Null(new World(new RunConfig(), seed: 1).LeafScent);

            SensorChannel[] without = new RunConfig { SenseScent = true, SenseBedTouch = true }.SensorPool();
            SensorChannel[] strength = new RunConfig { SenseScent = true, SenseBedTouch = true, LeafScent = LeafScentForm.Strength }.SensorPool();
            SensorChannel[] direction = new RunConfig { SenseScent = true, SenseBedTouch = true, LeafScent = LeafScentForm.Direction }.SensorPool();

            Assert.Equal(without.Length + 1, strength.Length);
            Assert.Equal(without.Length + 1, direction.Length);
            for (int i = 0; i < without.Length; i++)
            {
                Assert.Equal(without[i], strength[i]);
                Assert.Equal(without[i], direction[i]);
            }

            Assert.Equal(SensorChannel.LeafScent, strength[without.Length]);
            Assert.Equal(SensorChannel.LeafScentDirection, direction[without.Length]);
            Assert.Equal(1, SensorChannel.LeafScent.IndexCount());
            Assert.Equal(3, SensorChannel.LeafScentDirection.IndexCount());
        }

        // ------------------------------------------------------------------ the sources

        [Fact]
        public void EveryLivingBodyWithPhotosyntheticTissueIsASourceByThatTissuesAreaAndNoOtherIs()
        {
            RunConfig config = Lit();
            var world = new World(config, seed: 5);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 4, heightY: -5f);
            world.Inoculate(Fixtures.SingleBox(), count: 3, heightY: -6f);
            for (int i = 0; i < 40; i++) world.Step(0.5f);

            float[] sources = world.CopyLeafSources();
            var expected = new List<float>();
            int boxes = 0;
            foreach (Organism creature in world.Living)
            {
                float area = World.PhotosyntheticLitArea(
                    creature, config.CellTypes, config.LightByExposure, config.LightSilhouetteCap);

                bool fixes = creature.Phenotype.Parts.Any(p => config.CellTypes.Resolve(p.CellTypeId).FixesLight);
                if (!fixes)
                {
                    boxes++;
                    Assert.Equal(0f, area);
                    continue;
                }

                Assert.True(area > 0f, "a body with photosynthetic tissue has no lit area");
                Float3 p0 = creature.Point.Position;
                expected.AddRange(new[] { p0.X, p0.Y, p0.Z, area });
            }

            _out.WriteLine($"{world.Living.Count} living, {world.LeafSourceCount} sources, {boxes} with nothing that fixes light");
            Assert.True(world.LeafSourceCount > 0);
            Assert.Equal(expected.ToArray(), sources);
        }

        [Fact]
        public void ALeafsAreaIsWhatTheLightPassCharges()
        {
            RunConfig config = Lit();
            var world = new World(config, seed: 5);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 1, heightY: -5f);
            Organism leaf = world.Living.Single(c => c.Phenotype.Parts.All(p => p.CellTypeId == CellTypeIds.Photosynthetic));

            float area = World.PhotosyntheticLitArea(leaf, config.CellTypes, false, config.LightSilhouetteCap);
            Assert.Equal(leaf.Phenotype.EffectiveLitArea(config.LightSilhouetteCap), area, 5);
        }

        [Fact]
        public void TheConesPeakIsTheLeafsAreaAndItIsGoneBeyondTheRange()
        {
            var field = new ScentField(40f, 40f, 20f, range: 6f, cell: 1f);
            var sources = new[] { 20.5f, -10.5f, 20.5f, 0.3f };
            field.FillSources(sources, 1);

            Assert.Equal(0.3f, field.At(20.5f, -10.5f, 20.5f), 5);
            Assert.True(field.At(23.5f, -10.5f, 20.5f) > 0f);
            Assert.Equal(0f, field.At(27.5f, -10.5f, 20.5f));
        }

        [Fact]
        public void ADeadLeafStopsAndItsCorpseIsCarrionOnly()
        {
            RunConfig config = Dark(unbitten: false);
            config.LeafScent = LeafScentForm.Strength;
            config.MinimumPopulation = 0;
            var world = new World(config, seed: 4);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 6, heightY: -5f);

            int Lit() => world.Living.Count(c =>
                World.PhotosyntheticLitArea(c, config.CellTypes, config.LightByExposure, config.LightSilhouetteCap) > 0f);

            // Births replace the dead, so the count need not fall; every fill must still take the
            // living leaves and nothing else, on the steps where some die as on the rest.
            world.Step(0.5f);
            int first = world.LeafSourceCount;
            int stepsWithDeaths = 0;
            for (int i = 0; i < 400 && world.Living.Count > 0; i++)
            {
                int corpses = world.Corpses.Count;
                world.Step(0.5f);
                if (world.Corpses.Count > corpses) stepsWithDeaths++;
                Assert.Equal(Lit(), world.LeafSourceCount);
            }

            _out.WriteLine($"{first} sources at the first fill; {world.Living.Count} living and {world.Corpses.Count} corpses at the end, {world.LeafSourceCount} sources; deaths on {stepsWithDeaths} steps");
            Assert.True(first > 0);
            Assert.True(stepsWithDeaths > 0, "nothing died, so nothing was tested");
        }

        // ------------------------------------------------------------------ the fill

        [Fact]
        public void TheSourceFillIsTheSameAtAnyThreadCount()
        {
            var rng = new Random(167);
            const int n = 2_000;
            var sources = new float[4 * n];
            for (int i = 0; i < n; i++)
            {
                sources[4 * i] = (float)(61 * rng.NextDouble());
                sources[4 * i + 1] = (float)(-33 * rng.NextDouble());
                sources[4 * i + 2] = (float)(47 * rng.NextDouble());
                sources[4 * i + 3] = (float)(0.5 * rng.NextDouble());
            }

            int was = Parallelism.Threads;
            try
            {
                float[] one = null;
                foreach (int threads in new[] { 1, 3, 16 })
                {
                    Parallelism.Threads = threads;
                    var field = new ScentField(61f, 47f, 33f, 8f, 1.5f);
                    field.FillSources(sources, n);
                    float[] cells = field.CopyCells();
                    if (one == null) { one = cells; continue; }
                    for (int i = 0; i < one.Length; i++)
                    {
                        if (BitConverter.SingleToInt32Bits(one[i]) != BitConverter.SingleToInt32Bits(cells[i]))
                            Assert.Fail($"{threads} threads: cell {i} reads {cells[i]:R}, one thread {one[i]:R}");
                    }
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        /// <summary>
        /// The cost, measured: round 58's tank (22,000 m², 25 m deep) with 6,000 leaves on 2 m
        /// cells at a 10 m range, and at 5 m, at one thread and at eight. Reported, not asserted.
        /// </summary>
        [Fact]
        public void TheFillsCostWithSixThousandLeaves()
        {
            const float side = 168f, depth = 25f;
            const int n = 6_000;
            var rng = new Random(58);
            var sources = new float[4 * n];
            for (int i = 0; i < n; i++)
            {
                sources[4 * i] = (float)(side * rng.NextDouble());
                sources[4 * i + 1] = (float)(-depth * rng.NextDouble());
                sources[4 * i + 2] = (float)(side * rng.NextDouble());
                sources[4 * i + 3] = (float)(0.05 + 0.3 * rng.NextDouble());
            }

            int was = Parallelism.Threads;
            try
            {
                foreach (float range in new[] { 10f, 5f })
                {
                    foreach (int threads in new[] { 1, 8 })
                    {
                        Parallelism.Threads = threads;
                        var field = new ScentField(side, side, depth, range, 2f);
                        field.FillSources(sources, n);   // warm

                        const int reps = 10;
                        var clock = Stopwatch.StartNew();
                        for (int r = 0; r < reps; r++) field.FillSources(sources, n);
                        double ms = clock.Elapsed.TotalMilliseconds / reps;

                        _out.WriteLine($"6,000 leaves, {range} m range, 2 m cells, {threads} thread(s): {ms:0.0} ms a fill");
                    }
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        // ------------------------------------------------------------------ the restore

        [Fact]
        public void ARestoredWorldReadsTheLiveLeafField()
        {
            RunConfig config = Lit();
            var world = new World(config, seed: 9);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 6, heightY: -5f);
            for (int i = 0; i < 60; i++) world.Step(0.5f);
            Assert.True(world.LeafSourceCount > 0);

            byte[] state = StateOf(world);
            World restored = Restored(config, 9, state);

            Assert.Equal(world.LeafSourceCount, restored.LeafSourceCount);
            Assert.Equal(world.CopyLeafSources(), restored.CopyLeafSources());
            Assert.Equal(world.LeafScent.CopyCells(), restored.LeafScent.CopyCells());

            for (int i = 0; i < 60; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(StateOf(world), StateOf(restored));

            // Version 15 has no place for the sources.
            Assert.ThrowsAny<Exception>(() => StateOf(world, World.PreLeafStateVersion));
        }

        // ------------------------------------------------------------------ the unbitten burn

        [Fact]
        public void WithTheBurnOnAStarvedBodyLeavesNoCorpseAndTheBooksClose()
        {
            var control = new World(Dark(unbitten: false), seed: 2);
            var burnt = new World(Dark(unbitten: true), seed: 2);
            double spentBefore = burnt.Matter.TotalJoules;

            for (int i = 0; i < 200; i++)
            {
                control.Step(0.5f);
                burnt.Step(0.5f);
            }

            _out.WriteLine(
                $"control: {control.Deaths} deaths, {control.Corpses.Count} corpses; " +
                $"burnt: {burnt.Deaths} deaths, {burnt.Corpses.Count} corpses, spent units {spentBefore:0.##} to {burnt.Matter.TotalJoules:0.##}");

            Assert.True(control.Corpses.Count > 0, "the control left no corpse, so nothing was tested");
            Assert.True(burnt.Deaths > 0);
            Assert.Empty(burnt.Corpses);
            Assert.True(burnt.Matter.TotalJoules > spentBefore, "the dead returned no spent units");

            Assert.True(
                Math.Abs(burnt.AuditResidual) <= 1e-6 * Math.Max(1d, burnt.StandingJoules),
                $"audit residual {burnt.AuditResidual:R}");
            Assert.True(
                Math.Abs(burnt.MatterResidual) <= 1e-6 * Math.Max(1d, burnt.MatterInitialTotal),
                $"matter residual {burnt.MatterResidual:R}");
        }

        // ------------------------------------------------------------------ helpers

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

        private static byte[] StateOf(World world, int layout)
        {
            System.Reflection.MethodInfo writer = typeof(World).GetMethod(
                "WriteState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(BinaryWriter), typeof(int) }, null);

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    try
                    {
                        writer.Invoke(world, new object[] { w, layout });
                    }
                    catch (System.Reflection.TargetInvocationException e)
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
