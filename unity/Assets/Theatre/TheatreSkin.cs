using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's skin: dark field lighting, the water's fog, the sea bed, the sea's own
    /// surface and the shafts of light under it, the marine snow, and the two materials every
    /// body is drawn with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why dark field.</b> research/theatre-look/README.md's "Lighting, before any material":
    /// plankton photographers block the direct light so that the background is black and only what
    /// scatters at an edge reaches the lens [DF, WU, PC, AS]. The rendered equivalent is a near
    /// black water, low ambient, a raking key and a weak fill from the opposite side, and it costs
    /// nothing per body while doing more than any material. So the lighting is set up first and
    /// the shaders are written to suit it, not the other way round.
    /// </para>
    /// <para>
    /// <b>Everything here is generated.</b> No mesh, texture, material or shader is a committed
    /// binary. The bed and the surface are grids built in code, the sand, the wave and the
    /// caustics are arithmetic in the shader, the shafts are a handful of quads laid along the
    /// sun's refracted ray, the snow is a particle system configured in code with a procedural
    /// mote, and the meshes come from <see cref="TheatreMeshes"/>. That is the note's constraint
    /// (no purchased assets, nothing fetched at run time) and this repository's rule about what
    /// can be reviewed in a diff.
    /// </para>
    /// <para>
    /// <b>None of it changes a hash.</b> <c>simHash</c> is a digest of every <c>.cs</c> under
    /// <c>Assets/Evosim</c>; this file and its shaders live under <c>Assets/Theatre</c>, so a
    /// change to the look cannot refuse a recording or report a new build to the farm. That
    /// separation is the reason the theatre sits outside the simulation tree at all
    /// (<see cref="BuildIdentity"/>).
    /// </para>
    /// <para>
    /// <b>It applies to the Play mode view and to the snapshot camera alike.</b> The lights are
    /// scene objects and the fog is a render setting, so a picture is a picture of what the owner
    /// would see. <see cref="SnapshotCamera"/> rescales the fog to the span it is looking through
    /// for the length of one render, and says why there.
    /// </para>
    /// </remarks>
    public sealed class TheatreSkin
    {
        // ---------------------------------------------------------------- the water

        /// <summary>Near black blue: the camera's background, the fog's colour, the deep water.</summary>
        public Color Water = new Color(0.012f, 0.032f, 0.048f);

        /// <summary>
        /// Fog density for the Play mode view, as URP's exponential squared fog reads it.
        /// </summary>
        /// <remarks>
        /// At 0.035 the fog leaves about six tenths of a body's light at twenty metres and about a
        /// hundredth at sixty, which is the campaign box read the long way: the far wall of a 20 m
        /// box is dim but present, and the bottom of a 60 m column is the dark it should be.
        /// </remarks>
        public float FogDensity = 0.035f;

        /// <summary>Flat ambient. Low, because the dark field is the whole look.</summary>
        public Color Ambient = new Color(0.020f, 0.038f, 0.046f);

        /// <summary>
        /// The lit water at the waterline (2026-09-16, the look's design pass, R1). The water is
        /// this at the surface and falls by e-folds of <see cref="WaterReachMetres"/> to
        /// <see cref="Water"/>; every surface's fog mixes towards the column's colour at the
        /// height it is seen through, the backdrop paints the same column, and the ambient is
        /// graded from it (sky) to the deep (ground), so a body is lit a little from above the
        /// way a thing in water is. <c>EVOSIM_THEATRE_SHALLOW</c> scales it (0 to 3, default 1)
        /// and <c>EVOSIM_THEATRE_LIT_WATER=0</c> turns the whole of it off, which is the flat
        /// field the theatre had before.
        /// </summary>
        // An sRGB value like every other colour here. The first pictures (2026-09-16) went up
        // unconverted and turned the whole box one bright teal; at this value, converted, the
        // field is still dark and the top of the water is lit only by comparison with the deep.
        public Color Shallow = new Color(0.09f, 0.26f, 0.32f);
        public float ShallowGain = Dial("EVOSIM_THEATRE_SHALLOW", 1f, 0f, 3f);
        public bool LitWater = Dial("EVOSIM_THEATRE_LIT_WATER", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Metres over which the lit water falls to the deep. 0, the default, takes
        /// <see cref="SurfaceLightMetres"/>, so the water darkens where the caustics and the
        /// shafts give out. <c>EVOSIM_THEATRE_WATER_REACH</c>.
        /// </summary>
        public float WaterReachMetres = Dial("EVOSIM_THEATRE_WATER_REACH", 0f, 0f, 200f);

        /// <summary>
        /// The key's and the fill's strength. 3.0 and 0.8 from the linear colour space
        /// (2026-09-16): the same numbers that lit a body's face to 0.6 in gamma space light it
        /// to 0.15 in linear, and the dark field wants the body bright against the dark, not
        /// dark in it. <c>EVOSIM_THEATRE_KEY</c>, <c>EVOSIM_THEATRE_FILL</c>.
        /// </summary>
        public float KeyIntensity = Dial("EVOSIM_THEATRE_KEY", 3.0f, 0f, 12f);
        public float FillIntensity = Dial("EVOSIM_THEATRE_FILL", 0.8f, 0f, 12f);

        /// <summary>The lit water's colour as pushed: the shallow scaled, or the deep with it off.</summary>
        public Color ShallowAsLit => LitWater ? Shallow * ShallowGain : Water;

        // ---------------------------------------------------------------- the carve

        /// <summary>
        /// How deep the bodies are carved, as a fraction of a part's smallest half extent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The second day's one dial. The first day rounded the boxes and mottled them and the
        /// owner read it as still far too geometric: what makes a thing
        /// look grown is its silhouette, and rounding leaves every face flat and parallel. So the
        /// body shader cuts inward by two octaves of noise in each part's own object space, and
        /// this is how far.
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> The displacement is never positive, so the
        /// setting cannot put a vertex outside a collider at any value; the shader's own
        /// <c>_CarveMaximum</c> is the separate question of not cutting a body through its middle.
        /// </para>
        /// <para>
        /// Set from <c>EVOSIM_THEATRE_CARVE</c> so that three pictures at three depths can be
        /// taken from one build and the owner can choose from pictures rather than from a number.
        /// The default is 0.35 rather than the second day's 0.2, because that is the one of the
        /// three the owner picked from the close views on 2026-09-11
        /// (logbook/specs/skin-spec-3.md).
        /// </para>
        /// </remarks>
        public float CarveFraction = Dial("EVOSIM_THEATRE_CARVE", 0.35f, 0f, 0.5f);

        // ---------------------------------------------------------------- the box that grew

        /// <summary>
        /// How far a box part narrows towards one end, as a fraction of its cross-section.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The third day's reading of the same complaint the second day answered for the surface.
        /// The owner looked at the close views and said the spheres were right and the boxes still
        /// looked manufactured: six flat faces at right angles, and no amount of surface relief
        /// argues with a silhouette that is a rectangle. A grown thing is almost never the same
        /// width at both ends, so the box narrows down its longest axis, smoothly rather than as a
        /// wedge, and reads as a seed or a grain (logbook/specs/skin-spec-3.md).
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> The cross-section is multiplied by a number in
        /// [1 - this, 1], so every vertex moves towards the part's own long axis and none of them
        /// can reach the collider, let alone leave it.
        /// </para>
        /// </remarks>
        public float TaperFraction = Dial("EVOSIM_THEATRE_TAPER", 0.35f, 0f, 0.8f);

        /// <summary>
        /// How far a box part bends across itself, as a fraction of its smallest half extent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One low-frequency half wave down the longest axis, in a lateral direction drawn from
        /// the same per-body number the carve and the mottle are drawn from, so a body's parts are
        /// bent by its own id and not by the frame: it cannot flicker, and two creatures are bent
        /// differently (logbook/specs/skin-spec-3.md).
        /// </para>
        /// <para>
        /// <b>It can only make a body smaller.</b> A bend inside a box has to be paid for: the
        /// shader shrinks the cross-section by the amplitude before it displaces by it, and then
        /// clamps the object position into the mesh's own box anyway, so inside-ness is a
        /// property of the arithmetic and not of the setting (<c>TheatreBody.shader</c>,
        /// <c>ShapeBox</c>). Boxes only; a sphere or a capsule is not bent at all, because its
        /// collider is not the box the clamp would hold it inside.
        /// </para>
        /// </remarks>
        public float BendFraction = Dial("EVOSIM_THEATRE_BEND", 0.15f, 0f, 0.4f);

        /// <summary>
        /// How far a leaf may curl, as a fraction of its width (<c>EVOSIM_THEATRE_CURL</c>, 0 to
        /// 0.1, default 0.1).
        /// </summary>
        /// <remarks>
        /// The one dial here that draws a body outside its collider, and it is the owner's ruling
        /// (2026-09-24): a flat box a centimetre or two thick has no room inside it to curve, and
        /// a flat leaf is what made the crowd read as cut card. The curl is fixed for a body's
        /// life (drawn from the carve's seed), it is hidden under the raw shapes, and it is
        /// bounded here at a tenth of the leaf's width whatever the environment says.
        /// </remarks>
        public float CurlFraction = Dial("EVOSIM_THEATRE_CURL", 0.1f, 0f, 0.1f);

        /// <summary>How strongly a leaf's veins are drawn (<c>EVOSIM_THEATRE_VEINS</c>, 0 to 1).</summary>
        public float VeinStrength = Dial("EVOSIM_THEATRE_VEINS", 0.6f, 0f, 1f);

        /// <summary>
        /// How strongly a blade glows with the light from the surface coming through it when seen
        /// from below (<c>EVOSIM_THEATRE_LEAF_GLOW</c>, 0 to 2).
        /// </summary>
        public float LeafSkyGlow = Dial("EVOSIM_THEATRE_LEAF_GLOW", 0.6f, 0f, 2f);

        /// <summary>
        /// Whether the earth under a shaped bed is drawn at the glass (<c>EVOSIM_THEATRE_EARTH</c>,
        /// 1 on and 0 off). On by default: the ground is earth. Off shows the water under the
        /// beach as every picture before 2026-09-24 did, for a census view that wants to see
        /// through the shoal to the bodies behind it.
        /// </summary>
        public bool Earth = Dial("EVOSIM_THEATRE_EARTH", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Points the key and the fill from wherever the viewer now looks, keeping the offsets
        /// <see cref="Apply"/> chose. Until 2026-09-16 the two were placed once from the fly
        /// camera's starting rotation and never moved, so five of the six snapshot views and
        /// every flight around a body were lit by the fill (the look's design pass,
        /// <c>logbook/specs/striking-theatre-menu.md</c>). The runner calls this every frame
        /// with the fly camera; the snapshot camera calls it around each render and puts the
        /// previous bearing back, so two pictures of one second are lit the same way.
        /// </summary>
        /// <returns>The bearing the lights had, for putting back.</returns>
        public Quaternion Aim(Quaternion behind)
        {
            Quaternion was = _key != null ? _key.transform.rotation * Quaternion.Inverse(KeyOffset) : Quaternion.identity;
            if (_key != null) _key.transform.rotation = behind * KeyOffset;
            if (_fill != null) _fill.transform.rotation = behind * FillOffset;
            return was;
        }

        // ---------------------------------------------------------------- the sea above

        /// <summary>
        /// How far the surface rises above its own level at the crest of a wave, in metres.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The fourth day's first dial. The owner ran round 36 seed 1 in the theatre and said
        /// they could not see the sun, the water shimmer or the underwater ripple. None of the
        /// three were there: the first three days were about bodies, and the world's ceiling was
        /// the same empty background as everything outside the box
        /// (logbook/specs/skin-spec-4.md). With the wavelength below, this is the whole shape of
        /// the sea the theatre is now lit by.
        /// </para>
        /// <para>
        /// A few centimetres, which with a wavelength of a few metres is a calm day rather than
        /// surf. The steepness of the two together is what decides whether the ceiling reads as
        /// water or as crumpled foil, and it is the ratio rather than either number. The first
        /// picture, at 0.045 m over 1.6 m, read as bands of black and white; the second, at half
        /// the height over twice the length, read as water, and that is the default (the agent's
        /// pick from the two pictures on 2026-09-11 evening, logbook/0091).
        /// </para>
        /// <para>
        /// <b>It is a visual and moves nothing.</b> No body is pushed by this wave, no current
        /// changes and no hash moves: the theatre is outside <c>simHash</c> by construction, and
        /// a sea drawn on the ceiling is a picture of the waterline the world already has.
        /// </para>
        /// </remarks>
        public float SurfaceWaveMetres = Dial("EVOSIM_THEATRE_WAVE", 0.022f, 0f, 0.5f);

        /// <summary>The longest wave train's wavelength, in metres.</summary>
        /// <remarks>
        /// Two shorter trains ride on it at about a half and a quarter of it
        /// (<c>TheatreWater.hlsl</c>, <c>EvoRipple</c>), so this is the scale of the largest
        /// thing on the surface and not the only one. The default is a metre and a half, which
        /// puts a dozen crests across the campaign box's five metre width: enough for the window
        /// to break up, few enough that the pattern is not below what a picture can hold.
        /// </remarks>
        public float SurfaceWaveLengthMetres = Dial("EVOSIM_THEATRE_WAVELENGTH", 3.2f, 0.2f, 20f);

        /// <summary>How much of their true phase speed the wave trains run at.</summary>
        /// <remarks>
        /// The trains travel by deep water dispersion, so the long one outruns the short ones and
        /// the sea never repeats on a beat. This halves the lot, and it is a dial rather than a
        /// constant for an honest reason: nobody has watched this sea yet. At the true speed a
        /// metre and a half of wavelength beats about once a second, which may well read as rain
        /// on a ceiling rather than as a calm day, and the owner can put it back to 1 from a
        /// picture without a rebuild.
        /// </remarks>
        public float SurfaceWaveSpeed = Dial("EVOSIM_THEATRE_WAVE_SPEED", 0.5f, 0f, 4f);

        /// <summary>How far below the surface its light still reaches, in metres.</summary>
        /// <remarks>
        /// <para>
        /// One number for the shafts, the caustics on the bodies, the caustics on the sand and
        /// the brightness of the window seen from far down, so the lit part of the world is one
        /// depth rather than four. The default is the habitable band: the world is 60 m deep and
        /// the top twenty is where the light and the bodies are, below which the water is the
        /// dark field it already was.
        /// </para>
        /// <para>
        /// It is a look and not the light model. <c>LightModel.IrradianceAt</c> is what the world
        /// actually charges and pays by; this decides where a picture stops drawing beams.
        /// </para>
        /// </remarks>
        public float SurfaceLightMetres = Dial("EVOSIM_THEATRE_LIGHT_REACH", 18f, 1f, 60f);

        /// <summary>How many shafts of light come down from the surface.</summary>
        /// <remarks>
        /// A handful of additive quads and not a volumetric pass: URP has no volumetric fog, so
        /// the choice is a custom renderer feature that marches the light or cards that stand in
        /// for beams, and the note's own furniture list takes the second
        /// (research/theatre-look, [CQ] [CY3]). Nine is enough for a box twenty metres long to
        /// have light falling through it and few enough that the census underneath is still the
        /// thing being looked at. Zero turns them off, which is how a picture is taken with and
        /// without them.
        /// </remarks>
        public int Shafts = Mathf.RoundToInt(Dial("EVOSIM_THEATRE_SHAFTS", 9f, 0f, 32f));

        /// <summary>Reads one of the skin's dials from the environment, or its default.</summary>
        /// <remarks>
        /// Parsed invariantly and clamped rather than trusted. A machine whose decimal separator
        /// is a comma would read 0.35 as 35 and carve every body away to nothing, and a refusal
        /// here would take the viewer down for a cosmetic setting, which WaterBounds' rule
        /// forbids. One method for all of them, because a second copy of this parse is a second
        /// place for the clamp to be forgotten; <see cref="TheatreMeshes"/> reads its pillow
        /// through it too.
        /// </remarks>
        internal static float Dial(string name, float fallback, float low, float high)
        {
            string text = System.Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            if (!float.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float value))
            {
                Debug.LogWarning(
                    "[Theatre] " + name + " is not a number ('" + text + "'); using " +
                    fallback.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

                return fallback;
            }

            return Mathf.Clamp(value, low, high);
        }

        /// <summary>
        /// The marine producers' bend with the current (<c>EVOSIM_THEATRE_ALGA_BEND</c>, 0 off by default), read once:
        /// how much of the room the sway leaves a blade it may lean into, 0 to 1, and from 1 to 4 how far past that room it
        /// leans, by a share of its length (<c>TheatreBody.shader</c>, <c>AlgaBendReach</c>). Zero unless the marine producers'
        /// dial (<c>EVOSIM_THEATRE_ALGA</c>) and the sway dial (<c>EVOSIM_THEATRE_SWAY</c>) are on too, which the
        /// palette reads the same way. Zero means nothing is pushed, set or read for it anywhere.
        /// </summary>
        public static readonly float AlgaBendStrength =
            Dial("EVOSIM_THEATRE_ALGA", 0f, 0f, 1f) > 0f && Dial("EVOSIM_THEATRE_SWAY", 0f, 0f, 1f) > 0f
                ? Dial("EVOSIM_THEATRE_ALGA_BEND", 0f, 0f, 4f)
                : 0f;

        /// <summary>
        /// The water's edge from below after the dive's take A (<c>EVOSIM_THEATRE_WATER_EDGE</c>, 0 off by default, 1
        /// full), read once: fine ripples on the ceiling's normal that cut Snell's window into cells, a small bright sun
        /// they break into sparks, and a mirror lifted toward the lit water (<c>TheatreSurface.shader</c>). It overshoots
        /// a real sea on purpose (D262) and is dialled back from there. Zero means nothing is pushed, set or read for it.
        /// </summary>
        public static readonly float WaterEdge = Dial("EVOSIM_THEATRE_WATER_EDGE", 0f, 0f, 1f);

        /// <summary>
        /// The window's light through the water above the eye (<c>EVOSIM_THEATRE_WATER_WINDOW</c>, 0 off by default, 1
        /// full), read once: the sky seen through Snell's window loses its red over the metres between the surface and the
        /// eye, at the dive's take A's absorption, so a window seen from a metre or two is blue-cyan rather than grey
        /// (<c>TheatreSurface.shader</c>, WindowKept). Only the part of the path the look's own fade leaves uncounted. Its
        /// own dial because the safari draws the same window. Zero means nothing is pushed, set or read for it.
        /// </summary>
        public static readonly float WaterWindow = Dial("EVOSIM_THEATRE_WATER_WINDOW", 0f, 0f, 1f);

        /// <summary>
        /// The sun's glow in the water above the eye (<c>EVOSIM_THEATRE_WATER_GLOW</c>, 0 off by default, 1 the first cut's
        /// strength, up to 4), read once: a soft warm halo round the sun's place in Snell's window, from the light the water
        /// scatters forward, taken on the eye's own ray so the ripples do not break it up, and laid on the window's facets
        /// alone so the mirror's patches stay dark (<c>TheatreSurface.shader</c>, EdgeGlow). The dive's take A has it and the
        /// theatre's window did not. Zero means nothing is pushed, set or read for it.
        /// </summary>
        public static readonly float WaterGlow = Dial("EVOSIM_THEATRE_WATER_GLOW", 0f, 0f, 4f);

        /// <summary>
        /// The eyespot's core filled with the mark's colour rather than black (<c>EVOSIM_THEATRE_FAMILY_SPOT_SOFT</c>, 0 off by
        /// default, to 1), read once: the pattern family's eyespot (<c>TheatreBody.shader</c>, FamilyEyeShare) is then a ring and a
        /// spot of one colour, a mark rather than an iris round a pupil. Its size is the shader's rule (0.35 of the body's
        /// thickness) and has no dial. Zero means nothing is pushed, set or read for it.
        /// </summary>
        public static readonly float FamilySpotSoft = Dial("EVOSIM_THEATRE_FAMILY_SPOT_SOFT", 0f, 0f, 1f);

        /// <summary>
        /// A probe of the water edge's window (<c>EVOSIM_THEATRE_WATER_EDGE_PROBE</c>, 0 off by default): 1 draws the sky
        /// without its clouds, 2 without its clouds and without the split of the colours at the rim, to find what draws the
        /// tan blotches near the window's rim; 3 draws the sun's facets with none of the soft sun under them, to see whether
        /// the shards carry the sun alone; 4 draws the finer facets (the 3 cm ripples at their full slope and 1.5 cm ones
        /// besides), apart from the sun's facets. A diagnostic, never a look. Zero means nothing is pushed, set or read for it.
        /// </summary>
        public static readonly float WaterEdgeProbe = Dial("EVOSIM_THEATRE_WATER_EDGE_PROBE", 0f, 0f, 4f);

        /// <summary>
        /// The hit cone of the sun's facets under the water edge, in degrees (<c>EVOSIM_THEATRE_WATER_FACET</c>, 0 by default for
        /// the shader's own EDGE_FACET_HIT, up to 5), read once: a pixel shows the sun where its refracted ray comes within
        /// this of it, so a wider cone draws larger shards (<c>TheatreSurface.shader</c>, EdgeFacets). Zero means nothing is
        /// pushed, set or read for it.
        /// </summary>
        public static readonly float WaterFacet = Dial("EVOSIM_THEATRE_WATER_FACET", 0f, 0f, 5f);

        /// <summary>
        /// The surface's keyword for the water edge's fifth cut and those after it (the sun's facets, the glow on the window
        /// alone, the rim's clouds, the finer facets), enabled once by <see cref="PushLook"/> when <see cref="WaterEdge"/>, <see cref="WaterWindow"/> or
        /// <see cref="WaterGlow"/> is on. As plain branches the cut moved 28 pixels of the dials-off check by one level (phase144),
        /// the compiler's doing; behind the keyword the surface without it compiles the text from before the cut.
        /// </summary>
        public const string WaterEdge5Keyword = "_EVO_WATER_EDGE5";
        private static bool _waterEdge5KeywordOn;

        // skin2:begin
        /// <summary>
        /// The one skin's second look (<c>EVOSIM_THEATRE_SKIN2</c>, 0 off by default; 1 on), read once: a diffuse sheen, light
        /// through the tissue, asymmetry, a pattern by clade and a margin band on the one skin
        /// (<c>TheatreBody.shader</c>, <c>Skin2*</c>, <c>PatternSkin2</c>), and a pattern regime by clade on every part
        /// (<c>TheatrePattern.StyleOf</c>). Off means the shader keyword below is never enabled, <c>_PatternShift</c>'s z and w
        /// are never set, the skin mesh's leaf channel stays zero and nothing else in the theatre reads it.
        /// </summary>
        public static readonly bool Skin2On = Dial("EVOSIM_THEATRE_SKIN2", 0f, 0f, 2f) >= 0.5f;

        /// <summary>The shader keyword SKIN2's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Skin2Keyword = "_EVO_SKIN2";

        private static bool _skin2KeywordOn;

        // SHEEN begin
        /// <summary>
        /// The sheen cap (<c>EVOSIM_THEATRE_SHEEN_CAP</c>, 0 off by default; 1 on), read once, and on only with SKIN2 on: the
        /// white SKIN2's sheen adds on the one skin and on a part is rolled off toward a share of the luminance of the body's
        /// own key light at the pixel, so a surface facing the light keeps its colour under a soft highlight
        /// (<c>TheatreBody.shader</c>, <c>SheenCapped</c>, <c>SheenCapShare</c>). Off means the shader keyword below is never
        /// enabled and nothing else in the theatre reads it.
        /// </summary>
        public static readonly bool SheenCapOn = Dial("EVOSIM_THEATRE_SHEEN_CAP", 0f, 0f, 1f) >= 0.5f && Skin2On;

        /// <summary>The shader keyword the sheen cap's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string SheenCapKeyword = "_EVO_SHEEN_CAP";
        // SHEEN end
        // SHEEN2 begin
        /// <summary>
        /// The sheen cap's second value (<c>EVOSIM_THEATRE_SHEEN_CAP</c> at 2; the dial runs 0 to 2), read once. With SKIN2 on it
        /// is value 1 (<see cref="SheenCapOn"/>). With SKIN2 off, at 2, the same roll-off is applied to the white the base
        /// tissue's own specular adds on the one skin and on a creature's part (<c>TheatreBody.shader</c>,
        /// <c>SheenCappedBase</c>, <c>SheenCapBaseShare</c>). Off means the shader keyword below is never enabled and nothing
        /// else in the theatre reads it.
        /// </summary>
        public static readonly float SheenCapLevel = Dial("EVOSIM_THEATRE_SHEEN_CAP", 0f, 0f, 2f);

        /// <summary>Whether the base tissue's specular is capped: the dial at 2 and SKIN2 off.</summary>
        public static readonly bool SheenCapBaseOn = SheenCapLevel >= 1.5f && !Skin2On;

        /// <summary>The shader keyword the base cap's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string SheenCapBaseKeyword = "_EVO_SHEEN_CAP2";

        private static bool _sheenCapBaseKeywordOn;

        /// <summary>
        /// Enables <see cref="SheenCapBaseKeyword"/> once, for every material that has it, when <see cref="SheenCapBaseOn"/>;
        /// does nothing otherwise. Called when the look is pushed and when a one skin is built, and nothing turns it off.
        /// </summary>
        public static void EnsureSheenCapBaseKeyword()
        {
            if (!SheenCapBaseOn || _sheenCapBaseKeywordOn) return;

            Shader.EnableKeyword(SheenCapBaseKeyword);
            _sheenCapBaseKeywordOn = true;
            Debug.Log("[Theatre] sheen cap 2 on: the white of the base tissue's specular rolls off toward a share of the body's own " +
                      "key light, SKIN2 off (EVOSIM_THEATRE_SHEEN_CAP=2, shader keyword " + SheenCapBaseKeyword + ").");
        }
        // SHEEN2 end
        // skin2b:begin
        /// <summary>
        /// SKIN2 at level 2 (<c>EVOSIM_THEATRE_SKIN2</c> at 1.5 or more, read once): the margin band is laid at the body's
        /// outline seen across its broad face, in place of level 1's (<c>TheatreBody.shader</c>, <c>Skin2MarginB</c>), and the
        /// one skin's rim mark carries a hinge fade (<c>TheatreOneSkin.Skin2BOutline</c>). Level 1 is unchanged;
        /// this adds the shader keyword below, enabled by <see cref="EnsureSkin2Keyword"/> beside SKIN2's.
        /// </summary>
        public static readonly bool Skin2BOn = Dial("EVOSIM_THEATRE_SKIN2", 0f, 0f, 2f) >= 1.5f;

        /// <summary>The shader keyword SKIN2 level 2's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Skin2BKeyword = "_EVO_SKIN2B";

        // skin2band:begin
        /// <summary>
        /// The margin band's boldness at SKIN2 level 2 (<c>EVOSIM_THEATRE_SKIN2_BAND</c>, 1 to 4, default 1), read once: the band's
        /// width and opacity are multiplied by it (<c>TheatreBody.shader</c>, <c>Skin2MarginB</c>), the width capped at
        /// <c>Skin2BBandWidthCap</c> of the local half width up to 2, rising to <c>Skin2BBandWidthCap3</c> (half) at 3, and the
        /// opacity at 1. At 1 the shader's output is the level without the
        /// dial, to the bit. It means nothing unless <see cref="Skin2BOn"/>; <see cref="EnsureSkin2Keyword"/> writes it beside the keyword.
        /// </summary>
        public static readonly float Skin2BBand = Dial("EVOSIM_THEATRE_SKIN2_BAND", 1f, 1f, 4f);
        // skin2band:end
        // BAND4 begin
        /// <summary>
        /// The band dial at 4 (<c>EVOSIM_THEATRE_SKIN2_BAND=4</c> beside SKIN2 at 2), read once: the one skin's band adds the
        /// part ends, the crease line and a thin silhouette rim to level 3's (<c>TheatreBody.shader</c>, <c>Skin2B4*</c>), and the
        /// skin mesh carries the part-end mark in its UV0 (<c>TheatreOneSkin.Skin2BEnds</c>). Below 4 the keyword below is never
        /// enabled and the skin mesh has no UV0.
        /// </summary>
        public static readonly bool Skin2BandFour = Skin2BOn && Skin2BBand >= 3.5f;

        /// <summary>The shader keyword SKIN2_BAND 4's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Skin2BandFourKeyword = "_EVO_SKIN2_BAND4";
        // BAND4 end

        // skin2b:end
        /// <summary>
        /// Enables <see cref="Skin2Keyword"/> once, for every material that has it, when the dial is on; does nothing when it
        /// is off. Called when the look is pushed, when a one skin is built and when the palette paints under the dial, and
        /// nothing turns it off.
        /// </summary>
        public static void EnsureSkin2Keyword()
        {
            if (!Skin2On || _skin2KeywordOn) return;

            Shader.EnableKeyword(Skin2Keyword);
            _skin2KeywordOn = true;
            // SHEEN begin
            if (SheenCapOn)
            {
                Shader.EnableKeyword(SheenCapKeyword);
                Debug.Log("[Theatre] sheen cap on: the white of SKIN2's sheen rolls off toward a share of the body's own key light " +
                          "(EVOSIM_THEATRE_SHEEN_CAP, shader keyword " + SheenCapKeyword + ").");
            }
            // SHEEN end
            // skin2b:begin
            if (Skin2BOn)
            {
                Shader.EnableKeyword(Skin2BKeyword);
                Debug.Log("[Theatre] skin 2 level 2 on: the margin band at the body's outline, across its broad face (shader keyword " +
                          Skin2BKeyword + ").");
                // skin2band:begin
                // Set whenever the level is on, so a value left in the Editor's shader globals by an earlier run is overwritten.
                Shader.SetGlobalFloat("_EvoSkin2BBand", Skin2BBand);
                if (Skin2BBand > 1f)
                {
                    Debug.Log("[Theatre] skin 2 band: the margin band's width and opacity times " +
                              Skin2BBand.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                              " (EVOSIM_THEATRE_SKIN2_BAND), the width capped at a quarter of the local half width up to 2 and at a half at 3, the opacity at 1.");
                }
                // skin2band:end
                // BAND4 begin
                if (Skin2BandFour)
                {
                    Shader.EnableKeyword(Skin2BandFourKeyword);
                    Debug.Log("[Theatre] skin 2 band 4: the band adds the part ends, the crease line and a thin silhouette rim to " +
                              "level 3's (shader keyword " + Skin2BandFourKeyword + ").");
                }
                // BAND4 end
            }
            // skin2b:end
            Debug.Log("[Theatre] skin 2 on: a diffuse sheen, light through the tissue, asymmetry, a pattern by clade and a " +
                      "margin band on the one skin (shader keyword " + Skin2Keyword + ").");
        }

        // skin2:end
        /// <summary>
        /// The algal blade (<c>EVOSIM_THEATRE_ALGA_BLADE</c>, 0 off by default), read once: a free-drifting blade (the frond) is
        /// drawn as one or a few broad blades that fill most of its part (<c>TheatreBody.shader</c>, <c>AlgaBlade*</c>), in place
        /// of the stem with leaflets. On only with the marine producers' dial (<c>EVOSIM_THEATRE_ALGA</c>) on too, which the
        /// palette reads the same way. Off means the shader keyword below is never enabled and nothing else in the theatre reads it.
        /// </summary>
        public static readonly bool AlgaBladeOn =
            Dial("EVOSIM_THEATRE_ALGA", 0f, 0f, 1f) > 0f && Dial("EVOSIM_THEATRE_ALGA_BLADE", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The shader keyword the blade's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string AlgaBladeKeyword = "_EVO_ALGA_BLADE";

        private static bool _algaBladeKeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaBladeKeyword"/> once, for every material that has it, when the dial is on; does nothing when
        /// it is off. Called when the look is pushed and the first time the palette paints a frond, and nothing turns it off.
        /// </summary>
        public static void EnsureAlgaBladeKeyword()
        {
            if (!AlgaBladeOn || _algaBladeKeywordOn) return;

            Shader.EnableKeyword(AlgaBladeKeyword);
            _algaBladeKeywordOn = true;
            Debug.Log("[Theatre] algal blade on: a free-drifting blade is drawn as broad blades (shader keyword " +
                      AlgaBladeKeyword + ").");
        }

        /// <summary>
        /// Level 2 of the algal blade (<c>EVOSIM_THEATRE_ALGA_BLADE</c> at 2), read once: each frond takes a colour along a
        /// ramp from sea lettuce to kelp, the light through the blade when the key is behind it, and a finer ruffle along the
        /// margin, all shading in <c>TheatreBody.shader</c> (<c>AlgaBlade*</c>, under the keyword below as well as
        /// <see cref="AlgaBladeKeyword"/>). On only with the marine producers' dial on, as level 1 is. At 0 and 1 the keyword is
        /// never enabled, and the shader compiles the text it did before.
        /// </summary>
        public static readonly bool AlgaBlade2On =
            Dial("EVOSIM_THEATRE_ALGA", 0f, 0f, 1f) > 0f && Dial("EVOSIM_THEATRE_ALGA_BLADE", 0f, 0f, 3f) >= 1.5f;

        /// <summary>The second shader keyword the blade's level 2 is compiled under.</summary>
        public const string AlgaBlade2Keyword = "_EVO_ALGA_BLADE2";

        private static bool _algaBlade2KeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaBlade2Keyword"/> once, for every material that has it, when level 2 is on; does nothing at
        /// any other level. Called where <see cref="EnsureAlgaBladeKeyword"/> is.
        /// </summary>
        public static void EnsureAlgaBlade2Keyword()
        {
            if (!AlgaBlade2On || _algaBlade2KeywordOn) return;

            Shader.EnableKeyword(AlgaBlade2Keyword);
            _algaBlade2KeywordOn = true;
            Debug.Log("[Theatre] algal blade level 2 on: a colour range, light through the blade and a finer margin ruffle " +
                      "(shader keyword " + AlgaBlade2Keyword + ").");
        }

        // BLADE3 begin
        /// <summary>
        /// Level 3 of the algal blade (<c>EVOSIM_THEATRE_ALGA_BLADE</c> at 3), read once: level 2, and light through the thin
        /// blade and a ruffled margin (scallops cut inward, a wave out of the plane within the box's room), in
        /// <c>TheatreBody.shader</c> (<c>AlgaBlade3*</c>, under the keyword below as well as <see cref="AlgaBladeKeyword"/>). On
        /// only with the marine producers' dial on, as levels 1 and 2 are. At 2 and below the keyword is never enabled, and the
        /// shader compiles the text it did before.
        /// </summary>
        public static readonly bool AlgaBlade3On =
            Dial("EVOSIM_THEATRE_ALGA", 0f, 0f, 1f) > 0f && Dial("EVOSIM_THEATRE_ALGA_BLADE", 0f, 0f, 3f) >= 2.5f;

        /// <summary>The third shader keyword the blade's level 3 is compiled under.</summary>
        public const string AlgaBlade3Keyword = "_EVO_ALGA_BLADE3";

        private static bool _algaBlade3KeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaBlade3Keyword"/> once, for every material that has it, when level 3 is on; does nothing at
        /// any other level. Called where <see cref="EnsureAlgaBladeKeyword"/> is.
        /// </summary>
        public static void EnsureAlgaBlade3Keyword()
        {
            if (!AlgaBlade3On || _algaBlade3KeywordOn) return;

            Shader.EnableKeyword(AlgaBlade3Keyword);
            _algaBlade3KeywordOn = true;
            Debug.Log("[Theatre] algal blade level 3 on: light through the thin blade and a ruffled margin " +
                      "(shader keyword " + AlgaBlade3Keyword + ").");
        }

        // BLADE3 end
        // RAMPPHOTO begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_ALGA_RAMP_PHOTO</c> (0 to 1, default 0), read once: how far level 2's colour ramp (the sea-lettuce and
        /// mid-green stops of <c>AlgaBladeColour</c> in <c>TheatreBody.shader</c>) is moved toward photographed sea lettuce, in
        /// linear colour. Zero unless level 2 of the algal blade is on (<see cref="AlgaBlade2On"/>), as that level's code is. The
        /// value is the global <c>_EvoAlgaRampPhoto</c>; the shader's code sits under the keyword below.
        /// </summary>
        public static readonly float AlgaRampPhoto =
            AlgaBlade2On ? Dial("EVOSIM_THEATRE_ALGA_RAMP_PHOTO", 0f, 0f, 1f) : 0f;

        /// <summary>True when the photographed ramp's dial is above zero.</summary>
        public static readonly bool AlgaRampPhotoOn = AlgaRampPhoto > 0f;

        /// <summary>The shader keyword the photographed ramp's lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string AlgaRampPhotoKeyword = "_EVO_ALGA_RAMP_PHOTO";

        private static bool _algaRampPhotoKeywordOn;

        /// <summary>
        /// Sets the photographed ramp's global, and enables <see cref="AlgaRampPhotoKeyword"/> once, when the dial is above zero;
        /// does nothing when it is zero. Called where <see cref="EnsureAlgaBlade2Keyword"/> is.
        /// </summary>
        public static void EnsureAlgaRampPhoto()
        {
            if (!AlgaRampPhotoOn) return;

            Shader.SetGlobalFloat("_EvoAlgaRampPhoto", AlgaRampPhoto);
            if (_algaRampPhotoKeywordOn) return;

            Shader.EnableKeyword(AlgaRampPhotoKeyword);
            _algaRampPhotoKeywordOn = true;
            Debug.Log("[Theatre] algal blade ramp photo " + AlgaRampPhoto.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      ": the sea-lettuce stop moves toward linear (0.4185, 0.5134, 0.1481) and the mid-green stop toward (0.1682, 0.2063, 0.0595) " +
                      "(shader keyword " + AlgaRampPhotoKeyword + ").");
        }

        // RAMPPHOTO end
        // RAMPSTOPS begin
        /// <summary>The environment variable that replaces the sea-lettuce photo stop (<c>AlgaBladeRampPhotoA</c>).</summary>
        public const string AlgaRampStopAVariable = "EVOSIM_THEATRE_ALGA_RAMP_STOP_A";

        /// <summary>The environment variable that replaces the mid-green photo stop (<c>AlgaBladeRampPhotoB</c>).</summary>
        public const string AlgaRampStopBVariable = "EVOSIM_THEATRE_ALGA_RAMP_STOP_B";

        /// <summary>The environment variable that replaces <c>AlgaBladeRampShare</c> under the photographed ramp's keyword.</summary>
        public const string AlgaRampShareVariable = "EVOSIM_THEATRE_ALGA_RAMP_SHARE";

        /// <summary>
        /// The sea-lettuce photo stop from <see cref="AlgaRampStopAVariable"/>, read once and only while the photographed ramp
        /// is on: three numbers <c>r,g,b</c> in linear colour, each 0 to 1. <c>w</c> is 1 when it is set and valid, 0 when it is
        /// unset or refused, and the shader's constant then stands. The global is <c>_EvoAlgaRampStopA</c>.
        /// </summary>
        public static readonly Vector4 AlgaRampStopA = AlgaRampPhotoOn ? ReadAlgaRampStop(AlgaRampStopAVariable) : Vector4.zero;

        /// <summary>The mid-green photo stop, as <see cref="AlgaRampStopA"/> is; the global is <c>_EvoAlgaRampStopB</c>.</summary>
        public static readonly Vector4 AlgaRampStopB = AlgaRampPhotoOn ? ReadAlgaRampStop(AlgaRampStopBVariable) : Vector4.zero;

        /// <summary>
        /// The share of the way <c>AlgaBladeColour</c> takes the ramp's hue, from <see cref="AlgaRampShareVariable"/> (0 to 1),
        /// read once and only while the photographed ramp is on. <c>x</c> is the share, <c>w</c> is 1 when it is set and valid, 0
        /// when it is unset or refused, and <c>AlgaBladeRampShare</c> then stands. The global is <c>_EvoAlgaRampShare</c>.
        /// </summary>
        public static readonly Vector4 AlgaRampShare = AlgaRampPhotoOn ? ReadAlgaRampShare(AlgaRampShareVariable) : Vector4.zero;

        private static bool _algaRampStopsLogged;

        private static bool InUnitRange(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f && v <= 1f;

        // Unset or blank gives zero without a word; a value that is not three numbers in 0 to 1 is refused with a log line naming
        // the variable, and gives zero too, so the constant stands.
        private static Vector4 ReadAlgaRampStop(string name)
        {
            string text = System.Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(text)) return Vector4.zero;

            string[] parts = text.Split(',');
            float[] v = new float[3];
            bool ok = parts.Length == 3;
            for (int i = 0; ok && i < 3; i++)
            {
                ok = float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v[i]) && InUnitRange(v[i]);
            }

            if (ok) return new Vector4(v[0], v[1], v[2], 1f);

            Debug.LogWarning("[Theatre] " + name + " is not three numbers r,g,b in linear colour, each 0 to 1 ('" + text +
                             "'); the shader's constant stands.");
            return Vector4.zero;
        }

        private static Vector4 ReadAlgaRampShare(string name)
        {
            string text = System.Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(text)) return Vector4.zero;

            if (float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) && InUnitRange(v))
                return new Vector4(v, 0f, 0f, 1f);

            Debug.LogWarning("[Theatre] " + name + " is not a number from 0 to 1 ('" + text + "'); the shader's constant stands.");
            return Vector4.zero;
        }

        private static string RampStopsText(Vector4 v) =>
            v.x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Sets the three globals when the photographed ramp is on, and logs once the values in force and which came from the
        /// environment; does nothing when the dial is zero. Called straight after <see cref="EnsureAlgaRampPhoto"/>.
        /// </summary>
        public static void EnsureAlgaRampStops()
        {
            if (!AlgaRampPhotoOn) return;

            Shader.SetGlobalVector("_EvoAlgaRampStopA", AlgaRampStopA);
            Shader.SetGlobalVector("_EvoAlgaRampStopB", AlgaRampStopB);
            Shader.SetGlobalVector("_EvoAlgaRampShare", AlgaRampShare);
            if (_algaRampStopsLogged) return;

            _algaRampStopsLogged = true;
            Debug.Log("[Theatre] algal blade ramp stops: sea-lettuce stop " +
                      (AlgaRampStopA.w > 0.5f ? "(" + RampStopsText(AlgaRampStopA) + ") from " + AlgaRampStopAVariable
                                              : "the shader's (0.4185,0.5134,0.1481)") +
                      "; mid-green stop " +
                      (AlgaRampStopB.w > 0.5f ? "(" + RampStopsText(AlgaRampStopB) + ") from " + AlgaRampStopBVariable
                                              : "the shader's (0.1682,0.2063,0.0595)") +
                      "; share " +
                      (AlgaRampShare.w > 0.5f
                          ? AlgaRampShare.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " from " + AlgaRampShareVariable
                          : "the shader's AlgaBladeRampShare") + ".");
        }

        // RAMPSTOPS end
        // ROUNDPHOTO begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_ALGA_ROUND_PHOTO</c> (0 to 1, default 0), read once: how far a round producer (a photosynthetic sphere,
        /// capsule or superellipsoid, which <c>TheatrePalette.DrawnAsLeaf</c> keeps off the blade path) is taken toward the colour a
        /// blade gets at the photographed ramp's mid-green stop. Zero unless the photographed ramp is on
        /// (<see cref="AlgaRampPhotoOn"/>), as the stops it reads are. The value is the global <c>_EvoAlgaRoundPhoto</c>; the
        /// shader's code sits under the keyword below.
        /// </summary>
        public static readonly float AlgaRoundPhoto =
            AlgaRampPhotoOn ? Dial("EVOSIM_THEATRE_ALGA_ROUND_PHOTO", 0f, 0f, 1f) : 0f;

        /// <summary>True when the round producers' photographed colour is above zero.</summary>
        public static readonly bool AlgaRoundPhotoOn = AlgaRoundPhoto > 0f;

        /// <summary>The shader keyword the round producers' lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string AlgaRoundPhotoKeyword = "_EVO_ALGA_ROUND_PHOTO";

        private static bool _algaRoundPhotoKeywordOn;

        /// <summary>
        /// Sets the round producers' global, and enables <see cref="AlgaRoundPhotoKeyword"/> once, when the dial is above zero;
        /// does nothing when it is zero. Called straight after <see cref="EnsureAlgaRampStops"/>.
        /// </summary>
        public static void EnsureAlgaRoundPhoto()
        {
            if (!AlgaRoundPhotoOn) return;

            Shader.SetGlobalFloat("_EvoAlgaRoundPhoto", AlgaRoundPhoto);
            if (_algaRoundPhotoKeywordOn) return;

            Shader.EnableKeyword(AlgaRoundPhotoKeyword);
            _algaRoundPhotoKeywordOn = true;
            Debug.Log("[Theatre] algal round producers photo " + AlgaRoundPhoto.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      ": a round part takes that share of the multiplier a blade gets at the mid-green stop, target " +
                      (AlgaRampStopB.w > 0.5f
                          ? "(" + RampStopsText(AlgaRampStopB) + ") from " + AlgaRampStopBVariable
                          : "the ramp's mid green (0.055,0.2429,0.0301) moved toward (0.1682,0.2063,0.0595) by the ramp photo " +
                            AlgaRampPhoto.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)) +
                      " (shader keyword " + AlgaRoundPhotoKeyword + ").");
        }

        // ROUNDPHOTO end
        // ROUNDTARGET begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_ALGA_ROUND_TARGET</c> (0 to 1, default 0), read once: how far a round producer (a photosynthetic
        /// sphere, capsule or superellipsoid, which <c>TheatrePalette.DrawnAsLeaf</c> keeps off the blade path) takes, on its whole
        /// light as a blade does, the colour multiplier a blade gets at the photographed ramp's mid-green stop. Zero unless the
        /// photographed ramp is on (<see cref="AlgaRampPhotoOn"/>), as the stops it reads are. It is independent of
        /// <c>EVOSIM_THEATRE_ALGA_ROUND_PHOTO</c>. The value is the global <c>_EvoAlgaRoundTarget</c>; the shader's code sits under
        /// the keyword below.
        /// </summary>
        public static readonly float AlgaRoundTarget =
            AlgaRampPhotoOn ? Dial("EVOSIM_THEATRE_ALGA_ROUND_TARGET", 0f, 0f, 1f) : 0f;

        /// <summary>True when the round producers' target colour is above zero.</summary>
        public static readonly bool AlgaRoundTargetOn = AlgaRoundTarget > 0f;

        /// <summary>The environment variable that gives the round producers' target its own stop, apart from the blades' mid-green stop.</summary>
        public const string AlgaRoundStopVariable = "EVOSIM_THEATRE_ALGA_ROUND_STOP";

        /// <summary>
        /// The round producers' own target stop from <see cref="AlgaRoundStopVariable"/>, read once and only while the round
        /// producers' target is on: three numbers <c>r,g,b</c> in linear colour, each 0 to 1, parsed and refused as the ramp's
        /// stops are. <c>w</c> is 1 when it is set and valid, 0 when it is unset or refused, and the target the blades' stops
        /// give (<see cref="AlgaRampStopB"/>, else the ramp's own) then stands. The global is <c>_EvoAlgaRoundStop</c>.
        /// </summary>
        public static readonly Vector4 AlgaRoundStop = AlgaRoundTargetOn ? ReadAlgaRampStop(AlgaRoundStopVariable) : Vector4.zero;

        /// <summary>The shader keyword the round producers' target lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string AlgaRoundTargetKeyword = "_EVO_ALGA_ROUND_TARGET";

        private static bool _algaRoundTargetKeywordOn;

        /// <summary>
        /// Sets the round producers' target global, and enables <see cref="AlgaRoundTargetKeyword"/> once, when the dial is above
        /// zero; does nothing when it is zero. Called after <see cref="EnsureAlgaRoundPhoto"/>.
        /// </summary>
        public static void EnsureAlgaRoundTarget()
        {
            if (!AlgaRoundTargetOn) return;

            Shader.SetGlobalFloat("_EvoAlgaRoundTarget", AlgaRoundTarget);
            Shader.SetGlobalVector("_EvoAlgaRoundStop", AlgaRoundStop);
            if (_algaRoundTargetKeywordOn) return;

            Shader.EnableKeyword(AlgaRoundTargetKeyword);
            _algaRoundTargetKeywordOn = true;
            Debug.Log("[Theatre] algal round producers target " + AlgaRoundTarget.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      ": a round part's whole light takes that share of the multiplier a blade gets at the mid-green stop, target " +
                      (AlgaRoundStop.w > 0.5f
                          ? "(" + RampStopsText(AlgaRoundStop) + ") from " + AlgaRoundStopVariable
                          : AlgaRampStopB.w > 0.5f
                              ? "(" + RampStopsText(AlgaRampStopB) + ") from " + AlgaRampStopBVariable
                              : "the ramp's mid green (0.055,0.2429,0.0301) moved toward (0.1682,0.2063,0.0595) by the ramp photo " +
                                AlgaRampPhoto.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)) +
                      " (shader keyword " + AlgaRoundTargetKeyword + ").");
        }

        // ROUNDTARGET end
        // THALLI begin
        /// <summary>
        /// The thalli (<c>EVOSIM_THEATRE_THALLI</c>, 0 off by default, 1 on), read once: a compact photosynthetic box, which the
        /// blade path draws as a flat lamina, is drawn as a seaweed thallus that fills its box (<c>TheatreMeshes.Thallus</c>):
        /// Ulva, Padina or Codium by its proportions. Off means no thallus mesh is built, the shader keyword below is never
        /// enabled, and nothing else in the theatre reads it.
        /// </summary>
        public static readonly bool ThalliOn = Dial("EVOSIM_THEATRE_THALLI", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The shader keyword the thallus's lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string ThalliKeyword = "_EVO_THALLI";

        private static bool _thalliKeywordOn;

        /// <summary>
        /// Enables <see cref="ThalliKeyword"/> once, for every material that has it, when the dial is on; does nothing when it is
        /// off. Called when the look is pushed and the first time the palette dresses a thallus, and nothing turns it off.
        /// </summary>
        public static void EnsureThalliKeyword()
        {
            if (!ThalliOn || _thalliKeywordOn) return;

            Shader.EnableKeyword(ThalliKeyword);
            _thalliKeywordOn = true;
            Debug.Log("[Theatre] thalli on: a compact producer is drawn as Ulva, Padina or Codium inside its box (shader keyword " +
                      ThalliKeyword + ").");
        }

        // THALLI end
        // ARC begin
        /// <summary>
        /// The blade's arc bend with the current (<c>EVOSIM_THEATRE_ALGA_ARC</c> at 1 and 2, 0 off by default), read once: 1 from
        /// a dial of 0.5, 2 from 1.5. Level 1 bends a broad blade as a rod bends, inside its collider box; level 2 is a
        /// trial beyond the collider, whose reach the palette logs per blade (<c>TheatreBody.shader</c>, <c>AlgaArc*</c>,
        /// under the keyword below as well as <see cref="AlgaBladeKeyword"/> and <see cref="AlgaBlade2Keyword"/>). Zero unless
        /// the blade is at level 2 or 3 and <see cref="AlgaBendStrength"/> is above zero, which also needs the marine producers'
        /// and the sway dials: the flow a blade bends with is written by that dial's plumbing. It is a dial of its own and
        /// changes nothing of <c>EVOSIM_THEATRE_ALGA_BEND</c> at any value. Zero means the keyword is never enabled and
        /// nothing is pushed.
        /// </summary>
        public static readonly int AlgaArcLevel = ReadAlgaArcLevel();

        private static int ReadAlgaArcLevel()
        {
            if (!(AlgaBendStrength > 0f) || !AlgaBlade2On) return 0;

            float dial = Dial("EVOSIM_THEATRE_ALGA_ARC", 0f, 0f, 2f);
            return dial >= 1.5f ? 2 : dial >= 0.5f ? 1 : 0;
        }

        // ARCROOM begin
        /// <summary>
        /// The share of a blade's arc its bend may reach beyond its box on the thin axis (<c>EVOSIM_THEATRE_ALGA_ARC_ROOM</c>, 0 to 0.3,
        /// 0 off by default), read once; a preview beyond D242. Zero unless the arc is on (<see cref="AlgaArcLevel"/> above zero), so
        /// it acts where the arc acts and nowhere else. It reaches the shader in <c>_EvoAlgaArc.y</c> and the renderer's bounds in
        /// <c>TheatreMeshes.LeafBounds</c>.
        /// </summary>
        public static readonly float AlgaArcRoom = AlgaArcLevel > 0 ? Dial("EVOSIM_THEATRE_ALGA_ARC_ROOM", 0f, 0f, 0.3f) : 0f;
        // ARCROOM end
        // ARCDRIVE begin
        /// <summary>
        /// A diagnostic view of what holds the blade arc down (<c>EVOSIM_THEATRE_ALGA_ARC_DRIVE</c>, 0 to 3, 0 off by default), read once,
        /// never for a film. Zero unless the arc is on (<see cref="AlgaArcLevel"/> above zero), so it acts where the arc acts and nowhere
        /// else. 1 bends every acting blade by each axis' full limit; 2 draws it in grey by the flow's component across its thin axis;
        /// 3 by the drive. It reaches the shader in <c>_EvoAlgaArcDrive</c> and the keyword <see cref="AlgaArcDriveKeyword"/>.
        /// </summary>
        public static readonly int AlgaArcDriveMode = AlgaArcLevel > 0 ? (int)(Dial("EVOSIM_THEATRE_ALGA_ARC_DRIVE", 0f, 0f, 3f) + 0.5f) : 0;

        /// <summary>The shader keyword the arc diagnostic in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string AlgaArcDriveKeyword = "_EVO_ALGA_ARC_DRIVE";

        private static bool _algaArcDriveKeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaArcDriveKeyword"/> once, and says once what the value draws, when the diagnostic is on; does nothing
        /// when it is off. Called from <see cref="EnsureAlgaArcKeyword"/>, so only when the arc is on.
        /// </summary>
        private static void EnsureAlgaArcDriveKeyword()
        {
            if (AlgaArcDriveMode == 0 || _algaArcDriveKeywordOn) return;

            Shader.EnableKeyword(AlgaArcDriveKeyword);
            _algaArcDriveKeywordOn = true;
            Debug.Log("[Theatre] arc drive diagnostic " + AlgaArcDriveMode + " on, not for a film (shader keyword " + AlgaArcDriveKeyword +
                      "): " + (AlgaArcDriveMode == 1
                          ? "every blade the arc acts on bends by each axis' full limit, the flow's component and the drive taken as one."
                          : AlgaArcDriveMode == 2
                              ? "every blade the arc acts on is grey by |flow . thin axis|, black none (0.004) and white all (1)."
                              : "every blade the arc acts on is grey by the drive (share to its power, times the wave, times the hold), " +
                                "black none (0.004) and white all (1).") +
                      " A blade the arc does not act on is drawn as usual.");
        }
        // ARCDRIVE end
        // FLUTTER begin
        /// <summary>
        /// The blade's flutter (<c>EVOSIM_THEATRE_ALGA_FLUTTER</c>, 0 to 1, 0 off by default), read once. Zero unless the arc is on
        /// (<see cref="AlgaArcLevel"/> above zero), so it acts where the arc acts and nowhere else. It reaches the shader in
        /// <c>_EvoAlgaArc.z</c> and the keyword <see cref="AlgaFlutterKeyword"/>.
        /// </summary>
        public static readonly float AlgaFlutter = AlgaArcLevel > 0 ? Dial("EVOSIM_THEATRE_ALGA_FLUTTER", 0f, 0f, 1f) : 0f;

        /// <summary>The shader keyword the flutter in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string AlgaFlutterKeyword = "_EVO_ALGA_FLUTTER";

        // The flutter wave's constants as the shader has them (AlgaFlutterHertz, AlgaFlutterWaves), here only for the log: change both together.
        private const float AlgaFlutterHertzLogged = 0.5f;
        private const float AlgaFlutterWavesLogged = 1f;

        private static bool _algaFlutterKeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaFlutterKeyword"/> once, and says once its value and the wave's constants, when the dial is on; does
        /// nothing when it is off. Called from <see cref="EnsureAlgaArcKeyword"/>, so only when the arc is on.
        /// </summary>
        private static void EnsureAlgaFlutterKeyword()
        {
            if (!(AlgaFlutter > 0f) || _algaFlutterKeywordOn) return;

            Shader.EnableKeyword(AlgaFlutterKeyword);
            _algaFlutterKeywordOn = true;
            Debug.Log("[Theatre] algal blade flutter " + AlgaFlutter.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      " on (shader keyword " + AlgaFlutterKeyword + "): a wave from the anchor to the free end across the blade's thin axis, " +
                      AlgaFlutterHertzLogged.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " Hz and " +
                      AlgaFlutterWavesLogged.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      " wave(s) along the arc, at most the thin axis' limit, by the current's speed.");
        }
        // FLUTTER end
        // FLUTTERNOISE begin
        /// <summary>
        /// The flutter's noise (<c>EVOSIM_THEATRE_ALGA_FLUTTER_NOISE</c>, 0 to 1, 0 off by default), read once. Zero unless the flutter is
        /// on (<see cref="AlgaFlutter"/> above zero). Each blade ripples at a frequency and an amplitude of its own, and a slow gust
        /// swells and slackens the ripple. It reaches the shader in <c>_EvoAlgaArc.w</c> and the keyword <see cref="AlgaFlutterNoiseKeyword"/>.
        /// </summary>
        public static readonly float AlgaFlutterNoise = AlgaFlutter > 0f ? Dial("EVOSIM_THEATRE_ALGA_FLUTTER_NOISE", 0f, 0f, 1f) : 0f;

        /// <summary>The shader keyword the flutter's noise in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string AlgaFlutterNoiseKeyword = "_EVO_ALGA_FLUTTER_NOISE";

        // The noise's constants as the shader has them (AlgaFlutterNoiseHertzLow and High, AlgaFlutterNoiseAmpLow and High, AlgaFlutterGustFloor and Ceiling,
        // AlgaFlutterGustHertzA, B and C, AlgaFlutterGustSpread), here only for the log: change both together.
        private const float AlgaFlutterNoiseHertzLowLogged = 0.3f;
        private const float AlgaFlutterNoiseHertzHighLogged = 0.7f;
        private const float AlgaFlutterNoiseAmpLowLogged = 0.7f;
        private const float AlgaFlutterNoiseAmpHighLogged = 1.3f;
        private const float AlgaFlutterGustFloorLogged = 0.4f;
        private const float AlgaFlutterGustCeilingLogged = 1.6f;
        private const float AlgaFlutterGustHertzALogged = 0.053f;
        private const float AlgaFlutterGustHertzBLogged = 0.089f;
        private const float AlgaFlutterGustHertzCLogged = 0.137f;
        private const float AlgaFlutterGustSpreadLogged = 60f;

        private static bool _algaFlutterNoiseKeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaFlutterNoiseKeyword"/> once, and says once its value and the constants, when the dial is on; does
        /// nothing when it is off. Called from <see cref="EnsureAlgaArcKeyword"/>, after the flutter's own, so only when the flutter is on.
        /// </summary>
        private static void EnsureAlgaFlutterNoiseKeyword()
        {
            if (!(AlgaFlutterNoise > 0f) || _algaFlutterNoiseKeywordOn) return;

            Shader.EnableKeyword(AlgaFlutterNoiseKeyword);
            _algaFlutterNoiseKeywordOn = true;
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log("[Theatre] algal blade flutter noise " + AlgaFlutterNoise.ToString("0.###", invariant) +
                      " on (shader keyword " + AlgaFlutterNoiseKeyword + "): each blade its own ripple, " +
                      AlgaFlutterNoiseHertzLowLogged.ToString("0.###", invariant) + " to " + AlgaFlutterNoiseHertzHighLogged.ToString("0.###", invariant) +
                      " Hz and an amplitude factor from " + AlgaFlutterNoiseAmpLowLogged.ToString("0.###", invariant) +
                      " to " + AlgaFlutterNoiseAmpHighLogged.ToString("0.###", invariant) + " and a phase of its own by the part's number, under a gust of three sines at " + AlgaFlutterGustHertzALogged.ToString("0.###", invariant) + ", " +
                      AlgaFlutterGustHertzBLogged.ToString("0.###", invariant) + " and " + AlgaFlutterGustHertzCLogged.ToString("0.###", invariant) +
                      " Hz from " + AlgaFlutterGustFloorLogged.ToString("0.###", invariant) + " to " + AlgaFlutterGustCeilingLogged.ToString("0.###", invariant) + " of the ripple, each blade up to " +
                      AlgaFlutterGustSpreadLogged.ToString("0.###", invariant) + " s ahead of the clock.");
        }
        // FLUTTERNOISE end
        /// <summary>The shader keyword the blade's arc bend in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string AlgaArcKeyword = "_EVO_ALGA_ARC";

        private static bool _algaArcKeywordOn;

        /// <summary>
        /// Enables <see cref="AlgaArcKeyword"/> once, for every material that has it, when the arc is on; does nothing when
        /// it is off. Called when the look is pushed and the first time the palette paints a frond.
        /// </summary>
        public static void EnsureAlgaArcKeyword()
        {
            if (AlgaArcLevel == 0 || _algaArcKeywordOn) return;

            Shader.EnableKeyword(AlgaArcKeyword);
            _algaArcKeywordOn = true;
            // ARCDRIVE begin
            EnsureAlgaArcDriveKeyword();
            // ARCDRIVE end
            // FLUTTER begin
            EnsureAlgaFlutterKeyword();
            // FLUTTER end
            // FLUTTERNOISE begin
            EnsureAlgaFlutterNoiseKeyword();
            // FLUTTERNOISE end
            Debug.Log("[Theatre] algal blade arc bend level " + AlgaArcLevel + " on" +
                      (AlgaArcLevel > 1 ? ", a trial beyond the collider" : ", inside the collider") +
                      " (shader keyword " + AlgaArcKeyword + ").");
        }

        /// <summary>
        /// The arc's switch (<c>_EvoAlgaArc</c>, TheatreBody.shader): x the level while the look is not held, else zero. Never
        /// called with the dial off, so the global stays the zero a shader global is.
        /// </summary>
        private static void PushAlgaArc(bool on)
        {
            Shader.SetGlobalVector("_EvoAlgaArc", new Vector4(on ? (float)AlgaArcLevel : 0f, 0f, 0f, 0f));
            // ARCROOM begin
            if (AlgaArcRoom > 0f) Shader.SetGlobalVector("_EvoAlgaArc", new Vector4(on ? (float)AlgaArcLevel : 0f, on ? AlgaArcRoom : 0f, 0f, 0f));
            // ARCROOM end
            // FLUTTER begin
            if (AlgaFlutter > 0f) Shader.SetGlobalVector("_EvoAlgaArc", new Vector4(on ? (float)AlgaArcLevel : 0f, on ? AlgaArcRoom : 0f, on ? AlgaFlutter : 0f, 0f));
            // FLUTTER end
            // FLUTTERNOISE begin
            if (AlgaFlutterNoise > 0f) Shader.SetGlobalVector("_EvoAlgaArc", new Vector4(on ? (float)AlgaArcLevel : 0f, on ? AlgaArcRoom : 0f, on ? AlgaFlutter : 0f, on ? AlgaFlutterNoise : 0f));
            // FLUTTERNOISE end
            // ARCDRIVE begin
            if (AlgaArcDriveMode > 0) Shader.SetGlobalFloat("_EvoAlgaArcDrive", on ? (float)AlgaArcDriveMode : 0f);
            // ARCDRIVE end
        }

        // ARC end
        // LOOKSTAGE begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_LOOK_STAGE</c> (0 to 3, default 0), read once: a diagnostic, never a look and never a film. Above 0 a
        /// producer's pixel (a blade, or under <c>EVOSIM_THEATRE_THALLI</c> a thallus) is returned early from the body shader's
        /// forward fragment, opaque and in linear, with its colour at one stage: 1 the body as it enters the lighting, 2 the diffuse
        /// light alone, 3 everything before the water (the fog not yet mixed). Every other pixel draws as usual. The value is the
        /// global <c>_EvoLookStage</c>, the shader's code sits under the keyword below. Off, the keyword is never enabled and the
        /// global never set.
        /// </summary>
        public static readonly int LookStage = Mathf.RoundToInt(Dial("EVOSIM_THEATRE_LOOK_STAGE", 0f, 0f, 3f));

        /// <summary>True when the look stage's dial is at 1, 2 or 3.</summary>
        public static readonly bool LookStageOn = LookStage >= 1;

        /// <summary>The shader keyword the look stage's lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string LookStageKeyword = "_EVO_LOOK_STAGE";

        private static bool _lookStageKeywordOn;

        /// <summary>
        /// Sets the look stage's global, and enables <see cref="LookStageKeyword"/> once, when the dial is on; does nothing when it
        /// is off. Called when the look is pushed, and nothing turns it off.
        /// </summary>
        public static void EnsureLookStage()
        {
            if (!LookStageOn) return;

            Shader.SetGlobalFloat("_EvoLookStage", LookStage);
            if (_lookStageKeywordOn) return;

            Shader.EnableKeyword(LookStageKeyword);
            _lookStageKeywordOn = true;
            string what = LookStage == 1
                ? "the body colour as it enters the lighting"
                : LookStage == 2 ? "the diffuse light alone" : "everything before the water, the fog not yet mixed";
            Debug.Log("[Theatre] look stage " + LookStage + " on: a producer's pixel draws " + what + ", opaque and linear (shader keyword " +
                      LookStageKeyword + "). A diagnostic, never for a film.");
        }
        // LOOKSTAGE end
        // LOOKPROBE begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_LOOK_PROBE</c> (0 to 3, an integer, default 0), read once: a diagnostic, never a look and never a film. It
        /// switches one colour term in the body shader's forward fragment, to show whether that term is live. 1 skips the marine
        /// producers' level-2 colour ramp (<c>AlgaBladeColour</c>), so its ratio is one and the blade keeps every other term; 2 draws a
        /// sea-lettuce thallus (under <c>EVOSIM_THEATRE_THALLI</c>) magenta wherever the tone multiply on a part's whole light reaches
        /// it; 3 draws every thallus a flat colour by its form (sea lettuce red, Padina green, Codium blue). The value is the global
        /// <c>_EvoLookProbe</c>, the shader's code sits under the keyword below. Off, the keyword is never
        /// enabled and the global never set.
        /// </summary>
        public static readonly int LookProbe = Mathf.RoundToInt(Dial("EVOSIM_THEATRE_LOOK_PROBE", 0f, 0f, 3f));

        /// <summary>True when the look probe's dial is at 1, 2 or 3.</summary>
        public static readonly bool LookProbeOn = LookProbe >= 1;

        /// <summary>The shader keyword the look probe's lines in <c>TheatreBody.shader</c> are compiled under.</summary>
        public const string LookProbeKeyword = "_EVO_LOOK_PROBE";

        private static bool _lookProbeKeywordOn;

        /// <summary>
        /// Sets the look probe's global, and enables <see cref="LookProbeKeyword"/> once, when the dial is on; does nothing when it
        /// is off. Called when the look is pushed, and nothing turns it off.
        /// </summary>
        public static void EnsureLookProbe()
        {
            if (!LookProbeOn) return;

            Shader.SetGlobalFloat("_EvoLookProbe", LookProbe);
            if (_lookProbeKeywordOn) return;

            Shader.EnableKeyword(LookProbeKeyword);
            _lookProbeKeywordOn = true;
            string what = LookProbe == 1
                ? "the level-2 colour ramp's multiply on a blade is skipped"
                : LookProbe == 2
                    ? "a sea-lettuce thallus draws magenta where the tone multiply on the whole light reaches it"
                : "every thallus draws a flat colour by its form where the tone multiply on the whole light reaches it (sea lettuce red, Padina green, Codium blue)";
            Debug.Log("[Theatre] look probe " + LookProbe + " on: " + what + " (shader keyword " + LookProbeKeyword +
                      "). A diagnostic, never for a film.");
        }
        // LOOKPROBE end
        // caustic-variety:begin
        /// <summary>
        /// The varied caustic (<c>EVOSIM_THEATRE_CAUSTIC_VARIETY</c>, 0 off by default), read once: the caustic net the bed, the rocks,
        /// the bodies, the shafts and the snow are lit by is built from six wave trains at six scales with a warp, group envelopes, a
        /// phase wobble and a depth-raised fold clamp (<c>TheatreWater.hlsl</c>, <c>EvoCausticGainVaried</c>). Off means the shader
        /// keyword below is never enabled and nothing else in the theatre reads it.
        /// </summary>
        public static readonly bool CausticVarietyOn = Dial("EVOSIM_THEATRE_CAUSTIC_VARIETY", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The shader keyword the varied caustic's code in <c>TheatreWater.hlsl</c> is compiled under.</summary>
        public const string CausticVarietyKeyword = "_EVO_CAUSTIC_VARIETY";

        private static bool _causticVarietyKeywordOn;

        /// <summary>
        /// Enables <see cref="CausticVarietyKeyword"/> once, for every material that has it, when the dial is on; does nothing when
        /// it is off. Called when the look is pushed and nothing turns it off.
        /// </summary>
        public static void EnsureCausticVarietyKeyword()
        {
            if (!CausticVarietyOn || _causticVarietyKeywordOn) return;

            Shader.EnableKeyword(CausticVarietyKeyword);
            _causticVarietyKeywordOn = true;
            Debug.Log("[Theatre] varied caustic on (shader keyword " + CausticVarietyKeyword + ").");
        }

        // caustic-variety:end
        /// <summary>
        /// Dappled light on bodies (<c>EVOSIM_THEATRE_BODY_DAPPLE</c>, 0 off by default; 1, 2 and 3), read once: moving
        /// patches of the water's light over the bodies in the top metres, on the one skin and on parts alike, in place of
        /// the body caustic's few hairlines on upward faces (<c>TheatreBody.shader</c>, <c>EvoDappleNet</c>). The step
        /// sets the strength (<see cref="BodyDappleStrength"/>): 0.7, 1.0 and 1.4. Off means the shader keyword below is
        /// never enabled, the global is never set and nothing else in the theatre reads it.
        /// </summary>
        public static readonly int BodyDappleStep = Mathf.RoundToInt(Dial("EVOSIM_THEATRE_BODY_DAPPLE", 0f, 0f, 3f));

        /// <summary>True when the body dapple's dial is at 1, 2 or 3.</summary>
        public static readonly bool BodyDappleOn = BodyDappleStep >= 1;

        /// <summary>The dapple's strength at each step (index 0 is off): what the caustic colour is multiplied by at its brightest.</summary>
        private static readonly float[] BodyDappleStrengths = { 0f, 0.7f, 1.0f, 1.4f };

        /// <summary>The strength of the dapple at the dial's step, zero when the dial is off.</summary>
        public static float BodyDappleStrength => BodyDappleStrengths[Mathf.Clamp(BodyDappleStep, 0, 3)];

        /// <summary>The shader keyword the dapple's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string BodyDappleKeyword = "_EVO_BODY_DAPPLE";

        private static bool _bodyDappleKeywordOn;

        /// <summary>
        /// Enables <see cref="BodyDappleKeyword"/> once, for every material that has it, when the dial is on; does nothing
        /// when it is off. Called when the look is pushed, and nothing turns it off.
        /// </summary>
        public static void EnsureBodyDappleKeyword()
        {
            if (!BodyDappleOn || _bodyDappleKeywordOn) return;

            Shader.EnableKeyword(BodyDappleKeyword);
            _bodyDappleKeywordOn = true;
            Debug.Log("[Theatre] body dapple on: step " + BodyDappleStep + ", strength " +
                      BodyDappleStrength.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                      " (shader keyword " + BodyDappleKeyword + ").");
        }

        /// <summary>
        /// The dapple's strength for the shaders (<c>_EvoDapple</c>): the step's strength while the look is not held, and zero
        /// while it is, which draws the body caustic as it was. Only called with the dial on.
        /// </summary>
        private static void PushBodyDapple(bool on)
        {
            EnsureBodyDappleKeyword();
            Shader.SetGlobalFloat("_EvoDapple", on ? BodyDappleStrength : 0f);
        }

        // ---------------------------------------------------------------- the furniture

        /// <summary>Motes of marine snow over the whole box, whatever the population.</summary>
        public int SnowMotes = 2600;

        /// <summary>Metres a mote falls per second. Slow: this is detritus, not rain.</summary>
        public float SnowFallMetresPerSecond = 0.09f;

        /// <summary>
        /// A mote's diameter in metres.
        /// </summary>
        /// <remarks>
        /// Small on purpose. A snapshot's marker for a body too small to draw is five pixels, and
        /// at the seventy-odd pixels a metre a top view of the campaign box gives, a six centimetre
        /// mote is nearly the same size: the furniture would then be competing with the census.
        /// Three centimetres reads as texture in the water and never as a body.
        /// </remarks>
        public float SnowSizeMetres = 0.03f;

        /// <summary>
        /// How long a mote lives, in seconds of wall clock.
        /// </summary>
        /// <remarks>
        /// Not the time it takes to fall the height of the box, which is what the first cut used.
        /// A sixty metre column at nine centimetres a second is an eleven minute lifetime, and a
        /// particle system fills at maxParticles over that lifetime: the water held a few hundred
        /// motes when the first pictures were taken and looked empty. Prewarm only simulates one
        /// duration, so the duration is set to this and the volume is seeded in full before the
        /// first frame. Nothing in a picture says how far a mote has fallen, so the shorter life
        /// costs nothing and the seeding is what is actually seen.
        /// </remarks>
        public float SnowSecondsOfLife = 24f;

        // ---------------------------------------------------------------- state

        private GameObject _root;
        private Light _key;
        private Light _fill;

        /// <summary>The one skin in the scene, for a camera that renders after it (the snapshot's).</summary>
        public static TheatreSkin Current { get; private set; }

        /// <summary>The key's and fill's offsets from the viewer's rotation, as <see cref="Apply"/> set them.</summary>
        private static readonly Quaternion KeyOffset = Quaternion.Euler(38f, -26f, 0f);
        private static readonly Quaternion FillOffset = Quaternion.Euler(-14f, 168f, 0f);
        private GameObject _bed;

        /// <summary>The earth under a shaped bed, drawn at the glass. See <see cref="BuildEarth"/>.</summary>
        private GameObject _earth;
        private Material _earthMaterial;

        // Where the ground is, for GroundUnder: the bed, the box and the tank's radius.
        private bool _hasGround;
        private BedShape _groundBed;
        private Bounds _groundBox;
        private float _groundRadius;
        private GameObject _surface;
        private GameObject _shafts;
        private ParticleSystem _snow;

        private Material _body;
        private Material _neck;
        private Material _joint;
        private Material _bedMaterial;
        private Material _snowMaterial;
        private Material _surfaceMaterial;
        private Material _shaftMaterial;
        private Material _backdropMaterial;
        private Material _glassMaterial;
        private GameObject _glass;

        // The reefs (logbook/specs/reef-spec.md): one holder per reef, each reef's own cut meshes,
        // and the one rock material they all share (per-reef values ride a property block).
        private readonly List<GameObject> _reefs = new List<GameObject>();
        private readonly List<Mesh> _reefMeshes = new List<Mesh>();
        private Material _reefRockMaterial;

        /// <summary>The waterline's height, from the last box dressed; zero until then, as in every recording.</summary>
        private float _waterlineY;

        /// <summary>Unit, pointing at the sun from the water. See <see cref="ResolveSun"/>.</summary>
        private Vector3 _sun = new Vector3(0.28f, 0.92f, 0.27f);

        /// <summary>Unit, pointing down the sun's ray after the surface has bent it.</summary>
        private Vector3 _sunRay = new Vector3(0.20f, -0.96f, 0.19f);

        // ---------------------------------------------------------------- the underwater look

        /// <summary>
        /// The underwater look's six switches and their numbers
        /// (<c>logbook/specs/underwater-look/brief.md</c>), read from the environment when the skin
        /// is made, every one off by default. Set before <see cref="Apply"/> to change them; the
        /// solo still sets <see cref="TheatreLook.Off"/>.
        /// </summary>
        public TheatreLook Look = TheatreLook.FromEnvironment();

        /// <summary>
        /// The sun as it was before the batch, taken from the key at the last push, which a census
        /// view goes back to while the look is held (item 1).
        /// </summary>
        private Vector3 _sunLegacy = new Vector3(0.28f, 0.92f, 0.27f);

        /// <summary>The run's light at the second last pushed (items 3 and 6).</summary>
        private TheatreDaylight.Reading _daylight = TheatreDaylight.Neutral(0f);

        /// <summary>True while a census render holds the look off (<see cref="HoldTodaysLook"/>).</summary>
        private bool _holding;

        /// <summary>The last daylight the log was told of, so a film says it once and not every frame.</summary>
        private string _daylightLogged;

        private GameObject _march;
        private Material _marchMaterial;
        private GameObject _shells;

        /// <summary>The snow shells' renderer, or null: the lens draws them after itself (<see cref="TheatreLens"/>).</summary>
        public Renderer ShellsRenderer => _shells != null ? _shells.GetComponent<Renderer>() : null;
        private Material _shellMaterial;

        private bool _fogSaved;
        private bool _fogWas;
        private FogMode _fogModeWas;
        private Color _fogColourWas;
        private float _fogDensityWas;
        private AmbientMode _ambientModeWas;
        private Color _ambientWas;
        private Color _ambientSkyWas;
        private Color _ambientEquatorWas;
        private Color _ambientGroundWas;
        private Material _skyboxWas;

        /// <summary>The one material every body renderer is painted with.</summary>
        public Material BodyMaterial => _body != null ? _body : (_body = MakeBodyMaterial());

        /// <summary>The joint neck's material. Its colour is set per neck by the palette.</summary>
        public Material NeckMaterial => _neck != null ? _neck : (_neck = MakeNeckMaterial());

        /// <summary>
        /// The joint skin's material: the body's own tissue, mottle and caustics included, with
        /// the taper and the bend off and half the carve. See <c>TheatrePalette.BuildNecks</c>.
        /// </summary>
        public Material JointMaterial => _joint != null ? _joint : (_joint = MakeJointMaterial());

        /// <summary>True when the shaders resolved and bodies can be repainted.</summary>
        public bool Ready => BodyMaterial != null;

        // ---------------------------------------------------------------- lighting

        /// <summary>
        /// Sets the water, the fog, the ambient and the two lights. Called once, from
        /// <c>TheatreRunner.Start</c>, before anything is opened.
        /// </summary>
        /// <param name="viewCamera">
        /// The camera the key is placed behind, or null for the scene's main camera. The note asks
        /// for a raking light from above and behind the viewer, which is a direction relative to
        /// where the viewer starts rather than a compass bearing.
        /// </param>
        public void Apply(Camera viewCamera)
        {
            SaveEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Water;
            RenderSettings.fogDensity = FogDensity;

            // The ambient is the water column: the lit water from above, the deep from below,
            // the flat ambient between. Low still, because the dark field is the whole look; but
            // graded, so the top of a body is a little brighter than its underside the way a
            // thing in water is (2026-09-16). Flat, as it was, with the lit water off.
            if (LitWater)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ShallowAsLit * 0.9f;
                RenderSettings.ambientEquatorColor = Ambient;
                RenderSettings.ambientGroundColor = Water;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Ambient;
            }

            RenderSettings.ambientIntensity = 1f;

            // The backdrop is the water column painted by the direction of the look
            // (TheatreBackdrop.shader), in place of the one flat colour the camera cleared to
            // until 2026-09-16. It lights nothing: the ambient above is set by hand, not from
            // it, so a body is not lit from every direction the way a default skybox would.
            if (_backdropMaterial == null) _backdropMaterial = MakeBackdropMaterial();
            RenderSettings.skybox = _backdropMaterial;

            if (viewCamera == null) viewCamera = Camera.main;

            if (viewCamera != null)
            {
                viewCamera.clearFlags = _backdropMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = Water;
            }

            EnsureRoot();

            Quaternion behind = viewCamera != null
                ? viewCamera.transform.rotation
                : Quaternion.identity;

            // The key: down and forward from where the viewer stands, so it rakes across a body
            // rather than facing it. A light from the camera's own axis flattens everything.
            Current = this;

            _key = MakeLight(
                "Theatre Key",
                behind * KeyOffset,
                new Color(0.86f, 0.94f, 1.0f),
                KeyIntensity,
                LightShadows.None);

            // The fill: weak, cold, from the other side, so the far side of a body is dark rather
            // than lost. The note's own wording.
            _fill = MakeLight(
                "Theatre Fill",
                behind * FillOffset,
                new Color(0.30f, 0.52f, 0.68f),
                FillIntensity,
                LightShadows.None);

            // The sea, told to every shader that draws any of it, now that there is a light for
            // the sun to be.
            PushWater();

            // Always told, switches on or off, so the log names the look set a picture was drawn with.
            Debug.Log("[Theatre] " + Look.Describe());
        }

        /// <summary>
        /// Puts the water, the fog, the ambient and the two lights' strengths back into the scene
        /// from this skin's fields, after something has changed them since <see cref="Apply"/>: a
        /// story's lighter look (<see cref="StoryLook"/>) does, and puts them back the same way.
        /// The lights keep their bearings; the sea's globals are pushed again, and the surface's
        /// mirrored deep colour follows the water.
        /// </summary>
        public void Relight()
        {
            RenderSettings.fogColor = Water;
            RenderSettings.fogDensity = FogDensity;

            if (LitWater)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ShallowAsLit * 0.9f;
                RenderSettings.ambientEquatorColor = Ambient;
                RenderSettings.ambientGroundColor = Water;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Ambient;
            }

            if (_key != null) _key.intensity = KeyIntensity;
            if (_fill != null) _fill.intensity = FillIntensity;
            if (_surfaceMaterial != null) _surfaceMaterial.SetColor("_DeepColor", Water);

            PushWater();
            DynamicGI.UpdateEnvironment();
        }

        // ---------------------------------------------------------------- the sea above

        /// <summary>Unit, pointing at the sun from the water.</summary>
        public Vector3 SunDirection => _sun;

        /// <summary>Unit, pointing down the sun's ray once the surface has bent it.</summary>
        public Vector3 SunRay => _sunRay;

        /// <summary>
        /// Hands the sea to every shader that draws a piece of it: the wave, the sun, and how far
        /// down the light reaches.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Global rather than per material, and that is the point.</b> Five materials draw
        /// some part of the surface's light (the ceiling, the shafts, a body, a neck, the sand),
        /// and if two of them ever held different numbers the net on a creature would stop being
        /// the light coming through the water above it, which is the whole of the fourth day.
        /// One push, one copy, no way to set four of five (<c>TheatreWater.hlsl</c>).
        /// </para>
        /// <para>
        /// Clamped here as well as in <see cref="Dial"/>, because the fields are public and a
        /// scene or a check can set them without going through it.
        /// </para>
        /// </remarks>
        public void PushWater()
        {
            // Item 1 of the underwater look: with one sun on, everything the water shows is drawn
            // from the world's fixed bearing and the key keeps following the camera for the
            // modelling; without it, the key's bearing at this push, as always. The key's is kept
            // either way, for a census render to go back to.
            _sunLegacy = ResolveSun();
            _sun = Look.OneSun && !_holding ? Look.FixedSun() : _sunLegacy;
            _sunRay = Refracted(_sun);

            Shader.SetGlobalVector("_EvoRipple", new Vector4(
                Mathf.Clamp(SurfaceWaveMetres, 0f, 0.5f),
                Mathf.Clamp(SurfaceWaveLengthMetres, 0.2f, 20f),
                Mathf.Clamp(SurfaceWaveSpeed, 0f, 4f),
                Mathf.Clamp(SurfaceLightMetres, 1f, 60f)));

            Shader.SetGlobalVector("_EvoSun", _sun);
            Shader.SetGlobalVector("_EvoSunRay", _sunRay);

            // The water column (TheatreWater.hlsl, EvoWaterColour): the lit top water and how far
            // down it reaches, for every surface's fog, the backdrop and the ambient.
            float reach = WaterReachMetres > 0.01f ? WaterReachMetres : Mathf.Clamp(SurfaceLightMetres, 1f, 60f);

            Shader.SetGlobalVector("_EvoWaterColumn", new Vector4(_waterlineY, reach, LitWater ? 1f : 0f, 1f));
            // Converted here: a global colour is handed to the shader as it is, where a material
            // colour and a render setting are converted from sRGB by the engine in a linear
            // project. The first lit-water pictures (2026-09-16) were five times too bright
            // because these two went up unconverted.
            Shader.SetGlobalColor("_EvoWaterDeep", Water.linear);
            Shader.SetGlobalColor("_EvoWaterShallow", ShallowAsLit.linear);

            PushLook();
        }

        // ---------------------------------------------------------------- the underwater look

        /// <summary>
        /// Hands the underwater look's switches and numbers to the shaders
        /// (<c>TheatreWater.hlsl</c>'s <c>_EvoLook</c>, <c>_EvoAbsorb</c>, <c>_EvoPhase</c>,
        /// <c>_EvoDaylight</c>, <c>_EvoDepthRates</c>) and turns the furniture each switch replaces
        /// off and its replacement on. With every switch off the flags are zero and every shader
        /// takes the branch it drew before the batch.
        /// </summary>
        /// <remarks>
        /// None of these is a colour, so none is converted (the linear-space gotcha is about
        /// <c>SetGlobalColor</c>; the in-scatter's colour is the column's, which
        /// <see cref="PushWater"/> converts).
        /// </remarks>
        public void PushLook()
        {
            bool on = !_holding;

            Shader.SetGlobalVector("_EvoLook", new Vector4(
                on && Look.WaterScatter ? 1f : 0f,
                on && Look.DepthLight ? 1f : 0f,
                on && Look.SkyDay ? 1f : 0f,
                1f));

            Vector3 absorb = Look.AbsorptionPerMetre;
            Shader.SetGlobalVector("_EvoAbsorb", new Vector4(
                absorb.x, absorb.y, absorb.z, Mathf.Clamp(Look.ScatterPerMetre, 0f, 1f)));

            Shader.SetGlobalVector("_EvoPhase", new Vector4(
                Mathf.Clamp(Look.PhaseG, 0f, 0.95f), Mathf.Clamp01(Look.PhaseGain), 0f, 0f));

            Shader.SetGlobalVector("_EvoDepthRates", new Vector4(
                Look.DepthRates.x, Look.DepthRates.y, Look.DepthRates.z, 0f));

            Shader.SetGlobalVector("_EvoDaylight", new Vector4(
                1f / Mathf.Max(0.1f, _daylight.AttenuationDepth),
                Look.PictureDay(_daylight.DayFactor),
                Mathf.Clamp01(Look.DepthFloor),
                _daylight.Overcast));

            PushSecond(on);
            PushAlgaBend(on);
            if (WaterEdge > 0f) Shader.SetGlobalFloat("_EvoWaterEdge", on ? WaterEdge : 0f);
            if (WaterWindow > 0f) Shader.SetGlobalFloat("_EvoWaterWindow", on ? WaterWindow : 0f);
            if (WaterGlow > 0f) Shader.SetGlobalFloat("_EvoWaterGlow", on ? WaterGlow : 0f);
            if (FamilySpotSoft > 0f) Shader.SetGlobalFloat("_EvoFamilySpotSoft", on ? FamilySpotSoft : 0f);
            if (WaterEdgeProbe > 0f) Shader.SetGlobalFloat("_EvoWaterEdgeProbe", on ? WaterEdgeProbe : 0f);
            if (WaterFacet > 0f) Shader.SetGlobalFloat("_EvoWaterFacet", on ? WaterFacet : 0f);
            if ((WaterEdge > 0f || WaterWindow > 0f || WaterGlow > 0f) && !_waterEdge5KeywordOn)
            {
                Shader.EnableKeyword(WaterEdge5Keyword);
                _waterEdge5KeywordOn = true;
                Debug.Log("[Theatre] water edge on: the surface compiles its fifth cut (shader keyword " + WaterEdge5Keyword + ").");
            }
            if (AlgaBladeOn) EnsureAlgaBladeKeyword();
            if (AlgaBlade2On) EnsureAlgaBlade2Keyword();
            // BLADE3 begin
            if (AlgaBlade3On) EnsureAlgaBlade3Keyword();
            // BLADE3 end
            // RAMPPHOTO begin
            if (AlgaRampPhotoOn) EnsureAlgaRampPhoto();
            // RAMPPHOTO end
            // RAMPSTOPS begin
            if (AlgaRampPhotoOn) EnsureAlgaRampStops();
            // RAMPSTOPS end
            // ROUNDPHOTO begin
            if (AlgaRoundPhotoOn) EnsureAlgaRoundPhoto();
            // ROUNDPHOTO end
            // ROUNDTARGET begin
            if (AlgaRoundTargetOn) EnsureAlgaRoundTarget();
            // ROUNDTARGET end
            // ARC begin
            if (AlgaArcLevel > 0)
            {
                PushAlgaArc(on);
                EnsureAlgaArcKeyword();
            }
            // ARC end
            // THALLI begin
            if (ThalliOn) EnsureThalliKeyword();
            // THALLI end
            // LOOKSTAGE begin
            if (LookStageOn) EnsureLookStage();
            // LOOKSTAGE end
            // LOOKPROBE begin
            if (LookProbeOn) EnsureLookProbe();
            // LOOKPROBE end
            if (CausticVarietyOn) EnsureCausticVarietyKeyword(); // caustic-variety
            if (BodyDappleOn) PushBodyDapple(on);
            if (Skin2On) EnsureSkin2Keyword(); // skin2
            // SHEEN2 begin
            if (SheenCapBaseOn) EnsureSheenCapBaseKeyword();
            // SHEEN2 end
            ApplyFurnitureSwitches();
        }

        /// <summary>
        /// The bend with the current's switch (<c>_EvoAlgaBend</c>, TheatreBody.shader): z the dial's strength and w
        /// one while it is on and the look is not held. Never set with the dial off, so the global stays the zero a
        /// shader global is. The flow itself is per blade (<see cref="TheatrePalette.Flow"/>).
        /// </summary>
        private void PushAlgaBend(bool on)
        {
            if (!(AlgaBendStrength > 0f)) return;

            Shader.SetGlobalVector("_EvoAlgaBend", new Vector4(0f, 0f, AlgaBendStrength, on ? 1f : 0f));
        }

        /// <summary>
        /// The snow's lens for one render (<c>_EvoSnowLens</c>, TheatreSnow.hlsl): the focus, and the
        /// aperture the camera's field of view makes on the grade's frame at <see cref="TheatreLook.SnowFStop"/>.
        /// Every mote sharp, as before, with <see cref="TheatreLook.SnowBlur"/> off.
        /// </summary>
        /// <param name="focusMetres">The shot's focus along the view axis, or 0 for none.</param>
        /// <param name="fieldOfView">The camera's vertical field of view, degrees.</param>
        public void PushSnowLens(float focusMetres, float fieldOfView)
        {
            if (!Look.SnowBlur)
            {
                Shader.SetGlobalVector("_EvoSnowLens", Vector4.zero);
                return;
            }

            float format = TheatreGrade.Current != null ? TheatreGrade.Current.FormatMillimetres : 24f;
            float focal = Camera.FieldOfViewToFocalLength(Mathf.Clamp(fieldOfView, 1f, 179f), format);

            // Under the theatre's lens the snow is seen through the same glass as everything else.
            float stop = Look.Lens && TheatreGrade.Current != null ? TheatreGrade.Current.PortraitAperture : Look.SnowFStop;
            float aperture = focal / 1000f / Mathf.Clamp(stop, 1f, 22f);
            float focus = Look.SnowFocusMetres > 0f ? Look.SnowFocusMetres : Mathf.Max(0f, focusMetres);

            Shader.SetGlobalVector("_EvoSnowLens", new Vector4(
                focus, aperture, Mathf.Clamp(Look.SnowBlurCap, 1f, 200f), Mathf.Clamp01(Look.SnowBlurKeep)));
        }

        /// <summary>
        /// The second batch's switches and numbers (<c>logbook/specs/underwater-look/brief-2.md</c>):
        /// <c>_EvoLook2</c>, <c>_EvoFade</c>, the snow's references and drift, the shafts' placement
        /// on the march's material, and the tone step. All zero, and the tone step off, while a
        /// census view holds today's look or with the switches off.
        /// </summary>
        private void PushSecond(bool on)
        {
            Shader.SetGlobalVector("_EvoLook2", new Vector4(
                on && Look.FadeToBackdrop ? 1f : 0f,
                on && Look.RunSnow && _snowOnTexture ? 1f : 0f,
                on && Look.SnowDrift ? 1f : 0f,
                on && Look.ShaftPlace ? 1f : 0f));

            Shader.SetGlobalVector("_EvoFade", new Vector4(
                Mathf.Clamp(Look.FadeStartMetres, 0f, 10f),
                Mathf.Max(0f, Look.FadeScatterScale * Look.ScatterPerMetre),
                Mathf.Clamp(Look.WaterGain, 0.2f, 2f),
                Mathf.Max(Mathf.Clamp(Look.FadeFullMetres, 1f, 40f), Mathf.Clamp(Look.FadeStartMetres, 0f, 10f) + 1f)));

            Shader.SetGlobalVector("_EvoSnowRef", new Vector4(
                Mathf.Max(0.01f, Look.SnowReference),
                Mathf.Max(0.01f, Look.FloorReference),
                Mathf.Clamp01(Look.FloorDust),
                Mathf.Clamp01(Look.SnowLeast)));

            Shader.SetGlobalVector("_EvoSnowDrift", new Vector4(
                _snowDrift.x, _snowDrift.y, _snowDrift.z, on && Look.SnowDrift ? 1f : 0f));

            Shader.SetGlobalVector("_EvoSnowFlake", new Vector4(
                Mathf.Clamp(Look.SnowSizeSpread, 0f, 1.2f),
                Mathf.Clamp(Look.SnowBigShare, 0f, 0.5f),
                Mathf.Clamp(Look.SnowBigFactor, 1f, 8f),
                Mathf.Clamp01(Look.SnowShape)));
            Shader.SetGlobalVector("_EvoSnowFluff", new Vector4(
                Mathf.Clamp(Look.SnowFluff, 0f, 2f), Mathf.Clamp(Look.SnowAggregate, 0f, 2f), Look.SnowConserve ? 1f : 0f, 0f));

            // The bodies and the water (TheatreBody.shader's _EvoBodyWater): zero, today's bodies, while a census
            // view holds today's look.
            Shader.SetGlobalVector("_EvoBodyWater", on ? new Vector4(
                Mathf.Clamp01(Look.BodyWater), Mathf.Clamp01(Look.BodyRimCut),
                Mathf.Clamp01(Look.BodyGlowCut), Mathf.Clamp01(Look.BodyTransCut)) : Vector4.zero);

            if (_marchMaterial != null)
            {
                bool place = on && Look.ShaftPlace;

                _marchMaterial.SetFloat("_PlaceShare", Mathf.Clamp(Look.ShaftShare, 0.05f, 1f));
                _marchMaterial.SetFloat("_PlacePower", Mathf.Clamp(Look.ShaftNarrow, 1f, 12f));
                _marchMaterial.SetFloat("_PlaceOverObject", Mathf.Clamp01(Look.ShaftOverObject));
                _marchMaterial.SetFloat("_PlaceMaskMetres", Mathf.Clamp(Look.ShaftPatchMetres, 1f, 40f));
                _marchMaterial.SetFloat("_PlaceReach", Mathf.Clamp(Look.ShaftReachMetres, 4f, 200f));
                _marchMaterial.SetFloat("_SheetMetres", Mathf.Clamp(Look.ShaftSheetMetres, 0.5f, 20f));
                _marchMaterial.SetFloat("_SheetDepthMetres", Mathf.Clamp(Look.ShaftDepthMetres, 1f, 100f));
                _marchMaterial.SetFloat("_SheetShimmer", Mathf.Clamp01(Look.ShaftShimmer));
                _marchMaterial.SetFloat("_SheetOnsetMetres", Mathf.Clamp(Look.ShaftOnsetMetres, 0.1f, 20f));
                bool debug = place && Look.ShaftDebug > 0.5f;
                _marchMaterial.SetFloat("_PlaceDebug", debug ? Look.ShaftDebug : 0f);
                _marchMaterial.SetFloat("_DstBlend", (float)(debug ? BlendMode.Zero : place ? BlendMode.OneMinusSrcColor : BlendMode.One));
                _marchMaterial.SetFloat("_MarchStrength", place
                    ? Mathf.Clamp(Look.ShaftPlaceStrength, 0f, 0.4f)
                    : Mathf.Clamp(Look.MarchStrength, 0f, 0.2f));
            }

            if (Look.ToneStep) TheatreToneStep.Ensure();
            TheatreToneStep.Active = on && Look.ToneStep;
            TheatreToneStep.Curve = new Vector4(
                Mathf.Clamp(Look.BlackPoint, 0f, 0.3f),
                Mathf.Clamp(Look.ToneS, 1f, 2f),
                Mathf.Clamp(Look.TonePivot, 0.1f, 0.7f),
                Mathf.Clamp(Look.ToneShoulder, 1f, 2f));
        }

        // ---------------------------------------------------------------- the run's snow (second batch)

        private Vector3 _waterLow, _waterHigh;
        private Vector4 _waterTank;
        private bool _waterKnown;

        /// <summary>
        /// Whether a point is in the dressed world's water: under the waterline, in the box and, in
        /// a tank, inside the glass (<c>TheatreWater.hlsl</c>'s <c>EvoEyeInWater</c>, on the CPU).
        /// Under the waterline alone before a world is dressed.
        /// </summary>
        public bool EyeInWater(Vector3 p)
        {
            if (!_waterKnown) return p.y < _waterlineY;

            if (p.x < _waterLow.x || p.y < _waterLow.y || p.z < _waterLow.z) return false;
            if (p.x > _waterHigh.x || p.y > _waterHigh.y || p.z > _waterHigh.z) return false;

            if (_waterTank.w > 0.5f)
            {
                float dx = p.x - _waterTank.x, dz = p.z - _waterTank.y;
                if (dx * dx + dz * dz > _waterTank.z * _waterTank.z) return false;
            }

            return true;
        }

        /// <summary>The most reef caps the marched shafts are shadowed by (<c>TheatreShaftMarch.shader</c>).</summary>
        private const int MaxMarchCaps = 32;

        /// <summary>The run's snow fields and water, opened at the first frame of a run (null before).</summary>
        private TheatreRunSnow _runSnow;

        /// <summary>The run the snow was opened for, so a second run opens its own.</summary>
        private string _runSnowPath;

        /// <summary>True when the snow texture holds the run's fields at the last pushed second.</summary>
        private bool _snowOnTexture;

        private Texture2D _snowTexture;
        private float[] _snowDensity;
        private float[] _snowFloor;
        private double _snowTextureSecond = double.NaN;

        /// <summary>How far the water has carried the snow, metres, since the sequence began.</summary>
        private Vector3 _snowDrift;
        private double _snowDriftSecond = double.NaN;
        private float _snowDriftClock = float.NaN;
        private int _snowDriftPushes;
        private int _filmClockFrame = -1;
        private string _snowLogged;

        /// <summary>
        /// The run's light and snow at the frame's second, for a render from
        /// <paramref name="eye"/>: <see cref="PushDaylight"/>, then the snow fields and the drift
        /// (<see cref="PushSnow"/>). What every non-census render calls.
        /// </summary>
        public void PushFrame(ITheatreFrame frame, Vector3 eye, double filmSecond = double.NaN)
        {
            PushDaylight(TheatreDaylight.At(frame));
            PushSnow(frame, eye, filmSecond);
            if (TheatreUptake.Enabled) TheatreUptake.Frame(frame, eye, filmSecond);
            if (TheatreMouth2.Bite > 0f) TheatreMouthBite.Frame(frame);
            if (TheatreMouthBite.KillContactLog) TheatreMouthBite.LogContacts(frame);
        }

        /// <summary>
        /// The second batch's snow at the frame's second (items 4 to 6 and the drift): the run's
        /// column density and floor stock on the snow texture, and the water's displacement around
        /// the camera, accumulated over the simulation's seconds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The drift's clock is the simulation's.</b> Between two pushes the snow moves by the
        /// current at the camera times the seconds the world advanced, and sinks at the run's own
        /// rate; a film whose clock runs faster than the world's still moves the snow by the
        /// world's seconds. While the world stands still (a frozen film, or the same second pushed
        /// again) and the shaders' clock moves on, it drifts at <see cref="TheatreLook.SnowAlive"/>
        /// of the current, at least a centimetre a second along it, so a frozen frame's snow is
        /// alive. A jump of more than a minute either way, or a first push, starts a new sequence
        /// at zero: a still is the water at rest at its own second.
        /// </para>
        /// <para>
        /// <b>A film drives the drift by its own second</b> (<paramref name="filmSecond"/>), the
        /// continuous one its label prints. The frame's world second moves in half-second metabolic
        /// steps, so a drift read off it crept between steps on the slow alive drift and then took
        /// the whole half second of current in one frame: at 24 fps the snow jumped every twelfth
        /// frame (clip21, 2026-10-07). While a film is giving its second, a push without one (the
        /// runner's every frame, at the fly camera) leaves the drift alone.
        /// </para>
        /// </remarks>
        public void PushSnow(ITheatreFrame frame, Vector3 eye, double filmSecond = double.NaN)
        {
            if (!(Look.RunSnow || Look.SnowDrift || AlgaBendStrength > 0f) || frame == null) return;

            string path = frame.Record?.Path;
            if (_runSnow == null || _runSnowPath != path)
            {
                _runSnow = TheatreRunSnow.Open(frame);
                _runSnowPath = path;
                _snowTextureSecond = double.NaN;
                _snowDriftSecond = double.NaN;
                Debug.Log("[Theatre] look: snow fields: " + _runSnow.FieldSource + "; water: " + _runSnow.CurrentSource);
            }

            if (!TheatreDaylight.TrySecond(frame, out double second)) second = double.NaN;

            // Items 4 and 5: the fields at the second, uploaded when the second changes.
            if (Look.RunSnow && _runSnow.HasFields && !double.IsNaN(second) &&
                (double.IsNaN(_snowTextureSecond) || System.Math.Abs(second - _snowTextureSecond) >= 1.0))
            {
                int columns = _runSnow.CellsX * _runSnow.CellsZ;
                if (_snowDensity == null || _snowDensity.Length != columns)
                {
                    _snowDensity = new float[columns];
                    _snowFloor = new float[columns];
                }

                if (_runSnow.TryAt(second, _snowDensity, _snowFloor, out string at))
                {
                    UploadSnow();
                    _snowTextureSecond = second;
                    _snowOnTexture = true;

                    // Said when the pair of dumps changes, not at every second between them.
                    string key = System.Text.RegularExpressions.Regex.Replace(at, " at [0-9.]+$", "");
                    if (key != _snowLogged)
                    {
                        _snowLogged = key;
                        Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "[Theatre] look: snow from the run at {0:0.#} s ({1}); reference {2:0.##} J/m3, floor {3:0.##} J",
                            second, at, Look.SnowReference, Look.FloorReference));
                    }
                }
                else
                {
                    _snowOnTexture = false;
                    Debug.Log("[Theatre] look: the snow fields refused the second (" + at + "); the snow is decorative");
                }
            }

            // The drift, on the film's second where there is one.
            bool filmGives = !double.IsNaN(filmSecond) && !double.IsInfinity(filmSecond);
            if (filmGives) _filmClockFrame = Time.frameCount;
            bool filmOwns = !filmGives && _filmClockFrame >= 0 && Time.frameCount - _filmClockFrame <= 2;

            if (Look.SnowDrift && !filmOwns) AdvanceDrift(filmGives ? filmSecond : second, eye);

            // The second the bend with the current reads the run's water at: the film's where it gives one, as the
            // drift's is. Not entered with that dial off.
            if (AlgaBendStrength > 0f && (filmGives || !filmOwns)) _waterSecond = filmGives ? filmSecond : second;

            PushLook();
        }

        /// <summary>The run's second the water was last pushed at, NaN before any (the bend with the current).</summary>
        private double _waterSecond = double.NaN;

        /// <summary>
        /// Whether the run being replayed has a current the theatre could build (<see cref="TheatreRunSnow.HasCurrent"/>),
        /// which only a push of a frame under the bend with the current, the snow fields or the drift opens.
        /// </summary>
        public bool HasRunWater => _runSnow != null && _runSnow.HasCurrent;

        /// <summary>
        /// The run's water velocity at a place in the world's frame at the second last pushed, m/s: the world's
        /// current as <c>Evosim.Core</c>'s field gives it (the same field the snow's drift reads), horizontal and
        /// vertical. Zero without a current.
        /// </summary>
        public Vector3 WaterAt(Vector3 place) => WaterAt(place, _waterSecond);

        /// <summary>The same at a second the caller names (a fed view knows the frame it has just placed; the push comes after it).</summary>
        public Vector3 WaterAt(Vector3 place, double second) =>
            HasRunWater && !double.IsNaN(second) ? _runSnow.VelocityAt(place, second) : Vector3.zero;

        /// <summary>The run's second the water was last pushed at, NaN before any.</summary>
        public double WaterSecond => _waterSecond;

        /// <summary>
        /// How fast the run's snow sinks, m/s: the rate the shells' drift lowers their motes at, which the crumble's flakes
        /// sink by too (D277). Zero before a push has opened the run's water.
        /// </summary>
        public float SnowSinkMetresPerSecond => _runSnow != null ? _runSnow.SinkMetresPerSecond : 0f;

        /// <summary>
        /// Whether a push of a frame has opened the run's water, so the current and the sink above read the run's and not zero (D277's
        /// flakes cache both at their birth).
        /// </summary>
        public bool RunWaterOpen => _runSnow != null;

        /// <summary>
        /// Whether any switch that opens the run's water is on (the snow fields, the drift or the bend with the current), as
        /// <see cref="PushSnow"/> tests it. With none on the water is never opened, and <see cref="RunWaterOpen"/> stays false.
        /// </summary>
        public bool RunWaterWanted => Look.RunSnow || Look.SnowDrift || AlgaBendStrength > 0f;

        /// <summary>
        /// The snow shells' material, or null when the look has none: the crumble's flakes take the water's box, tank and
        /// waterline and the motes' colour and gain from it, so they sit in the same water the shells do (D277).
        /// </summary>
        public Material SnowShellsMaterial => _shellMaterial;

        private void UploadSnow()
        {
            int width = _runSnow.CellsZ, height = _runSnow.CellsX;

            if (_snowTexture == null || _snowTexture.width != width || _snowTexture.height != height)
            {
                if (_snowTexture != null) UnityEngine.Object.DestroyImmediate(_snowTexture);
                _snowTexture = new Texture2D(width, height, TextureFormat.RGFloat, false, true)
                {
                    name = "Theatre Run Snow",
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            var pixels = new Vector2[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Vector2(_snowDensity[i], _snowFloor[i]);

            _snowTexture.SetPixelData(pixels, 0);
            _snowTexture.Apply(false, false);

            Shader.SetGlobalTexture("_EvoSnowField", _snowTexture);
            Shader.SetGlobalVector("_EvoSnowGrid", new Vector4(0f, 1f / _runSnow.CellMetres, 0f, _runSnow.CellsX));
            Shader.SetGlobalVector("_EvoSnowCells", new Vector4(_runSnow.CellsZ, 0f, 0f, 0f));
        }

        private void AdvanceDrift(double second, Vector3 eye)
        {
            float clock = Time.timeSinceLevelLoad;
            bool fresh = double.IsNaN(_snowDriftSecond) || double.IsNaN(second) || System.Math.Abs(second - _snowDriftSecond) > 60.0;

            if (fresh)
            {
                _snowDrift = Vector3.zero;
            }
            else
            {
                Vector3 v = _runSnow.VelocityAt(eye, second);
                double worldStep = second - _snowDriftSecond;

                if (worldStep != 0.0)
                {
                    // The water's displacement over the world's step, and the snow's own sinking.
                    _snowDrift += v * (float)worldStep;
                    _snowDrift.y -= _runSnow.SinkMetresPerSecond * (float)worldStep;
                }
                else if (!float.IsNaN(_snowDriftClock) && clock > _snowDriftClock)
                {
                    // The world stands still and the shaders' clock does not: a slow drift along
                    // the water's own direction, so the snow is alive.
                    float step = clock - _snowDriftClock;
                    float speed = Mathf.Max(0.01f, Look.SnowAlive * v.magnitude);
                    Vector3 along = v.sqrMagnitude > 1e-10f ? v.normalized : new Vector3(0.7071f, 0f, 0.7071f);

                    _snowDrift += along * (speed * step);
                    _snowDrift.y -= Mathf.Max(_runSnow.SinkMetresPerSecond, 0.002f) * step;
                }
            }

            _snowDriftSecond = second;
            _snowDriftClock = clock;

            // For the log, so a film's drift can be read without a picture: the first pushes of a
            // sequence and every thirtieth after.
            _snowDriftPushes = fresh ? 0 : _snowDriftPushes + 1;
            if (_snowDriftPushes < 3 || _snowDriftPushes % 30 == 0)
            {
                Vector3 v = _runSnow.VelocityAt(eye, second);
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[Theatre] look: snow drift at {0:0.###} s (push {1}): water {2:0.0000}/{3:0.0000}/{4:0.0000} m/s at the camera, carried {5:0.000}/{6:0.000}/{7:0.000} m",
                    second, _snowDriftPushes, v.x, v.y, v.z, _snowDrift.x, _snowDrift.y, _snowDrift.z));
            }
        }

        /// <summary>
        /// Where the snow came from, for the label: "snow from the run" when the shells and the bed
        /// draw the run's fields, "snow decorative" otherwise; and "no drift" when the drift is on
        /// and the run's current could not be read. Null with neither switch on or the look held.
        /// </summary>
        public string SnowToken()
        {
            if (_holding || !(Look.RunSnow || Look.SnowDrift)) return null;

            string source = Look.RunSnow && _snowOnTexture ? "snow from the run" : "snow decorative";
            if (Look.SnowDrift && (_runSnow == null || !_runSnow.HasCurrent)) source += ", no drift";

            return source;
        }

        /// <summary>
        /// The run's light at the second about to be drawn (items 3 and 6). The snapshot camera
        /// pushes it before every render and the runner once a second in Play mode; a scene that
        /// never does draws the mean day.
        /// </summary>
        public void PushDaylight(TheatreDaylight.Reading reading)
        {
            _daylight = reading;
            PushLook();

            if (!Look.DepthLight && !Look.SkyDay) return;

            string line = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[Theatre] look: day factor {0:0.###} ({1}), attenuation depth {2:0.##} m, overcast {3:0.##}",
                reading.DayFactor, reading.Source, reading.AttenuationDepth, reading.Overcast);

            // Said when it changes by more than the log can read, not every frame of a film.
            string key = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.00}|{1:0.0}|{2}", reading.DayFactor, reading.AttenuationDepth,
                reading.Source != null && reading.Source.StartsWith("stats", System.StringComparison.Ordinal) ? "stats" : reading.Source);

            if (key == _daylightLogged) return;
            _daylightLogged = key;
            Debug.Log(line);
        }

        /// <summary>What a metered view is of, which decides what the meter gives back and how far.</summary>
        public enum MeterView
        {
            /// <summary>A subject at a depth (the close view, the reef, a film's shots): depth and day, at most ±2 EV.</summary>
            Subject,

            /// <summary>A view of the whole tank (the bed view): the day's share alone, at most ±1 EV.</summary>
            Wide,

            /// <summary>The sky view: no meter, so the window and the sky keep the day's own order.</summary>
            Sky,
        }

        /// <summary>
        /// The exposure, in EV, item 3's meter opens or closes a frame by, so a deep or a winter
        /// shot is still readable without being lifted past the look before the batch. Zero with
        /// item 3 off, the meter off, the look held, or a sky view.
        /// </summary>
        /// <remarks>
        /// Two parts. The day: <see cref="TheatreLook.MeterDayShare"/> of the picture day's
        /// departure from the mean, under one, so a summer frame stays brighter than a winter one of
        /// the same view (everything the day lights is scaled by the whole picture day, the floor
        /// included). The depth, for a view of a subject only: <see cref="TheatreLook.MeterStrength"/>
        /// of what the subject's depth took, which is the same in both seasons and so cannot
        /// reverse them. Then capped: ±2 EV for a subject, ±1 EV for a view of the whole tank, where
        /// the first stills' +3 EV took the bed view from a mean luma of 37 to 153.
        /// </remarks>
        public float MeterEv(float subjectY, MeterView view)
        {
            // The second batch's item 2: with the tone step on, the sky view takes the day's share
            // as the bed view does (at most 1 EV, the seasons' order kept), so a summer window is
            // not white and a winter one not grey (iteration 4: 126 and 59 against a bar of 77 to
            // 120 and 60 to 77).
            if (view == MeterView.Sky && Look.ToneStep && !_holding) view = MeterView.Wide;

            if (!Look.DepthLight || _holding || view == MeterView.Sky) return 0f;

            float strength = Mathf.Clamp01(Look.MeterStrength);
            if (strength <= 0f) return 0f;

            float day = Mathf.Max(1e-3f, Look.PictureDay(_daylight.DayFactor));
            float ev = -Mathf.Log(day, 2f) * Mathf.Clamp(Look.MeterDayShare, 0f, 0.9f);
            float scene = day;

            if (view == MeterView.Subject)
            {
                float depth = Mathf.Max(0f, _waterlineY - subjectY);
                float fall = Mathf.Exp(-depth / Mathf.Max(0.1f, _daylight.AttenuationDepth) * Look.DepthRates.y);
                fall = Mathf.Max(fall, Mathf.Max(1e-3f, Look.DepthFloor));

                ev += -Mathf.Log(fall, 2f) * strength;
                scene *= fall;
            }

            // Toward the look before the batch and never past it: at most nine tenths of the way
            // back to the subject's light at the mean day at the surface, so a frame darker than
            // that stays darker and a brighter one stays brighter, and the seasons keep their order
            // even where both reach this bound.
            float back = -Mathf.Log(Mathf.Max(1e-4f, scene), 2f) * 0.9f;
            ev = Mathf.Clamp(ev, Mathf.Min(0f, back), Mathf.Max(0f, back));

            float most = view == MeterView.Wide ? TheatreLook.MeterWideMostEv : TheatreLook.MeterMostEv;
            return Mathf.Clamp(ev, -most, most);
        }

        /// <summary>The look's token for a picture's label (<see cref="TheatreLook.LabelToken"/>).</summary>
        public string LabelToken(string gradeToken, bool census)
        {
            string token = Look.LabelToken(gradeToken, census || _holding);

            // The second batch's item 6: where the snow came from, on every picture the look draws.
            string snow = census || _holding ? null : SnowToken();

            return snow == null ? token : token + " · " + snow;
        }

        /// <summary>
        /// Puts today's look back for the length of one render, whatever the switches say, and
        /// hands back what puts the look back on. The census views (side, end, top, iso) are
        /// records and must stay comparable (the brief's rules), so <see cref="SnapshotCamera"/>
        /// holds the look around each of them. Null when there is nothing to hold.
        /// </summary>
        /// <remarks>
        /// What it holds: the sun goes back to the key's (item 1), the shaders' flags go to zero
        /// (items 2, 3 and 6), and the quads and the particle snow come back in place of the march
        /// and the shells (items 4 and 5). Taken before the census hides what only the water sees,
        /// and released after it has put that back, so the hiding sees the furniture of today.
        /// </remarks>
        public System.IDisposable HoldTodaysLook()
        {
            if (!Look.Any || _holding) return null;

            _holding = true;
            PushSun();
            PushLook();

            return new LookHold(this);
        }

        private sealed class LookHold : System.IDisposable
        {
            private TheatreSkin _skin;

            public LookHold(TheatreSkin skin) { _skin = skin; }

            public void Dispose()
            {
                if (_skin == null) return;

                _skin._holding = false;
                _skin.PushSun();
                _skin.PushLook();
                _skin = null;
            }
        }

        /// <summary>The sun alone, as <see cref="PushWater"/> would set it, from the key's last bearing.</summary>
        private void PushSun()
        {
            _sun = Look.OneSun && !_holding ? Look.FixedSun() : _sunLegacy;
            _sunRay = Refracted(_sun);

            Shader.SetGlobalVector("_EvoSun", _sun);
            Shader.SetGlobalVector("_EvoSunRay", _sunRay);
        }

        /// <summary>
        /// Each replaced piece of furniture off while its replacement is on, and back while the
        /// look is held: the nine quads against the march (item 4), the particle snow against the
        /// shells (item 5).
        /// </summary>
        private void ApplyFurnitureSwitches()
        {
            bool on = !_holding;

            SetRendered(_shafts, !(on && Look.ShaftMarch));
            SetRendered(_snow != null ? _snow.gameObject : null, !(on && Look.SnowShells));
            SetRendered(_march, on && Look.ShaftMarch);
            SetRendered(_shells, on && Look.SnowShells);
        }

        /// <summary>
        /// Which of the look's furniture exists and is drawn, for the build's check
        /// (<c>LookShaderCheck</c>): quads, particle snow, march, shells, each "-" when not built.
        /// </summary>
        public string FurnitureState()
        {
            string Of(GameObject holder)
            {
                if (holder == null) return "-";
                Renderer renderer = holder.GetComponent<Renderer>();
                return renderer != null && renderer.enabled ? "drawn" : "off";
            }

            return "quads " + Of(_shafts) + ", particle snow " + Of(_snow != null ? _snow.gameObject : null) +
                   ", march " + Of(_march) + ", shells " + Of(_shells) +
                   ", sun " + _sun.ToString("F3") + (_holding ? ", held" : "");
        }

        private static void SetRendered(GameObject holder, bool shown)
        {
            if (holder == null) return;

            Renderer renderer = holder.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = shown;
        }

        /// <summary>
        /// The marched shafts (item 4): a box over the lit water, drawn from its inside, on which
        /// <c>TheatreShaftMarch.shader</c> marches every view ray (<c>TheatreVolume.hlsl</c>).
        /// </summary>
        private void BuildMarch(Vector3 min, Vector3 size, float tankRadius, ReefGeometry reefs)
        {
            if (!Look.ShaftMarch) return;

            Material material = _marchMaterial != null ? _marchMaterial : (_marchMaterial = MakeVolumeMaterial("Evosim/Theatre Shaft March", "Theatre Shaft March"));
            if (material == null) return;

            float waterline = min.y + size.y;

            // Down to where the light is a few hundredths of the surface's, and no further than
            // the floor: below it a march would spend its steps on water nothing lights.
            float reach = Mathf.Clamp(SurfaceLightMetres, 1f, 60f);
            float depth = Mathf.Min(size.y, Mathf.Max(12f, 1.5f * reach));

            var low = new Vector3(min.x, waterline - depth, min.z);
            var high = new Vector3(min.x + size.x, waterline, min.z + size.z);

            SetVolume(material, low, high, min, size, tankRadius, waterline);
            material.SetFloat("_MarchStrength", Mathf.Clamp(Look.MarchStrength, 0f, 0.2f));
            material.SetFloat("_MarchSteps", Mathf.Clamp(Look.MarchSteps, 32, 64));

            // The second batch's item 3: the reefs' caps, for the march to leave the water under
            // them unlit (centre x and z, the cap's widest radius, its underside's height). Only
            // the shafts read them: the theatre draws no other shadow.
            var caps = new Vector4[MaxMarchCaps];
            int capCount = 0;
            if (reefs != null)
            {
                for (int i = 0; i < reefs.Count && capCount < MaxMarchCaps; i++)
                {
                    ReefLook cap = ReefLook.From(reefs, i);
                    caps[capCount++] = new Vector4(cap.CentreX, cap.CentreZ, cap.MaxRadius(), cap.CapUndersideY);
                }
            }
            material.SetVectorArray("_ReefCaps", caps);
            material.SetFloat("_ReefCapCount", capCount);

            _march = VolumeObject("Theatre Shaft March", low, high, material);

            // The second batch's placement on the new material (item 3), when it is on.
            PushSecond(!_holding);
        }

        /// <summary>
        /// The snow around the camera (item 5): a box over the whole water, drawn from its inside,
        /// on which <c>TheatreSnowShells.shader</c> walks the three shells of each view ray.
        /// </summary>
        private void BuildShells(Vector3 min, Vector3 size, float tankRadius)
        {
            if (!Look.SnowShells) return;

            Material material = _shellMaterial != null ? _shellMaterial : (_shellMaterial = MakeVolumeMaterial("Evosim/Theatre Snow Shells", "Theatre Snow Shells"));
            if (material == null) return;

            float waterline = min.y + size.y;
            var low = min;
            var high = new Vector3(min.x + size.x, waterline, min.z + size.z);

            SetVolume(material, low, high, min, size, tankRadius, waterline);
            material.SetFloat("_ShellGain", 0.35f * Mathf.Clamp(Look.ShellGain, 0f, 4f));
            material.SetFloat("_ShareScale", Mathf.Clamp(Look.SnowQuantity, 0.25f, 3f));
            material.SetFloat("_MoteAmbient", Mathf.Clamp(Look.SnowAmbient, 0f, 6f));

            _shells = VolumeObject("Theatre Snow Shells", low, high, material);
        }

        private static void SetVolume(Material material, Vector3 low, Vector3 high, Vector3 min, Vector3 size, float tankRadius, float waterline)
        {
            material.SetVector("_BoxMin", low);
            material.SetVector("_BoxMax", high);
            material.SetVector("_Tank", new Vector4(
                min.x + 0.5f * size.x, min.z + 0.5f * size.z, tankRadius, tankRadius > 0f ? 1f : 0f));
            material.SetFloat("_Waterline", waterline);
        }

        private GameObject VolumeObject(string name, Vector3 low, Vector3 high, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);

            go.AddComponent<MeshFilter>().sharedMesh = VolumeBox(name, low, high);

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Seen from inside the water only, and turned off outright for the census views, as
            // the quads and the ceiling are.
            go.AddComponent<TheatreInsideOnly>();

            return go;
        }

        /// <summary>
        /// A closed box in world coordinates with its faces wound outward, so <c>Cull Front</c>
        /// draws its inside: one fragment a pixel wherever it is on screen, from inside or out.
        /// </summary>
        /// <remarks>
        /// Each face's two triangles are (c0, c2, c1) and (c0, c3, c2) over corners c0 = -U-V,
        /// c1 = -U+V, c2 = +U+V, c3 = +U-V with U x V the outward normal; Unity's front face is
        /// the side cross(b - a, c - a) points to, as the bed's grid is wound.
        /// </remarks>
        private static Mesh VolumeBox(string name, Vector3 low, Vector3 high)
        {
            Vector3 centre = 0.5f * (low + high);
            Vector3 half = 0.5f * (high - low);

            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);

            void Face(Vector3 normal, Vector3 u, Vector3 v)
            {
                Vector3 c = centre + Vector3.Scale(normal, half);
                Vector3 du = Vector3.Scale(u, half);
                Vector3 dv = Vector3.Scale(v, half);

                int first = vertices.Count;
                vertices.Add(c - du - dv);
                vertices.Add(c - du + dv);
                vertices.Add(c + du + dv);
                vertices.Add(c + du - dv);

                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }

            Face(Vector3.right, Vector3.up, Vector3.forward);
            Face(Vector3.left, Vector3.forward, Vector3.up);
            Face(Vector3.up, Vector3.forward, Vector3.right);
            Face(Vector3.down, Vector3.right, Vector3.forward);
            Face(Vector3.forward, Vector3.right, Vector3.up);
            Face(Vector3.back, Vector3.up, Vector3.right);

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Material MakeVolumeMaterial(string shaderName, string name)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[Theatre] look: shader '" + shaderName + "' not found; " + name + " is not drawn.");
                return null;
            }

            return new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>
        /// Where the sun is, as a unit vector pointing at it from the water.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is the scene's directional light, so that the key and the sun agree.</b> A disc
        /// drawn in the ceiling at a bearing of its own would be a second sun, and a viewer would
        /// read the light on the bodies against it and find it wrong. The key is the light this
        /// class made and the one URP will pick as the main light, so it is asked first; the
        /// scene's own sun and then any enabled directional light are the fallbacks, for a scene
        /// that dressed itself without calling <see cref="Apply"/>.
        /// </para>
        /// <para>
        /// <b>Lifted to a low elevation if it points below the horizon.</b> The key is aimed
        /// relative to where the viewer's camera starts, so a scene whose camera begins looking
        /// upward would put the sun under the sea, at which Snell's window has nothing in it and
        /// the shafts point up. Eight degrees is the floor: a low sun is a long window and a nice
        /// picture, and a sun below the water is not a picture at all.
        /// </para>
        /// </remarks>
        private Vector3 ResolveSun()
        {
            Light light = _key;

            if (light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
            {
                light = RenderSettings.sun;
            }

            if (light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
            {
                light = null;

                foreach (Light other in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (other == null || !other.isActiveAndEnabled) continue;
                    if (other.type != LightType.Directional) continue;

                    light = other;
                    break;
                }
            }

            // A directional light's forward is the way its light travels, so the sun is behind it.
            Vector3 sun = light != null ? -light.transform.forward : new Vector3(0.28f, 0.92f, 0.27f);

            if (sun.sqrMagnitude < 1e-6f) sun = new Vector3(0.28f, 0.92f, 0.27f);
            sun = sun.normalized;

            const float lowest = 0.15f;

            if (sun.y < lowest)
            {
                Vector3 flat = new Vector3(sun.x, 0f, sun.z);
                if (flat.sqrMagnitude < 1e-6f) flat = Vector3.right;

                sun = (flat.normalized * Mathf.Sqrt(1f - lowest * lowest) + Vector3.up * lowest)
                    .normalized;
            }

            return sun;
        }

        /// <summary>
        /// The sun's ray once the surface has bent it, as a unit vector pointing down.
        /// </summary>
        /// <remarks>
        /// Snell's law at a flat interface, air into water, with water's index at 1.333: the ray
        /// is turned towards the vertical, so a sun forty five degrees up from the horizon sends
        /// its light down at thirty two degrees from the vertical rather than forty five. The shafts are built along this rather than along the sun itself, and the
        /// caustic net is read one of these back up the way the light came
        /// (<c>TheatreWater.hlsl</c>), so the beams and the pattern they throw agree about which
        /// way the light is going. Coming from air there is no total internal reflection, and the
        /// guard is there because an arithmetic slip should give a vertical beam rather than a
        /// non-finite one.
        /// </remarks>
        private static Vector3 Refracted(Vector3 sun)
        {
            const float eta = 1f / 1.333f;

            Vector3 travel = -sun;
            float cosIn = Mathf.Clamp01(sun.y);

            float k = 1f - eta * eta * (1f - cosIn * cosIn);
            if (k <= 0f) return travel;

            Vector3 ray = eta * travel + (eta * cosIn - Mathf.Sqrt(k)) * Vector3.up;

            return ray.sqrMagnitude < 1e-6f ? Vector3.down : ray.normalized;
        }

        private Light MakeLight(string name, Quaternion rotation, Color colour, float intensity,
                                LightShadows shadows)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = rotation;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = shadows;

            return light;
        }

        /// <summary>
        /// Turns off the scene's own directional light, if the scene builder left one.
        /// </summary>
        /// <remarks>
        /// The generated theatre scene carries a single directional light at intensity 1.1 aimed
        /// down and to one side. Leaving it on top of the key and the fill would be a third light
        /// nobody asked for, and a body lit from three directions has no dark side to read a rim
        /// against. Disabled rather than deleted, so rebuilding the scene is still the way to get
        /// it back.
        /// </remarks>
        public void SilenceSceneLights()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null || light == _key || light == _fill) continue;
                if (light.type != LightType.Directional) continue;

                light.enabled = false;
            }
        }

        // ---------------------------------------------------------------- the furniture

        /// <summary>
        /// Puts the bed and the snow in the water the run was simulated in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A tank is dressed by its bounding square, and that is enough for the two surfaces
        /// that matter.</b> <see cref="SnapshotCamera.BoxOf"/> hands back <c>[0, 2R)²</c> for a
        /// tank (<c>logbook/specs/tank-spec.md</c>), so the bed below covers the circle with its
        /// own 2% overhang to spare and the ceiling covers it several times over — its margin is
        /// half the footprint or eight metres, whichever is larger. Neither needed a shape of its
        /// own: a sand plane and a water ceiling that reach past the glass are exactly what a
        /// camera inside a cylinder should see, and the glass itself is drawn by
        /// <see cref="WaterBounds.ShowTank"/>.
        /// </para>
        /// <para>
        /// ⚠ <b>The snow and the shafts fill the square, not the circle.</b> The particle system's
        /// volume and the shaders' clip box are the bounds given here, so about a fifth of the
        /// motes and the ends of some shafts hang in the four corners, outside the water the run
        /// actually had. It is named rather than hidden: both are atmosphere with no census in
        /// them, and clipping them to a cylinder means a radius in
        /// <c>TheatreSnow.shader</c> and in <c>TheatreShafts.shader</c>, which is a shader change
        /// for a corner of fog. Nothing that is read off a picture — a body, a marker, the
        /// outline, the bed — is affected.
        /// </para>
        /// </remarks>
        /// <param name="box">
        /// The world's box, the same one <see cref="SnapshotCamera"/> frames: x and z are the
        /// footprint, y runs from the floor to the waterline at zero. In a tank it is the
        /// circle's bounding square.
        /// </param>
        /// <param name="bed">
        /// The floor's shape — <c>World.Bed</c> from the world the replay built, or null for the
        /// flat bed, which is every recording before D092. The sand is draped on this rather than
        /// on a map rebuilt here, so the drawing and the collider are the same height field and a
        /// picture cannot show a floor the physics does not have
        /// (<c>logbook/specs/bed-spec.md</c> item 13).
        /// </param>
        /// <param name="tankRadius">
        /// The tank's radius when the world is one (D089), for the glass; zero for a box or a
        /// solo creature, which get no wall.
        /// </param>
        /// <param name="reefs">
        /// The mushroom reefs of the world the frame belongs to (logbook/specs/reef-spec.md), or
        /// null with none, which is every recording before them. Drawn from the same geometry the
        /// grid's mask and the contacts read, so the rock in the picture is the rock in the run.
        /// </param>
        /// <param name="coral">
        /// Round 62's branching coral (logbook/specs/coral-build.md), or null with none. Drawn from
        /// the placed colonies' own pieces, beside the mushroom reefs and in the same rock; off with
        /// <c>EVOSIM_THEATRE_CORAL=0</c>.
        /// </param>
        public void Dress(Bounds box, BedShape bed = null, float tankRadius = 0f, ReefGeometry reefs = null, CoralRock coral = null)
        {
            Undress();
            EnsureRoot();

            Vector3 min = box.min;
            Vector3 size = box.size;
            _waterlineY = min.y + size.y;

            // Again here, because the sea's dials are public fields a caller can set between
            // constructing the skin and dressing a run, and because the shafts below are built
            // along the refracted ray this works out.
            PushWater();

            // The water's extent, for the underwater look's item 2 (TheatreWater.hlsl,
            // EvoWaterPathMetres): absorption and in-scatter run over the part of a look inside
            // the water only, so a camera outside the glass sees through air. Two metres of
            // margin under the floor, where a shaped bed's hollows reach below the box.
            _waterLow = new Vector3(min.x, min.y - 2f, min.z);
            _waterHigh = new Vector3(min.x + size.x, _waterlineY, min.z + size.z);
            _waterTank = new Vector4(min.x + 0.5f * size.x, min.z + 0.5f * size.z, tankRadius, tankRadius > 0f ? 1f : 0f);
            _waterKnown = true;

            Shader.SetGlobalVector("_EvoWaterLow", new Vector4(min.x, min.y - 2f, min.z, 1f));
            Shader.SetGlobalVector("_EvoWaterHigh", new Vector4(min.x + size.x, _waterlineY, min.z + size.z, 0f));
            Shader.SetGlobalVector("_EvoWaterTank", new Vector4(
                min.x + 0.5f * size.x, min.z + 0.5f * size.z, tankRadius, tankRadius > 0f ? 1f : 0f));

            BuildBed(min, size, bed);
            BuildEarth(min, size, bed, tankRadius);
            BuildSurface(min, size);

            _hasGround = true;
            _groundBed = bed;
            _groundBox = box;
            _groundRadius = tankRadius;
            BuildShafts(min, size);
            BuildSnow(min, size);

            // The underwater look's replacements for the two above (items 4 and 5), built only
            // when their switches are on; the quads and the particle snow are built either way,
            // because a census view holds today's look and draws them.
            BuildMarch(min, size, tankRadius, reefs);
            BuildShells(min, size, tankRadius);
            ApplyFurnitureSwitches();

            if (tankRadius > 0f) BuildGlass(min, size, tankRadius);
            if (reefs != null && reefs.Count > 0) BuildReefs(min, bed, reefs);
            if (coral != null && coral.Count > 0) BuildCoral(coral);
        }

        /// <summary>
        /// The mushroom reefs (logbook/specs/reef-spec.md §2) as rock: each a cap and a stem cut
        /// inward by noise in the reef's own frame, seeded per reef (<see cref="TheatreReefRock"/>),
        /// under one rock material in the bed's family (<c>TheatreRock.shader</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One contract.</b> Everything here reads a reef through <see cref="ReefLook.From"/> and
        /// nothing else from the geometry, so D118's per-reef sizes and outlines arrive by one
        /// method's edit there.
        /// </para>
        /// <para>
        /// <b>Inside the physics' rock.</b> The meshes start on the analytic surface
        /// <see cref="ReefGeometry.SignedDistance(double, double, double)"/> describes, the fillet
        /// under the cap included, and are cut only inward; the shader carves nothing. A body
        /// touching the rock can appear to stand a little off it, never to sink into it.
        /// </para>
        /// </remarks>
        private void BuildReefs(Vector3 min, BedShape bed, ReefGeometry reefs)
        {
            if (_reefRockMaterial == null) _reefRockMaterial = MakeReefMaterial("Theatre Reef Rock");
            if (_reefRockMaterial == null) return;

            var block = new MaterialPropertyBlock();

            for (int i = 0; i < reefs.Count; i++)
            {
                ReefLook look = ReefLook.From(reefs, i);
                float x = look.CentreX;
                float z = look.CentreZ;
                float middle = look.MidY;
                float half = 0.5f * look.CapThickness;

                var holder = new GameObject("Theatre Reef " + i) { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(_root.transform, false);
                holder.transform.position = new Vector3(x, middle, z);
                _reefs.Add(holder);

                block.Clear();
                block.SetFloat("_CapTopLocal", half);
                block.SetFloat("_CapUnderLocal", -half);
                block.SetFloat("_CapRadius", look.MaxRadius());
                block.SetFloat("_RockSeed", look.SeedUnit);

                Mesh cap = TheatreReefRock.Cap(look);
                _reefMeshes.Add(cap);
                AddPiece(holder, "Cap", cap, _reefRockMaterial, block);

                if (look.StemRadius > 0f)
                {
                    // From a metre under the floor at the axis (or the box's floor, whichever is
                    // deeper) to the cap's mid-plane, so no relief shows a gap at the foot.
                    float floor = bed != null && bed.HasRelief ? (float)bed.FloorY(x, z) : min.y;
                    float bottom = Mathf.Min(floor, min.y) - 1f - middle;

                    Mesh stem = TheatreReefRock.Stem(look, bottom);
                    _reefMeshes.Add(stem);
                    AddPiece(holder, "Stem", stem, _reefRockMaterial, block);
                }
            }
        }

        /// <summary>
        /// Round 62's branching coral as rock: one holder and one mesh a colony
        /// (<see cref="TheatreCoralRock"/>), at the colony's trunk foot, under the reefs' rock
        /// material. Tracked in the reefs' lists, so <see cref="Undress"/> drops it with them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The rock shader's cap properties, read for a tree.</b> The shader tints by height
        /// between <c>_CapUnderLocal</c> and <c>_CapTopLocal</c> (deep rock at the bottom, mid
        /// rock at the top, the crust on what faces up in the upper two thirds) and puts a cap's
        /// shadow below <c>_CapUnderLocal</c>. Per colony, in its own frame: the under is 0, the
        /// trunk's foot, so nothing above the bed sits under a shadow and only a branch's
        /// underside darkens, by its normal; the top is the colony's highest surface, so the
        /// tone climbs from a dark foot to a lighter crown and the crust settles on the plates
        /// and the upper branches; the radius is the colony's farthest horizontal reach, which
        /// the shader reads only for the shadow it never draws here; the seed is the colony's.
        /// </para>
        /// <para>
        /// Coordinates: a piece is in the run's metres in the tank frame, which is the frame the
        /// reefs and the bodies are drawn in (<see cref="BuildReefs"/> places a holder at
        /// <c>(CentreX, MidY, CentreZ)</c> with no offset), so a holder goes at
        /// <c>(X, BaseY, Z)</c> and no <c>min</c> is subtracted.
        /// </para>
        /// </remarks>
        private void BuildCoral(CoralRock rock)
        {
            if (!TheatreCoralRock.Enabled) return;

            if (_reefRockMaterial == null) _reefRockMaterial = MakeReefMaterial("Theatre Reef Rock");
            if (_reefRockMaterial == null) return;

            CoralGeometry coral = rock.Coral;
            var block = new MaterialPropertyBlock();
            int vertices = 0;
            float living = TheatreCoralRock.Living;
            float polyp = TheatreCoralRock.PolypMetres;
            float shape = TheatreCoralRock.Shape;
            float warm = TheatreCoralRock.Warm;

            // The further looks (each dial at 2): set only then, so at 0 and 1 the block and the
            // mesh are what they were.
            bool shapeFurther = shape > 0f && TheatreCoralRock.ShapeFurther;
            bool livingFurther = living > 0f && TheatreCoralRock.LivingFurther;

            // The bold looks (each dial at 3), laid over the further ones: set only then, so at 0 to 2
            // the block and the mesh are what they were.
            bool shapeBold = shapeFurther && TheatreCoralRock.ShapeBold;
            bool livingBold = livingFurther && TheatreCoralRock.LivingBold;

            // The polyps out, on living tissue only: set only then, so with the dial off the block is
            // what it was.
            bool tufts = living > 0f && TheatreCoralRock.PolypTufts;

            for (int c = 0; c < coral.Count; c++)
            {
                CoralGeometry.Colony colony = coral.ColonyAt(c);

                Mesh mesh = TheatreCoralRock.Colony(coral, c, out float top, out float reach, shape, shapeFurther, livingFurther, shapeBold);
                vertices += mesh.vertexCount;
                _reefMeshes.Add(mesh);

                var holder = new GameObject("Theatre Coral " + c) { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(_root.transform, false);
                holder.transform.position = new Vector3((float)colony.X, (float)colony.BaseY, (float)colony.Z);
                _reefs.Add(holder);

                block.Clear();
                block.SetFloat("_CapTopLocal", top);
                block.SetFloat("_CapUnderLocal", 0f);
                block.SetFloat("_CapRadius", reach);
                block.SetFloat("_RockSeed", TheatreCoralRock.SeedUnit(c));

                if (shapeFurther)
                {
                    block.SetFloat("_CoralForm", 1f);
                    block.SetFloat("_BumpMetres", TheatreCoralRock.BumpMetres);
                }

                if (living > 0f)
                {
                    TheatreCoralRock.LivingTint(c, out Vector4 tissue, out Vector4 tip);
                    if (livingFurther) TheatreCoralRock.PaletteTint(c, out tissue, out tip);
                    if (livingBold) TheatreCoralRock.VividTint(c, out tissue, out tip);
                    block.SetFloat("_Living", living);
                    block.SetVector("_LiveTint", tissue);
                    block.SetVector("_LiveTip", tip);
                    block.SetFloat("_PolypMetres", polyp);
                    if (warm > 0f)
                    {
                        Vector4 crown = TheatreCoralRock.WarmTint(c);
                        crown.w = warm;
                        block.SetVector("_LiveWarm", crown);
                    }

                    if (livingFurther)
                    {
                        block.SetFloat("_Bare", 1f);
                        block.SetVector("_BareTint", TheatreCoralRock.BareTint);
                        block.SetFloat("_BareEdge", TheatreCoralRock.BareEdge(c));
                    }

                    if (livingBold) block.SetFloat("_Vivid", 1f);

                    if (tufts) block.SetFloat("_Polyps", 1f);
                }

                AddPiece(holder, "Colony", mesh, _reefRockMaterial, block);
            }

            Debug.Log(TheatreCoralRock.Summary(coral, vertices));
            Debug.Log(
                "[Theatre] coral living " + living.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                (living > 0f
                    ? " (gain " + TheatreCoralRock.LivingGain.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                      ", corallite " + polyp.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m" +
                      ", warm crowns " + warm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")"
                    : " (bare stone)") +
                "; shape " + shape.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                (shape > 0f ? " (lobed plates, forked knobbly tips)" : " (the physics' pieces)") +
                (shapeFurther ? "; further shape (tapered branches, axial polyps, thin rims, bumps, rings and ridges)" : "") +
                (shapeBold ? "; bold shape (a bush: thin cores, sprigs, pronged tips, a mat on every plate)" : "") +
                (livingFurther ? "; palette per colony and bare skeleton below" : "") +
                (livingBold ? "; bright living tissue with pale tips" : ""));
        }

        private static void AddPiece(GameObject holder, string name, Mesh mesh, Material material, MaterialPropertyBlock block)
        {
            var piece = new GameObject(name) { hideFlags = HideFlags.DontSave };
            piece.transform.SetParent(holder.transform, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (block != null) renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// The rock's material (<c>TheatreRock.shader</c>), its colours given in sRGB here and
        /// handed over as linear vectors, so the engine converts nothing (CLAUDE.md's linear
        /// colour gotcha). Falls back to the bed's material at a carve of zero if the rock's
        /// shader is missing.
        /// </summary>
        private Material MakeReefMaterial(string name)
        {
            Shader shader = Shader.Find("Evosim/Theatre Rock");
            if (shader == null)
            {
                Material fallback = MakeBedMaterial();
                if (fallback == null) return null;
                fallback.name = name;
                if (fallback.HasProperty("_BedCarveMetres")) fallback.SetFloat("_BedCarveMetres", 0f);
                return fallback;
            }

            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };

            // Measured against the bed's sand (0.012 shaded, 0.066 lit, sRGB): the underside and
            // the stem a little under the sand and colder, the rim about the sand, the crust on the
            // table two to three times it, still grey so the guilds keep the colour.
            material.SetVector("_RockDeep", LinearOf(RockDeep));
            material.SetVector("_RockMid", LinearOf(RockMid));
            material.SetVector("_RockCrust", LinearOf(RockCrust));
            material.SetVector("_CausticColor", LinearOf(new Color(0.55f, 0.85f, 0.95f, 1f)));
            material.SetFloat("_CausticReach", Mathf.Clamp(SurfaceLightMetres, 1f, 60f));
            material.SetFloat("_GlintStrength", Mathf.Clamp(Look.RockGlint, 0f, 0.5f));
            material.SetFloat("_GlintRoughness", Mathf.Clamp(Look.RockGlintRoughness, 0.05f, 1f));
            return material;
        }

        /// <summary>The rock's three tones, sRGB. See <see cref="MakeReefMaterial"/>.</summary>
        public Color RockDeep = new Color(0.050f, 0.058f, 0.070f, 1f);
        public Color RockMid = new Color(0.100f, 0.100f, 0.102f, 1f);
        public Color RockCrust = new Color(0.215f, 0.205f, 0.180f, 1f);

        private static Vector4 LinearOf(Color colour)
        {
            Color linear = colour.linear;
            return new Vector4(linear.r, linear.g, linear.b, 1f);
        }

        /// <summary>
        /// The tank's wall as a faint Fresnel sheet (<c>TheatreGlass.shader</c>; the look's
        /// design pass, E3): a cylinder of 96 facets at the tank's radius from the floor to a
        /// little over the waterline, drawn only for a viewer inside the water, so the census
        /// views from outside are not touched by it.
        /// </summary>
        private void BuildGlass(Vector3 min, Vector3 size, float radius)
        {
            Material material =
                _glassMaterial != null ? _glassMaterial : (_glassMaterial = MakeGlassMaterial());

            if (material == null) return;

            const int facets = 96;
            float bottom = min.y;
            float top = min.y + size.y + 0.3f;

            var vertices = new Vector3[2 * (facets + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[6 * facets];

            for (int i = 0; i <= facets; i++)
            {
                float angle = 2f * Mathf.PI * i / facets;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[2 * i] = outward * radius + new Vector3(0f, bottom, 0f);
                vertices[2 * i + 1] = outward * radius + new Vector3(0f, top, 0f);
                normals[2 * i] = outward;
                normals[2 * i + 1] = outward;

                if (i == facets) continue;

                int t = 6 * i;
                triangles[t] = 2 * i; triangles[t + 1] = 2 * i + 1; triangles[t + 2] = 2 * i + 2;
                triangles[t + 3] = 2 * i + 1; triangles[t + 4] = 2 * i + 3; triangles[t + 5] = 2 * i + 2;
            }

            var mesh = new Mesh { name = "Theatre Glass", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            _glass = new GameObject("Theatre Glass") { hideFlags = HideFlags.DontSave };
            _glass.transform.SetParent(_root.transform, false);
            _glass.transform.position = new Vector3(min.x + 0.5f * size.x, 0f, min.z + 0.5f * size.z);
            _glass.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = _glass.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _glass.AddComponent<TheatreInsideOnly>();
        }

        private void BuildBed(Vector3 min, Vector3 size, BedShape bed)
        {
            Material material = _bedMaterial != null ? _bedMaterial : (_bedMaterial = MakeBedMaterial());
            if (material == null) return;

            // The bed reaches a little past the box on the horizontal axes, so the sand runs under
            // the box's own edge lines rather than stopping short of them and leaving a bright
            // line of background where the floor should be.
            float overhang = 0.02f * Mathf.Max(size.x, size.z);

            var centre = new Vector3(
                min.x + 0.5f * size.x, min.y, min.z + 0.5f * size.z);

            _bed = new GameObject("Theatre Bed") { hideFlags = HideFlags.DontSave };
            _bed.transform.SetParent(_root.transform, false);
            _bed.transform.position = centre;

            // D092: the sand takes the world's own height map where there is one, at the same
            // 0.2 m pitch it always drew, and the flat quad otherwise — which is every recording
            // before the shaped bed and has to keep drawing exactly as it did.
            _bed.AddComponent<MeshFilter>().sharedMesh =
                bed != null && bed.HasRelief
                    ? ShapedBed(bed, centre, size.x + overhang, size.z + overhang)
                    : Bed(size.x + overhang, size.z + overhang);

            MeshRenderer renderer = _bed.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // How far below the surface the caustics still reach on the sand. In a shallow box
            // they land on the bed; in a sixty metre column they do not, which is correct: the
            // light is gone long before the floor is (DESIGN's light model).
            //
            // Off the skin's own dial since the fourth day, rather than a fraction of the box's
            // depth. The shafts, the bodies' caustics, the sand's and the window's brightness now
            // all fade on one number, and a second rule here would have made the sand the one
            // surface in the picture that disagreed about where the light stops.
            material.SetFloat("_CausticReach", Mathf.Clamp(SurfaceLightMetres, 1f, 60f));
        }

        /// <summary>
        /// Whether a place is over the world's floor, and the floor's height there: inside the
        /// tank's circle (or the box's footprint), the bed's height, or the box's bottom where the
        /// bed is flat. The free-fly camera keeps above it (<see cref="TheatreCamera"/>).
        /// </summary>
        public bool GroundUnder(float x, float z, out float floorY)
        {
            floorY = 0f;
            if (!_hasGround) return false;

            Vector3 c = _groundBox.center;
            if (_groundRadius > 0f)
            {
                float dx = x - c.x;
                float dz = z - c.z;
                if (dx * dx + dz * dz > _groundRadius * _groundRadius) return false;
            }
            else if (x < _groundBox.min.x || x > _groundBox.max.x || z < _groundBox.min.z || z > _groundBox.max.z)
            {
                return false;
            }

            floorY = _groundBed != null && _groundBed.HasRelief
                ? (float)_groundBed.FloorY(x, z)
                : _groundBox.min.y;
            return true;
        }

        /// <summary>
        /// The earth under a shaped bed (the owner asked on 2026-09-24 that the ground
        /// be solid earth below, not more water). A shaped bed is drawn as a surface, so a view
        /// through the glass from outside saw water under the beach and the reef stems standing
        /// down into it. This is a wall at the glass, just inside it, from the bed's height at
        /// the rim down past the box's bottom, in the bed's own shader in darker, browner tones
        /// and with its downward carve off: from outside, a cut face of sediment under the sand.
        /// A flat bed is the box's bottom and has nothing under it, so it draws none.
        /// </summary>
        private void BuildEarth(Vector3 min, Vector3 size, BedShape bed, float tankRadius)
        {
            if (!Earth || bed == null || !bed.HasRelief || tankRadius <= 0f) return;

            Material material = _earthMaterial != null ? _earthMaterial : (_earthMaterial = MakeEarthMaterial());
            if (material == null) return;

            const int segments = 256;
            float r = tankRadius - 0.03f;
            float bottom = min.y - 0.5f;
            var centre = new Vector3(min.x + 0.5f * size.x, 0f, min.z + 0.5f * size.z);

            var vertices = new List<Vector3>(4 * (segments + 1));
            var normals = new List<Vector3>(4 * (segments + 1));
            var tops = new List<Vector2>(4 * (segments + 1));
            var triangles = new List<int>(12 * segments);

            // Two faces: the outer seen from outside the glass, the inner for a camera at the
            // glass looking along it. Each its own vertices, so each has its own normal.
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                float sign = face == 0 ? 1f : -1f;

                for (int i = 0; i <= segments; i++)
                {
                    float angle = 2f * Mathf.PI * i / segments;
                    float cx = Mathf.Cos(angle);
                    float sz = Mathf.Sin(angle);
                    float x = centre.x + r * cx;
                    float z = centre.z + r * sz;
                    float top = (float)bed.FloorY(x, z);

                    vertices.Add(new Vector3(x, top, z));
                    vertices.Add(new Vector3(x, bottom, z));
                    normals.Add(new Vector3(sign * cx, 0f, sign * sz));
                    normals.Add(new Vector3(sign * cx, 0f, sign * sz));

                    // The bed's height over the column, so the face knows how far under the
                    // sand each of its points is.
                    tops.Add(new Vector2(top, 0f));
                    tops.Add(new Vector2(top, 0f));
                }

                for (int i = 0; i < segments; i++)
                {
                    int a = start + 2 * i;
                    int b = a + 1;
                    int c = a + 2;
                    int d = a + 3;

                    if (face == 0)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            var mesh = new Mesh { name = "Theatre Earth", hideFlags = HideFlags.DontSave };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, tops);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            _earth = new GameObject("Theatre Earth") { hideFlags = HideFlags.DontSave };
            _earth.transform.SetParent(_root.transform, false);
            _earth.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = _earth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The earth's material: its own shader (TheatreEarth.shader says why the bed's drew the
        /// face black), or none, in which case no face is drawn rather than a black one.
        /// </summary>
        private Material MakeEarthMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Earth");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Earth",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>
        /// Puts the ceiling on the water: one rippling quad at the waterline, seen from below.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why it reaches well past the box.</b> From three metres under, a camera looking up
        /// through a wide lens sees ten metres of surface in every direction, and the campaign's
        /// water is five metres across. A ceiling that stopped at the wall would put the empty
        /// background where the sky should be and would read as a hole rather than as the edge of
        /// a box. The margin is generous for that reason, and it costs nothing: the plane is
        /// hidden from every eye above the waterline anyway.
        /// </para>
        /// <para>
        /// <b>Hidden from above three times.</b> The mesh is wound so its front face points down,
        /// so a camera above it culls the whole quad; the shader discards again for any eye above
        /// the waterline (<c>TheatreWater.hlsl</c>, <c>EvoSeenFromBelow</c>); and the renderer is
        /// marked <see cref="TheatreInsideOnly"/>, which turns it off outright for the four
        /// snapshot views that photograph the census from outside the box. The fourth day's
        /// requirement that those views are unchanged is too much to hang on one culling flag,
        /// and two of the four cameras stand under the waterline, where the shader's own test
        /// cannot refuse them.
        /// </para>
        /// </remarks>
        private void BuildSurface(Vector3 min, Vector3 size)
        {
            Material material =
                _surfaceMaterial != null ? _surfaceMaterial : (_surfaceMaterial = MakeSurfaceMaterial());

            if (material == null) return;

            float margin = Mathf.Max(8f, 0.5f * Mathf.Max(size.x, size.z));
            float waterline = min.y + size.y;

            _surface = new GameObject("Theatre Surface") { hideFlags = HideFlags.DontSave };
            _surface.transform.SetParent(_root.transform, false);
            _surface.transform.position = new Vector3(
                min.x + 0.5f * size.x, waterline, min.z + 0.5f * size.z);

            _surface.AddComponent<MeshFilter>().sharedMesh = SurfaceMesh(
                size.x + 2f * margin, size.z + 2f * margin);

            MeshRenderer renderer = _surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Marked as something only the inside of the water sees, so that the four snapshot
            // views that photograph the census from outside the box can turn it off outright
            // (SnapshotCamera.HideWhatOnlyTheWaterSees).
            _surface.AddComponent<TheatreInsideOnly>();

            material.SetFloat("_Waterline", waterline);
            material.SetColor("_DeepColor", Water);
        }

        /// <summary>
        /// The surface's display mesh: a grid at the waterline, facing down.
        /// </summary>
        /// <remarks>
        /// Coarser than the bed's, because almost all of the shimmer is in the per pixel normal
        /// and the vertices only carry the few centimetres of wave that keep the ceiling from
        /// being a ruled line when it is seen edge on. The bounds are opened by a metre each way
        /// so that a displaced quad is not culled by a frustum test made against the flat plane.
        /// </remarks>
        private static Mesh SurfaceMesh(float length, float width)
        {
            Mesh mesh = Grid("Theatre Surface Grid", length, width, 0.5f, false);

            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 2f, 0f));
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// Hangs a handful of light shafts under the sun.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>They run along the refracted ray, not along the sun.</b> A beam under water is the
        /// sun's light after the surface has bent it towards the vertical, and drawing it at the
        /// sun's own angle would have the beams and the caustic net they throw disagreeing by ten
        /// or fifteen degrees, which is exactly the kind of quiet wrongness a picture is read for.
        /// </para>
        /// <para>
        /// <b>A shaft stops where the water does.</b> The length is the distance to the box's own
        /// wall or floor along the ray, capped at the light's reach: a beam is water lit from
        /// above, so one hanging outside the box would be light in water that does not exist,
        /// which is the same fault the marine snow was clipped for. A draw whose beam would be a
        /// stub is thrown away and drawn again somewhere else.
        /// </para>
        /// <para>
        /// <b>Placed from a fixed seed.</b> Two pictures of the same second must be the same
        /// picture; scattering these from the frame clock would make every render of one run a
        /// different world.
        /// </para>
        /// </remarks>
        private void BuildShafts(Vector3 min, Vector3 size)
        {
            if (Shafts <= 0) return;

            Material material =
                _shaftMaterial != null ? _shaftMaterial : (_shaftMaterial = MakeShaftMaterial());

            if (material == null) return;

            Vector3 origin = new Vector3(
                min.x + 0.5f * size.x, min.y + size.y, min.z + 0.5f * size.z);

            Mesh mesh = ShaftMesh(min, size, origin, Mathf.Clamp(Shafts, 0, 32));
            if (mesh == null) return;

            _shafts = new GameObject("Theatre Shafts") { hideFlags = HideFlags.DontSave };
            _shafts.transform.SetParent(_root.transform, false);
            _shafts.transform.position = origin;

            _shafts.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = _shafts.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _shafts.AddComponent<TheatreInsideOnly>();

            material.SetFloat("_Waterline", origin.y);
        }

        /// <summary>The shafts, as one mesh of quads about the surface's own centre.</summary>
        /// <remarks>
        /// Each quad carries, besides its corners, where it leaves the surface and how bright it
        /// is, in a second UV channel. The shader reads the wave field at that point rather than
        /// at the pixel, because a beam's brightness is decided once, where it enters the water,
        /// and is the same all the way down it.
        /// </remarks>
        private Mesh ShaftMesh(Vector3 min, Vector3 size, Vector3 origin, int count)
        {
            var random = new System.Random(20260911);

            Vector3 low = min;
            Vector3 high = min + size;
            Vector3 ray = _sunRay;

            float reach = Mathf.Clamp(SurfaceLightMetres, 1f, 60f);

            var vertices = new List<Vector3>(4 * count);
            var normals = new List<Vector3>(4 * count);
            var across0 = new List<Vector2>(4 * count);
            var tops = new List<Vector4>(4 * count);
            var triangles = new List<int>(6 * count);

            Vector3 level = Vector3.Cross(ray, Vector3.up);
            if (level.sqrMagnitude < 1e-4f) level = Vector3.right;
            level = level.normalized;

            int made = 0;

            for (int attempt = 0; attempt < 8 * count && made < count; attempt++)
            {
                var top = new Vector3(
                    Mathf.Lerp(low.x, high.x, (float)random.NextDouble()),
                    high.y,
                    Mathf.Lerp(low.z, high.z, (float)random.NextDouble()));

                float length = Mathf.Min(reach, ExitDistance(top, ray, low, high));

                // A stub is not a beam. Drawn again somewhere else rather than kept, so a box
                // whose sun comes in steeply still gets its full handful.
                if (length < 3f) continue;

                // Turned about its own ray by a draw, so the slabs do not all face one way and
                // some of them read from wherever the camera happens to be.
                Vector3 across = Quaternion.AngleAxis(360f * (float)random.NextDouble(), ray) * level;
                Vector3 normal = Vector3.Cross(across, ray).normalized;

                float halfTop = 0.18f + 0.45f * (float)random.NextDouble();
                float halfEnd = halfTop * (1.5f + 0.9f * (float)random.NextDouble());
                float brightness = 0.45f + 0.55f * (float)random.NextDouble();

                Vector3 end = top + ray * length;
                int first = vertices.Count;

                vertices.Add(top - across * halfTop - origin);
                vertices.Add(top + across * halfTop - origin);
                vertices.Add(end - across * halfEnd - origin);
                vertices.Add(end + across * halfEnd - origin);

                for (int corner = 0; corner < 4; corner++)
                {
                    normals.Add(normal);
                    tops.Add(new Vector4(top.x, top.z, brightness, 0f));
                }

                across0.Add(new Vector2(0f, 0f));
                across0.Add(new Vector2(1f, 0f));
                across0.Add(new Vector2(0f, 1f));
                across0.Add(new Vector2(1f, 1f));

                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first + 2); triangles.Add(first + 1); triangles.Add(first + 3);

                made++;
            }

            if (made == 0) return null;

            var mesh = new Mesh { name = "Theatre Shafts", hideFlags = HideFlags.DontSave };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, across0);
            mesh.SetUVs(1, tops);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>How far a ray travels from a point inside a box before it leaves it.</summary>
        private static float ExitDistance(Vector3 from, Vector3 direction, Vector3 low, Vector3 high)
        {
            float distance = Slab(from.x, direction.x, low.x, high.x);
            distance = Mathf.Min(distance, Slab(from.y, direction.y, low.y, high.y));
            distance = Mathf.Min(distance, Slab(from.z, direction.z, low.z, high.z));

            return distance;
        }

        /// <summary>Where a ray leaves one pair of walls. Infinite when it runs between them.</summary>
        private static float Slab(float from, float direction, float low, float high)
        {
            if (Mathf.Abs(direction) < 1e-5f) return float.MaxValue;

            float one = (low - from) / direction;
            float two = (high - from) / direction;

            return Mathf.Max(one, two);
        }

        /// <summary>
        /// The bed's display mesh: a grid in the xz plane, facing up, centred on its own origin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A grid and no longer a quad.</b> The bed shader now cuts the surface downward by a
        /// low frequency field, and a displacement can only move vertices it has: four of them
        /// describe a plane whatever the field says. The spacing is set from the box rather than
        /// fixed, so a run with a different footprint gets the same size of ripple.
        /// </para>
        /// <para>
        /// <b>Nothing here is a size claim.</b> The vertices sit exactly on the collider's top
        /// plane and the shader only ever subtracts from that, so the drawn bed is at or below
        /// the sea floor and a body resting on it can appear to hover a little and never to sink.
        /// </para>
        /// </remarks>
        private static Mesh Bed(float length, float width)
        {
            // About a fifth of a metre between vertices, which is a twenty fifth of the shader's
            // five metre lobe.
            Mesh mesh = Grid("Theatre Bed Grid", length, width, 0.2f, true);

            // The shader cuts the surface down and the bounds have to know, or the bed is culled
            // from a low camera the moment its flat plane leaves the frustum.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 8f, 0f));
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// The sand draped on the world's own height map — D092,
        /// <c>logbook/specs/bed-spec.md</c> item 13.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The same map the collider has, at the theatre's own pitch.</b> The grid is the flat
        /// bed's 0.2 m lattice, which is two and a half times finer than the collider's half
        /// metre, so the drawing is smoother than the rock without ever being a different shape:
        /// both are samples of one closed-form function
        /// (<c>BedShape.HeightAndGradient</c>), not two approximations of each other.
        /// </para>
        /// <para>
        /// <b>Normals from the gradient rather than from the triangles.</b> The map's slope is
        /// exact and a face normal is a difference of two of its samples, and the shader's ripple
        /// tilts whatever normal it is handed (<c>TheatreBed.shader</c>) — so a per-vertex normal
        /// taken from <c>∇h</c> is both cheaper and smoother than recalculating them.
        /// </para>
        /// <para>
        /// <b>Still safely inside the rock.</b> The shader only ever subtracts from the vertex it
        /// is given, so the drawn sand is at or below the collider's surface wherever the two
        /// agree, which is everywhere: the vertices are <c>FloorY</c> itself.
        /// </para>
        /// </remarks>
        private static Mesh ShapedBed(BedShape bed, Vector3 centre, float length, float width)
        {
            // The flat bed's spacing and the flat bed's cap, so a shaped world is drawn at the
            // same density as the flat one it is compared against.
            const float metres = 0.2f;
            const int most = 220;

            int nx = Mathf.Clamp(Mathf.RoundToInt(length / metres), 1, most);
            int nz = Mathf.Clamp(Mathf.RoundToInt(width / metres), 1, most);

            float halfX = 0.5f * length;
            float halfZ = 0.5f * width;

            var vertices = new List<Vector3>((nx + 1) * (nz + 1));
            var normals = new List<Vector3>((nx + 1) * (nz + 1));
            var triangles = new List<int>(nx * nz * 6);

            for (int j = 0; j <= nz; j++)
            {
                float localZ = Mathf.Lerp(-halfZ, halfZ, (float)j / nz);

                for (int i = 0; i <= nx; i++)
                {
                    float localX = Mathf.Lerp(-halfX, halfX, (float)i / nx);

                    bed.HeightAndGradient(
                        centre.x + localX, centre.z + localZ,
                        out double height, out double slopeX, out double slopeZ);

                    // The object stands at the box's floor, so a world height becomes a local one
                    // by subtracting where the object is.
                    var y = (float)(-(double)bed.DepthMetres + height) - centre.y;

                    vertices.Add(new Vector3(localX, y, localZ));
                    normals.Add(new Vector3((float)-slopeX, 1f, (float)-slopeZ).normalized);
                }
            }

            int stride = nx + 1;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = "Theatre Bed Map", hideFlags = HideFlags.DontSave };
            mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            // The shader cuts the surface down, as it does for the flat bed, and the bounds have
            // to allow for it or a low camera culls the sand the moment the plane leaves frame.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 8f, 0f));
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// A flat grid in the xz plane, centred on its own origin, facing up or down.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two surfaces are built from this: the sea bed, which faces up and is cut downward by
        /// its shader, and the sea's own surface, which faces down and is seen from underneath.
        /// One builder for both, because the only differences are the spacing and which way the
        /// triangles are wound, and a second copy of a grid is a second place for a winding to be
        /// got wrong.
        /// </para>
        /// <para>
        /// <b>The winding is what hides the ceiling from above.</b> Unity takes a triangle's
        /// front face from the order of its vertices, so the downward grid's triangles are wound
        /// the other way round and its normals point down: a camera above it sees only back faces
        /// and <c>Cull Back</c> throws them away. That is the fourth day's requirement that the
        /// snapshot's top view still sees the world, made out of the mesh rather than argued for.
        /// </para>
        /// </remarks>
        private static Mesh Grid(string name, float length, float width, float metres, bool up)
        {
            // Capped so that a very large box cannot ask for a million vertices.
            const int most = 220;

            int nx = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(0.01f, metres)), 1, most);
            int nz = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(0.01f, metres)), 1, most);

            float x = 0.5f * length;
            float z = 0.5f * width;

            var vertices = new List<Vector3>((nx + 1) * (nz + 1));
            var normals = new List<Vector3>((nx + 1) * (nz + 1));
            var triangles = new List<int>(nx * nz * 6);

            for (int j = 0; j <= nz; j++)
            {
                float pz = Mathf.Lerp(-z, z, (float)j / nz);

                for (int i = 0; i <= nx; i++)
                {
                    vertices.Add(new Vector3(Mathf.Lerp(-x, x, (float)i / nx), 0f, pz));
                    normals.Add(up ? Vector3.up : Vector3.down);
                }
            }

            int stride = nx + 1;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    if (up)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };

            // A grid this size passes 65,535 vertices at the cap, and the default index format
            // would wrap silently rather than refuse.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        private void BuildSnow(Vector3 min, Vector3 size)
        {
            Material material =
                _snowMaterial != null ? _snowMaterial : (_snowMaterial = MakeSnowMaterial());

            if (material == null) return;

            // The water the shader will let a mote be drawn in. Belt and braces with the shape
            // module: see the note in TheatreSnow.shader on the leak the top view found.
            material.SetVector("_BoxMin", min);
            material.SetVector("_BoxMax", min + size);

            var go = new GameObject("Theatre Snow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(
                min.x + 0.5f * size.x, min.y + 0.5f * size.y, min.z + 0.5f * size.z);

            _snow = go.AddComponent<ParticleSystem>();

            // A particle system added at runtime is already playing when AddComponent returns,
            // and the duration cannot be set on a playing system: Unity logs an error and keeps
            // the default. The snow therefore never took its lifetime as its duration until
            // this stop was added (seen in the owner's console on 2026-09-11). Stopped and
            // cleared here, configured, then played once below.
            _snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float lifetime = Mathf.Max(1f, SnowSecondsOfLife);

            ParticleSystem.MainModule main = _snow.main;
            main.duration = lifetime;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = SnowSizeMetres;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 1f));
            main.maxParticles = Mathf.Max(1, SnowMotes);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // Prewarmed, so the first picture of a run is of a seeded volume and not of an empty
            // one filling from the top. A picture taken at t = 1000 s of an empty water column
            // would be a picture of the particle system's age, not the world's.
            main.prewarm = true;

            ParticleSystem.EmissionModule emission = _snow.emission;
            emission.rateOverTime = Mathf.Max(1f, SnowMotes / Mathf.Max(0.001f, lifetime));

            ParticleSystem.ShapeModule shape = _snow.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            ParticleSystem.VelocityOverLifetimeModule velocity = _snow.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;

            // All three axes given the same kind of curve. Setting only y left x and z as plain
            // constants, and the module logs "Particle Velocity curves must all be in the same
            // mode" once a frame for the length of the run. The sideways numbers are the gentle
            // drift the note asks for, a tenth of the fall.
            float drift = 0.1f * SnowFallMetresPerSecond;

            velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);
            velocity.y = new ParticleSystem.MinMaxCurve(
                -SnowFallMetresPerSecond * 1.4f, -SnowFallMetresPerSecond * 0.5f);

            ParticleSystem.NoiseModule noise = _snow.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.05f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = -1;

            _snow.Play();
        }

        /// <summary>Takes the bed, the sea, the shafts and the snow away. Lights and fog stay.</summary>
        public void Undress()
        {
            Discard(_bed);
            _bed = null;

            Discard(_earth);
            _earth = null;
            _hasGround = false;

            Discard(_surface);
            _surface = null;

            Discard(_shafts);
            _shafts = null;

            if (_snow != null) Discard(_snow.gameObject);
            _snow = null;

            foreach (GameObject volume in new[] { _march, _shells })
            {
                if (volume == null) continue;

                var filter = volume.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Discard(filter.sharedMesh);
                Discard(volume);
            }
            _march = null;
            _shells = null;

            if (_glass != null)
            {
                var filter = _glass.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Discard(filter.sharedMesh);
                Discard(_glass);
            }
            _glass = null;

            foreach (GameObject reef in _reefs) Discard(reef);
            _reefs.Clear();

            foreach (Mesh mesh in _reefMeshes) Discard(mesh);
            _reefMeshes.Clear();
        }

        /// <summary>
        /// Puts the render settings back and drops everything this made. Called when the theatre
        /// component goes away, so an Editor session is not left with the theatre's fog on.
        /// </summary>
        public void Dispose()
        {
            if (Current == this) Current = null;
            Undress();

            Discard(_root);
            _root = null;
            _key = null;
            _fill = null;

            Discard(_body); _body = null;
            Discard(_neck); _neck = null;
            Discard(_joint); _joint = null;
            Discard(_bedMaterial); _bedMaterial = null;
            Discard(_earthMaterial); _earthMaterial = null;
            Discard(_snowMaterial); _snowMaterial = null;
            Discard(_surfaceMaterial); _surfaceMaterial = null;
            Discard(_shaftMaterial); _shaftMaterial = null;
            Discard(_backdropMaterial); _backdropMaterial = null;
            Discard(_glassMaterial); _glassMaterial = null;
            Discard(_reefRockMaterial); _reefRockMaterial = null;
            Discard(_marchMaterial); _marchMaterial = null;
            Discard(_shellMaterial); _shellMaterial = null;

            // The look's flags are globals and outlive the skin in an Editor session; a scene
            // opened after this one starts from today's look until its own skin says otherwise.
            if (Look.Any) Shader.SetGlobalVector("_EvoLook", Vector4.zero);
            if (Look.AnySecond)
            {
                Shader.SetGlobalVector("_EvoLook2", Vector4.zero);
                Shader.SetGlobalVector("_EvoSnowDrift", Vector4.zero);
                TheatreToneStep.Active = false;
            }
            if (_snowTexture != null) { UnityEngine.Object.DestroyImmediate(_snowTexture); _snowTexture = null; }
            if (AlgaBendStrength > 0f) Shader.SetGlobalVector("_EvoAlgaBend", Vector4.zero);
            // ARC begin
            if (AlgaArcLevel > 0) Shader.SetGlobalVector("_EvoAlgaArc", Vector4.zero);
            // ARC end
            Shader.SetGlobalVector("_EvoWaterLow", Vector4.zero);

            TheatreMeshes.Release();

            RestoreEnvironment();
        }

        // ---------------------------------------------------------------- materials

        private Material MakeBodyMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Body");

            // Falls back rather than failing. A missing shader must not take the viewer down with
            // it (WaterBounds' rule), and URP's own Lit reads the same _BaseColor the palette
            // sets, so the fallback is the plain look this replaces.
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var material = new Material(shader)
            {
                name = "Theatre Body",
                hideFlags = HideFlags.HideAndDontSave,
            };

            // The three dials that change what a body's outline is, and none of them can make a
            // body larger. The carve's displacement is negative by construction, the taper only
            // ever multiplies a cross-section by a number at most one, and the bend buys its
            // amplitude out of the cross-section before it spends it and is clamped into the
            // mesh's own box afterwards (TheatreBody.shader, Vertex and ShapeBox). So the meshes'
            // inset is still the whole of the size bound and these only decide how far inside it
            // the tissue sits. Clamped again here: the fields are public and a scene or a check
            // can set them without going through Dial.
            material.SetFloat("_CarveFraction", Mathf.Clamp(CarveFraction, 0f, 0.5f));
            material.SetFloat("_TaperFraction", Mathf.Clamp(TaperFraction, 0f, 0.8f));
            material.SetFloat("_BendFraction", Mathf.Clamp(BendFraction, 0f, 0.4f));
            material.SetFloat("_CurlFraction", Mathf.Clamp(CurlFraction, 0f, 0.1f));
            material.SetFloat("_VeinStrength", Mathf.Clamp01(VeinStrength));
            material.SetFloat("_LeafSkyGlow", Mathf.Clamp(LeafSkyGlow, 0f, 2f));

            // The one depth the surface's light reaches, the same number the sand, the shafts and
            // the window fade on. The net itself now comes from the sea overhead rather than from
            // a pattern of its own (TheatreWater.hlsl, EvoCausticNet), so this is all that is left
            // to say about it per material.
            material.SetFloat("_CausticReach", Mathf.Clamp(SurfaceLightMetres, 1f, 60f));

            return material;
        }

        /// <summary>
        /// The body material with its three outline dials on or off: off for the raw shapes
        /// (<see cref="TheatrePalette.RawShapes"/>), so a primitive drawn with it is the
        /// collider and nothing else.
        /// </summary>
        public void ShowRawShapes(bool raw)
        {
            Material material = BodyMaterial;
            if (material == null) return;

            material.SetFloat("_CarveFraction", raw ? 0f : Mathf.Clamp(CarveFraction, 0f, 0.5f));
            material.SetFloat("_TaperFraction", raw ? 0f : Mathf.Clamp(TaperFraction, 0f, 0.8f));
            material.SetFloat("_BendFraction", raw ? 0f : Mathf.Clamp(BendFraction, 0f, 0.4f));
        }

        private Material MakeNeckMaterial()
        {
            Material material = MakeBodyMaterial();
            if (material == null) return null;

            material.name = "Theatre Neck";

            // A neck is a marker, not tissue: no mottle, no caustics, a hard bright rim, and no
            // carve at all. It is already sized to fit inside the part it sits in, and a marker
            // with impressions cut into it would read as tissue.
            material.SetFloat("_MottleStrength", 0f);
            material.SetFloat("_CausticStrength", 0f);
            material.SetFloat("_CarveFraction", 0f);

            // A neck is drawn with the cylinder, which the shader never tapers or bends anyway;
            // set to zero so that the marker stays a marker if it is ever drawn with something
            // else.
            material.SetFloat("_TaperFraction", 0f);
            material.SetFloat("_BendFraction", 0f);
            material.SetFloat("_RimStrength", 2.6f);
            material.SetFloat("_RimPower", 1.6f);
            material.SetFloat("_GlowStrength", 0.5f);
            material.SetFloat("_TransScale", 0f);

            return material;
        }

        private Material MakeJointMaterial()
        {
            Material material = MakeBodyMaterial();
            if (material == null) return null;

            material.name = "Theatre Joint";

            // Tissue, not a marker: the body's mottle, rim and light through it are kept, so a
            // knuckle reads as the same flesh as the parts it joins. The taper and the bend are
            // off because they shape a part along its own long axis and a knuckle has none, and
            // the carve is halved so the joint reads as smoother skin stretched over a hinge.
            material.SetFloat("_TaperFraction", 0f);
            material.SetFloat("_BendFraction", 0f);
            material.SetFloat("_CarveFraction", 0.5f * Mathf.Clamp(CarveFraction, 0f, 0.5f));

            return material;
        }

        private Material MakeBedMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Bed");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Bed",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>The ceiling's material. Falls back to nothing rather than to a lit plane.</summary>
        /// <remarks>
        /// There is no fallback shader here, unlike the body's. URP's Lit on a downward facing
        /// quad at the waterline would be a flat grey lid over the world, which is worse than no
        /// ceiling at all: a viewer would read it as the surface and take its flatness for the
        /// water's. A missing shader leaves the world as it was on the third day.
        /// </remarks>
        private Material MakeGlassMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Glass");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Glass",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private Material MakeBackdropMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Backdrop");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Backdrop",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private Material MakeSurfaceMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Surface");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Surface",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        /// <summary>The shafts' material. Additive, and no fallback for the ceiling's reason.</summary>
        private Material MakeShaftMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Shafts");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Shafts",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private Material MakeSnowMaterial()
        {
            Shader shader = Shader.Find("Evosim/Theatre Snow");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "Theatre Snow",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // ---------------------------------------------------------------- plumbing

        private void EnsureRoot()
        {
            if (_root != null) return;

            _root = new GameObject("Theatre Skin") { hideFlags = HideFlags.DontSave };
        }

        private void SaveEnvironment()
        {
            if (_fogSaved) return;

            _fogWas = RenderSettings.fog;
            _fogModeWas = RenderSettings.fogMode;
            _fogColourWas = RenderSettings.fogColor;
            _fogDensityWas = RenderSettings.fogDensity;
            _ambientModeWas = RenderSettings.ambientMode;
            _ambientWas = RenderSettings.ambientLight;
            _ambientSkyWas = RenderSettings.ambientSkyColor;
            _ambientEquatorWas = RenderSettings.ambientEquatorColor;
            _ambientGroundWas = RenderSettings.ambientGroundColor;
            _skyboxWas = RenderSettings.skybox;
            _fogSaved = true;
        }

        private void RestoreEnvironment()
        {
            if (!_fogSaved) return;

            RenderSettings.fog = _fogWas;
            RenderSettings.fogMode = _fogModeWas;
            RenderSettings.fogColor = _fogColourWas;
            RenderSettings.fogDensity = _fogDensityWas;
            RenderSettings.ambientMode = _ambientModeWas;
            RenderSettings.ambientLight = _ambientWas;
            RenderSettings.ambientSkyColor = _ambientSkyWas;
            RenderSettings.ambientEquatorColor = _ambientEquatorWas;
            RenderSettings.ambientGroundColor = _ambientGroundWas;
            RenderSettings.skybox = _skyboxWas;
            _fogSaved = false;
        }

        private static void Discard(Object thing)
        {
            if (thing == null) return;

            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }
    }
    // DEBUGTINT begin
    /// <summary>
    /// <c>EVOSIM_THEATRE_DEBUG_TINT</c> (0 or 1, default 0), read once: a diagnostic, never a look. At 1 every renderer under a
    /// creature's root is drawn in one flat colour by its kind (<see cref="KindOf"/>), with no light, fog, pattern or mark, so a
    /// frame answers "what draws this?". A renderer on the body shader takes <c>_DebugTint</c> on its block (w 1 the colour, w 2
    /// the vertex colour, which the one skin sets per vertex: <c>TheatreOneSkin.DebugColours</c>); one on another shader (the
    /// mouth mark's unlit sphere) takes an unlit material of the kind's colour. Nothing moves. The pass runs at the end of every
    /// paint (<c>TheatrePalette.Paint</c>) and of every skin dress (<c>TheatreOneSkin.Dress</c>), so the state a frame shows is the
    /// one after both. The log has one legend line a run and one count line per body. Off, nothing here runs and no keyword is set.
    /// </summary>
    public static class TheatreDebugTint
    {
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_DEBUG_TINT", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The body shader's keyword: the forward fragment returns the flat colour first.</summary>
        public const string Keyword = "_EVO_DEBUG_TINT";

        public const int Skin = 0, FinWeb = 1, PartBox = 2, PartRod = 3, PartBall = 4, Knuckle = 5, Bridge = 6, MouthMark = 7, Other = 8;

        /// <summary>The kinds' names, in the order of the numbers above.</summary>
        public static readonly string[] Names =
        {
            "one skin (per vertex, below)", "fin web", "part piece, box", "part piece, rod (cylinder shaft)",
            "part piece, ball or cap (sphere)", "knuckle", "bridge", "mouth mark", "anything else under the creature",
        };

        /// <summary>
        /// The kinds' colours: far apart in hue and value so a glance tells them apart. The skin's entry is never drawn (the skin
        /// takes its vertex colours); its colours are <see cref="SkinBone"/>, <see cref="SkinBlend"/> and <see cref="SkinMouth"/>.
        /// </summary>
        public static readonly Color[] Colours =
        {
            new Color(1f, 0f, 1f), new Color(0f, 1f, 1f), new Color(1f, 0f, 0f), new Color(1f, 0.5f, 0f),
            new Color(1f, 1f, 0f), new Color(0f, 1f, 0f), new Color(0f, 0.45f, 0f), new Color(0.55f, 0.55f, 0.55f),
            new Color(0f, 0f, 1f),
        };

        /// <summary>The one skin's vertices by their largest bone, cycling by bone slot: magenta, violet, pink, plum.</summary>
        public static readonly Color[] SkinBone =
        {
            new Color(1f, 0f, 1f), new Color(0.55f, 0f, 1f), new Color(1f, 0.45f, 0.75f), new Color(0.45f, 0f, 0.45f),
        };

        /// <summary>A skin vertex in a joint's blend (its largest bone weight under TheatreOneSkin's DebugBlendShare): white.</summary>
        public static readonly Color SkinBlend = Color.white;

        /// <summary>A skin vertex within the shader's mouth reach of a skin mouth: black.</summary>
        public static readonly Color SkinMouth = Color.black;

        private static readonly int TintId = Shader.PropertyToID("_DebugTint");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const string BodyShader = "Evosim/Theatre Body";

        private static bool _started;
        private static MaterialPropertyBlock _block;
        private static readonly Material[] _unlit = new Material[Colours.Length];
        private static readonly Dictionary<long, int> _passes = new Dictionary<long, int>();

        /// <summary>A renderer's kind, from the names and meshes the theatre gives what it makes.</summary>
        public static int KindOf(Renderer renderer)
        {
            string name = renderer.gameObject.name;

            if (renderer is SkinnedMeshRenderer)
            {
                if (name == "OneSkin") return Skin;
                return name.StartsWith("FinWeb", System.StringComparison.Ordinal) ? FinWeb : Other;
            }

            if (name.StartsWith("Knuckle", System.StringComparison.Ordinal)) return Knuckle;
            if (name.StartsWith("Bridge", System.StringComparison.Ordinal)) return Bridge;
            if (name == TheatreMouthMarks.MarkName) return MouthMark;

            if (name == "Visual")
            {
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null && (mesh == TheatreMeshes.Sphere() || TheatreMeshes.IsPrimitiveSphere(mesh))) return PartBall;
                if (mesh != null && (mesh == TheatreMeshes.Cylinder() || TheatreMeshes.IsPrimitiveCylinder(mesh))) return PartRod;
                return PartBox;
            }

            return Other;
        }

        /// <summary>Tints every renderer under <paramref name="root"/> by its kind, and logs the body's counts at its second pass.</summary>
        public static void Apply(long id, Transform root)
        {
            if (!On || root == null) return;

            if (!_started)
            {
                _started = true;
                Shader.EnableKeyword(Keyword);
                Debug.Log(Legend());
            }

            if (_block == null) _block = new MaterialPropertyBlock();

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var all = new int[Colours.Length];
            var drawn = new int[Colours.Length];

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;

                int kind = KindOf(renderer);
                Color colour = Colours[kind];
                all[kind]++;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) drawn[kind]++;

                Material material = renderer.sharedMaterial;
                bool body = material != null && material.shader != null && material.shader.name == BodyShader;

                if (!body)
                {
                    Material unlit = UnlitOf(kind);
                    if (unlit != null) renderer.sharedMaterial = unlit;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetVector(TintId, new Vector4(colour.r, colour.g, colour.b, kind == Skin ? 2f : 1f));

                // An unlit material reads its colour from the block before the material, so the block carries it too; a body-shader
                // renderer's own _BaseColor is left alone (the skin's dress reads it as its colour fallback).
                if (!body)
                {
                    _block.SetColor(BaseColorId, colour);
                    _block.SetColor(ColorId, colour);
                }

                renderer.SetPropertyBlock(_block);
            }

            _passes.TryGetValue(id, out int passes);
            _passes[id] = ++passes;
            if (passes != 2) return;

            var line = new System.Text.StringBuilder();
            line.Append("[Theatre] debug tint: body ").Append(id).Append(", renderers drawn of all by kind:");

            for (int k = 0; k < Colours.Length; k++)
            {
                if (all[k] == 0) continue;
                line.Append(' ').Append(Names[k]).Append(' ').Append(drawn[k]).Append(" of ").Append(all[k]).Append(';');
            }

            Debug.Log(line.ToString());
        }

        /// <summary>The legend: every kind and its colour, and the skin's per-vertex colours.</summary>
        public static string Legend()
        {
            var line = new System.Text.StringBuilder("[Theatre] debug tint: ");

            line.Append("skin by its largest bone = magenta (slot 0), violet (1), pink (2), plum (3), cycling; skin in a joint's blend = white; ");
            line.Append("skin within a mouth's reach = black; ");

            string[] colourNames = { "", "cyan", "red", "orange", "yellow", "green", "dark green", "grey", "blue" };
            for (int k = 1; k < Colours.Length; k++)
            {
                line.Append(Names[k]).Append(" = ").Append(colourNames[k]).Append(k < Colours.Length - 1 ? "; " : "");
            }

            line.Append(" (flat, no light, fog or mark; a renderer not under a creature draws as it does)");
            return line.ToString();
        }

        /// <summary>An unlit material of a kind's colour, for a renderer whose shader is not the body's, made once.</summary>
        private static Material UnlitOf(int kind)
        {
            if (_unlit[kind] != null) return _unlit[kind];

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var material = new Material(shader) { name = "Debug tint " + Names[kind], hideFlags = HideFlags.HideAndDontSave };
            material.SetColor(BaseColorId, Colours[kind]);
            material.SetColor(ColorId, Colours[kind]);
            _unlit[kind] = material;
            return material;
        }
    }
    // DEBUGTINT end
}
