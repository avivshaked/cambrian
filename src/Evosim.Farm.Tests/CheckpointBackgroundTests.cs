using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The checkpoint's digest, compression and file on a thread of their own (card-next-r57.md
    /// Part 8): the same bytes as the writer in line, from a payload the loop can step past, and a
    /// farm killed in the middle of a write resumes from the last whole checkpoint.
    /// </summary>
    /// <remarks>
    /// <b>The kill is a real one.</b> The farm runs as its own process with
    /// EVOSIM_TEST_CHECKPOINT_STALL_MS set, which pauses every write halfway through its file, and
    /// the test kills the process while the second checkpoint's file is half on disk. A small world's
    /// checkpoint is otherwise written in microseconds, and a kill timed by watching for the file
    /// would land in the middle of a write only by luck.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public sealed class CheckpointBackgroundTests
    {
        private readonly ITestOutputHelper _out;

        public CheckpointBackgroundTests(ITestOutputHelper output) => _out = output;

        // What each test saw, kept where a reader of the run can find it.
        private void Note(string line)
        {
            _out.WriteLine(line);
            File.AppendAllText(Path.Combine(SimulationTests.Scratch("checkpoint-background"), "counts.txt"),
                DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
        }

        private static string Fresh(string name)
        {
            string path = Path.Combine(SimulationTests.Scratch("checkpoint-background"), name);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
            return path;
        }

        private static CheckpointHeader Header() => new CheckpointHeader
        {
            Version = Checkpoint.Version,
            Seconds = 10.5,
            Seed = 1UL,
            PhysicsSteps = 525,
            PhysicsStepSeconds = 0.02f,
            StepsPerMetabolicStep = 25,
            ConfigHash = "config",
            CoreHash = "core",
            DynamicsHash = "dynamics",
            FarmHash = "farm",
            EngineVersion = "engine",
            SourceArm = "arm",
            SourceRun = "run",
            ReportEvery = 2,
            PoseEverySeconds = 0,
            DigestEverySteps = 0,
            CheckpointEverySeconds = 5,
        };

        private static Action<BinaryWriter> PayloadOf(World world, Simulation sim) => w =>
        {
            StateIo.Tag(w, "PAYL");
            world.WriteState(w);
            sim.WriteState(w);
            StateIo.Tag(w, "PEND");
        };

        // ------------------------------------------------------------------ the hook moves only the time

        [Fact]
        public void AStalledWriteHoldsTheBytesOfOneThatWasNot()
        {
            string dir = Fresh("stall-bytes");
            var world = new World(SimulationTests.SmallWorld(), 1UL);

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                for (int i = 0; i < 525; i++) sim.Step();
                CheckpointPayload payload = CheckpointWriter.Capture(PayloadOf(world, sim));

                string plain = Path.Combine(dir, "plain.ckpt");
                string stalled = Path.Combine(dir, "stalled.ckpt");

                CheckpointWriter.Store(plain, Header(), payload, 0, new long[3]);

                var clock = Stopwatch.StartNew();
                CheckpointWriter.Store(stalled, Header(), payload, 300, new long[3]);
                clock.Stop();

                byte[] a = File.ReadAllBytes(plain), b = File.ReadAllBytes(stalled);
                Note($"stall: {a.Length} bytes plain, {b.Length} stalled, the stalled write took {clock.ElapsedMilliseconds} ms");

                Assert.True(clock.ElapsedMilliseconds >= 300, "the hook did not stall the write");
                Assert.True(a.Length > 1000, "the checkpoint is too small to have held a world");
                Assert.Equal(a, b);
                Assert.False(File.Exists(stalled + ".tmp"));
            }
        }

        // ------------------------------------------------------------------ off the step's path

        [Fact]
        public void AWorldSteppedOnWhileAWriteIsStalledWritesTheCheckpointOfTheSecondItWasTakenAt()
        {
            string dir = Fresh("stepped-on");
            string behind = Path.Combine(dir, "behind.ckpt");
            string inLine = Path.Combine(dir, "in-line.ckpt");

            var worldA = new World(SimulationTests.SmallWorld(), 1UL);
            var worldB = new World(SimulationTests.SmallWorld(), 1UL);

            using (var simA = new Simulation(worldA, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            using (var simB = new Simulation(worldB, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                for (int i = 0; i < 525; i++)
                {
                    simA.Step();
                    simB.Step();
                }

                // A: captured, written behind, and the world stepped on while the write is paused.
                BackgroundCheckpoint write = BackgroundCheckpoint.Start(
                    behind, Header(), CheckpointWriter.Capture(PayloadOf(worldA, simA)), stallMs: 1500);

                int stepped = 0;
                while (!write.Done && stepped < 2000)
                {
                    simA.Step();
                    stepped++;
                }

                bool stillGoing = !write.Done;
                write.Wait();

                // B: the same second, written in line with nothing after it.
                CheckpointWriter.Write(inLine, Header(), PayloadOf(worldB, simB));

                byte[] a = File.ReadAllBytes(behind), b = File.ReadAllBytes(inLine);
                Note($"stepped on: {stepped} physics steps while the write was paused (still going after them: {stillGoing}), " +
                     $"{a.Length} and {b.Length} bytes, world A at {worldA.ElapsedSeconds:0.##} s");

                Assert.True(stepped >= 50, "the world was hardly stepped while the write was paused");
                Assert.True(worldA.ElapsedSeconds > worldB.ElapsedSeconds + 0.5, "world A did not move past the checkpoint's second");
                Assert.Equal(b, a);
            }
        }

        // ------------------------------------------------------------------ the hook's bounds and record

        [Theory]
        [InlineData("0")]
        [InlineData("10001")]
        [InlineData("-5")]
        [InlineData("1.5")]
        [InlineData("abc")]
        public void TheStallHookRefusesAValueOutsideOneToTenThousand(string value)
        {
            var env = EnvBinding.Of(new Dictionary<string, string> { { "EVOSIM_TEST_CHECKPOINT_STALL_MS", value } });
            Assert.Throws<ArgumentException>(() => EnvBinding.Read(env));
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("1", 1)]
        [InlineData("10000", 10000)]
        public void TheStallHookTakesOneToTenThousandAndIsOffUnset(string value, int expected)
        {
            var values = new Dictionary<string, string>();
            if (value != null) values["EVOSIM_TEST_CHECKPOINT_STALL_MS"] = value;
            Assert.Equal(expected, EnvBinding.Read(EnvBinding.Of(values)).TestCheckpointStallMs);
        }

        // ------------------------------------------------------------------ killed in the middle of a write

        private static string RunDirectoryIn(string runsRoot, string arm) =>
            Directory.GetDirectories(Path.Combine(runsRoot, arm)).OrderBy(d => d, StringComparer.Ordinal).Last();

        private static List<string> DigestRowsAfter(string runDirectory, long step) =>
            File.ReadAllLines(Path.Combine(runDirectory, "digest.jsonl"))
                .Where(l => l.Length > 0 && (long)Json.Parse(l)["step"].AsDouble() > step)
                .ToList();

        [Fact]
        public void AFarmKilledInTheMiddleOfAWriteResumesFromTheLastWholeCheckpoint()
        {
            string runsRoot = Fresh("killed");
            const int stall = 4000;

            // The killed run: its own process, every checkpoint's write paused halfway for four seconds.
            string exe = Path.Combine(AppContext.BaseDirectory, "Evosim.Farm.exe");
            Assert.True(File.Exists(exe), "no farm program beside the tests at " + exe);

            var start = new ProcessStartInfo(exe)
            {
                WorkingDirectory = runsRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            foreach (string a in FilmWindowTests.SmallRun(runsRoot, "killed", seconds: 60, checkpointEvery: 5)) start.ArgumentList.Add(a);
            start.ArgumentList.Add("EVOSIM_DIGEST_EVERY=25");
            start.ArgumentList.Add("EVOSIM_TEST_CHECKPOINT_STALL_MS=" + stall);

            string runDirectory = null, whole = null, half = null;
            long halfBytes = 0;

            using (Process farm = Process.Start(start))
            {
                farm.OutputDataReceived += (_, __) => { };
                farm.ErrorDataReceived += (_, __) => { };
                farm.BeginOutputReadLine();
                farm.BeginErrorReadLine();

                var deadline = Stopwatch.StartNew();
                try
                {
                    while (deadline.Elapsed.TotalSeconds < 90 && !farm.HasExited)
                    {
                        if (runDirectory == null && Directory.Exists(Path.Combine(runsRoot, "killed")) &&
                            Directory.GetDirectories(Path.Combine(runsRoot, "killed")).Length > 0)
                        {
                            runDirectory = RunDirectoryIn(runsRoot, "killed");
                        }

                        if (runDirectory != null)
                        {
                            whole = Checkpoint.PathFor(runDirectory, 5);
                            half = Checkpoint.PathFor(runDirectory, 10) + ".tmp";

                            if (File.Exists(whole) && File.Exists(half) && (halfBytes = new FileInfo(half).Length) > 0) break;
                        }

                        System.Threading.Thread.Sleep(20);
                    }
                }
                finally
                {
                    if (!farm.HasExited) farm.Kill(entireProcessTree: true);
                    farm.WaitForExit(30000);
                }

                Assert.True(runDirectory != null, "the killed run made no run directory");
                Assert.True(halfBytes > 0, "the farm was not caught with the second checkpoint half written (it exited: " + farm.HasExited + ")");
            }

            string[] left = Directory.GetFiles(Checkpoint.DirectoryIn(runDirectory)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string manifest = File.ReadAllText(Path.Combine(runDirectory, "run.json"));
            Note($"killed: {string.Join(", ", left)}; the half-written file {halfBytes} bytes");

            // Only whole checkpoints under the checkpoint name, the half-written one under its temporary
            // name, and the last whole one opens with its length, digest and trailer good.
            Assert.Equal(new[] { Path.GetFileName(whole), Path.GetFileName(half) }, left);
            Assert.Equal(whole, Checkpoint.Latest(runDirectory));
            using (CheckpointReader reader = CheckpointReader.Open(whole)) Assert.Equal(5.0, reader.Header.Seconds, 3);
            Assert.Contains("\"testCheckpointStallMs\": " + stall, manifest);

            // The resume: from the run directory, which takes the last whole checkpoint, to 15 s.
            string resumedRoot = Fresh("resumed");
            var resume = new List<string>(FilmWindowTests.SmallRun(resumedRoot, "resumed", seconds: 15, checkpointEvery: 5))
            {
                "EVOSIM_RESUME=" + runDirectory,
                "EVOSIM_DIGEST_EVERY=25",
            };
            Assert.Equal(0, Program.Main(resume.ToArray()));
            string resumed = RunDirectoryIn(resumedRoot, "resumed");
            Assert.DoesNotContain("testCheckpointStallMs", File.ReadAllText(Path.Combine(resumed, "run.json")));

            // And the run nobody killed, from the start to 15 s, in the same settings without the hook.
            string plainRoot = Fresh("plain");
            var plain = new List<string>(FilmWindowTests.SmallRun(plainRoot, "plain", seconds: 15, checkpointEvery: 5))
            {
                "EVOSIM_DIGEST_EVERY=25",
            };
            Assert.Equal(0, Program.Main(plain.ToArray()));
            string uninterrupted = RunDirectoryIn(plainRoot, "plain");

            List<string> fromResume = DigestRowsAfter(resumed, 250);
            List<string> fromPlain = DigestRowsAfter(uninterrupted, 250);
            Note($"resumed: {fromResume.Count} digest rows after step 250, uninterrupted {fromPlain.Count}");

            Assert.True(fromPlain.Count >= 15, "too few digest rows to compare");
            Assert.Equal(fromPlain, fromResume);
        }
    }
}
