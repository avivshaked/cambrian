using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>Where the director is in a scene.</summary>
    public enum SafariPhase { Idle, Seeking, Rehearsing, Playing, Parked, Missed, Done, Failed }

    /// <summary>One frame's camera, and what is written over it.</summary>
    public struct SafariPose
    {
        public Vector3 Eye;
        public Quaternion Rotation;
        public float FieldOfView;
        public bool Portrait;
        public float Focus;
        /// <summary>The caption showing, or null.</summary>
        public string Caption;
        /// <summary>The provenance word and the second, for the corner.</summary>
        public string Label;
        public int Take;
        /// <summary>The frame's index within its take, from 0.</summary>
        public int TakeFrame;
        /// <summary>Simulated seconds since the scene's first take began.</summary>
        public double SceneOffset;
        /// <summary>The world's second.</summary>
        public double Second;
        /// <summary>The take's shot, for a check that reads its tally.</summary>
        public FilmPlans.Shot Shot;
        /// <summary>How much the picture is darkened under its captions, 0 to 1: a story's held card only.</summary>
        public float Dim;
        /// <summary>
        /// The clade the call-outs are about (its sparkline), or null: set only when
        /// <see cref="SafariOptions.Callouts"/> is on and the scene has a clade with a count series.
        /// </summary>
        public SafariClade Callout;
    }

    /// <summary>What the director may do, set by its host.</summary>
    public sealed class SafariOptions
    {
        /// <summary>
        /// A flexible scene more than this far past its checkpoint opens at the checkpoint
        /// instead, when its clade is alive there, s. Negative never moves a scene.
        /// </summary>
        public double MostSeekSeconds = 300d;

        /// <summary>
        /// A flexible scene that would cost more than <see cref="MostSeekSeconds"/> of stepping
        /// opens at the next checkpoint after its second instead, when that checkpoint is no
        /// more than this far ahead and its clade is alive there, s. Negative never moves a scene
        /// forward. <c>EVOSIM_THEATRE_SAFARI_SNAP_AHEAD</c>, default 600.
        /// </summary>
        /// <remarks>
        /// Added 2026-09-24. Round 47 seed 2's portrait at 2,240 s had no checkpoint behind it
        /// but the founding, and the next was at 2,500 s, so the safari stepped the world on
        /// screen forward 916 s, which took 20.5 minutes; opening at 2,500 s costs a restore.
        /// </remarks>
        public double MostSnapAheadSeconds = 600d;

        /// <summary>A flexible scene opens on the world already on screen when it is within this of the scene's second, s.</summary>
        public double SlackSeconds = 300d;

        /// <summary>How long a birth scene waits past the recorded founding for a birth in the parent clade, s.</summary>
        public double MostBirthWaitSeconds = 600d;

        /// <summary>The frame's aspect, for the plans' lenses.</summary>
        public float Aspect = 16f / 9f;

        /// <summary>True in the Editor's own Play mode, false in a batch Editor (the log goes to the console either way).</summary>
        public bool Interactive;

        /// <summary>
        /// The on-screen call-outs (the safari review's item 7), off by default: the subject clade
        /// in full colour and every other body grey, and the clade's count over the run as a
        /// sparkline with the filmed second marked. <c>EVOSIM_THEATRE_SAFARI_CALLOUTS=1</c>.
        /// </summary>
        public bool Callouts = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CALLOUTS") == "1";

        /// <summary>
        /// The canopy shot for the arrival and the descent (the films review's second item,
        /// 2026-09-24), off by default: the arrival rises toward the leaves and the descent sinks
        /// away from them, each 8 to 15 m under the densest column near the surface, looking up
        /// at Snell's window, in place of the look through the glass and the dolly down the
        /// shallow side. <c>EVOSIM_THEATRE_SAFARI_CANOPY=1</c>.
        /// </summary>
        public bool Canopy = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_CANOPY") == "1";
    }

    /// <summary>
    /// The safari's director (safari-spec.md items 6 to 10): plays a scene list over a live world
    /// the theatre's runner holds, seeking each scene's second from the checkpoints, planning
    /// each take against the world at that second, and handing its host one pose a frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two hosts, one director.</b> <see cref="TheatreSafari"/> hosts it in the Editor's Play
    /// mode, with the panel, the keys and the Recorder; <c>EditorTools.TheatreSafari</c> hosts it
    /// in a batch Editor that writes frames. The director owns the world's clock while a scene
    /// plays: the runner is paused and every step is the director's, one frame interval at a
    /// time, the film's rule, so a take's length in simulated seconds is its length on screen.
    /// </para>
    /// <para>
    /// <b>Seeking (item 8).</b> A scene opens at its second by one of four routes, chosen in this
    /// order and said in the log before it starts: the world already on screen, when it is within
    /// the slack of a flexible scene and the scene's clade has members in it; a restore from the
    /// checkpoint at or before the second, played forward; a step forward from the world on
    /// screen, when that is shorter than the restore; or, with no checkpoint behind the second, a
    /// replay from the founding, with the time it will take said first. Every restore is a
    /// cousin and every label says <c>COUSIN</c>. A flexible scene whose checkpoint is more than
    /// <see cref="SafariOptions.MostSeekSeconds"/> behind it opens at the checkpoint when its
    /// clade is alive there, and the log says so: this is the agent's addition to the spec,
    /// taken because the round's checkpoints are 2,500 s apart and the Editor steps a crowd of
    /// thousands at around real time. A flexible scene that would cost more than that to reach
    /// opens at the next checkpoint after it instead, when that checkpoint is within
    /// <see cref="SafariOptions.MostSnapAheadSeconds"/> and the clade is alive there
    /// (2026-09-24); where both moves are open, the nearer to the scene's own second is taken.
    /// Every seek ends with a line saying where its wall time went.
    /// </para>
    /// <para>
    /// <b>A birth is rehearsed.</b> A cousin's births are its own, so the director restores from
    /// the checkpoint before the founding and steps until a body is born to a member of the
    /// clade's parent clade (the rehearsal), notes the parent and the second, restores the same
    /// checkpoint again and plays up to the birth with the camera held on that parent. A restore
    /// in the same Editor steps the same trajectory, so the birth recurs; the caption names what
    /// was got, or that it did not recur. The rehearsal doubles the seek, and the log says so.
    /// </para>
    /// <para>
    /// <b>A story's scenes</b> (<see cref="SafariStory"/>) take the same routes. Each takes its
    /// length on screen from the scene rather than the station, and the director writes no caption
    /// of its own over a story's: no body's age on a portrait and no word on what a birth got,
    /// both of which the log says instead. A story's birth far from its clade's founding waits
    /// for a child of the clade's own members (<see cref="SafariScene.BirthLine"/>).
    /// </para>
    /// </remarks>
    public sealed class SafariDirector
    {
        private sealed class Segment
        {
            public double At;
            public bool Flexible;
            public bool Rehearse;
            /// <summary>
            /// A held card (a story's title, or its chapter card): never stepped to, filmed on the
            /// world on screen or at the checkpoint nearest its second, and needing no clade alive.
            /// </summary>
            public bool Held;
            public Func<SafariPlans.Stage, List<SafariPlans.Take>> Build;
        }

        private readonly TheatreRunner _runner;
        private readonly string _runDirectory;
        private readonly SafariGuide _guide;
        private readonly SafariClades _clades;
        private readonly List<SafariScene> _scenes;
        private readonly SafariOptions _options;
        private readonly List<(double seconds, string path)> _checkpoints;
        private readonly double _farmPace;

        private readonly Stopwatch _wall = Stopwatch.StartNew();

        private int _index = -1;
        private Queue<Segment> _segments;
        private Segment _segment;
        private List<SafariPlans.Take> _takes;
        private int _take;
        private int _takeFrame;
        private double _takeT0;
        private long _takeSteps0;
        private double _takeTarget;
        private double _sceneOffsetBase;
        private double _seekTarget = double.NaN;
        private double _seekFrom;
        private double _seekWallFrom;

        // Where a seek's wall time goes (2026-09-24): inside the world's own steps, inside the
        // seek's loop (the steps and the director's bookkeeping between them), and the rest,
        // which is the Editor's tick outside Advance. Read against the solver's own split.
        private long _seekStepTicks;
        private long _seekLoopTicks;
        private int _seekTicks;
        private int _seekCollections0;
        private long _seekPhysicsMs0, _seekEconomyMs0, _seekHarnessMs0;
        private double _restoredAt = double.NaN;
        private long _maxSeenId = -1;
        private long _birthsSeen;
        private readonly HashSet<SafariCaption> _shown = new HashSet<SafariCaption>();

        // the birth's rehearsal
        private enum Rehearsal { None, Watching, Recurring }
        private Rehearsal _rehearsal;
        private long _birthParent = -1, _birthChild = -1;
        private byte _birthChildFlags = 255;
        private double _birthAt = double.NaN;
        /// <summary>Where the rehearsal's child landed from its parent's root, or NaN when no rehearsal saw it.</summary>
        private Vector3 _birthOffset = new Vector3(float.NaN, float.NaN, float.NaN);
        private double _watchFrom;
        private bool _birthRecurred;
        /// <summary>A story's named parent, and whether it was alive where the rehearsal started.</summary>
        private long _namedParent = -1;
        private bool _namedAlive;
        /// <summary>The line's first birth, kept while the rehearsal waits for the named parent's.</summary>
        private bool _hasCandidate;
        private (long child, long parent, byte flags, Vector3 offset, double at) _candidate;

        /// <summary>How long past a story's birth second the rehearsal waits for its named parent before taking the line's first birth, s.</summary>
        public const double NamedParentWaitSeconds = 120d;
        /// <summary>Where the rehearsal started: a checkpoint's path and second, or a null path for the founding.</summary>
        private (double seconds, string path) _rehearsalFrom;

        public SafariDirector(
            TheatreRunner runner, string runDirectory, SafariGuide guide, SafariClades clades,
            List<SafariScene> scenes, SafariOptions options)
        {
            _runner = runner;
            _runDirectory = RunRecord.ResolveRunDirectory(runDirectory) ?? runDirectory;
            _guide = guide;
            _clades = clades;
            _scenes = scenes;
            _options = options ?? new SafariOptions();
            _checkpoints = ReadCheckpoints(_runDirectory);
            _farmPace = ReadFarmPace(_runDirectory);
            Phase = SafariPhase.Idle;
        }

        public IReadOnlyList<SafariScene> Scenes => _scenes;
        public int Index => _index;
        public SafariScene Current => _index >= 0 && _index < _scenes.Count ? _scenes[_index] : null;
        public SafariPhase Phase { get; private set; }
        public string Status { get; private set; } = "";
        public IReadOnlyList<(double seconds, string path)> Checkpoints => _checkpoints;

        /// <summary>The playing take's shot, or null: a host warms the renderer up on it before the first frame.</summary>
        public FilmPlans.Shot CurrentShot => Phase == SafariPhase.Playing && _takes != null && _take < _takes.Count ? _takes[_take].Shot : null;

        /// <summary>The playing take's frame index, or -1.</summary>
        public int CurrentTakeFrame => Phase == SafariPhase.Playing ? _takeFrame : -1;

        /// <summary>A line for the log; the host routes it (the console by default).</summary>
        public Action<string> Say = line => Debug.Log("[Theatre] safari: " + line);

        /// <summary>A caption put on screen: the scene, the world's second, the text, the scene offset.</summary>
        public event Action<SafariScene, double, string, double> CaptionShown;

        /// <summary>A take about to play: the scene, the take's index, its plan line.</summary>
        public event Action<SafariScene, int, string> TakeStarted;

        /// <summary>A take played through: the scene, the take's index, its tally.</summary>
        public event Action<SafariScene, int, string> TakeEnded;

        public static List<(double seconds, string path)> ReadCheckpoints(string runDirectory)
        {
            var list = new List<(double, string)>();
            string dir = Path.Combine(runDirectory ?? "", "checkpoints");
            if (!Directory.Exists(dir)) return list;

            foreach (string f in Directory.GetFiles(dir, "*.ckpt"))
            {
                if (double.TryParse(Path.GetFileNameWithoutExtension(f), NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                    list.Add((s, f));
            }

            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return list;
        }

        /// <summary>The farm's own pace for this run, simulated seconds per wall second, or NaN.</summary>
        private static double ReadFarmPace(string runDirectory)
        {
            try
            {
                string path = Path.Combine(runDirectory, "run.json");
                if (!File.Exists(path)) return double.NaN;
                JsonNode run = Json.Parse(File.ReadAllText(path));
                return run.Has("timesRealTime") && run["timesRealTime"].Kind == JsonNode.NodeKind.Number
                    ? run["timesRealTime"].AsDouble() : double.NaN;
            }
            catch
            {
                return double.NaN;
            }
        }

        // ---------------------------------------------------------------- scenes

        /// <summary>Starts a scene: works out its route, says it, and begins the seek.</summary>
        public void Begin(int index)
        {
            if (index < 0 || index >= _scenes.Count)
            {
                Phase = SafariPhase.Done;
                Status = "the trip is over";
                return;
            }

            _index = index;
            _shown.Clear();
            _sceneOffsetBase = 0d;
            _rehearsal = Rehearsal.None;
            _birthParent = _birthChild = -1;
            _birthChildFlags = 255;
            _birthAt = double.NaN;
            _birthOffset = new Vector3(float.NaN, float.NaN, float.NaN);
            _birthRecurred = false;
            _takeCount = 0;

            SafariScene scene = _scenes[index];
            _segments = new Queue<Segment>(SegmentsOf(scene));
            Say("scene " + scene.Line().Trim());
            NextSegment();
        }

        public void Next() => Begin(Math.Min(_scenes.Count, _index + 1));
        public void Previous() => Begin(Math.Max(0, _index - 1));

        /// <summary>Stops wherever it is; the world stays where it is.</summary>
        public void Stop()
        {
            Phase = SafariPhase.Idle;
            Status = "stopped";
            if (_runner != null) _runner.Paused = true;
        }

        /// <summary>
        /// A scene's length on screen when it gives one, else the station's own: the template's
        /// scenes carry the station's length, so this is the same number for them, and a story's
        /// scene carries its writer's.
        /// </summary>
        private static float LengthOf(SafariScene scene, double station) =>
            (float)(scene.Seconds > 0d ? scene.Seconds : station);

        private IEnumerable<Segment> SegmentsOf(SafariScene scene)
        {
            SafariClade clade = scene.Clade;
            int hash = clade != null ? clade.Hash : scene.Index * 7919 + 17;

            // The template's arrival, descent, floor, portrait and colony are always flexible; a
            // story's are unless its writer held one at its second. The canopy is the options'
            // unless a story's scene asks for it itself.
            bool flexible = scene.Flexible;
            bool canopy = scene.Canopy ?? _options.Canopy;

            // A story's chapter card plays first, a held take of its own before the scene's; the
            // template's colony carries its card inside its own takes (ColonyTakes).
            if (scene.FromStory && scene.ChapterCard)
            {
                yield return new Segment
                {
                    At = scene.At, Flexible = true, Held = true,
                    Build = s => One(SafariPlans.FromAbove(s, (float)SafariTripBuilder.ChapterSeconds)),
                };
            }

            switch (scene.Station)
            {
                case SafariStation.Arrival:
                    yield return new Segment
                    {
                        At = scene.At, Flexible = flexible,
                        Build = s => One(canopy
                            ? SafariPlans.Canopy(s, LengthOf(scene, SafariTripBuilder.ArrivalSeconds), FilmPlans.CanopyMove.Rise, "arrival")
                            : SafariPlans.Arrival(s, LengthOf(scene, SafariTripBuilder.ArrivalSeconds), hash)),
                    };
                    break;

                case SafariStation.Descent:
                    yield return new Segment { At = scene.At, Flexible = flexible, Build = s => DescentTakes(s, scene, hash, canopy) };
                    break;

                case SafariStation.Floor:
                    yield return new Segment { At = scene.At, Flexible = flexible, Build = s => One(SafariPlans.Floor(s, LengthOf(scene, SafariTripBuilder.FloorSeconds), hash)) };
                    break;

                case SafariStation.Card:
                    // A story's title: the chapter card's look, the disc from above, held, and
                    // darkened under its captions when the writer asked for that.
                    yield return new Segment
                    {
                        At = scene.At, Flexible = true, Held = true,
                        Build = s => One(SafariPlans.FromAbove(s, LengthOf(scene, SafariTripBuilder.ChapterSeconds))),
                    };
                    break;

                case SafariStation.Portrait:
                    yield return new Segment { At = scene.At, Flexible = flexible, Build = s => PortraitTakes(s, scene) };
                    break;

                case SafariStation.Colony:
                    yield return new Segment { At = scene.At, Flexible = flexible, Build = s => ColonyTakes(s, scene) };
                    break;

                case SafariStation.Birth:
                    yield return new Segment { At = scene.At, Flexible = false, Rehearse = true, Build = s => BirthTakes(s, scene) };
                    break;

                case SafariStation.Time:
                {
                    // The template's time scene is not flexible (its two seconds are checkpoints);
                    // a story's may move within the snap rules like any other.
                    float take = scene.Seconds > 0d ? (float)(0.5d * scene.Seconds) : (float)SafariTripBuilder.TimeTakeSeconds;
                    yield return new Segment { At = scene.At, Flexible = flexible, Build = s => One(SafariPlans.Fixed(s, take, hash, "time-a")) };
                    yield return new Segment { At = scene.SecondAt, Flexible = flexible, Build = s => One(SafariPlans.Fixed(s, take, hash, "time-b")) };
                    break;
                }
            }
        }

        private static List<SafariPlans.Take> One(SafariPlans.Take t) => new List<SafariPlans.Take> { t };

        private void NextSegment()
        {
            if (_segments == null || _segments.Count == 0)
            {
                Phase = SafariPhase.Parked;
                Status = "parked at the end of " + Current?.Slug;
                return;
            }

            _segment = _segments.Dequeue();
            Route(_segment);
        }

        // ---------------------------------------------------------------- the route

        private void Route(Segment segment)
        {
            SafariScene scene = Current;
            TheatreDynamicsReplay live = _runner.Live;
            double target = segment.At;
            double now = live != null ? live.ElapsedSeconds : double.NaN;

            (double seconds, string path) ck = CheckpointAtOrBefore(target);
            bool haveCk = ck.path != null;

            // 0. A held card (a story's title or chapter card) is never stepped to: it opens on the
            //    world on screen when that is near its second, or at the checkpoint nearest its
            //    second, restored and filmed as it opens. Round 48 seed 2's card at 13,700 s lies
            //    1,200 s past the run's last checkpoint, where the run stopped on an error.
            if (segment.Held && live != null)
            {
                (double seconds, string path) near = CheckpointNearest(target);
                if (near.path == null || Math.Abs(now - target) <= _options.SlackSeconds)
                {
                    Say(string.Format(CultureInfo.InvariantCulture,
                        "the card opens on the world on screen at {0:0.#} s ({1:+0.#;-0.#} s from its {2:0.#} s); nothing is stepped",
                        now, now - target, target));
                    if (Math.Abs(now - target) > 0.5d) Refiled(scene, segment, target, now);
                    StartSeek(now);
                    return;
                }

                Say(string.Format(CultureInfo.InvariantCulture,
                    "the card: restoring the checkpoint at {0:0.#} s, the nearest to its {1:0.#} s; nothing is stepped; a cousin from here",
                    near.seconds, target));
                Refiled(scene, segment, target, near.seconds);
                if (!Restore(near.path, near.seconds)) return;
                _rehearsalFrom = near;
                StartSeek(near.seconds);
                return;
            }

            // 1. The world on screen, for a flexible scene near enough with its clade in it.
            if (segment.Flexible && !segment.Rehearse && live != null && Math.Abs(now - target) <= _options.SlackSeconds &&
                (scene.Clade == null || MembersAlive(live, scene.Clade.Founder) > 0))
            {
                Say(string.Format(CultureInfo.InvariantCulture,
                    "opening on the world on screen at {0:0.#} s ({1:+0.#;-0.#} s from the scene's {2:0.#} s, inside the {3:0} s slack)",
                    now, now - target, target, _options.SlackSeconds));
                if (Math.Abs(now - target) > 0.5d) Refiled(scene, segment, target, now);
                StartSeek(now);
                return;
            }

            // 2. Moved to a checkpoint, for a flexible scene with its clade alive there: back to the
            //    checkpoint at or before it when that is far behind (the rule since the first
            //    safari), or forward to the next one when reaching the scene's own second would
            //    cost too much stepping and the next checkpoint is near enough ahead (2026-09-24).
            //    Either move is a restore and no stepping, so where both are open the one nearer
            //    the asked second is taken, and back wins a tie, as the older rule.
            if (segment.Flexible && _options.MostSeekSeconds >= 0d)
            {
                bool back = haveCk && target - ck.seconds > _options.MostSeekSeconds &&
                            (scene.Clade == null || scene.Clade.AliveAt(ck.seconds + 60d));

                double stay = SteppingAt(target, live != null ? now : double.NaN, ck);
                (double seconds, string path) next = CheckpointAfter(target);
                bool forward = next.path != null && _options.MostSnapAheadSeconds >= 0d &&
                               next.seconds - target <= _options.MostSnapAheadSeconds &&
                               stay > _options.MostSeekSeconds &&
                               (scene.Clade == null || scene.Clade.AliveAt(next.seconds + 60d));

                if (back && forward && next.seconds - target < target - ck.seconds) back = false;

                string alive = scene.Clade != null
                    ? scene.Clade.Name + " was alive there by the guide's dates"
                    : "the world scene has no clade to lose";

                if (back)
                {
                    Say(string.Format(CultureInfo.InvariantCulture,
                        "moved from {0:0.#} s to the checkpoint at {1:0.#} s, which saves {2:0} s of stepping; {3}",
                        target, ck.seconds, target - ck.seconds, alive));
                    Refiled(scene, segment, target, ck.seconds);
                    target = ck.seconds;
                }
                else if (forward)
                {
                    Say(string.Format(CultureInfo.InvariantCulture,
                        "moved from {0:0.#} s forward to the checkpoint at {1:0.#} s, which saves {2:0} s of stepping; {3}",
                        target, next.seconds, stay, alive));
                    Refiled(scene, segment, target, next.seconds);
                    target = next.seconds;
                    ck = next;
                    haveCk = true;
                }
            }

            // 3. A step forward from the world on screen, when it is shorter than a restore.
            if (!segment.Rehearse && live != null && now <= target + 1e-6 && (!haveCk || now >= ck.seconds - 1e-6))
            {
                Say(string.Format(CultureInfo.InvariantCulture,
                    "stepping the world on screen forward from {0:0.#} s to {1:0.#} s ({2:0} s)", now, target, target - now));
                StartSeek(target);
                return;
            }

            // 4. A restore from the checkpoint at or before the second, played forward.
            if (haveCk)
            {
                Say(string.Format(CultureInfo.InvariantCulture,
                    "restoring the checkpoint at {0:0.#} s and playing forward {1:0} s to {2:0.#} s; a cousin from here",
                    ck.seconds, target - ck.seconds, target));
                if (!Restore(ck.path, ck.seconds)) return;
                _rehearsalFrom = ck;
                StartSeek(target);
                return;
            }

            // 5. No checkpoint behind it: a replay from the founding, said with its cost first.
            string pace = double.IsNaN(_farmPace)
                ? "the run does not say how fast the farm ran it"
                : string.Format(CultureInfo.InvariantCulture,
                    "the farm ran it at {0:0.##}x real time, so at least {1} of wall here, and the Editor's Mono is slower",
                    _farmPace, Minutes(target / _farmPace));
            Say(string.Format(CultureInfo.InvariantCulture,
                "no checkpoint at or before {0:0.#} s: replaying from the founding, {0:0} s of simulation; {1}", target, pace));

            _runner.OpenFounding(_runDirectory);
            if (_runner.Live == null) { Fail("the founding would not open: " + _runner.Error); return; }
            AfterOpen(0d);
            _rehearsalFrom = (0d, null);
            StartSeek(target);
        }

        /// <summary>
        /// Moves a scene to the second it will be filmed at and writes its captions again from
        /// that second, so a caption never states a second the clip was not filmed at: round 47's
        /// first safari moved its arrival from 4,860 s to the checkpoint at 2,500 s and still
        /// captioned it at 4,860 s with 4,860 s's count (2026-09-24).
        /// </summary>
        private void Refiled(SafariScene scene, Segment segment, double asked, double filmed)
        {
            segment.At = filmed;
            // The time scene's two takes are two seconds by design, and a chapter card is not the
            // scene it opens, so neither moves the scene's own second.
            if (scene.Station == SafariStation.Time || (segment.Held && scene.Station != SafariStation.Card)) return;
            if (double.IsNaN(scene.AskedAt)) scene.AskedAt = asked;
            scene.At = filmed;
            scene.Refill?.Invoke();
            Say(string.Format(CultureInfo.InvariantCulture, "captions written again for {0:0.#} s: {1}",
                filmed, string.Join(" | ", scene.Captions.Select(c => c.Text))));
        }

        /// <summary>The checkpoint nearest a second, the earlier on a tie, or a null path when there is none.</summary>
        private (double seconds, string path) CheckpointNearest(double second)
        {
            (double, string) best = (double.NaN, null);
            double gap = double.PositiveInfinity;
            foreach (var c in _checkpoints)
            {
                double d = Math.Abs(c.seconds - second);
                if (d < gap - 1e-6) { gap = d; best = c; }
            }
            return best;
        }

        private (double seconds, string path) CheckpointAtOrBefore(double second)
        {
            (double, string) best = (double.NaN, null);
            foreach (var c in _checkpoints) if (c.seconds <= second + 1e-6) best = c;
            return best;
        }

        /// <summary>The first checkpoint after a second, or a null path when there is none.</summary>
        private (double seconds, string path) CheckpointAfter(double second)
        {
            foreach (var c in _checkpoints) if (c.seconds > second + 1e-6) return c;
            return (double.NaN, null);
        }

        /// <summary>
        /// The simulated seconds a scene at <paramref name="target"/> would be stepped through by
        /// the route that reaches it with the least: forward from the world on screen (rule 3),
        /// from the checkpoint at or before it (rule 4), or from the founding (rule 5).
        /// </summary>
        /// <param name="now">The world on screen's second, or NaN when there is none.</param>
        private static double SteppingAt(double target, double now, (double seconds, string path) ck)
        {
            if (!double.IsNaN(now) && now <= target + 1e-6 && (ck.path == null || now >= ck.seconds - 1e-6))
                return Math.Max(0d, target - now);

            return ck.path != null ? Math.Max(0d, target - ck.seconds) : Math.Max(0d, target);
        }

        private bool Restore(string path, double seconds)
        {
            _runner.OpenCheckpoint(path, seconds);
            if (_runner.Live == null)
            {
                Fail("the checkpoint at " + seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s would not open: " + _runner.Error);
                return false;
            }

            AfterOpen(_runner.Live.ElapsedSeconds);
            return true;
        }

        private void AfterOpen(double seconds)
        {
            _runner.Paused = true;
            _restoredAt = seconds;
            _clades.Restored(seconds);
            _maxSeenId = -1;
            foreach (Organism o in _runner.Live.Sim.World.Living) _maxSeenId = Math.Max(_maxSeenId, o.Id);
            _birthsSeen = _runner.Live.Sim.World.Births;
        }

        private void StartSeek(double target)
        {
            TheatreDynamicsReplay live = _runner.Live;
            _seekTarget = target;
            _seekFrom = live.ElapsedSeconds;
            _seekWallFrom = _wall.Elapsed.TotalSeconds;
            StartTheSeekClock(live);
            _runner.Paused = true;

            if (_segment.Rehearse && _rehearsal == Rehearsal.None)
            {
                // Watch from the checkpoint (or the world on screen) with the lead in hand, for up
                // to the wait past the recorded founding.
                _rehearsal = Rehearsal.Watching;
                _watchFrom = Math.Max(live.ElapsedSeconds + SafariTripBuilder.BirthLeadSeconds + 0.5d, _segment.At - 120d);
                _seekTarget = _segment.At + _options.MostBirthWaitSeconds;
                _namedParent = Current.BirthParentBody;
                _namedAlive = false;
                _hasCandidate = false;
                if (_namedParent >= 0)
                    foreach (Organism o in live.Sim.World.Living) if (o.Id == _namedParent) { _namedAlive = true; break; }
                Say(string.Format(CultureInfo.InvariantCulture,
                    "rehearsing: stepping from {0:0.#} s for a birth to a member of {1} after {2:0.#} s, until {3:0.#} s at most{4}",
                    live.ElapsedSeconds, LineName(Current), _watchFrom, _seekTarget,
                    _namedParent < 0 ? ""
                        : _namedAlive
                            ? string.Format(CultureInfo.InvariantCulture,
                                "; body {0}, the story's parent, is alive here, so its child is taken first, and the line's first birth only if it has none by {1:0.#} s",
                                _namedParent, _segment.At + NamedParentWaitSeconds)
                            : "; body " + _namedParent + ", the story's parent, is not alive here, so the line's first birth is taken"));
                Phase = SafariPhase.Rehearsing;
                return;
            }

            Phase = SafariPhase.Seeking;
            Status = string.Format(CultureInfo.InvariantCulture, "seeking {0:0.#} s", target);
        }

        // ---------------------------------------------------------------- the seek

        /// <summary>
        /// Carries a seek or a rehearsal forward for at most the wall budget, and plans the take
        /// when the world is at its second. Call it every tick while <see cref="Phase"/> is
        /// seeking or rehearsing.
        /// </summary>
        public void Advance(double wallBudgetSeconds)
        {
            if (Phase != SafariPhase.Seeking && Phase != SafariPhase.Rehearsing) return;

            TheatreDynamicsReplay live = _runner.Live;
            if (live == null) { Fail("the world went away mid-seek: " + _runner.Error); return; }

            double deadline = _wall.Elapsed.TotalSeconds + Math.Max(0.01d, wallBudgetSeconds);
            float dt = Mathf.Max(1e-4f, live.Sim.PhysicsDt);

            // The world's own steps are timed one by one and the loop as a whole, so the seek's
            // last line can say how much of its wall time was the world and how much was not.
            long entered = Stopwatch.GetTimestamp();
            _seekTicks++;

            while (live.ElapsedSeconds + 0.5d * dt < _seekTarget && _wall.Elapsed.TotalSeconds < deadline)
            {
                long stepped = Stopwatch.GetTimestamp();
                bool metabolic = live.Step();
                _seekStepTicks += Stopwatch.GetTimestamp() - stepped;

                if (metabolic && Phase == SafariPhase.Rehearsing && _hasCandidate && live.ElapsedSeconds > _segment.At + NamedParentWaitSeconds)
                {
                    _seekLoopTicks += Stopwatch.GetTimestamp() - entered;
                    TakeTheCandidate("by " + (_segment.At + NamedParentWaitSeconds).ToString("0.#", CultureInfo.InvariantCulture) + " s");
                    return;
                }

                if (metabolic && Births(live, out long child, out long parent) && Phase == SafariPhase.Rehearsing)
                {
                    if (child >= 0)
                    {
                        _seekLoopTicks += Stopwatch.GetTimestamp() - entered;
                        _birthAt = live.ElapsedSeconds;
                        _birthParent = parent;
                        _birthChild = child;
                        Say(string.Format(CultureInfo.InvariantCulture,
                            "the rehearsal got a birth at {0:0.#} s: body {1} to body {2}, a member of {3}; restoring again to film it; " +
                            "the rehearsal: {4}",
                            _birthAt, child, parent, LineName(Current), SeekCost()));
                        _rehearsal = Rehearsal.Recurring;
                        RestoreForTheBirth();
                        return;
                    }
                }
            }

            _seekLoopTicks += Stopwatch.GetTimestamp() - entered;

            double wallSoFar = _wall.Elapsed.TotalSeconds - _seekWallFrom;
            double done = live.ElapsedSeconds - _seekFrom;
            double left = _seekTarget - live.ElapsedSeconds;
            double pace = wallSoFar > 0.5d ? done / wallSoFar : double.NaN;
            double world = WorldShare();
            Status = string.Format(CultureInfo.InvariantCulture, "{0} {1:0.#} of {2:0.#} s{3}{4}",
                Phase == SafariPhase.Rehearsing ? "rehearsing, at" : "seeking, at",
                live.ElapsedSeconds, _seekTarget,
                double.IsNaN(pace) || pace <= 0d ? "" : string.Format(CultureInfo.InvariantCulture, ", {0:0.##}x, {1} left", pace, Minutes(left / pace)),
                double.IsNaN(world) || !(wallSoFar > 0.5d) ? "" : string.Format(CultureInfo.InvariantCulture, ", the world's steps {0:0}% of the wall", 100d * world));

            if (live.ElapsedSeconds + 0.5d * dt < _seekTarget) return;

            if (Phase == SafariPhase.Rehearsing && _hasCandidate)
            {
                TakeTheCandidate("by the rehearsal's end");
                return;
            }

            if (Phase == SafariPhase.Rehearsing)
            {
                Say("the rehearsal: " + SeekCost());
                Missed(string.Format(CultureInfo.InvariantCulture,
                    "no birth to a member of {0} came between {1:0.#} s and {2:0.#} s in this cousin",
                    LineName(Current), _watchFrom, _seekTarget));
                return;
            }

            // A seek of any length says where its wall time went; opening on the world already
            // on screen steps nothing and says nothing.
            if (done >= 1d) Say("the seek to " + _seekTarget.ToString("0.#", CultureInfo.InvariantCulture) + " s: " + SeekCost());

            Plan();
        }

        /// <summary>The line's first birth, filmed in place of a named parent's that did not come.</summary>
        private void TakeTheCandidate(string when)
        {
            _birthChild = _candidate.child;
            _birthParent = _candidate.parent;
            _birthChildFlags = _candidate.flags;
            _birthOffset = _candidate.offset;
            _birthAt = _candidate.at;
            Say(string.Format(CultureInfo.InvariantCulture,
                "body {0}, the story's parent, gave no birth {1} in this cousin: the line's first, body {2} to body {3} at {4:0.#} s, is filmed; " +
                "restoring again; the rehearsal: {5}",
                _namedParent, when, _birthChild, _birthParent, _birthAt, SeekCost()));
            _rehearsal = Rehearsal.Recurring;
            RestoreForTheBirth();
        }

        private void RestoreForTheBirth()
        {
            // The same start as the rehearsal's, or the same trajectory is not stepped again.
            double lead = SafariTripBuilder.BirthLeadSeconds;
            var ck = _rehearsalFrom;
            if (ck.path == null)
            {
                _runner.OpenFounding(_runDirectory);
                if (_runner.Live == null) { Fail("the founding would not open: " + _runner.Error); return; }
                AfterOpen(0d);
            }
            else if (!Restore(ck.path, ck.seconds))
            {
                return;
            }

            _seekTarget = _birthAt - lead;
            _seekFrom = _runner.Live.ElapsedSeconds;
            _seekWallFrom = _wall.Elapsed.TotalSeconds;
            StartTheSeekClock(_runner.Live);
            Phase = SafariPhase.Seeking;
        }

        private void StartTheSeekClock(TheatreDynamicsReplay live)
        {
            _seekStepTicks = 0L;
            _seekLoopTicks = 0L;
            _seekTicks = 0;
            _seekCollections0 = GC.CollectionCount(0);
            _seekPhysicsMs0 = live?.Sim != null ? live.Sim.WallPhysicsMs : 0L;
            _seekEconomyMs0 = live?.Sim != null ? live.Sim.WallWorldMs : 0L;
            _seekHarnessMs0 = live?.Sim != null ? live.Sim.WallHarnessMs : 0L;
        }

        /// <summary>The share of the seek's wall time spent inside the world's own steps, 0 to 1.</summary>
        private double WorldShare()
        {
            double wall = _wall.Elapsed.TotalSeconds - _seekWallFrom;
            return wall > 0d ? Seconds(_seekStepTicks) / wall : double.NaN;
        }

        /// <summary>
        /// Where the seek's wall time went, for the log: inside the world's own steps (and, of
        /// those, the solver, the economy and the harness, the three parts of the farm's own
        /// footer), the director's bookkeeping between steps, and the rest of each Editor tick,
        /// which is everything the Editor does between two calls of <see cref="Advance"/>.
        /// </summary>
        private string SeekCost()
        {
            TheatreDynamicsReplay live = _runner.Live;
            double wall = _wall.Elapsed.TotalSeconds - _seekWallFrom;
            if (live == null || !(wall > 0d)) return "no wall time";

            double steps = Seconds(_seekStepTicks);
            double loop = Seconds(_seekLoopTicks);
            double simulated = live.ElapsedSeconds - _seekFrom;

            long physics = live.Sim.WallPhysicsMs - _seekPhysicsMs0;
            long economy = live.Sim.WallWorldMs - _seekEconomyMs0;
            long harness = live.Sim.WallHarnessMs - _seekHarnessMs0;
            double parts = Math.Max(1d, physics + economy + harness);

            return string.Format(CultureInfo.InvariantCulture,
                "{0:0.#} s simulated in {1} of wall ({2:0.##}x); the world's own steps {3:0.#}% of the wall " +
                "(of those, the solver {4:0}%, the economy {5:0}%, the harness {6:0}%), the director's bookkeeping between steps {7:0.#}%, " +
                "the rest of the Editor's {8} ticks {9:0.#}%; {10} garbage collections",
                simulated, Minutes(wall), simulated / wall, 100d * steps / wall,
                100d * physics / parts, 100d * economy / parts, 100d * harness / parts,
                100d * Math.Max(0d, loop - steps) / wall, _seekTicks, 100d * Math.Max(0d, wall - loop) / wall,
                GC.CollectionCount(0) - _seekCollections0);
        }

        private static double Seconds(long stopwatchTicks) => (double)stopwatchTicks / Stopwatch.Frequency;

        /// <summary>
        /// Notes every body born since the last look, and, in a rehearsal, returns the first born
        /// to a member of the scene's parent clade.
        /// </summary>
        private bool Births(TheatreDynamicsReplay live, out long child, out long parent)
        {
            child = parent = -1;
            World world = live.Sim.World;
            if (world.Births == _birthsSeen) return false;
            _birthsSeen = world.Births;

            IReadOnlyList<Organism> living = world.Living;
            Dictionary<long, Organism> byId = null;
            long most = _maxSeenId;

            for (int i = 0; i < living.Count; i++)
            {
                Organism o = living[i];
                if (o.Id <= _maxSeenId) continue;
                most = Math.Max(most, o.Id);

                if (byId == null) { byId = new Dictionary<long, Organism>(living.Count); foreach (Organism l in living) byId[l.Id] = l; }
                _clades.Saw(o, byId);

                SafariClade clade = Current?.Clade;
                long line = Current?.BirthLine ?? -1;
                if (Phase == SafariPhase.Rehearsing && clade != null && child < 0 && live.ElapsedSeconds >= _watchFrom &&
                    o.ParentId >= 0 && byId.TryGetValue(o.ParentId, out Organism p) &&
                    ((_namedAlive && o.ParentId == _namedParent) || (line >= 0 && _clades.CladeOf(p, byId) == line)))
                {
                    var offset = new Vector3(o.X - p.X, o.HeightY - p.HeightY, o.Z - p.Z);
                    if (!_namedAlive || o.ParentId == _namedParent)
                    {
                        child = o.Id;
                        parent = o.ParentId;
                        _birthChildFlags = SafariClades.FlagsOf(o);
                        _birthOffset = offset;
                    }
                    else if (!_hasCandidate)
                    {
                        // The story named a parent that is alive: the line's first birth waits in
                        // case the named one does not come.
                        _hasCandidate = true;
                        _candidate = (o.Id, o.ParentId, SafariClades.FlagsOf(o), offset, live.ElapsedSeconds);
                    }
                }
                else if (_rehearsal == Rehearsal.Recurring && o.ParentId == _birthParent &&
                         Math.Abs(live.ElapsedSeconds - _birthAt) < 2d)
                {
                    // The filmed pass: the rehearsal's birth arriving again.
                    _birthRecurred = true;
                }
            }

            _maxSeenId = most;
            return true;
        }

        // ---------------------------------------------------------------- the plan

        private void Plan()
        {
            TheatreDynamicsReplay live = _runner.Live;
            LiveWorldView view = _runner.LiveView;
            view?.Sync();
            view?.DressUndressed();

            _stageLiving = null;
            Focus(view, Current.Clade);
            var stage = SafariPlans.Stage.Of(live, view, _options.Aspect, Current.Index);
            List<SafariPlans.Take> takes;

            try
            {
                takes = _segment.Build(stage);
            }
            catch (Exception e)
            {
                Fail("the plan threw: " + e.GetType().Name + ": " + e.Message);
                return;
            }

            if (takes == null || takes.Count == 0)
            {
                if (Phase != SafariPhase.Missed) Missed("the plan had nothing to film");
                return;
            }

            _takes = takes;
            _take = 0;
            Current.Plan = string.Join(" | ", takes.Select(t => t.Plan));
            StartTake();
        }

        private void StartTake()
        {
            TheatreDynamicsReplay live = _runner.Live;
            SafariPlans.Take take = _takes[_take];
            take.Shot.ResetTally();

            _takeT0 = live.ElapsedSeconds;
            _takeSteps0 = live.Steps;
            _takeTarget = _takeT0;
            _takeFrame = 0;
            _endPending = false;
            Phase = SafariPhase.Playing;
            Status = "playing " + Current.Slug + (_takes.Count > 1 || _segments.Count > 0 ? " take " + (_take + 1) : "");

            Say(string.Format(CultureInfo.InvariantCulture, "take {0} of {1} at {2:0.#} s: {3}",
                _take + 1, Current.Slug, _takeT0, take.Plan));
            TakeStarted?.Invoke(Current, TakeNumber, take.Plan);
        }

        /// <summary>The take's number across the whole scene, counting earlier segments.</summary>
        private int TakeNumber => _takeCount + _take;
        private int _takeCount;
        private bool _endPending;

        private double Now(TheatreDynamicsReplay live) => _takeT0 + (live.Steps - _takeSteps0) * (double)live.Sim.PhysicsDt;

        /// <summary>
        /// The next frame: the world stepped to the frame's second, the take's camera posed at it.
        /// False when nothing is playing.
        /// </summary>
        /// <param name="interval">Simulated seconds since the last frame.</param>
        /// <param name="wallBudgetSeconds">
        /// The most wall time the stepping may take, or infinity for a record, which must land on
        /// every frame's second; a live viewer that falls behind drops the debt instead.
        /// </param>
        public bool Frame(double interval, double wallBudgetSeconds, out SafariPose pose)
        {
            pose = default;
            if (Phase != SafariPhase.Playing) return false;

            // The take's last frame was handed out on the previous call; it ends now, so the host
            // wrote that frame with the take's own camera and into the take's own directory
            // before the next take (or the next scene) began.
            if (_endPending)
            {
                _endPending = false;
                EndTake();
                return false;
            }

            TheatreDynamicsReplay live = _runner.Live;
            if (live == null) { Fail("the world went away mid-take"); return false; }

            SafariPlans.Take take = _takes[_take];
            float dt = Mathf.Max(1e-4f, live.Sim.PhysicsDt);

            if (_takeFrame > 0) _takeTarget += interval;
            double deadline = _wall.Elapsed.TotalSeconds + wallBudgetSeconds;

            while (Now(live) + 0.5d * dt < _takeTarget)
            {
                if (live.Step()) Births(live, out _, out _);
                if (_wall.Elapsed.TotalSeconds > deadline) { _takeTarget = Now(live); break; }
            }

            _runner.LiveView?.Sync();
            _runner.LiveView?.DressUndressed();

            double into = Now(live) - _takeT0;
            float u = take.Seconds > 0f ? Mathf.Clamp01((float)(into / take.Seconds)) : 1f;

            take.Shot.Pose(live, _runner.LiveView, u, (float)Math.Max(1e-3d, interval), out Vector3 eye, out Quaternion rotation, out float focus);

            double offset = _sceneOffsetBase + into;
            SafariCaption caption = SafariCaptions.At(Current, offset);
            if (caption != null && _shown.Add(caption)) CaptionShown?.Invoke(Current, Now(live), caption.Text, offset);

            pose = new SafariPose
            {
                Eye = eye,
                Rotation = rotation,
                FieldOfView = take.Shot.FieldOfView,
                Portrait = take.Shot.Portrait,
                Focus = focus,
                Caption = caption?.Text,
                Label = string.Format(CultureInfo.InvariantCulture, "COUSIN  t={0:0.0} s", Now(live)),
                Take = TakeNumber,
                TakeFrame = _takeFrame,
                SceneOffset = offset,
                Second = Now(live),
                Shot = take.Shot,
                Dim = _segment != null && _segment.Held && Current.Station == SafariStation.Card ? Mathf.Clamp01(Current.Dim) : 0f,
                Callout = _options.Callouts && Current.Clade != null && Current.Clade.Series.Count > 1 ? Current.Clade : null,
            };

            _takeFrame++;

            if (into + 0.5d * dt >= take.Seconds) _endPending = true;
            return true;
        }

        private void EndTake()
        {
            SafariPlans.Take take = _takes[_take];
            TakeEnded?.Invoke(Current, TakeNumber, take.Shot.Tally());
            Say("take " + (TakeNumber + 1) + " of " + Current.Slug + " ended: " + take.Shot.Tally());

            _sceneOffsetBase += take.Seconds;
            _take++;

            if (_take < _takes.Count)
            {
                StartTake();
                return;
            }

            _takeCount += _takes.Count;

            if (Current.Station == SafariStation.Birth && _rehearsal == Rehearsal.Recurring)
            {
                Say(_birthRecurred
                    ? string.Format(CultureInfo.InvariantCulture, "the birth recurred: body {0} was born to body {1} at {2:0.#} s", _birthChild, _birthParent, _birthAt)
                    : string.Format(CultureInfo.InvariantCulture, "the birth did NOT recur in the filmed pass (body {0} to body {1} at {2:0.#} s in the rehearsal)", _birthChild, _birthParent, _birthAt));
            }

            NextSegment();
        }

        // ---------------------------------------------------------------- station takes

        private List<SafariPlans.Take> PortraitTakes(SafariPlans.Stage stage, SafariScene scene)
        {
            int subject = Subject(stage, scene, out string why);
            if (subject < 0) { Missed(why); return null; }

            bool swimmer = Swimmer(scene, stage.Ids[subject]);

            int hash = scene.Clade?.Hash ?? (int)(stage.Ids[subject] & 0x7FFFFFFF);
            SafariPlans.Take t = SafariPlans.Portrait(stage, subject, (float)scene.Seconds, swimmer, hash);
            if (scene.Clade != null && stage.Ids[subject] != scene.Body)
            {
                Say(string.Format(CultureInfo.InvariantCulture, "the portrait's subject is body {0} of {1}{2}",
                    stage.Ids[subject], scene.Clade.Name, scene.Body >= 0 ? " (the guide's exemplar " + scene.Body + " is not alive in this cousin)" : ""));
            }
            Say(string.Format(CultureInfo.InvariantCulture, "the portrait films body {0} as {1}", stage.Ids[subject],
                swimmer ? "a swimmer (its joints move)" : "a body that holds still"));
            ThisBody(stage, scene, stage.Ids[subject]);
            return One(t);
        }

        /// <summary>
        /// Films the portrait as a swimmer only when its joints move: the clade's share of members
        /// that work their joints (the guide's reading of poses.jsonl) at half or more, or the
        /// exemplar's own reading when the subject is the exemplar the recording carried over.
        /// A body with a joint it holds still is not a swimmer (round 47's body 355, sd 0.00 over
        /// 194 samples, was filmed as one). With no reading, a jointed clade is, as before.
        /// </summary>
        private bool Swimmer(SafariScene scene, long subjectId)
        {
            SafariClade c = scene.Clade;
            if (c == null) return false;
            if (!double.IsNaN(c.Speed)) return c.Speed > SafariPlans.SwimmerSpeed;
            if (!c.Jointed) return false;
            if (subjectId == c.Exemplar && c.ExemplarJointsMove.HasValue && _clades.IsRecorded(subjectId)) return c.ExemplarJointsMove.Value;
            if (!double.IsNaN(c.JointsMovingShare)) return c.JointsMovingShare >= 0.5d;
            return true;
        }

        /// <summary>
        /// The body on screen, said from the live organism: its age against the crowd's, and
        /// whether it is the founder. A restored world is a cousin, so its bodies' ages and
        /// children are its own; the founder is claimed only for the recorded body the
        /// checkpoint carried over (<see cref="SafariClades.IsRecorded"/>), never for a cousin's
        /// body that happens to carry the founder's id. Said only when it is worth a caption: the
        /// founder, or a body older than half of all bodies ever get. It takes the place of the
        /// portrait's least interesting caption that is not a first, and the fact it displaces
        /// moves to the clade's colony when one is still to come.
        /// </summary>
        private void ThisBody(SafariPlans.Stage stage, SafariScene scene, long id)
        {
            SafariClade c = scene.Clade;
            if (scene.FromStory) return; // a story's captions are its writer's alone
            if (c == null || double.IsNaN(_guide.CrowdMedianLife)) return;

            Organism o = null;
            foreach (Organism l in stage.Live.Sim.World.Living) if (l.Id == id) { o = l; break; }
            if (o == null) return;

            bool founder = id == c.Founder && _clades.IsRecorded(id);
            double normal = Math.Round(_guide.CrowdMedianLife / 100d) * 100d;
            if (!founder && o.Age < normal) return;

            string text = "Half of all bodies die by " + SafariCaptions.Clock(normal) + "; this " +
                          (founder ? "founder" : "one") + " is " + SafariCaptions.Clock(Math.Round(o.Age)) + " old.";

            // Slots 1 to 3 hold the scene's facts; the least interesting non-first gives way.
            int give = -1;
            double least = double.PositiveInfinity;
            for (int i = 0; i < scene.Facts.Count && i < 3; i++)
            {
                SafariFact f = scene.Facts[i];
                if (f.Kind == "first") continue;
                if (f.Interest < least) { least = f.Interest; give = i; }
            }

            if (give < 0)
            {
                if (!SafariCaptions.AddFree(scene, SafariCaptions.Slot(1), text, scene.Seconds))
                    Say("the portrait had no room for the body's own caption: " + text);
                else Say("the portrait's own caption: " + text);
                return;
            }

            SafariFact displaced = scene.Facts[give];
            SafariCaptions.Replace(scene, SafariCaptions.Slot(give + 1), text);
            Say("the portrait's own caption, in place of '" + displaced.Text + "': " + text);

            SafariScene colony = null;
            for (int k = scene.Index + 1; k < _scenes.Count; k++)
                if (_scenes[k].Clade == c && _scenes[k].Station == SafariStation.Colony) { colony = _scenes[k]; break; }
            if (colony != null && colony.Facts.Count < 4 && !colony.Facts.Contains(displaced))
            {
                colony.Facts.Add(displaced);
                colony.Refill?.Invoke();
            }
        }

        /// <summary>
        /// The descent, its captions timed to the depths the light's marks sit at: the dolly down
        /// the shallow side, or the canopy's sink when <see cref="SafariOptions.Canopy"/> is on
        /// (whose captions are the marks its eye passes, fewer than the dolly's).
        /// </summary>
        private List<SafariPlans.Take> DescentTakes(SafariPlans.Stage stage, SafariScene scene, int hash, bool canopy)
        {
            // The template's descent has no length of its own (the ceiling sets the dolly's); a
            // story's has its writer's.
            SafariPlans.Take t = canopy
                ? SafariPlans.Canopy(stage, LengthOf(scene, SafariPlans.CanopyDescentSeconds), FilmPlans.CanopyMove.Sink, "descent")
                : scene.Seconds > 0d
                    ? SafariPlans.Descent(stage, hash, exactSeconds: (float)scene.Seconds)
                    : SafariPlans.Descent(stage, hash);
            if (t.EyeAt == null) return One(t);

            float startY = t.EyeAt(0f).y;
            foreach (SafariFact f in scene.Facts.Where(f => !double.IsNaN(f.DepthMetres)).OrderBy(f => f.DepthMetres))
            {
                float y = -(float)f.DepthMetres;
                if (startY <= y) continue; // the dolly starts below the mark
                float at = -1f;
                for (int k = 0; k <= 480; k++)
                {
                    float u = k / 480f;
                    if (t.EyeAt(u).y <= y) { at = u; break; }
                }
                if (at < 0f) continue; // it never gets that deep
                if (SafariCaptions.AddFree(scene, Math.Max(SafariCaptions.FirstOffset, at * t.Seconds - 1d), f.Text, t.Seconds))
                    Say(string.Format(CultureInfo.InvariantCulture, "descent: '{0}' at {1:0.#} s of {2:0} s", f.Text, at * t.Seconds, t.Seconds));
            }
            return One(t);
        }

        /// <summary>The call-outs' tint: the clade in full colour and the rest grey, or everyone as before.</summary>
        private void Focus(LiveWorldView view, SafariClade clade)
        {
            if (view?.Palette == null) return;
            System.Func<long, bool> wanted = null;
            if (_options.Callouts && clade != null)
            {
                long founder = clade.Founder;
                wanted = id =>
                {
                    World world = _runner.Live?.Sim.World;
                    if (world == null) return true;
                    if (_focusLiving == null || _focusLiving.Count != world.Living.Count || !_focusLiving.ContainsKey(id))
                    {
                        _focusLiving = new Dictionary<long, Organism>(world.Living.Count);
                        foreach (Organism o in world.Living) _focusLiving[o.Id] = o;
                    }
                    return _focusLiving.TryGetValue(id, out Organism body) && _clades.CladeOf(body, _focusLiving) == founder;
                };
            }

            // The lineage's hue is not a call-out: it is the owner's leaf ruling (2026-09-24), so
            // it is set whatever the call-outs say, once, and every body is painted by its clade.
            bool lineageNew = false;
            if (view.Palette.LineageOf == null && _clades != null)
            {
                view.Palette.LineageOf = id =>
                {
                    World world = _runner.Live?.Sim.World;
                    if (world == null) return -1;
                    if (_focusLiving == null || _focusLiving.Count != world.Living.Count || !_focusLiving.ContainsKey(id))
                    {
                        _focusLiving = new Dictionary<long, Organism>(world.Living.Count);
                        foreach (Organism o in world.Living) _focusLiving[o.Id] = o;
                    }
                    return _focusLiving.TryGetValue(id, out Organism body) ? _clades.CladeOf(body, _focusLiving) : -1;
                };
                lineageNew = true;
            }

            if (wanted == null && view.Palette.InFocus == null && !lineageNew) return;
            view.Palette.InFocus = wanted;
            view.Redress();
            view.DressUndressed();
        }

        private Dictionary<long, Organism> _focusLiving;

        private List<SafariPlans.Take> ColonyTakes(SafariPlans.Stage stage, SafariScene scene)
        {
            List<int> members = Members(stage, scene.Clade.Founder);
            if (members.Count == 0)
            {
                Missed(scene.Clade.Name + " has no member alive in this cousin at " + SafariCaptions.Seconds(stage.Live.ElapsedSeconds));
                return null;
            }

            // A colony of one is refused: a pull-back on one body says nothing its portrait did
            // not (round 47's first safari filmed Gastrophylla sefecis's "colony of 1 members").
            if (members.Count == 1)
            {
                Missed(scene.Clade.Name + " has one member alive in this cousin at " + SafariCaptions.Seconds(stage.Live.ElapsedSeconds) +
                       ": a colony of one is refused");
                return null;
            }

            // The largest member among the half nearest the members' centre: the pull-back's look
            // travels from it to the centre inside the ceiling, and the colony's largest body can
            // stand tens of metres out at the colony's edge.
            Vector3 centre = stage.Centroid(members);
            List<int> inner = members.OrderBy(i => (stage.Positions[i] - centre).sqrMagnitude).Take(Math.Max(1, (members.Count + 1) / 2)).ToList();
            int anchor = inner[0];
            foreach (int i in inner) if (stage.Reaches[i] > stage.Reaches[anchor]) anchor = i;

            // The chapter card opens the chapter, so it plays first, and the captions wait for it.
            // The pull-back is the scene's length less the card's (the template's colony carries
            // the two summed).
            // A story's chapter card is a take of its own before this one (SegmentsOf).
            bool card = scene.ChapterCard && !scene.FromStory;
            float pull = scene.Seconds > 0d
                ? (float)Math.Max(2d, scene.Seconds - (card ? SafariTripBuilder.ChapterSeconds : 0d))
                : (float)SafariTripBuilder.ColonySeconds;
            var takes = new List<SafariPlans.Take>();
            if (card) takes.Add(SafariPlans.FromAbove(stage, (float)SafariTripBuilder.ChapterSeconds));
            takes.Add(SafariPlans.PullBack(stage, members, anchor, pull, scene.Clade.Hash));
            return takes;
        }

        private List<SafariPlans.Take> BirthTakes(SafariPlans.Stage stage, SafariScene scene)
        {
            int parent = stage.IndexOf(_birthParent);
            if (parent < 0)
            {
                Missed("the rehearsal's parent, body " + _birthParent + ", is not in the world at " +
                       SafariCaptions.Seconds(stage.Live.ElapsedSeconds) + " on the filmed pass");
                return null;
            }

            // What the replay got, said as what it is: a birth in this cousin to a member of the
            // parent line, and whether the child is the clade's kind, as the recorded founder
            // was, or its parent's (the rehearsal takes the first birth in the parent line). A
            // story's birth says only what its writer wrote, and the log says what was got.
            bool same = _birthChildFlags == SafariClades.FlagsOf(scene.Clade);
            double lead = SafariTripBuilder.BirthLeadSeconds;
            float hold = (float)(scene.Seconds > 0d ? Math.Max(lead + 2d, scene.Seconds) : lead + SafariTripBuilder.BirthTailSeconds);
            if (scene.FromStory)
            {
                // The writer's captions stand, and the one line a story's birth keeps is the
                // director's own at the lead: what this replay got is a cousin's birth, which the
                // writer left the lead's slot for (round 48's story, scene 17). A chapter card
                // before the birth moves it by the card's length.
                double card = scene.ChapterCard ? SafariTripBuilder.ChapterSeconds : 0d;
                string said = "In this replay a member of " + LineName(scene) + " gives birth.";
                if (!SafariCaptions.AddFree(scene, card + lead + SafariCaptions.Slot(0), said, card + hold))
                    Say("the story's birth had no free slot for the replay's line: " + said);
                Say(string.Format(CultureInfo.InvariantCulture, "the story's birth: body {0}, a member of {1}, gives birth; the child is {2}",
                    _birthParent, LineName(scene), same ? "the clade's kind" : "not the clade's kind"));
            }
            else
            {
                SafariCaptions.Replace(scene, SafariTripBuilder.BirthLeadSeconds + SafariCaptions.Slot(0),
                    "In this replay a member of " + ParentName(scene.Clade) + " gives birth.");
                SafariCaptions.Replace(scene, SafariTripBuilder.BirthLeadSeconds + SafariCaptions.Slot(1), same
                    ? "This child is " + SafariCaptions.Guild(scene.Clade) + ", as the founder was."
                    : "This child kept its parent's body; the founder did not.");
            }

            // The child's spot from the rehearsal: the filmed pass is the same trajectory from the
            // same checkpoint, so it lands there again when the birth recurs. The birth comes at
            // the lead whatever the scene's length.
            return One(SafariPlans.Hold(stage, parent, hold, (float)lead, _birthOffset, scene.Clade.Hash, "birth"));
        }

        /// <summary>The scene's body: the named one when alive, else the clade's largest member.</summary>
        private int Subject(SafariPlans.Stage stage, SafariScene scene, out string why)
        {
            why = null;

            if (scene.Body >= 0)
            {
                int named = stage.IndexOf(scene.Body);
                if (named >= 0 && (scene.Clade == null || CladeOfIndex(stage, named) == scene.Clade.Founder)) return named;
                if (scene.Clade == null) { why = "body " + scene.Body + " is not alive in this cousin"; return -1; }
            }

            if (scene.Clade == null) { why = "a portrait with neither a clade nor a body"; return -1; }

            List<int> members = Members(stage, scene.Clade.Founder);
            if (members.Count == 0)
            {
                why = scene.Clade.Name + " has no member alive in this cousin at " + SafariCaptions.Seconds(stage.Live.ElapsedSeconds);
                return -1;
            }

            int best = members[0];
            foreach (int i in members)
            {
                if (stage.Reaches[i] > stage.Reaches[best] || (stage.Reaches[i] == stage.Reaches[best] && stage.Ids[i] < stage.Ids[best])) best = i;
            }
            return best;
        }

        private Dictionary<long, Organism> _stageLiving;

        private List<int> Members(SafariPlans.Stage stage, long founder)
        {
            var members = new List<int>();
            for (int i = 0; i < stage.Ids.Count; i++) if (CladeOfIndex(stage, i) == founder) members.Add(i);
            return members;
        }

        private long CladeOfIndex(SafariPlans.Stage stage, int index)
        {
            World world = stage.Live.Sim.World;
            if (_stageLiving == null || _stageLiving.Count != world.Living.Count)
            {
                _stageLiving = new Dictionary<long, Organism>(world.Living.Count);
                foreach (Organism o in world.Living) _stageLiving[o.Id] = o;
            }

            return _stageLiving.TryGetValue(stage.Ids[index], out Organism body) ? _clades.CladeOf(body, _stageLiving) : -1;
        }

        private int MembersAlive(TheatreDynamicsReplay live, long founder)
        {
            IReadOnlyList<Organism> living = live.Sim.World.Living;
            var byId = new Dictionary<long, Organism>(living.Count);
            foreach (Organism o in living) byId[o.Id] = o;
            int n = 0;
            foreach (Organism o in living) if (_clades.CladeOf(o, byId) == founder) n++;
            return n;
        }

        private string ParentName(SafariClade c)
        {
            if (c == null || c.ParentClade < 0) return "no parent clade";
            SafariClade p = _guide?.Find(c.ParentClade);
            return p != null ? p.Name : "the clade founded by body " + c.ParentClade;
        }

        /// <summary>The clade a birth scene waits on for a parent (<see cref="SafariScene.BirthLine"/>), by name.</summary>
        private string LineName(SafariScene scene)
        {
            if (scene == null) return "no clade";
            if (scene.BirthFrom < 0) return ParentName(scene.Clade);
            SafariClade c = scene.Clade != null && scene.Clade.Founder == scene.BirthFrom ? scene.Clade : _guide?.Find(scene.BirthFrom);
            return c != null ? c.Name : "the clade founded by body " + scene.BirthFrom;
        }

        // ---------------------------------------------------------------- endings

        private void Missed(string why)
        {
            Phase = SafariPhase.Missed;
            Status = "missed: " + why;
            Say("scene " + Current?.Slug + " MISSED: " + why);
        }

        private void Fail(string why)
        {
            Phase = SafariPhase.Failed;
            Status = "failed: " + why;
            Say("scene " + Current?.Slug + " FAILED: " + why);
        }

        public static string Minutes(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "an unknown time";
            if (seconds < 90d) return seconds.ToString("0", CultureInfo.InvariantCulture) + " s";
            if (seconds < 5400d) return (seconds / 60d).ToString("0", CultureInfo.InvariantCulture) + " min";
            return (seconds / 3600d).ToString("0.#", CultureInfo.InvariantCulture) + " h";
        }

        /// <summary>The recording's living count at the last sample at or before a second, or -1.</summary>
        public static int RecordedAlive(RunRecord record, double second)
        {
            if (record?.Samples == null || record.Samples.Count == 0) return -1;
            int alive = -1;
            foreach (RunSample s in record.Samples)
            {
                if (s.T > second + 1e-6) break;
                alive = s.Alive;
            }
            return alive;
        }
    }
}
