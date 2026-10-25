# The overshoot screen's read (D224, 2026-10-24)

The screen's six worlds, `r62cs-w60`, `-w500` and `-w1500` on seeds 6201 and 6202. The controls are `r62ce-6201` and
`r62ce-6202`, the rule without the store share, and round 62's `r62-h6201` and `r62-h6202`, the corpse rule. The tests,
their pass marks and the predictions are in `../r62cs-prereg/predictions.md`, and the card identity is in
`../r62cs-prereg/identity.md`. The worlds ran from commit `1ad7b21c` on the D224 build, config hashes `36147b55`
(60 s), `0f736451` (500 s) and `b5de4374` (1,500 s), in one card group, and no body diverged.

## Where the read departs from the pre-registration

The pre-registration reads every world at 24,000 s. The two 60 s worlds did not reach it. The coordinator ruled on
2026-10-24 that both be stopped short, since each had answered its question and was heading for the 25,000 ceiling
at about 0.6 to 0.8 times real time:

- `r62cs-w60-6202` was stopped at 21,020 s, at 19,918 alive.
- `r62cs-w60-6201` was stopped at 22,680 s, at about 20,700 alive.

Both keep checkpoints at 21,000 s, so they can be resumed exactly. The 60 s pair is therefore read at 21,000 s, with
the four controls read again at the same second, in `at21000/`. In this pair, test 3's last bin is 18,000 to
21,000 s, not 21,000 to 24,000 s. The other four worlds ran to their budget and are read at 24,000 s in this folder.

The tables come from `scripts/reads/r62cs-read.ps1`. It runs `r62-read.ps1 -Worlds` and `travel.py` for both
passes. The population, guild and remains columns were read from each world's `stats.jsonl`.

## The tests

The 60 s worlds are read at 21,000 s, and the others at 24,000 s.

| Test | w60 6201 / 6202 | w500 6201 / 6202 | w1500 6201 / 6202 | r62ce 6201 / 6202 | r62-h 6201 / 6202 |
|---|---|---|---|---|---|
| 1. Born share at the end (at least 0.80) | 0.083 / 0.000 | 0.000 / 0.062 | 0.071 / 0.000 | 0.000 / 0.000 | 0.888 / 0.944 |
| 1. Best cohort's mouthed kids a body (at least 1) | 0.48 / 0.59 | 0.66 / 0.59 | 0.51 / 0.45 | 0.07 / 0.04 | 1.16 / 1.14 |
| 1. Deepest mouthed chain (at least 10) | 1 / 2 | 2 / 2 | 2 / 2 | 1 / 1 | 23 / 21 |
| 2. Grazers alive at every season end | yes / yes | yes / yes | yes / yes | yes / yes | yes / yes |
| 2. Upkeep met in the summer windows (at least 0.9) | 0.48-0.66 / 0.32-0.65 | 0.28-0.61 / 0.34-0.51 | 0.48-0.54 / 0.35-0.51 | 0.16-0.20 / 0.14-0.21 | 0.99-1.17 / 0.90-1.21 |
| 3. Living share of kept intake, last bin (at least 0.5) | 0.926 / 0.957 | 0.862 / 0.819 | 0.675 / 0.726 | 0.791 / 0.817 | not defined |
| 3. Its trend from 12,000-15,000 s | falls from 0.972 / from 0.965 | rises from 0.846 / falls from 0.895 | falls from 0.753 / from 0.843 | falls from 0.837 / from 0.904 | not defined |

The best cohort is from `r62g-cohorts.txt` and the chain from `r62g-read.txt`. The summer upkeep share is the
`share` column of `r62g-seasons.txt` over the full summers 5,750 to 20,750 s. The w1500-6201 summer of 23,750 to
24,000 s, at 1.07, is 250 s long and is left out. The first summer has no grazers. The r62ce and r62-h figures at
24,000 s are those of `../r62ce-read/README.md`. The 21,000 s pass gives the controls the same verdict.

No arm passes. Tests 1 and 2 fail in all six worlds, and test 3 holds in all six. The store share lifted the
grazers' income about two and a half times over `r62ce`'s, but not to their upkeep:

- Bred grazers: 138 to 175 in each world over the run, against 1 to 5 in `r62ce` and 2,756 to 4,727 in the controls.
- Kept joules a kill: 56 to 89 J, against 24 to 27 J in `r62ce` and 418 to 434 J in the controls.
- Intake over upkeep: 0.43 to 0.52, against 0.19 to 0.20 in `r62ce` and 0.90 to 1.04 in the controls.

All of these are from `r62g-read.txt`. The kill rate barely moved, at 4.4e-3 to 8.1e-3 kills a grazer-second
against 6.1e-3 to 7.3e-3. The grazers alive at any time are 12 to 17, nearly all founders from the pool.

The window made little difference to the grazers. Their kept watts are 0.44 to 0.45 at 60 s, 0.43 at 500 s and 0.39
to 0.47 at 1,500 s (`r62g-read.txt`, J/g-s). Remains feed at most a third of their kept intake even at 1,500 s
(test 3's complement, 0.27 to 0.33), with 4,790 to 6,774 remains standing. Most of the remains' joules close
unbitten: 4.3 to 6.4 MJ burnt, against a guild intake of 13 to 36 kJ in the last bin.

## The overshoot

There is none. The pre-registration reads an overshoot as grazers breeding lines while the leaves fall below half of
`r62ce`'s count on the same seed. No world bred a line. The leaves stayed at 61 to 87% of `r62ce`'s in the 500 s
and 1,500 s worlds, and at 130% in the 60 s worlds:

| World | Read to | Peak alive (at) | Leaves | Absorbers | Jointed | Grazers alive | Remains standing | biteStore J |
|---|---|---|---|---|---|---|---|---|
| r62cs-w60-6201 | 21,000 | 19,372 (20,790) | 16,734 | 2,467 | 1,155 | 12 | 424 | 96,709 |
| r62cs-w60-6202 | 21,000 | 20,657 (20,770) | 17,241 | 2,817 | 683 | 15 | 505 | 118,251 |
| r62ce-6201 | 21,000 | 13,832 (20,730) | 12,844 | 647 | 699 | 11 | 313 | - |
| r62ce-6202 | 21,000 | 14,564 (20,760) | 13,374 | 606 | 685 | 11 | 371 | - |
| r62cs-w500-6201 | 24,000 | 13,631 (24,000) | 12,663 | 2,578 | 432 | 15 | 2,713 | 112,031 |
| r62cs-w500-6202 | 24,000 | 12,245 (24,000) | 11,466 | 784 | 1,111 | 16 | 2,024 | 147,678 |
| r62cs-w1500-6201 | 24,000 | 12,257 (24,000) | 11,365 | 1,024 | 1,660 | 14 | 6,774 | 117,550 |
| r62cs-w1500-6202 | 24,000 | 9,315 (24,000) | 9,080 | 298 | 154 | 12 | 4,790 | 128,943 |
| r62ce-6201 | 24,000 | 15,388 (24,000) | 14,570 | 1,072 | 818 | 14 | 359 | - |
| r62ce-6202 | 24,000 | 15,827 (24,000) | 14,963 | 926 | 719 | 12 | 394 | - |
| r62-h6201 | 24,000 | 6,359 (24,000) | 6,049 | 231 | 312 | 152 | - | - |
| r62-h6202 | 24,000 | 6,095 (20,720) | 5,554 | 204 | 96 | 285 | - | - |

Leaves are bodies with any photosynthetic tissue (`photosynthetic`) and absorbers bodies with absorptive tissue
(`absorptive`); the guilds overlap. Grazers alive are the `living` column of `r62-health.txt`. Every world's matter
residual stayed within 1.5e-5, under ten times the long baseline's drift.

The 60 s worlds grew fastest. At 21,000 s they held 19,081 and 19,941 alive against `r62ce`'s 13,272 and 13,940, and
they would probably have reached the ceiling before 24,000 s had they run on. What grew is leaves and absorbers, not
grazers: the absorbers were four times `r62ce`'s. My reading, which is inference: a short window closes the remains
fast, and their matter returns to the water near the oases. The store share makes the leaves' deaths by bite release
their reserve as well, and the absorbers live on what is released. Longer windows hold that matter in standing
remains instead, and those worlds grew more slowly than `r62ce`.

The oases did not empty. The share of the living on the islands at the end is 0.55 to 0.63, against 0.60 to 0.63 in
`r62ce` and 0.69 to 0.74 in the round 62 controls (`r60-read.txt`, M3).

## Travel (test 4, no pass mark)

From `travel.txt`, bodies seen for 600 s or more from 12,000 s to the end:

| World | Grazers | Path m per 1,000 s | Net m | Leaves' path m per 1,000 s |
|---|---|---|---|---|
| r62cs-w60-6201 (to 21,000) | 100 | 45.4 | 10.2 | 19.0 |
| r62cs-w60-6202 (to 21,000) | 108 | 47.6 | 9.9 | 19.2 |
| r62cs-w500-6201 | 162 | 39.6 | 11.1 | 19.6 |
| r62cs-w500-6202 | 181 | 38.5 | 11.0 | 19.2 |
| r62cs-w1500-6201 | 287 | 24.7 | 9.6 | 19.4 |
| r62cs-w1500-6202 | 288 | 26.2 | 12.7 | 19.2 |
| r62ce-6201 | 128 | 48.9 | 9.8 | 19.2 |
| r62ce-6202 | 151 | 45.8 | 11.2 | 19.2 |
| r62-h6201 | 498 | 23.8 | 8.2 | 18.9 |
| r62-h6202 | 934 | 20.2 | 7.2 | 19.1 |

No body went far by `travel.py`'s mark in any world. The grazers' path shortens as the window lengthens, from about
46 m per 1,000 s at 60 s to about 25 m at 1,500 s, near the controls'. `r60-read.py` counts one grazer migrant between
islands, in `r62cs-w60-6202` at 21,000 s, and none in the other screen worlds. The controls count one in `r62-h6201`.

## The predictions

- The 60 s arm passes test 1 in one world or more: **failed**, with a born share of 0.083 and 0.000.
- The 500 s arm passes tests 1 and 2 in both worlds, and remains feed a third or more of the intake: **failed**.
  Remains fed 14 to 18%.
- The 1,500 s arm overshoots, and test 3 fails as carrion takes over: **failed**. The leaves held at 78% and 61% of
  `r62ce`'s, and test 3 held at 0.675 and 0.726. The remains did stand in the thousands, and the pace fell below
  real time, as guessed.
- No world reaches the ceiling: **held**, but only because the 60 s pair were stopped at about 20,000 alive.
- The grazers travel further than in `r62ce`: **failed**. They travel as far at 60 s and less as the window
  lengthens. No
  grazer migrates in the 60 s arm: **failed by one**, the single migrant in `r62cs-w60-6202`.

## What it means for the plan

The pre-registration names this outcome. No arm passes tests 1 and 2, so the store share and the window together
are too small. The next step it names is a bigger dial, a longer window or a deeper wound, and that choice is the
coordinator's. My reading, which is inference:

- A longer window is not the lever. The 1,500 s worlds fed their grazers no better than the 60 s ones.
- The gap is the joules a kill yields, which is still five to eight times short of the corpse rule's.
- The kill rate is already the controls' or higher, so the deficit is the size of the meal and not how often grazers
  find one.

The screen was not watched in the theatre before this read was written.
