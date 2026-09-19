# The two cheapenings: the water held, and a body born inside itself refused

*2026-09-19, 14:00 to 15:00. Written by the agent from the owner's two rulings of the
afternoon (D100, D101) on the profile's reading (`harness-profile-spec.md` §7). Built by two
subagents on branch `profile` in `scratch/wt-profile`, beside the profile's instruments;
the branch waits for round 41d to end before it reaches main, because both rules are Core
changes and every worker compiles Core from the main tree at launch.*

## 1. The water held (D100)

`FluidConfig.WaterHoldSeconds` (`[Tunable("fluid")]`, seconds, default 0). In
`FluidEnvironment.Apply`'s gather loop, when the hold is above 0, the creature's first link
does the sampling. Whenever the simulation's clock has moved a hold's worth since the
creature's last sample, and on a fresh instance's first step, it samples the current's
velocity, and under D090's force its acceleration, at the root's position, and stores them
on the instance (`CreatureInstance.HeldWater`, `HeldWaterAcceleration`, `WaterSampledAt`).
Every link then reads the held values in place of its own sample. The per-link volume, the
waterline guard on the link's own height and the trace's stores stay per link. At 0 the recorded arithmetic runs
unchanged, call for call. `EVOSIM_WATER_HOLD`; header `water held 0.5 s` or `water per
link`, after `fluidAccel`. The sub-split's `water` bucket wraps the held sampling too.

## 2. A body born inside itself refused (D101)

`Geometry/BoxOverlap` in Core: the probe's oriented box, its fifteen-axis separating-axis
test with the depth, and the threshold rule (`DepthThreshold`, the smaller box's thinnest
half-extent times the fraction); the probe calls it and prints what it printed.
`Phenotype.SelfOverlappingPairs(fraction)`: the count of part pairs that are not parent and
child whose depth exceeds the threshold. `RunConfig.SelfOverlapDepthFraction`
(`[Tunable("development")]`, default 0, off). `World.Admit`: with the fraction above 0, a body
with any such pair is a stillbirth, settled on the same branch as a body of no parts, counted
in `Stillbirths` and `SelfOverlapStillbirths`; founders and inoculants pass the same door and
the floor draws again next step. `EVOSIM_SELF_OVERLAP`; header `selfOverlap 0.1` or
`selfOverlap off`, after `silhouette`; `selfOverlapStillbirths` in `stats.jsonl` after
`stillbirths`; the table's `self stillb`, appended at the end of the base columns.

## 3. Tests

`SelfOverlapTests` (11). The geometry: separated boxes, an axis-aligned pair at an exact
depth, a rotated pair the face axes alone would miss, the threshold from the thinner box.
The count: the sixteen-part coincident knot at 105 pairs, a spine and a leaf at 0, scale
invariance, the fraction biting. The world: an inoculated knot refused with the counters
right, the same knot living with the rule off, and the breeding path with both books
closed. The reflection guards and the fluid config's clone guard pass with the two
tunables.

## 4. The smoke and the screen

Both on worker 6 refreshed from the worktree, both on `simHash c80cbd77…` (a worktree
checkout; the round's expected hash comes from a smoke on a worker refreshed from main after
the merge, as CLAUDE.md says), both with `-WaterHold 0.5 -SelfOverlap 0.1`.

**The smoke** (`r41esmoke`, seed 1, 300 s at dt 0.02, `configHash 3973b0e1`): the header
reads `silhouette on · selfOverlap 0.1 · water held 0.5 s`, both books at zero on every row,
41 founders alive at the end, 8 births and one self-overlap stillbirth among the founders'
draws, and the drag pass's `water` share at 9% against the profile smoke's 43% at the same
founding crowd.

**The screen** (`r41escreen-s1`, seed 1, 1,500 s at dt 0.01, `configHash f6416481`, 10
minutes, 2.5x real time), read over its last 500 s against the profile's own run of the same
seconds on round 41d's world (`r41dprof3-s1`, the sub-split build), both beside round 41d's
three arms:

| | round 41d's world | round 41e's world |
|---|---|---|
| alive over the window | 360 to 756 | 289 to 730 |
| links a body | 1.73 | 1.77 |
| pace in the window | 1.08x | 1.37x |
| wall: physics, world, harness | 23%, 18%, 59% | 27%, 22%, 51% |
| harness, µs a body-step | 9.1 | 6.9 |
| of which fluid, trace, control, settle | 5.4, 1.9, 1.3, 0.5 | 3.0, 1.9, 1.4, 0.6 |
| fluid, µs a link-step | 3.09 | 1.72 |
| fluid split: gather, water, compute, apply | 22%, 45%, 10%, 23% | 34%, 14%, 18%, 34% |
| contact pairs a body-step | 0.037 | 0.019 |
| births, stillbirths, of them self-overlap | 746, 0, 0 | 719, 13, 13 |

The drag pass costs 44% less a link, the harness 24% less a body, and the seed runs 1.27
times faster over the same seconds at about the same crowd, which is what D100 was
expected to buy. What is left of the pass is the engine crossings (gather and apply, two
thirds of it now) and the panels; the water is a seventh. The birth rule refused 13 of 732
conceptions, 1.8%, so founding is not slowed by it at this seed, and the contact pairs a
body-step are half of round 41d's at 1,500 s, before either world has grown its sprawl;
whether they stay down is round 41e's E9. The two worlds are two realisations of one seed
and the counts are read as such. The trace's 1.9 µs is untouched and is lever 1's, after
the round.

## 5. What round 41e asks

Round 41d's world with both rules on: `rounds/launch-r41e.ps1`, `-WaterHold 0.5
-SelfOverlap 0.1`, everything else 41d's. Read against round 41d as distributions: the pace
at the crowd (the drag pass's `water` share and the harness per body-step from the profile
instruments, which the branch carries), `pairs/body` by window and the probe's self-pairs
(E9 and E10 as 0108 states them), `self stillb` against `births` in the same window, and
whether founding is slower. The pre-registration is its own entry.
