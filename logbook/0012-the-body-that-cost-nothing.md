# 0012 — The body that cost nothing

*2026-08-07*

[Entry 0011](0011-the-sun-was-infinite.md) made the sun finite and found the calibration
transition. This one is Phase 2: death returns tissue, absorptive cells feed on it, and
consumers scavenge it. It began by finding that §5A.2's audit had been closing for a smaller
world than it claimed.

## Two things were free

A body was free to build. A parent paid its offspring's endowment and a fixed overhead, and
that was the whole price, for a mote or a whale alike. Only upkeep ever noticed the
difference, and upkeep is paid later, by the offspring, out of energy it has to find for
itself.

A corpse was worth nothing. Death removed a creature and its body went nowhere, so the
detritus niche the design leans on had no fuel. `ConsumerCell` can only feed through
`TissueContact`, which needs physics that does not arrive until Milestone 4, so it earned
nothing whatsoever. The predator valley §5A.3 worries about was not merely wide. It was
infinite.

Both are fixed by one number, and it has to be one number.
`CellType.TissueEnergyPerCubicMetre` is what a body is worth, so it is what a body costs.
The parent pays it at birth and the pool receives it at death. If the two ever differed, a
birth-and-death cycle would create or destroy energy. That is a free-energy source put in by
us rather than discovered, and it is the single thing the audit exists to make impossible.
Both sides call one method.

## The pool had the same bug light did

`AbsorptiveCell.Acquire` read a nutrient density from its surroundings and converted it to
joules, with nothing anywhere being reduced. That is the shape that made the population
unbounded when light worked the same way: an infinite subsidy wearing the costume of a
resource. It had never bitten, because the density was always zero.

Building it as a finite stock that feeding depletes, shared proportionally in the same way
light is, means the fault cannot appear rather than appearing later under a different name.
That is entry 0011's lesson, applied before rather than after.

## Why one number was not enough for a cell's intake

A cell used to report a single number: the joules it acquired. The world needs three facts
about it.

- How much was sunlight, which is new energy, and the only kind there is.
- How much the cell kept from eating, which is transferred, so something must lose it.
- How much left the pool in order to yield that.

The third is not the second. Yield is below 1, so a consumer tearing up carrion destroys
most of what it takes, and that is what makes a food chain shorten with each level. Leaving
the difference in the pool turns every meal into a partial refund, and a food chain into a
perpetual motion machine.

The first implementation recovered the split by running the whole metabolic step a second
time with the food removed, and subtracting. That is two expressions of one quantity, on the
only hot loop in the design, at four evaluations per creature per step. It is the thing this
project keeps writing warnings about. A struct called `CellIntake` with three fields removes
both problems at once.

## Three mistakes, in order of embarrassment

The gate stopped covering the price. Reproduction is gated on whether a parent can afford
its brood, and the gate was left checking endowment and overhead while the price now
included tissue. So every solvent creature cleared a gate it could not pay, mutated a
genome, developed it, discovered it was unaffordable and threw it away. That happened once
per creature per step, for the whole run. The suite went from 18 seconds to not finishing.
The comment on the gate explaining why it exists was still sitting directly above the code
that no longer did it.

I watched the wrong variable and nearly called a success a failure. With the food web
running, the population at 96 W/m² went from about 38 to over 6,000 and kept climbing. That
looks like entry 0011's unbounded growth returning. It is not, and the second column is why.

| t | population | living biomass |
|---|---|---|
| 20,000 s | 6,420 | 546 m³ |
| 40,000 s | 8,508 | 559 m³ |

Biomass had converged. Light caps total tissue, because in steady state the world's
metabolic burn equals its light income, and burn is proportional to tissue. It says nothing
at all about how many creatures that tissue is divided into. The population rose because
bodies got smaller, and bodies got smaller because building one had finally started to cost
something. The correct result and the failure mode look identical in a population count, so
`BiomassIsCappedByLightRatherThanByUs` now asserts the thing that separates them.

And a four-minute test suite. The sweep at high irradiance was stepping worlds of thousands
of creatures for 20,000 seconds each. It was witnessing a boundedness that a direct
assertion on biomass shows better, and a hundred times faster.

`CLAUDE.md` says the one-second feedback loop is the point of a core library with no
dependency on `UnityEngine`.

Four minutes is not a slow test. It is a different kind of test wearing a unit test's
clothes, and `Evosim.Core` is back to 16 seconds.

## What it did to the world

The transition did not move. It is 32 W/m², identical across three seeds. But the seeds now
agree with each other far more closely than before, and everything above the transition
changed character.

The clearest number is the one entry 0011 flagged as unresolved: the total shadow cast by
the whole population, as a multiple of the world's own area.

| | before | after |
|---|---|---|
| shadow ÷ world area | 10 – 290× | **0.3 – 1.3×** |

Entry 0011 recorded that more light bought bigger creatures, and worried that the world had
no length scale to stop them. Both were true, and both had the same cause. A body cost
nothing, so the only limit on size was shading, and shading rewards being large. With tissue
priced, creatures are world-scale. Nothing measured the world and forbade a giant; a giant
is unaffordable to build.

The thin-sheet exploit died the same way, economically rather than by the clamp that had
been holding it.

The energy audit is now an equality across a whole food web,
`EnergyIn − EnergyOut == reserves + bodies + detritus`, and it closes to 0.0000%.

## What is still open

Detritus does not remineralise. It sinks, and 80–93% of it ends up on the sea floor where no
lineage has yet evolved to live. It is a sink and not a source, so conservation is
untouched. But it grows without bound, and whatever first evolves to live down there
inherits a very large bank. That could be a slow leak or the most interesting thing in the
world, and there is no way to tell yet, so it is recorded rather than fixed.

Nothing still dies of age. That is unchanged from 0011, and it is still the reason minimum
generation depth cannot be used as an instrument.

## The pattern

Entry 0011 ended by saying each fault was hidden behind the one in front of it. This time
two of the three were faults I introduced while fixing the previous ones. One was a gate
that stopped matching its price. The other was a redundant recomputation of a quantity I had
just finished arguing should never be computed twice. The third was reading a correct result
as a failure.

So the discipline that matters is narrower than "check the design against what it claims".
After every change, check the thing the change was about. That meant biomass rather than
population, and the price rather than the gate. Both were one measurement away.
