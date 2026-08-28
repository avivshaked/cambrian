# 0033 — The surface stripped itself

**2026-08-28.** Producers were consuming nothing, so nothing a creature did made its own
position worse. Giving reproduction a material cost built a three-hundred-fold vertical
gradient out of a uniform world in twenty minutes of simulated time, and the first world all
day that stopped needing the population floor.

## The asymmetry

```csharp
PhotosyntheticCell.Acquire → CellIntake.Light(irradiance × litArea × efficiency × dt)  // no pool
AbsorptiveCell.Acquire     → CellIntake.Food(nutrientDensity × clearance × volume × dt) // depletes
```

Consumers deplete; producers do not. The only thing a producer emits is shade, and shade harms
creatures *below* it and never itself. So there was no negative feedback anywhere on occupying
the best spot — the depth axis was a ramp with its maximum at the boundary, the answer to "where
should I be" was always *the top*, and every arm this project has run sorted to the surface and
stayed there.

That absence has been mistaken for other things repeatedly, most recently for the conclusion in
[0027](0027-the-prize-was-smaller-than-the-entry-fee.md) that a muscle has nothing worth buying.
It had nothing worth buying because there was nowhere better to be.

## What was built

[D048](../DECISIONS.md#d048). Light stays energy; matter becomes a separate, conserved currency
in its own field. Reproduction needs both — `MatterPerTissueJoule` per joule of the child's
tissue, drawn from **the parent's own layer** — and death returns it to the layer the body died
in, whence it sinks.

Reproduction is the right place to charge it and not merely a convenient one. §5A.6 has no
growth, so tissue is created exactly once, when a child is made; and no quantity of sunlight
builds a daughter cell without nitrogen and phosphorus. The consequence is that a starved world
does not kill its inhabitants, **it stops them breeding** — which is what a nutrient-limited
bloom actually does.

Matter is deliberately outside `StandingJoules`. §5A.2's audit is a hard equality over energy,
and folding a different substance into it would let the books balance by counting the wrong
thing.

## What happened

200 W/m², `MatterPerTissueJoule` 0.5, seeded uniformly at 1.0/m³:

| t | alive | matter at surface | matter deep | conceptions blocked | floor spawns | mean depth |
|---|---|---|---|---|---|---|
| 100 | 40 | 0.802 | 1.136 | 0 | 48 | −10.4 |
| 300 | 55 | 0.442 | 1.305 | 0 | 0 | −5.3 |
| 500 | 219 | 0.009 | 1.393 | 4,707 | 0 | −2.6 |
| 900 | 564 | 0.005 | 1.301 | 65,639 | 0 | −1.7 |
| 1,200 | 931 | **0.004** | **1.151** | 114,450 | **0** | −2.6 |

**The surface stripped itself to 0.4% of its starting concentration**, and the deep rose *above*
it as bodies fell. A ~300× gradient, from a uniform start, made entirely by the creatures. It is
the first thing this world has produced that was not either imposed by a config value or an
artefact of the refill rule.

**The floor went to zero at t=300 and stayed there.** By [D021](../DECISIONS.md#d021)'s success
condition — "fires at t=0 and never again" — this is the first world today that was alive rather
than being kept alive, and [0032](0032-the-instrument-that-was-designed-and-never-built.md) is
the reason that can now be said at all.

**The population keeps growing anyway**, because mixing resupplies the surface from below. So
primary production here is now limited by **vertical nutrient flux** — which is the constraint
that governs it in the real ocean, and nobody put it in. It falls out of a depleting producer, a
sinking corpse and a stirring water column.

## The bug its own test found

`NutrientField.Take` is a partial-take API: it removes `min(asked, stock)` and returns that.
Checking affordability *by* taking therefore removed the partial amount and then discarded it on
every blocked conception — 132 units of 24,000 in a 400 s test. It leaked fastest exactly when
matter was scarce enough to bind, so the mechanism would have quietly dismantled itself in the
worlds where it mattered most. `MatterIsConservedBecauseNothingCreatesIt` caught it on the first
run; availability is now checked before the take.

## What it sets up, and what is still open

Creatures sit at −2.6 m in water they have stripped, while the matter is at depth, and **nothing
in §5A.1 can move them there.** That is the pressure [D049](../DECISIONS.md#d049)'s buoyancy cell
exists to answer, and it is why D049 was sequenced after this: buoyancy in a world whose optimum
is at the surface collapses to "everyone floats". The optimum is no longer at the surface.

⚠ **The ratio is unmeasured.** 114,450 blocked conceptions against 931 living creatures says
matter binds hard; whether it binds *too* hard is open, and the blocked count is also pure wasted
compute. Calibrating it is the next measurement, not the next feature.

⚠ One run, one seed. Everything above is a single observation.

## Correction: the wasted bodies were not the bottleneck

I claimed, in the commit that added the early-bail check, that 944 blocked conceptions per birth
— 2.3 million genome mutations and body developments built and discarded — was *why* the probe
reached t=2,100 instead of its 4,000 s budget. **That was wrong, and I asserted it without
measuring.**

`Conceive` now refuses before mutating when the parent's layer cannot afford the cheapest child
that could physically exist. Re-running the identical probe against the identical 12-minute wall
budget:

| | reached | alive | blocked : births at t=2,000 |
|---|---|---|---|
| before | t=2,100 | 2,463 | 899 : 1 |
| after | t=2,000 | 2,242 | 899 : 1 |

**No speedup, and the same ratio to three figures.** The bound is exact — no conception is
refused that the full check would allow — but it is far too loose to filter anything: a real
child costs orders of magnitude more than one part at `MinPartVolume`, so almost every blocked
conception still pays for its mutation and body before failing the real check.

The check is kept, because it is correct and costs nothing. The claim is withdrawn. Where the
time actually goes is unmeasured, and with 2,400 creatures the physics step is the obvious
suspect — but that is a guess, and guessing is what produced the withdrawn claim. A tighter
bound (the parent's own tissue predicts the child's, since children are near-copies) would
filter far more and is available, but optimising before measuring is how this happened.
