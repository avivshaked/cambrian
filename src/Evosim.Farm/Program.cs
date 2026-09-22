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

            RunConfig config = EnvBinding.BuildConfig(settings);
            float physicsDt = EnvBinding.ResolvePhysicsStep(settings.PhysicsDt, out int stepsPerMetabolic);
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
            var world = new World(config, settings.Seed);

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
                config, DateTime.UtcNow);

            RunManifest manifest = Manifest.Build(
                settings, config.Hash(), inoculumHash, physicsDt, stepsPerMetabolic, threads);

            Manifest.RecordBed(manifest, world.Bed);

            // D102, set here for RecordBed's reason and from the same world: the ratio the streams
            // were built to, which the world's constructor derived when it told the field what
            // tank it is in. 1 in a box and in a tank whose axes balance, which is every run in
            // the record; the header's `axes v:h` token is the same number.
            manifest.StreamsAxisRatio = config.Current.StreamsAxisRatio;

            // A recording setting, recorded where the other recording settings are: it must not
            // reach config.json or its hash, and a reader of run.json is owed the cadence the
            // stream beside it was written at.
            manifest.PoseEverySeconds = settings.ResolvePoseEvery();

            Manifest.Write(dir, manifest, ending: null);

            var report = new Report(outPath, config);
            report.Begin(settings, config, space, manifest.EngineVersion);
            report.Flush();

            Console.WriteLine(report.Text.TrimEnd());
            Console.WriteLine();
            Console.WriteLine("run directory: " + dir.Path);
            Console.WriteLine("report:        " + outPath);
            Console.WriteLine(
                "threads:       " + threads.ToString(CultureInfo.InvariantCulture) +
                "   dt " + physicsDt.ToString(CultureInfo.InvariantCulture) +
                " s, metabolic step " + (stepsPerMetabolic * physicsDt).ToString(CultureInfo.InvariantCulture) + " s");

            return Loop(settings, config, world, dir, report, manifest, outPath,
                physicsDt, stepsPerMetabolic, threads);
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
            int threads)
        {
            var sim = new Simulation(
                world, settings.Seed, physicsDt, stepsPerMetabolic, threads, dir.Path);

            if (settings.DigestEvery > 0)
            {
                sim.EnableDigest(dir.Path, settings.DigestEvery, settings.DigestDumpSteps);
            }

            var readings = new Readings(sim);
            var sampler = new Sampler();

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
                    // assay that lands on an empty world rescues it by design.
                    if (inoculateOn && !assayFired && world.ElapsedSeconds >= settings.InoculateAt)
                    {
                        world.Inoculate(
                            inoculum, settings.InoculateCount, -settings.InoculateDepth);

                        assayFired = true;
                    }

                    // Checked every step, not only at a report row: an empty world would
                    // otherwise sit doing nothing for up to reportEvery more steps. A crash to
                    // zero is a real outcome, so it ends through the same finishing path as a
                    // normal one — one last row first, so the final state is not lost.
                    if (world.Living.Count == 0)
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

                    if (metabolicSteps % reportEvery != 0) continue;

                    sim.WritersClock.Start();

                    report.AppendRow(sampler.Write(sim, dir, report.Columns));
                    report.Flush();

                    // Every tenth report: often enough that a killed run keeps something recent,
                    // rare enough that a population of thousands is not serialised every sample.
                    if (metabolicSteps % (reportEvery * 10) == 0) sampler.Snapshot(dir, world);

                    sim.WritersClock.Stop();

                    if (File.Exists(stopPath))
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
            });

            report.GenomesLine(dir.Path);
            report.Flush();

            sim.Dispose();
            dir.Dispose();

            Console.WriteLine();
            Console.WriteLine("**Ended:** " + ending + ".");

            return 0;
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
