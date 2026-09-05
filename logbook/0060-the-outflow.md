# 0060 — The outflow

*2026-09-05. Pre-registered before launch. D074's dose correction: the open budget with
the matter sink fast enough for burial to see what the influx built, and the influx at
the vent's base so the deep sees it first.*

## Why this round exists

0058 showed the world's size is a flow and that the flow has no working outflow: matter
arrives at the surface, the leaves lock it within a step, and a dead body's matter at
−1 m sinks at 0.002 m/s — eight hours to the floor, where burial waits. Burial took a
fifth or less of the influx at every dose and the stock grew toward the ceiling. Two
levers connect the outflow to the population without a new rule: the matter sink at
0.02 m/s (D048's default; D071's screen found it harmless alone because nothing was
free to sink), and the influx at the vent's base (D074's second shape), where D067's
plume lifts it through the deep before the leaves see it.

## The arms

Reference world, dt 0.02, 20,000 s, influx 0.6/s, burial 0.01/s, age order, four arms:

| arm | influx at | matter sink | vent |
|---|---|---|---|
| `r23s-s2`, `r23s-s4` | surface | **0.02** | off |
| `r23v-s2`, `r23v-s4` | **vent** | **0.02** | on (D067: 0.05 m/s, patch 0, from 60 m, legs 1 m) |

Controls: 0058's `r22o-s2` / `r22o-s4` (surface, sink 0.002, vent off) and 0056's
closed `r20q0-s2` / `r20q0-s4`. The vent arms change two things at once (the plume and
the source); the surface arms isolate the sink. Launcher `scratch/launch-r23.ps1`;
workers refreshed and launched with `-ExpectSimHash`; four concurrent.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `matter 0.02 m/s` in the sink token, `matter in 0.6/s at surface` or `at vent`, `burial 0.01/s`, `vent off` or `vent 0.05 m/s in patch 0 from 60 m, legs 1 m`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample; the matter identity closes | `floor`, `audit`, `stats.jsonl` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched; `diverged` reported | `run.json` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **burial sees the influx**: `mat buried` per window ≥ half of `mat in` by t=15,000 in every arm (0058: a fifth or less) | `mat buried`, `mat in` |
| M2 | **the stock levels**: standing matter at 20,000 s within 30% of its value at 15,000 s in every arm (0058: +25–35% over that interval) | `stats.jsonl` |
| M3 | **the vent wets the deep**: in the vent arms `mat deep` (−54 m) ≥ 0.5 units/m³ over t > 10,000 and `matterHere` ≥ 0.3; in the surface arms neither | `mat deep`, `matterHere` |
| M4 | **the stomachs' share rises where the deep is wet**: absorptive share of the population at 20,000 s higher in each vent arm than in the same seed's surface arm | `absorpt`, `alive` |
| M5 | **a stable clade in every arm** (≥ 10 through the last 6,000 s) | `scripts/clade-score.ps1` |
| M6 | **the population is still a flow** but bounded: `alive` at 20,000 s between the closed world's 1,800 and 0058's 4,000–4,300 at the same dose | `alive` |

## The two-sided readings

- **M1–M2 hold:** the budget balances; with M3–M4 the vent shape is the open world to
  put to the owner for adoption and confirm at 0.01 — the ocean's upwelling, not its
  dust. With M3 holding and M4 failing, the deep is wet and the stomachs still do not
  gain: the contest, not the supply, and D073's finding stands in the open world too.
- **M1 fails with the sink at 0.02:** the floor is not where the matter goes even at
  ten times the sink — read where it is (the field by layer) before another dose.
- **M2 fails upward with M1 holding:** burial keeps pace proportionally and the stock
  still grows — the dose is simply high; halve the influx.
- **M6 fails upward (the ceiling):** the arm is censored and the dose halved.
- **M5 fails in the vent arms only:** the plume's return flow is where the stomachs
  were (round 13's reading, D067) and it carries them off the source; a spatial
  question the movement round inherits.

## Launch

Appended below.
