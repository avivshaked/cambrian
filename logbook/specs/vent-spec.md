# D067 — the vent (upwelling plume): implementation spec

Read `CLAUDE.md` first (no `UnityEngine` in Core; the reflection tests guard every tunable;
loading refuses rather than defaults; PowerShell scripts need a BOM). Then read
`src/Evosim.Core/Environment/CurrentField.cs` (the D066 roll: `VelocityAt` patch overload,
`HorizontalCrossingFraction`, `CrossingDirection`), `NutrientField.Advect`, and
`World.AdvectBodies`. The vent is an *addition* to that machinery: another prescribed,
divergence-free flow superposed on the roll, using the same staggered grid (w at layer
interfaces per patch, u at patch faces per layer) and the same upwind transfer.

## What it is

A plume of water rising from the floor to the surface in one patch (the vent patch), with the
return flow sinking uniformly through every other patch, joined by horizontal legs along the
surface and along the floor. It is upwelling: it lifts the deep larder into the light, and it
closes the loop the 30 m rolls leave open (a roll that stops above the floor is a trapdoor —
logbook/0048).

Geometry. `K` patches in a ring (D061), `V` = vent patch, `D` = the vent's depth (the floor),
`L` = leg thickness. Let `s` = `VentSpeed`.

- **Plume** (patch `V`): vertical velocity `w = +s` for `0 < d < D`, exactly 0 at `d ≤ 0` and
  `d ≥ D` (special-cased, like the roll's endpoints).
- **Return** (every patch `≠ V`): `w = −s / (K − 1)` for `0 < d < D`, 0 at the ends.
- **Surface leg** (`0 ≤ d < L`): horizontal flux across the face between patch `V+j` and `V+j+1`
  (indices mod `K`, `j = 0..K−1`), positive toward `V+j+1`, is
  `F_j = c_j · Q` with `Q = s · A_patch` and `c_j = 1/2 − j/(K−1)`.
  So the plume's water leaves it half to each side (`c_0 = +1/2`, `c_{K−1} = −1/2`) and each
  return patch keeps `Q/(K−1)` of what passes, which is exactly what it sinks. Check for K=4,
  V=0: faces 0→1: +Q/2, 1→2: +Q/6, 2→3: −Q/6, 3→0: −Q/2.
- **Floor leg** (`D − L < d ≤ D`... use `D − L ≤ d < D` so the last layer of a world whose depth
  is a whole number of layers is the leg): the same fluxes negated (water returns to the plume
  along the floor).
- Between the legs (`L ≤ d < D − L`) the horizontal vent flow is 0.

Discrete continuity — the property the tests must pin. On the staggered grid with layer
thickness equal to `L`, for every cell: inflow = outflow. Plume top layer: `Q` in from below,
`Q/2 + Q/2` out sideways. Return patch top layer: `F_{j−1} − F_j = Q/(K−1)` in sideways,
`Q/(K−1)` out below. Interior layers: `w` in = `w` out. Bottom layers mirror the top. Under the
existing upwind transfer (a cell loses `fraction × its own stock` per face it exports through,
gains `fraction × the neighbour's stock` per face it imports through), a **uniform field stays
exactly uniform** when the fluxes balance — that is the test (see below).

Crossing fractions, width-free. The fraction of a cell's *volume* that crosses a face in `dt`:
- vertical, plume: `s · dt / LayerMetres` (already what `Advect` computes from `w`);
- horizontal, in a leg, face `j`: `|c_j| · s · dt / L` — because `F_j · dt / (A_patch · L)` =
  `|c_j| · s · A_patch · dt / (A_patch · L)`. **Compute it this way**; do not derive it from a
  horizontal velocity and a width, because `CurrentField` has no width. It is clamped at ½ like
  the roll's.

The horizontal *velocity* a creature feels (`VelocityAt(...).X`, drag only, ecologically inert
per §6.3) is `F_j / (L · A_patch / W) = c_j · s · W / L` where `W` is the patch width. Give
`CurrentField` a **non-tunable** `public float PatchWidthMetres { get; set; }` (default 0),
set by `World`'s constructor from `Nutrients.PatchWidthMetres`; when it is 0 the vent's
horizontal drag term is 0 and nothing else changes (say so in the doc comment). It must not be
`[Tunable]` — it is derived from area and patch count, which are already hashed.

## Tunables on `CurrentField` (all `[Tunable("current", ...)]`, all in the hash and the JSON —
the two reflection tests will fail otherwise, and that is the check)

| name | unit | default | meaning |
|---|---|---|---|
| `VentSpeed` | m/s | 0 | plume speed; 0 is off, which is every run before D067 bit for bit |
| `VentPatch` | — | 0 | index of the plume patch; validated against `patchCount` at use (`patch % patchCount` is NOT acceptable — throw `ArgumentOutOfRangeException` from the setter for negative values, and from `World`'s constructor if `VentPatch ≥ HorizontalPatches` while `VentSpeed > 0`) |
| `VentDepthMetres` | m | 60 | the floor the plume draws from; `World` must throw at construction if `VentSpeed > 0` and this ≠ `WorldDepthMetres` (the vent draws from the floor, and the field does not know the world's depth — so the config states it and the world checks it) |
| `VentLegMetres` | m | 1 | thickness of the surface and floor legs; `World` must throw at construction if `VentSpeed > 0` and this is not (within 1e-6) a whole multiple of `LightLayerMetres`, because discrete continuity holds only then |

Setters reject negative / non-finite values the way `Speed` does. `ToString()` gains
`", vent {s} m/s in patch {V}"` when on.

**The vent needs `patchCount ≥ 2`** (no return patch otherwise): with one patch it is off,
like the roll — `VelocityAt`, the crossing methods and `Advect`/`AdvectBodies` all take the
old path. Add a `public bool VentActive(int patchCount) => VentSpeed > 0f && patchCount >= 2`.

## Where it plugs in

1. `CurrentField.VelocityAt(heightY, seconds, patch, patchCount)`:
   - if neither `Rolls` nor the vent is active (or `patchCount < 2`): unchanged (old field).
   - otherwise: `base` = the roll term as today when `Rolls` is on, else the old two-argument
     field (which is what a creature felt before and still feels; it is zero when `Speed` = 0);
     result = `base + vent(heightY, patch, patchCount)` where the vent's `Y` is the plume/return
     `w` above and its `X` is the drag term (`c_j · s · W / L` in a leg at this patch's
     right-hand face, 0 otherwise, 0 if `PatchWidthMetres` = 0). `Z = −X` as today.
   - **Bit-identical when the vent is off**: the code path for `Rolls` on / vent off must be
     the existing one (add zero to nothing — do not restructure the arithmetic; the existing
     `RollCellTests` must pass untouched).
2. Split the transport from the drag. Add a private `HorizontalTransportFraction(heightY,
   seconds, patch, patchCount, dt, patchWidthMetres)` = roll fraction (as today, only when
   `Rolls`) **+** vent fraction (`|c_j| s dt / L` in a leg), clamped at ½ after summing; and a
   private signed `HorizontalTransportSign(...)` = sign of (roll `u` when `Rolls`, else 0) +
   (vent `F_j`'s sign in a leg, weighted so the fraction and the sign agree: compute the signed
   *fraction* for each and sum, then take the sign and the magnitude of the sum). Make
   `HorizontalCrossingFraction` and `CrossingDirection` return those. The old two-argument field's
   `X` must **not** feed the transport when `Rolls` is off — it never did, and turning the vent on
   must not start moving stock along the ring by the old inert term. With the vent off the two
   methods must return exactly what they return today (the roll-only path, same arithmetic).
   Their `Rolls`-off early return becomes "neither Rolls nor vent active".
3. `NutrientField.Advect`: the vertical pass reads `VelocityAt(...).Y` — with the vent's `Y`
   included it needs no change *except* that its guard is `!current.AdvectFields` only; keep
   that. The horizontal pass already calls `CrossingDirection`/`HorizontalCrossingFraction` —
   no change. Confirm the sampling heights: vertical at interfaces `−(layer+1)·LayerMetres`,
   horizontal at layer mid-heights `−(layer+½)·LayerMetres`; the leg membership test uses the
   mid-height, so a leg of `L` = `LayerMetres` is exactly the top (and bottom) layer.
4. `World.AdvectBodies`: the guard `!current.Rolls` becomes `!(current.Rolls ||
   current.VentActive(PatchCount))`; the body of the method is unchanged (it already uses the
   two crossing methods). `World`'s constructor sets `Config.Current.PatchWidthMetres` and
   performs the three validations above (throw `ArgumentException` with a message naming the
   tunable).
5. Nothing in `unity/` changes for the physics: `FluidEnvironment` already samples the patch
   overload of `VelocityAt`. `EvolutionRun.cs` (Unity, editor assembly — you may edit it but
   cannot compile it here; keep the edit mechanical and mirror the existing `EVOSIM_CURRENT_*`
   lines exactly): env `EVOSIM_VENT` (speed, default 0), `EVOSIM_VENT_PATCH` (default 0),
   `EVOSIM_VENT_DEPTH` (default `new RunConfig().WorldDepthMetres`), `EVOSIM_VENT_LEG` (default
   `new RunConfig().LightLayerMetres`); assign them to `config.Current.Vent*` next to the
   `config.Current.Rolls = ...` lines; header token right after the `" · advect ..."` token:
   `" · vent " + (vent > 0f ? vent + " m/s in patch " + (int)ventPatch + " from " + ventDepth + " m, legs " + ventLeg + " m" : "off")`.

## Tests — new file `src/Evosim.Core.Tests/VentTests.cs`, in the style of `RollCellTests.cs`

1. **Off is bit-identical.** With `VentSpeed` = 0, `VelocityAt`, `HorizontalCrossingFraction`,
   `CrossingDirection` return exactly (`==`, not approximately) what a `CurrentField` built the
   same way returns, for rolls on and rolls off, over a grid of depths/times/patches; and a
   `World` stepped 200 steps with rolls+advection on gives the same `Audit`, populations and
   field totals as before (copy the pattern of `AWorldWithRollsOffIsUntouchedByAMovingCurrent`).
2. **The plume and the return.** Rolls off, `Speed` 0, `VentSpeed` 0.1, K=4, V=2: `Y` is +0.1 in
   patch 2 at d=0.5, 10, 30, 59.5; exactly 0 at d=0 and d=60 and below; −0.1/3 in patches 0, 1,
   3 at the same depths.
3. **The legs run the right way.** In the surface leg, `CrossingDirection` at faces V→V+1 is +1
   and at V−1→V is −1 (water leaves the plume both ways); in the floor leg the signs flip;
   between the legs it is 0. Fractions: face V→V+1 in the surface leg = `0.5 · s · dt / L`;
   face V+1→V+2 = `(1/2 − 1/3) · s · dt / L` for K=4.
4. **A uniform field stays uniform** (discrete continuity). `NutrientField` 60 m deep, 1 m
   layers, K=4, filled uniformly (`Deposit` the same joules in every cell), `AdvectFields` on,
   rolls off, vent 0.1 m/s, dt 0.5: after 2,000 `Advect` calls every cell's stock equals the
   initial value within 1e-9 relative, and the total is conserved to 1e-9. Repeat for V=0 and
   V=3 and for K=2 and K=5 (odd K is fine for the vent — no seam — say so in the test name).
5. **The vent lifts the larder.** Same field, empty, deposit 1,000 J in the bottom layer of a
   return patch; run `Advect` with dt 0.5; assert that by t = (D/ (s/(K−1)) ... no — the
   parcel is already at the floor; it travels along the floor leg to the plume (one or two
   patches at the leg's speed) and up the plume at `s`: assert that the surface layer of the
   plume patch holds ≥ 5% of the 1,000 J by t = 2 · (D/s + K · L / (0.5 s)) and that it was
   ≤ 0.01% at t = D/(4s). Also assert no cell ever goes negative.
6. **Conservation with the roll and the vent together.** Rolls on (blink 300 s), advection on,
   speed 0.3, cell 30, vent 0.1: 4,000 steps, total conserved to 1e-9, no negatives.
7. **Bodies ride the legs.** A `World` with K=4, vent on, rolls off, `Speed` 0, and a
   population placed by the test in the floor leg of a return patch (use the existing pattern in
   `RollsCarryCreaturesBetweenPatches`): after enough steps some creatures have changed patch,
   and every change is toward the plume patch (patch index moves the way the floor leg's
   `CrossingDirection` says). Bodies in mid-depth never change patch.
8. **Validation.** `World` construction throws when `VentSpeed > 0` and `VentPatch ≥ K`, when
   `VentDepthMetres ≠ WorldDepthMetres`, and when `VentLegMetres` is not a multiple of
   `LightLayerMetres`; constructs fine when `VentSpeed` = 0 whatever the other three say.
9. The two reflection tests (`RunConfigTests`, `RunConfigJsonTests`) pass unmodified — they
   are the proof the four tunables reach the hash and survive a save/load.

Run `./scripts/core-test.ps1` (PowerShell) until the whole suite is green (420 today + yours).
Do not touch DESIGN.md / DECISIONS.md / logbook (the reviewer writes those). Do not run Unity.
Do not write outside the project directory. Report: files changed, test count, and anything in
the spec that turned out to be wrong or ambiguous — say so plainly rather than guessing.
