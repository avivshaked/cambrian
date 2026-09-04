# 0057 — Energy buys matter

*2026-09-04. Pre-registered before launch. D073's screen: scarce matter allocated to the
parent with the largest energy reserve, against a stock large enough that light might
bind first, on the two seeds 0056 controlled.*

## Why this round exists

0055 found the free matter pool at a tenth of the stock; 0056 found the breeding walk
oldest-first and, on removing it, found that the stomachs had depended on it — because
in a matter-bound plateau every solvent body has the same fecundity whatever it earns,
and the age queue was the only route from an energy advantage to a child. The energy
economy was selecting only for not starving. D073 rules the repair to test: energy buys
matter.

## The rules under test

- **B, `reserve`** (`EVOSIM_CONCEPTION_ORDER reserve`): each step the solvent parents are
  walked in descending order of energy surplus above the breeding gate, ties by list
  index; the parent with the most to spare takes the layer's matter. Deterministic.
- **A, the larger stock** (`EVOSIM_MATTER_INITIAL 3`, age order): three times the matter.
  If the population plateaus below the new cap with free matter present, light binds
  first and the energy economy decides who breeds as designed; if it plateaus at the
  cap, matter still binds and the world is merely larger.

## The arms

Reference world (round 18's: exudation 0.15, sinks 0.002, clearance 10) at dt 0.02,
20,000 s. Four arms: `r21b-s2`, `r21b-s4` (reserve); `r21a-s2`, `r21a-s4` (stock 3,
age). Controls: 0056's `r20q0-s2` and `r20q0-s4` (age, stock 1, same step). Launcher
`scratch/launch-r21.ps1`; workers refreshed to the build and launched with
`-ExpectSimHash`; four concurrent. The A arms are expected to run 2–3× slower.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | B headers carry `conception reserve` and `from 1/m3`; A headers `conception age` and `from 3/m3`; all `dt=0.02`, `exudation 0.15`, `sink 0.002 m/s, matter 0.002 m/s`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample; matter conserved (`mat locked` ≤ stock) | `floor`, `audit`, `mat locked` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched | `run.json` |
| V4 | under `reserve`, the median parent age in the plateau is not the controls' 4,300–4,600 s (the walk is not the age walk) | `scratch/parent-age.py` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **B: the stomachs win the contest**: in each `reserve` arm with a stomach population at t=10,000, a connected absorptive clade ≥ 10 at 20,000 s, stable through the last 6,000 s, and *larger than the same seed's 0056 control clade* (120 and 227) | `scripts/clade-score.ps1` |
| M2 | **B: the world's size is unchanged**: `alive` at 20,000 s within the wingspan (±20%) of the control's; `mat locked` within 10% | `alive`, `mat locked` |
| M3 | **B: energy selects again**: the mean stomach `netW` from `absorptive.jsonl` at t > 10,000 is positive in both arms (0056's shuffled seed 2 read −0.04 to −0.10 W while its line aged out) and stomach `children` per living stomach exceeds the control's | `scripts/absorptive-log.ps1` |
| M4 | **A: matter still binds at three times the stock**: `mat locked` reaches ≥ 80% of 18,000 units by 20,000 s and `mat blk` per window is in the control's range or above; the population plateaus at 4,500–5,800 | `mat locked`, `mat blk`, `alive` |
| M5 | **A: the stomachs are no better off**: the largest clade at 20,000 s is not larger than the control's by more than the wingspan | the clade scorer |

## The two-sided readings

- **M1–M3 hold:** energy buys matter and the second trophic level breeds on its income;
  `reserve` goes to the owner for adoption into the reference world, then a confirmation
  at 0.01, five seeds, under the amended goal.
- **M1 fails with M3 holding:** the stomachs earn and bid and still lose — the leaves'
  reserves are larger than a stomach's; read the bid distribution from `absorptive.jsonl`
  (energy against the gate) before concluding anything.
- **M2 fails downward:** the reserve rule concentrates fecundity in few bodies and the
  plateau falls; a weighting rather than a sort is the next shape.
- **M4 fails (A plateaus below the cap):** light binds first at 3/m³ — the null the
  design assumed exists, at three times the stock and a population near 5,000; then the
  question is whether the stomachs breed there (M5), and throughput.
- **M5 fails (A's stomachs flourish):** the pool's size was the constraint after all,
  and 0055's reading is reversed for the larger world.

## Launch

Launched 2026-09-04 ~18:40 on workers 2–5, refreshed to commit `dff2d59` (the reserve
build, 491 tests) and launched with `-ExpectSimHash 3f3111cff9e23033`; every manifest
reads that `simHash`, `gitCommit dff2d59`, `gitDirty false`, `status running`. Headers
verified: `conception reserve` and `from 1/m3` on `r21b-s2` and `r21b-s4`; `conception
age` and `from 3/m3` on `r21a-s2` and `r21a-s4`; all `dt=0.02`, `exudation 0.15`,
`sink 0.002 m/s, matter 0.002 m/s`. Monitor running. Results appended below.
