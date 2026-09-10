#ifndef EVOSIM_THEATRE_WATER_INCLUDED
#define EVOSIM_THEATRE_WATER_INCLUDED

// The procedural patterns the theatre's skin is made of: a cell mottle for tissue and a caustic
// net for the light coming through the surface. Both are here rather than in each shader so that
// a body and the sea bed cannot drift into two different caustics, which would read as two
// different suns.
//
// Nothing in this file is fetched or sampled from a texture. research/theatre-look/README.md's
// constraint is "no purchased assets and nothing fetched at run time", and a procedural pattern
// is also the only kind that can be reviewed in a diff.

// A cheap integer-free hash. The constants are the usual large irrationals used to decorrelate
// the three components; any set with no small rational relation between them works.
float3 EvoHash3(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));

    return frac(sin(p) * 43758.5453123);
}

float EvoHash1(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

// Worley / Voronoi in three dimensions: the distance to the nearest feature point, and the tone
// of the cell that point belongs to. Three dimensions rather than a triplanar pair of 2D fields
// because a body turns, and a triplanar mottle would swim across the skin as it did.
//
// f2 is returned as well so a caller can draw the cell wall (f2 - f1 is small at a wall).
void EvoVoronoi(float3 p, out float f1, out float f2, out float tone)
{
    float3 cell = floor(p);
    float3 frc = p - cell;

    f1 = 8.0;
    f2 = 8.0;
    tone = 0.5;

    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int z = -1; z <= 1; z++)
            {
                float3 offset = float3(x, y, z);
                float3 seed = EvoHash3(cell + offset);
                float3 rel = offset + seed - frc;

                float d = dot(rel, rel);

                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    tone = seed.x;
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }
        }
    }

    f1 = sqrt(f1);
    f2 = sqrt(f2);
}

// Two overlaid Voronoi edge fields, panned in opposite directions. A caustic net is the caustic
// of a wave field, and the cheap stand-in everyone uses is the wall of a cell pattern, because
// the walls are where neighbouring wavefronts fold onto each other. Two layers at different
// scales and speeds stop the pattern reading as one tiling grid (research/theatre-look, [CY2],
// [AM]).
float EvoCaustics(float2 p, float time)
{
    float net = 0.0;

    [unroll]
    for (int layer = 0; layer < 2; layer++)
    {
        float scale = layer == 0 ? 0.55 : 0.92;
        float speed = layer == 0 ? 0.035 : -0.023;

        float2 q = p * scale + float2(time * speed, time * speed * 0.7);

        float2 cell = floor(q);
        float2 frc = q - cell;

        float f1 = 8.0;
        float f2 = 8.0;

        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                float2 offset = float2(x, y);
                float2 seed = float2(EvoHash1(cell + offset),
                                     EvoHash1(cell + offset + 37.0));

                float2 rel = offset + seed - frc;
                float d = dot(rel, rel);

                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) { f2 = d; }
            }
        }

        float wall = saturate(1.0 - (sqrt(f2) - sqrt(f1)) * 2.2);
        net += pow(wall, 3.0);
    }

    return saturate(net * 0.62);
}

// How much of the surface's light still reaches a point. Zero at reach metres down and one at
// the waterline: the world has no top (CLAUDE.md's gotcha), so this is clamped above y = 0
// rather than allowed to brighten into the unbounded ray above the surface.
float EvoCausticFade(float3 positionWS, float reach)
{
    return saturate(1.0 + min(positionWS.y, 0.0) / max(0.01, reach));
}

#endif
