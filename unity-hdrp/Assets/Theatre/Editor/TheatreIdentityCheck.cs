using System;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Mode B's identity check with the rendering taken away: does replaying a recorded run
    /// reproduce it, row for row? Twice, in the two loops the theatre can run in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the edit-mode check validates and what it cannot.</b> <see cref="Run"/> runs the
    /// theatre's loader, its world construction and its comparison against <c>stats.jsonl</c> in
    /// the same mode the recording was made in: an editor <c>-executeMethod</c>, batch, no
    /// graphics, one physics step per iteration of a <c>while</c> loop it owns. So a pass there
    /// says the theatre rebuilds the recorded world exactly, from a loop that is not the theatre's.
    /// </para>
    /// <para>
    /// <b>Which is how it missed the fault it exists to catch.</b> On 2026-09-10 the owner watched
    /// round 33 seed 3 in Play mode and the replay parted from its recording in the fourth figure
    /// of the audit between 100 s and 200 s, while this check had been passing (logbook/0083). The
    /// difference was Play mode itself: <c>Object.Destroy</c> is deferred to the end of the frame,
    /// a frame in the theatre is tens of physics steps, and a dead creature therefore stayed in
    /// the water as a collider that newborns were placed inside. The destroy is immediate in both
    /// modes now (<c>PhenotypeInstance.Destroy</c>), and <see cref="RunInPlayMode"/> is the check
    /// that would have seen it: the real <c>TheatreRunner</c>, the real <c>Update</c>, the real
    /// frame, in Play mode, with the identity comparison reported at every sample.
    /// </para>
    /// <para>
    /// <b>Both write nothing.</b> Not into the run directory (Mode B never does) and not anywhere
    /// else; the Play-mode entry carries its request across the domain reload in
    /// <c>SessionState</c>, which lives in the Editor process and touches no disk at all. The
    /// result is the log, which <c>run-arm.ps1</c>'s convention puts in <c>scratch/logs/</c>.
    /// </para>
    /// <code>
    /// # edit mode: one step per iteration, and -quit is safe
    /// $env:EVOSIM_THEATRE_RUN = 'D:\...\runs\th-ref'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', $proj, '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run',
    ///   '-logFile', 'scratch/logs/theatre-identity.log')
    ///
    /// # Play mode: NO -quit. The check enters Play mode, drives the runner, and exits the
    /// # Editor itself with 0 or 1 when the verdict is in.
    /// $env:EVOSIM_THEATRE_RUN = 'D:\...\runs\th-ref'
    /// $env:EVOSIM_THEATRE_SAMPLES = '20'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', $proj, '-batchmode', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.RunInPlayMode',
    ///   '-logFile', 'scratch/logs/theatre-identity-play.log')
    /// </code>
    /// </remarks>
    public static class TheatreIdentityCheck
    {
        // ------------------------------------------------------------------ the edit-mode check

        [MenuItem("Evosim/Theatre — check a replay against its record (edit mode)")]
        public static void FromMenu() => Check();

        /// <summary>Batchmode entry point: exits 0 when the replay matched, 1 when it did not.</summary>
        /// <remarks>
        /// <c>EVOSIM_THEATRE_PLAYMODE=1</c> sends it to <see cref="RunInPlayMode"/> instead, so a
        /// launcher that already sets the theatre's environment can ask for the other loop without
        /// changing the <c>-executeMethod</c> it passes. Remember to drop <c>-quit</c> when it
        /// does: Play mode needs frames, and <c>-quit</c> ends the process as soon as this method
        /// returns.
        /// </remarks>
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_PLAYMODE") == "1")
            {
                RunInPlayMode();
                return;
            }

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

            double until = SecondsFromEnvironment();

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
                    $"  physics jobs: {replay.PhysicsJobWorkers}" +
                    $"{(replay.ThreadCaveat != null ? " — " + replay.ThreadCaveat : ", as recorded")}\n" +
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
                string verdict = Verdict(replay);

                Debug.Log(
                    "[Theatre] identity check (edit mode, one step per iteration of this " +
                    "check's own loop): " + verdict + "\n" +
                    $"  {replay.SamplesMatched} matched, {replay.SamplesSkipped} skipped, " +
                    $"of {replay.Record.Samples.Count} recorded\n" +
                    $"  replayed {replay.ElapsedSeconds:0.#} s in " +
                    $"{clock.Elapsed.TotalMinutes:0.##} min " +
                    $"({replay.ElapsedSeconds / Math.Max(1e-9, clock.Elapsed.TotalSeconds):0.#}x real time), " +
                    $"{replay.Census.Alive} alive at the end\n" +
                    "  columns compared: alive, births, deaths, auditResidual, meanHeight\n" +
                    "  what this cannot see: anything that only happens when a frame carries " +
                    "many steps. Use RunInPlayMode for that.");

                if (!matched)
                {
                    Debug.LogError("[Theatre] identity check (edit mode) FAILED: " + verdict);
                }

                return matched;
            }
        }

        private static string Verdict(TheatreReplay replay) =>
            replay.Record.Samples.Count == 0
                ? "NO RECORD to check against"
                : replay.FirstMismatch != null
                    ? "MISMATCH — " + replay.FirstMismatch
                    : replay.SamplesMatched == 0
                        ? "no sample was reached"
                        : $"identical on all {replay.SamplesMatched} samples compared";

        private static double SecondsFromEnvironment()
        {
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SECONDS");

            return !string.IsNullOrEmpty(text) &&
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
                       out double parsed)
                ? parsed
                : double.PositiveInfinity;
        }

        // ------------------------------------------------------------------ the Play-mode check

        /// <summary>The pending request, carried across the domain reload Play mode causes.</summary>
        /// <remarks>
        /// <c>SessionState</c> rather than <c>EditorPrefs</c> or a file: it lives for this Editor
        /// process and writes nothing anywhere, and this project does not put its files outside
        /// the repository. It survives a domain reload, which is the only thing that is needed.
        /// </remarks>
        private const string PendingKey = "Evosim.Theatre.IdentityCheck.Pending";

        /// <summary>The verdict's exit code, set once the check is done and the Editor is leaving.</summary>
        private const string ExitKey = "Evosim.Theatre.IdentityCheck.Exit";

        private static int _samples;
        private static double _wallSecondsAllowed;
        private static double _deadline;
        private static int _reportedSamples;
        private static bool _paceSet;
        private static TheatreRunner _runner;
        private static bool _driving;

        [MenuItem("Evosim/Theatre — check a replay against its record (Play mode)")]
        public static void FromMenuInPlayMode() => RunInPlayMode();

        /// <summary>
        /// Batchmode entry point for the Play-mode check. <b>Launch it without <c>-quit</c>.</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// What it does is press Play and then watch: the scene's own <c>TheatreRunner</c> opens
        /// the run from <c>EVOSIM_THEATRE_RUN</c> in its <c>Start</c> and steps it from its
        /// <c>Update</c>, many steps to a frame, exactly as it does under a person's hands. This
        /// method contributes no loop of its own. That is the whole point: the fault this exists
        /// for lived in the frame, not in the step (logbook/0083).
        /// </para>
        /// <para>
        /// <c>EVOSIM_THEATRE_SAMPLES</c> sets how many samples to compare (default 20, which at a
        /// hundred simulated seconds a sample covers the window the recording parted in);
        /// <c>EVOSIM_THEATRE_WALL_MINUTES</c> caps the wall clock (default 30) so that a batch
        /// Editor cannot sit in Play mode for ever. Every other setting is the runner's own
        /// environment: <c>EVOSIM_THEATRE_RUN</c>, <c>EVOSIM_THEATRE_SEEK</c>,
        /// <c>EVOSIM_THEATRE_OVERRIDE</c>.
        /// </para>
        /// </remarks>
        public static void RunInPlayMode()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN")))
            {
                Debug.LogError("[Theatre] EVOSIM_THEATRE_RUN is not set: nothing to check.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Theatre] already in Play mode: stop it before asking for this.");
                return;
            }

            _samples = IntFromEnvironment("EVOSIM_THEATRE_SAMPLES", 20, 1, 100000);
            _wallSecondsAllowed = 60d * IntFromEnvironment("EVOSIM_THEATRE_WALL_MINUTES", 30, 1, 1440);

            // The scene has to be the theatre's, because the check reads the verdict off the
            // component in it. Rebuilt rather than refused if it is missing: it is generated, and
            // a batch run that stopped to say "open a scene first" would be a worse answer. If it
            // is already open, it is left alone, so running this from the menu does not throw away
            // an unsaved scene someone was working in.
            if (SceneManager.GetActiveScene().path != TheatreSceneBuilder.ScenePath)
            {
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                }

                if (System.IO.File.Exists(TheatreSceneBuilder.ScenePath))
                {
                    EditorSceneManager.OpenScene(TheatreSceneBuilder.ScenePath, OpenSceneMode.Single);
                }
                else if (!TheatreSceneBuilder.Build())
                {
                    Debug.LogError("[Theatre] no theatre scene, and it could not be built.");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
            }

            Debug.Log(
                $"[Theatre] Play-mode identity check: {_samples} sample(s), " +
                $"{_wallSecondsAllowed / 60d:0} min of wall clock at most. Entering Play mode.");

            // Invariant on the way out as well as on the way back in: a machine whose decimal
            // separator is a comma would otherwise write a number this cannot read.
            SessionState.SetString(
                PendingKey,
                _samples.ToString(CultureInfo.InvariantCulture) + "|" +
                _wallSecondsAllowed.ToString("R", CultureInfo.InvariantCulture));

            // Armed here as well as after the reload, because a project with domain reloading
            // turned off in its Enter Play Mode Options never reaches the reload path, and an
            // unarmed check in a batch Editor is a process that sits in Play mode until somebody
            // notices. Arming twice is harmless: the reload wipes the subscription this makes.
            Arm();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Picks the check back up on the other side of a domain reload.
        /// </summary>
        /// <remarks>
        /// Entering Play mode reloads the domain, which wipes every static field and every
        /// subscription this class had. So the request lives in <c>SessionState</c> and this runs
        /// on each reload to read it back: while a check is pending, drive it; once a verdict has
        /// been reached and Play mode has been left, quit with its code. A reload in the middle of
        /// a check (a script recompiled while playing) lands here too, and simply carries on.
        /// </remarks>
        [InitializeOnLoadMethod]
        private static void ResumeAcrossTheDomainReload()
        {
            string exit = SessionState.GetString(ExitKey, "");

            if (!string.IsNullOrEmpty(exit))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                SessionState.EraseString(ExitKey);
                Quit(int.TryParse(exit, out int code) ? code : 1);
                return;
            }

            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;

            string[] fields = pending.Split('|');
            _samples = fields.Length > 0 && int.TryParse(fields[0], out int n) ? n : 20;
            _wallSecondsAllowed =
                fields.Length > 1 &&
                double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double wall)
                    ? wall
                    : 1800d;

            Arm();
        }

        /// <summary>Puts the driver on the editor's update loop and starts its wall clock.</summary>
        /// <remarks>
        /// The clock is fresh on each arming rather than carried across, so a recompile in the
        /// middle of a check grants a fresh window instead of ending one that was going to pass.
        /// </remarks>
        private static void Arm()
        {
            _deadline = EditorApplication.timeSinceStartup + _wallSecondsAllowed;
            _reportedSamples = 0;
            _paceSet = false;
            _runner = null;

            if (_driving) return;

            _driving = true;
            EditorApplication.update += Drive;
        }

        /// <summary>One editor tick of the Play-mode check: read the runner, report, decide.</summary>
        private static void Drive()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Finish(1, "TIMED OUT before the samples were compared");
                    return;
                }

                // Play mode takes a moment to start. Nothing to read until it has.
                if (!EditorApplication.isPlaying) return;

                if (_runner == null)
                {
                    _runner = UnityEngine.Object.FindFirstObjectByType<TheatreRunner>();

                    if (_runner == null)
                    {
                        Finish(1, "no TheatreRunner in the scene: nothing is playing the run");
                        return;
                    }
                }

                TheatreReplay replay = _runner.Replay;

                if (replay == null)
                {
                    // Start may not have run yet on the first tick; an error means it has, and
                    // that the run was refused.
                    if (!string.IsNullOrEmpty(_runner.Error))
                    {
                        Finish(1, "refused: " + _runner.Error);
                    }

                    return;
                }

                if (!_paceSet) SetThePace(replay);

                int compared = replay.SamplesMatched + replay.SamplesSkipped;

                if (compared > _reportedSamples)
                {
                    _reportedSamples = compared;
                    Debug.Log(
                        $"[Theatre] t={replay.ElapsedSeconds:0.#} s, alive {replay.Census.Alive}, " +
                        $"{replay.IdentityLine()}");
                }

                if (replay.FirstMismatch != null)
                {
                    Finish(1, "MISMATCH — " + replay.FirstMismatch);
                    return;
                }

                if (compared >= _samples)
                {
                    Finish(0, $"identical on all {replay.SamplesMatched} samples compared");
                    return;
                }

                if (replay.ElapsedSeconds >= replay.RecordedThroughSeconds)
                {
                    bool matched = replay.SamplesMatched > 0;
                    Finish(
                        matched ? 0 : 1,
                        matched
                            ? $"the record ran out after {replay.SamplesMatched} matching sample(s)"
                            : "the record ran out before a sample was reached");
                }
            }
            catch (Exception e)
            {
                Finish(1, e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Puts the runner in the state the check needs: unpaused, and stepping as hard as the
        /// frame budget allows.
        /// </summary>
        /// <remarks>
        /// A high rate and a fat frame budget are not impatience, they are the condition under
        /// test. A frame that carries one step would hide exactly the class of fault this check
        /// exists for; the theatre a person watches carries tens, and so must this.
        /// </remarks>
        private static void SetThePace(TheatreReplay replay)
        {
            _paceSet = true;

            _runner.Paused = false;
            _runner.Rate = 10000f;
            _runner.FrameBudgetSeconds = 0.25f;
            _runner.ShowOverlay = false;

            Debug.Log(
                $"[Theatre] {replay.Record.Path}\n" +
                $"  arm {replay.Record.ArmName}, seed {replay.Record.Seed}, " +
                $"dt {replay.Record.PhysicsDtSeconds}, config {replay.Record.ConfigHash}\n" +
                $"  source: {(replay.Faithful ? "identical to the recording" : replay.SourceDifference)}\n" +
                $"  physics jobs: {replay.PhysicsJobWorkers}" +
                $"{(replay.ThreadCaveat != null ? " — " + replay.ThreadCaveat : ", as recorded")}\n" +
                $"  {replay.Record.Samples.Count} recorded samples, last at " +
                $"t={replay.RecordedThroughSeconds:0.#} s; comparing {_samples} of them in Play mode");
        }

        /// <summary>Writes the verdict, stops Play mode, and arranges the exit code.</summary>
        private static void Finish(int code, string verdict)
        {
            EditorApplication.update -= Drive;
            _driving = false;
            SessionState.EraseString(PendingKey);

            TheatreReplay replay = _runner != null ? _runner.Replay : null;

            string detail = replay == null
                ? ""
                : $"\n  {replay.SamplesMatched} matched, {replay.SamplesSkipped} skipped, " +
                  $"of {replay.Record.Samples.Count} recorded\n" +
                  $"  replayed {replay.ElapsedSeconds:0.#} s, {replay.Census.Alive} alive at the end\n" +
                  "  columns compared: alive, births, deaths, auditResidual, meanHeight";

            Debug.Log(
                "[Theatre] identity check (Play mode, the runner's own frame): " + verdict + detail);

            if (code != 0)
            {
                Debug.LogError("[Theatre] identity check (Play mode) FAILED: " + verdict);
            }

            _runner = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Quit(code);
                return;
            }

            // Leave Play mode first. The Editor is a worse thing to quit out of mid-play than it
            // is to wait one state change for, and the exit code has to survive the domain reload
            // that leaving causes, hence the second key and the two ways back to Quit: one for a
            // project that reloads the domain on exiting Play, one for a project that does not.
            SessionState.SetString(ExitKey, code.ToString(CultureInfo.InvariantCulture));
            EditorApplication.playModeStateChanged += QuitOnLeavingPlayMode;
            EditorApplication.isPlaying = false;
        }

        private static void QuitOnLeavingPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= QuitOnLeavingPlayMode;

            string exit = SessionState.GetString(ExitKey, "");
            if (string.IsNullOrEmpty(exit)) return;

            SessionState.EraseString(ExitKey);
            Quit(int.TryParse(exit, out int code) ? code : 1);
        }

        /// <summary>
        /// Ends the batch Editor with the verdict's code, and does nothing at all in a person's.
        /// </summary>
        private static void Quit(int code)
        {
            if (!Application.isBatchMode)
            {
                Debug.Log($"[Theatre] Play-mode identity check finished with code {code}.");
                return;
            }

            EditorApplication.Exit(code);
        }

        private static int IntFromEnvironment(string name, int fallback, int least, int most)
        {
            string text = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrEmpty(text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, least, most);
        }
    }
}
