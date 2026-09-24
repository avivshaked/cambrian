using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The safari's headless record (safari-spec.md item 12, route a): plays a trip over a
    /// recorded run in a batch Editor and writes each take's frames through the film's
    /// render-texture read-back, with the caption log and the scene list beside them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The film's shape, with the director in place of the film's two shots.</b> The entry
    /// enters Play mode, lets the runner open the run's live world, pauses it, and hands the world
    /// to <see cref="SafariDirector"/>, which seeks each scene from the checkpoints and plans its
    /// takes; this class then steps each take one frame interval at a time and renders it off
    /// screen with a <see cref="SnapshotCamera"/> of its own (a new one per take, so the grade's
    /// motion blur never draws a cut as a smear). <c>Time.captureDeltaTime</c> holds the shaders'
    /// clock to the frame interval, as the film does. Each take is warmed up until the skin has
    /// dressed every body before its first frame is kept.
    /// </para>
    /// <para>
    /// <b>What it writes</b>, under <c>scratch/safari/&lt;arm&gt;/&lt;date&gt;/</c> (or
    /// <c>EVOSIM_THEATRE_SAFARI_OUT</c>, which must lie inside <c>scratch/safari</c>):
    /// <c>&lt;scene&gt;/take-N/frame-NNNNNN.png</c> per take; <c>scenes.tsv</c> (index, slug,
    /// station, subject, the second asked and the second played, the outcome, the takes, the
    /// plan); and <c>captions.tsv</c> (scene, second, text, and the offset into the take's clip).
    /// <c>scripts/theatre-safari.ps1</c> launches it and encodes the takes.
    /// </para>
    /// <code>
    /// # NO -quit and NO -nographics, as the film.
    /// $env:EVOSIM_THEATRE_RUN = "$PWD/runs/r46-s1"
    /// $env:EVOSIM_THEATRE_SAFARI_SCENES = '1,3'
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-job-worker-count', '4',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreSafari.Run',
    ///   '-logFile', "$PWD/scratch/logs/theatre-safari.log")
    /// </code>
    /// </remarks>
    public static class TheatreSafari
    {
        /// <summary>Batchmode entry: plays the trip and writes its frames.</summary>
        public static void Run() => SafariHeadless.Start(check: false);
    }

    /// <summary>
    /// The safari's headless check (the spec's validation): plays a trip on a recorded run,
    /// writes a frame every two seconds into <c>scratch/snaps/safari/&lt;arm&gt;/</c>, and asserts
    /// over every frame that the camera is above the bed, outside every body's radius, and that no
    /// move between two frames of a take exceeds the ceiling. Prints one verdict line.
    /// </summary>
    public static class TheatreSafariCheck
    {
        /// <summary>Batchmode entry, same launch as <see cref="TheatreSafari.Run"/>.</summary>
        public static void Run() => SafariHeadless.Start(check: true);
    }

    /// <summary>The driver both entries share.</summary>
    internal static class SafariHeadless
    {
        private const string PendingKey = "Evosim.Theatre.Safari.Pending";
        private const string ExitKey = "Evosim.Theatre.Safari.Exit";

        /// <summary>A little over the film's 0.5 m/s: the ease's rounding and the subject's smoothed follow.</summary>
        public const float CeilingTolerance = 1.05f;

        // the request, packed across the domain reload
        private static bool _check;
        private static string _run;
        private static string _heuristic = "top10";
        private static string _scenesWanted = "";
        private static string _clade = "";
        private static int _fps = 30;
        private static float _checkEvery = 2f;
        private static int _width = 1920, _height = 1080;
        private static double _wallSeconds = 3600d;
        private static string _out;
        private static bool _burn = true;
        private static double _seekMax = 300d;
        private static double _snapAhead = 600d;

        // every frame of the trip, stage by stage, folded in at each take's end; the camera last
        // folded in, so a take is never counted twice
        private static SnapshotCamera.StageTimes _tripTimes = new SnapshotCamera.StageTimes();
        private static SnapshotCamera _timedCamera;

        // the drive
        private static bool _driving;
        private static double _deadline;
        private static double _lastSaid;
        private static TheatreRunner _runner;
        private static SafariDirector _director;
        private static SafariGuide _guide;
        private static List<int> _playList;
        private static int _playAt;
        private static SnapshotCamera _camera;
        private static string _takeDirectory;
        private static int _takeNumber = -1;
        private static bool _warm;
        private static int _warmFrames;
        private static StreamWriter _captions, _checkLog;

        // the call-outs (EVOSIM_THEATRE_SAFARI_CALLOUTS=1): the sparkline on the interface's
        // document, composited over each written frame through TheatreUiCapture.ArmOver, the
        // -Chrome route, and landed on the next tick
        private static SafariSparkline _sparkline;
        private static string _calloutPath;
        private static int _calloutsLanded, _calloutsFailed;
        private static readonly List<string> _outcomes = new List<string>();
        private static readonly Dictionary<int, int> _takesByScene = new Dictionary<int, int>();

        // the check's tally
        private static int _frames, _underBed, _inBody, _overCeiling, _outsideGlassFrames;
        private static float _fastest;
        private static Vector3 _lastEye;
        private static bool _hasLastEye;
        private static readonly List<string> _firstFaults = new List<string>();

        private static float Interval => _check ? _checkEvery : 1f / _fps;

        // ---------------------------------------------------------------- start

        public static void Start(bool check)
        {
            _check = check;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Theatre] safari: already in Play mode: stop it first.");
                return;
            }

            string refusal = ReadTheRequest();
            if (refusal != null) { Fail(refusal); return; }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Fail("this Editor has no graphics device. Drop -nographics; -batchmode alone is right.");
                return;
            }

            // The trip, built once here so a bad guide or a bad scene list fails in seconds
            // rather than after the Play-mode reload; it is built again on the other side.
            List<SafariScene> scenes = BuildTrip(out string why);
            if (scenes == null) { Fail(why); return; }

            if (!OpenTheTheatreScene()) return;

            // The live world opens from the run's founding and the director restores from there.
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_RUN", _run);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT", null);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_SEEK", null);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] safari{0}: {1} of {2} scenes of the {3} trip, {4}, into {5}. Entering Play mode.\n  {6}",
                _check ? " check" : "", _playList.Count, scenes.Count, string.IsNullOrEmpty(_clade) ? _heuristic : "one-clade (" + _clade + ")",
                _check ? "a frame every " + _checkEvery.ToString("0.#", CultureInfo.InvariantCulture) + " s" : _fps + " fps at " + _width + "x" + _height,
                _out, string.Join("\n  ", _playList.Select(i => scenes[i].Line()))));

            SessionState.SetString(PendingKey, Pack());
            Arm();
            EditorApplication.EnterPlaymode();
        }

        private static string ReadTheRequest()
        {
            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
            if (string.IsNullOrWhiteSpace(run)) return "EVOSIM_THEATRE_RUN is not set: a safari is a trip through one recorded run.";
            _run = RunRecord.ResolveRunDirectory(run.Trim().Trim('"')) ?? run.Trim();
            if (!Directory.Exists(_run)) return "EVOSIM_THEATRE_RUN: no run directory at '" + _run + "'.";

            _heuristic = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_HEURISTIC") ?? "top10").Trim();
            if (!SafariTripBuilder.TryParse(_heuristic, out _)) return "EVOSIM_THEATRE_SAFARI_HEURISTIC: '" + _heuristic + "' is not one of top10, guild, depth, age, firsts.";
            _scenesWanted = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_SCENES") ?? "").Trim();
            _clade = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CLADE") ?? "").Trim();

            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_FPS");
            _fps = 30;
            if (!string.IsNullOrWhiteSpace(text) && (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _fps) || _fps < 1 || _fps > 120))
                return "EVOSIM_THEATRE_SAFARI_FPS: '" + text + "' is not a frame rate from 1 to 120.";

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_EVERY");
            _checkEvery = 2f;
            if (!string.IsNullOrWhiteSpace(text) && (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _checkEvery) || _checkEvery < 0.05f || _checkEvery > 30f))
                return "EVOSIM_THEATRE_SAFARI_EVERY: '" + text + "' is not an interval from 0.05 to 30 s.";

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_SIZE");
            _width = _check ? 960 : 1920;
            _height = _check ? 540 : 1080;
            if (!string.IsNullOrWhiteSpace(text))
            {
                string[] halves = text.Trim().Split('x', 'X');
                if (halves.Length != 2 || !int.TryParse(halves[0], out _width) || !int.TryParse(halves[1], out _height) ||
                    _width < 64 || _height < 64 || _width > SnapshotCamera.MaximumSide || _height > SnapshotCamera.MaximumSide ||
                    _width % 2 != 0 || _height % 2 != 0)
                    return "EVOSIM_THEATRE_SAFARI_SIZE: '" + text + "' is not WxH with even sides from 64 to " + SnapshotCamera.MaximumSide + ".";
            }

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_WALL_MINUTES") ?? Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WALL_MINUTES");
            int minutes = 60;
            if (!string.IsNullOrWhiteSpace(text)) int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
            _wallSeconds = 60d * Mathf.Clamp(minutes, 1, 2880);

            _burn = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CAPTIONS") != "off";

            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_SEEK_MAX");
            _seekMax = 300d;
            if (!string.IsNullOrWhiteSpace(text)) double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _seekMax);

            // How far ahead a flexible scene may move to the next checkpoint rather than be
            // stepped to (SafariOptions.MostSnapAheadSeconds); negative never moves one forward.
            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_SNAP_AHEAD");
            _snapAhead = 600d;
            if (!string.IsNullOrWhiteSpace(text) &&
                !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _snapAhead))
                return "EVOSIM_THEATRE_SAFARI_SNAP_AHEAD: '" + text + "' is not a number of seconds.";

            string arm = new DirectoryInfo(_run).Parent?.Name ?? "run";
            string root = Path.Combine(BuildIdentity.RepositoryRoot(), "scratch");
            string allowed = _check ? Path.Combine(Path.Combine(root, "snaps"), "safari") : Path.Combine(root, "safari");

            _out = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_OUT");
            if (string.IsNullOrWhiteSpace(_out))
            {
                _out = _check ? Path.Combine(allowed, arm) : Path.Combine(Path.Combine(allowed, arm), DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            string full = Path.GetFullPath(_out).TrimEnd(Path.DirectorySeparatorChar);
            string inside = Path.GetFullPath(allowed).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                return "EVOSIM_THEATRE_SAFARI_OUT: '" + full + "' is not inside '" + inside + "', the only place this entry writes and deletes frames.";
            _out = full;
            return null;
        }

        /// <summary>The guide, the clades and the trip; the scene list filtered by the request.</summary>
        private static List<SafariScene> BuildTrip(out string why)
        {
            why = null;
            string guidePath = SafariGuide.Locate(_run, out string where);
            if (guidePath == null) { why = "no guide: " + where; return null; }

            _guide = SafariGuide.Read(guidePath, out string refusal);
            if (_guide == null) { why = "the guide was refused: " + refusal; return null; }

            var checkpoints = SafariDirector.ReadCheckpoints(_run).Select(c => c.seconds).ToList();
            RunRecord record = RunRecord.Load(_run);
            double runSeconds = record?.RequestedSeconds ?? (checkpoints.Count > 0 ? checkpoints[checkpoints.Count - 1] : 0d);

            List<SafariScene> scenes;
            if (!string.IsNullOrEmpty(_clade))
            {
                SafariClade one = _guide.FindByName(_clade);
                if (one == null) { why = "EVOSIM_THEATRE_SAFARI_CLADE: no clade named '" + _clade + "' in the guide."; return null; }
                scenes = SafariTripBuilder.One(one, checkpoints, _guide);
            }
            else
            {
                SafariTripBuilder.TryParse(_heuristic, out SafariHeuristic h);
                scenes = SafariTripBuilder.Build(_guide, SafariTripBuilder.Choose(_guide, h), checkpoints, runSeconds,
                    SafariTripBuilder.TimeOrder);
            }

            RunConfig config = record?.Config;
            float depth = config?.WorldDepthMetres ?? 0f;
            float radius = config != null && config.SharedSpace && config.WorldShape == WorldShape.Tank ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
            string arm = record?.ArmName ?? new DirectoryInfo(_run).Parent?.Name;
            foreach (SafariScene s in scenes) SafariCaptions.Fill(s, _guide, arm, t => SafariDirector.RecordedAlive(record, t), depth, radius);

            _playList = new List<int>();
            if (string.IsNullOrEmpty(_scenesWanted))
            {
                for (int i = 0; i < scenes.Count; i++) _playList.Add(i);
            }
            else
            {
                foreach (string word in _scenesWanted.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1 || n > scenes.Count)
                    {
                        why = "EVOSIM_THEATRE_SAFARI_SCENES: '" + word + "' is not a scene from 1 to " + scenes.Count + ". The trip:\n  " +
                              string.Join("\n  ", scenes.Select(s => s.Line()));
                        return null;
                    }
                    if (!_playList.Contains(n - 1)) _playList.Add(n - 1);
                }
            }

            return scenes;
        }

        private static bool OpenTheTheatreScene()
        {
            if (SceneManager.GetActiveScene().path == TheatreSceneBuilder.ScenePath) return true;
            if (File.Exists(TheatreSceneBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(TheatreSceneBuilder.ScenePath, OpenSceneMode.Single);
                return true;
            }
            if (TheatreSceneBuilder.Build()) return true;
            Fail("no theatre scene, and it could not be built.");
            return false;
        }

        // ---------------------------------------------------------------- across the reload

        private static string Pack() => string.Join("|",
            _check ? "1" : "0", _run, _heuristic, _scenesWanted, _clade,
            _fps.ToString(CultureInfo.InvariantCulture), _checkEvery.ToString("R", CultureInfo.InvariantCulture),
            _width.ToString(CultureInfo.InvariantCulture), _height.ToString(CultureInfo.InvariantCulture),
            _wallSeconds.ToString("R", CultureInfo.InvariantCulture), _out, _burn ? "1" : "0",
            _seekMax.ToString("R", CultureInfo.InvariantCulture),
            _snapAhead.ToString("R", CultureInfo.InvariantCulture));

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
            if (f.Length < 13) { SessionState.EraseString(PendingKey); return; }

            _check = f[0] == "1";
            _run = f[1];
            _heuristic = f[2];
            _scenesWanted = f[3];
            _clade = f[4];
            int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out _fps);
            float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out _checkEvery);
            int.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out _width);
            int.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out _height);
            double.TryParse(f[9], NumberStyles.Float, CultureInfo.InvariantCulture, out _wallSeconds);
            _out = f[10];
            _burn = f[11] == "1";
            double.TryParse(f[12], NumberStyles.Float, CultureInfo.InvariantCulture, out _seekMax);
            _snapAhead = 600d;
            if (f.Length > 13) double.TryParse(f[13], NumberStyles.Float, CultureInfo.InvariantCulture, out _snapAhead);

            Arm();
        }

        private static void Arm()
        {
            _deadline = EditorApplication.timeSinceStartup + _wallSeconds;
            _lastSaid = EditorApplication.timeSinceStartup;
            _runner = null;
            _director = null;
            _playAt = 0;
            _takeNumber = -1;
            _frames = _underBed = _inBody = _overCeiling = _outsideGlassFrames = 0;
            _fastest = 0f;
            _hasLastEye = false;
            _firstFaults.Clear();
            _outcomes.Clear();
            _takesByScene.Clear();
            _tripTimes = new SnapshotCamera.StageTimes();
            _timedCamera = null;

            if (_driving) return;
            _driving = true;
            EditorApplication.update += Drive;
        }

        // ---------------------------------------------------------------- the drive

        private static void Drive()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Finish(1, "TIMED OUT at scene " + (_director?.Current?.Slug ?? "none") + " (" + (_director?.Status ?? "") + ")");
                    return;
                }

                if (!EditorApplication.isPlaying) return;

                if (_runner == null)
                {
                    _runner = UnityEngine.Object.FindAnyObjectByType<TheatreRunner>();
                    if (_runner == null) { Finish(1, "no TheatreRunner in the scene"); return; }
                }

                if (_director == null)
                {
                    if (_runner.Live == null)
                    {
                        if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                        else if (_runner.Replay != null) Finish(1, "the theatre opened a PhysX replay: a safari needs a run the console farm recorded");
                        return;
                    }

                    SetUp();
                    return;
                }

                // Nothing is drawn while the world is carried to a scene's second, so the runner's
                // once-a-tick posing, building, painting and panel are held until the director has
                // planned the take, which brings the view up to date itself (2026-09-24).
                bool seeking = _director.Phase == SafariPhase.Seeking || _director.Phase == SafariPhase.Rehearsing;
                _runner.HoldView = seeking;

                switch (_director.Phase)
                {
                    case SafariPhase.Seeking:
                    case SafariPhase.Rehearsing:
                        _director.Advance(1.5d);
                        if (EditorApplication.timeSinceStartup - _lastSaid > 60d)
                        {
                            _lastSaid = EditorApplication.timeSinceStartup;
                            Debug.Log("[Theatre] safari: " + _director.Current?.Slug + ": " + _director.Status);
                        }
                        return;

                    case SafariPhase.Playing:
                        if (_calloutPath != null) { LandTheCallout(); return; }
                        if (!_warm) { WarmUp(); return; }
                        Shoot();
                        return;

                    case SafariPhase.Parked:
                    case SafariPhase.Missed:
                    case SafariPhase.Failed:
                        SceneOver();
                        return;

                    default:
                        NextScene();
                        return;
                }
            }
            catch (Exception e)
            {
                Finish(1, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        private static void SetUp()
        {
            _runner.Paused = true;
            _runner.PaceLock = true;
            _runner.Rate = 1f;
            _runner.ShowOverlay = false;

            Time.captureDeltaTime = Interval;

            Camera view = _runner.ViewCamera;
            if (view != null) view.enabled = false;

            List<SafariScene> scenes = BuildTrip(out string why);
            if (scenes == null) { Finish(1, why); return; }

            SafariClades clades = SafariClades.Read(_run);
            Debug.Log("[Theatre] safari: lineage " + clades.Note + "; guide " + _guide.Path + ", " + _guide.Clades.Count + " clades" +
                      (_guide.Ignored.Count > 0 ? "; keys not read: " + string.Join(", ", _guide.Ignored) : ""));

            _director = new SafariDirector(_runner, _run, _guide, clades, scenes, new SafariOptions
            {
                Aspect = _width / (float)_height,
                Interactive = false,
                MostSeekSeconds = _seekMax,
                MostSnapAheadSeconds = _snapAhead,
            });
            _director.TakeStarted += OnTakeStarted;
            _director.TakeEnded += OnTakeEnded;
            _director.CaptionShown += OnCaption;

            Directory.CreateDirectory(_out);
            _captions = new StreamWriter(Path.Combine(_out, "captions.tsv"), false);
            _captions.WriteLine("scene\tsecond\ttext\tclip_offset_s");
            if (_check)
            {
                _checkLog = new StreamWriter(Path.Combine(_out, "check.tsv"), false);
                _checkLog.WriteLine("scene\ttake\tframe\tt\tx\ty\tz\tfloor\tabove_bed\tnearest_gap\tspeed\toutside_glass");
            }

            _sparkline = null;
            _calloutPath = null;
            _calloutsLanded = _calloutsFailed = 0;
            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CALLOUTS") == "1")
            {
                if (_runner.Ui?.Document != null && _runner.Ui.Panel != null)
                {
                    _sparkline = new SafariSparkline();
                    _runner.Ui.Document.rootVisualElement.Add(_sparkline);
                    Debug.Log("[Theatre] safari: call-outs on: the subject clade in colour, the rest grey, and its sparkline composited over each frame");
                }
                else
                {
                    Debug.LogWarning("[Theatre] safari: call-outs asked for, but the interface did not load: the tint only, no sparkline");
                }
            }

            _playAt = 0;
            _director.Begin(_playList[0]);
        }

        private static double _takeStartSecond;

        private static void OnTakeStarted(SafariScene scene, int take, string plan)
        {
            _warm = false;
            _warmFrames = 0;
            _hasLastEye = false;
            _takeNumber = take;
            _takeStartSecond = _runner.Live.ElapsedSeconds;
            _takesByScene[scene.Index] = take + 1;

            _camera?.Dispose();
            _camera = new SnapshotCamera(_width, _height) { FillIntensity = 0.6f };

            _takeDirectory = Path.Combine(Path.Combine(_out, scene.Slug), "take-" + (take + 1));
            Directory.CreateDirectory(_takeDirectory);
            foreach (string stale in Directory.GetFiles(_takeDirectory, "frame-*.png")) File.Delete(stale);
        }

        /// <summary>
        /// The take's frames all on disk before anything else happens, and what they cost said:
        /// a failed write fails the safari here, at the take it belongs to.
        /// </summary>
        private static void OnTakeEnded(SafariScene scene, int take, string tally)
        {
            SnapshotCamera.FlushWrites();

            if (_camera == null || _camera == _timedCamera) return;
            _tripTimes.Add(_camera.Times);
            _timedCamera = _camera;
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Theatre] safari: take {0} of {1}, frames: {2}; {3}",
                take + 1, scene.Slug, _camera.Route, _camera.Times.Line()));
        }

        private static void OnCaption(SafariScene scene, double second, string text, double offset)
        {
            // Every frame handed to the writer is on disk before a row that points into the
            // take's clip is written.
            if (_captions != null) SnapshotCamera.FlushWrites();

            _captions?.WriteLine(string.Join("\t", scene.Slug, second.ToString("0.###", CultureInfo.InvariantCulture), text,
                (second - _takeStartSecond).ToString("0.###", CultureInfo.InvariantCulture)));
            _captions?.Flush();
        }

        /// <summary>Renders the take's first pose until every body is dressed, as the film's warm-up does.</summary>
        private static void WarmUp()
        {
            LiveWorldView view = _runner.LiveView;
            TheatreDynamicsReplay live = _runner.Live;
            FilmPlans.Shot shot = _director.CurrentShot;
            if (view == null || shot == null) { _warm = true; return; }

            view.Sync();
            view.DressUndressed();
            shot.Pose(live, view, 0f, Interval, out Vector3 eye, out Quaternion rotation, out float focus);
            _camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, "", null);
            _warmFrames++;

            if (_warmFrames >= 3 && view.UndressedCount == 0 && view.PlainRendererCount() == 0)
            {
                shot.ResetTally();
                _warm = true;
                return;
            }

            if (_warmFrames >= 60) Finish(1, "warm-up 60 frames and the skin has not dressed the crowd: " + view.UndressedCount + " bodies undressed");
        }

        private static void Shoot()
        {
            TheatreDynamicsReplay live = _runner.Live;
            if (!_director.Frame(Interval, double.PositiveInfinity, out SafariPose pose)) return;

            _camera.Caption = _burn ? pose.Caption : null;
            string path = Path.Combine(_takeDirectory, "frame-" + pose.TakeFrame.ToString("000000", CultureInfo.InvariantCulture) + ".png");
            _camera.CapturePlaced(live, pose.Eye, pose.Rotation, pose.FieldOfView, pose.Portrait, pose.Focus, pose.Label, path);

            if (_sparkline != null)
            {
                _sparkline.Set(pose.Callout, pose.Second);
                if (pose.Callout != null)
                {
                    if (TheatreUiCapture.ArmOver(_camera.LastFrame, _runner.Ui.Panel, out string note)) _calloutPath = path;
                    else if (_calloutsFailed++ == 0) Debug.LogWarning("[Theatre] safari: the call-out composite was not armed: " + note);
                }
            }

            Assess(live, pose);
        }

        /// <summary>Reads the composite back over the frame it was armed on: the frame with its call-outs.</summary>
        private static void LandTheCallout()
        {
            string path = _calloutPath;
            _calloutPath = null;

            // The composite is written over the frame's own file, so the frame must be on disk
            // first, or the writer's copy would land after it and undo the call-outs.
            SnapshotCamera.FlushWrites();

            if (TheatreUiCapture.Shoot(path, out string note) > 0) _calloutsLanded++;
            else if (_calloutsFailed++ == 0) Debug.LogWarning("[Theatre] safari: the call-out composite was not written: " + note);
        }

        /// <summary>The check's three assertions, on every frame, in both modes (the run's log carries them too).</summary>
        private static void Assess(TheatreDynamicsReplay live, SafariPose pose)
        {
            _frames++;
            var world = new FilmPlans.WorldBounds(live);
            Vector3 eye = pose.Eye;
            bool outside = world.Outside(eye);
            float floor = world.FloorAt(eye.x, eye.z);
            float above = eye.y - floor;

            if (outside) _outsideGlassFrames++;
            else if (above <= 0f) Fault(ref _underBed, pose, string.Format(CultureInfo.InvariantCulture, "under the bed by {0:0.###} m", -above));

            float nearest = float.PositiveInfinity;
            long nearestId = -1;
            foreach (Organism o in live.Sim.World.Living)
            {
                Vector3 at = FilmPlans.Shot.Where(o, _runner.LiveView);
                if (!FilmPlans.Shot.Finite(at)) continue;
                float gap = (eye - at).magnitude - SnapshotCamera.ReachOf(o.Phenotype);
                if (gap < nearest) { nearest = gap; nearestId = o.Id; }
            }
            if (nearest < 0f) Fault(ref _inBody, pose, string.Format(CultureInfo.InvariantCulture, "inside body {0} by {1:0.###} m", nearestId, -nearest));

            float speed = 0f;
            if (_hasLastEye && pose.TakeFrame > 0)
            {
                speed = (eye - _lastEye).magnitude / Interval;
                _fastest = Mathf.Max(_fastest, speed);
                if (speed > FilmPlans.OrbitSpeedCeiling * CeilingTolerance)
                    Fault(ref _overCeiling, pose, string.Format(CultureInfo.InvariantCulture, "moved at {0:0.###} m/s", speed));
            }
            _lastEye = eye;
            _hasLastEye = true;

            _checkLog?.WriteLine(string.Join("\t", _director.Current.Slug, (pose.Take + 1).ToString(CultureInfo.InvariantCulture),
                pose.TakeFrame.ToString(CultureInfo.InvariantCulture),
                pose.Second.ToString("0.###", CultureInfo.InvariantCulture),
                eye.x.ToString("0.###", CultureInfo.InvariantCulture), eye.y.ToString("0.###", CultureInfo.InvariantCulture), eye.z.ToString("0.###", CultureInfo.InvariantCulture),
                floor.ToString("0.###", CultureInfo.InvariantCulture), above.ToString("0.###", CultureInfo.InvariantCulture),
                (float.IsInfinity(nearest) ? "none" : nearest.ToString("0.###", CultureInfo.InvariantCulture)),
                speed.ToString("0.###", CultureInfo.InvariantCulture), outside ? "1" : "0"));
        }

        private static void Fault(ref int count, SafariPose pose, string what)
        {
            count++;
            if (_firstFaults.Count < 12)
                _firstFaults.Add(string.Format(CultureInfo.InvariantCulture, "{0} take {1} frame {2} (t={3:0.#} s): {4}",
                    _director.Current.Slug, pose.Take + 1, pose.TakeFrame, pose.Second, what));
        }

        private static void SceneOver()
        {
            SafariScene scene = _director.Current;
            _outcomes.Add(string.Join("\t",
                (scene.Index + 1).ToString(CultureInfo.InvariantCulture), scene.Slug, scene.Station.ToString(), scene.Subject,
                scene.At.ToString("0.###", CultureInfo.InvariantCulture),
                _runner.Live != null ? _runner.Live.ElapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture) : "",
                _director.Phase == SafariPhase.Parked ? "played" : _director.Phase.ToString().ToLowerInvariant() + ": " + _director.Status,
                (_takesByScene.TryGetValue(scene.Index, out int t) ? t : 0).ToString(CultureInfo.InvariantCulture),
                scene.Plan.Replace('\t', ' ')));

            _warm = false;
            NextScene();
        }

        private static void NextScene()
        {
            _playAt++;
            if (_playAt >= _playList.Count)
            {
                bool anyFailed = _outcomes.Any(o => o.Contains("\tfailed:"));
                Finish(anyFailed ? 1 : 0, _outcomes.Count + " scene(s) done");
                return;
            }

            _director.Begin(_playList[_playAt]);
        }

        // ---------------------------------------------------------------- the end

        private static void Fail(string why)
        {
            Debug.LogError("[Theatre] safari: " + why);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Finish(int code, string verdict)
        {
            EditorApplication.update -= Drive;
            _driving = false;
            Time.captureDeltaTime = 0f;
            SessionState.EraseString(PendingKey);
            if (_runner != null) _runner.HoldView = false;

            // Every frame on disk before scenes.tsv lists the scenes and before the Editor quits;
            // a frame the writer failed on fails the safari, whatever else went right.
            try
            {
                SnapshotCamera.FlushWrites();
            }
            catch (Exception e)
            {
                code = 1;
                verdict += "; FRAMES NOT WRITTEN: " + e.Message;
            }

            if (_camera != null && _camera != _timedCamera)
            {
                _tripTimes.Add(_camera.Times);
                _timedCamera = _camera;
            }

            verdict += "; every frame: " + _tripTimes.Line();

            TheatreUiCapture.Disarm();
            _calloutPath = null;
            if (_sparkline != null)
            {
                verdict += string.Format(CultureInfo.InvariantCulture, "; call-outs composited on {0} frames, {1} failed", _calloutsLanded, _calloutsFailed);
                _sparkline.RemoveFromHierarchy();
                _sparkline = null;
            }

            _camera?.Dispose();
            _camera = null;
            _captions?.Dispose();
            _captions = null;
            _checkLog?.Dispose();
            _checkLog = null;

            try
            {
                if (_out != null && Directory.Exists(_out))
                {
                    var sb = new StringBuilder("index\tslug\tstation\tsubject\tat\tplayed_to\toutcome\ttakes\tplan\n");
                    foreach (string o in _outcomes) sb.Append(o).Append('\n');
                    File.WriteAllText(Path.Combine(_out, "scenes.tsv"), sb.ToString());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Theatre] safari: scenes.tsv not written: " + e.Message);
            }

            string check = string.Format(CultureInfo.InvariantCulture,
                "{0} frames; under the bed {1}, inside a body {2}, over the {3:0.##} m/s ceiling {4} (fastest {5:0.###} m/s); outside the glass {6} (the arrival)",
                _frames, _underBed, _inBody, FilmPlans.OrbitSpeedCeiling, _overCeiling, _fastest, _outsideGlassFrames);
            bool clean = _frames > 0 && _underBed == 0 && _inBody == 0 && _overCeiling == 0;

            var report = new StringBuilder();
            report.Append("\n  into ").Append(_out);
            foreach (string o in _outcomes) report.Append("\n  ").Append(o.Replace('\t', ' '));
            foreach (string f in _firstFaults) report.Append("\n  fault: ").Append(f);

            if (_check)
            {
                int final = code != 0 ? code : clean ? 0 : 1;
                Debug.Log("[Theatre] safari check: " + (final == 0 ? "PASS: " : "FAIL: ") + check + "; " + verdict + report);
                code = final;
            }
            else
            {
                Debug.Log("[Theatre] safari: " + verdict + "; " + check + report);
            }

            if (code != 0) Debug.LogError("[Theatre] safari FAILED: " + verdict);
            _runner = null;
            _director = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode) { Quit(code); return; }

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
            if (!Application.isBatchMode) { Debug.Log("[Theatre] safari finished with code " + code + "."); return; }
            EditorApplication.Exit(code);
        }
    }
}
