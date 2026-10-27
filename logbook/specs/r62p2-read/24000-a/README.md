# Pace group 2, part a, at 24,000 s

*The read of `r62p2a`, 2026-10-27, the speed agent. The pre-registration is `logbook/specs/r62p2-prereg/predictions.md`,
and its tests are D222's (`DECISIONS.md`). Run by `scripts/reads/screen-read-24000.ps1` and
`scripts/reads/grazer-motion-read.py` on logical 16 to 19 at BelowNormal while the theatre held. Part b, `r62p-k3w9`,
has not run, so the group's comparison waits for it. Every number here is measured from the files beside it unless it
is marked otherwise.*

## The answer

**Speed carried the thrust arm, not cost.** `r62p-w9` has today's thrust and clock at a ninth of the work price, and it
fails D222 as badly as any world in the screen. Its grazers swim at the base's 0.07 m a world second, and its intake
over upkeep is 0.72. `r62p-t16` pushes 16 times harder at a sixteenth of the price. It passes all three tests, at an
intake over upkeep of 1.86, above `r62p-t3`'s 1.54. Its grazers swim about 1.7 times as fast as `r62p-t3`'s. Neither
world overshoots.

| World | Born share | Best cohort | Chain | Test 2: lowest summer-start share | Test 3 | D222 | Intake over upkeep | Never ate | Median age at death |
|---|---|---|---|---|---|---|---|---|---|
| `r62p-w9` | **0.077** | **0.82** | **4** | **0.56** | 0.829 | fails 1 and 2 | 0.72 | 305 of 511, 0.60 | 25.0 s |
| `r62p-t16` | 0.863 | 1.09 | 24 | 1.02 | 0.875 | **passes** | 1.86 | 1,882 of 4,721, 0.40 | 29.0 s |
| `r62p-t3` (group 1) | 0.841 | 1.09 | 14 | 1.04 | holds | passes | 1.54 | 0.51 | 26.5 s |
| `r62p-k3` (group 1) | 0.50 | 0.83 | 4 | | | fails 1 | 1.11 | | |

The columns are those of `logbook/specs/r62gr-read/24000/README.md`. Test 1 asks for a born share of at least 0.80, a
cohort's replacement of at least 1 and a chain of at least 10. Test 2 asks for at least 0.9 of upkeep in the 500 s
windows at the start of each summer. Test 3 asks for a living share of at least 0.5 in the last bin. Group 1's rows
are from `logbook/specs/r62p-read/24000-t3/` and `24000-k3/`.

## Beside the tests

- **The grazers' speed** (`grazer-motion.txt`, the median over the frame pairs in each 3,000 s bin):
  - `r62p-w9`: 0.068 to 0.073 m a world second from 9,000 s on.
  - `r62p-t16`: 0.18 to 0.26.
  - `r62p-t3`, in group 1's motion read: 0.11 to 0.14 over 12,000 to 18,000 s.
  - The base at k = 1, also from group 1's motion read: 0.07.
- **What the grazers eat.** In `r62p-w9`, grazers kill only 1 to 4 % of the leaves that die in any bin after 6,000 s.
  The leaf stand runs at 0.88 to 1.19 of `r62cs-w500-6201`'s, so the leaves barely notice them. In `r62p-t16` the
  grazers' share of leaf deaths rises from 0.18 at 9,000 s to 0.35 at 24,000 s, and the stand falls from 0.80-0.93 of
  the control's to 0.71 (`overshoot-*.txt`). Kills per grazer-second are 0.0066 and 0.023 (`r62g-read.txt`).
- **The overshoot.** The leaf stand is never below half the control's in either world, and the leaves' largest cause
  of death is age in every bin after 3,000 s. Neither world overshoots.
- **The islands.** 0.63 of the living are on the islands at 24,000 s in `r62p-w9`, and 0.50 in `r62p-t16`
  (`r60-read-3000.txt`). `r62p-t16`'s is the lowest in the screen so far. `r62p-t3` ended at 0.57, `r62p-k3` and
  `r62p-k3n` at 0.61, and growth group 1's worlds at 0.63 to 0.72. The faster the grazers, the lower the share runs.
  Inference: fast grazers spend more of their time in open water between the oases. That is the
  migration the ocean is meant to have, and it also puts the overshoot's guard below its 0.6 mark at the end.

## Against the predictions

| Prediction | Outcome |
|---|---|
| `r62p-w9`'s intake over upkeep is above `r62p-k3`'s 1.11 | Wrong: 0.72. The cheap stroke does not feed a grazer that cannot reach its food |
| `r62p-w9` fails test 1 | Held, at a born share of 0.077 |
| `r62p-w9`'s grazers swim at the base's 0.07 m a world second | Held: 0.068 to 0.073 |
| `r62p-t16`'s grazers swim no more than 1.3 times `r62p-t3`'s | Wrong: about 1.7 times. The limiters do not bind at a push of 9 |
| `r62p-t16` passes D222 | Held |
| `r62p-t16`'s intake over upkeep is within a fifth of `r62p-t3`'s | Wrong, narrowly: 1.86 is 21 % above 1.54 |
| The overshoot comes first in `r62p-t16`, if at all | It did not come |

## What follows

By the pre-registration's own rule, `r62p-w9` does not match `r62p-t3`, so cost alone did not carry the thrust arm.
The group's remaining question was whether the pace factor at 3 does as well once its strokes are cheap. That was part
b's, `r62p-k3w9`. The owner ruled on 2026-10-27, on the coordinator's recommendation, to drop part b: `r62p-t16`'s
strong pass answered the pace question, and the pace factor at 3 can be revisited later. So part b was
pre-registered and not run, and this read is the group's last. The thrust arm, not the pace factor, carries the
screens on.
`r62p-t16`'s push of 16 is the strongest world in the record so far, and it has not overshot. So a push past it is
still open within the owner's delegation of 2026-10-24 to overshoot and roll back (inference).
