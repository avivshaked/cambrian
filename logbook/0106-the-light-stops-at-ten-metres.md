# 0106 — The light stops at ten metres

**2026-09-18, morning, pre-registered before launch**  ·  round 40: round 39's world with the light's reach halved (D096, `EVOSIM_LIGHT_REACH` 6 m), five seeds, three arms at a time (D095), read for the leaves' band, the shading in it and the dark water under it; the baselines are 0105's

---

## What it asks

One thing, with three faces. Round 39 read the shelf as never in the light (0105). The irradiance falls by a factor
of e every 12 m, so the leaves float at 16 to 20 m, the shallow arc of the floor at 30 m
sees a third of the light they do, and 45 m of the water is lit enough for a leaf to
breed in. The owner looked at the pictures and saw a world of leaves from top to bottom.
They asked what a faster decay would do: make photosynthesis pay only near the surface,
so that the water has a lit band and a dark column. The ledger sweep in D096 prices it.
At a reach of 6 m a lone leaf's expected children per lifetime reaches one at 10 m and
not at 20. Its children come three times as fast at 6 m as at 10 m. So the round asks
**whether the leaves rise into a band**, **whether shading becomes a selected quantity in
it**, and **whether the dark water below becomes the eaters' column**, where the detritus
sinks and no leaf can follow.

## The world

Round 39's launcher with one dial (`rounds/launch-r40.ps1`): `EVOSIM_LIGHT_REACH` 6 m,
the light model's attenuation depth, 12 m in every round before this one, carried in the
header as `light reach 6 m` and in the config as `attenuationDepth 6`. Everything else
is round 39's. The tank is 2,200 m² and 45 m deep, the bed has 1.5 m of relief on a 30 m
tilt, the matter is 11,000 units, the streams run at 0.1 m/s with the acceleration force,
corpses decay at 0.005/s, and the step is dt 0.01 for 30,000 s. The build has moved since
round 39 for the timing split and the dial (`simHash 302df848…` on the smoke, `coreHash
96488dec…` unchanged). Every worker is refreshed and the queue checks the hash. The top ten metres of the disc are
22,000 m³ of the tank's 99,000. The smoke (`r40smoke`, 600 s at dt 0.02) founded 161
bodies at a mean depth of 3.2 m, against round 39's 7 m at the same second, which is the
founding and not the world.

## Rules, before scoring

V1: every header carries round 39's tokens and `light reach 6 m`; every config
`attenuationDepth 6`; every manifest one `simHash`, one `coreHash`, one `configHash`,
`physicsJobWorkers 0`, and `prereg.json` naming this entry's commit beside the arm and
beside the run. V2: `audit` 0.0000% and `mat resid` 0 on every row. V3: `wraps` 0 on
every row. V4: every diverged dump has its trace. V5: a manifest reading `error` or
`stopped` is censored and read at its last sample; the wall is 1,800 minutes, half again
round 39's measured pace at three arms. The entry is not written until the world has been
watched in the theatre, and frames of a live arm are looked at at about 3,000 and 6,000 s.

## Predictions

Round 39's five seeds (0105, `logbook/specs/r39-read/`): the leaves' median depth 15.9 to
20.1 m at 15,000 s and 9.9 to 20.8 m at 30,000 s, their third quartile 26.4 to 31.6 m at
15,000 s; `shade %` 1.2 to 3.5% at 15,000 s and 0.9 to 3.1% at 30,000 s, with run maxima
of 2.7 to 4.7%; the eaters' median 3.8 to 21.0 m below the leaves' at 15,000 s; the first
boom above 550 falling under a sixth of its peak 1,900 to 2,600 s after it, five of five;
the matter in living bodies 0.700 to 0.754 of the stock at 30,000 s; `alive` at 30,000 s
1,920 to 2,714, mean 2,397; births by 1,000 s 313 to 745; the rim quarter 0.225 to 0.339;
no throw over 9.36 million jointed body-seconds; the pace 860 to 904 wall seconds per
1,000 simulated seconds per 1,000 bodies at three arms.

| # | prediction | falsified by |
|---|---|---|
| L1 | **the leaves rise into a band**: the photosynthetic median depth shallower than 10 m at 15,000 and 30,000 s in 4 of 5, and their third quartile shallower than 15 m at both times in 4 of 5 | `positions-read.py`, the leaf rows of the depth table |
| L2 | **shading is selected**: `shade %` above round 39's five-seed maximum at the same second (3.5% at 15,000 s, 3.1% at 30,000 s) in 4 of 5 | `shade %` at every 1,000 s |
| L3 | **the eaters take the dark column**: the absorptive median depth at least 10 m below the leaves' at 15,000 s in 4 of 5 (round 39 read 3 of 5 at that bar) | the depth table by guild |
| L4 | **the bust slows**: the first boom above 550 inherited eaters falls under a sixth of its peak later than 2,600 s after it, or not before the budget, in 4 of 5 | `inherit` at every 100 s, `booms.py` |
| L5 | **more of the matter is free**: the matter in living bodies below 0.70 of the stock at 30,000 s in 4 of 5 | `matterLocked` over `matterStanding` in `stats.jsonl` |
| L6 | **the crowd is still the matter's**: `alive` at 30,000 s between 1,680 and 3,600 (0.7 to 1.5 of round 39's mean) in 4 of 5, and births by 1,000 s at least 40 in every seed | the timeline |
| L7 | **the disc stays mixed**: the rim quarter 15 to 40% at 5,000, 15,000 and 30,000 s in 4 of 5, and `cols` within 0.85 to 1.05 of the uniform expectation 1 − e^(−n/2211) at the same samples in 4 of 5 | `p3` over `alive`; `cols` against `alive` |
| L8 | **nothing throws**: `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5 | the manifests, `diverged-read.py` |
| L9 | **the pace is round 39's**: wall seconds per 1,000 simulated seconds per 1,000 living bodies between 600 and 1,300 at three arms in 4 of 5 | `run.json`, `pace-survey.py` |

Recorded and not predicted: the share of the living above 3 m, the surface film, at the
three times; the leaves' adult scale, investment and lift by depth band, which is rung 2's
candidate reading; how many seeds hold an absorptive clade at 30,000 s (round 39's two of
five is a 2-or-3-of-5 reading, and D095 says five seeds cannot decide it, so it is a count
here and a ten-seed question later); the jointed count's course; stillbirths and
`crowded`, which a band packs; `corpses`; the footer's wall split, the first at a full
crowd; the crowd's centre along the tilt, which leaned deepward in round 39 (0105) and has
no reason to under a light that does not know the floor; `floor J` and the free matter by
depth, since the dark column is where the sediment goes.

## The two-sided readings

- **L1 fails with the median above 3 m:** the reach is too short and the leaves are in the
  film. 9 m is the retry, and the round is not read for L2 to L5.
- **L1 fails with the leaves still at 15 to 20 m:** the light is not what sets the band. The
  ledger says a leaf at 18 m under a 6 m reach sees a twentieth of the surface light and
  cannot breed. A crowd living there means the ledger is missing something the world has,
  and the read prices a leaf from the round's own snapshot at its own depth.
- **L2 fails with L1 holding:** the band is thin enough that no leaf shades another at
  this matter; read the leaves per cubic metre in the band against round 39's.
- **L3 fails:** the eaters sit with the leaves whatever the light does, and the dark column
  is not used; read `det deep` and the corpses' depth for where the detritus actually is.
- **L4 fails:** the dark column is not what limits the eaters, and the read turns to the
  matter (L5) and to what the eaters ate (0101's larder).
- **L5 fails:** the leaves lock the matter whatever volume they live in; the matter binds,
  the light does not.
- **L6 fails low:** the lit volume binds where the matter did. Read `crowded`, `stillb` and
  `mat blk` against births, and the free matter below the band, which the leaves cannot
  reach.
- **L7 fails on the rim:** a crowd stacked at the top rides the gyre's surface flow into
  the glass; read the leaves' and the eaters' rim shares apart.
- **L8 fails:** a throw in a crowd packed into 22,000 m³; the dumps' anatomy against 0097's.
- **L9 fails high:** the physics' share grows faster than the crowd in a packed band; the
  wall split says whether it is the physics or the world.

## What the round does not ask

The shelf waits for this round's band. A floor raised into the lit ten metres over part
of the disc is the next round, and its predictions take L1's band as their baseline. The
long arm for the oscillation, the tempo dial and the light sense wait behind it.

## Launch

*Appended as the seeds launch; each with its header verified and its manifest's hashes.*

*08:02 to 08:03.* Seeds 1, 2 and 3 launched on workers 2, 3 and 4, each refreshed first, with
the queue's hash check against the smoke's `simHash 302df848…` passing and `prereg.json`
at this entry's commit (`8e77099`) beside the arm and beside the run. Every header
verified from `runs/r40-s<n>.md`: `light reach 6 m`, `space tank r=26.46 m (2200 m2),
depth 45, wall, bed relief 1.5 m tilt 30 m scale 17.64 m`, `matterBudget 11000`,
`fluidAccel 1`, `dt=0.01`, `physics jobs 0`, **`simHash 302df848…`, `coreHash 96488dec…`,
`configHash 866b711a`**; every config `attenuationDepth 6`. The floors are round 39's,
seed for seed (seed 1 no hollow and one ridge, seed 2 three and one, seed 3 two and one),
since the bed is drawn from the seed and the bed dials did not move. Seeds 4 and 5 launch
as arms end, the queue's worker list being the three-arm rule; the wall is 1,800 minutes.
The first look, at 1,400 to 1,900 s: 713 / 793 / 742 alive, mean depth 4.2 / 1.9 / 4.3 m
against round 39's 6 to 17 m at the same seconds, shading 2.3 to 2.7%, no eater yet.
Seed 2's crowd is nearest the film; the reading is L1's at 15,000 s, not the founding's.

*2026-09-18, 16:57.* **Stopped by the owner's ruling** (D098: the economy is rebuilt as one
substance and every current run stops). Seeds 1, 2 and 3 ended by `stop-arm.ps1` at 16,400,
12,200 and 14,300 s, `manual-other` with the note in each manifest; seeds 4 and 5 never
launched. Under V5 the round is read at its last samples, and the read says which of L1 to
L9 could be read at all. Seed 1's 15,000 s reading was in: the leaves' median about 14 m
against L1's 10 m bar, with the depth-by-age table (HANDOFF, 2026-09-18) showing the deep
half of the crowd as newborns dropped at 16 to 25 m by the few parents whose cell could
afford a child, most of them dead within 500 s.

## The read, at the last samples

*2026-09-18, 19:10; `logbook/specs/r40-read/` (`analyse.py`, `pos_analyse.py`, `booms.py`,
`birth-depth.py` and their tables).* Three seeds of five, stopped at 16,400, 12,200 and
14,300 s. V1 to V4 hold: every header token and hash as launched, both books closed on every
row, no wrap, and the round's one divergence (seed 2, body 1144 at 1,516 s) a two-part
jointed newborn with a joint mass ratio of 1.34, round 37b's signature, dumped with a trace
whose ring held three finite frames, so 0097's "the trace misses the onset" is fixed in this
build. The theatre was pointed at seed 1 at 3,000 and 6,000 s (the frames in HANDOFF's early
look): leaves in the top quarter, packed at the waterline, the lower thirty metres empty at
3,000 s; the crowd filling the top half to about 22 m at 6,000 s. Under V5 a bar named at
30,000 s is not readable and a bar named at 15,000 s is readable in seed 1 alone; the
number at the last sample is carried as a reading. The round cannot decide any "4 of 5".

| | seed 1 (16,400 s) | seed 2 (12,200 s) | seed 3 (14,300 s) |
|---|---|---|---|
| L1 leaf median, deep quartile | 12.4 m, 23.0 m at 15,000 s: **fails** both clauses; 13.1, 25.1 at the end | 4.1 m, 16.2 m at the end: the median passes, the quartile misses by a metre | 6.9 m, 22.8 m at the end: the median passes, the quartile misses badly |
| L2 shade % | 1.6% at 15,000 s: **fails**; run max 3.0% at 2,000 s | 2.7% at the end; max 3.9% at 3,300 s | 2.6% at the end; max 4.1% at 4,900 s |
| L3 eaters below leaves | 21.2 m below at 15,000 s (n 299): **holds**; 18.4 m at the end | 10 eaters at the end, 2.7 m above the leaves: not readable | 19.5 m below at the end (n 921): meets the bar |
| L4 the bust | `inherit` 470 at the end and rising, never 550: not readable | max 7, the eaters never founded | 913 at the last sample, still climbing: not readable |
| L5 locked share | 0.72 at the end | 0.71 | 0.75 |
| L6 alive; births by 1,000 s | 2,490; 283 **holds** | 2,503; 301 **holds** | 2,599; 471 **holds** |
| L7 rim share; cols of uniform | 0.28, 0.27 at 5,000 and 15,000 s; 0.94, 0.97: **holds** | 0.32; 0.89 at 5,000 s: **holds** | 0.26; 0.92 at 5,000 s: **holds** |
| L8 diverged per million jointed body-seconds | 0 of 0.36 M: **holds** | 1 of 5.29 M, 0.19: **holds** | 0 of 2.63 M: **holds** |
| L9 wall s per 1,000 s per 1,000 bodies | 957: **holds** | 1,283: **holds**, at the top of the band | 1,075: **holds** |

Depth by guild at the last sample: leaves' median 13.1 / 4.1 / 6.9 m against round 39's
15.9 to 20.1 m at 15,000 s, and the eaters' 31.4 / 1.4 / 26.4 m. The share of the living
above 3 m fell through every run (0.37 to 0.19, 0.51 to 0.45, 0.53 to 0.26 between 5,000 s
and the end). Shading peaked in every seed by 5,000 s, inside round 39's own range, and fell
after. Births by first depth, all three seeds together: born at 0 to 4 m, none dead within
500 s and 52 to 70% ever breed; born at 16 to 22 m, half dead within 500 s and 8 to 9%
breed; born below 22 m, 68 to 91% dead within 500 s. Residents older than 3,000 s sit at
2 to 11 m while the newborns of the last 2,000 s sit at 13 to 28 m, and a newborn's depth
minus its parent's is 0.0 m in every window of every seed: the disc disperses sideways.

**What I read in it.** The leaves rose and did not form a band. Against round 39 the one
comparable seed is 3.5 m shallower at 15,000 s and the two shorter seeds are shallower
still, but every seed's deep quartile is 16 to 25 m: a shallow mode with a long deep tail,
not the 0 to 15 m band the ledger priced. The tail is mortality, not habitat. A child is
set down at its parent's depth, the parents that can still afford a child once the lit
water fills are the deep ones (HANDOFF's cell-affordability reading), and their children
are dropped where nothing pays and die inside 500 s. So the leaves' median deepening
through a run is a birth statistic, and I would not read it again without conditioning on
age. L3 is the one substantive prediction that held where it could be read: with eaters
founded, the dark column is theirs, 19 to 21 m below the leaves and more numerous than
round 39's; seed 2's eaters never founded, one founding failure in three. Shading was not
selected and went the other way, which under the entry's two-sided reading has no
assigned meaning because L1 did not hold; my inference is that the crowd spread down the
tail rather than stacking in the band. And nothing about the matter or the crowd moved
(locked share, alive, rim, cols, pace all inside round 39's), so the reach changed where
bodies are and not how many there are. The constraint was still the matter, which is what
this stopped round hands to D098.

Two instrument notes. `pace-survey.py` cannot read a stopped arm (it wants the footer
`stop-arm.ps1` kills before it is written); `pace.tsv` takes the wall from the manifest
window and from the last row's `wallTotalMs`, which agree within 1%. And the wall split at
a full crowd reads physics 22 to 25%, world 11 to 14%, harness 62 to 65%, against the
founding smoke's 4 / 85 / 11: at a crowd it is the harness, not the grid (CLAUDE.md's
gotcha corrected). The 16:57 note's "about 14 m" was the all-guild median; the leaves' is
12.4 m, and the verdict is the same.
