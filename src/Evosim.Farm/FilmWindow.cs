using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// A film window: a stretch of a recorded run, replayed by the farm from the checkpoint at or
    /// before it and written down at a frame rate for the theatre to play back
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The farm moves the world and Unity draws it.</b> The Editor cannot re-simulate a farm
    /// run, because Mono and .NET round a double sum differently and the world on screen parts
    /// from the recording at its first sample. The farm replays its own run bit for bit from a
    /// checkpoint. So a scene's stretch is stepped here and every body's pose is written thirty
    /// times a second, and the theatre draws those poses without stepping anything.
    /// </para>
    /// <para>
    /// <b>It steps as the run stepped.</b> The world's rules come from the run's own
    /// <c>config.json</c>, and the world, the harness, the sampler and the loop's four numbers
    /// from the checkpoint, through the reader a resume uses (<see cref="Program.ReadCheckpoint"/>).
    /// Each metabolic step is followed by the loop's own work on the world
    /// (<see cref="Program.ActOnWorld"/>) and by <see cref="Program.EndsExtinct"/>. At the loop's
    /// report steps the sampler runs as it did in the run, with nowhere to write, so the
    /// instrument queues are drained on the steps the run drained them. Nothing the loop does
    /// beyond that writes to the world: its report, snapshots, checkpoints and manifest are
    /// files, and a checkpoint's settling of the harness is measured to move nothing
    /// (<c>logbook/specs/checkpoint-spec.md</c>). A run with no checkpoint at or before the
    /// window is founded from its config and seed, which is how the run itself began.
    /// </para>
    /// <para>
    /// <b>It samples below the metabolic step.</b> The run's own stream is raised to the half
    /// second because it hangs off the metabolic loop (<see cref="EnvSettings.ResolvePoseEvery"/>),
    /// and that bound is left where it is. A window asks after every physics step instead. Its
    /// clock is the world's second at the last metabolic step plus the physics steps since, and a
    /// frame is written at the first physics step at or after each <c>k / fps</c> second. So a
    /// frame's recorded second sits up to one physics step after its nominal one, never before
    /// it, and the stream's header carries the nominal interval, one over the frame rate. A rate
    /// above the physics rate is refused, because two nominal seconds would share one step.
    /// </para>
    /// <para>
    /// <b>What it writes, all into the out directory and never into the run's.</b>
    /// <c>film.poses.bin</c>, a version 2 state stream. <c>genomes.jsonl.gz</c>, one genome row
    /// per body alive at the window's start or born inside it, in gzip members written at each
    /// report second, so a killed window keeps every member before the last. <c>plans.jsonl</c>,
    /// each body's module counts and lost parts at the start where they differ from the genome's,
    /// and every change after. <c>events.jsonl</c>, the run's own lineage rows for every birth,
    /// death and bite inside the window. <c>identity.jsonl</c>, the window's count of the living,
    /// its births, its deaths, its audit residual and its mean height against the run's own
    /// <c>stats.jsonl</c> row at every report second inside the window, and a verdict line last.
    /// </para>
    /// <para>
    /// <b>Faithful is measured, not assumed.</b> A window whose rows all agree with the run's to
    /// the bit is <c>faithful</c>. One that parts, before the window or inside it, is a
    /// <c>cousin</c>, and the verdict says the second and the field. A checkpoint this build did
    /// not write is refused as a resume refuses it, unless <c>EVOSIM_ALLOW_SOURCE_MISMATCH</c> is
    /// set, and the window is then a cousin whatever its rows say. A window with no report second
    /// inside it steps on to the run's next one and compares that. A window that runs past the
    /// run's last row, or finds no row at all, is <c>unverified</c>.
    /// </para>
    /// <para>
    /// <b>Exit codes.</b> 0 for a faithful window, 2 for one written and not faithful, 1 for a
    /// refusal or a failure, which writes nothing or stops part way.
    /// </para>
    /// </remarks>
    public static class FilmWindow
    {
        public const string PosesFileName = "film.poses.bin";
        public const string GenomesFileName = "genomes.jsonl.gz";
        public const string PlansFileName = "plans.jsonl";
        public const string EventsFileName = "events.jsonl";
        public const string IdentityFileName = "identity.jsonl";

        public const double DefaultFramesPerSecond = 30d;

        public const int ExitFaithful = 0;
        public const int ExitRefused = 1;
        public const int ExitNotFaithful = 2;

        public const string Faithful = "faithful";
        public const string Cousin = "cousin";
        public const string Unverified = "unverified";

        private const double Eps = 1e-9;

        private const string Usage =
            "Usage: Evosim.Farm --film-window <run dir> <from s> <to s> <out dir> [--fps N] [--threads N]";

        /// <summary>What a window was asked for.</summary>
        public sealed class Options
        {
            /// <summary>A run directory, or an arm directory, which resolves to its newest run.</summary>
            public string Run;

            public double FromSeconds;
            public double ToSeconds;

            /// <summary>Where the five files go. Refused inside the run directory.</summary>
            public string Out;

            public double FramesPerSecond = DefaultFramesPerSecond;

            /// <summary>0 for <c>EVOSIM_THREADS</c>, or half the machine's logical processors.</summary>
            public int Threads;
        }

        /// <summary>What a window wrote and what it found.</summary>
        public sealed class Result
        {
            public string RunDirectory;
            public string OutDirectory;

            /// <summary>The checkpoint restored, or null when the run was founded.</summary>
            public string Checkpoint;

            public double RestoredSeconds;
            public int Threads;

            public bool SourceMismatch;
            public string SourceNote;

            public int Frames;

            /// <summary>Genome rows written: every body alive at the start or born inside.</summary>
            public int Bodies;

            public int Births;
            public int Deaths;
            public int Bites;
            public int BirthsWithoutAGenome;
            public int PlanRows;

            /// <summary>Report seconds compared inside the window, and the one after it when used.</summary>
            public int Rows;

            public int RowsAgreed;
            public int RowsBefore;
            public int RowsBeforeAgreed;

            public double PartedAtSeconds = double.NaN;
            public string PartedField;

            /// <summary>The run's last row, or NaN when its <c>stats.jsonl</c> holds none after the restore.</summary>
            public double LastRunRowSeconds = double.NaN;

            public bool EndedExtinct;
            public double LastSecond;

            public string Verdict;
            public string Reason;

            public double WallSeconds;
        }

        /// <summary>The <c>--film-window</c> entry: parses, films, prints one line, and exits.</summary>
        public static int Run(string[] args)
        {
            Options options;

            try
            {
                options = Parse(args);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            {
                Console.Error.WriteLine(e.Message);
                return ExitRefused;
            }

            Result result = Film(options, Environment.GetEnvironmentVariable, Console.Error);

            Console.WriteLine(Summary(result, options));

            return result.Verdict == Faithful ? ExitFaithful : ExitNotFaithful;
        }

        /// <summary>
        /// <c>--film-window &lt;run dir&gt; &lt;from s&gt; &lt;to s&gt; &lt;out dir&gt; [--fps N] [--threads N]</c>.
        /// </summary>
        public static Options Parse(string[] args)
        {
            if (args == null || args.Length < 5 || args[0] != "--film-window")
            {
                throw new ArgumentException(Usage);
            }

            var options = new Options
            {
                Run = args[1],
                FromSeconds = Number(args[2], "<from s>"),
                ToSeconds = Number(args[3], "<to s>"),
                Out = args[4],
            };

            for (int i = 5; i < args.Length; i++)
            {
                string name = args[i];

                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException(name + " wants a value. " + Usage);
                }

                string value = args[++i];

                switch (name)
                {
                    case "--fps":
                        options.FramesPerSecond = Number(value, "--fps");
                        break;

                    case "--threads":
                        options.Threads = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        break;

                    default:
                        throw new ArgumentException(
                            "'" + name + "' is not an option of --film-window, and is refused rather " +
                            "than ignored. " + Usage);
                }
            }

            return options;
        }

        private static double Number(string text, string what)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new FormatException(what + " is '" + text + "', which is not a number. " + Usage);
            }

            return value;
        }

        /// <summary>
        /// Films a window. Throws on a refusal, before anything is written wherever it can.
        /// </summary>
        /// <param name="options">What was asked for.</param>
        /// <param name="env">The environment: four names are read and every other ignored.</param>
        /// <param name="log">Where the progress lines go; stdout carries the summary alone.</param>
        public static Result Film(Options options, EnvBinding.Lookup env, TextWriter log)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (env == null) throw new ArgumentNullException(nameof(env));
            if (log == null) log = TextWriter.Null;

            Stopwatch wall = Stopwatch.StartNew();

            Validate(options);

            string runDirectory = ResolveRunDirectory(options.Run, log);
            string outDirectory = Path.GetFullPath(options.Out);

            RefuseTheRunDirectory(runDirectory, outDirectory);
            RefuseEarlierOutput(outDirectory);

            // Four names and no others, so that a shell still carrying a launcher's world cannot
            // reach the window: the world is the run's config and nothing else.
            EnvSettings settings = EnvBinding.Read(name => ReadsSetting(name) ? env(name) : null);

            RunConfig config = RunDirectory.ReadConfig(runDirectory, out string edited);

            if (edited != null)
            {
                log.WriteLine(
                    "film-window: warning: " + Path.Combine(runDirectory, "config.json") + " " + edited);
            }

            TricklePoolFiles.Pool pool = TricklePoolFiles.Load(runDirectory, config);
            string manifestText = File.ReadAllText(Path.Combine(runDirectory, "run.json"));
            JsonNode manifest = Json.Parse(manifestText);

            string checkpoint = Checkpoint.At(runDirectory, options.FromSeconds);

            CheckpointHeader header = checkpoint != null
                ? CheckpointReader.ReadHeader(checkpoint)
                : FoundingHeader(runDirectory, config, manifest, manifestText, options.FromSeconds);

            float physicsDt = EnvBinding.ResolvePhysicsStep(header.PhysicsStepSeconds, out int stepsPerMetabolic);

            if (1d / options.FramesPerSecond < physicsDt - 1e-12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.FramesPerSecond,
                    "A frame is taken at a physics step, and this run's step is " +
                    physicsDt.ToString("R", CultureInfo.InvariantCulture) + " s, so the rate can be " +
                    "at most " + (1d / physicsDt).ToString("0.##", CultureInfo.InvariantCulture) +
                    " frames a second. Above it two nominal seconds would share one step.");
            }

            int threads = options.Threads > 0
                ? options.Threads
                : settings.Threads > 0 ? settings.Threads : Math.Max(1, Environment.ProcessorCount / 2);

            var result = new Result
            {
                RunDirectory = runDirectory,
                OutDirectory = outDirectory,
                Checkpoint = checkpoint,
                RestoredSeconds = header.Seconds,
                Threads = threads,
            };

            // A resume's own refusal: all four hashes, and the refusal is the default.
            RunManifest build = Manifest.Build(
                settings, config.Hash(), null, physicsDt, stepsPerMetabolic, threads);

            IReadOnlyList<string> differences = header.Differences(
                config.Hash(), build.CoreHash, build.DynamicsHash, build.FarmHash);

            if (differences.Count > 0)
            {
                string note = string.Join("; ", differences);

                if (!settings.AllowSourceMismatch)
                {
                    throw new InvalidOperationException(
                        "This " + (checkpoint != null ? "checkpoint" : "run") + " was not written " +
                        "by this build, so a window of it would step a trajectory the recording " +
                        "never had: " + note + ". Set EVOSIM_ALLOW_SOURCE_MISMATCH=1 to film it " +
                        "anyway; the window is then marked a cousin.");
                }

                result.SourceMismatch = true;
                result.SourceNote = note;

                log.WriteLine(
                    "film-window: warning: filming across a source mismatch, so this window is a " +
                    "cousin of the recording: " + note);
            }

            // The run's rows, from the restore on, keyed by their second.
            SortedList<double, RunRow> runRows = ReadRunRows(runDirectory, header.Seconds - Eps);
            if (runRows.Count > 0) result.LastRunRowSeconds = runRows.Keys[runRows.Count - 1];

            float metabolicSeconds = stepsPerMetabolic * physicsDt; // Metabolise's own float
            int reportEvery = header.ReportEvery > 0
                ? header.ReportEvery
                : InferReportEvery(runRows, metabolicSeconds);

            Parallelism.Threads = threads;

            var world = new World(config, header.Seed, pool?.Genomes);
            var sim = new Simulation(
                world, header.Seed, physicsDt, stepsPerMetabolic, threads, runDirectory: null);
            var sampler = new Sampler { PoolNamed = config.FoundingTricklePoolCount > 0 };
            IReadOnlyList<string> columns = Sampler.Columns(config);

            Program.LoopState loop = checkpoint != null
                ? Program.ReadCheckpoint(checkpoint, world, sim, sampler)
                : new Program.LoopState();

            if (Math.Abs(world.ElapsedSeconds - header.Seconds) > 1e-6)
            {
                throw new InvalidDataException(
                    "The checkpoint's header says " + header.Seconds + " s and its world says " +
                    world.ElapsedSeconds + " s.");
            }

            Genome inoculum = Inoculum(config, manifest, settings, loop.AssayFired, options.ToSeconds, reportEvery * (double)metabolicSeconds, log);

            log.WriteLine(
                "film-window: " + (checkpoint != null
                    ? "restored " + checkpoint + " at " + Seconds(header.Seconds) + " s"
                    : "founded " + runDirectory + " from its config and seed " + header.Seed) +
                "; stepping to " + Seconds(options.FromSeconds) + " s writing nothing, then filming to " +
                Seconds(options.ToSeconds) + " s at " + options.FramesPerSecond.ToString("0.##", CultureInfo.InvariantCulture) +
                " frames a second on " + threads + " threads");

            var film = new Recording(outDirectory, config.Hash(), options, result);

            try
            {
                double physicsStep = (double)metabolicSeconds / stepsPerMetabolic;
                double Now() => world.ElapsedSeconds + (sim.Steps % stepsPerMetabolic) * physicsStep;

                bool windowDone = false;
                bool extinct = false;
                double afterSecond = double.NaN;

                // The restored instant is itself the end of a step: a row the run wrote there, and
                // a frame due there, are taken before anything moves.
                double t = Now();

                if (t + Eps >= options.FromSeconds) film.Start(world, t);

                Compare(world, runRows, options, film, result, afterSecond);

                if (film.Started) film.TakeFrameIfDue(sim, t);

                if (film.Started && t + Eps >= options.ToSeconds)
                {
                    windowDone = true;
                    afterSecond = AfterSecond(runRows, options, result);
                }

                while (!(windowDone && !(world.ElapsedSeconds + Eps < afterSecond)))
                {
                    bool metabolic = sim.Step();
                    t = Now();

                    if (!film.Started && t + Eps >= options.FromSeconds) film.Start(world, t);

                    if (metabolic)
                    {
                        loop.MetabolicSteps++;

                        // The config holds the launch's integer count as a float, exactly.
                        loop.AssayFired = Program.ActOnWorld(
                            world, inoculum, config.InoculateAtSeconds, (int)config.InoculateCount,
                            config.InoculateDepthMetres, loop.AssayFired);

                        extinct = Program.EndsExtinct(world);

                        if (film.Started && !windowDone) film.CaptureBodies(world, t);

                        Compare(world, runRows, options, film, result, afterSecond);

                        // The loop's report step, or its last row at an extinction. The window's
                        // events are taken off the queue first, because the sampler drains it.
                        if (loop.MetabolicSteps % reportEvery == 0 || extinct)
                        {
                            if (film.Started) film.TakeEvents(world);

                            sampler.Write(sim, null, columns);

                            if (film.Started && !windowDone) film.FlushMembers();
                        }
                    }

                    if (film.Started && !windowDone) film.TakeFrameIfDue(sim, t);

                    if (film.Started && !windowDone && t + Eps >= options.ToSeconds)
                    {
                        windowDone = true;
                        film.TakeEvents(world);
                        film.FlushMembers();
                        afterSecond = AfterSecond(runRows, options, result);
                    }

                    if (extinct)
                    {
                        result.EndedExtinct = true;
                        break;
                    }
                }

                film.TakeEvents(world);
                result.LastSecond = world.ElapsedSeconds;
            }
            finally
            {
                film.Close();
            }

            wall.Stop();
            result.WallSeconds = wall.Elapsed.TotalSeconds;

            Decide(result, options);
            film.WriteVerdict(result, options, header, physicsDt);

            return result;
        }

        // ------------------------------------------------------------------ the checks

        private static void Validate(Options options)
        {
            if (string.IsNullOrEmpty(options.Run)) throw new ArgumentException("No run directory. " + Usage);
            if (string.IsNullOrEmpty(options.Out)) throw new ArgumentException("No out directory. " + Usage);

            if (!(options.FromSeconds >= 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.FromSeconds, "<from s> must be 0 or more.");
            }

            if (!(options.ToSeconds > options.FromSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.ToSeconds, "<to s> must be after <from s>.");
            }

            if (!(options.FramesPerSecond > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.FramesPerSecond, "--fps must be above 0.");
            }

            if (options.Threads < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.Threads, "--threads must be 1 or more.");
            }
        }

        private static bool ReadsSetting(string name) =>
            name == "EVOSIM_ALLOW_SOURCE_MISMATCH" || name == "EVOSIM_REPO_ROOT" ||
            name == "EVOSIM_THREADS" || name == "EVOSIM_INOCULATE";

        /// <summary>
        /// A run directory, or the newest run under an arm directory: the last in ordinal order
        /// that holds a <c>config.json</c>, which is <see cref="Checkpoint.Resolve"/>'s rule.
        /// </summary>
        private static string ResolveRunDirectory(string path, TextWriter log)
        {
            string full = Path.GetFullPath(path);

            if (!Directory.Exists(full))
            {
                throw new DirectoryNotFoundException("'" + path + "' is not a directory. " + Usage);
            }

            if (File.Exists(Path.Combine(full, "config.json"))) return full;

            string[] candidates = Directory.GetDirectories(full);
            Array.Sort(candidates, StringComparer.Ordinal);

            for (int i = candidates.Length - 1; i >= 0; i--)
            {
                if (!File.Exists(Path.Combine(candidates[i], "config.json"))) continue;

                log.WriteLine("film-window: " + path + " is an arm directory; filming its newest run, " + candidates[i]);
                return candidates[i];
            }

            throw new DirectoryNotFoundException(
                "'" + path + "' holds no config.json and no run directory that does.");
        }

        /// <summary>The run directory is the recording, and a window never writes into it.</summary>
        private static void RefuseTheRunDirectory(string runDirectory, string outDirectory)
        {
            string run = runDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = outDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool inside =
                string.Equals(run, target, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(run + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(run + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (inside)
            {
                throw new ArgumentException(
                    "The out directory " + outDirectory + " is inside the run directory " +
                    runDirectory + ". A window never writes into the recording it replays.");
            }
        }

        /// <summary>Refused rather than overwritten or appended to: one window, one directory.</summary>
        private static void RefuseEarlierOutput(string outDirectory)
        {
            string[] names =
            {
                PosesFileName, Path.ChangeExtension(PosesFileName, ".idx"), GenomesFileName,
                PlansFileName, EventsFileName, IdentityFileName,
            };

            foreach (string name in names)
            {
                string path = Path.Combine(outDirectory, name);
                if (!File.Exists(path)) continue;

                throw new IOException(
                    path + " already exists. A window's files are refused rather than " +
                    "overwritten; name a new out directory or remove the old window.");
            }
        }

        /// <summary>
        /// What stands in for a checkpoint header when the run has none at or before the window:
        /// the run's own seed, step and source hashes, at second 0.
        /// </summary>
        private static CheckpointHeader FoundingHeader(
            string runDirectory, RunConfig config, JsonNode manifest, string manifestText, double from)
        {
            if (manifest.Has("resumedFrom") && manifest["resumedFrom"].Kind == JsonNode.NodeKind.Object)
            {
                JsonNode source = manifest["resumedFrom"];

                throw new InvalidOperationException(
                    runDirectory + " began from a checkpoint of " + source["arm"] + "/" +
                    source["run"] + " at " + source["seconds"] + " s and holds none of its own at " +
                    "or before " + Seconds(from) + " s. Founding it would step the source run's " +
                    "world from 0; film the source run instead, or a second after this run's " +
                    "first checkpoint.");
            }

            // By pattern rather than through the parser, whose numbers are doubles: a seed is a
            // 64-bit integer and a double holds 53 bits of one. CheckpointFidelity's rule.
            Match seed = Regex.Match(manifestText, "\"seed\"\\s*:\\s*(\\d+)");
            if (!seed.Success) throw new InvalidDataException("No seed in " + runDirectory + "/run.json.");

            float dt = EnvBinding.ResolvePhysicsStep(config.PhysicsStepSeconds, out int perMetabolic);
            JsonNode hashes = manifest["source"];

            return new CheckpointHeader
            {
                Version = Checkpoint.Version,
                Seconds = 0d,
                Seed = ulong.Parse(seed.Groups[1].Value, CultureInfo.InvariantCulture),
                PhysicsSteps = 0L,
                PhysicsStepSeconds = dt,
                StepsPerMetabolicStep = perMetabolic,
                ConfigHash = manifest["configHash"].AsString(),
                CoreHash = hashes["coreHash"].AsString(),
                DynamicsHash = hashes["dynamicsHash"].AsString(),
                FarmHash = hashes["farmHash"].AsString(),

                // Not in run.json; InferReportEvery takes it from the rows the run wrote.
                ReportEvery = 0,
            };
        }

        /// <summary>
        /// D060's inoculum, when the run's assay has not fired by the restore and could fire
        /// before the window stops stepping; null otherwise.
        /// </summary>
        /// <remarks>
        /// The instant, the count and the depth come from the run's config, which the launch
        /// wrote from the same settings the loop read them from. The genome comes from the path
        /// the run's manifest names, or from <c>EVOSIM_INOCULATE</c> when the file has moved, and
        /// is refused unless its bytes hash to what the manifest recorded.
        /// </remarks>
        private static Genome Inoculum(
            RunConfig config, JsonNode manifest, EnvSettings settings, bool assayFired,
            double to, double reportInterval, TextWriter log)
        {
            string recordedPath = manifest.Has("inoculateGenomePath") &&
                                  manifest["inoculateGenomePath"].Kind == JsonNode.NodeKind.String
                ? manifest["inoculateGenomePath"].AsString()
                : null;

            if (string.IsNullOrEmpty(recordedPath) || !(config.InoculateAtSeconds > 0f) || assayFired)
            {
                return null;
            }

            // The furthest a window steps: its end, and one report interval more for the row after.
            if (config.InoculateAtSeconds > to + reportInterval + Eps) return null;

            string path = !string.IsNullOrEmpty(settings.InoculatePath) ? settings.InoculatePath : recordedPath;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The run inoculates at " + Seconds(config.InoculateAtSeconds) + " s, which the " +
                    "window reaches, and its genome is not at " + path + ". Name it with " +
                    "EVOSIM_INOCULATE.", path);
            }

            byte[] bytes = File.ReadAllBytes(path);
            string recordedHash = manifest.Has("inoculateGenomeHash") &&
                                  manifest["inoculateGenomeHash"].Kind == JsonNode.NodeKind.String
                ? manifest["inoculateGenomeHash"].AsString()
                : null;

            string hash = Manifest.HashBytes(bytes);

            if (recordedHash != null && !string.Equals(hash, recordedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    path + " hashes to " + hash + " and the run inoculated " + recordedHash + ". A " +
                    "window inoculating another genome is a different world.");
            }

            log.WriteLine("film-window: the run's assay is pending, inoculating " + path + " at " + Seconds(config.InoculateAtSeconds) + " s as the run did");
            return GenomeJson.Read(Encoding.UTF8.GetString(bytes));
        }

        // ------------------------------------------------------------------ the identity

        /// <summary>The five fields of one of the run's own <c>stats.jsonl</c> rows.</summary>
        private struct RunRow
        {
            public double T;
            public int Alive;
            public long Births;
            public long Deaths;
            public double AuditResidual;
            public double MeanHeight;
        }

        /// <summary>
        /// Every complete row of the run's <c>stats.jsonl</c> at or after a second, by its second.
        /// </summary>
        /// <remarks>
        /// Through <see cref="JsonlWriter.ReadRows"/>, which shares the file with a live run's
        /// writer and drops a half-written last row. A row missing one of the five fields is
        /// refused, because a comparison against a guess is not a comparison.
        /// </remarks>
        private static SortedList<double, RunRow> ReadRunRows(string runDirectory, double from)
        {
            var rows = new SortedList<double, RunRow>();
            string path = Path.Combine(runDirectory, "stats.jsonl");

            if (!File.Exists(path)) return rows;

            foreach (string line in JsonlWriter.ReadRows(path))
            {
                if (line.Length == 0 || line[line.Length - 1] != '}') continue;

                JsonNode row = Json.Parse(line);
                double t = row["t"].AsDouble();
                if (t < from) continue;

                foreach (string field in new[] { "alive", "births", "deaths", "auditResidual", "meanHeight" })
                {
                    if (!row.Has(field))
                    {
                        throw new InvalidDataException(
                            path + "'s row at " + Seconds(t) + " s has no " + field + ", so the " +
                            "window cannot be compared with it.");
                    }
                }

                rows[t] = new RunRow
                {
                    T = t,
                    Alive = row["alive"].AsInt(),
                    Births = (long)row["births"].AsDouble(),
                    Deaths = (long)row["deaths"].AsDouble(),
                    AuditResidual = row["auditResidual"].AsDouble(),
                    MeanHeight = row["meanHeight"].AsDouble(),
                };
            }

            return rows;
        }

        /// <summary>
        /// A founded window's report cadence, from the run's first row: a founded run's first row
        /// is its first report step. Twenty, the farm's default, when the run wrote no row.
        /// </summary>
        /// <remarks>
        /// It decides only when the sampler drains the world's instrument queues, which nothing
        /// the world steps on reads; the identity is taken at the rows' own seconds either way.
        /// </remarks>
        private static int InferReportEvery(SortedList<double, RunRow> rows, float metabolicSeconds)
        {
            if (rows.Count == 0) return 20;

            int every = (int)Math.Round(rows.Keys[0] / metabolicSeconds);
            return every >= 1 ? every : 20;
        }

        /// <summary>
        /// Compares the world with the run's row at the world's second, if the run wrote one
        /// there, and books it before, inside or after the window.
        /// </summary>
        private static void Compare(
            World world, SortedList<double, RunRow> runRows, Options options, Recording film,
            Result result, double afterSecond)
        {
            double e = world.ElapsedSeconds;
            if (!runRows.TryGetValue(e, out RunRow run)) return;

            string phase = e < options.FromSeconds - Eps ? "before"
                : e <= options.ToSeconds + Eps ? "window"
                : "after";

            // Past the window only the one row the window stepped on for is compared.
            if (phase == "after" && !(Math.Abs(e - afterSecond) < Eps)) return;

            int alive = world.Living.Count;
            long births = world.Births;
            long deaths = world.Deaths;
            double audit = world.AuditResidual;

            // The sampler's mean height, term for term: a double sum of the living's float
            // heights in the world's own order, over the count (Sampler.Write's meanDepth).
            double depth = 0d;
            for (int i = 0; i < world.Living.Count; i++) depth += world.Living[i].HeightY;
            double meanHeight = alive > 0 ? depth / alive : 0d;

            var differ = new List<string>();
            if (alive != run.Alive) differ.Add("alive");
            if (births != run.Births) differ.Add("births");
            if (deaths != run.Deaths) differ.Add("deaths");
            if (BitConverter.DoubleToInt64Bits(audit) != BitConverter.DoubleToInt64Bits(run.AuditResidual)) differ.Add("auditResidual");
            if (BitConverter.DoubleToInt64Bits(meanHeight) != BitConverter.DoubleToInt64Bits(run.MeanHeight)) differ.Add("meanHeight");

            bool agree = differ.Count == 0;

            if (phase == "before")
            {
                result.RowsBefore++;
                if (agree) result.RowsBeforeAgreed++;
            }
            else
            {
                result.Rows++;
                if (agree) result.RowsAgreed++;
            }

            if (!agree && double.IsNaN(result.PartedAtSeconds))
            {
                result.PartedAtSeconds = e;
                result.PartedField = string.Join(",", differ);
            }

            if (phase == "before") return;

            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("t", e);
            w.Field("phase", phase);
            w.Field("agree", agree);

            if (!agree)
            {
                w.BeginArray("differ");
                foreach (string field in differ) w.Value(field);
                w.EndArray();
            }

            w.Field("alive", alive);
            w.Field("births", births);
            w.Field("deaths", deaths);
            w.Field("auditResidual", audit);
            w.Field("meanHeight", meanHeight);

            w.BeginObject("run");
            w.Field("alive", run.Alive);
            w.Field("births", run.Births);
            w.Field("deaths", run.Deaths);
            w.Field("auditResidual", run.AuditResidual);
            w.Field("meanHeight", run.MeanHeight);
            w.EndObject();

            w.EndObject();
            film.Identity(w.ToString());
        }

        /// <summary>
        /// The run's first row after the window, when no row fell inside it; NaN otherwise, or
        /// when the run wrote none after it.
        /// </summary>
        private static double AfterSecond(SortedList<double, RunRow> runRows, Options options, Result result)
        {
            if (result.Rows > 0) return double.NaN;

            foreach (double t in runRows.Keys)
            {
                if (t > options.ToSeconds + Eps) return t;
            }

            return double.NaN;
        }

        /// <summary>The verdict: faithful, cousin or unverified, and why.</summary>
        private static void Decide(Result result, Options options)
        {
            Judge(result, options);

            if (result.EndedExtinct)
            {
                result.Reason +=
                    "; the world went extinct at t=" + Seconds(result.LastSecond) + " s and the " +
                    "window stopped there";
            }
        }

        private static void Judge(Result result, Options options)
        {
            string counts =
                result.RowsAgreed + " of " + result.Rows + " report second(s) agree" +
                (result.RowsBefore > 0
                    ? ", and " + result.RowsBeforeAgreed + " of " + result.RowsBefore + " before the window"
                    : "");

            if (result.SourceMismatch)
            {
                result.Verdict = Cousin;
                result.Reason = "source mismatch (" + result.SourceNote + "); " + counts;
                return;
            }

            if (!double.IsNaN(result.PartedAtSeconds))
            {
                result.Verdict = Cousin;
                result.Reason =
                    "parted from the run at t=" + Seconds(result.PartedAtSeconds) + " s in " +
                    result.PartedField + "; " + counts;
                return;
            }

            if (result.Rows == 0)
            {
                result.Verdict = Unverified;
                result.Reason =
                    "the run wrote no row inside the window or after it to compare with" +
                    (result.RowsBefore > 0 ? "; " + counts : "");
                return;
            }

            double reached = result.EndedExtinct ? result.LastSecond : options.ToSeconds;

            if (double.IsNaN(result.LastRunRowSeconds) || reached > result.LastRunRowSeconds + Eps)
            {
                result.Verdict = Unverified;
                result.Reason =
                    "the run's last row is at t=" + Seconds(result.LastRunRowSeconds) + " s and the " +
                    "window runs to " + Seconds(reached) + " s, so its end is not compared; " + counts;
                return;
            }

            result.Verdict = Faithful;
            result.Reason = counts;
        }

        /// <summary>The one line stdout carries.</summary>
        public static string Summary(Result result, Options options)
        {
            var c = CultureInfo.InvariantCulture;

            return "film window " + Path.GetFileName(result.RunDirectory) + " " +
                   Seconds(options.FromSeconds) + "-" + Seconds(options.ToSeconds) + " s: " +
                   result.Frames.ToString(c) + " frames at " + options.FramesPerSecond.ToString("0.##", c) +
                   " fps, " + result.Bodies.ToString(c) + " bodies (" + result.Births.ToString(c) +
                   " born, " + result.Deaths.ToString(c) + " died), wall " +
                   (result.WallSeconds / 60d).ToString("0.0", c) + " min on " + result.Threads.ToString(c) +
                   " threads, " + result.Verdict.ToUpperInvariant() + ": " + result.Reason;
        }

        private static string Seconds(double s) =>
            double.IsNaN(s) ? "?" : s.ToString("0.###", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ the five files

        /// <summary>The window's writers, and what they have written.</summary>
        /// <remarks>
        /// Nothing is created until the window opens, so a window stopped during its approach
        /// leaves an empty out directory rather than five empty files that refuse the next try.
        /// </remarks>
        private sealed class Recording
        {
            private readonly string _outDirectory;
            private readonly string _configHash;
            private readonly Options _options;
            private readonly Result _result;

            private PoseRecorder _poses;
            private GzipMembers _genomes;
            private JsonlWriter _plans;
            private JsonlWriter _events;
            private JsonlWriter _identity;

            private readonly HashSet<long> _withGenome = new HashSet<long>();
            private readonly Dictionary<long, Plan> _plansWritten = new Dictionary<long, Plan>();
            private readonly List<long> _born = new List<long>();

            private long _nextFrame;
            private readonly long _lastFrame;

            public bool Started { get; private set; }

            public Recording(string outDirectory, string configHash, Options options, Result result)
            {
                _outDirectory = outDirectory;
                _configHash = configHash;
                _options = options;
                _result = result;

                double fps = options.FramesPerSecond;
                _nextFrame = (long)Math.Ceiling(options.FromSeconds * fps - 1e-6);
                _lastFrame = (long)Math.Floor(options.ToSeconds * fps + 1e-6);
            }

            private void Open()
            {
                Directory.CreateDirectory(_outDirectory);

                _poses = PoseRecorder.AtPath(
                    Path.Combine(_outDirectory, PosesFileName), (float)(1d / _options.FramesPerSecond),
                    _configHash);
                _genomes = new GzipMembers(Path.Combine(_outDirectory, GenomesFileName));
                _plans = new JsonlWriter(Path.Combine(_outDirectory, PlansFileName), flushEachRow: false);
                _events = new JsonlWriter(Path.Combine(_outDirectory, EventsFileName), flushEachRow: false);
                OpenIdentity();
            }

            private void OpenIdentity()
            {
                if (_identity != null) return;

                Directory.CreateDirectory(_outDirectory);
                _identity = new JsonlWriter(Path.Combine(_outDirectory, IdentityFileName), flushEachRow: true);
            }

            /// <summary>The window opens: every living body's genome, and its plan where it has moved.</summary>
            public void Start(World world, double t)
            {
                if (Started) return;

                Open();
                Started = true;

                for (int i = 0; i < world.Living.Count; i++)
                {
                    Organism creature = world.Living[i];
                    WriteGenome(creature);
                    WritePlanIfChanged(creature, t);
                }

                _genomes.Flush();
            }

            /// <summary>After a metabolic step inside the window: the newborns' genomes and every plan that moved.</summary>
            public void CaptureBodies(World world, double t)
            {
                for (int i = 0; i < world.Living.Count; i++)
                {
                    Organism creature = world.Living[i];
                    if (!_withGenome.Contains(creature.Id)) WriteGenome(creature);
                    WritePlanIfChanged(creature, t);
                }
            }

            /// <summary>A frame, if one is due at this physics step. Never two at one step.</summary>
            public void TakeFrameIfDue(Simulation sim, double t)
            {
                if (!Started || _nextFrame > _lastFrame) return;

                double fps = _options.FramesPerSecond;
                if (t + Eps < _nextFrame / fps) return;

                _poses.WriteFrame(sim, t);
                _result.Frames++;

                while (_nextFrame <= _lastFrame && _nextFrame / fps <= t + Eps) _nextFrame++;
            }

            /// <summary>The lineage queue's births, deaths and bites inside the window.</summary>
            public void TakeEvents(World world)
            {
                if (!Started) return;

                IReadOnlyList<LineageEvent> events = world.DrainLineageEvents();

                for (int i = 0; i < events.Count; i++)
                {
                    LineageEvent e = events[i];

                    // After the start and up to the end: a birth at the start second is already
                    // one of the bodies alive at the start, and a death there is already absent.
                    if (!(e.ElapsedSeconds > _options.FromSeconds + Eps) ||
                        e.ElapsedSeconds > _options.ToSeconds + Eps)
                    {
                        continue;
                    }

                    _events.Write(e.ToJson());

                    if (e.Kind == LineageEventKind.Birth)
                    {
                        _result.Births++;
                        _born.Add(e.Id);
                    }
                    else if (e.Kind == LineageEventKind.Death)
                    {
                        _result.Deaths++;
                    }
                    else
                    {
                        _result.Bites++;
                    }
                }
            }

            public void FlushMembers()
            {
                if (!Started) return;

                _genomes.Flush();
                _plans.Flush();
                _events.Flush();
            }

            public void Identity(string row)
            {
                OpenIdentity();
                _identity.Write(row);
            }

            public void Close()
            {
                // A birth whose body was born and gone inside one metabolic step never stood in
                // the living list the genomes are taken from.
                for (int i = 0; i < _born.Count; i++)
                {
                    if (!_withGenome.Contains(_born[i])) _result.BirthsWithoutAGenome++;
                }

                _born.Clear();

                _poses?.Dispose();
                _poses = null;
                _genomes?.Dispose();
                _genomes = null;
                _plans?.Dispose();
                _plans = null;
                _events?.Dispose();
                _events = null;
            }

            /// <summary>The verdict, last in <c>identity.jsonl</c>, and the file closed.</summary>
            public void WriteVerdict(Result result, Options options, CheckpointHeader header, float physicsDt)
            {
                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("verdict", result.Verdict);
                w.Field("reason", result.Reason);
                w.Field("run", Path.GetFileName(result.RunDirectory));
                w.Field("arm", header.SourceArm);
                w.Field("from", options.FromSeconds);
                w.Field("to", options.ToSeconds);
                w.Field("fps", options.FramesPerSecond);
                w.Field("physicsDt", physicsDt);
                w.Field("restoredFrom", result.Checkpoint != null ? Path.GetFileName(result.Checkpoint) : null);
                w.Field("restoredAt", result.RestoredSeconds);
                w.Field("frames", result.Frames);
                w.Field("bodies", result.Bodies);
                w.Field("births", result.Births);
                w.Field("deaths", result.Deaths);
                w.Field("bites", result.Bites);
                w.Field("birthsWithoutAGenome", result.BirthsWithoutAGenome);
                w.Field("planRows", result.PlanRows);
                w.Field("rows", result.Rows);
                w.Field("rowsAgreed", result.RowsAgreed);
                w.Field("rowsBefore", result.RowsBefore);
                w.Field("rowsBeforeAgreed", result.RowsBeforeAgreed);

                if (double.IsNaN(result.PartedAtSeconds)) w.Field("partedAt", (string)null);
                else w.Field("partedAt", result.PartedAtSeconds);

                w.Field("partedField", result.PartedField);
                w.Field("sourceMismatch", result.SourceNote);
                w.Field("endedExtinct", result.EndedExtinct);
                w.Field("threads", result.Threads);
                w.Field("wallSeconds", result.WallSeconds);
                w.EndObject();

                OpenIdentity();
                _identity.Write(w.ToString());
                _identity.Dispose();
                _identity = null;
            }

            private void WriteGenome(Organism creature)
            {
                if (!_withGenome.Add(creature.Id)) return;

                _genomes.Add(GenomeJson.Write(creature.Genome, indent: false, id: creature.Id));
                _result.Bodies++;
            }

            /// <summary>
            /// A plans row when the body's module counts or lost parts differ from what was last
            /// written for it, the genome's own plan standing for a body with no row yet.
            /// </summary>
            private void WritePlanIfChanged(Organism creature, double t)
            {
                int[] counts = creature.ModuleCounts;
                List<int[]> lost = creature.LostPartPaths;

                if (_plansWritten.TryGetValue(creature.Id, out Plan last))
                {
                    if (last.Same(counts, lost)) return;
                }
                else if (Plan.IsEmpty(counts, lost))
                {
                    return;
                }

                _plansWritten[creature.Id] = Plan.Of(counts, lost);

                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("t", t);
                w.Field("id", creature.Id);

                w.BeginArray("moduleCounts");
                if (counts != null) foreach (int count in counts) w.Value(count);
                w.EndArray();

                w.BeginArray("lostPaths");
                if (lost != null)
                {
                    foreach (int[] path in lost)
                    {
                        w.BeginArray();
                        foreach (int step in path) w.Value(step);
                        w.EndArray();
                    }
                }
                w.EndArray();

                w.EndObject();

                _plans.Write(w.ToString());
                _result.PlanRows++;
            }
        }

        /// <summary>A body's plan as it was last written: copies, so a change in place is seen.</summary>
        private sealed class Plan
        {
            private int[] _counts;
            private int[][] _lost;

            public static bool IsEmpty(int[] counts, List<int[]> lost) =>
                (counts == null || counts.Length == 0) && (lost == null || lost.Count == 0);

            public static Plan Of(int[] counts, List<int[]> lost)
            {
                var plan = new Plan
                {
                    _counts = counts == null ? new int[0] : (int[])counts.Clone(),
                    _lost = new int[lost == null ? 0 : lost.Count][],
                };

                for (int i = 0; i < plan._lost.Length; i++) plan._lost[i] = (int[])lost[i].Clone();

                return plan;
            }

            public bool Same(int[] counts, List<int[]> lost)
            {
                int countLength = counts == null ? 0 : counts.Length;
                if (countLength != _counts.Length) return false;

                for (int i = 0; i < countLength; i++)
                {
                    if (counts[i] != _counts[i]) return false;
                }

                int lostLength = lost == null ? 0 : lost.Count;
                if (lostLength != _lost.Length) return false;

                for (int i = 0; i < lostLength; i++)
                {
                    int[] a = lost[i];
                    int[] b = _lost[i];
                    if (a.Length != b.Length) return false;

                    for (int j = 0; j < a.Length; j++)
                    {
                        if (a[j] != b[j]) return false;
                    }
                }

                return true;
            }
        }
    }

    /// <summary>
    /// A gzip file written as a run of complete members: each <see cref="Flush"/> closes one.
    /// </summary>
    /// <remarks>
    /// Concatenated members are one valid gzip file (RFC 1952, section 2.2), so a reader takes
    /// the whole file as one stream, and a process killed between two flushes leaves every
    /// member before the kill complete. What was added and not yet flushed is lost with it, which
    /// is the JSONL writers' contract at a coarser grain.
    /// </remarks>
    public sealed class GzipMembers : IDisposable
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private FileStream _file;
        private readonly StringBuilder _pending = new StringBuilder();
        private int _pendingRows;

        /// <summary>Rows in complete members.</summary>
        public int Rows { get; private set; }

        /// <summary>Complete members written.</summary>
        public int Members { get; private set; }

        /// <summary>Creates the file. Refuses one that exists rather than appending a member to it.</summary>
        public GzipMembers(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            _file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }

        /// <summary>One row, held until the next member. One row is one line, as in every JSONL file.</summary>
        public void Add(string row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            if (row.IndexOf('\n') >= 0 || row.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A row contains a line break.", nameof(row));
            }

            _pending.Append(row).Append('\n');
            _pendingRows++;
        }

        /// <summary>Writes whatever is held as one complete member. Nothing held, nothing written.</summary>
        public void Flush()
        {
            if (_file == null) throw new ObjectDisposedException(nameof(GzipMembers));
            if (_pendingRows == 0) return;

            byte[] bytes = Utf8.GetBytes(_pending.ToString());

            using (var member = new GZipStream(_file, CompressionLevel.Optimal, true))
            {
                member.Write(bytes, 0, bytes.Length);
            }

            _file.Flush();

            Rows += _pendingRows;
            Members++;

            _pending.Clear();
            _pendingRows = 0;
        }

        public void Dispose()
        {
            if (_file == null) return;

            Flush();
            _file.Dispose();
            _file = null;
        }
    }
}
