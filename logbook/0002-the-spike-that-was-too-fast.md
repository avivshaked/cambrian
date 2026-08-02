# 0002 — The spike that was too fast

**2026-08-02**  ·  Spike 01

Spike 01 exists to answer one question before any real code is written: can Unity's
`ArticulationBody` build, simulate and destroy creatures fast enough for an evolutionary
loop? If not, the entire runtime architecture in [`DESIGN.md`](../DESIGN.md) §6 changes —
object pooling, possibly a different physics backend, possibly a different engine.

The budgets in [the spike spec](../spikes/01-articulation-body/README.md) were derived
backwards from the throughput targets in §6.4, so a pass means the architecture stands and a
fail means it doesn't.

## The first run passed everything

All six measurements green. M3 — the one that actually matters, per-creature step cost with
many creatures tiled in one scene — came in two to three orders of magnitude under budget.

That should have been the end of it. The reason it wasn't: the numbers were better than the
hardware could plausibly deliver, and there was a second measurement in the same run that
contradicted them.

M6 walks chain depth and reports the cost of a **single** actuated creature. It said roughly
0.06 ms/step for one depth-8 chain. M3 said roughly the same figure for **sixty-four**
creatures. Both were produced by the same harness, in the same process, seconds apart. At
least one had to be wrong.

## The cause

M3 built its creatures and stepped physics without driving anything. The spike scene has no
gravity — it's modelling water. So every body settled, and PhysX put the entire scene to
sleep and stopped integrating it.

The harness was timing an empty solver and reporting it as the cost of sixty-four creatures.

## The fix

Two changes, and the second matters more than the first:

1. Actuate every creature on every step of the measurement.
2. Report **mean body speed** alongside every timing, as a permanent awake-check. If that
   column collapses toward zero, the timings are void and the table says so.

A benchmark that can't prove its subject was doing anything isn't a benchmark. The speed
column is the difference between a number and a number you can trust.

## What it actually costs

Re-measured, with everything driven ([`FINDINGS.md`](../spikes/01-articulation-body/results/FINDINGS.md),
Unity 6000.5.6f1, i9-13900K):

| creatures | ms/step | ms/creature/step | vs linear | mean speed m/s |
|---|---|---|---|---|
| 1 | 0.067 | 0.0668 | 1.00× | 2.721 |
| 8 | 0.232 | 0.0290 | 0.43× | 2.853 |
| 32 | 0.815 | 0.0255 | 0.38× | 2.399 |
| 64 | 1.191 | 0.0186 | 0.28× | 2.524 |
| 128 | 1.945 | 0.0152 | 0.23× | 2.711 |

More than an order of magnitude worse than the sleeping run, and still comfortably inside a
0.15–0.30 ms/creature budget. Build plus teardown for a 10-part creature is 0.335 ms against
a 15 ms budget. Determinism across 10 runs at the same seed came out bitwise identical, 0.0 m
drift.

The falling per-creature cost is the result the architecture depends on: PhysX parallelises
across solver islands, so tiling many creatures into one scene is close to free. That's what
makes §6.3 viable.

## Consequences

- **Pooling is unnecessary.** It was in the design as a probable optimisation. At 0.335 ms to
  build and destroy, it would be complexity bought for nothing ([`DECISIONS.md`](../DECISIONS.md) D010).
- **The island model is no longer a throughput requirement.** One process extrapolates to
  ~1,600 evaluations per minute; the original target was that figure spread across ten. It
  stays in the roadmap because isolated subpopulations are evolutionarily useful, not because
  the hardware needs it (D011).

## Caveats, recorded so they aren't forgotten

The spike measured less than the real system will do. No fluid forces, no brain evaluation —
per-creature neural updates are managed C# in the hot loop and are not in these numbers.
Collision is disabled entirely via `IgnoreLayerCollision`, which for tiled creatures in water
is realistic but will not survive Milestone 5. Determinism was tested within a single
process only; PhysX is not bitwise deterministic across machines or Unity versions, which is
why §7 uses a config hash to *detect* divergence rather than promising portability.

## Two things to do differently next time

**The exact first-run numbers are gone.** The harness overwrote `results/` on the corrected
run, so the wrong table can't be quoted here — only described. Wrong results are evidence.
Future harnesses should write to a timestamped directory, or at minimum refuse to clobber.

**The bug was caught by cross-referencing, not by inspection.** Nothing about M3's code or
its output looked wrong in isolation; it looked excellent. What exposed it was two
measurements of overlapping quantities disagreeing. That argues for deliberately building in
redundant measurements whose results have to be consistent — they cost little and they are
the only thing that caught this.

---

**See also:** [`DESIGN.md`](../DESIGN.md) §11.1 (marked resolved, with these numbers and
these caveats). [`CLAUDE.md`](../CLAUDE.md) carries the short form under "Gotchas discovered
the hard way", because it is the kind of thing that will happen again.
