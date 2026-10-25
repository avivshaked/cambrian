# The contact-eating screen's read at 24,000 s (D222, 2026-10-24)

The screen's three worlds, `r62ce-6201` to `r62ce-6203`, against round 62's controls, `r62-h6201` to `r62-h6203`, on
the same seeds. The tests and their pass marks are in `../r62ce-prereg/predictions.md`. The card identity is in
`../r62ce-prereg/identity.md`. The worlds ran from commit `bc99c43b` with config `a99d10a5c2485585`. All three ended at
their budget with no body diverged.

The tables in this folder come from `scripts/reads/r62-read.ps1 -Worlds` on the six worlds and from
`scripts/reads/travel.py` (`travel.txt`). The population and matter columns below were read from each world's
`stats.jsonl` at 24,000 s.

## The tests

| Test | 6201 | 6202 | 6203 | Controls (6201 / 6202 / 6203) |
|---|---|---|---|---|
| 1. Born share at 24,000 s (at least 0.80) | 0.000 | 0.000 | 0.000 | 0.888 / 0.944 / 0.741 |
| 1. Best cohort's mouthed kids a body (at least 1) | 0.07 | 0.04 | 0.03 | 1.16 / 1.14 / 0.95 |
| 1. Deepest mouthed chain (at least 10) | 1 | 1 | 1 | 23 / 21 / 6 |
| 2. Grazers alive at every season end | yes, all founded | yes, all founded | yes, all founded | yes |
| 2. Upkeep met in the summer windows (at least 0.9) | 0.04 to 0.31 | 0.08 to 0.42 | 0.03 to 0.31 | 0.66 to 1.87 / 0.67 to 1.57 / 0.61 to 1.44 |
| 3. Living share of kept intake, 21,000-24,000 s (at least 0.5) | 0.791 | 0.817 | 0.765 | not defined |
| 3. Its trend from 12,000-15,000 s | falls from 0.837 | falls from 0.904 | falls from 0.901 | not defined |

The screen fails on the pass rule's first half, since tests 1 and 2 fail in all three worlds. Test 3 holds in all
three. No grazer bred a line: 1 to 5 were bred in each world over the whole run, against 711 to 4,727 in the
controls. The 9 to 14 alive at any time are all founders from the pool. The pre-registration names this outcome under "What would change the plan". The next step it names is a screen of
`IntakeScale`, which is the owner's to rule.

## Why, as far as the numbers show

The grazers kill leaves about as often as the controls' grazers do: 509 kills against 566 in the winter of
20,750-23,750 s in 6201. A kill yields far less. Over the run, a kill in the screen comes with about 24 to 31 J kept,
against 398 to 434 J in the controls (`r62g-read.txt`, J/kill). A grazer keeps 0.12 to 0.39 W against an upkeep of
0.908 W (`r62g-seasons.txt`). The guild's kept intake per 3,000 s bin is 5 to 11 kJ, against 33 to 459 kJ
in the controls (`r62-health.txt`).

The dead are not eaten. Remains closed with 7.1 MJ burnt in 6202 (`remainsClosedJoules` at 24,000 s), against about
10 kJ kept by grazers in the last bin. About a fifth of the grazers' little kept intake is from remains (test 3's
complement).

My reading, which is inference: a bite keeps at most the biting part's intake rate for the moment of contact, and a
leaf that dies leaves remains for 60 s. In the controls the same death left a corpse that fed several grazers over
its half-life of 500 s. Under this rule the meal a kill used to found is burnt, so the grazers' income falls by more
than ten times while their kill rate holds.

## The population and the guilds at 24,000 s

| World | Peak alive (at) | Leaves | Absorbers | Jointed | Remains standing |
|---|---|---|---|---|---|
| r62ce-6201 | 15,388 (24,000 s) | 14,570 | 1,072 | 818 | 359 |
| r62-h6201 | 6,359 (24,000 s) | 6,049 | 231 | 312 | - |
| r62ce-6202 | 15,827 (24,000 s) | 14,963 | 926 | 719 | 394 |
| r62-h6202 | 6,095 (20,720 s) | 5,554 | 204 | 96 | - |
| r62ce-6203 | 12,607 (24,000 s) | 11,900 | 743 | 47 | 248 |
| r62-h6203 | 6,462 (20,760 s) | 5,301 | 19 | 127 | - |

The guilds overlap: a body with any photosynthetic tissue is counted a leaf, one with absorptive tissue an absorber.

- No world reached the ceiling. The limit is 25,000 (`maximumPopulation`). The peaks were 12,607 to 15,827,
  about half of it. All three were still climbing at 24,000 s, at 2.3 to 2.7 times their controls' count then.
- The leaves more than doubled and the absorbers rose fourfold or more. The pre-registration guessed that the
  absorbers would fall with the Nutrients field. They did not: the free matter, standing less locked, is 8,545 to
  8,936 units against 7,479 to 7,761 in the controls. My reading, which is inference: no grazer line crops the leaves,
  so they grow until crowding and age kill them. Their remains close to the water, and that feeds the absorbers.
- Remains standing were 213 to 436 at the last watch looks and 248 to 394 at the end, the few hundred D220
  estimated.

## Travel (test 4, no pass mark)

From `travel.txt`, bodies seen 600 s or more between 12,000 and 24,000 s:

| World | Grazers | Path m per 1,000 s | Net m | Leaves' path m per 1,000 s |
|---|---|---|---|---|
| r62ce-6201 | 128 | 48.9 | 9.8 | 19.2 |
| r62-h6201 | 498 | 23.8 | 8.2 | 18.9 |
| r62ce-6202 | 151 | 45.8 | 11.2 | 19.2 |
| r62-h6202 | 934 | 20.2 | 7.2 | 19.1 |
| r62ce-6203 | 130 | 49.1 | 10.7 | 19.5 |
| r62-h6203 | 193 | 40.8 | 9.8 | 19.1 |

The screen's grazers trace about twice their controls' path in 6201 and 6202, and a fifth more in 6203. They end no
further from where they were first seen: the median net distance is 10 to 11 m against 7 to 10 m. In no guild does
any body pass a third of the box's shorter side. A leaf drifts about 19 m per 1,000 s, so that is the floor a
path is read against. The migrants in `r60-read.txt` are leaves in both arms. No grazer was seen over two islands in
any year in the screen, and one was in one year of one control.

## Books and pace

- The matter residual's largest over the run is 2.77e-5 units, in 6202. The pre-registration's stop line is ten
  times the baseline's drift, 2.54e-4.
- The audit residual is -2.8e-3, -1.4e-3 and 8.4e-4 J at 24,000 s, against 7e-5 to 1e-4 J in the controls. It grew as
  the population did, from under 1e-5 J at 12,000 s. The books it closes move millions of joules (`lightJoules`
  reads 4.5e6 at 24,000 s in 6202), so this is rounding in the sums (inference from the scale).
- The pace was 4.3 times real time at founding, 1.05 to 1.26 at 21,000 to 24,000 s. The prediction was at least 1 times
  real time, and it held, narrowly.

## The predictions, against the outcome

| Prediction | Outcome |
|---|---|
| Fewer grazers born than in the controls by 12,000 s | Held: none born |
| Test 1 holds in at least 2 worlds | Failed in all 3 |
| Remains feed under a fifth of the grazers' intake; test 3 passes in all 3 | Held: about a fifth (0.18 to 0.24 in the last bin), passes in all 3 |
| Nutrients and absorbers fall below the controls', leaves gain | Leaves gained; absorbers and free matter rose instead |
| Grazers travel further per 1,000 s | Held in path, by up to twice; not in net distance |
| Remains standing a few hundred | Held |
| Pace at least 1 times real time | Held narrowly |
