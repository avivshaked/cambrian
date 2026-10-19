// The tone step on HDRP, for the spike (D202): TheatreToneStep.shader's curve, word for word, read
// from HdrpToneStep's copy of the frame after HDRP's post-processing. The frame there is linear and
// already tonemapped, as URP's is where its step runs.

Shader "Hidden/Evosim/Theatre Tone Step HDRP"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 vulkan metal

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

    TEXTURE2D_X(_ToneSource);

    // x: the black point; y: the S's toe, its power under the pivot; z: the pivot; w: the S's
    // shoulder, its power over the pivot. All in display-encoded luma.
    float4 _ToneCurve;

    float Curve(float y)
    {
        float black = clamp(_ToneCurve.x, 0.0, 0.5);
        float power = max(1.0, _ToneCurve.y);
        float shoulder = max(1.0, _ToneCurve.w);
        float pivot = clamp(_ToneCurve.z, 0.05, 0.95);

        float t = saturate((y - black) / max(1e-4, 1.0 - black));

        float toe = pivot * pow(max(0.0, t) / pivot, power);
        float top = 1.0 - (1.0 - pivot) * pow(max(0.0, 1.0 - t) / (1.0 - pivot), shoulder);
        float window = 0.5 * min(pivot, 1.0 - pivot);

        return lerp(toe, top, smoothstep(pivot - window, pivot + window, t));
    }

    float4 ToneStep(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float4 source = LOAD_TEXTURE2D_X(_ToneSource, varyings.positionCS.xy);
        float3 linearColour = max(0.0, source.rgb);
        float3 encoded = LinearToSRGB(linearColour);

        float luma = dot(encoded, float3(0.2126, 0.7152, 0.0722));
        float ratio = Curve(luma) / max(1e-5, luma);

        float most = max(encoded.r, max(encoded.g, encoded.b));
        if (ratio > 1.0) ratio = min(ratio, max(1.0, 1.0 / max(1e-5, most)));

        return float4(SRGBToLinear(encoded * ratio), source.a);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Theatre Tone Step"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment ToneStep
            ENDHLSL
        }
    }
    Fallback Off
}
