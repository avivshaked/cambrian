# 0061 — The open world at the fine step

*2026-09-05. Pre-registered before launch. The confirmation round: D074 adopted in the
vent shape on the owner's ruling, run at dt 0.01 for 30,000 s on five seeds, and asked
D063 as amended.*

## Why this round exists

Two screens (0058, 0060) at the fast step established that matter as a flow makes the
world's size a flow, that only a source at the vent's base gives the outflow something to
bury, and that at 0.6 units/s with burial 0.01/s the stock had found no equilibrium in
20,000 s. The owner's ruling (2026-09-05, "agreed, proceed with your recommendations" on
the verdict of 0060): adopt the vent shape at the screened dose, confirm at the fine
step, and correct the dose on that reading rather than on the screen's. This is the
first scored condition since round 18, and the first on the open world. Everything the
fast step could not answer is asked here: whether the goal rule holds, whether the stock
levels, whether the founding survives the drain, and whether the vent's populations sit
in the film because of the step or because of the plume.

## The arms

`r24-s1` … `r24-s5`, dt **0.01**, 30,000 s, the reference world with D074 in the vent
shape: influx 0.6/s at the vent's base, burial 0.01/s, matter sink 0.02 m/s, D067's vent
on (0.05 m/s in patch 0 from 60 m, legs 1 m), age order, stock 1/m³ at start, exudation
0.15, ceiling 8,000. Launcher `scratch/launch-r24.ps1`; `-ExpectSimHash` on every
launch; four arms first (workers 2–5), the fifth on worker 6 when the perception build's
validation has released it, so the machine never carries more than five arms. Controls:
round 18's five seeds (closed world, dt 0.01, 30,000 s; logbook/0054) for the goal rule,
and 0060's vent arms for the fast-step comparison of seeds 2 and 4.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `dt=0.01`, `matter 0.02 m/s` in the sink token, `matter in 0.6/s at vent, burial 0.01/s`, `vent 0.05 m/s in patch 0 from 60 m, legs 1 m`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample; the matter identity closes; `diverged` 0 (the limiter is off at 0.01, so any divergence is a new fact and the arm is read with that caveat) | `floor`, `audit`, `diverged`, `stats.jsonl` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched, `gitDirty false` | `run.json` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the goal rule holds on the open world**: D063 as amended (all five clauses, `scripts/clade-score.ps1`) passes in ≥ 4 of 5 seeds, round 18's bar | `clade-score.ps1` |
| M2 | **no runaway**: no arm reaches the 8,000 ceiling before 30,000 s | `alive`, manifest `reason` |
| M3 | **the stock levels**: standing matter at 30,000 within 30% of its value at 20,000 in ≥ 4 of 5 | `stats.jsonl` (`scratch/matter-budget.py`) |
| M4 | **the film was the step, not the plume**: mean depth over t > 10,000 below −5 m in every arm (0060's vent arms at 0.02 ended at −0.4 and 0.2 m; the 0.01 record sits at −12 to −15 m) | `depth m` |
| M5 | **burial sees the influx at the fine step too**: `mat buried` per window ≥ 40% of `mat in` over t > 15,000 in every arm (0060: 43–50%) | `mat buried`, `mat in` |
| M6 | **founding survives the drain**: `alive` ≥ 40 at every sample to t=6,000 in ≥ 4 of 5 (0060's `r23s-s4` fell to 21) | `alive` |
| M7 | **the queue stays weak**: median parent age in the plateau (t > 10,000) below 2,000 s in every arm (closed world 4,300–4,600; 0060: 580–1,163) | `scratch/parent-age.py` |

## The two-sided readings

- **M1 holds:** the open world is the reference world; D074 is confirmed and the movement
  round (D075) launches on it. With M3 failing upward, the dose is corrected downward
  (burial 0.02 first, then influx 0.3) as a follow-up arm, not a repeat of this round.
- **M1 fails:** the open world does not hold what the closed one held. Read *which*
  clause: a stability failure with the stock still rising says the flow is disturbing
  the clade, and the dose is cut; a producer-lineage failure says something new.
- **M2 fails (the ceiling):** the arm is censored and the dose is halved for a re-run of
  that seed; four uncensored seeds still read M1.
- **M4 fails (the film at 0.01):** the plume lifts bodies, not only matter, and the
  vent's populations live at the surface for a reason the fast step did not invent.
  `EVOSIM_CURRENT_ADVECT` becomes a question for the owner; the movement round inherits
  the depth question either way.
- **M6 fails:** the drain kills founding at the fine step; the starting stock or a burial
  ramp is the fix, both world rules, both the owner's.

## Launch

Seeds 1-4 launched 2026-09-05 ~12:47 on workers 2-5 at commit `60094c0` (the open-budget
build's code, `simHash c43976d3d71f1f52`, unchanged since 1ce2e71; prose commits since),
`-ExpectSimHash` on each; every manifest reads that `simHash`, `gitDirty false`,
`physicsDtSeconds 0.01`, `status running`. Headers verified as V1 on all four: `dt=0.01`,
`sink 0.002 m/s, matter 0.02 m/s`, `matter in 0.6/s at vent, burial 0.01/s`, `vent 0.05
m/s in patch 0 from 60 m, legs 1 m`, `conception age`, `exudation 0.15`. Seed 5 follows on
worker 6 when the perception build's validation releases it. Monitor running. Results
appended below.
