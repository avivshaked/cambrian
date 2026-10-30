# r63d at 48,000 s

The three B worlds of `r63d` (`logbook/specs/r63d-prereg/predictions.md` on `scratch/wt-r63b`, D259), read with
`scripts/reads/bloom/g2-read.py --survive-from 12000 --end 48000` on 2026-10-30 at about 06:40 UTC, while the group ran
on toward 72,000 s. Each world is read from 0 s through its resume. The output is `g2-read.txt` beside this file. Gate 3
needs the trait tables that `Evosim.Farm --grazer-traits` writes, so it is judged at 72,000 s only, when the card is free.

| World | Gate 1, grazers survive | Gate 2, islands following the bloom | Crossings | Crowd islands | Recoveries |
|---|---|---|---|---|---|
| `r63d-b-6202` (from `r63c-b-6202`) | PASS | 0 of 8 | 122 | 8 | 4 |
| `r63d-b-6203` (from `r63c-b-6203`) | PASS | 1 of 8 | 147 | 7 | 5 |
| `r63d-b-6204-s2` (from step 1) | PASS | 0 of 8 | 322 | 5 | 6 |

- **Gate 1 passes in all three.** The sensitivity at 40 fails on 6203 and 6204-s2 only, in the 12,000-18,000 s windows
  (means of 34.6 to 37.6), and is not part of the gate.
- **Gate 2 fails in all three, and by more than at 24,000 s.** There `r63c`'s B had 4 and 5 of 8 islands at r 0.5 or
  more (`logbook/specs/r63c-read/`). Over 6,000 to 48,000 s the best-lag correlations are 0.0 to 0.38, with one island at
  0.503. The pre-registration predicted more islands, not fewer, and that prediction failed.
- **Why, as inference not yet checked:** the worlds filled up. They held about 9,000 bodies at 24,000 s and 10,000 to
  17,000 by 30,000-33,000 s, mostly leaves. A leaf crowd that grows through the whole span gives each island's leaf area
  a trend that the bloom's cycle does not have, and a plain correlation over the span reads the trend as noise. A
  detrended or per-cycle correlation would test this. It is not the pre-registered measure, so it could explain the
  verdict but not change it.
- **The story material.** Crossings between islands run from 122 to 322 over 48,000 s. That bears on the owner's aim
  of swimmers that travel between oases. The 24,000 s counts were not compared here.

By the pre-registration, a gate-2 failure at 72,000 s sends the question of what the bloom is for in the film back to
the owner. This read at 48,000 s decides nothing.
