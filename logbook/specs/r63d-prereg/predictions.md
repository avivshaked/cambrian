# The film's world run long: does the slow bloom reach the food over 48,000 s?

*Pre-registration, written by the coordinator on 2026-10-29 before any world of it ran. The owner chose `r63c`'s arm B
as the film's world on 2026-10-29 (D259), and asked for B's worlds run long. The screen launches from
`scratch/wt-r63b` at this file's commit, with that worktree's own Release farm.*

## The question

`r63c` judged its gates over 2.4 to 4.8 bloom cycles per island, and B's leaves followed the bloom on 4 and 5 of 8
islands (`logbook/specs/r63c-read/`). Over twice the cycles, do they follow on every island? And does evolution show on
both seeds?

## The worlds

`r63c-b-6202` and `r63c-b-6203`, each resumed from its own checkpoint at 24,000 s and run to 48,000 s, in one card
group named `r63d`. A resume replays exactly, so each is one world of 48,000 s.

- **Everything is `r63c`'s B**, read from the sources' configs: periods of 5,000 to 10,000 s, the far smell off and
  D249's energy sense on.
- **The build** is `r63c`'s: `scratch/wt-r63b` with this file's prose and launcher added. The farm hash `d53ec6ee6080`
  and the Dynamics hash `ddb068fd624c` must reappear in the manifests.
- **The launcher** is `rounds/launch-r63d.ps1`: group 32, the snow's transport on the card, checkpoints every 1,500 s,
  the heat stop on (D258), and logical 8 to 15 at 8 threads, since the nose's nursery tests hold 16 to 31.

## The read and the gates

`scripts/reads/bloom/g2-read.py` with `--survive-from 12000 --end 48000`, in windows of 3,000 s from 6,000 s to 48,000 s.
Each world is read from 0 s through its resume.
The gates are `r63c`'s, and an arm passes when it passes on both seeds.

| Gate | Pass |
|---|---|
| 1. Grazers survive | every window from 12,000 s holds a mean of at least 30 grazers and never 0; D222 passes |
| 2. The bloom reaches the food | every judged island's leaf area follows its influx, r at least 0.5 at the best lag |
| 3. Grazers evolve | one trait shifts by at least one scale unit between the first and last windows |

If gate 2 fails, the owner's word on what the bloom is for in the film is asked again. B stays the film's world
unless the owner rules otherwise. This file decides nothing about the film's world.

## Predictions

These are the coordinator's, written before any run, and they are guesses.

- **Gate 2.** More islands follow than at 24,000 s in both worlds, but not every island in either. The islands whose
  leaves stay near zero (3.6 m² of leaf in `r63c-b-6202`'s island 2) are noisy at any length (moderate confidence).
- **Gate 1.** Passes in both worlds (moderate confidence).
- **Gate 3.** Passes in both worlds: 6202's largest shift was 0.23 at 24,000 s, and twice the time doubles the
  generations (guess).
- **Pace.** The worlds start near 1.4x real time and fall below 1x before 48,000 s as their crowds grow, so the run
  takes about 6 to 8 h (`logbook/specs/slowdown/`, guess).

## What would change the plan

- **The hashes differ from `r63c`'s.** Stop: the build is not the one this file names.
- **A world falls far below real time.** It is stopped once it has answered the question, by the owner's rule of
  2026-10-19.
