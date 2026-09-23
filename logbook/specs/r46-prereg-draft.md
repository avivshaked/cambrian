# Round 46, the pre-registration draft: the angle, the price, the second founding

*Fable, 2026-09-23 midday, drafted before the rulings on `fable-propose-round-46.md` and
before the build, so that the thresholds are argued while no arm runs. It becomes the
logbook entry (0116) at launch, with the launch note and the header read after it; until
then the numbers in brackets are the ones the rulings can move. Round 45's entry
(logbook/0114) is the shape.*

## What it asks

Six rules land together (D110's light by exposure with the buoyancy offset; the consumer's
second founding; the support cost; per-part contact; the two repaired clauses; the beach),
and the round asks one thing of each: does the crowd use it. A leaf that can lie flat and
is paid for it; a consumer that arrives when there is food, over a floor that holds the
food in the light; a price that stops the fan; a contact that touches with the part; a
killer read on a window it can meet.

## The world

Round 45's (D108, D109: 22,000 m², 45 m, 15,000 units as islands, matter-mix 0.02, shade
off, the mouth's prices) with the five rules on at the ruled values: `light by exposure`,
`buoyancy offset [0.02 W/m3]`, `consumers found at [6000] s ([40] over [1000] s)`, `support
[0.1] W/m2/m2`, `contact per part`, the bed's `tilt [96] m shore [1] m` with the fade at
[15] m. Three seeds, 30,000 s at dt 0.01, five threads each,
the runaway ceiling 25,000, checkpoints every 2,500 s, the fields dumped with the snapshots,
poses recorded. The header is read after the launch and every token above is checked
against it before the queue is left to run.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| K1 | **the crowd lies down**: `expo` (1 = random, 2 = every leaf flat) above 1.3 at 30,000 s in 2 of 3, from a founding at 1.0 ± 0.1 | the column; `scripts/reads/tilt.py`'s flat factor as the check from the poses |
| K2 | **the offset is the way**: among living photosynthetic parts at 30,000 s, those with `|buoyancyOffset|` above 0.2 have a mean exposure factor above those without by 0.3 or more, in 2 of 3 | the snapshot's genomes against `PartExposure` in the absorptive log |
| K3 | **the second founding founds a line**: an inherited consumer birth (`ink` inherited) inside 5,000 s of the window's end, and `corpse eat` above zero in 20 windows before 30,000 s, in 2 of 3 | `lineage.jsonl` (`fnd 2`, `ink`), the column |
| K4 | **a killer line founds**: attack above zero with a kill in 20 windows inside the last 10,000 s, in 1 of 3 or more (J2 re-asked on the repaired window) | `lineage.jsonl` (`atk`), the `killed` column |
| K5 | **defence follows offence, read above a crowd floor**: with 500 alive or more, the first window with `prot %` above 1% comes after the first with `attack %` above 1%, in 3 of 3 with attack present | the two columns |
| K6 | **the price stops the fan**: `reach m` (the area-weighted mean part distance from the root) below 2 m at every sample after 5,000 s in 3 of 3, and no part farther than 6 m from its root in any snapshot | the column; the snapshots' `moduleCounts` and developed reach |
| K7 | **the crowd survives the price**: `alive` above 1,000 at 30,000 s in 3 of 3 (round 45 read 4,380 to 6,146) | the timeline |
| K8 | **the books close**: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at 30,000 s, `diverged` 0, in 3 of 3 (the offset's torque is the new term the divergence check watches) | `stats.jsonl`; the manifests |
| K9 | **contact is on the part**: `ovl/body` at 30,000 s under round 45's 0.01 in 3 of 3, and every kill row's part index equals the touching link of its overlap pair | the column; the kill rows against the overlap pairs |
| K10 | **the cost is small**: the exposure and the support terms together under 2% of the wall in the footer's `harness split`, in 3 of 3 | the footer |
| K11 | **the shelf holds the larder in the light**: at 10,000 s and after, the snow in the columns whose floor lies within 12 m of the surface reads above 0.4 J/m³ (the stomach's break-even) in its lowest live cell, in 3 of 3; and the consumer births of the second window's line, where K3 holds, lie over that shelf in 2 of 3 | `fields/*.snow-columns.f32` with `fields/bed.f32`; the births' positions against the floor under them |

## The two-sided readings

- **K1 fails with K8 holding:** the pay for lying flat did not move the crowd. Read `float
  off` first: if the offset never rose, the mutation rate or the price kept it out (a reading
  about the kit); if it rose and `expo` did not, the torque is not righting the leaves in this
  water (a reading about the physics, checked on one leaf alone).
- **K1 holds and K2 fails:** the crowd lay down another way (a float part, rising, a joint);
  the entry says which from the genomes of the flat bodies.
- **K3 fails with `fnd 2` rows present:** the second cohort starved with food present. Read
  their ages at death against the first cohort's (20 to 55 s): the same means the mouth's
  economy is short at this snow, and longer means they ate and did not breed.
- **K4 fails:** the killer of round 45 was that seed's; the two-sided reading stands.
- **K6 fails:** the price is too low or the copies earn past it; read `reach m` against the
  screen's `sqrt(10 / price)`.
- **K7 fails:** the price took the crowd; read `support W` against income before reading the
  mouth into it.
- **K9's second clause fails:** the mouth's part and the contact's part disagree, a fault
  and not a reading.

## What the round does not ask

Whether a leaf turns toward a sun it does not have (the light is straight down). Whether the
angle costs anything to hold (no price on the pose). Whether the reef would shade anything
(held for 47). Whether a body can live in air (the shoal is wet, and the terrestrial round is
later).
