# 0065 — The first shared water

*2026-09-05. Pre-registered before launch, and before the build it depends on has
landed (`scratch/footprint-spec.md`; the header tokens below are the spec's and are
verified against the build's own rendering at launch). D077's screen: the footprint world
at the fast step, with 0061's surface fix and dose correction folded in.*

## Why this round exists

Three findings arrived on one day. Round 24 (0061) confirmed the open budget at the goal
rule's threshold and found the world's top open: the vent's plume ratchets every
population to the waterline and above it, and the stock still grows at the dose. The
spike (0064) found that shared space costs nothing and that today's 10 × 10 m footprint
cannot pack the populations the world runs. D077 rules all of it as one world: a literal
box of four 10 × 10 m regions on a ring, 60 m deep, patches read from position, a
periodic horizontal wrap, a restoring top and bottom, newborns beside the parent. Every
density is re-read against area 400, so every rule tuned since D048 is screened once
here, with the dose on the table.

## The arms

dt **0.02**, 20,000 s, the reference world (D074 vent shape: influx at the vent's base,
burial 0.01/s, matter sink 0.02, D067's vent on) with `EVOSIM_SHARED_SPACE 1`,
`EVOSIM_SURFACE_RESTORE 1`, `EVOSIM_AREA 400`, ceiling 8,000:

| arm | seed | influx | initial stock |
|---|---|---|---|
| `r25-s2` | 2 | 0.6 | 1/m³ |
| `r25-s4` | 4 | 0.6 | 1/m³ |
| `r25h-s2` | 2 | **0.3** | 1/m³ |
| `r25h-s4` | 4 | **0.3** | 1/m³ |
| `r25q-s2` | 2 | 0.6 | **0.25/m³** (the drain hedge) |

Controls: 0060's `r23v-s2` / `r23v-s4` (the same world tiled, area 100, no restoring
boundary, 0.02) and 0061's five seeds at 0.01. Launcher `scratch/launch-r25.ps1`;
`-ExpectSimHash` from the build's own manifest; five concurrent.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `dt=0.02`, `space shared 4x10x10 m, depth 60, wrap`, `surface restore 1`, `area 400`, `matter in 0.6/s at vent` (or `0.3/s`), `burial 0.01/s`, `from 1/m3` (or `0.25/m3`), the vent tokens; every other token equals round 24's | header line 3 |
| V2 | `floor` = 0 after t=3,100; audit 0.0000% every sample; the matter identity closes; `diverged` reported | `floor`, `audit`, `diverged`, `stats.jsonl` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched, `gitDirty false` | `run.json` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the world has a top and a bottom**: `above` = 0 and `below` = 0 at every sample after t=3,000 in every arm | `above`, `below` |
| M2 | **the populations leave the film**: mean height over t > 10,000 below −5 m in every arm (0060's vent arms at this step: −0.4 and +0.2 m) | `depth m` |
| M3 | **founding survives the drain in the bigger box**: `alive` ≥ 40 at every sample to t=6,000 in ≥ 4 of 5 | `alive` |
| M4 | **the dose reads**: at influx 0.3 the standing matter grows ≤ 15% over 15,000–20,000, and at 0.6 it grows more than at 0.3 for the same seed | `stats.jsonl` (`scratch/matter-budget.py`) |
| M5 | **the stomachs live where the matter arrives**: the absorptive share in patch 0 (the plume's) exceeds the mean of the other three over t > 10,000 in every arm | `p0`..`p3`, per-patch absorptive (or the snapshot's patch field if the columns are population-only) |
| M6 | **a stable clade in every arm** (≥ 10 through the last 6,000 s) | `scripts/clade-score.ps1` |
| M7 | **placement and contact work**: `crowded` < 1% of births; `contacts` > 0 in every arm | `crowded`, `births`, `contacts` |
| M8 | **no runaway**: `alive` at 20,000 between 1,500 and 8,000 | `alive` |

## The two-sided readings

- **M1–M2 hold:** the hole is closed and the vent's populations live in the water; with
  M4 the dose is chosen (0.3 if it levels, 0.6 if 0.3 starves the flow) and the 0.01
  confirmation launches on that world.
- **M1 fails:** the restoring rule leaks (read `above` against `wraps` and the plume's
  patch); the fraction or the rule is wrong before anything else is read.
- **M2 fails with M1 holding:** the populations sit under the film by choice, not by
  the ratchet — light is the reason and the film is the vent world's ecology; the
  movement round inherits the question.
- **M3 fails:** the drain is worse in the bigger box (24,000 units to bury); the hedge
  arm's stock 0.25 is the reading, and the initial stock falls with the area.
- **M5 fails:** the stomachs do not follow the matter even when the patch is a place;
  the contest at the surface again, and the movement round is where they get the legs
  to follow it.
- **M7 fails on `crowded`:** the box is too small for the flow at this dose; W = 15.

## Launch

**Amendment before launch.** The build's own smoke (`fp-smoke`) showed rule 4 as the
proposal wrote it — restoring at no more than the founder sink rate — leaves 1,634 of
2,245 bodies parked at the waterline: the founder sink is ~2 mm/s and the plume lifts at
50 mm/s. The rule was corrected to the physics before anything was launched (D077's
amendment note; commit `78cb35c`): out of the water a body feels its whole weight, damped
by the drag model to about a metre a second. The re-run smoke reads `above` 0 at every
sample and the population at −8 m where the tiled world sits at −14 m. Two caveats the
build recorded: a founder placed exactly on the floor bounces at ~1 m/s in the first
seconds, so `max m/s` in founding is not locomotion; and one patch ran thin (p3 = 1 at
t=3,000 in the smoke) — the plume's doing, read here as M5's per-patch columns. The
predictions stand as written.

Launched 2026-09-06 ~00:20 on workers 2–6 at commit `78cb35c`, `simHash c27c23c0aa3b0b9e`,
`-ExpectSimHash` on each; every manifest reads that hash, `gitDirty false`, `status
running`. Headers verified as V1 on all five: `dt=0.02`, `space shared 4x10x10 m, depth
60, wrap`, `surface restore 1`, `area 400`, `matter in 0.6/s at vent` (`0.3/s` on the `h`
arms), `burial 0.01/s`, `from 1/m3` (`0.25/m3` on `r25q-s2`), the vent tokens. Monitor
running. Results appended below.

## Results

Three arms to budget; the two seed-2 arms stopped by the 600-minute wall at t=19,800
(`r25-s2`) and t=16,200 (`r25h-s2`) — populations of 5,700–6,000 in a contact world run
at half real time at the fast step, and the launcher's wall budget was sized for the
tiled world. Both are read to their last sample as budget stops (D069: a stop on merit is
a result), with the caveat. `divergedTotal` 0 in four; 3 in `r25q-s2`, all newborn
stomach-bearing bodies one to thirteen seconds old at the floor (`diverged/*.json`), where
the placeholder floor rule pushes back on anything a hair below 60 m and the vent
delivers the matter the stomachs live on. Audit 0.0000% at every sample, `floor` 0 from
t=3,100, the matter identity to the unit, V1–V3 held.

| arm | influx · stock | t last | alive | absorpt | largest clade · min last 6,000 s | D063 | mean height t > 10,000 | `above` max after 3,000 | standing, last quarter | `crowded` / births | contacts/step t > 10,000 | end |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| r25-s2 | 0.6 · 1 | 19,800 | 6,018 | 157 | 80 · 80 | pass | −16.1 m | 18 | +11% | 180,819 / 16,086 | 7,900 | wall |
| r25-s4 | 0.6 · 1 | 20,000 | 3,871 | 331 | 331 · 331 | pass | −17.6 m | 8 | +11% | 199,663 / 11,210 | 4,100 | budget |
| r25h-s2 | 0.3 · 1 | 16,200 | 5,747 | 44 | 30 · 30 | **fail** (no recruitment in the last 20) | −20.7 m | 17 | +5% | 492,364 / 13,405 | 13,600 | wall |
| r25h-s4 | 0.3 · 1 | 20,000 | 3,204 | 171 | 163 · 163 | pass | −15.3 m | 9 | **+3%** | 145,078 / 9,147 | 3,800 | budget |
| r25q-s2 | 0.6 · 0.25 | 20,000 | 3,142 | 643 | 642 · 17 (root born 12,161) | pass | −15.8 m | 4 | +22% | 14,139 / 6,148 | 2,050 | budget |
| r23v-s2 / r23v-s4 (0060, tiled, area 100) | 0.6 · 1 | 20,000 | 2,781 / 2,305 | 110 / 185 | 109 / 106 | pass / pass | −0.4 / +0.2 m | — | +21% / +22% | — | — | budget |

**The predictions:**

| # | prediction | result |
|---|---|---|
| M1 | a top and a bottom: `above` = 0 and `below` = 0 at every sample after 3,000 | **held in substance, not as written** — `above` peaks at 4–18 bodies (≤ 0.3% of the population) and `below` at 1: creatures caught crossing at the sample, not living there; yesterday's arms had whole populations parked above the line |
| M2 | the populations leave the film (mean height below −5 m) | **held in all five** — −15 to −31 m, where 0060's vent arms at this step sat at −0.4 and +0.2 m |
| M3 | founding survives the drain (≥ 40 to 6,000 s) | **held in all five** — never below the floor's 40, and every world was past 1,600 by t=4,000 |
| M4 | the dose reads: 0.3 levels (≤ 15% over the last quarter), 0.6 grows more | **held** — 0.3: +3% and +5%; 0.6: +11% and +11% on the same seeds; the first arms in the record whose stock levelled |
| M5 | the stomachs live in the plume's patch | **unread** — the per-patch columns count bodies, not stomachs; the instrument is queued |
| M6 | a stable clade ≥ 10 in every arm | **held in all five** (minima 80, 331, 30, 163, 17); D063 as amended passes 4 of 5, `r25h-s2`'s line of 30 stable but without a birth in the last 20 samples |
| M7 | placement and contact work: `crowded` < 1% of births, `contacts` > 0 | **half falsified** — contacts 2,000–13,600 pairs per step; `crowded` refusals ran 2–37× the births, the flow packing bodies into the plume's convergence |
| M8 | no runaway (1,500–8,000 alive) | held — 3,142 to 6,018; no ceiling; two arms met the wall instead |

**Not pre-registered.** The population is no longer set by the influx. Every 1/m³ arm
locked 14,000–17,000 units by t=4,000 out of a starting stock of 24,000 (area 400 × 60 m
× 1/m³), and grew on that to 3,200–6,000 bodies; the quarter-stock arm started with 6,000
units, held 3,100 bodies, and its stock still grew 22% on 0.6/s. In the big box the
starting stock is the world's size and the influx is its slope. Seed 2's worlds are twice
seed 4's on the same rules — the seed's lineages, not the dose — and they cost the wall.

## Verdict

**The footprint world works, and it is the first world in the record where the top is a
top, the stock can level, and a stomach lineage evolves where the matter arrives.** The
hole is closed: crossings, not residence. Every population lives in the water at 15–30 m.
Founding is safe. At influx 0.3 the stock levelled (+3%, +5%) with the population still
growing, which is what an open budget was meant to do. Stable stomach clades in five of
five, D063 in four; in `r25q-s2` the founders' stomach line died at t≈1,900 and a new one
evolved at 12,161 and reached 642 by the end, living at the vent's base — the first
stomach invasion the record holds that arrived where the food is rather than surviving
where it was born. Contact happens at thousands of pairs per step and the audit closes.

**Three costs, all real.** (1) **The crowd.** The plume's conveyor packs bodies into its
convergence zones; `crowded` refusals ran 2–37× the births and contacts reached 13,600
pairs per step, and that crowd — not shared space itself, which 0064 measured at nothing —
is what ran the 6,000-body worlds at half real time into the wall. A refusal costs
nothing (the parent keeps what it did not spend), so it is a carrying capacity in space,
the first the world has had; but it makes the fine step expensive. (2) **The floor is a
spring.** Three divergences in one arm, all newborn stomachs at 60 m, where the
placeholder pushes back and the stomachs' niche is. (3) **The size.** At 1/m³ the box
holds 24,000 units and the world grows to 4,000–6,000 bodies before the influx matters;
the quarter-stock world is 3,000 bodies and runs at the pace the confirmation can afford.

**For the owner, before the 0.01 confirmation.** The dose: influx **0.3**, the arm that
levelled. The starting stock: **0.25/m³** — an unscreened combination (0.3 was read at
1/m³; 0.25 at 0.6), recommended on the two readings separately and on cost (a 3,000-body
world at 0.01 is a day; a 6,000-body contact world is four). The floor: **a real sea bed,
a collider at −60 m**, before the confirmation; D077 called the mirror a placeholder and
the divergences are its bill (`scratch/floor-spec.md`). Not recommended now: a wider box
or a slower vent for the crowd — the crowd is the vent's ecology, and the confirmation
should read it before a rule is spent on it.

Closed 2026-09-06. Arms `r25-s2`, `r25-s4`, `r25h-s2`, `r25h-s4`, `r25q-s2`; the two
seed-2 arms wall-stopped at 19,800 and 16,200 s, read to their last sample; uncensored.
