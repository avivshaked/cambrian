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
//   vertex puff along the smoothed normal, bounded                                       [CY]
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

        [Header(Shape)]
        // Bounded, and the bound is the whole point. TheatreMeshes generates every mesh inset to
        // (1 - this) of the collider's half extent, so a puff of exactly this fraction lands on
        // the collider surface and never past it. TheatreSkin clamps the value it sets to what
        // the meshes were actually built for, so the two cannot drift apart.
        _PuffFraction("Vertex puff, fraction of the smallest half extent", Range(0, 0.03)) = 0.03
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
                float _PuffFraction;
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

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                float3 halfExtents = HalfExtentsWS();
                float smallest = min(halfExtents.x, min(halfExtents.y, halfExtents.z));

                // Displaced in world space, not object space. The visuals carry a non uniform
                // scale (a box part is 2h on each axis), so an object space push of a fixed
                // length would come out longer on the long axis and would break the bound this
                // whole arrangement exists to keep.
                positionWS += normalWS * (_PuffFraction * smallest);

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                // A thin part transmits and a thick one does not. Faked from the geometry rather
                // than from a baked thickness map, which is what the single pass approximation
                // leaves to the author [JA]: there is no unwrapped mesh here to bake into.
                float thickness = saturate(_TransMetres / max(1e-4, smallest));

                output.fogAndThickness = float2(ComputeFogFactor(output.positionCS.z), thickness);

                return output;
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
                float3 n = normalize(input.normalWS);
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
