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

// The water column (2026-09-16, the look's design pass, R1 "lit top water"): x is the
// waterline's height, y the metres over which the lit water at the top falls to the deep
// colour, z is 1 when the lit water is on and 0 for the flat field the theatre had before, and
// w is 1 once TheatreSkin has pushed any of this at all.
float4 _EvoWaterColumn;

// The deep water, the colour everything used to fade towards, and the lit water at the
// waterline. Both as the skin set them; the deep falls back to the engine's fog colour for a
// scene that never built a skin.
float4 _EvoWaterDeep;
float4 _EvoWaterShallow;

// The underwater look's first batch (logbook/specs/underwater-look/brief.md), pushed by
// TheatreSkin.PushLook from TheatreLook's switches. Every one of them is off by default, and a
// flag of zero sends each function below down the branch the theatre drew before the batch, so
// a picture with every switch off is today's picture.
//
// _EvoLook: x is item 2 (the water's absorption and in-scatter), y is item 3 (light by depth and
// by the run's day), z is item 6 (the sky from below), w is 1 once the skin has pushed any of it.
// The census views hold all three at zero for their render (TheatreSkin.HoldTodaysLook).
float4 _EvoLook;

// xyz: absorption per metre for red, green and blue (pure water's 0.36, 0.07, 0.01 scaled
// down); w: the in-scatter's coefficient per metre, one for all three channels.
float4 _EvoAbsorb;

// x: Henyey-Greenstein's g; y: how much of the in-scatter follows it (TheatreLook.PhaseGain).
float4 _EvoPhase;

// x: one over the run's attenuation depth, per metre; y: the run's day factor at the second on
// screen as the picture takes it (the factor to the power TheatreLook.DayGamma, 1 is the mean
// day); z: the floor the depth light never falls under; w: the overcast, 0 clear to 1 dull.
float4 _EvoDaylight;

// xyz: how fast red, green and blue fall against the attenuation depth (red first).
float4 _EvoDepthRates;

// The underwater look's second batch (logbook/specs/underwater-look/brief-2.md), pushed by
// TheatreSkin.PushLook. _EvoLook2: x is item 1 (fade into the backdrop), y is 1 when the run's snow
// fields are on the snow texture (items 4 and 5), z is the drift (the owner's addition), w is item 3
// (the shafts in their place). Held at zero by a census view, as _EvoLook is.
float4 _EvoLook2;

// Item 1: x the metres from the eye the fade starts at; y the extinction's scattering part per
// metre; z the open water's brightness as a multiple of the column under the second batch.
float4 _EvoFade;

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

float  EvoWaterlineY()       { return _EvoWaterColumn.w > 0.5 ? _EvoWaterColumn.x : 0.0; }
float  EvoWaterReachMetres() { return _EvoWaterColumn.y > 0.01 ? _EvoWaterColumn.y : 18.0; }

// How far through the water a look reaches before it is all water: three reaches. The
// backdrop colours a direction by the column at the height that far along it, so a side
// view of a deep box runs from the lit water at the top of the frame to the deep at the
// bottom rather than one colour a few metres either side of the eye (the first lit-water
// pictures, 2026-09-16, were one flat teal for that reason).
float  EvoWaterViewMetres()  { return 3.0 * EvoWaterReachMetres(); }
float3 EvoWaterDeep()        { return _EvoWaterColumn.w > 0.5 ? _EvoWaterDeep.rgb : unity_FogColor.rgb; }

// The water's own colour at a height: the lit shallow at the waterline, falling by e-folds of
// the reach to the deep, and the deep alone with the lit water off. Clamped at the waterline,
// because the world has no top and the water above it is not brighter, it is not there.
float3 EvoWaterColour(float y)
{
    float3 deep = EvoWaterDeep();
    if (_EvoWaterColumn.z <= 0.5) return deep;

    float depth = max(0.0, EvoWaterlineY() - y);
    float t = exp(-depth / EvoWaterReachMetres());

    return lerp(deep, _EvoWaterShallow.rgb, t);
}

// The colour the water between the eye and a point fades that point towards: the column at
// the height halfway between the two, which is the fog colour of a ray through a column that
// brightens upward, to first order. The eye's height is clamped at the waterline for the same
// reason as above.
float3 EvoWaterColourAlong(float3 positionWS)
{
    float eyeY = min(GetCameraPositionWS().y, EvoWaterlineY());

    return EvoWaterColour(0.5 * (eyeY + positionWS.y));
}

bool EvoLookWater() { return _EvoLook.x > 0.5; }
bool EvoLookDepth() { return _EvoLook.y > 0.5; }
bool EvoLookSky()   { return _EvoLook.z > 0.5; }

// The second batch's item 1 acts only with the first batch's water on: it replaces how the water
// mixes, not whether there is any.
bool EvoLookFade()  { return _EvoLook2.x > 0.5 && _EvoLook.x > 0.5; }
bool EvoLookPlace() { return _EvoLook2.w > 0.5; }

// The run's day as a brightness, 1 with item 3 off: everything the sun lights (the in-scatter,
// the shafts, the snow, the window) is as bright as the run's surface light at the second.
float EvoDayGain() { return EvoLookDepth() ? max(0.0, _EvoDaylight.y) : 1.0; }

// Henyey-Greenstein, normalised so that light scattered evenly in every direction is 1 rather
// than 1 / 4 pi: forward peaked for g above zero, 45 straight ahead and 0.06 straight back at
// g 0.8. cosTheta is between the light's travel and the direction it leaves in.
float EvoPhaseHG(float cosTheta, float g)
{
    float g2 = g * g;
    float denominator = max(1e-4, 1.0 + g2 - 2.0 * g * cosTheta);

    return (1.0 - g2) / (denominator * sqrt(denominator));
}

// How much brighter the water scatters toward an eye looking along viewDir (unit, eye to
// point) than its mean: 1 with the gain at zero, and above 1 where the look runs back up the
// refracted sun's ray, so the water glows where the light comes from.
float EvoPhaseTerm(float3 viewDir)
{
    float cosTheta = dot(EvoSunRayWS(), -viewDir);
    float p = EvoPhaseHG(cosTheta, clamp(_EvoPhase.x, 0.0, 0.95));

    return max(0.0, 1.0 + _EvoPhase.y * (p - 1.0));
}

// Metres of water that count toward the fade when a look crosses d metres of it (item 1): none
// over the first start metres, a ramp from there to the full distance (_EvoFade.w), then every
// metre. The integral of a weight that rises from 0 at start to 1 at full, so the fade has no kink.
// The ramp runs to 12 m rather than to twice the start since iteration 11: a rock at 5 to 10 m
// kept its colour and shape only once it had most of its own light.
float EvoFadeMetres(float d)
{
    float start = max(1e-3, _EvoFade.x);
    float full = max(start + 1.0, _EvoFade.w);
    float ramp = full - start;

    if (d <= start) return 0.0;
    if (d <= full) return (d - start) * (d - start) / (2.0 * ramp);

    return d - 0.5 * (start + full);
}

// The water the world has, pushed by TheatreSkin.Dress: the box from its floor to the waterline
// (xyz of each; _EvoWaterLow.w is 1 once pushed) and the tank's circle (x and z of its centre, its
// radius, and w at 1 for a tank). Read by EvoWaterPathMetres alone.
float4 _EvoWaterLow;
float4 _EvoWaterHigh;
float4 _EvoWaterTank;

// Metres of water between the eye and a point: only the part of the straight line that is inside
// the world's water. The first stills of the look (2026-10-07) took the whole eye distance, and
// the bed view, whose camera stands outside the tank, filled the air outside the glass with
// in-scatter and turned the earth wall to cyan glass. So the segment is clipped to the box (its
// top is the waterline) and to the tank's circle; a point just inside the glass seen from outside
// it is behind a few centimetres of water and nothing more. Before a skin has dressed a world the
// fallback is the line less the part above the waterline, which is what the look first did.
float EvoWaterPathMetres(float3 positionWS)
{
    float3 eye = GetCameraPositionWS();
    float3 delta = positionWS - eye;
    float d = length(delta);

    if (_EvoWaterLow.w < 0.5)
    {
        float waterline = EvoWaterlineY();

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

// Whether the eye is in the world's water: under the waterline, inside the box and, in a tank,
// inside the glass. True before a skin has dressed a world and said where the water is.
bool EvoEyeInWater()
{
    float3 eye = GetCameraPositionWS();
    if (_EvoWaterLow.w < 0.5) return eye.y < EvoWaterlineY();

    if (any(eye < _EvoWaterLow.xyz) || any(eye > _EvoWaterHigh.xyz)) return false;

    if (_EvoWaterTank.w > 0.5)
    {
        float2 o = eye.xz - _EvoWaterTank.xy;
        if (dot(o, o) > _EvoWaterTank.z * _EvoWaterTank.z) return false;
    }

    return true;
}

// The open water seen along a direction (unit, from the eye): what the camera sees where nothing
// stands, painted by TheatreBackdrop.shader, and since the second batch also the colour every far
// thing fades into (EvoMixFog). The column at the height three reaches along the look from the
// eye, clamped at the waterline; with the first batch's water on, brightened toward the sun by the
// phase term; as bright as the run's day; and under the second batch's item 1, scaled by its own
// gain for an eye in the water, which keeps the open water below the lit things in it. An eye
// outside the water (the bed view's, beyond the glass) keeps the column as it was: what it sees
// around the tank is a backdrop, not water it is in.
float3 EvoBackdrop(float3 direction)
{
    float eyeY = min(GetCameraPositionWS().y, EvoWaterlineY());
    float3 colour = EvoWaterColour(eyeY + direction.y * EvoWaterViewMetres());

    if (EvoLookWater()) colour *= EvoPhaseTerm(direction);
    colour *= EvoDayGain();
    if (EvoLookFade() && EvoEyeInWater()) colour *= max(0.0, _EvoFade.z);

    return colour;
}

// What a far thing fades into under item 1: the open water along the look at the second batch's
// gain, whether the eye is in the water or not. The same as the drawn backdrop for an eye in the
// water, which is the promise; an eye beyond the glass sees the tank's water fade to this and the
// world around the tank keep its column (EvoBackdrop), so the close view through the glass does not
// fade into a brighter water than it is looking through.
float3 EvoFadeTarget(float3 direction)
{
    float eyeY = min(GetCameraPositionWS().y, EvoWaterlineY());
    float3 colour = EvoWaterColour(eyeY + direction.y * EvoWaterViewMetres());

    if (EvoLookWater()) colour *= EvoPhaseTerm(direction);

    return colour * (EvoDayGain() * max(0.0, _EvoFade.z));
}

// URP's MixFog, with the column's colour in place of the one flat fog colour. Every surface
// that mixes towards the fog (a body, the bed, the ceiling) goes through this; the additive
// things (the snow, the shafts) fade towards nothing and never did mix.
//
// With item 2 on, the one grey factor becomes two terms (the brief's formula): the surface's
// colour kept by the water's absorption, per channel and red first, and the light the water
// itself scatters toward the eye over the same metres, at its own lower rate. The in-scatter is
// the column's colour at the height the look passes through, brightened toward the sun by the
// phase function, and as bright as the run's day when item 3 is on.
float3 EvoMixFog(float3 colour, float fogFactor, float3 positionWS)
{
    // One return, so the compiler does not read the branch as a path that leaves the result unset.
    float3 mixed = colour;

    if (EvoLookFade())
    {
        // The second batch's item 1: the thing fades into the open water seen behind it, over the
        // extinction of absorption and scattering together, so nothing in the water is brighter
        // than the water it stands against at the far end, and the near end keeps its black point.
        // Absorption is per channel, red first; the scattering part is grey. From the start
        // distance on (EvoFadeMetres), so nothing near the lens loses anything.
        float d = EvoFadeMetres(EvoWaterPathMetres(positionWS));
        float3 kept = exp(-(_EvoAbsorb.xyz + _EvoFade.yyy) * d);

        float3 view = -normalize(GetWorldSpaceViewDir(positionWS));

        mixed = colour * kept + EvoFadeTarget(view) * (1.0 - kept);
    }
    else if (EvoLookWater())
    {
        float d = EvoWaterPathMetres(positionWS);
        float3 kept = exp(-_EvoAbsorb.xyz * d);
        float scattered = 1.0 - exp(-_EvoAbsorb.w * d);

        float3 view = -normalize(GetWorldSpaceViewDir(positionWS));
        float3 inscatter = EvoWaterColourAlong(positionWS) * (EvoPhaseTerm(view) * EvoDayGain());

        mixed = colour * kept + inscatter * scattered;
    }
    else
    {
        mixed = MixFogColor(colour, EvoWaterColourAlong(positionWS), fogFactor);
    }

    return mixed;
}

// How much of the surface's light reaches a point at its depth, per channel, for the key, the
// ambient and a leaf's glow (item 3): one at the waterline on the mean day, falling by e-folds
// of the run's own attenuation depth with red first, times the run's day factor, and never
// under the floor, so the deep is dim and not black. One with item 3 off.
float3 EvoDepthLight(float3 positionWS)
{
    if (!EvoLookDepth()) return float3(1.0, 1.0, 1.0);

    float depth = max(0.0, EvoWaterlineY() - positionWS.y);
    float3 fall = exp(-depth * _EvoDaylight.x * _EvoDepthRates.xyz);

    // The floor before the day, so the deep is dimmer in winter than in summer too, and a
    // brighter season is brighter everywhere in a frame (the meter relies on that).
    return max(fall, _EvoDaylight.zzz) * EvoDayGain();
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
//
// Split in two for the underwater look's shafts (item 4): EvoCausticGain is the focusing itself,
// the light the surface sends to a point as a multiple of the flat water's, and the net is that
// gain with the flat water dropped. The arithmetic is the one this function always did.
float EvoCausticGain(float3 positionWS, float time)
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

    return 1.0 / max(0.14, abs(area));
}

float EvoCausticNet(float3 positionWS, float time)
{
    float gain = EvoCausticGain(positionWS, time);

    // Subtracting a little under one leaves the filaments and drops the flat water between them,
    // which is what a caustic net is: bright lines on dark, and not a bright wash.
    return saturate((gain - 0.9) * 0.8);
}

// The sun's light arriving at a point under the surface along the refracted ray, per channel,
// before the caustics' focusing and before it turns toward an eye (items 4 and 5): absorbed over
// the slant path down, faded with depth, and as bright as the run's day. The fade is the run's
// own attenuation depth when item 3 is on, so the shafts give out where the bodies' light does,
// and the theatre's light reach otherwise, which is where the caustics and the old shafts stop.
float3 EvoSunAt(float3 positionWS)
{
    float depth = max(0.0, EvoWaterlineY() - positionWS.y);
    float3 ray = EvoSunRayWS();
    float slant = depth / max(0.15, -ray.y);

    float3 fall = EvoLookDepth()
        ? exp(-depth * _EvoDaylight.x * _EvoDepthRates.xyz)
        : exp(-depth / EvoLightReachMetres()).xxx;

    return exp(-_EvoAbsorb.xyz * slant) * fall * EvoDayGain();
}

// How strongly the beam through a point burns, 0 to 1, for the marched shafts (item 4): read where
// the refracted sun's ray through the point left the surface, as how concave the water is there
// toward the sun (EvoShaftGain's question, asked of every point rather than of nine quads). A beam
// is then the line of points one patch of the surface lit, as distinct a metre down as twenty. The
// focusing at the point's own depth (EvoCausticGain) is too weak in the top few metres to draw a
// beam by itself: at three metres under it moves the light by a quarter either way, which is why
// the first marched stills (2026-10-07) showed only faint streaks.
float EvoBeamAt(float3 positionWS, float time)
{
    float depth = max(0.0, EvoWaterlineY() - positionWS.y);
    float3 ray = EvoSunRayWS();
    float2 top = positionWS.xz - depth * ray.xz / max(0.15, -ray.y);

    float2 slope;
    float curvature;
    EvoRipple(top, time, slope, curvature);

    // Twice the longest train's own curvature scale, since three trains add.
    float k = 6.2831853 / EvoWaveLengthMetres();
    float scale = max(1e-4, 2.0 * EvoWaveMetres() * k * k);

    return saturate(-curvature / scale);
}

// A fixed offset in [0, 1) per pixel, for starting a march: Jimenez's interleaved gradient
// noise. A function of the pixel and nothing else, so a still camera draws the same picture
// every frame and a film does not shimmer from it.
float EvoPixelDither(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
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
// reading of it was that it was still very geometric: rounding leaves every face flat and
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
// How many more wrinkles a big part carries than a small one (the look's design pass, D5):
// one at eight centimetres and below, rising as the square root of the smallest half extent
// to three at seventy two, so a metre-long body is not a hand-sized one inflated. The lobes
// stay at the part's own size, because a lobe is the part's shape and a wrinkle is its skin.
float EvoCarveDetail(float smallestHalfMetres)
{
    return clamp(sqrt(smallestHalfMetres / 0.08), 1.0, 3.0);
}

float EvoCarve(float3 p, float seed, float lobeGain, float wrinkleGain, float detail, out float3 gradient)
{
    const float lobeCycles = 1.8;
    float wrinkleCycles = 5.0 * lobeCycles * detail;

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
