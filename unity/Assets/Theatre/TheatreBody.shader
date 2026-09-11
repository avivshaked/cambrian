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
// snapshot camera takes.

Shader "Evosim/Theatre Body"
{
    Properties
    {
        [Header(Per body from TheatrePalette)]
        _BaseColor("Body colour", Color) = (0.10, 0.16, 0.17, 1)
        _RimColor("Guild rim colour", Color) = (0.34, 0.72, 0.36, 1)
        _Reserve("Reserve fraction, 0 starving to 1 sated", Range(0, 1)) = 1

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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _Reserve;
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
            };

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

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 halfExtents = HalfExtentsWS();
                float smallest = min(halfExtents.x, min(halfExtents.y, halfExtents.z));

                // Which solid this is, from the mesh itself, and how big the mesh is in its own
                // units. Anything that is not the box, the engine's own primitives included, reads
                // zero here and is left to the carve alone (TheatreMeshes.Finish).
                bool box = input.shapeOS.x > 0.5;
                float meshHalf = max(1e-4, input.shapeOS.y);

                float3 shapedOS = input.positionOS.xyz;
                float3 shapedNormalOS = input.normalOS;

                if (box) ShapeBox(shapedOS, shapedNormalOS, halfExtents, smallest, meshHalf);

                float3 positionWS = TransformObjectToWorld(shapedOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(shapedNormalOS));

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
                float depth = CarveDepth(input.positionOS.xyz, smallest);

                float3 unusedGradient;
                float carve = EvoCarve(
                    input.positionOS.xyz, _Carve.x, _Carve.y, _Carve.z, unusedGradient);

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

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionOS = input.positionOS.xyz;
                output.carveDepth = depth;
                output.positionCS = TransformWorldToHClip(positionWS);

                // A thin part transmits and a thick one does not. Faked from the geometry rather
                // than from a baked thickness map, which is what the single pass approximation
                // leaves to the author [JA]: there is no unwrapped mesh here to bake into.
                float thickness = saturate(_TransMetres / max(1e-4, smallest));

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
                float3 gradientOS;
                EvoCarve(positionOS, _Carve.x, _Carve.y, _Carve.z, gradientOS);

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

                // Key light.
                Light main = GetMainLight();
                float3 lit = body * main.color * (Wrapped(n, main.direction) * _KeyGain);
                lit += body * _TransColor.rgb * Transmission(n, v, main.direction, main.color, thickness);

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
                    lit += body * _TransColor.rgb * Transmission(n, v, extra.direction, colour, thickness);
                }
                #endif

                // Ambient. Near black by design: the dark field is the whole look.
                lit += body * SampleSH(n);

                // The rim, carrying the guild. Muted before it arrives: TheatrePalette caps the
                // saturation and lightness in HSL, because raw RGB gene colours read garish and
                // Species: ALRE moved off them for that reason [SP1] [SP2].
                float facing = 1.0 - saturate(dot(n, v));
                float rim = pow(facing, _RimPower);

                lit += _RimColor.rgb * (rim * _RimStrength);

                // The inner glow: a well fed body is lit from inside, a starving one is pale.
                // The same reading the palette's tint already carries, on the same per body
                // property, so the two cannot disagree.
                lit += _RimColor.rgb * (_GlowStrength * _Reserve * (0.30 + 0.70 * pow(facing, 0.7)));

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

                lit = MixFog(lit, input.fogAndThickness.x);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
