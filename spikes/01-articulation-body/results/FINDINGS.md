# Spike 01 — ArticulationBody at scale
Unity 6000.5.6f1   2026-08-02 14:01:52
fixedDeltaTime=0.01  solverIterations=6  solverVelocityIterations=1

## M1/M2 — build + teardown
- build   median 0.252 ms   p95 0.316 ms
- teardown median 0.082 ms   p95 0.105 ms
- combined median **0.335 ms** (budget 15 ms)
- **PASS** — rebuild-per-evaluation is affordable; pooling not required

## M3 — step cost scaling (THE ARCHITECTURE TEST)
All creatures actuated every step. `mean speed` is the awake-check —
if it collapses toward zero the bodies are asleep and timings are void.
| creatures | ms/step | ms/step/creature | vs linear | mean speed m/s |
|---|---|---|---|---|
| 1 | 0.067 | 0.0668 | 1.00× | 2.721 |
| 8 | 0.232 | 0.0290 | 0.43× | 2.853 |
| 32 | 0.815 | 0.0255 | 0.38× | 2.399 |
| 64 | 1.191 | 0.0186 | 0.28× | 2.524 |
| 128 | 1.945 | 0.0152 | 0.23× | 2.711 |

- at 64 tiled: **0.0186 ms/creature/step** (budget 0.15–0.3)
- **PASS** on absolute cost
- per-creature cost at 64 vs at 1: **0.28×**
- **PASS** — sub-linear: PhysX is parallelising across solver islands, tiling (§6.3) works

## M4 — torque stability (§4.4 effector conditioning)
- 2000 steps, 2 Hz full-amplitude sine on all 17 DOF
- max linear velocity 58.59 m/s, max angular 68.33 rad/s, max part separation 0.60 m
- NaN/Inf: no   joint separation: no   velocity blow-up: no
- **PASS** — articulations hold under the §4.4 scheme

## M5 — determinism (same seed, same process)
- 10 runs × 500 steps; max COM drift **0.000E+000 m**
- **PASS** — reproducibility claim in §7 holds within a process

## M6 — chain depth
| depth | build ms | ms/step | max |joint pos| | stable |
|---|---|---|---|---|
| 2 | 0.174 | 0.0302 | 0.629 | yes |
| 4 | 0.188 | 0.0411 | 0.660 | yes |
| 8 | 0.311 | 0.0632 | 0.847 | yes |
| 16 | 0.633 | 0.0969 | 0.926 | yes |

- DESIGN.md §4.2 caps depth at 8; this shows where solver quality actually degrades.

