// A supersampled picture box-filtered down on the card: each output pixel the mean of its block
// of rendered pixels, taken over the stored bytes, which is SnapshotCamera.BoxDown's arithmetic
// on the CPU to the bit (2026-09-24).
//
// Why a pass of its own and not a bilinear blit. The picture is rendered into an sRGB target (the
// project has been in linear colour space since 2026-09-16, logbook/0104), and the hardware turns
// an sRGB texel into linear light before it filters one. A bilinear blit would therefore average
// light and not bytes: a block of 200 and 10 comes out 146 where the CPU's mean of the stored
// bytes was 105, and every edge in every frame would brighten. So this pass reads each texel with
// Load, encodes it back to the byte the target stored, sums the bytes as integers and divides as
// integers, as BoxDown does, and writes into a linear target so the byte is stored as it is. On a
// target that is not sRGB (_Encode 0) the texel already is the byte.
//
// The block is found from the pixel being written (SV_POSITION) and not from a UV, so the pass is
// the same whichever way up the platform stores a render texture: output row r reads rendered
// rows 2r and 2r + 1 of the same memory, which is the pairing BoxDown makes on the read-back.
Shader "Hidden/Evosim/Theatre Box Down"
{
    Properties
    {
        _MainTex ("Supersampled picture", 2D) = "black" {}
        _Factor ("Rendered pixels per output pixel on each axis", Float) = 2
        _Encode ("1 when the picture is stored sRGB", Float) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #pragma target 4.5

            #include "UnityCG.cginc"

            Texture2D _MainTex;
            float _Factor;
            float _Encode;

            float4 Fragment(v2f_img input) : SV_Target
            {
                int factor = clamp((int)(_Factor + 0.5), 1, 4);
                int2 origin = int2(input.pos.xy) * factor;
                uint3 sum = uint3(0, 0, 0);

                for (int dy = 0; dy < factor; dy++)
                {
                    for (int dx = 0; dx < factor; dx++)
                    {
                        float3 c = _MainTex.Load(int3(origin + int2(dx, dy), 0)).rgb;

                        if (_Encode > 0.5)
                        {
                            c = float3(
                                LinearToGammaSpaceExact(c.r),
                                LinearToGammaSpaceExact(c.g),
                                LinearToGammaSpaceExact(c.b));
                        }

                        sum += (uint3)round(saturate(c) * 255.0);
                    }
                }

                // Integer division, which is a floor, as the CPU's is; a float division here
                // would be a reciprocal on most cards and could land a hair under a whole number.
                uint3 mean = sum / (uint)(factor * factor);
                return float4(float3(mean) / 255.0, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
