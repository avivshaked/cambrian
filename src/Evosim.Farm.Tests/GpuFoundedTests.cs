using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A world founded on the gpu engine, on ILGPU's CPU accelerator, with the card flags a round
    /// founds with. Every card sitting before 2026-10-10 was a resume, and a founded world steps
    /// blocks before any body is attached, which a restored one never does (r58t1).
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public class GpuFoundedTests
    {
        private static List<string> SmallTank(string runs, string arm, string speed)
        {
            var settings = new List<string>(FilmWindowTests.SmallRun(runs, arm, seconds: 10, checkpointEvery: 0))
            {
                "EVOSIM_SEED=1",
                "EVOSIM_SHAPE=tank",
                "EVOSIM_CURRENT_MODE=Transport",
                "EVOSIM_CURRENT=" + speed,
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
            };
            return settings;
        }

        /// <summary>
        /// Founded with the scent filled on the accelerator, the world steps and its steering
        /// instrument brings the grid home from the first sample. Before the fix the block sized the
        /// fill's column to one cell until a body was attached, the fill wrote the whole grid into it,
        /// and the first copy home threw.
        /// </summary>
        [Fact]
        public void AFoundedWorldWithTheScentOnTheCardSteps()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-founded", Guid.NewGuid().ToString("N"));
            try
            {
                string runs = Path.Combine(root, "runs");
                Directory.CreateDirectory(runs);
                string args = Path.Combine(root, "a.args");
                File.WriteAllLines(args, SmallTank(runs, "a", "0.05"));
                string second = Path.Combine(root, "b.args");
                File.WriteAllLines(second, SmallTank(runs, "b", "0.05").Select(l => l.Replace("EVOSIM_SEED=1", "EVOSIM_SEED=2")));

                int exit = Program.Main(new[] { "--group", args, second });
                Assert.True(exit == 0, "the group exited " + exit + ": " + File.ReadAllText(Path.ChangeExtension(args, ".err")));

                string run = Directory.GetDirectories(Path.Combine(runs, "a"))[0];
                JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")));
                Assert.Equal("budget", manifest["reason"].AsString());
                Assert.True(File.ReadAllLines(Path.Combine(run, SteeringRecorder.FileName)).Length > 0, "no steering row");
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A group on the gpu engine cut by its wall ends with its ending written and a checkpoint at
        /// the second it stopped. The engine steps a block on a block's first step and only counts
        /// the rest, so the wall is nearly always crossed inside a block; before the fix the loop
        /// left there, the last checkpoint refused a solver ahead of the world, and the world died
        /// with its run.json reading running (gp-old-a, 2026-10-10).
        /// </summary>
        [Fact]
        public void AGroupCutByItsWallEndsAtABlocksEndWithItsCheckpoint()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-founded", Guid.NewGuid().ToString("N"));
            try
            {
                string runs = Path.Combine(root, "runs");
                Directory.CreateDirectory(runs);
                IEnumerable<string> Walled(string arm, int seed) => SmallTank(runs, arm, "0.05")
                    .Where(l => !l.StartsWith("EVOSIM_SECONDS=", StringComparison.Ordinal) &&
                                !l.StartsWith("EVOSIM_WALL_MINUTES=", StringComparison.Ordinal))
                    .Select(l => l.Replace("EVOSIM_SEED=1", "EVOSIM_SEED=" + seed))
                    .Concat(new[] { "EVOSIM_SECONDS=600", "EVOSIM_WALL_MINUTES=0.1", "EVOSIM_CHECKPOINT_EVERY=600" });
                string args = Path.Combine(root, "a.args");
                File.WriteAllLines(args, Walled("a", 1));
                string second = Path.Combine(root, "b.args");
                File.WriteAllLines(second, Walled("b", 2));

                int exit = Program.Main(new[] { "--group", args, second });
                Assert.True(exit == 0, "the group exited " + exit + ": " + File.ReadAllText(Path.ChangeExtension(args, ".err")) +
                    File.ReadAllText(Path.ChangeExtension(second, ".err")));

                foreach (string arm in new[] { "a", "b" })
                {
                    string run = Directory.GetDirectories(Path.Combine(runs, arm))[0];
                    JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(run, "run.json")));
                    Assert.NotEqual("running", manifest["status"].AsString());
                    double stopped = manifest["simulatedSeconds"].AsDouble();
                    Assert.True(stopped < 600d, arm + " ran its whole budget, so the wall cut nothing");
                    Assert.True(Directory.GetFiles(Path.Combine(run, "checkpoints")).Length > 0,
                        arm + " wrote no checkpoint at the second it stopped (" + stopped + " s)");
                }
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A still current builds no streams, and the gpu engine refuses it by name rather than
        /// meeting a NullReferenceException in its constructor.
        /// </summary>
        [Fact]
        public void AStillCurrentIsRefusedByName()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-founded", Guid.NewGuid().ToString("N"));
            try
            {
                string runs = Path.Combine(root, "runs");
                Directory.CreateDirectory(runs);
                string a = Path.Combine(root, "a.args");
                string b = Path.Combine(root, "b.args");
                File.WriteAllLines(a, SmallTank(runs, "a", "0"));
                File.WriteAllLines(b, SmallTank(runs, "b", "0"));

                Assert.Equal(1, Program.Main(new[] { "--group", a, b }));
                string err = File.ReadAllText(Path.ChangeExtension(a, ".err"));
                Assert.Contains("NotSupportedException", err);
                Assert.Contains("moving current", err);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
