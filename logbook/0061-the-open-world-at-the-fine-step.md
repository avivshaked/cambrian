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
m/s in patch 0 from 60 m, legs 1 m`, `conception age`, `exudation 0.15`. Seed 5 launched ~15:40 on worker 7 **on the perception build** (commit `e59f6af`,
`simHash 30b96bf6f4da339b`): every worker compiles Core from the main tree, so once that
build landed no worker could carry round 24's exact source. The build replays the
previous one byte for byte at its default (logbook/0062: 20 of 20 rows over 2,000 s at
0.01 on r16dt-01c's settings) and the header's `senses` token reads the default four;
seed 5 is read on that evidence and the split is recorded here. Its header otherwise
matches V1. One launch was refused first, on a hash taken from `scratch/simhash.py`,
which disagrees with the C# on this tree (CLAUDE.md). Monitor running. Results appended
below.

## Results

Five arms to budget (`status ended`, `reason budget`), `divergedTotal` 0 in all five, the
audit 0.0000% at every sample, `floor` 0 from t=3,100, the matter identity to the unit.
V1–V3 held. Seeds 1–4 on the open-budget build (`simHash c43976d3…`), seed 5 on the
perception build (`30b96bf6…`), as the launch note records.

| arm | alive at end | absorpt | largest clade · min last 6,000 s | D063 as amended | standing 20k → 30k | buried/influx t > 15,000 | mean height t > 10,000 | min alive to 6,000 | median parent age, plateau |
|---|---|---|---|---|---|---|---|---|---|
| r24-s1 | 3,921 | 416 | 413 · **4** (root born 18,374; ≥ 10 only from 25,600) | **fail** (stability) | 9,603 → 13,007 (+35%) | 0.43 | −0.6 m | 40 | 1,239 s |
| r24-s2 | 3,448 | 15 | 6 · 6 | **fail** (never ≥ 10) | 8,196 → 11,526 (+41%) | 0.44 | +0.7 m | 36 | 1,198 s |
| r24-s3 | 3,593 | 268 | 267 · 117 | pass | 8,936 → 12,212 (+37%) | 0.45 | +0.4 m | 40 | 1,390 s |
| r24-s4 | 3,388 | 217 | 208 · 121 | pass | 8,215 → 11,544 (+41%) | 0.44 | 0.0 m | 40 | 1,142 s |
| r24-s5 | 3,105 | 113 | 90 · 68 | pass | 7,611 → 10,672 (+40%) | 0.49 | +1.5 m | 34 | 1,790 s |
| round 18 (closed, 0.01) | ~1,800 | — | 48 / 41 / 24 / 127 minima | 4 of 5 | 6,000 flat | — | −12 to −15 m | — | 4,300–4,600 s |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | D063 as amended in ≥ 4 of 5 | **not met at the pre-registered bar: 3 of 5** (seeds 3, 4, 5). D063's own threshold is ≥ 3 of 5, which this meets exactly; the pre-registration asked for round 18's four and did not get it. Seed 1's 413-strong clade is a mutant born at t=18,374 that crossed ten only at 25,600 and fails the stability clause; seed 2's one line never reached ten |
| M2 | no runaway | **held** — 3,105 to 3,921 at 30,000 against a ceiling of 8,000 |
| M3 | the stock levels (≤ +30% over the last third) | **falsified in all five** — +35% to +41%, the same linear growth 0060 saw at 0.02 |
| M4 | the film was the step (mean height below −5 m) | **falsified in all five** — mean height −0.6 to **+1.5 m**: at the fine step the vent world's populations sit in the surface film and *above the waterline*, where the closed world sat at −12 to −15 m |
| M5 | burial ≥ 40% of the influx | **held** — 43–49% |
| M6 | founding survives the drain (≥ 40 to 6,000 s in ≥ 4 of 5) | held in three; 36 and 34 in seeds 2 and 5 |
| M7 | the queue stays weak (< 2,000 s) | **held** — 1,142 to 1,790 s |

Two facts from the instrument columns: `jointed` is 0 at the end of every arm, so the
movement columns read empty in seeds 1–4 and `food rig` alone in seed 5 (10.4 J/m³);
movement has not paid in the open world either, and the round that asks it is D075's
next. And the stomachs' share at the end runs 0.4% to 10.6% across seeds — the widest
spread the record holds, in a world where nothing but the seed differs.

## Verdict

**The open world holds the goal rule at its threshold and not at round 18's bar, and it
does two things the closed world did not: it keeps growing, and it lives at the surface.**

*The stock.* Every seed's standing matter rose 35–41% over the last third at the same
slope as before, because burial takes half the influx and the rest locks into a
population growing at the flow rate. Round 18's controls held 6,000 flat. There is no
equilibrium at 0.6/s with burial 0.01/s at either step; 0060's reading is confirmed and
the dose is too high. The correction is the owner's; the two candidates are burial 0.02
(keeps the flow the owner asked to see; doubles the drain at founding, which already
dipped to 34–36 in two seeds) and influx 0.3.

*The surface.* This is the round's new fact. At 0.02 the vent arms' film was read as the
fast step's known mode (0056). At 0.01 all five populations sit at −0.6 to +1.5 m, and
the closed world at the same step sits at −12 to −15 m. The difference between those
worlds is the vent, and reading the code gives the mechanism (an inference from the
code, not yet a measurement): the plume lifts bodies at 0.05 m/s in its patch
(`EVOSIM_CURRENT_ADVECT 1`), its vertical velocity is special-cased to zero at the
waterline so it does not push them *through*, but a body arriving at y = 0 with upward
momentum crosses on its own, and above the surface nothing acts on it — D050 zeroes a
buoyant body's net vertical force at y ≥ 0 rather than restoring it, and the return
flow's downward push is also zero at depth ≤ 0. So the surface is a ratchet: up in the
vent's patch, over the top, and never back. That is CLAUDE.md's "the world has no top"
hole, opened by a current for the first time; D050 asked that any new upward push get
the same question, and this one did not get it. The producers gain nothing there (light
is a constant above 0), the stomachs are as far from the wet deep as they can be, and
the stomachs' share ran 0.4–10.6% across seeds on nothing but the seed. Two world-rule
options for the owner: make the region above the waterline restoring (a body above y =
0 gets its gravity back until it is under), or stop the plume short of the surface so it
spreads under the film. Either is a per-step change and a butterfly; the record's replay
is unaffected only at the default.

*The goal rule.* Three seeds hold stable, connected absorptive clades of 90–267 through
the last two lifetimes, which is what D063 asks; two do not. The record notes the
pre-registration asked for four. The owner rules whether the open world in this dose
and shape is the reference world, or whether the surface fix and the dose correction
come first and the confirmation is re-run on the corrected world. The recommendation:
**the latter** — the surface fault is a hole in the world, not a property of the open
budget, and a confirmation on a world with a known hole confirms the hole.

Closed 2026-09-05. Arms `r24-s1` … `r24-s5`; uncensored.
