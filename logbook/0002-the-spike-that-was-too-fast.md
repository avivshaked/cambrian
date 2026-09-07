# 0002 — The spike that was too fast

**2026-08-02**  ·  Spike 01

Spike 01 passed all six of its measurements, most of them by two or three orders of
magnitude. That was the bug. The scene has no gravity and nothing was driving the creatures,
so the physics engine put all sixty-four of them to sleep and stopped doing any work at all.
I was timing an empty solver and reporting it as the cost of sixty-four creatures. Two
measurements in the same run disagreed with each other, and that is the only reason I caught
it. Driven properly, the architecture still passes, with a comfortable margin instead of an
absurd one.

## What the spike was for

A spike is a throwaway project built to answer one question and then abandoned. This one
asks whether Unity's `ArticulationBody`, the engine's jointed-body type, can build, simulate
and destroy creatures fast enough for an evolutionary loop that runs thousands of them. A no
would change the whole runtime architecture in [`DESIGN.md`](../DESIGN.md) §6: object
pooling, possibly a different physics backend, possibly a different engine.

The budgets in [the spike spec](../spikes/01-articulation-body/README.md) were derived
backwards from the throughput targets in §6.4. A pass means the architecture stands. A fail
means it does not.

## The first run passed everything, and that was the problem

All six measurements came back green. They are numbered M1 to M6, and the one that matters
is M3: the per-creature cost of stepping many creatures tiled into a single scene. It came
in two to three orders of magnitude under budget.

Two things stopped me from calling that a pass. The numbers were better than the hardware
could plausibly deliver. And a second measurement in the same run contradicted them.

M6 walks chain depth and reports the cost of one actuated creature. It said roughly
0.06 ms/step for a single depth-8 chain. M3 said roughly the same figure for sixty-four
creatures. The same harness produced both, in the same process, seconds apart. At least one
of them had to be wrong.

## The solver had gone to sleep

M3 built its creatures and stepped the physics without driving anything. The spike scene has
no gravity, because it is modelling water. So every body settled, and PhysX, the physics
engine Unity runs, put the whole scene to sleep and stopped integrating it.

The harness was timing an empty solver and reporting the result as the cost of sixty-four
creatures.

## The fix, and the instrument it left behind

Two changes, and the second matters more than the first.

1. Actuate every creature on every step of the measurement.
2. Report the mean body speed of the creatures beside every timing, as a permanent
   awake-check. If that column collapses toward zero, the timings are void and the table
   says so.

A benchmark that cannot prove its subject was doing anything is no benchmark. The speed
column is what turns a number into a number I can trust.

## What it actually costs

Re-measured with everything driven. Watch the last two columns. The cost per creature falls
as creatures are added, and mean speed holds near 2.5 m/s all the way down, which is the
awake-check doing its job. Measured against
[`FINDINGS.md`](../spikes/01-articulation-body/results/FINDINGS.md) on Unity 6000.5.6f1 and
an i9-13900K.

| creatures | ms/step | ms/creature/step | vs linear | mean speed m/s |
|---|---|---|---|---|
| 1 | 0.067 | 0.0668 | 1.00× | 2.721 |
| 8 | 0.232 | 0.0290 | 0.43× | 2.853 |
| 32 | 0.815 | 0.0255 | 0.38× | 2.399 |
| 64 | 1.191 | 0.0186 | 0.28× | 2.524 |
| 128 | 1.945 | 0.0152 | 0.23× | 2.711 |

That is more than an order of magnitude worse than the sleeping run, and still comfortably
inside a 0.15–0.30 ms/creature budget. Building and tearing down a 10-part creature costs
0.335 ms against a 15 ms budget. Ten runs from the same seed came out identical to the last
bit, with 0.0 m of drift between them.

The falling per-creature cost is the result the architecture depends on. PhysX solves groups
of touching bodies together, as units it calls islands, and creatures tiled far apart never
share one. So putting many creatures into a single scene is close to free, and that is what
makes §6.3 viable.

## What follows from it

Pooling is unnecessary. It sat in the design as a probable optimisation, and at 0.335 ms to
build and destroy a creature it would be complexity bought for nothing
([`DECISIONS.md`](../DECISIONS.md) D010).

The island model is no longer a throughput requirement. It splits the population into
subpopulations that evolve apart and occasionally exchange migrants. One process
extrapolates to about 1,600 evaluations per minute, and the original target was that same
figure spread across ten processes. It stays in the roadmap because isolated subpopulations
are useful to evolution, and no longer because the hardware needs it (D011).

## The caveats, recorded so they are not forgotten

The spike measured less than the real system will do. There are no fluid forces in it and no
brain evaluation. Per-creature neural updates are managed C# running in the hot loop, the
code that executes every single step, and none of that is in these numbers. Collision is
switched off entirely with `IgnoreLayerCollision`, which is realistic for creatures tiled
far apart in water and will not survive Milestone 5.

Determinism was tested inside a single process only. PhysX does not give identical results
across machines or Unity versions, so §7 uses a hash of the configuration to detect
divergence rather than to promise portability.

## Two things to do differently next time

The exact first-run numbers are gone. The harness overwrote `results/` on the corrected run,
so the wrong table cannot be quoted here and can only be described. Wrong results are
evidence. A future harness should write to a timestamped directory, or at the least refuse
to overwrite.

Cross-referencing caught the bug, and inspection would never have. Nothing about M3's code
or its output looked wrong on its own; it looked excellent. What exposed it was two
measurements of overlapping quantities disagreeing. That argues for deliberately building in
redundant measurements whose results have to agree. They cost little, and they are the only
thing that caught this.

[`DESIGN.md`](../DESIGN.md) §11.1 is marked resolved, with these numbers and these caveats.
[`CLAUDE.md`](../CLAUDE.md) carries the short form under "Gotchas discovered the hard way",
because it is the kind of thing that will happen again.
