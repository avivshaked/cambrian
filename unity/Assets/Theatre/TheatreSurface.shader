// The surface of the sea, seen from underneath.
//
// Why there is one at all. The owner ran round 36 seed 1 in the theatre and said they could not
// see the sun, the water shimmer or the underwater ripple. There was nothing to see: the water
// had a colour, a fog and marine snow, and the world's ceiling was the same empty background as
// its floor (logbook/specs/skin-spec-4.md). This is that ceiling: one quad at the waterline,
// rippling, with the sky refracted through it.
//
// What is drawn on it. Looking up from inside water, the whole sky is squeezed into a cone of
// about 97 degrees about the vertical, because a ray that leaves water at grazing incidence has
// to have arrived from the horizon and there is nowhere further to come from. Outside that cone
// the surface is a mirror of the dark water below. That is Snell's window, and it is the single
// most recognisable thing about being under a calm sea. It is not painted on: the shader takes
// the direction from the eye to the pixel, refracts it through the rippled normal with water's
// index of 1.333, and shows the sky where a ray comes through and the water where none does. So
// the ripple breaks the window's edge and wobbles the sun by itself, which is the shimmer, and
// nothing has to be animated by hand.
//
// The sun is the scene's own directional light, handed over by TheatreSkin as a global
// (TheatreWater.hlsl, _EvoSun), so the light a body is lit by and the disc in the ceiling are the
// same object. A disc drawn at a bearing of its own would be a second sun, and a viewer would
// read the shadows against it and find them wrong.
//
// It is a visual and nothing else. No body is moved by the wave, no current changes, no hash
// moves: the theatre lives outside simHash by construction, and this file is a ceiling.
//
// Hidden from above, three times over, because the fourth day's one hard requirement about the
// outside is that the snapshot's top, side, end and iso views are unchanged, and one culling flag
// is a thin thing to hang that on. The quad's front face points down, so Cull Back removes it for
// any camera above it. EvoSeenFromBelow removes it again for any eye above the waterline, which
// is where the top and iso cameras stand. And SnapshotCamera turns the renderer off outright for
// the four views that photograph the census, which is what covers the side and the end, whose
// cameras are under the water looking in from outside and would otherwise catch the quad edge on.
// The Play mode viewer keeps all of it, wherever they fly.

Shader "Evosim/Theatre Surface"
{
    Properties
    {
        [Header(The sky through the window)]
        // A cool pale sky. Not a blue one: the window is small, it is seen through metres of
        // water that has already taken the red out, and a saturated blue ceiling over a dark
        // field reads as a lid rather than as air.
        _SkyHorizon("Sky at the window's rim", Color) = (0.30, 0.45, 0.56, 1)
        _SkyZenith("Sky overhead", Color) = (0.58, 0.76, 0.92, 1)

        // The water the surface mirrors outside the window. TheatreSkin sets it to the same near
        // black blue the fog carries everything towards, so the mirror and the far water agree.
        _DeepColor("Water, mirrored", Color) = (0.012, 0.032, 0.048, 1)

        [Header(The sun)]
        _SunColor("Sun colour", Color) = (1.0, 0.95, 0.86, 1)
        _SunStrength("Sun strength", Range(0, 24)) = 7

        // The real sun is half a degree across and would be two pixels. This is a picture of a
        // sun rather than a measurement of one, and the disc is widened until it reads; the glow
        // around it is the haze a wet ceiling puts round any bright thing.
        _SunDegrees("Sun disc, degrees", Range(0.2, 20)) = 3.2
        _GlowDegrees("Sun glow, degrees", Range(1, 80)) = 24

        [Header(The surface)]
        _SurfaceBrightness("Overall brightness", Range(0, 4)) = 1.0

        // How far down the window is still bright. Not a fog (the fog already does distance):
        // this is the plain fact that a ceiling seen from forty metres under is a dim patch, and
        // it never reaches nothing, because a viewer must always be able to find which way is up.
        _DeepFade("Brightness left at the bottom of the world", Range(0, 1)) = 0.28

        // Where the water stops, set by TheatreSkin from the run's own box. Zero in every run
        // the campaign has recorded, and passed rather than assumed because a run is entitled to
        // put its surface somewhere else and a ceiling that ignored it would be drawn into rock.
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
            Name "Surface"
            Tags { "LightMode" = "UniversalForward" }

            // Drawn from below only. The mesh is wound so that its front face points down
            // (TheatreSkin.Grid), so this removes it for every camera above the water.
            Cull Back

            // No depth written, and drawn after the bodies. A body that reaches the waterline is
            // in front of the ceiling from below, and the depth test keeps it there; writing
            // depth would instead let the ceiling hide whatever was drawn after it.
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyHorizon;
                float4 _SkyZenith;
                float4 _DeepColor;
                float4 _SunColor;
                float _SunStrength;
                float _SunDegrees;
                float _GlowDegrees;
                float _SurfaceBrightness;
                float _DeepFade;
                float _Waterline;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor   : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // The wave, in the geometry as well as in the normal. A few centimetres is almost
                // nothing at this scale and the shimmer is nearly all in the per pixel normal
                // below, but a ceiling whose outline is dead straight gives itself away the
                // moment it is seen edge on, which from three metres under is most of the frame.
                float2 slope;
                float curvature;
                positionWS.y += EvoRipple(positionWS.xz, _Time.y, slope, curvature);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) discard;

                float3 eye = GetCameraPositionWS();

                // The normal is taken per pixel from the same field the vertex moved by, at the
                // same time: a normal interpolated from a grid this coarse would step, and the
                // window's rim is exactly where a stepped normal shows.
                float3 n = EvoRippleNormal(input.positionWS.xz, _Time.y);

                // The ray, followed the way the light came rather than the way the eye looks: it
                // leaves the eye, arrives here travelling upward, and carries on into the air.
                // Taken from URP's own view direction rather than from the camera's position, so
                // that an orthographic camera, whose rays are parallel and do not meet at its
                // position, gets the right answer as well.
                float3 incident = -normalize(GetWorldSpaceViewDir(input.positionWS));

                // refract() wants the normal on the side the ray comes from, which is underneath,
                // and the ratio of the index it is leaving to the index it is entering: water to
                // air, 1.333. It returns zero where there is no ray, which is total internal
                // reflection: outside the window the surface is a mirror, and that is where the
                // 97 degree cone comes from rather than from a number written down here.
                float3 through = refract(incident, -n, 1.333);
                float caught = dot(through, through);

                float3 sky = _SkyHorizon.rgb;
                float mirrored = 1.0;

                if (caught > 1e-5)
                {
                    through = normalize(through);

                    // The gradient of the sky, by how high the ray came from. At the window's rim
                    // the ray came along the horizon, in the middle it came from overhead.
                    sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(through.y));

                    // The sun, where the ray that got through points at it.
                    float3 sun = EvoSunDirectionWS();
                    float angle = acos(clamp(dot(through, sun), -1.0, 1.0));

                    float disc = 1.0 - smoothstep(
                        radians(_SunDegrees) * 0.75, radians(_SunDegrees) * 1.25, angle);

                    float glow = pow(saturate(1.0 - angle / max(0.01, radians(_GlowDegrees))), 3.0);

                    sky += _SunColor.rgb * (_SunStrength * (disc + 0.10 * glow));

                    // How much of what arrives here is reflected rather than transmitted, by
                    // Schlick on the angle in the AIR. Taking it on the angle in the water would
                    // leave the rim dark: reflectance has to reach one exactly at the critical
                    // angle, which is where the ray in the air is running along the horizon, and
                    // that is what draws the bright ring round the window.
                    float cosAir = saturate(dot(through, n));
                    mirrored = saturate(0.02 + 0.98 * pow(1.0 - cosAir, 5.0));
                }

                // The mirror: the dark water below, brighter where the reflected ray goes steeply
                // down into it because that is where there is most water to scatter back.
                float3 bounced = reflect(incident, n);
                float3 mirror = _DeepColor.rgb * (0.55 + 0.45 * saturate(-bounced.y));

                float3 colour = lerp(sky, mirror, mirrored);

                // Deeper down the ceiling is a dim patch rather than a bright one. Over twice the
                // light's reach, which is the same dial the shafts and the caustics fade on, so
                // one number sets how deep the lit part of this world is.
                float below = max(0.0, _Waterline - eye.y);
                float reach = max(1.0, 2.0 * EvoLightReachMetres());
                float depthFade = lerp(1.0, _DeepFade, saturate(below / reach));

                colour *= _SurfaceBrightness * depthFade;
                colour = MixFog(colour, input.fogFactor);

                return half4(colour, inside);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
