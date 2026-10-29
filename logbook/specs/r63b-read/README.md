# r63b's G2 read

The bloom screen pre-registered in `logbook/specs/r63b-prereg/predictions.md` ran eight worlds: arms A to D on seeds 6202
and 6203, one card group from `75d8d2d6`, 24,000 s each, in `runs/r63b-{a..d}-{6202,6203}/2026-10-28-151002-*`. Every
world reached its budget. This folder is its read by `scripts/reads/bloom/g2-read.py`, which
`logbook/specs/bloom-build/g2-read-spec.md` defines. `scripts/reads/bloom/r63b-read.ps1` makes it again from the runs.

- `g2-read.txt` is the read's whole output: every gate per world, the story material and the ranking.
- `traits-r63b-<arm>-<seed>.tsv` are the grazer trait tables gate 3 reads, written by
  `Evosim.Farm --grazer-traits` from the screen's own Release farm.

Arm A has no bloom, so it takes its islands from arm B of its own seed, by `--islands-from ARM=RUN`, because the plan
differs by seed.

## What it says

No arm passes all three gates on both seeds, so nothing is ranked.

| World | Gate 1, survive | Gate 2, bloom reaches food | Gate 3, evolve (largest shift) |
|---|---|---|---|
| a-6202 | fails: 9,000-12,000 s mean 25.9; D222 born share 0.767 | not judged | fails, 0.18 |
| a-6203 | fails: 9,000-12,000 s mean 27.6 | not judged | fails, 0.44 |
| b-6202 | passes | fails: 1 of 8 islands | passes, 1.30 |
| b-6203 | fails: 9,000-12,000 s mean 29.0 | fails: 0 of 8 | fails, 0.75 |
| c-6202 | fails: D222 best cohort 0.99 | fails: 0 of 8 | fails, 0.43 |
| c-6203 | fails: 9,000-12,000 s mean 27.1 | fails: 0 of 8 | fails, 0.76 |
| d-6202 | fails: never 30 grazers (most 23) | fails: 0 of 7 | fails, 0.15 |
| d-6203 | fails: never 30 grazers (most 20) | fails: 0 of 6 | fails, 0.05 |

Gate 2's correlations sit between 0.1 and 0.4 at the best lag, against a bar of 0.5. The bloom arms gather grazers on
5 islands with a crowd correlation of at least 0.3 on every seed, where A has none.

Which way the round goes next is a design fork for the coordinator and the owner. What came of it is in the logbook.
