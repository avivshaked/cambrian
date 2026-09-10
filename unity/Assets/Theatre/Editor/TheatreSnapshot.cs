using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Pictures of a replayed world, at named simulated seconds, written where an agent can read
    /// them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> The owner opened round 33 in the theatre on 2026-09-10 and saw in
    /// a minute what thirty-three rounds of tables had not shown: the world was two vertical
    /// ribbons about a metre wide in a box twenty metres long, every clade a column packed round
    /// the spot its founder landed on (logbook/0083). Nothing in the record could have said so.
    /// The report carried a mean depth and per-patch bins and no number at all for where a body
    /// stood along the box, and the agent reading it cannot open the Editor to look. This entry
    /// gives that agent the owner's view as a file: four framed views of the world at each of
    /// several simulated seconds, as PNGs.
    /// </para>
    /// <para>
    /// <b>It is the Play-mode loop, not a loop of its own.</b> Shaped after
    /// <see cref="TheatreIdentityCheck.RunInPlayMode"/> and for its reason: the scene's own
    /// <c>TheatreRunner</c> opens the run and steps it from its <c>Update</c>, many steps to a
    /// frame, exactly as it does under a person's hands, and this class only watches the clock
    /// and takes the picture. The edit-mode identity check ran one step per iteration of its own
    /// loop and passed for three days while the Play-mode replay was wrong, because the fault
    /// lived in the frame and not in the step. A picture taken from a private loop would be a
    /// picture of a world nobody watches.
    /// </para>
    /// <para>
    /// <b>The identity check stays on, and the picture says what it found.</b> The runner
    /// compares the live world against the run's own <c>stats.jsonl</c> at every sample, so the
    /// log carries the verdict and the label burnt into each frame says NOT A FAITHFUL REPLAY
    /// when this build is not the build that recorded the run. A still of a cousin world under
    /// the arm's own name would be the quietest way this project could mislead itself.
    /// </para>
    /// <para>
    /// <b>It writes the pictures and nothing else.</b> Never into the run directory, and never
    /// outside the repository: an output directory that resolves outside the repository root is
    /// refused rather than written to, which is the owner's rule of 2026-09-03 turned into a
    /// precondition.
    /// </para>
    /// <code>
    /// # NO -quit, and NO -nographics: the entry quits, and rendering needs a graphics device.
    /// $env:EVOSIM_THEATRE_RUN = "$PWD/runs/r35tsmoke3"
    /// $env:EVOSIM_THEATRE_SNAP_TIMES = '300,600'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreSnapshot.Run',
    ///   '-logFile', "$PWD/scratch/logs/theatre-snap.log")
    /// </code>
    /// </remarks>
    public static class TheatreSnapshot
    {
        /// <summary>The pending request, carried across the domain reload Play mode causes.</summary>
        /// <remarks>
        /// <c>SessionState</c> rather than a file, for the reason the identity check gives: it
        /// lives in this Editor process, survives the reload, and touches no disk at all.
        /// </remarks>
        private const string PendingKey = "Evosim.Theatre.Snapshot.Pending";

        /// <summary>The exit code, once the pictures are taken and the Editor is leaving.</summary>
        private const string ExitKey = "Evosim.Theatre.Snapshot.Exit";

        /// <summary>
        /// How close to a target the runner is slowed down.
        /// </summary>
        /// <remarks>
        /// The runner steps until its frame budget runs out, so at ten thousand times real time a
        /// frame can carry eight simulated seconds and a picture asked for at 300 s would be
        /// taken at 308 s. Cruising to within this many seconds and then crawling costs a second
        /// or two of wall clock and puts the shot inside half a metabolic step of the second it
        /// was asked for. The label prints the second the world was actually at, never the one
        /// that was asked for.
        /// </remarks>
        private const double ApproachSeconds = 10d;

        private static double[] _times;
        private static SnapshotCamera.View[] _views;
        private static string _directory;
        private static int _width = 1600;
        private static int _height = 900;

        private static double _wallSecondsAllowed;
        private static double _deadline;
        private static int _next;
        private static bool _paceSet;
        private static bool _pastTheRecordSaid;
        private static TheatreRunner _runner;
        private static SnapshotCamera _camera;
        private static bool _driving;
        private static readonly List<string> _written = new List<string>();

        // ------------------------------------------------------------------ the batch entry

        /// <summary>
        /// Batchmode entry point. <b>Launch it without <c>-quit</c> and without
        /// <c>-nographics</c>.</b> It enters Play mode, drives nothing, photographs the world at
        /// each requested second and exits the Editor with 0 or 1.
        /// </summary>
        public static void Run()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN")))
            {
                Debug.LogError("[Theatre] EVOSIM_THEATRE_RUN is not set: nothing to photograph.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Theatre] already in Play mode: stop it before asking for this.");
                return;
            }

            string refusal = ReadTheRequest();

            if (refusal != null)
            {
                Debug.LogError("[Theatre] " + refusal);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // A graphics device is what a picture is made on, and -nographics takes it away. The
            // failure without this check is a null RenderTexture deep inside the first Capture,
            // which reads like a bug in the camera rather than like a wrong command line.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError(
                    "[Theatre] this Editor has no graphics device, so it cannot render anything. " +
                    "Drop -nographics from the command line; -batchmode on its own is right.");

                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (!OpenTheTheatreScene()) return;

            Debug.Log(
                "[Theatre] snapshot: " + _times.Length + " time(s), " + _views.Length +
                " view(s), " + _width + "x" + _height + ", into " +
                (_directory ?? "scratch/snaps/<arm>") + ". Entering Play mode.");

            // Invariant on the way out as well as back in: a machine whose decimal separator is a
            // comma would otherwise write a number this cannot read.
            SessionState.SetString(PendingKey, Pack());

            // Armed here as well as after the reload, because a project with domain reloading
            // turned off in its Enter Play Mode Options never reaches the reload path, and an
            // unarmed request in a batch Editor is a process that sits in Play mode for ever.
            Arm();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Reads the environment into the static request, or says what was wrong with it.
        /// </summary>
        /// <remarks>
        /// Refuses rather than defaults, on every field where a default would be a guess about
        /// what the caller meant. That is §9's rule for the config and it is worth as much here:
        /// a snapshot silently taken at the wrong second, or written somewhere nobody looks, is
        /// worse than one that did not happen.
        /// </remarks>
        private static string ReadTheRequest()
        {
            _times = TimesFrom(
                Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_TIMES"), out string wrong);

            if (_times == null) return "EVOSIM_THEATRE_SNAP_TIMES: " + wrong;

            _views = SnapshotCamera.ParseViews(
                Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_VIEWS"), out wrong);

            if (_views == null) return "EVOSIM_THEATRE_SNAP_VIEWS: " + wrong;

            if (!SizeFrom(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_SIZE"),
                    out _width, out _height, out wrong))
            {
                return "EVOSIM_THEATRE_SNAP_SIZE: " + wrong;
            }

            _directory = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_OUT");

            if (!string.IsNullOrWhiteSpace(_directory))
            {
                string inside = InsideTheRepository(_directory);
                if (inside != null) return "EVOSIM_THEATRE_SNAP_OUT: " + inside;
            }
            else
            {
                // Left null and resolved once the run is open, because the default carries the
                // arm's name and nothing knows it until the record has been read.
                _directory = null;
            }

            _wallSecondsAllowed =
                60d * IntFrom("EVOSIM_THEATRE_WALL_MINUTES", 30, 1, 1440);

            return null;
        }

        /// <summary>Ascending simulated seconds, all positive. The order is the caller's promise.</summary>
        private static double[] TimesFrom(string text, out string refusal)
        {
            refusal = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                refusal = "not set. Give it comma separated simulated seconds, e.g. 300,600.";
                return null;
            }

            string[] words = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var times = new List<double>(words.Length);

            foreach (string word in words)
            {
                if (!double.TryParse(word.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double t))
                {
                    refusal = "'" + word.Trim() + "' is not a number of seconds.";
                    return null;
                }

                if (!(t > 0d))
                {
                    refusal = "a time must be past zero; " + word.Trim() + " is not.";
                    return null;
                }

                if (times.Count > 0 && t <= times[times.Count - 1])
                {
                    refusal =
                        "times must ascend, and " + t.ToString("0.###", CultureInfo.InvariantCulture) +
                        " does not follow " +
                        times[times.Count - 1].ToString("0.###", CultureInfo.InvariantCulture) +
                        ". The world is stepped forward once, so it cannot go back for a picture.";
                    return null;
                }

                times.Add(t);
            }

            if (times.Count == 0)
            {
                refusal = "no time was named.";
                return null;
            }

            return times.ToArray();
        }

        private static bool SizeFrom(string text, out int width, out int height, out string refusal)
        {
            width = 1600;
            height = 900;
            refusal = null;

            if (string.IsNullOrWhiteSpace(text)) return true;

            string[] halves = text.Trim().Split('x', 'X');

            if (halves.Length != 2 ||
                !int.TryParse(halves[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
                !int.TryParse(halves[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
            {
                refusal = "'" + text + "' is not a size. Write it as WxH, e.g. 1600x900.";
                return false;
            }

            if (width < 64 || height < 64 ||
                width > SnapshotCamera.MaximumSide || height > SnapshotCamera.MaximumSide)
            {
                refusal =
                    "a side must be between 64 and " + SnapshotCamera.MaximumSide + " pixels.";
                return false;
            }

            return true;
        }

        /// <summary>Why the directory is not somewhere this may write, or null.</summary>
        /// <remarks>
        /// Nothing of the project's is written outside the repository, TEMP included: the owner's
        /// rule of 2026-09-03, made after the per-arm logs had lived in TEMP for weeks. A
        /// snapshot is the first thing here that takes a path from its caller, so it is the first
        /// thing that can break that rule by accident.
        /// </remarks>
        private static string InsideTheRepository(string directory)
        {
            string root, full;

            try
            {
                root = Path.GetFullPath(BuildIdentity.RepositoryRoot())
                    .TrimEnd(Path.DirectorySeparatorChar);

                full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }

            if (full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "'" + full + "' is outside the repository at '" + root +
                   "', and nothing of this project's is written outside it. Put the pictures " +
                   "under scratch/.";
        }

        private static bool OpenTheTheatreScene()
        {
            // The scene has to be the theatre's, because the runner in it is what plays the run.
            // Rebuilt rather than refused if it is missing, and left alone if it is already open,
            // so running this from the menu does not throw away an unsaved scene.
            if (SceneManager.GetActiveScene().path == TheatreSceneBuilder.ScenePath) return true;

            if (!Application.isBatchMode)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            if (File.Exists(TheatreSceneBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(TheatreSceneBuilder.ScenePath, OpenSceneMode.Single);
                return true;
            }

            if (TheatreSceneBuilder.Build()) return true;

            Debug.LogError("[Theatre] no theatre scene, and it could not be built.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }

        // ------------------------------------------------------------------ across the reload

        private static string Pack()
        {
            var times = new List<string>(_times.Length);
            foreach (double t in _times) times.Add(t.ToString("R", CultureInfo.InvariantCulture));

            var views = new List<string>(_views.Length);
            foreach (SnapshotCamera.View v in _views) views.Add(SnapshotCamera.NameOf(v));

            return string.Join(",", times.ToArray()) + "|" +
                   string.Join(",", views.ToArray()) + "|" +
                   (_directory ?? "") + "|" +
                   _width.ToString(CultureInfo.InvariantCulture) + "x" +
                   _height.ToString(CultureInfo.InvariantCulture) + "|" +
                   _wallSecondsAllowed.ToString("R", CultureInfo.InvariantCulture) + "|" +
                   _next.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Picks the request back up on the other side of the domain reload Play mode causes.
        /// </summary>
        /// <remarks>
        /// Entering Play mode wipes every static field and every subscription this class had, so
        /// the request lives in <c>SessionState</c> and this reads it back on each reload. A
        /// reload in the middle of a shoot, from a script recompiled while playing, lands here
        /// too and simply carries on from the next time not yet taken.
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
            if (fields.Length < 5) { SessionState.EraseString(PendingKey); return; }

            _times = TimesFrom(fields[0], out _);
            _views = SnapshotCamera.ParseViews(fields[1], out _);
            _directory = string.IsNullOrEmpty(fields[2]) ? null : fields[2];
            SizeFrom(fields[3], out _width, out _height, out _);

            _wallSecondsAllowed =
                double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double wall)
                    ? wall
                    : 1800d;

            // Which times are already on disk, so a recompile in the middle of a shoot does not
            // photograph the same second twice and does not walk the world backwards looking for
            // a second it has passed.
            _next = fields.Length > 5 &&
                    int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int taken)
                ? taken
                : 0;

            if (_times == null || _views == null) { SessionState.EraseString(PendingKey); return; }
            if (_next >= _times.Length) { SessionState.EraseString(PendingKey); return; }

            Arm();
        }

        /// <summary>Puts the driver on the editor's update loop and starts its wall clock.</summary>
        /// <remarks>
        /// The clock is fresh on each arming rather than carried across, so a recompile in the
        /// middle of a shoot grants a fresh window instead of ending one that was going to
        /// finish.
        /// </remarks>
        private static void Arm()
        {
            _deadline = EditorApplication.timeSinceStartup + _wallSecondsAllowed;
            _paceSet = false;
            _pastTheRecordSaid = false;
            _runner = null;

            if (_driving) return;

            _driving = true;
            EditorApplication.update += Drive;
        }

        // ------------------------------------------------------------------ the shoot

        /// <summary>One editor tick: read the clock, close on the next time, take the picture.</summary>
        private static void Drive()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Finish(1, "TIMED OUT with " + (_times.Length - _next) + " time(s) unphotographed");
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
                    if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                    return;
                }

                if (!_paceSet) SetThePace(replay);

                double target = _times[_next];
                double now = replay.ElapsedSeconds;

                if (now + 1e-9 < target)
                {
                    Approach(replay, target - now);
                    return;
                }

                _runner.Paused = true;
                Shoot(replay, target);
                _next++;
                SessionState.SetString(PendingKey, Pack());

                if (_next >= _times.Length)
                {
                    Finish(0, _written.Count + " picture(s) written");
                    return;
                }

                _runner.Paused = false;
            }
            catch (Exception e)
            {
                Finish(1, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Unpauses the runner and lets it step as hard as a frame allows.</summary>
        /// <remarks>
        /// The same pace the Play-mode identity check sets, and for the same reason: a frame that
        /// carried one step would not be the theatre a person watches, and the world in the
        /// picture must be the world the runner produces rather than one this class stepped by
        /// hand.
        /// </remarks>
        private static void SetThePace(TheatreReplay replay)
        {
            _paceSet = true;

            _runner.Paused = false;
            _runner.Rate = 10000f;
            _runner.FrameBudgetSeconds = 0.25f;
            _runner.ShowOverlay = false;

            Debug.Log(
                "[Theatre] " + replay.Record.Path + "\n" +
                "  arm " + replay.Record.ArmName + ", seed " + replay.Record.Seed +
                ", dt " + replay.Record.PhysicsDtSeconds + ", config " + replay.Record.ConfigHash + "\n" +
                "  source: " + (replay.Faithful ? "identical to the recording" : replay.SourceDifference) + "\n" +
                "  physics jobs: " + replay.PhysicsJobWorkers +
                (replay.ThreadCaveat != null ? ", " + replay.ThreadCaveat : ", as recorded") + "\n" +
                "  " + replay.Record.Samples.Count + " recorded samples, last at t=" +
                replay.RecordedThroughSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s");
        }

        private static void Approach(TheatreReplay replay, double remaining)
        {
            bool near = remaining <= ApproachSeconds;

            _runner.Rate = near ? 5f : 10000f;
            _runner.FrameBudgetSeconds = near ? 0.02f : 0.25f;

            if (_pastTheRecordSaid) return;
            if (_times[_times.Length - 1] <= replay.RecordedThroughSeconds + 1e-6) return;

            _pastTheRecordSaid = true;
            Debug.LogWarning(
                "[Theatre] the last time asked for is past the record's last sample (t=" +
                replay.RecordedThroughSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                " s). The world runs on deterministically from there, and nothing after that " +
                "instant is checked against anything.");
        }

        /// <summary>Every requested view of the world as it stands, written out.</summary>
        private static void Shoot(TheatreReplay replay, double asked)
        {
            string arm = replay.Record.ArmName ?? "run";

            if (_directory == null)
            {
                _directory = Path.Combine(
                    Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "snaps");

                _directory = Path.Combine(_directory, arm);
            }

            if (_camera == null) _camera = new SnapshotCamera(_width, _height);

            string stamp = asked.ToString("0.###", CultureInfo.InvariantCulture);

            foreach (SnapshotCamera.View view in _views)
            {
                string path = Path.Combine(
                    _directory, arm + "-t" + stamp + "-" + SnapshotCamera.NameOf(view) + ".png");

                int bytes = _camera.Capture(replay, view, path, out string remark);
                _written.Add(path);

                Debug.Log(
                    "[Theatre] wrote " + path + " (" + bytes + " bytes) at t=" +
                    replay.Census.T.ToString("0.##", CultureInfo.InvariantCulture) +
                    " s, asked for " + stamp + " s\n" +
                    "  " + remark + "\n" +
                    "  " + replay.IdentityLine());
            }
        }

        /// <summary>Writes the verdict, stops Play mode, and arranges the exit code.</summary>
        private static void Finish(int code, string verdict)
        {
            EditorApplication.update -= Drive;
            _driving = false;
            SessionState.EraseString(PendingKey);

            if (_camera != null) { _camera.Dispose(); _camera = null; }

            var files = new System.Text.StringBuilder();
            foreach (string path in _written) files.Append("\n  ").Append(path);

            Debug.Log(
                "[Theatre] snapshot: " + verdict + files +
                (_written.Count == 0 ? "\n  nothing was written" : ""));

            if (code != 0) Debug.LogError("[Theatre] snapshot FAILED: " + verdict);

            _runner = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Quit(code);
                return;
            }

            // Leave Play mode first, and carry the code across the reload that leaving causes:
            // the same two ways back to Quit the identity check needs, one for a project that
            // reloads the domain on exiting Play and one for a project that does not.
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

        /// <summary>Ends the batch Editor with the verdict's code, and does nothing in a person's.</summary>
        private static void Quit(int code)
        {
            if (!Application.isBatchMode)
            {
                Debug.Log("[Theatre] snapshot finished with code " + code + ".");
                return;
            }

            EditorApplication.Exit(code);
        }

        private static int IntFrom(string name, int fallback, int least, int most)
        {
            string text = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrEmpty(text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return fallback;
            }

            return Mathf.Clamp(value, least, most);
        }

        // ------------------------------------------------------------------ the owner's menu

        /// <summary>
        /// Photographs the theatre as it stands, for whoever is watching it.
        /// </summary>
        /// <remarks>
        /// The same four views the batch entry takes, of the world already on screen, into the
        /// same directory. Deliberately no seeking and no pausing: what this takes is the frame
        /// the person is looking at, which is the whole point of asking for it by hand.
        /// </remarks>
        [MenuItem("Evosim/Theatre/Snapshot Now")]
        public static void SnapshotNow()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "[Theatre] nothing is playing. Press Play on the theatre scene first; this " +
                    "photographs the world that is on screen.");
                return;
            }

            var runner = UnityEngine.Object.FindFirstObjectByType<TheatreRunner>();

            if (runner == null || runner.Replay == null)
            {
                Debug.LogError(
                    "[Theatre] no replayed world on screen" +
                    (runner != null && !string.IsNullOrEmpty(runner.Error)
                        ? ": " + runner.Error
                        : ". Mode B, with a run directory, is what this photographs."));
                return;
            }

            TheatreReplay replay = runner.Replay;
            string arm = replay.Record.ArmName ?? "run";

            string directory = Path.Combine(
                Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "snaps"), arm);

            SnapshotCamera.View[] views = SnapshotCamera.ParseViews(null, out _);
            string stamp = replay.ElapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture);

            using (var camera = new SnapshotCamera(1600, 900))
            {
                foreach (SnapshotCamera.View view in views)
                {
                    string path = Path.Combine(
                        directory, arm + "-t" + stamp + "-" + SnapshotCamera.NameOf(view) + ".png");

                    int bytes = camera.Capture(replay, view, path, out string remark);
                    Debug.Log("[Theatre] wrote " + path + " (" + bytes + " bytes)\n  " + remark);
                }
            }
        }
    }
}
