using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D176's leaf smell recorder (<see cref="LeafScentRecorder"/>): a world with the leaf smell
    /// writes its rows with the fields the reader takes, on the CPU engine and on the gpu engine on
    /// ILGPU's CPU accelerator, and a world recorded at every metabolic step ends in the same state,
    /// byte for byte, as the same world never recorded.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public sealed class LeafScentRecorderTests
    {
        private readonly ITestOutputHelper _out;

        public LeafScentRecorderTests(ITestOutputHelper output) => _out = output;

        private static RunConfig LeafWorld()
        {
            RunConfig config = SimulationTests.SmallWorld();
            config.LeafScent = LeafScentForm.Direction;
            config.LeafScentRangeMetres = 6f;
            return config;
        }

        private static string StateHash(World world, Simulation sim)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    sim.WriteState(w);
                }

                using (SHA256 sha = SHA256.Create())
                {
                    return Convert.ToHexString(sha.ComputeHash(buffer.ToArray()));
                }
            }
        }

        private static void AssertRowShape(JsonNode row)
        {
            Assert.True(row["t"].AsDouble() >= 0);
            foreach (string key in new[] { "read", "ctl", "gread", "gctl" })
            {
                List<JsonNode> triple = row[key].Items().ToList();
                Assert.Equal(3, triple.Count);
            }

            List<JsonNode> skip = row["skip"].Items().ToList();
            Assert.Equal(3, skip.Count);

            int counted = row["read"].Items().First().AsInt() + row["ctl"].Items().First().AsInt();
            List<JsonNode> bodies = row["b"].Items().ToList();
            Assert.Equal(counted, bodies.Count);

            foreach (JsonNode b in bodies)
            {
                List<JsonNode> f = b.Items().ToList();
                Assert.Equal(8, f.Count);
                Assert.InRange(f[1].AsInt(), -1000, 1000);
                Assert.InRange(f[2].AsInt(), 0, 1);
                Assert.InRange(f[3].AsInt(), 0, 1);
                Assert.True(f[4].AsInt() >= 1, "a counted body moves at 1 mm/s or more");
                Assert.InRange(f[5].AsInt(), 0, 1000);
                Assert.True(f[6].AsDouble() >= 0);
                Assert.True(f[7].AsDouble() > 0, "a counted body stands in a gradient");
            }
        }

        /// <summary>
        /// The same world twice, one recorded at every metabolic step rather than every 100 s: the
        /// two end in the same bytes of world and harness state, so the recorder moves nothing. The
        /// rows it wrote have the reader's fields, and some body was counted.
        /// </summary>
        [Fact]
        public void RecordingTheLeafSmellMovesNoStateAndWritesItsFields()
        {
            string dir = Path.Combine(SimulationTests.Scratch("leaf-scent-recorder"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var worldA = new World(LeafWorld(), 1UL);
            var worldB = new World(LeafWorld(), 1UL);
            Assert.NotNull(worldA.LeafScent);

            int counted = 0;
            using (var simA = new Simulation(worldA, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            using (var simB = new Simulation(worldB, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            using (var recorder = new LeafScentRecorder(dir, 100.0))
            {
                for (int i = 0; i < 2500; i++)
                {
                    if (simA.Step()) recorder.WriteRow(simA, worldA.ElapsedSeconds);
                    simB.Step();
                }

                Assert.True(recorder.Rows >= 90, "rows: " + recorder.Rows);
                Assert.Equal(worldB.ElapsedSeconds, worldA.ElapsedSeconds);
                Assert.Equal(StateHash(worldB, simB), StateHash(worldA, simA));
            }

            string[] lines = JsonlWriter.ReadRows(Path.Combine(dir, LeafScentRecorder.FileName));
            Assert.True(lines.Length >= 90);
            foreach (string line in lines)
            {
                JsonNode row = Json.Parse(line);
                AssertRowShape(row);
                counted += row["b"].Items().Count();
            }

            _out.WriteLine("rows " + lines.Length + ", bodies counted " + counted + "; last row: " + lines[lines.Length - 1]);
            Assert.True(counted > 0, "no body was ever counted, so the cosine was never taken");
        }

        private static List<string> LeafRun(string runs, string arm, bool leaf, bool gpu)
        {
            var settings = new List<string>(FilmWindowTests.SmallRun(runs, arm, seconds: 101, checkpointEvery: 0))
            {
                "EVOSIM_SEED=1",
            };

            if (leaf) settings.AddRange(new[] { "EVOSIM_LEAF_SCENT=direction", "EVOSIM_LEAF_SCENT_RANGE=6" });

            if (gpu)
            {
                settings.AddRange(new[]
                {
                    "EVOSIM_SHAPE=tank",
                    "EVOSIM_CURRENT_MODE=Transport",
                    "EVOSIM_CURRENT=0.05",
                    "EVOSIM_WATER_HOLD=0",
                    "EVOSIM_SENSE_SCENT=1",
                    "EVOSIM_CORPSE_DECAY=0.000385",
                    "EVOSIM_ENGINE=gpu",
                    "EVOSIM_GPU_DEVICE=cpu",
                    "EVOSIM_GPU_PRECISION=single",
                    "EVOSIM_GPU_GROUP=32",
                    "EVOSIM_GPU_SCENT=1",
                    "EVOSIM_GPU_TRANSPORT=1",
                    "EVOSIM_GPU_CONCURRENT=1",
                });
            }

            return settings;
        }

        // A founded world has no body at its first metabolic step, so the 101 s run's two rows are
        // the first step's (empty) and the one at 100 s, where bodies are counted.
        private static void AssertTwoRowsAndSomeBodies(string[] rows)
        {
            Assert.Equal(2, rows.Length);
            JsonNode first = Json.Parse(rows[0]), second = Json.Parse(rows[1]);
            AssertRowShape(first);
            AssertRowShape(second);
            Assert.Equal(0.5, first["t"].AsDouble(), 6);
            Assert.InRange(second["t"].AsDouble(), 100.0, 100.5);
            Assert.True(second["b"].Items().Any(), "no body counted at 100 s: " + rows[1]);
        }

        private static string OneRun(string runs, string arm) => Directory.GetDirectories(Path.Combine(runs, arm))[0];

        /// <summary>
        /// The farm's loop opens the recorder in a world with the leaf smell and in no other: the
        /// first metabolic step writes a row and the step at 100 s another, and a world without the
        /// smell has no file.
        /// </summary>
        [Fact]
        public void TheFarmWritesLeafScentRowsOnlyWhereTheWorldHasTheSmell()
        {
            string runs = Path.Combine(SimulationTests.Scratch("leaf-scent-recorder"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runs);

            Assert.Equal(0, Program.Main(LeafRun(runs, "leaf", leaf: true, gpu: false).ToArray()));
            Assert.Equal(0, Program.Main(LeafRun(runs, "plain", leaf: false, gpu: false).ToArray()));

            string[] rows = JsonlWriter.ReadRows(Path.Combine(OneRun(runs, "leaf"), LeafScentRecorder.FileName));
            AssertTwoRowsAndSomeBodies(rows);
            _out.WriteLine("cpu row: " + rows[1]);

            Assert.False(File.Exists(Path.Combine(OneRun(runs, "plain"), LeafScentRecorder.FileName)));
        }

        /// <summary>
        /// The same on the gpu engine, on ILGPU's CPU accelerator: the leaf field is filled on the
        /// host and the block unpacks the bodies before the metabolic step, so the recorder reads
        /// what the CPU engine's does, through the same loop.
        /// </summary>
        [Fact]
        public void TheGpuEngineOnTheAcceleratorWritesLeafScentRows()
        {
            string runs = Path.Combine(SimulationTests.Scratch("leaf-scent-recorder"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runs);

            Assert.Equal(0, Program.Main(LeafRun(runs, "card", leaf: true, gpu: true).ToArray()));

            string run = OneRun(runs, "card");
            JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")));
            Assert.Equal("budget", manifest["reason"].AsString());

            string[] rows = JsonlWriter.ReadRows(Path.Combine(run, LeafScentRecorder.FileName));
            AssertTwoRowsAndSomeBodies(rows);
            _out.WriteLine("accelerator row: " + rows[1]);
        }
    }
}
