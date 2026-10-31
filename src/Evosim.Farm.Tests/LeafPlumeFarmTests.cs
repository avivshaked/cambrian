using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D270 on the farm: the six names reach the config, the header and the field dump say the plume and the current sense are
    /// on only when they are, the channel names are the header's, and a plume-and-current world resumes from a checkpoint as
    /// the unbroken run did.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class LeafPlumeFarmTests
    {
        private readonly ITestOutputHelper _out;

        public LeafPlumeFarmTests(ITestOutputHelper output) => _out = output;

        private const int StepsPerMetabolic = 25;

        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        // ------------------------------------------------------------------ the names

        [Fact]
        public void UnsetIsOffAtTheSpecsDefaultsAndTheHeaderSaysNothing()
        {
            RunConfig config = Built(new Dictionary<string, string>());

            Assert.Equal(LeafPlumeForm.None, config.LeafPlume);
            Assert.Equal(5f, config.LeafPlumeCellMetres);
            Assert.Equal(3000f, config.LeafPlumeLifetimeSeconds);
            Assert.Equal(1f, config.LeafPlumeHalfScale);
            Assert.False(config.CurrentSense);
            Assert.Equal(0.01f, config.CurrentSenseHalfSpeedMetresPerSecond);

            string token = Report.RoundFiftyNineToken(config);
            Assert.DoesNotContain("plume", token);
            Assert.DoesNotContain("current-sense", token);
            Assert.Equal("", token);

            // And the pool is the default pool itself, so no checkpoint byte moves.
            Assert.Same(SensorChannels.DefaultPool, config.SensorPool());
        }

        [Fact]
        public void TheSixNamesReachTheConfigAndTheHeaderSaysSoOnlyWhenTheSwitchIsOn()
        {
            RunConfig config = Built(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "400" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_LEAF_PLUME", "Gradient" },
                { "EVOSIM_LEAF_PLUME_CELL", "10" },
                { "EVOSIM_LEAF_PLUME_LIFE", "1500" },
                { "EVOSIM_LEAF_PLUME_HALF", "0.25" },
                { "EVOSIM_CURRENT_SENSE", "1" },
                { "EVOSIM_CURRENT_SENSE_HALF", "0.02" },
            });

            Assert.Equal(LeafPlumeForm.Gradient, config.LeafPlume);
            Assert.Equal(10f, config.LeafPlumeCellMetres);
            Assert.Equal(1500f, config.LeafPlumeLifetimeSeconds);
            Assert.Equal(0.25f, config.LeafPlumeHalfScale);
            Assert.True(config.CurrentSense);
            Assert.Equal(0.02f, config.CurrentSenseHalfSpeedMetresPerSecond);

            Assert.Equal(
                " · plume gradient cell 10 m life 1500 s half 0.25 m2 s/m3 · current-sense half 0.02 m/s",
                Report.RoundFiftyNineToken(config));

            // Each alone says only itself.
            Assert.Equal(
                " · plume strength cell 5 m life 3000 s half 1 m2 s/m3",
                Report.RoundFiftyNineToken(new RunConfig { LeafPlume = LeafPlumeForm.Strength }));
            Assert.Equal(
                " · current-sense half 0.01 m/s",
                Report.RoundFiftyNineToken(new RunConfig { CurrentSense = true }));

            // The three new numbers appear in the senses token, in the pool's order.
            string senses = Report.SensesToken(config);
            _out.WriteLine(senses);
            Assert.EndsWith(",plume,plumegrad,current", senses);
            Assert.DoesNotContain("plume", Report.SensesToken(Built(new Dictionary<string, string>())));
        }

        [Fact]
        public void AnUnknownFormIsRefusedNamingTheName()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => Built(new Dictionary<string, string> { { "EVOSIM_LEAF_PLUME", "taste" } }));
            Assert.Contains("EVOSIM_LEAF_PLUME", e.Message);
            Assert.Contains("gradient", e.Message);
        }

        [Fact]
        public void ANumberAtOrBelowZeroIsRefused()
        {
            foreach (string name in new[]
                     {
                         "EVOSIM_LEAF_PLUME_CELL", "EVOSIM_LEAF_PLUME_LIFE", "EVOSIM_LEAF_PLUME_HALF", "EVOSIM_CURRENT_SENSE_HALF",
                     })
            {
                Assert.ThrowsAny<ArgumentException>(
                    () => Built(new Dictionary<string, string> { { name, "0" } }));
            }
        }

        // ------------------------------------------------------------------ the dump

        private static RunConfig DumpWorld(bool plume)
        {
            RunConfig config = SimulationTests.SmallWorld();
            if (plume) config.LeafPlume = LeafPlumeForm.Strength;
            return config;
        }

        private static (string layout, string[] plumeFiles, float[] plume, double stock) Dumped(bool plume, string name)
        {
            string scratch = SimulationTests.Scratch(name);
            RunConfig config = DumpWorld(plume);
            var world = new World(config, 1UL);
            RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
            double seconds;
            double stock = 0d;
            int cells = 0;

            using (var sim = new Simulation(world, 1UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: dir.Path))
            using (dir)
            {
                for (int i = 0; i < 6 * StepsPerMetabolic; i++) sim.Step();

                if (plume)
                {
                    stock = world.LeafPlume.TotalStock;
                    cells = world.LeafPlume.CellCount;
                }

                new Sampler().Snapshot(dir, world);
                seconds = world.ElapsedSeconds;
            }

            string fields = dir.FieldsPath;
            string layout = File.ReadAllText(Path.Combine(fields, "layout.json"));
            string[] files = Directory.GetFiles(fields, "*.plume.f32");
            float[] values = null;
            if (plume)
            {
                string path = Path.Combine(fields, ((long)seconds).ToString("000000000") + ".plume.f32");
                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(4 * cells, bytes.Length);
                values = new float[cells];
                Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            }

            return (layout, files, values, stock);
        }

        [Fact]
        public void TheDumpWritesEveryCellOfThePlumeOnlyWithThePlumeOn()
        {
            var on = Dumped(plume: true, name: "plume-dump-on");
            JsonNode node = Json.Parse(on.layout)["plume"];
            Assert.Equal("NNNNNNNNN.plume.f32", node["file"].AsString());
            Assert.Equal("(iy * cellsX + ix) * cellsZ + iz", node["order"].AsString());
            Assert.Equal(5.0, node["cellMetres"].AsDouble());
            Assert.Single(on.plumeFiles);
            Assert.True(on.plume.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f), "an unsound cell in the dump");

            double sum = 0d;
            foreach (float v in on.plume) sum += v;
            _out.WriteLine($"the dump sums to {sum:R} against the world's {on.stock:R}");
            Assert.Equal(on.stock, sum, 6);

            var off = Dumped(plume: false, name: "plume-dump-off");
            Assert.False(Json.Parse(off.layout).Has("plume"));
            Assert.Empty(off.plumeFiles);
        }

        // ------------------------------------------------------------------ the resume

        private static RunConfig PlumeAndCurrentWorld()
        {
            RunConfig config = Built(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "25" },
                { "EVOSIM_DEPTH", "5" },
                { "EVOSIM_FOUNDER_DEPTH", "5" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "1" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_LEAF_PLUME", "gradient" },
                { "EVOSIM_LEAF_PLUME_CELL", "5" },
                { "EVOSIM_CURRENT_SENSE", "1" },
                { "EVOSIM_BRAIN_MODEL", "central" },
                { "EVOSIM_NEURON_COST", "0" },
                { "EVOSIM_CONNECTION_COST", "0" },
            });

            // Links twice as often as the rest, so that some founders carry a joint and a leaf: a body without a joint
            // has no brain output, so nothing reads its senses.
            config.Genome.FounderCellTypes = new[]
            {
                CellTypeIds.Link, CellTypeIds.Link, CellTypeIds.Photosynthetic,
            };

            return config;
        }

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found()
        {
            var world = new World(PlumeAndCurrentWorld(), 3UL);
            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 3UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: null),
            };
        }

        private static byte[] Checkpoint(Pair pair)
        {
            pair.Sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    pair.World.WriteState(w);
                    pair.Sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static Pair Restore(byte[] state)
        {
            Pair pair = Found();

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        private static int Readers(Simulation sim)
        {
            int readers = 0;
            foreach (Creature body in sim.Dynamics.Creatures)
            {
                int mask = body.Brain.SensorMask;
                if (Brain.MaskReads(mask, SensorChannel.LeafPlume) || Brain.MaskReads(mask, SensorChannel.LeafPlumeGradient)
                    || Brain.MaskReads(mask, SensorChannel.GroundCurrent)) readers++;
            }

            return readers;
        }

        [Fact]
        public void APlumeAndCurrentWorldResumesFromACheckpointAsItRanOn()
        {
            using (Pair live = Found())
            {
                // Until some body's brain reads a new channel and the plume holds something, or fail saying so.
                int m = 0;
                while (m < 120 && !(Readers(live.Sim) > 0 && live.World.LeafPlume.TotalStock > 0d))
                {
                    for (int i = 0; i < StepsPerMetabolic; i++) live.Sim.Step();
                    m++;
                }

                _out.WriteLine($"{m} metabolic steps, {live.World.Living.Count} living, {Readers(live.Sim)} bodies read a new channel, " +
                               $"plume holds {live.World.LeafPlume.TotalStock:R}");
                Assert.True(Readers(live.Sim) > 0, "no body read the plume or the current, so the resume saw nothing");
                Assert.True(live.World.LeafPlume.TotalStock > 0d, "the plume holds nothing, so the resume saw nothing");

                byte[] state = Checkpoint(live);

                using (Pair resumed = Restore(state))
                {
                    Assert.Equal(live.World.LeafPlume.TotalStock, resumed.World.LeafPlume.TotalStock);
                    Assert.Equal(live.World.LeafPlumeEmittedTotal, resumed.World.LeafPlumeEmittedTotal);

                    // Four metabolic steps, so four holds of the far readings, each the run's own.
                    for (int i = 0; i < 4 * StepsPerMetabolic; i++)
                    {
                        live.Sim.Step();
                        resumed.Sim.Step();
                        Assert.Equal(live.Sim.Dynamics.Digest(), resumed.Sim.Dynamics.Digest());
                    }

                    Assert.Equal(live.World.LeafPlume.TotalStock, resumed.World.LeafPlume.TotalStock);
                    Assert.Equal(Checkpoint(live), Checkpoint(resumed));
                }
            }
        }
    }
}
