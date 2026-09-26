# The one-substance economy: build spec

*2026-09-18, evening.* The rules D098 rules, as amended in discussion the same evening, with
the numbers, the Core symbols, the columns and the tests. `economy-inventory.md` is the map
of what the build touches; this is what it builds. The owner's words that settle the design:
"Matter is matter. Absorbing matter costs energy. Energy is produced from contained matter,
and when used that matter returns to the water to preserve matter." And: photosynthetic
cells cannot get energy from what they take in; absorptive cells can.

## 1. The rules

**One matter, in two states.** A unit of matter is a unit wherever it is. A *charged* unit
carries `JoulesPerUnit` (ρ) of energy; a *spent* unit carries none. The water holds both:
spent units dissolved (the field the code calls `Matter`, in units) and charged units as
marine snow (the field the code calls `Nutrients`, in joules, which is charged units × ρ).
A body is charged units: its tissue and its reserve, both in joules. A corpse is charged
units, in joules. Nothing is ever created or destroyed; a unit only changes state and place.

**Energy is a flow, not a stock.** Light enters by charging spent units; heat leaves when
a charged unit is burnt. There is no energy anywhere that is not in a charged unit.

The legs, each an exact transfer:

1. **Fixation** (the photosynthetic cell, and a link with `PhotosyntheticEfficiency`). Light
   capacity `L = irradiance × litArea × Efficiency × dt` joules. Uptake capacity
   `U = UptakeRatePerSquareMetre × litArea × c / (c + UptakeHalfSaturation) × dt` units,
   where `c` is the spent density at the body (units/m³). The cell fixes
   `f = min(L, U × ρ)` joules, which takes `f / ρ` spent units from the body's cell and
   credits `f` joules of income. Light beyond `f` is wasted and never enters the books. The
   take goes through the spent field's demand and share pass exactly as the charged field's
   does for feeders: appetite priced at the cell's density, `Demand`, `FreezeAvailability`,
   `ShareAt`, and a short share scales `f` down. `EnergyIn += f`. Senescence's `wear`
   divides `f` as it divides every intake today.
2. **Burning.** Every joule a body spends (`Upkeep + Neural + Work + Handling`) is a charged
   unit's worth burnt: `EnergyOut += burnt` and `burnt / ρ` spent units are deposited at the
   body's point. A body burns no more than it holds: with `before` its reserve at the start
   of the step, `burnable = max(0, before + Income − Exuded)`, `burnt = min(Expenditure,
   burnable)`, and its reserve becomes `before + Income − Exuded − burnt`, never negative. A
   body that could not pay in full dies that step (reserve 0, as today's `Energy <= 0`).
   D052's excretion and `ExcretionPerJoule` go: this is that leg, at exactly `1 / ρ`.
3. **Eating** (the absorptive and consumer cells). What the mouth draws from the charged
   field (`PoolDrawn`) is charged matter moved into the body: `FoodIncome = PoolDrawn ×
   yield` stays in the reserve, and the rest, `PoolDrawn − FoodIncome`, is returned to the
   charged field at the body's point as faeces (charged, not heat; `DetritusReturnedTotal`
   counts it). `Wasted` no longer books `EnergyOut`. Eating costs: `Handling =
   HandlingCostPerJouleEaten × PoolDrawn` joules, burnt (leg 2), in the ledger's
   `Expenditure`. The satiation cap, the clearance toe, the rationing pass and the contact
   yields are unchanged.
4. **Exudation** (D070) is unchanged: `ExudationFraction × FromLight` to the charged field.
   It is charged matter leaving a body, not heat.
5. **A child** is charged matter given by its parent: `tissue + reserve` from the parent's
   reserve, and `PerOffspringOverheadJoules` burnt (leg 2, deposited at the parent's point).
   No draw from any field. `MatterPerTissueJoule`, `MatterPerCreature`,
   `CheapestPossibleChildMatter`, `ConceptionsBlockedByMatter`, `ConceptionsShortOfMatter`
   and `Organism.LockedMatter` go. The gate is the parent's reserve against
   `price + ReserveMargin × StandingWatts` (§3). A stillbirth (a child `Admit` refuses after
   the parent paid) deposits the child's `tissue + reserve` into the charged field at the
   parent's point; the overhead stays burnt.
6. **Growth** moves reserve into tissue, as today, with no field draw. `GrowthShortOfMatter`
   goes; `GrowthReserveFloor` stays.
7. **Death** makes a corpse of `TissueJoules + reserve` joules (one account; `Corpse.Matter`
   goes). A starved body's reserve is 0 by leg 2, so a starvation corpse is the tissue, as
   today; a diverged body's reserve is no longer booked out as heat. With
   `CorpseDecayPerSecond` 0 the corpse deposits at once into the charged field, as today.
8. **Remineralisation.** Every cell of the charged field loses `stock × (1 − e^(−r·dt))`
   joules a step, `r = RemineralisationPerSecond`, and its spent field gains that over ρ in
   units, in the spent cell containing the charged cell's centre. `EnergyOut += moved`: the
   bacteria's heat. The two floor leaks (`NutrientRemineralisationPerSecond`,
   `MatterRemineralisationPerSecond`, D051, 0 in every campaign config) and the fields'
   `Remineralise(seconds, rate)` go; the new leg is `IMatterField.Remineralise(IMatterField
   spent, double seconds, float ratePerSecond, float joulesPerUnit)` returning the joules
   moved, built on `GridField` and `NutrientField`, and refused by `VertexField` at any rate
   above 0.
9. **Founders and inoculants** are an influx of charged matter: `EnergyIn += energy +
   tissue` as today, and `MatterInfluxedTotal += (energy + tissue) / ρ`. Nothing is taken
   from the water; the experimenter's hand is a source and the books say so.
10. **The reserve cap** (`ReserveCapSeconds`, 0 = off). After the reserve update, a reserve
    above `cap × StandingWatts` is trimmed to it and the excess deposited into the charged
    field at the body's point (charged, not heat; counted with exudation). Off in the base
    round; the lever that moves a hoard into the larder while the body lives.

Sinks, mixing, advection, D074's influx and burial of the spent field, the corpse object,
the satiation cap, the toe, the tank, the bed, light reach, dispersal, senescence and the
mass floor are untouched.

## 2. The two identities

**Energy.** `StandingJoules = Nutrients.TotalJoules + Σ(Energy + TissueJoules) +
CorpseJoules`, unchanged in form. `AuditResidual = EnergyIn − EnergyOut − StandingJoules`
must read 0. `EnergyIn` is fixation plus founders; `EnergyOut` is burning (legs 2 and 5)
plus remineralisation (leg 8) and nothing else.

**Matter.** `World.MatterResidual = Matter.TotalUnits + StandingJoules / ρ −
MatterInitialTotal − MatterInfluxedTotal + MatterBuriedTotal`, a Core property for the first
time (the inventory's four inline copies read it). It must read 0 to the float's rounding:
the harness prints it as `mat resid`, the theatre's census reads it, and every world test
that closes the audit closes this beside it. `MatterInBodies`, `MatterInLivingBodies` and
`mat orphan` go; charged matter in bodies is `Σ(Energy + TissueJoules) / ρ`, printed as
`mat locked`.

The two are one equation in two units: every leg that books `EnergyOut` deposits the same
over ρ into the spent field in the same step, and every leg that books `EnergyIn` takes
the same over ρ from it. Keeping both catches a leg that did one half.

## 3. The breeding margin gene

`ReproductionTraits.ReserveMargin`, seconds, the reserve a parent keeps after a birth as a
multiple of its standing cost. `Organism.ReproductionThreshold = CostJoules(tissue,
overhead) + ReserveMargin × StandingWatts`, and `Conceive`'s gate is the same expression
against the parent's reserve, so a parent below its margin neither attempts nor pays.
Founders draw it uniformly in `[MinReserveMargin, MaxReserveMargin]` = `[0, 600]` s
(`RandomGenomeOptions`); mutation at `MutationRates.MarginChance` 0.08 by the same `Step`
the investment uses, bounded below at 0. It enters `SpeciesDistance` beside brood,
investment and adult scale; the lineage birth row carries it as `rm`; `World.
MeanReserveMargin` and the table's `margin s` column read the living. `GenomeJson.
FormatVersion` goes to 6 and the refusal message names the gene; every stored genome and
snapshot is refused, as the 4 → 5 bump did, and `inocula/growth-ledger-genome.json` is
re-extracted from the smoke. `ConceptionsUnderMargin` counts the attempts the gate refuses.

## 4. The numbers

From round 40 seed 1 at 16,400 s (2,490 bodies) and the leaf of `logbook/specs/r40-read/leaf.json`
(volume 0.0008 m³, lit area 0.0101 m², tissue 0.379 J, standing 0.0023 W, light income
0.101 W at the surface, net 0.083 W):

| Tunable | Value | Why |
|---|---|---|
| `JoulesPerUnit` (ρ) | 100 J | Round 40's crowd held about 550 kJ charged (reserves about 300 kJ, marine snow 252 kJ, tissue under 1 kJ). At ρ = 100 that is 5,500 of the 11,000 units, so the crowd the machine can afford stands at half the budget with the water half stripped. |
| `UptakeRatePerSquareMetre` (k) | 0.3 units/m²/s | A leaf at the surface fixes 0.101 W, 1.0e-3 units/s. At the seeded density (0.11 units/m³) the uptake capacity is 0.3 × 0.0101 × 0.11/0.16 = 2.1e-3 units/s, twice the light capacity, so unstripped water is light-limited and stripped water (c ≪ K) is uptake-limited. At 12 m the light capacity is 1.4e-4 units/s and the leaf is light-limited everywhere. |
| `UptakeHalfSaturation` (K) | 0.05 units/m³ | Half the seeded density: uptake falls to half when the water is half stripped. |
| `RemineralisationPerSecond` (r) | 5e-4 /s | A half-life of 1,400 s for marine snow nobody eats. The larder's inflow is about 100 W (reserves burnt as senescence upkeep are heat, so the inflow is tissue, exudation and faeces), which at this rate stands at about 200 kJ, 2,000 units, a fifth of the budget. |
| `HandlingCostPerJouleEaten` | 0.1 | Eating is not free and is far cheaper than fixing (ρ per unit): a filter feeder keeps 0.9 of what it clears. |
| `ReserveCapSeconds` | 0 (off) | The base round measures the one-substance economy alone. |
| `MatterBudgetUnits` | 3,000 (was 11,000 in this table until the screens) | The count's dial. In a closed one-substance world the count is the capacity over what a breeder holds, and at 11,000 units the smoke built 6,300 bodies in 1,100 s. At an overhead of 100 J the screens plateaued at 400 bodies for 2,000 units, 650 for 3,000 and on the same line for 4,000 (`r41o100b2k-s1`, `r41o100b3k-s1`, `r41o100b4k-s1`, dt 0.02, logbook/0107). |
| `PerOffspringOverheadJoules` | 100 (was 25 until the screens) | The floor under what a breeder holds, and so the count's second dial: however small bodies get, a parent must hold the overhead before it can breed. At 25 J the count was 0.4 bodies a unit at founder sizes and free to drift several-fold as bodies shrank; at 100 J it is about 0.22 a unit at founding with a ceiling near 1.2 a unit at the smallest bodies. Round 40's 12,759 births at 25 J were 319 kJ of burn against 288 kJ of living upkeep, the largest burn in the world; at 100 J it is larger still, and round 41 reads it. |

Two dials were screened and rejected for the count on the night of the build (logbook/0107).
A per-body standing cost does not bound it, because it sets where the water settles and not
how many bodies share it. The tissue value (`EVOSIM_TISSUE_ENERGY`) does bound it, but at
100,000 and 200,000 J/m³ the founders spent their whole stake growing and none bred by
2,500 s (`r41t1e5-s1`, `r41t2e5-s1`), and at 50,000 J/m³ with founders a third the usual
size the same (`r41t5e4b2k-s1`, `r41t5e4b4k-s1`); it stays at 500 J/m³.

The 5 m spent cell (125 m³) holds 14 units at the seeded density, 1,400 J; a surface leaf
draws 1.0e-3 units/s and returns 2.3e-5 units/s as upkeep. A plant in a stripped cell
lives on its own returns and whatever mixes in, which is the treadmill the owner and the
agent agreed is a feature (regenerated production).

## 5. Config, environment, header

Removed tunables: `MatterPerTissueJoule`, `MatterPerCreature`, `ExcretionPerJoule`,
`NutrientRemineralisationPerSecond`, `MatterRemineralisationPerSecond`. Added, all
`[Tunable("world")]` with the unit named: `JoulesPerUnit` (J/unit), `UptakeRatePerSquareMetre`
(units/m²/s), `UptakeHalfSaturation` (units/m³), `RemineralisationPerSecond` (1/s),
`HandlingCostPerJouleEaten` (J/J), `ReserveCapSeconds` (s). `RandomGenomeOptions` gains
`MinReserveMargin`, `MaxReserveMargin`; `MutationRates` gains `MarginChance`. The two
reflection guards (`RunConfigTests`, `RunConfigJsonTests`) prove every one reaches the hash
and the file. Every `config.json` on disk is refused by the new build, per §9; the three
tracked configs under `inocula/` are regenerated from the smoke.

Environment, in `EvolutionRun`: `EVOSIM_RHO`, `EVOSIM_UPTAKE_K`, `EVOSIM_UPTAKE_KS`,
`EVOSIM_REMIN` (repurposed: it set the floor leak), `EVOSIM_HANDLING`, `EVOSIM_RESERVE_CAP`,
`EVOSIM_MARGIN_MIN`, `EVOSIM_MARGIN_MAX`, `EVOSIM_MARGIN_CHANCE`; `EVOSIM_EXCRETION`,
`EVOSIM_MATTER_PER_TISSUE`, `EVOSIM_MATTER_PER_CREATURE` go. The header's matter line
becomes `economy rho 100 J/unit, uptake 0.3 /m2/s at K 0.05 /m3, remin 5e-4 /s, handling
0.1, reserveCap off, margin 0-600 s` and the `excretion` and `matter ... each` tokens go.

## 6. Report and stats

Columns: `mat blk` becomes `upt lim`, the share of photosynthetic body-steps in the window
whose fixation was bound by uptake rather than light (`UptakeLimitedSteps` over
`PhotosyntheticSteps`, both window counters on `World`); `mat short` becomes `burnt`, the
units returned to the spent field by burning in the window; `excreted` becomes `remin`,
the units remineralised in the window; `mat orphan` goes; `mat locked` is charged units in
bodies; `margin s` joins `adult scale`, `invest`, `brood`. `det in`, `det out`,
`det exuded` stay, and `det exuded` includes the reserve cap's trim. The row count is
asserted against `Columns.Length`, so the row builder moves with the header.

Stats fields: `matterLocked` (units in bodies), `matterResidual` (from `World.
MatterResidual`), `burntTotal`/`burntWindow` (units), `remineralisedTotal`/`Window`
(joules), `detritusReturnedTotal`/`Window` (faeces, joules), `uptakeLimitedShare`,
`meanReserveMargin`, `conceptionsUnderMargin`; `conceptionsBlockedByMatter`,
`conceptionsShortOfMatter`, `growthShortOfMatter`, `matterOrphaned`, `excretedTotal`,
`excretedWindow`, `corpseMatter` go. `run.json`'s ending block keeps `matterInfluxedTotal`
and `matterBuriedTotal`.

`scripts/analyse-arm.ps1` reads `upt lim` where it read `mat blk`; `scripts/reads/
rebuild-report.py`'s key list moves column for column; `scripts/absorptive-log.ps1`'s
schema is unchanged (its keys are joules and stay joules) and its fixture is re-recorded
only if a key moves. The per-round readers under `scripts/reads/` and `logbook/specs/
r3x-read/` read the runs they were written for and are not touched.

## 7. The ledger

`LedgerForecast.Forecast` takes a `spentDensity` (units/m³) beside `nutrientDensity` and
runs fixation through the same `min(L, U × ρ)`; `MatterPricePerChild` becomes
`UnitsPerChild = childPrice / ρ` and the table's last column is `units/child`. The lifetime
loop applies handling and the margin gate. `Evosim.Ledger` takes `--spent` (default: the
seeded density from the config's budget over its live volume, or `InitialMatterPerCubicMetre`)
and prints `Standing cost` and `Fixation at surface` in units/s.

## 8. Theatre and scripts

`TheatreReplay` reads `World.MatterResidual` and `World.AuditResidual`; the census labels
stay. `RunRecord` keeps `MatterResidualRecorded` for older runs. `TheatreUi.
MatterResidualFloor` stays at 1e-3 (11,000 units). `TheatreUiCheck`'s three census
assertions stand. Every recorded run is refused by the new build's config reader before
any of this is reached; the theatre built against a run's recorded commit in a worktree is
the way to replay one (HANDOFF's open item).

## 9. Tests

The economy contract in `WorldTests` is rewritten around the new legs, one test per leg
and one per identity: fixation takes exactly `f / ρ` from the spent cell and credits `f`;
uptake binds in stripped water and light binds in rich water; burning deposits exactly
`burnt / ρ`; a body cannot burn what it does not hold and dies at 0; faeces reach the
charged field and never the audit's outflow; handling is burnt; a child moves exactly its
price from the parent and burns the overhead; a stillbirth's joules reach the charged
field; growth draws nothing from any field; a corpse carries tissue plus reserve;
remineralisation moves `stock × (1 − e^(−r·dt))` and books the heat; a founder is an
influx in both books; the reserve cap trims and deposits; the margin gate refuses and
counts; and `AuditResidual` and `MatterResidual` both read 0 across a 3,000 s grid world
with every leg on. `MatterNeverEntersTheEnergyAudit` and the fixed-charge, excretion and
`CheapestPossibleChildMatter` tests go. `BoxPathTests`' two golden values are re-recorded
and the change is stated in the test's comment. `ReproductionTraitsTests`, `GenomeJsonTests`,
`MutationTests`, `SnapshotJoinTests`, `SnapshotReadbackTests` and `SpeciesDistance`'s tests
gain the margin. `LedgerForecastTests` gain the spent density and the units column.
`core-test.ps1` filtered while building, the default suite before the merge, `-All` before
the base round.

## 10. What the smoke reads

A 5,000 s screen at dt 0.02 on round 40's world (tank 2,200 m², 45 m, light reach 6 m,
budget 11,000, seed 1) on the merged build: both residuals 0 on every row; `upt lim`
rising from 0 as the surface strips; `mat top` falling and `mat deep` rising; `detritus J`
settling near 2,000 units' worth; `burnt` and `remin` each window against `det in`; the
crowd against round 40's at the same second; the ledger's leaf at 0, 6 and 12 m under the
new build against the table in §4. The base round's pre-registration takes its bars from
that screen.

## 11. Build notes (2026-09-18, evening)

Built on branches `economy` and `margin` by two Opus agents from this spec, merged by the
agent. What the build settled that the spec above did not say:

- **`ConceptionsUnderMargin` is an invariant, not a bite gauge.** The margin enters
  `ReproductionThreshold`, which `Brood` and the conception order already ask, so the gate
  in `Conceive` refuses nothing extra: a probe at margins 0, 60 and 600 s read 0 in every
  arm. A nonzero means the threshold and the gate have drifted apart, which is how D065's
  fixed term went wrong. How hard the margin bites is read from the time to first birth
  (58 s against 10 s at 600 s in the world test; 72 s against 17.5 s in the ledger) and
  from `margin s` against the reserve's seconds.
- **The mutator's floor is `Step`'s 1e-4**, so a mutated margin never reads a true 0
  although `Validate` admits one.
- **Every founder is an influx** (leg 9), so `MatterInfluxedTotal` is no longer D074's
  influx alone and the identity, not the standing total, is what the open-budget tests pin.
- **The energy audit's test tolerance is 1e-5 relative**, the matter identity's 1e-6: a
  reserve is a float added to and subtracted from every step and drifts about 2e-6 of the
  light over a few hundred steps.
- **A short fixation take is unreachable**: the spent field's demand and share pass prices
  the shortfall before any take, so `FixationShortTakes` reads 0 and is kept as a guard.
- **`World.MatterResidual` is standing minus initial minus influx plus buried**, the
  reverse sign of the harness's old inline expression; every reader compares the absolute
  value.
- **Remineralisation costs 0.52 ms a metabolic step** on round 40's grid (99,495 charged
  cells into 819 spent cells), under 1% of the Core step.
- **The founding draw takes one more number per founder** from the genome rng, so every
  seed is a new realisation from the second founder on, and `BoxPathTests`' golden is
  re-recorded (41 alive, 11 births, 127 deaths, 157 floor spawns over 400 steps of seed 3,
  against 77, 67, 97 and 107): founders drawn over 0 to 600 s of margin mostly wait in
  that dim test world, so the pin moved a long way. It pins sameness, as before.
- **`rebuild-report.py` rebuilds post-D098 runs only**; its one earlier product is already
  written.
- **A UTF-8 BOM on a `.cs` file is a false build difference**, since `simHash` and
  `coreHash` digest bytes; the build's edit scripts had added 32 and every one was stripped.
- **Two ecology tests pin the founder margin at 0** (`LightFieldTests`' wide-against-narrow
  world and `SenescenceTests`' turnover), because at the default range both worlds barely
  breed and the comparison becomes a reading of the draw.
