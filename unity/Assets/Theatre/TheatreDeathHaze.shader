// The death cue's haze (EVOSIM_THEATRE_DEATH_HAZE, with EVOSIM_THEATRE_DEATH_CUE): the dull murk a dead body releases, drawn in
// place of D281's disc cloud, which at full size read as a veil of identical flat discs laid over the body in one frame.
//
// Each puff and speck is a camera-facing quad of the mesh TheatreDeathCue.cs builds when a camera is about to render. Its fragment
// sends the view ray through the pixel, finds the ray's nearest approach to the quad's centre, and returns a premultiplied colour
// (colour times coverage times opacity, and that coverage times opacity as alpha) for a blend of One, OneMinusSrcAlpha: the haze
// darkens what lies behind it toward its colour, as the wound plume's dark motes do. Unlit: it is a stain in the water, not light.
//
// A puff (uv1.w = 0) is large and very soft: a gaussian of its radius, with its edge pushed in and out and its density broken by two
// octaves of value noise taken about its centre in units of its radius, which drift slowly with its age so the murk billows; tapered
// to nothing at EVO_HAZE_REACH radii, where its quad ends, so no square edge shows. It fades softly against the scene's depth and
// against the near end of the water, over half its radius, rather than being cut there. A third, lower noise cuts it into lumps:
// below a threshold of the puff's own (hashed from its seed) the density goes to nothing, so some puffs have holes or split into
// lobes, and none is an even aura (third pass, 2026-11-03).
//
// A speck (uv1.w = 1) is a fine grain of particulate: a tiny dark mote whose outline is cut by noise of the way out from its centre
// and stretched along a hashed axis, so no two are the same shape and none is a disc; the lens's blur is capped at about a pixel, so
// it never grows into a bokeh disc; and a speck finer than the pixel floor is drawn faint rather than as a full dot. Stopped by the
// scene's depth. Only the death cue's haze and the wound plume's haze use this shader, so the speck change reaches nothing else.
//
// Both take the water's fog with distance as the flakes take it (TheatreCrumbleFlakes.shader's BigMote): kept = exp(-(absorption +
// in-scatter) * the metres of water between the eye and the centre). The colour is kept per channel, and the opacity by its
// luminance, so a far puff fades into the water rather than standing on it as a dark patch.
//
// Per vertex: POSITION the centre in the world; uv0 the corner, -1 or 1 on each axis; uv1 the radius in metres, the hash seed, the
// age in film seconds, and the kind (0 a puff, 1 a speck); COLOR the colour, with the opacity in alpha.

Shader "Evosim/Theatre Death Haze"
{
    Properties
    {
        // Copied from the snow shells' material each time the haze is built: the water the shells sit in.
        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
        _Tank("Tank: centre x, centre z, radius, on", Vector) = (0, 0, 0, 0)
        _Waterline("The waterline, world y", Float) = 0

        // The haze at 2 (TheatreDeathCue.HazeKeyed): drawn in the water's own colour at its height, this share of it. Off by
        // default, and the colour is the vertex colour as it was.
        _HazeKeyed("Keyed to the water", Float) = 0
        _HazeShare("Share of the water's colour", Float) = 1.1
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
            Name "DeathHaze"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TheatreWater.hlsl"
            #include "TheatreDepth.hlsl"
            #include "TheatreVolume.hlsl"
            #include "TheatreSnow.hlsl"

            // How far a puff reaches, in its radius: its window ends here, and so does its quad (TheatreDeathCue.PuffReach).
            #define EVO_HAZE_REACH 1.6

            // The gaussian's fall: the density at one (noise-moved) radius is exp(-EVO_HAZE_FALL).
            #define EVO_HAZE_FALL 2.2

            // How far the noise pushes a puff's edge in and out, as a share of its radius either way.
            #define EVO_HAZE_EDGE 0.3

            // The noise's cycles across a puff's radius, and how many it drifts a film second (the billow).
            #define EVO_HAZE_CYCLES 1.3
            #define EVO_HAZE_BILLOW 0.22

            // The share of a puff's radius over which it fades against the scene and the water's near end.
            #define EVO_HAZE_SOFT 0.5

            // The lumps: a low noise's cycles across a puff's radius, and the range its gap threshold is hashed in (the noise is in
            // [0, 1]; below the threshold the puff is empty water), with the noise's rise from gap to full. D262's strong start.
            #define EVO_HAZE_LUMP_CYCLES 0.75
            #define EVO_HAZE_GAP_LOW 0.3
            #define EVO_HAZE_GAP_HIGH 0.46
            #define EVO_HAZE_GAP_SOFT 0.16

            // The lumps' density gain, so a holed puff keeps about the strength of the even one it replaces.
            #define EVO_HAZE_GAIN 1.35

            // How much the keyed haze's tint warms the water's colour (the haze at 2), as the motes' stain's does.
            #define EVO_HAZE_KEY_TINT 0.5

            // A speck's soft edge, as a share of its radius: crisper than the crumble's dark motes' 0.5, a grain and not a dot.
            #define EVO_SPECK_EDGE 0.3

            // The most the lens blurs a speck, in pixels: about one, so it never grows into a bokeh disc.
            #define EVO_SPECK_BLUR_PIXELS 1.0

            // How far the noise pushes a speck's outline in and out, as a share of its radius either way, and the noise's cycles
            // about it. D262's strong start.
            #define EVO_SPECK_RAGGED 0.45
            #define EVO_SPECK_RAGGED_CYCLES 1.6

            // The most a speck is stretched along its hashed axis (1 plus this): a shard or a crumb, not a round grain.
            #define EVO_SPECK_STRETCH 1.0

            CBUFFER_START(UnityPerMaterial)
                float4 _BoxMin;
                float4 _BoxMax;
                float4 _Tank;
                float _Waterline;
                float _HazeKeyed;
                float _HazeShare;
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

            // The most the lens blurs a speck, in metres at a pixel's size: the shells' cap in pixels of a 1080-line frame, and never
            // more than EVO_SPECK_BLUR_PIXELS.
            float SpeckBlurMost(float pixel)
            {
                float shells = _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0;
                return _EvoSnowLens.y > 0.0 ? min(shells, EVO_SPECK_BLUR_PIXELS * pixel) : 0.0;
            }

            // A speck's quad: half its side, holding the ragged, stretched outline, the blur and the soft edge at their greatest.
            float SpeckHalfSize(float radius, float pixel)
            {
                float r = max(radius, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                float blur = SpeckBlurMost(pixel);
                float soft = pixel + blur + EVO_SPECK_EDGE * r;
                float reach = r * (1.0 + EVO_SPECK_RAGGED) * (1.0 + EVO_SPECK_STRETCH) + 0.5 * blur;

                return (reach + 0.5 * soft) * 1.05 + pixel;
            }

            // A speck's coverage: a tiny dark grain whose outline the noise cuts and a hashed axis stretches, with at most about a
            // pixel of the lens's blur; one finer than the pixel floor is drawn faint, in proportion, rather than as a full dot.
            float SpeckCover(float3 q0, float3 dir, float t, float3 m, float radius, float seed)
            {
                float3 d = q0 + dir * t - m;
                float pixel = max(1e-5, EvoPixelMetres(t));

                float r = max(radius, 0.75 * pixel);
                r = min(r, 1e4 * pixel);

                // Stretched along its own axis: distance along the axis is shrunk, so the grain reaches further that way.
                float3 axis = frac(seed * float3(0.4142, 0.7321, 0.2361) + float3(0.31, 0.77, 0.53)) * 2.0 - 1.0;
                axis = dot(axis, axis) > 1e-4 ? normalize(axis) : float3(0.0, 1.0, 0.0);
                float stretch = 1.0 + EVO_SPECK_STRETCH * frac(seed * 0.7548776 + 0.19);
                float3 squeezed = d - (1.0 - 1.0 / stretch) * dot(d, axis) * axis;
                float dist = length(squeezed);

                // The outline pushed in and out by two octaves of noise of the way out from the centre: continuous all round it.
                float3 way = d / max(1e-7, length(d));
                float3 place = frac(seed * float3(0.2113, 0.1597, 0.3331) + float3(0.37, 0.61, 0.83)) * 64.0;
                float3 gradient;
                float n1 = EvoValueNoise(way * EVO_SPECK_RAGGED_CYCLES + place, gradient);
                float n2 = EvoValueNoise(way * (2.7 * EVO_SPECK_RAGGED_CYCLES) + place.zxy, gradient);
                float rag = 1.0 + EVO_SPECK_RAGGED * (2.0 * (0.65 * n1 + 0.35 * n2) - 1.0);

                float blur = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                    float focus = max(0.05, evoSnowFocus);
                    blur = 0.5 * _EvoSnowLens.y * abs(depth - focus) / focus;
                    blur = min(blur, SpeckBlurMost(pixel));
                }

                float soft = pixel + blur + EVO_SPECK_EDGE * r;
                float reach = r * rag + 0.5 * blur;
                float cover = saturate((reach - dist) / soft + 0.5);
                cover = cover * cover * (3.0 - 2.0 * cover);

                // A grain finer than the floor keeps its share of the floor's coverage, so the finest read as faint dust.
                return cover * saturate(radius / r);
            }

            // A puff's coverage at the ray's nearest approach to its centre: a gaussian whose edge and density the noise breaks,
            // windowed to nothing at EVO_HAZE_REACH radii.
            float PuffCover(float3 q0, float3 dir, float t, float3 m, float radius, float seed, float age)
            {
                float3 off = q0 + dir * t - m;
                float pixel = max(1e-5, EvoPixelMetres(t));
                float R = max(radius, 2.0 * pixel);
                float dist = length(off);

                // Two octaves of value noise about the centre in units of the radius, each puff in its own place in the field (from
                // its seed), drifting with its age. Value noise is continuous, so an ulp in the interpolated seed moves nothing.
                float3 place = frac(seed * float3(0.1731, 0.2917, 0.3713) + float3(0.13, 0.57, 0.91)) * 64.0;
                float3 p = off / R * EVO_HAZE_CYCLES + place + float3(0.0, 1.0, 0.6) * (age * EVO_HAZE_BILLOW);

                float3 gradient;
                float lobe = EvoValueNoise(p, gradient);
                float wisp = EvoValueNoise(p * 2.3 + float3(17.1, 5.3, 9.7), gradient);
                float n = 0.65 * lobe + 0.35 * wisp;

                // The lumps: a lower noise, drifting at half the billow, cut below the puff's own threshold into empty water.
                float3 pl = off / R * EVO_HAZE_LUMP_CYCLES + place.yzx + float3(0.7, 0.4, 0.0) * (age * 0.5 * EVO_HAZE_BILLOW);
                float low = EvoValueNoise(pl, gradient);
                float gap = lerp(EVO_HAZE_GAP_LOW, EVO_HAZE_GAP_HIGH, frac(seed * 0.618034 + 0.29));
                float lump = smoothstep(gap, gap + EVO_HAZE_GAP_SOFT, low);

                // The edge pushed in and out, and the density broken: thicker in places, threads of thin water between, and holes.
                float edge = R * (1.0 + EVO_HAZE_EDGE * (2.0 * n - 1.0));
                float x = dist / max(1e-6, edge);
                float density = EVO_HAZE_GAIN * exp(-EVO_HAZE_FALL * x * x) * lump * saturate(0.25 + 1.3 * (n - 0.2));

                float window = saturate(1.0 - dist / (EVO_HAZE_REACH * R));
                window = window * window * (3.0 - 2.0 * window);

                return density * window;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 centre = TransformObjectToWorld(input.centre);
                float radius = input.data.x;

                float3 eye = GetCameraPositionWS();
                float t = unity_OrthoParams.w > 0.5 ? 0.0 : length(centre - eye);
                float pixel = max(1e-5, EvoPixelMetres(t));

                // A puff's quad holds its window; a speck's its disc, blur and edge.
                float halfSize = input.data.w < 0.5
                    ? EVO_HAZE_REACH * max(radius, 2.0 * pixel) * 1.05 + pixel
                    : SpeckHalfSize(radius, pixel);

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

                float3 m = input.centreWS;
                if (!EvoInWater(m, _BoxMin.xyz, _BoxMax.xyz, _Tank)) return half4(0, 0, 0, 0);

                float t = dot(m - origin, dir);
                float cover = 0.0;

                if (input.data.w < 0.5)
                {
                    // A puff: faded against the scene and the water's near end over a share of its radius, never cut.
                    float soft = max(0.02, EVO_HAZE_SOFT * input.data.x);
                    float behind = saturate((t1 - t) / soft);
                    float front = saturate((t - t0) / soft);

                    if (behind > 0.0 && front > 0.0)
                        cover = PuffCover(origin, dir, t, m, input.data.x, input.data.y, input.data.z) * behind * front;
                }
                else
                {
                    // A speck: the lens's focus as the flakes take it, and stopped by the scene as they are.
                    evoSnowNearT = 1e9;
                    evoSnowFocus = _EvoSnowLens.x;
                    if (_EvoSnowLens.y > 0.0 && evoSnowFocus <= 0.0)
                        evoSnowFocus = min(EvoSceneEyeDepth(float2(0.5, 0.5)), 12.0);

                    if (t > t0 && t < t1) cover = SpeckCover(origin, dir, t, m, input.data.x, input.data.y);
                }

                if (cover <= 0.0) return half4(0, 0, 0, 0);

                // The water's fog with distance, as the flakes take it: the colour kept per channel, the opacity by its luminance.
                float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(m));
                float keptGrey = dot(kept, float3(0.2126, 0.7152, 0.0722));

                float alpha = saturate(cover * input.colour.a * inside);

                // The haze at 2: the water's own colour at the centre's height, a share of it, warmed a little by the tint (the drained
                // paint, linear, near 0.1), so the murk is keyed to the water at any depth. Otherwise the vertex colour as it was.
                float3 colourHaze = _HazeKeyed > 0.5 ? EvoWaterColour(m.y) * (_HazeShare + EVO_HAZE_KEY_TINT * input.colour.rgb) : input.colour.rgb;
                return half4(colourHaze * kept * alpha, alpha * keptGrey);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
