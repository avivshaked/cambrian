using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// propose-foraging.md's third rule: the cone a corpse lays down, read between cell centres.
    /// </summary>
    // Parallelism.Threads is a static: every test class that sets it shares this collection, so xUnit runs them one at a time.
    [Collection("Parallelism.Threads")]
    public sealed class ScentFieldTests
    {
        private static ScentField Field() => new ScentField(40f, 40f, 40f, 10f);

        [Fact]
        public void AnEmptyFieldReadsNothing()
        {
            Assert.Equal(0f, Field().At(20f, -20f, 20f));
        }

        [Fact]
        public void AtTheSourceTheConeReadsItsJoulesLessTheCellsDistance()
        {
            ScentField field = Field();
            field.Add(new Float3(20f, -20f, 20f), 100.0);

            // The source sits on a corner shared by eight cell centres, each root-3 metres away,
            // so the interpolation returns the cone at that distance.
            double expected = 100.0 * (1.0 - Math.Sqrt(3.0) / 10.0);
            Assert.InRange(field.At(20f, -20f, 20f), expected - 1e-3, expected + 1e-3);
        }

        [Fact]
        public void TheScentFallsWithDistanceAndIsGoneBeyondTheRange()
        {
            ScentField field = Field();
            field.Add(new Float3(10f, -20f, 20f), 100.0);

            float previous = float.MaxValue;
            for (float x = 10f; x <= 20f; x += 1f)
            {
                float here = field.At(x, -20f, 20f);
                Assert.True(here <= previous, $"the scent rose from {previous} to {here} at x {x}");
                previous = here;
            }

            Assert.Equal(0f, field.At(24f, -20f, 20f));
            Assert.Equal(0f, field.At(10f, -20f, 34f));
        }

        [Fact]
        public void TwoSourcesAddAndClearEmpties()
        {
            ScentField one = Field();
            one.Add(new Float3(20f, -20f, 20f), 100.0);

            ScentField two = Field();
            two.Add(new Float3(20f, -20f, 20f), 60.0);
            two.Add(new Float3(20f, -20f, 20f), 40.0);

            Assert.Equal(one.At(21f, -19f, 22f), two.At(21f, -19f, 22f), 3);

            two.Clear();
            Assert.Equal(0f, two.At(20f, -20f, 20f));
        }

        [Fact]
        public void AnEmptySourceAndAPointOffTheGridAreHarmless()
        {
            ScentField field = Field();
            field.Add(new Float3(20f, -20f, 20f), 0.0);
            field.Add(new Float3(20f, -20f, 20f), double.NaN);
            Assert.Equal(0f, field.At(20f, -20f, 20f));

            field.Add(new Float3(1f, -1f, 1f), 50.0);
            Assert.True(field.At(-5f, 3f, -5f) > 0f, "a point off the grid should read its nearest edge");
            Assert.Equal(0f, field.At(float.NaN, -1f, 1f));
        }

        /// <summary>
        /// The fill split over rows of cells is the serial fill, cell for cell and bit for bit, at
        /// any thread count, including counts that split the rows unevenly. The reference below is
        /// the serial fill as it stood before the split, copied.
        /// </summary>
        [Fact]
        public void TheSplitFillIsTheSerialFillAtAnyThreadCount()
        {
            const float length = 61f, width = 47f, depth = 33f, range = 10f;
            var rng = new Random(57);
            var corpses = new List<Corpse>();

            // Corners, edges and faces of the grid, cones poking out of it, and sources outside it.
            float[] xs = { 0f, 0.5f, 30.5f, 60.9f, 61f, -3f, 64f };
            float[] ys = { 0f, -0.5f, -16.5f, -32.9f, -33f, 2f, -36f };
            float[] zs = { 0f, 0.5f, 23.5f, 46.9f, 47f, -3f, 50f };
            foreach (float x in xs)
                foreach (float y in ys)
                    foreach (float z in zs)
                        corpses.Add(NewCorpse(new Float3(x, y, z), 1.0 + 999.0 * rng.NextDouble()));

            // A crowd of ordinary corpses, so every row boundary of every split is crossed by cones,
            // and the sources that add nothing.
            for (int c = 0; c < 3_000; c++)
            {
                var p = new Float3((float)(length * rng.NextDouble()), (float)(-depth * rng.NextDouble()), (float)(width * rng.NextDouble()));
                corpses.Add(NewCorpse(p, 1500.0 * rng.NextDouble()));
            }
            corpses.Add(NewCorpse(new Float3(20f, -10f, 20f), 0.0));
            corpses.Add(NewCorpse(new Float3(20f, -10f, 20f), -5.0));
            corpses.Add(NewCorpse(new Float3(20f, -10f, 20f), double.NaN));

            float[] expected = SerialFill(corpses, corpses.Count, length, width, depth, range);
            int was = Parallelism.Threads;
            try
            {
                foreach (int threads in new[] { 1, 3, 16 })
                {
                    foreach (int count in new[] { corpses.Count, corpses.Count - 7 })
                    {
                        float[] want = count == corpses.Count ? expected : SerialFill(corpses, count, length, width, depth, range);
                        Parallelism.Threads = threads;
                        var field = new ScentField(length, width, depth, range);
                        field.Fill(corpses, count);
                        float[] got = field.CopyCells();

                        Assert.Equal(want.Length, got.Length);
                        for (int i = 0; i < want.Length; i++)
                        {
                            if (BitConverter.SingleToInt32Bits(want[i]) != BitConverter.SingleToInt32Bits(got[i]))
                                Assert.Fail($"{threads} threads, {count} corpses: cell {i} reads {got[i]:R}, the serial fill {want[i]:R}");
                        }
                    }
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }

            Assert.Contains(expected, v => v > 0f);
        }

        [Fact]
        public void TheRootSkipChangesNoCellAtTheRangesEdge()
        {
            // C1 (card-next-r57.md): the fill skips a cell's root once its sum of squares reaches the
            // range squared. Corpses on a half-metre lattice put cell centres exactly at the range
            // (6, 8 and 0 metres off at 10 m; 3, 4 and 0 at 5 m; 1.5, 2 and 0 at 2.5 m). Ranges that
            // are not whole metres put the bound between the lattice's sums, a tiny range leaves most
            // of each box outside it, and corpses anywhere fill in the rest. The fill must be the
            // serial fill's to the bit at every thread count.
            const float length = 41f, width = 37f, depth = 29f;
            var rng = new Random(58);
            var corpses = new List<Corpse>();
            for (int c = 0; c < 1_500; c++)
            {
                var p = new Float3(0.5f * rng.Next(-4, 2 * (int)length + 5), -0.5f * rng.Next(-4, 2 * (int)depth + 5),
                    0.5f * rng.Next(-4, 2 * (int)width + 5));
                corpses.Add(NewCorpse(p, 1.0 + 999.0 * rng.NextDouble()));
            }
            for (int c = 0; c < 500; c++)
            {
                var p = new Float3((float)(length * rng.NextDouble()), (float)(-depth * rng.NextDouble()), (float)(width * rng.NextDouble()));
                corpses.Add(NewCorpse(p, 1500.0 * rng.NextDouble()));
            }

            int was = Parallelism.Threads;
            try
            {
                foreach (float range in new[] { 10f, 5f, 2.5f, 7.3f, 13.7f, 0.1f })
                {
                    float[] want = SerialFill(corpses, corpses.Count, length, width, depth, range);
                    foreach (int threads in new[] { 1, 3, 16 })
                    {
                        Parallelism.Threads = threads;
                        var field = new ScentField(length, width, depth, range);
                        field.Fill(corpses, corpses.Count);
                        float[] got = field.CopyCells();

                        Assert.Equal(want.Length, got.Length);
                        for (int i = 0; i < want.Length; i++)
                        {
                            if (BitConverter.SingleToInt32Bits(want[i]) != BitConverter.SingleToInt32Bits(got[i]))
                                Assert.Fail($"range {range:R}, {threads} threads: cell {i} reads {got[i]:R}, the serial fill {want[i]:R}");
                        }
                    }

                    // The lattice reached the bound itself at the whole and half-metre ranges.
                    if (range == 10f || range == 5f || range == 2.5f)
                        Assert.True(OnTheBound(corpses, length, width, depth, range) > 0, $"no cell sits exactly at {range} m");
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        /// <summary>The (corpse, cell) pairs whose sum of squares is the range squared, exactly.</summary>
        private static long OnTheBound(List<Corpse> corpses, float length, float width, float depth, float range)
        {
            const float cell = ScentField.CellMetres;
            int nx = Math.Max(1, (int)Math.Ceiling(length / cell));
            int ny = Math.Max(1, (int)Math.Ceiling(depth / cell));
            int nz = Math.Max(1, (int)Math.Ceiling(width / cell));
            double r2 = (double)range * range;
            int reach = (int)Math.Ceiling(range / cell);
            long n = 0;
            foreach (Corpse corpse in corpses)
            {
                Float3 p = corpse.Position;
                int ci = (int)Math.Floor(p.X / cell), cj = (int)Math.Floor(-p.Y / cell), ck = (int)Math.Floor(p.Z / cell);
                for (int i = Math.Max(0, ci - reach); i <= Math.Min(nx - 1, ci + reach); i++)
                    for (int j = Math.Max(0, cj - reach); j <= Math.Min(ny - 1, cj + reach); j++)
                        for (int k = Math.Max(0, ck - reach); k <= Math.Min(nz - 1, ck + reach); k++)
                        {
                            double dx = (i + 0.5) * cell - p.X, dy = -(j + 0.5) * cell - p.Y, dz = (k + 0.5) * cell - p.Z;
                            if (dx * dx + dy * dy + dz * dz == r2) n++;
                        }
            }
            return n;
        }

        private static Corpse NewCorpse(Float3 position, double joules) =>
            (Corpse)Activator.CreateInstance(
                typeof(Corpse), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { 1L, position, 0, joules }, null);

        /// <summary>The fill before it was split over rows: every corpse, in order, on one thread.</summary>
        private static float[] SerialFill(List<Corpse> corpses, int count, float length, float width, float depth, float range)
        {
            const float cell = ScentField.CellMetres;
            int nx = Math.Max(1, (int)Math.Ceiling(length / cell));
            int ny = Math.Max(1, (int)Math.Ceiling(depth / cell));
            int nz = Math.Max(1, (int)Math.Ceiling(width / cell));
            var v = new float[nx * ny * nz];
            for (int c = 0; c < count; c++)
            {
                Float3 p = corpses[c].Position;
                double joules = corpses[c].Joules;
                if (!(joules > 0)) continue;
                int reach = (int)Math.Ceiling(range / cell);
                int ci = (int)Math.Floor(p.X / cell);
                int cj = (int)Math.Floor(-p.Y / cell);
                int ck = (int)Math.Floor(p.Z / cell);
                for (int i = Math.Max(0, ci - reach); i <= Math.Min(nx - 1, ci + reach); i++)
                {
                    double dx = (i + 0.5) * cell - p.X;
                    for (int j = Math.Max(0, cj - reach); j <= Math.Min(ny - 1, cj + reach); j++)
                    {
                        double dy = -(j + 0.5) * cell - p.Y;
                        for (int k = Math.Max(0, ck - reach); k <= Math.Min(nz - 1, ck + reach); k++)
                        {
                            double dz = (k + 0.5) * cell - p.Z;
                            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            if (d >= range) continue;
                            v[(i * ny + j) * nz + k] += (float)(joules * (1.0 - d / range));
                        }
                    }
                }
            }
            return v;
        }

        [Fact]
        public void TheRangeMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScentField(10f, 10f, 10f, 0f));
        }

        [Fact]
        public void TheScentIsOffUntilItsDialIsOnAndThenComesLastInThePool()
        {
            // The append-order argument of Contact and Damage: a run with the dial off draws the
            // same channel for the same neuron as the record did.
            SensorChannel[] plain = new RunConfig().SensorPool();
            Assert.DoesNotContain(SensorChannel.Scent, plain);

            SensorChannel[] wider = new RunConfig { SenseContact = true, SenseDamage = true, SenseScent = true }.SensorPool();
            SensorChannel[] without = new RunConfig { SenseContact = true, SenseDamage = true }.SensorPool();

            Assert.Equal(without.Length + 1, wider.Length);
            for (int i = 0; i < without.Length; i++) Assert.Equal(without[i], wider[i]);
            Assert.Equal(SensorChannel.Scent, wider[without.Length]);
        }

        [Fact]
        public void AWorldRefusesAScentWithNoCorpsesToSmell()
        {
            var config = new RunConfig { SenseScent = true, CorpseDecayPerSecond = 0f };
            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
            Assert.Null(new World(new RunConfig(), seed: 1).Scent);
        }

        [Fact]
        public void AWorldsScentRisesWhereItsFirstCorpseLies()
        {
            // A dark world: the floor founds bodies, they starve, and their corpses lie as objects.
            var config = new RunConfig
            {
                Light = new LightModel(1e-6f, 1f),
                MinimumPopulation = 20,
                CorpseDecayPerSecond = 0.005f,
                SenseScent = true,
            };

            var world = new World(config, seed: 1);
            Assert.NotNull(world.Scent);

            Corpse corpse = null;
            for (int step = 0; step < 2_000 && corpse == null; step++)
            {
                world.Step(0.5f);
                if (world.Corpses.Count > 0) corpse = world.Corpses[0];
            }

            Assert.NotNull(corpse);
            float here = world.Scent.At(corpse.Position.X, corpse.Position.Y, corpse.Position.Z);
            Assert.True(here > 0f, $"the scent read {here} on a corpse of {corpse.Joules} J");
        }
    }
}