using System;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D212: a corpse's food value halves every <see cref="RunConfig.CorpseHalfLifeSeconds"/> whether or
    /// not anything eats it. The energy leaves as heat, the matter goes to the spent field, and both
    /// books close in the same line.
    /// </summary>
    public class CorpseSpoilageTests
    {
        private readonly ITestOutputHelper _output;

        public CorpseSpoilageTests(ITestOutputHelper output) => _output = output;

        /// <summary>A dark world where the only food is a corpse: nothing is born and nothing photosynthesises.</summary>
        private static RunConfig Dark(float halfLife, float decay = 1e-9f, bool keeps = false) => new RunConfig
        {
            Light = new LightModel(1e-6f, 1f),
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = decay,
            CorpseSinkMetresPerSecond = 0.2f,
            CorpseSettles = true,
            CorpseHalfLifeSeconds = halfLife,
            PlantsKeepMatter = keeps,
        };

        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
        }

        // ------------------------------------------------------------------ off

        [Fact]
        public void TheDialIsOffByDefaultAndAtZeroNothingIsSpoiled()
        {
            Assert.Equal(0f, new RunConfig().CorpseHalfLifeSeconds);

            var world = new World(Dark(halfLife: 0f), seed: 1);
            Corpse corpse = world.PlaceCorpse(new Float3(1f, -5f, 1f), 100d);
            for (int i = 0; i < 200; i++) world.Step(0.5f);

            // Only the instalments, at 1e-9 a second, have touched it.
            Assert.True(corpse.Joules > 100d * (1d - 1e-6), $"{corpse.Joules:R}");
            Assert.Equal(0d, world.CorpseSpoiledTotal);
            AssertBooksClose(world);
        }

        // ------------------------------------------------------------------ the clock

        [Fact]
        public void ACorpseHoldsHalfAfterOneHalfLifeAndAQuarterAfterTwo()
        {
            const float H = 100f;
            var world = new World(Dark(H), seed: 1);
            Corpse corpse = world.PlaceCorpse(new Float3(1f, -5f, 1f), 100d);
            double first = corpse.FirstJoules;

            for (int i = 0; i < 200; i++) world.Step(0.5f);
            _output.WriteLine($"after H: {corpse.Joules / first:R}");
            Assert.True(Math.Abs(corpse.Joules / first - 0.5) < 1e-5, $"{corpse.Joules / first:R}");

            for (int i = 0; i < 200; i++) world.Step(0.5f);
            _output.WriteLine($"after 2H: {corpse.Joules / first:R}");
            Assert.True(Math.Abs(corpse.Joules / first - 0.25) < 1e-5, $"{corpse.Joules / first:R}");

            // The corpse was sinking for the first part and settled for the rest: the clock ran on both.
            Assert.True(corpse.Settled, "the corpse never settled, so the settled half of the clock is unread");

            // What it lost is the total, in the one place the world counts it.
            Assert.True(world.CorpseSpoiledTotal > 74.99d, $"{world.CorpseSpoiledTotal:R}");
            AssertBooksClose(world);
        }

        [Fact]
        public void TheSpoiledJoulesAreHeatAndTheirMatterIsTheSpentFields()
        {
            var world = new World(Dark(halfLife: 50f), seed: 1);
            world.PlaceCorpse(new Float3(1f, -5f, 1f), 100d);

            double out0 = world.EnergyOut;
            double matter0 = world.Matter.TotalJoules;
            world.Step(0.5f);

            double spoiled = world.CorpseSpoiledTotal;
            Assert.True(spoiled > 0d);
            Assert.Equal(spoiled, world.EnergyOut - out0, 9);

            // The spent field gained the spoiled joules' units; the instalments go to the charged field.
            double gained = world.Matter.TotalJoules - matter0;
            Assert.True(Math.Abs(gained - spoiled / world.Config.JoulesPerUnit) < 1e-6, $"gained {gained:R}");
            AssertBooksClose(world);
        }

        // ------------------------------------------------------------------ the books, with eaters and the store

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheBooksCloseAndTheEnergyOutIsTheDialOffWorldsPlusTheSpoilage(bool keeps)
        {
            // Starving leaves die in the dark and leave corpses; the same world with the dial off is the
            // reference. Both are stepped together while they have lived the same lives.
            RunConfig on = Dark(halfLife: 60f, decay: 0.002f, keeps: keeps);
            RunConfig off = Dark(halfLife: 0f, decay: 0.002f, keeps: keeps);
            var a = new World(on, seed: 5);
            var b = new World(off, seed: 5);
            a.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 8, heightY: -5f);
            b.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 8, heightY: -5f);

            int compared = 0;
            for (int i = 0; i < 600; i++)
            {
                a.Step(0.5f);
                b.Step(0.5f);
                AssertBooksClose(a);

                if (a.Births != b.Births || a.Deaths != b.Deaths) break;
                double expected = a.EnergyOut - b.EnergyOut;
                Assert.True(
                    Math.Abs(expected - a.CorpseSpoiledTotal) <= 1e-9 * Math.Max(1d, a.EnergyOut),
                    $"step {i}: EnergyOut differs by {expected:R}, spoiled {a.CorpseSpoiledTotal:R}");
                compared++;
            }

            _output.WriteLine(
                $"compared {compared} steps; deaths {a.Deaths}, spoiled {a.CorpseSpoiledTotal:R} J, " +
                $"held released {a.HeldReleasedTotal:R}, corpses {a.Corpses.Count}");
            Assert.True(a.Deaths > 0, "nothing starved, so no corpse was spoiled");
            Assert.True(a.CorpseSpoiledTotal > 0d);
            Assert.True(compared > 100, $"the worlds parted after {compared} steps");

            if (keeps)
            {
                // D206: the held units are not spoilage's; the store's own books still close.
                double store = a.HeldTotal - a.RechargedTotal - a.HeldReleasedTotal;
                Assert.True(
                    Math.Abs(a.HeldUnits - store) <= 1e-9 * Math.Max(1d, a.HeldTotal),
                    $"held {a.HeldUnits:R} against the counters' {store:R}");
            }
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void TheDialIsRefusedWhereCorpsesAreNotObjectsAndForANegativeNaNOrInfiniteValue()
        {
            RunConfig none = Dark(halfLife: 100f, decay: 0f);
            Assert.Throws<ArgumentException>(() => new World(none, seed: 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { CorpseHalfLifeSeconds = -1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { CorpseHalfLifeSeconds = float.NaN });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { CorpseHalfLifeSeconds = float.PositiveInfinity });

            Assert.NotNull(new World(Dark(halfLife: 100f), seed: 1));
        }

        // ------------------------------------------------------------------ the checkpoint

        private static byte[] StateOf(World world, int layout = World.StateVersion)
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

        [Fact]
        public void ACheckpointCarriesTheTotalAndTheResumeIsTheSameWorld()
        {
            RunConfig config = Dark(halfLife: 60f, decay: 0.002f);
            var world = new World(config, seed: 9);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 8, heightY: -5f);
            for (int i = 0; i < 300; i++) world.Step(0.5f);
            Assert.True(world.CorpseSpoiledTotal > 0d);

            byte[] state = StateOf(world);
            Assert.Equal(20, World.StateVersion);
            World restored = Restored(config, 9, state);
            Assert.Equal(World.StateVersion, restored.StateVersionRead);
            Assert.Equal(world.CorpseSpoiledTotal, restored.CorpseSpoiledTotal);

            for (int i = 0; i < 200; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.CorpseSpoiledTotal, restored.CorpseSpoiledTotal);
            Assert.Equal(world.EnergyOut, restored.EnergyOut);
            Assert.Equal(world.Corpses.Select(c => c.Joules), restored.Corpses.Select(c => c.Joules));
            Assert.Equal(world.Corpses.Select(c => c.AgeSeconds), restored.Corpses.Select(c => c.AgeSeconds));
            Assert.Equal(world.StandingMatterUnits, restored.StandingMatterUnits);
        }

        [Fact]
        public void AWorldWithTheDialOffWritesNothingForItAndOneWithItOnWritesEightBytes()
        {
            var off = new World(Dark(halfLife: 0f), seed: 2);
            var on = new World(Dark(halfLife: 100f), seed: 2);

            Assert.Equal(StateOf(off).Length + sizeof(double), StateOf(on).Length);
            Assert.Equal(World.StateVersion, new World(Dark(halfLife: 0f), seed: 2).StateVersionRead);

            // The off world's bytes are a round trip of themselves, and read back to a zero total.
            World back = Restored(Dark(halfLife: 0f), 2, StateOf(off));
            Assert.Equal(0d, back.CorpseSpoiledTotal);
        }
    }
}
