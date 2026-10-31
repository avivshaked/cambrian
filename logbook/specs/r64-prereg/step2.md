# r64's step 2: D carried on to 48,000 s, with A beside it

*Pre-registration, written by the coordinator on 2026-10-30, after r64's read at 24,000 s (`logbook/specs/r64-read/` on
main) and before this step runs. It launches from `scratch/wt-phase1` at this file's commit, with that worktree's own
Release farm.*

## Why

No arm passed marks 1 to 5 at 24,000 s, so by the pre-registration's rule none is resumed as a winner. D is resumed
anyway, as a new step, because its trend is the one the owner asked to see (D264). Its grazer means doubled every
3,000 s at the end (374, 709, 1,255), and its leaves ended at 0.28 of the control's and falling. The question is whether
D's grazers settle against the leaves, overshoot them, or stall. A goes beside it, since a card group needs two worlds;
it is the health lever alone.

## The worlds

`r64s2-d-6202` and `r64s2-a-6202`, each resumed from its own checkpoint at 24,000 s (`runs/r64-d-6202`, `runs/r64-a-6202`)
for 24,000 s more, to 48,000 s, in one card group on r64's terms (`rounds/launch-r64s2.ps1`). A card resume on these
builds does not repeat the founded run's later steps exactly (D226), so each is a continuation of its world, and is read
as one.

## The marks, for D, read at 48,000 s

The windows are 3,000 s, from 24,000 s on. Grazers, leaves and income are counted as in r64's read.

1. **Grazers boom.** The mean over 45,000-48,000 s is at least 1,956.
2. **Grazers graze.** The leaf share of bite income over 45,000-48,000 s is at least 0.8.
3. **The plants live.** The fewest leaves after 24,000 s are at least 500.
4. **They balance.** Over the last two windows, the grazers' and the leaves' means each lie within a factor of 2 of the
   window before.

D passing 1 to 4 is grazers and plants balancing each other, and becomes the world the next phase builds on. D failing
3, with the leaves under 500, is the overshoot: the levers work, and the next screen dials them back. D failing 1 with
3 holding means the grazers stall below the leaves' limit, and phase 2's senses are the next lever. A is read the same
way, as information.

## Predictions

Mine, written before the step runs, and guesses.

- **D:** grazers pass 1,956 by 30,000 s and peak between 2,500 and 4,000. The leaves fall under 1,500 and then the
  grazers fall back, a swing rather than a settling. Mark 3 holds and mark 4 fails (a guess).
- **A:** grazers stay near 1,000 and the leaves near 2,000. Little changes (moderate).
- **Pace:** two worlds at 1.5 to 2.5 times real time, about 3 to 4.5 hours, slowing as D's grazers grow.

## What would change the plan

r64's rules hold. A world under 0.5 times real time is stopped. If D's leaves go under 500 and its grazers to 0, the step
is stopped early: the overshoot has answered the question.
