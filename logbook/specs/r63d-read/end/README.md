# r63d at its end

The three B worlds of `r63d` (`logbook/specs/r63d-prereg/predictions.md` on `scratch/wt-r63b`, D259), read on
2026-10-30 between 11:36 and 11:55 UTC by `scripts/reads/bloom/r63d-end-read.ps1`. None reached 72,000 s. Each ended
RUNAWAY at the population ceiling of 25,000 bodies, so each is read to the second it ended, as the pre-registration
reads a world stopped early. Each world is read from 0 s through its resume, by `g2-read.py --survive-from 12000`,
one world per call. The outputs are `g2-read-<arm>.txt` beside this file. The trait tables are 3.8 to 6.8 MB each, too
large to commit, and the script remakes them from the runs.

| World | Ended at | Gate 1, grazers survive | Gate 2, islands following the bloom | Gate 3, largest shift | Crossings | Crowd islands | Recoveries |
|---|---|---|---|---|---|---|---|
| `r63d-b-6202` | 67,240 s | PASS | 0 of 8 | mass, 3.96 units | 337 | 7 of 11 | 6 |
| `r63d-b-6203` | 54,440 s | PASS | 0 of 8 | mass, 7.54 units | 250 | 7 of 11 | 5 |
| `r63d-b-6204-s2` | 55,260 s | PASS | 0 of 8 | parts, 1.62 units | 551 | 6 of 11 | 6 |

The arm passes a gate when it passes on all three seeds. **B passes gates 1 and 3 and fails gate 2.**

- **Gate 1 passes in all three.** The floor at 40, a sensitivity and not the gate, fails on 6203 and 6204-s2 in their
  early windows, as it did at 48,000 s.
- **Gate 2 fails in all three, with no island at r 0.5 in any world.** The best-lag correlations of leaf area with the
  bloom run from -0.01 to 0.25 in 6202. At 24,000 s `r63c`'s B had 4 and 5 of 8 (`logbook/specs/r63c-read/`), and at
  48,000 s these worlds had 0, 1 and 0 (`../48000/`). The longer the run, the fewer islands follow.
- **Why, as inference:** the leaves grew without a check until the worlds reached the ceiling. A leaf area that climbs
  through the whole span has a trend the bloom's cycle lacks, and a plain correlation over the span reads it as noise.
  The worlds' end says the same from another side: the farm's ending reads that light covers upkeep so completely that
  nothing has to do anything. The bloom is a ripple on a flood. A detrended correlation would test this, but it is not
  the pre-registered measure and could not change the verdict.
- **Gate 3 passes in all three,** from the first window (6,000-9,000 s, about 365 grazers born) to the last full one. The
  largest shift is body mass in two worlds, about double the founders' mean, with its spread growing eight times over.
  The number of parts shifts by 1.2 to 1.8 units in all three. Whether mass is selected or drifts as the crowd grows is
  not something this read can tell (inference).
- **The grazers follow the bloom even where the leaves do not.** In 6 or 7 of 11 islands per world, the crowd of grazers
  in a grove correlates with the bloom at r 0.3 or more, lagging it by about 550 to 2,300 s. The grazers feel the
  bloom's pulse that the leaf totals hide (inference: the correlation is the read's, the reason is mine).
- **The story material.** Crossings between islands at a gap of 10 m run from 250 to 551, rising in the later windows.
  6202 has six busts, each a fall of the grazers to a third to a half of their peak, and a recovery from each. The last
  took 13,100 s.
- **The books.** The audit residual stays under 0.1 J in all three. The matter residual peaks at 1.7e-5 and 2.3e-5
  units in 6203 and 6204-s2, over the launcher's 1e-5 line, against 8.6e-6 in 6202. `r62ce` read 2.77e-5 as rounding,
  under its stop line of 2.54e-4 (`logbook/specs/r62ce-read/`), and these worlds hold 25,000 bodies. So this is rounding
  at scale (inference from the scale and the precedent).

## The predictions against the result

| Prediction | Result |
|---|---|
| Gate 2: more islands follow than at 24,000 s, but not every island (moderate) | Wrong: fewer, none in any world |
| Gate 1: passes in all three (moderate) | Right |
| Gate 3: passes in all three by 72,000 s (guess) | Right, by each world's end |
| Pace: step 2 takes 15 to 20 h | Ended at 10.8 h, at the ceiling, before 72,000 s |

By the pre-registration, a gate-2 failure at the end sends the question of what the bloom is for in the film back to
the owner. B stays the film's world unless the owner rules otherwise.
