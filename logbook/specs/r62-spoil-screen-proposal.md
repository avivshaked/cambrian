# Proposal: spoiling corpses crossed with the mouth's reach

This is for the coordinator to rule on overnight, on the owner's leave. The speed agent prepared it on 2026-10-19,
after D212. Nothing launches without the coordinator's yes.

## Where this comes from

The grazer screen (`logbook/specs/r62-grazer-screen/README.md` on `r62-graze`) found that only the mouth's reach
moved the grazers. At 2.0 m they bred 165 against the control's 9, and their kept intake rose from about a sixth
of their upkeep to four fifths. They did it by scavenging corpses they had not made. In 500 s windows they starve
in the first thousand seconds of summer, when fresh corpses stop falling, and they do not turn to living leaves
(`scripts/reads/r62g-windows.py`).

D212 rules a spoilage clock: a corpse's food value halves every H seconds, the lost energy leaves as heat, and its
matter goes to the spent field. The clock is built on `r62-spoil` behind `EVOSIM_CORPSE_HALF_LIFE`, default off.
The coordinator ruled overnight under D212 that the dial covers corpses only. Detritus already spoils this way
through remineralisation, whose half-life is about 1,386 s, and a screen may vary that rate beside the half-life.
D212 also rules that reach is tuned with spoilage on, since spoilage shrinks what reach won.

## The half-lives

Two values, from the ocean and from the world's own clocks:

- **500 s, half a world day.** Microbes spoil carrion within a day or two in the sea. Fish carrion left a day or two
  was about four times less likely to be eaten (`logbook/specs/matter-ocean-scan.md`; that source was seen only as
  a press release). At 500 s a corpse loses three quarters of its food in a world day, so only a fast scavenger or a
  hunter gets the best of it.
- **2,000 s, two world days.** About as slow as the corpse's own decay into detritus (a half-life near 1,800 s at
  `corpseDecayPerSecond` 0.000385) and the detritus's remineralisation (1,386 s). It halves what a slow scavenger
  finds without making carrion worthless.

A sinking corpse here leaves the world once it holds under 5% of its first joules. Without spoilage that takes about
7,800 s; at 2,000 s it takes about 4,100 s, and at 500 s about 1,700 s. Those figures are worked from the rates,
not measured; the screen's read will measure them.

## The arms

One card group of eight worlds, 12,000 s each, on `r62-spoil`'s build and the grazer screen's world:

| World | Seed | Reach | Half-life | Why |
|---|---|---|---|---|
| `r62s-r2-h500` | 6201 | 2.0 m | 500 s | the reach that worked, hard spoilage |
| `r62s-r2-h2000` | 6201 | 2.0 m | 2,000 s | the reach that worked, mild spoilage |
| `r62s-r3-h0` | 6201 | 3.0 m | off | the next reach, without spoilage |
| `r62s-r3-h500` | 6201 | 3.0 m | 500 s | |
| `r62s-r3-h2000` | 6201 | 3.0 m | 2,000 s | |
| `r62s-c-h500` | 6201 | 0.5 m | 500 s | does spoilage alone push grazers to kill living leaves |
| `r62t-r2-h0` | 6202 | 2.0 m | off | a second seed for 2 m without spoilage |
| `r62t-r2-h500` | 6202 | 2.0 m | 500 s | a second seed for the key cell |

The control and 2 m without spoilage at seed 6201 are the grazer screen's own worlds. D212's long baseline extends
them to 24,000 s on their own build, so they are not run again.

The read is the screen's (`r62g-read.py`, `r62g-seasons.py`, `r62g-windows.py`, `r60-read.py`). For each cell it
asks four questions:
- does replacement reach one;
- does a grazer kill living leaves more often than it empties corpses it did not make;
- does the start of summer stop being a famine;
- do the living leaves stay above half the control's (D201's limit).

## Machine time

The grazer screen's seven worlds took 90 to 117 minutes of wall time for 12,000 s. Eight take about 2 to 2.5
hours, on the card with cores 16-29 and the heat stop. The coordinator may choose 24,000 s instead, to match the
long baseline, for about 4.5 to 5 hours. That figure assumes the living count stays near the screen's 1,400 to
1,700, which it did to 12,000 s and is not measured past it. Checkpoints every 1,500 s let a long run be read at
12,000 s and stopped there.

## The ruling and the launch

The coordinator ruled it as D213 overnight: the eight worlds as set out, every one at 24,000 s. The clock is built
on `r62-spoil` (92b61136). The group launcher is `rounds/launch-r62s.ps1` there, and each arm's launcher is
`rounds/env-r62s-<arm>.ps1`. Each arm's config hash was read from a 2 s founded run on that build; a config hash
does not depend on the seed.

| World | Seed | configHash |
|---|---|---|
| `r62s-r2-h500` | 6201 | `3d4dd684363d2bf4` |
| `r62s-r2-h2000` | 6201 | `67ec4cdd99843691` |
| `r62s-r3-h0` | 6201 | `4c4a6bac75591d2e` |
| `r62s-r3-h500` | 6201 | `c5c9f4167e070655` |
| `r62s-r3-h2000` | 6201 | `81298c13ed9bf940` |
| `r62s-c-h500` | 6201 | `d94611a9299d5e39` |
| `r62t-r2-h0` | 6202 | `090817dcb96e1b0f` |
| `r62t-r2-h500` | 6202 | `3d4dd684363d2bf4` |

`r62s-c-h500` keeps the control's own reach of 0.5 m. Its config is the grazer screen's control config, apart from
the half-life and the hash.

## A finding from the long baseline

The long baseline's two worlds reached 24,000 s on 2026-10-20 at 01:11, with the books closed except for one
line. In `r62gx-ctl` the matter residual drifted steadily, with no step between samples larger than 3 × 10⁻⁶. It
read −1.4 × 10⁻⁶ units at 2,000 s, −6.5 × 10⁻⁶ at 12,000 s and −2.54 × 10⁻⁵ at 24,000 s, by which time 4,418
bodies were alive. That passes the farm's line of 10⁻⁵, which was set on runs of 12,000 s. It is about 6 × 10⁻¹⁰
of the standing matter. My reading is that rounding in the deposits accumulates as the crowd grows, and that no flow
is lost; this is inference. `r62gx-reach` stayed under the line, at 6.6 × 10⁻⁶. The coordinator ruled that the
drift does not block the screen. Which deposit rounds, and whether the line should scale with a run's length, wait
for daylight.

Round 62, stopped at 24,330 to 26,610 s on 2026-10-23, shows the same. At its end read, the max |matter residual|
passed 10⁻⁵ in three of its six worlds: `r62-h6202` at 1.31 × 10⁻⁵, `r62-n6203` at 1.25 × 10⁻⁵ and `r62-h6203` at
1.29 × 10⁻⁵. The other three read 3.5 × 10⁻⁶ to 9.4 × 10⁻⁶. The energy books stayed under 0.1 J in all six
(`card-run-r62.log` in `scratch/wt-r62launch/scratch/logs/`). It gets a proper look before round 63's bounds are set
(the coordinator, 2026-10-23).

## The result

The eight worlds ran as one card group from 00:46 to 06:34 on 2026-10-20. Every world reached 24,000 s with its
books closed, and none diverged. The tables are in `r62-spoil-screen/` beside this note: `read.txt`,
`cohorts.txt`, `seasons.txt`, `windows.txt` and `r60-read.txt`. They read the eight worlds beside the long
baseline, whose two worlds stand for seed 6201's 0.5 m and 2 m without spoilage.

At 3 m the grazers keep themselves going, with or without spoilage. At 2 m spoilage cuts them back. Spoilage alone
does nothing.

| World | Reach | Half-life | Grazers bred | Deepest chain | Intake ÷ upkeep | Mouthed alive at 24,000 s |
|---|---|---|---|---|---|---|
| `r62gx-ctl` | 0.5 m | off | 35 | 1 | 0.22 | 19 |
| `r62s-c-h500` | 0.5 m | 500 s | 16 | 1 | 0.17 | 12 |
| `r62gx-reach` | 2 m | off | 1,334 | 35 | 1.09 | 126 |
| `r62s-r2-h2000` | 2 m | 2,000 s | 473 | 10 | 0.86 | 24 |
| `r62s-r2-h500` | 2 m | 500 s | 257 | 3 | 0.69 | 30 |
| `r62t-r2-h0` (seed 6202) | 2 m | off | 512 | 5 | 0.84 | 31 |
| `r62t-r2-h500` (seed 6202) | 2 m | 500 s | 534 | 6 | 0.86 | 15 |
| `r62s-r3-h0` | 3 m | off | 2,782 | 18 | 1.11 | 145 |
| `r62s-r3-h500` | 3 m | 500 s | 2,756 | 23 | 1.04 | 152 |
| `r62s-r3-h2000` | 3 m | 2,000 s | 1,483 | 27 | 2.63 | 59 |

The intake column charges every mouth's kept intake in the world to the grazers, so it is an upper bound on
theirs. The deepest chain is the longest run of mouthed parent to mouthed child.

The proposal asked four questions of each cell.

- **Does replacement reach one?** Only at 3 m. The measure is mouthed children per grazer, by the cohorts born
  from 12,000 to 21,000 s.

  | 3 m world | 12,000-15,000 s | 15,000-18,000 s | 18,000-21,000 s |
  |---|---|---|---|
  | without spoilage | 1.24 | 0.89 | 1.08 |
  | 500 s | 1.16 | 1.08 | 1.08 |
  | 2,000 s | 1.06 | 0.88 | 1.02 |

  No 2 m world passed one in any cohort. The best was 0.95, in the long baseline. Seed 6202 at 2 m stayed between
  0.42 and 0.86, with spoilage or without. The last cohort in every world is cut short by the end of the run.
- **Does a grazer kill living leaves more often than it empties corpses it did not make?** No, in any world with
  grazers. Corpses that no grazer killed fed most of the intake in every cell. Take 3 m at 500 s, in the first
  windows of the summer that began at 17,750 s. Kills brought in 5 to 6 kJ a window, and other corpses about
  24 kJ.
- **Does the start of summer stop being a famine?** At 3 m, yes. The windows from 17,500 to 19,000 s are the
  test. In them the 3 m grazers met 1.04 to 1.20 of their upkeep without spoilage, and 0.96 to 1.14 at 500 s.
  At 2 m on seed 6201 with spoilage they met 0.52 to 0.82.
- **Do the living leaves stay above half the control's?** Yes, in every world. The control held 3,403 leaves at
  24,000 s, and the fewest elsewhere was 3,589.

What spoilage does, cell by cell:

- **At 2 m on seed 6201 it cuts the grazers.** Bred fell from 1,334 without spoilage to 473 at 2,000 s and 257 at
  500 s. The deepest chain fell from 35 to 10 and 3. Kills per grazer-second rose a little (2.7, 2.9 and
  3.2 × 10⁻³), but there were fewer grazers to make them.
- **At 2 m on seed 6202 it changes nothing**, and 2 m never boomed there: 512 bred without spoilage, 534 at
  500 s. Seed 6201's 2 m boom does not repeat on a second seed. My reading is that 2 m sits at the edge, where a
  seed decides.
- **At 3 m and 500 s it changes almost nothing.** Bred was 2,782 and 2,756. A mouth that reaches 3 m still finds
  enough fresh corpses. Kills per grazer-second rose from 1.9 to 2.2 × 10⁻³, and kills of living leaves over the
  run from 1,675 to 1,890. Corpses emptied fell from 12,223 to 8,725.
- **Alone, at 0.5 m, it does nothing.** Sixteen grazers were bred, and replacement stayed under 0.11 in every
  cohort. Spoiling the corpses does not push a short-reach grazer onto living leaves.
- **It shrinks the store of corpses.** Take the 3 m worlds in the windows at 18,000 s. The standing corpse energy
  was about 230 kJ without spoilage, 120 kJ at 2,000 s and 60 kJ at 500 s.
- **The worlds with spoilage held more leaves**, in three pairs of four. At 0.5 m it was 4,812 against 3,403, at
  3 m 5,722 against 4,683, and on seed 6202 5,068 against 4,089. The pair at 2 m on seed 6201 went the other way,
  3,720 against 4,204. My reading, which is inference, is that a spoiled corpse's matter goes straight to the spent
  field, where the leaves take it up. An unspoiled corpse would have carried it down to burial.

The oases held in every world. Between 0.75 and 0.79 of the 10 m squares were empty at 24,000 s. Between 0.73 and
0.77 of the living sat on five islands. The desert stayed below the line, and burial ran at 0.89 to 0.95 of the
influx.

### The 2.63 in `r62s-r3-h2000` is the scavengers'

`r62s-r3-h2000` ended with the most alive (7,366) and the fewest grazers of the 3 m worlds (59). Yet its intake
column read 2.63. The column charges every mouth's intake to the grazers, and this world grew a second guild that
the column cannot see. It had 8,296 births of bodies with intake but no attack, against 183 in `r62s-r3-h0` and
196 in `r62s-r3-h500`. They cannot kill, so they live on corpses. The 43 grazers the read counts as born of unmouthed
parents were born of these scavengers, regaining the attack.

A read by body measures the split. A checkpoint carries each body's kept intake over its life
(`Organism.MouthJoules`). The read-only `--mouth-census` on branch `r62-mouth` (`552c4de6`) writes it for every
living body at every checkpoint. `scripts/reads/r62-mouth-read.py` on the same branch joins it to the birth rows.
Bodies that die between checkpoints take their meals with them, so the sums cover only 0.44 to 0.57 of the world's
intake. The read therefore also estimates each class's intake, from its survivors' rate over all its body-seconds.
That estimate covers 1.02 to 1.07 of the world's intake, a little over, since the bodies that survive have eaten
better than those that die. Its figures are upper bounds. The tables are `mouth-r62s-r3-*.txt` beside this note.

| World | Grazers' share of the intake | Grazers' intake over upkeep | Scavengers' share |
|---|---|---|---|
| `r62s-r3-h0` | 0.98 | 1.16 | 0.02 |
| `r62s-r3-h500` | 0.98 | 1.04 | 0.02 |
| `r62s-r3-h2000` | 0.41 | 1.15 | 0.59 |

The grazers of the three 3 m worlds therefore paid their way alike, at 1.04 to 1.16 of their upkeep. In
`r62s-r3-h2000` the scavengers took most of the carrion, and their 2.0 million body-seconds were three times the
grazers'. Each scavenger met about half its upkeep by the estimate, so they lived on less. Why a scavenger guild
grew only at a half-life of 2,000 s, on one seed, is not answered here.
