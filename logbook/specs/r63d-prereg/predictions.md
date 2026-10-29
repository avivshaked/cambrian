# The film's world run long: does the slow bloom reach the food over 72,000 s?

*Pre-registration, written by the coordinator on 2026-10-29 before any world of it ran. The owner chose `r63c`'s arm B
as the film's world on 2026-10-29 (D259), and asked for three B worlds run to 72,000 s, or longer if they are
doing something worth watching. The screen launches from
`scratch/wt-r63b` at this file's commit, with that worktree's own Release farm.*

## The question

`r63c` judged its gates over 2.4 to 4.8 bloom cycles per island, and B's leaves followed the bloom on 4 and 5 of 8
islands (`logbook/specs/r63c-read/`). Over twice and three times the cycles, do they follow on every island? And does
evolution show on every seed?

## The worlds

Three worlds of B in two steps, each step one card group:
1. **`r63d-b-6204`** is founded from `rounds/env-r63c-b.ps1` on seed 6204 and run to 24,000 s
   (`rounds/launch-r63d-a.ps1`, group `r63d-a`), so that all three worlds stand at the same second.
2. **`r63d-b-6202`, `-6203` and `-6204`** resume from `r63c-b-6202`, `r63c-b-6203` and step 1's world at 24,000 s and
   run to 72,000 s (`rounds/launch-r63d.ps1`, group `r63d`). A resume replays exactly, so each is one world.

- **Everything is `r63c`'s B**, read from the sources' configs: periods of 5,000 to 10,000 s, the far smell off and
  D249's energy sense on.
- **The build** is `r63c`'s: `scratch/wt-r63b` with this file's prose and launcher added. The farm hash `d53ec6ee6080`
  and the Dynamics hash `ddb068fd624c` must reappear in the manifests.
- **The launchers** run at group 32, the snow's transport on the card, checkpoints every 1,500 s,
  the heat stop on (D258), and logical 8 to 15 at 8 threads, since the nose's nursery tests hold 16 to 31.

## The read and the gates

`scripts/reads/bloom/g2-read.py` with `--survive-from 12000`, at `--end 48000` and again at `--end 72000`, in windows of 3,000 s from 6,000 s.
Each world is read from 0 s through its resume.
The gates are `r63c`'s, and the arm passes when it passes on all three seeds.

| Gate | Pass |
|---|---|
| 1. Grazers survive | every window from 12,000 s holds a mean of at least 30 grazers and never 0; D222 passes |
| 2. The bloom reaches the food | every judged island's leaf area follows its influx, r at least 0.5 at the best lag |
| 3. Grazers evolve | one trait shifts by at least one scale unit between the first and last windows |

If gate 2 fails at 72,000 s, the owner's word on what the bloom is for in the film is asked again. B stays the film's world
unless the owner rules otherwise. This file decides nothing about the film's world.

## Predictions

These are the coordinator's, written before any run, and they are guesses.

- **Gate 2.** More islands follow than at 24,000 s in every world, but not every island in any world, at 48,000 s or at
  72,000 s. The islands whose leaves stay near zero, such as the 3.6 m² of `r63c-b-6202`'s island 2, are noisy at any
  length (moderate confidence).
- **Gate 1.** Passes in all three worlds (moderate confidence).
- **Gate 3.** Passes in all three by 72,000 s, since three times the time triples the generations (guess).
- **Pace.** Step 1, one world alone, runs at about 3x real time. Step 2's three worlds start near 1.4x and fall below
  1x as their crowds grow, so step 2 takes about 15 to 20 h (`logbook/specs/slowdown/`, guess).

## What would change the plan

- **The hashes differ from `r63c`'s.** Stop: the build is not the one this file names.
- **A world falls under 0.5x real time.** It is stopped at its last checkpoint (the owner, 2026-10-29), and the read
  uses the seconds it reached. Each look reports the pace. When the nose's nursery tests end, the group may resume at
  more threads, since a resume replays bit for bit at any thread count.
- **Something worth watching is still happening at 72,000 s.** The worlds resume from their last checkpoint for
  more, as a new pre-registered step.
