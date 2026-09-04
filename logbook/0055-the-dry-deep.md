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

Three arms once the contract-repairs build lands and the workers are refreshed to it;
the monitor's watch list carrying them; headers and manifests verified before any arm is
believed. Results appended below.
