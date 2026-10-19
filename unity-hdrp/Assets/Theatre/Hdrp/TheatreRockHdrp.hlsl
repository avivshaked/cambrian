#ifndef EVOSIM_THEATRE_ROCK_HDRP_INCLUDED
#define EVOSIM_THEATRE_ROCK_HDRP_INCLUDED

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

// This file: The reef rock (TheatreRock.shader).

#include "TheatreNoiseHdrp.hlsl"
#include "TheatreDepthHdrp.hlsl"


float RockValue2(float2 q)
{
    float2 cell = floor(q);
    float2 f = q - cell;
    f = f * f * (3.0 - 2.0 * f);
    float a = EvoHash1(cell);
    float b = EvoHash1(cell + float2(1.0, 0.0));
    float c = EvoHash1(cell + float2(0.0, 1.0));
    float d = EvoHash1(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float RockHeight(float2 p)
{
    float lumps = 0.65 * RockValue2(p) + 0.35 * RockValue2(p * 2.4 + 7.1);
    float crease = 1.0 - saturate(abs(RockValue2(p * 0.37 + 3.3) - 0.5) * 9.0);
    return lumps - 0.25 * crease;
}

float3 RockNormal(float2 p)
{
    const float e = 0.03;
    float h = RockHeight(p);
    float dx = RockHeight(p + float2(e, 0.0)) - h;
    float dy = RockHeight(p + float2(0.0, e)) - h;
    return normalize(float3(-dx / e * _GrainStrength, -dy / e * _GrainStrength, 6.0));
}

void EvoRockSurface_float(float3 PositionOS, float3 NormalWS, float4 Colour,
    out float3 BaseColor, out float3 NormalOut, out float Smoothness, out float Metallic,
    out float Occlusion, out float3 Specular)
{
    float3 geometric = normalize(NormalWS);
    float3 seed = EvoCarveOffset(_RockSeed);
    float3 local = PositionOS + seed;

    float3 blend = pow(abs(geometric), 4.0);
    blend /= max(1e-4, blend.x + blend.y + blend.z);
    float3 p = local / max(0.02, _GrainMetres);
    float3 nx = RockNormal(p.zy);
    float3 ny = RockNormal(p.xz);
    float3 nz = RockNormal(p.xy);
    float3 detail =
        blend.x * float3(nx.z * sign(geometric.x), nx.y, nx.x) +
        blend.y * float3(ny.x, ny.z * sign(geometric.y), ny.y) +
        blend.z * float3(nz.x, nz.y, nz.z * sign(geometric.z));
    float3 axis = blend.x * float3(sign(geometric.x), 0, 0)
                + blend.y * float3(0, sign(geometric.y), 0)
                + blend.z * float3(0, 0, sign(geometric.z));
    float3 n = normalize(detail + (geometric - axis));

    float cavity = Colour.r;
    float table = saturate(Colour.g * 2.0 - 1.0);
    float onCap = Colour.b;
    float y = PositionOS.y;
    float thickness = max(0.05, _CapTopLocal - _CapUnderLocal);
    float height = saturate((y - _CapUnderLocal) / thickness);

    float3 g1, g2;
    float m1 = EvoValueNoise(local / max(0.1, _MottleMetres), g1);
    float m2 = EvoValueNoise(local / max(0.1, 0.33 * _MottleMetres) + 11.0, g2);
    float mottle = 0.65 * m1 + 0.35 * m2;

    float3 rock = lerp(_RockDeep.rgb, _RockMid.rgb, onCap * smoothstep(0.0, 1.0, height));
    float crust = onCap * saturate(n.y) * smoothstep(0.35, 0.95, height) * (0.35 + 0.65 * table);
    crust *= smoothstep(0.30, 0.62, mottle) * (1.0 - 0.6 * cavity);
    rock = lerp(rock, _RockCrust.rgb, saturate(crust));
    rock *= lerp(1.0 - _MottleStrength * 0.5, 1.0 + _MottleStrength * 0.5, m2);
    float3 g3;
    float speck = EvoValueNoise(local / 0.07, g3);
    rock *= 0.8 + 0.4 * speck;
    rock *= 1.0 - _CavityDark * cavity * cavity;

    // The URP rock darkened its light under the cap by hand. HDRP's shadows and occlusion do
    // that for the direct and the indirect light here, so the hand-made shade goes into the
    // occlusion only, where it stands in for the cap's shelter from the water's light.
    float below = _CapUnderLocal - y;
    float reach = max(1.0, _CapRadius / 0.3);
    float underCap = saturate(below / 0.3 + 1.0) * (1.0 - smoothstep(0.6 * reach, reach, below));
    float facesDown = saturate(-geometric.y);
    float shade = lerp(1.0, _ShadeUnder, saturate(max(underCap * (1.0 - onCap * table), onCap * facesDown)));

    BaseColor = saturate(rock) * EvoDepthLight(GetAbsolutePositionWS(TransformObjectToWorld(PositionOS)).y);
    NormalOut = n;
    Smoothness = 0.2;
    Metallic = 0.0;
    Occlusion = shade;
    Specular = EvoStoneSpecular();
}

#endif
