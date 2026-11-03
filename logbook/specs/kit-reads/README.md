# Five reads for the breeding and plants specifications (2026-11-03)

Reads 1 to 4 are measure 2 and measure 3 of `logbook/specs/breeding-urge/spec.md` section 2, and tests 5 and 6 (its first
half) of `logbook/specs/plants-hold/spec.md` section 3. They read recorded rows only, and nothing was built for them.
Read 5, section 5 below, is the breeding spec's measure 1, read through a census built for it. The scripts are
`scripts/reads/kit-reads/read1.py` to `read4.py`. Each takes run names, or reads its own list, and writes its table to
`scratch/kit-reads/out/`. A Sonnet subagent wrote and ran them, and the coordinator checked the numbers below against
the output files.

## Definitions

- A **grazer** is a birth row with `atk > 0` and `ink > 0`, as `scripts/reads/bloom/grazer-deaths.py` reads it.
- A **plant** is a birth row with `pho = 1` and `abs = 0`.
- A **starvation** is a death row with `c = "starved"`. Age at death is the death row's `t` minus the birth row's.
- A grazer's **last birth** is the latest reproduction row (`k = "r"`) whose parent is that grazer.
- The **smell screen's control** is `r65-c-6202` and `r65-c-6203`. The spec's 64 % starved matches 6202 (3,531 of
  5,482 grazer deaths); 6203's 44 % was not checked.

## 1. What a birth leaves a parent

Most starved grazers never bred, so a birth did not kill them. In each world 81 to 84 % of the grazers that starved
had no child, and they died young, at a median age of 200 to 450 s. Grazers that bred and then starved died a median
410 to 470 s after their last birth. That is sooner than breeders that died of age (610 to 740 s), and about 1.2 to 1.9
times their own usual gap between births.

| | r64s2-d-6202 | r65-c-6202 | r65-c-6203 |
|---|---|---|---|
| grazers with a birth row | 27,712 | 6,270 | 13,432 |
| starved | 10,753 | 3,531 | 4,936 |
| of those, never bred | 8,861 (82 %) | 2,857 (81 %) | 4,168 (84 %) |
| age at death, starved, never bred: q25, median, q75 (s) | 236, 449, 931 | 119, 197, 395 | 155, 290, 609 |
| age at death, aged, never bred: median (s) | 2,409 | 2,245 | 2,342 |
| starved breeders | 1,892 | 674 | 768 |
| starved breeders, last birth to death: q25, median, q75 (s) | 126, 409, 948 | 193, 446, 849 | 232, 467, 894 |
| aged breeders, the same: median (s) | 736 | 727 | 613 |
| starved breeders dead within 60, 120, 240 s of the birth | 14, 24, 36 % | 4, 15, 30 % | 4, 11, 26 % |
| time since last birth over the grazer's own mean gap: median | 1.16 | 1.86 | 1.48 |

`r64s2-d` is a resume at 24,000 s, so its rows hold only grazers born after it. Its ratio row counts the 1,152 grazers
with two gaps or more.

What it means (inference): no cluster of starvations follows a birth, so the reflex does not visibly drain parents.
The shorter wait among starved breeders could be a small drain, or poorer breeders starving sooner, and this read
cannot tell the two apart. The large loss is elsewhere. Four fifths of grazer starvation falls on young bodies that
never reached their gate. That is a forager's failure to find food, the kit's target (D292 to D294), and not a
breeder's.

## 2. The drift of the margin and of gestation

The median margin on grazer births stays at the founders' 600 s through the first 24,000 s, while its spread opens both
ways. The share of births at 600 s falls from 88 to 68 %. After 32,000 s only five worlds remain, and after 48,000 s
only `r63d-b`, so the late rows are that world's.

| window (s) | grazer births | q25 | median | q75 | at 599 s or more | gestation's share |
|---|---|---|---|---|---|---|
| 0–4,000 | 6,062 | 600 | 600 | 600 | 88 % | 15 % |
| 8,000–12,000 | 32,293 | 600 | 600 | 600 | 79 % | 17 % |
| 16,000–20,000 | 70,605 | 584 | 600 | 666 | 72 % | 17 % |
| 20,000–24,000 | 96,389 | 566 | 600 | 659 | 68 % | 17 % |
| 32,000–36,000 | 17,049 | 510 | 565 | 600 | 41 % | 25 % |
| 44,000–48,000 | 21,103 | 436 | 498 | 603 | 30 % | 20 % |
| 64,000–68,000 | 6,559 | 267 | 334 | 367 | 0.3 % | 11 % |

By 20,000 to 24,000 s the median is still 600 s in seven of the eleven groups of worlds. It is 646 s in `r63g` and
639 s in `r63h`, whose upper quartile is 787 s. The two `r64s2` worlds hold near 570 to 600 s past 32,000 s, with an
upper quartile of 678 s. Gestation's share in that window is 0.12 to 0.14 in four groups and 0.18 to 0.36 in the rest.
The read covers about 670,000 grazer births in 41 worlds from `r62cf` to `r65`, in windows of 4,000 s.

What it means (inference): the margin has no single direction. It falls in the long, starved `r63d-b` and rises in
`r63g`, `r63h` and `r64s2`. Breeding sooner pays in one world, storing in others, which is the state-dependent case
the urge is for. Gestation stays a minority of grazer births.

## 3. Where a plant's child lands

A plant's child lands in thinner water than its parent. The child's cell holds a median tenth of the parent's matter.
This is the opposite of hypothesis 5, which expected a child to find new water.

| world | pairs | child/parent: q25, median, q75 | child below half its parent | child denser | child above the world's mean | parent above it | distance, median (m) |
|---|---|---|---|---|---|---|---|
| r66-m-6202 | 51,517 | 0.03, 0.09, 0.30 | 82 % | 10 % | 20 % | 84 % | 22 |
| r66-m-6203 | 75,521 | 0.03, 0.09, 0.35 | 80 % | 12 % | 21 % | 82 % | 22 |
| r66-mk-6202 | 18,986 | 0.03, 0.08, 0.31 | 81 % | 11 % | 27 % | 91 % | 22 |
| r66-mk-6203 | 22,916 | 0.03, 0.09, 0.35 | 80 % | 12 % | 26 % | 88 % | 22 |

The child's place is its first positions sample, up to 10 s after the birth, and the parent's is its row in the same
sample. Matter is the nearest 100 s dump of `fields/*.matter.f32`, divided by the cell's 125 m³, so cells that cross the
floor read low. `r66-m`'s ratio holds at 0.08 to 0.10 in every 6,000 s window. `r66-m4k` has 132 and 58 pairs, which
read the same way less sharply. Over 99 % of children land in another 5 m cell.

What it means (inference): parents stand in the rich cells, and children land a median 22 m away, mostly off them. A
blade's child leaves the oasis, where a part grown on the same body would stay (`plants-hold` rules 1 and 2). The read
does not compare against random cells within 30 m, and it does not test whether a second blade's own cell is
stripped.

## 4. Bites that take the root

Of the kill rows with a plant victim, 95 to 99 % took the root. Most plant victims have one part, though, and the code
marks a one-part kill as a root kill (`WorldMouth.KillPart`). Among plants of two parts or more, 73 to 93 % of kills
were root bites, and they took 84 to 97 % of the blades lost.

| world | plant kill rows | one part | two parts or more, root | non-root | root share, two parts or more | blades lost to root bites |
|---|---|---|---|---|---|---|
| r66-m-6202 | 1,881 | 1,509 | 273 | 99 | 73 % | 84 % |
| r66-m-6203 | 8,349 | 5,945 | 2,058 | 346 | 86 % | 93 % |
| r66-mk-6202 | 38 | 30 | 7 | 1 | 88 % | 93 % |
| r66-mk-6203 | 946 | 765 | 169 | 12 | 93 % | 97 % |

Kills ended 1,782 and 8,003 plants in `r66-m`, against 17,859 and 21,954 starved and 26,137 and 35,232 dead of age.
Bites are 4 to 12 % of plant deaths there, and 90 % of the kill rows fall in the last 6,000 s.

What it means (inference): a root bite takes most of a branched plant, so hypothesis 6 points the right way. It cannot
carry much of why plants stay single, because bites kill few plants in these worlds. The other half of test 6, a probe
that splits the shape rule's refusals into their causes, is still owed.

## 5. The reserve against the gate

This is measure 1 of the breeding spec. `Evosim.Farm --gate-census <run> <seconds> <out>` restores a recorded world at
each named second and steps nothing. It writes every living body's reserve, its gestation account and its gate, which
is Core's own `Organism.BreedingGate`. Beside them go the two things `World.IsSolvent` asks first: that the body is
grown, and that births are not held. `scripts/reads/kit-reads/read5.py` keeps the adult grazers and shares them out by reserve
over gate, or account over gate for a gestating one. Main's build refuses these worlds' configs, so each ran on its
own build with the census put onto it: `effc6860` for round 65's control and `632af648` for `r64s2-d`. Both checkouts
are `scratch/wt-gate-<commit>`, and the output is in `scratch/gate-census/out/`.

| world, second | adult lump grazers | below 0.5 | 0.5 to 0.9 | 0.9 to 1 | at or above 1 | median | adult gestating | median |
|---|---|---|---|---|---|---|---|---|
| r65-c-6202, 6,000 | 6 | 100 % | 0 | 0 | 0 | 0.19 | 1 | 0.07 |
| r65-c-6202, 12,000 | 8 | 88 % | 13 % | 0 | 0 | 0.20 | 0 | |
| r65-c-6202, 18,000 | 235 | 59 % | 38 % | 3.0 % | 0 | 0.41 | 37 | 0.26 |
| r65-c-6202, 24,000 | 621 | 67 % | 29 % | 5.0 % | 0 | 0.37 | 75 | 0.39 |
| r65-c-6203, 6,000 | 40 | 73 % | 25 % | 2.5 % | 0 | 0.27 | 8 | 0.23 |
| r65-c-6203, 12,000 | 358 | 66 % | 28 % | 5.0 % | 0 | 0.34 | 59 | 0.26 |
| r65-c-6203, 18,000 | 1,249 | 65 % | 31 % | 3.8 % | 0 | 0.37 | 151 | 0.26 |
| r65-c-6203, 24,000 | 1,728 | 66 % | 31 % | 3.7 % | 0 | 0.36 | 221 | 0.27 |
| r64s2-d-6202, 30,000 | 474 | 71 % | 26 % | 2.7 % | 0 | 0.33 | 68 | 0.30 |
| r64s2-d-6202, 36,000 | 940 | 70 % | 27 % | 2.7 % | 0 | 0.31 | 133 | 0.35 |
| r64s2-d-6202, 42,000 | 920 | 68 % | 29 % | 3.2 % | 0 | 0.33 | 135 | 0.34 |
| r64s2-d-6202, 46,500 | 1,374 | 70 % | 26 % | 3.4 % | 0 | 0.31 | 178 | 0.38 |

The share columns are of the adult lump grazers, by reserve over gate. The last column is account over gate.

No adult grazer stood at or above its gate at any second, as the code says it cannot for more than a step. Once a
world holds a few hundred of them, the shares settle and stay level. About two thirds hold less than half their gate
and 3 to 5 % stand within a tenth of it. The median holds about a third.

What it means (inference): the line holds almost no one. A lump breeder keeps its margin after a birth and climbs from
there, so most of an adult grazer's life is spent refilling, far below the line. A birth gated by the brain's urge
(D299) acts on the few who reach the gate. Until foraging lifts income, it can change little, which fits read 1:
grazers are short of food, and a birth is not what drains them.
