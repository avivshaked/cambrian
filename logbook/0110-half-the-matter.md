# Half the matter

*2026-09-20, 20:15. Written by the agent as the pre-registration of round 42, on the owner's
rulings of the same afternoon (half the matter; lever 1 and the restricted trace, built and
merged with identity kept; the arms pinned). The predictions are committed before the queue
starts.*

## What it asks

Round 41e answered its first question and lost the rest to a reboot: a world that cannot be
born folded stays affordable in contacts, and its 30,000 s clauses were never read (0109's
last section). What it did not fix was the pace. Its jointed seeds ran at a third of real
time, a day a seed, and the profile's next lever bought 3% where I had said 1.2x
(`logbook/specs/harness-profile-spec.md` §8).

Two things did move the pace that afternoon, and neither is in the harness. The crowd is
the budget's: the dissolved matter stands at a hundredth of what was seeded and four leaf
steps in five are bound by uptake, so half the units should be about half the bodies. And
an arm beside two others cost 1.48 of its solo wall, which pinning each arm to two fast
cores of its own took to 1.09 on one trajectory (§9). Round 42 asks whether the two hold
at a round's scale. Does half the matter halve the crowd and leave the ecology's mechanism
readings where they were? Does a pinned seed run near real time? And it asks again what
41e could not answer: the eaters, the shape selection grows under the refusal, and whether
the refusal stays a sieve in a jointed line.

## The world

Round 41e's with one number changed: `rounds/launch-r42.ps1`, which is 41e's launcher with
`-MatterBudget 1500`. The tank is 2,200 m² by 45 m with the bed tilted 30 m, and the
streams run at 0.1 m/s with the fluid acceleration on. The economy is the one substance on the grid, at 100 J a unit,
with the overhead at 100 J and the remineralisation at 0.002 /s. The silhouette cap is on,
the water is held for 0.5 s, the self-overlap refusal is at 0.1 and the joint at 0.0001.
Five seeds run 30,000 s at dt 0.01 under a wall of 1,800 minutes, three arms at a time on
workers 2, 3 and 4.

The build is main after the `lever1` merge (`bfc0993`): the solver read once a step and
shared, and the throw trace kept only for bodies with a movable joint. Both replayed round
41e's seed 1 on every non-clock field, to 5,000 s and to 3,000 s. A dump of an unjointed
body now carries `traceOmitted` and no trace file. Each arm is pinned by its worker number
to logical processors 2 to 5, 6 to 9 or 10 to 13 (`run-arm.ps1 -AffinityMask`). Pinning
moves no trajectory: the pinned replay matched the recording.

The owner also ruled the bed raised into the lit band. That is not in this round. A tank
20 m deep at this radius is refused by the streams, whose equal motion on every axis has no
solution when the overturning cell is wider than it is deep (`runs/r42scrB-s1`). The
shallowest tank that ran, 30 m with the floor from 15 m to 45 m, was screened at dt 0.02 to
10,000 s beside the same world on the bed as it is (`runs/r42scrB30-s1`,
`runs/r42scrA-s1`). Nine bodies in ten stayed above 12 m in it, and the bed saw 3.3 contact
pairs a step against 2.0. A floor at 5 m needs a smaller tank or the streams' rule relaxed,
and either is the owner's. So the round changes one thing, which also keeps it readable
against 41e.

## The screen

`runs/r42scrA-s1`: seed 1 at dt 0.02 to 10,000 s on 1,500 units. The count read 433 at
5,000 s and 696 at 10,000 s, still rising, against round 41e's 996 and 1,338 at the slow
step. Uptake-limited 86%, margin 212 s, no divergence, 515 of 696 jointed, no inherited
eaters. A fast-step screen says the founding works and where the count is heading, and
nothing about depth or muscle (CLAUDE.md's dt gotcha).

## Rules

V1 to V5 as 0109's, with `matterBudget 1500` in V1's header list. A manifest reading
`error` or `stopped` is censored and read at its last sample. A seed past 2,500 bodies is
stopped. A seed that shows the adjustment the world needs is stopped as `manual-futility`
and the entry says what it showed. The fail-fast reads are F9 and F11 from the first row
they can be read on, F10 on the 5,000 s snapshots, and F8 at 5,000 s.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| F1 | **the crowd is half**: `alive` between 250 and 650 at 5,000 s and between 500 and 1,400 at 30,000 s, in 4 of 5 | the timeline |
| F2 | **the crowd grows as the holding shrinks**: `alive` at 30,000 s at least 1.3 times `alive` at 5,000 s in 4 of 5, and `margin s` under 150 at 30,000 s in 3 of 5 (41e's seed 3 held 287 s at 10,000 s) | the timeline; `margin s` |
| F3 | **the water is still a treadmill**: `upt lim` between 40 and 95% at 15,000 and 30,000 s in 4 of 5, and `mat top` no higher than `mat deep` at both | `upt lim`; `mat top` against `mat deep` |
| F4 | **the larder keeps its share**: the marine snow between 0.10 and 0.40 of the budget at 15,000 and 30,000 s in 4 of 5 | `detritus J` over 1,500 units at 100 J |
| F5 | **the eaters found less often in a smaller larder**: `inherit` reaches 25 after 5,000 s in 2 of 5, and in every seed where it does, `inherit` is at least 5 at 30,000 s | `inherit` at every 100 s |
| F6 | **the bust is slower**: the first boom above 150 inherited eaters falls under a sixth of its peak later than 2,600 s after it, or not before the budget, in every seed that has one | `inherit`, `booms.py` |
| F7 | **nothing throws and the disc stays mixed**: `diverged` between 0 and 10 per million jointed body-seconds in 4 of 5; the rim quarter 0.12 to 0.40 and `cols` within 0.80 to 1.05 of 1 − e^(−n/2211) at 5,000, 15,000 and 30,000 s in 4 of 5 (41e sat at 0.86, its floor) | the manifests; `p3` over `alive`; `cols` |
| F8 | **the pace is near real time**: wall seconds per 1,000 simulated seconds per 1,000 living bodies between 600 and 1,600 from 5,000 s on in 4 of 5 (41e read 1,150 to 2,900 unpinned), and the whole seed at 0.9x real time or better in 4 of 5 (41e read 0.35 to 0.59x) | `run.json`; `scripts/reads/r41d-read.py` |
| F9 | **the physics stays affordable**: `pairs/body` under 0.5 at 5,000 s and under 1.0 at 15,000 and 30,000 s in 4 of 5, and no seed doubles in each of two consecutive thousand-second windows past 0.5, which stops the seed | `pairs/body` by window |
| F10 | **the fold stays gone**: in the 5,000, 15,000 and 30,000 s snapshots no living body at 16 parts, fewer than 1% at eight or more, and the probe's self-overlapping pairs a living body under 0.3, in 4 of 5 | the overlap probe on the snapshots |
| F11 | **the refusal is a sieve**: `self stillb` under 10% of the window's births at 5,000, 15,000 and 30,000 s in 4 of 5, and the floor stops firing by 1,000 s in 4 of 5. 41e's seed 3 read 10.0% at 10,000 s, so I expect one seed to fail this and say so | `self stillb` against `births`; `floor` |
| F12 | **selection grows open bodies**: the median parts a body at 30,000 s is at least 2 in 4 of 5, with F10's probe clause holding in the same snapshots | the probe's parts histogram |

## The two-sided readings

- **F1 holds and F8 holds:** the two free levers are real. The base world is affordable at
  about eight hours a seed, and the campaign's next rounds are planned at that pace.
- **F1 holds and F8 fails:** the crowd halved and the seed did not speed up in proportion.
  Read the wall split. If the harness per body-step rose, the pinning did not carry to
  three full arms and §9's neighbours were too light to show it; try four logical
  processors on separate cores, or two arms.
- **F1 fails high:** the count is not the budget's alone. Read `margin s` and the body sizes:
  a world on half the matter can build as many bodies if each holds half as much.
- **F1 fails low, or the founding stalls:** half the matter is under what founding needs at
  this volume, 0.015 units/m³ seeded; read `upt lim` in the first thousand seconds and the
  floor's firing.
- **F5 fails at zero:** the eaters' larder scales with the crowd and this world is under
  their threshold. The eaters' round then wants the budget back, or a reserve cap.
- **F11 fails in two seeds or more:** the refusal is a wall for jointed lines, and the depth
  fraction gets its own screen.

## What the round does not ask

Whether the bed matters; the screens say almost nothing touches it and the ruling waits on
the owner. Whether the pinning is clean: the round is not an A/B, since every arm is
pinned, and its evidence is the pace against 41e's at the same crowd.

## Read at 30,000 s, five seeds of five

*2026-09-22, 02:10. Every seed ended at its budget: seed 5 last, at 02:00. The clause
readings are `logbook/specs/r42-read/clauses.txt` (`scripts/reads/r41d-read.py --budget
1500` at 5,000, 15,000 and 30,000 s) and `probe-and-sieve.tsv` beside it (the overlap probe
on the fifteen snapshots, the sieve share and the eaters, with the script that read them).*

| # | held in | reading |
|---|---|---|
| F1 | 5 of 5 | 385 to 548 alive at 5,000 s, 843 to 1,162 at 30,000 s |
| F2 | 5 of 5, and 2 of 5 | every seed at least 1.7 times its 5,000 s count; `margin s` under 150 in seeds 4 and 5 only (30 and 38 s; the others 225 to 234) |
| F3 | 5 of 5 | `upt lim` 74 to 93%; `mat top` under `mat deep` at every mark |
| F4 | 5 of 5 | snow 0.15 to 0.29 of the budget |
| F5 | 0 of 5 | the most inherited eaters after 5,000 s was 21 (seed 4, at 18,400 s); at 30,000 s two seeds hold 2 and 0 and the rest 0 |
| F6 | vacuous | no boom passed 150 |
| F7 | 5 of 5 | nothing thrown in 66 million jointed body-seconds; rim quarter 0.25 to 0.34; `cols` 0.91 to 0.99 of the uniform count |
| F8 | 1 of 5, and 0 of 5 | wall seconds per 1,000 simulated per 1,000 bodies 1,589 to 3,471 (seed 2 inside the band); the seeds ran at 0.36 to 0.74x |
| F9 | 2 of 5 | `pairs/body` under 0.5 at 5,000 s in every seed, under 1.0 at 15,000 s in four, and at 30,000 s 1.28, 0.13, 0.04, 1.76 and 1.88; no seed doubled |
| F10 | 4 of 5 | no body at 16 parts and none at 8 or more through 15,000 s in every seed; at 30,000 s seed 5 holds one body of 16 and 2.1% at 8 or more, seed 1 1.7% at 8 or more; self-overlapping pairs a body 0.04 at worst |
| F11 | 4 of 5, and 5 of 5 | `self stillb` over the window's births 1.4 to 2.4% to 5,000 s, 3.9 to 8.2% to 15,000 s, 4.3 to 9.6% to 30,000 s in four seeds and 16.8% in seed 5; the floor last fired at 500 to 700 s |
| F12 | 5 of 5 | the median body at 30,000 s has 4, 2, 2, 3 and 4 parts |

**What it looked like.** Seed 1 from above at 30,000 s
(`logbook/images/r42-s1-t30000-recon-top.png`): the disc filled edge to centre with no
crust and no ring, D090 holding at half the matter. Seeds 4 and 5 from the side at 15,000 s
(`r42-s4-t15000-recon-side.png`, `r42-s5-t15000-recon-side.png`): every body in the top
twelve metres, a handful drifting below, and a bed nothing touches, as the screens said.
The close view of seed 1 at 3,000 s (`r42-s1-t3000-close.png`) is a loose crowd of green
one-part leaves with a few grey link bodies among them, none folded. Every picture is
drawn from a snapshot, at adult size and in the developer's frame.

**The two levers, read.** F1 held and F8 failed, which the entry's second reading planned
for: half the matter gave the crowd it predicted, and a pinned seed at that crowd ran at
0.36 to 0.74x where round 41e's unpinned ones ran at 0.35 to 0.59x. The wall split says
why the pinning could not carry it: PhysX and the harness around it are on one thread by
construction (D078), and the cost per body tripled as bodies gained links and began to
touch, seeds 4 and 5 at 3,000 to 3,500 wall seconds per thousand-body-thousand-seconds
against seed 2's 1,600 with a quarter of the joints. The seed with the most muscle is
the slowest, and that is the direction the campaign has to go. The owner read it that
way the same day and ruled the engine before the world (HANDOFF, 2026-09-21): a solver of
our own, the farm out of Unity, the GPU after. Round 42 seed 1's world replayed on it in
54 minutes before the grid was threaded and 24 minutes after, both books closed, and
that is where the next base round runs.

**The eaters, again at zero.** F5's zero is the third round running (40, 41e, 42), and
the entry's reading stands: the larder scales with the crowd, and at 1,500 units the
inherited line never reaches 25. Round 40's reading of the reserve (CLAUDE.md, the
unbounded reserve) is the standing cause, and the lever is the reserve cap or the budget
back, both the owner's.

**Joints were selected out in three seeds, and the physics was not free.** The jointed
count peaked at 602 to 827 in every seed between 10,000 and 20,000 s and ended at 47, 297,
14, 486 and 814: three seeds lost their muscle, two kept it. The spike's parity swims
(HANDOFF, 2026-09-21) found that in this build a body's own parts collide and a driven
joint is mostly not free to turn. A joint in round 42 therefore cost its upkeep and its
contact pairs and moved almost nothing. The one seed replayed on the free-joint engine
faded the same way (597 to 115, `r42farm-s1`), so the collision was not the whole cause. F9's fail
is the same fact from the other side: `pairs/body` climbed with the jointed count in
every seed and read 1.3 to 1.9 where the muscle survived.

**The sieve.** F11 read as predicted: one seed over the line, and it is the one with the
most joints, 16.8% of the last window's births refused in seed 5 against 4 to 10% in the
rest. The refusal grows with the muscle, as a sieve on folded bodies would in a world that is
evolving links. Whether it is a wall for a jointed line is still 0109's question, and the
next engine's contact does not fold a body at all.

**What the round did not settle.** Whether the bed matters (nothing reached it), whether
the pinning is clean (no A/B), and thrust: no body in any seed strokes, so `mean m/s`
still reads the water.
