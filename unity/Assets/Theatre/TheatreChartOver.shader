// A story's chart laid over the safari's frame (SafariChartLayer.cs, 2026-09-25): the chart's own
// panel texture composited into the camera's supersampled target before the box filter, so the
// chart is filtered with the world and the label and caption stamped after the read-back stay on
// top of it.
//
// Three layers in one pass, bottom to top: a full chart's darkening of the whole world
// (_WorldDim), the card's plate (a rounded rectangle, _Plate in UV from the bottom left,
// _PlateAlpha of the world taken away and replaced by _PlateColour), and the panel's ink. The
// panel draws ink only, over a transparent clear, so its colour arrives premultiplied by its
// coverage; the plate and the dim are closed forms here because what UI Toolkit writes into the
// alpha of a transparent target has never been measured in this project. With
// Blend One OneMinusSrcAlpha the three compose as
//
//     out = ink.rgb k + (1 - a_ink) a_plate plate + (1 - a_dim)(1 - a_plate)(1 - a_ink) world
//
// every term scaled by the fade k. The target is sRGB in a linear project, so the blend is done
// in linear light and the result encoded on write; the panel's texture is sRGB too and is read
// back as light.
//
// The plate's edge is antialiased over one pixel of the target, which the box filter halves.
Shader "Hidden/Evosim/Theatre Chart Over"
{
    Properties
    {
        _MainTex ("Chart panel", 2D) = "black" {}
        _Fade ("Opacity", Float) = 1
        _WorldDim ("Whole-picture dim (full chart)", Float) = 0
        _Plate ("Plate, UV from bottom left (xmin, ymin, xmax, ymax)", Vector) = (0, 0, 0, 0)
        _PlateAlpha ("Plate opacity", Float) = 0.78
        _PlateRadius ("Plate corner radius, target pixels", Float) = 28
        _PlateColour ("Plate colour", Color) = (0.02, 0.035, 0.043, 1)
        _TargetSize ("Target size, pixels", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Fade;
            float _WorldDim;
            float4 _Plate;
            float _PlateAlpha;
            float _PlateRadius;
            float4 _PlateColour;
            float4 _TargetSize;

            float4 Fragment(v2f_img input) : SV_Target
            {
                float k = saturate(_Fade);
                float4 ink = tex2D(_MainTex, input.uv);

                // The plate: a rounded rectangle's signed distance in target pixels.
                float2 pixel = input.uv * _TargetSize.xy;
                float4 box = _Plate * _TargetSize.xyxy;
                float radius = max(_PlateRadius, 0.0);
                float2 centre = 0.5 * (box.xy + box.zw);
                float2 halfSize = max(0.5 * (box.zw - box.xy) - radius, 0.0);
                float2 q = abs(pixel - centre) - halfSize;
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float inside = (box.z > box.x && box.w > box.y) ? saturate(0.5 - distance) : 0.0;

                float plate = inside * saturate(_PlateAlpha) * k;
                float dim = saturate(_WorldDim) * k;
                float coverage = saturate(ink.a) * k;

                float3 colour = ink.rgb * k + (1.0 - coverage) * plate * _PlateColour.rgb;
                float alpha = 1.0 - (1.0 - dim) * (1.0 - plate) * (1.0 - coverage);
                return float4(colour, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
