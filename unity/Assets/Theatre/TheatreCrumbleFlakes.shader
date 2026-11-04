// The crumble's flakes (D277, EVOSIM_THEATRE_CRUMBLE): the specks dead tissue sheds as it dwindles, drawn as marine snow.
//
// Each flake is a camera-facing quad of the mesh TheatreCrumble.cs builds when a camera is about to render, large enough to
// hold the flake's disc, its outline lobes, its blur and its soft edge. Its fragment sends the view ray through the pixel,
// finds the ray's nearest approach to the flake's centre and shades it with EvoShadeMote, the very function the snow shells
// (TheatreSnowShells.shader) shade their motes with, so the two snows share the size spread, the irregular outline, the soft
// edge, the lens blur and the energy kept. Like the shells it is additive, drawn with the depth test off, and stopped at the
// depth buffer by the ray's own length: a flake behind a body is not drawn across it, and none hangs outside the water.
//
// What differs from a shell mote: the colour. A flake is born in the dulled colour of the tissue it left (the vertex colour) and
// takes the snow's mote colour over its first seconds (_TakeSeconds), after which it is a mote of the general snow.
//
// Per vertex: POSITION the flake's centre in the world; uv0 the corner, -1 or 1 on each axis; uv1 the flake's mean radius in
// metres (already dissolved over its last seconds), its hash seed, its age in seconds, and 1 for a large mote (0 for a flake);
// COLOR the colour it starts in.
//
// The large motes. D279's birth cloud (TheatreBirthCloud.cs) and the bite crumbs (TheatreMouthBiteCrumbs.cs) draw with this
// shader at centimetres, and set uv1.w to 1; the crumble never does. EvoShadeMote hashes a mote's size and outline from its key,
// and here that key is the seed interpolated across the quad. The seed reaches the fragment off by an ulp in rows, columns and
// along the quad's diagonal, the hash's sine amplifies that past a whole turn, and each such pixel shades a different mote: a
// large flake clipped to the quad's square, lobes in other places. At a few millimetres it falls in a pixel or two; at
// centimetres it is the squares of lines and the hatched streaks. A large mote is therefore drawn by BigMote below: its own
// radius, no hash, no size spread, outline, fluff or aggregate, a smooth soft edge, the lens's blur, and EvoShadeMote's light.
//
// The dark motes. The wound plume (TheatreMouthBitePlume.cs) sets uv1.w to 2: the same soft disc, unlit, returned premultiplied
// (colour times coverage times the vertex alpha, and that coverage as alpha), drawn with its material's blend set to One,
// OneMinusSrcAlpha so it darkens what lies behind it. Every other material keeps the blend's default, One One, as before.

Shader "Evosim/Theatre Crumble Flakes"
{
    Properties
    {
        _MoteColor("Mote colour", Color) = (0.70, 0.80, 0.84, 1)
        _ShellGain("Brightness", Range(0, 4)) = 0.35
        _MoteAmbient("Ambient share", Range(0, 6)) = 0

        // Seconds a flake takes to go from its tissue colour to the mote colour.
        _TakeSeconds("Seconds to the snow's colour", Float) = 10

        // Copied from the snow shells' material each time the flakes are built: the water the shells sit in.
        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
        _Tank("Tank: centre x, centre z, radius, on", Vector) = (0, 0, 0, 0)
        _Waterline("The waterline, world y", Float) = 0

        // The pass's blend. One and One, the default, is the additive snow every material of this shader drew with before; the
        // wound plume (TheatreMouthBitePlume.cs) sets One and OneMinusSrcAlpha for its dark motes (uv1.w = 2).
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+12"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CrumbleFlakes"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5
            #pragma multi_compile _ _EVO_CAUSTIC_VARIETY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_EVO_CAUSTIC_VARIETY)
            #define EVO_CAUSTIC_LITE
            #endif
            #include "TheatreWater.hlsl"
            #include "TheatreDepth.hlsl"
            #include "TheatreVolume.hlsl"
            #define EVO_SNOW_MOTE
            #include "TheatreSnow.hlsl"

            // The snow shells' drift switch (TheatreSkin.PushSecond): only its on flag is read, for the phase term.
            float4 _EvoSnowDrift;

            // The most the phase term may brighten a mote seen toward the sun (as the shells').
            #define EVO_MOTE_PHASE_CAP 4.0

            // The size of the cell a flake is blurred within, in metres: the shells blur a mote no further than 0.07 of its cell,
            // and a flake has no cell, so this stands in for the middle shell's.
            #define EVO_FLAKE_CELL 1.0

            CBUFFER_START(UnityPerMaterial)
                float4 _MoteColor;
                float _ShellGain;
                float _MoteAmbient;
                float _TakeSeconds;
                float4 _BoxMin;
                float4 _BoxMax;
                float4 _Tank;
                float _Waterline;
            CBUFFER_END

            struct Attributes
            {
                float3 centre : POSITION;
                float2 corner : TEXCOORD0;
                float4 data   : TEXCOORD1;
                float4 colour : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centreWS   : TEXCOORD1;
                float4 data       : TEXCOORD2;
                float4 colour     : TEXCOORD3;
            };

            // A flake's hash key: the cell the shells hash a mote's size and outline by, here one per flake.
            float3 FlakeKey(float seed)
            {
                return float3(seed, seed * 1.31 + 0.17, seed * 0.77 + 3.3);
            }

            // A large mote's soft edge, as a share of its radius: the width over which it goes from full to nothing.
            #define EVO_BIG_MOTE_EDGE 0.5

            // The death cue's stains (uv1.w = 3, TheatreDeathCue.Motes): the share of the water's own colour a stain is drawn in, and
            // how much its tint (the drained paint, linear, near 0.1) warms it: a little lighter than the water, faintly warm, as a
            // cloud of matter catches the light it sits in. Half the water's colour (0.3 and 1.5) read as soot or ink (phase123);
            // 1.25 lifted the water behind it by 11 to 13 of 255, as much as D281's orange discs, and read as a haze (phase124).
            static const float DeathStainShare = 1.1;
            static const float DeathStainTint = 0.5;

            // The most the lens blurs a large mote, in metres at a pixel's size: the shells' cap in pixels of a 1080-line frame.
            // No cell clamp: a large mote has no cell, and 0.07 of EVO_FLAKE_CELL would shrink the blur as the mote grows.
            float BigMoteBlurMost(float pixel)
            {
                return _EvoSnowLens.y > 0.0 ? _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0 : 0.0;
            }

            // A large mote's quad: half its side, holding the disc, the blur and the soft edge at their greatest.
            float BigMoteHalfSize(float radius, float pixel)
            {
                float r = max(radius, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                float blur = BigMoteBlurMost(pixel);
                float soft = pixel + blur + EVO_BIG_MOTE_EDGE * r;
                float reach = r + 0.5 * blur;

                return (reach + 0.5 * soft) * 1.05 + pixel;
            }

            // A large mote's shading: a soft round disc of its own radius, lit as EvoShadeMote lights a mote. Every term is
            // continuous in the pixel, and nothing is hashed, so an ulp in the interpolated data moves nothing visibly. The same
            // arguments as EvoShadeMote's, less the hash key, the cap, the cell and the fall, which the flakes pass as 1e4 and 0.
            void BigMote(float3 q0, float3 dir, float t, float3 m, float radius, float time, float3 boxMin, float3 boxMax,
                         float4 tank, inout float4 sum)
            {
                float3 off = q0 + dir * t - m;
                float dist = length(off);
                float pixel = max(1e-5, EvoPixelMetres(t));

                float r = max(radius, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                // The lens: EvoShadeMote's circle of confusion, capped as the shells cap it.
                float blur = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                    float focus = max(0.05, evoSnowFocus);
                    blur = 0.5 * _EvoSnowLens.y * abs(depth - focus) / focus;
                    blur = min(blur, BigMoteBlurMost(pixel));
                }

                // A round disc with a smooth edge: full in its core, falling to nothing over the edge's width.
                float soft = pixel + blur + EVO_BIG_MOTE_EDGE * r;
                float reach = r + 0.5 * blur;
                float cover = saturate((reach - dist) / soft + 0.5);
                cover = cover * cover * (3.0 - 2.0 * cover);

                if (cover <= 0.0) return;
                if (!EvoInWater(m, boxMin, boxMax, tank)) return;

                // Its light kept as EvoShadeMote keeps a mote's: widened to a pixel, spread over the area it is drawn on.
                float energy = min(1.0, (radius * radius) / (r * r));
                if (_EvoSnowFluff.z > 0.5)
                {
                    float sharpArea = r * r + pixel * pixel / 12.0;
                    float drawnArea = reach * reach + soft * soft / 12.0;
                    energy *= saturate(sharpArea / max(1e-12, drawnArea));
                }
                else if (blur > 0.0)
                {
                    float wide = r + 0.5 * blur;
                    energy *= pow(saturate((r * r) / (wide * wide)), _EvoSnowLens.w);
                }

                float gain = min(EvoCausticGain(m, time), 7.0);
                float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(m));
                float3 light = EvoSunAt(m) * (0.3 + 0.7 * gain) * kept;

                sum.rgb += light * (cover * energy);
                float3 around = EvoWaterColour(m.y) * kept;
                sum.a += cover * energy * dot(around, float3(0.2126, 0.7152, 0.0722));
            }

            // A dark mote's coverage (uv1.w = 2, the wound plume): BigMote's soft round disc, its lens blur and its water test, with
            // no light. A copy rather than a share, so BigMote's arithmetic is the bytes it was.
            float DarkMoteCover(float3 q0, float3 dir, float t, float3 m, float radius, float3 boxMin, float3 boxMax, float4 tank)
            {
                float3 off = q0 + dir * t - m;
                float dist = length(off);
                float pixel = max(1e-5, EvoPixelMetres(t));

                float r = max(radius, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                float blur = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                    float focus = max(0.05, evoSnowFocus);
                    blur = 0.5 * _EvoSnowLens.y * abs(depth - focus) / focus;
                    blur = min(blur, BigMoteBlurMost(pixel));
                }

                float soft = pixel + blur + EVO_BIG_MOTE_EDGE * r;
                float reach = r + 0.5 * blur;
                float cover = saturate((reach - dist) / soft + 0.5);
                cover = cover * cover * (3.0 - 2.0 * cover);

                if (cover <= 0.0) return 0.0;
                if (!EvoInWater(m, boxMin, boxMax, tank)) return 0.0;

                return cover;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 centre = TransformObjectToWorld(input.centre);
                float radius = input.data.x;

                // The flake's largest size on screen, so the quad holds all of it. The size lines are EvoShadeMote's own (the mote's
                // size spread and its large flakes); the outline, the blur and the soft edge are taken at their greatest.
                float3 g = EvoHash3(FlakeKey(input.data.y) * 2.17 + 0.0 + 3.1);
                float own = radius;
                if (_EvoSnowFlake.x > 0.0)
                {
                    float normal = (g.x + g.y + g.z - 1.5) * 2.0;
                    own *= exp(_EvoSnowFlake.x * normal - _EvoSnowFlake.x * _EvoSnowFlake.x);
                }
                if (frac(g.x * 7.31 + g.z * 3.17) < _EvoSnowFlake.y) own *= max(1.0, _EvoSnowFlake.z);

                float3 eye = GetCameraPositionWS();
                float t = unity_OrthoParams.w > 0.5 ? 0.0 : length(centre - eye);
                float pixel = max(1e-5, EvoPixelMetres(t));

                float r = max(own, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                float outlineMost = _EvoSnowFlake.w > 0.0 ? 1.0 + _EvoSnowFlake.w * 0.66 : 1.0;

                float blur = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    blur = _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0;
                    blur = min(blur, max(0.0, 0.07 * EVO_FLAKE_CELL - r));
                }

                float soft = pixel + blur + _EvoSnowFluff.x * r;
                float reach = r * outlineMost + 0.5 * blur;
                float halfSize = (reach + 0.5 * soft) * 1.05 + pixel;

                // A large mote (uv1.w, never set by the crumble) is sized by its own terms, without the hash.
                if (input.data.w > 0.5) halfSize = BigMoteHalfSize(radius, pixel);

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 corner = centre + (right * input.corner.x + up * input.corner.y) * halfSize;

                output.positionWS = corner;
                output.positionCS = TransformWorldToHClip(corner);
                output.centreWS = centre;
                output.data = input.data;
                output.colour = input.colour;

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) return half4(0, 0, 0, 0);

                float3 origin, dir;
                float tScene;
                EvoViewRay(input.positionWS, input.positionCS, origin, dir, tScene);

                float t0 = unity_OrthoParams.w > 0.5 ? 0.0 : _ProjectionParams.y;
                float t1 = tScene;

                if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1))
                {
                    return half4(0, 0, 0, 0);
                }

                float time = EvoTime();

                // The lens's focus: the shot's, or with none the scene's depth at the frame's centre, as the shells take it.
                evoSnowNearT = 1e9;
                evoSnowFocus = _EvoSnowLens.x;
                if (_EvoSnowLens.y > 0.0 && evoSnowFocus <= 0.0)
                    evoSnowFocus = min(EvoSceneEyeDepth(float2(0.5, 0.5)), 12.0);

                float3 m = input.centreWS;
                float t = dot(m - origin, dir);
                float4 sum = float4(0.0, 0.0, 0.0, 0.0);

                // A dark mote (uv1.w = 2, the wound plume): premultiplied, its colour over its coverage times its opacity (the vertex
                // colour's alpha), so a material blending One, OneMinusSrcAlpha darkens what lies behind toward it. Unlit: it is a
                // stain in the water, not a speck catching light. No other mote reaches this branch.
                if (input.data.w > 1.5)
                {
                    float coverDark = t > t0 && t < t1
                        ? DarkMoteCover(origin, dir, t, m, input.data.x, _BoxMin.xyz, _BoxMax.xyz, _Tank) : 0.0;
                    float alphaDark = saturate(coverDark * input.colour.a * inside);

                    // uv1.w = 3, the death cue's stains (EVOSIM_THEATRE_DEATH_MOTES 1 or 3): a share of the water's own colour at the
                    // mote's height, warmed by its tint, so the cloud is keyed to the water at any depth and never the paint's glow.
                    // A plume's mote (w = 2) keeps its own colour.
                    float3 colourDark = input.data.w > 2.5
                        ? EvoWaterColour(m.y) * (DeathStainShare + DeathStainTint * input.colour.rgb)
                        : input.colour.rgb;
                    return half4(colourDark * alphaDark, alphaDark);
                }

                // Stopped by the scene as the shells' motes are: a flake behind a body is not drawn across it.
                if (t > t0 && t < t1 && input.data.w > 0.5)
                {
                    BigMote(origin, dir, t, m, input.data.x, time, _BoxMin.xyz, _BoxMax.xyz, _Tank, sum);
                }
                else if (t > t0 && t < t1)
                {
                    EvoShadeMote(origin, dir, t, m, FlakeKey(input.data.y), 0.0, input.data.x, 1e4, EVO_FLAKE_CELL,
                                 float3(0.0, 0.0, 0.0), time, _BoxMin.xyz, _BoxMax.xyz, _Tank, sum);
                }

                // From the tissue's colour to the snow's over the first seconds of its life.
                float take = saturate(input.data.z / max(0.01, _TakeSeconds));
                take = take * take * (3.0 - 2.0 * take);
                float3 colour = lerp(input.colour.rgb, _MoteColor.rgb, take);

                // As the shells finish: the phase term toward the sun, and the water's own light on the flake.
                float phase = _EvoSnowDrift.w > 0.5 ? min(EvoPhaseTerm(dir), EVO_MOTE_PHASE_CAP) : 1.0;

                float day = EvoDayGain() * (EvoLookFade() ? max(0.0, _EvoFade.z) : 1.0);
                float3 ambient = colour * (_MoteAmbient * day * inside) * sum.a;

                return half4(colour * (_ShellGain * inside * phase) * sum.rgb + ambient, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
