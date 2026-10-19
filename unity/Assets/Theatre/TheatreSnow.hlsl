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

#endif
