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
//   a flat box drawn as a leaf (2026-09-24, the owner's "these flat leaves" and his ruling on
//                            the curl): an outline, a lens cross-section, veins, and a curl of
//                            up to a tenth of the width, the one thing drawn outside a collider
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

        // The transmission is the guild's hue at a gain by guild (TheatrePalette): a producer
        // glows through when backlit, an eater stays opaque, a strut between. The design pass's
        // second ruling (2026-09-16): the guild reaches the face only as light coming through
        // the tissue, never as paint on it.
        _TransTint("Transmission tint, the guild's", Color) = (0.34, 0.72, 0.36, 1)
        _TransGain("Transmission gain by guild", Range(0, 2)) = 1

        // A wet sheen, by guild (TheatrePalette): the one surface the owner allowed the
        // absorptive tissue as its own (2026-09-16, "no organ the genome doesn't encode"), a
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _Reserve;
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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                // What solid this mesh is, baked per vertex by TheatreMeshes.Finish: x is one on
                // the box and zero on everything else, y is the mesh's own half extent in object
                // units. A mesh that carries no fourth UV channel reads zero here, which is the
                // answer that leaves it undeformed, so an unrecognised mesh drawn with this
                // material gets the carve and nothing else.
                float2 shapeOS    : TEXCOORD3;

                // Where the vertex is on a leaf, for the leaf mesh alone (TheatreMeshes.Lamina):
                // s from base to tip, t from rim to rim, and which face. Zero on every other mesh.
                float4 leafOS     : TEXCOORD4;
            };

            // The part's half extents in metres, taken from the object to world matrix rather
            // than passed in. PhenotypeBuilder gives each visual a localScale equal to the part's
            // full size (VisualPlan), and the parts themselves are never scaled, so the column
            // lengths of the matrix are that size. Half of it is the half extent, exactly for the
            // cube and the sphere and conservatively (by a factor of two) for a capsule's shaft,
            // whose mesh is two units tall rather than one.
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

            float CarveDepth(float3 positionOS, float smallest)
            {
                float pinch = EvoPinch(positionOS, _PinchA) + EvoPinch(positionOS, _PinchB);

                float fraction = _CarveFraction * _Carve.w * (1.0 + _PinchGain * pinch);

                return min(fraction, _CarveMaximum) * smallest;
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

                if (halfExtents.x >= halfExtents.y && halfExtents.x >= halfExtents.z)
                    axisA = float3(1, 0, 0);
                else if (halfExtents.y >= halfExtents.z)
                    axisA = float3(0, 1, 0);
                else
                    axisA = float3(0, 0, 1);

                // The two across it, in a fixed cyclic order, and the mask of both together.
                float3 axisB = float3(axisA.z, axisA.x, axisA.y);
                float3 axisC = float3(axisA.y, axisA.z, axisA.x);
                float3 across = 1.0 - axisA;

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

                return l;
            }

            // The outline's half width at s (0 the base, 1 the tip), as a fraction of the box's
            // half width: zero at both ends, one at most. The base tapers as a wedge and the tip
            // is round, from two exponents on one arch that meet at its crown, where the arch is
            // flat, so the join has no kink. The lobes cut inward only, so the outline stays
            // inside the box.
            float LeafWidth(float s, LeafShape l)
            {
                s = saturate(s);
                float u = pow(s, l.widest);
                float arch = max(1e-4, sin(PI * u));
                float g = pow(arch, u < 0.5 ? l.baseFull : l.tipFull);
                float lobes = 0.5 + 0.5 * sin(6.2831853 * (l.lobes * s + l.phase));
                return g * (1.0 - l.lobe * lobes * sin(PI * s));
            }

            // The leaf's frame: T the thinnest axis, L the longer of the other two, W their
            // cross, so the frame is a rotation of the mesh's own and the winding survives it.
            void LeafAxes(float3 he, out float3 axisL, out float3 axisT, out float3 axisW)
            {
                if (he.x <= he.y && he.x <= he.z)
                {
                    axisT = float3(1, 0, 0);
                    axisL = he.y >= he.z ? float3(0, 1, 0) : float3(0, 0, 1);
                }
                else if (he.y <= he.z)
                {
                    axisT = float3(0, 1, 0);
                    axisL = he.x >= he.z ? float3(1, 0, 0) : float3(0, 0, 1);
                }
                else
                {
                    axisT = float3(0, 0, 1);
                    axisL = he.x >= he.y ? float3(1, 0, 0) : float3(0, 1, 0);
                }

                axisW = cross(axisL, axisT);
            }

            // One point of the leaf in its own frame (x along, y thick, z across), object units.
            // baseSign says which end of the box the base is at; the width axis turns with it so
            // the frame stays a rotation. flat drops the curl, for the carve's field, which is a
            // property of the tissue and should not move when the leaf is drawn curled.
            float3 LeafPoint(float s, float t, float side, LeafShape l, float baseSign,
                             float meshHalf, float curlObj, float flat, float flesh, out float thickness)
            {
                float w = LeafWidth(s, l);
                float ends = pow(max(1e-4, sin(PI * pow(saturate(s), l.widest))), 0.3);
                float across = t * w;

                // The lens: full at the midrib, which stands a little proud, and thinning to
                // nothing at the rim; thinner towards the tip.
                float lens = sqrt(saturate(1.0 - t * t)) * (0.6 + 0.4 * exp(-t * t / 0.012));
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

                return float3(along, side * thickness + (1.0 - flat) * curlObj * lift, baseSign * meshHalf * across);
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

                LeafShape l = LeafOf(_Carve.x, _Individual);

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
                halfWidthMetres = hW;

                float s = leafOS.x;
                float t = leafOS.y;
                float side = leafOS.z;

                float thick;
                float3 p = LeafPoint(s, t, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, thick);
                float unused;
                float3 f = LeafPoint(s, t, side, l, baseSign, meshHalf, curlObj, 1.0, flesh, unused);

                positionOS = axisL * p.x + axisT * p.y + axisW * p.z;
                flatOS = axisL * f.x + axisT * f.y + axisW * f.z;

                // The normal from two differences in metres, not object units: the sheet is
                // stretched by the part's three sizes, and a normal taken before the stretch would
                // shade a long leaf as if it were square. Off the ends and the rim, where the
                // outline's width and the lens's slope run to nothing and infinity.
                float3 metres = 2.0 * float3(hL, hT, hW);
                float sd = clamp(s, 0.01, 0.98);
                float td = clamp(t, -0.985, 0.975);
                float3 q = LeafPoint(sd, td, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, unused) * metres;
                float3 qs = LeafPoint(sd + 0.01, td, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, unused) * metres;
                float3 qt = LeafPoint(sd, td + 0.01, side, l, baseSign, meshHalf, curlObj, 0.0, flesh, unused) * metres;

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
            };

            BodyVertex ShapeBody(Attributes input)
            {
                BodyVertex body = (BodyVertex)0;

                float3 halfExtents = HalfExtentsWS();
                float smallest = min(halfExtents.x, min(halfExtents.y, halfExtents.z));

                // Which solid this is, from the mesh itself, and how big the mesh is in its own
                // units. Anything that is not the box, the engine's own primitives included, reads
                // zero here and is left to the carve alone (TheatreMeshes.Finish).
                bool lamina = input.shapeOS.x > 1.5;
                bool box = input.shapeOS.x > 0.5 && !lamina;
                float meshHalf = max(1e-4, input.shapeOS.y);

                float3 shapedOS = input.positionOS.xyz;
                float3 shapedNormalOS = input.normalOS;

                if (box) ShapeBox(shapedOS, shapedNormalOS, halfExtents, smallest, meshHalf);

                float3 positionWS = TransformObjectToWorld(shapedOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(shapedNormalOS));

                // The leaf: shaped here from where the vertex is on it, and carved at its flat
                // position so an impression stays on the tissue whatever the curl does.
                float3 carveOS = input.positionOS.xyz;
                float halfThickMetres = smallest;
                float halfWidthMetres = 0.0;
                float4 leaf = float4(0, 0, 0, 0);

                if (lamina)
                {
                    ShapeLamina(input.leafOS, halfExtents, meshHalf,
                        shapedOS, carveOS, normalWS, halfThickMetres, halfWidthMetres, leaf);
                    positionWS = TransformObjectToWorld(shapedOS);
                }

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
                float depth = CarveDepth(carveOS, smallest);

                // A leaf thins to nothing at its rim, so the carve there is bounded by the
                // thickness the lens left, or it would cut through to the other face.
                if (lamina) depth = min(depth, 0.5 * halfThickMetres);

                float3 unusedGradient;
                float carve = EvoCarve(
                    carveOS + OwnShift(), _Carve.x, _Carve.y, _Carve.z,
                    EvoCarveDetail(smallest), unusedGradient);

                // Along the deformed normal, so the impressions cut into the bent surface rather
                // than into the box the mesh started as.
                float3 inward = -normalWS * (depth * carve);

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

                body.positionWS = positionWS;
                body.normalWS = normalWS;
                body.carveOS = carveOS;
                body.carveDepth = depth;
                body.leaf = leaf;
                body.halfThickMetres = halfThickMetres;
                body.halfWidthMetres = halfWidthMetres;

                return body;
            }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

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
                // same question the vertex asked it, and the depth in metres it was asked with.
                float3 positionOS : TEXCOORD3;
                float carveDepth  : TEXCOORD4;

                // On a leaf: s, the distance across the half width (-1 to 1), t, and one. Zero
                // elsewhere, which draws no veins.
                float4 leaf       : TEXCOORD5;
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
                float margin = smoothstep(0.8, 1.0, abs(t));

                face = 1.0 + 0.35 * vein - 0.08 * margin;
                through = 1.0 - 0.6 * vein;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                BodyVertex body = ShapeBody(input);

                output.positionWS = body.positionWS;
                output.normalWS = body.normalWS;
                output.positionOS = body.carveOS;
                output.carveDepth = body.carveDepth;
                output.positionCS = TransformWorldToHClip(body.positionWS);
                output.leaf = body.leaf;

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
            float3 CarvedNormal(float3 normalWS, float3 positionOS, float depth)
            {
                float3 he = HalfExtentsWS();
                float smallest = min(he.x, min(he.y, he.z));

                float3 gradientOS;
                EvoCarve(positionOS + OwnShift(), _Carve.x, _Carve.y, _Carve.z, EvoCarveDetail(smallest), gradientOS);

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

            float3 Transmission(float3 n, float3 v, float3 l, float3 lightColour, float thickness)
            {
                // Barre-Brisebois and Bouchard's single pass approximation [BB11] in Austin's URP
                // arrangement [JA]: the light direction is bent by the normal, and what the eye
                // catches is the part of it coming back towards the camera from behind the body.
                float3 bent = normalize(l + n * _TransDistortion);
                float back = pow(saturate(dot(v, -bent)), _TransPower) * _TransScale;

                return lightColour * (back * thickness);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 n = CarvedNormal(
                    normalize(input.normalWS), input.positionOS, input.carveDepth);

                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));

                float thickness = input.fogAndThickness.y;

                float3 body = _BaseColor.rgb;

                // The mottle. Scaled in world metres, so a body twice as long carries twice as
                // many cells rather than the same pattern stretched, which is the tell that a
                // texture is following the object rather than the tissue.
                float f1, f2, tone;
                EvoVoronoi(input.positionWS * _MottleCellsPerMetre, f1, f2, tone);

                float wall = saturate((f2 - f1) * 2.5);
                float mottle = lerp(1.0 - _MottleStrength, 1.0 + _MottleStrength, tone);
                mottle *= lerp(1.0 - _MottleStrength * 0.7, 1.0, wall);

                body *= mottle;

                float veinFace, veinThrough;
                LeafVeins(input.leaf, veinFace, veinThrough);
                body *= veinFace;
                thickness *= veinThrough;

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


                    // The guild's rim is at the edge of a solid; on a blade's face it is paint.
                    rimScale = 0.45;
                }

                // Key light.
                Light main = GetMainLight();
                float3 lit = body * main.color * (Wrapped(n, main.direction) * _KeyGain);
                lit += _TransTint.rgb * (_TransGain * Transmission(n, v, main.direction, main.color, thickness));

                // The light from the surface through a blade, seen from below or beside it: the
                // world's light comes from above, whatever the key does for the camera.
                if (input.leaf.w > 0.5)
                {
                    lit += _TransTint.rgb * (_TransGain * _LeafSkyGlow *
                        Transmission(n, v, float3(0.0, 1.0, 0.0), main.color, thickness));
                }

                // The sheen: a tight highlight off the key, white rather than the body's colour,
                // so it reads as wet skin catching the light and not as paint.
                float3 halfway = normalize(main.direction + v);
                lit += main.color * (_Sheen * pow(saturate(dot(n, halfway)), 28.0));

                // The fill, and any other light in the scene. TheatreSkin puts one weak
                // directional light opposite the key so the far side of a body is dark rather
                // than lost; in URP a second directional light is an additional light.
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    float3 colour = extra.color * extra.distanceAttenuation;

                    lit += body * colour * Wrapped(n, extra.direction);
                    lit += _TransTint.rgb * (_TransGain * Transmission(n, v, extra.direction, colour, thickness));
                }
                #endif

                // Ambient. Near black by design: the dark field is the whole look.
                lit += body * SampleSH(n);

                // The rim, carrying the guild. Muted before it arrives: TheatrePalette caps the
                // saturation and lightness in HSL, because raw RGB gene colours read garish and
                // Species: ALRE moved off them for that reason [SP1] [SP2].
                float facing = 1.0 - saturate(dot(n, v));
                float rim = pow(facing, _RimPower);

                lit += _RimColor.rgb * (rim * _RimStrength * rimScale);

                // The inner glow: a well fed body is lit from inside, a starving one is pale.
                // The same reading the palette's tint already carries, on the same per body
                // property, so the two cannot disagree.
                lit += _RimColor.rgb * (_GlowStrength * _Reserve * rimScale * (0.30 + 0.70 * pow(facing, 0.7)));

                // Caustics, on the upward faces of whatever is in the top few metres.
                //
                // The fourth day's fourth item: the net is the surface above this body, focused,
                // read from the one wave field the ceiling and the shafts are drawn from
                // (TheatreWater.hlsl, EvoCausticNet). Until then it was a Voronoi cell wall
                // panned in two directions, which looked like caustics and was the shadow of
                // nothing at all; on a body it could not agree with the water above it because
                // there was no water above it (logbook/specs/skin-spec-4.md).
                float fade = EvoCausticFade(input.positionWS, _CausticReach);

                if (fade > 0.001)
                {
                    float net = EvoCausticNet(input.positionWS, _Time.y);

                    lit += _CausticColor.rgb * (net * fade * saturate(n.y) * _CausticStrength);
                }

                lit *= leafTone;

                // Every cell's own shade: a little lighter or darker, a little warmer or cooler
                // than its family's, so a clade that shares one plan is a crowd and not copies.
                float shade = frac(_Individual * 17.37 + 0.41) * 2.0 - 1.0;
                float warmth = frac(_Individual * 31.91 + 0.07) * 2.0 - 1.0;
                lit *= (1.0 + 0.08 * shade) * float3(1.0 + 0.05 * warmth, 1.0, 1.0 - 0.07 * warmth);

                lit = EvoMixFog(lit, input.fogAndThickness.x, input.positionWS);

                return half4(lit, 1.0);
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
