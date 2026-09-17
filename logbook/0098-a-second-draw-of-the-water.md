# 0098 — A second draw of the water

**2026-09-13, evening**  ·  pre-registration of the fresh-seed batch, seeds 6 to 10 on round 37b's world and build (D091); committed before the queue starts; not a round

## What it asks

Every round from 28 to 37b has reused five founding lotteries, seeds 1 to 5, through nine
rounds of adaptive change (D091's own words). Round 37b read the goal rule five of five, a
standing jointed population in two seeds and a rigid eater clade founded late in four
(0097). Whether those are the world's or the seeds' is the one question a second draw
answers. It costs no build: the same launcher, the same hashes, five seeds nobody has
run. It is a batch on the base and not a treatment. Nothing is adopted or rejected on it,
and the goal rule is read as the base's own count for D081's reference bar from here on.

## The world

Round 37b's world, unchanged: 0095's five changes on round 37's tank. The launcher is
`rounds/launch-r37b.ps1` with `-Seed 6` to `-Seed 10`, at `simHash 5e164d01…`,
`coreHash ad5c952a…`, `configHash
2430e660` and `physicsJobWorkers 0`. The seed is not in the config hash, so every manifest
must carry the same one. The seeds run 30,000 s at dt 0.01, launched through
`launch-queue.ps1 -Prereg` as the round 37b renders free workers. The one-part control
`r37bc-s5` (0097) is already on worker 6. The trace fix and `field cv` are not in this build;
they land after the last of these seeds has launched.

## Rules, before scoring

V1: every header carries the tokens 0095's V1 names with `fluidAccel 1`, and every manifest
the three hashes above, `physicsJobWorkers 0` and `prereg.json` naming this entry's commit.
V2: `audit` 0.0000% and `mat resid` 0 on every row. V3: `wraps` 0. V4: every diverged dump
has its trace file. V5: a manifest reading `error` or `stopped` is censored and read at its
last sample, as 0097 read seed 5. The entry is not written until the world has been watched
in the theatre and frames of a live arm looked at during the run.

## Predictions

Round 37b's five at 30,000 s: `alive` 1,674 / 1,735 / 1,840 / 1,782 / 1,764 (the last at
29,200 s); the rim quarter 0.19 to 0.27; `cols` 98 to 100; `x sd` 2.66 to 2.90; the goal
rule five of five with the passing clade a rigid eater founded after 1,000 s in four seeds;
seeds holding at least 10 inherited jointed bodies at the end: two; `diverged` 0, 0, 1, 5,
37, 3.9 per million jointed body-seconds, all at birth; stillbirths 12 to 449.

| # | prediction | falsified by |
|---|---|---|
| F1 | **the world stands**: `alive` at 30,000 s between 1,250 and 2,300 (round 37b's range widened by a quarter) in at least 4 of 5 | `alive` |
| F2 | **the water does not gather**: the rim quarter between 15% and 40% at 5,000, 15,000 and 30,000 s in at least 4 of 5, and the pooled radial drift over 100 s below 0.01 m in every seed (37b: 0.0025 to 0.0032; round 37: 0.020 to 0.049) | `p3` over `alive`; `drift.tsv`'s method against the fully-mixed bound |
| F3 | **the middle is filled**: corrected `cols` at least 60 from 5,000 s and `x sd` 2.0 to 3.4 m at the three times, in at least 4 of 5 | the reader |
| F4 | **the eaters are the world's**: D063 as amended met in at least 3 of 5, and the passing clade founded after 1,000 s (a mutant line, not a founder's) in at least 2 of 5 | the scorer |
| F5 | **the joint's fate is the world's**: seeds holding at least 10 inherited jointed bodies at 30,000 s between 0 and 3; if 0 of 5, the standing jointed populations of rounds 37 and 37b were the founding lotteries' | `jnt inh` |
| F6 | **the tank throws newborns**: `diverged` between 0 and 10 per million jointed body-seconds in at least 4 of 5, and every dump a jointed body aged under 10 s with a joint mass ratio under 3 (37b: 40 of 43 under 3 s, ratios 1.0 to 2.4) | the manifests, `diverged-anatomy.tsv`'s method |
| F7 | **the crowd is the box's**: the three-dimensional nearest neighbour at matched abundance within 0.8 to 1.35 of round 36's in at least 3 of the anchors that match (37b: 0.99 to 1.32) | `nn_matched.py` |
| F8 | **the pace is 37b's**: wall seconds per 1,000 simulated seconds per living body within 0.7 to 1.4 of 37b's five (as `pace.tsv` normalises) | `run.json` |

Recorded and not predicted: stillbirths against births (0097's open reading), `det patch
sd` and `patch max share`, and the founding times and kinds of every passing clade.

## The two-sided readings

- If F1 to F8 hold, round 37b's readings are the world's. The base's reference count is
  ten seeds' worth; round 38 dilutes it.
- If F4 fails on the count (fewer than 3 of 5), the eaters' standing in 37b was the
  seeds'. The base still stands, since a replacement is not adopted on the goal rule.
  D081's reference bar for round 38 is then the ten seeds' count rather than five of five,
  and 0079's question about the eaters' recruitment reopens on the tank.
- If F4 fails on the founding time (the passing clades are founders' lines), the late
  rigid eater of 37b was one draw's; the entry says so and nothing moves.
- If F5 reads 0 of 5, the jointed populations of the tank rounds were the lotteries'.
  Round 41's stroke price and the fluid terms (the path's 6b) are read against that, and
  the fresh seeds become the joint's control from here on.
- If F5 reads 4 or 5 of 5, the joint stands more often in a second draw than in the
  first, which the first five seeds under-read. That is the same consequence, arrived at
  the other way.
- If F6 fails upward (a seed above 10 per million), the throws are the world's and not
  seed 5's. The control `r37bc-s5` and the trace fix say whether they are the force's.
  If F6 fails on the anatomy (an adult thrown, or a mass ratio over 3), there is a second
  mechanism beside the newborn's; the trace, once fixed, is the instrument.
- If F2 or F3 fails, the mixed disc of 37b was not the world's, which would contradict a
  tracer theorem; the reading is the reader's before it is the water's.
- If F8 fails, the pace moved with nothing in the build changed, so the machine is the
  cause (the renders, the control); recorded and not read.

## What the batch does not ask

Not any of the ten rounds' questions. Not which of 37b's changes did what (D091). Not the
throws' mechanism: that is the control's and the trace fix's. Not pockets: `field cv`
lands after these seeds launch and reads round 38.

## Launch

The queue armed at 15:42 on 2026-09-13, `launch-queue.ps1 -Refresh -Prereg` on this entry at
`237e7ce`. It held seeds 6 to 10 on workers 2, 3, 4, 5 and 7 under the cap of five, and waited
eight hours behind round 37b's renders and the control. *23:30.* Seed 6 launched on worker 3,
refreshed first, header verified (`space tank r=5.64 m (100 m2), depth 60, wall, bed`,
`fluidAccel 1`, `dispersal=5 m`, `driveLimit >0.01`, `linkPhoto 0.5`, `addedMass 0.5`,
`dt=0.01`, seed 6), `simHash 5e164d01…`, `coreHash ad5c952a…`, `configHash 2430e660`,
`physicsJobWorkers 0`, `prereg.json` naming this entry's commit. The queue's log is
`scratch/logs/r37b-fresh-queue.out`. *2026-09-14 00:10 and 00:11.* Seeds 7 and 8 launched on
workers 4 and 5 as the control and a render ended. Each worker was refreshed first, with
headers and hashes as seed 6's and their own seed. *03:06.* Seed 9 launched on worker 2 as seed
3's render ended, worker refreshed first, header and hashes as seed 6's with seed 9,
`prereg.json` at the same commit. *08:23.* Seed 7 landed first, at 30,000 s on its budget after
eight hours with 1,585 alive. Seed 10 launched on its worker, 4, refreshed first, header and
hashes as seed 6's with seed 10, `prereg.json` at the same commit. The queue is done, every
seed launched.

Seed 7's provisional look, before the read: the goal rule passes on a rigid eater clade
founded at 1,083 s, with 105 alive at the end. The column `jnt inh` peaked at 50 near
5,000 s and read 0 from 15,000 s. Then `diverged`, `wraps` and `stillb` read 0, with
`audit` and `mat resid` 0 on every row. And `cols` reads 100 of 100, `x sd` 2.6 to 2.9 m
throughout.

*10:15.* Seed 8 landed at 30,000 s on its budget with 1,831 alive. *10:35.* Seed 6 landed
the same way with 1,811 alive. *13:20.* Seed 9 landed the same way with 1,722 alive; the
renders of seeds 7, 6 and 8 run on workers 6, 3 and 5. *22:20.* Seed 10 landed the same
way with 1,759 alive, fourteen hours on a machine carrying three renders. Every seed of
the five ended on its budget, and none threw a body. With that, the read starts.
