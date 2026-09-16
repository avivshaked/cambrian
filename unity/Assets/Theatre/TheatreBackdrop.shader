// The water behind everything: what a viewer sees where there is nothing to see.
//
// Why there is one. Until 2026-09-16 the camera cleared to one flat colour, the near black blue
// the fog carries everything towards, so the top of the water and the bottom of the world were
// the same black behind a body and a picture had no up in it. The look's design pass ruled the
// lit top water (logbook/specs/striking-theatre-menu.md, R1): the water is brightest at the
// waterline and falls to the deep by e-folds of the reach the surface light has, and the fog
// every other shader mixes towards is that colour at the height it is looking through
// (TheatreWater.hlsl, EvoWaterColour). This skybox paints the same column for the background,
// by the direction of the look: a ray tilted upward ends in brighter water and one tilted down
// in darker, from the eye's own height, so that the backdrop and the fog on a body agree.
//
// The eye's height is clamped at the waterline: the world has no top (CLAUDE.md's gotcha) and a
// camera above the surface looking down sees the column from its top, not brighter water that
// is not there.
//
// It is atmosphere, not census: nothing read off a picture depends on it, and
// EVOSIM_THEATRE_LIT_WATER=0 flattens it to the deep colour, which is the old background.
Shader "Evosim/Theatre Backdrop"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Backdrop"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float eyeY = min(GetCameraPositionWS().y, EvoWaterlineY());
                float3 colour = EvoWaterColour(eyeY + direction.y * EvoWaterViewMetres());
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
