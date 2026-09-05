# 0058 — The open budget

*2026-09-04. Pre-registered before launch. D074's screen: matter with an influx at the
surface and a burial at the floor, the world's size a flow, on the two seeds 0056
controlled, at two doses.*

## Why this round exists

Three screens on one day (0055–0057) established that a conserved matter stock locks
97% of itself in bodies, sets the population by arithmetic, and thereby sets every
solvent body's fecundity to the same value whatever it earns — so the energy economy
selects only for not starving. The owner's reading: matter, like energy, is not finite;
there is a constant influx of both. D074 gives matter the shape energy already has: in
at the top, out at the bottom, the standing stock an equilibrium of the two.

## The rules under test

`EVOSIM_MATTER_INFLUX` units/s spread over the surface layer of every patch;
`EVOSIM_MATTER_BURIAL` 0.01/s of each patch's floor-layer free matter removed. Two
doses of influx, 0.6/s (the dose that should hold about today's stock) and 1.2/s.
Everything else the reference world's: age order, stock 1/m³ at start, exudation 0.15,
sinks 0.002.

## The arms

Four, at dt 0.02, 20,000 s: `r22o-s2`, `r22o-s4` (influx 0.6); `r22o2-s2`, `r22o2-s4`
(influx 1.2). Controls: 0056's `r20q0-s2` and `r20q0-s4` (closed budget, same step).
Launcher `scratch/launch-r22.ps1`; workers refreshed to the build and launched with
`-ExpectSimHash`; up to five concurrent with the divergence replay.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `matter in 0.6/s at surface, burial 0.01/s` (or `1.2/s`), `conception age`, `from 1/m3`, `dt=0.02`, `exudation 0.15`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample | `floor`, `audit` |
| V3 | the matter identity closes: `initial + Σ mat in − Σ mat buried` equals `mat locked` + the free field to the rounding at every sample | `stats.jsonl` totals |
| V4 | manifests `status ended`, `reason budget`, `simHash` as launched; `diverged` 0 (or the count reported and the arm read with that caveat) | `run.json`, `diverged` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the stock finds an equilibrium**: at 0.6/s, `mat in − mat buried` per window falls below a fifth of `mat in` by t=15,000, and the standing matter (free + locked) sits within a factor of two of 6,000 at 20,000 s | `stats.jsonl` (`scratch/matter-profile.py`, extended) |
| M2 | **the column is wet**: `matterHere` (the free density at the population's depth) ≥ 0.3 units/m³ on average over t > 10,000 in every arm — the number 0055 asked for and did not get | `stats.jsonl` |
| M3 | **the queue weakens**: median parent age in the plateau < 2,000 s in every arm (controls 4,318 and 4,632 s) | `scratch/parent-age.py` |
| M4 | **the stomachs hold**: a connected absorptive clade ≥ 10, stable through the last 6,000 s, in every arm with a stomach population at t=10,000 — no claim that it beats the control's | `scripts/clade-score.ps1` |
| M5 | **the size is a flow**: `alive` at 20,000 s in the 1.2/s arms exceeds the 0.6/s arms' by more than the wingspan (±20%) for the same seed, and `mat locked` with it | `alive`, `mat locked` |
| M6 | **matter turns over**: `mat buried` summed over the run exceeds a quarter of the initial stock at 0.6/s — the bodies' matter is not sitting | `stats.jsonl` totals |

## The two-sided readings

- **M1–M2 hold:** the world has a matter cycle; whatever M4–M5 say, the rule goes to the
  owner for adoption and the dose is set from the equilibrium read.
- **M1 fails upward (the stock runs away):** burial is too weak against the influx at
  this floor stock — the floor holds less than the dose assumed; halve the influx or
  double the burial, one arm, before anything else.
- **M1 fails downward (the stock drains):** the floor holds more than assumed and burial
  outruns the influx; the reverse correction.
- **M2 fails with M1 holding:** the leaves take the influx at the surface before it
  sinks and the column below is as dry as before; the vent shape (D074's second
  screen) delivers it from below instead.
- **M3 fails:** arriving matter is still contested by the whole column at once; the
  queue is not about supply.
- **M5 fails:** the population is not set by the flow — something else caps it at ~1,800
  (light, or the ceiling's shadow); read `mat locked` against the standing stock.

## Launch

**Amendment before launch.** The build's 300-s validation arm read burial at ~4.5
units/s against an influx of 0.6 in a founding world, because the floor layer holds
several hundred free units before any body has locked them: at 0.01/s the stock drains
by ~4 units/s until the population grows, so the founding may run short of matter and
the first ten thousand seconds are the transient, not the equilibrium. The pre-registered
doses stand (M1's downward reading covers it), and one hedge arm is added: `r22ob-s2`,
influx 0.6 with burial **0.002**/s, so the round reads the equilibrium's dependence on
the outflow as well as the inflow. Five arms.

Launched 2026-09-04 ~22:30 on workers 2–6, refreshed to commit `1ce2e71` (the open
budget build, 508 tests) and launched with `-ExpectSimHash c43976d3d71f1f52`. Headers
and manifests verified (every one `simHash c43976d3…`, `gitCommit 1ce2e71`, `gitDirty
false`); monitor running.

## Results

Five arms to budget, manifests `status ended`, `reason budget`, `divergedTotal 0`; V1–V4
held (headers as pre-registered, `floor` 0 from t=3,100, audit 0.0000% throughout, the
matter identity to the rounding at every sample). Every population sat in the surface
film (0 to −3.6 m at the end) — the fast step's bimodality (0056), here in all five.

| arm | influx · burial | alive at end | deaths | absorpt (share) | standing matter at 20,000 (10,000) | buried/window t > 10,000 | `matterHere` mean t > 10,000 | largest clade · min last 6,000 s | median parent age, plateau (> 3,500 s) |
|---|---|---|---|---|---|---|---|---|---|
| r22o-s2 | 0.6 · 0.01 | 4,068 | 1,683 | 98 (2.4%) | 13,285 (8,581) | 13.0 of 60 | 0.125 | 72 · 18 stable | 5,260 s (66%) |
| r22o-s4 | 0.6 · 0.01 | 4,263 | 3,208 | 40 (0.9%) | 13,462 (8,028) | 5.7 of 60 | 0.056 | 18 · 4 unstable | 1,684 s (29%) |
| r22o2-s2 | 1.2 · 0.01 | 7,727 | 6,609 | 307 (4.0%) | 24,380 (13,534) | 11.6 of 120 | 0.103 | 142 · 140 stable | 2,572 s (33%) |
| r22o2-s4 | 1.2 · 0.01 | 6,932 | 4,384 | 375 (5.4%) | 23,102 (12,865) | 17.6 of 120 | 0.102 | 244 · 177 stable | 4,086 s (54%) |
| r22ob-s2 | 0.6 · 0.002 | 5,320 | 3,067 | 273 (5.1%) | 16,808 (11,024) | 2.2 of 60 | 0.071 | 162 · 158 stable | 1,504 s (30%) |
| r20q0-s2 (control) | closed | 1,801 | 1,778 | 135 (7.5%) | 6,000 | — | 0.137 | 120 · 81 | 4,318 s (52%) |
| r20q0-s4 (control) | closed | 1,774 | 2,764 | 234 (13.2%) | 6,000 | — | — | 227 · 200 | 4,632 s (54%) |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | the stock finds an equilibrium at 0.6/s | **falsified upward** — burial took a fifth or less of the influx at every dose and the stock grew linearly, 8,600 → 13,300 at 0.6/s, to 24,400 at 1.2/s; no equilibrium in sight |
| M2 | the column is wet (`matterHere` ≥ 0.3) | **falsified** — 0.06–0.13, the closed world's reading |
| M3 | the queue weakens (median parent age < 2,000 s) | mixed — 1,504 and 1,684 s in two arms, 2,572–5,260 in three |
| M4 | a stable clade ≥ 10 in every arm | held in four of five (72, 142, 244, 162); seed 4 at 0.6 has a mutant clade of 18, unstable |
| M5 | the size is a flow | **held** — doubling the influx raised the population by 90% and 63%, and `mat locked` with it (12,455 → 23,864; 13,047 → 21,875) |
| M6 | matter turns over (buried > 1,500 at 0.6/s) | held — 4,715 and 4,538 buried; the hedge arm's 1,192 is the weaker dose |

## Verdict

**When matter does not lock, the world grows at the influx rate, and nothing in the
world stops it.** The population is a flow — that much the owner asked to see and it is
seen: 4,000 bodies at 0.6/s, 7,000–7,700 at 1.2/s, against 1,800 closed, and the 1.2
arms were a few hundred seconds from the 8,000 ceiling. But the flow has no outflow to
balance it. Burial at the floor took 5–18 units per window against 60–120 arriving, at
every dose, because **the matter never reaches the floor**: it arrives at the surface,
the leaves at the surface lock it within a step, and what a death releases at −1 m
sinks at 0.002 m/s — eight hours of simulated time to the floor, longer than the run.
The free pool sat at 400–1,200 units whatever the influx (the closed world's ~600),
the column read as dry as before, and the queue held wherever the population was old.
Burial and the population are connected only by the matter sink, and at 0.002 m/s they
are not connected at all.

The stomachs' share **fell** in the open world — 0.9–5.4% against the closed world's
7.5–13.2% — because the influx lands where the leaves are. A surface source feeds the
first trophic level; it is the ocean's dust, not its upwelling.

**The dose correction, and the next screen.** The outflow has to see what the inflow
built. Two levers do that without a new rule: the matter sink at 0.02 m/s (D071's
value, harmless alone in 0055 because there was nothing free to sink — here there is a
dead body's matter to carry down in 3,000 s instead of 30,000), and the influx at the
vent's base (D074's second shape), where the plume lifts it through the deep before the
leaves see it. Round 23 (logbook/0060) screens both together and apart, at 0.6/s and
burial 0.01/s, seeds 2 and 4. The open budget is not adopted or rejected on this round:
its flow side is confirmed and its balance side is untested until the sink connects
them. Both remain the owner's ruling.

Closed 2026-09-05. Arms `r22o-s2`, `r22o-s4`, `r22o2-s2`, `r22o2-s4`, `r22ob-s2`;
uncensored.
