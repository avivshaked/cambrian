# Economy rebuild inventory (D098: one substance)

*2026-09-18, evening.* A read-only sweep of the repository by an Opus subagent, briefed by
the agent to map every place a joule or a unit of matter is read, written, reported, tested
or described, ahead of `economy-spec.md`. Nothing was edited. The scanner it wrote for the
test count is `logbook/specs/economy-inventory/scan.py`. The agent's own notes on what the sweep
means for the spec are at the end. Line numbers are the tree at commit `8c75b9a`.

Two structural facts frame everything below:

- **`World.Matter` and `World.Nutrients` are both `IMatterField`, and the interface's
  vocabulary says "joules" throughout** (`TotalJoules`, `Deposit(at, joules)`,
  `EdibleDensityAt`). The matter field is the same type carrying a different unit by
  convention only; `World.cs:299-314` says so in prose and nothing enforces it. Under one
  substance the two fields become inorganic and organic pools of the *same* unit, so this
  naming stops being a lie, but every call site still has to be re-read for which pool it
  means.
- **The two books are computed in different places.** The energy audit lives in Core
  (`World.AuditResidual`); the **matter identity has no Core property**. It is recomputed
  by hand in three places (`EvolutionRun.cs:2513`, `TheatreReplay.cs:301`, and inline in
  about six tests). That is a hole the rebuild should close with a `World.MatterResidual`
  property.

---

## 1. Matter: every read and write in `src/Evosim.Core`

### 1a. The tunables (`src/Evosim.Core/RunConfig.cs`)

| Line | Tunable | Group/unit | What it does |
|---|---|---|---|
| 423 | `MatterPerTissueJoule` | `[Tunable("world")]` | Matter per joule of tissue, at conception **and** at growth |
| 454 | `MatterPerCreature` | `[Tunable("world")]` | D065's fixed per-body charge; **D098 deletes this** |
| 567 | `InitialMatterPerCubicMetre` | `[Tunable("world")]` | Seed density (default 1) |
| 601 | `MatterBudgetUnits` | validated property | Held total; 0 = the density rule |
| 776 | `MatterSinkMetresPerSecond` | | Matter field sink |
| 795 | `MatterMixingDiffusivity` | | 2 m²/s in the campaign |
| 806 | `MatterRemineralisationPerSecond` | `[Tunable("world", "1/s")]` | Matter floor→water leak, default 0 |
| 393 | `NutrientRemineralisationPerSecond` | `[Tunable("world", "1/s")]` | Detritus floor→water leak, default 0. **D098 repurposes this leg as detritus→inorganic** |
| 831 / 845 / 869 | `MatterInfluxPerSecond`, `MatterInfluxAt`, `MatterBurialPerSecond` | | D074's open budget |
| 921 | `ExcretionPerJoule` | `[Tunable("world","matter/J")]` | `min(locked − MatterPerCreature, this × upkeep)` returned per step; **D098 turns this leg into upkeep's organic→inorganic return** |
| 1131 | `CorpseDecayPerSecond` | | 0 = deposit at death |
| 1003 / 1100 | `FieldMatterKernelMetres`, `FieldMatterCellMetres` | | The matter field's reach/resolution |

Config plumbing is **reflection-driven**: `ConfigSchema.Walk` (`Config/ConfigSchema.cs:145`)
collects every `[Tunable]`, and `RunConfigJson.Write` (`Serialization/RunConfigJson.cs:42-72`)
emits them by group. Adding or removing a tunable needs no serializer edit, but it makes
every `config.json` on disk unreadable (§9's refuse-rather-than-default;
`RunConfigJson.FormatVersion = 2` at line 44).

### 1b. `Organism` and `World` state

| File:line | What |
|---|---|
| `Ecosystem/Organism.cs:219` | `LockedMatter`, the per-body matter store. Comment at 212-214 states the D065 composition |
| `Ecosystem/World.cs:328` | `StandingMatter => Matter.TotalJoules + MatterInBodies + CorpseMatter` |
| `World.cs:331` | `MatterInBodies`, the running sum |
| `World.cs:357` | `CorpseMatter`, O(n) over `_corpses` |
| `World.cs:377` | `MatterInitialTotal`, recorded at construction |
| `World.cs:394` / `400` | `MatterInfluxedTotal`, `MatterBuriedTotal`, the identity's only two holes |
| `World.cs:413` | `ExcretedTotal` |
| `World.cs:471` | `MatterInLivingBodies`, an O(n) instrument; `MatterInBodies − this` is `mat orphan` |
| `World.cs:498-523` | `CheapestPossibleChildMatter`, the cached gate, folds `MatterPerCreature` in at 514-516 |
| `World.cs:531` / `538` / `552` | `ConceptionsBlockedByMatter`, `ConceptionsShortOfMatter`, `GrowthShortOfMatter` |
| `Ecosystem/Corpse.cs` | `Joules` and `Matter` as two `double` accounts on one object |

### 1c. Every place matter moves

| File:line | Leg | What it does |
|---|---|---|
| `World.cs:1015-1049` | **seed** | `MatterBudgetUnits / LiveVolumeOf(...)` else `InitialMatterPerCubicMetre`; `SeedUniform` per field type; `MatterInitialTotal = Matter.TotalJoules` at 1049 |
| `World.cs:1366, 1371, 1387, 1397, 1408` | **field ops** | `Matter.Settle / Remineralise / Mix / Advect / Cull` in `Step` |
| `World.cs:1440-1596` | **influx** | `DepositMatterInflux`: vertex `Emit`, grid/cell `Deposit(0f, per, patch)` at 1539; credits `MatterInfluxedTotal` |
| `World.cs:1838-1869` | **burial** | `BuryMatter`: `TakeFromLayer(floor, patch, wanted)`; `MatterBuriedTotal += before − after` |
| `World.cs:2085-2101` | **excretion** | `excretable = max(0, LockedMatter − MatterPerCreature)`; `excreted = min(excretable, ExcretionPerJoule × ledger.Upkeep)`; `Matter.Deposit`, `LockedMatter -=`, `MatterInBodies -=`, `ExcretedTotal +=` |
| `World.cs:2165, 2187-2219` | **growth draw** | gate `Matter.ReachableStock / matterRate`, then `Matter.Take`; a short take shrinks the growth; refund `Matter.Deposit` at 2219 and 2243 |
| `World.cs:2252-2256` | **growth lock** | `LockedMatter += paidMatter; MatterInBodies += paidMatter` |
| `World.cs:2322-2338` | **death → corpse** | corpse founded with `(TissueJoules, LockedMatter)`; `MatterInBodies -= LockedMatter`; both zeroed |
| `World.cs:2358-2362` | **death → field** | `Matter.Deposit(creature.Point, LockedMatter)`; `MatterInBodies -=` |
| `World.cs:1679-1687` | **corpse decay** | `Matter.Deposit(at, given)` per instalment; corpse matter is **not** credited to any cumulative counter |
| `World.cs:2829-2834` | **conception gate 1** | cheap-child pre-gate; `ConceptionsBlockedByMatter++` |
| `World.cs:2915` | **conception price** | `matterPrice = MatterPerTissueJoule × tissue + MatterPerCreature` |
| `World.cs:2927-2932` | **conception gate 2** | exact-price gate against `ReachableStock` |
| `World.cs:2975-2996` | **conception take** | `Matter.Take(parent.Point, matterPrice)`; refund + `ConceptionsShortOfMatter++` on a short take; `MatterInBodies += taken` |
| `World.cs:3029` | **child lock** | `child.LockedMatter = matterPrice` |
| `World.cs:3140-3145`, `3275-3280` | **founders/inoculants** | `Admit` with **no** matter charge; `LockedMatter` stays 0 (the comment at 3173 states the rule) |
| `World.cs:2402-2417` | **diverged death** | `KillDiverged` → `Bury(..., DeathCause.Diverged)`, the same legs |
| `Environment/IMatterField.cs:129-194` | **the API** | `Deposit`, `Take`, `ReachableStock`, `TakeFromLayer`, `StockInLayer`, `Remineralise`, `Settle`, `Mix`, `Advect`, `Cull`, `TotalJoules` |
| `Environment/GridField.cs:1387`, `NutrientField.cs:609`, `VertexField.cs:845` | **remineralise** | three implementations; Grid's is exact rather than capped-Euler |

Matter identity (recomputed, never a Core property):
`MatterInitialTotal + MatterInfluxedTotal − MatterBuriedTotal − StandingMatter`.

---

## 2. Energy: every place it enters or leaves

### 2a. Income

| File:line | What |
|---|---|
| `Cells/StandardCellTypes.cs:359-362` | `PhotosyntheticCell.Acquire` → `CellIntake.Light(irradiance × litArea × Efficiency × seconds)`. `DefaultEfficiency = 0.05f` (line 331), `DefaultUpkeepWattsPerCubicMetre = 3f` (line 340). **This is the leg D098 replaces with a saturating inorganic uptake `k·surface·c/(c+K)`** |
| `StandardCellTypes.cs:148-153` | `LinkCell.Acquire`, the same light leg when `PhotosyntheticEfficiency > 0` |
| `StandardCellTypes.cs:479-497` | `AbsorptiveCell.Acquire`: `density × clearance` (clearance toe at 484-487, satiation cap at 490-494), returns `CellIntake.Food(drawn, Yield)`. Defaults `clearanceRate = 1.0f, upkeep = 4f, yield = 1f` (line 434) |
| `StandardCellTypes.cs:662-676` | `ConsumerCell.Acquire`: carrion from the detritus field + contact tissue at `YieldAgainst(contact)`. Defaults `biteRate = 20f, upkeep = 6f, scavengeRate = 1f` (570-575) |
| `StandardCellTypes.cs:39, 276, 783` | `StructuralCell`, `NeuralCell`, `BuoyancyCell` → `CellIntake.None` |
| `Cells/CellContext.cs:139-186` | `CellIntake`: `FromLight`, `FromPool`, `PoolDrawn`; `Food(drawn, yield)` is where assimilation efficiency already lives |
| `World.cs:1940-1978` | `Metabolise` appetite pass: `Field.Clear/Contribute/Solve` (light), `Nutrients.EdibleDensityAt`, `Metabolism.StepAt`, `Nutrients.Demand(point, PoolDrawn)` |
| `World.cs:1982-2030` | rationing pass: `FreezeAvailability`, `ShareAt`, re-price at rationed density, allowance cap at 2018-2019, `Nutrients.Take` at 2027, `DetritusTakenTotal +=`, `PoolShortTakes++` on a short take |

### 2b. Costs

| File:line | What |
|---|---|
| `Cells/CellType.cs:158` | `Upkeep` = `UpkeepWattsPerCubicMetre × volume × seconds` |
| `Cells/CellType.cs:52`, `StandardCellTypes.cs:35/127/253/344/434/571/766` | per-type upkeep: structural 1, link 2.5, neural 5, photo 3, absorptive 4, consumer 6, buoyancy 2.5 W/m³ |
| `StandardCellTypes.cs:185` | `LinkCell.Upkeep` override (idle actuator) |
| `StandardCellTypes.cs:785` | `BuoyancyCell.Upkeep` (lift) |
| `Ecosystem/Metabolism.cs:274-284` | neural cost: `neurons × NeuralCostPerNeuronWatts + connections × NeuralCostPerConnectionWatts`, times `NeuronCostMultiplier`, times seconds |
| `Metabolism.cs:242-244, 292-296, 313` | senescence `wear`: divides intake, multiplies upkeep and neural |
| `Metabolism.cs:314` | work: `max(0, workJoules) × WorkCostMultiplier` |
| `Metabolism.cs:308-310` | exudation: `ExudationFraction × intake.FromLight` |
| `Metabolism.cs:100, 130, 133` | `Expenditure = Upkeep + Neural + Work`; `Net = Income − Expenditure − Exuded`; `Wasted = PoolDrawn − FoodIncome` |
| `Metabolism.cs:335-344` | `StandingWatts`, the dark-world expenditure, cached on `Organism.StandingWatts` |
| `Metabolism.cs:354-367` | `TissueJoules = Σ volume × TissueEnergyPerCubicMetre` |
| `Cells/CellType.cs:99-117` | `TissueEnergyPerCubicMetre`, default **500 J/m³**, settable post-construction, refuses ≤ 0 |

### 2c. Bookkeeping

| File:line | What |
|---|---|
| `World.cs:684-685` | `EnergyIn`, `EnergyOut` |
| `World.cs:698-715` | `StandingJoules` = `Nutrients.TotalJoules + Σ(Energy + TissueJoules) + CorpseJoules` |
| `World.cs:718` | `AuditResidual = EnergyIn − EnergyOut − StandingJoules` |
| `World.cs:730-738` | `StandingTissueJoules` |
| `World.cs:2076-2077` | per creature per step: `EnergyIn += LightIncome`; `EnergyOut += Expenditure + Wasted` |
| `World.cs:2064-2071` | exudation deposit: `Nutrients.Deposit(point, Exuded)`, `DetritusExudedTotal +=` |
| `World.cs:2093-2096` | `creature.Energy += Net`, `Lifetime += ledger`, `StandingWatts` refresh at 2091 |
| `World.cs:2104-2110` | death check: `Energy <= 0` → `Bury(..., Starved)` |
| `World.cs:2313` | death: `EnergyOut += creature.Energy`, then zeroed |
| `World.cs:2348-2349` | death deposit: `Nutrients.Deposit(point, TissueJoules)`, `DetritusDepositedTotal +=` |
| `World.cs:1668-1676` | corpse decay: `Nutrients.Deposit(at, given)`, `DetritusDepositedTotal +=` |
| `World.cs:2247-2250` | growth: `Energy -= spend`, `TissueJoules = actual`, `BodyFraction` refreshed |
| `World.cs:2904-2906` | conception price `tissue + reserve + PerOffspringOverheadJoules`, gated on the parent's reserve |
| `World.cs:2998, 3005` | `parent.Energy -= price`; `EnergyOut += PerOffspringOverheadJoules` |
| `World.cs:3432` | stillbirth: `if (kind == Reproduction) EnergyOut += energy` |
| `World.cs:3486` | founder/inoculant: `EnergyIn += energy + tissue` (the only other creation of energy) |
| `World.cs:435, 449, 455` | `DetritusDepositedTotal`, `DetritusExudedTotal`, `DetritusTakenTotal`; identity `deposited + exuded − taken == Nutrients.TotalJoules` |
| `Organism.cs:317-318` | `SecondsOfReserve = Energy / StandingWatts`, read by the Energy sensor |
| `Organism.cs:349-350`, `Genome/ReproductionTraits.cs:82-83` | `ReproductionThreshold = BirthInvestment × TissueJoules + BroodSize × overhead` |

---

## 3. Report columns and `stats.jsonl` fields in joules or matter

### 3a. Table columns, as printed (`EvolutionRun.cs:3286-3357`, `BaseColumns`)

Row values are built at `EvolutionRun.cs:2980-3157`; the count is asserted against
`Columns.Length` at 3175-3180, so a column added or removed without the matching row value
throws.

**Joules:** `work J/s`, `work share`, `food %`, `detritus J`, `J/m3 here`, `% on floor`,
`det deep`, `audit`, `det here ed`, `refuge J`, `det patch sd`, `patch max share`, `det in`,
`det out`, `det exuded`, `food jnt`, `food rig`, `det cv`, `floor J` (shaped bed, the last
two before the `pN` block).

**Matter:** `mat top`, `mat deep`, **`mat blk`**, `mat locked`, `excreted`, `mat in`,
`mat buried`, `mat orphan`, `mat here`, **`mat resid`**, `mat short`, `mat cv`, `vtx`
(prints `detritus/matter` counts).

**Economy-adjacent, unit-free but affected:** `adult scale`, `invest`, `brood`, `body frac`,
`corpses`, `stillb`, `diverged`, `floor`, `photo`/`photo inh`, `absorpt`/`inherit`,
`p0..pN`.

Header prose naming the knobs (`EvolutionRun.cs`): line 926 `sink … matter … m/s`, 927
`remin … /s`, 928 `excretion … /J`, 949-951 `matter in …/s at …, burial …/s`, 976
`maxTissue=`, 990-991 `matter <perTissue>/J + <perCreature> each from <initial>/m3`, 1000
`sense scale chem … J/m3`, 1024 `matterBudget`.

### 3b. `stats.jsonl` fields (`EvolutionRun.cs:2717-2935`)

**Joules:** `workJoulesPerSecond` (2726), `spendJoules` (2727), `workJoules` (2728),
`lightJoules` (2729), `foodJoules` (2730), `detritusJoules` (2733), `detritusHere` (2734),
`detritusOnFloor` (2735), `detritusDeep` (2736), `edibleDetritusHere` (2763),
`refugeJoules` (2767), `detritusPatchSd` (2771), `detritusDepositedTotal`/`Window`
(2775-2776), `detritusTakenTotal`/`Window` (2777-2778), `detritusExudedTotal`/`Window`
(2782-2783), `auditResidual` (2758), `foodJointed`/`foodRigid` (2816-2818), `corpseJoules`
(2868), `detritusCv` (2913), `floorStockJoules` (2921).

**Matter:** `matterHere` (2748), `matterSurface` (2749), `matterDeep` (2750),
`matterStanding` (2751), `conceptionsBlockedByMatter` (2752), `matterLocked` (2766),
`excretedTotal`/`excretedWindow` (2768-2769), `matterInfluxWindow`/`matterBuriedWindow`
(2802-2803), `matterInfluxedTotal`/`matterBuriedTotal` (2804-2805), `matterOrphaned`
(2846), `matterVertices` (2855), `matterResidual` (2859), `conceptionsShortOfMatter`
(2860), `corpseMatter` (2869), `growthShortOfMatter` (2883), `matterCv` (2914).

Derived at 2494 (`residual` as a percent of `EnergyIn`), 2512 (`matterOrphaned`),
2513-2514 (`matterResidual`). `run.json`'s ending block also carries
`matterInfluxedTotal` / `matterBuriedTotal` (1976-1977).

**Core's own `WorldStats`** (`Ecosystem/WorldStats.cs`) is a separate, much smaller sample
used by tests and smokes: `energyIn` / `energyOut` (133-134), `reserve` (96). It does not
carry matter at all.

`AbsorptiveSample` (`Ecosystem/AbsorptiveSample.cs:224-253`) writes per-creature `energy`,
`tissue`, `investment`, `densityHere`, `share`, `foodW`, `lightW`, `upkeepW`, `exudedW`,
`netW`.

`LineageEvent` birth rows (`Ecosystem/LineageEvent.cs:189-201`) carry `bf` (birth fraction)
and `as` (adult scale); **a `ReserveMargin` gene would naturally add a third here.**

---

## 4. Core tests that assert on joules or matter

**224 test methods across 44 files** (scanned for `[Fact]`/`[Theory]` bodies containing
both an `Assert.` and an economy term). The heavy ones:

| File | Class | Count | Nature |
|---|---|---|---|
| `WorldTests.cs` | `WorldTests` | **31** | the whole economy contract: `EnergyIsConservedAcrossTheWholeRun`, `MatterNeverEntersTheEnergyAudit`, `MatterIsConservedBecauseNothingCreatesIt`, `MatterIsConservedWithAFixedPerCreatureCost`, `ExcretionNeverDrainsTheFixedMatterTerm`, `AChildLocksTheProportionalPricePlusTheFixedOne`, `AFixedCostAloneStillCharges`, `TheCheapestPossibleChildIncludesTheFixedCost`, `ExcretionPerJouleDefaultZeroLeavesLockedMatterUnchangedFromConception`, `ExcretionMovesExactlyWhatItDebitsInAQuietStep`, `ExcretedTotalIncrementsByExactlyWhatExcretionDebits...`, `ExcretionCapsAtWhatTheBodyStillHolds`, `MatterIsConservedWith{Remineralisation,Excretion}Running`, `ReproductionCostsExactlyWhatTheGenomeSays`, `AWorldWithNoMatterCannotBreedHoweverMuchLightItHas`, `InoculateCreditsExactlyWhatItCreates...`, `EnergyIsConservedWith{Exudation,Remineralisation,AFloorRefuge}Running`, `DetritusFluxCountersReconcileWithTheField` (×2), `NoCreatureEverHoldsNegativeEnergy`, `ATissueCeiling*` (×2), `SuccessAtADepthStripsThatDepth`, `NutrientRemineralisationPerSecondReachesTheArithmetic`, the refuge trio |
| `GridFieldTests.cs` | `GridFieldTests` | **21** | field mechanics + `AGridWorldClosesItsAuditAndItsMatterIdentity`, `AVentInfluxLandsInThePlumeAndTheIdentityStillCloses`, `ATakeDeliversWhatTheGatePromised`, `EveryOperatorConservesTheTotal` |
| `VertexFieldTests.cs` | `VertexFieldTests` | **15** | the same shape; `AVertexWorldClosesItsAuditAndItsMatterIdentity`, `BurialRemovesWholeVerticesRestingOnTheFloor` |
| `CurrentAndMixingTests.cs` | | 13 | `MixingConservesEveryJoule`, five `Remineralise*` tests, refuge |
| `OpenMatterBudgetTests.cs` | `OpenMatterBudgetTests` | **11** | D074's influx/burial identity: `TheIdentityClosesInALivingWorld`, `TheIdentityClosesOverATwoByTwoWorld`, `BothKnobsAtZeroChangeNothing`, `AllThreeReachTheHashAndTheFile`, the burial quartet |
| `GrowthTests.cs` | `GrowthTests` | **10** | `GrowthMovesReserveIntoTissueAndMatterFromTheCellIntoTheBody`, `ABodyShortOfMatterGrowsOnlyWhatItPaidFor`, `AChildIsBornAtTheInvestmentOverTheLitterAndTheParentPaysExactlyThat`, `AGrowingGridWorldClosesItsAuditAndItsMatterIdentity`, the mass-floor pair |
| `TankTests.cs` | `TankTests` | 10 | `ATankRunsAndBothBooksClose`, `ABudgetSetsTheTotal...`, `ABudgetOfZeroIsTheDensityRuleExactly` |
| `PatchyWorldTests.cs` | | 10 | conservation under K patches |
| `CellTypeTests.cs` | | 7 | `AcquisitionIsGrossNotNet`, `ConsumerYieldsMostFromCarrion...`, `UpkeepIsPerInstanceSoARunCanSweepIt` |
| `AbsorptiveLogTests.cs` | | 7 | the per-creature log's fields |
| `CorpseTests.cs` | `CorpseTests` | 6 | `AGridWorldWithCorpsesClosesItsAuditAndItsMatterIdentity`, half-life, last crumb |
| `LedgerForecastTests.cs` | | 6 | see §5 |
| `BedGridTests.cs` | | 5 | refuge stock over a shaped floor |
| `RollCellTests.cs` | | 5 | advection conservation |
| `ObservationTests.cs` | | 5 | `WorkIsSpentExactlyOnceAndTheAuditStillCloses` |
| `SenescenceTests.cs` | | 4 | `SenescenceStillClosesTheEnergyAudit`, `AtTheDoublingTimeCostsDoubleAndYieldHalves` |
| `ExudationTests.cs` | | 4 | D070's leg |
| `VentTests.cs` | | 4 | vent conservation |
| `SharedSpaceTests.cs` | | 4 | `ARefusedConceptionLeavesTheParentsEnergyAndMatterExactlyWhereTheyWere` |
| `ConceptionOrderTests.cs` | | 4 | `TwoEqualParentsAndOneChildsWorthOfMatter`, `OneRichParentAndOnePoorOne` |
| `AllocationTests.cs` | | 3 | `ACappedMouthIsCreditedOnlyWhatTheWaterGave`, `AStillbirthLocksNoMatterInABodyThatDoesNotExist` |
| `DivergenceTests.cs` | | 3 | `TheEnergyAuditAndTheMatterIdentityBothStillClose` |
| `CalibrationSweep.cs`, `RefugeImpulse.cs`, `GridFieldExperiments.cs`, `VertexFieldExperiments.cs` | | 3/1/4/1 | `Slow`-trait experiments; they need recalibrating rather than rewriting |
| the rest | | 1-3 each | `EnergyKnobTests` (2, both reflection-shaped: "every cost/income in the ledger responds to a config change"), `ReproductionTraitsTests` (2), `NeuralCellTests`, `SizeBoundsTests`, `ProducerCountTests`, `TrophicMarginTests`, `JointMarginTests`, `SensorPoolTests`, `LineageEventTests`, `ConservativeTransportTests`, `CurrentTransportTests`, `BoxPathTests`, `RunConfigTests`, `RunConfigJsonTests`, `RunDirectoryTests`, `FluidConfigTests` |

**Three tests will fail on golden values, not on logic**:

- `BoxPathTests.cs:278` asserts `MatterInitialTotal == 6000d` exactly.
- `BoxPathTests.cs:294` asserts `AuditResidual == 2.07525026780786E-05d` exactly (a
  400-step replay fingerprint).
- `CalibrationSweep.cs:243` and several others assert `|AuditResidual| / EnergyIn < 1e-4`
  or `1e-6`; these survive the rebuild if the audit still closes.

**Two reflection guards fire automatically** on any `RunConfig` change: `RunConfigTests`
(every tunable reaches `Hash()`; the type list at 72-77 covers `RandomGenomeOptions`,
`DevelopmentLimits`, `MutationRates`, `FluidConfig`, `LightModel`, `CurrentField`) and
`RunConfigJsonTests` (every tunable survives save/reload).

---

## 5. `LedgerForecast.cs` structure

`src/Evosim.Core/Ecosystem/LedgerForecast.cs`, 364 lines, a pure-Core R0 calculator with
no world and no field. Two public types: `LedgerForecast` (static) and
`LedgerForecastResult` (readonly struct, 306-363).

`Forecast(phenotype, config, irradiance, nutrientDensity, shadeFraction, reproduction)` at
**109-233**:

| Lines | Leg |
|---|---|
| 117-165 | argument validation, including `BroodSize ≥ 1` and `BirthInvestment > 0` mirroring `Genome.Validate` |
| 167-168 | `irradiance × (1 − shade)`; `tissue = Metabolism.TissueJoules` |
| 173-175 | **net watts at birth**: one `Metabolism.StepAt` at 1 s, age 0, no work |
| 177 / 235-276 | **break-even density**: `FindBreakEvenDensity`, bisection on `StepAt(...).Net`, `null` for a body with no absorptive part |
| 184-187 | **child price in joules**: `share = BirthInvestment × tissue / BroodSize`; `newbornReserve = share × NewbornReserveFraction`; `childBody = min(share − reserve, tissue)`; `childPrice = childBody + reserve + PerOffspringOverheadJoules` |
| **188** | **matter enters here, and only here**: `matterPricePerChild = MatterPerTissueJoule × childBody + MatterPerCreature`. It is **reported, never enforced**: the lifetime loop never checks it, so R0 is an energy-only forecast with a matter figure hung beside it |
| 189 | `reproductionGate = reproduction.CostJoules(tissue, overhead)` |
| 191-228 | **lifetime loop**: 0.5 s (`StepSeconds`) steps to `MaxLifetimeSeconds` (60,000), `energy += Net`, senescence via `ageSeconds`, breeding when `energy ≥ gate`, the brood drained at `childPrice` each |
| 230-232 | result |

`LedgerForecastResult` fields: `NetWattsAtBirth`, `BreakEvenNutrientDensity` (nullable),
`LifetimeSeconds`, `ChildrenProduced`, `TimeToFirstChildSeconds` (nullable),
**`MatterPricePerChild`** (331-335, the doc comment names both tunables),
`DiedOfStarvation`.

Front ends: `src/Evosim.Ledger/Program.cs` (`Metabolism.TissueJoules` at 135; the table
header at 167-168 prints `| depth (m) | density (J/m3) | net W at birth | break-even (J/m3) |
lifetime (s) | R0 | first child (s) | matter/child |`; `MatterPricePerChild` emitted at 191)
and `scripts/ledger.ps1` (119 lines, a thin wrapper that parses **nothing**; it passes
`--genome` and `--config` paths through at 109-110, so it breaks only through
`GenomeJson`/`RunConfigJson` refusing the old formats).

---

## 6. Genome: the path `ReserveMargin` must follow

`ReserveMargin` does not exist anywhere yet (only two prose mentions: `DECISIONS.md:5384`,
`HANDOFF.md:19`).

| Step | File:line | What `BirthInvestment`/`BroodSize` do, for `ReserveMargin` to copy |
|---|---|---|
| **Definition** | `Genome/ReproductionTraits.cs:29-89` | a plain mutable struct: `BroodSize` (32), `BirthInvestment` (56), `CostJoules(...)` (82-83), `Clone()` (85), `ToString()` (87-88) |
| **Held on the genome** | `Genome/Genome.cs:36-37` | `Reproduction` property, default `{ BroodSize = 1, BirthInvestment = 0.5f }`; copied in `Clone()` at 69 |
| **Validated** | `Genome.cs:111-128` | `BroodSize < 1` and `BirthInvestment` non-finite/≤0 both append to `issues` |
| **Founder draw** | `Genome/GenomeFactory.cs:382-387` and **again at 477-483** | two construction sites (founder and a second factory path); both must be updated |
| **Founder range tunables** | `GenomeFactory.cs:241-265` | `MinBroodSize`/`MaxBroodSize`, `MinBirthInvestment` (0.25) / `MaxBirthInvestment` (1.0) on `RandomGenomeOptions` |
| **Mutated** | `Mutation/Mutator.cs:116-136` | `MutateReproduction`: `rng.Chance(rates.BroodSizeChance)` then `rng.Chance(rates.InvestmentChance)` → `Step(...)`; `g.Reproduction = r` at 131 |
| **Mutation rates** | `Mutation/MutationRates.cs:179, 195, 216, 226` | `BroodSizeChance` 0.05, `InvestmentChance` 0.08, `AdultScaleChance` 0.08, `MaxBroodSize` 64 |
| **Serialised** | `Serialization/GenomeJson.cs:86-89` (write) and `122-126` (read) | a `reproduction` sub-object with `brood` and `investment` |
| **Format gate** | `GenomeJson.cs:61` `FormatVersion = 5`; `107-116` | read refuses any other version with a prose message naming what changed. **The format-5 bump pattern**: bump the constant, rewrite the message's parenthetical, and accept that every stored genome is refused with no migration path |
| **Species distance** | `Genome/SpeciesDistance.cs:259-261` | `RelativeDiff` over brood, investment and adult scale; a new gene belongs here or speciation silently ignores it |
| **Lineage row** | `Ecosystem/LineageEvent.cs:200-201` | `bf` and `as`; `World.cs:3500-3503` passes them |
| **Population means** | `World.cs:589-603` | `MeanAdultScale`, `MeanBirthInvestment`, `MeanBroodSize`, `MeanBodyFraction` via `MeanOverLiving` |
| **Report/stats** | `EvolutionRun.cs:2879-2882` (fields), `3286-3330` (`adult scale`, `invest`, `brood`, `body frac` columns), `3139-3142` (row values) | the four-column block a fifth joins |
| **Launch knobs** | `EvolutionRun.cs:501-508, 755-756` | `EVOSIM_INVEST_MIN/MAX`, `EVOSIM_INVEST_CHANCE`, `EVOSIM_ADULT_SCALE_CHANCE` |
| **Ledger** | `LedgerForecast.cs:150-165` | duplicated validation of the traits |
| **Tests** | `ReproductionTraitsTests.cs` (2), `GenomeJsonTests.cs:143-146, 221-227`, `MutationTests.cs`, `SnapshotJoinTests.cs:203`, `SnapshotReadbackTests.cs:106-172` | the last two skip rows whose format ≠ current, so they keep passing but stop testing anything on the day of the bump |

**Collateral of a format-6 bump:** every genome under `inocula/` is refused. Current
formats: `d056-s5-absorptive.json` 3, `inoculum-r13a-s2-t17000.json` 3, `s4-mixo-2node.json`
3, `s4-stomach-1/2/3.json` 3, `r28s4-stomach.json` 4, `r28s4-stomach2.json` 4,
`growth-ledger-genome.json` **5** (the only one this build reads, and it goes too). Every
`snapshots/*.jsonl` on disk likewise, which kills the theatre's Mode A on every recorded
run.

---

## 7. What else breaks silently

### 7a. Theatre (`unity/Assets/Theatre/`)

| File:line | What |
|---|---|
| `TheatreReplay.cs:29-48` | census fields `AuditResidual`, `AuditPercent`, `MatterHere`, `MatterStanding`, `MatterResidual` |
| `TheatreReplay.cs:296-303` | recomputes the matter identity inline, **a second copy of `EvolutionRun.cs:2513`'s arithmetic** |
| `TheatreReplay.cs:382-385` | the identity check compares `recorded.AuditResidual != Census.AuditResidual` **exactly**. Any change to what the audit counts makes every recorded run refuse to replay |
| `RunRecord.cs:26-43, 280-295` | reads `auditResidual` and `matterResidual` out of `stats.jsonl`; `MatterResidualRecorded` is the "column absent" flag for older runs; the rebuild needs the same pattern for any renamed field |
| `UI/Resources/TheatreUiDocument.uxml:89-107` | literal labels **`audit`**, **`matter residual`**, **`matter here`**, with element ids `value-audit`, `value-matter-residual`, `value-matter-here` |
| `UI/TheatreUi.cs:94-96` | `MatterResidualFloor = 1e-3`, `MatterResidualRelative = 1e-6`, the thresholds that turn the panel vermilion |
| `UI/TheatreUi.cs:699, 1003-1013` | formats the residual; the alarm string **`"MATTER RESIDUAL "`** and **`" ENERGY LEDGER DOES NOT BALANCE"`** (1012) |
| `UI/TheatreUi.cs:401, 2028-2031` | the solo inspector's `reserve` row, in seconds |
| `Editor/TheatreUiCheck.cs:528` | asserts `value-matter-residual` against `census.MatterResidual` to 0.002 |
| `README.md:353` | describes "the energy audit and the matter residual, that goes vermilion if either opens" |

### 7b. Scripts

| File:line | Depends on |
|---|---|
| `scripts/ledger.ps1` | nothing by name; passes paths to `Evosim.Ledger`. Breaks only via the format gates |
| `scripts/clade-score.ps1:486, 598-607` | report **column names** `inherit`, `photo`, `photo inh` looked up by header (throws `no 'inherit' column` if renamed); lineage `abs`/`pho` flags; `config.json`'s `floorClosesAfterSeconds` |
| `scripts/analyse-arm.ps1` | header-name → index map (`-ListColumns`); any renamed column silently prints `?` in a `-Timeline` cell rather than refusing |
| `scripts/reads/matter-budget.py:20-35` | `matterStanding`, `matterLocked` (falls back to `matterInBodies`), `matterHere`, `conceptionsBlockedByMatter`, `matterInfluxWindow`, `matterBuriedWindow`, `matterInfluxedTotal`, `matterBuriedTotal` |
| `scripts/reads/matter-profile.py:16` | `matterHere`, `matterSurface`, `matterDeep`, `matterStanding`, `conceptionsBlockedByMatter` |
| `scripts/reads/late-balance.py:12-15` | `matterStanding` slope |
| `scripts/reads/r32-read.py:276-287` | `corpseJoules`, `detritusJoules` (M4's threshold) |
| `scripts/reads/r33-read.py:364-369` | `growthShortOfMatter`, `conceptionsUnderMassFloor` |
| `scripts/reads/rebuild-report.py:15-35` | `conceptionsBlockedByMatter`, `detritusJoules`, `detritusHere`, `detritusOnFloor`, `detritusDeep`, `auditResidual`, `spendJoules`, `matterLocked`, `refugeJoules`, `excretedWindow`; this one **rebuilds the markdown table from `stats.jsonl`**, so it must move column for column with `BaseColumns` |
| `scripts/reads/score-r24/25/26/28.py` | `matterStanding`, `matterHere`, `matterDeep` |
| `scripts/compare-det.py`, `scripts/digest-diff.py`, `scripts/positions-read.py` | field-agnostic (positions and digests); unaffected |

### 7c. `DESIGN.md`: sections to rewrite

| Section | Lines | What it asserts |
|---|---|---|
| **§5A.2 The energy economy** | 1252-1293 | "Sunlight is the only primary input… total world energy is auditable: sun in, metabolism out, everything else conserved." Survives in spirit; the standing accounts change |
| **§5A.2b Competition for light** | 1294-1374 | carrying capacity from watts arriving; the saturating uptake changes the ceiling's derivation |
| **§5A.2c Bodies cost, corpses feed** | 1375-1503 | the tissue-value loop: a body costs joules to build and is worth joules dead. Becomes organic matter |
| **§5A.2d Matter, what the producer consumes** | 1504-1574 | **the two-currency rule in full**: the source/sink table at 1520-1523, the conception draw (1526), "a matter-starved world stops them breeding" (1531), death returns it (1534), "`World.Matter` is deliberately outside `StandingJoules`" (1539-1542), the ⚠ on matter returning only at death (1544-1552), the measured stripping table (1553-1560), the ⚠ on unmeasured knobs (1570). **The whole section is superseded** |
| **§5A.1 Cell types** | 1124-1251 | the per-type table, `AbsorptiveCell.Yield` "fraction of captured matter kept", the §5A.2d reference at 1146, buoyancy earning nothing (1180) |
| **§5A.3 Feeding** | 1575-1606 | yields per target type; becomes assimilation efficiency |
| **§5A.6 Reproduction and death** | 1690-1786 | death at zero energy, tissue returns to the pool, the corpse leg (1692-1695), reproduction paid from the parent's energy |
| **§5A.6c Senescence** | 1860-1894 | wear on income and cost |
| **§5A.6d Margin, not break-even** | 1895-1921 | the R0 framing the ledger implements |
| **§5A.10 Open parameters** | 2017-2178 | the knob table: **Earning** (2076-2088), **Spending** (2089-2094), **Moving between accounts** (2095-2103, including `MatterPerCreature` at 2103). Every row that names a joule or a matter unit |
| **§9 Persistence** | 2398-2432 | the genome-format rule; lines 2426-2430 narrate the 4→5 bumps and must gain 5→6 |
| **§0h Changelog (D065)** | 162-170 | introduces `MatterPerCreature`; marked superseded, not deleted |
| **§0u Changelog (D087)** | 331-346 | the growth/format-5 entry; **this is the template for the D098 changelog**: mechanism, the Core symbols touched, the format bump, the columns added, the date it was smoked |
| §0p, §0t, §0v, §0w | 396, 267, 287, 302 | the recent changelog entries a D098 entry sits beside |

§4.1 (Morphology graph, line 525) says nothing about reproduction traits; the genome-level
dials are specified in §5A.6 and §0u, not §4.1.

### 7d. Other prose

- **`primer/07-the-world-keeps-two-books.md`**: the entire essay is the two-currency rule,
  its title included. It asserts at 18-21 "the second is matter… a body is built from it at
  conception and returns it at death", at 79-91 that "matter is not energy, and a book that
  counts one substance cannot notice the other", and at 129 points at `World.cs` for "both
  identities". The primer is explicitly *never a source of truth* and *allowed to be out of
  date* (CLAUDE.md), so it does not block the build, but it is the most conspicuously wrong
  document the rebuild creates, and its sources table at 129-132 cites D074 and D083.
- `primer/06-the-producers-feed-the-water.md`: 18 mentions of matter; the producer's
  stripping story.
- `primer/08-the-water-as-vertices.md:81`: "a child costs 8 to 16 units of matter",
  superseded.
- `README.md:24, 28`: "a matter currency", "the matter ratchet" in the elevator
  description; `README.md:353` the theatre's two residuals.
- **`fable-propose-predation.md:76`**: a *live* proposal that prices a bite as a
  "`MatterPerTissueJoule` share out of the bitten creature's locked matter". D098 rules
  eating as an organic-matter transfer, so this proposal's mechanism needs rewriting before
  it is put to the owner.
- `logbook/0033`, `0041`, `0049`, `0050`, `logbook/specs/d087-entry.md`,
  `logbook/specs/growth-core-spec.md` name the tunables. The logbook is append-only history
  and stays as recorded.

### 7e. Two quieter traps

- **`CellTypeJson`** (`Serialization/CellTypeJson.cs:72-73, 95`) writes and reads
  `upkeepWattsPerCubicMetre` and `tissueEnergyPerCubicMetre` per registered type, and
  `CellType.FullHashContribution()` (`CellType.cs:255-258`) folds the tissue value into the
  config hash. A `JoulesPerUnit` living on the cell type would go through the same three
  places; one on `RunConfig` would go through `[Tunable]` and nothing else.
- **The sensors read joules by name.** `RunConfig.ChemicalHalfScaleJoulesPerCubicMetre`
  (1808) scales the Chemical channel against the detritus field's density, and
  `EnergyFullScaleSeconds` (1821) scales the Energy channel against
  `Organism.SecondsOfReserve` (`CreatureSensors.cs:141-142, 183, 252-265`). Both squash
  against a unit that is about to change magnitude, so a brain trained under the old scales
  reads a different world under the new ones even with the same genome; worth a line in the
  spec, since it is invisible in every column.

---

## 8. Supplement: a second sweep of scripts, theatre and prose

Delivered after the first hand-back; additional to the sections above, with corrections
to §7b and §7c where it says so.

### 8a. Three findings that change the build order

1. **Every stored `config.json` stops loading the moment the knob set changes**, and that
   breaks things before any column name matters. `RunConfigJson` requires a value for every
   declared `[Tunable]` (DESIGN.md:2043, "the reader iterates it and requires a value for
   each"). Consequences: `Evosim.Ledger` and `ledger.ps1` fail with a load error; the
   theatre's Mode B refuses **every** recorded run (`RunRecord.cs:52-58` loads the run's
   config through `RunDirectory.ReadConfig` as "the whole launch sequence", before any
   census string is reached); and the three tracked configs under `inocula/` stop loading
   (`spike-reference-config.json:165,167,173,174`; `s4-config-patched.json:161,163,166,167`;
   `growth-ledger-config.json:193,195,201,202`, each carrying `excretionPerJoule`,
   `initialMatterPerCubicMetre`, `matterPerCreature`, `matterPerTissueJoule`).
2. **`scripts/analyse-arm.ps1` returns `?` for a missing column and does not refuse**
   (:48-53). It maps by header name at :32-33, depending on `alive`, `births`, `absorpt`,
   `inherit`, `det deep`, `mat top`, `mat blk`, `refuge J`, `floor`, `t (s)` (:36, :84). A
   renamed economy column produces a status line that looks healthy and says nothing.
3. **`scripts/absorptive-log.ps1:86-94` parses `absorptive.jsonl` with one anchored regex
   over every key in order**: `t`, `id`, `age`, `gen`, `patch`, `y`, `volume`, `absVolume`,
   `photoArea`, `parts`, `mixotroph`, `energy`, `tissue`, `investment`, `densityHere`,
   `share`, `foodW`, `lightW`, `upkeepW`, `exudedW`, `netW`, `children`, `lastChildT`,
   `dead`. Rename any one and every row lands in the malformed counter and the summary is
   empty (documented at :80-85). Output columns `mean J/m3` and `min E J` (:217, :227) are
   unit strings. The schema is locked by the fixture
   `scripts/tests/absorptive-log/fixtures/fx-schema/2026-01-01-000000-fixture/absorptive.jsonl`;
   the guard `scripts/tests/absorptive-log/run-tests.ps1:56-61` fails on one malformed line,
   so the fixture must be regenerated.

### 8b. Scripts, additions to §7b

- `scripts/ledger.ps1:98-118` parses nothing. It resolves a `dotnet.exe` and runs
  `dotnet run --project src/Evosim.Ledger -- @toolArgs`; args forwarded at :108-116
  (`--genome --config --clearance --depth --density --shade --compare`), all mandatory
  except `-Shade` (:26-59). Its doc comment becomes wrong prose: :3 "per-creature
  energy-ledger forecast", :7-10 "break-even nutrient density", :45 "-Density … J/m3".
- `scripts/read-arm.ps1:133,140,226-241`: `det deep`, printed with the literal unit
  ` J/m3` and thresholds ≥ 2 / 4 / 8 J/m³.
- `scripts/reads/rebuild-report.py:14-39`: a flat positional list of about 48 stats keys
  that rebuilds `runs/<arm>.md` from `stats.jsonl` (`conceptionsBlockedByMatter`,
  `foodJoules`, `lightJoules`, `workJoulesPerSecond`, `workJoules`, `spendJoules`,
  `detritusJoules`, `detritusHere`, `detritusOnFloor`, `detritusDeep`, `matterSurface`,
  `matterDeep`, `auditResidual`, `edibleDetritusHere`, `matterLocked`, `refugeJoules`,
  `excretedWindow`, `detritusPatchSd`, `patchMaxShare`, `detritusDepositedWindow`,
  `detritusTakenWindow`, `detritusExudedWindow`, plus the non-economy set). Also reads
  `config.json`'s `configHash` (:72). One rename is a `KeyError`; one removal and
  replacement silently writes a differently shaped table.
- `scripts/reads/bids.py:8-33` discovers stats keys by pattern (`'nerg' in k.lower() or
  k.lower().endswith('joules')`, :8). A rename away from the `*Joules` suffix makes it print
  nothing. Per-row `energy`, `endowment`, `tissue` (:29-30); `endowment` is already a stale
  pre-D087 name.
- `scripts/reads/r29-read.py:92`: `detritus J`, `absorpt`, `inherit`, `depth m`.
- `scripts/reads/r30-read.py:108-137`: `detritus J`, `mat resid`, `mat blk`, `audit`,
  mat-short.
- `scripts/reads/r31-read.py:104-173`, `r31q-read.py:64-73`, `r31-results.py:18,69`:
  `J/m3 here`, `detritus J`, `det deep`, `mat resid`, `food rig`, `% on floor`, `audit`.
- `logbook/specs/r38-read/analyse.py:466-493` + `fields.tsv:2`, `boom-fields.tsv:3`:
  `matterStanding`, `corpseJoules`, `corpseMatter`; columns `det cv`, `mat cv`,
  `detritus J`, `refuge J`, `mat here`, `det deep`, `J/m3 here`.
- `logbook/specs/r39-read/analyse.py:561-572` + `matter.tsv:2`, `summary.tsv:29,41`:
  `matterStanding`, `matterResidual`, `matterLocked`, `corpseMatter`,
  `conceptionsBlockedByMatter`, `conceptionsShortOfMatter`, `growthShortOfMatter`,
  `matterCv`, `matterOrphaned`.

**Corrections to §7b.** `clade-score.ps1` touches no joule or matter field: it parses
`lineage.jsonl` by an anchored regex (:175), finds `"abs":` and `"pho":` by `IndexOf`
(:186-193), and reads `config.json` for `floorClosesAfterSeconds` only (:436). Its real
exposure is the guild flags: if the rebuild retires the absorptive/photosynthetic split,
every clade reading silently reports zero clades (the report columns `inherit`, `photo`,
`photo inh` at :486, :598-607 still hold). `compare-det.py` compares generically (:113) but
exits 2, not 1, whenever a pre- and post-rebuild run are compared ("a field is present on
one side of a shared sample and not the other", :20-22): the replay gate reports a usage
problem rather than a divergence. `positions-read.py` reads only `worldAreaSquareMetres`,
`worldDepthMetres`, `worldShape` (:147-159), unaffected. No script sets `EVOSIM_MATTER*` or
`EVOSIM_EXCRETION`; those env knobs are read only in `EvolutionRun.cs:243,675-685`, so
retiring them touches no launcher.

### 8c. Theatre, additions to §7a

- `UI/Resources/TheatreUiDocument.uxml:87` section title `INVARIANTS` over the audit and
  matter rows; :93 the unit label `%` on the audit row; the two matter rows have no unit
  label.
- `UI/TheatreUi.cs:84` `AuditPercentTolerance = 1e-4`; the doc at :73-83 states the plate
  shows "the two identities and nothing else".
- `UI/TheatreUi.cs:89-93`: the doc comment on `MatterResidualFloor` hardcodes the
  assumption that "a closed world holds six thousand units" and that `mat resid` prints to
  three decimals. A change of matter magnitude silently retunes the alarm.
- `UI/TheatreUi.cs:1011-1014`: both alarm strings in full, `"AUDIT " + … + " % " + EmDash +
  " ENERGY LEDGER DOES NOT BALANCE"` and `"MATTER RESIDUAL " + … + EmDash + " MATTER IS NOT
  CONSERVED"`.
- `UI/TheatreUi.cs:2013-2079`: the Mode A solo census carries no energy or matter row at
  all (parts, dof, neurons, speed, travelled, depth, joint rate, reserve in s, driven by,
  smell field, then sensors). Nothing to rewrite there.
- `UI/TheatreUi.cs:1061-1093`: `Warnings()` covers only config-hash mismatch, step
  disagreement, the samples note and running past the record. There is no warning path for
  `RunSample.MatterResidualRecorded == false`, so a run with no matter column displays
  0.000 indistinguishably from a conserved one.
- `RunRecord.cs:298-301`: a row missing any of the non-optional keys (`t`, `alive`,
  `births`, `deaths`, `meanHeight`, plus `auditResidual` at :288) is counted malformed; the
  theatre then reports "N stats row(s) unreadable and skipped" rather than a schema change.
- `TheatreReplay.cs:33-44`: the `WorldCensus` doc asserts in prose "It must read 0, like
  the audit" and "the energy audit does not see matter".
- `Editor/TheatreUiCheck.cs:525-526`: two more assertions beside the one in §7a, `census:
  audit` against `value-audit` at 1e-4, and `census: matter here` against
  `value-matter-here` at 0.01.

### 8d. `src/Evosim.Ledger/Program.cs`, the full surface

:11-15 the class doc cites §5A.2 and §5A.6 by name. :29-36 cites §9's
refuse-rather-than-default. :57-61 `GenomeJson.Read` (format-gated) and
`RunConfigJson.Read(text, out hashMismatch)`, prints `warning: {hashMismatch}`. :98-106
throws "The config at '…' has no '…' cell type registered, so a clearance sweep has nothing
to vary." :106, :235-257 casts to `AbsorptiveCell` and rebuilds it from `ClearanceRate`
(swept), `UpkeepWattsPerCubicMetre`, `Yield`, `TissueEnergyPerCubicMetre`. :115-124 mutates
`config.CellTypes` in place per clearance, restores at :124. :145-158 body summary literals
"- Tissue: … J", "- Standing cost: … W". :167-169 the table header (load-bearing). :196-198
the censored-lifetime footnote naming `LedgerForecast.MaxLifetimeSeconds`. :208-228
`--compare` swaps Absorptive and Photosynthetic on every `MorphNode.CellTypeId`. :339-366
argument validation messages naming densities and depths.

### 8e. `DESIGN.md`, line-precise, sharpening §7c

The sentence the whole rebuild deletes is DESIGN.md:1519: "Light is energy; matter is
matter. They are separate currencies and are never added."

Other load-bearing lines: 1521-1524 the currency source/sink table; 1536-1538
"`World.Matter` is deliberately outside `StandingJoules`… would let the books balance by
counting a different substance"; 1389-1392 `TissueEnergyPerCubicMetre` "what a body is
worth, and it is therefore what a body costs"; 1439-1443 the audit as an equality,
"measured residual 0.0000%"; 1461-1465 D051's two remineralisation rates; 1484-1502 the
detritus-flux identity and `ExudationFraction`; 1401-1423 the three field representations
and `CorpseDecayPerSecond` (1421-1422); 1577 §5A.3 "A Consumer part gains energy on contact
with tissue" (D098 makes it a matter transfer); 1720-1726 and 1737-1740 the price formula
`i × tissue + n × overhead`, energy only, overhead not evolvable; 1742-1751 growth "moves
reserve into tissue and matter into the body"; 1782-1785 "In a closed-matter world neither
[ceiling] can fire"; 2036-2066 the §5A.10 sub-heading "One declaration, four consumers"
with :2043's requires-a-value rule (D027), the paragraph explaining finding 1 above; 2070
"Nothing that costs or earns energy may be a constant in a class."

Changelog sections beyond the four named in §7c, each stating an economy rule and needing
a superseded marker rather than deletion: §0f (145-153, D051), §0i (171-182, D066
conservative advection of both fields), §0j (183-197, the vent), §0k (198-219, D070
`ExudationFraction`), §0m/0n (234-247; :244 records that "§5A's currency table still gave
matter no sink, which D074's burial falsified", so the table has been corrected before, not
retired), §0s (248-266, D083 two stocks and `FieldMatterKernelMetres`), §0t (267-286, D086
two cell sizes, "conception draws from the parent's matter cell"), §0w (302-330,
`MatterBudgetUnits`), §0p (396-412, stillbirth charged no matter), §0q (368-395; :393 is
where the `mat here` column is specified).

**Two corrections to §7c.** §4.1 Morphology graph is lines 525-571 and its code block
(:539-557) declares only `MorphNode` and `MorphEdge`: no reproduction traits, no format
version, no cell type. The dials are specified in §0u:333-335 and §5A.6:1715-1751;
`AdultScale`'s development semantics in §4.2:620; their mutation in §4.5:806.
`ReserveMargin`'s prose home is §5A.6 plus a new §0x changelog, not §4.1. §9 is lines
2398-2431, and the format-bump narration to extend is :2426-2430 (4 → 5 → 6, each "refused
rather than read", ending "This stops the theatre's Mode A on every run recorded before
it").

### 8f. Prose beyond DESIGN.md, additions to §7d

CLAUDE.md itself has to change: 661-666 the whole `mat resid` gotcha ("must read 0 like
`audit`… the energy audit does not see matter… 22,000 units created in 3,000 s with audit
at 0.0000% on every row"; :666 tells a reader to check `matterStanding` against seeded
stock plus influx less burial); 215 the theatre's identity check comparing `alive`,
`births`, `deaths`, `auditResidual`, `meanHeight`; 258 "the audit and the matter residual"
in the UI Toolkit paragraph; 423, 547, 897 three more audit references (the physics-leak
lesson; "0.05 is out… the audit opens"; "the discard is booked as an outflow").

Also: HANDOFF.md:122 (`matterStanding` exact, as an oscillation criterion) and :452 ("Both
audits kept"); `logbook/specs/sol-gpt-gap-map.md:160` (row R3-28 carries the predation
proposal's `MatterPerTissueJoule` rule, so the gap map needs the same edit as
`fable-propose-predation.md`); `logbook/specs/checkpoint-spec.md:98`,
`theatre-build-report.md:65`, `theatre-survey.md:117` (all cite `auditResidual` as a
round-trip or bit-identity instrument); logbook/0063:44, 0101:80, 0106:64 (history, no
edit needed).

DECISIONS.md entries superseded, with anchors: D052 at 2362ff (symbols 2380, 2383, 2413)
the excretion contract; D065 at 3039ff (3056, 3058, 3064, 3069, 3094) the fixed charge;
D087 at 4461ff (4515) growth's matter draw; D097 at 5303ff (5326) names `MatterPerCreature`'s
3 units as the knob; D098 at 5344ff (`JoulesPerUnit` at 5370).

In-tree test line anchors for the retiring knobs (for the rewrite pass): `WorldTests.cs`
(655, 746, 778-779, 804, 815-816, 833-835, 846, 852-854, 870-871, 884, 897, 901, 940, 953,
1074, 1082, 1095, 1112, 1147, 1164, 1198, 1234, 1260, 1290); `GrowthTests.cs` (472-474,
509, 560, 584, 650, 804, 808); `OpenMatterBudgetTests.cs` (322, 523);
`ConceptionOrderTests.cs` (352, 386-387); `AllocationTests.cs` (121-122);
`DivergenceTests.cs` (39, 46); `SharedSpaceTests.cs` (271-272); `RollCellTests.cs` (463);
`BoxPathTests.cs` (266).

### 8g. Ranked silent breakages

1. Every stored `config.json` stops loading: kills the ledger, the theatre's Mode B on all
   recorded runs, the three tracked inocula configs.
2. `analyse-arm.ps1` prints `?` for a renamed column and looks healthy.
3. `absorptive-log.ps1`'s anchored regex makes every row malformed on any key rename; its
   fixture must be regenerated.
4. `RunRecord.cs:280-296`: a renamed `auditResidual` reads as "N stats rows unreadable",
   not as a schema change.
5. `TheatreUi.cs:392-394`'s element-id strings against the UXML: a renamed row leaves the
   label handle null; `TheatreUiCheck.cs:525-528` is the only guard and uses the same
   strings.
6. `TheatreUi.MatterResidualFloor = 1e-3` silently retunes when the matter magnitude
   changes.
7. `rebuild-report.py`'s flat positional key list: `KeyError` on a rename, a differently
   shaped table on a replacement.
8. `compare-det.py` exits 2 rather than 1 across the rebuild boundary.

One process note: the matter identity is computed by hand in **four** places, not three:
`EvolutionRun.cs:2513`, `TheatreReplay.cs:301`, `logbook/specs/r39-read/analyse.py`, and
inline in about six Core tests. A `World.MatterResidual` property would collapse all four.

---

## The agent's notes for the spec

Read from the sweep, the evening of 2026-09-18, before the owner's alignment answers were
settled; each is a design implication, not a ruling.

- **`ReserveMargin` has a home.** The breeding gate at `World.cs:2904-2906` is already
  `tissue + reserve + overhead` against the parent's reserve; the gene multiplies the
  reserve term, and the lineage row's `bf`/`as` pair takes a third flag. The species
  distance at `SpeciesDistance.cs:259-261` must include it or speciation ignores it.
- **The matter identity becomes a Core property** (`World.MatterResidual`), and the
  theatre's and the harness's inline copies read it, so that one arithmetic serves three
  readers.
- **The identity check will refuse every recorded run anyway**: the tunables change, so
  every `config.json` on disk is refused before the audit's exact comparison is reached.
  The comparison itself stays exact; the rebuild does not loosen it.
- **`MatterPricePerChild` in the ledger was never enforced.** The rebuilt ledger has no
  matter price to report; the reserve a child takes is the price, in one unit.
- **The sensors' scales** (chemical half-scale in J/m³, energy full-scale in seconds) are
  re-expressed in the new unit in the spec, and the spec says that a brain reads a
  different world under them even with the same genome.
- **The predation proposal** (`fable-propose-predation.md`) is rewritten on the new base
  before it goes to the owner, as D097 already says.
