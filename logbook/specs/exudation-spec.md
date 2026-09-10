# Exudation knob — implementation spec (D070)

Written by Fable 2026-09-03 night, for whichever agent builds it once D070's two gating
readings land (`r14c10-s1-flux`'s measured income ≈ 0.2 W; `r14c10-s4` crashes). Do not
build before the gate opens. Default-off, bit-identical when off. No `UnityEngine` in Core.

## The number (review round 5, LITERATURE-REVIEW.md Q10)

Screen at `EVOSIM_EXUDATION 0.15`; bracket 0.05 / 0.13 / 0.20 / 0.37 in reserve. Flat
fraction of intake, no size or growth-phase term (isometric and phase-independent, [LS13]).
Open world-rule choice, not for the builder: per-biomass release independent of
instantaneous photosynthesis (real PER peaks in dim water, [MCP05]); build fraction-of-intake
first as specified below.

## Mechanism

Each metabolic step, a producer deposits `ExudationFraction × LightIncome` into the
nutrient field at its own height and patch. The joules come out of the creature's reserve
(its `Net` falls by that amount), so the audit closes by construction: `EnergyIn` still
counts the gross `LightIncome`, and the exuded joules move from a body (`StandingJoules`)
into `Nutrients.TotalJoules` (also `StandingJoules`).

## Core

1. `RunConfig.ExudationFraction` — `float`, `[Tunable("feeding")]`, default 0, XML doc in
   the house style (what it is, the D070 reason, "⚠ Unmeasured until the review round on
   phytoplankton exudation lands", `EVOSIM_EXUDATION` in the header). The two reflection
   tests (`RunConfigTests`, `RunConfigJsonTests`) must pass unchanged — they will fail
   until the property reaches `Hash()` and the JSON round-trip; follow how
   `ClearanceToeDensity` is wired in `RunConfigJson`.
2. `EnergyLedger` (`Metabolism.cs`): new read-only `Exuded` (J). `Net => Income −
   Expenditure − Exuded`. Both constructors and the `+` operator carry it; `ToString`
   shows it when non-zero. `Wasted` unchanged.
3. `Metabolism.StepAt`: after `wear` is applied to intake (the line that divides
   `intake.FromLight / wear`), `exuded = config.ExudationFraction × lightIncomeAfterWear`.
   Not applied to `FoodIncome` — stomachs do not exude. Clamp the fraction to [0, 1] at
   config load (throw on out-of-range, the way loading refuses rather than defaults).
4. `World.Metabolise`: where `creature.Energy += ledger.Net` — `Net` already carries the
   deduction; add `if (ledger.Exuded > 0f) { Nutrients.Deposit(creature.HeightY,
   ledger.Exuded, creature.Patch); DetritusExudedTotal += ledger.Exuded; }` **before** the
   death check, so a creature that exudes and then dies in the same step still deposits its
   tissue separately. `DetritusExudedTotal` — new cumulative counter beside
   `DetritusDepositedTotal`, same doc pattern; the identity becomes
   `Deposited + Exuded − Taken == Nutrients.TotalJoules`.
5. `LedgerForecast`: nothing to change if it integrates `ledger.Net` — verify, and add a
   test that R0 falls when the fraction rises (same genome, same irradiance).

## Unity (`EvolutionRun.cs`)

- `float exudation = Env("EVOSIM_EXUDATION", new RunConfig().ExudationFraction);` →
  `config.ExudationFraction = exudation;` beside the toe.
- Header: `(exudation > 0f ? " · exudation " + exudation : "")` next to the toe token.
- Report: window delta `LastDetritusExuded`, JSONL fields `detritusExudedTotal` /
  `detritusExudedWindow`, markdown column `"det exuded"` **appended after** `"det out"`,
  row value with `"0.###"`. Append-only: nothing existing moves.

## Tests (Core, all fast)

- `WorldTests`: audit residual < 1e-4 of `EnergyIn` with `ExudationFraction = 0.1` over
  300 steps (copy `EnergyIsConservedAcrossTheWholeRun`); the flux identity with exudation
  on (extend `DetritusFluxCountersReconcileWithTheField` or add a sibling); with the
  fraction 0 the world is step-for-step identical to today (compare a stats field after N
  steps against a world built without the property set — bit-identical when off).
- `MetabolismTests` (or wherever `StepAt` is tested): `Exuded == fraction × LightIncome`,
  `Net` reduced by exactly that, `FoodIncome` untouched.
- `LedgerForecastTests`: R0 monotone non-increasing in the fraction.

## Verify before handing back

`./scripts/core-test.ps1` green; Unity compile check on a refreshed idle worker
(`-batchmode -quit`, grep `error CS` in `scratch/logs/compile-wN.log`); a 300-s arm on that
worker with `EVOSIM_EXUDATION 0.1` whose header shows `exudation 0.1`, whose `det exuded`
column is positive from the first row, and whose audit reads 0.0000%.
