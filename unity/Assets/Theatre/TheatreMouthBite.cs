using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Evosim.Sim;
using DynCore = Evosim.Dynamics.ConvexCore;
using DynMat = Evosim.Dynamics.Mat3;
using DynVec = Evosim.Dynamics.Vec3;

namespace Evosim.Theatre
{
    /// <summary>
    /// The per-mouth bite timers behind <c>EVOSIM_THEATRE_MOUTH2_BITE</c> (<see cref="TheatreMouth2.Bite"/>): for each eating part, the
    /// film seconds since its last bite onset, written every frame to the shader's <c>_MouthBite</c>, where the pulse (a flash of the
    /// lip and the disc, a swell of the mouth) reads it. Design: <c>logbook/specs/mouth-bite-animation-design.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and off means nothing happens.</b> Every caller tests <c>TheatreMouth2.Bite &gt; 0</c> first, so with the dial
    /// at 0 nothing here runs, no timer is computed and no property is written to any block.
    /// </para>
    /// <para>
    /// <b>The timer and the onset source are apart.</b> A timer is a pure function of the onsets a mouth has had and the second asked
    /// (<see cref="BiteOnsets"/>); where the onsets come from is the reader's business. Today there are four: the pin
    /// (<see cref="TheatreMouth2.BiteAt"/>, no onsets at all), the fake onsets of <see cref="TheatreMouth2.BiteTest"/> (every N film
    /// seconds, on the render clock, on every mouth), the farm's ate bit in a film window's pose stream (<see cref="ReadAteBits"/>:
    /// version 5 on, one bit per eating part per pose frame), and, on a window whose stream is older or holds no frames, the kill rows of
    /// its <c>events.jsonl</c> (<see cref="ReadKills"/>: the killer's part is the mouth that bit). On a version 5 window the ate bit is
    /// the source and the kill rows are not read for onsets; a kill is not a bite, and grazing is a bite with no kill. The pin and the
    /// fake onsets keep their precedence over the recorded ones (<see cref="TimerOf"/>).
    /// </para>
    /// <para>
    /// <b>Film seconds.</b> A timer is in film seconds, so a time-lapse still shows whole motions: the world's seconds since the onset
    /// over the window's <see cref="FilmWindowWorld.Speed"/>. The refractory rule is the same: an onset within <see cref="PulseSeconds"/>
    /// film seconds of the one before it is dropped, so a run of kills reads as a rhythm and never as a flicker. A timer past
    /// <see cref="MotionSeconds"/> is idle (<see cref="TheatreMouth2.BiteIdle"/>).
    /// </para>
    /// <para>
    /// <b>Where a timer goes.</b> A part drawn alone takes it in <c>_MouthBite.x</c> on its own renderers. On the one skin the four
    /// slots <c>_MouthSkin0</c> to <c>_MouthSkin3</c> take it in x to w, by the slot the skin gave the part's mouth
    /// (<see cref="TheatreOneSkin.MouthSlotOfPart"/>); the skin keeps them and writes them at every dress. The palette's paint
    /// (<see cref="Seen"/>) and the skin's dress (<see cref="SkinSeen"/>) say which bodies there are.
    /// </para>
    /// </remarks>
    public static partial class TheatreMouthBite
    {
        /// <summary>The pulse's length, film seconds; also the refractory gap between two onsets of one mouth.</summary>
        public const float PulseSeconds = 0.35f;

        /// <summary>
        /// Past this many film seconds since an onset a timer is written idle: longer than any motion the kinds will have (the crown's
        /// 0.7 s), so an idle mouth costs no writes.
        /// </summary>
        public const float MotionSeconds = 1f;

        // ---------------------------------------------------------------- the onsets

        /// <summary>The onsets of every mouth, in the world's seconds, and the timer they give at a second.</summary>
        private sealed class BiteOnsets
        {
            private readonly Dictionary<(long, int), List<double>> _raw = new Dictionary<(long, int), List<double>>();
            private Dictionary<(long, int), double[]> _accepted;
            private double _speed = double.NaN;

            /// <summary>Onsets added.</summary>
            public int Scheduled;

            /// <summary>Onsets the refractory rule dropped at the speed last asked.</summary>
            public int Dropped;

            /// <summary>Mouths with at least one onset.</summary>
            public int Mouths => _raw.Count;

            /// <summary>An onset of part <paramref name="part"/> of body <paramref name="id"/> at a world second.</summary>
            public void Add(long id, int part, double second)
            {
                if (!_raw.TryGetValue((id, part), out List<double> list))
                {
                    list = new List<double>();
                    _raw[(id, part)] = list;
                }

                list.Add(second);
                Scheduled++;
                _accepted = null;
            }

            /// <summary>
            /// The film seconds since the latest accepted onset at or before <paramref name="second"/>; false (idle) where there is none or
            /// it is over. <paramref name="speed"/> is the world's seconds a film second.
            /// </summary>
            public bool TryTimer(long id, int part, double second, double speed, out float seconds)
            {
                seconds = TheatreMouth2.BiteIdle;

                if (_accepted == null || speed != _speed) Accept(speed);
                if (!_accepted.TryGetValue((id, part), out double[] list)) return false;

                int lo = 0, hi = list.Length;

                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (list[mid] <= second) lo = mid + 1;
                    else hi = mid;
                }

                if (lo == 0) return false;

                double t = (second - list[lo - 1]) / speed;
                if (!(t < MotionSeconds)) return false;

                seconds = (float)Math.Max(0d, t);
                return true;
            }

            /// <summary>
            /// EVOSIM_THEATRE_MOUTH2_GAPE: the share a mouth is open at <paramref name="second"/>, from its next accepted onset after the
            /// second and the one at or before it (<see cref="GapeShape"/>); 0 where none comes within the lead. A pure function of the
            /// onsets and the second, so a film draws the same gape at the same second every time.
            /// </summary>
            public float GapeOf(long id, int part, double second, double speed, double lead)
            {
                if (_accepted == null || speed != _speed) Accept(speed);
                if (!_accepted.TryGetValue((id, part), out double[] list)) return 0f;

                int lo = 0, hi = list.Length;

                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (list[mid] <= second) lo = mid + 1;
                    else hi = mid;
                }

                // list[lo] is the first onset after the second; at an onset's own second the mouth is shut.
                if (lo >= list.Length) return 0f;

                double until = (list[lo] - second) / speed;
                double gap = lo > 0 ? (list[lo] - list[lo - 1]) / speed : double.PositiveInfinity;

                return GapeShape(until, Math.Min(lead, gap));
            }

            /// <summary>The accepted onsets of every mouth, by second (then body, then part), built with them.</summary>
            private BiteOnset[] _flat;

            /// <summary>
            /// The accepted onsets with <paramref name="from"/> &lt; second &lt;= <paramref name="to"/>, in order of second, body and
            /// part, into <paramref name="into"/> (cleared first). The crumbs read them.
            /// </summary>
            public void Between(double from, double to, double speed, List<BiteOnset> into)
            {
                into.Clear();
                if (_accepted == null || speed != _speed) Accept(speed);

                int lo = 0, hi = _flat.Length;

                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (_flat[mid].Second <= from) lo = mid + 1;
                    else hi = mid;
                }

                for (int i = lo; i < _flat.Length && _flat[i].Second <= to; i++) into.Add(_flat[i]);
            }

            // The refractory rule: an onset less than PulseSeconds of film after the one kept before it is dropped.
            private void Accept(double speed)
            {
                _speed = speed;
                _accepted = new Dictionary<(long, int), double[]>(_raw.Count);
                Dropped = 0;
                var flat = new List<BiteOnset>();

                foreach (KeyValuePair<(long, int), List<double>> pair in _raw)
                {
                    List<double> sorted = pair.Value;
                    sorted.Sort();

                    var kept = new List<double>(sorted.Count);
                    double last = double.NegativeInfinity;

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if ((sorted[i] - last) / speed < PulseSeconds) { Dropped++; continue; }

                        kept.Add(sorted[i]);
                        last = sorted[i];
                        flat.Add(new BiteOnset { Id = pair.Key.Item1, Part = pair.Key.Item2, Second = sorted[i] });
                    }

                    _accepted[pair.Key] = kept.ToArray();
                }

                flat.Sort(BiteOnsetOrder);
                _flat = flat.ToArray();
            }
        }

        /// <summary>One accepted onset: the body, its eating part, the world second.</summary>
        private struct BiteOnset
        {
            public long Id;
            public int Part;
            public double Second;
        }

        private static readonly Comparison<BiteOnset> BiteOnsetOrder = (a, b) =>
            a.Second != b.Second ? a.Second.CompareTo(b.Second) : a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.Part.CompareTo(b.Part);

        // ---------------------------------------------------------------- the gape

        /// <summary>How long a mouth takes to shut at the end of its gape, film seconds (at most half the lead).</summary>
        public const float GapeShutSeconds = 0.08f;

        /// <summary>
        /// The gape's shape: the share open with <paramref name="until"/> film seconds to the onset and <paramref name="lead"/> film
        /// seconds of lead (the dial, or less where the onset before is nearer). Shut at the lead's start, opening with a smoothstep to
        /// fully open <see cref="GapeShutSeconds"/> (or half the lead, if less) before the onset, then shutting fast, quickening into
        /// the onset, where it is 0. Continuous everywhere, so a mouth never pops open or shut between two frames.
        /// </summary>
        internal static float GapeShape(double until, double lead)
        {
            if (!(lead > 1e-4) || !(until > 0d) || until >= lead) return 0f;

            double shut = Math.Min(GapeShutSeconds, 0.5d * lead);

            if (until >= shut)
            {
                double x = (lead - until) / (lead - shut);
                return (float)(x * x * (3d - 2d * x));
            }

            double y = 1d - until / shut;
            return (float)(1d - y * y);
        }

        // ---------------------------------------------------------------- the bodies

        private sealed class Body
        {
            public Transform Root;
            public Phenotype Phenotype;

            /// <summary>The eating parts, and per part the renderers of its own visuals.</summary>
            public int[] Parts;
            public MeshRenderer[][] Renderers;

            /// <summary>The timer last written per part (NaN: not known, write again), and the skin's four.</summary>
            public float[] Last;
            public Vector4 LastSkin = TheatreMouth2.BiteIdleVector;

            /// <summary>EVOSIM_THEATRE_MOUTH2_GAPE: the gape share last written per part (NaN: write again), and the skin's four.</summary>
            public float[] LastGape;
            public Vector4 LastSkinGape = Vector4.zero;
            public bool SkinGapeWritten;
        }

        private static Dictionary<long, Body> _bodies;
        private static Dictionary<long, TheatreOneSkin> _skins;
        private static List<long> _gone;
        private static MaterialPropertyBlock _block;

        private static FilmWindowWorld _window;
        private static BiteOnsets _onsets;

        // ---------------------------------------------------------------- the hooks

        /// <summary>
        /// Called by the palette as it paints a body: remembers the renderers of its eating parts, or forgets the body when it is not to
        /// be drawn with the look (a corpse, the raw shapes). A repaint marks the body for a rewrite of its timers.
        /// </summary>
        public static void Seen(long id, Transform root, Phenotype phenotype, bool hidden)
        {
            if (root == null || phenotype == null) return;

            if (_bodies == null) _bodies = new Dictionary<long, Body>();

            _bodies.TryGetValue(id, out Body body);

            if (hidden)
            {
                if (body != null)
                {
                    WriteIdle(body);
                    _bodies.Remove(id);
                }

                return;
            }

            if (body != null && body.Root == root && ReferenceEquals(body.Phenotype, phenotype))
            {
                for (int k = 0; k < body.Last.Length; k++) body.Last[k] = float.NaN;
                for (int k = 0; k < body.LastGape.Length; k++) body.LastGape[k] = float.NaN;
                return;
            }

            Body built = Build(root, phenotype);

            if (built == null)
            {
                if (body != null) _bodies.Remove(id);
                return;
            }

            _bodies[id] = built;
        }

        /// <summary>Called by the one skin at its dress: this skin holds the body's merged mouths, in four slots.</summary>
        public static void SkinSeen(long id, TheatreOneSkin skin)
        {
            if (skin == null) return;

            if (_skins == null) _skins = new Dictionary<long, TheatreOneSkin>();
            _skins[id] = skin;
        }

        /// <summary>
        /// Called with every rendered frame: reads the window's onsets once (the ate bit of a film window, else its kill rows), then gives
        /// every remembered mouth its timer. Writes only the timers that changed.
        /// </summary>
        /// <param name="frame">The world on screen, or null for a view that has no run (the fake onsets and the pin need none).</param>
        public static void Frame(ITheatreFrame frame)
        {
            if (!(TheatreMouth2.Bite > 0f)) return;

            double second = double.NaN;
            double speed = 1d;
            BiteOnsets onsets = null;

            if (frame is FilmWindowWorld window)
            {
                speed = Math.Max(1d, window.Speed);

                if (!ReferenceEquals(window, _window))
                {
                    // The onsets depend on the speed, which a film sets before its first frame.
                    _window = window;
                    _onsets = ReadOnsets(window, speed);
                }

                second = window.Second;
                onsets = _onsets;

                // EVOSIM_THEATRE_BITE_CRUMBS: the crumbs of the onsets shown so far, for the next camera to draw.
                if (TheatreMouth2.BiteCrumbs) CrumbsFrame(window, onsets, speed);

                // EVOSIM_THEATRE_WOUND_PLUME: the plumes of the blows that hurt an animal, with the crumbs on or off.
                if (WoundPlume) PlumeFrame(window, onsets, speed);

                // EVOSIM_THEATRE_BITE_BUBBLES: the bubbles of the onsets shown so far, with the crumbs on or off.
                if (BiteBubbles) BubbleFrame(window, onsets, speed);
            }

            if (_bodies == null || _bodies.Count == 0) return;

            if (_gone == null) _gone = new List<long>();
            _gone.Clear();

            double clock = Time.timeAsDouble;

            foreach (KeyValuePair<long, Body> pair in _bodies)
            {
                Body body = pair.Value;

                if (body.Root == null)
                {
                    _gone.Add(pair.Key);
                    continue;
                }

                TheatreOneSkin skin = null;
                _skins?.TryGetValue(pair.Key, out skin);

                Vector4 onSkin = TheatreMouth2.BiteIdleVector;
                Vector4 gapeSkin = Vector4.zero;
                bool gapeOn = TheatreMouth2.Gape > 0f;

                for (int k = 0; k < body.Parts.Length; k++)
                {
                    float t = TimerOf(pair.Key, body.Parts[k], clock, onsets, second, speed);

                    if (t != body.Last[k])
                    {
                        WritePart(body, k, t);
                        body.Last[k] = t;
                    }

                    int slot = skin != null ? skin.MouthSlotOfPart(body.Parts[k]) : -1;
                    if (slot >= 0) onSkin[slot] = t;

                    // EVOSIM_THEATRE_MOUTH2_GAPE: the share open, written only with the dial on and only where it changed.
                    if (gapeOn)
                    {
                        float g = GapeOf(pair.Key, body.Parts[k], clock, onsets, second, speed);

                        if (!(g == body.LastGape[k]))
                        {
                            WritePartGape(body, k, g);
                            body.LastGape[k] = g;
                        }

                        if (slot >= 0) gapeSkin[slot] = g;
                    }
                }

                if (skin != null && onSkin != body.LastSkin)
                {
                    skin.SetMouthBite(onSkin);
                    body.LastSkin = onSkin;
                }

                if (gapeOn && skin != null && (!body.SkinGapeWritten || gapeSkin != body.LastSkinGape))
                {
                    skin.SetMouthGape(gapeSkin);
                    body.LastSkinGape = gapeSkin;
                    body.SkinGapeWritten = true;
                }
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                _bodies.Remove(_gone[i]);
                _skins?.Remove(_gone[i]);
            }
        }

        /// <summary>For a census render: every timer back to idle, so the record pictures keep today's look. The next <see cref="Frame"/> brings them back.</summary>
        public static void Suspend()
        {
            // The census pictures keep today's look: no crumbs drawn until the next frame brings them back.
            if (TheatreMouth2.BiteCrumbs) CrumbsSuspend();

            if (_bodies == null) return;

            foreach (KeyValuePair<long, Body> pair in _bodies)
            {
                Body body = pair.Value;
                if (body.Root == null) continue;

                WriteIdle(body);

                if (_skins != null && _skins.TryGetValue(pair.Key, out TheatreOneSkin skin) && body.LastSkin != TheatreMouth2.BiteIdleVector)
                {
                    skin.SetMouthBite(TheatreMouth2.BiteIdleVector);
                    body.LastSkin = TheatreMouth2.BiteIdleVector;
                }

                // The gape shut on the skin too (only ever written with its dial on).
                if (body.SkinGapeWritten && body.LastSkinGape != Vector4.zero && _skins != null &&
                    _skins.TryGetValue(pair.Key, out TheatreOneSkin gapeSkin))
                {
                    gapeSkin.SetMouthGape(Vector4.zero);
                    body.LastSkinGape = Vector4.zero;
                }
            }
        }

        // ---------------------------------------------------------------- the timer

        // The pin wins; otherwise the shorter of the fake onsets' timer and the recorded onsets'.
        private static float TimerOf(long id, int part, double clock, BiteOnsets onsets, double second, double speed)
        {
            if (!float.IsNaN(TheatreMouth2.BiteAt)) return TheatreMouth2.BiteAt;

            float timer = TheatreMouth2.BiteIdle;

            if (TheatreMouth2.BiteTest > 0f)
            {
                float since = (float)(clock % TheatreMouth2.BiteTest);
                if (since < MotionSeconds) timer = since;
            }

            if (onsets != null && !double.IsNaN(second) && onsets.TryTimer(id, part, second, speed, out float recorded))
            {
                timer = Mathf.Min(timer, recorded);
            }

            return timer;
        }

        // EVOSIM_THEATRE_MOUTH2_GAPE: the share open. The pin holds every mouth shut (its stills keep their look); otherwise the larger of
        // the fake onsets' gape (every BiteTest film seconds, on the render clock) and the recorded onsets'.
        private static float GapeOf(long id, int part, double clock, BiteOnsets onsets, double second, double speed)
        {
            if (!float.IsNaN(TheatreMouth2.BiteAt)) return 0f;

            double lead = TheatreMouth2.Gape;
            float share = 0f;

            if (TheatreMouth2.BiteTest > 0f)
            {
                double period = TheatreMouth2.BiteTest;
                share = GapeShape(period - clock % period, Math.Min(lead, period));
            }

            if (onsets != null && !double.IsNaN(second))
            {
                share = Mathf.Max(share, onsets.GapeOf(id, part, second, speed, lead));
            }

            return share;
        }

        // ---------------------------------------------------------------- the onsets of a window

        // A window's onsets, read once: the ate bit where the pose stream carries it, else the kill rows. One log line says which, and how
        // the refractory rule treated them at this speed.
        private static BiteOnsets ReadOnsets(FilmWindowWorld window, double speed)
        {
            var inv = CultureInfo.InvariantCulture;
            FilmWindowReader reader = window.Window;
            string version = reader.Stream != null ? "v" + reader.Stream.Header.Version.ToString(inv) : "none";
            string span = string.Format(inv, "t {0:0} to {1:0} s", window.FirstSecond, window.LastSecond);

            BiteOnsets onsets;
            string source;
            string note;

            if (reader.RecordsBites && reader.FrameCount > 0)
            {
                source = "ate bit";
                onsets = ReadAteBits(window, speed, out note, meals: true);
            }
            else
            {
                source = "kill rows";
                onsets = ReadKills(window, out int rows, out int unattributed);
                note = string.Format(inv, "{0} kill rows in the window, {1} skipped for want of a killer or a part", rows, unattributed);
            }

            // Filtering is lazy; ask once so the dropped count is known for the line.
            onsets.TryTimer(-1L, -1, 0d, speed, out float unused);

            Debug.Log(string.Format(inv,
                "[Theatre] mouth 2 bite: source {0} (pose stream {1}, {2}): {3} onsets scheduled on {4} mouths at {5:0.##}x, {6} dropped by the refractory ({7:0.##} film s); {8}.",
                source, version, span, onsets.Scheduled, onsets.Mouths, speed, onsets.Dropped, PulseSeconds, note));

            return onsets;
        }

        // The state of one body's bits between pose frames.
        private sealed class AteState
        {
            public uint Previous;
            public int Frame = int.MinValue;

            /// <summary>The world second of each eating ordinal's latest onset.</summary>
            public readonly double[] Last = new double[MouthPoint.MaxMaskedEatingParts];

            /// <summary>The world second each eating ordinal's bit was last set, onset or not (<see cref="TheatreMouth2.BiteMeal"/>).</summary>
            public readonly double[] Seen = new double[MouthPoint.MaxMaskedEatingParts];

            public AteState()
            {
                for (int k = 0; k < Last.Length; k++) Last[k] = double.NegativeInfinity;
                for (int k = 0; k < Seen.Length; k++) Seen[k] = double.NegativeInfinity;
            }
        }

        /// <summary>
        /// The ate bit of a version 5 window as onsets. Bit k of a body's mask (<c>PoseBody.AteMask</c>) is its k-th eating part in part
        /// order, set when that part took matter since the previous pose frame. An onset is a frame whose bit is set after a frame whose
        /// bit was clear (a frame the body is absent from counts as clear); while the bit stays set, another comes at the first frame
        /// <see cref="PulseSeconds"/> of film after the last, so sustained eating is a rhythm. Ordinal k is mapped to a part of the body
        /// developed as the frame has it (<see cref="EatingPartsOf"/>). With <paramref name="meals"/> and
        /// <see cref="TheatreMouth2.BiteMeal"/> set, an onset is instead a set bit after a gap longer than the dial, one a meal.
        /// </summary>
        private static BiteOnsets ReadAteBits(FilmWindowWorld window, double speed, out string note,
            List<(long Id, int Part, double Second)> record = null, bool meals = false)
        {
            FilmWindowReader reader = window.Window;
            var onsets = new BiteOnsets();
            var states = new Dictionary<long, AteState>();
            var genomes = new Dictionary<long, Genome>();
            var parts = new Dictionary<(long, FilmWindowPlan), int[]>();

            // The same cut the window's own plan choice makes (FilmWindowWorld.AdultFor): the rows written as the window opened stand from
            // its first frame.
            FilmWindowVerdict verdict = reader.Verdict;
            double cut = verdict != null && !double.IsNaN(verdict.From)
                ? verdict.From + (double.IsNaN(verdict.PhysicsDt) ? 0d : 0.999d * verdict.PhysicsDt)
                : window.FirstSecond;

            // A little over the pulse, so the refractory rule's own comparison never drops a sustained onset to rounding.
            double step = PulseSeconds * speed * 1.000001d;
            // The pulse's onsets only: the ate-contact log measures every half-second onset whatever the look.
            double meal = meals ? TheatreMouth2.BiteMeal : 0d;
            int unmapped = 0;
            int frames = reader.FrameCount;

            for (int f = 0; f < frames; f++)
            {
                PoseFrame frame = reader.ReadFrame(f);
                double second = frame.Seconds;

                for (int b = 0; b < frame.Bodies.Length; b++)
                {
                    PoseBody body = frame.Bodies[b];

                    if (!states.TryGetValue(body.Id, out AteState state))
                    {
                        state = new AteState();
                        states[body.Id] = state;
                    }

                    uint previous = state.Frame == f - 1 ? state.Previous : 0u;
                    uint mask = body.AteMask;
                    state.Previous = mask;
                    state.Frame = f;

                    if (mask == 0u) continue;

                    int[] ordinals = null;
                    bool mapped = false;

                    for (int k = 0; k < state.Last.Length; k++)
                    {
                        if ((mask & (1u << k)) == 0u) continue;

                        // EVOSIM_THEATRE_MOUTH2_BITE_MEAL: a meal is one onset, at the first bit after a gap longer than the dial.
                        bool onset;
                        if (meal > 0d)
                        {
                            onset = second - state.Seen[k] > meal;
                            state.Seen[k] = second;
                        }
                        else onset = (previous & (1u << k)) == 0u || second - state.Last[k] >= step;

                        if (!onset) continue;

                        if (!mapped)
                        {
                            ordinals = EatingPartsOf(window, body.Id, second, cut, genomes, parts);
                            mapped = true;
                        }

                        if (ordinals == null || k >= ordinals.Length)
                        {
                            unmapped++;
                            continue;
                        }

                        state.Last[k] = second;
                        onsets.Add(body.Id, ordinals[k], second);
                        record?.Add((body.Id, ordinals[k], second));
                    }
                }
            }

            note = string.Format(CultureInfo.InvariantCulture, "{0} pose frames, {1} onsets with no eating part to name", frames, unmapped);
            if (meal > 0d) note += string.Format(CultureInfo.InvariantCulture, "; one onset a meal, a meal ending after {0:0.###} s with no bite", meal);
            return onsets;
        }

        // The part index of each eating ordinal of a body as the frame has it: the genome developed at the plan in force at the second
        // (as FilmWindowWorld.AdultFor picks it, without its degrees-of-freedom fallback), the eating parts counted in part order by
        // MouthPoint.IsEatingPart, which is the farm's own test for the mask. Null where the body has no genome the build can read or
        // develop. Cached per body and plan.
        private static int[] EatingPartsOf(FilmWindowWorld window, long id, double second, double cut,
            Dictionary<long, Genome> genomes, Dictionary<(long, FilmWindowPlan), int[]> cache)
        {
            IReadOnlyList<FilmWindowPlan> plans = window.Window.PlansOf(id);
            FilmWindowPlan plan = null;

            for (int i = 0; i < plans.Count; i++)
            {
                double at = plans[i].Seconds;
                if (at <= cut || at < second - 1e-6) plan = plans[i];
                else break;
            }

            if (cache.TryGetValue((id, plan), out int[] known)) return known;

            int[] ordinals = null;

            if (!genomes.TryGetValue(id, out Genome genome))
            {
                if (window.Window.TryGenome(id, out string row))
                {
                    try
                    {
                        genome = GenomeJson.Read(row);
                    }
                    catch (Exception)
                    {
                        try { genome = PictureGenome.Read(row, out int format); }
                        catch (Exception) { genome = null; }
                    }
                }

                genomes[id] = genome;
            }

            if (genome != null)
            {
                try
                {
                    Phenotype phenotype = SnapshotWorld.DevelopWithPlan(genome, window.Record.Config, plan?.ModuleCounts, plan?.LostPaths);
                    var list = new List<int>();

                    for (int p = 0; p < phenotype.PartCount && list.Count < MouthPoint.MaxMaskedEatingParts; p++)
                    {
                        if (MouthPoint.IsEatingPart(phenotype.Parts[p])) list.Add(p);
                    }

                    ordinals = list.ToArray();
                }
                catch (Exception)
                {
                    ordinals = null;
                }
            }

            cache[(id, plan)] = ordinals;
            return ordinals;
        }

        // ---------------------------------------------------------------- the kill rows

        /// <summary>One kill row of a film window: <c>{"e":"k","t":T,"id":VICTIM,"by":KILLER,"byPart":P}</c>.</summary>
        private sealed class KillRow
        {
            public double T;
            public long Victim;

            /// <summary>The killer's id and the part that bit, or -1 where the row names none.</summary>
            public long Killer;
            public int Part;

            /// <summary>The victim's part the bite took (the row's <c>part</c>), or -1 for a row written before it.</summary>
            public int VictimPart = -1;

            /// <summary>The row's <c>root</c>: 1 where the whole body died (and was swallowed in the pass), 0 where one part was cut off and the body lives on, -1 where absent.</summary>
            public int Root = -1;

            /// <summary>The kill-contact line has been written at the row's nearest frame.</summary>
            public bool Done;

            /// <summary>The smallest distance in seconds between the row and a frame shown so far.</summary>
            public double Best = double.PositiveInfinity;
        }

        private static FilmWindowWorld _killsWindow;
        private static List<KillRow> _kills;

        // A window's kill rows, read once per window.
        private static List<KillRow> KillsOf(FilmWindowWorld window)
        {
            if (ReferenceEquals(window, _killsWindow) && _kills != null) return _kills;

            _killsWindow = window;
            _kills = new List<KillRow>();

            foreach (FilmWindowEvent e in window.Window.Events)
            {
                if (e.Type != "k") continue;

                int part = -1;
                bool attributed = e.By >= 0L && TryInt(e.Row, "\"byPart\":", out part) && part >= 0;

                int victimPart = TryInt(e.Row, "\"part\":", out int named) && named >= 0 ? named : -1;

                int root = TryInt(e.Row, "\"root\":", out int rooted) ? rooted : -1;

                _kills.Add(new KillRow { T = e.Seconds, Victim = e.Id, Killer = attributed ? e.By : -1L, Part = attributed ? part : -1, VictimPart = victimPart, Root = root });
            }

            return _kills;
        }

        /// <summary>
        /// A film window's kill rows as onsets at second T on the killer's part P: the fallback where the pose stream carries no ate bit.
        /// A row with no killer or no part cannot say which mouth bit and is skipped; <paramref name="rows"/> and
        /// <paramref name="unattributed"/> count the rows and the skipped ones for the log.
        /// </summary>
        private static BiteOnsets ReadKills(FilmWindowWorld window, out int rows, out int unattributed)
        {
            var onsets = new BiteOnsets();
            unattributed = 0;
            List<KillRow> kills = KillsOf(window);
            rows = kills.Count;

            for (int i = 0; i < kills.Count; i++)
            {
                if (kills[i].Killer < 0L) { unattributed++; continue; }
                onsets.Add(kills[i].Killer, kills[i].Part, kills[i].T);
            }

            return onsets;
        }

        // ---------------------------------------------------------------- the kill-contact log

        /// <summary>
        /// <c>EVOSIM_THEATRE_KILL_CONTACT_LOG</c> (0 or 1, 0 off by default), read once and independent of the bite dial: with it on, a
        /// film window played in the theatre logs, for each kill row, how far the killer's mouth was from the victim's surface at the
        /// pose frame nearest the row (<see cref="LogContacts"/>). Nothing else changes.
        /// </summary>
        public static readonly bool KillContactLog = TheatreSkin.Dial("EVOSIM_THEATRE_KILL_CONTACT_LOG", 0f, 0f, 1f) >= 0.5f;

        /// <summary>A kill whose nearest frame is not shown is still logged at a shown frame within this many window seconds of it.</summary>
        public const double ContactEdgeSeconds = 2d;

        /// <summary>
        /// <c>EVOSIM_THEATRE_ATE_CONTACT_ALL</c> (0 or 1, 0 off by default), read once. With it and <see cref="KillContactLog"/> both on,
        /// the ate-contact log measures every ate-bit onset of every body in the window, whatever <c>EVOSIM_THEATRE_FILM_SUBJECT</c>
        /// says, and writes one summary line per window. The close shot still frames the film subject's body. Off, only the subject's
        /// onsets are measured.
        /// </summary>
        public static readonly bool AteContactAll = TheatreSkin.Dial("EVOSIM_THEATRE_ATE_CONTACT_ALL", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_MOUTH_TRUE</c> (0, 1 or 2, 0 off by default), read once. D275's open point 2: at 1 the kill-contact and
        /// ate-contact logs print the farm's measure for gap, radius and gap/radius (<c>MouthTouch.Reach</c>, over every eating part and
        /// every part of the other body), so a filmed bite's ratio and a counted bite's agree. At 2 the line keeps the drawn measure and
        /// adds two ratios beside it, the farm's and the farm's measure on the line's own mouth and part, so a table can tell what the
        /// measure changes from what the nearest pair changes. Off, every log line is the bytes it was. It reads poses and writes only
        /// log text; drawing, the pulse and every state drawing reads are untouched.
        /// </summary>
        private static readonly int MouthTrueLevel = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH_TRUE", 0f, 0f, 2f));

        private static FilmWindowWorld _contactWindow;
        private static int _contactFrame = -1;

        /// <summary>
        /// Called with every rendered frame while <see cref="KillContactLog"/> is on. When the window shows a new pose frame, writes
        /// <c>[Theatre] kill contact: ...</c> once for each kill row whose nearest frame it is, after the recorded pose has been applied to
        /// the part transforms (<c>FilmWindowWorld.Show</c> runs before any render). A row whose nearest frame a clip never shows (a scene
        /// that starts or ends just short of the kill) is also logged at each shown frame within <see cref="ContactEdgeSeconds"/> of it
        /// that is nearer than any shown so far, with a note, so the clip's last line for it is its closest.
        /// </summary>
        public static void LogContacts(ITheatreFrame frame)
        {
            if (!KillContactLog || !(frame is FilmWindowWorld window) || window.FrameIndex < 0) return;
            if (ReferenceEquals(window, _contactWindow) && window.FrameIndex == _contactFrame) return;

            // A later scene that plays the same window opens it again, earlier than the last frame seen: every kill is logged afresh.
            bool reopened = ReferenceEquals(window, _contactWindow) && window.FrameIndex < _contactFrame;

            _contactWindow = window;
            _contactFrame = window.FrameIndex;

            double shown = window.Second;
            List<KillRow> kills = KillsOf(window);

            if (reopened)
            {
                for (int i = 0; i < kills.Count; i++)
                {
                    kills[i].Done = false;
                    kills[i].Best = double.PositiveInfinity;
                }
            }

            for (int i = 0; i < kills.Count; i++)
            {
                KillRow kill = kills[i];
                if (kill.Done) continue;

                double gap = Math.Abs(shown - kill.T);

                if (window.Window.NearestFrame(kill.T) == window.FrameIndex)
                {
                    kill.Done = true;
                    kill.Best = gap;
                    LogContact(window, kill, "");
                }
                else if (gap <= ContactEdgeSeconds && gap < kill.Best)
                {
                    kill.Best = gap;
                    LogContact(window, kill, " [within " + ContactEdgeSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                                             " s of the kill, not its nearest frame]");
                }
            }

            LogAteContacts(window, shown, reopened);
        }

        private static void LogContact(FilmWindowWorld window, KillRow kill, string note)
        {
            var inv = CultureInfo.InvariantCulture;
            string head = string.Format(inv, "[Theatre] kill contact: t {0:0.###} killer {1} part {2} victim {3} part ",
                kill.T, kill.Killer >= 0L ? kill.Killer.ToString(inv) : "?", kill.Part >= 0 ? kill.Part.ToString(inv) : "?", kill.Victim);
            string tail = string.Format(inv, " frame {0} (window sec {1:0.###}){2}", window.FrameIndex, window.Second, note);

            if (!TryContact(window, kill, out int victimPart, out float gap, out float radius, out string nearestNote, out string absent))
            {
                Debug.Log(head + "? absent: " + absent + tail);
                return;
            }

            float shownGap = gap, shownRadius = radius, shownRatio = gap / radius;
            string farmNote = "";

            if (MouthTrueLevel > 0 &&
                TryBody(window, kill.Killer, out Phenotype farmKiller, out Transform[] farmKillerParts) &&
                TryBody(window, kill.Victim, out Phenotype farmVictim, out Transform[] farmVictimParts))
            {
                FarmNote(window, farmKiller, farmKillerParts, kill.Part, farmVictim, farmVictimParts, victimPart,
                    ref shownGap, ref shownRadius, ref shownRatio, out farmNote);
            }

            Debug.Log(head + victimPart.ToString(inv) + string.Format(inv,
                " mouth-to-surface {0:0.0000} m mouth radius {1:0.0000} m (gap/radius {2:0.###})", shownGap, shownRadius, shownRatio) + farmNote + nearestNote + tail);
        }

        // The body's phenotype at the size it is drawn at and its part transforms, from the view that holds it (bodies, then corpses, then remains).
        private static bool TryBody(FilmWindowWorld window, long id, out Phenotype sized, out Transform[] parts)
        {
            sized = null;
            parts = null;

            if (window.View != null && window.View.TryBody(id, out sized, out parts)) return true;
            if (window.CorpseView != null && window.CorpseView.TryBody(id, out sized, out parts)) return true;
            if (window.RemainsView != null && window.RemainsView.TryBody(id, out sized, out parts)) return true;

            return false;
        }

        // The signed distance from the killer part's mouth point to the victim's part the row names (the nearest one where it names
        // none), which part that is, and a note on how it compares with the nearest.
        private static bool TryContact(FilmWindowWorld window, KillRow kill, out int victimPart, out float gap, out float radius,
            out string nearestNote, out string absent)
        {
            var inv = CultureInfo.InvariantCulture;
            victimPart = -1;
            gap = float.NaN;
            radius = float.NaN;
            nearestNote = "";
            absent = null;

            if (kill.Killer < 0L || kill.Part < 0)
            {
                absent = "the row names no killer part";
                return false;
            }

            if (!TryBody(window, kill.Killer, out Phenotype killer, out Transform[] killerParts))
            {
                absent = "killer " + kill.Killer.ToString(inv) + " is not in the frame";
                return false;
            }

            if (kill.Part >= killer.PartCount || kill.Part >= killerParts.Length || killerParts[kill.Part] == null)
            {
                absent = "killer part " + kill.Part.ToString(inv) + " is not in the frame";
                return false;
            }

            if (!TryBody(window, kill.Victim, out Phenotype victim, out Transform[] victimParts))
            {
                absent = "victim " + kill.Victim.ToString(inv) + " is not in the frame";
                return false;
            }

            if (!TryMouth(killer, killerParts, kill.Part, out Vector3 mouth, out radius))
            {
                absent = "killer part " + kill.Part.ToString(inv) + " has no usable mouth";
                return false;
            }

            // The victim part nearest the point, by signed distance: the measure for a row that names no part, and beside the named
            // part's as a check on it otherwise.
            float best = float.PositiveInfinity;
            int nearest = -1;

            for (int q = 0; q < victim.PartCount && q < victimParts.Length; q++)
            {
                if (victimParts[q] == null) continue;

                float d = SignedDistance(victim.Parts[q], victimParts[q], mouth);

                if (d < best)
                {
                    best = d;
                    nearest = q;
                }
            }

            if (nearest < 0)
            {
                absent = "victim " + kill.Victim.ToString(inv) + " has no part drawn";
                return false;
            }

            // The part the row says the bite took is the one measured; a row written before the part was recorded falls back to the nearest.
            int named = kill.VictimPart;
            if (named >= 0 && named < victim.PartCount && named < victimParts.Length && victimParts[named] != null)
            {
                victimPart = named;
                gap = SignedDistance(victim.Parts[named], victimParts[named], mouth);
                nearestNote = named == nearest ? " (the nearest part)" :
                    string.Format(inv, " (nearest part {0} at {1:0.0000} m)", nearest, best);
            }
            else
            {
                victimPart = nearest;
                gap = best;
                nearestNote = named >= 0 ? " (the row's part " + named.ToString(inv) + " is not drawn; nearest part)" : " (the row names no part; nearest part)";
            }

            nearestNote += BallNote(killer.Parts[kill.Part], killerParts[kill.Part], victim.Parts[victimPart], victimParts[victimPart]);

            return true;
        }

        // The mouth point of one part as the theatre carries it (TheatreMouth2.Paint, TheatreSoloStill.LogMouthGeometry): Core's point in
        // the part's frame, taken through the part's transform as drawn, and TheatreMouth2's own radius. False where the part has no
        // usable mouth. The kill-contact log and the ate-contact log both read it.
        private static bool TryMouth(Phenotype body, Transform[] parts, int part, out Vector3 mouth, out float radius)
        {
            mouth = default;
            radius = float.NaN;

            TheatreMouth2.Measured measured = TheatreMouth2.Measure(body, part);
            if (!measured.Valid) return false;

            Float3 tip = MouthPoint.Local(body, part);
            mouth = parts[part].TransformPoint(new Vector3(tip.X, tip.Y, tip.Z));
            radius = measured.RadiusMetres;
            return true;
        }

        // The farm's own test under contactShape Spheres: the two parts' balls (each centred on its part, of the part's reach, as
        // Evosim.Dynamics' Creature.BuildReach sets it) overlap when the centres stand nearer than the reaches' sum. The words "killer"
        // and "victim" stand for the first part and the second in both logs, so one pattern reads both.
        private static string BallNote(PhenotypePart killerPart, Transform killerAt, PhenotypePart victimPart, Transform victimAt)
        {
            float centres = Vector3.Distance(killerAt.position, victimAt.position);
            float killerReach = Reach(killerPart);
            float victimReach = Reach(victimPart);
            float reaches = killerReach + victimReach;
            Float3 kh = killerPart.HalfExtents;
            Float3 vh = victimPart.HalfExtents;
            return string.Format(CultureInfo.InvariantCulture,
                "; balls: centres {0:0.0000} m, reaches {1:0.0000} m, {2} by {3:0.0000} m; killer part reach {4:0.0000} m half-extents {5:0.000} {6:0.000} {7:0.000}; victim part reach {8:0.0000} m half-extents {9:0.000} {10:0.000} {11:0.000}",
                centres, reaches, centres < reaches ? "overlap" : "apart", Mathf.Abs(reaches - centres),
                killerReach, Mathf.Abs(kh.X), Mathf.Abs(kh.Y), Mathf.Abs(kh.Z), victimReach, Mathf.Abs(vh.X), Mathf.Abs(vh.Y), Mathf.Abs(vh.Z));
        }

        // A part's contact ball radius under sphere contact: a family part's solid reach at its size, any other part's half-diagonal.
        private static float Reach(PhenotypePart part)
        {
            if (part.Family != null) return (float)(part.Family.ReferenceSolidReach * part.FamilyScale);

            Float3 h = part.HalfExtents;
            return Mathf.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);
        }

        // The signed distance from a world point to a part's surface, from the part's drawn transform (unit scale, at its centre, in its
        // own axes) and its half extents; negative inside. A box is exact. A capsule is the distance to its axis segment (along the part's y,
        // as CapsuleShape draws it) less its radius. A sphere, an ellipsoid and a superellipsoid are taken as a ball of the largest half
        // extent (centre distance less that radius), which overstates the surface along the shorter axes.
        private static float SignedDistance(PhenotypePart part, Transform at, Vector3 world)
        {
            Vector3 p = at.InverseTransformPoint(world);
            Float3 h = part.HalfExtents;
            var half = new Vector3(Mathf.Abs(h.X), Mathf.Abs(h.Y), Mathf.Abs(h.Z));

            if (part.ShapeId == ShapeIds.Box)
            {
                var q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - half;
                float outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
                return outside + inside;
            }

            if (part.ShapeId == ShapeIds.Capsule)
            {
                float span = CapsuleShape.HalfSpan(h);
                var axis = new Vector3(0f, Mathf.Clamp(p.y, -span, span), 0f);
                return (p - axis).magnitude - CapsuleShape.Radius(h);
            }

            return p.magnitude - Mathf.Max(half.x, Mathf.Max(half.y, half.z));
        }

        // ---------------------------------------------------------------- the ate-contact log

        // One ate-bit onset of a film's subject: the eating part whose bit set, and when (world seconds). An onset that a kill row of the
        // window matches (the same killer and part, within one pose frame's interval) carries the victim: a kill (root 1) swallows its
        // victim in the pass that sets the bit, so the victim is gone from the onset frame and is measured at the frame before it
        // (Match 1); a limb cut (root 0) leaves the victim and is measured at both (Match 2); a wound has no kill row and is measured at
        // the onset frame alone (Match 0).
        private sealed class AteRow
        {
            public double T;
            public long Eater;
            public int Part;

            /// <summary>0 no kill row (a wound), 1 a kill (the whole body died), 2 a limb cut (the body lives on).</summary>
            public int Match;

            /// <summary>With a match: the kill row's victim id and the victim part it took (-1 where the row names none).</summary>
            public long Victim = -1L;
            public int VictimPart = -1;

            /// <summary>With a match: the pose frame before the onset's nearest frame, or -1 where the onset's is the film's first.</summary>
            public int PrevFrame = -1;

            /// <summary>The onset - 1 measure has been tried (shown and measured, or found absent, or found not shown).</summary>
            public bool PrevDone;

            /// <summary>The onset - 1 measure found the victim; its figures wait for the onset frame, which gives the eater's motion.</summary>
            public bool PrevHave;

            /// <summary>With <see cref="PrevHave"/>: the line up to the verdict, the true-shape surface gap, the eater part's centre, the victim part's kind.</summary>
            public string PrevText;
            public double PrevGap;
            public Vector3 PrevEater;
            public string PrevKind;

            /// <summary>The onset - 1 verdict once the onset frame has been shown: 0 none yet, 1 far, 2 touch within a frame's motion, 3 touch.</summary>
            public int PrevVerdict;

            /// <summary>The ate-contact line has been written at the onset's nearest frame.</summary>
            public bool Done;

            /// <summary>The smallest distance in seconds between the onset and a frame shown so far.</summary>
            public double Best = double.PositiveInfinity;

            /// <summary>0 not yet known, 1 a subject, as the film names it; read when the eater is first in a frame.</summary>
            public int Subject;

            /// <summary>A line has been written for the onset (at its nearest frame or within the edge); it is the last one that counts.</summary>
            public bool Logged;

            /// <summary>The last line found a nearest part (false: the line says absent).</summary>
            public bool Measured;

            /// <summary>With <see cref="Measured"/>: the nearest part's kind, "leaf", "animal", "corpse" or "remains".</summary>
            public string Kind;

            /// <summary>With <see cref="Measured"/>: the verdict was touch (false: far).</summary>
            public bool Touch;
        }

        private static FilmWindowWorld _atesWindow;
        private static List<AteRow> _ates;

        // The window's summary line has been written for the pass in hand (AteContactAll only).
        private static bool _ateReported;

        // The subject's ate-bit onsets of a window, read once per window: what ReadAteBits adds, at the speed the film plays. Empty with no
        // film subject named, a subject that is neither a body id nor "leaf", or a window whose pose stream records no ate bit.
        private static List<AteRow> AtesOf(FilmWindowWorld window)
        {
            if (ReferenceEquals(window, _atesWindow) && _ates != null) return _ates;

            var inv = CultureInfo.InvariantCulture;

            // A window that is replaced before its summary was written gets it now, with what was measured.
            if (_ates != null && AteContactAll && !_ateReported) ReportAteWindow(_ates, "the window was replaced");

            _atesWindow = window;
            _ates = new List<AteRow>();
            _ateReported = false;

            bool everyone = AteContactAll;
            bool leaf = FilmSubject == "leaf";
            long wanted = -1L;
            if (!everyone && (FilmSubject.Length == 0 || (!leaf && !long.TryParse(FilmSubject, NumberStyles.Integer, inv, out wanted)))) return _ates;

            FilmWindowReader reader = window.Window;
            if (!(reader.RecordsBites && reader.FrameCount > 0))
            {
                Debug.Log("[Theatre] ate contact window: the pose stream records no ate bit, so no ate-contact lines.");
                return _ates;
            }

            var all = new List<(long Id, int Part, double Second)>();
            ReadAteBits(window, Math.Max(1d, window.Speed), out string note, all);

            for (int i = 0; i < all.Count; i++)
            {
                if (!everyone && !leaf && all[i].Id != wanted) continue;
                _ates.Add(new AteRow { T = all[i].Second, Eater = all[i].Id, Part = all[i].Part, Subject = !everyone && leaf ? 0 : 1 });
            }

            MatchAteKills(window, _ates);

            if (everyone)
            {
                Debug.Log(string.Format(inv, "[Theatre] ate contact window: {0} ate-bit onsets of every body in the window, {1} (t {2:0} to {3:0} s, {4}).",
                    _ates.Count, "the close shot's subject is " + (FilmSubject.Length > 0 ? FilmSubject : "unset"), window.FirstSecond, window.LastSecond, note));
                return _ates;
            }

            Debug.Log(string.Format(inv, "[Theatre] ate contact window: {0} ate-bit onsets of the subject {1} (t {2:0} to {3:0} s, {4}).",
                _ates.Count, FilmSubject, window.FirstSecond, window.LastSecond, note));

            return _ates;
        }

        // Gives each onset its kill row where one stands: the same killer and part, and a time within one pose frame's interval of the
        // onset's (the bit is set at the frame after the bite's step, the kill row written at the step). The nearest such row in time
        // wins. A kill (root 1) is then measured at the frame before the onset's nearest, where its victim is still drawn; a row with no
        // root (written before it) is taken as a cut, which is measured at both frames and so loses nothing.
        private static void MatchAteKills(FilmWindowWorld window, List<AteRow> rows)
        {
            List<KillRow> kills = KillsOf(window);
            FilmWindowReader reader = window.Window;
            if (kills.Count == 0 || reader.FrameCount == 0) return;

            for (int i = 0; i < rows.Count; i++)
            {
                AteRow row = rows[i];
                int at = reader.NearestFrame(row.T);

                double interval = at >= 1 ? reader.SecondOf(at) - reader.SecondOf(at - 1)
                    : reader.FrameCount > 1 ? reader.SecondOf(1) - reader.SecondOf(0) : 0d;

                KillRow best = null;
                double bestOff = double.PositiveInfinity;

                for (int j = 0; j < kills.Count; j++)
                {
                    KillRow kill = kills[j];
                    if (kill.Killer != row.Eater || kill.Part != row.Part) continue;

                    double off = Math.Abs(kill.T - row.T);
                    if (off > interval + 1e-9 || off >= bestOff) continue;

                    best = kill;
                    bestOff = off;
                }

                if (best == null) continue;

                row.Match = best.Root == 1 ? 1 : 2;
                row.Victim = best.Victim;
                row.VictimPart = best.VictimPart;
                row.PrevFrame = at - 1;
            }
        }

        // With "leaf" as the subject, an eater is a subject when its body carries a leaf; read once the body is in a frame.
        private static bool IsAteSubject(FilmWindowWorld window, AteRow row)
        {
            if (row.Subject != 0) return row.Subject > 0;
            if (!TryBody(window, row.Eater, out Phenotype sized, out Transform[] unused)) return false;

            row.Subject = CarriesLeaf(sized) ? 1 : -1;
            return row.Subject > 0;
        }

        // Called by LogContacts after the kill rows, with the same rules: each ate-bit onset of the subject is logged once at its nearest
        // frame, after the recorded pose is applied, and at each shown frame within ContactEdgeSeconds of it that is nearer than any shown
        // so far when a clip never shows its nearest. A later scene that reopens the window logs every onset afresh. An onset that a kill
        // row matches (MatchAteKills) is measured against its victim at the frame before the nearest, since a killing bite swallows the
        // victim in the pass that sets the bit; that line is written at the onset frame, which gives the eater's motion between them.
        private static void LogAteContacts(FilmWindowWorld window, double shown, bool reopened)
        {
            List<AteRow> ates = AtesOf(window);
            if (ates.Count == 0) return;

            if (reopened)
            {
                if (AteContactAll && !_ateReported) ReportAteWindow(ates, "a later scene reopened the window");
                _ateReported = false;

                for (int i = 0; i < ates.Count; i++)
                {
                    ates[i].Done = false;
                    ates[i].Logged = false;
                    ates[i].Measured = false;
                    ates[i].Best = double.PositiveInfinity;
                    ates[i].PrevDone = false;
                    ates[i].PrevHave = false;
                    ates[i].PrevText = null;
                    ates[i].PrevKind = null;
                    ates[i].PrevVerdict = 0;
                }
            }

            for (int i = 0; i < ates.Count; i++)
            {
                AteRow row = ates[i];
                if (row.Done || row.Subject < 0) continue;

                double gap = Math.Abs(shown - row.T);

                // A match is measured at the frame before the onset's nearest, which supersedes any line at or before it.
                if (row.Match != 0 && window.FrameIndex <= row.PrevFrame)
                {
                    if (window.FrameIndex == row.PrevFrame && !row.PrevDone && IsAteSubject(window, row)) MeasureAtePrev(window, row);
                    continue;
                }

                if (window.Window.NearestFrame(row.T) == window.FrameIndex)
                {
                    if (!IsAteSubject(window, row)) continue;

                    row.Done = true;
                    row.Best = gap;
                    LogAte(window, row, "");
                }
                else if (gap <= ContactEdgeSeconds && gap < row.Best)
                {
                    if (!IsAteSubject(window, row)) continue;

                    row.Best = gap;
                    LogAte(window, row, " [within " + ContactEdgeSeconds.ToString("0.#", CultureInfo.InvariantCulture) +
                                        " s of the onset, not its nearest frame]");
                }
            }

            // With every body measured, the window's summary comes once each onset has had its nearest frame.
            if (AteContactAll && !_ateReported)
            {
                bool finished = true;

                for (int i = 0; i < ates.Count && finished; i++) finished = ates[i].Done;

                if (finished)
                {
                    _ateReported = true;
                    ReportAteWindow(ates, "every onset reached its nearest frame");
                }
            }
        }

        private static readonly string[] AteKinds = { "leaf", "animal", "corpse", "remains" };

        // An onset's verdict from its last lines: a kill by its onset - 1 measure where it has one, a cut by touch at either frame, a wound
        // (and a match whose onset - 1 measure found nothing) by the onset frame. "within" marks a touch that only the eater's motion
        // between the two frames reaches; it counts as touch.
        private static void ResolveAte(AteRow row, out bool measured, out bool touch, out bool within, out string kind)
        {
            bool prev = row.PrevVerdict > 0;
            bool onset = row.Measured;
            measured = prev || onset;
            touch = false;
            within = false;
            kind = row.Kind;

            if (prev && row.Match == 1)
            {
                touch = row.PrevVerdict >= 2;
                within = row.PrevVerdict == 2;
                kind = row.PrevKind;
            }
            else if (prev)
            {
                if (onset && row.Touch) touch = true;
                else if (row.PrevVerdict >= 2)
                {
                    touch = true;
                    within = row.PrevVerdict == 2;
                    kind = row.PrevKind;
                }
                else if (!onset) kind = row.PrevKind;
            }
            else
            {
                touch = onset && row.Touch;
            }
        }

        // The window's summary: the onsets measured, and how many came out touch and far, by kind of the nearest part, then by match
        // (kill, cut, wound) and how many touches were within a frame's motion. Counts each onset by its last lines. Writes nothing when no
        // onset was logged.
        private static void ReportAteWindow(List<AteRow> rows, string why)
        {
            var inv = CultureInfo.InvariantCulture;
            var touch = new int[AteKinds.Length];
            var far = new int[AteKinds.Length];
            var matchTouch = new int[3];
            var matchFar = new int[3];
            int logged = 0, absent = 0, touches = 0, fars = 0, withins = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                AteRow row = rows[i];
                if (!row.Logged) continue;

                logged++;

                ResolveAte(row, out bool measured, out bool isTouch, out bool within, out string rowKind);

                if (!measured)
                {
                    absent++;
                    continue;
                }

                int k = Array.IndexOf(AteKinds, rowKind);
                if (k < 0) k = 0;

                if (isTouch) { touch[k]++; touches++; matchTouch[row.Match]++; if (within) withins++; }
                else { far[k]++; fars++; matchFar[row.Match]++; }
            }

            if (logged == 0) return;

            string Split(int[] counts)
            {
                var named = new string[AteKinds.Length];
                for (int j = 0; j < named.Length; j++) named[j] = AteKinds[j] + " " + counts[j].ToString(inv);
                return string.Join(", ", named);
            }

            Debug.Log(string.Format(inv,
                "[Theatre] ate contact window: onsets measured {0} of {1} ({2}): touch {3} ({4}), far {5} ({6}), absent {7}; " +
                "by match: kill touch {8} far {9}, cut touch {10} far {11}, wound touch {12} far {13}; touches within a frame's motion {14} of {3}.",
                logged, rows.Count, why, touches, Split(touch), fars, Split(far), absent,
                matchTouch[1], matchFar[1], matchTouch[2], matchFar[2], matchTouch[0], matchFar[0], withins));
        }

        private static string AteHead(AteRow row)
        {
            return string.Format(CultureInfo.InvariantCulture, "[Theatre] ate contact: t {0:0.###} eater {1} part {2} prey ", row.T, row.Eater, row.Part);
        }

        private static string AteMatchWord(AteRow row)
        {
            return row.Match == 1 ? "kill" : "cut";
        }

        // The onset - 1 measure, taken at that frame: the eating part against the matched victim's parts alone. A found victim waits for
        // the onset frame, which says how far the eater moved; a victim not found is logged absent now.
        private static void MeasureAtePrev(FilmWindowWorld window, AteRow row)
        {
            var inv = CultureInfo.InvariantCulture;
            row.PrevDone = true;

            if (TryAteContact(window, row, true, out string text, out string absent, out double surfaceGap, out Vector3 eaterAt, out string kind))
            {
                row.PrevHave = true;
                row.PrevText = text;
                row.PrevGap = surfaceGap;
                row.PrevEater = eaterAt;
                row.PrevKind = kind;
                return;
            }

            Debug.Log(AteHead(row) + row.Victim.ToString(inv) + " part ? (" + AteMatchWord(row) + ") victim at onset - 1 absent: " + absent +
                      string.Format(inv, " frame {0} (window sec {1:0.###})", window.FrameIndex, window.Second));
        }

        // Writes the onset - 1 line once the onset frame (or a later one) is shown: the displacement of the eating part's centre since the
        // onset - 1 frame, and the verdict. The bite fell at some physics step between the two frames, so a surface gap the displacement
        // covers is a touch within a frame's motion.
        private static void FinishAtePrev(FilmWindowWorld window, AteRow row, string note)
        {
            var inv = CultureInfo.InvariantCulture;
            double moved = double.NaN;

            if (TryBody(window, row.Eater, out Phenotype eater, out Transform[] eaterParts) && row.Part >= 0 && row.Part < eaterParts.Length &&
                eaterParts[row.Part] != null)
            {
                moved = Vector3.Distance(row.PrevEater, eaterParts[row.Part].position);
            }

            double reach = double.IsNaN(moved) ? 0d : moved;
            row.PrevVerdict = row.PrevGap < 0d ? 3 : row.PrevGap <= reach ? 2 : 1;

            string verdict = row.PrevVerdict == 3 ? "touch" : row.PrevVerdict == 2 ? "touch within a frame's motion" : "far";
            string movedText = double.IsNaN(moved)
                ? "; eater part not in the onset frame"
                : string.Format(inv, "; eater part moved {0:0.0000} m to the onset frame", moved);

            Debug.Log(AteHead(row) + row.PrevText + movedText + ": " + verdict +
                      string.Format(inv, " frame {0} to {1} (window sec {2:0.###} to {3:0.###}){4}", row.PrevFrame, window.FrameIndex,
                          window.Window.SecondOf(row.PrevFrame), window.Second, note));
        }

        private static void LogAte(FilmWindowWorld window, AteRow row, string note)
        {
            var inv = CultureInfo.InvariantCulture;
            row.Logged = true;

            // A match was measured at the frame before the onset's nearest; this frame follows it and finishes that line.
            if (row.Match != 0 && window.FrameIndex > row.PrevFrame)
            {
                if (!row.PrevDone)
                {
                    row.PrevDone = true;
                    Debug.Log(AteHead(row) + row.Victim.ToString(inv) + " part ? (" + AteMatchWord(row) + ") victim at onset - 1 absent: " +
                              (row.PrevFrame < 0 ? "the onset is the film's first frame" : "the onset - 1 frame (" + row.PrevFrame.ToString(inv) + ") was not shown") +
                              string.Format(inv, " frame {0} (window sec {1:0.###})", window.FrameIndex, window.Second));
                }
                else if (row.PrevHave && row.PrevVerdict == 0)
                {
                    FinishAtePrev(window, row, note);
                }

                // A kill's victim is gone from the onset frame, so its onset-frame measure would read some other body; it stands only
                // where the onset - 1 measure found nothing.
                if (row.Match == 1 && row.PrevVerdict > 0) return;
                if (row.PrevVerdict == 0) note += " [onset frame only]";
            }

            row.Measured = false;
            string head = AteHead(row);
            string tail = string.Format(inv, " frame {0} (window sec {1:0.###}){2}", window.FrameIndex, window.Second, note);

            if (!TryAteContact(window, row, false, out string text, out string absent, out _, out _, out _))
            {
                Debug.Log(head + "? part ? absent: " + absent + tail);
                return;
            }

            Debug.Log(head + text + tail);
        }

        // A part's convex core as the farm's true-shape contact has it (D228, D233): the core's half-extents or hull and its margin from
        // Evosim.Dynamics' ConvexCore, its turn and centre from the part's drawn transform (unit scale, at its centre, in its own axes).
        private struct PartCore
        {
            public DynVec Half;
            public double[] Hull;
            public double Margin;
            public DynMat Turn;
            public DynVec Centre;
        }

        private static bool TryCore(FilmWindowWorld window, PhenotypePart part, Transform at, out PartCore core)
        {
            core = default;

            try
            {
                DynVec half;
                double margin;
                double[] hull = null;

                if (part.Family != null)
                {
                    hull = new double[3 * Superellipsoid.ContactCorePoints];
                    DynCore.OfFamily(part, hull, out half, out margin);
                }
                else
                {
                    DynCore.Of(window.Record.Config.Shapes.Resolve(part.ShapeId), part.HalfExtents, out half, out margin);
                }

                Vector3 r = at.right, u = at.up, f = at.forward;
                Vector3 c = at.position;

                core = new PartCore
                {
                    Half = half,
                    Hull = hull,
                    Margin = margin,
                    Turn = new DynMat(r.x, u.x, f.x, r.y, u.y, f.y, r.z, u.z, f.z),
                    Centre = new DynVec(c.x, c.y, c.z),
                };

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The distance between two cores in the pair's own frame (the first at the origin, as the farm's pair routine is called), 0 where
        // they meet. True where they are apart.
        private static bool CoresApart(PartCore a, PartCore b, out double distance)
        {
            bool apart = DynCore.Distance(
                a.Half, a.Hull, a.Turn, DynVec.Zero,
                b.Half, b.Hull, b.Turn, b.Centre - a.Centre,
                out distance, out _, out _);

            if (!apart) distance = 0d;
            return apart;
        }

        // The distance from a world point to a core, the point taken as a core of its own (no extent, no margin). True where apart.
        private static bool PointApart(Vector3 world, PartCore core, out double distance)
        {
            bool apart = DynCore.Distance(
                DynVec.Zero, null, DynMat.Identity, new DynVec(world.x, world.y, world.z) - core.Centre,
                core.Half, core.Hull, core.Turn, DynVec.Zero,
                out distance, out _, out _);

            if (!apart) distance = 0d;
            return apart;
        }

        // A bound on how far a part's core and margin reach from its centre: the family's reach (every core point plus the margin, and
        // every point of the solid), else the half-diagonal, which holds a rounded box. Only a prefilter for the nearest-part search.
        private static float BoundRadius(PhenotypePart part)
        {
            if (part.Family != null) return (float)(part.Family.ReferenceReach * part.FamilyScale);

            Float3 h = part.HalfExtents;
            return Mathf.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);
        }

        // The farm's measure for one contact line under MouthTrueLevel (D275's open point 2). At 1 the line's gap, radius and ratio become
        // the farm's (TryFarmRatio), and the note names its pair. At 2 the drawn triple stays and the note carries the farm's ratio and the
        // farm's measure on the line's own mouth part and part (TryPairRatio): their gap from the drawn ratio is the measure, and the
        // farm's from the row's pair is the nearest pair. Where nothing could be measured the line is left as it is.
        private static void FarmNote(FilmWindowWorld window, Phenotype mouthBody, Transform[] mouthParts, int rowMouth, Phenotype other,
            Transform[] otherParts, int rowPart, ref float shownGap, ref float shownRadius, ref float shownRatio, out string note)
        {
            var inv = CultureInfo.InvariantCulture;
            note = "";

            bool farm = TryFarmRatio(window, mouthBody, mouthParts, other, otherParts,
                out float farmRatio, out float farmGap, out float farmRadius, out int farmMouthPart, out int farmTargetPart);

            string pair = farmMouthPart >= 0
                ? string.Format(inv, "mouth part {0} to part {1}", farmMouthPart, farmTargetPart)
                : "no part within the mouth's reach";

            if (MouthTrueLevel == 1)
            {
                if (!farm) return;
                shownGap = farmGap;
                shownRadius = farmRadius;
                shownRatio = farmRatio;
                note = " (the farm's measure, D275: " + pair + ")";
                return;
            }

            string own = TryPairRatio(window, mouthBody, mouthParts, rowMouth, other, otherParts, rowPart, out float ownRatio)
                ? string.Format(inv, "gap/radius {0:0.###}", ownRatio)
                : "not measured";

            note = (farm ? string.Format(inv, " (the farm's measure, D275: gap/radius {0:0.###}, {1}", farmRatio, pair) : " (the farm's measure, D275: not measured") +
                   "; the row's pair on the farm's measure: " + own + ")";
        }

        // The farm's measure on one mouth part and one part of the other body: the mouth point's distance to the part's core less its
        // margin, over MouthPoint.Radius. False where the mouth part has no radius (not an eating part) or either part is not drawn.
        private static bool TryPairRatio(FilmWindowWorld window, Phenotype mouthBody, Transform[] mouthParts, int mouthPart, Phenotype other,
            Transform[] otherParts, int part, out float ratio)
        {
            ratio = float.NaN;

            if (mouthPart < 0 || mouthPart >= mouthBody.PartCount || mouthPart >= mouthParts.Length || mouthParts[mouthPart] == null) return false;
            if (part < 0 || part >= other.PartCount || part >= otherParts.Length || otherParts[part] == null) return false;

            float r = MouthPoint.Radius(mouthBody, mouthPart);
            if (!(r > 0f)) return false;

            if (!TryCore(window, other.Parts[part], otherParts[part], out PartCore core)) return false;

            Float3 local = MouthPoint.Local(mouthBody, mouthPart);
            Vector3 point = mouthParts[mouthPart].TransformPoint(new Vector3(local.X, local.Y, local.Z));
            bool apart = PointApart(point, core, out double d);
            ratio = (float)((apart ? d - core.Margin : -core.Margin) / r);
            return true;
        }

        // The farm's measure of a bite (Evosim.Dynamics' MouthTouch.Reach, D275) on the drawn poses: the smallest gap / radius over every
        // eating part of the mouth body and every part of the other, the gap being the mouth point's distance to the part's core less its
        // margin (-margin inside the core). The earlier pair wins a tie, as there. False where no eating part has a usable radius; with
        // every part out of reach the ratio is +infinity, the gap NaN and the radius the first usable one, as there.
        private static bool TryFarmRatio(FilmWindowWorld window, Phenotype mouthBody, Transform[] mouthParts, Phenotype other, Transform[] otherParts,
            out float ratio, out float gap, out float radius, out int mouthPart, out int targetPart)
        {
            ratio = float.PositiveInfinity;
            gap = float.NaN;
            radius = float.NaN;
            mouthPart = -1;
            targetPart = -1;

            bool any = false;
            float firstRadius = float.NaN;

            for (int i = 0; i < mouthBody.PartCount && i < mouthParts.Length; i++)
            {
                if (mouthParts[i] == null || !MouthPoint.IsEatingPart(mouthBody.Parts[i])) continue;

                float r = MouthPoint.Radius(mouthBody, i);
                if (!(r > 0f)) continue;

                if (!any) firstRadius = r;
                any = true;

                Float3 local = MouthPoint.Local(mouthBody, i);
                Vector3 point = mouthParts[i].TransformPoint(new Vector3(local.X, local.Y, local.Z));

                for (int q = 0; q < other.PartCount && q < otherParts.Length; q++)
                {
                    if (otherParts[q] == null) continue;

                    if (Vector3.Distance(point, otherParts[q].position) > BoundRadius(other.Parts[q]) + r) continue;
                    if (!TryCore(window, other.Parts[q], otherParts[q], out PartCore core)) continue;

                    bool apart = PointApart(point, core, out double d);
                    double g = apart ? d - core.Margin : -core.Margin;
                    float rt = (float)(g / r);

                    if (!(rt < ratio)) continue;

                    ratio = rt;
                    gap = (float)g;
                    radius = r;
                    mouthPart = i;
                    targetPart = q;
                }
            }

            if (!any)
            {
                ratio = float.NaN;
                return false;
            }

            if (mouthPart < 0) radius = firstRadius;
            return true;
        }

        // One body the nearest-part search may take a part of: a live body, a corpse or a remains body the window draws in the frame.
        private struct AteCandidate
        {
            public long Id;
            public Phenotype Body;
            public Transform[] Parts;

            /// <summary>Null for a live body (its part's cell type names the kind), else "corpse" or "remains".</summary>
            public string Kind;
        }

        private static FilmWindowWorld _candidatesWindow;
        private static long _candidatesShown = -1L;
        private static readonly List<AteCandidate> Candidates = new List<AteCandidate>();
        private static readonly List<FilmWindowCorpsePlace> CandidateCorpses = new List<FilmWindowCorpsePlace>();

        // Every body the frame on screen draws, from the three views TryBody reads: the live bodies by the frame's list, the corpses by
        // the places the reader gives for the second shown (a corpse is keyed in its view as FilmWindowWorld.CorpseKey keys it, the
        // negative of its id less one), and the remains by the frame's bodies that carry the remains bit (FilmWindowWorld.Show draws
        // exactly those in the remains view). Built once per shown frame, since a frame often holds several onsets.
        private static List<AteCandidate> AteCandidatesOf(FilmWindowWorld window)
        {
            if (ReferenceEquals(window, _candidatesWindow) && window.FramesShown == _candidatesShown) return Candidates;

            _candidatesWindow = window;
            _candidatesShown = window.FramesShown;
            Candidates.Clear();

            if (window.View != null)
            {
                for (int i = 0; i < window.BodyCount; i++)
                {
                    long id = window.IdAt(i);
                    if (!window.View.TryBody(id, out Phenotype sized, out Transform[] parts)) continue;

                    Candidates.Add(new AteCandidate { Id = id, Body = sized, Parts = parts, Kind = null });
                }
            }

            if (window.CorpseView != null)
            {
                window.Window.CorpsesAt(window.Second, CandidateCorpses);

                for (int i = 0; i < CandidateCorpses.Count; i++)
                {
                    long id = CandidateCorpses[i].Corpse.Id;
                    if (!window.CorpseView.TryBody(-(id + 1L), out Phenotype sized, out Transform[] parts)) continue;

                    Candidates.Add(new AteCandidate { Id = id, Body = sized, Parts = parts, Kind = "corpse" });
                }
            }

            if (window.RemainsView != null && window.FrameIndex >= 0)
            {
                PoseBody[] bodies = window.Window.ReadFrame(window.FrameIndex).Bodies;

                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i].Flags < 0 || (bodies[i].Flags & PoseStream.RemainsBit) == 0) continue;
                    if (!window.RemainsView.TryBody(bodies[i].Id, out Phenotype sized, out Transform[] parts)) continue;

                    Candidates.Add(new AteCandidate { Id = bodies[i].Id, Body = sized, Parts = parts, Kind = "remains" });
                }
            }

            return Candidates;
        }

        // The ate-contact measure for one onset. The mouth is the eating part's, as the kill line measures it. The prey is the part of any
        // other drawn body (live, corpse or remains) whose true shape (core plus margin, the farm's own contact shape) stands nearest the
        // eating part's, and its kind is "leaf" for a photosynthetic part of a live body, "animal" for its other parts, "corpse" or
        // "remains" for a part of a body from those views. The verdict is the farm's pair test: touch where the margins
        // exceed the cores' distance (the pair routine's penetration is positive), far otherwise. Seams are not unwrapped.
        //
        // With atPrev, the frame in hand is the one before the onset's nearest and the prey is the matched kill row's victim alone: a
        // kill swallows its victim in the pass that sets the bit, so at the onset frame the victim is gone and the nearest of every body
        // is some other one. It names the victim part nearest the eating part and whether that is the part the row took, leaves the
        // verdict to the caller (which adds the eater's motion to the onset frame) and returns the surface gap and the eating part's
        // centre; it sets nothing on the row.
        private static bool TryAteContact(FilmWindowWorld window, AteRow row, bool atPrev, out string text, out string absent,
            out double surfaceGap, out Vector3 eaterAt, out string preyKind)
        {
            var inv = CultureInfo.InvariantCulture;
            text = null;
            absent = null;
            surfaceGap = double.NaN;
            eaterAt = Vector3.zero;
            preyKind = null;

            if (!TryBody(window, row.Eater, out Phenotype eater, out Transform[] eaterParts))
            {
                absent = "eater " + row.Eater.ToString(inv) + " is not in the frame";
                return false;
            }

            if (row.Part < 0 || row.Part >= eater.PartCount || row.Part >= eaterParts.Length || eaterParts[row.Part] == null)
            {
                absent = "eater part " + row.Part.ToString(inv) + " is not in the frame";
                return false;
            }

            if (!TryMouth(eater, eaterParts, row.Part, out Vector3 mouth, out float radius))
            {
                absent = "eater part " + row.Part.ToString(inv) + " has no usable mouth";
                return false;
            }

            if (!TryCore(window, eater.Parts[row.Part], eaterParts[row.Part], out PartCore mine))
            {
                absent = "eater part " + row.Part.ToString(inv) + " has no true-shape core";
                return false;
            }

            Vector3 eaterCentre = eaterParts[row.Part].position;
            float eaterBound = BoundRadius(eater.Parts[row.Part]);

            Phenotype bestBody = null;
            Transform[] bestParts = null;
            PartCore bestCore = default;
            long bestId = -1L;
            int bestPart = -1;
            double bestGap = double.PositiveInfinity, bestDistance = 0d, bestMargins = 0d, bestCentres = double.PositiveInfinity;
            bool bestApart = false;
            int bodies = 0;
            string bestKind = null;

            // The live bodies, then the corpses and the remains the window draws (AteCandidatesOf), the eater left out.
            List<AteCandidate> candidates = AteCandidatesOf(window);

            for (int i = 0; i < candidates.Count; i++)
            {
                AteCandidate candidate = candidates[i];
                long id = candidate.Id;
                if (id == row.Eater || (atPrev && (id != row.Victim || candidate.Kind != null))) continue;

                Phenotype other = candidate.Body;
                Transform[] otherParts = candidate.Parts;

                bodies++;

                for (int q = 0; q < other.PartCount && q < otherParts.Length; q++)
                {
                    if (otherParts[q] == null) continue;

                    // The surface gap is at least the centres' distance less both bounds, so a part that cannot beat the best is skipped.
                    float centres = Vector3.Distance(eaterCentre, otherParts[q].position);
                    if (centres - eaterBound - BoundRadius(other.Parts[q]) > bestGap) continue;

                    if (!TryCore(window, other.Parts[q], otherParts[q], out PartCore core)) continue;

                    bool apart = CoresApart(mine, core, out double distance);
                    double margins = mine.Margin + core.Margin;
                    double gap = apart ? distance - margins : -margins;

                    if (gap < bestGap - 1e-12 || (gap <= bestGap + 1e-12 && centres < bestCentres))
                    {
                        bestGap = gap;
                        bestDistance = distance;
                        bestMargins = margins;
                        bestCentres = centres;
                        bestApart = apart;
                        bestBody = other;
                        bestParts = otherParts;
                        bestCore = core;
                        bestId = id;
                        bestPart = q;
                        bestKind = candidate.Kind;
                    }
                }
            }

            if (bestPart < 0)
            {
                absent = atPrev
                    ? (bodies == 0 ? "victim " + row.Victim.ToString(inv) + " is not drawn in the frame" : "victim " + row.Victim.ToString(inv) + " has no part with a true-shape core")
                    : bodies == 0 ? "no other body is drawn" : "no other body has a part with a true-shape core";
                return false;
            }

            PhenotypePart prey = bestBody.Parts[bestPart];

            // A live body's part is a leaf or an animal part by its cell type; a corpse's or a remains body's is that kind of body.
            string kind = bestKind ?? (prey.CellTypeId == CellTypeIds.Photosynthetic ? "leaf" : "animal");

            if (!atPrev)
            {
                row.Measured = true;
                row.Kind = kind;
                row.Touch = bestGap < 0d;
            }

            // The kill line's own fields: the mouth point to the prey part's surface as drawn, and the balls.
            float mouthToSurface = SignedDistance(prey, bestParts[bestPart], mouth);

            bool mouthApart = PointApart(mouth, bestCore, out double mouthCore);
            string mouthTrue = mouthApart
                ? (mouthCore - bestCore.Margin).ToString("0.0000", inv) + " m"
                : "inside the core";

            // At the onset frame: the part and its kind. At onset - 1: the match, then the kind and whether the part is the one the row took.
            string who = atPrev
                ? bestId.ToString(inv) + " part " + bestPart.ToString(inv) + " (" + AteMatchWord(row) + ") victim at onset - 1 (" + kind + "; " +
                  (row.VictimPart < 0 ? "the row names no part" : bestPart == row.VictimPart ? "the row's part" : "the row took part " + row.VictimPart.ToString(inv)) + ") "
                : bestId.ToString(inv) + " part " + bestPart.ToString(inv) + " (" + kind + ") ";

            float shownGap = mouthToSurface, shownRadius = radius, shownRatio = mouthToSurface / radius;
            string farmNote = "";

            if (MouthTrueLevel > 0)
            {
                FarmNote(window, eater, eaterParts, row.Part, bestBody, bestParts, bestPart,
                    ref shownGap, ref shownRadius, ref shownRatio, out farmNote);
            }

            text = who + string.Format(inv,
                "mouth-to-surface {0:0.0000} m mouth radius {1:0.0000} m (gap/radius {2:0.###})", shownGap, shownRadius, shownRatio) + farmNote +
                (atPrev ? " (victim's nearest part by true surface gap)" : " (nearest part by true surface gap)") +
                BallNote(eater.Parts[row.Part], eaterParts[row.Part], prey, bestParts[bestPart]) +
                string.Format(inv, "; true shape: core distance {0:0.0000} m{1}, margins {2:0.0000} m, surface gap {3:0.0000} m, mouth to true surface {4}",
                    bestDistance, bestApart ? "" : " (cores meet)", bestMargins, bestGap, mouthTrue) +
                (atPrev ? "" : ": " + (bestGap < 0d ? "touch" : "far"));

            surfaceGap = bestGap;
            eaterAt = eaterCentre;
            preyKind = kind;
            return true;
        }

        // The integer after a key in a row, up to the comma or brace that ends it.
        private static bool TryInt(string row, string key, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(row)) return false;

            int start = row.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return false;
            start += key.Length;

            int end = start;
            while (end < row.Length && row[end] != ',' && row[end] != '}') end++;

            if (end >= row.Length || end == start) return false;

            return int.TryParse(row.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        // ---------------------------------------------------------------- the mouths placed on screen

        // The close shot's subject (EVOSIM_THEATRE_FILM_SUBJECT: a body id, or "leaf"), read once.
        private static readonly string FilmSubject = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT") ?? "").Trim();

        /// <summary>
        /// True with the bite dial on and a film subject named (<c>EVOSIM_THEATRE_FILM_SUBJECT</c>): a film then calls
        /// <see cref="LogPlaced"/> once a frame. With either off nothing here runs.
        /// </summary>
        public static bool PlaceLog => TheatreMouth2.Bite > 0f && FilmSubject.Length > 0;

        // The shader's pulse (TheatreBody.shader, MouthBitePulse and its three constants), at a timer in film seconds. Kept beside the
        // shader's text; zero at the idle timer, as the shader's is.
        private static float ShaderPulse(float t)
        {
            const float duration = 0.35f, attack = 0.05f;
            float rise01 = Mathf.Clamp01(t / attack);
            float release = Mathf.Clamp01((t - attack) / (duration - attack));
            float rise = 1f - (1f - rise01) * (1f - rise01);
            float fall = 1f - release * release * release;
            return rise * fall;
        }

        /// <summary>
        /// One line per eating part of each subject body, for one film frame, where the film's camera puts its mouth:
        /// <c>[Theatre] bite frame F t=S body B mouth K px X Y r R pulse P facing D on-screen Y|N</c>. K is the part's index; X Y the
        /// mouth point (<see cref="TheatreMouth2.Measure"/>, which the shader's opening and the kill-contact log use too) in pixels from
        /// the top left; R its radius at that depth in pixels; P the strength times the shader's pulse at the timer the shader was given
        /// for that part this frame (<see cref="TimerOf"/>); D the dot product of the mouth's outward normal and the unit direction from
        /// the mouth to the camera.
        /// </summary>
        /// <remarks>
        /// The outward normal is the direction of the mouth point from its part's centre, in the part's own frame, taken through the part's
        /// drawn transform: <see cref="MouthPoint.Local"/> lies on the axis of the face the mouth is cut in (a box's face centre, a
        /// capsule's tip, a sphere's surface point along its direction), so that direction is the face's normal, and the disc and the lip
        /// are drawn about it. The pixels are the pinhole of the camera <c>SnapshotCamera.CapturePlaced</c> sets up (vertical field of view
        /// clamped to 5 to 120 degrees, the aspect of the picture); the grade's lens and its blur are not applied. On-screen means the
        /// whole disc lies inside the frame and its centre is in front of the near plane (0.05 m); off-screen, X Y and R read NaN.
        /// </remarks>
        public static void LogPlaced(FilmWindowWorld window, int filmFrame, Vector3 eye, Quaternion rotation, float fieldOfView, int width, int height)
        {
            if (!PlaceLog || window == null || width < 1 || height < 1) return;

            double speed = Math.Max(1d, window.Speed);

            // The onsets, as Frame reads them (the capture has normally read them already).
            if (!ReferenceEquals(window, _window))
            {
                _window = window;
                _onsets = ReadOnsets(window, speed);
            }

            bool leaf = FilmSubject == "leaf";
            long wanted = -1L;
            if (!leaf && !long.TryParse(FilmSubject, NumberStyles.Integer, CultureInfo.InvariantCulture, out wanted)) return;

            for (int i = 0; i < window.BodyCount; i++)
            {
                long id = window.IdAt(i);

                if (leaf ? !CarriesLeaf(window.PhenotypeOf(i)) : id != wanted) continue;

                LogMouthsOf(window, id, filmFrame, eye, rotation, fieldOfView, width, height, speed);
            }
        }

        private static bool CarriesLeaf(Phenotype body)
        {
            if (body == null) return false;

            for (int k = 0; k < body.PartCount; k++)
            {
                if (body.Parts[k].ShapeId == ShapeIds.Box && body.Parts[k].CellTypeId == CellTypeIds.Photosynthetic) return true;
            }

            return false;
        }

        /// <summary>A mouth's outward normal in the world: its measured point's direction from the part's centre, through the part's drawn transform.</summary>
        private static Vector3 MouthNormal(Transform part, Vector3 mouthPoint) =>
            part.TransformDirection(mouthPoint.normalized).normalized;

        /// <summary>
        /// The first eating part of a drawn body and its outward world normal, as the per-frame bite log reads it (so that log's
        /// <c>facing</c> reads near 1 for a camera looking back along the reverse of this normal). False when the body is not drawn
        /// or has no measurable eating part.
        /// </summary>
        public static bool TryFirstMouthNormal(LiveWorldView view, long id, out int part, out Vector3 normal)
        {
            part = -1;
            normal = Vector3.zero;
            if (view == null || !view.TryBody(id, out Phenotype sized, out Transform[] parts) || sized == null || parts == null) return false;

            for (int p = 0; p < sized.PartCount && p < parts.Length; p++)
            {
                if (parts[p] == null || !MouthPoint.IsEatingPart(sized.Parts[p])) continue;

                TheatreMouth2.Measured m = TheatreMouth2.Measure(sized, p);
                if (!m.Valid) continue;

                part = p;
                normal = MouthNormal(parts[p], m.Point);
                return true;
            }

            return false;
        }

        private static void LogMouthsOf(FilmWindowWorld window, long id, int filmFrame, Vector3 eye, Quaternion rotation,
            float fieldOfView, int width, int height, double speed)
        {
            if (!TryBody(window, id, out Phenotype sized, out Transform[] parts)) return;

            var inv = CultureInfo.InvariantCulture;
            float tanV = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Clamp(fieldOfView, 5f, 120f));
            float tanH = tanV * width / (float)height;
            Quaternion toView = Quaternion.Inverse(rotation);
            double clock = Time.timeAsDouble;

            for (int p = 0; p < sized.PartCount && p < parts.Length; p++)
            {
                if (parts[p] == null || !MouthPoint.IsEatingPart(sized.Parts[p])) continue;

                TheatreMouth2.Measured m = TheatreMouth2.Measure(sized, p);
                if (!m.Valid) continue;

                Vector3 world = parts[p].TransformPoint(m.Point);
                Vector3 normal = MouthNormal(parts[p], m.Point);

                Vector3 view = toView * (world - eye);
                bool front = view.z > 0.05f;
                float x = float.NaN, y = float.NaN, r = float.NaN;

                if (front)
                {
                    x = (0.5f + 0.5f * view.x / (view.z * tanH)) * width;
                    y = (0.5f - 0.5f * view.y / (view.z * tanV)) * height;
                    r = m.RadiusMetres / (view.z * tanV) * 0.5f * height;
                }

                bool on = front && x - r >= 0f && x + r <= width && y - r >= 0f && y + r <= height;

                Vector3 toCamera = eye - world;
                float distance = toCamera.magnitude;
                float facing = distance > 1e-6f ? Vector3.Dot(normal, toCamera / distance) : 0f;

                float timer = TimerOf(id, p, clock, _onsets, window.Second, speed);
                float pulse = TheatreMouth2.Bite * ShaderPulse(timer);

                Debug.Log(string.Format(inv,
                    "[Theatre] bite frame {0} t={1:0.###} body {2} mouth {3} px {4:0.#} {5:0.#} r {6:0.#} pulse {7:0.###} facing {8:0.###} on-screen {9}",
                    filmFrame, window.Second, id, p, x, y, r, pulse, facing, on ? "Y" : "N"));
            }
        }

        // ---------------------------------------------------------------- writing

        private static void WritePart(Body body, int k, float timer)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            var value = new Vector4(timer, TheatreMouth2.BiteIdle, TheatreMouth2.BiteIdle, TheatreMouth2.BiteIdle);

            MeshRenderer[] renderers = body.Renderers[k];

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetVector(TheatreMouth2.BiteId, value);
                renderer.SetPropertyBlock(_block);
            }
        }

        // EVOSIM_THEATRE_MOUTH2_GAPE: a part's share open, on its own renderers, in x.
        private static void WritePartGape(Body body, int k, float share)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            var value = new Vector4(share, 0f, 0f, 0f);

            MeshRenderer[] renderers = body.Renderers[k];

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetVector(TheatreMouth2.GapeId, value);
                renderer.SetPropertyBlock(_block);
            }
        }

        private static void WriteIdle(Body body)
        {
            for (int k = 0; k < body.Parts.Length; k++)
            {
                if (body.Last[k] == TheatreMouth2.BiteIdle) continue;

                WritePart(body, k, TheatreMouth2.BiteIdle);
                body.Last[k] = TheatreMouth2.BiteIdle;
            }

            // The gape shut, where it was ever written (only with its dial on).
            if (!(TheatreMouth2.Gape > 0f)) return;

            for (int k = 0; k < body.Parts.Length; k++)
            {
                if (body.LastGape[k] == 0f) continue;

                WritePartGape(body, k, 0f);
                body.LastGape[k] = 0f;
            }
        }

        // ---------------------------------------------------------------- finding a body's mouths

        private static Body Build(Transform root, Phenotype phenotype)
        {
            var parts = new List<int>();

            for (int p = 0; p < phenotype.PartCount; p++)
            {
                if (MouthPoint.IsEatingPart(phenotype.Parts[p])) parts.Add(p);
            }

            if (parts.Count == 0) return null;

            var lists = new List<MeshRenderer>[parts.Count];
            for (int k = 0; k < lists.Length; k++) lists[k] = new List<MeshRenderer>();

            var found = new List<MeshRenderer>();
            root.GetComponentsInChildren(true, found);

            for (int i = 0; i < found.Count; i++)
            {
                MeshRenderer renderer = found[i];
                if (renderer == null) continue;

                Transform host = renderer.transform.parent;
                int index = host != null ? PartIndexOf(host.name) : -1;
                int k = parts.IndexOf(index);
                if (k >= 0) lists[k].Add(renderer);
            }

            var body = new Body
            {
                Root = root,
                Phenotype = phenotype,
                Parts = parts.ToArray(),
                Renderers = new MeshRenderer[parts.Count][],
                Last = new float[parts.Count],
                LastGape = new float[parts.Count],
            };

            for (int k = 0; k < lists.Length; k++)
            {
                body.Renderers[k] = lists[k].ToArray();
                body.Last[k] = float.NaN;
                body.LastGape[k] = float.NaN;
            }

            return body;
        }

        // "Part07_n2" -> 7; anything else -> -1.
        private static int PartIndexOf(string name)
        {
            if (name == null || !name.StartsWith("Part", StringComparison.Ordinal)) return -1;

            int end = name.IndexOf('_');
            if (end < 0) end = name.Length;

            return int.TryParse(name.Substring(4, end - 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : -1;
        }
    }
}
