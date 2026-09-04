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

## Results

Four arms to budget, manifests `status ended`, `reason budget`; V1–V3 held (headers as
pre-registered, `floor` 0 from t=3,100, audit 0.0000% throughout, `mat locked` ≤ stock).
V4 held: under `reserve` the median parent age in the plateau is 672 s and 2,497 s
against the controls' 4,632 s and 4,318 s — the walk is not the age walk. The larger-stock
arms ran at ~2.5× real time against the reserve arms' ~7×.

| arm | rule | depth at end | alive | deaths | absorpt (share) | largest clade · root · min last 6,000 s | median parent age, plateau (share > 3,500 s) | `mat blk` mean/window t > 10,000 | `mat locked` at end | stomach surplus above gate, median · share solvent · children each |
|---|---|---|---|---|---|---|---|---|---|---|
| r21b-s2 | reserve | −15 m | 1,817 | 2,004 | 35 (1.9%) | 22 · mutant t=8,420 · 9 → unstable | 2,497 s (31%) | 195,000 | 5,626 | +218 J · 94% · 0.91 |
| r20q0-s2 (control) | age | −1.4 m | 1,801 | 1,778 | 135 (7.5%) | 120 · founder · 81 | 4,318 s (52%) | 257,000 | 5,476 | −95 J · 16% · 0.84 |
| r21b-s4 | reserve | −0.2 m | 1,762 | 1,975 | 114 (6.5%) | 64 · founder · 62 | 672 s (15%) | 170,000 | 5,386 | +15 J · 61% · 0.67 |
| r20q0-s4 (control) | age | −0.9 m | 1,774 | 2,764 | 234 (13.2%) | 227 · founder · 200 | 4,632 s (54%) | 211,000 | 5,497 | +6 J · 56% · 0.74 |
| r21a-s2 | age, stock 3/m³ | −0.2 m | 5,744 | 5,920 | 479 (8.3%) | 243 · mutant t=7,417 · 180 | 2,854 s (44%) | 741,000 | 17,474 of 18,000 | −11 J · 46% · 0.91 |
| r21a-s4 | age, stock 3/m³ | 0.0 m | 5,672 | 6,924 | 464 (8.2%) | 417 · mutant t=3,673 · 380 | 4,841 s (66%) | 680,000 | 17,487 of 18,000 | +17 J · 59% · 0.92 |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | B: a stable clade larger than the control's | **falsified in both** — 22 (unstable) against 120; 64 against 227 |
| M2 | B: the world's size unchanged | held — `alive` +1% and −1%, `mat locked` +3% and −2% |
| M3 | B: stomach net positive at t > 10,000, children per stomach above the control's | half — net positive in both (seed 2 barely: +0.003 to +0.04 W; seed 4 +0.07 to +0.10 W) and the stomachs far more solvent than the controls' (94% and 61% above their gate against 16% and 56%); children per stomach *not* higher (0.91 vs 0.84, 0.67 vs 0.74) |
| M4 | A: matter still binds at three times the stock | **held** — 97% locked in both; population 5,700; refusals three times the control's |
| M5 | A: the stomachs no better off than the control's by more than the wingspan | **falsified by the letter** — clades of 243 and 417 against 120 and 227 — and held in substance: the stomachs' share of the population is 8.3% and 8.2% against the controls' 7.5% and 13.2%; a world three times the size carries about twice the clade |

## Verdict

**Neither shape changes who the matter economy favours.**

*B, `reserve`.* Energy now buys matter — the stomachs are the most solvent bodies in
both worlds, holding a median of 218 J above their gate in seed 2 where the control's
stomachs held −95 — and they still do not outbreed the leaves. Children per stomach are
the same as under the age order. A leaf in a saturated light field holds a reserve of
the same order as a stomach's, so ranking by reserve ranks the stomachs among the leaves
rather than above them; and what the age queue gave the stomachs — priority for
*outliving* the leaves, which the leak lets them do — the reserve rule does not. The
pre-registration's second reading is the one that applies: the stomachs earn and bid and
lose, and the leaves' reserves are the reason. Not adopted.

*A, three times the stock.* Matter binds again at 97% locked; the population is three
times the size; the stomachs are three times as many at the same share; the age queue is
back at 44–66% of births to bodies past a lifetime; and the machine runs at a third of
the pace. The clades are larger because the world is, not because the stomachs are
better off. Not adopted.

What both say together, with 0055 and 0056: in a conserved-matter world the second
trophic level's share is set by the matter economy's arithmetic — what fraction of the
bodies the stock can build are stomachs — and not by how well the stomachs eat. Energy
income buys survival and nothing else. The owner's reading on the same evening is the
one that reaches past this: *matter, like energy, is not finite; there is a constant
influx of both.* The ocean's nutrient budget is a flow — rivers, weathering and dust in,
burial out — and productivity anywhere is set by the supply rate, not the standing
stock. This world has a stock and no exchange, which is why it runs to a cap and stops
selecting. The open budget is put to the owner as `fable-propose-open-matter-budget.md`.

One reading on the side. Three of the four largest clades this round are mutant-rooted
(born 3,673–8,420 s) and stable at 180–380 members in the large worlds: under the leak,
late invasion is routine wherever the world is big enough for the draw.

Closed 2026-09-04. Arms `r21b-s2`, `r21b-s4`, `r21a-s2`, `r21a-s4`; negative result on
merit, uncensored.
