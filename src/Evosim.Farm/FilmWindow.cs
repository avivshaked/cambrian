using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    /// per body alive at the window's start or born inside it, in Core's gzip members
    /// (<see cref="JsonlGzWriter"/>, the run's own <c>genomes.jsonl.gz</c> format) written at each
    /// report second, so a killed window keeps every member before the last and one reader
    /// (<see cref="GzipMemberReader"/>, <c>runrec.py</c>) reads a run's genomes and a window's
    /// alike. <c>plans.jsonl</c>, each body's module counts and lost parts at the start where they
    /// differ from the genome's, and every change after. <c>events.jsonl</c>, the run's own
    /// lineage rows for every birth, death and bite inside the window. <c>identity.jsonl</c>, the
    /// window's count of the living, its births, its deaths, its audit residual and its mean
    /// height against the run's own <c>stats.jsonl</c> row at every report second inside the
    /// window, and a verdict line last.
    /// </para>
    /// <para>
    /// <b>A corpse is written beside the bodies, in a sixth file</b> (D127, round 51).
    /// <c>corpses.jsonl.gz</c> holds two kinds of row. A <c>corpse</c> row, once per corpse, carries
    /// what does not change while it stands: the pose its body died in, its plan, the linear
    /// scale it died at, its first joules, where it was first seen, and the second it was founded
    /// when that was inside the window. A <c>step</c> row at every metabolic step carries every
    /// corpse's place, the share of its first joules it still holds and whether it has settled,
    /// which is all the world moves: a corpse is carried, settled and dissolved at the metabolic
    /// step and nowhere between. The harness stamps a new corpse's pose at the next physics step,
    /// when it takes the dead body out of the solver, so a corpse first seen without one is held
    /// back to the next step's row, and one that never gets one (a body the solver lost) is
    /// written at its second sight without it. The corpses standing at the start died before the
    /// window, so their genomes come from the run's own <c>genomes.jsonl.gz</c>, read once. A
    /// world whose dead leave no corpse (<c>CorpseDecayPerSecond</c> 0) writes no such file.
    /// The rows are the world's own numbers, read after the metabolic step as the plans are, and
    /// nothing the world steps on reads them.
    /// </para>
    /// <para>
    /// <b>Remains are written beside the bodies, in a file of their own</b> (D220, contact eating).
    /// <c>remains.jsonl.gz</c> exists only for a window that sees remains, and holds three kinds of line. A
    /// <c>remains</c> row, once per remains body the first metabolic step the window sees it (at the window's opening
    /// for those standing then), carries its id, the body it was cut from (<c>from</c>, or -1 for a body that is
    /// itself the remains), the second its window began (<c>since</c>, which a remains of remains keeps from the
    /// original), the scale its body stood at, the plan it was developed on and the map that selects its parts from
    /// that developed body (<c>Phenotype.Select</c>), which is null for the whole body. A <c>shed</c> row follows each
    /// time the map changes, because a part of the remains was eaten. A genome row, in the form of the window's
    /// own, is written for each remains body the window has no genome row for: the standing at the start, whose
    /// bodies died before it, and a limb, which is no body any genome develops. The window's own files are written
    /// exactly as before: the rows are additive, a reader that does not know them reads the window as it always did,
    /// and a world without contact eating writes none of them.
    /// </para>
    /// <para>
    /// <b>The genomes come from the world's admission queue</b>
    /// (<see cref="World.QueueAdmittedGenomes"/>), turned on before the restore as the run's loop
    /// turns it on, so a body born and dead between two metabolic steps still has its genome. The
    /// bodies alive at the start were admitted before the window and are written from the living
    /// roster, which is what a resumed run does with its own. The window takes the queue at every
    /// metabolic step inside it, before the sampler's drain at a report step, and the sampler
    /// drains and drops whatever is left on the steps the run's sampler drained it.
    /// </para>
    /// <para>
    /// <b>Faithful is measured, not assumed.</b> The config is the world, so a window whose
    /// <c>configHash</c> differs from the run's is refused, unless
    /// <c>EVOSIM_ALLOW_SOURCE_MISMATCH</c> is set, and is then a cousin whatever its rows say.
    /// The three source hashes, <c>coreHash</c>, <c>dynamicsHash</c> and <c>farmHash</c>, decide
    /// nothing on their own. A window reads <c>faithful</c> when every identity row it compared
    /// agrees with the run's to the bit, and every source hash that differs is named in its
    /// verdict. The rows are the evidence: a change to the solver or the economy parts the audit
    /// residual or the mean height at the first row, and a build that only added a reader or a
    /// queue does not (the owner's ruling, 2026-09-24). A row that parts, before the window or
    /// inside it, makes a <c>cousin</c>, and the verdict says the second and the field. A window
    /// with no report second inside it steps on to the run's next one and compares that. A
    /// window that compared no row, or runs past the run's last row, is <c>unverified</c>. A
    /// resume still refuses all four hashes, because it writes the run's continuation.
    /// </para>
    /// <para>
    /// <b>Exit codes.</b> 0 for a faithful window, differing source hashes included. 2 for a
    /// cousin and 3 for an unverified window, both written whole. 1 for a refusal or a failure,
    /// which writes nothing or stops part way.
    /// </para>
    /// </remarks>
    public static class FilmWindow
    {
        public const string PosesFileName = "film.poses.bin";
        public const string GenomesFileName = "genomes.jsonl.gz";
        public const string PlansFileName = "plans.jsonl";
        public const string EventsFileName = "events.jsonl";
        public const string IdentityFileName = "identity.jsonl";
        public const string CorpsesFileName = "corpses.jsonl.gz";

        /// <summary>
        /// The remains standing at the start, founded inside the window or shedding parts (D220), and the genomes of
        /// those the window has none for. Written only when the window sees remains.
        /// </summary>
        public const string RemainsFileName = "remains.jsonl.gz";

        public const double DefaultFramesPerSecond = 30d;

        public const int ExitFaithful = 0;
        public const int ExitRefused = 1;
        public const int ExitCousin = 2;
        public const int ExitUnverified = 3;

        public const string Faithful = "faithful";
        public const string Cousin = "cousin";
        public const string Unverified = "unverified";

        private const double Eps = 1e-9;

        private const string Usage =
            "Usage: Evosim.Farm --film-window <run dir> <from s> <to s> <out dir> [--fps N] [--threads N] [--probe <report file>]";

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

            /// <summary>
            /// Where <see cref="NonFiniteProbe"/>'s report goes, or null for no probe. With one, every
            /// metabolic step from the restore on is scanned, and the window stops at the first
            /// finding, with the report written here and to the log.
            /// </summary>
            public string Probe;
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

            /// <summary>
            /// Whether the config differs from the run's: a refusal unless
            /// <c>EVOSIM_ALLOW_SOURCE_MISMATCH</c> is set, and a cousin when it is.
            /// </summary>
            public bool SourceMismatch;

            /// <summary>The config's hash line when it differs, or null.</summary>
            public string SourceNote;

            /// <summary>
            /// The line of every source hash (Core, Dynamics, farm) that differs from the run's.
            /// Named in the verdict and never a reason on its own to call the window a cousin:
            /// the identity rows decide.
            /// </summary>
            public readonly List<string> SourcesDiffer = new List<string>();

            public int Frames;

            /// <summary>Genome rows written: every body alive at the start or born inside.</summary>
            public int Bodies;

            public int Births;
            public int Deaths;
            public int Bites;
            public int BirthsWithoutAGenome;
            public int PlanRows;

            /// <summary>Corpses written (D127): one facts row each in <c>corpses.jsonl.gz</c>.</summary>
            public int Corpses;

            /// <summary>Step rows written, one at every metabolic step inside the window.</summary>
            public int CorpseSteps;

            /// <summary>Genome rows taken from the run's own file, for the corpses standing at the start.</summary>
            public int CorpseGenomes;

            /// <summary>Corpses whose body has no genome row in the window, which a player cannot draw.</summary>
            public int CorpsesWithoutAGenome;

            /// <summary>
            /// Remains bodies with a first row in <c>remains.jsonl.gz</c> (D220): those standing at the start, those
            /// that died inside the window and those founded inside it as a cut-off limb.
            /// </summary>
            public int Remains;

            /// <summary>Remains that lost parts after their first row: one <c>shed</c> row each time.</summary>
            public int RemainsSheds;

            /// <summary>Genome rows written into <c>remains.jsonl.gz</c>, for the remains the window has no genome row for.</summary>
            public int RemainsGenomes;

            /// <summary>The run's own <c>r</c> rows inside the window: remains cut off a body.</summary>
            public int RemainsFounded;

            /// <summary>The run's own <c>rc</c> rows inside the window: remains that closed.</summary>
            public int RemainsClosed;

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

            return ExitCodeOf(result.Verdict);
        }

        /// <summary>The exit code for a verdict: 0 faithful, 2 cousin, 3 unverified.</summary>
        public static int ExitCodeOf(string verdict) =>
            verdict == Faithful ? ExitFaithful
            : verdict == Unverified ? ExitUnverified
            : ExitCousin;

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

                    case "--probe":
                        options.Probe = value;
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

            float physicsDt = EnvBinding.ResolvePhysicsStep(header.PhysicsStepSeconds, config.PaceFactor, out int stepsPerMetabolic);

            // D239. A frame is taken at a physics step and filmed at a world second, so the step that
            // limits the rate is the physics step in world seconds: dt itself at a pace factor of 1.
            double worldStepSeconds = config.PaceFactor == 1f ? physicsDt : physicsDt / (double)config.PaceFactor;

            if (1d / options.FramesPerSecond < worldStepSeconds - 1e-12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.FramesPerSecond,
                    "A frame is taken at a physics step, and this run's step is " +
                    (config.PaceFactor == 1f
                        ? physicsDt.ToString("R", CultureInfo.InvariantCulture)
                        : worldStepSeconds.ToString("R", CultureInfo.InvariantCulture)) + " s, so the rate can be " +
                    "at most " + (1d / worldStepSeconds).ToString("0.##", CultureInfo.InvariantCulture) +
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

            // The config's hash is refused as a resume refuses it; the three source hashes are
            // named and the rows below decide (the faithful rule, in the remarks).
            RunManifest build = Manifest.Build(
                settings, config.Hash(), null, physicsDt, stepsPerMetabolic, threads, config.PaceFactor);

            SourceComparison sources = CompareSources(
                header, config.Hash(), build.CoreHash, build.DynamicsHash, build.FarmHash);

            ApplySources(result, sources, settings.AllowSourceMismatch, checkpoint != null, log);

            // The run's rows, from the restore on, keyed by their second.
            SortedList<double, RunRow> runRows = ReadRunRows(runDirectory, header.Seconds - Eps);
            if (runRows.Count > 0) result.LastRunRowSeconds = runRows.Keys[runRows.Count - 1];

            // Metabolise's own float: world seconds, which at a pace factor above 1 is the metabolic step itself.
            float metabolicSeconds = config.PaceFactor == 1f ? stepsPerMetabolic * physicsDt : EnvBinding.MetabolicStepSeconds;
            int reportEvery = header.ReportEvery > 0
                ? header.ReportEvery
                : InferReportEvery(runRows, metabolicSeconds);

            Parallelism.Threads = threads;

            // The admission queue on before the restore and before the first step, as the run's
            // loop turns it on, so no admission comes before it. Recording only: a world that
            // queues and one that does not step the same trajectory.
            var world = new World(config, header.Seed, pool?.Genomes, pool?.Weights) { QueueAdmittedGenomes = true };
            var sim = new Simulation(
                world, header.Seed, physicsDt, stepsPerMetabolic, threads, runDirectory: null);
            // D159: a world of a migrating group was sent genomes by the others, which the window
            // does not step. It makes the run's own recorded offers instead, at their seconds and in
            // their order, and refuses to go on if one lands under another id than the run's.
            bool migrating = manifest.Has("migrateEverySeconds");

            var sampler = new Sampler
            {
                PoolNamed = config.FoundingTricklePoolCount > 0,
                Migrating = migrating,
            };
            IReadOnlyList<string> columns = Sampler.Columns(config, migrating);

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

            ImmigrantRecord.Replay immigrants = migrating
                ? new ImmigrantRecord.Replay(runDirectory, world.ElapsedSeconds)
                : null;

            log.WriteLine(
                "film-window: " + (checkpoint != null
                    ? "restored " + checkpoint + " at " + Seconds(header.Seconds) + " s"
                    : "founded " + runDirectory + " from its config and seed " + header.Seed) +
                "; stepping to " + Seconds(options.FromSeconds) + " s writing nothing, then filming to " +
                Seconds(options.ToSeconds) + " s at " + options.FramesPerSecond.ToString("0.##", CultureInfo.InvariantCulture) +
                " frames a second on " + threads + " threads");

            var film = new Recording(
                outDirectory, config.Hash(), options, result, runDirectory, config.CorpseDecayPerSecond > 0f);

            try
            {
                double physicsStep = (double)metabolicSeconds / stepsPerMetabolic;
                double Now() => world.ElapsedSeconds + (sim.Steps % stepsPerMetabolic) * physicsStep;

                bool windowDone = false;
                bool extinct = false;
                double afterSecond = double.NaN;

                NonFiniteProbe probe = options.Probe == null ? null : new NonFiniteProbe();

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

                        // Where the run's loop takes its exchange: after the assay, before the test.
                        immigrants?.PlantDue(world);

                        extinct = Program.EndsExtinct(world);

                        // After the assay, whose inoculants are admissions too: this step's genomes
                        // off the admission queue, and every plan that moved.
                        if (film.Started && !windowDone) film.CaptureBodies(world, t);

                        Compare(world, runRows, options, film, result, afterSecond);

                        if (probe != null)
                        {
                            string finding = probe.Scan(world, t);
                            if (finding != null)
                            {
                                log.WriteLine(finding);
                                File.WriteAllText(options.Probe, finding);
                                break;
                            }

                            if (probe.Scans % 200 == 0)
                            {
                                log.WriteLine(FormattableString.Invariant($"probe: {t:0.#} s clean, {probe.Scans} scans"));
                            }
                        }

                        // The loop's report step, or its last row at an extinction. The window's
                        // events are taken off the lineage queue first, and its genomes were taken
                        // off the admission queue above, because the sampler drains both: with
                        // nowhere to write, as the run's sampler drained them into its files.
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
                        film.EndWindow(world);
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
            // The verdict's physicsDt is what a reader steps its clock by, so world seconds a step (D239); the same
            // number as the physics step at a pace factor of 1.
            film.WriteVerdict(result, options, header, config.PaceFactor == 1f ? physicsDt : (float)worldStepSeconds);

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
                PlansFileName, EventsFileName, IdentityFileName, CorpsesFileName,
                RemainsFileName,
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

            float dt = EnvBinding.ResolvePhysicsStep(config.PhysicsStepSeconds, config.PaceFactor, out int perMetabolic);
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

        /// <summary>
        /// The verdict, faithful, cousin or unverified, and why, from what a window found. Public
        /// so the rule can be tested on a result without filming one.
        /// </summary>
        /// <remarks>
        /// A differing source hash never decides the word. Each one is named at the end of the
        /// reason whatever the word is, so a window of a run recorded on an older build says so
        /// on its verdict line and in <c>identity.jsonl</c>.
        /// </remarks>
        public static void Decide(Result result, Options options)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (options == null) throw new ArgumentNullException(nameof(options));

            Judge(result, options);

            if (result.EndedExtinct)
            {
                result.Reason +=
                    "; the world went extinct at t=" + Seconds(result.LastSecond) + " s and the " +
                    "window stopped there";
            }

            if (result.SourcesDiffer.Count > 0)
            {
                result.Reason +=
                    "; the source differs from the run's (" + string.Join("; ", result.SourcesDiffer) +
                    "), and the identity rows decide rather than the hashes";
            }
        }

        /// <summary>
        /// A window's source hashes against the run's, split by what each one decides.
        /// </summary>
        public sealed class SourceComparison
        {
            /// <summary>
            /// The <c>configHash</c> line when it differs, naming the run's value and this build's,
            /// or null. It refuses the window unless <c>EVOSIM_ALLOW_SOURCE_MISMATCH</c> is set,
            /// and makes it a cousin when it is.
            /// </summary>
            public string Config;

            /// <summary>
            /// The <c>coreHash</c>, <c>dynamicsHash</c> and <c>farmHash</c> lines that differ, in
            /// that order, after a <c>checkpointVersion</c> line when the checkpoint is round 48's
            /// version 4, read lossily (<see cref="CheckpointHeader.ReadLossily"/>). Named in the
            /// verdict and never decisive on their own: the identity rows decide.
            /// </summary>
            public readonly List<string> Sources = new List<string>();
        }

        /// <summary>
        /// Splits a checkpoint's (or a founded run's) four hashes against this build's into the
        /// config's, which decides, and the three source hashes, which are named.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the config decides.</b> The config is the world. A window stepped under another
        /// one is another world, whatever its first rows say.
        /// </para>
        /// <para>
        /// <b>Why the source hashes do not.</b> Core, Dynamics and the farm are hashed as source
        /// trees, so a build that adds a reader, a queue or a column moves a hash without moving
        /// any trajectory. Round 49's build moves Core's and the farm's for recording alone, and a
        /// rule on the hashes would make a cousin of every window of every earlier run. The identity rows show
        /// whether the world stepped as the run did, and they are compared bit for bit: a change
        /// to the solver or the economy parts the audit residual or the mean height at the first
        /// row (the owner's ruling, 2026-09-24). A resume still refuses all four
        /// (<c>Program.RecordResume</c>), because it writes the run's continuation and not a film
        /// of it.
        /// </para>
        /// </remarks>
        public static SourceComparison CompareSources(
            CheckpointHeader header, string configHash, string coreHash, string dynamicsHash, string farmHash)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var comparison = new SourceComparison();

            // Differences reads the four in a fixed order, each line starting with its hash's name.
            foreach (string line in header.Differences(configHash, coreHash, dynamicsHash, farmHash))
            {
                if (line.StartsWith("configHash:", StringComparison.Ordinal)) comparison.Config = line;
                else comparison.Sources.Add(line);
            }

            return comparison;
        }

        /// <summary>
        /// Books a comparison on a result: refuses a differing config unless the override is set,
        /// marks the window a cousin when it is, and names every differing source hash.
        /// </summary>
        /// <remarks>
        /// Public so the refusal can be tested without a run to film. The source hashes never
        /// refuse a window, which is the whole of its difference from a resume's check.
        /// </remarks>
        public static void ApplySources(
            Result result, SourceComparison sources, bool allowSourceMismatch, bool fromCheckpoint,
            TextWriter log)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (log == null) log = TextWriter.Null;

            if (sources.Config != null)
            {
                if (!allowSourceMismatch)
                {
                    throw new InvalidOperationException(
                        "This " + (fromCheckpoint ? "checkpoint" : "run") + " was written under " +
                        "another config, so a window of it would step another world: " +
                        sources.Config + ". Set EVOSIM_ALLOW_SOURCE_MISMATCH=1 to film it anyway; " +
                        "the window is then marked a cousin.");
                }

                result.SourceMismatch = true;
                result.SourceNote = sources.Config;

                log.WriteLine(
                    "film-window: warning: filming under another config, so this window is a " +
                    "cousin of the recording: " + sources.Config);
            }

            if (sources.Sources.Count > 0)
            {
                result.SourcesDiffer.AddRange(sources.Sources);

                log.WriteLine(
                    "film-window: the source differs from the run's (" +
                    string.Join("; ", sources.Sources) + "). It is named in the verdict and does " +
                    "not make the window a cousin; the identity rows decide.");

                // Round 48's checkpoints (version 4) restore without the contact record, so any
                // body whose brain reads the Contact or Damage sense steps differently from the
                // first step. The rows still decide, and are expected to part at the first one.
                foreach (string line in sources.Sources)
                {
                    if (!line.StartsWith("checkpointVersion:", StringComparison.Ordinal)) continue;

                    log.WriteLine(
                        "film-window: warning: this checkpoint is read lossily (" + line + "); " +
                        "a body that reads its contact or damage sense steps differently from the " +
                        "recording, and the window is faithful only if every identity row still " +
                        "agrees.");
                }
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
                result.Reason =
                    "the config differs from the run's (" + result.SourceNote +
                    "), filmed under EVOSIM_ALLOW_SOURCE_MISMATCH; " + counts;
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
                   " born, " + result.Deaths.ToString(c) + " died)" +
                   (result.Corpses > 0 ? ", " + result.Corpses.ToString(c) + " corpses" : "") +
                   (result.Remains > 0 ? ", " + result.Remains.ToString(c) + " remains" : "") + ", wall " +
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
            private JsonlGzWriter _genomes;
            private JsonlWriter _plans;
            private JsonlWriter _events;
            private JsonlWriter _identity;

            private readonly string _runDirectory;
            private readonly bool _corpsesOn;
            private JsonlGzWriter _corpses;

            /// <summary>Corpses whose facts row is written: each is in every step row while it stands.</summary>
            private readonly HashSet<long> _corpseWritten = new HashSet<long>();

            /// <summary>Corpses seen without their pose and held back a step (the class remarks).</summary>
            private readonly Dictionary<long, CorpseSight> _corpseHeld = new Dictionary<long, CorpseSight>();

            private readonly HashSet<long> _corpseSeen = new HashSet<long>();
            private readonly List<long> _corpseGone = new List<long>();
            private readonly StringBuilder _corpseRow = new StringBuilder(1 << 16);
            private double _corpseLast = double.NegativeInfinity;

            private JsonlGzWriter _remains;
            private RemainsRecorder _remainsRows;

            private readonly HashSet<long> _withGenome = new HashSet<long>();
            private readonly Dictionary<long, Plan> _plansWritten = new Dictionary<long, Plan>();
            private readonly List<long> _born = new List<long>();

            /// <summary>
            /// Genomes taken off the world's admission queue and not yet written: each is written
            /// when its body's birth row turns out to lie inside the window, so the file holds the
            /// bodies alive at the start and those born inside, and no other.
            /// </summary>
            private readonly Dictionary<long, Genome> _admitted = new Dictionary<long, Genome>();

            private long _nextFrame;
            private readonly long _lastFrame;

            public bool Started { get; private set; }

            public Recording(
                string outDirectory, string configHash, Options options, Result result,
                string runDirectory, bool corpses)
            {
                _outDirectory = outDirectory;
                _configHash = configHash;
                _options = options;
                _result = result;
                _runDirectory = runDirectory;
                _corpsesOn = corpses;

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
                // Core's members, the run's own genomes.jsonl.gz format: a member at each report
                // second, each carrying its length, so one reader reads both files.
                _genomes = new JsonlGzWriter(Path.Combine(_outDirectory, GenomesFileName), memberEachRow: false);

                // D127's corpses, only in a world whose dead leave one (the class remarks).
                if (_corpsesOn) _corpses = new JsonlGzWriter(Path.Combine(_outDirectory, CorpsesFileName), memberEachRow: false);
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

            /// <summary>
            /// The window opens: every living body's genome from the roster, and its plan where it
            /// has moved. What the admission queue holds is kept for <see cref="TakeEvents"/>.
            /// </summary>
            /// <remarks>
            /// The bodies alive at the start were admitted before it, so the queue does not have
            /// them all: the sampler drained it at the run's last report step and dropped what it
            /// held. The roster has them, as a resumed run's roster has its own
            /// (<c>Sampler.WriteLivingGenomes</c>). The queue is taken here as well, because the
            /// step the window opens on may have admitted bodies of its own, and one born and dead
            /// inside that step stands in no roster.
            /// </remarks>
            public void Start(World world, double t)
            {
                if (Started) return;

                Open();
                Started = true;

                TakeAdmissions(world);

                for (int i = 0; i < world.Living.Count; i++)
                {
                    Organism creature = world.Living[i];
                    WriteGenome(creature.Id, creature.Genome);
                    WritePlanIfChanged(creature, t);
                }

                CaptureCorpses(world, t, atStart: true);
                TakeCorpseGenomesFromTheRun();
                CaptureRemains(world, t, atStart: true);

                _genomes.EndMember();
            }

            /// <summary>
            /// After a metabolic step inside the window: that step's admissions off the queue,
            /// before the sampler can drain them, and every plan that moved.
            /// </summary>
            public void CaptureBodies(World world, double t)
            {
                TakeAdmissions(world);

                // Before the end test below: a step row past the end still tells the player where
                // each corpse was going, for the frames after the last step inside the window.
                CaptureCorpses(world, t, atStart: false);

                // A step past the window's end (an end between two metabolic steps) moves no plan
                // a frame will draw.
                if (t > _options.ToSeconds + Eps) return;

                for (int i = 0; i < world.Living.Count; i++)
                {
                    WritePlanIfChanged(world.Living[i], t);
                }

                CaptureRemains(world, t, atStart: false);
            }

            /// <summary>Everything on the world's admission queue, held until its birth row is read.</summary>
            private void TakeAdmissions(World world)
            {
                IReadOnlyList<AdmittedGenome> admitted = world.DrainAdmittedGenomes();

                for (int i = 0; i < admitted.Count; i++)
                {
                    _admitted[admitted[i].Id] = admitted[i].Genome;
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

            /// <summary>
            /// The lineage queue's births, deaths and bites inside the window, and the genome of
            /// every body born inside it.
            /// </summary>
            /// <remarks>
            /// A bite (<see cref="LineageEventKind.Kill"/>) rides the same queue as a birth and a
            /// death, is written to <c>events.jsonl</c> as the run wrote it to its lineage, and is
            /// counted apart from both. Every admission is a birth row and a queued genome, one of
            /// each per id (<c>World.Admit</c>), so a birth row inside the window finds its genome
            /// among those taken off the admission queue.
            /// </remarks>
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

                        if (_admitted.TryGetValue(e.Id, out Genome genome))
                        {
                            WriteGenome(e.Id, genome);
                            _admitted.Remove(e.Id);
                        }
                    }
                    else if (e.Kind == LineageEventKind.Death)
                    {
                        _result.Deaths++;
                    }
                    else if (e.Kind == LineageEventKind.RemainsFounded)
                    {
                        _result.RemainsFounded++;
                    }
                    else if (e.Kind == LineageEventKind.RemainsClosed)
                    {
                        _result.RemainsClosed++;
                    }
                    else
                    {
                        _result.Bites++;
                    }
                }
            }

            /// <summary>
            /// The window's end: its last events and their genomes, every file's member closed,
            /// and the admissions held for a birth that can no longer fall inside it let go.
            /// </summary>
            public void EndWindow(World world)
            {
                TakeEvents(world);
                WriteHeldCorpses();
                FlushMembers();
                _admitted.Clear();
            }

            public void FlushMembers()
            {
                if (!Started) return;

                _genomes.EndMember();
                _plans.Flush();
                _events.Flush();
                _corpses?.EndMember();
                _remains?.EndMember();
            }

            public void Identity(string row)
            {
                OpenIdentity();
                _identity.Write(row);
            }

            public void Close()
            {
                // A check, and it reads 0: every admission queues its genome beside its birth row,
                // so a body born and dead between two metabolic steps has its genome too. A birth
                // counted here is a genome the queue did not hand over, which is a fault to find.
                for (int i = 0; i < _born.Count; i++)
                {
                    if (!_withGenome.Contains(_born[i])) _result.BirthsWithoutAGenome++;
                }

                _born.Clear();
                _admitted.Clear();

                _poses?.Dispose();
                _poses = null;
                _genomes?.Dispose();
                _genomes = null;
                _plans?.Dispose();
                _plans = null;
                _events?.Dispose();
                _events = null;

                // A corpse whose body has no genome in the window cannot be drawn. It is counted in
                // the verdict, and reads zero unless the run's own genomes file lacks the body.
                foreach (long id in _corpseWritten)
                {
                    if (!_withGenome.Contains(id)) _result.CorpsesWithoutAGenome++;
                }

                _corpseWritten.Clear();
                _corpseHeld.Clear();
                _corpses?.Dispose();
                _corpses = null;

                _remainsRows = null;
                _remains?.Dispose();
                _remains = null;
            }

            /// <summary>The verdict, last in <c>identity.jsonl</c>, and the file closed.</summary>
            public void WriteVerdict(Result result, Options options, CheckpointHeader header, float physicsDt)
            {
                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("verdict", result.Verdict);
                w.Field("reason", result.Reason);
                w.Field("run", Path.GetFileName(result.RunDirectory));

                // The full path, so a player can find the run's config, seed and floor without
                // being told (FilmWindowReader); the name above stays for every reader of it.
                w.Field("runDirectory", result.RunDirectory);
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
                w.Field("corpses", result.Corpses);
                w.Field("corpseSteps", result.CorpseSteps);
                w.Field("corpseGenomes", result.CorpseGenomes);
                w.Field("corpsesWithoutAGenome", result.CorpsesWithoutAGenome);

                // D220's remains, only for a window that saw some: a window of a world without contact eating
                // carries the fields it always did.
                if (result.Remains > 0 || result.RemainsFounded > 0 || result.RemainsClosed > 0)
                {
                    w.Field("remains", result.Remains);
                    w.Field("remainsSheds", result.RemainsSheds);
                    w.Field("remainsGenomes", result.RemainsGenomes);
                    w.Field("remainsFounded", result.RemainsFounded);
                    w.Field("remainsClosed", result.RemainsClosed);
                }

                w.Field("rows", result.Rows);
                w.Field("rowsAgreed", result.RowsAgreed);
                w.Field("rowsBefore", result.RowsBefore);
                w.Field("rowsBeforeAgreed", result.RowsBeforeAgreed);

                if (double.IsNaN(result.PartedAtSeconds)) w.Field("partedAt", (string)null);
                else w.Field("partedAt", result.PartedAtSeconds);

                w.Field("partedField", result.PartedField);
                w.Field("configDiffers", result.SourceNote);
                w.BeginArray("sourcesDiffer");
                foreach (string line in result.SourcesDiffer) w.Value(line);
                w.EndArray();
                w.Field("endedExtinct", result.EndedExtinct);
                w.Field("threads", result.Threads);
                w.Field("wallSeconds", result.WallSeconds);
                w.EndObject();

                OpenIdentity();
                _identity.Write(w.ToString());
                _identity.Dispose();
                _identity = null;
            }

            // ------------------------------------------------------------ the corpses (D127)

            /// <summary>
            /// One step row of every corpse the world holds, and the facts row of each corpse seen for
            /// the first time with its pose, or for the second time with or without one.
            /// </summary>
            /// <param name="atStart">
            /// The window's opening. A corpse standing there with its pose died before the window, is
            /// drawn from the first frame, and carries no founding second.
            /// </param>
            private void CaptureCorpses(World world, double t, bool atStart)
            {
                if (_corpses == null) return;

                // One row a step. The window can open on a metabolic step, and then the opening and
                // the step's own capture fall at one second; a second capture there would take a
                // corpse held for its pose as seen twice.
                if (t <= _corpseLast + 1e-9) return;
                _corpseLast = t;

                IReadOnlyList<Corpse> corpses = world.Corpses;

                _corpseRow.Clear();
                _corpseRow.Append('[');
                _corpseSeen.Clear();
                int placed = 0;

                for (int i = 0; i < corpses.Count; i++)
                {
                    Corpse corpse = corpses[i];
                    long id = corpse.CreatureId;

                    if (!_corpseWritten.Contains(id))
                    {
                        if (_corpseHeld.TryGetValue(id, out CorpseSight sight))
                        {
                            _corpseHeld.Remove(id);
                        }
                        else
                        {
                            sight = CorpseSight.Of(corpse, t, atStart && corpse.HasPose ? double.NaN : t);

                            if (!corpse.HasPose)
                            {
                                _corpseHeld[id] = sight;
                                _corpseSeen.Add(id);
                                continue;
                            }
                        }

                        WriteCorpseFacts(sight);
                    }

                    if (placed++ > 0) _corpseRow.Append(',');
                    AppendPlace(_corpseRow, corpse);
                }

                _corpseRow.Append(']');

                // A held corpse the world no longer has went inside one step (eaten, or dissolved at
                // once). It is written from its first sight, so a player shows it for that step.
                if (_corpseHeld.Count > _corpseSeen.Count)
                {
                    _corpseGone.Clear();

                    foreach (KeyValuePair<long, CorpseSight> held in _corpseHeld)
                    {
                        if (!_corpseSeen.Contains(held.Key)) _corpseGone.Add(held.Key);
                    }

                    for (int i = 0; i < _corpseGone.Count; i++)
                    {
                        WriteCorpseFacts(_corpseHeld[_corpseGone[i]]);
                        _corpseHeld.Remove(_corpseGone[i]);
                    }
                }

                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("k", "step");
                w.Field("t", t);
                w.Raw("h", _corpseRow.ToString());
                w.EndObject();

                _corpses.Write(w.ToString());
                _result.CorpseSteps++;
            }

            /// <summary>A corpse's facts row, once per corpse: what does not change while it stands.</summary>
            private void WriteCorpseFacts(CorpseSight sight)
            {
                Corpse corpse = sight.Corpse;
                long id = corpse.CreatureId;
                if (!_corpseWritten.Add(id)) return;

                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("k", "corpse");
                w.Field("id", id);
                w.Field("t", sight.Seconds);

                if (double.IsNaN(sight.Born)) w.Field("born", (string)null);
                else w.Field("born", sight.Born);

                w.BeginArray("at");
                w.Value(sight.X);
                w.Value(sight.Y);
                w.Value(sight.Z);
                w.Value(sight.Fraction);
                w.Value(sight.Settled ? 1 : 0);
                w.EndArray();

                w.Field("first", corpse.FirstJoules);
                w.Field("scaledBy", corpse.ScaledBy);

                // Read now and not at first sight: the harness stamps it a step after the death.
                w.Field("pose", corpse.HasPose);

                Quat q = corpse.Rotation;
                w.BeginArray("rot");
                w.Value(q.X);
                w.Value(q.Y);
                w.Value(q.Z);
                w.Value(q.W);
                w.EndArray();

                w.BeginArray("moduleCounts");
                if (corpse.ModuleCounts != null) foreach (int count in corpse.ModuleCounts) w.Value(count);
                w.EndArray();

                w.BeginArray("lostPaths");
                if (corpse.LostPartPaths != null)
                {
                    foreach (int[] path in corpse.LostPartPaths)
                    {
                        w.BeginArray();
                        foreach (int step in path) w.Value(step);
                        w.EndArray();
                    }
                }
                w.EndArray();

                w.EndObject();

                _corpses.Write(w.ToString());
                _result.Corpses++;
            }

            /// <summary>
            /// One corpse's place in a step row: its id, x, y and z to the millimetre, the share of its
            /// first joules it holds, and 1 when it has settled.
            /// </summary>
            private static void AppendPlace(StringBuilder row, Corpse corpse)
            {
                CultureInfo inv = CultureInfo.InvariantCulture;
                Float3 p = corpse.Position;

                row.Append(corpse.CreatureId.ToString(inv)).Append(',')
                   .Append(p.X.ToString("0.###", inv)).Append(',')
                   .Append(p.Y.ToString("0.###", inv)).Append(',')
                   .Append(p.Z.ToString("0.###", inv)).Append(',')
                   .Append(CorpseSight.FractionOf(corpse).ToString("0.####", inv)).Append(',')
                   .Append(corpse.Settled ? '1' : '0');
            }

            /// <summary>The window's end: every corpse still held back is written from its first sight.</summary>
            private void WriteHeldCorpses()
            {
                if (_corpses == null || _corpseHeld.Count == 0) return;

                _corpseGone.Clear();
                foreach (long id in _corpseHeld.Keys) _corpseGone.Add(id);

                for (int i = 0; i < _corpseGone.Count; i++) WriteCorpseFacts(_corpseHeld[_corpseGone[i]]);

                _corpseHeld.Clear();
            }

            /// <summary>
            /// The genomes of the corpses standing at the start, from the run's own
            /// <c>genomes.jsonl.gz</c>. They died before the window, so neither the roster nor the
            /// admission queue has them. One pass over the file, stopping when every one is found.
            /// </summary>
            private void TakeCorpseGenomesFromTheRun()
            {
                if (_corpses == null || _runDirectory == null) return;

                var wanted = new HashSet<long>();

                foreach (long id in _corpseWritten)
                {
                    if (!_withGenome.Contains(id)) wanted.Add(id);
                }

                foreach (long id in _corpseHeld.Keys)
                {
                    if (!_withGenome.Contains(id)) wanted.Add(id);
                }

                if (wanted.Count == 0) return;

                string path = Path.Combine(_runDirectory, GenomesFileName);
                if (!File.Exists(path)) return;

                using (GzipMemberReader reader = GzipMemberReader.Open(path))
                {
                    foreach (string line in reader.Lines())
                    {
                        long id = IdOf(line);
                        if (id < 0 || !wanted.Remove(id)) continue;

                        _genomes.Write(line);
                        _withGenome.Add(id);
                        _result.CorpseGenomes++;

                        if (wanted.Count == 0) break;
                    }
                }
            }

            /// <summary>
            /// A genome row's id, read off its head where <see cref="GenomeJson.Write"/> puts it, and
            /// parsed whole only when it is not there: the run's file holds a row for every birth.
            /// </summary>
            private static long IdOf(string row)
            {
                const string head = "{\"id\":";

                if (row.StartsWith(head, StringComparison.Ordinal))
                {
                    long id = 0;
                    int i = head.Length;
                    int digits = 0;

                    while (i < row.Length && row[i] >= '0' && row[i] <= '9')
                    {
                        id = id * 10 + (row[i] - '0');
                        i++;
                        digits++;
                    }

                    if (digits > 0 && i < row.Length && (row[i] == ',' || row[i] == '}')) return id;
                }

                try
                {
                    return GenomeJson.ReadId(row);
                }
                catch (Exception)
                {
                    return -1L;
                }
            }

            /// <summary>One genome row, the run's own form (<c>Sampler.DrainGenomes</c>), once per id.</summary>
            private void WriteGenome(long id, Genome genome)
            {
                if (!_withGenome.Add(id)) return;

                _genomes.Write(GenomeJson.Write(genome, indent: false, id: id));
                _result.Bodies++;
            }

            // ------------------------------------------------------------ the remains (D220)

            /// <summary>
            /// The remains the world holds, as the first row of each one the window has not written, and a shed row
            /// for each that lost parts since its last row. Writes nothing, and creates no file, in a world with none.
            /// </summary>
            private void CaptureRemains(World world, double t, bool atStart)
            {
                if (world.Remains.Count == 0) return;

                if (_remainsRows == null)
                {
                    _remainsRows = new RemainsRecorder(line =>
                    {
                        if (_remains == null)
                        {
                            _remains = new JsonlGzWriter(Path.Combine(_outDirectory, RemainsFileName), memberEachRow: false);
                        }

                        _remains.Write(line);
                    });
                }

                _remainsRows.Capture(
                    world.Remains, t, atStart, id => _withGenome.Contains(id), id => _admitted.ContainsKey(id));

                _result.Remains = _remainsRows.Remains;
                _result.RemainsSheds = _remainsRows.Sheds;
                _result.RemainsGenomes = _remainsRows.Genomes;
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

        /// <summary>
        /// The lines of <c>remains.jsonl.gz</c> (D220), taken from the world's standing remains at each metabolic
        /// step. Presentation only: it reads the world and nothing the world steps on reads it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Three lines, all one JSON object to a line.</b> A genome row, in <see cref="GenomeJson.Write"/>'s form
        /// (it starts <c>{"id":</c>), for a remains body the window has no genome row for. A first row
        /// <c>{"k":"remains","t":,"id":,"from":,"since":,"start":,"scaledBy":,"genome":,"moduleCounts":[],"lostPaths":[],"map":}</c>:
        /// <c>from</c> is the id of the body a limb was cut from and -1 for a body that is itself the remains,
        /// <c>since</c> the second its window began, <c>start</c> whether it was standing when the window opened,
        /// <c>genome</c> whether its genome row is in this file (otherwise the window's own file holds it),
        /// <c>scaledBy</c> the linear scale its body stood at (<c>Phenotype.ScaledBy</c>), and <c>map</c> the
        /// ascending indices of its parts in the body its genome develops to on the plan the counts and lost paths
        /// name, scaled by <c>scaledBy</c> (null for the whole body). A shed row
        /// <c>{"k":"shed","t":,"id":,"map":[]}</c> follows when the map changes.
        /// </para>
        /// <para>
        /// <b>A founding is a remains the window has no genome for.</b> A limb is founded under an id of its own
        /// that no birth row and no admission names, so a remains first seen after the opening whose id is neither
        /// among the window's genomes nor on its admission queue is a limb. The map is Core's
        /// <c>Organism.RemainsMap</c>, an internal property read through a delegate: the one place this reads what
        /// Core does not publish.
        /// </para>
        /// </remarks>
        internal sealed class RemainsRecorder
        {
            private static readonly Func<Organism, int[]> MapOf = MapReader();

            private static Func<Organism, int[]> MapReader()
            {
                System.Reflection.PropertyInfo property = typeof(Organism).GetProperty(
                    "RemainsMap",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                System.Reflection.MethodInfo getter = property?.GetGetMethod(nonPublic: true);
                if (getter == null) return null;

                return (Func<Organism, int[]>)Delegate.CreateDelegate(typeof(Func<Organism, int[]>), getter);
            }

            private readonly Action<string> _write;
            private readonly Dictionary<long, int[]> _seen = new Dictionary<long, int[]>();

            /// <summary>First rows written.</summary>
            public int Remains { get; private set; }

            /// <summary>Shed rows written.</summary>
            public int Sheds { get; private set; }

            /// <summary>Genome rows written.</summary>
            public int Genomes { get; private set; }

            public RemainsRecorder(Action<string> write)
            {
                _write = write ?? throw new ArgumentNullException(nameof(write));
            }

            /// <summary>The map a remains body's parts are selected by, or null for the whole body.</summary>
            public static int[] MapFor(Organism remains)
            {
                if (remains == null) throw new ArgumentNullException(nameof(remains));

                if (MapOf == null)
                {
                    throw new InvalidOperationException(
                        "Organism.RemainsMap is not there to read. A film window of a world with remains cannot say " +
                        "which parts of the developed body each remains body is without it.");
                }

                return MapOf(remains);
            }

            /// <param name="remains">The world's standing remains.</param>
            /// <param name="t">The second the rows are written at.</param>
            /// <param name="atStart">True at the window's opening: the remains standing then are not foundings.</param>
            /// <param name="hasWindowGenome">Whether the window's genomes file holds, or will hold, a body's genome.</param>
            /// <param name="isAdmitted">Whether a body's genome is on the admission queue, to be written at its birth row.</param>
            public void Capture(
                IReadOnlyList<Organism> remains, double t, bool atStart,
                Func<long, bool> hasWindowGenome, Func<long, bool> isAdmitted)
            {
                for (int i = 0; i < remains.Count; i++)
                {
                    Organism body = remains[i];
                    long id = body.Id;
                    int[] map = MapFor(body);

                    if (_seen.TryGetValue(id, out int[] last))
                    {
                        if (SameMap(last, map)) continue;

                        var shed = new Json.Writer(indent: false);
                        shed.BeginObject();
                        shed.Field("k", "shed");
                        shed.Field("t", t);
                        shed.Field("id", id);
                        MapField(shed, map);
                        shed.EndObject();
                        _write(shed.ToString());

                        _seen[id] = map == null ? null : (int[])map.Clone();
                        Sheds++;
                        continue;
                    }

                    bool inWindowFiles = hasWindowGenome(id) || (!atStart && isAdmitted(id));
                    bool founded = !atStart && !inWindowFiles;

                    if (!inWindowFiles)
                    {
                        _write(GenomeJson.Write(body.Genome, indent: false, id: id));
                        Genomes++;
                    }

                    var w = new Json.Writer(indent: false);
                    w.BeginObject();
                    w.Field("k", "remains");
                    w.Field("t", t);
                    w.Field("id", id);
                    w.Field("from", founded ? body.ParentId : -1L);
                    w.Field("since", body.RemainsSince);
                    w.Field("start", atStart);
                    w.Field("scaledBy", body.Phenotype.ScaledBy);
                    w.Field("genome", !inWindowFiles);

                    w.BeginArray("moduleCounts");
                    if (body.ModuleCounts != null) foreach (int count in body.ModuleCounts) w.Value(count);
                    w.EndArray();

                    w.BeginArray("lostPaths");
                    if (body.LostPartPaths != null)
                    {
                        foreach (int[] path in body.LostPartPaths)
                        {
                            w.BeginArray();
                            foreach (int step in path) w.Value(step);
                            w.EndArray();
                        }
                    }
                    w.EndArray();

                    MapField(w, map);
                    w.EndObject();
                    _write(w.ToString());

                    _seen[id] = map == null ? null : (int[])map.Clone();
                    Remains++;
                }
            }

            private static void MapField(Json.Writer w, int[] map)
            {
                if (map == null)
                {
                    w.Field("map", (string)null);
                    return;
                }

                w.BeginArray("map");
                foreach (int part in map) w.Value(part);
                w.EndArray();
            }

            private static bool SameMap(int[] a, int[] b)
            {
                if (a == null || b == null) return a == b;
                if (a.Length != b.Length) return false;

                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i]) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// A corpse as first seen: the object, whose pose the harness stamps a step later, and
        /// where it was then.
        /// </summary>
        private sealed class CorpseSight
        {
            public Corpse Corpse;
            public double Seconds;

            /// <summary>The second it was founded inside the window, or NaN for one standing at the start.</summary>
            public double Born;

            public float X;
            public float Y;
            public float Z;
            public float Fraction;
            public bool Settled;

            public static CorpseSight Of(Corpse corpse, double t, double born) => new CorpseSight
            {
                Corpse = corpse,
                Seconds = t,
                Born = born,
                X = corpse.Position.X,
                Y = corpse.Position.Y,
                Z = corpse.Position.Z,
                Fraction = FractionOf(corpse),
                Settled = corpse.Settled,
            };

            /// <summary>The share of its first joules a corpse still holds: what a player shrinks it by.</summary>
            public static float FractionOf(Corpse corpse) =>
                corpse.FirstJoules > 0d ? (float)(corpse.Joules / corpse.FirstJoules) : 1f;
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
}
