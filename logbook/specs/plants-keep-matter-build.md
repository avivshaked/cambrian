# A plant keeps its matter: the build plan (D206)

*2026-10-19, the speed agent, on r62-base. Written as the plan before the build and brought up to what was built. It
says what changes where, and records the coordinator's two choices of the same night.*

## The rule in the code's terms

Today a body is charged matter and nothing else. Its reserve, tissue and gestation account are joules, and a joule
is a charged unit over `JoulesPerUnit`. The metabolic step burns `min(Expenditure, burnable)` joules. Those leave as
heat, and the same over ρ goes into the spent field where the body is (`World.cs`, "D098's leg 2"). Fixation takes
`LightIncome / ρ` spent units from the water (`Matter.Take`, "leg 1").

D206 gives a body a second account, the spent units it holds, `Organism.SpentUnits`, a double. Three lines of the
step change:

1. **The burn is split by what the body is.** A body with no eating tissue (no mouth, and no consumer or absorptive
   cell) is a plant. It holds its whole burn, the brain and the joints included, and releases nothing. A body with
   eating tissue holds the share of its whole burn equal to its photosynthetic cells' upkeep over all its cells'
   upkeep, and releases the rest to the water as now. `Metabolism.HeldShare` reads the share off the body. Every
   upkeep term is a rate over the part's static shape, and wear and repair scale them all alike, so the share does
   not change with age. A body that cannot pay in full burns less, and the split keeps the same fraction. Every other burn a
   living body makes splits the same way, through `World.ReturnSpent`: the grips' charge (D162), a birth's overhead
   and healing. A death's tissue is not a burn of the living. It goes to the water or the corpse as now.
2. **Fixation draws on the body first.** The body's own units are charged up to what the light offers
   (`LightCapacity`). The water's uptake does not bound them, because they are already in the body. The water then
   gives at most the bill's uptake from it, within the capacity the body's own units leave. That is all the body
   demands of the water, and the share and the partial-take rule apply to it as today. The ledger is rebuilt with
   the whole fixation, and exudation is priced on it as the bill prices it (`EnergyLedger.WithFixation`).
3. **Death.** The held units go to the corpse, which releases them into the spent field with its decay instalments.

Exudation (D070) needs no new code. It already leaves charged, into the charged field, and becomes spent only through
the bacteria's decay. Its fraction is the existing `ExudationFraction`, set by a screen.

## Where else it reaches

- **The matter books.** `StandingMatterUnits` reads "the spent stock plus the standing joules over ρ, and there is no
  third place for a unit to hide". D206 makes the third place. It adds Σ `SpentUnits` over the living and the
  corpses (`World.HeldUnits`). `MatterResidual` reads this sum. With the rule on, the stats row gains four fields:
  `heldUnits`, and the three counters below. The table is unchanged.
- **The counters.** `BurntTotal` stays as the units that reached the water. `HeldTotal` counts the units kept,
  `RechargedTotal` the units fixation took back from a body, and `HeldReleasedTotal` the units a corpse, or a death
  with no corpse, let go. The store's books are `HeldUnits = HeldTotal − RechargedTotal − HeldReleasedTotal`.
- **Birth.** A child is made of charged matter from its parent's reserve, as now, and starts with no spent units.
  The parent keeps its own, and they recharge from light like any others.
- **A bite.** A grazer takes charged tissue. The bitten plant's spent units stay with it. They are not food.
- **`LedgerForecast` is unchanged.** Its energy is the same, since the joules still leave as heat. It prices
  fixation at the water's density and has no matter field, so it cannot see a body's own units. For a plant in
  water with spent matter it reads the same. For a plant in empty water it under-reads the fixation that the plant's
  own units allow.
- **The checkpoint.** `Organism.SpentUnits`, `Corpse.SpentUnits` and the three counters are state.
  `World.StateVersion` goes from 18 to 19. Version 18 is still read, as `PreHeldStateVersion`, as a world in which
  nothing was held, which is exact, since no rule could fill the store. So round 61's checkpoints still resume.
  Writing 18 with the rule on is refused. The harness's half did not move. It reads its card block from
  `World.CardPairStateVersion` (18), and no longer from the current version. The fixtures are re-recorded once,
  after this build.
- **The card needs nothing.** It steps bodies and the water's transport. Metabolism, fixation, the burn and burial
  are all host-side `World` code at the metabolic step, so the store is a host field and costs the kernel nothing.

## The tunable

D206 is a world rule, so it lands behind a tunable at its off default, and every world on file replays:
`PlantsKeepMatter` (bool, group feeding, env `EVOSIM_PLANTS_KEEP_MATTER`, header `· plants keep matter`). The
round-62 config turns it on. Off, no body holds anything, and every line of the step is the recorded arithmetic.

## The coordinator's choices

- **How the held units reach the water at death.** Through the corpse, released with its decay instalments, as D206
  says. A corpse gains `Corpse.SpentUnits`, and its checkpoint row changes with it. When a corpse is eaten, its held
  units stay with what is left of it, and an emptied corpse releases what it still holds where it lies.
- **What a body holds.** The coordinator chose a third reading over the two this plan offered, after the owner's
  point that a plant does not defecate and only eating makes waste. It is rule 1 above. A pure leaf then releases
  nothing. That a plant holds its brain's and joints' burn too goes past D206's "other tissue burns as now", which
  named only tissue. It is the coordinator's reading, for the owner to confirm.

## Tests

- Off: the recorded step, the same bits (the existing ledger and ecosystem tests).
- On, a lone plant in the dark: its burn goes to `SpentUnits`, the water's spent stock is unchanged, and the world's
  matter is conserved to the bit.
- On, in light: fixation drains `SpentUnits` before the water, and the water gives only the excess.
- On, a body with no eating tissue: its whole burn is held, brain and joints included.
- On, a body with a mouth and leaves: the photosynthetic cells' share of the cell upkeep is held, and the rest
  reaches the water.
- On, death: the held units pass to the corpse, leave with its instalments, and the books close.
- The tunable's two reflection tests, the checkpoint round trip at version 19, and the refusal at version 18.

## How the suites were run

Every suite on this build ran pinned to logical processors 16 to 19 at BelowNormal, the speed agent's standing terms.
Pinned to four processors, `GpuMeanTests` crashes ILGPU's CPU accelerator with "Index out of range" and takes the
test host down with it. It does the same at `df2bf3b8`, before this build, so the build did not cause it. The Farm
suite was run without it. Run it unpinned, or pinned to eight processors or more. That eight is enough is the
coordinator's suggestion and has not been tested.
