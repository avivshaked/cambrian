#ifndef EVOSIM_THEATRE_WATER_INCLUDED
#define EVOSIM_THEATRE_WATER_INCLUDED

// The procedural patterns the theatre's skin is made of: a cell mottle for tissue, the wave field
// the sea's surface is, the light that field focuses, and the noise the bodies are carved by.
// They are here rather than in each shader so that the surface plane, the shafts under it, a body
// and the sea bed cannot drift into two different seas, which would read as two different suns.
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

// ---------------------------------------------------------------------------------------------
// The surface: one sea, read by everything the sun reaches.
//
// Why this replaced a pattern that only looked like caustics. The first three days were about
// bodies, and the water got a colour, a fog, snow and a caustic net drawn from a Voronoi cell
// wall, which is the cheap stand-in everyone uses [CY2] [AM]. It was a net from nowhere: there
// was no surface above it, nothing it was the shadow of, and no reason for it to agree with
// anything. The owner ran round 36 and said they could not see the sun, the shimmer or the
// underwater ripple, and they were right to, because none of those existed
// (logbook/specs/skin-spec-4.md). So the wave field is written down once, here, and the surface
// plane, the shafts, the bodies and the sea bed are all lit from it. A caustic is then the
// surface above a body, focused, rather than a picture of one.
//
// The four numbers the sea is made of arrive as globals rather than as material properties,
// pushed once by TheatreSkin.PushWater. A material property would be a fifth copy of each dial
// (surface, shafts, body, neck, bed), and the moment two of them disagreed the light on a body
// would stop being the light coming through the ceiling above it, which is the one thing this
// day is for. They sit outside UnityPerMaterial deliberately: they are not material properties,
// so they belong among the shader's globals the way the engine's own light and time uniforms do.

// x: wave amplitude in metres. y: wavelength of the longest train in metres.
// z: how much of the true phase speed the trains run at. w: metres the surface light reaches.
float4 _EvoRipple;

// Unit, pointing at the sun from the water. Set from the scene's directional light.
float4 _EvoSun;

// Unit, pointing down along the sun's ray after it has been refracted into the water.
float4 _EvoSunRay;

// Each dial falls back rather than refusing, because a scene that never built a skin still draws
// bodies and a bed, and a zero wavelength there would divide the sea by nothing. The fallbacks
// are TheatreSkin's own defaults.
float EvoWaveMetres()       { return _EvoRipple.x > 0.0  ? _EvoRipple.x : 0.045; }
float EvoWaveLengthMetres() { return _EvoRipple.y > 0.01 ? _EvoRipple.y : 1.6; }
float EvoWaveSpeed()        { return _EvoRipple.z > 0.0  ? _EvoRipple.z : 0.5; }
float EvoLightReachMetres() { return _EvoRipple.w > 0.01 ? _EvoRipple.w : 18.0; }

float3 EvoSunDirectionWS()
{
    return dot(_EvoSun.xyz, _EvoSun.xyz) > 1e-4
        ? normalize(_EvoSun.xyz)
        : normalize(float3(0.28, 0.92, 0.27));
}

float3 EvoSunRayWS()
{
    return dot(_EvoSunRay.xyz, _EvoSunRay.xyz) > 1e-4
        ? normalize(_EvoSunRay.xyz)
        : normalize(float3(0.20, -0.96, 0.19));
}

// The wave field: how high the water stands over a point, how it tilts there, and how it curves.
//
// Three directional trains rather than a noise, for two reasons. A wave has a direction and a
// speed and a noise has neither, so a noise surface boils where a sea travels. And the second
// derivative of a sine is another sine: the curvature the caustics are built out of falls out of
// the same arithmetic, where a noise field would have to be sampled twice more per axis.
//
// The trains are set by deep water dispersion, c = sqrt(g L / 2 pi), so the long one runs ahead
// of the short ones and the pattern never repeats on a beat. The whole set is then slowed by the
// speed dial, which is not physics: it is there because nobody has watched this sea yet, and a
// ripple that beats about once a second may well read as rain on a ceiling rather than as a calm
// day. The dial puts it back to the true speed at 1.
//
// The amplitude is shared out so that the sum stays inside the dial: the gains add to one.
float EvoRipple(float2 xz, float time, out float2 slope, out float curvature)
{
    float amplitude = EvoWaveMetres();
    float wavelength = EvoWaveLengthMetres();
    float speed = EvoWaveSpeed();

    // Three headings at no simple angle to each other, so the sum never reads as a grid.
    const float2 heading0 = float2(0.99503, 0.09950);
    const float2 heading1 = float2(-0.31623, 0.94868);
    const float2 heading2 = float2(0.62470, -0.78087);

    float height = 0.0;
    slope = float2(0.0, 0.0);
    curvature = 0.0;

    [unroll]
    for (int train = 0; train < 3; train++)
    {
        // Spelled out rather than read from a small array, because an index into one is the one
        // construction in this file that a shader compiler is entitled to refuse.
        float2 heading = train == 0 ? heading0 : (train == 1 ? heading1 : heading2);
        float fraction = train == 0 ? 1.0 : (train == 1 ? 0.53 : 0.27);
        float gain = train == 0 ? 0.58 : (train == 1 ? 0.28 : 0.14);

        float metres = max(0.05, wavelength * fraction);
        float k = 6.2831853 / metres;
        float phaseSpeed = speed * sqrt(9.81 * metres / 6.2831853);
        float a = amplitude * gain;

        float phase = k * dot(heading, xz) - k * phaseSpeed * time;
        float s = sin(phase);
        float c = cos(phase);

        height += a * s;
        slope += (a * k * c) * heading;

        // The Laplacian of one train: the heading is a unit vector, so its two second
        // derivatives sum to -a k^2 sin whatever direction it runs in.
        curvature += -a * k * k * s;
    }

    return height;
}

// The surface's normal at a point, pointing up into the air.
float3 EvoRippleNormal(float2 xz, float time)
{
    float2 slope;
    float curvature;
    EvoRipple(xz, time, slope, curvature);

    // The normal of h(x, z) is (-dh/dx, 1, -dh/dz), the same construction the sea bed's carve
    // uses, and normalised rather than approximated: the surface is looked at edge on near the
    // window's rim, which is where a small angle approximation is worst.
    return normalize(float3(-slope.x, 1.0, -slope.y));
}

// How much of the sun's light the surface gathers onto a point, in [0, 1].
//
// This is the fourth day's fourth item and the reason the wave field exists. A bundle of rays
// that leaves a curved surface and falls a depth d covers, at the bottom, about
// (1 + d (1 - 1/n) lap h) times the area it started with: a surface curving down towards the
// light draws the bundle in, and where that factor reaches zero the bundle has folded onto a
// line, which is a cusp and is where a real caustic is brightest. So the brightness is the
// reciprocal of that area, clamped at the fold, because a picture cannot hold a division by
// nothing.
//
// The sample is taken where the ray that lands here met the surface, one refracted ray back up,
// so the net slides sideways with the sun and with depth instead of sitting under the body like
// a decal.
//
// The depth the focusing is worked out over is capped at the light's reach. Below that the factor
// swings through zero several times a metre, which is finer than any of these pictures can
// resolve and would come back as noise; and below that the fade has taken the caustics away in
// any case.
float EvoCausticNet(float3 positionWS, float time)
{
    float depth = max(0.0, -positionWS.y);
    float focusing = min(depth, EvoLightReachMetres());

    float3 ray = EvoSunRayWS();
    float2 shift = focusing * ray.xz / max(0.15, -ray.y);

    float2 slope;
    float curvature;
    EvoRipple(positionWS.xz - shift, time, slope, curvature);

    // 1 - 1 / 1.333: how much of a slope at the surface becomes sideways travel below it.
    const float bend = 0.248;

    float area = 1.0 + focusing * bend * curvature;
    float gain = 1.0 / max(0.14, abs(area));

    // Subtracting a little under one leaves the filaments and drops the flat water between them,
    // which is what a caustic net is: bright lines on dark, and not a bright wash.
    return saturate((gain - 0.9) * 0.8);
}

// How brightly one shaft of light burns, read where it leaves the surface.
//
// A shaft is a bundle of rays the surface sent down together, so its brightness is the same
// question the caustics ask, asked at one point rather than over a body: bright where the water
// above it is concave towards the sun. Divided by the field's own curvature scale, so the dials
// change how fast the shafts breathe and never how many of them are lit.
//
// It never falls to nothing. A shaft that switched off would read as a flicker; a real one dims
// and brightens.
float EvoShaftGain(float2 topXZ, float time)
{
    float2 slope;
    float curvature;
    EvoRipple(topXZ, time, slope, curvature);

    float k = 6.2831853 / EvoWaveLengthMetres();
    float scale = max(1e-4, EvoWaveMetres() * k * k);

    return 0.45 + 0.55 * saturate(-curvature / scale);
}

// How much of the water's own furniture this eye is allowed to see.
//
// The surface plane and the light shafts are things seen from under water. Above the waterline
// there is nothing for a beam to be in and the ceiling is not a ceiling, so an eye up there sees
// neither: that is one of the two ways the fourth day keeps its promise that the snapshot's top
// and iso views are unchanged, and the only one that also holds for a viewer flying the world in
// Play mode (the other is SnapshotCamera, which turns both off outright for the four views that
// stand outside the box and photograph the census).
//
// Faded in over the first metre under, rather than switched, so that a camera crossing the
// surface does not flash.
//
// The eye's own position, not the pixel's: whether a beam is there at all is a question about
// where it is being looked at from.
float EvoSeenFromBelow(float waterlineY)
{
    float below = waterlineY - GetCameraPositionWS().y;

    return saturate(below);
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
