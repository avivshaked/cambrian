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
            // The water edge's fifth cut (TheatreSkin, on with any of the edge's dials): without it the shader compiles as it did.
            #pragma multi_compile _ _EVO_WATER_EDGE5

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

            // EVOSIM_THEATRE_WATER_EDGE (TheatreSkin.WaterEdge): 0 draws the ceiling as before, 1 is the full
            // take-A edge. A global, never set while the dial is off, so it stays the zero a global is.
            float _EvoWaterEdge;
            float _EvoWaterWindow;
            float _EvoWaterGlow;
            float _EvoWaterEdgeProbe;
#if defined(_EVO_WATER_EDGE5)
            // EVOSIM_THEATRE_WATER_FACET (TheatreSkin.WaterFacet): the facets' hit cone in degrees, zero for EDGE_FACET_HIT.
            float _EvoWaterFacet;
#endif

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

            // ---------------------------------------------------------------------------------
            // The water's edge after the dive's take A (EVOSIM_THEATRE_WATER_EDGE, the owner's ask of
            // 2026-11-04; scratch/theatre-realism/water-edge-plan.md). Looking up from a metre or two,
            // the window is cut into irregular patches of sky and mirror, stretched across the wind, and
            // the sun into sparks. EvoRipple's three trains reach 16 degrees at most, on cells no shorter
            // than 0.43 m, so more slope is added here, to the ceiling's normal only: the caustics, the
            // shafts and the net on a body read EvoRipple and do not change.
            //
            // Built the way take A's sea is (scripts/channel/sea_still.py): the patches from long trains,
            // and the fine roughness from gradient noise, never from short sines, because any few sines
            // at fixed headings beat into a lattice (phase136's first cut, six sines from 35 cm to 4 cm,
            // tiled the whole ceiling like scales). Four long trains, 2 m to 0.5 m, run within 50
            // degrees of EvoRipple's main heading, the wind, at no simple angle to each other and at
            // their own phases, and carry most of the slope. Four octaves of noise, 32 cm to 3 cm,
            // stretched 1.8 times across the wind so their crests run across it, only roughen: equal
            // slope each, as in the saturation range (Phillips), the first three a third of the slope
            // variance and the 3 cm one, the sparks', the same again. The noise drifts down the wind at
            // the capillary-gravity speed of its octave and evolves through its third coordinate, as
            // take A's does, but slowly (EDGE_EVOLVE), and every speed is slowed by the same dial as the
            // swell.
            //
            // At 1 the rms slope of the trains and the first three octaves is 0.17 (EDGE_RMS), Cox and
            // Munk's at a 5 m/s wind; the sparks' octave lifts it to 0.19 at its full slope (tools/
            // edge_noise.py), about 0.18 at EDGE_FINE_SHARE (scaled from that measure, not measured), and
            // EvoRipple's swell rides on top, so the whole sits a little past a real sea (D262). The
            // first cut's rms was 0.17 as well: what failed was its pattern, not its steepness. Each part
            // fades out once a pixel's footprint covers a quarter of its length, and all of it falls to
            // 40% between 3 and 10 m from the eye, where take A's far ceiling is calm and the first cut's
            // crawled.
            #define EDGE_RMS 0.17

            // A hash of a cell to three numbers in [-1, 1]: Hoskins's hash without sine, because a sine
            // hash loses its bits at the thousands of cells a 200 m box spans, and integer hashes need a
            // shader target past this one's 3.0.
            float3 EdgeHash(float3 cell)
            {
                float3 p = frac(cell * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return 2.0 * frac((p.xxy + p.yxx) * p.zyx) - 1.0;
            }

            // Gradient noise in three dimensions with its derivatives (after Quilez): .x the value,
            // .yzw the gradient. The quintic fade keeps the gradient continuous across cells, so the
            // normal has no seams.
            float4 EdgeNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = p - i;
                float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float3 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

                float3 ga = EdgeHash(i + float3(0, 0, 0)), gb = EdgeHash(i + float3(1, 0, 0));
                float3 gc = EdgeHash(i + float3(0, 1, 0)), gd = EdgeHash(i + float3(1, 1, 0));
                float3 ge = EdgeHash(i + float3(0, 0, 1)), gf = EdgeHash(i + float3(1, 0, 1));
                float3 gg = EdgeHash(i + float3(0, 1, 1)), gh = EdgeHash(i + float3(1, 1, 1));

                float va = dot(ga, f - float3(0, 0, 0)), vb = dot(gb, f - float3(1, 0, 0));
                float vc = dot(gc, f - float3(0, 1, 0)), vd = dot(gd, f - float3(1, 1, 0));
                float ve = dot(ge, f - float3(0, 0, 1)), vf = dot(gf, f - float3(1, 0, 1));
                float vg = dot(gg, f - float3(0, 1, 1)), vh = dot(gh, f - float3(1, 1, 1));

                float k1 = vb - va, k2 = vc - va, k3 = ve - va, k4 = va - vb - vc + vd;
                float k5 = va - vc - ve + vg, k6 = va - vb - ve + vf, k7 = -va + vb + vc - vd + ve - vf - vg + vh;
                float value = va + u.x * k1 + u.y * k2 + u.z * k3 + u.x * u.y * k4 + u.y * u.z * k5 + u.z * u.x * k6 + u.x * u.y * u.z * k7;

                float3 g1 = gb - ga, g2 = gc - ga, g3 = ge - ga, g4 = ga - gb - gc + gd;
                float3 g5 = ga - gc - ge + gg, g6 = ga - gb - ge + gf, g7 = -ga + gb + gc - gd + ge - gf - gg + gh;
                float3 gradient = ga + u.x * g1 + u.y * g2 + u.z * g3 + u.x * u.y * g4 + u.y * u.z * g5 + u.z * u.x * g6 + u.x * u.y * u.z * g7
                    + du * float3(k1 + u.y * k4 + u.z * k6 + u.y * u.z * k7,
                                  k2 + u.z * k5 + u.x * k4 + u.z * u.x * k7,
                                  k3 + u.x * k6 + u.y * k5 + u.x * u.y * k7);
                return float4(value, gradient);
            }

            // The phase speed of a capillary-gravity wave L long: c^2 = g L / 2 pi + 2 pi (sigma / rho) / L.
            float EdgeWaveSpeed(float metres)
            {
                return EvoWaveSpeed() * sqrt(9.81 * metres / 6.2831853 + 6.2831853 * 7.4e-5 / metres);
            }

            float3 EdgeNormal(float2 xz, float time, float edge, float footprint, float reach)
            {
                float2 slope;
                float curvature;
                EvoRipple(xz, time, slope, curvature);

                // EvoRipple's main heading is the wind's.
                const float2 wind = float2(0.99503, 0.09950);
                const float2 across = float2(-0.09950, 0.99503);
                float far = lerp(1.0, 0.4, smoothstep(3.0, 10.0, reach));

                // The long trains: two thirds of the variance over four, so each carries a sine's
                // amplitude of EDGE_RMS * sqrt(2/3 * 2/4).
                float trainSlope = EDGE_RMS * 0.57735 * edge * far;
                [unroll]
                for (int train = 0; train < 4; train++)
                {
                    // Spelled out, like EvoRipple's, rather than read from an array.
                    float metres = train == 0 ? 2.0 : (train == 1 ? 1.3 : (train == 2 ? 0.8 : 0.5));
                    float turn = train == 0 ? 0.0 : (train == 1 ? 0.541 : (train == 2 ? -0.401 : 0.857));
                    float start = train == 0 ? 1.7 : (train == 1 ? 4.1 : (train == 2 ? 0.6 : 2.9));
                    float2 heading = cos(turn) * wind + sin(turn) * across;

                    float k = 6.2831853 / metres;
                    float seen = 1.0 - smoothstep(0.1, 0.25, footprint / metres);
                    slope += (trainSlope * seen * cos(k * (dot(heading, xz) - EdgeWaveSpeed(metres) * time) + start)) * heading;
                }

                // The noise: a third of the variance over three octaves. EDGE_NOISE_RMS is the rms of
                // the noise's gradient along one axis, measured (tools/edge_noise.py), so that an
                // octave's height of noiseSlope * metres / EDGE_NOISE_RMS gives an rms slope of
                // noiseSlope along the wind.
                // The fourth octave, 3 cm, is the sparks' (phase137's close eye showed big smooth
                // patches and a sun in three soft blobs; take A's sparks come from its 2 cm ripples):
                // the same slope again, so Phillips's equal share per octave holds, and gone by the
                // footprint wherever a pixel covers more than a quarter of it.
                #define EDGE_NOISE_RMS 0.50
                // The noise evolves at this share of the rate it drifts, so the near ceiling moves as a
                // whole rather than boiling (phase137's close eye crawled at twice take A's share).
                #define EDGE_EVOLVE 0.15
                // The sparks' octave at this share of its slope and of its pace: at full, a 3 cm cell drifts its own
                // length in a tenth of a second, and from a metre and a half the near ceiling crawled at three times
                // take A's share (phase139). Weaker as well as slower, so the sparks stay fine but fewer flicker.
                #define EDGE_FINE_SHARE 0.7
                #define EDGE_FINE_PACE 0.5
#if defined(_EVO_WATER_EDGE5)
                // The finer facets, a probe (EVOSIM_THEATRE_WATER_EDGE_PROBE 4): the 3 cm octave at its full slope and a fifth of
                // 1.5 cm at EDGE_FINE_SHARE and EDGE_FINE_PACE, gone wherever a pixel covers a quarter of it. Take A's patches have
                // about four times the films' share of steep edges (phase146: the 90th percentile of the luma gradient 11.4
                // against 2.8), but its shards are large because its facets are, so the finer facets are read apart from the
                // seventh cut's sun.
                bool finer = _EvoWaterEdgeProbe > 3.5;
#endif
                float noiseSlope = EDGE_RMS * 0.33333 * edge * far;
                [unroll]
#if defined(_EVO_WATER_EDGE5)
                for (int octave = 0; octave < 5; octave++)
#else
                for (int octave = 0; octave < 4; octave++)
#endif
                {
#if defined(_EVO_WATER_EDGE5)
                    if (octave == 4 && !finer) break;
                    float metres = octave == 0 ? 0.32 : (octave == 1 ? 0.16 : (octave == 2 ? 0.08 : (octave == 3 ? 0.03 : 0.015)));
                    float share = octave == 4 ? EDGE_FINE_SHARE : (octave == 3 && !finer ? EDGE_FINE_SHARE : 1.0);
                    float pace = octave >= 3 ? EDGE_FINE_PACE : 1.0;
#else
                    float metres = octave == 0 ? 0.32 : (octave == 1 ? 0.16 : (octave == 2 ? 0.08 : 0.03));
                    float share = octave == 3 ? EDGE_FINE_SHARE : 1.0;
                    float pace = octave == 3 ? EDGE_FINE_PACE : 1.0;
#endif
                    float seen = 1.0 - smoothstep(0.1, 0.25, footprint / metres);
                    float along = (dot(wind, xz) - pace * EdgeWaveSpeed(2.0 * metres) * time) / metres;
                    float side = dot(across, xz) / (1.8 * metres);
                    // Each octave evolves at its own rate and from its own place in the noise.
                    float3 p = float3(along, side, EDGE_EVOLVE * pace * time * EdgeWaveSpeed(2.0 * metres) / (2.0 * metres)) + 37.0 * octave;
                    float4 n = EdgeNoise(p);
                    float height = share * noiseSlope * seen * metres / EDGE_NOISE_RMS;
                    slope += height * (n.y / metres * wind + n.z / (1.8 * metres) * across);
                }

                return normalize(float3(-slope.x, 1.0, -slope.y));
            }

            // The sun under the edge, the light a facet sends toward the eye from a ray `angle` off the sun's
            // centre, with `disc` the plain window's for the same ray. At full the disc is a fine core, a
            // gaussian EDGE_SPARK_CORE degrees wide holding most of the sun's light, and a soft halo
            // EDGE_SPARK_HALO wide holding the rest, so a facet that catches it is a fine spark that fades out
            // rather than a cut-out: the third cut's 0.4 degree disc carried 64 times the sun's light in a hard
            // edge and clipped to flat white shapes (phase139). The whole holds the plain disc's light, the
            // area of a disc `radius` wide; a dull day widens all three alike.
            #define EDGE_SPARK_CORE 0.12
            #define EDGE_SPARK_HALO 0.5
            #define EDGE_SPARK_HALO_SHARE 0.15
            float EdgeSun(float angle, float disc, float radiusDegrees, float widen)
            {
                float r = radians(radiusDegrees);
                float core = radians(EDGE_SPARK_CORE * widen);
                float halo = radians(EDGE_SPARK_HALO * widen);
                float a2 = angle * angle;
                float spark = r * r * ((1.0 - EDGE_SPARK_HALO_SHARE) * exp(-0.5 * a2 / (core * core)) / (2.0 * core * core)
                    + EDGE_SPARK_HALO_SHARE * exp(-0.5 * a2 / (halo * halo)) / (2.0 * halo * halo));
                return lerp(disc, spark, _EvoWaterEdge);
            }

#if defined(_EVO_WATER_EDGE5)
            // The sun's facets under the edge (the seventh cut): a pixel shows the sun where its own refracted ray, taken through the
            // rippled normal, comes within the hit cone of the sun, EDGE_FACET_HIT degrees or the EVOSIM_THEATRE_WATER_FACET dial,
            // hard-edged to a pixel and at EDGE_FACET_GAIN times the sun's light, so a lit facet clips to white as take A's shards
            // do. Each shard is then the shape of the facet that catches the sun: irregular, travelling with its wave, never
            // redrawn. The sixth cut's shards were cells of their own, and came out as tiny dim dashes that did not carry the sun
            // without the kept blob under them (phase146). `width` is the angle's change across a pixel, taken outside every
            // branch: the edge is that wide, so where a pixel spans many facets they blur to their share of the cone rather than
            // alias. Started strong (D262): the fourth cut's soft core 0.12 degrees wide already drew pieces a hundred pixels
            // across (phase142), so a cone several times that, hard and saturated, overshoots.
            #define EDGE_FACET_HIT 0.6
            #define EDGE_FACET_GAIN 4.0
            float EdgeFacets(float angle, float width)
            {
                float hit = radians(_EvoWaterFacet > 0.0 ? _EvoWaterFacet : EDGE_FACET_HIT);
                float edge = max(0.5 * width, 1e-5);
                return (1.0 - smoothstep(hit - edge, hit + edge, angle)) * EDGE_FACET_GAIN;
            }

            // The share of the fourth cut's sun (EdgeSun) kept under the facets, and none under the probe's 3
            // (EVOSIM_THEATRE_WATER_EDGE_PROBE), to see whether the shards carry the sun.
            #define EDGE_SUN_KEEP 0.15
#endif

            // The sun's glow in the water above the eye (EVOSIM_THEATRE_WATER_GLOW, 0 by default): the light
            // the water scatters forward makes a soft warm halo round the sun's place in Snell's window, as in
            // take A. It is taken on the eye's own ray against the sun's refracted direction, never through
            // the rippled facets, so the ripples do not break it into pieces, and the caller lays it on the
            // window's facets alone, so the mirror's patches stay dark as take A's do. One tight part 4 degrees
            // wide: the fourth cut's broad one, 15 wide, veiled the whole frame grey (phase142). Warmer than the
            // sun's own colour, as take A's is. Started strong (D262).
            float3 EdgeGlow(float3 incident)
            {
                float3 down = refract(-EvoSunDirectionWS(), float3(0.0, 1.0, 0.0), 1.0 / 1.333);
                float3 toward = dot(down, down) > 1e-5 ? -normalize(down) : float3(0.0, 1.0, 0.0);
                float angle = acos(clamp(dot(incident, toward), -1.0, 1.0));
#if defined(_EVO_WATER_EDGE5)
                // At 0.40 it veiled the sun's place milky white and the shards were lost in it (phase146): back to 0.20, and
                // warmer, as take A's yellow-white halo is.
                float halo = 0.20 * exp(-angle / radians(4.0));
                return _SunColor.rgb * float3(1.0, 0.86, 0.60) * (_SunStrength * _EvoWaterGlow * halo);
#else
                float halo = 0.20 * exp(-angle / radians(4.0)) + 0.08 * exp(-angle / radians(15.0));
                return _SunColor.rgb * float3(1.0, 0.86, 0.66) * (_SunStrength * _EvoWaterGlow * halo);
#endif
            }


            // Water's index for one channel under the edge: the spread from 1.331 in the red to 1.338
            // in the blue, cut to a fifth at full. Over the second cut's many rims the full spread drew
            // a grey-tan fringe round every patch that read as dirt (phase137), where real dispersion
            // at a 0.35 degree split is a trace.
            float EdgeIndex(float index) { return 1.333 + (index - 1.333) * (1.0 - 0.8 * _EvoWaterEdge); }

            // The window's light through the water above the eye (EVOSIM_THEATRE_WATER_WINDOW, 0 by
            // default). The sky's light comes down through the metres between the surface and the eye,
            // and a metre or two of water takes most of the red out of it: that is why take A's window
            // is blue-cyan and the theatre's, kept near grey inside the fade's start, is not. Per metre,
            // take A's red (scripts/channel/sea_still.py WATER_ABSORB) with pure water's green and blue
            // (Pope and Fry 1997, 0.051 at 545 nm and 0.010 at 465 nm): take A's own green and
            // blue, 0.085 and 0.055, made the window too dark and too green-teal from 5 m (phase139).
            // Over the part of the path the look's own fade does not already cover, so no metre is
            // counted twice.
            float3 WindowKept(float3 positionWS)
            {
                float d = EvoWaterPathMetres(positionWS);
                float covered = EvoLookFade() ? EvoFadeMetres(d) : (EvoLookWater() ? d : 0.0);
                return exp(-float3(0.40, 0.051, 0.010) * (_EvoWaterWindow * max(d - covered, 0.0)));
            }

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

            // facetWidth, the sun angle's change across the pixel, is the facets' alone.
#if defined(_EVO_WATER_EDGE5)
            float3 SkyDay(float3 incident, float3 n, float3 kept, float facetWidth)
#else
            float3 SkyDay(float3 incident, float3 n, float3 kept)
#endif
            {
                float overcast = saturate(_EvoDaylight.w);
                float day = EvoDayGain();

                // Water's index runs from about 1.331 in the red to 1.338 in the blue.
                float3 index = float3(1.331, 1.333, 1.338);
                [branch] if (_EvoWaterEdge > 0.0)
                    index = float3(EdgeIndex(1.331), 1.333, EdgeIndex(1.338));
                // The probe's 2 (EVOSIM_THEATRE_WATER_EDGE_PROBE): no split of the colours at the rim.
#if defined(_EVO_WATER_EDGE5)
                [branch] if (_EvoWaterEdgeProbe > 1.5 && _EvoWaterEdgeProbe < 2.5)
#else
                [branch] if (_EvoWaterEdgeProbe > 1.5)
#endif
                    index = float3(1.333, 1.333, 1.333);
                float3 mirrored = float3(
                    WindowReflects(incident, n, index.x),
                    WindowReflects(incident, n, index.y),
                    WindowReflects(incident, n, index.z));

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
                    float field = Clouds(through, _Time.y);
                    float cover = smoothstep(0.72 - 0.45 * overcast, 0.90 - 0.40 * overcast, field);
                    cover = max(cover, 0.85 * overcast * overcast);
                    // The probe's 1 and 2 (EVOSIM_THEATRE_WATER_EDGE_PROBE): no clouds.
#if defined(_EVO_WATER_EDGE5)
                    [branch] if (_EvoWaterEdgeProbe > 0.5 && _EvoWaterEdgeProbe < 2.5)
#else
                    [branch] if (_EvoWaterEdgeProbe > 0.5)
#endif
                        cover = 0.0;

                    float luma = dot(sky, float3(0.2126, 0.7152, 0.0722));
                    float3 cloud = luma * float3(0.94, 0.97, 1.0) * lerp(1.05, 0.55, overcast);

#if defined(_EVO_WATER_EDGE5)
                    // Under the water edge the clouds low over the horizon, which Snell's window squeezes into its
                    // rim, are half the sky's own colour and fade out toward it: at full they drew tan blotches
                    // at the rim (phase142's probe), where real ones are lost in the haze over the horizon.
                    [branch] if (_EvoWaterEdge > 0.0)
                    {
                        cloud = lerp(cloud, sky, 0.5 * _EvoWaterEdge);
                        cover *= lerp(1.0, smoothstep(0.05, 0.45, through.y), _EvoWaterEdge);
                    }
#endif
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
                    // Under the water edge the disc is a faint share of the fine core and soft halo, under the facets.
#if defined(_EVO_WATER_EDGE5)
                    [branch] if (_EvoWaterEdge > 0.0)
                    {
                        float edgeDisc = EdgeSun(angle, disc, _SunDegrees * (1.0 + 2.5 * overcast), 1.0 + 2.5 * overcast);
                        float glints = EdgeFacets(angle, facetWidth);
                        float keep = _EvoWaterEdgeProbe > 2.5 && _EvoWaterEdgeProbe < 3.5 ? 0.0 : EDGE_SUN_KEEP;
                        disc = lerp(disc, max(keep * edgeDisc, glints), _EvoWaterEdge);
                    }
#else
                    [branch] if (_EvoWaterEdge > 0.0)
                        disc = EdgeSun(angle, disc, _SunDegrees * (1.0 + 2.5 * overcast), 1.0 + 2.5 * overcast);
#endif
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

                // Under the water edge the mirror takes a little of the lit water just under the surface,
                // which the cells of mirror show at every angle; EDGE_MIRROR_LIFT of it, since take A's
                // patches are near black against a bright window, and the third cut's 0.35 drew them
                // grey-teal against a grey one (phase139).
                #define EDGE_MIRROR_LIFT 0.12
                [branch] if (_EvoWaterEdge > 0.0)
                    mirror = lerp(mirror, EvoWaterColour(EvoWaterlineY()) * 1.3, EDGE_MIRROR_LIFT * _EvoWaterEdge);

                return lerp(sky * day * kept, mirror * day, mirrored);
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

                // The water edge's fine trains. The footprint is taken outside the branch, because a
                // gradient inside flow control is one a compiler is entitled to refuse.
                float2 across = fwidth(input.positionWS.xz);
                float footprint = max(across.x, across.y);
                [branch] if (_EvoWaterEdge > 0.0)
                    n = EdgeNormal(input.positionWS.xz, _Time.y, _EvoWaterEdge, footprint, length(input.positionWS.xz - eye.xz));

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
#if defined(_EVO_WATER_EDGE5)
                // The sun angle of the ray that gets through, and its change across the pixel, for the facets: taken here,
                // outside every branch, because a gradient inside flow control is one a compiler may refuse.
                float3 facetThrough = caught > 1e-5 ? through * rsqrt(max(caught, 1e-5)) : float3(0.0, -1.0, 0.0);
                float facetWidth = fwidth(acos(clamp(dot(facetThrough, EvoSunDirectionWS()), -1.0, 1.0)));
#endif

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
#if defined(_EVO_WATER_EDGE5)
                    [branch] if (_EvoWaterEdge > 0.0)
                    {
                        float edgeDisc = EdgeSun(angle, disc, _SunDegrees, 1.0);
                        float glints = EdgeFacets(angle, facetWidth);
                        float keep = _EvoWaterEdgeProbe > 2.5 && _EvoWaterEdgeProbe < 3.5 ? 0.0 : EDGE_SUN_KEEP;
                        disc = lerp(disc, max(keep * edgeDisc, glints), _EvoWaterEdge);
                    }
#else
                    [branch] if (_EvoWaterEdge > 0.0)
                        disc = EdgeSun(angle, disc, _SunDegrees, 1.0);
#endif

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
                [branch] if (_EvoWaterEdge > 0.0)
                    mirror = lerp(mirror, EvoWaterColour(EvoWaterlineY()) * 1.3, EDGE_MIRROR_LIFT * _EvoWaterEdge);

                float3 kept = float3(1.0, 1.0, 1.0);
                [branch] if (_EvoWaterWindow > 0.0) kept = WindowKept(input.positionWS);

                float3 colour = lerp(sky * kept, mirror, mirrored);

#if defined(_EVO_WATER_EDGE5)
                if (EvoLookSky()) colour = SkyDay(incident, n, kept, facetWidth);
#else
                if (EvoLookSky()) colour = SkyDay(incident, n, kept);
#endif

                // The sun's glow in the water above the eye, on the window's facets alone (the share of the light a
                // facet lets through), so the mirror's patches stay dark; dimmed with the day as the sky's sun is.
#if defined(_EVO_WATER_EDGE5)
                [branch] if (_EvoWaterGlow > 0.0)
                    colour += EdgeGlow(incident) * (1.0 - WindowReflects(incident, n, 1.333)) *
                        (EvoLookSky() ? EvoDayGain() * lerp(1.0, 0.3, saturate(_EvoDaylight.w)) : 1.0);
#else
                [branch] if (_EvoWaterGlow > 0.0)
                    colour += EdgeGlow(incident) * (EvoLookSky() ? EvoDayGain() * lerp(1.0, 0.3, saturate(_EvoDaylight.w)) : 1.0);
#endif

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
