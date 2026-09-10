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
//   caustics from above      [CY2] [AM]
//   inward vertex carve, two octaves of noise in the part's own object space, bounded,
//                            with the normal rebuilt per pixel from the same field         [CY]
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
        _RimPower("Rim sharpness", Range(0.5, 8)) = 2.6
        _RimStrength("Rim strength", Range(0, 4)) = 1.5
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
        _CausticColor("Caustic colour", Color) = (0.55, 0.85, 0.95, 1)
        _CausticStrength("Caustic strength on bodies", Range(0, 2)) = 0.35
        _CausticMetresPerCell("Caustic metres per cell", Range(0.2, 20)) = 3.0
        _CausticReach("Metres below the surface caustics reach", Range(1, 200)) = 10

        [Header(Carve)]
        // The dial the second day is about, and the bound is the whole point. The displacement
        // below is -normal * depth * noise with the noise in [0, 1], so it is never positive and
        // a carved vertex can only move away from the collider. depth is this fraction of the
        // part's SMALLEST half extent, which is at most that fraction of the half extent on any
        // axis, so the deepest impression on the longest body is still a fraction of its
        // thinnest dimension. TheatreSkin reads EVOSIM_THEATRE_CARVE into it and clamps.
        _CarveFraction("Carve depth, fraction of the smallest half extent", Range(0, 0.5)) = 0.2

        // The hard cap, after the joint pinch has deepened the carve. Nothing about the collider
        // needs it: it stops a body from being cut past its own middle and turning inside out.
        _CarveMaximum("Deepest carve of any kind, same fraction", Range(0, 0.6)) = 0.5

        _PinchGain("How much deeper the carve runs at a joint anchor", Range(0, 4)) = 1.6

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
                float _CausticMetresPerCell;
                float _CausticReach;
                float _CarveFraction;
                float _CarveMaximum;
                float _PinchGain;
                float4 _Carve;
                float4 _PinchA;
                float4 _PinchB;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                float3 halfExtents = HalfExtentsWS();
                float smallest = min(halfExtents.x, min(halfExtents.y, halfExtents.z));

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
                float depth = CarveDepth(input.positionOS.xyz, smallest);

                float3 unusedGradient;
                float carve = EvoCarve(
                    input.positionOS.xyz, _Carve.x, _Carve.y, _Carve.z, unusedGradient);

                positionWS -= normalWS * (depth * carve);

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
                float fade = EvoCausticFade(input.positionWS, _CausticReach);

                if (fade > 0.001)
                {
                    float net = EvoCaustics(
                        input.positionWS.xz / max(0.01, _CausticMetresPerCell), _Time.y);

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
