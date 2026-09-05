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

        public string ArmName { get; private set; }
        public string ConfigHash { get; private set; }
        public string UnityVersion { get; private set; }
        public string Status { get; private set; }
        public string GitCommit { get; private set; }
        public bool GitDirty { get; private set; }
        public string CoreHash { get; private set; }
        public string SimHash { get; private set; }

        /// <summary>Every sample the run wrote, in order. Empty when it wrote none.</summary>
        public IReadOnlyList<RunSample> Samples { get; private set; }

        /// <summary>Why <c>stats.jsonl</c> could not be read, or null.</summary>
        public string SamplesNote { get; private set; }

        /// <summary>
        /// Reads a run directory. Throws with the reason when the directory is not one.
        /// </summary>
        public static RunRecord Load(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("No run directory given.", nameof(runDirectory));
            }

            string dir = ResolveRunDirectory(runDirectory);

            var record = new RunRecord { Path = dir };

            record.Config = RunDirectory.ReadConfig(dir, out string hashMismatch);
            record.ConfigHashMismatch = hashMismatch;

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

            if (manifest.Has("source"))
            {
                JsonNode source = manifest["source"];
                record.GitCommit = source.OptionalString("gitCommit", null);
                record.GitDirty = source.Has("gitDirty") && source["gitDirty"].Kind ==
                                  JsonNode.NodeKind.Bool && source["gitDirty"].AsBool();
                record.CoreHash = source.OptionalString("coreHash", null);
                record.SimHash = source.OptionalString("simHash", null);
            }

            // The config carries the step too, since it became a tunable. Both are reported and
            // the manifest wins, because it is what the solver was configured with.
            float configured = record.Config.PhysicsStepSeconds;
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
                    samples.Add(new RunSample
                    {
                        T = n["t"].AsDouble(),
                        Alive = n["alive"].AsInt(),
                        Births = (long)n["births"].AsDouble(),
                        Deaths = (long)n["deaths"].AsDouble(),
                        AuditResidual = n["auditResidual"].AsDouble(),
                        MeanHeight = n["meanHeight"].AsDouble(),
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
