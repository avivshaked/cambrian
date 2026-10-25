# The deeper-wound screen's read (D226 and D227, 2026-10-25)

The screen's six worlds are `r62bd-x2`, `-x4` and `-x16` on seeds 6201 and 6202: a mouth's blow at 2, 4 and 16 times
its damage, with the intake scale at 16. The controls are:
- `r62cs-w500-6201` and `-6202`, the same world at a scale of 1;
- `r62ce-6201` and `-6202`, without the store share;
- round 62's `r62-h6201` and `-h6202`, the corpse rule.

The tests, their pass marks and the predictions are in `../r62bd-prereg/predictions.md`. The card identity is in
`../r62bd-prereg/identity.md`.

The worlds ran from commit `4e94b0e0` in one card group. The config hashes are `6da8f3c6` (2x), `490de19b` (4x) and
`169145a4` (16x). All six reached their budget of 24,000 s, and none was stopped. So the pre-registration's limit on
stopping a world does not apply, and every world is read at 24,000 s.

The tables come from `scripts/reads/r62bd-read.ps1`. It runs `r62-read.ps1 -Worlds` and `travel.py` on the twelve
worlds, `bite-cap.py` on the six and `r62cs-w500`, and `scripts/reads/r62bd-guilds.py` on all twelve. Test 3's living
share and the remains' joules were read from each world's `stats.jsonl`. The living share is the bin's
`biteKeptLeaves` plus `biteKeptOthers` over those plus `biteKeptRemains`.

## The tests

| Test | x2 6201 / 6202 | x4 6201 / 6202 | x16 6201 / 6202 | r62cs-w500 6201 / 6202 | r62-h 6201 / 6202 |
|---|---|---|---|---|---|
| 1. Born share at the end (at least 0.80) | 0.176 / 0.071 | 0.250 / 0.125 | 0.409 / 0.000 | 0.000 / 0.062 | 0.888 / 0.944 |
| 1. Best cohort's mouthed kids a body (at least 1) | 0.62 / 0.67 | 0.66 / 0.62 | 0.60 / 0.66 | 0.66 / 0.59 | 1.16 / 1.14 |
| 1. Deepest mouthed chain (at least 10) | 1 / 2 | 3 / 2 | 2 / 3 | 2 / 2 | 23 / 21 |
| 2. Grazers alive at every season end | yes / yes | yes / yes | yes / yes | yes / yes | yes / yes |
| 2. Upkeep met in the summer windows (at least 0.9) | 0.58-0.93 / 0.69-0.80 | 0.39-0.72 / 0.35-0.57 | 0.59-0.65 / 0.35-0.61 | 0.28-0.61 / 0.34-0.51 | 0.99-1.17 / 0.90-1.21 |
| 3. Living share of kept intake, 21,000-24,000 s (at least 0.5) | 0.746 / 0.828 | 0.796 / 0.798 | 0.767 / 0.813 | 0.862 / 0.819 | not defined |
| 3. Its trend from 12,000-15,000 s | falls from 0.817 / from 0.884 | falls from 0.824 / from 0.839 | falls from 0.830 / rises from 0.785 | rises from 0.846 / falls from 0.895 | not defined |

- **Sources.** The born share is from `r62-health.txt`, the best cohort from `r62g-cohorts.txt` and the chain from
  `r62g-read.txt`.
- **The summer windows.** The summer upkeep share is the `share` column of `r62g-seasons.txt` over the full summers
  from 5,750 to 20,750 s. The last summer, from 23,750 to 24,000 s, is 250 s long and is left out; it read 0.98 to 1.24
  in the screen's worlds. The first summer has no grazers.

**No arm passes.** Tests 1 and 2 fail in all six worlds, and test 3 holds in all six. One summer comes near the mark:
0.93 in `x2-6201` from 17,750 to 20,750 s. The deeper wound lifted the grazers' income over `r62cs-w500`'s on the
same seed, but not to their upkeep (all figures from `r62g-read.txt`):

| World | Bred grazers | Kills a grazer-second | Against r62cs-w500 | Kept J a kill | Intake over upkeep |
|---|---|---|---|---|---|
| x2-6201 | 279 | 9.31e-3 | 1.57x | 65.8 | 0.675 |
| x2-6202 | 394 | 8.02e-3 | 1.30x | 77.0 | 0.680 |
| x4-6201 | 296 | 8.71e-3 | 1.47x | 67.1 | 0.644 |
| x4-6202 | 272 | 7.02e-3 | 1.14x | 73.2 | 0.566 |
| x16-6201 | 308 | 1.00e-2 | 1.69x | 61.1 | 0.673 |
| x16-6202 | 301 | 7.63e-3 | 1.24x | 73.0 | 0.614 |
| r62cs-w500-6201 | 158 | 5.94e-3 | 1 | 72.8 | 0.476 |
| r62cs-w500-6202 | 164 | 6.17e-3 | 1 | 68.9 | 0.468 |
| r62-h6201 | 2,756 | 2.17e-3 | | 434.4 | 1.040 |
| r62-h6202 | 4,727 | 1.94e-3 | | 418.4 | 0.896 |

The deeper wound raised income through more kills a second, not through a bigger meal (question 4). The kept joules
a kill stayed at `r62cs-w500`'s, 61 to 77 J against 69 to 73 J. Kills a grazer-second rose 1.1 to 1.7 times, and
intake over upkeep rose from 0.47 to between 0.57 and 0.68. About twice as many grazers were bred in each world, yet
the grazers alive at any time stayed at 8 to 22, mostly founders from the pool.

The arms hardly differ from one another. The 2x arm kept as much as the 16x arm, and more than the 4x arm on seed
6202. So for income, the wound is already spent at 2 times.

## The share of blows that finish a part (question 5)

From `guilds.txt`, the bin's own blows from the stats columns `mouthBlows` and `mouthBlowsFinishing`:

| World | 3,000-6,000 s | 6,000-9,000 | 9,000-12,000 | 12,000-15,000 | 15,000-18,000 | 18,000-21,000 | 21,000-24,000 |
|---|---|---|---|---|---|---|---|
| x2-6201 | 0.830 | 0.937 | 0.948 | 0.968 | 0.985 | 0.979 | 0.986 |
| x2-6202 | 0.826 | 0.924 | 0.884 | 0.915 | 0.934 | 0.978 | 0.992 |
| x4, x16 (all four) | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 0.999 to 1.000 |

At 4 and 16 times every blow finishes the part it touches, from the first bite to the last, as the check found. The
leaves did not evolve armour against it in 24,000 s. At 2 times the share is lower at first and rises through the
run, toward 1. That is the opposite of armour. My reading, which is inference: as the crowd grows, more of the bitten
parts are small or newly grown, and a blow at 2 times already finishes those.

## The overshoot

There is none. The pre-registration reads an overshoot as grazers breeding lines while the leaves fall below half of
`r62cs-w500`'s count on the same seed. No world bred a line, and every world held more leaves than `r62cs-w500`, at
114 to 150% of its count at 24,000 s:

| World | Peak alive (at) | Leaves at 12,000 / 24,000 s | Absorbers at 24,000 | Jointed at 24,000 | Non-photosynthetic at 24,000 | Remains standing | Remains' joules closed |
|---|---|---|---|---|---|---|---|
| x2-6201 | 21,722 (24,000) | 4,520 / 18,966 | 2,879 | 610 | 2,756 | 3,795 | 6.99 MJ |
| x2-6202 | 14,863 (24,000) | 3,673 / 13,524 | 1,384 | 324 | 1,339 | 2,383 | 6.07 MJ |
| x4-6201 | 16,352 (24,000) | 4,322 / 15,598 | 952 | 511 | 754 | 3,707 | 6.83 MJ |
| x4-6202 | 14,210 (24,000) | 3,749 / 13,497 | 766 | 339 | 713 | 2,429 | 5.97 MJ |
| x16-6201 | 15,512 (24,000) | 4,369 / 14,484 | 1,132 | 1,178 | 1,028 | 3,125 | 6.67 MJ |
| x16-6202 | 16,061 (24,000) | 3,642 / 14,473 | 1,605 | 164 | 1,588 | 2,601 | 6.17 MJ |
| r62cs-w500-6201 | 13,631 (24,000) | 4,163 / 12,663 | 2,578 | 432 | 968 | 2,713 | 5.94 MJ |
| r62cs-w500-6202 | 12,245 (24,000) | 3,483 / 11,466 | 784 | 1,111 | 779 | 2,024 | 5.54 MJ |
| r62ce-6201 | 15,388 (24,000) | 4,412 / 14,570 | 1,072 | 818 | 818 | | |
| r62ce-6202 | 15,827 (24,000) | 4,406 / 14,963 | 926 | 719 | 864 | | |
| r62-h6201 | 6,359 (24,000) | 1,656 / 6,049 | 231 | 312 | 310 | | |
| r62-h6202 | 6,095 (20,720) | 1,860 / 5,554 | 204 | 96 | 412 | | |

- **The guilds overlap.** Leaves are bodies with any photosynthetic tissue, absorbers bodies with absorptive tissue.
  The non-photosynthetic are every body without photosynthetic tissue, an upper bound on the grazers. The grazers
  alive, by `r62-health.txt`, are 13 to 22.
- **The ceiling.** No world reached 25,000. The most crowded, `x2-6201` at 21,722, was still growing at the end, and
  most of the growth was leaves.
- **The oases did not empty.** The share of the living on the islands at the end is 0.60 to 0.64, against 0.62 to 0.63
  in `r62cs-w500` and 0.69 to 0.74 in the round 62 controls (`r60-read.txt`, M3). Three screen worlds sit at 0.60,
  on the edge of M3's mark.

The bite cap cut less in the screen than at a scale of 1. It took 0.15 to 0.24 of what bites offered in the last
bin, against 0.33 to 0.39 in `r62cs-w500` (`bite-cap.txt`, the "cut+no-intake" share). The intake scale of 16 raised
the cap, as intended.

## Travel (test 4, no pass mark)

From `travel.txt`, bodies seen for 600 s or more from 12,000 s to the end:

| World | Grazers | Path m per 1,000 s | Net m | Leaves' path m per 1,000 s |
|---|---|---|---|---|
| x2-6201 | 151 | 39.0 | 12.2 | 19.4 |
| x2-6202 | 177 | 38.4 | 11.8 | 19.3 |
| x4-6201 | 166 | 40.3 | 11.3 | 19.3 |
| x4-6202 | 177 | 39.2 | 10.4 | 19.2 |
| x16-6201 | 165 | 40.4 | 10.4 | 19.6 |
| x16-6202 | 179 | 38.0 | 12.2 | 19.3 |
| r62cs-w500-6201 | 162 | 39.6 | 11.1 | 19.6 |
| r62cs-w500-6202 | 181 | 38.5 | 11.0 | 19.2 |
| r62ce-6201 | 128 | 48.9 | 9.8 | 19.2 |
| r62ce-6202 | 151 | 45.8 | 11.2 | 19.2 |
| r62-h6201 | 498 | 23.8 | 8.2 | 18.9 |
| r62-h6202 | 934 | 20.2 | 7.2 | 19.1 |

The wound did not change how far grazers swim: the screen's grazers travel as `r62cs-w500`'s do. No body went far by
`travel.py`'s mark in any world.

## The predictions

- The 4x and 16x arms raise kills a grazer-second three times or more over `r62cs-w500`'s, with the kept joules a
  kill near or below 56 to 89 J: **failed on the kills, held on the meal.** Kills rose 1.1 to 1.7 times, and the meal
  stayed at 61 to 73 J.
- Intake over upkeep passes 0.9 in the 16x arm in at least one world, and test 1 holds there; tests 1 and 2 hold
  together in both worlds of at least one arm: **failed.** The 16x arm read 0.61 and 0.67, and no world held test 1.
- The 2x arm sits between `r62cs-w500` and the 4x arm on every economic column, with a finishing share well below 1:
  **failed.** The 2x arm kept as much as any arm. Its finishing share was 0.83 at first and near 0.99 by the end.
- The 16x arm overshoots: **failed.** Its leaves held at 114% and 126% of `r62cs-w500`'s.
- In the 16x arm the share of finishing blows stays near 1: **held**, at 1.000 throughout.
- No world reaches the ceiling of 25,000: **held.** The highest was 21,722.

## What it means for the plan

The pre-registration names this outcome. Even the 4x arm fails tests 1 and 2, so the wound is spent as a lever: at
2 times a touch already takes nearly the whole part. The next lever it names is the meal itself. It is bounded by one
part, so a kill could feed from the whole body, or a bite could take from parts beyond the one it touches. That
choice is put to the coordinator.

My reading, which is inference:
- The gap is still the meal, as in `r62cs`. A kill keeps 61 to 77 J against about 420 J under the corpse rule, five
  to seven times short. The deeper wound brought kills faster, which closed about a fifth of the distance to upkeep.
- Raising the wound further adds nothing, since the arms from 2 to 16 times are alike.
- A lever on the meal acts on the factor that is short. If a kill fed from the whole body, the meal would approach
  the corpse rule's. That is a guess from the controls' J a kill, not a measurement.

The screen was not watched in the theatre before this read was written.
