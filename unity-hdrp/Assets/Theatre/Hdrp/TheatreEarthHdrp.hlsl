#ifndef EVOSIM_THEATRE_EARTH_HDRP_INCLUDED
#define EVOSIM_THEATRE_EARTH_HDRP_INCLUDED

// The theatre's sea bed, reef rock and earth on HDRP, for the spike (D202). The graphs
// "Evosim/Theatre Bed", "Evosim/Theatre Rock" and "Evosim/Theatre Earth" call these; their specs
// are tools/bed.graph.json, rock.graph.json and earth.graph.json.
//
// Ported by hand from unity/Assets/Theatre/TheatreBed.shader, TheatreRock.shader and
// TheatreEarth.shader: every pattern, carve and colour below is the URP shader's arithmetic,
// and only the lighting is gone, because HDRP lights these surfaces itself. What the URP shaders
// faked for light they did not have becomes HDRP's own input: the rock's shade under its cap is
// its occlusion, and the earth's flat ambient is a faint emission. Left out for now: the bed's
// settled dust (EvoFloorDust reads the snow's globals, which the spike does not push yet).

// This file: The earth, the bed's cut face seen through the tank's side (TheatreEarth.shader).

#include "TheatreNoiseHdrp.hlsl"
#include "TheatreDepthHdrp.hlsl"


void EvoEarthSurface_float(float3 PositionAWS, float Top,
    out float3 BaseColor, out float3 Emission, out float Smoothness, out float Metallic,
    out float3 Specular)
{
    float3 p = PositionAWS;
    float below = max(0.0, Top - p.y);
    float3 unused;
    float stretch = EvoValueNoise(float3(3.1, p.y * 0.21, 7.7), unused);
    float bend = EvoValueNoise(float3(p.x * 0.11, p.y * 0.05, p.z * 0.11), unused);
    float y = p.y + 2.2 * stretch + 0.9 * (bend - 0.5);
    float layer = y / max(0.05, _StrataMetres);
    float index = floor(layer);
    float within = layer - index;
    float here = 0.3 + 0.7 * EvoHash1(float2(index, 17.0));
    float next = 0.3 + 0.7 * EvoHash1(float2(index + 1.0, 17.0));
    float tone = lerp(here, next, smoothstep(0.55, 1.0, within));
    float drift = EvoValueNoise(float3(p.x * 0.35, y * 0.9, p.z * 0.35), unused);
    tone = saturate(tone + 0.4 * (drift - 0.5));
    float grain = EvoHash1(floor(p * 33.0));

    float3 albedo = lerp(_EarthDark.rgb, _EarthLight.rgb, tone);
    albedo *= 0.85 + 0.3 * grain;
    float sandEdge = _SandMetres * (0.6 + 0.8 * bend);
    albedo = lerp(albedo, _Sand.rgb * (0.9 + 0.2 * grain), 1.0 - smoothstep(0.0, sandEdge, below));
    albedo *= lerp(1.0, 0.55, saturate(below / _DarkenMetres));

    float3 depthLight = EvoDepthLight(p.y);
    BaseColor = saturate(albedo) * depthLight;
    Emission = albedo * _Ambient;
    Smoothness = 0.1;
    Metallic = 0.0;
    Specular = EvoStoneSpecular();
}

#endif
