# Spike 01 — `ArticulationBody` at scale

**Status:** Specified, not implemented (Unity Editor not yet installed)
**Blocks:** Milestones 1–4. This is risk 11.1 in [`../../DESIGN.md`](../../DESIGN.md).
**Expected effort:** ~1 day
**Disposable:** yes — this code is thrown away once the question is answered.

---

## 1. The question

> Can Unity's `ArticulationBody` support the evaluation loop the design assumes —
> hundreds of creatures built, simulated, and destroyed per minute, tiled many-per-scene,
> under high joint torque?

The design commits to `ArticulationBody` over `Rigidbody` + `ConfigurableJoint` because
articulations are built for kinematic trees and are far more stable under high torque
(`DESIGN.md` §6.2). That stability claim is well-founded. **The scale claim is not.**

**This cannot be resolved by reading.** No paper in the review answers it — Krčah used
ODE, and the PhysX precedent in [L21 Table 2] is Lessin's, through a different
integration. Unity-specific articulation construction cost, teardown cost, and
many-instance behaviour are only observable by running code.

**Why it comes first:** §6.2, §6.3, §6.4 and the entire throughput plan sit on top of
"we can simulate many articulated creatures fast." If that's false, the fix is
architectural (DOTS / Unity Physics), not a parameter change. Discovering it at
Milestone 4 would waste the intervening work.

---

## 2. Budget — where the thresholds come from

Derived from `DESIGN.md` §6.4 (500–2000 evaluations/minute across ~10 worker processes):

```
Target:            50–200 evaluations / minute / process
Tiling:            64 creatures simulated concurrently per scene (§6.3)
Evaluation:        ~2000 fixed steps  (§5.5: 1/100 s over 20–30 s)

64 evals per 2000-step batch
  → at 100 evals/min, a batch must complete in ~38 s → 19 ms per step for 64 creatures
  → at 200 evals/min, a batch must complete in ~19 s → 9.5 ms per step for 64 creatures

╔══════════════════════════════════════════════════════════════════╗
║  STEP COST TARGET:  0.15 – 0.30 ms per creature per step         ║
║                     (10-part creature, 64 tiled)                 ║
╚══════════════════════════════════════════════════════════════════╝

Construction + teardown must stay under ~5% of batch time:
  0.05 × 19 s / 64 creatures ≈ 15 ms per creature
╔══════════════════════════════════════════════════════════════════╗
║  BUILD+DESTROY TARGET:  < 15 ms per creature                     ║
║                         (above this → pooling required)          ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## 3. What to measure

| # | Measurement | Method | Decision threshold |
|---|---|---|---|
| **M1** | Construction cost | Build a 10-part articulated tree from parameters, time it. 100 reps, report median + p95 | < 15 ms → fine. > 15 ms → pooling (§6) |
| **M2** | Teardown cost | `Destroy()` the hierarchy, time to next successful build. 100 reps | Same threshold. Suspect this is the expensive one |
| **M3** | Step cost scaling | `physicsScene.Simulate(dt)` with 1, 8, 32, 64, 128 creatures tiled 100 m apart on mutually-ignoring layers. Time per step | **Must scale sub-linearly.** Linear scaling means no solver-island parallelism and kills §6.3 |
| **M4** | Torque stability | Drive all joints with the §4.4 scheme (clamp → mass-scale → 10-step average) at max amplitude for 2000 steps | Zero NaN, zero joint separation, no unbounded velocity |
| **M5** | Determinism | Same seed, same parameters, 10 runs, same process. Compare final COM position | Bitwise identical, or < 1e-4 m drift. Anything larger breaks §7 |
| **M6** | Depth limit | Build chains of depth 2, 4, 8, 16 | Confirm the §4.2 depth cap of 8 is safe; find where solver quality degrades |

**M3 is the one that decides the architecture.** If step cost scales linearly with creature
count, PhysX is not parallelising across islands and the tiling strategy in §6.3 is worthless
— which forces the DOTS decision immediately.

---

## 4. Scope — deliberately narrow

**In:** runtime `ArticulationBody` construction from a parameter struct; tiling; manual
physics stepping; the §4.4 effector conditioning; timing harness; CSV output.

**Out — do not build any of this:**
- Genome, mutation, development rules
- MAP-Elites, archives, descriptors
- Fluid forces (M4 uses raw joint torque; drag is irrelevant to these questions)
- Rendering beyond a bare scene view
- Serialization, island model, theatre

The temptation will be to start writing the genome. Resist it — if M3 fails, the genome
gets rewritten against a different physics backend anyway.

---

## 5. Implementation sketch

```
spikes/01-articulation-body/
  Assets/
    Spike/
      CreatureSpec.cs      // plain struct: part dims, joint types, tree shape.
                           //   NOT the genome — hardcoded/random parameters only
      ArticulationBuilder.cs  // CreatureSpec -> ArticulationBody hierarchy
      ArticulationPool.cs     // M1/M2 fallback: reconfigure instead of rebuild
      TiledArena.cs           // N creatures, 100 m grid, layer assignment
      EffectorDriver.cs       // §4.4: clamp [-1,1] -> mass-scale -> 10-step average
      SpikeHarness.cs         // runs M1..M6, writes results/*.csv
  ProjectSettings/
  Packages/manifest.json
  results/                 // gitignored
```

Key configuration, from the design:

```csharp
Physics.simulationMode = SimulationMode.Script;   // §6.2 — manual stepping
Time.fixedDeltaTime    = 0.01f;                   // §5.5 — 1/100 s
// solver iteration counts fixed and recorded — they enter the configHash (§7)
```

Joint types to cover, from `DESIGN.md` §4.1 / [K12 §2.1, p.3]: fixed (0 DOF), hinge (1),
twist (1), hinge-twist (2), twist-hinge (2), universal (2), spherical (3).

---

## 6. Fallback if M1/M2 fail

Per `DESIGN.md` §11.1, the mitigation is a **pool of pre-allocated articulations
reconfigured between evaluations rather than rebuilt.** `ArticulationPool.cs` is in the
sketch above so this is testable in the same spike rather than a follow-up.

Open sub-question the spike should answer: **how much of an articulation can actually be
reconfigured at runtime** without teardown — joint limits and drive targets almost
certainly; body dimensions probably; tree topology almost certainly not. If topology is
fixed at construction, the pool must be bucketed by topology, which constrains how freely
morphology can vary within a batch. That would be a genuine design constraint, not just a
performance detail — record it if found.

---

## 7. Outcomes and what each one means

| Outcome | Meaning | Action |
|---|---|---|
| All targets met | Architecture confirmed | Proceed to Milestone 1, delete this spike |
| M1/M2 fail, pooling works | Rebuild is expensive but avoidable | Adopt pooling, note the topology constraint in §4.2, proceed |
| M1/M2 fail, pooling blocked by topology | Morphology can't vary freely within a batch | Redesign the batching strategy before Milestone 1 |
| **M3 scales linearly** | **No island parallelism — §6.3 is invalid** | **Escalate to DOTS / Unity Physics now**, revise §6 |
| M4 fails | Articulations no more stable than joints under our torques | Re-open the §4.4 effector decision; consider PD targets |
| M5 fails | Reproducibility (§7) unachievable as specified | Reconsider what `configHash` can promise; weaken §7 claims honestly |

Record the result in `results/FINDINGS.md` and update `DESIGN.md` §11.1 with measured
numbers replacing the current "unknown to me."
