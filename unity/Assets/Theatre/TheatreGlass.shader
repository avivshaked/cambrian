// The tank's wall as glass: a faint sheet that is nearly nothing face on and brightens at a
// grazing angle, the way a pane does, in place of nothing at all between the 48 wire chords
// (the look's design pass, E3, 2026-09-16). The arrival shot of the safari needs a wall that can
// be seen through and seen; the census views never see it, because the sheet is marked as
// something only the inside of the water shows (TheatreInsideOnly) and the four framed views
// photograph from outside. Faded towards nothing with distance, for the shafts' reason: a
// transparent thing far away adds nothing, and adding the fog colour would build a haze.
Shader "Evosim/Theatre Glass"
{
    Properties
    {
        _GlassColor("Glass colour", Color) = (0.55, 0.80, 0.90, 1)
        // 0.015, 0.18 and 5 after the first picture at 0.03, 0.45 and 3.5 drew a bright tube
        // around the floor (2026-09-16): from inside, most of a wall is seen at a grazing angle.
        _GlassBase("Alpha face on", Range(0, 0.3)) = 0.015
        _GlassRim("Alpha at a grazing angle", Range(0, 1)) = 0.18
        _GlassPower("How fast the rim comes in", Range(1, 12)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor;
                float _GlassBase;
                float _GlassRim;
                float _GlassPower;
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
                float fogFactor   : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = 1.0 - saturate(abs(dot(normalize(input.normalWS), v)));

                float alpha = _GlassBase + _GlassRim * pow(facing, _GlassPower);

                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                alpha *= saturate(input.fogFactor);
                #endif

                return half4(_GlassColor.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
