# Footprint survey — what `WorldAreaSquareMetres` and `PatchCount` actually govern

Read-only survey for the D076 footprint proposal. No files modified.

## 1. `WorldAreaSquareMetres` — every use

- Declared `src/Evosim.Core/RunConfig.cs:253` (default 400 m², `EVOSIM_AREA`). Doc comment
  (`RunConfig.cs:242-251`): "the sun's aperture... the only thing that sets [carrying
  capacity]... a world of finite width receives finite power... larger worlds support more
  life in exact proportion; they do not support a *denser* one." Read at launch
  `unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs:286` (`EVOSIM_AREA`, D053) and written into
  the built config at `EvolutionRun.cs:510`.
- **`World` construction** (`src/Evosim.Core/Ecosystem/World.cs:462-475`) feeds the *same*
  `config.WorldAreaSquareMetres` into three fields: `LightField`, `Nutrients`
  (`NutrientField`), and `Matter` (`NutrientField`). One area, three consumers — it is a
  single knob, not per-field.
- **Is it total or per-patch?** Total. Both `NutrientField.LayerVolume`
  (`Environment/NutrientField.cs:224`: `(WorldArea / PatchCount) * LayerMetres`) and
  `LightField.PatchArea` (`Environment/LightField.cs:98`: `WorldArea / PatchCount`) divide the
  configured area by `PatchCount` to get one patch's share. `PatchWidthMetres` is
  `sqrt(WorldArea / PatchCount)` (`NutrientField.cs:148`, doc `:56-64`) — the "treat the
  footprint as a square, take its side" approximation, chosen because `Mix`'s vertical pass
  already measures diffusion length the same way (a layer's thickness).
- **Layer volume / initial matter stock.** `InitialMatterPerCubicMetre * Matter.LayerVolume`
  is deposited per layer per patch at construction (`World.cs:483-494`): `perCell = config
  .InitialMatterPerCubicMetre * Matter.LayerVolume`, looped over every layer and every patch.
  So total initial matter scales with `WorldArea × WorldDepthMetres × InitialMatterPerCubicMetre`
  regardless of `PatchCount` (dividing the area among more patches just divides the same total
  among more columns).
- **Matter influx** (`RunConfig.cs:401-424`, `World.cs:795-825`). `MatterInfluxPerSecond` is
  **not a density** — doc comment `RunConfig.cs:417-421`: "It is not a density: scaling the
  world's area does not scale the influx." At `MatterInfluxAt = Surface` (default), the total
  `rate * seconds` is split evenly across every patch's surface layer (`World.cs:813-822`,
  `per = amount / patches`) — a fixed total the patch count divides, exactly like D061's own
  rule for area (comment `World.cs:780-786`: "K patches share one deposit rather than each
  getting one"). At `MatterInfluxAt = Vent`, the whole amount lands in one patch/depth
  (`World.cs:802-810`, `Matter.Deposit(-vent.VentDepthMetres, all, vent.VentPatch)`) — no area
  or patch-count dependence at all.
- **Burial** (`RunConfig.cs:440-462`, `World.cs:852-875`). `MatterBurialPerSecond` is a
  **rate constant** (fraction/second) applied per patch to that patch's own floor-layer stock
  (`before * fraction`, looped `for (patch...)`) — so burial *is* density-linked: each patch
  loses the same fraction of whatever its own floor holds, and a world split into more,
  smaller patches buries the same total fraction of the same total stock (linear, no area
  term directly, but the floor-layer stock itself is `WorldArea/PatchCount`-scaled).
- **Light / irradiance.** `LightModel.IrradianceAt` (`Environment/LightModel.cs:208-213`) is
  pure depth: `SurfaceIrradiance` at/above y=0, exponential decay below, **no area term** —
  irradiance is a W/m² intensity, unaffected by `WorldArea`. Area enters only through
  `LightField.IncidentWatts = Model.SurfaceIrradiance * WorldArea` (`LightField.cs:101`) —
  the *total* watts the world caps photosynthesis at (§5A.2b's carrying-capacity mechanism).
- **Shading.** `LightField.Contribute` registers a creature's `TotalLitArea` at its depth
  layer and patch (`LightField.cs:164-186`); `Solve()` (`:218-222`) always runs the pooled,
  whole-world pass (`SolvePooled`, `:230-279`, dividing shading by `WorldArea`) and,
  when `RunConfig.PerPatchShading` (`RunConfig.cs:1011-1038`, `EVOSIM_PATCH_SHADING`) is on,
  additionally runs `SolvePerPatch` (`:287-...`) which divides by `PatchArea` instead — "a
  crowded patch darkens only itself" (D061's endogenous inequality). Off (default) is one
  shared canopy pooled across every patch, bit-identical to pre-D061. There is no symbol
  literally named `patchShade`/`ExcessDensity` in this codebase; the nearest things are
  `PerPatchShading` (shading) and `FluidConfig.TissueExcessDensity` /
  `BuoyancyModel.ExcessDensityFactor` (`Environment/FluidConfig.cs:113`,
  `Environment/BuoyancyModel.cs:48` — D064 buoyancy, size-dependent, unrelated to area/patches).
- **Exudation / detritus deposition per m³.** `ExudationFraction` (`RunConfig.cs:684-753`) is
  a fraction of a living body's own light income, deposited via `Nutrients.Deposit(heightY,
  ledger.Exuded, creature.Patch)` (`World.cs:1021`, `Metabolism.cs:290-291`) — a per-body flux,
  not a density, but it lands in one `LayerVolume` (`WorldArea/PatchCount × LayerMetres`), so
  the *concentration* it produces is inversely proportional to that volume — smaller
  patches/thinner layers concentrate the same exuded joules more.
- **The "refuge."** `FloorRefugeMetres` / `RefugeEdibleFraction` (`RunConfig.cs:516-587`) are
  purely vertical — a thickness of seabed, in metres, that feeding cannot see
  (`NutrientField.IsRefuge`, `:194`) — no area or patch dependence at all; applies only to
  `Nutrients`, never `Matter` (doc `NutrientField.cs:104-118`, `RunConfig.cs:533-536`).
- **`ClearanceMetres`** does not exist anywhere in the repo (grep confirmed 0 hits). The
  question's `ClearanceMetres` appears to be conflating two different symbols:
  `AbsorptiveCell.ClearanceRate` (`Cells/StandardCellTypes.cs:413` — an evolved, per-cell
  m³/s rate, not a world knob) and D062's `SatiationWattsPerCubicMetre` /
  `ClearanceToeDensity` (`RunConfig.cs:653-681`, W/m³ and J/m³ respectively) — both are
  density-typed knobs on feeding, and both are read against whatever `LayerVolume` /
  `PatchArea` currently is, so the same clearance rate reads as a different fraction of a
  patch's stock when `WorldArea/PatchCount` changes.
- **`CurrentField.PatchWidthMetres`** (`Environment/CurrentField.cs:341-374`): **not**
  a `[Tunable]` — a derived geometry field, set once from `Nutrients.PatchWidthMetres` by
  `World`'s constructor (`World.cs:505`: `config.Current?.SetPatchWidth(Nutrients
  .PatchWidthMetres)`), i.e. `sqrt(WorldAreaSquareMetres / HorizontalPatches)`. It buys
  exactly one thing: the vent's horizontal drag *velocity* term (`CurrentField.cs:356-365`) —
  the transport itself (`VentTransportFraction`, `:648-654`) is width-free (derived from
  volume flux, cancels the area term out). At `PatchWidthMetres = 0` (default until the
  world sets it) the drag term is 0 and nothing else about the vent changes.
- **The vent's leg geometry.** `VentDepthMetres` (default 60, must equal
  `RunConfig.WorldDepthMetres` — validated `World.cs:578-586`) and `VentLegMetres` (default 1,
  must be a whole number of `LightLayerMetres` — validated `World.cs:588-598`) are both purely
  vertical/depth quantities; `VentPatch` (default 0) must be `< PatchCount` (validated
  `World.cs:568-576`). None of the three reads `WorldArea` directly; `PatchWidthMetres` is the
  only area-derived quantity the vent touches, and only for drag (above).
- **`EvolutionRun.cs`.** `EVOSIM_AREA` is read once (`:286`) as a float against `new
  RunConfig().WorldAreaSquareMetres` and written straight into `config.WorldAreaSquareMetres`
  (`:510`) — no other area arithmetic lives in that file; everything downstream is `World`'s
  and `NutrientField`/`LightField`'s.

## 2. `PatchCount` and patch semantics

- `RunConfig.HorizontalPatches` (`RunConfig.cs:936-963`, float-as-int, `EVOSIM_PATCHES`,
  default 1). `World.PatchCount` (`World.cs:192`) reads it, floored at 1:
  `Math.Max(1, (int)Config.HorizontalPatches)`.
- **Assignment at birth.** A floor founder draws a *uniform random* patch when `PatchCount >
  1` (`World.cs:1607-1612`: `patch = new Rng(patchSeed).Range(PatchCount)`, its own RNG seed
  slot, drawn only when K>1 so a K=1 run's stream is untouched). An inoculant (D060's assay)
  does the same (`World.cs:1680-1685`). **An offspring inherits its parent's patch** — the
  parameter doc at `World.cs:1727-1729` states it explicitly ("the parent's own patch for a
  reproduction... drawn nowhere"), and `Conceive`/`Admit` are never seen assigning a fresh
  patch to a child anywhere in the grepped call sites.
- **Transport between patches**, three independent mechanisms, all off by default and each
  guarded so a K=1 world or a knob left at 0 draws nothing from the RNG stream (replay
  safety):
  - `Disperse()` (`World.cs:1209-1233`) — `DispersalChancePerStep` (`RunConfig.cs:991-1009`),
    a flat per-metabolic-step probability, split into three equal slices of one RNG draw:
    move to `patch-1`, move to `patch+1`, or stay. Metapopulation-style, not advection.
  - `AdvectBodies()` (`World.cs:1268-1308`) — D066's roll / D067's vent carrying bodies:
    guarded on `current.Rolls && current.Speed > 0f` or `current.VentActive(PatchCount)`
    (needs `VentSpeed > 0f && patchCount >= 2`, `CurrentField.cs:398`). Uses
    `CurrentField.CrossingDirection` / `HorizontalCrossingFraction` at the creature's own
    depth to decide whether the creature crosses its right-hand or left-hand face this step.
  - `NutrientField.Mix`'s horizontal pass (`NutrientField.cs:593-620`, guarded on
    `horizontalDiffusivity > 0f && PatchCount >= 2`) and `Advect`
    (`NutrientField.cs:694-763`, driven by `CurrentField.CrossingDirection` /
    `VentTransportFraction`) move the *fields* (detritus, matter) between patches — the
    field-side counterpart of `Disperse`/`AdvectBodies`. `HorizontalMixingDiffusivity`
    (`RunConfig.cs:965-989`, `EVOSIM_H_MIXING`) is deliberately far smaller than
    `NutrientMixingDiffusivity` (the vertical rate) — "one pool with extra bookkeeping"
    otherwise (doc `:970-977`).
- **Does a patch have a width or position?** A width, `PatchWidthMetres =
  sqrt(WorldArea/PatchCount)` (§1 above) — used only for the vent's drag term. **No position
  at all.** A patch is an index (0..K-1) carried on `Organism.Patch` and on each field cell;
  it is never mapped to an (x, z) coordinate anywhere in `Evosim.Core` or `Evosim.Sim`.
  `unity/Assets/Theatre/WaterBounds.cs:16-21` states this outright: "D061's horizontal
  patches are an index carried on each creature and on each field cell —
  `Organism.Patch` — not a region of space: a creature's patch and its lattice tile are
  unrelated, and two creatures side by side on screen may be in different patches."
- **Ring order — is patch 0 adjacent to K−1?** Yes, always, by construction — a ring, never a
  line: `NutrientField.Mix`'s horizontal pass wraps `(patch + 1) % PatchCount`
  (`NutrientField.cs:605,612,747,757`), `Disperse` wraps the same way
  (`World.cs:1226,1230`), and the class doc (`NutrientField.cs:66-71`) states the reason:
  "no patch is architecturally an edge (a linear row would make the two end patches special
  for no ecological reason)." `CurrentField.LegCoefficient` computes face index `j = (patch -
  VentPatch) % patchCount` the same way (`CurrentField.cs:630-631`), and notes an *odd* patch
  count leaves a seam in the roll's parity at the 0/K−1 boundary (doc `:484-493`) — not a
  breakage of the ring itself, just an asymmetry in which two adjacent patches share an
  up/down parity.
- **`patchShade`** — no such symbol exists; the real mechanism is `RunConfig.PerPatchShading`
  / `LightField.PerPatchShading` (§1 above): off pools every patch's demand into one shared
  canopy (today's world, bit-identical); on, each patch's producers shade only their own
  column, computed against `PatchArea` rather than `WorldArea` (`LightField.cs:287-330`).

## 3. Depth

- `RunConfig.WorldDepthMetres` (`RunConfig.cs:264-274`, default 60). Doc: "Light needs no
  floor... A sinking pool does need one, because energy that falls past the last layer would
  vanish." It sets `NutrientField.LayerCount = Math.Max(1, Ceiling(worldDepth / layerMetres))`
  (`NutrientField.cs:146`) for both `Nutrients` and `Matter` (`World.cs:466-475`, both built
  with the same `config.WorldDepthMetres` and `config.LightLayerMetres`).
- `NutrientField.LayerOf(heightY)` (`NutrientField.cs:249-254`): `heightY >= 0` → layer 0;
  otherwise `(int)(-heightY / LayerMetres)`, **clamped** to `[0, LayerCount-1]` — the deepest
  layer absorbs everything below it (the floor), and layer 0 absorbs everything above the
  surface too (both directions ratchet to an edge layer rather than extending).
  `LightField.LayerOf` (`LightField.cs:142-143`) is the same rule but **not clamped upward**
  — `heightY >= 0f` is layer 0 and it never runs out of layers going down because
  `_demand`/`_factor` just grow (`while (_demand.Count <= layer) _demand.Add(...)`,
  `LightField.cs:178`) — it has no concept of a floor at all, consistent with "light needs no
  floor."
- **The "no top" gotcha, verified in code.** `LightModel.IrradianceAt` (§1 above,
  `LightModel.cs:208-213`) returns the flat constant `SurfaceIrradiance` for **any**
  `heightY >= 0f` — y=0 and y=155 read identically. `NutrientField.LayerOf` puts any
  `heightY >= 0f` in layer 0 (same clamp). Neither the `NutrientField`/`LightField` pair nor
  `FluidEnvironment` (Unity side) impose any upper bound on a body's y position — confirmed:
  `FluidEnvironment.cs` has no bound/clamp logic in `Apply()` (only drag against
  `CurrentField.VelocityAt`, §4 below), and no other bounding code was found for either axis.
  D050 (mentioned in CLAUDE.md) stops *upward net force* at y=0 for the current field
  specifically, not a hard position clamp — this was not re-verified line-by-line here since
  it is outside `WorldArea`/`PatchCount`'s scope, but nothing in the files read contradicts
  CLAUDE.md's account.
- **`belowWorld` counting.** `EvolutionRun.cs:1675-1679`: `if (creature.HeightY <
  -world.Config.WorldDepthMetres) belowWorld++` (and `absorptiveBelowWorld` for the subset).
  This is a **report-time instrument**, not a physics clamp — it counts how many living
  creatures are already deeper than the configured `WorldDepthMetres`, which the ecology
  fields silently treat as the floor layer regardless (per `LayerOf`'s clamp above). A
  nonzero `belowWorld` means the physics let bodies sink past where the ecology says the
  floor is.

## 4. Placement today ("nothing ecological" — verified)

- `Ecosystem.Build` (`unity/Assets/Evosim/Sim/Ecosystem.cs:783-819`): a newborn is tiled on a
  square lattice, `TileSpacing = 100f` (`Ecosystem.cs:93`), `side = 64`
  (`Ecosystem.cs:789`): `origin = ((tile % side) * TileSpacing, creature.HeightY, (tile /
  side) * TileSpacing)`. `tile` comes from a **reused-index pool** (`_freeTiles.Pop()` or
  `_nextTile++`, `Ecosystem.cs:788`) — purely a bookkeeping slot for physics isolation (comment
  `:785-787`: "so two overlapping articulations would depenetrate"), with **no relationship
  to `creature.Patch`** — confirmed again by `WaterBounds.cs:18` ("a creature's patch and its
  lattice tile are unrelated") and by the code itself: `instance.Patch = creature.Patch` is
  set separately, right after `origin` is computed (`Ecosystem.cs:798`), purely so the first
  physics step samples the right roll leg.
- **Founder depth draw**: `height = -rng.Range(0f, Config.FounderDepthSpread)`
  (`World.cs:1599`, default spread 20 m, `RunConfig.cs:232-240`) — a depth only, no x/z. This
  becomes `creature.HeightY`, which is the only spatial coordinate `Ecosystem.Build` reads
  from the creature (besides the RNG-assigned `Patch` index, used only for field
  bookkeeping/roll sampling, not position).
- **What horizontal position is used for, anywhere: nothing ecological.** `CurrentField
  .VelocityAt` has two overloads (`Environment/CurrentField.cs:418, 495`) —
  `(heightY, seconds)` and `(heightY, seconds, patch, patchCount)` — **neither takes an x or
  z coordinate**. `FluidEnvironment.Apply` (`FluidEnvironment.cs:244-247`) calls it with
  `body.transform.position.y` only; `.x`/`.z` are never read. `CreatureSensors.cs:212` reads
  `_nutrients.PatchCount` to validate a patch index, again no coordinate. The field does
  return a horizontal (X, Z) velocity component (§5A.4's cross-flow), applied as drag in
  `FluidEnvironment` and as `creature.Patch` transitions in `AdvectBodies`/`Disperse` — but
  never as a read of or write to actual world-space x/z. `WaterBounds.cs:24-29` and
  DECISIONS.md D066 (line 3068, "Nothing in the base world reads x; with patches, x now means
  which roll leg you are in") say the same in prose; code confirms it.

## 5. What the design says (§5A.4, D061, D067, wrap/periodic/torus/boundary)

- DESIGN.md itself has **no** literal "wrap"/"periodic"/"torus"/"boundary" discussion of a
  physical footprint — those words appear only in unrelated contexts (episode boundary,
  MAP-Elites, a depth "ramp with its maximum at the boundary"). The horizontal-ring
  wraparound and the footprint question live in **DECISIONS.md**, not DESIGN.md:
  - **D061** (`DECISIONS.md:2782+`): the patchy-world decision — horizontal structure,
    throttled exchange, endogenous inequality; the whole basis for `PatchCount`,
    `PatchWidthMetres`, dispersal and per-patch shading above.
  - **D067** (`DECISIONS.md:3099+`): the vent — "one prescribed, analytic, divergence-free
    flow over D061's patches... plume's water half to each side around D061's ring." The ring
    language ("around D061's ring") is DECISIONS.md's own name for the wraparound §2 shows in
    code.
  - **D076** (`DECISIONS.md:3673-3711`, ruled *today*, 2026-09-05) is the decision this survey
    was requested for: "Shared space: creatures share one volume and can touch." Key facts
    recorded there: creatures have been tiled 100 m apart since Milestone 1 on one shared
    collision layer, kept apart by distance alone, and "nothing in the world has ever touched
    anything else; the ecological coordinates are depth and patch only." It records explicitly
    what changes once footprint goes physical: **"patches become regions and the current
    becomes horizontal transport"** — i.e. today's index-only patch and depth-only current are
    both stated as provisional, pending exactly the proposal this survey feeds. It also flags
    that a physical corpse (logbook/0022's rejection, made because tiling had no contact) "is
    reversed by this."
  - No independent `D061`/`D067` text elsewhere describes patches as spatial regions; today's
    patch is consistently "an index... not a region of space" per D076 and `WaterBounds.cs`.

## 6. The two spike facts, and where a boundary rule would go

Both facts are recorded in `logbook/0064-the-crowd-costs-nothing.md` (measured 2026-09-05,
cited verbatim in `DECISIONS.md:3705-3711`):

- **Packing**: "2,000 founders at mean bounding radius 0.63 m did not fit in 10 × 10 × 60 m:
  35% fill of 6,000 m³, the random-sequential jamming fraction in three dimensions; 1,000 fit
  with 17.7 rejections per body. 20 × 20 × 60 at 2,000 is 9% fill and placed easily. **Today's
  `EVOSIM_AREA` 100 cannot hold the populations the world already runs**; the default 400
  can." (`logbook/0064:48-52`) — placement is rejection-sampled on bounding spheres, never
  overlapping (`SharedSpaceSpike.cs:47,719-757`), and depenetration is a real force
  (logbook/0007), so this is a hard placement constraint, not a cosmetic one.
- **Drift**: "Bodies drifted out of the 10 m and 20 m boxes in twelve seconds on the 0.3 m/s
  current alone; x and z are unbounded and tiling hid it. Wrap, reflect or a restoring current
  is a world rule, and the footprint and the boundary are one decision." (`logbook/0064:55-58`)
- **Where a horizontal boundary rule would go, in the current architecture:**
  - `FluidEnvironment.Apply` (`unity/Assets/Evosim/Sim/FluidEnvironment.cs:217-...`) is where
    per-body drag/advection is already computed against `body.transform.position` every fixed
    step (the `.y` read at `:246`) — the natural place to add a restoring force or a
    wrap/reflect check against `.x`/`.z`, since it already gathers `Transform` reads on the
    main thread once per step and applies forces afterward (doc `:200-215`).
  - Alternatively, `Ecosystem.Step`/`World.Step` (`Evosim.Sim/Ecosystem.cs`,
    `Evosim.Core/Ecosystem/World.cs`) is where `Disperse()`/`AdvectBodies()` already mutate
    `creature.Patch` once per metabolic step (`World.cs:1209-1308`) — if a physical footprint
    makes a patch a literal region, a boundary/wrap on literal (x, z) position most likely
    belongs beside these, since they are already the place patch transitions are decided and
    already run at the metabolic-step cadence rather than the physics-step cadence.
  - Newborn **placement** is decided in `Ecosystem.Build` (`Ecosystem.cs:783-819`, §4 above) —
    currently a pure lattice-tile lookup with no ecological content; this is the call site
    that would need to place a newborn inside its patch's actual region once patches become
    spatial, and the same site the packing fact (§6, jamming fraction) constrains.
  - No boundary/wrap logic exists anywhere today — this was confirmed by inspection of
    `FluidEnvironment.cs`, `Ecosystem.cs`, and `World.cs`; the only "boundary" behaviour in the
    whole codebase is CLAUDE.md's documented *vertical* clamps (`LightModel.IrradianceAt`,
    `NutrientField.LayerOf`), which have no horizontal counterpart.

## Table — what changes if the physical box is K patches of W×W side-by-side on a ring

| Quantity | Today | If footprint = K patches, W×W side-by-side | Changes an ecological dose? |
|---|---|---|---|
| **`WorldAreaSquareMetres`** | Total sun aperture / shading denominator; **not** spatial extent (100 m² reference world is "10 m across" while bodies sit on a kilometres-wide lattice, `WaterBounds.cs:24-29`) | Would become literal: `WorldArea = K × W²`, and `W` would need to equal (or derive from) the patch's real physical side — today `PatchWidthMetres = sqrt(WorldArea/K)` is *already* exactly `W` if the box is square, so the geometry is consistent by construction; the free variable becomes **W itself**, driven by the packing constraint (§6: 2,000 founders need ≥20×20, not 10×10) | **Yes** — `WorldArea` is the single denominator for irradiance cap, shading, `LayerVolume`-based density reads (matter, detritus, exudate), and the D062 satiation/toe knobs. Any change to how `W` is chosen changes the *concentration* of every one of those without touching their own [Tunable] values. |
| **`PatchCount` (`HorizontalPatches`)** | An index only; no width, no adjacency in space, ring order enforced by modular arithmetic alone (`(patch+1)%K`) | Would become literal ring topology: K boxes actually arranged side by side, patch k physically adjacent to k±1 (mod K) — the wraparound already assumed by `NutrientField.Mix`, `Disperse`, `CurrentField.LegCoefficient` becomes geometrically true rather than merely bookkept | **Yes, indirectly** — none of the per-patch *rates* (`HorizontalMixingDiffusivity`, `DispersalChancePerStep`, `VentSpeed`) need to change in value, but their physical meaning changes: "throttled exchange between islands" (D061's own framing) becomes "diffusion/advection across an actual shared boundary," which is a different physical process even at the same numeric rate — worth re-validating against Huffaker/Janssen (the sources D061 cites) once contact is real. |
| **`PatchWidthMetres`** | Derived (`sqrt(WorldArea/K)`), used only for the vent's drag *velocity* term; currently 0 until `World` sets it, and nothing else in the sim reads it | Becomes the actual, load-bearing side length of a real spatial region — every creature born into patch k needs an actual placement inside a W×W×WorldDepthMetres box, and `Ecosystem.Build`'s tiling (100 m spacing, side 64, unrelated to patch) has to be replaced or reconciled with it for newborn placement (§4, §6) | **Yes** — currently an inert byproduct that only nudges vent drag; becomes the thing that sets local density (packing fraction) inside one patch and therefore the local dose of every density-typed rule (feeding, matter draws, shading) *within* that patch, on top of the world-total dose §1 already tracks. |
| **`WorldDepthMetres`** | A vertical bound only; sets `LayerCount` for both `NutrientField`s; has **no horizontal analogue** — `LightModel`/`NutrientField.LayerOf` both silently clamp any y ≥ 0 into layer 0 with no upper limit enforced anywhere (the "no top" gotcha), and nothing enforces a lower limit on physical position either (only `belowWorld` counts the overrun after the fact) | Unaffected in its own definition, but a physical footprint needs the **same treatment applied to x and z that depth already gets in the vertical** — i.e. an actual boundary rule (wrap/reflect/restoring current, per logbook/0064) — because today nothing bounds x/z at all, and the drift fact (§6) shows that gap is not hypothetical once bodies share one visible volume: they leave a 10–20 m box in 12 s at 0.3 m/s. | **Yes, most directly of the four** — this is the one dimension with *zero* existing machinery to extend (unlike area/patch count, which at least have a consistent-but-inert geometry already); the footprint and the boundary rule are explicitly "one decision" per D076/logbook/0064, and whichever rule is chosen (wrap vs. reflect vs. restoring current) changes what "density" even means at a patch's edge. |
