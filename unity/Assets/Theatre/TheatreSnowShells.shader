// Marine snow around the camera: the underwater look's item 5
// (logbook/specs/underwater-look/brief.md), in place of the particle system over the whole tank
// when EVOSIM_THEATRE_SNOW_SHELLS is on.
//
// Why shells. Two thousand six hundred motes spread over a tank tens of metres across leave a
// few in any one shot, and at a film's scale none of them shows. Snow is seen near the lens, so
// it is drawn where the lens is: three shells about each camera (near, middle and far), each a
// lattice of cells holding at most one mote, the near cells small and the far ones large, so
// every shot sees about the same density of specks whatever it looks at.
//
// How, without particles. Each shell walks the cells its stretch of the view ray crosses
// (Amanatides and Woo's grid walk) and asks each cell's mote, placed by a hash of the cell,
// whether the ray passes within its radius. The lattice falls with the shaders' clock, and each
// mote sways a little inside its cell, so the snow drifts down and never jumps; nothing is drawn
// from a random draw per frame, and two renders of one instant are one picture. The motes stay
// in the world as the camera moves, so they parallax the way specks in water do.
//
// Small, and sharp only at the focus. With the lens's dials at zero a mote is a disc with a
// one-pixel edge, never blurred. With them on (_EvoSnowLens, _EvoSnowFlake, _EvoSnowFluff in
// TheatreSnow.hlsl; the owner, 2026-10-19: real snow is soft near and far and sharp only near the
// focus, which is what gives it depth) each mote is blurred by a thin lens's circle of confusion at
// its depth, its size is drawn from a spread with a few large flakes, and its outline is a few
// lobes of its own. A near mote's sharp size on screen is capped, so one passing the lens is a
// bright speck and not a blob; a far mote too small for a pixel is drawn at most of one and dimmed
// by the area it was given. Its brightness
// is the shafts' light at the mote (item 4's EvoCausticGain and EvoSunAt), so the snow sparkles
// where the shafts are and dims with depth.
//
// Additive, stopped at the depth buffer, and never drawn outside the water: a mote cannot hide
// a body, and none hangs where the run had no water.

Shader "Evosim/Theatre Snow Shells"
{
    Properties
    {
        _MoteColor("Mote colour", Color) = (0.70, 0.80, 0.84, 1)
        _ShellGain("Brightness", Range(0, 4)) = 0.35

        // Each shell's share of cells holding a mote is multiplied by this (EVOSIM_THEATRE_SNOW_QUANTITY).
        _ShareScale("Quantity", Range(0.25, 3)) = 1

        // How much brighter than the open water behind it a mote stands, from the water's own light,
        // over and above the sun's (EVOSIM_THEATRE_SNOW_AMBIENT): 0.25 is a quarter of the water's
        // brightness, in the mote's colour. Without it a mote is lit by the sun at its depth alone, and
        // near round 57's bed the sun left is too little to show against the bright water (2026-10-11).
        _MoteAmbient("Ambient share", Range(0, 6)) = 0

        // Metres a mote falls in a second of the shaders' clock. Slow: detritus, not rain.
        _FallMetresPerSecond("Fall, metres a second", Range(0, 0.5)) = 0.05

        // The shells: x and y the stretch of the ray from the eye, metres; z the cell's size;
        // w the share of cells holding a mote.
        _Near("Near shell (from, to, cell, share)", Vector) = (0.25, 2.0, 0.35, 0.5)
        _Middle("Middle shell (from, to, cell, share)", Vector) = (2.0, 6.0, 0.8, 0.5)
        _Far("Far shell (from, to, cell, share)", Vector) = (6.0, 16.0, 1.8, 0.6)

        // Each shell's mote radius in metres: near, middle, far; w the near motes' cap on screen,
        // in pixels of radius.
        _Radii("Mote radius near, middle, far; near cap px", Vector) = (0.0025, 0.004, 0.008, 5)

        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
        _Tank("Tank: centre x, centre z, radius, on", Vector) = (0, 0, 0, 0)
        _Waterline("The waterline, world y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+11"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SnowShells"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
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
            #include "TheatreSnow.hlsl"

            // The second batch's drift (the owner's addition to brief-2.md): xyz is how far the
            // water around the camera has carried the snow, metres, by the run's current and its
            // sink rate over the simulation's seconds (TheatreSkin.PushSnow); w is 1 while it is on,
            // in place of the first batch's fall. A global, so a film's every frame moves it.
            float4 _EvoSnowDrift;

            // The most the phase term may brighten a mote seen toward the sun.
            #define EVO_MOTE_PHASE_CAP 4.0

            CBUFFER_START(UnityPerMaterial)
                float4 _MoteColor;
                float _ShellGain;
                float _ShareScale;
                float _MoteAmbient;
                float _FallMetresPerSecond;
                float4 _Near;
                float4 _Middle;
                float4 _Far;
                float4 _Radii;
                float4 _BoxMin;
                float4 _BoxMax;
                float4 _Tank;
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
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                return output;
            }

            // One shell: the motes in the cells the ray crosses between tA and tB, in the frame
            // that falls with the snow (q = world + fall), so a mote is fixed in its cell there.
            // rgb: the motes' sunlight; a: the water's own light on them, as a brightness: the column's
            // colour at each mote's height, kept by the water between it and the eye (_MoteAmbient).
            float4 Shell(float3 origin, float3 dir, float tA, float tB, float4 shell, float radius,
                         float capPixels, float seed, float time)
            {
                float4 sum = float4(0.0, 0.0, 0.0, 0.0);
                if (tB <= tA) return sum;

                float cell = max(0.05, shell.z);

                // The lattice's frame: falling with the shaders' clock in the first batch, carried
                // by the run's water under the drift. A mote's world place is its lattice place
                // less this, so the water's displacement D is a frame offset of -D.
                bool drifting = _EvoSnowDrift.w > 0.5;
                float3 fall = drifting ? -_EvoSnowDrift.xyz : float3(0.0, _FallMetresPerSecond * time, 0.0);

                // Under the drift a cell holds its mote by the run's column density (item 4).
                bool runSnow = EvoRunSnow();
                float3 q0 = origin + fall;
                float3 start = q0 + dir * tA;

                float3 c = floor(start / cell);
                float3 safe = dir + step(abs(dir), 1e-6) * 1e-6;
                float3 ahead = step(0.0, safe);
                float3 stepC = ahead * 2.0 - 1.0;
                float3 tMax = tA + ((c + ahead) * cell - start) / safe;
                float3 tDelta = cell / abs(safe);

                [loop]
                for (int i = 0; i < 24; i++)
                {
                    float3 h = EvoHash3(c + seed);

                    // The share of cells with a mote, scaled by the run's column density at the
                    // cell (item 4); the first batch's share without the fields.
                    float share = shell.w * _ShareScale;
                    if (runSnow) share *= EvoSnowDensityRatio((c + 0.5) * cell - fall);
                    share = min(0.95, share);

                    if (h.x < share)
                    {
                        float3 place = EvoHash3(c * 1.31 + seed + 7.7);
                        float3 m = (c + 0.2 + 0.6 * place) * cell;

                        // A slow sway inside the cell, never out of it: 0.12 of a cell either way
                        // from a centre at least 0.2 of one in from every wall (under the drift a
                        // third of that, and in y too, so the snow tumbles a little in the water
                        // that carries it and never looks rigid).
                        if (drifting)
                        {
                            m += 0.04 * cell * sin(time * (0.13 + 0.11 * h.yzx) + 6.2831853 * h.zxy);
                        }
                        else
                        {
                            m.xz += 0.12 * cell * sin(time * (0.21 + 0.17 * h.yz) + 6.2831853 * h.zy);
                        }

                        float t = dot(m - q0, dir);

                        if (t > tA && t < tB)
                        {
                            float3 off = q0 + dir * t - m;
                            float dist = length(off);
                            float pixel = max(1e-5, EvoPixelMetres(t));

                            // The mote's own size (_EvoSnowFlake): the shell's radius spread
                            // log-normally about it with the mean area kept, and a share of the
                            // motes made large flakes. One size a shell with the dials at zero.
                            float3 g = EvoHash3(c * 2.17 + seed + 3.1);
                            float own = radius;
                            if (_EvoSnowFlake.x > 0.0)
                            {
                                float normal = (g.x + g.y + g.z - 1.5) * 2.0;
                                own *= exp(_EvoSnowFlake.x * normal - _EvoSnowFlake.x * _EvoSnowFlake.x);
                            }
                            if (frac(g.x * 7.31 + g.z * 3.17) < _EvoSnowFlake.y) own *= max(1.0, _EvoSnowFlake.z);

                            // At least most of a pixel, so a far mote is a speck and not a
                            // flicker; at most the cap, so a near one is never a blob.
                            float r = max(own, 0.75 * pixel);
                            r = min(r, capPixels * pixel);

                            // A flake's outline, in the plane across the line of sight: a few lobes
                            // of the mote's own, turning slowly, so no two specks are the same disc.
                            float outline = 1.0;
                            if (_EvoSnowFlake.w > 0.0 && dist > 1e-7)
                            {
                                float3 side = normalize(cross(abs(dir.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0), dir));
                                float3 rise = cross(dir, side);
                                float a = atan2(dot(off, rise), dot(off, side));
                                float3 turn = 6.2831853 * g + time * (0.04 + 0.08 * g.zxy);
                                outline = 1.0 + _EvoSnowFlake.w * (0.30 * g.y * cos(2.0 * a + turn.x) +
                                                                   0.22 * cos(3.0 * a + turn.y) +
                                                                   0.14 * g.x * cos(5.0 * a + turn.z));
                                outline = max(0.35, outline);
                            }

                            // The lens (_EvoSnowLens): a thin lens's circle of confusion at the mote,
                            // in metres at its depth, which is half the aperture times the mote's
                            // distance from the focus over the focus. Capped in pixels of a
                            // 1080-line frame, whatever the render's size, and kept
                            // inside the mote's cell, whose walls the ray's walk does not look past.
                            float blur = 0.0;
                            if (_EvoSnowLens.y > 0.0)
                            {
                                float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                                float focus = max(0.05, evoSnowFocus);
                                blur = 0.5 * _EvoSnowLens.y * abs(depth - focus) / focus;
                                blur = min(blur, _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0);
                                blur = min(blur, max(0.0, 0.07 * cell - r));
                            }

                            // The flake's edge: a pixel wide when sharp, as wide as the blur when
                            // not, and softened by the fluff. The disc's half-light radius grows by
                            // half the blur, so its light spreads over r + blur and no further.
                            float soft = pixel + blur + _EvoSnowFluff.x * r;
                            float reach = r * outline + 0.5 * blur;
                            float cover = saturate((reach - dist) / soft + 0.5);

                            if (cover > 0.0)
                            {
                                float3 world = m - fall;

                                if (EvoInWater(world, _BoxMin.xyz, _BoxMax.xyz, _Tank))
                                {
                                    // A mote widened to a pixel keeps its own light, spread; a
                                    // blurred one spreads it again, over its larger disc, by the
                                    // power the lens's dial gives.
                                    float energy = min(1.0, (own * own) / (r * r));
                                    if (_EvoSnowFluff.z > 0.5)
                                    {
                                        // The flake's light kept over the area it is drawn on: a disc
                                        // of radius reach with an edge as wide as soft covers about
                                        // pi (reach^2 + soft^2 / 12), against pi (r^2 + pixel^2 / 12)
                                        // drawn sharp, so a defocused or fluffy flake spreads the
                                        // same light thinly (the owner's note of 2026-10-19).
                                        float sharpArea = r * r + pixel * pixel / 12.0;
                                        float drawnArea = reach * reach + soft * soft / 12.0;
                                        energy *= saturate(sharpArea / max(1e-12, drawnArea));
                                    }
                                    else if (blur > 0.0)
                                    {
                                        float wide = r + 0.5 * blur;
                                        energy *= pow(saturate((r * r) / (wide * wide)), _EvoSnowLens.w);
                                    }

                                    // A large flake is a loose aggregate: its light grows with its
                                    // size, not its area.
                                    if (_EvoSnowFluff.y > 0.0 && own > radius)
                                        energy *= pow(saturate(radius / own), _EvoSnowFluff.y);
                                    if (dist < r * outline) evoSnowNearT = min(evoSnowNearT, t);

                                    float gain = min(EvoCausticGain(world, time), 7.0);
                                    float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(world));
                                    float3 light = EvoSunAt(world) * (0.3 + 0.7 * gain) * kept;

                                    sum.rgb += light * (cover * energy);
                                    float3 around = EvoWaterColour(world.y) * kept;
                                    sum.a += cover * energy * dot(around, float3(0.2126, 0.7152, 0.0722));
                                }
                            }
                        }
                    }

                    float tNext = min(tMax.x, min(tMax.y, tMax.z));
                    if (tNext > tB) break;

                    if (tMax.x <= tMax.y && tMax.x <= tMax.z)
                    {
                        c.x += stepC.x;
                        tMax.x += tDelta.x;
                    }
                    else if (tMax.y <= tMax.z)
                    {
                        c.y += stepC.y;
                        tMax.y += tDelta.y;
                    }
                    else
                    {
                        c.z += stepC.z;
                        tMax.z += tDelta.z;
                    }
                }

                return sum;
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
                float4 sum = float4(0.0, 0.0, 0.0, 0.0);

                // The lens's focus: the shot's, or with none the scene's depth at the frame's
                // centre, as an autofocus takes it, no further than the water lets anything be seen.
                evoSnowNearT = 1e9;
                evoSnowFocus = _EvoSnowLens.x;
                if (_EvoSnowLens.y > 0.0 && evoSnowFocus <= 0.0)
                    evoSnowFocus = min(EvoSceneEyeDepth(float2(0.5, 0.5)), 12.0);

                sum += Shell(origin, dir, max(t0, _Near.x), min(t1, _Near.y), _Near, _Radii.x, _Radii.w, 0.0, time);
                sum += Shell(origin, dir, max(t0, _Middle.x), min(t1, _Middle.y), _Middle, _Radii.y, 1e4, 17.0, time);
                sum += Shell(origin, dir, max(t0, _Far.x), min(t1, _Far.y), _Far, _Radii.z, 1e4, 41.0, time);

                // Under the drift a mote seen toward the sun glows brighter than one seen away from
                // it, by the water's own forward-peaked phase term, capped.
                float phase = _EvoSnowDrift.w > 0.5 ? min(EvoPhaseTerm(dir), EVO_MOTE_PHASE_CAP) : 1.0;

                // The water's own light: each mote lit by the water around it, a share of the column's
                // brightness at its height and at the run's day, in the mote's colour, so a mote near a
                // dark winter bed still shows where the sun that reaches it is too little. The first cut
                // (2026-10-11) took the water along the look, which is dark looking down at the bed, and
                // showed nothing at 0.1.
                float day = EvoDayGain() * (EvoLookFade() ? max(0.0, _EvoFade.z) : 1.0);
                float3 ambient = _MoteColor.rgb * (_MoteAmbient * day * inside) * sum.a;

                return half4(_MoteColor.rgb * (_ShellGain * inside * phase) * sum.rgb + ambient, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
