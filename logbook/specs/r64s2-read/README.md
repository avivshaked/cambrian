# r64's step 2: D carried on from 24,000 s, read to 45,000 s

The step pre-registered in `logbook/specs/r64-prereg/step2.md` on `phase1-overpower` (`632af648`).
- **The run.** `r64s2-d-6202` and `r64s2-a-6202` resumed their worlds' checkpoints at 24,000 s in one card group from
  23:34 UTC on 2026-10-30. A reached 48,000 s.
- **The stop.** At 02:44:59 UTC on 2026-10-31 the graphics driver logged an error (nvlddmkm id 13, a Graphics FECS
  Exception). Thirteen seconds later the card group's watch ended the farm, as it is built to do. D's last stats row is at
  46,530 s, and its last checkpoint at 46,500 s. The coordinator recorded the stop in D's `run.json`, since the farm
  wrote no ending.
- **This read.** D is read to 45,000 s, its last whole window, and A to 48,000 s. The read is
  `scripts/reads/bloom/r64s2-read.ps1`, and its outputs sit beside this file.
- **D's lineage.** Its last line was cut mid-write, so D's deaths and kin are read from a copy without that line.

## The marks, for D at its last whole window (42,000-45,000 s)

| Mark | Bar | D | A, information (45,000-48,000 s) |
|---|---|---|---|
| 1. Grazers boom | mean at least 1,956 | **2,630, pass** | 1,079 |
| 2. Grazers graze | leaf share of bite income at least 0.8 | 0.58, fail | 0.52 |
| 3. The plants live | fewest leaves after 24,000 s at least 500 | **1,686, pass** | 1,449 |
| 4. They balance | grazers' and leaves' means within a factor of 2 of the window before, over the last two windows | **pass**: grazers 0.85 then 1.35, leaves 0.70 then 1.60 | grazers 0.65, leaves 0.76 |

D passes marks 1, 3 and 4 and fails 2. Grazers number 2.7 times the control's 978 at 24,000 s, and the leaves live.

## What happened

- **D's grazers kept rising, then rode the seasons.** Their window means from 24,000 s run 1,284, 1,375, 1,516, 2,045,
  2,308, 1,955 and 2,630 (`g2-read-r64s2-d-6202.txt`). They passed the boom mark at 33,000 s.
- **The leaves swing with the year, and rise.** D's leaves by window run 4,566, 2,416, 6,373, 3,710, 7,322, 5,112 and
  8,165, then 4,222 in the half window to 46,530 s (`window-leaves.txt`). The swing has a period of 6,000 s, which is
  this world's year, and its peaks climb. A's leaves swing on the same clock, at about half the size.
- **So the swing is the seasons, not a chase.** The grazers' wobble keeps time with the leaves' within a window, with no
  lag. A grazer-and-plant cycle would show the grazers lagging the leaves. This is inference, at 3,000 s resolution.
- **The grazers do not hold the leaves down.** Each leaf peak beats the one before. The grazers are many, but they still
  take 42 % of their bite income from animals and remains, against the 20 % or less the mark asks for
  (`income-split-r64s2-d-6202.txt`).
- **More grazers, more killing.** Being eaten rose to 18.5 % of D's grazer deaths in the step, from 7 % at 24,000 s.
  Close kin are 1.3 % of grazer-on-grazer kills (`cannibal-r64s2-d-6202.txt`). Starvation fell to 42 %, at a median age
  of 568 s.

## The predictions against the result

| Prediction | Result |
|---|---|
| D: grazers past 1,956 by 30,000 s, peaking at 2,500 to 4,000; leaves under 1,500, then a swing; mark 3 holds and mark 4 fails | Grazers passed 1,956 at 33,000 s and reached 2,630; the leaves never went under 1,686, and they swing with the seasons; mark 3 holds; mark 4 passes |
| A: grazers near 1,000, leaves near 2,000, little change | Right: grazers 800-2,000 and leaves 1,850-3,660 by window |
| Pace 1.5 to 2.5 times real time, 3 to 4.5 h | 1.5 to 3.5 times; D stopped at 46,530 s by the driver event |

## What it means, as inference

D is the first world where grazers boom and stay boomed, about 2,600 against the control's 978, with the plants alive
beside them. But the plants are not yet pushed by them. The leaves climb year on year and swing with the islands'
blooms, and a grazer still eats animals for two fifths of its living. In the owner's terms, the grazers have boomed but
do not yet endanger the plants. The next pushes are the ones already planned. The smell proposal would let a grazer find
leaves rather than its neighbours. The anchoring cell would hold plants by their fountains, where grazers could learn
them. D's world, at 45,000 s or from its 46,500 s checkpoint, is the candidate world for those screens.
