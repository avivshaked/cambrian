# The fast nursery's measurements (D134)

The four measurements D134 asks for before design A runs (logbook/specs/fast-nursery-proposal.md §5),
and what came out of building it. The nursery is `src/Evosim.Nursery` on the branch `nursery`
(`scratch/wt-nursery`). Written on the night of 2026-09-29 and extended as each measurement lands.

## A fault in runs 1 and 2: the corpses were laid about the wrong centre

A tank's axis is at (R, R), not at the origin (`World.KeepInTheWater`). The nursery clamped each
corpse inside the glass about the origin, so most corpses were dragged away from the body and some
past the glass. In the 100 m² tank, with the same seed and body, the nearest corpse sat 4.8 to 6.4 m
from the body before the fix and 0.9 to 4.0 m after it (0.3 to 1.2 m across; the rest is height). So
the ancestors' 8 to 13 m from food in runs 1 and 2 was mostly this fault, and so, very likely, was
their one-in-twenty hit rate. Run 2 was left to finish, as the owner ruled, and measures nothing
about foraging. Fixed as `67e3961`; `World.PlaceCorpse` now refuses a place past a tank's glass.

## The episode configs

Made by the farm's own binding from `rounds/env-r52.ps1` (a 1 s run on one thread each, under
`scratch/nursery/runs/`): round 52's rules at 100 m² by 5 m, the bed flat, no reef, no islands, no
trickle, the matter budget scaled to the volume (8 units), founders and islands at 2.5 m, the limb
push 0.3 and the scent on.

| arm | change | configHash |
|---|---|---|
| nursery-small | the search | 756e6120 |
| nursery-small-still | `EVOSIM_CURRENT 0` | ba990f55 |
| nursery-small-nomix | the three mixings 0 | e9ee8c0b |
| nursery-cfg-s | D133's episode, 400 m² by 10 m | f3e51b49 |

A tank takes a current of 0 (Fable had guessed this was untested).

## Measurement 4: the bodies' intake

All ten pooled bodies (two parts, one joint, a mouth and no leaf) carry intake 1, with adult mouths of
0.05 to 0.27 m³. A mouth takes intake times its surface area per second, so a 4 J corpse inside its
reach (0.5 m plus the corpse's radius) is emptied in a step or two: the score is a count of corpses
reached, as Fable assumed.

## First look at an episode in the small tank

Body 0 (creature 5210), 300 s, 200 J laid as 39 corpses of 5.1 J: 1.04 s of wall on one thread
(0.82 s stepping), against Fable's guess of 0.45 s. The body lived the episode and ate none. Its root
swam between 1.3 and 4.5 m below the surface while the corpses settled on the bed near 5.3 m, so it
passed within 0.3 m across of one and never nearer than 0.9 m in all. Whether these bodies can reach
the bed is the question the hit-rate measurement answers.


## Measurements 1 to 3 (2026-09-30, 03:46 to 04:18)

The script is `scripts/nursery-measure-d134.ps1`, run on the worktree's `Release-b` build. That build
was rebuilt at 03:38 so that it writes `final-per.jsonl`; the one of 23:37 predated `f45bc89`. All ten
pooled bodies took part. The carrion was fine (200 J in 25 to 100 pieces a draw) or coarse (5 pieces of
40 J), and the body started 0.5 m or 1.5 m above the bed. The reader is `scripts/nursery-read-d134.py`,
and the outputs are under `scratch/nursery/measure/`.

**Measurement 1** is the wall of one episode on one thread, for body 0.

| config | seconds | wall | income |
|---|---|---|---|
| small, current | 300 | 1.00 s | 18.2 J (14.6 of it corpses) |
| small, still | 300 | 0.42 s | 0.6 J |
| small, no mixing | 300 | 0.83 s | 18.7 J |
| D133's tank | 300 | 1.19 s | 0 |
| D133's tank | 900 | 2.68 s | 2.7 J (snow only) |

Still water is 2.4 times cheaper than the current. The mixing costs about a sixth.

**Measurement 2** is the hit rate: every ancestor on 32 episodes, 320 episodes a condition.

| condition | mean | sd | episodes over 5 J |
|---|---|---|---|
| small, fine, 0.5 m | 7.06 J | 10.2 | 38% |
| small, fine, 1.5 m | 7.75 J | 10.2 | 41% |
| small, coarse, 0.5 m | 13.39 J | 29.5 | 20% |
| still, fine, 0.5 m | 5.70 J | 8.7 | 38% |
| D133's tank, coarse, 1.5 m | 3.03 J | 10.6 | 6% |

D133's episode feeds one episode in seventeen, which is the lottery Fable named. Coarse carrion in the
small tank doubles the mean and halves the hit rate, with an sd 2.2 times the mean. Fine carrion feeds
two episodes in five at either height tried.

**Measurement 3** is the positive control, and it fails. The kinesis graft (`--graft-kinesis a,m`)
drives the joint with an oscillator times `a − m × scent`. It was set against its own knockout on 32
held-out episodes a body, paired episode by episode, with fine carrion and the body 0.5 m above the bed.
The last column is the mean distance to the nearest corpse over each episode's second half.

| condition | wired | knockout | pooled difference | bodies 2 SE above | bodies 2 SE below | nearest, wired / knockout |
|---|---|---|---|---|---|---|
| small, 0.8, 0.8 | 8.34 J | 8.96 J | −0.62 J (−1.3 SE) | 0 of 10 | 1 | 3.24 / 3.15 m |
| small, 0.8, 3 | 9.61 J | 8.96 J | +0.64 J (+1.1 SE) | 1 of 10 | 1 | 3.26 / 3.15 m |
| still, 0.8, 0.8 | 5.28 J | 6.04 J | −0.76 J (−2.0 SE) | 1 of 10 | 1 | 2.34 / 3.22 m |
| still, 0.8, 3 | 5.83 J | 6.04 J | −0.22 J (−0.6 SE) | 1 of 10 | 2 | 3.64 / 3.22 m |

Three of forty bodies clear 2 SE above their knockout and five clear it below. That is about what
chance gives on forty tests. No pooled difference is 2 SE above zero, and the one at 2 SE is below it.

The graft does act. In still water at gain 0.8 it holds the body 0.9 m nearer the carrion, and it eats
less. My reading, which is inference: slowing where the scent is strong parks a one-tailed body near
the carrion rather than on it. The score counts only what the mouth reaches, 0.5 m past a corpse's
radius, so nearness earns nothing. D134 runs design A only if the control scores, so it was not started.

## What next: the options put to the owner (2026-09-30)

The owner has deferred this choice until review round 7 is written
(`research/nursery-curriculum/round7-fetch-brief.md`). The options, as they were put:

1. **A run-and-tumble control first (the agent's recommendation).** A bacterium with one sensor finds
   food by comparing the scent now with the scent a moment ago, keeping on while it rises and turning
   when it falls. The brain has the parts: `NeuronOp.Differentiate` exists
   (`src/Evosim.Core/Genome/NeuronOp.cs`). The graft is the oscillator as before plus an offset on the
   joint while the scent falls, since an offset makes a one-tailed swimmer curve. It is a small grid of
   offsets in the small tank and the still one, paired against its knockout as in measurement 3, about
   an hour on four threads, with no world rule changed. If it beats its knockout, design A runs with it
   as the positive control. If it fails, the agent reads these two-part, one-joint bodies as unsteerable
   in this world and goes to option 3, after counting how many bodies in round 52's snapshots carry two
   scent-sensing parts.
2. **Design A without a working control.** Evolution might find what the graft did not, but nothing
   would show that the score can see steering, so a null could not be read.
3. **Bodies that can steer**: pool bodies with two or more scent-sensing parts, or let the nursery
   change the body as well as the brain. It changes D134's design.
4. **Credit for coming near the food.** It would pay what the kinesis graft does, which the ecology
   does not, and the owner has called it breeding.
