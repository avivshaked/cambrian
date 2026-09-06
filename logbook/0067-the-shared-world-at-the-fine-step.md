# 0067 — The shared world at the fine step

*2026-09-06. Pre-registered before launch, before the floor build it depends on has
landed (`scratch/floor-spec.md`). The confirmation of D077's world under D063 as amended:
dt 0.01, 30,000 s, five seeds, at the dose and stock 0065 read. Owner: "love it. go
ahead."*

## Why this round exists

0065 screened the footprint world at the fast step and it worked: the top holds, the
populations live in the water, founding is safe, the stock levels at influx 0.3, stomach
clades stable in five of five and the goal rule in four. Two of its readings were taken
alone — influx 0.3 at stock 1/m³, stock 0.25/m³ at influx 0.6 — and the confirmation runs
them together, on the fine step where the record's replay lives, with the placeholder
floor replaced by a real sea bed (the three divergences 0065 saw were newborns at the
floor). If this world holds D063, it is the reference world, and the movement round and
the predation proposal run on it.

## The arms

`r26-s1` … `r26-s5`, dt **0.01**, 30,000 s: the D074 vent-shape open world in D077's
box — shared space, 4 × 10 × 10 m on a ring, 60 m deep (area 400), restoring surface,
the real floor, influx **0.3**/s at the vent's base, burial 0.01/s, matter sink 0.02,
starting stock **0.25/m³**, age order, exudation 0.15, ceiling 8,000, wall 2,400 minutes
(0065's seed-2 worlds hit a 600-minute wall at the fast step; the quarter-stock world ran
at 1.6× real there and is expected at ~0.5× here). Launcher `scratch/launch-r26.ps1`;
`-ExpectSimHash` from the floor build's own manifest; five concurrent on workers 2–6.
Controls: 0061's five seeds (the same open world, tiled, no top, 0.01) and 0065's
`r25q-s2` / `r25h-s4` (the two doses read alone, 0.02).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `dt=0.01`, `space shared 4x10x10 m, depth 60, wrap`, `surface restore 1`, `area 400`, `matter in 0.3/s at vent, burial 0.01/s`, `from 0.25/m3`, the vent tokens, and whatever token the floor build adds; every other token equals round 25's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample; the matter identity closes | `floor`, `audit`, `stats.jsonl` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched, `gitDirty false`; `diverged` 0 (the floor's whole point; any divergence is read with its dump) | `run.json`, `diverged` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the goal rule holds**: D063 as amended (all five clauses) in ≥ 4 of 5 seeds — the bar the closed world met (round 18) and the tiled open world did not (0061: 3 of 5) | `scripts/clade-score.ps1` |
| M2 | **the top and the floor hold at the fine step**: `above` ≤ 0.5% of `alive` at every sample after 3,000; `below` = 0 at every sample | `above`, `below`, `alive` |
| M3 | **the populations live in the water**: mean height over t > 10,000 below −5 m in every arm | `depth m` |
| M4 | **the stock levels together**: standing matter at 30,000 within 15% of its value at 20,000 in ≥ 4 of 5 | `stats.jsonl` |
| M5 | **the size is affordable**: `alive` at 30,000 between 1,500 and 5,000 in every arm; no wall stop | `alive`, manifest `reason` |
| M6 | **founding survives**: `alive` ≥ 40 at every sample to 6,000 in every arm | `alive` |
| M7 | **the crowd is bounded**: `crowded` per window falls below the window's births by t=15,000 in ≥ 4 of 5 (0065: 2–37× over the run) | `crowded`, `births` |
| M8 | **contact is ordinary**: `contacts` per step between 100 and 5,000 over t > 10,000 in every arm (0065: 2,000–13,600 at 0.02) | `contacts` |

## The two-sided readings

- **M1 holds:** D077's world is the reference world; the movement round (D075) is
  pre-registered on it and the predation proposal rewritten on it.
- **M1 fails at 3 of 5:** read which clause and which seed against 0061's failures on the
  same seeds; a stability failure where 0061 passed says the crowd is the cause and the
  vent's speed is the next ruling.
- **M4 fails upward:** the stock grows at 0.3 in the quarter-stock world; burial 0.02 is
  the next arm, not a re-run.
- **M5 fails on the wall:** the fine step cannot afford this world at this crowd; the
  vent's speed or the box's width, the owner's, before anything else is confirmed here.
- **M2 fails:** the floor or the surface leaks at 0.01 — a build fault, fixed and re-run.
- **M7 fails:** the conveyor's crowd is the steady state, not the founding transient; it
  is read as the world's carrying capacity and reported, not fixed.

## Launch

Launched 2026-09-06 ~13:30 on workers 2–6 at commit `a268311` (the floor build; `simHash
1f5455f4851591d0`, `coreHash 52eb6496…`), `-ExpectSimHash` on each; every manifest reads
that hash, `gitDirty false`, `physicsDtSeconds 0.01`, `status running`. Headers verified
as V1 on all five: `dt=0.01`, `space shared 4x10x10 m, depth 60, wrap, bed` (the floor
build's token), `surface restore 1`, `area 400`, `matter in 0.3/s at vent, burial 0.01/s`,
`from 0.25/m3`, the vent tokens. Monitor running. Results appended below.
