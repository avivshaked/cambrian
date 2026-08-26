# 0023 — Nothing died of old age

**2026-08-26**  ·  Milestone 4

[Entry 0022](0022-the-conveyor-belt-and-the-thin-soup.md) ended on a number it did not chase: 98
deaths against 1,164 births, and the observation that *a world where almost nothing dies is a world
where almost nothing is selected*. The human read that and said the obvious thing back — *"that
sounds like a real problem, and at the very least age should kill creatures, no?"*

It was already written down. DESIGN §5A.6b has carried this for weeks:

> ⚠ **Superseded — it is a true statement and an unreachable one.** Nothing dies of age: §5A.6
> kills only at zero energy, so a founder whose income covers its upkeep never dies at all.

The project noticed immortality, correctly diagnosed it, and then **fixed the instrument** — struck
out minimum generation depth as unmeasurable and replaced it with how long the floor had been quiet
(D025). The ecology was left exactly as it was. That is the whole entry: a known defect, filed
under the wrong heading, for long enough that every result since was measured in a world where a
successful lineage could never be replaced.

## Ageing without a lifespan

A maximum age would be an exogenous rule, and §5A.0 exists to remove those. So senescence raises
the *cost* of being alive: at age `t` the wear factor is `1 + t/T`. Death stays where §5A.6 puts
it — reserve reaches zero — and how long a body lasts still depends on how well it earned.

The first implementation put wear on upkeep alone. The human corrected it before it was measured:

> *"what age should do is have an impact on energy expenditure and consumption — the older the
> creature is, the less it converts, and the more it needs to expend."*

Right, and cheaper to fix then than after a sweep. Costs alone is the easier code and the wrong
biology: senescence is loss of function first and expense second, and a creature photosynthesising
at full efficiency until the day it starved is an odd thing to call old. Income is now divided by
the same factor the costs are multiplied by.

One choice inside that is worth stating because it went the non-obvious way. **What falls is what a
creature keeps, not what it takes.** `PoolDrawn` is unscaled, so an ageing population strips the
larder at full speed and feeds itself worse on it; the shortfall leaves through the transfer loss
§5A.3 already accounts for, and the audit closes at 0.0000% with no new term. Scaling the draw
would have made ageing a form of restraint — a world of the old depleting *less* than a world of
the young.

## Two settings, both lethal

T = 300 s and T = 1,200 s. Both collapsed to the population floor and stayed pinned there, kept
alive by it: 754 floor spawns against 42 real births at T=300, and generation depth never past 1.
By §5A.6's own standard — *"a floor that keeps firing is a failed world"* — two failed worlds.

The reason is arithmetic that was available before the run. §5A.2 sets basal metabolism just above
what light covers, so the margin is thin by design; generation time here is around 270 s. A
doubling time of 300 s therefore taxes a creature at 2× before it has bred once. **The knob is long
against the reproductive career, not against the lifespan**, and it was chosen against the wrong
quantity.

## What the transition looks like

| T | alive at t=4000 | births | floor spawns | gen max | gen min |
|---|---|---|---|---|---|
| off | 1,342 | 1,475 | 100 | 17 | **0** |
| 30,000 s | 1,066 | 1,177 | 99 | 14 | 0 |
| 10,000 s | 876 | 946 | 105 | 15 | 0 |
| 3,000 s | 106 | 142 | 134 | 20 | **1** |
| 1,200 s | 40 | 35 | 473 | 9 | 0 |
| 300 s | 40 | 42 | 754 | 1 | 0 |

## The founders are dead

**Minimum generation depth rose above zero and stayed there** — the first time in this project.
Replicated across three genuinely independent seeds (D034), and it climbs rather than touches:

| run | first t with gen min > 0 | gen min at end | alive | deaths / births |
|---|---|---|---|---|
| T=3,000, seed 1 | 3,600 s | 1 | 106 | 170 / 142 |
| T=3,000, seed 2 | 3,500 s | **3** | 1,919 | 1,226 / 3,038 — 40% |
| T=3,000, seed 3 | 4,400 s | **6** | 699 | 524 / 1,046 — 50% |
| T=5,000, seed 1 | 4,700 s | **3** | 619 | 582 / 1,089 — 53% |

**No floor-spawned creature is alive in any of these worlds.** Seed 1's population of 106 turned
out to be seed-specific; the others carry 619–1,919, so this is turnover and not collapse.

The control is sharper than the mortality column, and corrects it. The immortal world is **not** a
world without death — at seed 2 it kills 33% by t=4,100, because shading at 1,761 creatures is
severe. Matched on seed and elapsed time:

| at t=4,100 s, seed 2 | immortal | T=3,000 |
|---|---|---|
| alive | 1,761 | 1,599 |
| deaths / births | 828 / 2,496 — 33% | 629 / 2,121 — 30% |
| shading | 57.5% | 48.1% |
| gen max | 19 | **23** |
| **gen min** | **0** | **3** |

Near-identical mortality, and only one of them retires its founders. **What immortality protects is
not creatures in general but the particular creatures that reached the good depths first and never
had to give them up.** The death rate was the wrong number; 0022 quoted 8% and read it as "almost
nothing dies", and the real defect survived a world where a third of everything dies.

The struck passage in §5A.6b closes with a prediction:

> *Minimum depth is still reported and is still the stronger claim if it ever rises, which it will
> as soon as anything can die of something other than starvation.*

Which is what happened, by the mechanism it named. §5A.6b is amended rather than rewritten; the
supersession is superseded and both are dated, because the record of what was believed and when is
the reason any of this is checkable.

And it is *cheaper*: generation 20 in 1.3 minutes of wall clock against 17 in 6.9. §5A.6b calls
depth per wall-clock hour the actual evolutionary clock, and carrying thirteen hundred immortal
creatures was buying population, not progress.

## Two smaller things, recorded because they were wrong first

**The test that proved the opposite of what it claimed.** The first version compared an immortal
world and an ageing one *at their own final times* and concluded that senescence made creatures live
**longer**. It did not. The immortal world hits §5A.7's ceiling at t=467 s and stops, so the
comparison was 467 s of one world against 1,500 s of the other, sampled where mean age (127 s) is
well under the doubling time and senescence has not yet acted. Same shape as
[0017](0017-what-a-muscle-costs-to-own.md)'s population read mid-doubling, and the same shape as
[0022](0022-the-conveyor-belt-and-the-thin-soup.md)'s mixing test measuring a transient. Three
times now: **a comparison sampled before the mechanism starts measures the interval before the
mechanism starts.**

**`StandingWatts` was frozen at birth.** It is the denominator of `SecondsOfReserve` and of §4.4's
`Energy` sensor, and under senescence the cost of doing nothing stops being a property of the body
alone — so a creature's estimate of its own remaining life would have grown *more* optimistic the
closer it came to starving. Refreshed from the ledger each step, which is free: upkeep and neural
are exactly what it recomputes and they were just computed.

## What is still not fixed

Joints still go to zero. Absorptive creatures still starve on a break-even of 8 J/m³ against 0.18
produced. The population still grows without an endogenous ceiling in most arms — shading reached
45% at 1,342 creatures, so §5A.7's light cap is real and beginning to bite, but it has not closed.
Senescence supplies mortality; it does not supply density dependence.

## The pattern

0018–0022: a hypothesis that was not the answer, three knobs never read, a mechanism without a
substrate, a diagnosis that was wrong, a guard that asserted the wrong quantity.

This one is different in kind and worse. **The defect was found, correctly diagnosed, written into
the design document, and then answered by changing the instrument that measured it.** Nothing was
mistaken. The struck-out paragraph is accurate, its replacement is a genuine improvement, and the
prediction at the end of it is exactly right. What was missing is that *"nothing dies of age"* was
filed as a fact about the measurement rather than as a fault in the world — and a defect written
down in the right words under the wrong heading is harder to see than one nobody noticed at all.
