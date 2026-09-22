# The same world on the new engine

*2026-09-22, 03:20. Written by the agent as the pre-registration of round 43, the base round
on the farm out of Unity, on the owner's ruling of the same night that the water is sampled
per link (D104). The predictions are committed before the three seeds launch.*

## What it asks

Round 42 (0110) was the last round on the Unity farm. The same night the owner ruled the
engine before the world, and a solver of our own with the farm around it ran round 42 seed
1's world in 24 minutes where Unity took ten hours, both books closed, with the same
trajectory at any thread count (HANDOFF, 2026-09-22). Two things changed under the bodies
on the way. A body's own parts no longer collide, and the parity swims found that in
Unity they did, so that a driven joint was mostly not free to turn. And the water is
sampled once per link on every step (D104), where round 42 held one sample per body for
half a second and a neutral body drifted 22 m from its parcel in 1,000 s.

So the round asks whether round 42's world, on the new engine, is still round 42's world:
the crowd, the treadmill, the larder, the spread, and the one reading that the engine
could have been hiding, the fate of joints. It is D091's replacement round, read for the
mechanism and not for a bar, and it is the first round whose entry is written from the
farm's own output.

## The world

Round 42's, with the water hold at 0: `rounds/env-r42.ps1` through `run-farm.ps1` with
`EVOSIM_WATER_HOLD=0` and `EVOSIM_THREADS=8`, seeds 1 to 3 at once, 30,000 s at dt 0.01.
The engine is `Evosim.Dynamics` on main at `9fb08e5` or later (the hashes are read from
each manifest at launch and recorded in HANDOFF). Contact between bodies is a soft push
between bounding spheres, bounded by `ContactLaw`; the contact instrument is the overlap
census (`overlaps`, `ovl/body`), which does not compare with round 42's pairs. Everything
else is round 42's launcher, which the farm reproduces to the byte (`configHash` differs
only by the hold).

## Rules

A manifest reading `error` or `stopped` is censored and read at its last sample. A seed
past 2,500 bodies is stopped. Three seeds, not five (D095): the round is a reading of the
engine against a known world, and a 2-of-3 clause is written where round 42 read 3 or 4
of 5.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| G1 | **the crowd is round 42's**: `alive` between 250 and 650 at 5,000 s and between 500 and 1,400 at 30,000 s, in 3 of 3 (round 42: 385 to 548 and 843 to 1,162) | the timeline |
| G2 | **the books close and nothing is lost**: `audit` under 0.1 J at every sample, the matter residual under 1e-3 units at 30,000 s, `diverged` 0, in 3 of 3 | `stats.jsonl`; the manifests |
| G3 | **the pace**: each seed at 8 threads beside two others runs at 8x real time or better over the whole seed (alone at 16 threads round 42 seed 1 ran 20.5x) | the footers |
| G4 | **the treadmill and the larder**: `upt lim` between 40 and 95% and the marine snow between 0.10 and 0.40 of the budget at 15,000 and 30,000 s, in 3 of 3 | the columns |
| G5 | **the eaters stay at zero**: `inherit` does not reach 25 after 5,000 s in any seed (round 42: at most 21) | `inherit` |
| G6 | **joints fade without the jam**: the jointed count peaks above 400 between 5,000 and 20,000 s and ends under half its peak in 2 of 3 (round 42: 3 of 5; the one farm replay, 597 to 115). If it holds, the fade is the economy's and not the physics'; if the joints hold in 2 of 3, the jam was a cause and the entry says so | `jointed` by sample |
| G7 | **the disc stays mixed and the water carries**: the rim quarter 0.12 to 0.40 and `cols` within 0.85 to 1.05 of the uniform count at 5,000, 15,000 and 30,000 s in 3 of 3 | `p3` over `alive`; `cols` |
| G8 | **the sieve**: `self stillb` under 10% of the window's births at 5,000, 15,000 and 30,000 s in 2 of 3, the floor silent by 1,000 s in 3 of 3 | the differenced counts |
| G9 | **open bodies**: the median body at 30,000 s has at least 2 parts, no body at 16, fewer than 1% at eight or more, in 2 of 3 | the overlap probe on the snapshots |
| G10 | **the overlap census is a state, not a bar**: `ovl/body` at 5,000, 15,000 and 30,000 s is recorded per seed for the next round to read against | the column |

## The two-sided readings

- **G1 and G4 hold, G6 holds:** the engine changed the physics and not the ecology, and
  the joint's fate is the economy's. The animal kit's first rung (what a joint is for) is
  the next build.
- **G6 fails with joints held:** the jam was selecting muscle out. Every reading about
  joints from rounds 34 to 42 is reread with that, and the campaign has been under-reading
  movement since the free joint.
- **G1 fails:** the engine's contact or water changed the crowd. Read `ovl/body`, the
  stillbirths and `margin s` before anything; a soft push that keeps bodies apart where
  PhysX let them wedge would show in the crowding refusals.
- **G3 fails:** three seeds at eight threads each share the machine worse than the
  thread-count tests said. Read the wall split; run the next round two at a time.

## What the round does not ask

Thrust: no body in round 42 stroked, and nothing here makes a stroke pay. The bed, still.
Whether the theatre can draw the farm's record: `-From snapshot` reads the files the farm
writes unchanged, and the pictures at 5,000, 15,000 and 30,000 s are the test, taken after
the seeds end.

## Read at 30,000 s, three seeds of three

*2026-09-22, morning. Every seed ended at its budget in 74 to 86 minutes. The clause
readings are `logbook/specs/r43-read/clauses.tsv`, with the script beside it; the pictures
are the first the theatre has drawn from the farm's own files.*

| # | held in | reading |
|---|---|---|
| G1 | 3 of 3 | 401 to 447 alive at 5,000 s, 838 to 1,334 at 30,000 s (round 42: 385 to 548, 843 to 1,162) |
| G2 | 3 of 3 | the energy residual under 0.027 J at every sample, the matter residual 0.9 to 1.9e-04 units, nothing lost |
| G3 | 0 of 3 | 5.8, 6.6 and 6.7x at 8 threads each, three at once; physics 88 to 89% of the wall |
| G4 | 3 of 3 | `upt lim` 78 to 86%; the snow 0.15 to 0.22 of the budget |
| G5 | 3 of 3 | the most inherited eaters after 5,000 s was 3 |
| G6 | 1 of 3 | jointed peaked at 675, 183 and 730 and ended at 476, 1 and 687: **the joints held in seeds 1 and 3** (0.71 and 0.94 of the peak), and seed 2's never reached 400 |
| G7 | 3 of 3 | rim quarter 0.20 to 0.31; `cols` 0.98 to 1.00 of the uniform count |
| G8 | 1 of 3, and 3 of 3 | `self stillb` over the window's births 1.1 to 4.4% to 5,000 s, 2.4 to 7.7% to 15,000 s, then 11.9%, 4.8% and 10.9% to 30,000 s; the floor last fired at 400 to 580 s |
| G9 | 2 of 3 | the median body has 3 parts in every seed, none at 16; 0.9%, 0% and 1.3% at eight or more |
| G10 | recorded | `ovl/body` at 5,000, 15,000 and 30,000 s: seed 1 0.0001, 0.0020, 0.0238; seed 2 0.0001, 0.0015, 0.0458; seed 3 0.0119, 0.0019, 0.0055; `ovl held %` 93 to 99% throughout |

**What it looked like.** Seed 2 from the side at 30,000 s
(`logbook/images/r43-s2-t30000-recon-side.png`) is a shower of flat one-part leaves through
the top twenty-five metres of the column; seed 3 (`r43-s3-t30000-recon-side.png`) is
smaller jointed bodies spread deeper, with a few at the bed, which no body of round 42
reached. Both fill more of the column than round 42's top twelve metres. Seed 1 from
above (`r43-s1-t30000-recon-top.png`) is the disc filled edge to centre. Every picture is
drawn from a snapshot at adult size in the developer's frame.

**The engine changed the physics and not the ecology.** G1, G4, G5 and G7 hold as round
42 held them: the crowd, the treadmill, the larder, the spread and the eaters' zero are
the same world. G2 says the books close on the new farm over a round and not only over a
seed.

**The jam was a cause.** G6's second reading is the one the entry named: the joints held
in two seeds of three, where round 42 lost them in three of five and the one seed replayed
on this engine before the round lost them too. On this world a joint still earns nothing
that a leaf does not, so what selection is keeping is not a stroke; my reading, marked as
inference, is that a joint that turns freely costs its body less than one jammed against
its sibling, since a jammed drive pays its work into a contact and a free one into the
water, and at equal income the free body keeps its margin. Seed 2 is the other outcome
on the same engine: its jointed line never passed 183 and was gone by the end, so the
fade is not the physics' alone either. Every reading about joints from rounds 34 to 42
now carries the caveat that the physics was against them, and the count of rounds in
which muscle was "selected out" is not evidence about the economy.

**The pace, and what it says about threads.** G3 failed in every seed, and not because a
seed is slow: three seeds at 8 threads each gave 19x in total, the same total as one seed
at 16 threads gave alone (20.5x), and physics is 88% of the wall. Twenty-four threads on
twenty-four cores of which eight are fast is one machine's worth of solver, however it is
divided. A round of three runs in eighty minutes either way; the rule for the next is one
seed at a time at 16 threads when its wall matters and three at once when the round's
does, and the machine is the ceiling until the GPU.

**The sieve is at the line.** G8 read 11.9 and 10.9% in the two seeds that kept their
joints and 4.8% in the one that lost them: the refusal grows with the muscle, as in round
42, and on this engine it is the one contact cost a jointed line still pays. Whether it is
a wall is now the question for a round that gives a joint something to do.

**What the round did not settle.** Thrust, still: nothing strokes. The read script's
sampling assumption: the farm writes a stats row every 10 s where Unity wrote one every
100 s, so `r41d-read.py`'s jointed body-seconds and its snow at the default budget read
ten and two times off on a farm run until the script reads the interval from the rows,
and this entry's numbers are from `clauses.py`, which does.
