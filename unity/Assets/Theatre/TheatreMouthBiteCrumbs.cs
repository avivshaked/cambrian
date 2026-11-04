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
    /// The bite's crumbs (<c>EVOSIM_THEATRE_BITE_CRUMBS</c>, <see cref="TheatreMouth2.BiteCrumbs"/>): at each recorded bite onset a few
    /// motes torn from the prey at the eater's mouth, which drift off with the run's water, sink, and are gone in a few seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> Every entry tests the dial first; with it off no object, mesh, material, hook or log
    /// line is made. The dial is read only with the bite pulse on (<c>EVOSIM_THEATRE_MOUTH2_BITE</c>), since the onsets are the pulse's:
    /// one source for both, the one the pulse's log line names (the ate bit of a film window, else its kill rows). The flash dial
    /// (<c>EVOSIM_THEATRE_MOUTH2_BITE_FLASH</c>) at 0 leaves the onsets as they are, so the crumbs come with no flash at all.
    /// </para>
    /// <para>
    /// <b>A crumb is a function of (eater, part, onset).</b> Its count and size are set by the bite's kind, read from the window's data:
    /// a kill row of the same eater and part within a pose frame of the onset makes it a kill (the whole body died) or a cut (a part
    /// taken), and none makes it a wound. The prey is the row's victim and part where there is a row, its genome developed at the plan in
    /// force (as the onsets' eating parts are), and for a wound the drawn part nearest the mouth at the onset frame, within reach of the
    /// mouth; a leaf (a photosynthetic part) sheds more and smaller crumbs than an animal. The tint is the prey's painted colour
    /// (<see cref="TheatrePalette.PaintedGuildOf"/>, as the crumble's flakes take theirs), and a pale tissue tone where no prey is known.
    /// The birth point is the mouth point as the onset's own pose frame draws it, so the crumbs come from the recorded pose and not from
    /// a render's history. Every number of a crumb (where about the mouth, how far the tear throws it, its size, sink and wobble) is
    /// hashed from the eater's id, the part, the onset's second and the crumb's index; its place at a second is its birth plus the throw,
    /// the current at its birth and its sink times its age, and a small wobble.
    /// </para>
    /// <para>
    /// <b>A plant has no blood (D289).</b> A bite on a plant throws off its own fragments in its own painted colour (the prey's tint
    /// above); with <c>EVOSIM_THEATRE_PLANT_CRUMBS</c> on, a killing bite on a plant (<c>kill.Root == 1</c>, a leaf part) throws
    /// <see cref="PlantKillCrumbFactor"/> times the crumbs, thrown wider and larger. Off, and for every other bite, nothing changes.
    /// </para>
    /// <para>
    /// <b>Torn flakes, behind their own dial</b> (<c>EVOSIM_THEATRE_CRUMB_FLAKES</c>, <see cref="CrumbFlakes"/>). At full size the
    /// crumbs read as identical soft round blobs. With the dial on, every crumb is drawn instead as a flat flake of torn tissue
    /// (<c>Evosim/Theatre Crumb Flakes</c>): oriented in the world, tumbling about a hashed axis, with its own torn outline, a size on a
    /// power law about the crumb's radius (the bite's flakes then scaled as one so their summed area is a share of the piece the bite
    /// took), now and then a long strand, its colour varied a little; and each bite sheds a few fine
    /// specks besides. A crumb's place and motion are the ones above, untouched: every number of the flakes is hashed with salts of
    /// its own (20 and over). Off, <see cref="CrumbFlakes"/> is false, no flake, speck, list, material or log line is made, and every
    /// crumb is drawn through the code and the shader it always was; every flake line below sits behind that one flag.
    /// </para>
    /// <para>
    /// <b>A frame the film never shows.</b> A time-lapse can step over an onset's own pose frame; the crumbs are then born at the first
    /// shown frame within <see cref="CrumbCatchFilmSeconds"/> of film after it, and an onset with none is not shed (counted in the
    /// status line). A film shows the same frames every time it is rendered, so it draws the same crumbs. A scene that plays the window
    /// again from earlier forgets the crumbs born so far, so each pass is drawn from its own frames.
    /// </para>
    /// <para>
    /// <b>Drawn as the crumble draws its flakes</b>: one mesh of camera-facing quads rebuilt when a camera is about to render, shaded by
    /// the same shader (<c>Evosim/Theatre Crumble Flakes</c>, its large-mote path) in the snow shells' water, and drawn after the lens
    /// while the lens is armed. With no snow shells in the look nothing is drawn, and the log says so once.
    /// </para>
    /// </remarks>
    public static partial class TheatreMouthBite
    {
        // ---------------------------------------------------------------- the constants

        /// <summary>How long a crumb lives, film seconds.</summary>
        public const float CrumbLifeSeconds = 3f;

        /// <summary>The last stretch of a crumb's life over which it dissolves (shrinks away), film seconds.</summary>
        private const float CrumbDissolveSeconds = 1.5f;

        /// <summary>
        /// The shader's seconds from the tissue colour to the snow's, over a crumb's age in film seconds: four times the life, so a crumb
        /// keeps most of its tint to the end.
        /// </summary>
        private const float CrumbTakeSeconds = 4f * CrumbLifeSeconds;

        /// <summary>A crumb's mean radius as a share of the mouth's radius, before the kind's factor, and the bounds it is held to, metres.</summary>
        private const float CrumbMouthShare = 0.3f;

        private const float CrumbRadiusLow = 0.0012f;
        private const float CrumbRadiusHigh = 0.025f;

        /// <summary>How far the tear throws a crumb at most, in mouth radii, and the film seconds over which the throw settles.</summary>
        private const float CrumbThrowRadii = 3f;

        private const float CrumbThrowSeconds = 0.25f;

        /// <summary>
        /// How fast a crumb sinks, m/s of the run: torn tissue, faster than the snow's own sink so the fall reads within its few seconds,
        /// and varied by <see cref="CrumbSinkJitter"/> either way.
        /// </summary>
        private const float CrumbSinkMetresPerSecond = 0.01f;

        private const float CrumbSinkJitter = 0.4f;

        /// <summary>The wobble of a crumb about its path, metres, and the range of its period, film seconds.</summary>
        private const float CrumbWobbleMetres = 0.002f;

        private const float CrumbWobbleLow = 1.2f;
        private const float CrumbWobbleHigh = 2.4f;

        /// <summary>A crumb outside the frustum by more than this, metres, is not built.</summary>
        private const float CrumbMargin = 0.05f;

        /// <summary>An onset whose own frame the film skips is shed at a shown frame at most this many film seconds after it.</summary>
        public const double CrumbCatchFilmSeconds = 0.1d;

        /// <summary>A wound's prey is the drawn part nearest the mouth when its surface is within this many mouth radii, or this many metres.</summary>
        private const float CrumbPreyReachRadii = 2f;

        private const float CrumbPreyReachMetres = 0.01f;

        /// <summary>How many lines the captures write per window before only the status line counts them.</summary>
        private const int CrumbLinesPerWindow = 12;

        /// <summary>The tint where no prey is known: a pale tissue tone, in sRGB.</summary>
        private static readonly Color CrumbPaleTissue = new Color(0.86f, 0.78f, 0.72f, 1f);

        /// <summary>
        /// The plant crumbs (<c>EVOSIM_THEATRE_PLANT_CRUMBS</c>, 0 or 1, 0 off by default; D289): with it on, a killing bite on a plant
        /// (a leaf part, the whole plant taken) throws more, wider and larger crumbs. Off, every crumb is as it was.
        /// </summary>
        private static readonly bool PlantCrumbs = TheatreSkin.Dial("EVOSIM_THEATRE_PLANT_CRUMBS", 0f, 0f, 1f) >= 0.5f;

        /// <summary>A plant kill's crumbs number this many times a plant bite's otherwise (D289: a plant has no blood, its fragments fly).</summary>
        private const int PlantKillCrumbFactor = 2;

        /// <summary>A plant kill's crumbs are thrown this many times as far as a plant bite's.</summary>
        private const float PlantKillSpreadFactor = 1.4f;

        /// <summary>A plant kill's crumbs are this many times the radius of a plant bite's.</summary>
        private const float PlantKillRadiusFactor = 1.2f;

        private static bool _plantCrumbsTold;

        /// <summary>
        /// The torn flakes (<c>EVOSIM_THEATRE_CRUMB_FLAKES</c>, 0 or 1, 0 off by default), read once and only with the crumbs on
        /// (<see cref="TheatreMouth2.BiteCrumbs"/>): every crumb drawn as a tumbling flake of torn tissue through its own shader, and a
        /// few fine specks a bite. False leaves every crumb as it was.
        /// </summary>
        private static readonly bool CrumbFlakes =
            TheatreMouth2.BiteCrumbs && TheatreSkin.Dial("EVOSIM_THEATRE_CRUMB_FLAKES", 0f, 0f, 1f) >= 0.5f;

        private const string CrumbFlakesShader = "Evosim/Theatre Crumb Flakes";

        /// <summary>
        /// A flake's size over its crumb's radius, on a power law: the least, the tail's exponent (a Pareto's, so about half the flakes
        /// are under 0.53, one in nine over 1 and one in twenty-four held at the most), and the most. A steep tail and a low cap: at 3x a
        /// crumb the largest pieces stood as big as the eater's head.
        /// </summary>
        private const float FlakeSizeLow = 0.4f;

        private const float FlakeSizeTail = 2.4f;
        private const float FlakeSizeMost = 1.5f;

        /// <summary>
        /// The tissue a bite throws, as a share of the piece it took: the summed area of its flakes is held to this share of the piece's
        /// area (a kill's piece the victim's part, a cut's or a wound's a disc of the mouth's radius on it, held to the part's own face),
        /// by one factor on every flake's size of the bite, never above 1, so a small bite throws no more than it took.
        /// </summary>
        private const float FlakeShareKill = 0.45f;

        private const float FlakeShareCut = 0.25f;
        private const float FlakeShareWound = 0.15f;

        /// <summary>
        /// A flake's torn outline's area over the square of its nominal half-axis (FlakeSpan times its radius): the shader's cut stands at
        /// about 0.65 of the half-axes on a square measure, so about 4 x 0.65^2, less the cut corners. The aspect cancels.
        /// </summary>
        private const float FlakeOutlineArea = 1.6f;

        /// <summary>A flake's nominal half-axes over its radius (the torn outline stands inside them), and the largest radius drawn, metres.</summary>
        private const float FlakeSpan = 1.25f;

        private const float FlakeRadiusMost = 0.04f;

        /// <summary>A flake's long over short axis, a strand's (one crumb in <see cref="FlakeStrandOdds"/>), and a speck's.</summary>
        private const float FlakeAspectHigh = 2.2f;

        private const float FlakeStrandAspectLow = 4f;
        private const float FlakeStrandAspectHigh = 8f;
        private const float FlakeSpeckAspectHigh = 1.6f;
        private const int FlakeStrandOdds = 12;

        /// <summary>How fast a flake tumbles, turns a film second.</summary>
        private const float FlakeTumbleLow = 1f;

        private const float FlakeTumbleHigh = 4f;

        /// <summary>A flake's colour, varied about the crumb's: its value this share either way, its hue this share of a turn either way.</summary>
        private const float FlakeLightness = 0.1f;

        private const float FlakeHue = 0.02f;

        /// <summary>The opacity of a flake, a strand and a speck: thin translucent tissue, as torn algae is, and a faint grain.</summary>
        private const float FlakeOpacity = 0.55f;

        private const float FlakeStrandOpacity = 0.5f;
        private const float FlakeSpeckOpacity = 0.35f;

        /// <summary>
        /// The fine particulate: specks a bite, their radius in metres (0.3 to 1 mm across), how far the tear throws them in mouth
        /// radii, and how fast they sink as a share of a crumb's sink.
        /// </summary>
        private const int FlakeSpecksLow = 6;

        private const int FlakeSpecksHigh = 12;
        private const float FlakeSpeckRadiusLow = 0.00015f;
        private const float FlakeSpeckRadiusHigh = 0.0005f;
        private const float FlakeSpeckThrowRadii = 1.5f;
        private const float FlakeSpeckSinkShare = 0.5f;

        /// <summary>A speck's index in the hash, past any crumb's.</summary>
        private const int FlakeSpeckIndex = 1000;

        // The kinds, as the shader reads them in uv1.w.
        private const float FlakeKindFlake = 0f, FlakeKindStrand = 1f, FlakeKindSpeck = 2f;

        private static bool _flakesTold;

        // The bite's kinds and the prey's, as numbers for the table and the log.
        private const int CrumbWound = 0, CrumbKill = 1, CrumbCut = 2;
        private const int PreyUnknown = 0, PreyLeaf = 1, PreyAnimal = 2, PreyCorpse = 3, PreyRemains = 4;

        private static readonly string[] CrumbMatchWords = { "wound", "kill", "cut" };
        private static readonly string[] CrumbPreyWords = { "unknown", "leaf", "animal", "corpse", "remains" };

        // ---------------------------------------------------------------- one bite's crumbs

        /// <summary>One crumb, fixed at its birth.</summary>
        private struct Crumb
        {
            public Vector3 Birth;

            /// <summary>Where the tear throws it, relative to its birth, reached as the throw settles.</summary>
            public Vector3 Throw;

            public float Radius;
            public float Sink;
            public Vector3 Phase;
            public float Omega;
            public float Seed;
        }

        /// <summary>One onset's crumbs: the bite, its kind, its prey and tint, and the crumbs; or skipped, with none.</summary>
        private sealed class CrumbBite
        {
            public long Eater;
            public int Part;
            public double Onset;

            /// <summary>No crumb: the onset's frame was not shown near enough, or the eater's mouth was not drawn.</summary>
            public bool Skipped;

            public int Match;
            public int Prey;
            public long Victim = -1L;
            public int VictimPart = -1;

            /// <summary>The tint, in the colour space the shader mixes it in.</summary>
            public Color Tint;

            /// <summary>The current at the mouth at the onset, read once the run's water is open (or known to stay shut).</summary>
            public bool CurrentKnown;

            public Vector3 Current;
            public Vector3 Mouth;

            public Crumb[] Crumbs;

            /// <summary>
            /// With <see cref="CrumbFlakes"/> on only, else null: each crumb's flake (index for index), and the bite's specks with
            /// theirs.
            /// </summary>
            public CrumbFlake[] Flakes;

            public Crumb[] Specks;
            public CrumbFlake[] SpeckFlakes;
        }

        /// <summary>A crumb's flake (<see cref="CrumbFlakes"/>), fixed at its birth: its shape, its tumble and its colour.</summary>
        private struct CrumbFlake
        {
            /// <summary>Its radius over its crumb's (1 for a speck, whose crumb carries its own).</summary>
            public float Size;

            /// <summary>Its long over its short axis.</summary>
            public float Aspect;

            public float Kind;

            /// <summary>The axis it tumbles about, its normal and its long axis at age 0 (unit vectors), its rate (radians a film second) and phase.</summary>
            public Vector3 Axis;

            public Vector3 Normal;
            public Vector3 Long;
            public float Omega;
            public float Phase;

            /// <summary>Its place in the shader's noise field, which cuts its outline.</summary>
            public Vector2 Outline;

            /// <summary>Its colour, in the space the shader mixes it in, with its opacity in alpha.</summary>
            public Color Tint;
        }

        // ---------------------------------------------------------------- the state

        private static FilmWindowWorld _crumbWindow;
        private static int _crumbFrame = -1;
        private static double _crumbSecond = double.NaN;
        private static double _crumbSpeed = 1d;

        /// <summary>A census render is under way (<see cref="Suspend"/>): nothing is drawn until the next frame.</summary>
        private static bool _crumbHeld;

        /// <summary>The onsets already seen in this pass of the window, shed or skipped, in the order they were seen.</summary>
        private static readonly List<CrumbBite> CrumbBites = new List<CrumbBite>();

        private static readonly HashSet<(long, int, double)> CrumbSeen = new HashSet<(long, int, double)>();
        private static readonly List<BiteOnset> CrumbOnsets = new List<BiteOnset>();

        private static int _crumbShed;
        private static int _crumbMissed;
        private static int _crumbLines;

        // The prey shapes of the kill rows' victims, developed once per body and plan for the window.
        private static FilmWindowWorld _crumbShapesWindow;
        private static readonly Dictionary<long, Genome> CrumbGenomes = new Dictionary<long, Genome>();
        private static readonly Dictionary<(long, FilmWindowPlan), Phenotype> CrumbShapes = new Dictionary<(long, FilmWindowPlan), Phenotype>();

        // The drawing.
        private static GameObject _crumbObject;
        private static MeshRenderer _crumbRenderer;
        private static Mesh _crumbMesh;
        private static Mesh _crumbDrawMesh;
        private static Material _crumbMaterial;
        private static CrumbPass _crumbPass;
        private static bool _crumbHooked;
        private static bool _crumbNoShader;
        private static bool _crumbNoShellsTold;
        private static bool _crumbAnnounced;

        private static readonly List<Vector3> CrumbPositions = new List<Vector3>();
        private static readonly List<Vector2> CrumbCorners = new List<Vector2>();
        private static readonly List<Vector4> CrumbData = new List<Vector4>();
        private static readonly List<Color> CrumbColours = new List<Color>();
        private static readonly List<int> CrumbTriangles = new List<int>();
        private static readonly Plane[] CrumbPlanes = new Plane[6];

        private static double _crumbBuiltSecond = double.NaN;
        private static Camera _crumbBuiltCamera;
        private static Vector3 _crumbBuiltEye;
        private static Quaternion _crumbBuiltRotation;
        private static float _crumbBuiltFov;
        private static float _crumbBuiltAspect;
        private static bool _crumbBuiltHeld;
        private static double _crumbStatsSecond = double.NaN;
        private static int _crumbNewSeconds;

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="Frame"/> with every rendered frame of a film window, with the crumbs' dial on: sheds the crumbs of each
        /// onset of the last <see cref="CrumbLifeSeconds"/> of film not yet seen in this pass, at its own frame (or the first shown just
        /// after it), and forgets the ones whose crumbs are gone.
        /// </summary>
        private static void CrumbsFrame(FilmWindowWorld window, BiteOnsets onsets, double speed)
        {
            if (window == null || onsets == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;
            if (!EnsureCrumbDrawer()) return;

            _crumbHeld = false;

            bool replaced = !ReferenceEquals(window, _crumbWindow);
            bool reopened = replaced || window.FrameIndex < _crumbFrame;

            if (!reopened && window.FrameIndex == _crumbFrame) return;

            if (reopened)
            {
                if (replaced) Debug.Log(CrumbWindowLine(window, speed));

                CrumbBites.Clear();
                CrumbSeen.Clear();
                _crumbShed = 0;
                _crumbMissed = 0;
                if (replaced) _crumbLines = 0;
            }

            _crumbWindow = window;
            _crumbFrame = window.FrameIndex;
            _crumbSecond = window.Second;
            _crumbSpeed = speed;

            double second = window.Second;
            double life = CrumbLifeSeconds * speed;
            FilmWindowReader reader = window.Window;

            onsets.Between(second - life, second, speed, CrumbOnsets);

            for (int i = 0; i < CrumbOnsets.Count; i++)
            {
                BiteOnset onset = CrumbOnsets[i];
                var key = (onset.Id, onset.Part, onset.Second);
                if (CrumbSeen.Contains(key)) continue;

                int at = reader.NearestFrame(onset.Second);
                if (at > window.FrameIndex) continue;

                CrumbBite bite;

                if (at != window.FrameIndex && (second - onset.Second) / speed > CrumbCatchFilmSeconds)
                {
                    bite = new CrumbBite { Eater = onset.Id, Part = onset.Part, Onset = onset.Second, Skipped = true };
                    _crumbMissed++;
                }
                else
                {
                    bite = CaptureBite(window, onset);
                    if (bite.Skipped) _crumbMissed++;
                    else _crumbShed++;
                }

                CrumbSeen.Add(key);
                CrumbBites.Add(bite);
            }

            // The bites whose crumbs are all gone, forgotten; kept in their order, so the quads are written in the same order every time.
            CrumbBites.RemoveAll(b => b.Onset < second - life);
        }

        /// <summary>For a census render: nothing drawn until the next <see cref="CrumbsFrame"/>.</summary>
        private static void CrumbsSuspend()
        {
            _crumbHeld = true;
        }

        private static string CrumbWindowLine(FilmWindowWorld window, double speed)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[Theatre] bite crumbs: window t {0:0} to {1:0} s at {2:0.##}x; each recorded onset sheds crumbs at its own pose frame (or the first shown within {3:0.##} film s after it), live {4:0.##} film s, kill 7 / cut 5 / wound 3 before the prey's factor.",
                window.FirstSecond, window.LastSecond, speed, CrumbCatchFilmSeconds, CrumbLifeSeconds);
        }

        // ---------------------------------------------------------------- one onset

        /// <summary>One onset's crumbs, from the frame on screen (the onset's own, or one just after it).</summary>
        private static CrumbBite CaptureBite(FilmWindowWorld window, BiteOnset onset)
        {
            var bite = new CrumbBite { Eater = onset.Id, Part = onset.Part, Onset = onset.Second };

            if (!TryBody(window, onset.Id, out Phenotype eater, out Transform[] eaterParts) ||
                onset.Part < 0 || onset.Part >= eater.PartCount || onset.Part >= eaterParts.Length || eaterParts[onset.Part] == null ||
                !TryMouth(eater, eaterParts, onset.Part, out Vector3 mouth, out float radius) || !(radius > 0f))
            {
                bite.Skipped = true;
                CrumbLine(window, bite, "the eater's mouth is not drawn");
                return bite;
            }

            TheatreMouth2.Measured measured = TheatreMouth2.Measure(eater, onset.Part);
            Vector3 normal = MouthNormal(eaterParts[onset.Part], measured.Point);
            if (!(normal.sqrMagnitude > 0.5f)) normal = Vector3.up;

            bite.Mouth = mouth;

            // The kind, from the kill rows: a row of this eater and part within a pose frame of the onset.
            KillRow kill = CrumbKillOf(window, onset);
            Color tint = CrumbPaleTissue;
            bool tinted = false;

            if (kill != null)
            {
                bite.Match = kill.Root == 1 ? CrumbKill : CrumbCut;
                bite.Victim = kill.Victim;
                bite.VictimPart = kill.VictimPart;
                tinted = CrumbVictimTint(window, kill.Victim, kill.VictimPart, onset.Second, out bite.Prey, out tint);
            }
            else
            {
                bite.Match = CrumbWound;
                tinted = CrumbNearestPrey(window, onset.Id, mouth, radius, out bite.Prey, out tint, out bite.Victim, out bite.VictimPart);
            }

            if (!tinted) tint = CrumbPaleTissue;

            bite.Tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint;

            // The count and the size by the kind: a kill sheds most and largest, a wound least; a leaf more and smaller than an animal.
            int count = bite.Match == CrumbKill ? 14 : bite.Match == CrumbCut ? 10 : 6;
            float size = bite.Match == CrumbKill ? 1.35f : bite.Match == CrumbCut ? 1.15f : 1f;

            if (bite.Prey == PreyLeaf)
            {
                count += (count + 2) / 3;
                size *= 0.8f;
            }
            else if (bite.Prey != PreyAnimal)
            {
                size *= 0.9f;
            }

            // D289, dial on only: a plant kill (a leaf part, the whole plant taken) throws more, wider, larger crumbs. Every other bite
            // keeps spread 1 and its count and size above untouched. Crumb i's hashes depend on i alone, so a plant kill's first crumbs
            // are exactly the ones the dial-off bite throws and the extra ones follow them; no salt or stream moves for any other bite.
            float spread = 1f;

            if (PlantCrumbs && bite.Match == CrumbKill && bite.Prey == PreyLeaf)
            {
                count *= PlantKillCrumbFactor;
                size *= PlantKillRadiusFactor;
                spread = PlantKillSpreadFactor;

                if (!_plantCrumbsTold)
                {
                    _plantCrumbsTold = true;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] bite crumbs: a plant kill sheds extra crumbs (EVOSIM_THEATRE_PLANT_CRUMBS=1): {0}x the count, thrown {1:0.##}x as far, {2:0.##}x the size, in the plant's own colour; first at t {3:0.###} eater {4} part {5}, {6} crumbs.",
                        PlantKillCrumbFactor, PlantKillSpreadFactor, PlantKillRadiusFactor, onset.Second, onset.Id, onset.Part, count));
                }
            }

            float mean = Mathf.Clamp(CrumbMouthShare * radius, CrumbRadiusLow, CrumbRadiusHigh) * size;
            long id = onset.Id;
            int part = onset.Part;
            double at = onset.Second;

            bite.Crumbs = new Crumb[count];

            for (int i = 0; i < count; i++)
            {
                // Torn at the contact, just in front of the mouth, and thrown off mostly outward and to the sides.
                Vector3 about = CrumbUnit(CrumbHash(id, part, at, i, 2), CrumbHash(id, part, at, i, 3));
                Vector3 birth = mouth + normal * (0.6f * radius) + about * (0.4f * radius * CrumbHash(id, part, at, i, 4));

                // Outward and to the sides, never back into the eater: the normal outweighs any unit vector.
                Vector3 way = normal * 1.2f + CrumbUnit(CrumbHash(id, part, at, i, 5), CrumbHash(id, part, at, i, 6));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : normal;

                float period = Mathf.Lerp(CrumbWobbleLow, CrumbWobbleHigh, CrumbHash(id, part, at, i, 11));

                bite.Crumbs[i] = new Crumb
                {
                    Birth = birth,
                    Throw = way * (spread * CrumbThrowRadii * radius * Mathf.Lerp(0.4f, 1f, CrumbHash(id, part, at, i, 7))),
                    Radius = mean * Mathf.Lerp(0.6f, 1.4f, CrumbHash(id, part, at, i, 1)),
                    Sink = CrumbSinkMetresPerSecond * (1f + CrumbSinkJitter * (CrumbHash(id, part, at, i, 8) - 0.5f)),
                    Phase = new Vector3(
                        6.2831853f * CrumbHash(id, part, at, i, 9), 6.2831853f * CrumbHash(id, part, at, i, 10), 6.2831853f * CrumbHash(id, part, at, i, 12)),
                    Omega = 6.2831853f / period,
                    Seed = CrumbHash(id, part, at, i, 13) * 97f,
                };
            }

            // The flakes, dial on only: hashed after the crumbs, with salts of their own, so nothing above moves.
            if (CrumbFlakes) CrumbFlakesOf(window, bite, mouth, normal, radius);

            CrumbLine(window, bite, null);
            return bite;
        }

        // ---------------------------------------------------------------- the flakes (EVOSIM_THEATRE_CRUMB_FLAKES only)

        // A bite's flakes, one a crumb, and its specks: every number hashed from the bite and the index with salts 20 and over, which
        // no crumb uses. Called only with CrumbFlakes on.
        private static void CrumbFlakesOf(FilmWindowWorld window, CrumbBite bite, Vector3 mouth, Vector3 normal, float radius)
        {
            long id = bite.Eater;
            int part = bite.Part;
            double at = bite.Onset;

            bite.Flakes = new CrumbFlake[bite.Crumbs.Length];
            for (int i = 0; i < bite.Flakes.Length; i++) bite.Flakes[i] = FlakeOf(id, part, at, i, bite.Tint, false);

            // The tissue thrown, held to a share of the piece taken: one factor on every flake's size, after the draw, so no hash moves.
            float scale = FlakeBudgetScale(window, bite, radius);
            for (int i = 0; i < bite.Flakes.Length; i++) bite.Flakes[i].Size *= scale;

            int specks = Math.Min(FlakeSpecksHigh,
                FlakeSpecksLow + (int)(CrumbHash(id, part, at, FlakeSpeckIndex, 40) * (FlakeSpecksHigh - FlakeSpecksLow + 1)));

            bite.Specks = new Crumb[specks];
            bite.SpeckFlakes = new CrumbFlake[specks];

            for (int j = 0; j < specks; j++)
            {
                int k = FlakeSpeckIndex + 1 + j;

                // Torn at the contact as the crumbs are, thrown less far, sinking slower: a fine grain left in the water behind them.
                Vector3 about = CrumbUnit(CrumbHash(id, part, at, k, 41), CrumbHash(id, part, at, k, 42));
                Vector3 birth = mouth + normal * (0.6f * radius) + about * (0.5f * radius * CrumbHash(id, part, at, k, 43));

                Vector3 way = normal * 0.8f + CrumbUnit(CrumbHash(id, part, at, k, 44), CrumbHash(id, part, at, k, 45));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : normal;

                float period = Mathf.Lerp(CrumbWobbleLow, CrumbWobbleHigh, CrumbHash(id, part, at, k, 46));

                bite.Specks[j] = new Crumb
                {
                    Birth = birth,
                    Throw = way * (FlakeSpeckThrowRadii * radius * Mathf.Lerp(0.3f, 1f, CrumbHash(id, part, at, k, 47))),
                    Radius = Mathf.Lerp(FlakeSpeckRadiusLow, FlakeSpeckRadiusHigh, CrumbHash(id, part, at, k, 48)),
                    Sink = FlakeSpeckSinkShare * CrumbSinkMetresPerSecond * (1f + CrumbSinkJitter * (CrumbHash(id, part, at, k, 49) - 0.5f)),
                    Phase = new Vector3(
                        6.2831853f * CrumbHash(id, part, at, k, 50), 6.2831853f * CrumbHash(id, part, at, k, 51), 6.2831853f * CrumbHash(id, part, at, k, 52)),
                    Omega = 6.2831853f / period,
                    Seed = 0f,
                };

                bite.SpeckFlakes[j] = FlakeOf(id, part, at, k, bite.Tint, true);
            }
        }

        // The one factor on a bite's flake sizes that holds their summed area to a share of the piece the bite took (FlakeShareKill, Cut,
        // Wound), never above 1. The piece: a kill's victim part's largest face; a cut's or a wound's a disc of the mouth's radius, held to
        // the victim part's face where that part is known; with no victim part, the mouth's disc. A flake's area is FlakeOutlineArea times
        // the square of its nominal half-axis at the radius AddCrumbFlake draws (its crumb's times its size, held to FlakeRadiusMost).
        private static float FlakeBudgetScale(FilmWindowWorld window, CrumbBite bite, float radius)
        {
            float disc = Mathf.PI * radius * radius;
            float face = 0f;

            if (bite.Victim >= 0L && bite.VictimPart >= 0)
            {
                Phenotype shape = CrumbShapeOf(window, bite.Victim, bite.Onset);
                if (shape != null && bite.VictimPart < shape.PartCount) face = FlakePartFace(shape.Parts[bite.VictimPart]);
            }

            float piece = bite.Match == CrumbKill && face > 0f ? face : face > 0f ? Mathf.Min(disc, face) : disc;
            float share = bite.Match == CrumbKill ? FlakeShareKill : bite.Match == CrumbCut ? FlakeShareCut : FlakeShareWound;
            float budget = share * piece;

            float thrown = 0f;
            for (int i = 0; i < bite.Flakes.Length; i++)
            {
                float r = FlakeSpan * Mathf.Min(bite.Crumbs[i].Radius * bite.Flakes[i].Size, FlakeRadiusMost);
                thrown += FlakeOutlineArea * r * r;
            }

            if (!(thrown > 0f) || !(budget > 0f)) return 1f;
            return Mathf.Min(1f, Mathf.Sqrt(budget / thrown));
        }

        // A part's largest face, m^2: four times the product of its two largest half-extents (a leaf's blade). 0 where they are not
        // positive.
        private static float FlakePartFace(PhenotypePart part)
        {
            Float3 h = part.HalfExtents;
            float a = Mathf.Max(h.X, Mathf.Max(h.Y, h.Z));
            float c = Mathf.Min(h.X, Mathf.Min(h.Y, h.Z));
            float b = h.X + h.Y + h.Z - a - c;
            return a > 0f && b > 0f ? 4f * a * b : 0f;
        }

        // One flake's shape, tumble and colour, hashed with salts 20 to 34.
        private static CrumbFlake FlakeOf(long id, int part, double at, int index, Color tint, bool speck)
        {
            bool strand = !speck && CrumbHash(id, part, at, index, 21) < 1f / FlakeStrandOdds;

            // Many small, a few large: a Pareto from FlakeSizeLow, held to FlakeSizeMost.
            float size = speck ? 1f : Mathf.Min(FlakeSizeMost, FlakeSizeLow * Mathf.Pow(1f - CrumbHash(id, part, at, index, 20), -1f / FlakeSizeTail));

            float shape = CrumbHash(id, part, at, index, 22);
            float aspect = strand ? Mathf.Lerp(FlakeStrandAspectLow, FlakeStrandAspectHigh, shape)
                : Mathf.Lerp(1f, speck ? FlakeSpeckAspectHigh : FlakeAspectHigh, shape);

            // Its normal mostly across the tumble's axis, so it turns over and shows its face and its edge in turn; its long axis at a
            // hashed angle in its plane.
            Vector3 axis = CrumbUnit(CrumbHash(id, part, at, index, 23), CrumbHash(id, part, at, index, 24));
            Vector3 across = Vector3.Cross(axis, CrumbUnit(CrumbHash(id, part, at, index, 25), CrumbHash(id, part, at, index, 26)));
            if (across.sqrMagnitude < 1e-6f) across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
            across.Normalize();

            Vector3 normal = (across + axis * (0.5f * CrumbHash(id, part, at, index, 27))).normalized;
            Vector3 first = Vector3.Cross(normal, axis).normalized;
            Vector3 second = Vector3.Cross(normal, first);
            float turn = 6.2831853f * CrumbHash(id, part, at, index, 28);
            Vector3 along = first * Mathf.Cos(turn) + second * Mathf.Sin(turn);

            // Its colour: the crumb's, its value and its hue varied a little.
            Color.RGBToHSV(tint, out float hue, out float saturation, out float value);
            hue = Mathf.Repeat(hue + FlakeHue * (2f * CrumbHash(id, part, at, index, 33) - 1f), 1f);
            value *= 1f + FlakeLightness * (2f * CrumbHash(id, part, at, index, 34) - 1f);
            Color colour = Color.HSVToRGB(hue, saturation, value, true);
            colour.a = speck ? FlakeSpeckOpacity : strand ? FlakeStrandOpacity : FlakeOpacity;

            return new CrumbFlake
            {
                Size = size,
                Aspect = aspect,
                Kind = speck ? FlakeKindSpeck : strand ? FlakeKindStrand : FlakeKindFlake,
                Axis = axis,
                Normal = normal,
                Long = along,
                Omega = 6.2831853f * Mathf.Lerp(FlakeTumbleLow, FlakeTumbleHigh, CrumbHash(id, part, at, index, 29)),
                Phase = 6.2831853f * CrumbHash(id, part, at, index, 30),
                Outline = new Vector2(64f * CrumbHash(id, part, at, index, 31), 64f * CrumbHash(id, part, at, index, 32)),
                Tint = colour,
            };
        }

        // The first lines of a window, one a bite, then only the status line's counts.
        private static void CrumbLine(FilmWindowWorld window, CrumbBite bite, string absent)
        {
            if (_crumbLines >= CrumbLinesPerWindow) return;
            _crumbLines++;

            var inv = CultureInfo.InvariantCulture;
            string head = string.Format(inv, "[Theatre] bite crumbs: t {0:0.###} eater {1} part {2}, shown at frame {3} (t {4:0.###}): ",
                bite.Onset, bite.Eater, bite.Part, window.FrameIndex, window.Second);

            if (absent != null)
            {
                Debug.Log(head + "none, " + absent + ".");
                return;
            }

            Debug.Log(head + string.Format(inv, "{0}, prey {1}{2}, {3} crumbs{4}.",
                CrumbMatchWords[bite.Match], CrumbPreyWords[bite.Prey],
                bite.Victim >= 0L ? " (" + bite.Victim.ToString(inv) + " part " + bite.VictimPart.ToString(inv) + ")" : "",
                bite.Crumbs.Length, bite.Prey == PreyUnknown ? ", pale tissue" : ""));
        }

        // The kill row of an onset: the same killer and part, within one pose frame's interval of it; the nearest in time wins. As
        // MatchAteKills pairs the ate-contact log's onsets.
        private static KillRow CrumbKillOf(FilmWindowWorld window, BiteOnset onset)
        {
            List<KillRow> kills = KillsOf(window);
            FilmWindowReader reader = window.Window;
            if (kills.Count == 0 || reader.FrameCount == 0) return null;

            int at = reader.NearestFrame(onset.Second);
            double interval = at >= 1 ? reader.SecondOf(at) - reader.SecondOf(at - 1)
                : reader.FrameCount > 1 ? reader.SecondOf(1) - reader.SecondOf(0) : 0d;

            KillRow best = null;
            double bestOff = double.PositiveInfinity;

            for (int j = 0; j < kills.Count; j++)
            {
                KillRow kill = kills[j];
                if (kill.Killer != onset.Id || kill.Part != onset.Part) continue;

                double off = Math.Abs(kill.T - onset.Second);
                if (off > interval + 1e-9 || off >= bestOff) continue;

                best = kill;
                bestOff = off;
            }

            return best;
        }

        // A kill row's victim: its genome developed at the plan in force at the onset (a killed body is swallowed in the pass that sets
        // the bit, so it is not drawn at the onset's frame), the row's part's cell type for leaf or animal, and its painted colour.
        // False where the row names no part or the body cannot be developed.
        private static bool CrumbVictimTint(FilmWindowWorld window, long victim, int victimPart, double second, out int prey, out Color tint)
        {
            prey = PreyUnknown;
            tint = CrumbPaleTissue;

            if (victimPart < 0) return false;

            Phenotype shape = CrumbShapeOf(window, victim, second);
            if (shape == null || victimPart >= shape.PartCount) return false;

            prey = shape.Parts[victimPart].CellTypeId == CellTypeIds.Photosynthetic ? PreyLeaf : PreyAnimal;

            TheatrePalette palette = window.Palette;
            if (palette == null) return false;

            tint = palette.PaintedGuildOf(victim, shape, victimPart, window.ColourByCellType);
            tint.a = 1f;
            return true;
        }

        // A wound's prey: the part of any other drawn body (live, corpse or remains) whose surface stands nearest the mouth point, if
        // within reach of it; its kind and its colour as the palette paints that kind of body. False where none is within reach.
        private static bool CrumbNearestPrey(FilmWindowWorld window, long eater, Vector3 mouth, float radius,
            out int prey, out Color tint, out long victim, out int victimPart)
        {
            prey = PreyUnknown;
            tint = CrumbPaleTissue;
            victim = -1L;
            victimPart = -1;

            float reach = Mathf.Max(CrumbPreyReachRadii * radius, CrumbPreyReachMetres);
            float best = reach;
            AteCandidate found = default;
            int foundPart = -1;

            List<AteCandidate> candidates = AteCandidatesOf(window);

            for (int i = 0; i < candidates.Count; i++)
            {
                AteCandidate candidate = candidates[i];
                if (candidate.Id == eater && candidate.Kind == null) continue;

                Phenotype other = candidate.Body;
                Transform[] parts = candidate.Parts;
                if (other == null || parts == null) continue;

                for (int q = 0; q < other.PartCount && q < parts.Length; q++)
                {
                    if (parts[q] == null) continue;

                    // The surface is no nearer than the centre less the part's bound, so a part that cannot beat the best is skipped.
                    if (Vector3.Distance(mouth, parts[q].position) - BoundRadius(other.Parts[q]) > best) continue;

                    float d = SignedDistance(other.Parts[q], parts[q], mouth);

                    if (d < best)
                    {
                        best = d;
                        found = candidate;
                        foundPart = q;
                    }
                }
            }

            if (foundPart < 0) return false;

            victim = found.Id;
            victimPart = foundPart;

            TheatrePalette palette = window.Palette;
            bool on = window.ColourByCellType;
            PhenotypePart part = found.Body.Parts[foundPart];

            if (found.Kind == "corpse") prey = PreyCorpse;
            else if (found.Kind == "remains") prey = PreyRemains;
            else prey = part.CellTypeId == CellTypeIds.Photosynthetic ? PreyLeaf : PreyAnimal;

            if (palette == null) return false;

            // Painted as the crumble's flakes are: a corpse under its own key, grey at the corpse's brightness; remains dulled.
            if (prey == PreyCorpse)
            {
                Color guild = palette.PaintedGuildOf(-(found.Id + 1L), found.Body, foundPart, on, true);
                if (on) guild = TheatrePalette.Grey(guild) * palette.CorpseBrightness;
                tint = guild;
            }
            else if (prey == PreyRemains)
            {
                Color guild = palette.PaintedGuildOf(found.Id, found.Body, foundPart, on);
                if (on) guild = Color.Lerp(guild, TheatrePalette.Grey(guild), Mathf.Clamp01(palette.RemainsGreyness)) * palette.RemainsBrightness;
                tint = guild;
            }
            else
            {
                tint = palette.PaintedGuildOf(found.Id, found.Body, foundPart, on);
            }

            tint.a = 1f;
            return true;
        }

        // A body's phenotype at the plan in force at a second, as EatingPartsOf develops it; null where it cannot be. Cached per body and
        // plan for the window.
        private static Phenotype CrumbShapeOf(FilmWindowWorld window, long id, double second)
        {
            if (!ReferenceEquals(window, _crumbShapesWindow))
            {
                _crumbShapesWindow = window;
                CrumbGenomes.Clear();
                CrumbShapes.Clear();
            }

            FilmWindowVerdict verdict = window.Window.Verdict;
            double cut = verdict != null && !double.IsNaN(verdict.From)
                ? verdict.From + (double.IsNaN(verdict.PhysicsDt) ? 0d : 0.999d * verdict.PhysicsDt)
                : window.FirstSecond;

            IReadOnlyList<FilmWindowPlan> plans = window.Window.PlansOf(id);
            FilmWindowPlan plan = null;

            for (int i = 0; i < plans.Count; i++)
            {
                double at = plans[i].Seconds;
                if (at <= cut || at < second - 1e-6) plan = plans[i];
                else break;
            }

            if (CrumbShapes.TryGetValue((id, plan), out Phenotype known)) return known;

            if (!CrumbGenomes.TryGetValue(id, out Genome genome))
            {
                genome = null;

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

                CrumbGenomes[id] = genome;
            }

            Phenotype shape = null;

            if (genome != null)
            {
                try
                {
                    shape = SnapshotWorld.DevelopWithPlan(genome, window.Record.Config, plan?.ModuleCounts, plan?.LostPaths);
                }
                catch (Exception)
                {
                    shape = null;
                }
            }

            CrumbShapes[(id, plan)] = shape;
            return shape;
        }

        // ---------------------------------------------------------------- the hash

        private static uint CrumbMix(uint x)
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

        /// <summary>A number in [0, 1) hashed from an eater, its part, the onset's second (to the millisecond), a crumb and a salt.</summary>
        private static float CrumbHash(long id, int part, double onset, int index, int salt)
        {
            unchecked
            {
                long ms = (long)Math.Round(onset * 1000d);
                uint key = CrumbMix((uint)id ^ CrumbMix((uint)(id >> 32) + 0x9E3779B9U));
                key = CrumbMix(key ^ ((uint)part * 0x85EBCA6BU) ^ CrumbMix((uint)ms ^ ((uint)(ms >> 32) * 0xC2B2AE35U)));
                uint h = CrumbMix(key + (uint)index * 0x9E3779B9U + (uint)salt * 0x27D4EB2FU);
                return (h >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>A unit vector from two uniform numbers, uniform over the sphere.</summary>
        private static Vector3 CrumbUnit(float a, float b)
        {
            float z = 2f * a - 1f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = 6.2831853f * b;
            return new Vector3(r * Mathf.Cos(phi), z, r * Mathf.Sin(phi));
        }

        // ---------------------------------------------------------------- the drawing

        // The flakes' shader, a mesh, an object to draw it in the camera's render, and the hook; made once, when the first crumbs are
        // asked for. False when the shader cannot be found, said once.
        private static bool EnsureCrumbDrawer()
        {
            if (_crumbNoShader) return false;

            // The flakes, dial on only: their own shader and material in the crumbs' place, so the block below never runs for them.
            if (CrumbFlakes && _crumbMaterial == null)
            {
                Shader flakes = Shader.Find(CrumbFlakesShader);

                if (flakes == null)
                {
                    _crumbNoShader = true;
                    Debug.LogWarning("[Theatre] bite crumbs: shader '" + CrumbFlakesShader + "' not found (EVOSIM_THEATRE_CRUMB_FLAKES=1), so no crumbs are drawn");
                    return false;
                }

                _crumbMaterial = new Material(flakes) { name = "Theatre Bite Crumb Flakes", hideFlags = HideFlags.HideAndDontSave };
            }

            if (_crumbMaterial == null)
            {
                Shader shader = Shader.Find("Evosim/Theatre Crumble Flakes");

                if (shader == null)
                {
                    _crumbNoShader = true;
                    Debug.LogWarning("[Theatre] bite crumbs: shader 'Evosim/Theatre Crumble Flakes' not found, so no crumbs are drawn");
                    return false;
                }

                _crumbMaterial = new Material(shader) { name = "Theatre Bite Crumbs", hideFlags = HideFlags.HideAndDontSave };
            }

            if (_crumbMesh == null)
            {
                _crumbMesh = new Mesh { name = "Theatre Bite Crumbs", hideFlags = HideFlags.HideAndDontSave };
                _crumbMesh.MarkDynamic();
            }

            // The object belongs to the scene the film plays in, and is made again if a scene change took it.
            if (_crumbObject == null)
            {
                _crumbObject = new GameObject("Theatre Bite Crumbs");
                _crumbObject.AddComponent<MeshFilter>().sharedMesh = _crumbMesh;

                _crumbRenderer = _crumbObject.AddComponent<MeshRenderer>();
                _crumbRenderer.sharedMaterial = _crumbMaterial;
                _crumbRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _crumbRenderer.receiveShadows = false;

                // Seen from inside the water only, as the shells and the flakes are, and turned off for the census views with them.
                _crumbObject.AddComponent<TheatreInsideOnly>();
            }

            if (_crumbPass == null) _crumbPass = new CrumbPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };

            if (!_crumbHooked)
            {
                RenderPipelineManager.beginCameraRendering += OnCrumbCamera;
                _crumbHooked = true;
            }

            if (!_crumbAnnounced)
            {
                _crumbAnnounced = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] bite crumbs on (EVOSIM_THEATRE_BITE_CRUMBS=1): a crumb {0:0.##} of the mouth's radius ({1:0.####} to {2:0.####} m), thrown up to {3:0.#} radii, sinking {4:0.###} m/s with the run's current, gone after {5:0.##} film s; drawn with the crumble's flake shader.",
                    CrumbMouthShare, CrumbRadiusLow, CrumbRadiusHigh, CrumbThrowRadii, CrumbSinkMetresPerSecond, CrumbLifeSeconds));
            }

            return true;
        }

        private static void OnCrumbCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_crumbRenderer == null || camera == null || camera.cameraType != CameraType.Game) return;

            // Turned off by the census views (TheatreInsideOnly): nothing to draw and nothing to build.
            if (!_crumbRenderer.enabled) return;

            bool lens = TheatreLens.Armed;

            try
            {
                BuildCrumbs(camera);
            }
            catch (Exception e)
            {
                // A cosmetic layer never takes the film down; the crumbs are simply absent for this render.
                _crumbDrawMesh = null;
                if (_crumbMesh != null) _crumbMesh.Clear();
                Debug.LogWarning("[Theatre] bite crumbs: the crumbs could not be built for this render: " + e.GetType().Name + ": " + e.Message);
            }

            // With the lens armed the shells are drawn after it, sharp; the crumbs follow them, as the crumble's flakes do.
            _crumbRenderer.forceRenderingOff = lens;

            if (lens && _crumbDrawMesh != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_crumbPass);
            }
        }

        private static void BuildCrumbs(Camera camera)
        {
            Transform view = camera.transform;
            TheatreSkin skin = TheatreSkin.Current;
            Material shells = skin != null ? skin.SnowShellsMaterial : null;

            // The run's water is opened by the first push of a frame; until then the current at a mouth would read zero.
            bool water = skin != null && (skin.RunWaterOpen || !skin.RunWaterWanted);

            bool same = ReferenceEquals(_crumbBuiltCamera, camera) && _crumbSecond == _crumbBuiltSecond && view.position == _crumbBuiltEye &&
                        view.rotation == _crumbBuiltRotation && camera.fieldOfView == _crumbBuiltFov && camera.aspect == _crumbBuiltAspect &&
                        _crumbHeld == _crumbBuiltHeld && water;
            if (same) return;

            _crumbBuiltCamera = camera;
            _crumbBuiltSecond = _crumbSecond;
            _crumbBuiltEye = view.position;
            _crumbBuiltRotation = view.rotation;
            _crumbBuiltFov = camera.fieldOfView;
            _crumbBuiltAspect = camera.aspect;
            _crumbBuiltHeld = _crumbHeld;

            bool newSecond = !double.IsNaN(_crumbSecond) && Math.Floor(_crumbSecond) != _crumbStatsSecond;

            if (newSecond)
            {
                _crumbStatsSecond = Math.Floor(_crumbSecond);
                _crumbNewSeconds++;
            }

            CrumbPositions.Clear();
            CrumbCorners.Clear();
            CrumbData.Clear();
            CrumbColours.Clear();
            CrumbTriangles.Clear();
            _crumbDrawMesh = null;

            if (_crumbHeld || !water || double.IsNaN(_crumbSecond) || shells == null || _crumbMaterial == null)
            {
                if (shells == null && !_crumbNoShellsTold && !double.IsNaN(_crumbSecond) && skin != null)
                {
                    _crumbNoShellsTold = true;
                    Debug.Log("[Theatre] bite crumbs: the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the crumbs cannot be drawn in the water's box");
                }

                _crumbMesh.Clear();
                return;
            }

            // The flakes, dial on only: built by their own lines; everything after this is the crumbs as they always were.
            if (CrumbFlakes)
            {
                BuildCrumbFlakes(camera, skin, shells, newSecond);
                return;
            }

            // The water the shells sit in, and the colour and gain of their motes.
            _crumbMaterial.SetColor("_MoteColor", shells.GetColor("_MoteColor"));
            _crumbMaterial.SetFloat("_ShellGain", shells.GetFloat("_ShellGain"));
            _crumbMaterial.SetFloat("_MoteAmbient", shells.GetFloat("_MoteAmbient"));
            _crumbMaterial.SetVector("_BoxMin", shells.GetVector("_BoxMin"));
            _crumbMaterial.SetVector("_BoxMax", shells.GetVector("_BoxMax"));
            _crumbMaterial.SetVector("_Tank", shells.GetVector("_Tank"));
            _crumbMaterial.SetFloat("_Waterline", shells.GetFloat("_Waterline"));
            _crumbMaterial.SetFloat("_TakeSeconds", CrumbTakeSeconds);

            GeometryUtility.CalculateFrustumPlanes(camera, CrumbPlanes);

            bool open = skin.RunWaterOpen;
            double speed = Math.Max(1d, _crumbSpeed);
            int bites = 0, drawn = 0;

            for (int b = 0; b < CrumbBites.Count; b++)
            {
                CrumbBite bite = CrumbBites[b];
                if (bite.Skipped || bite.Crumbs == null) continue;

                double ageWorld = _crumbSecond - bite.Onset;
                float ageFilm = (float)(ageWorld / speed);
                if (ageWorld < 0d || ageFilm >= CrumbLifeSeconds) continue;

                // The current at the mouth at the onset: a function of the place and the second, read once.
                if (!bite.CurrentKnown)
                {
                    bite.Current = open ? skin.WaterAt(bite.Mouth, bite.Onset) : Vector3.zero;
                    bite.CurrentKnown = true;
                }

                bites++;

                float settled = 1f - Mathf.Exp(-ageFilm / CrumbThrowSeconds);
                float left = Mathf.Clamp01((CrumbLifeSeconds - ageFilm) / CrumbDissolveSeconds);
                float shrink = left * left * (3f - 2f * left);
                float age = (float)ageWorld;

                for (int i = 0; i < bite.Crumbs.Length; i++)
                {
                    Crumb c = bite.Crumbs[i];

                    // Its birth, the tear's throw settling, the water it was born into and its sink, and a small wobble that starts at nothing.
                    Vector3 place = c.Birth + c.Throw * settled + (bite.Current + new Vector3(0f, -c.Sink, 0f)) * age;
                    place.x += CrumbWobbleMetres * (Mathf.Sin(c.Omega * ageFilm + c.Phase.x) - Mathf.Sin(c.Phase.x));
                    place.y += 0.5f * CrumbWobbleMetres * (Mathf.Sin(1.17f * c.Omega * ageFilm + c.Phase.y) - Mathf.Sin(c.Phase.y));
                    place.z += CrumbWobbleMetres * (Mathf.Sin(0.83f * c.Omega * ageFilm + c.Phase.z) - Mathf.Sin(c.Phase.z));

                    if (!CrumbInFrustum(place, CrumbMargin)) continue;

                    // Dissolving over its last stretch: the matter shrinks away, it does not fade.
                    float radius = c.Radius * shrink;
                    if (!(radius > 0f)) continue;

                    int first = CrumbPositions.Count;
                    // w = 1: a large mote, which the shader draws as a soft round disc rather than as a crumble flake.
                    var data = new Vector4(radius, c.Seed, ageFilm, 1f);

                    for (int corner = 0; corner < 4; corner++)
                    {
                        CrumbPositions.Add(place);
                        CrumbCorners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                        CrumbData.Add(data);
                        CrumbColours.Add(bite.Tint);
                    }

                    CrumbTriangles.Add(first);
                    CrumbTriangles.Add(first + 1);
                    CrumbTriangles.Add(first + 2);
                    CrumbTriangles.Add(first);
                    CrumbTriangles.Add(first + 2);
                    CrumbTriangles.Add(first + 3);

                    drawn++;
                }
            }

            if (drawn > 0)
            {
                _crumbMesh.Clear();
                _crumbMesh.indexFormat = IndexFormat.UInt32;
                _crumbMesh.SetVertices(CrumbPositions);
                _crumbMesh.SetUVs(0, CrumbCorners);
                _crumbMesh.SetUVs(1, CrumbData);
                _crumbMesh.SetColors(CrumbColours);
                _crumbMesh.SetTriangles(CrumbTriangles, 0, false);
                _crumbMesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
                _crumbDrawMesh = _crumbMesh;
            }
            else
            {
                _crumbMesh.Clear();
            }

            if (newSecond && (_crumbNewSeconds == 1 || _crumbNewSeconds % 30 == 0))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] bite crumbs: second {0:0.###}, {1} bites shedding, {2} crumbs drawn; this pass {3} onsets shed, {4} not (their frame not shown, or no mouth drawn)",
                    _crumbSecond, bites, drawn, _crumbShed, _crumbMissed));
            }
        }

        private static bool CrumbInFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < CrumbPlanes.Length; i++)
            {
                if (CrumbPlanes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        // The flakes' vertex lists, made the first time the flakes are built (dial on only).
        private static List<Vector4> _flakeCorners;
        private static List<Vector4> _flakeAlong;
        private static List<Vector4> _flakeAcross;

        // The crumbs as flakes (CrumbFlakes on only): each crumb at the place BuildCrumbs would put it, drawn as a flat quad tumbling
        // in the world, and each bite's specks; the mesh, the frustum and the counts as BuildCrumbs keeps them. The caller has cleared
        // the shared lists and checked the shells, the water and the hold.
        private static void BuildCrumbFlakes(Camera camera, TheatreSkin skin, Material shells, bool newSecond)
        {
            _flakeCorners ??= new List<Vector4>();
            _flakeAlong ??= new List<Vector4>();
            _flakeAcross ??= new List<Vector4>();
            _flakeCorners.Clear();
            _flakeAlong.Clear();
            _flakeAcross.Clear();

            // The water the shells sit in.
            _crumbMaterial.SetVector("_BoxMin", shells.GetVector("_BoxMin"));
            _crumbMaterial.SetVector("_BoxMax", shells.GetVector("_BoxMax"));
            _crumbMaterial.SetVector("_Tank", shells.GetVector("_Tank"));
            _crumbMaterial.SetFloat("_Waterline", shells.GetFloat("_Waterline"));

            GeometryUtility.CalculateFrustumPlanes(camera, CrumbPlanes);

            bool open = skin.RunWaterOpen;
            double speed = Math.Max(1d, _crumbSpeed);
            int bites = 0, drawn = 0, strands = 0, specks = 0;

            for (int b = 0; b < CrumbBites.Count; b++)
            {
                CrumbBite bite = CrumbBites[b];
                if (bite.Skipped || bite.Crumbs == null || bite.Flakes == null) continue;

                double ageWorld = _crumbSecond - bite.Onset;
                float ageFilm = (float)(ageWorld / speed);
                if (ageWorld < 0d || ageFilm >= CrumbLifeSeconds) continue;

                if (!bite.CurrentKnown)
                {
                    bite.Current = open ? skin.WaterAt(bite.Mouth, bite.Onset) : Vector3.zero;
                    bite.CurrentKnown = true;
                }

                bites++;

                // The crumbs' own settle and dissolve.
                float settled = 1f - Mathf.Exp(-ageFilm / CrumbThrowSeconds);
                float left = Mathf.Clamp01((CrumbLifeSeconds - ageFilm) / CrumbDissolveSeconds);
                float shrink = left * left * (3f - 2f * left);
                float age = (float)ageWorld;

                for (int i = 0; i < bite.Crumbs.Length; i++)
                {
                    if (!AddCrumbFlake(bite.Crumbs[i], bite.Flakes[i], bite.Current, settled, shrink, age, ageFilm)) continue;

                    drawn++;
                    if (bite.Flakes[i].Kind == FlakeKindStrand) strands++;
                }

                if (bite.Specks == null) continue;

                for (int j = 0; j < bite.Specks.Length; j++)
                {
                    if (AddCrumbFlake(bite.Specks[j], bite.SpeckFlakes[j], bite.Current, settled, shrink, age, ageFilm)) specks++;
                }
            }

            if (drawn + specks > 0)
            {
                _crumbMesh.Clear();
                _crumbMesh.indexFormat = IndexFormat.UInt32;
                _crumbMesh.SetVertices(CrumbPositions);
                _crumbMesh.SetUVs(0, _flakeCorners);
                _crumbMesh.SetUVs(1, _flakeAlong);
                _crumbMesh.SetUVs(2, _flakeAcross);
                _crumbMesh.SetColors(CrumbColours);
                _crumbMesh.SetTriangles(CrumbTriangles, 0, false);
                _crumbMesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
                _crumbDrawMesh = _crumbMesh;

                if (!_flakesTold)
                {
                    _flakesTold = true;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] bite crumbs: drawn as torn flakes (EVOSIM_THEATRE_CRUMB_FLAKES=1) with '{0}': flat quads tumbling at {1:0.#} to {2:0.#} turns a film s, {3:0.##} to {4:0.#}x the crumb's radius on a power law (tail {5:0.#}), 1 in {6} a strand of aspect {7:0} to {8:0}, and {9} to {10} specks of {11:0.##} to {12:0.#} mm a bite; first at second {13:0.###}, {14} flakes and {15} specks.",
                        CrumbFlakesShader, FlakeTumbleLow, FlakeTumbleHigh, FlakeSizeLow, FlakeSizeMost, FlakeSizeTail, FlakeStrandOdds,
                        FlakeStrandAspectLow, FlakeStrandAspectHigh, FlakeSpecksLow, FlakeSpecksHigh, 2000f * FlakeSpeckRadiusLow,
                        2000f * FlakeSpeckRadiusHigh, _crumbSecond, drawn, specks));
                }
            }
            else
            {
                _crumbMesh.Clear();
            }

            if (newSecond && (_crumbNewSeconds == 1 || _crumbNewSeconds % 30 == 0))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] bite crumbs: second {0:0.###}, {1} bites shedding, {2} flakes drawn ({3} strands) and {4} specks; this pass {5} onsets shed, {6} not (their frame not shown, or no mouth drawn)",
                    _crumbSecond, bites, drawn, strands, specks, _crumbShed, _crumbMissed));
            }
        }

        // One crumb's flake into the lists, at the place BuildCrumbs puts the crumb (the same lines), its tumble turned to its age. False
        // when it is gone or outside the frustum.
        private static bool AddCrumbFlake(in Crumb c, in CrumbFlake f, Vector3 current, float settled, float shrink, float age, float ageFilm)
        {
            Vector3 place = c.Birth + c.Throw * settled + (current + new Vector3(0f, -c.Sink, 0f)) * age;
            place.x += CrumbWobbleMetres * (Mathf.Sin(c.Omega * ageFilm + c.Phase.x) - Mathf.Sin(c.Phase.x));
            place.y += 0.5f * CrumbWobbleMetres * (Mathf.Sin(1.17f * c.Omega * ageFilm + c.Phase.y) - Mathf.Sin(c.Phase.y));
            place.z += CrumbWobbleMetres * (Mathf.Sin(0.83f * c.Omega * ageFilm + c.Phase.z) - Mathf.Sin(c.Phase.z));

            // Its radius on the power law, dissolving as the crumb does; its half-axes keep the same area at any aspect.
            float radius = Mathf.Min(c.Radius * f.Size, FlakeRadiusMost) * shrink;
            if (!(radius > 0f)) return false;

            float root = Mathf.Sqrt(f.Aspect);
            float half = FlakeSpan * radius * root;
            float narrow = FlakeSpan * radius / root;

            if (!CrumbInFrustum(place, Mathf.Max(CrumbMargin, 1.3f * half))) return false;

            // The tumble: its normal and long axis turned about its axis by its phase and rate times its age.
            Quaternion turn = Quaternion.AngleAxis((f.Phase + f.Omega * ageFilm) * Mathf.Rad2Deg, f.Axis);
            Vector3 normal = turn * f.Normal;
            Vector3 along = turn * f.Long;
            Vector3 across = Vector3.Cross(normal, along);

            var alongData = new Vector4(along.x * half, along.y * half, along.z * half, f.Kind);
            var acrossData = new Vector4(across.x * narrow, across.y * narrow, across.z * narrow, 0f);

            int first = CrumbPositions.Count;

            for (int corner = 0; corner < 4; corner++)
            {
                CrumbPositions.Add(place);
                _flakeCorners.Add(new Vector4((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f, f.Outline.x, f.Outline.y));
                _flakeAlong.Add(alongData);
                _flakeAcross.Add(acrossData);
                CrumbColours.Add(f.Tint);
            }

            CrumbTriangles.Add(first);
            CrumbTriangles.Add(first + 1);
            CrumbTriangles.Add(first + 2);
            CrumbTriangles.Add(first);
            CrumbTriangles.Add(first + 2);
            CrumbTriangles.Add(first + 3);

            return true;
        }

        /// <summary>Draws the crumbs after the theatre's lens, as the crumble draws its flakes: each carries its own blur.</summary>
        private sealed class CrumbPass : ScriptableRenderPass
        {
            public CrumbPass()
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
                Mesh mesh = _crumbDrawMesh;
                Material material = _crumbMaterial;
                if (mesh == null || material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre Bite Crumbs", out PassData data))
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
