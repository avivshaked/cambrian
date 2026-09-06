# 0068 — The shared world, fed

*2026-09-07. Pre-registered before launch. The confirmation of D077's world under D063 as
amended, re-run at the dose the diagnostic held on: dt 0.01, 30,000 s, five seeds, influx
0.6/s, starting stock 0.25/m³. Owner: "good" on the plan (re-run at 0.6 if the diagnostic
holds).*

## Why this round exists

0067 ran the confirmation at influx 0.3 and stock 0.25/m³, the two doses 0065 had read
alone, and the combination starved the world: seed 4 to one survivor, seed 5 to a
recovery without stomachs, seeds 1–3 through the same crash. The free matter sat in the
bottom layer where the vent delivers it and the sink carries it while the bodies sat in
the return flow at 15–30 m and saw 0.1 units/m³. The diagnostic `r26d-s4` (0.6/s, stock
0.25, 20,000 s at 0.01) held: 1,633 alive, a stable stomach clade of 195, no divergence,
the top and the floor holding, the population at −13 m. This round runs that world on
five seeds to the goal rule's length.

## The arms

`r27-s1` … `r27-s5`, dt **0.01**, 30,000 s: 0067's world with influx **0.6**/s — shared
space, 4 × 10 × 10 m on a ring, 60 m deep (area 400), restoring surface, the real floor,
influx at the vent's base, burial 0.01/s, matter sink 0.02, starting stock 0.25/m³, age
order, exudation 0.15, ceiling 8,000, wall 2,400 minutes. Launcher `scratch/launch-r26.ps1
-Influx 0.6 -Name r27-sN`; `-ExpectSimHash 1f5455f4851591d0` (the floor build, unchanged);
seeds 4 and 5 first on the freed workers, 1–3 as 0067's arms end. Controls: 0067's five
seeds (the same world at 0.3), `r26d-s4` (this world, seed 4, to 20,000 s), and 0061's
five (the tiled open world).

## Validity checks

As 0067's V1–V3, with `matter in 0.6/s at vent` in V1.

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

- **M1 holds:** D077's world at this dose is the reference world; the movement round is
  pre-registered on it and the predation proposal goes to the owner.
- **M1 fails:** read the failing clause against 0067 and the diagnostic; a stomach line
  that dies in the first 6,000 s (0067's shape) says the founding stock, not the influx,
  and 0.5/m³ is the next arm.
- **M4 fails upward:** the world grows on 0.6 without levelling; burial 0.02 is the next
  arm, on this world, before adoption.
- **M5 fails with M1 holding:** the crash is the vent world's founding transient and the
  clade survives it; reported, not fixed.
- **M6 fails on the wall:** the fed world is too big for the fine step; the vent's speed
  or the box's width, the owner's.

## Launch

Appended below.
