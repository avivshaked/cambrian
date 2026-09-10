# Build spec: bodies that grow, the Unity half (fable-propose-growth.md rule 8 and rule 9)

Repo d:\Projects\experiments\evolution-simulator. Read `CLAUDE.md` (the Commands section on Unity in
batch mode, `run-arm.ps1`, `new-worker.ps1`, the worker gotchas, the divergence gotchas), then
`fable-propose-growth.md`, then the Core that landed uncommitted (`git diff --stat`; read
`src/Evosim.Core/Ecosystem/Organism.cs` for `AdultPhenotype`, `BodyFraction`, `Phenotype`,
`src/Evosim.Core/Development/Phenotype.cs` for `Scaled`, `src/Evosim.Core/Ecosystem/World.cs` for
`Grow` and the aggregates `MeanAdultScale`, `MeanBirthInvestment`, `MeanBroodSize`,
`MeanBodyFraction`, `GrowthShortOfMatter`, `ConceptionsUnderMassFloor`, and `RunConfig.cs`'s `growth`
group: `NewbornReserveFraction`, `GrowthReserveFloor`, `MinNewbornPartKilograms`, `GrowthStepSeconds`).

Rules: write nothing outside the repository (logs under `scratch/logs/`); do not commit; do not
modify anything under `src/` (report anything Core would need instead); smoke only on worker
`unity-w7` (refresh it first with `./scripts/new-worker.ps1 -Workers @(7)` from inside PowerShell
with a real array; workers 2 to 6 run round 32's arms: never launch against them, never stop any
process, never `Stop-Process` on Unity; a sixth short process on worker 7 beside five arms has
session precedent, keep the smoke short). Doc-comment register: why, with the incident; short
sentences; no em-dashes in new prose.

## Waiting is not your job (added 2026-09-09 after an incident)

You cannot sleep and you cannot hold; a previous agent that waited on a suite issued a no-op
shell command every four seconds for an hour and the owner had to stop the session. So: never
poll, never set a Monitor, never issue a command whose purpose is to pass time. Anything that
takes longer than a compile is launched and left: `run-arm.ps1` returns once Unity is started,
and the caller watches the log. Your task ends when the edits are in, the launcher is written,
worker 7 is refreshed and the smoke is launched. Report what you launched (arm name, worker,
log path under `scratch/logs/`) and the caller reads the result. If a compile check is needed,
it is the smoke itself: the log will show `error CS` or the header.

## The fact that forces this

Core now swaps `Organism.Phenotype` for a scaled copy as a body grows, and the Sim already reads
that phenotype live: `FluidEnvironment.cs` (~204) prices drag against
`creature.Phenotype.Parts[i].Volume`, `Ecosystem.cs` (~1339) takes a bounding radius from it, and
`PhenotypeBuilder.cs` (~209) set `body.mass` and the colliders once at birth. Without this half, a
growing body would have drag and lit area from its new size and mass and colliders from its old
one. The two halves ship together.

## What to build

### 1. In-place rescale of a living articulation

- `PhenotypeBuilder` (or a new `PhenotypeResizer` beside it) gains
  `Resize(CreatureInstance instance, Phenotype scaled, FluidConfig fluid)`: for every part `i`,
  in order (Core guarantees the scaled phenotype's parts are in the adult's order and count):
  set the collider's extents from `scaled.Parts[i].HalfExtents` by the part's shape (box size,
  sphere radius, capsule radius and height, as `AddColliderAndVisual` does at build), set
  `body.mass = max(0.001, Volume × DensityKgPerM3)` and then reapply added mass from the new
  volume the way `FluidEnvironment.ApplyAddedMass` does (do not apply it twice), set
  `anchorPosition`/`parentAnchorPosition` from `ChildAnchorLocal`/`ParentAnchorLocal` as
  `ConfigureJoint` does, keeping the rotations, and scale the visual. Rebuild the drag panels
  (`DragPanels`/`DragPanelsPerAxis`) from the new extents. Do not rebuild the articulation and do
  not touch velocities; note in the remark that PhysX keeps the joint state across an anchor
  change and that a smoke checks the body does not jump.
- `instance.Phenotype` becomes the scaled phenotype after a resize, so every live read (drag,
  bounding radius, lit area, sensors) sees the same body the ledger charges for.

### 2. When it runs

- `Ecosystem` keeps, per body, the `BodyFraction` it last applied. Once every
  `GrowthStepSeconds` of simulated time (a counter on the metabolic cadence, not a wall clock),
  every body whose `creature.BodyFraction` differs from the applied one is resized to
  `creature.Phenotype` (Core has already scaled it). A body at fraction 1 whose `Phenotype` is
  the adult object is resized once, on the step it reaches 1, and never again. Newborns are built
  from `creature.Phenotype` (the scaled body), never from `AdultPhenotype`: check every builder
  call site.
- Cost: state the per-resize cost in the remark and keep it off the per-physics-step path.

### 3. The record

- `EvolutionRun.cs`: `stats.jsonl` gains `meanAdultScale`, `meanBirthInvestment`,
  `meanBroodSize`, `meanBodyFraction`, `growthShortOfMatter`, `conceptionsUnderMassFloor`; the
  table gains four columns at the end of `BaseColumns` (append-only): `adult scale`,
  `invest`, `brood`, `body frac` (means over the living, 3 decimals); the header gains
  ` growth reserve=<NewbornReserveFraction> floor=<GrowthReserveFloor> minkg=<MinNewbornPartKilograms> step=<GrowthStepSeconds>`
  unconditionally, per the header convention. Env vars `EVOSIM_NEWBORN_RESERVE`,
  `EVOSIM_GROWTH_FLOOR`, `EVOSIM_MIN_NEWBORN_KG`, `EVOSIM_GROWTH_STEP`, `EVOSIM_INVEST_MIN`,
  `EVOSIM_INVEST_MAX` (the founder investment range, `RandomGenomeOptions.MinBirthInvestment`/
  `MaxBirthInvestment`) and `EVOSIM_ADULT_SCALE_CHANCE`, `EVOSIM_INVEST_CHANCE` (the two dial
  rates) wired the way the field knobs are.
- The theatre (`unity/Assets/Theatre/`) grows a body in Mode A only if it is trivial; otherwise
  leave it and say so.

### 4. Smoke and checks, worker 7 only

- `rounds/launch-r34.ps1` as a copy of `rounds/launch-r32.ps1` (keep the BOM; ASCII only)
  with the growth env vars at the proposal's defaults and the founder investment range 0.25 to
  1.0, default name `r34-s<seed>`. Then a 600 s smoke at dt 0.02:
  `./rounds/launch-r34.ps1 -Seed 1 -Worker 7 -Seconds 600 -Name r34smoke -Dt 0.02`. Expect:
  compile clean, `status ended`, `reason budget`, `audit` 0.0000% and `mat resid` 0 on every row,
  `body frac` below 1 early and rising, `diverged` read closely (a resize that jumps a body is
  the failure mode; if `diverged` is above round 32's smoke's 0, read the dumps and say whether
  the dead bodies had just been resized).
- A jump check: add to the smoke a log line (or a debug counter in the manifest) of the largest
  root displacement across one resize step, and assert in the report that it is under a
  centimetre; if PhysX moves bodies on an anchor change, say so and propose the fix rather than
  hiding it.
- Report: the diff of the Sim files (hunks), the launcher's parameter block, the smoke's arm
  name, worker and log path, how the jump check is logged so the caller can read it, and
  anything that did not fit. Do not wait for the smoke.
