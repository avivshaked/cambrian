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
