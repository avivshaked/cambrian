using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 42's world, stepped 400 times with a few hundred bodies in it, produces the same
    /// bits at every <see cref="Parallelism.Threads"/>, and the same bits it produced the last
    /// time the world's arithmetic was deliberately changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where the pinned word came from, and what it stopped being.</b> Until 2026-09-22 it was
    /// <c>1d1ee59f210b8bda</c>, taken on 2026-09-21 from commit <c>3560172</c>'s
    /// <c>src/Evosim.Core</c> — the tree as it stood before the threading — extracted with
    /// <c>git archive</c> into a directory of its own and compiled with the same
    /// <see cref="StateHash"/> walk this file carries. That word was the answer of a build this
    /// one had to agree with, which is a stronger thing than a recorded self-portrait. Widening a
    /// body's reserve and tissue to doubles ended that: the pre-threading build cannot produce
    /// the new word, and the hash walk now reads those two accounts as doubles besides. So
    /// <c>c9b0cabce249c1dd</c> is this build's own answer, recorded once, at 1, 4 and 16 threads
    /// alike, against the same <c>fixtures/r42-config.json</c>, the same seed, the same 300
    /// seeded founders and the same 400 metabolic steps. It pins sameness from here, as
    /// <c>BoxPathTests</c>' golden does — a change that moves it is either intended or a bug, and
    /// the test cannot tell which.
    /// </para>
    /// <para>
    /// <b>Why the hash and not a few totals.</b> A total closes over a great deal of drift — two
    /// cells that swapped a joule sum the same. This walks every cell of both grids, the canopy
    /// at every layer, every living body's reserve, tissue, age, place and patch, and the books,
    /// as raw IEEE bit patterns. A single unit in the last place anywhere in 173,000 cells moves
    /// the word.
    /// </para>
    /// <para>
    /// <b>The bodies do not move, and that is deliberate.</b> There is no solver here, so this
    /// file places every body once at a deterministic scatter and hands it back to the world at
    /// that place every step. What is under test is the fields and the economy — the 87% of the
    /// farm's wall the world step was taking — and a physics engine in the middle would only add
    /// a second thing that could differ.
    /// </para>
    /// <para>
    /// <b>Marked <c>Slow</c> and out of the default run</b> (2026-09-22): three worlds of 173,000
    /// cells stepped 400 times take 40 to 50 seconds, which was most of the default suite's
    /// minute. It is <c>-All</c>'s job now, beside the calibration sweeps — and <c>-All</c> is
    /// what CLAUDE.md already requires before a commit that touches the world, which is the only
    /// kind of commit that can move either half of what this asserts.
    /// </para>
    /// </remarks>
    [Trait("Category", "Slow")]
    public class ParallelIdentityTests
    {
        private readonly ITestOutputHelper _output;

        public ParallelIdentityTests(ITestOutputHelper output) => _output = output;

        /// <summary>The word this build writes at any thread count. See the class remarks.</summary>
        private const string TheWord = "c9b0cabce249c1dd";

        private const int Steps = 400;
        private const int Bodies = 300;
        private const ulong Seed = 1UL;

        private static string ConfigPath =>
            Path.Combine(AppContext.BaseDirectory, "fixtures", "r42-config.json");

        [Fact]
        public void Round42SteppedFourHundredTimesHashesTheSameAtEveryThreadCount()
        {
            int was = Parallelism.Threads;

            try
            {
                string one = Run(1);
                string four = Run(4);
                string sixteen = Run(16);

                _output.WriteLine("threads  1: " + one);
                _output.WriteLine("threads  4: " + four);
                _output.WriteLine("threads 16: " + sixteen);
                _output.WriteLine("pinned    : " + TheWord);

                Assert.Equal(TheWord, one);
                Assert.Equal(one, four);
                Assert.Equal(one, sixteen);
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        private string Run(int threads)
        {
            Parallelism.Threads = threads;

            RunConfig config = RunConfigJson.Read(File.ReadAllText(ConfigPath), out _);
            var world = new World(config, Seed);
            var crowd = new Crowd(world, Seed ^ 0x5eedUL);

            crowd.Seed(Bodies);
            for (int i = 0; i < Steps; i++) crowd.Step(0.5f);

            _output.WriteLine(
                threads.ToString(CultureInfo.InvariantCulture) + " threads: " +
                world.Living.Count.ToString(CultureInfo.InvariantCulture) + " bodies at t = " +
                world.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");

            return StateHash.Of(world);
        }

        // ------------------------------------------------------------------ the harness's stand-in

        /// <summary>
        /// Founders, scattered, and kept where this test put them — <c>Evosim.Core.Bench</c>'s
        /// <c>Crowd</c>, line for line, so the bench and the test drive the same world.
        /// </summary>
        private sealed class Crowd
        {
            private readonly World _world;
            private readonly Dictionary<long, Float3> _places = new Dictionary<long, Float3>();
            private readonly ulong _seed;
            private ulong _draw;

            public Crowd(World world, ulong seed)
            {
                _world = world;
                _seed = seed;
            }

            public void Seed(int wanted)
            {
                RunConfig config = _world.Config;
                var rng = new Rng(Rng.SeedFor(_seed, 7UL));

                int made = 0, tries = 0;

                while (made < wanted && tries < wanted * 20)
                {
                    tries++;

                    Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());

                    try
                    {
                        _world.Inoculate(genome, 1, -rng.Range(1f, config.WorldDepthMetres - 1f));
                        made++;
                    }
                    catch (ArgumentException)
                    {
                        // Under the newborn mass floor — draw another. The floor does the same.
                    }
                }

                Place();
            }

            public void Step(float seconds)
            {
                Place();
                _world.Step(seconds);
            }

            private void Place()
            {
                IReadOnlyList<Organism> living = _world.Living;

                for (int i = 0; i < living.Count; i++)
                {
                    Organism creature = living[i];

                    if (!_places.TryGetValue(creature.Id, out Float3 at))
                    {
                        at = Draw();
                        _places[creature.Id] = at;
                    }

                    _world.Observe(creature, at, 0f);
                }
            }

            private Float3 Draw()
            {
                var rng = new Rng(Rng.SeedFor(_seed, _draw++));

                RunConfig config = _world.Config;
                float depth = config.WorldDepthMetres;

                if (config.WorldShape == WorldShape.Tank)
                {
                    float radius = _world.TankRadiusMetres;
                    // Uniform over the disc, and a metre in from the glass so nothing sits on it.
                    float r = (float)(Math.Sqrt(rng.Range(0f, 1f)) * Math.Max(0f, radius - 1f));
                    float theta = rng.Range(0f, (float)(2.0 * Math.PI));
                    return new Float3(
                        (float)(radius + r * Math.Cos(theta)),
                        -rng.Range(1f, depth - 1f),
                        (float)(radius + r * Math.Sin(theta)));
                }

                return new Float3(
                    rng.Range(0f, _world.Nutrients.LengthMetres),
                    -rng.Range(1f, depth - 1f),
                    rng.Range(0f, _world.Nutrients.WidthMetres));
            }
        }

        // ------------------------------------------------------------------------- the state word

        /// <summary>
        /// One 64-bit word over everything a threaded field pass could move —
        /// <c>Evosim.Core.Bench</c>'s <c>StateHash</c>, line for line.
        /// </summary>
        private static class StateHash
        {
            public static string Of(World world)
            {
                ulong h = 14695981039346656037UL;

                Grid(ref h, world.Nutrients);
                Grid(ref h, world.Matter);

                int layers = (int)(world.Config.WorldDepthMetres / world.Field.LayerMetres) + 2;
                for (int i = 0; i < layers; i++)
                {
                    float y = -i * world.Field.LayerMetres;
                    Float(ref h, world.Field.IrradianceAt(y));
                    Float(ref h, world.Field.ShadingAt(y));
                }

                Double(ref h, world.Field.DayFactor);

                Long(ref h, world.Living.Count);

                for (int i = 0; i < world.Living.Count; i++)
                {
                    Organism creature = world.Living[i];

                    Long(ref h, creature.Id);
                    Double(ref h, creature.Energy);
                    Double(ref h, creature.TissueJoules);
                    Float(ref h, creature.Age);
                    Float(ref h, creature.HeightY);
                    Float(ref h, creature.X);
                    Float(ref h, creature.Z);
                    Long(ref h, creature.Patch);
                }

                Double(ref h, world.EnergyIn);
                Double(ref h, world.EnergyOut);
                Double(ref h, world.RemineralisedTotal);
                Double(ref h, world.DetritusTakenTotal);
                Double(ref h, world.BurntTotal);
                Long(ref h, world.Births);
                Long(ref h, world.Deaths);
                Long(ref h, world.Stillbirths);

                return h.ToString("x16", CultureInfo.InvariantCulture);
            }

            private static void Grid(ref ulong h, IMatterField field)
            {
                if (!(field is GridField grid))
                {
                    Double(ref h, field.TotalJoules);
                    return;
                }

                Long(ref h, grid.CellCount);

                for (int iy = 0; iy < grid.CellsY; iy++)
                {
                    for (int ix = 0; ix < grid.CellsX; ix++)
                    {
                        for (int iz = 0; iz < grid.CellsZ; iz++)
                        {
                            Double(ref h, grid.JoulesAt(ix, iy, iz));
                        }
                    }
                }

                Double(ref h, grid.TotalJoules);
            }

            private static void Double(ref ulong h, double v) =>
                Long(ref h, BitConverter.DoubleToInt64Bits(v));

            private static void Float(ref ulong h, float v) =>
                Long(ref h, BitConverter.ToInt32(BitConverter.GetBytes(v), 0));

            private static void Long(ref ulong h, long v)
            {
                ulong u = unchecked((ulong)v);

                for (int i = 0; i < 8; i++)
                {
                    h ^= (u >> (i * 8)) & 0xffUL;
                    h *= 1099511628211UL;
                }
            }
        }
    }
}
