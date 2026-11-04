// The theatre's lens: depth of field on the bodies, the bed, the reef, the earth, the backdrop, the
// surface and the shafts, drawn by TheatreLens.cs after the transparent objects. The surface and the
// shafts write no depth and are taken at the depth behind them. The snow shells are drawn after the
// lens and take the same lens themselves (TheatreSnow.hlsl's _EvoSnowLens), so a mote is blurred by
// its own depth and not by the water's behind it.
//
// Why not URP's own Bokeh: it caps the blur at 14 pixels of the render target, so the film's
// 3840x2160 target holds a lens to a seventh of the blur it would have at 1080 lines; its Gaussian
// is a blur that starts at a distance, not a lens. HDRP's physical camera showed what a lens adds
// (D202's second test); D208 brings it over.
//
// The blur is a thin lens's circle of confusion: a point at depth z, with the lens focused at s,
// spreads over a disc whose radius on the frame is K0 |z - s| / (s z) pixels, K0 being the
// aperture's diameter times the frame's height over four times the tangent of half the field of
// view. Capped, in pixels of a 1080-line frame scaled to the target, as the snow is.
//
// Three passes. Prefilter: the colour at half size, with the nearest depth of each 2x2. Gather:
// a spiral of samples out to the largest blur, each counted where its own disc reaches the pixel,
// and a farther sample's disc held to twice the pixel's own so the background never spills over a
// sharp subject (Dennis Gustafsson's single-pass bokeh, 2018, the agent's adaptation). Composite:
// the sharp frame where the disc is under a pixel or two, the gathered one elsewhere and wherever a
// blurred foreground reaches over the pixel.

Shader "Evosim/Theatre Lens"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // x: the focus, m along the view axis (0: the depth at the frame's centre, as an autofocus
        // takes it); y: K0, pixels of the full-size target; z: the largest blur, pixels of radius of
        // the full-size target; w: the farthest the autofocus looks, m.
        float4 _LensParams;

        // xy: the full-size target's pixel size in uv; zw: its width and height in pixels.
        float4 _LensTarget;

        TEXTURE2D_X_FLOAT(_LensDepth);
        TEXTURE2D_X(_LensBlur);

        float LensEyeDepth(float2 uv)
        {
            float raw = SAMPLE_TEXTURE2D_X_LOD(_LensDepth, sampler_PointClamp, uv, 0).r;
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        float LensFocus()
        {
            if (_LensParams.x > 0.0) return _LensParams.x;
            return clamp(LensEyeDepth(float2(0.5, 0.5)), 0.1, max(0.1, _LensParams.w));
        }

        // The circle of confusion's radius at an eye depth, pixels of the full-size target.
        float LensCoc(float z, float focus)
        {
            float coc = _LensParams.y * abs(z - focus) / (focus * max(z, 1e-3));
            return min(coc, _LensParams.z);
        }
        ENDHLSL

        Pass
        {
            Name "Prefilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5

            // Half size: the bilinear tap at a half-size pixel's centre is the mean of the 2x2 under
            // it; the depth is the nearest of the four, so a foreground's edge is never pushed back.
            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 colour = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;

                float2 h = 0.5 * _LensTarget.xy;
                float z = LensEyeDepth(uv + float2(-h.x, -h.y));
                z = min(z, LensEyeDepth(uv + float2(h.x, -h.y)));
                z = min(z, LensEyeDepth(uv + float2(-h.x, h.y)));
                z = min(z, LensEyeDepth(uv + float2(h.x, h.y)));
                return float4(colour, z);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Gather"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 4.5

            #define GOLDEN_ANGLE 2.39996323
            #define RAD_SCALE 0.75
            #define MAX_SAMPLES 1024

            // At half size: every radius here is in half-size pixels. rgb: the blurred colour; a: how
            // much of the pixel a blurred foreground covers, for the composite.
            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = 2.0 * _LensTarget.xy;

                float focus = LensFocus();
                float4 centre = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float centreSize = 0.5 * LensCoc(centre.a, focus);
                float largest = 0.5 * _LensParams.z;

                float3 colour = centre.rgb;
                float total = 1.0;
                float near = 0.0;
                float radius = RAD_SCALE;
                float angle = 0.0;

                [loop]
                for (int i = 0; i < MAX_SAMPLES && radius < largest; i++)
                {
                    float2 at = uv + float2(cos(angle), sin(angle)) * texel * radius;
                    float4 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, at, 0);
                    float size = 0.5 * LensCoc(s.a, focus);
                    bool behind = s.a > centre.a;
                    if (behind) size = clamp(size, 0.0, 2.0 * centreSize);

                    float m = smoothstep(radius - 0.5, radius + 0.5, size);
                    colour += lerp(colour / total, s.rgb, m);
                    total += 1.0;
                    if (!behind) near = max(near, m * saturate(size - 0.5));

                    radius += RAD_SCALE / radius;
                    angle += GOLDEN_ANGLE;
                }

                return float4(colour / total, near);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma target 3.5

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 sharp = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float4 blur = SAMPLE_TEXTURE2D_X_LOD(_LensBlur, sampler_LinearClamp, uv, 0);

                float coc = LensCoc(LensEyeDepth(uv), LensFocus());
                float w = max(smoothstep(1.0, 2.5, coc), saturate(blur.a));
                return float4(lerp(sharp.rgb, blur.rgb, w), sharp.a);
            }
            ENDHLSL
        }
    }
}
