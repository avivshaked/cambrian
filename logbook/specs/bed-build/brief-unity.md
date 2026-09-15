# Build brief: the bed with shape, the Unity half (round 39; D092; `logbook/specs/bed-spec.md`)

Read `CLAUDE.md` (all of it; the gotchas on the tank, the placer, the diverged guard, the
digest and `simHash` are the ground you build on), then `logbook/specs/bed-spec.md` in full
(this brief builds items 10, 11 and 13, the report's side of item 4, the header's side of
item 12, and the smoke's bed case; the Core half is built and is described in
`logbook/specs/bed-build/brief.md`, which read second), then `logbook/specs/tank-spec.md`
for how the glass, the placer and the radius guard were built.

## Rules

You work in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-bed`
(branch `bed`, at `4ce2ba0` or later). Every edit goes there and nowhere else: never the
main checkout at `D:\Projects\experiments\evolution-simulator\unity` or `src`, never a
worker (`unity-w*`), never the session scratchpad, never TEMP. Temporary files, if any, go
under `D:\Projects\experiments\evolution-simulator\scratch\bed-build\unity\`. No commit
(the caller commits). **No Unity**: do not launch the Editor against any project, in any
mode; the caller compiles and runs the smokes when an Editor slot is free, so write the
code to compile first time and re-read every edit against the surrounding code. Do not
touch `runs/` or any running process. Never sleep, poll or wait. Use absolute paths; the
working directory resets between tool calls. The Core tests run from the worktree with
`pwsh -File D:\Projects\experiments\evolution-simulator\scratch\wt-bed\scripts\core-test.ps1 -Filter <Class>`;
run them only if you touch Core (item 5 below may make you).

House rules that bind the code: everything under `unity/Assets/Evosim` is simulation
source and moves `simHash`, which is expected here and must be said in the report;
`unity/Assets/Theatre` moves no hash and must stay that way (nothing in the theatre may be
referenced from `Assets/Evosim`); **at relief 0 and tilt 0 the world must be bit-identical
to the flat one** (spec item 12: the digest checks the trajectory, so no new RNG draw, no
reordered draw, no changed collider, no changed arithmetic on the flat path; where the
shaped path needs a different order of operations, branch on `World.Bed.HasRelief` and
leave the flat branch's code as it is); a body is never placed inside the rock; XML doc
comments in the existing voice, saying why and citing the spec.

## The Core surface you consume (all in the worktree's `src/Evosim.Core`)

`World.Bed` (`BedShape`): `HasRelief`, `Height(x, z)` (the offset from the flat bed, m,
positive up, defined past the rim as well), `FloorY(x, z)` (`-depth + Height`),
`HeightAndGradient`, `RangeMetres`, `TotalRangeMetres`, `HighestMetres`, `LowestMetres`
(the floor's extreme offsets over the disc), `SteepestSlopeRadians` (the bands),
`SteepestTotalSlopeRadians` (with the tilt), `SlopeBoundBinds`, `Hollows`, `Ridges`,
`HollowRadiusMetres`, `TiltMetres`, `TiltDirectionRadians`, `ScaleMetres`, `ToString()`.
`World.Nutrients` (`GridField`): `LayerCount` (the array's layers, reaching below the mean
depth in a shaped tank; no longer `depth / cell`), `ArrayDepthMetres`, `RefugeLayerCount`,
`IsRefuge(layer)`, `FloorYAtColumn(ix, iz)`, `LowestLiveLayer(ix, iz)`,
`ColumnFloorAndFloorStock()` (over live columns: the floor height and the detritus held in
the lowest live cell). `RunConfig`: `BedReliefMetres`, `BedTiltMetres`, `BedScaleMetres`
(the `bed` group; the doc comments name the environment variables). The world's flat
guard `World.HeightIsInTheWorld(heightY, depth)` is unchanged and stays the outer box.

## What to build

**1. The floor collider (`unity/Assets/Evosim/Sim/SeaFloor.cs`; spec item 10).** When
`World.Bed.HasRelief`, build one static `MeshCollider` from the height map: a lattice at
half a metre over the square that the flat box covers today (the ring plus
`SeamMarginMetres`, so the mesh runs under the glass), vertices at `FloorY(x, z)` (the
map is defined past the rim, so no clamping and no crease), triangles wound so the
normals point up, `convex` false, `cookingOptions` the default, on
`PhenotypeBuilder.CreatureLayer` with the same material and `providesContacts` as the box.
Keep the flat `BoxCollider` as a backstop under it, its top at
`-depth + LowestMetres - 1 m`, so a body that tunnels a thin mesh at speed still meets
rock; it makes no contact in ordinary play. When `HasRelief` is false, build exactly
today's box and nothing else (the flat path unchanged). Replace the scalar `TopY` with
`FloorYAt(x, z)` (the box's top on the flat path, the map's on the shaped one) and
`MinimumPlacementY(x, z, radius)`; keep a `LowestTopY` for the wall. Say in the report how
many triangles the mesh has at 400 m² and whether `MeshCollider` cooking at world start
is a cost worth naming (it is once, not per step).

**2. The glass reaches the rock (`TankWall.cs`).** The slabs' bottom is
`-depth - SeamMarginMetres` today; make it `SeaFloor.LowestTopY - SeamMarginMetres` so a
hollow deeper than 5 m never opens a gap at the rim. Unchanged on the flat path.

**3. The placer (`SharedVolume.cs`; spec item 11).** `TryReserveOffspring` and
`TryReserveFounder` choose the height before the horizontal candidate is drawn and read a
single global `LowestPlacement(radius)`. On the shaped path the height must be read under
the point: after each candidate's `(x, z)` is drawn, `y = max(y, Floor.MinimumPlacementY(x,
z, radius))`. **Do not change the flat path's order of RNG draws or its arithmetic**: on
the flat path `MinimumPlacementY(x, z, r)` is the same number for every `(x, z)`, so the
clamp after the draw gives the same `y` the clamp before it gave, and the candidate loop
must draw exactly what it draws today. Read the two methods whole before editing; the
reservation, the crowding test and the wall test all follow the draw and must see the
clamped `y`. A founder's disc draw is uniform over the disc and stays so.

**4. The floor guard (`Ecosystem.CheckFinite`; spec item 11).** Beside the tank's radius
guard, on the shaped path: a root more than its `Body.Radius` below
`World.Bed.FloorY(root.x, root.z)` dies as a counted `Diverged` death with a dump whose
reason names the bed and the depth below it, the same route the radius guard takes
(same dump format, same counter, `MaxDumps` respected). On the flat path add nothing:
`HeightIsInTheWorld` stays the guard it is.

**5. The report reads the grid, not `depth / cell` (`EvolutionRun.cs`).** `refuge J` reads
`StockInLayer(LayerCount - 1, 0)`, which on a shaped tank is a layer under most of the
floor. Read the refuge stock as the sum over the grid's refuge layers (`IsRefuge`); if
`GridField` has no method that returns it, add one in Core (`RefugeStock(patch)` or the
like, with a test in `BedGridTests` that a flat grid's value equals the old expression and
that a shaped grid's counts every live refuge cell), and say so. Check every other read of
`LayerCount` under `unity/Assets/Evosim` (`EvolutionRun.cs` lines near 2321, 2488, 2735;
`Milestone1Smoke.cs` near 1142) for the same assumption. `det deep` samples the density at
90% of the depth on the flat path; on the shaped path leave it and say in the report that
it reads a fixed height, not the floor.

**6. The environment variables and the header (`EvolutionRun.cs`; spec item 12).** Parse
`EVOSIM_BED_RELIEF`, `EVOSIM_BED_TILT`, `EVOSIM_BED_SCALE` the way `EVOSIM_FLUID_ACCEL` is
parsed (`Env(name, new RunConfig().Bed…)`, assigned onto `config` beside `WorldShape`).
The header's `SpaceToken` prints `bed` when a floor exists; on the flat path that word
stays exactly as it is (round 38's headers are compared token by token). On the shaped
path it becomes `bed relief <r> m tilt <t> m scale <s> m` followed in the same token by
` (hollows <n>, ridges <n>, range <x.xx> m, steepest <nn>° bands <nn>°, bound binds|clear)`
from `World.Bed`, so a run's header says what floor it ran on and the smoke can print the
hollow count (spec item 4). Add `bedRelief`, `bedTilt`, `bedScale`, `bedHollows`,
`bedRidges`, `bedRangeMetres`, `bedSteepestDegrees` to the manifest (`run.json`) beside the
config's facts, and to the run's `stats.jsonl` header row if there is one (read how the
manifest is written before adding).

**7. Two table columns and their stats fields.** From `ColumnFloorAndFloorStock()`:
`floor low %`, the share of the detritus held in the lowest live cells that lies in
columns whose floor is in the lowest quarter of the floor's range (the hollows), and
`floor J`, the total those cells hold. Both print a dash on the flat path (as `vtx` does on
a grid), and `stats.jsonl` carries `floorStockJoules` and `floorStockLowQuarterShare`.
Read `BaseColumns` and the per-sample block whole before adding, and keep the analysis
script's named-column contract (`scripts/analyse-arm.ps1 -ListColumns` reads the header
row; a new column at the end is safe).

**8. The smoke's bed case (`SharedSpaceSmoke.cs`).** Part 4 walks bodies into the glass
and into the flat bed. Add a bed case that builds a tank at relief 4 m, tilt 6 m (the
spec's numbers for a visible floor) and: places a founder at the floor's highest and
lowest columns and asserts each sits above `FloorY` by at least its radius; pushes a body
below the floor at a hollow and asserts it comes back out or dies as the guard's
`Diverged` (whichever the harness does for the glass, do the same); asserts the header
token carries `hollows` and the manifest the bed fields; asserts a flat tank's header
still reads exactly `…, wall, bed`. Keep it under the time the other parts take.

**9. The theatre (`unity/Assets/Theatre`; spec item 13; no hash moves).**
`TheatreSkin.BuildBed`: when the replayed world's `Bed.HasRelief`, build the sand mesh on
its 0.2 m grid with vertices at `FloorY(x, z)` from the world the replay constructed
(`replay.Eco.World.Bed`, the same map the collider has), normals from
`HeightAndGradient`, the material and the ripple shader unchanged (the shader displaces
downward only, so the visual stays inside the collider). `WaterBounds`: the floor's circle
and grid lines at the floor's height along the rim and the grid (sample `FloorY`), not at
`-depth`. `SnapshotCamera.BoxOf`: `min.y = -depth + LowestMetres`. The marine snow's
settling reads the bed height if it reads a floor at all. Nothing in the theatre may
change on a flat world: a picture of round 38 must be the picture it is today.

**10. What you do not build:** the round launcher (`rounds/`), the pace measurement, the
digest run, the smoke run, any change to the box's fields, the rolls, the flat collider's
shape or the wall's count.

## Report (your final message; no `.md` files anywhere)

What you built, file by file, with the design choices where the brief left them to you
and why; every place the flat path's code changed at all (there should be none beyond a
branch; list each branch point); the triangle count; every place the brief's plan did not
survive contact with the code and what you did instead; what the caller must run (the
smoke's entry name, the digest pair, a 600 s tank smoke at relief 1 m tilt 6 m at 400 m²
with dt 0.02 and seed 3, and the pictures to take); the Core test counts if you touched
Core; and any compile risk you could not resolve by reading. Under 1,500 words.
