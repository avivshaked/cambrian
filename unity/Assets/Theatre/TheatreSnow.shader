// Marine snow: the motes of detritus drifting down through the water.
//
// One particle system for the whole box, not one per body: the note's furniture list asks for a
// few thousand faint motes over the volume [U5], and anything per creature would be a second
// population to keep in step with the real one.
//
// Unlit and additive on purpose. A mote is a speck of light scattered off a falling particle,
// which is what dark field photographs of plankton are full of; lighting it would make it a
// lump. Additive also means a mote can never hide a body behind it, which matters because a
// picture that hides a body is a picture that lies about the census.
//
// Fogged like everything else, so the snow recedes with the water rather than hanging in front
// of a wall it is behind.

Shader "Evosim/Theatre Snow"
{
    Properties
    {
        // Alpha carries the strength: additive blending multiplies by it, so this is how much a
        // mote adds to the water behind it. The first cut used 0.10 and nothing could be seen; a
        // mote is two or three pixels across in a world view and a tenth of a dim blue is below
        // what a picture can hold.
        _TintColor("Mote colour", Color) = (0.62, 0.74, 0.78, 0.30)
        _SoftEdge("Edge softness", Range(0.01, 1)) = 0.55

        // The water the snow is allowed to be in, set by TheatreSkin from the run's own box.
        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _SoftEdge;
                float4 _BoxMin;
                float4 _BoxMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 colour     : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 colour     : TEXCOORD1;
                float fogFactor   : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.colour = input.colour;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Clipped to the water, in the fragment and not left to the emitter.
                //
                // The shape module confines the emission and the first pictures showed it doing
                // so along the box's length and its depth, and not across its width: the top view
                // came out speckled from edge to edge of a frame twice as wide as the five metre
                // box, while the side and the corner views, which look along that axis, were
                // clean. An orthographic camera draws a mote the same size however far away it
                // is, so the one view that could see the leak was the one that made it obvious.
                // Rather than argue with the emitter, the water itself is the bound: a mote
                // outside the box the run was simulated in is not drawn, and no picture can show
                // snow in water that does not exist.
                if (any(input.positionWS < _BoxMin.xyz) || any(input.positionWS > _BoxMax.xyz))
                {
                    discard;
                }

                // A round mote from the quad's own coordinates, so there is no texture to ship.
                float2 d = input.uv * 2.0 - 1.0;
                float r = saturate(1.0 - dot(d, d));

                float a = pow(r, 1.0 / max(0.01, _SoftEdge));

                float4 tint = _TintColor * input.colour;
                float alpha = a * tint.a;

                // Faded towards nothing rather than towards the fog colour: an additive mote adds
                // nothing once it is far away, and adding the fog colour would build a bright haze
                // out of the furthest and least interesting particles. Guarded on the keyword,
                // because ComputeFogFactor returns zero when there is no fog at all and an
                // unguarded multiply would then delete the snow instead of fading it.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                alpha *= saturate(input.fogFactor);
                #endif

                return half4(tint.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
