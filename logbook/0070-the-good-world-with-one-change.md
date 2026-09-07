# 0070 — The good world with one change: contact

**2026-09-06**  ·  the first round under D079 · pre-registered before launch

Round 18's world (logbook/0054) met the goal rule in four seeds of five, and it still
replays. Every world since stacked a change on it, and none met the rule as well.

So this round goes back to that world and adds one thing: creatures share one box and can
touch. If the rule still holds, contact is free and the next change can come. If it fails,
we will know which change costs the rule, which no compound round could tell us.

The round also runs with the physics on one thread, so for the first time a shared world can
be re-run and watched.

It is pre-registered before D078's build has landed. The owner's ruling was short.

> agreed. proceed with that idea.

## What is held and what changes

Everything in `scratch/launch-r18.ps1` is held. That is the closed matter budget, the vent
off, starting stock 1/m³, area 100 m², exudation 0.15 and clearance 10. It is also
senescence 3,000 s, the floor closing at 3,000 s, and dt 0.01, over 30,000 s and five seeds.

Two settings change, `EVOSIM_SHARED_SPACE 1` and `EVOSIM_SURFACE_RESTORE 1`, and together
they are D077's world. That is one box of four 5 × 5 m patches on a ring, 60 m deep, with a
periodic wrap and newborns placed beside the parent. It has a top that gives a body above
the waterline its weight back, and a real sea bed.

The restoring top is not strictly part of contact, and it will matter. In round 18 seed 3's
population sat at +0.4 m, above the water, because that world had no top. D079 folds the top
into the package because the box needs a lid, and this entry reads it separately where it
can, in M2 and M3.

Under D078 the physics step runs single-threaded, recorded in the header as
`physics jobs 0`.

## The arms

The arms are `r28-s1` to `r28-s5`, at dt 0.01 for 30,000 s, on workers 2 to 6 as round 27's
arms end.

They launch with `scratch/launch-r28.ps1 -ExpectSimHash <D078 build>`, one arm per worker.

The wall budget is 1,200 minutes. Round 18 ran 30,000 s in about six hours, and
single-threaded physics is expected to add a quarter at this population.

The controls are round 18's five seeds.

| control | alive at the end | inherited stomachs | mean height |
|---|---|---|---|
| `r18x-s1` | 1,834 | 4 | −14.4 m |
| `r18x-s2` | 1,722 | 91 | −9.4 m |
| `r18x-s3` | 1,818 | 103 | +0.4 m |
| `r18x-s4` | 1,828 | 76 | −12.5 m |
| `r18x-s5` | 1,490 | 221 | −2.0 m |

One replay probe runs beside them. It is `r28p-s1`, seed 1 for 10,000 s with the digest on,
read against the first 10,000 s of its arm.

Both of them carry `EVOSIM_DIGEST_EVERY 100`.

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
| M4 | **the stomach lines persist**: the scored clade holds ≥ 10 living members at the last sample in ≥ 4 of 5 (round 18: four of five) | `scripts/clade-score.ps1` |
| M5 | **no crowd**: `crowded` per window below the window's births at every sample after 15,000 in ≥ 4 of 5 (there is no plume to pack bodies) | `crowded`, `births` |
| M6 | **contact is ordinary**: `contacts` per step between 100 and 5,000 over t > 10,000 | `contacts` |
| M7 | **the round replays**: `r28p-s1` identical to `r28-s1` on every digest to 10,000 s | `scripts/digest-diff.py` |
| M8 | **founding survives**: `alive` ≥ 40 at every sample to 6,000 | `alive` |

## The two-sided readings

- If M1 holds, contact costs the good world nothing. The next round adds the open matter
  budget in the vent shape (D074) to this world, and the movement round is pre-registered on
  whichever of the two holds.

- If M1 fails while M2 to M6 hold, the package costs the rule. Read which clause fails and in
  which seeds, against round 18's same seeds. The first follow-up candidate is the same box
  with creature collisions switched off, which separates contact from the rest of the
  package. The width of the box goes to the owner, since area is a light budget and a wider
  box is a different world.

- If M3 fails, the box changes the ecology beyond contact, through shading, depth or the lid.
  Read the height distribution against round 18's, seed by seed. If the seeds that lived at
  the surface in round 18 are the ones that moved, the lid is the cause and is read as such.

- If M5 fails, bodies pack without a plume. The box at area 100 is then too small for this
  population, and the owner rules on width.

- If M7 fails, D078's build does not do what 0069 measured, and nothing in this round is read
  until it does.

## Amendments before launch

Three changes were made before any arm was launched, on the late evening of 2026-09-06. They
follow the Sol/GPT review of the same night (`sol-gpt-2026-09-06-220754-review.md`; my
response is in `scratch/`).

The replay probe grows from 3,000 s to 10,000 s. It starts on the free worker while `r28-s1`
runs, so the longer probe costs nothing, and `det6` (0069) had only reached 518 bodies.
Without the longer probe the full population's replay would go unmeasured. The probe and its
arm both take the digest at every hundred steps, which the launcher did not provide for
until tonight.

M4 reads the scored clade rather than the aggregate count, which is the loophole D063's
amendment closed.

The review is also right that the treatment is a package, as the section above says. The
reading for an M1 fail now names the collisions-off control as the first candidate.

The scorer changed tonight as well. It now evaluates every connected clade and passes a seed
when any one qualifies, rather than asking the clauses of the largest alone.

The producer clause will be read from a photosynthetic flag on lineage rows that the
round-28 build adds. Round 18's five seeds are re-scored under the new script before this
round is read, so both sit under one scorer.

## The control replays on this build

The review asked whether round 18's five seeds, recorded on a build from 2026-09-04, are
still the same world on the build this round runs. They are, at least for the first 3,000 s.

The arm `r18chk-s1` is seed 1 under the round-18 launcher on the round-28 build, with the
single-threaded physics and the lineage flag. It matches `r18x-s1`'s recording on all 30
samples to 3,000 s.

The tiled world never depended on the thread count, so this is the expected answer, and now
it is a measured one. The historical five stand as the control without a same-build control
group.

The check cost one file. PowerShell variable names are not case-sensitive, so the name
parameter I added to the launcher was the same variable as its internal one. The run
launched as `r18x-s1`, into the historical arm's directory, and overwrote its report file
before I saw it.

The run directory, with every sample and every lineage row, was never touched. The report
was rebuilt from that data by `scratch/rebuild-report.py`, calibrated on `r18x-s2`, where 6
cells in 14,700 differ by rounding. It says so in its first line, and it re-scores
identically.

The check's own run now lives under `r18chk-s1`. Its manifest still says `r18x-s1`, because
that is the name it was launched under.

## Launch

| what | value |
|---|---|
| commit | `95d2091` (D078 at `d2b59ac`, the lineage flag at `95d2091`) |
| simHash | `63fbf5f7ea60cb32…` |
| coreHash | `bff3d696…` |
| configHash | `841c4266cc314b6b` |

Every arm is launched with `-ExpectSimHash`, and its header carries every V1 token, checked
at launch.

The manifests read the tree as dirty, because the owner's other session has an uncommitted
edit to `scripts/style-check.py`. Nothing under the simulation's source is uncommitted, and
the fingerprints say so.

The arms start as round 27's workers free up, at most five arms on the machine at once, so
the round launches over several hours rather than at once.

| arm | worker | launched (local) | digest | note |
|---|---|---|---|---|
| `r28-s1` | 7 | 2026-09-06 23:49 | every 100 steps | first, on the worker the build freed |
| `r28-s2` | 6 | 2026-09-07 02:05 | off | on `r27-s5`'s worker, refreshed first |
| `r28-s3` | 4 | 2026-09-07 02:48 | off | on `r27-s3`'s worker, refreshed first |
| `r28-s4` | 3 | 2026-09-07 06:34 | off | on `r27-s2`'s worker, refreshed first |
| `r28-s5` | 2 | 2026-09-07 06:37 | off | on `r27-s1`'s worker, refreshed first; the probe `r28p-s1` waits for the first free worker |
