# Round 58's predictions: three worlds that trade, three that do not (draft)

*2026-10-10, a draft by the coordinator. The exchange screen (`logbook/specs/r58-exchange-screen.md`) ran on
2026-10-11 and set the rate. It rests on D159, its rulings of 2026-10-09 and D160. The entry this becomes is committed on a clean tree
before the round launches, and comes to the owner first (D159). Every number says whether it is measured,
computed or guessed.*

## What it asks

In round 57, one leaf line took each world. By year 3 a single line of descent held 99% or more of the
living in seeds 1 and 3, and 91% in seed 2 (logbook/0137, measured). Seed 2 also grew a stomach line, bodies
that eat the matter settling on the bed, from one founder to 1,252 living. Seeds 1 and 3 had none that
lasted. So the worlds came out different from one another, and each poor in lines within itself.

Round 58 joins three such worlds in a ring that passes a few genomes along it, and keeps three apart as the
control (D159). It asks whether the joined worlds keep more lines alive (V), and whether a way of life that
one world has and another lacks spreads to it by descent (S). It asks whether a guild that dies out in one
world comes back from another (R), whether one world's line takes all three (W), and whether the joined
worlds' winters fall more into step than the apart worlds' (Y).

## The world

Round 57's world runs unchanged, from `rounds/env-r57.ps1`. Its year of 6,000 s holds three days of summer
at 1.6 of the mean light and three of winter at 0.4, its day is 1,000 s, and the weather changes daily. The sky is drawn from
its own seed, 57, so every world of the round shares one sky. The trickle refounds from round 55's control
pool at a share of 0.1. Species tracking is off, so the species ruling of D159 does nothing here.

Six worlds, each 48,000 s, on the graphics card in single precision (D157). Single precision is a new
realisation of every seed, so no world replays round 57. The isolated three are also the first card round's
own baseline.

The exchange follows D159's rules 2 to 6. Each joined world sends copies of genomes, picked at random among
its living, to the next world in the ring. Seed 1 sends to seed 2, seed 2 to seed 3, and seed 3 to seed 1. The receiver
plants each copy as a founder, by the inoculation path and the founders' placement. Exchanges happen only
at fixed simulated times between blocks, so the group replays bit for bit. Each arrival's lineage row says
`m`, with the world and creature it came from, and `immigrants.jsonl` records it with its guild.

The screen sets the rate. D159's first value is three genomes a world every 6,000 s, 24 in the
round. By round 57's shares that brings a world between 0.01 and 1 jointed body in the whole round (computed;
the screen's spec has the table), and twice as many at the rate chosen. The screen ran three, six, 12 and 48 a year for 12,000 s on 2026-10-11
and 12. Its rule picks six a year, three genomes a world every 3,000 s and about 48 in the round (measured).
At 12 a year, one line held 67.5% of seed 1's world by 12,000 s. At 48 a year, one seed 2 line took two
worlds of three. At six a year, no world's immigrant share passed 4.2%, though seed 1's world was rising
fastest at the end. Seed 1's world is the weak receiver at every rate, and W below reads whether it is
swamped by 48,000 s.

## Twins, and what the owner ruled

The isolated three reuse seeds 1 to 3, so each joined world has a twin that differs from it only by the
arrivals (the owner's ruling of 2026-10-10, amending D159's "each with its own seed"). Round 57 showed
that seeds differ widely: the leaves' median margin differed eightfold between them. Read as three pairs,
each clause compares a world with itself. The two worlds of a pair replay identically until the first
exchange, which checks the runner on the round itself. The cost is that the round draws only three worlds'
worth of chance.

The owner sees the three joined worlds as islands of one world, so a line founded by an arrival counts in V
and S. Once planted, an arrival's descendants are born and selected in the world that received it. The
screen's bars of 25% and 50% stand as a starting value, chosen by the coordinator. The owner also required
that the migration replay in a later run, which B2 checks.

## Predictions, proposed

| # | prediction | falsified by |
|---|---|---|
| V | **the joined worlds keep more lines**: at 48,000 s, the root lineages holding at least 1% of the living number more in each joined world than in its twin, in 3 of 3 pairs. A line founded by an arrival counts as one line. I guess this holds | lineage.jsonl, positions.jsonl |
| S | **a way of life spreads by descent**: in at least one joined world, a guild (jointed, absorptive or leaf) that it lacks at some sample after 6,000 s is later held, for at least one year, by at least 20 living bodies descended from an arrival. Read only if some joined world lacks a guild that its sender holds; otherwise unread, and said so. *The 20 bodies and the year are my choice, set before the screen.* At three arrivals a world every 3,000 s, about 48 in the round, I guess it fails | immigrants.jsonl, lineage.jsonl |
| R | **a lost guild comes back**: a guild that falls to no living bodies in a joined world after 6,000 s returns there through an arrival: at least one living body of that guild descended from an arrival at every sample for at least one year. Read only if a guild dies out in a joined world; otherwise unread. No guess, since round 57 lost no guild outright that I know of | the same |
| W | **one world's line takes all three**: at 48,000 s, the descendants of a single founder hold over half the living in every joined world. A line that arrives many times roots many lines in the world it reaches, so the reader traces each arrival back through the world and creature it came from to the founder it descends from. This is asked to be read both ways: holding means the exchange swamps, failing means the worlds stay distinct. *The screen's rule keeps the rate below where it would hold by 48,000 s, so I guess it fails* | lineage.jsonl |
| Y | **the joined worlds' winters fall into step**: the depth of each year's winter (round 57's W measure, years 2 to 8) correlates more between joined worlds than between isolated ones. The shared sky alone puts every world's winters in step to some degree, so the isolated three are the bar, not zero. With three worlds a group there are three pairs to correlate, so this is weak evidence either way. I guess it fails | stats.jsonl |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and every world ends on its budget, in 6 of 6 | stats rows, run.json |
| B2 | **the exchange replays**: each joined world matches its twin row for row until the first exchange, and the joined group, resumed on the card from its checkpoint at 45,000 s and run to 48,000 s, matches the uninterrupted group in stats, digest, lineage and arrival rows. Left out are the harness's two step counters and the wall timings, which count from the process's start, and `steering.jsonl`, whose sampler counts its 100 s from the process's first step, so a resumed run samples at other seconds by construction. About 15 minutes of the card after the round | the resumed runs, gpu-accept, the speed agent's `group-resume-compare.py`, moved to `scripts/reads/` before the round |

## The readings

- Per world and year: the most living in summer, the fewest in winter, and the largest root lineage's share.
- Per joined world, at every 1,500 s, from `scripts/reads/immigrants.py`: arrivals planted and refused by
  reason, the share of the living in arrival-founded lines, and the arrival lines alive and the largest.
- Per joined world and guild: the guilds of the genomes offered and of the living in arrival lines
  (`scratch/nursery-speed/immigrant-guilds.py`, to move to `scripts/reads/` before the round).
- Per joined world, the share of the living by the world whose founder began each line, following every
  arrival back round the ring (`scripts/reads/x-trace.py`, as the screen read it).
- With twins, each joined world against its twin to the first exchange, row for row (`scripts/compare-det.py`).
- The pictures: the theatre at a summer peak and a winter trough of one joined world and its twin in year 8,
  and an arrival line where one took hold.

## The reader

`scripts/reads/r58-read.py` reads V, S, R, W and Y, and its docstring states each clause as it is
computed. Every body born without a parent roots a line: the floor's founders, the trickle's, the pool's and
each arrival. V prints its count of lines by those kinds, so a count raised only by arrivals or the trickle
shows. S is readable only when a joined world lacks a guild at a sample while its sender holds it. A fall
for R is read from the births and deaths themselves, so a dip shorter than a sample still counts. Y leaves
out a pair of worlds whose depths do not vary. On the trial `r58t2` the reader's living matches the stats
rows at every sample, and every one of the 108 arrivals traces back to a founder.

## The launch, as planned

The two groups run one after the other on the card, the joined group first. The card's switches are the
trial `r58t2`'s: concurrent mode, the scent and the snow's transport on the card. Each group runs detached
at 16 threads on logical 16 to 31, with checkpoints every 1,500 s, under the heat watch. The screen's groups ran
5.0 to 5.4 times real time over their first 12,000 s, and a filling world slows. So a group takes about 3
to 4.5 hours, a guess from that pace and D159's correction. The build is `r58-runner` `02c448e6`, `card-r57`
with the speed branches merged after the day sitting (D160), which passed its card check. Before launch, the CPU accelerator runs
the group with every flag the round uses, then a short card run, as a founded round needs
(`src/Evosim.Farm.Gpu/CLAUDE.md`).
