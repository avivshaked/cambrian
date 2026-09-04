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

Appended below once the build lands.
