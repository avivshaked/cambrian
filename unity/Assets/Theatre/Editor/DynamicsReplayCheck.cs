using System;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The reading behind Mode B on the second engine: replay a run the console farm recorded,
    /// inside the Editor, and compare every sample against the run's own <c>stats.jsonl</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Edit mode, and not because edit mode is convenient.</b>
    /// <c>TheatreIdentityCheck</c>'s edit-mode entry passed for three days while the Play-mode
    /// replay was parting from its recording (logbook/0083), because it drove PhysX from a loop of
    /// its own rather than letting the engine drive it. That fault cannot be made here: nothing in
    /// this replay is Unity's. There is no <c>FixedUpdate</c>, no simulation mode, no job system
    /// and no scene — <see cref="Evosim.Farm.Simulation"/> steps Core's world with
    /// <c>Evosim.Dynamics</c>, and a loop calling <c>Step</c> is the only way it is ever stepped,
    /// in the Editor and on the farm alike. So an edit-mode verdict here is the whole verdict.
    /// </para>
    /// <para>
    /// <b>What a mismatch would mean.</b> The solver is bit-identical at any thread count by
    /// construction, and this replay runs at the recording's own count anyway. What is left that
    /// the Editor cannot hold equal is the JIT: every .NET project in this repository that reports
    /// a digest sets <c>TieredCompilation=false</c>, and Unity's Mono offers no such switch. Mono
    /// is single-tier, so the expectation is that the numbers come out the same; this entry is
    /// what turns the expectation into a reading, and a difference that shows up first in
    /// <c>auditResidual</c> or <c>meanHeight</c> while the three counts still agree is what a
    /// rounding edge would look like.
    /// </para>
    /// <code>
    /// $env:EVOSIM_THEATRE_RUN = "$PWD/scratch/dyn-replay/runs/dynsmoke"
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.DynamicsReplayCheck.Run',
    ///   '-logFile', "$PWD/scratch/logs/dynamics-replay.log")
    /// </code>
    /// </remarks>
    public static class DynamicsReplayCheck
    {
        private const string Tag = "[DynamicsReplayCheck] ";

        [MenuItem("Evosim/Theatre/Dynamics Replay Check")]
        public static void Run()
        {
            int code = 1;

            try
            {
                code = Check() ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "failed: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static bool Check()
        {
            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");

            if (string.IsNullOrWhiteSpace(run))
            {
                Debug.LogError(
                    Tag + "EVOSIM_THEATRE_RUN names no run. Point it at a run directory the " +
                    "console farm recorded, or at the arm directory above it.");

                return false;
            }

            bool allowMismatch = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_OVERRIDE") == "1";
            double wallMinutes = Minutes("EVOSIM_THEATRE_WALL_MINUTES", 30d);

            TheatreDynamicsReplay replay =
                TheatreDynamicsReplay.Open(run, allowMismatch, out string refusal);

            if (replay == null)
            {
                Debug.LogError(Tag + "refused: " + refusal);
                return false;
            }

            using (replay)
            {
                RunRecord record = replay.Record;

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "{0} seed {1}, engine {2}, dt {3} s, threads {4}, config {5}, " +
                    "{6} recorded samples through t={7:0.#} s — {8}",
                    record.ArmName ?? "run", record.Seed, record.Engine, record.PhysicsDtSeconds,
                    replay.Threads, record.ConfigHash, record.Samples.Count,
                    replay.RecordedThroughSeconds,
                    replay.Faithful
                        ? "same source as the recording"
                        : "SOURCE DIFFERS: " + replay.SourceDifference));

                if (record.SamplesNote != null) Debug.LogWarning(Tag + record.SamplesNote);
                if (record.ConfigHashMismatch != null) Debug.LogWarning(Tag + record.ConfigHashMismatch);
                if (record.StepDisagreement != null) Debug.LogWarning(Tag + record.StepDisagreement);

                if (record.Samples.Count == 0)
                {
                    Debug.LogError(Tag + "nothing to check the replay against.");
                    return false;
                }

                double through = replay.RecordedThroughSeconds;
                var clock = Stopwatch.StartNew();
                bool timedOut = false;
                bool extinct = false;

                while (replay.ElapsedSeconds < through - 1e-9)
                {
                    if (clock.Elapsed.TotalMinutes >= wallMinutes)
                    {
                        timedOut = true;
                        break;
                    }

                    if (!replay.Step(out bool compared, out string difference, out RunSample recorded))
                    {
                        continue;
                    }

                    if (compared) Say(replay, recorded, difference);

                    // Program.Loop's own break, in Program.Loop's own place: the farm ends a run
                    // the step a world empties, so a recording of one has no sample after it and
                    // stepping an empty world here would only burn the wall.
                    if (replay.Sim.World.Living.Count == 0)
                    {
                        extinct = true;
                        break;
                    }
                }

                ReplayIdentity identity = replay.Identity;

                bool whole = identity.Compared == identity.Recorded;
                bool clean = identity.FirstMismatch == null && identity.Skipped == 0;

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "replayed to t={0:0.#} s in {1:0.#} min, {2} physics steps{3}{4}",
                    replay.ElapsedSeconds, clock.Elapsed.TotalMinutes, replay.Steps,
                    timedOut ? ", WALL REACHED" : "", extinct ? ", world empty" : ""));

                Debug.Log(Tag + "by column: " + identity.ByColumn());

                if (clean && whole)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        Tag + "identical {0} of {1} samples", identity.Matched, identity.Recorded));

                    return !timedOut;
                }

                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    Tag + "identical {0} of {1} samples ({2} compared, {3} skipped){4}",
                    identity.Matched, identity.Recorded, identity.Compared, identity.Skipped,
                    identity.FirstMismatch != null ? " — first difference " + identity.FirstMismatch : ""));

                return false;
            }
        }

        /// <summary>One line per recorded sample: the five columns, and whether they agreed.</summary>
        /// <remarks>
        /// The whole census is printed on a matching sample too, not only on a differing one. A
        /// log that says nothing until something breaks cannot be read backwards afterwards to
        /// find where a world started to look strange, and thirty lines is what a smoke costs.
        /// </remarks>
        private static void Say(TheatreDynamicsReplay replay, RunSample recorded, string difference)
        {
            WorldCensus c = replay.Census;

            // The two doubles as a gap and not only as a verdict. A replay that parts in the
            // fifteenth decimal and one that parts in the third are the same word — MISMATCH — and
            // completely different findings, and the gap is what tells them apart at a glance.
            string line = string.Format(
                CultureInfo.InvariantCulture,
                Tag + "t={0,7:0.#}  alive {1,5}  births {2,6}  deaths {3,6}  audit {4:R}  " +
                "mean depth {5:R}  d(audit) {6:0.###e+0}  d(depth) {7:0.###e+0}  {8}",
                c.T, c.Alive, c.Births, c.Deaths, c.AuditResidual, c.MeanHeight,
                c.AuditResidual - recorded.AuditResidual, c.MeanHeight - recorded.MeanHeight,
                difference == null ? "ok" : "MISMATCH: " + difference);

            if (difference == null) Debug.Log(line);
            else Debug.LogError(line);
        }

        private static double Minutes(string name, double fallback)
        {
            string set = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrWhiteSpace(set) &&
                   double.TryParse(set, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) &&
                   v > 0d
                ? v
                : fallback;
        }
    }
}
