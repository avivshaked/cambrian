# Where the harness's wall goes: the profile before the cheapening

*2026-09-19, 11:35. Written by the agent on the owner's "This sounds like a good plan. Let's
do it", on the plan of the same hour: measure the harness's per-body work before cheapening
it the way a game would, keeping replay identity and giving up physical fidelity where it
buys nothing. Round 41d's jointed seeds run at 0.37 to 0.42x real time at 1,100 bodies with
the physics a flat quarter of the wall and the harness half, and nothing says what inside
the harness (logbook/0108; HANDOFF's queue).*

## 1. What exists

The timing-split build (2026-09-17) wraps each physics step in `Ecosystem` with two
timestamps: `_physicsTicks` around `Physics.Simulate`, `_worldTicks` around `World.Step`,
`_harnessTicks` as what is left of the step. `stats.jsonl` rows carry the cumulative
`wallPhysicsMs`, `wallWorldMs`, `wallHarnessMs`, `wallWritersMs`, `wallTotalMs`;
`run.json`'s ending block the totals; the footer `wall split: physics 25%, world 24%,
harness 53%, ...`. The harness's half is one number.

## 2. What is built

The harness's step split into its phases, each with the same two timestamps, cumulative in
Stopwatch ticks, summed into the existing `_harnessTicks` so the old split is unchanged to
the millisecond. The phases are what the per-step loop does between one `Physics.Simulate`
and the next, read from the code and named after it; the list below is the expected set
and the builder corrects it to what the loop actually does:

- `sense`: every creature's sensors read (transforms, the chemical and flow fields, energy).
- `brain`: every creature's neurons stepped.
- `drive`: every joint's drive target and limiter written.
- `fluid`: `Fluid.Apply`, drag and the water's acceleration on every part, added mass.
- `finite`: `CheckFinite` and the diverged dump.
- `contacts`: the contact event handler and `CloseContactStep`.
- `growth`: the resize pass on growth steps.
- `reconcile`: births' `Build`, deaths' `Destroy`, the index rebuilds, the placer.
- `metabolise`: the metabolic step's harness side less `World.Step` (positions, the
  absorptive log, the snapshot and lineage writers are `writers` already).
- `other`: what is left, so the phases sum to the harness.

Reported three ways. `stats.jsonl` rows carry `wallHarnessSenseMs` and the rest, cumulative,
beside the five that exist. `run.json`'s ending block carries the totals. The footer gains
one line after `wall split`: `harness split: sense 31%, brain 22%, drive 9%, fluid 18%,
finite 4%, contacts 6%, growth 1%, reconcile 3%, metabolise 4%, other 2%`, percentages of
the harness's total. And a per-body reading beside it: `harness per body-step: 41 µs` (the
harness total over the sum over steps of the living count), which is the number the
cheapening is judged on. `analyse-arm.ps1` prints nothing new; the footer and the fields
are the instrument.

Nothing else changes: a timestamp pair around a call moves no trajectory, and the
`RunConfig` guards are untouched because no tunable is added. It lives under
`Assets/Evosim`, so `simHash` moves; round 41d's workers are not refreshed until the round
ends, and the profile runs on worker 6.

## 3. What is measured with it

On worker 6, beside round 41d's three arms, round 41d's own world at dt 0.01 for 2,500 s
(`rounds/launch-r41d.ps1 -Seed 1 -Worker 6 -Seconds 2500 -Name r41dprof-s1`), which at
2,300 s had 846 bodies with half of them jointed on the same seed; the split read from the
footer and from the rows' differences over the last 500 s, where the crowd is. Then the
same seed for 300 s with `EVOSIM_FOUNDER_...` untouched but the joint priced out
(`-Idle 1`), so that a crowd of one-part leaves gives the per-body cost of a body with no
joint, for the difference. Both are the caller's; the builder's task ends at the compile.

## 4. Acceptance

A 300 s smoke at dt 0.02 on worker 6: the ten fields on every row, monotone, summing to
`wallHarnessMs` within 1 ms per row; the footer's two new lines; `wall split` unchanged in
kind. `simHash` and `coreHash` recorded in the smoke's manifest.

## 5. What comes after, for the owner's ruling

The split decides which levers to pull, in the order the plan named: the brain and the
senses ticked slower than the physics and held between ticks; the water's velocity and
the drag panels cached between metabolic steps; the per-body work in Burst jobs with a
fixed reduction order, PhysX left on one thread; the finite check every tenth step. Each
is a per-step change and a new realisation of every seed, read on distributions across
seeds and on the digest pair for identity. The grid's quarter of the wall is a world rule
and a separate ruling.

## 6. What was measured

*2026-09-19, 12:15 to 13:00.* Three runs on worker 6 beside round 41d's three arms, all on
the branch's build (`simHash d146f82a…`, `coreHash 40c9a478…`), all round 41d's seed 1 at
dt 0.01 under the round's `configHash 630f0206` unless said otherwise. Every split below is
two rows' difference over the run's last 500 s, where the crowd is; the footer's whole-run
line is given once for the record.

**The round's world** (`r41dprof-s1`, 2,500 s, 35 minutes, 1.2x real time over the run):
807 bodies at the end, 41% of them jointed, 1.77 parts a body (270 of one part, 464 of
two, 65 of three, 8 of four). The footer reads physics 23%, world 19%, harness 58%. Over
the last 500 s, at 813 to 846 bodies and 0.70x real time, physics 25%, world 11%, harness
64%, and the harness 10.8 µs a body-step:

| phase | of the harness | µs a body-step |
|---|---|---|
| fluid | 56% | 6.1 |
| trace | 20% | 2.2 |
| control | 15% | 1.7 |
| settle | 7% | 0.8 |
| contacts, reconcile, finite, metabolise, growth, other | under 1% each | under 0.1 |

**The one-part control** (`r41dprofctl-s1`, 1,500 s with the joint priced out, `-Idle 1`,
`configHash da0a64f1`, 11 minutes, 2.3x real time): 931 bodies at the end, none jointed,
1.45 parts a body (562 of one, 327 of two, 39 of three, three more). Over its last 500 s,
at 370 to 931 bodies, the harness is 59% of the wall and 7.0 µs a body-step: fluid 68%
(4.8 µs), trace 16% (1.1), control 10% (0.7), settle 4% (0.3).

Per link the fluid costs the same in both crowds, 3.3 µs a link-step (6.1 over 1.77 parts,
4.8 over 1.45): a flat price on every link every physics step, whatever the body is. The
trace is written for every body of more than one link, so it moves with the multi-link
share rather than with the joint. The control's 1.7 µs against 0.7 is the brain: a jointed
body carries neurons a leaf does not, and the sensors read every part. Of the whole wall
at the round's crowd, then, the water's pass is 36%, the trace 13%, the brain and senses
10%, the settle 4%; PhysX 25% and the grid 11%.

**The instrument moves no trajectory.** A rerun of the same seed on the same build
(`r41dprof2-s1`, 1,500 s, launched by mistake against a worker not yet refreshed for the
sub-split below) replays `r41dprof-s1` sample for sample on alive, births and mean height
at every one of its fifteen samples, which is §2's promise tested at a crowd.

**Inside the fluid pass** (`r41dprof3-s1`, 1,500 s on the sub-split build, `simHash
493f2605…`; the same trajectory a third time, sample for sample). The pass is split four
ways with the same timestamp pairs: `gather`, the reads of every link's rotation, spin,
position and velocity and the array stores; `water`, the streams' velocity and closed-form
acceleration sampled per link, timed per link so its clock overhead is inside it; `compute`,
the panel arithmetic in parallel; `apply`, the force and torque written to every link. The
rows carry `wallFluidGatherMs`, `wallFluidWaterMs`, `wallFluidComputeMs`, `wallFluidApplyMs`
and `fluidLinkSteps`, and the footer gains `fluid split: gather 20%, water 46%, compute 12%,
apply 21%` and `fluid per link-step: 3 µs`. Over the last 500 s, at 360 to 756 bodies and
1.73 links a body:

| piece of the pass | of the pass | µs a link-step |
|---|---|---|
| water | 45% | 1.39 |
| apply | 23% | 0.72 |
| gather | 22% | 0.68 |
| compute | 10% | 0.30 |

So the water is half the pass, the crossings into the engine (a property read or a force
write per link) are the other half, and the arithmetic the drag was once measured at 88%
of the step for (DESIGN §5A.9, on a tiled world of a few bodies) is a tenth. Of the whole
wall at the round's crowd the water's sampling is about 16%, the crossings 16%, the trace
13%, the brain and senses 10%, the panels 4%, the settle 4%; PhysX 25% and the grid 11%.
The round's own seeds read the physics at 27 to 28% on the same seconds, so the profile
arm stands for them.

Two things about running it. Every launch here was from inside the worktree, so the
launcher used the worktree's `unity-w6` and `runs/`. That worker is a copy of the tree like
any other, and a launch after an edit without a refresh ran the previous build
(`r41dprof2-s1`; the launch printed `simHash d146f82a…` and nobody compared it). The three
runs of one seed on two builds replaying each other is the identity check §2 promised,
taken for free.

## 7. The reading, and what is put to the owner

The tax is not the brains. Two thirds of the harness is the water touching every link on
every physics step. Half of that is sampling a field that moves five centimetres in a
metabolic step, at a point within half a metre of the body's other links. An eighth of the
wall is an instrument reading the solver a second time. The brain and the
senses, the first lever the plan named, are a tenth.

The levers, re-ordered by the measurement, with what each is expected to buy of today's
wall (inference from the splits; the digest pair and the next round's pace are the test):

1. **Read the solver once a step and share it.** The fluid gather, the trace, the sensors
   and the finite check each read a link's transform or velocity through the engine. One
   read per property per link into flat arrays, shared, keeps every number and moves no
   trajectory, so identity is kept by construction. It buys the trace's 13% and a part of
   the gather, and it is agent work, first.
2. **Sample the water once a body and hold it for a metabolic step.** The streams sampled
   at the body's root every 0.5 s in place of every link every 0.01 s: the water's 16%
   falls fiftyfold. It is a per-step force change and a new realisation of every seed. The
   fidelity given up is the difference between the water at a link and at its root, and
   over half a second, in a field whose eddies are metres wide. It needs the owner's ruling.
3. **No collision inside one body.** Seed 2's rising pairs are, by the probe, a body's own
   parts; PhysX resolves each such pair every step and never separates them, and games turn
   intra-body collision off. Buys the share of PhysX's 25% that is self-contact, which the
   probe puts at most of it in seed 2, and makes a fold cost nothing. It is a world rule,
   a body may pass through itself, and needs the owner's ruling.
4. **Fewer drag panels**: 4% at most. Not worth a realisation; dropped.
5. **The brain and the senses ticked slower**: 10% at most, and the control's 1.7 µs is
   half the sensors' transform reads that lever 1 shares. Dropped for now as well.
6. **Burst jobs over transform arrays** for the gather and the apply: the other 16%, days
   of work, after 1 to 3 are read.
7. **The grid**: 11%, a world rule and a separate ruling, as §5 said.

The arithmetic: lever 1 alone about 1.2x, and 1 with 2 about 1.6x, so a seed at 0.32x
beside two other arms runs at about 0.5x. Lever 3 on top buys the physics' share of
self-contact, unknown until tried. Real time at 1,300 bodies beside three arms needs the jobs as well, or fewer
arms. The branch stays unmerged until round 41d's fifth seed has launched, since the
queue's hash check refreshes from main; it carries both instruments and nothing else.

## 8. Lever 1 built and measured: identity kept, three percent bought

*2026-09-20 afternoon. Branch `lever1`, worktree `scratch/wt-lever1`.*

The build reads each living link's position, rotation and two velocities once before the
step's consumers and once after the solver, into flat arrays on `CreatureInstance`
(`ReadSolverStateBeforeStep`, `ReadSolverStateAfterStep`). The finite check, the senses,
the drivers, the drag pass, the settle and the throw trace take their values from there. A
new harness phase, `read`, times the two fills. No tunable was added and no config moved.

The validation was seed 1 of round 41e three ways: the recording (`runs/r41e-s1`), a replay
on main's build (`runs/r41ebase-s1`) and a replay on the branch
(`scratch/wt-lever1/runs/r41elev-s1`), the two replays side by side on an otherwise idle
machine for 5,000 s. Every field of `stats.jsonl` that is not a wall clock was compared at
every sample (`scratch/lever1/identity.py`).

| | main's build | the branch |
|---|---|---|
| samples compared with the recording, and fields | 50, 6,300 | 50, 6,300 |
| differences | none | none |
| wall for 100 to 5,000 s, at 996 bodies by the end | 4,197 s | 4,065 s |
| harness per body-step | 6.62 µs | 6.07 µs |
| harness split: fluid, trace, control, settle, read | 43, 25, 22, 9, none | 27, 20, 21, 5, 26 |

Identity is kept, which was the design's promise, and the branch is 3% faster in wall and
8% in harness. §7 put this lever at about 1.2x. It was wrong, and the split says why. The
fluid and the trace fell by what the sharing saved, and the `read` phase took a quarter of
the harness to do it. The cost was never the second reading of a value; it is each
crossing from managed code into the engine, about a fifth of a microsecond a property a
link, and four properties a link twice a step are still eight crossings. A one-part body
is slightly worse off, since it now pays a position read after the solver that only the
trace and the metabolic check use.

What follows from it. No lever that rearranges the same crossings can buy much, which
retires the family. What is left is fewer bodies (the budget, the owner's, ruled to 1,500
units the same afternoon), fewer crossings by construction (a body with no movable joint
out of the articulation solver, or the per-link work in compiled jobs over batched reads;
a physics change and a proposal), and the machine (why three arms on 32 logical cores
run at half speed each, §9). The throw trace was also restricted on the same branch to
bodies with a movable joint (`TotalDof > 0`); a one-part body already kept no ring, so the
saving is the welded multi-part bodies' links, large in a leafy world and small in a
jointed one. A dump of an unjointed body carries `traceOmitted` and no trace file.

## 9. What an arm costs its neighbours

*2026-09-20, 15:30.* A seed replays bit for bit, so one trajectory run under three loads is
the same work three times and the wall is the only thing that differs. Seed 1 of round 41e
ran alone on an idle machine (`runs/r41esolo-s1`), beside one other arm
(`runs/r41ebase-s1`, §8's pair) and beside two (`runs/r41e-s1`, the round itself, whose
neighbours were seeds 2 and 3 and an early snapshot render). The three agree on every
non-clock field through 3,000 s, 7,560 comparisons.

| wall for 100 to 3,000 s, to 764 bodies | alone | beside one | beside two |
|---|---|---|---|
| total | 1,717 s | 1,885 s | 2,549 s |
| PhysX | 459 s | 506 s | 665 s |
| the harness | 807 s | 921 s | 1,388 s |
| the world's step | 450 s | 457 s | 495 s |
| against alone | 1 | 1.10 | 1.48 |

Two arms deliver 1.8 times one arm's work and three deliver 2.0, which is the pace
survey's finding by a cleaner route: the third arm buys a fifth of an arm. The slowdown is
not even. The world's step, a tight loop over a grid, loses a tenth; PhysX loses 1.45; the
harness, managed code crossing into the engine for every link, loses 1.72. The machine is
a 13900K, eight fast cores with two threads each and sixteen slow ones, on the Balanced
power plan, and each arm keeps a little over one core busy. My reading, as inference: three
main threads are not short of cores, so what they share is either a fast core's second
thread, a slow core the scheduler moved a windowless process to, or the cache. The first
two are the scheduler's and can be tested for nothing by pinning each arm to fast cores of
its own; the test is running on the next three arms and its result goes here.

*The pinning test, 16:30 the same day.* The next three arms were each given two fast cores
of their own (`ProcessorAffinity` on the Editor's process: logical 2 to 5, 6 to 9 and 10 to
13). One of them was seed 1 again, on the `lever1` branch with the trace restricted
(`scratch/wt-lever1/runs/r41etrace-s1`), so its wall reads against the solo run's on the
same trajectory. It agreed with the recording on every non-clock field through 3,000 s,
3,780 comparisons, pinned or not.

| wall for 100 to 3,000 s | alone, unpinned | beside two, unpinned | beside two, pinned |
|---|---|---|---|
| total | 1,717 s | 2,549 s | 1,877 s |
| against alone | 1 | 1.48 | 1.09 |

It is indicative and not clean. The pinned arm's neighbours were two dt 0.02 screens at
half the matter, lighter than round 41e's seeds, and the pinned arm ran a build about 3%
cheaper. Both push the same way. Still, a harness that lost 1.72 beside two arms lost 1.12
here, and the simplest account is the scheduler's placement, which pinning removes. Round
42 runs pinned, from `run-arm.ps1`, and its pace against round 41e's at the same crowd is
the confirmation. The trace restricted to bodies with a movable joint took the trace's
share of the harness from 27% to 16% in this jointed seed.

*The fourth arm, 2026-09-21, 07:30.* The owner's ruling was to test four before moving the
cap of three. Round 42's seed 2 was replayed to 3,000 s as a fourth arm on worker 5
(`runs/r42four-s2`), pinned to logical 0-1 and 14-15, beside three pinned arms: seed 1 at
900 bodies and 26,000 s, and seeds 4 and 5 young. It agreed with its recording on every
non-clock field, 3,780 comparisons, and the recording is the same trajectory beside two
pinned arms of its own age.

| wall for 100 to 3,000 s | as one of three | as one of four | ratio |
|---|---|---|---|
| total | 907 s | 918 s | 1.01 |
| PhysX | | | 1.05 |
| the harness | | | 1.04 |
| the world's step | | | 0.98 |

A fourth pinned arm cost itself one part in a hundred, beside neighbours heavier than the
recording had. What it cost the other three was not measured, since none of them had a
recording to read against, and this is a young crowd of 300 bodies whose working set is
small; §9's slowdown grew with the crowd, so the test that would settle it is a fourth arm
at a full crowd. On this evidence four pinned arms on the eight fast cores is sound, and
the cap is the owner's to move.
