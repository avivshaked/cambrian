using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
                FarmConsole.Error.WriteLine(e.GetType().Name + ": " + e.Message);
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

            // --mirror-census <run dir> <seconds> <out file>: each mirrored part's joint against its
            // twin's, in the solver and in development (MirrorCensus).
            if (args.Length >= 1 && args[0] == "--mirror-census")
            {
                return MirrorCensus.Run(args);
            }

            // Two worlds in one process on one card: --pair <a.args> <b.args>; or two or more, on the
            // card or on the CPU engine: --group <a.args> <b.args> [<c.args> ...]. Group says what it
            // reads and where each world writes.
            if (args.Length >= 1 && args[0] == "--pair")
            {
                if (args.Length != 3)
                {
                    FarmConsole.Error.WriteLine("--pair takes two settings files: --pair <a.args> <b.args>.");
                    return 1;
                }

                return Group(new[] { args[1], args[2] }, pair: true);
            }

            if (args.Length >= 1 && args[0] == "--group")
            {
                if (args.Length < 3)
                {
                    FarmConsole.Error.WriteLine("--group takes two settings files or more: --group <a.args> <b.args> [<c.args> ...].");
                    return 1;
                }

                var paths = new string[args.Length - 1];
                Array.Copy(args, 1, paths, 0, paths.Length);
                return Group(paths, pair: false);
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
                    FarmConsole.Error.WriteLine(
                        "'" + arg + "' is not a setting. Arguments are EVOSIM_NAME=value pairs; " +
                        "everything else is refused rather than ignored.");
                    return 1;
                }

                overrides[arg.Substring(0, eq)] = arg.Substring(eq + 1);
            }

            return RunWorld(overrides, null, null, null);
        }

        /// <summary>
        /// One world, from the environment with <paramref name="overrides"/> over it, to its end.
        /// </summary>
        /// <param name="sharedDevice">
        /// The device a group on the card shares (<c>GpuDevice</c>, typed loosely because the engine is
        /// compiled only where it is), or null for a world that opens its own as it always did.
        /// </param>
        /// <param name="pairedWith">The other worlds' reports, recorded in the gpu block; null alone.</param>
        /// <param name="group">Every world's report in the group, in its order, recorded in run.json; null alone.</param>
        private static int RunWorld(
            Dictionary<string, string> overrides, object sharedDevice, string pairedWith, string group,
            GroupExchange exchange = null, int place = -1)
        {
            EnvBinding.Lookup env = name =>
                overrides.TryGetValue(name, out string v) ? v : Environment.GetEnvironmentVariable(name);

            EnvSettings settings = EnvBinding.Read(env);

            // D159: the exchange joins the worlds of one --group, which builds it; a lone run has
            // no neighbour, and a setting that would silently do nothing is refused instead.
            if (settings.MigrateEvery > 0f && exchange == null)
            {
                throw new ArgumentException(
                    "EVOSIM_MIGRATE_EVERY is " + settings.MigrateEvery.ToString(CultureInfo.InvariantCulture) +
                    " and this is a lone run. The exchange trades genomes between the worlds of one --group; " +
                    "launch them together, or unset it.");
            }

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
                FarmConsole.Error.WriteLine(
                    "warning: " + string.Join(", ", unknown) +
                    " is set and is not a setting this build reads — ignored, exactly as the " +
                    "Unity entry ignored it.");
            }

            if (settings.RequestedJobWorkers != 0)
            {
                FarmConsole.Error.WriteLine(
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
                FarmConsole.Error.WriteLine(
                    "warning: EVOSIM_INOCULATE_AT is set but EVOSIM_INOCULATE names no genome " +
                    "file — the assay will not fire.");
            }

            if (inoculumHash != null && settings.InoculateAt <= 0f)
            {
                FarmConsole.Error.WriteLine(
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
            RunConfig sourceConfig = null;
            IReadOnlyList<string> cousinChanges = null;
            TricklePoolFiles.Pool pool = null;

            if (resumePath != null)
            {
                resume = CheckpointReader.ReadHeader(resumePath);
                resumeSourceRun = Path.GetDirectoryName(Path.GetDirectoryName(resumePath));

                config = RunDirectory.ReadConfig(resumeSourceRun, out string configEdited);

                if (configEdited != null)
                {
                    FarmConsole.Error.WriteLine(
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
                    FarmConsole.Error.WriteLine(
                        "warning: EVOSIM_TRICKLE_POOL is set on a resume and is ignored: the pool " +
                        "is the resumed run's own, from " +
                        Path.Combine(resumeSourceRun, TricklePoolFiles.DirectoryName) + ".");
                }

                // D201: a cousin carries on under an edited copy of the source's config, refused
                // here, before a directory is made, if it changes anything but the dials a screen
                // turns. The checkpoint is checked against the source's config, which wrote it.
                if (!string.IsNullOrEmpty(settings.ResumeConfig) || settings.ResumeTrickleOff)
                {
                    RunConfig cousin = CousinConfig.Build(
                        config, settings.ResumeConfig, settings.ResumeTrickleOff,
                        out IReadOnlyList<string> changes);

                    // Unchanged, the copy read from the file is still the one stepped, so the test
                    // that it resumes bit-identical proves the file's round trip as well.
                    if (changes.Count > 0)
                    {
                        sourceConfig = config;
                        cousinChanges = changes;
                    }

                    config = cousin;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(settings.ResumeConfig) || settings.ResumeTrickleOff)
                {
                    throw new ArgumentException(
                        "EVOSIM_RESUME_CONFIG and EVOSIM_RESUME_TRICKLE_OFF change a resumed world " +
                        "and need EVOSIM_RESUME; a founded world takes its settings from the launcher.");
                }

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

            // D159: a resumed world trades as the run it continues traded, at its place in the same
            // ring, or it is another world from the checkpoint on.
            if (resumeSourceRun != null) RefuseAnotherExchange(resumeSourceRun, exchange, place);

            // D159's species rule (an immigrant keeps its home species, tagged with the world it
            // came from) is not built, and World.Immigrate refuses a world with species on. Asked
            // here, before a directory is made, rather than at the first exchange hours in.
            if (exchange != null && config.SpeciesDriftThreshold > 0f)
            {
                throw new ArgumentException(
                    "This world runs species accounting (SpeciesDriftThreshold " +
                    config.SpeciesDriftThreshold.ToString(CultureInfo.InvariantCulture) + ") in a migrating group. " +
                    "D159's ruling is that an immigrant keeps its home species, tagged with the world it came " +
                    "from, and this build has no tagged species path. Run the exchange with species off.");
            }

            // Record format 2 retires poses.jsonl for the state stream at the report interval,
            // unless the launcher named a cadence. After InheritRecording, so a resumed run keeps
            // the stream its source was writing (logbook/specs/record-and-film-spec.md A4).
            bool streamByDefault = settings.ApplyRecordDefaults(config.SharedSpace);

            float physicsDt = resume != null
                ? resume.PhysicsStepSeconds
                : settings.PhysicsDt;

            // D239. The factor is the config's: a resumed world's own, never the launcher's, so a
            // resume cannot step at another pace than the checkpoint was written at.
            physicsDt = EnvBinding.ResolvePhysicsStep(physicsDt, config.PaceFactor, out int stepsPerMetabolic);

            if (resume != null && resume.StepsPerMetabolicStep != stepsPerMetabolic)
            {
                throw new ArgumentException(
                    "The checkpoint was written at " + resume.StepsPerMetabolicStep.ToString(CultureInfo.InvariantCulture) +
                    " physics steps a metabolic step (dt " + resume.PhysicsStepSeconds.ToString(CultureInfo.InvariantCulture) +
                    " s), and this world's pace factor of " + config.PaceFactor.ToString(CultureInfo.InvariantCulture) +
                    " gives " + stepsPerMetabolic.ToString(CultureInfo.InvariantCulture) + ". A world is resumed at the pace factor " +
                    "it was written at (EVOSIM_PACE_FACTOR is part of the config hash).");
            }

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
            var world = new World(config, settings.Seed, pool?.Genomes, pool?.Weights);

            // The engine, before the directory, for the same reason as the world: a gpu world the
            // engine refuses (a box, a water hold, a dt it was not written for) or a device it
            // cannot find stops the launch before a run directory is made for it
            // (logbook/specs/gpu-port-spec.md section 3). Its solver config is the one the
            // simulation will build, from the same factory and the same world.
            Evosim.Dynamics.IStepBackend engine = null;
            string engineToken = null;

            if (!settings.EngineIsGpu && settings.Engine != "cpu")
            {
                throw new ArgumentException(
                    "EVOSIM_ENGINE is '" + settings.Engine + "'; the farm knows 'cpu' (the solver) and 'gpu'.");
            }

            if (settings.EngineIsGpu)
            {
#if EVOSIM_GPU
                Evosim.Farm.Gpu.GpuBackend gpu = FarmGpu.Open(
                    settings, Evosim.Dynamics.SolverConfig.FromWorld(config, physicsDt, world.Bed, world),
                    config.Shapes, sharedDevice as Evosim.Farm.Gpu.GpuDevice);
                engine = gpu;
                engineToken = gpu.HeaderToken();

                // The scent's fill on the same card, with the host's sums (GpuScent); the world
                // hands its corpses over where it would have filled.
                if (gpu.Options.Scent) gpu.FillScentOnCard(world);

                // The snow's transport on the same card, with the CPU's bits; the grid keeps its
                // decisions (GpuTransport).
                if (gpu.Options.Transport) OfferTransport(world, config, gpu.CreateTransport());
#else
                throw new NotSupportedException(
                    "EVOSIM_ENGINE is gpu and this build of the farm has no gpu engine: it is compiled " +
                    "in by src/Evosim.Farm's own csproj (EVOSIM_GPU), and never into Unity.");
#endif
            }
            else if (settings.GpuTransport)
            {
#if EVOSIM_GPU
                // The bodies on the CPU and the snow's transport on the card, on a context of its
                // own: the same bits as the CPU's transport, sooner.
                OfferTransport(world, config, Evosim.Farm.Gpu.GpuTransport.OpenOnCard());
                engineToken = RunManifest.EngineName + " transport";
#else
                throw new NotSupportedException(
                    "EVOSIM_GPU_TRANSPORT puts the snow's transport on the card, and this build of the " +
                    "farm has no gpu code: it is compiled in by src/Evosim.Farm's own csproj (EVOSIM_GPU).");
#endif
            }

            // The kill test's hook, named on the engine line wherever it is set, so a run paused by it
            // cannot pass for an ordinary one in its header or its console (card-next-r57.md Part 8).
            if (settings.TestCheckpointStallMs > 0)
            {
                engineToken = (engineToken ?? RunManifest.EngineName) +
                              " TEST-HOOK checkpoint stall " + settings.TestCheckpointStallMs + " ms";
            }

            // The thread pool's spin (card-next-r57.md Part 7): the runtime reads it from this process's own
            // environment at start, and it moves the pace and never a number. Named on the engine line, and
            // so in the report's header, and in run.json, wherever it is set, so a pace read names its spin.
            string spinLimit = Environment.GetEnvironmentVariable("DOTNET_ThreadPool_UnfairSemaphoreSpinLimit");
            if (string.IsNullOrEmpty(spinLimit)) spinLimit = null;
            if (spinLimit != null) engineToken = (engineToken ?? RunManifest.EngineName) + " spin " + spinLimit;

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
            if (pool != null) TricklePoolFiles.Write(dir.Path, pool.Lines, pool.IndexText);

            RunManifest manifest = Manifest.Build(
                settings, config.Hash(), inoculumHash, physicsDt, stepsPerMetabolic, threads, config.PaceFactor);

            Manifest.RecordBed(manifest, world.Bed);
            manifest.TestCheckpointStallMs = settings.TestCheckpointStallMs;
            manifest.ThreadPoolSpinLimit = spinLimit;
            manifest.Group = group;

            if (exchange != null)
            {
                manifest.MigrateEverySeconds = exchange.Every;
                manifest.MigrateCount = exchange.Count;
                manifest.GroupPlace = place;
                manifest.MigrateTo = exchange.Name(exchange.To(place));
                manifest.MigrateFrom = exchange.Name(exchange.From(place));
            }

#if EVOSIM_GPU
            if (engine is Evosim.Farm.Gpu.GpuBackend opened)
            {
                manifest.Engine = "gpu";
                manifest.Gpu = FarmGpu.Facts(opened, manifest.RepoRoot);
                manifest.Gpu.SharedProcessWith = pairedWith;
            }
#endif
            Manifest.RecordReefs(manifest, world.Reefs);
            Manifest.RecordCoral(manifest, world.Coral);

            if (settings.GpuTransport) manifest.Transport = "card";

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
                RecordResume(
                    manifest, settings, resume, resumePath, resumeSourceRun, sourceConfig ?? config,
                    sourceConfig != null ? config : null, cousinChanges);
            }

            Manifest.Write(dir, manifest, ending: null);

            var report = new Report(outPath, config, migrating: exchange != null);
            report.Begin(settings, config, space, manifest.EngineVersion, engineToken, world);
            report.Flush();

            FarmConsole.Out.WriteLine(report.Text.TrimEnd());
            FarmConsole.Out.WriteLine();
            FarmConsole.Out.WriteLine("run directory: " + dir.Path);
            FarmConsole.Out.WriteLine("report:        " + outPath);
            FarmConsole.Out.WriteLine(
                "record:        " + RunRecordFormat.Describe(settings.RecordFormat) +
                (resume != null && !settings.Provided.Contains("EVOSIM_RECORD_FORMAT")
                    ? ", the resumed run's own"
                    : "") +
                (streamByDefault
                    ? "; the state stream in place of poses.jsonl, every " +
                      settings.PoseEvery.ToString(CultureInfo.InvariantCulture) + " s (the report interval)"
                    : ""));
            FarmConsole.Out.WriteLine(
                "threads:       " + threads.ToString(CultureInfo.InvariantCulture) +
                "   dt " + physicsDt.ToString(CultureInfo.InvariantCulture) +
                " s, metabolic step " +
                (config.PaceFactor == 1f
                    ? stepsPerMetabolic * physicsDt
                    : EnvBinding.MetabolicStepSeconds).ToString(CultureInfo.InvariantCulture) + " s" +
                (config.PaceFactor == 1f
                    ? ""
                    : ", pace factor " + config.PaceFactor.ToString(CultureInfo.InvariantCulture) + " (" +
                      stepsPerMetabolic.ToString(CultureInfo.InvariantCulture) + " physics steps a metabolic step)"));

            if (resume != null)
            {
                FarmConsole.Out.WriteLine(
                    "resumed from:  " + resumePath + " at " +
                    resume.Seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s" +
                    (manifest.ResumedWithSourceMismatch ? "  (SOURCE MISMATCH)" : "") +
                    (manifest.ResumedAsCousin ? "  (COUSIN of config " + manifest.ResumeSourceConfigHash + ")" : ""));
            }

            if (manifest.CheckpointEverySeconds > 0d)
            {
                FarmConsole.Out.WriteLine(
                    "checkpoints:   " + Checkpoint.DirectoryIn(dir.Path) + " every " +
                    manifest.CheckpointEverySeconds.ToString(CultureInfo.InvariantCulture) + " s");
            }

            if (engineToken != null) FarmConsole.Out.WriteLine("engine:        " + engineToken);

            if (exchange != null)
            {
                FarmConsole.Out.WriteLine(
                    "exchange:      every " + exchange.Every.ToString(CultureInfo.InvariantCulture) + " s, " +
                    exchange.Count.ToString(CultureInfo.InvariantCulture) + " to " + manifest.MigrateTo +
                    ", from " + manifest.MigrateFrom + " (place " + place.ToString(CultureInfo.InvariantCulture) +
                    " of " + exchange.Worlds.ToString(CultureInfo.InvariantCulture) + ")");
            }

            return Loop(settings, config, world, dir, report, manifest, outPath,
                physicsDt, stepsPerMetabolic, threads, resumePath, engine, exchange, place);
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
            string sourceRun, RunConfig config, RunConfig cousin, IReadOnlyList<string> cousinChanges)
        {
            // D201: a cousin's own config hash is the manifest's, and its source's is kept beside
            // it with every field that changed; the checkpoint is checked below against the
            // source's, which is the config that wrote it.
            if (cousin != null)
            {
                manifest.ResumedAsCousin = true;
                manifest.ResumeSourceConfigHash = config.Hash();
                manifest.ResumeCousinChanges = new List<string>(cousinChanges);

                FarmConsole.Error.WriteLine(
                    "resuming as a cousin of " + Path.GetFileName(sourceRun) + " (config " +
                    config.Hash() + " -> " + cousin.Hash() + "): " + string.Join("; ", cousinChanges) + ".");
            }

            // The directory's name, not its path: a run is named for the instant it started and
            // its settings hash, and that name joins to the arm's own directory wherever the tree
            // has since been moved to.
            manifest.ResumedFromArm = resume.SourceArm;
            manifest.ResumedFromRun = Path.GetFileName(sourceRun);
            manifest.ResumedFromSeconds = resume.Seconds;
            manifest.ResumedFromCheckpointHash = Manifest.HashBytes(File.ReadAllBytes(resumePath));

            var differences = new List<string>(resume.Differences(
                config.Hash(), manifest.CoreHash, manifest.DynamicsHash, manifest.FarmHash));

            // Across engines, as across hashes (logbook/specs/gpu-port-spec.md section 5): the
            // checkpoint is the same file whichever engine wrote it, and what carries on from it
            // on another engine, or at another precision, is a cousin of the recording.
            string sourceEngine = EngineOf(sourceRun);
            string thisEngine = EngineOf(manifest);
            if (sourceEngine != null && sourceEngine != thisEngine)
            {
                differences.Add("engine " + sourceEngine + " against this run's " + thisEngine);
            }

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

            FarmConsole.Error.WriteLine(
                "warning: resuming across a source mismatch — " + note + ". What this run " +
                "produces is a cousin of the recording and not its continuation, and run.json " +
                "says so.");
        }

        /// <summary>
        /// D159: a resume carries on the exchange its source run had, or none if it had none — the
        /// same cadence, the same count and the same place in the group — and is refused otherwise.
        /// </summary>
        /// <remarks>
        /// The settings are the farm's and not the world's, so the config hash cannot see them; the
        /// source's run.json is where they are recorded. A world resumed alone from a migrating run
        /// would plant nothing at its next exchange, and one resumed at another place would name its
        /// immigrants' home worlds by other numbers, so both are refused rather than run as cousins.
        /// </remarks>
        private static void RefuseAnotherExchange(string sourceRun, GroupExchange exchange, int place)
        {
            JsonNode source = Json.Parse(File.ReadAllText(Path.Combine(sourceRun, "run.json")));
            bool sourceMigrated = source.Has("migrateEverySeconds");

            if (!sourceMigrated && exchange == null) return;

            string was = sourceMigrated
                ? "every " + source["migrateEverySeconds"].AsDouble().ToString("R", CultureInfo.InvariantCulture) + " s, " +
                  source["migrateCount"].AsInt().ToString(CultureInfo.InvariantCulture) + " a time, at place " +
                  source["groupPlace"].AsInt().ToString(CultureInfo.InvariantCulture)
                : "no exchange";

            string now = exchange != null
                ? "every " + exchange.Every.ToString("R", CultureInfo.InvariantCulture) + " s, " +
                  exchange.Count.ToString(CultureInfo.InvariantCulture) + " a time, at place " +
                  place.ToString(CultureInfo.InvariantCulture)
                : "no exchange";

            if (was == now) return;

            throw new InvalidOperationException(
                "The run this resumes, " + sourceRun + ", traded genomes " + was + ", and this launch trades " + now +
                " (D159). A resume carries on the source's exchange: launch the same group, in the same order, " +
                "with the same EVOSIM_MIGRATE_EVERY and EVOSIM_MIGRATE_COUNT.");
        }

        /// <summary>The engine a run directory's run.json names, with the gpu's precision; null when unreadable.</summary>
        private static string EngineOf(string runDirectory)
        {
            try
            {
                string path = Path.Combine(runDirectory, "run.json");
                if (!File.Exists(path)) return null;

                JsonNode node = Json.Parse(File.ReadAllText(path));
                if (!node.Has("engine")) return null;

                string engine = node["engine"].AsString();
                if (node.Has("gpu") && node["gpu"].Has("precision"))
                {
                    engine += " " + node["gpu"]["precision"].AsString();
                }

                return engine;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string EngineOf(RunManifest manifest) =>
            (manifest.Engine ?? RunManifest.EngineName) + (manifest.Gpu != null ? " " + manifest.Gpu.Precision : "");

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
#if EVOSIM_GPU
        // The device on the snow's grid, or the launch refused: a run that asked for the card and
        // would quietly carry its snow on the CPU is a pace reading of the wrong thing.
        private static void OfferTransport(World world, RunConfig config, Evosim.Farm.Gpu.GpuTransport device)
        {
            if (world.Nutrients is GridField snow && snow.OfferTransportDevice(device, config.Current)) return;

            device.Dispose();
            throw new ArgumentException(
                "EVOSIM_GPU_TRANSPORT is on and this world's snow is not one the card carries: the " +
                "card takes a tank's grid under the streams with the hoisted terms, or the box's under the open ocean " +
                "on a shaped floor with its phase tables and no shore, and this world " +
                "has " + (world.Nutrients is GridField ? "another transport path" : "no grid for its snow") +
                ". Turn the setting off.");
        }
#endif

        private static int Loop(
            EnvSettings settings, RunConfig config, World world, RunDirectory dir, Report report,
            RunManifest manifest, string outPath, float physicsDt, int stepsPerMetabolic,
            int threads, string resumePath, Evosim.Dynamics.IStepBackend engine = null,
            GroupExchange exchange = null, int place = -1)
        {
            var sim = new Simulation(
                world, settings.Seed, physicsDt, stepsPerMetabolic, threads, dir.Path);

            // The gpu engine takes the solver's blocks from the first one; a restore below reads
            // into the same creatures, which the engine's first block uploads whole.
            if (engine != null) sim.Dynamics.UseBackend(engine);

            if (settings.DigestEvery > 0)
            {
                sim.EnableDigest(dir.Path, settings.DigestEvery, settings.DigestDumpSteps);
            }

            var readings = new Readings(sim);
            var sampler = new Sampler
            {
                PoolNamed = config.FoundingTricklePoolCount > 0,
                Migrating = exchange != null,
            };

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
                FarmConsole.Out.WriteLine(
                    "state stream: " + Path.Combine(dir.Path, PoseStream.FileName) + " every " +
                    poseEvery.ToString(CultureInfo.InvariantCulture) + " s" +
                    (Math.Abs(poseEvery - settings.PoseEvery) > 1e-6f
                        ? " (raised from the " +
                          settings.PoseEvery.ToString(CultureInfo.InvariantCulture) +
                          " s asked for: nothing moves inside a metabolic step)"
                        : ""));
            }

            // propose-foraging.md's steering instrument, on wherever the world has a scent.
            SteeringRecorder steering = world.Scent != null ? new SteeringRecorder(dir.Path, 100.0) : null;

            // D176's instrument for the leaf smell, on wherever the world has one, at the steering
            // recorder's cadence. Not a world setting, so no tunable: a world with the smell records it.
            LeafScentRecorder leafScent = world.LeafScent != null ? new LeafScentRecorder(dir.Path, 100.0) : null;

            // D159: every genome this world is sent, as it came, so the world can be stepped again
            // without the worlds it traded with (ImmigrantRecord). A row an exchange's offer, flushed
            // as written: the rows are few and a window needs every one.
            JsonlWriter immigrantRows = exchange != null
                ? new JsonlWriter(Path.Combine(dir.Path, ImmigrantRecord.FileName), flushEachRow: true)
                : null;

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

            // The run header's line for the intake's origin books: from 0 s in a world founded here, and
            // from the checkpoint's second in one restored from layout 19 or older, so the reader of a
            // run knows the six intake totals cover only part of the world.
            FarmConsole.Out.WriteLine(
                "intake books:  from " +
                world.IntakeOriginBooksFromSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s" +
                (world.IntakeOriginBooksFromSeconds > 0d
                    ? "  (restored from an older layout: earlier kills read as carrion)"
                    : ""));

            // The bodies this run did not admit itself: a resumed run's inherited roster (and, in a
            // founded one, nobody). Their genomes go into this directory's genomes.jsonl.gz first,
            // so every slim snapshot row this run writes has its genome in its own directory.
            Sampler.WriteLivingGenomes(world, dir);

            float checkpointEvery = settings.ResolveCheckpointEvery();
            int checkpoints = 0;

            // The checkpoint being written behind the loop, at most one (card-next-r57.md Part 8),
            // and the count whose files are in place, which is the count the manifest gives.
            BackgroundCheckpoint inFlight = null;
            int checkpointsWritten = 0;
            string runawayCheckpointFailure = null;

            void Land()
            {
                if (inFlight == null) return;

                BackgroundCheckpoint write = inFlight;
                inFlight = null;
                write.Wait();

                checkpointsWritten++;
                manifest.LastCheckpoints = checkpointsWritten;
            }
            double lastCheckpointSeconds = double.NegativeInfinity;
            double deferredCheckpointAt = double.NaN;

            // The next second a checkpoint is due at, taken from the clock rather than counted
            // from the start, so a resumed run's checkpoints land on the same seconds the
            // original's did.
            double nextCheckpointAt = checkpointEvery > 0f
                ? (Math.Floor(world.ElapsedSeconds / checkpointEvery) + 1d) * checkpointEvery
                : double.MaxValue;

            // D159's next exchange, from the clock as the checkpoints' are, after any restore: a
            // checkpoint is written after the exchange of its second, so a resumed world's first is
            // the one after it.
            double nextExchangeAt = exchange != null
                ? exchange.NextAfter(world.ElapsedSeconds)
                : double.MaxValue;

            int reportEvery = Math.Max(1, settings.ReportEvery);
            double budgetSeconds = settings.BudgetSeconds;
            double wallMinutes = settings.WallMinutes;
            string stopPath = Path.Combine(dir.Path, "STOP");

            // The writers' clock split by writer (W0, card-next-r57.md Part 8): the poses, the
            // steering (with the leaf smell's recorder), the report rows, the snapshots and the checkpoints, in ticks and calls. Read
            // on the loop's own thread and printed at the end under the time probe; nothing reads it
            // otherwise, and nothing branches on it.
            var writerTicks = new long[5];
            var writerCalls = new long[5];
            double probeFrom = world.ElapsedSeconds;

            // The pace guard and the cap watch (proposals/propose-farm-pace-guard.md, items 2-4), read at every
            // row. The pace is the clock less the exchanges' waits, so a world held by a slow peer is not slow.
            PaceGuard pace = PaceGuard.Of(settings);
            bool canProbe = false;
            long[] capsAtStart = null;
#if EVOSIM_GPU
            canProbe = FarmGpu.CanProbe(sim.Dynamics.Backend);
            capsAtStart = FarmGpu.Overflow(sim.Dynamics.Backend);
#endif
            var caps = new CapWatch(capsAtStart, world.Diverged);
            var exchangeWait = new Stopwatch();
            string slowReason = null;
            bool checkpointNow = false;
            FarmConsole.Error.WriteLine(pace.Describe());

            var clock = Stopwatch.StartNew();
            sim.RunClock = clock;

            try
            {
                // A block the gpu engine has stepped is run to its end before the budget or the wall
                // is read (Simulation.InsideBlock). The engine steps the whole block on its first
                // step, so the wall is nearly always crossed inside one, and a world left there is a
                // block ahead of its own clock: its last checkpoint refused it, and the world died
                // with no ending (gp-old-a, 2026-10-10). The rest of the block only counts, so the
                // run stops a few milliseconds later, at the block's end. On the CPU a block is one
                // step and this reads as before.
                while (sim.InsideBlock ||
                       (world.ElapsedSeconds < budgetSeconds &&
                        clock.Elapsed.TotalMinutes < wallMinutes))
                {
                    if (!sim.Step()) continue;

                    metabolicSteps++;

                    // The state stream, before anything that can break out of the loop, so the
                    // last frame is the last instant the world was in and not the one before it.
                    if (poses != null)
                    {
                        long w0 = Stopwatch.GetTimestamp();
                        sim.WritersClock.Start();
                        poses.Sample(sim);
                        sim.WritersClock.Stop();
                        writerTicks[0] += Stopwatch.GetTimestamp() - w0;
                        writerCalls[0]++;
                    }

                    if (steering != null)
                    {
                        long w0 = Stopwatch.GetTimestamp();
                        sim.WritersClock.Start();
                        steering.Sample(sim);
                        sim.WritersClock.Stop();
                        writerTicks[1] += Stopwatch.GetTimestamp() - w0;
                        writerCalls[1]++;
                    }

                    // The leaf smell's recorder in the steering slot of the writers' clock, so the
                    // probe line keeps its five fields; the slot is the two smell recorders together.
                    if (leafScent != null)
                    {
                        long w0 = Stopwatch.GetTimestamp();
                        sim.WritersClock.Start();
                        leafScent.Sample(sim);
                        sim.WritersClock.Stop();
                        writerTicks[1] += Stopwatch.GetTimestamp() - w0;
                        if (steering == null) writerCalls[1]++;
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
#if EVOSIM_GPU
                    FarmGpu.Note(manifest, sim.Dynamics.Backend);
#endif

                    // D060. Fires once — the first metabolic step whose elapsed time reaches the
                    // pre-registered instant — and checked before the extinction test below, so an
                    // assay that lands on an empty world rescues it by design. The inoculum is null
                    // unless inoculateOn, which is the gate ActOnWorld reads.
                    assayFired = ActOnWorld(
                        world, inoculum, settings.InoculateAt, settings.InoculateCount,
                        settings.InoculateDepth, assayFired);

                    // D159's exchange, after the assay and before the extinction test for the
                    // assay's reason: an immigrant can refill an empty world. And the group's end,
                    // read every step, so a world stops one metabolic step after another has ended.
                    if (exchange != null)
                    {
                        bool groupEnded = exchange.EndedBy != null;

                        if (!groupEnded && world.ElapsedSeconds + 1e-9 >= nextExchangeAt)
                        {
                            exchangeWait.Start();
                            groupEnded = !exchange.Run(
                                place, world, nextExchangeAt,
                                (from, migrant, id) => immigrantRows.Write(
                                    ImmigrantRecord.Row(world.ElapsedSeconds, from, migrant, id, world.LastImmigrantRefusal,
                                        id >= 0 ? ImmigrantRecord.GuildOf(world, id) : -1)));
                            exchangeWait.Stop();
                            nextExchangeAt = (Math.Round(nextExchangeAt / exchange.Every) + 1d) * exchange.Every;
                        }

                        if (groupEnded)
                        {
                            status = "stopped";
                            terminationCode = "group";
                            ending =
                                "stopped at t=" +
                                world.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                                " s: the group ended, " + exchange.EndedBy;

                            break;
                        }
                    }

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
                        long w0 = Stopwatch.GetTimestamp();
                        sim.WritersClock.Start();

                        string row = sampler.Write(sim, dir, report.Columns);
                        long rowBuilt = Stopwatch.GetTimestamp();
                        report.AppendRow(row);
                        report.Flush();
                        long w1 = FarmProbe.Mark(FarmProbe.RowReport, rowBuilt);
                        writerTicks[2] += w1 - w0;
                        writerCalls[2]++;

                        // Every tenth report: often enough that a killed run keeps something
                        // recent, rare enough that a population of thousands is not serialised
                        // every sample.
                        if (metabolicSteps % (reportEvery * 10) == 0)
                        {
                            sampler.Snapshot(dir, world);
                            writerTicks[3] += Stopwatch.GetTimestamp() - w1;
                            writerCalls[3]++;
                        }

                        sim.WritersClock.Stop();

                        stopped = File.Exists(stopPath);

                        // Item 4: the run's first overflow of a card cap asks for a checkpoint at this row, and
                        // it and a first divergence are said in a line; neither stops the run.
                        long[] overflowNow = null;
#if EVOSIM_GPU
                        overflowNow = FarmGpu.Overflow(sim.Dynamics.Backend);
#endif
                        if (caps.Row(world.ElapsedSeconds, overflowNow, world.Diverged, FarmConsole.Error) &&
                            settings.OverflowCheckpoint)
                        {
                            checkpointNow = true;
                        }

                        // Items 2 and 3: a world slowing well under its trailing pace has the card probe
                        // switched on for a few rows, then stops at a checkpoint at this row with the reason.
                        PaceGuard.Act act = pace.Row(
                            clock.Elapsed.TotalMilliseconds - exchangeWait.Elapsed.TotalMilliseconds,
                            world.ElapsedSeconds, canProbe);

                        if (act == PaceGuard.Act.Probe)
                        {
#if EVOSIM_GPU
                            FarmGpu.Probe(sim.Dynamics.Backend);
#endif
                            FarmConsole.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "pace guard at t={0:0.#} s: {1}; the card probe is on for {2} rows, then the run stops",
                                world.ElapsedSeconds, pace.Pace(), pace.ProbeRows));
                        }
                        else if (act == PaceGuard.Act.Stop)
                        {
                            IReadOnlyList<string> probeLines = Array.Empty<string>();
#if EVOSIM_GPU
                            probeLines = FarmGpu.ProbeLines(sim.Dynamics.Backend);
#endif
                            slowReason = pace.Reason(
                                world, sim.Dynamics.Watch.LastWindow, caps.Since(lastCheckpointSeconds), probeLines);
                            stopped = true;
                            checkpointNow = true;
                        }
                    }

                    // A checkpoint the cap watch or the pace guard asked for is taken at this row, which a
                    // row has just closed; the cadence after it is the clock's, as before.
                    double cadenceCheckpointAt = nextCheckpointAt;
                    if (checkpointNow) nextCheckpointAt = Math.Min(nextCheckpointAt, world.ElapsedSeconds);

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

                            FarmConsole.Error.WriteLine(
                                "warning: the checkpoint due at " +
                                nextCheckpointAt.ToString("0.#", CultureInfo.InvariantCulture) +
                                " s waits: a body's plan has changed and its solver has not been " +
                                "rebuilt on it, which the harness does on the metabolic step of " +
                                "the change. A plan-changing path is missing its rebuild.");
                        }

                        checkpointDue = false;

                        // One asked for off the cadence is not carried to a later step, which no row closes.
                        if (checkpointNow) nextCheckpointAt = cadenceCheckpointAt;
                    }

                    checkpointNow = false;

                    if (checkpointDue)
                    {
                        long w0 = Stopwatch.GetTimestamp();
                        sim.WritersClock.Start();

                        lastCheckpointSeconds = world.ElapsedSeconds;
                        checkpoints++;

                        // The last write first, so at most one is ever in flight.
                        Land();

                        inFlight = WriteCheckpoint(
                            sim, sampler, dir, manifest, config, settings, physicsDt,
                            stepsPerMetabolic,
                            metabolicSteps, bestSpeedEver, bestSpeedAt, assayFired);

                        sim.WritersClock.Stop();
                        writerTicks[4] += Stopwatch.GetTimestamp() - w0;
                        writerCalls[4]++;

                        nextCheckpointAt = checkpointEvery > 0f
                            ? (Math.Floor(world.ElapsedSeconds / checkpointEvery) + 1d) * checkpointEvery
                            : double.MaxValue;
                        caps.Checkpointed();
                    }

                    if (stopped && slowReason != null)
                    {
                        status = "stopped";
                        terminationCode = "slow";
                        ending =
                            "stopped slow at t=" +
                            world.ElapsedSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s" +
                            (lastCheckpointSeconds == world.ElapsedSeconds ? ", at a checkpoint" : ", with no checkpoint at the stop") +
                            ": " + slowReason;

                        FarmConsole.Error.WriteLine(ending);
                        break;
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

                // D159: out of the exchange as soon as the loop is, with the reason, so a world
                // waiting at an exchange is released and, for anything but the budget, the group
                // ends.
                exchange?.Leave(place, terminationCode);

                // The budget, the wall and a stop leave the loop here, and the write behind it lands
                // inside the try, so a write that failed ends the run as an error below.
                Land();
            }
            catch (PopulationRunawayException runaway)
            {
                // A runaway leaves the loop mid-step, with perhaps a write still going. It lands
                // here; a write that failed is said in the ending rather than lost.
                try
                {
                    Land();
                }
                catch (Exception failed)
                {
                    FarmConsole.Error.WriteLine(failed.ToString());
                    runawayCheckpointFailure = failed.Message;
                }

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

                exchange?.Leave(place, terminationCode);

                if (runawayCheckpointFailure != null)
                {
                    ending += "; the checkpoint being written when it ran away was not written (" +
                              runawayCheckpointFailure + ")";
                }
            }
            catch (Exception e)
            {
                clock.Stop();

                // D159: an error ends the group, and releases any world waiting at an exchange.
                exchange?.Leave(place, "error");

                // The write behind the loop lands before anything closes, so the run never leaves a
                // write going. Its own failure is said beside the run's, which is the one recorded.
                try
                {
                    Land();
                }
                catch (Exception failed)
                {
                    FarmConsole.Error.WriteLine(failed.ToString());
                }

                // The stream first: disposing it writes poses.idx, so even a crashed run leaves
                // an index rather than a file only a scan can open.
                manifest.LastPoseFrames = poses?.FrameCount ?? 0;
                poses?.Dispose();
                steering?.Dispose();
                leafScent?.Dispose();
                immigrantRows?.Dispose();

#if EVOSIM_GPU
                FarmGpu.Note(manifest, sim.Dynamics.Backend);
#endif
                Manifest.Write(dir, manifest, Manifest.ErrorEnding(manifest, e));

                report.AppendLine();
                report.AppendLine("**Ended:** " + e.GetType().Name + ": " + e.Message + ".");
                report.Flush();

                sampler.Close();
                sim.Dispose();
                ((world.Nutrients as GridField)?.TransportDevice as IDisposable)?.Dispose();
                engine?.Dispose();
                dir.Dispose();

                FarmConsole.Error.WriteLine(e.ToString());
                return 1;
            }

            clock.Stop();

            // The trickle question's instrument (Metabolise.cs): living creatures the harness had
            // no body for at a metabolic step. A reading for the console and nowhere else.
            FarmConsole.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "bodiless: {0} creatures seen without a body at a metabolic step, {1} creature-steps, the longest {2:0.###} s",
                sim.BodilessCreatures, sim.BodilessCreatureSteps, sim.LongestBodilessSeconds));

            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_PROBE") == "1")
            {
                double perSecond = 1000.0 / Stopwatch.Frequency / Math.Max(1e-9, world.ElapsedSeconds - probeFrom);
                FarmConsole.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "writers-probe, ms a simulated second over {0:0.#} s: poses {1:0.00}, steering {2:0.00}, rows {3:0.00}, " +
                    "snapshots {4:0.00}, checkpoints {5:0.00}; calls {6} / {7} / {8} / {9} / {10}; the writers' clock {11} ms",
                    world.ElapsedSeconds - probeFrom, writerTicks[0] * perSecond, writerTicks[1] * perSecond,
                    writerTicks[2] * perSecond, writerTicks[3] * perSecond, writerTicks[4] * perSecond,
                    writerCalls[0], writerCalls[1], writerCalls[2], writerCalls[3], writerCalls[4], sim.WallWritersMs));
                FarmConsole.Error.WriteLine(FarmProbe.Line(world.ElapsedSeconds - probeFrom));
            }

            report.Footer(
                config, readings, ending, clock.ElapsedMilliseconds, sim.WallWritersMs,
                bestSpeedEver, bestSpeedAt);

#if EVOSIM_GPU
            FarmGpu.Note(manifest, sim.Dynamics.Backend);
#endif

            // The gpu engine's capacities (logbook/specs/gpu-port-spec.md section 2): a nonzero
            // overflow is a step that dropped a candidate or an overlap, and is read first.
            if (manifest.Gpu != null) report.AppendLine(manifest.Gpu.OverflowLine());

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
                    FarmConsole.Error.WriteLine(
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

                    // Started and landed at once: nothing steps after it.
                    inFlight = WriteCheckpoint(
                        sim, sampler, dir, manifest, config, settings, physicsDt, stepsPerMetabolic,
                        metabolicSteps, bestSpeedEver, bestSpeedAt, assayFired);
                    Land();
                }
            }

            // The checkpoints whose files are in place; every write has landed by here.
            manifest.LastCheckpoints = checkpointsWritten;

            sampler.Snapshot(dir, world);
            sampler.Close();

            // Closing the stream writes poses.idx, so the frame count is read before the close
            // and the index exists before run.json claims a count for it.
            int poseFrames = poses?.FrameCount ?? 0;
            manifest.LastPoseFrames = poseFrames;
            poses?.Dispose();
            steering?.Dispose();
            leafScent?.Dispose();
            immigrantRows?.Dispose();

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
            ((world.Nutrients as GridField)?.TransportDevice as IDisposable)?.Dispose();
            engine?.Dispose();
            dir.Dispose();

            FarmConsole.Out.WriteLine();
            FarmConsole.Out.WriteLine("**Ended:** " + ending + ".");

            // The pace guard's reason is the error log's last line too (proposals/propose-farm-pace-guard.md).
            if (slowReason != null) FarmConsole.Error.WriteLine(ending);

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
        /// Starts one checkpoint: the world, the harness, the sampler and the loop's own four
        /// numbers, at the second the caller is standing on, captured here and written behind the
        /// loop. The caller waits for the write it returns before the next and before the run ends.
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
        private static BackgroundCheckpoint WriteCheckpoint(
            Simulation sim, Sampler sampler, RunDirectory dir, RunManifest manifest,
            RunConfig config, EnvSettings settings, float physicsDt, int stepsPerMetabolic,
            int metabolicSteps, double bestSpeedEver, double bestSpeedAt, bool assayFired)
        {
            World world = sim.World;
            long settleStarted = Stopwatch.GetTimestamp();

            sim.SettleBeforeCheckpoint();

            IReadOnlyList<LineageEvent> queued = world.DrainLineageEvents();
            for (int i = 0; i < queued.Count; i++) dir.Lineage.Write(queued[i].ToJson());

            // The genome queue with it, for the same reason: it is not in the checkpoint, so what
            // it holds belongs on this side of the line.
            Sampler.DrainGenomes(world, dir);
            FarmProbe.Mark(FarmProbe.CheckpointSettle, settleStarted);

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

            // The payload here, on the loop's thread, before the next step moves anything; the
            // digest, the compression and the file on a thread of their own (card-next-r57.md
            // Part 8). The caller waits for it before the next checkpoint and at every way out.
            long payloadStarted = Stopwatch.GetTimestamp();
            CheckpointPayload payload = CheckpointWriter.Capture(w =>
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
            FarmProbe.Mark(FarmProbe.CheckpointPayload, payloadStarted);

            return BackgroundCheckpoint.Start(
                Checkpoint.PathFor(dir.Path, world.ElapsedSeconds), header, payload, settings.TestCheckpointStallMs);
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

        /// <summary>
        /// Two worlds or more in one process (card-next-r57.md, part 4): <c>--group a.args b.args [c.args ...]</c>,
        /// and <c>--pair a.args b.args</c>, which is a group of two on the card. Each file holds a world's
        /// settings, one <c>EVOSIM_NAME=value</c> a line over the environment as a command line's are; blank
        /// lines and lines starting with <c>#</c> are skipped. Each world steps on a thread of its own and
        /// writes what a lone run prints to <c>a.log</c> and <c>a.err</c> beside its settings file. On the
        /// card the worlds share one device, a stream each; on the CPU engine each world has its own solver,
        /// as it would alone. The exit code is the worst of them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every world takes Core's one thread count (<see cref="Parallelism"/>), so a group whose counts
        /// differ is refused rather than run at whichever world set it last. A split depends only on the
        /// count, so the worlds share the pool at a cost in time and none in bits.
        /// </para>
        /// <para>
        /// Nothing passes between the worlds: each is the plain world a lone run steps. Each world's run.json
        /// names the whole group in its order (<c>group</c>), and on the card its gpu block names the others
        /// (<c>sharedProcessWith</c>), so a run says it shared a process and with which.
        /// </para>
        /// </remarks>
        private static int Group(string[] paths, bool pair)
        {
            string flag = pair ? "--pair" : "--group";
            int n = paths.Length;
            var overrides = new Dictionary<string, string>[n];
            var settings = new EnvSettings[n];
            var outs = new string[n];

            for (int k = 0; k < n; k++)
            {
                overrides[k] = ReadSettingsFile(paths[k]);
                settings[k] = EnvBinding.Read(Over(overrides[k]));
                outs[k] = Path.GetFullPath(settings[k].ResolveOutPath());
            }

            bool onCard = settings[0].EngineIsGpu;
            for (int k = 1; k < n; k++)
            {
                if (settings[k].EngineIsGpu != onCard)
                {
                    Console.Error.WriteLine(flag + ": one world asks for the gpu engine and another for the CPU's; a group runs on one.");
                    return 1;
                }
            }

            if (pair && !onCard)
            {
                Console.Error.WriteLine("--pair shares one card between two worlds; both need EVOSIM_ENGINE=gpu (--group runs the CPU engine).");
                return 1;
            }

            for (int k = 1; k < n; k++)
            {
                if (settings[k].ResolveThreads() != settings[0].ResolveThreads())
                {
                    Console.Error.WriteLine(
                        flag + ": the worlds ask for " + settings[0].ResolveThreads().ToString(CultureInfo.InvariantCulture) +
                        " and " + settings[k].ResolveThreads().ToString(CultureInfo.InvariantCulture) +
                        " threads, and Core has one thread count for the process.");
                    return 1;
                }
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (string.Equals(outs[i], outs[j], StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine(flag + ": two worlds would write " + outs[i] + ".");
                        return 1;
                    }
                }
            }

            string group = string.Join(", ", outs.Select(Path.GetFileName));
            var codes = new int[n];

            // D159's exchange: on for every world or for none, at one cadence and one count, and
            // with one budget, so the worlds meet at every exchange and none leaves the group at a
            // budget the others have not reached.
            GroupExchange exchange = null;
            bool migrating = settings[0].MigrateEvery > 0f;

            for (int k = 1; k < n; k++)
            {
                if (settings[k].MigrateEvery != settings[0].MigrateEvery ||
                    (migrating && settings[k].MigrateCount != settings[0].MigrateCount))
                {
                    Console.Error.WriteLine(
                        flag + ": the worlds ask for different exchanges (EVOSIM_MIGRATE_EVERY, EVOSIM_MIGRATE_COUNT); " +
                        "a migrating group exchanges at one cadence and one count, and a group that does not sets neither.");
                    return 1;
                }

                if (migrating && settings[k].BudgetSeconds != settings[0].BudgetSeconds)
                {
                    Console.Error.WriteLine(
                        flag + ": the worlds of a migrating group ask for different budgets (EVOSIM_SECONDS); " +
                        "one that reached its budget first would leave the others with nobody to trade with.");
                    return 1;
                }
            }

            // D159, the coordinator's call of 2026-10-09: a migrating group resumes from one common
            // second, every world from its own checkpoint at it, or founds every world; a group
            // resumed at different seconds would take its next exchange with worlds at different
            // moments of their histories.
            if (migrating)
            {
                var resumed = new string[n];
                for (int k = 0; k < n; k++) resumed[k] = Checkpoint.Resolve(settings[k].ResumeFrom, settings[k].ResumeAt);

                int resuming = resumed.Count(r => r != null);
                if (resuming != 0 && resuming != n)
                {
                    Console.Error.WriteLine(
                        flag + ": " + resuming + " of the " + n + " worlds resume and the rest are founded; a migrating " +
                        "group resumes every world or none.");
                    return 1;
                }

                if (resuming == n)
                {
                    double[] at = resumed.Select(r => CheckpointReader.ReadHeader(r).Seconds).ToArray();
                    if (at.Any(s => s != at[0]))
                    {
                        Console.Error.WriteLine(
                            flag + ": the worlds' checkpoints are at " +
                            string.Join(", ", at.Select(s => s.ToString("0.###", CultureInfo.InvariantCulture))) +
                            " s; a migrating group resumes from one common second. Name it with EVOSIM_RESUME_AT in every world.");
                        return 1;
                    }
                }
            }

            if (migrating)
            {
                if (settings[0].MigrateCount < 1)
                {
                    Console.Error.WriteLine(flag + ": EVOSIM_MIGRATE_COUNT is " + settings[0].MigrateCount + "; a world sends 1 genome or more.");
                    return 1;
                }

                exchange = new GroupExchange(outs.Select(Path.GetFileName).ToArray(), settings[0].MigrateEvery, settings[0].MigrateCount);
            }

            if (!onCard) return RunGroup(paths, overrides, null, outs, group, codes, exchange);

#if EVOSIM_GPU
            Evosim.Farm.Gpu.GpuOptions first = FarmGpu.Options(settings[0]);
            for (int k = 1; k < n; k++)
            {
                if (FarmGpu.Options(settings[k]).OnCpu != first.OnCpu)
                {
                    Console.Error.WriteLine(flag + ": one world asks for the card and another for the CPU accelerator.");
                    return 1;
                }
            }

            using var device = Evosim.Farm.Gpu.GpuDevice.Open(first);
            return RunGroup(paths, overrides, device, outs, group, codes, exchange);
#else
            throw new NotSupportedException(
                flag + " on the gpu engine shares a card, and this build of the farm has no gpu engine: it is compiled " +
                "in by src/Evosim.Farm's own csproj (EVOSIM_GPU), and never into Unity.");
#endif
        }

        /// <summary>The group's worlds, a thread each, to their ends; the worst exit code.</summary>
        private static int RunGroup(
            string[] paths, Dictionary<string, string>[] overrides, object device, string[] outs, string group, int[] codes,
            GroupExchange exchange)
        {
            int n = paths.Length;
            var worlds = new System.Threading.Thread[n];

            for (int k = 0; k < n; k++)
            {
                int self = k;
                string others = string.Join(", ", outs.Where((_, i) => i != self).Select(Path.GetFileName));
                worlds[k] = GroupWorld(paths[k], overrides[k], device, others, group, codes, k, exchange);
            }

            foreach (var t in worlds) t.Start();
            foreach (var t in worlds) t.Join();
            return codes.Max();
        }

        private static System.Threading.Thread GroupWorld(
            string path, Dictionary<string, string> overrides, object device, string others, string group,
            int[] codes, int k, GroupExchange exchange)
        {
            return new System.Threading.Thread(() =>
            {
                string stem = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFileNameWithoutExtension(path));
                using var log = new StreamWriter(stem + ".log", false, new UTF8Encoding(false)) { AutoFlush = true };
                using var err = new StreamWriter(stem + ".err", false, new UTF8Encoding(false)) { AutoFlush = true };
                FarmConsole.Use(log, err);
#if EVOSIM_GPU
                Evosim.Farm.Gpu.GpuConsole.Use(err);
#endif

                try
                {
                    codes[k] = RunWorld(overrides, device, others, group, exchange, k);
                }
                catch (Exception e)
                {
                    err.WriteLine(e.GetType().Name + ": " + e.Message);
                    codes[k] = 1;
                }
                finally
                {
                    // The loop says why it ended; this is for a world that never reached it or left
                    // by an exception, so the others are released from the exchange and stop.
                    exchange?.Leave(k, null);
                }
            })
            { Name = "world " + k.ToString(CultureInfo.InvariantCulture) };
        }

        private static EnvBinding.Lookup Over(Dictionary<string, string> overrides) =>
            name => overrides.TryGetValue(name, out string v) ? v : Environment.GetEnvironmentVariable(name);

        /// <summary>A pair's settings file: <c>EVOSIM_NAME=value</c> a line; anything else is refused, as on a command line.</summary>
        private static Dictionary<string, string> ReadSettingsFile(string path)
        {
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0 || !line.StartsWith("EVOSIM_", StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        path + " line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": '" + line +
                        "' is not a setting. A pair's settings are EVOSIM_NAME=value lines.");
                }

                overrides[line.Substring(0, eq)] = line.Substring(eq + 1);
            }

            return overrides;
        }

        private static void Help()
        {
            FarmConsole.Out.WriteLine(
                "Evosim.Farm — one evolution arm, stepped by Evosim.Dynamics rather than by Unity.");
            FarmConsole.Out.WriteLine();
            FarmConsole.Out.WriteLine("  Evosim.Farm [EVOSIM_NAME=value ...]");
            FarmConsole.Out.WriteLine();
            FarmConsole.Out.WriteLine(
                "Every setting is an environment variable, as it was under the editor; an");
            FarmConsole.Out.WriteLine(
                "argument of the same form overrides one. The settings this build reads:");
            FarmConsole.Out.WriteLine();

            IReadOnlyList<string> names = EnvBinding.Names;
            for (int i = 0; i < names.Count; i++) FarmConsole.Out.WriteLine("  " + names[i]);
        }
    }
    /// <summary>
    /// Where the farm prints: the console, or a world's own files when two worlds share the process
    /// (<c>Program.Pair</c>). Per thread, so each world's lines reach its own files; a thread that set
    /// nothing prints to the console as the farm always has.
    /// </summary>
    internal static class FarmConsole
    {
        [ThreadStatic] private static TextWriter _out;
        [ThreadStatic] private static TextWriter _error;

        public static TextWriter Out => _out ?? Console.Out;

        public static TextWriter Error => _error ?? Console.Error;

        public static void Use(TextWriter output, TextWriter error)
        {
            _out = output;
            _error = error;
        }
    }
}
