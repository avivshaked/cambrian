using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Evosim.Core;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// D279's birth cloud (<c>EVOSIM_THEATRE_BIRTH_CLOUD</c>): at a newborn's first drawn frame a small cloud of pale motes, like
    /// released eggs, sits about it and thins over <see cref="ThinSeconds"/> of film, drifting with the current, revealing a body that
    /// is drawn solid at its true size and place from that frame on. With it, the caption a film of one newborn carries
    /// (<see cref="TryCaption"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> <see cref="FilmWindowWorld"/> calls <see cref="Frame"/> only with the dial on, and
    /// the film asks for the caption only with it on. With it off no object, mesh, material, hook, collection or log line is made, and
    /// the thinning time's dial is not even read. The newborn's own drawing is left to the look (<c>EVOSIM_THEATRE_BIRTH_SECONDS=0</c>,
    /// <c>EVOSIM_THEATRE_BIRTH_SPECK</c> unset); nothing here touches it.
    /// </para>
    /// <para>
    /// <b>Which births.</b> The window's birth rows (<see cref="FilmWindowReader.TryBirthOf"/>), each at the first frame of the pass that
    /// draws the newborn. The rule is <c>FilmWindowWorld.Appear</c>'s: a newborn first drawn more than
    /// <see cref="CatchWorldSeconds"/> of the run after its birth row (a film that opens after the birth) gets no cloud, so a body born
    /// long before the first frame is not drawn arriving. A scene that plays the window again from earlier forgets the clouds, so each
    /// pass is drawn from its own frames.
    /// </para>
    /// <para>
    /// <b>A mote is a function of (newborn, index).</b> Every number of a mote (where in the ball, how far it spreads, its size, its
    /// moment of dissolving, its sink, wobble and shade) is hashed from the newborn's id and the mote's index, so a film draws the same
    /// cloud every time. The ball is centred on the box about the newborn's drawn parts at its first frame, its radius 1.0 to 1.5 times
    /// that box's largest half-extent; the count and the size follow the body's largest extent. A mote's place at a second is its place
    /// in the ball, a slow spread outward, the current at the ball's centre at the birth times its age in the run's seconds (as the bite
    /// crumbs take theirs), its sink, and a small wobble.
    /// </para>
    /// <para>
    /// <b>It thins, it does not flash.</b> The tint is a pale cream shaded by the flake shader's water light, and its brightness eases in
    /// over the first few film frames. Each mote dissolves (shrinks and dims together, smoothstepped to nothing) at its own hashed moment,
    /// most of them early, so the body shows through from the first frame, is mostly clear by about a second, and the last motes go at
    /// <see cref="ThinSeconds"/>. A time-lapse stretches it by the window's <see cref="FilmWindowWorld.Speed"/>, as <c>Appear</c> stretches
    /// its timing, so the cloud takes the same film time at any speed.
    /// </para>
    /// <para>
    /// <b>Drawn as the bite crumbs and the crumble's flakes are</b> (<c>TheatreMouthBiteCrumbs.cs</c>): one mesh of camera-facing quads
    /// rebuilt when a camera is about to render, shaded by <c>Evosim/Theatre Crumble Flakes</c> in the snow shells' water, drawn after the
    /// lens while the lens is armed, and turned off for the census views by <see cref="TheatreInsideOnly"/>. With no snow shells in the
    /// look nothing is drawn, and the log says so once.
    /// </para>
    /// <para>
    /// <b>Bubbles in place of the motes</b> (<see cref="TheatreBirthBubbles"/>, <c>EVOSIM_THEATRE_BIRTH_BUBBLES</c>, read only with this
    /// dial on). Each place that differs opens with <c>if (TheatreBirthBubbles.On)</c> and leaves the motes' lines below it as they
    /// were: the gathering adds the bubbles, the drawing writes them instead of the motes in the same mesh with their own shader, and
    /// the clouds are forgotten on the run's clock. The timing, the placement, the lead, the adoption, the catch and the caption are
    /// this class's and are shared.
    /// </para>
    /// </remarks>
    public static class TheatreBirthCloud
    {
        // ---------------------------------------------------------------- the dials

        /// <summary>The dial, <c>EVOSIM_THEATRE_BIRTH_CLOUD</c> (0 or 1, 0 by default), read once.</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_CLOUD", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The film seconds over which a cloud thins to nothing (<c>EVOSIM_THEATRE_BIRTH_CLOUD_SECONDS</c>, 2.5 by default, held to 0.5
        /// to 10). Read only with <see cref="On"/>.
        /// </summary>
        public static readonly float ThinSeconds = On ? TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_CLOUD_SECONDS", 2.5f, 0.5f, 10f) : 2.5f;

        /// <summary>
        /// The film seconds the cloud forms before the birth row's second (<c>EVOSIM_THEATRE_BIRTH_CLOUD_LEAD</c>, 0 by default, held to
        /// 0 to 3). Read only with <see cref="On"/>. At 0 nothing here exists: no forming cloud is made and every frame is the one drawn
        /// without the dial. Above 0 a few motes show about the place the newborn will be, gather to the full cloud by the birth row's
        /// second, and the newborn is drawn inside it, the cloud thinning from there as ever.
        /// </summary>
        public static readonly float Lead = On ? TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_CLOUD_LEAD", 0f, 0f, 3f) : 0f;

        /// <summary>The share of the motes showing at the lead's start, and how far out (as a share of its ball offset) an unformed mote waits.</summary>
        private const float FormFrom = 0.15f;

        private const float FormOut = 0.3f;

        /// <summary>The newborn's extent as a share of its parent's, used to size a forming cloud before the newborn exists (a third of the tissue, cube-rooted), and the least extent it takes, metres.</summary>
        private const float KinShare = 0.69f;

        private const float KinLeast = 0.02f;

        // ---------------------------------------------------------------- the constants

        /// <summary>A newborn first drawn more than this many seconds of the run after its birth row gets no cloud (<c>Appear</c>'s rule).</summary>
        public const double CatchWorldSeconds = 1.0d;

        /// <summary>The count of motes: <see cref="CountLow"/> for a body of <see cref="ExtentLow"/> metres or less, up to <see cref="CountHigh"/> at <see cref="ExtentHigh"/>.</summary>
        private const int CountLow = 30;

        private const int CountHigh = 80;
        private const float ExtentLow = 0.03f;
        private const float ExtentHigh = 0.3f;

        /// <summary>A mote's diameter as a share of the body's largest extent: a thirtieth to a twentieth, hashed per mote.</summary>
        private const float SizeShareLow = 1f / 30f;

        private const float SizeShareHigh = 1f / 20f;

        /// <summary>The bounds a mote's radius is held to, metres.</summary>
        private const float RadiusLow = 0.001f;

        private const float RadiusHigh = 0.01f;

        /// <summary>The ball's radius in the body's largest half-extents, hashed per mote between these.</summary>
        private const float BallLow = 1.0f;

        private const float BallHigh = 1.5f;

        /// <summary>The nearest a mote starts to the ball's centre, as a share of its ball radius, so few sit hidden at the body's core.</summary>
        private const float BallCore = 0.3f;

        /// <summary>How far a mote spreads outward over its life, in half-extents, and the film seconds over which the spread settles.</summary>
        private const float SpreadHalfExtents = 0.35f;

        private const float SpreadSeconds = 0.8f;

        /// <summary>
        /// A mote's moment of dissolving is <c>lerp(EndLow, 1, h^EndPower)</c> of <see cref="ThinSeconds"/>: the power puts most of them
        /// early, so about half are gone by a second at the default and the rest thin steadily to the end.
        /// </summary>
        private const float EndLow = 0.15f;

        private const float EndPower = 2f;

        /// <summary>The stretch over which a mote dissolves before its end, as a share of the end, and held under this many film seconds.</summary>
        private const float SpanShare = 0.6f;

        private const float SpanMostSeconds = 0.9f;

        /// <summary>The film seconds over which the cloud's brightness eases in from <see cref="InFrom"/>, so its first frame is no flash.</summary>
        private const float InSeconds = 0.15f;

        private const float InFrom = 0.4f;

        /// <summary>How fast a mote sinks, m/s of the run (eggs, near the water's own density), varied by <see cref="SinkJitter"/> either way.</summary>
        private const float SinkMetresPerSecond = 0.003f;

        private const float SinkJitter = 0.4f;

        /// <summary>The wobble of a mote about its path, metres, and the range of its period, film seconds.</summary>
        private const float WobbleMetres = 0.0015f;

        private const float WobbleLow = 1.2f;
        private const float WobbleHigh = 2.4f;

        /// <summary>A mote outside the frustum by more than this, metres, is not built.</summary>
        private const float Margin = 0.05f;

        /// <summary>
        /// The shader's seconds from the vertex colour to the snow's mote colour, set far beyond any cloud's life: a mote keeps its cream
        /// to the end, and its dimming is the vertex colour's.
        /// </summary>
        private const float TakeSeconds = 10000f;

        /// <summary>The tint, in sRGB: a pale cream, lighter than the bite crumbs' pale tissue (0.86, 0.78, 0.72).</summary>
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f, 1f);

        /// <summary>A mote's shade about the tint, hashed between these.</summary>
        private const float ShadeLow = 0.85f;

        private const float ShadeHigh = 1.05f;

        // ---------------------------------------------------------------- one newborn's cloud

        /// <summary>One mote, fixed at the cloud's start.</summary>
        private struct Mote
        {
            /// <summary>Its place relative to the ball's centre at the start.</summary>
            public Vector3 Offset;

            /// <summary>Where its spread takes it, relative to <see cref="Offset"/>, reached as the spread settles.</summary>
            public Vector3 Out;

            public float Radius;

            /// <summary>The film second of the cloud's age at which it is gone, and the stretch before it over which it dissolves.</summary>
            public float End;

            public float Span;

            public float Sink;
            public Vector3 Phase;
            public float Omega;
            public float Seed;
            public float Shade;

            /// <summary>The order in which it appears while the cloud forms (a hash in [0, 1)); used only with a lead.</summary>
            public float Rank;
        }

        /// <summary>One newborn's cloud: where and when it started, the current it drifts in, and its motes.</summary>
        private sealed class Cloud
        {
            public long Id;

            /// <summary>The second of the run of the newborn's first drawn frame in this pass: the cloud's start.</summary>
            public double First;

            public Vector3 Centre;

            public bool CurrentKnown;
            public Vector3 Current;

            public Mote[] Motes;

            /// <summary>A forming cloud (only with a lead): its newborn is not drawn yet, <see cref="First"/> is the birth row's second.</summary>
            public bool Forming;

            /// <summary>Its bubbles, drawn in place of the motes; made only with <see cref="TheatreBirthBubbles.On"/>, else null.</summary>
            public TheatreBirthBubbles.Bubble[] Bubbles;
        }

        /// <summary>What the caption says of a newborn: its first drawn frame in this pass, its parent, and how far apart their centres were then.</summary>
        private struct Born
        {
            public double First;
            public long Parent;
            public float Distance;
        }

        // ---------------------------------------------------------------- the state (made at the first frame, with the dial on)

        private static FilmWindowWorld _window;
        private static int _frame = -1;
        private static double _second = double.NaN;
        private static double _speed = 1d;
        private static int _births;
        private static bool _logged;

        /// <summary>A census render is under way (<see cref="Suspend"/>): nothing is drawn until the next frame.</summary>
        private static bool _held;

        private static List<Cloud> _clouds;
        private static HashSet<long> _seen;

        /// <summary>The newborns whose forming cloud was started (only with a lead), so each is started once per pass.</summary>
        private static HashSet<long> _led;
        private static Dictionary<long, Born> _born;

        // The caption's subject, read once: EVOSIM_THEATRE_FILM_SUBJECT naming one body by its id.
        private static bool _subjectRead;
        private static bool _subjectNamed;
        private static long _subject;

        // The drawing.
        private static GameObject _object;
        private static MeshRenderer _renderer;
        private static Mesh _mesh;
        private static Mesh _drawMesh;
        private static Material _material;
        private static CloudPass _pass;
        private static bool _hooked;
        private static bool _noShader;
        private static bool _noShellsTold;

        private static List<Vector3> _positions;
        private static List<Vector2> _corners;
        private static List<Vector4> _data;
        private static List<Color> _colours;
        private static List<int> _triangles;
        private static Plane[] _planes;

        private static double _builtSecond = double.NaN;
        private static Camera _builtCamera;
        private static Vector3 _builtEye;
        private static Quaternion _builtRotation;
        private static float _builtFov;
        private static float _builtAspect;
        private static int _builtClouds = -1;
        private static bool _builtHeld;

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="FilmWindowWorld.Show"/> after every body of a frame is placed, with the dial on: starts the cloud of each
        /// newborn the pass draws for the first time (within <see cref="CatchWorldSeconds"/> of its birth row), records what its caption
        /// says, and forgets the clouds that have thinned away.
        /// </summary>
        public static void Frame(FilmWindowWorld window)
        {
            if (!On || window == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;

            if (_clouds == null)
            {
                _clouds = new List<Cloud>();
                _seen = new HashSet<long>();
                _born = new Dictionary<long, Born>();
            }

            _held = false;

            bool replaced = !ReferenceEquals(window, _window);
            bool reopened = replaced || window.FrameIndex < _frame;

            if (!reopened && window.FrameIndex == _frame) return;

            if (replaced)
            {
                _born.Clear();
                _logged = false;
                _births = BirthsIn(window.Window);
            }

            if (reopened)
            {
                _clouds.Clear();
                _seen.Clear();
                if (_led != null) _led.Clear();
            }

            _window = window;
            _frame = window.FrameIndex;
            _second = window.Second;
            _speed = Math.Max(1d, window.Speed);

            // A window with no birth at all says so at once; any other says its first cloud when it starts.
            if (!_logged && _births == 0) Announce(null, -1L, float.NaN);

            bool drawer = EnsureDrawer();
            FilmWindowReader reader = window.Window;

            // With a lead: the births due within it, whose newborns the frame does not draw yet, start a forming cloud.
            if (Lead > 0f && drawer) StartForming(window, reader);

            for (int i = 0; i < window.BodyCount; i++)
            {
                long id = window.IdAt(i);
                if (!reader.TryBirthOf(id, out FilmWindowEvent birth)) continue;
                if (!_seen.Add(id)) continue;

                float distance = DistanceToParent(window, i, birth.Parent);
                _born[id] = new Born { First = _second, Parent = birth.Parent, Distance = distance };

                // Appear's rule: a newborn first drawn long after its birth row is not drawn arriving.
                if (_second - birth.Seconds > CatchWorldSeconds) continue;

                // The newborn is drawn now. With a lead its forming cloud becomes its cloud, motes, place and size kept, so nothing
                // jumps at the hand-over: at the end of its lead a forming cloud is drawn as a cloud at age 0 is.
                Cloud formed = Lead > 0f ? Adopt(id) : null;
                Cloud cloud = formed ?? Capture(window, id);
                if (cloud == null) continue;

                if (drawer && formed == null) _clouds.Add(cloud);
                if (!_logged) Announce(cloud, birth.Parent, distance);
            }

            // The clouds that have thinned away, forgotten; kept in their order, so the quads are written in the same order every time.
            for (int c = _clouds.Count - 1; c >= 0; c--)
            {
                // With the bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES): gone on the run's clock rather than the film's, the catch as below.
                if (TheatreBirthBubbles.On)
                {
                    if (_second - _clouds[c].First >= TheatreBirthBubbles.LastWorldSeconds) _clouds.RemoveAt(c);
                    else if (_clouds[c].Forming && _second - _clouds[c].First > CatchWorldSeconds) _clouds.RemoveAt(c);
                    continue;
                }

                if ((_second - _clouds[c].First) / _speed >= ThinSeconds) _clouds.RemoveAt(c);
                // A forming cloud whose newborn is still not drawn past Appear's catch after its birth second is dropped (it never
                // formed a body). Within the catch it holds as full, for a newborn first drawn a frame or two after its row.
                else if (_clouds[c].Forming && _second - _clouds[c].First > CatchWorldSeconds) _clouds.RemoveAt(c);
            }
        }

        /// <summary>
        /// For a census render: nothing drawn until the next <see cref="Frame"/>. The census views already turn the cloud's renderer off
        /// through <see cref="TheatreInsideOnly"/>, so this is for a caller that renders some other view it must keep bare.
        /// </summary>
        public static void Suspend()
        {
            if (!On) return;
            _held = true;
        }

        /// <summary>
        /// The caption D279 gives a film of one newborn: <c>"  born to &lt;parent&gt;, &lt;d&gt; m away"</c>, from the newborn's first
        /// drawn frame to the end, with <c>d</c> the distance between the parent's and the newborn's centres at that frame. False with the
        /// dial off, with <c>EVOSIM_THEATRE_FILM_SUBJECT</c> naming no single body, for a subject not born in the window, and before its
        /// first drawn frame; the label is then left as it was.
        /// </summary>
        public static bool TryCaption(FilmWindowWorld window, out string caption)
        {
            caption = null;
            if (!On || window == null || _born == null || !ReferenceEquals(window, _window)) return false;

            if (!_subjectRead)
            {
                _subjectRead = true;
                string pick = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT") ?? "").Trim();
                _subjectNamed = long.TryParse(pick, NumberStyles.Integer, CultureInfo.InvariantCulture, out _subject);
            }

            if (!_subjectNamed || !_born.TryGetValue(_subject, out Born born)) return false;
            if (born.Parent < 0L || window.Second < born.First) return false;

            var inv = CultureInfo.InvariantCulture;
            caption = float.IsNaN(born.Distance)
                ? "  born to " + born.Parent.ToString(inv)
                : string.Format(inv, "  born to {0}, {1:0.0} m away", born.Parent, born.Distance);
            return true;
        }

        // ---------------------------------------------------------------- one newborn

        /// <summary>The window's birth rows, counted once.</summary>
        private static int BirthsIn(FilmWindowReader reader)
        {
            IReadOnlyList<FilmWindowEvent> events = reader.Events;
            int n = 0;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == 'b') n++;
            }

            return n;
        }

        /// <summary>
        /// The distance between a newborn's centre and its parent's at the frame on screen: the parent's drawn centre where the frame
        /// draws it, else its recorded root (a parent the frame no longer draws as a living body). NaN where neither is known.
        /// </summary>
        private static float DistanceToParent(FilmWindowWorld window, int index, long parent)
        {
            if (parent < 0L) return float.NaN;

            Vector3 child = window.PositionOf(index);
            Vector3 there;

            if (window.TryIndexOf(parent, out int at)) there = window.PositionOf(at);
            else if (!window.RootAt(parent, window.Second, out there)) return float.NaN;

            float d = Vector3.Distance(child, there);
            return float.IsNaN(d) || float.IsInfinity(d) ? float.NaN : d;
        }

        /// <summary>One newborn's cloud, from its parts as the frame on screen draws them; null where the view does not hold the body.</summary>
        private static Cloud Capture(FilmWindowWorld window, long id)
        {
            if (window.View == null || !window.View.TryBody(id, out Phenotype sized, out Transform[] parts) || sized == null || parts == null)
            {
                return null;
            }

            // The box about the drawn parts, each part's box turned into the world's axes.
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 hi = -lo;
            int counted = 0;

            for (int q = 0; q < sized.PartCount && q < parts.Length; q++)
            {
                Transform part = parts[q];
                if (part == null) continue;

                Float3 h = sized.Parts[q].HalfExtents;
                Vector3 e = Abs(part.right) * h.X + Abs(part.up) * h.Y + Abs(part.forward) * h.Z;
                Vector3 c = part.position;

                lo = Vector3.Min(lo, c - e);
                hi = Vector3.Max(hi, c + e);
                counted++;
            }

            if (counted == 0) return null;

            Vector3 size = hi - lo;
            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (!(extent > 1e-6f) || float.IsInfinity(extent)) return null;

            return Gather(id, _second, 0.5f * (lo + hi), extent, size, false);
        }

        /// <summary>
        /// A cloud of a newborn of this extent about this centre, first at this second; the motes are a function of (id, index, extent).
        /// <paramref name="box"/> is the newborn's box's three sides, read only by the bubbles' cover.
        /// </summary>
        private static Cloud Gather(long id, double first, Vector3 centre, float extent, Vector3 box, bool forming)
        {
            float half = 0.5f * extent;
            int count = Mathf.RoundToInt(Mathf.Lerp(CountLow, CountHigh, Mathf.InverseLerp(ExtentLow, ExtentHigh, extent)));
            float thin = ThinSeconds;

            var cloud = new Cloud { Id = id, First = first, Centre = centre, Motes = new Mote[count], Forming = forming };

            for (int i = 0; i < count; i++)
            {
                // Uniform through the ball's shell from its core outward, each mote with a ball radius of its own: a loose ball.
                Vector3 way = Unit(Hash(id, i, 1), Hash(id, i, 2));
                float ball = half * Mathf.Lerp(BallLow, BallHigh, Hash(id, i, 3));
                float reach = ball * Mathf.Lerp(BallCore, 1f, Mathf.Pow(Hash(id, i, 4), 1f / 3f));

                float end = thin * Mathf.Lerp(EndLow, 1f, Mathf.Pow(Hash(id, i, 5), EndPower));
                float span = Mathf.Min(SpanShare * end, SpanMostSeconds);

                float diameter = extent * Mathf.Lerp(SizeShareLow, SizeShareHigh, Hash(id, i, 6));
                float period = Mathf.Lerp(WobbleLow, WobbleHigh, Hash(id, i, 7));

                cloud.Motes[i] = new Mote
                {
                    Offset = way * reach,
                    Out = way * (SpreadHalfExtents * half * Mathf.Lerp(0.5f, 1f, Hash(id, i, 8))),
                    Radius = Mathf.Clamp(0.5f * diameter, RadiusLow, RadiusHigh),
                    End = end,
                    Span = Mathf.Max(1e-3f, span),
                    Sink = SinkMetresPerSecond * (1f + SinkJitter * (Hash(id, i, 9) - 0.5f)),
                    Phase = new Vector3(6.2831853f * Hash(id, i, 10), 6.2831853f * Hash(id, i, 11), 6.2831853f * Hash(id, i, 12)),
                    Omega = 6.2831853f / period,
                    Seed = Hash(id, i, 13) * 97f,
                    Shade = Mathf.Lerp(ShadeLow, ShadeHigh, Hash(id, i, 14)),
                    Rank = Hash(id, i, 15),
                };
            }

            // With the bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES): made beside the motes, which are then not drawn. A forming cloud's early
            // bubbles leave over the run's seconds still to come before its birth row, so none is first drawn already risen.
            if (TheatreBirthBubbles.On) cloud.Bubbles = TheatreBirthBubbles.Make(id, extent, box, forming ? (float)Math.Max(0d, first - _second) : 0f);

            return cloud;
        }

        /// <summary>
        /// With a lead: starts the forming cloud of each birth due within the lead (in film seconds, taken as the run's seconds by
        /// the time-lapse) whose newborn the frame does not draw. The newborn does not exist yet, so its place is its recorded root at
        /// the birth row's second (<see cref="FilmWindowWorld.RootAt"/>, read from the window's pose frames ahead of time), and its size
        /// is its parent's drawn box scaled by <see cref="KinShare"/>, or a least size where the parent is not drawn.
        /// </summary>
        private static void StartForming(FilmWindowWorld window, FilmWindowReader reader)
        {
            if (_led == null) _led = new HashSet<long>();

            double to = _second + Lead * _speed;

            foreach (FilmWindowEvent birth in reader.EventsBetween(_second, to))
            {
                if (birth.Kind != 'b' || _seen.Contains(birth.Id) || window.TryIndexOf(birth.Id, out _) || !_led.Add(birth.Id)) continue;
                // The newborn's own box at its first recorded pose (FilmWindowWorld.TryUnborn, the close shot's stand-in), so the
                // forming cloud has the place and size the cloud drawn at the birth would have; the parent's extent scaled by
                // KinShare and the recorded root only where that box cannot be had. The parent's scale overshot by about three
                // times on 81811 (phase106).
                Vector3 centre;
                float extent;
                Vector3 box;

                if (window.TryUnborn(birth.Id, out FilmUnborn unborn) && unborn.Box.size.sqrMagnitude > 1e-12f)
                {
                    centre = unborn.Box.center;
                    extent = Mathf.Max(unborn.Box.size.x, Mathf.Max(unborn.Box.size.y, unborn.Box.size.z));
                    box = unborn.Box.size;
                }
                else
                {
                    if (!window.RootAt(birth.Id, birth.Seconds, out centre)) continue;
                    extent = KinLeast;
                    if (birth.Parent >= 0L && BoxExtent(window, birth.Parent, out float parent)) extent = Mathf.Max(KinLeast, KinShare * parent);
                    box = new Vector3(extent, extent, extent);
                }

                _clouds.Add(Gather(birth.Id, birth.Seconds, centre, extent, box, true));
            }
        }

        /// <summary>
        /// Where a newborn has come to be drawn and its forming cloud is still kept: that cloud, turned into the newborn's cloud
        /// from this second (no longer forming, its age counted from now), in its place in the list. Null where there is none.
        /// </summary>
        private static Cloud Adopt(long id)
        {
            for (int c = 0; c < _clouds.Count; c++)
            {
                Cloud cloud = _clouds[c];
                if (!cloud.Forming || cloud.Id != id) continue;

                cloud.Forming = false;
                cloud.First = _second;
                cloud.CurrentKnown = false;
                return cloud;
            }

            return null;
        }

        /// <summary>The largest side of the box about a drawn body's parts (as <see cref="Capture"/> takes it); false where the view does not hold the body.</summary>
        private static bool BoxExtent(FilmWindowWorld window, long id, out float extent)
        {
            extent = 0f;
            if (window.View == null || !window.View.TryBody(id, out Phenotype sized, out Transform[] parts) || sized == null || parts == null) return false;

            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 hi = -lo;
            int counted = 0;

            for (int q = 0; q < sized.PartCount && q < parts.Length; q++)
            {
                Transform part = parts[q];
                if (part == null) continue;

                Float3 h = sized.Parts[q].HalfExtents;
                Vector3 e = Abs(part.right) * h.X + Abs(part.up) * h.Y + Abs(part.forward) * h.Z;
                lo = Vector3.Min(lo, part.position - e);
                hi = Vector3.Max(hi, part.position + e);
                counted++;
            }

            if (counted == 0) return false;

            Vector3 size = hi - lo;
            extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return extent > 1e-6f && !float.IsInfinity(extent);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>The one log line of a window, with the dial on: its births, the thinning time, and its first cloud (or none).</summary>
        private static void Announce(Cloud first, long parent, float distance)
        {
            _logged = true;
            var inv = CultureInfo.InvariantCulture;

            string head = string.Format(inv, "[Theatre] birth cloud (D279): {0} births in the window, thinning over {1:0.##} film s; first: ",
                _births, ThinSeconds);

            string lead = Lead > 0f ? string.Format(inv, " lead {0:0.##} s", Lead) : "";

            if (first == null)
            {
                Debug.Log(head + "none" + lead);
                return;
            }

            string away = float.IsNaN(distance) ? "distance unknown" : string.Format(inv, "{0:0.0} m away", distance);
            Debug.Log(head + string.Format(inv, "{0} born to {1}, {2}, {3} motes", first.Id, parent, away, first.Motes.Length) + lead);
        }

        // ---------------------------------------------------------------- the hash

        private static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7feb352dU;
                x ^= x >> 15;
                x *= 0x846ca68bU;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>A number in [0, 1) hashed from a newborn's id, a mote's index and a salt (the bubbles' too, at salts from 21).</summary>
        internal static float Hash(long id, int index, int salt)
        {
            unchecked
            {
                uint key = Mix((uint)id ^ Mix((uint)(id >> 32) + 0x9E3779B9U) ^ 0xB5297A4DU);
                uint h = Mix(key + (uint)index * 0x9E3779B9U + (uint)salt * 0x27D4EB2FU);
                return (h >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>A unit vector from two uniform numbers, uniform over the sphere.</summary>
        internal static Vector3 Unit(float a, float b)
        {
            float z = 2f * a - 1f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = 6.2831853f * b;
            return new Vector3(r * Mathf.Cos(phi), z, r * Mathf.Sin(phi));
        }

        // ---------------------------------------------------------------- the drawing

        // The flakes' shader, a mesh, an object to draw it in the camera's render, and the hook; made once, at the first frame with the
        // dial on. False when the shader cannot be found, said once.
        private static bool EnsureDrawer()
        {
            if (_noShader) return false;

            // With the bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES): their own shader in place of the flakes'; the block below is then skipped.
            if (_material == null && TheatreBirthBubbles.On)
            {
                _material = TheatreBirthBubbles.MakeMaterial();

                if (_material == null)
                {
                    _noShader = true;
                    return false;
                }
            }

            if (_material == null)
            {
                Shader shader = Shader.Find("Evosim/Theatre Crumble Flakes");

                if (shader == null)
                {
                    _noShader = true;
                    Debug.LogWarning("[Theatre] birth cloud: shader 'Evosim/Theatre Crumble Flakes' not found, so no cloud is drawn");
                    return false;
                }

                _material = new Material(shader) { name = "Theatre Birth Cloud", hideFlags = HideFlags.HideAndDontSave };
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Theatre Birth Cloud", hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
            }

            if (_positions == null)
            {
                _positions = new List<Vector3>();
                _corners = new List<Vector2>();
                _data = new List<Vector4>();
                _colours = new List<Color>();
                _triangles = new List<int>();
                _planes = new Plane[6];
            }

            // The object belongs to the scene the film plays in, and is made again if a scene change took it.
            if (_object == null)
            {
                _object = new GameObject("Theatre Birth Cloud");
                _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

                _renderer = _object.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                // Seen from inside the water only, as the shells, the flakes and the crumbs are, and turned off for the census views with them.
                _object.AddComponent<TheatreInsideOnly>();
            }

            if (_pass == null) _pass = new CloudPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };

            if (!_hooked)
            {
                RenderPipelineManager.beginCameraRendering += OnCamera;
                _hooked = true;
            }

            return true;
        }

        private static void OnCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_renderer == null || camera == null || camera.cameraType != CameraType.Game) return;

            // Turned off by the census views (TheatreInsideOnly): nothing to draw and nothing to build.
            if (!_renderer.enabled) return;

            bool lens = TheatreLens.Armed;

            try
            {
                Build(camera);
            }
            catch (Exception e)
            {
                // A cosmetic layer never takes the film down; the cloud is simply absent for this render.
                _drawMesh = null;
                if (_mesh != null) _mesh.Clear();
                Debug.LogWarning("[Theatre] birth cloud: the cloud could not be built for this render: " + e.GetType().Name + ": " + e.Message);
            }

            // With the lens armed the shells are drawn after it, sharp; the cloud follows them, as the crumble's flakes do.
            _renderer.forceRenderingOff = lens;

            if (lens && _drawMesh != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_pass);
            }
        }

        private static void Build(Camera camera)
        {
            Transform view = camera.transform;
            TheatreSkin skin = TheatreSkin.Current;
            Material shells = skin != null ? skin.SnowShellsMaterial : null;

            // The run's water is opened by the first push of a frame; until then the current at a cloud would read zero.
            bool water = skin != null && (skin.RunWaterOpen || !skin.RunWaterWanted);
            int clouds = _clouds != null ? _clouds.Count : 0;

            bool same = ReferenceEquals(_builtCamera, camera) && _second == _builtSecond && view.position == _builtEye &&
                        view.rotation == _builtRotation && camera.fieldOfView == _builtFov && camera.aspect == _builtAspect &&
                        clouds == _builtClouds && _held == _builtHeld && water;
            if (same) return;

            _builtCamera = camera;
            _builtSecond = _second;
            _builtEye = view.position;
            _builtRotation = view.rotation;
            _builtFov = camera.fieldOfView;
            _builtAspect = camera.aspect;
            _builtClouds = clouds;
            _builtHeld = _held;

            _positions.Clear();
            _corners.Clear();
            _data.Clear();
            _colours.Clear();
            _triangles.Clear();
            _drawMesh = null;

            if (_held || clouds == 0 || !water || double.IsNaN(_second) || shells == null || _material == null)
            {
                if (shells == null && clouds > 0 && !_noShellsTold && skin != null)
                {
                    _noShellsTold = true;
                    Debug.Log("[Theatre] birth cloud: the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the cloud cannot be drawn in the water's box");
                }

                _mesh.Clear();
                return;
            }

            // The water the shells sit in, and the colour and gain of their motes.
            _material.SetColor("_MoteColor", shells.GetColor("_MoteColor"));
            _material.SetFloat("_ShellGain", shells.GetFloat("_ShellGain"));
            _material.SetFloat("_MoteAmbient", shells.GetFloat("_MoteAmbient"));
            _material.SetVector("_BoxMin", shells.GetVector("_BoxMin"));
            _material.SetVector("_BoxMax", shells.GetVector("_BoxMax"));
            _material.SetVector("_Tank", shells.GetVector("_Tank"));
            _material.SetFloat("_Waterline", shells.GetFloat("_Waterline"));
            _material.SetFloat("_TakeSeconds", TakeSeconds);

            GeometryUtility.CalculateFrustumPlanes(camera, _planes);

            bool open = skin.RunWaterOpen;
            double speed = Math.Max(1d, _speed);
            Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? Cream.linear : Cream;
            int drawn = 0;

            for (int c = 0; c < _clouds.Count; c++)
            {
                Cloud cloud = _clouds[c];

                double ageWorld = _second - cloud.First;
                float ageFilm = (float)(ageWorld / speed);

                // With the bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES): drawn in place of the motes, on the run's clock, forming or not. A
                // forming cloud is held at the birth row's second while its newborn is caught late, so nothing jumps at the adoption.
                if (TheatreBirthBubbles.On)
                {
                    if (cloud.Bubbles == null) continue;

                    if (!cloud.CurrentKnown)
                    {
                        cloud.Current = open ? skin.WaterAt(cloud.Centre, cloud.First) : Vector3.zero;
                        cloud.CurrentKnown = true;
                    }

                    float bubbleAge = cloud.Forming ? Mathf.Min(0f, (float)ageWorld) : (float)ageWorld;
                    drawn += TheatreBirthBubbles.Draw(cloud.Id, cloud.Bubbles, cloud.Centre, cloud.Current, bubbleAge, view.position);
                    continue;
                }

                // Only with a lead: a cloud still forming gathers its motes about the place the newborn will be.
                if (cloud.Forming)
                {
                    drawn += DrawForming(cloud, 1f + ageFilm / Lead, tint);
                    continue;
                }

                if (ageWorld < 0d || ageFilm >= ThinSeconds) continue;

                // The current at the ball's centre at the cloud's start: a function of the place and the second, read once.
                if (!cloud.CurrentKnown)
                {
                    cloud.Current = open ? skin.WaterAt(cloud.Centre, cloud.First) : Vector3.zero;
                    cloud.CurrentKnown = true;
                }

                float spread = 1f - Mathf.Exp(-ageFilm / SpreadSeconds);
                float shown = Mathf.Clamp01(ageFilm / InSeconds);
                float easeIn = Mathf.Lerp(InFrom, 1f, shown * shown * (3f - 2f * shown));
                float age = (float)ageWorld;

                for (int i = 0; i < cloud.Motes.Length; i++)
                {
                    Mote m = cloud.Motes[i];

                    // Dissolving over its own stretch before its own end: it shrinks and dims together, smoothly to nothing.
                    float left = Mathf.Clamp01((m.End - ageFilm) / m.Span);
                    float keep = left * left * (3f - 2f * left);
                    if (!(keep > 0f)) continue;

                    // Its place in the ball, the spread settling, the water it was born into and its sink, and a small wobble that starts at nothing.
                    Vector3 place = cloud.Centre + m.Offset + m.Out * spread + (cloud.Current + new Vector3(0f, -m.Sink, 0f)) * age;
                    place.x += WobbleMetres * (Mathf.Sin(m.Omega * ageFilm + m.Phase.x) - Mathf.Sin(m.Phase.x));
                    place.y += 0.5f * WobbleMetres * (Mathf.Sin(1.17f * m.Omega * ageFilm + m.Phase.y) - Mathf.Sin(m.Phase.y));
                    place.z += WobbleMetres * (Mathf.Sin(0.83f * m.Omega * ageFilm + m.Phase.z) - Mathf.Sin(m.Phase.z));

                    if (!InFrustum(place, Margin)) continue;

                    float radius = m.Radius * keep;
                    if (!(radius > 0f)) continue;

                    float gain = m.Shade * keep * easeIn;
                    var colour = new Color(tint.r * gain, tint.g * gain, tint.b * gain, 1f);

                    int first = _positions.Count;
                    // w = 1: a large mote, which the shader draws as a soft round disc rather than as a crumble flake.
                    var data = new Vector4(radius, m.Seed, ageFilm, 1f);

                    for (int corner = 0; corner < 4; corner++)
                    {
                        _positions.Add(place);
                        _corners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                        _data.Add(data);
                        _colours.Add(colour);
                    }

                    _triangles.Add(first);
                    _triangles.Add(first + 1);
                    _triangles.Add(first + 2);
                    _triangles.Add(first);
                    _triangles.Add(first + 2);
                    _triangles.Add(first + 3);

                    drawn++;
                }
            }

            if (drawn > 0)
            {
                _mesh.Clear();
                _mesh.indexFormat = IndexFormat.UInt32;
                _mesh.SetVertices(_positions);
                _mesh.SetUVs(0, _corners);
                _mesh.SetUVs(1, _data);
                _mesh.SetColors(_colours);
                _mesh.SetTriangles(_triangles, 0, false);
                _mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
                _drawMesh = _mesh;
            }
            else
            {
                _mesh.Clear();
            }
        }

        /// <summary>
        /// A forming cloud at <paramref name="u"/> of its lead (0 at its start, 1 at the birth row's second): the share of the motes
        /// showing rises from <see cref="FormFrom"/> to all of them, in the order of their hashed rank, so it gathers from scattered
        /// motes. Each fades and grows in as it appears and drifts in from <see cref="FormOut"/> further out, so none pops. At u = 1 it is
        /// the cloud's first frame as drawn without a lead (age 0: full size, offset in the ball, the brightness at <see cref="InFrom"/>).
        /// Returns the motes written.
        /// </summary>
        private static int DrawForming(Cloud cloud, float u, Color tint)
        {
            u = Mathf.Clamp01(u);

            // 0.25 of rank is the stretch over which one mote grows in; the share runs past 1 so the last rank is whole at u = 1.
            const float Grow = 0.25f;
            float share = FormFrom + (1f + Grow - FormFrom) * u;
            int drawn = 0;

            for (int i = 0; i < cloud.Motes.Length; i++)
            {
                Mote m = cloud.Motes[i];

                float t = Mathf.Clamp01((share - m.Rank) / Grow);
                float keep = t * t * (3f - 2f * t);
                if (!(keep > 0f)) continue;

                Vector3 place = cloud.Centre + m.Offset * (1f + FormOut * (1f - keep));
                if (!InFrustum(place, Margin)) continue;

                float radius = m.Radius * keep;
                if (!(radius > 0f)) continue;

                float gain = m.Shade * keep * InFrom;
                var colour = new Color(tint.r * gain, tint.g * gain, tint.b * gain, 1f);
                AddQuad(place, new Vector4(radius, m.Seed, 0f, 1f), colour);
                drawn++;
            }

            return drawn;
        }

        /// <summary>One camera-facing quad of the cloud's mesh: four corners and two triangles (the bubbles' too).</summary>
        internal static void AddQuad(Vector3 place, Vector4 data, Color colour)
        {
            int first = _positions.Count;

            for (int corner = 0; corner < 4; corner++)
            {
                _positions.Add(place);
                _corners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                _data.Add(data);
                _colours.Add(colour);
            }

            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }

        internal static bool InFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < _planes.Length; i++)
            {
                if (_planes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        /// <summary>Draws the cloud after the theatre's lens, as the crumble draws its flakes: each mote carries its own blur.</summary>
        private sealed class CloudPass : ScriptableRenderPass
        {
            public CloudPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private sealed class PassData
            {
                public Mesh Mesh;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Mesh mesh = _drawMesh;
                Material material = _material;
                if (mesh == null || material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre Birth Cloud", out PassData data))
                {
                    data.Mesh = mesh;
                    data.Material = material;

                    builder.UseTexture(depth);
                    builder.SetRenderAttachment(colour, 0, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawMesh(d.Mesh, Matrix4x4.identity, d.Material, 0, 0);
                    });
                }
            }
        }
    }
}
