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
