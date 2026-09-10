# Per-creature absorptive ledger log — instrument spec

Fable, 2026-09-04. Build AFTER the exudation build (both touch `EvolutionRun.cs`). Agent
work, no ruling needed (an instrument). Motive: `r14c10-s4`'s dissection (0050's closing)
— stomachs that the ledger says should breed at R0 2–6 did not, and no output records
where they were, what density they saw, or why two children died 74–253 s after birth on
a 101 J endowment.

## What is logged

A new append-only JSONL file in the run directory, `absorptive.jsonl`, written by the same
`JsonlWriter` and at the same cadence as a report row (every sample), **one row per
living creature whose developed phenotype has at least one absorptive part**. Fields:

- `t` (s), `id` (the creature's lineage id — the same id `lineage.jsonl` uses, so the
  two join), `age` (s), `gen`, `patch`, `y` (height, m; negative below the surface),
- `volume` (m³, whole body), `absVolume` (m³ of absorptive tissue), `photoArea` (lit
  area, m²), `parts`, `mixotroph` (bool: also carries photosynthetic tissue),
- `energy` (reserve, J), `tissue` (J), `endowment` (J, the genome's),
- `densityHere` (J/m³ the world fed this creature at its own height and patch at this
  sample — the same value `Metabolise` passed to `StepAt`), `share` (the demand-share the
  field granted, 0–1),
- `foodW`, `lightW`, `upkeepW`, `exudedW`, `netW` — the last step's ledger terms
  divided by the step (W), so a reader sees the budget without summing windows,
- `children` (so far), `lastChildT` (s or null).

Deaths: when a creature with an absorptive part dies, one final row with `dead: true`
and the same fields as of its last step, so the terminal budget is on record (the
`DeathCause` column stays `starved` — this row is what discriminates).

## Where the numbers come from

`World.Metabolise` already has `ledger`, `creature`, the density it passed in and the
share; keep the last ledger and density per creature on the creature (or in a
side-dictionary keyed by id, cleared on death) so the report writer can read them
without recomputing. No physics reads; `Evosim.Core` only. Cap the file: if more than
2,000 absorptive creatures are alive, log the first 2,000 by id and a `truncated` count
row, so a stomach bloom cannot make the file the run's biggest.

## Report

One new markdown column appended after `det exuded`: `abs logged` (rows written this
sample). JSONL field `absorptiveLogged`.

## Tests

`WorldTests`: a world with an inoculated pure stomach (use the `Inoculate` path the
assay uses) produces rows whose `densityHere` equals `Nutrients.DensityAt(y, patch)` at
that sample and whose `netW` equals the ledger's `Net / step`; the death row appears
exactly once with `dead: true`; a world with no absorptive creatures writes no rows.
`JsonlWriter` refuses embedded newlines — keep the row single-line (no genome).

## Reader

`scripts/absorptive-log.ps1 <arm>`: per creature, a one-line summary (birth, death,
lifetime, children, mean densityHere, mean netW, min energy) and a per-1,000-s table of
mean depth, mean densityHere and mean netW across the living stomachs — the columns the
dissection could not produce.
