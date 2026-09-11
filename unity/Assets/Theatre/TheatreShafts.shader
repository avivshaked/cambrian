// Shafts of sunlight coming down through the water.
//
// Why they are geometry. URP has no volumetric fog, so the two ways to do this are a custom
// renderer feature that marches the light [CQ] or additive quads that stand in for the beams
// [CY3]. The fourth day asks for the second in as many words, and it is the right trade here:
// the theatre already draws a couple of thousand bodies a frame, and a beam is one of the few
// things in a picture that a viewer reads as atmosphere rather than as an object, so it does not
// have to survive being looked at closely. A handful of quads, no pass, no buffer
// (logbook/specs/skin-spec-4.md).
//
// What makes them believable is not the geometry, it is that they agree with the ceiling. Each
// slab knows the point on the surface it leaves from, carried in a second UV channel, and reads
// the same wave field there that the surface plane is drawn by and the caustics are focused by
// (TheatreWater.hlsl, EvoShaftGain). So a shaft brightens when the water above it curves in
// towards the sun, and the beam, the window and the net on a body below it all move together.
//
// Three ways one of these is faded, and each of them is a way a flat card gives itself away:
//   - across its width, so it has no edges;
//   - with depth, on the same curve the caustics use, so the lit part of the world is one depth;
//   - and by how squarely it is being looked at, because a sheet of light seen edge on has no
//     area and should not be there at all.
//
// Additive, and it never writes depth, so a shaft cannot hide a body: a picture that hides a body
// is a picture that lies about the census (the same rule the marine snow is drawn under).

Shader "Evosim/Theatre Shafts"
{
    Properties
    {
        // Cool and pale. A beam is the water lit, not the sun itself, and by the time the light
        // is a few metres down the red is gone out of it (DESIGN's light model is a single band,
        // so this is a look rather than a reading).
        _ShaftColor("Shaft colour", Color) = (0.48, 0.74, 0.92, 1)
        _ShaftStrength("Shaft strength", Range(0, 4)) = 0.55

        // Where the water stops, set by TheatreSkin from the run's own box. A beam is water lit
        // from above, so there is none of it over the waterline.
        _Waterline("The waterline, world y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Shafts"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShaftColor;
                float _ShaftStrength;
                float _Waterline;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;

                // Where this slab leaves the surface, and how bright it is: xy is the world x and
                // z of its top, z is the draw that made it (TheatreSkin.BuildShafts), w is spare.
                float4 shaft      : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 shaft      : TEXCOORD3;
                float fogFactor   : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = input.uv;
                output.shaft = input.shaft;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) discard;

                // Across the slab: zero at both edges, so the beam has no sides. Sharpened a
                // little past a plain half wave, which otherwise reads as a fat soft stripe.
                float across = pow(sin(3.14159265 * saturate(input.uv.x)), 1.6);

                // Along it: bright where it enters the water and gone by the end of the quad,
                // squared so most of the beam is in its top half.
                float along = pow(saturate(1.0 - input.uv.y), 1.5);

                // And in absolute depth, on the caustics' own curve, so a shaft and the net it
                // throws on a body stop at the same place.
                float depth = EvoCausticFade(input.positionWS, EvoLightReachMetres());

                // Edge on, a sheet has no area. Without this the slabs read as cards turning with
                // the camera, which is the one tell that cannot be argued with.
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float square = pow(saturate(abs(dot(input.normalWS, view))), 0.8);

                // The surface above, at this shaft's own top: the beam breathes with the wave.
                float gain = EvoShaftGain(input.shaft.xy, _Time.y);

                float alpha = _ShaftStrength * input.shaft.z * gain *
                              across * along * depth * square * inside;

                // Faded towards nothing rather than towards the fog colour, for the marine snow's
                // reason: an additive thing adds nothing once it is far away, and adding the fog
                // colour would build a haze out of the furthest beams. Guarded on the keyword,
                // because ComputeFogFactor returns zero with no fog at all and would delete the
                // shafts instead of fading them.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                alpha *= saturate(input.fogFactor);
                #endif

                return half4(_ShaftColor.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
