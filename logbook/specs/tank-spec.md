# Build spec: the tank (fable-propose-aquarium.md, ruling 1; owner "yes proceed", 2026-09-11)

Read `fable-propose-aquarium.md` first, then CLAUDE.md in full (the gotchas on the grid, the
current, the placer, the config's refuse-rather-than-default rule and the reflection tests
are all load-bearing here), then the files this spec names. The branch is `tank`, in the
worktree `scratch/wt-tank`, which already carries the box branch's two-axis layout
(`RunConfig.PatchesAcross`, `PatchOf(x, z)` everywhere, `SetBox(..., patchesAcross)`); keep
it, since a Box world with `PatchesAcross` 1 must stay bit-identical to every recording,
and the tank is a third shape beside it rather than a rewrite of it.

Rules: edit only in the worktree; nothing written outside the repository; no commit; no
Unity (the caller compiles on a worker afterwards); run the Core suite with the fast filter
as you go (`./scripts/core-test.ps1 -Filter <TestClass>`) and the default suite once at the
end (about a minute); never wait on anything longer; do not write any `.md` file; doc
comments say why and cite this spec by path.

## The one invariant

`WorldShape` defaults to `Box`, and at that default every recorded `config.json` reads, every
hash-bearing number is unchanged, and the digest of a Box world is identical to today's. The
tank is reached only by setting the shape. The reflection tests (`RunConfigTests`,
`RunConfigJsonTests`) must pass with every new tunable wired.

## The tunables (all `[Tunable]`, all in the header unconditionally)

| property | env var | default | meaning |
|---|---|---|---|
| `RunConfig.WorldShape` (enum `Box`, `Tank`) | `EVOSIM_SHAPE` | `Box` | the container |
| `RunConfig.MatterBudgetUnits` | `EVOSIM_MATTER_BUDGET` | 0 | 0 = today's rule (density × live volume); above 0, the seeded matter total, and the density is derived from it over the live volume |
| `RunConfig.DriveLimitAtEveryStep` | `EVOSIM_DRIVE_LIMIT_ALWAYS` | false | `EffectorDriver`'s 30 rad/s cap applies at every step rather than only above dt 0.01 (`fable-propose-limiter.md`) |

The tank's radius is not a tunable: `R = sqrt(WorldAreaSquareMetres / π)`, so the area keeps
its meaning across shapes (100 m² is R 5.64 m; 400 m² is R 11.28 m). The header's space
token reads `space tank r=5.64 m (100 m2), depth 60, wall, bed` for the tank and stays
exactly as it is for the box. Refuse at construction: `Tank` with `FieldModel.Cells`, `Tank`
with `PatchesAcross` above 1, `Tank` with D061's dispersal lottery above 0, `Tank` with
`CurrentMode.Rolls` at a nonzero speed (the rolls are the box's field).

## Geometry

The tank's bounding square is `[0, 2R) × [0, 2R)` with the axis at `(R, R)`, so every
existing consumer of `LengthMetres` and `WidthMetres` sees `2R` and the storage stays a
rectangle. A point is inside when `(x − R)² + (z − R)² ≤ R²`. `SharedVolume`, `World`,
`CurrentField` and `GridField` each gain the shape and the test; `ShortestDistance` in the
tank is the plain Euclidean distance (no minimum image); `TryWrap` returns false; `WrapAxis`
is the identity.

**Patches are rings of equal area.** `HorizontalPatches` K rings: ring i holds
`R·sqrt(i/K) ≤ r < R·sqrt((i+1)/K)`, so the per-patch bins stay comparable in area and the
report's `alivePerPatch` columns read centre to rim. `PatchOfXZ` is the ring index. A
patch's "centre cell" for the per-patch density reads is the cell at the ring's mid-radius
on the θ = 0 ray from the axis; say so in the doc comment.

## The wall (`unity/Assets/Evosim/Sim/TankWall.cs`, beside `SeaFloor`)

A static collider ring: 48 thin `BoxCollider` slabs, each tangent to the circle at its
segment's midpoint, 0.5 m thick, extending from the floor's top face minus the floor's seam
margin to 5 m above the surface, on the creature layer, `providesContacts` true, the floor's
physics material. Built by `Ecosystem` right after `SeaFloor` when the shape is `Tank`.
`WrapAtTheSeams` does nothing in the tank. Add a guard beside `HeightIsInTheWorld`: a root
farther than `R + 1 m` from the axis is a `Diverged` death with a dump, as a height outside
the box is; it should never fire and the count says whether it did.

## The current: a gyre (`CurrentField`, a third construction beside Rolls and Transport)

Prescribed, divergence-free and tangential at the wall by construction, closed at the
surface and the floor, in cylindrical coordinates about the axis with `r`, `θ`, and `y`
(down is negative as everywhere). Three parts summed, each a curl or a pure swirl:

1. **The swirl**, azimuthal only: `v_θ = S · (r/R)(1 − (r/R)²) · (1 + 0.5·cos(π y/D))`,
   zero on the axis and at the wall, divergence-free because it depends on r and y only.
2. **The overturning**, axisymmetric, from a Stokes stream function
   `Ψ(r, y) = Ψ₀ · r²(1 − r/R) · sin(qπ y/D)` with `v_r = −(1/r)∂Ψ/∂y`, `v_y = (1/r)∂Ψ/∂r`;
   `v_r` vanishes at the wall and the axis, `v_y` at the surface and the floor; two cells,
   q = 1 and q = 2, with independent slow phases in time.
3. **The eddies**, horizontal, from a vertical vector potential `A_y = Σ_m a_m f_m(r) cos(mθ + φ_m + ω_m t) · sin(q_m π y/D)`
   with `f_m(r) = (r/R)^m (1 − (r/R)²)` for m = 1, 2, so `v_r = (1/r)∂A_y/∂θ` vanishes at
   the wall and `v_θ = −∂A_y/∂r`; divergence-free as any curl is.

Normalise numerically: measure the RMS of each component over the live volume and a few
phases on a fine grid at construction, scale the overturning so the vertical RMS equals the
horizontal, then scale the whole field so the total RMS is the `Speed` knob; measure the
maximum speed on the same grid times 1.1 as `MaximumTransportSpeed` so `GridField.Advect`'s
Courant logic keeps working unchanged. Keep the existing periodic Transport field untouched
for the box; the gyre is selected by shape, not by mode.

Tests (`CurrentTransportTests`, new class `GyreTests`): numerical divergence at 1,000 random
interior points below 1e-3 of the RMS per metre; radial velocity at 200 points on the wall
below 1e-6; vertical velocity at the surface and the floor below 1e-6; RMS at the knob to
1%; every component's RMS within 20% of the others; seed reproducibility; and the
dead-pocket fraction, the share of live cells whose speed is under a tenth of the RMS,
printed by a test so the caller can read it (the proposal's check 2).

## The grid mask (`GridField`)

A per-cell `bool` mask, live when the cell's centre is inside the circle, built once. Every
face between a live and a dead cell is a wall: `Advect` and `Mix` move nothing across it
and never wrap x or z in the tank. `CellAt` for a point outside the circle moves it toward
the axis until inside, then indexes (say why: a corpse or a deposit cannot be outside, but
float error at the wall can be). `SeedUniform`, totals, refuge and per-depth sums run over
live cells only; `MatterInitialTotal` records the actual. `MatterBudgetUnits` above 0 sets
the seeded density to `budget / (live cells × cell volume)` for the matter field; the
detritus seeding is unchanged. Tests: live-cell count within one ring of cells of
`πR²/cell²`; advection and mixing conserve the total on the mask over 1,000 steps at the
campaign's speed; a Box world's cell count and digest unchanged.

## The placer (`SharedVolume`)

Founders: uniform in area over the disc (`R·sqrt(u)`, θ uniform), same depth rule. Offspring:
today's disc about the parent, then the candidate is refused if outside the circle (the
rejection loop already exists; add the test to `Free`). `TryReserve*` never wraps in the tank.
Test: 10,000 founders all inside; ring counts of uniform points equal to within 5%.

## The report (`EvolutionRun.cs`, `Ecosystem.MeasureHorizontalSpread`)

The space token as above; `wraps` stays a column and reads 0 in the tank; `cols` counts the
occupied 1 m columns whose centre is inside the circle against the live column count
(printed `n/N` as now); `x sd` and `z sd` are plain standard deviations in the tank (no
circular statistics; there is no seam). Add `driveLimit always` or `driveLimit >0.01` to the
header beside `physics jobs`. The manifest's `driveImpulsesLimited` already exists.

## The limiter (`EffectorDriver`)

`_limitDrive` becomes `dt > 0.01 || config.DriveLimitAtEveryStep`, read once as now. The
un-limited branch stays character for character (its comment says why).

## Launchers (`rounds/`)

`launch-r37.ps1`: round 36's launcher with `EVOSIM_SHAPE Tank`, area 100, `PatchesAcross`
removed, `EVOSIM_DRIVE_LIMIT_ALWAYS` as a parameter defaulting to false (the caller flips
it after the limiter's check). `launch-r38.ps1`: the same with area 400,
`EVOSIM_MATTER_BUDGET 6000`, `EVOSIM_CORPSE_DECAY 0.005`. Header comments say what each
round asks.

## Not in this build

The theatre (`WaterBounds`, `SnapshotCamera`, `TheatreRunner`) and `scripts/positions-read.py`
learn the shape in a second pass by another agent; `SharedSpaceSmoke` gains a tank case
then too. Do not touch them beyond what compiles.

## Final message

Tables: files changed; the tunables with their header tokens; the gyre's measured RMS per
component, its maximum-to-RMS ratio and its dead-pocket fraction from the test output; the
Core suite's count and time; what is unverified (everything under `unity/` is uncompiled
until the caller compiles it) and any place the spec was ambiguous and what you chose.
