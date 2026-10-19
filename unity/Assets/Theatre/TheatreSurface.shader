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
                positionWS.y += EvoRipple(positionWS.xz, EvoTime(), slope, curvature);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // ---------------------------------------------------------------------------------
            // The sky from below, the underwater look's item 6 (logbook/specs/underwater-look/
            // brief.md), drawn in place of the window above when EVOSIM_THEATRE_SKY_DAY is on.
            // Three things the plain window lacks, and nothing the run does not imply:
            //   - an overcast from the run's day (TheatreDaylight: more cloud and a softer, dimmer
            //     disc on a dull day, a clear sky on a bright one), drifting with the shaders'
            //     clock, never a random draw;
            //   - a slight colour split at the window's rim, because water bends blue a little
            //     more than red and the edge of the window is where that shows;
            //   - a mirror outside the window that brightens toward it, where the surface reflects
            //     the lit water just under it at a grazing angle.
            // The sun stays where item 1 or the key put it: the run's day factor never reaches
            // zero, so no sun sets and none is drawn low.

            // How much of the light arriving at the surface from below is reflected rather than let
            // through, for one channel's index: one outside the window, Schlick on the angle in
            // the air inside it, as the plain window does for all three at once.
            float WindowReflects(float3 incident, float3 n, float index)
            {
                float3 through = refract(incident, -n, index);
                if (dot(through, through) <= 1e-5) return 1.0;

                float cosAir = saturate(dot(normalize(through), n));
                return saturate(0.02 + 0.98 * pow(1.0 - cosAir, 5.0));
            }

            // A soft cloud field over the sky, 0 to 1, from three octaves of the carve's value
            // noise laid on the sky's own plane and drifting with the clock.
            float Clouds(float3 through, float time)
            {
                float2 plane = through.xz / max(0.08, through.y) * 0.35 + time * float2(0.004, 0.0015);
                float3 gradient;

                float field = 0.55 * EvoValueNoise(float3(plane, 0.37), gradient);
                field += 0.30 * EvoValueNoise(float3(plane * 2.3, 1.91), gradient);
                field += 0.15 * EvoValueNoise(float3(plane * 5.1, 4.13), gradient);

                return field;
            }

            float3 SkyDay(float3 incident, float3 n)
            {
                float overcast = saturate(_EvoDaylight.w);
                float day = EvoDayGain();

                // Water's index runs from about 1.331 in the red to 1.338 in the blue.
                float3 mirrored = float3(
                    WindowReflects(incident, n, 1.331),
                    WindowReflects(incident, n, 1.333),
                    WindowReflects(incident, n, 1.338));

                float3 sky = _SkyHorizon.rgb;
                float3 through = refract(incident, -n, 1.333);

                if (dot(through, through) > 1e-5)
                {
                    through = normalize(through);
                    sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(through.y));

                    // Cloud: patches on a bright day, a grey lid on a dull one. The cloud is the
                    // sky's own brightness made grey and dimmed, never a fixed white: the first
                    // stills (2026-10-07) used a literal near-white, which in this linear project
                    // is brighter than any of the sky, and a winter window came out blown.
                    float field = Clouds(through, EvoTime());
                    float cover = smoothstep(0.72 - 0.45 * overcast, 0.90 - 0.40 * overcast, field);
                    cover = max(cover, 0.85 * overcast * overcast);

                    float luma = dot(sky, float3(0.2126, 0.7152, 0.0722));
                    float3 cloud = luma * float3(0.94, 0.97, 1.0) * lerp(1.05, 0.55, overcast);
                    sky = lerp(sky, cloud, cover);

                    // A dull day is a darker sky as well as a greyer one.
                    sky *= lerp(1.0, 0.7, overcast);

                    // The sun: a smaller share of it through the cloud, its disc wider and its edge
                    // softer on a dull day; its glow wider and weaker, so the overcast spreads a
                    // little light about the sun's place and never whitens the window.
                    float3 sun = EvoSunDirectionWS();
                    float angle = acos(clamp(dot(through, sun), -1.0, 1.0));

                    float discRadians = radians(_SunDegrees * (1.0 + 2.5 * overcast));
                    float disc = 1.0 - smoothstep(discRadians * lerp(0.75, 0.2, overcast), discRadians * 1.25, angle);
                    float glow = pow(saturate(1.0 - angle / max(0.01, radians(_GlowDegrees * (1.0 + overcast)))), 3.0);

                    float seen = lerp(1.0, 0.06, overcast) * (1.0 - 0.6 * cover);
                    sky += _SunColor.rgb * (_SunStrength * (disc * seen + 0.10 * lerp(1.0, 0.3, overcast) * glow));
                }

                // The mirror: the dark water below, as before, brightening toward the window's rim,
                // where the reflected ray runs along under the surface through the lit water.
                float3 bounced = reflect(incident, n);
                float3 mirror = _DeepColor.rgb * (0.55 + 0.45 * saturate(-bounced.y));

                float cosIn = saturate(dot(incident, n));
                float sinIn = sqrt(saturate(1.0 - cosIn * cosIn));
                float beyond = max(0.0, (sinIn - 0.7502) / 0.2498);
                float nearRim = exp(-5.0 * beyond);

                mirror = lerp(mirror, EvoWaterColour(EvoWaterlineY()) * 1.6, 0.55 * nearRim);

                // The second batch's item 1: outside the window the surface mirrors the open water
                // below, as an eye would see it looking along the bounced ray (EvoBackdrop, which
                // carries the day, divided out here because the return multiplies by it), with the
                // lit water at the rim in a narrower band. The first batch's mirror was the lit
                // shallow water over most of its width, which greyed the whole sky view.
                if (EvoLookFade())
                {
                    // No phase term: the glow toward the sun is the water's look at the light,
                    // and the mirror looks down into the deep, away from it (with the phase on,
                    // the reflected glow drew a bright oval in the mirror, iteration 3).
                    float eyeY = min(GetCameraPositionWS().y, EvoWaterlineY());
                    float3 below = EvoWaterColour(eyeY + bounced.y * EvoWaterViewMetres()) * max(0.0, _EvoFade.z);
                    mirror = lerp(below, EvoWaterColour(EvoWaterlineY()) * 1.3, 0.45 * nearRim * nearRim * nearRim);
                }

                return lerp(sky * day, mirror * day, mirrored);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) discard;

                float3 eye = GetCameraPositionWS();

                // The normal is taken per pixel from the same field the vertex moved by, at the
                // same time: a normal interpolated from a grid this coarse would step, and the
                // window's rim is exactly where a stepped normal shows.
                float3 n = EvoRippleNormal(input.positionWS.xz, EvoTime());

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

                if (EvoLookSky()) colour = SkyDay(incident, n);

                // Deeper down the ceiling is a dim patch rather than a bright one. Over twice the
                // light's reach, which is the same dial the shafts and the caustics fade on, so
                // one number sets how deep the lit part of this world is.
                float below = max(0.0, _Waterline - eye.y);
                float reach = max(1.0, 2.0 * EvoLightReachMetres());
                float depthFade = lerp(1.0, _DeepFade, saturate(below / reach));

                colour *= _SurfaceBrightness * depthFade;
                colour = EvoMixFog(colour, input.fogFactor, input.positionWS);

                return half4(colour, inside);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
