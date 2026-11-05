// One material for every body in the theatre.
//
// Why it looks the way it does: plankton photographers light transparent animals by dark field,
// blocking the direct light so that only what scatters at an edge reaches the lens
// (research/theatre-look/README.md, "Lighting, before any material", [DF] [PC] [AS]). The
// rendered equivalent is a near black water, a raking key, and the guild colour carried on a
// Fresnel rim rather than painted on the face. So the guild reads at the edge, where a body is
// separated from the water, and not as a flat coloured box.
//
// The parts of it, and where each comes from:
//   Fresnel rim              [U1]
//   thickness faked subsurface, single pass, Battlefield 3's approximation in John Austin's
//                            URP form, no pre pass                                  [BB11] [JA]
//   Voronoi cell mottle      [U2], done in three dimensions here so it does not swim on a
//                            turning body
//   caustics from above, focused by the surface the theatre now actually draws, out of the one
//                            wave field it is drawn from (TheatreWater.hlsl)         [CY2] [AM]
//   inward vertex carve, two octaves of noise in the part's own object space, bounded,
//                            with the normal rebuilt per pixel from the same field         [CY]
//   taper and bend of a box part in its own object space, inward by construction and clamped
//                            into the mesh's box afterwards (logbook/specs/skin-spec-3.md)
//   a flat box drawn as a leaf (2026-09-24, the owner's remark about flat leaves and the owner's ruling on
//                            the curl): an outline, a lens cross-section, veins, and a curl of
//                            up to a tenth of the width, the one thing drawn outside a collider
//   sway and pulse at rest, a dial off by default (EVOSIM_THEATRE_SWAY): a blade bent only within
//                            the room its box has left, a float squeezed toward its centre plane
//   an organic silhouette for a box, a dial off by default (EVOSIM_THEATRE_ORGANIC): a rounder
//                            section, closed ends, an off-centre bulge and a lean, all inward
//
// The third day is about the box. The owner looked at the close views and said the spheres looked
// alive and the boxes still looked manufactured: six flat faces at right angles with a small
// rounding on the edges, which is a silhouette no amount of surface relief argues with. So a box
// is pillowed in the mesh (TheatreMeshes.Pillow), narrowed towards one end, and bent once across
// its longest axis. Spheres and capsules are left alone, and the reason is the size bound rather
// than taste: their colliders are the ball and the capsule, so the box clamp that makes the bend
// safe would not be holding them inside anything the physics has (TheatreMeshes.Finish).
//
// Batching. TheatrePalette paints every body through one MaterialPropertyBlock on one shared
// material, and that is kept: a block drops those renderers out of the SRP Batcher, which is the
// documented trade [U3], and the alternative (a material per body) breaks the batch far harder.
// The properties are still declared in a UnityPerMaterial block so the shader stays batcher
// compatible for any future palette that paints by guild instead of by body.
//
// There is no shadow caster pass. Bodies do not cast shadows here: at a couple of thousand of
// them that is a second full draw of the world for an effect a dark field cannot show anyway,
// and the key light is raking, so a cast shadow would fall out of frame in every view the
// snapshot camera takes. (Whether the key moves to the world's sun and casts is the owner's
// open ruling on the films review's first item; nothing here decides it.)
//
// There are depth passes, from 2026-09-24: DepthOnly and DepthNormals draw the body at exactly
// the shape the forward pass does, through one shared function (ShapeBody), so the camera's
// depth texture holds the carved, tapered, bent and leaf-shaped body a portrait focuses on.

Shader "Evosim/Theatre Body"
{
    Properties
    {
        [Header(Per body from TheatrePalette)]
        _BaseColor("Body colour", Color) = (0.10, 0.16, 0.17, 1)
        _RimColor("Guild rim colour", Color) = (0.34, 0.72, 0.36, 1)
        _Reserve("Reserve fraction, 0 starving to 1 sated", Range(0, 1)) = 1
        _Fade("Share of the body drawn, 1 whole to 0 gone (a birth, the end of a corpse)", Range(0, 1)) = 1
        [HideInInspector] _SrcBlend("Source blend, One opaque", Float) = 1
        [HideInInspector] _DstBlend("Destination blend, Zero opaque", Float) = 0
        [HideInInspector] _ZWrite("Depth write, 1 opaque", Float) = 1

        // The transmission is the guild's hue at a gain by guild (TheatrePalette): a producer
        // glows through when backlit, an eater stays opaque, a strut between. The design pass's
        // second ruling (2026-09-16): the guild reaches the face only as light coming through
        // the tissue, never as paint on it.
        _TransTint("Transmission tint, the guild's", Color) = (0.34, 0.72, 0.36, 1)
        _TransGain("Transmission gain by guild", Range(0, 2)) = 1

        // A wet sheen, by guild (TheatrePalette): the one surface the owner allowed the
        // absorptive tissue as its own (2026-09-16, no organ the genome does not encode), a
        // gut wall's gloss against a leaf's matte, a strut between. A highlight, not a colour.
        _Sheen("Sheen by guild", Range(0, 1)) = 0.1

        [Header(The look)]
        // Wider and softer than the second day's 2.6 and 1.5, by about a third. A Fresnel rim at
        // a high power is a bright line one or two pixels wide at the silhouette, which is the
        // hard edge the dark field was supposed to be getting rid of: it drew the outline of the
        // collider rather than the shape of the tissue. Lowering the power widens the band into
        // the body, and the strength comes down with it so that the total light on a body's edge
        // is about what it was and the guild still reads at the same brightness
        // (logbook/specs/skin-spec-3.md).
        _RimPower("Rim sharpness", Range(0.5, 8)) = 1.75
        _RimStrength("Rim strength", Range(0, 4)) = 1.3
        _GlowStrength("Inner glow at full reserve", Range(0, 1)) = 0.22
        _Wrap("Diffuse wrap", Range(0, 1)) = 0.45
        _KeyGain("Key gain", Range(0, 3)) = 1.0

        [Header(Subsurface)]
        _TransColor("Transmission colour", Color) = (1.0, 0.62, 0.38, 1)
        _TransScale("Transmission scale", Range(0, 4)) = 1.3
        _TransPower("Transmission sharpness", Range(1, 16)) = 4.0
        _TransDistortion("Transmission distortion", Range(0, 1)) = 0.35
        _TransMetres("Half extent that is fully translucent, metres", Range(0.001, 1)) = 0.06

        [Header(Mottle)]
        _MottleCellsPerMetre("Voronoi cells per metre", Range(0.5, 200)) = 26
        _MottleStrength("Mottle strength", Range(0, 0.5)) = 0.14

        [Header(Caustics)]
        // The net's own scale is no longer a property here: it is the sea's wavelength, which is
        // a global pushed once by TheatreSkin so that the body, the bed, the shafts and the
        // ceiling cannot be lit by four different surfaces (TheatreWater.hlsl). A metres per cell
        // dial beside it would be a fifth sea nobody could see was disagreeing.
        _CausticColor("Caustic colour", Color) = (0.55, 0.85, 0.95, 1)
        _CausticStrength("Caustic strength on bodies", Range(0, 2)) = 0.35

        // How far down the caustics still reach. Set by TheatreSkin from the same dial the shafts
        // and the ceiling fade on (EVOSIM_THEATRE_LIGHT_REACH), so the lit part of the world is
        // one depth rather than three.
        _CausticReach("Metres below the surface caustics reach", Range(1, 200)) = 18

        [Header(Carve)]
        // The dial the second day is about, and the bound is the whole point. The displacement
        // below is -normal * depth * noise with the noise in [0, 1], so it is never positive and
        // a carved vertex can only move away from the collider. depth is this fraction of the
        // part's SMALLEST half extent, which is at most that fraction of the half extent on any
        // axis, so the deepest impression on the longest body is still a fraction of its
        // thinnest dimension. TheatreSkin reads EVOSIM_THEATRE_CARVE into it and clamps.
        //
        // 0.35 rather than the second day's 0.2: the owner ruled it by eye on 2026-09-11 from the
        // close views taken at 0.1, 0.2 and 0.35 (logbook/specs/skin-spec-3.md).
        _CarveFraction("Carve depth, fraction of the smallest half extent", Range(0, 0.5)) = 0.35

        // The hard cap, after the joint pinch has deepened the carve. Nothing about the collider
        // needs it: it stops a body from being cut past its own middle and turning inside out.
        _CarveMaximum("Deepest carve of any kind, same fraction", Range(0, 0.6)) = 0.5

        _PinchGain("How much deeper the carve runs at a joint anchor", Range(0, 4)) = 1.6

        [Header(The box that stops looking made)]
        // Both are read from the environment by TheatreSkin (EVOSIM_THEATRE_TAPER,
        // EVOSIM_THEATRE_BEND) and both are for box parts only: the mesh says which those are
        // (TheatreMeshes.Finish), and a mesh that does not say reads zero and is not deformed.
        //
        // The taper is a fraction of the cross-section, so the far end is (1 - taper) of the near
        // one and the multiplier is never above one. The bend is a fraction of the smallest half
        // extent, and it is paid for out of the cross-section before it is spent, so it cannot
        // reach the wall either. Neither claim is what the size bound rests on: ShapeBox clamps
        // the object position into the mesh's own box whatever the dials say.
        _TaperFraction("Taper, fraction of the cross-section lost at the far end", Range(0, 0.8)) = 0.35
        _BendFraction("Bend, fraction of the smallest half extent", Range(0, 0.4)) = 0.15

        [Header(Per body from TheatrePalette)]
        // x seed, y lobe gain, z wrinkle gain, w how much carve this guild takes at all.
        _Carve("Carve: seed, lobes, wrinkles, character", Vector) = (0, 0.85, 0.30, 1)

        // A joint anchor in this visual's own object units, xyz, with its reach in w. Zero reach
        // is "no joint on this part", which is most of them.
        _PinchA("Joint anchor A", Vector) = (0, 0, 0, 0)
        _PinchB("Joint anchor B", Vector) = (0, 0, 0, 0)

        [Header(The leaf)]
        // A flat box is drawn as a leaf (TheatreMeshes.Lamina, ShapeLamina below). The curl is
        // the owner's ruling of 2026-09-24 and the only term in this shader that can put a
        // vertex outside the collider: a fraction of the leaf's width, at most a tenth, spent on
        // the thin axis. TheatreSkin clamps it again.
        _CurlFraction("Leaf curl, fraction of its width", Range(0, 0.1)) = 0.1
        _VeinStrength("Leaf veins", Range(0, 1)) = 0.6

        // The light from the surface coming through a blade seen from below: the glow of a kelp
        // canopy looked up at. Transmitted, so it follows the blade's thinness.
        _LeafSkyGlow("Leaf glow from above", Range(0, 2)) = 0.6

        // Per body: the part's own joint anchor in its object units, w one when it hangs from a
        // parent. A leaf's base is drawn at the end its joint is on.
        _Leaf("Leaf: own anchor, w one when it has one", Vector) = (0, 0, 0, 0)

        // Per part, from the creature's id rather than its plan: a blade's own small departure
        // from its family's outline, curl and tone, so no two siblings are drawn alike.
        _Individual("Individual seed", Range(0, 1)) = 0

        [Header(Absorptive uptake)]
        // The uptake look (TheatreUptake.cs, EVOSIM_THEATRE_UPTAKE), per absorptive visual: x is u in
        // [0, 1], how much this tissue is taking in against its run's 95th percentile, and y is the swell
        // in metres at that u. It is read only in the _EVO_UPTAKE variant, which only the dial turns on.
        _Uptake("Uptake: u, swell in metres", Vector) = (0, 0, 0, 0)

        [Header(Two dials off by default)]
        // Skin by tissue (EVOSIM_THEATRE_TISSUE_LOOK) and light through tissue (EVOSIM_THEATRE_SSS).
        // TheatrePalette sets each only while its dial is on; at zero every term behind them is
        // skipped by a branch, not multiplied away, so a frame is today's to the pixel.
        [ToggleUI] _TissueLook("Skin by tissue", Float) = 0
        _Banding("Muscle bands across the long axis, the tissue look's", Range(0, 0.5)) = 0
        [ToggleUI] _Sss("Light through tissue and glow", Float) = 0

        [Header(Papillae and a frilled edge, the bold tissue looks only)]
        // EVOSIM_THEATRE_TISSUE_LOOK=2. TheatrePalette sets these three only at 2, from the tissue's own
        // entry, and every term behind them is skipped by a branch on a value over zero, so at their
        // default of zero a frame is the one without them, whatever the other dials say.
        // Papillae: small raised bumps drawn in the normal and the colour (paler at the tip, darker at the
        // base) and never as a displacement. _Papillae is how strongly, _PapillaeCellsPerMetre their
        // feature points per metre (spacing is the inverse).
        // Frill: a blade's outline scalloped by cutting inward between the scallops; any other part's
        // silhouette pleated in shading.
        _Papillae("Papillae strength", Range(0, 1)) = 0
        _PapillaeCellsPerMetre("Papillae per metre", Range(0, 400)) = 0
        _Frill("Frilled edge strength", Range(0, 1)) = 0

        [Header(A third dial off by default)]
        // Soft silhouettes (EVOSIM_THEATRE_SOFT): a lower carve without terraces, a coarser and
        // gentler mottle, normals turned back toward the uncarved surface, and a softer rim.
        // TheatrePalette sets it only while its dial is on, and every term behind it is skipped by
        // a branch, so at zero a frame is the one without it, with the two dials above on or off.
        [ToggleUI] _Soft("Soft skin", Float) = 0

        [Header(A fourth dial off by default)]
        // Sway and pulse at rest (EVOSIM_THEATRE_SWAY): a blade bends about its anchor and a float
        // draws in along its smallest axis, slowly, on the shaders' clock. TheatrePalette sets both
        // only while its dial is on, and every term behind them is skipped by a branch, so at zero a
        // frame is the one without it, with the three dials above on or off.
        [ToggleUI] _Sway("Sway and pulse at rest", Float) = 0

        // Per body from TheatrePalette: x what moves (0 nothing, 1 a blade, 2 a float), y the phase
        // in cycles, z the cycles per second.
        _SwayPart("Sway: what moves, phase, cycles per second", Vector) = (0, 0, 0, 0)

        [Header(The soft dial at two)]
        // One smooth hull (EVOSIM_THEATRE_SOFT=2), over the soft skin: the carve all but gone with a faint
        // lobe kept, the normal turned most of the way back to the uncarved one and nearly all of it at a
        // joint, and a fine faint mottle. TheatrePalette sets it only at 2, beside _Soft, and every term
        // behind it is skipped by a branch, so below 2 a frame is the one without it.
        [ToggleUI] _SoftHull("One smooth hull", Float) = 0

        [Header(A measurement off by default)]
        // The board's light (EVOSIM_THEATRE_BOARD_LIGHT): the body lit as the reference board's photographs
        // are, by a white key and a neutral fill and ambient, with no depth light, caustics, water cuts or
        // fog. TheatrePalette sets it only while its dial is on, and every term behind it is skipped by a
        // branch, so at zero a frame is the one without it.
        [ToggleUI] _BoardLight("Board light", Float) = 0

        [Header(The organic silhouette off by default)]
        // A box part's outline drawn as an animal's or a plant's (EVOSIM_THEATRE_ORGANIC): a rounder
        // cross-section, ends that close like a seed's, a bulge a little off the middle and a slight lean,
        // all by scaling the section inward (ShapeBox, OrganicFactor). TheatrePalette sets it only while its
        // dial is on, and every term behind it is skipped by a branch, so at zero a frame is the one without it.
        [ToggleUI] _Organic("Organic silhouette", Float) = 0

        [Header(Organic soft tissue off by default)]
        // Soft tissue on the organic box (EVOSIM_THEATRE_ORGANIC=2): a carve with no terraces and shallower
        // wrinkles that run lengthwise, pores and a soft sheen in shading, and a thin ruffled margin on a flat
        // part (TissueCarve, TissueMargin and the fragment's pores). TheatrePalette sets it only at 2, beside
        // _Organic, and every term behind it is skipped by a branch, so below 2 a frame is the one without it.
        [ToggleUI] _OrganicTissue("Organic soft tissue", Float) = 0

        [Header(Soft mottle off by default)]
        // The mottle's tone blended between neighbouring cells and its cell walls left out
        // (EVOSIM_THEATRE_MOTTLE_SOFT, MottleSoft), so a strong mottle reads as tissue and not as a mosaic of
        // flat tiles. TheatrePalette sets it only while its dial is on, and the term is behind a branch.
        [ToggleUI] _MottleSoft("Soft mottle", Float) = 0

        [Header(Cell view off by default)]
        // EVOSIM_THEATRE_CELL_VIEW, a diagnostic: the part drawn in one flat colour for its cell type, lit simply
        // (Fragment, the branch on _CellView just after the normal is known). TheatrePalette sets _CellView and
        // _CellColour (a Color, linear) only while its dial is on; the term is behind a branch, so at the default of
        // zero a frame is the one without it.
        [ToggleUI] _CellView("Cell view", Float) = 0
        _CellColour("Cell view colour", Color) = (1, 1, 1, 1)

        [Header(Countershading off by default)]
        // The body's own colour darker where the surface faces up and paler where it faces down
        // (EVOSIM_THEATRE_COUNTERSHADE). TheatrePalette sets it only while its dial is on, and the term behind
        // it is skipped by a branch, so at zero a frame is the one without it, with any other dial on or off.
        [ToggleUI] _Countershade("Countershading", Float) = 0

        [Header(A colour ramp along each part off by default)]
        // EVOSIM_THEATRE_COLOUR_RAMP. _RampAxis is per visual, in object units: the ramp coordinate of an object
        // position p is dot(p, xyz) + w, zero at the part's base and one at its free end. _RampTip is the
        // tissue's multiplier on the body colour at the tip in xyz, and w one when the ramp is on. TheatrePalette
        // sets both only while its dial is on; the term is skipped by a branch on w, so at the default of zero a
        // frame is the one without it.
        _RampAxis("Ramp: axis and offset in object units", Vector) = (0, 0, 0, 0)
        _RampTip("Ramp: tip multiplier, w one when on", Vector) = (1, 1, 1, 0)

        [Header(Living leaves off by default)]
        // EVOSIM_THEATRE_LEAF: a blade's carve without facets, light through it when the key is behind it, and
        // a rim that ruffles out of plane (LeafCarve, the backlit term in the fragment, the ruffle in LeafPoint).
        // TheatrePalette sets it only while its dial is on, and every term behind it is skipped by a branch, so at
        // zero a frame is the one without it, with any other dial on or off. The palette sets 1 or 2 (the dial's
        // level); every level 1 term tests above a half, and level 2's (the margin's finer waves, kelp relief)
        // test above one and a half. The toggle only draws the inspector's checkbox.
        [ToggleUI] _LeafLook("Living leaves", Float) = 0

        [Header(Marine producers off by default)]
        // EVOSIM_THEATRE_ALGA: a photosynthetic blade drawn as a seaweed or a seagrass (AlgaShapeOf, AlgaOutline,
        // the twist in LeafPoint, the fragment's Alga* terms). _Alga is set per renderer by TheatrePalette, only while
        // its dial is on and only on a photosynthetic part: one on a living one, AlgaCorpse's share on a corpse's,
        // which keeps the form and takes that share of the wet sheen and the light through. _AlgaDeep and
        // _AlgaShallow are the multipliers on the body, rim and light-through colours at the floor and at the
        // waterline (rgb), w one when the depth tint is on. Every term is behind a branch on _Alga or on
        // _AlgaDeep.w, so at the defaults of zero a frame is the one without it, with any other dial on or off.
        _Alga("Marine producers", Float) = 0
        _AlgaDeep("Alga: colour multiplier at the floor, w one when on", Vector) = (1, 1, 1, 0)
        _AlgaShallow("Alga: colour multiplier at the waterline", Vector) = (1, 1, 1, 0)

        [Header(Velvet absorbers off by default)]
        // EVOSIM_THEATRE_VELVET: an absorber part drawn as a sponge or a colonial tunicate (a matte skin with a
        // soft bright rim, a regular pattern of pores, and one osculum). _VelvetOsculum is per visual: the
        // osculum's centre in object units in xyz and its radius in metres in w, or zero for none.
        // TheatrePalette sets both only while its dial is on, and every term behind them is skipped by a branch.
        // _Velvet is the dial's level, 1 or 2: level 2 swaps the pores for star systems (VelvetStars) and tests
        // above one and a half.
        [ToggleUI] _Velvet("Velvet absorbers", Float) = 0
        _VelvetOsculum("Velvet: osculum centre in object units, radius in metres in w", Vector) = (0, 0, 0, 0)

        [Header(Tapered and fringed links off by default)]
        // EVOSIM_THEATRE_LINK: a capsule part (a link) narrows from its base to its free end, and carries a segment
        // fringe in shading (LinkTaper, the fragment's Link* block). Both are per visual, from TheatrePalette, which
        // sets them only while its dial is on and only on a capsule's shaft and caps. _LinkAxis: x and y give the
        // coordinate along the whole capsule, t = x * (object y) + y, zero at the base pole and one at the free pole;
        // z and w are t at the base cap's join with the shaft and at the tip cap's. _LinkShape: x the capsule's length
        // in radii, y the tip's share of the base radius, w one when on. Every term is behind a branch on w, so at the
        // default of zero a frame is the one without it.
        _LinkAxis("Link: coordinate along the capsule, joins in z and w", Vector) = (0, 0, 0, 0)
        _LinkShape("Link: length in radii, tip share, w one when on", Vector) = (0, 0, 0, 0)

        [Header(A coloured margin on consumer bodies off by default)]
        // EVOSIM_THEATRE_RIM_BAND: a pale line inside a warm band round the outline of a box part, as a sea slug's
        // mantle margin is (RimBand, the fragment's colour step). Colour only, read at the undisplaced object
        // position. TheatrePalette sets it only while its dial is on, and to one only on a consumer part drawn in
        // colour; the term is skipped by a branch, so at the default of zero a frame is the one without it.
        [ToggleUI] _RimBand("Rim band on consumers", Float) = 0

        [Header(Chromatophore mottling off by default)]
        // EVOSIM_THEATRE_CHROMATOPHORES: a field of small pigment discs that open and close, with a slow band that
        // opens the ones it passes, as a cephalopod's skin has (Chromatophores, the fragment's colour step). The value
        // is the tissue's strength in [0, 1], not a toggle: zero is off, and the palette sets it only while its dial is
        // on, per renderer. The term is skipped by a branch, so at the default of zero a frame is the one without it.
        _Chromatophores("Chromatophore strength", Float) = 0

        [Header(Reaction diffusion skin pattern off by default)]
        // EVOSIM_THEATRE_PATTERN: a Gray-Scott field, one tile of a small library (TheatrePattern.cs), mapped onto the
        // part as pigment (PatternSkin, the fragment's colour step). _Pattern: x the tile's repeat length in metres, y the
        // tissue's strength in [0, 1] (zero is off), z the mapping's quarter-turns (0 to 3) and w the share of the tile
        // the pigment covers. _PatternShift: xy the offset into the tile, in [0, 1). _PatternTex: the field, R8, repeat.
        // TheatrePalette sets all three only while its dial is on; the term is skipped by a branch on y, so at the
        // default of zero a frame is the one without it.
        _Pattern("Pattern: tile metres, strength, quarter-turns, cover", Vector) = (0, 0, 0, 0)
        _PatternShift("Pattern: offset into the tile in xy", Vector) = (0, 0, 0, 0)
        _PatternTint("Pattern tint (rgb, a = share)", Color) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_SKIN_MAP_FAMILY (TheatreSkinMap.cs): a pattern family drawn over the body in a second colour. _FamilyParams: x the
        // family (1 bars, 2 eyespot, 3 edge outline), y the strength (zero is off: every term below is skipped by a branch on it, so at the
        // default a frame is the one without it), z the bar count. _FamilyColor: rgb the colour, a the share. The palette sets both only
        // while the dial is on.
        _FamilyColor("Family colour (rgb, a = share)", Color) = (0, 0, 0, 0)
        _FamilyParams("Family (mode, strength, bars, -)", Vector) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_SKIN_MAP_FAMILY_MIX (TheatreSkinMap.cs): the marks combine. _FamilyMix x a bitmask of the marks a body carries (1 bars,
        // 2 eyespot, 4 edge outline), y the edge outline's widening (what _FamilyParams.w carries for the single mode), zw zero. With x over
        // zero FamilyMask draws the largest share of the selected marks (_FamilyParams.y and .z, the strength and the bar count, stay as they
        // are); with x zero the single mode on _FamilyParams.x is what it was. The palette sets it only while the dial is on.
        _FamilyMix("Family mix (bitmask, edge widening, -, -)", Vector) = (0, 0, 0, 0)
        // The one skin's rest box (TheatreOneSkin, only while the family dial is on): _FamilyBox xyz the centre and w one, _FamilyExtent xyz the
        // half extents, in the rest metres the skin's rest position (restOS) is in. With w over a half the bars and the eyespot are laid
        // on the body's box and not on the skin object's unit one. Zero (no box) on a part and with the dial off.
        _FamilyBox("Family: one skin rest box centre, w one", Vector) = (0, 0, 0, 0)
        _FamilyExtent("Family: one skin rest box half extents", Vector) = (0, 0, 0, 0)
        // The one skin's eyespot place (TheatreOneSkin, only while EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT is on): _FamilySpot xyz the centre of the
        // body's largest part in the box's -0.5 to 0.5 units, w its radius in rest metres. With w over zero the eyespot is laid there, and
        // with w zero (a part, the dial off) 0.30 along the box's long axis as before.
        _FamilySpot("Family: one skin eyespot centre in box units, w radius", Vector) = (0, 0, 0, 0)
        _PatternTex("Pattern field", 2D) = "black" {}

        [Header(Thin film iridescence off by default)]
        // EVOSIM_THEATRE_IRIDESCENCE: the sheen's white becomes the colour of a thin wet film's interference, which
        // moves with the view and drifts over the skin (IridescentTint, the fragment's sheen step). Sheen colour only.
        // The value is the tissue's strength in [0, 1], not a toggle: zero is off, and the palette sets it only while its
        // dial is on, per renderer. The term is skipped by a branch, so at the default of zero a frame is the one without it.
        _Iridescence("Iridescence strength", Float) = 0

        [Header(The one skin off by default)]
        // EVOSIM_THEATRE_ONE_SKIN: the metres in the world of one rest metre on a one skin (TheatreOneSkin), the growth
        // its bones carry times the root's scale, for its per-vertex half thickness. Read on a skin mesh alone (the shape
        // channel's x above 2.5), and there a zero reads as one; TheatreOneSkin sets it on the skin's own block alone.
        _SkinScale("One skin: world metres per rest metre", Float) = 0

        // EVOSIM_THEATRE_ONE_SKIN=3, the carve at rest: one on the skin's own block at that level alone, and the length in
        // rest metres its one carve field is read over (the median of the parts' smallest half extents at rest). Read on a
        // skin mesh alone, behind the flag; zero is off, and the palette never sets either on a part.
        _SkinCarve("One skin: carve at rest (level 3)", Float) = 0
        _SkinCarveLength("One skin: the carve's length, rest metres", Float) = 1
// skin2parts:begin

        [Header(SKIN2 on a part off by default)]
        // EVOSIM_THEATRE_SKIN2, on a body part that is not the one skin: one on a part in colour drawn in animal tissue
        // (not a producer, a holdfast, a gas bladder, a stipe, a velvet absorber, a greyed body, a corpse or a neck), zero
        // elsewhere. TheatrePalette.PaintSkin2 sets it per renderer, only while the dial is on; the shader reads it in the
        // _EVO_SKIN2 variant alone, behind a test of the shape flag, so zero (the default) is a part as it was.
        _Skin2Part("SKIN2 on a part: one for animal tissue in colour", Float) = 0
// skin2parts:end

        [Header(Genome to skin off by default)]
        // EVOSIM_THEATRE_SKIN_MAP (TheatreSkinMap.cs): four of a body's skin vector the shader reads, per renderer. _SkinMap: x the
        // rim band's width as a multiple of RimBandInner's complement, y the rim band's warm colour turned about the grey axis
        // (radians), z the soft mottle's gain, w the countershade's share. _SkinMapOn is one on a body that has a vector. The
        // defaults are today's constants, and the shader reads them in the _EVO_SKIN_MAP variant alone, which only the dial turns
        // on, behind a test of _SkinMapOn, so a body with none is the body as it was.
        _SkinMap("Skin map: rim width, rim hue turn, mottle gain, countershade share", Vector) = (1, 0, 1.4, 1)
        _SkinMapOn("Skin map on a body: one for a body with a skin vector", Float) = 0

        [Header(The nose pit off by default)]
        // EVOSIM_THEATRE_NOSE_PIT (D242): a nose part (cell type nose) is drawn at its true size with an olfactory
        // pit cupped into the face at its sensing tip, the MouthPoint. TheatreNosePit sets both per visual and only
        // on a nose part, and only while its dial is on; the shader reads them in the _EVO_NOSE_PIT variant alone,
        // which only that class turns on. _Pit: xyz the tip in this visual's object units, w the pit's depth in
        // radii; zero is no pit. _PitRadii: the pit's radius along each object axis, in object units.
        _Pit("Nose pit: tip in object units, depth in radii in w", Vector) = (0, 0, 0, 0)
        _PitRadii("Nose pit: radius along each object axis", Vector) = (0, 0, 0, 0)
        // noseskin:begin

        // EVOSIM_THEATRE_NOSE_PIT=2 (the nose joined to the one skin): on the skin's own block TheatreNosePit.DressSkin sets
        // _PitSkin, the pit's centre in the body's rest metres in xyz and its radius there in w, and _PitSkinDepth, its depth
        // in rest metres; zero is no pit. The shader reads them in the _EVO_NOSE_SKIN variant alone, which only a skin built
        // with a nose turns on.
        _PitSkin("Nose pit on the one skin: centre in rest metres, radius in w", Vector) = (0, 0, 0, 0)
        _PitSkinDepth("Nose pit on the one skin: depth in rest metres", Float) = 0
        // noseskin:end

        [Header(The mouth opening off by default)]
        // EVOSIM_THEATRE_MOUTH2: an eating part (MouthPoint.IsEatingPart) wears an opening with a lip on its face at
        // MouthPoint.Local. TheatreMouth2 sets _Mouth and _MouthRadii per visual, on an eating part only and only while
        // its dial is on, and on the one skin's own block sets _Mouth.w and up to four _MouthSkinN; the shader reads
        // them in the _EVO_MOUTH2 variant alone, which only that class turns on. _Mouth: xyz the point in this visual's
        // object units, w the cup's depth in radii; zero is no opening. _MouthRadii: the radius along each object axis,
        // in object units. _MouthSkinN: on the one skin, a mouth's centre in the body's rest metres in xyz and its
        // radius there in w; zero is no mouth.
        _Mouth("Mouth: point in object units, depth in radii in w", Vector) = (0, 0, 0, 0)
        _MouthRadii("Mouth: radius along each object axis", Vector) = (0, 0, 0, 0)
        _MouthSkin0("Mouth on the one skin: centre in rest metres, radius in w", Vector) = (0, 0, 0, 0)
        _MouthSkin1("Mouth on the one skin: centre in rest metres, radius in w", Vector) = (0, 0, 0, 0)
        _MouthSkin2("Mouth on the one skin: centre in rest metres, radius in w", Vector) = (0, 0, 0, 0)
        _MouthSkin3("Mouth on the one skin: centre in rest metres, radius in w", Vector) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_MOUTH2_DISC: the oral disc round the mouth, only in the _EVO_MOUTH2 variant and only while _MouthDisc.a is over
        // zero, which TheatreMouth2 sets with its dial on. _MouthDisc: rgb the disc colour (the body's at another lightness, in the
        // shader's linear space), a its share. _MouthAxis: on a part, xyz the mouth's axis in the radii's frame (the part's axes). On the
        // one skin, _MouthAxisSkinN: xyz the Nth mouth's axis in the rest frame, w the stripes' phase about it. The stripes' count is
        // _MouthRadii.w.
        _MouthDisc("Mouth disc: colour in rgb, share in a, zero off", Color) = (0, 0, 0, 0)
        _MouthAxis("Mouth disc: axis on a part in xyz", Vector) = (0, 0, 0, 0)
        _MouthAxisSkin0("Mouth disc on the one skin: axis in xyz, phase in w", Vector) = (0, 0, 0, 0)
        _MouthAxisSkin1("Mouth disc on the one skin: axis in xyz, phase in w", Vector) = (0, 0, 0, 0)
        _MouthAxisSkin2("Mouth disc on the one skin: axis in xyz, phase in w", Vector) = (0, 0, 0, 0)
        _MouthAxisSkin3("Mouth disc on the one skin: axis in xyz, phase in w", Vector) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_MOUTH2_FLAT: the share of the way from the mouth's distance in space to its distance taken flat along its axis
        // (the disc's), so a mouth on a sharp rim is round seen along its axis; zero, the default, is today's.
        _MouthFlat("Mouth flat along its axis: share, zero off", Float) = 0
        // EVOSIM_THEATRE_MOUTH2_FLAT_SLACK: how far behind the mouth's plane, in radii, the flat distance reaches before the far side is
        // pushed back out; zero is MouthFlatSlack.
        _MouthFlatSlack("Mouth flat: slack behind the mouth in radii, zero the default", Float) = 0
        // EVOSIM_THEATRE_MOUTH2_BITE: the bite pulse. _MouthBite: seconds since the last bite onset, in film seconds, on a part in x and
        // on the one skin in x to w for _MouthSkin0 to _MouthSkin3; 10000 is idle. _MouthBiteParams: x the strength (the dial times
        // EVOSIM_THEATRE_MOUTH2_BITE_FLASH, zero off), y the flash share, z the test period, w 1 with EVOSIM_THEATRE_MOUTH2_GAPE on.
        // Written by TheatreMouth2 and TheatreMouthBite only with the dial on.
        _MouthBite("Mouth bite: seconds since the onset (part: x; skin: slots x to w), 10000 idle", Vector) = (10000, 10000, 10000, 10000)
        _MouthBiteParams("Mouth bite: strength in x, flash share in y, test period in z, zero off", Vector) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_MOUTH2_GAPE: the share the mouth is open before a bite, 0 to 1, on a part in x and on the one skin in x to w by
        // slot; read only while _MouthBiteParams.w is above zero. Written by TheatreMouth2 and TheatreMouthBite only with the dial on.
        _MouthBiteGape("Mouth gape: share open (part: x; skin: slots x to w)", Vector) = (0, 0, 0, 0)
        // EVOSIM_THEATRE_MOUTH2_RELIEF: the strength of the disc, toothed ring and gape kinds' relief (lip, inner wall, teeth), zero off.
        _MouthRelief("Mouth relief: strength, zero off", Float) = 0
        _MouthSmall("Mouth disc faded by its size on screen: strength, zero off", Float) = 0
        _MouthPlain("Mouth in the body's own plain colour: strength, zero off", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // The shape of a body, shared by all three passes (2026-09-24, the films review's third
        // item). Until then the shader had the forward pass alone, and the camera's depth texture
        // (which the depth of field, the ambient occlusion and the soft edges of the shafts and
        // the snow all read) took each body from the fallback's depth passes. Those draw the mesh
        // as it arrives: a box before its taper, bend and carve, and a leaf as the plain oval the
        // mesh is baked with, on the mesh's own axes rather than the part's (an inference from
        // how a fallback resolves a pass, not a frame debugger's reading). A portrait focused on
        // a leaf would have focused on a shape that is not on screen. So the displacement is one
        // function, ShapeBody, and the forward pass and the two depth passes below all call it.
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"
#if defined(_EVO_UPTAKE)
            #include "TheatreUptake.hlsl"
#endif

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _Reserve;
                float _Fade;
                float4 _TransTint;
                float _TransGain;
                float _Sheen;
                float _RimPower;
                float _RimStrength;
                float _GlowStrength;
                float _Wrap;
                float _KeyGain;
                float4 _TransColor;
                float _TransScale;
                float _TransPower;
                float _TransDistortion;
                float _TransMetres;
                float _LeafSkyGlow;
                float _Individual;
                float _MottleCellsPerMetre;
                float _MottleStrength;
                float4 _CausticColor;
                float _CausticStrength;
                float _CausticReach;
                float _CarveFraction;
                float _CarveMaximum;
                float _PinchGain;
                float _TaperFraction;
                float _BendFraction;
                float4 _Carve;
                float4 _PinchA;
                float4 _PinchB;
                float _CurlFraction;
                float _VeinStrength;
                float4 _Leaf;
                float _TissueLook;
                float _Banding;
                float _Sss;
                float _Soft;
                float _Sway;
                float4 _SwayPart;
                float _SoftHull;
                float _BoardLight;
                float _Papillae;
                float _PapillaeCellsPerMetre;
                float _Frill;
                float _Organic;
                float _OrganicTissue;
                float _MottleSoft;
                float _CellView;
                float4 _CellColour;
                float _Countershade;
                float4 _RampAxis;
                float4 _RampTip;
                float _LeafLook;
                float _Alga;
                float4 _AlgaDeep;
                float4 _AlgaShallow;
                float _Velvet;
                float4 _VelvetOsculum;
                float4 _LinkAxis;
                float4 _LinkShape;
                float _RimBand;
                float _Chromatophores;
                float4 _Pattern;
                float4 _PatternShift;
                float4 _PatternTint;
                float4 _FamilyColor;
                float4 _FamilyParams;
                float4 _FamilyMix;
                float4 _FamilyBox;
                float4 _FamilyExtent;
                float4 _FamilySpot;
                float _Iridescence;
                float _SkinScale;
                float _SkinCarve;
                float _SkinCarveLength;
#if defined(_EVO_SKIN2)
// skin2parts: the flag PaintSkin2 writes on a part.
                float _Skin2Part;
#endif
#if defined(_EVO_SKIN_MAP)
                float4 _SkinMap;
                float _SkinMapOn;
#endif
#if defined(_EVO_NOSE_PIT)
                float4 _Pit;
                float4 _PitRadii;
#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)
                float4 _PitSkin;
                float _PitSkinDepth;
#endif
#if defined(_EVO_MOUTH2)
                float4 _Mouth;
                float4 _MouthRadii;
                float4 _MouthSkin0;
                float4 _MouthSkin1;
                float4 _MouthSkin2;
                float4 _MouthSkin3;
                float4 _MouthDisc;
                float4 _MouthAxis;
                float4 _MouthAxisSkin0;
                float4 _MouthAxisSkin1;
                float4 _MouthAxisSkin2;
                float4 _MouthAxisSkin3;
                float _MouthFlat;
                float _MouthFlatSlack;
                float4 _MouthBite;
                float4 _MouthBiteParams;
                float4 _MouthBiteGape;
                float _MouthRelief;
                float _MouthSmall;
                float _MouthPlain;
#endif
#if defined(_EVO_UPTAKE)
                float4 _Uptake;
#endif
            CBUFFER_END

            // The marine producers' bend with the current (EVOSIM_THEATRE_ALGA_BEND, TheatreSkin.PushAlgaBend). A
            // global, outside the buffer: z the dial's strength (0 to 1), w one while the dial is on, and zero
            // while it is off, which TheatreSkin never sets then. x and y are unused. The flow itself is per
            // blade, in _SwayPart.w (AlgaBendFlow). Only LeafSway reads it, behind a test of w.
            float4 _EvoAlgaBend;
#if defined(_EVO_ALGA_ARC)
            // The blade's arc bend (EVOSIM_THEATRE_ALGA_ARC, TheatreSkin.PushAlgaArc). A global, outside the buffer: x the level
            // (1 or 2), zero while the dial is off or the look is held, which TheatreSkin never sets then. Only LeafSway reads it.
            // y (EVOSIM_THEATRE_ALGA_ARC_ROOM, a preview beyond D242): the share of the arc L the thin axis may reach beyond the box, 0
            // to 0.3; zero when the dial is off or the look is held. The block that reads it is behind a test of y above zero.
            float4 _EvoAlgaArc;
#if defined(_EVO_ALGA_FLUTTER)
            // z (EVOSIM_THEATRE_ALGA_FLUTTER): the flutter dial, 0 to 1; zero when the dial is off or the look is held. w is unused.
#if defined(_EVO_ALGA_FLUTTER_NOISE)
            // w (EVOSIM_THEATRE_ALGA_FLUTTER_NOISE): the flutter's noise dial, 0 to 1; zero when the dial is off or the look is held. It is
            // read only under this keyword, in the flutter's block of LeafSway (w is unused without it).
#endif
#endif
#if defined(_EVO_ALGA_ARC_DRIVE)
            // The arc's diagnostic (EVOSIM_THEATRE_ALGA_ARC_DRIVE, TheatreSkin.PushAlgaArc): 1, 2 or 3 while the dial is on and the look is not
            // held; never set with the dial off, so the global stays the zero a shader global is. Read only where the arc acts.
            float _EvoAlgaArcDrive;
#endif
#endif
#if defined(_EVO_BODY_DAPPLE)

            // Dappled light on bodies (EVOSIM_THEATRE_BODY_DAPPLE, TheatreSkin.PushBodyDapple). A global, outside the
            // buffer: the strength of the dapple on a body, 0.7, 1.0 or 1.4 for the dial's three steps, and zero while
            // the look is held (a census render), which draws the body caustic as it was. Never set with the dial off.
            float _EvoDapple;

            // The net the dapple is cut from: the same focusing the body caustic reads (EvoCausticGain, which is one at
            // the flat water), through a lower threshold and a gentler slope, so the cells are patches with bright
            // edges and not hairlines. (1 - DappleThreshold) * DappleScale is the soft base a flat surface gets, 0.25;
            // the gain reaches DappleThreshold + 1 / DappleScale (2.2) before the net is full.
            static const float DappleThreshold = 0.6;
            static const float DappleScale = 0.625;

            // The side wrap on the normal's y: a side face (y of zero) takes DappleSideFloor of the light, an upward
            // face all of it, and an underside little (none below a y of -DappleSideFloor / DappleSideSlope).
            static const float DappleSideFloor = 0.4;
            static const float DappleSideSlope = 0.6;

            float EvoDappleNet(float3 positionWS, float time)
            {
                return saturate((EvoCausticGain(positionWS, time) - DappleThreshold) * DappleScale);
            }
#endif

            // The skin pattern's field (_PatternTex, EVOSIM_THEATRE_PATTERN). Outside the buffer, as a texture is.
            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                // What solid this mesh is, baked per vertex by TheatreMeshes.Finish: x is one on
                // the box, two on the blade and zero on everything else, y is the mesh's own half
                // extent in object units. A one skin (TheatreOneSkin) carries three, with the vertex's own
                // half thickness in rest metres in y. A mesh that carries no fourth UV channel reads zero here, which is the
                // answer that leaves it undeformed, so an unrecognised mesh drawn with this
                // material gets the carve and nothing else.
                float2 shapeOS    : TEXCOORD3;

                // Where the vertex is on a leaf, for the leaf mesh alone (TheatreMeshes.Lamina):
                // s from base to tip, t from rim to rim, and which face. Zero on every other mesh.
                float4 leafOS     : TEXCOORD4;

                // A one skin's alone, read only behind its flag (shapeOS.x above 2.5): the vertex's place and normal
                // in the body's rest frame, in metres (UV5, UV6), which the skinning leaves alone, so the markings
                // ride on the tissue; and its tissue colour, the merged parts' painted colours blended by its
                // weights (the vertex colour), which takes the place of _BaseColor. No other mesh carries them.
                float3 restOS       : TEXCOORD5;
                float3 restNormalOS : TEXCOORD6;
                float4 skinColour   : COLOR;
#if defined(_ONE_SKIN)

                // A one skin's carve at rest (level 3), read only behind _SkinCarve and the skin's flag (UV7): the wrinkles'
                // share of the lobe and wrinkle gains, the depth share, and the weights of the soft-tissue box and of the
                // soft leaf, each blended from the nearest parts by the skinning weights (TheatreOneSkin.BlendCarve). A
                // mesh that carries no UV7 reads zero here, and nothing reads it.
                float4 skinCarve    : TEXCOORD7;
#endif
#if defined(_EVO_MOUTH2)

                // The one skin's skinning weight toward the part each of its (up to four) mouths is on, per vertex
                // (UV2; TheatreOneSkin): x the first mouth's part, y the second's, z the third's, w the fourth's. Read
                // on a skin mesh alone, and a skin built under the dial always carries it. No other mesh does.
                float4 mouthSkin : TEXCOORD2;
#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)

                // The one skin's skinning weight toward the part its nose is on, per vertex (UV1; TheatreOneSkin). Read on a
                // skin mesh alone and only behind _PitSkin.w: a mesh without the channel reads whatever Unity gives, and
                // nothing looks at it.
                float pitSkin : TEXCOORD1;
#endif
#if defined(_EVO_SKIN2_BAND4)

                // SKIN2_BAND at 4: the one skin's part-end mark (UV0, which a skin mesh leaves free and which the skin
                // writes only at that level): x from 0 in the middle of a part to 1 at its very end. Read on a skin mesh
                // alone, behind the skin's flag (Vertex).
                float2 band4End : TEXCOORD0;
#endif
            };

            // The part's half extents in metres, taken from the object to world matrix rather
            // than passed in. PhenotypeBuilder gives each visual a localScale equal to the part's
            // full size (VisualPlan), and the parts themselves are never scaled, so the column
            // lengths of the matrix are that size. Half of it is the half extent, exactly for the
            // cube and the sphere and conservatively (by a factor of two) for a capsule's shaft,
            // whose mesh is two units tall rather than one.
            // Which axis is longest or thinnest, asked of the extents above with a tolerance. Those are
            // column lengths of a rotated matrix, so two genome sides that are equal come out a few
            // units in the last place apart, and the sign of that gap changes from frame to frame as
            // the body turns. An exact comparison then swaps the axis: n6203's square slab, 36134, had
            // its taper and bend jump a quarter turn and back every few frames in round 62's swimmer
            // films (2026-10-24). Within a thousandth the sides count as equal and the fixed order
            // decides, which is what the order was always meant to do. The values are not changed.
            static const float AxisTieTolerance = 1e-3;
            bool AtLeast(float a, float b) { return a >= b * (1.0 - AxisTieTolerance); }
            bool AtMost(float a, float b) { return a <= b * (1.0 + AxisTieTolerance); }

            float3 HalfExtentsWS()
            {
                return 0.5 * float3(
                    length(UNITY_MATRIX_M._m00_m10_m20),
                    length(UNITY_MATRIX_M._m01_m11_m21),
                    length(UNITY_MATRIX_M._m02_m12_m22));
            }

            // How deep the carve is allowed to go on this vertex, in metres.
            //
            // Two multipliers on one fraction, and then a cap. The guild's character (_Carve.w)
            // makes a structural strut smoother than a leaf; the pinch deepens the cut near a
            // joint anchor so a junction reads as a waist rather than as one box entering
            // another. The cap is the only thing that stops the two compounding into a body cut
            // through its own middle; it says nothing about the collider, which the sign of the
            // displacement already settles.
            // The creature's own departure from its family's impressions (2026-09-24, the
            // owner: the small variations for every cell, not only the blades). The carve's
            // noise is read a little off where the plan's seed puts it: about a seventh of a
            // lobe, so the lobes stay the family's, and most of a wrinkle, so the wrinkles are
            // the individual's. The pinch at a joint is not moved, and nothing about the sign
            // changes, so the carve still only cuts inward.
            float3 OwnShift()
            {
                return (float3(
                    frac(_Individual * 11.31 + 0.17),
                    frac(_Individual * 23.93 + 0.61),
                    frac(_Individual * 37.17 + 0.43)) - 0.5) * 0.16;
            }

            // ---------------------------------------------------------------- the soft dial
            //
            // Soft silhouettes (_Soft, EVOSIM_THEATRE_SOFT). Every term from here on that names
            // _Soft reaches a vertex or a pixel only behind a test of it, and the palette never sets
            // it with its dial off, so a frame with the dial off is the frame from before it.

            // The carve's depth under the dial, as a share of today's at the same place, pinch and all.
            static const float SoftCarveDepth = 0.75;

            // The carve's two octaves at EvoCarve's own wavelengths (TheatreWater.hlsl: 1.8 lobes to an
            // object unit, five wrinkles to a lobe) and from its own offsets, so a body keeps the lobes
            // it has today and is recognisably the same body. Copied, because EvoCarve keeps them as
            // locals; the two must move together.
            static const float SoftLobeCycles = 1.8;
            static const float SoftWrinklesPerLobe = 5.0;

            // The wrinkles' share of the field under the dial, on the tissue's own wrinkle weight.
            // Lowered most of all: on today's carve most of the slope is the wrinkles'.
            static const float SoftWrinkleGain = 0.5;

            // The band the stretch spreads over. Today's, 0.28 to 0.72, saturates an eighth to a sixth
            // of a body at 0 or at 1, and those are the terraces: flats with the stretch's steepest
            // slope between them. A wider band takes most of them away. At 0.12 to 0.88, with the depth
            // at half and the wrinkles at a quarter, a body became a smooth pebble and lost its lobes in
            // the first renders, so the band, the depth and the wrinkles sit between that and today.
            static const float SoftBandLow = 0.18;
            static const float SoftBandHigh = 0.82;

            // One smooth hull (_SoftHull, EVOSIM_THEATRE_SOFT=2), over the soft dial's terms and only with
            // them. Every term that names _SoftHull reaches a vertex or a pixel only behind a test of it, and
            // the palette sets it only at 2, so below 2 a frame is the one without it.
            //
            // The carve's depth under the hull, as a share of the soft dial's, pinch and all: about a fifth of
            // today's carve, which keeps one faint lobe or two and no terrace, so a body is not an ellipsoid.
            static const float HullCarveDepth = 0.3;

            // The wrinkles' share of the field under the hull, on the soft dial's: nearly none, so what is
            // left of the carve is the lobes'.
            static const float HullWrinkleGain = 0.15;

            // EvoCarve with the wrinkles turned down and the wider band, in [0, 1], with its gradient,
            // in the part's own object units. The same smoothstep, so the field stays differentiable
            // and the rebuilt normal continuous, and the same range, so the carve still only cuts in.
            float SoftCarve(float3 p, float seed, float lobeGain, float wrinkleGain, float detail, out float3 gradient)
            {
                float wrinkleCycles = SoftWrinklesPerLobe * SoftLobeCycles * detail;

                float3 offset = EvoCarveOffset(seed);

                float3 lobeGradient, wrinkleGradient;
                float lobe = EvoValueNoise(p * SoftLobeCycles + offset, lobeGradient);
                float wrinkle = EvoValueNoise(p * wrinkleCycles + offset * 1.7, wrinkleGradient);

                float wrinkles = wrinkleGain * SoftWrinkleGain;

                // Under the hull the wrinkles all but go, before the total, so the field still runs 0 to 1.
                [branch] if (_SoftHull > 0.5) wrinkles *= HullWrinkleGain;

                float total = max(1e-4, lobeGain + wrinkles);

                float raw = (lobeGain * lobe + wrinkles * wrinkle) / total;

                float3 rawGradient = (lobeGain * SoftLobeCycles * lobeGradient +
                                      wrinkles * wrinkleCycles * wrinkleGradient) / total;

                float t = saturate((raw - SoftBandLow) / (SoftBandHigh - SoftBandLow));

                gradient = rawGradient * (6.0 * t * (1.0 - t) / (SoftBandHigh - SoftBandLow));

                return t * t * (3.0 - 2.0 * t);
            }

            // ---------------------------------------------------------------- the organic soft tissue
            //
            // Soft tissue on a box part (_OrganicTissue, EVOSIM_THEATRE_ORGANIC=2), over the organic silhouette
            // and with it. The organic dial's rounded box still reads as a worn pebble: the carve's ledges
            // (EvoCarve and SoftCarve stretch a noise through a smoothstep band, which saturates an eighth to a
            // sixth of a body at 0 or at 1, and those are flats with steep steps between them), a hard
            // highlight and a thick, blunt edge. A flatworm or a nudibranch has smooth skin with shallow
            // wrinkles, fine pores, a soft sheen and a thin, ruffled margin. Four terms, each behind a test of
            // _OrganicTissue (the fragment's through the tissue flag ShapeBody sets), and the palette sets it
            // only at 2, so at 0 and 1 a frame is the one without it.
            //
            // Box parts alone: ShapeBody's own test (the mesh's shape channel says box and not leaf), so a
            // sphere, a capsule and a leaf are drawn as they were.
            //
            // The honesty rule: the margin outside the part's collider is 0. Terms 1 and 2 only cut inward
            // (a field in [0, 1] through CarveDepth, whose cap has the last word, and ShapeBox's clamp after
            // it). Term 3 is shading. Term 4 thins the margin inward and moves the rim along the thin axis
            // only within the half thickness it has just given up (TissueMargin), and the clamp is still last.

            // 1. No ledges. The carve's depth under the dial, as a share of the fraction it would have had, before
            // the cap, so the cap still has the last word. Pinch and all, as the soft dial's is.
            static const float TissueCarveDepth = 0.5;

            // The lobe noise is mapped through t = 1/2 + 1/2 s / sqrt(1 + s^2), s = TissueStretch (raw - 1/2):
            // monotonic, smooth to every order and strictly inside (0, 1), so it has no flat at 0 or at 1 and no
            // band to leave a terrace. Value noise clusters near a half (a mean of eight uniform numbers), so
            // the stretch spreads the bulk of it over the field: at 5 a raw 0.25 to 0.75 comes out 0.11 to
            // 0.89, and the steepest slope is 2.5 against the carve's 3.4, so the walls are gentler as well.
            static const float TissueStretch = 5.0;

            // 2. Wrinkles along the long axis. Their share of the field is the guild's own share of wrinkle
            // in its mix (_Carve.z over _Carve.y plus _Carve.z) times this scale, capped, so a producer is still
            // more lobed and a stomach more wrinkled, and the structural parts smoother; the field is the mix
            // of the mapped lobes and the wrinkles by that share, so it stays in [0, 1]. At the tissues'
            // defaults (0.85 and 0.30) the share is about 0.23.
            static const float TissueCreaseScale = 0.9;
            static const float TissueCreaseMax = 0.5;

            // The crease noise is read in metres, so it is the same in size on every part whatever its
            // proportions: a wavelength across the part of this many of the smallest half extent, and this many
            // times as long along the longest axis, so the creases run lengthwise like the skin of a slug.
            static const float TissueCreaseAcross = 0.5;
            static const float TissueCreaseLength = 4.0;

            // The most cycles of the crease noise to an object unit on any axis. The mesh has 24 segments to
            // a face edge, about 24 to an object unit, so eight cycles is three segments to a crease.
            // Finer than that the vertex stage reads the noise as grit on the silhouette, while the normal
            // (rebuilt per pixel from the same capped field) still shows it.
            static const float TissueCreaseMaxCycles = 8.0;

            // The longest axis of a box part from its half extents, the same choice as ShapeBox's, with the
            // same tie tolerance and the same fixed order. The two must move together.
            float3 TissueLongAxis(float3 he)
            {
                if (AtLeast(he.x, he.y) && AtLeast(he.x, he.z)) return float3(1, 0, 0);
                if (AtLeast(he.y, he.z)) return float3(0, 1, 0);
                return float3(0, 0, 1);
            }

            // The soft tissue's carve field, in [0, 1], with its gradient in the part's own object units. The
            // lobes are SoftCarve's: the same wavelength (SoftLobeCycles) at the same offset (EvoCarveOffset), so
            // a body keeps the lobes it has today and is recognisably the same body, mapped through the smooth
            // stretch above in place of the band. The wrinkles are a second value noise in the part's metres,
            // long along the part's longest axis and short across it, at the offset EvoCarve gives its own.
            // Differentiable everywhere, as the rebuilt normal needs.
            float TissueCarve(float3 p, float seed, float lobeGain, float wrinkleGain, float3 he, out float3 gradient)
            {
                float3 offset = EvoCarveOffset(seed);

                float3 lobeGradient;
                float lobe = EvoValueNoise(p * SoftLobeCycles + offset, lobeGradient);

                float s = TissueStretch * (lobe - 0.5);
                float root = rsqrt(1.0 + s * s);
                float lobes = 0.5 + 0.5 * s * root;
                float lobesSlope = 0.5 * TissueStretch * root * root * root;

                float creaseShare = clamp(
                    wrinkleGain / max(1e-4, lobeGain + wrinkleGain) * TissueCreaseScale, 0.0, TissueCreaseMax);

                // Cycles to an object unit on each axis: the part's size on it over the wavelength on it.
                float smallest = min(he.x, min(he.y, he.z));
                float3 axisA = TissueLongAxis(he);
                float3 wavelength = TissueCreaseAcross * smallest * (1.0 + axisA * (TissueCreaseLength - 1.0));
                float3 cycles = min(2.0 * he / max(wavelength, 1e-5), TissueCreaseMaxCycles);

                float3 creaseGradient;
                float crease = EvoValueNoise(p * cycles + offset * 1.7, creaseGradient);

                gradient = (1.0 - creaseShare) * lobesSlope * SoftLobeCycles * lobeGradient +
                           creaseShare * cycles * creaseGradient;

                return (1.0 - creaseShare) * lobes + creaseShare * crease;
            }

            // 4. A thin frilled margin on a flattened part. Flattened: its smallest half extent is at most this
            // share of its middle one, within AtMost's tolerance (so a slab that is exactly half as thick as it
            // is wide, round 62's swimmers, is on the right side of the line on every frame). A box this thin
            // is drawn as a leaf when its sides are under TheatreMeshes.LeafThinness (0.4) of each other, and
            // a leaf is not a box here, so this reaches the boxes from 0.4 up to this ratio.
            static const float TissueFlatRatio = 0.55;

            // The margin starts where the section's distance from its centre, in the half extents of the two
            // long axes and read as a p-norm of 8 (the max norm has a kink at the corners, the organic dial's
            // reason for its corner power), is this far out, and the thickness falls from there by a smoothstep
            // to 1 - TissueMarginThin of itself from the rim inward, so at 0.6 the rim is 0.4 of the thickness.
            // The box mesh has about five segments between 0.6 and 1, enough for the fall to show as geometry.
            static const float TissueMarginStart = 0.6;
            static const float TissueMarginThin = 0.6;

            // The ruffle: the rim undulates along the thin axis by this share of the half thickness the
            // margin has given up, so at most 0.5 * 0.6 = 0.3 of the half thickness. Under one is the
            // bound: the thinned half thickness plus the ruffle is at most the box's own. Waves round the
            // perimeter are a whole number from low to low + span - 1 (whole, so the cut where the angle wraps
            // shows nothing), drawn from _Carve.x; the second harmonic, at twice the waves plus one, has this
            // share of the amplitude, so the wave is not a pure sine.
            static const float TissueRuffleShare = 0.5;
            static const float TissueRuffleWavesLow = 3.0;
            static const float TissueRuffleWavesSpan = 4.0;
            static const float TissueRuffleHarmonic = 0.3;

            // The margin of a flattened box, in the part's own object units, on the point and the normal in
            // place, before the organic silhouette's map. Writing a, b for the two long axes' coordinates over
            // the mesh half (what the rim is read from) and c for the thin coordinate in object units, the map
            // is
            //   c' = c m(a, b) + R(a, b),   m = 1 - TissueMarginThin w,   R = Share Thin h w wave(theta),
            // with a and b left alone, w the smoothstep of the rim distance and theta the angle round the
            // section. |c'| <= h (1 - Thin w (1 - Share)) <= h wherever |c| <= h (h the mesh half), so the
            // margin outside the collider is 0 before the clamp, which still has the last word. Its Jacobian
            // is [1, 0, 0; 0, 1, 0; g_a, g_b, m] (g the gradient of c' across the section), whose inverse
            // transpose takes the normal to (n_a - g_a n_c / m, n_b - g_b n_c / m, n_c / m). m is at least
            // 1 - Thin, so the divisions are safe. A part that is not flattened is left alone.
            void TissueMargin(inout float3 p, inout float3 n, float3 he, float3 axisA, float meshHalf)
            {
                // The thinnest axis, with the leaf's own tie rule (LeafAxes).
                float3 axisT;
                if (AtMost(he.x, he.y) && AtMost(he.x, he.z)) axisT = float3(1, 0, 0);
                else if (AtMost(he.y, he.z)) axisT = float3(0, 1, 0);
                else axisT = float3(0, 0, 1);

                // The middle half extent: the smaller of the two that are not the thinnest.
                float3 others = he + axisT * 1e6;
                float middle = min(others.x, min(others.y, others.z));

                if (dot(axisA, axisT) > 0.5) return;
                if (!AtMost(dot(he, axisT), TissueFlatRatio * middle)) return;

                float3 axisV = 1.0 - axisA - axisT;

                float a = dot(p, axisA) / meshHalf;
                float b = dot(p, axisV) / meshHalf;
                float c = dot(p, axisT);

                // The rim distance e, the p-norm of 8 of (a, b), read on the point scaled to the unit max norm so
                // nothing underflows (OrganicRound's way), and its gradient in (a, b). s is at least one wherever
                // one of r is one, which is everywhere but the face's centre, where big is clamped and r is zero:
                // there s would be zero and de nought over nought, a NaN normal the carve then carried into the
                // position, which the clamp snapped to the box's corner (a black spike from each face's middle).
                // The floor changes nothing elsewhere and makes de zero there.
                float big = max(max(abs(a), abs(b)), 1e-5);
                float2 r = abs(float2(a, b)) / big;
                float2 r2 = r * r;
                float2 r4 = r2 * r2;
                float2 r7 = r4 * r2 * r;
                float s = max(pow(r4.x * r4.x + r4.y * r4.y, 0.125), 1.0);
                float e = big * s;
                float2 de = (float2)sign(float2(a, b)) * r7 / pow(s, 7.0);

                float t = saturate((e - TissueMarginStart) / (1.0 - TissueMarginStart));
                float w = t * t * (3.0 - 2.0 * t);
                float2 dw = (6.0 * t * (1.0 - t) / (1.0 - TissueMarginStart)) * de;

                float thick = 1.0 - TissueMarginThin * w;

                // The ruffle's phase and count from the part's own numbers, mixed with constants of their own so
                // they line up with nothing else: _Carve.x for the count and the phase, _Individual for the phase.
                float waves = TissueRuffleWavesLow + floor(TissueRuffleWavesSpan * frac(_Carve.x * 5.719 + 0.233));
                float phase = 6.2831853 * frac(_Carve.x * 8.317 + _Individual * 3.71 + 0.519);

                float theta = atan2(b, a + 1e-7);
                float k1 = waves * theta + phase;
                float k2 = (2.0 * waves + 1.0) * theta + 2.3 * phase;
                float wave = (1.0 - TissueRuffleHarmonic) * sin(k1) + TissueRuffleHarmonic * sin(k2);
                float waveTheta = (1.0 - TissueRuffleHarmonic) * waves * cos(k1) +
                                  TissueRuffleHarmonic * (2.0 * waves + 1.0) * cos(k2);
                float2 dtheta = float2(-b, a) / max(a * a + b * b, 1e-8);

                float ruffle = TissueRuffleShare * TissueMarginThin * meshHalf * w * wave;

                // The gradient of c' across the section, in object units: the thinning's, then the ruffle's.
                float2 g = (-c * TissueMarginThin / meshHalf) * dw +
                           TissueRuffleShare * TissueMarginThin * (dw * wave + (w * waveTheta) * dtheta);

                float nA = dot(n, axisA);
                float nB = dot(n, axisV);
                float nC = dot(n, axisT);

                p = axisA * (a * meshHalf) + axisV * (b * meshHalf) + axisT * (c * thick + ruffle);
                n = axisA * (nA - g.x * nC / thick) + axisV * (nB - g.y * nC / thick) + axisT * (nC / thick);
            }

            // ---------------------------------------------------------------- living leaves
            //
            // A blade under the leaf dial (_LeafLook, EVOSIM_THEATRE_LEAF). Three terms, each behind a test of
            // _LeafLook and only on a blade (the lamina path), and the palette sets it only while its dial is on, so
            // with the dial off a frame is the one without it. This is the first: a carve with no facets. The
            // other two are the ruffle (LeafPoint) and the backlit glow (the fragment).
            //
            // 1. No facets. A blade's carve reads as polygon facets: flat cells (EvoCarve's and SoftCarve's band
            // saturates at 0 or at 1 over an eighth to a sixth of the sheet) with a crease between each. Under the
            // dial the field is TissueCarve's lobes alone, at SoftCarve's wavelength and offsets, through the same
            // kind of smooth monotone stretch (no band, so no flat, and no wrinkle octave to make a crease), at
            // this gentler stretch, and cut to LeafCarveShare of the depth the blade would have had. The depth
            // is still the blade's, capped at half the lens's thickness there (ShapeBody) before this share, so it
            // is at most LeafCarveShare / 2 of the lens's half thickness: the cut is inward, in [0, 1] times a
            // depth under the lens's own, and the drawn blade stays inside what it was. The midrib is not
            // touched: it is the lens's own ridge (LeafPoint) and the shading's (LeafVeins).
            static const float LeafCarveShare = 0.4;
            static const float LeafCarveStretch = 3.0;

            // The blade's carve field, in (0, 1), with its gradient in the part's own object units, for the
            // vertex stage and for the fragment's rebuilt normal, which must ask the same question.
            float LeafCarve(float3 p, float seed, out float3 gradient)
            {
                float3 offset = EvoCarveOffset(seed);

                float3 lobeGradient;
                float lobe = EvoValueNoise(p * SoftLobeCycles + offset, lobeGradient);

                float s = LeafCarveStretch * (lobe - 0.5);
                float root = rsqrt(1.0 + s * s);

                gradient = (0.5 * LeafCarveStretch * root * root * root * SoftLobeCycles) * lobeGradient;

                return 0.5 + 0.5 * s * root;
            }

            // The carve field and its gradient, today's or the soft dial's.
            float BodyCarve(float3 p, float smallest, out float3 gradient)
            {
                float carve;

                [branch] if (_Soft > 0.5)
                {
                    carve = SoftCarve(p, _Carve.x, _Carve.y, _Carve.z, EvoCarveDetail(smallest), gradient);
                }
                else
                {
                    carve = EvoCarve(p, _Carve.x, _Carve.y, _Carve.z, EvoCarveDetail(smallest), gradient);
                }

                return carve;
            }

            float CarveDepth(float3 positionOS, float smallest, float tissue)
            {
                float pinch = EvoPinch(positionOS, _PinchA) + EvoPinch(positionOS, _PinchB);

                float fraction = _CarveFraction * _Carve.w * (1.0 + _PinchGain * pinch);

                // Under the soft dial the whole of it, the pinch's share too, is lowered, so a waist
                // at a joint is still a waist, only shallower. Positive, so the cut is still inward,
                // and before the cap, so the cap still has the last word.
                [branch] if (_Soft > 0.5) fraction *= SoftCarveDepth;

                // Under the hull lower again, and still positive and before the cap: less carve is still
                // only inward, so the visual stays inside its collider.
                [branch] if (_SoftHull > 0.5) fraction *= HullCarveDepth;

                // Under the soft tissue (a box part with _OrganicTissue, the caller's flag) shallower again,
                // and still positive and before the cap.
                [branch] if (tissue > 0.5) fraction *= TissueCarveDepth;

                return min(fraction, _CarveMaximum) * smallest;
            }
#if defined(_ONE_SKIN)

            // ---------------------------------------------------------------- the skin's carve at rest
            //
            // A one skin at level 3 (_SkinCarve; EVOSIM_THEATRE_ONE_SKIN=3). Everything here is reached behind a test of
            // _SkinCarve and of the skin's flag, and the palette never sets _SkinCarve, so a frame without it is the frame
            // from before it. One field for the whole body: the carve functions above, read at the vertex's rest position
            // (UV5, rest metres) over one body-wide length (_SkinCarveLength) with the body's seed (_Carve.x on the skin's
            // block, the largest part's), where a part reads its own object units and its own seed. The field is
            // the blend of the three the parts use, by weights blended from the nearest parts: the plain carve (EvoCarve, or
            // SoftCarve under the soft dial), TissueCarve for a soft-tissue box and LeafCarve for a soft leaf. The lobe and
            // wrinkle gains come as the wrinkles' share, since every field uses them as a ratio. The gradient is in the
            // field's own units (a unit is one body-wide length); a weight's own gradient is left out, as the pinch's is.
            float SkinCarveField(float3 p, float wrinkleShare, float tissueWeight, float leafWeight, out float3 gradient)
            {
                float lobeGain = 1.0 - wrinkleShare;
                float bodyWeight = saturate(1.0 - tissueWeight - leafWeight);

                float carve = 0.0;
                gradient = float3(0.0, 0.0, 0.0);

                float3 part = float3(0.0, 0.0, 0.0);
                float value = 0.0;

                [branch] if (bodyWeight > 0.001)
                {
                    float detail = EvoCarveDetail(_SkinCarveLength);

                    [branch] if (_Soft > 0.5)
                    {
                        value = SoftCarve(p, _Carve.x, lobeGain, wrinkleShare, detail, part);
                    }
                    else
                    {
                        value = EvoCarve(p, _Carve.x, lobeGain, wrinkleShare, detail, part);
                    }

                    carve += bodyWeight * value;
                    gradient += bodyWeight * part;
                }

                [branch] if (tissueWeight > 0.001)
                {
                    // An object unit is twice the half extent, so a body-wide length is that of a box of half this.
                    float3 skinHalf = float3(0.5, 0.5, 0.5) * _SkinCarveLength;

                    value = TissueCarve(p, _Carve.x, lobeGain, wrinkleShare, skinHalf, part);

                    carve += tissueWeight * value;
                    gradient += tissueWeight * part;
                }

                [branch] if (leafWeight > 0.001)
                {
                    value = LeafCarve(p, _Carve.x, part);

                    carve += leafWeight * value;
                    gradient += leafWeight * part;
                }

                return carve;
            }

            // The skin's carve depth in metres: CarveDepth's fraction (the depth share, the soft dial's, the hull's and the
            // soft tissue's, then the cap, then the soft leaf's share, in CarveDepth's order) of the vertex's own half
            // thickness, and at most half of it. No pinch: the anchors are in a part's object units.
            float SkinCarveDepth(float depthShare, float tissueWeight, float leafWeight, float halfThickMetres)
            {
                float fraction = _CarveFraction * depthShare;

                [branch] if (_Soft > 0.5) fraction *= SoftCarveDepth;
                [branch] if (_SoftHull > 0.5) fraction *= HullCarveDepth;

                fraction *= 1.0 - tissueWeight * (1.0 - TissueCarveDepth);
                fraction = min(fraction, _CarveMaximum);
                fraction *= 1.0 - leafWeight * (1.0 - LeafCarveShare);

                return min(fraction, 0.5) * halfThickMetres;
            }
#endif

            // ---------------------------------------------------------------- the soft mottle
            //
            // The mottle's tone under EVOSIM_THEATRE_MOTTLE_SOFT. EvoVoronoi hands each point its nearest cell's
            // tone, one flat value per cell, and the fragment darkens a wall where two cells meet, so a strong
            // mottle (the bold tissue looks') reads as a mosaic of flat tiles. Here every cell of the same 27 is
            // weighted by exp(-MottleSoftSharpness d^2) on its squared distance d^2 in cells, and the tone is the
            // weighted mean of their tones: deep inside a cell its own tone, across a border a blend over about a
            // quarter of a cell, so the tones still differ cell to cell and no edge is drawn. The weights are taken
            // relative to the nearest cell's (exp of the difference), so nothing underflows. Still in [0, 1].
            static const float MottleSoftSharpness = 8.0;
            static const float MottleSoftGain = 1.4;

            float MottleSoft(float3 p)
            {
                float3 cell = floor(p);
                float3 frc = p - cell;

                float d2[27];
                float tones[27];
                float nearest = 8.0;

                [unroll]
                for (int i = 0; i < 27; i++)
                {
                    float3 offset = float3(i % 3, (i / 3) % 3, i / 9) - 1.0;
                    float3 seed = EvoHash3(cell + offset);
                    float3 rel = offset + seed - frc;
                    d2[i] = dot(rel, rel);
                    tones[i] = seed.x;
                    nearest = min(nearest, d2[i]);
                }

                float sumW = 0.0;
                float sumT = 0.0;

                [unroll]
                for (int j = 0; j < 27; j++)
                {
                    float w = exp(-MottleSoftSharpness * (d2[j] - nearest));
                    sumW += w;
                    sumT += w * tones[j];
                }

                return sumT / sumW;
            }

            // ---------------------------------------------------------------- the organic dial
            //
            // An organic silhouette for a box part (_Organic, EVOSIM_THEATRE_ORGANIC), so a slab reads as
            // a pebble or a flatworm's body and not as a box. Four terms, all on the two coordinates across
            // the long axis, and all one number: the section at a point is scaled toward the long axis by
            // a factor in (0, 1], the product of the four below, and the long axis is left alone, so the
            // part keeps its length. A section only ever shrinks toward its own centre, and the box's
            // section is convex about that centre, so every scaled point is inside the box wherever the
            // point was; ShapeBox's clamp still has the last word. Everything that names _Organic reaches a
            // vertex only behind a test of it, and the palette never sets it with its dial off, so a frame
            // with the dial off is the frame from before it.
            //
            // The amounts. Chosen so a square slab reads as a thick pebble and not as an egg: the corners
            // round, the ends close by about half, the middle swells by about a tenth against the ends.

            // 1. The section's superellipse, |a|^n + |b|^n <= 1, with a and b the two coordinates across
            // the long axis over their own half extents: 2 is an ellipse, infinity the box. 2.5 rounds a
            // square section and leaves a flat one an ellipse with full flanks.
            static const float OrganicSectionPower = 2.5;

            // The square is read as this norm rather than as the max norm. The max norm has a kink on the
            // diagonals that would put a crease in the shading at every edge of the box; a p-norm at 8 is
            // smooth and within about 9 % of the max norm at a corner, so the section's corners come in to
            // 0.83 of the box's rather than to the pure superellipse's 0.76.
            static const float OrganicCornerPower = 8.0;

            // 2. The ends. Over the last half of each half length the section closes along an elliptic
            // arc, to 1 - depth at the very end, so an end is a rounded cap of about half the section and
            // not a cut. The roundness is how far along the arc the end stops, under one so that the
            // arc's slope at the end stays finite and the normal well defined.
            static const float OrganicEndStart = 0.5;
            static const float OrganicEndDepth = 0.45;
            static const float OrganicEndRoundness = 0.9;

            // 3. The bulge: the section is full at its peak and 1 - depth at both ends, a parabola on
            // each side of the peak, so the body swells gently and does not look like a sausage.
            static const float OrganicBulgeDepth = 0.12;

            // 4. The peak's place, up to this far from the middle on the normalised length (whose ends
            // are -1 and 1), so within 15 % of the length either way.
            static const float OrganicBulgeShift = 0.3;

            // And the lean: toward one end the far side of the section, opposite a direction across the
            // long axis, is drawn in by up to this much, and the near side keeps its fill, so that end
            // leans that way. Under two thirds keeps the map one to one; this is far under.
            static const float OrganicLean = 0.14;

            // The section's term: S = N8(a, b) / N2.5(a, b), with its gradient in (a, b). N is a p-norm,
            // which falls as p rises, so S is at most one, and it is at least 2^(1/8 - 1/2.5) = 0.83. S
            // depends only on the direction of (a, b), so it is read on the point scaled to the unit max
            // norm (the sums are then at least one and nothing underflows), and its gradient, which falls
            // as one over the distance, is scaled back. A point on the long axis is left alone.
            float OrganicRound(float2 ab, out float2 gradient)
            {
                gradient = float2(0, 0);

                float2 m = abs(ab);
                float big = max(m.x, m.y);
                if (big < 1e-5) return 1.0;

                float2 r = max(m / big, 1e-6);
                float2 rn = pow(r, OrganicSectionPower);
                float2 rm = pow(r, OrganicCornerPower);
                float sumN = rn.x + rn.y;
                float sumM = rm.x + rm.y;

                float s = pow(sumM, 1.0 / OrganicCornerPower) / pow(sumN, 1.0 / OrganicSectionPower);

                // d ln S / d r_i = r_i^(m-1) / sum_m - r_i^(n-1) / sum_n.
                float2 dln = (rm / sumM - rn / sumN) / r;
                gradient = s * (float2)sign(ab) * dln / big;

                return min(s, 1.0);
            }

            // The organic factor at a point of a box, the gradient of the factor across the long axis (in
            // object units, on axisB and axisC) and its slope along it. along is the point's coordinate on
            // the long axis and v its two across, both in object units.
            float OrganicFactor(float along, float2 v, float meshHalf, out float2 gradient, out float slope)
            {
                float x = along / meshHalf;
                float2 ab = v / meshHalf;

                // 1. The rounder section.
                float2 roundGradient;
                float rounded = OrganicRound(ab, roundGradient);
                roundGradient /= meshHalf;

                // 2. The ends, closing on an elliptic arc from OrganicEndStart out, level where they start.
                float span = 1.0 - OrganicEndStart;
                float w = saturate((abs(x) - OrganicEndStart) / span);
                float arc = sqrt(1.0 - OrganicEndRoundness * w * w);
                float arcFull = 1.0 - sqrt(1.0 - OrganicEndRoundness);
                float ends = 1.0 - OrganicEndDepth * (1.0 - arc) / arcFull;
                float endsSlope = w > 0.0
                    ? -OrganicEndDepth / arcFull * (OrganicEndRoundness * w / arc) * (float)sign(x) / (span * meshHalf)
                    : 0.0;

                // 3 and 4. The bulge, its peak placed by the part's own number, mixed with constants of its
                // own so it does not line up with the taper's end, the bend's direction or the carve.
                float peak = OrganicBulgeShift * (2.0 * frac(_Carve.x * 5.381 + 0.447) - 1.0);
                float reach = x >= peak ? 1.0 - peak : 1.0 + peak;
                float d = (x - peak) / reach;
                float bulge = 1.0 - OrganicBulgeDepth * d * d;
                float bulgeSlope = -2.0 * OrganicBulgeDepth * d / (reach * meshHalf);

                // 4. The lean: a direction across the long axis and the end that leans, from the same
                // number. u is the point's place along that direction, -1 to 1 over the box's section.
                float leanAngle = 6.2831853 * frac(_Carve.x * 3.917 + 0.829);
                float leanEnd = frac(_Carve.x * 9.173 + 0.358) < 0.5 ? -1.0 : 1.0;
                float2 leanDirection = float2(cos(leanAngle), sin(leanAngle)) * 0.70710678;

                float uRaw = dot(leanDirection, ab);
                float u = clamp(uRaw, -1.0, 1.0);
                float toward = saturate(0.5 + 0.5 * leanEnd * x);
                float farSide = 0.5 - 0.5 * u;
                float lean = 1.0 - OrganicLean * toward * farSide;
                float2 leanGradient = (abs(uRaw) < 1.0 ? 1.0 : 0.0) *
                    (0.5 * OrganicLean * toward / meshHalf) * leanDirection;
                float leanSlope = -OrganicLean * farSide * 0.5 * leanEnd / meshHalf;

                float profile = ends * bulge;

                gradient = profile * (lean * roundGradient + rounded * leanGradient);
                slope = rounded * (lean * (endsSlope * bulge + ends * bulgeSlope) + profile * leanSlope);

                return rounded * profile * lean;
            }

            // A box part's living shape: a taper down its longest axis and one half wave of bend
            // across it, both in the part's own object space, both inward.
            //
            // Why here and not in the mesh. The taper narrows the part along its LONGEST axis and
            // the bend's amplitude is a fraction of its SMALLEST half extent, and neither of those
            // is known where the mesh is built: one unit cube is shared by every box in the world
            // and the part's proportions arrive as the object to world matrix. So the mesh carries
            // the pillowing, which is the same on every axis and can be baked, and the two dials
            // that need to know which axis is which are applied per vertex here.
            //
            // Why it is inside the collider, which is the one invariant of this skin. Three
            // separate reasons, in the order they apply. The taper multiplies the cross-section by
            // a number in [1 - taper, 1], so every vertex moves towards the long axis. The bend
            // shrinks the cross-section by its own amplitude before displacing by it, so the
            // outside of the curve reaches the wall at the deepest point of the bend and nothing
            // passes it. And then the object position is clamped into the box the mesh was built
            // inside, so a dial set past its range, a mesh that is not the one this was written
            // for, or an arithmetic slip cannot put a vertex outside the collider.
            void ShapeBox(
                inout float3 positionOS, inout float3 normalOS,
                float3 halfExtents, float smallest, float meshHalf)
            {
                float3 p = positionOS;
                float3 n = normalOS;

                // The longest axis, read from the part's own scale, and it is the axis a grown
                // thing has a direction along. Ties resolve in a fixed order, so a part with two
                // equal sides picks the same one every frame rather than swapping between them.
                float3 axisA;

                if (AtLeast(halfExtents.x, halfExtents.y) && AtLeast(halfExtents.x, halfExtents.z))
                    axisA = float3(1, 0, 0);
                else if (AtLeast(halfExtents.y, halfExtents.z))
                    axisA = float3(0, 1, 0);
                else
                    axisA = float3(0, 0, 1);

                // The two across it, in a fixed cyclic order, and the mask of both together.
                float3 axisB = float3(axisA.z, axisA.x, axisA.y);
                float3 axisC = float3(axisA.y, axisA.z, axisA.x);
                float3 across = 1.0 - axisA;

                // The soft tissue's margin on a flattened part (TissueMargin, _OrganicTissue), first of the
                // maps: it thins the section toward its rim along the thin axis and ruffles it there, and
                // hands the moved point and normal to the organic silhouette below. Inward by construction.
                [branch] if (_OrganicTissue > 0.5)
                {
                    TissueMargin(p, n, halfExtents, axisA, meshHalf);
                }

                // The organic silhouette (OrganicFactor), before the taper and the bend, so they shape
                // the organic body as they shaped the box. It is a map of its own, v' = o(p) v across the
                // long axis with the axis left alone, composed with theirs by handing them the moved
                // point and the moved normal: the composed normal is their inverse transpose applied to
                // this one's. Its Jacobian is [1, 0; v h, o I + v g^T] (g the factor's gradient across,
                // h its slope along), whose inverse transpose takes the normal to
                //   across: (n_across - g (v . n_across) / D) / o,  along: n_along - h (v . n_across) / D,
                // with D = o + g . v, the section's radial stretch. It stays positive: the rounding
                // depends on direction alone and adds nothing to it, and the lean keeps it above
                // 1 - 1.5 OrganicLean times the other three terms.
                [branch] if (_Organic > 0.5)
                {
                    float alongHere = dot(p, axisA);
                    float2 v = float2(dot(p, axisB), dot(p, axisC));
                    float nAlong = dot(n, axisA);
                    float2 nAcross = float2(dot(n, axisB), dot(n, axisC));

                    float2 organicGradient;
                    float organicSlope;
                    float organic = max(1e-3, OrganicFactor(alongHere, v, meshHalf, organicGradient, organicSlope));

                    float radial = max(1e-4, organic + dot(organicGradient, v));
                    float vn = dot(v, nAcross) / radial;

                    float2 vOut = organic * v;
                    float2 nAcrossOut = (nAcross - organicGradient * vn) / organic;
                    float nAlongOut = nAlong - organicSlope * vn;

                    p = axisA * alongHere + axisB * vOut.x + axisC * vOut.y;
                    n = axisA * nAlongOut + axisB * nAcrossOut.x + axisC * nAcrossOut.y;
                }

                float along = dot(p, axisA);

                // Which end narrows, and which way the body leans. Both are drawn from the number
                // the carve and the mottle are already drawn from: _Carve.x is the creature's id
                // mixed with the part index (TheatrePalette.Character), so this is a property of
                // the creature rather than of the frame and cannot flicker, and two bodies are
                // shaped differently. Mixed twice more so that the end and the direction do not
                // line up with the carve's own phase.
                float end = frac(_Carve.x * 13.37 + 0.271) < 0.5 ? -1.0 : 1.0;
                float angle = 6.2831853 * frac(_Carve.x * 7.919 + 0.613);

                // The taper, eased rather than straight: a linear narrowing is a wedge, which is a
                // made thing, and the eased one reads as a seed or a grain.
                float u = saturate(0.5 + 0.5 * end * along / meshHalf);
                float ease = u * u * (3.0 - 2.0 * u);
                float easeSlope = 6.0 * u * (1.0 - u) * (0.5 * end / meshHalf);

                float taper = 1.0 - _TaperFraction * ease;
                float taperSlope = -_TaperFraction * easeSlope;

                // The bend: one half wave, zero at both ends and deepest in the middle, in a
                // direction across the long axis. The amplitude is in world metres, a fraction of
                // the smallest half extent, so each axis divides by its own size to reach object
                // units: an object unit on an axis is the part's full size on it, twice the half
                // extent the matrix column gave.
                float3 amplitude =
                    axisB * (cos(angle) / max(1e-6, 2.0 * dot(halfExtents, axisB))) +
                    axisC * (sin(angle) / max(1e-6, 2.0 * dot(halfExtents, axisC)));

                amplitude *= _BendFraction * smallest;

                float t = saturate(0.5 + 0.5 * along / meshHalf);
                float wave = sin(3.14159265 * t);
                float waveSlope = 3.14159265 * cos(3.14159265 * t) * (0.5 / meshHalf);

                // What makes the bend affordable. A bend inside a box cannot be a translation of
                // the cross-section: the side the body leans towards is already against the wall,
                // so translating it would put it through the collider and leave the clamp to shave
                // it flat, which draws a plane exactly where the curve was meant to be. So the
                // cross-section gives the amplitude up before it spends it.
                float3 room = 1.0 - saturate(abs(amplitude) / meshHalf);

                // The two composed as one map of the lateral coordinates, p' = scale * p + offset,
                // with the long axis left alone (scale one, offset zero).
                float3 scale = axisA + across * (taper * room);
                float3 offset = across * (amplitude * wave);

                float3 scaleSlope = across * (taperSlope * room);
                float3 offsetSlope = across * (amplitude * waveSlope);

                scale = max(scale, 1e-3);

                positionOS = clamp(scale * p + offset, -meshHalf, meshHalf);

                // The deformed surface's normal: the inverse transpose of the map's Jacobian on
                // the old one. The map is triangular, because a lateral coordinate depends on
                // itself and on where the vertex is along the axis while the axis coordinate
                // depends on nothing, so the whole of it is two terms: divide the lateral
                // components by the scale, and tilt the axis component by the lateral slope.
                // Without it a tapered box shades like an untapered one and the taper only shows
                // on the outline. The carve rebuilds the pixel normal from what this leaves
                // (CarvedNormal), so this has to be right for the impressions to sit on the bend.
                float3 shear = across * ((scaleSlope * p + offsetSlope) / scale);

                normalOS = n / scale - axisA * dot(shear, n);
            }


            // ---------------------------------------------------------------- the leaf
            //
            // A flat box part is drawn as a leaf. The mesh is a sheet laid out by where each
            // vertex is on a leaf (s base to tip, t rim to rim, and a face), and everything a
            // leaf looks like is built from those here, per body, from the seed the carve
            // already draws from, so a crowd of one genome is not a crowd of one leaf.
            //
            // What is inside the collider and what is not. The outline is a width at most one
            // times the box's half width at every station, the cross-section a thickness at
            // most one times its half thickness, and the length exactly the box's: all of that is
            // inside. The curl lifts the whole sheet along the thin axis by up to _CurlFraction
            // of the leaf's width, both faces together, so the leaf keeps its thickness and
            // leaves the box by at most that much. That is the owner's ruling of 2026-09-24 and
            // nothing else in this shader leaves a collider.

            // A seaweed's blade, not a land plant's leaf (2026-09-24, the second pass: the first
            // pass's pointed tip, toothed margin and pinnate veins read as a tree's leaf, and
            // its teeth seen at an angle hung like legs). Kelp and sea lettuce have no veins
            // and no teeth; their tips are round, their bases taper to the stalk, their margins
            // are ruffled, and they let the light through.
            struct LeafShape
            {
                float widest;   // where the blade is widest: the exponent on s
                float baseFull; // how the base tapers: near one a wedge, under it rounder
                float tipFull;  // how round the tip is: smaller is blunter
                float lobe;     // how deep the lobes are, as a fraction of the width
                float lobes;    // how many lobes along the blade
                float cup;      // the curl across the width, signed
                float arch;     // the curl along the length, signed
                float wave;     // the frill at the margin
                float frills;   // how many ruffles along the margin
                float phase;

                // The marine producers' dial (_Alga, EVOSIM_THEATRE_ALGA; AlgaShapeOf). Zero, and read by
                // nothing, on a blade without it.
                float alga;        // one when the blade is drawn as a seaweed
                float form;        // 0 a sheet, 1 a strap, by the blade's own length over width
                float strap;       // the strap's share, 0 under AlgaStrapLow to 1 over AlgaStrapHigh
                float kelp;        // a long strap's faint midrib, 0 to 1
                float tipLen;      // the strap's round tip, as a share of the length
                float baseLen;     // the strap's taper at the stalk, as a share of the length
                float ruffleWaves; // the ruffle's waves round the outline, not whole (AlgaRuffleWave blends two)
                float twists;      // the twist's waves along the length
                float twistAmp;    // the twist at the rim, thin-axis object units, before the room bounds it
            };

            LeafShape LeafOf(float seed, float own)
            {
                LeafShape l;

                float r1 = frac(seed * 17.137 + 0.371);
                float r2 = frac(seed * 29.713 + 0.113);
                float r3 = frac(seed * 41.307 + 0.731);
                float r4 = frac(seed * 53.911 + 0.293);
                float r5 = frac(seed * 61.717 + 0.537);
                float r6 = frac(seed * 73.019 + 0.619);
                float r7 = frac(seed * 83.231 + 0.157);
                float r8 = frac(seed * 97.103 + 0.883);
                float r9 = frac(seed * 101.51 + 0.447);

                l.widest = lerp(0.75, 1.3, r1);
                l.baseFull = lerp(0.65, 1.1, r2);
                l.tipFull = lerp(0.22, 0.5, r3);
                l.lobe = r4 < 0.35 ? 0.0 : lerp(0.04, 0.13, r4);
                l.lobes = lerp(1.5, 3.5, r5);
                l.frills = lerp(3.0, 5.5, frac(r5 * 7.31 + r8));

                // The individual's departure from its family's blade: every term moved a little,
                // none so far that a sibling stops looking like its parent.
                float o1 = frac(own * 13.713 + 0.11);
                float o2 = frac(own * 27.191 + 0.53);
                float o3 = frac(own * 39.517 + 0.29);
                float o4 = frac(own * 51.313 + 0.71);
                float o5 = frac(own * 67.919 + 0.37);
                float o6 = frac(own * 79.137 + 0.83);
                float o7 = frac(own * 91.373 + 0.19);
                l.widest *= lerp(0.88, 1.12, o1);
                l.baseFull *= lerp(0.85, 1.15, o2);
                l.tipFull *= lerp(0.8, 1.2, o3);
                l.lobe *= lerp(0.6, 1.4, o4);
                l.frills += lerp(-0.6, 0.6, o5);
                r6 = saturate(r6 + lerp(-0.2, 0.2, o6));
                r7 = saturate(r7 + lerp(-0.2, 0.2, o7));
                r9 = frac(r9 + 0.3 * o5 + 0.2 * o6);

                // The three curls share one budget, so together they never lift a point by more
                // than the dial allows: each term below is at most its weight in size. The frill
                // takes the largest share: a ruffled margin is what says seaweed.
                float cup = lerp(-0.6, 0.6, r6);
                float arch = lerp(-0.4, 0.4, r7);
                float wave = lerp(0.35, 0.8, r8);
                float sum = abs(cup) + abs(arch) + wave;
                float k = sum > 1.0 ? 1.0 / sum : 1.0;

                l.cup = cup * k;
                l.arch = arch * k;
                l.wave = wave * k;
                l.phase = r9;

                l.alga = 0.0;
                l.form = 0.0;
                l.strap = 0.0;
                l.kelp = 0.0;
                l.tipLen = 0.0;
                l.baseLen = 0.0;
                l.ruffleWaves = 0.0;
                l.twists = 0.0;
                l.twistAmp = 0.0;

                return l;
            }

            // ---------------------------------------------------------------- the marine producers
            //
            // EVOSIM_THEATRE_ALGA (_Alga): a photosynthetic blade drawn as a seaweed or a seagrass and not
            // as a land plant's leaf. Its form comes from the blade's own proportions, its length over its
            // width (the two larger half extents, LeafAxes's L and W), smoothly and with no hash in the
            // character; the part's seed (_Carve.x) sets only phases (where a wave starts, which face a cup
            // or an arch bows toward) and _Individual nothing at all.
            //   A strap or ribbon (seagrass, a kelp's blade), over AlgaStrapLow to AlgaStrapHigh: parallel
            //   sides, a round tip a little frayed, a taper only at the stalk, and a gentle twist.
            //   A sheet (Ulva), under AlgaSheetLow to AlgaSheetHigh, centred on 1.8: broad, thin and round,
            //   its margin rippled by the leaf dial's level 2 ruffle, applied here even with that dial off.
            //   Between, the two blended (form, 0 a sheet to 1 a strap, half way between the two ramps).
            // No lobes (LeafScallop at zero depth, lobe zero), no veins, and no midrib or lens ridge but a
            // faint kelp's on a strap over AlgaKelpLow to AlgaKelpHigh times as long as it is wide.
            //
            // The bound is the leaf dial's, kept: every point is inside the room LeafPoint's ruffle is bound
            // by, the box's half thickness less the lens there less the curl's lift there. The outline is at
            // most one (AlgaStrapWidth is under it) and the fray only cuts in, so the width is inside the
            // box's; the lens's ridge only falls, so the lens is never thicker than the leaf dial's; the
            // curl's three weights keep their budget of one. The ruffle and the twist share
            // LeafRuffleRoomShare of the room (the twist AlgaTwistRoomShare of it on a strap, the ruffle the
            // rest), and the sway keeps the other half (LeafRoom halves its room under this dial as under
            // the leaf dial), so the lens, the curl, the ruffle, the twist and the bend together are inside
            // the collider box wherever the curl alone is. LeafRoom reads AlgaOutline before the fray, which
            // is at least the drawn width, so its bound is loose and not tight.
            static const float AlgaSheetLow = 1.5;       // length over width where the sheet begins to give way
            static const float AlgaSheetHigh = 2.1;      // and where it has gone; the two centred on 1.8
            static const float AlgaStrapLow = 2.5;       // where the strap begins
            static const float AlgaStrapHigh = 3.5;      // and where it is whole; the two centred on 3
            static const float AlgaKelpLow = 4.5;        // a strap's faint midrib begins
            static const float AlgaKelpHigh = 5.5;       // and is whole; the two centred on 5
            static const float AlgaKelpMidrib = 0.5;     // its share of the leaf's midrib (_VeinStrength) and ridge

            // The outlines. A sheet is sin(pi s) to AlgaSheetRound, broad and round at both ends. A strap is
            // AlgaStrapWidth wide along its whole length, narrowing at the stalk over baseLen (AlgaBaseHalfWidths of
            // the blade's half width, at most AlgaBaseLongest of the length) by a quarter sine to AlgaBasePower, and
            // closing at the tip in a half circle in metres, tipLen = AlgaStrapWidth hW / 2 hL of the length (between
            // AlgaTipShortest and AlgaTipLongest). The fray: shallow notches, AlgaFrayDepth deep and AlgaFrayPeriod of
            // the length apart (about six of the mesh's 96 stations), out of step on the two margins, over the last
            // AlgaFrayReach tips' lengths of a strap, inward only.
            static const float AlgaSheetRound = 0.5;
            static const float AlgaStrapWidth = 0.92;
            static const float AlgaBaseHalfWidths = 1.5;
            static const float AlgaBaseShortest = 0.05;
            static const float AlgaBaseLongest = 0.35;
            static const float AlgaBasePower = 0.6;
            static const float AlgaTipShortest = 0.03;
            static const float AlgaTipLongest = 0.5;
            static const float AlgaFrayDepth = 0.1;
            static const float AlgaFrayPeriod = 0.06;
            static const float AlgaFrayReach = 2.0;
            static const float AlgaFrayBeat = 2.618;        // the period's slow wander, in notch periods
            static const float AlgaFraySwing = 1.618034;    // the depth's cosine, in notch periods: incommensurate, and
                                                            // slow enough for the 96 stations to draw (a faster one aliased)
            static const float AlgaFrayDepthLeast = 0.4;    // of AlgaFrayDepth; the depth is 0.04 to 0.12
            static const float AlgaFrayDepthMost = 1.2;
            static const float AlgaThinHalf = 0.08;         // a blade's half thickness at most, of its half width
            static const float AlgaToneMiddle = 0.85;       // the tone across a blade, flat: its middle
            static const float AlgaToneEdge = 1.05;         // and its rim
            static const float AlgaCurlBudget = 0.6;        // what the curl's three weights add up to (a budget of one less the room)

            // The curl's three weights, from the form (a sheet's first, a strap's second), scaled to add up to
            // AlgaCurlBudget (under the budget of one LeafOf gives them, so the ruffle and the twist keep room on a thin blade): a sheet cups and frills more, a strap arches gently along its length. The frill's
            // count along the margin is AlgaFrillsPerAspect times the length over width, between the two limits.
            static const float AlgaCupSheet = 0.35;
            static const float AlgaCupStrap = 0.12;
            static const float AlgaArchSheet = 0.15;
            static const float AlgaArchStrap = 0.3;
            static const float AlgaFrillSheet = 0.5;
            static const float AlgaFrillStrap = 0.2;
            static const float AlgaFrillsPerAspect = 1.2;
            static const float AlgaFrillsFewest = 3.0;
            static const float AlgaFrillsMost = 12.0;

            // The ruffle's waves round the outline, AlgaRuffleWavesBase plus AlgaRuffleWavesPerAspect a unit of length
            // over width, between the leaf dial's fewest and AlgaRuffleWavesMost; not a whole number, so two whole
            // counts are blended (AlgaRuffleWave).
            static const float AlgaRuffleWavesBase = 4.0;
            static const float AlgaRuffleWavesPerAspect = 2.0;
            static const float AlgaRuffleWavesFewest = 6.0;    // the leaf dial's fewest (LeafRuffleWavesLow)
            static const float AlgaRuffleWavesMost = 16.0;

            // The twist (LeafPoint): the sheet moved along its thin axis by its distance across times a sine along
            // the length, the margins going opposite ways, so the blade reads as turning about its midline. It is
            // AlgaTwistAmplitude of the half width at the rim before the room bounds it, AlgaTwistsPerAspect waves a
            // unit of length over width (between the two limits), and at most AlgaTwistRoomShare of the room, on a
            // strap alone (times its share). Faded at the stalk, as the ruffle is.
            static const float AlgaTwistAmplitude = 0.12;
            static const float AlgaTwistsPerAspect = 0.15;
            static const float AlgaTwistsFewest = 0.5;
            static const float AlgaTwistsMost = 2.0;
            static const float AlgaTwistRoomShare = 0.25;   // of the room; at most LeafRuffleRoomShare

            // A blade's form from its length over its width: x the sheet's share, y the strap's, z the kelp
            // midrib's, w the form itself (0 a sheet, 1 a strap). The vertex stage, the fragment and the palette
            // (TheatrePalette.AlgaFormOf) read the same numbers; the two must move together.
            float4 AlgaFormOf(float aspect)
            {
                float sheet = 1.0 - smoothstep(AlgaSheetLow, AlgaSheetHigh, aspect);
                float strap = smoothstep(AlgaStrapLow, AlgaStrapHigh, aspect);
                float kelp = strap * smoothstep(AlgaKelpLow, AlgaKelpHigh, aspect);
                return float4(sheet, strap, kelp, 0.5 * (1.0 - sheet) + 0.5 * strap);
            }

            // The fragment's form, from the aspect the vertex stage carries in leaf.w (1 + hW / 2 hL).
            float4 AlgaFormOfLeaf(float leafW)
            {
                return AlgaFormOf(1.0 / max(1e-6, 2.0 * (leafW - 1.0)));
            }

            // The round form, a fan (Padina, the producers board's 10 and 11): a blade whose length over width is under
            // AlgaRoundLow, 1, to over AlgaRoundHigh, 0, which is under where the sheet (Ulva) begins to give way
            // (AlgaSheetLow); the palette (TheatrePalette.AlgaRoundOf) reads the same two numbers, and the two must
            // move together. A round blade takes (a) faint concentric growth bands in tone, AlgaBands of them over its
            // length from the stalk, AlgaBandDepth either way, (b) its margin rolled toward the face its cup bows
            // toward, the outer AlgaRollWidth of the way out, AlgaRollHeight of the half width at the edge, bounded by
            // AlgaRollRoomShare of the room (taken off the ruffle's share, so the ruffle and the roll together keep the
            // share the ruffle had), and (c) the palette's ochre. Under AlgaRoundOfAspect only; zero on a sheet or a strap.
            static const float AlgaRoundLow = 1.2;
            static const float AlgaRoundHigh = 1.5;
            static const float AlgaRollWidth = 0.15;         // of the way out, the margin that rolls
            static const float AlgaRollHeight = 0.12;        // of the half width, at the edge, before the room bounds it
            static const float AlgaRollRoomShare = 0.25;     // of the room, at most; at most LeafRuffleRoomShare less the ruffle's
            static const float AlgaBands = 6.0;              // growth bands over the blade's length
            static const float AlgaBandDepth = 0.14;         // the tone either side of one: pale and dark rings

            float AlgaRoundOfAspect(float aspect)
            {
                return 1.0 - smoothstep(AlgaRoundLow, AlgaRoundHigh, aspect);
            }

            float AlgaRoundOfLeaf(float leafW)
            {
                return AlgaRoundOfAspect(1.0 / max(1e-6, 2.0 * (leafW - 1.0)));
            }

            // The growth bands' tone at a point of a blade, a factor about one: rings centred on the blade's base (the
            // stalk end, s zero, the midline), in units of the blade's length, from the fragment's leaf (s, the distance
            // across in half widths, the aspect). A soft square wave, so a ring has a pale and a dark edge, and the
            // seed's phase sets where the first one is; nothing at the attachment itself.
            float AlgaBandTone(float s, float acrossHalfWidths, float leafW)
            {
                float aspect = 1.0 / max(1e-6, 2.0 * (leafW - 1.0));
                float rho = sqrt(s * s + (acrossHalfWidths / (2.0 * aspect)) * (acrossHalfWidths / (2.0 * aspect)));
                float phase = frac(_Carve.x * 7.919 + 0.311);
                float ring = smoothstep(-0.5, 0.5, sin(6.2831853 * (AlgaBands * rho + phase))) - 0.5;
                return 1.0 + 2.0 * AlgaBandDepth * ring * smoothstep(0.08, 0.25, rho);
            }

            // ---------------------------------------------------------------- the drifting frond
            //
            // A free-drifting blade (one of a body with no anchoring part; the palette sets _AlgaShallow.w to one,
            // which nothing else reads, and the decode is `_AlgaShallow.w > 0.5`) is drawn as a Sargassum-like frond and
            // not as a pond's floating leaf (the producers board's 12 and 13). It is the blade of this dial with its
            // outline, curl and lens replaced; only code that already runs under _Alga reaches it. The frond is marked in
            // the shape by l.alga = 2 (every other test of l.alga is `> 0.5` or `< 0.5`, true and false as for one), set
            // by AlgaFrondOf, and the fragment reads the flag from _AlgaShallow.w itself.
            //   The outline: a thin stem down the middle, AlgaFrondStem of the half width, and on each side three rows of
            //   blades. A row is a run of broad blades, each a bump in the width along the stem, a quick rise to a
            //   pointed apex and a shorter fall, with fine teeth. The first row has 4 to 7 blades a side by length over
            //   width; the second, 1.5 times the count plus one and AlgaFrondSecondSize the size, and the third, 2.2 times
            //   the count plus two and AlgaFrondThirdSize the size, sit among them, and the outline is the widest of the
            //   three, so the blades overlap and fill each other's gaps. Each blade has its own size
            //   (AlgaFrondSizeLeast to one) and its own length (the row's cells are stretched and squeezed by
            //   AlgaFrondJitter), both smooth functions of the blade's number and the seed's phases, so the blades are
            //   uneven in size and spacing and nothing repeats at a fixed pitch. The two sides are out of step by
            //   AlgaFrondSide (LeafScallopSide's 0.41). Under the blades runs a thin web, AlgaFrondWeb of the envelope, so
            //   the gaps between blades do not reach down to the stem. Every term is a factor in [0, 1], so the width is
            //   the stem's plus (one less the stem's) times the envelope times the larger of the web and the blades: at
            //   most one, only ever inward.
            //   The bladders: a few round beads on the stem. A bead is a disc of radius AlgaFrondBladderRadius of the half
            //   width in the plane of the blade, and a thickness of rho (a share of hT, AlgaFrondBladderShare at most)
            //   standing off it, at a fixed place on the stem; the blades are cut away round it so it stands clear.
            //   The lens: round on the stem and AlgaFrondLeafLens of that on a blade, never past the lens of the leaf
            //   dial (one), and the bead's own where that is thicker, so the bead replaces the lens there.
            //   The droop: the blades hang out of the plane, each side by the square of its distance from the stem times
            //   a slow wave along the stem (AlgaFrondDroop of the half width at the most, toward the face the arch bows
            //   toward, with the wave mostly one way and a few blades curling the other); see LeafPoint and LeafRoom.
            // The bound. The width is at most one: the blades by construction and a bead at AlgaFrondBladderRadius <= 0.2.
            // The thickness is the stem's, under the flesh cap with its lens taken at one (AlgaFleshCap), or a bead's, at
            // most AlgaFrondBladderShare of the box's half thickness less the arch's lift there (AlgaFrondOf), and the
            // frond has no cup and no frill, so the arch is the only lift and the bead and the arch together are inside the
            // box. LeafRoom counts the bead's thickness over the whole bead zone, and the droop's most over the outline, so
            // the sway keeps what is left. The curl (the arch and the droop together, at most AlgaFrondArch times 0.2 plus
            // AlgaFrondDroop of the half width, under the 0.2 that TheatreMeshes.LeafBounds allows) may leave the box as
            // ruled, and nothing here adds to that.
            // The shape's own fields carry the frond's numbers, since the fields of a blade it does not use are free:
            //   l.ruffleWaves  blades a side in the first row   l.frills   bladders on the stem
            //   l.kelp         a bladder's thickness, in the box's half thicknesses (rho over hT)
            //   l.baseLen      a bladder's width, in half widths   l.tipLen   a bladder's radius, in lengths of the blade
            //   l.twists       the droop's waves along the blade   l.lobes   the droop at the rim, thin-axis object units
            //   l.twistAmp     zero, so the strap's twist adds nothing (LeafPoint)
            //   l.strap        one
            static const float AlgaFrondStem = 0.08;           // the stem's half width, of the blade's half width, at the base
            static const float AlgaFrondStemTip = 0.6;         // and at the tip, of that
            static const float AlgaFrondStemEnd = 0.03;        // the stem closes over this much of the length at each end
            static const float AlgaFrondEnvStart = 0.02;       // the blades' length: nothing here, full from Full
            static const float AlgaFrondEnvFull = 0.18;
            static const float AlgaFrondEnvTipStart = 0.7;     // and shortening from here to the tip by Tip of it
            static const float AlgaFrondEnvTip = 0.3;
            static const float AlgaFrondTipClose = 0.07;       // the blades close in a round end over this much of the length
            static const float AlgaFrondWeb = 0.15;            // the web under the blades, of the envelope
            static const float AlgaFrondSide = 0.41;           // the other side's offset in blades (LeafScallopSide)
            static const float AlgaFrondBladesBase = 2.5;      // blades a side in the first row: this plus PerAspect times length over width
            static const float AlgaFrondBladesPerAspect = 1.2;
            static const float AlgaFrondBladesFewest = 4.0;
            static const float AlgaFrondBladesMost = 7.0;
            static const float AlgaFrondSecondBase = 1.0;      // the second row: this plus Share times the first's count, rounded
            static const float AlgaFrondSecondShare = 1.5;
            static const float AlgaFrondSecondSize = 0.85;     // and its blades' size, of the first row's
            static const float AlgaFrondThirdBase = 2.0;       // the third row, of small blades: this plus Share times the first's count
            static const float AlgaFrondThirdShare = 2.2;
            static const float AlgaFrondThirdSize = 0.65;      // and its blades' size, of the first row's
            static const float AlgaFrondJitter = 0.4;          // how far a row's cells stretch and squeeze, in cells; under one
            static const float AlgaFrondJitterRate = 0.41;     // and the pace of that, per cell
            static const float AlgaFrondApex = 0.6;            // of a blade's length, where its width peaks (the tip leans forward)
            static const float AlgaFrondRise = 0.5;            // the power of the rise to the apex (under one: full)
            static const float AlgaFrondFall = 0.6;            // and of the fall after it
            static const float AlgaFrondSizeLeast = 0.7;       // the smallest blade, of the largest
            static const float AlgaFrondTeethPerBlade = 2.5;   // fine teeth on the blades, by blade
            static const float AlgaFrondTooth = 0.12;          // their depth, of the blade's width, inward only
            static const float AlgaFrondTone = 0.12;           // a blade's lightness differs from its neighbour's by up to this either way
            static const float AlgaFrondBeadLow = 0.15;        // the bladders sit between these two stations
            static const float AlgaFrondBeadHigh = 0.85;
            static const float AlgaFrondBeadsBase = 1.5;       // bladders: this plus PerAspect times length over width
            static const float AlgaFrondBeadsPerAspect = 1.2;
            static const float AlgaFrondBeadsFewest = 3.0;
            static const float AlgaFrondBeadsMost = 5.0;
            static const float AlgaFrondBladderRadius = 0.17;  // a bladder's radius in the plane of the blade, of the half width
            static const float AlgaFrondBladderShare = 0.6;    // a bladder's thickness, of hT, at most
            static const float AlgaFrondBeadSpan = 0.45;       // the radius along the blade, of the spacing between bladders, at most
            static const float AlgaFrondBeadSizeLeast = 0.75;  // the smallest bladder, of the largest
            static const float AlgaFrondBeadClear = 0.5;       // the blades are gone where the bladder's circle passes this
            static const float AlgaFrondArch = 0.3;            // the frond's one curl in the budget, an arch; under AlgaCurlBudget
            static const float AlgaFrondDroop = 0.12;          // the droop at the rim of a full blade, of the half width, in metres
            static const float AlgaFrondDroopPerBlade = 0.6;   // droop waves along the stem, per blade of the first row on a side
            static const float AlgaFrondDroopBias = 0.4;       // the droop wave's mean; it swings by one less this either way
            static const float AlgaFrondLeafLens = 0.5;        // a blade's lens, of the stem's
            static const float AlgaFrondLeafBlend = 0.15;      // the width over the stem's at which a blade's lens is whole
            static const float AlgaFrondRoomLow = 0.1;         // LeafRoom counts a bladder's thickness between these
            static const float AlgaFrondRoomHigh = 0.9;
            static const float AlgaFrondCurlHalf = 0.49;       // the box's half thickness, a little under, for the arch's share
            static const float AlgaThroughFrond = 1.0;         // the light through a frond, every term (AlgaThroughSheet's place)
            static const float AlgaFrondWetGain = 0.05;        // a much weaker, tighter wet sheen than AlgaWetGain at AlgaWetPower
            static const float AlgaFrondWetPower = 24.0;
#if defined(_EVO_ALGA_BLADE)
            // ---------------------------------------------------------------- the broad blade
            //
            // EVOSIM_THEATRE_ALGA_BLADE (the keyword _EVO_ALGA_BLADE, which TheatreSkin and the palette turn on only while that
            // dial and the marine producers' dial are both on): the free-drifting blade above is drawn as one or a few broad
            // blades that fill most of their part, as sea lettuce and a kelp's blade do, and not as a stem with leaflets.
            // Every line of it is inside this keyword and is reached only from the frond's own functions (AlgaFrondWidth,
            // AlgaFrondBound, AlgaFrondCut, AlgaFrondLens, AlgaFrondOf, AlgaFrondBladeTone) and the fragment's frond branches,
            // so without the keyword the frond is the one above, line for line.
            //   The outline is one smooth envelope, AlgaBladeBound: a base that fills quickly (a sine to a power under one, over
            //   AlgaBladeBaseLeast to Most of the length), a long body at the box's full half width times AlgaBladePeakLeast to
            //   one, and a rounded tip (one less the square, to a power under one, over AlgaBladeTipLeast to Most). Inside it
            //   each side is cut inward by AlgaBladeCut: a margin that wanders (three sines at incommensurate paces, 1.7 to 13
            //   waves along the blade, AlgaBladeEdgeDepthLeast to Most of the width at the deepest, a different phase on each
            //   side) and, on a plant with two or three blades (l.lobe, 1 to 3), one or two clefts a side (a smooth dip,
            //   AlgaBladeNotchDepthLeast to Most deep and AlgaBladeNotchWidthLeast to Most of the length wide) where one blade
            //   overlaps the next. The two sides differ in scale by up to one less AlgaBladeSideLeast. No teeth, no round pad,
            //   no regular lobes: every number is a hash of the part's seed and its own key, so no two plants alike.
            //   The width is the envelope times the cut, the cut is in (0, 1], the envelope is at most one: the blade is inside
            //   the box's face wherever the frond was. It fills about 0.8 of the face (notes\fill_blade.py).
            //   The shape's own fields carry its numbers, as the frond's do (they are free in a frond): l.lobe the blade count,
            //   l.baseLen the midrib's strength (zero on a short part), l.ruffleWaves the margin's waves out of the plane,
            //   l.twists the twist's waves along the length (the droop shares them), l.twistAmp the twist at the rim, l.cup
            //   and l.arch and l.wave the curl (AlgaBladeCup, Arch and Frill, under the curl budget with the droop), l.frills
            //   the frill's count, l.kelp zero (no bladders), l.tipLen small (so the roll's roundness is zero).
            static const float AlgaBladePeakLeast = 0.96;          // the envelope's widest, of the box's half width, to one
            static const float AlgaBladeBaseLeast = 0.10;          // the base's length, of the blade's
            static const float AlgaBladeBaseMost = 0.20;
            static const float AlgaBladeTipLeast = 0.14;           // the tip's length
            static const float AlgaBladeTipMost = 0.26;
            static const float AlgaBladeBasePowerLeast = 0.40;     // the base's fullness (under one: full)
            static const float AlgaBladeBasePowerMost = 0.70;
            static const float AlgaBladeTipPowerLeast = 0.34;      // the tip's fullness
            static const float AlgaBladeTipPowerMost = 0.55;
            static const float AlgaBladeEdgeDepthLeast = 0.06;     // the margin's deepest wander, of the width
            static const float AlgaBladeEdgeDepthMost = 0.16;
            static const float AlgaBladeEdgeWaves1Least = 1.7;     // the wander's three paces, in waves along the blade
            static const float AlgaBladeEdgeWaves1Most = 2.7;
            static const float AlgaBladeEdgeWaves2Least = 4.3;
            static const float AlgaBladeEdgeWaves2Most = 6.3;
            static const float AlgaBladeEdgeWaves3Least = 9.0;
            static const float AlgaBladeEdgeWaves3Most = 13.0;
            static const float AlgaBladeEdgeWeight1 = 0.50;        // and their shares; the three add up to one
            static const float AlgaBladeEdgeWeight2 = 0.30;
            static const float AlgaBladeEdgeWeight3 = 0.20;
            static const float AlgaBladeSideLeast = 0.93;          // the smaller side's scale, of the larger's
            static const float AlgaBladeCountSecond = 0.35;        // a plant has a second blade when its hash is over this
            static const float AlgaBladeCountThird = 0.70;         // a third, over this and on a part longer than the aspect below
            static const float AlgaBladeCountAspect = 2.5;
            static const float AlgaBladeNotchLow = 0.30;           // the clefts lie between these two stations
            static const float AlgaBladeNotchHigh = 0.90;
            static const float AlgaBladeNotchWidthLeast = 0.025;   // a cleft's width, of the length
            static const float AlgaBladeNotchWidthMost = 0.050;
            static const float AlgaBladeNotchDepthLeast = 0.30;    // and its depth, of the width
            static const float AlgaBladeNotchDepthMost = 0.50;
            static const float AlgaBladeRuffleWavesLeast = 5.0;    // the margin's waves out of the plane, round the outline
            static const float AlgaBladeRuffleWavesMost = 11.0;
            static const float AlgaBladeRuffle = 1.25;             // their amplitude, times the leaf dial's (the room still binds it)
            static const float AlgaBladeTwist = 0.16;              // the twist at the rim, of the half width (the strap's is 0.12)
            static const float AlgaBladeTwistsLeast = 0.6;         // its waves along the blade; the droop's too
            static const float AlgaBladeTwistsMost = 1.5;
            static const float AlgaBladeCup = 0.15;                // the curl across; with the arch and the frill 0.55 of a budget of 0.6
            static const float AlgaBladeArch = 0.25;
            static const float AlgaBladeFrill = 0.15;
            static const float AlgaBladeFrillsLeast = 3.5;
            static const float AlgaBladeFrillsMost = 6.0;
            static const float AlgaBladeMidrib = 0.5;              // the midrib's share of the leaf's (AlgaKelpMidrib's), on a long part
            static const float AlgaBladeMidribLow = 2.0;           // length over width where it begins
            static const float AlgaBladeMidribHigh = 3.0;          // and is whole
            static const float AlgaBladeRibFadeLow = 0.35;         // the midrib fades out up the blade between these stations
            static const float AlgaBladeRibFadeHigh = 0.95;
            static const float AlgaBladeToneAmp = 0.10;            // the lightness drifts over the blade by this either way
            static const float3 AlgaBladeLeanBrown = float3(1.08, 0.96, 0.92);   // a plant leans browner (kelp) ...
            static const float3 AlgaBladeLeanGreen = float3(0.92, 1.04, 0.98);   // ... or greener (sea lettuce); the middle is near one
            static const float AlgaBladeGlow = 1.0;                // the extra light through at the thin margin, times AlgaSeeGain's
            static const float AlgaBladeGlowStart = 0.55;          // the share of the way out across where it begins
            static const float AlgaBladeCapGain = 1.15;            // and the rim's brightest it may reach, times the cap of the see-through

            // A hash of one number into [0, 1): multiplies and fractions alone, so the vertex and the pixel agree and a plant's
            // seed (_Carve.x, in [0, 1)) moves the input by 61.3 a unit. No sin.
            float AlgaBladeHash(float n)
            {
                n = frac(n * 0.1031);
                n *= n + 33.33;
                n *= n + n;
                return frac(n);
            }

            // The plant's own number for a key: the same at every vertex and pixel of one part.
            float AlgaBladePlant(float key)
            {
                return AlgaBladeHash(_Carve.x * 61.3 + key * 13.7 + 5.1);
            }

            // The envelope at s: the most the width can be, a fraction of the box's half width, at most one.
            float AlgaBladeBound(float s)
            {
                s = saturate(s);

                float peak = lerp(AlgaBladePeakLeast, 1.0, AlgaBladePlant(1.0));
                float baseLength = lerp(AlgaBladeBaseLeast, AlgaBladeBaseMost, AlgaBladePlant(2.0));
                float tipLength = lerp(AlgaBladeTipLeast, AlgaBladeTipMost, AlgaBladePlant(3.0));
                float basePower = lerp(AlgaBladeBasePowerLeast, AlgaBladeBasePowerMost, AlgaBladePlant(4.0));
                float tipPower = lerp(AlgaBladeTipPowerLeast, AlgaBladeTipPowerMost, AlgaBladePlant(5.0));

                float x = saturate(s / baseLength);
                float y = saturate((s - (1.0 - tipLength)) / tipLength);

                return peak * pow(max(1e-5, sin(0.5 * PI * x)), basePower) * pow(max(1e-5, 1.0 - y * y), tipPower);
            }

            // The cut at (s, t) on the side t is on (t under zero is the other side): the margin's wander and the clefts,
            // times the side's scale. In (0, 1], so the width is the envelope or inside it.
            float AlgaBladeCut(float s, float t, LeafShape l)
            {
                s = saturate(s);

                float j = t < 0.0 ? 1.0 : 0.0;

                float depth = lerp(AlgaBladeEdgeDepthLeast, AlgaBladeEdgeDepthMost, AlgaBladePlant(6.0));
                float waves1 = lerp(AlgaBladeEdgeWaves1Least, AlgaBladeEdgeWaves1Most, AlgaBladePlant(7.0));
                float waves2 = lerp(AlgaBladeEdgeWaves2Least, AlgaBladeEdgeWaves2Most, AlgaBladePlant(8.0));
                float waves3 = lerp(AlgaBladeEdgeWaves3Least, AlgaBladeEdgeWaves3Most, AlgaBladePlant(9.0));

                float wander = AlgaBladeEdgeWeight1 * sin(6.2831853 * (waves1 * s + AlgaBladePlant(20.0 + 3.0 * j))) +
                    AlgaBladeEdgeWeight2 * sin(6.2831853 * (waves2 * s + AlgaBladePlant(21.0 + 3.0 * j))) +
                    AlgaBladeEdgeWeight3 * sin(6.2831853 * (waves3 * s + AlgaBladePlant(22.0 + 3.0 * j)));

                float cut = lerp(AlgaBladeSideLeast, 1.0, AlgaBladePlant(10.0 + j)) * (1.0 - depth * (0.5 + 0.5 * wander));

                [unroll] for (int k = 0; k < 2; k++)
                {
                    float key = 40.0 + 4.0 * k + 2.0 * j;
                    float centre = lerp(AlgaBladeNotchLow, AlgaBladeNotchHigh, AlgaBladePlant(key));
                    float width = lerp(AlgaBladeNotchWidthLeast, AlgaBladeNotchWidthMost, AlgaBladePlant(key + 1.0));
                    float dip = lerp(AlgaBladeNotchDepthLeast, AlgaBladeNotchDepthMost, AlgaBladePlant(key + 100.0));
                    float x = (s - centre) / width;
                    float present = l.lobe > (float)k + 1.5 ? 1.0 : 0.0;

                    cut *= 1.0 - present * dip * exp(-x * x);
                }

                return cut;
            }

            // The lens at t: the leaf's own (a half-circle under a faint ridge) with the midrib l.baseLen strong, zero on a short
            // part. Never thicker than the lens of the leaf dial (0.6 + 0.4 AlgaBladeMidrib at the most).
            float AlgaBladeLens(float t, LeafShape l)
            {
                return sqrt(saturate(1.0 - t * t)) * (0.6 + 0.4 * l.baseLen * exp(-t * t / 0.012));
            }

            // The shape's blade numbers (see the block above), in the frond's shape, which this replaces in part.
            LeafShape AlgaBladeOf(LeafShape l, float hL, float hW, float hT)
            {
                float aspect = hL / max(1e-6, hW);
                float cupWay = frac(_Carve.x * 17.137 + 0.371) < 0.5 ? -1.0 : 1.0;

                l.lobe = 1.0 + (AlgaBladePlant(30.0) > AlgaBladeCountSecond ? 1.0 : 0.0) +
                    ((AlgaBladePlant(31.0) > AlgaBladeCountThird && aspect > AlgaBladeCountAspect) ? 1.0 : 0.0);

                l.cup = AlgaBladeCup * cupWay;
                l.arch = AlgaBladeArch * (l.arch < 0.0 ? -1.0 : 1.0);
                l.wave = AlgaBladeFrill;
                l.frills = lerp(AlgaBladeFrillsLeast, AlgaBladeFrillsMost, AlgaBladePlant(51.0));
                l.ruffleWaves = lerp(AlgaBladeRuffleWavesLeast, AlgaBladeRuffleWavesMost, AlgaBladePlant(50.0));
                l.twists = lerp(AlgaBladeTwistsLeast, AlgaBladeTwistsMost, AlgaBladePlant(52.0));
                l.twistAmp = AlgaBladeTwist * hW / max(1e-6, 2.0 * hT);

                l.kelp = 0.0;
                l.baseLen = AlgaBladeMidrib * smoothstep(AlgaBladeMidribLow, AlgaBladeMidribHigh, aspect);
                l.tipLen = AlgaTipShortest;

                return l;
            }

            // The blade's lightness in the fragment, about one: a slow drift along the blade and across it, from the plant's own
            // phases. leaf is the fragment's (s, distance across, t, 1 + hW / 2 hL).
            float AlgaBladeTone(float4 leaf)
            {
                float along = sin(6.2831853 * (1.7 * leaf.x + AlgaBladePlant(60.0)));
                float across = sin(6.2831853 * (2.9 * leaf.x + 1.3 * leaf.z + AlgaBladePlant(61.0)));

                return 1.0 + AlgaBladeToneAmp * (0.6 * along + 0.4 * across);
            }

            // The plant's lean along the family, brown to green, as a multiplier on the blade's light.
            float3 AlgaBladeLean()
            {
                return lerp(AlgaBladeLeanBrown, AlgaBladeLeanGreen, AlgaBladePlant(62.0));
            }
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
            // ---------------------------------------------------------------- the blade at level 2
            //
            // EVOSIM_THEATRE_ALGA_BLADE at 2 (the keyword _EVO_ALGA_BLADE2 beside _EVO_ALGA_BLADE, which TheatreSkin enables only
            // at that level): the broad blade gains three things, all of them shading, so no vertex moves and nothing can leave
            // the collider (D242). The vertex stage already ruffles the margin out of the plane as far as the room allows (the
            // ruffle takes LeafRuffleRoomShare of it and the sway keeps the rest), so there is none to spend on a second row;
            // the margin's finer ruffle below is in the normal alone.
            //   (1) A colour for each plant along a ramp from sea lettuce to kelp (AlgaBladeColour). A plant's place on it is
            //   its seed hash (AlgaBladePlant(63)) blended with the visual's depth share (AlgaDepthShare) by AlgaBladeRampDepth,
            //   shallow toward the bright yellow-green end and deep toward the olive-brown. The colour reaches the whole of the
            //   blade's light as a multiplier on leafTone (the rim, the glow and the light through carry the body's hue, so a
            //   body colour alone moves almost nothing), and only on a living blade (_AlgaDeep.w, which a greyed or dead
            //   blade does not have). Within one blade a paler base and tip and a faint darker zone along the midrib
            //   (AlgaBladeWithin).
            //   (2) Light through the blade (the fragment, after the see-through): when the key is behind the blade as the
            //   camera sees it, the blade's own hue at full value and a little more saturated, AlgaBladeBackGain of the key's
            //   direct term at full backlight, falling off as the view-to-key alignment to AlgaBladeBackPower, by the same
            //   thickness the other transmitted terms take. It uses Transmission's own bent light direction
            //   (_TransDistortion), the key, the key gain and the water's cut of the light through (bodyWater.w), and the
            //   see-through's hue.
            //   (3) A finer ruffle along the margin, in the normal (AlgaBladeRuffleNormal): the outer AlgaBladeShadeRim of the
            //   half width, AlgaBladeShadeWavesLeast to Most waves round the outline from the plant's hash, tilting the normal by
            //   at most about AlgaBladeShadeTilt (rise over run) at the rim. Laid in the blade's own coordinates (s, across), so
            //   it stays on the tissue as the body moves; the screen derivatives only give the surface's frame.
            // The ramp's three stops are sRGB (0.55, 0.78, 0.22) for sea lettuce, (0.26, 0.53, 0.19) for a mid green and
            // (0.42, 0.36, 0.16) for kelp, here in linear light.
            static const float3 AlgaBladeRampA = float3(0.2633, 0.5705, 0.0397);   // sea lettuce
            static const float3 AlgaBladeRampB = float3(0.0550, 0.2429, 0.0301);   // mid green
            static const float3 AlgaBladeRampC = float3(0.1473, 0.1065, 0.0220);   // kelp
            static const float AlgaBladeRampMidLuma = 0.1876;      // the mid green's luminance: the lightness it keeps
            static const float AlgaBladeLumaShare = 0.5;           // the exponent on a stop's luminance over the mid green's
            static const float AlgaBladeRampDepth = 0.35;          // the depth share's weight in the place on the ramp
            static const float AlgaBladeRampShare = 0.85;          // how much of the way to the ramp's hue the blade goes
            static const float AlgaBladePaleBaseLength = 0.18;     // the paler base, of the length
            static const float AlgaBladePaleTipLength = 0.15;      // and tip
            static const float3 AlgaBladePaleMul = float3(1.12, 1.14, 1.22);   // paler: lighter and less saturated
            static const float AlgaBladeRibWidth = 0.02;           // the midrib zone's width, as a Gaussian's variance in |across|
            static const float3 AlgaBladeRibMul = float3(0.90, 0.94, 0.86);   // a little darker, a little more saturated
            static const float AlgaBladeBackGain = 0.65;           // the light through, of the key's direct term at full backlight
            static const float AlgaBladeBackPower = 3.0;           // the view-to-key alignment's exponent
            static const float AlgaBladeBackSaturation = 1.3;      // the hue's saturation, one the see-through's
            static const float AlgaBladeShadeRim = 0.18;           // the finer ruffle's reach in from the margin, of the half width
            static const float AlgaBladeShadeWavesLeast = 12.0;    // its waves round the outline
            static const float AlgaBladeShadeWavesMost = 22.0;
            static const float AlgaBladeShadeTilt = 0.55;          // the most the along-margin slope reaches, rise over run
            static const float AlgaBladeShadeFadeLow = 0.25;       // it fades out as its waves near a pixel each: cycles a pixel
            static const float AlgaBladeShadeFadeHigh = 0.5;

            // The ramp's linear colour at a place x in [0, 1]: A to B over the first half, B to C over the second.
            float3 AlgaBladeRamp(float x)
            {
                return x < 0.5 ? lerp(AlgaBladeRampA, AlgaBladeRampB, 2.0 * x) : lerp(AlgaBladeRampB, AlgaBladeRampC, 2.0 * x - 1.0);
            }

            // The blade's own variation as a multiplier on its light, about one: a paler base and tip, and a faint darker zone
            // along the midrib (the same fade up the blade and the same length-over-width as the level-1 midrib's, and none on
            // a short part). leaf is the fragment's (s, distance across, t, 1 + hW / 2 hL).
            float3 AlgaBladeWithin(float4 leaf)
            {
                float s = leaf.x;
                float t = leaf.z;

                float pale = max(1.0 - smoothstep(0.0, AlgaBladePaleBaseLength, s), smoothstep(1.0 - AlgaBladePaleTipLength, 1.0, s));
                float rib = exp(-t * t / AlgaBladeRibWidth) * (1.0 - smoothstep(AlgaBladeRibFadeLow, AlgaBladeRibFadeHigh, s)) *
                    smoothstep(AlgaBladeMidribLow, AlgaBladeMidribHigh, 1.0 / max(1e-6, 2.0 * (leaf.w - 1.0)));

                return lerp(float3(1.0, 1.0, 1.0), AlgaBladePaleMul, pale) * lerp(float3(1.0, 1.0, 1.0), AlgaBladeRibMul, rib);
            }
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
            // ---------------------------------------------------------------- the blade at level 3
            //
            // EVOSIM_THEATRE_ALGA_BLADE at 3 (the keyword _EVO_ALGA_BLADE3 beside _EVO_ALGA_BLADE and _EVO_ALGA_BLADE2, which
            // TheatreSkin enables, all three, only at that level): level 2, and two more things.
            //   (1) Light through the thin blade (the fragment, after level 2's own): when the eye looks toward the key through the
            //   blade, the blade's own hue, shifted toward yellow-green (AlgaBlade3Shift) and brought to full value, at
            //   AlgaBlade3Gain of the key's direct term, falling off as the view-to-key alignment (the key bent by the normal as
            //   Transmission bends it, _TransDistortion) to AlgaBlade3Power, by the thickness the other transmitted terms take
            //   and by how thin the tissue is at that pixel: full at the margin, AlgaBlade3ThinMiddle of that on the midline,
            //   following the lens. Shading only. AlgaBlade3Gain is its strength.
            //   (2) A ruffled margin, in the vertex stage (LeafPoint), in the outer AlgaBlade3RimWidth of the half width and
            //   nowhere else, so the blade's middle is level 2's. Both parts are noise in the blade's own coordinates (s along,
            //   t across, and the perimeter coordinate q the existing ruffle uses), so they ride on the tissue.
            //     Scallops (AlgaBlade3Scallop): the outline is cut inward by up to AlgaBlade3ScallopDepth of the half width, in
            //     AlgaBlade3ScallopsLeast to Most rounded lobes a margin with a sharp notch between, the cells jittered by noise
            //     and each notch of its own depth (down to 1 - AlgaBlade3ScallopVary of the deepest). The factor is in
            //     [1 - AlgaBlade3ScallopDepth, 1] and multiplies the distance across, so nothing is drawn outside the outline
            //     level 2 draws, which is inside the box (D242). The rows do not fold over while
            //     AlgaBlade3ScallopDepth (1 + 1.5 / AlgaBlade3RimWidth) is under one (0.09 times 8.5 is 0.77).
            //     A wave out of the plane (AlgaBlade3Wave): value noise round the outline, AlgaBlade3WaveCellsLeast to Most cells
            //     with a finer octave mixed in (a convex mix, so the wave stays in [-1, 1]), growing from nothing at the rim's
            //     inner edge to its full height at the margin, and none at the stalk. Its height is AlgaBlade3WaveHeight of the
            //     box's half thickness and at most AlgaBlade3RoomShare of the room the lens and the curl leave, which is the
            //     bound the margin's other ruffle keeps to. LeafRoom takes the same AlgaBlade3RoomShare off the sway's room, so
            //     the lens, the curl, the ruffle, the twist, this wave and the sway together stay inside the box wherever
            //     the curl alone does; a part with no room has no wave and keeps its scallops.
            //   Every part of the ruffle is in LeafPoint, which the three passes share, so the depth passes draw what the
            //   forward pass does and a shadow matches its blade.
            static const float AlgaBlade3Gain = 0.35;             // the light through, of the key's direct term at full backlight
            static const float AlgaBlade3Power = 4.0;             // the view-to-key alignment's exponent (level 2's is 3)
            static const float3 AlgaBlade3Shift = float3(1.10, 1.0, 0.55);   // the hue's shift toward yellow-green, before full value
            static const float AlgaBlade3ThinMiddle = 0.6;        // how thin the tissue reads on the midline, of the margin's
            // The colour at level 3 (coordinator, 2026-10-27: the blade read brighter and flatter than the Ulva reference, and far
            // blades stayed lime): the ramp's target is taken AlgaBlade3UlvaShare of the way to the reference's pale yellow-olive
            // at the target's own luminance, then times AlgaBlade3Value; level 2's back light is kept at AlgaBlade3BackKeep, so
            // level 3's own light through, at AlgaBlade3ThruSaturation of its hue's saturation, replaces it rather than adding.
            static const float3 AlgaBlade3Ulva = float3(0.40, 0.45, 0.10);  // the reference's blade in the water, linear
            static const float AlgaBlade3UlvaShare = 0.6;         // how far the ramp's target goes toward it
            static const float AlgaBlade3Value = 0.85;            // the target's lightness at level 3, of level 2's
            static const float AlgaBlade3BackKeep = 0.0;          // level 2's back light at level 3, of its own
            static const float AlgaBlade3ThruSaturation = 0.7;    // level 3's light-through hue's saturation, of its own
            static const float AlgaBlade3RimWidth = 0.20;         // the ruffle's reach in from the margin, of the half width
            static const float AlgaBlade3WaveHeight = 0.5;        // the wave out of the plane, of the box's half thickness
            static const float AlgaBlade3RoomShare = 0.15;        // and at most this much of the room; the sway gives it up
            static const float AlgaBlade3WaveCellsLeast = 10.0;   // the wave's noise cells round the outline
            static const float AlgaBlade3WaveCellsMost = 18.0;
            static const float AlgaBlade3WaveFine = 0.3;          // the finer octave's share of the wave (twice the cells)
            static const float AlgaBlade3ScallopDepth = 0.09;     // the deepest notch, of the half width, at the margin
            static const float AlgaBlade3ScallopsLeast = 9.0;     // the scallops along one margin
            static const float AlgaBlade3ScallopsMost = 14.0;
            static const float AlgaBlade3ScallopVary = 0.5;       // a notch's depth varies down to this share short of the deepest
            static const float AlgaBlade3ScallopJitter = 0.4;     // the cells' jitter, in cells (under two thirds: no cell turns back)

            // A smooth value noise in [0, 1) at x, from the plant's hash at the integer cells either side (keys key + i).
            float AlgaBlade3Noise(float x, float key)
            {
                float i = floor(x);
                float f = x - i;
                f = f * f * (3.0 - 2.0 * f);

                return lerp(AlgaBladePlant(key + i), AlgaBladePlant(key + i + 1.0), f);
            }

            // The scallops' factor on the distance across at (s, t), in [1 - AlgaBlade3ScallopDepth, 1]: one on the blade's middle
            // and at the stalk, falling in the outer AlgaBlade3RimWidth to the notch's depth. The cells are the margin's own
            // (a side of its own), their edges jittered by noise, each notch of its own depth; a lobe is flat at its centre and
            // a notch sharp (the cube of the distance from the centre).
            float AlgaBlade3Scallop(float s, float t)
            {
                float j = t < 0.0 ? 1.0 : 0.0;
                float waves = lerp(AlgaBlade3ScallopsLeast, AlgaBlade3ScallopsMost, AlgaBladePlant(80.0 + j));
                float phase = AlgaBladePlant(82.0 + j);

                float u = waves * s + phase + AlgaBlade3ScallopJitter * (2.0 * AlgaBlade3Noise(0.5 * waves * s, 500.0 + 100.0 * j) - 1.0);
                float k = floor(u);
                float x = 2.0 * (u - k) - 1.0;
                float notch = x * x * abs(x);
                float vary = lerp(1.0 - AlgaBlade3ScallopVary, 1.0, AlgaBladePlant(700.0 + 100.0 * j + k));

                float edge = smoothstep(1.0 - AlgaBlade3RimWidth, 1.0, abs(t));
                float stalk = smoothstep(0.08, 0.25, s);

                return 1.0 - AlgaBlade3ScallopDepth * notch * vary * edge * stalk;
            }

            // The wave out of the plane at (s, t), in the thin axis' object units (added to the point's thin coordinate): value
            // noise round the outline (q: up one margin and down the other, the existing ruffle's), a coarse octave and a finer
            // one mixed convexly, so the wave is in [-1, 1]; its rise from the rim's inner edge is a smoothstep and it is faded
            // in at the stalk as the other ruffle is. room is the room LeafPoint has at the point; the height is at most
            // AlgaBlade3WaveHeight of meshHalf (the box's half thickness) and AlgaBlade3RoomShare of that room.
            float AlgaBlade3Wave(float s, float t, float meshHalf, float room)
            {
                float u = saturate((abs(t) - (1.0 - AlgaBlade3RimWidth)) / AlgaBlade3RimWidth);
                float rim = u * u * (3.0 - 2.0 * u);
                float stalk = smoothstep(0.05, 0.3, s);

                float cells = lerp(AlgaBlade3WaveCellsLeast, AlgaBlade3WaveCellsMost, AlgaBladePlant(84.0));
                float q = t >= 0.0 ? 0.5 * s : 1.0 - 0.5 * s;
                float coarse = 2.0 * AlgaBlade3Noise(cells * q, 300.0) - 1.0;
                float fine = 2.0 * AlgaBlade3Noise(2.0 * cells * q, 400.0) - 1.0;

                return min(AlgaBlade3WaveHeight * meshHalf, AlgaBlade3RoomShare * room) * rim * stalk * lerp(coarse, fine, AlgaBlade3WaveFine);
            }
#endif

            // The stem's half width at s of the blade's: round at the base, thinner to the tip, closed at both ends.
            float AlgaFrondStemWidth(float s)
            {
                s = saturate(s);

                return AlgaFrondStem * lerp(1.0, AlgaFrondStemTip, s) *
                    sqrt(saturate(s / AlgaFrondStemEnd)) * sqrt(saturate((1.0 - s) / AlgaFrondStemEnd));
            }

            // How long the blades are at s, in [0, 1]: none at the base, full from AlgaFrondEnvFull, shorter to the tip,
            // and closed in a round end over the last AlgaFrondTipClose.
            float AlgaFrondEnvelope(float s)
            {
                s = saturate(s);

                return smoothstep(AlgaFrondEnvStart, AlgaFrondEnvFull, s) *
                    (1.0 - AlgaFrondEnvTip * smoothstep(AlgaFrondEnvTipStart, 1.0, s)) *
                    sqrt(saturate((1.0 - s) / AlgaFrondTipClose));
            }

            // The bladder at s: x its width and thickness share (the bead's size times its circle, in [0, 1]), y its
            // circle alone (in [0, 1]). Evenly spaced between AlgaFrondBeadLow and High, the size swinging smoothly
            // from AlgaFrondBeadSizeLeast to one by the seed's phase; none off the stem's middle stretch.
            float2 AlgaFrondBead(float s, LeafShape l)
            {
                float spacing = (AlgaFrondBeadHigh - AlgaFrondBeadLow) / max(1.0, l.frills);
                float u = (s - AlgaFrondBeadLow) / spacing;
                float k = floor(u);
                float centre = AlgaFrondBeadLow + (k + 0.5) * spacing;
                float size = lerp(AlgaFrondBeadSizeLeast, 1.0, 0.5 + 0.5 * sin(6.2831853 * (0.37 * k + l.phase)));
                float x = (s - centre) / max(1e-4, l.tipLen * size);
                float circle = sqrt(saturate(1.0 - x * x));
                float inside = (u >= 0.0 && u < l.frills) ? 1.0 : 0.0;

                return float2(size * circle * inside, circle * inside);
            }

            // Blades a side in the first row, from the blade's length over width, whole: 4 to 7. And in the second and the
            // third rows, from the first's.
            float AlgaFrondCountA(float aspect)
            {
                return round(clamp(AlgaFrondBladesBase + AlgaFrondBladesPerAspect * aspect,
                                   AlgaFrondBladesFewest, AlgaFrondBladesMost));
            }

            float AlgaFrondCountB(float countA)
            {
                return round(AlgaFrondSecondBase + AlgaFrondSecondShare * countA);
            }

            float AlgaFrondCountC(float countA)
            {
                return round(AlgaFrondThirdBase + AlgaFrondThirdShare * countA);
            }

            // One row of blades at s on the side t is on: the width share of the blade there, in [0, 1], and in tone a number
            // in [0, 1] for the blade's lightness. The cell coordinate u runs count times along the stem, stretched and
            // squeezed by a slow sine (its slope 1 + AlgaFrondJitter cos, over zero, so u never turns back); a blade is a
            // cell, floor(u) its number. Its size and its tone are sums of two sines of its number, from the seed's
            // phase: smooth, deterministic and uneven. Its shape is a rise to the apex and a fall to a point, both fuller
            // than a line (the powers are under one), with teeth by blade that follow the same u. All of it is a factor in
            // [0, 1] times size, so the row is at most size.
            float AlgaFrondRow(float s, float t, float count, float phase, float size, out float tone)
            {
                float x = count * s + phase + (t < 0.0 ? AlgaFrondSide : 0.0);
                float u = x + AlgaFrondJitter *
                    sin(6.2831853 * (AlgaFrondJitterRate * x + 3.0 * phase)) / (6.2831853 * AlgaFrondJitterRate);
                float k = floor(u);
                float f = u - k;

                float big = 0.5 + 0.25 * sin(6.2831853 * (0.37 * k + 2.0 * phase)) +
                    0.25 * sin(6.2831853 * (0.151 * k + 5.0 * phase + 0.3));
                tone = 0.5 + 0.25 * sin(6.2831853 * (0.71 * k + 7.0 * phase + 0.1)) +
                    0.25 * sin(6.2831853 * (0.233 * k + 3.0 * phase + 0.6));

                float rise = pow(saturate(f / AlgaFrondApex), AlgaFrondRise);
                float fall = pow(saturate((1.0 - f) / (1.0 - AlgaFrondApex)), AlgaFrondFall);
                float teeth = 1.0 - AlgaFrondTooth * (0.5 + 0.5 * cos(6.2831853 * AlgaFrondTeethPerBlade * u));

                return size * lerp(AlgaFrondSizeLeast, 1.0, saturate(big)) * min(rise, fall) * teeth;
            }

            // The three rows at s on the side t is on: the widest blade's width share, and its tone. counts is the rows'
            // blade counts a side. The seed's phases are the rows' own.
            float AlgaFrondBlades(float s, float t, float3 counts, out float tone)
            {
                float phaseA = frac(_Carve.x * 5.717 + 0.233);
                float phaseB = frac(_Carve.x * 7.331 + 0.619);
                float phaseC = frac(_Carve.x * 3.173 + 0.411);

                float toneA;
                float toneB;
                float toneC;
                float a = AlgaFrondRow(s, t, counts.x, phaseA, 1.0, toneA);
                float b = AlgaFrondRow(s, t, counts.y, phaseB, AlgaFrondSecondSize, toneB);
                float c = AlgaFrondRow(s, t, counts.z, phaseC, AlgaFrondThirdSize, toneC);

                float ab = max(a, b);

                tone = ab >= c ? (a >= b ? toneA : toneB) : toneC;
                return max(ab, c);
            }

            // The frond's width at s on the side t is on (t under zero is the other side), as a fraction of the box's
            // half width: the stem's plus the blades' (or the web's, where that is wider), at most one and only inward of
            // it, and a bladder's where that is wider.
            float AlgaFrondWidth(float s, float t, LeafShape l)
            {
#if defined(_EVO_ALGA_BLADE)
                return AlgaBladeBound(s) * AlgaBladeCut(s, t, l);
#endif
                float2 bead = AlgaFrondBead(s, l);

                float tone;
                float blades = AlgaFrondBlades(s, t, float3(l.ruffleWaves, AlgaFrondCountB(l.ruffleWaves), AlgaFrondCountC(l.ruffleWaves)), tone);
                float bare = 1.0 - smoothstep(0.0, AlgaFrondBeadClear, bead.y);

                float stem = AlgaFrondStemWidth(s);
                float leaves = stem + (1.0 - stem) * AlgaFrondEnvelope(s) * max(AlgaFrondWeb, blades) * bare;

                return max(leaves, l.baseLen * bead.x);
            }

            // The most the width can be at s on either side, which is the outline of the frond as the rest of the shader
            // knows it (AlgaOutline): the stem and the whole of the blades' envelope, or a bladder if wider. At most one.
            float AlgaFrondBound(float s, LeafShape l)
            {
#if defined(_EVO_ALGA_BLADE)
                return AlgaBladeBound(s);
#endif
                float stem = AlgaFrondStemWidth(s);

                return max(stem + (1.0 - stem) * AlgaFrondEnvelope(s), l.baseLen * AlgaFrondBead(s, l).x);
            }

            // The factor on the distance across (AlgaFray's place in LeafPoint): the frond's width over its bound, so the
            // outline LeafWidth returns times it is the width above. In [0, 1], so only inward.
            float AlgaFrondCut(float s, float t, LeafShape l)
            {
#if defined(_EVO_ALGA_BLADE)
                return AlgaBladeCut(s, t, l);
#endif
                return min(1.0, AlgaFrondWidth(s, t, l) / max(1e-4, AlgaFrondBound(s, l)));
            }

            // The lens factor at (s, t), in LeafPoint's place for it: round on the stem, a thin blade's where the width is
            // over the stem's, and a bladder's where that is thicker. taper is what LeafPoint multiplies the lens by to
            // get the thickness (meshHalf, the ends, the taper along the blade and the flesh), so the thickness is the
            // bladder's own, meshHalf times its share times the circle, where the bladder wins.
            float AlgaFrondLens(float s, float t, LeafShape l, float meshHalf, float taper)
            {
#if defined(_EVO_ALGA_BLADE)
                return AlgaBladeLens(t, l);
#endif
                float circle = sqrt(saturate(1.0 - t * t));
                float leafness = saturate((AlgaFrondWidth(s, t, l) - AlgaFrondStemWidth(s)) / AlgaFrondLeafBlend);
                float lens = circle * lerp(1.0, AlgaFrondLeafLens, leafness);
                float beadLens = meshHalf * l.kelp * AlgaFrondBead(s, l).x * circle / max(1e-6, taper);

                return max(lens, beadLens);
            }

            // The shape's frond numbers (see the block above): the curl an arch and a droop, the blades and bladders
            // counted from the blade's length over width, the bladder's thickness sized by hT less the arch's lift, its
            // radius in the plane by the half width and by its room along the stem, and the droop at the rim in the thin
            // axis' object units. hL, hW and hT are the blade's half length, width and thickness in metres.
            LeafShape AlgaFrondOf(LeafShape l, float hL, float hW, float hT)
            {
                float aspect = hL / max(1e-6, hW);

                l.alga = 2.0;
                l.strap = 1.0;

                float archWay = frac(_Carve.x * 29.713 + 0.113) < 0.5 ? -1.0 : 1.0;
                l.cup = 0.0;
                l.arch = AlgaFrondArch * archWay;
                l.wave = 0.0;

                l.ruffleWaves = AlgaFrondCountA(aspect);
                l.frills = round(clamp(AlgaFrondBeadsBase + AlgaFrondBeadsPerAspect * aspect,
                                       AlgaFrondBeadsFewest, AlgaFrondBeadsMost));

                // The bladder's thickness: AlgaFrondBladderShare of hT, and at most what is left of the box's half thickness
                // after the arch's lift at its highest (the curl in object units, _CurlFraction hW / hT, times the arch's
                // weight). Its radius in the plane: AlgaFrondBladderRadius of hW, and at most AlgaFrondBeadSpan of the gap
                // between two bladders along the blade.
                float archLift = abs(_CurlFraction * hW / max(1e-6, hT) * l.arch) / AlgaFrondCurlHalf;
                float rho = clamp(1.0 - archLift, 0.0, AlgaFrondBladderShare);
                float spacing = (AlgaFrondBeadHigh - AlgaFrondBeadLow) / max(1.0, l.frills);
                float radius = AlgaFrondBladderRadius * hW / max(1e-6, 2.0 * hL);
                float widthShare = AlgaFrondBladderRadius * min(1.0, AlgaFrondBeadSpan * spacing / max(1e-6, radius));

                l.kelp = rho;
                l.baseLen = widthShare;
                l.tipLen = widthShare * hW / max(1e-6, 2.0 * hL);

                // The droop, at the rim of a full blade, in metres AlgaFrondDroop of the half width, in the thin axis'
                // object units (one is the full thickness, 2 hT). The strap's twist is given nothing to do.
                l.twists = AlgaFrondDroopPerBlade * l.ruffleWaves;
                l.lobes = AlgaFrondDroop * hW / max(1e-6, 2.0 * hT);
                l.twistAmp = 0.0;
#if defined(_EVO_ALGA_BLADE)
                l = AlgaBladeOf(l, hL, hW, hT);
#endif

                return l;
            }

            // A blade's lightness in the fragment (AlgaFrondTone): the tone of the blade that makes the outline at this
            // pixel's station and side, from the leaf's (s, distance across, t, 1 + hW / 2 hL), one around one. The counts
            // are the vertex stage's, from the aspect the vertex stage carries in leaf.w.
            float AlgaFrondBladeTone(float4 leaf)
            {
#if defined(_EVO_ALGA_BLADE)
                return AlgaBladeTone(leaf);
#endif
                float countA = AlgaFrondCountA(1.0 / max(1e-6, 2.0 * (leaf.w - 1.0)));
                float tone;
                AlgaFrondBlades(leaf.x, leaf.z, float3(countA, AlgaFrondCountB(countA), AlgaFrondCountC(countA)), tone);

                return 1.0 + AlgaFrondTone * 2.0 * (tone - 0.5);
            }

            // The frond flag, for the fragment: the palette's _AlgaShallow.w, one on a free-drifting blade and zero on every
            // other (the depth tint reads only _AlgaShallow.rgb). Only asked inside a branch on the dial's blade.
            bool AlgaIsFrond()
            {
                return _AlgaShallow.w > 0.5;
            }

            // A blade's shape under the dial: LeafOf's, with every character term replaced by the proportions'. hL,
            // hW and hT are the blade's half length, width and thickness in metres.
            LeafShape AlgaShapeOf(LeafShape l, float hL, float hW, float hT)
            {
                float aspect = hL / max(1e-6, hW);
                float4 f = AlgaFormOf(aspect);

                l.alga = 1.0;
                l.form = f.w;
                l.strap = f.y;
                l.kelp = f.z;
                l.tipLen = clamp(AlgaStrapWidth * hW / max(1e-6, 2.0 * hL), AlgaTipShortest, AlgaTipLongest);
                l.baseLen = clamp(AlgaBaseHalfWidths * hW / max(1e-6, 2.0 * hL), AlgaBaseShortest, AlgaBaseLongest);

                // No hash in the outline: where it is widest, its base and its tip are the proportions', and
                // there are no lobes.
                l.widest = 1.0;
                l.lobe = 0.0;

                // The curl. The seed says only which face a cup and an arch bow toward, half a turn of a phase.
                float cupWay = frac(_Carve.x * 17.137 + 0.371) < 0.5 ? -1.0 : 1.0;
                float archWay = frac(_Carve.x * 29.713 + 0.113) < 0.5 ? -1.0 : 1.0;
                float cup = lerp(AlgaCupSheet, AlgaCupStrap, l.form) * cupWay;
                float arch = lerp(AlgaArchSheet, AlgaArchStrap, l.form) * archWay;
                float wave = lerp(AlgaFrillSheet, AlgaFrillStrap, l.form);
                float sum = abs(cup) + abs(arch) + wave;
                float k = AlgaCurlBudget / max(1e-4, sum);

                l.cup = cup * k;
                l.arch = arch * k;
                l.wave = wave * k;
                l.frills = clamp(AlgaFrillsPerAspect * aspect, AlgaFrillsFewest, AlgaFrillsMost);
                l.phase = frac(_Carve.x * 101.51 + 0.447);

                l.ruffleWaves = clamp(AlgaRuffleWavesBase + AlgaRuffleWavesPerAspect * aspect,
                                      AlgaRuffleWavesFewest, AlgaRuffleWavesMost);
                l.twists = clamp(AlgaTwistsPerAspect * aspect, AlgaTwistsFewest, AlgaTwistsMost);
                l.twistAmp = AlgaTwistAmplitude * hW / max(1e-6, 2.0 * hT);

                // A free-drifting blade (the palette's flag, _AlgaShallow.w) is a frond: its outline, curl, lens and
                // counts replace the above (AlgaFrondOf). Read only here, under the dial.
                [branch] if (_AlgaShallow.w > 0.5) l = AlgaFrondOf(l, hL, hW, hT);

                return l;
            }

            // The blade's shape: LeafOf's, or under the dial the proportions'. ShapeLamina and LeafSway both
            // ask it, so the sway's room is the drawn blade's.
            LeafShape BladeShapeOf(float hL, float hW, float hT)
            {
                LeafShape l = LeafOf(_Carve.x, _Individual);
                [branch] if (_Alga > 0.001) l = AlgaShapeOf(l, hL, hW, hT);
                return l;
            }

            // The outline under the dial, before the fray: a fraction of the box's half width, at most one.
            float AlgaOutline(float s, LeafShape l)
            {
                // A frond's outline is its stem and the whole of its leaflets' envelope, or a bladder (AlgaFrondBound).
                [branch] if (l.alga > 1.5) return AlgaFrondBound(s, l);

                s = saturate(s);

                float sheet = pow(max(1e-4, sin(PI * s)), AlgaSheetRound);

                float base = pow(max(1e-4, sin(0.5 * PI * saturate(s / l.baseLen))), AlgaBasePower);
                float x = saturate((s - (1.0 - l.tipLen)) / l.tipLen);
                float tip = sqrt(max(1e-4, 1.0 - x * x));
                float strap = AlgaStrapWidth * base * tip;

                return lerp(sheet, strap, l.form);
            }

            // The fray at a strap's tip, a factor in [1 - AlgaFrayDepthMost, 1] on the width: only inward. The notch's
            // depth swings between AlgaFrayDepthLeast and AlgaFrayDepthMost of AlgaFrayDepth on a second cosine with a
            // period of AlgaFraySwing notches, and its period wanders on a slower one of AlgaFrayBeat notches, both with
            // a phase from the seed, so the edge is ragged and not a comb. The factor is 1 off
            // the zone, and the caller multiplies it into the distance across, which is nothing at the midline.
            float AlgaFray(float s, float t, LeafShape l)
            {
                // A frond's cut is its leaflets', teeth and bladders' (AlgaFrondCut): its width over its outline's, inward.
                [branch] if (l.alga > 1.5) return AlgaFrondCut(s, t, l);

                float reach = AlgaFrayReach * l.tipLen;
                float zone = smoothstep(1.0 - reach, 1.0 - 0.25 * reach, saturate(s));
                float phase = frac(_Carve.x * 5.717 + 0.233) + (t < 0.0 ? 0.5 : 0.0);
                float seed = frac(_Carve.x * 13.913 + 0.571);
                float warp = 0.15 * sin(6.2831853 * (s / (AlgaFrayPeriod * AlgaFrayBeat) + seed));
                float notch = 0.5 + 0.5 * cos(6.2831853 * (s / AlgaFrayPeriod + phase + warp));
                float swing = 0.5 + 0.5 * cos(6.2831853 * (s / (AlgaFrayPeriod * AlgaFraySwing) + seed));
                float depth = AlgaFrayDepth * lerp(AlgaFrayDepthLeast, AlgaFrayDepthMost, swing);
                return 1.0 - depth * l.strap * zone * notch;
            }

            // The thickest a blade's half thickness may be under the dial, as a share of its half width, and the
            // cap on the flesh (LeafPoint's last factor on the thickness) that keeps it. The lens at its thickest
            // is 0.6 plus the kelp midrib's 0.2 at most, and an object unit is the full thickness (2 hT), so the
            // thickest point is 2 hT meshHalf lensMost flesh in metres. A cap and only that: it never raises flesh.
            float AlgaFleshCap(LeafShape l, float hW, float hT, float meshHalf)
            {
                float lensMost = 0.6 + 0.2 * l.kelp;

                // A frond's lens is round on its stem, at one (AlgaFrondLens); its l.kelp is the bladder's, not a midrib's.
                [branch] if (l.alga > 1.5) lensMost = 1.0;

                return AlgaThinHalf * hW / max(1e-6, 2.0 * hT * meshHalf * lensMost);
            }

            // The outline's half width at s (0 the base, 1 the tip), as a fraction of the box's
            // half width: zero at both ends, one at most. The base tapers as a wedge and the tip
            // is round, from two exponents on one arch that meet at its crown, where the arch is
            // flat, so the join has no kink. The lobes cut inward only, so the outline stays
            // inside the box. Under the marine producers' dial, AlgaOutline (LeafPoint cuts its fray).
            float LeafWidth(float s, LeafShape l)
            {
                [branch] if (l.alga > 0.5) return AlgaOutline(s, l);

                s = saturate(s);
                float u = pow(s, l.widest);
                float arch = max(1e-4, sin(PI * u));
                float g = pow(arch, u < 0.5 ? l.baseFull : l.tipFull);
                float lobes = 0.5 + 0.5 * sin(6.2831853 * (l.lobes * s + l.phase));
                return g * (1.0 - l.lobe * lobes * sin(PI * s));
            }

            // The scalloped edge of a blade (_Frill, the bold tissue looks). The blade's width at a point
            // across it is multiplied by a number in [1 - _Frill * LeafScallopDepth, 1], falling to its
            // smallest in a notch between two scallops and staying at one in the middle of the blade, so
            // every point of the outline moves toward the midrib and none away from it, and the scalloped
            // blade is inside the one without them and so inside the box. Lobe and notch are both smooth, a
            // cosine, and shallow, so the margin reads as a kelp's or a sea lettuce's ruffle and not as a
            // holly's teeth. The two margins are out of step, and the stalk's end is left clean. The 96
            // stations along a blade give a scallop about fourteen of them.
            static const float LeafScallopsBase = 5.0;
            static const float LeafScallopsSpread = 3.0;
            static const float LeafScallopDepth = 0.14;
            static const float LeafScallopSide = 0.41;
            static const float LeafScallopEdgeLow = 0.5;
            static const float LeafScallopEdgeHigh = 0.95;

            float LeafScallop(float s, float t, LeafShape l)
            {
                float count = LeafScallopsBase + LeafScallopsSpread * frac(l.phase * 7.31);
                float x = count * s + l.phase * 3.0 + (t < 0.0 ? LeafScallopSide : 0.0);
                float notch = 0.5 + 0.5 * cos(2.0 * PI * x);
                float edge = smoothstep(LeafScallopEdgeLow, LeafScallopEdgeHigh, abs(t));
                float stalk = smoothstep(0.08, 0.25, s);

                return 1.0 - saturate(_Frill) * LeafScallopDepth * notch * edge * stalk;
            }

            // 3. A margin that ruffles out of plane (_LeafLook, EVOSIM_THEATRE_LEAF; LeafPoint), as sea lettuce
            // and kelp do. The whole sheet moves along its thin axis, both faces together, so the lens keeps its
            // thickness: by a wave round the outline, growing from nothing at LeafRuffleStart of the way out
            // across the blade (|t|, rim to rim) by a smoothstep to its full amplitude at the rim, and faded out
            // at the stalk as the existing frill is. Round the outline means by the perimeter coordinate q, 0 at
            // the base, going up one margin to the tip at a half and back down the other, so the waves are one
            // row round the whole blade, a whole number of them (LeafRuffleWavesLow to Low + Span - 1, from
            // _Carve.x) so the join at the base shows nothing; the second harmonic, at twice the waves plus one
            // (also whole), has LeafRuffleHarmonic of the amplitude so the wave is not a pure sine. The wave is
            // a number in [-1, 1].
            //
            // The amplitude is LeafRuffleAmplitude of the blade's half width (metres, turned to thin-axis
            // object units in ShapeLamina), and it is bounded by the room the blade has left. The room at a
            // point is the box's half thickness less the lens's own half thickness there, less LeafRuffleCurlShare
            // of the curl's lift there (the owner's 2026-09-24 ruling lets the curl alone take a blade out of its
            // box; the ruffle is never allowed to add to that). The ruffle is at most LeafRuffleRoomShare of the
            // room, so |lens + curl + ruffle| is under the box's half thickness wherever the curl is, and the
            // margin the ruffle adds outside the collider is 0. A blade whose curl already fills its box (a wide,
            // thin one) has no room and does not ruffle. The sway dial keeps the rest of the room
            // (LeafRoom takes LeafRuffleRoomShare off its room under this dial), so the two together are inside
            // as well. With CurlShare at 0 the bound is the lens's alone, and the curl and the ruffle together
            // may then pass the box where the curl alone does.
            static const float LeafRuffleAmplitude = 0.08;     // share of the blade's half width; in (0, 1)
            static const float LeafRuffleStart = 0.55;         // share of the way out across where it begins; in [0, 1)
            static const float LeafRuffleWavesLow = 6.0;       // fewest waves round the outline; a whole number
            static const float LeafRuffleWavesSpan = 7.0;      // how many different counts; a whole number, 6 to 12 here
            static const float LeafRuffleHarmonic = 0.3;       // share of the second harmonic; in [0, 1)
            static const float LeafRuffleRoomShare = 0.5;      // share of the free room the ruffle may spend; in (0, 1)
            static const float LeafRuffleCurlShare = 1.0;      // share of the curl's lift taken off the room; 0 to 1

            // Level 2 of the dial (_LeafLook at 2): the margin by growth. A sheet whose margin grows more than its
            // middle buckles in finer waves toward its edge (Ulva), so a second, finer wave grows in over the
            // first from LeafRuffleFineStart of the way out across the blade, and takes LeafRuffleFineShare of the
            // wave at the rim. The fine row has LeafRuffleFineRatio times the coarse count, rounded to a whole
            // number so the join at the base shows nothing. The honesty bound is level 1's, unchanged: the wave
            // is a convex mix, (1 - m) coarse + m fine, with m = FineShare * smoothstep(FineStart, 1, |t|) in
            // [0, FineShare] and FineShare at most 1, and both terms are numbers in [-1, 1], so the mix is in
            // [-1, 1] too and the amplitude in front of it (the least of LeafRuffleAmplitude and the share of
            // the room) is the one level 1 has. The margin outside the collider stays 0. At 31 waves (12 times
            // 2.6) the mesh's 96 stations give six samples to a wave.
            static const float LeafRuffleFineRatio = 2.6;      // fine waves per coarse wave; at least 1
            static const float LeafRuffleFineStart = 0.8;      // share of the way out where it begins; in [LeafRuffleStart, 1)
            static const float LeafRuffleFineShare = 0.65;     // its share of the wave at the rim; in (0, 1]

            // The ruffle's wave under the marine producers' dial (_Alga): the leaf dial's, coarse row, second
            // harmonic and fine row alike, at a whole number of waves round the outline; the fine row mixed in by
            // fineWeight times level 2's own weight, so a convex mix as level 2's is.
            float LeafRuffleWaveAt(float q, float t, float waves, float phase, float fineWeight)
            {
                float k1 = 6.2831853 * (waves * q + phase);
                float k2 = 6.2831853 * ((2.0 * waves + 1.0) * q + 2.3 * phase);
                float wave = (1.0 - LeafRuffleHarmonic) * sin(k1) + LeafRuffleHarmonic * sin(k2);

                float fineWaves = max(1.0, round(waves * LeafRuffleFineRatio));
                float fine = sin(6.2831853 * (fineWaves * q + frac(phase * 3.71 + 0.317)));
                float fineMix = fineWeight * LeafRuffleFineShare * smoothstep(LeafRuffleFineStart, 1.0, abs(t));

                return lerp(wave, fine, fineMix);
            }

            // The count from the blade's proportions (l.ruffleWaves) is not whole, so the waves at the two whole
            // counts either side are blended by its fraction: a convex mix of two numbers in [-1, 1], so the wave
            // stays in [-1, 1] and the bound is the leaf dial's; it moves smoothly with the proportions, and the
            // join at the base shows nothing, since each count is whole. The phase is the seed's alone. The fine row
            // (the margin by growth) is whole on a blade under level 2 of the leaf dial and otherwise weighted by
            // how far from a strap the blade is, so a sheet carries it with the leaf dial off or at level 1.
            float AlgaRuffleWave(float q, float t, LeafShape l)
            {
                float phase = frac(_Carve.x * 6.131 + 0.683);
                float fineWeight = _LeafLook > 1.5 ? 1.0 : 1.0 - l.strap;
                float low = floor(l.ruffleWaves);

                return lerp(LeafRuffleWaveAt(q, t, low, phase, fineWeight),
                            LeafRuffleWaveAt(q, t, low + 1.0, phase, fineWeight),
                            l.ruffleWaves - low);
            }

            // The leaf's frame: T the thinnest axis, L the longer of the other two, W their
            // cross, so the frame is a rotation of the mesh's own and the winding survives it.
            void LeafAxes(float3 he, out float3 axisL, out float3 axisT, out float3 axisW)
            {
                if (AtMost(he.x, he.y) && AtMost(he.x, he.z))
                {
                    axisT = float3(1, 0, 0);
                    axisL = AtLeast(he.y, he.z) ? float3(0, 1, 0) : float3(0, 0, 1);
                }
                else if (AtMost(he.y, he.z))
                {
                    axisT = float3(0, 1, 0);
                    axisL = AtLeast(he.x, he.z) ? float3(1, 0, 0) : float3(0, 0, 1);
                }
                else
                {
                    axisT = float3(0, 0, 1);
                    axisL = AtLeast(he.x, he.y) ? float3(1, 0, 0) : float3(0, 1, 0);
                }

                axisW = cross(axisL, axisT);
            }

            // One point of the leaf in its own frame (x along, y thick, z across), object units.
            // baseSign says which end of the box the base is at; the width axis turns with it so
            // the frame stays a rotation. flat drops the curl, for the carve's field, which is a
            // property of the tissue and should not move when the leaf is drawn curled.
            float3 LeafPoint(float s, float t, float side, LeafShape l, float baseSign,
                             float meshHalf, float curlObj, float flat, float flesh, float ruffleAmp, out float thickness)
            {
                float w = LeafWidth(s, l);
                float ends = pow(max(1e-4, sin(PI * pow(saturate(s), l.widest))), 0.3);
                float across = t * w;

                // The scalloped edge, only inward (LeafScallop), behind the dial's own value. A blade
                // under the marine producers' dial has no lobes: its scallop is at zero depth.
                [branch] if (_Frill > 0.001 && l.alga < 0.5)
                {
                    across *= LeafScallop(s, t, l);
                }

                // A strap's frayed tip under the marine producers' dial (AlgaFray), only inward.
                [branch] if (l.alga > 0.5)
                {
                    across *= AlgaFray(s, t, l);
                }
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                // Level 3: the blade's margin scalloped inward (AlgaBlade3Scallop), a factor in (0, 1] on the distance across, on a
                // frond (which is the blade under this keyword) alone. Before the curl reads the distance across, so the lift follows it.
                [branch] if (l.alga > 1.5) across *= AlgaBlade3Scallop(s, t);
#endif

                // The lens: full at the midrib, which stands a little proud, and thinning to
                // nothing at the rim; thinner towards the tip.
                float lens = sqrt(saturate(1.0 - t * t)) * (0.6 + 0.4 * exp(-t * t / 0.012));

                // Under the marine producers' dial the midrib's ridge goes, but for a long strap's faint
                // kelp midrib (AlgaKelpMidrib of it): never thicker than the lens above at any t.
                [branch] if (l.alga > 0.5)
                {
                    lens = sqrt(saturate(1.0 - t * t)) * (0.6 + 0.4 * AlgaKelpMidrib * l.kelp * exp(-t * t / 0.012));

                    // A frond's lens (AlgaFrondLens): a round stem, thin leaflets, and a bladder's circle where it is
                    // thicker, which is why it is given what multiplies the lens below. Never past one but at a bladder,
                    // which is bound by LeafRoom (its l.kelp, counted in the room).
                    [branch] if (l.alga > 1.5)
                        lens = AlgaFrondLens(s, t, l, meshHalf, meshHalf * ends * lerp(1.0, 0.55, saturate(s)) * flesh);
                }

                thickness = meshHalf * lens * ends * lerp(1.0, 0.55, saturate(s)) * flesh;

                float arch = 1.0 - (2.0 * s - 1.0) * (2.0 * s - 1.0);

                // The frill: ruffles along the margin, out of step on the two sides, growing from
                // nothing at the stalk and strongest at the rim. It is still; the owner ruled out
                // a flutter, and the curl budget bounds it.
                // Two ruffles at incommensurate lengths and a slow swell over them, so the margin
                // never repeats the way a crimped ribbon does.
                float a = abs(across);
                float side2 = across < 0.0 ? 0.37 : 0.0;
                float x = l.frills * s + l.phase + side2;
                float ruffle = 0.72 * sin(6.2831853 * x) + 0.28 * sin(6.2831853 * (2.37 * x + 0.21));
                float swell = 0.6 + 0.4 * sin(6.2831853 * (0.61 * x + l.phase * 1.7));
                float frill = a * a * ruffle * swell * smoothstep(0.04, 0.3, s);
                float lift = l.cup * across * across + l.arch * arch + l.wave * frill;

                float along = baseSign * meshHalf * (1.0 - 2.0 * s);

                float thin = side * thickness + (1.0 - flat) * curlObj * lift;

                // The out of plane ruffle at the margin (_LeafLook; ruffleAmp is its amplitude in thin-axis object
                // units, zero with the dial off). Not in the carve's flat position, which is the tissue's.
                [branch] if (ruffleAmp > 0.0 && flat < 0.5)
                {
                    float rim = smoothstep(LeafRuffleStart, 1.0, abs(t));
                    float stalk = smoothstep(0.05, 0.3, s);

                    float q = t >= 0.0 ? 0.5 * s : 1.0 - 0.5 * s;
                    float waves = LeafRuffleWavesLow + floor(LeafRuffleWavesSpan * frac(_Carve.x * 11.273 + 0.419));
                    float phase = frac(_Carve.x * 6.131 + _Individual * 2.77 + 0.683);
                    float k1 = 6.2831853 * (waves * q + phase);
                    float k2 = 6.2831853 * ((2.0 * waves + 1.0) * q + 2.3 * phase);
                    float wave = (1.0 - LeafRuffleHarmonic) * sin(k1) + LeafRuffleHarmonic * sin(k2);

                    // Level 2: the fine row, mixed in by a convex weight so |wave| stays within 1 (see above).
                    [branch] if (_LeafLook > 1.5)
                    {
                        float fineWaves = max(1.0, round(waves * LeafRuffleFineRatio));
                        float fine = sin(6.2831853 * (fineWaves * q + frac(phase * 3.71 + 0.317)));
                        float fineMix = LeafRuffleFineShare * smoothstep(LeafRuffleFineStart, 1.0, abs(t));

                        wave = lerp(wave, fine, fineMix);
                    }

                    float room = max(0.0, meshHalf - thickness - LeafRuffleCurlShare * abs(curlObj * lift));

                    // Under the marine producers' dial the wave's count is the proportions' (AlgaRuffleWave), and
                    // the ruffle leaves the twist its share of the room (AlgaTwistRoomShare times the strap's).
                    // Two whole branches, so the leaf dial's own line is the one it always was.
                    [branch] if (l.alga > 0.5)
                    {
                        // A round blade (the fan, AlgaRoundOfAspect, from the aspect its tip's length carries) also rolls
                        // its margin toward the face its cup bows toward, the outer AlgaRollWidth of the way out
                        // (max of the distance across and along, 1 at the outline), by at most AlgaRollRoomShare of the
                        // room times its roundness, which the ruffle gives up, so the two are at most the ruffle's
                        // share and the strap's twist's, which a round blade has none of.
                        float algaRound = AlgaRoundOfAspect(0.5 * AlgaStrapWidth / max(1e-6, l.tipLen));
                        float share = LeafRuffleRoomShare - AlgaTwistRoomShare * l.strap - AlgaRollRoomShare * algaRound;
                        thin += min(ruffleAmp, share * room) * rim * stalk * AlgaRuffleWave(q, t, l);

                        float rollEdge = smoothstep(1.0 - AlgaRollWidth, 1.0, max(abs(t), abs(2.0 * s - 1.0)));
                        float rollAmp = ruffleAmp * (AlgaRollHeight / LeafRuffleAmplitude);
                        thin += sign(l.cup) * min(rollAmp, AlgaRollRoomShare * algaRound * room) * rollEdge * rollEdge * stalk;
                    }
                    else
                    {
                        thin += min(ruffleAmp, LeafRuffleRoomShare * room) * rim * stalk * wave;
                    }
                }

#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                // Level 3: the margin's wave out of the plane (AlgaBlade3Wave), within AlgaBlade3RoomShare of the room the lens and the curl
                // leave, as the ruffle above is within its share; LeafRoom gives the same share up from the sway. Not in the carve's
                // flat position.
                [branch] if (l.alga > 1.5 && flat < 0.5)
                {
                    float waveRoom = max(0.0, meshHalf - thickness - LeafRuffleCurlShare * abs(curlObj * lift));

                    thin += AlgaBlade3Wave(s, t, meshHalf, waveRoom);
                }
#endif
                // The strap's twist under the marine producers' dial: the sheet along its thin axis by its
                // distance across (across, in [-1, 1], nothing at the midline and at the tip, where the
                // outline closes) times a sine along its length, so the two margins go opposite ways. At
                // most AlgaTwistRoomShare of the strap's share of the room at this point, the ruffle's room
                // above less its own; so the lens, the curl, the ruffle and the twist together are inside
                // the box wherever the curl alone is. Not in the carve's flat position.
                [branch] if (l.alga > 0.5 && l.strap > 0.0 && flat < 0.5)
                {
                    float twistStalk = smoothstep(0.05, 0.3, s);
                    float twistPhase = frac(_Carve.x * 8.443 + 0.157);
                    float twistWave = sin(6.2831853 * (l.twists * s + twistPhase));

                    float twistRoom = max(0.0, meshHalf - thickness - LeafRuffleCurlShare * abs(curlObj * lift));

                    thin += min(l.twistAmp, AlgaTwistRoomShare * l.strap * twistRoom) * across * twistStalk * twistWave;
                }

                // A frond's droop (AlgaFrondDroop; l.lobes is its size at the rim, l.twists its waves along the blade): each
                // side hangs out of the plane by the square of its distance from the stem times a slow wave along it,
                // mostly toward the face the arch bows toward and a little the other way, the two sides out of step. It
                // is no part of the room (the twist above, at zero for a frond, was), and LeafRoom takes its most off the
                // sway's. Not in the carve's flat position.
                [branch] if (l.alga > 1.5 && flat < 0.5)
                {
                    float droopPhase = frac(_Carve.x * 8.443 + 0.157) + (across < 0.0 ? AlgaFrondSide : 0.0);
                    float droopWave = AlgaFrondDroopBias +
                        (1.0 - AlgaFrondDroopBias) * sin(6.2831853 * (l.twists * s + droopPhase));
                    float droopStalk = smoothstep(0.05, 0.3, s);

                    thin += l.lobes * across * across * droopWave * droopStalk * (l.arch < 0.0 ? -1.0 : 1.0);
                }

                return float3(along, thin, baseSign * meshHalf * across);
            }

            // The leaf's shaped position (object units), its flat position for the carve, its
            // world normal, and its local half thickness in metres.
            void ShapeLamina(
                float4 leafOS, float3 halfExtents, float meshHalf,
                out float3 positionOS, out float3 flatOS, out float3 normalWS,
                out float halfThickMetres, out float halfWidthMetres, out float4 leafOut)
            {
                float3 axisL, axisT, axisW;
                LeafAxes(halfExtents, axisL, axisT, axisW);

                float hL = dot(halfExtents, abs(axisL));
                float hT = dot(halfExtents, abs(axisT));
                float hW = dot(halfExtents, abs(axisW));

                // LeafOf's shape, or the marine producers' (BladeShapeOf, AlgaShapeOf) under their dial.
                LeafShape l = BladeShapeOf(hL, hW, hT);

                // The base at the end the part's own joint is on, and by the seed for a root or a
                // joint on the middle of a face.
                float anchorAlong = dot(_Leaf.xyz, axisL);
                float baseSign = frac(_Carve.x * 13.37 + 0.271) < 0.5 ? -1.0 : 1.0;
                if (_Leaf.w > 0.5 && abs(anchorAlong) > 0.05) baseSign = anchorAlong > 0.0 ? 1.0 : -1.0;

                // The curl in object units on the thin axis: a fraction of the full width, in
                // units of the full thickness.
                float curlObj = _CurlFraction * hW / max(1e-6, hT);

                // A blade is drawn at most fleshy: its half thickness at the midrib is at most a
                // third of its half width. Round 47's photosynthetic boxes are nearly as thick as
                // they are wide, and a lens that fills one is a pillow, not a blade. Thinner is
                // inside the box, so the collider's bound holds.
                float flesh = min(1.0, 0.35 * hW / max(1e-6, hT));

                // Under the marine producers' dial a blade is a thin membrane: its half thickness at the thickest
                // is at most AlgaThinHalf of its half width (AlgaFleshCap). Only ever thinner, so inside the box.
                [branch] if (_Alga > 0.001) flesh = min(flesh, AlgaFleshCap(l, hW, hT, meshHalf));
                halfWidthMetres = hW;

                float s = leafOS.x;
                float t = leafOS.y;
                float side = leafOS.z;

                // The leaf dial's ruffle at the margin, in thin-axis object units (an object unit there is the
                // blade's full thickness): LeafRuffleAmplitude of the half width in metres. Zero with the dial
                // off, and LeafPoint tests for it.
                float ruffleAmp = 0.0;
                [branch] if (_LeafLook > 0.5) ruffleAmp = LeafRuffleAmplitude * hW / max(1e-6, 2.0 * hT);

                // Under the marine producers' dial a sheet's margin ruffles with the leaf dial off as well, by the
                // leaf dial's amplitude times how far from a strap the blade is; with the leaf dial on every blade
                // keeps its ruffle. The room in LeafPoint bounds it as it bounds the leaf dial's.
                [branch] if (_Alga > 0.001)
                {
                    ruffleAmp = LeafRuffleAmplitude * hW / max(1e-6, 2.0 * hT) * (_LeafLook > 0.5 ? 1.0 : 1.0 - l.strap);

                    // A frond has no ruffle: its leaflets and bladders are the margin, and the room is theirs and the twist's.
                    [branch] if (l.alga > 1.5) ruffleAmp = 0.0;
#if defined(_EVO_ALGA_BLADE)
                    // The broad blade ruffles its margin out of the plane (LeafPoint, AlgaRuffleWave), bounded by the room as the
                    // sheet's is: AlgaBladeRuffle times the leaf dial's amplitude, and at most the ruffle's share of the room.
                    [branch] if (l.alga > 1.5) ruffleAmp = AlgaBladeRuffle * LeafRuffleAmplitude * hW / max(1e-6, 2.0 * hT);
#endif
                }

                float thick;
                float3 p = LeafPoint(s, t, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, ruffleAmp, thick);
                float unused;
                float3 f = LeafPoint(s, t, side, l, baseSign, meshHalf, curlObj, 1.0, flesh, ruffleAmp, unused);

                positionOS = axisL * p.x + axisT * p.y + axisW * p.z;
                flatOS = axisL * f.x + axisT * f.y + axisW * f.z;

                // The normal from two differences in metres, not object units: the sheet is
                // stretched by the part's three sizes, and a normal taken before the stretch would
                // shade a long leaf as if it were square. Off the ends and the rim, where the
                // outline's width and the lens's slope run to nothing and infinity.
                float3 metres = 2.0 * float3(hL, hT, hW);
                float sd = clamp(s, 0.01, 0.98);
                float td = clamp(t, -0.985, 0.975);
                float3 q = LeafPoint(sd, td, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, ruffleAmp, unused) * metres;
                float3 qs = LeafPoint(sd + 0.01, td, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, ruffleAmp, unused) * metres;
                float3 qt = LeafPoint(sd, td + 0.01, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, ruffleAmp, unused) * metres;

                // Outward on either face: the parametrisation's own handedness, fixed by the frame
                // being a rotation and the width turning with the base (TheatreMeshes.BuildLamina).
                float3 n = cross(qs - q, qt - q) * side;

                float3 nOS = axisL * n.x + axisT * n.y + axisW * n.z;

                float3 cx = normalize(UNITY_MATRIX_M._m00_m10_m20);
                float3 cy = normalize(UNITY_MATRIX_M._m01_m11_m21);
                float3 cz = normalize(UNITY_MATRIX_M._m02_m12_m22);

                normalWS = normalize(nOS.x * cx + nOS.y * cy + nOS.z * cz + 1e-12);

                halfThickMetres = thick * 2.0 * hT;
                // w carries the flag (above a half) and the leaf's aspect, its half width over
                // its length, which the veins need to hold their angle in metres.
                leafOut = float4(s, t * LeafWidth(s, l), t, 1.0 + hW / max(1e-6, 2.0 * hL));
            }

            // ---------------------------------------------------------------- the sway dial
            //
            // Sway and pulse at rest (_Sway, EVOSIM_THEATRE_SWAY), so a body that is not swimming
            // still reads as alive. A blade bends about its own anchor, more toward its free end, as
            // a frond does in a slow swell; a float draws in along its smallest axis and lets go, as
            // a medusa's bell does. Nothing else moves: a strut, a stomach, a link or a holdfast that
            // swelled would read as the creature doing something, and the film's one rule about
            // motion is that a viewer can tell the world's from the camera's and from decoration.
            //
            // Every term that names _Sway reaches a vertex only behind a test of it, and the palette
            // never sets it with its dial off, so a frame with the dial off is the frame from before.
            //
            // The clock is EvoTime(), the one the caustics and the snow already move by, so a frame's
            // sway is a property of the second it is drawn at and not of how long it took to draw.
            //
            // The bound, which is the point. Neither term can put a vertex outside the part's
            // collider. The float's draw is a squeeze toward the visual's own centre plane along one
            // of its axes, and every collider here is mirror symmetric across that plane, so a
            // squeezed point lies between the point and its mirror image and is inside wherever the
            // point was. The blade moves only along its thin axis, and by one amplitude for the whole
            // blade: the most that leaves every station inside its box once the lens and the curl
            // have taken their most there (LeafRoom, LeafSwayAmplitude). A blade the curl already
            // carries out of its box holds still. A blade at least two fifths as thick as it is wide
            // keeps the whole swing, as round 47's plump blades do; one under a quarter keeps little
            // of it or none.

            // How far a blade's free end swings, as a fraction of its largest half extent.
            static const float SwayLeafTip = 0.03;

            // How far a float draws in at the height of its pulse, as a fraction of its smallest half
            // extent, measured at its surface.
            static const float SwayFloatDraw = 0.04;

            // What a part does under the dial (_SwayPart.x, TheatrePalette.SwayOf).
            static const float SwayKindLeaf = 1.0;
            static const float SwayKindFloat = 2.0;

            // A blade with no anchor, the body's own root, bends about its middle: both ends lift
            // together, as a gull's wings do.
            static const float SwayRootStation = 0.5;

            // The stations along the blade its room is read at, one more than this from base to tip,
            // and the share of the tightest the swing may spend. The room changes slowly along the
            // blade, and the margin keeps what falls between two stations inside.
            static const int SwayRoomStations = 16;
            static const float SwayRoomMargin = 0.85;

            // LeafPoint's lens at a fifth of the way across, and what its midrib's ridge still adds
            // three tenths of the way across, both rounded up, for LeafMostLift's three pieces.
            static const float SwayLensAtFifth = 0.602;
            static const float SwayRidgeTail = 2.3e-4;

            // Where this part is in its own beat, in radians. The cycle is wrapped before the sine,
            // so the angle stays small however long the clock has run.
            float SwayAngle()
            {
                return 6.2831853 * frac(EvoTime() * _SwayPart.z + _SwayPart.y);
            }

            // One away from every joint anchor this visual knows, falling to nothing at one, by the
            // pinch's own reach: the tissue where a neck meets a part holds still, so a blade that
            // carries a child at its tip, or a float that carries a limb, does not move off the neck.
            float SwayHold(float3 positionOS)
            {
                return 1.0 - saturate(EvoPinch(positionOS, _PinchA) + EvoPinch(positionOS, _PinchB));
            }
#if defined(_EVO_THALLI)

            // ---------------------------------------------------------------- the thallus
            //
            // EVOSIM_THEATRE_THALLI (TheatreSkin.ThalliOn): a compact photosynthetic box is drawn as a seaweed thallus, a mesh
            // TheatreMeshes builds whole (Ulva, Padina or Codium; TheatreMeshes.Thallus), inside the box it is scaled to. Its solid
            // flag (UV3.x) is ThalliFlagUlva, Padina or Codium, all under a half, so every test above reads a thallus as neither the
            // box, the blade nor the skin. Here it is not carved (its form is in the mesh, and a sheet a few hundredths of its box
            // thick would be cut through), and it sways along the two axes across its growth by the reach the mesh baked per vertex
            // (UV4.x, object units: at most the room that vertex has on those axes), then is clamped into the mesh's box. Its
            // vertex colour carries its tone (r), lime (g) and thinness (b), which the fragment reads.
            static const float ThalliFlagUlva = 0.2;
            static const float ThalliFlagPadina = 0.25;
            static const float ThalliFlagCodium = 0.3;
            static const float ThalliFlagTolerance = 0.02;

            // The share of the baked reach the swing spends, the swing on the second axis as a share of the first, and the
            // phase between the two (radians), so the free end moves round a small ellipse.
            static const float ThalliSwayShare = 0.9;
            static const float ThalliSwayAcross = 0.6;
            static const float ThalliSwayTurn = 1.3;

            // Padina's lime: where the vertex colour's g is one the body is greyed this far toward its own luminance and lifted
            // by this much, so a dim or starving body's rim stays dim.
            static const float ThalliLimeGrey = 0.7;
            static const float ThalliLimeLift = 1.6;

            // Codium's felt: the sheen replaced by a weak, broad one, as the velvet's is.
            static const float ThalliMatteSheenGain = 0.12;
            static const float ThalliMatteSheenPower = 6.0;

            // A sheet's (Ulva's, Padina's) rim scale, as a blade's: on a thin face the guild's rim is paint.
            static const float ThalliSheetRimScale = 0.5;

            // Ulva's light from the surface through its sheets, as a share of the leaf's (_LeafSkyGlow).
            static const float ThalliUlvaSkyGlow = 0.6;

            bool ThalliIs(float flag)
            {
                return abs(flag - ThalliFlagUlva) < ThalliFlagTolerance || abs(flag - ThalliFlagPadina) < ThalliFlagTolerance ||
                    abs(flag - ThalliFlagCodium) < ThalliFlagTolerance;
            }

            bool ThalliIsUlva(float flag) { return abs(flag - ThalliFlagUlva) < ThalliFlagTolerance; }
            bool ThalliIsCodium(float flag) { return abs(flag - ThalliFlagCodium) < ThalliFlagTolerance; }

            // The sway: w is the growth axis (0, 1, 2 for x, y, z), and the move is along the other two alone. Each component is
            // at most ThalliSwayShare * reach, and the mesh baked the reach at most the room on those axes, so the vertex stays in
            // the box; the caller clamps it as well. y is the vertex's lag in cycles, so the thallus waves from base to tip.
            void ThalliSway(float4 reach, float3 flatOS, inout float3 positionOS)
            {
                float axisU = round(reach.w);
                float3 axisA = axisU < 0.5 ? float3(0.0, 1.0, 0.0) : (axisU < 1.5 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0));
                float3 axisB = axisU < 0.5 ? float3(0.0, 0.0, 1.0) : (axisU < 1.5 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0));

                float angle = SwayAngle() - 6.2831853 * reach.y;
                float swing = ThalliSwayShare * max(0.0, reach.x) * SwayHold(flatOS);

                positionOS += swing * (sin(angle) * axisA + ThalliSwayAcross * cos(angle + ThalliSwayTurn) * axisB);
            }
#endif

            // The most of thick * lens(t) + grow * t^2 over t in [0, 1], or a little more, never less:
            // how far one face of the blade stands off its middle at a station, the lens's share and
            // the cup's and the frill's together, at whichever distance across puts it furthest out.
            // Adding the two maxima would be simpler and is twice as loose, since the lens is thickest
            // at the midrib and the cup lifts most at the rim. The lens falls as t grows, so it is
            // bounded on three pieces: up to a fifth of the way across by its value at the midrib;
            // from a fifth to three tenths by its value at a fifth; and beyond, where the midrib's
            // ridge has all but gone, by 0.6 sqrt(1 - t^2) plus what is left of the ridge, a sum
            // that is concave in t^2 and so has its top where its slope is nothing.
            float LeafMostLift(float thick, float grow)
            {
                float midrib = thick + 0.04 * grow;
                float shoulder = SwayLensAtFifth * thick + 0.09 * grow;

                float q = 0.3 * thick / max(1e-6, grow);
                float tau = clamp(1.0 - q * q, 0.09, 1.0);
                float flank = 0.6 * thick * sqrt(1.0 - tau) + grow * tau + SwayRidgeTail * thick;

                return max(midrib, max(shoulder, flank));
            }

            // The room a blade has on its thin axis at station s, in object units: the box's half
            // thickness less the most the lens and the curl can take there, on either face. Each term
            // is LeafPoint's own at its bound: the width at most the outline before its lobes, which
            // only ever cut in; the cup and the frill at most their weights times the width squared,
            // times t squared; and the arch the same at every t. Negative where the curl already
            // leaves the box.
            float LeafRoom(float s, LeafShape l, float meshHalf, float curlObj, float flesh)
            {
                s = saturate(s);

                float u = pow(s, l.widest);
                float crown = max(1e-4, sin(PI * u));
                float outline = pow(crown, u < 0.5 ? l.baseFull : l.tipFull);

                // Under the marine producers' dial the outline is theirs, before its fray, which only cuts in.
                // Its lens is the one below or thinner (its ridge falls), and its curl keeps the budget.
                [branch] if (l.alga > 0.5)
                {
                    outline = AlgaOutline(s, l);

                    // A frond's bladders (AlgaFrondBead) stand off the stem by l.kelp of the box's half thickness, between
                    // AlgaFrondRoomLow and High, where all of them are: counted here as the blade's thickness so the sway
                    // and the twist's room is what is left. The flesh is raised to give exactly that thickness below.
                    [branch] if (l.alga > 1.5 && s > AlgaFrondRoomLow && s < AlgaFrondRoomHigh)
                        flesh = max(flesh, l.kelp / max(1e-4, pow(crown, 0.3) * lerp(1.0, 0.55, s)));
                }

                float ends = pow(crown, 0.3);
                float thick = meshHalf * ends * lerp(1.0, 0.55, s) * flesh;

                float arch = 1.0 - (2.0 * s - 1.0) * (2.0 * s - 1.0);
                float standing = abs(curlObj) * abs(l.arch) * arch;

                // A frond's droop (LeafPoint) is the most l.lobes times the distance across squared, taken as standing
                // at its most over the outline, so the sway keeps only what the droop leaves.
                [branch] if (l.alga > 1.5) standing += l.lobes * outline * outline;

                float grow = abs(curlObj) * (abs(l.cup) + l.wave * smoothstep(0.04, 0.3, s)) * outline * outline;

                float room = meshHalf - standing - LeafMostLift(thick, grow);

                // Under the leaf dial the margin's ruffle spends up to LeafRuffleRoomShare of the room at
                // every point (LeafPoint), and this station's room is no more than any point's across it, so
                // the sway keeps the rest and the two together stay inside the box. Under the marine producers'
                // dial the ruffle and the twist spend that share between them (LeafPoint), so the same holds.
                [branch] if (_LeafLook > 0.5 || _Alga > 0.001) room *= 1.0 - LeafRuffleRoomShare;
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                // Level 3: the margin's wave out of the plane spends AlgaBlade3RoomShare of the full room (AlgaBlade3Wave), and the sway
                // keeps what is left: the room above is already the sway's (1 - LeafRuffleRoomShare) of it, so it is cut by the wave's
                // share over that. Only a frond, which is the blade under this keyword.
                [branch] if (l.alga > 1.5) room *= 1.0 - AlgaBlade3RoomShare / (1.0 - LeafRuffleRoomShare);
#endif

                return room;
            }

            // How far the blade's further free end may swing, in object units on its thin axis: what
            // is wanted, or less, so that the bend, the square of the distance from the anchor over the
            // further end's, stays inside the room at every station. One number for the whole blade, so
            // the bend keeps its shape and has no crease where the room runs out.
            float LeafSwayAmplitude(float anchorStation, float span, LeafShape l,
                                    float meshHalf, float curlObj, float flesh, float want)
            {
                float amplitude = want;

                for (int i = 0; i <= SwayRoomStations; i++)
                {
                    float s = (float)i / SwayRoomStations;
                    float x = (s - anchorStation) / span;
                    float bend = x * x;

                    if (bend > 1e-3)
                        amplitude = min(amplitude, SwayRoomMargin * LeafRoom(s, l, meshHalf, curlObj, flesh) / bend);
                }

                return max(0.0, amplitude);
            }

            // ---------------------------------------------------------------- the bend with the current
            //
            // A marine producer's blade leans with the water (_EvoAlgaBend, EVOSIM_THEATRE_ALGA_BEND), which needs
            // the sway dial and the marine producers' dial both. The water is the flow past the blade: the
            // world's current where the blade is, less the blade's own velocity (TheatreSkin, LiveWorldView), its
            // horizontal direction and speed packed per blade into _SwayPart.w (AlgaBendFlow; TheatrePalette.Flow).
            // None of it is a vertex input. LeafSway adds three terms, all inside the room the sway already
            // reads (LeafRoom through LeafSwayAmplitude), the sway giving up AlgaBendSwayKeep of its swing:
            //   stream  along the thin axis, toward the side the flow is on, as x^2 (x the distance from the
            //           anchor over the far end's): a long blade over its thickness streams, a short one barely;
            //   flutter along the thin axis, a wave along the length at AlgaBendFlutterHertz, on a sheet
            //           (1 - the strap's share) alone, where a strap streams;
            //   lateral along the width axis, toward the flow, by at most AlgaBendLateral of the room the outline
            //           leaves beside it at that station (1 - LeafWidth), so none leaves the box.
            // The thin axis total is at most AlgaBendSwayKeep + AlgaBendStream + AlgaBendFlutter = 1 of the room's
            // amplitude, so it holds the bound that LeafSwayAmplitude's margin (SwayRoomMargin) gives the sway.
            // That room is the box's half thickness less the lens and the curl, which on a thin blade is little or none
            // (the curl's lift grows with the half width over the half thickness, and the lens takes its share), so the
            // three terms above are a fraction of a thickness in metres and the dial at one moves a rim by a pixel or
            // two. A dial above one
            // adds a fourth term that does not read the room (AlgaBendReach, below).

            // The share of the sway's own swing kept when the bend is on, so the two together fit the room.
            static const float AlgaBendSwayKeep = 0.5;

            // Shares of the room's amplitude the stream and the flutter may spend, and of the width room the lateral move.
            static const float AlgaBendStream = 0.35;
            static const float AlgaBendFlutter = 0.15;
            static const float AlgaBendLateral = 0.8;

            // The length over the thickness (hL / hT) at which the stream begins and is whole.
            static const float AlgaBendSlenderLow = 4.0;
            static const float AlgaBendSlenderHigh = 40.0;

            // The dial past one (EVOSIM_THEATRE_ALGA_BEND from 1 to 4). Its second part, more = (dial - 1) / AlgaBendMoreSpan
            // in [0, 1], leans a blade by a length and not by the room: AlgaBendReach of its half length at the free end,
            // the same x^2 along it, toward the flow on the thin axis and on the width axis alike, times the flow share
            // raised to 1 / (1 + AlgaBendFlowRaise more) (a slow current counts for more), the gust and the hold, and a
            // smoothstep of the half length over the half thickness from AlgaBendReachSlenderLow to High (a plump blade
            // stays put). It leaves the box, as the curl is ruled to; only the room is dropped, nothing else.
            static const float AlgaBendMoreSpan = 3.0;
            static const float AlgaBendFlowRaise = 3.0;
            static const float AlgaBendReach = 0.5;
            static const float AlgaBendReachSlenderLow = 2.0;
            static const float AlgaBendReachSlenderHigh = 8.0;

            // The gust: the stream swells and eases over this many seconds, to 1 - AlgaBendGust at its slackest.
            static const float AlgaBendSurgeSeconds = 7.0;
            static const float AlgaBendGust = 0.25;

            // The flutter's cycles per second, and its waves along the blade's length.
            static const float AlgaBendFlutterHertz = 0.8;
            static const float AlgaBendFlutterWaves = 1.3;

            // How TheatrePalette.Flow packs the flow into _SwayPart.w: 1 + azimuth step + steps * speed step, zero
            // for none. The azimuth is the flow's bearing in the horizontal plane from +x toward +z in 360 steps;
            // the speed is 1 - exp(-metres per second / TheatrePalette.AlgaBendSpeedScale) in 255 steps, the first of
            // which is no flow.
            static const float AlgaBendAzimuthSteps = 360.0;
            static const float AlgaBendSpeedSteps = 255.0;

#if defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
            // ---------------------------------------------------------------- the blade's arc bend, levels 1 and 2
            //
            // EVOSIM_THEATRE_ALGA_ARC at 1 and 2 (the keyword _EVO_ALGA_ARC, beside _EVO_ALGA_BLADE and _EVO_ALGA_BLADE2, which
            // TheatreSkin enables only at those levels). The level is _EvoAlgaArc.x. It is a dial of its own: the lean above
            // (EVOSIM_THEATRE_ALGA_BEND) is read and drawn as it always was at every value, and the flow is the one above
            // (_SwayPart.w, AlgaBendFlow), which only that dial's plumbing writes, so the arc needs that dial above zero.
            // A broad blade (l.alga above 1.5) bends as a rod does, in the closed form of an angle bend: the tangent turns by
            // theta(u) = tip u^2, u the arc from the anchor as a share of the arc to the far end (|x|), so the point at u moves
            // sideways by tip L u^3 / 3 and toward the anchor by tip^2 L u^5 / 10 (L the arc from the anchor to the far end, in
            // metres). The blade turns and does not stretch, and its normal is turned by theta(u). Each axis across the long
            // axis has its own tip angle, from the flow's component on it, so the bend is in the flow's plane. The tip angle is
            // the axis' limit times the drive: the current (the flow share to AlgaArcCurrentPower, a slow current counts for
            // more) times a wave along the blade (one sin; the phase is the sway's own, _SwayPart.y, which differs by plant)
            // times the hold.
            //   Meeting the lean. With the lean's strength above one it spends room past the box (AlgaBendReach), so the arc
            //   yields and does nothing on that blade. With the lean on at one or less it has kept AlgaBendSwayKeep of the
            //   sway's swing and spends up to AlgaBendStream of its strength of the room's amplitude on the thin axis and up to
            //   AlgaBendLateral of its strength of the width room; the arc takes what is left of each. With the lean off the arc
            //   does the keeping itself, and takes the rest of the thin room and AlgaBendLateral of the width room.
            //   Level 1, inside the box. The thin limit is 3 times the room it has (metres, the x^2 amplitude LeafSwayAmplitude
            //   gives; LeafRoom is already net of the ruffle's share and blade level 3's wave share) over L, because u^3 is
            //   under u^2, the shape the room is read in. The width limit is 3 times its share of the room the widest station
            //   leaves beside the outline, over L, that room being one less the envelope's peak (AlgaBladeBound's, a hash) of
            //   the half width. Each is at most AlgaArcTipCap times the flow's component, so every vertex stays inside the box.
            //   Level 2 is a trial beyond the collider (D242): the thin axis' tip angle is at least AlgaArc2OutAngle times the
            //   flow's component, with no limit from the box. A vertex at the blade's tip can stand outside the box by
            //   L tip / 3 less the box's half thickness (TheatrePalette.LogArcTrial prints it per blade). The width axis
            //   keeps its limit.
            static const float AlgaArcTipCap = 0.5236;           // the largest tip angle of either level, radians (30 degrees)
            static const float AlgaArc2OutAngle = 0.3491;        // level 2's thin-axis tip angle, radians (20 degrees)
            static const float AlgaArcCurrentPower = 0.5;        // the flow share is raised to this
            static const float AlgaArcWaveHertz = 0.3;           // the wave along the blade, cycles per second
            static const float AlgaArcWaves = 0.5;               // and its waves along the blade's arc
            static const float AlgaArcWaveShare = 0.35;          // its swing of the drive, either way; the drive stays in [0, 1]
            //   The room (_EvoAlgaArc.y, EVOSIM_THEATRE_ALGA_ARC_ROOM, a preview beyond D242). A point at the far end moves sideways by
            //   tip L / 3, so a reach of y L beyond the box allows a thin-axis tip angle of 3 (the thin room over L, as level 1 has it, plus
            //   y). Level 1's thin limit is that angle and its cap the larger of AlgaArcTipCap and it; level 2's thin tip angle is the
            //   larger of AlgaArc2OutAngle and it; each times the drive and the flow's component as before. The angle is at most
            //   AlgaArcRoomCeiling, because the closed form (theta = tip u^2, its sine and cosine cut to a few terms) stops following a
            //   rod near a radian: at 60 degrees the cut cosine is about 0.05 off the true one.
            static const float AlgaArcRoomCeiling = 1.0472;      // the largest thin-axis tip angle with a room, radians (60 degrees)
#if defined(_EVO_ALGA_FLUTTER)
            //   The flutter (_EvoAlgaArc.z, EVOSIM_THEATRE_ALGA_FLUTTER): a blade in a current along its plane is not pushed across its thin
            //   axis, but a flexible sheet still ripples out of its plane, as a flag does. A wave runs from the anchor to the free end and
            //   is added to the thin axis' angle, then the angle is cut to the axis' limit, so the box (or ROOM's reach) is never left.
            static const float AlgaFlutterHertz = 0.5;           // the wave along the blade, cycles per second
            static const float AlgaFlutterWaves = 1.0;           // and its waves along the blade's arc
#if defined(_EVO_ALGA_FLUTTER_NOISE)
            //   The flutter's noise (_EvoAlgaArc.w, EVOSIM_THEATRE_ALGA_FLUTTER_NOISE): at its value v each blade ripples at a frequency and an
            //   amplitude of its own, and a slow gust swells and slackens the whole ripple, each blade's gust offset in time. The blade is
            //   the part, and its number is _Individual (TheatrePalette.OwnOf: the creature's seed turned by the part's index), which
            //   differs by part; _SwayPart.y does not, for it is the creature's phase plus a fixed lag per joint, so parts at one depth of
            //   one body share it. Three hashes of _Individual: the frequency, the amplitude factor and the gust's offset.
            static const float AlgaFlutterNoiseHertzLow = 0.3;   // a blade's ripple frequency at v = 1 is drawn from this to
            static const float AlgaFlutterNoiseHertzHigh = 0.7;  // this, cycles per second
            static const float AlgaFlutterNoiseAmpLow = 0.7;     // and its amplitude factor from this
            static const float AlgaFlutterNoiseAmpHigh = 1.3;    // to this (mean one, so the dial needs no compensation)
            static const float AlgaFlutterGustFloor = 0.4;       // the gust's low, as a multiple of the ripple
            static const float AlgaFlutterGustCeiling = 1.6;     // and its high (a mean near one; peaks over the limit are cut by the clamp)
            static const float AlgaFlutterGustHertzA = 0.053;    // the gust is three sines at these, cycles per second (periods near
            static const float AlgaFlutterGustHertzB = 0.089;    // 19, 11 and 7 seconds; 53, 89 and 137 thousandths are primes, so no
            static const float AlgaFlutterGustHertzC = 0.137;    // two repeat in step) averaged and mapped from [-1, 1] to floor..ceiling
            static const float AlgaFlutterGustSpread = 60.0;     // a blade's gust is this many seconds at most ahead of the clock

            // A short standard hash of two numbers to [0, 1): the sine of their dot with a fixed pair, times a large constant, fractional.
            float AlgaFlutterHash(float seed, float salt)
            {
                return frac(sin(dot(float2(seed, salt), float2(12.9898, 78.233))) * 43758.5453);
            }
#endif
#endif
#if defined(_EVO_ALGA_ARC_DRIVE)
            //   The arc's diagnostic (_EvoAlgaArcDrive, EVOSIM_THEATRE_ALGA_ARC_DRIVE), a view of what holds the arc down, never for a film.
            //   1: every acting blade bends by each axis' full limit (ROOM's lift where it is on); 2: it is drawn in grey by the flow's
            //   component across its thin axis; 3: the same by the drive (the share to its power, times the wave, times the hold). The
            //   colour is per vertex, written in LeafSway into this static, read by Vertex (forward pass) behind the same keyword, which
            //   puts it in depthColour.yzw, and returned early by Fragment. It is zero unless LeafSway's arc block ran at mode 2 or 3, and
            //   its floor is 0.004 and not zero so that the fragment can tell a blade the arc acts on from one it does not.
            static float3 arcDriveColour = float3(0.0, 0.0, 0.0);
#endif

#endif
            // The flow on this blade: its unit direction in the world's horizontal plane, and its share of the
            // full bend (0 to 1, from its speed). Only called with _SwayPart.w above a half.
            float AlgaBendFlow(out float3 direction)
            {
                float code = _SwayPart.w - 0.5;
                float speedStep = floor(code / AlgaBendAzimuthSteps);
                float azimuthStep = code - speedStep * AlgaBendAzimuthSteps;
                float angle = floor(azimuthStep) * (6.2831853 / AlgaBendAzimuthSteps);

                direction = float3(cos(angle), 0.0, sin(angle));
                return saturate(speedStep / AlgaBendSpeedSteps);
            }

            // The blade's sway: the shaped sheet moved along its thin axis by the bend, and its world
            // normal sheared by the bend's slope. The move depends on the station alone, so the blade
            // bends across its whole width at once and does not twist. ShapeLamina's frame, base,
            // curl and flesh are recomputed here rather than passed out of it, so its arithmetic is
            // untouched by the dial; the two must move together.
            void LeafSway(float4 leafOS, float3 halfExtents, float meshHalf, float3 flatOS,
                          inout float3 positionOS, inout float3 normalWS)
            {
                float3 axisL, axisT, axisW;
                LeafAxes(halfExtents, axisL, axisT, axisW);

                float hL = dot(halfExtents, abs(axisL));
                float hT = dot(halfExtents, abs(axisT));
                float hW = dot(halfExtents, abs(axisW));

                // ShapeLamina's shape, the marine producers' under their dial (BladeShapeOf).
                LeafShape l = BladeShapeOf(hL, hW, hT);

                float anchorAlong = dot(_Leaf.xyz, axisL);
                float baseSign = frac(_Carve.x * 13.37 + 0.271) < 0.5 ? -1.0 : 1.0;
                if (_Leaf.w > 0.5 && abs(anchorAlong) > 0.05) baseSign = anchorAlong > 0.0 ? 1.0 : -1.0;

                float curlObj = _CurlFraction * hW / max(1e-6, hT);
                float flesh = min(1.0, 0.35 * hW / max(1e-6, hT));

                // ShapeLamina's thin cap under the marine producers' dial, so the room is the drawn blade's.
                [branch] if (_Alga > 0.001) flesh = min(flesh, AlgaFleshCap(l, hW, hT, meshHalf));

                // The station the blade hangs from: its own anchor's, which is the base when the
                // anchor is at an end and the middle when it is on a face.
                float anchorStation = _Leaf.w > 0.5
                    ? saturate(0.5 - 0.5 * anchorAlong / (baseSign * meshHalf))
                    : SwayRootStation;
                float span = max(anchorStation, 1.0 - anchorStation);

                // What is wanted at the free end, in object units on the thin axis: an object unit
                // there is the blade's full thickness, as ShapeLamina's own conversion has it.
                float want = SwayLeafTip * max(hL, max(hT, hW)) / max(1e-6, 2.0 * hT);
                float amplitude = LeafSwayAmplitude(anchorStation, span, l, meshHalf, curlObj, flesh, want);

                // The hold's own slope is left out of the normal below: it changes over a joint's
                // reach, about half the part, and the move is a few per cent of the part, so what it
                // would tilt is a degree or two, near a joint only.
                float swing = sin(SwayAngle()) * SwayHold(flatOS) * amplitude;

                // The bend with the current (_EvoAlgaBend; see the section above), on a marine producer's blade
                // alone and only while the dial is on and the blade has a flow. Its stream joins the swing, so the
                // move and the normal below take it as they take the sway; the flutter and the lateral move are
                // added after them. All three stay at zero, and nothing above or below changes, otherwise.
                float bendSide = 0.0;
                float bendFlutter = 0.0;
                float bendSideMore = 0.0;
                float3 bendWidthWS = float3(0.0, 0.0, 0.0);

                [branch] if (_EvoAlgaBend.w > 0.5 && _SwayPart.w > 0.5 && l.alga > 0.5)
                {
                    float3 flowDirection;
                    float flowShare = AlgaBendFlow(flowDirection);

                    float3 thinDirWS = normalize(mul((float3x3)UNITY_MATRIX_M, axisT));
                    bendWidthWS = normalize(mul((float3x3)UNITY_MATRIX_M, axisW));

                    // The most the room allows the whole thin-axis move, the sway's own and the bend's.
                    float roomAmplitude = LeafSwayAmplitude(anchorStation, span, l, meshHalf, curlObj, flesh, 1.0e3);

                    float slender = smoothstep(AlgaBendSlenderLow, AlgaBendSlenderHigh, hL / max(1e-6, hT));
                    float strength = saturate(_EvoAlgaBend.z) * flowShare * SwayHold(flatOS);

                    float gustPhase = frac(_Carve.x * 5.713 + 0.371);
                    float gust = 1.0 - AlgaBendGust * (0.5 + 0.5 * sin(6.2831853 * (EvoTime() / AlgaBendSurgeSeconds + gustPhase)));

                    float stream = AlgaBendStream * roomAmplitude * slender * gust * strength * dot(flowDirection, thinDirWS);

                    swing = swing * AlgaBendSwayKeep + stream;
                    bendFlutter = AlgaBendFlutter * roomAmplitude * (1.0 - l.strap) * strength;
                    bendSide = AlgaBendLateral * slender * gust * strength * dot(flowDirection, bendWidthWS);

                    // The dial past one (up to four): the lean of AlgaBendReach of the blade's half length at its free end,
                    // in metres and not in the room, since a thin blade's room is nothing (see the section above). Only
                    // here, behind a test of the dial above one, so a dial of one or under draws what it always drew.
                    [branch] if (_EvoAlgaBend.z > 1.0)
                    {
                        float more = saturate((_EvoAlgaBend.z - 1.0) / AlgaBendMoreSpan);
                        float share = pow(flowShare, 1.0 / (1.0 + AlgaBendFlowRaise * more));
                        float slenderMore = smoothstep(AlgaBendReachSlenderLow, AlgaBendReachSlenderHigh, hL / max(1e-6, hT));
                        float lean = AlgaBendReach * hL * more * share * gust * SwayHold(flatOS) * slenderMore;

                        swing += lean * dot(flowDirection, thinDirWS) / max(1e-6, 2.0 * hT);
                        bendSideMore = lean * dot(flowDirection, bendWidthWS) / max(1e-6, 2.0 * hW);
                    }
                }

#if defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                // The arc of levels 1 and 2 (below) takes the room the sway gives up: the sway keeps AlgaBendSwayKeep of its swing.
                // With the lean on (_EvoAlgaBend.w) the lean's own block has already done this, with the same test.
                [branch] if (_EvoAlgaArc.x > 0.5 && _EvoAlgaBend.w < 0.5 && _SwayPart.w > 0.5 && l.alga > 1.5) swing *= AlgaBendSwayKeep;
#endif
                float x = (saturate(leafOS.x) - anchorStation) / span;

                positionOS += axisT * (swing * x * x);

                // The normal. The move is a shear, T += d(L), and a shear's normal is the old one
                // less d'(L) along L times its own part along T. The slope is taken in metres: the
                // move's per station, 2 x / span of the swing, over the length's per station.
                float riseMetres = swing * (2.0 * x / span) * 2.0 * hT;
                float runMetres = -4.0 * baseSign * meshHalf * hL;
                float slope = riseMetres / (abs(runMetres) > 1e-8 ? runMetres : 1e-8);

                float3 alongWS = normalize(mul((float3x3)UNITY_MATRIX_M, axisL));
                float3 thinWS = normalize(mul((float3x3)UNITY_MATRIX_M, axisT));

                normalWS = normalize(normalWS - alongWS * (slope * dot(normalWS, thinWS)));

                // The bend's flutter and lateral move (zero unless the bend above set them). The flutter is a
                // wave along the length on the thin axis, its slope left out of the normal as the hold's is. The
                // lateral move is by at most AlgaBendLateral of the width the outline leaves free at this station,
                // so the outline plus the move is inside the box; the normal is sheared by its slope as above.
                [branch] if (bendSide != 0.0 || bendFlutter != 0.0)
                {
                    float station = saturate(leafOS.x);
                    float widthRoom = meshHalf * (1.0 - saturate(LeafWidth(station, l)));

                    float flutterPhase = frac(_Carve.x * 3.917 + 0.613);
                    float flutterWave = sin(6.2831853 * (EvoTime() * AlgaBendFlutterHertz + flutterPhase - AlgaBendFlutterWaves * station));

                    positionOS += axisT * (bendFlutter * flutterWave * x * x);
                    positionOS += axisW * (bendSide * widthRoom * x * x);

                    float riseWidthMetres = bendSide * widthRoom * (2.0 * x / span) * 2.0 * hW;
                    float runWidthMetres = -4.0 * baseSign * meshHalf * hL;
                    float slopeWidth = riseWidthMetres / (abs(runWidthMetres) > 1e-8 ? runWidthMetres : 1e-8);

                    normalWS = normalize(normalWS - alongWS * (slopeWidth * dot(normalWS, bendWidthWS)));
                }

                // The dial past one: the lean's share along the width axis, in metres turned to width-axis object units
                // (one is the full width, 2 hW), outside the outline's room as the thin axis' is outside the box's. The
                // normal is sheared by its slope, as the lateral move's is.
                [branch] if (bendSideMore != 0.0)
                {
                    positionOS += axisW * (bendSideMore * x * x);

                    float riseMoreMetres = bendSideMore * (2.0 * x / span) * 2.0 * hW;
                    float runMoreMetres = -4.0 * baseSign * meshHalf * hL;
                    float slopeMore = riseMoreMetres / (abs(runMoreMetres) > 1e-8 ? runMoreMetres : 1e-8);

                    normalWS = normalize(normalWS - alongWS * (slopeMore * dot(normalWS, bendWidthWS)));
                }
#if defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                // The blade's arc bend, levels 1 and 2 (the section above AlgaBendFlow). After the sway, so the normal is the
                // sway's turned again by the arc's angle at this point; the position is moved by the closed form. Not on a blade
                // the lean spends room past the box on (its strength above one): the arc yields there.
                [branch] if (_EvoAlgaArc.x > 0.5 && _SwayPart.w > 0.5 && l.alga > 1.5 && !(_EvoAlgaBend.w > 0.5 && _EvoAlgaBend.z > 1.0))
                {
                    float3 arcFlow;
                    float arcShare = AlgaBendFlow(arcFlow);

                    float3 arcWidthAxis = normalize(mul((float3x3)UNITY_MATRIX_M, axisW));
                    float flowThin = dot(arcFlow, thinWS);
                    float flowWide = dot(arcFlow, arcWidthAxis);

                    // L: the arc from the anchor to the far end, metres. An object unit is the full size on each axis.
                    float arc = max(1e-6, span * 2.0 * hL);

                    // What the lean, if it is on, has taken of the room at its most: its strength is the dial up to one.
                    float leanStrength = _EvoAlgaBend.w > 0.5 ? saturate(_EvoAlgaBend.z) : 0.0;

                    // The room, metres, each axis leaves the arc at its x^2 amplitude: the thin axis' is what the sway and the lean
                    // gave up of LeafRoom's; the width axis' is its share of what the envelope's widest station leaves beside it.
                    float roomAmplitude = LeafSwayAmplitude(anchorStation, span, l, meshHalf, curlObj, flesh, 1.0e3);
                    float thinRoom = (1.0 - AlgaBendSwayKeep - AlgaBendStream * leanStrength) * roomAmplitude * 2.0 * hT;
                    float peak = lerp(AlgaBladePeakLeast, 1.0, AlgaBladePlant(1.0));
                    float wideShare = min(AlgaBendLateral, 1.0 - AlgaBendLateral * leanStrength);
                    float wideRoom = wideShare * meshHalf * (1.0 - peak) * 2.0 * hW;

                    // The tip angle each axis allows, radians: at most the cap times the flow's component, and at most what
                    // the room holds. Level 2's thin axis is at least the trial's angle times the flow's component.
                    float tipThin = min(AlgaArcTipCap * abs(flowThin), 3.0 * thinRoom / arc);
                    float tipWide = min(AlgaArcTipCap * abs(flowWide), 3.0 * wideRoom / arc);
                    [branch] if (_EvoAlgaArc.x > 1.5) tipThin = max(tipThin, AlgaArc2OutAngle * abs(flowThin));
                    // The room (see AlgaArcRoomCeiling): the angle its reach allows, which no flow component pulls under what the
                    // lines above gave, because it is at least 3 thinRoom / arc until the ceiling and the ceiling is over the cap.
                    [branch] if (_EvoAlgaArc.y > 0.0)
                    {
                        float roomAngle = min(3.0 * (thinRoom / arc + _EvoAlgaArc.y), AlgaArcRoomCeiling);
                        tipThin = min(max(AlgaArcTipCap, roomAngle) * abs(flowThin), roomAngle);
                        [branch] if (_EvoAlgaArc.x > 1.5) tipThin = max(tipThin, max(AlgaArc2OutAngle, roomAngle) * abs(flowThin));
                    }

                    float u = abs(x);
                    float wave = sin(6.2831853 * frac(EvoTime() * AlgaArcWaveHertz + _SwayPart.y - AlgaArcWaves * u));
                    float drive = saturate(pow(arcShare, AlgaArcCurrentPower) * (1.0 + AlgaArcWaveShare * wave) /
                                           (1.0 + AlgaArcWaveShare)) * SwayHold(flatOS);

                    float angleThin = sign(flowThin) * tipThin * drive;
                    float angleWide = sign(flowWide) * tipWide * drive;
#if defined(_EVO_ALGA_FLUTTER)
                    // The flutter (_EvoAlgaArc.z). The thin axis' full limit with the flow's component at one (the diagnostic's driveThin,
                    // ROOM's rule where ROOM is on), times the dial, the current's speed (the share to its power; not its direction) and
                    // the hold, times a wave from the anchor to the free end. Its phase has the arc's wave's per-blade offset. The sum is
                    // cut to the limit, which a blade the arc bent as far as it may keeps. The displacement below is per vertex, so a sign
                    // that changes along the blade moves each station by its own angle; the wave is smooth, so the sheet has no seam.
                    [branch] if (_EvoAlgaArc.z > 0.0)
                    {
                        float arcFlutterLimit = min(AlgaArcTipCap, 3.0 * thinRoom / arc);
                        [branch] if (_EvoAlgaArc.x > 1.5) arcFlutterLimit = max(arcFlutterLimit, AlgaArc2OutAngle);
                        [branch] if (_EvoAlgaArc.y > 0.0)
                        {
                            float arcFlutterRoom = min(3.0 * (thinRoom / arc + _EvoAlgaArc.y), AlgaArcRoomCeiling);
                            arcFlutterLimit = min(max(AlgaArcTipCap, arcFlutterRoom), arcFlutterRoom);
                            [branch] if (_EvoAlgaArc.x > 1.5) arcFlutterLimit = max(arcFlutterLimit, max(AlgaArc2OutAngle, arcFlutterRoom));
                        }
                        arcFlutterLimit = max(arcFlutterLimit, 0.0);
                        float arcFlutterWave = sin(6.2831853 * frac(EvoTime() * AlgaFlutterHertz + _SwayPart.y - AlgaFlutterWaves * u));
                        float arcFlutter = _EvoAlgaArc.z * arcFlutterLimit * pow(arcShare, AlgaArcCurrentPower) * arcFlutterWave * SwayHold(flatOS);
#if defined(_EVO_ALGA_FLUTTER_NOISE)
                        // The noise (_EvoAlgaArc.w). Each blade its own ripple: a frequency and an amplitude factor from two hashes of the
                        // part's number. A wave whose frequency differs by blade keeps a continuous phase over time, because the frequency
                        // is constant per blade (the phase is time times it plus an offset, and nothing changes it). Then the slow gust:
                        // three sines of the clock at low incommensurate frequencies, a blade's offset in time from a third hash, averaged
                        // and mapped to [floor, ceiling], times the ripple by v. A fourth hash adds a phase of its own, times v, to the wave, since
                        // _SwayPart.y is shared by parts at one depth of one body. The two flutter lines above are drawn again with these; the cut
                        // to the limit below is the existing one.
                        [branch] if (_EvoAlgaArc.w > 0.0)
                        {
                            float arcFlutterNoise = _EvoAlgaArc.w;
                            float arcFlutterNoiseHz = lerp(AlgaFlutterHertz, lerp(AlgaFlutterNoiseHertzLow, AlgaFlutterNoiseHertzHigh, AlgaFlutterHash(_Individual, 1.0)), arcFlutterNoise);
                            float arcFlutterNoiseAmp = lerp(1.0, lerp(AlgaFlutterNoiseAmpLow, AlgaFlutterNoiseAmpHigh, AlgaFlutterHash(_Individual, 2.0)), arcFlutterNoise);
                            float arcFlutterGustClock = EvoTime() + AlgaFlutterHash(_Individual, 3.0) * AlgaFlutterGustSpread;
                            float arcFlutterGustSum = sin(6.2831853 * frac(arcFlutterGustClock * AlgaFlutterGustHertzA)) +
                                                      sin(6.2831853 * frac(arcFlutterGustClock * AlgaFlutterGustHertzB)) +
                                                      sin(6.2831853 * frac(arcFlutterGustClock * AlgaFlutterGustHertzC));
                            float arcFlutterGust = lerp(AlgaFlutterGustFloor, AlgaFlutterGustCeiling, 0.5 + 0.16666667 * arcFlutterGustSum);
                            arcFlutterWave = sin(6.2831853 * frac(EvoTime() * arcFlutterNoiseHz + _SwayPart.y + arcFlutterNoise * AlgaFlutterHash(_Individual, 4.0) - AlgaFlutterWaves * u));
                            arcFlutter = _EvoAlgaArc.z * arcFlutterLimit * pow(arcShare, AlgaArcCurrentPower) * arcFlutterWave * SwayHold(flatOS) *
                                         arcFlutterNoiseAmp * lerp(1.0, arcFlutterGust, arcFlutterNoise);
                        }
#endif
                        angleThin = clamp(angleThin + arcFlutter, -arcFlutterLimit, arcFlutterLimit);
                    }
#endif
#if defined(_EVO_ALGA_ARC_DRIVE)
                    // The diagnostic (_EvoAlgaArcDrive; see arcDriveColour). Mode 1, the most bend: angleThin and angleWide, and no other
                    // variable, are overridden here, after tipThin, tipWide and drive are set. In each, abs(flowThin) or abs(flowWide) and the
                    // drive (the share, the wave and the hold) are taken as one, so the angle is the axis' full limit with its sign kept (a
                    // component of exactly zero takes the positive sign). The limits are tipThin's and tipWide's lines above with abs(flow) at
                    // one, and ROOM's block with it at one too, repeated here because tipThin and tipWide already hold the flow's component.
                    [branch] if (_EvoAlgaArcDrive > 0.5 && _EvoAlgaArcDrive < 1.5)
                    {
                        float driveThin = min(AlgaArcTipCap, 3.0 * thinRoom / arc);
                        float driveWide = min(AlgaArcTipCap, 3.0 * wideRoom / arc);
                        [branch] if (_EvoAlgaArc.x > 1.5) driveThin = max(driveThin, AlgaArc2OutAngle);
                        [branch] if (_EvoAlgaArc.y > 0.0)
                        {
                            float driveRoom = min(3.0 * (thinRoom / arc + _EvoAlgaArc.y), AlgaArcRoomCeiling);
                            driveThin = min(max(AlgaArcTipCap, driveRoom), driveRoom);
                            [branch] if (_EvoAlgaArc.x > 1.5) driveThin = max(driveThin, max(AlgaArc2OutAngle, driveRoom));
                        }
                        angleThin = (flowThin < 0.0 ? -1.0 : 1.0) * driveThin;
                        angleWide = (flowWide < 0.0 ? -1.0 : 1.0) * driveWide;
                    }
                    // Modes 2 and 3: the false colour, a grey ramp from 0.004 (black) at 0 to 1 (white) at 1. 2 shows abs(flowThin), the push
                    // the thin axis gets from the flow (both are unit vectors, so it is in [0, 1]); 3 shows drive, before the flow's component.
                    [branch] if (_EvoAlgaArcDrive > 1.5)
                    {
                        float driveShown = saturate(_EvoAlgaArcDrive > 2.5 ? drive : abs(flowThin));
                        arcDriveColour = float3(0.004 + 0.996 * driveShown, 0.004 + 0.996 * driveShown, 0.004 + 0.996 * driveShown);
                    }
#endif
                    float angleTip = sqrt(angleThin * angleThin + angleWide * angleWide);

                    [branch] if (angleTip > 1.0e-5)
                    {
                        float u2 = u * u;
                        float sideways = arc * u2 * u / 3.0;

                        positionOS += axisT * (angleThin * sideways / (2.0 * hT));
                        positionOS += axisW * (angleWide * sideways / (2.0 * hW));

                        // Toward the anchor: span stations of the length per unit of the arc, and a station runs along
                        // -baseSign of axisL, so the move toward the anchor is +baseSign sign(x) of it.
                        positionOS += axisL * (2.0 * baseSign * meshHalf * sign(x) * span * angleTip * angleTip * u2 * u2 * u / 10.0);

                        // The normal turned by the tangent's angle, in the plane of the way away from the anchor and the arc.
                        float turn = angleTip * u2;
                        float sinTurn = turn * (1.0 - turn * turn / 6.0);
                        float cosTurn = 1.0 - 0.5 * turn * turn;
                        float3 away = alongWS * (-baseSign * sign(x));
                        float3 arcDirection = (thinWS * angleThin + arcWidthAxis * angleWide) / angleTip;
                        float nAway = dot(normalWS, away);
                        float nArc = dot(normalWS, arcDirection);

                        normalWS = normalize(normalWS +
                                             away * (nAway * (cosTurn - 1.0) - nArc * sinTurn) +
                                             arcDirection * (nArc * (cosTurn - 1.0) + nAway * sinTurn));
                    }
                }
#endif
            }

            // The float's pulse: the drawn body squeezed toward its own centre plane across its
            // smallest axis, by SwayFloatDraw of that half extent at the surface at the height of the
            // pulse and by nothing at rest. Only ever in, so the rest shape is the shape without the
            // dial. The normal is the squeeze's inverse transpose on the old one, exact for a squeeze.
            void FloatPulse(float3 holdOS, inout float3 positionWS, inout float3 normalWS)
            {
                float3 he = HalfExtentsWS();

                // The smallest axis, ties in a fixed order so a part with two equal sides picks the
                // same one every frame (AtMost's tolerance is what makes the order hold).
                float3 axis;

                if (AtMost(he.x, he.y) && AtMost(he.x, he.z))
                    axis = float3(1, 0, 0);
                else if (AtMost(he.y, he.z))
                    axis = float3(0, 1, 0);
                else
                    axis = float3(0, 0, 1);

                float3 across = normalize(mul((float3x3)UNITY_MATRIX_M, axis));
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));

                // Raised cosine: nothing at rest, one at the height, smooth at both.
                float draw = 0.5 - 0.5 * cos(SwayAngle());
                float squeeze = SwayFloatDraw * draw * SwayHold(holdOS);

                positionWS -= across * (squeeze * dot(positionWS - centre, across));

                normalWS = normalize(normalWS + across * (dot(normalWS, across) * (1.0 / (1.0 - squeeze) - 1.0)));
            }

            // ---------------------------------------------------------------- the link's taper
            //
            // A capsule part (a link) narrowing from its base to its free end (_LinkAxis, _LinkShape;
            // EVOSIM_THEATRE_LINK), as a worm's body or an arm does. A capsule is drawn as a shaft and two caps,
            // three meshes that share one axis through their object origins, so one function of the position along
            // that axis, read at each vertex of each, gives all three the same cross-section where they meet. The
            // palette's coordinate t runs from zero at the base pole to one at the free pole, with the base cap
            // ending at t = _LinkAxis.z and the tip cap beginning at t = _LinkAxis.w.
            //
            // The cross-section is scaled toward the axis by s, one at the base cap and across it, falling along
            // the shaft by a smoothstep to the tip's share of the base radius (_LinkShape.y, LinkTipShare in the
            // palette) at the tip cap's join, and held there across the tip cap. The smoothstep has no slope at
            // either end, so neither seam is a crease: the shaft meets the base cap on a vertical tangent and the
            // tip cap on a vertical tangent, and the tip cap is the same hemisphere drawn at the tip's radius along
            // the axis and not shrunk along it, so the free pole stays where the collider's pole is (a child hung
            // there still meets it, and the bridges' reach along the axis is unchanged). The base cap and the half
            // of the tip cap inside the shaft take the shaft's s, so they stay inside it.
            //
            // The honesty margin is 0: s is in [tip share, 1] and only the two cross coordinates are scaled, so
            // every point moves toward the axis and stays inside the cylinder and the caps it was inside.
            //
            // The normal is the inverse transpose of the map's Jacobian on the mesh's, as ShapeBox does. The map is
            // (x, y, z) to (s x, y, s z) with s a function of y alone, so the Jacobian has s on the two cross
            // diagonals and (x, z) times ds/dy in the y column; its inverse transpose divides the cross components
            // by s and turns the axial one toward the narrowing end by ds/dy times the cross position.
            void LinkTaper(inout float3 positionOS, inout float3 normalOS)
            {
                float a = _LinkAxis.x;
                float t = a * positionOS.y + _LinkAxis.y;
                float span = max(_LinkAxis.w - _LinkAxis.z, 1e-4);
                float u = saturate((t - _LinkAxis.z) / span);

                float tipShare = _LinkShape.y;
                float s = 1.0 - (1.0 - tipShare) * (u * u * (3.0 - 2.0 * u));
                float dsdy = -(1.0 - tipShare) * 6.0 * u * (1.0 - u) * a / span;

                float3 n = normalOS;
                float axial = n.y - dsdy * (positionOS.x * n.x + positionOS.z * n.z) / s;

                normalOS = normalize(float3(n.x / s, axial, n.z / s));
                positionOS = float3(positionOS.x * s, positionOS.y, positionOS.z * s);
            }

#if defined(_EVO_NOSE_PIT)
            // ---------------------------------------------------------------- the nose pit
            //
            // A nose part (cell type nose, D242) carries an olfactory pit in the face at its sensing tip, the
            // MouthPoint, which is where the world reads the smell: a cup with a flat floor, a steep wall and a
            // slight lip, like a nostril on a snout. All of it is compiled under _EVO_NOSE_PIT, a keyword that
            // only TheatreNosePit turns on, and reached only by a part whose _Pit.w is over zero, which only that
            // class sets, on a nose part. Without the dial a frame is the one without it.
            //
            // The pit is scale free, in the part's own object units, as the joint pinches are. _Pit.xyz is the tip,
            // _PitRadii.xyz the pit's radius along each object axis (its radius in metres over the visual's size on
            // that axis), and _Pit.w the depth in radii. The distance from the tip in radii, u, is then a number
            // that a growth step and a corpse's shrink leave alone, and the depth in metres is _Pit.w times the
            // radius in metres now (PitRadiusMetres), which the matrix gives.
            //
            // The honesty margin is 0. The vertex stage only adds to the inward move (never negative), takes away
            // at most PitCarveCalm of the carve's own inward move, which is itself never positive outward, and the
            // box clamp still has the last word. The lip and the wall's tilt are shading and move nothing.

            // The cup's floor, as a share of the radius: the cup is at full depth inside it, and rises to the rim
            // across the rest by a smoothstep, so the wall is steepest half way up and flat at both ends.
            static const float PitFloor = 0.35;

            // The carve is calmed round the pit so the cup reads as a clean opening: it keeps 1 - PitCarveCalm of
            // its inward move inside PitCleanLow radii, none of the calming outside PitCleanHigh, a smoothstep
            // between. The fragment turns its normal back toward the geometric one by the same share.
            static const float PitCarveCalm = 0.85;
            static const float PitCleanLow = 0.6;
            static const float PitCleanHigh = 1.35;

            // The lip: a ring of normal tilt, a gaussian of this width (in radii) centred this far out, as high as
            // this share of the depth. Shading alone; no vertex rises.
            static const float PitLipAt = 1.08;
            static const float PitLipWidth = 0.16;
            static const float PitLipHeight = 0.08;

            // The lining: the albedo loses this share at the floor of a pit that is at least PitDeepRef radii
            // deep, and the light (rim and glow too) loses PitLightDark of itself; both fall with the cup's depth.
            static const float PitAlbedoDark = 0.6;
            static const float PitLightDark = 0.4;
            static const float PitDeepRef = 0.8;

            // The tilt fades out as the pit's radius falls from PitFadeHigh pixels to PitFadeLow, so a far nose does
            // not shimmer; its darkness stays, as a nostril far off is still a dark dot.
            static const float PitFadeLow = 1.5;
            static const float PitFadeHigh = 4.0;

            // The slope's bound, rise over run, as the papillae's is, so a steep wall cannot turn a normal away
            // from the eye.
            static const float PitSlopeBound = 1.4;

            // The pit's radius in metres now: the visual's size on an axis (the matrix column) over the object
            // units a radius takes on it. The three agree by construction; the mean keeps a rounding off one axis.
            float PitRadiusMetres()
            {
                return dot(2.0 * HalfExtentsWS(), _PitRadii.xyz) * (1.0 / 3.0);
            }

            // How far a point of the visual is from the tip, in radii, along the part's own axes.
            float PitU(float3 positionOS)
            {
                return length((positionOS - _Pit.xyz) / max(_PitRadii.xyz, float3(1e-6, 1e-6, 1e-6)));
            }

            // The cup, one on the floor and zero at the rim, and its slope in radii.
            float PitCup(float u, out float slope)
            {
                float t = saturate((u - PitFloor) / (1.0 - PitFloor));
                slope = -6.0 * t * (1.0 - t) / (1.0 - PitFloor);
                return 1.0 - t * t * (3.0 - 2.0 * t);
            }

            // One where the carve is calmed, zero outside.
            float PitClean(float u)
            {
                return 1.0 - smoothstep(PitCleanLow, PitCleanHigh, u);
            }

            // What the pit does to a vertex: the metres it takes off along the inward normal, and the share of the
            // carve's own inward move that is kept.
            void PitCut(float3 positionOS, out float metres, out float keep)
            {
                float u = PitU(positionOS);
                float slope;

                metres = _Pit.w * PitRadiusMetres() * PitCup(u, slope);
                keep = 1.0 - PitCarveCalm * PitClean(u);
            }

#endif
#if defined(_EVO_MOUTH2)
            // ---------------------------------------------------------------- the mouth's opening
            //
            // An eating part (MouthPoint.IsEatingPart) wears an opening with a lip where the world reads its food,
            // MouthPoint.Local: the nose pit's method, applied to the mouth, on a part and on the one skin. A cup is cut
            // inward along the normal here; the fragment stage darkens it into a throat and shades a ring round it as a
            // lip. All of it is compiled under _EVO_MOUTH2, a keyword that only TheatreMouth2 turns on, and reached only
            // by a part whose _Mouth.w is over zero, which only that class sets, on an eating part, or by the one skin
            // with its mouths. Without the dial a frame is the one without it.
            //
            // On a part it is scale free, in the part's own object units, as the nose pit is: _Mouth.xyz is the point,
            // _MouthRadii.xyz the radius along each object axis, _Mouth.w the depth in radii. The distance from the
            // point in radii, u, is then a number that a growth step and a corpse's shrink leave alone.
            //
            // On the one skin the object space is the body's, so a mouth is _MouthSkinN: xyz the point in rest metres, w
            // its radius there, compared with the vertex's rest position (UV5). Its depth is _Mouth.w radii, in the
            // world's metres (_SkinScale). A vertex carries in mouthSkin (UV2) the skinning weight of the part each of
            // the four mouths is on, and the cut is that weight times the cup: the opening is the part's tissue and
            // moves with the part, and a neighbouring part's surface inside the radius takes none of it.
            //
            // The honesty margin is 0. The vertex stage only adds to the inward move, takes away at most MouthCarveCalm
            // of the carve's own inward move (itself never outward), and on the skin cuts no deeper than
            // MouthSkinThickShare of the vertex's half thickness; the box clamp still has the last word on a part. The
            // lip and the wall's tilt are shading and move nothing.

            // The cup's floor, as a share of the radius: full depth inside it, a smoothstep wall to the rim.
            static const float MouthFloor = 0.35;

            // The carve is calmed round the opening so it reads as a clean cut: it keeps 1 - MouthCarveCalm of its inward
            // move inside MouthCleanLow radii, none of the calming outside MouthCleanHigh, a smoothstep between. The
            // fragment turns its normal back toward the geometric one by the same share.
            static const float MouthCarveCalm = 0.85;
            static const float MouthCleanLow = 0.7;
            static const float MouthCleanHigh = 1.3;

            // On the skin the cup goes no deeper than this share of the vertex's own half thickness.
            static const float MouthSkinThickShare = 0.5;

            // The radius in metres now, on a part: the visual's size on an axis (the matrix column) over the object units
            // a radius takes on it. The three agree by construction; the mean keeps a rounding off one axis.
            float MouthPartRadiusMetres()
            {
                return dot(2.0 * HalfExtentsWS(), _MouthRadii.xyz) * (1.0 / 3.0);
            }

            // EVOSIM_THEATRE_MOUTH2_FLAT (_MouthFlat, 0 by default): the mouth's distance taken flat along its axis, the offset less its
            // part along the axis, so on a sharp rim, where the surface runs away along the axis on both sides, the opening, the lip and
            // the disc are round seen along the axis rather than squashed across the rim. A point more than MouthFlatSlack radii behind
            // the mouth's plane has the rest added back MouthFlatSteep times over, so the far side of the part, which also lies near the
            // axis, takes no mouth (_MouthFlatSlack, EVOSIM_THEATRE_MOUTH2_FLAT_SLACK, replaces MouthFlatSlack where it is over zero; on
            // a part sharp as a wedge at the rim a round mouth needs more than the default). _MouthFlat is the share of the way from today's distance to the flat one; at zero every distance is
            // today's expression, untouched. It needs the axis (_MouthAxis, _MouthAxisSkinN), which only the oral disc's dial writes.
            static const float MouthFlatSlack = 2.5;
            static const float MouthFlatSteep = 2.0;

            // The distance u (in radii) of an offset d (in radii) from a mouth with the given axis, flattened by _MouthFlat.
            float MouthFlatten(float u, float3 d, float4 axisPhase)
            {
                float axisLength = dot(axisPhase.xyz, axisPhase.xyz);

                [branch] if (_MouthFlat > 0.0 && axisLength > 0.5)
                {
                    float3 axis = axisPhase.xyz * rsqrt(axisLength);
                    float along = dot(d, axis);
                    float3 across = d - axis * along;
                    float slack = _MouthFlatSlack > 0.0 ? _MouthFlatSlack : MouthFlatSlack;
                    float behind = max(0.0, -along - slack) * MouthFlatSteep;

                    u = lerp(u, sqrt(dot(across, across) + behind * behind), saturate(_MouthFlat));
                }

                return u;
            }

            // How far a point of the visual is from the mouth, in radii, along the part's own axes.
            float MouthPartU(float3 positionOS)
            {
                float u = length((positionOS - _Mouth.xyz) / max(_MouthRadii.xyz, float3(1e-6, 1e-6, 1e-6)));

                [branch] if (_MouthFlat > 0.0)
                {
                    u = MouthFlatten(u, (positionOS - _Mouth.xyz) / max(_MouthRadii.xyz, float3(1e-6, 1e-6, 1e-6)), _MouthAxis);
                }

                return u;
            }

            // The cup, one on the floor and zero at the rim, and its slope in radii.
            float MouthCup(float u, out float slope)
            {
                float t = saturate((u - MouthFloor) / (1.0 - MouthFloor));
                slope = -6.0 * t * (1.0 - t) / (1.0 - MouthFloor);
                return 1.0 - t * t * (3.0 - 2.0 * t);
            }

            // One where the carve is calmed, zero outside.
            float MouthClean(float u)
            {
                return 1.0 - smoothstep(MouthCleanLow, MouthCleanHigh, u);
            }

            // The skin's i-th mouth (xyz the centre in rest metres, w the radius), and a vertex's weight toward its part.
            float4 MouthSkinAt(int i)
            {
                if (i == 0) return _MouthSkin0;
                if (i == 1) return _MouthSkin1;
                if (i == 2) return _MouthSkin2;
                return _MouthSkin3;
            }

            // The skin's i-th mouth's axis (xyz, in the rest frame) and the stripes' phase (w).
            float4 MouthSkinAxisAt(int i)
            {
                if (i == 0) return _MouthAxisSkin0;
                if (i == 1) return _MouthAxisSkin1;
                if (i == 2) return _MouthAxisSkin2;
                return _MouthAxisSkin3;
            }

            // The distance in radii of a rest position from the skin's i-th mouth: today's, or with _MouthFlat over zero flattened along
            // its axis (MouthFlatten).
            float MouthSkinU(float3 restAt, int i)
            {
                float4 mouth = MouthSkinAt(i);
                float u = length(restAt - mouth.xyz) / mouth.w;

                [branch] if (_MouthFlat > 0.0)
                {
                    u = MouthFlatten(u, (restAt - mouth.xyz) / mouth.w, MouthSkinAxisAt(i));
                }

                return u;
            }

            float MouthShareAt(float4 weights, int i)
            {
                if (i == 0) return weights.x;
                if (i == 1) return weights.y;
                if (i == 2) return weights.z;
                return weights.w;
            }

            // What the opening does to a vertex of a part: the metres it takes off along the inward normal, and the share
            // of the carve's own inward move that is kept.
            void MouthPartCut(float3 positionOS, out float metres, out float keep)
            {
                float u = MouthPartU(positionOS);
                float slope;

                metres = _Mouth.w * MouthPartRadiusMetres() * MouthCup(u, slope);
                keep = 1.0 - MouthCarveCalm * MouthClean(u);
            }

            // The same on the one skin, from the vertex's rest position and its weights toward the four mouths' parts. Where
            // two mouths reach a vertex the deeper cut stands and the carve keeps the least.
            void MouthSkinCut(float3 restPosition, float4 weights, float halfThickMetres, out float metres, out float keep)
            {
                metres = 0.0;
                keep = 1.0;

                float scale = _SkinScale > 0.0 ? _SkinScale : 1.0;

                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 mouth = MouthSkinAt(i);
                    float share = MouthShareAt(weights, i);

                    if (mouth.w > 0.0 && share > 0.0)
                    {
                        float u = MouthSkinU(restPosition, i);
                        float slope;

                        metres = max(metres, share * _Mouth.w * mouth.w * scale * MouthCup(u, slope));
                        keep = min(keep, 1.0 - MouthCarveCalm * MouthClean(u) * share);
                    }
                }

                metres = min(metres, MouthSkinThickShare * halfThickMetres);
            }

#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)
            // ---------------------------------------------------------------- the nose pit on the one skin
            //
            // EVOSIM_THEATRE_NOSE_PIT=2: the nose is merged into the skin at its true size, and its pit is carved in the skin
            // as the mouth's opening is, with the nose pit's own cup (PitCup, PitClean). All of it is compiled under
            // _EVO_NOSE_SKIN (and the pit's keyword), which only a skin built with a nose turns on, and reached only by a skin
            // whose _PitSkin.w is over zero. Without the dial a frame is the one without it.
            //
            // The pit is _PitSkin: xyz the sensing tip in the body's rest metres, w its radius there; _PitSkinDepth is its depth
            // in rest metres, and both are scaled to the world by _SkinScale. A vertex carries in pitSkin (UV1) its skinning
            // weight toward the nose's part, and the cut is that weight times the cup: the pit moves with the nose, and a
            // neighbouring part's surface inside the radius takes none of it. It is the pit of a nose part, in the skin's frame.
            //
            // The honesty margin is 0. The vertex stage only adds to the inward move, takes away at most PitCarveCalm of the
            // carve's own inward move (itself never outward), and cuts no deeper than NoseSkinThickShare of the vertex's own
            // half thickness. The lip and the wall's tilt are shading and move nothing.

            // The cup goes no deeper than this share of the vertex's own half thickness.
            static const float NoseSkinThickShare = 0.5;

            // Past this many radii the fragment does nothing: beyond the lip and its tail.
            static const float NoseSkinReach = 1.6;

            // The nose's lamellae: fine rings of the distance from the pit's centre, a pitch of NoseLamellaShare of the body
            // pattern's cell (the tile over PatternCellsPerTile; NoseLamellaBareCell radii when the body has no pattern), a
            // darkening of NoseLamellaDark at a ridge, none inside NoseLamellaFrom radii and whole by NoseLamellaSoft more,
            // gone as a ridge falls under a pixel (NoseLamellaFadeRate times the screen derivative).
            static const float NoseLamellaShare = 0.25;
            static const float NoseLamellaBareCell = 0.5;
            static const float NoseLamellaDark = 0.16;
            static const float NoseLamellaFrom = 1.35;
            static const float NoseLamellaSoft = 0.45;
            static const float NoseLamellaFadeRate = 1.5;

            // The nose's tissue lets this much more of the light through, by its share of the vertex's weight.
            static const float NoseTransGain = 0.1;

            // What the pit does to a vertex of the skin: the metres it takes off along the inward normal, and the share of the
            // carve's own inward move that is kept.
            void NoseSkinCut(float3 restPosition, float share, float halfThickMetres, out float metres, out float keep)
            {
                float scale = _SkinScale > 0.0 ? _SkinScale : 1.0;
                float u = length(restPosition - _PitSkin.xyz) / max(_PitSkin.w, 1e-6);
                float slope;

                metres = min(share * _PitSkinDepth * scale * PitCup(u, slope), NoseSkinThickShare * halfThickMetres);
                keep = 1.0 - PitCarveCalm * PitClean(u) * share;
            }
#endif
            // ---------------------------------------------------------------- the shape

            // Where one vertex of a body is drawn, and what the forward pass needs to know about
            // it afterwards. Every pass takes its position from here and from nowhere else, so
            // the depth the depth of field reads is the depth the colour was drawn at.
            struct BodyVertex
            {
                float3 positionWS;
                float3 normalWS;
                // The undisplaced object position the carve field was read at, and the depth in
                // metres it was read with, for the fragment's rebuilt normal.
                float3 carveOS;
                float carveDepth;
                // On a leaf: s, the distance across the half width, t, and the flag; zero elsewhere.
                float4 leaf;
                float halfThickMetres;
                float halfWidthMetres;
                // One on a box part under the soft tissue (_OrganicTissue), zero elsewhere: the fragment's
                // rebuilt normal, its pores and its sheen ask the same question the vertex did.
                float tissue;
#if defined(_ONE_SKIN)
                // A one skin at level 3: the wrinkles' share, the carve depth in metres, and the tissue and soft-leaf
                // weights; zero elsewhere, and on the skin with the dial below 3.
                float4 skinCarve;
#endif
            };

            BodyVertex ShapeBody(Attributes input)
            {
                BodyVertex body = (BodyVertex)0;

                float3 halfExtents = HalfExtentsWS();
                float smallest = min(halfExtents.x, min(halfExtents.y, halfExtents.z));

                // Which solid this is, from the mesh itself, and how big the mesh is in its own
                // units. Anything that is not the box, the engine's own primitives included, reads
                // zero here and is left to the carve alone (TheatreMeshes.Finish).
                // A one skin (TheatreOneSkin, EVOSIM_THEATRE_ONE_SKIN) carries three here and, in y, the
                // vertex's own half thickness in rest metres: neither the box nor the blade, with no carve, no
                // link taper and no float pulse, since its object space is the body's and not a part's. The
                // meshes that carry zero, one or two read as they always did.
                bool skin = input.shapeOS.x > 2.5;
                bool lamina = input.shapeOS.x > 1.5 && !skin;
                bool box = input.shapeOS.x > 0.5 && !lamina && !skin;
                float meshHalf = max(1e-4, input.shapeOS.y);
#if defined(_EVO_THALLI)

                // A thallus (EVOSIM_THEATRE_THALLI; ThalliIs): neither the box, the blade nor the skin.
                bool thallus = ThalliIs(input.shapeOS.x);
#endif

                float3 shapedOS = input.positionOS.xyz;
                float3 shapedNormalOS = input.normalOS;

                if (box) ShapeBox(shapedOS, shapedNormalOS, halfExtents, smallest, meshHalf);

                // A link narrowing toward its free end (_LinkShape.w, EVOSIM_THEATRE_LINK; LinkTaper). A capsule's
                // shaft and caps are neither the box nor the blade, and the palette sets it on those alone. The
                // carve below still reads the undeformed position, so its impressions stay on the tissue.
                [branch] if (_LinkShape.w > 0.5)
                {
                    if (!box && !lamina && !skin) LinkTaper(shapedOS, shapedNormalOS);
                }

                float3 positionWS = TransformObjectToWorld(shapedOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(shapedNormalOS));

                // The leaf: shaped here from where the vertex is on it, and carved at its flat
                // position so an impression stays on the tissue whatever the curl does.
                float3 carveOS = input.positionOS.xyz;

                // A skin's half thickness is its vertex's, in rest metres, times the metres in the world of one
                // rest metre: the growth its bones carry and the root's scale, as a part's half extents carry
                // both through its matrix (_SkinScale; unset, one).
                float halfThickMetres = skin ? meshHalf * (_SkinScale > 0.0 ? _SkinScale : 1.0) : smallest;
                float halfWidthMetres = 0.0;
                float4 leaf = float4(0, 0, 0, 0);

                if (lamina)
                {
                    ShapeLamina(input.leafOS, halfExtents, meshHalf,
                        shapedOS, carveOS, normalWS, halfThickMetres, halfWidthMetres, leaf);
                    positionWS = TransformObjectToWorld(shapedOS);
                }

                // The blade's sway, on the shaped sheet and before the carve, so the impressions move
                // with the blade. The carve still reads the flat position, so they stay on the tissue.
                [branch] if (_Sway > 0.5)
                {
                    if (lamina && abs(_SwayPart.x - SwayKindLeaf) < 0.5)
                    {
                        LeafSway(input.leafOS, halfExtents, meshHalf, carveOS, shapedOS, normalWS);
                        positionWS = TransformObjectToWorld(shapedOS);
                    }
                }
#if defined(_EVO_THALLI)

                // The thallus's sway (ThalliSway), on the part a producer's beat is given to, then the clamp into the mesh's box,
                // so inside-ness is a property of the final position. The carve below is zero on a thallus.
                [branch] if (_Sway > 0.5)
                {
                    if (thallus && abs(_SwayPart.x - SwayKindLeaf) < 0.5)
                    {
                        ThalliSway(input.leafOS, carveOS, shapedOS);
                        shapedOS = clamp(shapedOS, -meshHalf, meshHalf);
                        positionWS = TransformObjectToWorld(shapedOS);
                    }
                }
#endif

                // The carve, and the whole of the second day in four lines.
                //
                // The field is read in the part's own object units, so an impression is a place
                // on the body rather than a place in the water: it travels with the part, turns
                // with it, and does not swim across it as it moves. Its wavelength is set in the
                // same units, so the lobes come out about the size of the part whatever the
                // part's size is (TheatreWater.hlsl, EvoCarve).
                //
                // The displacement is in world metres along the world normal, not in object
                // units, because the visuals carry a non uniform scale and an object space push
                // of a fixed length would come out longer on the long axis.
                //
                // The sign is the bound. carve is in [0, 1] and depth is positive, so this term
                // can only move a vertex inward, and the mesh it moves is already inset
                // (TheatreMeshes.Inset). Nothing here can put a vertex outside the collider, at
                // any dial setting, on any body.
                //
                // It is read at the undeformed object position, not at the shaped one, so an
                // impression stays on the same piece of tissue when the part is tapered and bent:
                // the field is a property of the body, and the fragment stage asks it the same
                // question at the same place to rebuild the normal.
                // The soft tissue (_OrganicTissue) is for a box part, the same parts ShapeBox shapes, and
                // not a leaf, a sphere or a capsule.
                bool tissueBox = box && _OrganicTissue > 0.5;

                // A skin is not carved (its depth zero): the smooth union is its form.
                float depth = skin ? 0.0 : CarveDepth(carveOS, smallest, tissueBox ? 1.0 : 0.0);

                // A leaf thins to nothing at its rim, so the carve there is bounded by the
                // thickness the lens left, or it would cut through to the other face.
                if (lamina) depth = min(depth, 0.5 * halfThickMetres);

                // The leaf dial (_LeafLook): a blade's carve is the smooth, shallow LeafCarve, cut to
                // LeafCarveShare of the depth left above. Less depth, and still inward. An absorber under the
                // velvet dial (_Velvet, which the palette sets only on an absorber) takes the same carve: a sponge
                // has no facets, and the band's terraces made a velvet absorber read as faceted rock (b40).
                bool leafSoft = (lamina && _LeafLook > 0.5) || _Velvet > 0.5;
                [branch] if (leafSoft) depth *= LeafCarveShare;
#if defined(_EVO_THALLI)

                // A thallus is not carved: its form is in the mesh, and a sheet a few hundredths of its box thick would be cut through.
                if (thallus) depth = 0.0;
#endif

                // Under the soft dial the field is SoftCarve's, in the same [0, 1], so the sign
                // argument below is untouched.
                // Under the soft tissue the field is TissueCarve's, also in [0, 1].
                float3 unusedGradient = float3(0.0, 0.0, 0.0);
                float carve = 0.0;

                [branch] if (!skin)
                {
                    [branch] if (tissueBox)
                    {
                        carve = TissueCarve(carveOS + OwnShift(), _Carve.x, _Carve.y, _Carve.z, halfExtents, unusedGradient);
                    }
                    else if (leafSoft)
                    {
                        carve = LeafCarve(carveOS + OwnShift(), _Carve.x, unusedGradient);
                    }
                    else
                    {
                        carve = BodyCarve(carveOS + OwnShift(), smallest, unusedGradient);
                    }
                }
#if defined(_ONE_SKIN)

                // A one skin at level 3 (_SkinCarve): one carve field read at the vertex's rest position over the
                // body-wide length, to a depth that is a share of the vertex's own half thickness (SkinCarveDepth).
                // Held in variables of its own: the lines above and below are the parts' and are not changed.
                float skinCarveValue = 0.0;
                float skinCarveMetres = 0.0;

                [branch] if (_SkinCarve > 0.5)
                {
                    [branch] if (skin)
                    {
                        float3 skinCarveGradient;

                        skinCarveValue = SkinCarveField(
                            input.restOS / max(_SkinCarveLength, 1e-6) + OwnShift(),
                            input.skinCarve.x, input.skinCarve.z, input.skinCarve.w, skinCarveGradient);

                        skinCarveMetres = SkinCarveDepth(
                            input.skinCarve.y, input.skinCarve.z, input.skinCarve.w, halfThickMetres);

                        body.skinCarve = float4(input.skinCarve.x, skinCarveMetres, input.skinCarve.z, input.skinCarve.w);
                    }
                }
#endif

                // Along the deformed normal, so the impressions cut into the bent surface rather
                // than into the box the mesh started as.
                float3 inward = -normalWS * (depth * carve);
#if defined(_ONE_SKIN)

                // The skin's, inward along the skinned normal, and no further than half its own thickness.
                [branch] if (_SkinCarve > 0.5)
                {
                    [branch] if (skin) inward = -normalWS * (skinCarveMetres * skinCarveValue);
                }
#endif
#if defined(_EVO_NOSE_PIT)

                // The nose pit (D242): the cup, inward along the shaped normal, and the carve calmed round it. A nose is
                // a box, a sphere or a capsule and never a blade or the one skin, which leave _Pit at zero.
                [branch] if (_Pit.w > 0.0 && !skin && !lamina)
                {
                    float pitMetres, pitKeep;
                    PitCut(carveOS, pitMetres, pitKeep);
                    inward = inward * pitKeep - normalWS * pitMetres;
                }
#endif
#if defined(_EVO_MOUTH2)

                // The mouth's opening (EVOSIM_THEATRE_MOUTH2): the cup, inward along the shaped normal, and the carve calmed
                // round it. On a part that is not a blade, from the part's own _Mouth; on the one skin, from its mouths and
                // the vertex's weights toward their parts. Each leaves _Mouth.w at zero unless the dial's class set it.
                [branch] if (_Mouth.w > 0.0 && !skin && !lamina)
                {
                    float mouthMetres, mouthKeep;
                    MouthPartCut(carveOS, mouthMetres, mouthKeep);
                    inward = inward * mouthKeep - normalWS * mouthMetres;
                }

                [branch] if (_Mouth.w > 0.0 && skin)
                {
                    float mouthMetres, mouthKeep;
                    MouthSkinCut(input.restOS, input.mouthSkin, halfThickMetres, mouthMetres, mouthKeep);
                    inward = inward * mouthKeep - normalWS * mouthMetres;
                }
#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)

                // The nose pit on the one skin (EVOSIM_THEATRE_NOSE_PIT=2): the cup, inward along the skinned normal, and the
                // carve calmed round it, weighted by the vertex's share in the nose's part. _PitSkin.w is zero unless the
                // dial's class set it on this skin's block.
                [branch] if (_PitSkin.w > 0.0 && skin)
                {
                    float noseMetres, noseKeep;
                    NoseSkinCut(input.restOS, input.pitSkin, halfThickMetres, noseMetres, noseKeep);
                    inward = inward * noseKeep - normalWS * noseMetres;
                }
#endif

                if (box)
                {
                    // The same displacement written in the part's own units, so that the box clamp
                    // has the last word on a box. The world to object matrix is the exact inverse
                    // and parts are never sheared, so this is the same move in the other basis and
                    // not an approximation of it; the clamp then makes inside-ness a property of
                    // the final position rather than of the sign of a term.
                    shapedOS = clamp(
                        shapedOS + mul((float3x3)GetWorldToObjectMatrix(), inward),
                        -meshHalf, meshHalf);

                    positionWS = TransformObjectToWorld(shapedOS);
                }
                else
                {
                    positionWS += inward;
                }
#if defined(_EVO_UPTAKE)

                // The absorptive uptake look: tissue that is taking in swells along its normal, by metres the
                // palette's writer worked out from the part's own smallest half-extent (up to 6% of it), the
                // same for every visual of a part. The one term besides the leaf's curl that can leave the
                // collider, and it is bounded by that 6%. Only the dial sets _Uptake.
                if (_Uptake.x > 0.0) positionWS += normalWS * _Uptake.y;
#endif

                // The float's pulse, last, on the carved and clamped shape, so it squeezes the body
                // that is drawn, and what keeps it inside is the collider's own symmetry.
                [branch] if (_Sway > 0.5)
                {
                    if (!skin && abs(_SwayPart.x - SwayKindFloat) < 0.5) FloatPulse(carveOS, positionWS, normalWS);
                }

                body.positionWS = positionWS;
                body.normalWS = normalWS;
                body.carveOS = carveOS;
                body.carveDepth = depth;
                body.leaf = leaf;
                body.halfThickMetres = halfThickMetres;
                body.halfWidthMetres = halfWidthMetres;
                body.tissue = tissueBox ? 1.0 : 0.0;

                return body;
            }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            // Opaque unless the palette hands a fading body (a birth, the end of a corpse) its see-through
            // copy of this material, which blends by _Fade and writes no depth. The depth passes
            // below are opaque only, and URP does not draw a transparent body into them.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma multi_compile_local _ _ONE_SKIN
            #pragma multi_compile_local _ _EVO_PATTERN_AA
            #pragma multi_compile_local _ _EVO_SKIN_JOIN
            #pragma multi_compile_local _ _EVO_JOIN_DEBUG
            #pragma multi_compile _ _EVO_NOSE_PIT
            #pragma multi_compile _ _EVO_DEBUG_TINT
            #pragma multi_compile _ _EVO_THALLI
            #pragma multi_compile _ _EVO_NOSE_SKIN
            #pragma multi_compile _ _EVO_BODY_DAPPLE
            #pragma multi_compile _ _EVO_MOUTH2
            #pragma multi_compile _ _EVO_SKIN2
            #pragma multi_compile _ _EVO_SKIN2B
            #pragma multi_compile _ _EVO_SKIN2_BAND4
            #pragma multi_compile _ _EVO_SHEEN_CAP
            #pragma multi_compile _ _EVO_SHEEN_CAP2
            #pragma multi_compile _ _EVO_UPTAKE
            #pragma multi_compile _ _EVO_ALGA_BLADE
            #pragma multi_compile _ _EVO_ALGA_BLADE2
            #pragma multi_compile _ _EVO_ALGA_RAMP_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_TARGET
            #pragma multi_compile _ _EVO_ALGA_BLADE3
            #pragma multi_compile _ _EVO_ALGA_ARC
            #pragma multi_compile _ _EVO_ALGA_ARC_DRIVE
            #pragma multi_compile _ _EVO_ALGA_FLUTTER
            #pragma multi_compile _ _EVO_ALGA_FLUTTER_NOISE
            #pragma multi_compile _ _EVO_SKIN_MAP

            #pragma multi_compile _ _EVO_CAUSTIC_VARIETY
            #pragma multi_compile _ _EVO_LOOK_STAGE
            #pragma multi_compile _ _EVO_LOOK_PROBE
            #pragma multi_compile _ _EVO_DEATH_DRAIN
            #pragma multi_compile _ _EVO_DEATH_MILK
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                // x is the fog factor, y is how translucent this part's thickness makes it.
                float2 fogAndThickness : TEXCOORD2;
                // The undisplaced object position, so the fragment can ask the carve field the
                // same question the vertex asked it, and in x the depth in metres it was asked with.
                // On a one skin the position is its rest position over the column lengths (PartSizes), so
                // every marking that reads the position in metres reads the rest frame's (Vertex).
                float3 positionOS : TEXCOORD3;

                // x the carve depth; yzw a one skin's tissue colour (Attributes.skinColour), zero elsewhere.
                // Packed into the depth's register, which had three to spare, so the skin adds no interpolator.
                float4 depthColour : TEXCOORD4;

                // On a leaf: s, the distance across the half width (-1 to 1), t, and one. Zero
                // elsewhere, which draws no veins.
                float4 leaf       : TEXCOORD5;

                // x one on a box part under the soft tissue (_OrganicTissue), zero elsewhere. Constant over a
                // part, so it interpolates to one or zero and is tested at a half. yzw a one skin's rest normal
                // (Attributes.restNormalOS), zero elsewhere, packed the same way.
                float4 tissueNormal : TEXCOORD6;

                // The mesh's own solid flag and half extent (Attributes.shapeOS), for the rim band alone: constant
                // over a mesh, so it interpolates to the same pair, and is read only behind a test of _RimBand.
                float2 shape      : TEXCOORD7;
#if defined(_EVO_SKIN2)

                // SKIN2 (EVOSIM_THEATRE_SKIN2): a one skin's four marks, from the vertex's own leaf channel (UV4, zero on a
                // skin without the dial). x how thin the tissue is against its part's usual thickness, y how near the rest
                // normal lies to the part's rim, z how evenly two bones share the vertex (a joint's crease), w the crease's
                // depth in rest metres. Zero on every mesh that is not a skin.
                float4 skin2 : TEXCOORD8;
#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)

                // The one skin's share in the nose's part (Attributes.pitSkin), zero on every mesh that is not a skin: the
                // fragment stage shades the pit by it, so a pixel on a neighbouring part's surface inside the radius takes
                // none of the pit.
                float pitSkin : TEXCOORD9;
#endif
            };

            // The midrib and the margin: a midrib from the stalk that fades up the blade, and a
            // rim barely darker than the lamina, since a blade's thin edge lets the light through.
            // On the face the midrib is a little lighter than the lamina; in the light that comes
            // through the blade it is the shadow. Faded out when it is finer than a pixel, so a
            // distant crowd does not shimmer.
            void LeafVeins(float4 leaf, out float face, out float through)
            {
                face = 1.0;
                through = 1.0;
                if (leaf.w < 0.5) return;

                float s = leaf.x;
                float v = leaf.y;
                float t = leaf.z;

                // A blade has no pinnate veins. What it has, in kelps like Alaria, is a thickened
                // midrib from the stalk that fades up the blade; nothing past half way.
                float midrib = exp(-v * v / 0.0012) * (1.0 - smoothstep(0.1, 0.55, s));
                midrib *= saturate(1.0 - 20.0 * fwidth(v));

                float vein = saturate(midrib) * _VeinStrength;

                // A seaweed has no midrib (_Alga, EVOSIM_THEATRE_ALGA), but for a faint kelp's on a strap more than
                // AlgaKelpLow to AlgaKelpHigh times as long as it is wide, at AlgaKelpMidrib of the leaf's.
                [branch] if (_Alga > 0.001) vein *= AlgaKelpMidrib * AlgaFormOfLeaf(leaf.w).z;
#if defined(_EVO_ALGA_BLADE)
                // The broad blade's faint midrib, on a part longer than AlgaBladeMidribLow times its width and none on a short
                // one, fading out up the blade from AlgaBladeRibFadeLow to High (the leaf's fades by half way).
                float ribFine = saturate(1.0 - 20.0 * fwidth(v));
                [branch] if (_Alga > 0.001 && AlgaIsFrond())
                    vein = saturate(exp(-v * v / 0.0012) * (1.0 - smoothstep(AlgaBladeRibFadeLow, AlgaBladeRibFadeHigh, s)) * ribFine) *
                        _VeinStrength * AlgaBladeMidrib *
                        smoothstep(AlgaBladeMidribLow, AlgaBladeMidribHigh, 1.0 / max(1e-6, 2.0 * (leaf.w - 1.0)));
#endif

                float margin = smoothstep(0.8, 1.0, abs(t));

                face = 1.0 + 0.35 * vein - 0.08 * margin;
                through = 1.0 - 0.6 * vein;
            }

#if defined(_EVO_SKIN2_BAND4)
            // SKIN2_BAND at 4: the part-end mark rides in the skin's tissue flag (Varyings.tissueNormal.x), which is zero on a
            // skin, and every reader of it tests it against a half. So it goes in times this, under a half, and the fragment
            // reads it back over it. No interpolator is added; all ten are in use with the nose pit.
            static const float Skin2B4EndPack = 0.4;

#endif
            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
#if defined(_EVO_ALGA_ARC_DRIVE) && defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                arcDriveColour = float3(0.0, 0.0, 0.0);
#endif

                BodyVertex body = ShapeBody(input);

                output.positionWS = body.positionWS;
                output.normalWS = body.normalWS;
                output.positionOS = body.carveOS;
                output.depthColour = float4(body.carveDepth, 0.0, 0.0, 0.0);
#if defined(_EVO_ALGA_ARC_DRIVE) && defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                // The arc diagnostic's false colour (modes 2 and 3) rides in depthColour.yzw. A blade is neither a one skin nor a
                // thallus, the only meshes that write yzw (below, and only for their own shape channel), so nothing is overwritten;
                // the colour is zero on every vertex the arc does not act on.
                output.depthColour.yzw = arcDriveColour;
#endif
                output.positionCS = TransformWorldToHClip(body.positionWS);
                output.leaf = body.leaf;
                output.tissueNormal = float4(body.tissue, 0.0, 0.0, 0.0);
                output.shape = input.shapeOS;
#if defined(_EVO_SKIN2)
                output.skin2 = input.shapeOS.x > 2.5 ? input.leafOS : float4(0.0, 0.0, 0.0, 0.0);
#endif

                // A one skin (EVOSIM_THEATRE_ONE_SKIN): its markings read the rest frame, not the skinned body's
                // object space, so they ride on the tissue as the body bends and turns. The markings read the
                // position in metres as positionOS * PartSizes(), so the rest position goes in over those column
                // lengths, written here as twice HalfExtentsWS, the same lengths; the fragment moves the one that
                // reads the world (the mottle's flat cells) and the planes they are laid on. Its colour and its
                // rest normal ride in the spare components.
                [branch] if (input.shapeOS.x > 2.5)
                {
                    output.positionOS = input.restOS / max(2.0 * HalfExtentsWS(), 1e-6);
                    output.depthColour.yzw = input.skinColour.rgb;
                    output.tissueNormal.yzw = input.restNormalOS;
                }
#if defined(_EVO_SKIN2_BAND4)

                // SKIN2_BAND at 4: the skin's part-end mark (UV0), packed under a half in the tissue flag (Skin2B4EndPack).
                [branch] if (input.shapeOS.x > 2.5) output.tissueNormal.x = Skin2B4EndPack * saturate(input.band4End.x);
#endif
#if defined(_ONE_SKIN)

                // The skin's carve at level 3, for the fragment's rebuilt normal: the wrinkles' share, the depth in metres
                // and the tissue weight in the leaf's xyz (a skin has no leaf, so they are free and its w stays zero), the
                // soft-leaf weight in the shape's y (read on a box alone). No new interpolator.
                [branch] if (_SkinCarve > 0.5)
                {
                    [branch] if (input.shapeOS.x > 2.5)
                    {
                        output.leaf.xyz = body.skinCarve.xyz;
                        output.shape.y = body.skinCarve.w;
                    }
                }
#endif
#if defined(_EVO_MOUTH2)

                // The one skin's share in an eating part's tissue, minus, in the leaf's w, which a skin leaves free and
                // which every reader tests against one half (a leaf is above it): the fragment stage shades the mouth by
                // it, so a pixel on a neighbouring part's surface inside the radius takes none of the opening.
                [branch] if (input.shapeOS.x > 2.5) output.leaf.w = -saturate(dot(input.mouthSkin, float4(1.0, 1.0, 1.0, 1.0)));
#endif
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)
                output.pitSkin = input.shapeOS.x > 2.5 ? saturate(input.pitSkin) : 0.0;
#endif

                // A thin part transmits and a thick one does not. Faked from the geometry rather
                // than from a baked thickness map, which is what the single pass approximation
                // leaves to the author [JA]: there is no unwrapped mesh here to bake into. On a
                // leaf the thickness is the lens's here, so the rim glows more than the midrib.
                // A blade is translucent by its own size as well: a twelfth of its half width is
                // added to the threshold, so a large blade still lets the light through where it
                // is thin, as kelp does.
                float transMetres = _TransMetres + 0.08 * body.halfWidthMetres;
                float thickness = saturate(transMetres / max(1e-4, body.halfThickMetres));

                output.fogAndThickness = float2(ComputeFogFactor(output.positionCS.z), thickness);
#if defined(_EVO_THALLI)

                // A thallus (EVOSIM_THEATRE_THALLI): its thinness is the mesh's vertex colour b (one at Ulva's thin margin, none on
                // Codium), and its tone and lime (r, g) go to the fragment in the depth register's spare components, which a mesh
                // that is not a skin leaves at zero.
                [branch] if (ThalliIs(input.shapeOS.x))
                {
                    output.fogAndThickness.y = saturate(input.skinColour.b);
                    output.depthColour.yzw = input.skinColour.rgb;
                }
#endif

                return output;
            }

            // The normal of the carved surface, rebuilt per pixel from the field's own gradient.
            //
            // Why per pixel and not per vertex. Without this the carve is nearly invisible: a
            // dent that does not shade is only a change in the outline, and most of a body is
            // not on its outline. Taking it in the fragment stage also frees the impressions
            // from the mesh, so the wrinkles read at full resolution on a mesh dense enough only
            // for the lobes, which is what keeps two thousand bodies affordable.
            //
            // The chain rule, which is the only fiddly part. The field is a function of the
            // object position and the surface is in world metres, so the object gradient has to
            // be divided by how many metres an object unit is on each axis and then turned into
            // world axes. The object to world matrix's column i is (size on axis i) times (world
            // direction of axis i), so column_i / dot(column_i, column_i) is exactly that world
            // direction divided by that size. Parts are never sheared, so the columns are
            // orthogonal and the three terms simply add.
            //
            // Then the standard displaced surface normal: for a surface pushed along its normal
            // by a height h, the new normal is the old one less the part of grad h lying in the
            // surface. The pinch's own gradient is left out; it varies over a whole part rather
            // than over a wrinkle, so its slope is small beside the field's.
            float3 CarvedNormal(float3 normalWS, float3 positionOS, float depth, float tissue, float leafSoft)
            {
                float3 he = HalfExtentsWS();
                float smallest = min(he.x, min(he.y, he.z));

                // The same field the vertex stage cut by: TissueCarve's on a box part under the soft tissue.
                float3 gradientOS;

                [branch] if (tissue > 0.5)
                {
                    TissueCarve(positionOS + OwnShift(), _Carve.x, _Carve.y, _Carve.z, he, gradientOS);
                }
                else if (leafSoft > 0.5)
                {
                    // A blade under the leaf dial (the caller's flag): LeafCarve's field, as the vertex stage cut.
                    LeafCarve(positionOS + OwnShift(), _Carve.x, gradientOS);
                }
                else
                {
                    BodyCarve(positionOS + OwnShift(), smallest, gradientOS);
                }

                float3 cx = UNITY_MATRIX_M._m00_m10_m20;
                float3 cy = UNITY_MATRIX_M._m01_m11_m21;
                float3 cz = UNITY_MATRIX_M._m02_m12_m22;

                float3 gradientWS =
                    gradientOS.x * cx / max(1e-8, dot(cx, cx)) +
                    gradientOS.y * cy / max(1e-8, dot(cy, cy)) +
                    gradientOS.z * cz / max(1e-8, dot(cz, cz));

                // The height is -depth * field, so its gradient is -depth times the field's.
                float3 slope = -depth * gradientWS;
                float3 alongSurface = slope - normalWS * dot(slope, normalWS);

                // Bounded, because a steep wrinkle on a nearly flat face can otherwise turn the
                // normal past ninety degrees and light the inside of the body. Smoothly rather
                // than by a clamp: a hard clamp bites over whole regions of a well carved body
                // at once, and every pixel inside such a region then gets the same tilt, which
                // is a flat patch of noise where the wrinkle was.
                float reach = length(alongSurface);
                alongSurface *= rsqrt(1.0 + reach * reach / (1.4 * 1.4));

                return normalize(normalWS - alongSurface);
            }

            // A wrapped Lambert. A hard terminator on a body two centimetres across is one pixel
            // of information; wrapping it spreads the shading over the whole of a small body,
            // which is what makes size and curvature readable at the scale these are seen at.
            float Wrapped(float3 n, float3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
            }

            // The cell view's darkest shade, a share of its colour (the fragment's cell-view branch): enough shading for
            // a shape to read, little enough that a part in shade keeps its colour.
            static const float CellViewShadeFloor = 0.78;

            // The cell view's answer to the frame's post-exposure (TheatreGrade, 0.4 EV by default): 2^-0.4, so a lit face
            // leaves the grade near its swatch. Without it the consumer's vermillion came out (233,128,0) against its
            // (213,94,0), halfway to the absorptive amber (b43).
            static const float CellViewExposureLift = 0.758;

            float3 Transmission(float3 n, float3 v, float3 l, float3 lightColour, float thickness)
            {
                // Barre-Brisebois and Bouchard's single pass approximation [BB11] in Austin's URP
                // arrangement [JA]: the light direction is bent by the normal, and what the eye
                // catches is the part of it coming back towards the camera from behind the body.
                float3 bent = normalize(l + n * _TransDistortion);
                float back = pow(saturate(dot(v, -bent)), _TransPower) * _TransScale;

                return lightColour * (back * thickness);
            }

            // ---------------------------------------------------------------- the two dials
            //
            // Everything from here to the fragment reaches a pixel only behind a test of _TissueLook,
            // _Sss, _Soft, _SoftHull or _BoardLight, all zero unless TheatrePalette sets them, so a frame with the dials off is the
            // frame from before they existed. (The bands are worked out before their test, for the
            // derivative they take, and used only inside it.)

            // How far apart a link's muscle bands are, as a fraction of its smallest half extent: the
            // same in metres on a capsule's shaft and its caps, which share a cross radius.
            static const float BandSpacing = 0.7;

            // The tissue look's muscle bands: a darkening in stripes across the part's long axis, read
            // at the undisplaced object position so the bands stay on the tissue as it moves. The long
            // axis takes Y on a tie, so a capsule's caps band the way its shaft does. Faded where a band
            // is finer than a couple of pixels, so a distant link does not shimmer. One minus a
            // fraction, so it only darkens.
            float TissueBands(float3 positionOS)
            {
                float3 he = HalfExtentsWS();

                float3 axis;
                if (AtLeast(he.y, he.x) && AtLeast(he.y, he.z)) axis = float3(0, 1, 0);
                else if (AtLeast(he.x, he.z)) axis = float3(1, 0, 0);
                else axis = float3(0, 0, 1);

                float smallest = min(he.x, min(he.y, he.z));

                // An object unit on an axis is the visual's scale on it, which is twice the half extent.
                float metres = dot(positionOS, axis) * 2.0 * dot(he, axis);
                float x = metres / max(1e-4, BandSpacing * smallest);

                float stripe = 0.5 + 0.5 * cos(6.2831853 * x);
                float fade = saturate(1.0 - 1.5 * fwidth(x));

                return 1.0 - _Banding * stripe * fade;
            }

            // ---------------------------------------------------------------- the pattern family
            //
            // EVOSIM_THEATRE_SKIN_MAP_FAMILY: a family of marks in a second colour, by _FamilyParams.x (1 bars across the long axis, 2 an
            // eyespot on each flank near the far end of it, 3 a line round the silhouette). Returns the mark's share, and for an eyespot its
            // black core in the out parameter. Zero, and the core zero, unless _FamilyParams.y is over zero. The axes are TissueBands's
            // (Y on a tie, then X), read at the undisplaced object position so the marks stay on the tissue as it moves. On a part the
            // object units are the part's own, -0.5 to 0.5 on each axis; on a one skin the object space is the body's (see HalfExtentsWS).
            float3 FamilyAxis(int i)
            {
                return float3(i == 0 ? 1.0 : 0.0, i == 1 ? 1.0 : 0.0, i == 2 ? 1.0 : 0.0);
            }

            // The three marks' shares, each by its own function so that the single mode (_FamilyParams.x) and the mix (_FamilyMix.x) draw the
            // same mark.
            //
            // The edge outline: where the surface turns away from the eye. widen (_FamilyParams.w in the single mode, _FamilyMix.y in the mix;
            // EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE) widens it, the smoothstep's edges running from 0.72 and 0.92 to 0.35 and 0.75 as it goes from 0 to 1.
            // bendGate (_FamilyMix.z; EVOSIM_THEATRE_SKIN_MAP_FAMILY_EDGE_BEND) keeps it to where the surface bends: the rim alone covers the
            // whole face of a flat part seen at a glancing angle (40020 and 54035 went glassy), whose normal hardly changes from pixel to pixel,
            // while at a real edge it turns fast on screen. bend is the normal's screen derivative, taken in the open by the caller; at 1 the
            // share is scaled by saturate(bend * EdgeBendScale), and at 0 it is the rim alone.
            static const float EdgeBendScale = 25.0;

            float FamilyEdgeShare(float3 normalWS, float3 viewDirWS, float widen, float bend, float bendGate)
            {
                float rim = 1.0 - saturate(abs(dot(normalize(normalWS), normalize(viewDirWS))));
                float edge = (widen > 0.0) ? smoothstep(lerp(0.72, 0.35, widen), lerp(0.92, 0.75, widen), rim) : smoothstep(0.72, 0.92, rim);

                return edge * lerp(1.0, saturate(bend * EdgeBendScale), bendGate);
            }

            // Bars: whole bars across the long axis, a bar in the middle of each of barCount cells, sharp-edged and narrower than the gaps,
            // faded where a bar is finer than a pixel or so.
            float FamilyBarsShare(float3 p, float3 dp, float3 axisL, float barCount)
            {
                float bars = max(barCount, 1.0);
                float x = (dot(p, axisL) + 0.5) * bars;
                float fade = saturate(1.0 - 1.5 * bars * dot(dp, axisL));

                return smoothstep(0.62, 0.70, 0.5 - 0.5 * cos(6.2831853 * x)) * fade;
            }

            // The eyespot: a ring and a black core (the out parameter), centred 0.30 along the long axis (object units) and in the middle of the
            // second, the distance d in metres in that plane (an object unit on an axis is twice the half extent). The flank's
            // normal is the smallest axis, which the spot does not depend on, so it shows on both flanks.
            // With _FamilySpot.w over zero (a one skin under EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT) it is centred on _FamilySpot.xyz,
            // the body's largest part in the box's units, with that radius in metres.
            // EVOSIM_THEATRE_FAMILY_SPOT_SOFT (TheatreSkin.FamilySpotSoft): how far the eyespot's black core is filled with the mark's
            // colour, 0 to 1. A global, outside the buffer, never set with the dial off, so it stays the zero a shader global is.
            float _EvoFamilySpotSoft;

            float FamilyEyeShare(float3 p, float3 he, float3 axisL, float3 axisM, float3 axisS, out float core)
            {
                float mL = (dot(p, axisL) - 0.30) * 2.0 * dot(he, axisL);
                float mM = dot(p, axisM) * 2.0 * dot(he, axisM);
                float R = max(0.35 * dot(he, axisS) * 2.0, 1e-5);

                [branch] if (_FamilySpot.w > 0.0)
                {
                    mL = (dot(p, axisL) - dot(_FamilySpot.xyz, axisL)) * 2.0 * dot(he, axisL);
                    mM = (dot(p, axisM) - dot(_FamilySpot.xyz, axisM)) * 2.0 * dot(he, axisM);
                    R = max(_FamilySpot.w, 1e-5);
                }

                float d = length(float2(mL, mM));

                core = 1.0 - smoothstep(R * 0.5, R * 0.55, d);

                return saturate((1.0 - smoothstep(R * 0.9, R, d)) - (1.0 - smoothstep(R * 0.55, R * 0.62, d)));
            }

            float FamilyMask(float3 positionOS, float3 fwOS, float3 normalWS, float3 viewDirWS, float bend, out float core)
            {
                core = 0.0;
                if (_FamilyParams.y <= 0.0) return 0.0;

                // _FamilyMix.x over zero is the mix (EVOSIM_THEATRE_SKIN_MAP_FAMILY_MIX): the marks its bitmask selects (1 bars, 2 eyespot,
                // 4 edge outline), the largest of their shares and the eyespot's core. With it zero the single mode on _FamilyParams.x.
                bool mixed = _FamilyMix.x > 0.0;
                int mode = (int)round(_FamilyParams.x);

                if (!mixed && mode == 3) return FamilyEdgeShare(normalWS, viewDirWS, _FamilyParams.w, bend, _FamilyMix.z);

                // The position and half extents the marks are laid on, and the position's screen derivative in the same units. On a part: the
                // part's own object units and half extents. On a one skin with a rest box (_FamilyBox.w): the rest position, which is
                // positionOS over twice HalfExtentsWS inverted, mapped into the body's box so it runs -0.5 to 0.5 along each of its axes,
                // and the box's half extents.
                float3 he = HalfExtentsWS();
                float3 p = positionOS;
                float3 dp = fwOS;

                [branch] if (_FamilyBox.w > 0.5)
                {
                    float3 boxScale = he / max(_FamilyExtent.xyz, 1e-4);

                    p = (positionOS * 2.0 * he - _FamilyBox.xyz) / max(2.0 * _FamilyExtent.xyz, 1e-4);
                    dp = fwOS * boxScale;
                    he = _FamilyExtent.xyz;
                }

                // Which axis is longest (Y on a tie, then X, as TissueBands), then which of the other two is longer.
                int iL = (AtLeast(he.y, he.x) && AtLeast(he.y, he.z)) ? 1 : (AtLeast(he.x, he.z) ? 0 : 2);
                int iA = (iL == 0) ? 1 : 0;
                int iB = (iL == 2) ? 1 : 2;
                int iM = AtLeast(dot(he, FamilyAxis(iA)), dot(he, FamilyAxis(iB))) ? iA : iB;
                int iS = 3 - iL - iM;

                float3 axisL = FamilyAxis(iL);
                float3 axisM = FamilyAxis(iM);
                float3 axisS = FamilyAxis(iS);

                [branch] if (mixed)
                {
                    int bits = (int)round(_FamilyMix.x);
                    float share = 0.0;

                    if ((bits & 1) != 0) share = max(share, FamilyBarsShare(p, dp, axisL, _FamilyParams.z));
                    if ((bits & 2) != 0) share = max(share, FamilyEyeShare(p, he, axisL, axisM, axisS, core));
                    if ((bits & 4) != 0) share = max(share, FamilyEdgeShare(normalWS, viewDirWS, _FamilyMix.y, bend, _FamilyMix.z));

                    return share;
                }

                if (mode == 1) return FamilyBarsShare(p, dp, axisL, _FamilyParams.z);

                if (mode == 2) return FamilyEyeShare(p, he, axisL, axisM, axisS, core);

                return 0.0;
            }

            // Under _Sss the half extent that is fully translucent reaches this many times further,
            // since the path it is set against below is a chord and not the smallest half extent.
            static const float SssReach = 1.5;

            // The light through the tissue under _Sss, coloured more deeply the thicker the part: the
            // tint is raised to a power running from SssTintThin on the thinnest part to SssTintThick
            // on a thick one, as light loses its other colours along a longer path, then brought back
            // to the tint's own brightest channel, so a deeper colour is not a dimmer light. The gain
            // is what makes the tissue's tint the stronger part of what is seen through it.
            static const float SssTintThin = 1.25;
            static const float SssTintThick = 2.0;
            static const float SssTransGain = 1.2;

            // The inner glow under _Sss: what an edge keeps of it, how fast it falls from the middle
            // of the view to the edge, and what a part too thick to see into keeps.
            static const float SssGlowFloor = 0.15;
            static const float SssGlowPower = 1.5;
            static const float SssGlowThick = 0.6;

            // The sheen under _Sss: how far the glint's normal is turned back from the carved surface
            // to the smooth one, and what is left of the glint's strength. The carve's terraces are
            // flat between steep walls, and each flat catches the key as one white fleck; on the smooth
            // normal the glint is one highlight over the body's own shape.
            static const float SssSheenSmooth = 0.7;
            static const float SssSheenGain = 0.6;
#if defined(_ONE_SKIN)

            // A one skin's key-light lobe (any level): tighter than the parts' 28. On the uncarved skin the key's Blinn
            // lobe formed one broad highlight disc at a fixed place on the screen, where the parts' carve breaks it into
            // flecks; at this power the disc is a small glint, and the carve (level 3) breaks it up further.
            static const float SkinSheenPower = 96.0;
#endif

            static const float3 LumaWeights = float3(0.2126, 0.7152, 0.0722);
#if defined(_EVO_JOIN_DEBUG)

            // ---------------------------------------------------------------- JOIN_DEBUG
            //
            // EVOSIM_THEATRE_JOIN_DEBUG (1 to 4), a diagnostic and never a look, on the one skin alone: the fragment returns an
            // unlit false colour in place of the skin's colour, once everything the view reads has been worked out (the last
            // lines of Fragment). Compiled only under _EVO_JOIN_DEBUG, a local keyword TheatreOneSkin turns on in the skin's own
            // material copy while the dial is above 0, with the dial's value in the global _EvoJoinDebug. Nothing is read from
            // the screen and nothing moves; the statics below are only written where the functions that own the numbers run.
            //   1  the triplanar plane weights as red, green, blue (x, y, z), as JoinCover computes them (after JoinBlendPower
            //      and the normalisation); needs _EVO_SKIN_JOIN
            //   2  the cover JoinCover returns, grey, 0 black to 1 white; needs _EVO_SKIN_JOIN
            //   3  red: the amount Skin2MarginB lerps the body toward its band colour, all its terms (its cover times its
            //      opacity); green: the light the thin backlight adds, as luminance over JoinDebugThinFull; blue: the vertex's
            //      crease mark (marks z) as the vertex carries it, before JoinCreaseKeep
            //   4  the stretch of the rest coordinate against the drawn surface, length(fwidth(rest metres)) over
            //      length(fwidth(position in the world)), times _SkinScale (the world metres of one rest metre) so an unstretched
            //      surface reads 1: JoinDebugStretchGrey mid grey at 1, blue toward JoinDebugStretchLow, red toward
            //      JoinDebugStretchHigh, each full at its value, on a log scale
            // Views 1 and 2 show magenta where JoinCover did not run (no pattern drawn at the pixel, or _EVO_SKIN_JOIN off).
            float _EvoJoinDebug;
            static const float JoinDebugThinFull = 0.25;            // the backlight's added luminance that reads full green; the dark
                                                                    // field's lit values are of this order, to be tuned by eye
            static const float JoinDebugStretchLow = 0.5;           // the stretch that reads full blue (the maze compressed)
            static const float JoinDebugStretchHigh = 2.0;          // the stretch that reads full red (the maze stretched)
            static const float JoinDebugStretchGrey = 0.5;          // the grey of a stretch of 1
            static const float3 JoinDebugUnreached = float3(1.0, 0.0, 1.0);

            // What the functions that own the numbers leave for the end of Fragment. A negative cover or plane weight is "not run".
            static float3 JoinDebugPlanes = float3(-1.0, -1.0, -1.0);
            static float JoinDebugCover = -1.0;
            static bool JoinDebugBandOn = false;                    // true across the skin's own call of Skin2MarginB alone, so the
                                                                    // mouth's lip, which calls it too, writes nothing
            static float JoinDebugBand = 0.0;
            static float JoinDebugThin = 0.0;
            static float JoinDebugCrease = 0.0;

            float3 JoinDebugColour(int view, float stretch)
            {
                [branch] if (view == 1) return JoinDebugPlanes.x < 0.0 ? JoinDebugUnreached : saturate(JoinDebugPlanes);
                [branch] if (view == 2) return JoinDebugCover < 0.0 ? JoinDebugUnreached : float3(JoinDebugCover, JoinDebugCover, JoinDebugCover);
                [branch] if (view == 3) return saturate(float3(JoinDebugBand, JoinDebugThin / JoinDebugThinFull, JoinDebugCrease));

                float logStretch = log2(max(stretch, 1e-4));
                float3 colour = float3(JoinDebugStretchGrey, JoinDebugStretchGrey, JoinDebugStretchGrey);
                colour = lerp(colour, float3(0.0, 0.0, 1.0), saturate(logStretch / log2(JoinDebugStretchLow)));
                colour = lerp(colour, float3(1.0, 0.0, 0.0), saturate(logStretch / log2(JoinDebugStretchHigh)));
                return colour;
            }

#endif

            // The soft dial's terms in the fragment (_Soft; its carve is with ShapeBody). Each scales
            // what the material or the tissue look already holds, so a tissue's own rim and mottle are
            // the starting point and not replaced.
            //
            // The mottle's cells per metre and its strength, as shares of the tissue's: cells two and a
            // half times as wide, so the skin reads as patches of colour and not as grit, and gentler.
            static const float SoftMottleCells = 0.4;
            static const float SoftMottleStrength = 0.6;

            // The rim's sharpness, as a share of the tissue's: a lower power is a wider band, a glow
            // round the silhouette and not a line on it. The strength is left alone, so the edge itself
            // is as bright as today and only the fall-off is softer.
            static const float SoftRimPower = 0.65;

            // How far the carved normal is turned back toward the uncarved one: over the whole body,
            // and near a joint anchor, where the pinch deepens the carve and two parts meet. A body
            // keeps its lobes in the light, and the light rolls over it; at a junction the relief of
            // both parts' waists all but goes, so the two surfaces meet without a crease of light.
            static const float SoftSmooth = 0.2;
            static const float SoftSeamSmooth = 0.85;

            // The hull's terms in the fragment (_SoftHull), each on what the soft dial has left.
            //
            // How far the normal is turned back again toward the uncarved one, over the whole body and near
            // a joint anchor: with the soft dial's own turn before it, about seven tenths of the way over the
            // body and all but the whole of it at a joint, so the light rolls over one skin and two parts
            // that overlap meet without a crease of light.
            static const float HullSmooth = 0.6;
            static const float HullSeamSmooth = 0.97;

            // The mottle under the hull, as shares of the soft dial's: cells four times as many, so about
            // one and a half times the tissue's own count, and a marking two fifths as strong, so the
            // skin carries the fine faint speckle of wet skin and no patches.
            static const float HullMottleCells = 4.0;
            static const float HullMottleStrength = 0.4;

            // ---------------------------------------------------------------- papillae and a frill
            //
            // The bold tissue looks (_Papillae, _Frill; EVOSIM_THEATRE_TISSUE_LOOK=2). Every term that names
            // either reaches a pixel only behind a test of it, and the palette sets them only at 2, so a
            // frame below 2 is the one without them. Nothing here moves a vertex outward: papillae are
            // shading alone, since the mesh's vertices are far apart beside a papilla, and the frill on a
            // part that is not a blade is shading too. (A blade's scalloped outline is LeafScallop, which
            // cuts inward.)

            // A papilla's footprint, in cells: the radius of the dome round a feature point. Feature points
            // sit between 0.15 and 0.85 of their cell, so a papilla is about two thirds of the spacing across
            // where the surface passes through its middle, and smaller, as a cut through a dome is, where it
            // passes to one side.
            static const float PapillaRadius = 0.55;

            // How steep a papilla's flank is, as the rise over the run at its steepest, at full strength: the
            // slope the normal is tilted by. Bounded below, as the carve's is.
            static const float PapillaRelief = 0.5;
            static const float PapillaSlopeBound = 1.4;

            // What the colour does: the base darkens by this much at full strength and the tip lifts this far
            // toward its own paler colour, the tip by the cell's own tone as well so no two are paler alike.
            static const float PapillaBaseDark = 0.32;
            static const float PapillaTipLift = 0.8;

            // A papilla's tip is the body's own colour made paler, by this gain, not a white: the board's
            // tubercles are a lighter tone of the mantle they stand on.
            static const float PapillaTipGain = 1.9;

            // The spacing in pixels over which they fade in: finer than the low end the papillae are gone and
            // the skin is the plain one, so a far body does not shimmer. A papilla is about two thirds of the
            // spacing, so the low end puts the smallest at two pixels across.
            static const float PapillaFadeLow = 3.0;
            static const float PapillaFadeHigh = 7.0;

            // The pleat's spacing along a part's edge, in cells per metre, and the pixel spacing over which
            // it fades in, as the papillae's does.
            static const float FrillCellsPerMetre = 90.0;
            static const float FrillFadeLow = 4.0;
            static const float FrillFadeHigh = 9.0;

            // The silhouette band the pleat shows in, on facing = 1 - n.v: nothing facing the eye, all of it
            // at the edge.
            static const float FrillEdgeLow = 0.5;
            static const float FrillEdgeHigh = 0.95;

            // What a crest and a notch do: the rim's strength over them, and what is left of the body's light
            // in a notch. A crest is brighter than the rim alone and a notch nearly dark, so the edge reads as
            // a row of lobes with a gap between each.
            static const float FrillCrestRim = 1.6;
            static const float FrillNotchRim = 0.1;
            static const float FrillNotchBody = 0.3;

            // 3. The soft tissue's pores and sheen (_OrganicTissue, through the tissue flag). Both are shading
            // alone, no vertex moves for them and nothing can leave the collider. Behind a test of the flag and
            // the palette sets _OrganicTissue only at EVOSIM_THEATRE_ORGANIC=2.
            //
            // The pores are dimples: PapillaField's dome, read as a depression, on a jittered grid in the
            // part's own metres, so they stay on the tissue as it moves. Their spacing and the pixel spacing
            // over which they fade in, as the papillae's does, so a far body does not shimmer: finer than the
            // low end the skin is plain. At 4 mm a spacing, the pores are whole at 0.5 mm a pixel or finer (a
            // 25 cm body across the frame at 1920 pixels is 0.13 mm) and gone at 1.3 mm a pixel or coarser.
            static const float TissuePoreCellsPerMetre = 250.0;
            static const float TissuePoreFadeLow = 3.0;
            static const float TissuePoreFadeHigh = 8.0;

            // How steep a pore's wall is at its steepest, the rise over the run at full strength (the papillae's
            // is 0.5; bounded by PapillaSlopeBound as theirs is), and how much a pore's middle darkens the
            // body: one minus this share of its colour.
            static const float TissuePoreRelief = 0.2;
            static const float TissuePoreDark = 0.12;

            // The sheen under the soft tissue: wet skin catches the key as a broad soft glow and not as a
            // polished stone's tight glint. The exponent falls from the body's 28 to this, a lobe about three
            // times as wide, and the gain falls to this share, so the peak is under half of the body's.
            // Composed with _Sss's glint when that dial is on (its smoothed normal and its own gain).
            static const float TissueSheenPower = 9.0;
            static const float TissueSheenGain = 0.45;

            // The feature point of the cell nearest to p, in cells, and the dome round it: its height in [0, 1]
            // (zero at and beyond PapillaRadius, a smooth quartic dome inside), the height's gradient in cells,
            // and the cell's own tone. The search is the cell and its twenty-six neighbours, as EvoVoronoi's is,
            // and the dome depends on the distance alone, so two papillae that touch meet at the same height
            // with a crease between them and the height is never discontinuous.
            void PapillaField(float3 p, out float height, out float3 gradient, out float tone)
            {
                float3 cell = floor(p);
                float3 frc = p - cell;

                float best = 8.0;
                float3 bestRel = float3(0.0, 0.0, 0.0);
                tone = 0.5;

                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        [unroll]
                        for (int z = -1; z <= 1; z++)
                        {
                            float3 offset = float3(x, y, z);
                            float3 seed = EvoHash3(cell + offset + float3(5.3, 1.7, 9.1));
                            float3 rel = offset + 0.15 + 0.7 * seed - frc;
                            float d = dot(rel, rel);

                            if (d < best)
                            {
                                best = d;
                                bestRel = rel;
                                tone = seed.x;
                            }
                        }
                    }
                }

                float dist = sqrt(best);
                float u = dist / PapillaRadius;
                float q = u * u;

                height = 0.0;
                gradient = float3(0.0, 0.0, 0.0);

                if (q < 1.0)
                {
                    height = (1.0 - q) * (1.0 - q);

                    // d(height)/d(distance) = -4 u (1 - q) / radius, and the distance grows away from the
                    // feature point, so the height's gradient points back toward it, along bestRel.
                    gradient = bestRel * (4.0 * u * (1.0 - q) / (PapillaRadius * max(dist, 1e-4)));
                }
            }

            // The part's three sizes in metres, the lengths of the object to world matrix's columns: the
            // position in the part's own units times this is the position in metres along its own axes.
            float3 PartSizes()
            {
                return float3(
                    length(UNITY_MATRIX_M._m00_m10_m20),
                    length(UNITY_MATRIX_M._m01_m11_m21),
                    length(UNITY_MATRIX_M._m02_m12_m22));
            }

            // The pleat along a part's silhouette, 0 in a notch and 1 on a crest. The long axis takes Y on a
            // tie, as TissueBands's does. Seen from the side the pleat runs along that axis, and seen end on
            // it runs round it, a whole number of lobes to the turn so the cut where the angle wraps shows
            // nothing; the two are blended by how end on the part is, so the edge is a row of lobes from
            // either side. Read at the undisplaced object position, so it stays on the tissue as it moves.
            float FrillPleat(float3 positionOS, float3 v)
            {
                float3 sizes = PartSizes();
                float3 m = positionOS * sizes;

                float along, ca, cb, radius;
                float3 axisWS;

                if (AtLeast(sizes.y, sizes.x) && AtLeast(sizes.y, sizes.z))
                {
                    along = m.y; ca = m.z; cb = m.x;
                    radius = 0.25 * (sizes.x + sizes.z);
                    axisWS = UNITY_MATRIX_M._m01_m11_m21 / max(sizes.y, 1e-6);
                }
                else if (AtLeast(sizes.x, sizes.z))
                {
                    along = m.x; ca = m.y; cb = m.z;
                    radius = 0.25 * (sizes.y + sizes.z);
                    axisWS = UNITY_MATRIX_M._m00_m10_m20 / max(sizes.x, 1e-6);
                }
                else
                {
                    along = m.z; ca = m.x; cb = m.y;
                    radius = 0.25 * (sizes.x + sizes.y);
                    axisWS = UNITY_MATRIX_M._m02_m12_m22 / max(sizes.z, 1e-6);
                }

                float turns = max(5.0, round(6.2831853 * radius * FrillCellsPerMetre));

                float sideways = abs(sin(PI * (along * FrillCellsPerMetre + _Individual * 7.0)));
                float round_ = abs(sin(PI * (atan2(cb, ca + 1e-7) * (0.15915494 * turns) + _Individual * 5.0)));
                float endOn = smoothstep(0.45, 0.9, abs(dot(axisWS, v)));

                return lerp(sideways, round_, endOn);
            }

            // Countershading (_Countershade, EVOSIM_THEATRE_COUNTERSHADE) and the colour ramp (_RampTip,
            // EVOSIM_THEATRE_COLOUR_RAMP). Both are on the body's colour alone, before the light: the sheen, the
            // rim, the glow and the light through the tissue are untouched. Each reaches a pixel only behind a
            // test, and the palette sets neither with its dial off.
            //
            // Countershading follows the world's up (the normal's y in world space), not the body's, so a body
            // that rolls keeps its dark side up, as a swimming animal holds itself. It reads the normal before
            // the carve (the vertex stage's, as the soft dial's turned-back normal does), so a wrinkle does not
            // flicker between dark and pale. The factor on the colour runs smoothly from CountershadeUnder facing
            // straight down to CountershadeTop facing straight up, and a colour channel is never taken over one
            // by it (it may already be, and is then left where it was). A blade is graded at CountershadeLeafShare
            // of that, the share of the way from no change to the full factor: an algal blade is paler beneath
            // too, but it is thin and the light through it does the rest.
            static const float CountershadeTop = 0.8;         // factor facing up; in (0, 1], below one darkens
            static const float CountershadeUnder = 1.1;       // factor facing down; at least one, capped at channel one
            static const float CountershadeLeafShare = 0.5;   // share of the grade a blade takes; in [0, 1]

            float3 Countershade(float3 body, float3 normalWS, bool blade)
            {
                float up = smoothstep(-1.0, 1.0, normalize(normalWS).y);
                float factor = lerp(CountershadeUnder, CountershadeTop, up);

                if (blade) factor = lerp(1.0, factor, CountershadeLeafShare);

                return min(body * factor, max(body, float3(1.0, 1.0, 1.0)));
            }
#if defined(_EVO_SKIN_MAP)

            // The same grade at a share of the full one (EVOSIM_THEATRE_SKIN_MAP, the body's countershade parameter): one is
            // Countershade, zero is no grade, and a share over one pushes both ends a little past it. Only the body's
            // colour, as Countershade, and a channel is never taken over one by the pale side.
            float3 CountershadeMapped(float3 body, float3 normalWS, bool blade, float share)
            {
                float up = smoothstep(-1.0, 1.0, normalize(normalWS).y);
                float factor = lerp(CountershadeUnder, CountershadeTop, up);

                factor = lerp(1.0, factor, share);

                if (blade) factor = lerp(1.0, factor, CountershadeLeafShare);

                return min(body * factor, max(body, float3(1.0, 1.0, 1.0)));
            }
#endif

            // The colour ramp: the body's colour multiplied toward the tissue's tip colour along the part, by
            // the smooth curve of its coordinate from base to tip. A blade takes its own coordinate from base to
            // tip (leaf.x, whose base is at the end the joint is on); every other part takes the palette's, a
            // dot product with the axis it gave this visual, which names the base end by the part's anchor.
            float3 ColourRamp(float3 body, float3 positionOS, float4 leaf)
            {
                float along = leaf.w > 0.5 ? leaf.x : dot(positionOS, _RampAxis.xyz) + _RampAxis.w;
                float t = smoothstep(0.0, 1.0, saturate(along));

                return body * lerp(float3(1.0, 1.0, 1.0), _RampTip.xyz, t);
            }

            // The rim band (_RimBand, EVOSIM_THEATRE_RIM_BAND), on a consumer's box part: two stripes of pigment
            // round the outline of the part, a pale line inside a warm band, as a sea slug's mantle has (Chromodoris)
            // or a flatworm's rim. Colour only, on the body's colour before the light, read at the undisplaced
            // object position (the pores' and the bands' way), so it stays on the tissue as the part moves and turns
            // and follows neither the light nor the view. The palette sets _RimBand only while its dial is on, and
            // only to one on a consumer part drawn in colour; the shader tests it before the call.
            //
            // The outline is that of the section across the part's thinnest axis, TissueMargin's section (the thin
            // axis found with its tie rule): the distance e from the section's centre toward its edge, a p-norm of
            // 8 of the two other coordinates over the mesh half, so the corners do not kink. e runs from 0 at the
            // middle of the broad faces to 1 at their edge, and is about 1 on the faces across them, so the band
            // lies round the rim of the broad face and the warm stripe continues over the sides. t is e rescaled so
            // the band begins at 0 and the section's edge is 1.
            static const float RimBandInner = 0.82;       // e where the band begins; in (0, 1)
            static const float RimBandLine = 0.25;        // the pale line's share of the band's width, from its
                                                          // inner edge; in (0, 1), and the warm band is the rest
            static const float RimBandEdge = 0.1;         // the smoothstep's width, a share of the band; in (0, RimBandLine]

            // Each stripe's colour is the body's own luminance times a lift, so the band is at most this many times
            // as light as the body (the board's colours are flash-lit, and the water takes the rest), mixed into the
            // body by a strength under one so the tissue shows through. The pale line is near white with this share
            // of the body's own hue; the warm band is a yellow-orange of luminance one (the weights: 0.2126, 0.7152,
            // 0.0722), modest in saturation, so the lift is its lightness whatever the body.
            static const float RimBandPaleLift = 1.6;     // at least 1, about 1.6 at most
            static const float RimBandWarmLift = 1.4;     // at least 1, about 1.6 at most
            static const float RimBandPaleTrace = 0.15;   // share of the body's hue in the line; in [0, 1]
            static const float3 RimBandWarm = float3(1.35, 0.95, 0.45);
            static const float RimBandPaleStrength = 0.85;   // in [0, 1]
            static const float RimBandWarmStrength = 0.75;   // in [0, 1]

            // The pale line is finer than the warm band, so it fades out first: the line's width in pixels, gone
            // under the first and whole over the second. The warm band is not faded. The width is read from the
            // part's size, which overstates it where the margin folds away edge-on, so the fade starts well above a
            // pixel: at 1 to 3 the biter's line (about 2.7 by this estimate) broke into white dashes along its fold (b40).
            static const float RimBandFadeLow = 3.0;
            static const float RimBandFadeHigh = 6.0;

            float3 RimBand(float3 body, float3 positionOS, float meshHalf, float pixelMetres)
            {
                // The thinnest axis, with TissueMargin's tie rule, and the two others as a mask.
                float3 he = HalfExtentsWS();
                float3 axisT;
                if (AtMost(he.x, he.y) && AtMost(he.x, he.z)) axisT = float3(1, 0, 0);
                else if (AtMost(he.y, he.z)) axisT = float3(0, 1, 0);
                else axisT = float3(0, 0, 1);

                // e, the p-norm of 8 on the point scaled to the unit max norm so nothing underflows
                // (TissueMargin's way). The thin coordinate is masked out, so it adds nothing.
                float3 q = abs(positionOS) * (1.0 - axisT) / max(meshHalf, 1e-4);
                float big = max(max(q.x, q.y), max(q.z, 1e-5));
                float3 r2 = (q / big) * (q / big);
                float3 r4 = r2 * r2;
                float e = big * pow(dot(r4 * r4, float3(1.0, 1.0, 1.0)), 0.125);

                float t = (e - RimBandInner) / (1.0 - RimBandInner);

                // The stripes: the line rises out of the body at the band's inner edge and hands over to the warm
                // band at RimBandLine, the two crossfading over RimBandEdge; the warm band holds to the edge and
                // past it (e is about 1 on the sides).
                float handover = smoothstep(RimBandLine, RimBandLine + RimBandEdge, t);
                float stripe = smoothstep(0.0, RimBandEdge, t) * (1.0 - handover);

                // The line's width in pixels: its width in e over how much e changes across a pixel, which is a
                // pixel in metres over the smaller of the two half extents the section runs across.
                float3 others = he + axisT * 1e6;
                float narrow = max(min(others.x, min(others.y, others.z)), 1e-6);
                float widthPixels = (1.0 - RimBandInner) * RimBandLine * narrow / max(pixelMetres, 1e-6);
                float fade = smoothstep(RimBandFadeLow, RimBandFadeHigh, widthPixels);

                float luma = dot(body, LumaWeights);
                float3 hue = body / max(luma, 1e-4);
                float3 pale = saturate(lerp(float3(1.0, 1.0, 1.0), hue, RimBandPaleTrace) * (luma * RimBandPaleLift));
                float3 warm = saturate(RimBandWarm * (luma * RimBandWarmLift));

                body = lerp(body, warm, handover * RimBandWarmStrength);
                body = lerp(body, pale, stripe * fade * RimBandPaleStrength);

                return body;
            }
#if defined(_EVO_SKIN_MAP)

            // A colour turned about the grey axis (Rodrigues' rotation of the RGB cube about (1, 1, 1)), so a warm
            // yellow-orange moves round the wheel and its mean stays; the caller sets the luminance back.
            float3 SkinMapTurned(float3 colour, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                float3 k = float3(0.57735027, 0.57735027, 0.57735027);

                return colour * c + cross(k, colour) * s + k * (dot(k, colour) * (1.0 - c));
            }

            // RimBand with the body's own band (EVOSIM_THEATRE_SKIN_MAP, the rim width and rim hue parameters): the band's width
            // is widthScale times RimBandInner's complement, and the warm band's colour is turned by hueTurn radians, then
            // set to luminance one again, so the lift is still its lightness. A scale of one and a turn of zero is RimBand.
            // The pale line keeps its share of the band. The width is at most 1.6 times today's, so the inner edge stays
            // above 0.7; the band is colour only and moves no vertex.
            float3 RimBandMapped(float3 body, float3 positionOS, float meshHalf, float pixelMetres, float widthScale, float hueTurn)
            {
                float inner = 1.0 - (1.0 - RimBandInner) * widthScale;

                float3 he = HalfExtentsWS();
                float3 axisT;
                if (AtMost(he.x, he.y) && AtMost(he.x, he.z)) axisT = float3(1, 0, 0);
                else if (AtMost(he.y, he.z)) axisT = float3(0, 1, 0);
                else axisT = float3(0, 0, 1);

                float3 q = abs(positionOS) * (1.0 - axisT) / max(meshHalf, 1e-4);
                float big = max(max(q.x, q.y), max(q.z, 1e-5));
                float3 r2 = (q / big) * (q / big);
                float3 r4 = r2 * r2;
                float e = big * pow(dot(r4 * r4, float3(1.0, 1.0, 1.0)), 0.125);

                float t = (e - inner) / (1.0 - inner);

                float handover = smoothstep(RimBandLine, RimBandLine + RimBandEdge, t);
                float stripe = smoothstep(0.0, RimBandEdge, t) * (1.0 - handover);

                float3 others = he + axisT * 1e6;
                float narrow = max(min(others.x, min(others.y, others.z)), 1e-6);
                float widthPixels = (1.0 - inner) * RimBandLine * narrow / max(pixelMetres, 1e-6);
                float fade = smoothstep(RimBandFadeLow, RimBandFadeHigh, widthPixels);

                float luma = dot(body, LumaWeights);
                float3 hue = body / max(luma, 1e-4);
                float3 pale = saturate(lerp(float3(1.0, 1.0, 1.0), hue, RimBandPaleTrace) * (luma * RimBandPaleLift));

                float3 warmBase = SkinMapTurned(RimBandWarm, hueTurn);
                warmBase = warmBase / max(dot(warmBase, LumaWeights), 1e-4);
                float3 warm = saturate(warmBase * (luma * RimBandWarmLift));

                body = lerp(body, warm, handover * RimBandWarmStrength);
                body = lerp(body, pale, stripe * fade * RimBandPaleStrength);

                return body;
            }
#endif

            // Chromatophores (_Chromatophores, EVOSIM_THEATRE_CHROMATOPHORES): the skin of an octopus or a cuttlefish
            // is a field of small pigment cells, each a disc that a muscle opens and closes, so the skin is finely
            // speckled, the speckle changes, and waves of darkening ("passing clouds") travel across it. Colour only,
            // on the body's colour before the light, read at the undisplaced object position in the part's own metres
            // (the velvet pores' way), so the cells stay on the tissue as it moves and turns. The palette sets
            // _Chromatophores, the tissue's strength in (0, 1], only while its dial is on; the shader tests it before
            // the call, and the term moves no vertex and no light, so the margin outside the collider is 0.
            //
            // The cells are the velvet pores' search: a jittered grid of ChromaSpacing metres, the 27 cells round the
            // pixel, each cell's point at 0.15 to 0.85 of its cell, taken across the surface alone and weighed by how
            // far off the surface it lies. Each cell is a soft disc, and the discs are summed and clamped, not
            // switched, so no two cells flip over where they meet. Each disc's radius, in cells, breathes on its own
            // phase: r = ChromaRadius (1 + ChromaBreath sin(omega t + phi_i)), phi_i from the cell's hash. The cloud
            // is a plane wave s = smoothstep(sin(k . x - omega_c t)) along a direction from the part's own numbers
            // (_Carve.x, _Individual), with a wavelength of ChromaCloudWavelength of the part's longest size, and it
            // raises the radius of the cells it passes by up to ChromaCloudOpen. Time is EvoTime(), the film's clock,
            // taken as frac(t / period) so a long film loses no precision, and a frame rendered twice is the same
            // frame. Where the spacing falls under a few pixels the term goes to its mean colour (the tissue mixed
            // with the pigment by the discs' mean cover), so a far body shows a uniform, slightly darker tint.
            static const float ChromaSpacing = 0.006;             // metres between grid points; the search needs the
                                                                  // reach below under 1.15 cells
            static const float ChromaRadius = 0.3;                // r0, the disc radius in cells; in (0, 0.45]
            static const float ChromaBreath = 0.4;                // a, the radius's swing as a share of r0; in [0, 1)
            static const float ChromaBreathPeriod = 4.0;          // seconds a disc takes to open and close
            static const float ChromaEdge = 0.35;                 // the disc's soft edge, a share of r each side; in (0, 1)
            static const float ChromaCloudWavelength = 0.5;       // the cloud's wavelength, a share of the part's longest size
            static const float ChromaCloudMinimum = 0.04;         // metres; the wavelength's floor, for a very small part
            static const float ChromaCloudPeriod = 10.0;          // seconds a cloud takes to cross a wavelength; slower
                                                                  // than the breathing
            static const float ChromaCloudLow = -0.3;             // sin where the cloud begins to open the cells
            static const float ChromaCloudHigh = 0.9;             // and where it has opened them fully
            static const float ChromaCloudOpen = 0.5;             // the largest rise of the radius in the cloud, a
                                                                  // share of it; the largest radius,
                                                                  // r0 (1 + a)(1 + open) (1 + edge), stays under 1.15 cells
            static const float ChromaLayer = 0.25;                // cells off the surface where a point begins to fade
                                                                  // (the pores' layer); it is gone at 0.5
            static const float ChromaReach = 1.0;                 // cells beyond which a disc is nothing
            static const float3 ChromaPigment = float3(0.5, 0.28, 0.24);  // the pigment as a multiple of the body's
                                                                  // colour: darker (luminance about 0.3 of it) and
                                                                  // warmer, a brown-maroon that keeps a little red
                                                                  // for the water to take
            static const float ChromaOpacity = 0.85;              // how much of the pigment a full disc shows; in (0, 1]
            // The mean cover of the discs on a surface, for the far fade: the points' mass in the surface layer
            // (3/4 of a cell) times the disc's area, pi r0^2 (1 + a^2/2). A little under the product, for overlap. If
            // the radius or the swing change, change this.
            static const float ChromaMeanCover = 0.22;
            static const float ChromaFadeLow = 3.0;               // spacing in pixels where the discs are gone to the mean
            static const float ChromaFadeHigh = 7.0;              // and where they are whole
            static const float ChromaCloudDark = 0.8;             // how far the cloud's crest takes the tissue toward the
                                                                  // pigment by itself, at any distance; in [0, 1).
                                                                  // 0.45 did not read on a dark body at sheet scale (b41)

            float3 Chromatophores(float3 body, float3 positionOS, float3 normalWS, float strength, float pixelMetres,
                bool skin, float3 restNormal)
            {
                float fade = smoothstep(ChromaFadeLow, ChromaFadeHigh, ChromaSpacing / max(pixelMetres, 1e-6));
                float cover = ChromaMeanCover;

                float3 sizes = PartSizes();
                float3 metres = positionOS * sizes;

                // The cloud: a direction in the part's own axes and a starting phase from the part's numbers,
                // a wavelength from its size, and s in [0, 1] from the plane wave. Worked out at every distance,
                // since it also darkens the tissue by itself (ChromaCloudDark, below).
                float cloudTurn = frac(EvoTime() / ChromaCloudPeriod);
                float3 chosen = EvoHash3(float3(_Carve.x * 61.7 + 3.1, _Individual * 17.3 + 5.9, 11.3));
                float3 direction = chosen * 2.0 - 1.0;
                direction *= rsqrt(max(dot(direction, direction), 1e-4));
                float wavelength = max(ChromaCloudWavelength * max(sizes.x, max(sizes.y, sizes.z)), ChromaCloudMinimum);
                float cloud = smoothstep(ChromaCloudLow, ChromaCloudHigh,
                    sin(6.2831853 * (dot(metres, direction) / wavelength - cloudTurn + chosen.x)));

                [branch] if (fade > 0.001)
                {
                    float3 axisX = UNITY_MATRIX_M._m00_m10_m20 / max(sizes.x, 1e-6);
                    float3 axisY = UNITY_MATRIX_M._m01_m11_m21 / max(sizes.y, 1e-6);
                    float3 axisZ = UNITY_MATRIX_M._m02_m12_m22 / max(sizes.z, 1e-6);
                    float3 geometric = normalize(normalWS);
                    float3 partNormal = float3(dot(geometric, axisX), dot(geometric, axisY), dot(geometric, axisZ));

                    // On a one skin, the plane is the rest frame's: the rest normal, so it does not turn with the body.
                    [branch] if (skin) partNormal = restNormal;

                    // The clock, as a turn of the breathing's period; the phase adds the cell's own below.
                    float breathTurn = frac(EvoTime() / ChromaBreathPeriod);

                    float3 p = metres * (1.0 / ChromaSpacing) +
                               _Individual * float3(19.3, 47.1, 7.7) + float3(31.9, 61.3, 13.7);
                    float3 cellBase = floor(p);
                    float3 cellFrac = p - cellBase;

                    float sum = 0.0;

                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        [unroll]
                        for (int y = -1; y <= 1; y++)
                        {
                            [unroll]
                            for (int z = -1; z <= 1; z++)
                            {
                                float3 offset = float3(x, y, z);
                                float3 corner = cellBase + offset;
                                float3 seed = EvoHash3(corner + float3(4.7, 9.1, 2.3));
                                float3 rel = offset + 0.15 + 0.7 * seed - cellFrac;

                                float off = dot(rel, partNormal);
                                float3 across = rel - partNormal * off;
                                float dist = length(across);
                                float layer = 1.0 - smoothstep(ChromaLayer, 0.5, abs(off));

                                [branch] if (dist < ChromaReach && layer > 0.0)
                                {
                                    float phi = EvoHash1(corner + float3(2.9, 7.1, 4.3));
                                    float radius = ChromaRadius *
                                        (1.0 + ChromaBreath * sin(6.2831853 * (breathTurn + phi))) *
                                        (1.0 + ChromaCloudOpen * cloud);

                                    sum += layer * (1.0 - smoothstep(radius * (1.0 - ChromaEdge), radius * (1.0 + ChromaEdge), dist));
                                }
                            }
                        }
                    }

                    cover = lerp(ChromaMeanCover, saturate(sum), fade);
                }

                float3 pigment = body * ChromaPigment;

                // The cells, then the cloud's own darkening: on film the cells are a pixel or two (6 mm at about a
                // millimetre a pixel at portrait range) and read as almost nothing on a dark body, while the passing
                // cloud is what footage of a cuttlefish shows. b40 measured the cells alone at 0.73/255 on 44560.
                float3 speckled = lerp(body, pigment, saturate(strength * ChromaOpacity * cover));

                return lerp(speckled, pigment, saturate(strength * ChromaCloudDark * cloud));
            }

            // Thin-film iridescence (_Iridescence, EVOSIM_THEATRE_IRIDESCENCE): wet, thin animal tissue (octopus skin,
            // many sea slugs) shows a shifting colour in its highlights as the view changes. IridescentTint is the colour
            // the sheen is multiplied by, in place of white; the sheen keeps its strength and its shape, and nothing else
            // (the body, the rim, the light through the tissue) is touched, so the margin outside the collider is 0. The
            // palette sets _Iridescence, the tissue's strength in (0, 1], only while its dial is on; the fragment tests it
            // before the call and uses the tint on every branch of the sheen chain.
            //
            // The film: a layer of index IridescentIndex and thickness d, seen at theta between the geometric normal and
            // the view (the carved normal's flats would show as steps in the colour). Light enters the film at theta_t,
            // sin(theta_t) = sin(theta) / n, and the two reflected beams differ in phase by
            //     delta(lambda) = 4 pi n d cos(theta_t) / lambda,
            // and the reflected intensity is, to two beams of equal strength,
            //     I(lambda) = 1/2 + 1/2 cos(delta + pi) = 1/2 - 1/2 cos(delta).
            // The pi is the phase flip of the beam off the film's top face with none on the bottom, the soap film's
            // case (Hecht, Optics, thin films; Born and Wolf, 7.6): a film thin against the wavelength reflects nothing,
            // and a quarter wave of optical path reflects most. A wet skin over denser tissue would flip both or neither
            // and swap the colours, which at this strength reads as a hue shift and no more; this is the cheap form, and
            // not Belcour and Barla's full Airy sum. I is read at three wavelengths for red, green and blue, then divided
            // by its mean over the three so the sheen's brightness is kept and only its hue moves.
            //
            // d = IridescentBase + IridescentSwing (2 noise - 1), noise a smooth value noise in [0, 1] of the part's own
            // metres at IridescentScale per cycle, seeded by the part's numbers (_Carve.x, _Individual), so colour drifts
            // across the skin and, since theta changes as the body turns, with the view. The thickest and thinnest films
            // (base +- swing) give the first and second orders of colour.
            //
            // The strength mixes white toward the interference colour by IridescentMix times the tissue's strength. The
            // water takes red away with depth, so the tint reads blue, violet and green; the mix keeps it a shimmer.
            static const float IridescentIndex = 1.33;            // n, the film's refractive index, wet tissue; in (1, 2)
            static const float IridescentBase = 300.0;            // nm, the film's mean thickness; in (0, 1000)
            static const float IridescentSwing = 150.0;           // nm, the thickness's swing either side; in [0, base]
            static const float3 IridescentWavelengths = float3(650.0, 532.0, 450.0);  // nm, red, green, blue
            static const float IridescentScale = 0.06;            // metres of the part per cycle of the thickness noise;
                                                                  // the colour changes over a few of these
            static const float IridescentCorePower = 19.0;        // under the soft tissue's broad sheen (power 9), the
                                                                  // power that leaves the colour to the core a power 28
                                                                  // lobe would light: 28 - 9
            static const float IridescentMix = 0.6;               // the overall share of the interference colour in the
                                                                  // sheen, times the tissue's strength; in [0, 1]
            static const float IridescentMeanFloor = 0.05;        // the least mean the three intensities are divided by,
                                                                  // so a dark film does not blow up a colour

            float3 IridescentTint(float3 positionOS, float3 normalWS, float3 viewWS, float strength)
            {
                float3 metres = positionOS * PartSizes();

                float3 seed = float3(_Carve.x * 29.3 + 7.7, _Individual * 13.1 + 3.3, _Carve.x * 5.9 + _Individual * 2.7);
                float3 gradient;
                float noise = EvoValueNoise(metres * (1.0 / IridescentScale) + seed, gradient);
                float thickness = IridescentBase + IridescentSwing * (2.0 * noise - 1.0);

                float cosTheta = saturate(abs(dot(normalize(normalWS), viewWS)));
                float cosInside = sqrt(max(1.0 - (1.0 - cosTheta * cosTheta) / (IridescentIndex * IridescentIndex), 0.0));

                float3 phase = (4.0 * 3.14159265 * IridescentIndex * thickness * cosInside) / IridescentWavelengths;
                float3 reflected = 0.5 + 0.5 * cos(phase + 3.14159265);

                float3 tint = reflected / max(dot(reflected, float3(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0)), IridescentMeanFloor);

                return lerp(float3(1.0, 1.0, 1.0), tint, saturate(IridescentMix * strength));
            }

            // The skin pattern (_Pattern, EVOSIM_THEATRE_PATTERN): a reaction-diffusion field, solved once per pattern key in
            // C# (TheatrePattern.cs) and kept as one 128 x 128 R8 texture, mapped onto the part as pigment, as the
            // stripes of a nudibranch or the net of a mantle are. Colour only, on the body's colour before the light, read
            // at the undisplaced object position in the part's own metres (the pores' way), so it stays on the tissue as
            // it moves and turns. The palette sets _Pattern only while its dial is on; the shader tests y before the call,
            // and the term moves no vertex and no light, so the margin outside the collider is 0.
            //
            // Triplanar: the field is read on the part's three axis planes, in metres over the tile's repeat length
            // (_Pattern.x, a share of the creature's longest part, the same for all its parts, so the pattern runs across a
            // body at one scale), turned by _Pattern.z quarter-turns and moved by _PatternShift.xy (a creature's own
            // placement on a tile it shares with others), and blended by the uncarved normal, in the part's axes, raised to PatternBlendPower, so a
            // box edge shows no seam. The field v goes through a smoothstep to a cover, and the body is mixed toward its
            // pigment by the cover: the same hue, darker (PatternDarken) and a little more saturated (PatternSaturation)
            // than the tissue, so it reads in the theatre's blue-green water and is not a flash photograph's colours.
            // The mix is the tissue's strength times PatternOpacity. The texture is sampled at an explicit level worked
            // out from the pixel's size (no derivative, so the call is allowed in a branch), and the mip maps are the
            // field's own, so a far pattern averages rather than shimmers; where a pattern cell falls under a few pixels
            // the term goes to the tile's mean cover (_Pattern.w), the colour a far body keeps.
            static const float PatternLow = 0.35;                 // v, as a share of its highest, where the pigment begins;
                                                                  // equals ThresholdLow in TheatrePattern.cs
            static const float PatternHigh = 0.65;                // and where it is whole; equals ThresholdHigh there
            static const float PatternBlendPower = 4.0;           // the triplanar weights' exponent; at least 1, a high
                                                                  // one narrows the blend at an edge
            static const float PatternTexels = 128.0;             // the field's side, TheatrePattern.Side
            static const float PatternCellsPerTile = 12.0;        // pattern cells across one tile, about one wavelength of
                                                                  // the solved field (8 to 10 texels of 128, so 13 to 16;
                                                                  // taken low so the fade begins a little late)
            static const float PatternFadeLow = 2.0;              // cell size in pixels where the pattern is gone to its mean
            static const float PatternFadeHigh = 5.0;             // and where it is whole
            static const float PatternDarken = 0.42;              // the pigment's luminance as a multiple of the body's
            static const float PatternSaturation = 1.35;          // the pigment's saturation as a multiple of the body's;
                                                                  // at least 1
            static const float PatternOpacity = 0.9;              // how much of the pigment a full cover shows; in (0, 1]

            // A 2D point turned by a whole number of quarter-turns, 0 to 3.
            float2 PatternTurn(float2 p, float turns)
            {
                [branch] if (turns > 2.5) return float2(p.y, -p.x);
                [branch] if (turns > 1.5) return -p;
                [branch] if (turns > 0.5) return float2(-p.y, p.x);
                return p;
            }

#if defined(_EVO_SKIN_JOIN)
            // ---------------------------------------------------------------- SKIN_JOIN
            //
            // EVOSIM_THEATRE_SKIN_JOIN, on the one skin at the rig levels: one pattern over the whole skin, and the joint's
            // crease a soft fold. Compiled only under _EVO_SKIN_JOIN, a local keyword TheatreOneSkin turns on in the skin's
            // own material copy, and only while the dial is on, so a part and a frame with the dial off are as they were.
            // Colour and normal only: nothing moves, and nothing is drawn outside a collider (D242).
            //
            // The skin reads its pattern in one frame, the rest frame (UV5, UV6), at one tile, one strength, one offset and
            // one turn for the body (the largest part's block), so nothing about the pattern changes at a joint but the
            // surface's turn against the triplanar read's three planes. A box's faces lie on the planes and read one plane at
            // the tile's own scale; a round limb's side turns between two planes, where the plain blend averages two
            // unrelated fields and so pulls the field under PatternLow: the side goes plain, while the limb's end and the
            // fillet at its root, which face a plane, stay patterned. JoinCover thresholds each plane's field first and
            // blends the covers about the tile's mean cover (_Pattern.w), with the weights' sum renormalised by the square
            // root of their squares, so the mean cover is kept and nearly all the contrast; and the weights are the rest
            // normal to JoinBlendPower, so the zone where two planes share a pixel is narrow. One plane at full weight is
            // the cover PatternSkin reads. The frame is not changed per bone: one frame has no seam to blend.
            //
            // The pigment is worked out from the body's colour (_BaseColor, the largest part's painted colour) and not the
            // pixel's, by JoinToneShare, so the pattern is one pigment over parts of different colour.
            //
            // SKIN2's crease (the vertex's marks z and w) keeps JoinCreaseKeep of its darkening and of its groove's depth.
            static const float JoinBlendPower = 8.0;              // the skin's triplanar exponent; PatternBlendPower is 4.
                                                                  // At 8 the lesser of two planes has over a fifth of the
                                                                  // weight within 5 degrees of their diagonal (10 at 4);
                                                                  // in [4, 16]
            static const float JoinPlaneFloor = 0.01;             // a plane under this weight is not read, as in PatternSkin
            static const float JoinToneShare = 1.0;               // the pigment's reference: 0 the pixel's colour, 1 the
                                                                  // body's (_BaseColor); in [0, 1]
            static const float JoinCreaseKeep = 0.35;             // the share of the crease's darkening and groove depth
                                                                  // kept, a gentle fold; in [0, 1]

            // The pattern's cover on the skin: each plane's field thresholded (PatternLow, PatternHigh), and the covers
            // blended about the tile's mean cover with the weights renormalised by the root of their squares, clamped.
            float JoinCover(float3 metres, float3 normal, float tile, float turns, float2 shift, float level, float meanCover)
            {
                float3 weights = pow(abs(normal), JoinBlendPower);
                weights /= max(weights.x + weights.y + weights.z, 1e-6);
#if defined(_EVO_JOIN_DEBUG)
                JoinDebugPlanes = weights;
#endif

                float deviation = 0.0;
                float squares = 0.0;

                [branch] if (weights.x > JoinPlaneFloor)
                {
                    float coverX = smoothstep(PatternLow, PatternHigh,
                        SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.yz / tile, turns) + shift, level).r);
                    deviation += weights.x * (coverX - meanCover);
                    squares += weights.x * weights.x;
                }
                [branch] if (weights.y > JoinPlaneFloor)
                {
                    float coverY = smoothstep(PatternLow, PatternHigh,
                        SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xz / tile, turns) + shift, level).r);
                    deviation += weights.y * (coverY - meanCover);
                    squares += weights.y * weights.y;
                }
                [branch] if (weights.z > JoinPlaneFloor)
                {
                    float coverZ = smoothstep(PatternLow, PatternHigh,
                        SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xy / tile, turns) + shift, level).r);
                    deviation += weights.z * (coverZ - meanCover);
                    squares += weights.z * weights.z;
                }

                return saturate(meanCover + deviation * rsqrt(max(squares, 1e-6)));
            }

#endif
            float3 PatternSkin(float3 body, float3 positionOS, float3 normalWS, float4 pattern, float2 shift, float pixelMetres,
                bool skin, float3 restNormal)
            {
                float tile = max(pattern.x, 1e-4);
                float cellPixels = (tile / PatternCellsPerTile) / max(pixelMetres, 1e-6);
                float fade = smoothstep(PatternFadeLow, PatternFadeHigh, cellPixels);
                float cover = pattern.w;

                [branch] if (fade > 0.001)
                {
                    float3 sizes = PartSizes();
                    float3 axisX = UNITY_MATRIX_M._m00_m10_m20 / max(sizes.x, 1e-6);
                    float3 axisY = UNITY_MATRIX_M._m01_m11_m21 / max(sizes.y, 1e-6);
                    float3 axisZ = UNITY_MATRIX_M._m02_m12_m22 / max(sizes.z, 1e-6);
                    float3 geometric = normalize(normalWS);
                    float3 partNormal = float3(dot(geometric, axisX), dot(geometric, axisY), dot(geometric, axisZ));

                    // On a one skin, the plane is the rest frame's: the rest normal, so it does not turn with the body.
                    [branch] if (skin) partNormal = restNormal;

                    float3 weights = pow(abs(partNormal), PatternBlendPower);
                    weights /= max(weights.x + weights.y + weights.z, 1e-6);

                    float3 metres = positionOS * sizes;
                    float level = max(0.0, log2(max(pixelMetres, 1e-6) / tile * PatternTexels));

#if defined(_EVO_SKIN_JOIN)
                    // SKIN_JOIN: the skin's cover by JoinCover, and the three reads below left unmade.
                    float joinCover = 0.0;
                    [branch] if (skin)
                    {
                        joinCover = JoinCover(metres, partNormal, tile, pattern.z, shift, level, pattern.w);
#if defined(_EVO_JOIN_DEBUG)
                        JoinDebugCover = joinCover;
#endif
                        weights = float3(0.0, 0.0, 0.0);
                    }
#endif
                    float field = 0.0;

                    [branch] if (weights.x > 0.01)
                        field += weights.x * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.yz / tile, pattern.z) + shift, level).r;
                    [branch] if (weights.y > 0.01)
                        field += weights.y * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xz / tile, pattern.z) + shift, level).r;
                    [branch] if (weights.z > 0.01)
                        field += weights.z * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xy / tile, pattern.z) + shift, level).r;

                    cover = lerp(pattern.w, smoothstep(PatternLow, PatternHigh, field), fade);
#if defined(_EVO_SKIN_JOIN)
                    [branch] if (skin) cover = lerp(pattern.w, joinCover, fade);
#endif
                }

                float luma = dot(body, float3(0.2126, 0.7152, 0.0722));
                float3 pigment = lerp(float3(luma, luma, luma), body, PatternSaturation) * PatternDarken;
                pigment = lerp(pigment, _PatternTint.rgb, _PatternTint.a);
#if defined(_EVO_SKIN_JOIN)
                // SKIN_JOIN: the skin's pigment from the body's colour.
                [branch] if (skin)
                {
                    float joinLuma = dot(_BaseColor.rgb, float3(0.2126, 0.7152, 0.0722));
                    float3 joinPigment = lerp(float3(joinLuma, joinLuma, joinLuma), _BaseColor.rgb, PatternSaturation) * PatternDarken;
                    joinPigment = lerp(joinPigment, _PatternTint.rgb, _PatternTint.a);
                    pigment = lerp(pigment, joinPigment, JoinToneShare);
                }
#endif

                return lerp(body, saturate(pigment), saturate(pattern.y * PatternOpacity * cover));
            }

            // The board's light (_BoardLight, EVOSIM_THEATRE_BOARD_LIGHT): a measurement, not a look. Every
            // term that names it reaches a pixel only behind a test of it, and the palette never sets it with
            // its dial off. A light's colour under it is the white of the same luminance, so the key and the
            // fill keep their strength and lose their cold tint, and the ambient keeps its strength and loses
            // the water's teal.
            float3 BoardWhite(float3 colour)
            {
                return dot(colour, LumaWeights).xxx;
            }

            // The half chord through the part's own ellipsoid along a world direction, in metres: how
            // much tissue a ray through its middle crosses. One over the length of d / h, with d the
            // direction in the part's axes and h its half extents, so a plate seen face on is thin and
            // seen edge on is thick.
            float HalfChord(float3 directionWS, float3 he)
            {
                float3 d = float3(
                    dot(directionWS, normalize(UNITY_MATRIX_M._m00_m10_m20)),
                    dot(directionWS, normalize(UNITY_MATRIX_M._m01_m11_m21)),
                    dot(directionWS, normalize(UNITY_MATRIX_M._m02_m12_m22)));

                float3 q = d / max(he, 1e-4);
                return rsqrt(max(1e-8, dot(q, q)));
            }

            // How translucent a part is along the view and the light together, 0 to 1: the tissue's
            // translucent half extent over the mean of the two half chords. It grows as the part
            // thins along either.
            float SssThinness(float3 v, float3 l)
            {
                float3 he = HalfExtentsWS();
                float path = 0.5 * (HalfChord(v, he) + HalfChord(l, he));

                return saturate(SssReach * _TransMetres / max(1e-4, path));
            }

            float3 SssTint(float3 tint, float thinness)
            {
                float3 deep = pow(max(tint, 1e-4), lerp(SssTintThick, SssTintThin, thinness));
                float before = max(tint.r, max(tint.g, tint.b));
                float after = max(1e-4, max(deep.r, max(deep.g, deep.b)));

                return deep * (before / after) * SssTransGain;
            }

            // 2. Light through a living leaf (_LeafLook, EVOSIM_THEATRE_LEAF), on a blade (leaf.w) alone. When the
            // key is behind the blade as the camera sees it (the key against the face the camera sees: minus
            // dot(blade normal, key direction), the normal before the carve so no facet moves it) the blade
            // glows with its own colour, its tissue's light-through tint pushed warm to a yellow-green, as kelp
            // and sea lettuce do against the sky. It follows the key's light and the depth's (keyColour), the
            // tissue's transmission (through) and the lens's thinness there (thickness, which the vertex stage
            // raises as the lens thins, so the rim glows most and the midrib, whose veins take thickness down,
            // least), to the power LeafBacklitFalloff. Light only: nothing moves.
            static const float LeafBackLow = 0.15;               // -dot(n, key) where it begins; in [0, 1)
            static const float LeafBackHigh = 0.85;              // and where it is whole; in (Low, 1]
            static const float LeafBacklitGain = 0.9;            // strength, against the transmission's own 1.3
            static const float LeafBacklitFalloff = 1.5;         // power on the thinness, 0 to 1; higher: rim only
            static const float3 LeafBacklitTint = float3(1.15, 1.05, 0.62);   // warm shift of the tissue's tint

            // The marine producers' surface (_Alga, EVOSIM_THEATRE_ALGA), on a blade (leaf.w) alone. Light only.
            // The wet sheen: a broad, soft highlight off the key, AlgaWetGain at the power AlgaWetPower (the body's
            // is 28 at _Sheen, a leaf's _Sheen near nothing), added over the sheen the tissue already has, so a sheet
            // and a strap read as wet seaweed and not as matte paper or plastic. The light through: the transmission's
            // strength on a blade times AlgaThroughSheet on a sheet and AlgaThroughStrap on a strap (by the form),
            // every transmitted term at once, the leaf dial's backlit glow among them, each still by the lens's
            // thinness at the pixel. Both take _Alga's share, so a corpse keeps AlgaCorpse's. On a thin sheet (not a
            // round fan, not a strap) a second light through, AlgaSeeGain of the first, in the blade's own colour and
            // never above the rim's brightest (the fragment's seeLight, after the wet sheen).
            static const float AlgaWetGain = 0.22;
            static const float AlgaWetPower = 9.0;
            static const float AlgaThroughSheet = 1.45;
            static const float AlgaThroughStrap = 0.9;
            static const float AlgaSeeGain = 1.0;   // the extra light through a thin sheet, in the blade's own colour, of the above

            // The depth's tint on a producer's colour (_AlgaDeep, _AlgaShallow; the palette's AlgaDepthSwing): the
            // body, the rim and the light through times the multiplier at the waterline, one half way down the
            // water column, and the multiplier at the floor, piecewise linearly by the visual's centre's depth over
            // the column, so a blade is one colour. The column is the world's water box, from its waterline
            // (_EvoWaterHigh.y) to its floor, which TheatreSkin.Dress pushes AlgaFloorMargin under the box's floor
            // (_EvoWaterLow.y). Before a skin has pushed it (_EvoWaterLow.w zero) the share is a half, the palette's
            // own colour. The palette sends the same factor on all three channels, a darkening with depth (under one at
            // the floor, one at the waterline), so the depth never moves a producer's hue.
            static const float AlgaFloorMargin = 2.0;

            // The share of the way from the waterline to the floor a visual's centre is at, or a half.
            float AlgaDepthShare()
            {
                if (_EvoWaterLow.w < 0.5) return 0.5;

                float top = _EvoWaterHigh.y;
                float floorY = _EvoWaterLow.y + AlgaFloorMargin;
                return saturate((top - UNITY_MATRIX_M._m13) / max(0.01, top - floorY));
            }

            // The colour multiplier at a share of the column: the shallow end's at 0, one at a half, the deep end's at 1.
            float3 AlgaDepthTint()
            {
                float share = AlgaDepthShare();
                return share < 0.5
                    ? lerp(_AlgaShallow.rgb, float3(1.0, 1.0, 1.0), 2.0 * share)
                    : lerp(float3(1.0, 1.0, 1.0), _AlgaDeep.rgb, 2.0 * share - 1.0);
            }
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)

#if defined(_EVO_ALGA_RAMP_PHOTO)
            // EVOSIM_THEATRE_ALGA_RAMP_PHOTO (the keyword _EVO_ALGA_RAMP_PHOTO, nested in level 2's, which TheatreSkin enables only
            // while that dial is above 0): the ramp's sea-lettuce and mid-green stops are moved toward photographed sea lettuce, by
            // the dial's share (the global _EvoAlgaRampPhoto, 0 to 1), in linear colour. Each photo stop has its stop's own luminance
            // (0.4669 and 0.1876, the mid green's AlgaBladeRampMidLuma) at the photographs' chromaticity, so the lightness term is
            // unchanged; kelp (AlgaBladeRampC) is unchanged.
            static const float3 AlgaBladeRampPhotoA = float3(0.4185, 0.5134, 0.1481);   // sea lettuce
            static const float3 AlgaBladeRampPhotoB = float3(0.1682, 0.2063, 0.0595);   // mid green
            float _EvoAlgaRampPhoto;
            // The two photo stops and the share, each tunable from the environment (EVOSIM_THEATRE_ALGA_RAMP_STOP_A, _STOP_B and
            // _SHARE; TheatreSkin.EnsureAlgaRampStops sets them). w is 1 when the value is set, and then xyz (x for the share)
            // replaces the constant; w is 0 when it is not, and then the constant stands, its own lines untouched.
            float4 _EvoAlgaRampStopA;
            float4 _EvoAlgaRampStopB;
            float4 _EvoAlgaRampShare;

#endif
            // The colour a blade takes at level 2, as a multiplier on its light: from the blade's own colour (body, the ramp
            // and tint it already has) to the plant's place on the ramp. The hue is moved by the ratio of the two colours at
            // equal luminance, bounded; the lightness follows the ramp's luminance over the mid green's at the exponent
            // AlgaBladeLumaShare; the whole is taken AlgaBladeRampShare of the way, and the blade's own variation is on top.
            float3 AlgaBladeColour(float3 body, float4 leaf)
            {
                float x = saturate(lerp(AlgaBladePlant(63.0), AlgaDepthShare(), AlgaBladeRampDepth));
                float3 target = AlgaBladeRamp(x);
#if defined(_EVO_ALGA_RAMP_PHOTO)
                // The ramp's stops moved toward the photographs' by the dial's share, then the lookup again; level 3's adjustment
                // below takes this target in its turn.
                float3 photoA = lerp(AlgaBladeRampA, AlgaBladeRampPhotoA, _EvoAlgaRampPhoto);
                float3 photoB = lerp(AlgaBladeRampB, AlgaBladeRampPhotoB, _EvoAlgaRampPhoto);
                if (_EvoAlgaRampStopA.w > 0.5) photoA = lerp(AlgaBladeRampA, _EvoAlgaRampStopA.xyz, _EvoAlgaRampPhoto);
                if (_EvoAlgaRampStopB.w > 0.5) photoB = lerp(AlgaBladeRampB, _EvoAlgaRampStopB.xyz, _EvoAlgaRampPhoto);
                target = x < 0.5 ? lerp(photoA, photoB, 2.0 * x) : lerp(photoB, AlgaBladeRampC, 2.0 * x - 1.0);
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                target = lerp(target, AlgaBlade3Ulva * (max(dot(target, LumaWeights), 1e-4) / dot(AlgaBlade3Ulva, LumaWeights)),
                    AlgaBlade3UlvaShare) * AlgaBlade3Value;
#endif

                float targetLuma = max(dot(target, LumaWeights), 1e-4);
                float bodyLuma = max(dot(body, LumaWeights), 1e-4);
                float3 ratio = clamp((target / targetLuma) / max(body / bodyLuma, 1e-3), 0.2, 5.0);
                float lightness = pow(targetLuma / AlgaBladeRampMidLuma, AlgaBladeLumaShare);
#if defined(_EVO_ALGA_RAMP_PHOTO)
                // A share set from the environment replaces AlgaBladeRampShare; unset, the return below runs as it always did.
                if (_EvoAlgaRampShare.w > 0.5)
                    return lerp(float3(1.0, 1.0, 1.0), ratio * lightness, _EvoAlgaRampShare.x) * AlgaBladeWithin(leaf);
#endif

                return lerp(float3(1.0, 1.0, 1.0), ratio * lightness, AlgaBladeRampShare) * AlgaBladeWithin(leaf);
            }

#if defined(_EVO_ALGA_ROUND_PHOTO)
#if defined(_EVO_ALGA_RAMP_PHOTO)
            // EVOSIM_THEATRE_ALGA_ROUND_PHOTO (the keyword _EVO_ALGA_ROUND_PHOTO, nested in the photographed ramp's, which TheatreSkin
            // enables only while that dial is above 0 and this one is): the round producers (a photosynthetic sphere, capsule or
            // superellipsoid, which TheatrePalette.DrawnAsLeaf keeps off the blade path) take the multiplier a blade gets at the
            // ramp's mid-green stop, taken the global's share of the way from one. The same arithmetic as AlgaBladeColour: the hue by
            // the ratio of the target to the body's colour at equal luminance, bounded; the lightness by the target's luminance over
            // the mid green's at the exponent AlgaBladeLumaShare; the whole taken the ramp's share of the way (_EvoAlgaRampShare when
            // set, else AlgaBladeRampShare). The target is the mid-green photo stop: _EvoAlgaRampStopB when set, else the ramp's
            // mid green moved toward AlgaBladeRampPhotoB by _EvoAlgaRampPhoto. None of a blade's own inputs (leaf, AlgaBladeWithin,
            // the plant's place on the ramp) is read, so every round part of a colour takes the same multiplier.
            float _EvoAlgaRoundPhoto;

            float3 AlgaRoundPhotoTone(float3 body)
            {
                float3 target = lerp(AlgaBladeRampB, AlgaBladeRampPhotoB, _EvoAlgaRampPhoto);
                if (_EvoAlgaRampStopB.w > 0.5) target = _EvoAlgaRampStopB.xyz;

                float targetLuma = max(dot(target, LumaWeights), 1e-4);
                float bodyLuma = max(dot(body, LumaWeights), 1e-4);
                float3 ratio = clamp((target / targetLuma) / max(body / bodyLuma, 1e-3), 0.2, 5.0);
                float lightness = pow(targetLuma / AlgaBladeRampMidLuma, AlgaBladeLumaShare);
                float share = _EvoAlgaRampShare.w > 0.5 ? _EvoAlgaRampShare.x : AlgaBladeRampShare;
                float3 bladeTone = lerp(float3(1.0, 1.0, 1.0), ratio * lightness, share);

                return lerp(float3(1.0, 1.0, 1.0), bladeTone, _EvoAlgaRoundPhoto);
            }
#endif
#endif

#if defined(_EVO_ALGA_ROUND_TARGET)
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2) && defined(_EVO_ALGA_RAMP_PHOTO)
            // EVOSIM_THEATRE_ALGA_ROUND_TARGET (the keyword _EVO_ALGA_ROUND_TARGET, nested in the photographed ramp's, which
            // TheatreSkin enables only while that dial is above 0 and this one is): a round producer part (see ROUND_PHOTO) takes
            // the colour multiplier a blade gets at the ramp's mid-green photo stop on its whole light, as a blade does: it goes
            // into leafTone, which multiplies the light at the end, after the key, the ambient, the rim, the glow and the light
            // through. A tone on the body, the rim's colour or the light through each alone is re-tinted by the water's teal key
            // and ambient and moves the colour on screen hardly at all. The same arithmetic as AlgaBladeColour: the hue by the
            // ratio of the target to the body's colour at equal luminance, bounded as it is; the lightness by the target's
            // luminance over the mid green's at the exponent AlgaBladeLumaShare; the whole taken the ramp's share of the way
            // (_EvoAlgaRampShare when set, else AlgaBladeRampShare). The target is the one ROUND_PHOTO reads: _EvoAlgaRampStopB
            // when set, else the ramp's mid green moved toward AlgaBladeRampPhotoB by _EvoAlgaRampPhoto, so a stop that comes
            // from the world is followed. None of a blade's own inputs (leaf, AlgaBladeWithin, the plant's place on the ramp)
            // is read. The round producers' own stop, EVOSIM_THEATRE_ALGA_ROUND_STOP, is preferred when it is set, so the blades'
            // stops can move per world without moving the round producers' target. The multiplier is taken _EvoAlgaRoundTarget
            // of the way from one at the call.
            float _EvoAlgaRoundTarget;
            // The round producers' own target stop: w is 1 when it is set, and xyz is then the stop in linear colour; unset it is
            // zero and the target above (the blades' stop, else the ramp's) stands.
            float4 _EvoAlgaRoundStop;

            float3 AlgaRoundTargetTone(float3 body)
            {
                float3 target = lerp(AlgaBladeRampB, AlgaBladeRampPhotoB, _EvoAlgaRampPhoto);
                if (_EvoAlgaRampStopB.w > 0.5) target = _EvoAlgaRampStopB.xyz;
                if (_EvoAlgaRoundStop.w > 0.5) target = _EvoAlgaRoundStop.xyz;

                float targetLuma = max(dot(target, LumaWeights), 1e-4);
                float bodyLuma = max(dot(body, LumaWeights), 1e-4);
                float3 ratio = clamp((target / targetLuma) / max(body / bodyLuma, 1e-3), 0.2, 5.0);
                float lightness = pow(targetLuma / AlgaBladeRampMidLuma, AlgaBladeLumaShare);
                float share = _EvoAlgaRampShare.w > 0.5 ? _EvoAlgaRampShare.x : AlgaBladeRampShare;

                return lerp(float3(1.0, 1.0, 1.0), ratio * lightness, share);
            }
#endif
#endif

            // The margin's finer ruffle as a bend of the shading normal n (geo is the interpolated geometric normal). The
            // height is a wave round the outline (the vertex stage's perimeter coordinate q: up one margin and down the other),
            // growing from nothing at 1 - AlgaBladeShadeRim of the way out to its full amplitude at the rim, faded in at the
            // stalk as the vertex ruffle is, with the plant's hash for its count and its phase. Its amplitude is set in
            // metres so the slope along the margin is at most AlgaBladeShadeTilt whatever the blade's size: the wavelength is
            // the outline's length (about four half lengths) over the waves. The height's gradient on the screen comes from
            // the leaf coordinates' own derivatives (ds, dt: those of s and of the distance across) by the chain rule, and the
            // normal from the surface gradient (Mikkelsen's unparametrised bump mapping) over the world position's
            // derivatives (dpx, dpy). Faded out as the waves near one cycle each two pixels.
            float3 AlgaBladeRuffleNormal(float3 n, float3 geo, float4 leaf, float2 ds, float2 dt, float3 dpx, float3 dpy)
            {
                float s = leaf.x;
                float t = leaf.z;
                float side = t >= 0.0 ? 1.0 : -1.0;

                float u = saturate((abs(t) - (1.0 - AlgaBladeShadeRim)) / AlgaBladeShadeRim);
                float rim = u * u * (3.0 - 2.0 * u);
                float rimSlope = 6.0 * u * (1.0 - u) / AlgaBladeShadeRim;
                float stalk = smoothstep(0.05, 0.3, s);

                float waves = lerp(AlgaBladeShadeWavesLeast, AlgaBladeShadeWavesMost, AlgaBladePlant(70.0));
                float phase = AlgaBladePlant(71.0);
                float q = t >= 0.0 ? 0.5 * s : 1.0 - 0.5 * s;
                float theta = 6.2831853 * (waves * q + phase);

                float3 he = HalfExtentsWS();
                float3 axisL, axisT, axisW;
                LeafAxes(he, axisL, axisT, axisW);

                float height = AlgaBladeShadeTilt * 4.0 * dot(he, abs(axisL)) / (6.2831853 * waves);
                float fade = 1.0 - smoothstep(AlgaBladeShadeFadeLow, AlgaBladeShadeFadeHigh, waves * 0.5 * (abs(ds.x) + abs(ds.y)));
                float amp = height * stalk * fade;

                float dhds = amp * rim * cos(theta) * 6.2831853 * waves * (0.5 * side);
                float dhdt = amp * rimSlope * side * sin(theta);
                float2 dh = dhds * ds + dhdt * dt;

                float3 r1 = cross(dpy, geo);
                float3 r2 = cross(geo, dpx);
                float det = dot(dpx, r1);
                float3 grad = sign(det) * (dh.x * r1 + dh.y * r2);

                return normalize(max(abs(det), 1e-12) * n - grad);
            }
#endif

            // 4. Level 2 of the dial (_LeafLook at 2): kelp relief. On a long blade, rows of shallow blisters
            // (bullations) either side of the midrib, as Laminaria and Macrocystis carry, in the normal alone: no
            // vertex moves, so nothing can leave the collider. A blade counts as kelp when its length over its
            // width (hL / hW) is over LeafKelpRatio, by a ramp LeafKelpRamp wide, so a rounded sea-lettuce sheet is
            // left plain. The blisters repeat along the blade every LeafBullSpacing of its length (s), in whole
            // cells from LeafBullFrom to LeafBullTo (so the first and last are whole), and in one row a side,
            // across from LeafBullIn to LeafBullOut of the way to the rim (|t|), clear of the midrib and gone
            // before the rim. A blister is a squared sine in each direction, fading toward the rim by
            // LeafBullTaper over the row. Its height is set so the tilt along the blade is at most
            // LeafBullRelief (rise over run); across it the tilt keeps the same height, so it is at most
            // LeafBullRelief times the blister's length over its breadth, and that ratio is held to LeafBullAcross.
            // The tilt is bounded as the papillae's is. The relief is a blister on the upper face and a hollow
            // on the lower, as a puckered sheet is. It fades out as the spacing falls from LeafBullFadeHigh to
            // LeafBullFadeLow pixels.
            static const float LeafKelpRatio = 2.5;             // length over width where it begins; at least 1
            static const float LeafKelpRamp = 1.0;              // and how far on it is whole; above 0
            static const float LeafBullSpacing = 0.12;          // a blister's length along the blade, in s; in (0, 0.5)
            static const float LeafBullFrom = 0.12;             // s where the first cell may start; in [0, 1)
            static const float LeafBullTo = 0.92;               // s where the last cell must end; in (From, 1]
            static const float LeafBullIn = 0.14;               // |t| where a row begins; in (0, Out)
            static const float LeafBullOut = 0.86;              // |t| where it ends; in (In, 1)
            static const float LeafBullTaper = 0.5;             // share of the height lost across the row to the rim; in [0, 1)
            static const float LeafBullRelief = 0.3;            // the steepest tilt along the blade; below the papillae's bound
            static const float LeafBullAcross = 2.0;            // the most the tilt across may exceed the tilt along; at least 0
            static const float LeafBullFadeLow = 4.0;           // spacing in pixels where they are gone
            static const float LeafBullFadeHigh = 10.0;         // and where they are whole

            // ---------------------------------------------------------------- velvet absorbers
            //
            // An absorber part under the velvet dial (_Velvet, EVOSIM_THEATRE_VELVET), as a sponge or a colonial
            // tunicate is photographed. Light and shading only, no vertex moves for any of it, so nothing can
            // leave the collider. Three terms, each behind a test of _Velvet, which the palette sets only on an
            // absorptive part and only while its dial is on.
            //
            // 1. A matte skin. The sheen's gain is this share of the body's and its exponent this, a broad dull
            // lobe (the body's is 28); the rim's power is this share of the material's (under one: wider, softer),
            // and at grazing angles (facing = 1 - n.v over VelvetRimLow) the rim is up to this many times its
            // strength, as a velvet's pile catches light along its edge.
            static const float VelvetSheenGain = 0.3;            // share of the body's sheen; in [0, 1]
            static const float VelvetSheenPower = 5.0;           // the sheen's exponent; the body's is 28
            static const float VelvetRimPower = 0.7;             // share of the rim's power; in (0, 1]
            static const float VelvetRimGain = 1.6;              // the rim's strength at grazing angles; at least 1
            static const float VelvetRimLow = 0.55;              // facing where it begins to rise; in [0, 1)

            // 2. Pores: small round dimples, each with a slightly paler lip, one to a cell of a jittered grid
            // in the part's own metres (PapillaField's way: a feature point in 0.15 to 0.85 of each cell). Their
            // spacing and size are in metres. The grid is three dimensional and a surface crosses it, so each
            // point counts by how far it lies off the surface along the normal (all of it within VelvetPoreLayer
            // of a cell off, none at half a cell, smoothly), which puts about one pore to a cell of the surface,
            // and a point is measured by its distance across the surface alone. A pore's profile in radii u: a
            // dimple (1 - u^2)^2 inside u = 1, deep VelvetPoreDepth of a radius, and a lip, a gaussian bump on
            // u = 1 of height VelvetPoreLip and width VelvetPoreLipWidth, gone by VelvetPoreReach. The normal is
            // tilted by the profile's slope (bounded as the papillae's is), the body darkens by VelvetPoreDark in
            // the dimple, and lifts toward its own paler tone by VelvetLipLift on the lip. They fade out as the
            // spacing falls from VelvetPoreFadeHigh to VelvetPoreFadeLow pixels, so a far part is plain.
            static const float VelvetPoreSpacing = 0.015;        // metres between pores
            static const float VelvetPoreDiameter = 0.004;       // metres across the dimple (u = 1 is its edge)
            static const float VelvetPoreLayer = 0.25;           // cells off the surface where a point begins to fade
            static const float VelvetPoreDepth = 0.6;            // the dimple's depth, in radii
            static const float VelvetPoreLip = 0.25;             // the lip's height, in radii
            static const float VelvetPoreLipWidth = 0.25;        // the lip's width, in radii
            static const float VelvetPoreReach = 1.6;            // radii beyond which a pore is nothing
            static const float VelvetPoreDark = 0.55;            // share of the body's light lost at a dimple's middle
            static const float VelvetLipGain = 1.5;              // the pale tone the lip lifts toward, times the body
            static const float VelvetLipLift = 0.5;              // how far toward it, at the lip's crest
            static const float VelvetPoreFadeLow = 5.0;          // spacing in pixels where they are gone
            static const float VelvetPoreFadeHigh = 12.0;        // and where they are whole

            // 3. One osculum on the part, at the far end of its long axis from the joint (the palette's
            // _VelvetOsculum: the centre in object units, the radius in metres, from VelvetOsculumShare of the
            // smallest half extent). In radii u from the centre: a hole inside VelvetOsculumHoleLow, its wall
            // a smoothstep out to VelvetOsculumHoleHigh, and a raised pale lip, a gaussian bump on
            // VelvetOsculumLipAt of width VelvetOsculumLipWidth, gone by VelvetOsculumReach. All of it is normal
            // and colour: the hole's light is cut by VelvetOsculumDark, the normal is tilted by the profile's slope
            // (depth and lip heights in radii), the lip lifts toward its pale tone, and the pores keep away from
            // it. The hole is not cut in geometry: the carve's bound is a share of the lens or of the smallest
            // half extent, and a hole as wide as an osculum would take more of it. It fades out as the radius
            // falls from VelvetOsculumFadeHigh to VelvetOsculumFadeLow pixels.
            static const float VelvetOsculumHoleLow = 0.55;
            static const float VelvetOsculumHoleHigh = 0.8;
            static const float VelvetOsculumLipAt = 0.95;
            static const float VelvetOsculumLipWidth = 0.2;
            static const float VelvetOsculumReach = 1.7;
            static const float VelvetOsculumDepth = 0.5;         // the wall's height, in radii
            static const float VelvetOsculumLip = 0.2;           // the lip's height, in radii
            static const float VelvetOsculumDark = 0.88;         // share of the light lost inside the hole
            static const float VelvetOsculumLipLift = 0.6;       // the lip's lift toward VelvetLipGain, at its crest
            static const float VelvetOsculumFadeLow = 3.0;       // radius in pixels where it is gone
            static const float VelvetOsculumFadeHigh = 8.0;      // and where it is whole

            // 4. Level 2 of the dial (_Velvet at 2): star systems, as a colonial tunicate (Botryllus) carries
            // them, in place of the pores. Colour alone, no vertex or normal moves. The centres sit on a jittered
            // hexagonal lattice in the part's own metres, VelvetStarSpacing apart, in the plane of the two axes
            // the surface faces least (three planes, each weighed by the geometric normal's component along its
            // axis to the power VelvetStarPlanePower, so a flat face is one plane whole; a plane under
            // VelvetStarPlaneSkip is left out, so a face pays for one 3 by 3 search and an edge for two). A
            // centre moves by at most VelvetStarJitter of the spacing on each axis, so neighbours stay at least
            // (1 - 2 sqrt(2) jitter) of the spacing apart. A system is a disc of VelvetStarRadius of the
            // spacing, and it stays whole inside its own Voronoi cell while 1.05 radius is under half that
            // least gap (0.41 under 0.415 at these values: systems nearly touch, as a colony's do), so no two systems overlap and the nearest centre
            // alone is read. Round the centre a rosette of 6
            // to 12 zooids (VelvetStarZooidsLow, Span, from the centre's hash), each an oval in polar
            // coordinates pointing at the centre, from VelvetStarInner to VelvetStarOuter of the radius, as wide as
            // VelvetStarFill of its sector at its middle, so neighbours do not touch. At the centre a dark pore of
            // VelvetStarPoreRadius of the radius. The zooids lift toward a paler tone (VelvetStarPaleGain, each
            // system's own within VelvetStarToneSpread), the rest of the system, its surround, loses
            // VelvetStarSurroundDark of the light, and a faint line (VelvetStarLineDark, VelvetStarLineWidth of
            // the spacing wide in F2 - F1) runs along the edge between neighbours. They fade out as the spacing
            // falls from VelvetStarFadeHigh to VelvetStarFadeLow pixels, and keep away from the osculum as the
            // pores do.
            static const float VelvetStarSpacing = 0.025;        // metres between centres
            static const float VelvetStarJitter = 0.06;          // share of the spacing a centre may move on each axis; in [0, 0.17)
            static const float VelvetStarRadius = 0.39;          // a system's radius, in spacings; 1.05 times it under 0.5 (1 - 2.83 jitter)
            static const float VelvetStarZooidsLow = 6.0;        // fewest zooids to a system; a whole number
            static const float VelvetStarZooidsSpan = 7.0;       // how many different counts; a whole number, 6 to 12 here
            static const float VelvetStarInner = 0.28;           // radius where the zooids begin, in system radii; in [PoreRadius, Outer)
            static const float VelvetStarOuter = 0.95;           // and where they end; in (Inner, 1)
            static const float VelvetStarFill = 0.62;            // a zooid's breadth, in sectors at its middle; in (0, 1)
            static const float VelvetStarSoft = 0.65;            // the oval's norm where its edge begins to soften; in [0, 1)
            static const float VelvetStarPoreRadius = 0.22;      // the central pore's radius, in system radii; in (0, Inner]
            static const float VelvetStarPaleGain = 1.25;        // the tone a zooid lifts to, times the body
            static const float VelvetStarPaleFloor = 0.14;       // and the least luminance it lifts to (linear); in (0, 1)
            static const float VelvetStarPaleHue = 0.8;          // how much of the body's hue a zooid keeps, the rest white; in [0, 1]
            static const float VelvetStarToneSpread = 0.5;       // how far a system's zooids may fall short of it; in [0, 1]
            static const float VelvetStarSurroundDark = 0.3;     // share of the body's light lost in a surround; in [0, 1)
            static const float VelvetStarPoreDark = 0.85;        // and at the pore's middle; in [0, 1)
            static const float VelvetStarLineWidth = 0.05;       // spacings of F2 - F1 where the line is gone
            static const float VelvetStarLineDark = 0.25;        // share of the light the line takes; in [0, 1)
            static const float VelvetStarPlanePower = 6.0;       // higher: a sharper change of plane; at least 1
            static const float VelvetStarPlaneSkip = 0.03;       // weight under which a plane is left out; in [0, 0.3)
            static const float VelvetStarFadeLow = 6.0;          // spacing in pixels where they are gone
            static const float VelvetStarFadeHigh = 14.0;        // and where they are whole

            // The star systems in one plane, at a point in metres in that plane: the nearest centre of the
            // jittered hexagonal lattice by a 3 by 3 search of its cells (rows VelvetStarSpacing * sqrt(3) / 2
            // apart, every other row half a spacing along), then x the zooids (0 to 1, times the system's
            // tone), y the surround (the system less its zooids), z the line between systems, w the pore.
            // seed shifts the hash so two planes, and two creatures, do not repeat. The names that are
            // reserved words in HLSL (line, point) are avoided on purpose.
            float4 VelvetStars(float2 metres, float3 seed)
            {
                float rowHeight = VelvetStarSpacing * 0.8660254;
                float2 baseCell = floor(float2(metres.x / VelvetStarSpacing, metres.y / rowHeight));

                float nearest = 1e4;
                float second = 1e4;
                float2 nearestRel = float2(0.0, 0.0);
                float2 nearestCell = float2(0.0, 0.0);

                [unroll]
                for (int dr = -1; dr <= 1; dr++)
                {
                    [unroll]
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        float latRow = baseCell.y + dr;
                        float latCol = baseCell.x + dc;
                        float stagger = latRow - 2.0 * floor(0.5 * latRow);
                        float3 jitter = EvoHash3(float3(latCol, latRow, 0.0) + seed);
                        float2 centre = float2((latCol + 0.5 * stagger) * VelvetStarSpacing, latRow * rowHeight) +
                                        (jitter.xy - 0.5) * (2.0 * VelvetStarJitter * VelvetStarSpacing);
                        float2 rel = metres - centre;
                        float dist = length(rel);

                        if (dist < nearest)
                        {
                            second = nearest;
                            nearest = dist;
                            nearestRel = rel;
                            nearestCell = float2(latCol, latRow);
                        }
                        else if (dist < second)
                        {
                            second = dist;
                        }
                    }
                }

                float radius = VelvetStarRadius * VelvetStarSpacing;
                float3 own = EvoHash3(float3(nearestCell, 1.0) + seed);
                float zooids = VelvetStarZooidsLow + floor(VelvetStarZooidsSpan * own.x);

                // Which sector of the rosette, as a turn: a whole number of sectors to the circle, so only the
                // fraction is read and the cut where the angle wraps shows nothing. across is the offset from a
                // zooid's axis, in sectors, in [-0.5, 0.5).
                float turn = atan2(nearestRel.y, nearestRel.x) * 0.15915494 * zooids + own.y;
                float across = frac(turn) - 0.5;
                float lateral = nearest * across * 6.2831853 / zooids;

                float middle = 0.5 * (VelvetStarInner + VelvetStarOuter) * radius;
                float halfLength = 0.5 * (VelvetStarOuter - VelvetStarInner) * radius;
                float halfBreadth = VelvetStarFill * middle * 3.14159265 / zooids;
                float ovalU = (nearest - middle) / halfLength;
                float ovalV = lateral / max(halfBreadth, 1e-6);
                float zooid = 1.0 - smoothstep(VelvetStarSoft, 1.0, sqrt(ovalU * ovalU + ovalV * ovalV));

                float disc = 1.0 - smoothstep(0.85 * radius, 1.05 * radius, nearest);
                float tone = lerp(1.0 - VelvetStarToneSpread, 1.0, own.z);
                float edge = 1.0 - smoothstep(0.0, VelvetStarLineWidth, (second - nearest) / VelvetStarSpacing);
                float pore = 1.0 - smoothstep(0.5 * VelvetStarPoreRadius, VelvetStarPoreRadius, nearest / radius);

                return float4(zooid * tone, disc * (1.0 - zooid), edge, pore);
            }

            // ---------------------------------------------------------------- links, segmented and fringed
            //
            // A capsule part under the link dial (_LinkShape.w, EVOSIM_THEATRE_LINK), after a bristle worm's body:
            // repeated segments along it, each with a lobe and a few bristles at the sides. Light and shading only,
            // no vertex moves for any of it (the taper is LinkTaper's), so nothing can leave the collider. The
            // segments are counted along the whole capsule, base pole to free pole, from the palette's coordinate,
            // so the shaft and the two caps, three meshes, carry one set of them.
            //
            // The segment phase q is zero at a groove and whole numbers of q apart. A segment is LinkSegmentShare of
            // the base radius long; when the tissue look's muscle bands are on as well (_TissueLook with _Banding)
            // q is the bands' own coordinate (a copy of TissueBands's, the two must move together), so a groove sits
            // on a ring and a lobe between two.
            static const float LinkSegmentShare = 0.6;          // a segment's length, in base radii; at least 0.2
            static const float LinkPhaseMix = 7.3;              // the creature's own number times this offsets the segments

            // What a segment is made of, in c = 0.5 - 0.5 cos(2 pi q), zero at a groove and one at a segment's
            // middle. The groove is (1 - c) to this power, narrow; the lobe rises from LinkLobeLow to LinkLobeHigh.
            static const float LinkGroovePower = 5.0;          // higher: a thinner groove
            static const float LinkLobeLow = 0.05;             // c where a lobe begins; in [0, LinkLobeHigh)
            static const float LinkLobeHigh = 0.6;             // and where it is whole; in (LinkLobeLow, 1]

            // The silhouette band they show in, on facing = 1 - n.v: nothing facing the eye, all of it at the edge
            // (the frill's, FrillEdgeLow and High).
            static const float LinkEdgeLow = 0.45;
            static const float LinkEdgeHigh = 0.9;

            // The groove: the share of the body's light it takes across the surface, and at the silhouette, where it
            // is the gap between two lobes. The rim over a lobe's crest and in a groove, as the frill's crest and
            // notch are; and a pale lobe added to the rim, in a tone of the rim's own colour lifted by LinkPaleLift,
            // at LinkPaleGain of the rim's strength.
            static const float LinkGrooveDark = 0.16;          // across the surface; in [0, 1)
            static const float LinkNotchDark = 0.5;            // at the silhouette; in [LinkGrooveDark, 1)
            static const float LinkLobeRim = 1.7;              // the rim's strength over a lobe; at least 1
            static const float LinkNotchRim = 0.12;            // and in a groove; in [0, 1]
            static const float LinkPaleGain = 0.55;            // the added pale rim, as a share of the rim's
            static const float LinkPaleLift = 1.5;             // the rim's colour times this, capped at one

            // Bristles: fine dark lines across a lobe at the silhouette, LinkBristlesPerSegment to a segment, each
            // taking LinkBristleDark of the lobe's rim and its pale. They are finer than the segments, so they fade
            // out first. All of it fades out as the segments fall from LinkFadeHigh to LinkFadeLow pixels apart.
            static const float LinkBristlesPerSegment = 4.0;   // whole number, 2 to 6
            static const float LinkBristlePower = 6.0;         // higher: thinner lines
            static const float LinkBristleDark = 0.6;          // in [0, 1]
            static const float LinkFadeLow = 3.0;              // pixels between two segments where they are gone
            static const float LinkFadeHigh = 8.0;             // and where they are whole

            // The segment phase of a point on a link, in segments. Free of the bands it counts from the base
            // pole: the palette's coordinate times the capsule's length in radii over a segment's length in radii,
            // offset by the creature's own number so two links are not ringed alike. With the bands on it is
            // TissueBands's x, read at the same undisplaced position from the visual's own scale and half extents
            // (the long axis takes Y on a tie, through AtLeast).
            float LinkPhase(float3 positionOS)
            {
                float ownPhase = (_LinkAxis.x * positionOS.y + _LinkAxis.y) * (_LinkShape.x / LinkSegmentShare) +
                                 _Individual * LinkPhaseMix;

                float3 he = HalfExtentsWS();
                float3 axis;
                if (AtLeast(he.y, he.x) && AtLeast(he.y, he.z)) axis = float3(0, 1, 0);
                else if (AtLeast(he.x, he.z)) axis = float3(1, 0, 0);
                else axis = float3(0, 0, 1);

                float smallest = min(he.x, min(he.y, he.z));
                float metres = dot(positionOS, axis) * 2.0 * dot(he, axis);
                float bandPhase = metres / max(1e-4, BandSpacing * smallest);

                return (_TissueLook > 0.5 && _Banding > 0.0) ? bandPhase : ownPhase;
            }

            // The bodies and the water (TheatreSkin; the owner's note of 2026-10-19 that the bodies read as
            // self-lit). x: how far the guild rim and the inner glow take the light left at the body's depth,
            // as the key and the ambient do (0: they shine at full strength anywhere); y, z, w: how much of
            // the rim, the inner glow and the light through the tissue is taken away. Zero is today's body.
            float4 _EvoBodyWater;

            // A one skin's rest frame at a pixel (EVOSIM_THEATRE_ONE_SKIN): the world directions of the rest frame's
            // three axes, for a marking that tilts the normal by a slope worked out in rest metres (the papillae, the
            // velvet pores), so the slope reaches the world as a part's does through its matrix's columns. A skin's
            // object space is its root's, which does not turn as the body does, so its columns cannot say this. The
            // screen derivatives of the rest position and of the world position can, exactly across one triangle,
            // since the skinning is linear across one: a rest vector's part in the surface is carried by the 2 x 2
            // solve through the two derivatives, and its part along the rest normal goes to the world normal at the
            // same stretch (the square root of the two derivatives' areas). False where the derivatives are
            // degenerate, a triangle seen edge on, and the caller keeps the columns.
            bool SkinRestAxes(
                float3 restDx, float3 restDy, float3 worldDx, float3 worldDy, float3 restNormal, float3 normalWS,
                out float3 axisX, out float3 axisY, out float3 axisZ)
            {
                axisX = float3(1.0, 0.0, 0.0);
                axisY = float3(0.0, 1.0, 0.0);
                axisZ = float3(0.0, 0.0, 1.0);

                float aa = dot(restDx, restDx);
                float ab = dot(restDx, restDy);
                float bb = dot(restDy, restDy);
                float det = aa * bb - ab * ab;

                if (!(aa * bb > 1e-36) || !(det > 1e-6 * aa * bb)) return false;

                float stretch = sqrt(length(cross(worldDx, worldDy)) * rsqrt(det));

                // Each rest axis e: its components along the two rest derivatives, solved, carried by the world's.
                float2 cX = float2(bb * restDx.x - ab * restDy.x, aa * restDy.x - ab * restDx.x) / det;
                float2 cY = float2(bb * restDx.y - ab * restDy.y, aa * restDy.y - ab * restDx.y) / det;
                float2 cZ = float2(bb * restDx.z - ab * restDy.z, aa * restDy.z - ab * restDx.z) / det;

                axisX = normalize(cX.x * worldDx + cX.y * worldDy + normalWS * (stretch * restNormal.x));
                axisY = normalize(cY.x * worldDx + cY.y * worldDy + normalWS * (stretch * restNormal.y));
                axisZ = normalize(cZ.x * worldDx + cZ.y * worldDy + normalWS * (stretch * restNormal.z));

                return true;
            }
#if defined(_EVO_SHEEN_CAP2)

            // ---------------------------------------------------------------- SHEEN_CAP 2
            //
            // EVOSIM_THEATRE_SHEEN_CAP at 2, with SKIN2 off: the white the base tissue's own specular adds on a creature's body
            // (the sheen chain after litBeforeSheen: the soft tissue's lobe, the _Sss branch's, the plain one, and on the one
            // skin its power-96 lobe, which at ONE_SKIN 3 is the hard glint) is rolled off toward a ceiling of
            // SheenCapBaseShare of the luminance of the body's own key light at the pixel, by the soft knee of SheenCapped
            // (SKIN2's cap, which is compiled only with SKIN2 and so cannot be called here): ceiling * (1 - exp(-white /
            // ceiling)). The white is scaled and not recoloured, so an iridescent tint keeps its hue. Every line of it is under
            // this keyword, which TheatreSkin.EnsureSheenCapBaseKeyword turns on only at 2 with SKIN2 off.
            //
            // The share is SheenCapped's (0.5): the base lobe's peak is _Sheen times its gain of the key, against a body whose
            // own light is its albedo's luminance of the key, the same ratio SKIN2's cap was set against. Kept as a name of
            // its own because SheenCapShare is not compiled here; change the two together.
            static const float SheenCapBaseShare = 0.5;
            static const float SheenCapBaseTiny = 1e-6;             // the floor under a luminance divided by
            static const float SheenCapBaseThallusLow = 0.1;        // a thallus's solid flag is in [0.18, 0.32] (ThalliFlag*),
            static const float SheenCapBaseThallusHigh = 0.5;       // a sphere's or capsule's is 0 and a box's, a lamina's and a skin's above 0.5

            float3 SheenCappedBase(float3 white, float3 bodyLit)
            {
                float whiteLuma = max(dot(white, LumaWeights), 0.0);
                float ceiling = max(SheenCapBaseShare * dot(bodyLit, LumaWeights), SheenCapBaseTiny);
                float kept = ceiling * (1.0 - exp(-whiteLuma / ceiling));

                return white * (kept / max(whiteLuma, SheenCapBaseTiny));
            }
#endif
#if defined(_EVO_SKIN2)

            // ---------------------------------------------------------------- SKIN2
            //
            // EVOSIM_THEATRE_SKIN2: on the one skin (the shape channel's x above 2.5), a diffuse sheen, light through the
            // tissue, asymmetry, a pattern by clade and a margin band. Every statement of it is under this keyword, which
            // only TheatreSkin.EnsureSkin2Keyword turns on, so a frame with the dial off is the frame without it. All of
            // it reads the rest frame (the vertex's rest position over the column lengths, times them again), so it stays
            // on the tissue as the body bends and turns, and none of it moves a vertex or reads the screen.
            // What reaches it from outside: _PatternShift.z and .w, a clade's two fractions in [0, 1) (z is zero on a
            // greyed body, a corpse or a neck, which then take no margin and no crease); the vertex's four marks
            // (Varyings.skin2); and _SkinCarveLength, the body-wide length in rest metres that TheatreOneSkin sets under
            // this dial at every level.

            // The diffuse sheen: a broad lobe at half the strength, in place of the skin's tight one (SkinSheenPower 96),
            // and a soft Fresnel sheen at grazing angles, a share of the key. The Fresnel term is by how glossy the
            // tissue is (_Sheen) so a matte tissue and a corpse take less.
            static const float Skin2SheenPower = 20.0;              // in [16, 24]
            static const float Skin2SheenGain = 0.5;                // of the sheen the tight lobe had
            static const float Skin2FresnelGain = 0.11;             // of the key; in [0.08, 0.15]
            static const float Skin2FresnelPower = 3.0;             // on 1 - n.v
            static const float Skin2FresnelSheenScale = 10.0;       // the Fresnel is saturate(_Sheen * this) of its gain
#if defined(_EVO_SHEEN_CAP)

            // ---------------------------------------------------------------- SHEEN_CAP
            //
            // EVOSIM_THEATRE_SHEEN_CAP: the white SKIN2's sheen adds (the lobe and the Fresnel, on the one skin and on a
            // part; on a part with the soft tissue, its own lobe and the Fresnel) is rolled off toward a ceiling of
            // SheenCapShare of the luminance of the body's own key light at the pixel (litBeforeSheen: the diffuse, the
            // wider wrap and the light through), by the soft knee ceiling * (1 - exp(-white / ceiling)): a faint sheen
            // passes nearly whole and the peak stops at the ceiling. The white is scaled and not recoloured, so an
            // iridescent tint keeps its hue. Every line of it is under this keyword, which TheatreSkin.EnsureSkin2Keyword
            // turns on beside _EVO_SKIN2 only while the dial is on.
            //
            // Why a half. Uncapped, the lobe's peak is _Sheen * Skin2SheenGain of the key (0.225 on a biter's 0.45), and
            // the body's own light is its albedo's luminance of the key, which on a red body is a few hundredths: the white
            // is then about three times the body's light at the highlight, and the surface keeps under a third of its
            // saturation. A white of SheenCapShare of the body's light lifts the surface by log2(1.5) = 0.58 EV and keeps
            // 70 % of its saturation (1.0 would keep 55 %).
            static const float SheenCapShare = 0.5;
            static const float SheenCapTiny = 1e-6;                 // the floor under a luminance divided by

            float3 SheenCapped(float3 white, float3 bodyLit)
            {
                float whiteLuma = max(dot(white, LumaWeights), 0.0);
                float ceiling = max(SheenCapShare * dot(bodyLit, LumaWeights), SheenCapTiny);
                float kept = ceiling * (1.0 - exp(-whiteLuma / ceiling));

                return white * (kept / max(whiteLuma, SheenCapTiny));
            }
#endif

            // Light through the tissue. The diffuse wraps this much wider than _Wrap (0.45 in the material), and the
            // light the wider wrap adds is in a mix of the body's colour and the tissue's transmission hue at the body's
            // luminance (at least Skin2WrapFloor, so a dark body still glows), a soft coloured terminator. Thin tissue
            // (marks.x from Skin2ThinLow to Skin2ThinHigh) takes more of the backlight: Skin2ThinGain times Transmission
            // at full thickness, since the metre-based thickness is one over nearly all of a small body.
            static const float Skin2WrapExtra = 0.35;               // in [0.3, 0.5] on top of _Wrap
            static const float Skin2WrapScatter = 0.5;              // the share of the transmission hue in the wrapped light
            static const float Skin2WrapFloor = 0.08;
            static const float Skin2ThinLow = 0.30;
            static const float Skin2ThinHigh = 0.85;
            static const float Skin2ThinGain = 0.9;

            // Asymmetry. A low-frequency noise of the rest position, per body (seeded by _Carve.x and _Individual): value
            // by Skin2ToneAmp either way, hue by Skin2HueAmp turns of the wheel either way; the wavelength is
            // Skin2NoiseWavelength of _SkinCarveLength. The same noise sets the margin's width. The pattern's coordinates
            // are warped by Skin2WarpAmp of a tile, over Skin2WarpWavelength tiles. A second, finer octave of relief in the
            // normal, at Skin2ReliefWavelength of _SkinCarveLength and a slope of up to about Skin2ReliefHeight times the
            // noise's own slope, fading out as its wavelength falls from Skin2ReliefFadeHigh to Skin2ReliefFadeLow
            // pixels. A crease at a joint (marks.z, one where two bones share a vertex evenly): the colour darkens by
            // Skin2CreaseDark and the normal tilts as a groove of depth marks.w would, Skin2CreaseSlope of the full tilt.
            static const float Skin2ToneAmp = 0.06;
            static const float Skin2HueAmp = 0.04;                  // in turns of the hue wheel
            static const float Skin2NoiseWavelength = 3.0;
            static const float Skin2WarpAmp = 0.08;
            static const float Skin2WarpWavelength = 1.6;
            static const float Skin2ReliefWavelength = 0.3;
            static const float Skin2ReliefHeight = 0.12;
            static const float Skin2ReliefFadeLow = 3.0;
            static const float Skin2ReliefFadeHigh = 8.0;
            static const float Skin2CreaseDark = 0.18;
            static const float Skin2CreaseSlope = 0.6;

            // The clade's pigment pair, on the pattern (PatternSkin2): the body's own hue family. Either the dark pair as
            // today's (PatternDarken, PatternSaturation) or, for Skin2PigmentPaleShare of clades, a pale one at
            // Skin2PigmentLight times the body's luminance (at least Skin2PigmentFloor); the hue turned by up to
            // Skin2PigmentHue turns either way, from the clade's second fraction.
            static const float Skin2PigmentPaleShare = 0.35;
            static const float Skin2PigmentLight = 1.9;
            static const float Skin2PigmentFloor = 0.07;
            static const float Skin2PigmentHue = 0.07;

            // The margin band. The clade's first fraction picks its colour: none below Skin2MarginAbsent (a quarter of
            // clades), orange with a pale line (white on a warm body) to Skin2MarginWarmEnd, pale to
            // Skin2MarginPaleEnd, dark on a body light enough to show it (pale on a dark one) to Skin2MarginDarkEnd, and
            // the hue's complement at more saturation above. The band's luminance is the body's times Skin2MarginLift
            // plus Skin2MarginFloor, between Skin2MarginMinLuma and Skin2MarginMaxLuma. Its width is
            // Skin2MarginMin to Skin2MarginMax of the local width, by the asymmetry noise: on a flat section the edge
            // measure (one minus the half thickness over the part's usual) is 1 - sqrt(2w - w^2) at a share w of the half
            // width in from the rim, which is where the band starts. The measure is taken as the larger of that, a little
            // lifted by the rest normal's nearness to the rim, and the rest normal's own call (marks.y from
            // Skin2SilRimLow to Skin2SilRimHigh), since the thickness along the normal is long at the very rim.
            static const float Skin2MarginAbsent = 0.25;
            static const float Skin2MarginWarmEnd = 0.55;
            static const float Skin2MarginPaleEnd = 0.70;
            static const float Skin2MarginDarkEnd = 0.80;
            static const float Skin2MarginMin = 0.04;
            static const float Skin2MarginMax = 0.10;
            static const float Skin2MarginSoft = 0.06;              // half the smoothstep across the band's inner edge, in edge units
            static const float Skin2MarginBlend = 0.25;
            static const float Skin2SilBlendLow = 0.05;
            static const float Skin2SilBlendHigh = 0.50;
            static const float Skin2SilRimLow = 0.55;
            static const float Skin2SilRimHigh = 0.90;
            static const float Skin2MarginOpacity = 0.9;
            static const float Skin2MarginLift = 1.6;
            static const float Skin2MarginFloor = 0.06;
            static const float Skin2MarginMinLuma = 0.16;
            static const float Skin2MarginMaxLuma = 0.60;
            static const float Skin2MarginWarmSimilar = 0.90;       // cosine of the body's colour to orange over which the band is pale
            static const float Skin2MarginLine = 0.8;               // the pale line's share, at the band's inner edge
            static const float Skin2MarginLineWidth = 0.35;         // the line's share of the band
            static const float Skin2MarginPaleTrace = 0.15;         // a pale band keeps this much of the body's hue
            static const float Skin2MarginDarkLuma = 0.14;          // a body darker than this takes a pale band and not a dark one
            static const float Skin2MarginDark = 0.28;              // the dark band's luminance, a share of the body's
            static const float Skin2MarginSaturation = 1.4;         // the complement's saturation against the body's

            // A colour turned about the grey axis: a hue turn of `turn` of the wheel, and nothing else.
            float3 Skin2HueTurn(float3 c, float turn)
            {
                float a = turn * 6.2831853;
                float cs = cos(a);
                float sn = sin(a);
                float3 k = float3(0.57735027, 0.57735027, 0.57735027);

                return max(c * cs + cross(k, c) * sn + k * (dot(k, c) * (1.0 - cs)), float3(0.0, 0.0, 0.0));
            }

            // The asymmetry's value noise, in [0, 1], of a rest position in metres: one number a body and a side own.
            float Skin2ValueNoise(float3 restMetres)
            {
                float3 seed = float3(_Carve.x * 31.7 + 11.3, _Individual * 17.9 + 5.1, _Carve.x * 7.3 + _Individual * 3.1);
                float3 gradient;

                return EvoValueNoise(restMetres / (max(_SkinCarveLength, 1e-4) * Skin2NoiseWavelength) + seed, gradient);
            }

            // The skin's colour with its asymmetry: its value and hue drift over the body, differently on each side.
            float3 Skin2Tone(float3 body, float3 restMetres)
            {
                float value = Skin2ValueNoise(restMetres);

                float3 seed = float3(_Carve.x * 13.9 + 2.9, _Individual * 29.3 + 17.7, _Carve.x * 3.7 + _Individual * 11.3);
                float3 gradient;
                float hue = EvoValueNoise(
                    restMetres / (max(_SkinCarveLength, 1e-4) * Skin2NoiseWavelength * 1.7) + seed, gradient);

                return Skin2HueTurn(body * (1.0 + Skin2ToneAmp * (2.0 * value - 1.0)), Skin2HueAmp * (2.0 * hue - 1.0));
            }

#if defined(_EVO_SKIN_JOIN)
            // SKIN_JOIN: PatternSkin2's pigment pair, worked out from a reference colour (the body's, through Skin2Tone) in
            // place of the pixel's; the same arithmetic as PatternSkin2's own lines.
            float3 JoinPigment2(float3 reference, float2 clade)
            {
                float luma = dot(reference, LumaWeights);
                float3 family = Skin2HueTurn(reference, (frac(clade.y * 5.37 + 0.11) - 0.5) * 2.0 * Skin2PigmentHue);
                float3 dark = lerp(float3(luma, luma, luma), family, PatternSaturation) * PatternDarken;
                dark = lerp(dark, _PatternTint.rgb, _PatternTint.a);
                float3 pale = family * (max(luma * Skin2PigmentLight, Skin2PigmentFloor) / max(dot(family, LumaWeights), 1e-4));
                return clade.y < Skin2PigmentPaleShare ? pale : dark;
            }

#endif
#if defined(_EVO_PATTERN_AA)
            // ---------------------------------------------------------------- PATTERN_AA
            //
            // EVOSIM_THEATRE_PATTERN_AA (1 or 2), on the one skin alone: PatternSkin2's fade and its mip level read the pixel's
            // footprint by a measure that does not follow its long axis. Compiled only under _EVO_PATTERN_AA, a local keyword
            // TheatreOneSkin turns on in the skin's own material copy while the dial is above 0, with the dial's value in the
            // global _EvoPatternAa. Colour only: nothing moves, and nothing is drawn outside a collider (D242).
            //
            // pixelMetres, length(fwidth(positionWS)), is the footprint's long axis. On a surface foreshortened in one
            // direction, a limb's shaft seen along its length, the pattern's cells are several pixels across the other way
            // and the fade takes the pattern to its mean, so the shaft draws no maze. Fragment works out the footprint's
            // measure in the open, from the world derivatives it already takes, and leaves it in PatternAaMetres for
            // PatternSkin2 alone; a part and a pixel the skin's test fails leave 0 there, and PatternSkin2 then keeps
            // pixelMetres. The measure serves the fade (the cell's pixel count) and the mip level together.
            //   1  the area measure, the geometric mean of the two axes: sqrt(length(cross(ddx, ddy)))
            //   2  the minor axis, held no smaller than the major over PatternAaMaxAniso: max(min(a, b), max(a, b) / 8),
            //      a = length(ddx), b = length(ddy), what an 8x anisotropic sampler resolves
            float _EvoPatternAa;
            static const float PatternAaMaxAniso = 8.0;           // value 2's anisotropy: the footprint's long axis over its
                                                                  // short, at most; 8:1 is what an 8x anisotropic sampler
                                                                  // resolves; in [1, 16]
            static const float PatternAaAreaFloor = 1e-6;         // metres: a measure under this, or not finite, is not used and
                                                                  // pixelMetres stands; the same 1 micrometre as the floor
                                                                  // every use of pixelMetres is clamped to

            static float PatternAaMetres = 0.0;                   // the measure for PatternSkin2; 0 is "not set"

            float PatternAaFootprint(float3 dx, float3 dy, float pixelMetres, float mode)
            {
                float a = length(dx);
                float b = length(dy);
                float measure = mode > 1.5
                    ? max(min(a, b), max(a, b) / PatternAaMaxAniso)
                    : sqrt(length(cross(dx, dy)));
                return (isfinite(measure) && measure >= PatternAaAreaFloor) ? measure : pixelMetres;
            }

#endif
            // PatternSkin for the one skin (PatternSkin's own text, on the rest position alone), with the pattern's
            // coordinates warped a little by a low-frequency noise (so the two sides differ) and the pigment a pair in the
            // body's own hue family, picked by the clade's second fraction (clade.y).
            float3 PatternSkin2(float3 body, float3 restMetres, float4 pattern, float2 shift, float2 clade,
                float pixelMetres, float3 restNormal)
            {
                float tile = max(pattern.x, 1e-4);
                float cellPixels = (tile / PatternCellsPerTile) / max(pixelMetres, 1e-6);
                float fade = smoothstep(PatternFadeLow, PatternFadeHigh, cellPixels);
#if defined(_EVO_PATTERN_AA)
                // PATTERN_AA: the cell's pixel count, and so the fade, from the footprint's measure.
                if (PatternAaMetres > 0.0)
                {
                    cellPixels = (tile / PatternCellsPerTile) / max(PatternAaMetres, 1e-6);
                    fade = smoothstep(PatternFadeLow, PatternFadeHigh, cellPixels);
                }
#endif
                float cover = pattern.w;

                [branch] if (fade > 0.001)
                {
                    float3 weights = pow(abs(restNormal), PatternBlendPower);
                    weights /= max(weights.x + weights.y + weights.z, 1e-6);

                    float3 warpSeed = float3(_Carve.x * 23.1 + 3.7, _Individual * 19.7 + 8.3, _Carve.x * 3.3 + _Individual * 9.1);
                    float3 warpP = restMetres / (tile * Skin2WarpWavelength) + warpSeed;
                    float3 gradient;
                    float3 warp = float3(
                        EvoValueNoise(warpP, gradient),
                        EvoValueNoise(warpP + 31.7, gradient),
                        EvoValueNoise(warpP + 67.3, gradient)) - 0.5;

                    float3 metres = restMetres + warp * (2.0 * Skin2WarpAmp * tile);
                    float level = max(0.0, log2(max(pixelMetres, 1e-6) / tile * PatternTexels));
#if defined(_EVO_PATTERN_AA)
                    // PATTERN_AA: the mip level from the footprint's measure.
                    if (PatternAaMetres > 0.0)
                        level = max(0.0, log2(max(PatternAaMetres, 1e-6) / tile * PatternTexels));
#endif

#if defined(_EVO_SKIN_JOIN)
                    // SKIN_JOIN: the cover by JoinCover, on the warped position, and the three reads below left unmade.
                    float joinCover = JoinCover(metres, restNormal, tile, pattern.z, shift, level, pattern.w);
#if defined(_EVO_JOIN_DEBUG)
                    JoinDebugCover = joinCover;
#endif
                    weights = float3(0.0, 0.0, 0.0);
#endif
                    float field = 0.0;

                    [branch] if (weights.x > 0.01)
                        field += weights.x * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.yz / tile, pattern.z) + shift, level).r;
                    [branch] if (weights.y > 0.01)
                        field += weights.y * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xz / tile, pattern.z) + shift, level).r;
                    [branch] if (weights.z > 0.01)
                        field += weights.z * SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, PatternTurn(metres.xy / tile, pattern.z) + shift, level).r;

                    cover = lerp(pattern.w, smoothstep(PatternLow, PatternHigh, field), fade);
#if defined(_EVO_SKIN_JOIN)
                    cover = lerp(pattern.w, joinCover, fade);
#endif
                }

                float luma = dot(body, LumaWeights);
                float3 family = Skin2HueTurn(body, (frac(clade.y * 5.37 + 0.11) - 0.5) * 2.0 * Skin2PigmentHue);
                float3 dark = lerp(float3(luma, luma, luma), family, PatternSaturation) * PatternDarken;
                dark = lerp(dark, _PatternTint.rgb, _PatternTint.a);
                float3 pale = family * (max(luma * Skin2PigmentLight, Skin2PigmentFloor) / max(dot(family, LumaWeights), 1e-4));
                float3 pigment = clade.y < Skin2PigmentPaleShare ? pale : dark;
#if defined(_EVO_SKIN_JOIN)
                // SKIN_JOIN: the pigment from the body's colour, with the skin's own asymmetry (Skin2Tone).
                pigment = lerp(pigment, JoinPigment2(Skin2Tone(_BaseColor.rgb, restMetres), clade), JoinToneShare);
#endif

                return lerp(body, saturate(pigment), saturate(pattern.y * PatternOpacity * cover));
            }

            // The margin band: a band of the clade's colour along the thin edge of the tissue, in the rest frame.
            float3 Skin2Margin(float3 body, float4 marks, float2 clade, float3 restMetres)
            {
                [branch] if (clade.x < Skin2MarginAbsent) return body;

                float width = lerp(Skin2MarginMin, Skin2MarginMax, Skin2ValueNoise(restMetres));
                float start = 1.0 - sqrt(max(2.0 * width - width * width, 1e-4));
                float edge = max(
                    saturate(marks.x + Skin2MarginBlend * smoothstep(Skin2SilBlendLow, Skin2SilBlendHigh, marks.y)),
                    smoothstep(Skin2SilRimLow, Skin2SilRimHigh, marks.y));
                float cover = smoothstep(start - Skin2MarginSoft, start + Skin2MarginSoft, edge);

                [branch] if (cover < 0.002) return body;

                float luma = dot(body, LumaWeights);
                float across = saturate((edge - start) / max(1.0 - start, 1e-3));
                float aim = clamp(luma * Skin2MarginLift + Skin2MarginFloor, Skin2MarginMinLuma, Skin2MarginMaxLuma);

                float3 direction = normalize(max(body, float3(1e-3, 1e-3, 1e-3)));
                float3 warm = float3(1.0, 0.42, 0.07);
                float3 pale = float3(0.88, 0.96, 1.0);
                float3 accent = pale;

                [branch] if (clade.x < Skin2MarginWarmEnd)
                {
                    accent = dot(direction, normalize(warm)) > Skin2MarginWarmSimilar ? pale : warm;
                    accent = lerp(accent, pale, Skin2MarginLine * (1.0 - smoothstep(0.0, Skin2MarginLineWidth, across)));
                }
                else if (clade.x < Skin2MarginPaleEnd)
                {
                    accent = lerp(pale, direction, Skin2MarginPaleTrace);
                }
                else if (clade.x < Skin2MarginDarkEnd)
                {
                    [branch] if (luma > Skin2MarginDarkLuma)
                    {
                        accent = lerp(float3(luma, luma, luma), body, Skin2MarginSaturation);
                        aim = luma * Skin2MarginDark;
                    }
                }
                else
                {
                    float3 turned = Skin2HueTurn(body, 0.5);
                    float turnedLuma = max(dot(turned, LumaWeights), 1e-4);
                    accent = max(lerp(float3(turnedLuma, turnedLuma, turnedLuma), turned, Skin2MarginSaturation), float3(0.0, 0.0, 0.0));
                }

                float3 margin = saturate(accent * (aim / max(dot(accent, LumaWeights), 1e-4)));

                return lerp(body, margin, cover * Skin2MarginOpacity);
            }

#endif
#if defined(_EVO_SKIN2) && defined(_EVO_SKIN2B)
// skin2b: SKIN2 level 2, the margin band at the outline.

            // ---------------------------------------------------------------- SKIN2 level 2: the band at the outline
            //
            // EVOSIM_THEATRE_SKIN2 at 2 (_EVO_SKIN2B, which TheatreSkin.EnsureSkin2Keyword turns on beside _EVO_SKIN2 and only
            // then): the margin band is laid where a real margin is, the outline of a body seen across its broad face (a
            // flatworm's or a nudibranch's mantle edge, a fin's trailing edge), and not on the thin rim of a flat part seen
            // edge-on. It replaces level 1's band (Skin2Margin) on the skin and on a part; the call sites keep level 1's
            // call and put this one over its result. The cover is the product of three terms, each a smoothstep of an
            // interpolated measure evaluated per pixel, so no per-vertex step shows:
            //   thin  the tissue's thinness, marks.x (one minus the half thickness over the part's usual), a smoothstep
            //         Skin2BSoft either side of the start that the band's width sets (level 1's: 4 to 10 % of the local half
            //         width by the asymmetry noise);
            //   rim   how far the rest normal has turned from the part's thinnest axis, marks.y: the flatness times one minus
            //         the normal's cosine with that axis, so a broad face has none however it is seen and a round part has
            //         none. On the skin TheatreOneSkin.Skin2BOutline has multiplied it by a hinge fade of the vertex's
            //         second heaviest skinning weight, so a joint's neck takes none;
            //   view  one minus the absolute cosine of the geometric normal with the direction to the eye, so the band shows
            //         where the surface turns away from the eye, the outline across a face, and a rim wall that faces the
            //         eye shows none. It is shading, as a Fresnel term is, and reads no screen position.
            // thin and rim are the rest frame's and ride on the tissue. The colour is level 1's palette with the cyan
            // complement gone (the clades that took it take the body's own hue, turned a little), and every hue is held to
            // Skin2BSaturation times the body's own saturation.
            static const float Skin2BSoft = 0.12;                   // half the smoothstep across the band's inner edge, in thin units
            static const float Skin2BRimLow = 0.25;                 // the rim gate: none below, whole above Skin2BRimHigh
            static const float Skin2BRimHigh = 0.60;
            static const float Skin2BViewLow = 0.15;                // 1 - |n.v|: none below, whole above Skin2BViewHigh
            static const float Skin2BViewHigh = 0.55;
            static const float Skin2BSaturation = 1.1;              // the most a band hue's saturation is, against the body's
            static const float Skin2BSatFloor = 0.08;               // the least saturation the cap allows, so a grey body keeps a trace
            static const float Skin2BNearTurn = 0.08;               // the former complement's clades: the body's hue turned this much of the wheel

            // The band dial (EVOSIM_THEATRE_SKIN2_BAND, 1 to 3): TheatreSkin.EnsureSkin2Keyword writes it beside the keyword. Skin2MarginB takes
            // it as max(_EvoSkin2BBand, 1), so an unset global (zero) is the dial at 1. It multiplies the band's width, kept to at most
            // Skin2BBandWidthCap of the local half width, and its opacity, kept to at most 1.
            float _EvoSkin2BBand;
            static const float Skin2BBandWidthCap = 0.25;
            // band3: from the dial at 2 to 3 the cap rises to this, so 3 widens the band where 2's cap held it (coordinator,
            // 2026-10-27); at 2 and below the cap is Skin2BBandWidthCap exactly.
            static const float Skin2BBandWidthCap3 = 0.5;
#if defined(_EVO_SKIN2_BAND4)

            // ------------------------------------------------ SKIN2_BAND at 4: three more edges on the one skin
            //
            // EVOSIM_THEATRE_SKIN2_BAND at 4 (_EVO_SKIN2_BAND4, which TheatreSkin.EnsureSkin2Keyword turns on at that value and
            // only then): Skin2MarginB's cover is the largest of level 3's (the dial's width and opacity stop at 3) and three
            // terms, each times its own strength, and each recolours the drawn surface in the colour Skin2MarginB already
            // computes for the clade (its margin), never moving it. A round body has no thin and no rim mark, so level 3 draws
            // it no band; these three are its edges.
            //   ends     the last of a part's half length along its longest axis, a per-vertex mark (UV0, packed in the tissue
            //            flag) from 0 in the middle to 1 at the end, faded to none at a joint face buried in a neighbour;
            //   crease   the joint's crease mark (marks.z) in a window about Skin2B4CreaseAt: a thin line along each flank of
            //            the crease, not its whole width;
            //   rim      level 2's view term alone, from a higher start so it is narrower, times a floor to one by the
            //            thinness mark (marks.x), so a fat part's is faint.
            // Only the one skin: Skin2B4End is minus one except across the skin's own call, so a part and the mouth's lip
            // take level 3's cover.
            static const float Skin2B4EndLow = 0.15;                // the end mark: none below, whole above Skin2B4EndHigh
            static const float Skin2B4EndHigh = 0.55;
            static const float Skin2B4EndStrength = 0.9;            // the ends' cover at the very end
            static const float Skin2B4CreaseAt = 0.5;               // the crease mark where the line lies (one is the middle)
            static const float Skin2B4CreaseWidth = 0.2;            // half the line's width, in crease-mark units
            static const float Skin2B4CreaseStrength = 0.8;         // the crease line's cover at its middle
            static const float Skin2B4SilLow = 0.55;                // the rim: level 2's view term from here, whole above High
            static const float Skin2B4SilHigh = 0.95;
            static const float Skin2B4SilFloor = 0.35;              // the rim's share on a part with no thinness (a fat one)
            static const float Skin2B4SilStrength = 0.6;            // the rim's cover at its fullest

            // The part-end mark of the pixel's tissue, 0 to 1, set by the skin's own call of Skin2MarginB; minus one outside it.
            static float Skin2B4End = -1.0;

            float Skin2B4Edges(float4 marks, float viewTerm)
            {
                float ends = Skin2B4EndStrength * smoothstep(Skin2B4EndLow, Skin2B4EndHigh, Skin2B4End);
                float crease = Skin2B4CreaseStrength * (1.0 - smoothstep(0.0, Skin2B4CreaseWidth, abs(marks.z - Skin2B4CreaseAt)));
                float rim = Skin2B4SilStrength * smoothstep(Skin2B4SilLow, Skin2B4SilHigh, viewTerm) * lerp(Skin2B4SilFloor, 1.0, marks.x);

                return max(ends, max(crease, rim));
            }
#endif

            // The view term: the outline across a face.
            float Skin2BView(float3 normalWS, float3 viewWS)
            {
                return smoothstep(Skin2BViewLow, Skin2BViewHigh, 1.0 - abs(dot(normalWS, viewWS)));
            }

            // HSV saturation, (max - min) / max.
            float Skin2BSaturationOf(float3 colour)
            {
                float peak = max(colour.r, max(colour.g, colour.b));

                return (peak - min(colour.r, min(colour.g, colour.b))) / max(peak, 1e-4);
            }

            // The colour with its HSV saturation held to at most cap: its value kept, its chroma scaled.
            float3 Skin2BCap(float3 colour, float cap)
            {
                float peak = max(colour.r, max(colour.g, max(colour.b, 1e-4)));
                float saturation = Skin2BSaturationOf(colour);

                [branch] if (saturation <= cap) return colour;

                return lerp(float3(peak, peak, peak), colour, cap / saturation);
            }

            // The margin band at the outline: Skin2Margin's colours, its width, and the cover above.
            float3 Skin2MarginB(float3 body, float4 marks, float2 clade, float3 restMetres, float viewTerm)
            {
                [branch] if (clade.x < Skin2MarginAbsent) return body;

                float skin2BBand = max(_EvoSkin2BBand, 1.0);
#if defined(_EVO_SKIN2_BAND4)
                // At 4 the existing band is level 3's: the dial's width and opacity stop at 3, and the edges are added to its cover.
                skin2BBand = min(skin2BBand, 3.0);
#endif
                float skin2BCap = skin2BBand > 2.0 ? lerp(Skin2BBandWidthCap, Skin2BBandWidthCap3, saturate(skin2BBand - 2.0)) : Skin2BBandWidthCap;
                float width = min(lerp(Skin2MarginMin, Skin2MarginMax, Skin2ValueNoise(restMetres)) * skin2BBand, skin2BCap);
                float start = 1.0 - sqrt(max(2.0 * width - width * width, 1e-4));
                float thin = smoothstep(start - Skin2BSoft, start + Skin2BSoft, marks.x);
                float rim = smoothstep(Skin2BRimLow, Skin2BRimHigh, marks.y);
                float cover = thin * rim * viewTerm;
#if defined(_EVO_SKIN2_BAND4)
                [branch] if (Skin2B4End >= 0.0) cover = max(cover, Skin2B4Edges(marks, viewTerm));
#endif

                [branch] if (cover < 0.002) return body;

                float luma = dot(body, LumaWeights);
                float across = saturate((marks.x - start) / max(1.0 - start, 1e-3));
                float aim = clamp(luma * Skin2MarginLift + Skin2MarginFloor, Skin2MarginMinLuma, Skin2MarginMaxLuma);

                float3 direction = normalize(max(body, float3(1e-3, 1e-3, 1e-3)));
                float3 warm = float3(1.0, 0.42, 0.07);
                float3 pale = float3(0.88, 0.96, 1.0);
                float3 accent = pale;

                [branch] if (clade.x < Skin2MarginWarmEnd)
                {
                    accent = dot(direction, normalize(warm)) > Skin2MarginWarmSimilar ? pale : warm;
                    accent = lerp(accent, pale, Skin2MarginLine * (1.0 - smoothstep(0.0, Skin2MarginLineWidth, across)));
                }
                else if (clade.x < Skin2MarginPaleEnd)
                {
                    accent = lerp(pale, direction, Skin2MarginPaleTrace);
                }
                else if (clade.x < Skin2MarginDarkEnd)
                {
                    [branch] if (luma > Skin2MarginDarkLuma)
                    {
                        accent = lerp(float3(luma, luma, luma), body, Skin2MarginSaturation);
                        aim = luma * Skin2MarginDark;
                    }
                }
                else
                {
                    accent = Skin2HueTurn(body, Skin2BNearTurn);
                }

                accent = Skin2BCap(accent, max(Skin2BSatFloor, Skin2BSaturation * Skin2BSaturationOf(body)));

                float3 margin = saturate(accent * (aim / max(dot(accent, LumaWeights), 1e-4)));

#if defined(_EVO_JOIN_DEBUG)
                [branch] if (JoinDebugBandOn) JoinDebugBand = cover * min(Skin2MarginOpacity * skin2BBand, 1.0);
#endif
                return lerp(body, margin, cover * min(Skin2MarginOpacity * skin2BBand, 1.0));
            }
#endif
#if defined(_EVO_SKIN2)
// skin2parts: SKIN2 on a part.

            // ---------------------------------------------------------------- SKIN2 on a part
            //
            // EVOSIM_THEATRE_SKIN2, on a body part that is not the one skin and is drawn in animal tissue (_Skin2Part, which
            // TheatrePalette.PaintSkin2 writes; a blade, a velvet absorber and the skin itself are left out in the
            // Fragment's own test). The part has none of the skin's vertex marks, so they are made here from its frame, in
            // the undisplaced object space, and fed to the skin's own functions (Skin2Tone, Skin2Margin, PatternSkin2), so
            // a part's colour, band and noise are the skin's, at the part's scale. Nothing here moves a vertex or reads the
            // screen. The thickness profile is a half ellipse whose half thickness is the part's smallest half extent
            // (HalfExtentsWS is in world metres, since a part's matrix is not scaled: no _SkinScale), so the thin mark is
            // one minus the local half thickness over it, as the skin's is over its part's usual one.
            static const float Skin2PartFlatLow = 0.1;              // the skin's flatness, TheatreOneSkin.Skin2FlatLow
            static const float Skin2PartFlatHigh = 0.6;             // and Skin2FlatHigh: 1 - smallest/middle half extent
            static const float Skin2PartRoundThin = 0.35;           // what a round part keeps of its thinness (Skin2RoundThin)
            static const float Skin2PartNoiseShare = 1.0;           // the noise's unit, a share of the part's smallest half
                                                                    // extent (the skin's is the median of its parts')

            // The part's marks: x how thin the tissue is here (the skin's mark x), y how near the mesh normal lies to the
            // part's rim (the skin's mark y), z the part's flatness, w unused. partNormal is the world normal's components
            // along the part's own unit axes (what PatternSkin takes its planes from), noiseLength the metres that are one
            // unit of the asymmetry noise (Skin2ValueNoise's length over Skin2NoiseWavelength).
            float4 Skin2PartMarks(float3 positionOS, float3 normalWS, float2 shape, out float3 partNormal, out float noiseLength)
            {
                float3 sizes = PartSizes();
                float3 he = HalfExtentsWS();

                // The thinnest axis, with RimBand's tie rule.
                float3 axisT;
                if (AtMost(he.x, he.y) && AtMost(he.x, he.z)) axisT = float3(1.0, 0.0, 0.0);
                else if (AtMost(he.y, he.z)) axisT = float3(0.0, 1.0, 0.0);
                else axisT = float3(0.0, 0.0, 1.0);

                float smallest = dot(he, axisT);
                float largest = max(he.x, max(he.y, he.z));
                float middle = he.x + he.y + he.z - smallest - largest;
                float flatness = smoothstep(Skin2PartFlatLow, Skin2PartFlatHigh, 1.0 - smallest / max(middle, 1e-6));

                float3 axisX = UNITY_MATRIX_M._m00_m10_m20 / max(sizes.x, 1e-6);
                float3 axisY = UNITY_MATRIX_M._m01_m11_m21 / max(sizes.y, 1e-6);
                float3 axisZ = UNITY_MATRIX_M._m02_m12_m22 / max(sizes.z, 1e-6);
                float3 geometric = normalize(normalWS);
                partNormal = float3(dot(geometric, axisX), dot(geometric, axisY), dot(geometric, axisZ));

                // The mesh's own normal in the part's frame: the world normal taken back through the matrix's inverse
                // transpose, which is the components along the axes times the column lengths.
                float3 meshNormal = partNormal * sizes;
                meshNormal *= rsqrt(max(dot(meshNormal, meshNormal), 1e-12));
                float alongThin = abs(dot(meshNormal, axisT));

                // The local thickness over the part's smallest half extent. On a sphere or a capsule it is the normal's
                // cosine with the thin axis (the section's half ellipse, read from the normal). On the box it is read from
                // the position, as RIM_BAND's section is, since a box's faces are flat: e is the p-norm of 8 of the two
                // other coordinates over the mesh half, and the thickness sqrt(1 - e^2).
                float thickness = alongThin;

                [branch] if (shape.x > 0.5 && shape.x < 1.5)
                {
                    float3 q = abs(positionOS) * (1.0 - axisT) / max(shape.y, 1e-4);
                    float big = max(max(q.x, q.y), max(q.z, 1e-5));
                    float3 r2 = (q / big) * (q / big);
                    float3 r4 = r2 * r2;
                    float e = saturate(big * pow(dot(r4 * r4, float3(1.0, 1.0, 1.0)), 0.125));

                    thickness = sqrt(max(1.0 - e * e, 0.0));
                }

                noiseLength = max(smallest * Skin2PartNoiseShare, 1e-4);

                return float4((1.0 - thickness) * lerp(Skin2PartRoundThin, 1.0, flatness),
                    flatness * (1.0 - alongThin), flatness, 0.0);
            }

#endif
#if defined(_EVO_MOUTH2)
            // ---------------------------------------------------------------- the mouth's opening, in the fragment
            //
            // What the vertex stage cut, the fragment finishes: the throat, a deep warm red-brown that the inner wall
            // and the floor darken toward; and the lip, a ring from the rim out MouthLipWidth radii that reads as raised
            // by its normal alone (the height is MouthLipHeight of the cup's depth, and no vertex rises), coloured with
            // the clade's margin band where it has one and SKIN2 is on, else the body's colour lifted. Everything is a
            // function of u, the distance from the mouth in radii, read at the undisplaced position (a part's own units,
            // or the rest frame of the one skin), so it stays on the tissue as the body moves, and nothing reads the screen
            // but the radius in pixels that fades the tilt out on a far body.

            // The throat's colour is full inside MouthThroatIn radii and gone by MouthThroatOut, and the light of the
            // lining (the rim and glow too) loses MouthLightDark of itself at the floor.
            static const float MouthThroatIn = 0.55;
            static const float MouthThroatOut = 0.95;
            static const float3 MouthThroat = float3(0.100, 0.034, 0.026);
            static const float MouthLightDark = 0.45;

            // The lip: a ring MouthLipWidth radii wide from MouthLipFrom, as high as MouthLipHeight of the cup's depth
            // (a sine squared, in the normal's tilt only). Its colour has soft edges MouthLipEdge wide, takes
            // MouthLipOpacity of the lip's colour, and a soft highlight of MouthLipHighlight on its outer half.
            static const float MouthLipFrom = 1.0;
            static const float MouthLipWidth = 0.25;
            static const float MouthLipHeight = 0.25;
            static const float MouthLipEdge = 0.05;
            static const float MouthLipOpacity = 0.9;
            static const float MouthLipHighlight = 0.18;

            // Without a band the lip is the body's colour lifted by this in value, at most to one.
            static const float MouthLipLift = 1.15;

            // The tilt fades out as the lip's width falls from MouthFadeHigh pixels to MouthFadeLow, so a far body does not
            // shimmer; the throat stays, as a mouth far off is still a dark dot. The slope's bound is the nose pit's.
            static const float MouthFadeLow = 1.5;
            static const float MouthFadeHigh = 4.0;
            static const float MouthSlopeBound = 1.4;

            // The small-mouth dial (EVOSIM_THEATRE_MOUTH2_SMALL): the disc fades out as the mouth's radius on screen falls from
            // MouthSmallFull pixels to MouthSmallGone, and its stripes' edges are at least this soft at every size.
            static const float MouthSmallGone = 10.0;
            static const float MouthSmallFull = 28.0;
            static const float MouthSmallSoftEdge = 0.4;

            // The plain mouth (EVOSIM_THEATRE_MOUTH2_PLAIN): the lip's step from the body, in the body's own hue, is this share of
            // its step without the dial, so the ring reads as a shaded rim and not as an eye's iris.
            static const float MouthPlainLipStep = 0.5;

            // Past this many radii nothing is done: beyond the lip and its soft edge.
            static const float MouthReach = 1.35;

            // The oral disc (EVOSIM_THEATRE_MOUTH2_DISC; _MouthDisc.a over zero): a ring from the lip's end (MouthLipFrom + MouthLipWidth) to
            // MouthDiscOuter radii, tinted toward the disc colour in radial stripes that alternate with the body's colour, the share fading
            // to zero over the outer MouthDiscFade of the ring's width. The stripe count is _MouthRadii.w, MouthDiscStripes where none is
            // written. Colour only: no vertex moves for it.
            static const float MouthDiscFrom = 1.25;
            static const float MouthDiscOuter = 1.9;
            static const float MouthDiscFade = 0.25;
            static const float MouthDiscStripes = 8.0;

            // The oral disc's kinds (EVOSIM_THEATRE_MOUTH2_KINDS; TheatreMouth2.MouthKind). _MouthRadii.w carries stripes + MouthKindStep * kind,
            // the kind 0 (radial stripes, the code under MouthKindStep) being today's disc. Each kind is colour only, drawn in the disc's polar
            // frame (radii, the angle about the axis), and gives a share in 0 to 1 that replaces the stripes' ring; the colour may be darkened
            // (a tone, a multiplier of the disc colour) or lifted toward white (a share) per pixel. Where a feature is finer than a pixel the
            // share fades to the kind's mean, as the stripes do.
            static const float MouthKindStep = 16.0;

            // 1, toothed ring: 2 * count triangular teeth, the base on the lip's inner edge (MouthLipFrom), the point at MouthToothTip radii.
            // A tooth's base takes MouthToothBase of its half pitch. The teeth are the disc colour lifted MouthToothWhite toward white; the
            // mean share is the triangles' area share of the band.
            static const float MouthToothTip = 0.6;
            static const float MouthToothBase = 0.8;
            static const float MouthToothWhite = 0.55;
            static const float MouthToothMean = 0.4;

            // 2, sucker: MouthSuckerRows concentric rows of round tooth dots between MouthDiscFrom and MouthDiscOuter, each row half a step
            // round from the one before, count and count + MouthSuckerExtra dots alternately. A dot is MouthSuckerDot radii across at most
            // (and at most MouthSuckerFill of its row's pitch), lifted MouthSuckerWhite toward white; the mean is a dot's share of its cell.
            static const float MouthSuckerRows = 3.0;
            static const float MouthSuckerExtra = 4.0;
            static const float MouthSuckerDot = 0.075;
            static const float MouthSuckerFill = 0.32;
            static const float MouthSuckerWhite = 0.45;
            static const float MouthSuckerMean = 0.15;

            // 3, crown: count tentacle lobes from the lip (MouthLipFrom) out to MouthCrownReach radii, which lifts this kind's reach past
            // MouthDiscOuter. A lobe starts MouthCrownBase of its half pitch wide, narrows by MouthCrownTaper of that toward its tip and ends
            // in a rounded cap; the colour between lobes is the body's. The mean is the lobes' share of the band.
            static const float MouthCrownReach = 2.6;
            static const float MouthCrownBase = 0.7;
            static const float MouthCrownTaper = 0.7;
            static const float MouthCrownMean = 0.4;

            // 4, beak: two plates over the opening, out to the lip (MouthLipFrom), dark keratin: the disc colour scaled to a peak channel of
            // MouthBeakValue. They meet at a straight line along the first tangent axis, a pale seam MouthBeakSeam radii each side of it, the
            // disc colour lifted MouthBeakSeamWhite toward white.
            static const float MouthBeakValue = 0.2;
            static const float MouthBeakSeam = 0.04;
            static const float MouthBeakSeamWhite = 0.5;

            // 5, gape: a slit MouthGapeLong radii each way along the first tangent axis and MouthGapeWide across it, dark (the disc colour
            // scaled to a peak channel of MouthGapeValue), ringed by a lip of the disc colour MouthGapeLip wide in the slit's own units
            // (the ellipse's). The mean is the slit and lip's share of the band.
            static const float MouthGapeLong = 1.8;
            static const float MouthGapeWide = 0.4;
            static const float MouthGapeValue = 0.15;
            static const float MouthGapeLip = 0.25;
            static const float MouthGapeMean = 0.25;

            // The relief (EVOSIM_THEATRE_MOUTH2_RELIEF; _MouthRelief, the strength r, over zero only with the kinds on): the oral disc (kind 0),
            // the toothed ring (1), the sucker (2) and the gape (5) read as openings with a lip and an inner wall rather than painted ovals. Every number below
            // is reached at r = 1 and scaled by r; at zero none of it runs. Shading and colour only, as the lip is: no vertex moves for it.
            //
            // The round lip of kinds 0, 1 and 2 stands MouthReliefLipGain times higher, and the cup's wall tilts MouthReliefWallGain times
            // more. The wall, from MouthReliefWallIn to the lip, takes a lining colour (the body's darkened to MouthLiningBody, mixed
            // MouthLiningMix toward a flesh red), the throat's dark held to the floor inside MouthReliefThroatIn to MouthReliefThroatOut, and
            // the light on the wall is occluded by MouthLightDark times the cup's depth there, so the far wall catches the light and the near
            // one falls into shadow.
            static const float MouthReliefLipGain = 2.0;
            static const float MouthReliefWallGain = 1.0;
            static const float MouthReliefWallIn = 0.3;
            static const float MouthReliefThroatIn = 0.35;
            static const float MouthReliefThroatOut = 0.6;
            static const float MouthLiningBody = 0.5;
            static const float MouthLiningMix = 0.55;
            static const float3 MouthLiningFlesh = float3(0.30, 0.075, 0.06);

            // Kind 0: the disc's stripes become radial furrows, a height of MouthReliefFurrow radii (a cosine across the stripes), the
            // stripe of the disc colour standing up.
            static const float MouthReliefFurrow = 0.08;

            // Kind 1: each tooth narrows to MouthReliefToothBase of its half pitch at the base and stands as a ridge MouthReliefTooth radii
            // high along its centre line, so one flank takes the light and the other falls away. Its colour is enamel, MouthEnamel, graded
            // from MouthEnamelBase at the gum to one at the tip, and the gaps between teeth fall MouthReliefToothGap of the way to the throat.
            static const float MouthReliefToothBase = 0.5;
            static const float MouthReliefTooth = 0.08;
            static const float3 MouthEnamel = float3(0.62, 0.57, 0.44);
            static const float MouthEnamelBase = 0.7;
            static const float MouthReliefToothGap = 0.8;

            // Kind 2: each dot is a small cup of its own, a rim MouthReliefCupRim radii high round a dimple darkened to
            // MouthReliefCupDark of the dot's colour; the round mouth's floor, lining and lip gain stay kinds 0 and 1. A dot at its
            // plain size is a few pixels in a close still, too small to carry a cup, so the relief grows it MouthReliefCupGrow
            // times, up to MouthReliefCupFill of the pitch (the plain dot's MouthSuckerFill), and the dimple is the dot's inner
            // MouthReliefCupHollow of its radius. Overshot at 1.8 first (D262); 1.4, since at 1.8 the two dots of a sparse row read as
            // eyes with irises.
            static const float MouthReliefCupRim = 0.06;
            static const float MouthReliefCupDark = 0.12;
            static const float MouthReliefCupLight = 1.6;
            static const float MouthReliefCupGrow = 1.4;
            static const float MouthReliefCupFill = 0.45;
            static const float MouthReliefCupHollow = 0.7;

            // Kind 5: the slit is a trench and its lip a raised ring. In the slit's own units (the ellipse's radius, one at its edge) the lip
            // rises MouthReliefGapeLip radii in a sine squared over its width, and the wall falls MouthReliefGapeDepth radii from the edge to
            // MouthReliefGapeFloor, where the floor is the throat; the wall between takes the lining. The round mouth's lip and throat give
            // way to the slit's, and the kind reaches out to the end of the slit's lip. The slit opens to MouthReliefGapeWide across, since
            // without the round throat a slit MouthGapeWide across leaves a floor of a line and the mouth reads shut. 1.3, since at 0.9 a
            // gape on a fold of the body (47834) is foreshortened across and reads shut, narrower than the round throat it replaces.
            static const float MouthReliefGapeWide = 1.3;
            static const float MouthReliefGapeLip = 0.12;
            static const float MouthReliefGapeDepth = 0.3;
            static const float MouthReliefGapeFloor = 0.55;

            // The relief's tilts are bounded as the lip's is (MouthSlopeBound) and fade as a stripe or a tooth falls under a pixel.

            // The body's colour lifted about 15 % in value.
            float3 MouthLifted(float3 body)
            {
                float peak = max(max(body.r, body.g), max(body.b, 1e-4));
                return body * clamp(1.0 / peak, 1.0, MouthLipLift);
            }

            // The nearest of the skin's mouths to a rest position, by u: u, the offset from its centre in rest metres and
            // its radius in rest metres. u is huge where the skin has no mouth.
            void MouthSkinNearest(float3 restAt, out float u, out float3 offset, out float radius)
            {
                u = 1.0e4;
                offset = float3(0.0, 0.0, 0.0);
                radius = 0.0;

                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 mouth = MouthSkinAt(i);

                    if (mouth.w > 0.0)
                    {
                        float3 d = restAt - mouth.xyz;
                        float ui = MouthSkinU(restAt, i);

                        if (ui < u)
                        {
                            u = ui;
                            offset = d;
                            radius = mouth.w;
                        }
                    }
                }
            }

            // The disc's axis (xyz) and the stripes' phase (w) of the skin's mouth nearest a rest position, by u as MouthSkinNearest takes it.
            float4 MouthSkinAxisNearest(float3 restAt)
            {
                float u = 1.0e4;
                float4 axisPhase = float4(0.0, 0.0, 0.0, 0.0);

                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 mouth = MouthSkinAt(i);

                    if (mouth.w > 0.0)
                    {
                        float ui = MouthSkinU(restAt, i);

                        if (ui < u)
                        {
                            u = ui;
                            axisPhase = i == 0 ? _MouthAxisSkin0 : (i == 1 ? _MouthAxisSkin1 : (i == 2 ? _MouthAxisSkin2 : _MouthAxisSkin3));
                        }
                    }
                }

                return axisPhase;
            }

            // EVOSIM_THEATRE_MOUTH2_BITE (_MouthBiteParams.x, 0 by default): the bite pulse, over MouthBiteDuration seconds from the onset. e(t)
            // rises in MouthBiteAttack seconds (ease-out quad) and falls over the rest (ease-in cubic); zero before the onset and after the
            // pulse, so the idle timer 1e4 gives zero. The flash lifts the lip and the disc toward white by _MouthBiteParams.y * strength * e,
            // and the swell divides the mouth's distance u by 1 + MouthBiteSwell * strength * e, before anything reads it. Colour and normals
            // only; no vertex moves. Nothing here is reached while _MouthBiteParams.x is zero.
            static const float MouthBiteDuration = 0.35;
            static const float MouthBiteAttack = 0.05;
            static const float MouthBiteSwell = 0.25;

            float MouthBitePulse(float t)
            {
                float attack = saturate(t / MouthBiteAttack);
                float release = saturate((t - MouthBiteAttack) / (MouthBiteDuration - MouthBiteAttack));
                float rise = 1.0 - (1.0 - attack) * (1.0 - attack);
                float fall = 1.0 - release * release * release;
                return rise * fall;
            }

            // The skin's bite timer of the mouth nearest a rest position, by u as MouthSkinNearest takes it; 1e4 where the skin has no mouth.
            float MouthSkinBiteNearest(float3 restAt)
            {
                float u = 1.0e4;
                float timer = 1.0e4;

                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 mouth = MouthSkinAt(i);

                    if (mouth.w > 0.0)
                    {
                        float ui = MouthSkinU(restAt, i);

                        if (ui < u)
                        {
                            u = ui;
                            timer = i == 0 ? _MouthBite.x : (i == 1 ? _MouthBite.y : (i == 2 ? _MouthBite.z : _MouthBite.w));
                        }
                    }
                }

                return timer;
            }

            // EVOSIM_THEATRE_MOUTH2_GAPE (_MouthBiteParams.w, 0 by default): the gape before a bite. _MouthBiteGape is the share open, 0 to
            // 1, as TheatreMouthBite works it out from the onsets to come (open over the lead, shut on the onset). The whole mouth widens
            // by MouthBiteGapeSwell times the share, and its throat further by MouthBiteGapeThroat, so the dark of the opening grows inside the
            // lip and behind any teeth. Colour and normals only; no vertex moves. Nothing here is reached while _MouthBiteParams.w is zero.
            static const float MouthBiteGapeSwell = 0.2;
            static const float MouthBiteGapeThroat = 0.3;

            // The skin's gape share of the mouth nearest a rest position, by u as MouthSkinNearest takes it; 0 where the skin has no mouth.
            float MouthSkinBiteGapeNearest(float3 restAt)
            {
                float u = 1.0e4;
                float share = 0.0;

                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 mouth = MouthSkinAt(i);

                    if (mouth.w > 0.0)
                    {
                        float ui = MouthSkinU(restAt, i);

                        if (ui < u)
                        {
                            u = ui;
                            share = i == 0 ? _MouthBiteGape.x : (i == 1 ? _MouthBiteGape.y : (i == 2 ? _MouthBiteGape.z : _MouthBiteGape.w));
                        }
                    }
                }

                return share;
            }
#endif
#if defined(_EVO_MOUTH2) && defined(_EVO_SKIN2)

            // The lip's colour: skin2's margin band for the body's clade (Skin2Margin at full cover, so the band's colour at
            // its own opacity), or, where the clade has none (the first fraction under Skin2MarginAbsent, which is also a
            // greyed body, a corpse and a neck), the body's colour lifted. The clade reaches here in _PatternShift.zw, as
            // it reaches the margin.
            float3 MouthLipColour(float3 body, float3 restMetres)
            {
                [branch] if (_PatternShift.z >= Skin2MarginAbsent)
                {
#if defined(_EVO_SKIN2B)
                    // SKIN2 level 2: the lip takes level 2's band at half cover (blended into the body's colour), so the same hue rule (no cyan complement,
                    // saturation held to Skin2BSaturation times the body's) colours the mouth's ring as the margin.
                    return Skin2MarginB(body, float4(1.0, 1.0, 0.0, 0.0), _PatternShift.zw, restMetres, 0.5);
#endif
                    return Skin2Margin(body, float4(1.0, 0.0, 0.0, 0.0), _PatternShift.zw, restMetres);
                }

                return MouthLifted(body);
            }
#endif
#if defined(_EVO_MOUTH2) && !defined(_EVO_SKIN2)

            // The lip's colour without SKIN2: the body's colour lifted.
            float3 MouthLipColour(float3 body, float3 restMetres)
            {
                return MouthLifted(body);
            }
#endif

#if defined(_EVO_DEBUG_TINT)
            // DEBUG_TINT: the kind's colour (rgb) and how to draw it (w: 0 as usual, 1 flat, 2 the vertex colour), per renderer.
            float4 _DebugTint;

#endif
#if defined(_EVO_DEATH_DRAIN)
            // The death cue's drain (EVOSIM_THEATRE_DEATH_CUE, TheatreDeathCue, which turns the keyword on with its dial): how far a
            // dead body's colour is taken toward a dull mid grey, 0 to 1 eased, per renderer (TheatrePalette.Paint); zero, as every
            // renderer the palette has not drained is, leaves it as it was. The grey is the colour's own luminance times DeathDrainGrey.
            float _DeathDrain;
            static const float DeathDrainGrey = 0.7;
#if defined(_EVO_DEATH_MILK)

            // The death look (EVOSIM_THEATRE_DEATH_LOOK, TheatreDeathCue.Look, which turns _EVO_DEATH_MILK on with its dial): in place
            // of the grey above, dead tissue goes paler and opaque and stays wet. The albedo goes to a warm milk and the light that fell on
            // the body is kept, so the shading and the terminator stay; nothing is added after the light, so the milk is no glow.
            // _DeathMilk is per renderer (TheatrePalette.SetDeathDrain): x the seconds of the run since the death row, y the variant,
            // 1 the milky drain, 2 the blanch with blotches and speckle. The constants are TheatreDeathCue's, kept together by hand.
            float4 _DeathMilk;

            // The milk's luminance, per variant. The palette knows no one luminance for the brightest living guild, so these hold.
            static const float MilkLuminance1 = 0.78;
            static const float MilkLuminance2 = 0.80;

            // The warm tint the milk takes with the hue's leaving, and the share of the body's saturation it keeps.
            static const float3 MilkWarm = float3(1.04, 1.0, 0.90);
            static const float MilkKeepSaturation = 0.25;

            // The seconds over which the hue leaves, per variant.
            static const float MilkHueSeconds1 = 0.5;
            static const float MilkHueSeconds2 = 0.3;

            // The seconds a thick part takes to reach full milk (T), per variant, and the share of variant 1's at which its lift
            // begins (0.3 s of 2.5). A thin part takes MilkThinLag times as long, so the thick parts go first. Variant 2's 0.42 s is
            // the span a smoothstep is 0.8 at 0.3 s of.
            static const float MilkLiftSeconds1 = 2.5;
            static const float MilkLiftSeconds2 = 0.42;
            static const float MilkLiftFrom1 = 0.12;
            static const float MilkThinLag = 2.0;

            // The most the colour's chroma is scaled by when its luminance is lifted, so a very dark body's faint chroma is not made a hue.
            static const float MilkChromaScaleMax = 8.0;

            // The light a body took, as a multiple of the light its albedo would give, is kept to this much; the albedo floor keeps a
            // dark body's ratio from running away.
            static const float MilkLightMax = 4.0;
            static const float MilkAlbedoFloor = 0.05;

            // Variant 2's blotches: the cycles of value noise across the part's longest side, the noise level above which a blotch is
            // (value noise in [0, 1] has a standard deviation near 0.17, so 0.59 covers about three tenths of the area) and the width
            // of its edge, the own hue's saturation and luminance kept inside, and the share they fade to over BlotchFadeSeconds.
            static const float MilkBlotchCycles = 3.0;
            static const float MilkBlotchLevel = 0.59;
            static const float MilkBlotchEdge = 0.05;
            static const float MilkBlotchSaturation = 0.7;
            static const float MilkBlotchLuminance = 0.75;
            static const float MilkBlotchFadeTo = 0.5;
            static const float MilkBlotchFadeSeconds = 3.0;

            // Variant 2's speckle: the cycles of value noise across the part's longest side, the level above which a dot is, the width
            // of its edge, the share of the own hue's colour a dot keeps, and the share of a cell a dot spans (for the pixel fade).
            static const float MilkSpeckleCycles = 40.0;
            static const float MilkSpeckleLevel = 0.85;
            static const float MilkSpeckleEdge = 0.03;
            static const float MilkSpeckleLuminance = 0.5;
            static const float MilkSpeckleSpan = 0.3;

            // The milk of a dead body: the light the body took (lit) on the albedo it would have had dead, by the fragment's own age
            // and thinness. body is the albedo the light read, thin the part's thinness (the vertex stage's, 1 thin), metres the
            // position in the part's own axes in metres, sizes the part's three lengths, and pixel a pixel's size in metres.
            // Hashed in object space from the body's _Individual alone, so a film draws the same death every time.
            float3 DeathMilk(float3 lit, float3 body, float thin, float3 metres, float3 sizes, float pixel)
            {
                float age = _DeathMilk.x;
                bool blanch = _DeathMilk.y > 1.5;

                float hueK = smoothstep(0.0, blanch ? MilkHueSeconds2 : MilkHueSeconds1, age);
                float liftSeconds = (blanch ? MilkLiftSeconds2 : MilkLiftSeconds1) * lerp(1.0, MilkThinLag, saturate(thin));
                float lift = smoothstep(blanch ? 0.0 : MilkLiftFrom1, 1.0, saturate(age / liftSeconds));
                float weight = max(hueK, lift);

                // The milk: the body's colour with its hue drawn toward its own luminance (to MilkKeepSaturation of it), rescaled to
                // the luminance the lift has reached, and warmed.
                float lumB = dot(body, LumaWeights);
                float lumM = lerp(lumB, blanch ? MilkLuminance2 : MilkLuminance1, lift);
                float3 chroma = body - lumB.xxx;
                float3 milk = lumM.xxx + chroma * (lerp(1.0, MilkKeepSaturation, hueK) *
                    min(lumM / max(lumB, 1e-3), MilkChromaScaleMax));
                milk *= lerp(float3(1.0, 1.0, 1.0), MilkWarm, hueK);

                [branch] if (blanch)
                {
                    float longest = max(max(sizes.x, sizes.y), max(sizes.z, 0.02));
                    float3 at = metres / longest;
                    float3 seed = float3(frac(_Individual * 53.1 + 0.3), frac(_Individual * 29.7 + 0.7), frac(_Individual * 71.3 + 0.1)) * 61.0;
                    float3 gradient;

                    // Blotches: the body's own hue, at MilkBlotchSaturation of its saturation and MilkBlotchLuminance of its luminance,
                    // where the low noise is high; they fade to MilkBlotchFadeTo of themselves, and come in with the milk.
                    float blotch = smoothstep(MilkBlotchLevel - MilkBlotchEdge, MilkBlotchLevel + MilkBlotchEdge,
                        EvoValueNoise(at * MilkBlotchCycles + seed, gradient));
                    blotch *= lerp(1.0, MilkBlotchFadeTo, smoothstep(0.0, MilkBlotchFadeSeconds, age)) * weight;
                    milk = lerp(milk, MilkBlotchLuminance * (lumB.xxx + chroma * MilkBlotchSaturation), blotch);

                    // Speckle: dark dots at MilkSpeckleLuminance of the own hue where the fine noise is highest, standing in for relaxed
                    // chromatophores; faded where a dot is finer than the pixel.
                    float speckle = smoothstep(MilkSpeckleLevel, MilkSpeckleLevel + MilkSpeckleEdge,
                        EvoValueNoise(at * MilkSpeckleCycles + seed.zxy + 17.0, gradient));
                    speckle *= saturate(1.0 - pixel / (MilkSpeckleSpan * longest / MilkSpeckleCycles)) * weight;
                    milk = lerp(milk, MilkSpeckleLuminance * body, speckle);
                }

                // The light the body took, kept: the milk takes the same multiple of its albedo the light gave the body.
                float taken = min(dot(lit, LumaWeights) / max(lumB, MilkAlbedoFloor), MilkLightMax);
                return lerp(lit, max(milk, 0.0) * taken, weight);
            }

            // Variant 3, the corpse that takes the water (_DeathMilk.y 3). The milk above warms the albedo and lights it with the light's
            // brightness alone, so it ends cream-white against the water's blue; and its clock is per part, so a part's edge shows.
            // Here the albedo goes to a dull grey that keeps a ghost of the body's own marks, and is lit by the water's own light in
            // colour, as that light colours a grey card at the fragment; and one front per body carries the change from the body's
            // extremities inward. Strong to start (D262). The constants are TheatreDeathCue's, kept together by hand.
            //
            // _DeathFront is per renderer (TheatrePalette.SetDeathDrain), the same on every renderer of a body: xyz the body's centre in
            // the world, w its largest reach from there, metres.
            float4 _DeathFront;

            // The end albedo luminance, before the ghost of the marks: 0.13, so the corpse shows near 0.55 under the close shot's key
            // and the film's fill light (0.55 showed at 0.85, as bright as the milk; 0.22, keyed before the fill, showed at 0.70).
            static const float MilkLuminance3 = 0.13;

            // The seconds over which the hue leaves, all at once over the body.
            static const float MilkHueSeconds3 = 0.5;

            // The seconds the front takes from the extremities (r 1) to the centre (r 0), after the hue.
            static const float MilkFrontSeconds3 = 3.5;

            // The width of the front's soft edge, as a share of the body's largest reach.
            static const float MilkFrontEdge3 = 0.5;

            // The share of the marks' living contrast (pattern, mottle, bands, countershade) the dead albedo keeps.
            static const float MilkGhostContrast3 = 0.33;

            // The most a mark may stand above the plain albedo it is read against, as a ratio, so a pale lining is no flare.
            static const float MilkGhostRatioMax3 = 2.0;

            // How far the albedo is pulled, behind the front, toward the grey of its own luminance, which the water's light then tints.
            static const float MilkTintPull3 = 0.6;

            // The trial dials of the corpse (TheatreDeathCue.PaleShade, PaleWater), globals set only with the death look 3 on, 0 to 1.
            // _EvoDeathPaleShade: how far the corpse's light falls off on the side away from the key (1 is a plain Lambert key for it,
            // with the ambient and the fill kept).
            // _EvoDeathPaleWater: how far the corpse's colour is drawn toward the water's own colour at its height, darker as it goes.
            // _EvoDeathPaleHue: how much of the water's chroma that colour takes (1 all of it, as pale11b; less leaves it greyer).
            // _EvoDeathPaleDim: the exponent of the pull toward the water's luminance at PaleWater 1 (0.5 the geometric mean, as
            // pale11b; less leaves the corpse brighter and its lit and dark sides further apart).
            float _EvoDeathPaleShade;
            float _EvoDeathPaleWater;
            float _EvoDeathPaleHue;
            float _EvoDeathPaleDim;

            // The corpse of variant 3: the light the body took (lit) on the albedo it would have had dead, by the body's clock and the
            // fragment's place on the body. body is the albedo the light read, plain the albedo before the body's marks were laid on it,
            // positionWS the fragment's place in the world, and card the diffuse light that fell on the fragment (the key, wrapped, and
            // the ambient, both at the body's depth): the light a grey card there would take, whose colour is the water's.
            // Two trial terms, both off at 0 (the arithmetic is then today's): keyLoss is the key's light the wrap lends the side away
            // from it, taken back from the light the body took (0 when _EvoDeathPaleShade is 0), so that side goes darker and keeps
            // the ambient and the fill; and _EvoDeathPaleWater draws the corpse's colour toward the water's own hue at its height, and
            // its luminance toward the water's.
            float3 DeathMilk3(float3 lit, float3 body, float3 plain, float3 positionWS, float3 card, float keyLoss)
            {
                float age = _DeathMilk.x;

                // The hue leaves first, over the whole body at once; then the front, one per body, from r 1 to r 0 with a soft edge.
                float hueK = smoothstep(0.0, MilkHueSeconds3, age);
                float r = length(positionWS - _DeathFront.xyz) / max(_DeathFront.w, 1e-3);
                float front = lerp(1.0 + 0.5 * MilkFrontEdge3, -0.5 * MilkFrontEdge3, saturate((age - MilkHueSeconds3) / MilkFrontSeconds3));
                float lift = smoothstep(front - 0.5 * MilkFrontEdge3, front + 0.5 * MilkFrontEdge3, r);
                float weight = max(hueK, lift);

                // The ghost of the marks: how far this fragment's albedo stands from the plain albedo under it, kept at a third.
                float lumB = dot(body, LumaWeights);
                float mark = min(lumB / max(dot(plain, LumaWeights), MilkAlbedoFloor), MilkGhostRatioMax3);
                float lumEnd = MilkLuminance3 * (1.0 + (mark - 1.0) * MilkGhostContrast3);

                // The dead albedo: the hue drawn toward the body's own luminance (to MilkKeepSaturation of it), rescaled to the luminance
                // the front has reached, then pulled behind the front toward the grey of that luminance. No warm tint.
                float lumM = lerp(lumB, lumEnd, lift);
                float3 chroma = body - lumB.xxx;
                float3 dead = lumM.xxx + chroma * (lerp(1.0, MilkKeepSaturation, hueK) * min(lumM / max(lumB, 1e-3), MilkChromaScaleMax));
                dead = lerp(dead, lumM.xxx, MilkTintPull3 * lift);

                // The light the body took, kept in brightness as the milk keeps it, and in colour as the water's light colours a grey
                // card here (card over its own luminance), so the corpse takes the water's colour and is never brighter than its light.
                float taken = min(dot(lit, LumaWeights) / max(lumB, MilkAlbedoFloor), MilkLightMax);
                // Where no diffuse light falls at all (lit is then the glow or the caustic alone), the light is taken as white.
                float3 cardLight = max(card, 0.0);
                float cardLuminance = dot(cardLight, LumaWeights);
                float3 tint = cardLuminance > 1e-4 ? cardLight / cardLuminance : float3(1.0, 1.0, 1.0);
                float3 deadLit = max(dead, 0.0) * tint * max(taken - keyLoss, 0.0);
                [branch] if (_EvoDeathPaleWater > 0.0)
                {
                    // The water's hue, and its luminance as well: a white disk lowered into the sea goes pale blue-green and
                    // darker, not a bright cyan (the coordinator's note on pale11, 2026-11-04). At 1 the corpse's luminance is
                    // its own to the power 1 - dim times the water's to the power dim, where it is brighter than the water (dim
                    // 0.5 the geometric mean); its hue is the water's, carrying _EvoDeathPaleHue of the water's chroma. The hue
                    // and white both have luminance 1, so the mix keeps the luminance. pale11b's flat cyan ball came from the
                    // full chroma and from dim 0.5, which halves the lit and dark sides' difference in stops.
                    float3 water = EvoWaterColour(positionWS.y);
                    float lumWater = max(dot(water, LumaWeights), 1e-4);
                    float3 waterHue = lerp(float3(1.0, 1.0, 1.0), water / lumWater, _EvoDeathPaleHue);
                    float lumDead = dot(deadLit, LumaWeights);
                    float lumTo = lumDead > lumWater ? lumDead * pow(lumWater / max(lumDead, 1e-4), _EvoDeathPaleDim * _EvoDeathPaleWater) : lumDead;
                    deadLit = lerp(deadLit, lumTo * waterHue, _EvoDeathPaleWater);
                }
                return lerp(lit, deadLit, weight);
            }
#endif

#endif
#if defined(_EVO_LOOK_STAGE)
            // LOOK_STAGE (EVOSIM_THEATRE_LOOK_STAGE, TheatreSkin.EnsureLookStage): the stage a producer's pixel is returned at, 1 to 3.
            // A global, outside the buffer, set only with the dial on.
            float _EvoLookStage;
#endif
#if defined(_EVO_LOOK_PROBE)
            // LOOK_PROBE (EVOSIM_THEATRE_LOOK_PROBE, TheatreSkin.EnsureLookProbe): which term the probe switches, 1 or 2.
            // A global, outside the buffer, set only with the dial on.
            // LOOK_PROBE 3 begin
            // 3: every thallus draws a flat colour by its form (sea lettuce red, Padina green, Codium blue).
            // LOOK_PROBE 3 end
            float _EvoLookProbe;
#endif
            half4 Fragment(Varyings input) : SV_Target
            {
#if defined(_EVO_DEBUG_TINT)
                // DEBUG_TINT (TheatreDebugTint): one flat colour by the renderer's kind, before any light, fog or mark; w 2 is the one
                // skin, which carries its debug colour per vertex (in depthColour.yzw, as the skin's colour always is).
                [branch] if (_DebugTint.w > 1.5) return half4(input.depthColour.yzw, 1.0);
                [branch] if (_DebugTint.w > 0.5) return half4(_DebugTint.rgb, 1.0);
#endif
#if defined(_EVO_ALGA_ARC_DRIVE) && defined(_EVO_ALGA_ARC) && defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                // The arc diagnostic (modes 2 and 3): the false colour from the vertex stage, on a leaf (leaf.w is above one on a
                // blade and not above a half on a skin or a thallus) whose colour the arc wrote (it is never below 0.004 when it
                // did), before any light, fog or mark; any other pixel is drawn as it always was.
                [branch] if (_EvoAlgaArcDrive > 1.5 && input.leaf.w > 0.5 && dot(input.depthColour.yzw, float3(1.0, 1.0, 1.0)) > 0.0)
                    return half4(input.depthColour.yzw, 1.0);
#endif
                // How many metres a pixel spans on this surface, for the papillae and the frill to fade out
                // by. Worked out here, in the open, since a screen derivative is kept out of branches; it is
                // read only inside the tests of _Papillae and _Frill.
                float pixelMetres = length(fwidth(input.positionWS));

                // A one skin (EVOSIM_THEATRE_ONE_SKIN; the shape channel's x above 2.5). Its markings read the rest
                // frame: the position by the vertex stage's substitution (Vertex), the plane a marking is laid on by
                // the rest normal, and a slope's way into the world by the rest frame's axes (SkinRestAxes). The
                // derivatives are taken in the open for the reason above, and read only behind the skin's test.
                bool skinMesh = input.shape.x > 2.5;
                float3 restDx = ddx(input.positionOS);
                float3 restDy = ddy(input.positionOS);
                float3 worldDx = ddx(input.positionWS);
                float3 worldDy = ddy(input.positionWS);
#if defined(_EVO_PATTERN_AA)
#if defined(_EVO_SKIN2)
                // PATTERN_AA: the footprint's measure for PatternSkin2, from the derivatives above, in the open; 0 on a part.
                // Under SKIN2 as well, where PatternAaMetres, PatternAaFootprint and PatternSkin2 are declared.
                PatternAaMetres = skinMesh ? PatternAaFootprint(worldDx, worldDy, pixelMetres, _EvoPatternAa) : 0.0;
#endif
#endif

                bool skinFrame = false;
                float3 restNormal = float3(0.0, 0.0, 1.0);
                float3 skinAxisX = float3(1.0, 0.0, 0.0);
                float3 skinAxisY = float3(0.0, 1.0, 0.0);
                float3 skinAxisZ = float3(0.0, 0.0, 1.0);

                [branch] if (skinMesh)
                {
                    float3 restSizes = PartSizes();
                    restNormal = input.tissueNormal.yzw * rsqrt(max(dot(input.tissueNormal.yzw, input.tissueNormal.yzw), 1e-12));
                    skinFrame = SkinRestAxes(restDx * restSizes, restDy * restSizes, worldDx, worldDy,
                        restNormal, normalize(input.normalWS), skinAxisX, skinAxisY, skinAxisZ);
                }

                // A blade under the leaf dial (_LeafLook) is carved by LeafCarve; one flag, as the vertex stage's.
                bool leafSoft = (input.leaf.w > 0.5 && _LeafLook > 0.5) || _Velvet > 0.5;

                float3 n = CarvedNormal(
                    normalize(input.normalWS), input.positionOS, input.depthColour.x, input.tissueNormal.x, leafSoft ? 1.0 : 0.0);

                // Soft silhouettes: the carved normal turned back toward the uncarved one, more so near
                // a joint anchor, by the same pinch the carve deepens with there. The carve tilts a
                // normal by under sixty degrees (CarvedNormal's bound), so the blend cannot vanish.
                [branch] if (_Soft > 0.5)
                {
                    float seam = saturate(EvoPinch(input.positionOS, _PinchA) + EvoPinch(input.positionOS, _PinchB));
                    n = normalize(lerp(n, normalize(input.normalWS), lerp(SoftSmooth, SoftSeamSmooth, seam)));
                }

                // One smooth hull: turned back again, on what the soft dial left, by the same pinch.
                [branch] if (_SoftHull > 0.5)
                {
                    float hullSeam = saturate(EvoPinch(input.positionOS, _PinchA) + EvoPinch(input.positionOS, _PinchB));
                    n = normalize(lerp(n, normalize(input.normalWS), lerp(HullSmooth, HullSeamSmooth, hullSeam)));
                }
#if defined(_ONE_SKIN)

                // A one skin at level 3 (_SkinCarve): the normal rebuilt from the carve's gradient. The field is read at
                // the interpolated rest position (the position over the column lengths, times them again), the gradient
                // is in rest metres once divided by the body-wide length, and reaches the world through the rest frame's
                // axes (SkinRestAxes), as the papillae's slope does. The height is the depth in world metres times the
                // field, inward, so the slope is over the metres in the world of one rest metre (_SkinScale) as well.
                // Turned and bounded as CarvedNormal's is. The slope's length is kept (skinCarveReach) for the sheen
                // below: where the carve tilts the normal strongly the tight lobe catches the noisy normals and
                // sparkles, so the sheen is spread and lowered there.
                float skinCarveReach = 0.0;
                [branch] if (_SkinCarve > 0.5)
                {
                    [branch] if (skinFrame)
                    {
                        float skinCarveLength = max(_SkinCarveLength, 1e-6);
                        float3 skinRest = input.positionOS * PartSizes();
                        float3 skinCarveGradient;

                        SkinCarveField(skinRest / skinCarveLength + OwnShift(), input.leaf.x, input.leaf.z, input.shape.y,
                            skinCarveGradient);

                        float3 skinSlope =
                            (skinCarveGradient.x * skinAxisX + skinCarveGradient.y * skinAxisY + skinCarveGradient.z * skinAxisZ) *
                            (-input.leaf.y / (skinCarveLength * (_SkinScale > 0.0 ? _SkinScale : 1.0)));

                        float3 skinAlong = skinSlope - n * dot(skinSlope, n);
                        float skinReach = length(skinAlong);
                        skinCarveReach = skinReach;
                        skinAlong *= rsqrt(1.0 + skinReach * skinReach / (1.4 * 1.4));

                        n = normalize(n - skinAlong);
                    }
                }
#endif

#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                // The blade's finer margin ruffle, in the normal (AlgaBladeRuffleNormal). The derivatives of the leaf coordinates are
                // taken in the open, as the others are; the bend itself is behind the test, on a living or dead frond alone.
                float2 bladeDs = float2(ddx(input.leaf.x), ddy(input.leaf.x));
                float2 bladeDt = float2(ddx(input.leaf.z), ddy(input.leaf.z));

                [branch] if (_Alga > 0.001 && input.leaf.w > 0.5 && AlgaIsFrond())
                    n = AlgaBladeRuffleNormal(n, normalize(input.normalWS), input.leaf, bladeDs, bladeDt, worldDx, worldDy);

#endif
                // Cell view (_CellView, EVOSIM_THEATRE_CELL_VIEW), a diagnostic: the part's cell-type colour, nearly
                // unlit and outside the water, so a colour reads the same near and far and in shade. The key's wrapped
                // diffuse only shades it between CellViewShadeFloor and one, enough for the shape to read; no
                // ambient, light colour, fog, depth light or water absorption, and none of the tissue's looks. Alpha
                // as the rest of the fragment sets it. The frame's grade (exposure, neutral tonemap) still applies.
                [branch] if (_CellView > 0.5)
                {
                    Light cellMain = GetMainLight();
                    float cellShade = lerp(CellViewShadeFloor, 1.0, Wrapped(n, cellMain.direction));
                    return half4(_CellColour.rgb * (cellShade * CellViewExposureLift), _Fade);
                }

                // Papillae (the bold tissue looks): small raised bumps, one to each feature point of a jittered
                // grid in the part's own metres, so they stay on the tissue as it moves and as the body turns.
                // They tilt the normal as a dome would, on top of whatever the soft dial left, and mark the
                // colour below: a paler tip and a darker base. Never a displacement, so they cannot leave the
                // collider. Faded out where the spacing is under a few pixels, so a far body does not shimmer.
                float papTip = 0.0;
                float papBase = 0.0;

                [branch] if (_Papillae > 0.001)
                {
                    float cells = max(_PapillaeCellsPerMetre, 1.0);
                    float spacingPixels = (1.0 / cells) / max(pixelMetres, 1e-6);
                    float papFade = saturate(_Papillae) * smoothstep(PapillaFadeLow, PapillaFadeHigh, spacingPixels);

                    [branch] if (papFade > 0.001)
                    {
                        float3 sizes = PartSizes();
                        float3 p = input.positionOS * sizes * cells + _Individual * float3(37.1, 17.3, 53.7);

                        float papHeight, papTone;
                        float3 papGradient;
                        PapillaField(p, papHeight, papGradient, papTone);

                        // The height's slope in the part's own axes, a rise over a run, in metres whichever the
                        // axis, turned into world axes by the columns' directions and bounded as the carve's
                        // slope is, then taken along the surface only.
                        float3 slopeOS = papGradient * (PapillaRelief * papFade);
                        float3 slopeWS =
                            slopeOS.x * (UNITY_MATRIX_M._m00_m10_m20 / max(sizes.x, 1e-6)) +
                            slopeOS.y * (UNITY_MATRIX_M._m01_m11_m21 / max(sizes.y, 1e-6)) +
                            slopeOS.z * (UNITY_MATRIX_M._m02_m12_m22 / max(sizes.z, 1e-6));

                        // On a one skin the slope is in rest metres, and reaches the world by the rest frame's axes.
                        [branch] if (skinFrame)
                            slopeWS = slopeOS.x * skinAxisX + slopeOS.y * skinAxisY + slopeOS.z * skinAxisZ;

                        slopeWS -= n * dot(slopeWS, n);
                        slopeWS *= rsqrt(1.0 + dot(slopeWS, slopeWS) / (PapillaSlopeBound * PapillaSlopeBound));

                        // The height rises toward a papilla's middle, so the normal leans away from it.
                        n = normalize(n - slopeWS);

                        papTip = papFade * smoothstep(0.3, 0.95, papHeight) * (0.6 + 0.4 * papTone);
                        papBase = papFade * (1.0 - smoothstep(0.0, 0.5, papHeight));
                    }
                }

                // Pores (the soft tissue, a box part under _OrganicTissue): fine dimples in the normal and a
                // slight darkening, shading alone, faded out where their spacing falls under a few pixels. The
                // grid is PapillaField's, at a seed of its own so a pore is not under a papilla's feature point.
                float poreShade = 0.0;

                [branch] if (input.tissueNormal.x > 0.5)
                {
                    float poreSpacingPixels = (1.0 / TissuePoreCellsPerMetre) / max(pixelMetres, 1e-6);
                    float poreFade = smoothstep(TissuePoreFadeLow, TissuePoreFadeHigh, poreSpacingPixels);

                    [branch] if (poreFade > 0.001)
                    {
                        float3 poreSizes = PartSizes();
                        float3 poreP = input.positionOS * poreSizes * TissuePoreCellsPerMetre +
                                       _Individual * float3(29.7, 43.1, 13.9) + float3(71.3, 19.9, 37.1);

                        float poreHeight, poreTone;
                        float3 poreGradient;
                        PapillaField(poreP, poreHeight, poreGradient, poreTone);

                        // The slope in the part's own axes, turned into world axes and bounded as the
                        // papillae's is. A dimple falls toward its middle, so the normal leans toward it.
                        float3 poreSlopeOS = poreGradient * (TissuePoreRelief * poreFade);
                        float3 poreSlopeWS =
                            poreSlopeOS.x * (UNITY_MATRIX_M._m00_m10_m20 / max(poreSizes.x, 1e-6)) +
                            poreSlopeOS.y * (UNITY_MATRIX_M._m01_m11_m21 / max(poreSizes.y, 1e-6)) +
                            poreSlopeOS.z * (UNITY_MATRIX_M._m02_m12_m22 / max(poreSizes.z, 1e-6));
                        poreSlopeWS -= n * dot(poreSlopeWS, n);
                        poreSlopeWS *= rsqrt(1.0 + dot(poreSlopeWS, poreSlopeWS) / (PapillaSlopeBound * PapillaSlopeBound));

                        n = normalize(n + poreSlopeWS);

                        poreShade = poreFade * smoothstep(0.2, 0.9, poreHeight);
                    }
                }

                // Velvet absorbers (_Velvet; the constants and the reasons are with Velvet*): an osculum, then the
                // pores, shading alone. What they leave for the colour below is the pores' darkness and lip, the
                // osculum's lip and the share of the light the osculum's hole keeps.
                float velvetPore = 0.0;
                float velvetLip = 0.0;
                float velvetOscLip = 0.0;
                float velvetShade = 1.0;

                // Level 2's star systems (VelvetStars): zooids, surround, line, pore. Zero below level 2.
                float4 velvetStar = float4(0.0, 0.0, 0.0, 0.0);

                [branch] if (_Velvet > 0.5)
                {
                    float3 vSizes = PartSizes();
                    float3 vAxisX = UNITY_MATRIX_M._m00_m10_m20 / max(vSizes.x, 1e-6);
                    float3 vAxisY = UNITY_MATRIX_M._m01_m11_m21 / max(vSizes.y, 1e-6);
                    float3 vAxisZ = UNITY_MATRIX_M._m02_m12_m22 / max(vSizes.z, 1e-6);

                    // On a one skin, the rest frame's axes (TheatreOneSkin zeroes _Velvet on the skin today, so this
                    // waits for the day it does not).
                    [branch] if (skinFrame)
                    {
                        vAxisX = skinAxisX;
                        vAxisY = skinAxisY;
                        vAxisZ = skinAxisZ;
                    }
                    float3 vGeometric = normalize(input.normalWS);

                    // The osculum: a distance from its centre in the radius's units, in metres along the part's
                    // own axes, so it stays on the tissue as the part moves.
                    float oscClear = 1.0;

                    [branch] if (_VelvetOsculum.w > 0.0)
                    {
                        float oscRadius = _VelvetOsculum.w;
                        float u = length((input.positionOS - _VelvetOsculum.xyz) * vSizes) / oscRadius;
                        float oscFade = smoothstep(VelvetOsculumFadeLow, VelvetOsculumFadeHigh,
                            oscRadius / max(pixelMetres, 1e-6));

                        oscClear = smoothstep(1.0, VelvetOsculumReach, u);

                        [branch] if (u < VelvetOsculumReach && oscFade > 0.001)
                        {
                            float wall = saturate((u - VelvetOsculumHoleLow) / (VelvetOsculumHoleHigh - VelvetOsculumHoleLow));
                            float hole = 1.0 - wall * wall * (3.0 - 2.0 * wall);
                            float lipT = (u - VelvetOsculumLipAt) / VelvetOsculumLipWidth;
                            float lip = exp(-lipT * lipT);

                            // The profile's slope, rise over run, positive where the surface rises away from the
                            // centre: the wall's, and the lip's on both sides of its crest.
                            float slope = VelvetOsculumLip * lip * (-2.0 * lipT / VelvetOsculumLipWidth) +
                                VelvetOsculumDepth * (6.0 * wall * (1.0 - wall) / (VelvetOsculumHoleHigh - VelvetOsculumHoleLow));

                            // Outward along the surface, from the centre in the world.
                            float3 outward = input.positionWS - TransformObjectToWorld(_VelvetOsculum.xyz);
                            outward -= n * dot(outward, n);
                            outward *= rsqrt(max(dot(outward, outward), 1e-10));

                            float3 tilt = outward * (slope * oscFade);
                            tilt *= rsqrt(1.0 + dot(tilt, tilt) / (PapillaSlopeBound * PapillaSlopeBound));

                            n = normalize(n - tilt);

                            velvetShade = 1.0 - VelvetOsculumDark * hole * oscFade;
                            velvetOscLip = lip * oscFade;
                        }
                    }

                    // The pores, on the part's own metres. A point's distance is taken across the surface alone
                    // (the normal in the part's axes, from the geometric normal), and weighed by how far off the
                    // surface it lies; the pores add, so the sum is continuous wherever two reach one pixel.
                    float poreFade = smoothstep(VelvetPoreFadeLow, VelvetPoreFadeHigh,
                        VelvetPoreSpacing / max(pixelMetres, 1e-6)) * oscClear;

                    // Level 2 puts the star systems in the pores' place (VelvetStars, below).
                    [branch] if (poreFade > 0.001 && _Velvet < 1.5)
                    {
                        float poreRadius = 0.5 * VelvetPoreDiameter / VelvetPoreSpacing;
                        float3 poreP = input.positionOS * vSizes * (1.0 / VelvetPoreSpacing) +
                                       _Individual * float3(23.9, 11.3, 41.7) + float3(53.1, 17.9, 29.3);
                        float3 poreNormal = float3(dot(vGeometric, vAxisX), dot(vGeometric, vAxisY), dot(vGeometric, vAxisZ));
                        [branch] if (skinMesh) poreNormal = restNormal;

                        float3 poreCell = floor(poreP);
                        float3 poreFrac = poreP - poreCell;

                        float3 poreTilt = float3(0.0, 0.0, 0.0);
                        float poreDark = 0.0;
                        float poreLip = 0.0;

                        [unroll]
                        for (int x = -1; x <= 1; x++)
                        {
                            [unroll]
                            for (int y = -1; y <= 1; y++)
                            {
                                [unroll]
                                for (int z = -1; z <= 1; z++)
                                {
                                    float3 offset = float3(x, y, z);
                                    float3 seed = EvoHash3(poreCell + offset + float3(8.1, 3.3, 6.7));
                                    float3 rel = offset + 0.15 + 0.7 * seed - poreFrac;

                                    float off = dot(rel, poreNormal);
                                    float3 across = rel - poreNormal * off;
                                    float dist = length(across);
                                    float pu = dist / poreRadius;
                                    float layer = 1.0 - smoothstep(VelvetPoreLayer, 0.5, abs(off));

                                    [branch] if (pu < VelvetPoreReach && layer > 0.0)
                                    {
                                        float inside = pu < 1.0 ? 1.0 : 0.0;
                                        float q = pu * pu;
                                        float dimple = inside * (1.0 - q) * (1.0 - q);
                                        float lipT = (pu - 1.0) / VelvetPoreLipWidth;
                                        float lip = exp(-lipT * lipT);

                                        // The profile's slope, rise over run: the lip's, and the dimple's wall.
                                        float slope = VelvetPoreLip * lip * (-2.0 * lipT / VelvetPoreLipWidth) +
                                            inside * VelvetPoreDepth * 4.0 * pu * (1.0 - q);

                                        // Positive slope: the surface rises away from the centre, so the normal
                                        // leans toward it, the way rel points.
                                        poreTilt += across * (slope * layer / max(dist, 1e-4));
                                        poreDark += dimple * layer;
                                        poreLip += lip * layer;
                                    }
                                }
                            }
                        }

                        float3 poreTiltWS = (poreTilt.x * vAxisX + poreTilt.y * vAxisY + poreTilt.z * vAxisZ) * poreFade;
                        poreTiltWS -= n * dot(poreTiltWS, n);
                        poreTiltWS *= rsqrt(1.0 + dot(poreTiltWS, poreTiltWS) / (PapillaSlopeBound * PapillaSlopeBound));

                        n = normalize(n + poreTiltWS);

                        velvetPore = saturate(poreDark) * poreFade;
                        velvetLip = saturate(poreLip) * poreFade;
                    }

                    // Level 2: the star systems (VelvetStars), in the pores' place. On each plane the surface faces,
                    // weighed by the geometric normal's component along that plane's axis, so a flat face is one plane
                    // and an edge two; the planes' results are averaged over the weights used.
                    float starFade = smoothstep(VelvetStarFadeLow, VelvetStarFadeHigh,
                        VelvetStarSpacing / max(pixelMetres, 1e-6)) * oscClear;

                    [branch] if (_Velvet > 1.5 && starFade > 0.001)
                    {
                        float3 starFacing = abs(float3(dot(vGeometric, vAxisX), dot(vGeometric, vAxisY), dot(vGeometric, vAxisZ)));
                        [branch] if (skinMesh) starFacing = abs(restNormal);
                        float3 starWeight = pow(max(starFacing, 1e-4), VelvetStarPlanePower);
                        starWeight /= starWeight.x + starWeight.y + starWeight.z;

                        float3 starMetres = input.positionOS * vSizes;
                        float starBase = _Individual * 13.1 + 2.3;
                        float4 starSum = float4(0.0, 0.0, 0.0, 0.0);
                        float starUsed = 0.0;

                        [branch] if (starWeight.x > VelvetStarPlaneSkip)
                        {
                            starSum += starWeight.x * VelvetStars(starMetres.yz, float3(_Individual * 31.7 + 4.1, _Individual * 19.3 + 9.7, starBase));
                            starUsed += starWeight.x;
                        }

                        [branch] if (starWeight.y > VelvetStarPlaneSkip)
                        {
                            starSum += starWeight.y * VelvetStars(starMetres.xz, float3(_Individual * 31.7 + 4.1, _Individual * 19.3 + 9.7, starBase + 7.0));
                            starUsed += starWeight.y;
                        }

                        [branch] if (starWeight.z > VelvetStarPlaneSkip)
                        {
                            starSum += starWeight.z * VelvetStars(starMetres.xy, float3(_Individual * 31.7 + 4.1, _Individual * 19.3 + 9.7, starBase + 14.0));
                            starUsed += starWeight.z;
                        }

                        velvetStar = (starSum / max(starUsed, 1e-4)) * starFade;
                    }
                }

                // Kelp relief (the leaf dial at level 2; LeafBull*): rows of blisters either side of the midrib on a
                // blade longer than LeafKelpRatio of its width, in the normal alone. The blade's frame is the
                // vertex stage's (LeafAxes of the same half extents; the base end repeats ShapeLamina's choice),
                // so the blisters' slopes lie along the blade's own length and breadth. Height is taken along the
                // outward normal of the face drawn: a blister on the upper face is a hollow on the lower.
                [branch] if (_LeafLook > 1.5 && input.leaf.w > 0.5)
                {
                    float3 bullHalf = HalfExtentsWS();
                    float3 bullAxisL, bullAxisT, bullAxisW;
                    LeafAxes(bullHalf, bullAxisL, bullAxisT, bullAxisW);

                    float bullLength = dot(bullHalf, abs(bullAxisL));
                    float bullBreadth = dot(bullHalf, abs(bullAxisW));
                    float bullSpacing = LeafBullSpacing * 2.0 * bullLength;
                    float bullFade = smoothstep(LeafKelpRatio, LeafKelpRatio + LeafKelpRamp, bullLength / max(bullBreadth, 1e-6)) *
                        smoothstep(LeafBullFadeLow, LeafBullFadeHigh, bullSpacing / max(pixelMetres, 1e-6));

                    float bullX = input.leaf.x / LeafBullSpacing;
                    float bullAbsT = abs(input.leaf.z);

                    [branch] if (bullFade > 0.001 && bullAbsT > LeafBullIn && bullAbsT < LeafBullOut &&
                        bullX >= ceil(LeafBullFrom / LeafBullSpacing - 1e-3) && bullX < floor(LeafBullTo / LeafBullSpacing + 1e-3))
                    {
                        // The blade's base end, as ShapeLamina picks it.
                        float bullBase = frac(_Carve.x * 13.37 + 0.271) < 0.5 ? -1.0 : 1.0;
                        float bullAnchor = dot(_Leaf.xyz, bullAxisL);
                        if (_Leaf.w > 0.5 && abs(bullAnchor) > 0.05) bullBase = bullAnchor > 0.0 ? 1.0 : -1.0;

                        float3 bullX3 = normalize(UNITY_MATRIX_M._m00_m10_m20);
                        float3 bullY3 = normalize(UNITY_MATRIX_M._m01_m11_m21);
                        float3 bullZ3 = normalize(UNITY_MATRIX_M._m02_m12_m22);
                        float3 dirL = bullAxisL.x * bullX3 + bullAxisL.y * bullY3 + bullAxisL.z * bullZ3;
                        float3 dirT = bullAxisT.x * bullX3 + bullAxisT.y * bullY3 + bullAxisT.z * bullZ3;
                        float3 dirW = bullAxisW.x * bullX3 + bullAxisW.y * bullY3 + bullAxisW.z * bullZ3;

                        // The blister: a squared sine along the blade (cell edges at the whole numbers of bullX) and
                        // across the row, with the row's taper toward the rim. The slopes are the derivatives over pi.
                        float rowY = (bullAbsT - LeafBullIn) / (LeafBullOut - LeafBullIn);
                        float alongHeight = 0.5 - 0.5 * cos(6.2831853 * bullX);
                        float alongSlope = sin(6.2831853 * bullX);
                        float rowHeight = sin(3.14159265 * rowY);
                        rowHeight *= rowHeight;
                        float rowTaper = 1.0 - LeafBullTaper * rowY;
                        float rowSlope = sin(6.2831853 * rowY) * rowTaper - (LeafBullTaper / 3.14159265) * rowHeight;

                        // Metres across one row, from the outline's width there (leaf.y is |t| times it).
                        float bullWidth = max(abs(input.leaf.y) / max(bullAbsT, 1e-3), 0.05);
                        float bullRow = (LeafBullOut - LeafBullIn) * bullBreadth * bullWidth;
                        float bullRatio = min(bullSpacing / max(bullRow, 1e-6), LeafBullAcross);

                        float slopeAlong = LeafBullRelief * rowHeight * rowTaper * alongSlope;
                        float slopeAcross = LeafBullRelief * bullRatio * alongHeight * rowSlope;

                        // Up the blade the s coordinate falls from the base end, and the row's |t| rises away from the
                        // midrib on whichever side the pixel lies.
                        float3 bullGradient = slopeAlong * (-bullBase * dirL) +
                            slopeAcross * ((input.leaf.z >= 0.0 ? bullBase : -bullBase) * dirW);

                        float bullSide = dot(normalize(input.normalWS), dirT) >= 0.0 ? 1.0 : -1.0;
                        bullGradient *= bullSide * bullFade;
                        bullGradient -= n * dot(bullGradient, n);
                        bullGradient *= rsqrt(1.0 + dot(bullGradient, bullGradient) / (PapillaSlopeBound * PapillaSlopeBound));

                        n = normalize(n - bullGradient);
                    }
                }

#if defined(_EVO_NOSE_PIT)
                // The nose pit (D242), in the normal: the carve turned back toward the geometric normal inside the
                // pit so the cup is clean, then the cup's wall and the lip as a height field's tilt, outward from the
                // tip along the surface. The height z(u) = depth (lip(u) - cup(u)) rises outward, so the normal leans
                // away from the uphill, toward the tip, as the papillae's does. Shading only; the vertex stage cut the
                // cup. pitDark, the cup's depth share, is what the colour and the light take below.
                float pitDark = 0.0;

                [branch] if (_Pit.w > 0.0 && !skinMesh && input.leaf.w < 0.5)
                {
                    float3 pitGeometric = normalize(input.normalWS);
                    float pitU = PitU(input.positionOS);
                    float pitCupSlope;
                    float pitCup = PitCup(pitU, pitCupSlope);

                    n = normalize(lerp(n, pitGeometric, PitCarveCalm * PitClean(pitU)));

                    float pitLipT = (pitU - PitLipAt) / PitLipWidth;
                    float pitLipSlope = PitLipHeight * exp(-pitLipT * pitLipT) * (-2.0 * pitLipT / PitLipWidth);

                    // Outward from the tip along the surface, in the world: the offset in the part's axes, taken
                    // to the world by the matrix, less its part along the geometric normal.
                    float3 pitOut = mul((float3x3)UNITY_MATRIX_M, input.positionOS - _Pit.xyz);
                    pitOut -= pitGeometric * dot(pitOut, pitGeometric);
                    pitOut *= rsqrt(max(dot(pitOut, pitOut), 1e-12));

                    float pitFade = smoothstep(PitFadeLow, PitFadeHigh, PitRadiusMetres() / max(pixelMetres, 1e-6));
                    float3 pitTilt = pitOut * (_Pit.w * (pitLipSlope - pitCupSlope) * pitFade);
                    pitTilt *= rsqrt(1.0 + dot(pitTilt, pitTilt) / (PitSlopeBound * PitSlopeBound));

                    n = normalize(n - pitTilt);

                    pitDark = pitCup * saturate(_Pit.w / PitDeepRef);
                }
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)

                // The nose pit on the one skin (EVOSIM_THEATRE_NOSE_PIT=2), in the normal: the carve turned back toward the
                // geometric normal inside the pit, then the cup's wall and the lip as a height field's tilt, outward from the
                // tip along the surface, as the part's is above, read at the rest position over the skin's rest metres and
                // carried to the world by the rest frame's axes. Shading only; the vertex stage cut the cup. pitDark, the
                // cup's depth share times the vertex's weight, is what the colour and the light take below, and noseShare is
                // what the lamellae and the light through take.
                float noseShare = 0.0;

                [branch] if (skinMesh && _PitSkin.w > 0.0)
                {
                    noseShare = saturate(input.pitSkin);

                    float3 noseOffset = input.positionOS * PartSizes() - _PitSkin.xyz;
                    float noseU = length(noseOffset) / _PitSkin.w;

                    [branch] if (noseShare > 0.001 && noseU < NoseSkinReach)
                    {
                        float3 noseGeometric = normalize(input.normalWS);
                        float noseCupSlope;
                        float noseCup = PitCup(noseU, noseCupSlope);

                        n = normalize(lerp(n, noseGeometric, PitCarveCalm * PitClean(noseU) * noseShare));

                        float noseLipT = (noseU - PitLipAt) / PitLipWidth;
                        float noseLipSlope = PitLipHeight * exp(-noseLipT * noseLipT) * (-2.0 * noseLipT / PitLipWidth);

                        // Outward from the tip along the surface, in the world: the offset in the rest axes taken by
                        // SkinRestAxes (none where the derivatives are degenerate, which leaves the colour and the cut and no
                        // tilt), less its part along the geometric normal.
                        float3 noseOut = float3(0.0, 0.0, 0.0);

                        [branch] if (skinFrame) noseOut = noseOffset.x * skinAxisX + noseOffset.y * skinAxisY + noseOffset.z * skinAxisZ;

                        noseOut -= noseGeometric * dot(noseOut, noseGeometric);
                        noseOut *= rsqrt(max(dot(noseOut, noseOut), 1e-12));

                        float noseRadiusMetres = _PitSkin.w * (_SkinScale > 0.0 ? _SkinScale : 1.0);
                        float noseFade = smoothstep(PitFadeLow, PitFadeHigh, noseRadiusMetres / max(pixelMetres, 1e-6));
                        float noseDepth = _PitSkinDepth / _PitSkin.w;
                        float3 noseTilt = noseOut * (noseDepth * (noseLipSlope - noseCupSlope) * noseFade * noseShare);
                        noseTilt *= rsqrt(1.0 + dot(noseTilt, noseTilt) / (PitSlopeBound * PitSlopeBound));

                        n = normalize(n - noseTilt);

                        pitDark = noseCup * saturate(noseDepth / PitDeepRef) * noseShare;
                    }
                }
#endif

#endif
#if defined(_EVO_SKIN2)

                // SKIN2's relief on the one skin, in the normal alone. (1) A second, finer octave of value noise of the rest
                // position, as the papillae tilt it: the noise's slope in rest axes, carried to the world by the rest
                // frame's axes (SkinRestAxes), bounded as the carve's is, fading out as its wavelength falls under a few
                // pixels. (2) The crease at a joint as a groove: the height is minus marks.w in world metres, and its
                // screen derivatives (taken in the open, as the others are) tilt the normal by Mikkelsen's surface
                // gradient. Shading alone: no vertex moves.
                float skin2Height = -input.skin2.w * (_SkinScale > 0.0 ? _SkinScale : 1.0);
#if defined(_EVO_SKIN_JOIN)
                // SKIN_JOIN: the crease's groove at JoinCreaseKeep of its depth, before its derivatives are taken.
                skin2Height *= JoinCreaseKeep;
#endif
                float skin2HeightDx = ddx(skin2Height);
                float skin2HeightDy = ddy(skin2Height);

                [branch] if (skinMesh)
                {
                    float skin2Length = max(_SkinCarveLength, 1e-4) * Skin2ReliefWavelength;
                    float skin2Pixels = skin2Length * (_SkinScale > 0.0 ? _SkinScale : 1.0) / max(pixelMetres, 1e-6);
                    float skin2Fade = smoothstep(Skin2ReliefFadeLow, Skin2ReliefFadeHigh, skin2Pixels);

                    [branch] if (skinFrame && skin2Fade > 0.001)
                    {
                        float3 skin2Seed = float3(_Carve.x * 41.3 + 9.1, _Individual * 7.7 + 23.9, _Carve.x * 2.9 + _Individual * 19.3);
                        float3 skin2Gradient;
                        EvoValueNoise((input.positionOS * PartSizes()) / skin2Length + skin2Seed, skin2Gradient);

                        float3 skin2Slope =
                            (skin2Gradient.x * skinAxisX + skin2Gradient.y * skinAxisY + skin2Gradient.z * skinAxisZ) *
                            (Skin2ReliefHeight * skin2Fade);
                        skin2Slope -= n * dot(skin2Slope, n);
                        skin2Slope *= rsqrt(1.0 + dot(skin2Slope, skin2Slope) / (PapillaSlopeBound * PapillaSlopeBound));

                        n = normalize(n - skin2Slope);
                    }

                    float3 skin2R1 = cross(worldDy, n);
                    float3 skin2R2 = cross(n, worldDx);
                    float skin2Det = dot(worldDx, skin2R1);

                    [branch] if (abs(skin2Det) > 1e-14)
                    {
                        float3 skin2Surface = sign(skin2Det) * (skin2HeightDx * skin2R1 + skin2HeightDy * skin2R2);
                        n = normalize(abs(skin2Det) * n - Skin2CreaseSlope * skin2Surface);
                    }
                }

#endif
#if defined(_EVO_MOUTH2)
                // The mouth's opening (EVOSIM_THEATRE_MOUTH2), in the normal: the carve turned back toward the geometric
                // normal inside the opening so the cup is clean, then the cup's wall and the lip as a height field's tilt,
                // outward from the mouth along the surface, as the nose pit's is. The height z(u) = depth (lip(u) - cup(u))
                // rises outward across the wall and the inner half of the ring, so the normal leans inward there and
                // outward across the outer half: a raised ring. Shading only; the vertex stage cut the cup. mouthThroat, the
                // dark of the throat, mouthLip and mouthOuter, the lip's colour and its highlight, are what the colour and
                // the light take below.
                float mouthU = 1.0e4;
                float mouthShare = 0.0;
                float mouthDepth = 0.0;
                float mouthRadiusMetres = 0.0;
                float3 mouthOffset = float3(0.0, 0.0, 0.0);
                float mouthThroat = 0.0;
                float mouthLip = 0.0;
                float mouthOuter = 0.0;
                float mouthDiscMix = 0.0;
                float mouthDiscTone = 1.0;
                float mouthDiscWhite = 0.0;
                float mouthBite = 0.0;

                // The relief (EVOSIM_THEATRE_MOUTH2_RELIEF): its strength on the round mouth of kinds 0 and 1 (the lip and the wall), on the
                // sucker's cups alone (kind 2, whose throat and floor stay as they are), and on the gape (kind 5), whose slit takes the round
                // lip's and throat's place. mouthLining is the lining colour's share, and
                // mouthOcclude what the light loses for depth. With the dial off all are zero, and every line of the relief is behind a branch
                // or a multiply by exactly one.
                float mouthReliefKind = _MouthRadii.w >= MouthKindStep ? floor(_MouthRadii.w / MouthKindStep) : 0.0;
                float mouthRelief = _MouthDisc.a > 0.0 ? saturate(_MouthRelief) : 0.0;
                float mouthReliefRing = mouthReliefKind < 1.5 ? mouthRelief : 0.0;
                float mouthReliefCups = mouthReliefKind > 1.5 && mouthReliefKind < 2.5 ? mouthRelief : 0.0;
                float mouthReliefGape = mouthReliefKind > 4.5 ? mouthRelief : 0.0;
                float mouthLining = 0.0;
                float mouthOcclude = 0.0;
                float mouthEnamel = 0.0;

                [branch] if (skinMesh)
                {
                    mouthShare = saturate(-input.leaf.w);

                    [branch] if (mouthShare > 0.001 && _Mouth.w > 0.0)
                    {
                        float mouthRadiusRest;
                        MouthSkinNearest(input.positionOS * PartSizes(), mouthU, mouthOffset, mouthRadiusRest);
                        mouthRadiusMetres = mouthRadiusRest * (_SkinScale > 0.0 ? _SkinScale : 1.0);
                        mouthDepth = _Mouth.w;
                    }
                }
                else if (_Mouth.w > 0.0 && input.leaf.w < 0.5)
                {
                    mouthShare = 1.0;
                    mouthU = MouthPartU(input.positionOS);
                    mouthOffset = input.positionOS - _Mouth.xyz;
                    mouthRadiusMetres = MouthPartRadiusMetres();
                    mouthDepth = _Mouth.w;
                }

                // The bite pulse (EVOSIM_THEATRE_MOUTH2_BITE): the swell, mouthU divided before anything below reads it; mouthBite is the
                // strength times e(t), which the flash below reads. Zero strength never enters.
                [branch] if (_MouthBiteParams.x > 0.0 && mouthShare > 0.001)
                {
                    float mouthBiteTimer = skinMesh ? MouthSkinBiteNearest(input.positionOS * PartSizes()) : _MouthBite.x;

                    mouthBite = _MouthBiteParams.x * MouthBitePulse(mouthBiteTimer);
                    mouthU /= 1.0 + MouthBiteSwell * mouthBite;
                }

                // The gape (EVOSIM_THEATRE_MOUTH2_GAPE): the mouth widened as a whole by its share open, before anything below reads u;
                // mouthThroatU is the u the throat reads, widened further. With the gape off both stay as they were and the branch is
                // never entered.
                float mouthBiteGape = 0.0;

                [branch] if (_MouthBiteParams.w > 0.0 && mouthShare > 0.001)
                {
                    mouthBiteGape = saturate(skinMesh ? MouthSkinBiteGapeNearest(input.positionOS * PartSizes()) : _MouthBiteGape.x);

                    [branch] if (mouthBiteGape > 0.0) mouthU /= 1.0 + MouthBiteGapeSwell * mouthBiteGape;
                }

                [branch] if (mouthShare > 0.001 && mouthU < MouthReach)
                {
                    float3 mouthGeometric = normalize(input.normalWS);
                    float mouthCupSlope;
                    float mouthCup = MouthCup(mouthU, mouthCupSlope);

                    n = normalize(lerp(n, mouthGeometric, MouthCarveCalm * MouthClean(mouthU) * mouthShare));

                    // The lip's slope: a sine squared over the ring, d/du of MouthLipHeight sin^2(pi t), t across the ring.
                    float mouthLipT = (mouthU - MouthLipFrom) / MouthLipWidth;
                    float mouthLipSlope = 0.0;

                    [branch] if (mouthLipT > 0.0 && mouthLipT < 1.0)
                    {
                        mouthLipSlope = MouthLipHeight * 3.14159265 * sin(6.2831853 * mouthLipT) / MouthLipWidth;
                    }

                    // The relief: a higher lip and a steeper wall on kinds 0 and 1; on the gape no round lip, its slit has its own.
                    [branch] if (mouthRelief > 0.0)
                    {
                        mouthLipSlope *= (1.0 + MouthReliefLipGain * mouthReliefRing) * (1.0 - mouthReliefGape);
                        mouthCupSlope *= (1.0 + MouthReliefWallGain * mouthReliefRing) * (1.0 - mouthReliefGape);
                    }

                    // Outward from the mouth along the surface, in the world: on a part the offset in the part's axes taken
                    // to the world by the matrix, on the skin the offset in the rest axes taken by SkinRestAxes (none where
                    // the derivatives are degenerate, which leaves the colour and the cut and no tilt); less its part
                    // along the geometric normal.
                    float3 mouthOut = float3(0.0, 0.0, 0.0);

                    [branch] if (skinMesh)
                    {
                        [branch] if (skinFrame) mouthOut = mouthOffset.x * skinAxisX + mouthOffset.y * skinAxisY + mouthOffset.z * skinAxisZ;
                    }
                    else
                    {
                        mouthOut = mul((float3x3)UNITY_MATRIX_M, mouthOffset);
                    }

                    mouthOut -= mouthGeometric * dot(mouthOut, mouthGeometric);
                    mouthOut *= rsqrt(max(dot(mouthOut, mouthOut), 1e-12));

                    float mouthFade = smoothstep(MouthFadeLow, MouthFadeHigh,
                        MouthLipWidth * mouthRadiusMetres / max(pixelMetres, 1e-6));
                    float3 mouthTilt = mouthOut * (mouthDepth * (mouthLipSlope - mouthCupSlope) * mouthFade * mouthShare);
                    mouthTilt *= rsqrt(1.0 + dot(mouthTilt, mouthTilt) / (MouthSlopeBound * MouthSlopeBound));

                    n = normalize(n - mouthTilt);

                    // The throat inside, the lip's ring (soft edges) and the highlight on its outer half.
                    float mouthLipEnd = MouthLipFrom + MouthLipWidth;

                    // The gape's wider throat (EVOSIM_THEATRE_MOUTH2_GAPE); u itself where the gape is shut or off.
                    float mouthThroatU = mouthU;
                    [branch] if (mouthBiteGape > 0.0) mouthThroatU = mouthU / (1.0 + MouthBiteGapeThroat * mouthBiteGape);

                    mouthThroat = (1.0 - smoothstep(MouthThroatIn, MouthThroatOut, mouthThroatU)) * mouthShare;
                    mouthLip = smoothstep(MouthLipFrom - MouthLipEdge, MouthLipFrom + MouthLipEdge, mouthU) *
                        (1.0 - smoothstep(mouthLipEnd - MouthLipEdge, mouthLipEnd + MouthLipEdge, mouthU)) * mouthShare;
                    mouthOuter = smoothstep(MouthLipFrom + 0.5 * MouthLipWidth - MouthLipEdge,
                        MouthLipFrom + 0.5 * MouthLipWidth + 2.0 * MouthLipEdge, mouthU);

                    // The relief's wall: on kinds 0 and 1 the throat held to the floor and the wall in the lining, occluded by its depth;
                    // on the gape the round lip and throat give way to the slit's, set with the kind below.
                    [branch] if (mouthReliefRing > 0.0)
                    {
                        float mouthFloorThroat = (1.0 - smoothstep(MouthReliefThroatIn, MouthReliefThroatOut, mouthThroatU)) * mouthShare;

                        mouthThroat = lerp(mouthThroat, mouthFloorThroat, mouthReliefRing);
                        mouthLining = smoothstep(MouthReliefWallIn - MouthLipEdge, MouthReliefWallIn + MouthLipEdge, mouthU) *
                            (1.0 - smoothstep(MouthLipFrom - MouthLipEdge, MouthLipFrom + MouthLipEdge, mouthU)) * mouthReliefRing * mouthShare;
                        mouthOcclude = mouthLining * mouthCup;
                    }

                    mouthLip *= 1.0 - mouthReliefGape;
                    mouthThroat *= 1.0 - mouthReliefGape;
                }

                // The oral disc (EVOSIM_THEATRE_MOUTH2_DISC), its own reach: radial stripes about the mouth's axis, in the tangent plane's basis
                // built from the axis and a fixed reference (up, or x where the axis is within 0.9 of up), the offset taken in radii so the
                // disc is round. The angle is turned by the phase (w) so a skin's stripes stand where the part drawn alone has them. Sharp
                // edged with a pixel-wide fade, blurred to the mean where a stripe is finer than a pixel; no axis, no stripes.
                // The kind rides in _MouthRadii.w above MouthKindStep (EVOSIM_THEATRE_MOUTH2_KINDS); under it the kind is 0 and the code is
                // today's. The crown alone reaches past the ring's outer edge.
                float discKind = _MouthRadii.w >= MouthKindStep ? floor(_MouthRadii.w / MouthKindStep) : 0.0;
                float discReach = (discKind > 2.5 && discKind < 3.5) ? MouthCrownReach : MouthDiscOuter;
                float discKindShare = -1.0;

                // The relief's gape reaches the end of its slit's lip.
                [branch] if (mouthReliefGape > 0.0) discReach = MouthGapeLong * (1.0 + MouthGapeLip) + MouthLipEdge;

                [branch] if (_MouthDisc.a > 0.0 && mouthShare > 0.001 && mouthU < discReach)
                {
                    float4 discAxisPhase = skinMesh ? MouthSkinAxisNearest(input.positionOS * PartSizes()) : _MouthAxis;
                    float3 discOffset = skinMesh
                        ? mouthOffset / max(mouthRadiusMetres / (_SkinScale > 0.0 ? _SkinScale : 1.0), 1e-6)
                        : mouthOffset / max(_MouthRadii.xyz, float3(1e-6, 1e-6, 1e-6));
                    float discStripe = 1.0;
                    float discAxisLength = dot(discAxisPhase.xyz, discAxisPhase.xyz);

                    [branch] if (discAxisLength > 0.5)
                    {
                        float3 discAxis = discAxisPhase.xyz * rsqrt(discAxisLength);
                        float3 discHelper = abs(discAxis.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                        float3 discBasis1 = normalize(cross(discAxis, discHelper));
                        float3 discBasis2 = cross(discAxis, discBasis1);
                        float discAngle = atan2(dot(discOffset, discBasis2), dot(discOffset, discBasis1)) - discAxisPhase.w;
                        float discCount = _MouthRadii.w > 0.5 ? (discKind > 0.5 ? _MouthRadii.w - MouthKindStep * discKind : _MouthRadii.w) : MouthDiscStripes;
                        float discEdge = clamp(discCount * pixelMetres / max(mouthRadiusMetres * max(mouthU, 0.25), 1e-6), 0.03, 1.0);

                        // The small-mouth dial (EVOSIM_THEATRE_MOUTH2_SMALL) softens the stripes' edges at every size, so even a portrait's
                        // read as soft furrows rather than hard pale blocks.
                        [branch] if (_MouthSmall > 0.0) discEdge = max(discEdge, MouthSmallSoftEdge * saturate(_MouthSmall));

                        discStripe = lerp(0.5, smoothstep(-discEdge, discEdge, cos(discCount * discAngle)), 1.0 - smoothstep(0.5, 1.0, discEdge));

                        // The relief's slope in the kinds' own frame, (px, py) in radii per radius; turned to the world and applied below.
                        float reliefGx = 0.0;
                        float reliefGy = 0.0;
                        float reliefPix = max(pixelMetres / max(mouthRadiusMetres, 1e-6), 0.004);

                        // Kind 0's furrows: the height MouthReliefFurrow cos(count angle) over the disc's ring, its slope round the axis.
                        [branch] if (mouthReliefRing > 0.0 && discKind < 0.5)
                        {
                            float furrowRing = smoothstep(MouthDiscFrom - MouthLipEdge, MouthDiscFrom + MouthLipEdge, mouthU) *
                                (1.0 - smoothstep(MouthDiscOuter - MouthDiscFade * (MouthDiscOuter - MouthDiscFrom), MouthDiscOuter, mouthU));
                            float furrowSlope = -MouthReliefFurrow * mouthReliefRing * discCount * sin(discCount * discAngle) / max(mouthU, 0.25) *
                                furrowRing * (1.0 - smoothstep(0.5, 1.0, discEdge));

                            reliefGx = -sin(discAngle) * furrowSlope;
                            reliefGy = cos(discAngle) * furrowSlope;
                        }

                        // The kinds other than the stripes. discKindShare is their share (negative: not one of them), mouthDiscTone and
                        // mouthDiscWhite their colour. u is the offset in radii, (px, py) the tangent offset about the axis in the stripes' own
                        // frame (the angle already turned by the phase), discPix a pixel in radii.
                        [branch] if (discKind > 0.5)
                        {
                            float discU = max(mouthU, 0.05);
                            float discPix = max(pixelMetres / max(mouthRadiusMetres, 1e-6), 0.004);
                            float discTangent = length(float2(dot(discOffset, discBasis1), dot(discOffset, discBasis2)));
                            float discPx = discTangent * cos(discAngle);
                            float discPy = discTangent * sin(discAngle);
                            float discTurns = discAngle * 0.15915494;
                            float discKeratin = 1.0 / max(max(_MouthDisc.r, _MouthDisc.g), max(_MouthDisc.b, 1e-4));

                            [branch] if (discKind < 1.5)
                            {
                                // Toothed ring: a triangle per tooth, its width (a share of the half pitch, q) growing from the point to the base.
                                float teeth = 2.0 * discCount;
                                float q = abs(frac(discTurns * teeth) - 0.5) * 2.0;
                                float height = MouthLipFrom - MouthToothTip;
                                float toothBase = mouthReliefRing > 0.0 ? lerp(MouthToothBase, MouthReliefToothBase, mouthReliefRing) : MouthToothBase;
                                float grow = toothBase * saturate((mouthU - MouthToothTip) / height);
                                float qEdge = clamp(discPix * teeth * 0.31830989 / discU, 0.02, 1.0);
                                float inside = smoothstep(-qEdge, qEdge, grow - q) * smoothstep(MouthToothTip - discPix, MouthToothTip + discPix, mouthU) *
                                    (1.0 - smoothstep(MouthLipFrom - discPix, MouthLipFrom + discPix, mouthU));

                                discKindShare = lerp(MouthToothMean * step(MouthToothTip, mouthU) * step(mouthU, MouthLipFrom), inside,
                                    1.0 - smoothstep(0.5, 1.0, qEdge));
                                mouthDiscWhite = MouthToothWhite;

                                // The teeth stand inside the throat's darkening (MouthThroatIn to MouthThroatOut), which comes after the disc's
                                // colour, so the throat is eased where a tooth is, as the beak's plates ease it.
                                mouthThroat *= 1.0 - discKindShare;

                                // The relief: a ridge along each tooth's centre line, enamel graded from the gum to the tip, dark gaps between.
                                [branch] if (mouthReliefRing > 0.0)
                                {
                                    float toothF = frac(discTurns * teeth) - 0.5;
                                    float toothFine = 1.0 - smoothstep(0.5, 1.0, qEdge);
                                    float toothSlope = -MouthReliefTooth * mouthReliefRing / max(grow, 0.05) * sign(toothF) * teeth * 0.31830989 /
                                        discU * inside * toothFine;
                                    float toothBand = smoothstep(MouthToothTip - discPix, MouthToothTip + discPix, mouthU) *
                                        (1.0 - smoothstep(MouthLipFrom - discPix, MouthLipFrom + discPix, mouthU));

                                    reliefGx = -sin(discAngle) * toothSlope;
                                    reliefGy = cos(discAngle) * toothSlope;
                                    mouthEnamel = mouthReliefRing;
                                    mouthDiscTone = lerp(1.0, MouthEnamelBase, saturate((mouthU - MouthToothTip) / height) * mouthReliefRing);
                                    mouthThroat = max(mouthThroat, MouthReliefToothGap * mouthReliefRing * toothBand * (1.0 - discKindShare) * toothFine * mouthShare);
                                }
                            }
                            else if (discKind < 2.5)
                            {
                                // Sucker: the row nearest the point, its dots a half step round from the row before.
                                float rowStep = (MouthDiscOuter - MouthDiscFrom) / MouthSuckerRows;
                                float row = clamp(floor((mouthU - MouthDiscFrom) / rowStep), 0.0, MouthSuckerRows - 1.0);
                                float rowRadius = MouthDiscFrom + (row + 0.5) * rowStep;
                                float dots = discCount + MouthSuckerExtra * fmod(row, 2.0);
                                float pitch = 6.2831853 * rowRadius / dots;
                                float across = (frac(discTurns * dots + 0.5 * row) - 0.5) * pitch;
                                float dotRadius = min(MouthSuckerDot * (1.0 + (MouthReliefCupGrow - 1.0) * mouthReliefCups),
                                    (MouthSuckerFill + (MouthReliefCupFill - MouthSuckerFill) * mouthReliefCups) * pitch);
                                float dotEdge = max(discPix, 0.004);
                                float inside = (1.0 - smoothstep(dotRadius - dotEdge, dotRadius + dotEdge, length(float2(across, mouthU - rowRadius)))) *
                                    step(MouthDiscFrom - rowStep * 0.25, mouthU) * step(mouthU, MouthDiscOuter + rowStep * 0.25);

                                discKindShare = lerp(MouthSuckerMean * step(MouthDiscFrom, mouthU) * step(mouthU, MouthDiscOuter), inside,
                                    1.0 - smoothstep(0.5, 1.0, dotEdge / max(dotRadius, 1e-4)));
                                mouthDiscWhite = MouthSuckerWhite;

                                // The relief: each dot a small cup, a rim MouthReliefCupRim radii high (a sine squared over the dot's radius)
                                // round a dimple darkened to MouthReliefCupDark, its rim lit to MouthReliefCupLight, its slope taken round the
                                // dot's centre in (across, radial). Its own strength (mouthReliefCups), so the sucker keeps its dark throat and
                                // lined floor: the round mouth's floor, lining and lip gain are kinds 0 and 1 only.
                                [branch] if (mouthReliefCups > 0.0)
                                {
                                    float radial = mouthU - rowRadius;
                                    float rho = length(float2(across, radial));
                                    float cupFine = 1.0 - smoothstep(0.5, 1.0, dotEdge / max(dotRadius, 1e-4));
                                    float cupT = rho / max(dotRadius, 1e-4);
                                    float cupSlope = cupT < 1.0
                                        ? MouthReliefCupRim * 3.14159265 * sin(6.2831853 * cupT) / max(dotRadius, 1e-4) * mouthReliefCups * cupFine / max(rho, 1e-4)
                                        : 0.0;
                                    float cupA = -sin(discAngle);
                                    float cupB = cos(discAngle);

                                    reliefGx = cupSlope * (across * cupA + radial * cupB);
                                    reliefGy = cupSlope * (across * cupB - radial * cupA);
                                    float cupLit = smoothstep(0.55, 0.75, cupT) * (1.0 - smoothstep(0.9, 1.0, cupT));
                                    mouthDiscTone = lerp(1.0, MouthReliefCupDark, (1.0 - smoothstep(MouthReliefCupHollow - 0.3, MouthReliefCupHollow, cupT)) * inside * mouthReliefCups) *
                                        lerp(1.0, MouthReliefCupLight, cupLit * inside * mouthReliefCups);
                                }
                            }
                            else if (discKind < 3.5)
                            {
                                // Crown: lobes from the lip to the reach, narrowing to a rounded cap.
                                float along = saturate((mouthU - MouthLipFrom) / (MouthCrownReach - MouthLipFrom));
                                float pitch = 3.14159265 * discU / discCount;
                                float lateral = abs(frac(discTurns * discCount) - 0.5) * 2.0 * pitch;
                                float cap = sqrt(saturate(1.0 - pow(along, 8.0)));
                                float halfWidth = MouthCrownBase * 3.14159265 * MouthLipFrom / discCount * (1.0 - MouthCrownTaper * along) * cap;
                                float inside = smoothstep(-discPix, discPix, halfWidth - lateral) *
                                    smoothstep(MouthLipFrom - MouthLipEdge, MouthLipFrom + MouthLipEdge, mouthU);

                                discKindShare = lerp(MouthCrownMean * step(MouthLipFrom, mouthU), inside,
                                    1.0 - smoothstep(0.5, 1.0, discPix / max(halfWidth * 2.0, 1e-4)));
                            }
                            else if (discKind < 4.5)
                            {
                                // Beak: two dark plates out to the lip, a pale seam along the first tangent axis.
                                float plates = 1.0 - smoothstep(MouthLipFrom - discPix, MouthLipFrom + discPix, mouthU);
                                float seam = 1.0 - smoothstep(MouthBeakSeam - discPix, MouthBeakSeam + discPix, abs(discPy));

                                discKindShare = plates;
                                mouthDiscTone = lerp(min(MouthBeakValue * discKeratin, 1.0), 1.0, seam);
                                mouthDiscWhite = MouthBeakSeamWhite * seam;
                                mouthThroat *= 1.0 - plates;
                            }
                            else
                            {
                                // Gape: a dark slit, long along the first tangent axis, ringed by a lip of the disc colour.
                                float gapeWide = mouthReliefGape > 0.0 ? lerp(MouthGapeWide, MouthReliefGapeWide, mouthReliefGape) : MouthGapeWide;
                                float slitEdge = clamp(discPix / gapeWide, 0.03, 1.0);
                                float slitR = length(float2(discPx / MouthGapeLong, discPy / gapeWide));
                                float slit = 1.0 - smoothstep(1.0 - slitEdge, 1.0 + slitEdge, slitR);
                                float lipRing = smoothstep(1.0 - slitEdge, 1.0 + slitEdge, slitR) *
                                    (1.0 - smoothstep(1.0 + MouthGapeLip - slitEdge, 1.0 + MouthGapeLip + slitEdge, slitR));

                                discKindShare = lerp(MouthGapeMean, max(slit, lipRing), 1.0 - smoothstep(0.5, 1.0, slitEdge));
                                mouthDiscTone = lerp(1.0, min(MouthGapeValue * discKeratin, 1.0), slit);

                                // The relief: the slit a trench, its wall in the lining and its floor the throat, its lip a raised ring. The
                                // height is a function of the ellipse's radius, so its slope runs along (px / long^2, py / wide^2) / radius.
                                [branch] if (mouthReliefGape > 0.0)
                                {
                                    float gapeFine = 1.0 - smoothstep(0.5, 1.0, slitEdge);
                                    float gapeLipT = (slitR - 1.0) / MouthGapeLip;
                                    float gapeLipSlope = gapeLipT > 0.0 && gapeLipT < 1.0
                                        ? MouthReliefGapeLip * 3.14159265 * sin(6.2831853 * gapeLipT) / MouthGapeLip
                                        : 0.0;
                                    float gapeWallT = saturate((slitR - MouthReliefGapeFloor) / (1.0 - MouthReliefGapeFloor));
                                    float gapeWallSlope = MouthReliefGapeDepth * 6.0 * gapeWallT * (1.0 - gapeWallT) / (1.0 - MouthReliefGapeFloor);
                                    float gapeSlope = (gapeLipSlope + gapeWallSlope) * mouthReliefGape * gapeFine / max(slitR, 0.05);
                                    float gapeFloor = 1.0 - smoothstep(MouthReliefGapeFloor - slitEdge, MouthReliefGapeFloor + slitEdge, slitR);

                                    reliefGx = gapeSlope * discPx / (MouthGapeLong * MouthGapeLong);
                                    reliefGy = gapeSlope * discPy / (gapeWide * gapeWide);
                                    mouthDiscTone = lerp(mouthDiscTone, 1.0, (slit - gapeFloor) * mouthReliefGape);
                                    discKindShare = lerp(discKindShare, lipRing, (slit - gapeFloor) * mouthReliefGape);
                                    mouthLining = max(mouthLining, (slit - gapeFloor) * mouthReliefGape * mouthShare);
                                    mouthOcclude = max(mouthOcclude, (slit - gapeFloor) * (1.0 - gapeWallT) * mouthReliefGape * mouthShare);
                                    mouthThroat = max(mouthThroat, gapeFloor * mouthReliefGape * mouthShare);
                                }
                            }
                        }

                        // The relief's tilt: the slope in (px, py) turned to the world through the stripes' basis (on a part the basis in
                        // radii taken by the matrix, on the skin by the rest axes, none where they are degenerate), off the geometric normal,
                        // bounded as the lip's is and faded as the lip's is on a far body.
                        [branch] if (mouthRelief > 0.0 && (reliefGx != 0.0 || reliefGy != 0.0))
                        {
                            float reliefCos = cos(discAxisPhase.w);
                            float reliefSin = sin(discAxisPhase.w);
                            float3 reliefB1 = discBasis1 * reliefCos + discBasis2 * reliefSin;
                            float3 reliefB2 = discBasis2 * reliefCos - discBasis1 * reliefSin;
                            float3 reliefE1 = float3(0.0, 0.0, 0.0);
                            float3 reliefE2 = float3(0.0, 0.0, 0.0);

                            [branch] if (skinMesh)
                            {
                                [branch] if (skinFrame)
                                {
                                    reliefE1 = reliefB1.x * skinAxisX + reliefB1.y * skinAxisY + reliefB1.z * skinAxisZ;
                                    reliefE2 = reliefB2.x * skinAxisX + reliefB2.y * skinAxisY + reliefB2.z * skinAxisZ;
                                }
                            }
                            else
                            {
                                reliefE1 = mul((float3x3)UNITY_MATRIX_M, reliefB1 * _MouthRadii.xyz);
                                reliefE2 = mul((float3x3)UNITY_MATRIX_M, reliefB2 * _MouthRadii.xyz);
                            }

                            float3 reliefGeometric = normalize(input.normalWS);
                            reliefE1 -= reliefGeometric * dot(reliefE1, reliefGeometric);
                            reliefE2 -= reliefGeometric * dot(reliefE2, reliefGeometric);
                            reliefE1 *= rsqrt(max(dot(reliefE1, reliefE1), 1e-12));
                            reliefE2 *= rsqrt(max(dot(reliefE2, reliefE2), 1e-12));

                            float reliefFade = smoothstep(MouthFadeLow, MouthFadeHigh, MouthLipWidth / reliefPix);

                            // The small-mouth dial fades the furrows and ridges by the mouth's size on screen as it fades the disc's colour,
                            // so a mouth in a world shot shows no teeth in its shading either (phase143's crimson body).
                            [branch] if (_MouthSmall > 0.0)
                                reliefFade *= lerp(1.0, smoothstep(MouthSmallGone, MouthSmallFull, mouthRadiusMetres / max(pixelMetres, 1e-7)), saturate(_MouthSmall));
                            float3 reliefTilt = (reliefGx * reliefE1 + reliefGy * reliefE2) * (reliefFade * mouthShare);
                            reliefTilt *= rsqrt(1.0 + dot(reliefTilt, reliefTilt) / (MouthSlopeBound * MouthSlopeBound));

                            n = normalize(n - reliefTilt);
                        }
                    }

                    float discWidth = MouthDiscOuter - MouthDiscFrom;
                    float discRing = smoothstep(MouthDiscFrom - MouthLipEdge, MouthDiscFrom + MouthLipEdge, mouthU) *
                        (1.0 - smoothstep(MouthDiscOuter - MouthDiscFade * discWidth, MouthDiscOuter, mouthU));

                    mouthDiscMix = (discKindShare >= 0.0 ? discKindShare : discRing * discStripe) * _MouthDisc.a * mouthShare;

                    // The small-mouth dial (EVOSIM_THEATRE_MOUTH2_SMALL): the disc, its stripes and every kind's marks fade out as the mouth
                    // shrinks on screen, full from a radius of MouthSmallFull pixels and gone by MouthSmallGone, so a mouth in a world shot
                    // (15 to 25 px across, where the stripes read as a cog's teeth to the owner on 2026-11-04) is its opening and lip alone.
                    [branch] if (_MouthSmall > 0.0)
                        mouthDiscMix *= lerp(1.0, smoothstep(MouthSmallGone, MouthSmallFull, mouthRadiusMetres / max(pixelMetres, 1e-7)), saturate(_MouthSmall));

                    // The plain mouth (EVOSIM_THEATRE_MOUTH2_PLAIN) draws no stripes: in the body's plain colour they still showed,
                    // as dark smudges beside the maze body's mouth where that colour is the maze's ground (phase145's portrait of 38393).
                    [branch] if (_MouthPlain > 0.0)
                        mouthDiscMix *= 1.0 - saturate(_MouthPlain);
                }

#endif
#if defined(_EVO_SKIN2)
// skin2parts: SKIN2 on a part. A part drawn in animal tissue: not the one skin, not a blade, not velvet, and the flag the
// palette wrote. Its marks and the rest position the asymmetry noise and the margin read (scaled so the skin's own
// noise has the part's wavelength) are made once, here, for the colour and light below.
                bool skin2Part = !skinMesh && _Skin2Part > 0.5 && input.leaf.w < 0.5 && _Velvet < 0.5;
                float4 skin2PartMarks = float4(0.0, 0.0, 0.0, 0.0);
                float3 skin2PartNormal = float3(0.0, 0.0, 1.0);
                float3 skin2PartRest = float3(0.0, 0.0, 0.0);

                [branch] if (skin2Part)
                {
                    float skin2PartLength;
                    skin2PartMarks = Skin2PartMarks(input.positionOS, input.normalWS, input.shape, skin2PartNormal, skin2PartLength);
                    skin2PartRest = input.positionOS * PartSizes() * (max(_SkinCarveLength, 1e-4) / skin2PartLength);
                }

#endif
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));

                float thickness = input.fogAndThickness.y;

                float3 body = _BaseColor.rgb;
#if defined(_EVO_UPTAKE)

                // The absorptive uptake look deepens the body, rim and through-light colours within their own
                // hue (TheatreUptake.hlsl). The two macros below send the later reads of the properties to the
                // deepened copies, and are undefined after the last of them.
                float4 evoRimColour = _RimColor;
                float4 evoTransTint = _TransTint;

                [branch]
                if (_Uptake.x > 0.0)
                {
                    body = EvoUptakeDeepen(body, _Uptake.x);
                    evoRimColour.rgb = EvoUptakeDeepen(evoRimColour.rgb, _Uptake.x);
                    evoTransTint.rgb = EvoUptakeDeepen(evoTransTint.rgb, _Uptake.x);
                }

#define _RimColor evoRimColour
#define _TransTint evoTransTint
#endif

                // A one skin's colour is its vertex's, the merged parts' painted colours blended by the skin's weights
                // (TheatreOneSkin.Blend), in place of the one part's _BaseColor its block carries.
                [branch] if (skinMesh) body = input.depthColour.yzw;
#if defined(_EVO_THALLI)

                // A thallus's tissue (EVOSIM_THEATRE_THALLI): Padina's growth bands and the darker base in the tone (the vertex
                // colour's r), and Padina's limy rim (its g): the body greyed toward its own luminance and lifted there.
                bool thallusMesh = ThalliIs(input.shape.x);
                [branch] if (thallusMesh)
                {
                    body *= input.depthColour.y;
                    body = lerp(body, lerp(body, dot(body, LumaWeights).xxx, ThalliLimeGrey) * ThalliLimeLift, saturate(input.depthColour.z));
                }
#endif

                // A producer's colour by its depth (_AlgaDeep, EVOSIM_THEATRE_ALGA; AlgaDepthTint): darker toward the
                // floor, in the same hue, on the body here and on the light through and the rim below.
                // The rim's colour is copied to rimColour, which is _RimColor itself with the dial off.
                float3 algaTint = float3(1.0, 1.0, 1.0);
                float3 rimColour = _RimColor.rgb;

                [branch] if (_AlgaDeep.w > 0.5)
                {
                    algaTint = AlgaDepthTint();
                    body *= algaTint;
#if defined(_EVO_ALGA_ROUND_PHOTO)
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2) && defined(_EVO_ALGA_RAMP_PHOTO)
                    // ROUND_PHOTO: a living producer that is not a blade, not a swimmer's merged skin and not a thallus (a round
                    // part) takes the multiplier a blade gets at the ramp's mid-green stop (AlgaRoundPhotoTone), v of the way.
                    bool roundPhoto = _AlgaDeep.w > 0.5 && input.leaf.w < 0.5 && !skinMesh;
#if defined(_EVO_THALLI)
                    roundPhoto = roundPhoto && !thallusMesh;
#endif
                    [branch] if (roundPhoto) body *= AlgaRoundPhotoTone(body);
#endif
#endif
                    rimColour *= algaTint;
                }

                // A blade's form under the marine producers' dial (AlgaFormOfLeaf), for its sheen and its light
                // through below; w is the form, 0 a sheet and 1 a strap.
                float4 algaForm = float4(0.0, 0.0, 0.0, 0.0);
                bool algaBlade = _Alga > 0.001 && input.leaf.w > 0.5;
                [branch] if (algaBlade) algaForm = AlgaFormOfLeaf(input.leaf.w);
#if defined(_EVO_DEATH_DRAIN) && defined(_EVO_DEATH_MILK)

                // The death look's variant 3 (DeathMilk3): the albedo before the body's marks are laid on it, which a dead body's marks
                // are read against so a ghost of them is kept. Read only for variant 3.
                float3 deathPlain = body;
#endif

                // The mottle. Scaled in world metres, so a body twice as long carries twice as
                // many cells rather than the same pattern stretched, which is the tell that a
                // texture is following the object rather than the tissue.
                //
                // Under the soft dial the cells are wider and the marking gentler.
                float mottleCells = _MottleCellsPerMetre;
                float mottleStrength = _MottleStrength;

                [branch] if (_Soft > 0.5)
                {
                    mottleCells *= SoftMottleCells;
                    mottleStrength *= SoftMottleStrength;
                }

                // Under the hull fine and faint, on what the soft dial set.
                [branch] if (_SoftHull > 0.5)
                {
                    mottleCells *= HullMottleCells;
                    mottleStrength *= HullMottleStrength;
                }

                float f1, f2, tone;
                float wall;

                // Under the soft mottle the tone is MottleSoft's blend and there is no wall; otherwise the
                // cell's own flat tone and a darker wall where two cells meet. The soft mottle is also read
                // on the part's own position scaled to metres (the pores' way), with an offset of its own per
                // body, so it rides on the skin: read in world space, the cells stay where they are and a
                // swimming body slides through them, a new tiling of its skin each second.
                [branch] if (_MottleSoft > 0.5)
                {
                    float3 skinMetres = input.positionOS * PartSizes() +
                                        _Individual * float3(17.3, 31.9, 7.7) + float3(5.1, 47.3, 23.9);
                    tone = MottleSoft(skinMetres * mottleCells);
                    wall = 1.0;

                    // The blend between cells pulls tones toward their mean, so the marking is lifted by
                    // MottleSoftGain to keep about the contrast of the flat cells (b39 read it fainter).
#if defined(_EVO_SKIN_MAP)
                    mottleStrength *= _SkinMapOn > 0.5 ? _SkinMap.z : MottleSoftGain;
#else
                    mottleStrength *= MottleSoftGain;
#endif
                }
                else
                {
                    // Read in the world on a part, so a body slides through its cells; on a one skin in the rest
                    // frame's metres, so the cells ride on it.
                    float3 mottleAt = input.positionWS;
                    [branch] if (skinMesh) mottleAt = input.positionOS * PartSizes();

                    EvoVoronoi(mottleAt * mottleCells, f1, f2, tone);
                    wall = saturate((f2 - f1) * 2.5);
                }

                float mottle = lerp(1.0 - mottleStrength, 1.0 + mottleStrength, tone);
                mottle *= lerp(1.0 - mottleStrength * 0.7, 1.0, wall);

                body *= mottle;
#if defined(_EVO_SKIN2)
                [branch] if (skinMesh) body = Skin2Tone(body, input.positionOS * PartSizes());
#endif
#if defined(_EVO_SKIN2)
// skin2parts: the asymmetry on a part, the skin's tone noise at the part's wavelength.
                [branch] if (skin2Part) body = Skin2Tone(body, skin2PartRest);

#endif

                // A link's muscle bands, under the tissue look. Worked out before the test, since it
                // takes a screen derivative and those are kept out of branches.
                float bands = TissueBands(input.positionOS);
                if (_TissueLook > 0.5) body *= bands;

                // A link's segment phase and how many pixels a segment spans, worked out here for the derivative's
                // sake and read only behind a test of _LinkShape.w (LinkPhase).
                float linkQ = LinkPhase(input.positionOS);
                float linkPixels = 1.0 / max(fwidth(linkQ), 1e-6);

                // The papillae's colour: the base darker and the tip paler, worked out above with the normal.
                [branch] if (_Papillae > 0.001)
                {
                    body *= 1.0 - PapillaBaseDark * papBase;
                    body = lerp(body, saturate(body * PapillaTipGain), PapillaTipLift * papTip);
                }

                // The pores' darkening (the soft tissue), worked out above with the normal.
                [branch] if (input.tissueNormal.x > 0.5) body *= 1.0 - TissuePoreDark * poreShade;

                // The velvet pores' darkness and lips and the osculum's lip (_Velvet), worked out above with the
                // normal. The hole's darkness is on the whole of the light, at the end (velvetShade).
                [branch] if (_Velvet > 0.5)
                {
                    body *= 1.0 - VelvetPoreDark * velvetPore;
                    body = lerp(body, saturate(body * VelvetLipGain), VelvetLipLift * velvetLip);
                    body = lerp(body, saturate(body * VelvetLipGain), VelvetOsculumLipLift * velvetOscLip);

                    // Level 2's star systems: the surround and the line darker, the zooids paler, the pore darkest.
                    [branch] if (_Velvet > 1.5)
                    {
                        body *= 1.0 - VelvetStarSurroundDark * velvetStar.y;
                        body *= 1.0 - VelvetStarLineDark * velvetStar.z;

                        // The zooids: the body's hue, lifted by VelvetStarPaleGain but to at least
                        // VelvetStarPaleFloor of luminance and a little toward white, so they show pale on a dark
                        // absorber as a Botryllus's do; a lift of the body alone left them near black (b40).
                        float starLuma = dot(body, LumaWeights);
                        float3 starHue = body / max(starLuma, 1e-4);
                        float3 zooid = saturate(lerp(float3(1.0, 1.0, 1.0), starHue, VelvetStarPaleHue) *
                            max(starLuma * VelvetStarPaleGain, VelvetStarPaleFloor));
                        body = lerp(body, zooid, velvetStar.x);
                        body *= 1.0 - VelvetStarPoreDark * velvetStar.w;
                    }
                }

                float veinFace, veinThrough;
                LeafVeins(input.leaf, veinFace, veinThrough);
                body *= veinFace;
                thickness *= veinThrough;
#if defined(_EVO_NOSE_PIT)
                body *= 1.0 - PitAlbedoDark * pitDark;
#endif

#if defined(_EVO_SKIN2)
// skin2parts: the body before RIM_BAND, to restore below where SKIN2's band draws in its place.
                float3 skin2PartBeforeRim = body;

#endif
                // The rim band on a consumer's box part (_RimBand; RimBand). The mesh's flag is one on the box, two
                // on a blade and zero on a capsule or a sphere, which are left as they were. Before the ramp and the
                // countershade, so they grade the pigment as they grade the body.
                [branch] if (_RimBand > 0.5)
                {
                    if (input.shape.x > 0.5 && input.shape.x < 1.5)
#if defined(_EVO_SKIN_MAP)
                        if (_SkinMapOn > 0.5)
                            body = RimBandMapped(body, input.positionOS, input.shape.y, pixelMetres, _SkinMap.x, _SkinMap.y);
                        else
#endif
                        body = RimBand(body, input.positionOS, input.shape.y, pixelMetres);
                }
#if defined(_EVO_SKIN2)
// skin2parts: where the clade has a margin band and the part is flat enough to wear it, SKIN2's band (below) replaces
// RIM_BAND's: the body goes back to what it was before RimBand. A clade with no band, or a part too round, keeps RIM_BAND.
                [branch] if (skin2Part && skin2PartMarks.z > 0.001 && _PatternShift.z >= Skin2MarginAbsent) body = skin2PartBeforeRim;

#endif

#if defined(_EVO_MOUTH2)
                // The body's colour before its chromatophores and pattern, which the plain mouth (EVOSIM_THEATRE_MOUTH2_PLAIN)
                // draws inside the lip below.
                float3 mouthPlainBody = body;
#endif

                // The chromatophores (_Chromatophores; Chromatophores): the strength is the tissue's, set per
                // renderer by the palette. After the rim band, so the discs lie on the pigment, and before the ramp
                // and the countershade, which grade them as they grade the body.
                [branch] if (_Chromatophores > 0.001)
                {
                    body = Chromatophores(body, input.positionOS, input.normalWS, _Chromatophores, pixelMetres, skinMesh, restNormal);
                }

                // The skin pattern (_Pattern; PatternSkin): the strength is the tissue's, set per renderer by the palette.
                // After the chromatophores, so the speckle lies on the pattern, and before the ramp and the countershade,
                // which grade it as they grade the body.
                [branch] if (_Pattern.y > 0.001)
                {
#if defined(_EVO_SKIN2)
                    [branch] if (skinMesh)
                        body = PatternSkin2(body, input.positionOS * PartSizes(), _Pattern, _PatternShift.xy, _PatternShift.zw,
                            pixelMetres, restNormal);
                    else
#if defined(_EVO_SKIN2)
// skin2parts: a part draws the clade's pattern as the skin does (the warp, and the pigment pair picked by the clade), on
// the part's own planes (the normal's components along its axes) and its own metres.
                    [branch] if (skin2Part)
                        body = PatternSkin2(body, input.positionOS * PartSizes(), _Pattern, _PatternShift.xy, _PatternShift.zw,
                            pixelMetres, skin2PartNormal);
                    else
#endif
#endif
                    body = PatternSkin(body, input.positionOS, input.normalWS, _Pattern, _PatternShift.xy, pixelMetres, skinMesh, restNormal);
                }
#if defined(_EVO_SKIN2)

                // SKIN2's margin band along the thin edge of the one skin, in the clade's colour, and the joints' creases
                // darker (Skin2Margin). Before the ramp and the countershade, which grade it as they grade the body. A greyed
                // body, a corpse and a neck have _PatternShift.z of zero and take neither.
                [branch] if (skinMesh && _PatternShift.z > 0.0)
                {
#if defined(_EVO_SKIN2B)
                    float3 skin2BBody = body;
#endif
#if defined(_EVO_JOIN_DEBUG)
                    JoinDebugCrease = input.skin2.z;
#endif
                    body = Skin2Margin(body, input.skin2, _PatternShift.zw, input.positionOS * PartSizes());
#if defined(_EVO_SKIN2B)
#if defined(_EVO_SKIN2_BAND4)
                    Skin2B4End = saturate(input.tissueNormal.x / Skin2B4EndPack);
#endif
#if defined(_EVO_JOIN_DEBUG)
                    JoinDebugBandOn = true;
#endif
                    body = Skin2MarginB(skin2BBody, input.skin2, _PatternShift.zw, input.positionOS * PartSizes(),
                        Skin2BView(normalize(input.normalWS), v));
#if defined(_EVO_JOIN_DEBUG)
                    JoinDebugBandOn = false;
#endif
#if defined(_EVO_SKIN2_BAND4)
                    Skin2B4End = -1.0;
#endif
#endif
#if defined(_EVO_SKIN_JOIN)
                    // SKIN_JOIN: the crease's darkening at JoinCreaseKeep (marks z is read nowhere after this line).
                    input.skin2.z *= JoinCreaseKeep;
#endif
                    body *= 1.0 - Skin2CreaseDark * input.skin2.z;
                }

#endif
#if defined(_EVO_SKIN2)
// skin2parts: SKIN2's margin band on a part, by the skin's own function with the part's marks, in the clade's colour
// (_PatternShift.zw); none for a clade without one, a greyed body, a corpse and a neck, whose _PatternShift.z is zero or
// under Skin2MarginAbsent.
#if defined(_EVO_SKIN2B)
                float3 skin2BPartBody = body;
#endif
                [branch] if (skin2Part && _PatternShift.z > 0.0)
                    body = Skin2Margin(body, skin2PartMarks, _PatternShift.zw, skin2PartRest);
#if defined(_EVO_SKIN2B)
                [branch] if (skin2Part && _PatternShift.z > 0.0)
                    body = Skin2MarginB(skin2BPartBody, skin2PartMarks, _PatternShift.zw, skin2PartRest,
                        Skin2BView(normalize(input.normalWS), v));
#endif

#endif
#if defined(_EVO_MOUTH2)

                // The mouth's opening, in the colour: the lip in the clade's margin colour (or the body's, lifted) with a
                // highlight on its outer half, and the throat a deep warm red-brown, no lighter than the body. After the
                // pattern and the margin, so the lip lies on the markings and not under them, and before the ramp and the
                // countershade, which grade it as they grade the body.

                // The plain mouth (EVOSIM_THEATRE_MOUTH2_PLAIN): out to the lip's outer edge the body's colour before its
                // chromatophores and pattern, so no mark of the skin runs across the lip, the wall or the throat, where the
                // throat's darker-of-two would keep a mark's dark stroke as a glyph.
                [branch] if (_MouthPlain > 0.0 && mouthShare > 0.001)
                {
                    float mouthPlainEnd = MouthLipFrom + MouthLipWidth;
                    float mouthPlain = (1.0 - smoothstep(mouthPlainEnd - MouthLipEdge, mouthPlainEnd + MouthLipEdge, mouthU)) * mouthShare;
                    body = lerp(body, mouthPlainBody, saturate(_MouthPlain) * mouthPlain);
                }

                // The relief's lining on the inner wall (EVOSIM_THEATRE_MOUTH2_RELIEF), under the lip, the teeth and the throat.
                [branch] if (mouthLining > 0.002)
                {
                    float3 mouthLiningColour = lerp(body * MouthLiningBody, MouthLiningFlesh, MouthLiningMix);
                    body = lerp(body, mouthLiningColour, saturate(mouthLining));
                }

                [branch] if (mouthLip > 0.002)
                {
                    float3 mouthLipColour = MouthLipColour(body, input.positionOS * PartSizes());

                    // The oral disc (EVOSIM_THEATRE_MOUTH2_DISC): the lip ring takes the mark colour where the body has one.
                    [branch] if (_MouthDisc.a > 0.0 && _FamilyColor.a > 0.0) mouthLipColour = _FamilyColor.rgb;

                    // The plain mouth: the lip in the body's own hue (its plain colour lifted), at MouthPlainLipStep of its step
                    // from the body; the relief's tilt still shades it.
                    [branch] if (_MouthPlain > 0.0)
                    {
                        float mouthPlainStrength = saturate(_MouthPlain);
                        mouthLipColour = lerp(mouthLipColour, MouthLifted(mouthPlainBody), mouthPlainStrength);
                        mouthLipColour = lerp(mouthLipColour, body, (1.0 - MouthPlainLipStep) * mouthPlainStrength);
                    }

                    mouthLipColour *= 1.0 + MouthLipHighlight * mouthOuter;
                    [branch] if (mouthBite > 0.0) mouthLipColour = lerp(mouthLipColour, float3(1.0, 1.0, 1.0), saturate(_MouthBiteParams.y * mouthBite));
                    body = lerp(body, saturate(mouthLipColour), mouthLip * MouthLipOpacity);
                }

                // The oral disc's stripes, under the lip where their reaches overlap.
                [branch] if (mouthDiscMix > 0.002)
                {
                    // The kinds' tone scales the disc colour and their white lifts it; both are one and zero for the stripes (kind 0).
                    float3 mouthDiscColour = saturate(_MouthDisc.rgb * mouthDiscTone);

                    // The plain mouth: the disc in the body's own hue, the lip's colour, where a plain strength under one leaves any
                    // of it (phase143's portrait of 53866 had dark wedges round the mouth).
                    [branch] if (_MouthPlain > 0.0) mouthDiscColour = lerp(mouthDiscColour, MouthLifted(mouthPlainBody), saturate(_MouthPlain));
                    [branch] if (mouthDiscWhite > 0.0) mouthDiscColour = lerp(mouthDiscColour, float3(1.0, 1.0, 1.0), mouthDiscWhite);
                    [branch] if (mouthBite > 0.0) mouthDiscColour = lerp(mouthDiscColour, float3(1.0, 1.0, 1.0), saturate(_MouthBiteParams.y * mouthBite));

                    // The relief's teeth are enamel, graded by the tone from the gum to the tip.
                    [branch] if (mouthEnamel > 0.0) mouthDiscColour = lerp(mouthDiscColour, MouthEnamel * mouthDiscTone, mouthEnamel);

                    body = lerp(body, mouthDiscColour, saturate(mouthDiscMix * (1.0 - mouthLip * MouthLipOpacity)));
                }

                body = lerp(body, min(MouthThroat, body), mouthThroat);
#endif

#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)
                // The nose's lamellae (EVOSIM_THEATRE_NOSE_PIT=2): fine rings of the distance from the pit's centre in the rest
                // frame, so they ride on the tissue, at NoseLamellaShare of the body pattern's cell, darkening the colour at a
                // ridge, by the vertex's share in the nose's part, and none inside the lip. After the pattern and the margin,
                // before the ramp and the countershade, which grade it as they grade the body. The derivative is taken in the
                // open.
                float noseRingLength = length(input.positionOS * PartSizes() - _PitSkin.xyz);
                float noseRingCell = _Pattern.x > 1e-4 ? _Pattern.x / PatternCellsPerTile : NoseLamellaBareCell * _PitSkin.w;
                float noseRingX = noseRingLength / max(NoseLamellaShare * noseRingCell, 1e-5);
                float noseRingFine = saturate(1.0 - NoseLamellaFadeRate * fwidth(noseRingX));

                [branch] if (noseShare > 0.001)
                {
                    float noseRingOut = smoothstep(NoseLamellaFrom, NoseLamellaFrom + NoseLamellaSoft, noseRingLength / _PitSkin.w);
                    float noseRidge = 0.5 + 0.5 * cos(6.2831853 * noseRingX);

                    body *= 1.0 - NoseLamellaDark * noseRidge * noseRingFine * noseRingOut * noseShare;
                }

#endif
                // The pattern family (_FamilyParams.y over zero; FamilyMask): bars, an eyespot or an edge outline in the family colour, on the
                // pigment and the margin, before the ramp and the countershade, which grade it as they grade the body.
                // The derivatives of the position and of the normal (the edge outline's bend) are taken in the open, outside the branch.
                float3 familyDPos = fwidth(input.positionOS);
                float familyBend = length(fwidth(normalize(input.normalWS)));

                [branch] if (_FamilyParams.y > 0.0)
                {
                    float familyCore;
                    float familyMask = FamilyMask(input.positionOS, familyDPos, input.normalWS, v, familyBend, familyCore);

                    body = lerp(body, _FamilyColor.rgb, saturate(familyMask * _FamilyColor.a));

                    // The eyespot's core filled in the mark's colour rather than black (EVOSIM_THEATRE_FAMILY_SPOT_SOFT), so the spot is
                    // a mark and not an eye's iris and pupil.
                    float familySpotSoft = saturate(_EvoFamilySpotSoft);
                    [branch] if (familySpotSoft > 0.0)
                        body = lerp(body, _FamilyColor.rgb, saturate(familyCore * _FamilyColor.a * familySpotSoft));
                    body = lerp(body, float3(0.02, 0.02, 0.02), saturate(familyCore * _FamilyColor.a * (1.0 - familySpotSoft)));
                }

                // The colour ramp, then the countershade: the body's colour only (see ColourRamp, Countershade).
                [branch] if (_RampTip.w > 0.5) body = ColourRamp(body, input.positionOS, input.leaf);
#if defined(_EVO_SKIN_MAP)
                [branch] if (_Countershade > 0.5)
                {
                    if (_SkinMapOn > 0.5) body = CountershadeMapped(body, input.normalWS, input.leaf.w > 0.5, _SkinMap.w);
                    else body = Countershade(body, input.normalWS, input.leaf.w > 0.5);
                }
#else
                [branch] if (_Countershade > 0.5) body = Countershade(body, input.normalWS, input.leaf.w > 0.5);
#endif

                // A blade's tone: denser and darker toward the stalk and along the middle, where
                // the tissue is thickest, and lighter and a little warmer at the ruffled edge,
                // where it is one or two cells thick, as a backlit kelp's edge goes gold. Applied
                // to the whole of the blade's light at the end, not to the body colour alone: on a
                // blade the rim, the glow and the light through it outweigh the diffuse term, so
                // a tone on the body colour alone moved no pixel by more than 3 in 255.
                float3 leafTone = float3(1.0, 1.0, 1.0);
                float rimScale = 1.0;
                if (input.leaf.w > 0.5)
                {
                    float across = saturate(abs(input.leaf.z));
                    float edge = smoothstep(0.35, 1.0, across);
                    float stalk = 1.0 - smoothstep(0.0, 0.4, input.leaf.x);
                    float tone = lerp(0.5, 1.12, edge) * (1.0 - 0.35 * stalk);
                    leafTone = tone * lerp(float3(1.0, 1.0, 1.0), float3(1.12, 1.06, 0.78), 0.5 * edge);
                    leafTone *= lerp(1.0, mottle, 0.7);

                    // A seaweed under the marine producers' dial (_Alga) has no dark midrib band: the tone across
                    // the blade is flat, AlgaToneMiddle in the middle to AlgaToneEdge at the rim, and only the run from
                    // the stalk (dark) to the tip (pale) stays. The value above is replaced, not changed, so with the
                    // dial off it is the one it always was.
                    [branch] if (algaBlade)
                    {
                        float algaTone = lerp(AlgaToneMiddle, AlgaToneEdge, edge) * (1.0 - 0.35 * stalk);
                        leafTone = algaTone * lerp(float3(1.0, 1.0, 1.0), float3(1.12, 1.06, 0.78), 0.5 * edge);
                        leafTone *= lerp(1.0, mottle, 0.7);

                        // A fan's growth bands (AlgaBandTone), faint pale and dark rings round the stalk, by how
                        // round the blade is: none on a sheet or a strap, and none on a frond.
                        [branch] if (!AlgaIsFrond())
                            leafTone *= 1.0 + (AlgaBandTone(input.leaf.x, input.leaf.y, input.leaf.w) - 1.0) *
                                AlgaRoundOfLeaf(input.leaf.w) * saturate(_Alga);

                        // A frond's blades differ a little in lightness, each by its own (AlgaFrondBladeTone).
                        [branch] if (AlgaIsFrond())
                            leafTone *= AlgaFrondBladeTone(input.leaf);
#if defined(_EVO_ALGA_BLADE)
                        // The plant's lean along the golden-olive family, brown to green (AlgaBladeLean).
                        [branch] if (AlgaIsFrond())
                            leafTone *= AlgaBladeLean();
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                        // Level 2: the plant's place on the ramp from sea lettuce to kelp (AlgaBladeColour) in place of the lean above, on a
                        // living blade (_AlgaDeep.w: a greyed or dead one keeps the lean and its grey).
                        [branch] if (AlgaIsFrond() && _AlgaDeep.w > 0.5)
                        {
                            leafTone /= AlgaBladeLean();
#if defined(_EVO_LOOK_PROBE)
                            // LOOK_PROBE 1 (a diagnostic, never a film): the ramp's multiply is skipped, so its ratio is one. The
                            // statement below is the if's one body; with the keyword off the line is the one it always was.
                            [branch] if (_EvoLookProbe < 0.5 || _EvoLookProbe > 1.5)
#endif
                            leafTone *= AlgaBladeColour(body, input.leaf);
                        }
#endif
                    }


                    // The guild's rim is at the edge of a solid; on a blade's face it is paint.
                    rimScale = 0.45;
                }
#if defined(_EVO_ALGA_ROUND_TARGET)
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2) && defined(_EVO_ALGA_RAMP_PHOTO)
                // ROUND_TARGET: a living producer that is not a blade, not a swimmer's merged skin and not a thallus (a round part)
                // takes the multiplier a blade gets at the ramp's mid-green stop (AlgaRoundTargetTone), v of the way, on its whole
                // light. The body is the one AlgaBladeColour is handed above. The look probe's tone blocks below still override it.
                bool roundTarget = _AlgaDeep.w > 0.5 && input.leaf.w < 0.5 && !skinMesh;
#if defined(_EVO_THALLI)
                roundTarget = roundTarget && !thallusMesh;
#endif
                [branch] if (roundTarget) leafTone *= lerp(float3(1.0, 1.0, 1.0), AlgaRoundTargetTone(body), _EvoAlgaRoundTarget);
#endif
#endif
#if defined(_EVO_THALLI)

                // A thallus's sheets (Ulva, Padina) take a blade's rim scale; Codium is a solid and keeps the rim.
                [branch] if (thallusMesh && !ThalliIsCodium(input.shape.x)) rimScale = ThalliSheetRimScale;
#endif

                // Key light. Scaled by the light left at this body's depth on the run's day when
                // the underwater look's item 3 is on (EvoDepthLight, red first); one otherwise.
                Light main = GetMainLight();
                float3 depthLight = EvoDepthLight(input.positionWS);
                float3 keyColour = main.color * depthLight;

                // The board's light: no light lost to the body's depth, and a white key of the key's own
                // luminance. The water's cuts to the rim, the glow and the light through go with them.
                float4 bodyWater = _EvoBodyWater;

                [branch] if (_BoardLight > 0.5)
                {
                    depthLight = float3(1.0, 1.0, 1.0);
                    keyColour = BoardWhite(main.color);
                    bodyWater = float4(0.0, 0.0, 0.0, 0.0);
                }

                // Light through tissue (_Sss): off a leaf the thickness is the part's own along the view
                // and the key rather than its smallest half extent, and the tint deepens with it. A leaf
                // keeps the lens's thickness, which already knows where the blade is thin.
                float3 transTint = _TransTint.rgb;

                [branch] if (_Sss > 0.5)
                {
                    // A one skin keeps its vertex's own half thickness, at SssThinness's reach: the vertex stage's
                    // thickness is saturate(_TransMetres / halfThick), so this is SssThinness with that half thickness
                    // for the path (exactly, wherever the vertex's was under one, and one where both saturate).
                    if (input.leaf.w < 0.5)
                    {
                        [branch] if (skinMesh) thickness = saturate(SssReach * thickness);
                        else thickness = SssThinness(v, main.direction);
                    }
                    transTint = SssTint(_TransTint.rgb, thickness);
                }
#if defined(_EVO_THALLI)

                // A thallus keeps the mesh's own thinness (the vertex stage's), which the light through tissue above replaces with
                // the part's; the tint follows it as it would have.
                [branch] if (thallusMesh)
                {
                    thickness = input.fogAndThickness.y;
                    transTint = _Sss > 0.5 ? SssTint(_TransTint.rgb, thickness) : _TransTint.rgb;
                }
#endif

                // A producer's light through, tinted by its depth as its body is (algaTint, above).
                [branch] if (_AlgaDeep.w > 0.5) transTint *= algaTint;
#if defined(_EVO_UPTAKE)
#undef _RimColor
#undef _TransTint
#endif

#if defined(_EVO_LOOK_STAGE)
                // LOOK_STAGE 1 (a diagnostic, never a film): a producer's pixel, a blade or a thallus, returns here with the colour as it
                // enters the lighting, opaque and in linear, after every tone and tint on it (the colour ramp and the countershade are the
                // last to change it). Every other pixel falls through to the light as it always did.
                bool lookProducer = input.leaf.w > 0.5;
#if defined(_EVO_THALLI)
                lookProducer = lookProducer || thallusMesh;
#endif
                [branch] if (lookProducer && _EvoLookStage > 0.5 && _EvoLookStage < 1.5) return half4(body, 1.0);
#endif
                float3 lit = body * keyColour * (Wrapped(n, main.direction) * _KeyGain);
#if defined(_EVO_LOOK_STAGE)
                // LOOK_STAGE 2: the diffuse light alone, the key's wrapped term on the body.
                [branch] if (lookProducer && _EvoLookStage > 1.5 && _EvoLookStage < 2.5) return half4(lit, 1.0);
#endif
                float through = _TransGain * (1.0 - saturate(bodyWater.w));
#if defined(_EVO_NOSE_PIT) && defined(_EVO_NOSE_SKIN)
                through += NoseTransGain * noseShare * (1.0 - saturate(bodyWater.w));
#endif

                // A seaweed's light through (_Alga; AlgaThrough*): stronger on a sheet than on a strap, every
                // transmitted term at once, each still by the lens's thinness at the pixel.
                [branch] if (algaBlade)
                {
                    float algaThrough = lerp(AlgaThroughSheet, AlgaThroughStrap, algaForm.w);

                    // A frond's is AlgaThroughFrond, whatever its proportions.
                    [branch] if (AlgaIsFrond()) algaThrough = AlgaThroughFrond;

                    through *= lerp(1.0, algaThrough, saturate(_Alga));
                }
                lit += transTint * (through * Transmission(n, v, main.direction, keyColour, thickness));
#if defined(_EVO_SKIN2)

                // SKIN2's light through the tissue on the one skin: the diffuse wraps further (the light the wider wrap
                // adds is tinted toward the transmission hue), and thin tissue takes more of the backlight, at full
                // thickness since the metre-based one is one nearly everywhere on a small body.
                [branch] if (skinMesh)
                {
                    float skin2Narrow = Wrapped(n, main.direction);
                    float skin2WrapWidth = _Wrap + Skin2WrapExtra;
                    float skin2Wide = saturate((dot(n, main.direction) + skin2WrapWidth) / (1.0 + skin2WrapWidth));
                    float3 skin2Hue = transTint *
                        (max(dot(body, LumaWeights), Skin2WrapFloor) / max(dot(transTint, LumaWeights), 1e-4));
                    float3 skin2Scatter = lerp(body, skin2Hue, Skin2WrapScatter);

                    lit += skin2Scatter * keyColour * (_KeyGain * max(skin2Wide - skin2Narrow, 0.0));

                    float skin2Thin = smoothstep(Skin2ThinLow, Skin2ThinHigh, input.skin2.x);
#if defined(_EVO_JOIN_DEBUG)
                    float3 joinDebugThinBefore = lit;
#endif
                    lit += transTint * (through * Skin2ThinGain * skin2Thin) *
                        Transmission(n, v, main.direction, keyColour, 1.0);
#if defined(_EVO_JOIN_DEBUG)
                    JoinDebugThin = dot(lit - joinDebugThinBefore, LumaWeights);
#endif
                }

#endif
#if defined(_EVO_SKIN2)
// skin2parts: the skin's wider wrap and thin backlight on a part (the same lines, with the part's thin mark).
                [branch] if (skin2Part)
                {
                    float skin2PartNarrow = Wrapped(n, main.direction);
                    float skin2PartWrapWidth = _Wrap + Skin2WrapExtra;
                    float skin2PartWide = saturate((dot(n, main.direction) + skin2PartWrapWidth) / (1.0 + skin2PartWrapWidth));
                    float3 skin2PartHue = transTint *
                        (max(dot(body, LumaWeights), Skin2WrapFloor) / max(dot(transTint, LumaWeights), 1e-4));
                    float3 skin2PartScatter = lerp(body, skin2PartHue, Skin2WrapScatter);

                    lit += skin2PartScatter * keyColour * (_KeyGain * max(skin2PartWide - skin2PartNarrow, 0.0));

                    float skin2PartThin = smoothstep(Skin2ThinLow, Skin2ThinHigh, skin2PartMarks.x);
                    lit += transTint * (through * Skin2ThinGain * skin2PartThin) *
                        Transmission(n, v, main.direction, keyColour, 1.0);
                }

#endif

                // The light from the surface through a blade, seen from below or beside it: the
                // world's light comes from above, whatever the key does for the camera.
                if (input.leaf.w > 0.5)
                {
                    lit += transTint * (through * _LeafSkyGlow *
                        Transmission(n, v, float3(0.0, 1.0, 0.0), keyColour, thickness));
                }
#if defined(_EVO_THALLI)

                // Ulva's light from the surface through its thin sheets, as a blade's, at ThalliUlvaSkyGlow of it, by the sheet's
                // own thinness (full at the margin).
                [branch] if (ThalliIsUlva(input.shape.x))
                {
                    lit += transTint * (through * _LeafSkyGlow * ThalliUlvaSkyGlow *
                        Transmission(n, v, float3(0.0, 1.0, 0.0), keyColour, thickness));
                }
#endif

                // A living leaf's light through the sheet when the key is behind it (_LeafLook; LeafBack*).
                [branch] if (_LeafLook > 0.5)
                {
                    if (input.leaf.w > 0.5)
                    {
                        float behind = smoothstep(LeafBackLow, LeafBackHigh,
                            -dot(normalize(input.normalWS), main.direction));

                        lit += transTint * LeafBacklitTint * keyColour *
                            (through * LeafBacklitGain * behind * pow(saturate(thickness), LeafBacklitFalloff));
                    }
                }

                // The sheen: a tight highlight off the key, white rather than the body's colour,
                // so it reads as wet skin catching the light and not as paint.
                //
                // Under _Sss the glint is read off a normal turned most of the way back to the uncarved
                // surface and is dimmer, so a dark body carries one soft highlight and not a scatter of
                // white flecks, one on each flat of the carve.
                float3 halfway = normalize(main.direction + v);
                float3 litBeforeSheen = lit;

                // The sheen's light: the key, or under _Iridescence (IridescentTint) the key in the film's interference
                // colour, on every branch of the chain below. The same value as the key with the dial off.
                float3 sheenKey = keyColour;
                [branch] if (_Iridescence > 0.001) sheenKey = keyColour * IridescentTint(input.positionOS, input.normalWS, v, _Iridescence);

                // Under the soft tissue the highlight is broader and weaker (TissueSheenPower, TissueSheenGain),
                // on the smoothed normal and the glint's own gain when _Sss is on as well. The chain below it
                // is what every other part reaches; its first test lost its [branch] hint to become an else if.
                [branch] if (input.tissueNormal.x > 0.5)
                {
                    float3 tissueSheenNormal = n;
                    float tissueSheenGain = TissueSheenGain;

                    [branch] if (_Sss > 0.5)
                    {
                        tissueSheenNormal = normalize(lerp(n, normalize(input.normalWS), SssSheenSmooth));
                        tissueSheenGain *= SssSheenGain;
                    }

                    float tissueFacing = saturate(dot(tissueSheenNormal, halfway));

                    // Under _Iridescence the film's colour only in the highlight's core: the share of this broad lobe
                    // that the tight one (power 28) would have, so the rest of the broad sheen stays the key's white.
                    // Spread over the whole broad lobe, the thickness noise's cycles crossed the body as even rainbow
                    // bands, an oil slick (b39). The same value as before with the dial off, since sheenKey is then
                    // the key itself.
                    float3 tissueSheenKey = sheenKey;
                    [branch] if (_Iridescence > 0.001)
                        tissueSheenKey = lerp(keyColour, sheenKey, pow(tissueFacing, IridescentCorePower));

                    lit += tissueSheenKey * (_Sheen * tissueSheenGain * pow(tissueFacing, TissueSheenPower));
                }
                else if (_Sss > 0.5)
                {
                    float3 sheenNormal = normalize(lerp(n, normalize(input.normalWS), SssSheenSmooth));
#if defined(_ONE_SKIN)
                    [branch] if (skinMesh && _SkinCarve > 0.5)
                    {
                        float skinSheenWet = saturate(skinCarveReach / 0.6);
                        lit += sheenKey * (_Sheen * SssSheenGain * lerp(1.0, 0.5, skinSheenWet) *
                            pow(saturate(dot(sheenNormal, halfway)), lerp(SkinSheenPower, 40.0, skinSheenWet)));
                    }
                    else if (skinMesh)
                        lit += sheenKey * (_Sheen * SssSheenGain * pow(saturate(dot(sheenNormal, halfway)), SkinSheenPower));
                    else
                    lit += sheenKey * (_Sheen * SssSheenGain * pow(saturate(dot(sheenNormal, halfway)), 28.0));
#else
                    lit += sheenKey * (_Sheen * SssSheenGain * pow(saturate(dot(sheenNormal, halfway)), 28.0));
#endif
                }
                else
                {
#if defined(_ONE_SKIN)
                    [branch] if (skinMesh && _SkinCarve > 0.5)
                    {
                        float skinSheenWet = saturate(skinCarveReach / 0.6);
                        lit += sheenKey * (_Sheen * lerp(1.0, 0.5, skinSheenWet) *
                            pow(saturate(dot(n, halfway)), lerp(SkinSheenPower, 40.0, skinSheenWet)));
                    }
                    else if (skinMesh)
                        lit += sheenKey * (_Sheen * pow(saturate(dot(n, halfway)), SkinSheenPower));
                    else
                    lit += sheenKey * (_Sheen * pow(saturate(dot(n, halfway)), 28.0));
#else
                    lit += sheenKey * (_Sheen * pow(saturate(dot(n, halfway)), 28.0));
#endif
                }

                // A seaweed's wet sheen (_Alga; AlgaWet*): broad and soft, over the tissue's own, on a blade alone.
                [branch] if (algaBlade)
                {
                    // A frond's sheen is much weaker and tighter (AlgaFrondWetGain at AlgaFrondWetPower): the wet gloss the
                    // sheets took as a pale haze is not a golden-brown weed's.
                    float algaWetGain = AlgaWetGain;
                    float algaWetPower = AlgaWetPower;
                    [branch] if (AlgaIsFrond())
                    {
                        algaWetGain = AlgaFrondWetGain;
                        algaWetPower = AlgaFrondWetPower;
                    }

                    lit += sheenKey * (saturate(_Alga) * algaWetGain * pow(saturate(dot(n, halfway)), algaWetPower));

                    // A thin sheet seen through (AlgaSeeGain): the board's backlit Ulva and kelp glow gold-green through
                    // the sheet. The same light through as above, from the key, in the blade's own colour (its body's
                    // hue at full value, raised to the one and a half, warmed half way to the leaf dial's backlit
                    // tint) and not the pale tint, by the sheet's share (not a round fan's, not a strap's), and cut
                    // at the rim's own brightest (the rim colour, the rim's strength and the blade's share of it,
                    // under the same water cuts), so it is never brighter than the rim can be.
                    float algaThin = algaForm.x * (1.0 - AlgaRoundOfLeaf(input.leaf.w)) * saturate(_Alga);

                    // A frond is thin throughout, whatever its proportions: the gold light through it, for backlight.
                    [branch] if (AlgaIsFrond()) algaThin = saturate(_Alga);
                    float3 seeHue = body / max(max(body.r, max(body.g, body.b)), 1e-4);
                    float3 seeTint = seeHue * lerp(float3(1.0, 1.0, 1.0), seeHue, 0.5) *
                        lerp(float3(1.0, 1.0, 1.0), LeafBacklitTint, 0.5);
                    float3 seeWater = lerp(float3(1.0, 1.0, 1.0), depthLight, saturate(bodyWater.x)) *
                        (1.0 - saturate(bodyWater.y));
                    float3 seeCap = rimColour * seeWater * (_RimStrength * rimScale);
                    float3 seeLight = seeTint * (through * AlgaSeeGain * algaThin) *
                        Transmission(n, v, main.direction, keyColour, thickness);
                    lit += min(seeLight, seeCap);
#if defined(_EVO_ALGA_BLADE)
                    // The broad blade is a thin membrane: more light through toward its margin, where it is thinnest and the
                    // light comes from behind (Transmission's own view and key terms), up to AlgaBladeCapGain of the rim's
                    // brightest. Added to the see-through above, so it is the difference the cap leaves.
                    [branch] if (AlgaIsFrond())
                    {
                        float bladeRim = smoothstep(AlgaBladeGlowStart, 1.0, abs(input.leaf.z));
                        float3 bladeLight = seeTint * (through * AlgaBladeGlow * algaThin * bladeRim) *
                            Transmission(n, v, main.direction, keyColour, thickness);

                        lit += min(seeLight + bladeLight, seeCap * AlgaBladeCapGain) - min(seeLight, seeCap * AlgaBladeCapGain);
                    }
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE2)
                    // Level 2: light through the blade when the key is behind it as the camera sees it. The blade's hue at full value, more
                    // saturated, AlgaBladeBackGain of the key's direct term (keyColour times _KeyGain) at full backlight, by the
                    // alignment of the view and the key as Transmission bends the key by the normal, to AlgaBladeBackPower, by
                    // the blade's thickness and thinness as the other terms take them, under the water's cut of the light through.
                    [branch] if (AlgaIsFrond())
                    {
                        float3 backBent = normalize(main.direction + n * _TransDistortion);
                        float backAlign = pow(saturate(dot(v, -backBent)), AlgaBladeBackPower);
                        float backLuma = dot(seeHue, LumaWeights);
                        float3 backTint = saturate(lerp(float3(backLuma, backLuma, backLuma), seeHue, AlgaBladeBackSaturation));
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                        backTint *= AlgaBlade3BackKeep;
#endif

                        lit += backTint * keyColour *
                            (_KeyGain * AlgaBladeBackGain * backAlign * thickness * algaThin * (1.0 - saturate(bodyWater.w)));
                    }
#endif
#if defined(_EVO_ALGA_BLADE) && defined(_EVO_ALGA_BLADE3)
                    // Level 3: light through the thin blade when the eye looks toward the key through it. The blade's hue shifted toward
                    // yellow-green and brought to full value, AlgaBlade3Gain of the key's direct term at full backlight, falling off as
                    // the alignment of the view and the key as Transmission bends it, to AlgaBlade3Power, by the thickness the other
                    // terms take and by how thin the tissue is here (the lens's: full at the margin, AlgaBlade3ThinMiddle of it on the
                    // midline), under the water's cut of the light through.
                    [branch] if (AlgaIsFrond())
                    {
                        float3 thruBent = normalize(main.direction + n * _TransDistortion);
                        float thruAlign = pow(saturate(dot(v, -thruBent)), AlgaBlade3Power);
                        float thruThin = thickness *
                            lerp(AlgaBlade3ThinMiddle, 1.0, 1.0 - sqrt(saturate(1.0 - input.leaf.z * input.leaf.z)));
                        float3 thruHue = seeHue * AlgaBlade3Shift;
                        thruHue /= max(max(thruHue.r, max(thruHue.g, thruHue.b)), 1e-4);
                        thruHue = lerp(dot(thruHue, LumaWeights).xxx, thruHue, AlgaBlade3ThruSaturation);

                        lit += thruHue * keyColour *
                            (_KeyGain * AlgaBlade3Gain * thruAlign * thruThin * algaThin * (1.0 - saturate(bodyWater.w)));
                    }
#endif
                }

                // Velvet (_Velvet): the sheen above is replaced by a weak, broad one (VelvetSheenGain, Power).
                [branch] if (_Velvet > 0.5)
                {
                    lit = litBeforeSheen + sheenKey *
                        (_Sheen * VelvetSheenGain * pow(saturate(dot(n, halfway)), VelvetSheenPower));
                }
#if defined(_EVO_THALLI)

                // Codium's felt: the sheen above replaced by a weak, broad one (ThalliMatteSheenGain, Power), as the velvet's is.
                [branch] if (ThalliIsCodium(input.shape.x))
                {
                    lit = litBeforeSheen + sheenKey *
                        (_Sheen * ThalliMatteSheenGain * pow(saturate(dot(n, halfway)), ThalliMatteSheenPower));
                }
#endif
#if defined(_EVO_SHEEN_CAP2)

                // SHEEN_CAP 2: the white the base sheen chain above added, rolled off toward SheenCapBaseShare of the body's own
                // light here. A creature's body only: the one skin, or a part that is not a blade (a leaf, which a marine
                // producer is drawn as), not a velvet absorber, not a seaweed (_Alga) and not a thallus. A producer's round or
                // capsule part with the seaweed dial off cannot be told from an animal's here, and is included.
                bool sheenCapBody = skinMesh || (input.leaf.w < 0.5 && _Velvet < 0.5 && _Alga < 0.001 &&
                    (input.shape.x < SheenCapBaseThallusLow || input.shape.x > SheenCapBaseThallusHigh));

                [branch] if (sheenCapBody)
                    lit = litBeforeSheen + SheenCappedBase(lit - litBeforeSheen, litBeforeSheen);
#endif
#if defined(_EVO_SKIN2)

                // SKIN2's diffuse sheen on the one skin, in place of the tight lobe above (as the velvet's replaces it, from
                // litBeforeSheen): a broad lobe at half the strength, on the same normal (turned back toward the uncarved
                // surface under _Sss, with its gain), and a soft Fresnel sheen at grazing angles, a share of the key by the
                // key's wrap and the tissue's gloss. The iridescent tint (sheenKey) goes on the lobe as before.
                [branch] if (skinMesh)
                {
                    float3 skin2SheenNormal = n;
                    float skin2SheenGain = Skin2SheenGain;

                    [branch] if (_Sss > 0.5)
                    {
                        skin2SheenNormal = normalize(lerp(n, normalize(input.normalWS), SssSheenSmooth));
                        skin2SheenGain *= SssSheenGain;
                    }

                    float3 skin2Lobe = sheenKey *
                        (_Sheen * skin2SheenGain * pow(saturate(dot(skin2SheenNormal, halfway)), Skin2SheenPower));
                    float3 skin2Fresnel = keyColour *
                        (Skin2FresnelGain * saturate(_Sheen * Skin2FresnelSheenScale) *
                         pow(1.0 - saturate(dot(n, v)), Skin2FresnelPower) * Wrapped(n, main.direction));

                    lit = litBeforeSheen + skin2Lobe + skin2Fresnel;
#if defined(_EVO_SHEEN_CAP)
                    // SHEEN_CAP: the lobe and the Fresnel rolled off toward SheenCapShare of the body's own light here.
                    lit = litBeforeSheen + SheenCapped(skin2Lobe + skin2Fresnel, litBeforeSheen);
#endif
                }

#endif
#if defined(_EVO_SKIN2)
// skin2parts: the skin's diffuse sheen on a part, in place of the lobe the chain above chose (the _Sss branch's or the plain
// one's, both power 28); the soft tissue's own lobe (power 9) is broader already and stays, and takes the Fresnel alone.
                [branch] if (skin2Part)
                {
                    float3 skin2PartSheenNormal = n;
                    float skin2PartSheenGain = Skin2SheenGain;

                    [branch] if (_Sss > 0.5)
                    {
                        skin2PartSheenNormal = normalize(lerp(n, normalize(input.normalWS), SssSheenSmooth));
                        skin2PartSheenGain *= SssSheenGain;
                    }

                    float3 skin2PartLobe = sheenKey *
                        (_Sheen * skin2PartSheenGain * pow(saturate(dot(skin2PartSheenNormal, halfway)), Skin2SheenPower));
                    float3 skin2PartFresnel = keyColour *
                        (Skin2FresnelGain * saturate(_Sheen * Skin2FresnelSheenScale) *
                         pow(1.0 - saturate(dot(n, v)), Skin2FresnelPower) * Wrapped(n, main.direction));

                    [branch] if (input.tissueNormal.x > 0.5) lit += skin2PartFresnel;
                    else lit = litBeforeSheen + skin2PartLobe + skin2PartFresnel;
#if defined(_EVO_SHEEN_CAP)
                    // SHEEN_CAP: the white either branch added (the soft tissue's lobe and the Fresnel, or the skin's lobe and
                    // the Fresnel), rolled off toward SheenCapShare of the body's own light here.
                    lit = litBeforeSheen + SheenCapped(lit - litBeforeSheen, litBeforeSheen);
#endif
                }

#endif

                // The fill, and any other light in the scene. TheatreSkin puts one weak
                // directional light opposite the key so the far side of a body is dark rather
                // than lost; in URP a second directional light is an additional light.
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    float3 colour = extra.color * extra.distanceAttenuation;

                    // The board's light: the fill in white, at its own strength.
                    [branch] if (_BoardLight > 0.5) colour = BoardWhite(colour);

                    lit += body * colour * Wrapped(n, extra.direction);
                    lit += transTint * (through * Transmission(n, v, extra.direction, colour, thickness));
                }
                #endif

                // Ambient. Near black by design: the dark field is the whole look. Falls with
                // depth as the key does under item 3.
                //
                // The board's light: the ambient in grey, at its own strength, without the water's teal.
                float3 ambient = SampleSH(n);
                [branch] if (_BoardLight > 0.5) ambient = BoardWhite(ambient);

                lit += body * ambient * depthLight;

                // The rim, carrying the guild. Muted before it arrives: TheatrePalette caps the
                // saturation and lightness in HSL, because raw RGB gene colours read garish and
                // Species: ALRE moved off them for that reason [SP1] [SP2].
                float facing = 1.0 - saturate(dot(n, v));

                // Under the soft dial a lower power, so the rim falls off softly into the body.
                float rimPower = _RimPower;
                [branch] if (_Soft > 0.5) rimPower *= SoftRimPower;

                // Velvet: wider and softer still, and stronger at grazing angles (below).
                [branch] if (_Velvet > 0.5) rimPower *= VelvetRimPower;

                float rim = pow(facing, rimPower);

                [branch] if (_Velvet > 0.5) rim *= lerp(1.0, VelvetRimGain, smoothstep(VelvetRimLow, 1.0, facing));

                // A frilled edge on a part that is not a blade (the bold tissue looks; a blade's outline is
                // cut, in LeafScallop): the silhouette band is pleated in shading, the rim brighter on a
                // crest and nearly gone in a notch, and the body's light held back in a notch, so the edge
                // reads as a row of lobes. Shading and not a cut, since the body is opaque and a notch would
                // need a pixel to be dropped, which the depth passes would not share; nothing moves.
                float frillBody = 1.0;

                [branch] if (_Frill > 0.001)
                {
                    if (input.leaf.w < 0.5)
                    {
                        float frillPixels = (1.0 / FrillCellsPerMetre) / max(pixelMetres, 1e-6);
                        float frillStrength = saturate(_Frill) *
                            smoothstep(FrillFadeLow, FrillFadeHigh, frillPixels) *
                            smoothstep(FrillEdgeLow, FrillEdgeHigh, facing);
                        float pleat = FrillPleat(input.positionOS, v);

                        rim *= lerp(1.0, lerp(FrillNotchRim, FrillCrestRim, pleat), frillStrength);
                        frillBody = lerp(1.0, lerp(FrillNotchBody, 1.0, pleat), frillStrength);
                    }
                }

                // A link's segments (_LinkShape.w; the constants and the reasons are with Link*): a faint groove
                // between segments, and at the silhouette a pale lobe in each segment with a few fine dark bristle
                // lines across it, the groove between two lobes nearly dark, so the edge reads as a row of lobes
                // as a worm's parapodia do. Shading alone, as the frill is: the rim over a lobe and in a groove,
                // a pale rim added over a lobe, and the body's light held back in a groove. What it leaves for the
                // lines below is the pale lobe's weight and the body's factor.
                float linkBody = 1.0;
                float linkPale = 0.0;

                [branch] if (_LinkShape.w > 0.5)
                {
                    float linkFade = smoothstep(LinkFadeLow, LinkFadeHigh, linkPixels);
                    float bristleFade = smoothstep(LinkFadeLow, LinkFadeHigh, linkPixels / LinkBristlesPerSegment);

                    float linkC = 0.5 - 0.5 * cos(6.2831853 * linkQ);
                    float linkGroove = pow(saturate(1.0 - linkC), LinkGroovePower);
                    float linkLobe = smoothstep(LinkLobeLow, LinkLobeHigh, linkC);
                    float linkSilhouette = smoothstep(LinkEdgeLow, LinkEdgeHigh, facing);
                    float linkEdge = linkSilhouette * linkFade;
                    float linkBristle = pow(0.5 + 0.5 * cos(6.2831853 * LinkBristlesPerSegment * linkQ), LinkBristlePower);
                    float linkBristled = 1.0 - LinkBristleDark * linkBristle * linkLobe * bristleFade;

                    rim *= lerp(1.0, lerp(LinkNotchRim, LinkLobeRim, linkLobe), linkEdge) *
                           lerp(1.0, linkBristled, linkEdge);

                    linkPale = LinkPaleGain * linkLobe * linkEdge * linkBristled;
                    linkBody = 1.0 - linkFade * linkGroove * lerp(LinkGrooveDark, LinkNotchDark, linkSilhouette);
                }

                // Under _EvoBodyWater.x the rim and the glow are lit by the water's own light at this depth,
                // red first, rather than shining as if from inside.
                float3 waterLight = lerp(float3(1.0, 1.0, 1.0), depthLight, saturate(bodyWater.x));
                lit += rimColour * waterLight * (rim * _RimStrength * rimScale * (1.0 - saturate(bodyWater.y)));

                // A link's pale lobes (_LinkShape.w): the rim's own colour lifted, at the rim's strength.
                [branch] if (_LinkShape.w > 0.5)
                {
                    lit += min(rimColour * LinkPaleLift, float3(1.0, 1.0, 1.0)) * waterLight *
                        (linkPale * _RimStrength * rimScale * (1.0 - saturate(bodyWater.y)));
                }

                // The inner glow: a well fed body is lit from inside, a starving one is pale.
                // The same reading the palette's tint already carries, on the same per body
                // property, so the two cannot disagree.
                //
                // Under _Sss it is seen from inside instead: brightest where the eye looks straight
                // through the body and falling toward the edge, the rim's inverse, dimmer on a part too
                // thick to see into, and in the colour of the light through the tissue at the rim's
                // brightness, so the reserve, the dimming and the corpse's grey still reach it.
                float3 glowTint = rimColour;
                float glowShape = 0.30 + 0.70 * pow(facing, 0.7);

                [branch] if (_Sss > 0.5)
                {
                    float rimLuma = dot(rimColour, LumaWeights);
                    glowTint = transTint * (rimLuma / max(1e-4, dot(transTint, LumaWeights)));
                    glowShape = (SssGlowFloor + (1.0 - SssGlowFloor) * pow(saturate(dot(n, v)), SssGlowPower)) *
                        lerp(SssGlowThick, 1.0, thickness);
                }

                lit += glowTint * waterLight * (_GlowStrength * _Reserve * rimScale * (1.0 - saturate(bodyWater.z)) *
                    glowShape);

                // Caustics, on the upward faces of whatever is in the top few metres.
                //
                // The fourth day's fourth item: the net is the surface above this body, focused,
                // read from the one wave field the ceiling and the shafts are drawn from
                // (TheatreWater.hlsl, EvoCausticNet). Until then it was a Voronoi cell wall
                // panned in two directions, which looked like caustics and was the shadow of
                // nothing at all; on a body it could not agree with the water above it because
                // there was no water above it (logbook/specs/skin-spec-4.md).
                float fade = EvoCausticFade(input.positionWS, _CausticReach);

                // The board's light: no caustics, which are the water's light and coloured by it.
                [branch] if (_BoardLight > 0.5) fade = 0.0;
#if defined(_EVO_BODY_DAPPLE)

                // Dappled light (EVOSIM_THEATRE_BODY_DAPPLE): in place of the caustic below, on a body that has one
                // (_CausticStrength over zero, so not a neck) and while the dial is not held off (_EvoDapple). The
                // same fade to _CausticReach, so it lives in the top metres; the same clock, so it moves; and the side
                // wrap, so a side face catches some of it. It reads the world position, as the caustic does, so it
                // stays on the water's light and not on the screen. Clearing the fade skips the caustic below.
                [branch] if (_EvoDapple > 0.0 && fade > 0.001 && _CausticStrength > 0.0)
                {
                    float dapple = EvoDappleNet(input.positionWS, EvoTime());

                    lit += _CausticColor.rgb * (dapple * fade * saturate(DappleSideFloor + DappleSideSlope * n.y) * _EvoDapple);
                    fade = 0.0;
                }
#endif

                if (fade > 0.001)
                {
                    float net = EvoCausticNet(input.positionWS, EvoTime());

                    lit += _CausticColor.rgb * (net * fade * saturate(n.y) * _CausticStrength);
                }

#if defined(_EVO_LOOK_PROBE) && defined(_EVO_THALLI)
                // LOOK_PROBE 2 (a diagnostic, never a film): a sea-lettuce thallus draws magenta wherever the tone multiply
                // below reaches it, which shows whether a tone at this line is live on a thallus.
                [branch] if (_EvoLookProbe > 1.5 && thallusMesh && ThalliIsUlva(input.shape.x)) leafTone = float3(1.0, 0.0, 1.0);
                // LOOK_PROBE 3 begin
                // LOOK_PROBE 3 (a diagnostic, never a film): every thallus draws a flat colour by its form, sea lettuce red, Padina
                // green, Codium blue, wherever the tone multiply below reaches it, which shows what form each thallus is.
                [branch] if (_EvoLookProbe > 2.5 && thallusMesh)
                {
                    if (ThalliIsUlva(input.shape.x)) leafTone = float3(1.0, 0.0, 0.0);
                    else if (abs(input.shape.x - ThalliFlagPadina) < ThalliFlagTolerance) leafTone = float3(0.0, 1.0, 0.0);
                    else if (ThalliIsCodium(input.shape.x)) leafTone = float3(0.0, 0.0, 1.0);
                }
                // LOOK_PROBE 3 end
#endif
                lit *= leafTone;

                [branch] if (_Frill > 0.001) lit *= frillBody;

                // The grooves between a link's segments (_LinkShape.w).
                [branch] if (_LinkShape.w > 0.5) lit *= linkBody;

                // The osculum's hole keeps only a share of the light (_Velvet).
                [branch] if (_Velvet > 0.5) lit *= velvetShade;
#if defined(_EVO_NOSE_PIT)

                // The pit's lining keeps only a share of the light, the rim and the glow included: an occlusion, and
                // no light of its own.
                lit *= 1.0 - PitLightDark * pitDark;
#endif
#if defined(_EVO_MOUTH2)

                // The throat keeps only a share of the light, the rim and the glow included: an occlusion, and no light of
                // its own.
                lit *= 1.0 - MouthLightDark * mouthThroat;

                // The relief's inner wall (EVOSIM_THEATRE_MOUTH2_RELIEF) is occluded by its depth.
                [branch] if (mouthOcclude > 0.0) lit *= 1.0 - MouthLightDark * mouthOcclude;
#endif

                // Every cell's own shade: a little lighter or darker, a little warmer or cooler
                // than its family's, so a clade that shares one plan is a crowd and not copies.
                float shade = frac(_Individual * 17.37 + 0.41) * 2.0 - 1.0;
                float warmth = frac(_Individual * 31.91 + 0.07) * 2.0 - 1.0;
                lit *= (1.0 + 0.08 * shade) * float3(1.0 + 0.05 * warmth, 1.0, 1.0 - 0.07 * warmth);

#if defined(_EVO_DEATH_DRAIN)
#if defined(_EVO_DEATH_MILK)
                // The death look (EVOSIM_THEATRE_DEATH_LOOK; DeathMilk): in place of the grey, the albedo goes to milk and the light the
                // body took is kept, before the water's fog. The pixel's size is taken in the open, outside the branch, for its derivative.
                float deathMilkPixel = length(fwidth(input.positionWS));
                [branch] if (_DeathDrain > 0.0)
                {
                    // Variant 3 (DeathMilk3) by its own function, lit by the light a grey card here takes; variants 1 and 2 as they were.
                    [branch] if (_DeathMilk.y > 2.5)
                    {
                        float3 deathCard = keyColour * (Wrapped(n, main.direction) * _KeyGain) + ambient * depthLight;
                        // The shade: the key's light the wrap lends the side away from it, taken back, in light per unit albedo as
                        // DeathMilk3's taken is. Only the key's: the ambient and the fill a living body keeps on its dark side stay
                        // (pale11's first trial scaled all of it and drew the underside as a black cut-out).
                        float keyLoss = 0.0;
                        [branch] if (_EvoDeathPaleShade > 0.0)
                        {
                            float keyWrapped = dot(keyColour, LumaWeights) * Wrapped(n, main.direction) * _KeyGain;
                            float keyLambert = dot(keyColour, LumaWeights) * saturate(dot(n, main.direction)) * _KeyGain;
                            keyLoss = _EvoDeathPaleShade * max(keyWrapped - keyLambert, 0.0);
                        }
                        lit = DeathMilk3(lit, body, deathPlain, input.positionWS, deathCard, keyLoss);
                    }
                    else
                    {
                        lit = DeathMilk(lit, body, input.fogAndThickness.y, input.positionOS * PartSizes(), PartSizes(), deathMilkPixel);
                    }
                }
#else
                // The death cue's drain: the body as it is drawn, every layer of its look included, faded toward its own luminance
                // times DeathDrainGrey with no hue, before the water's fog, so the grey sits in the water as the body did.
                [branch] if (_DeathDrain > 0.0)
                {
                    float deathGrey = dot(lit, float3(0.2126, 0.7152, 0.0722)) * DeathDrainGrey;
                    lit = lerp(lit, float3(deathGrey, deathGrey, deathGrey), saturate(_DeathDrain));
                }
#endif

#endif
                // The board's light: the body as it leaves its surface, with no fog over it. Kept before the
                // fog and put back after, so the fog's own line is the one it always was.
                float3 unfogged = lit;

#if defined(_EVO_LOOK_STAGE)
                // LOOK_STAGE 3: everything before the water, the light through, the sheen, the rim, the glow, the caustic and the leaf
                // tone and every shade included, and the fog not yet mixed.
                [branch] if (lookProducer && _EvoLookStage > 2.5) return half4(lit, 1.0);
#endif
                lit = EvoMixFog(lit, input.fogAndThickness.x, input.positionWS);

                [branch] if (_BoardLight > 0.5) lit = unfogged;

#if defined(_EVO_JOIN_DEBUG)
                // JOIN_DEBUG: the one skin's colour replaced by the view's false colour, unlit (nothing after this line acts on it).
                // The derivatives are taken in the open, before the test of the skin.
                float joinDebugStretch = length(fwidth(input.positionOS * PartSizes())) * (_SkinScale > 0.0 ? _SkinScale : 1.0) /
                    max(length(fwidth(input.positionWS)), 1e-8);
                [branch] if (skinMesh && _EvoJoinDebug > 0.5)
                    return half4(JoinDebugColour((int)(_EvoJoinDebug + 0.5), joinDebugStretch), 1.0);
#endif
                return half4(lit, _Fade);
            }
            ENDHLSL
        }

        // The body in the depth texture, at the shape the forward pass draws. URP renders this
        // pass when it needs a depth prepass and the DepthNormals pass below when a feature asks
        // for normals too (the renderer's ambient occlusion does, so that is the one a film's
        // depth of field reads today). Both are the vertex stage and nothing else.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma target 3.0
            #pragma multi_compile_local _ _ONE_SKIN
            #pragma multi_compile_local _ _EVO_PATTERN_AA
            #pragma multi_compile_local _ _EVO_SKIN_JOIN
            #pragma multi_compile_local _ _EVO_JOIN_DEBUG
            #pragma multi_compile _ _EVO_NOSE_PIT
            #pragma multi_compile _ _EVO_DEBUG_TINT
            #pragma multi_compile _ _EVO_THALLI
            #pragma multi_compile _ _EVO_NOSE_SKIN
            #pragma multi_compile _ _EVO_BODY_DAPPLE
            #pragma multi_compile _ _EVO_MOUTH2
            #pragma multi_compile _ _EVO_SKIN2
            #pragma multi_compile _ _EVO_SKIN2B
            #pragma multi_compile _ _EVO_SKIN2_BAND4
            #pragma multi_compile _ _EVO_SHEEN_CAP
            #pragma multi_compile _ _EVO_SHEEN_CAP2
            #pragma multi_compile _ _EVO_UPTAKE
            #pragma multi_compile _ _EVO_ALGA_BLADE
            #pragma multi_compile _ _EVO_ALGA_BLADE2
            #pragma multi_compile _ _EVO_ALGA_RAMP_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_TARGET
            #pragma multi_compile _ _EVO_ALGA_BLADE3
            #pragma multi_compile _ _EVO_ALGA_ARC
            #pragma multi_compile _ _EVO_ALGA_ARC_DRIVE
            #pragma multi_compile _ _EVO_ALGA_FLUTTER
            #pragma multi_compile _ _EVO_ALGA_FLUTTER_NOISE
            #pragma multi_compile _ _EVO_SKIN_MAP

            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(ShapeBody(input).positionWS);
            }

            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        // The depth and the normal. The normal is the shaped surface's (the taper, the bend and
        // the leaf's own), not the carve's per pixel relief: that relief is a noise read a pixel
        // at a time, and nothing reads the normals texture's fine detail today, so the prepass is
        // spared it. It is the forward pass's CarvedNormal away if the occlusion ever wants it.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma target 3.0
            #pragma multi_compile_local _ _ONE_SKIN
            #pragma multi_compile_local _ _EVO_PATTERN_AA
            #pragma multi_compile_local _ _EVO_SKIN_JOIN
            #pragma multi_compile_local _ _EVO_JOIN_DEBUG
            #pragma multi_compile _ _EVO_NOSE_PIT
            #pragma multi_compile _ _EVO_DEBUG_TINT
            #pragma multi_compile _ _EVO_THALLI
            #pragma multi_compile _ _EVO_NOSE_SKIN
            #pragma multi_compile _ _EVO_BODY_DAPPLE
            #pragma multi_compile _ _EVO_MOUTH2
            #pragma multi_compile _ _EVO_SKIN2
            #pragma multi_compile _ _EVO_SKIN2B
            #pragma multi_compile _ _EVO_SKIN2_BAND4
            #pragma multi_compile _ _EVO_SHEEN_CAP
            #pragma multi_compile _ _EVO_SHEEN_CAP2
            #pragma multi_compile _ _EVO_UPTAKE
            #pragma multi_compile _ _EVO_ALGA_BLADE
            #pragma multi_compile _ _EVO_ALGA_BLADE2
            #pragma multi_compile _ _EVO_ALGA_RAMP_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_PHOTO
            #pragma multi_compile _ _EVO_ALGA_ROUND_TARGET
            #pragma multi_compile _ _EVO_ALGA_BLADE3
            #pragma multi_compile _ _EVO_ALGA_ARC
            #pragma multi_compile _ _EVO_ALGA_ARC_DRIVE
            #pragma multi_compile _ _EVO_ALGA_FLUTTER
            #pragma multi_compile _ _EVO_ALGA_FLUTTER_NOISE
            #pragma multi_compile _ _EVO_SKIN_MAP

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVertex(Attributes input)
            {
                BodyVertex body = ShapeBody(input);

                DepthNormalsVaryings output;
                output.positionCS = TransformWorldToHClip(body.positionWS);
                output.normalWS = body.normalWS;

                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octahedral = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(octahedral), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
