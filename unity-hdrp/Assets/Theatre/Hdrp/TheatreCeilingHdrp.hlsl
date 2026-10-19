#ifndef EVOSIM_THEATRE_CEILING_HDRP_INCLUDED
#define EVOSIM_THEATRE_CEILING_HDRP_INCLUDED

// The surface of the sea seen from underneath, on HDRP, for the spike (D202). The graph
// "Evosim/Theatre Ceiling" calls EvoCeilingSurface and does nothing else; its spec is
// tools/ceiling.graph.json, and HdrpLook puts it on the skin's surface quad in place of the URP
// shader.
//
// Ported by hand from unity/Assets/Theatre/TheatreSurface.shader and the parts of
// TheatreWater.hlsl it reads: the ripple, Snell's window, the sun's disc and glow, the mirror
// outside the window, and the sky of the run's day (item 6). The arithmetic is the URP shader's.
// What changes is where it goes: the colour is the surface's emission on a black albedo, so HDRP
// adds nothing of its own lights to it, and HDRP's fog stands in for EvoMixFog. Left out: the wave
// in the geometry (a few centimetres; the shimmer is in the per pixel normal) and the fade as the
// camera crosses the waterline, since every film shot is under the water.

#include "TheatreNoiseHdrp.hlsl"

// The shaders' clock as TheatreWater.hlsl's EvoTime reads it: Unity's own, or the safari's take time
// under EVOSIM_THEATRE_SAFARI_CLOCK, so this ceiling ripples in step with the URP theatre's.
float _EvoClock;
float _EvoClockOn;
float EvoCeilingTime() { return _EvoClockOn > 0.5 ? _EvoClock : _Time.y; }

// TheatreSkin's globals, as TheatreWater.hlsl reads them. Set with Shader.SetGlobal, so the same
// values reach either pipeline.
float4 _EvoRipple;
float4 _EvoSun;
float4 _EvoWaterColumn;
float4 _EvoWaterDeep;
float4 _EvoWaterShallow;
float4 _EvoLook;
float4 _EvoDaylight;
float4 _EvoLook2;
float4 _EvoFade;

float EvoCeilWaveMetres()       { return _EvoRipple.x > 0.0  ? _EvoRipple.x : 0.045; }
float EvoCeilWaveLengthMetres() { return _EvoRipple.y > 0.01 ? _EvoRipple.y : 1.6; }
float EvoCeilWaveSpeed()        { return _EvoRipple.z > 0.0  ? _EvoRipple.z : 0.5; }
float EvoCeilLightReachMetres() { return _EvoRipple.w > 0.01 ? _EvoRipple.w : 18.0; }

float3 EvoCeilSunDirectionWS()
{
    return dot(_EvoSun.xyz, _EvoSun.xyz) > 1e-4 ? normalize(_EvoSun.xyz) : normalize(float3(0.28, 0.92, 0.27));
}

float EvoCeilWaterlineY()      { return _EvoWaterColumn.w > 0.5 ? _EvoWaterColumn.x : 0.0; }
float EvoCeilWaterReach()      { return _EvoWaterColumn.y > 0.01 ? _EvoWaterColumn.y : 18.0; }
float EvoCeilWaterViewMetres() { return 3.0 * EvoCeilWaterReach(); }

float3 EvoCeilWaterColour(float y)
{
    float3 deep = _EvoWaterDeep.rgb;
    if (_EvoWaterColumn.z <= 0.5) return deep;
    float depth = max(0.0, EvoCeilWaterlineY() - y);
    return lerp(deep, _EvoWaterShallow.rgb, exp(-depth / EvoCeilWaterReach()));
}

bool EvoCeilLookDepth() { return _EvoLook.y > 0.5; }
bool EvoCeilLookSky()   { return _EvoLook.z > 0.5; }
bool EvoCeilLookFade()  { return _EvoLook2.x > 0.5 && _EvoLook.x > 0.5; }
float EvoCeilDayGain()  { return EvoCeilLookDepth() ? max(0.0, _EvoDaylight.y) : 1.0; }

// TheatreWater.hlsl's EvoRipple, the slope only: three trains at deep water speeds.
float2 EvoCeilSlope(float2 xz, float time)
{
    float amplitude = EvoCeilWaveMetres();
    float wavelength = EvoCeilWaveLengthMetres();
    float speed = EvoCeilWaveSpeed();

    const float2 heading0 = float2(0.99503, 0.09950);
    const float2 heading1 = float2(-0.31623, 0.94868);
    const float2 heading2 = float2(0.62470, -0.78087);

    float2 slope = float2(0.0, 0.0);

    [unroll]
    for (int train = 0; train < 3; train++)
    {
        float2 heading = train == 0 ? heading0 : (train == 1 ? heading1 : heading2);
        float fraction = train == 0 ? 1.0 : (train == 1 ? 0.53 : 0.27);
        float gain = train == 0 ? 0.58 : (train == 1 ? 0.28 : 0.14);

        float metres = max(0.05, wavelength * fraction);
        float k = 6.2831853 / metres;
        float phaseSpeed = speed * sqrt(9.81 * metres / 6.2831853);
        float a = amplitude * gain;

        float phase = k * dot(heading, xz) - k * phaseSpeed * time;
        slope += (a * k * cos(phase)) * heading;
    }

    return slope;
}

float EvoCeilWindowReflects(float3 incident, float3 n, float index)
{
    float3 through = refract(incident, -n, index);
    if (dot(through, through) <= 1e-5) return 1.0;
    float cosAir = saturate(dot(normalize(through), n));
    return saturate(0.02 + 0.98 * pow(1.0 - cosAir, 5.0));
}

float EvoCeilClouds(float3 through, float time)
{
    float2 plane = through.xz / max(0.08, through.y) * 0.35 + time * float2(0.004, 0.0015);
    float3 gradient;
    float field = 0.55 * EvoValueNoise(float3(plane, 0.37), gradient);
    field += 0.30 * EvoValueNoise(float3(plane * 2.3, 1.91), gradient);
    field += 0.15 * EvoValueNoise(float3(plane * 5.1, 4.13), gradient);
    return field;
}

// TheatreSurface.shader's SkyDay: the sky of the run's day, its overcast and the mirror's rim.
float3 EvoCeilSkyDay(float3 incident, float3 n, float eyeY, float time)
{
    float overcast = saturate(_EvoDaylight.w);
    float day = EvoCeilDayGain();

    float3 mirrored = float3(
        EvoCeilWindowReflects(incident, n, 1.331),
        EvoCeilWindowReflects(incident, n, 1.333),
        EvoCeilWindowReflects(incident, n, 1.338));

    float3 sky = _SkyHorizon.rgb;
    float3 through = refract(incident, -n, 1.333);

    if (dot(through, through) > 1e-5)
    {
        through = normalize(through);
        sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(through.y));

        float field = EvoCeilClouds(through, time);
        float cover = smoothstep(0.72 - 0.45 * overcast, 0.90 - 0.40 * overcast, field);
        cover = max(cover, 0.85 * overcast * overcast);

        float luma = dot(sky, float3(0.2126, 0.7152, 0.0722));
        float3 cloud = luma * float3(0.94, 0.97, 1.0) * lerp(1.05, 0.55, overcast);
        sky = lerp(sky, cloud, cover);
        sky *= lerp(1.0, 0.7, overcast);

        float3 sun = EvoCeilSunDirectionWS();
        float angle = acos(clamp(dot(through, sun), -1.0, 1.0));
        float discRadians = radians(_SunDegrees * (1.0 + 2.5 * overcast));
        float disc = 1.0 - smoothstep(discRadians * lerp(0.75, 0.2, overcast), discRadians * 1.25, angle);
        float glow = pow(saturate(1.0 - angle / max(0.01, radians(_GlowDegrees * (1.0 + overcast)))), 3.0);

        float seen = lerp(1.0, 0.06, overcast) * (1.0 - 0.6 * cover);
        sky += _SunColor.rgb * (_SunStrength * (disc * seen + 0.10 * lerp(1.0, 0.3, overcast) * glow));
    }

    float3 bounced = reflect(incident, n);
    float3 mirror = _DeepColor.rgb * (0.55 + 0.45 * saturate(-bounced.y));

    float cosIn = saturate(dot(incident, n));
    float sinIn = sqrt(saturate(1.0 - cosIn * cosIn));
    float beyond = max(0.0, (sinIn - 0.7502) / 0.2498);
    float nearRim = exp(-5.0 * beyond);

    mirror = lerp(mirror, EvoCeilWaterColour(EvoCeilWaterlineY()) * 1.6, 0.55 * nearRim);

    if (EvoCeilLookFade())
    {
        float3 below = EvoCeilWaterColour(min(eyeY, EvoCeilWaterlineY()) + bounced.y * EvoCeilWaterViewMetres())
            * max(0.0, _EvoFade.z);
        mirror = lerp(below, EvoCeilWaterColour(EvoCeilWaterlineY()) * 1.3, 0.45 * nearRim * nearRim * nearRim);
    }

    return lerp(sky * day, mirror * day, mirrored);
}

// PositionAWS is the absolute world position of the pixel on the quad; ViewWS points from it to
// the eye. The clock is the shaders' own, as the URP ceiling's is.
void EvoCeilingSurface_float(float3 PositionAWS, float3 ViewWS,
    out float3 BaseColor, out float3 Emission, out float Smoothness, out float Metallic)
{
    float Time = EvoCeilingTime();
    float2 slope = EvoCeilSlope(PositionAWS.xz, Time);
    float3 n = normalize(float3(-slope.x, 1.0, -slope.y));
    float3 incident = -normalize(ViewWS);
    float3 eye = GetAbsolutePositionWS(float3(0.0, 0.0, 0.0));

    float3 through = refract(incident, -n, 1.333);
    float3 sky = _SkyHorizon.rgb;
    float mirrored = 1.0;

    if (dot(through, through) > 1e-5)
    {
        through = normalize(through);
        sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(through.y));

        float3 sun = EvoCeilSunDirectionWS();
        float angle = acos(clamp(dot(through, sun), -1.0, 1.0));
        float disc = 1.0 - smoothstep(radians(_SunDegrees) * 0.75, radians(_SunDegrees) * 1.25, angle);
        float glow = pow(saturate(1.0 - angle / max(0.01, radians(_GlowDegrees))), 3.0);
        sky += _SunColor.rgb * (_SunStrength * (disc + 0.10 * glow));

        float cosAir = saturate(dot(through, n));
        mirrored = saturate(0.02 + 0.98 * pow(1.0 - cosAir, 5.0));
    }

    float3 bounced = reflect(incident, n);
    float3 mirror = _DeepColor.rgb * (0.55 + 0.45 * saturate(-bounced.y));
    float3 colour = lerp(sky, mirror, mirrored);

    if (EvoCeilLookSky()) colour = EvoCeilSkyDay(incident, n, eye.y, Time);

    float below = max(0.0, _Waterline - eye.y);
    float reach = max(1.0, 2.0 * EvoCeilLightReachMetres());
    float depthFade = lerp(1.0, _DeepFade, saturate(below / reach));

    BaseColor = float3(0.0, 0.0, 0.0);
    Emission = colour * (_SurfaceBrightness * depthFade);
    Smoothness = 0.0;
    Metallic = 0.0;
}

#endif
