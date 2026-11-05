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
    /// The death cue (<c>EVOSIM_THEATRE_DEATH_CUE</c>): at a body's death row its colour drains to grey and its sheen goes over about a
    /// film second (<see cref="DrainOf"/>), and it releases a dull cloud of its own matter that drifts off with the water, sinks and thins
    /// over a few film seconds. Made because a preview of an animal dying of old age showed nothing a viewer could read as a death: at its
    /// death row the body only dulled a little, kept its shape and drift, and read as a change of light.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> <see cref="FilmWindowWorld"/> calls <see cref="Frame"/> and <see cref="DrainOf"/>
    /// only with the dial on, and with it off passes the drain's "none" (-1) on, which every drawing below reads as no drain. With it off
    /// no object, mesh, material, hook, collection or log line is made.
    /// </para>
    /// <para>
    /// <b>Which deaths.</b> The window's death rows (<see cref="FilmWindowReader.TryDeathOf"/>) of the three causes a viewer can see:
    /// <c>aged</c>, <c>starved</c> and <c>eaten</c>. A death row more than <see cref="CatchFilmSeconds"/> of film before the first frame
    /// that reaches it (a film that opens after the death) gets no cloud, as a newborn drawn long after its birth gets no birth cloud. A
    /// scene that plays the window again from earlier forgets the clouds, so each pass is drawn from its own frames.
    /// </para>
    /// <para>
    /// <b>The drain</b> is a function of the body's death row and the second on screen alone: 0 at the death row, eased to 1 over
    /// <see cref="DrainSeconds"/> of the run (a time-lapse compresses it with the world, so at 50x a death greys within a frame), and 1 from
    /// then on. The world hands it to the remains view (<see cref="LiveWorldView.Place"/>), which repaints the body whenever it changes.
    /// The palette (<see cref="TheatrePalette.Paint"/>) paints the remains as they are without it, takes their sheen, rim light,
    /// iridescence and chromatophores down by it to none, and writes it to each renderer's <c>_DeathDrain</c>; the body shader, under the
    /// keyword this class turns on (<c>_EVO_DEATH_DRAIN</c>), fades the drawn colour toward its own luminance times 0.7 with no hue by
    /// it. Nothing switches at either end, so the drain has no step, and it holds at 1 as a dull mid grey. A corpse (D127) is painted
    /// grey and matt already, and is left as it is.
    /// </para>
    /// <para>
    /// <b>A mote is a function of (body, index).</b> Every number of a mote (which part it leaves, where on that part's surface, how far
    /// it spreads, its size, its moment of dissolving, its sink, wobble and shade) is hashed from the body's id and the mote's index, so a
    /// film draws the same cloud every time. The motes are born on the surface of the body's drawn parts, spread over them by area: the
    /// dead body as the frame draws it (its remains, or its corpse), else the living body where the frame still draws it, else the last
    /// frame that drew it alive before its death row (a body that leaves nothing drawn). The count and the size follow the body's largest
    /// extent. Each mote is tinted from the painted colour of the part it left (<see cref="TheatrePalette.PaintedGuildOf"/>, as the bite
    /// crumbs take theirs), drained most of the way to grey and at about two fifths of its brightness: a dull grey-brown stain, not a
    /// light. A mote's place at a second is its birth point, a spread
    /// outward from the body's centre, the current at the centre at the death row times its age in the run's seconds, its sink, and a
    /// small wobble.
    /// </para>
    /// <para>
    /// <b>Drawn as the birth cloud is</b> (<c>TheatreBirthCloud.cs</c>): one mesh of camera-facing quads rebuilt when a camera is about
    /// to render, shaded by <c>Evosim/Theatre Crumble Flakes</c> in the snow shells' water, drawn after the lens while the lens is armed,
    /// and turned off for the census views by <see cref="TheatreInsideOnly"/>. With no snow shells in the look nothing is drawn, and the
    /// log says so once. Its motes take the shader's dark-mote path (<c>uv1.w = 2</c>, as the wound plume's do) through a material of
    /// its own blending One, OneMinusSrcAlpha, so the cloud darkens what lies behind it toward its colour, where the additive large
    /// motes could only lighten it. The vertex colour is the tint, unfaded; its alpha is the opacity, <see cref="CoreOpacity"/> eased in
    /// and thinned by each mote's dissolving.
    /// </para>
    /// <para>
    /// <b>Overpowered to start (D262)</b>, to be dialled back: about seventy motes for a body 0.8 m long, each a few times a bite crumb's
    /// radius, a cloud that spreads to about the body's own size and lasts <see cref="LifeSeconds"/> of the run. A time-lapse compresses it
    /// with the world, as it does everything the world does (the coordinator, 2026-11-02): at 50x a cloud is about two frames.
    /// </para>
    /// <para>
    /// <b>The haze</b> (<c>EVOSIM_THEATRE_DEATH_HAZE</c>, read only with the cue on, 0 by default): the cloud drawn as a soft murk in
    /// place of the discs, which at full size laid a veil of identical discs over the body in one frame. A few large puffs leave the
    /// body, spread, grow and thin as they go, each a gaussian whose edge and density are broken by noise; with them a few fine specks
    /// whose sizes follow a truncated power law toward the small, sinking. Each puff and speck starts at its own hashed moment in the
    /// cloud's first fraction of a second and eases in, so the haze gathers over several frames. It is shaded by
    /// <c>Evosim/Theatre Death Haze</c>, premultiplied, unlit in the D281 browns, and fogged with distance by the water's path as the
    /// flakes are, so the far haze fades into the water. The puffs leave mostly from one hashed side of the body, are released over
    /// the first second, and detach from the outline as they age, out to one or two half-extents along a way bent downstream, while
    /// the current carries each at its own share of its speed: the murk trails off the body in a stream and is never an aura about it.
    /// The shader cuts each puff into lumps with real gaps, and draws the specks as fine ragged grains with almost no lens blur.
    /// The drain, the log line of the window and everything else are as they are.
    /// With the dial off <see cref="Haze"/> is false, every line of the haze below is behind it, and nothing of it is made or read: the
    /// discs are drawn as they always were.
    /// </para>
    /// </remarks>
    public static class TheatreDeathCue
    {
        // ---------------------------------------------------------------- the dial

        /// <summary>The dial, <c>EVOSIM_THEATRE_DEATH_CUE</c> (0 or 1, 0 by default), read once.</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_CUE", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The haze's dial, <c>EVOSIM_THEATRE_DEATH_HAZE</c> (0 to 2, 0 by default), read once and only with the cue on: the soft murk in
        /// place of the disc cloud, at 2 keyed to the water (<see cref="HazeKeyed"/>). False leaves every frame as it was.
        /// </summary>
        public static readonly bool Haze = On && TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_HAZE", 0f, 0f, 2f) > 0f;

        /// <summary>
        /// The haze at 2 (<c>EVOSIM_THEATRE_DEATH_HAZE</c> 2): its puffs and specks drawn in the water's own colour at their height,
        /// <see cref="HazeShare"/> of it and faintly warmed by their tint, in place of the D281 browns, and their opacity raised
        /// <see cref="HazeBoost"/> times, so the murk shows at the close look (on the motes sheet the haze at 1 lifted the water behind
        /// it by 1 of 255). At 1 the haze is drawn as it was.
        /// </summary>
        public static readonly bool HazeKeyed = Haze && TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_HAZE", 0f, 0f, 2f) >= 1.5f;

        /// <summary>The share of the water's colour the keyed haze is drawn in, <c>EVOSIM_THEATRE_DEATH_HAZE_SHARE</c> (0.5 to 2, 1.1 by default).</summary>
        public static readonly float HazeShare = TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_HAZE_SHARE", 1.1f, 0.5f, 2f);

        /// <summary>How many times the keyed haze's opacity is raised, held under 1, <c>EVOSIM_THEATRE_DEATH_HAZE_BOOST</c> (1 to 4, 2 by default).</summary>
        public static readonly float HazeBoost = TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_HAZE_BOOST", 2f, 1f, 4f);

        /// <summary>
        /// <c>EVOSIM_THEATRE_DEATH_HAZE_LOG</c> (0 or 1, off by default): with it on, each cloud's haze logs how many of its puffs and
        /// specks are drawn in the frame, their mean radius and opacity, and its distance from the eye, at every half second of its
        /// age, so a faint haze is known to be few, small or thin before it is made stronger. Only clouds within
        /// <see cref="HazeLogMetres"/> of the eye, and at most <see cref="HazeLogLines"/> lines.
        /// </summary>
        private static readonly bool HazeLog = Haze && TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_HAZE_LOG", 0f, 0f, 1f) >= 0.5f;

        private const int HazeLogLines = 300;

        /// <summary>The haze log's reach from the eye, metres: a cloud further off is not logged.</summary>
        private const float HazeLogMetres = 8f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_DEATH_NO_CLOUD</c> (0 or 1, 0 by default): with it on, a death draws no cloud at all, neither the discs
        /// nor the haze, while the drain and the death look go on as before, so the dead body alone marks the death. Off, every frame is
        /// as it was.
        /// </summary>
        public static readonly bool NoCloud = On && TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_NO_CLOUD", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The death look's dial, <c>EVOSIM_THEATRE_DEATH_LOOK</c> (0 to 3, 0 by default), read once and only with the cue on: how a dead
        /// body is drawn. 0 is the grey drain as it always was. 1 is the milky drain: dead tissue in water goes paler and opaque, not
        /// greyer, and stays wet (the albedo goes to a warm milk and the light is kept, so the shading and the terminator stay). 2 is the
        /// same milk, faster, with dark blotches of the body's own hue and a speckle of relaxed chromatophores. 3 is the corpse that takes
        /// the water: the hue leaves, then one front per body crosses the skin from the extremities inward, taking the albedo to a dull
        /// grey near <see cref="MilkLuminance3"/> that keeps a ghost of the body's own marks and is lit by the water's own light, coloured
        /// as it colours a grey card there. See <see cref="DrainOf"/> and <c>_EVO_DEATH_MILK</c> in <c>TheatreBody.shader</c>.
        /// </summary>
        public static readonly int Look = On ? Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_LOOK", 0f, 0f, 3f)) : 0;

        /// <summary>
        /// The corpse's shade dial, <c>EVOSIM_THEATRE_DEATH_PALE_SHADE</c> (0 to 1, 0 by default), read once and only with the death look
        /// at 3: how far the corpse's light falls off on the side away from the key. 1 replaces the wrapped key with a plain Lambert one
        /// for the corpse; 0 leaves it as it was. A trial, set as the shader global <c>_EvoDeathPaleShade</c>.
        /// </summary>
        public static readonly float PaleShade = Look == 3 ? TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_PALE_SHADE", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// The corpse's water dial, <c>EVOSIM_THEATRE_DEATH_PALE_WATER</c> (0 to 1, 0 by default), read once and only with the death look
        /// at 3: how far the corpse's colour is drawn toward the water's own colour at its height, at the same luminance; 0 leaves it as
        /// it was. A trial, set as the shader global <c>_EvoDeathPaleWater</c>.
        /// </summary>
        public static readonly float PaleWater = Look == 3 ? TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_PALE_WATER", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// The corpse's hue dial, <c>EVOSIM_THEATRE_DEATH_PALE_HUE</c> (0 to 1, 1 by default), read once and only with the death look at 3:
        /// how much of the water's chroma the water dial gives the corpse; 1 is all of it, less leaves it greyer. Acts only with
        /// <see cref="PaleWater"/> above 0. A trial, set as the shader global <c>_EvoDeathPaleHue</c>.
        /// </summary>
        public static readonly float PaleHue = Look == 3 ? TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_PALE_HUE", 1f, 0f, 1f) : 1f;

        /// <summary>
        /// The corpse's dimming dial, <c>EVOSIM_THEATRE_DEATH_PALE_DIM</c> (0 to 1, 0.5 by default), read once and only with the death look
        /// at 3: the exponent of the water dial's pull toward the water's luminance; 0.5 is the geometric mean, less leaves the corpse
        /// brighter and its shading deeper. Acts only with <see cref="PaleWater"/> above 0. A trial, set as the shader global
        /// <c>_EvoDeathPaleDim</c>.
        /// </summary>
        public static readonly float PaleDim = Look == 3 ? TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_PALE_DIM", 0.5f, 0f, 1f) : 0.5f;

        /// <summary>
        /// The haze's colour dial, <c>EVOSIM_THEATRE_DEATH_LOOK_HAZE</c> (0 or 1, 0 by default), read once and only with the death look
        /// on: 1 draws the haze in a pale warm grey (<see cref="HazeGreyLuminance"/>) in place of the D281 browns, so it does not
        /// mismatch a white body; 0 leaves the haze as it was.
        /// </summary>
        public static readonly bool HazeGrey = Look > 0 && TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_LOOK_HAZE", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The motes' dial, <c>EVOSIM_THEATRE_DEATH_MOTES</c> (0 to 3, 0 by default), read once and only with the cue on, for the disc
        /// cloud (the haze draws its own). 0 leaves the motes as D281 first drew them: the drained paint, unlit, which in the dim water
        /// at depth shows lighter than the water behind it, an orange disc at the close look. 1 draws each mote in the water's own colour
        /// at its own height, a little lighter, warmed by the drained paint (<c>DeathStainShare</c> and
        /// <c>DeathStainTint</c> in <c>TheatreCrumbleFlakes.shader</c>). 2 draws them at a bite crumb's size (<see cref="SmallRadiusLow"/>
        /// to <see cref="SmallRadiusHigh"/>), <see cref="SmallCountFactor"/> times as many. 3 is both.
        /// </summary>
        public static readonly int Motes = On ? Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_MOTES", 0f, 0f, 3f)) : 0;

        /// <summary>Whether the motes are stains of the water (<see cref="Motes"/> 1 or 3), and whether they are crumb-sized (2 or 3).</summary>
        private static bool Stained => (Motes & 1) != 0;

        private static bool Small => (Motes & 2) != 0;

        // ---------------------------------------------------------------- the constants

        /// <summary>The run's seconds over which a dead body's colour drains to grey and its sheen goes; film seconds at 1x.</summary>
        public const float DrainSeconds = 1f;

        // ---------------------------------------------------------------- the death look's constants (EVOSIM_THEATRE_DEATH_LOOK)
        //
        // The body shader (TheatreBody.shader, DeathMilk) holds the same numbers under the same names; the two are kept together by hand.

        /// <summary>
        /// The luminance the milk reaches, per variant. The palette knows no one luminance for "the brightest living guild" (a body's
        /// albedo is its guild's colour, muted, times its reserve, or its remains' brightness), so the milk keeps the brief's numbers.
        /// </summary>
        public const float MilkLuminance1 = 0.78f;

        public const float MilkLuminance2 = 0.80f;

        /// <summary>The seconds of the run over which the hue leaves, per variant.</summary>
        public const float MilkHueSeconds1 = 0.5f;

        public const float MilkHueSeconds2 = 0.3f;

        /// <summary>
        /// The seconds a thick part takes to reach full milk (T), per variant: variant 1's lift runs from <see cref="MilkLiftFrom1"/> of it
        /// (0.3 s) to the whole; variant 2's is 80 percent done by 0.3 s (a smoothstep is 0.8 at 0.713 of its span).
        /// </summary>
        public const float MilkLiftSeconds1 = 2.5f;

        public const float MilkLiftSeconds2 = 0.42f;

        /// <summary>The share of <see cref="MilkLiftSeconds1"/> at which variant 1's lift begins: 0.3 s of 2.5.</summary>
        public const float MilkLiftFrom1 = 0.12f;

        /// <summary>How much longer a thin part takes than a thick one (thin parts reach full milk at this times T).</summary>
        public const float MilkThinLag = 2f;

        /// <summary>The share of the body's saturation the milk keeps (hue leaves, but not all of it).</summary>
        public const float MilkKeepSaturation = 0.25f;

        /// <summary>The shares the milk takes off the light through the tissue, the sheen and the rim light at full lift.</summary>
        public const float MilkTransmissionLoss = 0.7f;

        public const float MilkSheenLoss = 0.4f;
        public const float MilkRimLoss = 0.5f;

        /// <summary>The diffuse wrap a milk body moves toward at full lift (the material's is 0.45): a softer terminator.</summary>
        public const float MilkWrap = 0.65f;

        /// <summary>The seconds a variant's drain runs, which is the span the drain value 0 to 1 covers, linear, with the look on.</summary>
        public const float MilkSpan1 = MilkLiftSeconds1 * MilkThinLag;

        /// <summary>Variant 2's span: the blotches fade over three seconds, which is longer than its milk (0.42 s, thin 0.84 s).</summary>
        public const float MilkSpan2 = 3f;

        // Variant 3, the corpse that takes the water. Strong to start (D262), to be dialled back.

        /// <summary>
        /// Variant 3's end albedo luminance, before the ghost of the marks: 0.13, so the corpse shows near 0.55 under the close shot's
        /// key and the film's fill light. An albedo of 0.55 showed at 0.85 on 63868, as bright as the milk; 0.22, keyed before the fill
        /// light came in, showed at 0.70, twice the grey drain's corpse (2026-11-04).
        /// </summary>
        public const float MilkLuminance3 = 0.13f;

        /// <summary>Variant 3's seconds over which the hue leaves (to <see cref="MilkKeepSaturation"/> of it), all at once over the body.</summary>
        public const float MilkHueSeconds3 = 0.5f;

        /// <summary>Variant 3's seconds over which the front crosses the body, from its extremities (r 1) to its centre (r 0), after the hue.</summary>
        public const float MilkFrontSeconds3 = 3.5f;

        /// <summary>The width of variant 3's front's soft edge, as a share of the body's largest reach.</summary>
        public const float MilkFrontEdge3 = 0.5f;

        /// <summary>The share of the living contrast of the body's own marks (pattern, mottle, bands) the dead albedo keeps.</summary>
        public const float MilkGhostContrast3 = 0.33f;

        /// <summary>How far variant 3's albedo is pulled, behind the front, toward the grey of its own luminance, which the water's light then tints.</summary>
        public const float MilkTintPull3 = 0.6f;

        /// <summary>The place on the body (r, 0 at the centre to 1 at the extremities) at which the palette reads variant 3's front for the per renderer sheen, rim, light through and wrap.</summary>
        public const float MilkFrontMiddling3 = 0.5f;

        /// <summary>Variant 3's span: the hue, then the front, about four seconds of the run from the death to the end state.</summary>
        public const float MilkSpan3 = MilkHueSeconds3 + MilkFrontSeconds3;

        /// <summary>The luminance of the haze's pale grey with <see cref="HazeGrey"/>.</summary>
        public const float HazeGreyLuminance = 0.55f;

        /// <summary>The haze's pale grey, in sRGB: luminance 0.55 with a slight warm tint (0.58, 0.55, 0.51 weighs 0.5535).</summary>
        private static readonly Color HazePale = new Color(0.58f, 0.55f, 0.51f, 1f);

        /// <summary>
        /// The seconds of the run the drain value 0 to 1 spans: <see cref="DrainSeconds"/> with the look off, the look's span with it on.
        /// </summary>
        public static float DrainSpan => Look == 1 ? MilkSpan1 : Look == 2 ? MilkSpan2 : Look == 3 ? MilkSpan3 : DrainSeconds;

        /// <summary>How far the hue has left a milk body at an age in the run's seconds, 0 to 1: a smoothstep over the variant's hue seconds.</summary>
        public static float MilkHue(float age)
        {
            float u = Mathf.Clamp01(age / (Look == 3 ? MilkHueSeconds3 : Look == 2 ? MilkHueSeconds2 : MilkHueSeconds1));
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// How far the luminance has lifted on a part of a thinness (0 thick, 1 thin) at an age, 0 to 1: a smoothstep, thick parts first
        /// (a thin one takes <see cref="MilkThinLag"/> times as long). The shader's per fragment lift is this with the fragment's thinness.
        /// Variant 3 has no clock per part: its lift is the front's at <see cref="MilkFrontMiddling3"/>, whatever the thinness.
        /// </summary>
        public static float MilkLift(float age, float thin)
        {
            if (Look == 3) return MilkFront(age, MilkFrontMiddling3);

            float seconds = (Look == 2 ? MilkLiftSeconds2 : MilkLiftSeconds1) * Mathf.Lerp(1f, MilkThinLag, Mathf.Clamp01(thin));
            float x = Mathf.Clamp01(age / seconds);
            float from = Look == 2 ? 0f : MilkLiftFrom1;
            float u = Mathf.Clamp01((x - from) / (1f - from));
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// Variant 3's front at a place on the body (r, its distance from the body's centre over the body's largest reach) at an age, 0
        /// to 1: the front moves from r 1 to r 0 over <see cref="MilkFrontSeconds3"/>, after the hue's <see cref="MilkHueSeconds3"/>,
        /// with a soft edge <see cref="MilkFrontEdge3"/> wide. The shader's <c>DeathMilk3</c> is the same with the fragment's own r.
        /// </summary>
        public static float MilkFront(float age, float r)
        {
            float s = Mathf.Clamp01((age - MilkHueSeconds3) / MilkFrontSeconds3);
            float front = Mathf.Lerp(1f + 0.5f * MilkFrontEdge3, -0.5f * MilkFrontEdge3, s);
            float u = Mathf.Clamp01((r - (front - 0.5f * MilkFrontEdge3)) / MilkFrontEdge3);
            return u * u * (3f - 2f * u);
        }

        /// <summary>The run's seconds a cloud lasts, which are film seconds at 1x: its last motes are gone by then.</summary>
        public const float LifeSeconds = 4f;

        /// <summary>A death row more than this many film seconds before the first frame that reaches it gets no cloud.</summary>
        public const double CatchFilmSeconds = 0.5d;

        /// <summary>
        /// How far ahead, in film seconds, a living body whose death row is coming is kept as it is drawn, for a death that leaves
        /// nothing drawn: the cloud is then born from its last living frame.
        /// </summary>
        private const double AheadFilmSeconds = 2d;

        /// <summary>The count of motes: <see cref="CountLow"/> for a body of <see cref="ExtentLow"/> metres or less, up to <see cref="CountHigh"/> at <see cref="ExtentHigh"/>.</summary>
        private const int CountLow = 24;

        private const int CountHigh = 96;
        private const float ExtentLow = 0.05f;
        private const float ExtentHigh = 1.2f;

        /// <summary>A mote's radius as a share of the body's largest extent: a fortieth to a twenty-fifth, hashed per mote.</summary>
        private const float RadiusShareLow = 1f / 40f;

        private const float RadiusShareHigh = 1f / 25f;

        /// <summary>The bounds a mote's radius is held to, metres: a few times a bite crumb's (0.0012 to 0.025).</summary>
        private const float RadiusLow = 0.004f;

        private const float RadiusHigh = 0.06f;

        /// <summary>
        /// With <see cref="Motes"/> at 2 or 3, a mote's radius as a share of the body's largest extent (a hundredth to a sixtieth, hashed
        /// per mote), the bounds it is held to (a bite crumb's, metres), and how many times the count the motes are.
        /// </summary>
        private const float SmallShareLow = 1f / 100f;

        private const float SmallShareHigh = 1f / 60f;
        private const float SmallRadiusLow = 0.0012f;
        private const float SmallRadiusHigh = 0.025f;
        private const int SmallCountFactor = 4;

        /// <summary>How far a mote spreads outward over its life, in the body's largest half-extents, and the film seconds over which the spread settles.</summary>
        private const float SpreadHalfExtents = 1f;

        private const float SpreadSeconds = 1.5f;

        /// <summary>
        /// A mote's moment of dissolving is <c>lerp(EndLow, 1, h^EndPower)</c> of <see cref="LifeSeconds"/>: most of the cloud holds for
        /// over two seconds, and the last of it thins to the end.
        /// </summary>
        private const float EndLow = 0.55f;

        private const float EndPower = 1.5f;

        /// <summary>The stretch over which a mote dissolves before its end, as a share of the end, and held under this many film seconds.</summary>
        private const float SpanShare = 0.5f;

        private const float SpanMostSeconds = 1.5f;

        /// <summary>The film seconds over which the cloud's brightness eases in from <see cref="InFrom"/>, so its first frame is no flash.</summary>
        private const float InSeconds = 0.12f;

        private const float InFrom = 0.5f;

        /// <summary>How fast a mote sinks, m/s of the run (dead matter, slower than torn crumbs), varied by <see cref="SinkJitter"/> either way.</summary>
        private const float SinkMetresPerSecond = 0.005f;

        private const float SinkJitter = 0.4f;

        /// <summary>How far a mote's spread leans off the line out from the body's centre: a unit vector of this weight against the line's one.</summary>
        private const float SpreadScatter = 0.6f;

        /// <summary>The wobble of a mote about its path, metres, and the range of its period, film seconds.</summary>
        private const float WobbleMetres = 0.004f;

        private const float WobbleLow = 1.2f;
        private const float WobbleHigh = 2.4f;

        /// <summary>A mote outside the frustum by more than this, metres, is not built.</summary>
        private const float Margin = 0.1f;

        /// <summary>The shader's seconds from the vertex colour to the snow's mote colour, set far beyond any cloud's life: a mote keeps its tint.</summary>
        private const float TakeSeconds = 10000f;

        /// <summary>How far a mote's painted colour is drained toward its grey, and the share of its brightness it keeps: a dull grey-brown stain.</summary>
        private const float GreyShare = 0.6f;

        private const float BrightnessShare = 0.4f;

        /// <summary>The tint where the palette is not known: the bite crumbs' pale tissue (0.86, 0.78, 0.72) at two fifths of its brightness, in sRGB.</summary>
        private static readonly Color DullTissue = new Color(0.34f, 0.31f, 0.29f, 1f);

        /// <summary>A mote's opacity (the vertex alpha the dark-mote path reads) at full strength: overlapping at the cloud's core, the stain is dense.</summary>
        private const float CoreOpacity = 0.7f;

        /// <summary>A mote's shade about its tint, hashed between these.</summary>
        private const float ShadeLow = 0.8f;

        private const float ShadeHigh = 1.1f;

        // ---------------------------------------------------------------- the haze's constants (EVOSIM_THEATRE_DEATH_HAZE)

        /// <summary>The haze's shader.</summary>
        private const string HazeShaderName = "Evosim/Theatre Death Haze";

        /// <summary>The count of puffs, from a body of <see cref="ExtentLow"/> metres to one of <see cref="ExtentHigh"/>, as the motes' count runs.</summary>
        private const int PuffCountLow = 6;

        private const int PuffCountHigh = 12;

        /// <summary>
        /// A puff's starting radius as a share of the body's largest extent, <c>lerp(low, high, h^PuffSharePower)</c> per puff, so most
        /// are small and a few large (D262's strong start: a wider range than haze2's 0.16 to 0.28), and the metres it is held to.
        /// </summary>
        private const float PuffShareLow = 0.1f;

        private const float PuffShareHigh = 0.34f;
        private const float PuffSharePower = 1.6f;
        private const float PuffRadiusLow = 0.008f;
        private const float PuffRadiusHigh = 0.4f;

        /// <summary>How far toward the body's centre a puff's birth point is drawn from the surface point it leaves (0: on the surface): it starts at the outline.</summary>
        private const float PuffInset = 0.15f;

        /// <summary>
        /// The share of puffs seeded on the cloud's own hashed side of the body, each the furthest that way of <see cref="PuffSideTries"/>
        /// hashed surface points; the rest anywhere. Matter leaves a dead body unevenly, not as an aura (D262's strong start).
        /// </summary>
        private const float PuffSideShare = 0.8f;

        private const int PuffSideTries = 4;

        /// <summary>
        /// How far a puff detaches from the body over its life, in the body's largest half-extents, hashed between these: its distance
        /// from the surface grows from nothing to this, eased out over <see cref="PuffDetachSeconds"/> of its own age (D262's strong start).
        /// </summary>
        private const float PuffDetachLow = 1f;

        private const float PuffDetachHigh = 2f;
        private const float PuffDetachSeconds = LifeSeconds;

        /// <summary>
        /// The weight of the downstream direction (the current at the body) against a puff's or a speck's own outward direction, when
        /// its detachment is bent: the haze trails downstream. Below <see cref="CurrentFloor"/> m/s there is no downstream, and no bend.
        /// </summary>
        private const float DownstreamLean = 1.5f;

        private const float CurrentFloor = 1e-4f;

        /// <summary>
        /// A puff is carried by the current at <c>1 ± PuffCarrySpread</c> of its speed, hashed, so the puffs string out along the flow
        /// as water sheared about a body does (D262's strong start).
        /// </summary>
        private const float PuffCarrySpread = 0.3f;

        /// <summary>The film seconds over which a speck's spread settles.</summary>
        private const float PuffSpreadSeconds = 1.8f;

        /// <summary>How much a puff's radius grows as it detaches (1 + this at the end), and the power its opacity thins by against that growth.</summary>
        private const float PuffGrow = 1.2f;

        private const float PuffThinPower = 0.8f;

        /// <summary>A puff's opacity at full strength, before its thinning: strong at the start (D262), since 0.3 with a steeper thinning barely read against the dark water at 1x (phase109).</summary>
        private const float PuffOpacity = 0.7f;

        /// <summary>
        /// The haze's strength at a time-lapse: from 1x to <see cref="LapseFullSpeed"/> it falls from the constants above to these, which
        /// are phase109's first haze. At 50x other bodies die every few frames, each haze lives about two frames, and the strong puffs
        /// flashed there as faint two-frame halos (phase110, 50x haze2 against haze); the first haze's level did not.
        /// </summary>
        private const int LapsePuffCountLow = 4;

        private const int LapsePuffCountHigh = 8;
        private const float LapsePuffThinPower = 1.2f;
        private const float LapsePuffOpacity = 0.3f;
        private const int LapseSpeckCountLow = 10;
        private const int LapseSpeckCountHigh = 36;

        /// <summary>The time-lapse at and above which the haze takes the <c>Lapse</c> constants whole.</summary>
        private const double LapseFullSpeed = 10d;

        /// <summary>How far the haze has gone toward its time-lapse strength: 0 at 1x, 1 at <see cref="LapseFullSpeed"/> and above.</summary>
        private static float LapseShare => (float)Math.Min(1d, Math.Max(0d, (_speed - 1d) / (LapseFullSpeed - 1d)));

        // A strength between the 1x constant and the time-lapse one, exactly either at the ends.
        private static float ByLapse(float strong, float lapse)
        {
            float u = LapseShare;
            return u <= 0f ? strong : u >= 1f ? lapse : Mathf.Lerp(strong, lapse, u);
        }

        /// <summary>
        /// The film seconds within which each puff starts, hashed, and over which each eases in from nothing: released over the first
        /// second, so the earliest are furthest off and the haze leaves in a stream (haze2's was 0.3).
        /// </summary>
        private const float PuffStagger = 1f;

        private const float PuffInSeconds = 0.25f;

        /// <summary>A puff's end, <c>lerp(PuffEndLow, 1, h)</c> of <see cref="LifeSeconds"/>, and the share of it over which it dissolves.</summary>
        private const float PuffEndLow = 0.7f;

        private const float PuffSpanShare = 0.5f;

        /// <summary>How fast a puff settles downward, m/s of the run: slower than the specks.</summary>
        private const float PuffSinkMetresPerSecond = 0.003f;

        /// <summary>A puff's shade about its tint, hashed between these: never above the tint.</summary>
        private const float PuffShadeLow = 0.85f;

        private const float PuffShadeHigh = 1f;

        /// <summary>How far a puff's quad reaches, in its radius: the shader's window ends there (EVO_HAZE_REACH).</summary>
        private const float PuffReach = 1.6f;

        /// <summary>The count of specks, from a body of <see cref="ExtentLow"/> metres to one of <see cref="ExtentHigh"/>: fewer than haze2's 20 to 72.</summary>
        private const int SpeckCountLow = 10;

        private const int SpeckCountHigh = 30;

        /// <summary>
        /// A speck's radius, metres: fine particulate whatever the body's size, drawn from a power law truncated to this range, its
        /// density falling as the radius to the power <c>-(1 + SpeckPower)</c>, so most specks are near the small end.
        /// </summary>
        private const float SpeckRadiusLow = 0.0003f;

        private const float SpeckRadiusHigh = 0.0015f;
        private const float SpeckPower = 1.6f;

        /// <summary>How far a speck spreads outward, in the body's largest half-extents, at most.</summary>
        private const float SpeckSpreadHalfExtents = 0.8f;

        /// <summary>How fast a speck sinks, m/s of the run, from the smallest to the largest (a larger speck sinks faster), varied by <see cref="SinkJitter"/>.</summary>
        private const float SpeckSinkLow = 0.006f;

        private const float SpeckSinkHigh = 0.016f;

        /// <summary>A speck's opacity at full strength: a fine dark grain, denser than a puff (D262's strong start; the shader thins the sub-pixel ones).</summary>
        private const float SpeckOpacity = 0.9f;

        /// <summary>The film seconds within which each speck starts, hashed, and over which each eases in.</summary>
        private const float SpeckStagger = 0.25f;

        private const float SpeckInSeconds = 0.15f;

        /// <summary>A speck's end, <c>lerp(SpeckEndLow, 1, h)</c> of <see cref="LifeSeconds"/>, and the share of it over which it dissolves.</summary>
        private const float SpeckEndLow = 0.6f;

        private const float SpeckSpanShare = 0.4f;

        /// <summary>A speck's shade about its tint, hashed between these: darker than the puffs.</summary>
        private const float SpeckShadeLow = 0.65f;

        private const float SpeckShadeHigh = 0.9f;

        // ---------------------------------------------------------------- one body's cloud

        /// <summary>One mote, fixed at the cloud's start.</summary>
        private struct Mote
        {
            /// <summary>Its birth point on the body's surface, relative to the body's centre.</summary>
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

            /// <summary>The dulled painted colour of the part it left, in the colour space the shader mixes it in.</summary>
            public Color Tint;
        }

        /// <summary>One body's cloud: its death row's second, the current it drifts in, and its motes.</summary>
        private sealed class Cloud
        {
            public long Id;

            /// <summary>The second of the run of the death row: the cloud's start.</summary>
            public double First;

            public Vector3 Centre;

            public bool CurrentKnown;
            public Vector3 Current;

            public Mote[] Motes;

            /// <summary>The haze's puffs and specks (<see cref="Haze"/>); null with the haze's dial off.</summary>
            public Puff[] Puffs;

            public Speck[] Specks;
        }

        /// <summary>One puff of the haze, fixed at the cloud's start. A radius of 0 is no puff.</summary>
        private struct Puff
        {
            /// <summary>Its birth point, relative to the body's centre, and where its detachment takes it from there before the downstream bend.</summary>
            public Vector3 Offset;

            public Vector3 Out;

            /// <summary>Its radius at birth, before it grows as it detaches.</summary>
            public float Radius;

            /// <summary>The share of the current's speed it is carried at.</summary>
            public float Carry;

            /// <summary>The film second of the cloud's age at which it starts, the one at which it is gone, and its dissolving stretch.</summary>
            public float Start;

            public float End;
            public float Span;

            public float Sink;
            public float Seed;
            public float Shade;
            public Color Tint;
        }

        /// <summary>One speck of the haze, fixed at the cloud's start. A radius of 0 is no speck.</summary>
        private struct Speck
        {
            public Vector3 Offset;
            public Vector3 Out;
            public float Radius;
            public float Start;
            public float End;
            public float Span;
            public float Sink;
            public Vector3 Phase;
            public float Omega;
            public float Seed;
            public float Shade;
            public Color Tint;
        }

        // ---------------------------------------------------------------- the state (made at the first frame, with the dial on)

        private static FilmWindowWorld _window;
        private static int _frame = -1;
        private static double _second = double.NaN;
        private static double _speed = 1d;
        private static int _deaths;
        private static bool _logged;

        private static List<Cloud> _clouds;
        private static HashSet<long> _seen;

        /// <summary>The clouds of bodies whose death row is coming, captured from their living frames, for a death that leaves nothing drawn.</summary>
        private static Dictionary<long, Cloud> _ahead;

        private static List<long> _aheadGone;

        // The drawing.
        private static GameObject _object;
        private static MeshRenderer _renderer;
        private static Mesh _mesh;
        private static Mesh _drawMesh;
        private static Material _material;
        private static CuePass _pass;
        private static bool _hooked;
        private static bool _noShader;
        private static bool _noShellsTold;

        /// <summary>Whether the haze's one log line has been written (<see cref="Haze"/> only).</summary>
        private static bool _hazeLogged;

        /// <summary>Whether <see cref="NoCloud"/>'s one log line has been written.</summary>
        private static bool _noCloudLogged;

        /// <summary>The haze log's lines written, by cloud and half second of its age (<see cref="HazeLog"/> only).</summary>
        private static readonly HashSet<(long, int)> _hazeLogKeys = new HashSet<(long, int)>();

        /// <summary>The eye of the camera the cloud is being built for, for the haze log's distance.</summary>
        private static Vector3 _eye;

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

        // ---------------------------------------------------------------- the drain

        /// <summary>
        /// How far a body's drain has gone at the second on screen: -1 for no drain (the dial off, a body with no death row of a seen cause
        /// in the window, or a second before its death row), else 0 at the death row eased to 1 over <see cref="DrainSeconds"/> of the run,
        /// and 1 from then on. A function of the death row and the second alone, so any film of the window draws the same drain.
        /// </summary>
        public static float DrainOf(FilmWindowWorld window, long id)
        {
            if (!On || window == null || window.Window == null || double.IsNaN(window.Second)) return -1f;
            if (!window.Window.TryDeathOf(id, out FilmWindowEvent death) || !Seen(death.Cause)) return -1f;

            double age = window.Second - death.Seconds;
            if (age < -1e-6) return -1f;

            EnsureKeyword();

            // The death look (EVOSIM_THEATRE_DEATH_LOOK): the drain is the run's age over the look's span, linear, which the palette
            // turns back into seconds and the shader's milk eases by its own ramps. Never with the dial at 0.
            if (Look > 0) return Mathf.Clamp01((float)(age / DrainSpan));

            // The run's clock, as the cloud's: an effect lasts as long in the run whatever the film's speed.
            float u = Mathf.Clamp01((float)(age / DrainSeconds));
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// The body shader's drain (<c>_EVO_DEATH_DRAIN</c>, a global keyword), turned on once, at the first drain, so the shader reads
        /// each renderer's <c>_DeathDrain</c>. Never with the dial off, and then every body draws through the variant it always did.
        /// </summary>
        private static void EnsureKeyword()
        {
            if (_keywordOn) return;

            _keywordOn = true;
            Shader.EnableKeyword(DrainKeyword);

            // The death look's milk, only with its dial on, and with the one log line of the first frame it takes effect.
            if (Look > 0)
            {
                Shader.EnableKeyword(MilkKeyword);

                // Variant 3's own line, with its own numbers; variants 1 and 2 keep theirs below as they were.
                if (Look == 3)
                {
                    Shader.SetGlobalFloat("_EvoDeathPaleShade", PaleShade);
                    Shader.SetGlobalFloat("_EvoDeathPaleWater", PaleWater);
                    Shader.SetGlobalFloat("_EvoDeathPaleHue", PaleHue);
                    Shader.SetGlobalFloat("_EvoDeathPaleDim", PaleDim);
                    string look3Line = string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] death cue: death look 3 (EVOSIM_THEATRE_DEATH_LOOK), the corpse that takes the water; hue leaves over {0:0.##} s " +
                        "(keeps {1:0.#}% of it), then one front per body from the extremities inward over {2:0.##} s, edge {3:0.##} of the reach; " +
                        "luminance {4:0.##}, marks kept at {5:0.#}% of their contrast, pulled {6:0.#}% to grey under the water's own light; drain span " +
                        "{7:0.##} s, sheen -{8:0.#}%, rim -{9:0.#}%, light through -{10:0.#}%, wrap to {11:0.##}; haze {12}",
                        MilkHueSeconds3, MilkKeepSaturation * 100f, MilkFrontSeconds3, MilkFrontEdge3,
                        MilkLuminance3, MilkGhostContrast3 * 100f, MilkTintPull3 * 100f, DrainSpan,
                        MilkSheenLoss * 100f, MilkRimLoss * 100f, MilkTransmissionLoss * 100f, MilkWrap,
                        HazeGrey ? "a pale grey (EVOSIM_THEATRE_DEATH_LOOK_HAZE)" : "the D281 browns");
                    if (PaleShade > 0f || PaleWater > 0f)
                    {
                        look3Line += string.Format(CultureInfo.InvariantCulture,
                            "; trial: shade {0:0.##} (EVOSIM_THEATRE_DEATH_PALE_SHADE), water {1:0.##} (EVOSIM_THEATRE_DEATH_PALE_WATER), " +
                            "hue {2:0.##} (EVOSIM_THEATRE_DEATH_PALE_HUE), dim {3:0.##} (EVOSIM_THEATRE_DEATH_PALE_DIM)",
                            PaleShade, PaleWater, PaleHue, PaleDim);
                    }
                    Debug.Log(look3Line);
                    return;
                }

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] death cue: death look {0} (EVOSIM_THEATRE_DEATH_LOOK), {1}; milk luminance {2:0.##}, hue leaves over {3:0.##} s, lift over {4:0.##} s " +
                    "(thin parts {5:0.#}x as long), drain span {6:0.##} s, sheen -{7:0.#}%, rim -{8:0.#}%, light through -{9:0.#}%, wrap to {10:0.##}; haze {11}",
                    Look, Look == 1 ? "the milky drain" : "the blanch with blotches and speckle",
                    Look == 2 ? MilkLuminance2 : MilkLuminance1, Look == 2 ? MilkHueSeconds2 : MilkHueSeconds1,
                    Look == 2 ? MilkLiftSeconds2 : MilkLiftSeconds1, MilkThinLag, DrainSpan,
                    MilkSheenLoss * 100f, MilkRimLoss * 100f, MilkTransmissionLoss * 100f, MilkWrap,
                    HazeGrey ? "a pale grey (EVOSIM_THEATRE_DEATH_LOOK_HAZE)" : "the D281 browns"));
            }
        }

        /// <summary>The body shader's keyword for the drain.</summary>
        private const string DrainKeyword = "_EVO_DEATH_DRAIN";

        /// <summary>The body shader's keyword for the death look's milk, turned on beside <see cref="DrainKeyword"/> and only with <see cref="Look"/> above 0.</summary>
        private const string MilkKeyword = "_EVO_DEATH_MILK";

        private static bool _keywordOn;

        /// <summary>The causes a viewer can see a body die of.</summary>
        private static bool Seen(string cause) => cause == "aged" || cause == "starved" || cause == "eaten";

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="FilmWindowWorld.Show"/> after every body of a frame is placed, with the dial on: starts the cloud of each
        /// death row this frame reaches (within <see cref="CatchFilmSeconds"/> of film of it), keeps the living bodies whose death rows
        /// are coming, and forgets the clouds that have thinned away.
        /// </summary>
        public static void Frame(FilmWindowWorld window)
        {
            if (!On || window == null || window.Window == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;

            if (_clouds == null)
            {
                _clouds = new List<Cloud>();
                _seen = new HashSet<long>();
                _ahead = new Dictionary<long, Cloud>();
                _aheadGone = new List<long>();
            }

            bool replaced = !ReferenceEquals(window, _window);
            bool reopened = replaced || window.FrameIndex < _frame;

            if (!reopened && window.FrameIndex == _frame) return;

            if (replaced)
            {
                _logged = false;
                _deaths = DeathsIn(window.Window);
            }

            if (reopened)
            {
                _clouds.Clear();
                _seen.Clear();
                _ahead.Clear();
            }

            _window = window;
            _frame = window.FrameIndex;
            _second = window.Second;
            _speed = Math.Max(1d, window.Speed);

            // A window with no death at all says so at once; any other says its first cloud when it starts.
            if (!_logged && _deaths == 0) Announce(null, -1L, double.NaN, null);

            bool drawer = EnsureDrawer();
            FilmWindowReader reader = window.Window;

            // The death rows this frame reaches: at or before its second, and no more than the catch before it.
            foreach (FilmWindowEvent death in reader.EventsBetween(_second - CatchFilmSeconds * _speed, _second))
            {
                if (death.Kind != 'd' || !Seen(death.Cause) || !_seen.Add(death.Id)) continue;

                Cloud cloud = CaptureDrawn(window, death.Id, death.Seconds);

                if (cloud == null && _ahead.TryGetValue(death.Id, out Cloud kept)) cloud = kept;
                _ahead.Remove(death.Id);

                if (cloud == null)
                {
                    if (!_logged) Announce(null, death.Id, death.Seconds, "the body is drawn in no frame near its death row");
                    continue;
                }

                if (drawer) _clouds.Add(cloud);
                if (!_logged) Announce(cloud, cloud.Id, cloud.First, null);
            }

            // The living bodies whose death rows are coming, kept as this frame draws them, for a death that leaves nothing drawn.
            if (drawer && window.View != null)
            {
                foreach (FilmWindowEvent death in reader.EventsBetween(_second, _second + AheadFilmSeconds * _speed))
                {
                    if (death.Kind != 'd' || !Seen(death.Cause) || _seen.Contains(death.Id) || !window.View.Holds(death.Id)) continue;

                    Cloud living = Capture(window, window.View, death.Id, death.Id, death.Seconds);
                    if (living != null) _ahead[death.Id] = living;
                }
            }

            // Kept bodies whose death row has passed unreached (a frame the film never showed), forgotten.
            _aheadGone.Clear();
            foreach (KeyValuePair<long, Cloud> pair in _ahead)
            {
                if (pair.Value.First < _second - CatchFilmSeconds * _speed) _aheadGone.Add(pair.Key);
            }

            for (int i = 0; i < _aheadGone.Count; i++) _ahead.Remove(_aheadGone[i]);

            // The clouds that have thinned away, forgotten; kept in their order, so the quads are written in the same order every time.
            for (int c = _clouds.Count - 1; c >= 0; c--)
            {
                if (_second - _clouds[c].First >= LifeSeconds) _clouds.RemoveAt(c);
            }
        }

        // ---------------------------------------------------------------- one body

        /// <summary>The window's death rows of a seen cause, counted once.</summary>
        private static int DeathsIn(FilmWindowReader reader)
        {
            IReadOnlyList<FilmWindowEvent> events = reader.Events;
            int n = 0;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == 'd' && Seen(events[i].Cause)) n++;
            }

            return n;
        }

        /// <summary>
        /// A dead body's cloud from the frame on screen: its remains where the remains view draws them, else its corpse, else the living
        /// body where the frame still draws it. Null where the frame draws none of them.
        /// </summary>
        private static Cloud CaptureDrawn(FilmWindowWorld window, long id, double first)
        {
            Cloud cloud = null;

            if (window.RemainsView != null) cloud = Capture(window, window.RemainsView, id, id, first);
            if (cloud == null && window.CorpseView != null) cloud = Capture(window, window.CorpseView, -(id + 1L), id, first);
            if (cloud == null && window.View != null) cloud = Capture(window, window.View, id, id, first);

            return cloud;
        }

        /// <summary>
        /// One body's cloud from its parts as a view draws them: the motes born on the parts' surfaces, spread over them by area, and
        /// tinted from the painted colour of the part each leaves. <paramref name="key"/> is the body's key in that view (a corpse's is
        /// its own), <paramref name="id"/> its id, which the motes and the colours are hashed and painted from. Null where the view does
        /// not hold the body.
        /// </summary>
        private static Cloud Capture(FilmWindowWorld window, LiveWorldView view, long key, long id, double first)
        {
            if (!view.TryBody(key, out Phenotype sized, out Transform[] parts) || sized == null || parts == null) return null;

            int n = Mathf.Min(sized.PartCount, parts.Length);
            if (n == 0) return null;

            // Each part's scale in the world (a corpse's shrink sits on its root), its surface's area, and the box about them all.
            var scales = new float[n];
            var areas = new float[n];
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 hi = -lo;
            float total = 0f;

            for (int q = 0; q < n; q++)
            {
                Transform part = parts[q];
                if (part == null) continue;

                float s = Mathf.Abs(part.lossyScale.x);
                if (!(s > 0f) || float.IsInfinity(s)) s = 1f;

                Float3 h = sized.Parts[q].HalfExtents;
                float hx = Mathf.Abs(h.X) * s, hy = Mathf.Abs(h.Y) * s, hz = Mathf.Abs(h.Z) * s;

                scales[q] = s;
                areas[q] = hx * hy + hx * hz + hy * hz;
                total += areas[q];

                Vector3 e = Abs(part.right) * hx + Abs(part.up) * hy + Abs(part.forward) * hz;
                lo = Vector3.Min(lo, part.position - e);
                hi = Vector3.Max(hi, part.position + e);
            }

            if (!(total > 0f) || float.IsInfinity(total)) return null;

            Vector3 size = hi - lo;
            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (!(extent > 1e-6f) || float.IsInfinity(extent)) return null;

            Vector3 centre = 0.5f * (lo + hi);
            float half = 0.5f * extent;
            int count = Mathf.RoundToInt(Mathf.Lerp(CountLow, CountHigh, Mathf.InverseLerp(ExtentLow, ExtentHigh, extent)));
            if (Small) count *= SmallCountFactor;

            // The painted colour of each part, dulled, worked out once for the parts a mote leaves.
            var tints = new Color[n];
            var tinted = new bool[n];
            TheatrePalette palette = window.Palette;
            bool on = window.ColourByCellType;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            var cloud = new Cloud { Id = id, First = first, Centre = centre, Motes = new Mote[count] };

            for (int i = 0; i < count; i++)
            {
                // The part it leaves, by area, and a point on that part's surface.
                float pick = total * Hash(id, i, 1);
                int q = 0;
                float run = 0f;

                for (; q < n - 1; q++)
                {
                    run += areas[q];
                    if (pick < run) break;
                }

                while (q > 0 && parts[q] == null) q--;
                Transform part = parts[q];
                if (part == null) continue;

                Float3 h = sized.Parts[q].HalfExtents;
                float s = scales[q];
                Vector3 local = TheatreCrumble.SurfacePoint(Mathf.Abs(h.X) * s, Mathf.Abs(h.Y) * s, Mathf.Abs(h.Z) * s,
                    Hash(id, i, 2), Hash(id, i, 3), Hash(id, i, 4), Hash(id, i, 5));
                Vector3 birth = part.position + part.rotation * local;

                // Outward from the body's centre through the birth point, leaning off that line by a hashed unit vector.
                Vector3 from = birth - centre;
                Vector3 way = (from.sqrMagnitude > 1e-12f ? from.normalized : Vector3.up) + SpreadScatter * Unit(Hash(id, i, 6), Hash(id, i, 7));
                way = way.sqrMagnitude > 1e-8f ? way.normalized : Vector3.up;

                float end = LifeSeconds * Mathf.Lerp(EndLow, 1f, Mathf.Pow(Hash(id, i, 8), EndPower));
                float span = Mathf.Min(SpanShare * end, SpanMostSeconds);
                float radius = Small
                    ? Mathf.Clamp(extent * Mathf.Lerp(SmallShareLow, SmallShareHigh, Hash(id, i, 9)), SmallRadiusLow, SmallRadiusHigh)
                    : extent * Mathf.Lerp(RadiusShareLow, RadiusShareHigh, Hash(id, i, 9));
                float period = Mathf.Lerp(WobbleLow, WobbleHigh, Hash(id, i, 10));

                if (!tinted[q])
                {
                    tinted[q] = true;
                    Color painted = palette != null ? palette.PaintedGuildOf(id, sized, q, on) : DullTissue;
                    if (palette != null) painted = Dulled(painted);
                    painted.a = 1f;
                    tints[q] = linear ? painted.linear : painted;
                }

                cloud.Motes[i] = new Mote
                {
                    Offset = birth - centre,
                    Out = way * (SpreadHalfExtents * half * Mathf.Lerp(0.5f, 1f, Hash(id, i, 11))),
                    Radius = Small ? radius : Mathf.Clamp(radius, RadiusLow, RadiusHigh),
                    End = end,
                    Span = Mathf.Max(1e-3f, span),
                    Sink = SinkMetresPerSecond * (1f + SinkJitter * (Hash(id, i, 12) - 0.5f)),
                    Phase = new Vector3(6.2831853f * Hash(id, i, 13), 6.2831853f * Hash(id, i, 14), 6.2831853f * Hash(id, i, 15)),
                    Omega = 6.2831853f / period,
                    Seed = Hash(id, i, 16) * 97f,
                    Shade = Mathf.Lerp(ShadeLow, ShadeHigh, Hash(id, i, 17)),
                    Tint = tints[q],
                };
            }

            // The haze (EVOSIM_THEATRE_DEATH_HAZE): its puffs and specks, hashed by salts of their own, so the motes above are as they were.
            if (Haze) CaptureHaze(cloud, sized, parts, n, areas, scales, total, extent, tints, tinted, palette, on, linear);

            return cloud;
        }

        /// <summary>
        /// The haze's puffs and specks of one body's cloud (<see cref="Haze"/> only), born on the surfaces of its drawn parts by area as
        /// the motes are, and tinted as they are. Every number is hashed from the body's id, the index and salts no mote uses.
        /// </summary>
        private static void CaptureHaze(Cloud cloud, Phenotype sized, Transform[] parts, int n, float[] areas, float[] scales, float total,
            float extent, Color[] tints, bool[] tinted, TheatrePalette palette, bool on, bool linear)
        {
            long id = cloud.Id;
            Vector3 centre = cloud.Centre;
            float half = 0.5f * extent;
            float size = Mathf.InverseLerp(ExtentLow, ExtentHigh, extent);

            cloud.Puffs = new Puff[Mathf.RoundToInt(Mathf.Lerp(ByLapse(PuffCountLow, LapsePuffCountLow), ByLapse(PuffCountHigh, LapsePuffCountHigh), size))];
            cloud.Specks = new Speck[Mathf.RoundToInt(Mathf.Lerp(ByLapse(SpeckCountLow, LapseSpeckCountLow), ByLapse(SpeckCountHigh, LapseSpeckCountHigh), size))];

            // The cloud's own side of the body, hashed once: most puffs leave from there (salts 90 and 91, index 0).
            Vector3 side = Unit(Hash(id, 0, 90), Hash(id, 0, 91));

            for (int i = 0; i < cloud.Puffs.Length; i++)
            {
                int q = PartAt(total * Hash(id, i, 41), areas, parts, n);
                if (q < 0) continue;

                Vector3 birth = SurfaceAt(parts[q], sized, q, scales[q], id, i, 42);

                // Most puffs: the furthest toward the side of a few hashed surface points (salts 125 to 139), so the haze is one-sided.
                if (Hash(id, i, 55) < PuffSideShare)
                {
                    float best = Vector3.Dot(birth - centre, side);

                    for (int k = 1; k < PuffSideTries; k++)
                    {
                        int qk = PartAt(total * Hash(id, i, 120 + 5 * k), areas, parts, n);
                        if (qk < 0) continue;

                        Vector3 b = SurfaceAt(parts[qk], sized, qk, scales[qk], id, i, 121 + 5 * k);
                        float d = Vector3.Dot(b - centre, side);
                        if (d <= best) continue;

                        best = d;
                        birth = b;
                        q = qk;
                    }
                }

                Vector3 way = OutWay(birth - centre, id, i, 46);
                float end = LifeSeconds * Mathf.Lerp(PuffEndLow, 1f, Hash(id, i, 48));

                cloud.Puffs[i] = new Puff
                {
                    Offset = PuffInset * (centre - birth) + (birth - centre),
                    Out = way * (half * Mathf.Lerp(PuffDetachLow, PuffDetachHigh, Hash(id, i, 49))),
                    Radius = Mathf.Clamp(extent * Mathf.Lerp(PuffShareLow, PuffShareHigh, Mathf.Pow(Hash(id, i, 50), PuffSharePower)),
                        PuffRadiusLow, PuffRadiusHigh),
                    Carry = 1f + PuffCarrySpread * (2f * Hash(id, i, 56) - 1f),
                    Start = PuffStagger * Hash(id, i, 51),
                    End = end,
                    Span = Mathf.Max(1e-3f, PuffSpanShare * end),
                    Sink = PuffSinkMetresPerSecond * (1f + SinkJitter * (Hash(id, i, 52) - 0.5f)),
                    Seed = Hash(id, i, 53) * 97f,
                    Shade = Mathf.Lerp(PuffShadeLow, PuffShadeHigh, Hash(id, i, 54)),
                    Tint = PartTint(q, id, sized, tints, tinted, palette, on, linear),
                };
            }

            // The specks' radii: a power law truncated to [low, high], drawn by inverting its distribution; fine particulate at any size of body.
            const float low = SpeckRadiusLow;
            const float high = SpeckRadiusHigh;
            float ratio = Mathf.Pow(low / high, SpeckPower);

            for (int i = 0; i < cloud.Specks.Length; i++)
            {
                int q = PartAt(total * Hash(id, i, 61), areas, parts, n);
                if (q < 0) continue;

                Vector3 birth = SurfaceAt(parts[q], sized, q, scales[q], id, i, 62);
                Vector3 way = OutWay(birth - centre, id, i, 66);
                float end = LifeSeconds * Mathf.Lerp(SpeckEndLow, 1f, Hash(id, i, 68));
                float radius = low * Mathf.Pow(1f - Hash(id, i, 69) * (1f - ratio), -1f / SpeckPower);
                float big = Mathf.InverseLerp(low, high, radius);
                float period = Mathf.Lerp(WobbleLow, WobbleHigh, Hash(id, i, 70));

                cloud.Specks[i] = new Speck
                {
                    Offset = birth - centre,
                    Out = way * (SpeckSpreadHalfExtents * half * Mathf.Lerp(0.25f, 1f, Hash(id, i, 71))),
                    Radius = Mathf.Clamp(radius, SpeckRadiusLow, SpeckRadiusHigh),
                    Start = SpeckStagger * Hash(id, i, 72),
                    End = end,
                    Span = Mathf.Max(1e-3f, SpeckSpanShare * end),
                    Sink = Mathf.Lerp(SpeckSinkLow, SpeckSinkHigh, big) * (1f + SinkJitter * (Hash(id, i, 73) - 0.5f)),
                    Phase = new Vector3(6.2831853f * Hash(id, i, 74), 6.2831853f * Hash(id, i, 75), 6.2831853f * Hash(id, i, 76)),
                    Omega = 6.2831853f / period,
                    Seed = Hash(id, i, 77) * 97f,
                    Shade = Mathf.Lerp(SpeckShadeLow, SpeckShadeHigh, Hash(id, i, 78)),
                    Tint = PartTint(q, id, sized, tints, tinted, palette, on, linear),
                };
            }
        }

        /// <summary>The drawn part a pick in [0, total) falls on by area, as the motes pick theirs; -1 where it has no transform.</summary>
        private static int PartAt(float pick, float[] areas, Transform[] parts, int n)
        {
            int q = 0;
            float run = 0f;

            for (; q < n - 1; q++)
            {
                run += areas[q];
                if (pick < run) break;
            }

            while (q > 0 && parts[q] == null) q--;
            return parts[q] != null ? q : -1;
        }

        /// <summary>A hashed point on a part's surface in the world, from four salts starting at <paramref name="salt"/>.</summary>
        private static Vector3 SurfaceAt(Transform part, Phenotype sized, int q, float s, long id, int i, int salt)
        {
            Float3 h = sized.Parts[q].HalfExtents;
            Vector3 local = TheatreCrumble.SurfacePoint(Mathf.Abs(h.X) * s, Mathf.Abs(h.Y) * s, Mathf.Abs(h.Z) * s,
                Hash(id, i, salt), Hash(id, i, salt + 1), Hash(id, i, salt + 2), Hash(id, i, salt + 3));
            return part.position + part.rotation * local;
        }

        /// <summary>Outward from the body's centre along <paramref name="from"/>, leaning off it by a unit vector hashed from two salts.</summary>
        private static Vector3 OutWay(Vector3 from, long id, int i, int salt)
        {
            Vector3 way = (from.sqrMagnitude > 1e-12f ? from.normalized : Vector3.up) + SpreadScatter * Unit(Hash(id, i, salt), Hash(id, i, salt + 1));
            return way.sqrMagnitude > 1e-8f ? way.normalized : Vector3.up;
        }

        /// <summary>A part's dulled painted colour, worked out once a cloud, as the motes take theirs.</summary>
        private static Color PartTint(int q, long id, Phenotype sized, Color[] tints, bool[] tinted, TheatrePalette palette, bool on, bool linear)
        {
            if (!tinted[q])
            {
                tinted[q] = true;
                Color painted = palette != null ? palette.PaintedGuildOf(id, sized, q, on) : DullTissue;
                if (palette != null) painted = Dulled(painted);

                // The death look's haze (EVOSIM_THEATRE_DEATH_LOOK_HAZE): one pale warm grey for every part, in place of the browns.
                if (HazeGrey) painted = HazePale;

                painted.a = 1f;
                tints[q] = linear ? painted.linear : painted;
            }

            return tints[q];
        }

        /// <summary>A painted colour drained <see cref="GreyShare"/> of the way to its grey, at <see cref="BrightnessShare"/> of its brightness, in sRGB.</summary>
        private static Color Dulled(Color c)
        {
            Color drained = Color.Lerp(c, TheatrePalette.Grey(c), GreyShare);
            return new Color(drained.r * BrightnessShare, drained.g * BrightnessShare, drained.b * BrightnessShare, 1f);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>The one log line of a window, with the dial on: its deaths and its first cloud (or none, and why).</summary>
        private static void Announce(Cloud first, long id, double second, string none)
        {
            _logged = true;
            var inv = CultureInfo.InvariantCulture;
            string head = string.Format(inv, "[Theatre] death cue: {0} deaths in the window; first: ", _deaths);

            if (first != null)
            {
                Debug.Log(head + string.Format(inv, "{0} at {1:0.##} s, {2} motes", first.Id, first.First, first.Motes.Length));
            }
            else if (id >= 0L)
            {
                Debug.Log(head + string.Format(inv, "{0} at {1:0.##} s, 0 motes ({2})", id, second, none));
            }
            else
            {
                Debug.Log(head + "none");
            }
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

        /// <summary>A number in [0, 1) hashed from a body's id, a mote's index and a salt.</summary>
        private static float Hash(long id, int index, int salt)
        {
            unchecked
            {
                uint key = Mix((uint)id ^ Mix((uint)(id >> 32) + 0x9E3779B9U) ^ 0x2C1B3C6DU);
                uint h = Mix(key + (uint)index * 0x9E3779B9U + (uint)salt * 0x27D4EB2FU);
                return (h >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>A unit vector from two uniform numbers, uniform over the sphere.</summary>
        private static Vector3 Unit(float a, float b)
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

            // The haze (EVOSIM_THEATRE_DEATH_HAZE) draws through its own shader; with its dial off this line does nothing.
            if (_material == null && Haze && !MakeHazeMaterial()) return false;

            if (_material == null)
            {
                Shader shader = Shader.Find("Evosim/Theatre Crumble Flakes");

                if (shader == null)
                {
                    _noShader = true;
                    Debug.LogWarning("[Theatre] death cue: shader 'Evosim/Theatre Crumble Flakes' not found, so no cloud is drawn");
                    return false;
                }

                _material = new Material(shader) { name = "Theatre Death Cue", hideFlags = HideFlags.HideAndDontSave };

                // Premultiplied over what lies behind, as the wound plume's: the dark motes (uv1.w = 2) darken toward the cloud's colour.
                _material.SetFloat("_SrcBlend", (float)BlendMode.One);
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

                if (Motes > 0)
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] death cue: the motes (EVOSIM_THEATRE_DEATH_MOTES {0}): {1}, {2}",
                        Motes, Stained ? "stains of the water at their height" : "the drained paint, unlit",
                        Small ? string.Format(CultureInfo.InvariantCulture, "{0} to {1} m, {2} times as many",
                            SmallRadiusLow, SmallRadiusHigh, SmallCountFactor) : "at D281's size"));
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Theatre Death Cue", hideFlags = HideFlags.HideAndDontSave };
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
                _object = new GameObject("Theatre Death Cue");
                _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

                _renderer = _object.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                // Seen from inside the water only, as the shells, the flakes and the crumbs are, and turned off for the census views with them.
                _object.AddComponent<TheatreInsideOnly>();
            }

            if (_pass == null) _pass = new CuePass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };

            if (!_hooked)
            {
                RenderPipelineManager.beginCameraRendering += OnCamera;
                _hooked = true;
            }

            return true;
        }

        /// <summary>
        /// The haze's material (<see cref="Haze"/> only), held where the discs' would be, so the mesh, the object and the pass are the
        /// discs' own. Its shader blends premultiplied (One, OneMinusSrcAlpha) by itself. False when the shader cannot be found, said once.
        /// </summary>
        private static bool MakeHazeMaterial()
        {
            Shader shader = Shader.Find(HazeShaderName);

            if (shader == null)
            {
                _noShader = true;
                Debug.LogWarning("[Theatre] death cue: shader '" + HazeShaderName + "' not found, so no haze is drawn");
                return false;
            }

            _material = new Material(shader) { name = "Theatre Death Haze", hideFlags = HideFlags.HideAndDontSave };

            // The haze at 2: keyed to the water, its share read by the shader. At 1 the material keeps the shader's defaults.
            if (HazeKeyed)
            {
                _material.SetFloat("_HazeKeyed", 1f);
                _material.SetFloat("_HazeShare", HazeShare);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] death cue: the haze keyed to the water (EVOSIM_THEATRE_DEATH_HAZE 2), {0} of its colour, opacity raised {1} times",
                    HazeShare, HazeBoost));
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
                Debug.LogWarning("[Theatre] death cue: the cloud could not be built for this render: " + e.GetType().Name + ": " + e.Message);
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
                        clouds == _builtClouds && water;
            if (same) return;

            _builtCamera = camera;
            _builtSecond = _second;
            _builtEye = view.position;
            _builtRotation = view.rotation;
            _builtFov = camera.fieldOfView;
            _builtAspect = camera.aspect;
            _builtClouds = clouds;

            _positions.Clear();
            _corners.Clear();
            _data.Clear();
            _colours.Clear();
            _triangles.Clear();
            _drawMesh = null;

            if (clouds == 0 || !water || double.IsNaN(_second) || shells == null || _material == null)
            {
                if (shells == null && clouds > 0 && !_noShellsTold && skin != null)
                {
                    _noShellsTold = true;
                    Debug.Log("[Theatre] death cue: the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the cloud cannot be drawn in the water's box");
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
            _eye = camera.transform.position;

            bool open = skin.RunWaterOpen;
            int drawn = 0;

            for (int c = 0; c < _clouds.Count; c++)
            {
                Cloud cloud = _clouds[c];

                // EVOSIM_THEATRE_DEATH_NO_CLOUD: no cloud is drawn, and the drain and the death look go on.
                if (NoCloud)
                {
                    if (!_noCloudLogged)
                    {
                        _noCloudLogged = true;
                        Debug.Log("[Theatre] death cue: no cloud is drawn at a death (EVOSIM_THEATRE_DEATH_NO_CLOUD); the drain and the death look go on");
                    }

                    continue;
                }

                // The cloud's clock is the run's (LifeSeconds), so a time-lapse compresses it with the world; at 1x it is the film's.
                double ageWorld = _second - cloud.First;
                float ageFilm = (float)ageWorld;
                if (ageWorld < 0d || ageFilm >= LifeSeconds) continue;

                // The current at the body's centre at the death row: a function of the place and the second, read once.
                if (!cloud.CurrentKnown)
                {
                    cloud.Current = open ? skin.WaterAt(cloud.Centre, cloud.First) : Vector3.zero;
                    cloud.CurrentKnown = true;
                }

                // The haze (EVOSIM_THEATRE_DEATH_HAZE) in place of the discs. With its dial off this is never taken, and the discs
                // below are drawn as they always were.
                if (Haze)
                {
                    drawn += AddHaze(cloud, ageFilm, (float)ageWorld);
                    continue;
                }

                float spread = 1f - Mathf.Exp(-ageFilm / SpreadSeconds);
                float shown = Mathf.Clamp01(ageFilm / InSeconds);
                float easeIn = Mathf.Lerp(InFrom, 1f, shown * shown * (3f - 2f * shown));
                float age = (float)ageWorld;

                for (int i = 0; i < cloud.Motes.Length; i++)
                {
                    Mote m = cloud.Motes[i];
                    if (!(m.Radius > 0f)) continue;

                    // Dissolving over its own stretch before its own end: it shrinks and dims together, smoothly to nothing.
                    float left = Mathf.Clamp01((m.End - ageFilm) / m.Span);
                    float keep = left * left * (3f - 2f * left);
                    if (!(keep > 0f)) continue;

                    // Its birth point, the spread settling, the water it was released into and its sink, and a small wobble that starts at nothing.
                    Vector3 place = cloud.Centre + m.Offset + m.Out * spread + (cloud.Current + new Vector3(0f, -m.Sink, 0f)) * age;
                    place.x += WobbleMetres * (Mathf.Sin(m.Omega * ageFilm + m.Phase.x) - Mathf.Sin(m.Phase.x));
                    place.y += 0.5f * WobbleMetres * (Mathf.Sin(1.17f * m.Omega * ageFilm + m.Phase.y) - Mathf.Sin(m.Phase.y));
                    place.z += WobbleMetres * (Mathf.Sin(0.83f * m.Omega * ageFilm + m.Phase.z) - Mathf.Sin(m.Phase.z));

                    if (!InFrustum(place, Margin)) continue;

                    float radius = m.Radius * keep;
                    if (!(radius > 0f)) continue;

                    // The colour stays (its shade aside); the opacity eases in and thins as the mote dissolves.
                    var colour = new Color(m.Tint.r * m.Shade, m.Tint.g * m.Shade, m.Tint.b * m.Shade, CoreOpacity * keep * easeIn);

                    // w = 2: a dark mote, a soft round disc that darkens what lies behind it; w = 3 (EVOSIM_THEATRE_DEATH_MOTES 1 or 3):
                    // the same disc in a share of the water's own colour at its height, warmed by its tint.
                    AddQuad(place, new Vector4(radius, m.Seed, ageFilm, Stained ? 3f : 2f), colour);
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
        /// One cloud's haze at its age (<see cref="Haze"/> only): its puffs (<c>uv1.w = 0</c>) and specks (<c>uv1.w = 1</c>) as quads of
        /// the mesh, each eased in from its own start and dissolving to its own end. Returns the count of quads written, and writes the
        /// haze's one log line at its first.
        /// </summary>
        private static int AddHaze(Cloud cloud, float ageFilm, float age)
        {
            int drawn = 0;

            // The specks' spread settling, the water it was released into, and the way downstream (none in still water).
            float spread = 1f - Mathf.Exp(-ageFilm / PuffSpreadSeconds);
            float thinPower = ByLapse(PuffThinPower, LapsePuffThinPower);
            float opacity = ByLapse(PuffOpacity, LapsePuffOpacity);
            if (HazeKeyed) opacity = Mathf.Min(1f, opacity * HazeBoost);
            float speckOpacity = HazeKeyed ? Mathf.Min(1f, SpeckOpacity * HazeBoost) : SpeckOpacity;
            int puffsDrawn = 0;
            float radiusSum = 0f, alphaSum = 0f;
            Vector3 drift = cloud.Current * age;
            Vector3 down = Downstream(cloud.Current);

            Puff[] puffs = cloud.Puffs;
            for (int i = 0; puffs != null && i < puffs.Length; i++)
            {
                Puff p = puffs[i];
                if (!(p.Radius > 0f)) continue;

                // Eased in from its own start, dissolving before its own end: the haze gathers over several frames, never at once.
                float fade = Ease(Mathf.Clamp01((ageFilm - p.Start) / PuffInSeconds)) * Ease(Mathf.Clamp01((p.End - ageFilm) / p.Span));
                if (!(fade > 0f)) continue;

                // Detaching from the outline over its own age, eased out: it leaves the body, never holds to it, and grows and thins as it goes.
                float u = Mathf.Clamp01((ageFilm - p.Start) / PuffDetachSeconds);
                float detach = u * (2f - u);
                float grow = 1f + PuffGrow * detach;
                float thin = Mathf.Pow(1f / grow, thinPower);

                // Its way off bent downstream, and the current carrying it at its own share, so the puffs trail off in a stream.
                float radius = p.Radius * grow;
                Vector3 place = cloud.Centre + p.Offset + Bent(p.Out, down) * detach + drift * p.Carry + new Vector3(0f, -p.Sink * age, 0f);
                if (!InFrustum(place, PuffReach * radius + Margin)) continue;

                // Grown and thinned together, so the murk spreads rather than brightens or thickens.
                var colour = new Color(p.Tint.r * p.Shade, p.Tint.g * p.Shade, p.Tint.b * p.Shade, opacity * thin * fade);
                AddQuad(place, new Vector4(radius, p.Seed, ageFilm, 0f), colour);
                drawn++;
                puffsDrawn++;
                radiusSum += radius;
                alphaSum += colour.a;
            }

            Speck[] specks = cloud.Specks;
            for (int i = 0; specks != null && i < specks.Length; i++)
            {
                Speck s = specks[i];
                if (!(s.Radius > 0f)) continue;

                float fade = Ease(Mathf.Clamp01((ageFilm - s.Start) / SpeckInSeconds)) * Ease(Mathf.Clamp01((s.End - ageFilm) / s.Span));
                if (!(fade > 0f)) continue;

                Vector3 place = cloud.Centre + s.Offset + Bent(s.Out, down) * spread + drift + new Vector3(0f, -s.Sink * age, 0f);
                place.x += WobbleMetres * (Mathf.Sin(s.Omega * ageFilm + s.Phase.x) - Mathf.Sin(s.Phase.x));
                place.y += 0.5f * WobbleMetres * (Mathf.Sin(1.17f * s.Omega * ageFilm + s.Phase.y) - Mathf.Sin(s.Phase.y));
                place.z += WobbleMetres * (Mathf.Sin(0.83f * s.Omega * ageFilm + s.Phase.z) - Mathf.Sin(s.Phase.z));

                if (!InFrustum(place, Margin)) continue;

                // A speck keeps its size as it dissolves and only fades: a grain of matter does not shrink in the water.
                var colour = new Color(s.Tint.r * s.Shade, s.Tint.g * s.Shade, s.Tint.b * s.Shade, speckOpacity * fade);
                AddQuad(place, new Vector4(s.Radius, s.Seed, ageFilm, 1f), colour);
                drawn++;
            }

            // EVOSIM_THEATRE_DEATH_HAZE_LOG: what of this cloud's haze is in the frame, every half second of its age.
            if (HazeLog && drawn > 0 && (cloud.Centre - _eye).sqrMagnitude < HazeLogMetres * HazeLogMetres &&
                _hazeLogKeys.Count < HazeLogLines && _hazeLogKeys.Add((cloud.Id, Mathf.FloorToInt(2f * ageFilm))))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] death haze: body {0} age {1:0.00} s, {2:0.00} m from the eye; {3} of {4} puffs in the frame, mean radius {5:0.000} m, " +
                    "mean opacity {6:0.000}; {7} of {8} specks{9}",
                    cloud.Id, ageFilm, (cloud.Centre - _eye).magnitude, puffsDrawn, puffs != null ? puffs.Length : 0,
                    puffsDrawn > 0 ? radiusSum / puffsDrawn : 0f, puffsDrawn > 0 ? alphaSum / puffsDrawn : 0f,
                    drawn - puffsDrawn, specks != null ? specks.Length : 0, HazeKeyed ? ", keyed" : ""));
            }

            if (drawn > 0 && !_hazeLogged)
            {
                _hazeLogged = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] death cue: the haze is drawn in place of the discs (EVOSIM_THEATRE_DEATH_HAZE); first: {0} at {1:0.##} s, {2} puffs and {3} specks",
                    cloud.Id, cloud.First, puffs != null ? puffs.Length : 0, specks != null ? specks.Length : 0));
            }

            return drawn;
        }

        /// <summary>Smoothstep of a share already in [0, 1].</summary>
        private static float Ease(float u) => u * u * (3f - 2f * u);

        /// <summary>The unit direction of a current, or zero below <see cref="CurrentFloor"/> m/s.</summary>
        private static Vector3 Downstream(Vector3 current)
        {
            float speed = current.magnitude;
            return speed > CurrentFloor ? current / speed : Vector3.zero;
        }

        /// <summary>A way off, its length kept, its direction leant toward <paramref name="down"/> by <see cref="DownstreamLean"/>.</summary>
        private static Vector3 Bent(Vector3 way, Vector3 down)
        {
            float length = way.magnitude;
            if (!(length > 0f) || down == Vector3.zero) return way;

            Vector3 bent = way / length + DownstreamLean * down;
            return bent.sqrMagnitude > 1e-8f ? bent.normalized * length : way;
        }

        /// <summary>One camera-facing quad of the cloud's mesh: four corners and two triangles.</summary>
        private static void AddQuad(Vector3 place, Vector4 data, Color colour)
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

        private static bool InFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < _planes.Length; i++)
            {
                if (_planes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        /// <summary>Draws the cloud after the theatre's lens, as the birth cloud and the crumble's flakes are drawn: each mote carries its own blur.</summary>
        private sealed class CuePass : ScriptableRenderPass
        {
            public CuePass()
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

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre Death Cue", out PassData data))
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
