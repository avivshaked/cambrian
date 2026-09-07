# 0055 — The dry deep

**2026-09-04**  ·  screen for D071 · pre-registered before launch, results appended after

Round 18's one failing seed had stomachs that were not starving. They sat at −15 m with four
times a child's price in reserve, earned a positive net, and were refused conception for
want of matter at their layer. D071 read that as a transport problem. The cure this screen
tested was to return the matter sink to its old speed, so that matter released at the
surface reaches the deep. The cure did nothing, and the reason was arithmetic the proposal
had done only halfway.

## Why this round exists

Round 18 (0054) met the goal in 4 seeds of 5, and the failing seed was rich rather than
poor. Its last stomachs sat at −15 m in 13.7 J/m³ and had no children.

D071 does the arithmetic. There are 6,000 units of matter in the world. About 5,500 of them
are locked in bodies at maturity, and a child takes about 3.5 from the parent's own layer.
It reads the population plateau of 1,700–1,850 in every round since D065 as a matter cap
rather than a light cap.

Round 13 slowed the matter sink to 0.002 m/s together with the detritus sink. Matter
released at the producers' layer is therefore re-locked there before it reaches the
stomachs, taking about 6,500 s to fall 15 m instead of about 650.

The rule under test changes where the matter is rather than how much of it there is.
`EVOSIM_MATTER_SINK` returns to 0.02 m/s, which is D048's default, while `EVOSIM_SINK` stays
at 0.002. No code was changed.

## The arms

Two seeds get one arm each: round 18's world with the matter sink at 0.02, at dt 0.02, for
20,000 s. They are `r19m-s1` and `r19m-s4`, the two seeds whose clades tracked the deep
matter most tightly in 0054.

One control, `r19m0-s1`, runs round 18's sinks unchanged at dt 0.02. Every round-18 row is
at 0.01, and 0052 says a step change is a butterfly, so the comparison is made inside one
step.

The launcher is `scratch/launch-r19.ps1`.

Workers were refreshed to the contract-repairs build and hash-checked. That build carries
the run manifest, the `photo` columns, invariant culture and the step in the config hash.
From this round on, the manifest's `simHash` is the check.

Round 18's baseline at 0.01 follows. `mat deep` is in units per cubic metre, and `mat blk`
counts refused conceptions per 100-s window.

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

Scoring is D063 as amended, by connected clade, following 0054's addendum and
`scratch/clade-score.py`. One connected absorptive clade must be alive for at least 20
consecutive samples to the end. It must hold at least 10 living members at the last sample
and through the last 6,000 s. Inside the clade there must be an inherited absorptive birth
in the last 20 samples. Producers are read as an inherited photosynthetic lineage from
`photo inh`.

A screen at 0.02 does not score the goal. It decides whether the rule goes to confirmation.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the deep is wetted**: `mat deep` ≥ 0.3 units/m³ at every sample after t=10,000 in both treatment arms (round 18: 0.08–0.18) | `mat deep` |
| M2 | **refusals fall**: `mat blk` per window at t > 10,000 below half the control's at the same t | `mat blk` |
| M3 | **a clade holds**: a connected absorptive clade ≥ 10 at 20,000 s in both treatment arms, and larger than the control's at 20,000 s | the clade scorer |
| M4 | **the surface pays, but not the producers**: `mat top` falls below round 18's at t > 10,000, and `photo inh` stays ≥ 1,000 to the end with no ceiling | `mat top`, `photo inh`, `**Ended:**` |
| M5 | **the plateau moves**: `alive` at 20,000 s in the treatment arms exceeds the control's by more than 0052's wingspan (±20%), because matter that was locked at the top now builds bodies below | `alive` |

## The two-sided readings

- If M1 to M3 hold, the rule is adopted into the reference world as D071. It is then
  confirmed at 0.01 under the amended goal, over five seeds in a round 20, before it is
  believed. The primer's chapter 06 keeps its dry-deep section as written.

- If M1 holds and M3 fails, matter reaches the deep and the stomachs still do not breed. The
  refusal was one cause among others, and `absorptive.jsonl` says what the last stomachs
  earned and held. D071 is marked so, and the primer's section is struck.

- If M1 fails, the matter does not arrive. It was re-locked on the way down by the producers
  between the surface and −15 m, which the arithmetic did not model. The lever is then
  matter excretion upward, D071's second lever, through `EVOSIM_EXCRETION`, or the vent as a
  source.

- If M4 fails on `photo inh`, the faster sink strips the surface faster than the leaves
  recruit. The rule costs the producers, and the trade is the owner's to make.

- If M5 fails while M1 to M3 hold, the matter moved without adding bodies. The plateau is the
  same one, differently placed, and the cap is elsewhere than D071 says.

## Launch

Launched on 2026-09-04 at about 10:30 on workers 2, 3 and 4, refreshed to the
contract-repairs build and hash-checked at launch.

| what | value |
|---|---|
| commit | `5c6c035` |
| launch guard | `-ExpectSimHash f0dd2b05f865de88` |
| every manifest | that `simHash`, `gitCommit 5c6c035`, `gitDirty false`, `status running` |
| header, treatment | `dt=0.02`, `sink 0.002 m/s, matter 0.02 m/s` on `r19m-s1` and `r19m-s4` |
| header, control | `matter 0.002 m/s` on `r19m0-s1` |
| header, both | `exudation 0.15`, `clearance 10`, `vent off` |

A monitor runs over the three arms. Results are appended below.

## Results

All three arms ran to budget at 6.5 to 7.8 times real time, and every manifest reads
`status ended` with `reason budget`.

The validity checks V1 to V5 held. Headers were as pre-registered, `floor` was 0 from
t=3,100, and the audit read 0.0000% on every sample. The column `photo inh` stayed above 900
from t=10,000 in all three arms.

The pre-registration did not anticipate one thing. Seed 1's treatment arm never had a
stomach population at all. Its nine founding stomachs died out by t=4,100 while the world
was still under forty bodies. That is the founding lottery at this realisation. A step
change is a butterfly, 0052 says, and 0.02 is a different draw of seed 1 from the 0.01
record.

Prediction M3 is therefore unreadable for that seed, though its matter predictions still
read. The control is the same seed at the same step, and it held a founder-rooted clade of
46 at the end, in a population that sat at −1 m. The treatment's population sat at −15 m, so
the control was a surface-film world.

The table's `mat deep` column is the density at −54 m. The reading that carries the round is
`matterHere`, the density at the population's own mean depth.

That column lives in `stats.jsonl` rather than in the report table, and
`scratch/matter-profile.py` extracts it. Densities below are in units per cubic metre, and
the means are over t ≥ 10,000.

| arm | matter sink | depth | `matterHere` mean · min · last | `mat deep` mean · min · last | `mat locked` at end | `mat blk` mean/window | alive at end |
|---|---|---|---|---|---|---|---|
| r19m-s1 | 0.02 | −15 m | 0.150 · 0.033 · 0.106 | 0.380 · 0.147 · 0.210 | 5,429 | 109,600 | 1,707 |
| r19m-s4 | 0.02 | −12.6 m | 0.101 · 0.076 · 0.093 | 0.246 · 0.151 · 0.225 | 5,383 | 90,300 | 1,659 |
| r19m0-s1 (control) | 0.002 | −1 m | 0.137 · 0.038 · 0.038 | 0.402 · 0.232 · 0.290 | 5,032 | 173,900 | 1,571 |
| r18x-s1 (0.01, for scale) | 0.002 | −14 m | 0.083 · 0.001 · 0.082 | 0.097 · 0.044 · 0.076 | — | — | 1,834 at 30,000 |
| r18x-s4 (0.01, for scale) | 0.002 | −12.5 m | 0.076 · 0.049 · 0.095 | 0.094 · 0.044 · 0.078 | — | — | 1,828 at 30,000 |

The clade scorer `scripts/clade-score.ps1` was run over all five clauses. Seed 1's treatment
arm has no clade, only a two-member mutant line at the end.

Seed 4's largest clade is mutant-rooted, born at t=10,967, with 67 alive at 20,000 s and 39
inherited births in the last 20 samples. It reached ten only at t=15,200, so its minimum
over the last 6,000 s is 7, and the stability clause reads unstable by the letter. A second
clade of 40 sits beside it, for an aggregate of 107.

The control's clade is founder-rooted at 46 members, stable at a minimum of 30, with 18
recent births.

| # | prediction | result |
|---|---|---|
| M1 | `mat deep` ≥ 0.3 at every sample after 10,000 in both treatment arms | **falsified** — minima 0.147 and 0.151; the control's is 0.232. At the population's own depth the treatment reads 0.10–0.15, the control 0.14, round 18 0.08 |
| M2 | `mat blk` below half the control's | **not held** — 0.63× (s1) and 0.52× (s4) of the control's mean; s4 is at the boundary, s1 well short |
| M3 | a clade ≥ 10 in both, larger than the control's | s1 unreadable (no stomach population); s4 held — 67 against the control's 46, though not stable by the letter |
| M4 | `mat top` below round 18's, producers ≥ 1,000, no ceiling | held (0.017 and 0.017 at the end against 0.096 / 0.107; `photo inh` 1,699 and 1,546) |
| M5 | `alive` above the control's by more than the wingspan | **falsified** — +8.7% and +5.6%, inside ±20% |

## Verdict

The rule does not do what D071 said it would, and the reason is in the `mat locked` column.
At maturity, between 5,380 and 5,430 of the world's 6,000 units are in bodies, or 90%. That
holds in the treatment arms as in the control, and as in round 18.

The free pool is the other 570 to 620 units, spread over 6,000 m³ of water. That is about
0.1 units/m³. The column `matterHere` reads that figure in every arm, at every step from
0.01 to 0.02, and at either sink speed.

A sink speed moves that tenth around the column. It cannot make the tenth larger, and the
treatment arms locked more than the control did, because they carried more bodies, leaving
less free.

The deep is not dry because matter fails to reach it. The whole column is dry. The stomachs
sit in the same band as the leaves, between −12 and −15 m. Every unit of matter arriving in
their layer is contested with a leaf that earns more. That contest is the refusal count,
unchanged.

D071's arithmetic on the cap was right and its lever was wrong. The constraint is the size
of the free pool, and only a rule that changes that pool can move the stomachs' refusals.
There are four candidates.

| lever | what it changes |
|---|---|
| `EVOSIM_MATTER_PER_CREATURE` | the fixed matter price per body, 3 units of the ~3.5 a child costs |
| `EVOSIM_MATTER_INITIAL` | the initial stock |
| `EVOSIM_EXCRETION` | excretion of the tissue share, at most ~15% of the locked pool under D065's contract |
| an open budget | the vent as a source paired with burial, the owner's deferred experiment |

All four are world rules, so the choice is the owner's. What this screen adds to that choice
is a throughput warning. A population plateau of about 1,800 at 90% locked means that any
lever which frees matter also grows the producer population toward the ceiling and the
machine's limit. The lever and `EVOSIM_MAX_POP` have to be chosen together.

The rule is not adopted, and `EVOSIM_MATTER_SINK` stays at 0.002 in the reference world.
D071 is marked so, and the primer's dry-deep paragraph on this rule is struck as that
chapter promised.

One secondary reading stands on its own. The fast step's seed-4 world grew a mutant-rooted
clade of 67 in 9,000 s under exudation. That is the second such clade after 0054's seed 4,
so late invasion under the leak is not rare.

Closed on 2026-09-04. The arms were r19m-s1, r19m-s4 and r19m0-s1, and the result is
negative on merit and uncensored.
