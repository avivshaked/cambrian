# r64t's read: the true-shape screen

The screen is pre-registered in `logbook/specs/r64t-prereg/predictions.md`. It ran D's grazer world (`rounds/env-r64-d.ps1`)
under true-shape contact on seeds 6202 and 6203, and under sphere contact on 6203, as one card group of three from
`scratch/wt-r64t` at `f412e4ea`. Each world ran 24,000 s, in `runs/r64t-{t-6202,t-6203,s-6203}/2026-10-31-235754-*`. All
three reached their budget. The manifests carry config `2991f19e83d0a446` for true shape and `6ef3732d4dcec48b` for
spheres, with farm `96f89cef`, Dynamics `7076166a` and Core `2fa8f1cd`. The two configs differ only in `contactShape`.
The spheres world on 6202 is the recorded `runs/r64-d-6202/2026-10-30-202411-11de0613`, on the build before the smell
landing.

- `contact.txt` is `scripts/reads/contact-share.py`, each true-shape world `--against` the same seed's spheres world.
- `g2.txt` is `scripts/reads/bloom/g2-read.py --survive-from 12000 --upkeep-w 0.0091` over all four worlds.
- `deaths.txt` is `scripts/reads/bloom/grazer-deaths.py` and the head of `cannibal.py`, one world at a time. Both scripts
  pool every file they are given, so a call with four worlds mixes their ids.
- `income.txt` is `scripts/reads/bloom/r63g-motion/income.py` per world.
- `rest.txt` is `income-split.py`, `overshoot-read.py` per seed and `pace-calc.py`.
- `floaters.txt` is `scripts/reads/shape/r63s-floaters.py`.

## What it says

| Readout | 6202, true over spheres | 6203, true over spheres | Predicted |
|---|---|---|---|
| Mouth blows per grazer-second, 6,000 to 24,000 s | 0.47 | 0.67 | 0.3 to 0.6 |
| Intake per grazer | 0.75 | 0.71 | about the contact share |
| Grazers, mean of the last window | 1.23 (1,540 against 1,255) | 2.16 (2,351 against 1,087) | 0.2 to 0.6 |
| Grazers killed by a mouth, share of grazer deaths | 0.015 against 0.067 | 0.009 against 0.091 | at most spheres' |
| Starved, share of grazer deaths | 0.56 against 0.60 | 0.56 against 0.58 | above spheres' |
| Leaf share of income, last window | 0.75 against 0.59 | 0.80 against 0.62 | 0.45 to 0.65 |
| Leaves standing at 24,000 s | 1.94 | 2.63 | 1.2 to 2 |
| Pace | 1.10, across groups | 0.87 | 0.80 to 0.98 |
| Floaters | 0 against 12 | 0 against 4 | at most spheres' |

- **A mouth that must touch bites about half as often, as predicted.** The contact share fell to 0.47 and 0.67 of the
  spheres worlds'. Neither is near the falsifiers, 0.2 below and 0.8 above, so the balls did limit bites, and nothing
  beyond geometry shows.
- **Grazers did not fall. They rose.** Both true-shape worlds ended with more grazers than their spheres worlds, which the
  pre-registration names as a result that shows its prediction wrong. Both held the floor of 30 in every window from
  12,000 s, by the mean, and neither reached 0. The fewest in a window dipped to 24 on 6202 between 12,000 and 15,000 s.
- **Grazers stopped killing grazers.** Kill rows with a grazer as victim stayed close to the spheres worlds' count: 1,104
  against 1,110, and 1,754 against 1,916. Most are now part bites. Few are lethal: 87 against 432, and 102 against 821.
- **Leaves stood higher and fed more.** Leaves ended 1.9 and 2.6 times the spheres worlds' count, and the leaf share of
  income rose above the predicted range. Intake per grazer fell by about a quarter, and stayed eight to nine times the
  standing cost of 0.0091 W.
- **The start was slower on 6202.** Its grazers averaged 10 between 6,000 and 9,000 s, against 88 under spheres. 6203's
  averaged 234 against 453. Raw mouth blows at 5,100 s were 11 and 53 times fewer under true shape, which looked like a
  fault in the card's hand-over while the screen ran. Per grazer-second the early windows sit at 0.71 and 0.66. The early
  gap was mostly in how many grazers there were.

My reading, an inference: under spheres a grazer's ball reached a neighbour's parts before its surface did, and most
lethal grazer-on-grazer kills came from that reach. True shape takes the reach away. Grazers lose some leaf bites, but they
stop eating each other, and the leaves they no longer reach stand and feed more of them.

## The host agrees on the slow start

A probe ran the 6202 world on the host engine under both contact forms to 5,400 s, from `scratch/wt-r64t`'s Release farm
(`scratch/r64t-prereg/host-probe.ps1`, runs in `scratch/r64t-probe/`, not kept). Its configs differ only in `contactShape`.
Each host world is its own realisation, so only ratios are compared. Over 0 to 5,400 s, by `contact-share.py --from 0`:

| Engine and seed | Blows per grazer-second, true over spheres | Grazer-seconds, true over spheres |
|---|---|---|
| Host, 6202 | 0.56 | 0.25 |
| Card, 6202 | 0.48 | 0.18 |
| Card, 6203 | 0.32 | 0.06 |

The host shows the same slow start and the same halving of bites. So the slow start is the world's, and the card's
hand-over of the true-shape census to the mouth shows no fault at this scale.

## What it does not say yet

- **The card bites at a distance under true shape, rarely, and the cause is found.** The pre-registration's last
  falsifier fired. Measured on host replays in the first seconds after each checkpoint (`scratch/killgap/`), 220 of the
  232 recorded kills that could be measured touch, the largest gap 0.0002 m. Of the 12 kills only the card made, six were
  0.10 to 0.56 m apart. A card resume of
  `r64t-t-6203` from 22,500 s with a census dump (branch `card-dump`, `9b457e6c`; `scratch/card-dump/`) reproduced one
  of them, 66874 on 70081 at 22,500.5 s. The bookkeeping was clean, and the card's own true-shape test called the pair
  touching: its single-precision GJK ended "origin inside" at pass 3, where the host finds the cores 0.41 m apart. Both cores
  are flat, zero along one axis. The same float inputs on the host give apart: the card contracts
  multiply-adds, so its last bits differ and a near-flat tetrahedron reads as enclosing the origin. The fix (`gjk-fix`,
  merged at `fe0548e7`) writes the GJK uncontracted and refuses "inside" from a flat tetrahedron. On the fixed card, resumes
  at three far kills lose exactly those kills and keep every other kill of the run (3 of 3, 19 of 19, 10 of 10), and groups
  32 and 64 are identical over 30 s (`scratch/gjk-fix/run/`). The read above ran on the unfixed card; the far kills were a
  few in a hundred of the true-shape worlds' kills, so its ratios stand as an inference.
- **Nothing has been seen in the theatre.** The worlds have not been drawn.
- **6202's pace ratio crosses groups.** The spheres world on 6202 ran in a five-world group on the earlier build.
