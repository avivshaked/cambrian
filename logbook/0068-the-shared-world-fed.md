# 0068 — The shared world, fed

**2026-09-06**  ·  the confirmation of D077's world, re-run at the fed dose · pre-registered before
launch, results appended after

The confirmation at influx 0.3 starved the world (0067). This round runs the same world at
twice the influx, which is the dose the diagnostic arm held on, over five seeds and 30,000 s
at the fine step. Fed at 0.6 the world keeps a breeding stomach clade in three seeds of
five, which is D063's own threshold and one short of this round's bar. The stock never
levelled: standing matter grew in all five seeds, and the world retains about two thirds of
what the vent delivers.

The owner's ruling on the plan, which was to re-run at 0.6 if the diagnostic held, was one
word.

> good

## Why this round exists

0067 ran the confirmation at influx 0.3 with stock 0.25/m³, the two doses 0065 had read
alone, and the combination starved the world. Seed 4 fell to one survivor, seed 5 to a
recovery without stomachs, and seeds 1 to 3 went through the same crash.

The free matter sat in the bottom layer, where the vent delivers it and the sink carries it.
The bodies sat in the return flow at 15 to 30 m and saw 0.1 units/m³.

The diagnostic `r26d-s4` ran the same world at 0.6/s with stock 0.25, for 20,000 s at 0.01,
and it held. There were 1,633 alive, a stable stomach clade of 195, no divergence, the top
and the floor holding, and the population at −13 m.

This round runs that world on five seeds, to the goal rule's length.

## The arms

The arms are `r27-s1` to `r27-s5`, at dt 0.01 for 30,000 s, on 0067's world with the influx
doubled.

| setting | value |
|---|---|
| space | shared, 4 × 10 × 10 m on a ring, 60 m deep, area 400 |
| boundaries | restoring surface, the real floor |
| influx | **0.6**/s at the vent's base |
| burial | 0.01/s |
| matter sink | 0.02 |
| starting stock | 0.25/m³ |
| conception | age order |
| exudation | 0.15 |
| ceiling | 8,000 |
| wall | 2,400 minutes |

The launcher is `rounds/launch-r26.ps1 -Influx 0.6 -Name r27-sN`, with
`-ExpectSimHash 1f5455f4851591d0`, the floor build unchanged. Seeds 4 and 5 go first on the
freed workers, and seeds 1 to 3 follow as 0067's arms end.

The controls are 0067's five seeds, which are the same world at 0.3, and the diagnostic
`r26d-s4`, which is this world on seed 4 to 20,000 s. Beside them sit 0061's five seeds in
the tiled open world.

## Validity checks

As 0067's V1 to V3, with `matter in 0.6/s at vent` in V1.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the goal rule holds**: D063 as amended in ≥ 4 of 5 seeds | `scripts/clade-score.ps1` |
| M2 | **the top and the floor hold**: `above` ≤ 0.5% of `alive` at every sample after 3,000; `below` = 0 | `above`, `below` |
| M3 | **the populations live in the water**: mean height over t > 10,000 below −5 m in every arm | `depth m` |
| M4 | **the stock's slope**: standing matter at 30,000 within 30% of its value at 20,000 in ≥ 4 of 5 (the diagnostic: +25% over 16,000–20,000; 0067's M4 asked 15% of a dose that starved) | `stats.jsonl` |
| M5 | **no crash**: `alive` never falls below half its running maximum after t=6,000 in ≥ 4 of 5 (0067: every seed crashed by two thirds or more) | `alive` |
| M6 | **the size is affordable**: `alive` at 30,000 between 1,500 and 5,000; no wall stop | `alive`, manifest `reason` |
| M7 | **founding survives**: `alive` ≥ 40 at every sample to 6,000 | `alive` |
| M8 | **contact is ordinary**: `contacts` per step between 100 and 5,000 over t > 10,000 | `contacts` |

## The two-sided readings

- If M1 holds, D077's world at this dose is the reference world. The movement round is
  pre-registered on it, and the predation proposal goes to the owner.

- If M1 fails, read the failing clause against 0067 and the diagnostic. A stomach line that
  dies in the first 6,000 s is 0067's shape, and it says the founding stock rather than the
  influx. Then 0.5/m³ is the next arm.

- If M4 fails upward, the world grows on 0.6 without levelling. Burial 0.02 is then the next
  arm, on this world, before adoption.

- If M5 fails while M1 holds, the crash is the vent world's founding transient and the clade
  survives it. That is reported rather than fixed.

- If M6 fails on the wall, the fed world is too big for the fine step. The vent's speed or
  the box's width is then the owner's call.

## Launch

Seeds 4 and 5 launched on 2026-09-06 at 15:38 local, on workers 5 and 6, with
`-ExpectSimHash` on each.

| what | value |
|---|---|
| commit | `a268311`, the floor build, unchanged since 0067 |
| hash | `simHash 1f5455f4851591d0` |
| both manifests | that hash, `gitDirty false`, `physicsDtSeconds 0.01`, `status running` |
| header | `matter in 0.6/s at vent`, `space shared 4x10x10 m, depth 60, wrap, bed`, `from 0.25/m3`, the rest as 0067's |

The other three followed as 0067's arms ended, on the same hash and the same checks. Seed 3
went at 18:30 on worker 4, seed 2 at 18:46 on worker 3, and seed 1 at 19:24 on worker 2.
Seed 1's header and manifest were verified at launch.

Five run concurrent, with a monitor over them. Results are appended below.

## Results

Read on 2026-09-07, with all five arms ended on budget.

Every arm ran before D078, at Unity's default 31 job worker threads, so each of them is one
realisation of its seed (0069).

The scorer is the 2026-09-06 script, which asks every connected clade. In all five the
passing or best clade was also the largest, so the older script would have read the same.

| arm | alive | stomachs | scored clade (root born) / min over last 6,000 s | D063 | height t > 10,000 | stock 20,000 → 30,000 | worst alive / running max after 6,000 | contacts per step, mean (min, max) | diverged | wall min |
|---|---|---|---|---|---|---|---|---|---|---|
| `r27-s1` | 3,711 | 177 | 176 (5,543) / 79 | **PASS** | −20.3 m | 9,013 → 13,221 (+47%) | 0.90 | 1,254 (35, 4,737) | 2 | 669 |
| `r27-s2` | 3,682 | 100 | 93 (5,664) / 38 | **PASS** | −20.0 m | 9,177 → 13,205 (+44%) | 0.98 | 1,333 (40, 5,059) | 1 | 707 |
| `r27-s3` | 3,181 | 20 | 16 (9,427) / 7 | FAIL, stability | −20.0 m | 7,625 → 11,701 (+53%) | 0.86 | 920 (14, 4,251) | 0 | 495 |
| `r27-s4` | 2,527 | 26 | 26 (27,343) / 0 | FAIL, stability | −18.8 m | 6,251 → 9,401 (+50%) | 0.21 at 9,700 | 817 (3, 4,134) | 0 | 379 |
| `r27-s5` | 3,452 | 356 | 334 (4,387) / 73 | **PASS** | −19.8 m | 8,275 → 12,408 (+50%) | 0.99 | 1,288 (29, 5,419) | 0 | 625 |

The validity checks held in every arm. The headers carried the fed dose and the box, the
floor was silent after 3,100 s, and the audit closed at every sample. Every manifest reads
`ended` on `budget` at the launched hash.

Seeds 1 and 2 each lost a body or two to a solver divergence, counted as `Diverged` deaths
and dumped. The entry reads them with that caveat.

| # | prediction | result |
|---|---|---|
| M1 | goal rule in ≥ 4 of 5 | **fails at the round's bar: 3 of 5.** D063's own wording asks 3 of 5, which this meets |
| M2 | top and floor hold | holds in four; seed 3 reached 0.57% above at one sample |
| M3 | populations in the water | holds: −18.8 to −20.3 m in every arm |
| M4 | stock within 30% | **fails upward in all five: +44% to +53%** |
| M5 | no crash | holds in four; seed 4 fell to 0.21 of its running maximum at 9,700 s |
| M6 | size affordable | holds: 2,527 to 3,711 alive, no wall stop, 379 to 707 wall minutes |
| M7 | founding survives | holds: 40 alive at every sample to 6,000 s in every arm |
| M8 | contact ordinary | holds on the mean, 817 to 1,333 pairs per step; single samples fell to 3 and rose to 5,419 |

## What the round says

Fed at 0.6, the shared world keeps a breeding stomach clade in three seeds of five.

The three that pass do so comfortably. Their clades run 93 to 334 members, with minima of 38
to 79 through the last two lifetimes, and all are rooted in the first 6,000 s.

The two that fail do so differently. Seed 4's founder-era clade died out by 24,000 s, and
the clade alive at the end was born at 27,343 s. That seed is the diagnostic's own world on
a different realisation, and the diagnostic had held a clade of 195. Seed 3 held a clade of
322 members over the run and finished with 16, dipping to 7.

Neither is 0067's shape, where the stomachs starved in the first lifetime. So the dose did
what the diagnostic promised, and the stock question is what remains.

The stock did not level. Over the last two lifetimes, standing matter rose at 295 to 419
units per 1,000 s in every seed. That is 49% to 70% of the 600 the vent delivers, and burial
carried the other 30% to 51%.

The pre-registered reading for M4 failing upward is burial 0.02 on this world before
adoption. D079, ruled while the round ran, sends the path back to round 18's closed world
instead. So that reading is recorded rather than run. This world is the fed screen, and the
open budget returns to the good world one change at a time (0070).

The number the owner's balance ruling will need is here: at this dose the world retains
about two thirds of what comes in.

Three smaller findings remain, and the first is the crowd. The population crowds the plume's
patches whatever the dose. Refusals for want of room ran at two to three times the births in
every seed, and the two patches beside the vent held 60% to 80% of the bodies.

Contact stayed ordinary at three thousand bodies. And the five arms cost 379 to 707 wall
minutes each, about a third of the budget.
