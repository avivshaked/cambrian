# 0025 — Something ate something

**2026-08-27**  ·  Milestone 4

[Entry 0024](0024-the-larder-filled-and-nobody-came.md) left one question open, and phrased it so
that it could only be answered by a measurement: the trophic niche opens on its own, absorptive
creatures arrive and survive on it, and the count never reaches two. **Arrival-limited, or
establishment-limited?**

It is arrival-limited, and the world grew a food chain.

## The probe

`EVOSIM_CELLTYPE_MUTATION` raised the cell-type mutation rate twentyfold — one absorptive arrival
per ~256 births instead of one per 5,128. Nothing else changed: not the energetics, not the cell
parameters, not the physics. The probe accelerated a clock; it did not alter what the clock was
measuring.

| t | density | absorptive alive | arrivals per 250 s window |
|---|---|---|---|
| 9,000 | 5.99 J/m³ | 1 | 1.70 |
| 10,000 | 7.17 | 6 | 1.28 |
| 11,000 | 8.65 | 10 | 1.00 |
| 11,500 | 8.05 | 13 | 1.25 |
| 12,250 | 8.60 | **15** | 1.44 |

**Arrivals flat and slightly falling; standing population up fifteenfold.** A crop maintained purely
by mutation pressure tracks its arrival rate. This one did not.

## The instrument that made it a fact instead of an inference

That argument is suggestive and not conclusive, and the hole in it is specific: nutrient density was
rising over the same window, so absorptive creatures were living longer, and a longer-lived standing
crop draws the same curve as a reproducing one. Density went 5.99 → 8.60 J/m³ across exactly the
interval the population grew.

So: an `inherit` column, counting living absorptive creatures whose **parent** was also absorptive,
checked against every absorptive id ever seen rather than against the living — because a parent that
has already died is the case that matters. It means the lineage outlived its founder.

| t | density | absorptive | **inherited** | share | food income |
|---|---|---|---|---|---|
| 10,500 | 6.96 J/m³ | 8 | **1** | 12% | 0.13% |
| 11,000 | 8.65 | 10 | **2** | 20% | 0.11% |
| 11,250 | 8.00 | 12 | **3** | 25% | 0.13% |
| 12,000 | 8.26 | 12 | **3** | 25% | 0.25% |
| 12,250 | 8.60 | **15** | **4** | **27%** | **0.42%** |

A mutation-pressure equilibrium holds `inherit` at zero however large `absorpt` grows, because every
occupant is a fresh convert. Instead better than a quarter of the detritivores were **born into the
trade**, and the share rises as the larder gets richer.

**The instrument under-reports, and in the safe direction.** `EverAbsorptive` is populated when a
row is sampled, every 250 s, so a creature that lives and dies between two samples never enters the
set and its offspring are not credited. Lifetimes run around a thousand seconds, so most are caught,
but 27% is a floor rather than an estimate. A positive reading is trustworthy; a zero would have
been weaker evidence than it looked.

## What that is

Energy path: **sun → photosynthesiser → corpse → detritus → consumer**, with §5A.2's audit closing
at 0.0000% the entire way. It is the first food chain this project has had.

Nobody designed it. There is no fitness function, no niche assignment, no archive cell, and no line
of code that says detritivores should exist. Detritus accumulated because [0023](0023-nothing-died-of-old-age.md)
made things die of age; the trade became worth taking because density crossed the margin
([0024](0024-the-larder-filled-and-nobody-came.md)); the creatures that could take it arrived by
mutation and bred. That chain of consequences is the whole of D017's bet, and this is the first time
it has paid.

It is also **small**: 15 creatures in 2,478, and 0.42% of income. A toehold, not a trophic level.

The human called the mechanism before any of it existed — *"the world should have more and more
material as entities die… as the world gets more populated with food, then animals that consume the
dead matter should start to be more common"* — and every step of that sentence is now on the record
in order.

## An unplanned determinism check

The `inherit` column meant re-running the probe, same seed, same `configHash 4a4473f17b43c449`. The
absorptive column came back byte-identical across 10,500 simulated seconds and two thousand
creatures of PhysX:

```
probe:    3  2  1  0  1  1  4  4  6  7  8
lineage:  3  2  1  0  1  1  4  4  6  7  8
```

§7 only promises the hash *detects* mismatches rather than guaranteeing portability. Same-machine
reproducibility at this depth is better than the design claims, and it was free.

## What actually stopped the run

Not extinction, not runaway, not the population ceiling. **Ninety minutes of wall clock**, at 2.3×
real time and falling as the population grew, ending at t=12,309 against a 16,000 s target.

That is the finding underneath the finding. At default settings this world reaches the same state —
arrivals happen at one per 5,128 births rather than never — but 8,140 births took ninety minutes, so
the default path is roughly a twenty-hour run. **The binding constraint stopped being ecological
somewhere around t=9,500 and became throughput.** Every remaining question on the list — do joints
ever pay, does anything swim on purpose, does shading ever close the population — is a question
about states this world reaches later than it can currently be run to.

## The pattern

0024 recorded two recommendations killed by the measurements run to justify them. This entry is the
other half of that discipline and worth recording for the same reason: **the argument from arrival
rates was correct, and it was not sufficient.** Flat arrivals against a rising population is real
evidence and it had a confound sitting in plain sight in the next column of the same table. One
extra counter turned a persuasive story into a fact, and cost thirty lines.

The version of this entry written without it would have claimed a food chain, been right, and had no
way to know.
