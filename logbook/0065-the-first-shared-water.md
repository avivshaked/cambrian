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

Appended below.
