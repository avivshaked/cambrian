# 0055 — The dry deep

*2026-09-04. Pre-registered before launch. D071's screen: the matter sink decoupled from
the detritus sink, on the two matter-starved seeds of round 18, at the screening step.*

## Why this round exists

Round 18 (0054) met the goal 4 of 5 and its failing seed was not starving: its last
stomachs sat at −15 m in 13.7 J/m³, earning a positive net with four times a child's
price in reserve, and had no children. Refused, for want of matter at their layer. D071
does the arithmetic — 6,000 units of matter in the world, ~5,500 locked in bodies at
maturity, ~3.5 per child taken from the parent's own layer — and reads the 1,700–1,850
population plateau of every round since D065 as the matter cap, not the light. Round 13
slowed the matter sink to 0.002 m/s together with the detritus sink, so matter released
at the producers' layer is re-locked there before it reaches the stomachs: ~6,500 s to
fall 15 m instead of ~650.

The rule under test changes where the matter is, not how much there is: `EVOSIM_MATTER_SINK`
returns to 0.02 m/s (D048's default) while `EVOSIM_SINK` stays at 0.002. No code.

## The arms

Two seeds, one arm each: round 18's world with the matter sink at 0.02, dt 0.02,
20,000 s — `r19m-s1` and `r19m-s4`, the two seeds whose clades tracked the deep matter
most tightly in 0054. One control, `r19m0-s1`: round 18's sinks unchanged at dt 0.02,
because every round-18 row is at 0.01 and 0052 says a step change is a butterfly, so the
comparison is made inside one step. Launcher `scratch/launch-r19.ps1`. Workers refreshed
to the contract-repairs build (run manifest, `photo` columns, invariant culture, the step
in the config hash) and hash-checked; the manifest's `simHash` is the check from this
round on.

Baseline, from round 18 at 0.01 (`mat deep` in units/m³; `mat blk` is refused
conceptions per 100-s window):

| arm | t | alive | absorpt | mat top | mat deep | mat blk |
|---|---|---|---|---|---|---|
| r18x-s1 | 10,000 | 1,259 | 50 | 0.061 | 0.182 | 79,179 |
| r18x-s1 | 30,000 | 1,834 | 6 | 0.096 | 0.076 | 217,789 |
| r18x-s4 | 10,000 | 1,481 | 31 | 0.028 | 0.141 | 157,592 |
| r18x-s4 | 30,000 | 1,828 | 77 | 0.107 | 0.078 | 251,790 |

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `sink 0.002 m/s, matter 0.02 m/s` (control: `matter 0.002 m/s`), `dt=0.02`, `exudation 0.15`, `clearance 10`, `vent off`; every other token equals round 18's | header line 3 |
| V2 | `floor` = 0 after t=3,100 | `floor` |
| V3 | audit 0.0000% every sample; matter conserved: `mat locked` + the field totals constant to the rounding | `audit`, `mat locked` |
| V4 | `run.json` exists from the first minute with `status: "running"`, a real commit hash and a 64-hex `simHash`; at the end `status: "ended"`, `reason: "budget"` | the run directory |
| V5 | `photo` ≥ `alive − absorpt` at every sample (mixotrophs count in both) and `photo inh` ≥ 0.9 × `photo` after t=5,000 | the new columns |

## Scoring

D063 as amended, by connected clade (0054's addendum, `scratch/clade-score.py`): one
connected absorptive clade alive ≥ 20 consecutive samples to the end, ≥ 10 living
members at the last sample and through the last 6,000 s, an inherited absorptive birth
inside the clade in the last 20 samples; producers as an inherited photosynthetic
lineage from `photo inh`. A screen at 0.02 does not score the goal — it decides whether
the rule goes to confirmation.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the deep is wetted**: `mat deep` ≥ 0.3 units/m³ at every sample after t=10,000 in both treatment arms (round 18: 0.08–0.18) | `mat deep` |
| M2 | **refusals fall**: `mat blk` per window at t > 10,000 below half the control's at the same t | `mat blk` |
| M3 | **a clade holds**: a connected absorptive clade ≥ 10 at 20,000 s in both treatment arms, and larger than the control's at 20,000 s | the clade scorer |
| M4 | **the surface pays, but not the producers**: `mat top` falls below round 18's at t > 10,000, and `photo inh` stays ≥ 1,000 to the end with no ceiling | `mat top`, `photo inh`, `**Ended:**` |
| M5 | **the plateau moves**: `alive` at 20,000 s in the treatment arms exceeds the control's by more than 0052's wingspan (±20%), because matter that was locked at the top now builds bodies below | `alive` |

## The two-sided readings

- **M1–M3 hold:** the rule is adopted into the reference world (D071) and confirmed at
  0.01 under the amended goal before it is believed — a round 20, five seeds. The primer's
  chapter 06 keeps its dry-deep section as written.
- **M1 holds and M3 fails:** matter reaches the deep and the stomachs still do not breed;
  the refusal was not the only cause, and `absorptive.jsonl` says what the last stomachs
  earned and held. D071 is marked so, and the primer's section is struck.
- **M1 fails:** the matter does not arrive — re-locked on the way down by the producers
  between the surface and −15 m, which the arithmetic did not model. The lever is then
  matter excretion up (`EVOSIM_EXCRETION`, D071's second lever), or the vent as a source.
- **M4 fails on `photo inh`:** the faster sink strips the surface faster than the leaves
  recruit; the rule costs the producers, and the trade is the owner's to make.
- **M5 fails with M1–M3 holding:** the matter moved without adding bodies — the same
  plateau, differently placed; the cap is elsewhere than D071 says.

## Launch

Launched 2026-09-04 ~10:30 on workers 2, 3 and 4, refreshed to commit `5c6c035` (the
contract-repairs build) and launched with `-ExpectSimHash f0dd2b05f865de88`; every
manifest reads that `simHash`, `gitCommit 5c6c035`, `gitDirty false`, `status running`.
Headers verified: `dt=0.02`, `sink 0.002 m/s, matter 0.02 m/s` on `r19m-s1` and
`r19m-s4`, `matter 0.002 m/s` on the control `r19m0-s1`, `exudation 0.15`,
`clearance 10`, `vent off`. Monitor running over the three. Results appended below.

## Results

All three arms ran to budget at 6.5–7.8× real time; every manifest reads `status ended`,
`reason budget`. V1–V5 held: headers as pre-registered, `floor` 0 from t=3,100, audit
0.0000% on every sample, `photo inh` above 900 from t=10,000 in all three.

One thing the pre-registration did not anticipate: **seed 1's treatment arm never had a
stomach population.** Its nine founding stomachs died out by t=4,100 while the world was
still under forty bodies — the founding lottery at this realisation (0052: a step change
is a butterfly, and 0.02 is a different draw of seed 1 from the 0.01 record). M3 is
unreadable for that seed; its matter predictions still read. The control, the same seed
at the same step, held a founder-rooted clade of 46 at the end, in a population that sat
at −1 m — a surface-film world, where the treatment's sat at −15 m.

**The matter readings.** `mat deep` is the density at −54 m; the reading that matters is
`matterHere`, the density at the population's mean depth, which is in `stats.jsonl` and
not in the table (`scratch/matter-profile.py`). Units/m³; means over t ≥ 10,000:

| arm | matter sink | depth | `matterHere` mean · min · last | `mat deep` mean · min · last | `mat locked` at end | `mat blk` mean/window | alive at end |
|---|---|---|---|---|---|---|---|
| r19m-s1 | 0.02 | −15 m | 0.150 · 0.033 · 0.106 | 0.380 · 0.147 · 0.210 | 5,429 | 109,600 | 1,707 |
| r19m-s4 | 0.02 | −12.6 m | 0.101 · 0.076 · 0.093 | 0.246 · 0.151 · 0.225 | 5,383 | 90,300 | 1,659 |
| r19m0-s1 (control) | 0.002 | −1 m | 0.137 · 0.038 · 0.038 | 0.402 · 0.232 · 0.290 | 5,032 | 173,900 | 1,571 |
| r18x-s1 (0.01, for scale) | 0.002 | −14 m | 0.083 · 0.001 · 0.082 | 0.097 · 0.044 · 0.076 | — | — | 1,834 at 30,000 |
| r18x-s4 (0.01, for scale) | 0.002 | −12.5 m | 0.076 · 0.049 · 0.095 | 0.094 · 0.044 · 0.078 | — | — | 1,828 at 30,000 |

**Clades** (`scripts/clade-score.ps1`, all five clauses): r19m-s1 — no clade (a two-member
mutant line at the end); r19m-s4 — largest clade mutant-rooted (born t=10,967), 67 alive
at 20,000 s, 39 inherited births in the last 20 samples, but it reached ten only at
t=15,200, so its minimum over the last 6,000 s is 7 and the stability clause reads
unstable by the letter; a second clade of 40 beside it, aggregate 107. Control — founder-
rooted clade of 46, stable (minimum 30), 18 recent births.

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | `mat deep` ≥ 0.3 at every sample after 10,000 in both treatment arms | **falsified** — minima 0.147 and 0.151; the control's is 0.232. At the population's own depth the treatment reads 0.10–0.15, the control 0.14, round 18 0.08 |
| M2 | `mat blk` below half the control's | **not held** — 0.63× (s1) and 0.52× (s4) of the control's mean; s4 is at the boundary, s1 well short |
| M3 | a clade ≥ 10 in both, larger than the control's | s1 unreadable (no stomach population); s4 held — 67 against the control's 46, though not stable by the letter |
| M4 | `mat top` below round 18's, producers ≥ 1,000, no ceiling | held (0.017 and 0.017 at the end against 0.096 / 0.107; `photo inh` 1,699 and 1,546) |
| M5 | `alive` above the control's by more than the wingspan | **falsified** — +8.7% and +5.6%, inside ±20% |

## Verdict

**The rule does not do what D071 said it would, and the reason is in the `mat locked`
column.** At maturity 5,380–5,430 of the world's 6,000 units are in bodies — 90% — in the
treatment arms as in the control and in round 18. The free pool is the other 570–620
units, spread over 6,000 m³ of water: about 0.1 units/m³, which is what `matterHere` reads
in every arm at every step from 0.01 to 0.02 and at either sink speed. A sink speed moves
that 10% around the column; it cannot make it larger, and the treatment arms locked
*more* than the control (more bodies), leaving less free. The deep is not dry because
matter fails to reach it. **The whole column is dry**, and the stomachs sit in the same
band as the leaves (−12 to −15 m), so every unit of matter that arrives in their layer
is contested with a leaf that earns more — which is the refusal count, unchanged.

D071's arithmetic on the cap was right and its lever was wrong. The constraint is the
size of the free pool, and only a rule that changes it can move the stomachs' refusals:
the fixed matter price per body (`EVOSIM_MATTER_PER_CREATURE`, 3 units of the ~3.5 a
child costs), the initial stock (`EVOSIM_MATTER_INITIAL`), excretion of the tissue share
(`EVOSIM_EXCRETION`, at most ~15% of the locked pool under D065's contract), or an open
budget — the vent as a source paired with burial, the owner's deferred experiment. All
four are world rules; the choice is the owner's. What this screen adds to that choice: a
population plateau of ~1,800 at 90% locked means any lever that frees matter also grows
the producer population toward the ceiling and the machine's throughput, so the lever
and `EVOSIM_MAX_POP` have to be chosen together.

The rule is **not adopted**; `EVOSIM_MATTER_SINK` stays at 0.002 in the reference
world. D071 is marked so, and the primer's dry-deep paragraph on this rule is struck as
that chapter promised. One secondary reading stands on its own: the fast step's seed-4
world grew a *mutant-rooted* clade of 67 in 9,000 s under exudation — the second such
clade after 0054's seed 4 — so late invasion under the leak is not rare.

Closed 2026-09-04. Arms `r19m-s1`, `r19m-s4`, `r19m0-s1`; negative result on merit,
uncensored.
