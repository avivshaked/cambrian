using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// <c>run.json</c>: the identity of a run, separate from <c>config.json</c>'s resolved
    /// tunables — <c>EvolutionRun.BuildManifest</c> and <c>WriteRunManifest</c>, out of Unity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written twice, and the second write rewrites.</b> Once before the first step saying
    /// <c>running</c>, once at an orderly end with how it ended. That is the whole reason a killed
    /// arm is readable as killed and a crashed one as crashed, and it is why
    /// <c>stop-arm.ps1</c> can merge <c>status: "stopped"</c> into a document it did not write.
    /// The replacement goes through a temporary file and a move, so a reader polling the file
    /// never catches half of it.
    /// </para>
    /// <para>
    /// <b>What the port changes, and nothing else.</b> <c>simHash</c> named the Unity assembly
    /// that stepped the world; there is no such assembly now, so the source block carries
    /// <c>dynamicsHash</c> over <c>src/Evosim.Dynamics</c> and <c>farmHash</c> over
    /// <c>src/Evosim.Farm</c>, both by the same tree digest, and <c>coreHash</c> is untouched.
    /// <c>unityVersion</c> becomes <c>engine</c> and <c>engineVersion</c>; <c>physicsJobWorkers</c>
    /// and <c>jobWorkerMaximum</c> become <c>threads</c>, which decides pace and not the
    /// trajectory (the spike's digests are equal at 1, 4 and 16 threads); and <c>workerPath</c>,
    /// which named a copy of <c>unity/</c>, becomes <c>programPath</c> and <c>processId</c> —
    /// what a later <c>stop-arm</c> has to find instead of a Unity process. Every other field,
    /// and the whole ending block, keeps its name and its place.
    /// </para>
    /// </remarks>
    public sealed class RunManifest
    {
        public const string EngineName = "dynamics";

        public string ArmName;
        public ulong Seed;
        public float RequestedSeconds;
        public float RequestedWallMinutes;
        public string ConfigHash;
        public string InoculatePath;
        public string InoculumHash;

        /// <summary>The physics step and the metabolic step this run integrates at.</summary>
        public float PhysicsStepSeconds = EnvBinding.DefaultPhysicsStepSeconds;
        public float MetabolicStepSeconds = EnvBinding.MetabolicStepSeconds;

        /// <summary>What version of the solver ran, beside the hash that actually identifies it.</summary>
        public string EngineVersion;

        /// <summary>Threads the body pass ran on. A pace setting, never a realisation.</summary>
        public int Threads;

        /// <summary>This process, so an arm can be stopped without guessing which one it is.</summary>
        public int ProcessId = GetCurrentProcessId();

        public string GitCommit;
        public bool GitDirty;
        public string CoreHash;
        public string DynamicsHash;
        public string FarmHash;
        public string ProgramPath;
        public string RepoRoot;

        /// <summary>Why a source fact is missing, or null when nothing is.</summary>
        public string Note;

        /// <summary>When this run started, ISO-8601 UTC. Both writes carry the same instant.</summary>
        public string StartedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>D092's seven: the three dials as the config carried them, and the four the map
        /// measured of itself. All 0 on a flat world.</summary>
        public float BedRelief;
        public float BedTilt;
        public float BedScale;
        public int BedHollows;
        public int BedRidges;
        public double BedRangeMetres;
        public double BedSteepestDegrees;

        /// <summary>D102's ratio: the water the streams were built to, read off the built world.</summary>
        /// <remarks>
        /// 1 in a box and in every tank whose axes balance, which is every recording before the
        /// build that added it; the header's <c>axes v:h</c> token is the same number.
        /// </remarks>
        public double StreamsAxisRatio = 1d;

        /// <summary>Seconds between state-stream frames. 0 is a run that recorded no stream.</summary>
        public double PoseEverySeconds;

        /// <summary>Frames the state stream had written when the loop last looked.</summary>
        public int LastPoseFrames;

        /// <summary>
        /// What was true as of the last metabolic step, for the error path.
        /// </summary>
        /// <remarks>
        /// Carried on the manifest rather than recomputed in a catch, because by the time the
        /// catch runs the world and the clock have unwound with the loop's frame. A manifest that
        /// reports zero and one that reports nothing are both lies; this one reports the last
        /// thing that was true (logbook/0056).
        /// </remarks>
        public double LastSimulatedSeconds;
        public long LastPhysicsSteps;
        public long LastBirths;
        public int LastAlive;
        public long LastDragImpulsesLimited;
        public long LastDriveImpulsesLimited;
        public long LastDiverged;
        public double LastMatterInfluxed;
        public double LastMatterBuried;
        public double LastWallClockMinutes;
        public long LastWraps;
        public long LastCrowdedTotal;
        public long LastContactPairsTotal;
        public long LastContactPairsJointedTotal;
        public long LastContactPairsPersistentTotal;
        public long LastContactBodiesTotal;
        public double LastMaxJointMassRatio;
        public long LastBodiesOverMassRatio10;
        public long LastWallPhysicsMs;
        public long LastWallWorldMs;
        public long LastWallHarnessMs;
        public long LastWallWritersMs;
        public long LastWallTotalMs;

        private static int GetCurrentProcessId()
        {
            using (Process p = Process.GetCurrentProcess()) return p.Id;
        }
    }

    /// <summary>How a run stopped. Null while it is still going.</summary>
    /// <remarks>
    /// Field for field the Unity build's <c>RunEnding</c>, because the ending block is what every
    /// reader of a run.json actually reads — <c>clade-score.ps1</c>'s qualifier, the round reads,
    /// <c>watch-round.py</c>'s status — and a port is not an excuse to rename any of it.
    /// </remarks>
    public sealed class RunEnding
    {
        public string Status;
        public string Reason;
        public string Prose;
        public double SimulatedSeconds;
        public long PhysicsSteps;
        public long Births;
        public int Alive;
        public double WallClockMinutes;
        public double TimesRealTime;
        public long DragImpulsesLimited;
        public bool SharedSpace;
        public long Wraps;
        public long Crowded;
        public double ContactPairsPerStep;
        public long ContactPairsJointed;
        public long ContactPairsPersistent;
        public long ContactBodies;
        public long DriveImpulsesLimited;
        public long DivergedTotal;
        public double MatterInfluxedTotal;
        public double MatterBuriedTotal;
        public double BestSpeed;
        public double BestSpeedAtSeconds;
        public double MaxJointMassRatio;
        public long BodiesOverMassRatio10;
        public long WallPhysicsMs;
        public long WallWorldMs;
        public long WallHarnessMs;
        public long WallWritersMs;
        public long WallTotalMs;

        /// <summary>
        /// The harness profile, ms per phase, and the names they are written under. Null on the
        /// error path, where the manifest's last-sample values are what there is: the block then
        /// omits them rather than writing zeros that read like a run that spent no time anywhere.
        /// </summary>
        public long[] WallHarnessPhaseMs;
        public string[] HarnessPhases;
        public long HarnessBodySteps;

        public long WallFluidGatherMs;
        public long WallFluidWaterMs;
        public long WallFluidComputeMs;
        public long WallFluidApplyMs;
        public long FluidLinkSteps;

        /// <summary>Complete frames in <c>poses.bin</c>. 0 when the run recorded no stream.</summary>
        public int PoseFrames;

        /// <summary>The statistics field a phase is written under — <c>wallHarnessSettleMs</c>.</summary>
        /// <remarks>
        /// Built from the phase names rather than from a second list, so the two cannot disagree —
        /// <c>Ecosystem.BuildHarnessPhaseFields</c>'s rule, kept.
        /// </remarks>
        public static string FieldFor(string phase) =>
            "wallHarness" + char.ToUpperInvariant(phase[0]) + phase.Substring(1) + "Ms";
    }

    /// <summary>Builds and writes <see cref="RunManifest"/>.</summary>
    public static class Manifest
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Gathers the run's identity and the identity of the source about to produce it.
        /// </summary>
        /// <remarks>
        /// <b>Nothing here may take the run down.</b> git may not be on PATH, the repository root
        /// may be unfindable, a directory may be missing: each failure writes <c>"unknown"</c> and
        /// says why in <c>note</c>. A run is expensive and a missing provenance field is a smaller
        /// loss than a run that would not start.
        /// </remarks>
        public static RunManifest Build(
            EnvSettings settings, string configHash, string inoculumHash,
            float physicsStepSeconds, int stepsPerMetabolicStep, int threads)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var notes = new List<string>();

            // The program that is running, which is what "the copy actually running" means here
            // and is not necessarily the process's current directory.
            string programPath = AppContext.BaseDirectory?.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string repoRoot = settings.RepoRoot;
            if (string.IsNullOrEmpty(repoRoot))
            {
                repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());

                if (repoRoot == null)
                {
                    repoRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                    notes.Add(
                        "EVOSIM_REPO_ROOT unset and no src/Evosim.Core above the working " +
                        "directory; repo root assumed to be the working directory");
                }
                else
                {
                    notes.Add(
                        "EVOSIM_REPO_ROOT unset; repo root found by walking up from the working " +
                        "directory to the first src/Evosim.Core");
                }
            }

            var manifest = new RunManifest
            {
                ArmName = Path.GetFileNameWithoutExtension(settings.ResolveOutPath()),
                Seed = settings.Seed,
                RequestedSeconds = settings.BudgetSeconds,
                RequestedWallMinutes = settings.WallMinutes,
                ConfigHash = configHash,
                InoculatePath = settings.InoculatePath,
                InoculumHash = inoculumHash,
                PhysicsStepSeconds = physicsStepSeconds,
                MetabolicStepSeconds = stepsPerMetabolicStep * physicsStepSeconds,
                EngineVersion = EngineVersion(),
                Threads = threads,
                ProgramPath = programPath,
                RepoRoot = repoRoot,
            };

            manifest.GitCommit = Git(repoRoot, "rev-parse HEAD", out string commitFailure)?.Trim();
            if (string.IsNullOrEmpty(manifest.GitCommit))
            {
                manifest.GitCommit = "unknown";
                notes.Add("git rev-parse HEAD failed: " + (commitFailure ?? "no output"));
            }

            // Code paths only, and named explicitly, as the Unity build names them: a run is not
            // "built from dirty source" because somebody left a markdown draft at the root.
            string porcelain = Git(repoRoot, "status --porcelain -- src unity scripts",
                out string statusFailure);

            if (statusFailure != null)
            {
                notes.Add("git status --porcelain failed: " + statusFailure);
                manifest.GitDirty = false;
            }
            else
            {
                manifest.GitDirty = !string.IsNullOrEmpty(porcelain.Trim());
            }

            manifest.CoreHash = HashOrUnknown(
                Path.Combine(repoRoot, "src", "Evosim.Core"), notes);
            manifest.DynamicsHash = HashOrUnknown(
                Path.Combine(repoRoot, "src", "Evosim.Dynamics"), notes);
            manifest.FarmHash = HashOrUnknown(
                Path.Combine(repoRoot, "src", "Evosim.Farm"), notes);

            manifest.Note = notes.Count == 0 ? null : string.Join("; ", notes.ToArray());
            return manifest;
        }

        /// <summary>Copies D092's seven facts off the world the launch produced.</summary>
        /// <remarks>
        /// Set from the built world rather than passed into <see cref="Build"/>, which takes the
        /// launch's own facts: the last four exist nowhere else, and all seven stay 0 on a flat
        /// world.
        /// </remarks>
        public static void RecordBed(RunManifest manifest, BedShape bed)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (bed == null || !bed.HasRelief) return;

            manifest.BedRelief = bed.ReliefMetres;
            manifest.BedTilt = bed.TiltMetres;
            manifest.BedScale = bed.ScaleMetres;
            manifest.BedHollows = bed.Hollows;
            manifest.BedRidges = bed.Ridges;
            manifest.BedRangeMetres = bed.RangeMetres;
            manifest.BedSteepestDegrees = bed.SteepestTotalSlopeRadians * 180d / Math.PI;
        }

        /// <summary>Writes <c>run.json</c> into the run directory: the first call, or the second.</summary>
        public static void Write(RunDirectory dir, RunManifest m, RunEnding ending)
        {
            if (dir == null) throw new ArgumentNullException(nameof(dir));

            // Written beside the file and moved over it: a reader polling run.json — which
            // run-arm.ps1 does within the first minute of a launch — must never catch a truncated
            // document. File.Replace when the target exists (the creation write always made one),
            // File.Move when it does not.
            string finalPath = Path.Combine(dir.Path, "run.json");
            string tempPath = finalPath + ".tmp";

            File.WriteAllText(tempPath, Render(m, ending), Utf8NoBom);

            if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, null);
            else File.Move(tempPath, finalPath);
        }

        /// <summary>The document itself, so a test can read it without a directory.</summary>
        public static string Render(RunManifest m, RunEnding ending)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            var w = new Json.Writer(indent: true);
            w.BeginObject();

            // The creation half. Written identically by both calls, from the same object, so the
            // two can never disagree about what the run was.
            w.Field("arm", m.ArmName);
            w.Field("seed", m.Seed);

            // What stepped the bodies, where the Unity build named its editor. The hash below is
            // what identifies the solver; this says which solver it is at all.
            w.Field("engine", RunManifest.EngineName);
            w.Field("engineVersion", m.EngineVersion);
            w.Field("physicsDtSeconds", m.PhysicsStepSeconds);
            w.Field("metabolicStepSeconds", m.MetabolicStepSeconds);

            // Where physicsJobWorkers and jobWorkerMaximum stood. The count is recorded for the
            // same reason theirs was — a reader has to know what produced the pace — but it is not
            // the same kind of fact: PhysX parted from its recording above 0 workers and this
            // solver does not, so a thread count is not a realisation.
            w.Field("threads", m.Threads);
            w.Field("processId", m.ProcessId);
            w.Field("requestedSeconds", m.RequestedSeconds);
            w.Field("requestedWallMinutes", m.RequestedWallMinutes);
            w.Field("configHash", m.ConfigHash);
            w.Field("inoculateGenomePath", m.InoculatePath);
            w.Field("inoculateGenomeHash", m.InoculumHash);

            w.BeginObject("source");
            w.Field("gitCommit", m.GitCommit);
            w.Field("gitDirty", m.GitDirty);
            w.Field("coreHash", m.CoreHash);
            w.Field("dynamicsHash", m.DynamicsHash);
            w.Field("farmHash", m.FarmHash);
            w.Field("programPath", m.ProgramPath);
            w.Field("repoRoot", m.RepoRoot);
            w.Field("note", m.Note);
            w.EndObject();

            w.Field("bedRelief", m.BedRelief);
            w.Field("bedTilt", m.BedTilt);
            w.Field("bedScale", m.BedScale);
            w.Field("bedHollows", m.BedHollows);
            w.Field("bedRidges", m.BedRidges);
            w.Field("bedRangeMetres", m.BedRangeMetres);
            w.Field("bedSteepestDegrees", m.BedSteepestDegrees);

            // D102 — after the bed's seven, per the same append-only rule and in the place
            // EvolutionRun writes it. Derived from the world the launch produced rather than from
            // the launch; 1 on every box and on every tank that balances, and the header's
            // `axes v:h` token is the same number.
            w.Field("streamsAxisRatio", m.StreamsAxisRatio);

            // A recording setting and not a world one, so it sits here and not in config.json:
            // the seconds between state-stream frames, 0 when the run wrote no poses.bin
            // (logbook/specs/state-stream-spec.md).
            w.Field("poseEverySeconds", m.PoseEverySeconds);

            w.Field("startedAt", m.StartedAtUtc);

            if (ending == null)
            {
                w.Field("status", "running");
            }
            else
            {
                w.Field("status", ending.Status);
                w.Field("reason", ending.Reason);
                w.Field("ending", ending.Prose);
                w.Field("endedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                w.Field("simulatedSeconds", ending.SimulatedSeconds);
                w.Field("physicsSteps", ending.PhysicsSteps);
                w.Field("births", ending.Births);
                w.Field("aliveAtEnd", ending.Alive);
                w.Field("wallClockMinutes", ending.WallClockMinutes);
                w.Field("timesRealTime", ending.TimesRealTime);
                w.Field("dragImpulsesLimited", ending.DragImpulsesLimited);
                w.Field("driveImpulsesLimited", ending.DriveImpulsesLimited);
                w.Field("divergedTotal", ending.DivergedTotal);
                w.Field("matterInfluxedTotal", ending.MatterInfluxedTotal);
                w.Field("matterBuriedTotal", ending.MatterBuriedTotal);
                w.Field("bestSpeed", ending.BestSpeed);
                w.Field("bestSpeedAtSeconds", ending.BestSpeedAtSeconds);
                w.Field("sharedSpace", ending.SharedSpace);
                w.Field("wraps", ending.Wraps);
                w.Field("crowded", ending.Crowded);
                w.Field("overlapPairsPerStep", ending.ContactPairsPerStep);
                w.Field("overlapPairsJointed", ending.ContactPairsJointed);
                w.Field("overlapPairsHeld", ending.ContactPairsPersistent);
                w.Field("overlapBodies", ending.ContactBodies);
                w.Field("maxJointMassRatio", ending.MaxJointMassRatio);
                w.Field("bodiesOverMassRatio10", ending.BodiesOverMassRatio10);
                w.Field("wallPhysicsMs", ending.WallPhysicsMs);
                w.Field("wallWorldMs", ending.WallWorldMs);
                w.Field("wallHarnessMs", ending.WallHarnessMs);
                w.Field("wallWritersMs", ending.WallWritersMs);
                w.Field("wallTotalMs", ending.WallTotalMs);

                if (ending.WallHarnessPhaseMs != null && ending.HarnessPhases != null)
                {
                    for (int p = 0; p < ending.WallHarnessPhaseMs.Length &&
                                    p < ending.HarnessPhases.Length; p++)
                    {
                        w.Field(RunEnding.FieldFor(ending.HarnessPhases[p]),
                            ending.WallHarnessPhaseMs[p]);
                    }

                    w.Field("harnessBodySteps", ending.HarnessBodySteps);
                    w.Field("wallFluidGatherMs", ending.WallFluidGatherMs);
                    w.Field("wallFluidWaterMs", ending.WallFluidWaterMs);
                    w.Field("wallFluidComputeMs", ending.WallFluidComputeMs);
                    w.Field("wallFluidApplyMs", ending.WallFluidApplyMs);
                    w.Field("fluidLinkSteps", ending.FluidLinkSteps);
                }

                // The state stream's own count, appended after everything the profile writes.
                // 0 on a run that recorded no stream, which is every run before this build and
                // every run whose launcher left EVOSIM_POSE_EVERY alone.
                w.Field("poseFrames", ending.PoseFrames);
            }

            w.EndObject();
            return w.ToString();
        }

        /// <summary>
        /// The ending a crash writes, from the last facts the loop recorded.
        /// </summary>
        /// <remarks>
        /// The last facts and not zeros: a censored arm's manifest is the only machine-readable
        /// account of it there will ever be, and one reading "0 physics steps, 0 alive" of a run
        /// that had simulated 15,345 seconds with 1,707 creatures in it is worse than one that
        /// omitted the fields (logbook/0056).
        /// </remarks>
        public static RunEnding ErrorEnding(RunManifest m, Exception e) =>
            new RunEnding
            {
                Status = "error",
                Reason = "error",
                Prose = e.GetType().Name + ": " + e.Message,
                SimulatedSeconds = m.LastSimulatedSeconds,
                PhysicsSteps = m.LastPhysicsSteps,
                Births = m.LastBirths,
                Alive = m.LastAlive,
                WallClockMinutes = m.LastWallClockMinutes,
                TimesRealTime = m.LastSimulatedSeconds /
                    Math.Max(1e-9, m.LastWallClockMinutes * 60d),
                DragImpulsesLimited = m.LastDragImpulsesLimited,
                DriveImpulsesLimited = m.LastDriveImpulsesLimited,
                DivergedTotal = m.LastDiverged,
                MatterInfluxedTotal = m.LastMatterInfluxed,
                MatterBuriedTotal = m.LastMatterBuried,
                Wraps = m.LastWraps,
                Crowded = m.LastCrowdedTotal,
                ContactPairsPerStep = m.LastPhysicsSteps > 0
                    ? m.LastContactPairsTotal / (double)m.LastPhysicsSteps
                    : 0d,
                ContactPairsJointed = m.LastContactPairsJointedTotal,
                ContactPairsPersistent = m.LastContactPairsPersistentTotal,
                ContactBodies = m.LastContactBodiesTotal,
                MaxJointMassRatio = m.LastMaxJointMassRatio,
                BodiesOverMassRatio10 = m.LastBodiesOverMassRatio10,
                WallPhysicsMs = m.LastWallPhysicsMs,
                WallWorldMs = m.LastWallWorldMs,
                WallHarnessMs = m.LastWallHarnessMs,
                WallWritersMs = m.LastWallWritersMs,
                WallTotalMs = m.LastWallTotalMs,
                PoseFrames = m.LastPoseFrames,
            };

        /// <summary>
        /// SHA-256 over every <c>.cs</c> under <paramref name="root"/>, or null if there are none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The algorithm is a contract</b>, transcribed from <c>EvolutionRun.HashSourceTree</c>
        /// so that <c>run-arm.ps1</c>'s PowerShell digest still agrees with it: for every file, in
        /// ordinal order of its path relative to the root with <c>/</c> separators,
        /// <c>relativePath \n sha256OfBytes \n</c>, then SHA-256 over that string.
        /// </para>
        /// <para>
        /// <b>Bytes, so a line ending changes the hash.</b> Deliberately: <c>simHash</c> is a
        /// property of a checkout and not of a commit, and two checkouts of one commit differing
        /// by carriage returns are two different trees to this function (CLAUDE.md, and round
        /// 41c's refused launch). Normalising the line endings would make the digest agree across
        /// checkouts and stop it identifying the bytes that ran, which is its whole job.
        /// </para>
        /// <para>
        /// <b>Filtered by extension rather than by search pattern</b>, for the same reason:
        /// <c>Directory.GetFiles</c> with <c>"*.cs"</c> also matches longer extensions through 8.3
        /// short names.
        /// </para>
        /// </remarks>
        public static string HashSourceTree(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;

            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var files = new List<string>();

            foreach (string path in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }

            if (files.Count == 0) return null;

            var relative = new List<string>(files.Count);
            var byRelative = new Dictionary<string, string>(files.Count, StringComparer.Ordinal);

            foreach (string path in files)
            {
                string rel = Path.GetFullPath(path)
                    .Substring(full.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace('\\', '/');

                relative.Add(rel);
                byRelative[rel] = path;
            }

            relative.Sort(StringComparer.Ordinal);

            var manifest = new StringBuilder();
            using (SHA256 sha256 = SHA256.Create())
            {
                foreach (string rel in relative)
                {
                    byte[] digest = sha256.ComputeHash(File.ReadAllBytes(byRelative[rel]));
                    manifest.Append(rel).Append('\n')
                        .Append(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant())
                        .Append('\n');
                }

                byte[] total = sha256.ComputeHash(
                    new UTF8Encoding(false).GetBytes(manifest.ToString()));

                return BitConverter.ToString(total).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>SHA-256 of a file's bytes, lowercase hex — a genome's identity.</summary>
        public static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
            }
        }

        private static string HashOrUnknown(string root, List<string> notes)
        {
            string hash = HashSourceTree(root);
            if (hash != null) return hash;

            notes.Add("no .cs found under " + root);
            return "unknown";
        }

        /// <summary>The first directory at or above <paramref name="from"/> holding src/Evosim.Core.</summary>
        private static string FindRepoRoot(string from)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(from));

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Evosim.Core")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }

        private static string EngineVersion()
        {
            try
            {
                Version v = typeof(Evosim.Dynamics.DynamicsWorld).Assembly.GetName().Version;
                return v == null ? "unknown" : v.ToString();
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        /// <summary>Runs git and returns stdout, or null. Best-effort, read-only, bounded.</summary>
        private static string Git(string workingDirectory, string arguments, out string failure)
        {
            failure = null;

            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Bounded, because a run must not hang on a prompt.
                    if (!process.WaitForExit(15000))
                    {
                        failure = "timed out";
                        return null;
                    }

                    if (process.ExitCode != 0)
                    {
                        failure = "exit " + process.ExitCode + ": " + error.Trim();
                        return null;
                    }

                    return output;
                }
            }
            catch (Exception e)
            {
                failure = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
    }
}
