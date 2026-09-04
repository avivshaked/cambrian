# 0054 — The confirmation

*2026-09-04. Pre-registered before launch. D070's confirmation round: the leak at the
confirmation step, five seeds, scored under the goal rule as written.*

## Why this round exists

The screen (0053) passed on both seeds: with producers releasing 15% of their light
intake into the water, the world's stomachs held inherited lines of 46 and 133 at
20,000 s, never below ten after reaching it, while the control on the same build held
none. That was at the 0.02 screening step and for 20,000 s. D063 as amended scores a
full run — 30,000 s — and every run before 0052 was at 0.01, which stays the
confirmation step (0052's verdict). So this is the round that can pass: the first since
D063 was set in which the mechanism has already been seen to work.

## The arms

Five seeds, one arm each, dt 0.01, 30,000 s, round 14's clearance-10 world plus
`EVOSIM_EXUDATION 0.15`: `r18x-s1` … `r18x-s5` on w2–w7 as workers free (≤ 5 concurrent).
Launcher `scratch/launch-r18.ps1`. All workers refreshed to the build that carries the
exudation knob, the flux columns and the absorptive log (`absorptive.jsonl`), and
hash-checked. Seeds 1 and 2 are the screen's seeds at the finer step — different
realisations, per 0052, so the screen's numbers are a distribution to compare against,
not rows.

No sequential rule and no futility stop this time: the mechanism is established and the
question is the count, so all five run to budget. Wall clock: 30,000 s at 0.01 with five
arms sharing the machine is 5–6 h each.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | header carries `exudation 0.15`, `dt=0.01`, `clearance 10`, `sink 0.002 m/s, matter 0.002 m/s`, `vent off`; every other token equals round 14's | header line 3 |
| V2 | `floor` = 0 after t=3,100 | `floor` |
| V3 | audit 0.0000% every sample; `det in + det exuded − det out` summed equals `detritus J` to the rounding | `audit`, the flux columns |
| V4 | `absorptive.jsonl` is written and `abs logged` matches its row count per sample (the log's first scored round) | the run directory |

## Scoring

D063 as amended, per seed: producers persist to the end · an absorptive lineage is
inherited for ≥ 20 consecutive samples · it is still alive (≥ 10 individuals) at the last
sample · at least one absorptive birth within the last 20 samples (from `lineage.jsonl`).
A pass at cell-type mutation 0.005 is a discovery-regime pass and is labelled so. The
round passes if ≥ 3 of 5 seeds pass.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the round passes**: ≥ 3 of 5 seeds meet all four clauses | the scoring table |
| M2 | **the lines are founder-descended**, as in the screen: in every seed with a line, the majority of absorptive lineage roots are floor-era founders, not mutants | `lineage.jsonl` |
| M3 | **the leak is the income** in every seed: `det exuded` ≥ 10× `det in` at t > 10,000 | the flux columns |
| M4 | **the stomachs eat what lands**: `det out` within 30% of `det exuded` at t > 10,000, and the field at the population's depth below 2 J/m³ | the flux and field columns |
| M5 | **producers persist**: `alive` ≥ 1,000 at the end in every seed; no ceiling | `alive`, `**Ended:**` |
| M6 | **the absorptive log agrees with the ledger**: for the stomachs alive at t=20,000, mean `netW` from `absorptive.jsonl` is positive, and mean `densityHere` is within a factor of two of `J/m3 here` at that sample | `scripts/absorptive-log.ps1` |

## The two-sided readings

- **M1 holds:** the goal is met, in the discovery regime, and DESIGN.md takes exudation
  as a world rule with the review's fraction and caveats (D070). The frontier moves to
  0049's reading, one lever later: movement, in water that moves and feeds — and to the
  questions the pass leaves open: whether a *late* stomach can invade (the assay at 0.15,
  run two lifetimes past inoculation), and where the line's size stops tracking the leak
  (the bracket 0.05 / 0.13).
- **M1 fails with M3–M4 holding:** the leak is there and eaten and the lines still fall
  short at 0.01 — the finer step changes something the screen did not see (the film, the
  sink, contact). Read `absorptive.jsonl` first: it says where every stomach was and what
  it earned, which 0050's dissection could not.
- **M2 fails (mutant-rooted lines):** a stronger result than predicted — the leak lets a
  late stomach invade as well as keeping the founders' alive.
- **M5 fails:** the 15% tax binds at the finer step; the bracket downward.
- **M6 fails:** the log and the ledger disagree on what a stomach earns in this water —
  the instrument or the ledger has a term wrong, and that is settled before anything is
  concluded from either.

## Launch

Five arms, workers refreshed to the current build and hash-checked, the monitor's watch
list carrying them. Headers verified before any arm is believed. Results appended below.
