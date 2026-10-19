using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// One row of <c>stats.jsonl</c>, reduced to the columns the identity check compares.
    /// </summary>
    /// <remarks>
    /// <b>Five columns, chosen for what they would catch.</b> <c>alive</c>, <c>births</c> and
    /// <c>deaths</c> are the population's whole history in three integers — nothing can drift in
    /// the economy without moving one of them eventually. <c>auditResidual</c> is a double
    /// accumulated over millions of steps (§5A.2), so it is the strictest available test of
    /// bit-identity, and <c>meanHeight</c> is the only positional quantity a stats row carries:
    /// a replay whose physics diverged in the third decimal shows here first and in the counts
    /// much later, or never.
    /// </remarks>
    public struct RunSample
    {
        public double T;
        public int Alive;
        public long Births;
        public long Deaths;
        public double AuditResidual;
        public double MeanHeight;

        /// <summary>
        /// The row's <c>matterResidual</c>, or 0 on a run recorded before that column existed —
        /// <see cref="MatterResidualRecorded"/> says which.
        /// </summary>
        /// <remarks>
        /// Read but not compared. The five columns above are the identity check, and adding a
        /// sixth would make every run recorded before the column mismatch on its first sample for
        /// a reason that has nothing to do with whether the replay is the run. What it is for is
        /// the interface, which shows the live world's own matter residual beside the audit and
        /// can say whether the recording carried one to put beside it.
        /// </remarks>
        public double MatterResidual;

        /// <summary>False on a run recorded before <c>matterResidual</c> was a column.</summary>
        public bool MatterResidualRecorded;
    }

    /// <summary>
    /// A recorded run, read back: its settings, its identity, and its per-sample record —
    /// DESIGN.md §9, §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything a replay needs is on disk and nothing else is.</b> <c>EvolutionRun.RunBody</c>
    /// reads environment variables into a <see cref="RunConfig"/>, calls
    /// <c>new Ecosystem(config, seed)</c> and steps it; there is no scene and nothing else
    /// consumed at launch. So <c>config.json</c> (through <see cref="RunDirectory.ReadConfig"/>)
    /// plus the seed and the timestep from <c>run.json</c> is the whole launch sequence, and that
    /// is what makes Mode B cheap to build and honest to show.
    /// </para>
    /// <para>
    /// <b>The timestep is read from <c>run.json</c> and cross-checked against the config.</b> It
    /// became a tunable when DESIGN.md §6.2's queued item closed, so it now reaches
    /// <c>config.json</c> and the hash as well — but the manifest's <c>physicsDtSeconds</c> is
    /// the number the solver was actually configured with, and a replay at the wrong step is a
    /// different chaotic realisation rather than a replay (logbook/0052). They disagreeing is
    /// worth saying out loud rather than silently preferring one.
    /// </para>
    /// </remarks>
    public sealed class RunRecord
    {
        public string Path { get; private set; }
        public RunConfig Config { get; private set; }

        /// <summary>Set when <c>config.json</c>'s stored hash disagrees with its own settings.</summary>
        public string ConfigHashMismatch { get; private set; }

        public ulong Seed { get; private set; }
        public float PhysicsDtSeconds { get; private set; }

        /// <summary>Set when <c>run.json</c>'s step and <c>config.json</c>'s disagree.</summary>
        public string StepDisagreement { get; private set; }

        /// <summary>
        /// How many job worker threads the physics step was spread over, or null when the run was
        /// recorded before the manifest carried it — D078, logbook/0069.
        /// </summary>
        /// <remarks>
        /// Null is not "zero". A shared-space run recorded at the old default parts from any
        /// re-run of itself after about 148,000 steps, whatever this replay is configured with,
        /// so the two cases have to stay distinguishable and the viewer has to be told which one
        /// it is watching.
        /// </remarks>
        public int? PhysicsJobWorkers { get; private set; }

        /// <summary>The job-worker ceiling of the machine that recorded the run, or null.</summary>
        public int? JobWorkerMaximum { get; private set; }

        /// <summary>
        /// Simulated seconds the run was launched to reach, or null when the manifest does not
        /// say — the timeline's axis, and nothing else.
        /// </summary>
        /// <remarks>
        /// A run that was stopped, killed or censored never reaches it, which is exactly why the
        /// timeline wants it: the distance between where the record ends and where it was meant to
        /// end is a fact about the run that no other column carries.
        /// </remarks>
        public double? RequestedSeconds { get; private set; }

        public string ArmName { get; private set; }
        public string ConfigHash { get; private set; }
        public string UnityVersion { get; private set; }
        public string Status { get; private set; }
        public string GitCommit { get; private set; }
        public bool GitDirty { get; private set; }
        public string CoreHash { get; private set; }
        public string SimHash { get; private set; }

        /// <summary>
        /// Which engine stepped the run: <c>"physx"</c> for every run the Unity farm recorded,
        /// <c>"dynamics"</c> for one the console farm did — <c>run.json</c>'s <c>engine</c>.
        /// </summary>
        /// <remarks>
        /// <b>Absent means PhysX, and that is a reading of history rather than a default.</b>
        /// <c>EvolutionRun</c> never wrote the field because until 2026-09-22 there was only one
        /// engine, so every manifest without it was written by the Editor stepping PhysX. A run
        /// recorded by the console farm carries the word, and the two are not replayable by the
        /// same code: one needs a scene and an <c>ArticulationBody</c> per link, the other needs
        /// neither. Nothing here guesses — the word decides which replay is opened, and a word
        /// this build does not know is refused rather than assumed to be one of the two.
        /// </remarks>
        public string Engine { get; private set; }

        /// <summary>SHA-256 over <c>src/Evosim.Dynamics</c>, or null on a PhysX run.</summary>
        public string DynamicsHash { get; private set; }

        /// <summary>SHA-256 over <c>src/Evosim.Farm</c>, or null on a PhysX run.</summary>
        public string FarmHash { get; private set; }

        /// <summary>
        /// How many threads stepped the bodies, or null when the manifest does not say.
        /// </summary>
        /// <remarks>
        /// Pace and not a realisation — <c>DynamicsWorld.Step</c> takes one <c>Parallel.For</c>
        /// path at every count, including one, so the digests are equal at 1, 4 and 16. It is read
        /// and used anyway, because a replay that runs at the recording's own count is one fewer
        /// difference to argue about when a sample does part.
        /// </remarks>
        public int? Threads { get; private set; }

        /// <summary>
        /// The economy's step, from <c>run.json</c>'s <c>metabolicStepSeconds</c>, or 0.
        /// </summary>
        /// <remarks>
        /// Read but not obeyed: it is a constant in both engines (<c>Ecosystem</c>'s and
        /// <c>EnvBinding.MetabolicStepSeconds</c>, 0.5 s), and the replay derives its own steps per
        /// metabolic step from the physics step through the farm's own
        /// <c>EnvBinding.ResolvePhysicsStep</c>. What it is for is to catch a recording made by a
        /// build whose economy ran at another cadence, which would make every sample time
        /// meaningless without a single column saying so.
        /// </remarks>
        public double MetabolicStepSeconds { get; private set; }

        /// <summary>Every sample the run wrote, in order. Empty when it wrote none.</summary>
        public IReadOnlyList<RunSample> Samples { get; private set; }

        /// <summary>Why <c>stats.jsonl</c> could not be read, or null.</summary>
        public string SamplesNote { get; private set; }

        /// <summary>
        /// Reads a run directory. Throws with the reason when the directory is not one.
        /// </summary>
        public static RunRecord Load(string runDirectory) => Load(runDirectory, false, out _);

        /// <summary>
        /// The same, except that a config this build's strict reader refuses is read again by the
        /// picture-only reader — §11 of <c>logbook/specs/snapshot-render-spec.md</c>.
        /// </summary>
        /// <param name="oldRun">
        /// Why the strict reader refused and what the picture reader assumed, or null when the
        /// strict reader was enough. The caller says so in the log and on the label; nothing that
        /// simulates may take this path.
        /// </param>
        /// <remarks>
        /// Strict first and tolerant second, which is the whole of the safety here: a run this
        /// build recorded is read by the same code it always was, and the difference shows up as
        /// a string rather than as a silently different picture.
        /// </remarks>
        public static RunRecord LoadForPicture(string runDirectory, out string oldRun) =>
            Load(runDirectory, true, out oldRun);

        private static RunRecord Load(string runDirectory, bool forPicture, out string oldRun)
        {
            oldRun = null;

            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("No run directory given.", nameof(runDirectory));
            }

            string dir = ResolveRunDirectory(runDirectory);

            var record = new RunRecord { Path = dir };

            try
            {
                record.Config = RunDirectory.ReadConfig(dir, out string hashMismatch);
                record.ConfigHashMismatch = hashMismatch;
            }
            catch (Exception refusal) when (forPicture)
            {
                record.Config = PictureConfig.Read(dir, out string absent);

                oldRun =
                    "the strict config reader refused it (" + refusal.Message.Split('\n')[0] +
                    "); the picture-only reader took the shape, the size, the floor, the patches " +
                    "and the development limits" +
                    (absent != null ? ". Absent from the file: " + absent : ", all of them present");
            }

            string manifestPath = System.IO.Path.Combine(dir, "run.json");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException(
                    $"No run.json in '{dir}'. The seed and the physics step live there and " +
                    "nowhere else that a replay can trust, so a run without one cannot be " +
                    "replayed faithfully.", manifestPath);
            }

            JsonNode manifest = Json.Parse(File.ReadAllText(manifestPath));

            record.Seed = manifest["seed"].AsULong();
            record.PhysicsDtSeconds = manifest["physicsDtSeconds"].AsFloat();
            record.ArmName = manifest.OptionalString("arm", null);
            record.ConfigHash = manifest.OptionalString("configHash", null);
            record.UnityVersion = manifest.OptionalString("unityVersion", null);
            record.Status = manifest.OptionalString("status", "unknown");

            // See the property's remarks: absent is PhysX because there was nothing else to be.
            record.Engine = manifest.OptionalString("engine", "physx");

            if (manifest.Has("threads")) record.Threads = manifest["threads"].AsInt();

            record.MetabolicStepSeconds =
                manifest.Has("metabolicStepSeconds") ? manifest["metabolicStepSeconds"].AsDouble() : 0d;

            // Optional on purpose: every run recorded before D078 has no such field, and refusing
            // them would make the theatre useless on the whole record to date. Absent stays absent
            // — the replay decides what to do about it and the overlay says which case it is.
            if (manifest.Has("physicsJobWorkers"))
            {
                record.PhysicsJobWorkers = manifest["physicsJobWorkers"].AsInt();
            }

            if (manifest.Has("jobWorkerMaximum"))
            {
                record.JobWorkerMaximum = manifest["jobWorkerMaximum"].AsInt();
            }

            if (manifest.Has("requestedSeconds"))
            {
                record.RequestedSeconds = manifest["requestedSeconds"].AsDouble();
            }

            if (manifest.Has("source"))
            {
                JsonNode source = manifest["source"];
                record.GitCommit = source.OptionalString("gitCommit", null);
                record.GitDirty = source.Has("gitDirty") && source["gitDirty"].Kind ==
                                  JsonNode.NodeKind.Bool && source["gitDirty"].AsBool();
                record.CoreHash = source.OptionalString("coreHash", null);
                record.SimHash = source.OptionalString("simHash", null);

                // The dynamics engine's two halves. A PhysX manifest has neither, and a dynamics
                // manifest has no simHash — there is no Assets/Evosim in a run the Editor never
                // touched — which is why the identity check has to be told which engine it is
                // asking about rather than comparing whatever hashes it happens to find.
                record.DynamicsHash = source.OptionalString("dynamicsHash", null);
                record.FarmHash = source.OptionalString("farmHash", null);
            }

            // The config carries the step too, since it became a tunable. Both are reported and
            // the manifest wins, because it is what the solver was configured with. Not asked of
            // a picture-only config, which never read the physics group and would report a
            // disagreement with a number nobody wrote.
            float configured = oldRun == null ? record.Config.PhysicsStepSeconds : record.PhysicsDtSeconds;
            if (Math.Abs(configured - record.PhysicsDtSeconds) > 1e-9f)
            {
                record.StepDisagreement =
                    $"run.json says the step was {record.PhysicsDtSeconds} s and config.json says " +
                    $"{configured} s. Replaying at run.json's, which is the one the solver used.";
            }

            record.Samples = ReadSamples(dir, out string note);
            record.SamplesNote = note;

            return record;
        }

        /// <summary>
        /// The engine word alone, read without validating anything else in the run.
        /// </summary>
        /// <remarks>
        /// For the one caller that has to choose a replay before it has one: a full
        /// <see cref="Load"/> refuses a config this build cannot read, and which engine a run was
        /// stepped by is exactly the question a viewer needs answered before it can say why. Any
        /// failure at all returns <c>"physx"</c>, because the caller's next move is to open the
        /// PhysX replay, which will refuse the run properly and with the real reason.
        /// </remarks>
        public static string PeekEngine(string runDirectory)
        {
            try
            {
                string dir = ResolveRunDirectory(runDirectory);
                string path = System.IO.Path.Combine(dir, "run.json");
                if (!File.Exists(path)) return "physx";

                return Json.Parse(File.ReadAllText(path)).OptionalString("engine", "physx");
            }
            catch
            {
                return "physx";
            }
        }

        /// <summary>
        /// Accepts either a run directory or the arm directory above it (<c>runs/th-ref</c>),
        /// which is what a person has to hand.
        /// </summary>
        /// <remarks>
        /// An arm directory holds one timestamped run per launch. Picking the newest is the
        /// convenience; naming the run directory is always unambiguous, and an arm directory with
        /// several runs in it says which one it took.
        /// </remarks>
        public static string ResolveRunDirectory(string path)
        {
            string dir = System.IO.Path.GetFullPath(path.Trim().Trim('"'));

            if (!Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException($"No directory at '{dir}'.");
            }

            if (File.Exists(System.IO.Path.Combine(dir, "config.json"))) return dir;

            string newest = null;
            DateTime newestAt = DateTime.MinValue;

            foreach (string child in Directory.GetDirectories(dir))
            {
                if (!File.Exists(System.IO.Path.Combine(child, "config.json"))) continue;

                DateTime at = Directory.GetLastWriteTimeUtc(child);
                if (at <= newestAt) continue;

                newest = child;
                newestAt = at;
            }

            if (newest == null)
            {
                throw new FileNotFoundException(
                    $"No config.json in '{dir}' and no run directory under it.",
                    System.IO.Path.Combine(dir, "config.json"));
            }

            return newest;
        }

        private static IReadOnlyList<RunSample> ReadSamples(string dir, out string note)
        {
            note = null;
            var samples = new List<RunSample>();

            string path = System.IO.Path.Combine(dir, "stats.jsonl");
            if (!File.Exists(path))
            {
                note = "no stats.jsonl: nothing to check the replay against";
                return samples;
            }

            string[] rows;
            try
            {
                // ReadRows, not ReadAllLines: the latter opens with FileShare.Read and will not
                // coexist with a live run's writer.
                rows = JsonlWriter.ReadRows(path);
            }
            catch (Exception e)
            {
                note = "stats.jsonl unreadable: " + e.Message;
                return samples;
            }

            int malformed = 0;

            foreach (string row in rows)
            {
                if (string.IsNullOrWhiteSpace(row)) continue;

                try
                {
                    JsonNode n = Json.Parse(row);
                    bool hasMatter = n.Has("matterResidual");

                    samples.Add(new RunSample
                    {
                        T = n["t"].AsDouble(),
                        Alive = n["alive"].AsInt(),
                        Births = (long)n["births"].AsDouble(),
                        Deaths = (long)n["deaths"].AsDouble(),
                        AuditResidual = n["auditResidual"].AsDouble(),
                        MeanHeight = n["meanHeight"].AsDouble(),

                        // Optional, like every column added after a run was recorded: the matter
                        // identity arrived with the vertex field (2026-09-07) and every stats row
                        // written before it has no such field.
                        MatterResidual = hasMatter ? n["matterResidual"].AsDouble() : 0d,
                        MatterResidualRecorded = hasMatter,
                    });
                }
                catch
                {
                    malformed++;
                }
            }

            if (samples.Count == 0)
            {
                note = "stats.jsonl held no readable samples: nothing to check the replay against";
            }
            else if (malformed > 0)
            {
                note = malformed + " stats row(s) unreadable and skipped";
            }

            return samples;
        }
    }
}
