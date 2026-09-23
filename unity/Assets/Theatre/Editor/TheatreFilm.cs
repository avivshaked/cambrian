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

        /// <summary>The orbit's peak speed along its arc, m/s: about what a body swims.</summary>
        public const float OrbitSpeedCeiling = 0.5f;

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
        private static bool _trace;
        private static bool _raw;
        private static double _closeSeconds = 20d;
        private static bool _closeStill = true;
        private static StreamWriter _traceWriter;
        private static Vector3 _traceEye;
        private static Quaternion _traceRotation = Quaternion.identity;
        private static bool _traceHasCamera;
        private static float _turns = 0.25f;
        private static double _wallSecondsAllowed = 1800d;

        private static double _deadline;
        private static bool _driving;
        private static TheatreRunner _runner;
        private static bool _setUp;
        private static bool _freeze;
        private static float _firstClock, _lastClock;
        private static int _clockSlips;
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
        /// <summary>
        /// One row per link per frame for every living body (or
        /// of the largest body when no close shot runs): the solver's position and rotation, and
        /// the view's part transform and first visual's local scale. Tab-separated, in
        /// <c>trace.tsv</c> beside the shot directories.
        /// </summary>
        private static void Trace(TheatreDynamicsReplay live)
        {
            LiveWorldView view = _runner.LiveView;
            if (view == null || live?.Sim == null) return;

            if (_traceWriter == null)
            {
                Directory.CreateDirectory(_directory);
                _traceWriter = new StreamWriter(Path.Combine(_directory, "trace.tsv"), false);
                _traceWriter.WriteLine("frame	t	id	link	px	py	pz	rx	ry	rz	rw	vx	vy	vz	qx	qy	qz	qw	sx	sy	sz	lossy	u	v	depth	hx	hy	hz	shape	visuals	visualScales");
            }

            long subject = -1;
            foreach (Shot shot in _shots) if (shot.Subject >= 0) { subject = shot.Subject; break; }

            IReadOnlyList<Organism> living = live.Sim.World.Living;
            Vector3 centre = default;
            bool haveCentre = false;

            if (subject >= 0 && live.Sim.TryPose(subject, out Evosim.Dynamics.Creature s) && s != null && s.Links > 0)
            {
                centre = new Vector3((float)s.Position[0], (float)s.Position[1], (float)s.Position[2]);
                haveCentre = true;
            }

            double t = Now(live);
            float fov = 50f;
            foreach (Shot shot in _shots) if (shot.Name == "close") fov = shot.FieldOfView;
            float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            float tanH = tanV * _width / (float)_height;
            Quaternion inverse = Quaternion.Inverse(_traceRotation);

            for (int i = 0; i < living.Count; i++)
            {
                long id = living[i].Id;
                if (!live.Sim.TryPose(id, out Evosim.Dynamics.Creature body) || body == null) continue;

                var root = new Vector3((float)body.Position[0], (float)body.Position[1], (float)body.Position[2]);

                Transform viewRoot = view.RootOf(id);

                for (int l = 0; l < body.Links; l++)
                {
                    Transform part = viewRoot != null && l < viewRoot.childCount ? viewRoot.GetChild(l) : null;
                    Transform visual = part != null && part.childCount > 0 ? part.GetChild(0) : null;
                    PhenotypePart ph = body.Phenotype != null && l < body.Phenotype.PartCount ? body.Phenotype.Parts[l] : null;

                    Vector3 vp = part != null ? part.localPosition : default;
                    Quaternion vq = part != null ? part.localRotation : Quaternion.identity;
                    Vector3 sc = visual != null ? visual.localScale : default;
                    float lossy = visual != null ? visual.lossyScale.magnitude : 0f;

                    // Screen position under the close shot's camera: u across from the left, v down
                    // from the top, both 0 to 1 inside the frame; depth along the view axis, m.
                    var world = new Vector3((float)body.Position[3 * l], (float)body.Position[3 * l + 1], (float)body.Position[3 * l + 2]);
                    Vector3 cam = inverse * (world - _traceEye);
                    float u = cam.z > 1e-3f ? 0.5f + 0.5f * (cam.x / cam.z) / tanH : -1f;
                    float vv = cam.z > 1e-3f ? 0.5f - 0.5f * (cam.y / cam.z) / tanV : -1f;

                    _traceWriter.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}	{1:0.###}	{2}	{3}	{4:0.####}	{5:0.####}	{6:0.####}	{7:0.####}	{8:0.####}	{9:0.####}	{10:0.####}" +
                        "	{11:0.####}	{12:0.####}	{13:0.####}	{14:0.####}	{15:0.####}	{16:0.####}	{17:0.####}	{18:0.####}	{19:0.####}	{20:0.####}	{21:0.####}	{22:0.###}	{23:0.###}	{24:0.##}	{25:0.####}	{26:0.####}	{27:0.####}	{28}	{29}	{30}",
                        _next, t, id, l,
                        body.Position[3 * l], body.Position[3 * l + 1], body.Position[3 * l + 2],
                        body.Rotation[4 * l], body.Rotation[4 * l + 1], body.Rotation[4 * l + 2], body.Rotation[4 * l + 3],
                        vp.x, vp.y, vp.z, vq.x, vq.y, vq.z, vq.w, sc.x, sc.y, sc.z, lossy, u, vv, cam.z,
                        ph != null ? ph.HalfExtents.X : 0f, ph != null ? ph.HalfExtents.Y : 0f, ph != null ? ph.HalfExtents.Z : 0f,
                        ph != null ? ph.ShapeId : "?", part != null ? part.childCount : -1, VisualScales(part)));
                }
            }

            _traceWriter.Flush();
        }

        /// <summary>Every visual under a part: its mesh's name and local scale, semicolon-separated.</summary>
        private static string VisualScales(Transform part)
        {
            if (part == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int v = 0; v < part.childCount; v++)
            {
                Transform visual = part.GetChild(v);
                var filter = visual.GetComponent<MeshFilter>();
                Vector3 sc = visual.localScale;
                if (v > 0) sb.Append(';');
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0}:{1:0.####},{2:0.####},{3:0.####}",
                    filter != null && filter.sharedMesh != null ? filter.sharedMesh.name.Replace(' ', '_') : "none", sc.x, sc.y, sc.z));
            }
            return sb.ToString();
        }

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

            // A diagnostic: the world is not stepped, so a clip's only motion is the camera's and
            // the shaders' clock. Off by default.
            _freeze = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_FREEZE") == "1";
            _trace = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_TRACE") == "1";
            // A diagnostic: the bodies are drawn as the colliders the physics has, no rounding,
            // carve, taper or bend, as the runner's X key does.
            _raw = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_RAW") == "1";

            // The close shot: held still by default (the owner, 2026-09-23 evening: with the
            // camera following its subject nothing in the frame said whether the camera or the
            // creatures moved), and shorter, since a drifting subject leaves a still frame in
            // tens of seconds. EVOSIM_THEATRE_FILM_CLOSE_FOLLOW=1 restores the follow and the
            // dolly; EVOSIM_THEATRE_FILM_CLOSE_SECONDS sets its length (default 20, never past
            // the film's own).
            _closeStill = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_FOLLOW") != "1";
            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_SECONDS");
            _closeSeconds = Math.Min(_seconds, 20d);
            if (!string.IsNullOrWhiteSpace(text) &&
                (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _closeSeconds) ||
                 !(_closeSeconds > 0d) || _closeSeconds > _seconds))
            {
                return "EVOSIM_THEATRE_FILM_CLOSE_SECONDS: '" + text + "' is not a length between 0 and the film's " +
                       _seconds.ToString("0.###", CultureInfo.InvariantCulture) + " s.";
            }

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
            _wallSecondsAllowed.ToString("R", CultureInfo.InvariantCulture) + "|" +
            (_freeze ? "1" : "0") + "|" +
            (_trace ? "1" : "0") + "|" +
            (_raw ? "1" : "0") + "|" +
            _closeSeconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
            (_closeStill ? "1" : "0");

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
            _freeze = f.Length > 7 && f[7] == "1";
            _trace = f.Length > 8 && f[8] == "1";
            _raw = f.Length > 9 && f[9] == "1";
            if (f.Length > 10) double.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out _closeSeconds);
            _closeStill = f.Length <= 11 || f[11] == "1";

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

                // Stepped here, a whole frame interval in one tick. There was a wall budget that
                // spread a heavy frame's steps across ticks; with Time.captureDeltaTime set, every
                // tick advances the shaders' clock by one frame interval, so a tick without a
                // capture would be a jump in the caustics and the ripples.
                //
                // The film's clock is the physics step's, never World.ElapsedSeconds: the world's
                // clock moves in metabolic steps of half a second, and a film read off it put five
                // frames on one instant and then jumped (the first smoke, 2026-09-23).
                if (!_freeze)
                {
                    while (Now(live) + 0.5d * dt < target) live.Step();
                }

                _runner.LiveView?.Sync();

                // A body born since the last frame is built plain and the palette's rotation reaches
                // it frames later; it is dressed here, before the shutter, instead.
                if (_runner.LiveView != null) _dressedLate += _runner.LiveView.DressUndressed();


                // The shaders' clock against the film's: one frame interval a capture, or the log
                // says how often it was not.
                float clock = Time.time;
                if (_next > 0 && Mathf.Abs(clock - _lastClock - 1f / _fps) > 0.25f / _fps) _clockSlips++;
                if (_next == 0) _firstClock = clock;
                _lastClock = clock;

                double at = Now(live);
                if (!_freeze) _worstLag = Mathf.Max(_worstLag, (float)Math.Abs(at - target));
                if (_next == 0) _firstAt = at;
                _lastAt = at;

                float u = _frames > 1 ? (float)_next / (_frames - 1) : 0f;
                string label = string.Format(CultureInfo.InvariantCulture, "COUSIN  t={0:0.0} s{1}", at,
                    _freeze ? "  FROZEN" : "");
                string frameName = "frame-" + _next.ToString("000000", CultureInfo.InvariantCulture) + ".png";

                foreach (Shot shot in _shots)
                {
                    if (_next >= shot.Frames) continue;
                    u = shot.Frames > 1 ? (float)_next / (shot.Frames - 1) : 0f;

                    shot.Pose(live, _runner.LiveView, u, 1f / _fps, out Vector3 eye, out Quaternion rotation, out float focus);

                    if (_trace && (shot.Name == "close" || !_traceHasCamera))
                    {
                        _traceEye = eye; _traceRotation = rotation; _traceHasCamera = true;
                    }

                    shot.Camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, label,
                        Path.Combine(shot.Directory, frameName));

                    if (_next == 0 || _next == shot.Frames / 2 || _next == shot.Frames - 1)
                    {
                        shot.Camera.LastPictureSpread(out float mean, out float spread);
                        shot.Spreads.Add(string.Format(CultureInfo.InvariantCulture,
                            "frame {0}: luminance mean {1:0.0}, sd {2:0.0}{3}", _next, mean, spread,
                            spread < 2f ? " (UNIFORM: the device may have rendered nothing)" : ""));
                    }
                }

                // A diagnostic trace, off by default: every body within a few metres of the close
                // shot's subject, link by link, as the solver holds it, as the view draws it and
                // where the close shot's camera puts it on screen; one row per link per frame.
                if (_trace) Trace(live);

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

            // Unity's offline-recording clock, set before the warm-up so the first capture is
            // already on it: Time.time, Time.deltaTime and the shaders' _Time then advance one frame
            // interval per player-loop frame whatever the wall clock does. Without it the caustic
            // net on the bodies and the bed (TheatreBody.shader, TheatreBed.shader), the shafts'
            // gain and the surface ripples all ran on the Editor's uneven wall time between
            // captures, and r46-s1's first clips flickered on every body (2026-09-23). Put back to
            // 0 in Finish, which every exit goes through.
            Time.captureDeltaTime = 1f / _fps;
            _clockSlips = 0;

            // The viewer's own camera has nothing to show a batch Editor, and would render the whole
            // world every tick for nobody.
            Camera view = _runner.ViewCamera;
            if (view != null) view.enabled = false;

            if (_raw && _runner.LiveView?.Palette != null)
            {
                _runner.LiveView.Palette.RawShapes = true;
                _runner.LiveView.Palette.Clear();
            }

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
                bool close = name == "close";
                Shot shot = Shot.Plan(name, live, _runner.LiveView, world,
                    (float)(close ? _closeSeconds : _seconds), _width / (float)_height, _turns, close && _closeStill);
                shot.Frames = close ? Math.Max(1, (int)Math.Round(_closeSeconds * _fps)) : _frames;
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
            if (_traceWriter != null) { _traceWriter.Dispose(); _traceWriter = null; }
            EditorApplication.update -= Drive;
            _driving = false;
            Time.captureDeltaTime = 0f;
            SessionState.EraseString(PendingKey);

            foreach (Shot shot in _shots)
            {
                if (shot.Camera != null) { shot.Camera.Dispose(); shot.Camera = null; }
            }

            var report = new System.Text.StringBuilder();
            report.AppendFormat(CultureInfo.InvariantCulture,
                "\n  first frame at t={0:0.###} s, last at t={1:0.###} s, worst distance from the asked-for second {2:0.####} s; " +
                "warm-up {3} frames; {4} bodies born during the film dressed before their first frame" +
                "\n  shaders' clock (Time.time) {5:0.####} to {6:0.####} s, {7} capture interval(s) off 1/{8} s{9}",
                _firstAt, _lastAt, _worstLag, _warmFrames, _dressedLate,
                _firstClock, _lastClock, _clockSlips, _fps,
                _freeze ? "; FROZEN: the world was not stepped" : "");

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
            /// <summary>Frames this shot captures; the close shot's are fewer than the film's.</summary>
            public int Frames;
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

            /// <summary>The body the close shot follows, or -1 for any other shot.</summary>
            public long Subject => _subject;
            private Vector3 _offset;
            private Vector3 _forward;
            private float _standoff;
            private float _dolly;
            private Vector3 _followed;
            private bool _hasFollowed;
            private bool _still;
            private Vector3 _stillEye;
            private Quaternion _stillRotation;
            private bool _hasStill;

            // the orbit's aim below the crowd, so the bed and the far glass share the frame
            private float _aimDown;

            // the tally
            private int _frames, _glass, _bed, _surface, _pushed, _inside;
            private float _fastest, _fastestRelative;
            private Vector3 _lastEye, _lastSubject;
            private bool _hasLast;

            public static Shot Plan(
                string name, TheatreDynamicsReplay live, LiveWorldView view, WorldBounds world,
                float seconds, float aspect, float turns, bool still = false)
            {
                var shot = new Shot { Name = name, _world = world, _seconds = seconds, _still = still };

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
                // A crowd near the surface leaves a tilted-down orbit no room, and the two
                // fallbacks this had (level at the crowd's depth, then 15 deg up from under it)
                // framed water and bodies and nothing fixed, so a viewer could not tell the
                // camera's motion from the creatures' (the owner, 2026-09-23 evening). So when
                // the surface caps the lift below half of what is wanted, the orbit stays level
                // at the crowd's depth, as far out as the glass allows, and aims ten degrees
                // below the centroid: the crowd sits in the upper third of the frame, the bed
                // and the far glass fill the rest, and the surface's underside crosses the top.
                if (!drift && headroom < room && most < 0.5f * wanted)
                {
                    float surfaceCap = most;
                    _elevation = 0f;
                    _aimDown = 10f * Mathf.Deg2Rad;
                    most = _world.RoomAround(_centre);
                    tilt = string.Format(CultureInfo.InvariantCulture,
                        "level at the crowd's depth aiming 10 deg down at the bed: the surface capped 12 deg down at {0:0.##} m",
                        surfaceCap);
                }

                _distance = Mathf.Max(2f, Mathf.Min(wanted, most));

                _azimuth = -0.5f * Mathf.PI;
                _turns = turns;

                string ring = "";
                if (!drift)
                {
                    // The ring must clear the bed all the way round: a tank with a beach has a
                    // shoal that rises to the surface, and a level orbit at the glass's room
                    // crossed it (r46-s2 at 5,000 s: the bed clamp lifted the camera 396 times
                    // in 360 frames and the last frames skimmed the sand). Sampled every five
                    // degrees; the ring shrinks until every sample's floor is two metres and the
                    // clearance below the eye, and never under twenty metres.
                    float asked = _distance;
                    float eyeY = _centre.y + _distance * Mathf.Sin(_elevation);
                    while (_distance > 20f && !RingClearsTheBed(eyeY)) _distance -= 2f;
                    if (_distance < asked)
                    {
                        ring = string.Format(CultureInfo.InvariantCulture,
                            ", ring shrunk from {0:0.##} to {1:0.##} m to clear the bed", asked, _distance);
                    }

                    // The owner's rule: nothing moves faster than a body swims. The arc's peak
                    // speed is capped at half a metre a second, which at the tank's radius is a
                    // few degrees of turn a minute; the film says what it turned.
                    float peakPerTurn = 2f * Mathf.PI * _distance * Mathf.Cos(_elevation) / _seconds / (1f - EaseShare);
                    float allowed = peakPerTurn > 1e-6f ? OrbitSpeedCeiling / peakPerTurn : turns;
                    if (_turns > allowed)
                    {
                        ring += string.Format(CultureInfo.InvariantCulture,
                            ", turn cut from {0:0.###} to {1:0.###} for the {2:0.##} m/s ceiling", _turns, allowed, OrbitSpeedCeiling);
                        _turns = allowed;
                    }
                }

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
                            "{0:0.##} turn(s), {3}, {2:0.###} m/s along the arc at its peak{4}",
                            _turns, _elevation * Mathf.Rad2Deg,
                            _turns * 2f * Mathf.PI * _distance * Mathf.Cos(_elevation) / _seconds / (1f - EaseShare),
                            tilt, ring));
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
                    "({6:0.###} m/s at its peak), {7}",
                    _subject, reaches[anchor],
                    neighbour >= 0
                        ? string.Format(CultureInfo.InvariantCulture, " and body {0} (reach {1:0.###} m)", ids[neighbour], reaches[neighbour])
                        : ", no neighbour within reach",
                    framed.size.magnitude, _standoff, _dolly, _dolly / _seconds / (1f - EaseShare),
                    _still ? "held still a quarter further back, no dolly, no follow" : "following the subject");
            }

            /// <summary>True when every point of the orbit's ring stands over a floor two metres and the clearance below the eye.</summary>
            private bool RingClearsTheBed(float eyeY)
            {
                float horizontal = _distance * Mathf.Cos(_elevation);
                for (int i = 0; i < 72; i++)
                {
                    float theta = i * (2f * Mathf.PI / 72f);
                    float x = _centre.x + horizontal * Mathf.Cos(theta);
                    float z = _centre.z + horizontal * Mathf.Sin(theta);
                    if (_world.FloorAt(x, z) + Clearance + 2f > eyeY) return false;
                }
                return true;
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

                    if (_still)
                    {
                        // Framed once, a quarter further back than the following shot so the
                        // subject's drift has room, and never moved: every motion in the clip
                        // is a creature's or the water's.
                        if (!_hasStill)
                        {
                            _stillEye = subject - _forward * (1.25f * _standoff);
                            _stillRotation = Quaternion.LookRotation(_forward, Vector3.up);
                            _hasStill = true;
                        }

                        eye = _stillEye;
                        rotation = _stillRotation;
                        focus = 1.25f * _standoff;
                    }
                    else
                    {
                        eye = subject - _forward * distance;
                        rotation = Quaternion.LookRotation(_forward, Vector3.up);
                        focus = distance;
                    }
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
                    subject = _centre - Vector3.up * (_distance * Mathf.Tan(_aimDown));
                    rotation = Quaternion.LookRotation(subject - eye, Vector3.up);
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
