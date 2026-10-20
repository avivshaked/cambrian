using System;
using System.Globalization;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The underwater look (<c>logbook/specs/underwater-look/brief.md</c> and <c>brief-2.md</c>):
    /// eleven switches in two batches, each off by default, and the numbers they are drawn with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off means today's picture.</b> Every switch below is read once, when the skin is made,
    /// from its own <c>EVOSIM_THEATRE_*</c> dial or from the combined
    /// <c>EVOSIM_THEATRE_LOOK</c>, and with all six off nothing the skin pushes to a shader differs
    /// from what it pushed before the batch: the shaders take their old branch on a flag of zero.
    /// </para>
    /// <para>
    /// <b>The combined dial.</b> <c>EVOSIM_THEATRE_LOOK</c> is 0 to 4: 1 turns on the one sun and
    /// the water's absorption and in-scatter, 2 adds the light by depth and day and the marched
    /// shafts, 3 adds the snow around the camera and the sky from below, which is the whole first
    /// batch, and 4 adds the second batch (the fade into the backdrop, the tone step, the shafts in
    /// their place, the run's snow and its drift). An item's own dial, when it is set at all, wins over the combined one either way, so
    /// <c>EVOSIM_THEATRE_LOOK=3</c> with <c>EVOSIM_THEATRE_SNOW_SHELLS=0</c> is the batch without
    /// the new snow.
    /// </para>
    /// <para>
    /// <b>The look set.</b> Which defaults the tuning dials of the lens, the snow, the bodies, the rock
    /// and the grade start from is named by <c>EVOSIM_THEATRE_LOOK_SET</c> (<see cref="LookSet"/>):
    /// <c>fix</c> by default, and <c>today</c> for the numbers every earlier film was drawn with. Each of
    /// those dials takes its default through <see cref="ByLook"/>, and its own variable still wins.
    /// </para>
    /// <para>
    /// <b>Never on a record.</b> The census stills (side, end, top, iso) and the solo stills keep
    /// today's look whatever these say: <see cref="SnapshotCamera"/> holds the look off around a
    /// census render (<see cref="TheatreSkin.HoldTodaysLook"/>) and <c>TheatreSoloStill</c> builds
    /// its skin with <see cref="Off"/>.
    /// </para>
    /// </remarks>
    public sealed class TheatreLook
    {
        // ---------------------------------------------------------------- the six switches

        /// <summary>
        /// Item 1, one sun: everything the water shows (the window's disc, the caustics' shift, the
        /// shafts) is drawn from one world bearing fixed for the world, while the key stays behind
        /// each camera for modelling. <c>EVOSIM_THEATRE_ONE_SUN</c>.
        /// </summary>
        public bool OneSun;

        /// <summary>
        /// Item 2, the water you can see: every surface's fog is absorption per channel and a
        /// separate in-scatter brightened toward the sun. <c>EVOSIM_THEATRE_WATER_SCATTER</c>.
        /// </summary>
        public bool WaterScatter;

        /// <summary>
        /// Item 3, light by depth and by the run's own day: the key, the ambient and the leaf glow
        /// fall by the run's attenuation depth, red first, and scale with the run's day factor,
        /// with a floor; the exposure is compensated per shot. <c>EVOSIM_THEATRE_DEPTH_LIGHT</c>.
        /// </summary>
        public bool DepthLight;

        /// <summary>
        /// Item 4, shafts through the volume: a raymarched single-scattering pass in place of the
        /// nine quads. <c>EVOSIM_THEATRE_SHAFT_MARCH</c>.
        /// </summary>
        public bool ShaftMarch;

        /// <summary>
        /// Item 5, snow around the camera: three shells of motes lit by the shafts' light, in place
        /// of the particle system over the whole tank. <c>EVOSIM_THEATRE_SNOW_SHELLS</c>.
        /// </summary>
        public bool SnowShells;

        /// <summary>
        /// Item 6, the sky from below: an overcast from the day, a colour split at the window's
        /// rim, and a mirror that brightens toward the window. <c>EVOSIM_THEATRE_SKY_DAY</c>.
        /// </summary>
        public bool SkyDay;

        // ---------------------------------------------------------------- the second batch's switches
        // (logbook/specs/underwater-look/brief-2.md), every one off by default and on together at
        // EVOSIM_THEATRE_LOOK=4.

        /// <summary>
        /// Second batch, item 1: the water fades a thing into the open water seen behind it (the
        /// backdrop in that direction) over the extinction of absorption and scattering together,
        /// from <see cref="FadeStartMetres"/> on, in place of the first batch's in-scatter added
        /// over the kept colour. <c>EVOSIM_THEATRE_FADE_BACKDROP</c>. Needs item 2 of the first
        /// batch (the water's absorption) to be on, and does nothing without it.
        /// </summary>
        public bool FadeToBackdrop;

        /// <summary>
        /// Second batch, item 2: one tone curve after everything else, a black point and a gentle S
        /// on the luma with the hue kept (<c>TheatreToneStep</c>). <c>EVOSIM_THEATRE_TONE_STEP</c>.
        /// </summary>
        public bool ToneStep;

        /// <summary>
        /// Second batch, item 3: the marched shafts fewer and narrower, screened rather than added so
        /// they are strongest over dark water, and faint in front of a lit object.
        /// <c>EVOSIM_THEATRE_SHAFT_PLACE</c>. Acts on the marched shafts only (first batch item 4).
        /// </summary>
        public bool ShaftPlace;

        /// <summary>
        /// Second batch, items 4 to 6: the snow from the run. The shells' mote density follows the
        /// recorded column density, the bed carries the recorded floor stock as a pale dusting, and
        /// the label says where the snow came from. <c>EVOSIM_THEATRE_RUN_SNOW</c>.
        /// </summary>
        public bool RunSnow;

        /// <summary>
        /// Second batch, the owner's addition: the shell snow sinks at the run's own sink rate and
        /// drifts with the run's current at the camera, by the simulation's seconds, in place of
        /// the first batch's fall. <c>EVOSIM_THEATRE_SNOW_DRIFT</c>.
        /// </summary>
        public bool SnowDrift;

        /// <summary>True when any of the eleven is on.</summary>
        public bool Any => OneSun || WaterScatter || DepthLight || ShaftMarch || SnowShells || SkyDay || AnySecond;

        /// <summary>True when any of the second batch's five is on.</summary>
        public bool AnySecond => FadeToBackdrop || ToneStep || ShaftPlace || RunSnow || SnowDrift;

        /// <summary>True when the whole first batch is on.</summary>
        public bool AllFirst => OneSun && WaterScatter && DepthLight && ShaftMarch && SnowShells && SkyDay;

        /// <summary>True when the whole second batch is on.</summary>
        public bool AllSecond => FadeToBackdrop && ToneStep && ShaftPlace && RunSnow && SnowDrift;

        // ---------------------------------------------------------------- item 1

        /// <summary>
        /// The world sun's bearing, degrees from +x toward +z (<c>EVOSIM_THEATRE_SUN_AZIMUTH</c>).
        /// The default is the bearing of the shaders' own fallback sun, (0.28, 0.92, 0.27).
        /// </summary>
        public float SunAzimuthDegrees = TheatreSkin.Dial("EVOSIM_THEATRE_SUN_AZIMUTH", 44f, -360f, 360f);

        /// <summary>
        /// The world sun's elevation above the horizon, degrees (<c>EVOSIM_THEATRE_SUN_ELEVATION</c>,
        /// 15 to 90). Fixed: the run's day factor never reaches zero, so no sun sets.
        /// </summary>
        public float SunElevationDegrees = TheatreSkin.Dial("EVOSIM_THEATRE_SUN_ELEVATION", 67f, 15f, 90f);

        // ---------------------------------------------------------------- item 2

        /// <summary>
        /// The multiplier on pure water's absorption, 0.36, 0.07 and 0.01 per metre for red, green
        /// and blue (<c>EVOSIM_THEATRE_ABSORB</c>, 0 to 2, default 0.3): scaled down so the tank
        /// stays readable and a body at 3 m keeps its guild's hue.
        /// </summary>
        public float AbsorbScale = TheatreSkin.Dial("EVOSIM_THEATRE_ABSORB", 0.3f, 0f, 2f);

        /// <summary>
        /// The in-scatter's coefficient, per metre, one number for the three channels
        /// (<c>EVOSIM_THEATRE_SCATTER</c>, 0 to 1, default 0.02): separate from the absorption and
        /// lower than its red.
        /// </summary>
        public float ScatterPerMetre = TheatreSkin.Dial("EVOSIM_THEATRE_SCATTER", 0.02f, 0f, 1f);

        /// <summary>Henyey–Greenstein's asymmetry, forward peaked (<c>EVOSIM_THEATRE_PHASE_G</c>, 0 to 0.95).</summary>
        public float PhaseG = TheatreSkin.Dial("EVOSIM_THEATRE_PHASE_G", 0.8f, 0f, 0.95f);

        /// <summary>
        /// How much of the in-scatter follows the phase function rather than spreading evenly
        /// (<c>EVOSIM_THEATRE_PHASE_GAIN</c>, 0 to 1, default 0.12). At g 0.8 the normalised
        /// function is 45 straight at the sun and 0.06 straight away from it, so the whole of it
        /// would turn the window's direction white; at 0.12 the water toward the sun glows about
        /// six times the column's colour and the rest of the frame stays within a tenth of it.
        /// </summary>
        public float PhaseGain = TheatreSkin.Dial("EVOSIM_THEATRE_PHASE_GAIN", 0.12f, 0f, 1f);

        // ---------------------------------------------------------------- item 3

        /// <summary>
        /// What is left of the light at the bottom of the world, as a fraction of the surface's,
        /// before the day scales it (<c>EVOSIM_THEATRE_DEPTH_FLOOR</c>, 0 to 1, default 0.1). Under
        /// the day since the first stills, so a winter deep is dimmer than a summer one.
        /// </summary>
        public float DepthFloor = TheatreSkin.Dial("EVOSIM_THEATRE_DEPTH_FLOOR", 0.1f, 0f, 1f);

        /// <summary>
        /// The power the run's day factor is taken to before a picture uses it
        /// (<c>EVOSIM_THEATRE_DAY_GAMMA</c>, 0 to 1, default 0.5). At 1 a picture is as much
        /// brighter in round 57's summer than its winter as the run's light is, 2.82 against 0.34,
        /// eight times, which the first stills could not hold without blowing the summer or
        /// blacking the winter; at 0.5 it is the square root, about three times, and the order of
        /// the seasons is kept. 1 is the setting for the brief's literal check.
        /// </summary>
        public float DayGamma = TheatreSkin.Dial("EVOSIM_THEATRE_DAY_GAMMA", 0.5f, 0f, 1f);

        /// <summary>The day factor as a picture takes it: the run's, to <see cref="DayGamma"/>.</summary>
        public float PictureDay(float dayFactor) =>
            Mathf.Pow(Mathf.Max(0f, dayFactor), Mathf.Clamp01(DayGamma));

        /// <summary>
        /// How fast each channel falls against the run's attenuation depth: green at the run's own
        /// rate, red faster, blue a little slower. Not a dial: three numbers are tuned together or
        /// not at all.
        /// </summary>
        public Vector3 DepthRates = new Vector3(1.8f, 1.0f, 0.9f);

        /// <summary>
        /// How much of the light a subject has lost to its depth the exposure gives back, 0 (none)
        /// to 1 (all of it) (<c>EVOSIM_THEATRE_LOOK_METER</c>, default 0.75). Only for a view of a
        /// subject (the close view, the reef, a film's shots); 0 turns the whole meter off, which
        /// is the setting for the brief's check that a winter frame is darker than a summer one.
        /// </summary>
        public float MeterStrength = TheatreSkin.Dial("EVOSIM_THEATRE_LOOK_METER", 0.75f, 0f, 1f);

        /// <summary>
        /// How much of the day's departure from the mean the meter gives back
        /// (<c>EVOSIM_THEATRE_METER_DAY</c>, 0 to 0.9, default 0.5). Under one, so a summer frame
        /// stays brighter than a winter frame of the same view after the meter: everything the
        /// day lights is scaled by the whole picture day and the meter undoes only this share.
        /// </summary>
        public float MeterDayShare = TheatreSkin.Dial("EVOSIM_THEATRE_METER_DAY", 0.5f, 0f, 0.9f);

        /// <summary>The most the meter may open or close a view of a subject, EV.</summary>
        public const float MeterMostEv = 2f;

        /// <summary>
        /// The most the meter may open or close a view that frames the whole tank (the bed view),
        /// EV: toward the look before the batch and never past it (the first stills' bed view went
        /// from a mean luma of 37 to 153 on a meter of +3 EV).
        /// </summary>
        public const float MeterWideMostEv = 1f;

        // ---------------------------------------------------------------- item 4

        /// <summary>Steps along each view ray (<c>EVOSIM_THEATRE_MARCH_STEPS</c>, 32 to 64, default 48).</summary>
        public int MarchSteps = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_MARCH_STEPS", 48f, 32f, 64f));

        /// <summary>
        /// The marched shafts' strength, per metre of lit water (<c>EVOSIM_THEATRE_MARCH_STRENGTH</c>,
        /// 0 to 0.2, default 0.03; 0.01 drew only faint streaks in the first stills).
        /// </summary>
        public float MarchStrength = TheatreSkin.Dial("EVOSIM_THEATRE_MARCH_STRENGTH", 0.03f, 0f, 0.2f);

        // ---------------------------------------------------------------- item 5

        /// <summary>The shell snow's brightness (<c>EVOSIM_THEATRE_SHELL_GAIN</c>, 0 to 4, default 1).</summary>
        public float ShellGain = TheatreSkin.Dial("EVOSIM_THEATRE_SHELL_GAIN", ByLook(1f, 1f), 0f, 4f);

        // ---------------------------------------------------------------- second batch, item 1

        /// <summary>
        /// Where the fade starts, metres of water from the eye (<c>EVOSIM_THEATRE_FADE_START</c>,
        /// 0 to 10, default 3): nothing nearer loses anything to the water, and the fade grows
        /// from here to <see cref="FadeFullMetres"/> before it runs at its full rate.
        /// </summary>
        public float FadeStartMetres = TheatreSkin.Dial("EVOSIM_THEATRE_FADE_START", 3f, 0f, 10f);

        /// <summary>
        /// Where the fade reaches its full rate, metres of water from the eye
        /// (<c>EVOSIM_THEATRE_FADE_FULL</c>, 1 to 40, default 12; at least a metre past the start).
        /// With the full rate at twice the start, 6 m, the creature close-up's near rock at 5 to
        /// 10 m lost its colour and its shape to the water (iteration 11).
        /// </summary>
        public float FadeFullMetres = TheatreSkin.Dial("EVOSIM_THEATRE_FADE_FULL", 12f, 1f, 40f);

        /// <summary>
        /// The extinction's scattering part as a multiple of the first batch's in-scatter rate
        /// (<c>EVOSIM_THEATRE_FADE_SCATTER</c>, 0 to 10, default 0.5): the fade's blue, which the
        /// absorption barely touches, runs at this times <see cref="ScatterPerMetre"/>. Higher fades
        /// the bed view's far reefs into the backdrop; the black point comes from the backdrop's gain.
        /// </summary>
        public float FadeScatterScale = TheatreSkin.Dial("EVOSIM_THEATRE_FADE_SCATTER", 0.5f, 0f, 10f);

        /// <summary>
        /// How bright the open water is under the second batch, as a multiple of the column
        /// (<c>EVOSIM_THEATRE_WATER_GAIN</c>, 0.2 to 2, default 0.7): the backdrop and so the colour
        /// every far thing fades to. Under one keeps the black point down after the meter.
        /// </summary>
        public float WaterGain = TheatreSkin.Dial("EVOSIM_THEATRE_WATER_GAIN", 0.7f, 0.2f, 2f);

        // ---------------------------------------------------------------- second batch, item 2

        /// <summary>
        /// The black point, as a share of the display-encoded luma (<c>EVOSIM_THEATRE_BLACK_POINT</c>,
        /// 0 to 0.3, default 0.01): a pixel at or under it goes black, its hue kept.
        /// </summary>
        public float BlackPoint = TheatreSkin.Dial("EVOSIM_THEATRE_BLACK_POINT", 0.01f, 0f, 0.3f);

        /// <summary>
        /// The S's toe, the power under the pivot (<c>EVOSIM_THEATRE_TONE_S</c>, 1 to 2, default
        /// 1.2): 1 is a straight line after the black point. At 1.35 with the black point at 0.02 the
        /// creature close-up's near rock went to black on its shaded face (iteration 12).
        /// </summary>
        public float ToneS = TheatreSkin.Dial("EVOSIM_THEATRE_TONE_S", ByLook(1.2f, 1.3f), 1f, 2f);

        /// <summary>
        /// The S's shoulder, the power over the pivot (<c>EVOSIM_THEATRE_TONE_SHOULDER</c>, 1 to 2,
        /// default 1.1): gentler than the toe, so a summer window is not pushed to white.
        /// </summary>
        public float ToneShoulder = TheatreSkin.Dial("EVOSIM_THEATRE_TONE_SHOULDER", 1.1f, 1f, 2f);

        /// <summary>
        /// The S's pivot in display-encoded luma (<c>EVOSIM_THEATRE_TONE_PIVOT</c>, 0.1 to 0.7,
        /// default 0.2): under it darker, over it brighter. Low because these frames are dark.
        /// </summary>
        public float TonePivot = TheatreSkin.Dial("EVOSIM_THEATRE_TONE_PIVOT", 0.2f, 0.1f, 0.7f);

        // ---------------------------------------------------------------- second batch, item 3

        /// <summary>
        /// The share of the surface that sends a shaft down (<c>EVOSIM_THEATRE_SHAFT_SHARE</c>, 0.05
        /// to 1, default 0.25): a slow mask over the surface, so most of the water between beams is
        /// left to the in-scatter.
        /// </summary>
        public float ShaftShare = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_SHARE", 0.25f, 0.05f, 1f);

        /// <summary>
        /// The longest march under item 3, metres from where the look enters the water
        /// (<c>EVOSIM_THEATRE_SHAFT_REACH</c>, 4 to 200, default 30; the first batch's is 40), faded
        /// over its last two fifths.
        /// </summary>
        public float ShaftReachMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_REACH", 30f, 4f, 200f);

        /// <summary>
        /// The size of the stretches of surface over which the sheets' strength varies under item 3,
        /// metres (<c>EVOSIM_THEATRE_SHAFT_PATCH</c>, 1 to 40, default 12), so one stretch of sheets
        /// is bright and the next faint.
        /// </summary>
        public float ShaftPatchMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_PATCH", 12f, 1f, 40f);

        /// <summary>
        /// The size of the patch of surface one sheet of light comes down from under item 3, metres
        /// (<c>EVOSIM_THEATRE_SHAFT_SHEET</c>, 0.5 to 20, default 2.5): the low frequency the sheets are
        /// read at, in place of the 1.6 m wave's curvature, which drew a regular comb.
        /// </summary>
        public float ShaftSheetMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_SHEET", 2.5f, 0.5f, 20f);

        /// <summary>
        /// The depth over which the sheets fade by an e-fold under item 3, metres
        /// (<c>EVOSIM_THEATRE_SHAFT_DEPTH</c>, 1 to 100, default 12), so they are strongest near the
        /// surface.
        /// </summary>
        public float ShaftDepthMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_DEPTH", 12f, 1f, 100f);

        /// <summary>
        /// The share of a sheet that the waves' focusing at its surface sets under item 3
        /// (<c>EVOSIM_THEATRE_SHAFT_SHIMMER</c>, 0 to 1, default 0.35): fine streaks that move through
        /// the sheet with the waves. At 1 the sheets carry the comb they replaced.
        /// </summary>
        public float ShaftShimmer = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_SHIMMER", 0.35f, 0f, 1f);

        /// <summary>
        /// The depth over which a sheet comes in under the surface under item 3, metres
        /// (<c>EVOSIM_THEATRE_SHAFT_ONSET</c>, 0.1 to 20, default 4), so a sheet seen meeting the
        /// surface fades out rather than ending in a round glow.
        /// </summary>
        public float ShaftOnsetMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_ONSET", 4f, 0.1f, 20f);

        /// <summary>
        /// The power a sheet is drawn at under item 3 (<c>EVOSIM_THEATRE_SHAFT_NARROW</c>, 1 to 12,
        /// default 2.5): higher is narrower and harder at the sides.
        /// </summary>
        public float ShaftNarrow = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_NARROW", 2.5f, 1f, 12f);

        /// <summary>
        /// The debug view of item 3 (<c>EVOSIM_THEATRE_SHAFT_DEBUG</c>, 0 off, 1 on, 2 on without the caps): the march draws
        /// the shaft term alone, opaque and four times as bright, over a black frame, so the caps'
        /// shade can be checked. Never for a picture anyone keeps.
        /// </summary>
        public float ShaftDebug = Mathf.Round(TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_DEBUG", 0f, 0f, 2f));

        /// <summary>
        /// What is left of a shaft in front of a thing the march stops at
        /// (<c>EVOSIM_THEATRE_SHAFT_OVER_OBJECT</c>, 0 to 1, default 0.4).
        /// </summary>
        public float ShaftOverObject = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_OVER_OBJECT", 0.4f, 0f, 1f);

        /// <summary>
        /// The marched shafts' strength under item 3, per metre (<c>EVOSIM_THEATRE_SHAFT_PLACE_STRENGTH</c>,
        /// 0 to 0.4, default 0.2): the sheets are sparse and soft at the sides, so each carries more.
        /// </summary>
        public float ShaftPlaceStrength = TheatreSkin.Dial("EVOSIM_THEATRE_SHAFT_PLACE_STRENGTH", 0.2f, 0f, 0.4f);

        // ---------------------------------------------------------------- second batch, items 4 to 6

        /// <summary>
        /// The column density the shells' mote density is measured against, J per cubic metre
        /// (<c>EVOSIM_THEATRE_SNOW_REFERENCE</c>, default 1). Fixed, never rescaled per frame, so a
        /// film across a year shows the real change and two rounds compare.
        /// </summary>
        public float SnowReference = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_REFERENCE", 1f, 0.01f, 100f);

        /// <summary>
        /// The floor cell's stock the bed's dusting is measured against, J
        /// (<c>EVOSIM_THEATRE_FLOOR_REFERENCE</c>, default 2; round 57's median is 1.3 to 1.7).
        /// </summary>
        public float FloorReference = TheatreSkin.Dial("EVOSIM_THEATRE_FLOOR_REFERENCE", 2f, 0.01f, 100f);

        /// <summary>
        /// How pale the bed goes at the reference stock (<c>EVOSIM_THEATRE_FLOOR_DUST</c>, 0 to 1,
        /// default 0.35), and in proportion either side of it up to twice that.
        /// </summary>
        public float FloorDust = TheatreSkin.Dial("EVOSIM_THEATRE_FLOOR_DUST", 0.35f, 0f, 1f);

        /// <summary>
        /// How many motes the shells hold, as a multiple of each shell's own share of cells
        /// (<c>EVOSIM_THEATRE_SNOW_QUANTITY</c>, 0.25 to 3, default 1), capped at 0.95 of the cells.
        /// The owner asked on 2026-10-11 for more snow in round 57's film, whose 4K shots near the bed
        /// showed almost none.
        /// </summary>
        public float SnowQuantity = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_QUANTITY", ByLook(1f, 1.25f), 0.25f, 3f);

        /// <summary>
        /// The least share of the reference density a column counts as for the shells' motes
        /// (<c>EVOSIM_THEATRE_SNOW_LEAST</c>, 0 to 1, default 0), so water the run left thin in snow
        /// still shows some. The bed's dusting is untouched.
        /// </summary>
        public float SnowLeast = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_LEAST", 0f, 0f, 1f);

        /// <summary>
        /// How much brighter than the open water behind it a mote stands, from the water's own light
        /// over and above the sun's (<c>EVOSIM_THEATRE_SNOW_AMBIENT</c>, 0 to 6, default 0): a share of
        /// the water's brightness at the mote's height, in the mote's colour. Near round 57's bed the sun left at a mote's
        /// depth was too little to show it against the bright water, at any gain or quantity (2026-10-11).
        /// </summary>
        public float SnowAmbient = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_AMBIENT", 0f, 0f, 6f);

        /// <summary>
        /// The snow through a lens (<c>EVOSIM_THEATRE_SNOW_BLUR</c>, 1 on, default 0): every mote blurred
        /// by a thin lens's circle of confusion at its depth, sharp only near the focus. The owner observed
        /// on 2026-10-19 that real marine snow is soft near the lens and far from it and sharp only near
        /// the focus, and that this is what gives it depth; the shells drew every mote sharp.
        /// </summary>
        public bool SnowBlur = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_BLUR", ByLook(0f, 1f), 0f, 1f) >= 0.5f;

        /// <summary>The snow's lens's f-number (<c>EVOSIM_THEATRE_SNOW_FSTOP</c>, 1 to 22, default 2), on the grade's frame.</summary>
        public float SnowFStop = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_FSTOP", 2f, 1f, 22f);

        /// <summary>
        /// The snow's focus, m along the view axis (<c>EVOSIM_THEATRE_SNOW_FOCUS</c>, default 0): 0 takes a
        /// portrait's own focus, and otherwise the scene's depth at the frame's centre.
        /// </summary>
        public float SnowFocusMetres = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_FOCUS", 0f, 0f, 100f);

        /// <summary>The most a mote is blurred, pixels of radius at 1080 lines (<c>EVOSIM_THEATRE_SNOW_BLUR_CAP</c>, default 24).</summary>
        public float SnowBlurCap = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_BLUR_CAP", 24f, 1f, 200f);

        /// <summary>
        /// The power of a blurred mote's area ratio its brightness keeps (<c>EVOSIM_THEATRE_SNOW_BLUR_KEEP</c>,
        /// 0 to 1, default 1): 1 spreads its light over the larger disc and conserves it, as a lens does;
        /// less keeps a soft mote brighter than its light allows.
        /// </summary>
        public float SnowBlurKeep = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_BLUR_KEEP", 1f, 0f, 1f);

        /// <summary>
        /// The spread of the motes' sizes (<c>EVOSIM_THEATRE_SNOW_SIZE_SPREAD</c>, 0 to 1.2, default 0): the
        /// standard deviation of their log radius, with the mean area kept.
        /// </summary>
        public float SnowSizeSpread = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_SIZE_SPREAD", ByLook(0f, 0.6f), 0f, 1.2f);

        /// <summary>The share of motes that are large flakes (<c>EVOSIM_THEATRE_SNOW_BIG_SHARE</c>, 0 to 0.5, default 0).</summary>
        public float SnowBigShare = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_BIG_SHARE", ByLook(0f, 0.08f), 0f, 0.5f);

        /// <summary>How many times larger a large flake is (<c>EVOSIM_THEATRE_SNOW_BIG_FACTOR</c>, 1 to 8, default 3).</summary>
        public float SnowBigFactor = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_BIG_FACTOR", ByLook(3f, 2.5f), 1f, 8f);

        /// <summary>How irregular a flake's outline is (<c>EVOSIM_THEATRE_SNOW_SHAPE</c>, 0 round to 1, default 0).</summary>
        public float SnowShape = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_SHAPE", ByLook(0f, 0.6f), 0f, 1f);

        /// <summary>How soft a flake's edge is, as a share of its radius (<c>EVOSIM_THEATRE_SNOW_FLUFF</c>, 0 to 2, default 0).</summary>
        public float SnowFluff = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_FLUFF", ByLook(0f, 1f), 0f, 2f);

        /// <summary>
        /// The theatre's own lens on the bodies and the scenery (<c>EVOSIM_THEATRE_LENS</c>, 1 on, default 0;
        /// D208, item 1): depth of field from the shot's focus through a thin lens, in place of URP's own,
        /// whose blur is capped at 14 pixels of the render target (<see cref="TheatreLens"/>). With it on, the
        /// snow takes the grade's aperture rather than <see cref="SnowFStop"/>, so the two are one lens.
        /// </summary>
        public bool Lens = TheatreSkin.Dial("EVOSIM_THEATRE_LENS", ByLook(0f, 1f), 0f, 1f) >= 0.5f;

        /// <summary>
        /// The snow keeps each flake's light (<c>EVOSIM_THEATRE_SNOW_CONSERVE</c>, 1 on, default 0): a flake the lens
        /// or the fluff spreads is drawn as much fainter as it is larger, its light spread thinly over the area
        /// it covers, as a lens spreads it. The owner's note of 2026-10-19: blurred flakes that kept their
        /// brightness read as a snowfall.
        /// </summary>
        public bool SnowConserve = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_CONSERVE", ByLook(0f, 1f), 0f, 1f) >= 0.5f;

        /// <summary>
        /// How far a large flake is a loose aggregate (<c>EVOSIM_THEATRE_SNOW_AGGREGATE</c>, 0 to 2, default 0): its
        /// brightness divided by its size over its shell's to this power, so at 1 its light grows with its size
        /// and not its area.
        /// </summary>
        public float SnowAggregate = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_AGGREGATE", ByLook(0f, 1f), 0f, 2f);

        /// <summary>
        /// The bodies obey the water (<c>EVOSIM_THEATRE_BODY_WATER</c>, 0 to 1, default 0): their guild rim and
        /// inner glow take the light left at their depth on the run's day, red first, as the key and the ambient
        /// do, in place of shining at full strength wherever they are. The owner's note of 2026-10-19: the bodies
        /// read as self-lit.
        /// </summary>
        public float BodyWater = TheatreSkin.Dial("EVOSIM_THEATRE_BODY_WATER", ByLook(0f, 1f), 0f, 1f);

        /// <summary>How much of the bodies' guild rim is taken away (<c>EVOSIM_THEATRE_BODY_RIM_CUT</c>, 0 to 1, default 0).</summary>
        public float BodyRimCut = TheatreSkin.Dial("EVOSIM_THEATRE_BODY_RIM_CUT", ByLook(0f, 0.4f), 0f, 1f);

        /// <summary>How much of the bodies' inner glow is taken away (<c>EVOSIM_THEATRE_BODY_GLOW_CUT</c>, 0 to 1, default 0).</summary>
        public float BodyGlowCut = TheatreSkin.Dial("EVOSIM_THEATRE_BODY_GLOW_CUT", ByLook(0f, 0.5f), 0f, 1f);

        /// <summary>How much of the light through the bodies' tissue is taken away (<c>EVOSIM_THEATRE_BODY_TRANS_CUT</c>, 0 to 1, default 0).</summary>
        public float BodyTransCut = TheatreSkin.Dial("EVOSIM_THEATRE_BODY_TRANS_CUT", ByLook(0f, 0.3f), 0f, 1f);

        /// <summary>The lens's largest blur, pixels of radius at 1080 lines (<c>EVOSIM_THEATRE_LENS_CAP</c>, 1 to 120, default 24).</summary>
        public float LensBlurCap = TheatreSkin.Dial("EVOSIM_THEATRE_LENS_CAP", 24f, 1f, 120f);

        /// <summary>
        /// A specular glint on the reef's rock (<c>EVOSIM_THEATRE_ROCK_GLINT</c>, 0 to 0.5, default 0; D208, item
        /// 3): the sun's highlight off the wet stone, as a reflectance at the lobe's peak, brightest where the
        /// caustic net lands. HDRP drew it in D202's first test and its stone read richer for it.
        /// </summary>
        public float RockGlint = TheatreSkin.Dial("EVOSIM_THEATRE_ROCK_GLINT", ByLook(0f, 0.1f), 0f, 0.5f);

        /// <summary>The glint's roughness (<c>EVOSIM_THEATRE_ROCK_GLINT_ROUGHNESS</c>, 0.05 to 1, default 0.35): smaller is a tighter, sharper highlight.</summary>
        public float RockGlintRoughness = TheatreSkin.Dial("EVOSIM_THEATRE_ROCK_GLINT_ROUGHNESS", ByLook(0.35f, 0.5f), 0.05f, 1f);

        // ---------------------------------------------------------------- second batch, the drift

        /// <summary>
        /// The snow's speed while the world stands still (a frozen film, a still), as a share of
        /// the current at the camera, per second of the shaders' clock
        /// (<c>EVOSIM_THEATRE_SNOW_ALIVE</c>, 0 to 1, default 0.25), with a floor of a centimetre a
        /// second so still water still drifts.
        /// </summary>
        public float SnowAlive = TheatreSkin.Dial("EVOSIM_THEATRE_SNOW_ALIVE", 0.25f, 0f, 1f);

        // ---------------------------------------------------------------- reading

        // ---------------------------------------------------------------- the look set

        /// <summary>
        /// The named set of defaults the look dials start from, read once from
        /// <c>EVOSIM_THEATRE_LOOK_SET</c>: <c>fix</c> or <c>today</c>, which draws what every film and frame
        /// made before the set existed was drawn with. Unset or empty, a film (the look on,
        /// <c>EVOSIM_THEATRE_LOOK</c> at 1 or more) takes <c>fix</c> and a census or solo still <c>today</c>.
        /// Anything else is said once in the log and read as <c>fix</c>. A dial's own environment variable
        /// still wins over the set, set or not, exactly as it did before.
        /// </summary>
        public static string LookSet { get; } = ReadLookSet();

        /// <summary>True while <see cref="Off"/> builds a look, so its dials take today's defaults whatever the set says.</summary>
        [ThreadStatic] private static bool _buildingOff;

        /// <summary>The set this look's dials took their defaults from, for the log.</summary>
        public readonly string Set = _buildingOff ? "today" : LookSet;

        private static string ReadLookSet()
        {
            // Unset, the fix is the default only where the film's look is on (EVOSIM_THEATRE_LOOK at 1 or
            // more, which every film sets): a census or solo still is a record picture, compared with every
            // one taken before it, and keeps today's look unless the set is named.
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_LOOK_SET");
            if (string.IsNullOrWhiteSpace(text))
                return TheatreSkin.Dial("EVOSIM_THEATRE_LOOK", 0f, 0f, 4f) >= 1f ? "fix" : "today";

            string set = text.Trim().ToLowerInvariant();
            if (set == "fix" || set == "today") return set;

            Debug.LogWarning("[Theatre] EVOSIM_THEATRE_LOOK_SET is not 'fix' or 'today' ('" + text + "'); using fix.");
            return "fix";
        }

        /// <summary>
        /// A look dial's default for the set in force: <paramref name="today"/> in the <c>today</c> set,
        /// <paramref name="fix"/> in the <c>fix</c> set. The dial's own variable still wins over either.
        /// </summary>
        internal static float ByLook(float today, float fix) => _buildingOff || LookSet == "today" ? today : fix;

        /// <summary>
        /// Every switch off: the look before this batch. What the solo still uses, so its dials take
        /// today's defaults whatever the set says, as the solo still always has.
        /// </summary>
        public static TheatreLook Off
        {
            get
            {
                bool was = _buildingOff;
                _buildingOff = true;
                try { return new TheatreLook(); }
                finally { _buildingOff = was; }
            }
        }

        /// <summary>The switches as the environment sets them.</summary>
        public static TheatreLook FromEnvironment()
        {
            int combined = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_LOOK", 0f, 0f, 4f));

            return new TheatreLook
            {
                OneSun = Switch("EVOSIM_THEATRE_ONE_SUN", combined >= 1),
                WaterScatter = Switch("EVOSIM_THEATRE_WATER_SCATTER", combined >= 1),
                DepthLight = Switch("EVOSIM_THEATRE_DEPTH_LIGHT", combined >= 2),
                ShaftMarch = Switch("EVOSIM_THEATRE_SHAFT_MARCH", combined >= 2),
                SnowShells = Switch("EVOSIM_THEATRE_SNOW_SHELLS", combined >= 3),
                SkyDay = Switch("EVOSIM_THEATRE_SKY_DAY", combined >= 3),
                FadeToBackdrop = Switch("EVOSIM_THEATRE_FADE_BACKDROP", combined >= 4),
                ToneStep = Switch("EVOSIM_THEATRE_TONE_STEP", combined >= 4),
                ShaftPlace = Switch("EVOSIM_THEATRE_SHAFT_PLACE", combined >= 4),
                RunSnow = Switch("EVOSIM_THEATRE_RUN_SNOW", combined >= 4),
                SnowDrift = Switch("EVOSIM_THEATRE_SNOW_DRIFT", combined >= 4),
            };
        }

        /// <summary>An item's own dial when it is set, the combined dial's answer otherwise.</summary>
        private static bool Switch(string name, bool fromCombined)
        {
            string text = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(text)) return fromCombined;

            return TheatreSkin.Dial(name, fromCombined ? 1f : 0f, 0f, 1f) >= 0.5f;
        }

        /// <summary>Unit, pointing at the world's sun from the water (item 1).</summary>
        public Vector3 FixedSun()
        {
            float azimuth = SunAzimuthDegrees * Mathf.Deg2Rad;
            float elevation = Mathf.Clamp(SunElevationDegrees, 15f, 90f) * Mathf.Deg2Rad;
            float flat = Mathf.Cos(elevation);

            return new Vector3(flat * Mathf.Cos(azimuth), Mathf.Sin(elevation), flat * Mathf.Sin(azimuth)).normalized;
        }

        /// <summary>
        /// Absorption per metre for red, green and blue: pure water's ratio times
        /// <see cref="AbsorbScale"/>.
        /// </summary>
        public Vector3 AbsorptionPerMetre => AbsorbScale * new Vector3(0.36f, 0.07f, 0.01f);

        /// <summary>One line for the log: which switches are on and the numbers they use.</summary>
        public string Describe()
        {
            string set = "look set " + Set + "; ";
            if (!Any) return set + "underwater look: off (every switch at its default)";

            Vector3 a = AbsorptionPerMetre;
            return set + string.Format(
                CultureInfo.InvariantCulture,
                "underwater look: one sun {0}, water scatter {1}, depth light {2}, shaft march {3}, snow shells {4}, sky day {5}; " +
                "sun az {6:0.#} el {7:0.#} deg; sigma_abs {8:0.####}/{9:0.####}/{10:0.####} per m, sigma_bs {11:0.####} per m, g {12:0.##}, phase gain {13:0.##}; " +
                "depth floor {14:0.###}, day gamma {19:0.##}, meter {15:0.##} depth / {20:0.##} day; march {16} steps at {17:0.####}; shell gain {18:0.##}",
                On(OneSun), On(WaterScatter), On(DepthLight), On(ShaftMarch), On(SnowShells), On(SkyDay),
                SunAzimuthDegrees, SunElevationDegrees, a.x, a.y, a.z, ScatterPerMetre, PhaseG, PhaseGain,
                DepthFloor, MeterStrength, MarchSteps, MarchStrength, ShellGain, DayGamma, MeterDayShare) +
                (AnySecond ? "; " + DescribeSecond() : "");
        }

        /// <summary>The second batch's switches and numbers, for the log.</summary>
        public string DescribeSecond() => string.Format(
            CultureInfo.InvariantCulture,
            "second batch: fade to backdrop {0} (from {1:0.#} m, full at {20:0.#} m, scatter x{2:0.##}, water gain {3:0.##}), " +
            "tone step {4} (black {5:0.###}, toe {6:0.##} shoulder {19:0.##} about {7:0.##}), shaft place {8} (share {9:0.##}, power {10:0.#}, " +
            "sheet {21:0.#} m, patch {22:0.#} m, depth {23:0.#} m, shimmer {25:0.##}, onset {26:0.#} m, over object {11:0.##}, strength {12:0.###}{24}), " +
            "run snow {13} (reference {14:0.##} J/m3, floor {15:0.##} J, dust {16:0.##}, quantity x{27:0.##}, least {28:0.##}, ambient {29:0.##}), " +
            "snow drift {17} (alive {18:0.##})",
            On(FadeToBackdrop), FadeStartMetres, FadeScatterScale, WaterGain,
            On(ToneStep), BlackPoint, ToneS, TonePivot,
            On(ShaftPlace), ShaftShare, ShaftNarrow, ShaftOverObject, ShaftPlaceStrength,
            On(RunSnow), SnowReference, FloorReference, FloorDust,
            On(SnowDrift), SnowAlive, ToneShoulder, FadeFullMetres,
            ShaftSheetMetres, ShaftPatchMetres, ShaftDepthMetres, ShaftDebug > 0.5f ? ", DEBUG " + ShaftDebug.ToString("0", CultureInfo.InvariantCulture) + ": the shaft term alone" : "",
            ShaftShimmer, ShaftOnsetMetres, SnowQuantity, SnowLeast, SnowAmbient) + ", " + DescribeSnowLens();

        /// <summary>The snow's lens and flakes, for the log.</summary>
        public string DescribeSnowLens() => string.Format(
            CultureInfo.InvariantCulture,
            "snow lens {0} (f/{1:0.#}, focus {2}, cap {3:0} px, keep {4:0.##}), flakes (spread {5:0.##}, large {6:0.###} x{7:0.#}, shape {8:0.##}, fluff {9:0.##})",
            On(SnowBlur), SnowFStop, SnowFocusMetres > 0f ? SnowFocusMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m" : "the shot's",
            SnowBlurCap, SnowBlurKeep, SnowSizeSpread, SnowBigShare, SnowBigFactor, SnowShape, SnowFluff) +
            string.Format(CultureInfo.InvariantCulture, ", lens {0} (cap {1:0} px), rock glint {2:0.###} (roughness {3:0.##}), " +
                "snow conserve {4}, aggregate {5:0.##}, bodies obey the water {6:0.##} (rim cut {7:0.##}, glow cut {8:0.##}, through cut {9:0.##})",
                On(Lens), LensBlurCap, RockGlint, RockGlintRoughness, On(SnowConserve), SnowAggregate,
                BodyWater, BodyRimCut, BodyGlowCut, BodyTransCut);

        /// <summary>
        /// The look's token for a picture's label: the grade's own (<c>look 2</c>) with every switch
        /// off or the look held for a census view; <c>look 3</c> with the whole batch on; and the
        /// grade's token with the items that are on otherwise, as <c>look 2+sun,water</c>.
        /// </summary>
        public string LabelToken(string gradeToken, bool held)
        {
            if (held || !Any) return gradeToken;
            if (AllFirst && !AnySecond) return "look 3";
            if (AllFirst && AllSecond) return "look 4";

            var items = new System.Collections.Generic.List<string>(11);
            if (OneSun) items.Add("sun");
            if (WaterScatter) items.Add("water");
            if (DepthLight) items.Add("depth");
            if (ShaftMarch) items.Add("shafts");
            if (SnowShells) items.Add("snow");
            if (SkyDay) items.Add("sky");
            if (FadeToBackdrop) items.Add("fade");
            if (ToneStep) items.Add("tone");
            if (ShaftPlace) items.Add("place");
            if (RunSnow) items.Add("runsnow");
            if (SnowDrift) items.Add("drift");

            return gradeToken + "+" + string.Join(",", items.ToArray());
        }

        private static string On(bool on) => on ? "on" : "off";
    }
}
