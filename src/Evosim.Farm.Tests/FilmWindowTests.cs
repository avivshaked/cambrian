using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// A run file's complete lines: plain JSONL, or record format 2's gzip members through
        /// Core's one reader.
        /// </summary>
        private static IEnumerable<string> Lines(string runDirectory, string name)
        {
            string plain = Path.Combine(runDirectory, name);

            IEnumerable<string> lines = File.Exists(plain)
                ? Encoding.UTF8.GetString(File.ReadAllBytes(plain)).Split('\n')
                : GzipMemberReader.ReadLines(plain + ".gz", out _);

            foreach (string line in lines)
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0 && trimmed[trimmed.Length - 1] == '}') yield return trimmed;
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
        /// Every body a frame draws, and every body born inside the window, has a genome row. The
        /// rows are Core's gzip members, read by the reader that reads a run's own
        /// <c>genomes.jsonl.gz</c>, and each is the row the run's file holds for the same id.
        /// </summary>
        [Fact]
        public void EveryFramedBodyHasAGenome()
        {
            string outDirectory = Out("genomes");
            FilmWindow.Result result = Film(41d, 47d, outDirectory);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);

            var ids = new HashSet<long>();
            var rows = new Dictionary<long, string>();

            using (GzipMemberReader reader = GzipMemberReader.Open(Path.Combine(outDirectory, FilmWindow.GenomesFileName)))
            {
                foreach (string line in reader.Lines())
                {
                    long id = GenomeJson.ReadId(line);
                    Assert.True(ids.Add(id), "a genome row written twice");
                    rows[id] = line;
                    GenomeJson.Read(line);
                }

                Assert.False(reader.Torn, reader.TornNote);
                Assert.True(reader.CompleteMembers > 0, "no member was written");
            }

            Assert.Equal(result.Bodies, ids.Count);
            Assert.Equal(0, result.BirthsWithoutAGenome);

            // Every birth the window's events hold, whether or not the body lived to a frame.
            foreach (string line in File.ReadAllLines(Path.Combine(outDirectory, FilmWindow.EventsFileName)))
            {
                if (line.Length == 0) continue;

                JsonNode e = Json.Parse(line);
                if (e["e"].AsString() == "b") Assert.Contains((long)e["id"].AsDouble(), ids);
            }

            // The run records in format 2 by default, so its genomes file holds the same row for
            // every id: one writer, one reader.
            string runGenomes = Path.Combine(_run.RunDirectory, RecordFiles.GenomesName);
            Assert.True(File.Exists(runGenomes), "the recording wrote no genomes.jsonl.gz");

            var runRows = new Dictionary<long, string>();
            foreach (string line in GzipMemberReader.ReadLines(runGenomes, out _))
            {
                runRows[GenomeJson.ReadId(line)] = line;
            }

            foreach (KeyValuePair<long, string> row in rows)
            {
                Assert.True(runRows.TryGetValue(row.Key, out string runRow), "body " + row.Key + " is not in the run's genomes");
                Assert.Equal(runRow, row.Value);
            }

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

        // ------------------------------------------------------------------ the faithful rule

        private static CheckpointHeader Hashes(string config, string core, string dynamics, string farm) =>
            new CheckpointHeader { ConfigHash = config, CoreHash = core, DynamicsHash = dynamics, FarmHash = farm };

        /// <summary>
        /// The farm's hash is named and never decides; the config's, Core's and Dynamics' decide.
        /// </summary>
        [Fact]
        public void OnlyTheConfigCoreAndDynamicsDecideTheSource()
        {
            FilmWindow.SourceComparison same = FilmWindow.CompareSources(
                Hashes("c", "core", "dyn", "farm"), "c", "core", "dyn", "farm");
            Assert.Empty(same.Deciding);
            Assert.Null(same.Farm);

            FilmWindow.SourceComparison farm = FilmWindow.CompareSources(
                Hashes("c", "core", "dyn", "farm-old"), "c", "core", "dyn", "farm-new");
            Assert.Empty(farm.Deciding);
            Assert.StartsWith("farmHash:", farm.Farm, StringComparison.Ordinal);

            FilmWindow.SourceComparison core = FilmWindow.CompareSources(
                Hashes("c", "core-old", "dyn-old", "farm-old"), "c", "core-new", "dyn-new", "farm-new");
            Assert.Equal(2, core.Deciding.Count);
            Assert.StartsWith("coreHash:", core.Deciding[0], StringComparison.Ordinal);
            Assert.StartsWith("dynamicsHash:", core.Deciding[1], StringComparison.Ordinal);
            Assert.NotNull(core.Farm);

            FilmWindow.SourceComparison config = FilmWindow.CompareSources(
                Hashes("c-old", "core", "dyn", "farm"), "c-new", "core", "dyn", "farm");
            Assert.Single(config.Deciding);
        }

        private static FilmWindow.Result AgreeingResult() =>
            new FilmWindow.Result
            {
                RunDirectory = "run",
                Rows = 3,
                RowsAgreed = 3,
                LastRunRowSeconds = 100d,
                LastSecond = 50d,
            };

        private static readonly FilmWindow.Options Window =
            new FilmWindow.Options { Run = "run", Out = "out", FromSeconds = 10d, ToSeconds = 40d };

        /// <summary>
        /// A window whose rows all agree is faithful under a differing farm hash, which its
        /// verdict names, and exits 0.
        /// </summary>
        [Fact]
        public void AFarmHashAloneLeavesAFaithfulWindowFaithfulAndNamed()
        {
            FilmWindow.Result result = AgreeingResult();
            result.FarmNote = "farmHash: checkpoint 8f04d32857fa…, this build 1234567890ab…";

            FilmWindow.Decide(result, Window);

            Assert.Equal(FilmWindow.Faithful, result.Verdict);
            Assert.Contains("farm's source differs", result.Reason, StringComparison.Ordinal);
            Assert.Contains("8f04d32857fa", result.Reason, StringComparison.Ordinal);
            Assert.Equal(FilmWindow.ExitFaithful, FilmWindow.ExitCodeOf(result.Verdict));
        }

        /// <summary>A differing Core or Dynamics, filmed anyway, is a cousin whatever its rows say.</summary>
        [Fact]
        public void ASourceMismatchIsACousinWhateverItsRowsSay()
        {
            FilmWindow.Result result = AgreeingResult();
            result.SourceMismatch = true;
            result.SourceNote = "coreHash: checkpoint aaaa, this build bbbb";

            FilmWindow.Decide(result, Window);

            Assert.Equal(FilmWindow.Cousin, result.Verdict);
            Assert.Contains("coreHash", result.Reason, StringComparison.Ordinal);
            Assert.Equal(FilmWindow.ExitCousin, FilmWindow.ExitCodeOf(result.Verdict));
        }

        /// <summary>A parted row is a cousin that names the second and the field, farm hash or none.</summary>
        [Fact]
        public void APartedRowIsACousinWithTheSecondAndTheField()
        {
            FilmWindow.Result result = AgreeingResult();
            result.RowsAgreed = 2;
            result.PartedAtSeconds = 30d;
            result.PartedField = "auditResidual";
            result.FarmNote = "farmHash: checkpoint aaaa, this build bbbb";

            FilmWindow.Decide(result, Window);

            Assert.Equal(FilmWindow.Cousin, result.Verdict);
            Assert.Contains("t=30 s in auditResidual", result.Reason, StringComparison.Ordinal);
            Assert.Contains("farm's source differs", result.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnverifiedWindowExitsThree()
        {
            Assert.Equal(3, FilmWindow.ExitCodeOf(FilmWindow.Unverified));
            Assert.Equal(2, FilmWindow.ExitCodeOf(FilmWindow.Cousin));
            Assert.Equal(0, FilmWindow.ExitCodeOf(FilmWindow.Faithful));
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
