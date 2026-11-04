#ifndef EVOSIM_THEATRE_SNOW_INCLUDED
#define EVOSIM_THEATRE_SNOW_INCLUDED

// The run's own snow, for the underwater look's second batch (logbook/specs/underwater-look/brief-2.md,
// items 4 and 5): the farm's snow fields at the frame's second, read by TheatreRunSnow and pushed by
// TheatreSkin.PushSnow as one texture over the world's columns, red the column's density in J per
// cubic metre and green the floor cell's stock in J, interpolated in time on the CPU. The shell snow
// (TheatreSnowShells.shader) scales its motes by the density and the bed (TheatreBed.shader) takes
// a pale dusting from the floor's stock, both against fixed references, never rescaled per frame.
//
// A shader that includes this must include URP's Core.hlsl and TheatreWater.hlsl first.

TEXTURE2D(_EvoSnowField);
SAMPLER(sampler_EvoSnowField);

// x, z: the world position of the grid's corner (column 0's low edge); y: one over the column's
// size in metres; w: the columns across x (z is the texture's other side, _EvoSnowCells.x).
float4 _EvoSnowGrid;
float4 _EvoSnowCells;

// x: the reference column density, J per cubic metre; y: the reference floor stock, J; z: how pale
// the bed goes at the reference; w: the least density ratio a column counts as for the shells' motes.
float4 _EvoSnowRef;

// The shells' lens, pushed for every render by the snapshot (TheatreSkin.PushSnowLens). x: the focus
// distance along the view axis, m (0: the scene's depth at the frame's centre, as an autofocus takes
// it); y: the lens's aperture, its diameter in m (0: every mote sharp, as before 2026-10-19); z: the
// most a mote is blurred, in pixels of radius; w: the power of a blurred mote's area ratio its
// brightness keeps (1: its light spread over the larger disc and conserved).
float4 _EvoSnowLens;

// The flakes. x: the spread of the motes' sizes, the standard deviation of their log radius, with
// the mean area kept (0: one size a shell); y: the share of motes that are large flakes; z: how many
// times larger; w: how irregular a flake's outline is (0: round).
float4 _EvoSnowFlake;

// x: how soft a flake's edge is, as a share of its radius (0: a pixel's edge); y: how far a large flake is a
// loose aggregate, its brightness divided by its size over the shell's to this power (0: as bright as a small
// one); z: 1 keeps each flake's light when the lens or the fluff spreads it, over the area it is drawn on, as a
// lens does (0: the older rule, _EvoSnowLens.w's power of the half-light radius); w unused.
float4 _EvoSnowFluff;

// Set by the shells' fragment before the walk: the focus it settled on, and the nearest mote whose
// core covers the pixel, which HDRP's snow writes as depth for its own depth of field.
static float evoSnowFocus;
static float evoSnowNearT;

bool EvoRunSnow() { return _EvoLook2.y > 0.5; }

// The recorded snow at a world position: x the column's density, y the floor's stock. Bilinear
// between column centres. Zero outside the grid.
float2 EvoSnowAt(float3 positionWS)
{
    float2 cells = max(float2(1.0, 1.0), float2(_EvoSnowGrid.w, _EvoSnowCells.x));
    float2 index = (positionWS.xz - _EvoSnowGrid.xz) * _EvoSnowGrid.y;

    if (any(index < 0.0) || any(index > cells)) return float2(0.0, 0.0);

    // The texture's u runs along z and v along x: the dump's order is ix * cellsZ + iz, which
    // TheatreSkin uploads row by row, one row per ix.
    float2 uv = float2(index.y / cells.y, index.x / cells.x);

    return SAMPLE_TEXTURE2D_LOD(_EvoSnowField, sampler_EvoSnowField, uv, 0).rg;
}

// How many motes a shell's cell holds at a place, as a multiple of the shell's own share: the
// column's density over the reference, at least the least ratio (_EvoSnowRef.w), 1 without the
// run's fields.
float EvoSnowDensityRatio(float3 positionWS)
{
    if (!EvoRunSnow()) return 1.0;

    return max(_EvoSnowRef.w, EvoSnowAt(positionWS).x / max(1e-4, _EvoSnowRef.x));
}

// How pale the bed is at a place from its settled snow: the floor stock over the reference times
// the dial, up to twice the dial; 0 without the run's fields.
float EvoFloorDust(float3 positionWS)
{
    if (!EvoRunSnow()) return 0.0;

    float share = EvoSnowAt(positionWS).y / max(1e-4, _EvoSnowRef.y);

    return min(2.0 * _EvoSnowRef.z, _EvoSnowRef.z * share);
}

// One mote's shading, shared by the snow shells (TheatreSnowShells.shader's Shell) and the crumble's flakes
// (TheatreCrumbleFlakes.shader, D277), so the two snows cannot drift apart in look. Only for a shader that has
// included URP's Core.hlsl, TheatreWater.hlsl, TheatreDepth.hlsl and TheatreVolume.hlsl and then defined
// EVO_SNOW_MOTE; the bed shader includes this file without those and never defines it.
//
// The body of the shell's walk, moved here unchanged: q0 the ray's frame (the eye less the snow's fall), t the
// distance along the ray to the mote's nearest approach, m the mote's centre in that frame, c and seed its hash
// key, radius its shell's mote radius, capPixels the most a sharp mote's radius is on screen, cell its cell's
// size, fall the frame's offset from the world, time the shaders' clock, and the water's box and tank for the
// in-water test; sum takes the mote's light, rgb the sun's and a the water's own.
#ifdef EVO_SNOW_MOTE
void EvoShadeMote(float3 q0, float3 dir, float t, float3 m, float3 c, float seed, float radius, float capPixels,
                  float cell, float3 fall, float time, float3 boxMin, float3 boxMax, float4 tank, inout float4 sum)
{
    float3 off = q0 + dir * t - m;
    float dist = length(off);
    float pixel = max(1e-5, EvoPixelMetres(t));

    // The mote's own size (_EvoSnowFlake): the shell's radius spread
    // log-normally about it with the mean area kept, and a share of the
    // motes made large flakes. One size a shell with the dials at zero.
    float3 g = EvoHash3(c * 2.17 + seed + 3.1);
    float own = radius;
    if (_EvoSnowFlake.x > 0.0)
    {
        float normal = (g.x + g.y + g.z - 1.5) * 2.0;
        own *= exp(_EvoSnowFlake.x * normal - _EvoSnowFlake.x * _EvoSnowFlake.x);
    }
    if (frac(g.x * 7.31 + g.z * 3.17) < _EvoSnowFlake.y) own *= max(1.0, _EvoSnowFlake.z);

    // At least most of a pixel, so a far mote is a speck and not a
    // flicker; at most the cap, so a near one is never a blob.
    float r = max(own, 0.75 * pixel);
    r = min(r, capPixels * pixel);

    // A flake's outline, in the plane across the line of sight: a few lobes
    // of the mote's own, turning slowly, so no two specks are the same disc.
    float outline = 1.0;
    if (_EvoSnowFlake.w > 0.0 && dist > 1e-7)
    {
        float3 side = normalize(cross(abs(dir.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0), dir));
        float3 rise = cross(dir, side);
        float a = atan2(dot(off, rise), dot(off, side));
        float3 turn = 6.2831853 * g + time * (0.04 + 0.08 * g.zxy);
        outline = 1.0 + _EvoSnowFlake.w * (0.30 * g.y * cos(2.0 * a + turn.x) +
                                           0.22 * cos(3.0 * a + turn.y) +
                                           0.14 * g.x * cos(5.0 * a + turn.z));
        outline = max(0.35, outline);
    }

    // The lens (_EvoSnowLens): a thin lens's circle of confusion at the mote,
    // in metres at its depth, which is half the aperture times the mote's
    // distance from the focus over the focus. Capped in pixels of a
    // 1080-line frame, whatever the render's size, and kept
    // inside the mote's cell, whose walls the ray's walk does not look past.
    float blur = 0.0;
    if (_EvoSnowLens.y > 0.0)
    {
        float depth = t * dot(dir, -UNITY_MATRIX_V[2].xyz);
        float focus = max(0.05, evoSnowFocus);
        blur = 0.5 * _EvoSnowLens.y * abs(depth - focus) / focus;
        blur = min(blur, _EvoSnowLens.z * pixel * max(1.0, _ScreenParams.y) / 1080.0);
        blur = min(blur, max(0.0, 0.07 * cell - r));
    }

    // The flake's edge: a pixel wide when sharp, as wide as the blur when
    // not, and softened by the fluff. The disc's half-light radius grows by
    // half the blur, so its light spreads over r + blur and no further.
    float soft = pixel + blur + _EvoSnowFluff.x * r;
    float reach = r * outline + 0.5 * blur;
    float cover = saturate((reach - dist) / soft + 0.5);

    if (cover > 0.0)
    {
        float3 world = m - fall;

        if (EvoInWater(world, boxMin, boxMax, tank))
        {
            // A mote widened to a pixel keeps its own light, spread; a
            // blurred one spreads it again, over its larger disc, by the
            // power the lens's dial gives.
            float energy = min(1.0, (own * own) / (r * r));
            if (_EvoSnowFluff.z > 0.5)
            {
                // The flake's light kept over the area it is drawn on: a disc
                // of radius reach with an edge as wide as soft covers about
                // pi (reach^2 + soft^2 / 12), against pi (r^2 + pixel^2 / 12)
                // drawn sharp, so a defocused or fluffy flake spreads the
                // same light thinly (the owner's note of 2026-10-19).
                float sharpArea = r * r + pixel * pixel / 12.0;
                float drawnArea = reach * reach + soft * soft / 12.0;
                energy *= saturate(sharpArea / max(1e-12, drawnArea));
            }
            else if (blur > 0.0)
            {
                float wide = r + 0.5 * blur;
                energy *= pow(saturate((r * r) / (wide * wide)), _EvoSnowLens.w);
            }

            // A large flake is a loose aggregate: its light grows with its
            // size, not its area.
            if (_EvoSnowFluff.y > 0.0 && own > radius)
                energy *= pow(saturate(radius / own), _EvoSnowFluff.y);
            if (dist < r * outline) evoSnowNearT = min(evoSnowNearT, t);

            float gain = min(EvoCausticGain(world, time), 7.0);
            float3 kept = exp(-(_EvoAbsorb.xyz + _EvoAbsorb.w) * EvoWaterPathMetres(world));
            float3 light = EvoSunAt(world) * (0.3 + 0.7 * gain) * kept;

            sum.rgb += light * (cover * energy);
            float3 around = EvoWaterColour(world.y) * kept;
            sum.a += cover * energy * dot(around, float3(0.2126, 0.7152, 0.0722));
        }
    }
}
#endif

#endif
