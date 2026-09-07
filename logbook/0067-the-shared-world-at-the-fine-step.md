# 0067 — The shared world at the fine step

**2026-09-06**  ·  the confirmation of D077's world · pre-registered before launch, results appended after

The lean dose does not feed the world. At influx 0.3 with a quarter of the starting stock,
the founders' stomach lines died in every seed by about t=6,000. The goal rule came out at
two seeds of five. The world's physics held throughout, with no divergence against the real
floor, no crowd and no wall stop. The lean world also showed something the fed one cannot: a
stomach lineage evolving at the vent's floor from nothing, three times in three seeds.

This was pre-registered before the floor build it depends on had landed
(`scratch/floor-spec.md`). The owner's ruling was short.

> love it. go ahead.

## Why this round exists

0065 screened the footprint world at the fast step, and it worked. The top holds, the
populations live in the water, and founding is safe. The stock levels at influx 0.3, stomach
clades were stable in five seeds of five, and the goal rule passed in four.

Two of its readings were taken alone: influx 0.3 at stock 1/m³, and stock 0.25/m³ at influx
0.6. The confirmation runs them together, on the fine step where replay lives, with the
placeholder floor replaced by a real sea bed. The three divergences 0065 saw were newborns
at that floor.

If this world holds D063, it is the reference world, and the movement round and the
predation proposal both run on it.

## The arms

The arms are `r26-s1` to `r26-s5`, at dt 0.01 for 30,000 s, on the D074 vent-shape open
world inside D077's box.

| setting | value |
|---|---|
| space | shared, 4 × 10 × 10 m on a ring, 60 m deep, area 400 |
| boundaries | restoring surface, the real floor |
| influx | **0.3**/s at the vent's base |
| burial | 0.01/s |
| matter sink | 0.02 |
| starting stock | **0.25/m³** |
| conception | age order |
| exudation | 0.15 |
| ceiling | 8,000 |
| wall | 2,400 minutes |

The wall is generous on purpose, because 0065's seed-2 worlds hit a 600-minute wall at the
fast step. The quarter-stock world ran there at 1.6 times real time, so about half real time
is expected here.

The launcher is `scratch/launch-r26.ps1`, with `-ExpectSimHash` from the floor build's own
manifest, five concurrent on workers 2 to 6.

The controls are 0061's five seeds, which are the same open world tiled with no top at 0.01.
Beside them sit 0065's `r25q-s2` and `r25h-s4`, the two doses read alone at 0.02.

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

- If M1 holds, D077's world is the reference world. The movement round (D075) is
  pre-registered on it, and the predation proposal is rewritten on it.

- If M1 fails at 3 of 5, read which clause and which seed against 0061's failures on the same
  seeds. A stability failure where 0061 passed says the crowd is the cause, and the vent's
  speed is the next ruling.

- If M4 fails upward, the stock grows at 0.3 in the quarter-stock world. Burial 0.02 is then
  the next arm, rather than a re-run.

- If M5 fails on the wall, the fine step cannot afford this world at this crowd. The vent's
  speed or the box's width is the owner's call, before anything else is confirmed here.

- If M2 fails, the floor or the surface leaks at 0.01. That is a build fault, fixed and
  re-run.

- If M7 fails, the conveyor's crowd is the steady state rather than the founding transient.
  It is then read as the world's carrying capacity and reported rather than fixed.

## Launch

Launched on 2026-09-06 at about 13:30 on workers 2 to 6, with `-ExpectSimHash` on each.

| what | value |
|---|---|
| commit | `a268311`, the floor build |
| hashes | `simHash 1f5455f4851591d0`, `coreHash 52eb6496…` |
| every manifest | that hash, `gitDirty false`, `physicsDtSeconds 0.01`, `status running` |
| header, all five | `dt=0.01`, `space shared 4x10x10 m, depth 60, wrap, bed` (the floor build's token), `surface restore 1`, `area 400` |
| header, matter | `matter in 0.3/s at vent, burial 0.01/s`, `from 0.25/m3`, the vent tokens |

A monitor runs over the five. Results are appended below.

## Seed 4 is dead, an interim note from the evening of 2026-09-06

The arm `r26-s4` ran to budget in nine hours because there was nothing left to simulate. It
held 389 alive at t=4,000, then took 400 starvation deaths in the next 2,000 s. It read 11
alive at 12,000 and one at 26,000, with no stomach after 12,000. The audit closed throughout
and nothing diverged. The world had starved out.

The matter side says why. There were 400,000 conceptions refused for matter by t=4,000, with
2,000 units free in the world. They were free in the bottom layer, where the vent delivers
them and the sink carries them, at `mat deep` 0.16 to 0.2 against `mat top` 0.06. The
population meanwhile sat at −15 to −31 m in the return flow, and saw 0.1 units/m³.

With the stock at a quarter and the influx at half, the world's arrivals were locked by
whoever sat on the vent's floor, and everyone else could not breed. Senescence did the rest.

Meanwhile `r26-s5` crashed from 182 to 58 by t=10,000, and is recovering at 409 with no
stomach. Seeds 1 to 3 are at t=4,600 to 7,100 with 1 to 5 stomachs, and the crash's shape
ahead of them.

The pre-registration read each dose alone, 0.3 at stock 1/m³ and 0.25/m³ at 0.6, and the
combination is what fails. M4's downward case was not written, and it is being read now.

The scored arms run to their budgets, since D069 says a futility stop is a result rather
than a censor. One diagnostic arm, not scored, launched on the freed worker. It is
`r26d-s4`, the same world at influx 0.6 with stock 0.25/m³, which is the combination
`r25q-s2` held 3,100 bodies on at the fast step. It runs 20,000 s at 0.01, to separate the
dose from the step before the next round is designed.

## Results

Five arms reached budget, with `divergedTotal` 0 in all five, which was the floor's whole
point. The audit read 0.0000% at every sample, `floor` was 0 from t=3,100, and the matter
identity closed to the unit. Checks V1 to V3 held.

The interim note above overstated one thing. Seeds 1 to 3 did not crash: their populations
never fell below 88% of their running maximum after t=6,000. Seed 5 fell to 42%, and seed 4
to nothing.

| arm | t last | alive | absorpt | largest clade · min last 6,000 s | D063 | mean height t > 10,000 | `above` max (% of alive) | standing 20k → 30k | `crowded` / births | contacts/step t > 10,000 |
|---|---|---|---|---|---|---|---|---|---|---|
| r26-s1 | 30,000 | 2,017 | 525 | 268 · 41 (root born 21,641) | pass | −20.2 m | 0.41% | +34% | 5,633 / 5,147 | 530 |
| r26-s2 | 30,000 | 1,772 | 155 | 71 · 19 (root born 15,867) | pass | −19.9 m | 0.29% | +36% | 4,684 / 4,805 | 449 |
| r26-s3 | 30,000 | 1,931 | 46 | 46 · **8** (root born 9,387; ≥ 10 only from 25,300) | **fail** (stability) | −20.4 m | 0.37% | +41% | 6,054 / 3,797 | 645 |
| r26-s4 | 30,000 | **1** | 0 | — | **fail** (dead) | −21 m | 0 | drained | 70 / 783 | ~0 |
| r26-s5 | 30,000 | 929 | 0 | — | **fail** (no stomach) | −19.4 m | 0.22% | +42% | 1,872 / 2,056 | 133 |
| r26d-s4 (diagnostic, influx 0.6) | 20,000 | 1,633 | 195 | 195 · 29 (root born 253) | pass (at 20,000) | −13.2 m | 0.20% | +25% (16k → 20k) | 7,510 / 3,204 | 690 |

Here is how the predictions came out.

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

The lean dose does not feed the world. At influx 0.3 with a quarter of the starting stock,
the founders' stomach lines died in every seed by about t=6,000.

The free matter sat in the bottom layer, where the vent delivers it and the sink carries it.
A body in the return flow at 20 m saw 0.1 units/m³ and could not breed.

What happened next depended on the seed. One world starved to nothing, and one crashed to 58
and came back as a producer lawn. Three carried on at 1,800 to 2,000 producers until a
mutant stomach line found the vent's floor. Those lines were born at t=15,867, 21,641 and
9,387, and grew to 71, 268 and 46. Two of those arrived in time to be stable, and the round
scores 2 of 5.

The world's physics held throughout. There was no divergence with the real floor, the top
and bottom were crossed by a fraction of a percent, and there was no crowd and no wall. The
diagnostic at influx 0.6, on the same seed that died, held 1,633 bodies and a founder-rooted
stomach clade of 195 through 20,000 s.

Two readings come out of that, and the first is a failure of mine. The pre-registration
combined two doses each of which had been read alone, on cost grounds, and the combination
is what starved. The two-sided readings did not write the downward case for M4.

The second is what the lean world says and the fed one cannot. A stomach lineage evolves at
the vent's floor from nothing, repeatedly: three times in three seeds here, and once more in
0065's `r25q-s2`. That answers D075's late-stomach question, the invasion assay, as a
by-product. Yes, and it takes 10,000 to 20,000 s.

The confirmation is re-run at influx 0.6 as logbook/0068, the dose the diagnostic held on.
M4 is loosened to the slope that world actually shows, and M5 asks that no seed crash.

Closed on 2026-09-06. The arms were r26-s1 to r26-s5, with r26d-s4 as a diagnostic and not
scored, and the round is uncensored.
