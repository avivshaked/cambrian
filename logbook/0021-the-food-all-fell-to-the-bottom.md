# 0021 — The food all fell to the bottom

**2026-08-25**  ·  Milestone 4

Two questions came in together, both from the human, and the run that answered one answered the
other.

> *did we have enough deaths and reproductions to create an evolution?*

> *is there a cyclical(-ish) current that moves creatures around?*

The answers are **no, and it would not have helped** and **no, and it is the missing mechanism**.

## Six millimetres

`Organism` now records `BirthHeightY`, so the run reports how far a living creature has moved from
where it was born. Over 20,000 simulated seconds at 100 W/m², reaching generation 16 with 3,172
births:

| t (s) | alive | births | deaths | gen max | **rise m** | age s |
|---|---|---|---|---|---|---|
| 1000 | 198 | 178 | 63 | 9 | **0.006** | 289 |
| 2000 | 3161 | 3172 | 94 | 16 | **0.000** | 451 |

**A creature moves six millimetres from where it was born, over a mean life of 289 seconds.**
Founders are scattered by the floor across the top twenty metres.

So the ratio that decides whether selection can see swimming at all is **1 : 3300**. Where a
creature ends up is essentially entirely where it was put, and swimming contributes about 0.03% of
the variance. **This is not a search problem and no number of generations fixes it.** A trait whose
effect is three orders of magnitude beneath the noise it competes against is invisible to selection
in the same way a whisper is inaudible in a gale — running the gale for longer does not help.

That is a different claim from the one [0018](0018-nothing-to-swim-towards.md) made. 0018 said
locomotion has negative expected value because it cannot be aimed. Sensors fixed the aiming and the
result did not move, and this is why: even a perfectly aimed swimmer cannot go anywhere.

It also retires the hypothesis at the top of [0020](0020-a-sun-that-sets-over-a-world-with-one-crop.md)
— that the depth gradient had been *exhausted* by drifting to the top. It has not. The floor keeps
spawning founders as deep as twenty metres, and a founder that could rise really would beat one
that cannot. The advantage is there. Nothing can collect it.

## And the food is sixty metres down

The same run reported one absorptive creature alive out of 198, and none out of 3,161 — which
overturned [0020](0020-a-sun-that-sets-over-a-world-with-one-crop.md)'s explanation immediately.
That entry said absorptive cells were effectively unreachable at `CellTypeChance`. **They do not
come from mutation at all**: `RandomGenomeOptions.FounderCellTypes` draws absorptive one time in
four, in every floor spawn, and always has.

So they are reachable, they are born constantly, and they die. A run at 64 W/m² with two new
columns — nutrient density *where the creatures actually are*, and what share of the world's
detritus is already on the sea floor — says why:

| t (s) | alive | **absorpt** | detritus J | **J/m³ here** | **% on floor** | depth m | age s |
|---|---|---|---|---|---|---|---|
| 500 | 40 | **5** | 5585 | **0.511** | **0%** | −9.5 | 286 |
| 1000 | 46 | **2** | 6678 | **0.025** | **0%** | −6.6 | 572 |
| 1500 | 79 | **0** | 7075 | **0** | **0.2%** | −3.2 | 621 |
| 2000 | 212 | **0** | 7176 | **0.016** | **6.5%** | −1.9 | 544 |
| 2500 | 507 | **0** | 7374 | **0** | **31.4%** | −1.4 | 545 |
| 3000 | 1112 | **0** | 7758 | **0** | **61.3%** | −1.2 | 578 |
| 3500 | 1752 | **0** | 8330 | **0** | **77.5%** | −1.2 | 754 |

**The detritus drains to the bottom and stays there.** By the end, 77.5% of every joule of dead
matter the world has ever produced is lying on the sea floor at −60 m, and the density where the
living population sits is exactly zero. The absorptive count tracks it precisely — 5, 2, 0 — as the
food falls out from under them.

The human predicted the first half of this exactly: *"the world should have more and more material
as entities die… as the world gets more populated with food, then animals that consume the dead
matter should start to be more common."* The material does accumulate — 5,585 J to 8,330 J, monotonic.
It accumulates **where nothing lives**, sixty metres below a population that cannot move six
millimetres.

## The world has no return path for energy

The audit closes at 0.0000% throughout, and that is the point rather than a reassurance. **The
energy is not lost. It is immobilised.**

Light enters at the top. Plants grow at the top. They die at the top. Their bodies sink past
everything that could eat them and pile up on a floor nothing can reach, and there they stay
forever. Every joule the world has ever received is on a one-way trip from the surface to the
sediment.

Real oceans have exactly this problem and solve it exactly one way: **upwelling.** Deep water
carrying accumulated nutrients is driven back to the lit zone, and that is where essentially all
marine productivity happens. Without it the ocean would do what this world does — strand its own
fertility on the bottom.

## Which is the thing that was never built

§5A.4 specifies a current: *"A procedural divergence-free field (curl noise), evolving slowly. It
enters the existing model at one point: `FluidModel.BoxDrag` already takes a velocity, so passing
`bodyVelocity − currentVelocity` gives real advection for the price of a noise lookup."* §10 lists
it under Milestone 4. **The water is completely still and always has been.**

It resolves three separate measured failures, which is why it is worth writing down as one finding
rather than three:

1. **Energy has no return path.** A current with vertical structure lifts stranded detritus back
   into the lit zone. The food web can start.
2. **Position is inherited and immutable.** A current moves creatures independently of where they
   were born, which is what breaks the 1 : 3300 ratio above — not by making creatures faster, but
   by making birth depth stop being destiny.
3. **Swimming has no climbable gradient.** This is the one that matters most and it is the least
   obvious. In still water, *doing nothing is free and optimal* — drift costs zero, so the best
   strategy is to be a blob, and every run has duly converged on blobs. In moving water,
   **station-keeping is a task with continuous returns from arbitrarily close to zero**: a creature
   that swims a little holds position a little better than one that swims not at all. That is a
   gradient evolution can climb from nothing, which "swim four metres to better light" is not.

§5A.4 also notes that nutrients, being small and drag-dominated, are carried much further than
creatures — so food arrives in drifting patches rather than uniformly, which is what would finally
give the `Chemical` sensor something to smell.

*(Points 2 and 3 are the author's inference. §5A.4 argues for currents as heterogeneity and as
advection; it does not make the station-keeping argument.)*

## The pattern

0018: a hypothesis confirmed decisively and still not the answer. 0019: three knobs declared,
hashed and never read. 0020: a mechanism that worked on the first run over a substrate that did not
exist.

This one: **the previous entry's diagnosis was wrong, and it was wrong in the way this logbook keeps
recording.** The measurement said *0% food income*. Instead of asking the code where absorptive
creatures come from — a one-line grep, which would have said `FounderCellTypes` — I reasoned from a
mutation rate read ten minutes earlier to a conclusion that fitted the number. It fitted because it
was plausible. Every entry here is some version of that, and this is the first one where the
plausible-looking wrong thing was the explanation rather than the instrument.

The correction cost one run, because the fix was to add the column that distinguishes *there is no
food* from *the food is in the wrong place*. Those wanted opposite responses, and the aggregate that
had been reported for three entries — a single "food %" — could not tell them apart.
