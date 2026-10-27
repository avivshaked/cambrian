# Growth group 1 at 24,000 s

*The read of `r62gr1`, 2026-10-27, the speed agent. The pre-registration is `logbook/specs/r62gr-prereg/predictions.md`,
and its tests are D222's (`DECISIONS.md`). Run by `scripts/reads/r62gr-read-24000.ps1` on logical 16 to 19 at
BelowNormal while the theatre held, in under four minutes. Every number here is measured from the files beside it
unless it is marked otherwise.*

## The answer

The climb to adulthood does what the group asked of it. In every world, fewer of the bred grazers that died never ate
than in `r62p-t3`, and they lived about three times as long. By D222's tests and the pre-registered rule, **the
winning T is 60 s**, and **the juvenile-strength switch is off in group 2**. `r62gr-t60` is the only one of the four
that passes all three tests. The other three fail test 2 in one window each.

| World | Born share | Best cohort | Chain | Test 2: lowest summer-start share | Test 3 | D222 | Never ate | Median age at death |
|---|---|---|---|---|---|---|---|---|
| `r62gr-t60` | 0.888 | 1.13 | 22 | 1.28 | 0.901 | **passes** | 1,110 of 2,972, 0.373 | 63.8 s |
| `r62gr-t120` | 0.838 | 1.21 | 18 | **0.71**, at 11,750 s | 0.900 | fails test 2 | 870 of 2,632, 0.331 | 72.0 s |
| `r62gr-t240` | 0.894 | 1.03 | 14 | **0.51**, at 5,750 s | 0.895 | fails test 2 | 896 of 2,316, 0.387 | 74.5 s |
| `r62gr-t120s` | 0.854 | 1.21 | 21 | **0.69**, at 5,750 s | 0.893 | fails test 2 | 854 of 2,410, 0.354 | 72.0 s |
| `r62p-t3` (control) | 0.841 | 1.09 | 14 | 1.04 | holds | passes | 0.51 | 26.5 s |

The columns:
- **Born share** is test 1's share of living grazers born in the world (`r62-health.txt`).
- **Best cohort** is the highest mouthed children a body over the 3,000 s cohorts (`r62g-cohorts.txt`).
- **Chain** is the deepest mouthed chain (`r62g-read.txt`).
- **Test 2** is the lowest share of upkeep met in the 500 s windows at the start of each summer, at 5,750, 11,750,
  17,750 and 23,750 s (`r62g-windows.txt`, the rows marked `summer*`). The bar is 0.9. Grazers are alive at every
  season's end after the founders arrive in all four worlds, as in `r62p-t3`.
- **Test 3** is the living share of kept intake in 21,000 to 24,000 s, against a bar of 0.5 (`r62-health.txt`).
- **Never ate** counts bred grazers that died with no kept meal and no kill (`grazer-tally-*.txt`).
- **The control's row** comes from `logbook/specs/r62p-read/24000-t3/`. Its tally predates the never-ate column, so
  its 0.51 is the pre-registration's measurement.

## The pre-registered rules, applied

**The winning T.** Among `r62gr-t60`, `r62gr-t120` and `r62gr-t240`, only `r62gr-t60` passes D222's three tests, so
it wins outright. The never-ate tie-break does not come into play. Had test 2 held everywhere, the rule would still
have given 60 s: `r62gr-t120` had the lowest share at 0.331, and `r62gr-t60`'s 0.373 is within 0.05 of it, so the
shorter T wins.

**The strength arm.** The switch is on only if `r62gr-t120s` passes D222 and its never-ate share is at most 0.05
above `r62gr-t120`'s. The share is 0.023 above, but the world fails test 2, so the switch is off. The
pre-registration predicted the switch off for a different reason, a share 0.05 to 0.15 higher. That part of the
prediction was wrong. A juvenile at an adult's strength and one at its own size's strength starve about equally
often in this world.

**What test 2 rests on** (inference). Each failing window holds 12 to 17 grazers. The 5,750 s window is the first
summer with grazers in it. The 11,750 s window follows `r62gr-t120`'s low winter. So each failure is a dozen bodies'
upkeep at a turn of the seasons. It is a failure by the test's wording. One seed cannot say whether 120 s and 240 s
are worse at that turn, or whether this seed drew it. Every summer start after 12,000 s holds at 1.4 or more in all
four worlds.

## Beside the tests

- **The economy.** Intake over upkeep is 1.61, 1.68, 1.60 and 1.47 in `r62gr-t60`, `-t120`, `-t240` and `-t120s`,
  against `r62p-t3`'s 1.54. The grazer-seconds lived are 1.03, 1.04, 0.89 and 0.96 million, against 0.64 million
  (`r62g-read.txt`).
- **The overshoot.** Against `r62cs-w500-6201`'s, the leaf stand over the 3,000 s bins runs as follows
  (`overshoot-*.txt`):
  - 0.74 to 1.07 in `r62gr-t60`;
  - 0.64 to 0.82 in `r62gr-t120`;
  - 0.45 to 0.59 in `r62gr-t240`;
  - 0.60 to 0.82 in `r62gr-t120s`.

  It falls below half once, in `r62gr-t240` at 12,000 s. There the leaves' largest cause of death is age, so the
  grazers do not lead the fall, and the three marks do not hold together. No world overshoots. `r62gr-t240`'s stand
  sits low from the first bin, as the pre-registration expected of leaves that climb for 379 s.
- **The islands.** 0.63, 0.66, 0.72 and 0.66 of the living are on the islands at 24,000 s, all above 0.6
  (`r60-read-3000.txt`).
- **The gene.** The growth reserve G's median in bred grazers stays at its start of 30 s in every 3,000 s bin of
  every world. Its lower quartile drifts to 22 s only in `r62gr-t120`'s last bin (`grs-drift.txt`, by
  `scripts/reads/grs-drift-read.py`). That is within the pre-registration's factor of 2. At a mutation chance of 0.08 and about 20 generations, G has not moved
  (inference: the selection on it is weak, or too slow for 24,000 s).
- **The cost.** No world diverged. Applying growth took 0.42 to 0.50 % of each world's wall (`grs-drift.txt`).

## Against the predictions

| Prediction | Outcome |
|---|---|
| Fewer bred grazers never eat than in `r62p-t3` | Held, in all four: 0.33 to 0.39 against 0.51 |
| `r62gr-t60` and `r62gr-t120` pass D222, born shares above 0.84 | `r62gr-t60` passed at 0.888. `r62gr-t120` failed test 2, and its born share was 0.838 |
| `r62gr-t240` passes test 1 and is likeliest to fail test 2 | Held |
| The winning T is 120 s | Wrong: 60 s |
| The strength arm's never-ate share is 0.05 to 0.15 higher, so the switch is off | The switch is off, but because of test 2. The share was 0.023 higher |
| G's median stays within a factor of 2 of 30 s | Held: it did not move |

## What follows

Group 2 runs at T = 60 s with the switch off, screening G's starting value at 1, 30 and 60 s, as
`logbook/specs/growth-runway-design.md` plans. Test 2's thin summer-start windows are worth a second seed before any
T is ruled out for good (inference).
