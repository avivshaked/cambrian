# Round 55's predictions: the nursery's swimmers meet the world

*2026-10-04, written before round 55's first seed and committed before it starts. It rests on the
owner's rulings D146, D147 and D149, on round 54's read (`logbook/specs/r54-read/read-30000.txt` on
`round-54`), and on a smoke of round 55's world on seed 2 to 6,000 s (`r55smoke-s2`,
`logbook/specs/r55-build/smoke-read.txt`). Every number is round 54's or the smoke's, and each says
which. A number I guessed says so.*

## What it asks

The nursery evolved brains on six small bodies in four runs (`logbook/specs/nursery-plan-joint-drive.md`).
On its exam they beat a frozen body by 236 to 308 J and a hand-wired forager by 9 to 81 J, and tied the
best steady stroke. They read the body's own senses and not the scent. One seed per run could not say
which of the four recipes was best, so the owner chose to let the world say (D149).

Round 55 puts 161 of the nursery's genomes into round 54's world, with every creature on the one brain
(D146). Half of all founders are drawn from them, the ten-to-a-body sets of the 24 runs and bodies
weighted equally: at the floor, while the world fills, and through the trickle after. Seed 1 carries none
and is the control.

Round 55 asks three things. The first is whether the nursery's lines hold any of the tank at 30,000 s
(I1). The second is whether one nursery run's lines hold the most in both inoculated seeds (I2). The
third is whether the world with them is a different world from the control, in its jointed share (I3).

## The world

Round 54's world (`rounds/env-r54.ps1` on `round-54`), with three changes, all in `rounds/env-r55.ps1`:

- **The one brain for every creature** (D146, D147): founders wired from every sense to every output,
  brains grown by mutation at birth. Round 55 therefore differs from round 54 in its brains too, and
  seed 1 controls for the inoculation only.
- **The drive sense on** (D144), so the world reads the nursery's thirteen channels.
- **The pool** (D117's trickle pool, D149): a weighted list, `rounds/r55-pool.tsv`, of round 54's four
  stomachs (one founder in ten) and the nursery's 161 genomes (one in two), drawn at the floor
  (`EVOSIM_FLOOR_POOL_SHARE`) and in the trickle (`EVOSIM_TRICKLE_POOL_SHARE`), both at 0.6. Seed 1
  draws only the stomachs, in the trickle only, as round 54 did: `rounds/r55-pool-control.tsv` at 0.1,
  the floor's share at 0.

The smoke ran before the floor's pool share existed, so its floor founders were all ordinary. It showed
the one brain working in the world: every genome central, the jointed bodies' brains growing from 103
links in the first 1,000 s to 121 in the last 1,000, links gained in 365 jointed births and lost in 134,
hidden units gained in 208. It showed about 4 MB a simulated hour on disk, and a crowd that tracked
round 54's seed 2 (2,146 alive at 5,580 s against 1,994). It also showed why the floor draws from the
pool. Of the founders that arrived after the floor closed, 3 of 44 ordinary ones and 3 of 56 from the
pool had a child, against 32 of the 45 floor founders.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| I1 | **the nursery's lines hold some of the tank**: at 30,000 s, at least 5% of the living descend from a nursery founder, in 2 of the 2 inoculated seeds. With half the founders theirs and equal fitness, the share would be near half, but one or two lines took each of round 54's seeds, so luck dominates. I guess this fails, since round 54's winners were rigid leaves and the nursery's bodies are two-part swimmers that feed on the snow | lineage.jsonl, pool/index.tsv |
| I2 | **one recipe wins**: the nursery run whose founders' descendants are the most numerous among the living at 30,000 s is the same run in both inoculated seeds. With four runs, chance alone gives this one time in four. Read only if I1 holds in both | the same |
| I3 | **the world is different**: the jointed share of the living at 30,000 s is at least twice seed 1's in each inoculated seed. Round 54's seeds ended with 62%, 1.0% and 0.2% jointed samples relative to the rigid, so a seed's own luck moves this by far more than twice, and I set the bar there for that reason | the last stats row |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and the run ends on its budget, in 3 of 3, read by round 54's reader | the stats rows, run.json |

## The readings

- **Where the nursery's lines came from:** of the living descended from a nursery founder, the share
  rooted in a floor founder and in a trickle founder, and each run and body's founders, children and
  living at 5,000, 15,000 and 30,000 s.
- **The brains:** the living's mean links and hidden units at the same times, split by the nursery run
  of their root, over the bodies with a joint to drive (a rigid body's brain is empty).
- **Round 54's clauses**, read by round 54's reader (`scripts/reads/r54-read.py` on `round-54`) with
  round 54's same seed as the baseline. They are readings here, not bars: the brain changed, and so
  did the founders.
- **The disk:** genomes and lineage per simulated hour.

## What the round does not ask

Whether a nursery line forages by smell. The nursery's brains do not read the scent (its knockout costs
nothing on their exam), so a line that holds says that a good swimmer's body and brain together prosper
in this world, not that it smells its food. Whether the one brain does better than the old per-node
brains: every seed carries it, so no seed is a control for it. Whether the free first joint (D143)
pays: its nursery arm never ran, and it stays off.

## The reader

The new clauses are read by `scripts/reads/r55-read.py`, built from the smoke's reader
(`scripts/reads/r55-smoke-read.py`). It reads the lineage's founder rows with the pool's
`pool/index.tsv` for each origin, walks every living body to its root founder, and reads the stats rows
for I3 and B1. A founder is a floor founder if it was born before the floor closed at 3,000 s.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree, merged into
`round-55`. Every seed's manifest names it as `gitCommit`, with no DIRTY mark. The build is the branch
`round-55`, launched from `scratch/wt-r55` into the main tree's runs, on the CPU at 8 threads a seed,
two seeds at a time within the owner's limit of half the machine. Seeds 2 and 3 run first, then seed 1.
Before the launch, the fixtures are re-recorded for the floor's new tunable and the suites run on them.
