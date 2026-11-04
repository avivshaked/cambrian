using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The genome-to-skin map (<c>EVOSIM_THEATRE_SKIN_MAP</c>, 0 off by default; <c>logbook/specs/theatre-skin-plan.md</c>,
    /// section 5): ten numbers in [0, 1] per genome, each a smooth function of features of the genome and its adult body, which
    /// the palette reads in place of the plan hash's choices and the investment and margin inputs. Theatre only: nothing here is
    /// read by the simulation. Off, the class creates nothing, computes nothing and sets nothing; the palette's hooks are tests
    /// of <see cref="Enabled"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The vector.</b> The port of <c>scratch/skin-map/skin_features.py</c>'s <c>features_of</c>, <c>brain_summary</c> and
    /// <c>skin_vectors</c>, on Core's own development in place of the Python's. A feature is standardised by the reference
    /// population's mean and spread (<see cref="TheatreSkinMapConstants"/>, a generated file naming the run and the second),
    /// each brain_w_* z shrinks by n / (n + <see cref="TheatreSkinMapConstants.BrainShrinkLinks"/>) for a brain of n
    /// connections, every z is clipped to plus or minus <see cref="TheatreSkinMapConstants.ZClip"/>, and a parameter is the
    /// logistic of its bias and weighted z inputs (0.5 with none). No hash and no founder seed enters a parameter; the pattern's
    /// noise phase keeps its plan hash.
    /// </para>
    /// <para>
    /// <b>Once per genome.</b> <see cref="Register"/> develops the genome at its adult size under the run's own limits (the
    /// genome's own module counts, as the Python does), computes the features and the vector and keeps both by the body's id.
    /// A growth step changes no parameter: none reads absolute size. A body never registered, or whose genome does not develop,
    /// has no entry and is drawn as it is without the dial.
    /// </para>
    /// <para>
    /// <b>The hybrid (the dial at 2).</b> At 1 the map is the pooled fit's, unchanged. At 2 two of the ten values are replaced
    /// once the rest are computed: the base hue becomes the middle rank, among the fitting set's sorted lineage scores
    /// (<see cref="TheatreSkinMapConstants.HybridFittingScores"/>), of this genome's lineage score, a weighted sum of the same
    /// clipped, shrunk z values (<see cref="TheatreSkinMapConstants.HybridWeights"/>) over every feature but the three diet
    /// shares; and the base saturation becomes the mean of today's saturation and today's base hue, which is the diet's
    /// logistic. The numbers come from <c>hue_screen.py</c>'s hybrid through <c>hybrid.json</c>
    /// (<see cref="TheatreSkinMapConstants.HybridRule"/>). The palette and every other use of the vector are untouched.
    /// </para>
    /// <para>
    /// <b>Full colour (the dial at 3).</b> Mode 2's map, and two things more. The base hue is put on the whole wheel
    /// (<see cref="WheelStartDegrees"/>) in place of the marine path; and each entry carries a value rank, the middle rank of a
    /// second lineage score (<see cref="TheatreSkinMapConstants.ValueWeights"/>: the generalised eigenvector with the second
    /// largest eigenvalue of the same problem as the hue score, so it is uncorrelated with the hue score over the fitting set)
    /// among <see cref="TheatreSkinMapConstants.ValueFittingScores"/>. The value rank moves a body colour's lightness only
    /// while <c>EVOSIM_THEATRE_SKIN_MAP_VALUE</c> is over zero (<see cref="Valued"/>). It is not one of the ten values.
    /// </para>
    /// <para>
    /// <b>The consumers</b> are in <c>TheatrePalette</c> (the tint's hue and saturation, the carve's depth, the iridescence's
    /// strength, and the per-renderer block the shader reads) and <c>TheatrePattern.SolvedForMap</c> (the regime and the scale).
    /// The mapping of each parameter onto its range is below; each is set so that a parameter at the median of the reference
    /// population lands near today's constant.
    /// </para>
    /// </remarks>
    internal static class TheatreSkinMap
    {
        /// <summary>The dial, read once: 0 off, 1 the pooled fit's map as it is, 2 the hybrid hue, 3 the hybrid on the full hue wheel with a value rank.</summary>
        public static readonly int Mode = (int)Math.Round(TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP", 0f, 0f, 3f));

        /// <summary>True with the dial at 1, 2 or 3.</summary>
        public static readonly bool Enabled = Mode > 0;

        /// <summary>
        /// The share of a wide-hue body's palette turn that is also given to the animal colours which do not take it today
        /// (<c>EVOSIM_THEATRE_SKIN_MAP_TURN_ALL</c>, 0 to 1, 0 off by default), read once. Only the rim band's warm colour is one in
        /// the film look; see <see cref="WriteTurnAll"/>. Inert with the map off.
        /// </summary>
        public static readonly float TurnAll = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_TURN_ALL", 0f, 0f, 1f);

        /// <summary>True when the map is on and <see cref="TurnAll"/> is over zero; the palette's hook is a test of this.</summary>
        public static readonly bool TurnAllOn = Enabled && TurnAll > 0f;

        /// <summary>
        /// A floor on the base saturation <see cref="SaturationGain"/> reads (<c>EVOSIM_THEATRE_SKIN_MAP_SAT_FLOOR</c>, 0 to 1, 0 off
        /// by default), read once, for a non-producer. Inert with the map off.
        /// </summary>
        public static readonly float SatFloor = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_SAT_FLOOR", 0f, 0f, 1f);

        /// <summary>
        /// How far toward <see cref="DeepenCeiling"/> a non-producer body colour's lightness is brought down
        /// (<c>EVOSIM_THEATRE_SKIN_MAP_DEEPEN</c>, 0 to 1, 0 off by default), read once; see <see cref="Deepened"/>. Inert with the map off.
        /// </summary>
        public static readonly float Deepen = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_DEEPEN", 0f, 0f, 1f);

        /// <summary>True when the map is on and <see cref="Deepen"/> is over zero; the palette's body-colour lines are tests of this.</summary>
        public static readonly bool DeepenOn = Enabled && Deepen > 0f;

        /// <summary>The lightness a tissue paler than this is brought toward at <see cref="Deepen"/> 1 (HSL lightness, as <c>Muted</c> reads it).</summary>
        private const float DeepenCeiling = 0.42f;

        /// <summary>
        /// How far a non-producer body colour's lightness moves with its value rank, down toward black for a low rank and up toward
        /// pale for a high one (<c>EVOSIM_THEATRE_SKIN_MAP_VALUE</c>, 0 to 1, 0 off by default), read once; see <see cref="Valued"/>.
        /// Only with the dial <see cref="Mode"/> at 3.
        /// </summary>
        public static readonly float Value = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_VALUE", 0f, 0f, 1f);

        /// <summary>True when the dial is at 3 and <see cref="Value"/> is over zero; the palette's body-colour lines are tests of this.</summary>
        public static readonly bool ValueOn = Mode == 3 && Value > 0f;

        /// <summary>A body whose signed value is -1 has its lightness multiplied by 1 less this (0.85: 0.66 comes to about 0.10, near black).</summary>
        private const float ValueDarkReach = 0.85f;

        /// <summary>The lightness a body of signed value +1 is brought toward, by <see cref="ValuePaleReach"/> of the way (0.66 comes to about 0.86).</summary>
        private const float ValuePale = 0.95f, ValuePaleReach = 0.7f;

        /// <summary>
        /// The perceptual levelling (<c>EVOSIM_THEATRE_SKIN_MAP_OKLAB</c>, 0 or 1, 0 off by default), read once; see <see cref="Level"/>.
        /// Only with the dial <see cref="Mode"/> at 3.
        /// </summary>
        public static readonly float OkLab = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_OKLAB", 0f, 0f, 1f);

        /// <summary>True when the dial is at 3 and <see cref="OkLab"/> is on; the palette's levelling lines are tests of this.</summary>
        public static readonly bool OkOn = Mode == 3 && OkLab > 0.5f;

        /// <summary>The default of <see cref="OkBodyLightness"/> (0.68).</summary>
        private const float OkBodyLightnessDefault = 0.68f;

        /// <summary>
        /// The OKLab lightness L a body colour is set to before the value axis moves it: <c>EVOSIM_THEATRE_SKIN_MAP_OKLAB_L</c> (0.3 to
        /// 0.9, default 0.68), read once and only while <see cref="OkOn"/>; the default otherwise. The shader's light adds to it, so a
        /// rendered body reads lighter than this.
        /// </summary>
        public static readonly float OkBodyLightness = OkOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_OKLAB_L", OkBodyLightnessDefault, 0.3f, 0.9f)
            : OkBodyLightnessDefault;

        /// <summary>The most OKLab chroma any of the three colours keeps, the default of <see cref="OkChroma"/> (0.14).</summary>
        private const float OkChromaMax = 0.14f;

        /// <summary>
        /// The chroma cap in use: <c>EVOSIM_THEATRE_SKIN_MAP_OKLAB_CHROMA</c> (0.05 to 0.35, default <see cref="OkChromaMax"/>), read once
        /// and only while <see cref="OkOn"/>; <see cref="OkChromaMax"/> otherwise.
        /// </summary>
        public static readonly float OkChroma = OkOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_OKLAB_CHROMA", OkChromaMax, 0.05f, 0.35f)
            : OkChromaMax;

        /// <summary>A body of signed value -1 has its OKLab lightness multiplied by 1 less this (0.6: 0.68 comes to 0.272).</summary>
        private const float OkDarkReach = 0.6f;

        /// <summary>The OKLab lightness a body of signed value +1 is brought toward, by <see cref="OkPaleReach"/> of the way (0.68 comes to about 0.87).</summary>
        private const float OkPale = 0.95f;

        /// <summary>The default of <see cref="OkPaleReach"/> (0.7).</summary>
        private const float OkPaleReachDefault = 0.7f;

        /// <summary>
        /// How far toward <see cref="OkPale"/> a body of signed value +1 is brought: <c>EVOSIM_THEATRE_SKIN_MAP_OKLAB_PALE_REACH</c> (0 to 1,
        /// default 0.7), read once and only while <see cref="OkOn"/>; the default otherwise.
        /// </summary>
        public static readonly float OkPaleReach = OkOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_OKLAB_PALE_REACH", OkPaleReachDefault, 0f, 1f)
            : OkPaleReachDefault;

        /// <summary>
        /// The axes dial (<c>EVOSIM_THEATRE_SKIN_MAP_AXES</c>, 0 or 1, 0 off by default), read once: every colour property of a
        /// non-producer gets its own heritable axis (hue on an arc that skips green, lightness from the value rank, chroma from the chroma
        /// rank, sheen from the sheen rank). Needs the dial <see cref="Mode"/> at 3 and the OKLab levelling on.
        /// </summary>
        public static readonly float Axes = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_AXES", 0f, 0f, 1f);

        /// <summary>True when <see cref="Mode"/> is 3, <see cref="OkOn"/> is on and <see cref="Axes"/> is on; every axes line is a test of this.</summary>
        public static readonly bool AxesOn = Mode == 3 && OkOn && Axes > 0.5f;

        /// <summary>The defaults of <see cref="ArcStart"/> and <see cref="ArcEnd"/> (170 and 55 degrees: 245 degrees of arc, no green).</summary>
        private const float ArcStartDefault = 170f, ArcEndDefault = 55f;

        /// <summary>
        /// The hue arc of the axes dial: base hue 0 lands at <see cref="ArcStart"/> degrees and base hue 1 at <see cref="ArcEnd"/>, going
        /// forward round the wheel (through 360), so the arc covers <c>(ArcEnd - ArcStart) mod 360</c> degrees. The dials are
        /// <c>EVOSIM_THEATRE_SKIN_MAP_ARC_START</c> and <c>EVOSIM_THEATRE_SKIN_MAP_ARC_END</c> (0 to 360, defaults 170 and 55), read once and
        /// only while <see cref="AxesOn"/>; the defaults otherwise.
        /// </summary>
        public static readonly float ArcStart = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ARC_START", ArcStartDefault, 0f, 360f)
            : ArcStartDefault;

        /// <summary>See <see cref="ArcStart"/>.</summary>
        public static readonly float ArcEnd = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ARC_END", ArcEndDefault, 0f, 360f)
            : ArcEndDefault;

        /// <summary>
        /// The arc's middle (<c>EVOSIM_THEATRE_SKIN_MAP_ARC_MID</c>, -1 to 360, default -1 for off), read once and only while
        /// <see cref="AxesOn"/>: the hue, in degrees, that base hue 0.5 lands on. With it the arc is mapped in two linear halves (base hue
        /// 0 to 0.5 from <see cref="ArcStart"/> to the middle, 0.5 to 1 from the middle to <see cref="ArcEnd"/>) instead of one, so the
        /// middle can sit where the lineages crowd. See <see cref="ArcMidOn"/> and <see cref="PaletteHue"/>.
        /// </summary>
        public static readonly float ArcMid = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ARC_MID", -1f, -1f, 360f)
            : -1f;

        /// <summary>
        /// True when <see cref="AxesOn"/>, <see cref="ArcMid"/> is 0 or more and the middle lies strictly inside the arc, that is
        /// <c>Repeat(ArcMid - ArcStart, 360)</c> is over zero and under <c>Repeat(ArcEnd - ArcStart, 360)</c>. A middle outside the arc
        /// leaves the arc linear (and the log says so); the palette-hue line is a test of this.
        /// </summary>
        public static readonly bool ArcMidOn = AxesOn && ArcMid >= 0f
            && Mathf.Repeat(ArcMid - ArcStart, 360f) > 0f
            && Mathf.Repeat(ArcMid - ArcStart, 360f) < Mathf.Repeat(ArcEnd - ArcStart, 360f);

        /// <summary>The OKLab lightness of the lowest and the highest value rank under the axes dial (defaults 0.12 and 0.90).</summary>
        public static readonly float LightDark = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_LIGHT_DARK", 0.12f, 0.02f, 0.6f)
            : 0.12f;

        /// <summary>See <see cref="LightDark"/>.</summary>
        public static readonly float LightPale = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_LIGHT_PALE", 0.90f, 0.5f, 0.98f)
            : 0.90f;

        /// <summary>
        /// The axes dial's OKLab chroma is <c>ChromaMax * ChromaRank ^ ChromaPower</c>, set on all three colours (defaults 0.22 and 1.5;
        /// <c>EVOSIM_THEATRE_SKIN_MAP_CHROMA_MAX</c>, 0.02 to 0.37, and <c>_CHROMA_POWER</c>, 0.25 to 4).
        /// </summary>
        public static readonly float ChromaMax = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_CHROMA_MAX", 0.22f, 0.02f, 0.37f)
            : 0.22f;

        /// <summary>See <see cref="ChromaMax"/>.</summary>
        public static readonly float ChromaPower = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_CHROMA_POWER", 1.5f, 0.25f, 4f)
            : 1.5f;

        /// <summary>
        /// The axes dial's sheen factor is <c>lerp(SheenLow, SheenHigh, SheenRank ^ SheenPower)</c>, multiplied into the tissue's sheen and
        /// clamped to [0, 1] (defaults 0.5, 4 and 2; <c>EVOSIM_THEATRE_SKIN_MAP_SHEEN_LOW</c>, <c>_SHEEN_HIGH</c> and <c>_SHEEN_POWER</c>).
        /// </summary>
        public static readonly float SheenLow = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_SHEEN_LOW", 0.5f, 0f, 4f)
            : 0.5f;

        /// <summary>See <see cref="SheenLow"/>.</summary>
        public static readonly float SheenHigh = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_SHEEN_HIGH", 4f, 0f, 16f)
            : 4f;

        /// <summary>See <see cref="SheenLow"/>.</summary>
        public static readonly float SheenPower = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_SHEEN_POWER", 2f, 0.25f, 4f)
            : 2f;

        /// <summary>
        /// The brown bias of the axes dial (<c>EVOSIM_THEATRE_SKIN_MAP_BROWN</c>, 0 to 1, 0 default), read once and only while
        /// <see cref="AxesOn"/>: a body whose palette hue is in the warm band has b = Brown * BrownWeight(hue), its target lightness
        /// becomes <c>lerp(L, min(L, BrownLightness), b)</c> and every colour's chroma <c>lerp(C, BrownChroma, b)</c>.
        /// </summary>
        public static readonly float Brown = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_BROWN", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The warm band's centre and default half-width in degrees (42 and 30: weight 1 at 42, 0 at 12, at 72 and outside), and the
        /// lightness and chroma it pulls toward. The half-width in use is <see cref="BrownWidth"/>.
        /// </summary>
        private const float BrownCentre = 42f, BrownHalfWidth = 30f, BrownLightness = 0.38f, BrownChroma = 0.075f;

        /// <summary>
        /// The warm band's half-width in degrees (<c>EVOSIM_THEATRE_SKIN_MAP_BROWN_WIDTH</c>, 5 to 90, default 30 = the constant
        /// <see cref="BrownHalfWidth"/>), read once and only while <see cref="AxesOn"/>; 30 otherwise.
        /// </summary>
        public static readonly float BrownWidth = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_BROWN_WIDTH", BrownHalfWidth, 5f, 90f)
            : BrownHalfWidth;

        /// <summary>
        /// The neutral dial (<c>EVOSIM_THEATRE_SKIN_MAP_NEUTRAL</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: an animal at the lightness ends (a very dark or a very pale value rank) or a very glossy one (a high sheen
        /// rank) loses chroma toward grey, so the dark ones read charcoal, the pale bone and pewter, the glossy silver. See <see cref="NeutralFactor"/>.
        /// </summary>
        public static readonly float Neutral = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_NEUTRAL", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="Neutral"/> is over zero; the neutral lines are tests of this.</summary>
        public static readonly bool NeutralOn = AxesOn && Neutral > 0f;

        /// <summary>
        /// The neutral dial's edges. The value weight is <c>SmoothStep(NeutralValueEdge, 1, |2 * ValueRank - 1|)</c>, zero for the middle
        /// of the value ranks and one at either end (so 0.3 starts the fade at a rank under 0.35 or over 0.65); the sheen weight is
        /// <c>SmoothStep(NeutralSheenEdge, 1, SheenRank)</c>, zero until the sheen rank passes 0.75. The larger of the two is the weight.
        /// </summary>
        private const float NeutralValueEdge = 0.3f, NeutralSheenEdge = 0.75f;

        /// <summary>
        /// The neutral dial's value edge (<c>EVOSIM_THEATRE_SKIN_MAP_NEUTRAL_EDGE</c>, 0 to 0.9, default 0.3 = the constant
        /// <see cref="NeutralValueEdge"/>), read once and only while <see cref="AxesOn"/>; 0.3 otherwise. It is the edge of the value weight
        /// in <see cref="NeutralFactor"/> and in <see cref="DarkWeight"/>: a lower edge starts the fade nearer the middle of the value ranks.
        /// </summary>
        public static readonly float NeutralEdge = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_NEUTRAL_EDGE", NeutralValueEdge, 0f, 0.9f)
            : NeutralValueEdge;

        /// <summary>
        /// The dark dial (<c>EVOSIM_THEATRE_SKIN_MAP_DARK</c>, 0 to 1, 0 off by default), read once and only while <see cref="AxesOn"/>: a
        /// low-lightness genome (a value rank under a half, faded in from <see cref="NeutralEdge"/>) goes to near-black instead of deep
        /// brown, its target lightness pulled toward <see cref="LightDark"/> and its chroma scaled down by the same weight. See <see cref="DarkWeight"/>.
        /// </summary>
        public static readonly float Dark = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_DARK", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="Dark"/> is over zero; the dark lines are tests of this.</summary>
        public static readonly bool DarkOn = AxesOn && Dark > 0f;

        /// <summary>
        /// The violet dial (<c>EVOSIM_THEATRE_SKIN_MAP_VIOLET</c>, 0 to 1, 0 off by default), read once and only while <see cref="AxesOn"/>:
        /// a body whose palette hue is in the violet band loses chroma by <see cref="Violet"/> times the band's weight, so the violets read quieter.
        /// See <see cref="VioletAmount"/>.
        /// </summary>
        public static readonly float Violet = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_VIOLET", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="Violet"/> is over zero; the violet lines are tests of this.</summary>
        public static readonly bool VioletOn = AxesOn && Violet > 0f;

        /// <summary>The violet band's centre and half-width in degrees (290 and 45: weight 1 at 290, 0 at 245, at 335 and outside).</summary>
        private const float VioletCentre = 290f, VioletHalfWidth = 45f;

        /// <summary>The pink band the family mark's hue is moved out of (<see cref="FamilyPinkAway"/>), in OKLab hue degrees: centred on 340 with a half-width of 50, so from 290 (violet-blue) to 30 (coral), wrapping through 0. v7e's
        /// marks rendered at hue 326, which a band of 350 +- 35 barely moved, and a pale mark anywhere from 315 to 25 still reads as pink.</summary>
        private const float PinkCentre = 340f, PinkHalfWidth = 50f;

        /// <summary>
        /// The part-colour dial (<c>EVOSIM_THEATRE_SKIN_MAP_PART_COLOUR</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: each part's colours move from the body's by the part's place in the body (t, its distance from the root
        /// over the body's largest) and by its own elongation (u). The lightness moves by <c>PartColour * dL * PartLightSwing * t</c>,
        /// dL +1 for a body whose value rank is under a half (a dark body gets paler tips) and -1 otherwise. The hue moves along the
        /// axes arc, not round the wheel (so no limb, tip or spot lands in the green the arc skips): the part's arc position is
        /// <c>p = baseHue + PartColour * (dH * PartHueSwing * t + PartNodeSwing * (2u - 1))</c>, dH +1 for a chroma rank of a half or
        /// more and -1 otherwise, folded into [0, 1] by reflection (1.1 is 0.9, -0.2 is 0.2), and the three colours turn by
        /// <c>PaletteHue(p) - PaletteHue(baseHue)</c>, the short way round. See <see cref="PartHueTurn"/>.
        /// </summary>
        public static readonly float PartColour = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PART_COLOUR", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="PartColour"/> is over zero; the part-colour lines are tests of this.</summary>
        public static readonly bool PartOn = AxesOn && PartColour > 0f;

        /// <summary>The lightness swing of the part-colour dial, at t = 1 (<c>EVOSIM_THEATRE_SKIN_MAP_PART_LIGHT</c>, 0 to 1, default 0.45; OKLab L).</summary>
        public static readonly float PartLightSwing = PartOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PART_LIGHT", 0.45f, 0f, 1f)
            : 0.45f;

        /// <summary>
        /// The highest OKLab L the part-colour dial may lift a part to (<c>EVOSIM_THEATRE_SKIN_MAP_PART_LIGHT_MAX</c>, 0 to 1, default 0
        /// and off). A pale link lifted near 1 clips to white once lit (52133's, 2026-10-31); the cap holds the lift under it and never
        /// takes a part below the body's own target. The rim and the light through ride the same lift from above the body's lightness,
        /// so each of the three is held as it receives the lift (53866's tail, rim L 0.98 under a body at 0.81). It acts only on a lift
        /// (dL +1), so a darker tip is untouched.
        /// </summary>
        public static readonly float PartLightMax = PartOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PART_LIGHT_MAX", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="PartLightMax"/> is over zero.</summary>
        public static readonly bool PartLightMaxOn = PartLightMax > 0f;

        /// <summary>
        /// A probe (<c>EVOSIM_THEATRE_SKIN_MAP_LEVEL_LOG</c>=1, off by default): one line per levelled part with a place, giving the body's
        /// ranks, the part's t and u, dL, the target lightness after the axes, the part dial and the zone, each colour's final L, chroma
        /// and whether it fitted the gamut, and the three colours out. It only logs.
        /// </summary>
        public static readonly bool LevelLogOn = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_MAP_LEVEL_LOG") == "1";

        /// <summary>The hue swing along the arc with the part's place, at t = 1, as a share of the arc (<c>EVOSIM_THEATRE_SKIN_MAP_PART_HUE</c>, 0 to 1, default 0.6).</summary>
        public static readonly float PartHueSwing = PartOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PART_HUE", 0.6f, 0f, 1f)
            : 0.6f;

        /// <summary>The hue swing along the arc with the part's own elongation, either way from the middle, as a share of the arc (<c>EVOSIM_THEATRE_SKIN_MAP_PART_NODE</c>, 0 to 0.5, default 0.25).</summary>
        public static readonly float PartNodeSwing = PartOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PART_NODE", 0.25f, 0f, 0.5f)
            : 0.25f;

        /// <summary>
        /// The pattern-tint dial (<c>EVOSIM_THEATRE_SKIN_MAP_PATTERN_TINT</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: the share of a part's pattern pigment that is drawn in a second colour in place of the body's darkened
        /// one (the shader's <c>_PatternTint</c>; its alpha is this). See <see cref="PatternTintColour"/>.
        /// </summary>
        public static readonly float PatternTint = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PATTERN_TINT", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="PatternTint"/> is over zero; the palette's tint lines are tests of this.</summary>
        public static readonly bool PatternTintOn = AxesOn && PatternTint > 0f;

        /// <summary>
        /// How far round the axes arc the tint's hue sits from the body's, as a share of the arc, wrapping (not reflecting): the tint's
        /// arc position is <c>q = Repeat(baseHue + PatternTintHue, 1)</c>, its hue <c>PaletteHue(q)</c> and its turn from the body's
        /// <c>PaletteHue(q) - PaletteHue(baseHue)</c>, the short way round (<c>EVOSIM_THEATRE_SKIN_MAP_PATTERN_HUE</c>, 0 to 1, default 0.5;
        /// 0 keeps the body's hue).
        /// </summary>
        public static readonly float PatternTintHue = PatternTintOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PATTERN_HUE", 0.5f, 0f, 1f)
            : 0.5f;

        /// <summary>The tint's OKLab lightness is the body's flipped across the middle by this much (<c>EVOSIM_THEATRE_SKIN_MAP_PATTERN_FLIP</c>, 0 to 0.8, default 0.45).</summary>
        public static readonly float PatternTintFlip = PatternTintOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_PATTERN_FLIP", 0.45f, 0f, 0.8f)
            : 0.45f;

        /// <summary>The least OKLab chroma the tint keeps, so a grey body still has a coloured pattern.</summary>
        private const float PatternTintChromaFloor = 0.10f;

        /// <summary>
        /// The yellow-lift dial (<c>EVOSIM_THEATRE_SKIN_MAP_YELLOW_LIFT</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: a body whose palette hue is in the yellow band has its target lightness raised toward
        /// <see cref="YellowLightness"/> by this times the band's weight (<see cref="YellowWeight"/>). A yellow levelled at mid lightness
        /// renders olive or mustard; a reef yellow is a light colour.
        /// </summary>
        public static readonly float YellowLift = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_YELLOW_LIFT", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="YellowLift"/> is over zero; the yellow-lift lines are tests of this.</summary>
        public static readonly bool YellowLiftOn = AxesOn && YellowLift > 0f;

        /// <summary>The yellow band's centre and half-width in degrees (60 and 35: weight 1 at 60, 0 at 25, at 95 and outside), and the lightness it is raised toward.</summary>
        private const float YellowCentre = 60f, YellowHalfWidth = 35f, YellowLightness = 0.82f;

        /// <summary>
        /// The zone dial (<c>EVOSIM_THEATRE_SKIN_MAP_ZONE</c>, 0 to 1, 0 off by default), read once and only while <see cref="AxesOn"/>: the
        /// body's distal parts (fins, limbs, tail: a part's place t over <see cref="ZoneT"/>) take a second, contrasting colour, as a reef
        /// fish's fins do. The zone hue sits a quarter to three quarters of the arc from the body's by the zone rank (<see cref="ZoneTurn"/>),
        /// its chroma is raised to at least <see cref="ZoneChroma"/>, and the share is this times a smooth step of t about
        /// <see cref="ZoneT"/> (half-width 0.15). It needs the part's place, so the palette gives it under this dial as under
        /// <see cref="PartOn"/>. The rim contrast band takes the zone colour under it.
        /// </summary>
        public static readonly float Zone = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ZONE", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="Zone"/> is over zero; the zone lines are tests of this.</summary>
        public static readonly bool ZoneOn = AxesOn && Zone > 0f;

        /// <summary>The part place t (<c>EVOSIM_THEATRE_SKIN_MAP_ZONE_T</c>, 0 to 1, default 0.5, read only while <see cref="AxesOn"/>) at which a part is half way to the zone colour.</summary>
        public static readonly float ZoneT = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ZONE_T", 0.5f, 0f, 1f)
            : 0.5f;

        /// <summary>The least OKLab chroma the zone colour has (<c>EVOSIM_THEATRE_SKIN_MAP_ZONE_CHROMA</c>, 0.02 to 0.3, default 0.14, read only while <see cref="AxesOn"/>).</summary>
        public static readonly float ZoneChroma = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ZONE_CHROMA", 0.14f, 0.02f, 0.3f)
            : 0.14f;

        /// <summary>
        /// The zone-pattern dial (<c>EVOSIM_THEATRE_SKIN_MAP_ZONE_PATTERN</c>, 0 or 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: with it on, and under <see cref="ZoneOn"/> and <see cref="PatternTintOn"/>, the pattern tint takes the zone
        /// hue and a chroma of at least <see cref="ZoneChroma"/> (not scaled by the neutral factor, so a drab body may carry a bright pattern).
        /// </summary>
        public static readonly float ZonePattern = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_ZONE_PATTERN", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="ZoneOn"/>, <see cref="PatternTintOn"/> and <see cref="ZonePattern"/> is on; the pattern tint's zone lines are tests of this.</summary>
        public static readonly bool ZonePatternOn = ZoneOn && PatternTintOn && ZonePattern > 0.5f;

        /// <summary>
        /// The family dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY</c>, 0 to 1, 0 off by default), read once and only while <see cref="AxesOn"/>: the
        /// strength of a pattern family drawn over the body in a second colour (<see cref="FamilyColour"/>), as a reef fish's white bars, an
        /// eyespot or a nudibranch's dark edge. Which family a body has is <see cref="FamilyOf"/>'s; the shader's <c>_FamilyColor</c> alpha
        /// and <c>_FamilyParams</c> y are this.
        /// </summary>
        public static readonly float Family = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="Family"/> is over zero; the family lines are tests of this.</summary>
        public static readonly bool FamilyOn = AxesOn && Family > 0f;

        /// <summary>
        /// The family-force dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_FORCE</c>, -1 to 3, default -1, read only while <see cref="AxesOn"/>): zero or
        /// more forces every body to that family (0 none, 1 bars, 2 eyespot, 3 edge outline). For testing only.
        /// </summary>
        public static readonly int FamilyForce = AxesOn
            ? Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_FORCE", -1f, -1f, 3f))
            : -1;

        /// <summary>The OKLab lightness of the family colour on a dark body (under a half) and on a pale one: a strong lightness contrast.</summary>
        private const float FamilyLightOnDark = 0.88f, FamilyLightOnPale = 0.14f;

        /// <summary>
        /// The family-edge dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: widens the edge outline (family 3). The smoothstep's lower edge on the view angle moves from 0.72 to 0.35 and its
        /// upper from 0.92 to 0.75 as the dial goes from 0 to 1. The shader's <c>_FamilyParams</c> w is this, for family 3 alone.
        /// </summary>
        public static readonly float FamilyEdge = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The family edge-bend dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE_BEND</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>, written to the shader's <c>_FamilyMix</c> z under <see cref="MixOn"/>: keeps the edge outline to where the
        /// surface bends on screen. The rim alone covers the whole face of a flat part seen at a glancing angle, so 40020 and 54035 went
        /// glassy; at 1 the outline's share is scaled by the normal's screen derivative times 25, saturated, which is near zero on a flat face
        /// and one at a real edge or silhouette. At 0 the outline is the rim alone, as before.
        /// </summary>
        public static readonly float FamilyEdgeBend = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE_BEND", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The family-light dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_LIGHT</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: moves the family colour's OKLab lightness toward the middle, from 0.88 to 0.70 on a dark body and from 0.14 to
        /// 0.30 on a pale one, so the zone hue is kept and not lost to white or black. See <see cref="FamilyColour"/>.
        /// </summary>
        public static readonly float FamilyLight = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_LIGHT", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The family-chroma dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_CHROMA</c>, 0 to 0.3, 0 off by default, read only while
        /// <see cref="AxesOn"/>): over zero, the OKLab chroma of the family colour in place of <see cref="ZoneChroma"/> (bisected into the gamut
        /// as the colour always is). Zero keeps <see cref="ZoneChroma"/>.
        /// </summary>
        public static readonly float FamilyChroma = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_CHROMA", 0f, 0f, 0.3f)
            : 0f;

        /// <summary>
        /// The family pink-away dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_AWAY</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: over zero, the family mark's OKLab hue, where it lies in the pink band (<see cref="PinkCentre"/> with
        /// half-width <see cref="PinkHalfWidth"/>), is moved toward the band's nearer edge by this dial times a raised-cosine weight (1 at
        /// the centre, 0 at the edges and outside) times the angular distance to that edge. At 1 the centre goes all the way to an edge,
        /// 290 (violet-blue) from exactly 340, and a hue near an edge barely moves (v7e2's marks at 10 to 22 moved about 3 degrees). See
        /// <see cref="FamilyColour"/>.
        /// </summary>
        public static readonly float FamilyPinkAway = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_AWAY", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The family pink-deepen dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_DEEPEN</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: over zero, a light mark on a dark body whose OKLab hue lies in the warm band (<see cref="WarmCentre"/> with
        /// half-width <see cref="WarmHalfWidth"/>) has its lightness moved from <see cref="FamilyLightDark"/> toward <see cref="WarmDeepL"/> by
        /// this dial times a weight that is 1 over the band's inner <see cref="WarmFlat"/> of its half-width and falls by a raised cosine to 0
        /// at its edges. Its hue is kept, so kin still match: a pale salmon or rose mark goes rust, brick or terracotta, since a warm hue reads
        /// pink at any angle when it is pale. A mark on a pale body is already dark and is unchanged. See <see cref="FamilyColour"/>.
        /// </summary>
        public static readonly float FamilyPinkDeepen = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_PINK_DEEPEN", 0f, 0f, 1f)
            : 0f;

        /// <summary>The warm band <see cref="FamilyPinkDeepen"/> darkens, in OKLab hue degrees (290 to 50 through 0), its flat inner share, and the lightness it goes toward.</summary>
        private const float WarmCentre = 350f, WarmHalfWidth = 60f, WarmFlat = 0.75f, WarmDeepL = 0.55f;

        /// <summary>
        /// The family rust dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_RUST</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: over zero, a mark on the coral side of the pink band (OKLab hue from <see cref="RustFrom"/> through 0 to
        /// <see cref="RustHue"/>, ramped in by a raised cosine over its first <see cref="RustRamp"/> degrees) has its hue moved toward
        /// <see cref="RustHue"/> by this dial times the way there, after <see cref="FamilyPinkAway"/>: a linear squeeze toward <see cref="RustHue"/>
        /// that keeps 1 minus the dial of the side's spread, so 1 is the overshoot (every coral mark at 50) and 0.6 keeps 40 %. With
        /// <see cref="FamilyPinkDeepen"/> it makes rust, brick and terracotta, which a deepened mark at its own hue of 10 to 22 did not: v7e3
        /// read coral-red. The deepen weight is taken before the turn. See <see cref="FamilyColour"/>.
        /// </summary>
        public static readonly float FamilyRust = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_RUST", 0f, 0f, 1f)
            : 0f;

        /// <summary>
        /// The coral side <see cref="FamilyRust"/> turns, in OKLab hue degrees (from 340 through 0 to 50), its ramp, and the rust hue it goes
        /// toward. It starts at <see cref="PinkCentre"/>, where <see cref="FamilyPinkAway"/> already splits the band: below it a mark goes
        /// violet, above it rust.
        /// </summary>
        private const float RustFrom = PinkCentre, RustRamp = 10f, RustHue = 50f;

        /// <summary>
        /// The family-spot dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT</c>, 0 or 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: on a one skin, the eyespot is centred on the body's largest part and its radius comes from that part's smallest
        /// axis, in place of 0.30 along the whole body's box. TheatreOneSkin writes the shader's <c>_FamilySpot</c>; a body drawn as parts is unchanged.
        /// </summary>
        public static readonly float FamilySpot = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="FamilyOn"/> and <see cref="FamilySpot"/> is on; TheatreOneSkin's spot lines are tests of this.</summary>
        public static readonly bool FamilySpotOn = FamilyOn && FamilySpot > 0.5f;

        /// <summary>
        /// The family-mix dial (<c>EVOSIM_THEATRE_SKIN_MAP_FAMILY_MIX</c>, 0 or 1, 0 off by default), read once and only while <see cref="AxesOn"/>:
        /// the maze, the bars, the eyespot, the edge outline and the colour zones each have their own heritable gate (a mean of two of the
        /// entry's ranks, see <see cref="MixMaze"/> and its siblings), so a body carries any mix of them and kin share it. Bars, eyespot and
        /// edge outline combine in the shader (<c>_FamilyMix</c>); the maze gate zeroes the body's pattern strength; the zones gate keeps the
        /// body's own hue where it is off. Needs <see cref="FamilyOn"/>, and <see cref="FamilyForce"/> (zero or more) wins over it.
        /// </summary>
        public static readonly float FamilyMix = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_FAMILY_MIX", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="FamilyOn"/>, <see cref="FamilyMix"/> is on and no family is forced; the mix lines are tests of this.</summary>
        public static readonly bool MixOn = FamilyOn && FamilyMix > 0.5f && FamilyForce < 0;

        /// <summary>
        /// The mix gates' thresholds (fitted constants, <c>scratch/skin-map/full-colour/v7d/gates.py</c>): a gate is on when the mean of its two
        /// ranks, one taken as its complement where noted, is above its threshold. Maze: sheen and 1 - value. Bars: zone and sheen. Eyespot:
        /// chroma and 1 - zone. Edge: zone and chroma. Zones: hue (<c>V[BaseHue]</c> clamped to 0 to 1) and chroma. The edge gate read value
        /// and zone until v7e5; the maze gate reads 1 - value, so one modest difference in value turned a body's maze off and its outline on
        /// together (40020 against its parent 38847, at 0.358 against 0.389 and 0.617 against 0.611). No two gates share the value rank now.
        /// </summary>
        private const float MixMaze = 0.389f, MixBars = 0.643f, MixEyespot = 0.667f, MixEdge = 0.562f, MixZones = 0.576f;

        /// <summary>The OKLab lightness of the family colour on a dark body and on a pale one under <see cref="FamilyLight"/> (the constants with it at zero).</summary>
        private static readonly float FamilyLightDark = Mathf.Lerp(FamilyLightOnDark, 0.70f, FamilyLight);
        private static readonly float FamilyLightPale = Mathf.Lerp(FamilyLightOnPale, 0.30f, FamilyLight);

        /// <summary>
        /// The countershade-boost dial (<c>EVOSIM_THEATRE_SKIN_MAP_COUNTER_BOOST</c>, 0 to 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: the countershade share's maximum rises from 1.6 to <c>lerp(1.6, 3, boost)</c> and its per-unit from 2
        /// to <c>lerp(2, 4, boost)</c>, so a dark back and a pale belly are stronger. See <see cref="CountershadeShare"/>.
        /// </summary>
        public static readonly float CounterBoost = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_COUNTER_BOOST", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="CounterBoost"/> is over zero; <see cref="CountershadeShare"/> tests it.</summary>
        public static readonly bool CounterBoostOn = AxesOn && CounterBoost > 0f;

        /// <summary>The countershade share's maximum and per-unit factor under the boost (the constants without it); read after <see cref="CounterBoost"/>, which they use.</summary>
        private static readonly float CounterMaximum = Mathf.Lerp(1.6f, 3f, CounterBoost), CounterPerUnit = Mathf.Lerp(2f, 4f, CounterBoost);

        /// <summary>
        /// The rim-contrast dial (<c>EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST</c>, 0 or 1, 0 off by default), read once and only while
        /// <see cref="AxesOn"/>: a wide-hue part's rim band takes the body's palette turn plus half a turn, so the edge stripe is the
        /// opposite colour. It wins over <see cref="TurnAll"/> where both are on. See <see cref="WriteRimContrast"/>.
        /// </summary>
        public static readonly float RimContrast = AxesOn
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST", 0f, 0f, 1f)
            : 0f;

        /// <summary>True when <see cref="AxesOn"/> and <see cref="RimContrast"/> is on; the palette's rim-contrast line is a test of this.</summary>
        public static readonly bool RimContrastOn = AxesOn && RimContrast > 0.5f;

        /// <summary>Under <c>EVOSIM_THEATRE_SKIN_MAP_LOG=1</c>, one line a body when its vector is first computed (for the parity check).</summary>
        private static readonly bool LogOn = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_LOG", 0f, 0f, 1f) > 0f;

        /// <summary>The shader keyword the map's code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Keyword = "_EVO_SKIN_MAP";

        // ---------------------------------------------------------------- the ranges

        /// <summary>A producer's base hue moves the guild colour either way by up to this fraction of the wheel (about 29 degrees), at 0 and at 1.</summary>
        public const float HueSpan = 0.08f;

        /// <summary>A body whose photosynthetic share of volume is at least this is a producer (the Python's PRODUCER_SHARE); under it, the body is not.</summary>
        public const double ProducerShare = 0.5;

        /// <summary>
        /// A non-producer's base hue picks a hue on the marine palette's path: a straight sweep of the wheel from this many degrees
        /// (blue) through violet, magenta and red to orange and gold, over this many degrees (220 to 410, which is 50, the gold). The
        /// path holds no green or cyan, the producers' colours. Base hue 0 is the blue end and 1 the gold end.
        /// </summary>
        public const float PaletteStartDegrees = 220f, PaletteSpanDegrees = 190f;

        /// <summary>
        /// In mode 3 a non-producer's base hue picks a hue on the whole wheel, from this many degrees through 360 degrees, round to
        /// the same place: base hue 0.5 lands at 345 degrees (red-pink), 0.63 near 32 (rust-orange), 0.76 near 79 (olive-gold), and
        /// 0 and 1 meet at 165 (cyan-green), at the default. Modes 1 and 2 keep the marine path above. The start is
        /// <c>EVOSIM_THEATRE_SKIN_MAP_WHEEL_START</c> (0 to 360 degrees, default 165), read once and only in mode 3: lowering it turns
        /// every animal's hue back by the same amount, so the bodies at ranks 0.70 to 0.76, olive at 165, land on ochre and rust near 130.
        /// </summary>
        public static readonly float WheelStartDegrees = Mode == 3
            ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_MAP_WHEEL_START", WheelStartDefault, 0f, 360f)
            : WheelStartDefault;

        /// <summary>The default of <see cref="WheelStartDegrees"/> (165).</summary>
        private const float WheelStartDefault = 165f;

        /// <summary>The base saturation scales the body's saturation cap from the first to the second, at 0 and at 1 (the median lands near one).</summary>
        public const float SaturationLow = 0.55f, SaturationHigh = 1.45f;

        /// <summary>The rim hue turns the rim band's warm colour about the grey axis by up to this many radians either way (a quarter of the wheel).</summary>
        public const float RimHueSpanRadians = 1.5707964f;

        /// <summary>The rim width multiplies the band's width by this at 0 and at 1.</summary>
        public const float RimWidthLow = 0.4f, RimWidthHigh = 1.6f;

        /// <summary>The mottle strength is the soft mottle's gain at 0 and at 1 (today's constant is 1.4).</summary>
        public const float MottleGainLow = 0.6f, MottleGainHigh = 2.2f;

        /// <summary>The iridescence multiplies the tissue's strength by this at 0 and at 1.</summary>
        public const float IridescenceLow = 0.2f, IridescenceHigh = 1.8f;

        /// <summary>The countershade is this many shares of today's grade at 1, capped at <see cref="CountershadeMaximum"/> (today's is one).</summary>
        public const float CountershadePerUnit = 2f, CountershadeMaximum = 1.6f;

        /// <summary>The carve character multiplies the guild's carve depth (<c>_Carve.w</c>) by this at 0 and at 1.</summary>
        public const float CarveLow = 0.6f, CarveHigh = 1.6f;

        // ---------------------------------------------------------------- an entry

        /// <summary>One genome's vector and the features it came from.</summary>
        public sealed class Entry
        {
            /// <summary>The ten parameters in [0, 1], in the order of <see cref="TheatreSkinMapConstants.ParameterNames"/>.</summary>
            public readonly float[] V;

            /// <summary>The same ten in double precision, for the log's comparison with the Python's.</summary>
            public readonly double[] Exact;

            /// <summary>The raw features, in the order of <see cref="TheatreSkinMapConstants.Features"/>.</summary>
            public readonly double[] Features;

            /// <summary>True when the photosynthetic share of the body's volume is <see cref="ProducerShare"/> or more. A producer keeps its colour as the alga look sets it; the base hue only nudges it.</summary>
            public readonly bool Producer;

            /// <summary>The palette's turn of the body's main tint, a fraction of the wheel; set by the palette on first use (NaN until then), since it reads the tissue colours.</summary>
            public float MainTurn = float.NaN;

            /// <summary>
            /// The middle rank, in [0, 1], of this genome's value score among <see cref="TheatreSkinMapConstants.ValueFittingScores"/>
            /// (mode 3 only; NaN in every other mode). Not one of the ten values of <see cref="V"/>.
            /// </summary>
            public float ValueRank = float.NaN;

            /// <summary>
            /// The middle rank of the chroma score (<see cref="TheatreSkinMapConstants.ChromaWeights"/>) among its fitting scores, computed as
            /// <see cref="ValueRank"/> is (mode 3 only; NaN in every other mode).
            /// </summary>
            public float ChromaRank = float.NaN;

            /// <summary>The middle rank of the sheen score (<see cref="TheatreSkinMapConstants.SheenWeights"/>), as <see cref="ChromaRank"/> (mode 3 only; NaN otherwise).</summary>
            public float SheenRank = float.NaN;

            /// <summary>The middle rank of the zone score (<see cref="TheatreSkinMapConstants.ZoneWeights"/>), as <see cref="ChromaRank"/> (mode 3 only; NaN otherwise). It places the zone colour on the arc; see <see cref="ZoneTurn"/>.</summary>
            public float ZoneRank = float.NaN;

            public Entry(double[] exact, double[] features)
            {
                Exact = exact;
                V = new float[exact.Length];
                for (int k = 0; k < exact.Length; k++) V[k] = (float)exact[k];

                Features = features;

                int photosynthetic = Array.IndexOf(TheatreSkinMapConstants.Features, "share_photosynthetic");
                Producer = photosynthetic >= 0 && features[photosynthetic] >= ProducerShare;
            }
        }

        private const int BaseHue = 0, BaseSaturation = 1, PatternRegime = 2, PatternScale = 3, RimHue = 4, RimWidth = 5,
            MottleStrength = 6, Iridescence = 7, Countershade = 8, CarveCharacter = 9;

        /// <summary>
        /// The tint's turn of the hue, as a fraction of the wheel, in place of the lineage's turn. A producer's tint takes it as it is.
        /// A non-producer takes it only for a part the alga look recolours (a leaf on an animal); the rest of its body takes
        /// <see cref="MainTurn"/> instead.
        /// </summary>
        public static float TintTurn(Entry e) => (e.V[BaseHue] - 0.5f) * 2f * HueSpan;

        /// <summary>
        /// The palette's hue for a base hue in [0, 1], as a fraction of the wheel in [0, 1): the marine path in modes 1 and 2, the
        /// whole wheel from <see cref="WheelStartDegrees"/> in mode 3. Under <see cref="AxesOn"/> it is the axes arc, linear in the base hue,
        /// or, with <see cref="ArcMidOn"/>, two linear halves meeting at <see cref="ArcMid"/> when the base hue is 0.5.
        /// </summary>
        public static float PaletteHue(float baseHue) => AxesOn
            ? ArcMidOn
                ? Mathf.Repeat(ArcMidDegrees(Mathf.Clamp01(baseHue)) / 360f, 1f)
                : Mathf.Repeat((ArcStart + Mathf.Repeat(ArcEnd - ArcStart, 360f) * Mathf.Clamp01(baseHue)) / 360f, 1f)
            : Mode == 3
                ? Mathf.Repeat((WheelStartDegrees + 360f * Mathf.Clamp01(baseHue)) / 360f, 1f)
                : Mathf.Repeat((PaletteStartDegrees + PaletteSpanDegrees * Mathf.Clamp01(baseHue)) / 360f, 1f);

        /// <summary>
        /// The arc's hue in degrees (not wrapped) for a clamped base hue under <see cref="ArcMidOn"/>: <c>ArcStart + Repeat(ArcMid - ArcStart, 360) * 2b</c>
        /// up to a base hue of a half, <c>ArcMid + Repeat(ArcEnd - ArcMid, 360) * (2b - 1)</c> above.
        /// </summary>
        private static float ArcMidDegrees(float b) => b <= 0.5f
            ? ArcStart + Mathf.Repeat(ArcMid - ArcStart, 360f) * (2f * b)
            : ArcMid + Mathf.Repeat(ArcEnd - ArcMid, 360f) * (2f * b - 1f);

        /// <summary>
        /// The turn, a fraction of the wheel in [-0.5, 0.5), that carries a colour of hue <paramref name="mainHue"/> (a fraction of the
        /// wheel) onto the body's palette hue, by the short way round. The same turn is given to every colour of the body, so its
        /// tissues keep their hues' places relative to the main tint.
        /// </summary>
        public static float PaletteTurn(Entry e, float mainHue) =>
            Mathf.Repeat(PaletteHue(e.V[BaseHue]) - mainHue + 0.5f, 1f) - 0.5f;

        /// <summary>
        /// The factor on the body colour's saturation cap. With <see cref="SatFloor"/> over zero a non-producer reads the larger of
        /// its base saturation and the floor; with it at zero the line is the one it always was.
        /// </summary>
        public static float SaturationGain(Entry e)
        {
            if (SatFloor > 0f && !e.Producer)
                return Mathf.Lerp(SaturationLow, SaturationHigh, Mathf.Max(e.V[BaseSaturation], SatFloor));

            return Mathf.Lerp(SaturationLow, SaturationHigh, e.V[BaseSaturation]);
        }

        /// <summary>The place along the path spots, net, stripes, in [0, 1], in place of the normalised investment.</summary>
        public static float RegimeAlong(Entry e) => e.V[PatternRegime];

        /// <summary>The place between the shortest and longest tile length, in [0, 1], in place of the normalised margin bin.</summary>
        public static float ScaleAlong(Entry e) => e.V[PatternScale];

        /// <summary>The rim band's colour turn, radians.</summary>
        public static float RimHueRadians(Entry e) => (e.V[RimHue] - 0.5f) * 2f * RimHueSpanRadians;

        /// <summary>The rim band's width, as a multiple of today's.</summary>
        public static float RimWidthScale(Entry e) => Mathf.Lerp(RimWidthLow, RimWidthHigh, e.V[RimWidth]);

        /// <summary>The soft mottle's gain.</summary>
        public static float MottleGain(Entry e) => Mathf.Lerp(MottleGainLow, MottleGainHigh, e.V[MottleStrength]);

        /// <summary>The factor on a tissue's iridescence strength.</summary>
        public static float IridescenceGain(Entry e) => Mathf.Lerp(IridescenceLow, IridescenceHigh, e.V[Iridescence]);

        /// <summary>The share of today's countershade grade.</summary>
        public static float CountershadeShare(Entry e) => CounterBoostOn
            ? Mathf.Min(CounterMaximum, CounterPerUnit * e.V[Countershade])
            : Mathf.Min(CountershadeMaximum, CountershadePerUnit * e.V[Countershade]);

        /// <summary>The factor on the guild's carve depth.</summary>
        public static float CarveGain(Entry e) => Mathf.Lerp(CarveLow, CarveHigh, e.V[CarveCharacter]);

        // ---------------------------------------------------------------- the registry

        /// <summary>The most ids held before the registry is emptied; a body dropped and still alive is drawn without the map until it is registered again, which every caller does every sweep.</summary>
        private const int RegistryLimit = 200000;

        private static Dictionary<long, Entry> _entries;
        private static bool _keywordOn;
        private static bool _failureSaid;

        /// <summary>
        /// Says which genome a body was grown from, once a body: the vector is made the first time an id is seen and never again,
        /// and with the dial off the call returns at once. A caller with a genome and the run's config calls it before the body
        /// is first painted, beside <see cref="TheatrePattern.Register"/>. A null config develops under Core's default limits.
        /// </summary>
        public static void Register(long id, Genome genome, RunConfig config)
        {
            if (!Enabled || genome == null) return;

            if (_entries == null) _entries = new Dictionary<long, Entry>();
            if (_entries.ContainsKey(id)) return;
            if (_entries.Count >= RegistryLimit) _entries.Clear();

            Entry entry = null;

            try
            {
                entry = Compute(genome, config);
            }
            catch (Exception e)
            {
                // A genome that does not develop is drawn as it would be without the map; said once, not at every sweep.
                if (!_failureSaid)
                {
                    _failureSaid = true;
                    Debug.LogWarning("[Theatre] skin map: creature " + id + " has no vector, its genome did not develop (" +
                                     e.GetType().Name + ": " + e.Message + "); it is drawn without the map, and the rest are not said.");
                }
            }

            _entries[id] = entry;

            if (entry == null) return;

            EnsureKeyword();
            if (LogOn) Debug.Log(LogLine(id, entry));
        }

        /// <summary>A registered body's entry, or false (never registered, or its genome developed to nothing).</summary>
        public static bool TryGet(long id, out Entry entry)
        {
            entry = null;
            if (_entries == null || !_entries.TryGetValue(id, out entry) || entry == null) return false;

            // A producer is drawn as it is without the map: its colour is the alga look's, set per world (the owner, 2026-10-29),
            // and the re-centred saturation and carve turned the pooled fit's producers khaki (the third sheet). Its vector is
            // still made and logged, for the checks.
            if (entry.Producer)
            {
                entry = null;
                return false;
            }

            return true;
        }

        /// <summary>Turns the shader keyword on once, for every material that has it; does nothing with the dial off, and nothing turns it off.</summary>
        public static void EnsureKeyword()
        {
            if (!Enabled || _keywordOn) return;

            Shader.EnableKeyword(Keyword);
            _keywordOn = true;
            Debug.Log("[Theatre] skin map on: " + TheatreSkinMapConstants.Reference + " (shader keyword " + Keyword + ").");

            if (TurnAllOn)
            {
                Debug.Log("[Theatre] skin map turn all on: " + TurnAll.ToString("F2", CultureInfo.InvariantCulture) +
                          " of a wide-hue body's palette turn is also given to the rim band's warm colour.");
            }

            if (SatFloor > 0f)
            {
                Debug.Log("[Theatre] skin map saturation floor on: a non-producer's base saturation reads at least " +
                          SatFloor.ToString("F2", CultureInfo.InvariantCulture) + " in the body's saturation gain.");
            }

            if (DeepenOn)
            {
                Debug.Log("[Theatre] skin map deepen on: a non-producer body colour's lightness comes down " +
                          Deepen.ToString("F2", CultureInfo.InvariantCulture) + " of the way to " +
                          DeepenCeiling.ToString("F2", CultureInfo.InvariantCulture) + " where it is lighter.");
            }

            if (Mode == 3)
            {
                Debug.Log("[Theatre] skin map full colour on: a non-producer's base hue picks a hue on the whole wheel from " +
                          WheelStartDegrees.ToString("F0", CultureInfo.InvariantCulture) + " degrees, and each body has a value rank (" +
                          TheatreSkinMapConstants.ValueRule + ").");
            }

            if (ValueOn)
            {
                Debug.Log("[Theatre] skin map value on: a non-producer body colour's lightness moves with its value rank, by " +
                          Value.ToString("F2", CultureInfo.InvariantCulture) + " of the range (dark reach " +
                          ValueDarkReach.ToString("F2", CultureInfo.InvariantCulture) + ", pale " +
                          ValuePale.ToString("F2", CultureInfo.InvariantCulture) + " at reach " +
                          ValuePaleReach.ToString("F2", CultureInfo.InvariantCulture) + ").");
            }

            if (OkOn)
            {
                Debug.Log("[Theatre] skin map OKLab levelling on: the body, rim and light-through colours of a non-producer are set to OKLab L " +
                          OkBodyLightness.ToString("F2", CultureInfo.InvariantCulture) + " moved by the value rank (value dial " +
                          Value.ToString("F2", CultureInfo.InvariantCulture) + ", dark reach " +
                          OkDarkReach.ToString("F2", CultureInfo.InvariantCulture) + ", pale " +
                          OkPale.ToString("F2", CultureInfo.InvariantCulture) + " at reach " +
                          OkPaleReach.ToString("F2", CultureInfo.InvariantCulture) + "), chroma capped at " +
                          OkChroma.ToString("F3", CultureInfo.InvariantCulture) + " (EVOSIM_THEATRE_SKIN_MAP_OKLAB_CHROMA).");
            }

            if (AxesOn)
            {
                Debug.Log("[Theatre] skin map axes on: hue arc " + F0(ArcStart) + " to " + F0(ArcEnd) + " degrees (" +
                          F0(Mathf.Repeat(ArcEnd - ArcStart, 360f)) + " of arc), OKLab L " + F2(LightDark) + " to " + F2(LightPale) +
                          " by the value rank, chroma " + F3(ChromaMax) + " * chroma rank ^ " + F2(ChromaPower) + ", sheen factor " +
                          F2(SheenLow) + " to " + F2(SheenHigh) + " by sheen rank ^ " + F2(SheenPower) + " (clamped to 0 to 1), brown " +
                          F2(Brown) + " (band centre " + F0(BrownCentre) + " +- " + F0(BrownWidth) + " degrees, L at most " +
                          F2(BrownLightness) + ", chroma " + F3(BrownChroma) + ") (EVOSIM_THEATRE_SKIN_MAP_AXES)" +
                          (ArcMidOn ? "; arc middle at " + F0(ArcMid) + " degrees for base hue 0.5 (_ARC_MID)" : "") +
                          (NeutralOn ? "; neutral " + F2(Neutral) + " (_NEUTRAL): chroma toward grey by the larger of a value-rank end weight (edge " +
                                       F2(NeutralEdge) + ") and a sheen-rank weight (edge " + F2(NeutralSheenEdge) + ")" : "") +
                          (NeutralEdge != NeutralValueEdge ? "; neutral value edge " + F2(NeutralEdge) + " (_NEUTRAL_EDGE)" : "") +
                          (DarkOn ? "; dark " + F2(Dark) + " (_DARK): a low value rank goes toward near-black, chroma scaled down" : "") +
                          (VioletOn ? "; violet " + F2(Violet) + " (_VIOLET): chroma down in the band " + F0(VioletCentre) + " +- " + F0(VioletHalfWidth) + " degrees" : "") +
                          (YellowLiftOn ? "; yellow lift " + F2(YellowLift) + " (_YELLOW_LIFT): L toward " + F2(YellowLightness) + " in the band " + F0(YellowCentre) + " +- " + F0(YellowHalfWidth) + " degrees" : "") +
                          (ZoneOn ? "; zone " + F2(Zone) + " (_ZONE): parts past t " + F2(ZoneT) + " (_ZONE_T) take a second hue, chroma at least " + F3(ZoneChroma) + " (_ZONE_CHROMA)" : "") +
                          (ZonePatternOn ? "; the pattern tint takes the zone hue (_ZONE_PATTERN)" : "") +
                          (FamilyOn ? "; family " + F2(Family) + " (_FAMILY): bars, eyespot or edge outline in the zone colour at L " + F2(FamilyLightDark) +
                                      " on a dark body, " + F2(FamilyLightPale) + " on a pale one" + (FamilyForce >= 0 ? ", forced to family " + FamilyForce + " (_FAMILY_FORCE)" : "") +
                                      (FamilyEdge > 0f ? ", edge outline widened " + F2(FamilyEdge) + " (_FAMILY_EDGE)" : "") +
                                      (FamilyEdgeBend > 0f ? ", edge outline kept to bends " + F2(FamilyEdgeBend) + " (_FAMILY_EDGE_BEND)" : "") +
                                      (FamilyLight > 0f ? ", lightness toward the middle " + F2(FamilyLight) + " (_FAMILY_LIGHT)" : "") +
                                      (FamilyChroma > 0f ? ", chroma " + F3(FamilyChroma) + " in place of the zone's (_FAMILY_CHROMA)" : "") +
                                      (FamilyPinkAway > 0f ? ", hue out of pink " + F2(FamilyPinkAway) + " (_FAMILY_PINK_AWAY): the band " + F0(PinkCentre) + " +- " + F0(PinkHalfWidth) + " degrees moved toward its nearer edge" : "") +
                                      (FamilyPinkDeepen > 0f ? ", warm marks deepened " + F2(FamilyPinkDeepen) + " (_FAMILY_PINK_DEEPEN): on a dark body, L toward " + F2(WarmDeepL) + " in the band " + F0(WarmCentre) + " +- " + F0(WarmHalfWidth) + " degrees" + (FamilyRust > 0f ? "" : ", hue kept") : "") +
                                      (FamilyRust > 0f ? ", coral marks to rust " + F2(FamilyRust) + " (_FAMILY_RUST): hue " + F0(RustFrom) + " to " + F0(RustHue) + " turned toward " + F0(RustHue) : "") +
                                      (FamilySpotOn ? ", eyespot on the largest part of a one skin (_FAMILY_SPOT)" : "") +
                                      (MixOn ? ", combinable (_FAMILY_MIX): five heritable gates, a body carries any mix of the maze, bars, eyespot, edge outline and zones" : "") : "") + ".");

                if (ArcMid >= 0f && !ArcMidOn)
                {
                    Debug.LogWarning("[Theatre] skin map: EVOSIM_THEATRE_SKIN_MAP_ARC_MID " + F0(ArcMid) + " is not strictly inside the arc " +
                                     F0(ArcStart) + " to " + F0(ArcEnd) + " degrees; the arc stays linear.");
                }
            }

            if (PartOn)
            {
                Debug.Log("[Theatre] skin map part colour on: " + F2(PartColour) + " of a part's shift from the body colour (EVOSIM_THEATRE_SKIN_MAP_PART_COLOUR); " +
                          "lightness " + F2(PartLightSwing) + " * t, paler for a dark body and darker for a pale one (_PART_LIGHT); hue shifted along the " +
                          "axes arc, not round the wheel: arc position p = base hue + dial * (+-" + F2(PartHueSwing) + " * t (_PART_HUE) + " +
                          F2(PartNodeSwing) + " * (2u - 1) (_PART_NODE)), folded into [0, 1] by reflection, and each colour turns by " +
                          "PaletteHue(p) - PaletteHue(base hue), the short way round; t is the part's distance from the root over the body's largest, " +
                          "u its elongation.");

                if (PartLightMaxOn)
                {
                    Debug.Log("[Theatre] skin map part light cap on: a part's lift stops at OKLab L " + F2(PartLightMax) +
                              ", never below the body's own target, and a darkening is untouched (EVOSIM_THEATRE_SKIN_MAP_PART_LIGHT_MAX).");
                }
            }

            if (PatternTintOn)
            {
                Debug.Log("[Theatre] skin map pattern tint on: the pattern's pigment is drawn " + F2(PatternTint) + " of the way to a second colour " +
                          "(EVOSIM_THEATRE_SKIN_MAP_PATTERN_TINT); its hue is on the axes arc, " + F2(PatternTintHue) + " of the arc from the " +
                          "body's, wrapping (arc position q = Repeat(base hue + that, 1), turn PaletteHue(q) - PaletteHue(base hue)) (_PATTERN_HUE); " +
                          "its OKLab lightness is the body's flipped across the middle by " + F2(PatternTintFlip) + " (_PATTERN_FLIP), its chroma at " +
                          "least " + F2(PatternTintChromaFloor) + ".");
            }

            if (CounterBoostOn)
            {
                Debug.Log("[Theatre] skin map countershade boost on: " + F2(CounterBoost) + " (EVOSIM_THEATRE_SKIN_MAP_COUNTER_BOOST); the share's maximum is " +
                          F2(CounterMaximum) + " (1.60 without) and its per-unit " + F2(CounterPerUnit) + " (2.00 without).");
            }

            if (RimContrastOn)
            {
                Debug.Log("[Theatre] skin map rim contrast on (EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST): a wide-hue part's rim band takes the body's palette " +
                          "turn plus half a turn; it wins over turn-all (" + F2(TurnAll) + ") where both are on.");
            }
        }

        /// <summary>The shader's property ids, made on the first write (the main thread), not at the class's first touch.</summary>
        private static class Ids
        {
            public static readonly int Map = Shader.PropertyToID("_SkinMap");
            public static readonly int MapOn = Shader.PropertyToID("_SkinMapOn");
            public static readonly int PatternTint = Shader.PropertyToID("_PatternTint");
            public static readonly int FamilyColor = Shader.PropertyToID("_FamilyColor");
            public static readonly int FamilyParams = Shader.PropertyToID("_FamilyParams");
            public static readonly int FamilyMix = Shader.PropertyToID("_FamilyMix");
        }

        /// <summary>
        /// Writes a renderer's share of the vector into the palette's block: the rim band's width and colour turn, the soft
        /// mottle's gain and the countershade's share, and the flag that makes the shader read them. A null entry writes the flag
        /// at zero, which is the body as it was. Called by the palette only while the dial is on, either way, since a block cannot
        /// forget a property.
        /// </summary>
        public static void Write(MaterialPropertyBlock block, Entry entry)
        {
            if (entry == null)
            {
                block.SetFloat(Ids.MapOn, 0f);
                return;
            }

            block.SetVector(Ids.Map, new Vector4(
                RimWidthScale(entry), RimHueRadians(entry), MottleGain(entry), CountershadeShare(entry)));
            block.SetFloat(Ids.MapOn, 1f);
        }

        /// <summary>
        /// The turn-all dial's one write (<c>EVOSIM_THEATRE_SKIN_MAP_TURN_ALL</c>): the rim band's warm colour, a constant of the
        /// shader (<c>RimBand*</c>, orange) that no palette turn reaches, turned about the grey axis by <see cref="TurnAll"/> of the
        /// body's palette turn on top of its own rim hue. The turn is a fraction of the wheel (<paramref name="turn"/>, the
        /// palette's), the shader's angle is in radians, so the angle is two pi times the share times the turn; the rotation about
        /// the grey axis is the RGB cube's and follows an HSV hue turn only approximately. Rewrites <c>_SkinMap</c> after
        /// <see cref="Write"/> has written it, and only for a body with a vector. Called by the palette only while
        /// <see cref="TurnAllOn"/>, for a wide-hue part in colour; nothing here runs with the dial at zero.
        /// </summary>
        public static void WriteTurnAll(MaterialPropertyBlock block, Entry entry, float turn)
        {
            if (entry == null) return;

            block.SetVector(Ids.Map, new Vector4(
                RimWidthScale(entry), RimHueRadians(entry) + TurnAll * turn * 6.2831855f, MottleGain(entry), CountershadeShare(entry)));
        }

        /// <summary>
        /// A tissue's lightness with the deepen dial applied: <c>L - Deepen * max(0, L - 0.42)</c>, so a tissue lighter than the
        /// ceiling comes down toward it by the dial's share and a darker one is untouched. Called by the palette only while
        /// <see cref="DeepenOn"/>, for a wide-hue part in colour; nothing here runs with the dial at zero.
        /// </summary>
        public static float Deepened(float lightness) => lightness - Deepen * Mathf.Max(0f, lightness - DeepenCeiling);

        /// <summary>
        /// A tissue's lightness with the value dial applied, for a body of value rank r and s = <see cref="Value"/> * (2r - 1).
        /// Below zero, <c>L * (1 + ValueDarkReach * s)</c> (s = -1 takes 0.66 to about 0.10); above zero,
        /// <c>L + (ValuePale - L) * ValuePaleReach * s</c> (s = 1 takes 0.66 to about 0.86). Both rise with L at a given s, so the
        /// tissues keep their order of lightness. A body with no value rank (no entry, or not mode 3) gets its lightness back.
        /// Called by the palette only while <see cref="ValueOn"/>, for a wide-hue part in colour; nothing here runs with the dial at zero.
        /// </summary>
        public static float Valued(float lightness, Entry e)
        {
            if (e == null || float.IsNaN(e.ValueRank)) return lightness;

            float s = Value * (2f * e.ValueRank - 1f);

            return s < 0f
                ? lightness * (1f + ValueDarkReach * s)
                : lightness + (ValuePale - lightness) * ValuePaleReach * s;
        }

        /// <summary>
        /// The perceptual levelling of the three colours of a wide-hue part (<see cref="OkOn"/>), in OKLab (Ottosson's matrices), the
        /// three taken as the gamma-encoded sRGB <c>Muted</c> makes them. The target lightness T is <see cref="OkBodyLightness"/>;
        /// with <see cref="Value"/> over zero and a value rank, s = Value * (2 rank - 1), and T is multiplied by
        /// <c>1 + OkDarkReach * s</c> below zero or raised by <c>(OkPale - T) * OkPaleReach * s</c> above. The shift T less the body's
        /// own L is added to all three colours' L (so the rim and the light through keep their offsets from the body), each L is
        /// clamped to [0.02, 0.98], each chroma to <see cref="OkChroma"/>, and each hue is kept. A colour out of the sRGB gamut has its
        /// chroma bisected down (L and hue kept) until it is in, then is clamped. Alpha is kept. HSL lightness is not perceived
        /// lightness (a yellow and a magenta at one HSL L differ by 25 in CIELAB L*), and a body moved alone left its rim at its old
        /// lightness; this puts the three on one scale. Called by the palette only while <see cref="OkOn"/>.
        /// Under <see cref="AxesOn"/> the target is <c>lerp(LightDark, LightPale, ValueRank)</c>, each chroma is set to
        /// <c>ChromaMax * ChromaRank ^ ChromaPower</c>, the brown bias (<see cref="BrownAmount"/>, its band's width <see cref="BrownWidth"/>)
        /// acts on both, under <see cref="DarkOn"/> the target is pulled toward <see cref="LightDark"/> and the chroma scaled down by
        /// <see cref="DarkWeight"/>, and under <see cref="NeutralOn"/> and <see cref="VioletOn"/> the chroma is then multiplied by
        /// <see cref="NeutralFactor"/> and by one less <see cref="VioletAmount"/>; otherwise nothing of that runs. The hue is <see cref="PaletteHue"/>'s, so the arc's middle (<see cref="ArcMidOn"/>) reaches it through the palette.
        /// Under <see cref="PartOn"/>, with <paramref name="partT"/> and <paramref name="partU"/> given (NaN is not given: no part shift),
        /// the target also moves by <c>PartColour * dL * PartLightSwing * t</c> and the three hues turn by <see cref="PartHueTurn"/>.
        /// </summary>
        public static void Level(ref Color body, ref Color rim, ref Color through, Entry e,
            float partT = float.NaN, float partU = float.NaN) =>
            LevelCore(ref body, ref rim, ref through, e, partT, partU, false, out _, out _);

        /// <summary>
        /// <see cref="Level"/> that also makes the pattern's second colour (<see cref="PatternTintOn"/>): the body's own hue (before any
        /// part shift) turned along the arc by <see cref="PatternTintTurn"/>, its OKLab lightness the levelled body's flipped across the
        /// middle by <see cref="PatternTintFlip"/>, its chroma the body's or the floor (scaled by <see cref="NeutralFactor"/> under
        /// <see cref="NeutralOn"/>), whichever is more, bisected into the gamut. Its
        /// alpha is <see cref="PatternTint"/>; zero for a body without the axes' ranks, so the shader leaves its pattern alone.
        /// </summary>
        public static void LevelWithTint(ref Color body, ref Color rim, ref Color through, Entry e,
            float partT, float partU, out Color tint) =>
            LevelCore(ref body, ref rim, ref through, e, partT, partU, true, out tint, out _);

        /// <summary>
        /// <see cref="LevelWithTint"/> that also gives the family colour (<see cref="FamilyColour"/>, alpha <see cref="Family"/>; alpha zero for
        /// a body without the axes' ranks). Called by the palette only while <see cref="FamilyOn"/>. The tint is made only under
        /// <see cref="PatternTintOn"/>, as before, and is left at zero otherwise.
        /// </summary>
        public static void LevelWithTint(ref Color body, ref Color rim, ref Color through, Entry e,
            float partT, float partU, out Color tint, out Color family) =>
            LevelCore(ref body, ref rim, ref through, e, partT, partU, true, out tint, out family);

        private static void LevelCore(ref Color body, ref Color rim, ref Color through, Entry e,
            float partT, float partU, bool wantTint, out Color tint, out Color family)
        {
            tint = new Color(0f, 0f, 0f, 0f);
            family = new Color(0f, 0f, 0f, 0f);
            ToLch(body, out double lb, out double cb, out double hb);
            ToLch(rim, out double lr, out double cr, out double hr);
            ToLch(through, out double lt, out double ct, out double ht);

            double target = OkBodyLightness;
            if (Value > 0f && e != null && !float.IsNaN(e.ValueRank))
            {
                double s = Value * (2.0 * e.ValueRank - 1.0);
                if (s < 0.0) target *= 1.0 + OkDarkReach * s;
                else if (s > 0.0) target += (OkPale - target) * OkPaleReach * s;
            }

            double cap = OkChroma;
            bool axes = AxesOn && e != null && !float.IsNaN(e.ValueRank) && !float.IsNaN(e.ChromaRank);

            if (axes)
            {
                // The axes dial (AxesOn): the lightness comes from the value rank alone, and each colour's chroma is set, not capped,
                // from the chroma rank. The brown bias then pulls a warm body toward a dark, quiet brown.
                target = LightDark + (LightPale - LightDark) * (double)e.ValueRank;
                double chroma = ChromaMax * Math.Pow(e.ChromaRank, ChromaPower);
                double b = BrownAmount(e);

                if (b > 0.0)
                {
                    target += (Math.Min(target, BrownLightness) - target) * b;
                    chroma += (BrownChroma - chroma) * b;
                }

                if (DarkOn)
                {
                    // A black is not a dark colour: the target goes toward the dark end and the chroma goes with it.
                    double dw = DarkWeight(e);
                    target += (LightDark - target) * dw;
                    chroma *= 1.0 - dw;
                }

                // A yellow levelled at mid lightness renders olive or mustard; a reef yellow is a light colour.
                if (YellowLiftOn) target += (Math.Max(target, YellowLightness) - target) * YellowLift * YellowWeight(PaletteHue(e.V[BaseHue]));

                if (NeutralOn) chroma *= NeutralFactor(e);
                if (VioletOn) chroma *= 1.0 - VioletAmount(e);

                cb = cr = ct = chroma;
                cap = double.MaxValue;
            }

            // The body's hue before the part shift: the pattern tint is placed from it, not from the part's.
            double trunkHue = hb;
            double probeAxesL = target;
            double probePartL = target;
            double probeDL = 0.0;
            double partLift = 0.0;

            if (axes && PartOn && !float.IsNaN(partT) && !float.IsNaN(partU))
            {
                // The part-colour dial: a dark body (value rank under a half) gets paler tips, a pale one darker; the hue turns along the arc.
                double dL = e.ValueRank < 0.5f ? 1.0 : -1.0;
                probeDL = dL;
                double unlifted = target;
                target += PartColour * dL * PartLightSwing * partT;

                if (PartLightMaxOn && dL > 0.0)
                {
                    target = Math.Min(target, Math.Max(unlifted, PartLightMax));
                    partLift = target - unlifted;
                }

                double turn = PartHueTurn(e, partT, partU);
                hb += turn;
                hr += turn;
                ht += turn;
                probePartL = target;
            }

            if (axes && ZoneOn && ZoneGateOn(e) && !float.IsNaN(partT) && !float.IsNaN(partU) && !float.IsNaN(e.ZoneRank))
            {
                // The zone dial: the distal parts take the second colour, its hue turned from the body's (after the part-colour turn), its chroma
                // raised to the floor (cb, cr and ct are one chroma under the axes), and a yellow zone lifted as a yellow body is.
                double zs = Zone * SmoothStep(ZoneT - 0.15, ZoneT + 0.15, partT);
                double zturn = zs * ZoneTurn(e) * 2.0 * Math.PI;
                hb += zturn;
                hr += zturn;
                ht += zturn;

                double zc = cb;
                cb = cr = ct = zc + (Math.Max(zc, ZoneChroma) - zc) * zs;

                if (YellowLiftOn) target += zs * YellowLift * (Math.Max(target, YellowLightness) - target) * YellowWeight(ZoneHue(e));
            }

            double shift = target - lb;

            if (partLift > 0.0)
            {
                // The cap holds the lift as each colour receives it: the rim and the light through sit above the body's lightness and
                // ride the same shift, so a lift the body's target can afford takes them to white (53866's tail, rim L 0.98).
                lb = CappedLift(lb + shift, partLift) - shift;
                lr = CappedLift(lr + shift, partLift) - shift;
                lt = CappedLift(lt + shift, partLift) - shift;
            }

            body = FromLch(Clamp(lb + shift, 0.02, 0.98), Math.Min(cb, cap), hb, body.a);
            rim = FromLch(Clamp(lr + shift, 0.02, 0.98), Math.Min(cr, cap), hr, rim.a);
            through = FromLch(Clamp(lt + shift, 0.02, 0.98), Math.Min(ct, cap), ht, through.a);

            if (LevelLogOn && e != null && !float.IsNaN(partT)) LogLevel(e, partT, partU, probeDL, probeAxesL, probePartL, target,
                Clamp(lb + shift, 0.02, 0.98), Math.Min(cb, cap), hb, Clamp(lr + shift, 0.02, 0.98), Math.Min(cr, cap), hr,
                Clamp(lt + shift, 0.02, 0.98), Math.Min(ct, cap), ht, body, rim, through);

            if (wantTint && axes && PatternTintOn)
            {
                double bodyL = Clamp(lb + shift, 0.02, 0.98);
                double tintL = bodyL < 0.5 ? Math.Min(0.95, bodyL + PatternTintFlip) : Math.Max(0.05, bodyL - PatternTintFlip);
                double tintHue = trunkHue + PatternTintTurn(e) * 2.0 * Math.PI;

                double tintFloor = NeutralOn ? PatternTintChromaFloor * NeutralFactor(e) : PatternTintChromaFloor;
                double tintChroma = Math.Max(Math.Min(cb, cap), tintFloor);

                if (ZonePatternOn && ZoneGateOn(e) && !float.IsNaN(e.ZoneRank))
                {
                    // The tint takes the zone hue and a bright chroma, not scaled by the neutral factor: a drab body may carry a bright pattern.
                    tintHue = trunkHue + ZoneTurn(e) * 2.0 * Math.PI;
                    tintChroma = Math.Max(Math.Min(cb, cap), ZoneChroma);
                }

                tint = FromLch(tintL, tintChroma, tintHue, PatternTint);
            }

            // The family dial: the family colour is made from the body's levelled lightness and its hue before the part shift.
            if (wantTint && axes && FamilyOn)
                family = FamilyColour(e, (float)Clamp(lb + shift, 0.02, 0.98), trunkHue);
        }

        /// <summary>Folds an arc position into [0, 1] by reflection: 1.1 is 0.9, -0.2 is 0.2, so a colour past either end comes back along the arc.</summary>
        private static float Reflect01(float x)
        {
            float r = Mathf.Repeat(x, 2f);

            return r > 1f ? 2f - r : r;
        }

        /// <summary>The short way round from palette hue <paramref name="from"/> to <paramref name="to"/> (fractions of the wheel), in [-0.5, 0.5).</summary>
        private static double ShortTurn(float from, float to) =>
            Mathf.Repeat(to - from + 0.5f, 1f) - 0.5f;

        /// <summary>
        /// The part-colour dial's hue turn of a part, in OKLab hue radians: with dH +1 for a chroma rank of a half or more and -1 below,
        /// the part's arc position <c>p = baseHue + PartColour * (dH * PartHueSwing * t + PartNodeSwing * (2u - 1))</c> folded into [0, 1]
        /// by reflection, and the turn <c>PaletteHue(p) - PaletteHue(baseHue)</c> the short way round. The palette's hue and OKLab's both
        /// run red, yellow, green, cyan, blue, magenta, so the turn is carried over as an angle (approximately: they are not the same
        /// wheel). Zero with the dial off. Called only while <see cref="AxesOn"/> and with the ranks present.
        /// </summary>
        public static double PartHueTurn(Entry e, float t, float u)
        {
            if (!PartOn) return 0.0;

            float baseHue = Mathf.Clamp01(e.V[BaseHue]);
            float dH = e.ChromaRank >= 0.5f ? 1f : -1f;
            float p = Reflect01(baseHue + PartColour * (dH * PartHueSwing * t + PartNodeSwing * (2f * u - 1f)));

            return ShortTurn(PaletteHue(baseHue), PaletteHue(p)) * 2.0 * Math.PI;
        }

        /// <summary>
        /// The pattern tint's hue turn from the body's, a fraction of the wheel: the arc position <c>q = Repeat(baseHue + PatternTintHue, 1)</c>
        /// (wrapping, no reflection: the tint needs no continuity with the body) and <c>PaletteHue(q) - PaletteHue(baseHue)</c>, the short
        /// way round. Zero at a <see cref="PatternTintHue"/> of zero.
        /// </summary>
        public static double PatternTintTurn(Entry e)
        {
            float baseHue = Mathf.Clamp01(e.V[BaseHue]);
            float q = Mathf.Repeat(baseHue + PatternTintHue, 1f);

            return ShortTurn(PaletteHue(baseHue), PaletteHue(q));
        }

        /// <summary>
        /// The zone colour's arc position, <c>q = Repeat(baseHue + 0.25 + 0.5 * ZoneRank, 1)</c> (wrapping, no reflection): a quarter to three
        /// quarters of the arc from the body's, so the second hue is always a contrasting one and never the body's own. For an entry with a zone rank.
        /// </summary>
        private static float ZoneArc(Entry e) => Mathf.Repeat(Mathf.Clamp01(e.V[BaseHue]) + 0.25f + 0.5f * e.ZoneRank, 1f);

        /// <summary>The zone colour's palette hue, a fraction of the wheel (see <see cref="ZoneArc"/>). For an entry with a zone rank.</summary>
        public static float ZoneHue(Entry e) => PaletteHue(ZoneArc(e));

        /// <summary>
        /// The zone hue's turn from the body's, a fraction of the wheel: <c>PaletteHue(q) - PaletteHue(baseHue)</c>, the short way round,
        /// q the arc position <c>Repeat(baseHue + 0.25 + 0.5 * ZoneRank, 1)</c>. Zero when <see cref="ZoneOn"/> is off, for no entry, when
        /// the zone rank is NaN or when the body's zones gate is off (<see cref="ZoneGateOn"/>).
        /// </summary>
        public static double ZoneTurn(Entry e)
        {
            if (!ZoneOn || e == null || float.IsNaN(e.ZoneRank) || !ZoneGateOn(e)) return 0.0;

            return ShortTurn(PaletteHue(Mathf.Clamp01(e.V[BaseHue])), ZoneHue(e));
        }

        /// <summary>
        /// The rim-contrast dial's one write (<c>EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST</c>): <c>_SkinMap</c> rewritten with the rim band's
        /// turn <c>RimHueRadians + turn * 2 pi + pi</c>, the body's palette turn (the palette's, a fraction of the wheel) plus half a turn,
        /// so the stripe at a wide-hue part's edge is the opposite colour (under <see cref="ZoneOn"/>, for a body with a zone rank, the
        /// zone colour: <c>ZoneTurn * 2 pi</c> in place of the half turn). Written after <see cref="WriteTurnAll"/>, so it wins where
        /// both are on (turn-all's share is not added). Only for a body with a vector. Called by the palette only while
        /// <see cref="RimContrastOn"/>, for a wide-hue part in colour.
        /// </summary>
        public static void WriteRimContrast(MaterialPropertyBlock block, Entry entry, float turn)
        {
            if (entry == null) return;

            float away = ZoneOn && ZoneGateOn(entry) && !float.IsNaN(entry.ZoneRank) ? (float)ZoneTurn(entry) * 6.2831855f : 3.1415927f;

            block.SetVector(Ids.Map, new Vector4(
                RimWidthScale(entry), RimHueRadians(entry) + turn * 6.2831855f + away, MottleGain(entry), CountershadeShare(entry)));
        }

        /// <summary>The pattern tint's write: <c>_PatternTint</c> (rgb the colour, alpha the share, zero for none) on a part's block. Called only while <see cref="PatternTintOn"/>.</summary>
        public static void WritePatternTint(MaterialPropertyBlock block, Color tint) => block.SetColor(Ids.PatternTint, tint);

        /// <summary>
        /// The pattern family of a body: -1 when <see cref="FamilyOn"/> is off, for no entry or when either rank is NaN; the forced family under
        /// <see cref="FamilyForce"/> (zero or more); otherwise <c>(ValueRank >= 0.5 ? 2 : 0) + (SheenRank >= 0.5 ? 1 : 0)</c>. 0 is the maze
        /// alone (nothing added), 1 bars, 2 eyespot, 3 edge outline. Both ranks are heritable lineage scores, so kin mostly share a family.
        /// </summary>
        public static int FamilyOf(Entry e)
        {
            if (!FamilyOn || e == null || float.IsNaN(e.ValueRank) || float.IsNaN(e.SheenRank)) return -1;
            if (FamilyForce >= 0) return FamilyForce;

            return (e.ValueRank >= 0.5f ? 2 : 0) + (e.SheenRank >= 0.5f ? 1 : 0);
        }

        /// <summary>True when the mean of two ranks is over a mix threshold; false when either is NaN.</summary>
        private static bool MixGate(float a, float b, float threshold) => !float.IsNaN(a) && !float.IsNaN(b) && 0.5f * (a + b) > threshold;

        /// <summary>The maze gate: sheen and 1 - value over <see cref="MixMaze"/>. False for a NaN rank.</summary>
        private static bool MixMazeOf(Entry e) => MixGate(e.SheenRank, 1f - e.ValueRank, MixMaze);

        /// <summary>The zones gate: hue (<c>V[BaseHue]</c> clamped to 0 to 1) and chroma over <see cref="MixZones"/>. False for a NaN rank.</summary>
        private static bool MixZonesOf(Entry e) => MixGate(Mathf.Clamp01(e.V[BaseHue]), e.ChromaRank, MixZones);

        /// <summary>
        /// A body's mix of bars (1), eyespot (2) and edge outline (4) as a bitmask, from its gates (bars: zone and sheen over
        /// <see cref="MixBars"/>; eyespot: chroma and 1 - zone over <see cref="MixEyespot"/>; edge: zone and chroma over <see cref="MixEdge"/>).
        /// Zero with <see cref="MixOn"/> off or for no entry.
        /// </summary>
        public static int MixBitsOf(Entry e)
        {
            if (!MixOn || e == null) return 0;

            return (MixGate(e.ZoneRank, e.SheenRank, MixBars) ? 1 : 0) +
                   (MixGate(e.ChromaRank, 1f - e.ZoneRank, MixEyespot) ? 2 : 0) +
                   (MixGate(e.ZoneRank, e.ChromaRank, MixEdge) ? 4 : 0);
        }

        /// <summary>
        /// The body's pattern (maze) strength under the mix: <paramref name="strength"/> as it is with <see cref="MixOn"/> off or for no entry,
        /// otherwise zero where the maze gate is off. The palette puts it in <c>_Pattern</c> y.
        /// </summary>
        public static float MazeStrength(Entry e, float strength) => !MixOn || e == null || MixMazeOf(e) ? strength : 0f;

        /// <summary>
        /// False when <see cref="MixOn"/> and the body's zones gate is off, so it takes no second hue (its zone parts, rim stripe and zone
        /// pattern tint keep its own hue, as with <see cref="Zone"/> at zero); true otherwise and for no entry.
        /// </summary>
        public static bool ZoneGateOn(Entry e) => !MixOn || e == null || MixZonesOf(e);

        /// <summary>
        /// The family colour's hue turn from the body's, a fraction of the wheel: the zone hue's (<see cref="ZoneArc"/>) for an entry with a zone
        /// rank, whether or not <see cref="ZoneOn"/> is on; otherwise half the arc round, <c>PaletteHue(Repeat(baseHue + 0.5, 1))</c>.
        /// </summary>
        private static double FamilyTurn(Entry e)
        {
            float baseHue = Mathf.Clamp01(e.V[BaseHue]);

            if (!float.IsNaN(e.ZoneRank)) return ShortTurn(PaletteHue(baseHue), ZoneHue(e));

            return ShortTurn(PaletteHue(baseHue), PaletteHue(Mathf.Repeat(baseHue + 0.5f, 1f)));
        }

        /// <summary>
        /// The family colour: the body's hue (<paramref name="bodyHue"/>, OKLab radians) turned by <see cref="FamilyTurn"/>, the zone chroma
        /// (<see cref="ZoneChroma"/>, or <see cref="FamilyChroma"/> when over zero), OKLab lightness 0.88 on a dark body
        /// (<paramref name="bodyOkL"/> under a half) and 0.14 on a pale one (toward 0.70 and 0.30 by <see cref="FamilyLight"/>), bisected into
        /// the gamut; alpha <see cref="Family"/>. Alpha zero when <see cref="FamilyOn"/> is off or there is no entry. The hue, after the turn
        /// and before the colour is made, is moved out of the pink band by <see cref="FamilyPinkAway"/> (<see cref="PinkAway"/>).
        /// </summary>
        public static Color FamilyColour(Entry e, float bodyOkL, double bodyHue)
        {
            if (!FamilyOn || e == null) return new Color(0f, 0f, 0f, 0f);

            double hue = bodyHue + FamilyTurn(e) * 2.0 * Math.PI;

            if (FamilyPinkAway > 0f) hue += PinkAway(hue) * Math.PI / 180.0;

            float warm = WarmWeight(hue);

            if (FamilyRust > 0f) hue += FamilyRust * RustTurn(hue) * Math.PI / 180.0;

            float light = bodyOkL < 0.5f ? FamilyLightDark : FamilyLightPale;

            if (FamilyPinkDeepen > 0f && bodyOkL < 0.5f) light = Mathf.Lerp(light, WarmDeepL, FamilyPinkDeepen * warm);

            return FromLch(light, FamilyChroma > 0f ? FamilyChroma : ZoneChroma, hue, Family);
        }

        /// <summary>
        /// The degrees <see cref="FamilyRust"/> at 1 turns <paramref name="hue"/> (OKLab radians) by, before <see cref="WarmWeight"/>: on the
        /// coral side (<see cref="RustFrom"/> through 0 to <see cref="RustHue"/>) the way to <see cref="RustHue"/>, ramped in by a raised cosine
        /// over the first <see cref="RustRamp"/> degrees so that kin either side of <see cref="RustFrom"/> stay close; zero elsewhere.
        /// </summary>
        private static double RustTurn(double hue)
        {
            double from = hue * 180.0 / Math.PI - RustFrom;

            from -= 360.0 * Math.Floor(from / 360.0);

            double span = RustHue + 360.0 - RustFrom;
            if (from >= span) return 0.0;

            double ramp = from >= RustRamp ? 1.0 : 0.5 * (1.0 - Math.Cos(Math.PI * from / RustRamp));

            return ramp * (span - from);
        }

        /// <summary>
        /// The weight of <see cref="FamilyPinkDeepen"/> at <paramref name="hue"/> (OKLab radians): 1 within <see cref="WarmFlat"/> of
        /// <see cref="WarmHalfWidth"/> from <see cref="WarmCentre"/>, falling by a raised cosine to 0 at the band's edges, 0 outside.
        /// </summary>
        private static float WarmWeight(double hue)
        {
            double offset = hue * 180.0 / Math.PI - WarmCentre;

            offset -= 360.0 * Math.Floor((offset + 180.0) / 360.0);

            double distance = Math.Abs(offset), flat = WarmFlat * WarmHalfWidth;
            if (distance >= WarmHalfWidth) return 0f;
            if (distance <= flat) return 1f;

            return (float)(0.5 * (1.0 + Math.Cos(Math.PI * (distance - flat) / (WarmHalfWidth - flat))));
        }

        /// <summary>
        /// The degrees the family mark's hue (<paramref name="hue"/>, OKLab radians) moves out of the pink band: <see cref="FamilyPinkAway"/> times a
        /// raised-cosine weight (1 at <see cref="PinkCentre"/>, 0 at the band's edges and outside) times the angular distance to the nearer edge,
        /// signed toward that edge (a hue at or below the centre moves down, so the centre itself goes to 315). Zero outside the band.
        /// </summary>
        private static double PinkAway(double hue)
        {
            double degrees = hue * 180.0 / Math.PI;
            double offset = degrees - PinkCentre;

            offset -= 360.0 * Math.Floor((offset + 180.0) / 360.0);

            double distance = Math.Abs(offset);
            if (distance >= PinkHalfWidth) return 0.0;

            double weight = 0.5 * (1.0 + Math.Cos(Math.PI * distance / PinkHalfWidth));
            double move = FamilyPinkAway * weight * (PinkHalfWidth - distance);

            return offset <= 0.0 ? -move : move;
        }

        /// <summary>
        /// The oral-disc stripe count (<c>EVOSIM_THEATRE_MOUTH2_DISC</c>, <c>TheatreMouth2</c>), which also picks a flat mouth's kind
        /// (<c>TheatreMouth2.MouthKind</c>): 6, 8, 10 or 12, <c>6 + 2 * floor(4 * SheenRank)</c>. The rank is a population quantile, so the
        /// four counts are about equally common and kin, whose ranks sit close, share a count except near a quarter's edge. It was
        /// <c>frac(11 * baseHue)</c>, which turned a small difference in hue between kin into a different count: r62-h6202's family of six
        /// drew four different mouths. With no sheen rank (the axes dial off) the base hue stands in for it. 8 for no entry.
        /// </summary>
        public static int MouthStripes(Entry e)
        {
            if (e == null) return 8;

            float rank = float.IsNaN(e.SheenRank) ? Mathf.Clamp01(e.V[BaseHue]) : Mathf.Clamp01(e.SheenRank);

            return 6 + 2 * Mathf.Min(3, Mathf.FloorToInt(4f * rank));
        }

        /// <summary>
        /// The oral disc's colour (<c>EVOSIM_THEATRE_MOUTH2_DISC</c>): the body's colour, <paramref name="linearBody"/> (the shader's linear rgb) at another OKLab
        /// lightness, lighter by 0.25 on a body under L 0.5 and darker by 0.25 on one above, with hue and chroma kept and the chroma bisected into
        /// the gamut as the family colour's is. Linear rgb out, alpha one. Independent of the axes dial.
        /// </summary>
        public static Color MouthDiscColour(Vector4 linearBody)
        {
            var gamma = new Color(
                (float)ToGamma(Math.Max(0.0, linearBody.x)), (float)ToGamma(Math.Max(0.0, linearBody.y)), (float)ToGamma(Math.Max(0.0, linearBody.z)), 1f);

            ToLch(gamma, out double l, out double chroma, out double hue);

            double shifted = Clamp(l < 0.5 ? l + MouthDiscShift : l - MouthDiscShift, 0.0, 1.0);

            return FromLch(shifted, chroma, hue, 1f).linear;
        }

        /// <summary>How far the oral disc's lightness is from the body's, in OKLab L (<see cref="MouthDiscColour"/>).</summary>
        private const double MouthDiscShift = 0.25;

        /// <summary>
        /// The bars' count, 2, 3 or 4: <c>2 + floor(3 * frac(7 * baseHue))</c>, a hash of the body's hue rank, so kin share a count. For an entry.
        /// </summary>
        private static int FamilyBars(Entry e)
        {
            float x = Mathf.Clamp01(e.V[BaseHue]) * 7f;

            return 2 + Mathf.Min(2, Mathf.FloorToInt(3f * (x - Mathf.Floor(x))));
        }

        /// <summary>
        /// The family's write, on a part's block: <c>_FamilyColor</c> (rgb the colour, alpha the share) and <c>_FamilyParams</c> (the family as
        /// a float, the strength, the bar count, <see cref="FamilyEdge"/> for the edge outline and 0 for the others). Zeros for a body in family 0, one with no family, or a part with no family colour (a
        /// part not levelled), since a block cannot forget a property. Under <see cref="MixOn"/> the family is the mix: <c>_FamilyParams</c> is
        /// (0, the strength, the bar count, 0) and <c>_FamilyMix</c> (the bitmask of bars 1, eyespot 2, edge outline 4, the edge's widening, <see cref="FamilyEdgeBend"/>, 0), both zero for a body with none.
        /// Called by the palette only while <see cref="FamilyOn"/>.
        /// </summary>
        public static void WriteFamily(MaterialPropertyBlock block, Entry e, Color familyColour)
        {
            if (MixOn)
            {
                // The mix: the bars, eyespot and edge outline a body's gates select, as a bitmask in _FamilyMix x (the edge's widening in y,
                // its bend gate in z).
                int bits = MixBitsOf(e);
                bool none = bits == 0 || familyColour.a <= 0f;

                block.SetColor(Ids.FamilyColor, none ? new Color(0f, 0f, 0f, 0f) : familyColour);
                block.SetVector(Ids.FamilyParams, none ? Vector4.zero : new Vector4(0f, Family, FamilyBars(e), 0f));
                block.SetVector(Ids.FamilyMix, none ? Vector4.zero : new Vector4(bits, FamilyEdge, FamilyEdgeBend, 0f));
                return;
            }

            int mode = FamilyOf(e);

            if (mode < 1 || familyColour.a <= 0f)
            {
                block.SetColor(Ids.FamilyColor, new Color(0f, 0f, 0f, 0f));
                block.SetVector(Ids.FamilyParams, Vector4.zero);
                return;
            }

            block.SetColor(Ids.FamilyColor, familyColour);
            block.SetVector(Ids.FamilyParams, new Vector4(mode, Family, FamilyBars(e), mode == 3 ? FamilyEdge : 0f));
        }

        /// <summary>
        /// The brown bias of a body, b = <see cref="Brown"/> * w, w a raised cosine of the palette hue h (degrees) about
        /// <see cref="BrownCentre"/> with half-width <see cref="BrownWidth"/> (30 degrees by default): 1 at the centre, 0 at the band's edges
        /// and outside. Zero with the dial at zero. Called only while <see cref="AxesOn"/>.
        /// </summary>
        public static double BrownAmount(Entry e)
        {
            if (Brown <= 0f || e == null) return 0.0;

            double d = Math.Abs(Mathf.Repeat(PaletteHue(e.V[BaseHue]) * 360f - BrownCentre + 180f, 360f) - 180f);
            if (d >= BrownWidth) return 0.0;

            return Brown * 0.5 * (1.0 + Math.Cos(Math.PI * d / BrownWidth));
        }

        /// <summary>
        /// The yellow band's weight at a palette hue (a fraction of the wheel): a raised cosine of the hue in degrees about
        /// <see cref="YellowCentre"/> with half-width <see cref="YellowHalfWidth"/>, 1 at the centre, 0 at the band's edges and outside. Zero
        /// when <see cref="YellowLiftOn"/> is off. The dial itself is not in it.
        /// </summary>
        public static double YellowWeight(float paletteHueFraction)
        {
            if (!YellowLiftOn) return 0.0;

            double d = Math.Abs(Mathf.Repeat(paletteHueFraction * 360f - YellowCentre + 180f, 360f) - 180f);
            if (d >= YellowHalfWidth) return 0.0;

            return 0.5 * (1.0 + Math.Cos(Math.PI * d / YellowHalfWidth));
        }

        /// <summary>
        /// The neutral dial's factor on a body's chroma (and on the pattern tint's chroma floor): 1 when <see cref="NeutralOn"/> is off, for
        /// no entry, or when either rank is NaN; otherwise <c>1 - Neutral * max(wv, ws)</c>, wv the value-rank end weight
        /// <c>SmoothStep(NeutralValueEdge, 1, |2 * ValueRank - 1|)</c> and ws the gloss weight <c>SmoothStep(NeutralSheenEdge, 1, SheenRank)</c>.
        /// </summary>
        public static double NeutralFactor(Entry e)
        {
            if (!NeutralOn || e == null || float.IsNaN(e.ValueRank) || float.IsNaN(e.SheenRank)) return 1.0;

            double wv = SmoothStep(NeutralEdge, 1.0, Math.Abs(2.0 * e.ValueRank - 1.0));
            double ws = SmoothStep(NeutralSheenEdge, 1.0, e.SheenRank);

            return 1.0 - Neutral * Math.Max(wv, ws);
        }

        /// <summary>
        /// The dark dial's weight on a body: 0 when <see cref="DarkOn"/> is off, for no entry, or when the value rank is NaN; otherwise
        /// <c><see cref="Dark"/> * SmoothStep(NeutralEdge, 1, max(0, 1 - 2 * ValueRank))</c>. Only the dark side counts: a value rank of a half
        /// or more gives 0, a rank of 0 gives <see cref="Dark"/>. Part lightness and the pattern tint are not touched, so a black body keeps a
        /// lighter maze and lighter tips.
        /// </summary>
        public static double DarkWeight(Entry e)
        {
            if (!DarkOn || e == null || float.IsNaN(e.ValueRank)) return 0.0;

            return Dark * SmoothStep(NeutralEdge, 1.0, Math.Max(0.0, 1.0 - 2.0 * e.ValueRank));
        }

        /// <summary>
        /// The violet dial's amount on a body, <see cref="Violet"/> * w, w a raised cosine of the palette hue h (degrees) about
        /// <see cref="VioletCentre"/> with half-width <see cref="VioletHalfWidth"/>: 1 at the centre, 0 at the band's edges and outside.
        /// Zero when <see cref="VioletOn"/> is off or for no entry.
        /// </summary>
        public static double VioletAmount(Entry e)
        {
            if (!VioletOn || e == null) return 0.0;

            double d = Math.Abs(Mathf.Repeat(PaletteHue(e.V[BaseHue]) * 360f - VioletCentre + 180f, 360f) - 180f);
            if (d >= VioletHalfWidth) return 0.0;

            return Violet * 0.5 * (1.0 + Math.Cos(Math.PI * d / VioletHalfWidth));
        }

        /// <summary>The cubic smooth step: <c>x = clamp01((v - e0) / (e1 - e0))</c>, then <c>x * x * (3 - 2x)</c>.</summary>
        private static double SmoothStep(double e0, double e1, double v)
        {
            double x = Clamp((v - e0) / (e1 - e0), 0.0, 1.0);

            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// A tissue's sheen with the axes dial applied: <c>sheen * lerp(SheenLow, SheenHigh, SheenRank ^ SheenPower)</c>, clamped to [0, 1].
        /// A body with no sheen rank gets its sheen back. Called by the palette only while <see cref="AxesOn"/>, for a wide-hue part in
        /// colour that is not dead or aged.
        /// </summary>
        public static float Sheened(float sheen, Entry e)
        {
            if (e == null || float.IsNaN(e.SheenRank)) return sheen;

            float factor = Mathf.Lerp(SheenLow, SheenHigh, Mathf.Pow(e.SheenRank, SheenPower));

            return Mathf.Clamp01(sheen * factor);
        }

        private static string F0(float x) => x.ToString("F0", CultureInfo.InvariantCulture);

        private static string F2(float x) => x.ToString("F2", CultureInfo.InvariantCulture);

        private static string F3(float x) => x.ToString("F3", CultureInfo.InvariantCulture);

        private static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double ToGamma(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

        private static double Cbrt(double x) => x < 0.0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);

        /// <summary>A gamma-encoded sRGB colour as OKLab lightness, chroma and hue (radians).</summary>
        private static void ToLch(Color c, out double l, out double chroma, out double hue)
        {
            double r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);

            double lc = Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
            double mc = Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
            double sc = Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

            l = 0.2104542553 * lc + 0.7936177850 * mc - 0.0040720468 * sc;
            double a = 1.9779984951 * lc - 2.4285922050 * mc + 0.4505937099 * sc;
            double bb = 0.0259040371 * lc + 0.7827717662 * mc - 0.8086757660 * sc;

            chroma = Math.Sqrt(a * a + bb * bb);
            hue = Math.Atan2(bb, a);
        }

        /// <summary>Gamma-encoded sRGB from OKLab L, C, h, clamped; false when a channel fell outside [0, 1] beyond a small tolerance.</summary>
        private static bool TryLch(double l, double chroma, double hue, out double r, out double g, out double b)
        {
            double a = chroma * Math.Cos(hue), bb = chroma * Math.Sin(hue);

            double lc = l + 0.3963377774 * a + 0.2158037573 * bb;
            double mc = l - 0.1055613458 * a - 0.0638541728 * bb;
            double sc = l - 0.0894841775 * a - 1.2914855480 * bb;

            lc = lc * lc * lc;
            mc = mc * mc * mc;
            sc = sc * sc * sc;

            double lr = 4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc;
            double lg = -1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc;
            double lb = -0.0041960863 * lc - 0.7034186147 * mc + 1.7076147010 * sc;

            const double tol = 1e-5;
            bool inside = lr >= -tol && lr <= 1.0 + tol && lg >= -tol && lg <= 1.0 + tol && lb >= -tol && lb <= 1.0 + tol;

            r = ToGamma(Clamp(lr, 0.0, 1.0));
            g = ToGamma(Clamp(lg, 0.0, 1.0));
            b = ToGamma(Clamp(lb, 0.0, 1.0));

            return inside;
        }

        /// <summary>The colour of OKLab L, C, h; out of gamut, the chroma is bisected down (12 steps, L and hue kept) to the largest that fits.</summary>
        private static Color FromLch(double l, double chroma, double hue, float alpha)
        {
            double r, g, b;
            if (!TryLch(l, chroma, hue, out r, out g, out b))
            {
                double lo = 0.0, hi = chroma;
                for (int i = 0; i < 12; i++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (TryLch(l, mid, hue, out r, out g, out b)) lo = mid; else hi = mid;
                }

                TryLch(l, lo, hue, out r, out g, out b);
            }

            return new Color((float)r, (float)g, (float)b, alpha);
        }

        // ---------------------------------------------------------------- the log

        // A colour's lightness after the part dial's lift, held under PartLightMax: it never rises past the cap, nor falls below where
        // it stood without the lift.
        private static double CappedLift(double lifted, double lift) => Math.Min(lifted, Math.Max(lifted - lift, PartLightMax));

        // LevelLogOn's line: which of the levelling's steps moved a part's lightness, and whether each colour fitted the gamut.
        private static void LogLevel(Entry e, float partT, float partU, double dL, double axesL, double partL, double zoneL,
            double lb, double cb, double hb, double lr, double cr, double hr, double lt, double ct, double ht, Color body, Color rim, Color through)
        {
            var inv = CultureInfo.InvariantCulture;
            Debug.Log(string.Format(inv,
                "[Theatre] skin map level: ranks v {0:0.000} c {1:0.000} s {2:0.000}; t {3:0.000} u {4:0.000} dL {5:+0;-0;0}; target L axes {6:0.000} part {7:0.000} zone {8:0.000}; " +
                "body L {9:0.000} C {10:0.000} fits {11}; rim L {12:0.000} C {13:0.000} fits {14}; through L {15:0.000} C {16:0.000} fits {17}; " +
                "out body ({18:0.000}, {19:0.000}, {20:0.000}) rim ({21:0.000}, {22:0.000}, {23:0.000}) through ({24:0.000}, {25:0.000}, {26:0.000})",
                e.ValueRank, e.ChromaRank, e.SheenRank, partT, partU, dL, axesL, partL, zoneL,
                lb, cb, TryLch(lb, cb, hb, out _, out _, out _), lr, cr, TryLch(lr, cr, hr, out _, out _, out _), lt, ct, TryLch(lt, ct, ht, out _, out _, out _),
                body.r, body.g, body.b, rim.r, rim.g, rim.b, through.r, through.g, through.b));
        }

        private static string LogLine(long id, Entry entry)
        {
            var sb = new StringBuilder(512);
            sb.Append("[Theatre] skin map: id ").Append(id.ToString(CultureInfo.InvariantCulture)).Append(" v");

            for (int k = 0; k < entry.Exact.Length; k++)
            {
                sb.Append(' ').Append(entry.Exact[k].ToString("F6", CultureInfo.InvariantCulture));
            }

            sb.Append(" f");

            for (int j = 0; j < entry.Features.Length; j++)
            {
                sb.Append(' ').Append(entry.Features[j].ToString("G9", CultureInfo.InvariantCulture));
            }

            // Mode 3 only, at the end, so the fields a parity script parses in modes 1 and 2 are where they were.
            if (Mode == 3)
            {
                sb.Append(" value ").Append(((double)entry.ValueRank).ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(" chroma ").Append(((double)entry.ChromaRank).ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(" sheen ").Append(((double)entry.SheenRank).ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(" zone ").Append(((double)entry.ZoneRank).ToString("F6", CultureInfo.InvariantCulture));
                if (FamilyOn) sb.Append(" family ").Append(FamilyOf(entry).ToString(CultureInfo.InvariantCulture));

                // The mix's five gates, 1 on and 0 off, in the order maze bars eyespot edge zones (the bars, eyespot and edge are the bitmask's 1, 2 and 4).
                if (MixOn)
                {
                    int bits = MixBitsOf(entry);

                    sb.Append(" mix ").Append(MixMazeOf(entry) ? '1' : '0')
                      .Append(' ').Append((bits & 1) != 0 ? '1' : '0')
                      .Append(' ').Append((bits & 2) != 0 ? '1' : '0')
                      .Append(' ').Append((bits & 4) != 0 ? '1' : '0')
                      .Append(' ').Append(MixZonesOf(entry) ? '1' : '0');
                }
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- the vector

        /// <summary>The vector of a genome developed at its adult size, or null when it develops to nothing.</summary>
        private static Entry Compute(Genome genome, RunConfig config)
        {
            Phenotype body = Developer.Develop(
                genome, config?.Development, null, config?.Shapes, null, null, config?.CellTypes);

            double[] x = FeaturesOf(genome, body);
            if (x == null) return null;

            double[] v = VectorOf(x, out double valueRank, out double chromaRank, out double sheenRank, out double zoneRank);
            var entry = new Entry(v, x);

            if (Mode == 3)
            {
                entry.ValueRank = (float)valueRank;
                entry.ChromaRank = (float)chromaRank;
                entry.SheenRank = (float)sheenRank;
                entry.ZoneRank = (float)zoneRank;
            }

            return entry;
        }

        /// <summary>
        /// The ten parameters from a feature row: z, the brain markers' shrink, the clip, the logistic. In mode 3 the value, chroma,
        /// sheen and zone ranks come out too (NaN in the other modes).
        /// </summary>
        private static double[] VectorOf(double[] x, out double valueRank, out double chromaRank, out double sheenRank, out double zoneRank)
        {
            string[] names = TheatreSkinMapConstants.Features;
            double[] mean = TheatreSkinMapConstants.Mean;
            double[] sd = TheatreSkinMapConstants.Sd;

            // The brain markers' connection count: the Python takes it from brain_log_links (expm1 of the log), which is the count.
            int linksAt = Array.IndexOf(names, "brain_log_links");
            double links = linksAt >= 0 ? Math.Exp(x[linksAt]) - 1.0 : 0.0;
            double shrink = links / (links + TheatreSkinMapConstants.BrainShrinkLinks);

            var z = new double[names.Length];

            for (int j = 0; j < names.Length; j++)
            {
                z[j] = sd[j] > 0.0 ? (x[j] - mean[j]) / sd[j] : 0.0;
                if (names[j].StartsWith("brain_w_", StringComparison.Ordinal)) z[j] *= shrink;

                double clip = TheatreSkinMapConstants.ZClip;
                z[j] = Math.Max(-clip, Math.Min(clip, z[j]));
            }

            var v = new double[TheatreSkinMapConstants.ParameterNames.Length];

            for (int k = 0; k < v.Length; k++)
            {
                (int Feature, double Weight)[] inputs = TheatreSkinMapConstants.Inputs[k];

                if (inputs.Length == 0)
                {
                    v[k] = 0.5;
                    continue;
                }

                double s = TheatreSkinMapConstants.Bias[k];
                for (int i = 0; i < inputs.Length; i++) s += inputs[i].Weight * z[inputs[i].Feature];

                v[k] = 1.0 / (1.0 + Math.Exp(-s));
            }

            valueRank = double.NaN;
            chromaRank = double.NaN;
            sheenRank = double.NaN;
            zoneRank = double.NaN;

            if (Mode >= 2) ApplyHybrid(v, z);

            if (Mode == 3)
            {
                valueRank = ValueRankOf(z);
                chromaRank = RankOf(z, TheatreSkinMapConstants.ChromaWeights, TheatreSkinMapConstants.ChromaFittingScores);
                sheenRank = RankOf(z, TheatreSkinMapConstants.SheenWeights, TheatreSkinMapConstants.SheenFittingScores);
                zoneRank = RankOf(z, TheatreSkinMapConstants.ZoneWeights, TheatreSkinMapConstants.ZoneFittingScores);
            }

            return v;
        }

        /// <summary>Two lineage scores closer than this are one score for the rank (a last-bit difference in the order of summation).</summary>
        private const double RankTolerance = 1e-12;

        /// <summary>The hybrid's feature indices in <see cref="TheatreSkinMapConstants.Features"/>, resolved once, on first use at dial 2.</summary>
        private static int[] _hybridIndex;

        private static int[] HybridIndexOf()
        {
            string[] names = TheatreSkinMapConstants.Features;
            string[] wanted = TheatreSkinMapConstants.HybridFeatures;
            var index = new int[wanted.Length];

            for (int i = 0; i < wanted.Length; i++)
            {
                index[i] = Array.IndexOf(names, wanted[i]);

                if (index[i] < 0)
                {
                    throw new InvalidOperationException("skin map: the hybrid feature '" + wanted[i] + "' is not a feature of the map.");
                }
            }

            return index;
        }

        /// <summary>
        /// The dial at 2: the base hue becomes the lineage score's middle rank among the fitting set's scores, and the base
        /// saturation the mean of today's and today's base hue (the diet's logistic). <paramref name="z"/> is the clipped, shrunk
        /// array the vector was made from; <paramref name="v"/> holds today's ten values on entry.
        /// </summary>
        private static void ApplyHybrid(double[] v, double[] z)
        {
            double todayHue = v[BaseHue];

            if (_hybridIndex == null) _hybridIndex = HybridIndexOf();

            double[] weights = TheatreSkinMapConstants.HybridWeights;
            double score = 0.0;
            for (int i = 0; i < weights.Length; i++) score += weights[i] * z[_hybridIndex[i]];

            double[] fitting = TheatreSkinMapConstants.HybridFittingScores;

            // lo: how many fitting scores are below the score; hi: how many are at or below it (numpy searchsorted left and
            // right, within RankTolerance).
            int lo = LowerBound(fitting, score, false);
            int hi = LowerBound(fitting, score, true);

            v[BaseHue] = (lo + hi) / 2.0 / fitting.Length;
            v[BaseSaturation] = (v[BaseSaturation] + todayHue) / 2.0;
        }

        /// <summary>
        /// The dial at 3: the middle rank, among the fitting set's value scores, of this genome's value score, a weighted sum of the
        /// same clipped, shrunk z values as the hybrid's lineage score (<see cref="TheatreSkinMapConstants.ValueWeights"/>, over the
        /// same features), by the same rule as the base hue's.
        /// </summary>
        private static double ValueRankOf(double[] z) =>
            RankOf(z, TheatreSkinMapConstants.ValueWeights, TheatreSkinMapConstants.ValueFittingScores);

        /// <summary>
        /// The middle rank, among <paramref name="fitting"/>, of the weighted sum of the clipped, shrunk z values under
        /// <paramref name="weights"/> (over the hybrid's features), by the rule of the base hue's: how the value, chroma and sheen ranks are made.
        /// </summary>
        private static double RankOf(double[] z, double[] weights, double[] fitting)
        {
            if (_hybridIndex == null) _hybridIndex = HybridIndexOf();

            double score = 0.0;
            for (int i = 0; i < weights.Length; i++) score += weights[i] * z[_hybridIndex[i]];

            int lo = LowerBound(fitting, score, false);
            int hi = LowerBound(fitting, score, true);

            return (lo + hi) / 2.0 / fitting.Length;
        }

        /// <summary>The count of entries of an ascending array below <paramref name="x"/>, or at or below it when <paramref name="orEqual"/>.</summary>
        private static int LowerBound(double[] sorted, double x, bool orEqual)
        {
            // A score within the tolerance of a fitting score is that score: a clone family shares one score, and another
            // summation order than numpy's moves it by a last bit, which would otherwise split the family across its ranks.
            x = orEqual ? x + RankTolerance : x - RankTolerance;

            int low = 0, high = sorted.Length;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (orEqual ? sorted[mid] <= x : sorted[mid] < x) low = mid + 1;
                else high = mid;
            }

            return low;
        }

        // ---------------------------------------------------------------- the features (skin_features.py, features_of)

        /// <summary>
        /// The feature row in the order of <see cref="TheatreSkinMapConstants.Features"/>, from the adult body Core developed (the
        /// same part list the theatre draws) and the genome's reproduction numbers and brain; null for a body of no parts.
        /// </summary>
        private static double[] FeaturesOf(Genome genome, Phenotype body)
        {
            IReadOnlyList<PhenotypePart> parts = body.Parts;
            int n = parts.Count;
            if (n == 0) return null;

            double volume = 0.0;
            int mirrored = 0, jointed = 0, maxDepth = 0;
            var aspects = new double[n];
            var lo = new double[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var hi = new double[] { double.MinValue, double.MinValue, double.MinValue };

            for (int i = 0; i < n; i++)
            {
                PhenotypePart p = parts[i];

                volume += p.Volume;
                if (p.Mirrored) mirrored++;
                if (p.JointType != JointType.Fixed) jointed++;
                if (p.Depth > maxDepth) maxDepth = p.Depth;

                aspects[i] = AspectOf(p);

                double[] half = BoxHalf(p);
                double[] centre = { p.Position.X, p.Position.Y, p.Position.Z };

                for (int a = 0; a < 3; a++)
                {
                    lo[a] = Math.Min(lo[a], centre[a] - half[a]);
                    hi[a] = Math.Max(hi[a], centre[a] + half[a]);
                }
            }

            double sizeMax = 0.0, sizeMin = double.MaxValue;

            for (int a = 0; a < 3; a++)
            {
                double size = Math.Max(hi[a] - lo[a], 1e-9);
                sizeMax = Math.Max(sizeMax, size);
                sizeMin = Math.Min(sizeMin, size);
            }

            Array.Sort(aspects);
            double median = n % 2 == 1 ? aspects[n / 2] : 0.5 * (aspects[n / 2 - 1] + aspects[n / 2]);

            ReproductionTraits reproduction = genome.Reproduction;
            BrainSummary(genome, out int neurons, out List<double> weights);

            string[] names = TheatreSkinMapConstants.Features;
            var x = new double[names.Length];

            for (int j = 0; j < names.Length; j++)
            {
                string name = names[j];

                if (name.StartsWith("share_", StringComparison.Ordinal))
                {
                    x[j] = ShareOf(parts, name.Substring(6), volume);
                    continue;
                }

                if (name.StartsWith("shape_", StringComparison.Ordinal))
                {
                    int count = 0;
                    string shape = name.Substring(6);
                    for (int i = 0; i < n; i++) if (parts[i].ShapeId == shape) count++;

                    x[j] = count / (double)n;
                    continue;
                }

                switch (name)
                {
                    case "log_parts": x[j] = Math.Log(n); break;
                    case "log_chain": x[j] = Math.Log(maxDepth + 1); break;
                    case "mirrored_share": x[j] = mirrored / (double)n; break;
                    case "bbox_aspect": x[j] = Math.Log(sizeMax / sizeMin); break;
                    case "part_aspect_median": x[j] = Math.Log(median); break;
                    case "jointed_share": x[j] = jointed / (double)n; break;
                    case "investment": x[j] = reproduction.BirthInvestment; break;
                    case "log_margin": x[j] = Log1p(Math.Max(0.0, reproduction.ReserveMargin)); break;
                    case "brain_log_neurons": x[j] = Log1p(neurons); break;
                    case "brain_log_links": x[j] = Log1p(weights.Count); break;
                    case "brain_w_mean": x[j] = Moments(weights, out _, out _, out _, out _); break;
                    case "brain_w_sd": Moments(weights, out _, out x[j], out _, out _); break;
                    case "brain_w_absmean": Moments(weights, out _, out _, out x[j], out _); break;
                    case "brain_w_pos_share": Moments(weights, out _, out _, out _, out x[j]); break;
                    case "brain_w_absp90": x[j] = AbsPercentile(weights, 90.0); break;
                    default:
                        throw new InvalidOperationException("skin map: the feature '" + name + "' has no computation here.");
                }
            }

            return x;
        }

        /// <summary>The share of the body's volume in one cell type.</summary>
        private static double ShareOf(IReadOnlyList<PhenotypePart> parts, string cellType, double volume)
        {
            if (!(volume > 0.0)) return 0.0;

            double sum = 0.0;

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].CellTypeId == cellType) sum += parts[i].Volume;
            }

            return sum / volume;
        }

        /// <summary>log(1 + x), without the cancellation of Math.Log(1.0 + x) for a small x (the numpy log1p).</summary>
        private static double Log1p(double x)
        {
            double u = 1.0 + x;
            return u == 1.0 ? x : Math.Log(u) * x / (u - 1.0);
        }

        /// <summary>A part's longest over shortest effective extent: a sphere is one, a capsule its half length over its radius.</summary>
        private static double AspectOf(PhenotypePart p)
        {
            double hx = p.HalfExtents.X, hy = p.HalfExtents.Y, hz = p.HalfExtents.Z;

            if (p.ShapeId == ShapeIds.Sphere) return 1.0;

            if (p.ShapeId == ShapeIds.Capsule)
            {
                double r = (hx + hz) / 2.0;
                return r > 0.0 ? Math.Max(1.0, hy / r) : 1.0;
            }

            double low = Math.Min(hx, Math.Min(hy, hz));
            double high = Math.Max(hx, Math.Max(hy, hz));
            return low > 0.0 ? high / low : 1.0;
        }

        /// <summary>
        /// Half the part's world axis-aligned box about its position: a sphere its ball, a capsule its spine's reach plus its
        /// radius, anything else its half extents through the absolute rotation. The rotation is proper, and the axis flip that
        /// recovered it from a reflection changes no absolute value.
        /// </summary>
        private static double[] BoxHalf(PhenotypePart p)
        {
            double hx = p.HalfExtents.X, hy = p.HalfExtents.Y, hz = p.HalfExtents.Z;

            if (p.ShapeId == ShapeIds.Sphere)
            {
                double r = (hx + hy + hz) / 3.0;
                return new[] { r, r, r };
            }

            Float3 c0 = p.Rotation.Rotate(new Float3(1f, 0f, 0f));
            Float3 c1 = p.Rotation.Rotate(new Float3(0f, 1f, 0f));
            Float3 c2 = p.Rotation.Rotate(new Float3(0f, 0f, 1f));

            if (p.ShapeId == ShapeIds.Capsule)
            {
                double r = (hx + hz) / 2.0;
                double span = Math.Max(0.0, hy - r);

                return new[]
                {
                    Math.Abs(c1.X) * span + r,
                    Math.Abs(c1.Y) * span + r,
                    Math.Abs(c1.Z) * span + r,
                };
            }

            return new[]
            {
                Math.Abs(c0.X) * hx + Math.Abs(c1.X) * hy + Math.Abs(c2.X) * hz,
                Math.Abs(c0.Y) * hx + Math.Abs(c1.Y) * hy + Math.Abs(c2.Y) * hz,
                Math.Abs(c0.Z) * hx + Math.Abs(c1.Z) * hy + Math.Abs(c2.Z) * hz,
            };
        }

        // ---------------------------------------------------------------- the brain (skin_features.py, brain_summary)

        /// <summary>
        /// The neuron count and the connection weights: the central brain's outputs and hidden units and its links, or, for a
        /// genome of the older model, every node's neurons and the global brain's, each with its inputs' weights.
        /// </summary>
        private static void BrainSummary(Genome genome, out int neurons, out List<double> weights)
        {
            weights = new List<double>();

            CentralBrain brain = genome.Brain;

            if (brain != null)
            {
                neurons = brain.Outputs.Count + brain.Hidden.Count;
                for (int i = 0; i < brain.Links.Count; i++) weights.Add(brain.Links[i].Weight);
                return;
            }

            neurons = 0;

            for (int i = 0; i < genome.Nodes.Count; i++) AddNeurons(genome.Nodes[i].Neurons, ref neurons, weights);
            AddNeurons(genome.GlobalBrain, ref neurons, weights);
        }

        private static void AddNeurons(NeuronDef[] list, ref int neurons, List<double> weights)
        {
            if (list == null) return;

            for (int i = 0; i < list.Length; i++)
            {
                neurons++;

                NeuronInput[] inputs = list[i].Inputs;
                if (inputs == null) continue;

                for (int k = 0; k < inputs.Length; k++) weights.Add(inputs[k].Weight);
            }
        }

        /// <summary>
        /// The weights' mean, population standard deviation, mean absolute value and share above zero; all zero for none, as the
        /// Python puts zeros for a brain with no connection. The mean is returned and also written to <paramref name="mean"/>.
        /// </summary>
        private static double Moments(List<double> w, out double mean, out double sd, out double absMean, out double positive)
        {
            mean = sd = absMean = positive = 0.0;
            if (w.Count == 0) return 0.0;

            double sum = 0.0, abs = 0.0;
            int above = 0;

            for (int i = 0; i < w.Count; i++)
            {
                sum += w[i];
                abs += Math.Abs(w[i]);
                if (w[i] > 0.0) above++;
            }

            mean = sum / w.Count;
            absMean = abs / w.Count;
            positive = above / (double)w.Count;

            double squares = 0.0;
            for (int i = 0; i < w.Count; i++) squares += (w[i] - mean) * (w[i] - mean);
            sd = Math.Sqrt(squares / w.Count);

            return mean;
        }

        /// <summary>The q-th percentile of the weights' absolute values by linear interpolation between order statistics (numpy's default); zero for none.</summary>
        private static double AbsPercentile(List<double> w, double q)
        {
            if (w.Count == 0) return 0.0;

            var a = new double[w.Count];
            for (int i = 0; i < a.Length; i++) a[i] = Math.Abs(w[i]);
            Array.Sort(a);

            double position = q / 100.0 * (a.Length - 1);
            int below = (int)Math.Floor(position);
            int above = Math.Min(below + 1, a.Length - 1);

            return a[below] + (a[above] - a[below]) * (position - below);
        }
    }
}
