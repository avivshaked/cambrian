// The tone step: the underwater look's second batch, item 2 (logbook/specs/underwater-look/brief-2.md),
// drawn by TheatreToneStep.cs after the post-processing.
//
// One curve on the luma of the display-encoded colour: a black point, then an S made of two power
// curves blended across the pivot, so the curve has no kink. The colour is then
// scaled by the ratio of the new luma to the old, which keeps a pixel's hue and the proportions of
// its channels; and a ratio that would push a channel past white is held at white, so the curve
// can never clip a highlight it brightens. Nothing here is noisy or depends on the clock.

Shader "Evosim/Theatre Tone Step"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "ToneStep"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            // x: the black point; y: the S's toe, its power under the pivot; z: the pivot; w: the
            // S's shoulder, its power over the pivot. All in display-encoded luma.
            float4 _ToneCurve;

            float Curve(float y)
            {
                float black = clamp(_ToneCurve.x, 0.0, 0.5);
                float power = max(1.0, _ToneCurve.y);
                float shoulder = max(1.0, _ToneCurve.w);
                float pivot = clamp(_ToneCurve.z, 0.05, 0.95);

                float t = saturate((y - black) / max(1e-4, 1.0 - black));

                // Each half is defined across the whole range; they meet at the pivot in value but
                // not in slope (the toe's power against the shoulder's), so the two are blended over
                // a window either side of it, which leaves the curve with a continuous slope.
                float toe = pivot * pow(max(0.0, t) / pivot, power);
                float top = 1.0 - (1.0 - pivot) * pow(max(0.0, 1.0 - t) / (1.0 - pivot), shoulder);
                float window = 0.5 * min(pivot, 1.0 - pivot);

                return lerp(toe, top, smoothstep(pivot - window, pivot + window, t));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord.xy, 0);
                float3 linearColour = max(0.0, source.rgb);

                float3 encoded = LinearToSRGB(linearColour);
                float luma = dot(encoded, float3(0.2126, 0.7152, 0.0722));

                float ratio = Curve(luma) / max(1e-5, luma);

                float most = max(encoded.r, max(encoded.g, encoded.b));
                if (ratio > 1.0) ratio = min(ratio, max(1.0, 1.0 / max(1e-5, most)));

                float3 shaped = SRGBToLinear(encoded * ratio);

                return half4(shaped, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
