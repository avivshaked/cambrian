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
using static Evosim.Theatre.FilmPlans;

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

        /// <summary>
        /// The shots this entry knows, in the order they are named in its errors. The canopy
        /// (2026-09-24) looks up through the leaves at Snell's window; its move is
        /// <c>EVOSIM_THEATRE_CANOPY_MOVE</c> (<see cref="FilmPlans.FilmCanopyMove"/>).
        /// </summary>
        public static readonly string[] KnownShots = { "orbit", "close", "drift", "canopy" };

        /// <summary>The camera's ceiling against its subject in a close shot, m/s (the owner's rule).</summary>
        public const float CloseSpeedCeiling = FilmPlans.CloseSpeedCeiling;

        /// <summary>The orbit's peak speed along its arc, m/s: about what a body swims.</summary>
        public const float OrbitSpeedCeiling = FilmPlans.OrbitSpeedCeiling;

        /// <summary>How far above the bed and below the surface the camera keeps, m.</summary>
        public const float Clearance = FilmPlans.Clearance;

        /// <summary>How far inside the glass the camera keeps, m.</summary>
        public const float GlassClearance = FilmPlans.GlassClearance;

        /// <summary>The share of a strong move spent easing in, and again easing out (safari-spec §9).</summary>
        public const float EaseShare = FilmPlans.EaseShare;

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

            // The canopy's move is read again when the shot is planned; a bad word fails here, in
            // seconds, rather than after the Play-mode reload.
            if (names.Contains("canopy"))
            {
                try { FilmPlans.FilmCanopyMove(); }
                catch (ArgumentException e) { return e.Message + "."; }
            }

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
                    shot.RenderMs.Add(shot.Camera.LastRenderMs);

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
                // Its rows name the frame, so the frame is on disk first.
                if (_trace)
                {
                    SnapshotCamera.FlushWrites();
                    Trace(live);
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

            // Every frame on disk before the report and before the Editor quits; a frame the
            // writer failed on fails the film, whatever else went right.
            try
            {
                SnapshotCamera.FlushWrites();
            }
            catch (Exception e)
            {
                code = 1;
                verdict += "; FRAMES NOT WRITTEN: " + e.Message;
            }

            // Each shot's frames stage by stage, read before its camera goes.
            var stages = new Dictionary<Shot, string>();
            foreach (Shot shot in _shots)
            {
                if (shot.Camera != null) stages[shot] = shot.Camera.Route + "; " + shot.Camera.Times.Line();
            }

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
                if (shot.RenderMs.Count > 0)
                {
                    var sorted = new List<double>(shot.RenderMs);
                    sorted.Sort();
                    double sum = 0.0;
                    foreach (double ms in sorted) sum += ms;
                    report.Append("\n    ").Append(string.Format(CultureInfo.InvariantCulture,
                        "render and read-back: median {0:0.0} ms, mean {1:0.0} ms, slowest {2:0.0} ms a frame over {3} frames",
                        sorted[sorted.Count / 2], sum / sorted.Count, sorted[sorted.Count - 1], sorted.Count));
                }

                if (stages.TryGetValue(shot, out string stage)) report.Append("\n    frames: ").Append(stage);
            }

            report.Append(string.Format(CultureInfo.InvariantCulture,
                "\n  the frame writer: {0} frame(s) written off the main thread, the film waited {1:0.0} s in all for a free slot",
                FrameWriter.Written, FrameWriter.WaitedMs / 1000d));

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

        // ------------------------------------------------------------------ the plans

        /// <summary>
        /// A trapezoid of speed, eased in over the first fifth and out over the last. The
        /// shots and the walls they keep to moved to <see cref="FilmPlans"/> on 2026-09-23 so the
        /// safari's director can use them in Play mode; this entry uses them unchanged.
        /// </summary>
        public static float Ease(float u) => FilmPlans.Ease(u);
    }
}
