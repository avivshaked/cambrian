# Build spec: bodies that grow, the Core half (fable-propose-growth.md)

Repo d:\Projects\experiments\evolution-simulator. Read `CLAUDE.md`, then `fable-propose-growth.md`
in full, then the seams below. Core is `src/Evosim.Core` (netstandard2.1, C# 9, no UnityEngine, no
dependencies); tests `src/Evosim.Core.Tests` (xunit), `./scripts/core-test.ps1 -Filter <name>` while
iterating, the whole suite in the background at the end (`> scratch/logs/core-test-growth.log 2>&1`,
15 min; five arms are running on this machine, leave them alone). Rules: nothing written outside the
repo; no Unity; no commit; do not touch `unity/` (a second task does the harness); the doc-comment
register is why-with-the-incident, short sentences, no em-dashes in new prose. The proposal's
constants are the defaults; the owner rules on them with the build in hand.

## The seams (mapped 2026-09-08)

- `Genome.Reproduction` (`ReproductionTraits`: `BroodSize`, `OffspringEndowment` J,
  `CostJoules(overhead) = Brood × (Endowment + overhead)`); `Organism.ReproductionThreshold`
  = `CostJoules(overhead + TissueJoules)`; `World.Brood` (~1720) gates on `Energy >= threshold`
  and calls `Conceive` `BroodSize` times; `Conceive` (~1805-1910) mutates, develops the child,
  prices it `endowment + tissue + overhead`, checks the matter gate `Matter.ReachableStock`
  against `MatterPerTissueJoule × tissue + MatterPerCreature`, takes the matter, admits the child.
- `Metabolism.TissueJoules(phenotype, config)` = Σ part volume × the cell type's
  `TissueEnergyPerCubicMetre`; birth and death both call it (§5A.2c).
- `Developer.Expand` folds `accumulatedScale` into half-extents (Developer.cs ~99, 171) and anchors
  (178-180); `DevelopmentLimits.MinPartVolume` prunes a subtree; `PhenotypePart.HalfExtents`,
  `Position`, `Volume`, `SurfaceArea`, `LitArea`, `ParentAnchorLocal`, `ChildAnchorLocal`.
- `Organism`: `Energy`, `Age`, `TissueJoules`, `LockedMatter`, `StandingWatts` (cached at birth,
  "the body does not change"), `Phenotype`. Upkeep is `UpkeepWattsPerCubicMetre × volume`
  (CellType.cs ~158); light income uses `LitArea`; feeding draws `density × clearance × tissue
  volume × dt`; the light field is shaded by `Contribute(heightY, litArea, patch)`.
- `Mutator.MutateReproduction` (Mutator.cs ~98): brood ±1 under `BroodSizeChance`, endowment
  `PerturbPositive` under `EndowmentChance`; `MutateNode` perturbs `Dimensions`.
- `GenomeJson` (fields `brood`, `endowment`; `FormatVersion` 4, refuses older); `SpeciesDistance`
  (~255) and `LedgerForecast`/`AbsorptiveSample` read `OffspringEndowment`; `scripts/ledger.ps1`
  reads genomes through Core.
- `LineageEvent.Birth(...)` rows: `e:"b", t, id, p, k, g, s, abs, jnt, pho, pt`.
- `RunConfig`: every settable property `[Tunable]`, reaches `Hash()`, survives JSON
  (`RunConfigTests`, `RunConfigJsonTests`); `SenescenceDoublingSeconds`; `PerOffspringOverheadJoules`;
  `MatterPerTissueJoule`, `MatterPerCreature`.
- The founders: `GenomeFactory` draws genomes; the floor admits them with `FounderFloat`.
- Divergence record: the lightest link among diverged newborns weighed 0.143 kg.

## What to build

### 1. Genome

- `ReproductionTraits.BirthInvestment` (float, fraction of the parent's tissue value, default 0.5)
  replaces `OffspringEndowment`. `BroodSize` stays. `CostJoules` becomes the investment times the
  parent's tissue joules (the caller passes them) plus brood × overhead.
- `Genome.AdultScale` (float, default 1): one scalar the developer multiplies into every node's
  dimensions, i.e. `Develop` starts `accumulatedScale` at `AdultScale` on every axis.
- Mutation: `BirthInvestment` by `PerturbPositive` under `EndowmentChance` (rename the rate
  `InvestmentChance` if you rename anything, keeping the JSON key stable or bumping the config
  format as §9 demands); `AdultScale` by `PerturbPositive` under a new `MutationRates.AdultScaleChance`
  with the same default as the investment's; brood ±1 as now. Validation: investment finite and
  > 0, adult scale finite and > 0, brood ≥ 1.
- `GenomeJson`: `FormatVersion` 5, fields `brood`, `investment`, `adultScale`; older formats
  refused with the usual message. `SpeciesDistance` uses investment and adult scale where it used
  the endowment. `LedgerForecast`/`AbsorptiveSample` and anything else reading the endowment are
  adapted so `scripts/ledger.ps1` still runs (test it on a new snapshot row if one exists in
  `runs/r32-s1/*/snapshots/`; that world is running, so read with `JsonlWriter.ReadRows`).
- `GenomeFactory`: founders draw `AdultScale` 1 and `BirthInvestment` from the defaults.

### 2. Growth on the organism

- `Organism` gains `AdultPhenotype` (developed once at birth, at `AdultScale`), `BodyFraction`
  (volume fraction of the adult, 0 to 1), and `Phenotype` becomes the scaled body: every
  half-extent, position and anchor times `BodyFraction^(1/3)`, volumes and areas recomputed.
  Implement `Phenotype.Scaled(float linear)` (a copy; the developer's scale folding is the model)
  and re-derive `TissueJoules` with `Metabolism.TissueJoules` on the scaled body so the two figures
  cannot drift. `StandingWatts` is recomputed on every growth step (its remark says the body does
  not change; it now does).
- `World.Grow(seconds)` once per metabolic step after feeding and upkeep: for a body with
  `BodyFraction < 1`, the affordable tissue is
  `min(Energy − GrowthReserveFloor × TissueJoules, matterReachable / MatterPerTissueJoule,
  adultTissue − TissueJoules)`, clamped at 0; move it from `Energy` into `TissueJoules`, take
  `MatterPerTissueJoule × Δ` from `Matter` at the body's point and add it to `LockedMatter` (so
  `MatterInBodies` and the D074 identity stay closed), and set the new `BodyFraction` and scaled
  phenotype. A short matter take (the field returns less than asked) grows only what was paid for.
  Count `GrowthShortOfMatter` on the world. Both books must close by construction.
- `RunConfig` tunables (`[Tunable("growth")]`): `NewbornReserveFraction` 0.2,
  `GrowthReserveFloor` 0.1, `MinNewbornPartKilograms` 0.5, `GrowthStepSeconds` 10 (the cadence at
  which the harness applies the scaled body to the physics; Core grows every step and the harness
  reads `BodyFraction` at its cadence). Remarks in the house register with the proposal's reasons.

### 3. Birth

- `Brood`: the gate is `Energy >= BirthInvestment × TissueJoules + brood × overhead`. When it
  passes, the parent spends the investment: each child's share is `BirthInvestment × TissueJoules
  / BroodSize`; body value `share × (1 − NewbornReserveFraction)`, reserve `share ×
  NewbornReserveFraction`.
- `Conceive` per child: mutate, develop the adult body at the child's `AdultScale`; the child's
  `BodyFraction` is `min(1, bodyValue / adultTissue)`; the surplus above the adult body stays with
  the parent. The floor: if any part of the scaled newborn has mass `volume × 1000 kg/m³` below
  `MinNewbornPartKilograms`, the conception does not happen (count `ConceptionsUnderMassFloor`),
  and the parent keeps its reserve. Matter at conception is `MatterPerTissueJoule × tissue(scaled)
  + MatterPerCreature`, under the existing gate and take. The overhead is burned as now.
- Founders are born as children are: adult body developed at their `AdultScale`, placed at the
  fraction `BirthInvestment / BroodSize × (1 − NewbornReserveFraction)` of their own adult value
  (cap 1), with the reserve the floor gives them now scaled the same way; the founder float stays.
- `LineageEvent.Birth` gains `bf` (birth fraction) and `as` (adult scale); death rows unchanged.

### 4. World aggregates for the record

Expose on `World`: mean `AdultScale`, mean `BirthInvestment`, mean brood over the living; mean
`BodyFraction` over the living; `GrowthShortOfMatter`, `ConceptionsUnderMassFloor` counters. The
harness task prints them.

### 5. Tests (`GrowthTests.cs`)

- A child is born at the investment over the litter as a fraction of its adult body, with the
  reserve split by the constant, and the parent's energy falls by exactly the investment plus
  overhead; a share above the adult body is capped and the surplus stays with the parent.
- A newborn part under the mass floor is not conceived and nothing moves.
- Growth moves reserve into tissue and matter from the cell into the body at the stated rates;
  a body reaches `BodyFraction` 1 and stops; a body short of matter grows only what it paid for.
- The scaled phenotype's volumes are the adult's times the fraction, and its tissue joules by
  `Metabolism.TissueJoules` equal the ledger's to the last float.
- The audit and the matter identity close to 1e-6 relative over 3,000 steps of a grid world
  with growth, births, deaths, corpses, influx, burial and a rolling current (port the grid
  closure test; set the floor to 3,000 s so founders stop).
- Mutation moves each dial and JSON round-trips a genome with the three fields; a format-4
  genome is refused.
- The whole suite green, including the config guards and the ledger tools' tests.

### 6. Report

Files changed with the diff stat, the test names, the full-suite verdict line, a sample lineage
birth row, the aggregates from the closure test's last step (means of the three dials and the
body fraction), and anything in the seam that did not fit the spec and what you did about it.
