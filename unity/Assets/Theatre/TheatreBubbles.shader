// The birth cloud's bubbles (EVOSIM_THEATRE_BIRTH_BUBBLES, TheatreBirthBubbles.cs): air in water, seen from the water.
//
// Each bubble is a camera-facing quad of the birth cloud's mesh, large enough to hold the bubble, its blur and its soft edge. Its
// fragment sends the view ray through the pixel and finds the ray's nearest approach to the bubble's centre, as the crumble's flakes
// do (TheatreCrumbleFlakes.shader); the distance there over the radius places the pixel on a sphere impostor and gives its normal.
//
// What a bubble shows. Light leaving water for air is totally reflected beyond the critical angle, asin(1 / 1.333) = 48.6 degrees from
// the normal, which a sphere's near side reaches at 0.75 of its radius: the outer ring is a mirror and the centre is clear. Inside the
// ring the reflectance is Schlick's for water to air (on the transmitted angle's cosine), so it climbs smoothly to the mirror. The
// mirror shows the open water in the reflected direction (EvoBackdrop) and, toward the surface, the light coming down; a small
// highlight toward the sun sits on it. Through the clear centre a share of what lies behind is replaced by the open water seen along a
// bent ray (a bubble in water is a diverging lens), which lightens or darkens the centre slightly: the cheap stand-in for refraction.
//
// Premultiplied, blended One, OneMinusSrcAlpha: a bubble replaces the share of what lies behind it that it reflects, not adds white.
// The water's fog is the flakes' own, the light kept over the water between the eye and the bubble, taken on the colour and on the
// opacity, so a far bubble fades into the water behind it. The lens's blur is the large motes' (BigMote): a soft edge as wide as the
// circle of confusion, the light spread over the larger disc, and the rim's contrast smeared toward its mean.
//
// Per vertex: POSITION the bubble's centre in the world; uv0 the corner, -1 or 1 on each axis; uv1 the bubble's radius in metres, its
// fade (1 to 0), its seed, and its oblate shape (0 for a sphere); COLOR read only with _EVO_BUBBLES_TAKE2.
//
// The oblate shape (EVOSIM_THEATRE_BIRTH_BUBBLES_OBLATE), only with the local keyword _EVO_BUBBLES_TAKE2, which TheatreBirthBubbles.cs
// sets on the material when the oblate or the cover dial is on: with uv1.w > 0 the bubble is an ellipsoid of the sphere's volume, wide
// over tall uv1.w, its short axis (0, 1, 0) tilted by COLOR.rg (its x and z per metre up). The ray's nearest approach is taken in the
// ellipsoid's own metric, which gives the pixel's place on its silhouette (rho) and, from the ray's entry, the true normal, so the
// critical-angle rim and the highlight follow the shape. With uv1.w = 0 the sphere's lines run and the oblate branch is skipped.
//
// Without the keyword the preprocessed source is the first take's (e4e4d805) line for line: no COLOR input, no TEXCOORD3, no branch.
// The take-2 lines were first added as plain branches, and the dials-off check then moved one rim pixel by 3 levels in 180 frames.

Shader "Evosim/Theatre Bubbles"
{
    Properties
    {
        // The mirror ring's brightness, as a multiple of the open water it reflects.
        _RimGain("Rim brightness", Range(0, 4)) = 1.4

        // How much of the light coming down the mirror shows where it faces the surface.
        _SkyGain("The surface's light in the rim", Range(0, 4)) = 0.8

        // The highlight toward the sun: its strength and how tight it is.
        _SpecGain("Highlight", Range(0, 20)) = 6
        _SpecPower("Highlight tightness", Range(8, 2000)) = 400

        // The clear centre: the share of what lies behind replaced by the bent view of the water, and how far the view bends.
        _LensShare("Centre's bent view", Range(0, 1)) = 0.15
        _LensBend("Bend of the centre's view", Range(0, 1)) = 0.5

        // Copied from the snow shells' material each time the cloud is built: the water the shells sit in.
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
            Name "Bubbles"
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
            #pragma multi_compile_local _ _EVO_BUBBLES_TAKE2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_EVO_CAUSTIC_VARIETY)
            #define EVO_CAUSTIC_LITE
            #endif
            #include "TheatreWater.hlsl"
            #include "TheatreDepth.hlsl"
            #include "TheatreVolume.hlsl"
            #include "TheatreSnow.hlsl"

            // Water to air: the ratio of the refractive indices, and the reflectance looking straight through ((n - 1) / (n + 1))^2.
            #define EVO_BUBBLE_IOR 1.333
            #define EVO_BUBBLE_R0 0.0204

            // The mean reflectance over the disc (the mirror ring is 44% of its area): what a fully blurred bubble shows everywhere.
            #define EVO_BUBBLE_R_MEAN 0.47

            CBUFFER_START(UnityPerMaterial)
                float _RimGain;
                float _SkyGain;
                float _SpecGain;
                float _SpecPower;
                float _LensShare;
                float _LensBend;
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
            #if defined(_EVO_BUBBLES_TAKE2)
                float4 colour : COLOR;
            #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centreWS   : TEXCOORD1;
                float4 data       : TEXCOORD2;
            #if defined(_EVO_BUBBLES_TAKE2)
                float2 tilt       : TEXCOORD3;
            #endif
            };

            // The most the lens blurs a bubble, in metres at a pixel's size: the shells' cap in pixels of a 1080-line frame (BigMote's).
            float BubbleBlurMost(float pixel)
            {
                return _EvoSnowLens.y > 0.0 ? _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0 : 0.0;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 centre = TransformObjectToWorld(input.centre);
                float radius = input.data.x;

                float3 eye = GetCameraPositionWS();
                float t = unity_OrthoParams.w > 0.5 ? 0.0 : length(centre - eye);
                float pixel = max(1e-5, EvoPixelMetres(t));

                // The quad holds the bubble at its greatest blur, with its soft edge and a pixel to spare.
                float r = clamp(radius, 0.75 * pixel, 1e4 * pixel);
            #if defined(_EVO_BUBBLES_TAKE2)

                // An oblate bubble's widest half-axis, which bounds its silhouette at any tilt.
                if (input.data.w > 0.0) r *= pow(input.data.w, 1.0 / 3.0);

            #endif
                float blur = BubbleBlurMost(pixel);
                float soft = pixel + blur;
                float halfSize = (r + 0.5 * blur + 0.5 * soft) * 1.05 + pixel;

                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 corner = centre + (right * input.corner.x + up * input.corner.y) * halfSize;

                output.positionWS = corner;
                output.positionCS = TransformWorldToHClip(corner);
                output.centreWS = centre;
                output.data = input.data;
            #if defined(_EVO_BUBBLES_TAKE2)
                output.tilt = input.colour.xy;
            #endif

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

                if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1)) return half4(0, 0, 0, 0);

                float3 m = input.centreWS;
                float t = dot(m - origin, dir);

                // Stopped by the scene as the flakes are: a bubble behind a body is not drawn across it, and none hangs outside the water.
                if (!(t > t0 && t < t1)) return half4(0, 0, 0, 0);
                if (!EvoInWater(m, _BoxMin.xyz, _BoxMax.xyz, _Tank)) return half4(0, 0, 0, 0);

                float radius = input.data.x;
                float pixel = max(1e-5, EvoPixelMetres(t));
                float r = clamp(radius, 0.75 * pixel, 1e4 * pixel);

                // The lens: the large motes' circle of confusion, capped as the shells cap it; the focus is the shot's, or the scene's
                // depth at the frame's centre.
                float blur = 0.0;
                if (_EvoSnowLens.y > 0.0)
                {
                    float focus = _EvoSnowLens.x > 0.0 ? _EvoSnowLens.x : min(EvoSceneEyeDepth(float2(0.5, 0.5)), 12.0);
                    focus = max(0.05, focus);
                    float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
                    blur = min(0.5 * _EvoSnowLens.y * abs(depth - focus) / focus, BubbleBlurMost(pixel));
                }

                // The disc, with a soft edge as wide as a pixel and the blur.
                float3 off = origin + dir * t - m;
                float dist = length(off);
            #if defined(_EVO_BUBBLES_TAKE2)

                // The oblate shape: the ellipsoid x.Ax = 1, A = (I - uu)/wide^2 + uu/tall^2 about the tilted short axis u. Its silhouette
                // radius toward this pixel replaces r, so the lines below place the pixel on it; its normal at the ray's entry replaces
                // the sphere's after them. The light kept scales by the same ratio.
                float energyRadius = radius;
                bool oblate = input.data.w > 0.0;
                float3 ellNormal = float3(0.0, 1.0, 0.0);
                float ellMu = 1.0;

                if (oblate)
                {
                    float aspect = input.data.w;
                    float wide = r * pow(aspect, 1.0 / 3.0);
                    float tall = wide / aspect;
                    float3 axis = normalize(float3(input.tilt.x, 1.0, input.tilt.y));
                    float inWide = 1.0 / (wide * wide);
                    float inExtra = 1.0 / (tall * tall) - inWide;

                    float3 aOff = off * inWide + axis * (dot(off, axis) * inExtra);
                    float3 aDir = dir * inWide + axis * (dot(dir, axis) * inExtra);
                    float offAOff = dot(off, aOff);
                    float dirAOff = dot(dir, aOff);
                    float dirADir = max(dot(dir, aDir), 1e-20);

                    // The nearest approach in the ellipsoid's metric: 1 on its silhouette.
                    float rhoE = sqrt(max(offAOff - dirAOff * dirAOff / dirADir, 0.0));
                    float rhoC = min(rhoE, 0.999);

                    // The ray's entry, its outward normal, and the cosine the rim's reflectance is taken on.
                    float s = (-dirAOff - sqrt(dirADir * (1.0 - rhoC * rhoC))) / dirADir;
                    float3 hit = off + dir * s;
                    ellNormal = normalize(hit * inWide + axis * (dot(hit, axis) * inExtra));
                    ellMu = saturate(-dot(ellNormal, dir));

                    float rEll = rhoE > 1e-4 ? dist / rhoE : r;
                    energyRadius = radius * rEll / r;
                    r = rEll;
                }

            #endif
                float soft = pixel + blur;
                float reach = r + 0.5 * blur;
                float cover = saturate((reach - dist) / soft + 0.5);
                cover = cover * cover * (3.0 - 2.0 * cover);
                if (cover <= 0.0) return half4(0, 0, 0, 0);

                // The impostor: where on the sphere's near side the pixel lies, and the normal there.
                float rho = min(dist / r, 0.999);
                float mu = sqrt(1.0 - rho * rho);
                float3 across = dist > 1e-7 ? off / dist : float3(0.0, 0.0, 0.0);
                float3 n = across * rho - dir * mu;
            #if defined(_EVO_BUBBLES_TAKE2)

                if (oblate)
                {
                    mu = ellMu;
                    n = ellNormal;
                }
            #endif

                // Reflectance, water to air: Schlick's on the transmitted angle, reaching 1 at the critical angle and staying there.
                float sinT2 = EVO_BUBBLE_IOR * EVO_BUBBLE_IOR * (1.0 - mu * mu);
                float cosT = sqrt(saturate(1.0 - sinT2));
                float k = 1.0 - cosT;
                float reflectance = EVO_BUBBLE_R0 + (1.0 - EVO_BUBBLE_R0) * (k * k * k * k * k);

                // A blurred bubble's rim and centre smear together toward the disc's mean.
                float sharp = r / (r + blur);
                reflectance = lerp(EVO_BUBBLE_R_MEAN, reflectance, sharp);

                // The mirror: the open water along the reflected ray, and the light coming down where it faces the surface.
                float time = EvoTime();
                float caustic = min(EvoCausticGain(m, time), 7.0);
                float3 sun = EvoSunAt(m) * (0.3 + 0.7 * caustic);

                float3 bounce = reflect(dir, n);
                float3 mirror = EvoBackdrop(bounce) * _RimGain + EvoSunAt(m) * (_SkyGain * saturate(bounce.y));

                // The highlight toward the sun, along the refracted sun's ray back up.
                float3 toSun = -EvoSunRayWS();
                float3 halfway = normalize(toSun - dir);
                float glint = pow(saturate(dot(n, halfway)), _SpecPower) * _SpecGain * sharp;

                // The clear centre: a share of what lies behind replaced by the water along a ray bent outward, a diverging lens.
                float3 bent = normalize(dir + across * (rho * _LensBend));
                float3 lens = EvoBackdrop(bent);

                float share = (1.0 - reflectance) * _LensShare;
                float opacity = reflectance + share;
                float3 colour = mirror * reflectance + lens * share + sun * glint;

                // The flakes' fog: the light kept over the water between the eye and the bubble, on the colour and the opacity alike.
                float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(m));
                float keptGrey = dot(kept, float3(0.2126, 0.7152, 0.0722));

                // The light kept as the large motes keep theirs (spread over the blurred disc), the fade, and the eye in the water.
            #if defined(_EVO_BUBBLES_TAKE2)
                float energy = saturate((energyRadius * energyRadius) / max(1e-12, reach * reach));
            #else
                float energy = saturate((radius * radius) / max(1e-12, reach * reach));
            #endif
                float weight = cover * energy * saturate(input.data.y) * inside;

                float alpha = saturate(opacity * weight * keptGrey);
                return half4(colour * kept * weight, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
