using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// What a creature looks like in the theatre — three colours for what it is made of, and a
    /// brightness for how much reserve it is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a view mode, not the creature's appearance.</b> DESIGN.md §5A.5 reserves part
    /// colour as an evolvable trait — camouflage, warning colouration, display — and painting
    /// bodies by cell type in the ordinary view would spend exactly the channel that trait needs.
    /// So it is a mode, it can be turned off, and it is never what a creature looks like.
    /// <c>PhenotypeBuilder.ApplyCellTypeColours</c> makes the same distinction for the sandbox.
    /// </para>
    /// <para>
    /// <b>Three colours, not seven.</b> The question a viewer asks of a world at a glance is who
    /// eats what: producers, stomachs, everything else. A seven-colour key is a legend to
    /// memorise, and the guilds it would distinguish (link, neural, buoyancy) are not the ones
    /// the ecology turns on.
    /// </para>
    /// <para>
    /// <b>The guild is carried on the rim, not on the face.</b> Since the skin landed
    /// (<see cref="TheatreSkin"/>) the body colour is a dark, desaturated version of the guild's
    /// hue and the guild's own colour goes to the shader's Fresnel rim, where a grazing edge
    /// catches it. That is the dark field arrangement the technique reading argues for, and it is
    /// also more legible: a small body against near black water is mostly edge.
    /// </para>
    /// <para>
    /// <b>Muted in HSL, not in RGB.</b> Species: ALRE found raw RGB colours drift garish and moved
    /// its creature colouring to HSL for that reason (research/theatre-look, [SP1] [SP2]). The
    /// caps here are the same idea: the hue is the guild's and is never touched, saturation and
    /// lightness are capped, so a guild stays recognisable without shouting.
    /// </para>
    /// <para>
    /// <b>Applied through one <see cref="MaterialPropertyBlock"/>.</b> Every part shares one
    /// material and therefore one draw-call batch; instancing a material per part would break
    /// batching for a debug view, which is the wrong trade at any population worth watching. The
    /// block is reused across calls, so painting allocates nothing after a body's first pass.
    /// A block drops those renderers out of the SRP Batcher, which is the documented cost of
    /// per-instance properties [U3]; that strategy was already this class's and it is kept.
    /// </para>
    /// <para>
    /// <b>Why the renderers are found by hierarchy and not asked for.</b>
    /// <c>CreatureInstance</c> carries a renderer-to-part table, and <c>Ecosystem</c> keeps every
    /// instance private — the theatre must not reach into that file. So each body's renderers are
    /// gathered once, from the root the id map paired it with, and the part each one belongs to is
    /// read from the name <c>PhenotypeBuilder</c> gives it (<c>Part07_n2</c>). Gathered once per
    /// body and cached: <c>GetComponentsInChildren</c> per creature per frame would be the most
    /// expensive thing in the viewer.
    /// </para>
    /// </remarks>
    public sealed class TheatrePalette
    {
        public Color Photosynthetic = new Color(0.34f, 0.72f, 0.36f);
        public Color Absorptive = new Color(0.90f, 0.55f, 0.22f);
        public Color Structural = new Color(0.72f, 0.74f, 0.78f);

        /// <summary>D247's fat cell: a pale yellow, off the guild wheel as the link's pink is.</summary>
        public Color Fat = new Color(0.96f, 0.90f, 0.55f);

        /// <summary>
        /// The colour of a link: a part whose cell is <c>link</c>, the tissue a joint moves.
        /// </summary>
        /// <remarks>
        /// Deliberately off the guild wheel. Green, orange and grey are what a body is made of;
        /// this is what a body can do, and reading the two off one scale would mean a jointed
        /// producer and a jointless stomach could not be told apart at a glance. Framsticks
        /// colours function the same way and keeps muscle on its own colour [FR]. It was the neck's
        /// colour first. From round 51, whose links catch no light (D127), it is the link's own
        /// skin, the pink-purple the owner chose, where a link had been drawn in the grey of
        /// structure; that grey is now also what a corpse is drawn in.
        /// </remarks>
        public Color Jointed = new Color(0.95f, 0.35f, 0.70f);

        /// <summary>
        /// Brightness of a body with nothing left, as a fraction of a sated one. Raised from
        /// 0.22 to 0.45 on 2026-09-16 (the look's one-day pass): at 0.22 a starving world fell
        /// off the bottom of the display range, so the reserve now also carries saturation
        /// (<see cref="StarvingSaturation"/>) and a starving body reads as pale, not as absent.
        /// </summary>
        public float Starving = 0.45f;

        /// <summary>
        /// Brightness of a corpse (D127) as a fraction of a sated body: dead tissue, drawn in one grey.
        /// A corpse is told from a starving body by having no colour, not by being darker: at 0.35 the
        /// corpses read as charcoal among the dim living; the owner tried 0.5 and chose 0.55
        /// (2026-09-28). <c>EVOSIM_THEATRE_CORPSE_BRIGHTNESS</c>.
        /// </summary>
        public float CorpseBrightness = TheatreSkin.Dial("EVOSIM_THEATRE_CORPSE_BRIGHTNESS", 0.55f, 0.05f, 1f);

        /// <summary>
        /// Brightness of remains (D220, <c>EVOSIM_THEATRE_REMAINS</c>) as a fraction of a sated body: dead tissue
        /// that still wears its own colours, dulled. Used only by a view told it draws remains.
        /// <c>EVOSIM_THEATRE_REMAINS_BRIGHTNESS</c>.
        /// </summary>
        public float RemainsBrightness = TheatreSkin.Dial("EVOSIM_THEATRE_REMAINS_BRIGHTNESS", 0.85f, 0.05f, 1f);

        /// <summary>
        /// One reserve tint for every body that is not a producer, in place of its own (D276: the normal view draws no hunger),
        /// 0 to 1; unset (NaN), each body is shaded by its reserve as before, which is now the hunger view.
        /// <c>EVOSIM_THEATRE_FIXED_TINT</c>.
        /// </summary>
        public float FixedTint = TheatreSkin.Dial("EVOSIM_THEATRE_FIXED_TINT", float.NaN, 0f, 1f);

        /// <summary>
        /// The fixed tint of a producer (a body with a photosynthetic part, as the pose stream's flag says), while
        /// <see cref="FixedTint"/> is set; unset (NaN), the same as <see cref="FixedTint"/>. A leaf's reserve sits near zero, so the
        /// look the leaves were tuned in is a producer's tint near zero, not a sated one. <c>EVOSIM_THEATRE_FIXED_TINT_PRODUCER</c>.
        /// </summary>
        public float FixedProducerTint = TheatreSkin.Dial("EVOSIM_THEATRE_FIXED_TINT_PRODUCER", float.NaN, 0f, 1f);

        private bool _fixedTintLogged;

        /// <summary>
        /// How far remains' own colours are taken toward their grey, 0 (the guild's colour) to 1 (a corpse's grey).
        /// A corpse is one flat grey; remains keep a trace of what the body was while it dies.
        /// <c>EVOSIM_THEATRE_REMAINS_GREY</c>.
        /// </summary>
        public float RemainsGreyness = TheatreSkin.Dial("EVOSIM_THEATRE_REMAINS_GREY", 0.3f, 0f, 1f);

        /// <summary>The light a corpse lets through its tissue and the sheen on it: dull and nearly opaque.</summary>
        private const float CorpseTransmission = 0.1f;

        private const float CorpseSheen = 0.02f;

        /// <summary>Saturation of a body with nothing left, as a fraction of a sated one's.</summary>
        public float StarvingSaturation = 0.35f;

        /// <summary>Saturation cap for the rim, in HSL. See the class remarks.</summary>
        public float RimSaturation = 0.62f;

        /// <summary>Lightness cap for the rim, in HSL.</summary>
        public float RimLightness = 0.58f;

        /// <summary>Saturation cap for the body under the rim.</summary>
        public float BodySaturation = 0.45f;

        /// <summary>
        /// Lightness of the body under the rim.
        /// </summary>
        /// <remarks>
        /// Dark, because the rim is what is meant to be seen, but not as dark as the first cut's
        /// 0.10. A Fresnel rim is nearly nothing on a surface facing the camera square on, and the
        /// top view looks straight down at the flat top face of every body in the world: at 0.10
        /// the whole footprint read as black boxes and the guild could not be told from the
        /// picture at all. Legibility before prettiness, which is the second of the two rules
        /// these pictures are made under.
        /// </remarks>
        public float BodyLightness = 0.18f;

        /// <summary>
        /// The skin, or null for the plain look.
        /// </summary>
        /// <remarks>
        /// Set by <c>TheatreRunner</c> before the first paint. When it is null every body keeps
        /// the material and the meshes <c>PhenotypeBuilder</c> gave it and only the colours are
        /// set, which is what this class did before the skin existed and is what the headless
        /// checks that never build a runner still get.
        /// </remarks>
        public TheatreSkin Skin;

        /// <summary>
        /// Draw every body as the physics has it: the engine's primitives, no rounding, no
        /// carve, taper or bend. The key X in the theatre (the owner's ask of 2026-09-13, built
        /// 2026-09-16): a viewer who doubts a picture can see the collider under it. Bodies are
        /// dressed again on the next paint after it changes (<see cref="Clear"/>).
        /// </summary>
        public bool RawShapes;

        private sealed class Body
        {
            public Transform Root;
            public MeshRenderer[] Renderers;
            public int[] Part;

            /// <summary>This body's carve seed, so no two bodies wear the same impressions.</summary>
            public float Seed;

            /// <summary>
            /// The key of this body's pattern when no genome was registered for it (<see cref="TheatrePattern.FallbackKey"/>),
            /// made on the first paint under the pattern dial and null otherwise.
            /// </summary>
            public TheatrePattern.Key? PatternFallback;

            /// <summary>
            /// This creature's own seed, from its id and not its plan. <see cref="Seed"/> comes from
            /// the body plan (the skin genes), so every blade of a clade that shares a plan would be
            /// drawn with one outline; this one perturbs a blade's outline, curl and tone a little,
            /// so siblings are similar and never identical (the owner asked on 2026-09-24 for leaves that
            /// are similar but not exactly the same).
            /// </summary>
            public float Own;

            /// <summary>
            /// Per renderer, the two joint anchors nearest it, in that visual's own object units,
            /// with a reach in w, or a zero reach for none. See <see cref="Pinches"/>.
            /// </summary>
            public Vector4[] PinchA;
            public Vector4[] PinchB;

            /// <summary>
            /// Per renderer, what the visual's local scale has to be multiplied by to draw the
            /// genome's three half-extents rather than the collider's one radius. See
            /// <see cref="Aspect"/>. Never above one on any axis, except the stretch along the
            /// shaft of a merged cap (<see cref="Merged"/>), which stays inside the capsule.
            /// </summary>
            public Vector3[] Shape;

            /// <summary>Per renderer, the local scale this class last wrote. See <see cref="Reshape"/>.</summary>
            public Vector3[] Wrote;

            /// <summary>
            /// Per renderer, true for the one cap that draws a near-spherical capsule alone, at
            /// the part's centre; its shaft and other cap are switched off. See <see cref="Dress"/>.
            /// </summary>
            public bool[] Merged;

            /// <summary>
            /// Per renderer, the part's own joint anchor in that visual's object units, w one when
            /// it hangs from a parent: a leaf's base is drawn at that end (<c>ShapeLamina</c>).
            /// </summary>
            public Vector4[] Leaf;

            /// <summary>
            /// Per renderer, the colour ramp's axis in that visual's object units, or zero for none; null
            /// unless the ramp dial is on. See <see cref="RampOf"/>.
            /// </summary>
            public Vector4[] Ramp;

            /// <summary>
            /// Per renderer, an absorber's osculum as the shader's <c>_VelvetOsculum</c>: its centre in that
            /// visual's object units in xyz and its radius in metres in w (zero for none); null unless the
            /// velvet dial is on. See <see cref="OsculumOf"/>.
            /// </summary>
            public Vector4[] Velvet;

            /// <summary>
            /// Per renderer, a capsule's link taper as the shader's <c>_LinkAxis</c> and <c>_LinkShape</c>, or zero
            /// for none; null unless the link dial is on. See <see cref="LinkOf"/>.
            /// </summary>
            public Vector4[] LinkAxis;
            public Vector4[] LinkShape;

            /// <summary>
            /// Per renderer, the <c>_SwayPart</c> last written and the flow code in its w (see <see cref="Flow"/>);
            /// null unless the bend with the current is on, and made on the first paint and the first flow under it.
            /// </summary>
            public Vector4[] SwayWas;
            public float[] FlowCode;

            /// <summary>
            /// The neck's pieces: two knuckles at every joint, and a bridge on each side whose
            /// drawn surface stands back from the anchor. Null when this body has no joint.
            /// </summary>
            public Transform[] Necks;

            /// <summary>
            /// True where the piece is a bridge (<see cref="Bridge"/>), false for a knuckle.
            /// Parallel to <see cref="Necks"/>.
            /// </summary>
            public bool[] NeckBridge;

            /// <summary>The part each neck's joint attaches. Parallel to <see cref="Necks"/>.</summary>
            public int[] NeckPart;

            /// <summary>
            /// True where the knuckle sits in the child part's frame, false in the parent's.
            /// Parallel to <see cref="Necks"/>.
            /// </summary>
            public bool[] NeckChildSide;
            // THALLI begin

            /// <summary>
            /// Per renderer, the thallus it draws (<see cref="TheatreMeshes.ThallusForm"/>) plus one, or zero for none; null
            /// until the thalli dial dresses the body (<see cref="DressThallus"/>).
            /// </summary>
            public byte[] Thallus;
            // THALLI end
        }

        private readonly Dictionary<long, Body> _bodies = new Dictionary<long, Body>();
        /// <summary>
        /// Made on first paint, never in a field initializer — see the remarks below.
        /// </summary>
        /// <remarks>
        /// A <see cref="MaterialPropertyBlock"/> is an engine object, and Unity refuses to create
        /// one while it is deserializing a MonoBehaviour, which is exactly when that
        /// MonoBehaviour's field initializers run. <c>TheatreRunner</c> holds its palette as
        /// <c>private readonly TheatrePalette _palette = new TheatrePalette()</c>, so building the
        /// block here threw <c>CreateImpl is not allowed…</c> before <c>Start</c> ran, left
        /// <c>_palette</c> null, and killed the viewer on Play with a bare
        /// <c>NullReferenceException</c> two frames later. The headless checks never saw it: they
        /// build the world directly and never instantiate the component, so the interactive path
        /// went unexercised from D075 until someone first pressed Play (2026-09-10).
        /// </remarks>
        private MaterialPropertyBlock _block;
        private readonly List<MeshRenderer> _scratch = new List<MeshRenderer>();
        private readonly List<Transform> _necks = new List<Transform>();
        private readonly List<int> _neckPart = new List<int>();
        private readonly List<bool> _neckSide = new List<bool>();
        private readonly List<bool> _neckBridge = new List<bool>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int ReserveId = Shader.PropertyToID("_Reserve");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        /// <summary>
        /// Each body material and the pair a fading body draws with in its place, and each
        /// see-through copy's opaque original.
        /// </summary>
        private readonly Dictionary<Material, Material[]> _fadingSetOf = new Dictionary<Material, Material[]>();
        private readonly Dictionary<Material, Material> _opaqueOf = new Dictionary<Material, Material>();
        private static readonly int TransTintId = Shader.PropertyToID("_TransTint");
        private static readonly int TransGainId = Shader.PropertyToID("_TransGain");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int CarveId = Shader.PropertyToID("_Carve");
        private static readonly int IndividualId = Shader.PropertyToID("_Individual");
        private static readonly int PinchAId = Shader.PropertyToID("_PinchA");
        private static readonly int PinchBId = Shader.PropertyToID("_PinchB");
        private static readonly int LeafId = Shader.PropertyToID("_Leaf");
        private static readonly int TissueLookId = Shader.PropertyToID("_TissueLook");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int TransMetresId = Shader.PropertyToID("_TransMetres");
        private static readonly int MottleCellsId = Shader.PropertyToID("_MottleCellsPerMetre");
        private static readonly int MottleStrengthId = Shader.PropertyToID("_MottleStrength");
        private static readonly int BandingId = Shader.PropertyToID("_Banding");
        private static readonly int PapillaeId = Shader.PropertyToID("_Papillae");
        private static readonly int PapillaeCellsId = Shader.PropertyToID("_PapillaeCellsPerMetre");
        private static readonly int FrillId = Shader.PropertyToID("_Frill");
        private static readonly int SssId = Shader.PropertyToID("_Sss");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private static readonly int SoftHullId = Shader.PropertyToID("_SoftHull");
        private static readonly int OrganicId = Shader.PropertyToID("_Organic");
        private static readonly int OrganicTissueId = Shader.PropertyToID("_OrganicTissue");
        private static readonly int BoardLightId = Shader.PropertyToID("_BoardLight");
        private static readonly int SwayId = Shader.PropertyToID("_Sway");
        private static readonly int SwayPartId = Shader.PropertyToID("_SwayPart");
        private static readonly int CountershadeId = Shader.PropertyToID("_Countershade");
        private static readonly int RampAxisId = Shader.PropertyToID("_RampAxis");
        private static readonly int RampTipId = Shader.PropertyToID("_RampTip");
        private static readonly int LeafLookId = Shader.PropertyToID("_LeafLook");
        private static readonly int AlgaId = Shader.PropertyToID("_Alga");
        private static readonly int AlgaDeepId = Shader.PropertyToID("_AlgaDeep");
        private static readonly int AlgaShallowId = Shader.PropertyToID("_AlgaShallow");
        private static readonly int VelvetId = Shader.PropertyToID("_Velvet");
        private static readonly int VelvetOsculumId = Shader.PropertyToID("_VelvetOsculum");
        private static readonly int LinkAxisId = Shader.PropertyToID("_LinkAxis");
        private static readonly int LinkShapeId = Shader.PropertyToID("_LinkShape");
        private static readonly int RimBandId = Shader.PropertyToID("_RimBand");
        private static readonly int ChromatophoresId = Shader.PropertyToID("_Chromatophores");
        private static readonly int IridescenceId = Shader.PropertyToID("_Iridescence");
        private static readonly int MottleSoftId = Shader.PropertyToID("_MottleSoft");
        private static readonly int CellViewId = Shader.PropertyToID("_CellView");
        private static readonly int CellColourId = Shader.PropertyToID("_CellColour");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int PatternShiftId = Shader.PropertyToID("_PatternShift");
        private static readonly int Skin2PartId = Shader.PropertyToID("_Skin2Part"); // skin2parts
        private static readonly int PatternTexId = Shader.PropertyToID("_PatternTex");

        /// <summary>
        /// Light through tissue and glow (<c>EVOSIM_THEATRE_SSS</c>, 0 off by default), read once: the
        /// shader tints the light through a thin part more strongly by its tissue, makes the reserve's
        /// glow come from inside, and softens the sheen's glints (<c>TheatreBody.shader</c>, <c>_Sss</c>).
        /// Off, the palette never sets <c>_Sss</c> and the material's zero stands.
        /// </summary>
        private readonly bool _sss = TheatreSkin.Dial("EVOSIM_THEATRE_SSS", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The soft dial (<c>EVOSIM_THEATRE_SOFT</c>, 0 to 2, 0 off by default), read once: over 0 the soft
        /// skin, at 2 the soft skin drawn as one smooth hull as well.
        /// </summary>
        private static readonly float SoftDial = TheatreSkin.Dial("EVOSIM_THEATRE_SOFT", 0f, 0f, 2f);

        /// <summary>
        /// Soft silhouettes (<c>EVOSIM_THEATRE_SOFT</c> over 0), read once: the shader cuts a
        /// lower carve without terraces, draws a coarser and gentler mottle, turns the carved normal back
        /// toward the uncarved one (most near a joint, where two parts meet) and softens the rim's
        /// fall-off (<c>TheatreBody.shader</c>, <c>_Soft</c>). It scales the tissue look's own rim and
        /// mottle when that dial is on, and the material's when it is off. Off, the palette never sets
        /// <c>_Soft</c> and the material's zero stands.
        /// </summary>
        private readonly bool _soft = SoftDial > 0f;

        /// <summary>
        /// One smooth hull (<c>EVOSIM_THEATRE_SOFT=2</c>), over the soft skin, which it needs: the carve all but
        /// gone, a faint lobe kept, the normal turned most of the way back to the uncarved one and nearly all
        /// of it at a joint, and a fine faint mottle (<c>TheatreBody.shader</c>, <c>_SoftHull</c>). Below 2 the
        /// palette never sets <c>_SoftHull</c> and the material's zero stands, so the soft dial at 1 draws
        /// what it drew when it ran from 0 to 1.
        /// </summary>
        private readonly bool _softHull = SoftDial >= 2f;

        /// <summary>
        /// The organic dial (<c>EVOSIM_THEATRE_ORGANIC</c>, 0 to 2, 0 off by default), read once: over 0 the
        /// organic silhouette, at 2 the soft tissue laid over it.
        /// </summary>
        private static readonly float OrganicDial = TheatreSkin.Dial("EVOSIM_THEATRE_ORGANIC", 0f, 0f, 2f);

        /// <summary>
        /// The organic silhouette (<c>EVOSIM_THEATRE_ORGANIC</c> over 0), read once: a box part's
        /// section is rounded toward a superellipse, its ends close like a seed's, it swells a little off its
        /// middle and leans slightly, each from the part's own number, so a slab reads as a pebble or a
        /// flatworm's body (<c>TheatreBody.shader</c>, <c>_Organic</c>, <c>OrganicFactor</c>). Every term only
        /// shrinks the section, so the drawn box stays inside its collider. Spheres, capsules and leaves are
        /// left alone. Off, the palette never sets <c>_Organic</c> and the material's zero stands.
        /// </summary>
        private readonly bool _organic = OrganicDial > 0f;

        /// <summary>
        /// The organic soft tissue (<c>EVOSIM_THEATRE_ORGANIC=2</c>), over the organic silhouette, which it
        /// needs: a box part's carve without terraces and shallower, wrinkles that run along its long axis, fine
        /// pores and a broad soft sheen in the shading, and a thin ruffled margin on a flattened part
        /// (<c>TheatreBody.shader</c>, <c>_OrganicTissue</c>, <c>TissueCarve</c>, <c>TissueMargin</c>). Every
        /// term cuts inward, shades or thins inward, so the drawn box stays inside its collider (margin outside
        /// it: 0). Below 2 the palette never sets <c>_OrganicTissue</c> and the material's zero stands, so the
        /// organic dial at 1 draws what it drew when it ran from 0 to 1.
        /// </summary>
        private readonly bool _organicTissue = OrganicDial >= 2f;

        /// <summary>
        /// How much more lobed an absorber's carve is under the organic dial: the factor on its tissue's lobe
        /// gain (<c>_Carve.y</c>), so a stomach reads as a fleshy, lobed sac and not as a carved block. The gain
        /// is a weight in the carve's mix of lobes and wrinkles (<c>EvoCarve</c>, <c>SoftCarve</c>), so raising
        /// it gives the lobes more of the field and leaves the depth to <c>_CarveFraction</c>, the guild's take
        /// and <c>_CarveMaximum</c>, none of which it touches: the carve still only cuts inward, as deep as it
        /// did, and the part stays inside its collider. Applied only while <c>EVOSIM_THEATRE_ORGANIC</c> is on.
        /// </summary>
        private const float OrganicAbsorberLobes = 1.6f;

        /// <summary>
        /// Blended joints (<c>EVOSIM_THEATRE_BLEND</c>, 0 off by default), read once: where a box child joins
        /// a box parent, the parent side's knuckle is drawn as a flare, a smooth ring round the child's base
        /// that rises a little up its side, so a limb reads as growing out of the body (<see cref="Flare"/>).
        /// The ring is the one thing outside both colliders, by at most the blend radius. Off, every knuckle
        /// is sized as before and no transform differs.
        /// </summary>
        private readonly bool _blend = TheatreSkin.Dial("EVOSIM_THEATRE_BLEND", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// Countershading (<c>EVOSIM_THEATRE_COUNTERSHADE</c>, 0 off by default), read once: the body's own colour
        /// is darker where the surface faces up and paler where it faces down, as a swimming animal's is
        /// (<c>TheatreBody.shader</c>, <c>_Countershade</c>, <c>CountershadeTop</c>). It follows the world's up,
        /// so a body that rolls keeps its dark side up. Colour only: the sheen, the rim and the light through
        /// the tissue are left as they are. Off, the palette never sets <c>_Countershade</c> and the
        /// material's zero stands.
        /// </summary>
        private readonly bool _countershade = TheatreSkin.Dial("EVOSIM_THEATRE_COUNTERSHADE", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// A colour ramp along each part (<c>EVOSIM_THEATRE_COLOUR_RAMP</c>, 0 off by default), read once: the
        /// body's colour changes smoothly from the end that joins the parent (the base) to the free end (the tip),
        /// by the tissue's own <see cref="TissueLook.RampTip"/> (<c>TheatreBody.shader</c>, <c>_RampAxis</c>,
        /// <c>_RampTip</c>). Colour only. Off, the palette sets neither property, builds no axis and the
        /// material's defaults stand.
        /// </summary>
        private readonly bool _ramp = TheatreSkin.Dial("EVOSIM_THEATRE_COLOUR_RAMP", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// Living leaves (<c>EVOSIM_THEATRE_LEAF</c>, 0 off by default), read once: a blade's carve is a smooth,
        /// shallow relief with no facets, a blade glows with its own warm colour when the key is behind it, and
        /// its margin ruffles out of plane by no more than the room the box leaves (<c>TheatreBody.shader</c>,
        /// <c>_LeafLook</c>, <c>LeafCarve</c>, <c>LeafRuffle*</c>, <c>LeafBacklit*</c>). It acts on a blade (the
        /// lamina path) alone, whatever sets it. Off, the palette never sets <c>_LeafLook</c> and the material's
        /// zero stands. The dial has two levels: 1 is the living leaf above, and 2 adds the margin by growth (a
        /// second, finer ruffle toward the rim, inside level 1's room) and kelp relief (rows of blisters in the
        /// normal on a long blade). A value above 0 and under 1.5 is level 1, 1.5 and over level 2; the shader is
        /// told the level (<see cref="LeafLevel"/>), and tests above 1.5 for level 2.
        /// </summary>
        private readonly bool _leafLook = LeafLevel > 0f;

        /// <summary>The leaf dial's level for the shader: 0 off, 1 or 2 (see <see cref="_leafLook"/>).</summary>
        private static readonly float LeafLevel = LevelOf(TheatreSkin.Dial("EVOSIM_THEATRE_LEAF", 0f, 0f, 2f));

        /// <summary>
        /// A level dial's level: 0 for 0 or under, 2 from 1.5 up, and 1 between, so the dial's old on value (any
        /// positive number, a 1 in practice) is still level 1.
        /// </summary>
        private static float LevelOf(float dial) => dial <= 0f ? 0f : dial >= 1.5f ? 2f : 1f;

        /// <summary>
        /// Marine producers (<c>EVOSIM_THEATRE_ALGA</c>, 0 off by default), read once: a photosynthetic blade is drawn as
        /// a seaweed or a seagrass, a strap or a sheet by its own length over width, with no lobes and no veins, a wet
        /// sheen and more light through a sheet (<c>TheatreBody.shader</c>, <c>_Alga</c>, <c>AlgaShapeOf</c>,
        /// <c>Alga*</c>), and a producer's colour comes from a family of green, olive-brown, golden-brown and red by its
        /// proportions and its depth (<see cref="AlgaDressOf"/>). A stalk under a blade draws darker and a float next to
        /// one as an olive gas bladder. Works with <c>EVOSIM_THEATRE_LEAF</c> on or off. Off, the palette never sets
        /// <c>_Alga</c>, <c>_AlgaDeep</c> or <c>_AlgaShallow</c>, and no colour, brightness or sheen differs.
        /// </summary>
        private readonly bool _alga = TheatreSkin.Dial("EVOSIM_THEATRE_ALGA", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The producers' colour family, four anchors in sRGB, laid along one line from the shallows' green to the
        /// deep's red (<see cref="AlgaFamily"/>: green at 0, olive-brown at a third, golden-brown at two thirds, red at
        /// one). Each is a board colour less saturated than today's grass green (the guild's 0.34, 0.72, 0.36, hue 123°,
        /// HSV saturation 0.53), so through <see cref="Muted"/> a producer reads olive rather than lawn.
        /// </summary>
        /// <remarks>
        /// The green: the guild's own green turned 20° toward yellow and its saturation lowered to 0.45, the sea lettuce
        /// and seagrass of the board (algae-fronds-03 and -04, Ulva), hue 103°.
        /// </remarks>
        private static readonly Color AlgaGreen = new Color(0.42f, 0.62f, 0.34f);

        /// <summary>
        /// Olive-brown: a wrack's or a kelp blade's in shade (algae-fronds-08, Laminaria; the holdfast look's sampled
        /// stipes, 0.48, 0.42, 0.24, and Macrocystis about 0.31, 0.31, 0.26), hue 55°, saturation 0.49.
        /// </summary>
        private static readonly Color AlgaOlive = new Color(0.47f, 0.45f, 0.24f);

        /// <summary>
        /// Golden-brown: a giant kelp's or a Sargassum's blade in light (algae-fronds-01, Macrocystis), hue 36°,
        /// saturation 0.63, the warmest and the most saturated of the four, which the body's cap holds to 0.45.
        /// </summary>
        private static readonly Color AlgaGolden = new Color(0.60f, 0.45f, 0.22f);

        /// <summary>
        /// Red-brown: the red algae of deeper water, a dull maroon rather than an animal's red, hue 4°, saturation 0.56.
        /// Reached by a producer only far down the family (a strap, fleshy, deep).
        /// </summary>
        private static readonly Color AlgaRed = new Color(0.50f, 0.24f, 0.22f);

        /// <summary>
        /// The HSV value of the producers' guild colour the anchors were set against (0.72): a guild colour of another
        /// value (a tissue look's) scales the family's value by its own over this, so the guild stays the anchor of the
        /// family's brightness.
        /// </summary>
        private const float AlgaGuildValue = 0.72f;

        /// <summary>
        /// Where a producer sits along the family before its depth: <see cref="AlgaPositionSheet"/> for a sheet, rising
        /// to <see cref="AlgaPositionStrap"/> for a strap by the form, plus up to <see cref="AlgaPositionFlesh"/> as the
        /// blade's thickness over its width rises from <see cref="AlgaFleshLow"/> to <see cref="AlgaFleshHigh"/>. So a
        /// thin sheet is green, a thin strap olive to golden and a fleshy strap golden-brown, all smooth in the half
        /// extents. Inference: a look, after the board's Ulva sheets and kelp straps, not a taxonomy.
        /// </summary>
        private const float AlgaPositionSheet = 0.1f;

        private const float AlgaPositionStrap = 0.5f;

        private const float AlgaPositionFlesh = 0.15f;

        private const float AlgaFleshLow = 0.5f;

        private const float AlgaFleshHigh = 1f;

        /// <summary>
        /// The depth's tint on a producer's colour, through the shader's <c>_AlgaShallow</c> and <c>_AlgaDeep</c>
        /// (<c>AlgaDepthTint</c>): one factor on all three channels, so the hue and the saturation stay the family's and
        /// the depth only darkens. <see cref="AlgaShallowShade"/> at the waterline, one at half depth,
        /// <see cref="AlgaDeepShade"/> at the floor. Less light reaches a blade deeper; that the family also shifts
        /// toward red there is debated (chromatic adaptation), and a per channel multiplier that moved the colour toward
        /// cyan in the shallows is what made the first cut read as mint and milky.
        /// </summary>
        private const float AlgaDeepShade = 0.6f;

        private const float AlgaShallowShade = 1f;

        /// <summary>
        /// The round form (a blade whose length over width is under <see cref="AlgaRoundLow"/>, gone by
        /// <see cref="AlgaRoundHigh"/>: the shader's <c>AlgaRoundLow</c> and <c>AlgaRoundHigh</c>, which must move
        /// together) is a fan, Padina's (algae-fronds, producers 10 and 11): ochre to olive, at this place along the family
        /// (between the olive anchor and the golden one), and not the green of a sheet.
        /// </summary>
        private const float AlgaRoundLow = 1.2f;

        private const float AlgaRoundHigh = 1.5f;

        private const float AlgaPositionRound = 0.42f;

        /// <summary>
        /// A producer's rim and light-through colours, on its guild colour's hue: the guild's HSV saturation times
        /// <see cref="AlgaRimSaturationGain"/> (at most <see cref="AlgaRimSaturationCap"/>) and the value of the muted
        /// colour times <see cref="AlgaRimValue"/>. The muted rim and light-through sit at a fixed HSL lightness of
        /// 0.58 and 0.6 over a saturation the family's pale anchors cannot fill, which is a pastel.
        /// </summary>
        private const float AlgaRimSaturationGain = 1.25f;

        private const float AlgaRimSaturationCap = 0.7f;

        private const float AlgaRimValue = 0.92f;

        /// <summary>
        /// A free-drifting blade (a blade of a body with no anchoring part) is drawn as a Sargassum-like frond
        /// (<c>TheatreBody.shader</c>, <c>AlgaFrondOf</c>), flagged by <c>_AlgaShallow.w</c> one (<see cref="AlgaDress.Shallow"/>),
        /// and coloured golden-brown to olive: its place along the family is <see cref="AlgaFrondPositionLow"/> to
        /// <see cref="AlgaFrondPositionHigh"/> by the blade's own place (<see cref="AlgaPositionOf"/>), and its body at HSL
        /// saturation <see cref="AlgaFrondSaturation"/> and lightness <see cref="AlgaFrondLightness"/>, where a sheet's is the
        /// family's pale 0.3 at the tissue's 0.42. Its guild colour (which the rim and the light through take their hue and
        /// richness from, <see cref="AlgaRich"/>) is at HSV saturation <see cref="AlgaFrondGuildSaturation"/> and value
        /// <see cref="AlgaFrondGuildValue"/>, so the body's cap is the one that binds. Inference: a look after the board's
        /// Sargassum (producers 12 and 13), not a measurement.
        /// </summary>
        private const float AlgaFrondPositionLow = 0.45f;

        private const float AlgaFrondPositionHigh = 0.6f;

        private const float AlgaFrondSaturation = 0.55f;

        private const float AlgaFrondLightness = 0.32f;

        private const float AlgaFrondGuildSaturation = 0.85f;

        private const float AlgaFrondGuildValue = 0.62f;

        /// <summary>
        /// A producer corpse's <c>_Alga</c>: the form kept (so a blade does not change shape when it dies) with this
        /// share of the wet sheen and the extra light through, since a corpse is dull and nearly opaque. In (0, 1].
        /// </summary>
        private const float AlgaCorpse = 0.25f;

        /// <summary>The brightness a stipe keeps, as a share of its body's: a kelp's stalk is darker than its blade.</summary>
        private const float AlgaStipeShade = 0.7f;

        /// <summary>How many parts a stipe's chain runs down to its blade, at most.</summary>
        private const int AlgaStipeChain = 4;

        /// <summary>Where a gas bladder sits along the family: the olive anchor.</summary>
        private const float AlgaBladderPosition = 1f / 3f;

        /// <summary>The sheen a gas bladder takes at least: glossy, against a float tissue's 0.08 to 0.25.</summary>
        private const float AlgaBladderSheen = 0.45f;

        /// <summary>
        /// The form's thresholds on length over width, the shader's <c>AlgaSheetLow</c>, <c>AlgaSheetHigh</c>,
        /// <c>AlgaStrapLow</c> and <c>AlgaStrapHigh</c> (<c>AlgaFormOf</c>); the two must move together.
        /// </summary>
        private const float AlgaSheetLow = 1.5f;

        private const float AlgaSheetHigh = 2.1f;

        private const float AlgaStrapLow = 2.5f;

        private const float AlgaStrapHigh = 3.5f;

        /// <summary>
        /// Velvet absorbers (<c>EVOSIM_THEATRE_VELVET</c>, 0 off by default), read once: an absorptive part gets
        /// a matte skin with a soft bright rim, a regular pattern of pores and one osculum at the far end of its
        /// long axis from its joint (<c>TheatreBody.shader</c>, <c>_Velvet</c>, <c>_VelvetOsculum</c>,
        /// <c>Velvet*</c>). Shading only, so nothing leaves the collider. Off, the palette never sets either
        /// property and the material's zeros stand. Level 2 (1.5 and over) replaces the pores with star systems, as a
        /// colonial tunicate has them (<c>VelvetStars</c>); the shader is told the level (<see cref="VelvetLevel"/>).
        /// </summary>
        private readonly bool _velvet = VelvetLevel > 0f;

        /// <summary>The velvet dial's level for the shader: 0 off, 1 or 2 (see <see cref="LevelOf"/>).</summary>
        private static readonly float VelvetLevel = LevelOf(TheatreSkin.Dial("EVOSIM_THEATRE_VELVET", 0f, 0f, 2f));

        /// <summary>
        /// An osculum's radius as a share of its part's smallest half extent (<c>_VelvetOsculum.w</c>, in
        /// metres): a hole well inside the part's end, whose lip stays under 1.7 radii, so on a box it stays on
        /// the end face. In (0, 0.6).
        /// </summary>
        private const float VelvetOsculumShare = 0.35f;

        /// <summary>
        /// Tapered, fringed links (<c>EVOSIM_THEATRE_LINK</c>, 0 off by default), read once: a capsule part narrows
        /// from its base (the end that joins its parent) to its free end, and carries repeated segments in shading,
        /// each with a groove, a pale lobe and fine bristles at the silhouette, as a worm's body or an arm does
        /// (<c>TheatreBody.shader</c>, <c>_LinkAxis</c>, <c>_LinkShape</c>, <c>LinkTaper</c>, <c>Link*</c>). The
        /// taper only shrinks the cross-section toward the axis, so the drawn link stays inside its collider
        /// (margin outside it: 0); the fringe is shading alone. A capsule drawn as one ball (<see cref="NearlyASphere"/>)
        /// is not a link and is left alone. Off, the palette never sets either property and the material's zeros
        /// stand, and the bridges (<see cref="Bridge"/>) are as they were.
        /// </summary>
        private readonly bool _link = TheatreSkin.Dial("EVOSIM_THEATRE_LINK", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// A coloured margin on consumer bodies (<c>EVOSIM_THEATRE_RIM_BAND</c>, 0 off by default), read once: a
        /// consumer part's box is drawn with a pale line inside a warm band round the outline of its broad faces,
        /// as a sea slug's mantle or a flatworm's rim is (<c>TheatreBody.shader</c>, <c>_RimBand</c>,
        /// <c>RimBand*</c>). Colour only, the same on every consumer part. A greyed body (out of focus, or a corpse)
        /// keeps its grey. Off, the palette never sets <c>_RimBand</c> and the material's zero stands.
        /// </summary>
        private readonly bool _rimBand = TheatreSkin.Dial("EVOSIM_THEATRE_RIM_BAND", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// Chromatophore mottling (<c>EVOSIM_THEATRE_CHROMATOPHORES</c>, 0 off by default), read once: a field of small
        /// pigment discs that breathe on their own phases, with a slow band that opens the ones it passes, as an
        /// octopus's or a cuttlefish's skin has (<c>TheatreBody.shader</c>, <c>_Chromatophores</c>,
        /// <c>Chromatophores</c>, <c>Chroma*</c>). Colour only. The shader property carries the tissue's strength
        /// (<see cref="ChromatophoresOf"/>), not a toggle. A greyed body (out of focus, or a corpse) and a neck keep
        /// their plain colour. Off, the palette never sets <c>_Chromatophores</c> and the material's zero stands.
        /// </summary>
        private readonly bool _chromatophores = TheatreSkin.Dial("EVOSIM_THEATRE_CHROMATOPHORES", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The chromatophore strength of an absorber, as a share of a biter's or a link's (one). Absorbers are the
        /// sponge-like feeders, so they take a fainter speckle. In [0, 1].
        /// </summary>
        private const float ChromatophoresAbsorber = 0.4f;

        /// <summary>
        /// The chromatophore strength of a structural part, as a share of a biter's or a link's (one). In [0, 1].
        /// </summary>
        private const float ChromatophoresStructural = 0.4f;

        /// <summary>
        /// Thin-film iridescence on wet tissue (<c>EVOSIM_THEATRE_IRIDESCENCE</c>, 0 off by default), read once: the sheen
        /// (the specular highlight off the key) takes the colour of a thin film's interference, which shifts with the
        /// view and drifts across the skin, as an octopus's or a sea slug's wet skin shows (<c>TheatreBody.shader</c>,
        /// <c>_Iridescence</c>, <c>Iridescent*</c>). Colour of the sheen only: its strength, its shape, the body, the rim
        /// and the light through the tissue stay as they were. The shader property carries the tissue's strength
        /// (<see cref="IridescenceOf"/>), not a toggle. A greyed body (out of focus, or a corpse) and a neck keep
        /// their white sheen. Off, the palette never sets <c>_Iridescence</c> and the material's zero stands.
        /// </summary>
        private readonly bool _iridescence = TheatreSkin.Dial("EVOSIM_THEATRE_IRIDESCENCE", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The soft mottle (<c>EVOSIM_THEATRE_MOTTLE_SOFT</c>, off by default): the mottle's tone blended across
        /// neighbouring cells and no cell walls (<c>TheatreBody.shader</c>, <c>_MottleSoft</c>, <c>MottleSoft</c>), so
        /// a strong mottle reads as tissue and not as a mosaic of tiles. One for every body. Off, the palette never
        /// sets <c>_MottleSoft</c> and the material's zero stands.
        /// </summary>
        private readonly bool _mottleSoft = TheatreSkin.Dial("EVOSIM_THEATRE_MOTTLE_SOFT", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The cell view (<c>EVOSIM_THEATRE_CELL_VIEW</c>, off by default), a diagnostic: every part drawn in one flat
        /// colour for its cell type (<see cref="CellViewColourOf"/>), nearly unlit, with none of the tissue's looks
        /// (<c>TheatreBody.shader</c>, <c>_CellView</c>, <c>_CellColour</c>). A greyed body and a corpse take their
        /// type's colour too, and a neck the colour of the part it hangs from. Off, the palette never sets
        /// <c>_CellView</c> or <c>_CellColour</c> and the material's defaults stand. Any value above zero is the one
        /// palette (a 2 from a script written while there were two is read the same).
        /// </summary>
        public static readonly bool CellView = TheatreSkin.Dial("EVOSIM_THEATRE_CELL_VIEW", 0f, 0f, 2f) > 0f;

        /// <summary>The cell view's colour for a cell type: Okabe and Ito's palette, light grey for the structural type
        /// and their black for the anchoring type, the consumer in the set's reddish purple, apart from the absorptive
        /// orange, and the neural type near white (the owner's choice, 2026-10-25).</summary>
        /// <remarks>
        /// sRGB values; a <see cref="Color"/> set on a property block is converted to linear by Unity when the
        /// project is in linear colour space, as the other colours here are. Public with <see cref="CellViewLegend"/>,
        /// which draws the same table. Anchoring takes the set's black rather than its blue, which sat beside the
        /// buoyancy's sky blue (the closest pair of the set for normal vision).
        /// </remarks>
        public static Color CellViewColourOf(string cellTypeId)
        {
            switch (cellTypeId)
            {
                case CellTypeIds.Link: return CellViewRgb(0xF0, 0xE4, 0x42);
                case CellTypeIds.Neural: return CellViewRgb(0xF2, 0xF2, 0xF2);
                case CellTypeIds.Photosynthetic: return CellViewRgb(0x00, 0x9E, 0x73);
                case CellTypeIds.Absorptive: return CellViewRgb(0xE6, 0x9F, 0x00);
                case CellTypeIds.Consumer: return CellViewRgb(0xCC, 0x79, 0xA7);
                case CellTypeIds.Buoyancy: return CellViewRgb(0x56, 0xB4, 0xE9);
                case CellTypeIds.Anchoring: return CellViewRgb(0x00, 0x00, 0x00);
                default: return CellViewRgb(0xBB, 0xBB, 0xBB);
            }
        }

        private static Color CellViewRgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        /// <summary>
        /// The iridescence strength of the translucent float type (buoyancy), as a share of a biter's or a link's (one):
        /// a comb jelly's and a medusa's tissue is thin and wet, but the share is held to half. In [0, 1].
        /// </summary>
        private const float IridescenceFloat = 0.5f;

        /// <summary>
        /// The iridescence strength of an absorber and of a structural part, as a share of a biter's or a link's (one):
        /// a sponge's or a strut's skin is drier and thicker, so the shimmer is faint. In [0, 1].
        /// </summary>
        private const float IridescenceDry = 0.2f;

        /// <summary>
        /// Reaction-diffusion skin patterns (<c>EVOSIM_THEATRE_PATTERN</c>, 0 off by default), read once by
        /// <see cref="TheatrePattern"/>: a Gray-Scott field, one tile of a library of at most 36 and placed by the pattern key, mapped onto every part as
        /// a darker, deeper pigment (<c>TheatreBody.shader</c>, <c>_Pattern</c>, <c>_PatternTex</c>, <c>PatternSkin</c>,
        /// <c>Pattern*</c>). Colour only. A greyed body (out of focus, or a corpse) and a neck keep their plain colour.
        /// Off, the palette never sets either property, and nothing is solved or created.
        /// </summary>
        private readonly bool _pattern = TheatrePattern.Enabled;

        /// <summary>
        /// The pattern tile's repeat length as a share of the creature's longest part (its full length), before the
        /// regime's own multiple (<see cref="TheatrePattern.Regime"/>). The same for every part of a body, so the
        /// pattern runs across it at one scale. In (0, 2].
        /// </summary>
        private const float PatternTileShare = 0.25f;

        /// <summary>The tile's shortest length in metres, for a very small body. Under this the pattern cells are sub-pixel anyway.</summary>
        private const float PatternTileMinimum = 0.02f;

        /// <summary>
        /// The pattern's strength on an absorber and on a structural part, as a share of every other tissue's (one).
        /// A leaf takes none. In [0, 1].
        /// </summary>
        private const float PatternSmaller = 0.5f;

        /// <summary>
        /// A link's radius at its free end as a share of its radius at the base, where the shaft meets the tip
        /// cap and across that cap (<c>_LinkShape.y</c>). In (0, 1]; one is no taper. The shaft falls to it by a
        /// smoothstep, so neither seam is a crease, and the tip cap is drawn at it, a half-ellipsoid as long as the
        /// hemisphere it replaces and narrower, so the free pole stays on the collider's. Only shrinking, so the margin is 0.
        /// </summary>
        private const float LinkTipShare = 0.6f;

        /// <summary>
        /// The share of a part's half length its parent anchor must sit out along the long axis for the ramp's
        /// base to be that end (<see cref="RampOf"/>). Under it the anchor is on the part's side or its middle
        /// and does not say which end is the base, so a fixed end from the part's seed is used. In (0, 1).
        /// </summary>
        private const float RampAnchorShare = 0.5f;

        /// <summary>
        /// Mixes of the part's seed for the fixed end of a part with no anchor on its long axis, apart from the
        /// shader's uses of the same seed (the carve's lean uses 13.37 and 7.919), so the choice is its own.
        /// </summary>
        private const float RampEndMix = 17.713f;

        private const float RampEndOffset = 0.347f;

        /// <summary>The relative tolerance within which two half extents count as equal, the shader's <c>AxisTieTolerance</c>.</summary>
        private const float AxisTie = 1e-3f;

        /// <summary>
        /// The board's light on the bodies (<c>EVOSIM_THEATRE_BOARD_LIGHT</c>, 0 off by default), read once: a
        /// measurement and not a look. The bodies are lit as the reference board's photographs are, by a white
        /// key and a neutral fill and ambient, with no light lost to depth, no caustics, none of the water's
        /// cuts to the rim, the glow and the light through, and no fog (<c>TheatreBody.shader</c>,
        /// <c>_BoardLight</c>). Everything else in the frame keeps the water, and the grade still runs over the
        /// whole frame, so a pair of frames with the dial off and on shows what the water takes from a body's
        /// colour and what is the tissue's own. Off, the palette never sets <c>_BoardLight</c>.
        /// </summary>
        private readonly bool _boardLight = TheatreSkin.Dial("EVOSIM_THEATRE_BOARD_LIGHT", 0f, 0f, 1f) >= 0.5f;

        /// <summary>Whether the log has said once that the board's light is on.</summary>
        private static bool _boardLightSaid;

        /// <summary>
        /// Sway and pulse at rest (<c>EVOSIM_THEATRE_SWAY</c>, 0 off by default), read once: a leaf
        /// bends gently about its anchor and a float draws in along its smallest axis, slowly, on the
        /// shaders' clock, so a body that is not swimming still looks alive (<c>TheatreBody.shader</c>,
        /// <c>_Sway</c>, <c>_SwayPart</c>). Nothing else moves, and neither leaves its collider. Off,
        /// the palette never sets either property and the material's zeros stand.
        /// </summary>
        private readonly bool _sway = TheatreSkin.Dial("EVOSIM_THEATRE_SWAY", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// The marine producers' bend with the current (<c>EVOSIM_THEATRE_ALGA_BEND</c>, 0 off by default), read once
        /// by <see cref="TheatreSkin.AlgaBendStrength"/>: on only with <c>EVOSIM_THEATRE_ALGA</c> and
        /// <c>EVOSIM_THEATRE_SWAY</c> both on as well. While it is on, <see cref="Flow"/> writes each blade's flow into
        /// <c>_SwayPart.w</c>, and nothing else in a block changes. Off, <see cref="Paint"/> and <see cref="Flow"/> do
        /// nothing for it and <c>_SwayPart.w</c> stays at the zero <see cref="SwayOf"/> gives it.
        /// </summary>
        private static readonly bool AlgaBendOn = TheatreSkin.AlgaBendStrength > 0f;

        /// <summary>Whether the bend with the current is on, for a view to know whether to hand <see cref="Flow"/> anything.</summary>
        public static bool AlgaBending => AlgaBendOn;

        // ARC begin
        /// <summary>
        /// The thin-axis tip angle of the blade's arc bend at level 2 (<c>EVOSIM_THEATRE_ALGA_ARC</c> at 2), degrees: the shader's
        /// <c>AlgaArc2OutAngle</c>, and the two must move together. Level 2 is a trial beyond the collider (D242).
        /// </summary>
        private const float AlgaArc2OutAngleDegrees = 20f;
        // ARCROOM begin
        /// <summary>
        /// The largest thin-axis tip angle the arc's room allows (<c>EVOSIM_THEATRE_ALGA_ARC_ROOM</c>), degrees: the shader's
        /// <c>AlgaArcRoomCeiling</c>, and the two must move together.
        /// </summary>
        private const float AlgaArcRoomCeilingDegrees = 60f;

        private static readonly HashSet<(long, int)> _arcRoomSaid = new HashSet<(long, int)>();

        /// <summary>
        /// Says once per blade how far a vertex can stand outside its box with the room dial on, by the shader's formula
        /// (<c>AlgaArcRoomCeiling</c> and the block behind <c>_EvoAlgaArc.y</c>). The tip angle is 3 (thin room over L, plus the dial),
        /// at most the ceiling, and at level 2 at least the trial's angle. The palette has no lens or curl, so the box's whole half
        /// thickness stands for the thin room, which makes the angle and the reach upper bounds: the shader's room is a share of it,
        /// and the drive and the flow's component only lower the angle. The reach is L tip / 3 less the half thickness, or zero, for
        /// L the blade's length with the anchor at an end and half of it at the middle.
        /// </summary>
        private static void LogArcRoom(long id, int part, PhenotypePart blade)
        {
            if (!_arcRoomSaid.Add((id, part))) return;

            Vector3 h = Abs(blade.HalfExtents.ToVector3());
            float thin = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
            float length = 2f * longest;
            float room = TheatreSkin.AlgaArcRoom;
            float ceiling = AlgaArcRoomCeilingDegrees * Mathf.Deg2Rad;

            float tipEnd = Mathf.Min(3f * (thin / Mathf.Max(1e-6f, length) + room), ceiling);
            float tipMiddle = Mathf.Min(3f * (thin / Mathf.Max(1e-6f, 0.5f * length) + room), ceiling);
            if (TheatreSkin.AlgaArcLevel > 1)
            {
                float trial = AlgaArc2OutAngleDegrees * Mathf.Deg2Rad;
                tipEnd = Mathf.Max(tipEnd, trial);
                tipMiddle = Mathf.Max(tipMiddle, trial);
            }

            float atEnd = Mathf.Max(0f, length * tipEnd / 3f - thin);
            float atMiddle = Mathf.Max(0f, 0.5f * length * tipMiddle / 3f - thin);

            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log("[Theatre] arc bend room " + room.ToString("F3", c) + " of the arc, body " + id + " part " + part +
                      ": blade length " + length.ToString("F3", c) + " m, thin half extent " + thin.ToString("F4", c) + " m; at most " +
                      (tipEnd * Mathf.Rad2Deg).ToString("F0", c) + " degrees with the anchor at an end, a vertex up to " +
                      atEnd.ToString("F3", c) + " m (" + (atEnd / Mathf.Max(1e-6f, length)).ToString("P1", c) +
                      " of the length) outside its box, and at most " + (tipMiddle * Mathf.Rad2Deg).ToString("F0", c) +
                      " degrees, " + atMiddle.ToString("F3", c) + " m (" + (atMiddle / Mathf.Max(1e-6f, length)).ToString("P1", c) +
                      "), with it at the middle.");
        }
        // ARCROOM end

        private static readonly HashSet<(long, int)> _arcTrialSaid = new HashSet<(long, int)>();

        private static bool _arcYieldSaid;

        /// <summary>
        /// Says once that the arc yields: with <c>EVOSIM_THEATRE_ALGA_BEND</c> above 1 that dial's lean already spends room past
        /// the box, so the arc does nothing on a blade and the level 2 trial has nothing to report.
        /// </summary>
        private static void LogArcYield()
        {
            if (_arcYieldSaid) return;

            _arcYieldSaid = true;
            Debug.Log("[Theatre] algal blade arc bend yields: EVOSIM_THEATRE_ALGA_BEND is above 1, and its lean already spends " +
                      "room past the box, so the arc does nothing on any blade.");
        }

        /// <summary>
        /// Says once per blade how far a vertex can stand outside its box at level 2, by the shader's formula
        /// (<c>TheatreBody.shader</c>, the section above <c>AlgaBendFlow</c>). The arc's tip stands sideways by L tip / 3, with
        /// tip the angle in radians and L the arc from the anchor to the far end: the blade's length 2 hL with the anchor at an
        /// end, half of that with it at the middle. The tip has no lens and no curl, so the box's half thickness is all the room
        /// it has; the distance is that less the half thickness, or zero. The sizes are the part's rest sizes, as the
        /// phenotype has them, in metres; the shader sees them times the body's scale.
        /// </summary>
        private static void LogArcTrial(long id, int part, PhenotypePart blade)
        {
            if (!_arcTrialSaid.Add((id, part))) return;

            Vector3 h = Abs(blade.HalfExtents.ToVector3());
            float thin = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
            float length = 2f * longest;
            float tip = AlgaArc2OutAngleDegrees * Mathf.Deg2Rad;

            float atEnd = Mathf.Max(0f, length * tip / 3f - thin);
            float atMiddle = Mathf.Max(0f, 0.5f * length * tip / 3f - thin);

            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log("[Theatre] arc bend trial beyond the collider (D242), body " + id + " part " + part + ": blade length " +
                      length.ToString("F3", c) + " m, thin half extent " + thin.ToString("F4", c) + " m (h/L " +
                      (thin / Mathf.Max(1e-6f, length)).ToString("F3", c) + "); at " + AlgaArc2OutAngleDegrees.ToString("F0", c) +
                      " degrees a vertex can stand up to " + atEnd.ToString("F3", c) + " m (" +
                      (atEnd / Mathf.Max(1e-6f, length)).ToString("P1", c) + " of the length) outside its box with the anchor at an end, " +
                      atMiddle.ToString("F3", c) + " m (" + (atMiddle / Mathf.Max(1e-6f, length)).ToString("P1", c) +
                      ") with it at the middle.");
            // ARCROOM begin
            if (TheatreSkin.AlgaArcRoom > 0f) LogArcRoom(id, part, blade);
            // ARCROOM end
        }

        // ARC end
        /// <summary>
        /// The flow speed, m/s, at which a blade has leaned 63 % of the way (1 - 1/e) toward the most the bend gives.
        /// Typical run currents are a few centimetres a second and a swimmer's body a few tenths, so this puts the
        /// current's bend in the lower half of the range and a swimmer's near the top.
        /// </summary>
        private const float AlgaBendSpeedScale = 0.1f;

        // The shader's AlgaBendAzimuthSteps and AlgaBendSpeedSteps; the two must move together.
        private const int AlgaBendAzimuthSteps = 360;
        private const int AlgaBendSpeedSteps = 255;

        /// <summary>
        /// What a part does under the sway dial, as <c>_SwayPart.x</c> tells the shader: a
        /// photosynthetic part sways, a buoyancy part pulses, and every other part, every neck and
        /// every corpse holds still. The shader's <c>SwayKindLeaf</c> and <c>SwayKindFloat</c>; the
        /// two must move together.
        /// </summary>
        private const float SwayKindLeaf = 1f;

        private const float SwayKindFloat = 2f;

        /// <summary>
        /// The leaf's sway period, seconds: a frond in a slow swell. Each body takes one period in
        /// this range from its id, so neighbours drift in and out of step.
        /// </summary>
        private const float LeafSwayFastest = 3f;

        private const float LeafSwaySlowest = 5f;

        /// <summary>The float's pulse period, seconds: a medusa's bell at rest, a little quicker than a frond.</summary>
        private const float FloatPulseFastest = 2f;

        private const float FloatPulseSlowest = 3f;

        /// <summary>
        /// The share of a cycle each joint between a part and the body's root puts the part behind
        /// it, so a body moves as one with a small lag outward, as a swell runs up a frond.
        /// </summary>
        private const float SwayLagPerJoint = 0.06f;

        /// <summary>
        /// Mixes of the creature's own seed for its beat's period and its phase, apart from each
        /// other and from the shader's uses of the same seed, so the two do not line up.
        /// </summary>
        private const float SwayPeriodMix = 7.919f;

        private const float SwayPeriodOffset = 0.313f;

        private const float SwayPhaseMix = 13.73f;

        private const float SwayPhaseOffset = 0.587f;

        public int Painted => _bodies.Count;

        /// <summary>
        /// The safari's colony tint (its call-outs, <c>EVOSIM_THEATRE_SAFARI_CALLOUTS=1</c>): when
        /// set, a body it answers false for is painted grey and darker, so the subject clade stands
        /// in full colour among the rest. Null paints every body as it always was.
        /// </summary>
        public System.Func<long, bool> InFocus;

        /// <summary>How much of a body's brightness a body out of focus keeps.</summary>
        public float OutOfFocusBrightness = 0.5f;

        /// <summary>
        /// A body's lineage, for its hue (item 4 of the owner's leaf ruling, 2026-09-24): the
        /// safari sets it to the clade its guide names, so a clade reads as one kind of plant
        /// among others of its guild. -1, or null, paints the guild's own colour.
        /// </summary>
        public System.Func<long, long> LineageOf;

        /// <summary>
        /// How far a lineage's hue turns from its guild's, either way, as a fraction of the
        /// colour wheel (<c>EVOSIM_THEATRE_LINEAGE_HUE</c>, 0 to 0.2, default 0.06: about twenty
        /// degrees, so a leaf is still green and a stomach still amber).
        /// </summary>
        public float LineageHue = TheatreSkin.Dial("EVOSIM_THEATRE_LINEAGE_HUE", 0.06f, 0f, 0.2f);

        /// <summary>
        /// The skin map's turn for a non-producer's body: the palette hue its base hue picks, less the hue of the body's main tint.
        /// The main tint is the colour of the cell type with the most volume weighted by its colour's saturation (so a bulk of grey
        /// structure does not claim a body that carries coloured tissue), leaving out photosynthetic tissue, whose colour is the alga
        /// look's. Worked out once a body and kept in its entry.
        /// </summary>
        private float SkinMainTurnOf(TheatreSkinMap.Entry skin)
        {
            if (!float.IsNaN(skin.MainTurn)) return skin.MainTurn;

            string[] features = TheatreSkinMapConstants.Features;
            float bestScore = -1f, bestShare = -1f, mainHue = 0f, shareHue = 0f;

            for (int j = 0; j < features.Length; j++)
            {
                if (!features[j].StartsWith("share_", System.StringComparison.Ordinal)) continue;

                string cell = features[j].Substring(6);
                if (cell == CellTypeIds.Photosynthetic) continue;

                float share = (float)skin.Features[j];
                Color.RGBToHSV(ColourOf(cell), out float h, out float s, out _);

                if (share * s > bestScore)
                {
                    bestScore = share * s;
                    mainHue = h;
                }

                if (share > bestShare)
                {
                    bestShare = share;
                    shareHue = h;
                }
            }

            // Every coloured tissue absent, or none coloured: the biggest tissue's hue, however pale.
            if (!(bestScore > 0f)) mainHue = shareHue;

            skin.MainTurn = TheatreSkinMap.PaletteTurn(skin, mainHue);
            return skin.MainTurn;
        }

        /// <summary>A lineage's turn of the hue, from its key: the same key, the same turn.</summary>
        private float TurnOf(long id)
        {
            if (LineageOf == null || LineageHue <= 0f) return 0f;

            long key = LineageOf(id);
            if (key < 0) return 0f;

            return TurnFor(key, LineageHue);
        }

        /// <summary>
        /// A lineage key's turn of the hue at a strength: the one arithmetic every body of the
        /// lineage is painted by, so a story's chart can draw a clade in the colour its bodies wear.
        /// </summary>
        public static float TurnFor(long key, float lineageHue)
        {
            if (key < 0 || lineageHue <= 0f) return 0f;

            ulong h = unchecked((ulong)key * 0x9E3779B97F4A7C15UL);
            h ^= h >> 29;
            h = unchecked(h * 0xBF58476D1CE4E5B9UL);
            h ^= h >> 32;

            float signed = (h & 0xFFFFF) / (float)0x80000 - 1f;
            return lineageHue * signed;
        }

        /// <summary>
        /// A colour with its hue turned and its saturation nudged the same way. With <paramref name="hueOnly"/> (the skin map's
        /// palette turn, which can be half the wheel) the saturation and value are kept exactly and only the hue moves.
        /// </summary>
        public static Color Turned(Color c, float turn, bool hueOnly = false)
        {
            if (turn == 0f) return c;

            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + turn, 1f);
            if (!hueOnly) s = Mathf.Clamp01(s * (1f + 1.5f * turn));

            Color turned = Color.HSVToRGB(h, s, v);
            turned.a = c.a;
            return turned;
        }

        /// <summary>A colour's grey of the same luminance.</summary>
        public static Color Grey(Color c)
        {
            float y = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return new Color(y, y, y, c.a);
        }

        /// <summary>
        /// The reserve tint, 0 (at the gate) to 1 (sated) — the same squash
        /// <c>CreatureSensors</c> puts on <see cref="SensorChannel.Energy"/>, so what a viewer
        /// sees and what a creature feels are the same number.
        /// </summary>
        /// <param name="secondsOfReserve"><c>Organism.SecondsOfReserve</c>; infinity reads 1.</param>
        /// <param name="fullScaleSeconds"><c>RunConfig.EnergyFullScaleSeconds</c>.</param>
        public static float Tint(float secondsOfReserve, float fullScaleSeconds)
        {
            if (float.IsPositiveInfinity(secondsOfReserve)) return 1f;
            if (!(secondsOfReserve > 0f)) return 0f;

            float scale = Mathf.Max(1e-6f, fullScaleSeconds);
            return Mathf.Clamp01((float)System.Math.Tanh(secondsOfReserve / scale));
        }

        /// <summary>Whether any part of the body is photosynthetic: the pose stream's producer flag (PoseStream.PhotosyntheticBit).</summary>
        private static bool HasPhotosynthetic(Phenotype phenotype)
        {
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].CellTypeId == CellTypeIds.Photosynthetic) return true;
            }

            return false;
        }

        /// <summary>
        /// The colour <see cref="Paint"/> gives one part of a body before brightness, greyness and the reserve: the guild's colour (white
        /// while the guilds are not painted, <paramref name="on"/> false), the marine producers' recolour under their dial, and the
        /// lineage's turn of hue, by the same steps in the same order. What a crumbling body's flakes start in (D277). It reads
        /// state and writes none, and Paint does not call it.
        /// </summary>
        /// <param name="id">The key the body is painted under (a corpse's is its view's, not its living id).</param>
        /// <param name="dead">A corpse, whose producers' form is the dead one.</param>
        public Color PaintedGuildOf(long id, Phenotype phenotype, int part, bool on, bool dead = false)
        {
            bool known = part >= 0 && part < phenotype.PartCount;

            Color guild = Color.white;
            if (on) guild = known ? ColourOf(phenotype.Parts[part].CellTypeId) : ColourOf(CellTypeIds.Structural);

            float turn = on ? TurnOf(id) : 0f;

            TheatreSkinMap.Entry skin = null;
            if (TheatreSkinMap.Enabled && on && TheatreSkinMap.TryGet(id, out skin)) turn = TheatreSkinMap.TintTurn(skin);

            bool wideHue = skin != null && !skin.Producer;
            float wideTurn = wideHue ? SkinMainTurnOf(skin) : 0f;

            AlgaDress alga = _alga && on && known ? AlgaDressOf(phenotype, part, turn, dead, dead) : default;
            if (alga.Recoloured) guild = alga.Guild;

            bool partWide = wideHue && !alga.Recoloured;
            float partTurn = partWide ? wideTurn : turn;

            if (on) guild = Turned(guild, partTurn, partWide);

            return guild;
        }

        /// <summary>Paints one body: cell type per part, brightness by reserve.</summary>
        /// <param name="id">The creature, so its renderer table can be cached.</param>
        /// <param name="root">The body's root, from <see cref="CreatureIdMap"/>.</param>
        /// <param name="phenotype">Its developed body, for the cell type of each part.</param>
        /// <param name="tint">0–1 from <see cref="Tint"/>.</param>
        /// <param name="on">False restores the plain look.</param>
        /// <param name="corpse">
        /// A corpse (D127): every part in one grey at <see cref="CorpseBrightness"/>, whatever its tissue
        /// and whatever <paramref name="tint"/> says.
        /// </param>
        /// <param name="remains">
        /// Remains (D220): the guild's own colour taken <see cref="RemainsGreyness"/> of the way to its grey, no
        /// reserve, <see cref="RemainsBrightness"/>, a corpse's dull and nearly opaque tissue, and no sway. False
        /// paints as it always did.
        /// </param>
        /// <param name="crumble">
        /// D277's progress of a crumbling remains body, 0 to 1 (<c>EVOSIM_THEATRE_CRUMBLE</c>): above 0, remains are taken from
        /// <see cref="RemainsGreyness"/> toward full grey and from <see cref="RemainsBrightness"/> toward
        /// <see cref="CorpseBrightness"/> by that share. 0 paints as it always did; a corpse, already grey, ignores it.
        /// </param>
        /// <param name="drain">
        /// The death cue's drain of a dead body's remains (<c>EVOSIM_THEATRE_DEATH_CUE</c>, <see cref="TheatreDeathCue.DrainOf"/>), 0 at its
        /// death row to 1 drained, eased: the remains are painted as they are without it, their sheen, rim light, iridescence and
        /// chromatophores taken down by it to none, and every renderer's <c>_DeathDrain</c> set to it, by which the body shader fades the
        /// drawn colour toward the colour's own luminance times 0.7 with no hue. Nothing switches at either end, so the drain has no step.
        /// -1, the default and all it is with the dial off, paints as it always did; a corpse ignores it.
        /// </param>
        public void Paint(long id, Transform root, Phenotype phenotype, float tint, bool on, bool corpse = false, bool remains = false, float crumble = 0f,
            float drain = -1f)
        {
            if (root == null || phenotype == null) return;

            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root)
            {
                body = Gather(id, root, phenotype);
                _bodies[id] = body;
            }

            // EVOSIM_THEATRE_FIXED_TINT (D276): the body's own reserve is not drawn; every body takes its kind's one tint.
            if (!float.IsNaN(FixedTint))
            {
                if (!_fixedTintLogged)
                {
                    _fixedTintLogged = true;
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Theatre] fixed tint (D276): every body at {0:0.###}, a producer at {1:0.###}; no body is shaded by its reserve",
                        FixedTint, float.IsNaN(FixedProducerTint) ? FixedTint : FixedProducerTint));
                }

                tint = !float.IsNaN(FixedProducerTint) && HasPhotosynthetic(phenotype) ? FixedProducerTint : FixedTint;
            }

            // A corpse has no reserve and no guild: dead tissue is drawn in one grey.
            bool dead = on && corpse;

            // Remains keep their colours, dulled; a corpse (D127) has none. Never both: a corpse wins.
            bool aged = on && remains && !corpse;

            // The death cue's drain (EVOSIM_THEATRE_DEATH_CUE): false with the dial off, and then no line below changes.
            bool drained = aged && drain >= 0f;
            float undrained = drained ? 1f - Mathf.Clamp01(drain) : 1f;

            // The death look's milk (EVOSIM_THEATRE_DEATH_LOOK): what is kept of the sheen, the rim light, the light through the tissue and
            // the chromatophores and iridescence at this age. Each is exactly `undrained` (or one) with the dial at 0, and then no line
            // below changes. The sheen and the rim take the milk's lift at a part of middling thinness, the chromatophores and the
            // iridescence go to none with the hue, and the shader's per fragment lift (thick parts first) has the last word on the colour.
            float sheenKeep = undrained, rimKeep = undrained, tissueKeep = undrained, transKeep = 1f;
            if (drained && TheatreDeathCue.Look > 0)
            {
                float milkAge = Mathf.Clamp01(drain) * TheatreDeathCue.DrainSpan;
                float milkLift = TheatreDeathCue.MilkLift(milkAge, MilkMiddlingThin);
                sheenKeep = 1f - TheatreDeathCue.MilkSheenLoss * milkLift;
                rimKeep = 1f - TheatreDeathCue.MilkRimLoss * milkLift;
                tissueKeep = 1f - TheatreDeathCue.MilkHue(milkAge);
                transKeep = 1f - TheatreDeathCue.MilkTransmissionLoss * milkLift;
            }

            // Variant 3's front (EVOSIM_THEATRE_DEATH_LOOK 3): the body's centre and largest reach in the world, once for all its parts,
            // so the front crosses the whole body on one clock. Zero, and never read, for any other look.
            Vector4 deathFront = drained && TheatreDeathCue.Look == 3 ? DeathFrontOf(body, phenotype) : Vector4.zero;

            float reserve = dead || aged ? 0f : Mathf.Clamp01(tint);
            float brightness = on ? (dead ? CorpseBrightness : aged ? (crumble > 0f ? Mathf.Lerp(RemainsBrightness, CorpseBrightness, crumble) : RemainsBrightness) : Mathf.Lerp(Starving, 1f, reserve)) : 1f;
            bool dim = on && InFocus != null && !InFocus(id);
            if (dim) brightness *= OutOfFocusBrightness;

            float turn = on ? TurnOf(id) : 0f;

            // The genome-to-skin map (EVOSIM_THEATRE_SKIN_MAP): the body's skin vector, null with the dial off, for a body never
            // registered or a body painted plain. Its base hue takes the lineage's turn's place.
            TheatreSkinMap.Entry skin = null;
            if (TheatreSkinMap.Enabled && on && TheatreSkinMap.TryGet(id, out skin)) turn = TheatreSkinMap.TintTurn(skin);

            // A body that is not a producer takes its main tint's hue from the marine palette (base hue picks it) and turns every
            // colour of its body by the same amount, hue alone. A part the alga look recolours keeps the producers' narrow turn.
            bool wideHue = skin != null && !skin.Producer;
            float wideTurn = wideHue ? SkinMainTurnOf(skin) : 0f;

            if (_block == null) _block = new MaterialPropertyBlock();

            // The skin map's part-colour dial (EVOSIM_THEATRE_SKIN_MAP_PART_COLOUR): the body's largest distance from its root, once for
            // all its parts, which is what each part's place t is a share of. Zero (and no part gets a place) with the dial off.
            float partReach = (TheatreSkinMap.PartOn || TheatreSkinMap.ZoneOn) && wideHue ? MaxDistanceFromRoot(phenotype) : 0f;

            // The pattern's field, tile and cover for this body (EVOSIM_THEATRE_PATTERN), looked up once for all its
            // parts. Null for a greyed body or a corpse: every part is then plain.
            long patternStart = DressTimers.Now(); // the film's timing: dress line
            TheatrePattern.Solved pattern = _pattern && on && !dead && !dim ? PatternOf(id, body, phenotype) : null;
            DressTimers.PatternLookupMs += DressTimers.Since(patternStart);
            float patternTile = pattern != null ? PatternTileOf(phenotype) * pattern.ScaleMultiple : 0f;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                int part = body.Part[i];
                bool known = part >= 0 && part < phenotype.PartCount;

                Color guild = Color.white;
                if (on) guild = known ? ColourOf(phenotype.Parts[part].CellTypeId) : ColourOf(CellTypeIds.Structural);

                // The marine producers (EVOSIM_THEATRE_ALGA), only while their dial is on: a producer's colour from
                // its family by its proportions, its depth tint, and a stipe's and a gas bladder's dress
                // (AlgaDressOf). Before the lineage's turn and the grey, which apply to it as to any guild colour.
                AlgaDress alga = _alga && on && known ? AlgaDressOf(phenotype, part, turn, dim || dead, dead) : default;
                // The algal blade (EVOSIM_THEATRE_ALGA_BLADE): the first frond painted turns its shader keyword on.
                if (TheatreSkin.AlgaBladeOn && alga.Shallow.w > 0.5f) TheatreSkin.EnsureAlgaBladeKeyword();
                if (TheatreSkin.AlgaBlade2On && alga.Shallow.w > 0.5f) TheatreSkin.EnsureAlgaBlade2Keyword();
                // BLADE3 begin
                if (TheatreSkin.AlgaBlade3On && alga.Shallow.w > 0.5f) TheatreSkin.EnsureAlgaBlade3Keyword();
                // BLADE3 end
                // ARC begin
                long arcStart = DressTimers.Now(); // the film's timing: dress line
                if (TheatreSkin.AlgaArcLevel > 0 && alga.Shallow.w > 0.5f)
                {
                    TheatreSkin.EnsureAlgaArcKeyword();
                    if (TheatreSkin.AlgaBendStrength > 1f) LogArcYield();
                    else if (TheatreSkin.AlgaArcLevel > 1) LogArcTrial(id, part, phenotype.Parts[part]);
                }
                // ARCROOM begin
                if (TheatreSkin.AlgaArcRoom > 0f && alga.Shallow.w > 0.5f && TheatreSkin.AlgaBendStrength <= 1f) LogArcRoom(id, part, phenotype.Parts[part]);
                // ARCROOM end
                DressTimers.ArcBendMs += DressTimers.Since(arcStart);
                // ARC end
                if (alga.Recoloured) guild = alga.Guild;

                bool partWide = wideHue && !alga.Recoloured;
                float partTurn = partWide ? wideTurn : turn;

                if (on) guild = Turned(guild, partTurn, partWide);
                if (dim || dead) guild = Grey(guild);
                else if (aged) guild = Color.Lerp(guild, Grey(guild), Mathf.Clamp01(crumble > 0f ? Mathf.Lerp(RemainsGreyness, 1f, crumble) : RemainsGreyness));

                Reshape(body, i, renderer.transform);

                string cellType = known ? phenotype.Parts[part].CellTypeId : CellTypeIds.Structural;

                float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
                seed -= Mathf.Floor(seed);

                Vector4 carve = Character(cellType, seed);

                // Lobed absorbers, under the organic dial alone (OrganicAbsorberLobes): the lobes' weight
                // and nothing else, so the carve's caps still bound its depth.
                if (_organic && cellType == CellTypeIds.Absorptive) carve.y *= OrganicAbsorberLobes;

                // The skin map's carve character: the guild's carve depth times the vector's, in place of the plan hash's share
                // of the character. The seed in x keeps the hash, and the carve's cap still has the last word.
                if (skin != null) carve.w *= TheatreSkinMap.CarveGain(skin);

                float own = OwnOf(body, part);

                Vector4 sway = _sway ? SwayOf(body, phenotype, part, cellType, corpse || aged) : Vector4.zero;

                // The bend with the current: the flow the last Flow gave this blade survives a repaint, and the
                // vector is kept for Flow to rewrite. Not entered with the dial off.
                if (AlgaBendOn)
                {
                    if (body.SwayWas == null || body.SwayWas.Length != body.Renderers.Length)
                    {
                        body.SwayWas = new Vector4[body.Renderers.Length];
                    }

                    if (body.FlowCode != null && Mathf.Abs(sway.x - SwayKindLeaf) < 0.5f) sway.w = body.FlowCode[i];

                    body.SwayWas[i] = sway;
                }

                float sheen = on ? (dead || aged ? CorpseSheen : SheenOf(cellType)) : 0.1f;
                if (alga.Bladder && !dead) sheen = Mathf.Max(sheen, AlgaBladderSheen);

                // The skin map's axes dial (EVOSIM_THEATRE_SKIN_MAP_AXES, mode 3): a wide-hue part in colour, not dead or aged, has its sheen
                // multiplied by the body's sheen factor. AxesOn is false otherwise, and then the line does nothing.
                if (TheatreSkinMap.AxesOn && on && partWide && !dim && !dead && !aged && skin != null) sheen = TheatreSkinMap.Sheened(sheen, skin);

                // The death cue's drain takes the sheen (every specular lobe of the body's shader scales by it) down to none.
                if (drained) sheen *= sheenKeep;

                // The part-colour dial: this part's place in the body (its distance from the root over the body's largest, 0 for a body
                // of one part) and its own elongation, for the levelling to move its colours by. NaN (not given) with the dial off.
                float partT = float.NaN, partU = float.NaN;

                if ((TheatreSkinMap.PartOn || TheatreSkinMap.ZoneOn) && partWide && known)
                {
                    PhenotypePart placed = phenotype.Parts[part];
                    partT = partReach > 0f ? Mathf.Clamp01(placed.DistanceFromRoot / partReach) : 0f;
                    partU = ElongationOf(placed);
                }

                Set(renderer, guild, alga.Stipe ? brightness * AlgaStipeShade : brightness, on ? reserve : 1f, on,
                    carve, body.PinchA[i], body.PinchB[i],
                    (on ? (dead || aged ? CorpseTransmission : TransmissionOf(cellType)) : 0.4f) * transKeep,
                    sheen, body.Leaf[i], own, rimGain: drained ? rimKeep : 1f,
                    look: TheatreTissue.LookOn && on ? TheatreTissue.Of(cellType) : null,
                    alga: alga.Form, algaDeep: alga.Deep, algaShallow: alga.Shallow, algaHue: alga.Recoloured,
                    turn: partTurn, grey: dim || dead, sway: sway,
                    rampAxis: _ramp && body.Ramp != null ? body.Ramp[i] : Vector4.zero,
                    rampTip: _ramp && on ? RampTipOf(cellType) : Vector4.zero,
                    velvet: _velvet && on && cellType == CellTypeIds.Absorptive,
                    osculum: _velvet && on && body.Velvet != null ? body.Velvet[i] : Vector4.zero,
                    linkAxis: _link && on && body.LinkAxis != null ? body.LinkAxis[i] : Vector4.zero,
                    linkShape: _link && on && body.LinkShape != null ? body.LinkShape[i] : Vector4.zero,
                    rimBand: _rimBand && on && !dead && !dim && cellType == CellTypeIds.Consumer,
                    chromatophores: _chromatophores && on && !dead && !dim ? (drained ? tissueKeep * ChromatophoresOf(cellType) : ChromatophoresOf(cellType)) : 0f,
                    iridescence: _iridescence && on && !dead && !dim ? (drained ? tissueKeep * IridescenceOf(cellType) : IridescenceOf(cellType)) : 0f,
                    pattern: pattern != null
                        ? new Vector4(patternTile, TheatreSkinMap.MazeStrength(skin, PatternStrengthOf(cellType)), pattern.Rotation, pattern.Cover)
                        : Vector4.zero,
                    patternShift: pattern != null
                        ? new Vector4(
                            Mathf.Repeat(pattern.OffsetU + 0.61803399f * Mathf.Max(0, part), 1f),
                            Mathf.Repeat(pattern.OffsetV + 0.41421356f * Mathf.Max(0, part), 1f), 0f, 0f)
                        : Vector4.zero,
                    patternTexture: pattern != null ? pattern.Texture : null,
                    cellView: CellView && on, cellColour: CellView ? CellViewColourOf(cellType) : default,
                    skin: skin, wideHue: partWide, partT: partT, partU: partU);
                // THALLI begin
                if (TheatreSkin.ThalliOn && on && !dim && !dead && body.Thallus != null && i < body.Thallus.Length && body.Thallus[i] > 0)
                    PaintThallus(renderer, body.Thallus[i] - 1, brightness);
                // THALLI end
                // SNOUT begin
                long noseStart = DressTimers.Now(); // the film's timing: dress line
                if (TheatreNosePit.SnoutOn) TheatreNosePit.Snout(renderer, phenotype, part, cellType, RawShapes);
                // SNOUT end
                if (TheatreNosePit.Enabled) TheatreNosePit.Paint(renderer, phenotype, part, cellType, RawShapes);
                long mouthStart = DressTimers.Now();
                DressTimers.NoseMs += (mouthStart - noseStart) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                if (TheatreMouth2.Enabled) TheatreMouth2.Paint(renderer, phenotype, part, RawShapes, skin); // mouth2
                DressTimers.MouthMs += DressTimers.Since(mouthStart);
                if (TheatreSkin.Skin2On) PaintSkin2(renderer, id, body, phenotype, part, cellType, on && !dead && !dim, pattern); // skin2

                // The death cue's drain, last on the renderer's block (the one skin copies it from there): the body shader fades the
                // part's drawn colour toward a dull grey by it (_DeathDrain, under _EVO_DEATH_DRAIN). Never with the dial off.
                if (drained) SetDeathDrain(renderer, id, drain, deathFront);
            }

            // SNOUT begin
            if (TheatreNosePit.SnoutOn) PaintSnouts(body, phenotype, on, dim || dead);

            // SNOUT end
            RefreshNecks(body, phenotype, brightness, on ? reserve : 1f, on, dim || dead, turn, skin);

            // The death cue's drain on the knuckles and bridges too, after their paint. Never with the dial off.
            if (drained && body.Necks != null)
            {
                for (int n = 0; n < body.Necks.Length; n++)
                {
                    MeshRenderer neck = body.Necks[n] != null ? body.Necks[n].GetComponent<MeshRenderer>() : null;
                    if (neck != null) SetDeathDrain(neck, id, drain, deathFront);
                }
            }

            if (TheatreUptake.Enabled) TheatreUptake.Seen(id, root, phenotype, !on || corpse || RawShapes);
            if (TheatreMouth2.Bite > 0f) TheatreMouthBite.Seen(id, root, phenotype, !on || corpse || RawShapes);
            // DEBUGTINT begin
            if (TheatreDebugTint.On) TheatreDebugTint.Apply(id, root);
            // DEBUGTINT end
        }

        /// <summary>
        /// The death cue's drain written into one renderer's block, over whatever the paint left there: <c>_DeathDrain</c>, which the
        /// body shader reads only under <c>_EVO_DEATH_DRAIN</c>. Called only for a drained body, so never with the dial off. front is
        /// variant 3's body centre and reach (<see cref="DeathFrontOf"/>), written only with the death look at 3.
        /// </summary>
        private void SetDeathDrain(Renderer renderer, long id, float drain, Vector4 front)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(Shader.PropertyToID("_DeathDrain"), Mathf.Clamp01(drain));

            // The death look's milk (EVOSIM_THEATRE_DEATH_LOOK): the age in the run's seconds and the variant, for the shader's per
            // fragment ramps, and the diffuse wrap moved from the material's toward MilkWrap by the lift at a part of middling thinness
            // (a softer terminator; _Wrap sits in the material's buffer, as _TransGain does, and a block overrides it). Never with the dial at 0.
            if (TheatreDeathCue.Look > 0)
            {
                float age = Mathf.Clamp01(drain) * TheatreDeathCue.DrainSpan;
                Material shared = renderer.sharedMaterial;
                float wrap = shared != null && shared.HasProperty(WrapId) ? shared.GetFloat(WrapId) : MilkMaterialWrap;

                _block.SetVector(DeathMilkId, new Vector4(age, TheatreDeathCue.Look, 0f, 0f));
                _block.SetFloat(WrapId, Mathf.Lerp(wrap, TheatreDeathCue.MilkWrap, TheatreDeathCue.MilkLift(age, MilkMiddlingThin)));

                // Variant 3's front: the body's centre in the world (xyz) and its largest reach from there (w), the same on every
                // renderer of the body, so the shader's front is one per body. Never with any other look.
                if (TheatreDeathCue.Look == 3) _block.SetVector(DeathFrontId, front);

                if (MilkLog && (id == MilkLogSubject || id == -(MilkLogSubject + 1))) LogMilk(renderer, id, age, front);
            }

            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// EVOSIM_THEATRE_DEATH_MILK_LOG (0 or 1, 0 off by default), read once: with it on, the values the death look writes into a
        /// renderer's block are logged as each renderer's age passes a whole second, with whether the milk's keyword is on at all,
        /// so a change to the milk is proved to reach the shader before a frame is read for it. Only the film's subject
        /// (EVOSIM_THEATRE_FILM_SUBJECT, or its corpse), so the lines go to the body being judged. At most <see cref="MilkLogLines"/> lines.
        /// </summary>
        private static readonly bool MilkLog = TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_MILK_LOG", 0f, 0f, 1f) >= 0.5f;

        private const int MilkLogLines = 200;

        private static readonly long MilkLogSubject =
            long.TryParse(System.Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT"), out long subject) ? subject : long.MinValue + 1;

        private static readonly HashSet<(Renderer, int)> _milkLogged = new HashSet<(Renderer, int)>();

        private static void LogMilk(Renderer renderer, long id, float age, Vector4 front)
        {
            if (_milkLogged.Count >= MilkLogLines) return;
            if (!_milkLogged.Add((renderer, Mathf.FloorToInt(age)))) return;

            Material shared = renderer.sharedMaterial;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Theatre] death milk: body {10} {0} age {1:0.###} s look {2} front {3:0.###} {4:0.###} {5:0.###} reach {6:0.###}; shader {7}, " +
                "_EVO_DEATH_MILK {8}; MilkLuminance3 {9}",
                renderer.name, age, TheatreDeathCue.Look, front.x, front.y, front.z, front.w,
                shared != null ? shared.shader.name : "none",
                Shader.IsKeywordEnabled("_EVO_DEATH_MILK") ? "on (global)" : "off",
                TheatreDeathCue.MilkLuminance3, id));
        }

        /// <summary>
        /// Variant 3's front for a body: the centre of the box about its parts in the world (xyz), as the death cue's cloud takes it, and
        /// the body's largest reach from that centre (w), the furthest any part's box stands from it along the line out to the part. A
        /// part's box is its half extents along its own axes, times its scale in the world (a corpse's shrink sits on its root).
        /// </summary>
        private static Vector4 DeathFrontOf(Body body, Phenotype phenotype)
        {
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 hi = -lo;
            bool any = false;

            // The box about the parts, for the centre.
            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (!DeathPartBox(body, phenotype, i, out Transform t, out Vector3 h)) continue;

                Vector3 e = Abs(t.right) * h.x + Abs(t.up) * h.y + Abs(t.forward) * h.z;
                lo = Vector3.Min(lo, t.position - e);
                hi = Vector3.Max(hi, t.position + e);
                any = true;
            }

            if (!any)
            {
                Vector3 root = body.Root != null ? body.Root.position : Vector3.zero;
                return new Vector4(root.x, root.y, root.z, MilkFrontFallbackReach);
            }

            Vector3 centre = 0.5f * (lo + hi);
            float reach = 0f;

            // The reach: how far each part's box stands from the centre along the line out to the part.
            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (!DeathPartBox(body, phenotype, i, out Transform t, out Vector3 h)) continue;

                Vector3 outward = t.position - centre;
                float distance = outward.magnitude;
                Vector3 way = distance > 1e-6f ? outward / distance : Vector3.up;
                float far = distance + Mathf.Abs(Vector3.Dot(way, t.right)) * h.x + Mathf.Abs(Vector3.Dot(way, t.up)) * h.y +
                            Mathf.Abs(Vector3.Dot(way, t.forward)) * h.z;
                reach = Mathf.Max(reach, far);
            }

            return new Vector4(centre.x, centre.y, centre.z, Mathf.Max(reach, MilkFrontLeastReach));
        }

        /// <summary>A renderer's part transform and its half extents in the world, metres; false for a renderer of no known part.</summary>
        private static bool DeathPartBox(Body body, Phenotype phenotype, int i, out Transform t, out Vector3 h)
        {
            t = null;
            h = Vector3.zero;

            MeshRenderer renderer = body.Renderers[i];
            int part = body.Part[i];
            if (renderer == null || part < 0 || part >= phenotype.PartCount) return false;

            t = renderer.transform.parent;
            if (t == null) return false;

            float s = Mathf.Abs(t.lossyScale.x);
            if (!(s > 0f) || float.IsInfinity(s)) s = 1f;

            h = Abs(phenotype.Parts[part].HalfExtents.ToVector3()) * s;
            return true;
        }

        /// <summary>The reach, metres, variant 3's front takes for a body with no part it can measure.</summary>
        private const float MilkFrontFallbackReach = 0.1f;

        /// <summary>The least reach, metres, variant 3's front takes, so a speck of a body does not divide by nothing.</summary>
        private const float MilkFrontLeastReach = 0.01f;

        private static readonly int DeathFrontId = Shader.PropertyToID("_DeathFront");

        /// <summary>The thinness (0 thick, 1 thin) at which the palette takes the milk's lift for the sheen, rim, light through and wrap, which are per renderer.</summary>
        private const float MilkMiddlingThin = 0.5f;

        /// <summary>The material's diffuse wrap where the material has none to read (<c>_Wrap</c>'s default).</summary>
        private const float MilkMaterialWrap = 0.45f;

        private static readonly int WrapId = Shader.PropertyToID("_Wrap");
        private static readonly int DeathMilkId = Shader.PropertyToID("_DeathMilk");

        // SNOUT begin
        /// <summary>
        /// The colour pass of the nose snout (<c>EVOSIM_THEATRE_NOSE_SNOUT</c>), after every renderer of the body is painted, so
        /// the root's block holds this paint and not the last. Each nose that draws a snout takes the root part's colours
        /// (<see cref="TheatreNosePit.PaintSnout"/>). The root is the part with no parent; a nose that is the root keeps its
        /// own. <paramref name="grey"/> is a greyed or dead body, which takes no tip ramp.
        /// </summary>
        private void PaintSnouts(Body body, Phenotype phenotype, bool on, bool grey)
        {
            int rootPart = SnoutRootPart(phenotype);
            if (rootPart < 0) return;

            MeshRenderer rootRenderer = null;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (body.Part[i] == rootPart && body.Renderers[i] != null) { rootRenderer = body.Renderers[i]; break; }
            }

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                int part = body.Part[i];

                if (renderer == null || part < 0 || part >= phenotype.PartCount || part == rootPart) continue;
                if (phenotype.Parts[part].CellTypeId != CellTypeIds.Nose) continue;

                long key = ((long)(body.Root != null ? body.Root.GetHashCode() : 0) << 32) | (uint)part;

                TheatreNosePit.PaintSnout(renderer, rootRenderer, phenotype, part, rootPart, key, on ? (grey ? 1 : 0) : 2);
            }
        }

        /// <summary>The first part with no parent, or minus one.</summary>
        private static int SnoutRootPart(Phenotype phenotype)
        {
            for (int p = 0; p < phenotype.PartCount; p++)
            {
                if (phenotype.Parts[p].ParentIndex < 0) return p;
            }

            return -1;
        }

        /// <summary>
        /// The guild colour of a knuckle or bridge on a nose that draws a snout: the root part's, as the snout's is, in place
        /// of the nose tissue's. False for any other part.
        /// </summary>
        private bool SnoutNeckColour(Body body, Phenotype phenotype, int own, out Color colour)
        {
            colour = default;

            int rootPart = SnoutRootPart(phenotype);
            if (rootPart < 0 || rootPart == own) return false;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (body.Part[i] != own || body.Renderers[i] == null) continue;
                if (!TheatreNosePit.IsSnout(body.Renderers[i])) return false;

                colour = ColourOf(phenotype.Parts[rootPart].CellTypeId);
                return true;
            }

            return false;
        }

        // SNOUT end
        /// <summary>Writes one renderer's per-body properties into the shared block.</summary>
        /// <remarks>
        /// <c>_BaseColor</c> and <c>_Color</c> are both set: URP's Lit reads the first and the
        /// built-in Standard shader the second, and this class has to work whichever shader the
        /// skin resolved (or failed to). <c>_RimColor</c> and <c>_Reserve</c> exist only on the
        /// theatre's own shader and are ignored by the others, which is why the body colour still
        /// carries the reserve brightness rather than leaving it all to the glow.
        /// </remarks>
        private void Set(
            Renderer renderer, Color guild, float brightness, float reserve, bool on,
            Vector4 carve, Vector4 pinchA, Vector4 pinchB, float transmission, float sheen = 0.1f,
            Vector4 leaf = default, float individual = 0f, float rimGain = 1f, float throughGain = 1f,
            TissueLook look = null, float turn = 0f, bool grey = false, Vector4 sway = default,
            Vector4 rampAxis = default, Vector4 rampTip = default,
            bool velvet = false, Vector4 osculum = default,
            Vector4 linkAxis = default, Vector4 linkShape = default, bool rimBand = false,
            float chromatophores = 0f, Vector4 pattern = default, Texture patternTexture = null,
            Vector4 patternShift = default, float iridescence = 0f, bool cellView = false, Color cellColour = default,
            float alga = 0f, Vector4 algaDeep = default, Vector4 algaShallow = default, bool algaHue = false,
            TheatreSkinMap.Entry skin = null, bool wideHue = false, float partT = float.NaN, float partU = float.NaN)
        {
            // The skin map's factor on the body colour's saturation cap, one for a body with no vector: a multiply by exactly one
            // leaves the cap's arithmetic as it was.
            float skinSaturation = skin != null ? TheatreSkinMap.SaturationGain(skin) : 1f;

            // The skin map's deepen dial (EVOSIM_THEATRE_SKIN_MAP_DEEPEN), only while it is on: a wide-hue part in colour has the lightness
            // its body colour is muted to brought down toward a ceiling. False with the dial at 0, and then no line below changes.
            bool deepen = TheatreSkinMap.DeepenOn && on && wideHue && !grey;

            // The skin map's value dial (EVOSIM_THEATRE_SKIN_MAP_VALUE, mode 3): the same parts, and only a body with a vector. False
            // with the dial at 0 or the mode not 3, and then no line below changes.
            // Not while the OKLab levelling is on: it moves the lightness of all three colours by the same rank itself.
            bool valued = TheatreSkinMap.ValueOn && !TheatreSkinMap.OkOn && on && wideHue && !grey && skin != null;

            // The skin map's perceptual levelling (EVOSIM_THEATRE_SKIN_MAP_OKLAB, mode 3): the same parts. False with the dial at 0, and
            // then no line below changes.
            bool levelled = TheatreSkinMap.OkOn && on && wideHue && !grey && skin != null;

            Color rim = on
                ? Muted(guild, RimSaturation, RimLightness)
                : Color.white;

            // The light that comes through the tissue is the guild's hue, a little lighter than
            // the rim so that a backlit leaf reads as lit rather than as edged.
            Color through = on
                ? Muted(guild, RimSaturation, Mathf.Min(0.6f, RimLightness + 0.12f))
                : Color.white;

            Color body = on
                ? Muted(guild, BodySaturation * Mathf.Lerp(StarvingSaturation, 1f, reserve) * skinSaturation,
                    valued
                        ? TheatreSkinMap.Valued(deepen ? TheatreSkinMap.Deepened(BodyLightness) : BodyLightness, skin)
                        : deepen ? TheatreSkinMap.Deepened(BodyLightness) : BodyLightness)
                : Color.white;

            // The tissue look (EVOSIM_THEATRE_TISSUE_LOOK): the rim and the light through are the
            // tissue's own colours, turned and greyed as the guild is, and the body takes the
            // tissue's lightness. Written over today's three only when the dial is on, so with it off
            // the arithmetic above is all there is.
            bool tissue = on && look != null && TheatreTissue.LookOn;
            if (tissue)
            {
                rim = Tissue(look.RimColour, turn, grey, wideHue);
                through = Tissue(look.TransTint, turn, grey, wideHue);

                // A bold look (EVOSIM_THEATRE_TISSUE_LOOK=2) caps the body's saturation itself; every
                // other look takes the palette's cap, by the line it always took it by.
                if (look.Saturation < 0f)
                    body = Muted(guild, BodySaturation * Mathf.Lerp(StarvingSaturation, 1f, reserve) * skinSaturation,
                        valued
                            ? TheatreSkinMap.Valued(deepen ? TheatreSkinMap.Deepened(look.Lightness) : look.Lightness, skin)
                            : deepen ? TheatreSkinMap.Deepened(look.Lightness) : look.Lightness);
                else
                    body = Muted(guild, look.Saturation * Mathf.Lerp(StarvingSaturation, 1f, reserve) * skinSaturation,
                        valued
                            ? TheatreSkinMap.Valued(deepen ? TheatreSkinMap.Deepened(look.Lightness) : look.Lightness, skin)
                            : deepen ? TheatreSkinMap.Deepened(look.Lightness) : look.Lightness);
            }

            // The OKLab levelling sits here: the rim, the light through and the body are all final for a wide-hue part (the tissue
            // look has written them; the brightness, gains and alpha come after), and a part the alga look recolours is overwritten below.
            // The part-colour dial's place and elongation (partT, partU, NaN when not given: a neck and a bridge are not given, and take the
            // body colour) move the three colours inside the call; the pattern-tint dial's second colour comes out of it (alpha zero for
            // a part that is not levelled, which the block then writes as no tint).
            Color patternTint = default;
            Color familyColour = default;

            if (levelled)
            {
                // The family dial (EVOSIM_THEATRE_SKIN_MAP_FAMILY): the same levelling, and the family's colour out of it too.
                if (TheatreSkinMap.FamilyOn)
                    TheatreSkinMap.LevelWithTint(ref body, ref rim, ref through, skin, partT, partU, out patternTint, out familyColour);
                else if (TheatreSkinMap.PatternTintOn)
                    TheatreSkinMap.LevelWithTint(ref body, ref rim, ref through, skin, partT, partU, out patternTint);
                else
                    TheatreSkinMap.Level(ref body, ref rim, ref through, skin, partT, partU);
            }

            // A part the marine producers recoloured (EVOSIM_THEATRE_ALGA, algaHue: a producer, a gas bladder): the rim
            // and the light-through colours, the tissue's or the muted guild's, take the guild colour's hue at its own
            // richness (AlgaRich), so they stay in the producers' family with the body and are not a pastel. Never with
            // the dial off.
            if (algaHue)
            {
                rim = AlgaRich(rim, guild);
                through = AlgaRich(through, guild);

                // A frond (algaShallow.w one, AlgaDressOf): its body at its own lightness and saturation, golden-brown and
                // not the tissue's pastel. The guild is already the frond's colour, turned and greyed.
                if (algaShallow.w > 0.5f)
                {
                    body = Muted(guild, AlgaFrondSaturation * Mathf.Lerp(StarvingSaturation, 1f, reserve), AlgaFrondLightness);
                }
            }

            // The rim colour also drives the shader's inner glow, so a rim gain dims both, as the
            // shader's own rimScale does on a blade. The light through the tissue is not scaled by
            // brightness on any part; a neck of leaf tissue scales it by the blade's tone.
            rim *= brightness * rimGain;
            body *= brightness;
            through *= throughGain;

            // The pattern tint takes the part's brightness as the body colour the pigment would have come from does; its alpha is the share.
            if (TheatreSkinMap.PatternTintOn)
                patternTint = new Color(patternTint.r * brightness, patternTint.g * brightness, patternTint.b * brightness, patternTint.a);

            // The family colour takes the part's brightness the same way; its alpha is the strength.
            if (TheatreSkinMap.FamilyOn)
                familyColour = new Color(familyColour.r * brightness, familyColour.g * brightness, familyColour.b * brightness, familyColour.a);

            rim.a = 1f;
            body.a = 1f;
            through.a = 1f;

            renderer.GetPropertyBlock(_block);

            _block.SetColor(BaseColorId, body);
            _block.SetColor(ColorId, body);
            _block.SetColor(RimColorId, rim);
            _block.SetFloat(ReserveId, reserve);
            _block.SetColor(TransTintId, through);
            _block.SetFloat(TransGainId, transmission);
            _block.SetFloat(SheenId, sheen);
            _block.SetVector(CarveId, carve);
            _block.SetVector(PinchAId, pinchA);
            _block.SetVector(PinchBId, pinchB);
            _block.SetVector(LeafId, leaf);
            _block.SetFloat(IndividualId, individual);

            // Set only while their dial is on, and so never with it off: the material's own values,
            // which are today's, then stand. With the tissue dial on and the plain look asked for (the
            // colour key), the block is put back to the shader's own values, since a block cannot
            // forget one property and the last paint's would otherwise stay.
            if (TheatreTissue.LookOn)
            {
                _block.SetFloat(TissueLookId, tissue ? 1f : 0f);
                _block.SetFloat(RimStrengthId, TheatreTissue.ShaderRimStrength * (tissue ? look.RimStrength : 1f));
                _block.SetFloat(RimPowerId, tissue ? look.RimPower : TheatreTissue.ShaderRimPower);
                _block.SetFloat(TransMetresId, tissue ? look.TransMetres : TheatreTissue.ShaderTransMetres);
                _block.SetFloat(MottleCellsId, tissue ? look.MottleCellsPerMetre : TheatreTissue.ShaderMottleCellsPerMetre);
                _block.SetFloat(MottleStrengthId, tissue ? look.MottleStrength : TheatreTissue.ShaderMottleStrength);
                _block.SetFloat(BandingId, tissue ? look.Banding : 0f);

                // Papillae and a frilled edge are the bold looks' alone (EVOSIM_THEATRE_TISSUE_LOOK=2). Below 2
                // the palette never sets them and the material's zero stands, so the first set draws as it did;
                // at 2 a part with no bold look is put back to zero, as the lines above are.
                if (TheatreTissue.Bold)
                {
                    _block.SetFloat(PapillaeId, tissue ? look.Papillae : 0f);
                    _block.SetFloat(PapillaeCellsId, tissue ? look.PapillaeCellsPerMetre : 0f);
                    _block.SetFloat(FrillId, tissue ? look.Frill : 0f);
                }
            }

            if (_sss) _block.SetFloat(SssId, 1f);
            if (_soft) _block.SetFloat(SoftId, 1f);
            if (_softHull) _block.SetFloat(SoftHullId, 1f);
            if (_organic) _block.SetFloat(OrganicId, 1f);
            if (_organicTissue) _block.SetFloat(OrganicTissueId, 1f);

            // Countershading and the colour ramp, only while their dials are on. Written either way then
            // (on or off in the paint), since a block cannot forget a property and the plain look asked for
            // after a coloured paint would otherwise keep it. A neck passes no ramp (zero, w zero: none),
            // so it keeps its tissue's colour; it is countershaded by its own normal like any body.
            if (_countershade) _block.SetFloat(CountershadeId, on ? 1f : 0f);

            if (_ramp)
            {
                _block.SetVector(RampAxisId, on ? rampAxis : Vector4.zero);
                _block.SetVector(RampTipId, on ? rampTip : Vector4.zero);
            }

            // The leaf dial and the velvet dial, only while theirs is on. The velvet is written either way then
            // (on for an absorptive part in a coloured paint, off for anything else), since a block cannot
            // forget a property. A neck passes none, so it keeps its tissue's skin.
            if (_leafLook) _block.SetFloat(LeafLookId, on ? LeafLevel : 0f);

            // The marine producers, only while their dial is on. Written either way then (a producer's form and depth
            // tint, zero for anything else, a neck included), since a block cannot forget a property.
            if (_alga)
            {
                _block.SetFloat(AlgaId, alga);
                _block.SetVector(AlgaDeepId, algaDeep);
                _block.SetVector(AlgaShallowId, algaShallow);
            }

            if (_velvet)
            {
                _block.SetFloat(VelvetId, velvet ? VelvetLevel : 0f);
                _block.SetVector(VelvetOsculumId, velvet ? osculum : Vector4.zero);
            }

            // The link dial, only while it is on. Written either way then (zero for anything that is not a capsule's
            // shaft or cap, a neck included), since a block cannot forget a property.
            if (_link)
            {
                _block.SetVector(LinkAxisId, linkAxis);
                _block.SetVector(LinkShapeId, linkShape);
            }

            // The rim band, only while its dial is on. Written either way then (one for a consumer part in colour,
            // zero for anything else, a neck included), since a block cannot forget a property.
            if (_rimBand) _block.SetFloat(RimBandId, rimBand ? 1f : 0f);

            // The chromatophores, only while their dial is on. Written either way then (the tissue's strength for a
            // body in colour, zero for a greyed body, a corpse, a neck or a tissue that takes none), since a block
            // cannot forget a property.
            if (_chromatophores) _block.SetFloat(ChromatophoresId, chromatophores);

            // The iridescence, only while its dial is on. Written either way then (the tissue's strength for a body in
            // colour, zero for a greyed body, a corpse, a neck or a tissue that takes none), since a block cannot forget
            // a property.
            if (_iridescence) _block.SetFloat(IridescenceId, skin != null ? iridescence * TheatreSkinMap.IridescenceGain(skin) : iridescence);

            // The genome-to-skin map, only while its dial is on: the rim band's width and colour, the soft mottle's gain and the
            // countershade's share, and the flag that makes the shader read them. Written either way then (zero for a body with
            // no vector, a body painted plain or a neck of one), since a block cannot forget a property.
            if (TheatreSkinMap.Enabled) TheatreSkinMap.Write(_block, on ? skin : null);

            // The skin map's turn-all dial (EVOSIM_THEATRE_SKIN_MAP_TURN_ALL), only while it is on: a wide-hue part in colour gives
            // the rim band's warm colour, which no palette turn reaches, its share of the body's turn. A producer, a part the alga look
            // recoloured, a corpse and a greyed body are not wide-hue parts in colour, and are left as the lines above wrote them.
            if (TheatreSkinMap.TurnAllOn && on && wideHue && !grey) TheatreSkinMap.WriteTurnAll(_block, skin, turn);

            // The skin map's rim-contrast dial (EVOSIM_THEATRE_SKIN_MAP_RIM_CONTRAST), the same parts, written after turn-all's so it wins
            // where both are on: the rim band's colour is the body's palette turn plus half a turn.
            if (TheatreSkinMap.RimContrastOn && on && wideHue && !grey) TheatreSkinMap.WriteRimContrast(_block, skin, turn);

            // The skin map's pattern-tint dial (EVOSIM_THEATRE_SKIN_MAP_PATTERN_TINT), only while it is on. Written either way then (the
            // second colour for a wide-hue part in colour, alpha zero for anything else), since a block cannot forget a property and the
            // renderer's block would otherwise keep the last paint's tint.
            if (TheatreSkinMap.PatternTintOn) TheatreSkinMap.WritePatternTint(_block, patternTint);

            // The skin map's family dial (EVOSIM_THEATRE_SKIN_MAP_FAMILY), only while it is on. Written either way then (the family's colour
            // and parameters for a wide-hue part in colour whose body has a family, zeros for anything else), since a block cannot forget a property.
            if (TheatreSkinMap.FamilyOn) TheatreSkinMap.WriteFamily(_block, skin, familyColour);

            // The soft mottle, only while its dial is on, and the same for every body.
            if (_mottleSoft) _block.SetFloat(MottleSoftId, 1f);

            // The cell view, only while its dial is on. Written either way then (one and the type's colour while the
            // palette is on, zero when it is off), since a block cannot forget a property.
            if (CellView)
            {
                _block.SetFloat(CellViewId, cellView ? 1f : 0f);
                if (cellView) _block.SetColor(CellColourId, cellColour);
            }

            // The skin pattern, only while its dial is on. Written either way then (zero strength for a greyed body, a
            // corpse, a neck, a leaf or a body with no field), since a block cannot forget a property. The texture is
            // set only with a field to read; a zero strength never samples it.
            if (_pattern)
            {
                _block.SetVector(PatternId, pattern);
                _block.SetVector(PatternShiftId, patternShift);
                if (patternTexture != null) _block.SetTexture(PatternTexId, patternTexture);
            }

            if (_boardLight)
            {
                _block.SetFloat(BoardLightId, 1f);

                if (!_boardLightSaid)
                {
                    _boardLightSaid = true;
                    Debug.Log("[Theatre] board light on: the bodies take a white key, neutral fill and ambient, no depth " +
                              "light, caustics, water cuts or fog; the frame's grade and everything else keep the water.");
                }
            }

            // A neck is given no sway, so it reads zero and holds still: it is the join the swaying
            // and pulsing tissue holds still at (TheatreBody.shader, SwayHold).
            if (_sway)
            {
                _block.SetFloat(SwayId, 1f);
                _block.SetVector(SwayPartId, sway);
            }

            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// One part's beat under the sway dial, as <c>_SwayPart</c>: what moves, the phase in cycles
        /// and the cycles per second.
        /// </summary>
        /// <remarks>
        /// The period and the phase are the creature's, from its id (<c>Body.Own</c>), so neighbours
        /// are out of step while a body's parts keep one beat; each joint between a part and the root
        /// puts it <see cref="SwayLagPerJoint"/> of a cycle behind, so the body moves as one with a
        /// lag outward. Nothing moves under the raw shapes, which draw the colliders, and a corpse
        /// holds still, since a body at rest is meant to look alive and a dead one is not.
        /// </remarks>
        private Vector4 SwayOf(Body body, Phenotype phenotype, int part, string cellType, bool corpse)
        {
            if (RawShapes || corpse || part < 0 || part >= phenotype.PartCount) return Vector4.zero;

            float kind, fastest, slowest;

            if (cellType == CellTypeIds.Photosynthetic)
            {
                kind = SwayKindLeaf;
                fastest = LeafSwayFastest;
                slowest = LeafSwaySlowest;
            }
            else if (cellType == CellTypeIds.Buoyancy)
            {
                kind = SwayKindFloat;
                fastest = FloatPulseFastest;
                slowest = FloatPulseSlowest;
            }
            else
            {
                return Vector4.zero;
            }

            float period = Mathf.Lerp(fastest, slowest, Fraction(body.Own * SwayPeriodMix + SwayPeriodOffset));
            float phase = Fraction(body.Own * SwayPhaseMix + SwayPhaseOffset + SwayLagPerJoint * JointsToRoot(phenotype, part));

            return new Vector4(kind, phase, 1f / period, 0f);
        }

        /// <summary>
        /// Gives each marine producer blade of a body the water's flow past it, for the bend with the current
        /// (<c>EVOSIM_THEATRE_ALGA_BEND</c>), as one number in <c>_SwayPart.w</c>. Does nothing with the dial off.
        /// </summary>
        /// <param name="id">The creature, as in <see cref="Paint"/>.</param>
        /// <param name="root">The body's root, as in <see cref="Paint"/>; a body painted under another root is left alone.</param>
        /// <param name="flow">
        /// Per part, the water's velocity past that part in the world's frame, m/s: the water's own velocity where
        /// the part is less the part's own velocity. Only the horizontal components are read.
        /// </param>
        /// <param name="count">How many entries of <paramref name="flow"/> are valid.</param>
        /// <remarks>
        /// Called by the view once a frame after the pose, because the water changes with the world's clock and the
        /// blade's velocity with its pose, and a paint is on a budget. A blade is rewritten only when its code
        /// changes (the code is a step of 1/255 of the speed range and a degree), so a still flow costs nothing. A blade
        /// not yet painted is skipped until it is (<see cref="Paint"/> keeps the code through a repaint).
        /// </remarks>
        public void Flow(long id, Transform root, Vector3[] flow, int count)
        {
            if (!AlgaBendOn || root == null || flow == null) return;
            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root || body.SwayWas == null) return;

            if (_block == null) _block = new MaterialPropertyBlock();
            if (body.FlowCode == null) body.FlowCode = new float[body.Renderers.Length];

            for (int i = 0; i < body.Renderers.Length && i < body.SwayWas.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                Vector4 sway = body.SwayWas[i];

                if (renderer == null || Mathf.Abs(sway.x - SwayKindLeaf) >= 0.5f) continue;

                int part = body.Part[i];
                float code = part >= 0 && part < count ? FlowCodeOf(flow[part]) : 0f;

                if (code == body.FlowCode[i]) continue;

                body.FlowCode[i] = code;
                sway.w = code;
                body.SwayWas[i] = sway;

                renderer.GetPropertyBlock(_block);
                _block.SetVector(SwayPartId, sway);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// A flow packed for <c>_SwayPart.w</c>: 1 + the bearing in degrees (from +x toward +z, the horizontal plane)
        /// + 360 times the speed step, the speed as 1 - exp(-speed / <see cref="AlgaBendSpeedScale"/>) in 255
        /// steps; zero for no flow (a speed that rounds to the first step). The shader's <c>AlgaBendFlow</c> reads it.
        /// All integers up to 91,801, so exact in a float.
        /// </summary>
        private static float FlowCodeOf(Vector3 flow)
        {
            float speed = Mathf.Sqrt(flow.x * flow.x + flow.z * flow.z);
            if (!(speed > 1e-4f) || float.IsInfinity(speed)) return 0f;

            int step = Mathf.Clamp(
                Mathf.RoundToInt((1f - Mathf.Exp(-speed / AlgaBendSpeedScale)) * AlgaBendSpeedSteps), 0, AlgaBendSpeedSteps);
            if (step <= 0) return 0f;

            int bearing = Mathf.RoundToInt(Mathf.Atan2(flow.z, flow.x) * (AlgaBendAzimuthSteps / (2f * Mathf.PI)));
            bearing = ((bearing % AlgaBendAzimuthSteps) + AlgaBendAzimuthSteps) % AlgaBendAzimuthSteps;

            return 1f + bearing + AlgaBendAzimuthSteps * step;
        }

        /// <summary>How many joints lie between a part and its body's root, guarded against a cycle.</summary>
        private static int JointsToRoot(Phenotype phenotype, int part)
        {
            int joints = 0;

            for (int p = phenotype.Parts[part].ParentIndex;
                 p >= 0 && p < phenotype.PartCount && joints < phenotype.PartCount;
                 p = phenotype.Parts[p].ParentIndex)
            {
                joints++;
            }

            return joints;
        }

        /// <summary>A number's fractional part, in [0, 1).</summary>
        private static float Fraction(float x) => x - Mathf.Floor(x);

        /// <summary>A tissue's own colour, turned by lineage and greyed out of focus or dead, as the guild is.</summary>
        private static Color Tissue(Color colour, float turn, bool grey, bool hueOnly = false)
        {
            Color c = Turned(colour, turn, hueOnly);
            return grey ? Grey(c) : c;
        }

        /// <summary>
        /// The guild's hue at a capped saturation and a fixed lightness.
        /// </summary>
        /// <remarks>
        /// Lightness is set rather than capped, because the point is a body dark enough for a rim
        /// to read against; saturation is capped rather than set, so a colour that was already
        /// muted stays muted instead of being pushed up to the cap.
        /// </remarks>
        public static Color Muted(Color colour, float saturationCap, float lightness)
        {
            Color.RGBToHSV(colour, out float h, out float s, out float v);

            // Unity gives HSV, and HSL's lightness is what the reading asks for. The two agree on
            // hue, and for the fully saturated end of a guild colour the conversion below is the
            // standard one: L = V (1 - S/2), and the saturation that keeps that lightness.
            float l = v * (1f - 0.5f * s);
            float sl = l <= 0f || l >= 1f ? 0f : (v - l) / Mathf.Min(l, 1f - l);

            sl = Mathf.Min(sl, saturationCap);
            l = Mathf.Clamp01(lightness);

            float vOut = l + sl * Mathf.Min(l, 1f - l);
            float sOut = vOut <= 0f ? 0f : 2f * (1f - l / vOut);

            return Color.HSVToRGB(h, Mathf.Clamp01(sOut), Mathf.Clamp01(vOut));
        }

        /// <summary>
        /// One creature's carve seed, in [0, 1).
        /// </summary>
        /// <remarks>
        /// Mixed rather than taken raw, and fractional rather than large, and both for the same
        /// reason: the shader turns this into a lattice offset with a <c>frac</c>, and a float
        /// carrying four digits before the point has only three left after it. Consecutive ids
        /// would then land on the same offset and a cohort of siblings would wear one carving.
        /// The multiplier is Knuth's, the odd integer nearest 2^32 over the golden ratio.
        /// </remarks>
        /// <summary>A part's own seed: the creature's, turned by the part's index.</summary>
        private static float OwnOf(Body body, int part)
        {
            float own = body.Own + 0.38196601f * Mathf.Max(0, part);
            return own - Mathf.Floor(own);
        }

        // The skin's copies of the two seeds (TheatreOneSkin, through CarveSeedOf and IndividualOf). The painted seeds stay
        // inline in OwnOf, Paint and PartSeed(Body, int): routing them through these helpers moved a seed by a bit under
        // Unity's Mono, and the bloom carried the change into isolated water pixels of every dials-off frame (b44's probes).
        // A skin's seed may therefore differ from its parts' in the last bit, which nothing can see.

        /// <summary>A part's own seed from its creature's (<see cref="OwnOf"/>), the shader's <c>_Individual</c>.</summary>
        private static float PartOwn(float bodyOwn, int part)
        {
            float own = bodyOwn + 0.38196601f * Mathf.Max(0, part);
            return own - Mathf.Floor(own);
        }

        /// <summary>A part's carve seed from its body's plan seed, the shader's <c>_Carve.x</c>.</summary>
        private static float PartSeed(float bodySeed, int part)
        {
            float seed = bodySeed + 0.61803399f * Mathf.Max(0, part);
            seed -= Mathf.Floor(seed);
            return seed;
        }

        private static float SeedOf(long id)
        {
            long mixed = unchecked(id * 2654435761L);

            return ((mixed >> 13) & 0xFFFF) / 65536f;
        }

        /// <summary>
        /// The skin genes, theatre-only (the owner's ruling of 2026-09-16, the first of the two
        /// versions): the seed every per-body choice is drawn from (the carve's offsets, which
        /// end tapers, which way a part leans, TheatrePalette.Character and TheatreBody.shader's
        /// ShapeBox) comes from the body plan rather than from the creature's id, so a child
        /// that inherits its parent's plan wears its parent's skin and a cousin two mutations
        /// away wears a cousin's. Hashed from what a plan is: the part count, and each part's
        /// guild, parent, shape, mirroring and its shape's proportions in coarse steps, so a
        /// growth or a small size mutation keeps the family look and a new part or a new guild
        /// changes it. Nothing here reaches the simulation; the genome's own version, a few
        /// neutral numbers the shader reads, is queued for a round boundary.
        /// </summary>
        /// <summary>A string's hash that is the same in every process, which string.GetHashCode is not.</summary>
        private static long Stable(string text)
        {
            unchecked
            {
                long h = (long)14695981039346656037UL;
                if (text != null) foreach (char c in text) h = (h ^ c) * 1099511628211L;
                return h;
            }
        }

        private static float SeedOf(Phenotype phenotype, long id)
        {
            if (phenotype == null || phenotype.PartCount == 0) return SeedOf(id);

            unchecked
            {
                long h = 1469598103934665603L ^ phenotype.PartCount;

                for (int i = 0; i < phenotype.PartCount; i++)
                {
                    PhenotypePart part = phenotype.Parts[i];
                    Float3 he = part.HalfExtents;

                    float longest = Mathf.Max(he.X, Mathf.Max(he.Y, he.Z));
                    float shortest = Mathf.Max(1e-4f, Mathf.Min(he.X, Mathf.Min(he.Y, he.Z)));
                    int proportion = Mathf.RoundToInt(2f * Mathf.Log(longest / shortest, 2f));

                    h = (h ^ part.ParentIndex) * 1099511628211L;
                    h = (h ^ Stable(part.CellTypeId)) * 1099511628211L;
                    h = (h ^ Stable(part.ShapeId)) * 1099511628211L;
                    h = (h ^ (part.Mirrored ? 1 : 0)) * 1099511628211L;
                    h = (h ^ proportion) * 1099511628211L;
                }

                return ((h >> 17) & 0xFFFF) / 65536f;
            }
        }

        private Body Gather(long id, Transform root, Phenotype phenotype)
        {
            _scratch.Clear();
            root.GetComponentsInChildren<MeshRenderer>(true, _scratch);
            if (TheatreMouthMarks.Enabled) TheatreMouthMarks.Strip(_scratch);

            var body = new Body
            {
                Root = root,
                Renderers = _scratch.ToArray(),
                Part = new int[_scratch.Count],
                Seed = SeedOf(phenotype, id),
                Own = SeedOf(id),
                PinchA = new Vector4[_scratch.Count],
                PinchB = new Vector4[_scratch.Count],
                Leaf = new Vector4[_scratch.Count],
                Shape = new Vector3[_scratch.Count],
                Wrote = new Vector3[_scratch.Count],
                Merged = new bool[_scratch.Count],
                Ramp = _ramp ? new Vector4[_scratch.Count] : null,
                Velvet = _velvet ? new Vector4[_scratch.Count] : null,
                LinkAxis = _link ? new Vector4[_scratch.Count] : null,
                LinkShape = _link ? new Vector4[_scratch.Count] : null,
            };

            for (int i = 0; i < _scratch.Count; i++)
            {
                body.Part[i] = PartIndexOf(_scratch[i].transform.parent);
                body.Shape[i] = Vector3.one;
            }

            Dress(body, phenotype);

            return body;
        }

        /// <summary>
        /// The carve's character for one part: its seed, how lobed it is, how wrinkled, and how
        /// much of the dial it takes at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Lightly by guild, as the shape asks.</b> A producer is more lobed, because a leaf
        /// is a thing with a shape rather than a thing with a texture; a stomach is more
        /// wrinkled; a structural part is smoother, because it is the strut and not the organ.
        /// The differences are small and none of them is what tells a viewer which guild a part
        /// is: that is the rim's colour, and giving the shape a second vote would make a badly
        /// lit producer read as a stomach.
        /// </para>
        /// <para>
        /// <b>The seed carries the part index as well as the body's id.</b> The field is read in
        /// each part's own object units, so two parts of one body with the same size and one seed
        /// would come out as two copies of one carving. An irrational step, wrapped back into
        /// [0, 1), keeps them apart while leaving the whole body drawn from one creature's
        /// number; the golden ratio is the step that fills the interval most evenly for any
        /// number of parts.
        /// </para>
        /// </remarks>
        /// <summary>
        /// How much light a guild's tissue lets through when backlit (the design pass's R2,
        /// 2026-09-16): a producer is a leaf and glows, an eater is a stomach wall and does not,
        /// a strut is between. The one place the guild reaches a body's face, and it reaches it
        /// as light, not as paint.
        /// </summary>
        /// <summary>
        /// The wet sheen by guild: a gut wall glossy, a leaf matte, a strut between. The
        /// absorptive tissue's own surface (the owner, 2026-09-16), a highlight and never a
        /// drawn organ.
        /// </summary>
        private static float SheenOf(string cellTypeId) => TheatreTissue.Of(cellTypeId).Sheen;

        private static float TransmissionOf(string cellTypeId) => TheatreTissue.Of(cellTypeId).Transmission;

        /// <summary>
        /// A tissue's chromatophore strength, <c>_Chromatophores</c>: one for the consumer (a biter) and the link, the
        /// share <see cref="ChromatophoresAbsorber"/> or <see cref="ChromatophoresStructural"/> for an absorber or a
        /// structural part, and zero for a leaf and for the nerve, float and holdfast, which the brief does not name.
        /// </summary>
        private static float ChromatophoresOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Consumer || cellTypeId == CellTypeIds.Link) return 1f;
            if (cellTypeId == CellTypeIds.Absorptive) return ChromatophoresAbsorber;
            if (cellTypeId == CellTypeIds.Structural) return ChromatophoresStructural;
            return 0f;
        }

        /// <summary>
        /// A tissue's iridescence strength, <c>_Iridescence</c>: one for the consumer (a biter) and the link,
        /// <see cref="IridescenceFloat"/> for the buoyancy (float) type, <see cref="IridescenceDry"/> for an absorber and
        /// a structural part, and zero for a leaf, a nerve and a holdfast.
        /// </summary>
        private static float IridescenceOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Consumer || cellTypeId == CellTypeIds.Link) return 1f;
            if (cellTypeId == CellTypeIds.Buoyancy) return IridescenceFloat;
            if (cellTypeId == CellTypeIds.Absorptive || cellTypeId == CellTypeIds.Structural) return IridescenceDry;
            return 0f;
        }

        /// <summary>
        /// What the marine producers' dial does to one part (<see cref="AlgaDressOf"/>). Its default is a part the dial
        /// leaves alone.
        /// </summary>
        private struct AlgaDress
        {
            /// <summary>True when <see cref="Guild"/> replaces the guild's colour: a producer or a gas bladder.</summary>
            public bool Recoloured;

            /// <summary>The colour that replaces the guild's, before the lineage's turn and the grey.</summary>
            public Color Guild;

            /// <summary>The shader's <c>_Alga</c>: one on a living producer, <see cref="AlgaCorpse"/> on a dead one, else zero.</summary>
            public float Form;

            /// <summary>
            /// The shader's <c>_AlgaDeep</c> (w one when the depth tint is on) and <c>_AlgaShallow</c> (rgb the tint's
            /// shallow end, w one on a frond, a free-drifting blade, and zero on every other part: nothing else reads it).
            /// </summary>
            public Vector4 Deep;

            public Vector4 Shallow;

            /// <summary>A stalk part a blade hangs from, drawn darker (<see cref="AlgaStipeShade"/>).</summary>
            public bool Stipe;

            /// <summary>A float next to a blade, drawn as a gas bladder: olive and glossy (<see cref="AlgaBladderSheen"/>).</summary>
            public bool Bladder;
        }

        /// <summary>
        /// One part's dress under the marine producers' dial: a producer's colour and form, a stipe's shade, a gas
        /// bladder's colour and gloss, or nothing. Called only while the dial is on, for a part the paint knows.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A producer</b> (photosynthetic): its place along the family is its proportions' alone
        /// (<see cref="AlgaPositionOf"/>), with nothing of <c>_Individual</c> or the seed in it, so a family of one plan
        /// is one colour and a mutation of shape moves it smoothly. Its colour there is <see cref="AlgaColour"/>, and the
        /// depth's tint goes to the shader as two multipliers of equal red, green and blue, <see cref="AlgaDeepShade"/>
        /// at the floor and <see cref="AlgaShallowShade"/> at the waterline, applied to the body, the rim and the light
        /// through alike: the depth darkens and never moves the hue. A greyed body (out of focus or a
        /// corpse) takes no tint, so its grey stays grey; a corpse keeps the form at <see cref="AlgaCorpse"/>.
        /// </para>
        /// <para>
        /// <b>A stipe</b>: a structural part that hangs a blade by a chain of only-child structural parts, at most <see cref="AlgaStipeChain"/> long: drawn at <see cref="AlgaStipeShade"/> of its brightness.
        /// Colour only; it is not drawn rounder (that is a shape change the palette does not make).
        /// </para>
        /// <para>
        /// <b>A gas bladder</b>: a float (buoyancy) whose parent or child is a blade: the olive anchor's colour and at
        /// least <see cref="AlgaBladderSheen"/> of sheen. Colour and gloss only, no shape change.
        /// </para>
        /// </remarks>
        private AlgaDress AlgaDressOf(Phenotype phenotype, int part, float turn, bool grey, bool dead)
        {
            AlgaDress dress = default;
            PhenotypePart p = phenotype.Parts[part];
            Color anchor = ColourOf(CellTypeIds.Photosynthetic);

            if (p.CellTypeId == CellTypeIds.Photosynthetic)
            {
                float x = AlgaPositionOf(p);

                // A frond: a blade of a body that has no anchoring part, which drifts free. Its colour, and the flag the
                // shader reads (Shallow.w), which a greyed or dead one keeps too, since it keeps its form.
                bool frond = IsBlade(p) && !HasAnchoring(phenotype);
                // FROND begin
                // A swimmer's leaf (EVOSIM_THEATRE_FROND_SWIMMER): on a body with a link part the blade is a leaf sheet inside its
                // own box, never a free frond.
                if (FrondSwimmerSheet && frond && HasLink(phenotype)) frond = false;
                // FROND end

                dress.Recoloured = true;
                dress.Guild = frond ? AlgaFrondColour(anchor, x) : AlgaColour(anchor, x);
                dress.Form = dead ? AlgaCorpse : 1f;

                if (!grey)
                {
                    dress.Deep = new Vector4(AlgaDeepShade, AlgaDeepShade, AlgaDeepShade, 1f);
                    dress.Shallow = new Vector4(AlgaShallowShade, AlgaShallowShade, AlgaShallowShade, frond ? 1f : 0f);
                }
                else if (frond)
                {
                    dress.Shallow = new Vector4(0f, 0f, 0f, 1f);
                }

                return dress;
            }

            if (p.CellTypeId == CellTypeIds.Buoyancy)
            {
                if (NextToBlade(phenotype, part))
                {
                    dress.Recoloured = true;
                    dress.Bladder = true;
                    dress.Guild = AlgaColour(anchor, AlgaBladderPosition);
                }

                return dress;
            }

            if (p.CellTypeId != CellTypeIds.Anchoring) dress.Stipe = StipeOf(phenotype, part);

            return dress;
        }

        /// <summary>
        /// A producer's place along the family before its depth, in [0, 1], from its half extents alone: its form by
        /// length over width (the shader's <c>AlgaFormOf</c>) and how fleshy it is (thinnest over the width). A blade of
        /// the round form (<see cref="AlgaRoundOf"/>) moves to <see cref="AlgaPositionRound"/>, the ochre of a fan.
        /// </summary>
        private static float AlgaPositionOf(PhenotypePart part)
        {
            Vector3 h = Abs(part.HalfExtents.ToVector3());
            float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
            float thinnest = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            float width = Mathf.Max(1e-6f, h.x + h.y + h.z - longest - thinnest);

            float form = AlgaFormOf(longest / width);
            float flesh = Smooth(AlgaFleshLow, AlgaFleshHigh, thinnest / width);

            float x = Mathf.Clamp01(Mathf.Lerp(AlgaPositionSheet, AlgaPositionStrap, form) + AlgaPositionFlesh * flesh);

            // The round form of a blade, by its length over width alone, so a mutation of shape moves it smoothly.
            if (IsBlade(part)) x = Mathf.Lerp(x, AlgaPositionRound, AlgaRoundOf(longest / width));

            return x;
        }

        /// <summary>How round a blade is by its length over width, 1 under <see cref="AlgaRoundLow"/> to 0 over <see cref="AlgaRoundHigh"/>: the shader's <c>AlgaRoundOfAspect</c>.</summary>
        private static float AlgaRoundOf(float aspect) => 1f - Smooth(AlgaRoundLow, AlgaRoundHigh, aspect);

        /// <summary>The form by length over width, 0 a sheet and 1 a strap: the shader's <c>AlgaFormOf(aspect).w</c>.</summary>
        private static float AlgaFormOf(float aspect)
        {
            float sheet = 1f - Smooth(AlgaSheetLow, AlgaSheetHigh, aspect);
            float strap = Smooth(AlgaStrapLow, AlgaStrapHigh, aspect);
            return 0.5f * (1f - sheet) + 0.5f * strap;
        }

        /// <summary>HLSL's smoothstep, which Unity's <see cref="Mathf.SmoothStep"/> is not.</summary>
        private static float Smooth(float low, float high, float x)
        {
            float t = Mathf.Clamp01((x - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The family's colour at a place along it, in sRGB: the four anchors joined by straight lines.</summary>
        private static Color AlgaFamily(float x)
        {
            float u = 3f * Mathf.Clamp01(x);

            if (u < 1f) return Color.Lerp(AlgaGreen, AlgaOlive, u);
            if (u < 2f) return Color.Lerp(AlgaOlive, AlgaGolden, u - 1f);
            return Color.Lerp(AlgaGolden, AlgaRed, u - 2f);
        }

        /// <summary>
        /// A producer's colour at a place along the family, anchored on the guild's: the family's hue and saturation,
        /// and its value scaled by the guild's over <see cref="AlgaGuildValue"/>, so with today's guild green it is the
        /// family's colour itself.
        /// </summary>
        private static Color AlgaColour(Color guild, float x)
        {
            Color.RGBToHSV(AlgaFamily(x), out float h, out float s, out float v);
            Color.RGBToHSV(guild, out _, out _, out float guildValue);

            Color c = Color.HSVToRGB(h, s, Mathf.Clamp01(v * guildValue / AlgaGuildValue));
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// A frond's guild colour: the family's hue at <see cref="AlgaFrondPositionLow"/> to <see cref="AlgaFrondPositionHigh"/>
        /// by the blade's own place <paramref name="x"/> (so it moves smoothly with the proportions), at HSV saturation
        /// <see cref="AlgaFrondGuildSaturation"/> and value <see cref="AlgaFrondGuildValue"/> scaled by the guild's over
        /// <see cref="AlgaGuildValue"/> as <see cref="AlgaColour"/> scales it.
        /// </summary>
        private static Color AlgaFrondColour(Color guild, float x)
        {
            Color.RGBToHSV(AlgaFamily(Mathf.Lerp(AlgaFrondPositionLow, AlgaFrondPositionHigh, Mathf.Clamp01(x))), out float h, out _, out _);
            Color.RGBToHSV(guild, out _, out _, out float guildValue);

            Color c = Color.HSVToRGB(h, AlgaFrondGuildSaturation, Mathf.Clamp01(AlgaFrondGuildValue * guildValue / AlgaGuildValue));
            c.a = 1f;
            return c;
        }

        // FROND begin
        /// <summary>
        /// A swimmer's leaf (<c>EVOSIM_THEATRE_FROND_SWIMMER</c>, 0 off by default, 1 on), read once: the frond rule above draws
        /// every blade of a body without an anchoring part as a free-drifting frond, which also catches a jointed swimmer that
        /// carries a photosynthetic part. On, a body with a link part draws its blade as a leaf sheet inside its own box.
        /// </summary>
        private static readonly bool FrondSwimmerSheet = TheatreSkin.Dial("EVOSIM_THEATRE_FROND_SWIMMER", 0f, 0f, 1f) >= 0.5f;

        /// <summary>Whether any part of a body is a link (a jointed swimmer's limb).</summary>
        private static bool HasLink(Phenotype phenotype)
        {
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].CellTypeId == CellTypeIds.Link) return true;
            }

            return false;
        }
        // FROND end
        /// <summary>Whether any part of a body is an anchoring one (a holdfast): a body without one drifts free.</summary>
        private static bool HasAnchoring(Phenotype phenotype)
        {
            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].CellTypeId == CellTypeIds.Anchoring) return true;
            }

            return false;
        }

        /// <summary>
        /// A rim or light-through colour in a producer's own hue and richness: the guild colour's hue, its HSV saturation
        /// times <see cref="AlgaRimSaturationGain"/> (at most <see cref="AlgaRimSaturationCap"/>, so a grey guild stays
        /// grey) and the colour's value times <see cref="AlgaRimValue"/>. Only for a part the producers' dial recoloured.
        /// </summary>
        private static Color AlgaRich(Color colour, Color guild)
        {
            Color.RGBToHSV(colour, out _, out _, out float v);
            Color.RGBToHSV(guild, out float gh, out float gs, out _);

            Color c = Color.HSVToRGB(gh, Mathf.Min(AlgaRimSaturationCap, gs * AlgaRimSaturationGain), Mathf.Clamp01(v * AlgaRimValue));
            c.a = colour.a;
            return c;
        }

        // THALLI begin
        /// <summary>Ulva's own colour (sRGB): bright lime.</summary>
        private static readonly Color ThalliUlvaHue = new Color(0.56f, 0.80f, 0.24f);

        /// <summary>Padina's own colour (sRGB): pale tan, near white with the lime it carries.</summary>
        private static readonly Color ThalliPadinaHue = new Color(0.80f, 0.74f, 0.60f);

        /// <summary>Codium's own colour (sRGB): dark olive.</summary>
        private static readonly Color ThalliCodiumHue = new Color(0.22f, 0.28f, 0.12f);

        /// <summary>The light through each form's tissue (sRGB), in the same order.</summary>
        private static readonly Color ThalliUlvaThrough = new Color(0.70f, 0.92f, 0.35f);
        private static readonly Color ThalliPadinaThrough = new Color(0.85f, 0.80f, 0.65f);
        private static readonly Color ThalliCodiumThrough = new Color(0.30f, 0.36f, 0.16f);

        /// <summary>
        /// The share of the clade's producer colour (the body colour and light through the paint gave the part) in a thallus's
        /// colour; the rest is the form's own.
        /// </summary>
        private const float ThalliCladeShare = 0.25f;

        /// <summary>
        /// Swaps a compact producer's blade for a thallus (<c>EVOSIM_THEATRE_THALLI</c>; <see cref="TheatreMeshes.Thallus"/>), and
        /// records which form it took. Only a part the blade path drew as a leaf, photosynthetic, on a body with no link part
        /// (a swimmer's frond keeps its blade), never under the raw shapes. A part the compact test refuses, or one the full cache
        /// cannot take, keeps its blade.
        /// </summary>
        private void DressThallus(Body body, int i, Phenotype phenotype, int part, MeshRenderer renderer, MeshFilter filter)
        {
            if (body.Thallus == null || body.Thallus.Length != body.Renderers.Length) body.Thallus = new byte[body.Renderers.Length];
            body.Thallus[i] = 0;

            if (RawShapes || filter == null) return;
            if (!TheatreMeshes.IsLamina(filter.sharedMesh) && !TheatreMeshes.IsThallus(filter.sharedMesh)) return;

            PhenotypePart p = phenotype.Parts[part];
            if (p.CellTypeId != CellTypeIds.Photosynthetic || HasLink(phenotype)) return;

            uint hash = TheatreMeshes.ThallusHash(body.Seed, part, Abs(p.HalfExtents.ToVector3()));
            Mesh thallus = TheatreMeshes.Thallus(renderer.transform.localScale, body.Leaf[i], hash, out TheatreMeshes.ThallusForm form);

            if (thallus == null) return;

            filter.sharedMesh = thallus;
            renderer.ResetLocalBounds();
            body.Thallus[i] = (byte)(1 + (int)form);

            TheatreSkin.EnsureThalliKeyword();
            TheatreMeshes.CountThallus(body.Root != null ? body.Root.GetHashCode() : 0, part, form);
        }

        /// <summary>
        /// A thallus's colour, written over the paint's: the form's own hue at the paint's brightness, blended toward the clade's
        /// producer colour by <see cref="ThalliCladeShare"/>, and its light through the same way. Linear, as the block holds
        /// colours. Not for a greyed body or a corpse, which keep the paint's grey.
        /// </summary>
        private void PaintThallus(MeshRenderer renderer, int form, float brightness)
        {
            Color hue = form == 0 ? ThalliUlvaHue : (form == 1 ? ThalliPadinaHue : ThalliCodiumHue);
            Color glow = form == 0 ? ThalliUlvaThrough : (form == 1 ? ThalliPadinaThrough : ThalliCodiumThrough);
            Color own = hue.linear * brightness;
            Color ownThrough = glow.linear;

            renderer.GetPropertyBlock(_block);

            Vector4 clade = _block.GetVector(BaseColorId);
            Vector4 cladeThrough = _block.GetVector(TransTintId);

            Vector4 body = Vector4.Lerp(new Vector4(own.r, own.g, own.b, 1f), clade, ThalliCladeShare);
            Vector4 through = Vector4.Lerp(new Vector4(ownThrough.r, ownThrough.g, ownThrough.b, 1f), cladeThrough, ThalliCladeShare);
            body.w = 1f;
            through.w = 1f;

            _block.SetVector(BaseColorId, body);
            _block.SetVector(ColorId, body);
            _block.SetVector(TransTintId, through);

            renderer.SetPropertyBlock(_block);
        }

        // THALLI end
        /// <summary>Whether a part is a blade: a producer drawn on the lamina path.</summary>
        private static bool IsBlade(PhenotypePart part) =>
            part.CellTypeId == CellTypeIds.Photosynthetic && DrawnAsLeaf(part, Abs(part.HalfExtents.ToVector3()));

        /// <summary>Whether a part's parent or one of its children is a blade.</summary>
        private static bool NextToBlade(Phenotype phenotype, int part)
        {
            int parent = phenotype.Parts[part].ParentIndex;
            if (parent >= 0 && parent < phenotype.PartCount && IsBlade(phenotype.Parts[parent])) return true;

            for (int c = 0; c < phenotype.PartCount; c++)
            {
                if (phenotype.Parts[c].ParentIndex == part && IsBlade(phenotype.Parts[c])) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a part is a stipe: it is structural tissue with one child, and that child is a blade or, down a chain
        /// of such structural parts at most <see cref="AlgaStipeChain"/> long, reaches one. Any other tissue (a producer,
        /// a float, a holdfast, a mouth, an absorber, a jointed link) is never a stipe, and a part with several
        /// children is a body's hub, not a stalk.
        /// </summary>
        private static bool StipeOf(Phenotype phenotype, int part)
        {
            int at = part;

            for (int step = 0; step < AlgaStipeChain; step++)
            {
                if (phenotype.Parts[at].CellTypeId != CellTypeIds.Structural) return false;

                int only = -1;
                for (int c = 0; c < phenotype.PartCount; c++)
                {
                    if (phenotype.Parts[c].ParentIndex != at) continue;
                    if (only >= 0) return false;
                    only = c;
                }

                if (only < 0) return false;
                if (IsBlade(phenotype.Parts[only])) return true;

                at = only;
            }

            return false;
        }

        /// <summary>
        /// A tissue's share of the skin pattern, the <c>y</c> of <c>_Pattern</c>: none on a leaf (a blade's own veins and
        /// tone carry it), <see cref="PatternSmaller"/> on an absorber and a structural part, one on every other.
        /// </summary>
        private static float PatternStrengthOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Photosynthetic) return 0f;
            if (cellTypeId == CellTypeIds.Absorptive || cellTypeId == CellTypeIds.Structural) return PatternSmaller;
            return 1f;
        }

        /// <summary>
        /// The pattern for a body (a library tile and its own placement): the registered genome's key when a caller said which genome the id was grown
        /// from (<see cref="TheatrePattern.Register"/>), else the key of its parts' cell types, made once and kept on
        /// the body.
        /// </summary>
        private static TheatrePattern.Solved PatternOf(long id, Body body, Phenotype phenotype)
        {
            if (!TheatrePattern.TryRegistered(id, out TheatrePattern.Key key))
            {
                if (body.PatternFallback == null) body.PatternFallback = TheatrePattern.FallbackKey(phenotype);
                key = body.PatternFallback.Value;
            }

            // The genome-to-skin map: the regime and the scale from the body's vector, in place of its investment and margin.
            if (TheatreSkinMap.Enabled && TheatreSkinMap.TryGet(id, out TheatreSkinMap.Entry skin))
            {
                return TheatrePattern.SolvedForMap(key, TheatreSkinMap.RegimeAlong(skin), TheatreSkinMap.ScaleAlong(skin));
            }

            return TheatrePattern.SolvedFor(key);
        }

        // skin2:begin
        /// <summary>The least the clade's first fraction is written as on a body in colour; zero means a greyed body, a corpse or a neck.</summary>
        private const float Skin2CladeFloor = 1e-4f;

        /// <summary>
        /// A body's clade for <c>EVOSIM_THEATRE_SKIN2</c>: the founder's number when a caller has said which it is
        /// (<see cref="LineageOf"/>, which the safari sets to its clade's founder; odd, so the two cannot meet), else the hash
        /// of the pattern key's cell types, which kin share (even). Without a caller that sets <see cref="LineageOf"/>, a clade
        /// is a body plan's cell-type multiset, so two clades of one plan wear one pattern.
        /// </summary>
        private ulong CladeOf(long id, TheatrePattern.Key key)
        {
            if (LineageOf != null)
            {
                long founder = LineageOf(id);
                if (founder >= 0) return unchecked((ulong)founder) * 2UL + 1UL;
            }

            return (ulong)key.Types * 2UL;
        }

        /// <summary>
        /// Under <c>EVOSIM_THEATRE_SKIN2</c>, after a part's own paint: the clade's two fractions into <c>_PatternShift</c>'s z and
        /// w (z only on a body in colour, so the margin and the creases leave a greyed body, a corpse and a neck alone), and,
        /// when the skin pattern was drawn for this body, the pattern of its clade in place of the one its investment gave:
        /// the tile, strength, turn and cover in <c>_Pattern</c>, the offset in <c>_PatternShift</c>'s x and y, and the texture.
        /// The one skin takes the largest part's block, so it wears what that part was given.
        /// </summary>
        private void PaintSkin2(
            Renderer renderer, long id, Body body, Phenotype phenotype, int part, string cellType, bool coloured,
            TheatrePattern.Solved patterned)
        {
            TheatreSkin.EnsureSkin2Keyword();

            if (!TheatrePattern.TryRegistered(id, out TheatrePattern.Key key))
            {
                if (body.PatternFallback == null) body.PatternFallback = TheatrePattern.FallbackKey(phenotype);
                key = body.PatternFallback.Value;
            }

            ulong clade = CladeOf(id, key);
            TheatrePattern.CladeStyle style = TheatrePattern.StyleOf(clade);

            renderer.GetPropertyBlock(_block);
            Vector4 shift = _block.GetVector(PatternShiftId);
            shift.z = coloured ? Mathf.Max(style.Hash1, Skin2CladeFloor) : 0f;
            shift.w = style.Hash2;

            // The genome-to-skin map replaces the clade's pattern with the vector's regime and scale, at the tissue's own strength.
            TheatreSkinMap.Entry mapped = null;

            if (patterned != null && TheatreSkinMap.Enabled && TheatreSkinMap.TryGet(id, out mapped))
            {
                TheatrePattern.Solved fromMap = TheatrePattern.SolvedForMap(
                    key, TheatreSkinMap.RegimeAlong(mapped), TheatreSkinMap.ScaleAlong(mapped));

                _block.SetVector(PatternId, new Vector4(
                    PatternTileOf(phenotype) * fromMap.ScaleMultiple, TheatreSkinMap.MazeStrength(mapped, PatternStrengthOf(cellType)),
                    fromMap.Rotation, fromMap.Cover));

                shift.x = Mathf.Repeat(fromMap.OffsetU + 0.61803399f * Mathf.Max(0, part), 1f);
                shift.y = Mathf.Repeat(fromMap.OffsetV + 0.41421356f * Mathf.Max(0, part), 1f);
                if (fromMap.Texture != null) _block.SetTexture(PatternTexId, fromMap.Texture);
            }
            else if (patterned != null)
            {
                TheatrePattern.Solved solved = TheatrePattern.SolvedForClade(key, clade, out style);

                _block.SetVector(PatternId, new Vector4(
                    PatternTileOf(phenotype) * solved.ScaleMultiple, PatternStrengthOf(cellType) * style.Strength,
                    solved.Rotation, solved.Cover));

                shift.x = Mathf.Repeat(solved.OffsetU + 0.61803399f * Mathf.Max(0, part), 1f);
                shift.y = Mathf.Repeat(solved.OffsetV + 0.41421356f * Mathf.Max(0, part), 1f);
                if (solved.Texture != null) _block.SetTexture(PatternTexId, solved.Texture);
            }

            _block.SetFloat(Skin2PartId, Skin2AnimalTissue(phenotype, part, cellType, coloured) ? 1f : 0f); // skin2parts
            _block.SetVector(PatternShiftId, shift);
            renderer.SetPropertyBlock(_block);
        }

        // skin2:end
        // skin2parts:begin
        /// <summary>
        /// Whether a part gets SKIN2's look as a part (the shader's <c>_Skin2Part</c>): a part in colour, drawn in animal
        /// tissue. Not a producer (photosynthetic), a holdfast (anchoring), a gas bladder (a float next to a blade), a stipe
        /// (a structural chain that hangs a blade), or a velvet absorber, which has its own sheen; and never a greyed body or a
        /// corpse, which <paramref name="coloured"/> is false for. A neck is not painted here at all. The shader leaves a
        /// blade and the one skin out as well.
        /// </summary>
        private bool Skin2AnimalTissue(Phenotype phenotype, int part, string cellType, bool coloured)
        {
            if (!coloured || part < 0 || part >= phenotype.PartCount) return false;
            if (cellType == CellTypeIds.Photosynthetic || cellType == CellTypeIds.Anchoring) return false;
            if (cellType == CellTypeIds.Buoyancy && NextToBlade(phenotype, part)) return false;
            if (cellType == CellTypeIds.Structural && StipeOf(phenotype, part)) return false;
            if (_velvet && cellType == CellTypeIds.Absorptive) return false;

            return true;
        }

        // skin2parts:end
        /// <summary>
        /// The pattern tile's repeat length in metres before the regime's multiple: <see cref="PatternTileShare"/> of the
        /// body's longest part, over every part, so all of a body's parts share one scale.
        /// </summary>
        private static float PatternTileOf(Phenotype phenotype)
        {
            float longest = 0f;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                Float3 he = phenotype.Parts[i].HalfExtents;
                longest = Mathf.Max(longest, 2f * Mathf.Max(he.X, Mathf.Max(he.Y, he.Z)));
            }

            return Mathf.Max(PatternTileMinimum, PatternTileShare * longest);
        }

        private static Vector4 Character(string cellTypeId, float seed)
        {
            TissueLook look = TheatreTissue.Of(cellTypeId);
            return new Vector4(seed, look.Lobes, look.Wrinkles, look.Take);
        }

        /// <summary>
        /// Puts the skin on a body the first time it is seen: the theatre's material on every
        /// renderer, the rounded mesh in place of the primitive, and a neck at every joint.
        /// </summary>
        /// <remarks>
        /// <b>The mesh is swapped, the transform is not.</b> <c>PhenotypeBuilder.VisualPlan</c>
        /// sizes each visual by its local scale and <c>ResizeColliderAndVisual</c> rewrites that
        /// scale every time a body grows; the generated meshes are unit-sized on exactly the
        /// conventions the engine's primitives use (a cube of side one, a sphere of diameter one,
        /// a cylinder of diameter one and height two), so replacing <c>sharedMesh</c> and touching
        /// nothing else means growth keeps working with no second copy of the arithmetic. That is
        /// also why the swap is by mesh name and not by part shape: the theatre must not build a
        /// second opinion about which primitive a shape draws with.
        /// </remarks>
        private void Dress(Body body, Phenotype phenotype)
        {
            if (Skin == null || !Skin.Ready) return;

            Material material = Skin.BodyMaterial;
            _capTaken.Clear();

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                // Dressed again after the raw shapes were shown: whatever a merge switched off
                // is switched on before this pass decides afresh.
                renderer.enabled = true;
                body.Merged[i] = false;

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null) continue;

                Mesh mesh = filter.sharedMesh;

                if (RawShapes)
                {
                    Mesh primitive = TheatreMeshes.PrimitiveFor(mesh);
                    if (primitive != null) { filter.sharedMesh = primitive; mesh = primitive; }
                }
                else
                {
                    // How cubic the part is, from the visual's own scale: the plan sizes a unit
                    // mesh by its local scale, so the three components are the part's sides.
                    Vector3 sides = renderer.transform.localScale;
                    float smallest = Mathf.Min(Mathf.Abs(sides.x), Mathf.Min(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));
                    float largest = Mathf.Max(Mathf.Abs(sides.x), Mathf.Max(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));
                    float cubicness = largest > 1e-6f ? smallest / largest : 1f;

                    // A leaf (TheatreMeshes.Lamina) is a box that is flat or photosynthetic
                    // (DrawnAsLeaf). Asked of the genome's part and the visual's mesh together: the
                    // engine's cube stands for a box only.
                    int leafPart = body.Part[i];
                    bool leaf = leafPart >= 0 && leafPart < phenotype.PartCount &&
                                (TheatreMeshes.IsPrimitiveCube(mesh) || TheatreMeshes.IsRoundedCube(mesh) ||
                                 TheatreMeshes.IsLamina(mesh) ||
                                 (TheatreNoBox.Enabled && TheatreNoBox.Is(mesh))) &&
                                DrawnAsLeaf(phenotype.Parts[leafPart], sides);

                    Mesh rounded = leaf ? TheatreMeshes.Lamina() : TheatreMeshes.RoundedFor(mesh, cubicness);
                    // NOBOX begin
                    // EVOSIM_THEATRE_NO_BOX: a box that is not a leaf is the superellipsoid, whichever cube it holds now
                    // (the engine's, a rounded one from an earlier dress, or the superellipsoid itself on a dress again).
                    if (TheatreNoBox.Enabled && !leaf &&
                        (TheatreMeshes.IsPrimitiveCube(mesh) || TheatreMeshes.IsRoundedCube(mesh) || TheatreNoBox.Is(mesh)))
                    {
                        rounded = TheatreNoBox.Mesh();
                    }
                    // NOBOX end
                    if (rounded != null) { filter.sharedMesh = rounded; mesh = rounded; }
                }

                // A leaf's curl is drawn outside its box, so its bounds carry it; anything else
                // keeps the mesh's own.
                if (TheatreMeshes.IsLamina(mesh))
                {
                    renderer.localBounds = TheatreMeshes.LeafBounds(
                        renderer.transform.localScale, Skin != null ? Skin.CurlFraction : 0.1f);
                }
                else
                {
                    renderer.ResetLocalBounds();
                }

                // Which solid this visual draws, by reference and not by name, so that dressing a
                // body twice reads the same answer the second time: after the swap the mesh is
                // one of the solids this theatre generated, and RoundedFor would no longer
                // recognise it by the engine's name for the primitive it replaced. By name as
                // well, for the raw shapes.
                bool isSphere = mesh == TheatreMeshes.Sphere() || TheatreMeshes.IsPrimitiveSphere(mesh);
                bool isCylinder = mesh == TheatreMeshes.Cylinder() || TheatreMeshes.IsPrimitiveCylinder(mesh);

                int part = body.Part[i];
                if (part < 0 || part >= phenotype.PartCount) continue;

                body.Shape[i] = Aspect(phenotype.Parts[part], isSphere, isCylinder);

                // A capsule whose shaft is shorter than its radius is a ball to any eye, and the
                // three-piece plan draws it badly: the shaft's carve is bounded by its own
                // thickness while the caps are cut a quarter of the way in, so the shaft's rim
                // stands out of the carved caps as a thin disc, and a thin disc flashes wide for
                // one frame when a tumbling body carries it through edge-on (round 46's first
                // films, 2026-09-23). So one cap draws the whole part, at the part's centre,
                // stretched along the shaft to the genome's own half-extent there; the shaft and
                // the other cap are switched off. The ellipsoid with semi-axes (r, r + span, r)
                // is inside the capsule of radius r and half-span span (the distance from any
                // of its points to the axis segment is at most r, by a two-line argument on the
                // convex remainder), so the size bound holds. Not under the raw shapes, which
                // draw the collider as the physics has it.
                if (!RawShapes && (isSphere || isCylinder) && NearlyASphere(phenotype.Parts[part]))
                {
                    if (isSphere && _capTaken.Add(part))
                    {
                        Vector3 h = Abs(phenotype.Parts[part].HalfExtents.ToVector3());
                        float r = CapsuleShape.Radius(phenotype.Parts[part].HalfExtents);
                        Vector3 stretched = body.Shape[i];
                        stretched.y = r > 0f ? Mathf.Max(h.y, r) / r : 1f;
                        body.Shape[i] = stretched;
                        body.Merged[i] = true;
                    }
                    else
                    {
                        renderer.enabled = false;
                        continue;
                    }
                }

                // Squashed before the anchors are measured, not after: Pinches divides by the
                // visual's scale to reach object units, and the scale it has to divide by is the
                // one the mesh is finally drawn at.
                Reshape(body, i, renderer.transform);

                Pinches(body, i, phenotype, part, renderer.transform);
                body.Leaf[i] = OwnAnchor(phenotype, part, renderer.transform);
                // THALLI begin
                if (TheatreSkin.ThalliOn) DressThallus(body, i, phenotype, part, renderer, filter);
                // THALLI end

                if (body.Ramp != null) body.Ramp[i] = RampOf(body, phenotype, part, renderer.transform);

                if (body.Velvet != null) body.Velvet[i] = OsculumOf(body, phenotype, part, renderer.transform);

                if (body.LinkAxis != null)
                {
                    LinkOf(body, phenotype, part, renderer.transform, out Vector4 linkAxis, out Vector4 linkShape);
                    body.LinkAxis[i] = linkAxis;
                    body.LinkShape[i] = linkShape;
                }
            }

            BuildNecks(body, phenotype);
        }

        // ---------------------------------------------------------------- shape

        /// <summary>
        /// What a visual's local scale has to be multiplied by to draw the genome's three
        /// half-extents rather than the one radius the simulation reduced them to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This shows the genome's intent and not the body's physics, and the collider is the
        /// sphere.</b> A node carries three half-extents whatever shape it takes, and
        /// <c>SphereShape.Radius</c> is their mean, so a genome that has evolved a part twice as
        /// long as it is wide collides, displaces and drags as a ball of the average size and the
        /// two other numbers are silent in the world. Drawing the ball back as a ball hides a
        /// trait the genome is carrying; drawing the three half-extents shows what the genome
        /// says while the collider goes on being the sphere. A viewer reading a swimming stroke
        /// off an ellipsoid's long axis is reading something the physics does not have.
        /// <c>CapsuleShape.Radius</c> is the mean of the X and Z half-extents in the same way, so
        /// a capsule's cross-section is drawn elliptical on the same argument and its length,
        /// which the shape does use, is left alone.
        /// </para>
        /// <para>
        /// <b>Why it is a multiplier and not a size.</b> <c>PhenotypeBuilder.VisualPlan</c> owns
        /// the arithmetic that turns half-extents into a visual's scale, and
        /// <c>ResizeColliderAndVisual</c> rewrites it every time a body grows. A second copy of
        /// that arithmetic here would drift from it silently, which is the fault that file spends
        /// its remarks warning about. So this class never computes a size: it takes whatever
        /// scale the simulation last wrote and squashes it.
        /// </para>
        /// <para>
        /// <b>The size bound.</b> Every component is the half-extent divided by the largest of
        /// them, so the longest semi-axis keeps the collider's radius exactly and the two others
        /// come in. An ellipsoid whose longest semi-axis is the sphere's radius is inside the
        /// sphere; for a capsule the same argument holds against the swept sphere, since a cap
        /// squashed on X and Z and left alone on Y is inside the hemisphere it replaces. So this
        /// under-reports and never over-reports, which is the direction the two constraints
        /// allow.
        /// </para>
        /// </remarks>
        private static Vector3 Aspect(PhenotypePart part, bool isSphere, bool isCylinder)
        {
            if (!isSphere && !isCylinder) return Vector3.one;

            Vector3 h = Abs(part.HalfExtents.ToVector3());

            if (part.ShapeId == ShapeIds.Sphere && isSphere)
            {
                float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                if (longest <= 0f) return Vector3.one;

                return new Vector3(h.x / longest, h.y / longest, h.z / longest);
            }

            if (part.ShapeId == ShapeIds.Capsule)
            {
                float longest = Mathf.Max(h.x, h.z);
                if (longest <= 0f) return Vector3.one;

                // Y is left at one on both the shaft and the caps: the shaft's Y is the span the
                // shape really has, and a cap squashed on Y would pull the end of the drawn body
                // in from the end of the capsule collider for no reason.
                return new Vector3(h.x / longest, 1f, h.z / longest);
            }

            return Vector3.one;
        }

        /// <summary>
        /// Applies <see cref="Aspect"/> to whatever local scale the simulation last wrote.
        /// </summary>
        /// <remarks>
        /// Checked against what this class wrote rather than applied every paint, because the
        /// multiplier would compound: a body squashed to nine tenths on every frame of a run is
        /// gone inside a minute. An exact float comparison is the right one here and not a
        /// sloppy one, because an untouched transform holds the identical bits and a growth
        /// resize writes a different number however small the step
        /// (<c>PhenotypeBuilder.ResizeColliderAndVisual</c>).
        /// </remarks>
        private static void Reshape(Body body, int i, Transform visual)
        {
            if (visual == null) return;

            Vector3 current = visual.localScale;
            Vector3 wrote = body.Wrote[i];

            if (current.x == wrote.x && current.y == wrote.y && current.z == wrote.z) return;

            Vector3 aspect = body.Shape[i];
            var next = new Vector3(
                current.x * aspect.x, current.y * aspect.y, current.z * aspect.z);

            visual.localScale = next;
            body.Wrote[i] = next;

            // The merged cap draws from the part's centre, and a growth resize puts it back at
            // the plan's offset along the shaft together with the scale this just corrected.
            if (body.Merged != null && body.Merged[i]) visual.localPosition = Vector3.zero;
        }

        /// <summary>A capsule whose half-span is under its radius: drawn as one ellipsoid.</summary>
        private static bool NearlyASphere(PhenotypePart part) =>
            part.ShapeId == ShapeIds.Capsule &&
            CapsuleShape.HalfSpan(part.HalfExtents) < CapsuleShape.Radius(part.HalfExtents);

        private readonly HashSet<int> _capTaken = new HashSet<int>();

        /// <summary>
        /// The joint anchors that touch one visual, in that visual's own object units.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the shader needs them.</b> Two parts meet by one solid entering another, and
        /// nothing on a surface hides that. The carve deepens near an anchor on both sides of a
        /// junction, so the tissue draws in where the hinge is and the join reads as a waist; it
        /// also widens the gap the neck is drawn in (<see cref="BuildNecks"/>).
        /// </para>
        /// <para>
        /// <b>Two of them.</b> A part is touched by its own attachment to its parent and by every
        /// child hanging off it, and a fixed pair of shader properties is the arrangement that
        /// does not need an array through a property block. The two nearest the visual are the
        /// two that would be seen; a third anchor on the far side of a long part is carved by
        /// whichever of its own pair it is nearest.
        /// </para>
        /// <para>
        /// <b>In object units, and computed once.</b> The anchors are metres in the part's frame,
        /// so they are moved into the visual's frame by its offset and divided by its scale,
        /// which is what makes them mean the same thing to a mesh built as a unit solid. Growth
        /// rebuilds a phenotype at a new scale rather than reshaping it
        /// (<c>Phenotype.Scaled</c>), so a point given as a fraction of a part's own size stays
        /// where it was and this does not have to be redone as a body grows.
        /// </para>
        /// </remarks>
        private static void Pinches(
            Body body, int i, Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null) return;

            // How far a pinch reaches, in object units, where a part's own size is one. Under
            // half, so it is a waist at the joint and not a general thinning of the part.
            const float reach = 0.45f;

            Vector3 offset = visual.localPosition;
            Vector3 scale = visual.localScale;

            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y) < 1e-6f ||
                Mathf.Abs(scale.z) < 1e-6f)
            {
                return;
            }

            var best = new Vector4(0f, 0f, 0f, 0f);
            var second = new Vector4(0f, 0f, 0f, 0f);
            float bestAway = float.MaxValue;
            float secondAway = float.MaxValue;

            void Consider(Vector3 anchorPartLocal)
            {
                Vector3 local = anchorPartLocal - offset;
                var here = new Vector3(local.x / scale.x, local.y / scale.y, local.z / scale.z);

                float away = here.magnitude;
                var point = new Vector4(here.x, here.y, here.z, reach);

                if (away < bestAway)
                {
                    secondAway = bestAway; second = best;
                    bestAway = away; best = point;
                }
                else if (away < secondAway)
                {
                    secondAway = away; second = point;
                }
            }

            PhenotypePart mine = phenotype.Parts[part];

            if (mine.ParentIndex >= 0) Consider(mine.ChildAnchorLocal.ToVector3());

            for (int c = 0; c < phenotype.PartCount; c++)
            {
                if (phenotype.Parts[c].ParentIndex != part) continue;

                Consider(phenotype.Parts[c].ParentAnchorLocal.ToVector3());
            }

            body.PinchA[i] = best;
            body.PinchB[i] = second;
        }

        /// <summary>
        /// The part's own joint anchor in its visual's object units, with w one, or zero for a
        /// root: which end of a leaf is its base.
        /// </summary>
        private static Vector4 OwnAnchor(Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null || part < 0 || part >= phenotype.PartCount) return Vector4.zero;

            PhenotypePart mine = phenotype.Parts[part];
            if (mine.ParentIndex < 0) return Vector4.zero;

            Vector3 scale = visual.localScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f)
            {
                return Vector4.zero;
            }

            Vector3 local = mine.ChildAnchorLocal.ToVector3() - visual.localPosition;
            return new Vector4(local.x / scale.x, local.y / scale.y, local.z / scale.z, 1f);
        }

        // ---------------------------------------------------------------- the colour ramp

        /// <summary>A tissue's ramp tip as the shader's <c>_RampTip</c>: the multiplier in xyz, w one for "on".</summary>
        private static Vector4 RampTipOf(string cellTypeId)
        {
            Vector3 tip = TheatreTissue.Of(cellTypeId).RampTip;
            return new Vector4(tip.x, tip.y, tip.z, 1f);
        }

        /// <summary>
        /// The colour ramp's axis for one visual, as the shader's <c>_RampAxis</c>: the ramp coordinate of an
        /// object position p is <c>dot(p, xyz) + w</c>, zero at the part's base and one at its free end, or the
        /// zero vector for no axis.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The long axis</b> comes from the genome's half extents, in the order the shader's other axis
        /// choices take (<c>TissueBands</c>: Y, then X, then Z) and through the same tie tolerance
        /// (<see cref="AxisTie"/>), so two equal sides pick the same axis every frame. A capsule's is its own
        /// Y, which is its length. Nothing here is read off the drawn matrix, so nothing can flicker as a body
        /// rolls.
        /// </para>
        /// <para>
        /// <b>The base is the end the part joins its parent at.</b> The part's own anchor
        /// (<c>ChildAnchorLocal</c>) names the end by the sign of its component along the long axis, when that
        /// component is at least <see cref="RampAnchorShare"/> of the half length. A root has no anchor, and
        /// an anchor on the side or the middle of the part does not say which end is nearer, so those take a
        /// fixed end from the part's seed, as the carve's lean takes its end from <c>_Carve.x</c>. A blade is
        /// not given an axis here: the shader reads a leaf's own coordinate from base to tip (<c>leaf.x</c>),
        /// which already has its base at the end the joint is on.
        /// </para>
        /// <para>
        /// <b>In object units, as <see cref="Pinches"/> has it.</b> The two ends are points in the part's frame,
        /// moved into the visual's frame by its offset and divided by its scale, so a capsule's shaft and
        /// two caps, each a mesh of its own, share one ramp across the part, and a growth that rescales the
        /// phenotype leaves the numbers as they were. The half length is the box's half extent, the ball's
        /// radius for a sphere part (the drawn ellipsoid's longest semi-axis is the collider's radius), and a
        /// capsule's span plus its radius.
        /// </para>
        /// </remarks>
        private static Vector4 RampOf(Body body, Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null || part < 0 || part >= phenotype.PartCount) return Vector4.zero;

            PhenotypePart mine = phenotype.Parts[part];
            Vector3 half = Abs(mine.HalfExtents.ToVector3());

            int axis;
            float reach;

            if (mine.ShapeId == ShapeIds.Capsule)
            {
                axis = 1;
                reach = CapsuleShape.HalfSpan(mine.HalfExtents) + CapsuleShape.Radius(mine.HalfExtents);
            }
            else
            {
                axis = LongAxisOf(half);
                reach = mine.ShapeId == ShapeIds.Sphere ? SphereShape.Radius(mine.HalfExtents) : half[axis];
            }

            float scaleAlong = visual.localScale[axis];
            if (reach <= 1e-6f || Mathf.Abs(scaleAlong) < 1e-6f) return Vector4.zero;

            // Which end: the anchor's, when it sits out along the long axis; else one the part's seed fixes.
            float sign = 0f;

            if (mine.ParentIndex >= 0)
            {
                float along = mine.ChildAnchorLocal.ToVector3()[axis];
                if (Mathf.Abs(along) >= RampAnchorShare * reach) sign = along > 0f ? 1f : -1f;
            }

            if (sign == 0f)
            {
                float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
                seed -= Mathf.Floor(seed);
                sign = Fraction(seed * RampEndMix + RampEndOffset) < 0.5f ? -1f : 1f;
            }

            // The base and the tip along the axis, in this visual's object units.
            float offset = visual.localPosition[axis];
            float baseOS = (sign * reach - offset) / scaleAlong;
            float tipOS = (-sign * reach - offset) / scaleAlong;
            float span = tipOS - baseOS;
            if (Mathf.Abs(span) < 1e-6f) return Vector4.zero;

            var result = Vector4.zero;
            result[axis] = 1f / span;
            result.w = -baseOS / span;
            return result;
        }

        // ---------------------------------------------------------------- the velvet absorber's osculum

        /// <summary>
        /// An absorber's osculum for one visual, as the shader's <c>_VelvetOsculum</c>: its centre in the visual's
        /// object units in xyz, and its radius in metres in w, or the zero vector for none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It sits on the end of the part's long axis away from its joint, the colour ramp's tip: the axis, the
        /// reach and the base end are <see cref="RampOf"/>'s, copied (the two must move together), and the
        /// osculum is at the far end from that base. The point is the part's end on its axis and the middle of
        /// the other two, taken from the part's frame into the visual's by its offset and divided by its scale,
        /// as the ramp's ends and the pinches are, so a capsule's shaft and caps agree on it.
        /// </para>
        /// <para>
        /// The radius is <see cref="VelvetOsculumShare"/> of the part's smallest half extent (a capsule's is its
        /// radius), and the shader draws the opening in shading, so nothing it does leaves the collider.
        /// </para>
        /// </remarks>
        private static Vector4 OsculumOf(Body body, Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null || part < 0 || part >= phenotype.PartCount) return Vector4.zero;
            if (phenotype.Parts[part].CellTypeId != CellTypeIds.Absorptive) return Vector4.zero;

            PhenotypePart mine = phenotype.Parts[part];
            Vector3 half = Abs(mine.HalfExtents.ToVector3());

            int axis;
            float reach;
            float smallest;

            if (mine.ShapeId == ShapeIds.Capsule)
            {
                axis = 1;
                reach = CapsuleShape.HalfSpan(mine.HalfExtents) + CapsuleShape.Radius(mine.HalfExtents);
                smallest = CapsuleShape.Radius(mine.HalfExtents);
            }
            else
            {
                axis = LongAxisOf(half);
                reach = mine.ShapeId == ShapeIds.Sphere ? SphereShape.Radius(mine.HalfExtents) : half[axis];
                smallest = Mathf.Min(half.x, Mathf.Min(half.y, half.z));
            }

            Vector3 scale = visual.localScale;
            if (reach <= 1e-6f || smallest <= 1e-6f || Mathf.Abs(scale.x) < 1e-6f ||
                Mathf.Abs(scale.y) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f)
            {
                return Vector4.zero;
            }

            // Which end is the base, as the ramp's is: the anchor's, when it sits out along the long axis; else
            // one the part's seed fixes.
            float sign = 0f;

            if (mine.ParentIndex >= 0)
            {
                float along = mine.ChildAnchorLocal.ToVector3()[axis];
                if (Mathf.Abs(along) >= RampAnchorShare * reach) sign = along > 0f ? 1f : -1f;
            }

            if (sign == 0f)
            {
                float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
                seed -= Mathf.Floor(seed);
                sign = Fraction(seed * RampEndMix + RampEndOffset) < 0.5f ? -1f : 1f;
            }

            // The tip end, on the axis, and the middle on the other two, in the part's frame; then the visual's.
            var tip = Vector3.zero;
            tip[axis] = -sign * reach;

            Vector3 local = tip - visual.localPosition;
            return new Vector4(local.x / scale.x, local.y / scale.y, local.z / scale.z, VelvetOsculumShare * smallest);
        }

        // ---------------------------------------------------------------- the link's taper

        /// <summary>
        /// One capsule visual's link taper, as the shader's <c>_LinkAxis</c> and <c>_LinkShape</c>, or both zero
        /// when the visual is not part of a link.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A link is a capsule drawn as a shaft and two caps</b> (<see cref="Dress"/>), and not a capsule drawn
        /// as one ball (<see cref="NearlyASphere"/>), a sphere, a box or a blade. The raw shapes draw the colliders,
        /// so they take none.
        /// </para>
        /// <para>
        /// <b>The coordinate</b> t runs along the whole capsule, zero at the base pole and one at the free pole, as
        /// the colour ramp's does (<see cref="RampOf"/>: the base is the end the part's own anchor sits at, or one
        /// its seed fixes, copied in <see cref="BaseSignOf"/>), in the visual's own object units, so the shaft and
        /// the two caps, each a mesh of its own, read one coordinate and a growth that rescales the phenotype leaves
        /// the numbers as they were. <c>_LinkAxis</c> is <c>(a, w, tb, tj)</c> with t = a y + w for the object y, tb
        /// the t at which the base cap joins the shaft (a radius in from the base pole, over the whole length) and tj
        /// the t at which the tip cap does, 1 - tb. <c>_LinkShape</c> is <c>(length in radii, tip share, 0,
        /// 1)</c>; the length is a ratio and so unchanged by growth.
        /// </para>
        /// </remarks>
        private void LinkOf(
            Body body, Phenotype phenotype, int part, Transform visual,
            out Vector4 axis, out Vector4 shape)
        {
            axis = Vector4.zero;
            shape = Vector4.zero;

            if (RawShapes || visual == null || part < 0 || part >= phenotype.PartCount) return;

            PhenotypePart mine = phenotype.Parts[part];
            if (mine.ShapeId != ShapeIds.Capsule || NearlyASphere(mine)) return;

            float radius = CapsuleShape.Radius(mine.HalfExtents);
            float span = CapsuleShape.HalfSpan(mine.HalfExtents);
            float reach = span + radius;
            float scaleAlong = visual.localScale.y;

            if (radius <= 1e-6f || span <= 1e-6f || Mathf.Abs(scaleAlong) < 1e-6f) return;

            float sign = BaseSignOf(body, mine, part, reach);

            // The base and the free pole along Y, in this visual's object units, as RampOf has them.
            float offset = visual.localPosition.y;
            float baseOS = (sign * reach - offset) / scaleAlong;
            float tipOS = (-sign * reach - offset) / scaleAlong;
            float length = tipOS - baseOS;
            if (Mathf.Abs(length) < 1e-6f) return;

            float baseJoin = radius / (2f * reach);

            axis = new Vector4(1f / length, -baseOS / length, baseJoin, 1f - baseJoin);
            shape = new Vector4(2f * reach / radius, Mathf.Clamp(LinkTipShare, 0.05f, 1f), 0f, 1f);
        }

        /// <summary>
        /// Which end of a capsule is its base, as a sign along Y: the end its own anchor sits at, when that anchor
        /// is out at least <see cref="RampAnchorShare"/> of the reach along the axis, else the end its seed fixes.
        /// A copy of <see cref="RampOf"/>'s choice for a capsule (the two must move together).
        /// </summary>
        private static float BaseSignOf(Body body, PhenotypePart mine, int part, float reach)
        {
            if (mine.ParentIndex >= 0)
            {
                float along = mine.ChildAnchorLocal.ToVector3().y;
                if (Mathf.Abs(along) >= RampAnchorShare * reach) return along > 0f ? 1f : -1f;
            }

            float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
            seed -= Mathf.Floor(seed);
            return Fraction(seed * RampEndMix + RampEndOffset) < 0.5f ? -1f : 1f;
        }

        /// <summary>
        /// The share of its base radius a link keeps at coordinate <paramref name="t"/>, as the shader's
        /// <c>LinkTaper</c> has it: one across the base cap, a smoothstep down to <see cref="LinkTipShare"/> along
        /// the shaft, and that across the tip cap. <paramref name="baseJoin"/> is the t at which the base cap
        /// meets the shaft; the tip cap's is one less.
        /// </summary>
        private static float LinkShare(float t, float baseJoin)
        {
            float u = Mathf.Clamp01((t - baseJoin) / Mathf.Max(1f - 2f * baseJoin, 1e-4f));
            return 1f - (1f - Mathf.Clamp(LinkTipShare, 0.05f, 1f)) * (u * u * (3f - 2f * u));
        }

        /// <summary>
        /// How much further into a link a bridge must run for the taper, in the part's own frame: the radius the
        /// taper took where the ray meets the untapered surface, along the part of the ray that goes across the
        /// axis. A ray along the axis meets the free pole where the collider has it, and takes none.
        /// </summary>
        private static float LinkStandBack(
            Body body, PhenotypePart own, int ownIndex, float radius, float span,
            Vector3 start, Vector3 dir, float hit)
        {
            if (float.IsInfinity(hit) || float.IsNaN(hit) || span <= 1e-6f || radius <= 1e-6f) return 0f;

            float reach = span + radius;
            float sign = BaseSignOf(body, own, ownIndex, reach);
            float y = start.y + dir.y * hit;
            float t = (reach - sign * y) / (2f * reach);
            float across = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);

            return radius * (1f - LinkShare(t, radius / (2f * reach))) * across;
        }

        /// <summary>
        /// A part's long axis from its half extents: Y, then X, then Z, within <see cref="AxisTie"/>, the order
        /// of the shader's <c>TissueBands</c>.
        /// </summary>
        private static int LongAxisOf(Vector3 half)
        {
            if (half.y >= half.x * (1f - AxisTie) && half.y >= half.z * (1f - AxisTie)) return 1;
            if (half.x >= half.z * (1f - AxisTie)) return 0;
            return 2;
        }

        // ---------------------------------------------------------------- joints

        /// <summary>
        /// The joint skin: two knuckles of tissue at every joint, one in each part, and a bridge
        /// from the anchor to a part's drawn surface wherever that surface stands back from it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Presentation only.</b> Renderers with no collider and no rigid body, drawn by the
        /// theatre from the phenotype the run already carries; nothing about the simulation changes
        /// and nothing under <c>Assets/Evosim</c> is touched.
        /// </para>
        /// <para>
        /// <b>What it replaces, and why.</b> Until 2026-09-24 a joint was marked by a short
        /// cylinder in the jointed colour, sunk inside the parent part so it could never leave the
        /// collider. In the safari films that read as two solids meeting at a crease with a bead
        /// of paint in it; the owner asked for a joint skin that looks organic. So each side of a
        /// joint now carries an ellipsoid of its own tissue (<see cref="TheatreSkin.JointMaterial"/>,
        /// the part's own guild colour, mottle and rim), centred on the joint anchor in that part's
        /// frame and moving rigidly with the part. With the limb straight, the half of each knuckle
        /// that leaves its own part lies inside the other part and is hidden. As the joint bends,
        /// the two knuckles round over the opening side of the crease, like skin over a knuckle,
        /// and the inset rounded meshes and the pinch at each anchor read as a waist into it.
        /// </para>
        /// <para>
        /// <b>The size bound, and the one place it is spent.</b> Every other visual is drawn inside
        /// its collider. A knuckle's inner half is inside its own part's box (<see cref="Knuckle"/>:
        /// its cross radii are the anchor's clearance to the box's side faces and its length is
        /// clipped to the room behind the face), and its outer half is inside the other part while
        /// the joint is straight. When the joint bends, the outer half shows in the wedge that opens
        /// between the two parts, which is outside both colliders by at most the knuckle's length.
        /// That length is capped at the smaller of the two cross radii, which the other part bounds
        /// too, so on a thin leaf it is the leaf's own thickness. It is the one deliberate exception,
        /// made because a joint drawn without it shows the crease the owner saw.
        /// </para>
        /// <para>
        /// <b>At a fixed joint too, from 2026-10-06.</b> Until then a fixed joint had no knuckle, on
        /// the reading that two parts welded together need no hinge drawn. But the pinch carves
        /// both parts in at every anchor, fixed or free (<see cref="Pinches"/>), so a fixed joint
        /// was drawn as a gap: a stomach bud floated clear of the capsule it is fixed to, and two
        /// leaves fixed face to face met at one point. A fixed joint never bends, so its knuckles'
        /// outer halves stay where a straight free joint's do.
        /// </para>
        /// <para>
        /// <b>The bridges</b> (<see cref="Bridge"/>). A part's drawn surface is not always at its
        /// anchor. A sphere part is drawn as the genome's ellipsoid inside the ball, so an anchor
        /// on the ball off the long axis lies outside it; a leaf thins to nothing at its rim and
        /// stands a lens's half thickness off a face; and the pinch carves every part in at the
        /// anchor by up to half its smallest half extent. A knuckle reaches in from the anchor by
        /// its own length and no further. So on a side whose drawn tissue lies deeper than that,
        /// a second ellipsoid of the same tissue runs from the anchor in to the drawn tissue, and
        /// the two knuckles carry the join across the anchor between the two sides' bridges. A
        /// bridge lies wholly inside its own part's collider, which is checked for each collider
        /// shape where it is sized, so it spends none of the knuckle's exception.
        /// </para>
        /// <para>
        /// <b>Hidden under the raw shapes</b> (<see cref="RawShapes"/>), which draw the colliders
        /// and nothing else, and under the plain look. Built afresh at every dressing, and the
        /// previous set destroyed first: the raw shapes' toggle dresses a body again, and each
        /// toggle used to leave another set of necks behind.
        /// </para>
        /// </remarks>
        private void BuildNecks(Body body, Phenotype phenotype)
        {
            if (body.Necks != null)
            {
                foreach (Transform old in body.Necks)
                {
                    if (old == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject);
                    else UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                body.Necks = null;
                body.NeckPart = null;
                body.NeckChildSide = null;
                body.NeckBridge = null;
            }

            _necks.Clear();
            _neckPart.Clear();
            _neckSide.Clear();
            _neckBridge.Clear();

            Mesh mesh = TheatreMeshes.Sphere();
            Material material = Skin != null ? (Skin.JointMaterial ?? Skin.BodyMaterial) : null;
            if (mesh == null || material == null) return;

            var partTransform = new Transform[phenotype.PartCount];

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                int part = body.Part[i];
                if (part < 0 || part >= partTransform.Length) continue;
                if (partTransform[part] == null && body.Renderers[i] != null)
                {
                    partTransform[part] = body.Renderers[i].transform.parent;
                }
            }

            void Add(Transform host, int p, bool childSide, bool bridge)
            {
                if (host == null) return;

                string name = bridge
                    ? (childSide ? "Bridge (child)" : "Bridge (parent)")
                    : (childSide ? "Knuckle (child)" : "Knuckle (parent)");

                var go = new GameObject(name)
                {
                    layer = host.gameObject.layer,
                };

                go.transform.SetParent(host, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _necks.Add(go.transform);
                _neckPart.Add(p);
                _neckSide.Add(childSide);
                _neckBridge.Add(bridge);
            }

            for (int p = 0; p < phenotype.PartCount; p++)
            {
                PhenotypePart part = phenotype.Parts[p];

                if (part.ParentIndex < 0) continue;

                Add(partTransform[part.ParentIndex], p, false, false);
                Add(partTransform[p], p, true, false);

                // A bridge only on a side that needs one. Growth rebuilds a phenotype at a new
                // scale with its proportions kept (Phenotype.Scaled), so a side that needs no
                // bridge at birth needs none as an adult, and a body does not carry an idle
                // renderer for every joint it has.
                for (int side = 0; side < 2; side++)
                {
                    bool childSide = side == 1;
                    if (Bridge(body, phenotype, p, childSide, out _, out _, out _))
                    {
                        Add(partTransform[childSide ? p : part.ParentIndex], p, childSide, true);
                    }
                }
            }

            if (_necks.Count == 0) return;

            body.Necks = _necks.ToArray();
            body.NeckPart = _neckPart.ToArray();
            body.NeckChildSide = _neckSide.ToArray();
            body.NeckBridge = _neckBridge.ToArray();
        }

        /// <summary>
        /// Sizes and paints every knuckle from the phenotype as it is now.
        /// </summary>
        /// <remarks>
        /// Recomputed on each paint rather than once, because a body grows: D087 rebuilds a
        /// creature's phenotype at its new size and <c>ResizeColliderAndVisual</c> moves the
        /// visuals to match, so a knuckle placed once would keep a newborn's dimensions on an
        /// adult. Three transform writes a knuckle, inside a repaint budget already capped per
        /// frame. Each knuckle takes its own part's colour, carve character and light through the
        /// tissue, a little darker than the part, so a joint between a leaf and a stomach shades
        /// from one into the other across the crease.
        /// </remarks>
        private void RefreshNecks(Body body, Phenotype phenotype, float brightness, float reserve, bool on, bool dim = false, float turn = 0f,
            TheatreSkinMap.Entry skin = null)
        {
            if (body.Necks == null) return;

            for (int i = 0; i < body.Necks.Length; i++)
            {
                Transform neck = body.Necks[i];
                if (neck == null) continue;

                int p = body.NeckPart[i];
                bool childSide = body.NeckChildSide != null && body.NeckChildSide[i];

                if (p < 0 || p >= phenotype.PartCount || phenotype.Parts[p].ParentIndex < 0)
                {
                    neck.gameObject.SetActive(false);
                    continue;
                }

                PhenotypePart part = phenotype.Parts[p];
                PhenotypePart parent = phenotype.Parts[part.ParentIndex];

                bool bridge = body.NeckBridge != null && body.NeckBridge[i];

                Vector3 centre, scale;
                Quaternion rotation = Quaternion.identity;
                bool fitted = bridge
                    ? Bridge(body, phenotype, p, childSide, out centre, out rotation, out scale)
                    : Knuckle(part, parent, childSide, out centre, out scale);

                // Blended joints (EVOSIM_THEATRE_BLEND): the parent side's knuckle drawn as a flare where
                // Flare finds one fits, and as today's knuckle where it does not.
                if (_blend && fitted && !bridge && !childSide &&
                    Flare(part, parent, out Vector3 flareCentre, out Vector3 flareScale))
                {
                    centre = flareCentre;
                    scale = flareScale;
                }

                bool show = fitted && on && !RawShapes;

                neck.gameObject.SetActive(show);
                if (!show) continue;

                neck.localPosition = centre;
                neck.localRotation = rotation;
                neck.localScale = scale;

                var renderer = neck.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                int own = childSide ? p : part.ParentIndex;
                string cellType = phenotype.Parts[own].CellTypeId;

                // The marine producers (EVOSIM_THEATRE_ALGA): a neck of producer or gas-bladder tissue takes its part's
                // colour from the family, and a producer's its depth tint, so the join matches the blade; no form.
                Color neckColour = ColourOf(cellType);
                AlgaDress neckAlga = _alga ? AlgaDressOf(phenotype, own, turn, dim, false) : default;
                if (neckAlga.Recoloured) neckColour = neckAlga.Guild;
                // SNOUT begin
                if (TheatreNosePit.SnoutOn && on && cellType == CellTypeIds.Nose && SnoutNeckColour(body, phenotype, own, out Color snoutNeck)) neckColour = snoutNeck;
                // SNOUT end

                // A non-producer's neck takes the body's palette turn, as its parts do, unless the alga look recoloured it.
                bool neckWide = skin != null && !skin.Producer && !neckAlga.Recoloured;
                float neckTurn = neckWide ? SkinMainTurnOf(skin) : turn;

                Color guild = Turned(neckColour, neckTurn, neckWide);
                if (dim) guild = Grey(guild);

                float seed = body.Seed + 0.61803399f * own + 0.5f;
                seed -= Mathf.Floor(seed);

                // Leaf tissue in a neck is lit as the blade is: the shader dims a blade's rim
                // and glow and darkens it toward the midrib (TheatreBody.shader, rimScale and
                // leafTone), and does neither on any other mesh, so a knuckle or a bridge of leaf
                // tissue lit as a solid, rim and light through at full, read as a pale glass
                // spindle against the blade it joins (p03-r57s1-18207, 2026-10-06).
                PhenotypePart ownPart = phenotype.Parts[own];
                bool leafTissue = DrawnAsLeaf(ownPart, Abs(ownPart.HalfExtents.ToVector3()));
                float tone = leafTissue ? LeafNeckTone : 1f;

                // A stipe's neck keeps the stipe's shade and a gas bladder's the bladder's gloss, as the part does
                // (AlgaStipeShade, AlgaBladderSheen); both false, and so the numbers above untouched, with the dial off.
                // The skin map's axes dial: the neck's sheen is the body's sheen factor times its own, as a part's is (a neck is given the tissue's sheen and never a corpse's, so there is no dead or aged test).
                // The part-colour dial is not given a neck or a bridge (partT and partU stay NaN in the call below), so a bridge takes the body colour.
                float neckSheen = neckAlga.Bladder ? Mathf.Max(SheenOf(cellType), AlgaBladderSheen) : SheenOf(cellType);
                if (TheatreSkinMap.AxesOn && neckWide && !dim && skin != null) neckSheen = TheatreSkinMap.Sheened(neckSheen, skin);

                Set(renderer, guild, neckAlga.Stipe ? KnuckleShade * brightness * tone * AlgaStipeShade : KnuckleShade * brightness * tone, reserve, true,
                    Character(cellType, seed), Vector4.zero, Vector4.zero,
                    TransmissionOf(cellType), neckSheen, default, OwnOf(body, own),
                    leafTissue ? LeafNeckRim : 1f, tone,
                    TheatreTissue.LookOn ? TheatreTissue.Of(cellType) : null, neckTurn, dim,
                    cellView: CellView, cellColour: CellView ? CellViewColourOf(cellType) : default,
                    algaDeep: neckAlga.Deep, algaShallow: neckAlga.Shallow, algaHue: neckAlga.Recoloured,
                    skin: skin, wideHue: neckWide);
            }
        }

        /// <summary>How much of its part's brightness a knuckle keeps: a crease reads a shade darker.</summary>
        private const float KnuckleShade = 0.85f;

        /// <summary>
        /// The rim and glow a neck of leaf tissue keeps: the share the shader leaves a blade's
        /// (<c>TheatreBody.shader</c>, <c>rimScale</c>, 0.45).
        /// </summary>
        private const float LeafNeckRim = 0.45f;

        /// <summary>
        /// The tone a neck of leaf tissue is drawn at, body and light through it alike: a blade's
        /// near its midrib and stalk, where its tone runs from a half to about four fifths
        /// (<c>TheatreBody.shader</c>, <c>leafTone</c>).
        /// </summary>
        private const float LeafNeckTone = 0.75f;

        /// <summary>
        /// Whether a part is drawn as a leaf. A box that is flat is one whatever its tissue, and
        /// a photosynthetic box is one whatever its thickness: round 47's leaves are not sheets
        /// (smallest side over the next, median 0.79 in seed 1's 5,000 s snapshot, none under
        /// 0.4), so a thick one is drawn as a plump leaf, a succulent's, inside its box.
        /// </summary>
        public static bool DrawnAsLeaf(PhenotypePart part, Vector3 sides)
        {
            if (!TheatreMeshes.Leaves || part.ShapeId != ShapeIds.Box) return false;
            return TheatreMeshes.LeafShaped(sides) || part.CellTypeId == CellTypeIds.Photosynthetic;
        }

        /// <summary>
        /// One side of a joint as the knuckle sizes it: the anchor in the side's own part frame,
        /// the face axis and which way along it the other part lies, the two cross radii and the
        /// half-length along the face axis.
        /// </summary>
        private struct KnuckleFit
        {
            public Vector3 Anchor;
            public int Axis;
            public float Sign;
            public float Ra;
            public float Rb;
            public float Length;

            /// <summary>This side's clearance from the anchor to its side faces, on the two cross axes.</summary>
            public float ClearA;
            public float ClearB;

            /// <summary>The other part's clearance along the same two axes (<see cref="ClearanceAlong"/>).</summary>
            public float OtherA;
            public float OtherB;
        }

        /// <summary>The unit vector along one of a part's own box axes.</summary>
        private static Vector3 Unit(int axis)
        {
            var v = Vector3.zero;
            v[axis] = 1f;
            return v;
        }

        /// <summary>
        /// How far the other part reaches from its own anchor along a direction given in this
        /// part's frame: the nearer of its two side faces across its joint, along that direction
        /// carried into its frame by the two parts' rest rotations; <paramref name="fallback"/>
        /// when the direction runs along its joint's axis or no face fits.
        /// </summary>
        private static float ClearanceAlong(
            PhenotypePart own, PhenotypePart other, Vector3 otherAnchor, Vector3 dirOwn, float fallback)
        {
            if (!Face(other, own, otherAnchor, out int axis, out float ca, out float cb, out _, out _))
            {
                return fallback;
            }

            Quaternion ownRotation = own.Rotation.ToQuaternion();
            Quaternion otherRotation = other.Rotation.ToQuaternion();
            Vector3 d = Quaternion.Inverse(otherRotation) * (ownRotation * dirOwn);

            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;
            float reach = float.PositiveInfinity;

            if (Mathf.Abs(d[a]) > 1e-3f) reach = Mathf.Min(reach, ca / Mathf.Abs(d[a]));
            if (Mathf.Abs(d[b]) > 1e-3f) reach = Mathf.Min(reach, cb / Mathf.Abs(d[b]));

            return float.IsInfinity(reach) ? fallback : reach;
        }

        /// <summary>
        /// The half thickness a leaf is drawn at, near its midrib, a little under: the blade's
        /// lens at most a third of its half width thick (TheatreBody.shader, ShapeLamina's flesh),
        /// at seven tenths.
        /// </summary>
        private static float LeafThickness(Vector3 h)
        {
            LeafAxes(h, out _, out int thick, out int width);
            return 0.7f * Mathf.Min(h[thick], 0.35f * h[width]);
        }

        /// <summary>
        /// One knuckle's centre and scale in its own part's frame, or false when nothing fits.
        /// </summary>
        /// <remarks>
        /// The mesh is <see cref="TheatreMeshes.Sphere"/>, a unit sphere of diameter one, so the
        /// scale is the ellipsoid's three diameters on the part's own box axes and no rotation is
        /// needed. See <see cref="FitKnuckle"/> for the sizes.
        /// </remarks>
        private static bool Knuckle(
            PhenotypePart part, PhenotypePart parent, bool childSide,
            out Vector3 centre, out Vector3 scale)
        {
            centre = Vector3.zero;
            scale = Vector3.one;

            if (!FitKnuckle(part, parent, childSide, out KnuckleFit fit)) return false;

            int a = (fit.Axis + 1) % 3;
            int b = (fit.Axis + 2) % 3;

            centre = fit.Anchor;
            scale = Vector3.zero;
            scale[fit.Axis] = 2f * fit.Length;
            scale[a] = 2f * fit.Ra;
            scale[b] = 2f * fit.Rb;

            return true;
        }

        /// <summary>
        /// The blend radius's share of the smaller of the two parts' smallest half extents
        /// (<see cref="Flare"/>): a quarter, so on the thinnest limb the ring is a quarter of its thickness.
        /// </summary>
        private const float BlendShare = 0.25f;

        /// <summary>The most the blend radius can be, in metres, on parts of any size (<see cref="Flare"/>).</summary>
        private const float BlendMaximum = 0.02f;

        /// <summary>
        /// How far the flare's centre is sunk into the parent, as a share of its own half length
        /// (<see cref="Flare"/>). Sunk, the ellipsoid meets the parent's face below its widest ring, so it
        /// leans in toward the child rather than standing up from the face as a collar, and its inner half
        /// reaches into the parent's carved tissue the way the knuckle did.
        /// </summary>
        private const float FlareSink = 0.35f;

        /// <summary>
        /// The parent side's knuckle as a flare round the child's base (<c>EVOSIM_THEATRE_BLEND</c>): its centre
        /// and scale in the parent's frame, or false when the joint keeps today's knuckle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it is.</b> The knuckle's ellipsoid, on the same face axis (<see cref="FitKnuckle"/>), made
        /// wider than the child by a blend radius r and sunk into the parent by <see cref="FlareSink"/> of its
        /// half length, so that where it leaves the parent's face it is r wider than the child on each cross
        /// axis and it closes onto the child's side r up from the face. The rest of it is inside one part or
        /// the other, so the join reads as one tissue swelling into the next, and the parts keep their own
        /// outlines above the ring.
        /// </para>
        /// <para>
        /// <b>The sizes.</b> r is <see cref="BlendShare"/> of the smaller of the two parts' smallest half
        /// extents, and never more than <see cref="BlendMaximum"/>. On a cross axis with the child's half width
        /// w there (the knuckle's <c>OtherA</c> or <c>OtherB</c>) the radius is (w + r) / √(1 − k²), with k the
        /// sink, so the section at the face is w + r; it is capped at nine tenths of the anchor's clearance to
        /// the parent's side faces, as the knuckle's is, so the widest ring stays inside the parent's box and
        /// under its inset rounded edge. The ellipsoid's reach into the parent, its half length L times
        /// (1 + k), is the knuckle's own length, which is clipped to the room in both parts, or less: on each
        /// cross axis whose radius is over w, the height at which the surface meets the child's side is
        /// L (√(1 − (w/R)²) − k), and the reach is cut until that is at most r. The narrower axis sets it, and
        /// the wider one rises less.
        /// </para>
        /// <para>
        /// <b>The bound, stated.</b> With the joint straight, everything the flare draws outside both colliders
        /// lies above the parent's face by at most r and beyond the child's nearer side face by at most r on
        /// either cross axis, so r ≤ 2 cm is the margin. Below the face the ellipsoid is inside the parent's
        /// box (cross radii under the clearance, reach under the room); above it, within the child's section,
        /// it is inside the child (its height there is under the knuckle's length, which the child's room
        /// bounds). When the joint bends the dome above the face shows in the wedge as the knuckle's outer
        /// half did, by at most its height, which is under the knuckle's length: no more than today's
        /// exception (<see cref="BuildNecks"/>).
        /// </para>
        /// <para>
        /// <b>Boxes only.</b> The parent and the child must both be boxes and neither drawn as a leaf. A sphere's
        /// or a capsule's parent knuckle keeps its rules (<see cref="FitKnuckle"/>), since its collider is not
        /// the box the clearances are measured on; a sphere or capsule child is round where it meets the face,
        /// so a ring sized on its box would stand off it by more than r; and a leaf narrows to a point at its
        /// base and its blade is thin, so a ring as wide as its box would be a lozenge round nothing. Those
        /// joints keep today's knuckle, as does one where the parent is no wider than the child on either
        /// axis, which leaves no room for a ring. The child side's knuckle and the bridges are not changed: the
        /// child's knuckle lies inside the flare near the face, and a bridge is sized from
        /// <see cref="FitKnuckle"/>, which this does not alter.
        /// </para>
        /// </remarks>
        private static bool Flare(
            PhenotypePart part, PhenotypePart parent, out Vector3 centre, out Vector3 scale)
        {
            centre = Vector3.zero;
            scale = Vector3.zero;

            if (parent.ShapeId != ShapeIds.Box || part.ShapeId != ShapeIds.Box) return false;

            Vector3 parentHalf = Abs(parent.HalfExtents.ToVector3());
            Vector3 childHalf = Abs(part.HalfExtents.ToVector3());
            if (DrawnAsLeaf(parent, parentHalf) || DrawnAsLeaf(part, childHalf)) return false;

            if (!FitKnuckle(part, parent, false, out KnuckleFit fit)) return false;

            float smallest = Mathf.Min(
                Mathf.Min(parentHalf.x, Mathf.Min(parentHalf.y, parentHalf.z)),
                Mathf.Min(childHalf.x, Mathf.Min(childHalf.y, childHalf.z)));
            float r = Mathf.Min(BlendShare * smallest, BlendMaximum);
            if (r <= 1e-4f || fit.Length <= 1e-4f) return false;

            float k = FlareSink;
            float widen = 1f / Mathf.Sqrt(1f - k * k);

            float ra = Mathf.Min((fit.OtherA + r) * widen, 0.9f * fit.ClearA);
            float rb = Mathf.Min((fit.OtherB + r) * widen, 0.9f * fit.ClearB);
            if (ra <= fit.OtherA && rb <= fit.OtherB) return false;

            // The reach into the parent: the knuckle's length, cut on each axis that carries a ring until the
            // ring meets the child's side no more than r up from the face. The height there is linear in the
            // reach, so the cut is a division.
            float reach = fit.Length;
            float RiseShare(float w, float radius)
            {
                float q = w / radius;
                return (Mathf.Sqrt(Mathf.Max(0f, 1f - q * q)) - k) / (1f + k);
            }

            if (ra > fit.OtherA)
            {
                float g = RiseShare(fit.OtherA, ra);
                if (g > 1e-6f) reach = Mathf.Min(reach, r / g);
            }

            if (rb > fit.OtherB)
            {
                float g = RiseShare(fit.OtherB, rb);
                if (g > 1e-6f) reach = Mathf.Min(reach, r / g);
            }

            float half = reach / (1f + k);
            if (half <= 1e-4f) return false;

            int axis = fit.Axis;
            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;

            centre = fit.Anchor - Unit(axis) * (fit.Sign * k * half);
            scale = Vector3.zero;
            scale[axis] = 2f * half;
            scale[a] = 2f * ra;
            scale[b] = 2f * rb;

            return true;
        }

        /// <summary>
        /// One knuckle's sizes, or false when nothing fits.
        /// </summary>
        /// <remarks>
        /// The joint's face is the box axis the anchor reaches furthest along (the axis whose reach, over that
        /// axis's own half extent, is largest); the two cross radii are the anchor's clearance to the side
        /// faces on the other two axes, each capped at the other part's smaller clearance so the
        /// knuckle is never wider than the limb across the joint, and taken at nine tenths so its
        /// widest ring stays under the inset rounded edge; the half-length along the face's axis is
        /// the smaller cross radius, clipped to the room behind the face in both parts.
        /// </remarks>
        private static bool FitKnuckle(
            PhenotypePart part, PhenotypePart parent, bool childSide, out KnuckleFit fit)
        {
            fit = default;

            PhenotypePart own = childSide ? part : parent;
            PhenotypePart other = childSide ? parent : part;
            Vector3 ownAnchor = (childSide ? part.ChildAnchorLocal : part.ParentAnchorLocal).ToVector3();
            Vector3 otherAnchor = (childSide ? part.ParentAnchorLocal : part.ChildAnchorLocal).ToVector3();

            if (!Face(own, other, ownAnchor, out int axis, out float ca, out float cb, out float room, out float sign))
            {
                return false;
            }

            if (!Face(other, own, otherAnchor, out _, out float oa, out float ob, out float otherRoom, out _))
            {
                return false;
            }

            // The other part's room across the joint, on each of this side's two cross axes:
            // measured along that axis carried into the other part's frame, so a slab joined to
            // a block is as wide as the slab along its width and as thin as it along its
            // thickness. Until 2026-10-06 both radii took the other part's smaller clearance,
            // which drew a plate's edge joined to a block, or two plates joined rim to rim, as
            // touching through a bead as thin as the plate. On a box only: a sphere's or a
            // capsule's knuckle keeps the smaller clearance, since its collider is not the box
            // these clearances are measured on and its knuckle already leaves the ball near
            // the anchor (BuildNecks' remarks).
            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;
            float across = Mathf.Min(oa, ob);
            bool box = own.ShapeId == ShapeIds.Box;
            float otherA = box ? ClearanceAlong(own, other, otherAnchor, Unit(a), across) : across;
            float otherB = box ? ClearanceAlong(own, other, otherAnchor, Unit(b), across) : across;

            float ra = 0.9f * Mathf.Min(ca, otherA);
            float rb = 0.9f * Mathf.Min(cb, otherB);

            Vector3 ownHalf = Abs(own.HalfExtents.ToVector3());
            if (DrawnAsLeaf(own, ownHalf))
            {
                LeafAxes(ownHalf, out _, out int thick, out int width);

                if (axis == width)
                {
                    // Joined at its rim: a seam along the rim, as long as the blade is wide
                    // there and no thicker than the blade is drawn at its midrib, so two blades
                    // joined edge to edge read as one blade fused along a seam.
                    float blade = LeafThickness(ownHalf);
                    ra = a == thick ? Mathf.Min(ra, blade) : Mathf.Min(ra, 0.5f * ownHalf[a]);
                    rb = b == thick ? Mathf.Min(rb, blade) : Mathf.Min(rb, 0.5f * ownHalf[b]);
                }
                else
                {
                    // A leaf narrows to a point at its base (TheatreBody.shader, ShapeLamina), so a
                    // knuckle as wide as its box would stand out of the drawn leaf as a flat lozenge.
                    // On a leaf-shaped part it is a swelling at the stalk: a fifth of the half width.
                    float halfWidth = ownHalf.x + ownHalf.y + ownHalf.z
                        - Mathf.Min(ownHalf.x, Mathf.Min(ownHalf.y, ownHalf.z))
                        - Mathf.Max(ownHalf.x, Mathf.Max(ownHalf.y, ownHalf.z));
                    ra = Mathf.Min(ra, 0.2f * halfWidth);
                    rb = Mathf.Min(rb, 0.2f * halfWidth);
                }
            }
            float length = Mathf.Min(Mathf.Min(ra, rb), Mathf.Min(room, otherRoom));

            if (ra <= 1e-4f || rb <= 1e-4f || length <= 1e-4f) return false;

            fit = new KnuckleFit
            {
                Anchor = ownAnchor,
                Axis = axis,
                Sign = sign,
                Ra = ra,
                Rb = rb,
                Length = length,
                ClearA = ca,
                ClearB = cb,
                OtherA = otherA,
                OtherB = otherB,
            };

            return true;
        }

        /// <summary>
        /// The face of <paramref name="host"/>'s box a joint anchor sits on, the anchor's clearance
        /// to the side faces across it, the room behind it, and which way along the face's axis
        /// the other part lies.
        /// </summary>
        private static bool Face(
            PhenotypePart host, PhenotypePart across, Vector3 anchor,
            out int axis, out float clearA, out float clearB, out float room, out float sign)
        {
            axis = 0; clearA = 0f; clearB = 0f; room = 0f; sign = 1f;

            Vector3 h = Abs(host.HalfExtents.ToVector3());
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;

            // The direction from the host's centre towards the other part, in the host's frame:
            // Direction's second argument is the part whose frame it measures in.
            Vector3 towards = Direction(across, host, anchor);
            float best = -1f;

            for (int j = 0; j < 3; j++)
            {
                float reach = Mathf.Abs(towards[j]) / h[j];
                if (reach > best) { best = reach; axis = j; }
            }

            sign = towards[axis] >= 0f ? 1f : -1f;
            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;

            clearA = h[a] - Mathf.Abs(anchor[a]);
            clearB = h[b] - Mathf.Abs(anchor[b]);
            room = sign > 0f ? anchor[axis] + h[axis] : h[axis] - anchor[axis];

            return clearA > 0f && clearB > 0f && room > 0f;
        }

        // ---------------------------------------------------------------- bridges

        /// <summary>
        /// How far past the deepest carve a bridge runs into the drawn tissue, as a fraction of
        /// the part's smallest drawn half extent.
        /// </summary>
        private const float BridgeOverlap = 0.25f;

        /// <summary>
        /// A bridge squeezed by its collider to under this fraction of its knuckle's width is not
        /// drawn: it would be a thread, not a neck.
        /// </summary>
        private const float BridgeThinnest = 0.2f;

        /// <summary>
        /// The core of a leaf, as a fraction of its half length and half width about its centre:
        /// where the blade is drawn wide and thick whatever its seed gives its outline
        /// (<c>TheatreBody.shader</c>, <c>LeafWidth</c>: a third of the half width or more from a
        /// quarter to three quarters of the way along it).
        /// </summary>
        private const float LeafCore = 0.3f;

        /// <summary>
        /// The deepest the carve cuts a part at a joint, as a fraction of its smallest half
        /// extent: the dial at the heaviest guild's weight (<see cref="Character"/>), deepened by
        /// two anchors' pinches at the shader's gain, and capped where the shader caps it
        /// (<c>_CarveMaximum</c>).
        /// </summary>
        private float CarveReach()
        {
            float fraction = Skin != null ? Mathf.Clamp(Skin.CarveFraction, 0f, 0.5f) : 0.35f;
            return Mathf.Min(0.5f, fraction * 1.05f * (1f + 1.6f * 2f));
        }

        /// <summary>
        /// One bridge's centre, rotation and scale in its own part's frame, or false when its side
        /// needs none or none fits.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it is.</b> An ellipsoid of the part's own tissue (the knuckle's mesh and
        /// material) that starts at the joint's anchor and runs into the part until it is inside
        /// the drawn tissue: the gap between a joint and a drawn surface that stands back from it,
        /// filled by a neck. As wide as the side's knuckle, or narrower where the collider asks.
        /// A side whose drawn tissue the knuckle already reaches gets none, so a joint the old
        /// skin drew whole is drawn as it was.
        /// </para>
        /// <para>
        /// <b>Where it runs to, by the part's drawn shape.</b> A sphere part and a capsule run
        /// along the line from the anchor to the collider's core (the ball's centre, or the
        /// nearest point of the capsule's axis) as far as the drawn ellipsoid, the cap or the
        /// shaft, measured exactly from the shapes <see cref="Aspect"/> and <see cref="Dress"/>
        /// draw. A leaf runs to its core (<see cref="LeafCore"/>), lifted by the arch of its curl
        /// as the shader draws it there (<see cref="LeafArch"/>), because its rim thins to nothing
        /// and no point of it near the rim is sure to be drawn. A rounded box runs in along the
        /// face's axis by the most its inset, taper and bend can take, with the anchor moved off
        /// the rounded edges onto the flat of the face. Each then runs on by the carve's
        /// deepest cut and a quarter of the part's smallest half extent more, so it ends in tissue
        /// and not in a hollow.
        /// </para>
        /// <para>
        /// <b>Inside the collider, by construction and then by a check.</b> On a ball, or a
        /// capsule, whose core is at distance a from the anchor, an ellipsoid with its tip at the
        /// anchor, its axis through the core, half length L and cross radius r lies inside the
        /// ball of radius a about the core when L ≤ a and r² ≤ a L. (With v one less the cosine of
        /// the angle round the ellipsoid from its tip, the squared distance from the core is
        /// a² + 2v(r² − aL) + v²(L² − r²) for v from 0 to 2: a² at the tip, not rising from it
        /// while r² ≤ aL, and (a − 2L)² at the far tip; convex or concave, it stays under a².)
        /// And that ball is inside the collider: the core is the ball's centre or a point of the
        /// capsule's axis, and a is at most the collider's radius. On a box the ellipsoid's own bounding
        /// box, from its three axes, is held inside the collider's box, and its cross radii are
        /// shrunk until it is. A bridge does not leave its own part, so it spends none of the
        /// knuckle's exception; the knuckles alone carry the join across the anchor.
        /// </para>
        /// <para>
        /// <b>Refitted at every paint</b>, with the knuckles (<see cref="RefreshNecks"/>), so it
        /// follows a body that grows, and it is a child of its part, so it moves with it.
        /// </para>
        /// </remarks>
        private bool Bridge(
            Body body, Phenotype phenotype, int p, bool childSide,
            out Vector3 centre, out Quaternion rotation, out Vector3 scale)
        {
            centre = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.zero;

            if (p < 0 || p >= phenotype.PartCount) return false;

            PhenotypePart part = phenotype.Parts[p];
            if (part.ParentIndex < 0 || part.ParentIndex >= phenotype.PartCount) return false;

            PhenotypePart parent = phenotype.Parts[part.ParentIndex];
            if (!FitKnuckle(part, parent, childSide, out KnuckleFit fit)) return false;

            int ownIndex = childSide ? p : part.ParentIndex;
            PhenotypePart own = phenotype.Parts[ownIndex];

            Vector3 h = Abs(own.HalfExtents.ToVector3());
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;

            Vector3 anchor = fit.Anchor;
            float ra = fit.Ra;
            float rb = fit.Rb;

            if (own.ShapeId == ShapeIds.Sphere || own.ShapeId == ShapeIds.Capsule)
            {
                bool ball = own.ShapeId == ShapeIds.Sphere;
                float radius = ball
                    ? SphereShape.Radius(own.HalfExtents)
                    : CapsuleShape.Radius(own.HalfExtents);
                float span = ball ? 0f : CapsuleShape.HalfSpan(own.HalfExtents);

                Vector3 core = new Vector3(0f, Mathf.Clamp(anchor.y, -span, span), 0f);
                if (ball) core = Vector3.zero;

                Vector3 toCore = core - anchor;
                float away = toCore.magnitude;
                if (away < 1e-4f || radius <= 1e-4f) return false;

                Vector3 dir = toCore / away;

                // From the collider's surface: an anchor a float's width outside it starts there.
                float a = Mathf.Min(away, radius);
                Vector3 start = core - dir * a;

                float hit = DrawnHit(own, h, radius, span, start, dir, out float smallest);

                // Under the link dial a capsule is drawn narrower toward its free end, so a ray across it meets
                // the drawn surface deeper by the radius the taper took there (LinkStandBack). The depth below
                // is still capped at the collider's own, so the bridge stays inside it.
                if (_link && !ball && !NearlyASphere(own)) hit += LinkStandBack(body, own, ownIndex, radius, span, start, dir, hit);

                float depth = Mathf.Min(Mathf.Min(hit, a) + (CarveReach() + BridgeOverlap) * smallest, a);
                if (depth <= fit.Length) return false;

                float half = 0.5f * depth;
                float cap = 0.95f * Mathf.Sqrt(a * half);
                float widest = Mathf.Max(ra, rb);
                float f = widest > cap ? cap / widest : 1f;
                if (f < BridgeThinnest) return false;

                centre = start + dir * half;
                rotation = Orient(dir, fit.Axis);
                scale = new Vector3(2f * ra * f, 2f * half, 2f * rb * f);
                return true;
            }

            if (own.ShapeId != ShapeIds.Box) return false;

            int axis = fit.Axis;
            float smallestHalf = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            Vector3 target = anchor;

            if (DrawnAsLeaf(own, h))
            {
                LeafAxes(h, out int iL, out int iT, out int iW);

                target[iL] = Mathf.Clamp(anchor[iL], -LeafCore * h[iL], LeafCore * h[iL]);
                target[iW] = Mathf.Clamp(anchor[iW], -LeafCore * h[iW], LeafCore * h[iW]);

                // The midrib's height there: the arch of the curl, the one term of the lift that
                // is not zero on the midrib, in metres (TheatreBody.shader, LeafPoint).
                float along = target[iL] / h[iL];
                float curl = Skin != null ? Mathf.Clamp(Skin.CurlFraction, 0f, 0.1f) : 0.1f;
                float lift = LeafArch(PartSeed(body, ownIndex), OwnOf(body, ownIndex)) * (1f - along * along);
                target[iT] = Mathf.Clamp(2f * curl * h[iW] * lift, -0.4f * h[iT], 0.4f * h[iT]);

                // Its own widths, not the knuckle's: across the blade no thicker than the blade is
                // drawn at its midrib, so it reads as the blade's own tissue and not as a ridge
                // standing out of it; along the blade as wide as the joint, on both parts, and at
                // most half the blade, so a blade whose end or rim is joined to a part across its
                // width is drawn running into that part, not narrowing to a point beside it.
                float blade = LeafThickness(h);
                int ca = (axis + 1) % 3;
                int cb = (axis + 2) % 3;
                ra = ca == iT
                    ? Mathf.Min(blade, 0.9f * fit.ClearA)
                    : Mathf.Min(0.9f * Mathf.Min(fit.ClearA, fit.OtherA), 0.5f * h[ca]);
                rb = cb == iT
                    ? Mathf.Min(blade, 0.9f * fit.ClearB)
                    : Mathf.Min(0.9f * Mathf.Min(fit.ClearB, fit.OtherB), 0.5f * h[cb]);
            }
            else
            {
                float largest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                float pillow = TheatreMeshes.PillowFor(largest > 0f ? smallestHalf / largest : 1f);

                for (int c = 0; c < 3; c++)
                {
                    if (c == axis) continue;
                    float flat = (1f - pillow) * h[c];
                    target[c] = Mathf.Clamp(anchor[c], -flat, flat);
                }

                // The longest axis as the shader picks it (ShapeBox), ties in the same order: a
                // face across it is an end, which the taper does not narrow.
                int longest = h.x >= h.y && h.x >= h.z ? 0 : (h.y >= h.z ? 1 : 2);
                float taper = longest == axis || Skin == null ? 0f : Mathf.Clamp(Skin.TaperFraction, 0f, 0.8f) * h[axis];
                float bend = Skin != null ? Mathf.Clamp(Skin.BendFraction, 0f, 0.4f) * smallestHalf : 0f;

                float recess = (1f - TheatreMeshes.Inset) * h[axis] + taper + bend +
                               (CarveReach() + BridgeOverlap) * smallestHalf;
                float below = h[axis] - Mathf.Abs(anchor[axis]);
                float need = Mathf.Clamp(recess - below, 0f, Mathf.Abs(anchor[axis]));

                target[axis] = anchor[axis] - fit.Sign * need;
            }

            Vector3 path = target - anchor;
            float reach = path.magnitude;
            if (reach <= fit.Length || reach < 1e-4f) return false;

            Vector3 from = anchor;
            Vector3 heading = path / reach;

            var inward = Vector3.zero;
            inward[axis] = -fit.Sign;
            if (Vector3.Dot(heading, inward) < 0.995f)
            {
                // A bridge that leaves the face's axis would put its side through the face it
                // starts on, so it starts half its knuckle's length in, where the knuckle still
                // covers it.
                from = anchor + inward * (0.5f * fit.Length);
            }
            else
            {
                // An anchor inside the box (a part joined across a plate's end, a little in from
                // it) has its own box still around it out to the face. The bridge starts there,
                // so it is at its full width where the other part meets it rather than narrowing
                // to its tip at the anchor.
                float outside = h[axis] - Mathf.Abs(anchor[axis]);
                if (outside > 0f) from = anchor - inward * outside;
            }

            path = target - from;
            reach = path.magnitude;
            if (reach < 1e-4f) return false;
            heading = path / reach;

            float halfLength = 0.5f * reach;
            Vector3 middle = from + heading * halfLength;
            Quaternion turn = Orient(heading, axis);

            float fits = BoxFit(h, middle, turn, ra, halfLength, rb);
            if (fits < BridgeThinnest || Mathf.Min(ra, rb) * fits <= 1e-4f) return false;

            centre = middle;
            rotation = turn;
            scale = new Vector3(2f * ra * fits, 2f * halfLength, 2f * rb * fits);
            return true;
        }

        /// <summary>
        /// The rotation that lays the unit sphere's Y along <paramref name="dir"/> and its X and Z
        /// as near the knuckle's two cross axes as that allows, so the bridge's two cross radii
        /// lie where the knuckle's do.
        /// </summary>
        private static Quaternion Orient(Vector3 dir, int axis)
        {
            var eb = Vector3.zero;
            eb[(axis + 2) % 3] = 1f;
            Vector3 forward = eb - dir * Vector3.Dot(eb, dir);

            if (forward.sqrMagnitude < 1e-6f)
            {
                var ea = Vector3.zero;
                ea[(axis + 1) % 3] = 1f;
                forward = ea - dir * Vector3.Dot(ea, dir);
            }

            return Quaternion.LookRotation(forward.normalized, dir);
        }

        /// <summary>
        /// The most the cross radii of an ellipsoid can be kept, as a fraction in [0, 1], with its
        /// bounding box inside the box of half extents <paramref name="h"/> about the origin.
        /// </summary>
        /// <remarks>
        /// The ellipsoid's reach along a frame axis j is the root of the sum of its three
        /// semi-axes' squared j components, so holding the cross radii to a fraction f of their
        /// size leaves the long axis's share alone and solves for f on each axis. A hundredth of a
        /// millimetre of float slack, so a bridge whose tip lies on the face it starts from is not
        /// refused for a rounding.
        /// </remarks>
        private static float BoxFit(
            Vector3 h, Vector3 centre, Quaternion rotation, float ra, float half, float rb)
        {
            Vector3 x = rotation * Vector3.right * ra;
            Vector3 y = rotation * Vector3.up * half;
            Vector3 z = rotation * Vector3.forward * rb;

            float f = 1f;

            for (int j = 0; j < 3; j++)
            {
                float room = h[j] + 1e-5f - Mathf.Abs(centre[j]);
                if (room <= 0f) return 0f;

                float spare = room * room - y[j] * y[j];
                float cross = x[j] * x[j] + z[j] * z[j];

                if (cross < 1e-10f)
                {
                    if (spare < -1e-8f) return 0f;
                    continue;
                }

                if (spare <= 0f) return 0f;
                f = Mathf.Min(f, Mathf.Sqrt(spare / cross));
            }

            return f;
        }

        /// <summary>
        /// How far along a ray from a point on a sphere or capsule part's collider the part's
        /// drawn surface is, before the carve; infinity when the ray misses it. Also the part's
        /// smallest drawn half extent, which is what the carve's depth is a fraction of.
        /// </summary>
        /// <remarks>
        /// The drawn shapes are the ones <see cref="Dress"/> and <see cref="Aspect"/> make: a
        /// sphere part as the genome's ellipsoid with its longest semi-axis the ball's radius; a
        /// near-spherical capsule as one ellipsoid at the centre (<see cref="NearlyASphere"/>); any
        /// other capsule as two caps squashed across the shaft and an elliptic shaft between them.
        /// </remarks>
        private static float DrawnHit(
            PhenotypePart own, Vector3 h, float radius, float span, Vector3 start, Vector3 dir,
            out float smallest)
        {
            if (own.ShapeId == ShapeIds.Sphere)
            {
                float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                Vector3 s = longest > 0f ? h * (radius / longest) : Vector3.one * radius;
                smallest = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                return RayEllipsoid(start, dir, Vector3.zero, s);
            }

            float across = Mathf.Max(h.x, h.z);
            float sx = across > 0f ? h.x / across : 1f;
            float sz = across > 0f ? h.z / across : 1f;
            smallest = radius * Mathf.Min(sx, sz);

            if (NearlyASphere(own))
            {
                return RayEllipsoid(
                    start, dir, Vector3.zero, new Vector3(radius * sx, Mathf.Max(h.y, radius), radius * sz));
            }

            var cap = new Vector3(radius * sx, radius, radius * sz);
            float hit = Mathf.Min(
                RayEllipsoid(start, dir, new Vector3(0f, span, 0f), cap),
                RayEllipsoid(start, dir, new Vector3(0f, -span, 0f), cap));

            return Mathf.Min(hit, RayShaft(start, dir, radius * sx, radius * sz, span));
        }

        /// <summary>Where a ray first meets an axis-aligned ellipsoid: zero from inside it, infinity when it misses.</summary>
        private static float RayEllipsoid(Vector3 origin, Vector3 dir, Vector3 centre, Vector3 semi)
        {
            if (semi.x <= 0f || semi.y <= 0f || semi.z <= 0f) return float.PositiveInfinity;

            Vector3 o = origin - centre;
            o = new Vector3(o.x / semi.x, o.y / semi.y, o.z / semi.z);
            var d = new Vector3(dir.x / semi.x, dir.y / semi.y, dir.z / semi.z);

            float qa = Vector3.Dot(d, d);
            float qb = 2f * Vector3.Dot(o, d);
            float qc = Vector3.Dot(o, o) - 1f;

            if (qc <= 0f) return 0f;
            if (qa <= 0f) return float.PositiveInfinity;

            float disc = qb * qb - 4f * qa * qc;
            if (disc < 0f) return float.PositiveInfinity;

            float t = (-qb - Mathf.Sqrt(disc)) / (2f * qa);
            return t >= 0f ? t : float.PositiveInfinity;
        }

        /// <summary>Where a ray first meets an elliptic shaft along Y of half-span <paramref name="span"/>.</summary>
        private static float RayShaft(Vector3 origin, Vector3 dir, float rx, float rz, float span)
        {
            if (span <= 0f || rx <= 0f || rz <= 0f) return float.PositiveInfinity;

            float ox = origin.x / rx, oz = origin.z / rz;
            float dx = dir.x / rx, dz = dir.z / rz;

            float qa = dx * dx + dz * dz;
            float qb = 2f * (ox * dx + oz * dz);
            float qc = ox * ox + oz * oz - 1f;

            float t;
            if (qc <= 0f)
            {
                t = 0f;
            }
            else
            {
                if (qa <= 0f) return float.PositiveInfinity;
                float disc = qb * qb - 4f * qa * qc;
                if (disc < 0f) return float.PositiveInfinity;
                t = (-qb - Mathf.Sqrt(disc)) / (2f * qa);
                if (t < 0f) return float.PositiveInfinity;
            }

            float y = origin.y + t * dir.y;
            return Mathf.Abs(y) <= span ? t : float.PositiveInfinity;
        }

        /// <summary>
        /// A leaf's frame as the shader picks it (<c>TheatreBody.shader</c>, <c>LeafAxes</c>): the
        /// thinnest axis, the longer of the other two, and the one left, ties in the same order.
        /// </summary>
        private static void LeafAxes(Vector3 h, out int along, out int thick, out int width)
        {
            if (h.x <= h.y && h.x <= h.z)
            {
                thick = 0;
                along = h.y >= h.z ? 1 : 2;
            }
            else if (h.y <= h.z)
            {
                thick = 1;
                along = h.x >= h.z ? 0 : 2;
            }
            else
            {
                thick = 2;
                along = h.x >= h.y ? 0 : 1;
            }

            width = 3 - thick - along;
        }

        /// <summary>
        /// The arch of a leaf's curl, signed: the term of its lift that holds on the midrib, as
        /// the shader's <c>LeafOf</c> draws it from the part's seed and the creature's own number.
        /// </summary>
        /// <remarks>
        /// A copy of five lines of the shader, and the one place this class repeats its
        /// arithmetic. It is needed because the curl lifts a thin blade's midrib by more than the
        /// blade is thick, so a bridge aimed at the box's middle plane can pass under the blade it
        /// is meant to reach. If <c>LeafOf</c> changes, this changes with it.
        /// </remarks>
        private static float LeafArch(float seed, float own)
        {
            float r6 = Frac(seed * 73.019f + 0.619f);
            float r7 = Frac(seed * 83.231f + 0.157f);
            float r8 = Frac(seed * 97.103f + 0.883f);

            float o6 = Frac(own * 79.137f + 0.83f);
            float o7 = Frac(own * 91.373f + 0.19f);

            r6 = Mathf.Clamp01(r6 + Mathf.Lerp(-0.2f, 0.2f, o6));
            r7 = Mathf.Clamp01(r7 + Mathf.Lerp(-0.2f, 0.2f, o7));

            float cup = Mathf.Lerp(-0.6f, 0.6f, r6);
            float arch = Mathf.Lerp(-0.4f, 0.4f, r7);
            float wave = Mathf.Lerp(0.35f, 0.8f, r8);
            float sum = Mathf.Abs(cup) + Mathf.Abs(arch) + wave;

            return arch * (sum > 1f ? 1f / sum : 1f);
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>A part's carve seed, as <see cref="Paint"/> hands it to the shader in <c>_Carve.x</c>.</summary>
        private static float PartSeed(Body body, int part)
        {
            float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
            return seed - Mathf.Floor(seed);
        }

        /// <summary>
        /// The direction from the parent's centre to the child's, in the parent's local frame.
        /// </summary>
        /// <remarks>
        /// The anchor alone would do for a joint on a face, and it is used when it is not
        /// degenerate; a joint whose anchor sits at the parent's centre has no face to point at,
        /// and there the child's own position is what says which way the limb leaves.
        /// </remarks>
        private static Vector3 Direction(PhenotypePart part, PhenotypePart parent, Vector3 anchor)
        {
            if (anchor.sqrMagnitude > 1e-8f) return anchor;

            Quaternion parentRotation = parent.Rotation.ToQuaternion();
            Vector3 offset = part.Position.ToVector3() - parent.Position.ToVector3();
            Vector3 local = Quaternion.Inverse(parentRotation) * offset;

            return local.sqrMagnitude > 1e-8f ? local : Vector3.up;
        }

        private static Vector3 Abs(Vector3 v) =>
            new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// The skin map's part-colour dial: the body's largest <see cref="PhenotypePart.DistanceFromRoot"/> over its parts (zero for a body
        /// of one part, or of parts all at the root). Called once a body, only while the dial is on.
        /// </summary>
        private static float MaxDistanceFromRoot(Phenotype phenotype)
        {
            float reach = 0f;

            for (int i = 0; i < phenotype.PartCount; i++) reach = Mathf.Max(reach, phenotype.Parts[i].DistanceFromRoot);

            return reach;
        }

        /// <summary>
        /// The skin map's part-colour dial: a part's own elongation u = 1 - exp(-ln(longest half extent / shortest)) = 1 - shortest / longest,
        /// in [0, 1): 0 for a cube or a sphere, near 1 for a thread or a sheet. A degenerate extent counts as 1e-6.
        /// </summary>
        private static float ElongationOf(PhenotypePart part)
        {
            Vector3 h = Abs(part.HalfExtents.ToVector3());
            float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
            float shortest = Mathf.Max(1e-6f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));

            return longest <= shortest ? 0f : 1f - Mathf.Exp(-Mathf.Log(longest / shortest));
        }

        /// <summary>
        /// The part index in <c>PhenotypeBuilder</c>'s name for a part — <c>Part07_n2</c> — or -1.
        /// </summary>
        private static int PartIndexOf(Transform partTransform)
        {
            if (partTransform == null) return -1;

            string name = partTransform.name;
            if (!name.StartsWith("Part", System.StringComparison.Ordinal)) return -1;

            int end = name.IndexOf('_');
            if (end < 0) end = name.Length;

            return int.TryParse(
                name.Substring(4, end - 4),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int index)
                ? index
                : -1;
        }

        public Color ColourOf(string cellTypeId) => TheatreTissue.Of(cellTypeId).Colour;

        /// <summary>Drops cached bodies whose root Unity has destroyed.</summary>
        /// <remarks>
        /// Called on a cadence rather than per death: the cache is one small array per creature,
        /// and a dead entry costs a dictionary slot until it is swept.
        /// </remarks>
        public void PurgeDead()
        {
            var gone = new List<long>();

            foreach (KeyValuePair<long, Body> entry in _bodies)
            {
                if (entry.Value.Root == null) gone.Add(entry.Key);
            }

            for (int i = 0; i < gone.Count; i++) _bodies.Remove(gone[i]);
        }

        public void Clear() => _bodies.Clear();

        /// <summary>
        /// The necks this palette drew for a body, each with the part whose joint to its parent it draws: what
        /// <see cref="TheatreOneSkin"/> reads to hide a neck between two parts its skin covers
        /// (EVOSIM_THEATRE_ONE_SKIN). False, with both lists empty, for a body not painted on this root. It reads
        /// and changes nothing, and nothing calls it with the dial off.
        /// </summary>
        public bool NecksOf(long id, Transform root, List<Transform> necks, List<int> joints)
        {
            if (necks == null || joints == null) return false;

            necks.Clear();
            joints.Clear();

            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root) return false;
            if (body.Necks == null || body.NeckPart == null) return false;

            for (int i = 0; i < body.Necks.Length && i < body.NeckPart.Length; i++)
            {
                necks.Add(body.Necks[i]);
                joints.Add(body.NeckPart[i]);
            }

            return true;
        }

        // ---------------------------------------------------------------- what the one skin reads
        //
        // Read-only, for TheatreOneSkin (EVOSIM_THEATRE_ONE_SKIN), so the skin draws what the palette draws
        // without a second copy of its arithmetic. Nothing calls these with the dial off.

        private MaterialPropertyBlock _readBlock;

        /// <summary>
        /// The body colour this palette last painted on one part of a body, as the shader receives it
        /// (<c>_BaseColor</c> read back from the part's first renderer, linear): the guild's colour muted,
        /// turned by lineage, greyed out of focus or dead, at the reserve's brightness. False for a body not
        /// painted on this root, or a part with no renderer.
        /// </summary>
        public bool PaintedColourOf(long id, Transform root, int part, out Vector4 colour)
        {
            colour = default;

            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root) return false;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (body.Part[i] != part || body.Renderers[i] == null) continue;

                if (_readBlock == null) _readBlock = new MaterialPropertyBlock();
                body.Renderers[i].GetPropertyBlock(_readBlock);
                if (_readBlock.isEmpty) return false;

                colour = _readBlock.GetVector(BaseColorId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The carve character this palette last painted on one part of a body, as the shader receives it: the
        /// part's <c>_Carve</c> (seed, lobe gain, wrinkle gain, depth share), and its <c>_Velvet</c> and
        /// <c>_LeafLook</c>, zero where the dial that sets one is off. What the one skin's carve at rest blends
        /// per vertex (EVOSIM_THEATRE_ONE_SKIN=3). False for a body not painted on this root, or a part with no
        /// renderer. It reads and changes nothing, and nothing calls it below level 3.
        /// </summary>
        public bool PaintedCharacterOf(long id, Transform root, int part, out Vector4 carve, out float velvet, out float leafLook)
        {
            carve = default;
            velvet = 0f;
            leafLook = 0f;

            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root) return false;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                if (body.Part[i] != part || body.Renderers[i] == null) continue;

                if (_readBlock == null) _readBlock = new MaterialPropertyBlock();
                body.Renderers[i].GetPropertyBlock(_readBlock);
                if (_readBlock.isEmpty) return false;

                carve = _readBlock.GetVector(CarveId);
                velvet = _readBlock.GetFloat(VelvetId);
                leafLook = _readBlock.GetFloat(LeafLookId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// One part's carve seed (the shader's <c>_Carve.x</c>), from the phenotype the body is first painted
        /// with and its id: the number ShapeBox draws the taper's end, the bend's direction and the organic
        /// silhouette's bulge and lean from.
        /// </summary>
        public static float CarveSeedOf(Phenotype phenotype, long id, int part) => PartSeed(SeedOf(phenotype, id), part);

        /// <summary>One part's own seed (the shader's <c>_Individual</c>), from the creature's id.</summary>
        public static float IndividualOf(long id, int part) => PartOwn(SeedOf(id), part);

        /// <summary>Whether the palette sets the organic silhouette (<c>_Organic</c>) on every part it paints.</summary>
        public static bool OrganicSilhouetteOn => OrganicDial > 0f;

        /// <summary>Whether it sets the organic soft tissue (<c>_OrganicTissue</c>) too.</summary>
        public static bool OrganicTissueOn => OrganicDial >= 2f;

        /// <summary>Whether the genome-to-skin map (<c>EVOSIM_THEATRE_SKIN_MAP</c>) is on; for the editor entries, which cannot see its class.</summary>
        public static bool SkinMapOn => TheatreSkinMap.Enabled;

        /// <summary>
        /// Says which genome a body painted under this id was grown from, for a caller outside this assembly (the stills entry,
        /// which paints one body at a time): the pattern's key and the skin vector, as the worlds say it with
        /// <see cref="TheatrePattern.Register"/> and <see cref="TheatreSkinMap.Register"/>. Does nothing with the skin map's dial off.
        /// </summary>
        public static void RegisterForSkinMap(long id, Genome genome, RunConfig config)
        {
            if (!TheatreSkinMap.Enabled) return;

            TheatrePattern.Register(id, genome);
            TheatreSkinMap.Register(id, genome, config);
        }

        /// <summary>
        /// Sets how much of a body is drawn, 1 whole to 0 gone: a newborn fading in, a corpse fading
        /// out. While it fades a renderer draws with two materials in place of its own: the
        /// see-through copy, which blends by <c>_Fade</c> after the backdrop and writes no depth,
        /// and a stand-in in the opaque range that draws no colour. The stand-in keeps the body in
        /// the depth prepass, which takes the opaque range only, so the depth of field reads the
        /// body's own distance through the fade; without it a newborn was blurred at the distance
        /// of the water behind it and snapped into focus in one frame when its fade ended. At 1 the
        /// renderer goes back to its own material. The block is read before it is written, as
        /// <see cref="Set"/> reads it, so a repaint keeps the fade and a fade keeps the paint. A
        /// material without the blend properties is left opaque.
        /// </summary>
        public void SetFade(Renderer[] renderers, float fade)
        {
            if (renderers == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            bool fading = fade < 1f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                Material first = renderer.sharedMaterial;
                if (first != null)
                {
                    Material own = _opaqueOf.TryGetValue(first, out Material opaque) ? opaque : first;
                    if (fading)
                    {
                        Material[] pair = FadingSetOf(own);
                        if (pair != null && !ReferenceEquals(first, pair[0])) renderer.sharedMaterials = pair;
                    }
                    else if (!ReferenceEquals(first, own) || renderer.sharedMaterials.Length != 1)
                    {
                        renderer.sharedMaterials = new[] { own };
                    }
                }

                renderer.GetPropertyBlock(_block);
                _block.SetFloat(FadeId, fade);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>The pair a fading body draws with in place of a body material, made once; null when it cannot blend.</summary>
        private Material[] FadingSetOf(Material opaque)
        {
            if (_fadingSetOf.TryGetValue(opaque, out Material[] found)) return found;

            Material[] made = null;
            if (opaque.HasProperty(SrcBlendId) && opaque.HasProperty(DstBlendId) && opaque.HasProperty(ZWriteId))
            {
                var seeThrough = new Material(opaque)
                {
                    name = opaque.name + " (fading)",
                    hideFlags = HideFlags.DontSave,
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
                };
                seeThrough.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                seeThrough.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                seeThrough.SetFloat(ZWriteId, 0f);
                seeThrough.SetOverrideTag("RenderType", "Transparent");

                // Zero and One leave the colour as it was, and no depth is written in the forward
                // pass, so the backdrop still fills behind the body; the depth passes are the
                // opaque body's own, in the opaque body's queue.
                var depth = new Material(opaque)
                {
                    name = opaque.name + " (fading, depth)",
                    hideFlags = HideFlags.DontSave,
                };
                depth.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.Zero);
                depth.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.One);
                depth.SetFloat(ZWriteId, 0f);

                made = new[] { seeThrough, depth };
                _opaqueOf[seeThrough] = opaque;
            }

            _fadingSetOf[opaque] = made;
            return made;
        }
    }
}
