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

## Interim, 2026-09-06 evening: seed 4 is dead

`r26-s4` ran to budget in nine hours because there was nothing left to simulate: 389
alive at t=4,000, then 400 starvation deaths in the next 2,000 s, 11 alive at 12,000, one
at 26,000, no stomach after 12,000. The audit closed throughout and nothing diverged; the
world simply starved. The matter side says why: 400,000 conceptions refused for matter by
t=4,000 with 2,000 units free in the world — free in the bottom layer, where the vent
delivers it and the sink carries it (`mat deep` 0.16–0.2 against `mat top` 0.06), while
the population sat at −15 to −31 m in the return flow and saw 0.1 units/m³. With the
stock at a quarter and the influx at half, the world's arrivals were locked by whoever
sat on the vent's floor and everyone else could not breed; senescence did the rest.
`r26-s5` crashed from 182 to 58 by t=10,000 and is recovering at 409 with no stomach;
seeds 1–3 are at t=4,600–7,100 with 1–5 stomachs and the crash's shape ahead of them.
The pre-registration read each dose alone (0.3 at stock 1/m³; 0.25/m³ at 0.6) and the
combination is what fails; M4's downward case was not written and is being read now.

The scored arms run to their budgets (D069: a futility stop is a result, not a censor).
One **diagnostic** arm, not scored, launched on the freed worker: `r26d-s4`, the same
world at influx 0.6 with stock 0.25/m³ — the combination `r25q-s2` held 3,100 bodies on at
the fast step — for 20,000 s at 0.01, to separate the dose from the step before the next
round is designed.

## Results

Five arms to budget, `divergedTotal` 0 in all five (the floor's whole point, met), audit
0.0000% at every sample, `floor` 0 from t=3,100, the matter identity to the unit; V1–V3
held. The interim note above overstated one thing: seeds 1–3 did not crash. Their
populations never fell below 88% of their running maximum after t=6,000; seed 5 fell to
42% and seed 4 to nothing.

| arm | t last | alive | absorpt | largest clade · min last 6,000 s | D063 | mean height t > 10,000 | `above` max (% of alive) | standing 20k → 30k | `crowded` / births | contacts/step t > 10,000 |
|---|---|---|---|---|---|---|---|---|---|---|
| r26-s1 | 30,000 | 2,017 | 525 | 268 · 41 (root born 21,641) | pass | −20.2 m | 0.41% | +34% | 5,633 / 5,147 | 530 |
| r26-s2 | 30,000 | 1,772 | 155 | 71 · 19 (root born 15,867) | pass | −19.9 m | 0.29% | +36% | 4,684 / 4,805 | 449 |
| r26-s3 | 30,000 | 1,931 | 46 | 46 · **8** (root born 9,387; ≥ 10 only from 25,300) | **fail** (stability) | −20.4 m | 0.37% | +41% | 6,054 / 3,797 | 645 |
| r26-s4 | 30,000 | **1** | 0 | — | **fail** (dead) | −21 m | 0 | drained | 70 / 783 | ~0 |
| r26-s5 | 30,000 | 929 | 0 | — | **fail** (no stomach) | −19.4 m | 0.22% | +42% | 1,872 / 2,056 | 133 |
| r26d-s4 (diagnostic, influx 0.6) | 20,000 | 1,633 | 195 | 195 · 29 (root born 253) | pass (at 20,000) | −13.2 m | 0.20% | +25% (16k → 20k) | 7,510 / 3,204 | 690 |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | D063 in ≥ 4 of 5 | **falsified — 2 of 5.** Seed 4 starved to one survivor; seed 5 recovered from a crash without a stomach; seed 3's line reached ten too late to be stable. Both passes are late mutant clades that arrived after t=15,000 |
| M2 | the top and the floor hold | **held** — `above` ≤ 0.41% of alive at any sample; `below` 0 everywhere; no divergence |
| M3 | the populations live in the water | **held** — −19 to −21 m in all five |
| M4 | the stock levels (≤ +15%) | **falsified** — +34% to +42% in the four living worlds: the stock the drain had cut to ~4,500 was being rebuilt by the influx |
| M5 | 1,500–5,000 alive, no wall | held in three; 929 and 1 in the other two; no wall stop (a lean world is cheap) |
| M6 | founding survives | **held** — 40 at every sample to 6,000 in all five |
| M7 | the crowd falls below births by 15,000 | falsified as written in all five, but the crowd itself is gone: `crowded` per window ran 3–19 against thousands at the fast step — the lean world never packed |
| M8 | contacts 100–5,000 per step | held on the mean (133–645); the minimum dips to single digits when a population thins |

## Verdict

**The lean dose does not feed the world.** At influx 0.3 with a quarter of the starting
stock, the founders' stomach lines died in every seed by t≈6,000 — the free matter sat in
the bottom layer where the vent delivers it and the sink carries it, and a body in the
return flow at 20 m saw 0.1 units/m³ and could not breed. What happened next depended on
the seed: one world starved to nothing, one crashed to 58 and came back as a producer
lawn, and three carried on at 1,800–2,000 producers until a mutant stomach line found the
vent's floor — at t=15,867, 21,641 and 9,387 — and grew to 71, 268 and 46. Two of those
arrived in time to be stable and the round scores 2 of 5. The world's physics held
throughout: no divergence with the real floor, the top and bottom crossed by a fraction of
a percent, no crowd, no wall. The diagnostic at influx 0.6 on the same seed that died held
1,633 bodies and a founder-rooted stomach clade of 195 through 20,000 s.

**Two readings for the record.** First, the failure is mine: the pre-registration
combined two doses each of which had been read alone, on cost grounds, and the
combination is what starved; the two-sided readings did not write the downward case for
M4. Second, the lean world says something the fed one cannot: **a stomach lineage
evolves at the vent's floor from nothing, repeatedly** — three times in three seeds, and
once more in 0065's `r25q-s2` — which is the invasion assay (D075's "late stomach"
question) answered as a by-product: yes, and it takes 10,000–20,000 s.

The confirmation is re-run at influx 0.6 as logbook/0068, the dose the diagnostic held on,
with M4 loosened to the slope that world actually shows and M5 asking that no seed crash.

Closed 2026-09-07. Arms `r26-s1` … `r26-s5`, `r26d-s4` (diagnostic, not scored); uncensored.
