# The structural mutation screen at 24,000 s: the rate is not the lever

*2026-10-27, the speed agent. The four worlds of `logbook/specs/r62sm-prereg/predictions.md` on seed 6201, T16's world
with growth at T = 60 s and G starting at 30 s, with every bound structural chance scaled by S. They ran as one card
group on logical 22 to 31 at 10 threads, from 11:09 to 14:56 UTC, and all four reached 24,000 s. The screen read is
`scripts/reads/screen-read-24000.ps1 -Gene`, whose outputs sit beside this file. The newborn reads are in `newborn/`,
and the part counts are in `part-count.txt` (`scripts/reads/part-count-read.py`).*

## The rule's answer: today's rate stands

The pre-registered rule takes the highest S that passes D222 and whose slow share is at least 0.04 below today's arm.
Today's arm has a slow share of 0.198, so an arm must reach 0.158 or less. None does. The quarter-rate arm comes
closest at 0.164, 0.034 below. **By the rule, today's structural rate stands, the rate is recorded as not the lever, and
the next screen is fitting the brain at birth.** The rates are the owner's, so this goes to the owner as a proposal.

| World | S | D222 | Slow share | Slow, plan changed | Slow, same plan | Share of births whose plan changed |
|---|---|---|---|---|---|---|
| `r62sm-s100-6201` | 1 | fails test 2 | 640 of 3,231, 0.198 | 0.276 | 0.109 | 0.533 |
| `r62sm-s50-6201` | 0.5 | passes | 633 of 2,998, 0.211 | 0.290 | 0.151 | 0.431 |
| `r62sm-s25-6201` | 0.25 | passes | 628 of 3,830, 0.164 | 0.247 | 0.114 | 0.374 |
| `r62sm-s10-6201` | 0.1 | passes | 540 of 2,807, 0.192 | 0.264 | 0.156 | 0.336 |

The slow share counts bred grazers with a first speed, slow meaning under 0.03 m a second on the first frame pair, as
the newborn ledger does. "Plan changed" is a nonzero structure count in `genome-diff-read.py`'s diff.

**The rate barely moved the share of changed plans.** At a tenth of the seven chances, a third of births still changed
their plan, against the 0.06 the pre-registration derived. The screen therefore tested change shares from 0.53 down to
0.34, a much narrower range than the 0.48 to 0.06 it was designed for. My reading is that most of the changes the diff
counts come from the sources the screen left at today's rates. Those are an edge's child retargeted by the scalar step,
and a node's removal when it shrinks below its extinction size (inference: the diff does not say which operator made a
change). Within the range tested, the slow share shows no trend.

**A changed plan still carries about twice the risk.** In every arm a newborn whose plan changed is slow at 0.25 to
0.29, against 0.11 to 0.16 for one with its parent's plan. The risk is real. The rate fails as a lever because it moves too
few births from one column to the other.

## D222 and the side readouts

| World | Born share | Best cohort | Chain | Test 2, lowest summer-start share | Test 3 | Never ate | Median age at death (s) | Intake over upkeep |
|---|---|---|---|---|---|---|---|---|
| `r62sm-s100-6201` | 0.828 | 1.26 | 17 | 0.896, at 5,500 s, 15 grazers | 0.897 | 1,064 of 3,408, 0.312 | 68.0 | 1.81 |
| `r62sm-s50-6201` | 0.882 | 1.14 | 19 | 1.054, at 5,500 s | 0.893 | 1,125 of 3,102, 0.363 | 65.5 | 1.61 |
| `r62sm-s25-6201` | 0.939 | 1.24 | 26 | 1.296, at 5,500 s | 0.896 | 1,274 of 3,977, 0.320 | 66.5 | 1.80 |
| `r62sm-s10-6201` | 0.835 | 1.10 | 19 | 0.957, at 11,500 s | 0.891 | 924 of 2,900, 0.319 | 67.0 | 1.65 |

Test 2's share is the kept watts a grazer over 0.908 W, at the 500 s window that starts each summer
(`r62g-windows.txt`). Today's arm misses 0.9 at the first summer start, when 15 grazers were alive, so it fails D222 by
0.004 there. It passes every other test, with a margin. Test-2 failures in small windows have come before, in growth
group 2 at 9 to 17 grazers (`logbook/specs/r62gr2-read/24000/`). My reading is that this one is the same kind of
noise, not a cost of today's rate (inference, on one seed). Grazers are alive at every season's end after the first,
which ends before any grazer is founded.

**Nothing overshoots.** No arm's leaves fall below half the control's in any 3,000 s bin. The lowest ratio is 0.56,
in the half-rate arm.

**Body plans keep their variety.** The bred grazers' part counts have a median of 4 in every arm and bin, apart from
the quarter-rate arm's last bin at 3. The upper quartile is 4 or 5, and every arm reaches 8 parts.

## Against the predictions

| Prediction | Result |
|---|---|
| The change share follows the table within 0.05 | Failed: 0.53, 0.43, 0.37, 0.34 against 0.48, 0.28, 0.15, 0.06 |
| The slow share falls with S: 0.28, 0.25, 0.23, 0.22 | Failed: 0.20, 0.21, 0.16, 0.19, with no trend |
| All four pass D222 | Three pass; today's arm misses test 2 by 0.004 |
| The rule picks S = 0.25 | Failed: S = 0.25 misses the margin by 0.006, and no S is picked |
| The tenth-rate arm's part counts sit within one part of today's | Held |
| No arm's slow share is under 0.17 | Failed narrowly: S = 0.25 is at 0.164 |

## What it means

The structural rate cannot carry the slow newborns away. It moves too few births, and the slow share is already near
the floor the same-plan newborns set. The follow-up on `r62p-t3` (`logbook/specs/r62p-read/newborn-followup/`) finds
those same-plan slow newborns still in open water, near food, with their joints barely moving and every brain central.
That points at the brain a child is born with, which fitting the brain at birth addresses (inference).

## Sources

| Claim | Source |
|---|---|
| slow shares, plan changes | `newborn/genome-diff-*.txt`, `newborn/slow-newborn-*.txt` |
| D222's tests 1 and 3, intake | `r62g-read.txt`, `r62g-cohorts.txt`, `r62-health.txt` |
| test 2 | `r62g-windows.txt`, `r62g-seasons.txt` |
| never ate, age at death | `grazer-tally-*.txt` |
| overshoot | `overshoot-*.txt`, against `r62cs-w500-6201` |
| part counts | `part-count.txt` |
| the unbound sources make most changes | inference, not measured |
| test 2's miss is noise | inference, on one seed |
