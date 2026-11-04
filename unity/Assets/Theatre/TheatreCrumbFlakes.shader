// The bite crumbs as torn flakes (EVOSIM_THEATRE_CRUMB_FLAKES, with EVOSIM_THEATRE_BITE_CRUMBS; TheatreMouthBiteCrumbs.cs).
//
// At full size the crumbs drawn through the crumble's large-mote path (TheatreCrumbleFlakes.shader, uv1.w = 1) read as a scatter of
// identical soft round blobs. Torn kelp or flesh is not that: it comes off as flat flakes and shreds, each with its own outline, which
// tumble as they drift, flash as they turn to face the light and thin to a line edge-on. This shader draws that, behind its own dial.
//
// Each flake is a flat quad that is not camera-facing. TheatreMouthBiteCrumbs.cs tumbles it on the CPU, about a hashed axis at a hashed
// 1 to 4 Hz of the crumb's age (film seconds from the run's second), and hands the vertex stage its two in-plane half-axes in metres.
// The vertex stage lays the quad along them, a little larger than the flake so its outline, its soft edge and its blur fit, and holds
// it at least most of a pixel across on each axis and at least a pixel thick on screen when it turns edge-on; the share of the drawn
// area that is not the flake's own is taken off its opacity, so a flake seen edge-on thins to a faint line rather than broken pixels.
//
// The outline. Nothing in it is radial. The quad's own coordinates p (1 at the flake's nominal half-axes) are sheared by a hashed
// amount and measured by the square measure max(|x|, |y|) with its corners cut by a hashed share (never less than the unsheared
// square, which keeps the outline inside the quad); two octaves of value noise, each flake at its own place in the field, push that
// field up and down, and the flake is where the sum stands above a threshold. The edge is soft over about one and a half pixels of the field's own screen gradient (fwidth), widened by the lens's blur
// at the flake's real depth. A strand (about 1 crumb in 12, aspect 4 to 8) takes the square measure only, no shear, and noise finer
// across it than along it, so its long edges are ragged and its ends torn. A speck (the fine particulate, 6 to 12 a bite) is a flake
// of a fraction of a millimetre, round only because it is a pixel.
//
// The light. The sun's light at the flake (EvoSunAt, with the caustics' focusing as the large motes take it) falls on it by
// |dot(normal, toward the sun)|: lit from the side the eye sees, the tissue reflects it; lit from behind, light comes through, in the
// tissue's colour between once and twice (a translucency term, strong: thin algae). A small sheen toward the half vector makes a flake
// flash as it turns through the angle that mirrors the sun to the eye. A flake seen near edge-on is darkened a little; a band just
// inside its torn edge, where the tissue is thinnest, is lighter and more see-through.
//
// The water. The fog is the flakes' and the haze's (TheatreDeathHaze.shader): the light kept over the water between the eye and the
// flake, on the colour per channel and on the opacity by its luminance. The flake is stopped by the scene's depth at its own surface
// point and drawn only in the water, as the large motes are. Premultiplied, blended One, OneMinusSrcAlpha: tissue covers what is behind
// it, it does not add light. Both sides are drawn.
//
// Per vertex: POSITION the flake's centre in the world; uv0 the corner (-1 or 1 on each axis) and the flake's place in the noise field
// (two numbers in [0, 64)); uv1 the long half-axis in world metres (xyz) and the kind (0 a flake, 1 a strand, 2 a speck); uv2 the
// short half-axis in world metres (xyz), w unused; COLOR the tissue's colour (in the space the shader mixes it in), opacity in alpha.

Shader "Evosim/Theatre Crumb Flakes"
{
    Properties
    {
        // The light on a flake: the share it shows unlit by the sun's direction, the sun's share facing it, the light through it from
        // behind, and the flash toward the half vector with its tightness.
        _FlakeAmbient("Ambient share", Range(0, 2)) = 0.35
        _FlakeDiffuse("Facing the sun", Range(0, 4)) = 1.0
        _FlakeThrough("Through it from behind", Range(0, 4)) = 1.3
        _FlakeSheen("Flash toward the sun's mirror", Range(0, 4)) = 0.5
        _FlakeSheenPower("Flash tightness", Range(2, 256)) = 24
        _FlakeGain("Brightness", Range(0, 4)) = 1.0

        // Copied from the snow shells' material each time the flakes are built: the water the shells sit in.
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
            "Queue" = "Transparent+12"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CrumbFlakes"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
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
            #include "TheatreSnow.hlsl"

            // How far the quad reaches, in the flake's nominal half-axes: the outline can stand out to about 1.2 of them.
            #define EVO_FLAKE_REACH 1.25

            // The outline's field: 1 less the measure, plus the noise less a half times the rag, cut at EVO_FLAKE_CUT.
            #define EVO_FLAKE_CUT 0.35
            #define EVO_FLAKE_RAG 1.1
            #define EVO_STRAND_RAG 0.75

            // The noise's cycles across a nominal half-axis: a flake's, and a strand's along and across it.
            #define EVO_FLAKE_CYCLES 1.6
            #define EVO_STRAND_ALONG 1.0
            #define EVO_STRAND_ACROSS 2.4

            // The most a flake is sheared before it is measured, either way.
            #define EVO_FLAKE_SKEW 0.35

            // The soft edge in pixels of the field's own screen gradient, before the lens's blur.
            #define EVO_FLAKE_EDGE_PIXELS 1.5

            // The band inside the torn edge, where the tissue is thinnest: its width in the field's units, how much lighter it is at
            // the edge, and its opacity there as a share of the face's. Thin algae lets more light through at a tear, not less. The
            // output is premultiplied, so the light over the thinning (1.6 x 0.75 = 1.2) is what the edge adds over the face.
            #define EVO_FLAKE_RIM 0.18
            #define EVO_FLAKE_RIM_LIGHT 1.6
            #define EVO_FLAKE_RIM_THIN 0.75

            // Of the light through a flake from behind, the share in the tissue's colour once (thin tissue) rather than twice.
            #define EVO_FLAKE_THROUGH_THIN 0.5

            // Edge-on: the shade at |dot(normal, eye)| = 0, back to 1 by EVO_FLAKE_EDGE_ON.
            #define EVO_FLAKE_EDGE_ON_DARK 0.65
            #define EVO_FLAKE_EDGE_ON 0.4

            // The least a flake is drawn on screen: its half-axes most of a pixel, its thickness edge-on a pixel.
            #define EVO_FLAKE_LEAST_HALF 0.75
            #define EVO_FLAKE_LEAST_THICK 1.0

            CBUFFER_START(UnityPerMaterial)
                float _FlakeAmbient;
                float _FlakeDiffuse;
                float _FlakeThrough;
                float _FlakeSheen;
                float _FlakeSheenPower;
                float _FlakeGain;
                float4 _BoxMin;
                float4 _BoxMax;
                float4 _Tank;
                float _Waterline;
            CBUFFER_END

            struct Attributes
            {
                float3 centre : POSITION;
                float4 corner : TEXCOORD0;
                float4 along  : TEXCOORD1;
                float4 across : TEXCOORD2;
                float4 colour : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centreWS   : TEXCOORD1;

                // xy: the flake's own coordinates (1 at its nominal half-axes); zw: its place in the noise field.
                float4 p          : TEXCOORD2;

                // xyz: the flake's true normal; w: the share of its drawn area that is its own (the least size and the edge-on hold).
                float4 normal     : TEXCOORD3;

                // x: how far the corners are cut; y: the shear; z: the kind; w unused.
                float4 shape      : TEXCOORD4;

                float4 colour     : TEXCOORD5;
            };

            // The most the lens blurs a flake, in metres at a pixel's size: the shells' cap in pixels of a 1080-line frame (BigMote's).
            float FlakeBlurMost(float pixel)
            {
                return _EvoSnowLens.y > 0.0 ? _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0 : 0.0;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 centre = TransformObjectToWorld(input.centre);
                float kind = input.along.w;

                // The flake's half-axes and the plane they span. Every vertex of a flake carries the same numbers, so every line below
                // gives the four of them the same result.
                float a = max(length(input.along.xyz), 1e-7);
                float b = max(length(input.across.xyz), 1e-7);
                float3 uDir = input.along.xyz / a;
                float3 vDir = input.across.xyz / b;
                float3 normal = normalize(cross(uDir, vDir));

                bool ortho = unity_OrthoParams.w > 0.5;
                float3 eye = GetCameraPositionWS();
                float3 toEye = ortho ? normalize(UNITY_MATRIX_V[2].xyz) : normalize(eye - centre);
                float t = ortho ? 0.0 : length(centre - eye);
                float pixel = max(1e-5, EvoPixelMetres(t));

                // At least most of a pixel on each axis; the light it then lacks is taken off its opacity.
                float aDraw = max(a, EVO_FLAKE_LEAST_HALF * pixel);
                float bDraw = max(b, EVO_FLAKE_LEAST_HALF * pixel);
                float keep = (a * b) / (aDraw * bDraw);

                // Edge-on: the plane is turned toward the eye just far enough to stay a pixel thick, and its opacity scaled by the true
                // projected area over the drawn one, so it thins to a faint line.
                float facing = dot(normal, toEye);
                float least = min(0.9, EVO_FLAKE_LEAST_THICK * pixel / (EVO_FLAKE_REACH * min(aDraw, bDraw)));

                if (abs(facing) < least)
                {
                    float3 side = normal - toEye * facing;
                    float len = length(side);
                    side = len > 1e-5 ? side / len : vDir;

                    float sgn = facing >= 0.0 ? 1.0 : -1.0;
                    float3 drawn = side * sqrt(1.0 - least * least) + toEye * (least * sgn);

                    uDir = normalize(uDir - drawn * dot(uDir, drawn));
                    vDir = cross(drawn, uDir);
                    keep *= abs(facing) / least;
                }

                // The quad: the outline's reach, plus a soft edge of two pixels and the lens's blur at its greatest.
                float margin = 2.0 * pixel + FlakeBlurMost(pixel);
                float spanA = EVO_FLAKE_REACH * aDraw + margin;
                float spanB = EVO_FLAKE_REACH * bDraw + margin;

                float3 corner = centre + uDir * (input.corner.x * spanA) + vDir * (input.corner.y * spanB);

                output.positionWS = corner;
                output.positionCS = TransformWorldToHClip(corner);
                output.centreWS = centre;

                // The flake's own coordinates: affine across the planar quad, so the interpolation carries them exactly.
                output.p = float4(input.corner.x * spanA / aDraw, input.corner.y * spanB / bDraw, input.corner.z, input.corner.w);
                output.normal = float4(normal, saturate(keep));

                // The shape's two numbers from the flake's place in the field, here where all four vertices take the same inputs.
                float blend = kind > 0.5 && kind < 1.5 ? 0.0 : frac(input.corner.w * 0.6180339);
                float skew = kind > 0.5 && kind < 1.5 ? 0.0 : (2.0 * frac(input.corner.z * 0.7548777) - 1.0) * EVO_FLAKE_SKEW;
                output.shape = float4(blend, skew, kind, 0.0);

                output.colour = input.colour;

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // The outline's field and its screen gradient, first, in uniform control flow.
                float2 p = input.p.xy;
                float strand = input.shape.z > 0.5 && input.shape.z < 1.5 ? 1.0 : 0.0;

                // The measure: the sheared square with its corners cut by the hashed blend, never less than the unsheared square,
                // so the outline stays inside EVO_FLAKE_REACH whatever the noise and the shear do.
                float2 q = float2(p.x + input.shape.y * p.y, p.y);
                float square = max(abs(q.x), abs(q.y));
                float cut = square + 0.5 * min(abs(q.x), abs(q.y));
                float measure = max(lerp(square, cut, input.shape.x), max(abs(p.x), abs(p.y)));

                float2 cycles = strand > 0.5 ? float2(EVO_STRAND_ALONG, EVO_STRAND_ACROSS) : float2(EVO_FLAKE_CYCLES, EVO_FLAKE_CYCLES);
                float3 at = float3(p * cycles + input.p.zw, 0.37 * input.p.z + 0.61 * input.p.w);

                float3 gradient;
                float lobe = EvoValueNoise(at, gradient);
                float tear = EvoValueNoise(at * 2.3 + float3(17.1, 5.3, 9.7), gradient);
                float noise = 0.65 * lobe + 0.35 * tear;

                float rag = strand > 0.5 ? EVO_STRAND_RAG : EVO_FLAKE_RAG;
                float field = 1.0 - measure + rag * (noise - 0.5);
                float perPixel = max(fwidth(field), 1e-5);

                float inside = EvoSeenFromBelow(_Waterline);
                if (inside <= 0.002) return half4(0, 0, 0, 0);

                float3 origin, dir;
                float tScene;
                EvoViewRay(input.positionWS, input.positionCS, origin, dir, tScene);

                float t0 = unity_OrthoParams.w > 0.5 ? 0.0 : _ProjectionParams.y;
                float t1 = tScene;

                if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1)) return half4(0, 0, 0, 0);

                // Stopped by the scene at the flake's own surface point, and drawn only in the water.
                float3 m = input.centreWS;
                float t = dot(input.positionWS - origin, dir);
                if (!(t > t0 && t < t1)) return half4(0, 0, 0, 0);
                if (!EvoInWater(m, _BoxMin.xyz, _BoxMax.xyz, _Tank)) return half4(0, 0, 0, 0);

                // The lens: the large motes' circle of confusion at the flake's real depth, capped as the shells cap it.
                float pixel = max(1e-5, EvoPixelMetres(t));
                float blurPixels = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    float focus = _EvoSnowLens.x > 0.0 ? _EvoSnowLens.x : min(EvoSceneEyeDepth(float2(0.5, 0.5)), 12.0);
                    focus = max(0.05, focus);
                    float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                    float blur = min(0.5 * _EvoSnowLens.y * abs(depth - focus) / focus, FlakeBlurMost(pixel));
                    blurPixels = blur / pixel;
                }

                // The outline: above the cut, with a soft edge of a pixel and a half and the blur.
                float d = field - EVO_FLAKE_CUT;
                float soft = perPixel * (EVO_FLAKE_EDGE_PIXELS + blurPixels);
                float cover = saturate(d / soft + 0.5);
                cover = cover * cover * (3.0 - 2.0 * cover);
                if (cover <= 0.0) return half4(0, 0, 0, 0);

                // The light: the sun at the flake, focused by the caustics as the large motes take it.
                float time = EvoTime();
                float caustic = min(EvoCausticGain(m, time), 7.0);
                float3 sun = EvoSunAt(m) * (0.3 + 0.7 * caustic);

                float3 normal = input.normal.xyz;
                float3 toSun = -EvoSunRayWS();
                float3 toEye = -dir;
                float lit = dot(normal, toSun);
                float seen = dot(normal, toEye);
                float facingSun = abs(lit);
                float front = lit * seen >= 0.0 ? 1.0 : 0.0;

                float3 tissue = input.colour.rgb;
                float3 shade;

                if (input.shape.z > 1.5)
                {
                    // A speck: lit by how it faces the sun, either side; no sheen, nothing through it worth the name.
                    shade = tissue * sun * (_FlakeAmbient + _FlakeDiffuse * facingSun);
                }
                else
                {
                    float3 halfway = normalize(toSun + toEye);
                    float flash = pow(saturate(abs(dot(normal, halfway))), _FlakeSheenPower) * _FlakeSheen;

                    shade = tissue * sun * (_FlakeAmbient + _FlakeDiffuse * facingSun * front)
                          + lerp(tissue * tissue, tissue, EVO_FLAKE_THROUGH_THIN) * sun * (_FlakeThrough * facingSun * (1.0 - front))
                          + sun * (flash * front);
                }

                // Darker edge-on; lighter and thinner in a band just inside the torn edge, where the tissue is thinnest.
                float edgeOn = lerp(EVO_FLAKE_EDGE_ON_DARK, 1.0, saturate(abs(seen) / EVO_FLAKE_EDGE_ON));
                float rimAt = saturate(d / EVO_FLAKE_RIM);
                float rim = lerp(EVO_FLAKE_RIM_LIGHT, 1.0, rimAt);
                float rimThin = lerp(EVO_FLAKE_RIM_THIN, 1.0, rimAt);
                shade *= _FlakeGain * edgeOn * rim;

                // The water's fog with distance, as the haze takes it: the colour kept per channel, the opacity by its luminance.
                float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(m));
                float keptGrey = dot(kept, float3(0.2126, 0.7152, 0.0722));

                float alpha = saturate(cover * rimThin * input.colour.a * input.normal.w * inside);
                return half4(shade * kept * alpha, alpha * keptGrey);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
