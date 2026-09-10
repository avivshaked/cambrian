# Build spec: `GridField`, the water as a 3D grid of cells (fable-propose-grid.md rules 1–5, 8–10)

Repo: d:\Projects\experiments\evolution-simulator. Core is `src/Evosim.Core` (netstandard2.1, C# 9,
no UnityEngine, no dependencies); tests `src/Evosim.Core.Tests` (xunit), run with
`./scripts/core-test.ps1 -Filter <name>` (seconds) and `./scripts/core-test.ps1` (the whole
suite, ~15 min under machine load — run it in the background and read the log).
Read `CLAUDE.md` first. Rules that bind you: write nothing outside the repository (transient
files go in `scratch/`); do not launch Unity; do not commit; do not touch `unity-w*`.
Doc comments in this codebase are prose that explains *why*, with the incident that taught it
— read `VertexField.cs`'s remarks and match that register. No em-dashes in new prose, short
sentences.

## The seam (mapped 2026-09-08)

- `src/Evosim.Core/Environment/IMatterField.cs` — the interface; implementers are
  `NutrientField` (cells: layers × patches) and `VertexField`. Read all three files fully
  before writing a line. The grid must honour every member's documented contract; where a
  member is undocumented, `NutrientField`'s behaviour is the contract (it is the original).
- `World.cs` field construction at ~544–585 (`config.FieldModel` switch), seeding 593–611,
  influx `DepositMatterInflux` 973–1035 (branches on `Matter is VertexField` → `Emit`, else
  casts `(NutrientField)Matter`), `BuryMatter` 1062–1093 (casts `(NutrientField)Matter` and
  calls its `Take(floorY, wanted, patch)`), per-step passes 906–941 (Settle, Remineralise,
  Mix, Advect, Cull), feeding 1136–1220 (ClearDemand / EdibleDensityAt / Demand /
  FreezeAvailability / ShareAt / FrozenEdibleDensityAt / Take at `creature.Point`),
  exudation 1263, excretion 1288, death deposits 1342/1352, conception's matter gate
  1762/1806 (`ReachableStock(parent.Point)`) and draw 1847/1851.
- `RunConfig.cs` field group at ~600–660: `FieldModel` (enum `MatterField { Cells, Vertices }`),
  `FieldKernelMetres`, `FieldMatterKernelMetres`, `FieldMergeMetres`, `FieldVertexCap`,
  `FieldVertexJoules`. Reflection guards: `RunConfigTests` and `RunConfigJsonTests` (every
  settable property must be `[Tunable]`, reach `Hash()`, and survive JSON).
- `CurrentField.cs`: `VelocityAt(heightY, seconds, patch, patchCount)` returns a `Float3`
  velocity; `HorizontalCrossingFraction` / `CrossingDirection` are the patch-level helpers
  `NutrientField.Advect` (761–850) uses. Courant is clamped at ½ there.
- `VertexFieldTests.cs` is the example of the contract tests the grid needs its own version
  of: conservation under every operator, exact sharing between mouths, take delivers what
  the gate promised, refuge semantics, determinism, refusal of a point without a position,
  a full-world audit-and-matter-identity closure test (`AVertexWorldClosesItsAuditAndItsMatterIdentity`).
- Geometry: `NutrientField` / `VertexField` constructors carry `worldArea`, `depthMetres`,
  `layerMetres`, `patchCount`, `patchWidthMetres`, sink speed. The box is `PatchCount ×
  PatchWidthMetres` long in x, `PatchWidthMetres` wide in z (a ring), `depthMetres` deep in
  −y; x and z wrap (D077). Check `VertexField.PatchOf` and `SharedVolume` for the exact
  convention and keep it.

## What to build

### 1. `src/Evosim.Core/Environment/GridField.cs` — `public sealed class GridField : IMatterField`

Constructor mirrors the others plus `cellMetres`. Cells: `nx = round(length/cell)`,
`ny = round(depth/cell)`, `nz = round(width/cell)`; refuse (ArgumentException) a cell size that
does not divide the box's length, depth and width to within 1e-4, and print the three sizes
in the message. `LayerMetres = cellMetres`, `LayerCount = ny`, `LayerVolume = worldArea × cell`,
`PatchCount`/`PatchWidthMetres` as given (a patch is a range of x-columns).

- **Cell of a point**: `ix = floor(x/cell)` wrapped into `[0,nx)`, `iy = heightY >= 0 ? 0 :
  min(ny−1, floor(−heightY/cell))`, `iz = floor(z/cell)` wrapped. A `FieldPoint` without a
  horizontal position (`!HasHorizontal`) is refused with `InvalidOperationException`, as the
  vertex field refuses it. The depth-and-patch overloads (`Deposit(heightY, joules, patch)`,
  `DensityAt(heightY, patch)`, `EdibleDensityAt(heightY, patch)`, `StockInLayer(layer, patch)`)
  address the patch's centre column at that height for deposits and reads, and the sum over
  the patch's columns for `StockInLayer`.
- **Amounts, not densities**: `double[] _stock` per cell, `TotalJoules` a running double
  reconciled by a `Recount()` used in tests. `DensityAt = stock / cellVolume`.
- **Refuge**: match `NutrientField` exactly — read how it treats the floor layer for
  `EdibleDensityAt`, `ReachableStock`, `Take`, `FrozenEdibleDensityAt` (the floor layer is
  a refuge invisible to edible reads; `DensityAt`/`TotalJoules` still count it).
- **Feeding** (`ClearDemand`, `Demand`, `FreezeAvailability`, `ShareAt`,
  `FrozenEdibleDensityAt`, `Take`, `ReachableStock`): per cell, the frozen-availability rule
  `NutrientField` implements per cell. `ReachableStock` is the cell's edible stock. `Take`
  returns what it removed, never more than the cell holds, never negative stock.
- **`Settle(seconds)`**: downward transfer of fraction `min(0.5, sink·dt/cell)` from each
  layer to the layer below, buffered then applied; the floor layer keeps what reaches it.
- **`Remineralise(seconds, rate)`**: as `NutrientField` does it (floor layer back up one).
- **`Mix(seconds, D, hD)`**: explicit Fick between face neighbours, all six faces, with `D`
  on every axis. Refuse `hD` that is nonzero and differs from `D` (ArgumentException citing
  D084 and the grid's isotropy); refuse `D·dt/cell² > 1/6` (ArgumentException naming the
  three numbers and the limit). Zero flux at the surface and the floor; wrap in x and z.
  Buffered then applied, so the pass is order-independent.
- **`Advect(current, seconds, dt, patchWidth)`**: upwind per axis. For each face between two
  cells, velocity at the face's centre from `current.VelocityAt(faceY, seconds, patch,
  PatchCount)`; the fraction `min(0.5, |v|·dt/cell)` of the upstream cell's stock crosses to
  the downstream cell. Vertical faces at the surface and floor carry nothing. x and z wrap.
  Buffered then applied. Conservative by construction; assert it in a test.
- **`Cull()`**: no-op.
- **`SeedUniform(joules)`** (grid-only, like `VertexField.SeedUniform`) and
  **`DepositBox(joules, centre, halfExtent)`** (grid-only, the influx's plume box: spread the
  amount over the cells whose centres fall inside the box, equally; if none, the cell at the
  centre). Name them to match the vertex field's `SeedUniform`/`Emit` where the semantics
  match, and read how `DepositMatterInflux` uses `Emit` so the grid branch is the same rule.

### 2. `RunConfig.cs`

- `MatterField.Grid` enum member.
- `FieldCellMetres` (`[Tunable("field", Unit = "m")]`, default 1f) and `FieldMatterCellMetres`
  (default 3f), with remarks in the house register: why two sizes (a child costs 8 to 16
  units of matter and 1 m³ holds about one at the seeded density; the matter grid's cell is
  the old 24 m³ cell's volume), the stability limit, and the one-cell rule.
- Reflection tests must pass unchanged.

### 3. `World.cs`

- Construction: a `Grid` branch building `Nutrients = new GridField(... FieldCellMetres ...)`
  and `Matter = new GridField(... FieldMatterCellMetres ...)`; refuse without `SharedSpace`
  (same message shape as the vertex refusal, citing the proposal: a cell is a position).
- Seeding: grid → `SeedUniform`.
- `DepositMatterInflux`: a grid branch with the plume box → `DepositBox`, same box as the
  vertex branch; the surface influx (all patches at 0) → deposit per patch column at the
  surface, as the cell branch does.
- `BuryMatter`: stop casting to `NutrientField`. Add to `IMatterField` one member,
  `double TakeFromLayer(int layer, int patch, double wanted)` (returns what was taken), and
  implement it on all three fields (`VertexField` already has `BuryFloor`; wrap or implement
  the equivalent for its floor vertices — read `BuryMatter`'s vertex path first, it may
  already special-case; keep the vertex world's behaviour byte-identical, since rounds 30
  and 31 must remain replayable).
- Every other call site already goes through `creature.Point` and needs no change. Grep for
  `is VertexField` and `(NutrientField)` casts and make each one field-agnostic or give it
  a grid branch.

### 4. Tests — `src/Evosim.Core.Tests/GridFieldTests.cs`

Hold the grid to the vertex field's contracts, one test per line:

- a uniform seed reads back its density at any interior point and `TotalJoules` equals the seed;
- two mouths in one cell share it exactly and the cell empties exactly, nothing negative;
- thirty mouths spread over cells never overdraw, takes sum to what shares promised;
- a take delivers `min(asked, reachable)`; `ReachableStock` is the cell's edible stock;
- every operator conserves the total over 20 steps (Settle, Remineralise, Mix, Advect with
  a rolling `CurrentField` as `VertexFieldTests` configures one, Cull), to 1e-9 relative;
- Fick: a cell twice as far above its neighbours loses twice as fast (linear in the
  difference), and a uniform field stays uniform under mixing;
- mixing is refused above the stability limit and with an unequal horizontal diffusivity;
- advection moves a patch downstream one cell per `cell/speed` seconds and nothing upstream;
  wraps at the seam; and **prints the along-flow spread** (variance growth per second of a
  point deposit under a steady current, converted to an effective diffusivity) so the
  number can be stated in DESIGN — assert only that it is finite and positive;
- the refuge hides what sinks into the floor from edible reads and not from `TotalJoules`;
- a point without a position is refused; a grid world needs shared space;
- **a still mouth eats a hole and a moving one does not** (port `AStillMouthEatsAHoleAndAMovingOneDoesNot`);
- **the owner's geometry**: five mouths clustered in one cell and five mouths in the
  neighbouring cells; assert that the neighbours' cells lose nothing to the cluster's
  feeding in a step with mixing off, and print both groups' intake;
- a full-world closure test on the grid (port `AVertexWorldClosesItsAuditAndItsMatterIdentity`
  with `FieldModel = Grid`): audit residual and matter identity within 1e-6 relative over
  3,000 steps with founders, feeding, exudation, death, conception, influx, burial, sinking,
  mixing and a rolling current;
- determinism: two grids given the same sequence are identical (trivial, but assert it).

Port the three experiments in `VertexFieldExperiments.cs` to a `GridFieldExperiments.cs`
(sitter versus mover over mixing and speed; the column profile; the corpse patch), same
output tables, so the two representations can be read side by side. Use `ITestOutputHelper`.

### 5. Do not

Do not remove or alter `VertexField` or `NutrientField` behaviour (rounds 30 and 31 replay on
them). Do not touch `unity/` (the Unity wiring is a separate task). Do not add a corpse or
particle object (separate task). Do not change the chemical sense.

## Deliverable

All tests green (`./scripts/core-test.ps1`, full suite, log in `scratch/logs/`). Report: the
files changed, the interface member added, the along-flow spread number the advection test
prints at speed 0.3 m/s and cell 1 m, the sitter/mover ratios at mixing 0.2 and 0.02 from the
experiment, the geometry test's two intakes, and anything in the seam that did not fit this
spec and what you did about it. Do not commit.
