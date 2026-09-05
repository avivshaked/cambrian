using System;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Mode B's identity check with the rendering taken away: does replaying a recorded run
    /// reproduce it, row for row?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this validates and what it cannot.</b> It runs the theatre's loader, its world
    /// construction and its comparison against <c>stats.jsonl</c> in the same mode the recording
    /// was made in — an editor <c>-executeMethod</c>, batch, no graphics. So a pass here says the
    /// theatre rebuilds the recorded world exactly. It says nothing about Play mode with a camera
    /// in the scene, which is a different loop around the same <c>Ecosystem.Step</c>: that reading
    /// is the one the overlay gives a person watching, and it needs eyes.
    /// </para>
    /// <para>
    /// <b>It writes nothing.</b> Not into the run directory (Mode B never does) and not anywhere
    /// else — the result is the log, which <c>run-arm.ps1</c>'s convention puts in
    /// <c>scratch/logs/</c>.
    /// </para>
    /// <code>
    /// $env:EVOSIM_THEATRE_RUN = 'D:\...\runs\th-ref'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', $proj, '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run',
    ///   '-logFile', 'scratch/logs/theatre-identity.log')
    /// </code>
    /// </remarks>
    public static class TheatreIdentityCheck
    {
        [MenuItem("Evosim/Theatre — check a replay against its record")]
        public static void FromMenu() => Check();

        /// <summary>Batchmode entry point: exits 0 when the replay matched, 1 when it did not.</summary>
        public static void Run()
        {
            bool ok = Check();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Check()
        {
            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
            if (string.IsNullOrEmpty(run))
            {
                Debug.LogError("[Theatre] EVOSIM_THEATRE_RUN is not set: nothing to check.");
                return false;
            }

            bool allowMismatch = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_OVERRIDE") == "1";

            double until = double.PositiveInfinity;
            string untilText = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SECONDS");
            if (!string.IsNullOrEmpty(untilText) &&
                double.TryParse(untilText, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double parsed))
            {
                until = parsed;
            }

            TheatreReplay replay = TheatreReplay.Open(run, allowMismatch, out string refusal);

            if (replay == null)
            {
                Debug.LogError("[Theatre] refused: " + refusal);
                return false;
            }

            using (replay)
            {
                double target = Math.Min(until, replay.RecordedThroughSeconds);

                Debug.Log(
                    $"[Theatre] {replay.Record.Path}\n" +
                    $"  arm {replay.Record.ArmName}, seed {replay.Record.Seed}, " +
                    $"dt {replay.Record.PhysicsDtSeconds}, config {replay.Record.ConfigHash}\n" +
                    $"  source: {(replay.Faithful ? "identical to the recording" : replay.SourceDifference)}\n" +
                    $"  {replay.Record.Samples.Count} recorded samples, last at " +
                    $"t={replay.RecordedThroughSeconds:0.#} s; replaying to t={target:0.#} s");

                if (replay.Record.ConfigHashMismatch != null)
                {
                    Debug.LogWarning("[Theatre] " + replay.Record.ConfigHashMismatch);
                }

                if (replay.Record.StepDisagreement != null)
                {
                    Debug.LogWarning("[Theatre] " + replay.Record.StepDisagreement);
                }

                var clock = Stopwatch.StartNew();
                int reported = 0;

                while (replay.ElapsedSeconds < target)
                {
                    replay.Step();

                    // A line every thousand simulated seconds, so a long check is watchable in a
                    // tail rather than silent for hours — the same reason a run flushes its rows.
                    int thousand = (int)(replay.ElapsedSeconds / 1000d);
                    if (thousand <= reported) continue;

                    reported = thousand;
                    Debug.Log(
                        $"[Theatre] t={replay.ElapsedSeconds:0} s, alive {replay.Census.Alive}, " +
                        $"{replay.IdentityLine()}, " +
                        $"{replay.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds):0.#}x real time");
                }

                clock.Stop();

                bool matched = replay.FirstMismatch == null && replay.SamplesMatched > 0;

                string verdict =
                    replay.Record.Samples.Count == 0
                        ? "NO RECORD to check against"
                        : replay.FirstMismatch != null
                            ? "MISMATCH — " + replay.FirstMismatch
                            : replay.SamplesMatched == 0
                                ? "no sample was reached"
                                : $"identical on all {replay.SamplesMatched} samples compared";

                Debug.Log(
                    "[Theatre] identity check: " + verdict + "\n" +
                    $"  {replay.SamplesMatched} matched, {replay.SamplesSkipped} skipped, " +
                    $"of {replay.Record.Samples.Count} recorded\n" +
                    $"  replayed {replay.ElapsedSeconds:0.#} s in " +
                    $"{clock.Elapsed.TotalMinutes:0.##} min " +
                    $"({replay.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds):0.#}x real time), " +
                    $"{replay.Census.Alive} alive at the end\n" +
                    $"  columns compared: alive, births, deaths, auditResidual, meanHeight");

                if (!matched) Debug.LogError("[Theatre] identity check FAILED: " + verdict);

                return matched;
            }
        }
    }
}
