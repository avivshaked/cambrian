using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The farm, out of Unity: an arm launched from the environment, as
    /// <c>-executeMethod Evosim.Sim.EditorTools.EvolutionRun.Run</c> was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this does today (work package I).</b> Reads the environment, builds the
    /// <see cref="RunConfig"/>, constructs the world so the header and the manifest can describe
    /// the one the run will actually have, creates the run directory with its <c>config.json</c>,
    /// writes the <c>running</c> manifest, prints the report header — and then stops, because the
    /// loop is work package G's file and is not here. Everything above the loop is finished and
    /// testable without it, which is the point of the split.
    /// </para>
    /// <para>
    /// <b>Parameterised by environment rather than by argument</b>, as the Unity entry was, so
    /// that <c>run-arm.ps1</c> and the round launchers keep working unchanged. Arguments are
    /// accepted as <c>EVOSIM_NAME=value</c> pairs that override the environment, which a console
    /// program can take and an <c>-executeMethod</c> could not; anything else is refused rather
    /// than ignored, for the reason an unparseable setting is.
    /// </para>
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// Whether the run ends <c>extinct</c> after this step: nobody alive, and nobody being
        /// added — D115, the owner's game over (<c>logbook/specs/founding-trickle-spec.md</c> §1).
        /// </summary>
        /// <remarks>
        /// A world with no living body ends the step it empties, as it always has, except while
        /// the founding trickle is adding founders (<see cref="World.FoundersStillArriving"/>):
        /// that world is being refounded at the trickle's pace, and the record shows the crash as
        /// a gap in every inherited column rather than as the end of the run. With the trickle
        /// off this is the old test to the character. Every other ending is decided where it
        /// always was.
        /// </remarks>
        public static bool EndsExtinct(World world) =>
            world.Living.Count == 0 && !world.FoundersStillArriving;

        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            // The placer's float maths on this runtime: --float-math [<out file>]. FloatMathSweep
            // says what it prints and what the Editor's twin is.
            if (args.Length >= 1 && args[0] == "--float-math")
            {
                return FloatMathSweep.Run(args.Length > 1 ? args[1] : Path.Combine("scratch", "floatmath", "dotnet.txt"));
            }

            // The checkpoint's in-process acceptance: --verify-checkpoint <file> <seconds>
            // [<scratch dir>] [<threads>]. CheckpointFidelity says what it does.
            if (args.Length >= 3 && args[0] == "--verify-checkpoint")
            {
                return CheckpointFidelity.Run(
                    args[1],
                    double.Parse(args[2], CultureInfo.InvariantCulture),
                    args.Length > 3 ? args[3] : Path.Combine("scratch", "checkpoint-fidelity"),
                    args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 4);
            }

            // A stretch of a recorded run, replayed from its checkpoint and written down thirty
            // times a second for the theatre: --film-window <run dir> <from s> <to s> <out dir>
            // [--fps N] [--threads N]. FilmWindow says what it writes and what it refuses.
            if (args.Length >= 1 && args[0] == "--film-window")
            {
                return FilmWindow.Run(args);
            }

            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string arg in args)
            {
                if (arg == "-h" || arg == "--help")
                {
                    Help();
                    return 0;
                }

                int eq = arg.IndexOf('=');
                if (eq <= 0 || !arg.StartsWith("EVOSIM_", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(
                        "'" + arg + "' is not a setting. Arguments are EVOSIM_NAME=value pairs; " +
                        "everything else is refused rather than ignored.");
                    return 1;
                }

                overrides[arg.Substring(0, eq)] = arg.Substring(eq + 1);
            }

            EnvBinding.Lookup env = name =>
                overrides.TryGetValue(name, out string v) ? v : Environment.GetEnvironmentVariable(name);

            EnvSettings settings = EnvBinding.Read(env);

            // Ignored, and said aloud rather than only ignored: EvolutionRun never enumerated the
            // environment, so a typo in a launcher has always been silent, and silence is how a
            // world nobody asked for gets filed under a header that describes another one.
            var names = new List<string>(overrides.Keys);
            foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            {
                names.Add(e.Key as string);
            }

            IReadOnlyList<string> unknown = EnvBinding.UnknownNames(names);
            if (unknown.Count > 0)
            {
                Console.Error.WriteLine(
                    "warning: " + string.Join(", ", unknown) +
                    " is set and is not a setting this build reads — ignored, exactly as the " +
                    "Unity entry ignored it.");
            }

            if (settings.RequestedJobWorkers != 0)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_PHYSICS_JOBS is " + settings.RequestedJobWorkers +
                    " and this engine has no job system — it changes nothing. The thread count " +
                    "is EVOSIM_THREADS, and it changes pace and not the trajectory.");
            }

            // The inoculum, loaded at startup rather than when the assay fires, so a malformed or
            // missing file fails the run immediately instead of thousands of simulated seconds in.
            string inoculumHash = null;
            string inoculumHashShort = null;

            if (!string.IsNullOrEmpty(settings.InoculatePath))
            {
                byte[] bytes = File.ReadAllBytes(settings.InoculatePath);
                GenomeJson.Read(Encoding.UTF8.GetString(bytes));
                inoculumHash = Manifest.HashBytes(bytes);
                inoculumHashShort = inoculumHash.Substring(0, 12);
            }
            else if (settings.InoculateAt > 0f)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_INOCULATE_AT is set but EVOSIM_INOCULATE names no genome " +
                    "file — the assay will not fire.");
            }

            if (inoculumHash != null && settings.InoculateAt <= 0f)
            {
                Console.Error.WriteLine(
                    "warning: EVOSIM_INOCULATE names a genome file but EVOSIM_INOCULATE_AT is " +
                    "unset or zero — the assay will not fire.");
                inoculumHashShort = null;
            }

            // D-day for a resumed run: what world it is comes from the source run's own
            // config.json rather than from this launcher's environment. The checkpoint carries a
            // configHash and the two are checked against each other below, but reading the file
            // the original wrote is what makes them equal in the first place — an environment
            // that differs by one knob would otherwise build a world the checkpoint cannot be
            // put into, and say so only after the directory had been made.
            string resumePath = Checkpoint.Resolve(settings.ResumeFrom, settings.ResumeAt);
            CheckpointHeader resume = null;
            string resumeSourceRun = null;
            RunConfig config;
            TricklePoolFiles.Pool pool = null;

            if (resumePath != null)
            {
                resume = CheckpointReader.ReadHeader(resumePath);
                resumeSourceRun = Path.GetDirectoryName(Path.GetDirectoryName(resumePath));

                config = RunDirectory.ReadConfig(resumeSourceRun, out string configEdited);

                if (configEdited != null)
                {
                    Console.Error.WriteLine(
                        "warning: " + Path.Combine(resumeSourceRun, "config.json") + " " +
                        configEdited);
                }

                // The world's seed is the world's, not the launcher's: every per-creature seed
                // derives from it and World.ReadState refuses a checkpoint from another one.
                settings.Seed = resume.Seed;

                InheritRecording(settings, resume, resumeSourceRun);

                // D117. The pool is the source run's, read from its pool/ and refused if its
                // bytes no longer hash to what the config pins; a launcher's EVOSIM_TRICKLE_POOL
                // is ignored here as every other world setting is.
                pool = TricklePoolFiles.Load(resumeSourceRun, config);

                if (!string.IsNullOrEmpty(settings.TricklePool))
                {
                    Console.Error.WriteLine(
                        "warning: EVOSIM_TRICKLE_POOL is set on a resume and is ignored: the pool " +
                        "is the resumed run's own, from " +
                        Path.Combine(resumeSourceRun, TricklePoolFiles.DirectoryName) + ".");
                }
            }
            else
            {
                config = EnvBinding.BuildConfig(settings);

                // D117. Read, re-serialised in this build's bytes and hashed before the config
                // is written, so configHash pins the pool; the files follow the directory below.
                if (!string.IsNullOrEmpty(settings.TricklePool))
                {
                    pool = TricklePoolFiles.Prepare(settings.TricklePool);
                    config.FoundingTricklePoolCount = pool.Genomes.Count;
                    config.FoundingTricklePoolHash = pool.Hash;
                }
            }

            // Record format 2 retires poses.jsonl for the state stream at the report interval,
            // unless the launcher named a cadence. After InheritRecording, so a resumed run keeps
            // the stream its source was writing (logbook/specs/record-and-film-spec.md A4).
            bool streamByDefault = settings.ApplyRecordDefaults(config.SharedSpace);

            float physicsDt = resume != null
                ? resume.PhysicsStepSeconds
                : settings.PhysicsDt;

            physicsDt = EnvBinding.ResolvePhysicsStep(physicsDt, out int stepsPerMetabolic);
            int threads = settings.ResolveThreads();

            // The same count for Core's own per-cell loops as for the body pass. Core defaults to
            // one so that the Unity farm, which sets this nowhere, keeps the serial path it has
            // always had; the console farm is the only thing that raises it, and it is a pace
            // setting on both sides of the line — the grid's numbers are the same bits at any
            // count (Evosim.Core.Parallelism).
            Parallelism.Threads = threads;

            string outPath = settings.ResolveOutPath();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

            // The world, before the directory: the header's space token and the manifest's four
            // measured bed facts are properties of the world the launch produced and exist
            // nowhere else. Constructing it here also means a config the world refuses — a cell
            // that does not divide the box, a tank under rolls — stops the launch before a run
            // directory is made for it.
            var world = new World(config, settings.Seed, pool?.Genomes);

            var space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: threads,
                inoculumHashShort: inoculumHashShort);

            RunDirectory dir = RunDirectory.Create(
                Path.Combine(
                    Path.GetDirectoryName(outPath),
                    Path.GetFileNameWithoutExtension(outPath)),
                config, DateTime.UtcNow, settings.RecordFormat);

            // D117. Beside config.json, in the bytes the hash was taken over; a resumed run
            // carries its source's pool forward so that it can itself be resumed.
            if (pool != null) TricklePoolFiles.Write(dir.Path, pool.Lines);

            RunManifest manifest = Manifest.Build(
                settings, config.Hash(), inoculumHash, physicsDt, stepsPerMetabolic, threads);

            Manifest.RecordBed(manifest, world.Bed);
            Manifest.RecordReefs(manifest, world.Reefs);

            // D102, set here for RecordBed's reason and from the same world: the ratio the streams
            // were built to, which the world's constructor derived when it told the field what
            // tank it is in. 1 in a box and in a tank whose axes balance, which is every run in
            // the record; the header's `axes v:h` token is the same number.
            manifest.StreamsAxisRatio = config.Current.StreamsAxisRatio;

            // A recording setting, recorded where the other recording settings are: it must not
            // reach config.json or its hash, and a reader of run.json is owed the cadence the
            // stream beside it was written at.
            manifest.PoseEverySeconds = settings.ResolvePoseEvery();
            manifest.CheckpointEverySeconds = settings.ResolveCheckpointEvery();
            manifest.RecordFormat = settings.RecordFormat;

            if (resume != null)
            {
                RecordResume(manifest, settings, resume, resumePath, resumeSourceRun, config);
            }

            Manifest.Write(dir, manifest, ending: null);

            var report = new Report(outPath, config);
            report.Begin(settings, config, space, manifest.EngineVersion);
            report.Flush();

            Console.WriteLine(report.Text.TrimEnd());
            Console.WriteLine();
            Console.WriteLine("run directory: " + dir.Path);
            Console.WriteLine("report:        " + outPath);
            Console.WriteLine(
                "record:        " + RunRecordFormat.Describe(settings.RecordFormat) +
                (resume != null && !settings.Provided.Contains("EVOSIM_RECORD_FORMAT")
                    ? ", the resumed run's own"
                    : "") +
                (streamByDefault
                    ? "; the state stream in place of poses.jsonl, every " +
                      settings.PoseEvery.ToString(CultureInfo.InvariantCulture) + " s (the report interval)"
                    : ""));
            Console.WriteLine(
                "threads:       " + threads.ToString(CultureInfo.InvariantCulture) +
                "   dt " + physicsDt.ToString(CultureInfo.InvariantCulture) +
                " s, metabolic step " + (stepsPerMetabolic * physicsDt).ToString(CultureInfo.InvariantCulture) + " s");

            if (resume != null)
            {
                Console.WriteLine(
                    "resumed from:  " + resumePath + " at " +
                    resume.Seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s" +
                    (manifest.ResumedWithSourceMismatch ? "  (SOURCE MISMATCH)" : ""));
            }

            if (manifest.CheckpointEverySeconds > 0d)
            {
                Console.WriteLine(
                    "checkpoints:   " + Checkpoint.DirectoryIn(dir.Path) + " every " +
                    manifest.CheckpointEverySeconds.ToString(CultureInfo.InvariantCulture) + " s");
            }

            return Loop(settings, config, world, dir, report, manifest, outPath,
                physicsDt, stepsPerMetabolic, threads, resumePath);
        }

        /// <summary>
        /// Fills in the manifest's <c>resumedFrom</c> block, and refuses a checkpoint this build
        /// did not write unless the launcher said to take it anyway.
        /// </summary>
        /// <remarks>
        /// <b>All four hashes, and the refusal is the default.</b> The config is the world, Core
        /// is the economy and the development, Dynamics is the solver, the farm is the loop
        /// around them: a checkpoint read under a different one of those carries on into a
        /// trajectory the recording never had, which is the same fault
        /// <c>Allow Source Mismatch</c> guards in the theatre and <c>-ExpectSimHash</c> guards at
        /// a launch. <c>EVOSIM_ALLOW_SOURCE_MISMATCH</c> turns it into a warning and marks the
        /// manifest, so the resulting run is readable as a cousin rather than as a continuation.
        /// </remarks>
        private static void RecordResume(
            RunManifest manifest, EnvSettings settings, CheckpointHeader resume, string resumePath,
            string sourceRun, RunConfig config)
        {
            // The directory's name, not its path: a run is named for the instant it started and
            // its settings hash, and that name joins to the arm's own directory wherever the tree
            // has since been moved to.
            manifest.ResumedFromArm = resume.SourceArm;
            manifest.ResumedFromRun = Path.GetFileName(sourceRun);
            manifest.ResumedFromSeconds = resume.Seconds;
            manifest.ResumedFromCheckpointHash = Manifest.HashBytes(File.ReadAllBytes(resumePath));

            IReadOnlyList<string> differences = resume.Differences(
                config.Hash(), manifest.CoreHash, manifest.DynamicsHash, manifest.FarmHash);

            if (differences.Count == 0) return;

            string note = string.Join("; ", differences);

            if (!settings.AllowSourceMismatch)
            {
                throw new InvalidOperationException(
                    "This checkpoint was not written by this build, so carrying on from it " +
                    "would produce a trajectory the recording never had — " + note + ". Set " +
                    "EVOSIM_ALLOW_SOURCE_MISMATCH=1 to take it anyway; the run is then marked in " +
                    "run.json as a cousin of the recording rather than its continuation.");
            }

            manifest.ResumedWithSourceMismatch = true;
            manifest.ResumeSourceNote = note;

            Console.Error.WriteLine(
                "warning: resuming across a source mismatch — " + note + ". What this run " +
                "produces is a cousin of the recording and not its continuation, and run.json " +
                "says so.");
        }

        /// <summary>
        /// The run — <c>EvolutionRun.RunBody</c>'s loop, its footer and the second half of the
        /// manifest's two-write protocol.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Five honest endings and no sixth.</b> <c>budget</c>, <c>wall</c>, <c>extinct</c>,
        /// <c>ceiling-population</c> or <c>ceiling-tissue</c> for a runaway, <c>stopped</c> for
        /// the stop file, and <c>error</c> with the exception. Both the prose and
        /// <c>run.json</c>'s <c>reason</c> start null rather than defaulted to "budget reached",
        /// which is the pre-round-8 contract's fix for a footer that made every censored arm look
        /// like a completed one.
        /// </para>
        /// <para>
        /// <b>The stop file.</b> A future <c>stop-arm</c> has no Unity process to kill and no
        /// reason to kill one: it writes <c>STOP</c> into the run directory with its reason on
        /// the first line, and the loop ends at the next metabolic step with <c>status:
        /// "stopped"</c>, that reason, and a last row and snapshot written first. The check is one
        /// <c>File.Exists</c> per report row rather than per metabolic step, so a run pays for it
        /// once every <c>EVOSIM_REPORT_EVERY</c> samples; a stop is not urgent to the second and a
        /// filesystem probe twice a simulated second is.
        /// </para>
        /// </remarks>
        private static int Loop(
            EnvSettings settings, RunConfig config, World world, RunDirectory dir, Report report,
            RunManifest manifest, string outPath, float physicsDt, int stepsPerMetabolic,
            int threads, string resumePath)
        {
            var sim = new Simulation(
                world, settings.Seed, physicsDt, stepsPerMetabolic, threads, dir.Path);

            if (settings.DigestEvery > 0)
            {
                sim.EnableDigest(dir.Path, settings.DigestEvery, settings.DigestDumpSteps);
            }

            var readings = new Readings(sim);
            var sampler = new Sampler { PoolNamed = config.FoundingTricklePoolCount > 0 };

            // Record format 2's genome file is fed from the world's admission queue, which is off
            // unless a run asks for it (a queue nobody drains is a leak). Before the first step and
            // before a restore, so no admission can come before it.
            world.QueueAdmittedGenomes = dir.Genomes != null;

            // The state stream, off unless a launcher asked for it. Opened here rather than in the
            // sampler because its cadence is not the sample's: it takes a frame from the metabolic
            // loop, where the sampler is called once a report row.
            float poseEvery = settings.ResolvePoseEvery();

            PoseRecorder poses = poseEvery > 0f
                ? new PoseRecorder(dir.Path, poseEvery, manifest.ConfigHash)
                : null;

            if (poses != null)
            {
                Console.WriteLine(
                    "state stream: " + Path.Combine(dir.Path, PoseStream.FileName) + " every " +
                    poseEvery.ToString(CultureInfo.InvariantCulture) + " s" +
                    (Math.Abs(poseEvery - settings.PoseEvery) > 1e-6f
                        ? " (raised from the " +
                          settings.PoseEvery.ToString(CultureInfo.InvariantCulture) +
                          " s asked for: nothing moves inside a metabolic step)"
                        : ""));
            }

            Genome inoculum = null;
            bool inoculateOn = !string.IsNullOrEmpty(settings.InoculatePath) && settings.InoculateAt > 0f;

            if (inoculateOn)
            {
                inoculum = GenomeJson.Read(
                    Encoding.UTF8.GetString(File.ReadAllBytes(settings.InoculatePath)));
            }

            bool assayFired = false;

            string ending = null;
            string terminationCode = null;
            string status = "ended";
            int metabolicSteps = 0;
            double bestSpeedEver = 0d;
            double bestSpeedAt = 0d;

            // The world, the harness, the sampler and this loop's own four numbers, all out of
            // one file. Before the first row is written and before the first step is taken, so
            // that everything below runs against the restored world and not against a founded
            // one (logbook/specs/checkpoint-spec.md).
            if (resumePath != null)
            {
                LoopState restored = ReadCheckpoint(resumePath, world, sim, sampler);

                metabolicSteps = restored.MetabolicSteps;
                bestSpeedEver = restored.BestSpeedEver;
                bestSpeedAt = restored.BestSpeedAt;
                assayFired = restored.AssayFired;
            }

            // The bodies this run did not admit itself: a resumed run's inherited roster (and, in a
            // founded one, nobody). Their genomes go into this directory's genomes.jsonl.gz first,
            // so every slim snapshot row this run writes has its genome in its own directory.
            Sampler.WriteLivingGenomes(world, dir);

            float checkpointEvery = settings.ResolveCheckpointEvery();
            int checkpoints = 0;
            double lastCheckpointSeconds = double.NegativeInfinity;
            double deferredCheckpointAt = double.NaN;

            // The next second a checkpoint is due at, taken from the clock rather than counted
            // from the start, so a resumed run's checkpoints land on the same seconds the
            // original's did.
            double nextCheckpointAt = checkpointEvery > 0f
                ? (Math.Floor(world.ElapsedSeconds / checkpointEvery) + 1d) * checkpointEvery
                : double.MaxValue;

            int reportEvery = Math.Max(1, settings.ReportEvery);
            double budgetSeconds = settings.BudgetSeconds;
            double wallMinutes = settings.WallMinutes;
            string stopPath = Path.Combine(dir.Path, "STOP");

            var clock = Stopwatch.StartNew();
            sim.RunClock = clock;

            try
            {
                while (world.ElapsedSeconds < budgetSeconds &&
                       clock.Elapsed.TotalMinutes < wallMinutes)
                {
                    if (!sim.Step()) continue;

                    metabolicSteps++;

                    // The state stream, before anything that can break out of the loop, so the
                    // last frame is the last instant the world was in and not the one before it.
                    if (poses != null)
                    {
                        sim.WritersClock.Start();
                        poses.Sample(sim);
                        sim.WritersClock.Stop();
                    }

                    // So the error path can say how far the run got. All of it, not only the
                    // clock: a manifest that reports zero and one that reports nothing are both
                    // lies, and this one reports the last thing that was true (logbook/0056).
                    manifest.LastSimulatedSeconds = world.ElapsedSeconds;
                    manifest.LastPhysicsSteps = sim.Steps;
                    manifest.LastBirths = world.Births;
                    manifest.LastAlive = world.Living.Count;
                    manifest.LastDragImpulsesLimited = sim.DragImpulsesLimited;
                    manifest.LastDriveImpulsesLimited = sim.DriveImpulsesLimited;
                    manifest.LastDiverged = world.Diverged;
                    manifest.LastMatterInfluxed = world.MatterInfluxedTotal;
                    manifest.LastMatterBuried = world.MatterBuriedTotal;
                    manifest.LastWraps = sim.Wraps;
                    manifest.LastCrowdedTotal = sim.Crowded;
                    manifest.LastContactPairsTotal = readings.ContactPairs;
                    manifest.LastContactPairsJointedTotal = readings.ContactPairsJointed;
                    manifest.LastContactPairsPersistentTotal = readings.ContactPairsPersistent;
                    manifest.LastContactBodiesTotal = readings.ContactBodies;
                    manifest.LastMaxJointMassRatio = sim.MaxJointMassRatio;
                    manifest.LastBodiesOverMassRatio10 = sim.BodiesOverMassRatio10;
                    manifest.LastWallClockMinutes = clock.Elapsed.TotalMinutes;
                    manifest.LastWallPhysicsMs = sim.WallPhysicsMs;
                    manifest.LastWallWorldMs = sim.WallWorldMs;
                    manifest.LastWallHarnessMs = sim.WallHarnessMs;
                    manifest.LastWallWritersMs = sim.WallWritersMs;
                    manifest.LastWallTotalMs = clock.ElapsedMilliseconds;

                    // D060. Fires once — the first metabolic step whose elapsed time reaches the
                    // pre-registered instant — and checked before the extinction test below, so an
                    // assay that lands on an empty world rescues it by design. The inoculum is null
                    // unless inoculateOn, which is the gate ActOnWorld reads.
                    assayFired = ActOnWorld(
                        world, inoculum, settings.InoculateAt, settings.InoculateCount,
                        settings.InoculateDepth, assayFired);

                    // Checked every step, not only at a report row: an empty world would
                    // otherwise sit doing nothing for up to reportEvery more steps. A crash to
                    // zero is a real outcome, so it ends through the same finishing path as a
                    // normal one — one last row first, so the final state is not lost.
                    if (EndsExtinct(world))
                    {
                        ending =
                            "extinct at t=" +
                            world.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                            " s, and the floor could not refill it";
                        terminationCode = "extinct";

                        sim.WritersClock.Start();
                        report.AppendRow(sampler.Write(sim, dir, report.Columns));
                        report.Flush();
                        sim.WritersClock.Stop();

                        break;
                    }

                    // When, not only how much. A best that only ever occurs in the opening
                    // seconds is a transient; one that recurs late is a creature.
                    if (sim.MaxSpeed > bestSpeedEver)
                    {
                        bestSpeedEver = sim.MaxSpeed;
                        bestSpeedAt = world.ElapsedSeconds;
                    }

                    bool stopped = false;

                    if (metabolicSteps % reportEvery == 0)
                    {
                        sim.WritersClock.Start();

                        report.AppendRow(sampler.Write(sim, dir, report.Columns));
                        report.Flush();

                        // Every tenth report: often enough that a killed run keeps something
                        // recent, rare enough that a population of thousands is not serialised
                        // every sample.
                        if (metabolicSteps % (reportEvery * 10) == 0) sampler.Snapshot(dir, world);

                        sim.WritersClock.Stop();

                        stopped = File.Exists(stopPath);
                    }

                    // After the row, never before it. A checkpoint carries the sampler's window
                    // baselines, so one taken before the row that closes a window would hand the
                    // resumed run the same window to write again — the same duplicate row a
                    // restore exists to avoid. It is also after the stop check has been read and
                    // before the loop acts on it, so a stopped arm's last checkpoint is the
                    // instant it stopped at.
                    // Deferred, not skipped, while a body's solver is not on its organism's plan:
                    // such a checkpoint cannot be restored (Simulation.PlanChangesPending). From
                    // round 49 the harness rebuilds a changed plan on the metabolic step that
                    // changed it, so this never fires; it stays as the guard for a plan-changing
                    // path that forgets its rebuild. The next metabolic step asks again.
                    bool checkpointDue = world.ElapsedSeconds + 1e-9 >= nextCheckpointAt;

                    if (checkpointDue && sim.PlanChangesPending() > 0)
                    {
                        if (deferredCheckpointAt != nextCheckpointAt)
                        {
                            deferredCheckpointAt = nextCheckpointAt;

                            Console.Error.WriteLine(
                                "warning: the checkpoint due at " +
                                nextCheckpointAt.ToString("0.#", CultureInfo.InvariantCulture) +
                                " s waits: a body's plan has changed and its solver has not been " +
                                "rebuilt on it, which the harness does on the metabolic step of " +
                                "the change. A plan-changing path is missing its rebuild.");
                        }

                        checkpointDue = false;
                    }

                    if (checkpointDue)
                    {
                        sim.WritersClock.Start();

                        lastCheckpointSeconds = world.ElapsedSeconds;
                        checkpoints++;

                        WriteCheckpoint(
                            sim, sampler, dir, manifest, config, settings, physicsDt,
                            stepsPerMetabolic,
                            metabolicSteps, bestSpeedEver, bestSpeedAt, assayFired);

                        sim.WritersClock.Stop();

                        manifest.LastCheckpoints = checkpoints;

                        nextCheckpointAt =
                            (Math.Floor(world.ElapsedSeconds / checkpointEvery) + 1d) * checkpointEvery;
                    }

                    if (stopped)
                    {
                        string reason = StopReason(stopPath);

                        status = "stopped";
                        terminationCode = reason;
                        ending =
                            "stopped at t=" +
                            world.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                            " s (" + reason + ")";

                        break;
                    }
                }

                // Reached whenever the loop finished without a break — either the budget or the
                // wall. D058: only a budget-complete arm may pass the persistence endpoint, so
                // "wall clock reached" here is what makes a censored arm readable as censored
                // from the footer alone.
                if (terminationCode == null)
                {
                    if (world.ElapsedSeconds >= budgetSeconds)
                    {
                        ending = "budget reached";
                        terminationCode = "budget";
                    }
                    else
                    {
                        ending = "wall clock reached";
                        terminationCode = "wall";
                    }
                }
            }
            catch (PopulationRunawayException runaway)
            {
                // D021: not a crash. It locates the generous end of the calibration exactly as
                // extinction locates the lean end, and culling to fit a compute budget would be
                // selection performed by us. D058 files this the same as a wall cut: censored,
                // never a pass.
                ending =
                    "RUNAWAY at t=" +
                    runaway.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                    " s with " + runaway.Population.ToString(CultureInfo.InvariantCulture) +
                    " alive and " +
                    runaway.TissueJoules.ToString("0", CultureInfo.InvariantCulture) +
                    " J of standing tissue, over the " + runaway.Ceiling + " ceiling — light is " +
                    "covering upkeep so completely that nothing has to do anything";

                terminationCode = "ceiling-" + runaway.Ceiling;
                status = "ended";
            }
            catch (Exception e)
            {
                clock.Stop();

                // The stream first: disposing it writes poses.idx, so even a crashed run leaves
                // an index rather than a file only a scan can open.
                manifest.LastPoseFrames = poses?.FrameCount ?? 0;
                poses?.Dispose();

                Manifest.Write(dir, manifest, Manifest.ErrorEnding(manifest, e));

                report.AppendLine();
                report.AppendLine("**Ended:** " + e.GetType().Name + ": " + e.Message + ".");
                report.Flush();

                sampler.Close();
                sim.Dispose();
                dir.Dispose();

                Console.Error.WriteLine(e.ToString());
                return 1;
            }

            clock.Stop();

            report.Footer(
                config, readings, ending, clock.ElapsedMilliseconds, sim.WallWritersMs,
                bestSpeedEver, bestSpeedAt);

            report.Flush();

            // The last window's lineage rows. They are drained at every sample and nowhere else,
            // so a run that ended between samples would otherwise put its final births in the
            // snapshot and never in lineage.jsonl (r25-s2's wall end left 38 snapshot ids with no
            // birth row).
            IReadOnlyList<LineageEvent> lineageTail = world.DrainLineageEvents();
            for (int i = 0; i < lineageTail.Count; i++) dir.Lineage.Write(lineageTail[i].ToJson());
            Sampler.DrainGenomes(world, dir);

            // One last checkpoint at the second the run actually stopped at, unless the cadence
            // already wrote one there. A run stopped or walled between two cadence seconds is
            // exactly the case a resume is for, and without this it would resume from the last
            // round number and re-simulate everything after it.
            //
            // Not while a body's solver is off its organism's plan: a checkpoint then cannot be
            // restored (Simulation.PlanChangesPending), and it would be the newest file, the one a
            // resume picks by default. From round 49 no body is, since the harness rebuilds on
            // the metabolic step of the change; the test stays as the guard, and when it fires
            // the last cadence checkpoint stands instead and the run says so.
            if (checkpointEvery > 0f && world.Living.Count > 0 &&
                world.ElapsedSeconds > lastCheckpointSeconds + 1e-9)
            {
                int pending = sim.PlanChangesPending();

                if (pending > 0)
                {
                    Console.Error.WriteLine(
                        "note: no checkpoint at the second the run ended (" +
                        world.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                        " s): " + pending.ToString(CultureInfo.InvariantCulture) + " bodies had " +
                        "changed plan without their solver being rebuilt, and a checkpoint of that " +
                        "moment cannot be restored. A resume reads the last cadence checkpoint " +
                        "instead.");
                }
                else
                {
                    checkpoints++;

                    WriteCheckpoint(
                        sim, sampler, dir, manifest, config, settings, physicsDt, stepsPerMetabolic,
                        metabolicSteps, bestSpeedEver, bestSpeedAt, assayFired);
                }
            }

            manifest.LastCheckpoints = checkpoints;

            sampler.Snapshot(dir, world);
            sampler.Close();

            // Closing the stream writes poses.idx, so the frame count is read before the close
            // and the index exists before run.json claims a count for it.
            int poseFrames = poses?.FrameCount ?? 0;
            manifest.LastPoseFrames = poseFrames;
            poses?.Dispose();

            long[] harnessPhaseMs = sim.HarnessPhaseMs();

            manifest.LastSimulatedSeconds = world.ElapsedSeconds;

            Manifest.Write(dir, manifest, new RunEnding
            {
                Status = status,
                Reason = terminationCode,
                Prose = ending,
                SimulatedSeconds = world.ElapsedSeconds,
                PhysicsSteps = sim.Steps,
                Births = world.Births,
                Alive = world.Living.Count,
                WallClockMinutes = clock.Elapsed.TotalMinutes,
                TimesRealTime =
                    world.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds),
                DragImpulsesLimited = sim.DragImpulsesLimited,
                DriveImpulsesLimited = sim.DriveImpulsesLimited,
                DivergedTotal = world.Diverged,
                MatterInfluxedTotal = world.MatterInfluxedTotal,
                MatterBuriedTotal = world.MatterBuriedTotal,
                BestSpeed = bestSpeedEver,
                BestSpeedAtSeconds = bestSpeedAt,
                SharedSpace = config.SharedSpace,
                Wraps = sim.Wraps,
                Crowded = sim.Crowded,
                ContactPairsPerStep =
                    sim.Steps > 0 ? readings.ContactPairs / (double)sim.Steps : 0d,
                ContactPairsJointed = readings.ContactPairsJointed,
                ContactPairsPersistent = readings.ContactPairsPersistent,
                ContactBodies = readings.ContactBodies,
                MaxJointMassRatio = sim.MaxJointMassRatio,
                BodiesOverMassRatio10 = sim.BodiesOverMassRatio10,
                WallPhysicsMs = sim.WallPhysicsMs,
                WallWorldMs = sim.WallWorldMs,
                WallHarnessMs = sim.WallHarnessMs,
                WallWritersMs = sim.WallWritersMs,
                WallTotalMs = clock.ElapsedMilliseconds,
                WallHarnessPhaseMs = harnessPhaseMs,
                HarnessPhases = Simulation.HarnessPhases,
                HarnessBodySteps = sim.HarnessBodySteps,
                WallFluidGatherMs = 0L,
                WallFluidWaterMs = 0L,
                WallFluidComputeMs = 0L,
                WallFluidApplyMs = 0L,
                FluidLinkSteps = sim.FluidLinkSteps,
                PoseFrames = poseFrames,
                Checkpoints = checkpoints,
            });

            report.GenomesLine(dir.Path);
            report.Flush();

            sim.Dispose();
            dir.Dispose();

            Console.WriteLine();
            Console.WriteLine("**Ended:** " + ending + ".");

            return 0;
        }

        /// <summary>
        /// A resumed run keeps the cadences and the record the run it continues was recording at,
        /// except where this launcher named one itself.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The world comes out of the source run's <c>config.json</c> and is therefore the same
        /// world whatever the launcher said. The cadences are not in the config — they are
        /// recording settings and move no hash — so without this they would fall back to their
        /// defaults, and a continuation would write its rows on other seconds than the run it
        /// continues. The acceptance is a row-for-row comparison of the two, and a cadence that
        /// did not carry fails it for a reason that has nothing to do with the world.
        /// </para>
        /// <para>
        /// The record format carries for the same reason (<see cref="SourceRecordFormat"/>): a
        /// continuation of a format 1 run written in format 2 would be a directory no reader
        /// written against its source could follow, and its checkpoints would change version
        /// between two files of one run.
        /// </para>
        /// <para>
        /// A launcher that names one wins, because <see cref="EnvSettings.Provided"/> records
        /// which names were actually set rather than which values differ from a default. Resuming
        /// at a finer cadence to watch something closely is a real thing to want, and so is
        /// resuming an old run into the compact record.
        /// </para>
        /// </remarks>
        private static void InheritRecording(EnvSettings settings, CheckpointHeader resume, string sourceRun)
        {
            if (!settings.Provided.Contains("EVOSIM_RECORD_FORMAT"))
            {
                settings.RecordFormat = SourceRecordFormat(sourceRun, resume);
            }

            if (!settings.Provided.Contains("EVOSIM_REPORT_EVERY") && resume.ReportEvery > 0)
            {
                settings.ReportEvery = resume.ReportEvery;
            }

            if (!settings.Provided.Contains("EVOSIM_POSE_EVERY") && resume.PoseEverySeconds > 0d)
            {
                settings.PoseEvery = (float)resume.PoseEverySeconds;
            }

            if (!settings.Provided.Contains("EVOSIM_DIGEST_EVERY") && resume.DigestEverySteps > 0L)
            {
                settings.DigestEvery = resume.DigestEverySteps;
            }

            if (!settings.Provided.Contains("EVOSIM_CHECKPOINT_EVERY") &&
                resume.CheckpointEverySeconds > 0d)
            {
                settings.CheckpointEvery = (float)resume.CheckpointEverySeconds;
            }
        }

        /// <summary>
        /// The record the source run wrote: its <c>run.json</c>'s <c>recordFormat</c>, format 1
        /// when the manifest names none, and, when there is no manifest to ask, format 1 for a
        /// version-4 checkpoint and the files the directory holds for a version-6 one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The manifest first, because it is where the record is recorded and what every reader
        /// dispatches on. A manifest without the field was written before record format 2 existed,
        /// so its run is format 1. A manifest this build cannot read is a refusal, under the rule
        /// that a resume never guesses at its source.
        /// </para>
        /// <para>
        /// The checkpoint's version stands in only when the source directory has lost its
        /// manifest. Every version-4 checkpoint was written before record format 2 existed, so it
        /// is format 1 without a guess. Both records write version 6 (<c>WriteCheckpoint</c>), so
        /// for one of those the directory's own files decide
        /// (<see cref="RecordFiles.FormatOf(string, out string)"/>: the converter's mark, or
        /// <c>genomes.jsonl.gz</c> or <c>positions.jsonl.gz</c> present), and a directory holding
        /// neither is format 1.
        /// </para>
        /// </remarks>
        public static int SourceRecordFormat(string sourceRun, CheckpointHeader resume)
        {
            string manifestPath = sourceRun != null ? Path.Combine(sourceRun, "run.json") : null;

            if (manifestPath != null && File.Exists(manifestPath))
            {
                JsonNode manifest = Json.Parse(File.ReadAllText(manifestPath));

                if (!manifest.Has("recordFormat")) return RunRecordFormat.Jsonl;

                JsonNode field = manifest["recordFormat"];
                int format = field.Kind == JsonNode.NodeKind.Number ? field.AsInt() : -1;

                if (!RunRecordFormat.IsKnown(format))
                {
                    throw new InvalidDataException(
                        manifestPath + " says recordFormat " + field + ", and this build writes " +
                        "records 1 and 2. Name EVOSIM_RECORD_FORMAT to resume it anyway.");
                }

                return format;
            }

            if (resume != null && resume.Version == Checkpoint.LossyVersion) return RunRecordFormat.Jsonl;

            return sourceRun != null && Directory.Exists(sourceRun)
                ? RecordFiles.FormatOf(sourceRun)
                : RunRecordFormat.Jsonl;
        }

        /// <summary>
        /// Writes one checkpoint: the world, the harness, the sampler and the loop's own four
        /// numbers, at the second the caller is standing on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two things happen before a byte is written. The harness is settled, because a
        /// checkpoint taken mid-reconciliation would carry a world that holds creatures the solver
        /// has no body for; <c>Reconcile</c> is idempotent, so settling a settled harness costs a
        /// walk of the roster and changes nothing.
        /// </para>
        /// <para>
        /// And the queued lineage rows are drained. They are otherwise drained at a sample and
        /// nowhere else, so a checkpoint between samples would carry births in its state that the
        /// original run had already written to <c>lineage.jsonl</c> and the resumed run would
        /// write again. Draining here puts them in the file on the same side of the line the
        /// checkpoint draws, and the resumed run's first rows are the ones that come after it.
        /// </para>
        /// </remarks>
        private static void WriteCheckpoint(
            Simulation sim, Sampler sampler, RunDirectory dir, RunManifest manifest,
            RunConfig config, EnvSettings settings, float physicsDt, int stepsPerMetabolic,
            int metabolicSteps, double bestSpeedEver, double bestSpeedAt, bool assayFired)
        {
            World world = sim.World;

            sim.SettleBeforeCheckpoint();

            IReadOnlyList<LineageEvent> queued = world.DrainLineageEvents();
            for (int i = 0; i < queued.Count; i++) dir.Lineage.Write(queued[i].ToJson());

            // The genome queue with it, for the same reason: it is not in the checkpoint, so what
            // it holds belongs on this side of the line.
            Sampler.DrainGenomes(world, dir);

            var header = new CheckpointHeader
            {
                // Version 6 in either record: the payload is the world's layout, which has one
                // writer, gzipped after it is digested. Record format 1 keeps the old run files;
                // it cannot keep the old checkpoint, whose layout (version 4) lacks the contact
                // record and is only read.
                Version = Checkpoint.Version,
                Seconds = world.ElapsedSeconds,
                Seed = manifest.Seed,
                PhysicsSteps = sim.Steps,
                PhysicsStepSeconds = physicsDt,
                StepsPerMetabolicStep = stepsPerMetabolic,
                ConfigHash = config.Hash(),
                CoreHash = manifest.CoreHash,
                DynamicsHash = manifest.DynamicsHash,
                FarmHash = manifest.FarmHash,
                EngineVersion = manifest.EngineVersion,
                SourceArm = manifest.ArmName,
                SourceRun = Path.GetFileName(dir.Path),
                ReportEvery = Math.Max(1, settings.ReportEvery),
                PoseEverySeconds = settings.ResolvePoseEvery(),
                DigestEverySteps = settings.DigestEvery,
                CheckpointEverySeconds = settings.ResolveCheckpointEvery(),
            };

            CheckpointWriter.Write(
                Checkpoint.PathFor(dir.Path, world.ElapsedSeconds),
                header,
                w =>
                {
                    StateIo.Tag(w, "PAYL");
                    world.WriteState(w);
                    sim.WriteState(w);
                    sampler.WriteState(w);

                    StateIo.Tag(w, "LOOP");
                    w.Write(metabolicSteps);
                    w.Write(bestSpeedEver);
                    w.Write(bestSpeedAt);
                    w.Write(assayFired);

                    StateIo.Tag(w, "PEND");
                });
        }

        /// <summary>The loop's own four numbers, which a checkpoint carries under <c>LOOP</c>.</summary>
        public struct LoopState
        {
            public int MetabolicSteps;
            public double BestSpeedEver;
            public double BestSpeedAt;
            public bool AssayFired;
        }

        /// <summary>
        /// Puts the world, the harness, the sampler and the loop's four numbers back out of one
        /// checkpoint: <see cref="WriteCheckpoint"/> read backwards.
        /// </summary>
        /// <remarks>
        /// Shared by a resume and by <see cref="FilmWindow"/>, so the two read one layout and a
        /// section added to the writer is added to one reader.
        /// </remarks>
        internal static LoopState ReadCheckpoint(
            string path, World world, Simulation sim, Sampler sampler)
        {
            var restored = new LoopState();

            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                System.IO.BinaryReader r = reader.Reader;

                StateIo.Tag(r, "PAYL");
                world.ReadState(r);
                sim.ReadState(r);
                sampler.ReadState(r);

                StateIo.Tag(r, "LOOP");
                restored.MetabolicSteps = r.ReadInt32();
                restored.BestSpeedEver = r.ReadDouble();
                restored.BestSpeedAt = r.ReadDouble();
                restored.AssayFired = r.ReadBoolean();

                StateIo.Tag(r, "PEND");
            }

            return restored;
        }

        /// <summary>
        /// What the loop does to the world at a metabolic step beyond stepping it: D060's assay,
        /// fired once at the first step whose second reaches the pre-registered instant.
        /// </summary>
        /// <returns>Whether the assay has fired, this step or before.</returns>
        /// <remarks>
        /// <b>One method, called by the loop and by <see cref="FilmWindow"/></b>, so that a window
        /// replays every change the run made to its world and not only the ones
        /// <see cref="Simulation.Step"/> makes. Anything new the loop does that writes to the world
        /// belongs here; left in the loop, it would make every film window of a run that has it
        /// part from the run at the first step it acts on. A null inoculum is the assay switched
        /// off.
        /// </remarks>
        internal static bool ActOnWorld(
            World world, Genome inoculum, float inoculateAt, int inoculateCount,
            float inoculateDepth, bool assayFired)
        {
            if (inoculum != null && inoculateAt > 0f && !assayFired &&
                world.ElapsedSeconds >= inoculateAt)
            {
                world.Inoculate(inoculum, inoculateCount, -inoculateDepth);
                assayFired = true;
            }

            return assayFired;
        }

        /// <summary>The stop file's first line, or a word that says it had none.</summary>
        private static string StopReason(string path)
        {
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) return trimmed;
                }
            }
            catch (IOException)
            {
            }

            return "manual-other";
        }

        private static void Help()
        {
            Console.WriteLine(
                "Evosim.Farm — one evolution arm, stepped by Evosim.Dynamics rather than by Unity.");
            Console.WriteLine();
            Console.WriteLine("  Evosim.Farm [EVOSIM_NAME=value ...]");
            Console.WriteLine();
            Console.WriteLine(
                "Every setting is an environment variable, as it was under the editor; an");
            Console.WriteLine(
                "argument of the same form overrides one. The settings this build reads:");
            Console.WriteLine();

            IReadOnlyList<string> names = EnvBinding.Names;
            for (int i = 0; i < names.Count; i++) Console.WriteLine("  " + names[i]);
        }
    }
}
