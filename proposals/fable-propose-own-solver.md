# Proposal: a solver of our own, the farm out of Unity, and the road to the GPU

*Fable, 2026-09-21, brought current 2026-09-22. The owner ruled the direction on the 21st:
pace before a more complex world, staggered, with 10,000 creatures as the committed target
and 100,000 a stretch. Stages 1 and 2 are built and on main (`9eb262a`, `bd608d8`,
`7fb748c`), round 43 ran on them (logbook/0111), and D104 took the first of the rulings
below. This text is the plan as it stands and the rulings still open. Absorbed into
DECISIONS.md on ruling, then deleted.*

## Why

Round 42 (logbook/0110) ran 900 bodies at 0.3 to 0.5 times real time with twenty of
twenty-four cores idle. PhysX took 37 to 44% of the wall and the harness around it about
50%, both on one thread: PhysX because touching articulations solved on several threads
part from their recording (D078), the harness because every read from and write to the
engine is on the main thread. The cost per body tripled as bodies gained links and began
to touch, which is the direction the worlds have to go. Halving the matter again gave 127
bodies, too few to read (`r42half-s4`).

## What the spike measured

`Evosim.Dynamics`: Featherstone's articulated-body algorithm in plain C#, doubles, flat
arrays, no Unity, each creature stepped alone, the water ported term for term, contact a
soft push computed from the previous step.

| | Result |
|---|---|
| Correctness | two independent constraint solves agree with it to 1e-8 (hinge chain, ball joint); internal drives move no momentum (7e-12 N) |
| Stability | 1,000 of round 42's bodies, 600 s at dt 0.01 under their own brains: none lost. At 0.02, three lost |
| Identity | state digests equal to the bit at 1, 4 and 16 threads after 10,000 steps |
| Pace, one thread | 2.9 µs a body-step at 1,000 bodies, 6.1 at 4,000; PhysX and its harness cost about 20 |
| Pace, 24 threads | 0.34 µs at 1,000 bodies, 0.50 at 4,000, on a machine running two arms |
| Parity with PhysX | forty of round 42's jointed bodies, alone in still water, 60 s: with PhysX's self-collision off, 40 of 40 agree within 0.05 rad on every joint (worst 0.043 after the implicit limit and damper); with it on, as the farm runs, 12 of 40. Two hand-built strokers (0.70 and 0.76/0.43 rad at 2 s) agree to 1e-4 rad and 0.1 mm over 60 s |
| A full seed | round 42 seed 1's world, 30,000 s: 24 minutes at 16 threads, both books closed, nothing lost, lineage byte-equal at 8 and 16 threads |
| A round | round 43, three seeds at 8 threads each in 80 minutes: round 42's crowd, treadmill, larder and spread; the joints held in two seeds of three where Unity lost them in three of five |
| 10,000 bodies | 3,536 bodies at 4.7x on the CPU, the solver at 0.32 µs a body-step; about 2.4x at 10,000, extrapolated |

The parity result is also a finding about the record. In the farm a body's own parts
collide, and a part touching its sibling or a non-adjacent part stops a driven joint
within a step, turns the motion onto another axis, or moves an undriven body. Twenty-eight
of forty evolved jointed bodies were affected. A driven joint in the farm has mostly not
been free to turn.

The parity rows are still water and no economy; the stroke is hand-built because round
42's jointed bodies hold a pose. No evolved body has yet stroked in either engine.

## The four stages

Each ships a better simulator, and each has a gate that is measured before the next.

| Stage | What it is | Gate |
|---|---|---|
| 1 | The farm leaves Unity: `Evosim.Farm`, a .NET console program stepping Core's `World` with `Evosim.Dynamics`, threaded, bit-identical at any thread count | parity on a stroke; both books close on a 3,000 s smoke; digests equal at 1, 8 and 24 threads for 3,000 s with births and deaths |
| 2 | The grid and the economy threaded (the grid's 0.17 s of wall a simulated second is the next ceiling, about 6x) | a base round of three seeds read against round 42's distributions |
| | *The animal kit and the floor's places resume here, on rounds that take hours* | |
| 3 | The GPU port, single precision, checked against the CPU reference at 1,000 bodies | distributions agree across seeds; replay on the same card |
| 4 | Scale: a larger tank, binary forms of the high-volume run files, the theatre drawing from a state stream | measured pace at 10,000; 30,000 and 100,000 only if the headroom is there |

Estimates, not measurements: today's worlds at about 5x after stage 1 and 15x after stage
2; 10,000 creatures at 1 to 2x on the CPU and 20 to 50x on the RTX 4090.

## Stage 1 in work packages

From the inventory of `Ecosystem.cs`, `EvolutionRun.cs` and the files around them. One
fact makes the plan possible: no random draw happens anywhere in per-body physics-step
code, so one parallel region over bodies, with every sum across bodies taken serially in
`World.Living` order, keeps identity.

| | Package | Acceptance |
|---|---|---|
| A | Dynamics as a local Unity package beside Core | Unity compiles against it; tests green |
| B | The work and dissipation ledgers (absent from the spike; `World.Observe` bills from the first, the energy audit reads the second) | work less kinetic change less dissipation under 1e-3 of the work, 600 s |
| C | The current and D100's water hold | a tracer follows the streams' analytic line |
| D | Growth resize in place | no jump at a resize, to the bit |
| E | Contact, the shaped bed, the glass, the contact instrument | 1,000 bodies on the bed 600 s: none lost, none under it |
| F | Divergence guards, dumps, the trace, the digest file | digest equal at 1, 8, 24 threads with births and deaths |
| G | The farm loop and the metabolic handoff (the join) | `audit` and `mat resid` read 0 on a 3,000 s smoke |
| H | The placer (`SharedVolume`) out of Unity | founder and offspring places equal the Unity placer's to the bit, 5,000 draws |
| I | The `EVOSIM_*` binding, the manifest, the report | the same launcher gives the same `configHash` |
| J | The writers | `positions.jsonl` unchanged, so the theatre reads it |
| K | The timers | phases sum to the run clock within 1% |

The theatre: first a `poses.jsonl` beside `positions.jsonl` so `-From snapshot` draws
bodies as they were posed (it cannot today); then a replay that steps `Evosim.Dynamics`
inside the Editor, which will be faithful at any thread count.

## What changes in the world, for the owner to rule

1. **Joint limits.** PhysX's stops cannot be passed; a spring's can. Built as an implicit
   spring-damper (its stiffness on the solver's diagonal, `JointLimitOmegaTimesStep` 1.5,
   critically damped), which overshoots by about 0.01 rad against PhysX's hard stop and
   is what the 40-of-40 parity was measured on. The ruling is whether 0.01 rad of
   overshoot is acceptable as the joint's stop; a velocity-level hard stop on top is the
   alternative and was not needed for parity.
2. **Contact between bodies** becomes a soft push between bounding spheres: no friction,
   no wedging, no depenetration. Finer contact (a sphere a part) is a later option if the
   bite needs it, and it will.
3. **D101's refusal of a folded body stays.** It lives in Core and needs no physics. With
   self-collision gone its physical reason is gone too, and the owner may prefer to retire
   it; I would keep it through the base round so that round reads against round 42.
4. **The contact instrument** is redefined on sphere overlaps, and its columns get new
   names, since the numbers will not compare across the change.
5. **A ball joint's angle**, as the brain senses it, becomes the rotation vector's
   component and not PhysX's swing and twist.
6. **The record.** Every run on file stays valid as recorded and none replays on the new
   engine. Mode B for old runs stays on the Unity path while `unity/` still carries
   `Evosim.Sim`. `run.json` keeps `coreHash` and `configHash`, and `simHash` gives way to
   `dynamicsHash` and `farmHash` (built so; the ruling confirms it).
8. **Ruled: D104**, the water sampled per link (D100's hold retired at 0), 2026-09-22.
9. **The contact law's two numbers** (`MaxSeparationSpeed` 1 m/s per pair,
   `MaxContactSpeedChange` 1 m/s per body per step): a soft push between spheres cannot
   throw a body, and the per-body cap means a body in a crowd is not conserving momentum
   with its neighbours exactly. Round 43 ran on them (`ovl held %` 93 to 99: most overlaps
   persist rather than resolve). The ruling is whether that is the contact the world has,
   or whether a stiffer, momentum-conserving push is wanted before the bite, which will
   need contact to mean something.
7. **DESIGN §11.1 and the ArticulationBody decision are superseded**, with a DECISIONS
   entry saying why: the spike it rested on measured capacity, and the campaign measured
   pace.
