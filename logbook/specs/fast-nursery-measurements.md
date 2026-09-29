# The fast nursery's measurements (D134)

The four measurements D134 asks for before design A runs (logbook/specs/fast-nursery-proposal.md §5),
and what came out of building it. The nursery is `src/Evosim.Nursery` on the branch `nursery`
(`scratch/wt-nursery`). Written on the night of 2026-09-29 and extended as each measurement lands.

## A fault in runs 1 and 2: the corpses were laid about the wrong centre

A tank's axis is at (R, R), not at the origin (`World.KeepInTheWater`). The nursery clamped each
corpse inside the glass about the origin, so most corpses were dragged away from the body and some
past the glass. In the 100 m² tank, with the same seed and body, the nearest corpse sat 4.8 to 6.4 m
from the body before the fix and 0.9 to 4.0 m after it (0.3 to 1.2 m across; the rest is height). So
the ancestors' 8 to 13 m from food in runs 1 and 2 was mostly this fault, and so, very likely, was
their one-in-twenty hit rate. Run 2 was left to finish, as the owner ruled, and measures nothing
about foraging. Fixed as `67e3961`; `World.PlaceCorpse` now refuses a place past a tank's glass.

## The episode configs

Made by the farm's own binding from `rounds/env-r52.ps1` (a 1 s run on one thread each, under
`scratch/nursery/runs/`): round 52's rules at 100 m² by 5 m, the bed flat, no reef, no islands, no
trickle, the matter budget scaled to the volume (8 units), founders and islands at 2.5 m, the limb
push 0.3 and the scent on.

| arm | change | configHash |
|---|---|---|
| nursery-small | the search | 756e6120 |
| nursery-small-still | `EVOSIM_CURRENT 0` | ba990f55 |
| nursery-small-nomix | the three mixings 0 | e9ee8c0b |
| nursery-cfg-s | D133's episode, 400 m² by 10 m | f3e51b49 |

A tank takes a current of 0 (Fable had guessed this was untested).

## Measurement 4: the bodies' intake

All ten pooled bodies (two parts, one joint, a mouth and no leaf) carry intake 1, with adult mouths of
0.05 to 0.27 m³. A mouth takes intake times its surface area per second, so a 4 J corpse inside its
reach (0.5 m plus the corpse's radius) is emptied in a step or two: the score is a count of corpses
reached, as Fable assumed.

## First look at an episode in the small tank

Body 0 (creature 5210), 300 s, 200 J laid as 39 corpses of 5.1 J: 1.04 s of wall on one thread
(0.82 s stepping), against Fable's guess of 0.45 s. The body lived the episode and ate none. Its root
swam between 1.3 and 4.5 m below the surface while the corpses settled on the bed near 5.3 m, so it
passed within 0.3 m across of one and never nearer than 0.9 m in all. Whether these bodies can reach
the bed is the question the hit-rate measurement answers.
