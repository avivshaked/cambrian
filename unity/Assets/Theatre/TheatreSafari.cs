using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The safari in the Editor's Play mode (safari-spec.md items 7, 11 and 12): the director
    /// hosted over the runner's live world, with its panel, its keys, auto mode and the record
    /// button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Attached by the runner, and only where it can work.</b> <see cref="TheatreRunner"/>
    /// adds this component at start in an interactive Editor (never under <c>-batchmode</c>, where
    /// the film, the snapshot and the headless safari drive the runner themselves, and never with
    /// <c>EVOSIM_THEATRE_SAFARI=0</c>). It then waits for a live world and a guide beside its run
    /// (<c>runs/&lt;arm&gt;/&lt;run&gt;/guide/guide.json</c>, or <c>EVOSIM_THEATRE_SAFARI_GUIDE</c>);
    /// without a guide it says why on the console once and removes itself, so a run with no guide
    /// shows no panel.
    /// </para>
    /// <para>
    /// <b>Keys.</b> <c>.</c> next scene, <c>,</c> previous, <c>F9</c> record on or off; <c>H</c>
    /// hides the panel with the rest of the interface, and <c>Backspace</c> stops the trip. While
    /// a scene plays the fly camera is the director's: its component is switched off, and on
    /// again when the trip stops.
    /// </para>
    /// <para>
    /// <b>Recording</b> is one clip per take, started when the take starts and stopped when it
    /// ends, so the seeks between takes never reach a clip; a scene of two takes (a colony with its
    /// chapter card, the time station) is two clips, and the Time station's two are crossfaded in
    /// the encode, as the headless route does. The clips go to
    /// <c>scratch/safari/&lt;arm&gt;/&lt;date&gt;/</c> with <c>captions.tsv</c> beside them, and a
    /// <c>trip.txt</c> concat list for one file of the trip. The frame is the composite of
    /// <see cref="SafariComposite"/>, never the Game View; the interface is hidden in it unless
    /// the <c>ui</c> button says <c>ui on</c>, and the caption and the corner word are in it
    /// either way.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    public sealed class TheatreSafari : MonoBehaviour
    {
        /// <summary>The Unity Recorder, set by the Editor assembly that wraps it, or null in a player.</summary>
        public static ISafariRecorder Recorder;

        public TheatreRunner Runner;

        private static readonly SafariHeuristic[] Heuristics =
            { SafariHeuristic.Top10, SafariHeuristic.Guild, SafariHeuristic.Depth, SafariHeuristic.Age, SafariHeuristic.Firsts };

        private SafariGuide _guide;
        private SafariClades _clades;
        private SafariDirector _director;
        private SafariPanel _panel;
        private string _runDirectory;
        private string _arm;
        private bool _ready;
        private bool _gaveUp;
        private int _heuristic;
        private bool _auto;
        private float _resumeAt = -1f;

        private bool _recordArmed;
        private bool _clipOpen;
        private bool _interfaceInRecord;
        private bool _overlayWas = true;
        private SafariComposite _composite;
        private StreamWriter _captionLog;
        private StreamWriter _tripList;
        private string _recordDirectory;
        private double _takeStartSecond;
        private int _fps = 30;

        private Light _fill;
        private bool _flying = true;

        /// <summary>Adds the safari to a runner's object, in an interactive Editor only.</summary>
        public static void Attach(TheatreRunner runner)
        {
            if (runner == null || Application.isBatchMode) return;
            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI") == "0") return;
            if (runner.GetComponent<TheatreSafari>() != null) return;

            TheatreSafari safari = runner.gameObject.AddComponent<TheatreSafari>();
            safari.Runner = runner;
        }

        public SafariDirector Director => _director;

        // ---------------------------------------------------------------- opening

        private void Update()
        {
            if (_gaveUp || Runner == null) return;

            if (!_ready)
            {
                if (Runner.Live == null) return;
                Open();
                return;
            }

            ReadKeys();
        }

        private void Open()
        {
            _runDirectory = Runner.Live.Record.Path;
            _arm = Runner.Live.Record.ArmName ?? "run";

            string path = SafariGuide.Locate(_runDirectory, out string why);
            if (path == null)
            {
                Debug.Log("[Theatre] safari: off for this run: " + why);
                GiveUp();
                return;
            }

            _guide = SafariGuide.Read(path, out string refusal);
            if (_guide == null)
            {
                Debug.LogWarning("[Theatre] safari: the guide was refused: " + refusal);
                GiveUp();
                return;
            }

            _clades = SafariClades.Read(RunRecord.ResolveRunDirectory(_runDirectory) ?? _runDirectory);
            Debug.Log("[Theatre] safari: guide " + path + ", " + _guide.Clades.Count + " clades, " +
                      _guide.Picker.Count + " in the picker; lineage " + _clades.Note +
                      (_guide.Ignored.Count > 0 ? "; keys not read: " + string.Join(", ", _guide.Ignored) : ""));

            string fps = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_FPS");
            if (!string.IsNullOrWhiteSpace(fps) && int.TryParse(fps.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f >= 1 && f <= 120) _fps = f;

            string h = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_HEURISTIC");
            if (SafariTripBuilder.TryParse(h, out SafariHeuristic parsed)) _heuristic = Array.IndexOf(Heuristics, parsed);

            _panel = SafariPanel.Create(Runner.Ui);
            if (_panel != null)
            {
                _panel.HeuristicClicked += () => { _heuristic = (_heuristic + 1) % Heuristics.Length; BuildTrip(null); };
                _panel.PreviousClicked += () => Go(-1);
                _panel.NextClicked += () => Go(+1);
                _panel.AutoClicked += ToggleAuto;
                _panel.RecordClicked += ToggleRecord;
                _panel.InterfaceInRecordClicked += () => { _interfaceInRecord = !_interfaceInRecord; _panel.SetInterfaceInRecord(_interfaceInRecord); if (_recordArmed) Runner.ShowOverlay = _interfaceInRecord; };
                _panel.SceneClicked += i => { StopClip(); _director?.Begin(i); };
                _panel.Picked += name =>
                {
                    SafariClade c = _guide.FindByName(name);
                    if (c != null) BuildTrip(c);
                };
                _panel.SetPicker(_guide.Picker.Select(_guide.Find).Where(c => c != null).Select(c => c.Name).ToList());
                _panel.SetInterfaceInRecord(_interfaceInRecord);
            }

            BuildTrip(null);
            _ready = true;
        }

        private void GiveUp()
        {
            _gaveUp = true;
            enabled = false;
        }

        /// <summary>A trip by the current heuristic, or a trip of one clade.</summary>
        private void BuildTrip(SafariClade one)
        {
            StopClip();
            FlyAgain();

            var checkpoints = SafariDirector.ReadCheckpoints(RunRecord.ResolveRunDirectory(_runDirectory) ?? _runDirectory);
            var seconds = checkpoints.Select(c => c.seconds).ToList();
            double runSeconds = Runner.Live.Record.RequestedSeconds ?? (seconds.Count > 0 ? seconds[seconds.Count - 1] : 0d);

            List<SafariScene> scenes = one != null
                ? SafariTripBuilder.One(one, seconds, _guide)
                : SafariTripBuilder.Build(_guide, SafariTripBuilder.Choose(_guide, Heuristics[_heuristic]), seconds, runSeconds,
                    SafariTripBuilder.TimeOrder);

            RunConfigFacts(out float depth, out float radius);
            foreach (SafariScene s in scenes)
            {
                SafariCaptions.Fill(s, _guide, _arm, t => SafariDirector.RecordedAlive(Runner.Live?.Record, t), depth, radius);
            }

            _director = new SafariDirector(Runner, _runDirectory, _guide, _clades, scenes, new SafariOptions
            {
                Aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f,
                Interactive = true,
                MostSeekSeconds = Dial("EVOSIM_THEATRE_SAFARI_SEEK_MAX", 300d),
                MostSnapAheadSeconds = Dial("EVOSIM_THEATRE_SAFARI_SNAP_AHEAD", 600d),
            });
            _director.CaptionShown += OnCaption;
            _director.TakeStarted += OnTakeStarted;
            _director.TakeEnded += OnTakeEnded;

            _panel?.SetHeuristic(one != null ? one.Name : HeuristicWord(Heuristics[_heuristic]));
            _panel?.SetScenes(scenes);

            Debug.Log("[Theatre] safari: " + (one != null ? "a trip of one, " + one.Name : "the " + HeuristicWord(Heuristics[_heuristic]) + " trip") +
                      ", " + scenes.Count + " scenes:\n  " + string.Join("\n  ", scenes.Select(s => s.Line())));
        }

        private void RunConfigFacts(out float depth, out float radius)
        {
            Evosim.Core.RunConfig config = Runner.Live.Record.Config;
            depth = config.WorldDepthMetres;
            radius = config.SharedSpace && config.WorldShape == Evosim.Core.WorldShape.Tank
                ? Evosim.Core.TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
        }

        public static string HeuristicWord(SafariHeuristic h) =>
            h == SafariHeuristic.Top10 ? "top 10" : h.ToString().ToLowerInvariant();

        private static double Dial(string name, double fallback)
        {
            string text = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrWhiteSpace(text) &&
                   double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        // ---------------------------------------------------------------- the controls

        private void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.Period)) Go(+1);
            if (Input.GetKeyDown(KeyCode.Comma)) Go(-1);
            if (Input.GetKeyDown(KeyCode.F9)) ToggleRecord();
            if (Input.GetKeyDown(KeyCode.Backspace)) { _auto = false; _panel?.SetAuto(false); StopClip(); _director?.Stop(); FlyAgain(); }
        }

        private void Go(int step)
        {
            if (_director == null) return;
            StopClip();
            _resumeAt = -1f;
            int next = _director.Index < 0 ? 0 : _director.Index + step;
            if (next < 0) next = 0;
            if (next >= _director.Scenes.Count) { _director.Stop(); FlyAgain(); return; }
            _director.Begin(next);
        }

        private void ToggleAuto()
        {
            _auto = !_auto;
            _panel?.SetAuto(_auto);
            if (_auto && _director != null && (_director.Phase == SafariPhase.Idle || _director.Phase == SafariPhase.Done))
            {
                _director.Begin(_director.Index < 0 || _director.Phase == SafariPhase.Done ? 0 : _director.Index);
            }
        }

        // ---------------------------------------------------------------- the frame

        private void LateUpdate()
        {
            if (!_ready || _director == null) return;

            _panel?.SyncWidth(Runner.Ui?.Root);

            switch (_director.Phase)
            {
                case SafariPhase.Seeking:
                case SafariPhase.Rehearsing:
                    _director.Advance(0.25d);
                    break;

                case SafariPhase.Playing:
                {
                    bool fixedStep = _clipOpen;
                    double interval = fixedStep ? 1d / _fps : Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
                    double budget = fixedStep ? double.PositiveInfinity : 0.05d;

                    if (_director.Frame(interval, budget, out SafariPose pose)) Pose(pose);
                    break;
                }

                case SafariPhase.Parked:
                case SafariPhase.Missed:
                case SafariPhase.Failed:
                    _panel?.SetCaption(null);
                    if (_auto)
                    {
                        if (_resumeAt < 0f) _resumeAt = Time.unscaledTime + 1f;
                        else if (Time.unscaledTime >= _resumeAt)
                        {
                            _resumeAt = -1f;
                            if (_director.Index + 1 < _director.Scenes.Count) _director.Next();
                            else { _auto = false; _panel?.SetAuto(false); _director.Stop(); FlyAgain(); }
                        }
                    }
                    break;
            }

            if (_composite != null && _composite.Open) _composite.Render();

            _panel?.Mark(_director.Index, _director.Scenes);
            _panel?.SetStatus(StatusLine());
            _panel?.SetProvenance(_director.Phase == SafariPhase.Playing && !Runner.ShowOverlay ? "COUSIN" : null);
        }

        private string StatusLine()
        {
            string s = _director.Status;
            if (_recordArmed) s += Recorder == null ? " · record: the Unity Recorder is not loaded" : _clipOpen ? " · recording" : " · record armed for the next take";
            if (_auto) s += " · auto";
            return s;
        }

        private void Pose(SafariPose pose)
        {
            TheatreCamera fly = Runner.FlyCamera;
            Camera camera = Runner.ViewCamera;
            if (camera == null) return;

            if (fly != null && fly.enabled) { fly.StopFollowing(); fly.enabled = false; }
            _flying = false;

            camera.transform.SetPositionAndRotation(pose.Eye, pose.Rotation);
            camera.fieldOfView = pose.FieldOfView;
            TheatreSkin.Current?.Aim(pose.Rotation);

            TheatreGrade grade = TheatreGrade.Current;
            if (pose.Portrait && pose.Focus > 0f) grade?.FocusPortrait(pose.Focus, pose.FieldOfView);
            else grade?.Unfocus();

            Fill(pose.Portrait, pose.Rotation);
            _panel?.SetCaption(pose.Caption);
            _panel?.SetSparkline(pose.Callout, pose.Second);
        }

        /// <summary>The portrait's fill: from the camera's side, low, theatre only (item 9's light rule).</summary>
        private void Fill(bool on, Quaternion camera)
        {
            if (!on)
            {
                if (_fill != null) _fill.enabled = false;
                return;
            }

            if (_fill == null)
            {
                var holder = new GameObject("Theatre Safari Fill") { hideFlags = HideFlags.HideAndDontSave };
                _fill = holder.AddComponent<Light>();
                _fill.type = LightType.Directional;
                _fill.color = new Color(0.8f, 0.9f, 1f);
                _fill.shadows = LightShadows.None;
            }

            _fill.intensity = (float)Dial("EVOSIM_THEATRE_SAFARI_FILL", 0.6d);
            // From the camera's right and a little above, towards the subject.
            _fill.transform.rotation = camera * Quaternion.Euler(10f, -35f, 0f);
            _fill.enabled = true;
        }

        private void FlyAgain()
        {
            if (_flying) return;
            _flying = true;
            if (Runner.FlyCamera != null) Runner.FlyCamera.enabled = true;
            TheatreGrade.Current?.Unfocus();
            if (_fill != null) _fill.enabled = false;
            _panel?.SetCaption(null);
            _panel?.SetSparkline(null, 0d);
            Runner.Paused = false;
        }

        // ---------------------------------------------------------------- the record

        private void ToggleRecord()
        {
            _recordArmed = !_recordArmed;
            _panel?.SetRecording(_recordArmed);

            if (_recordArmed)
            {
                _overlayWas = Runner.ShowOverlay;
                Runner.ShowOverlay = _interfaceInRecord;

                _recordDirectory = Path.Combine(Path.Combine(Path.Combine(Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "safari"), _arm),
                    DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(_recordDirectory);
                if (Recorder == null)
                {
                    Debug.LogWarning("[Theatre] safari: record needs the Unity Recorder, which only an Editor carries, and its wrapper (Evosim.Theatre.Recorder.Editor) did not load.");
                }
                Debug.Log("[Theatre] safari: record armed: one clip per take into " + _recordDirectory +
                          ", the interface " + (_interfaceInRecord ? "shown" : "hidden") + " in the clip");
            }
            else
            {
                StopClip();
                Runner.ShowOverlay = _overlayWas;
                _captionLog?.Dispose(); _captionLog = null;
                _tripList?.Dispose(); _tripList = null;
            }
        }

        private void OnTakeStarted(SafariScene scene, int take, string plan)
        {
            _takeStartSecond = Runner.Live != null ? Runner.Live.ElapsedSeconds : 0d;
            if (!_recordArmed || Recorder == null) return;

            int w = Screen.width, h = Screen.height;
            string size = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_SIZE");
            if (!string.IsNullOrWhiteSpace(size))
            {
                string[] halves = size.Trim().Split('x', 'X');
                if (halves.Length == 2) { int.TryParse(halves[0], out w); int.TryParse(halves[1], out h); }
            }

            _composite ??= new SafariComposite();
            if (!_composite.Begin(w, h, Runner.ViewCamera, Runner.Ui?.Panel, out string note))
            {
                Debug.LogWarning("[Theatre] safari: no composite frame to record: " + note);
                return;
            }

            string file = Path.Combine(_recordDirectory, _arm + "-" + scene.Slug + "-take" + (take + 1));
            if (!Recorder.Start(_composite.Target, file, _fps, out string said))
            {
                Debug.LogWarning("[Theatre] safari: the Recorder did not start: " + said);
                _composite.Dispose();
                return;
            }

            _clipOpen = true;
            EnsureLogs();
            _tripList.WriteLine("file '" + Path.GetFileName(file) + ".mp4'");
            _tripList.Flush();
            Debug.Log("[Theatre] safari: recording " + file + " (" + note + "; " + said + ")");
        }

        private void OnTakeEnded(SafariScene scene, int take, string tally) => StopClip();

        private void StopClip()
        {
            if (!_clipOpen) return;
            _clipOpen = false;
            Recorder?.Stop(out string note);
            _composite?.Dispose();
        }

        private void OnCaption(SafariScene scene, double second, string text, double offset)
        {
            if (!_recordArmed) return;
            EnsureLogs();
            _captionLog.WriteLine(string.Join("\t", scene.Slug, second.ToString("0.###", CultureInfo.InvariantCulture),
                text, (second - _takeStartSecond).ToString("0.###", CultureInfo.InvariantCulture)));
            _captionLog.Flush();
        }

        private void EnsureLogs()
        {
            if (_captionLog == null)
            {
                string path = Path.Combine(_recordDirectory, "captions.tsv");
                bool fresh = !File.Exists(path);
                _captionLog = new StreamWriter(path, true);
                if (fresh) _captionLog.WriteLine("scene\tsecond\ttext\tclip_offset_s");
            }

            if (_tripList == null) _tripList = new StreamWriter(Path.Combine(_recordDirectory, "trip.txt"), true);
        }

        private void OnDestroy()
        {
            StopClip();
            _captionLog?.Dispose();
            _tripList?.Dispose();
            _composite?.Dispose();
            _panel?.Dispose();
            if (_fill != null) Destroy(_fill.gameObject);
        }
    }
}
