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
    /// <summary>What the crumble needs from the world that plays a window: bodies' measures, places and posed parts.</summary>
    public interface ICrumbleSource
    {
        /// <summary>The skin's palette, for a flake's tissue colour; null before it is set.</summary>
        TheatrePalette Palette { get; }

        /// <summary>Whether the palette paints the guilds (the view's switch), which a flake's starting colour follows.</summary>
        bool ColourByCellType { get; }

        /// <summary>
        /// The window's own sample of a corpse: its second and the share of its first joules it held then. False when the window
        /// cannot place the corpse at that sample. Read to find where a standing corpse's decay is timed from.
        /// </summary>
        bool CorpseSample(FilmWindowCorpse corpse, int sample, out double second, out float fraction);

        /// <summary>A body's box volume at the start of its crumble, m^3, and how far it reaches, m. False when it cannot be developed.</summary>
        bool Measure(CrumbleBody body, out float volume, out float reach);

        /// <summary>Where a body stands at a second: a remains body's recorded root (held at its last frame), a corpse's place.</summary>
        bool PositionOf(CrumbleBody body, double second, out Vector3 position);

        /// <summary>The frame to read for a pose at a second, clamped to the frames that hold the body; -1 for none, -2 when no frame is needed.</summary>
        int FrameFor(CrumbleBody body, double second);

        /// <summary>One recorded frame of the window.</summary>
        PoseFrame ReadFrame(int frame);

        /// <summary>
        /// Lays the body's parts in the frame (a remains body) or at the second (a corpse) into <paramref name="layout"/>. False when it
        /// cannot be. A remains body is laid once for all the flakes of one frame.
        /// </summary>
        bool Lay(CrumbleBody body, PoseFrame poses, double second, CrumbleLayout layout);
    }

    /// <summary>A body's parts as the source laid them: the world point is <c>Anchor + k * (position + rotation * local - Centre)</c>.</summary>
    public sealed class CrumbleLayout
    {
        /// <summary>The phenotype whose half-extents and cell types the parts have.</summary>
        public Phenotype Shape;

        public Vector3[] Positions;
        public Quaternion[] Rotations;

        /// <summary>The world place the body hangs from: a remains body's root, a corpse's centre of mass.</summary>
        public Vector3 Anchor;

        /// <summary>Taken from the part positions before the scale: a corpse's centre, so it shrinks about itself; zero for remains.</summary>
        public Vector3 Centre;

        /// <summary>The scale the body is drawn at before the crumble's: a corpse's shrink, 1 for remains.</summary>
        public float BaseScale = 1f;

        /// <summary>Arrays a remains body is laid into, kept so a layout allocates once.</summary>
        public Vector3[] Scratch = new Vector3[0];

        public Quaternion[] ScratchRotations = new Quaternion[0];
    }

    /// <summary>One flake, fixed at its birth: where it left the body, how it drifts and the colour it started in.</summary>
    public struct CrumbleFlake
    {
        public Vector3 Birth;

        /// <summary>The current at the birth point, held, plus the flake's own sinking.</summary>
        public Vector3 Drift;

        /// <summary>The part's guild colour dulled as the remains are at that point of the crumble, in the shader's colour space.</summary>
        public Color Tissue;

        public Vector3 Phase;
        public float Omega;
        public float Seed;
    }

    /// <summary>A body that crumbles: a remains body or a corpse, its timeline and its flakes' schedule.</summary>
    public sealed class CrumbleBody
    {
        public long Id;
        public bool Corpse;
        public int Order;

        public FilmWindowRemains Remains;
        public FilmWindowCorpse CorpseRow;

        /// <summary>The second its progress leaves 0, and the second it reaches 1.</summary>
        public double StartSecond;

        public double EndSecond;

        /// <summary>A corpse's timeline: the second at which it held <see cref="AnchorFraction"/> of its first joules.</summary>
        public double AnchorSecond;

        public float AnchorFraction;
        public bool Anchored;

        /// <summary>Flakes born later than this are not shed: a remains body that left the stream before its window closed sheds no more.</summary>
        public double ShedUntil = double.PositiveInfinity;

        public bool Scheduled;

        /// <summary>No flake can come of this body: it cannot be developed, or the window never holds it.</summary>
        public bool Unusable;

        public float Reach;
        public float Volume;

        public int Count;

        /// <summary>Each flake's birth progress and birth second, ascending.</summary>
        public float[] C;

        public double[] T;

        /// <summary>0 not yet placed, 1 placed, 2 skipped (no pose found), 3 not shed.</summary>
        public byte[] State;

        public CrumbleFlake[] Flakes;
    }

    /// <summary>
    /// D277's crumble (<c>EVOSIM_THEATRE_CRUMBLE</c>): dead tissue dwindles to nothing and sheds flakes of marine snow as it goes.
    /// A remains body (D220) or a corpse (D127) shrinks continuously to zero, dulls toward grey, and leaves flakes that drift off
    /// with the water, sink, and dissolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> The film window builds one of these only with the dial on; with it off there
    /// is no object, mesh, material, hook or log line, and the remains and corpses are drawn as they were.
    /// </para>
    /// <para>
    /// <b>A flake is a stateless function of (body, flake index, second).</b> Its birth progress, birth second, birth point, size,
    /// wobble and drift are hashed from the body's id and the flake's index, and its place at a second is its birth point plus
    /// its drift times its age plus a small wobble, so any film of the window draws the same flake in the same place. The
    /// only state is a cache of what is already worked out. The pose it is born in is read from the window's pose stream at the
    /// birth second rounded down to a whole second, clamped to the frames that hold the body.
    /// </para>
    /// <para>
    /// <b>Drawn in the camera render that draws the bodies.</b> The flakes are one mesh of camera-facing quads, rebuilt when a camera
    /// is about to render, because only then is the view known: a crumbling body takes part when its sphere, grown by the furthest
    /// its flakes drift in their life, meets the camera's frustum. The quad is shaded by the snow shells' own mote function
    /// (<c>EvoShadeMote</c> in <c>TheatreSnow.hlsl</c>). While the theatre's lens is armed the shells are held off the camera's own
    /// drawing and drawn after the lens, sharp, so each mote is blurred by its own depth; the flakes follow them there.
    /// </para>
    /// </remarks>
    public sealed class TheatreCrumble : IDisposable
    {
        // ---------------------------------------------------------------- the dials

        // The main dial, EVOSIM_THEATRE_CRUMBLE (0 off, 1 on), is read by the world that builds this (FilmWindowWorld.CrumbleDial), so
        // that with it off this class is never touched and its dials below are never read.

        /// <summary>The share of a remains body's window the crumble takes, at its end (<c>EVOSIM_THEATRE_CRUMBLE_REMAINS_SHARE</c>).</summary>
        private static readonly float Share = TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_REMAINS_SHARE", 0.4f, 0.05f, 1f);

        /// <summary>A flake's mean radius, metres (<c>EVOSIM_THEATRE_CRUMBLE_FLAKE_RADIUS</c>).</summary>
        private static readonly float FlakeRadius = TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_FLAKE_RADIUS", 0.004f, 0.0005f, 0.05f);

        /// <summary>The most and the fewest flakes a body sheds (<c>EVOSIM_THEATRE_CRUMBLE_FLAKES_MAX</c>, <c>_MIN</c>).</summary>
        private static readonly int FlakesMax = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_FLAKES_MAX", 150f, 1f, 2000f));

        private static readonly int FlakesMin = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_FLAKES_MIN", 12f, 0f, 2000f));

        /// <summary>How long a flake lives, world seconds (<c>EVOSIM_THEATRE_CRUMBLE_LIFE</c>).</summary>
        private static readonly float Life = TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_LIFE", 120f, 10f, 3600f);

        /// <summary>
        /// How late the flakes leave (<c>EVOSIM_THEATRE_CRUMBLE_BURST</c>, 1 to 8): a flake's birth progress is drawn from the
        /// distribution function <c>F(c) = c^p</c>, so 1 spreads the births evenly along the shrink and 3 sheds half of them after
        /// c = 0.79, while the shrink is fastest.
        /// </summary>
        private static readonly float Burst = TheatreSkin.Dial("EVOSIM_THEATRE_CRUMBLE_BURST", 3f, 1f, 8f);

        /// <summary>
        /// A body id whose progress, scale and drawn scale are logged every frame (<c>EVOSIM_THEATRE_CRUMBLE_TRACE</c>), 0 for none.
        /// Read as an integer, since a float dial would lose digits of a large id.
        /// </summary>
        private static readonly long TraceId = ReadTrace();

        private static long ReadTrace()
        {
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CRUMBLE_TRACE");
            if (string.IsNullOrWhiteSpace(text)) return 0L;
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) ? id : 0L;
        }

        // ---------------------------------------------------------------- the constants

        /// <summary>A body is not placed once its scale is below this.</summary>
        public const float GoneBelow = 0.001f;

        /// <summary>Where the cube root's last stretch is replaced by a cubic Hermite that reaches zero with zero slope.</summary>
        private const float HermiteFrom = 0.95f;

        private const float HermiteSpan = 0.05f;

        /// <summary>Seconds a flake takes to go from its tissue colour to the snow's colour.</summary>
        private const float TakeSeconds = 10f;

        /// <summary>Seconds a flake dissolves over, at the end of its life.</summary>
        private const float DissolveSeconds = 5f;

        /// <summary>The pose a flake is born in is read at its birth second rounded down to a multiple of this, seconds.</summary>
        private const double PoseSnapSeconds = 1d;

        /// <summary>The wobble of a flake about its drifting path, metres, and the range of its period, seconds.</summary>
        private const float WobbleMetres = 0.01f;

        private const float WobblePeriodLow = 4f;
        private const float WobblePeriodHigh = 6f;

        /// <summary>How much a flake's own sinking differs from the snow's, as a share either way.</summary>
        private const float SinkJitter = 0.4f;

        /// <summary>A flake outside the frustum by more than this, metres, is not built.</summary>
        private const float FlakeMargin = 0.25f;

        private static readonly float HermiteP0 = Mathf.Pow(HermiteSpan, 1f / 3f);
        private static readonly float HermiteM0 = -(1f / 3f) * Mathf.Pow(HermiteSpan, -2f / 3f);

        // ---------------------------------------------------------------- the curve

        /// <summary>
        /// The linear scale at progress <paramref name="c"/>, 1 whole to 0 gone: the cube root of what is left up to 0.95, so the
        /// volume falls linearly, then a cubic Hermite from there to 0 with the cube root's slope where it joins and zero slope at
        /// the end (the cube root's own slope is infinite at 1).
        /// </summary>
        public static float ScaleAt(float c)
        {
            if (!(c > 0f)) return 1f;
            if (c >= 1f) return 0f;
            if (c <= HermiteFrom) return Mathf.Pow(1f - c, 1f / 3f);

            float t = (c - HermiteFrom) / HermiteSpan;
            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;

            return h00 * HermiteP0 + h10 * HermiteSpan * HermiteM0;
        }

        /// <summary>A remains body's progress at an age in its window of <paramref name="window"/> seconds: 0 until the last share, then 0 to 1.</summary>
        public static float ProgressOfRemains(double age, float window)
        {
            double span = window * (double)Share;
            if (!(span > 0d)) return 0f;

            double c = (age - window * (1d - Share)) / span;
            return (float)Math.Min(1d, Math.Max(0d, c));
        }

        /// <summary>The sum of the parts' box volumes, m^3.</summary>
        public static float BoxVolume(Phenotype phenotype)
        {
            float volume = 0f;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                Float3 h = phenotype.Parts[i].HalfExtents;
                volume += 8f * Mathf.Abs(h.X * h.Y * h.Z);
            }

            return volume;
        }

        /// <summary>A point on the surface of a box of half-extents (hx, hy, hz), face chosen by area: four uniform numbers in [0, 1).</summary>
        public static Vector3 SurfacePoint(float hx, float hy, float hz, float face, float a, float b, float side)
        {
            float ax = hy * hz, ay = hx * hz, az = hx * hy;
            float pick = face * (ax + ay + az);
            float sign = side < 0.5f ? -1f : 1f;
            float p = 2f * a - 1f, q = 2f * b - 1f;

            if (pick < ax) return new Vector3(sign * hx, p * hy, q * hz);
            if (pick < ax + ay) return new Vector3(p * hx, sign * hy, q * hz);
            return new Vector3(p * hx, q * hy, sign * hz);
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

        /// <summary>A number in [0, 1) hashed from a body, a flake index and a salt: the same on every machine and in every film.</summary>
        public static float Hash01(long id, int index, int salt, bool corpse)
        {
            unchecked
            {
                uint key = Mix((uint)id ^ Mix((uint)(id >> 32) + 0x9E3779B9U));
                if (corpse) key ^= 0x5bd1e995U;
                uint h = Mix(key + (uint)index * 0x9E3779B9U + (uint)salt * 0x85EBCA6BU);
                return (h >> 8) * (1f / 16777216f);
            }
        }

        // ---------------------------------------------------------------- the state

        private readonly ICrumbleSource _source;
        private readonly FilmWindowReader _window;
        private readonly float _remainsWindow;
        private readonly float _corpseFadeSeconds;
        private readonly float _floor;
        private readonly float _rate;
        private readonly float _top;
        private readonly bool _corpsesTimed;

        /// <summary>Whether the world draws remains and corpses at all: only what is drawn crumbles.</summary>
        private readonly bool _remainsDrawn;

        private readonly bool _corpsesDrawn;

        private readonly Dictionary<long, CrumbleBody> _remainsBodies = new Dictionary<long, CrumbleBody>();
        private readonly Dictionary<long, CrumbleBody> _corpseBodies = new Dictionary<long, CrumbleBody>();

        /// <summary>Every corpse of the window as a body, built and anchored once at the first <see cref="Advance"/>.</summary>
        private readonly List<CrumbleBody> _corpseList = new List<CrumbleBody>();

        private bool _corpsesReady;
        private readonly List<CrumbleBody> _active = new List<CrumbleBody>();
        private readonly List<CrumbleBody> _near = new List<CrumbleBody>();
        private int _orders;
        private int _worldCrumbling;
        private double _second = double.NaN;

        private GameObject _object;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Mesh _drawMesh;
        private Material _material;
        private DrawPass _pass;
        private bool _hooked;
        private bool _noShellsTold;

        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector2> _corners = new List<Vector2>();
        private readonly List<Vector4> _data = new List<Vector4>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<int> _triangles = new List<int>();
        private readonly Plane[] _planes = new Plane[6];

        private struct Need
        {
            public int Frame;
            public int Order;
            public CrumbleBody Body;
            public int Index;
        }

        private readonly List<Need> _needs = new List<Need>();
        private readonly CrumbleLayout _layout = new CrumbleLayout();

        private static readonly Comparison<Need> NeedOrder = (a, b) =>
            a.Frame != b.Frame ? a.Frame.CompareTo(b.Frame) : a.Order.CompareTo(b.Order);

        /// <summary>The whole rendered second the status line last counted, and how many new ones it has counted.</summary>
        private double _statsSecond = double.NaN;

        private int _newSeconds;

        /// <summary>The shells' own fall rate, m/s, for a run whose water is never opened.</summary>
        private float _fall;

        private int _drawn;
        private int _skipped;
        private double _builtSecond = double.NaN;
        private Vector3 _builtEye;
        private Quaternion _builtRotation;
        private float _builtFov;
        private float _builtAspect;
        private Camera _builtCamera;
        private bool _builtWater;

        /// <summary>
        /// Builds the crumble for a film window and says so in the log. Call it only with the dial on.
        /// </summary>
        /// <param name="source">The world that plays the window.</param>
        /// <param name="window">The window, for its remains and corpses.</param>
        /// <param name="config">The run's rules: the remains window and the corpse's decay and floor.</param>
        /// <param name="corpseFadeSeconds">The seconds before a corpse leaves that its crumble takes (<c>EVOSIM_THEATRE_CORPSE_FADE_SECONDS</c>).</param>
        /// <param name="remainsDrawn">Whether the world draws remains; if not, none crumble and none shed.</param>
        /// <param name="corpsesDrawn">Whether the world draws corpses; if not, none crumble and none shed.</param>
        public TheatreCrumble(ICrumbleSource source, FilmWindowReader window, RunConfig config, float corpseFadeSeconds, bool remainsDrawn, bool corpsesDrawn)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _window = window ?? throw new ArgumentNullException(nameof(window));
            if (config == null) throw new ArgumentNullException(nameof(config));

            _remainsDrawn = remainsDrawn;
            _corpsesDrawn = corpsesDrawn;

            _remainsWindow = config.RemainsWindowSeconds;
            _corpseFadeSeconds = corpseFadeSeconds;
            _floor = config.CorpseLeavesBelowFraction;
            _rate = config.CorpseDecayPerSecond;
            _corpsesTimed = _floor > 0f && _rate > 0f && corpseFadeSeconds > 0f;
            _top = _corpsesTimed ? _floor * Mathf.Exp(_rate * corpseFadeSeconds) : 1f;

            Shader shader = Shader.Find("Evosim/Theatre Crumble Flakes");

            if (shader == null)
            {
                Debug.LogWarning("[Theatre] crumble: shader 'Evosim/Theatre Crumble Flakes' not found, so the flakes are not drawn; the bodies still crumble");
            }
            else
            {
                _material = new Material(shader) { name = "Theatre Crumble Flakes", hideFlags = HideFlags.HideAndDontSave };

                _mesh = new Mesh { name = "Theatre Crumble Flakes", hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();

                _object = new GameObject("Theatre Crumble Flakes") { hideFlags = HideFlags.DontSave };
                _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

                _renderer = _object.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                // Seen from inside the water only, as the shells are, and turned off for the census views with them.
                _object.AddComponent<TheatreInsideOnly>();

                _pass = new DrawPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
                _hooked = true;
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] crumble (D277): EVOSIM_THEATRE_CRUMBLE=1 remains share={0} flake radius={1} m flakes {2}..{3} a body life={4} s trace={5} burst={8}; " +
                "corpses {6}; remains window {7}",
                Share, FlakeRadius, FlakesMin, FlakesMax, Life, TraceId,
                !_corpsesDrawn ? "not drawn" :
                _corpsesTimed ? "crumble over their last " + corpseFadeSeconds.ToString(CultureInfo.InvariantCulture) + " s" : "not timed (no decay, floor or fade)",
                _remainsDrawn ? _remainsWindow.ToString(CultureInfo.InvariantCulture) + " s" : "not drawn", Burst));
        }

        // ---------------------------------------------------------------- progress, for the world's placing

        /// <summary>Whether corpses crumble: the run decays them, removes them at a floor, and the fade dial is above 0.</summary>
        public bool CorpsesTimed => _corpsesTimed;

        /// <summary>The share of its first joules a corpse holds as its crumble begins: the floor it leaves at grown by the decay over the fade.</summary>
        public float CorpseTop => _top;

        /// <summary>A corpse's progress from the share of its first joules it holds: 0 at the share its fade begins at, 1 at the floor it leaves at.</summary>
        public float ProgressOfCorpse(float fraction)
        {
            if (!_corpsesTimed) return 0f;
            return Mathf.Clamp01((_top - fraction) / (_top - _floor));
        }

        /// <summary>Logs a traced body's progress, scale and drawn scale (<c>EVOSIM_THEATRE_CRUMBLE_TRACE</c>). Nothing for any other.</summary>
        public void Trace(long id, bool corpse, double second, float c, float s, float drawn)
        {
            if (TraceId == 0L || id != TraceId) return;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] crumble trace {0} {1}: second {2:0.###} c={3:0.#####} s={4:0.#####} drawn scale={5:0.#####}",
                corpse ? "corpse" : "remains", id, second, c, s, drawn));
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Says which bodies are crumbling at a second, for the next camera to draw their flakes: the remains whose last share of
        /// the window has begun and the corpses within the last stretch before they leave, and for <see cref="Life"/> after the end of
        /// each its flakes still drifting. Cheap; the work is done when a camera renders.
        /// </summary>
        /// <param name="second">The frame's second.</param>
        public void Advance(double second)
        {
            _second = second;
            _active.Clear();
            _worldCrumbling = 0;

            if (_remainsDrawn && _remainsWindow > 0f)
            {
                IReadOnlyList<FilmWindowRemains> list = _window.Remains;

                for (int i = 0; i < list.Count; i++)
                {
                    FilmWindowRemains r = list[i];
                    if (double.IsNaN(r.Since)) continue;

                    double start = r.Since + _remainsWindow * (1d - Share);
                    double end = r.Since + _remainsWindow;
                    if (second < start || second >= end + Life) continue;

                    if (!_remainsBodies.TryGetValue(r.Id, out CrumbleBody body))
                    {
                        body = new CrumbleBody { Id = r.Id, Remains = r, Order = _orders++, StartSecond = start, EndSecond = end };
                        _remainsBodies[r.Id] = body;
                    }

                    _active.Add(body);
                    if (second < end) _worldCrumbling++;
                }
            }

            if (_corpsesDrawn && _corpsesTimed)
            {
                if (!_corpsesReady) BuildCorpses();

                // Each corpse is visited once, so none is added twice.
                for (int i = 0; i < _corpseList.Count; i++)
                {
                    CrumbleBody body = _corpseList[i];
                    if (body.Unusable || second < body.StartSecond || second >= body.EndSecond + Life) continue;

                    _active.Add(body);
                    if (second < body.EndSecond) _worldCrumbling++;
                }
            }
        }

        /// <summary>
        /// Makes a body of every corpse the window holds and times it, once. A corpse that leaves inside the window is timed from
        /// the second it leaves, at its floor. One still standing at the window's end is timed from the window's own samples of it
        /// (<see cref="AnchorFromSamples"/>), so the schedule is the same whichever frame a film renders first.
        /// </summary>
        private void BuildCorpses()
        {
            _corpsesReady = true;
            IReadOnlyList<FilmWindowCorpse> corpses = _window.Corpses;

            for (int i = 0; i < corpses.Count; i++)
            {
                FilmWindowCorpse k = corpses[i];
                CrumbleBody body = CorpseBodyOf(k);
                _corpseList.Add(body);

                if (!double.IsPositiveInfinity(k.Gone)) Anchor(body, k.Gone, _floor);
                else if (!AnchorFromSamples(body)) body.Unusable = true;
            }
        }

        /// <summary>
        /// Times a corpse the window never sees leave: from the first sample of it that holds less than the share its crumble begins
        /// at (the share falls as it decays and is bitten, so the first is found by bisection), or from its first sample when none
        /// does, when its crumble begins later than the window shows. False when the window cannot place it at a sample.
        /// </summary>
        private bool AnchorFromSamples(CrumbleBody body)
        {
            FilmWindowCorpse k = body.CorpseRow;
            int last = k.SampleCount - 1;
            if (last < 0) return false;

            if (!_source.CorpseSample(k, last, out double atLast, out float heldLast)) return false;

            int pick = 0;

            if (heldLast < _top)
            {
                int lo = 0, hi = last;

                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (!_source.CorpseSample(k, mid, out _, out float held)) return false;

                    if (held < _top) hi = mid;
                    else lo = mid + 1;
                }

                pick = lo;
            }

            if (!_source.CorpseSample(k, pick, out double at, out float fraction) || !(fraction > 0f)) return false;

            Anchor(body, at, fraction);
            return true;
        }

        private CrumbleBody CorpseBodyOf(FilmWindowCorpse k)
        {
            if (_corpseBodies.TryGetValue(k.Id, out CrumbleBody body)) return body;

            body = new CrumbleBody { Id = k.Id, Corpse = true, CorpseRow = k, Order = _orders++ };
            _corpseBodies[k.Id] = body;
            return body;
        }

        private void Anchor(CrumbleBody body, double second, float fraction)
        {
            body.AnchorSecond = second;
            body.AnchorFraction = fraction;
            body.Anchored = true;
            body.StartSecond = BirthSecond(body, 0f);
            body.EndSecond = BirthSecond(body, 1f);
        }

        /// <summary>
        /// The second a flake of progress <paramref name="c"/> is shed at. Remains: along their window. A corpse: the second at which
        /// it holds the share <c>top - c * (top - floor)</c> of its first joules, from its anchor by its exponential decay.
        /// </summary>
        private double BirthSecond(CrumbleBody body, float c)
        {
            if (!body.Corpse) return body.StartSecond + (double)c * (_remainsWindow * (double)Share);

            double f = _top - (double)c * (_top - _floor);
            return body.AnchorSecond + Math.Log(body.AnchorFraction / f) / _rate;
        }

        // ---------------------------------------------------------------- the camera

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
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
                // A cosmetic layer never takes the film down; the flakes are simply absent for this render.
                _drawMesh = null;
                if (_mesh != null) _mesh.Clear();
                Debug.LogWarning("[Theatre] crumble: the flakes could not be built for this render: " + e.GetType().Name + ": " + e.Message);
            }

            // With the lens armed the shells are drawn after it, sharp; the flakes follow them.
            _renderer.forceRenderingOff = lens;

            if (lens && _drawMesh != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_pass);
            }
        }

        private void Build(Camera camera)
        {
            Transform view = camera.transform;

            TheatreSkin skin = TheatreSkin.Current;
            Material shells = skin != null ? skin.SnowShellsMaterial : null;

            // The run's water is opened by the first push of a frame; until then a flake would be cached with no current and no sink.
            bool water = skin != null && (skin.RunWaterOpen || !skin.RunWaterWanted);

            bool same = ReferenceEquals(_builtCamera, camera) && _second == _builtSecond && view.position == _builtEye &&
                        view.rotation == _builtRotation && camera.fieldOfView == _builtFov && camera.aspect == _builtAspect &&
                        water == _builtWater;
            if (same) return;

            _builtCamera = camera;
            _builtSecond = _second;
            _builtEye = view.position;
            _builtRotation = view.rotation;
            _builtFov = camera.fieldOfView;
            _builtAspect = camera.aspect;
            _builtWater = water;

            // The status line counts rendered seconds: the first new whole second, then every 30th.
            bool newSecond = !double.IsNaN(_second) && Math.Floor(_second) != _statsSecond;

            if (newSecond)
            {
                _statsSecond = Math.Floor(_second);
                _newSeconds++;
            }

            _drawn = 0;
            _skipped = 0;
            _near.Clear();
            _positions.Clear();
            _corners.Clear();
            _data.Clear();
            _colours.Clear();
            _triangles.Clear();
            _drawMesh = null;

            if (double.IsNaN(_second) || skin == null || shells == null || _material == null)
            {
                if (!_noShellsTold && !double.IsNaN(_second) && _material != null)
                {
                    _noShellsTold = true;
                    Debug.Log("[Theatre] crumble: the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the flakes cannot be drawn in the water's box; the bodies still crumble");
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

            // The sink the flakes fall at: the run's, once its water is open; for a run whose water is never opened (snow fields, drift
            // and the bend with the current all off) the shells' own fall rate, with no current.
            bool open = skin.RunWaterOpen;
            _fall = shells.GetFloat("_FallMetresPerSecond");
            float sink = open ? skin.SnowSinkMetresPerSecond : _fall;

            // The bodies near the view: a sphere about the body, grown by the furthest a flake can drift in its life, meets the frustum.
            for (int i = 0; i < _active.Count; i++)
            {
                CrumbleBody body = _active[i];
                if (body.Unusable) continue;
                if (!_source.PositionOf(body, _second, out Vector3 at)) continue;

                Vector3 v = open ? skin.WaterAt(at, _second) : Vector3.zero;
                float reach = body.Scheduled ? body.Reach : 3f;
                float radius = reach + (v.magnitude + sink * (1f + SinkJitter * 0.5f)) * Life + 0.1f;

                if (InFrustum(at, radius)) _near.Add(body);
            }

            for (int i = 0; i < _near.Count; i++)
            {
                if (!_near[i].Scheduled) Schedule(_near[i]);
            }

            Place(skin);
            Emit();

            if (_drawn > 0)
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

            if (newSecond && (_newSeconds == 1 || _newSeconds % 30 == 0))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] crumble: second {0:0.###}, {1} bodies crumbling in the world, {2} near the view, {3} flakes drawn, {4} skipped (no pose found)",
                    _second, _worldCrumbling, _near.Count, _drawn, _skipped));
            }
        }

        private bool InFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < _planes.Length; i++)
            {
                if (_planes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- the schedule of a body's flakes

        private void Schedule(CrumbleBody body)
        {
            body.Scheduled = true;

            if (!_source.Measure(body, out float volume, out float reach) || !(volume > 0f))
            {
                body.Unusable = true;
                return;
            }

            if (!body.Corpse)
            {
                // A remains body shed from a window that never holds it, or that it left early, cannot shed what it did not hold.
                if (!_window.TryFramesOf(body.Id, out _, out int last))
                {
                    body.Unusable = true;
                    return;
                }

                double lastSecond = _window.SecondOf(last);
                double windowEnd = _window.SecondOf(_window.FrameCount - 1);
                double spacing = _window.FrameCount > 1 ? _window.SecondOf(1) - _window.SecondOf(0) : 1d;
                double slack = Math.Max(1d, 2d * spacing);

                if (lastSecond < windowEnd - slack && lastSecond < body.EndSecond - slack) body.ShedUntil = lastSecond;
            }

            float flakeVolume = 4f / 3f * Mathf.PI * FlakeRadius * FlakeRadius * FlakeRadius;
            int n = Mathf.Clamp(Mathf.RoundToInt(volume / flakeVolume), FlakesMin, FlakesMax);

            body.Reach = reach;
            body.Volume = volume;
            body.Count = n;
            body.C = new float[n];
            body.T = new double[n];
            body.State = new byte[n];
            body.Flakes = new CrumbleFlake[n];

            for (int i = 0; i < n; i++)
            {
                // The births do not follow the volume lost. With the scale (1 - c)^(1/3) the volume is linear in c, so flakes shed by
                // volume would leave evenly along the shrink and nothing would burst at the end. Flake i is stratified in the unit
                // interval, u = (i + h) / n, and takes the progress c = u^(1/p) of the distribution function F(c) = c^p, so most of
                // the flakes leave late, in the last stretch where the shrink is fastest (p is EVOSIM_THEATRE_CRUMBLE_BURST).
                float u = (i + Hash01(body.Id, i, 6, body.Corpse)) / n;
                float c = Mathf.Pow(u, 1f / Burst);

                body.C[i] = c;
                body.T[i] = BirthSecond(body, c);
            }
        }

        private static int FirstAfter(double[] times, double second)
        {
            int lo = 0, hi = times.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (times[mid] > second) hi = mid;
                else lo = mid + 1;
            }

            return lo;
        }

        /// <summary>Places the flakes of the near bodies that are alive and not yet placed: poses read frame by frame, once each.</summary>
        private void Place(TheatreSkin skin)
        {
            _needs.Clear();

            // A flake caches the current and the sink at its birth, which read zero until the first push of a frame opens the run's
            // water: so nothing is placed before then, and a render before the first push draws none.
            if (skin.RunWaterWanted && !skin.RunWaterOpen) return;

            for (int b = 0; b < _near.Count; b++)
            {
                CrumbleBody body = _near[b];
                if (body.Unusable || body.Count == 0) continue;

                int lo = FirstAfter(body.T, _second - Life);
                int hi = FirstAfter(body.T, _second) - 1;

                for (int i = lo; i <= hi; i++)
                {
                    if (body.State[i] != 0) continue;

                    if (body.T[i] > body.ShedUntil)
                    {
                        body.State[i] = 3;
                        continue;
                    }

                    int frame = _source.FrameFor(body, Math.Floor(body.T[i] / PoseSnapSeconds) * PoseSnapSeconds);

                    if (frame == -1)
                    {
                        body.State[i] = 2;
                        continue;
                    }

                    _needs.Add(new Need { Frame = frame, Order = body.Order, Body = body, Index = i });
                }
            }

            if (_needs.Count == 0) return;

            _needs.Sort(NeedOrder);

            int n = _needs.Count;
            int a = 0;

            while (a < n)
            {
                int frame = _needs[a].Frame;
                int end = a;
                while (end < n && _needs[end].Frame == frame) end++;

                PoseFrame poses = null;
                bool read = true;

                if (frame >= 0)
                {
                    try { poses = _source.ReadFrame(frame); }
                    catch (Exception) { read = false; }
                }

                int g = a;

                while (g < end)
                {
                    CrumbleBody body = _needs[g].Body;
                    int h = g;
                    while (h < end && ReferenceEquals(_needs[h].Body, body)) h++;

                    bool laid = false, ok = false;

                    for (int k = g; k < h; k++)
                    {
                        int index = _needs[k].Index;

                        // A remains body is laid once for the frame; a corpse is laid at each flake's own second.
                        if (read && (body.Corpse || !laid))
                        {
                            ok = _source.Lay(body, poses, body.T[index], _layout);
                            laid = true;
                        }

                        if (!read || !ok)
                        {
                            body.State[index] = 2;
                            continue;
                        }

                        body.Flakes[index] = MakeFlake(body, index, _layout, skin);
                        body.State[index] = 1;
                    }

                    g = h;
                }

                a = end;
            }
        }

        /// <summary>One flake at its birth: a part by surface area, a point on its box, the current there and its own sinking.</summary>
        private CrumbleFlake MakeFlake(CrumbleBody body, int index, CrumbleLayout layout, TheatreSkin skin)
        {
            long id = body.Id;
            bool corpse = body.Corpse;
            float c = body.C[index];
            float k = layout.BaseScale * ScaleAt(c);
            Phenotype shape = layout.Shape;

            float total = 0f;

            for (int p = 0; p < shape.PartCount; p++)
            {
                total += SurfaceArea(shape.Parts[p].HalfExtents);
            }

            float pick = Hash01(id, index, 1, corpse) * total;
            int part = shape.PartCount - 1;
            float acc = 0f;

            for (int p = 0; p < shape.PartCount; p++)
            {
                acc += SurfaceArea(shape.Parts[p].HalfExtents);

                if (pick < acc)
                {
                    part = p;
                    break;
                }
            }

            Float3 h = shape.Parts[part].HalfExtents;
            Vector3 local = SurfacePoint(
                Mathf.Abs(h.X), Mathf.Abs(h.Y), Mathf.Abs(h.Z),
                Hash01(id, index, 2, corpse), Hash01(id, index, 3, corpse), Hash01(id, index, 4, corpse), Hash01(id, index, 5, corpse));

            Vector3 inBody = layout.Positions[part] + layout.Rotations[part] * local;
            Vector3 birth = layout.Anchor + k * (inBody - layout.Centre);

            double second = body.T[index];
            // The run's current and sink once its water is open; the shells' own fall rate and no current where it never is.
            bool open = skin.RunWaterOpen;
            Vector3 current = open ? skin.WaterAt(birth, second) : Vector3.zero;
            float sink = (open ? skin.SnowSinkMetresPerSecond : _fall) * (1f + SinkJitter * (Hash01(id, index, 7, corpse) - 0.5f));

            float period = Mathf.Lerp(WobblePeriodLow, WobblePeriodHigh, Hash01(id, index, 11, corpse));

            return new CrumbleFlake
            {
                Birth = birth,
                Drift = current + new Vector3(0f, -sink, 0f),
                Tissue = TissueColour(id, shape, part, c, corpse),
                Phase = new Vector3(
                    6.2831853f * Hash01(id, index, 8, corpse), 6.2831853f * Hash01(id, index, 9, corpse), 6.2831853f * Hash01(id, index, 10, corpse)),
                Omega = 6.2831853f / period,
                Seed = Hash01(id, index, 12, corpse) * 97f,
            };
        }

        private static float SurfaceArea(Float3 h)
        {
            float x = Mathf.Abs(h.X), y = Mathf.Abs(h.Y), z = Mathf.Abs(h.Z);
            return 8f * (x * y + y * z + x * z);
        }

        /// <summary>
        /// The colour a flake starts in: the part's colour as the palette paints it (<see cref="TheatrePalette.PaintedGuildOf"/>: the
        /// guild's, the marine producers' recolour and the lineage's turn of hue, and plain white when the guilds are not painted),
        /// dulled as the remains are at its birth (greyness and brightness taken toward a corpse's by the progress), or a corpse's
        /// grey; in the colour space the shader mixes it in.
        /// </summary>
        private Color TissueColour(long id, Phenotype shape, int part, float c, bool corpse)
        {
            TheatrePalette palette = _source.Palette;
            bool on = _source.ColourByCellType;

            // A corpse is painted under a key of its own (the film window's CorpseKey), never its living id.
            Color guild = palette != null ? palette.PaintedGuildOf(corpse ? -(id + 1L) : id, shape, part, on, corpse) : new Color(0.5f, 0.5f, 0.5f, 1f);
            float brightness;

            if (palette == null || !on)
            {
                brightness = 1f;
            }
            else if (corpse)
            {
                guild = TheatrePalette.Grey(guild);
                brightness = palette.CorpseBrightness;
            }
            else
            {
                guild = Color.Lerp(guild, TheatrePalette.Grey(guild), Mathf.Clamp01(Mathf.Lerp(palette.RemainsGreyness, 1f, c)));
                brightness = Mathf.Lerp(palette.RemainsBrightness, palette.CorpseBrightness, c);
            }

            var dulled = new Color(guild.r * brightness, guild.g * brightness, guild.b * brightness, 1f);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? dulled.linear : dulled;
        }

        // ---------------------------------------------------------------- the quads

        /// <summary>Writes a quad for every placed flake of the near bodies that is alive and in the frustum.</summary>
        private void Emit()
        {
            for (int b = 0; b < _near.Count; b++)
            {
                CrumbleBody body = _near[b];
                if (body.Unusable || body.Count == 0) continue;

                int lo = FirstAfter(body.T, _second - Life);
                int hi = FirstAfter(body.T, _second) - 1;

                for (int i = lo; i <= hi; i++)
                {
                    if (body.State[i] == 2)
                    {
                        _skipped++;
                        continue;
                    }

                    if (body.State[i] != 1) continue;

                    float age = (float)(_second - body.T[i]);
                    if (age < 0f || age >= Life) continue;

                    CrumbleFlake f = body.Flakes[i];

                    // Its birth point, carried by the water it was born into and sinking, with a small wobble that starts at nothing.
                    Vector3 place = f.Birth + f.Drift * age;
                    place.x += WobbleMetres * (Mathf.Sin(f.Omega * age + f.Phase.x) - Mathf.Sin(f.Phase.x));
                    place.y += 0.5f * WobbleMetres * (Mathf.Sin(1.17f * f.Omega * age + f.Phase.y) - Mathf.Sin(f.Phase.y));
                    place.z += WobbleMetres * (Mathf.Sin(0.83f * f.Omega * age + f.Phase.z) - Mathf.Sin(f.Phase.z));

                    if (!InFrustum(place, FlakeMargin)) continue;

                    // Dissolving over its last seconds: the matter shrinks away, it does not fade.
                    float left = Mathf.Clamp01((Life - age) / DissolveSeconds);
                    float radius = FlakeRadius * (left * left * (3f - 2f * left));
                    if (!(radius > 0f)) continue;

                    int first = _positions.Count;
                    var data = new Vector4(radius, f.Seed, age, 0f);

                    for (int corner = 0; corner < 4; corner++)
                    {
                        _positions.Add(place);
                        _corners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                        _data.Add(data);
                        _colours.Add(f.Tissue);
                    }

                    _triangles.Add(first);
                    _triangles.Add(first + 1);
                    _triangles.Add(first + 2);
                    _triangles.Add(first);
                    _triangles.Add(first + 2);
                    _triangles.Add(first + 3);

                    _drawn++;
                }
            }
        }

        // ---------------------------------------------------------------- the draw after the lens

        /// <summary>
        /// Draws the flakes after the theatre's lens, as the lens draws the shells: the lens would blur a flake by the depth of the
        /// bed behind it, and a flake carries its own blur (<c>_EvoSnowLens</c>).
        /// </summary>
        private sealed class DrawPass : ScriptableRenderPass
        {
            private readonly TheatreCrumble _owner;

            public DrawPass(TheatreCrumble owner)
            {
                _owner = owner;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private sealed class PassData
            {
                public Mesh Mesh;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Mesh mesh = _owner._drawMesh;
                Material material = _owner._material;
                if (mesh == null || material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre Crumble Flakes", out PassData data))
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

        // ---------------------------------------------------------------- housekeeping

        public void Dispose()
        {
            if (_hooked)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                _hooked = false;
            }

            if (_renderer != null) _renderer.forceRenderingOff = false;
            if (_object != null) UnityEngine.Object.DestroyImmediate(_object);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);

            _object = null;
            _renderer = null;
            _mesh = null;
            _drawMesh = null;
            _material = null;
            _pass = null;
            _remainsBodies.Clear();
            _corpseBodies.Clear();
            _active.Clear();
            _near.Clear();
        }
    }
}
