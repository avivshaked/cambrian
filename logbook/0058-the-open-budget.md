# 0058 — The open budget

**2026-09-04**  ·  screen for D074 · pre-registered before launch, results appended after

Matter was given the shape energy already has, in at the top and out at the bottom. The
world's size became a flow, from 1,800 bodies closed to 7,700 at the higher dose. Nothing in
the world balanced it. Burial took a fifth or less of the influx at every dose. Matter
arriving at the surface is locked by the leaves within a step and never reaches the floor.

## Why this round exists

Three screens on one day, 0055 to 0057, established that a conserved matter stock locks 97%
of itself in bodies and sets the population by arithmetic. That in turn sets every solvent
body's fecundity to the same value whatever it earns, so the energy economy selects only for
not starving.

The owner's reading was this.

> matter, like energy, is not finite; there is a constant influx of both.

D074 gives matter the shape energy already has: in at the top, out at the bottom, with the
standing stock an equilibrium of the two.

## The rules under test

The inflow is `EVOSIM_MATTER_INFLUX`, in units per second, spread over the surface layer of
every patch. The outflow is `EVOSIM_MATTER_BURIAL`, which removes 0.01/s of each patch's
floor-layer free matter.

Two doses of influx are screened: 0.6/s, the dose that should hold about today's stock, and
1.2/s. Everything else is the reference world's, at age order, stock 1/m³ at start, exudation
0.15 and sinks 0.002.

## The arms

Four arms, at dt 0.02 for 20,000 s.

| arms | influx |
|---|---|
| `r22o-s2`, `r22o-s4` | 0.6/s |
| `r22o2-s2`, `r22o2-s4` | 1.2/s |

The controls are 0056's `r20q0-s2` and `r20q0-s4`, the closed budget at the same step.

The launcher is `scratch/launch-r22.ps1`. Workers were refreshed to the build and launched
with `-ExpectSimHash`, up to five concurrent alongside the divergence replay.

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

- If M1 and M2 hold, the world has a matter cycle. Whatever M4 and M5 say, the rule goes to
  the owner for adoption, and the dose is set from the equilibrium read.

- If M1 fails upward and the stock runs away, burial is too weak against the influx at this
  floor stock, because the floor holds less than the dose assumed. Halve the influx or
  double the burial, in one arm, before anything else.

- If M1 fails downward and the stock drains, the floor holds more than assumed and burial
  outruns the influx. The correction is the reverse one.

- If M2 fails while M1 holds, the leaves take the influx at the surface before it sinks and
  the column below is as dry as before. The vent shape, D074's second screen, delivers it
  from below instead.

- If M3 fails, arriving matter is still contested by the whole column at once, and the queue
  is about something other than supply.

- If M5 fails, the population is not set by the flow, and something else caps it at about
  1,800: light, or the ceiling's shadow. Read `mat locked` against the standing stock.

## Launch

One amendment came before launch. The build's 300-s validation arm read burial at about 4.5
units/s against an influx of 0.6 in a founding world. The floor layer holds several hundred
free units before any body has locked them. At 0.01/s the stock therefore drains by about 4
units per second until the population grows. The founding may run short of matter, and the
first ten thousand seconds are the transient rather than the equilibrium.

The pre-registered doses stand, since M1's downward reading covers this, and one hedge arm
is added. It is `r22ob-s2`, influx 0.6 with burial 0.002/s. That lets the round read the
equilibrium's dependence on the outflow as well as on the inflow, and it makes five arms in
all.

Launched on 2026-09-04 at about 22:30 on workers 2 to 6, refreshed to the open-budget build
at 508 tests.

| what | value |
|---|---|
| commit | `1ce2e71` |
| launch guard | `-ExpectSimHash c43976d3d71f1f52` |
| every manifest | `simHash c43976d3…`, `gitCommit 1ce2e71`, `gitDirty false` |

Headers and manifests were verified, and a monitor runs over the arms.

## Results

All five arms reached budget, and every manifest reads `status ended` with `reason budget`.

Each of them also reads `divergedTotal 0`.

Checks V1 to V4 held: headers as pre-registered, `floor` 0 from t=3,100, audit 0.0000%
throughout, and the matter identity closing to the rounding at every sample.

Every population sat in the surface film, between 0 and −3.6 m at the end. That is the fast
step's bimodality from 0056, here in all five arms.

| arm | influx · burial | alive at end | deaths | absorpt (share) | standing matter at 20,000 (10,000) | buried/window t > 10,000 | `matterHere` mean t > 10,000 | largest clade · min last 6,000 s | median parent age, plateau (> 3,500 s) |
|---|---|---|---|---|---|---|---|---|---|
| r22o-s2 | 0.6 · 0.01 | 4,068 | 1,683 | 98 (2.4%) | 13,285 (8,581) | 13.0 of 60 | 0.125 | 72 · 18 stable | 5,260 s (66%) |
| r22o-s4 | 0.6 · 0.01 | 4,263 | 3,208 | 40 (0.9%) | 13,462 (8,028) | 5.7 of 60 | 0.056 | 18 · 4 unstable | 1,684 s (29%) |
| r22o2-s2 | 1.2 · 0.01 | 7,727 | 6,609 | 307 (4.0%) | 24,380 (13,534) | 11.6 of 120 | 0.103 | 142 · 140 stable | 2,572 s (33%) |
| r22o2-s4 | 1.2 · 0.01 | 6,932 | 4,384 | 375 (5.4%) | 23,102 (12,865) | 17.6 of 120 | 0.102 | 244 · 177 stable | 4,086 s (54%) |
| r22ob-s2 | 0.6 · 0.002 | 5,320 | 3,067 | 273 (5.1%) | 16,808 (11,024) | 2.2 of 60 | 0.071 | 162 · 158 stable | 1,504 s (30%) |
| r20q0-s2 (control) | closed | 1,801 | 1,778 | 135 (7.5%) | 6,000 | — | 0.137 | 120 · 81 | 4,318 s (52%) |
| r20q0-s4 (control) | closed | 1,774 | 2,764 | 234 (13.2%) | 6,000 | — | — | 227 · 200 | 4,632 s (54%) |

Here is how the predictions came out.

| # | prediction | result |
|---|---|---|
| M1 | the stock finds an equilibrium at 0.6/s | **falsified upward** — burial took a fifth or less of the influx at every dose and the stock grew linearly, 8,600 → 13,300 at 0.6/s, to 24,400 at 1.2/s; no equilibrium in sight |
| M2 | the column is wet (`matterHere` ≥ 0.3) | **falsified** — 0.06–0.13, the closed world's reading |
| M3 | the queue weakens (median parent age < 2,000 s) | mixed — 1,504 and 1,684 s in two arms, 2,572–5,260 in three |
| M4 | a stable clade ≥ 10 in every arm | held in four of five (72, 142, 244, 162); seed 4 at 0.6 has a mutant clade of 18, unstable |
| M5 | the size is a flow | **held** — doubling the influx raised the population by 90% and 63%, and `mat locked` with it (12,455 → 23,864; 13,047 → 21,875) |
| M6 | matter turns over (buried > 1,500 at 0.6/s) | held — 4,715 and 4,538 buried; the hedge arm's 1,192 is the weaker dose |

## Verdict

When matter does not lock, the world grows at the influx rate, and nothing in the world
stops it.

The population is a flow, and that is the thing the owner asked to see. The numbers are
4,000 bodies at 0.6/s and 7,000 to 7,700 at 1.2/s, against 1,800 closed. The 1.2 arms were a
few hundred seconds from the 8,000 ceiling.

The flow has no outflow to balance it. Burial at the floor took 5 to 18 units per window
against 60 to 120 arriving, at every dose, because the matter never reaches the floor. It
arrives at the surface, the leaves at the surface lock it within a step, and what a death
releases at −1 m sinks at 0.002 m/s. That is eight hours of simulated time to the floor,
longer than the run.

So the free pool sat at 400 to 1,200 units whatever the influx, against the closed world's
600 or so. The column read as dry as before, and the queue held wherever the population was
old. Burial and the population are connected only by the matter sink, and at 0.002 m/s they
are not connected at all.

The stomachs' share fell in the open world, to between 0.9 and 5.4% against the closed
world's 7.5 to 13.2%. The influx lands where the leaves are. A surface source feeds the
first trophic level. It is the ocean's dust rather than its upwelling.

That gives the dose correction and the next screen. The outflow has to see what the inflow
built, and two levers do that without a new rule.

The first is the matter sink at 0.02 m/s, D071's value. It was harmless alone in 0055,
because there was nothing free to sink. Here there is a dead body's matter to carry down in
3,000 s instead of 30,000.

The second is the influx at the vent's base, D074's second shape, where the plume lifts it
through the deep before the leaves see it.

Round 23 (logbook/0060) screens both together and apart, at influx 0.6/s and burial 0.01/s,
on seeds 2 and 4. The open budget is neither adopted nor rejected on this round: its flow
side is confirmed and its balance side is untested until the sink connects them. Both remain
the owner's ruling.

Closed on 2026-09-05. The arms were r22o-s2, r22o-s4, r22o2-s2, r22o2-s4 and r22ob-s2, and
the round is uncensored.
