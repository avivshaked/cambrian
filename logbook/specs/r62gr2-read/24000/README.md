# Growth group 2 at 24,000 s

*The read of `r62gr2`, 2026-10-27, the speed agent. The pre-registration is `logbook/specs/r62gr2-prereg/predictions.md`,
and its tests are D222's (`DECISIONS.md`). Run by `scripts/reads/screen-read-24000.ps1 -Gene` on logical 16 to 19 at
BelowNormal while the theatre held. The start of 30 s on seed 6201 is growth group 1's `r62gr-t60-6201`
(`logbook/specs/r62gr-read/24000/`). Every number here is measured from the files beside it unless it is marked
otherwise.*

## The answer

**By the pre-registered rules, G's start stays at 30 s, and T = 60 s stands on two seeds.** Neither new start passes
D222 on seed 6201, so 30 s stands. On seed 6202, both T = 60 s and T = 120 s pass, and their never-ate shares
are within 0.006 of each other, so T = 60 s stands. The growth settings the screens carry on are T = 60 s, G starting
at 30 s, and the juvenile-strength switch off.

| World | Born share | Best cohort | Chain | Test 2: lowest summer-start share | Test 3 | D222 | Never ate | Median age at death | Intake over upkeep |
|---|---|---|---|---|---|---|---|---|---|
| `r62gr2-g1-6201`, G at 1 s | **0.772** | 1.11 | 11 | **0.82**, at 5,750 s | 0.888 | fails 1 and 2 | 934 of 2,255, 0.414 | 44.0 s | 1.49 |
| `r62gr-t60-6201`, G at 30 s (group 1) | 0.888 | 1.13 | 22 | 1.28 | 0.901 | passes | 0.373 | 63.8 s | 1.61 |
| `r62gr2-g60-6201`, G at 60 s | 0.906 | 1.12 | 15 | **0.74**, at 11,750 s | 0.871 | fails 2 | 831 of 2,581, 0.322 | 97.0 s | 1.49 |
| `r62gr-t60-6202`, T = 60 s | 0.949 | 1.12 | 30 | 0.902, at 5,750 s | 0.901 | passes | 1,716 of 5,220, 0.329 | 59.5 s | 2.08 |
| `r62gr-t120-6202`, T = 120 s | 0.925 | 1.01 | 20 | 0.99, at 11,750 s | 0.884 | passes | 1,179 of 3,647, 0.323 | 72.5 s | 1.66 |

The columns are those of `logbook/specs/r62gr-read/24000/README.md`. `r62gr-t60-6202` meets test 2 by a hair. Its
lowest window kept 0.819 W a grazer against an upkeep of 0.908 W, which is 0.902 of it (`r62g-windows.txt`, read to
three places).

## The pre-registered rules, applied

**G's start.** `r62gr2-g1` fails tests 1 and 2, and `r62gr2-g60` fails test 2, so neither new start passes D222 and
30 s stands. `r62gr2-g60`'s never-ate share of 0.322 is 0.051 below 30 s's 0.373. Had it passed, it would have won by
0.001 over the 0.05 margin.

**The second seed.** `r62gr-t60-6202` passes D222, and `r62gr-t120-6202` passes too, with a never-ate share 0.006
below it. That is far inside the 0.05 that would reopen T = 120 s, so T = 60 s stands. Group 1's single failing window
for `r62gr-t120-6201` did not repeat on seed 6202.

## Beside the tests

- **The start's effect is direct.** A larger start gives a longer life before a first meal: the median age at death
  is 44, 64 and 97 s at starts of 1, 30 and 60 s, and the never-ate share is 0.41, 0.37 and 0.32. The runway table
  in `logbook/specs/growth-runway-design.md` predicted 35, 43.5 and 59.5 s unfed (derived). So the measured lives run
  longer than the unfed runway, and in the same order.
- **The gene does not move.** G's median in bred grazers is 1.00, 60.00, 30.00 and 29.09 s in the 21,000 to
  24,000 s bin, the start of each world to within a few percent (`grs-drift.txt`). The starts do not converge: by the
  pre-registered measure, the medians on seed 6201 lie a factor of 60 apart. This is the second group in which G
  holds still. Inference: at a mutation chance of 0.08 and a step of 15 % on the logarithm, about 20 generations
  move it by little, and the selection on it is too weak to show in 24,000 s.
- **Test 2's failures.** Across the eight growth worlds of both groups, the four that failed test 2 each failed it
  in one window at the turn into the first or second summer, at 5,750 or 11,750 s, holding 9 to 19 grazers. No world
  failed a summer start after 12,000 s. Inference: test 2's early windows weigh a dozen founders' luck against the
  0.9 bar.
- **The overshoot.** No world's leaf stand falls below half of `r62cs-w500`'s on its own seed in any bin
  (`overshoot-*.txt`). `r62gr-t60-6202`'s grazers are the leaves' largest cause of death in one bin, but its stand
  stays at 0.89 or more of the control's. No world overshoots.
- **The islands.** 0.56, 0.56, 0.48 and 0.54 of the living are on the islands at 24,000 s (`r60-read-3000.txt`),
  below the overshoot's 0.6 guard in every world. Seed 6202's worlds are the most spread.
- **The cost.** No world diverged, and applying growth took 0.48 to 0.71 % of each world's wall.

## Against the predictions

| Prediction | Outcome |
|---|---|
| Never ate: 0.42 at a start of 1 s, 0.33 at 60 s | Held: 0.41 and 0.32 |
| `r62gr2-g60` passes D222, `r62gr2-g1` is likelier to fail test 2 | Half held: `r62gr2-g1` failed tests 1 and 2, but `r62gr2-g60` failed test 2 too |
| G's start stays at 30 s | Held, though because both new starts failed D222, not on the never-ate margin |
| G does not converge; each median stays within a fifth of its start | Held |
| Both T pass on seed 6202 within 0.05, so T = 60 s stands | Held, at 0.006 apart |
| No world overshoots | Held |

## What follows

Growth is settled for the screens that follow: T = 60 s, G starting at 30 s, the switch off. The structural mutation
screen (`logbook/specs/r62sm-prereg/predictions.md`) takes these settings.
