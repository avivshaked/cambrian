#ifndef EVOSIM_THEATRE_BED_HDRP_INCLUDED
#define EVOSIM_THEATRE_BED_HDRP_INCLUDED

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

// This file: The bed (TheatreBed.shader).

#include "TheatreNoiseHdrp.hlsl"
#include "TheatreDepthHdrp.hlsl"


float BedCarve(float2 xz, out float2 slope)
{
    float metres = max(0.5, _BedLobeMetres);
    float3 coarseGradient, fineGradient;
    float coarse = EvoValueNoise(float3(xz.x, 0.0, xz.y) / metres, coarseGradient);
    float fine = EvoValueNoise(float3(xz.x, 0.0, xz.y) * (2.7 / metres), fineGradient);
    float value = 0.78 * coarse + 0.22 * fine;
    slope = (0.78 * coarseGradient.xz + 0.22 * 2.7 * fineGradient.xz) / metres;
    return saturate(value);
}

float BedHeight(float2 p)
{
    float ripple =
        sin(p.x * 6.2831853 + sin(p.y * 1.7) * 1.3) * 0.6 +
        sin((p.x * 0.72 + p.y * 0.44) * 6.2831853) * 0.4;
    float2 q = p * (_RippleMetres / max(0.001, _GrainMetres));
    float2 cell = floor(q);
    float2 f = q - cell;
    f = f * f * (3.0 - 2.0 * f);
    float a = EvoHash1(cell);
    float b = EvoHash1(cell + float2(1.0, 0.0));
    float c = EvoHash1(cell + float2(0.0, 1.0));
    float d = EvoHash1(cell + float2(1.0, 1.0));
    float grain = lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    return ripple * _RippleStrength + (grain - 0.5) * _GrainStrength * 2.0;
}

float3 BedNormal(float2 p)
{
    const float e = 0.02;
    float h = BedHeight(p);
    float dx = BedHeight(p + float2(e, 0.0)) - h;
    float dy = BedHeight(p + float2(0.0, e)) - h;
    return normalize(float3(-dx / e, -dy / e, 8.0));
}

// The URP bed's BedSurface, in HDRP's camera-relative world and back to object space. The carve
// is read at the absolute position, so the lobes stay where they are when the camera moves.
void EvoBedVertex_float(float3 PositionOS, float3 NormalOS,
    out float3 Position, out float3 Normal, out float3 SurfaceNormalWS)
{
    float3 positionRWS = TransformObjectToWorld(PositionOS);
    float3 positionAWS = GetAbsolutePositionWS(positionRWS);
    float3 normalWS = normalize(TransformObjectToWorldNormal(NormalOS));

    float2 slope;
    float carve = BedCarve(positionAWS.xz, slope);
    positionRWS.y -= _BedCarveMetres * carve;

    float2 gradient = -_BedCarveMetres * slope;
    float3 up = normalWS.y < 0.0 ? -normalWS : normalWS;
    float3 tilted = normalize(float3(up.x - gradient.x, up.y, up.z - gradient.y));
    SurfaceNormalWS = normalWS.y < 0.0 ? -tilted : tilted;

    Position = TransformWorldToObject(positionRWS);
    Normal = normalize(TransformWorldToObjectNormal(SurfaceNormalWS));
}

void EvoBedSurface_float(float3 SurfaceNormalWS, float3 PositionAWS,
    out float3 BaseColor, out float3 NormalOut, out float Smoothness, out float Metallic,
    out float3 Specular)
{
    float3 geometric = normalize(SurfaceNormalWS);
    float3 blend = pow(abs(geometric), 6.0);
    blend /= max(1e-4, blend.x + blend.y + blend.z);

    float3 p = PositionAWS / max(0.001, _RippleMetres);
    float3 nx = BedNormal(p.zy);
    float3 ny = BedNormal(p.xz);
    float3 nz = BedNormal(p.xy);
    float3 detail =
        blend.x * float3(nx.z * sign(geometric.x), nx.y, nx.x) +
        blend.y * float3(ny.x, ny.z * sign(geometric.y), ny.y) +
        blend.z * float3(nz.x, nz.y, nz.z * sign(geometric.z));
    float3 n = normalize(detail + (geometric - float3(0.0, 1.0, 0.0)));

    float tone = saturate(0.5 + 0.5 * n.y);
    BaseColor = lerp(_SandDark.rgb, _SandLight.rgb, tone) * EvoDepthLight(PositionAWS.y);
    NormalOut = n;
    Smoothness = 0.15;
    Metallic = 0.0;
    Specular = EvoStoneSpecular();
}

#endif
