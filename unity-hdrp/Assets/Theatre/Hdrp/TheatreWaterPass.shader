// The water between the eye and everything, on HDRP, for the spike (D202): the URP theatre's own
// fade, applied once over the frame as a custom pass after the opaque objects and the sky.
//
// Why a pass and not HDRP's fog. HDRP's volumetric fog scatters only the light that reaches it,
// and under forty metres of water that light is dim, so it darkened the far reef and the bed into
// clear grey air where the URP theatre fades them into the lit water (the third sheet's Arrival
// and Descent). Its analytic fog has one grey extinction where the water takes red first. So this
// is TheatreWater.hlsl's EvoMixFog and EvoBackdrop, word for word, read from the globals the skin
// pushes in either pipeline: the water path clipped to the world's box, the absorption per channel
// from the fade's start, the column's colour at the height three reaches along the look as the
// in-scatter, the phase term toward the sun and the run's day. Where nothing stands the pass draws
// the URP backdrop in place of HDRP's sky. HdrpLook turns HDRP's fog off while this is on.
//
// HDRP's colour buffer holds light times the exposure, so the colours this adds are multiplied by
// it; the surfaces already in the frame are.
//
// The light shafts are two more passes after the water, the URP theatre's TheatreShaftMarch.shader
// run over the frame: HDRP's volumetric fog drew no shaft under that dim light either. The marine
// snow is one more, TheatreSnowShells.shader's shells, drawn with a second material of this shader
// because the snow's box is the whole water and the shafts' only the lit top of it.

Shader "Hidden/Evosim/Theatre Water Pass"
{
    // The shaft march's properties, declared so HdrpLook can copy them from the skin's URP march
    // material by name; the defaults are TheatreShaftMarch.shader's.
    Properties
    {
        _ShaftColor("Shaft colour", Color) = (0.62, 0.84, 0.96, 1)
        _MarchStrength("Strength per metre", Float) = 0.03
        _MarchSteps("Steps along a ray", Float) = 48
        _MarchContrast("Beam contrast", Float) = 2.0
        _MarchFocus("Share of a beam's light the focusing sets", Float) = 0.5
        _MarchPhaseCap("Phase term's ceiling", Float) = 3
        _MarchReach("Longest march, metres", Float) = 40
        _BoxMin("Box minimum, world metres", Vector) = (-1000, -1000, -1000, 0)
        _BoxMax("Box maximum, world metres", Vector) = (1000, 1000, 1000, 0)
        _Tank("Tank: centre x, centre z, radius, on", Vector) = (0, 0, 0, 0)
        _Waterline("The waterline, world y", Float) = 0
        _PlaceShare("Share of the surface that sends a beam", Float) = 0.35
        _PlacePower("A sheet's power under item 3", Float) = 2.5
        _PlaceOverObject("What is left in front of a thing", Float) = 0.4
        _PlaceReach("Longest march under item 3, metres", Float) = 30
        _PlaceMaskMetres("Size of the stretches of surface the sheets' strength varies over, metres", Float) = 12
        _SheetMetres("Size of a sheet's patch of surface, metres", Float) = 2.5
        _SheetDepthMetres("Depth the sheets fade over, metres", Float) = 12
        _SheetShimmer("Share of a sheet the waves' focusing sets", Float) = 0.35
        _SheetOnsetMetres("Depth a sheet comes in over under the surface, metres", Float) = 4
        _PlaceDebug("Draw the shaft term alone", Float) = 0

        // The snow shells' properties, TheatreSnowShells.shader's, on the snow's own material.
        _MoteColor("Mote colour", Color) = (0.70, 0.80, 0.84, 1)
        _ShellGain("Brightness", Float) = 0.35
        _ShareScale("Quantity", Float) = 1
        _MoteAmbient("Ambient share", Float) = 0
        _FallMetresPerSecond("Fall, metres a second", Float) = 0.05
        _Near("Near shell (from, to, cell, share)", Vector) = (0.25, 2.0, 0.35, 0.5)
        _Middle("Middle shell (from, to, cell, share)", Vector) = (2.0, 6.0, 0.8, 0.5)
        _Far("Far shell (from, to, cell, share)", Vector) = (6.0, 16.0, 1.8, 0.6)
        _Radii("Mote radius near, middle, far; near cap px", Vector) = (0.0025, 0.004, 0.008, 5)
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 vulkan metal

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    // The shaft march's material, copied from the skin's URP march material before every render
    // (HdrpLook.PushShafts); the names are TheatreShaftMarch.shader's.
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
    float4 _ReefCaps[32];
    float _ReefCapCount;

    // The snow shells' material (HdrpLook.PushSnow); the names are TheatreSnowShells.shader's.
    float4 _MoteColor;
    float _ShellGain;
    float _ShareScale;
    float _MoteAmbient;
    float _FallMetresPerSecond;
    float4 _Near;
    float4 _Middle;
    float4 _Far;
    float4 _Radii;

    // The drift of the snow with the run's water, pushed by the skin (TheatreSnowShells.shader).
    float4 _EvoSnowDrift;
    #define EVO_MOTE_PHASE_CAP 4.0

    // The water column, the look's switches, the wave field and the march's helpers, copied from
    // the URP theatre by unity-hdrp/tools/port_hlsl.py.
    #include "TheatreShaftHdrp.hlsl"

    float4 _EvoWaterLow;
    float4 _EvoWaterHigh;
    float4 _EvoWaterTank;
    float _EvoHdrpBodyDebug;   // 3: the test view of the water path (Water, below)

    float WaterlineY()      { return _EvoWaterColumn.w > 0.5 ? _EvoWaterColumn.x : 0.0; }
    float WaterReach()      { return _EvoWaterColumn.y > 0.01 ? _EvoWaterColumn.y : 18.0; }
    float WaterViewMetres() { return 3.0 * WaterReach(); }
    bool LookWater()        { return _EvoLook.x > 0.5; }
    bool LookDepth()        { return _EvoLook.y > 0.5; }
    bool LookFade()         { return _EvoLook2.x > 0.5 && _EvoLook.x > 0.5; }
    float DayGain()         { return LookDepth() ? max(0.0, _EvoDaylight.y) : 1.0; }

    float3 SunRay()
    {
        return dot(_EvoSunRay.xyz, _EvoSunRay.xyz) > 1e-4 ? normalize(_EvoSunRay.xyz) : normalize(float3(0.20, -0.96, 0.19));
    }

    float3 WaterColour(float y)
    {
        float3 deep = _EvoWaterDeep.rgb;
        if (_EvoWaterColumn.z <= 0.5) return deep;
        float depth = max(0.0, WaterlineY() - y);
        return lerp(deep, _EvoWaterShallow.rgb, exp(-depth / WaterReach()));
    }

    float PhaseHG(float cosTheta, float g)
    {
        float g2 = g * g;
        float denominator = max(1e-4, 1.0 + g2 - 2.0 * g * cosTheta);
        return (1.0 - g2) / (denominator * sqrt(denominator));
    }

    float PhaseTerm(float3 view)
    {
        float p = PhaseHG(dot(SunRay(), -view), clamp(_EvoPhase.x, 0.0, 0.95));
        return max(0.0, 1.0 + _EvoPhase.y * (p - 1.0));
    }

    float FadeMetres(float d)
    {
        float start = max(1e-3, _EvoFade.x);
        float full = max(start + 1.0, _EvoFade.w);
        float ramp = full - start;
        if (d <= start) return 0.0;
        if (d <= full) return (d - start) * (d - start) / (2.0 * ramp);
        return d - 0.5 * (start + full);
    }

    // TheatreWater.hlsl's EvoWaterPathMetres, with the eye handed in.
    float WaterPathMetres(float3 eye, float3 positionWS)
    {
        float3 delta = positionWS - eye;
        float d = length(delta);

        if (_EvoWaterLow.w < 0.5)
        {
            float waterline = WaterlineY();
            if (eye.y > waterline)
            {
                if (positionWS.y >= waterline) return 0.0;
                d *= (waterline - positionWS.y) / max(1e-4, eye.y - positionWS.y);
            }
            return d;
        }

        float3 dir = delta / max(1e-6, d);
        float3 safe = dir + step(abs(dir), 1e-6) * 1e-6;
        float3 a = (_EvoWaterLow.xyz - eye) / safe;
        float3 b = (_EvoWaterHigh.xyz - eye) / safe;
        float3 nearer = min(a, b);
        float3 farther = max(a, b);
        float t0 = max(0.0, max(max(nearer.x, nearer.y), nearer.z));
        float t1 = min(d, min(min(farther.x, farther.y), farther.z));

        if (_EvoWaterTank.w > 0.5)
        {
            float2 o = eye.xz - _EvoWaterTank.xy;
            float2 h = dir.xz;
            float qa = dot(h, h);
            float qb = dot(o, h);
            float qc = dot(o, o) - _EvoWaterTank.z * _EvoWaterTank.z;
            if (qa > 1e-8)
            {
                float disc = qb * qb - qa * qc;
                float s = sqrt(max(0.0, disc));
                t0 = max(t0, (-qb - s) / qa);
                t1 = min(t1, disc < 0.0 ? t0 : (-qb + s) / qa);
            }
            else if (qc > 0.0)
            {
                t1 = t0;
            }
        }

        return max(0.0, t1 - t0);
    }

    bool EyeInWater(float3 eye)
    {
        if (_EvoWaterLow.w < 0.5) return eye.y < WaterlineY();
        if (any(eye < _EvoWaterLow.xyz) || any(eye > _EvoWaterHigh.xyz)) return false;
        if (_EvoWaterTank.w > 0.5)
        {
            float2 o = eye.xz - _EvoWaterTank.xy;
            if (dot(o, o) > _EvoWaterTank.z * _EvoWaterTank.z) return false;
        }
        return true;
    }

    float3 Backdrop(float3 eye, float3 direction)
    {
        float eyeY = min(eye.y, WaterlineY());
        float3 colour = WaterColour(eyeY + direction.y * WaterViewMetres());
        if (LookWater()) colour *= PhaseTerm(direction);
        colour *= DayGain();
        if (LookFade() && EyeInWater(eye)) colour *= max(0.0, _EvoFade.z);
        return colour;
    }

    float3 FadeTarget(float3 eye, float3 direction)
    {
        float eyeY = min(eye.y, WaterlineY());
        float3 colour = WaterColour(eyeY + direction.y * WaterViewMetres());
        if (LookWater()) colour *= PhaseTerm(direction);
        return colour * (DayGain() * max(0.0, _EvoFade.z));
    }

    // The run's snow field, the volume helpers and the shells' grid walk, copied from the URP theatre
    // by unity-hdrp/tools/port_hlsl.py; the walk calls WaterPathMetres and WaterColour above.
    #include "TheatreSnowHdrp.hlsl"

    // What the water keeps of the surface behind each pixel and what it adds, per channel: the
    // frame becomes colour * kept + added. Two passes apply it with the blend unit (the first
    // multiplies, the second adds), so the pass never reads the frame; HDRP did not copy the frame
    // for a pass to read at either injection point tried, and the read came back black.
    void Water(Varyings varyings, out float3 kept, out float3 added)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float3 eye = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));
        float3 positionWS = GetAbsolutePositionWS(posInput.positionWS);
        float3 view = -GetWorldSpaceNormalizeViewDir(posInput.positionWS);
        float exposure = GetCurrentExposureMultiplier();

        kept = float3(1.0, 1.0, 1.0);
        added = float3(0.0, 0.0, 0.0);

        // Nothing stands here: the open water, as the URP backdrop paints it.
        if (depth == UNITY_RAW_FAR_CLIP_VALUE)
        {
            kept = float3(0.0, 0.0, 0.0);
            added = Backdrop(eye, view) * exposure;
            return;
        }

        if (LookFade())
        {
            float d = FadeMetres(WaterPathMetres(eye, positionWS));
            kept = exp(-(_EvoAbsorb.xyz + _EvoFade.yyy) * d);
            added = FadeTarget(eye, view) * exposure * (1.0 - kept);
        }
        else if (LookWater())
        {
            float d = WaterPathMetres(eye, positionWS);
            kept = exp(-_EvoAbsorb.xyz * d);
            float scattered = 1.0 - exp(-_EvoAbsorb.w * d);
            float3 inscatter = WaterColour(0.5 * (min(eye.y, WaterlineY()) + positionWS.y)) * (PhaseTerm(view) * DayGain());
            added = inscatter * exposure * scattered;
        }

        // The test view 3: red the water path over 60 m, green the green kept, blue the start over 10.
        if (_EvoHdrpBodyDebug > 2.5)
        {
            float keptGreen = kept.g;
            kept = float3(0.0, 0.0, 0.0);
            added = float3(saturate(WaterPathMetres(eye, positionWS) / 60.0), keptGreen, saturate(_EvoFade.x / 10.0)) * exposure;
        }
    }

    float4 Keep(Varyings varyings) : SV_Target
    {
        float3 kept, added;
        Water(varyings, kept, added);
        return float4(kept, 1.0);
    }

    float4 Add(Varyings varyings) : SV_Target
    {
        float3 kept, added;
        Water(varyings, kept, added);
        return float4(added, 0.0);
    }

    // The light shafts: TheatreShaftMarch.shader's march, run over the frame after the water
    // rather than on the inside faces of a box. The eye and the points are absolute, from the
    // depth buffer as in Water above, since HDRP's camera-relative rendering puts the camera at the
    // origin. colour is what the march adds in the URP theatre's light; under item 3 (the shafts in
    // their place) the URP blend is a screen, the frame times one less the colour plus the colour,
    // and the two passes below apply it as Water's do.
    void Shafts(Varyings varyings, out float3 colour, out bool screen)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        colour = float3(0.0, 0.0, 0.0);
        bool place = EvoLookPlace();
        screen = place && _PlaceDebug <= 0.5;

        // Only from under the surface, as the URP march.
        float3 origin = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));
        float inside = saturate(_Waterline - origin.y);
        if (inside <= 0.002) return;

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 dir = -GetWorldSpaceNormalizeViewDir(posInput.positionWS);
        float tScene = depth == UNITY_RAW_FAR_CLIP_VALUE ? 1e6 : length(posInput.positionWS);

        float t0 = _ProjectionParams.y;
        float t1 = 1e6;
        if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1)) return;

        bool onObject = tScene < t1 - 0.05;

        t1 = min(t1, tScene);
        if (t1 <= t0) return;

        t1 = min(t1, t0 + (place ? _PlaceReach : _MarchReach));

        int steps = (int)clamp(_MarchSteps, 8.0, 64.0);
        float dt = (t1 - t0) / steps;
        float jitter = EvoPixelDither(varyings.positionCS.xy);

        float3 sum = float3(0.0, 0.0, 0.0);

        [loop]
        for (int i = 0; i < steps; i++)
        {
            float t = t0 + (i + jitter) * dt;
            float3 p = origin + dir * t;

            float gain = min(EvoCausticGain(p, EvoTime()), 4.0);
            float shaft;

            if (place)
            {
                float2 top = SurfaceEntry(p);
                shaft = SheetAt(top, EvoTime()) * PlaceMask(top, EvoTime())
                      * (_PlaceDebug > 1.5 ? 1.0 : CapShade(p))
                      * lerp(1.0, gain, 0.2 * _MarchFocus);
                shaft *= lerp(1.0, 0.55 + 1.5 * EvoBeamAt(p, EvoTime()), _SheetShimmer);

                float below = max(0.0, EvoWaterlineY() - p.y);
                shaft *= exp(-below / max(1.0, _SheetDepthMetres));
                shaft *= smoothstep(2.0, 8.0, t - t0);
            }
            else
            {
                shaft = pow(EvoBeamAt(p, EvoTime()), _MarchContrast) * lerp(1.0, gain, _MarchFocus);
            }

            float3 light = EvoSunAt(p) * exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * WaterPathMetres(origin, p));

            if (place) shaft *= 1.0 - smoothstep(0.6 * _PlaceReach, _PlaceReach, t - t0);
            if (place) shaft *= smoothstep(0.0, max(0.1, _SheetOnsetMetres), EvoWaterlineY() - p.y);

            sum += shaft * light;
        }

        float phase = min(EvoPhaseTerm(dir), _MarchPhaseCap);
        colour = _ShaftColor.rgb * (_MarchStrength * phase * dt * inside) * sum;

        if (place && _PlaceDebug > 0.5) { colour = min(4.0 * colour, 1.0); return; }

        if (place)
        {
            if (onObject) colour *= _PlaceOverObject;
            colour = min(colour, 0.95);
        }
    }

    // The marine snow: TheatreSnowShells.shader's fragment, with the eye and the ray from the depth
    // buffer as in Shafts. Added over the frame, as in URP.
    float4 Snow(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float3 origin = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));
        float inside = saturate(_Waterline - origin.y);
        if (inside <= 0.002) return float4(0.0, 0.0, 0.0, 0.0);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 dir = -GetWorldSpaceNormalizeViewDir(posInput.positionWS);
        float tScene = depth == UNITY_RAW_FAR_CLIP_VALUE ? 1e6 : length(posInput.positionWS);

        float t0 = _ProjectionParams.y;
        float t1 = tScene;
        if (!EvoClipToWater(origin, dir, _BoxMin.xyz, _BoxMax.xyz, _Tank, t0, t1)) return float4(0.0, 0.0, 0.0, 0.0);

        float time = EvoTime();
        float4 sum = float4(0.0, 0.0, 0.0, 0.0);

        sum += Shell(origin, dir, max(t0, _Near.x), min(t1, _Near.y), _Near, _Radii.x, _Radii.w, 0.0, time);
        sum += Shell(origin, dir, max(t0, _Middle.x), min(t1, _Middle.y), _Middle, _Radii.y, 1e4, 17.0, time);
        sum += Shell(origin, dir, max(t0, _Far.x), min(t1, _Far.y), _Far, _Radii.z, 1e4, 41.0, time);

        float phase = _EvoSnowDrift.w > 0.5 ? min(EvoPhaseTerm(dir), EVO_MOTE_PHASE_CAP) : 1.0;
        float day = EvoDayGain() * (EvoLookFade() ? max(0.0, _EvoFade.z) : 1.0);
        float3 ambient = _MoteColor.rgb * (_MoteAmbient * day * inside) * sum.a;

        float3 colour = _MoteColor.rgb * (_ShellGain * inside * phase) * sum.rgb + ambient;
        return float4(colour * GetCurrentExposureMultiplier(), 0.0);
    }

    // The URP debug view writes the shaft term over a black frame: kept 0.
    float4 ShaftKeep(Varyings varyings) : SV_Target
    {
        if (EvoLookPlace() && _PlaceDebug > 0.5) return float4(0.0, 0.0, 0.0, 1.0);
        if (!EvoLookPlace()) return float4(1.0, 1.0, 1.0, 1.0);
        float3 colour;
        bool screen;
        Shafts(varyings, colour, screen);
        return float4(1.0 - colour, 1.0);
    }

    float4 ShaftAdd(Varyings varyings) : SV_Target
    {
        float3 colour;
        bool screen;
        Shafts(varyings, colour, screen);
        return float4(colour * GetCurrentExposureMultiplier(), 0.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Theatre Water Keep"
            ZWrite Off
            ZTest Always
            Blend Zero SrcColor
            Cull Off

            HLSLPROGRAM
                #pragma fragment Keep
            ENDHLSL
        }
        Pass
        {
            Name "Theatre Water Add"
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma fragment Add
            ENDHLSL
        }
        Pass
        {
            Name "Theatre Shaft Keep"
            ZWrite Off
            ZTest Always
            Blend Zero SrcColor
            Cull Off

            HLSLPROGRAM
                #pragma fragment ShaftKeep
            ENDHLSL
        }
        Pass
        {
            Name "Theatre Shaft Add"
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma fragment ShaftAdd
            ENDHLSL
        }
        Pass
        {
            Name "Theatre Snow Add"
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma fragment Snow
            ENDHLSL
        }
    }
    Fallback Off
}
