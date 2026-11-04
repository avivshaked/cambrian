using System.Globalization;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The birth cloud's bubbles (<c>EVOSIM_THEATRE_BIRTH_BUBBLES</c>): with the birth cloud on, a burst of air bubbles at the birth
    /// place in place of its pale motes. They rise, the large ones wobbling and swelling a little, the small ones dissolving, and leave
    /// the newborn visible within about two seconds of the run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing here is made.</b> <see cref="On"/> is read only with <see cref="TheatreBirthCloud.On"/>, and
    /// <see cref="TheatreBirthCloud"/> asks it only on paths that run with its own dial on. With it off no bubble, material or log line
    /// is made and the motes are drawn exactly as before. The birth cloud keeps its timing, placement and caption: the newborn's box,
    /// the lead, the adoption at the first drawn frame and the catch rule are its own, and this class only makes and draws bubbles.
    /// </para>
    /// <para>
    /// <b>A bubble is a function of (newborn, index).</b> Its size, start in the burst, moment of release, rise, wobble and end are
    /// hashed from the newborn's id and its index with the cloud's own hash, so a film draws the same burst every time. Diameters run
    /// from 2 mm to 2 cm, absolute and not scaled with the body, drawn from a power law so most are small and a few large.
    /// </para>
    /// <para>
    /// <b>On the run's clock.</b> Unlike the motes' thinning, which is film time, a bubble lives in the run's seconds: a time-lapse
    /// compresses the burst, it does not stretch it. With a lead a few bubbles leave before the birth row's second, more of them as it
    /// nears, and the rest go in one burst just after it.
    /// </para>
    /// <para>
    /// <b>What the size does is the physics.</b> A small bubble (under about 5 mm) dissolves: it shrinks to nothing at its own hashed
    /// moment. A large one swells by a few per cent as it rises into lower pressure, and leaves the shot upward or fades (its opacity,
    /// not its size) at its end, as the water's fog fades the far ones.
    /// </para>
    /// <para>
    /// <b>Drawn in the cloud's mesh, by <c>Evosim/Theatre Bubbles</c></b>: each a camera-facing quad holding a sphere impostor, with
    /// the silver rim of total internal reflection, a small highlight toward the sun, a slightly bent view of the water through its
    /// centre, the lens's blur and the water's fog.
    /// </para>
    /// <para>
    /// <b>A second take, three dials of its own, each 0 by default and read only with this one on.</b> With all three unset every
    /// bubble, vertex and frame is the first take's. <see cref="Cover"/> appends a dense, held foam about the newborn after the first
    /// take's bubbles (salts from 51), so the arrival is hidden and then uncovered from the bottom up. <see cref="Oblate"/> draws a
    /// bubble over about 3 mm as a wobbling oblate ellipsoid and caps the diameter at 12 mm (salt 40). <see cref="Rise2"/> rises
    /// each bubble at contaminated water's terminal speed instead of the first take's slower law. COVER = 2 (<see cref="Cluster"/>)
    /// appends instead a few hundred small bubbles in a lumpy volume about the newborn, weighted to the camera's side and released over
    /// the lead and just after the birth (salts 70-89). The shader's second-take path sits behind the local keyword
    /// <see cref="Take2Keyword"/>, set only with OBLATE or a cover on, so with the dials off the compiled shader is the first take's.
    /// </para>
    /// <para>
    /// <b>The laws are shared.</b> A bubble's rise, wobble, dissolving and fade are <see cref="Place"/>, the body <see cref="Draw"/> has
    /// always had, moved into a function that the bite bubbles (<c>EVOSIM_THEATRE_BITE_BUBBLES</c>) and the oxygen pearls
    /// (<c>EVOSIM_THEATRE_PEARLING</c>) call too, with <see cref="Single"/> making one free bubble by <see cref="Make"/>'s laws and
    /// <see cref="PowerDiameter"/> drawing its size. <see cref="Make"/>'s own loop is untouched, so the birth bubbles' frames do not move.
    /// </para>
    /// </remarks>
    public static class TheatreBirthBubbles
    {
        // ---------------------------------------------------------------- the dial

        /// <summary>
        /// The dial, <c>EVOSIM_THEATRE_BIRTH_BUBBLES</c> (0 or 1, 0 by default), read once and only with the birth cloud's own dial on.
        /// With both on the bubbles replace the motes.
        /// </summary>
        public static readonly bool On = TheatreBirthCloud.On && TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_BUBBLES", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_COVER</c> (0, 1 or 2, 0 by default), read only with <see cref="On"/>. 1 is <see cref="Cover"/>,
        /// 2 is <see cref="Cluster"/>. A value up to 1.5 reads as 1, above it as 2.
        /// </summary>
        public static readonly int CoverMode = CoverModeOf(On ? TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_BUBBLES_COVER", 0f, 0f, 2f) : 0f);

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_COVER</c> = 1: a dense foam held about the newborn at its first drawn frame, released from the
        /// bottom up, so it is uncovered rather than appearing whole.
        /// </summary>
        public static readonly bool Cover = CoverMode == 1;

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_COVER</c> = 2: a few hundred 1-8 mm bubbles in a lumpy volume about 1.3 times the newborn's
        /// box, weighted to the camera's side, released over the lead and the first 0.3 s after the birth and rising at once, so the
        /// newborn is first seen inside a moving column. Nothing is held still, and the column is clear by about 1.5 s after the birth.
        /// </summary>
        public static readonly bool Cluster = CoverMode == 2;

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_OBLATE</c> (0 or 1, 0 by default), read only with <see cref="On"/>: a bubble over about 3 mm
        /// is an oblate ellipsoid, flattened along its rise, tilting and breathing with its wobble; diameters are capped at 12 mm.
        /// </summary>
        public static readonly bool Oblate = On && TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_BUBBLES_OBLATE", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_RISE2</c> (0 or 1, 0 by default), read only with <see cref="On"/>: the rise follows
        /// <see cref="RiseOf"/>, contaminated water's terminal speed, instead of the first take's law.
        /// </summary>
        public static readonly bool Rise2 = On && TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_BUBBLES_RISE2", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_BIRTH_BUBBLES_COUNT</c> (1 to 10, 1 by default), read only with <see cref="On"/>: the first take's count
        /// times this. A bubble is a function of (id, index), so the first take's bubbles are kept and the rest are added to them; at 1
        /// every bubble is the first take's. The covers' counts are their own.
        /// </summary>
        public static readonly float CountScale = On ? TheatreSkin.Dial("EVOSIM_THEATRE_BIRTH_BUBBLES_COUNT", 1f, 1f, 10f) : 1f;

        /// <summary>The bubbles' shader.</summary>
        internal const string ShaderName = "Evosim/Theatre Bubbles";

        /// <summary>
        /// The shader's local keyword for the second take's path (the oblate impostor, its COLOR and TEXCOORD3), set on the material only
        /// with <see cref="Oblate"/> or a cover on. Without it the compiled shader is the first take's, so the dials off are pixel-identical.
        /// </summary>
        internal const string Take2Keyword = "_EVO_BUBBLES_TAKE2";

        private static int CoverModeOf(float value) => value > 1.5f ? 2 : value > 0f ? 1 : 0;

        // ---------------------------------------------------------------- the constants

        /// <summary>The count of bubbles: <see cref="CountLow"/> for a body of <see cref="ExtentLow"/> metres or less, up to <see cref="CountHigh"/> at <see cref="ExtentHigh"/>.</summary>
        private const int CountLow = 40;

        private const int CountHigh = 120;
        private const float ExtentLow = 0.03f;
        private const float ExtentHigh = 0.3f;

        /// <summary>The range of a bubble's diameter, metres, absolute: 2 mm to 2 cm.</summary>
        private const float DiameterLow = 0.002f;

        private const float DiameterHigh = 0.02f;

        /// <summary>The power law of the diameters (density as d^-SizePower): about 70% under 5 mm and 9% over 1 cm.</summary>
        private const float SizePower = 2.2f;

        /// <summary>The diameters, metres, between which a bubble goes from dissolving (shrinking to nothing) to lasting (swelling, then fading).</summary>
        private const float LastingFrom = 0.004f;

        private const float LastingFull = 0.006f;

        /// <summary>The rise, m/s of the run, at the smallest and largest diameter (by the square root of the size between), varied by <see cref="RiseJitter"/> either way.</summary>
        private const float RiseLow = 0.10f;

        private const float RiseHigh = 0.26f;
        private const float RiseJitter = 0.15f;

        /// <summary>The run's seconds over which a released bubble gathers its rising speed from rest.</summary>
        private const float RiseTau = 0.08f;

        /// <summary>The diameters, metres, over which the wobble grows from none to full: a bubble under a few millimetres rises straight.</summary>
        private const float WobbleFrom = 0.003f;

        private const float WobbleFull = 0.008f;

        /// <summary>The wobble's half-swing as a share of the diameter, and its frequency range, Hz of the run.</summary>
        private const float WobbleShare = 0.6f;

        private const float WobbleHzLow = 3f;
        private const float WobbleHzHigh = 5.5f;

        /// <summary>The run's seconds over which the wobble grows in after release, so none starts with a jump.</summary>
        private const float WobbleSettle = 0.12f;

        /// <summary>The share of the wobble's swing taken across its plane too, so a large bubble spirals a little as it zigzags.</summary>
        private const float WobbleAcross = 0.35f;

        /// <summary>Where a bubble starts: in a ball about the newborn's box of radius (in its largest half-extents) between these, from its core outward.</summary>
        private const float BallLow = 0.6f;

        private const float BallHigh = 1.1f;
        private const float BallCore = 0.2f;

        /// <summary>The run's seconds after the birth over which the burst's bubbles are released.</summary>
        private const float BurstSpread = 0.12f;

        /// <summary>With a lead, the share of the bubbles released before the birth, more of them as it nears.</summary>
        private const float EarlyShare = 0.15f;

        /// <summary>A dissolving bubble's end, run seconds after its release, hashed between these with most of them early.</summary>
        private const float EndSmallLow = 0.4f;

        private const float EndSmallHigh = 1.8f;
        private const float EndPower = 1.5f;

        /// <summary>A lasting bubble's end (where it has faded out if it has not left the shot), run seconds after its release.</summary>
        private const float EndLargeLow = 1.3f;

        private const float EndLargeHigh = 1.88f;

        /// <summary>The run's seconds before its end over which a bubble dissolves or fades.</summary>
        private const float Span = 0.35f;

        /// <summary>The run's seconds over which a released bubble grows from nothing, so none pops in.</summary>
        private const float GrowSeconds = 0.05f;

        /// <summary>How much a lasting bubble swells by its end, as a share of its radius: the pressure falls as it rises.</summary>
        private const float Swell = 0.05f;

        /// <summary>The run's seconds after the birth by which every bubble of the burst is gone; the cloud is forgotten then.</summary>
        internal const float LastWorldSeconds = BurstSpread + EndLargeHigh;

        /// <summary>A bubble outside the frustum by more than this, metres, is not built.</summary>
        private const float Margin = 0.05f;

        // ---------------------------------------------------------------- the second take's constants

        /// <summary>
        /// The cover's count: its bubbles' discs summed over each broad face, as a multiple of the face-on silhouette (a bubble's
        /// clear centre shows what lies behind, so one layer hides about half; 2.5 leaves roughly a quarter of the body showing).
        /// </summary>
        private const float CoverFill = 2.5f;

        /// <summary>The cover's count is held between these: about 140 for a 3 cm cube, the most for a face from about 0.23 m across.</summary>
        private const int CoverLeast = 24;

        private const int CoverMost = 8000;

        /// <summary>The cover's diameters, metres, uniform between these.</summary>
        private const float CoverDiameterLow = 0.003f;

        private const float CoverDiameterHigh = 0.008f;

        /// <summary>
        /// Where a cover bubble sits, as a scale of the ellipsoid inscribed in the newborn's box: on a shell between these, so the foam
        /// follows the body's outline (a flat lens on a leaf) and is never a box; <see cref="CoverDeepShare"/> of them lie deeper, from
        /// <see cref="CoverDeepLow"/>.
        /// </summary>
        private const float CoverShellLow = 0.8f;

        private const float CoverShellHigh = 1.2f;
        private const float CoverDeepShare = 0.25f;
        private const float CoverDeepLow = 0.25f;

        /// <summary>The run's seconds after the birth at which the cover is released, from its bottom to its top, varied by the jitter either way.</summary>
        private const float CoverHoldLow = 0.2f;

        private const float CoverHoldHigh = 0.4f;
        private const float CoverHoldJitter = 0.04f;

        /// <summary>With a lead, the run's seconds before the birth over which the cover gathers, each bubble growing in at its own moment.</summary>
        private const float CoverGather = 0.15f;

        /// <summary>A cover bubble's end, run seconds after its release, hashed between these with most of them early: the foam thins as it rises off.</summary>
        private const float CoverEndLow = 0.35f;

        private const float CoverEndHigh = 1.2f;

        /// <summary>The diameter, metres, from which a bubble is drawn oblate, blended in over these.</summary>
        private const float OblateFrom = 0.0025f;

        private const float OblateFull = 0.0035f;

        /// <summary>
        /// The oblate shape, wide over tall, is 1 + OblateGain Eo^OblatePower, Eo the Eotvos number g Δρ d² / σ in seawater (Δρ 1025
        /// kg/m³, σ 0.073 N/m): about 1.3 at 4 mm and 2.2 at 10 mm. From memory of Wellek, Agrawal and Skelland's correlation for
        /// contaminated systems as Clift, Grace and Weber quote it; not checked against the source.
        /// </summary>
        private const float OblateGain = 0.163f;

        private const float OblatePower = 0.757f;
        private const float EotvosPerSquareMetre = 9.81f * 1025f / 0.073f;

        /// <summary>The largest diameter with the oblate dial on, metres; a larger one is redrawn between <see cref="OblateRedrawLow"/> and it.</summary>
        private const float OblateMost = 0.012f;

        private const float OblateRedrawLow = 0.008f;

        /// <summary>How far the axis tilts with the wobble at full sway, degrees, and how much the shape breathes, as a share of its aspect.</summary>
        private const float OblateTiltDegrees = 6f;

        private const float OblateBreath = 0.1f;

        /// <summary>
        /// The rise with <see cref="Rise2"/> on, m/s of the run: RiseFar d² / (d² + RiseKnee²), so 0.10 at 1 mm, 0.18 at 2 mm, 0.24
        /// at 5 mm and 0.25 by 2 cm. From memory of Clift, Grace and Weber's curve for air in contaminated water; not checked against
        /// the source.
        /// </summary>
        private const float RiseFar = 0.25f;

        private const float RiseKnee = 0.00122f;

        // ---------------------------------------------------------------- COVER = 2, the cluster's constants

        /// <summary>The cluster's count kept on the camera's side and its edges: <see cref="ClusterLow"/> for a body of <see cref="ExtentLow"/> metres or less, up to <see cref="ClusterHigh"/> at <see cref="ExtentHigh"/>.</summary>
        private const int ClusterLow = 250;

        private const int ClusterHigh = 500;

        /// <summary>
        /// The share of the far half's bubbles dropped when the camera is first known (<see cref="LatchCluster"/>), so about 85% of those
        /// kept lie on the camera's side. The count seeded is the count wanted over (1 - ClusterHide / 2).
        /// </summary>
        private const float ClusterHide = 0.7f;

        /// <summary>The cluster's region: the ellipsoid of the newborn's half-sides times this, each semi-axis at least <see cref="ClusterDepth"/> of the largest, so a leaf gets a volume and not a layer.</summary>
        private const float ClusterReach = 1.3f;

        private const float ClusterDepth = 0.4f;

        /// <summary>The cluster's diameters, metres: a power law of <see cref="SizePower"/> from 1 mm, capped at 8 mm, so about 88% are under 4 mm.</summary>
        private const float ClusterDiameterLow = 0.001f;

        private const float ClusterDiameterHigh = 0.008f;

        /// <summary>
        /// The cluster's lumps: value noise on a hashed lattice, two octaves at these frequencies (cycles across the region's radius)
        /// and weights. A seed is kept with the chance smoothstep(<see cref="ClusterNoiseLow"/>, <see cref="ClusterNoiseHigh"/>, noise)
        /// times the edge's falloff: by a Python mirror of the method, a third to a half of the inner region is empty.
        /// </summary>
        private const float ClusterNoiseFreq = 1.7f;

        private const float ClusterNoiseFreq2 = 3.4f;
        private const float ClusterNoiseWeight = 0.65f;
        private const float ClusterNoiseLow = 0.42f;
        private const float ClusterNoiseHigh = 0.6f;

        /// <summary>The edge: density falls from full at <see cref="ClusterEdgeFrom"/> of the radius to none at 1, the radius moved by a third noise up to <see cref="ClusterEdgeWobble"/> either way, so the outline is ragged.</summary>
        private const float ClusterEdgeFrom = 0.45f;

        private const float ClusterEdgeWobble = 0.3f;
        private const float ClusterEdgeFreq = 2.3f;

        /// <summary>The candidates tried per bubble seeded before the cluster settles for fewer (the mirror needs 3 to 7).</summary>
        private const int ClusterTries = 24;

        /// <summary>The releases, run seconds about the birth: from the lead's start (at most <see cref="ClusterLeadMost"/> before it) to <see cref="ClusterReleaseLast"/> after it, uniform.</summary>
        private const float ClusterLeadMost = 0.8f;

        private const float ClusterReleaseLast = 0.3f;

        /// <summary>A cluster bubble's life after its release, hashed between these with most of them early, and cut so it is gone by its own clearing second, <see cref="ClusterClearLow"/> to <see cref="ClusterClearHigh"/> after the birth.</summary>
        private const float ClusterEndLow = 0.6f;

        private const float ClusterEndHigh = 1.8f;
        private const float ClusterClearLow = 1.0f;
        private const float ClusterClearHigh = 1.5f;

        /// <summary>The run's seconds over which a cluster bubble grows in from nothing at its release, so none pops in.</summary>
        private const float ClusterGrow = 0.12f;

        // ---------------------------------------------------------------- one bubble

        /// <summary>One bubble, fixed when its cloud is gathered.</summary>
        internal struct Bubble
        {
            /// <summary>Its place relative to the ball's centre at its release.</summary>
            public Vector3 Offset;

            public float Radius;

            /// <summary>The run's second of its release, relative to the cloud's start (negative for one released in the lead).</summary>
            public float Release;

            /// <summary>The run's seconds after its release at which it is gone.</summary>
            public float End;

            /// <summary>Its rising speed, m/s of the run.</summary>
            public float Rise;

            /// <summary>Its wobble: half-swing in metres, angular frequency, phase, and the heading of its plane in radians.</summary>
            public float WobbleMetres;

            public float Omega;
            public float Phase;
            public float Heading;

            /// <summary>How far it is a lasting bubble (1) rather than a dissolving one (0).</summary>
            public float Lasting;

            public float Seed;

            /// <summary>A cover bubble's run seconds shown, held still, before its release; 0 for every other bubble.</summary>
            public float Hold;

            /// <summary>Its oblate shape, wide over tall, with <see cref="Oblate"/> on; 0 for a sphere.</summary>
            public float Aspect;

            /// <summary>A bubble of <see cref="Cluster"/> (COVER = 2).</summary>
            public bool InCluster;

            /// <summary>A cluster bubble's hashed draw against <see cref="ClusterHide"/> until the camera is first known, then -1.</summary>
            public float Toward;

            /// <summary>A cluster bubble's seconds of growing in; 0 for every other bubble, which grows in over <see cref="GrowSeconds"/>.</summary>
            public float GrowIn;
        }

        private static bool _told;
        private static bool _toldCover;
        private static bool _toldCluster;
        private static bool _toldOblate;
        private static bool _toldRise2;

        // ---------------------------------------------------------------- making and drawing

        /// <summary>
        /// The bubbles of a newborn of this extent (metres), a function of (id, index). <paramref name="early"/> is the run's seconds
        /// before the birth over which a forming cloud's early bubbles leave; 0 for a cloud gathered at the newborn's first frame.
        /// <paramref name="box"/> is the newborn's box's three sides, metres, read only by the covers (1 and 2): the first take's
        /// bubbles depend on the id and the extent alone.
        /// </summary>
        internal static Bubble[] Make(long id, float extent, Vector3 box, float early)
        {
            float half = 0.5f * extent;
            int count = Mathf.RoundToInt(Mathf.Lerp(CountLow, CountHigh, Mathf.InverseLerp(ExtentLow, ExtentHigh, extent)));
            if (CountScale > 1f) count = Mathf.RoundToInt(count * CountScale);
            Vector3 axes = Cover ? CoverAxes(box, half) : Vector3.zero;
            int covering = Cover ? CoverCount(axes) : 0;
            var bubbles = new Bubble[count + covering];

            // The inverse of the power law's distribution between the bounds.
            float k = 1f - SizePower;
            float lowK = Mathf.Pow(DiameterLow, k);
            float highK = Mathf.Pow(DiameterHigh, k);

            for (int i = 0; i < count; i++)
            {
                float diameter = Mathf.Pow(Mathf.Lerp(lowK, highK, TheatreBirthCloud.Hash(id, i, 21)), 1f / k);
                diameter = Mathf.Clamp(diameter, DiameterLow, DiameterHigh);
                if (Oblate && diameter > OblateMost) diameter = Mathf.Lerp(OblateRedrawLow, OblateMost, TheatreBirthCloud.Hash(id, i, 40));

                float size = Mathf.InverseLerp(DiameterLow, DiameterHigh, diameter);
                float lasting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LastingFrom, LastingFull, diameter));
                float wobble = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WobbleFrom, WobbleFull, diameter));

                Vector3 way = TheatreBirthCloud.Unit(TheatreBirthCloud.Hash(id, i, 22), TheatreBirthCloud.Hash(id, i, 23));
                float ball = half * Mathf.Lerp(BallLow, BallHigh, TheatreBirthCloud.Hash(id, i, 24));
                float reach = ball * Mathf.Lerp(BallCore, 1f, Mathf.Pow(TheatreBirthCloud.Hash(id, i, 25), 1f / 3f));

                // A few leave in the lead, more of them as the birth nears; the rest in the burst just after it.
                float rank = TheatreBirthCloud.Hash(id, i, 26);
                float release = early > 0f && rank < EarlyShare
                    ? -early * (1f - Mathf.Sqrt(rank / EarlyShare))
                    : BurstSpread * TheatreBirthCloud.Hash(id, i, 27);

                float endSmall = Mathf.Lerp(EndSmallLow, EndSmallHigh, Mathf.Pow(TheatreBirthCloud.Hash(id, i, 28), EndPower));
                float endLarge = Mathf.Lerp(EndLargeLow, EndLargeHigh, TheatreBirthCloud.Hash(id, i, 29));

                float rise = (Rise2 ? RiseOf(diameter) : Mathf.Lerp(RiseLow, RiseHigh, Mathf.Sqrt(size))) *
                             (1f + RiseJitter * (2f * TheatreBirthCloud.Hash(id, i, 30) - 1f));
                float hz = Mathf.Lerp(WobbleHzLow, WobbleHzHigh, TheatreBirthCloud.Hash(id, i, 31));

                bubbles[i] = new Bubble
                {
                    Offset = way * reach,
                    Radius = 0.5f * diameter,
                    Release = release,
                    End = Mathf.Lerp(endSmall, endLarge, lasting),
                    Rise = rise,
                    WobbleMetres = WobbleShare * diameter * wobble,
                    Omega = 6.2831853f * hz,
                    Phase = 6.2831853f * TheatreBirthCloud.Hash(id, i, 32),
                    Heading = 6.2831853f * TheatreBirthCloud.Hash(id, i, 33),
                    Lasting = lasting,
                    Seed = TheatreBirthCloud.Hash(id, i, 34) * 97f,
                    Aspect = Oblate ? AspectOf(diameter) : 0f,
                };
            }

            if (covering > 0) MakeCover(id, bubbles, count, axes, early);
            if (Cluster) return WithCluster(id, bubbles, extent, box, half, early);
            return bubbles;
        }

        /// <summary>
        /// COVER = 2: the first take's bubbles with the cluster appended after them, a function of (id, index) at salts 70-89. Seeds are
        /// tried uniformly in the unit ball, kept by <see cref="ClusterDensity"/> (lumps, gaps and a ragged edge), and scaled onto the
        /// region's semi-axes. Each is released between the lead's start and <see cref="ClusterReleaseLast"/>, rises at once and is
        /// gone by its clearing second. The far half is thinned only when the camera is first known (<see cref="LatchCluster"/>).
        /// </summary>
        private static Bubble[] WithCluster(long id, Bubble[] first, float extent, Vector3 box, float half, float early)
        {
            Vector3 axes = ClusterAxes(box, half);
            int wanted = Mathf.RoundToInt(Mathf.Lerp(ClusterLow, ClusterHigh, Mathf.InverseLerp(ExtentLow, ExtentHigh, extent)));
            int seeded = Mathf.RoundToInt(wanted / (1f - 0.5f * ClusterHide));
            var cluster = new System.Collections.Generic.List<Bubble>(seeded);

            // The lattice's offset for this newborn, so no two clusters share their lumps.
            var shift = new Vector3(17f * TheatreBirthCloud.Hash(id, 0, 70), 17f * TheatreBirthCloud.Hash(id, 0, 71), 17f * TheatreBirthCloud.Hash(id, 0, 72));

            float k = 1f - SizePower;
            float lowK = Mathf.Pow(ClusterDiameterLow, k);
            float highK = Mathf.Pow(ClusterDiameterHigh, k);
            float start = -Mathf.Min(Mathf.Max(0f, early), ClusterLeadMost);

            for (int c = 0; c < seeded * ClusterTries && cluster.Count < seeded; c++)
            {
                Vector3 q = TheatreBirthCloud.Unit(TheatreBirthCloud.Hash(id, c, 76), TheatreBirthCloud.Hash(id, c, 77)) *
                            Mathf.Pow(TheatreBirthCloud.Hash(id, c, 78), 1f / 3f);
                if (!(TheatreBirthCloud.Hash(id, c, 79) < ClusterDensity(id, q, shift))) continue;

                int j = cluster.Count;
                float diameter = Mathf.Clamp(Mathf.Pow(Mathf.Lerp(lowK, highK, TheatreBirthCloud.Hash(id, j, 81)), 1f / k), ClusterDiameterLow, ClusterDiameterHigh);
                float size = Mathf.InverseLerp(DiameterLow, DiameterHigh, diameter);
                float lasting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LastingFrom, LastingFull, diameter));
                float wobble = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WobbleFrom, WobbleFull, diameter));

                float release = Mathf.Lerp(start, ClusterReleaseLast, TheatreBirthCloud.Hash(id, j, 87));
                float clear = Mathf.Lerp(ClusterClearLow, ClusterClearHigh, TheatreBirthCloud.Hash(id, j, 89));
                float life = Mathf.Lerp(ClusterEndLow, ClusterEndHigh, Mathf.Pow(TheatreBirthCloud.Hash(id, j, 88), EndPower));

                float rise = (Rise2 ? RiseOf(diameter) : Mathf.Lerp(RiseLow, RiseHigh, Mathf.Sqrt(size))) *
                             (1f + RiseJitter * (2f * TheatreBirthCloud.Hash(id, j, 82) - 1f));
                float hz = Mathf.Lerp(WobbleHzLow, WobbleHzHigh, TheatreBirthCloud.Hash(id, j, 83));

                cluster.Add(new Bubble
                {
                    Offset = Vector3.Scale(q, axes),
                    Radius = 0.5f * diameter,
                    Release = release,
                    End = Mathf.Min(life, clear - release),
                    Rise = rise,
                    WobbleMetres = WobbleShare * diameter * wobble,
                    Omega = 6.2831853f * hz,
                    Phase = 6.2831853f * TheatreBirthCloud.Hash(id, j, 84),
                    Heading = 6.2831853f * TheatreBirthCloud.Hash(id, j, 85),
                    Lasting = lasting,
                    Seed = TheatreBirthCloud.Hash(id, j, 86) * 97f,
                    Aspect = Oblate ? AspectOf(diameter) : 0f,
                    InCluster = true,
                    Toward = TheatreBirthCloud.Hash(id, j, 80),
                    GrowIn = ClusterGrow,
                });
            }

            var all = new Bubble[first.Length + cluster.Count];
            System.Array.Copy(first, all, first.Length);
            cluster.CopyTo(all, first.Length);
            return all;
        }

        /// <summary>The cluster's semi-axes, metres: <see cref="ClusterReach"/> times the box's half-sides, none under <see cref="ClusterDepth"/> of the largest; a ball where the box is not usable.</summary>
        private static Vector3 ClusterAxes(Vector3 box, float half)
        {
            bool usable = box.x > 0f && box.y > 0f && box.z > 0f && !float.IsInfinity(box.x + box.y + box.z) && !float.IsNaN(box.x + box.y + box.z);
            if (!usable) return new Vector3(ClusterReach * half, ClusterReach * half, ClusterReach * half);

            Vector3 axes = 0.5f * ClusterReach * box;
            float least = ClusterDepth * Mathf.Max(axes.x, Mathf.Max(axes.y, axes.z));
            return new Vector3(Mathf.Max(least, axes.x), Mathf.Max(least, axes.y), Mathf.Max(least, axes.z));
        }

        /// <summary>
        /// The chance a cluster seed at <paramref name="q"/> (in the unit ball) is kept: two octaves of value noise against the
        /// threshold, times a falloff toward the edge whose radius a third noise moves, so the cluster has lumps, gaps and no clean outline.
        /// </summary>
        private static float ClusterDensity(long id, Vector3 q, Vector3 shift)
        {
            float lumps = ClusterNoiseWeight * ValueNoise(id, 73, q * ClusterNoiseFreq + shift) +
                          (1f - ClusterNoiseWeight) * ValueNoise(id, 74, q * ClusterNoiseFreq2 + shift);
            float ragged = ClusterEdgeWobble * (2f * ValueNoise(id, 75, q * ClusterEdgeFreq + shift) - 1f);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClusterEdgeFrom, 1f, q.magnitude + ragged));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClusterNoiseLow, ClusterNoiseHigh, lumps)) * edge;
        }

        /// <summary>Value noise in [0, 1): the newborn's hashed lattice at this salt, blended smoothly between its corners.</summary>
        private static float ValueNoise(long id, int salt, Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x);
            int iy = Mathf.FloorToInt(p.y);
            int iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix;
            float fy = p.y - iy;
            float fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float x00 = Mathf.Lerp(Lattice(id, salt, ix, iy, iz), Lattice(id, salt, ix + 1, iy, iz), fx);
            float x10 = Mathf.Lerp(Lattice(id, salt, ix, iy + 1, iz), Lattice(id, salt, ix + 1, iy + 1, iz), fx);
            float x01 = Mathf.Lerp(Lattice(id, salt, ix, iy, iz + 1), Lattice(id, salt, ix + 1, iy, iz + 1), fx);
            float x11 = Mathf.Lerp(Lattice(id, salt, ix, iy + 1, iz + 1), Lattice(id, salt, ix + 1, iy + 1, iz + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        private static float Lattice(long id, int salt, int x, int y, int z)
        {
            unchecked
            {
                return TheatreBirthCloud.Hash(id, x * 73856093 ^ y * 19349663 ^ z * 83492791, salt);
            }
        }

        /// <summary>
        /// The first time a cloud with a cluster is drawn (the start of its lead, or its first frame), the camera's side is fixed for the
        /// film: of the bubbles seeded behind the plane through the centre facing the eye, those whose hashed draw is under
        /// <see cref="ClusterHide"/> are dropped for good (their end set to 0). None of them has grown in yet, so nothing vanishes on
        /// screen. Every cluster bubble's draw is then set to -1, the mark that this is done.
        /// </summary>
        private static void LatchCluster(Bubble[] bubbles, Vector3 centre, Vector3 eye)
        {
            Vector3 toEye = eye - centre;
            bool known = toEye.sqrMagnitude > 1e-12f;

            for (int i = 0; i < bubbles.Length; i++)
            {
                if (!bubbles[i].InCluster) continue;
                if (known && bubbles[i].Toward < ClusterHide && Vector3.Dot(bubbles[i].Offset, toEye) < 0f) bubbles[i].End = 0f;
                bubbles[i].Toward = -1f;
            }
        }

        /// <summary>
        /// The semi-axes, metres, of the ellipsoid inscribed in the newborn's box (its half-sides, none under a cover bubble's
        /// radius); a ball of the largest half-extent where the box is not usable.
        /// </summary>
        private static Vector3 CoverAxes(Vector3 box, float half)
        {
            bool usable = box.x > 0f && box.y > 0f && box.z > 0f && !float.IsInfinity(box.x + box.y + box.z) && !float.IsNaN(box.x + box.y + box.z);
            if (!usable) return new Vector3(half, half, half);

            float least = 0.5f * CoverDiameterLow;
            return new Vector3(Mathf.Max(least, 0.5f * box.x), Mathf.Max(least, 0.5f * box.y), Mathf.Max(least, 0.5f * box.z));
        }

        /// <summary>
        /// The cover's count for an ellipsoid of these semi-axes (metres): enough 3-8 mm discs on each of its two broad faces to sum
        /// to <see cref="CoverFill"/> times its largest silhouette (pi times its two longest semi-axes, the body seen face on), held
        /// between <see cref="CoverLeast"/> and <see cref="CoverMost"/>.
        /// </summary>
        private static int CoverCount(Vector3 axes)
        {
            float longest = Mathf.Max(axes.x, Mathf.Max(axes.y, axes.z));
            float shortest = Mathf.Min(axes.x, Mathf.Min(axes.y, axes.z));
            float middle = axes.x + axes.y + axes.z - longest - shortest;

            float meanSquare = (CoverDiameterLow * CoverDiameterLow + CoverDiameterLow * CoverDiameterHigh + CoverDiameterHigh * CoverDiameterHigh) / 3f;
            float disc = 0.25f * Mathf.PI * meanSquare;
            float silhouette = Mathf.PI * longest * middle;
            float wanted = 2f * CoverFill * silhouette / Mathf.Max(1e-12f, disc);
            if (!(wanted > CoverLeast)) return CoverLeast;
            return wanted < CoverMost ? Mathf.RoundToInt(wanted) : CoverMost;
        }

        /// <summary>
        /// The cover's bubbles, written from <paramref name="from"/> on, a function of (id, index) at salts 51-64: on and inside the
        /// ellipsoid of these semi-axes about the newborn's centre, spread evenly as seen along its shortest axis and on both its broad
        /// faces (on a leaf, a flat lens over each face that follows its outline), shown from just before the birth (gathering over the
        /// lead, where there is one), held still, then released from the bottom to the top between <see cref="CoverHoldLow"/> and
        /// <see cref="CoverHoldHigh"/> of the run, each rising and wobbling as any bubble of its size and thinning as it goes.
        /// </summary>
        private static void MakeCover(long id, Bubble[] bubbles, int from, Vector3 axes, float early)
        {
            // The shortest axis is the one the faces are seen along; the other two span them.
            int thin = axes.x <= axes.y && axes.x <= axes.z ? 0 : axes.y <= axes.z ? 1 : 2;
            int wideA = thin == 0 ? 1 : 0;
            int wideB = thin == 2 ? 1 : 2;

            for (int j = 0; from + j < bubbles.Length; j++)
            {
                float diameter = Mathf.Lerp(CoverDiameterLow, CoverDiameterHigh, TheatreBirthCloud.Hash(id, j, 51));
                float size = Mathf.InverseLerp(DiameterLow, DiameterHigh, diameter);
                float lasting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LastingFrom, LastingFull, diameter));
                float wobble = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WobbleFrom, WobbleFull, diameter));

                // Even over the face's disc, lifted onto the ellipsoid on a hashed side, scaled onto its shell or inside it; a bubble on
                // the shell stands off the face by its own radius, so its centre is not inside the body.
                float spread = Mathf.Sqrt(TheatreBirthCloud.Hash(id, j, 52));
                float turn = 6.2831853f * TheatreBirthCloud.Hash(id, j, 53);
                float u = spread * Mathf.Cos(turn);
                float v = spread * Mathf.Sin(turn);
                float w = Mathf.Sqrt(Mathf.Max(0f, 1f - spread * spread));
                float side = TheatreBirthCloud.Hash(id, j, 64) < 0.5f ? -1f : 1f;

                float depth = TheatreBirthCloud.Hash(id, j, 55);
                bool deep = TheatreBirthCloud.Hash(id, j, 54) < CoverDeepShare;
                float scale = deep ? Mathf.Lerp(CoverDeepLow, CoverShellLow, Mathf.Pow(depth, 1f / 3f)) : Mathf.Lerp(CoverShellLow, CoverShellHigh, depth);

                Vector3 offset = Vector3.zero;
                offset[wideA] = u * axes[wideA] * scale;
                offset[wideB] = v * axes[wideB] * scale;
                offset[thin] = side * (w * axes[thin] * scale + (deep ? 0f : 0.5f * diameter));

                // Released from the bottom of the cover to its top; shown from just before the birth, or over the lead's last stretch.
                float height = Mathf.Clamp01(0.5f + 0.5f * offset.y / (CoverShellHigh * axes.y + CoverDiameterHigh));
                float release = Mathf.Lerp(CoverHoldLow, CoverHoldHigh, height) + CoverHoldJitter * (2f * TheatreBirthCloud.Hash(id, j, 56) - 1f);
                float appear = -GrowSeconds - (early > 0f ? Mathf.Min(early, CoverGather) * TheatreBirthCloud.Hash(id, j, 57) : 0f);

                float rise = (Rise2 ? RiseOf(diameter) : Mathf.Lerp(RiseLow, RiseHigh, Mathf.Sqrt(size))) *
                             (1f + RiseJitter * (2f * TheatreBirthCloud.Hash(id, j, 59) - 1f));
                float hz = Mathf.Lerp(WobbleHzLow, WobbleHzHigh, TheatreBirthCloud.Hash(id, j, 60));

                bubbles[from + j] = new Bubble
                {
                    Offset = offset,
                    Radius = 0.5f * diameter,
                    Release = release,
                    End = Mathf.Lerp(CoverEndLow, CoverEndHigh, Mathf.Pow(TheatreBirthCloud.Hash(id, j, 58), EndPower)),
                    Rise = rise,
                    WobbleMetres = WobbleShare * diameter * wobble,
                    Omega = 6.2831853f * hz,
                    Phase = 6.2831853f * TheatreBirthCloud.Hash(id, j, 61),
                    Heading = 6.2831853f * TheatreBirthCloud.Hash(id, j, 62),
                    Lasting = lasting,
                    Seed = TheatreBirthCloud.Hash(id, j, 63) * 97f,
                    Hold = release - appear,
                    Aspect = Oblate ? AspectOf(diameter) : 0f,
                };
            }
        }

        /// <summary>The oblate shape, wide over tall, of a bubble of this diameter (metres): 1 under <see cref="OblateFrom"/>, then the correlation of <see cref="OblateGain"/>.</summary>
        private static float AspectOf(float diameter)
        {
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OblateFrom, OblateFull, diameter));
            if (!(weight > 0f)) return 1f;
            float eotvos = EotvosPerSquareMetre * diameter * diameter;
            return 1f + weight * OblateGain * Mathf.Pow(eotvos, OblatePower);
        }

        /// <summary>The rise with <see cref="Rise2"/> on, m/s of the run, of a bubble of this diameter (metres): see <see cref="RiseFar"/>.</summary>
        private static float RiseOf(float diameter)
        {
            float square = diameter * diameter;
            return RiseFar * square / (square + RiseKnee * RiseKnee);
        }

        /// <summary>
        /// Writes one cloud's bubbles into the cloud's mesh at <paramref name="age"/>, the run's seconds since the cloud's start
        /// (negative while it forms). A bubble's place is the ball's centre, its offset, the current at the birth place times the age,
        /// its rise and its wobble. Returns the bubbles written. <paramref name="eye"/> is the camera's place, read only with
        /// <see cref="Cluster"/> on, at the cloud's first draw.
        /// </summary>
        internal static int Draw(long id, Bubble[] bubbles, Vector3 centre, Vector3 current, float age, Vector3 eye)
        {
            int drawn = 0;
            int covered = 0;
            int oblong = 0;
            int clustered = 0;
            Vector3 drift = centre + current * age;

            // COVER = 2: the camera's side is fixed at the cloud's first draw (the cluster is appended last, so its last bubble tells).
            if (Cluster && bubbles.Length > 0 && bubbles[bubbles.Length - 1].InCluster && bubbles[bubbles.Length - 1].Toward >= 0f)
                LatchCluster(bubbles, centre, eye);

            for (int i = 0; i < bubbles.Length; i++)
            {
                Bubble b = bubbles[i];

                // Its place, size and fade at this age, by the one set of laws the other bubble effects reuse (Place).
                if (!Place(in b, age, drift, out Placed at)) continue;

                Vector3 place = at.Place;
                float radius = at.Radius;
                float fade = at.Fade;
                float risen = at.Risen;
                float swing = at.Swing;
                float cos = at.Cos;
                float sin = at.Sin;

                if (!TheatreBirthCloud.InFrustum(place, Margin + radius)) continue;

                if (b.Hold > 0f) covered++;
                if (b.InCluster) clustered++;

                if (b.Aspect > 1f)
                {
                    // Oblate: uv1.w the shape, wide over tall, breathing with the wobble; the colour's r and g the tilt of its short
                    // axis from the vertical (x and z per metre up), toward the way the zigzag moves it.
                    float sway = swing / (WobbleShare * 2f * b.Radius);
                    float wave = b.Omega * risen + b.Phase;
                    float aspect = Mathf.Max(1.001f, b.Aspect * (1f + OblateBreath * sway * Mathf.Sin(2f * wave)));
                    float tilt = Mathf.Tan(OblateTiltDegrees * Mathf.Deg2Rad) * sway;
                    float tiltAlong = tilt * Mathf.Cos(wave);
                    float tiltAcross = -WobbleAcross * tilt * Mathf.Sin(wave);
                    var lean = new Color(tiltAlong * cos - tiltAcross * sin, tiltAlong * sin + tiltAcross * cos, 0f, 1f);

                    TheatreBirthCloud.AddQuad(place, new Vector4(radius, fade, b.Seed, aspect), lean);
                    oblong++;
                    drawn++;
                    continue;
                }

                // uv1: the radius, the fade, the seed, 0 (a sphere); the colour is unused by the shader.
                TheatreBirthCloud.AddQuad(place, new Vector4(radius, fade, b.Seed, 0f), Color.white);
                drawn++;
            }

            if (drawn > 0 && !_told) Tell(id, bubbles.Length);
            if (covered > 0 && !_toldCover) TellCover(id, bubbles);
            if (clustered > 0 && !_toldCluster) TellCluster(id, bubbles);
            if (oblong > 0 && !_toldOblate) TellOblate(id);
            if (drawn > 0 && Rise2 && !_toldRise2) TellRise2(id);
            return drawn;
        }

        // ---------------------------------------------------------------- the laws, for the other bubble effects

        /// <summary>The run's seconds after its release by which every bubble is gone, at most (a lasting bubble's longest end): the bite bubbles and the pearls size their lives by it.</summary>
        internal const float EndMostSeconds = EndLargeHigh;

        /// <summary>
        /// One bubble's place, size and fade at <paramref name="age"/>, the run's seconds since its cloud's start (the bubble's own
        /// <see cref="Bubble.Release"/> counted from there), with <paramref name="drift"/> the ball's centre carried to this age. The
        /// rise from rest, the wobble growing in, the dissolving of a small bubble and the swell and fade of a lasting one, as
        /// <see cref="Draw"/> has always taken them: this is its body, moved here so the bite bubbles and the oxygen pearls take the
        /// same laws from the same lines. False when the bubble is not yet shown, is gone, or has no size left.
        /// </summary>
        internal static bool Place(in Bubble b, float age, Vector3 drift, out Placed placed)
        {
            placed = default;

            // A cover bubble (Hold > 0) shows for its hold before its release, held still; for every other bubble Hold is 0 and
            // risen is s, so each line below is the first take's.
            float s = age - b.Release;
            if (!(s > -b.Hold) || s >= b.End) return false;
            float risen = s > 0f ? s : 0f;

            // Dissolving or fading over its last stretch, and grown in over its first.
            float left = Mathf.Clamp01((b.End - s) / Span);
            float keep = left * left * (3f - 2f * left);
            float grow = Mathf.Clamp01((s + b.Hold) / (b.GrowIn > 0f ? b.GrowIn : GrowSeconds));
            grow = grow * grow * (3f - 2f * grow);

            // A small one shrinks to nothing; a large one swells a little and fades instead.
            float swell = 1f + Swell * Mathf.Clamp01(risen / EndLargeHigh);
            float radius = b.Radius * grow * Mathf.Lerp(keep, swell, b.Lasting);
            float fade = Mathf.Lerp(1f, keep, b.Lasting);
            if (!(radius > 0f) || !(fade > 0f)) return false;

            // The rise from rest, and the wobble growing in: a zigzag in its own plane, with a little across it.
            float lift = b.Rise * (risen - RiseTau * (1f - Mathf.Exp(-risen / RiseTau)));
            float swing = b.WobbleMetres * (1f - Mathf.Exp(-risen / WobbleSettle));
            float along = swing * Mathf.Sin(b.Omega * risen + b.Phase);
            float across = WobbleAcross * swing * Mathf.Cos(b.Omega * risen + b.Phase);
            float cos = Mathf.Cos(b.Heading);
            float sin = Mathf.Sin(b.Heading);

            Vector3 place = drift + b.Offset;
            place.x += along * cos - across * sin;
            place.y += lift;
            place.z += along * sin + across * cos;

            placed = new Placed { Place = place, Radius = radius, Fade = fade, Risen = risen, Swing = swing, Cos = cos, Sin = sin };
            return true;
        }

        /// <summary>What <see cref="Place"/> works out for a bubble at an age.</summary>
        internal struct Placed
        {
            /// <summary>The bubble's centre in the world.</summary>
            public Vector3 Place;

            public float Radius;

            /// <summary>The opacity share left, 1 to 0 (a dissolving bubble keeps 1 and shrinks instead).</summary>
            public float Fade;

            /// <summary>The run's seconds risen since its release; 0 while it is held.</summary>
            public float Risen;

            /// <summary>The wobble's present half-swing in metres, and the cosine and sine of its plane's heading.</summary>
            public float Swing;

            public float Cos;
            public float Sin;
        }

        /// <summary>
        /// A diameter on the birth bubbles' power law (density as d^-<see cref="SizePower"/>) between two bounds, from a uniform number
        /// in [0, 1): most small, a few large, as <see cref="Make"/> draws its own.
        /// </summary>
        internal static float PowerDiameter(float u, float low, float high)
        {
            float k = 1f - SizePower;
            float diameter = Mathf.Pow(Mathf.Lerp(Mathf.Pow(low, k), Mathf.Pow(high, k), u), 1f / k);
            return Mathf.Clamp(diameter, low, high);
        }

        /// <summary>A dissolving bubble's life after its release, run seconds, from a uniform number: <see cref="EndSmallLow"/> to <see cref="EndSmallHigh"/> with most early, as <see cref="Make"/> takes it.</summary>
        internal static float DissolveEnd(float u) => Mathf.Lerp(EndSmallLow, EndSmallHigh, Mathf.Pow(u, EndPower));

        /// <summary>
        /// One free bubble by the first take's laws, for the bite bubbles and the oxygen pearls: its size sets how far it lasts rather
        /// than dissolves, how far it wobbles and how fast it rises, exactly as in <see cref="Make"/>, whose loop is left as it was so
        /// the birth bubbles' frames cannot move. Every <c>h</c> is a hashed number in [0, 1) the caller draws with its own salts.
        /// </summary>
        /// <param name="diameter">Metres.</param>
        /// <param name="offset">From the caller's ball centre, at its release.</param>
        /// <param name="release">The run's second of its release, counted from the age the caller passes to <see cref="Place"/>.</param>
        /// <param name="hold">Run seconds it is shown, held still, before its release (a pearl growing attached); 0 for none.</param>
        /// <param name="growIn">Run seconds it grows in from nothing from its first shown moment; 0 for <see cref="GrowSeconds"/>.</param>
        internal static Bubble Single(float diameter, Vector3 offset, float release, float hold, float growIn,
            float hEndSmall, float hEndLarge, float hRise, float hHz, float hPhase, float hHeading, float hSeed)
        {
            float size = Mathf.InverseLerp(DiameterLow, DiameterHigh, diameter);
            float lasting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LastingFrom, LastingFull, diameter));
            float wobble = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WobbleFrom, WobbleFull, diameter));
            float endSmall = DissolveEnd(hEndSmall);
            float endLarge = Mathf.Lerp(EndLargeLow, EndLargeHigh, hEndLarge);
            float rise = (Rise2 ? RiseOf(diameter) : Mathf.Lerp(RiseLow, RiseHigh, Mathf.Sqrt(size))) * (1f + RiseJitter * (2f * hRise - 1f));
            float hz = Mathf.Lerp(WobbleHzLow, WobbleHzHigh, hHz);

            return new Bubble
            {
                Offset = offset,
                Radius = 0.5f * diameter,
                Release = release,
                End = Mathf.Lerp(endSmall, endLarge, lasting),
                Rise = rise,
                WobbleMetres = WobbleShare * diameter * wobble,
                Omega = 6.2831853f * hz,
                Phase = 6.2831853f * hPhase,
                Heading = 6.2831853f * hHeading,
                Lasting = lasting,
                Seed = hSeed * 97f,
                Hold = hold,
                GrowIn = growIn,
            };
        }

        /// <summary>The bubbles' material, or null (said once by the caller's flag) when the shader cannot be found.</summary>
        internal static Material MakeMaterial()
        {
            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogWarning("[Theatre] birth bubbles: shader '" + ShaderName + "' not found, so no cloud is drawn");
                return null;
            }

            var material = new Material(shader) { name = "Theatre Birth Bubbles", hideFlags = HideFlags.HideAndDontSave };

            // The second take's shader path only where a second-take dial asks for it: without the keyword the compiled variant is the
            // first take's, so the dials off are pixel-identical (RISE2 needs no shader change).
            if (Oblate || CoverMode > 0) material.EnableKeyword(Take2Keyword);
            return material;
        }

        /// <summary>The one log line, at the first frame a bubble is drawn: the dial took effect.</summary>
        private static void Tell(long id, int count)
        {
            _told = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] birth bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES): drawn in place of the motes; first: {0}, {1} bubbles of {2:0}-{3:0} mm, " +
                "rising {4:0.##}-{5:0.##} m/s, gone by {6:0.##} s of the run",
                id, count, 1000f * DiameterLow, 1000f * DiameterHigh, RiseLow, RiseHigh, LastWorldSeconds));
        }

        /// <summary>The cover's one log line, at the first frame a cover bubble is drawn.</summary>
        private static void TellCover(long id, Bubble[] bubbles)
        {
            _toldCover = true;
            int count = 0;
            float reach = 0f;

            for (int i = 0; i < bubbles.Length; i++)
            {
                if (!(bubbles[i].Hold > 0f)) continue;
                count++;
                reach = Mathf.Max(reach, bubbles[i].Offset.magnitude);
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] birth bubbles cover (EVOSIM_THEATRE_BIRTH_BUBBLES_COVER): a held foam over the newborn; first: {0}, {1} bubbles of " +
                "{2:0}-{3:0} mm on the ellipsoid in its box, reaching {4:0.###} m, released from its bottom to its top over {5:0.##}-{6:0.##} s of the run",
                id, count, 1000f * CoverDiameterLow, 1000f * CoverDiameterHigh, reach, CoverHoldLow, CoverHoldHigh));
        }

        /// <summary>COVER = 2's one log line, at the first frame a cluster bubble is drawn: the count kept, the region it reaches, and the release window.</summary>
        private static void TellCluster(long id, Bubble[] bubbles)
        {
            _toldCluster = true;
            int kept = 0;
            int seeded = 0;
            Vector3 reach = Vector3.zero;
            float first = float.MaxValue;
            float last = float.MinValue;
            float clear = 0f;

            for (int i = 0; i < bubbles.Length; i++)
            {
                Bubble b = bubbles[i];
                if (!b.InCluster) continue;
                seeded++;
                if (!(b.End > 0f)) continue;
                kept++;
                reach = Vector3.Max(reach, new Vector3(Mathf.Abs(b.Offset.x), Mathf.Abs(b.Offset.y), Mathf.Abs(b.Offset.z)));
                first = Mathf.Min(first, b.Release);
                last = Mathf.Max(last, b.Release);
                clear = Mathf.Max(clear, b.Release + b.End);
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] birth bubbles cover 2 (EVOSIM_THEATRE_BIRTH_BUBBLES_COVER=2): a rising cluster in volume about the newborn; first: {0}, " +
                "{1} bubbles of {2:0}-{3:0} mm kept of {4} seeded (the far half thinned by {5:0}%), in a lumpy ellipsoid of {6:0.##}x the box reaching " +
                "{7:0.###}, {8:0.###}, {9:0.###} m in x, y, z, released over {10:0.##} to {11:0.##} s of the run about the birth, clear by {12:0.##} s",
                id, kept, 1000f * ClusterDiameterLow, 1000f * ClusterDiameterHigh, seeded, 100f * ClusterHide, ClusterReach,
                reach.x, reach.y, reach.z, first, last, clear));
        }

        /// <summary>The oblate shape's one log line, at the first frame an oblate bubble is drawn.</summary>
        private static void TellOblate(long id)
        {
            _toldOblate = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] birth bubbles oblate (EVOSIM_THEATRE_BIRTH_BUBBLES_OBLATE): oblate from {0:0.#} mm, wide over tall {1:0.##} at 4 mm " +
                "and {2:0.##} at 10 mm, tilting {3:0} degrees and breathing {4:0}% with the wobble, diameters capped at {5:0} mm; first: {6}",
                1000f * OblateFrom, AspectOf(0.004f), AspectOf(0.01f), OblateTiltDegrees, 100f * OblateBreath, 1000f * OblateMost, id));
        }

        /// <summary>The second rise's one log line, at the first frame a bubble is drawn with it.</summary>
        private static void TellRise2(long id)
        {
            _toldRise2 = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] birth bubbles rise (EVOSIM_THEATRE_BIRTH_BUBBLES_RISE2): contaminated water's rise, {0:0.###} m/s at 2 mm, " +
                "{1:0.###} at 5 mm, {2:0.###} at 20 mm; first: {3}",
                RiseOf(0.002f), RiseOf(0.005f), RiseOf(0.02f), id));
        }
    }
}
