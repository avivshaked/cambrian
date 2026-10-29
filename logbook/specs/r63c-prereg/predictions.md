# The bloom rerun: do slower cycles let the leaves follow the bloom?

*Pre-registration, written by the coordinator on 2026-10-29 before any world of it ran. It is G2's fallback in the
grazer film's plan (`logbook/specs/grazer-film-plan.md`, D254), taken because `r63b` failed gate 2 in every bloom arm
(`logbook/specs/r63b-read/`), with the survival floor as D256 amends it. The screen launches from `scratch/wt-r63b` at
this file's commit, with that worktree's own Release farm, after HOLD is lifted and the heat watch is armed.*

## The question

Does each island's leaf area rise and fall with its influx when the cycles run two to three times slower? `r63b`'s
periods of 2,500 to 6,000 s gave correlations of 0.1 to 0.4 at the best lag, against a bar of 0.5. Producer births
followed the bloom on 4 or 5 of 8 islands, but that check was added after the run.

## The worlds

Two arms on seeds `6202` and `6203`, 24,000 world seconds each, four worlds in one card group.

| Arm | Bloom cycles | Far smell |
|---|---|---|
| B | periods 5,000 to 10,000 s | off |
| C | periods 5,000 to 10,000 s | on |

- **Everything else is `r63b`'s.** The arms are `rounds/env-r63c-b.ps1` and `-c.ps1`, each `r63b`'s arm with the two
  period bounds changed. The amplitude is 0.8 to 1.0, the phase uniform, and the world's total influx held. The far smell
  is G = 0.005, B = 10 m, R = 30 m. D249's energy sense is on.
- **Fat's arm D is not rerun** (D256).
- **The controls are already run.** Arm A on these seeds would replay `r63b`'s A bit for bit. `r62cf` on these seeds is A
  without the energy sense, since `r63b`'s switch-off check stepped it identically to 600 s. Both are read beside the
  new arms.
- **The build** is `r63b`'s, unchanged: `r63b-build` at `75d8d2d6` plus this pre-registration's prose and launchers.
  The farm hash `d53ec6ee6080` and Dynamics hash `ddb068fd624c` must reappear in the manifests.
- **The launcher** is `rounds/launch-r63c.ps1`: one card group named `r63c` at group 32, the snow's transport on the
  card, checkpoints every 1,500 s, the heat stop on, cores 22 to 31 at 10 threads.

## The read and the gates

The read is `scripts/reads/bloom/g2-read.py`, as `logbook/specs/bloom-build/g2-read-spec.md` defines it, run with
`--survive-from 12000`. Arm A's islands come from arm B of the same seed, by `--islands-from`.

| Gate | Pass |
|---|---|
| 1. Grazers survive | every window from 12,000 s holds a mean of at least 30 grazers and never 0 (D256); D222 passes by `r62cf`'s recorded procedure |
| 2. The bloom reaches the food | every judged island's leaf area follows its influx factor, with a correlation of at least 0.5 at the best lag |
| 3. Grazers evolve | one trait shifts by at least one scale unit between the first and last windows, from 6,000 s |

An arm passes when it passes on both seeds. Passing arms are ranked by `r63b`'s four parts of story material, ties going
to the simpler arm. `r63b` read again with the floor from 12,000 s, with `r62cf` beside it, is `r63b-from12k.txt` beside this file. There every
floor passes. D222 fails in A-6202 (born share 0.767 against 0.8) and C-6202 (best cohort 0.99 against 1). Gate 3
passes only in B-6202 (a shift of 1.30).

**If no arm passes every gate,** the choice of the film's world goes to the owner with the coordinator's recommendation.
It is not made by this file. `r63b`'s pre-registration said that if every arm fails gate 3, evolution is too slow to show
in 24,000 s, and G7's 48,000 s round is the place to look. That case is now the likely one.

## Predictions

These are the coordinator's, written before any run, and they are guesses.

- **Gate 2.** Passes on at least half the judged islands in each world, and on every judged island in at most one world.
  The leaves lagged the bloom by hundreds of seconds in `r63b`'s post hoc read, and a longer period leaves more of each
  cycle above that lag. But at 24,000 s a world sees only 2.4 to 4.8 cycles of each island, so a correlation over so
  few cycles is noisy (moderate confidence). The likeliest result is that gate 2 still fails in every world, on one or
  two islands each.
- **Gate 1.** The floor passes in every world, as it did in `r63b` from 12,000 s. D222 fails in at least one of the four
  worlds on a near miss, as it did in two of `r63b`'s six (guess).
- **Gate 3.** Fails in at least three of the four worlds, as it did in five of `r63b`'s six (moderate confidence).
- **Story.** C has more crossings than B, as in `r63b` (15 to 17 against 8 to 9), and both arms have crowds on 4 to 6
  islands.

## What would change the plan

- **The hashes differ from `r63b`'s.** Stop: the build is not the one this file names.
- **Gate 2 passes in both arms on both seeds.** The slower cycle is the bloom's period for G7.
- **Gate 2 fails in every world on most islands.** The leaves do not follow a bloom at any period this world allows,
  and the bloom stays in the film only for its crowds and crossings, on the owner's word.
