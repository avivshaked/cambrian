// The earth under a shaped bed, seen through the glass (the owner, 2026-09-24: "everything under
// the ground [is] full of earth, rather than more water").
//
// A ring wall just inside the glass from the bed's height at the rim down past the box's floor,
// built by TheatreSkin.BuildEarth: from outside, the cut face of the sediment the beach and the
// reef stand on. The bed's own shader drew it black. Its normal composition adds the geometric
// normal's departure from straight up, the small angle form for a bed whose slope is a fraction
// of a metre over five, and on a vertical face that turns the normal to point down, away from
// every light. This shader is the bed's lighting without that step, and a face of its own.
//
// What the face shows. Layers of sediment in a few tones, their boundaries warped by a slow
// noise so no layer is a ruled line, a speckle of grain, a band of sand under the bed's surface
// that ties the face to the beach above it, and a darkening with depth under the top, since the
// light that reaches the sand comes down through the water. Browner than the bed, which gives its
// colour up to the guilds: nothing lives in the earth, so it may carry a colour of its own.
//
// Costs one noise and three hashes a pixel on a wall of 256 segments, drawn only in a tank whose
// bed has relief.

Shader "Evosim/Theatre Earth"
{
    Properties
    {
        _EarthDark("Earth, deep layer", Color) = (0.042, 0.035, 0.028, 1)
        _EarthLight("Earth, light layer", Color) = (0.120, 0.100, 0.080, 1)
        _Sand("Sand band under the bed", Color) = (0.085, 0.083, 0.078, 1)

        _StrataMetres("Metres per layer, on average", Range(0.1, 10)) = 1.1
        _SandMetres("Sand band, metres", Range(0, 3)) = 0.8
        _DarkenMetres("Metres over which the face darkens with depth", Range(1, 100)) = 25

        // A floor under the lighting, so a face turned from every light still reads as a face.
        _Ambient("Ambient floor", Range(0, 2)) = 0.35
        _Wrap("Diffuse wrap", Range(0, 1)) = 0.5
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

            // The mesh carries both faces, each with its own normal.
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EarthDark;
                float4 _EarthLight;
                float4 _Sand;
                float _StrataMetres;
                float _SandMetres;
                float _DarkenMetres;
                float _Ambient;
                float _Wrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0; // x: the bed's height over this column
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float top         : TEXCOORD2;
                float fogFactor   : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.top = input.uv.x;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            float Wrapped(float3 n, float3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float3 n = normalize(input.normalWS);
                float below = max(0.0, input.top - p.y);

                // The layers. The height is first stretched and squeezed by a slow noise of the
                // height alone, so the layers are of unequal thickness (its slope stays under
                // one, so the order of the layers is kept), then bent a little by a noise across
                // the face, so no boundary is a ruled line. Each layer takes a tone from a hash of
                // its index, kept off the darkest end so no boundary is drawn as a black line,
                // with a soft edge to the next, and the tone drifts along the layer, so a layer is
                // a band of sediment and not a stripe of paint.
                float3 unused;
                float stretch = EvoValueNoise(float3(3.1, p.y * 0.21, 7.7), unused);
                float bend = EvoValueNoise(float3(p.x * 0.11, p.y * 0.05, p.z * 0.11), unused);
                float y = p.y + 2.2 * stretch + 0.9 * (bend - 0.5);
                float layer = y / max(0.05, _StrataMetres);
                float index = floor(layer);
                float within = layer - index;
                float here = 0.3 + 0.7 * EvoHash1(float2(index, 17.0));
                float next = 0.3 + 0.7 * EvoHash1(float2(index + 1.0, 17.0));
                float tone = lerp(here, next, smoothstep(0.55, 1.0, within));
                float drift = EvoValueNoise(float3(p.x * 0.35, y * 0.9, p.z * 0.35), unused);
                tone = saturate(tone + 0.4 * (drift - 0.5));
                float warp = bend;

                // Grain: a speckle at about three centimetres, on the face's own two axes.
                float grain = EvoHash1(floor(p * 33.0));

                float3 albedo = lerp(_EarthDark.rgb, _EarthLight.rgb, tone);
                albedo *= 0.85 + 0.3 * grain;

                // The sand under the surface, its lower edge warped like the layers.
                float sandEdge = _SandMetres * (0.6 + 0.8 * warp);
                albedo = lerp(albedo, _Sand.rgb * (0.9 + 0.2 * grain), 1.0 - smoothstep(0.0, sandEdge, below));

                // Darker with depth under the top.
                albedo *= lerp(1.0, 0.55, saturate(below / _DarkenMetres));

                Light main = GetMainLight();
                float3 lit = albedo * main.color * Wrapped(n, main.direction);
                lit += albedo * SampleSH(n);
                lit += albedo * _Ambient;

                lit = EvoMixFog(lit, input.fogFactor, p);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
