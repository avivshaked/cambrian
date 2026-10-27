# The confirmation round at 24,000 s: the settings hold on every new seed, and grazers stay home

*2026-10-27, the speed agent. Round `r62cf`, pre-registered in `logbook/specs/r62cf-prereg/predictions.md` at
`6e310858`, ran as one card group from 18:04 to 21:01 on that commit, clean. The outputs of
`scripts/reads/screen-read-24000.ps1 -Gene -ControlSeed 6202` sit beside this file, with `grazer-moves.txt` and
`cols.txt`. The reads ran pinned at BelowNormal, the screen read on logical 20-21 and the rest on 22-31.*

## The round

Three new seeds, 6202, 6203 and 6204, of `r62sm-s100`'s settings, which are the screens' choices. Those are `r62p-t16`'s world,
growth as a climb in 60 s with the growth-reserve gene starting at 30 s, and today's structural mutation rate. Each
world ran 24,000 s.

**It is the screen's build.** Every manifest carries gitCommit `6e310858`, not dirty, with core `361aa924`, dynamics
`29144991`, farm `ae99d3c2` and kernel `eec89fde`. Those are `r62sm-s100-6201`'s, so that world stands as the fourth
seed.

**The books close.** No world diverged. The largest audit residual is 0.0017 J, under the 0.1 J line. The largest
matter residual is 1.2e-5 to 1.7e-5 units, just over the 1e-5 line the card log checks against. `r62sm` sat at 8.1e-6
to 1.8e-5 on the same build, its s100 world at 1.77e-5, so this is the build's floor, not a new fault.

**It ran fast.** The worlds stepped at 2.4 to 3.0 times real time early on, and 1.6 to 2.0 times late, as they filled. The
group took 2 h 58 min of wall time, against 3.5 to 4 h guessed. No exit came near.

## D222, by D244's reading first

D244 (the owner, 2026-10-27) amends D222 for runs aimed at grazers. A world must first reach 30 grazers alive. A
summer-start window whose mean count is under 30 is not judged.

| World | Grazers at most | Born share | Best cohort | Chain | Test 3 | Test-2 windows judged at 30 | Their shares | Verdict at 30 | At 40 | Strict |
|---|---|---|---|---|---|---|---|---|---|---|
| `r62cf-6202` | 141 | 0.908 | 1.03 | 25 | 0.882 | 17,500 and 23,500 s | 1.64, 1.85 | pass | pass | pass |
| `r62cf-6203` | 87 | 0.812 | 1.10 | 34 | 0.904 | 17,500 and 23,500 s | 1.49, 2.23 | pass | pass | pass |
| `r62cf-6204` | 112 | 0.946 | 1.01 | 18 | 0.895 | 17,500 and 23,500 s | 1.59, 1.70 | pass | pass | pass |
| `r62sm-s100-6201`, the fourth seed | 160 | 0.828 | 1.26 | 17 | 0.897 | 17,500 and 23,500 s | 1.89, 1.92 | pass | pass | fails, 0.896 at 5,500 s |

The bars are a born share of 0.8, a best cohort of 1, a chain of 10, a test-2 share of 0.9 and a test-3 share of 0.5.
"Grazers at most" is the largest count alive at a window's end.

**The settings are confirmed on every seed, by D244's reading.** All four worlds reach 30 grazers, and every test
judged passes, at 30 and at 40 alike. At 40, `r62cf-6204`'s 17,500 s window, with a mean of 35 grazers, is no longer
judged. Its other window still passes.

**The three new worlds pass the strict reading too.** Every summer-start window passes, the small ones included. The
lowest is `r62cf-6204`'s first at 0.98, with 15 grazers. Only the fourth seed's first summer start, at 15 grazers, fails
strictly, and D244 does not judge it. So the amendment changes the verdict of one window in four worlds.

**The margins are thinnest in test 1.** `r62cf-6203`'s born share is 0.812 against 0.8, and `r62cf-6204`'s best cohort
is 1.01 against 1. Both pass, but by little.

## Side readouts

| World | Never ate | Median age at death (s) | Intake over upkeep | Islands held at 5% | Lowest leaf ratio |
|---|---|---|---|---|---|
| `r62cf-6202` | 1,178 of 3,504, 0.336 | 66.5 | 1.64 | 4 to 5 | 0.733 |
| `r62cf-6203` | 961 of 2,597, 0.370 | 60.0 | 1.86 | 4 to 5 | 0.471, at 24,000 s |
| `r62cf-6204` | 1,070 of 3,028, 0.353 | 62.0 | 1.70 | 4 to 6 | 0.558 |

The leaf ratio is a world's leaves against a control without grazers.

**One overshoot mark, and it is weak.** `r62cf-6203`'s leaves fall to 0.471 of its control's at 24,000 s, under the
0.5 mark. That control is seed 6202's (`r62cs-w500-6202`), because 6203 has none of its own, so the comparison crosses
seeds. 6203's world holds fewer bodies than 6202's throughout, at 5,467 against 8,659 at the end. Grazers are not the
leading cause of leaf deaths in any bin, where age is. My reading is that the mark measures the seed difference more
than overgrazing (inference).

## The ocean question: grazers stay on their oases

The moves read takes a 10 s stride, on r60-read's islands as seeded at 100 s. At a 100 s stride its count agrees with
r60-read's migrants in every year, in every world.

| World | Moves | Movers | Bred grazers that ever move | Moves per 1,000 grazer-seconds | Distance, quartiles (m) | Crossing time, quartiles (s) | Largest move | Open-water share, grazers / all |
|---|---|---|---|---|---|---|---|---|
| `r62cf-6202` | 5 | 4 | 1 of 3,627 | 0.004 | 23, 29, 46 | 140, 230, 350 | 83 m in 520 s | 0.16 / 0.35 |
| `r62cf-6203` | 6 | 5 | 5 of 2,640 | 0.007 | 23, 30, 40 | 72, 140, 148 | 50 m in 150 s | 0.21 / 0.36 |
| `r62cf-6204` | 13 | 10 | 6 of 3,121 | 0.013 | 30, 33, 46 | 120, 170, 190 | 97 m in 190 s | 0.18 / 0.35 |
| `r62sm-s100-6201` | 14 | 12 | 9 of 3,465 | 0.012 | 13, 16, 34 | 60, 90, 142 | 143 m in 510 s | 0.18 / 0.38 |

Every prediction about moves held. Each world had under 30 moves, and under 1% of bred grazers ever moved. The
median move was 29 to 33 m, inside the guessed 10 to 40 m. The grazers' open-water share was 0.16 to 0.21, inside the
guessed 0.15 to 0.25, and about half that of all the living.

**So the world has oases and grazers that make a living on them, but almost no migration.** A grazer dies at a median
of 60 to 67 s, and a crossing takes a median of 140 to 230 s, so few live long enough to make one (inference, as the
pre-registration put it). The owner chose fat as what should make a crossing pay, and its screen waited on this round.

**Life spreads over the box rather than standing in columns.** The bodies' x spread is 60 to 82 m at every 3,000 s,
against about 87 m for bodies spread evenly over the 300 m box. They fill 1,400 to 6,000 of the 90,000 square metres
(`cols.txt`; inference: a spread this wide could still be several dense patches, which only the pictures show).

## What it means

**The chosen settings are confirmed.** On four seeds, the three new ones and the screen's own, grazers make a living
from living leaves, by D244's reading at 30 and at 40. Three of the four also pass strictly. D222's gate is passed, and
fat's screen, which waited on it, can be planned (inference: the scheduling is the coordinator's).

**The ocean aim is not met.** The world has separate oases with mostly empty water between them, which is the aim. But
its grazers almost never travel between oases, and a crossing costs about three times a grazer's median life. Making a
crossing pay is what fat is for.

The pictures, one wide frame per world at 12,000 s and 24,000 s, are asked of b0 and are not yet seen.

## Sources

| Claim | Source |
|---|---|
| build, hashes, clean tree | each world's `run.json`; the card log `scratch/wt-r62cf/scratch/logs/card-run-r62cf.log` |
| residuals, divergence, wall time | the card log's closing lines; `r62sm`'s from `scratch/wt-struct/scratch/logs/card-run-r62sm.log` |
| born share, test 3 | `r62-health.txt` |
| best cohort | `r62g-cohorts.txt` |
| chain, intake over upkeep | `r62g-read.txt` |
| test 2, the windows' counts, grazers at most | `r62g-windows.txt`, `r62g-seasons.txt` |
| the fourth seed's row | `logbook/specs/r62sm-read/24000/` |
| never ate, age at death | `grazer-tally-*.txt` |
| islands held | `r60-read.txt` |
| leaf ratios and the overshoot mark | `overshoot-*.txt`, against `r62cs-w500-6202` |
| moves, distances, crossings, open water | `grazer-moves.txt`; the fourth seed's from `logbook/specs/r62cf-prereg/moves-test/` |
| alive, cols, x sd | `cols.txt`, read from each world's `runs/<world>.md` |
| the mark measures the seed difference | inference |
| few grazers live to cross | inference, from median life against median crossing |
