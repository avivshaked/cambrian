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

Launched 2026-09-05 ~02:15 on workers 2–5 at commit `1ce2e71` (the open-budget build;
no code change for this round), `-ExpectSimHash c43976d3d71f1f52`; every manifest reads
that `simHash`, `gitDirty false`, `status running`. Headers verified: `sink 0.002 m/s,
matter 0.02 m/s` on all four; `matter in 0.6/s at surface, burial 0.01/s` with `vent off`
on `r23s-s2`/`r23s-s4`; `matter in 0.6/s at vent, burial 0.01/s` with `vent 0.05 m/s in
patch 0 from 60 m, legs 1 m` on `r23v-s2`/`r23v-s4`. Monitor running. Results appended
below.

## Results

Four arms to budget, manifests `status ended`, `reason budget`, `divergedTotal 0`; V1–V3
held (headers as pre-registered, `floor` 0 from t=3,100, audit 0.0000% at every sample,
the matter identity to the unit at every sample: 6,000 + 12,000 in − buried = standing).

| arm | shape | alive at end | deaths | absorpt (share) | standing at 20,000 (15,000) | buried/window, t ≥ 15,000 (of 60) | `mat deep` mean t > 10,000 | `matterHere` mean t > 10,000 | depth at end | largest clade · min last 6,000 s | median parent age, plateau |
|---|---|---|---|---|---|---|---|---|---|---|---|
| r23s-s2 | surface, sink 0.02 | 3,493 | 3,380 | 127 (3.6%) | 11,813 (9,303) +27% | 9.8 | 0.15 | 0.107 | −14.8 m | 60 · 42 stable | 1,163 s |
| r23s-s4 | surface, sink 0.02 | 2,849 | 1,767 | 234 (8.2%) | 9,461 (6,946) +36% | 9.7 | 0.17 | 0.065 | −6.3 m | 234 · 29 stable | 1,110 s |
| r23v-s2 | vent, sink 0.02 | 2,781 | 1,609 | 110 (4.0%) | 9,662 (7,962) +21% | 26.0 | 0.58 | 0.070 | −0.4 m | 109 · 99 stable | 856 s |
| r23v-s4 | vent, sink 0.02 | 2,305 | 1,549 | 185 (8.0%) | 8,485 (6,978) +22% | 29.9 | 0.62 | 0.084 | 0.2 m | 106 · 82 stable | 580 s |
| r22o-s2 (0058) | surface, sink 0.002 | 4,068 | 1,683 | 98 (2.4%) | 13,285 | 13.0 | — | 0.125 | film | 72 · 18 | 5,260 s |
| r22o-s4 (0058) | surface, sink 0.002 | 4,263 | 3,208 | 40 (0.9%) | 13,462 | 5.7 | — | 0.056 | film | 18 · 4 | 1,684 s |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | burial ≥ half the influx by 15,000 in every arm | **falsified as written** — the surface arms buried a sixth (9.7–9.8 of 60), no better than 0058's 0.002 sink; the vent arms buried 43% and 50% (26 and 30), the second at half exactly and 2.5–3× the surface arms' |
| M2 | standing matter at 20,000 within 30% of 15,000 | held in three of four (+21%, +22%, +27%); r23s-s4 +36% |
| M3 | the vent wets the deep: `mat deep` ≥ 0.5 and `matterHere` ≥ 0.3 in the vent arms, neither in the surface arms | **half held** — `mat deep` 0.58 and 0.62 in the vent arms against 0.15–0.17 in the surface arms; `matterHere` 0.07–0.08 everywhere, because both vent populations ended in the surface film (0.2 and −0.4 m) where the leaves lock what arrives |
| M4 | the stomachs' share higher in each vent arm than its surface twin | **falsified** — 4.0% vs 3.6% on seed 2, 8.0% vs 8.2% on seed 4: no shift |
| M5 | a stable clade in every arm | **held** — minima 42, 29, 99, 82 over the last 6,000 s |
| M6 | `alive` between 1,800 and 4,300 | **held** — 2,305 to 3,493 |

**Not pre-registered, and the round's largest fact: the drain.** The sink at 0.02 m/s
carried the initial 6,000 units to the floor within 3,000 s, and burial at 1%/s of the
floor's free matter removed 75–117 units per window against 60 arriving until t≈6,000:
the free pool fell from 4,800 to 500 (surface) or 1,000–1,300 (vent) before any body could
lock it. Founding ran through that on the floor's forty and nearly failed in `r23s-s4`,
which fell to 21 alive at t=4,000, a thousand seconds after the floor closed, with no
stomach left; its absorptive line re-evolved from a leaf lineage at t=12,360 and is the
234-strong clade the scorer passes. 0058's "M1 fails downward" reading, arriving one
round late: the outflow this round connected is one that eats the starting stock first.

**The world is still growing, at the influx rate.** No arm reached a plateau. From
t≈6,000 every population rose linearly at 0.13–0.19 bodies per second, which is the
arithmetic of 0.6 units/s at about 3.5 units per body less what burial takes; deaths lag
a lifetime behind. Where it levels depends on mortality catching up, which 20,000 s did
not show. The queue weakened everywhere — median parent age in the plateau 580–1,163 s
against the closed world's 4,300–4,600 — because matter now arrives continuously rather
than being released by a death.

**The two vent populations rose into the film.** `r23v-s2` sat at −9 m at t=6,000 and
−0.4 m at the end; `r23v-s4` at −2.5 m and 0.2 m. The surface arms sat at −15 and −6 m.
Two readings, unresolved at this step: the fast step's bimodality (0056; all five of
0058's arms were in the film), or the plume itself, which advects bodies as well as
matter (`EVOSIM_CURRENT_ADVECT 1`) and lifts whatever sits over patch 0. The 0.01
confirmation separates them; a 0.02 result about depth is not a result (CLAUDE.md).

## Verdict

**The vent connects the outflow; the surface does not; and at this dose neither
balances.** Matter delivered at the vent's base sits in the deep at 0.6 units/m³ where
burial can reach it, and the vent arms buried 2.5–3× what the surface arms did with the
same sink, with the standing stock's growth slowed to +21% over the last quarter. Matter
delivered at the surface is locked by the leaves before it sinks, at 0.02 m/s exactly as
at 0.002: the sink was never the lever there. But every arm still grows at the influx
rate, so the stock has no equilibrium in 20,000 s at 0.6/s with burial 0.01/s, and the
world would reach the ceiling somewhere past 30,000 s.

**The stomachs did not gain from a wet deep, because they were not in it.** M4 is
falsified on both seeds with the deep three to four times wetter under the vent, and
D073's finding stands a third time: the contest at the surface, not the supply below,
sets the stomachs' share. What would let a stomach live where the matter is — a body
that can find it, and a reason to stay — is the movement round's question, and this
round's wet deep is the stage D075 asked for.

**What goes to the owner.** Adoption of the open budget in the vent shape (D074 as
corrected: influx at the vent's base, D067's vent on, matter sink 0.02, burial 0.01)
as the reference world's matter rule, and its dose. Three doses on the table: 0.6/s as
screened (the flow the owner asked to see; founding survived the drain in four of four,
narrowly in one); 0.6/s with burial 0.02/s (tightens the balance, doubles the drain);
0.3/s (halves both the growth and what the founding has). The recommendation is the
screened dose for the 0.01 confirmation, five seeds, 30,000 s, with the ceiling as the
censor and the drain read at the fine step; the dose is corrected on that reading, not
on this one. The surface shape is not recommended at any dose.

Closed 2026-09-05. Arms `r23s-s2`, `r23s-s4`, `r23v-s2`, `r23v-s4`; uncensored.
