using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Evosim.Core;
using Evosim.Farm;
using Evosim.Sim;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The wound plume (<c>EVOSIM_THEATRE_WOUND_PLUME</c>, <see cref="WoundPlume"/>): at each recorded blow that hurts an animal (a kill,
    /// a cut or a wound) a dark red cloud released at the bitten place, which spreads, swells and drifts off with the run's water and
    /// fades over a few seconds, so the eye reads that an attack happened.
    /// Every hurt animal bleeds (D286), and a kill bleeds more: a kill's plume has <see cref="PlumeKillMoteFactor"/> times the motes,
    /// spreads <see cref="PlumeKillSpreadFactor"/> times as far, has larger motes (<see cref="PlumeKillRadiusFactor"/>) and lingers
    /// <see cref="PlumeKillLifeFactor"/> times as long as a cut's or a wound's, which are drawn alike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> The dial is read only with the bite pulse on (<c>EVOSIM_THEATRE_MOUTH2_BITE</c>),
    /// whose onsets it reads, and is independent of the crumbs' dial: the plume works with the crumbs off, and the crumbs draw what
    /// they drew with the plume on. <see cref="Frame"/> calls <see cref="PlumeFrame"/> only with the dial on, beside the crumbs' call;
    /// with it off no hook is registered, and no object, mesh, material or log line is made.
    /// </para>
    /// <para>
    /// <b>Which blows.</b> The crumbs' own classification, by the crumbs' own functions (<see cref="CrumbKillOf"/>,
    /// <see cref="CrumbVictimTint"/>, <see cref="CrumbNearestPrey"/>): a kill, a cut or a wound whose prey is an animal. A leaf, a corpse,
    /// remains or an unknown prey gets no plume. The catch rule is the crumbs': an onset whose own frame the film skips is captured at
    /// the first shown frame within <see cref="CrumbCatchFilmSeconds"/> of film after it, or not at all.
    /// </para>
    /// <para>
    /// <b>Where.</b> The bitten part's surface point nearest the mouth, at the frame the onset is captured, where that part of the prey
    /// is drawn there; else the mouth point (a kill swallows its victim in the pass that sets the bit, so a kill's plume is born at the
    /// mouth).
    /// </para>
    /// <para>
    /// <b>A mote is a function of (eater, part, onset, index)</b>, hashed as the crumbs are with salts of its own; its place at a second
    /// is its birth, its spread easing out, the current read once at the wound point and the onset's second times its age, and a slow
    /// wobble. Drawn as the crumbs are (a mesh of camera-facing quads rebuilt at each game camera's render, a pass after the lens while
    /// it is armed), by the crumble's flake shader's dark-mote path (<c>uv1.w = 2</c>): an unlit soft disc returned premultiplied, in a
    /// material of its own blending One, OneMinusSrcAlpha, so the plume darkens what lies behind it toward its colour. The vertex
    /// colour's alpha is the opacity, and the fade lowers it.
    /// </para>
    /// <para>
    /// <b>The haze</b> (<c>EVOSIM_THEATRE_WOUND_PLUME_HAZE</c>, <see cref="WoundPlumeHaze"/>, read only with the plume on, 0 by
    /// default): the plume drawn in place of the discs, which at full size read as overlapping saturated discs whose circles showed at
    /// the thin edge, by the death cue's haze shader (<c>Evosim/Theatre Death Haze</c>) as it is. A few large, soft, noise-broken puffs
    /// (<c>uv1.w = 0</c>) are born at the wound point over the first fraction of a second, detach from it as they age along a way bent
    /// downstream, are carried by the current each at its own share of its speed, and grow and thin, so the blood trails off in a
    /// stream; the shader cuts each into lumps with real gaps. A few fine ragged specks (<c>uv1.w = 1</c>, 0.3 to 1.5 mm) spread less and sink. Each eases in from its own hashed start and dissolves before
    /// its own end within the plume's life, in a deeper, darker red than the discs' and far less opaque (D262's dial-back), so the
    /// bitten body stays readable through it. A kill's haze has more puffs and specks, spread wider and larger (D286). The material,
    /// the puffs and the specks are made only with the dial on; with it off <see cref="WoundPlumeHaze"/> is false, every line of the
    /// haze below is behind it, and the discs are drawn as they always were.
    /// </para>
    /// </remarks>
    public static partial class TheatreMouthBite
    {
        // ---------------------------------------------------------------- the dial

        /// <summary>
        /// The wound plume (<c>EVOSIM_THEATRE_WOUND_PLUME</c>, 0 or 1, 0 off by default), read only with <see cref="TheatreMouth2.Bite"/>
        /// on, whose onsets it reads. Independent of <see cref="TheatreMouth2.BiteCrumbs"/>.
        /// </summary>
        public static readonly bool WoundPlume =
            TheatreMouth2.Bite > 0f && TheatreSkin.Dial("EVOSIM_THEATRE_WOUND_PLUME", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The plume's haze (<c>EVOSIM_THEATRE_WOUND_PLUME_HAZE</c>, 0 or 1, 0 off by default), read once and only with
        /// <see cref="WoundPlume"/> on: soft puffs and fine specks on the death haze's shader in place of the discs. False leaves every
        /// frame as it was. Declared after <see cref="WoundPlume"/> in this file, so it is initialised after it.
        /// </summary>
        public static readonly bool WoundPlumeHaze =
            WoundPlume && TheatreSkin.Dial("EVOSIM_THEATRE_WOUND_PLUME_HAZE", 0f, 0f, 1f) >= 0.5f;

        // ---------------------------------------------------------------- the constants

        /// <summary>How long a plume lingers, film seconds: twice a crumb's life.</summary>
        public const float PlumeLifeSeconds = 2f * CrumbLifeSeconds;

        /// <summary>Motes a plume (D262: started overpowered, to be dialled back).</summary>
        private const int PlumeMotes = 40;

        /// <summary>A mote's mean birth radius as a multiple of a crumb's mean (before the crumbs' kind factor).</summary>
        private const float PlumeRadiusShare = 2f;

        /// <summary>A mote's radius grows to 1 plus this many times its birth radius, easing out over <see cref="PlumeGrowSeconds"/>.</summary>
        private const float PlumeGrowth = 2f;

        private const float PlumeGrowSeconds = 2f;

        /// <summary>How far a mote spreads at most, in mouth radii, and the film seconds over which the spread eases out.</summary>
        private const float PlumeSpreadRadii = 4f;

        private const float PlumeSpreadSeconds = 1.5f;

        /// <summary>How packed the motes are born about the wound point, in mouth radii.</summary>
        private const float PlumeBirthRadii = 0.5f;

        /// <summary>The share of the life, at its end, over which a mote fades to nothing.</summary>
        private const float PlumeFadeShare = 0.7f;

        /// <summary>The wobble of a mote about its path, metres, and the range of its period, film seconds: slower than a crumb's.</summary>
        private const float PlumeWobbleMetres = 0.003f;

        private const float PlumeWobbleLow = 2.5f;
        private const float PlumeWobbleHigh = 4.5f;

        /// <summary>The share of the prey's painted colour blended into the red, and the brightest any channel may then be (display colour).</summary>
        private const float PlumePreyShare = 0.25f;

        private const float PlumeDarkMost = 0.4f;

        /// <summary>A mote's opacity at the centre of its disc before it fades (D262: overpowered, to be dialled back).</summary>
        private const float PlumeOpacity = 0.8f;

        /// <summary>A kill's plume has this many times a wound's motes (D286: a kill bleeds more).</summary>
        private const float PlumeKillMoteFactor = 2f;

        /// <summary>A kill's motes spread this many times as far as a wound's.</summary>
        private const float PlumeKillSpreadFactor = 1.5f;

        /// <summary>A kill's motes are this many times the radius of a wound's.</summary>
        private const float PlumeKillRadiusFactor = 1.25f;

        /// <summary>A kill's plume lingers this many times as long as a wound's.</summary>
        private const float PlumeKillLifeFactor = 1.4f;

        /// <summary>The plume's red, in display colour (sRGB).</summary>
        private static readonly Color PlumeRed = new Color(0.35f, 0.02f, 0.03f, 1f);

        // ---------------------------------------------------------------- the haze's constants (EVOSIM_THEATRE_WOUND_PLUME_HAZE)

        /// <summary>The haze's shader: the death cue's, used as it is.</summary>
        private const string PlumeHazeShaderName = "Evosim/Theatre Death Haze";

        /// <summary>The haze's red, in display colour (sRGB): deeper and darker than <see cref="PlumeRed"/>, unlit like the death haze's tints.</summary>
        private static readonly Color PlumeHazeRed = new Color(0.3f, 0.02f, 0.03f, 1f);

        /// <summary>The share of the prey's painted colour blended into the haze's red, and the brightest any channel may then be (display colour).</summary>
        private const float PlumeHazePreyShare = 0.1f;

        private const float PlumeHazeDarkMost = 0.32f;

        /// <summary>Puffs a wound's or a cut's haze; a kill's has <see cref="PlumeKillMoteFactor"/> times as many (D286). One more than wound4's, for the stream.</summary>
        private const int PlumeHazePuffs = 6;

        /// <summary>
        /// A puff's birth radius as a multiple of a mote's mean (the kill's factor included), <c>lerp(low, high, h^power)</c>: most small,
        /// a few large (D262's strong start: a wider range than wound4's 2.2 to 3.4).
        /// </summary>
        private const float PlumeHazePuffShareLow = 1.6f;

        private const float PlumeHazePuffShareHigh = 4f;
        private const float PlumeHazePuffSharePower = 1.6f;

        /// <summary>
        /// How far a puff detaches from the wound over the plume's life, in mouth radii (a kill's times <see cref="PlumeKillSpreadFactor"/>),
        /// hashed between these: from nothing to about one or two of the victim's half-extents, eased out over its own age (D262's strong start).
        /// </summary>
        private const float PlumeHazeDetachRadiiLow = 4f;

        private const float PlumeHazeDetachRadiiHigh = 8f;

        /// <summary>
        /// The weight of the downstream direction (the current at the wound) against a puff's or a speck's own way out, when it is bent:
        /// the blood trails downstream. Below <see cref="PlumeHazeCurrentFloor"/> m/s there is no downstream, and no bend.
        /// </summary>
        private const float PlumeHazeDownstreamLean = 1.5f;

        private const float PlumeHazeCurrentFloor = 1e-4f;

        /// <summary>A puff is carried by the current at <c>1 ± PlumeHazePuffCarrySpread</c> of its speed, hashed, so the puffs string out along the flow (D262's strong start).</summary>
        private const float PlumeHazePuffCarrySpread = 0.3f;

        /// <summary>
        /// A puff's radius grows to 1 plus this many times its birth radius as it detaches, and its opacity thins as the growth to this
        /// power: the haze spreads rather than thickens.
        /// </summary>
        private const float PlumeHazePuffGrow = 1.2f;

        private const float PlumeHazePuffThinPower = 0.8f;

        /// <summary>
        /// A puff's opacity at full strength, before its thinning (D262's dial-back, from the discs' <see cref="PlumeOpacity"/>): low, so
        /// several overlapping blows still leave the bitten body readable. 0.25 with a steeper thinning barely showed at 1x (phase109,
        /// wound3-80323); 0.55 read quietly (wound4-80323); 0.62 keeps that strength or a little more now the shader cuts gaps in each puff.
        /// </summary>
        private const float PlumeHazePuffOpacity = 0.62f;

        /// <summary>
        /// The film seconds within which each puff starts, hashed, and over which each eases in from nothing: released over this, so the
        /// earliest are furthest off and the blood leaves in a stream (wound4's was 0.2).
        /// </summary>
        private const float PlumeHazePuffStagger = 0.6f;

        private const float PlumeHazePuffInSeconds = 0.2f;

        /// <summary>A puff's end, <c>lerp(PlumeHazePuffEndLow, 1, h)</c> of the plume's life; it dissolves over <see cref="PlumeFadeShare"/> of it.</summary>
        private const float PlumeHazePuffEndLow = 0.7f;

        /// <summary>A puff's shade about the haze's red, hashed between these: never above it.</summary>
        private const float PlumeHazePuffShadeLow = 0.85f;

        private const float PlumeHazePuffShadeHigh = 1f;

        /// <summary>How far a puff's quad reaches, in its radius: the haze shader's window ends there (EVO_HAZE_REACH).</summary>
        private const float PlumeHazePuffReach = 1.6f;

        /// <summary>Specks a wound's or a cut's haze; a kill's has <see cref="PlumeKillMoteFactor"/> times as many (D286). Fewer than wound4's 6.</summary>
        private const int PlumeHazeSpecks = 4;

        /// <summary>
        /// A speck's radius, metres: fine particulate, drawn from a power law truncated to this range, its density falling as the radius
        /// to the power <c>-(1 + PlumeHazeSpeckPower)</c>, so most specks are near the small end.
        /// </summary>
        private const float PlumeHazeSpeckRadiusLow = 0.0003f;

        private const float PlumeHazeSpeckRadiusHigh = 0.0015f;
        private const float PlumeHazeSpeckPower = 1.6f;

        /// <summary>The least share of the motes' furthest spread a speck spreads, hashed up to all of it.</summary>
        private const float PlumeHazeSpeckSpreadLow = 0.25f;

        /// <summary>How fast a speck sinks, m/s of the run, from the smallest to the largest, varied by <see cref="PlumeHazeSpeckSinkJitter"/> either way.</summary>
        private const float PlumeHazeSpeckSinkLow = 0.006f;

        private const float PlumeHazeSpeckSinkHigh = 0.012f;
        private const float PlumeHazeSpeckSinkJitter = 0.4f;

        /// <summary>A speck's opacity at full strength: a fine dark grain, denser than a puff (D262's strong start; the shader thins the sub-pixel ones).</summary>
        private const float PlumeHazeSpeckOpacity = 0.7f;

        /// <summary>The film seconds within which each speck starts, hashed, and over which each eases in.</summary>
        private const float PlumeHazeSpeckStagger = 0.15f;

        private const float PlumeHazeSpeckInSeconds = 0.15f;

        /// <summary>A speck's end, <c>lerp(PlumeHazeSpeckEndLow, 1, h)</c> of the plume's life, and the share of it over which it dissolves.</summary>
        private const float PlumeHazeSpeckEndLow = 0.6f;

        private const float PlumeHazeSpeckSpanShare = 0.4f;

        /// <summary>A speck's shade about the haze's red, hashed between these: darker than the puffs, a clot and not a pink dot (wound4's 0.6 to 0.85).</summary>
        private const float PlumeHazeSpeckShadeLow = 0.45f;

        private const float PlumeHazeSpeckShadeHigh = 0.7f;

        // ---------------------------------------------------------------- one blow's plume

        /// <summary>One mote, fixed at its birth.</summary>
        private struct PlumeMote
        {
            public Vector3 Birth;

            /// <summary>Where the spread takes it, relative to its birth, reached as the spread eases out.</summary>
            public Vector3 Spread;

            public float Radius;
            public Vector3 Phase;
            public float Omega;
            public float Seed;
        }

        /// <summary>One puff of the haze (<see cref="WoundPlumeHaze"/> only), fixed at its birth.</summary>
        private struct PlumePuff
        {
            public Vector3 Birth;

            /// <summary>Where the spread takes it, relative to its birth, reached as the spread eases out.</summary>
            public Vector3 Spread;

            /// <summary>Its radius at birth, before it grows.</summary>
            public float Radius;

            /// <summary>The share of the current's speed it is carried at.</summary>
            public float Carry;

            /// <summary>The film second of the plume's age at which it starts, the one at which it is gone, and its dissolving stretch.</summary>
            public float Start;

            public float End;
            public float Span;
            public float Seed;
            public float Shade;
        }

        /// <summary>One speck of the haze (<see cref="WoundPlumeHaze"/> only), fixed at its birth.</summary>
        private struct PlumeSpeck
        {
            public Vector3 Birth;
            public Vector3 Spread;
            public float Radius;
            public float Start;
            public float End;
            public float Span;
            public float Sink;
            public Vector3 Phase;
            public float Omega;
            public float Seed;
            public float Shade;
        }

        // How long a bite's plume lingers, film seconds: a kill's longer than a cut's or a wound's.
        private static float PlumeLifeOf(PlumeBite bite)
        {
            return bite.Match == CrumbKill ? PlumeLifeSeconds * PlumeKillLifeFactor : PlumeLifeSeconds;
        }

        /// <summary>One onset's plume: the blow, its kind, its prey, the wound point and colour, and the motes.</summary>
        private sealed class PlumeBite
        {
            public long Eater;
            public int Part;
            public double Onset;

            public int Match;
            public long Victim = -1L;
            public int VictimPart = -1;

            /// <summary>The colour, in the colour space the shader mixes it in.</summary>
            public Color Colour;

            public Vector3 Wound;

            /// <summary>The current at the wound point at the onset, read once the run's water is open (or known to stay shut).</summary>
            public bool CurrentKnown;

            public Vector3 Current;

            public PlumeMote[] Motes;

            /// <summary>
            /// The haze's colour (in the colour space the shader mixes it in), puffs and specks (<see cref="WoundPlumeHaze"/>); with the
            /// haze's dial off the colour is never set and the arrays are null.
            /// </summary>
            public Color HazeColour;

            public PlumePuff[] Puffs;
            public PlumeSpeck[] Specks;
        }

        // ---------------------------------------------------------------- the state

        private static FilmWindowWorld _plumeWindow;
        private static int _plumeFrame = -1;
        private static double _plumeSecond = double.NaN;
        private static double _plumeSpeed = 1d;

        /// <summary>The plumes of this pass of the window still lingering, in the order they were captured.</summary>
        private static readonly List<PlumeBite> PlumeBites = new List<PlumeBite>();

        /// <summary>The onsets seen in this pass, and those counted for the window's log line across every pass.</summary>
        private static readonly HashSet<(long, int, double)> PlumeSeen = new HashSet<(long, int, double)>();

        private static readonly HashSet<(long, int, double)> PlumeCounted = new HashSet<(long, int, double)>();
        private static readonly List<BiteOnset> PlumeOnsets = new List<BiteOnset>();

        private static int _plumeKills;
        private static int _plumeCuts;
        private static int _plumeWounds;
        private static PlumeBite _plumeFirst;
        private static bool _plumeLogged;

        // The drawing.
        private static GameObject _plumeObject;
        private static MeshRenderer _plumeRenderer;
        private static Mesh _plumeMesh;
        private static Mesh _plumeDrawMesh;
        private static Material _plumeMaterial;
        private static PlumePass _plumePass;
        private static bool _plumeNoShader;
        private static bool _plumeNoShellsTold;

        private static readonly List<Vector3> PlumePositions = new List<Vector3>();
        private static readonly List<Vector2> PlumeCorners = new List<Vector2>();
        private static readonly List<Vector4> PlumeData = new List<Vector4>();
        private static readonly List<Color> PlumeColours = new List<Color>();
        private static readonly List<int> PlumeTriangles = new List<int>();
        private static readonly Plane[] PlumePlanes = new Plane[6];

        private static double _plumeBuiltSecond = double.NaN;
        private static Camera _plumeBuiltCamera;
        private static Vector3 _plumeBuiltEye;
        private static Quaternion _plumeBuiltRotation;
        private static float _plumeBuiltFov;
        private static float _plumeBuiltAspect;

        private static bool _plumeHooked;

        /// <summary>Whether the haze's one log line has been written (<see cref="WoundPlumeHaze"/> only).</summary>
        private static bool _plumeHazeLogged;

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="Frame"/> with every rendered frame of a film window, with the plume's dial on and the crumbs' on or off:
        /// captures the plume of each onset of the last <see cref="PlumeLifeSeconds"/> of film not yet seen in this pass, at its own
        /// frame (or the first shown just after it), and forgets the plumes that are gone.
        /// </summary>
        private static void PlumeFrame(FilmWindowWorld window, BiteOnsets onsets, double speed)
        {
            if (window == null || onsets == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;
            if (!EnsurePlumeDrawer()) return;

            bool replaced = !ReferenceEquals(window, _plumeWindow);
            bool reopened = replaced || window.FrameIndex < _plumeFrame;

            if (!reopened && window.FrameIndex == _plumeFrame) return;

            if (replaced)
            {
                // The window before, if its last frame was never shown: its line now.
                if (_plumeWindow != null && !_plumeLogged) Debug.Log(PlumeWindowLine());

                PlumeCounted.Clear();
                _plumeKills = 0;
                _plumeCuts = 0;
                _plumeWounds = 0;
                _plumeFirst = null;
                _plumeLogged = false;
            }

            if (reopened)
            {
                PlumeBites.Clear();
                PlumeSeen.Clear();
            }

            double second = window.Second;
            double life = PlumeLifeSeconds * speed;
            FilmWindowReader reader = window.Window;

            _plumeWindow = window;
            _plumeFrame = window.FrameIndex;
            _plumeSecond = second;
            _plumeSpeed = speed;

            onsets.Between(second - life, second, speed, PlumeOnsets);

            for (int i = 0; i < PlumeOnsets.Count; i++)
            {
                BiteOnset onset = PlumeOnsets[i];
                var key = (onset.Id, onset.Part, onset.Second);
                if (PlumeSeen.Contains(key)) continue;

                int at = reader.NearestFrame(onset.Second);
                if (at > window.FrameIndex) continue;

                PlumeSeen.Add(key);

                // Its own frame skipped and none shown near enough after it: no plume, as no crumbs.
                if (at != window.FrameIndex && (second - onset.Second) / speed > CrumbCatchFilmSeconds) continue;

                PlumeBite bite = CapturePlume(window, onset);
                if (bite == null) continue;

                PlumeBites.Add(bite);

                // One line a plume, as the crumbs write one a shed: a film shorter than its window never reaches the window's line.
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] wound plume: t {0:0.###} eater {1} part {2}, shown at frame {3} (t {4:0.###}): {5}, victim {6} part {7}.",
                    onset.Second, onset.Id, onset.Part, window.FrameIndex, second, CrumbMatchWords[bite.Match], bite.Victim, bite.VictimPart));

                if (PlumeCounted.Add(key))
                {
                    if (bite.Match == CrumbKill) _plumeKills++;
                    else if (bite.Match == CrumbCut) _plumeCuts++;
                    else _plumeWounds++;

                    if (_plumeFirst == null) _plumeFirst = bite;
                }
            }

            // The plumes that are gone, forgotten; kept in their order, so the quads are written in the same order every time.
            PlumeBites.RemoveAll(b => b.Onset < second - PlumeLifeOf(b) * speed);

            if (!_plumeLogged && window.FrameIndex >= reader.FrameCount - 1)
            {
                _plumeLogged = true;
                Debug.Log(PlumeWindowLine());
            }
        }

        private static string PlumeWindowLine()
        {
            var inv = CultureInfo.InvariantCulture;
            int count = _plumeKills + _plumeCuts + _plumeWounds;

            string first = _plumeFirst == null
                ? "none"
                : string.Format(inv, "eater {0} on {1} at {2:0.###} s", _plumeFirst.Eater,
                    _plumeFirst.Victim >= 0L
                        ? "animal " + _plumeFirst.Victim.ToString(inv) + " part " + _plumeFirst.VictimPart.ToString(inv)
                        : "an animal",
                    _plumeFirst.Onset);

            return string.Format(inv, "[Theatre] wound plume: {0} plumes in the window (kills {1}, cuts {2}, wounds {3}); first: {4}",
                count, _plumeKills, _plumeCuts, _plumeWounds, first);
        }

        // ---------------------------------------------------------------- one onset

        /// <summary>One onset's plume, from the frame on screen; null where the blow hurt no animal or the eater's mouth is not drawn.</summary>
        private static PlumeBite CapturePlume(FilmWindowWorld window, BiteOnset onset)
        {
            if (!TryBody(window, onset.Id, out Phenotype eater, out Transform[] eaterParts) ||
                onset.Part < 0 || onset.Part >= eater.PartCount || onset.Part >= eaterParts.Length || eaterParts[onset.Part] == null ||
                !TryMouth(eater, eaterParts, onset.Part, out Vector3 mouth, out float radius) || !(radius > 0f))
            {
                return null;
            }

            // The crumbs' classification, by their own functions: the kill row of this eater and part, else the nearest drawn prey.
            KillRow kill = CrumbKillOf(window, onset);
            int match;
            int prey;
            long victim = -1L;
            int victimPart = -1;
            Color painted;
            bool tinted;

            if (kill != null)
            {
                match = kill.Root == 1 ? CrumbKill : CrumbCut;
                victim = kill.Victim;
                victimPart = kill.VictimPart;
                tinted = CrumbVictimTint(window, victim, victimPart, onset.Second, out prey, out painted);
            }
            else
            {
                match = CrumbWound;
                tinted = CrumbNearestPrey(window, onset.Id, mouth, radius, out prey, out painted, out victim, out victimPart);
            }

            // An animal only: a leaf sheds crumbs, a corpse or remains does not bleed, and an unknown prey is not drawn a wound.
            if (prey != PreyAnimal) return null;

            var bite = new PlumeBite
            {
                Eater = onset.Id, Part = onset.Part, Onset = onset.Second, Match = match, Victim = victim, VictimPart = victimPart,
            };

            // The wound point: the bitten part's surface nearest the mouth, where that part is drawn; else the mouth.
            Vector3 wound = mouth;
            Vector3 outward = Vector3.zero;

            if (victim >= 0L && victimPart >= 0 && PlumeWoundPoint(window, victim, victimPart, mouth, out Vector3 at, out Vector3 away))
            {
                wound = at;
                outward = away;
            }

            bite.Wound = wound;

            // The colour: the red, a quarter toward the prey's paint so it belongs to the body, and held dark.
            Color colour = tinted ? Color.Lerp(PlumeRed, painted, PlumePreyShare) : PlumeRed;
            float most = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

            if (most > PlumeDarkMost)
            {
                float k = PlumeDarkMost / most;
                colour.r *= k;
                colour.g *= k;
                colour.b *= k;
            }

            colour.a = 1f;
            bite.Colour = QualitySettings.activeColorSpace == ColorSpace.Linear ? colour.linear : colour;

            // A kill bleeds more (D286): more motes, spread wider, a little larger. Cuts and wounds are as they were.
            bool isKill = match == CrumbKill;
            float mean = PlumeRadiusShare * Mathf.Clamp(CrumbMouthShare * radius, CrumbRadiusLow, CrumbRadiusHigh) *
                         (isKill ? PlumeKillRadiusFactor : 1f);
            float spreadScale = isKill ? PlumeKillSpreadFactor : 1f;
            int motes = isKill ? Mathf.RoundToInt(PlumeMotes * PlumeKillMoteFactor) : PlumeMotes;
            long id = onset.Id;
            int part = onset.Part;
            double second = onset.Second;

            bite.Motes = new PlumeMote[motes];

            for (int i = 0; i < motes; i++)
            {
                // Born packed about the wound point.
                Vector3 about = CrumbUnit(CrumbHash(id, part, second, i, 101), CrumbHash(id, part, second, i, 102));
                Vector3 birth = wound + about * (PlumeBirthRadii * radius * CrumbHash(id, part, second, i, 103));

                // Spreading every way, leaning outward from the bitten part where it is known.
                Vector3 way = outward * 0.8f + CrumbUnit(CrumbHash(id, part, second, i, 104), CrumbHash(id, part, second, i, 105));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : about;

                float period = Mathf.Lerp(PlumeWobbleLow, PlumeWobbleHigh, CrumbHash(id, part, second, i, 106));

                bite.Motes[i] = new PlumeMote
                {
                    Birth = birth,
                    Spread = way * (PlumeSpreadRadii * spreadScale * radius * Mathf.Lerp(0.3f, 1f, CrumbHash(id, part, second, i, 107))),
                    Radius = mean * Mathf.Lerp(0.6f, 1.4f, CrumbHash(id, part, second, i, 108)),
                    Phase = new Vector3(
                        6.2831853f * CrumbHash(id, part, second, i, 109), 6.2831853f * CrumbHash(id, part, second, i, 110),
                        6.2831853f * CrumbHash(id, part, second, i, 111)),
                    Omega = 6.2831853f / period,
                    Seed = CrumbHash(id, part, second, i, 112) * 97f,
                };
            }

            // The haze (EVOSIM_THEATRE_WOUND_PLUME_HAZE): its puffs and specks, hashed by salts of their own, so the motes above are as
            // they were. With its dial off this line does nothing.
            if (WoundPlumeHaze) CapturePlumeHaze(bite, outward, radius, mean, spreadScale, isKill, tinted, painted);

            return bite;
        }

        /// <summary>
        /// The haze's colour, puffs and specks of one blow (<see cref="WoundPlumeHaze"/> only), born about the wound point as the motes
        /// are. Every number is hashed from the eater, its part, the onset and the index, with salts 201 to 212 (puffs) and 221 to 236
        /// (specks), which neither the crumbs (1 to 13) nor the motes (101 to 112) use.
        /// </summary>
        private static void CapturePlumeHaze(PlumeBite bite, Vector3 outward, float radius, float mean, float spreadScale, bool isKill,
            bool tinted, Color painted)
        {
            long id = bite.Eater;
            int part = bite.Part;
            double second = bite.Onset;
            float life = PlumeLifeOf(bite);

            // The colour: the deeper red, a tenth toward the prey's paint, and held darker than the discs'.
            Color colour = tinted ? Color.Lerp(PlumeHazeRed, painted, PlumeHazePreyShare) : PlumeHazeRed;
            float most = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

            if (most > PlumeHazeDarkMost)
            {
                float k = PlumeHazeDarkMost / most;
                colour.r *= k;
                colour.g *= k;
                colour.b *= k;
            }

            colour.a = 1f;
            bite.HazeColour = QualitySettings.activeColorSpace == ColorSpace.Linear ? colour.linear : colour;

            // A kill bleeds more (D286): more puffs and specks; the wider spread and larger mean come in with spreadScale and mean.
            float factor = isKill ? PlumeKillMoteFactor : 1f;
            bite.Puffs = new PlumePuff[Mathf.RoundToInt(PlumeHazePuffs * factor)];
            bite.Specks = new PlumeSpeck[Mathf.RoundToInt(PlumeHazeSpecks * factor)];

            for (int i = 0; i < bite.Puffs.Length; i++)
            {
                // Born packed about the wound point, spreading every way and leaning outward from the bitten part, as the motes.
                Vector3 about = CrumbUnit(CrumbHash(id, part, second, i, 201), CrumbHash(id, part, second, i, 202));
                Vector3 birth = bite.Wound + about * (PlumeBirthRadii * radius * CrumbHash(id, part, second, i, 203));
                Vector3 way = outward * 0.8f + CrumbUnit(CrumbHash(id, part, second, i, 204), CrumbHash(id, part, second, i, 205));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : about;
                float end = life * Mathf.Lerp(PlumeHazePuffEndLow, 1f, CrumbHash(id, part, second, i, 206));

                bite.Puffs[i] = new PlumePuff
                {
                    Birth = birth,
                    Spread = way * (spreadScale * radius *
                                    Mathf.Lerp(PlumeHazeDetachRadiiLow, PlumeHazeDetachRadiiHigh, CrumbHash(id, part, second, i, 207))),
                    Radius = mean * Mathf.Lerp(PlumeHazePuffShareLow, PlumeHazePuffShareHigh,
                        Mathf.Pow(CrumbHash(id, part, second, i, 208), PlumeHazePuffSharePower)),
                    Carry = 1f + PlumeHazePuffCarrySpread * (2f * CrumbHash(id, part, second, i, 212) - 1f),
                    Start = PlumeHazePuffStagger * CrumbHash(id, part, second, i, 209),
                    End = end,
                    Span = Mathf.Max(1e-3f, PlumeFadeShare * end),
                    Seed = CrumbHash(id, part, second, i, 210) * 97f,
                    Shade = Mathf.Lerp(PlumeHazePuffShadeLow, PlumeHazePuffShadeHigh, CrumbHash(id, part, second, i, 211)),
                };
            }

            // The specks' radii: a power law truncated to [low, high], drawn by inverting its distribution; fine particulate at any size.
            const float low = PlumeHazeSpeckRadiusLow;
            const float high = PlumeHazeSpeckRadiusHigh;
            float ratio = Mathf.Pow(low / high, PlumeHazeSpeckPower);

            for (int i = 0; i < bite.Specks.Length; i++)
            {
                Vector3 about = CrumbUnit(CrumbHash(id, part, second, i, 221), CrumbHash(id, part, second, i, 222));
                Vector3 birth = bite.Wound + about * (PlumeBirthRadii * radius * CrumbHash(id, part, second, i, 223));
                Vector3 way = outward * 0.8f + CrumbUnit(CrumbHash(id, part, second, i, 224), CrumbHash(id, part, second, i, 225));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : about;
                float end = life * Mathf.Lerp(PlumeHazeSpeckEndLow, 1f, CrumbHash(id, part, second, i, 226));
                float size = low * Mathf.Pow(1f - CrumbHash(id, part, second, i, 227) * (1f - ratio), -1f / PlumeHazeSpeckPower);
                float big = Mathf.InverseLerp(low, high, size);
                float period = Mathf.Lerp(PlumeWobbleLow, PlumeWobbleHigh, CrumbHash(id, part, second, i, 228));

                bite.Specks[i] = new PlumeSpeck
                {
                    Birth = birth,
                    Spread = way * (PlumeSpreadRadii * spreadScale * radius * Mathf.Lerp(PlumeHazeSpeckSpreadLow, 1f, CrumbHash(id, part, second, i, 229))),
                    Radius = Mathf.Clamp(size, PlumeHazeSpeckRadiusLow, PlumeHazeSpeckRadiusHigh),
                    Start = PlumeHazeSpeckStagger * CrumbHash(id, part, second, i, 230),
                    End = end,
                    Span = Mathf.Max(1e-3f, PlumeHazeSpeckSpanShare * end),
                    Sink = Mathf.Lerp(PlumeHazeSpeckSinkLow, PlumeHazeSpeckSinkHigh, big) *
                           (1f + PlumeHazeSpeckSinkJitter * (CrumbHash(id, part, second, i, 231) - 0.5f)),
                    Phase = new Vector3(
                        6.2831853f * CrumbHash(id, part, second, i, 232), 6.2831853f * CrumbHash(id, part, second, i, 233),
                        6.2831853f * CrumbHash(id, part, second, i, 234)),
                    Omega = 6.2831853f / period,
                    Seed = CrumbHash(id, part, second, i, 235) * 97f,
                    Shade = Mathf.Lerp(PlumeHazeSpeckShadeLow, PlumeHazeSpeckShadeHigh, CrumbHash(id, part, second, i, 236)),
                };
            }
        }

        // The victim's part as drawn at this frame, if it is: the point of its surface nearest the mouth, and the way out of the part
        // through it. False where the victim is not drawn among the live bodies (a kill's, swallowed) or the part is not.
        private static bool PlumeWoundPoint(FilmWindowWorld window, long victim, int victimPart, Vector3 mouth, out Vector3 wound,
            out Vector3 outward)
        {
            wound = mouth;
            outward = Vector3.zero;

            List<AteCandidate> candidates = AteCandidatesOf(window);

            for (int i = 0; i < candidates.Count; i++)
            {
                AteCandidate candidate = candidates[i];
                if (candidate.Id != victim || candidate.Kind != null) continue;

                Phenotype body = candidate.Body;
                Transform[] parts = candidate.Parts;
                if (body == null || parts == null || victimPart >= body.PartCount || victimPart >= parts.Length || parts[victimPart] == null)
                    return false;

                Transform at = parts[victimPart];
                wound = PlumeSurfacePoint(body.Parts[victimPart], at, mouth);

                Vector3 away = wound - at.position;
                if (away.sqrMagnitude > 1e-12f) outward = away.normalized;
                return true;
            }

            return false;
        }

        // The point of a part's surface nearest a world point, by the shapes SignedDistance measures; the point itself where it is inside.
        private static Vector3 PlumeSurfacePoint(PhenotypePart part, Transform at, Vector3 world)
        {
            Vector3 p = at.InverseTransformPoint(world);
            Float3 h = part.HalfExtents;
            var half = new Vector3(Mathf.Abs(h.X), Mathf.Abs(h.Y), Mathf.Abs(h.Z));
            Vector3 q;

            if (part.ShapeId == ShapeIds.Box)
            {
                q = new Vector3(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y), Mathf.Clamp(p.z, -half.z, half.z));
            }
            else if (part.ShapeId == ShapeIds.Capsule)
            {
                float span = CapsuleShape.HalfSpan(h);
                float r = CapsuleShape.Radius(h);
                var axis = new Vector3(0f, Mathf.Clamp(p.y, -span, span), 0f);
                Vector3 d = p - axis;
                q = d.magnitude > r ? axis + d.normalized * r : p;
            }
            else
            {
                float r = Mathf.Max(half.x, Mathf.Max(half.y, half.z));
                q = p.magnitude > r ? p.normalized * r : p;
            }

            return at.TransformPoint(q);
        }

        // ---------------------------------------------------------------- the drawing

        // The flakes' shader in a material of the plume's own, a mesh and an object to draw it in the camera's render; made once, at the
        // first step with a film window. False when the shader cannot be found, said once.
        private static bool EnsurePlumeDrawer()
        {
            if (_plumeNoShader) return false;

            // The haze (EVOSIM_THEATRE_WOUND_PLUME_HAZE) draws through its own shader; with its dial off this line does nothing.
            if (_plumeMaterial == null && WoundPlumeHaze && !MakePlumeHazeMaterial()) return false;

            if (_plumeMaterial == null)
            {
                Shader shader = Shader.Find("Evosim/Theatre Crumble Flakes");

                if (shader == null)
                {
                    _plumeNoShader = true;
                    Debug.LogWarning("[Theatre] wound plume: shader 'Evosim/Theatre Crumble Flakes' not found, so no plume is drawn");
                    return false;
                }

                _plumeMaterial = new Material(shader) { name = "Theatre Wound Plume", hideFlags = HideFlags.HideAndDontSave };

                // Premultiplied over what lies behind: the dark motes (uv1.w = 2) darken toward the plume's colour.
                _plumeMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
                _plumeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (_plumeMesh == null)
            {
                _plumeMesh = new Mesh { name = "Theatre Wound Plume", hideFlags = HideFlags.HideAndDontSave };
                _plumeMesh.MarkDynamic();
            }

            // The object belongs to the scene the film plays in, and is made again if a scene change took it.
            if (_plumeObject == null)
            {
                _plumeObject = new GameObject("Theatre Wound Plume");
                _plumeObject.AddComponent<MeshFilter>().sharedMesh = _plumeMesh;

                _plumeRenderer = _plumeObject.AddComponent<MeshRenderer>();
                _plumeRenderer.sharedMaterial = _plumeMaterial;
                _plumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _plumeRenderer.receiveShadows = false;

                // Seen from inside the water only, and turned off for the census views, as the crumbs are.
                _plumeObject.AddComponent<TheatreInsideOnly>();
            }

            if (_plumePass == null) _plumePass = new PlumePass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };

            if (!_plumeHooked)
            {
                RenderPipelineManager.beginCameraRendering += OnPlumeCamera;
                _plumeHooked = true;
            }

            return true;
        }

        /// <summary>
        /// The haze's material (<see cref="WoundPlumeHaze"/> only), held where the discs' would be, so the mesh, the object and the pass
        /// are the discs' own. Its shader blends premultiplied (One, OneMinusSrcAlpha) by itself. False when the shader cannot be found,
        /// said once.
        /// </summary>
        private static bool MakePlumeHazeMaterial()
        {
            Shader shader = Shader.Find(PlumeHazeShaderName);

            if (shader == null)
            {
                _plumeNoShader = true;
                Debug.LogWarning("[Theatre] wound plume: shader '" + PlumeHazeShaderName + "' not found, so no haze is drawn");
                return false;
            }

            _plumeMaterial = new Material(shader) { name = "Theatre Wound Plume Haze", hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private static void OnPlumeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_plumeRenderer == null || camera == null || camera.cameraType != CameraType.Game) return;

            // Turned off by the census views (TheatreInsideOnly): nothing to draw and nothing to build.
            if (!_plumeRenderer.enabled) return;

            bool lens = TheatreLens.Armed;

            try
            {
                BuildPlumes(camera);
            }
            catch (Exception e)
            {
                _plumeDrawMesh = null;
                if (_plumeMesh != null) _plumeMesh.Clear();
                Debug.LogWarning("[Theatre] wound plume: the plumes could not be built for this render: " + e.GetType().Name + ": " + e.Message);
            }

            // With the lens armed the shells are drawn after it, sharp; the plume follows them, as the crumbs do.
            _plumeRenderer.forceRenderingOff = lens;

            if (lens && _plumeDrawMesh != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_plumePass);
            }
        }

        private static void BuildPlumes(Camera camera)
        {
            Transform view = camera.transform;
            TheatreSkin skin = TheatreSkin.Current;
            Material shells = skin != null ? skin.SnowShellsMaterial : null;

            // The run's water is opened by the first push of a frame; until then the current at a wound would read zero.
            bool water = skin != null && (skin.RunWaterOpen || !skin.RunWaterWanted);

            bool same = ReferenceEquals(_plumeBuiltCamera, camera) && _plumeSecond == _plumeBuiltSecond && view.position == _plumeBuiltEye &&
                        view.rotation == _plumeBuiltRotation && camera.fieldOfView == _plumeBuiltFov && camera.aspect == _plumeBuiltAspect && water;
            if (same) return;

            _plumeBuiltCamera = camera;
            _plumeBuiltSecond = _plumeSecond;
            _plumeBuiltEye = view.position;
            _plumeBuiltRotation = view.rotation;
            _plumeBuiltFov = camera.fieldOfView;
            _plumeBuiltAspect = camera.aspect;

            PlumePositions.Clear();
            PlumeCorners.Clear();
            PlumeData.Clear();
            PlumeColours.Clear();
            PlumeTriangles.Clear();
            _plumeDrawMesh = null;

            if (!water || double.IsNaN(_plumeSecond) || shells == null || _plumeMaterial == null)
            {
                if (shells == null && !_plumeNoShellsTold && !double.IsNaN(_plumeSecond) && skin != null)
                {
                    _plumeNoShellsTold = true;
                    Debug.Log("[Theatre] wound plume: the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the plume cannot be drawn in the water's box");
                }

                _plumeMesh.Clear();
                return;
            }

            // The water the shells sit in; a dark mote reads nothing else of theirs.
            _plumeMaterial.SetVector("_BoxMin", shells.GetVector("_BoxMin"));
            _plumeMaterial.SetVector("_BoxMax", shells.GetVector("_BoxMax"));
            _plumeMaterial.SetVector("_Tank", shells.GetVector("_Tank"));
            _plumeMaterial.SetFloat("_Waterline", shells.GetFloat("_Waterline"));

            GeometryUtility.CalculateFrustumPlanes(camera, PlumePlanes);

            bool open = skin.RunWaterOpen;
            double speed = Math.Max(1d, _plumeSpeed);
            int drawn = 0;

            for (int b = 0; b < PlumeBites.Count; b++)
            {
                PlumeBite bite = PlumeBites[b];
                if (bite.Motes == null) continue;

                double ageWorld = _plumeSecond - bite.Onset;
                float ageFilm = (float)(ageWorld / speed);
                float lifeFilm = PlumeLifeOf(bite);
                if (ageWorld < 0d || ageFilm >= lifeFilm) continue;

                // The current at the wound point at the onset: a function of the place and the second, read once.
                if (!bite.CurrentKnown)
                {
                    bite.Current = open ? skin.WaterAt(bite.Wound, bite.Onset) : Vector3.zero;
                    bite.CurrentKnown = true;
                }

                // The haze (EVOSIM_THEATRE_WOUND_PLUME_HAZE) in place of the discs. With its dial off this is never taken, and the
                // discs below are drawn as they always were.
                if (WoundPlumeHaze)
                {
                    drawn += AddPlumeHaze(bite, ageFilm, (float)ageWorld);
                    continue;
                }

                // The spread and the swell ease out softly; the colour fades over the last stretch of the life.
                float spread = 1f - Mathf.Exp(-ageFilm / PlumeSpreadSeconds);
                float grow = 1f + PlumeGrowth * (1f - Mathf.Exp(-ageFilm / PlumeGrowSeconds));
                float left = Mathf.Clamp01((lifeFilm - ageFilm) / (PlumeFadeShare * lifeFilm));
                float fade = left * left * (3f - 2f * left);
                if (!(fade > 0f)) continue;

                // The fade is the opacity: the colour stays, the stain thins.
                Color colour = bite.Colour;
                colour.a = PlumeOpacity * fade;
                float age = (float)ageWorld;

                for (int i = 0; i < bite.Motes.Length; i++)
                {
                    PlumeMote m = bite.Motes[i];

                    // Its birth, the spread easing out, the water it was born into, and a slow wobble that starts at nothing.
                    Vector3 place = m.Birth + m.Spread * spread + bite.Current * age;
                    place.x += PlumeWobbleMetres * (Mathf.Sin(m.Omega * ageFilm + m.Phase.x) - Mathf.Sin(m.Phase.x));
                    place.y += 0.5f * PlumeWobbleMetres * (Mathf.Sin(1.17f * m.Omega * ageFilm + m.Phase.y) - Mathf.Sin(m.Phase.y));
                    place.z += PlumeWobbleMetres * (Mathf.Sin(0.83f * m.Omega * ageFilm + m.Phase.z) - Mathf.Sin(m.Phase.z));

                    float radius = m.Radius * grow;
                    if (!(radius > 0f)) continue;
                    if (!PlumeInFrustum(place, radius + CrumbMargin)) continue;

                    int first = PlumePositions.Count;
                    // w = 2: a dark mote, a soft round disc that darkens what lies behind it.
                    var data = new Vector4(radius, m.Seed, ageFilm, 2f);

                    for (int corner = 0; corner < 4; corner++)
                    {
                        PlumePositions.Add(place);
                        PlumeCorners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                        PlumeData.Add(data);
                        PlumeColours.Add(colour);
                    }

                    PlumeTriangles.Add(first);
                    PlumeTriangles.Add(first + 1);
                    PlumeTriangles.Add(first + 2);
                    PlumeTriangles.Add(first);
                    PlumeTriangles.Add(first + 2);
                    PlumeTriangles.Add(first + 3);

                    drawn++;
                }
            }

            if (drawn > 0)
            {
                _plumeMesh.Clear();
                _plumeMesh.indexFormat = IndexFormat.UInt32;
                _plumeMesh.SetVertices(PlumePositions);
                _plumeMesh.SetUVs(0, PlumeCorners);
                _plumeMesh.SetUVs(1, PlumeData);
                _plumeMesh.SetColors(PlumeColours);
                _plumeMesh.SetTriangles(PlumeTriangles, 0, false);
                _plumeMesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
                _plumeDrawMesh = _plumeMesh;
            }
            else
            {
                _plumeMesh.Clear();
            }
        }

        private static bool PlumeInFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < PlumePlanes.Length; i++)
            {
                if (PlumePlanes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        /// <summary>
        /// One blow's haze at its age (<see cref="WoundPlumeHaze"/> only): its puffs (<c>uv1.w = 0</c>) and specks (<c>uv1.w = 1</c>) as
        /// quads of the plume's mesh, each eased in from its own start and dissolving to its own end. Returns the count of quads written,
        /// and writes the haze's one log line at its first.
        /// </summary>
        private static int AddPlumeHaze(PlumeBite bite, float ageFilm, float age)
        {
            int drawn = 0;

            // The specks' spread easing out as the motes' does, the water it was born into, and the way downstream (none in still water).
            float spread = 1f - Mathf.Exp(-ageFilm / PlumeSpreadSeconds);
            float lifeFilm = PlumeLifeOf(bite);
            Vector3 drift = bite.Current * age;
            Vector3 down = PlumeDownstream(bite.Current);
            Color tint = bite.HazeColour;

            PlumePuff[] puffs = bite.Puffs;
            for (int i = 0; puffs != null && i < puffs.Length; i++)
            {
                PlumePuff p = puffs[i];
                if (!(p.Radius > 0f)) continue;

                // Eased in from its own start, dissolving before its own end: the haze gathers over several frames, never at once.
                float fade = PlumeEase(Mathf.Clamp01((ageFilm - p.Start) / PlumeHazePuffInSeconds)) *
                             PlumeEase(Mathf.Clamp01((p.End - ageFilm) / p.Span));
                if (!(fade > 0f)) continue;

                // Detaching from the wound over its own age, eased out over the plume's life: it leaves the body, grows and thins.
                float u = Mathf.Clamp01((ageFilm - p.Start) / lifeFilm);
                float detach = u * (2f - u);
                float grow = 1f + PlumeHazePuffGrow * detach;
                float thin = Mathf.Pow(1f / grow, PlumeHazePuffThinPower);

                // Its way off bent downstream, and the current carrying it at its own share, so the blood trails off in a stream.
                float radius = p.Radius * grow;
                Vector3 place = p.Birth + PlumeBent(p.Spread, down) * detach + drift * p.Carry;
                if (!PlumeInFrustum(place, PlumeHazePuffReach * radius + CrumbMargin)) continue;

                // Grown and thinned together, so the stain spreads rather than thickens.
                var colour = new Color(tint.r * p.Shade, tint.g * p.Shade, tint.b * p.Shade, PlumeHazePuffOpacity * thin * fade);
                AddPlumeQuad(place, new Vector4(radius, p.Seed, ageFilm, 0f), colour);
                drawn++;
            }

            PlumeSpeck[] specks = bite.Specks;
            for (int i = 0; specks != null && i < specks.Length; i++)
            {
                PlumeSpeck s = specks[i];
                if (!(s.Radius > 0f)) continue;

                float fade = PlumeEase(Mathf.Clamp01((ageFilm - s.Start) / PlumeHazeSpeckInSeconds)) *
                             PlumeEase(Mathf.Clamp01((s.End - ageFilm) / s.Span));
                if (!(fade > 0f)) continue;

                // Its birth, the spread, the water and its sink, and the motes' slow wobble that starts at nothing.
                Vector3 place = s.Birth + PlumeBent(s.Spread, down) * spread + drift + new Vector3(0f, -s.Sink * age, 0f);
                place.x += PlumeWobbleMetres * (Mathf.Sin(s.Omega * ageFilm + s.Phase.x) - Mathf.Sin(s.Phase.x));
                place.y += 0.5f * PlumeWobbleMetres * (Mathf.Sin(1.17f * s.Omega * ageFilm + s.Phase.y) - Mathf.Sin(s.Phase.y));
                place.z += PlumeWobbleMetres * (Mathf.Sin(0.83f * s.Omega * ageFilm + s.Phase.z) - Mathf.Sin(s.Phase.z));

                if (!PlumeInFrustum(place, s.Radius + CrumbMargin)) continue;

                // A speck keeps its size as it dissolves and only fades.
                var colour = new Color(tint.r * s.Shade, tint.g * s.Shade, tint.b * s.Shade, PlumeHazeSpeckOpacity * fade);
                AddPlumeQuad(place, new Vector4(s.Radius, s.Seed, ageFilm, 1f), colour);
                drawn++;
            }

            if (drawn > 0 && !_plumeHazeLogged)
            {
                _plumeHazeLogged = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] wound plume: the haze is drawn in place of the discs (EVOSIM_THEATRE_WOUND_PLUME_HAZE); first: t {0:0.###} eater {1} part {2}, {3}, {4} puffs and {5} specks",
                    bite.Onset, bite.Eater, bite.Part, CrumbMatchWords[bite.Match], puffs != null ? puffs.Length : 0,
                    specks != null ? specks.Length : 0));
            }

            return drawn;
        }

        /// <summary>Smoothstep of a share already in [0, 1].</summary>
        private static float PlumeEase(float u) => u * u * (3f - 2f * u);

        /// <summary>The unit direction of a current, or zero below <see cref="PlumeHazeCurrentFloor"/> m/s.</summary>
        private static Vector3 PlumeDownstream(Vector3 current)
        {
            float speed = current.magnitude;
            return speed > PlumeHazeCurrentFloor ? current / speed : Vector3.zero;
        }

        /// <summary>A way off, its length kept, its direction leant toward <paramref name="down"/> by <see cref="PlumeHazeDownstreamLean"/>.</summary>
        private static Vector3 PlumeBent(Vector3 way, Vector3 down)
        {
            float length = way.magnitude;
            if (!(length > 0f) || down == Vector3.zero) return way;

            Vector3 bent = way / length + PlumeHazeDownstreamLean * down;
            return bent.sqrMagnitude > 1e-8f ? bent.normalized * length : way;
        }

        /// <summary>One camera-facing quad of the plume's mesh (the haze's only; the discs write theirs in place): four corners and two triangles.</summary>
        private static void AddPlumeQuad(Vector3 place, Vector4 data, Color colour)
        {
            int first = PlumePositions.Count;

            for (int corner = 0; corner < 4; corner++)
            {
                PlumePositions.Add(place);
                PlumeCorners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                PlumeData.Add(data);
                PlumeColours.Add(colour);
            }

            PlumeTriangles.Add(first);
            PlumeTriangles.Add(first + 1);
            PlumeTriangles.Add(first + 2);
            PlumeTriangles.Add(first);
            PlumeTriangles.Add(first + 2);
            PlumeTriangles.Add(first + 3);
        }

        /// <summary>Draws the plume after the theatre's lens, as the crumbs are drawn: each mote carries its own blur.</summary>
        private sealed class PlumePass : ScriptableRenderPass
        {
            public PlumePass()
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
                Mesh mesh = _plumeDrawMesh;
                Material material = _plumeMaterial;
                if (mesh == null || material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre Wound Plume", out PassData data))
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
