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
    /// plan); and <c>captions.tsv</c> (every take's length and first second, and every caption's
    /// span in it, appended a session at a time; <see cref="SafariHeadless.CaptionHeader"/>).
    /// <c>scripts/theatre-safari.ps1</c> launches it and encodes the takes.
    /// </para>
    /// <para>
    /// <b>Text in the frames.</b> A trip's frames carry its captions and its provenance label in
    /// the bitmap font, as they always did; a story's carry neither by default, and
    /// <c>scripts/story-assemble.py</c> sets both as subtitles from <c>captions.tsv</c>
    /// (<see cref="SafariCaptions.TextInFrames"/>, <c>EVOSIM_THEATRE_STORY_BURN_TEXT</c>).
    /// </para>
    /// <para>
    /// <b>Story mode.</b> With <c>EVOSIM_THEATRE_SAFARI_STORY</c> naming a writer's shot list
    /// (<see cref="SafariStory"/>), the trip is the story's scenes for one arm
    /// (<c>EVOSIM_THEATRE_SAFARI_STORY_RUN</c>, the run's own arm when unset) in the story's order,
    /// in place of the heuristic's; every note the reader made is logged as
    /// <c>[Theatre] safari story:</c>, and <c>EVOSIM_THEATRE_SAFARI_SCENES</c> names scenes by
    /// the story's numbers. Each scene's directory, and so its clip, is
    /// <c>story-NN-&lt;arm&gt;-&lt;station&gt;-&lt;subject&gt;</c>, so the clips of every run sort
    /// into the story's order for <c>scripts/story-assemble.py</c>.
    /// </para>
    /// <para>
    /// <b>From farm film windows</b> (<c>EVOSIM_THEATRE_SAFARI_WINDOWS</c>, a folder
    /// <c>scripts/story-windows.py</c> planned and <c>scripts/story-windows.ps1</c> recorded; B3),
    /// a story's scenes are filmed from the windows and the live world is never opened: the runner
    /// opens the first recorded window, the director opens each scene's in turn, and every take's
    /// row in <c>captions.tsv</c> carries its window's word (FAITHFUL, COUSIN or UNVERIFIED), as
    /// <c>scenes.tsv</c>'s last column carries each scene's. A scene with no recorded window is
    /// missed and listed.
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

        // What is stamped into the frames (SafariCaptions.TextInFrames): both for a trip, neither
        // for a story, whose captions and label the join sets as subtitles from captions.tsv;
        // EVOSIM_THEATRE_SAFARI_CAPTIONS=off takes the captions out of a trip's frames alone.
        private static bool _burnCaptions = true;
        private static bool _burnLabel = true;
        private static double _seekMax = 300d;
        private static double _snapAhead = 600d;

        // Story mode (EVOSIM_THEATRE_SAFARI_STORY): a writer's shot list in place of the
        // template's trip, its scenes for one arm (EVOSIM_THEATRE_SAFARI_STORY_RUN, the run's
        // own arm by default) in the story's order.
        private static string _story = "";
        private static string _storyRun = "";
        private static SafariClades _lineage;

        // Film windows (EVOSIM_THEATRE_SAFARI_WINDOWS, B3): the plan's folder, or empty to step the
        // world live; and the word the open take's frames carry.
        private static string _windows = "";
        private static string _takeWord = "COUSIN";

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

        // the story look (StoryLook.cs: on for a story unless EVOSIM_THEATRE_STORY_LOOK=0), its
        // exposure meter, and the story's charts (SafariChartLayer.cs), made the first time a
        // scene with a chart starts a take (the owner, 2026-09-25)
        private static StoryLook _look;
        private static StoryExposure _exposure;
        private static SafariChartLayer _charts;
        private static bool _chartsRefused;
        private static int _settleFrames;

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

            // From film windows the runner opens the first recorded window of the scenes asked for
            // and never the live world; a list with none recorded fails here, before Play mode.
            string firstWindow = null;
            if (_windows.Length > 0)
            {
                firstWindow = FirstRecordedWindow(scenes, out string none);
                if (firstWindow == null) { Fail(none); return; }
            }

            if (!OpenTheTheatreScene()) return;

            // The live world opens from the run's founding and the director restores from there,
            // or the first window opens and the director opens each scene's.
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_RUN", _run);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT", null);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_SEEK", null);
            Environment.SetEnvironmentVariable("EVOSIM_THEATRE_WINDOW", firstWindow);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] safari{0}: {1} of {2} scenes of the {3} trip, {4}, into {5}. Entering Play mode.\n  {6}",
                _check ? " check" : "", _playList.Count, scenes.Count,
                !string.IsNullOrEmpty(_story) ? "story (" + Path.GetFileName(_story) + " for " + _storyRun + ")"
                    : string.IsNullOrEmpty(_clade) ? _heuristic : "one-clade (" + _clade + ")",
                (_check ? "a frame every " + _checkEvery.ToString("0.#", CultureInfo.InvariantCulture) + " s" : _fps + " fps at " + _width + "x" + _height) +
                (_windows.Length > 0 ? ", from the farm's film windows in " + _windows : ", stepping the world live"),
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

            _story = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_STORY") ?? "").Trim().Trim('"');
            _storyRun = "";
            if (_story.Length > 0)
            {
                if (!Path.IsPathRooted(_story)) _story = Path.Combine(BuildIdentity.RepositoryRoot(), _story);
                _story = Path.GetFullPath(_story);
                if (!File.Exists(_story)) return "EVOSIM_THEATRE_SAFARI_STORY: no story at '" + _story + "'.";
                _storyRun = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_STORY_RUN") ?? "").Trim();
                if (_storyRun.Length == 0) _storyRun = arm;
                if (!string.IsNullOrEmpty(_clade))
                    Debug.LogWarning("[Theatre] safari: EVOSIM_THEATRE_SAFARI_CLADE is ignored: the story decides the trip.");
            }

            // Film windows (B3): a story's scenes from the farm's recorded windows, nothing stepped.
            _windows = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_WINDOWS") ?? "").Trim().Trim('"');
            if (_windows.Length > 0)
            {
                if (_story.Length == 0)
                    return "EVOSIM_THEATRE_SAFARI_WINDOWS: film windows are planned for a story's scenes; name the story (EVOSIM_THEATRE_SAFARI_STORY) too.";
                if (!Path.IsPathRooted(_windows)) _windows = Path.Combine(BuildIdentity.RepositoryRoot(), _windows);
                _windows = Path.GetFullPath(_windows);
                try
                {
                    Evosim.Farm.StoryWindows.Read(_windows);
                }
                catch (Exception e)
                {
                    return "EVOSIM_THEATRE_SAFARI_WINDOWS: " + e.Message;
                }
            }

            text = (Environment.GetEnvironmentVariable(SafariCaptions.BurnTextVariable) ?? "").Trim();
            if (text.Length > 0 && text != "0" && text != "1")
                return SafariCaptions.BurnTextVariable + ": '" + text + "' is neither 0 nor 1.";
            bool inFrames = SafariCaptions.TextInFrames(_story.Length > 0);
            _burnLabel = inFrames;
            _burnCaptions = inFrames && Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CAPTIONS") != "off";

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
            if (guidePath == null && string.IsNullOrEmpty(_story)) { why = "no guide: " + where; return null; }

            if (guidePath == null)
            {
                // A story names its subjects by their roots, so it can be filmed on a seed whose
                // guide is not written yet (round 48 seed 3's is written after the run ends).
                _guide = SafariGuide.Empty(where);
                Debug.LogWarning("[Theatre] safari story: WARNING: no guide (" + where + "): every subject is placed from the lineage alone");
            }
            else
            {
                _guide = SafariGuide.Read(guidePath, out string refusal);
                if (_guide == null) { why = "the guide was refused: " + refusal; return null; }
            }

            var checkpoints = SafariDirector.ReadCheckpoints(_run).Select(c => c.seconds).ToList();
            RunRecord record = RunRecord.Load(_run);
            double runSeconds = record?.RequestedSeconds ?? (checkpoints.Count > 0 ? checkpoints[checkpoints.Count - 1] : 0d);

            List<SafariScene> scenes;
            if (!string.IsNullOrEmpty(_story))
            {
                SafariStory story = SafariStory.Read(_story, out string storyRefusal);
                if (story == null) { why = "the story was refused: " + storyRefusal; return null; }

                // The lineage is read only when a subject is not in the guide.
                scenes = story.Trip(_storyRun, _guide, () => _lineage ?? (_lineage = SafariClades.Read(_run)), checkpoints, runSeconds,
                    out List<string> tripNotes);
                foreach (string note in story.Notes.Concat(tripNotes)) Debug.Log("[Theatre] safari story: " + note);

                if (scenes.Count == 0)
                {
                    why = "the story at '" + _story + "' has no scene for the run '" + _storyRun + "'; its runs are " +
                          string.Join(", ", story.Runs) + ". Name the run with EVOSIM_THEATRE_SAFARI_STORY_RUN (-StoryRun).";
                    return null;
                }
            }
            else if (!string.IsNullOrEmpty(_clade))
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
            else if (!string.IsNullOrEmpty(_story))
            {
                // A story's scenes are asked for by the story's own numbers.
                foreach (string word in _scenesWanted.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int at = int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                        ? scenes.FindIndex(s => s.StoryNumber == n) : -1;
                    if (at < 0)
                    {
                        why = "EVOSIM_THEATRE_SAFARI_SCENES: '" + word + "' is not a story number of this run's scenes (" +
                              string.Join(", ", scenes.Select(s => s.StoryNumber)) + "). The trip:\n  " +
                              string.Join("\n  ", scenes.Select(s => s.Line()));
                        return null;
                    }
                    if (!_playList.Contains(at)) _playList.Add(at);
                }
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

        /// <summary>
        /// The directory of the first scene asked for whose main window is recorded, or null with
        /// every scene's reason: what the runner opens first, so the live world is never opened.
        /// </summary>
        private static string FirstRecordedWindow(List<SafariScene> scenes, out string why)
        {
            why = null;
            Evosim.Farm.StoryWindows plan = Evosim.Farm.StoryWindows.Read(_windows);
            var lines = new List<string>();

            foreach (int i in _playList)
            {
                SafariScene s = scenes[i];
                Evosim.Farm.StoryWindow w = plan.Find(s.StoryNumber, Evosim.Farm.StoryWindows.MainPart, s.StoryArm);
                if (w == null)
                {
                    string skipped = plan.SkippedWhy(s.StoryNumber, s.StoryArm);
                    lines.Add("story " + s.StoryNumber + ": no window planned" + (skipped != null ? " (" + skipped + ")" : ""));
                    continue;
                }

                string not = Evosim.Farm.StoryWindows.Recorded(w, out _);
                if (not == null) return w.Directory;
                lines.Add("story " + s.StoryNumber + ": " + not);
            }

            why = "no scene asked for has a recorded film window in " + plan.Path + " (record them with scripts/story-windows.ps1):\n  " +
                  string.Join("\n  ", lines);
            return null;
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
            _wallSeconds.ToString("R", CultureInfo.InvariantCulture), _out, _burnCaptions ? "1" : "0",
            _seekMax.ToString("R", CultureInfo.InvariantCulture),
            _snapAhead.ToString("R", CultureInfo.InvariantCulture),
            _story ?? "", _storyRun ?? "", _burnLabel ? "1" : "0", _windows ?? "");

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
            _burnCaptions = f[11] == "1";
            double.TryParse(f[12], NumberStyles.Float, CultureInfo.InvariantCulture, out _seekMax);
            _snapAhead = 600d;
            if (f.Length > 13) double.TryParse(f[13], NumberStyles.Float, CultureInfo.InvariantCulture, out _snapAhead);
            _story = f.Length > 14 ? f[14] : "";
            _storyRun = f.Length > 15 ? f[15] : "";
            _burnLabel = f.Length > 16 ? f[16] == "1" : true;
            _windows = f.Length > 17 ? f[17] : "";
            _lineage = null;

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
                    if (_windows.Length > 0)
                    {
                        if (_runner.Window == null)
                        {
                            if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                            return;
                        }

                        SetUp();
                        return;
                    }

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

            // The lineage first: a story's subject outside the guide is placed with it.
            SafariClades clades = _lineage ?? (_lineage = SafariClades.Read(_run));

            List<SafariScene> scenes = BuildTrip(out string why);
            if (scenes == null) { Finish(1, why); return; }

            Debug.Log("[Theatre] safari: lineage " + clades.Note + "; guide " + _guide.Path + ", " + _guide.Clades.Count + " clades" +
                      (_guide.Ignored.Count > 0 ? "; keys not read: " + string.Join(", ", _guide.Ignored) : ""));

            _director = new SafariDirector(_runner, _run, _guide, clades, scenes, new SafariOptions
            {
                Aspect = _width / (float)_height,
                Interactive = false,
                MostSeekSeconds = _seekMax,
                MostSnapAheadSeconds = _snapAhead,
                WindowsDirectory = _windows.Length > 0 ? _windows : null,
            });
            if (_windows.Length > 0)
                Debug.Log("[Theatre] safari: filming from the farm's film windows in " + _windows + ": nothing is stepped, and each scene's label carries its window's verdict");
            _director.TakeStarted += OnTakeStarted;
            _director.TakeEnded += OnTakeEnded;

            Directory.CreateDirectory(_out);
            OpenTheCaptions();
            Debug.Log("[Theatre] safari: text in the frames: " +
                      (_burnCaptions && _burnLabel ? "the captions and the label, in the bitmap font"
                          : _burnLabel ? "the label only (EVOSIM_THEATRE_SAFARI_CAPTIONS=off)"
                          : "none: the captions and the label are in captions.tsv for scripts/story-assemble.py to set as subtitles") +
                      " (" + SafariCaptions.BurnTextVariable + "=" + (Environment.GetEnvironmentVariable(SafariCaptions.BurnTextVariable) ?? "unset") + ")");
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

            // The story look, after the skin and the grade are up (the runner's Start) and before
            // the first take renders anything.
            _look = null;
            _exposure = null;
            _charts = null;
            _chartsRefused = false;
            if (StoryLook.Wanted(!string.IsNullOrEmpty(_story)))
            {
                _look = new StoryLook();
                Debug.Log("[Theatre] safari: " + _look.Apply(TheatreSkin.Current, TheatreGrade.Current));
                if (_look.TargetLuma > 0f && TheatreGrade.Current != null)
                    _exposure = new StoryExposure(_look, TheatreGrade.Current.Exposure);
            }
            else if (!string.IsNullOrEmpty(_story))
            {
                Debug.Log("[Theatre] safari: the story look is off (EVOSIM_THEATRE_STORY_LOOK=0): the census's dark field, no meter, no lamp");
            }

            _playAt = 0;
            _director.Begin(_playList[0]);
        }

        /// <summary>How far down the world the camera stands, 0 at the surface and 1 at the bed, for the meter's target.</summary>
        private static float DepthOf(ITheatreFrame live, Vector3 eye)
        {
            Bounds box = SnapshotCamera.BoxOf(live, out _);
            if (!(box.size.y > 0.01f)) return 0.5f;
            return Mathf.Clamp01((box.max.y - eye.y) / box.size.y);
        }

        private static double _takeStartSecond;

        private static void OnTakeStarted(SafariScene scene, int take, string plan)
        {
            CloseTheTake();
            _takeScene = scene;
            _takeIndex = take;
            _takeOpen = true;
            _takeFrames = 0;
            _spanText = null;

            _warm = false;
            _warmFrames = 0;
            _hasLastEye = false;
            _takeNumber = take;
            _takeStartSecond = _director.World?.Second ?? double.NaN;
            _takeWord = _director.ProvenanceWord;
            _takesByScene[scene.Index] = take + 1;

            // The story's lamp rides with the camera on a portrait and a birth and takes the place
            // of the safari's fill there, so a close body carries two lights of the camera's and
            // not three (URP's per-object limit is four, the skin's sun and fill among them).
            bool lamp = _look != null && _look.Lamp > 0f && (scene.Station == SafariStation.Portrait || scene.Station == SafariStation.Birth);

            _camera?.Dispose();
            _camera = new SnapshotCamera(_width, _height)
            {
                FillIntensity = lamp ? 0f : 0.6f,
                LampIntensity = lamp ? _look.Lamp : 0f,
                Meter = _exposure != null,
            };
            if (_look != null && TheatreSkin.Current != null) _camera.Water = TheatreSkin.Current.Water;

            _settleFrames = 0;
            _exposure?.BeginTake(scene.Slug + " take " + (take + 1));

            // The chart's panel is made once, at the camera's supersampled size, the first time a
            // scene carries a chart; each take builds its scene's chart (or clears the last) now,
            // so the panel has drawn it during the warm-up, before the take's first frame.
            if (scene.Chart != null && _charts == null && !_chartsRefused)
            {
                _charts = SafariChartLayer.Create(_camera.TargetWidth, _camera.TargetHeight, out string note);
                if (_charts == null)
                {
                    _chartsRefused = true;
                    Debug.LogWarning("[Theatre] safari: the charts are off: " + note);
                }
                else
                {
                    Debug.Log("[Theatre] safari: charts: " + note);
                }
            }
            _charts?.Begin(scene, SafariChartLayer.InkFor(scene.Clade, _director.View?.Palette));

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
            CloseTheTake();

            if (_exposure != null) Debug.Log("[Theatre] safari: " + _exposure.TakeLine());

            if (_camera == null || _camera == _timedCamera) return;
            _tripTimes.Add(_camera.Times);
            _timedCamera = _camera;
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Theatre] safari: take {0} of {1}, frames: {2}; {3}",
                take + 1, scene.Slug, _camera.Route, _camera.Times.Line()));
        }

        // ---------------------------------------------------------------- captions.tsv
        //
        // What the join needs to set a story's text as subtitles (scripts/story-assemble.py), one
        // row each, the times in seconds into the take's own clip (the frame's index over the frame
        // rate, so a row lands on the frame it names):
        //
        //   format   the session: the file's version, the frame rate, what the frames carry
        //   take     a take that wrote frames: from 0 to its length, the world's second at its
        //            first frame, and the provenance word; the join counts the takes in order to
        //            place each in the scene's clip, and ticks the label's second from here
        //   caption  a caption's span in one take: its first frame to the frame after its last
        //
        // The file is appended to, a session at a time, so a render chain that films a story's
        // scenes into one folder in two sittings keeps both sittings' rows (round 48's second
        // chain overwrote its first's); the join takes each scene's rows from the last session
        // that filmed it. A file in the version-1 form (scene, second, text, clip_offset_s) is
        // moved aside first, not appended to.

        /// <summary>The header of captions.tsv since 2026-09-25.</summary>
        public const string CaptionHeader = "kind\tscene\tstory\tarm\tstation\ttake\tfrom_s\tto_s\tsecond\ttext";

        private static string _spanText;
        private static int _spanFrom;
        private static int _takeFrames;
        private static double _takeFirstSecond = double.NaN;
        private static SafariScene _takeScene;
        private static int _takeIndex = -1;
        private static bool _takeOpen;

        private static void OpenTheCaptions()
        {
            string path = Path.Combine(_out, "captions.tsv");
            string first = null;
            if (File.Exists(path))
            {
                using (var reader = new StreamReader(path)) first = reader.ReadLine();
                if (first != CaptionHeader)
                {
                    string aside = Path.Combine(_out, "captions-v1-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".tsv");
                    File.Move(path, aside);
                    Debug.Log("[Theatre] safari: captions.tsv in the older form is moved aside to " + aside);
                    first = null;
                }
            }

            _captions = new StreamWriter(path, true);
            if (first == null) _captions.WriteLine(CaptionHeader);
            _captions.WriteLine(string.Join("\t", "format", "-", "-", "-", "-", "-", "-", "-", "-",
                string.Format(CultureInfo.InvariantCulture, "v2 fps={0:0.######} captions_in_frames={1} label_in_frames={2} session={3:yyyy-MM-ddTHH:mm:ss}",
                    1d / Interval, _burnCaptions ? 1 : 0, _burnLabel ? 1 : 0, DateTime.Now)));
            _captions.Flush();
        }

        private static string Tsv(string text) => (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        private static string ArmOf(SafariScene scene) =>
            !string.IsNullOrEmpty(scene?.StoryArm) ? scene.StoryArm : new DirectoryInfo(_run ?? "").Parent?.Name ?? "run";

        private static void CaptionRow(string kind, int fromFrame, int toFrame, double second, string text)
        {
            if (_captions == null || _takeScene == null) return;
            _captions.WriteLine(string.Join("\t", kind, _takeScene.Slug,
                _takeScene.StoryNumber.ToString(CultureInfo.InvariantCulture), Tsv(ArmOf(_takeScene)), _takeScene.Station.ToString(),
                (_takeIndex + 1).ToString(CultureInfo.InvariantCulture),
                (fromFrame * (double)Interval).ToString("0.######", CultureInfo.InvariantCulture),
                (toFrame * (double)Interval).ToString("0.######", CultureInfo.InvariantCulture),
                double.IsNaN(second) ? "" : second.ToString("0.###", CultureInfo.InvariantCulture), Tsv(text)));
        }

        /// <summary>One frame's caption: a span closes when the caption on screen changes and opens with the next.</summary>
        private static void Span(string caption, int frame, double second)
        {
            if (_takeFrames == 0) _takeFirstSecond = second;
            if (caption != _spanText)
            {
                if (_spanText != null) CaptionRow("caption", _spanFrom, frame, _spanSecond, _spanText);
                _spanText = caption;
                _spanFrom = frame;
                _spanSecond = second;
            }
            _takeFrames = Math.Max(_takeFrames, frame + 1);
        }

        private static double _spanSecond;

        /// <summary>The take's rows: the open caption closed at its last frame, and the take's own row. Once per take.</summary>
        private static void CloseTheTake()
        {
            if (!_takeOpen) return;
            _takeOpen = false;
            if (_takeFrames > 0)
            {
                if (_spanText != null) CaptionRow("caption", _spanFrom, _takeFrames, _spanSecond, _spanText);
                CaptionRow("take", 0, _takeFrames, _takeFirstSecond, _takeWord);
            }
            _captions?.Flush();
            _spanText = null;
            _takeFrames = 0;
            _takeFirstSecond = double.NaN;
        }

        /// <summary>Renders the take's first pose until every body is dressed, as the film's warm-up does.</summary>
        private static void WarmUp()
        {
            LiveWorldView view = _director.View;
            IFilmWorld live = _director.World;
            FilmPlans.Shot shot = _director.CurrentShot;
            if (view == null || live == null || shot == null) { _warm = true; return; }

            // The take starts at the exposure the last take ended on.
            if (_warmFrames == 0 && _exposure != null) TheatreGrade.Current?.SetExposure(_exposure.Ev);

            _director.SyncView();
            shot.Pose(live, view, 0f, Interval, out Vector3 eye, out Quaternion rotation, out float focus);
            _camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, "", null);
            _warmFrames++;

            bool dressed = _warmFrames >= 3 && view.UndressedCount == 0 && view.PlainRendererCount() == 0;
            if (!dressed)
            {
                if (_warmFrames >= 60) Finish(1, "warm-up 60 frames and the skin has not dressed the crowd: " + view.UndressedCount + " bodies undressed");
                return;
            }

            // The story's meter settles on the dressed first pose before the first frame is kept:
            // each warm-up render is measured (the camera reads it back when metering) and the
            // exposure stepped toward the target, until two renders running are within
            // StoryExposure.SettledWithinEv or the cap is reached.
            if (_exposure != null && !_exposure.Settled && _settleFrames < StoryExposure.MostSettleFrames)
            {
                _exposure.Settle(_camera.LastMeanLuma, DepthOf(live, eye));
                TheatreGrade.Current?.SetExposure(_exposure.Ev);
                _settleFrames++;
                if (!_exposure.Settled && _settleFrames < StoryExposure.MostSettleFrames) return;
            }

            shot.ResetTally();
            _warm = true;
        }

        private static void Shoot()
        {
            if (!_director.Frame(Interval, double.PositiveInfinity, out SafariPose pose)) return;
            IFilmWorld live = _director.World;

            Span(pose.Caption, pose.TakeFrame, pose.Second);
            _camera.Caption = _burnCaptions ? pose.Caption : null;
            _camera.Dim = pose.Dim;

            // The scene's chart, if it shows on this frame: composited into the render before the
            // box filter, and a corner card left out of the meter.
            bool chart = _charts != null && _charts.Frame(pose, live, Interval);
            _camera.OverRender = chart ? _charts.Composite : null;
            _camera.MeterExclude = chart ? _charts.MeterExclude(_width, _height) : new RectInt(0, 0, 0, 0);

            string path = Path.Combine(_takeDirectory, "frame-" + pose.TakeFrame.ToString("000000", CultureInfo.InvariantCulture) + ".png");
            _camera.CapturePlaced(live, pose.Eye, pose.Rotation, pose.FieldOfView, pose.Portrait, pose.Focus, _burnLabel ? pose.Label : null, path);

            // The meter follows the picture slowly, and holds under a full chart, whose dimmed
            // world it must not answer.
            if (_exposure != null)
            {
                if (chart && _charts.FullVisible) _exposure.Hold(_camera.LastMeanLuma);
                else _exposure.Track(_camera.LastMeanLuma, DepthOf(live, pose.Eye));
                TheatreGrade.Current?.SetExposure(_exposure.Ev);
            }

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
        private static void Assess(IFilmWorld live, SafariPose pose)
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
            LiveWorldView view = _director.View;
            for (int i = 0; i < live.BodyCount; i++)
            {
                Vector3 at = FilmPlans.Shot.Where(live, i, view);
                if (!FilmPlans.Shot.Finite(at)) continue;
                float gap = (eye - at).magnitude - SnapshotCamera.ReachOf(live.PhenotypeOf(i));
                if (gap < nearest) { nearest = gap; nearestId = live.IdAt(i); }
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
            CloseTheTake();
            SafariScene scene = _director.Current;
            _outcomes.Add(string.Join("\t",
                (scene.Index + 1).ToString(CultureInfo.InvariantCulture), scene.Slug, scene.Station.ToString(), scene.Subject,
                scene.At.ToString("0.###", CultureInfo.InvariantCulture),
                _director.World != null ? _director.World.Second.ToString("0.###", CultureInfo.InvariantCulture) : "",
                _director.Phase == SafariPhase.Parked ? "played" : _director.Phase.ToString().ToLowerInvariant() + ": " + _director.Status,
                (_takesByScene.TryGetValue(scene.Index, out int t) ? t : 0).ToString(CultureInfo.InvariantCulture),
                scene.Plan.Replace('\t', ' '),
                _director.SceneProvenance));

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

            if (_charts != null)
            {
                _charts.Dispose();
                _charts = null;
            }
            if (_look != null)
            {
                _look.Restore();
                _look = null;
            }
            _exposure = null;

            _camera?.Dispose();
            _camera = null;
            CloseTheTake();
            _takeScene = null;
            _captions?.Dispose();
            _captions = null;
            _checkLog?.Dispose();
            _checkLog = null;

            try
            {
                if (_out != null && Directory.Exists(_out))
                {
                    var sb = new StringBuilder("index\tslug\tstation\tsubject\tat\tplayed_to\toutcome\ttakes\tplan\tprovenance\n");
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
