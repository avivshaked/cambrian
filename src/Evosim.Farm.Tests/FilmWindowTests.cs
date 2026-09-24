using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The film window's acceptance on a world small enough to record in a test: a window of a
    /// run on its own build reads faithful against the run's own rows, holds the frames its rate
    /// asks for, and never writes into the run it replays
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B1).
    /// </summary>
    /// <remarks>
    /// <b>One recording for the class.</b> The fixture runs the farm for sixty seconds with a
    /// row every second and a checkpoint every twenty, which takes seconds; every test then films
    /// a window of it into a directory of its own. The recording is made by
    /// <see cref="Program.Main"/> itself, so the window is compared with what the farm's own loop
    /// wrote and not with a second copy of it.
    /// </remarks>
    public class FilmWindowTests : IClassFixture<FilmWindowTests.RecordedRun>
    {
        private static readonly EnvBinding.Lookup NoEnvironment = name => null;

        private readonly RecordedRun _run;

        public FilmWindowTests(RecordedRun run)
        {
            _run = run;
        }

        /// <summary>A sixty second run of the small world, recorded once for the class.</summary>
        public sealed class RecordedRun : IDisposable
        {
            public string Root { get; }
            public string RunDirectory { get; }

            public RecordedRun()
            {
                Root = Path.Combine(AppContext.BaseDirectory, "film-tests", Guid.NewGuid().ToString("N"));
                string runsRoot = Path.Combine(Root, "runs");

                int exit = Program.Main(SmallRun(runsRoot, "filmed", seconds: 60, checkpointEvery: 20));
                if (exit != 0) throw new InvalidOperationException("The recording exited " + exit + ".");

                RunDirectory = Directory.GetDirectories(Path.Combine(runsRoot, "filmed"))[0];
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root)) Directory.Delete(Root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>
        /// The small world of <c>SimulationTests</c>' stop test: a box of 100 m² and 20 m at one
        /// patch, founding from the floor, stepped at 0.02 s, with a row every second.
        /// </summary>
        internal static string[] SmallRun(string runsRoot, string arm, int seconds, int checkpointEvery)
        {
            var settings = new List<string>
            {
                "EVOSIM_RUNS_ROOT=" + runsRoot,
                "EVOSIM_OUT=" + arm + ".md",
                "EVOSIM_SECONDS=" + seconds,
                "EVOSIM_WALL_MINUTES=10",
                "EVOSIM_REPORT_EVERY=2",
                "EVOSIM_AREA=100", "EVOSIM_DEPTH=20", "EVOSIM_FOUNDER_DEPTH=20",
                "EVOSIM_PATCHES=1", "EVOSIM_SHARED_SPACE=1",
                "EVOSIM_FIELD=grid", "EVOSIM_FIELD_CELL=2", "EVOSIM_FIELD_MATTER_CELL=5",
                "EVOSIM_IRRADIANCE=200", "EVOSIM_MATTER_BUDGET=300",
                "EVOSIM_TISSUE_ENERGY=500", "EVOSIM_RHO=100", "EVOSIM_OVERHEAD=100",
                "EVOSIM_DT=0.02", "EVOSIM_THREADS=2",
            };

            if (checkpointEvery > 0) settings.Add("EVOSIM_CHECKPOINT_EVERY=" + checkpointEvery);

            return settings.ToArray();
        }

        /// <summary>
        /// Every body's flags in <c>positions.jsonl</c> (or <c>positions.jsonl.gz</c>), by
        /// second and id.
        /// </summary>
        internal static Dictionary<double, Dictionary<long, int>> PositionFlags(string runDirectory)
        {
            var bySecond = new Dictionary<double, Dictionary<long, int>>();

            foreach (string line in Lines(runDirectory, "positions.jsonl"))
            {
                JsonNode row = Json.Parse(line);
                var flags = new Dictionary<long, int>();

                foreach (JsonNode body in row["b"].Items())
                {
                    flags[(long)body[0].AsDouble()] = body[4].AsInt();
                }

                bySecond[row["t"].AsDouble()] = flags;
            }

            return bySecond;
        }

        /// <summary>A run file's complete lines, plain or gzipped.</summary>
        private static IEnumerable<string> Lines(string runDirectory, string name)
        {
            string plain = Path.Combine(runDirectory, name);
            string gzipped = plain + ".gz";

            string text;

            if (File.Exists(plain))
            {
                text = Encoding.UTF8.GetString(File.ReadAllBytes(plain));
            }
            else
            {
                text = Encoding.UTF8.GetString(Gunzip(gzipped));
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0 && trimmed[trimmed.Length - 1] == '}') yield return trimmed;
            }
        }

        private static byte[] Gunzip(string path)
        {
            using (var file = File.OpenRead(path))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var into = new MemoryStream())
            {
                gzip.CopyTo(into);
                return into.ToArray();
            }
        }

        private string Out(string name) => Path.Combine(_run.Root, "windows", name + "-" + Guid.NewGuid().ToString("N"));

        private FilmWindow.Result Film(double from, double to, string outDirectory, double fps = 10d) =>
            FilmWindow.Film(
                new FilmWindow.Options
                {
                    Run = _run.RunDirectory,
                    FromSeconds = from,
                    ToSeconds = to,
                    Out = outDirectory,
                    FramesPerSecond = fps,
                    Threads = 2,
                },
                NoEnvironment,
                TextWriter.Null);

        private static List<JsonNode> Identity(string outDirectory)
        {
            var rows = new List<JsonNode>();
            foreach (string line in File.ReadAllLines(Path.Combine(outDirectory, FilmWindow.IdentityFileName)))
            {
                if (line.Length > 0) rows.Add(Json.Parse(line));
            }

            return rows;
        }

        // ------------------------------------------------------------------ faithful

        /// <summary>
        /// A window restored from the checkpoint at 20 s and filmed from 25 to 35 s agrees with
        /// the run at every row from the restore to its end, and holds one frame for every tenth
        /// of a second, each at the first physics step at or after its nominal second.
        /// </summary>
        [Fact]
        public void AWindowFromACheckpointIsFaithfulAndHoldsEveryFrame()
        {
            string outDirectory = Out("from-checkpoint");
            FilmWindow.Result result = Film(25d, 35d, outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.EndsWith("000000020.ckpt", result.Checkpoint, StringComparison.Ordinal);
            Assert.Equal(20d, result.RestoredSeconds);

            // Rows every second: 20 to 24 before the window, 25 to 35 inside it.
            Assert.Equal(5, result.RowsBefore);
            Assert.Equal(5, result.RowsBeforeAgreed);
            Assert.Equal(11, result.Rows);
            Assert.Equal(11, result.RowsAgreed);

            Assert.Equal(101, result.Frames);

            RunConfig config = RunDirectory.ReadConfig(_run.RunDirectory, out _);

            using (PoseStreamReader reader = PoseStreamReader.Open(Path.Combine(outDirectory, FilmWindow.PosesFileName)))
            {
                Assert.Equal(2, reader.Header.Version);
                Assert.Equal((float)(1d / 10d), reader.Header.CadenceSeconds);
                Assert.Equal(config.Hash(), reader.Header.ConfigHash);
                Assert.True(reader.IndexRead);
                Assert.Equal(101, reader.Frames.Length);

                for (int k = 250; k <= 350; k++)
                {
                    double nominal = k / 10d;
                    double recorded = reader.Frames[k - 250].Seconds;

                    Assert.True(recorded >= nominal - 1e-9, "frame " + k + " at " + recorded + " is before " + nominal);
                    Assert.True(recorded < nominal + 0.02 - 1e-9, "frame " + k + " at " + recorded + " is a whole physics step after " + nominal);
                }
            }

            List<JsonNode> identity = Identity(outDirectory);
            Assert.Equal(12, identity.Count);

            for (int i = 0; i < 11; i++)
            {
                Assert.Equal("window", identity[i]["phase"].AsString());
                Assert.True(identity[i]["agree"].AsBool());
                Assert.Equal(25d + i, identity[i]["t"].AsDouble());
            }

            Assert.Equal(FilmWindow.Faithful, identity[11]["verdict"].AsString());
        }

        /// <summary>
        /// Every body a frame draws has a genome row, and the rows are complete gzip members that
        /// read back as one stream of genomes this build reads.
        /// </summary>
        [Fact]
        public void EveryFramedBodyHasAGenome()
        {
            string outDirectory = Out("genomes");
            FilmWindow.Result result = Film(41d, 47d, outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);

            var ids = new HashSet<long>();
            string text = Encoding.UTF8.GetString(Gunzip(Path.Combine(outDirectory, FilmWindow.GenomesFileName)));

            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;

                JsonNode row = Json.Parse(line);
                Assert.True(ids.Add((long)row["id"].AsDouble()), "a genome row written twice");
                GenomeJson.Read(line);
            }

            Assert.Equal(result.Bodies, ids.Count);
            Assert.Equal(0, result.BirthsWithoutAGenome);

            int framed = 0;

            using (PoseStreamReader reader = PoseStreamReader.Open(Path.Combine(outDirectory, FilmWindow.PosesFileName)))
            {
                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    foreach (PoseBody body in reader.Read(f).Bodies)
                    {
                        Assert.Contains((long)body.Id, ids);
                        Assert.InRange(body.Flags, 0, PoseStream.AllFlagBits);
                        framed++;
                    }
                }
            }

            Assert.True(framed > 0, "no body was ever framed");
        }

        /// <summary>
        /// The window's events are the run's own lineage rows inside it, byte for byte and in
        /// the run's order.
        /// </summary>
        [Fact]
        public void TheEventsAreTheRunsOwnLineageRows()
        {
            string outDirectory = Out("events");
            Film(21d, 39d, outDirectory);

            var expected = new List<string>();

            foreach (string line in Lines(_run.RunDirectory, "lineage.jsonl"))
            {
                double t = Json.Parse(line)["t"].AsDouble();
                if (t > 21d + 1e-9 && t <= 39d + 1e-9) expected.Add(line);
            }

            var actual = new List<string>();
            foreach (string line in File.ReadAllLines(Path.Combine(outDirectory, FilmWindow.EventsFileName)))
            {
                if (line.Length > 0) actual.Add(line);
            }

            Assert.Equal(expected, actual);
        }

        /// <summary>A window before the first checkpoint is founded from the run's config and seed.</summary>
        [Fact]
        public void AWindowBeforeTheFirstCheckpointIsFounded()
        {
            string outDirectory = Out("founded");
            FilmWindow.Result result = Film(5d, 8d, outDirectory);

            Assert.Null(result.Checkpoint);
            Assert.Equal(0d, result.RestoredSeconds);
            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.Equal(4, result.RowsBefore);
            Assert.Equal(4, result.Rows);
            Assert.Equal(31, result.Frames);
        }

        /// <summary>
        /// A window with no report second inside it steps on to the run's next one and is
        /// compared there.
        /// </summary>
        [Fact]
        public void AWindowWithNoRowInsideIsComparedAtTheNextOne()
        {
            string outDirectory = Out("between-rows");
            FilmWindow.Result result = Film(25.2d, 25.7d, outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.Equal(1, result.Rows);
            Assert.Equal(6, result.Frames);

            List<JsonNode> identity = Identity(outDirectory);
            Assert.Equal("after", identity[0]["phase"].AsString());
            Assert.Equal(26d, identity[0]["t"].AsDouble());
        }

        /// <summary>A window that runs past the run's last row cannot be faithful at its end.</summary>
        [Fact]
        public void AWindowPastTheRunsLastRowIsUnverified()
        {
            string outDirectory = Out("past-the-end");
            FilmWindow.Result result = Film(55d, 65d, outDirectory, fps: 5d);

            Assert.Equal(FilmWindow.Unverified, result.Verdict);
            Assert.Equal(6, result.Rows);
            Assert.Equal(6, result.RowsAgreed);
            Assert.Equal(51, result.Frames);
        }

        // ------------------------------------------------------------------ what it will not do

        /// <summary>Filming a window leaves every file of the run exactly as it was.</summary>
        [Fact]
        public void TheRunDirectoryIsNotWritten()
        {
            Dictionary<string, string> before = Listing(_run.RunDirectory);

            Film(41d, 43d, Out("untouched"));

            Assert.Equal(before, Listing(_run.RunDirectory));
        }

        [Fact]
        public void AWindowInsideTheRunDirectoryIsRefused()
        {
            string inside = Path.Combine(_run.RunDirectory, "film");

            Assert.Throws<ArgumentException>(() => Film(41d, 42d, inside));
            Assert.False(Directory.Exists(inside));
        }

        [Fact]
        public void AWindowWillNotOverwriteAnother()
        {
            string outDirectory = Out("twice");
            Film(41d, 42d, outDirectory);

            Assert.Throws<IOException>(() => Film(41d, 42d, outDirectory));
        }

        [Fact]
        public void ARateAboveThePhysicsRateIsRefused()
        {
            // dt 0.02 s is fifty steps a second.
            Assert.Throws<ArgumentOutOfRangeException>(() => Film(41d, 42d, Out("too-fast"), fps: 60d));
        }

        [Fact]
        public void TheEntryPointFilmsAndRefuses()
        {
            string outDirectory = Out("entry");

            int exit = Program.Main(new[]
            {
                "--film-window", _run.RunDirectory, "30", "31", outDirectory, "--fps", "10", "--threads", "2",
            });

            Assert.Equal(FilmWindow.ExitFaithful, exit);
            Assert.True(File.Exists(Path.Combine(outDirectory, FilmWindow.PosesFileName)));

            Assert.Equal(
                FilmWindow.ExitRefused,
                Program.Main(new[] { "--film-window", _run.RunDirectory, "30", "31", Out("bad"), "--speed", "2" }));
        }

        [Fact]
        public void GzipMembersReadBackAsOneStream()
        {
            string path = Path.Combine(Out("members"), "rows.jsonl.gz");

            using (var members = new GzipMembers(path))
            {
                members.Add("{\"a\":1}");
                members.Flush();
                members.Flush();
                members.Add("{\"a\":2}");
                members.Add("{\"a\":3}");
                members.Flush();
                members.Add("{\"a\":4}");

                Assert.Equal(2, members.Members);
                Assert.Equal(3, members.Rows);
            }

            Assert.Equal("{\"a\":1}\n{\"a\":2}\n{\"a\":3}\n{\"a\":4}\n", Encoding.UTF8.GetString(Gunzip(path)));
            Assert.Throws<IOException>(() => new GzipMembers(path));
        }

        // ------------------------------------------------------------------

        /// <summary>Every file under a directory, with its length and its last write.</summary>
        private static Dictionary<string, string> Listing(string directory)
        {
            var listing = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(path);
                listing[path] = info.Length + " " + info.LastWriteTimeUtc.Ticks;
            }

            return listing;
        }
    }
}
