# 0070 — The good world with one change: contact

*2026-09-06. Pre-registered before launch, before D078's build has landed. The first round
under D079: round 18's closed world, the last one that met the goal rule, with one change
added and everything else held. Owner: "agreed. proceed with that idea."*

## In one paragraph

Round 18's world (logbook/0054) met the goal rule in four seeds of five, and it still
replays. Every world since stacked a change on it, and none met the rule as well. So we go
back to that world and add one thing: creatures share one box and can touch. If the rule
still holds, contact is free and the next change can come. If it fails, we will know which
change costs the rule, which no compound round could tell us. The round also runs with the
physics on one thread, so for the first time a shared world can be re-run and watched.

## What is held and what changes

Held: every setting of `scratch/launch-r18.ps1` (closed matter budget, vent off, starting
stock 1/m³, area 100 m², exudation 0.15, clearance 10, senescence 3,000 s, the floor
closing at 3,000 s, dt 0.01), 30,000 s, five seeds. Changed: `EVOSIM_SHARED_SPACE 1` and
`EVOSIM_SURFACE_RESTORE 1`, which together are D077's world — one box of four 5 × 5 m
patches on a ring, 60 m deep, periodic wrap, newborns placed beside the parent, a top that
gives a body above the waterline its weight back, and a real sea bed. The restoring top is
not strictly part of "contact", and it will matter: in round 18 seed 3's population sat at
+0.4 m, above the water, because that world had no top. D079 folds it into the package
because the box needs a lid, and this entry reads it separately where it can (M2, M3). Under
D078 the physics step runs single-threaded, recorded in the header as `physics jobs 0`.

## The arms

`r28-s1` … `r28-s5`, dt 0.01, 30,000 s, on workers 2–6 as round 27's arms end, launched with
`scratch/launch-r28.ps1 -ExpectSimHash <D078 build>`. Wall budget 1,200 minutes (round 18
ran 30,000 s in about six hours; single-threaded physics is expected to add a quarter at
this population). Controls: round 18's five seeds (`r18x-s1` … `r18x-s5`: alive at the end
1,834 / 1,722 / 1,818 / 1,828 / 1,490; inherited stomachs 4 / 91 / 103 / 76 / 221; mean
height −14.4 / −9.4 / +0.4 / −12.5 / −2.0 m). One replay probe: `r28p-s1`, seed 1 for 3,000 s
with the digest on, against `r28-s1`'s first 3,000 s.

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `dt=0.01`, `vent off`, `sink 0.002 m/s, matter 0.002 m/s`, `area 100`, `from 1/m3`, `exudation 0.15`, `clearance 10`, `space shared 4x5x5 m, depth 60, wrap, bed`, `surface restore 1`, `physics jobs 0`; every other token equals round 18's | header line 3 |
| V2 | `floor` 0 after t=3,100; audit 0.0000% at every sample; the matter identity closes | `floor`, `audit`, `stats.jsonl` |
| V3 | manifests `status ended`, `reason budget`, `simHash` as launched, `physicsJobWorkers 0`, `gitDirty false`; `diverged` 0 | `run.json`, `diverged` |

## Predictions

| # | prediction | falsified by |
|---|---|---|
| M1 | **the goal rule holds at round 18's bar**: D063 as amended in ≥ 4 of 5 seeds | `scripts/clade-score.ps1` |
| M2 | **the top and the floor hold**: `above` ≤ 0.5% of `alive` at every sample after 3,000; `below` 0 | `above`, `below` |
| M3 | **the world is round 18's world**: `alive` at 30,000 within 25% of the same seed's round-18 value in every arm (the butterfly's own spread, 0052) | `alive` |
| M4 | **the stomach lines persist**: inherited stomachs ≥ 10 at the last sample in ≥ 4 of 5 (round 18: four of five) | `absorptive inherited` |
| M5 | **no crowd**: `crowded` per window below the window's births at every sample after 15,000 in ≥ 4 of 5 (there is no plume to pack bodies) | `crowded`, `births` |
| M6 | **contact is ordinary**: `contacts` per step between 100 and 5,000 over t > 10,000 | `contacts` |
| M7 | **the round replays**: `r28p-s1` identical to `r28-s1` on every digest to 3,000 s | `scripts/digest-diff.py` |
| M8 | **founding survives**: `alive` ≥ 40 at every sample to 6,000 | `alive` |

## The two-sided readings

- **M1 holds:** contact costs the good world nothing. The next round adds the open matter
  budget in the vent shape (D074) to this world, and the movement round is pre-registered
  on whichever of the two holds.
- **M1 fails with M2–M6 holding:** contact itself costs the rule. Read which clause fails
  and in which seeds against round 18's same seeds; the width of the box (area is a light
  budget, so a wider box is a different world) goes to the owner.
- **M3 fails:** the box changes the ecology beyond contact — shading, depth, the lid. Read
  the height distribution against round 18's, seed by seed; if the seeds that lived at the
  surface in round 18 are the ones that moved, the lid is the cause and is read as such.
- **M5 fails:** bodies pack without a plume; the box at area 100 is too small for this
  population, and the owner rules on width.
- **M7 fails:** D078's build does not do what 0069 measured; nothing in this round is read
  until it does.

## Launch

*Appended at launch: build, hashes, times, headers verified.*
