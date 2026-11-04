using System.Collections.Generic;
using Evosim.Core;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// How one cell type looks: its colour, its wet sheen, how much light it lets through when
    /// backlit, and the three numbers that carve its surface.
    /// </summary>
    /// <remarks>
    /// The fields after <see cref="Take"/> are read only under <c>EVOSIM_THEATRE_TISSUE_LOOK</c>
    /// (<see cref="TheatreTissue.LookOn"/>), and <see cref="Saturation"/> only when a bold look sets it.
    /// Today's entries carry today's values in them, which is what the material already draws with,
    /// so they say what the off look is without driving it.
    /// </remarks>
    public sealed class TissueLook
    {
        /// <summary>The cell type this look belongs to, as <see cref="CellTypeIds"/> spells it.</summary>
        public readonly string CellTypeId;

        /// <summary>The tissue's own colour, before the turn, the reserve and the dimming are applied.</summary>
        public readonly Color Colour;

        /// <summary>The wet sheen: a gut wall glossy, a leaf matte, a strut between. A highlight and never a drawn organ.</summary>
        public readonly float Sheen;

        /// <summary>How much light the tissue lets through when backlit: a leaf glows, a stomach wall does not. The shader's <c>_TransGain</c>.</summary>
        public readonly float Transmission;

        /// <summary>The carve's lobe weight, the second component of the shader's <c>_Carve</c>.</summary>
        public readonly float Lobes;

        /// <summary>The carve's wrinkle weight, the third component of the shader's <c>_Carve</c>.</summary>
        public readonly float Wrinkles;

        /// <summary>How deep the carve cuts: the multiplier on the shader's carve depth, the fourth component of <c>_Carve</c>.</summary>
        public readonly float Take;

        /// <summary>
        /// The body's lightness under the rim, in HSL: what makes one tissue pale and another dense.
        /// Today every tissue is drawn at the palette's one lightness (<see cref="TheatrePalette.BodyLightness"/>).
        /// </summary>
        public readonly float Lightness;

        /// <summary>
        /// The rim's colour, drawn as given (turned by lineage and greyed when dim, as the guild is).
        /// Today's path mutes the guild colour into the rim, so today's entries hold that colour here.
        /// </summary>
        public readonly Color RimColour;

        /// <summary>The rim's strength, as a multiplier on the shader's own (<see cref="TheatreTissue.ShaderRimStrength"/>).</summary>
        public readonly float RimStrength;

        /// <summary>The rim's sharpness, the shader's <c>_RimPower</c>: high is a thin bright edge, low a soft wide one.</summary>
        public readonly float RimPower;

        /// <summary>
        /// The colour of the light that comes through the tissue, the shader's <c>_TransTint</c>. Today's
        /// path mutes the guild colour into it, so today's entries hold that colour here.
        /// </summary>
        public readonly Color TransTint;

        /// <summary>The half extent at which the tissue is fully translucent, metres: the shader's <c>_TransMetres</c>.</summary>
        public readonly float TransMetres;

        /// <summary>The mottle's Voronoi cells per metre, the shader's <c>_MottleCellsPerMetre</c>: a fine dense mottle reads as villi.</summary>
        public readonly float MottleCellsPerMetre;

        /// <summary>How strongly the mottle marks the body colour, the shader's <c>_MottleStrength</c>.</summary>
        public readonly float MottleStrength;

        /// <summary>Bands across the part's long axis, as a muscle is banded: the shader's <c>_Banding</c>. Zero for none.</summary>
        public readonly float Banding;

        /// <summary>
        /// The cap on the body's saturation under the rim, in HSL, or <see cref="TheatreTissue.PaletteSaturation"/>
        /// for the palette's own cap (<see cref="TheatrePalette.BodySaturation"/>). Only the bold looks
        /// (<c>EVOSIM_THEATRE_TISSUE_LOOK=2</c>) set one: at the palette's 0.45 every tissue sat in one grey-brown.
        /// </summary>
        public readonly float Saturation;

        /// <summary>
        /// How strongly the surface carries papillae, 0 to 1: small raised bumps, paler at their tips and
        /// darker at their bases, drawn in the normal and the colour and never as a displacement outward
        /// (the shader's <c>_Papillae</c>). Zero for every look but a bold one that sets it.
        /// </summary>
        public readonly float Papillae;

        /// <summary>The papillae's feature points per metre, the shader's <c>_PapillaeCellsPerMetre</c>: spacing is its inverse. Read only where <see cref="Papillae"/> is over zero.</summary>
        public readonly float PapillaeCellsPerMetre;

        /// <summary>
        /// How strongly the edge is frilled, 0 to 1 (the shader's <c>_Frill</c>): on a blade the outline is
        /// scalloped, cut inward between the scallops; on any other part the silhouette is pleated in
        /// shading. Zero for every look but a bold one that sets it.
        /// </summary>
        public readonly float Frill;

        /// <summary>
        /// The colour ramp's tip (<c>EVOSIM_THEATRE_COLOUR_RAMP</c>): the multiplier on the body's colour at the
        /// free end of a part, per channel, with the base keeping the tissue's colour and a smooth curve between
        /// along the part's long axis (the shader's <c>_RampTip</c>, <c>_RampAxis</c>). Over one reads paler or
        /// warmer at the tip, so a base that reads darker is the same ramp seen the other way round. Every entry
        /// names its own, none over about 1.3 on a channel. Read only while that dial is on, so with it off an
        /// entry's answers are what they were. Set once, by <see cref="TheatreTissue"/> as it builds the table.
        /// </summary>
        public Vector3 RampTip { get; internal set; } = Vector3.one;

        /// <summary>A look as the table held it before the tissue look: every later field at today's value.</summary>
        public TissueLook(
            string cellTypeId, Color colour, float sheen, float transmission,
            float lobes, float wrinkles, float take)
            : this(cellTypeId, colour, sheen, transmission, lobes, wrinkles, take,
                TheatreTissue.TodayLightness, colour, 1f, TheatreTissue.ShaderRimPower,
                colour, TheatreTissue.ShaderTransMetres,
                TheatreTissue.ShaderMottleCellsPerMetre, TheatreTissue.ShaderMottleStrength, 0f)
        {
        }

        /// <summary>A tissue look that keeps the palette's own saturation cap.</summary>
        public TissueLook(
            string cellTypeId, Color colour, float sheen, float transmission,
            float lobes, float wrinkles, float take,
            float lightness, Color rimColour, float rimStrength, float rimPower,
            Color transTint, float transMetres,
            float mottleCellsPerMetre, float mottleStrength, float banding)
            : this(cellTypeId, colour, sheen, transmission, lobes, wrinkles, take,
                lightness, rimColour, rimStrength, rimPower, transTint, transMetres,
                mottleCellsPerMetre, mottleStrength, banding, TheatreTissue.PaletteSaturation)
        {
        }

        public TissueLook(
            string cellTypeId, Color colour, float sheen, float transmission,
            float lobes, float wrinkles, float take,
            float lightness, Color rimColour, float rimStrength, float rimPower,
            Color transTint, float transMetres,
            float mottleCellsPerMetre, float mottleStrength, float banding,
            float saturation)
            : this(cellTypeId, colour, sheen, transmission, lobes, wrinkles, take,
                lightness, rimColour, rimStrength, rimPower, transTint, transMetres,
                mottleCellsPerMetre, mottleStrength, banding, saturation, 0f, 0f, 0f)
        {
        }

        /// <summary>A bold look that also carries papillae and a frilled edge.</summary>
        public TissueLook(
            string cellTypeId, Color colour, float sheen, float transmission,
            float lobes, float wrinkles, float take,
            float lightness, Color rimColour, float rimStrength, float rimPower,
            Color transTint, float transMetres,
            float mottleCellsPerMetre, float mottleStrength, float banding,
            float saturation,
            float papillae, float papillaeCellsPerMetre, float frill)
        {
            Papillae = papillae;
            PapillaeCellsPerMetre = papillaeCellsPerMetre;
            Frill = frill;
            CellTypeId = cellTypeId;
            Colour = colour;
            Sheen = sheen;
            Transmission = transmission;
            Lobes = lobes;
            Wrinkles = wrinkles;
            Take = take;
            Lightness = lightness;
            RimColour = rimColour;
            RimStrength = rimStrength;
            RimPower = rimPower;
            TransTint = transTint;
            TransMetres = transMetres;
            MottleCellsPerMetre = mottleCellsPerMetre;
            MottleStrength = mottleStrength;
            Banding = banding;
            Saturation = saturation;
        }
    }

    /// <summary>
    /// The one place a tissue's look lives: one entry per cell type, read by
    /// <see cref="TheatrePalette"/> for colour, sheen, transmission and character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two sets. Today's entries reproduce the look from before the table, value for value: four
    /// tissues (consumer, neural, buoyancy, anchoring) borrow the structural look in them. With
    /// <c>EVOSIM_THEATRE_TISSUE_LOOK</c> on, <see cref="Of"/> answers from the tissue looks instead,
    /// one per cell type, drawn from the reference board (<c>scratch/owner/realism-refs/</c>). A cell
    /// type with no entry is drawn as structural and named once in the log, in either set.
    /// </para>
    /// <para>
    /// The first set of tissue looks stays inside the range of value and saturation today's guild colours were
    /// chosen in against the film's grade: the body is still dark under the rim, and hue, lightness,
    /// rim, sheen and the light through the tissue carry the difference. A number read off a board
    /// picture names the picture; the board's colours were taken under water and in a studio, so
    /// each is a direction and was moved into the grade's range rather than copied.
    /// </para>
    /// <para>
    /// A third set, the bold looks, at <c>EVOSIM_THEATRE_TISSUE_LOOK=2</c>: the same tissues in saturated,
    /// readable colours at a much lighter value, so a body stands out from the teal water instead of
    /// sitting in it as a dark lump. Each bold look sets its own saturation cap and a lightness of 0.26
    /// to 0.66 where the first set kept 0.12 to 0.30, and the warm ones are pushed a little redder than
    /// the board, since the light by depth takes red first (<see cref="BuildBold"/>).
    /// </para>
    /// </remarks>
    public static class TheatreTissue
    {
        /// <summary>
        /// The tissue look's dial (<c>EVOSIM_THEATRE_TISSUE_LOOK</c>, 0 to 2, 0 off by default), read once:
        /// over 0 the tissue looks, at 2 the bold looks.
        /// </summary>
        private static readonly float LookDial = TheatreSkin.Dial("EVOSIM_THEATRE_TISSUE_LOOK", 0f, 0f, 2f);

        /// <summary>
        /// Whether the tissue looks are drawn (<c>EVOSIM_THEATRE_TISSUE_LOOK</c> over 0), read once. Off,
        /// every look is today's and the palette sets nothing it did not set before.
        /// </summary>
        public static readonly bool LookOn = LookDial > 0f;

        /// <summary>
        /// Whether the tissue looks are the bold ones (<c>EVOSIM_THEATRE_TISSUE_LOOK=2</c>). Below 2 the
        /// dial draws the first set, as it did when it ran from 0 to 1.
        /// </summary>
        public static readonly bool Bold = LookDial >= 2f;

        /// <summary>The <see cref="TissueLook.Saturation"/> that means the palette's own cap.</summary>
        public const float PaletteSaturation = -1f;

        /// <summary>The body's lightness under the rim today: the palette's <see cref="TheatrePalette.BodyLightness"/>.</summary>
        public const float TodayLightness = 0.18f;

        /// <summary>The shader's own rim strength, which the body and joint materials keep (<c>TheatreBody.shader</c>, <c>_RimStrength</c>).</summary>
        public const float ShaderRimStrength = 1.3f;

        /// <summary>The shader's own rim sharpness (<c>_RimPower</c>).</summary>
        public const float ShaderRimPower = 1.75f;

        /// <summary>The shader's own fully translucent half extent, metres (<c>_TransMetres</c>).</summary>
        public const float ShaderTransMetres = 0.06f;

        /// <summary>The shader's own mottle cells per metre (<c>_MottleCellsPerMetre</c>).</summary>
        public const float ShaderMottleCellsPerMetre = 26f;

        /// <summary>The shader's own mottle strength (<c>_MottleStrength</c>).</summary>
        public const float ShaderMottleStrength = 0.14f;

        private static readonly Color StructuralColour = new Color(0.72f, 0.74f, 0.78f);

        // The colour ramp's tips (TissueLook.RampTip; EVOSIM_THEATRE_COLOUR_RAMP). Each multiplies the body's
        // colour at a part's free end and leaves its base as the tissue's, so a tissue "darker at its root"
        // is the same ramp read from the other end. They are picked to read under the theatre's blue-green
        // water and not copied from flash photographs, so each is modest: no channel moves by more than
        // about 30 per cent. Bounds: each channel in [0.7, 1.3]; a channel over one lightens, under one darkens.

        /// <summary>A leaf: toward a yellow-green at the tip, as a frond lit at its end. Red and green lift, blue is cut.</summary>
        private static readonly Vector3 RampLeaf = new Vector3(1.20f, 1.12f, 0.78f);

        /// <summary>An absorber: paler and a little warmer at the tip, a sponge's pale rim. Blue lifts least.</summary>
        private static readonly Vector3 RampAbsorber = new Vector3(1.25f, 1.15f, 0.95f);

        /// <summary>A biter, or anything that consumes: paler at the tip, a lighter margin, no change of hue.</summary>
        private static readonly Vector3 RampBiter = new Vector3(1.25f, 1.20f, 1.15f);

        /// <summary>A jointed link: its base darker than its tip, an arm's or a worm's tail's, in the link's pink.</summary>
        private static readonly Vector3 RampLink = new Vector3(1.28f, 1.20f, 1.20f);

        /// <summary>Structure, nerve, float, holdfast and every type without an entry: a small paling toward the tip.</summary>
        private static readonly Vector3 RampOther = new Vector3(1.10f, 1.10f, 1.10f);

        /// <summary>Sets an entry's ramp tip and hands the entry back, so the tables read as they did with the tip beside each.</summary>
        private static TissueLook Ramped(TissueLook look, Vector3 tip)
        {
            look.RampTip = tip;
            return look;
        }

        private static readonly Dictionary<string, TissueLook> Table = Build();

        private static readonly Dictionary<string, TissueLook> TissueTable = BuildTissue();

        private static readonly Dictionary<string, TissueLook> BoldTable = BuildBold();

        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>The look for a cell type, or the structural look (named once in the log) when there is none.</summary>
        public static TissueLook Of(string cellTypeId)
        {
            Dictionary<string, TissueLook> table = LookOn ? (Bold ? BoldTable : TissueTable) : Table;

            if (cellTypeId != null && table.TryGetValue(cellTypeId, out TissueLook look)) return look;

            string key = cellTypeId ?? "";
            if (Reported.Add(key))
                Debug.Log("[Theatre] tissue: no look for '" + key + "', drawn as structural");

            return table[CellTypeIds.Structural];
        }

        private static Dictionary<string, TissueLook> Build()
        {
            var table = new Dictionary<string, TissueLook>();

            Add(table, Ramped(new TissueLook(CellTypeIds.Photosynthetic,
                new Color(0.34f, 0.72f, 0.36f), 0.04f, 1.0f, 1.00f, 0.42f, 1.05f), RampLeaf));

            Add(table, Ramped(new TissueLook(CellTypeIds.Absorptive,
                new Color(0.90f, 0.55f, 0.22f), 0.35f, 0.12f, 0.60f, 0.78f, 1.00f), RampAbsorber));

            // The link's own skin, the pink-purple off the guild wheel (TheatrePalette.Jointed).
            Add(table, Ramped(new TissueLook(CellTypeIds.Link,
                new Color(0.95f, 0.35f, 0.70f), 0.08f, 0.4f, 0.85f, 0.32f, 0.62f), RampLink));

            Add(table, Ramped(Structural(CellTypeIds.Structural), RampOther));
            Add(table, Ramped(Structural(CellTypeIds.Consumer), RampBiter));
            Add(table, Ramped(Structural(CellTypeIds.Neural), RampOther));
            Add(table, Ramped(Structural(CellTypeIds.Buoyancy), RampOther));
            Add(table, Ramped(Structural(CellTypeIds.Anchoring), RampOther));

            return table;
        }

        private static TissueLook Structural(string cellTypeId) =>
            new TissueLook(cellTypeId, StructuralColour, 0.08f, 0.4f, 0.85f, 0.32f, 0.62f);

        // ------------------------------------------------------------ the tissue looks

        // The colours below are sRGB, as every colour in this table is. Each comment names the board
        // pictures the look draws on, by file name under scratch/owner/realism-refs/. The carve's take
        // stays at or under the leaf's 1.05, which TheatrePalette.CarveReach assumes is the heaviest.

        /// <summary>
        /// A leaf: today's green, which the owner accepted, with a finer chloroplast mottle, a slightly
        /// wetter surface and yellower light through it, as a backlit blade of sea lettuce or kelp shows
        /// (algae-fronds-03 and -04, Ulva; algae-fronds-01, Macrocystis; algae-fronds-08, Laminaria).
        /// The rim is about today's rim colour: the guild green muted as the palette mutes it, worked by hand.
        /// </summary>
        private static TissueLook Leaf() => new TissueLook(CellTypeIds.Photosynthetic,
            new Color(0.34f, 0.72f, 0.36f), 0.06f, 1.1f, 1.00f, 0.42f, 1.05f,
            TodayLightness, new Color(0.41f, 0.75f, 0.43f), 1f, ShaderRimPower,
            new Color(0.52f, 0.80f, 0.36f), 0.07f,
            40f, 0.18f, 0f);

        /// <summary>
        /// An absorber: pale warm cream, soft and villous, its gut a faint amber through it. The cream
        /// is the aeolid's cerata and the ascidian's tunic (soft-skin-06, Phyllodesmium, sampled at
        /// about 0.64, 0.52, 0.38; tunicates-sponges-10, Botrylloides, about 0.58, 0.50, 0.42), lifted
        /// and paled. A fine dense mottle for the villi, a low sheen, a soft wide rim, moderate light
        /// through, and more wrinkle than lobe.
        /// </summary>
        private static TissueLook Absorber() => new TissueLook(CellTypeIds.Absorptive,
            new Color(0.82f, 0.75f, 0.64f), 0.10f, 0.55f, 0.50f, 0.90f, 1.00f,
            0.30f, new Color(0.80f, 0.72f, 0.58f), 0.8f, 1.3f,
            new Color(0.85f, 0.55f, 0.30f), 0.08f,
            60f, 0.16f, 0f);

        /// <summary>
        /// A biter: dense, dark maroon, glossy, with a firm strong rim. The red of the Spanish dancer and
        /// the flatworm's dark margin (soft-skin-01, Hexabranchus, sampled at about 0.96, 0.41, 0.36;
        /// soft-skin-04, Pseudobiceros; the sea angel's red gut, swimming-strokes-06, Clione), deepened
        /// to a maroon under the rim. A high sheen, little light through, and a smoother carve, so the
        /// gloss lies on a firm surface and not on a field of facets. Its lightness sits just under
        /// today's 0.18: at 0.13 a biter in mid water read as near black, its maroon lost.
        /// </summary>
        private static TissueLook Biter() => new TissueLook(CellTypeIds.Consumer,
            new Color(0.62f, 0.20f, 0.18f), 0.45f, 0.08f, 0.90f, 0.20f, 0.55f,
            0.165f, new Color(0.85f, 0.38f, 0.32f), 1.35f, 2.4f,
            new Color(0.70f, 0.22f, 0.18f), 0.04f,
            18f, 0.10f, 0f);

        /// <summary>
        /// A nerve: pale, with a blue-white sheen, as the aeolid's white body and the polychaete's
        /// glassy cyan (soft-skin-05, Flabellina, sampled at about 0.54, 0.57, 0.64;
        /// swimming-strokes-09, Tomopteris). The sheen is the shader's white highlight; the blue is
        /// carried by the rim and the light through.
        /// </summary>
        private static TissueLook Nerve() => new TissueLook(CellTypeIds.Neural,
            new Color(0.70f, 0.76f, 0.86f), 0.30f, 0.45f, 0.70f, 0.25f, 0.70f,
            0.24f, new Color(0.70f, 0.80f, 0.92f), 1f, ShaderRimPower,
            new Color(0.62f, 0.74f, 0.95f), 0.06f,
            30f, 0.08f, 0f);

        /// <summary>
        /// Structure: opaque, matte and bone-like, the ivory of bare coral skeleton
        /// (coral-living-bare-01, bleached staghorn), warmed from the board's blue cast. Low sheen, very
        /// little light through, a fine faint mottle, and today's carve.
        /// </summary>
        private static TissueLook Bone(string cellTypeId) => new TissueLook(cellTypeId,
            new Color(0.86f, 0.84f, 0.76f), 0.05f, 0.12f, 0.85f, 0.32f, 0.62f,
            0.26f, new Color(0.80f, 0.78f, 0.70f), 0.8f, ShaderRimPower,
            new Color(0.85f, 0.80f, 0.66f), 0.03f,
            50f, 0.10f, 0f);

        /// <summary>
        /// A float: glassy and mostly see-through, with a bubble's bright thin rim. A dark body stands for
        /// the water seen through it, as a comb jelly or a salp reads in the dark field, with a near-white
        /// light through it and little colour (translucent-bodies-18, a ctenophore, sampled at about
        /// 0.48, 0.50, 0.69; translucent-bodies-16, salps; translucent-bodies-11, Pyrosoma). Smooth,
        /// barely mottled.
        /// </summary>
        private static TissueLook Float() => new TissueLook(CellTypeIds.Buoyancy,
            new Color(0.78f, 0.84f, 0.88f), 0.25f, 1.4f, 0.90f, 0.10f, 0.40f,
            0.12f, new Color(0.88f, 0.94f, 0.98f), 1.6f, 3.5f,
            new Color(0.82f, 0.90f, 0.95f), 0.12f,
            12f, 0.03f, 0f);

        /// <summary>
        /// Anchoring: a dark, rough holdfast, the brown-olive of a kelp's stipe and holdfast
        /// (algae-fronds-06, the kelp forest's stipes, sampled at about 0.17, 0.22, 0.22;
        /// algae-fronds-01, Macrocystis, about 0.31, 0.31, 0.26), browned. Matte, opaque, a coarse
        /// strong mottle and the most wrinkled carve.
        /// </summary>
        private static TissueLook Holdfast() => new TissueLook(CellTypeIds.Anchoring,
            new Color(0.48f, 0.42f, 0.24f), 0.03f, 0.05f, 0.70f, 0.90f, 1.05f,
            0.14f, new Color(0.55f, 0.50f, 0.32f), 0.7f, 1.5f,
            new Color(0.50f, 0.42f, 0.20f), 0.02f,
            40f, 0.22f, 0f);

        /// <summary>
        /// A link: muscle red-pink, banded across its long axis. The board holds no muscle; the red is
        /// moved from the sea angel's and the Spanish dancer's reds (swimming-strokes-06, Clione;
        /// soft-skin-01, Hexabranchus) toward pink, and keeps today's carve.
        /// </summary>
        private static TissueLook Muscle() => new TissueLook(CellTypeIds.Link,
            new Color(0.86f, 0.40f, 0.46f), 0.15f, 0.35f, 0.85f, 0.32f, 0.62f,
            0.20f, new Color(0.92f, 0.50f, 0.56f), 1f, ShaderRimPower,
            new Color(0.92f, 0.42f, 0.42f), 0.05f,
            26f, 0.08f, MuscleBanding);

        /// <summary>How strongly a link's bands darken it, at the darkest of a band.</summary>
        private const float MuscleBanding = 0.22f;

        // noseskin:begin
        /// <summary>
        /// The nose on the one skin (<c>EVOSIM_THEATRE_NOSE_PIT=2</c>; the entry is in the table only while the dial is at 2, so
        /// a nose is drawn as structural otherwise): pale pink-cream, sRGB (0.96, 0.88, 0.84), wet and smooth,
        /// paler than the body it is on. A little more light through it than structure has (0.22 against
        /// 0.12) and a mottle four times as fine as structure's, standing for the lamellae a part shows (the skin's are in the
        /// shader, <c>NoseLamella*</c>). The carve is gentle.
        /// </summary>
        private static TissueLook Nose() => new TissueLook(CellTypeIds.Nose,
            new Color(0.96f, 0.88f, 0.84f), 0.16f, 0.22f, 0.55f, 0.18f, 0.45f,
            0.34f, new Color(0.98f, 0.90f, 0.84f), 0.8f, 1.6f,
            new Color(0.96f, 0.64f, 0.52f), 0.07f,
            200f, 0.10f, 0f);
        // noseskin:end

        private static Dictionary<string, TissueLook> BuildTissue()
        {
            var table = new Dictionary<string, TissueLook>();

            Add(table, Ramped(Leaf(), RampLeaf));
            Add(table, Ramped(Absorber(), RampAbsorber));
            Add(table, Ramped(Muscle(), RampLink));
            Add(table, Ramped(Bone(CellTypeIds.Structural), RampOther));
            Add(table, Ramped(Biter(), RampBiter));
            Add(table, Ramped(Nerve(), RampOther));
            Add(table, Ramped(Float(), RampOther));
            Add(table, Ramped(Holdfast(), RampOther));
            // noseskin:begin
            if (TheatreNosePit.OnSkin) Add(table, Ramped(Nose(), RampOther));
            // noseskin:end

            return table;
        }

        // ------------------------------------------------------------ the bold looks
        //
        // EVOSIM_THEATRE_TISSUE_LOOK=2. What the light and the water do to a body's colour, which these
        // are chosen against (TheatreBody.shader, TheatreWater.hlsl, TheatreSkin, TheatreGrade):
        //   the key is a cold white (0.86, 0.94, 1.0), so a red is lit at about 0.7 of a blue;
        //   under the underwater look's light by depth, red falls 1.8 times as fast as green and blue a
        //   little slower, and the meter gives back three quarters of the green's loss, so a warm colour
        //   drifts toward yellow and then green with depth, and an ivory toward mint; the meter opens the
        //   water behind the body too, so a body's lead over the water's luma shrinks with depth;
        //   the fade into the backdrop starts 3 m from the eye, so a portrait's subject keeps nearly all
        //   its colour to the water, and a far body goes to the water's teal whatever it is painted;
        //   the grade's neutral curve and contrast keep hue, and push a tone above mid-grey (0.18 linear)
        //   brighter and one below it darker, as the tone step's S does about its pivot after them.
        // So the colours are light, which is what lifts a body above the water's luma, and saturated,
        // and the warm ones a little redder than the board, which is what is left after the red-first
        // fall at a portrait's few metres of depth. A red cannot be both deep and light, since red
        // carries a fifth of the luma: the biter is a scarlet at a lightness of 0.56, not a maroon.
        // Every colour is sRGB, and every comment names the board pictures under
        // scratch/owner/realism-refs/. The carve's take stays at or under the leaf's 1.05
        // (TheatrePalette.CarveReach).
        //
        // The surface is rich where the outline is smooth, as a real soft body's is (the board's nudibranchs):
        // a strong mottle, its cells' walls a darker net and its cells a spread of tones, and ridges from the
        // banding on the biter, the holdfast and the muscle. The soft dial takes the cells two and a half times
        // wider and the marking to six tenths, so these strengths are set for the soft skin they are seen with.
        //
        // Papillae and a frilled edge (TissueLook.Papillae, PapillaeCellsPerMetre, Frill; TheatreBody.shader,
        // PapillaField, LeafScallop, FrillPleat). Papillae are the speckled cerata with their pale tips
        // (soft-skin-06, Phyllodesmium) and the fine tubercled mantle of a Chromodoris (soft-skin-08), on the
        // absorber and the biter; a little on structure and the holdfast, whose sponge-like skin is pitted
        // (tunicates-sponges-05, Aplysina); none on the leaf, the float or the nerve, which are smooth.
        // Their spacing is the inverse of the cells per metre: 7 mm on the absorber, 5 mm on the biter, which
        // is a few millimetres to a centimetre on a body of a few centimetres. A frill is on the leaf, whose
        // outline is scalloped (soft-skin-04, Pseudobiceros; soft-skin-01, Hexabranchus; algae-fronds-03 and
        // -04, Ulva), and on the absorber, whose silhouette is pleated in shading. Neither moves a vertex
        // outward.

        /// <summary>The bold link's bands, deeper than the first set's, since a lighter body hides a shallow band.</summary>
        private const float BoldMuscleBanding = 0.35f;

        /// <summary>
        /// A bold leaf: green-gold, yellower and lighter than today's green, the gold of a backlit sea
        /// lettuce or a laminarian blade (algae-fronds-03, Ulva, sampled at about 0.43, 0.48, 0.08;
        /// algae-fronds-04, Ulva; algae-fronds-08, Laminaria, about 0.32, 0.45, 0.01). At hue 87 it reads
        /// near 100 at a few metres' depth, where today's 122 turns teal.
        /// </summary>
        private static TissueLook BoldLeaf() => new TissueLook(CellTypeIds.Photosynthetic,
            new Color(0.55f, 0.80f, 0.25f), 0.08f, 1.1f, 1.00f, 0.42f, 1.05f,
            0.42f, new Color(0.78f, 0.92f, 0.45f), 0.8f, ShaderRimPower,
            new Color(0.80f, 0.92f, 0.40f), 0.07f,
            55f, 0.30f, 0f,
            0.65f,
            0f, 0f, 1.0f);

        /// <summary>
        /// A bold absorber: pale peach to orange, the aeolid's cerata and the sea angel's gut, with a peach
        /// tunicate between (soft-skin-06, Phyllodesmium, sampled at about 0.73, 0.62, 0.44;
        /// swimming-strokes-06, Clione's gut, about 0.94, 0.48, 0.20; tunicates-sponges-10, Botrylloides,
        /// about 0.71, 0.64, 0.53). Pushed toward orange so it is still peach after the red-first fall. A
        /// pale cream rim, an amber light through, and the first set's villous mottle and carve.
        /// </summary>
        private static TissueLook BoldAbsorber() => new TissueLook(CellTypeIds.Absorptive,
            new Color(0.98f, 0.60f, 0.34f), 0.12f, 0.55f, 0.50f, 0.90f, 1.00f,
            0.64f, new Color(1.00f, 0.82f, 0.62f), 0.7f, 1.3f,
            new Color(1.00f, 0.62f, 0.30f), 0.08f,
            90f, 0.32f, 0f,
            0.80f,
            1.0f, 140f, 0.8f);

        /// <summary>
        /// A bold biter: clearly red, the Spanish dancer's scarlet (soft-skin-01, Hexabranchus, sampled at
        /// about 0.88, 0.35, 0.30), its rim an orchid pink between that animal's pale margin (about 0.97,
        /// 0.80, 0.84) and the purple of soft-skin-08 (Chromodoris, about 0.37, 0.26, 0.62), so the edge leans
        /// toward purple and a lineage's turn of the hue takes some biters there; the flatworm's frilled
        /// margin is the shape it stands for (soft-skin-04, Pseudobiceros). Glossy, nearly opaque, a firm
        /// smooth carve.
        /// </summary>
        private static TissueLook BoldBiter() => new TissueLook(CellTypeIds.Consumer,
            new Color(0.95f, 0.22f, 0.20f), 0.45f, 0.10f, 0.90f, 0.20f, 0.55f,
            0.56f, new Color(0.95f, 0.52f, 0.85f), 1.0f, 2.4f,
            new Color(0.85f, 0.18f, 0.22f), 0.04f,
            40f, 0.40f, 0.22f,
            0.80f,
            0.9f, 190f, 0f);

        /// <summary>
        /// A bold nerve: pale blue-white, the aeolid's white body and the polychaete's glassy cyan
        /// (soft-skin-05, Flabellina, sampled at about 0.78, 0.81, 0.86; swimming-strokes-09, Tomopteris,
        /// about 0.36, 0.72, 0.73). A white sheen, a cool rim, a little light through.
        /// </summary>
        private static TissueLook BoldNerve() => new TissueLook(CellTypeIds.Neural,
            new Color(0.84f, 0.90f, 0.98f), 0.30f, 0.50f, 0.70f, 0.25f, 0.70f,
            0.66f, new Color(0.85f, 0.93f, 1.00f), 0.9f, ShaderRimPower,
            new Color(0.70f, 0.82f, 1.00f), 0.06f,
            30f, 0.20f, 0f,
            0.45f,
            0f, 0f, 0f);

        /// <summary>
        /// Bold structure: ivory to sand, bare coral skeleton warmed out of the board's blue cast and a
        /// tunicate's sand (coral-living-bare-01, bleached staghorn, sampled at about 0.67, 0.80, 0.73;
        /// tunicates-sponges-10, Botrylloides, about 0.71, 0.64, 0.53). Warmer than either, since an ivory
        /// goes mint at depth. Matte, opaque, a faint fine mottle and today's carve.
        /// </summary>
        private static TissueLook BoldBone(string cellTypeId) => new TissueLook(cellTypeId,
            new Color(0.96f, 0.80f, 0.58f), 0.05f, 0.12f, 0.85f, 0.32f, 0.62f,
            0.64f, new Color(0.98f, 0.92f, 0.80f), 0.7f, ShaderRimPower,
            new Color(0.95f, 0.85f, 0.66f), 0.03f,
            70f, 0.30f, 0f,
            0.55f,
            0.3f, 110f, 0f);

        /// <summary>
        /// A bold float: glassy and near clear, a comb jelly or a salp. Its body is a pale cool blue just
        /// over the water's value and barely coloured, so it reads as the water seen through glass, and
        /// it carries a bright thin near-white rim, a near-white light through it, and almost no mottle
        /// (translucent-bodies-18, a ctenophore, sampled at about 0.46, 0.50, 0.68; translucent-bodies-16,
        /// salps, about 0.54, 0.63, 0.78; translucent-bodies-17, ctenophores, whose comb rows are the bright
        /// edges the rim stands for).
        /// </summary>
        private static TissueLook BoldFloat() => new TissueLook(CellTypeIds.Buoyancy,
            new Color(0.72f, 0.86f, 0.96f), 0.30f, 1.6f, 0.90f, 0.10f, 0.40f,
            0.26f, new Color(0.92f, 0.97f, 1.00f), 1.8f, 3.0f,
            new Color(0.86f, 0.94f, 1.00f), 0.12f,
            12f, 0.12f, 0f,
            0.30f,
            0f, 0f, 0f);

        /// <summary>
        /// A bold holdfast: olive-brown, a kelp's stipe and holdfast (algae-fronds-01, Macrocystis, sampled
        /// at about 0.37, 0.37, 0.21; algae-fronds-08, Laminaria's stipe, about 0.34, 0.26, 0.02), browner
        /// than the board, since an olive goes green at depth. Matte, opaque, coarse and wrinkled.
        /// </summary>
        private static TissueLook BoldHoldfast() => new TissueLook(CellTypeIds.Anchoring,
            new Color(0.58f, 0.44f, 0.22f), 0.04f, 0.05f, 0.70f, 0.90f, 1.05f,
            0.32f, new Color(0.70f, 0.62f, 0.40f), 0.6f, 1.5f,
            new Color(0.55f, 0.45f, 0.22f), 0.02f,
            60f, 0.45f, 0.18f,
            0.45f,
            0.35f, 90f, 0f);

        /// <summary>
        /// A bold link: muscle pink-red, banded across its long axis, lighter, pinker and less saturated
        /// than the biter so the two are not one colour. The board holds no muscle; the red is the sea
        /// angel's and the Spanish dancer's (swimming-strokes-06, Clione; soft-skin-01, Hexabranchus)
        /// moved toward pink.
        /// </summary>
        private static TissueLook BoldMuscle() => new TissueLook(CellTypeIds.Link,
            new Color(0.94f, 0.42f, 0.50f), 0.15f, 0.35f, 0.85f, 0.32f, 0.62f,
            0.58f, new Color(1.00f, 0.66f, 0.70f), 0.8f, ShaderRimPower,
            new Color(0.95f, 0.45f, 0.45f), 0.05f,
            40f, 0.25f, BoldMuscleBanding,
            0.60f,
            0.3f, 150f, 0f);

        // noseskin:begin
        /// <summary>
        /// The bold nose (<c>EVOSIM_THEATRE_NOSE_PIT=2</c>, in the table only while the dial is at 2): the same pale pink-cream,
        /// sRGB (0.96, 0.88, 0.84), at a lightness of 0.72 and a saturation cap of 0.55, so it reads paler than the body it is
        /// on. A little more light through it than structure has (0.22 against 0.12), a mottle four times as fine as
        /// structure's, no papillae and no frill.
        /// </summary>
        private static TissueLook BoldNose() => new TissueLook(CellTypeIds.Nose,
            new Color(0.96f, 0.88f, 0.84f), 0.18f, 0.22f, 0.55f, 0.18f, 0.45f,
            0.72f, new Color(1.00f, 0.94f, 0.88f), 0.7f, 1.6f,
            new Color(1.00f, 0.70f, 0.58f), 0.07f,
            280f, 0.26f, 0f,
            0.55f,
            0f, 0f, 0f);
        // noseskin:end

        private static Dictionary<string, TissueLook> BuildBold()
        {
            var table = new Dictionary<string, TissueLook>();

            Add(table, Ramped(BoldLeaf(), RampLeaf));
            Add(table, Ramped(BoldAbsorber(), RampAbsorber));
            Add(table, Ramped(BoldMuscle(), RampLink));
            Add(table, Ramped(BoldBone(CellTypeIds.Structural), RampOther));
            Add(table, Ramped(BoldBiter(), RampBiter));
            Add(table, Ramped(BoldNerve(), RampOther));
            Add(table, Ramped(BoldFloat(), RampOther));
            Add(table, Ramped(BoldHoldfast(), RampOther));
            // noseskin:begin
            if (TheatreNosePit.OnSkin) Add(table, Ramped(BoldNose(), RampOther));
            // noseskin:end

            return table;
        }

        private static void Add(Dictionary<string, TissueLook> table, TissueLook look) =>
            table.Add(look.CellTypeId, look);
    }
}
