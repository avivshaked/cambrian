# Opus read of the water sampling (A2) and the body's step (A3), 2026-10-04 evening

A read-only Opus subagent's report, saved as it came. Nothing was built or run. Line numbers are
`scratch/wt-speed/src` with the profile switch's uncommitted edits in `DynamicsWorld.cs`. [m] measured
(the 2026-10-02 single-thread split it was given), [b] believed from the code, [g] guessed.

Path facts [b]: no reefs (reefCover 0) and no shore fade, so `VelocityAndAccelerationAt` (CurrentField.cs:1094)
falls through to separate `VelocityAt` and `AccelerationAt`; water hold 0, Transport, coefficient 1, so
`Water.Sample` takes its one-call branch (Water.cs:79-88); the nursery runs `Simulation(..., threads 1, runDirectory
null)` (Tank.cs:141), which turns `ContactInstrument` on and the trace on for jointed bodies, with no divergence dumps;
`TieredCompilation` false.

## A2: one link's water sample (velocity and acceleration)

| Piece | file:line | Ops per call [b] | Notes |
|---|---|---|---|
| Guards and dispatch | CurrentField.cs:1094-1107, 1053-1075, 1574-1592, 2220-2238, 5822-5845 | ~15 branches, 2 divisions, 2 casts of the scale float | All invariant for the pass |
| Bed, on the velocity's miss | MapAt 2397-2427 -> BedSample 814-836 ([ThreadStatic] memo) -> BedShape.cs:1002-1064 | 24 transcendentals (12 cos, 12 sin), ~220 flops, 48 loads, 2 divisions | The acceleration call hits the memo |
| StreamsUnit (velocity) | 5109-5238 | 1 sqrt, 2 trig (phi), ~22 divisions, ~520 flops, ~150 loads, ~75 branches | Recomputes w·a_k·env_k, which is `_pinnedEddyBase[k]` (4941); cells recompute `_pinnedCellBase` |
| Sloped map and gradient wrapper | 3673-3750 | 3 divisions, ~75 flops | Repeats MapAt's work to the same bits |
| StreamsUnitWithGradient | 5350-5604 | 1 sqrt, 2 trig (the same phi), ~30 divisions (ky = q·π/D in all 24 terms, plus 3 cells), ~1,650 flops, ~170 loads | raw·env and raw·envRate depend only on the instant |
| Total per link | | 28 transcendentals, 2 sqrt, ~55 divisions, ~2,500 flops, ~250 bounds-checked loads, ~12 calls | ~1.6 µs a link [b] |
| Per step: pin | PinInstant 4882-4925, EnsureInstant 5008-5063 | 1-2 slot fills × 108 trig, ~1.5-3 µs a step | t1 and t2 differ by an ulp on some steps; not BI to avoid |
| Per step: Parallel.For and closure | DynamicsWorld.Forces.cs:51 | ~1 µs | |

## A3: the body's own step (2 links, 1 joint), ns per body-step

| Piece | file:line | ns | Notes |
|---|---|---|---|
| Senses | CreatureSenses.cs:177-288 | 370 [m] | |
| Brain | | 440 [m] | |
| Drive | EffectorDrive.cs:172-331 | ~80 [b] | |
| Fluid.Apply | Fluid.cs:32-249, panels 78-91 | ~600-900 [b] | 24 panels a box or sphere link, ~56 a capsule |
| Contacts (per part) | Contacts.cs:847-998 | ~800-1,000 [b] | ContactBed.cs:66 -> BedShape.HeightAndGradient (938-981): 24 trig and a sqrt a link, ~250-300 ns, every step; NeighbourLinks 523-568 ~100-200 ns |
| JointTorques | | ~20 [b] | |
| Aba.Solve and Integrate | Aba.cs:35-300 | ~450-550 [b] | |
| Kinematics | Kinematics.cs:40-140 | ~250 [b] | |
| Settle | Creature.Ledger.cs:160-220 | ~40 [b] | |
| RecordTraceFrame | CreatureTrace.cs:119-195 | ~100-150 [b] | On for jointed bodies (Reconcile.cs:221/384); nothing reads it in the nursery |
| IsFinite, RefreshContactSphere | Creature.cs:710, 493-550 | ~50 [b] | |
| Share of the body loop's Parallel.For | DynamicsWorld.cs:162 | ~125 [b] | |
| Sum | | ~3.6-4.0 µs | Matches the measured ~4 µs; nothing allocates inside StepOne |

Grid build and after-step [b, inferred from measured]: `BuildLinks` and `Fill` (Contacts.cs:356-503, 155-307) make 8
`Slabs.Run` calls a step (Slabs.cs:46-53), each a `Parallel.For` with its own options and closure over trivial work;
commit makes 1 and after-step 2. At ~0.9-1.4 µs a call that is nearly all of the grid's 7.6 µs a step.

## Speed-ups

| # | Idea | Class | Expected gain | Risk | Lines | Bit-identity test |
|---|---|---|---|---|---|---|
| 1 | Serial path when one slab or one thread: Slabs.Run calls body(0,0,items) directly; StepOnCpu and SampleWater loop directly; cached delegates | BI | grid 1.8 -> ~0.15 s, commit 0.45 -> ~0.12, after 0.46 -> ~0.05, water and bodies ~-0.23 each: ~-2.8 s of 17.0, ~16% of the physics step [b] | Low; the "one path" comment rests on tiered compilation, which is off | ~30 | Digest at 1 and 2+ threads; WaterThreadIdentityTests; nursery rows byte for byte |
| 2 | Per-pin folded tables (eddyRateBase, cells' amplitude and rate, ky[q]) used when the clock equals the pin | BI | water -10 to -15%, step -4 to -6% [b] | Low | ~80 | Old vs new VelocityAt/AccelerationAt bit for bit at ~10^4 points × instants, both floors; digest |
| 3 | Fused, flattened tank point sampler on #2 (shared prologue once; unrolled sums) | BI | #2+#3: water -30 to -35%, step -12 to -14% [b/g] | Moderate | ~400 | As #2, at the axis, glass, surface and floor |
| 4 | ContactBed early-out when the sphere is provably clear of the highest floor | BI | bodies -10 to -14%, step ~-5% if mostly off the bed [g] | Low; the bound must be strictly conservative | ~30 | Push old vs new on random and grazing spheres; digest |
| 5 | No EnableTrace in the nursery | BI for the physics | ~1.3% | Loses the post-mortem trace | ~5 | Digest |
| 6 | Fluid panels: precompute -k·area, drop bounds checks | BI | 1-2% of the step [g] | Low | ~40 | Digest |
| 7 | [SkipLocalsInit] on Aba's stackallocs | BI | <1% | Needs unsafe | ~5 | Digest |
| N1 | Velocity read off the gradient pass | NR | water a further -25% | New realisation | ~20 | |
| N2 | Math.SinCos or *invR | NR | small | | | |
| W1 | Water hold > 0, panels per axis 1, or D090's coefficient 0 | WR | water -50% / fluid -60% / water -60% | The owner's | config | |

Recommendation: #1 first (largest, cheapest, no arithmetic touched), then #2 then #3 on the water phase (39% of
the step [m]), then #4 after checking how often nursery bodies are within reach of the bed. #1-#4 together about
30-35% off the physics step [b/g]. #5 is free if the owner does not need the trace in the nursery.
