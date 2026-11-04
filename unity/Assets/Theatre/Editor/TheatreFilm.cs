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
    /// <para>
    /// <b>Or it is a farm film window, and nothing is stepped</b> (<c>EVOSIM_THEATRE_WINDOW</c>,
    /// <c>theatre-film.ps1 -FromFarm</c>; <c>record-and-film-spec.md</c> B3). The window the farm
    /// recorded is played from its first frame (or from <c>EVOSIM_THEATRE_SEEK</c>), each film
    /// frame is the farm's frame nearest its second, a film longer than the window is cut to it
    /// and says so, and the label's word is the window's verdict: FAITHFUL, COUSIN or UNVERIFIED.
    /// The trace (<c>-Trace</c>) reads the solver, so it is the live world's alone.
    /// </para>
    /// <code>
    /// # NO -quit, and NO -nographics: the entry quits, and rendering needs a graphics device.
    /// $env:EVOSIM_THEATRE_CHECKPOINT = "$PWD/runs/ckUi/.../checkpoints/000000400.ckpt"
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
        /// <c>EVOSIM_THEATRE_CANOPY_MOVE</c> (<see cref="FilmPlans.FilmCanopyMove"/>). The corpse shots,
        /// sink and vanish (2026-09-28), are <see cref="FilmCorpseShots"/>' and are filmed from a window.
        /// </summary>
        public static readonly string[] KnownShots = { "orbit", "close", "drift", "canopy", "sink", "vanish" };

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

        /// <summary>
        /// Seconds of the run a second of film (<c>EVOSIM_THEATRE_FILM_SPEED</c>): 1 at the run's
        /// own pace, more for a time-lapse, which is filmed from a window only.
        /// </summary>
        private static double _speed = 1d;

        /// <summary>True when the film plays a farm film window (<c>EVOSIM_THEATRE_WINDOW</c>) rather than a continued world.</summary>
        private static bool _fromWindow;

        /// <summary>True when the film plays a nursery tank (<c>EVOSIM_THEATRE_NURSERY</c>), re-read from the variable across the domain reload.</summary>
        private static bool _fromNursery;

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

        // ------------------------------------------------------------------ the stage timers

        /// <summary>
        /// The Editor's first look at the film's code and the last stage mark, both as UTC tick counts in <see cref="SessionState"/>,
        /// so the clock survives the domain reload Play mode causes. The <c>[Theatre] timing:</c> lines read them; nothing else does.
        /// </summary>
        private const string StartKey = "Evosim.Theatre.Film.TimingStart";
        private const string MarkKey = "Evosim.Theatre.Film.TimingMark";

        private static bool _playMarked;
        private static bool _worldMarked;
        private static int _logLines;
        private static readonly Dictionary<string, int> _logKinds = new Dictionary<string, int>();

        /// <summary>Starts the timers' clock if none runs (a batch Editor's first load), or restarts it at once for an Editor a person has open.</summary>
        private static void NoteStart(bool restart)
        {
            if (!restart && !string.IsNullOrEmpty(SessionState.GetString(StartKey, ""))) return;

            string now = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            SessionState.SetString(StartKey, now);
            SessionState.SetString(MarkKey, now);
        }

        private static long TicksOf(string key, long fallback) =>
            long.TryParse(SessionState.GetString(key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) ? ticks : fallback;

        /// <summary>One line: the stage that just ended, its seconds, and the seconds since the clock started.</summary>
        private static void Stage(string name)
        {
            long now = DateTime.UtcNow.Ticks;
            long start = TicksOf(StartKey, now);
            long mark = TicksOf(MarkKey, start);
            SessionState.SetString(MarkKey, now.ToString(CultureInfo.InvariantCulture));

            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Theatre] timing: {0} {1:0.00} s (since start {2:0.00} s)",
                name, (now - mark) / 1e7, (now - start) / 1e7));
        }

        /// <summary>Counts every log line the Editor receives, by the kind its first words name (<see cref="LogKind"/>).</summary>
        private static void CountLog(string message, string stack, LogType type)
        {
            _logLines++;
            string kind = LogKind(message);
            _logKinds.TryGetValue(kind, out int seen);
            _logKinds[kind] = seen + 1;
        }

        /// <summary>The words after "[Theatre] " up to the first colon, comma, bracket or number, so "one skin: body 3" and "one skin: body 4" are one kind.</summary>
        private static string LogKind(string message)
        {
            const string prefix = "[Theatre] ";
            if (message == null || !message.StartsWith(prefix, StringComparison.Ordinal)) return "(not theatre)";

            int end = prefix.Length;
            int stop = Math.Min(message.Length, prefix.Length + 40);
            while (end < stop)
            {
                char c = message[end];
                if (c == ':' || c == ',' || c == '(' || (char.IsDigit(c) && message[end - 1] == ' ')) break;
                end++;
            }

            return message.Substring(prefix.Length, end - prefix.Length).TrimEnd();
        }

        /// <summary>
        /// What the dressing has spent so far, from the sums <see cref="DressTimers"/> keeps: the one skins, the pattern tiles, the
        /// palette's paint (and the pieces of it that are timed), the skin's dress, and how many log lines the Editor received.
        /// </summary>
        private static void DressLine(string when)
        {
            DressTimers.PatternTiles(out int tiles, out long tileMs);
            double other = DressTimers.PaintMs - DressTimers.PatternLookupMs - DressTimers.ArcBendMs - DressTimers.NoseMs - DressTimers.MouthMs;

            var kinds = new List<KeyValuePair<string, int>>(_logKinds);
            kinds.Sort((a, b) => b.Value.CompareTo(a.Value));
            var top = new System.Text.StringBuilder();
            for (int i = 0; i < kinds.Count && i < 8; i++)
            {
                if (i > 0) top.Append(", ");
                top.Append(kinds[i].Key).Append(' ').Append(kinds[i].Value.ToString(CultureInfo.InvariantCulture));
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] timing: dress {0}: one skins {1} in {2:0.00} s; pattern tiles {3} in {4:0.00} s; " +
                "palette paint {5} calls in {6:0.00} s (pattern lookup {7:0.00} s, tiles included; arc bend {8:0.00} s; nose {9:0.00} s; mouth {10:0.00} s; other {11:0.00} s); " +
                "skin dress {12:0.00} s; Editor log lines {13} ({14})",
                when, DressTimers.OneSkins, DressTimers.OneSkinMs / 1000d, tiles, tileMs / 1000d,
                DressTimers.Paints, DressTimers.PaintMs / 1000d, DressTimers.PatternLookupMs / 1000d, DressTimers.ArcBendMs / 1000d,
                DressTimers.NoseMs / 1000d, DressTimers.MouthMs / 1000d, other / 1000d,
                DressTimers.SkinDressMs / 1000d, _logLines, top));
        }

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
        // nursery2:begin guard=_fromNursery
        /// <summary>The film frame a nursery shot starts at (the nose pass starts later than the follow); a shot absent here starts at frame 0.</summary>
        private static readonly Dictionary<Shot, int> _firstFrames = new Dictionary<Shot, int>();
        // nursery2:end

        // ------------------------------------------------------------------ the batch entry

        /// <summary>
        /// Batchmode entry point. <b>Launch it without <c>-quit</c> and without
        /// <c>-nographics</c>.</b> It enters Play mode, films, and exits the Editor with 0 or 1.
        /// </summary>
        public static void Run()
        {
            // A batch Editor's clock started when the film's code first loaded (ResumeAcrossTheDomainReload); an Editor a person has open starts it here.
            NoteStart(!Application.isBatchMode);
            Stage("before Play mode: project load, to the entry point");
            LogCodeMode();

            _fromWindow = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WINDOW"));
            _fromNursery = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_NURSERY"));

            if (_fromNursery && _fromWindow)
            {
                Fail("EVOSIM_THEATRE_NURSERY and EVOSIM_THEATRE_WINDOW are both set: a film is shot from one source.");
                return;
            }

            // A nursery tank needs neither: the statement below is this if's body.
            if (!_fromNursery)
            if (!_fromWindow && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CHECKPOINT")))
            {
                Fail("neither EVOSIM_THEATRE_CHECKPOINT nor EVOSIM_THEATRE_WINDOW is set: a film is shot from a checkpoint or from a farm film window.");
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
            Stage("before Play mode: request read and scene open");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>The first tick the runner holds the world (the farm film window, the nursery tank or the continued checkpoint): one timing line.</summary>
        private static void MarkWorldRead()
        {
            if (_worldMarked) return;

            _worldMarked = true;
            Stage("reading the window (the runner's world is up)");
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

        /// <summary>
        /// Seconds of the run between two frames, which the shots' rules read (their speed ceilings,
        /// their smoothing): a frame interval at the run's pace, the speed times it in a time-lapse.
        /// </summary>
        private static float FrameSeconds() => (float)(_speed / _fps);

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
                // nursery2:begin guard=_fromNursery
                if (_fromNursery && FilmNurseryShots.Knows(name))
                {
                    if (!names.Contains(name)) names.Add(name);
                    continue;
                }
                // nursery2:end
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

            // A time-lapse: a continued world is stepped frame by frame in the Editor, so it would
            // step the speed times the physics a frame, where a window was stepped by the farm.
            text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SPEED");
            _speed = 1d;
            if (!string.IsNullOrWhiteSpace(text) &&
                (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _speed) ||
                 !(_speed >= 1d) || _speed > 3600d))
            {
                return "EVOSIM_THEATRE_FILM_SPEED: '" + text + "' is not a speed from 1 to 3600 seconds of the run a second of film.";
            }

            if (_speed != 1d && !_fromWindow)
            {
                return "EVOSIM_THEATRE_FILM_SPEED: a time-lapse is filmed from a farm film window (-FromFarm), not from a checkpoint.";
            }

            foreach (string name in names)
            {
                if (FilmCorpseShots.Knows(name) && !_fromWindow)
                {
                    return "EVOSIM_THEATRE_FILM_SHOTS: '" + name + "' follows a corpse, and the corpses are read from a farm film window (-FromFarm).";
                }
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
            (_closeStill ? "1" : "0") + "|" +
            (_fromWindow ? "1" : "0") + "|" +
            _speed.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// Picks the request back up on the other side of the domain reload Play mode causes, and
        /// quits with the carried code on the way out, as the snapshot does.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ResumeAcrossTheDomainReload()
        {
            NoteStart(false); // the timers' clock starts at the first load of this code in the Editor and survives every reload after

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
            _fromWindow = f.Length > 12 && f[12] == "1";
            _fromNursery = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_NURSERY"));
            _speed = 1d;
            if (f.Length > 13) double.TryParse(f[13], NumberStyles.Float, CultureInfo.InvariantCulture, out _speed);
            if (!(_speed >= 1d)) _speed = 1d;

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

            // EVOSIM_THEATRE_DRESS_NEAR: told to the runtime now, before Play mode, because the window's first frame is shown the
            // moment the scene starts and has to leave the skins unbuilt. False for any film the request alone rules out.
            TheatreDressSet.Wanted = TheatreDressSet.NearOn && DressNearReason() == null;

            // The timers' log count starts at each arming: the domain reload empties it, and the dressing all comes after.
            _playMarked = false;
            _worldMarked = false;
            _logLines = 0;
            _logKinds.Clear();
            Application.logMessageReceived -= CountLog;
            Application.logMessageReceived += CountLog;

            if (_driving) return;

            _driving = true;
            EditorApplication.update += Drive;
        }

        // ------------------------------------------------------------------ the shoot

        // The session's code optimisation and when the two assemblies the film runs on were built, so a
        // film's log says whether it ran on Release code.
        private static void LogCodeMode()
        {
            string theatre = File.GetLastWriteTime(typeof(TheatreRunner).Assembly.Location)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            string core = File.GetLastWriteTime(typeof(Evosim.Core.World).Assembly.Location)
                .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            Debug.Log(
                "[Theatre] code optimization: " + UnityEditor.Compilation.CompilationPipeline.codeOptimization +
                " (Evosim.Theatre built " + theatre + ", Evosim.Core built " + core + ")");
        }

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

                if (!_playMarked)
                {
                    _playMarked = true;
                    Stage("entering Play mode (domain reload and scene start)");
                    LogCodeMode();
                }

                if (_runner == null)
                {
                    _runner = UnityEngine.Object.FindFirstObjectByType<TheatreRunner>();
                    if (_runner == null)
                    {
                        Finish(1, "no TheatreRunner in the scene: nothing is playing the world");
                        return;
                    }
                }

                if (_fromNursery)
                {
                    DriveNursery();
                    return;
                }

                if (_fromWindow)
                {
                    DriveWindow();
                    return;
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

                MarkWorldRead();

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
                // D239: the film's clock is in world seconds, so the half-step slack is half of a
                // step's world seconds (physics dt over k); at k = 1 that is the physics dt itself.
                float dt = Mathf.Max(1e-4f, live.Sim.WorldSecondsPerStep);

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

                    // The film's second, not the world's, for the snow's drift: the world's clock
                    // moves in half-second metabolic steps, and the drift read off it crept for
                    // eleven frames and jumped on the twelfth at 24 fps (clip21, 2026-10-07).
                    shot.Camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, label,
                        Path.Combine(shot.Directory, frameName), at);
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

                if (_next == 1) Stage("first frame");

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
        private static void WarmUp(IFilmWorld live)
        {
            LiveWorldView view = _fromWindow ? _runner.Window?.View : _runner.LiveView;
            if (_fromNursery) view = _runner.Nursery?.View;
            if (view == null)
            {
                Finish(1, "no live view in the scene: nothing draws the world");
                return;
            }

            if (!view.Fed) view.Sync();
            view.DressUndressed();

            foreach (Shot shot in _shots)
            {
                shot.Pose(live, view, 0f, FrameSeconds(), out Vector3 eye, out Quaternion rotation, out float focus);
                shot.Camera.CapturePlaced(live, eye, rotation, shot.FieldOfView, shot.Portrait, focus, "", null);
            }

            _warmFrames++;

            int undressed = view.UndressedCount;
            int plain = view.PlainRendererCount();

            if (_warmFrames >= LeastWarmUpFrames && undressed == 0 && plain == 0)
            {
                foreach (Shot shot in _shots) shot.ResetTally();
                _warmed = true;

                // EVOSIM_THEATRE_DRESS_NEAR: the gate looked at the near set alone; say how many bodies it passed over.
                string passedOver = "";

                if (view.NearActive)
                {
                    view.NearCounts(out int inSet, out _, out _);
                    passedOver = string.Format(CultureInfo.InvariantCulture,
                        "; dress near: the gate passed over {0} bodies outside the set", view.BodyCount - inSet);
                }

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: warm-up {0} frames, every body dressed ({1} bodies drawn, {2} parts, " +
                    "no renderer on the plain material{3})", _warmFrames, view.BodyCount, view.PartCount, passedOver));
                Stage("warm-up and dressing of every body (" + _warmFrames.ToString(CultureInfo.InvariantCulture) + " frames)");
                DressLine("after the warm-up");
                return;
            }

            if (_warmFrames >= MostWarmUpFrames)
            {
                Finish(1, string.Format(CultureInfo.InvariantCulture,
                    "warm-up {0} frames and the skin has not dressed the crowd: {1} bodies undressed, " +
                    "{2} renderers on the plain material", _warmFrames, undressed, plain));
            }
        }

        /// <summary>
        /// The second a farm-window film was asked to start at (<c>EVOSIM_THEATRE_SEEK</c>, read as the runner reads it, a float), or false
        /// where none was asked.
        /// </summary>
        private static bool AskedSecond(out float asked)
        {
            asked = 0f;
            string seek = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SEEK");

            return !string.IsNullOrEmpty(seek) &&
                   float.TryParse(seek, NumberStyles.Float, CultureInfo.InvariantCulture, out asked) && asked > 0f;
        }

        /// <summary>Stops the runner's own clock, plans the shots, and clears their directories.</summary>
        private static void SetUp(IFilmWorld live)
        {
            _setUp = true;
            FilmWindowWorld window = live as FilmWindowWorld;
            TheatreDynamicsReplay stepped = live as TheatreDynamicsReplay;
            LiveWorldView liveView = window != null ? window.View : _runner.LiveView;
            if (_fromNursery) liveView = _runner.Nursery?.View;

            // The lineage's hue, as the safari paints it (SafariDirector, the owner's leaf ruling of 2026-09-24; the owner, 2026-11-03:
            // whatever the safari does, the films do too): every body by its clade, from the run's lineage.jsonl and the window's own
            // birth rows, recorded to the window's restore (its last frame when faithful), as SafariDirector restores its clades.
            if (window != null && liveView?.Palette != null && liveView.Palette.LineageOf == null)
            {
                string run = window.Record != null ? window.Record.Path : null;
                if (!string.IsNullOrEmpty(run))
                {
                    SafariClades clades = SafariClades.Read(run);
                    var verdict = window.Window.Verdict;
                    double recorded = window.Faithful ? window.LastSecond
                        : verdict != null && !double.IsNaN(verdict.RestoredAt) ? verdict.RestoredAt : 0d;
                    clades.Restored(recorded);
                    liveView.Palette.LineageOf = id => clades.CladeOf(id, body =>
                    {
                        bool known = window.TryLineageOf(body, out long parent, out byte flags);
                        return (known, parent, flags);
                    });
                    liveView.Redress();
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] film: the lineage's hue as the safari paints it, {0:0.###} of the wheel (EVOSIM_THEATRE_LINEAGE_HUE); lineage {1}, recorded to {2:0.###} s",
                        liveView.Palette.LineageHue, clades.Note, recorded));
                }
                else Debug.LogWarning("[Theatre] film: no run directory for the window, so no lineage's hue (the safari paints one)");
            }
            // A film from a checkpoint: the same hue from the live world, as SafariDirector reads a live cousin (its clades restored
            // at the film's start, each body by its living organism and the run's recorded births).
            else if (stepped != null && !_fromNursery && liveView?.Palette != null && liveView.Palette.LineageOf == null &&
                     !string.IsNullOrEmpty(_runner.RunDirectory))
            {
                SafariClades clades = SafariClades.Read(RunRecord.ResolveRunDirectory(_runner.RunDirectory) ?? _runner.RunDirectory);
                clades.Restored(live.Second);
                Dictionary<long, Organism> living = null;
                liveView.Palette.LineageOf = id =>
                {
                    World world = stepped.Sim?.World;
                    if (world == null) return -1;
                    if (living == null || living.Count != world.Living.Count || !living.ContainsKey(id))
                    {
                        living = new Dictionary<long, Organism>(world.Living.Count);
                        foreach (Organism o in world.Living) living[o.Id] = o;
                    }
                    return living.TryGetValue(id, out Organism body) ? clades.CladeOf(body, living) : -1;
                };
                liveView.Redress();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: the lineage's hue as the safari paints it, {0:0.###} of the wheel (EVOSIM_THEATRE_LINEAGE_HUE); lineage {1}, from the checkpoint at {2:0.###} s",
                    liveView.Palette.LineageHue, clades.Note, live.Second));
            }

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

            if (_raw && liveView?.Palette != null)
            {
                liveView.Palette.RawShapes = true;
                liveView.Palette.Clear();
                // filmraw:begin
                // The skin's material takes its taper, bend and carve off too, as the runner's X key does (TheatreSafari's
                // EVOSIM_THEATRE_SAFARI_RAW does the same), so a nursery film draws the colliders as the physics has them.
                TheatreSkin.Current?.ShowRawShapes(true);
                Debug.Log("[Theatre] film: RAW, the colliders as the physics has them (EVOSIM_THEATRE_FILM_RAW, a diagnostic)" +
                          (_fromNursery ? ", on the nursery tank's bodies" : ""));
                // filmraw:end
            }
            // filmraw:begin
            else if (_raw) Debug.Log("[Theatre] film: RAW asked for (EVOSIM_THEATRE_FILM_RAW), but this world's view has no palette to draw it");
            // filmraw:end

            if (!_fromNursery)
            if (window == null) liveView?.Sync();

            // A farm window asked for a second (-At, EVOSIM_THEATRE_SEEK): the runner seeked there when the window opened, then kept
            // playing in real time (TheatreRunner.StepWindow, Paused false) for as long as the scene took to come up, and a film that
            // took its clock from wherever the window had reached started a few seconds late, by a different amount each run. Seek
            // again, to the frame nearest the asked second, which is the frame the first seek chose, now that Paused is set above
            // and nothing moves the window until the first film frame. Only a window with a seek: every other mode is as it was.
            if (window != null && AskedSecond(out float asked))
            {
                double reached = window.Second;
                _runner.ShowWindowSecond(asked);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: start pinned at {0:0.###} s (asked {1:0.###} s, the window had reached {2:0.###} s)",
                    window.Second, asked, reached));
            }

            _t0 = live.Second;
            _steps0 = stepped != null ? stepped.Steps : 0L;
            // D239: world seconds a step (the verdict's physicsDt is already that, the farm writes
            // it so); a stepped world's is Simulation.WorldSecondsPerStep, PhysicsDt itself at k = 1.
            if (!_fromNursery)
            _dt = stepped != null ? stepped.Sim.WorldSecondsPerStep
                : window.Window.Verdict != null && window.Window.Verdict.PhysicsDt > 0d ? window.Window.Verdict.PhysicsDt : 1d / _fps;
            else _dt = _runner.Nursery.StepSeconds;
            _frames = FrameCount();
            _next = 0;
            _worstLag = 0f;

            // A film longer than the window is cut to the window: a frame past its last would hold
            // it, and a held frame says nothing a still does not.
            if (window != null && _t0 + (double)(_frames - 1) * _speed / _fps > window.LastSecond + 0.5d * _speed / _fps)
            {
                int fits = Math.Max(1, (int)Math.Floor((window.LastSecond - _t0) * _fps / _speed + 1e-6) + 1);
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: {0} frame(s) asked for from {1:0.###} s, and the window ends at {2:0.###} s: cut to {3}",
                    _frames, _t0, window.LastSecond, fits));
                _frames = fits;
                _closeSeconds = Math.Min(_closeSeconds, (double)fits / _fps);
            }

            if (window != null)
            {
                window.Speed = _speed;

                // A window recorded at fewer frames a second than the time-lapse shows holds each of
                // its frames for several of the film's, and the clip stutters.
                double recorded = window.Window.Verdict != null ? window.Window.Verdict.Fps : double.NaN;
                if (!double.IsNaN(recorded) && recorded * _speed < _fps * 0.99d)
                {
                    Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] film: the window was recorded at {0:0.###} fps, so at {1:0.##}x each of its frames " +
                        "is held for {2:0.#} film frames; record it at {3:0.###} fps or more for a pose a frame",
                        recorded, _speed, _fps / (recorded * _speed), _fps / _speed));
                }

                if (_speed != 1d)
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] film: a time-lapse at {0:0.##}x, {1:0.#} s of the run in {2:0.#} s of film",
                        _speed, _frames * _speed / _fps, (double)_frames / _fps));
                }
            }

            if (window != null && _trace)
            {
                _trace = false;
                Debug.LogWarning("[Theatre] film: the trace reads the solver, and a film window has none: no trace.tsv");
            }

            if (_fromNursery)
            {
                NurseryEpisodeWorld tank = _runner.Nursery;

                // A film longer than the recording is cut to it, as a window's is: a frame past its last would hold it.
                if (_t0 + (double)(_frames - 1) * _speed / _fps > tank.LastSecond + 0.5d * _speed / _fps)
                {
                    int fits = Math.Max(1, (int)Math.Floor((tank.LastSecond - _t0) * _fps / _speed + 1e-6) + 1);
                    Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] film: {0} frame(s) asked for from {1:0.###} s, and the nursery tank ends at {2:0.###} s: cut to {3}",
                        _frames, _t0, tank.LastSecond, fits));
                    _frames = fits;
                    _closeSeconds = Math.Min(_closeSeconds, (double)fits / _fps);
                }

                tank.NoteFilmRate(_fps, _speed);
                Debug.Log("[Theatre] film: a nursery tank, " + tank.Describe() + " (the line below says no checkpoint; a tank has none)");
            }

            LiveCheckpoint checkpoint = stepped?.ContinuedFrom;
            string arm = live.Record.ArmName ?? "run";
            double ckptSeconds = checkpoint != null ? checkpoint.Seconds : window != null ? window.FirstSecond : _t0;

            if (_directory == null)
            {
                _directory = Path.Combine(
                    Path.Combine(Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "films"), arm),
                    (window != null ? "farm-" : "") + ckptSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            }

            var world = new WorldBounds(live);
            _shots.Clear();
            // nursery2:begin guard=_fromNursery
            _firstFrames.Clear();
            // nursery2:end

            foreach (string name in _shotNames)
            {
                bool close = name == "close";
                Shot shot;
                // nursery2:begin guard=_fromNursery
                double nurseryStart = _t0, nurseryLength = 0d;

                if (_fromNursery && FilmNurseryShots.Knows(name))
                {
                    shot = FilmNurseryShots.Plan(
                        name, _runner.Nursery, world, _t0, (double)_frames * _speed / _fps, _width / (float)_height,
                        out nurseryStart, out nurseryLength, out string nurseryRefusal);
                    if (shot == null)
                    {
                        Finish(1, "the " + name + " shot: " + nurseryRefusal);
                        return;
                    }
                }
                else
                // nursery2:end

                if (FilmCorpseShots.Knows(name))
                {
                    shot = FilmCorpseShots.Plan(name, window, world, (float)((double)_frames / _fps), _speed, out string refusal);
                    if (shot == null)
                    {
                        Finish(1, "the " + name + " shot: " + refusal);
                        return;
                    }
                }
                else
                {
                    shot = Shot.Plan(name, live, liveView, world,
                        (float)(close ? _closeSeconds : _seconds), _width / (float)_height, _turns, close && _closeStill);
                }
                shot.Frames = close ? Math.Max(1, (int)Math.Round(_closeSeconds * _fps)) : _frames;
                // nursery2:begin guard=_fromNursery
                if (_fromNursery && FilmNurseryShots.Knows(name))
                {
                    int nurseryFirst = (int)Math.Round((nurseryStart - _t0) * _fps / _speed);
                    shot.Frames = Math.Max(1, Math.Min(_frames - nurseryFirst, (int)Math.Round(nurseryLength * _fps / _speed)));
                    _firstFrames[shot] = nurseryFirst;
                }
                // nursery2:end
                shot.Directory = Path.Combine(_directory, name);

                // A camera of its own per shot, never one camera posed for each in turn: the
                // grade's motion blur reads each camera's previous frame, and a camera that
                // jumped between two shots every frame drew the jump as a smear across the
                // orbit's middle (the first smoke, 2026-09-23).
                // A portrait takes the safari's fill from the camera's side, 0.6, as a heuristic trip's portrait does (Editor
                // TheatreSafari; the owner, 2026-11-03: whatever the safari does, the films do too). A story's lamp is the story
                // look's, which D290 keeps out of films.
                shot.Camera = new SnapshotCamera(_width, _height) { FillIntensity = 0.6f };

                Directory.CreateDirectory(shot.Directory);
                foreach (string stale in Directory.GetFiles(shot.Directory, "frame-*.png")) File.Delete(stale);

                _shots.Add(shot);
            }

            var lines = new System.Text.StringBuilder();
            foreach (Shot shot in _shots) lines.Append("\n  ").Append(shot.Name).Append(": ").Append(shot.Plan_);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] film of {0}: {1}\n  t0={2:0.###} s, dt {3} s, {4} frames at {5} fps, alive {6}, {7}{8}",
                arm, window != null ? "FARM FILM WINDOW " + window.Describe() : checkpoint != null ? checkpoint.Line() : "no checkpoint: founded at t=0",
                _t0, _dt, _frames, _fps, live.BodyCount, world.Describe(), lines));

            // EVOSIM_THEATRE_DRESS_NEAR=1: only the bodies the close shot can show are given skins and paint. With the dial off
            // nothing here runs.
            bool near = false;
            if (TheatreDressSet.NearOn) near = DressNear(window);

            // EVOSIM_THEATRE_DRESS_SET_COUNT=1: how many bodies each still close shot could show, counted from the window's recorded
            // poses, one line a shot. It reads and logs and changes nothing; with the dial off nothing here runs. (With
            // EVOSIM_THEATRE_DRESS_NEAR serving the film the same sweep has just logged it.)
            if (TheatreDressSet.Enabled && !near)
            {
                foreach (Shot shot in _shots)
                {
                    TheatreDressSet.Log(window, shot, _t0, _speed, _fps, _freeze, _width / (float)_height);
                }
            }

            Stage("set up (shots planned, frame directories cleared)");
        }

        /// <summary>
        /// Why <c>EVOSIM_THEATRE_DRESS_NEAR</c> cannot serve this film, or null where it can: a farm film window, every planned shot a
        /// close shot with a still eye, and the palette's shapes as the first frame found them. Read from the request alone, so
        /// the film can decide before Play mode starts (<see cref="Arm"/>); the still eye itself is known only once the shots are
        /// planned (<see cref="DressNear"/>).
        /// </summary>
        private static string DressNearReason()
        {
            if (!_fromWindow) return "not a farm film window";
            if (_fromNursery) return "a nursery tank";
            if (_raw) return "a raw film changes the palette's shapes after the first frame";
            if (!_closeStill) return "the close shot follows its subject";

            foreach (string name in _shotNames)
            {
                if (name != "close") return "the film plans a " + name + " shot, and only close shots are served";
            }

            return null;
        }

        /// <summary>
        /// EVOSIM_THEATRE_DRESS_NEAR, after the shots are planned: sweeps the bodies every close shot can show, builds their
        /// pending one skins and puts the set in force, so the warm-up and every frame dress it alone. A film the dial cannot
        /// serve says why in one line, gets its skins built as they would have been, and is otherwise as it was. True when the
        /// set is in force.
        /// </summary>
        private static bool DressNear(FilmWindowWorld window)
        {
            string reason = DressNearReason();
            LiveWorldView view = window != null ? window.View : null;

            if (reason == null && (view == null || !view.Deferring)) reason = "the window's view was not asked to defer its skins";

            HashSet<long> set = null;

            if (reason == null)
            {
                set = new HashSet<long>();

                foreach (Shot shot in _shots)
                {
                    HashSet<long> one = TheatreDressSet.Set(
                        window, shot, _t0, _speed, _fps, _freeze, _width / (float)_height, TheatreDressSet.Enabled, out string refusal);

                    if (one == null)
                    {
                        reason = "the " + shot.Name + " shot: " + refusal;
                        break;
                    }

                    set.UnionWith(one);
                }
            }

            if (reason != null)
            {
                Debug.Log("[Theatre] dress near: off for this film (" + reason + ")");
                if (view != null && view.Deferring) view.EndDeferral();
                return false;
            }

            view.ApplySet(set);
            view.NearCounts(out int inSet, out int built, out int pending);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] dress near: dressed {0} of {1} bodies; one skins built {2}, pending {3}",
                inSet, view.BodyCount, built, pending));
            return true;
        }

        /// <summary>
        /// One tick of a film from a farm film window: the window's frame nearest the film frame's
        /// second shown through the runner, each shot posed on it and captured. Nothing is stepped.
        /// </summary>
        private static void DriveWindow()
        {
            FilmWindowWorld window = _runner.Window;

            if (window == null)
            {
                if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                return;
            }

            MarkWorldRead();

            if (!_setUp)
            {
                SetUp(window);
                return;
            }

            if (!_warmed)
            {
                WarmUp(window);
                return;
            }

            double target = _t0 + (double)_next * _speed / _fps;
            if (!_freeze)
            {
                int frame = window.Window.NearestFrame(target);
                if (frame >= 0 && frame != window.FrameIndex) _runner.ShowWindowFrame(frame);
            }

            _dressedLate += window.DressAll();

            float clock = Time.time;
            if (_next > 0 && Mathf.Abs(clock - _lastClock - 1f / _fps) > 0.25f / _fps) _clockSlips++;
            if (_next == 0) _firstClock = clock;
            _lastClock = clock;

            double at = window.Second;
            if (!_freeze) _worstLag = Mathf.Max(_worstLag, (float)Math.Abs(at - target));
            if (_next == 0) _firstAt = at;
            _lastAt = at;

            string label = string.Format(CultureInfo.InvariantCulture, "{0}  t={1:0.0} s{2}{3}", window.ProvenanceWord, at,
                _freeze ? "  FROZEN" : "",
                _speed != 1d ? "  " + _speed.ToString("0.##", CultureInfo.InvariantCulture) + "x TIME-LAPSE" : "");

            // D279's birth caption (EVOSIM_THEATRE_BIRTH_CLOUD with EVOSIM_THEATRE_FILM_SUBJECT naming one newborn of the window), from
            // the newborn's first drawn frame. With the dial off nothing is looked up, and the label is as it was.
            if (TheatreBirthCloud.On && TheatreBirthCloud.TryCaption(window, out string born)) label += born;

            // The death caption (EVOSIM_THEATRE_DEATH_CAPTION with EVOSIM_THEATRE_FILM_SUBJECT naming one body that dies in the window),
            // from the death row's second. With the dial off nothing is read, and the label is as it was.
            if (TheatreDeathCaption.On && TheatreDeathCaption.TryCaption(window, out string died)) label += died;

            string frameName = "frame-" + _next.ToString("000000", CultureInfo.InvariantCulture) + ".png";

            // The bite pulse placed (EVOSIM_THEATRE_MOUTH2_BITE with EVOSIM_THEATRE_FILM_SUBJECT): one line a frame per subject mouth, for
            // the close shot (the one the subject frames), else the first. Nothing is looked for with either dial off.
            Shot biteShot = null;

            if (TheatreMouthBite.PlaceLog)
            {
                foreach (Shot candidate in _shots) if (candidate.Name == "close") biteShot = candidate;
                if (biteShot == null && _shots.Count > 0) biteShot = _shots[0];
            }

            foreach (Shot shot in _shots)
            {
                if (_next >= shot.Frames) continue;
                float u = shot.Frames > 1 ? (float)_next / (shot.Frames - 1) : 0f;

                shot.Pose(window, window.View, u, FrameSeconds(), out Vector3 eye, out Quaternion rotation, out float focus);

                // The preview id tag (EVOSIM_THEATRE_ID_TAG, off at 0): the close subject's id beside it, burnt into this frame. With the
                // dial off nothing here runs and the camera's hook is never set.
                if (IdTag.On && shot.Name == "close") IdTag.Place(shot, window, eye, rotation, _width, _height);

                shot.Camera.CapturePlaced(window, eye, rotation, shot.FieldOfView, shot.Portrait, focus, label,
                    Path.Combine(shot.Directory, frameName));
                shot.RenderMs.Add(shot.Camera.LastRenderMs);

                // EVOSIM_THEATRE_DRESS_NEAR_AUDIT=1, a diagnostic: the bodies outside the near set that this frame's camera could see.
                if (window.View.NearActive && Environment.GetEnvironmentVariable("EVOSIM_THEATRE_DRESS_NEAR_AUDIT") == "1")
                {
                    var hits = new List<long>();
                    window.View.OutsideSetInView(eye, rotation, shot.FieldOfView, _width / (float)_height,
                        2f * SnapshotCamera.BoxOf(window, out _).size.magnitude + 200f, hits);
                    if (hits.Count > 0)
                    {
                        Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Theatre] dress near audit: frame {0}: {1} outside the set in view: {2}",
                            _next, hits.Count, string.Join(" ", hits.GetRange(0, Math.Min(12, hits.Count)))));
                    }
                }

                // EVOSIM_THEATRE_DRESS_NEAR_PICK="x,y[,from,to]", a diagnostic: every renderer of the window's three views whose bounds
                // cover that pixel of the PNG (x from the left, y from the top), at film frames from..to, dial on or off.
                string pick = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_DRESS_NEAR_PICK");
                if (!string.IsNullOrWhiteSpace(pick))
                {
                    string[] p = pick.Split(',');
                    int from = p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 0;
                    int to = p.Length > 3 ? int.Parse(p[3], CultureInfo.InvariantCulture) : int.MaxValue;
                    if (_next >= from && _next <= to)
                    {
                        float px = float.Parse(p[0], CultureInfo.InvariantCulture) + 0.5f;
                        float py = _height - float.Parse(p[1], CultureInfo.InvariantCulture) - 0.5f;
                        Matrix4x4 viewMatrix = Matrix4x4.TRS(eye, rotation, new Vector3(1f, 1f, -1f)).inverse;
                        Matrix4x4 lens = Matrix4x4.Perspective(Mathf.Clamp(shot.FieldOfView, 5f, 120f), _width / (float)_height, 0.05f,
                            2f * SnapshotCamera.BoxOf(window, out _).size.magnitude + 200f);
                        var picked = new List<string>();
                        window.View.PickAt(lens * viewMatrix, px, py, _width, _height, "body", picked);
                        window.CorpseView?.PickAt(lens * viewMatrix, px, py, _width, _height, "corpse", picked);
                        window.RemainsView?.PickAt(lens * viewMatrix, px, py, _width, _height, "remains", picked);
                        Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Theatre] dress near pick: frame {0} pixel {1}: {2}",
                            _next, pick, picked.Count == 0 ? "nothing" : string.Join("; ", picked)));
                    }
                }

                // After the capture: the bite timers are written in it (TheatreSkin.PushFrame), and the line reads the same ones.
                if (shot == biteShot) TheatreMouthBite.LogPlaced(window, _next, eye, rotation, shot.FieldOfView, _width, _height);

                if (_next == 0 || _next == shot.Frames / 2 || _next == shot.Frames - 1)
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
                    "[Theatre] film: {0} of {1} frames, t={2:0.0} s, drawn {3}, {4}",
                    _next, _frames, at, window.BodyCount, window.ProvenanceWord));
            }

            if (_next == 1) Stage("first frame");

            if (_next >= _frames) Finish(0, _next + " frame(s) per shot written from the farm film window " + window.Window.Directory);
        }

        /// <summary>
        /// One tick of a film from a nursery tank: the pose at the film frame's second shown through the runner (a
        /// recorded frame exactly when the second lands on one, two blended otherwise), each shot posed on it and
        /// captured. Nothing is stepped.
        /// </summary>
        private static void DriveNursery()
        {
            NurseryEpisodeWorld nursery = _runner.Nursery;

            if (nursery == null)
            {
                if (!string.IsNullOrEmpty(_runner.Error)) Finish(1, "refused: " + _runner.Error);
                return;
            }

            MarkWorldRead();

            if (!_setUp)
            {
                SetUp(nursery);
                return;
            }

            if (!_warmed)
            {
                WarmUp(nursery);
                return;
            }

            double target = _t0 + (double)_next * _speed / _fps;
            if (!_freeze) _runner.ShowNurserySecond(target);

            _dressedLate += nursery.DressAll();

            float clock = Time.time;
            if (_next > 0 && Mathf.Abs(clock - _lastClock - 1f / _fps) > 0.25f / _fps) _clockSlips++;
            if (_next == 0) _firstClock = clock;
            _lastClock = clock;

            double at = nursery.Second;
            if (!_freeze) _worstLag = Mathf.Max(_worstLag, (float)Math.Abs(at - target));
            if (_next == 0) _firstAt = at;
            _lastAt = at;

            string label = string.Format(CultureInfo.InvariantCulture, "{0}  t={1:0.0} s{2}", nursery.ProvenanceLine, at, _freeze ? "  FROZEN" : "");
            string frameName = "frame-" + _next.ToString("000000", CultureInfo.InvariantCulture) + ".png";

            foreach (Shot shot in _shots)
            {
                // A shot has its own first film frame (the nose pass starts after the follow) and numbers its frames from 0.
                int shotFirst = _firstFrames.TryGetValue(shot, out int firstAt) ? firstAt : 0;
                int local = _next - shotFirst;
                if (local < 0 || local >= shot.Frames) continue;

                float u = shot.Frames > 1 ? (float)local / (shot.Frames - 1) : 0f;
                string shotFrameName = "frame-" + local.ToString("000000", CultureInfo.InvariantCulture) + ".png";

                shot.Pose(nursery, nursery.View, u, FrameSeconds(), out Vector3 eye, out Quaternion rotation, out float focus);
                shot.Camera.CapturePlaced(nursery, eye, rotation, shot.FieldOfView, shot.Portrait, focus, label,
                    Path.Combine(shot.Directory, shotFrameName));
                shot.RenderMs.Add(shot.Camera.LastRenderMs);

                if (local == 0 || local == shot.Frames / 2 || local == shot.Frames - 1)
                {
                    shot.Camera.LastPictureSpread(out float mean, out float spread);
                    shot.Spreads.Add(string.Format(CultureInfo.InvariantCulture,
                        "frame {0}: luminance mean {1:0.0}, sd {2:0.0}{3}", local, mean, spread,
                        spread < 2f ? " (UNIFORM: the device may have rendered nothing)" : ""));
                }
            }

            _next++;

            if (_next % Math.Max(1, _fps * 5) == 0)
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] film: {0} of {1} frames, t={2:0.0} s, drawn {3}, {4}",
                    _next, _frames, at, nursery.BodyCount, nursery.ProvenanceWord));
            }

            if (_next == 1) Stage("first frame");

            if (_next >= _frames) Finish(0, _next + " frame(s) per shot written from the nursery tank " + nursery.TankDirectory);
        }

        private static void Finish(int code, string verdict)
        {
            Stage("all frames (first frame to the end of the film)");

            if (_traceWriter != null) { _traceWriter.Dispose(); _traceWriter = null; }
            EditorApplication.update -= Drive;
            _driving = false;
            TheatreDressSet.Wanted = false; // EVOSIM_THEATRE_DRESS_NEAR: a later window in this session is not this film's
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

            Stage("frame writer flush and report");
            DressLine("over the whole film");
            Application.logMessageReceived -= CountLog;

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
            Stage("shutdown (leave Play mode, reload) up to exit");

            if (!Application.isBatchMode)
            {
                Debug.Log("[Theatre] film finished with code " + code + ".");
                return;
            }

            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ the preview id tag

        /// <summary>
        /// The preview id tag (<c>EVOSIM_THEATRE_ID_TAG</c>, 0 off by default, 1 on): the close shot's subject's id, a small white number
        /// with a thin dark outline, beside the subject in the frame, so a preview can say which body is which. For previews only, never
        /// the film look.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Off by default, and then nothing exists.</b> The film asks for it only with the dial on; with it off no texture, material or
        /// log line is made, no camera's <see cref="SnapshotCamera.OverRender"/> is set, and every frame is the one drawn without it.
        /// </para>
        /// <para>
        /// <b>Where.</b> At a fixed offset in pixels above the subject's drawn centre projected through the shot's camera, raised further
        /// where that would put it inside the projection of the body's drawn box, so it sits clear of the body. The subject is read where
        /// the frame draws it: alive in the window's view, else its remains in the remains view (as the close shot reads its subject
        /// through <c>_subjectView</c> and <see cref="FilmWindowWorld.TryRemains"/>), else its corpse. A frame that draws none of them, or
        /// whose camera has the subject behind it, carries no tag.
        /// </para>
        /// <para>
        /// <b>Burnt in as the label's font is</b> (<see cref="SnapshotCamera"/>'s five by seven glyphs, of which an id needs the ten digits):
        /// the tag is stamped into a texture of the frame's size, and composited into the camera's supersampled target before the box
        /// filter through the safari chart's shader (<c>Hidden/Evosim/Theatre Chart Over</c>, with no plate and no dim), by the camera's
        /// <see cref="SnapshotCamera.OverRender"/> hook, the one a story's chart uses. The texture is point-filtered at the frame's own
        /// size, so each of its pixels covers one block of the supersample and comes back from the box filter as itself.
        /// </para>
        /// </remarks>
        private static class IdTag
        {
            /// <summary>The dial, read once.</summary>
            public static readonly bool On = ReadOn();

            private static bool ReadOn()
            {
                string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_ID_TAG");
                return !string.IsNullOrWhiteSpace(text) &&
                       float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value > 0f;
            }

            /// <summary>The tag's height above the subject's projected centre, and its least gap above the body's projected box, in glyph cells.</summary>
            private const int RiseCells = 8;

            private const int GapCells = 3;

            /// <summary>The share of the frame's height kept clear at its top for the label's bar.</summary>
            private const float LabelClear = 0.07f;

            /// <summary>The outline's opacity, premultiplied black: a thin dark edge one pixel wide about every inked pixel.</summary>
            private const byte OutlineAlpha = 210;

            /// <summary>The ten digits of the label's font (SnapshotCamera's glyphs), seven rows of five, top row first.</summary>
            private static readonly string[] Digits =
            {
                ".###.#...##..###.#.###..##...#.###.",
                "..#...##....#....#....#....#...###.",
                ".###.#...#....#...#...#...#...#####",
                "#####...#...#.....#.....##...#.###.",
                "...#...##..#.#.#..#.#####...#....#.",
                "######....####.....#....##...#.###.",
                "..##..#...#....####.#...##...#.###.",
                "#####....#...#...#...#....#....#...",
                ".###.#...##...#.###.#...##...#.###.",
                ".###.#...##...#.####....#...#..##..",
            };

            private static Texture2D _texture;
            private static Color32[] _pixels;
            private static Material _material;
            private static bool _noShader;
            private static bool _told;
            private static int _width, _height;

            /// <summary>The rectangle the last tag was stamped in (pixels, from the bottom left), cleared before the next; empty for none.</summary>
            private static RectInt _stamped;

            /// <summary>Whether the texture holds a tag for the frame about to be captured.</summary>
            private static bool _shown;

            /// <summary>
            /// Places the tag for one frame of the close shot, before its capture: finds the subject as the frame draws it, projects it
            /// through the camera, stamps the tag into the texture and arms the camera's hook. A frame with no subject to tag carries none.
            /// </summary>
            public static void Place(Shot shot, FilmWindowWorld window, Vector3 eye, Quaternion rotation, int width, int height)
            {
                if (!On || shot == null || shot.Camera == null || window == null || width <= 0 || height <= 0) return;
                if (!Ensure(width, height)) return;

                if (shot.Camera.OverRender == null) shot.Camera.OverRender = Composite;

                Clear();

                long id = shot.Subject;
                if (id < 0L) return;

                // The subject as the frame draws it: alive, else its remains, else its corpse (a corpse's key in its view is its own).
                bool found = DrawnBounds(window.View, id, out Bounds box);
                if (!found && window.RemainsView != null) found = DrawnBounds(window.RemainsView, id, out box);
                if (!found && window.CorpseView != null) found = DrawnBounds(window.CorpseView, -(id + 1L), out box);
                if (!found) return;

                // The camera as CapturePlaced sets it, as the film's pick diagnostic builds it.
                Matrix4x4 view = Matrix4x4.TRS(eye, rotation, new Vector3(1f, 1f, -1f)).inverse;
                Matrix4x4 lens = Matrix4x4.Perspective(Mathf.Clamp(shot.FieldOfView, 5f, 120f), width / (float)height, 0.05f, 100000f);
                Matrix4x4 clip = lens * view;

                if (!Project(clip, box.center, width, height, out Vector2 centre)) return;

                // The top of the body's box on screen, from the corners in front of the camera.
                float top = centre.y;
                Vector3 c = box.center, e = box.extents;

                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3(c.x + ((k & 1) != 0 ? e.x : -e.x), c.y + ((k & 2) != 0 ? e.y : -e.y), c.z + ((k & 4) != 0 ? e.z : -e.z));
                    if (Project(clip, corner, width, height, out Vector2 at)) top = Mathf.Max(top, at.y);
                }

                string text = id.ToString(CultureInfo.InvariantCulture);
                int cell = Mathf.Max(2, height / 540);
                int textWidth = text.Length * 6 * cell - cell;
                int textHeight = 7 * cell;

                float bottom = Mathf.Max(centre.y + RiseCells * cell, top + GapCells * cell);
                int x0 = Mathf.RoundToInt(centre.x - 0.5f * textWidth);
                int y0 = Mathf.RoundToInt(bottom);

                x0 = Mathf.Clamp(x0, 2, Mathf.Max(2, width - textWidth - 2));
                y0 = Mathf.Clamp(y0, 2, Mathf.Max(2, height - textHeight - 2 - Mathf.RoundToInt(LabelClear * height)));

                Stamp(text, x0, y0, cell);

                if (!_told)
                {
                    _told = true;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] id tag: body {0} tagged beside the close shot's subject, {1} px glyph cells", id, cell));
                }
            }

            /// <summary>A world point through the camera to pixels from the bottom left; false behind the camera or not finite.</summary>
            private static bool Project(Matrix4x4 clip, Vector3 point, int width, int height, out Vector2 pixel)
            {
                pixel = default;
                Vector4 p = clip * new Vector4(point.x, point.y, point.z, 1f);
                if (!(p.w > 1e-4f)) return false;

                pixel = new Vector2((p.x / p.w * 0.5f + 0.5f) * width, (p.y / p.w * 0.5f + 0.5f) * height);
                return !float.IsNaN(pixel.x) && !float.IsNaN(pixel.y) && !float.IsInfinity(pixel.x) && !float.IsInfinity(pixel.y);
            }

            /// <summary>The texture, its pixels and the composite's material, made once at the frame's size. False when the shader is missing, said once.</summary>
            private static bool Ensure(int width, int height)
            {
                if (_noShader) return false;

                if (_material == null)
                {
                    Shader shader = Shader.Find("Hidden/Evosim/Theatre Chart Over");

                    if (shader == null)
                    {
                        _noShader = true;
                        Debug.LogWarning("[Theatre] id tag: shader 'Hidden/Evosim/Theatre Chart Over' not found, so no tag is drawn");
                        return false;
                    }

                    _material = new Material(shader) { name = "Theatre Id Tag", hideFlags = HideFlags.HideAndDontSave };
                }

                if (_texture == null || _width != width || _height != height)
                {
                    if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture);

                    _width = width;
                    _height = height;
                    _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                    {
                        name = "Theatre Id Tag",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave,
                    };

                    _pixels = new Color32[width * height];
                    _texture.SetPixels32(_pixels);
                    _texture.Apply(false);
                    _stamped = new RectInt(0, 0, 0, 0);
                    _shown = false;
                }

                return true;
            }

            /// <summary>Clears the last tag's rectangle to nothing.</summary>
            private static void Clear()
            {
                _shown = false;
                if (_stamped.width <= 0 || _stamped.height <= 0) return;

                var none = new Color32(0, 0, 0, 0);

                for (int y = Mathf.Max(0, _stamped.yMin); y < Mathf.Min(_height, _stamped.yMax); y++)
                {
                    for (int x = Mathf.Max(0, _stamped.xMin); x < Mathf.Min(_width, _stamped.xMax); x++) _pixels[y * _width + x] = none;
                }

                _texture.SetPixels32(_pixels);
                _texture.Apply(false);
                _stamped = new RectInt(0, 0, 0, 0);
            }

            /// <summary>Stamps the digits with their bottom left at (x0, y0): the outline first, one pixel about every inked pixel, then the ink.</summary>
            private static void Stamp(string text, int x0, int y0, int cell)
            {
                var ink = new Color32(255, 255, 255, 255);
                var edge = new Color32(0, 0, 0, OutlineAlpha);
                int top = y0 + 7 * cell - 1;

                for (int pass = 0; pass < 2; pass++)
                {
                    int x = x0;

                    foreach (char ch in text)
                    {
                        int digit = ch - '0';
                        if (digit < 0 || digit > 9) { x += 6 * cell; continue; }

                        string rows = Digits[digit];

                        for (int r = 0; r < 7; r++)
                        {
                            for (int column = 0; column < 5; column++)
                            {
                                if (rows[r * 5 + column] != '#') continue;

                                int px = x + column * cell, py = top - (r + 1) * cell + 1;

                                if (pass == 0) Fill(px - 1, py - 1, cell + 2, cell + 2, edge, true);
                                else Fill(px, py, cell, cell, ink, false);
                            }
                        }

                        x += 6 * cell;
                    }
                }

                int width = text.Length * 6 * cell - cell;
                _stamped = new RectInt(x0 - 1, y0 - 1, width + 2, 7 * cell + 2);
                _texture.SetPixels32(_pixels);
                _texture.Apply(false);
                _shown = true;
            }

            /// <summary>Fills a rectangle of the tag's pixels; the outline does not overwrite ink.</summary>
            private static void Fill(int x, int y, int w, int h, Color32 colour, bool under)
            {
                for (int py = Mathf.Max(0, y); py < Mathf.Min(_height, y + h); py++)
                {
                    for (int px = Mathf.Max(0, x); px < Mathf.Min(_width, x + w); px++)
                    {
                        int at = py * _width + px;
                        if (under && _pixels[at].r == 255) continue;
                        _pixels[at] = colour;
                    }
                }
            }

            /// <summary>The camera's hook: the tag laid over the supersampled target, under the label, which is stamped after the read-back.</summary>
            private static void Composite(RenderTexture target)
            {
                if (!_shown || _texture == null || _material == null || target == null) return;

                _material.SetFloat("_Fade", 1f);
                _material.SetFloat("_WorldDim", 0f);
                _material.SetVector("_Plate", Vector4.zero);
                _material.SetFloat("_PlateAlpha", 0f);
                _material.SetVector("_TargetSize", new Vector4(target.width, target.height, 0f, 0f));

                Graphics.Blit(_texture, target, _material);
            }
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
