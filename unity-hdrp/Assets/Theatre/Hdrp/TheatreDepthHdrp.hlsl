#ifndef EVOSIM_THEATRE_DEPTH_HDRP_INCLUDED
#define EVOSIM_THEATRE_DEPTH_HDRP_INCLUDED

// Light lost on the way down. HDRP lights every surface at the sun's full strength wherever it is,
// and its fog dims only the path from a surface to the camera, so a bed 25 m down lit as at the
// waterline reads as land under a sky. This is the URP theatre's own factor, TheatreWater.hlsl's
// EvoDepthLight, word for word: one at the waterline on the mean day, falling by e-folds of the
// run's attenuation depth with red first, times the run's day, never under the look's floor, and
// one with the look's item 3 off. The URP shaders multiply the key and the ambient by it and leave
// the rim, the glow and the earth's flat ambient alone; applied here to the albedo, it scales the
// sun and the ambient HDRP brings alike, and the emission is left alone as URP leaves it.

float4 _EvoLook;            // y: item 3, light by depth and day (TheatreSkin.PushLook)
float4 _EvoDaylight;        // x: one over the attenuation depth; y: the day; z: the floor
float4 _EvoDepthRates;      // how fast red, green and blue fall against the attenuation depth
float4 _EvoWaterColumn;     // x: the waterline; w: 1 once the skin has pushed it
float _EvoHdrpSurfaceY;     // the waterline from HdrpLook, for a scene the skin has not pushed
float _EvoHdrpDepthLight;   // 1 takes the light out, 0 leaves every surface at full sun (EVOSIM_HDRP_DEPTH_LIGHT)
float _EvoHdrpStoneSpecular; // the bed's and the rock's specular, as a share of a dielectric's (EVOSIM_HDRP_STONE_SPECULAR)

float3 EvoDepthLight(float y)
{
    if (_EvoLook.y <= 0.5) return float3(1.0, 1.0, 1.0);

    float waterline = _EvoWaterColumn.w > 0.5 ? _EvoWaterColumn.x : _EvoHdrpSurfaceY;
    float depth = max(0.0, waterline - y);
    float3 fall = exp(-depth * _EvoDaylight.x * _EvoDepthRates.xyz);
    float3 light = max(fall, _EvoDaylight.zzz) * max(0.0, _EvoDaylight.y);

    return lerp(float3(1.0, 1.0, 1.0), light, saturate(_EvoHdrpDepthLight));
}

// The bed's, the rock's and the earth's specular colour. The URP theatre's three have no specular term,
// and HDRP Lit's, at a dielectric's 0.04, grows toward a grazing view: it lit the bed seen across
// the water in Descent to twice URP's brightness (2026-10-19). So the three graphs take HDRP's
// specular colour workflow, at 0 by default; 1 gives back the dielectric's.
float3 EvoStoneSpecular()
{
    return (0.04 * saturate(_EvoHdrpStoneSpecular)).xxx;
}

#endif
