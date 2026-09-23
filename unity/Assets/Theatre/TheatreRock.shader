// The mushroom reefs' rock (logbook/specs/reef-spec.md; the owner, 2026-09-23 night: "an
// appropriate skin to the reefs, so they look more like a natural rocky structure").
//
// The bed's shader's family, and deliberately so: the same lighting (a wrapped main light, the
// additional lights, spherical harmonics), the same caustic net from TheatreWater.hlsl and the
// same column fog, so the rock and the sand read as one sea floor. What differs is the surface.
//
// The shape is in the mesh, not here. TheatreReefRock cuts the cap and the stem inward on the
// CPU, seeded per reef, because a vertex carve in world y (the bed's) would pull a stem's wall
// or a cap's rim out of its own shape; so this shader carves nothing and every drawn point stays
// at or inside the rock the physics pushes bodies by.
//
// The detail normal is the bed's triplanar with its whiteout blend [BG], with a rock's height in
// place of the sand's ripples, projected in the reef's own frame (object space: every reef's
// holder sits on its axis, unrotated), so nothing stretches on the stem and the grain does not
// slide over a reef the camera moves round. The colour's mottle is a solid field in the same
// frame rather than a projected one: a projection's seams show in colour where they hide in a
// normal, and a solid field has none.
//
// The tone: a lighter crust on the lit table that darkens down the rim, a darker and colder
// underside and stem, a cavity term from how deep the mesh was cut (vertex colour red), and the
// cap's shadow on everything under it. The colours arrive from TheatreSkin as linear values set
// with SetVector, so none of them goes through the engine's sRGB conversion twice or not at all.

Shader "Evosim/Theatre Rock"
{
    Properties
    {
        _RockDeep("Rock, underside and stem (linear)", Vector) = (0.0012, 0.0016, 0.0024, 1)
        _RockMid("Rock, rim (linear)", Vector) = (0.0052, 0.0056, 0.0062, 1)
        _RockCrust("Rock, encrusted table (linear)", Vector) = (0.022, 0.021, 0.017, 1)

        _MottleMetres("Metres per mottle patch", Range(0.1, 10)) = 1.4
        _MottleStrength("Mottle strength", Range(0, 1)) = 0.55
        _GrainMetres("Metres per grain", Range(0.02, 2)) = 0.2
        _GrainStrength("Grain strength", Range(0, 3)) = 2.0
        _CavityDark("How much a deep cut darkens", Range(0, 1)) = 0.6

        _CausticColor("Caustic colour", Color) = (0.55, 0.85, 0.95, 1)
        _CausticStrength("Caustic strength", Range(0, 3)) = 0.35
        _CausticReach("Metres below the surface caustics reach", Range(1, 400)) = 18

        _ShadeUnder("Main light left under the cap", Range(0, 1)) = 0.3
        _Wrap("Diffuse wrap", Range(0, 1)) = 0.3

        // Per reef, through a MaterialPropertyBlock: the reef frame's cap top and underside
        // heights, its largest radius, and its seed in [0, 1).
        _CapTopLocal("Cap top, reef frame", Float) = 1
        _CapUnderLocal("Cap underside, reef frame", Float) = -1
        _CapRadius("Cap radius", Float) = 4
        _RockSeed("Seed", Float) = 0
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

            // Two sided, for the bed's reason: the fly camera can go inside the rock.
            Cull Off
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
                float4 _RockDeep;
                float4 _RockMid;
                float4 _RockCrust;
                float _MottleMetres;
                float _MottleStrength;
                float _GrainMetres;
                float _GrainStrength;
                float _CavityDark;
                float4 _CausticColor;
                float _CausticStrength;
                float _CausticReach;
                float _ShadeUnder;
                float _Wrap;
                float _CapTopLocal;
                float _CapUnderLocal;
                float _CapRadius;
                float _RockSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float fogFactor   : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.color = input.color;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // Two-dimensional value noise, the bed's grain, in [0, 1].
            float Value2(float2 q)
            {
                float2 cell = floor(q);
                float2 f = q - cell;
                f = f * f * (3.0 - 2.0 * f);

                float a = EvoHash1(cell);
                float b = EvoHash1(cell + float2(1.0, 0.0));
                float c = EvoHash1(cell + float2(0.0, 1.0));
                float d = EvoHash1(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // A rock's grain on one plane: two octaves of lumps and a crease where a slow field
            // crosses its middle, which reads as a fracture line at a distance.
            float RockHeight(float2 p)
            {
                float lumps = 0.65 * Value2(p) + 0.35 * Value2(p * 2.4 + 7.1);
                float crease = 1.0 - saturate(abs(Value2(p * 0.37 + 3.3) - 0.5) * 9.0);

                return lumps - 0.25 * crease;
            }

            float3 RockNormal(float2 p)
            {
                const float e = 0.03;

                float h = RockHeight(p);
                float dx = RockHeight(p + float2(e, 0.0)) - h;
                float dy = RockHeight(p + float2(0.0, e)) - h;

                return normalize(float3(-dx / e * _GrainStrength, -dy / e * _GrainStrength, 6.0));
            }

            float Wrapped(float3 n, float3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 geometric = normalize(input.normalWS);
                float3 seed = EvoCarveOffset(_RockSeed);
                float3 local = input.positionOS + seed;

                // The grain: the bed's triplanar, whiteout blended, in the reef's frame.
                float3 blend = pow(abs(geometric), 4.0);
                blend /= max(1e-4, blend.x + blend.y + blend.z);

                float3 p = local / max(0.02, _GrainMetres);

                float3 nx = RockNormal(p.zy);
                float3 ny = RockNormal(p.xz);
                float3 nz = RockNormal(p.xy);

                float3 detail =
                    blend.x * float3(nx.z * sign(geometric.x), nx.y, nx.x) +
                    blend.y * float3(ny.x, ny.z * sign(geometric.y), ny.y) +
                    blend.z * float3(nz.x, nz.y, nz.z * sign(geometric.z));

                // The detail comes back around each projection's own axis; carry the geometric
                // normal's departure from that axis so the cut's big shapes still shade.
                float3 axis = blend.x * float3(sign(geometric.x), 0, 0)
                            + blend.y * float3(0, sign(geometric.y), 0)
                            + blend.z * float3(0, 0, sign(geometric.z));
                float3 n = normalize(detail + (geometric - axis));

                // The tone. Green is the analytic surface's upward component (1 on the table),
                // blue is 1 on the cap, red how deep the mesh was cut there.
                float cavity = input.color.r;
                float table = saturate(input.color.g * 2.0 - 1.0);
                float onCap = input.color.b;

                float y = input.positionOS.y;
                float thickness = max(0.05, _CapTopLocal - _CapUnderLocal);
                float height = saturate((y - _CapUnderLocal) / thickness);

                float3 g1, g2;
                float m1 = EvoValueNoise(local / max(0.1, _MottleMetres), g1);
                float m2 = EvoValueNoise(local / max(0.1, 0.33 * _MottleMetres) + 11.0, g2);
                float mottle = 0.65 * m1 + 0.35 * m2;

                // Down the rim from the table to the underside, cold dark to rock grey.
                float3 rock = lerp(_RockDeep.rgb, _RockMid.rgb, onCap * smoothstep(0.0, 1.0, height));

                // The crust: on what faces up on the cap's top, in patches, thinned into the cuts.
                float crust = onCap * saturate(n.y) * smoothstep(0.35, 0.95, height) * (0.35 + 0.65 * table);
                crust *= smoothstep(0.30, 0.62, mottle) * (1.0 - 0.6 * cavity);
                rock = lerp(rock, _RockCrust.rgb, saturate(crust));

                rock *= lerp(1.0 - _MottleStrength * 0.5, 1.0 + _MottleStrength * 0.5, m2);

                // A speckle under the patches, the grain of the stone itself, finer than the mesh.
                float3 g3;
                float speck = EvoValueNoise(local / 0.07, g3);
                rock *= 0.8 + 0.4 * speck;
                rock *= 1.0 - _CavityDark * cavity * cavity;

                // The cap's shadow: the room under it keeps a little of the main light, and the
                // stem comes back into it as the sun's slant clears the rim far enough down.
                float below = _CapUnderLocal - y;
                float reach = max(1.0, _CapRadius / 0.3);
                float underCap = saturate(below / 0.3 + 1.0) * (1.0 - smoothstep(0.6 * reach, reach, below));
                float facesDown = saturate(-geometric.y);
                float shade = lerp(1.0, _ShadeUnder, saturate(max(underCap * (1.0 - onCap * table), onCap * facesDown)));

                Light main = GetMainLight();
                float3 lit = rock * main.color * Wrapped(n, main.direction) * shade;

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    lit += rock * extra.color * extra.distanceAttenuation * Wrapped(n, extra.direction) * shade;
                }
                #endif

                lit += rock * SampleSH(n) * lerp(1.0, 0.55, underCap * (1.0 - onCap * table));

                // The net, on what the sky can see: the table and the rim's shoulder, not the
                // room under the cap.
                float fade = EvoCausticFade(input.positionWS, _CausticReach) * saturate(n.y) * (1.0 - underCap);

                if (fade > 0.001)
                {
                    float net = EvoCausticNet(input.positionWS, _Time.y);

                    lit += _CausticColor.rgb * (net * fade * _CausticStrength) * (0.25 + 0.75 * rock / max(1e-4, _RockCrust.rgb));
                }

                lit = EvoMixFog(lit, input.fogFactor, input.positionWS);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
