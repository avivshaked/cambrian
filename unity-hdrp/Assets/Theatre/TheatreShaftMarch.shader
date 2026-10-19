// Shafts of sunlight through the volume of the water: the underwater look's item 4
// (logbook/specs/underwater-look/brief.md), in place of the nine additive quads of
// TheatreShafts.shader when EVOSIM_THEATRE_SHAFT_MARCH is on.
//
// Why marched. A quad is a beam standing somewhere, and the light under a real sea is not
// somewhere: it is the whole lit water, brighter where the surface above focused it. So along
// each view ray this samples the light the surface sends to each point (the caustic field the
// bed and the bodies are lit by, TheatreWater.hlsl's EvoCausticGain, read at the sample's depth
// along the refracted sun's ray), times the phase function, times the absorption on the way
// down to the sample and on the way back to the eye. A shaft is then a line of points the same
// patch of surface focused on, and it comes from the same waves as the caustics and moves with
// them. Since the first stills the beam itself is read where the sample's ray left the surface
// (EvoBeamAt, the surface's concavity there), and the focusing at the sample's depth brightens it:
// the focusing alone moved the light by a quarter either way in the top few metres and drew only
// faint streaks.
//
// Single scattering, one pass, no buffer and no renderer feature: drawn on the inside faces of
// a box over the lit water (TheatreVolume.hlsl says how that gives one fragment a pixel), added
// over whatever is there, and stopped at the depth buffer, so a body in front is never covered.
// Time-stable: the field moves with the shaders' clock and the start of each march is offset by
// a fixed per-pixel dither, never by a random draw per frame.

Shader "Evosim/Theatre Shaft March"
{
    Properties
    {
        // Cool and pale, as the quads were: the water lit, not the sun.
        _ShaftColor("Shaft colour", Color) = (0.62, 0.84, 0.96, 1)

        // Per metre of lit water. Set by TheatreSkin from TheatreLook.MarchStrength.
        _MarchStrength("Strength per metre", Range(0, 0.2)) = 0.03
        _MarchSteps("Steps along a ray", Range(8, 64)) = 48

        // A beam is where the surface above is concave toward the sun (EvoBeamAt), drawn at this
        // power so the water between beams adds next to nothing: the flat water's light is the
        // in-scatter's (item 2). The focusing at the sample's own depth (EvoCausticGain) then
        // brightens a beam where it converges, by this share.
        _MarchContrast("Beam contrast", Range(0.5, 6)) = 2.0
        _MarchFocus("Share of a beam's light the focusing sets", Range(0, 1)) = 0.5

        // The most the phase term may brighten a beam looking up it toward the sun, so the
        // shafts cannot whiten the window they are seen against.
        _MarchPhaseCap("Phase term's ceiling", Range(1, 45)) = 3

        // The farthest a march reaches from where it enters the water, metres.
        _MarchReach("Longest march, metres", Range(4, 200)) = 40

        // The water the light is allowed to be in, set by TheatreSkin from the run's box.
        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
        _Tank("Tank: centre x, centre z, radius, on", Vector) = (0, 0, 0, 0)
        _Waterline("The waterline, world y", Float) = 0

        // The second batch's item 3 (logbook/specs/underwater-look/brief-2.md), set by TheatreSkin
        // and read only while _EvoLook2.w is on: the share of the surface that sends a beam down,
        // the power a sheet is drawn at (higher is narrower), and what is left of the shafts in
        // front of a thing the march stops at. The blend's destination factor is One (added, the
        // first batch), OneMinusSrcColor (screened, so a shaft adds most over dark water and little
        // over a lit thing), or Zero for the debug view.
        _PlaceShare("Share of the surface that sends a beam", Range(0.05, 1)) = 0.35
        _PlacePower("A sheet's power under item 3", Range(1, 12)) = 2.5
        _PlaceOverObject("What is left in front of a thing", Range(0, 1)) = 0.4
        _PlaceReach("Longest march under item 3, metres", Range(4, 200)) = 30
        _PlaceMaskMetres("Size of the stretches of surface the sheets' strength varies over, metres", Range(1, 40)) = 12

        // Item 3's sheets (iteration 11): the size of the patch of surface a sheet of light comes
        // down from, metres; the depth over which the sheets fade, metres; and the debug switch
        // that draws the shaft term alone, opaque and four times as bright, over a black frame.
        _SheetMetres("Size of a sheet's patch of surface, metres", Range(0.5, 20)) = 2.5
        _SheetDepthMetres("Depth the sheets fade over, metres", Range(1, 100)) = 12
        _SheetShimmer("Share of a sheet the waves' focusing sets", Range(0, 1)) = 0.35
        _SheetOnsetMetres("Depth a sheet comes in over under the surface, metres", Range(0.1, 20)) = 4
        _PlaceDebug("Draw the shaft term alone", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            // After the ceiling: the light is in the water between the eye and the surface, so
            // it is added over the surface rather than drawn under it.
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ShaftMarch"
            Tags { "LightMode" = "UniversalForward" }

            Blend One [_DstBlend]
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"
            #include "TheatreDepth.hlsl"
            #include "TheatreVolume.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShaftColor;
                float _MarchStrength;
                float _MarchSteps;
                float _MarchContrast;
                float _MarchFocus;
                float _MarchPhaseCap;
                float _MarchReach;
                float4 _BoxMin;
                float4 _BoxMax;
                float4 _Tank;
                float _Waterline;
                float _PlaceShare;
                float _PlacePower;
                float _PlaceOverObject;
                float _PlaceReach;
                float _PlaceMaskMetres;
                float _SheetMetres;
                float _SheetDepthMetres;
                float _SheetShimmer;
                float _SheetOnsetMetres;
                float _PlaceDebug;
                float _DstBlend;
            CBUFFER_END

            // Item 3's caps: the reefs' (centre x, centre z, widest radius, underside y), set by
            // TheatreSkin. Outside the material's constant buffer, as an array must be.
            float4 _ReefCaps[32];
            float _ReefCapCount;

            // How much of the sun reaches a point past the reefs' caps, 0 to 1: the refracted
            // sun's ray from the point up to each cap's underside, inside the cap's circle or
            // not, with a soft rim. A beam under a cap was drawn as bright as one in open water
            // until iteration 4, as a comb of light under the rock.
            float CapShade(float3 p)
            {
                float3 up = -EvoSunRayWS();
                float shade = 1.0;
                int count = (int)min(_ReefCapCount, 32.0);

                [loop]
                for (int i = 0; i < count; i++)
                {
                    float4 cap = _ReefCaps[i];
                    if (p.y >= cap.w) continue;

                    float t = (cap.w - p.y) / max(0.15, up.y);
                    float2 q = p.xz + up.xz * t;
                    float r = length(q - cap.xy);

                    shade *= smoothstep(0.9 * cap.z, 1.05 * cap.z, r);
                }

                return shade;
            }

            // Where the refracted sun's ray through p left the surface, in the surface's plane: every
            // point of one beam shares it, so a field read here is a beam all the way down.
            float2 SurfaceEntry(float3 p)
            {
                float depth = max(0.0, EvoWaterlineY() - p.y);
                float3 ray = EvoSunRayWS();

                return p.xz - depth * ray.xz / max(0.15, -ray.y);
            }

            // Item 3's strength over the surface: a slow value noise at _PlaceMaskMetres, so one
            // stretch of sheets is bright and the next faint, never all at one strength. Drifts with
            // the shaders' clock over tens of seconds.
            float PlaceMask(float2 top, float time)
            {
                float3 unusedGradient;
                float field = EvoValueNoise(float3(top / max(1.0, _PlaceMaskMetres) + 31.0, time * 0.015), unusedGradient);

                return lerp(0.25, 1.0, smoothstep(0.3, 0.75, field));
            }

            // Item 3's sheets (iteration 11, in place of EvoBeamAt's comb): broad soft shafts from the
            // surface's light at a low spatial frequency, two octaves of value noise read where the
            // ray left the surface, each turned off the noise's lattice so nothing lines up with the
            // world's axes, drifting at about 0.2 m/s the way the long swell's focusing does and
            // changing shape over about fifty seconds (at twenty, an 8 s clip lost its main sheet
            // and a quarter of its light between the first frame and the last). Thresholded
            // at the share with a slow rise and a slower fall, so a sheet has no hard side, and each
            // varies in width with the noise. EvoBeamAt's curvature sits on the 1.6 m wave and drew
            // about ten parallel beams of one width and one strength, a stage curtain.
            float SheetAt(float2 top, float time)
            {
                float size = max(0.5, _SheetMetres);
                float2 flow = float2(0.2, 0.09) * time;

                float2 a = float2(0.8, 0.6);
                float2 q1 = float2(dot(top + flow, a), dot(top + flow, float2(-a.y, a.x))) / size;
                float2 b = float2(0.28, -0.96);
                float2 q2 = float2(dot(top - 0.7 * flow, b), dot(top - 0.7 * flow, float2(-b.y, b.x))) / (0.43 * size);

                float3 unusedGradient;
                float wide = EvoValueNoise(float3(q1, time * 0.02), unusedGradient);
                float fine = EvoValueNoise(float3(q2 + 17.0, time * 0.04), unusedGradient);
                float n = 0.72 * wide + 0.28 * fine;

                // Value noise clusters about a half; the share is read off its upper band.
                float edge = lerp(0.70, 0.40, saturate(_PlaceShare));
                float sheet = smoothstep(edge - 0.10, edge + 0.22, n);

                return pow(sheet, max(1.0, _PlacePower));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Only from under the surface, as the quads: above it there is nothing for a
                // beam to be in.
                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) return half4(0, 0, 0, 0);

                float3 origin, dir;
                float tScene;
                EvoViewRay(input.positionWS, input.positionCS, origin, dir, tScene);

                float t0 = unity_OrthoParams.w > 0.5 ? 0.0 : _ProjectionParams.y;
                float t1 = 1e6;

                if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1))
                {
                    return half4(0, 0, 0, 0);
                }

                // Whether the march ends on a thing the scene drew (a body, the rock, the bed) rather
                // than in open water: the depth stops it short of where the water would.
                bool place = EvoLookPlace();
                bool onObject = tScene < t1 - 0.05;

                t1 = min(t1, tScene);
                if (t1 <= t0) return half4(0, 0, 0, 0);

                t1 = min(t1, t0 + (place ? _PlaceReach : _MarchReach));

                int steps = (int)clamp(_MarchSteps, 8.0, 64.0);
                float dt = (t1 - t0) / steps;
                float jitter = EvoPixelDither(input.positionCS.xy);

                float3 sum = float3(0.0, 0.0, 0.0);

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = t0 + (i + jitter) * dt;
                    float3 p = origin + dir * t;

                    // The beam this point is in, and the light the surface focused onto it, as a
                    // multiple of the flat water's; between beams the in-scatter has the water.
                    float gain = min(EvoCausticGain(p, _Time.y), 4.0);
                    float shaft;

                    if (place)
                    {
                        // Item 3: the sheets, at the strength of their stretch of surface, out of the
                        // caps' shadow; the focusing at the sample's depth only a fifth of it, since at
                        // full share it redraws the short waves' comb inside a sheet.
                        float2 top = SurfaceEntry(p);
                        shaft = SheetAt(top, _Time.y) * PlaceMask(top, _Time.y)
                              * (_PlaceDebug > 1.5 ? 1.0 : CapShade(p))
                              * lerp(1.0, gain, 0.2 * _MarchFocus);

                        // The shimmer: a share of the sheet set by the waves' focusing where its ray
                        // left the surface (EvoBeamAt), so fine streaks run through a sheet and move
                        // with the waves, as they do under a real sea. At full share this is the comb
                        // the sheets replaced; a third of it reads as streaks inside soft light.
                        shaft *= lerp(1.0, 0.55 + 1.5 * EvoBeamAt(p, _Time.y), _SheetShimmer);

                        // Strongest near the surface, fading with depth; and in from the lens
                        // between two and eight metres, so the mid-distance carries them and a sheet
                        // beside the camera does not fill the frame.
                        float depth = max(0.0, EvoWaterlineY() - p.y);
                        shaft *= exp(-depth / max(1.0, _SheetDepthMetres));
                        shaft *= smoothstep(2.0, 8.0, t - t0);
                    }
                    else
                    {
                        shaft = pow(EvoBeamAt(p, _Time.y), _MarchContrast) * lerp(1.0, gain, _MarchFocus);
                    }

                    // Down to the sample along the refracted sun, and back to the eye through
                    // water that absorbs and also scatters out of the line of sight, so a long
                    // look across the tank is dominated by the near beams and does not sum the
                    // far ones into a haze.
                    float3 light = EvoSunAt(p) * exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(p));

                    // Under item 3 the march's far end is faded over its last two fifths, so a beam
                    // that runs past the reach dims out rather than stopping on a line (iteration 6
                    // drew every beam's top cut at one distance in the creature close-up).
                    if (place) shaft *= 1.0 - smoothstep(0.6 * _PlaceReach, _PlaceReach, t - t0);

                    // And a sheet comes in softly over its first metres under the surface
                    // (_SheetOnsetMetres), so a sheet seen ending at the surface fades out there
                    // rather than stopping in a rounded top: at 1.5 m the creature close-up drew
                    // its main sheet's top as a round glow, a moon in the water.
                    if (place) shaft *= smoothstep(0.0, max(0.1, _SheetOnsetMetres), EvoWaterlineY() - p.y);

                    sum += shaft * light;
                }

                // One phase term for the ray: the angle between the sun's ray and the look is the
                // same at every sample of a straight ray under a sun at infinity.
                float phase = min(EvoPhaseTerm(dir), _MarchPhaseCap);

                float3 colour = _ShaftColor.rgb * (_MarchStrength * phase * dt * inside) * sum;

                // Item 3: faint in front of a thing, and never past white, which the screen blend
                // needs (it takes one less the source as the destination's share).
                // The debug view (EVOSIM_THEATRE_SHAFT_DEBUG): the shaft term alone, before the cut
                // in front of things, four times as bright, written over the frame (TheatreSkin sets
                // the destination's blend to Zero), so the caps' shade can be seen; at 2 the same
                // without the caps, so the difference of the two is the caps' shade.
                if (place && _PlaceDebug > 0.5) return half4(min(4.0 * colour, 1.0), 1.0);

                if (place)
                {
                    if (onObject) colour *= _PlaceOverObject;
                    colour = min(colour, 0.95);
                }

                return half4(colour, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
