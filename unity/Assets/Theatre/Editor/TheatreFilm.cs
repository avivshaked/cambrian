using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// A film of a live world continued from a farm checkpoint: one frame per 1/fps of simulated
    /// time, from each of a few camera plans, written as numbered PNGs an encoder can read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> The owner wants films of the world (logbook/specs/video-tools-notes.md,
    /// 2026-09-15), and the safari's director is not built. What is built is the live mode, which
    /// restores a checkpoint and carries it on in the Editor, and the snapshot's off-screen
    /// render. This entry is those two with a clock: it restores the world, steps it forward by
    /// exactly one frame's worth of simulated time, poses each shot's camera from its plan, renders
    /// and reads back, and repeats. <c>scripts/theatre-film.ps1</c> launches it and turns the
    /// frames into an mp4.
    /// </para>
    /// <para>
    /// <b>Real time is the film's clock, not the Editor's.</b> The pace lock (<c>L</c>) holds a
    /// viewer at or under one simulated second a wall second. A batch Editor rendering two shots
    /// at 1920x1080 with a 2x supersample cannot keep that pace, so the lock is set for the record
    /// and the runner is paused, and this class advances the world itself by the frame interval
    /// through <see cref="TheatreDynamicsReplay.Step()"/>, the call the runner's own loop makes.
    /// Played back at the given fps, the clip runs at one simulated second a second, which is what
    /// the lock is for. The frame's second is the nearest physics step to the asked-for one, never
    /// more than half a step off, and the log prints both at the first and the last frame.
    /// </para>
    /// <para>
    /// <b>The owner's filming rules are the plans' rules</b> (video-tools-notes.md, safari-spec.md
    /// §9): one move per shot, eased at both ends; nothing faster than a body swims (a close shot's
    /// camera moves under 0.3 m/s against its subject); no whip pans; no box, rings or markers; the
    /// interface hidden except the provenance word and the second, burnt into the corner; the
    /// camera never under the bed, never past the glass, and pushed off any body it would stand
    /// inside. Each constraint is checked at every frame, and the log says how often each one
    /// bound and how fast the camera went.
    /// </para>
    /// <para>
    /// <b>It is a cousin, and the frame says so.</b> A restore is a cousin whatever the digests
    /// say (CLAUDE.md, the live-play gotcha); the label reads <c>COUSIN</c> and the second.
    /// </para>
    /// <code>
    /// # NO -quit, and NO -nographics: the entry quits, and rendering needs a graphics device.
    /// $env:EVOSIM_THEATRE_CHECKPOINT = "$PWD/scratch/live-ui/runs/ckUi/.../checkpoints/000000400.ckpt"
    /// $env:EVOSIM_THEATRE_SEEK = '400'
    /// $env:EVOSIM_THEATRE_FILM_SECONDS = '20'; $env:EVOSIM_THEATRE_FILM_FPS = '10'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-job-worker-count', '4',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreFilm.Run',
    ///   '-logFile', "$PWD/scratch/logs/theatre-film.log")
    /// </code>
    /// </remarks>
    public static class TheatreFilm
    {
        private const string PendingKey = "Evosim.Theatre.Film.Pending";
        private const string ExitKey = "Evosim.Theatre.Film.Exit";

        /// <summary>The shots this entry knows, in the order they are named in its errors.</summary>
        public static readonly string[] KnownShots = { "orbit", "close", "drift" };

        /// <summary>The camera's ceiling against its subject in a close shot, m/s (the owner's rule).</summary>
        public const float CloseSpeedCeiling = 0.3f;

        /// <summary>How far above the bed and below the surface the camera keeps, m.</summary>
        public const float Clearance = 1f;

        /// <summary>How far inside the glass the camera keeps, m.</summary>
        public const float GlassClearance = 1f;

        /// <summary>The share of a strong move spent easing in, and again easing out (safari-spec §9).</summary>
        public const float EaseShare = 0.2f;

        private static double _seconds = 60d;
        private static int _fps = 30;
        private static int _width = 1920;
        private static int _height = 1080;
        private static string[] _shotNames = { "orbit", "close" };
        private static string _directory;
        private static float _turns = 0.25f;
        private static double _wallSecondsAllowed = 1800d;

        private static double _deadline;
        private static bool _driving;
        private static TheatreRunner _runner;
        private static bool _setUp;
        private static int _warmFrames;
        private static bool _warmed;
        private static int _dressedLate;

        /// <summary>The fewest throwaway renders before the first captured frame.</summary>
        public const int LeastWarmUpFrames = 3;

        /// <summary>The most, after which the film refuses rather than capture raw bodies.</summary>
        public const int MostWarmUpFrames = 60;
        private static double _t0;
        private static long _steps0;
        private static double _dt;
        private static int _frames;
        private static int _next;
        private static double _firstAt, _lastAt;
        private static float _worstLag;
        private static readonly List<Shot> _shots = new List<Shot>();

        // ------------------------------------------------------------------ the batch entry

        /// <summary>
        /// Batchmode entry point. <b>Launch it without <c>-quit</c> and without
        /// <c>-nographics</c>.</b> It enters Play mode, films, and exits the Editor with 0 or 1.
        /// </summary>
        public static void Run()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT")))
            {
                Fail("EVOSIM_THEATRE_CHECKPOINT is not set: a film is shot from a checkpoint.");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Theatre] already in Play mode: stop it before asking for a film.");
                return;
            }

            string refusal = ReadTheRequest();
            if (refusal != null) { Fail(refusal); return; }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Fail("this Editor has no graphics device, so it cannot render anything. Drop " +
                     "-nographics from the command line; -batchmode on its own is right.");
                return;
            }

            if (!OpenTheTheatreScene()) return;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] film: {0:0.###} s at {1} fps ({2} frames), {3}x{4}, shots {5}, into {6}. " +
                "Entering Play mode.",
                _seconds, _fps, FrameCount(), _width, _height, string.Join(",", _shotNames),
                _directory ?? "scratch/films/<arm>/<checkpoint s>"));

            SessionState.SetString(PendingKey, Pack());
            Arm();
            EditorApplication.EnterPlaymode();
        }

        private static void Fail(string why)
        {
            Debug.LogError("[Theatre] film: " + why);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>Simulated seconds by the physics step: the restore's second plus the steps since.</summary>
        private static double Now(TheatreDynamicsReplay live) => _t0 + (live.Steps - _steps0) * _dt;

        private static int FrameCount() => Math.Max(1, (int)Math.Round(_seconds * _fps));

        /// <summary>Reads the environment into the request, or says what was wrong with it.</summary>
        private static string ReadTheRequest()
        {
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SECONDS");
            _seconds = 60d;
            if (!string.IsNullOrWhiteSpace(text) &&
                (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _seconds) ||
                 !(_seconds > 0d) || _seconds > 3600d))
            {
                return "EVOSIM_THEATRE_FILM_SECONDS: '" + text + "' is not a length between 0 and 3600 s.";
            }

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_FPS");
            _fps = 30;
            if (!string.IsNullOrWhiteSpace(text) &&
                (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _fps) ||
                 _fps < 1 || _fps > 120))
            {
                return "EVOSIM_THEATRE_FILM_FPS: '" + text + "' is not a frame rate from 1 to 120.";
            }

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SIZE");
            _width = 1920;
            _height = 1080;
            if (!string.IsNullOrWhiteSpace(text))
            {
                string[] halves = text.Trim().Split('x', 'X');
                if (halves.Length != 2 ||
                    !int.TryParse(halves[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _width) ||
                    !int.TryParse(halves[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _height) ||
                    _width < 64 || _height < 64 ||
                    _width > SnapshotCamera.MaximumSide || _height > SnapshotCamera.MaximumSide)
                {
                    return "EVOSIM_THEATRE_FILM_SIZE: '" + text + "' is not WxH with sides from 64 to " +
                           SnapshotCamera.MaximumSide + ".";
                }

                // yuv420p wants even sides, and an encoder that pads silently shifts the frame.
                if (_width % 2 != 0 || _height % 2 != 0)
                {
                    return "EVOSIM_THEATRE_FILM_SIZE: both sides must be even for an H.264 yuv420p clip.";
                }
            }

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SHOTS");
            if (string.IsNullOrWhiteSpace(text)) text = "orbit,close";

            var names = new List<string>();
            foreach (string word in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = word.Trim().ToLowerInvariant();
                if (name.Length == 0) continue;
                if (Array.IndexOf(KnownShots, name) < 0)
                {
                    return "EVOSIM_THEATRE_FILM_SHOTS: '" + name + "' is not a shot; the shots are " +
                           string.Join(", ", KnownShots) + ".";
                }

                if (!names.Contains(name)) names.Add(name);
            }

            if (names.Count == 0) return "EVOSIM_THEATRE_FILM_SHOTS: no shot was named.";
            _shotNames = names.ToArray();

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_TURNS");
            _turns = 0.25f;   // a quarter turn over the clip, the safari spec's default; nothing faster than a body swims
            if (!string.IsNullOrWhiteSpace(text) &&
                (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _turns) ||
                 _turns < 0f || _turns > 4f))
            {
                return "EVOSIM_THEATRE_FILM_TURNS: '" + text + "' is not a number of turns from 0 to 4.";
            }

            _directory = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_OUT");
            if (string.IsNullOrWhiteSpace(_directory))
            {
                _directory = null;
            }
            else
            {
                string outside = OutsideTheFilms(_directory);
                if (outside != null) return "EVOSIM_THEATRE_FILM_OUT: " + outside;
            }

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WALL_MINUTES");
            int minutes = 30;
            if (!string.IsNullOrWhiteSpace(text)) int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
            _wallSecondsAllowed = 60d * Mathf.Clamp(minutes, 1, 1440);

            return null;
        }

        /// <summary>Why a directory is not under the repository's scratch/films, or null.</summary>
        /// <remarks>
        /// Narrower than the snapshot's rule on purpose: this entry deletes the frames it finds in
        /// a shot's directory before it writes, and a deletion is only safe where nothing but films
        /// lives.
        /// </remarks>
        private static string OutsideTheFilms(string directory)
        {
            string films, full;
            try
            {
                films = Path.GetFullPath(Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "films"))
                    .TrimEnd(Path.DirectorySeparatorChar);
                full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }

            if (full.StartsWith(films + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;

            return "'" + full + "' is not inside '" + films + "', which is the only place a film is written.";
        }

        private static bool OpenTheTheatreScene()
        {
            if (SceneManager.GetActiveScene().path == TheatreSceneBuilder.ScenePath) return true;

            if (!Application.isBatchMode) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            if (File.Exists(TheatreSceneBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(TheatreSceneBuilder.ScenePath, OpenSceneMode.Single);
                return true;
            }

            if (TheatreSceneBuilder.Build()) return true;

            Fail("no theatre scene, and it could not be built.");
            return false;
        }

        // ------------------------------------------------------------------ across the reload

        private static string Pack() =>
            _seconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
            _fps.ToString(CultureInfo.InvariantCulture) + "|" +
            _width.ToString(CultureInfo.InvariantCulture) + "x" + _height.ToString(CultureInfo.InvariantCulture) + "|" +
            string.Join(",", _shotNames) + "|" +
            (_directory ?? "") + "|" +
            _turns.ToString("R", CultureInfo.InvariantCulture) + "|" +
            _wallSecondsAllowed.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// Picks the request back up on the other side of the domain reload Play mode causes, and
        /// quits with the carried code on the way out, as the snapshot does.
        /// </summary>
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

            string[] f = pending.Split('|');
            if (f.Length < 7) { SessionState.EraseString(PendingKey); return; }

            double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _seconds);
            int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _fps);
            string[] size = f[2].Split('x');
            int.TryParse(size[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _width);
            int.TryParse(size.Length > 1 ? size[1] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out _height);
            _shotNames = f[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _directory = string.IsNullOrEmpty(f[4]) ? null : f[4];
            float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out _turns);
            double.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out _wallSecondsAllowed);

            if (_seconds <= 0d || _fps < 1 || _width < 64 || _height < 64 || _shotNames.Length == 0)
            {
                SessionState.EraseString(PendingKey);
                return;
            }

            Arm();
        }

        /// <summary>Puts the driver on the editor's update loop and starts its wall clock.</summary>
        private static void Arm()
        {
            _deadline = EditorApplication.timeSinceStartup + _wallSecondsAllowed;
            _runner = null;
            _setUp = false;
            _warmed = false;
            _warmFrames = 0;
            _dressedLate = 0;
            _next = 0;

            if (_driving) return;

            _driving = true;
            EditorApplication.update += Drive;
        }

        // ------------------------------------------------------------------ the shoot

        private static void Drive()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Finish(1, "TIMED OUT with " + (_frames - _next) + " of " + _frames + " frame(s) unfilmed");
                    return;
                }

                if (!EditorApplication.isPlaying) return;

                if (_runner == null)
                {
                    _runner = UnityEngine.Object.FindFirstObjectByType<TheatreRunner>();
                    if (_runner == null)
                    {
                        Finish(1, "no TheatreRunner in the scene: nothing is playing the world");
                        return;
                    }
                }

                TheatreDynamicsReplay live = _runner.Live;

                if (live == null)
                {
                    if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                    else if (_runner.Replay != null)
                    {
                        Finish(1, "the theatre opened a PhysX replay rather than a continued world: a " +
                                  "checkpoint belongs to a run the console farm recorded");
                    }

                    return;
                }

                if (!_setUp)
                {
                    SetUp(live);
                    return;
                }

                if (!_warmed)
                {
                    WarmUp(live);
                    return;
                }

                double target = _t0 + (double)_next / _fps;
                float dt = Mathf.Max(1e-4f, live.Sim.PhysicsDt);

                // Stepped here, a frame interval at a time, with a budget so that a heavy world
                // spreads its steps across ticks rather than looking wedged inside one.
                double budgetEnds = EditorApplication.timeSinceStartup + 0.5d;

                // The film's clock is the physics step's, never World.ElapsedSeconds: the world's
                // clock moves in metabolic steps of half a second, and a film read off it put five
                // frames on one instant and then jumped (the first smoke, 2026-09-23).
                while (Now(live) + 0.5d * dt < target)
                {
                    live.Step();
                    if (EditorApplication.timeSinceStartup > budgetEnds) return;
                }

                _runner.LiveView?.Sync();

                // A body born since the last frame is built plain and the palette's rotation reaches
                // it frames later; it is dressed here, before the shutter, instead.
                if (_runner.LiveView != null) _dressedLate += _runner.LiveView.DressUndressed();

                double at = Now(live);
                _worstLag = Mathf.Max(_worstLag, (float)Math.Abs(at - target));
                if (_next == 0) _firstAt = at;
                _lastAt = at;

                float u = _frames > 1 ? (float)_next / (_frames - 1) : 0f;
                string label = string.Format(CultureInfo.InvariantCulture, "COUSIN  t={0:0.0} s", at);
                string frameName = "frame-" + _next.ToString("000000", CultureInfo.InvariantCulture) + ".png";

                foreach (Shot shot in _shots)
                {
                    shot.Pose(live, _runner.LiveView, u, 1f / _fps, out Vector3 eye, out Quaternion rotation, out float focus);

                    shot.Camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, label,
                        Path.Combine(shot.Directory, frameName));

                    if (_next == 0 || _next == _frames / 2 || _next == _frames - 1)
                    {
                        shot.Camera.LastPictureSpread(out float mean, out float spread);
                        shot.Spreads.Add(string.Format(CultureInfo.InvariantCulture,
                            "frame {0}: luminance mean {1:0.0}, sd {2:0.0}{3}", _next, mean, spread,
                            spread < 2f ? " (UNIFORM: the device may have rendered nothing)" : ""));
                    }
                }

                _next++;

                if (_next % Math.Max(1, _fps * 5) == 0)
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] film: {0} of {1} frames, t={2:0.0} s, alive {3}",
                        _next, _frames, at, live.BodyCount));
                }

                if (_next >= _frames)
                {
                    Finish(0, _next + " frame(s) per shot written");
                }
            }
            catch (Exception e)
            {
                Finish(1, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// Renders and discards frames at the first pose, with the world held still, until the skin
        /// has dressed every restored body; only then does the clock start.
        /// </summary>
        /// <remarks>
        /// A restored body is built with the plain material and the raw shapes, and the palette
        /// dresses the crowd on a budget of a hundred-odd bodies a frame, so the first frames of
        /// r46-s1 at 5,000 s (742 bodies) carried white spheres and cubes among skinned bodies
        /// (2026-09-23). Every undressed body is dressed off budget here, the renderers are
        /// checked for the plain material, and the film refuses rather than capture raw bodies.
        /// </remarks>
        private static void WarmUp(TheatreDynamicsReplay live)
        {
            LiveWorldView view = _runner.LiveView;
            if (view == null)
            {
                Finish(1, "no live view in the scene: nothing draws the world");
                return;
            }

            view.Sync();
            view.DressUndressed();

            foreach (Shot shot in _shots)
            {
                shot.Pose(live, view, 0f, 1f / _fps, out Vector3 eye, out Quaternion rotation, out float focus);
                shot.Camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, "", null);
            }

            _warmFrames++;

            int undressed = view.UndressedCount;
            int plain = view.PlainRendererCount();

            if (_warmFrames >= LeastWarmUpFrames && undressed == 0 && plain == 0)
            {
                foreach (Shot shot in _shots) shot.ResetTally();
                _warmed = true;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: warm-up {0} frames, every body dressed ({1} bodies drawn, {2} parts, " +
                    "no renderer on the plain material)", _warmFrames, view.BodyCount, view.PartCount));
                return;
            }

            if (_warmFrames >= MostWarmUpFrames)
            {
                Finish(1, string.Format(CultureInfo.InvariantCulture,
                    "warm-up {0} frames and the skin has not dressed the crowd: {1} bodies undressed, " +
                    "{2} renderers on the plain material", _warmFrames, undressed, plain));
            }
        }

        /// <summary>Stops the runner's own clock, plans the shots, and clears their directories.</summary>
        private static void SetUp(TheatreDynamicsReplay live)
        {
            _setUp = true;

            // The runner steps nothing from here: this class advances the world by the frame
            // interval. The lock and the rate are set so the runner's state says what the film is.
            _runner.Paused = true;
            _runner.PaceLock = true;
            _runner.Rate = 1f;
            _runner.ShowOverlay = false;

            // The viewer's own camera has nothing to show a batch Editor, and would render the whole
            // world every tick for nobody.
            Camera view = _runner.ViewCamera;
            if (view != null) view.enabled = false;

            _runner.LiveView?.Sync();

            _t0 = live.ElapsedSeconds;
            _steps0 = live.Steps;
            _dt = live.Sim.PhysicsDt;
            _frames = FrameCount();
            _next = 0;
            _worstLag = 0f;

            LiveCheckpoint checkpoint = live.ContinuedFrom;
            string arm = live.Record.ArmName ?? "run";
            double ckptSeconds = checkpoint != null ? checkpoint.Seconds : _t0;

            if (_directory == null)
            {
                _directory = Path.Combine(
                    Path.Combine(Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "films"), arm),
                    ckptSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            }

            var world = new WorldBounds(live);
            _shots.Clear();

            foreach (string name in _shotNames)
            {
                Shot shot = Shot.Plan(name, live, _runner.LiveView, world, (float)_seconds, _width / (float)_height, _turns);
                shot.Directory = Path.Combine(_directory, name);

                // A camera of its own per shot, never one camera posed for each in turn: the
                // grade's motion blur reads each camera's previous frame, and a camera that
                // jumped between two shots every frame drew the jump as a smear across the
                // orbit's middle (the first smoke, 2026-09-23).
                shot.Camera = new SnapshotCamera(_width, _height);

                Directory.CreateDirectory(shot.Directory);
                foreach (string stale in Directory.GetFiles(shot.Directory, "frame-*.png")) File.Delete(stale);

                _shots.Add(shot);
            }

            var lines = new System.Text.StringBuilder();
            foreach (Shot shot in _shots) lines.Append("\n  ").Append(shot.Name).Append(": ").Append(shot.Plan_);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] film of {0}: {1}\n  t0={2:0.###} s, dt {3} s, {4} frames at {5} fps, alive {6}, {7}{8}",
                arm, checkpoint != null ? checkpoint.Line() : "no checkpoint: founded at t=0",
                _t0, live.Sim.PhysicsDt, _frames, _fps, live.BodyCount, world.Describe(), lines));
        }

        private static void Finish(int code, string verdict)
        {
            EditorApplication.update -= Drive;
            _driving = false;
            SessionState.EraseString(PendingKey);

            foreach (Shot shot in _shots)
            {
                if (shot.Camera != null) { shot.Camera.Dispose(); shot.Camera = null; }
            }

            var report = new System.Text.StringBuilder();
            report.AppendFormat(CultureInfo.InvariantCulture,
                "\n  first frame at t={0:0.###} s, last at t={1:0.###} s, worst distance from the asked-for second {2:0.####} s; " +
                "warm-up {3} frames; {4} bodies born during the film dressed before their first frame",
                _firstAt, _lastAt, _worstLag, _warmFrames, _dressedLate);

            foreach (Shot shot in _shots)
            {
                report.Append("\n  ").Append(shot.Name).Append(" -> ").Append(shot.Directory);
                report.Append("\n    ").Append(shot.Tally());
                foreach (string s in shot.Spreads) report.Append("\n    ").Append(s);
            }

            Debug.Log("[Theatre] film: " + verdict + report);
            if (code != 0) Debug.LogError("[Theatre] film FAILED: " + verdict);

            _runner = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Quit(code);
                return;
            }

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

        private static void Quit(int code)
        {
            if (!Application.isBatchMode)
            {
                Debug.Log("[Theatre] film finished with code " + code + ".");
                return;
            }

            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ the water's walls

        /// <summary>Where a camera may stand: inside the glass, over the bed, under the surface.</summary>
        private sealed class WorldBounds
        {
            public readonly bool Tank;
            public readonly float Radius;
            public readonly Vector2 Axis;
            public readonly Bounds Box;
            public readonly BedShape Bed;
            public readonly float Depth;

            public WorldBounds(TheatreDynamicsReplay live)
            {
                RunConfig config = live.Record.Config;
                Box = SnapshotCamera.BoxOf(live, out _);
                Bed = live.Bed;
                Depth = config.WorldDepthMetres;
                Tank = config.SharedSpace && config.WorldShape == WorldShape.Tank;
                Radius = Tank ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
                Axis = new Vector2(Radius, Radius);
            }

            /// <summary>The floor's height under a place, m.</summary>
            public float FloorAt(float x, float z) =>
                Bed != null ? (float)Bed.FloorY(x, z) : -Depth;

            /// <summary>How far a camera centred here may stand horizontally, m.</summary>
            public float RoomAround(Vector3 centre)
            {
                if (!Tank)
                {
                    float rx = Mathf.Min(centre.x - Box.min.x, Box.max.x - centre.x);
                    float rz = Mathf.Min(centre.z - Box.min.z, Box.max.z - centre.z);
                    return Mathf.Max(0.5f, Mathf.Min(rx, rz) - GlassClearance);
                }

                float off = new Vector2(centre.x - Axis.x, centre.z - Axis.y).magnitude;
                return Mathf.Max(0.5f, Radius - GlassClearance - off);
            }

            /// <summary>The nearest place a camera may stand, and which rule moved it, if any.</summary>
            public Vector3 Keep(Vector3 eye, ref int glass, ref int bed, ref int surface)
            {
                if (Tank)
                {
                    var h = new Vector2(eye.x - Axis.x, eye.z - Axis.y);
                    float most = Radius - GlassClearance;
                    if (h.magnitude > most)
                    {
                        h = h.normalized * most;
                        eye.x = Axis.x + h.x;
                        eye.z = Axis.y + h.y;
                        glass++;
                    }
                }
                else
                {
                    float x = Mathf.Clamp(eye.x, Box.min.x + GlassClearance, Box.max.x - GlassClearance);
                    float z = Mathf.Clamp(eye.z, Box.min.z + GlassClearance, Box.max.z - GlassClearance);
                    if (x != eye.x || z != eye.z) glass++;
                    eye.x = x;
                    eye.z = z;
                }

                float top = -Clearance;
                if (eye.y > top) { eye.y = top; surface++; }

                float floor = FloorAt(eye.x, eye.z) + Clearance;
                if (eye.y < floor) { eye.y = floor; bed++; }

                return eye;
            }

            public string Describe() =>
                Tank
                    ? string.Format(CultureInfo.InvariantCulture, "tank r={0:0.##} m, depth {1:0.#} m{2}",
                        Radius, Depth, Bed != null && Bed.HasRelief ? ", shaped bed" : ", flat bed")
                    : string.Format(CultureInfo.InvariantCulture, "box {0:0.#} by {1:0.#} by {2:0.#} m",
                        Box.size.x, Box.size.y, Box.size.z);
        }

        // ------------------------------------------------------------------ the plans

        /// <summary>A trapezoid of speed: eased in over the first fifth, level, eased out over the last.</summary>
        /// <remarks>
        /// Position against time for a speed that rises linearly over <see cref="EaseShare"/>, holds,
        /// and falls again; the peak is 1/(1 − share) of the mean, 1.25 at a fifth.
        /// </remarks>
        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            float a = EaseShare;
            float peak = 1f / (1f - a);

            if (u < a) return 0.5f * peak * u * u / a;
            if (u > 1f - a) { float w = 1f - u; return 1f - 0.5f * peak * w * w / a; }

            return 0.5f * peak * a + peak * (u - a);
        }

        /// <summary>One shot: a plan fixed at the first frame, and its camera posed at each.</summary>
        private sealed class Shot
        {
            public string Name;
            public string Directory;
            public string Plan_;
            public float FieldOfView;
            public bool Portrait;
            public SnapshotCamera Camera;
            public readonly List<string> Spreads = new List<string>();

            private WorldBounds _world;
            private float _seconds;

            // orbit and drift
            private Vector3 _centre;
            private float _distance;
            private float _elevation;
            private float _azimuth;
            private float _turns;
            private Vector3 _across;
            private float _pass;

            // close
            private long _subject = -1;
            private Vector3 _offset;
            private Vector3 _forward;
            private float _standoff;
            private float _dolly;
            private Vector3 _followed;
            private bool _hasFollowed;

            // the tally
            private int _frames, _glass, _bed, _surface, _pushed, _inside;
            private float _fastest, _fastestRelative;
            private Vector3 _lastEye, _lastSubject;
            private bool _hasLast;

            public static Shot Plan(
                string name, TheatreDynamicsReplay live, LiveWorldView view, WorldBounds world,
                float seconds, float aspect, float turns)
            {
                var shot = new Shot { Name = name, _world = world, _seconds = seconds };

                var positions = new List<Vector3>();
                var reaches = new List<float>();
                var ids = new List<long>();
                Crowd(live, view, positions, reaches, ids);

                switch (name)
                {
                    case "close": shot.PlanClose(positions, reaches, ids, aspect); break;
                    case "drift": shot.PlanCrowd(positions, reaches, aspect, 0f, true); break;
                    default: shot.PlanCrowd(positions, reaches, aspect, turns, false); break;
                }

                return shot;
            }

            /// <summary>Every living body's root, its reach and its id, from the drawn scene where it can.</summary>
            private static void Crowd(
                TheatreDynamicsReplay live, LiveWorldView view,
                List<Vector3> positions, List<float> reaches, List<long> ids)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;

                for (int i = 0; i < living.Count; i++)
                {
                    Organism o = living[i];
                    Vector3 at = Where(o, view);
                    if (!Finite(at)) continue;

                    positions.Add(at);
                    reaches.Add(SnapshotCamera.ReachOf(o.Phenotype));
                    ids.Add(o.Id);
                }
            }

            /// <summary>
            /// A body's place: its drawn root, which is posed every frame, or the world's centre of
            /// mass, which moves twice a simulated second, when it is not drawn.
            /// </summary>
            private static Vector3 Where(Organism o, LiveWorldView view)
            {
                Transform root = view?.RootOf(o.Id);
                return root != null ? root.position : new Vector3(o.X, o.HeightY, o.Z);
            }

            private static bool Finite(Vector3 v) =>
                !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                  float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

            /// <summary>
            /// The orbit and the drift: the crowd's centroid, and a distance that holds the
            /// crowd's spread in the frame, pulled in where the glass or the surface leave no room.
            /// </summary>
            private void PlanCrowd(List<Vector3> positions, List<float> reaches, float aspect, float turns, bool drift)
            {
                FieldOfView = drift ? 45f : 50f;
                Portrait = false;

                _centre = positions.Count > 0 ? Vector3.zero : _world.Box.center;
                foreach (Vector3 p in positions) _centre += p;
                if (positions.Count > 0) _centre /= positions.Count;

                // The crowd's spread as the 85th percentile of distance from the centroid: the
                // farthest stray would frame the whole tank round one body that wandered.
                var distances = new List<float>(positions.Count);
                float reach = 0.2f;
                for (int i = 0; i < positions.Count; i++)
                {
                    distances.Add((positions[i] - _centre).magnitude);
                    reach = Mathf.Max(reach, reaches[i]);
                }

                distances.Sort();
                float spread = distances.Count > 0 ? distances[Mathf.Min(distances.Count - 1, (int)(0.85f * distances.Count))] : 2f;
                spread = Mathf.Max(1.5f, spread + reach);

                float tanV = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
                float tanH = tanV * aspect;
                float wanted = 1.1f * spread / Mathf.Min(tanV, tanH) + spread;

                _elevation = (drift ? 4f : 12f) * Mathf.Deg2Rad;

                // Room: horizontally to the glass, and upward to the surface for the orbit's lift.
                float room = _world.RoomAround(_centre) / Mathf.Max(0.2f, Mathf.Cos(_elevation));
                float headroom = (-Clearance - _centre.y) / Mathf.Max(0.02f, Mathf.Sin(_elevation));
                float most = Mathf.Min(room, headroom);
                string tilt = drift ? "drift at 4 deg down" : "12 deg down as planned";

                // A crowd near the surface leaves a tilted-down orbit no room: r46-s1 at 5,000 s,
                // centroid at -3.4 m, got 11 m against 166 wanted, and the camera orbited inside the
                // crowd, pushed off a body 98 times. So when the surface is the cap and it caps
                // below half of what is wanted, the orbit goes level at the crowd's depth, capped
                // by the glass alone; and if the glass caps that below half too, it looks slightly
                // up at the crowd from under it, where a 45 m tank has water to spare.
                if (!drift && headroom < room && most < 0.5f * wanted)
                {
                    float surfaceCap = most;
                    _elevation = 0f;
                    float level = _world.RoomAround(_centre);
                    most = level;
                    tilt = string.Format(CultureInfo.InvariantCulture,
                        "level at the crowd's depth: the surface capped 12 deg down at {0:0.##} m", surfaceCap);

                    if (level < 0.5f * wanted)
                    {
                        _elevation = -15f * Mathf.Deg2Rad;
                        float glass = _world.RoomAround(_centre) / Mathf.Cos(_elevation);
                        float floor = _world.FloorAt(_centre.x, _centre.z) + Clearance;
                        float footroom = (_centre.y - floor) / Mathf.Sin(-_elevation);
                        most = Mathf.Min(glass, footroom);
                        tilt = string.Format(CultureInfo.InvariantCulture,
                            "15 deg up from under the crowd: the surface capped 12 deg down at {0:0.##} m and " +
                            "the glass capped level at {1:0.##} m (room below {2:0.##} m)",
                            surfaceCap, level, footroom);
                    }
                }

                _distance = Mathf.Max(2f, Mathf.Min(wanted, most));

                _azimuth = -0.5f * Mathf.PI;
                _turns = turns;

                if (drift)
                {
                    // A truck, not a pan: the camera keeps its bearing and slides across it at a
                    // swimmer's pace, a quarter of a metre a second at the most.
                    Vector3 look = new Vector3(-Mathf.Cos(_azimuth), 0f, -Mathf.Sin(_azimuth));
                    _across = Vector3.Cross(Vector3.up, look).normalized;
                    _pass = Mathf.Min(0.25f * _seconds, Mathf.Max(2f, spread));
                }

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "{0} of {1} bodies about ({2:0.##}, {3:0.##}, {4:0.##}), spread {5:0.##} m, distance {6:0.##} m " +
                    "(wanted {7:0.##}, room {8:0.##}), lens {9:0} deg, {10}",
                    drift ? "drift" : "orbit", positions.Count, _centre.x, _centre.y, _centre.z, spread,
                    _distance, wanted, most, FieldOfView,
                    drift
                        ? string.Format(CultureInfo.InvariantCulture, "a {0:0.##} m pass, {1:0.###} m/s at its peak",
                            _pass, _pass / _seconds / (1f - EaseShare))
                        : string.Format(CultureInfo.InvariantCulture,
                            "{0:0.##} turn(s), {3}, {2:0.###} m/s along the arc at its peak",
                            _turns, _elevation * Mathf.Rad2Deg,
                            _turns * 2f * Mathf.PI * _distance * Mathf.Cos(_elevation) / _seconds / (1f - EaseShare),
                            tilt));
            }

            /// <summary>
            /// The close view's framing: the largest body and the largest of its neighbours, from
            /// a three-quarter angle, the frame fitted to the pair and a slow dolly in.
            /// </summary>
            private void PlanClose(List<Vector3> positions, List<float> reaches, List<long> ids, float aspect)
            {
                FieldOfView = 28f;
                Portrait = true;
                _forward = new Vector3(-0.78f, -0.34f, 1f).normalized;

                int anchor = -1;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (anchor < 0 || reaches[i] > reaches[anchor]) anchor = i;
                }

                if (anchor < 0)
                {
                    _subject = -1;
                    _followed = _world.Box.center;
                    _hasFollowed = true;
                    _offset = Vector3.zero;
                    _standoff = 5f;
                    _dolly = 0f;
                    Plan_ = "nothing alive to frame: a still of the box's centre";
                    return;
                }

                float around = Mathf.Max(1.2f, 6f * reaches[anchor]);
                int neighbour = -1;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (i == anchor) continue;
                    if ((positions[i] - positions[anchor]).sqrMagnitude > around * around) continue;
                    if (neighbour < 0 || reaches[i] > reaches[neighbour]) neighbour = i;
                }

                var framed = new Bounds(positions[anchor], 2.4f * reaches[anchor] * Vector3.one);
                if (neighbour >= 0) framed.Encapsulate(new Bounds(positions[neighbour], 2.4f * reaches[neighbour] * Vector3.one));

                // The snapshot's perspective fit (SnapshotCamera.Frame), with its 6% margin.
                Quaternion rotation = Quaternion.LookRotation(_forward, Vector3.up);
                Quaternion inverse = Quaternion.Inverse(rotation);
                float tanV = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
                float tanH = tanV * aspect;
                Vector3 half = 0.5f * framed.size;
                float need = 0f, reachZ = 0f;

                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 c = inverse * new Vector3(sx * half.x, sy * half.y, sz * half.z);
                    need = Mathf.Max(need, Mathf.Abs(c.y) / tanV - c.z);
                    need = Mathf.Max(need, Mathf.Abs(c.x) / tanH - c.z);
                    reachZ = Mathf.Max(reachZ, Mathf.Abs(c.z));
                }

                _standoff = 1.06f * Mathf.Max(need, reachZ + 0.5f);

                // A few metres in at most, never past two fifths of the standoff (the subject is
                // there), and never faster on average than a quarter of the ceiling.
                _dolly = Mathf.Min(3f, Mathf.Min(0.4f * _standoff, 0.25f * CloseSpeedCeiling * _seconds));

                _subject = ids[anchor];
                _offset = framed.center - positions[anchor];
                _followed = positions[anchor];
                _hasFollowed = true;

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "body {0} (reach {1:0.###} m){2}, framed over {3:0.##} m, standoff {4:0.##} m, dolly in {5:0.##} m " +
                    "({6:0.###} m/s at its peak), following the subject",
                    _subject, reaches[anchor],
                    neighbour >= 0
                        ? string.Format(CultureInfo.InvariantCulture, " and body {0} (reach {1:0.###} m)", ids[neighbour], reaches[neighbour])
                        : ", no neighbour within reach",
                    framed.size.magnitude, _standoff, _dolly, _dolly / _seconds / (1f - EaseShare));
            }

            /// <summary>The camera at a point of the clip, kept inside the water and off every body.</summary>
            public void Pose(
                TheatreDynamicsReplay live, LiveWorldView view, float u, float frameSeconds,
                out Vector3 eye, out Quaternion rotation, out float focus)
            {
                Vector3 subject;

                if (Name == "close")
                {
                    // The subject's root, smoothed over a second and a half so the frame follows a
                    // swimmer without copying every stroke.
                    Vector3 now = _followed;
                    if (_subject >= 0)
                    {
                        Transform root = view?.RootOf(_subject);
                        if (root != null && Finite(root.position)) now = root.position;
                    }

                    float k = 1f - Mathf.Exp(-frameSeconds / 1.5f);
                    _followed = _hasFollowed ? Vector3.Lerp(_followed, now, k) : now;
                    _hasFollowed = true;

                    subject = _followed + _offset;
                    float distance = _standoff - _dolly * Ease(u);
                    eye = subject - _forward * distance;
                    rotation = Quaternion.LookRotation(_forward, Vector3.up);
                    focus = distance;
                }
                else if (Name == "drift")
                {
                    Vector3 look = new Vector3(-Mathf.Cos(_azimuth), 0f, -Mathf.Sin(_azimuth));
                    Vector3 back = -look * Mathf.Cos(_elevation) + Vector3.up * Mathf.Sin(_elevation);
                    Vector3 slide = _across * (_pass * (Ease(u) - 0.5f));

                    // The look slides with the camera, so the bearing holds and nothing pans.
                    subject = _centre + slide;
                    eye = _centre + back * _distance + slide;
                    rotation = Quaternion.LookRotation(subject - eye, Vector3.up);
                    focus = 0f;
                }
                else
                {
                    subject = _centre;
                    float theta = _azimuth + _turns * 2f * Mathf.PI * Ease(u);
                    eye = _centre + _distance * new Vector3(
                        Mathf.Cos(_elevation) * Mathf.Cos(theta), Mathf.Sin(_elevation),
                        Mathf.Cos(_elevation) * Mathf.Sin(theta));
                    rotation = Quaternion.LookRotation(_centre - eye, Vector3.up);
                    focus = 0f;
                }

                eye = _world.Keep(eye, ref _glass, ref _bed, ref _surface);
                eye = OffTheBodies(live, view, eye);

                // A push can cross the glass or the bed again; the walls win, and the tally says so.
                eye = _world.Keep(eye, ref _glass, ref _bed, ref _surface);
                if (InsideABody(live, view, eye)) _inside++;

                if (Name != "close") rotation = Quaternion.LookRotation(subject - eye, Vector3.up);

                if (_hasLast)
                {
                    _fastest = Mathf.Max(_fastest, (eye - _lastEye).magnitude / frameSeconds);
                    _fastestRelative = Mathf.Max(_fastestRelative,
                        ((eye - subject) - (_lastEye - _lastSubject)).magnitude / frameSeconds);
                }

                _lastEye = eye;
                _lastSubject = subject;
                _hasLast = true;
                _frames++;
            }

            /// <summary>Moves the eye out of any body's reach, plus a quarter metre.</summary>
            private Vector3 OffTheBodies(TheatreDynamicsReplay live, LiveWorldView view, Vector3 eye)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;
                bool moved = false;

                for (int pass = 0; pass < 3; pass++)
                {
                    bool again = false;

                    for (int i = 0; i < living.Count; i++)
                    {
                        Vector3 at = Where(living[i], view);
                        if (!Finite(at)) continue;

                        float keep = SnapshotCamera.ReachOf(living[i].Phenotype) + 0.25f;
                        Vector3 away = eye - at;
                        float d = away.magnitude;
                        if (d >= keep) continue;

                        eye = at + (d > 1e-4f ? away / d : Vector3.up) * keep;
                        again = true;
                        moved = true;
                    }

                    if (!again) break;
                }

                if (moved) _pushed++;
                return eye;
            }

            private static bool InsideABody(TheatreDynamicsReplay live, LiveWorldView view, Vector3 eye)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;
                for (int i = 0; i < living.Count; i++)
                {
                    Vector3 at = Where(living[i], view);
                    if (!Finite(at)) continue;
                    if ((eye - at).magnitude < SnapshotCamera.ReachOf(living[i].Phenotype)) return true;
                }

                return false;
            }

            /// <summary>Forgets the warm-up's poses, so the tally counts the film's frames alone.</summary>
            public void ResetTally()
            {
                _frames = _glass = _bed = _surface = _pushed = _inside = 0;
                _fastest = _fastestRelative = 0f;
                _hasLast = false;
            }

            public string Tally() => string.Format(CultureInfo.InvariantCulture,
                "{0} frames; the glass bound {1}, the bed {2}, the surface {3}; pushed off a body {4}, " +
                "still inside one {5}; fastest camera {6:0.###} m/s, fastest against the subject {7:0.###} m/s{8}",
                _frames, _glass, _bed, _surface, _pushed, _inside, _fastest, _fastestRelative,
                Name == "close" && _fastestRelative > CloseSpeedCeiling ? " (OVER the 0.3 m/s ceiling)" : "");
        }
    }
}
