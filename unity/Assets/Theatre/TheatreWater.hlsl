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


// ---------------------------------------------------------------------------------------------
// The carve: the field the bodies and the sea bed are cut away by.
//
// Why there is one. The first day's skin rounded the boxes and mottled them, and the owner's
// reading of it was that it was "still very very geometric": rounding leaves every face flat and
// every pair of faces parallel, and a mottle is paint on a flat face. What separates a grown
// thing from a built one is the silhouette, so the second day cuts into the shape itself
// (research/theatre-look, "Skin as shape"), and everything below exists to make that cut and to
// shade it.
//
// Why value noise with an analytic gradient rather than a hashed lattice sampled three times.
// A carve is only visible if it shades, and shading needs the gradient of the same field the
// vertex stage displaced by. Finite differences of a hashed field cost three more evaluations
// per axis and read the lattice back out as faceting; the quintic interpolant below carries its
// own derivative for free and has a zero first derivative at every cell corner, so the field and
// its gradient are both continuous and the grid does not show. The construction is Inigo
// Quilez's, "value noise derivatives" (https://iquilezles.org/articles/morenoise/).

/// A scalar hash of a lattice corner. The float2 form above, one dimension wider.
float EvoHash1(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
}

// Value noise in three dimensions, in [0, 1], with its gradient with respect to p.
float EvoValueNoise(float3 p, out float3 gradient)
{
    float3 cell = floor(p);
    float3 f = p - cell;

    // 6t^5 - 15t^4 + 10t^3 and its derivative. See the note above on why this and not smoothstep.
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float3 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

    float a = EvoHash1(cell + float3(0.0, 0.0, 0.0));
    float b = EvoHash1(cell + float3(1.0, 0.0, 0.0));
    float c = EvoHash1(cell + float3(0.0, 1.0, 0.0));
    float d = EvoHash1(cell + float3(1.0, 1.0, 0.0));
    float e = EvoHash1(cell + float3(0.0, 0.0, 1.0));
    float g = EvoHash1(cell + float3(1.0, 0.0, 1.0));
    float h = EvoHash1(cell + float3(0.0, 1.0, 1.0));
    float i = EvoHash1(cell + float3(1.0, 1.0, 1.0));

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k3 = e - a;
    float k4 = a - b - c + d;
    float k5 = a - c - e + h;
    float k6 = a - b - e + g;
    float k7 = -a + b + c - d + e - g - h + i;

    gradient = du * float3(
        k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
        k2 + k4 * u.x + k5 * u.z + k7 * u.z * u.x,
        k3 + k5 * u.y + k6 * u.x + k7 * u.x * u.y);

    return k0 + k1 * u.x + k2 * u.y + k3 * u.z +
           k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x +
           k7 * u.x * u.y * u.z;
}

// Where one body's impressions sit. Two bodies with the same shape must not carry the same
// dents, or a world of them reads as a stamped batch; the seed is mixed from the creature's own
// id and its part's index (TheatrePalette.SeedOf) and arrives in [0, 1), which is the range that
// leaves a float all of its precision for the multiply below.
float3 EvoCarveOffset(float seed)
{
    return 64.0 * frac(seed * float3(97.13, 61.71, 43.37) + float3(0.13, 0.57, 0.91));
}

// The carve field, in [0, 1], and its gradient, both in the part's own object units.
//
// Two octaves, as the shape asks for: lobes at about the part's own size, because a body that
// is thinner in one place than another reads as grown, and wrinkles at a fifth of that, because
// a surface with no small structure reads as moulded. The two gains are per body and per guild
// (TheatrePalette): a producer is more lobed, a stomach more wrinkled, a structural part
// smoother. The sum is normalised by the gains, so changing the character changes the shape of
// the carve and never its depth, which is the one number the size bound is stated in.
float EvoCarve(float3 p, float seed, float lobeGain, float wrinkleGain, out float3 gradient)
{
    const float lobeCycles = 1.8;
    const float wrinkleCycles = 5.0 * lobeCycles;

    float3 offset = EvoCarveOffset(seed);

    float3 lobeGradient, wrinkleGradient;
    float lobe = EvoValueNoise(p * lobeCycles + offset, lobeGradient);
    float wrinkle = EvoValueNoise(p * wrinkleCycles + offset * 1.7, wrinkleGradient);

    float total = max(1e-4, lobeGain + wrinkleGain);

    float raw = (lobeGain * lobe + wrinkleGain * wrinkle) / total;

    float3 rawGradient = (lobeGain * lobeCycles * lobeGradient +
                          wrinkleGain * wrinkleCycles * wrinkleGradient) / total;

    // Stretched across the band the noise actually occupies, and this is the difference between
    // a carve and a shrink. Trilinear value noise is an average of eight uniform numbers and so
    // clusters hard around a half: the first carved pictures (r35-s1 at t=5,000 s, carve 0.2)
    // varied by about a seventh of the dial across a whole body, which came out as every body
    // being uniformly a centimetre smaller and not one dent anywhere. The bulk of the
    // distribution is mapped onto the whole of [0, 1] instead, with a smoothstep rather than a
    // clamp so that the field stays differentiable and the normals below stay continuous.
    const float low = 0.28;
    const float high = 0.72;

    float t = saturate((raw - low) / (high - low));

    // The chain rule again: the shaping is a function of the field, so the field's gradient is
    // multiplied by the shaping's slope. That slope reaches 1.5 / (high - low), which is why the
    // stretch makes the wrinkles shade as well as show on the outline.
    gradient = rawGradient * (6.0 * t * (1.0 - t) / (high - low));

    return t * t * (3.0 - 2.0 * t);
}

// How much deeper the carve goes near one joint anchor.
//
// Why deepen it there at all. Two parts meet by one box entering another, and no amount of
// surface detail hides that: what a joint looks like on an animal is a pinch, tissue drawn in
// on both sides of the hinge. The anchor is a point the phenotype already carries, so both the
// parent and the child can be told where their junction is and can pull away from it, which
// also opens the gap the neck is drawn in (TheatrePalette.BuildNecks).
//
// anchor.xyz is the joint in this visual's own object units and anchor.w its reach; a reach of
// zero is the "no joint here" case and costs one comparison. Not called "point", which HLSL
// reserves for a geometry shader's primitive type.
float EvoPinch(float3 p, float4 anchor)
{
    if (anchor.w <= 0.0) return 0.0;

    float d = length(p - anchor.xyz) / anchor.w;

    return 1.0 - smoothstep(0.0, 1.0, saturate(d));
}

#endif
