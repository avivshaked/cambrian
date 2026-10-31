# r63h at 24,000 s: mouths at a hundredth of their price

The screen pre-registered in `logbook/specs/r63h-prereg/predictions.md` on `scratch/wt-r63b` (`3f2310b4`). Three worlds founded on seed 6202 ran in one card group from 16:14 to 19:02 UTC on 2026-10-30, all to 24,000 s, at 1.7 to 3.2 times real time. The read is `scripts/reads/bloom/r63h-read.ps1`, and its outputs sit beside this file (UTF-16, as PowerShell writes them). The control is `r63c-b-6202`, and `r63g-b-6202`, the last screen's best, is shown beside it.

The arms share a consumer upkeep of 0.06 W/m³ and attack and intake prices of 0.001, a tenth of `r63g-b`'s. D adds nothing to that. E sets the intake waste to 0. F raises the bite damage four times.

| Gate | Mark | Control | `r63g-b` | D, cheap | E, no waste | F, deep bite |
|---|---|---|---|---|---|---|
| 1. The leaves fall | at most 4,388 at 24,000 s | 8,777 | 5,338 | **3,008, pass** | **2,495, pass** | 6,670, fail |
| 2. The leaves stop growing | late over mid mean at most 1.0 | 2.05 | 1.43 | 1.18, fail | 1.17, fail | 1.77, fail |
| 3. Grazers do it | at least 0.6 of leaf deaths, last two bins | 0.51, 0.47 | 0.76, 0.65 | **0.73, 0.66, pass** | **0.81, 0.69, pass** | **0.76, 0.71, pass** |
| 4. Grazers rise | mean over 21,000-24,000 s at least 238 | 119 | 589 | **978, pass** | **691, pass** | **1,700, pass** |
| 5. The plants live | at least 500 leaves after 3,000 s | 1,286 | 1,077 | **920, pass** | **706, pass** | **1,185, pass** |
| 6. The grazers live | `g2-read.py`'s gate 1 | pass | fail | fail | fail | fail |

**No arm passes all six, so no arm controls the plants.** Gate 2 fails in every arm, as in `r63g`: the leaves still grow from the middle window to the last. Gate 6 fails at the fixed 0.908 W in every arm and passes in all three at each arm's own standing cost of about 0.0091 W (`g2-read-*-upkeep0.0091.txt`), as `r63g`'s B and C did at theirs. It is information, not the gate.

- **Grazers are now plentiful.** They number six to fourteen times the control's in the last window, and they do two thirds to four fifths of the leaf killing from 9,000 s on. D and E end with the leaves at a third of the control's.
- **And they live more on animals than on leaves.** The share of bite income from living leaves, over 21,000-24,000 s (`income-split.txt`):

| World | Leaves | Other living bodies | Remains |
|---|---|---|---|
| control | 0.65 | 0.26 | 0.09 |
| `r63g-b` | 0.40 | 0.52 | 0.08 |
| D | 0.38 | 0.54 | 0.07 |
| E | 0.36 | 0.57 | 0.07 |
| F | 0.52 | 0.42 | 0.07 |

- **The grazers' main killer is each other.** Being eaten is 55 % of grazer deaths in D, 64 % in E and 76 % in F, against 57 % in `r63g-b`, almost all by grazers (`grazer-deaths-*.txt`). Close kin are 13.5 %, 16.0 % and 18.8 % of grazer-on-grazer kills (`cannibal-*.txt`), against 16 % in `r63g-b`.
- **The deep bite leaves more leaves, not fewer.** F has the most grazers and the highest leaf share of income, but its leaves end at 0.76 of the control's. A harder bite kills grazers faster as well as leaves (76 % of its grazer deaths are kills). Which effect wins is not something this read shows.
- **The books.** The audit residual stays under 0.01 J. The matter residual peaks at 8.5e-5 units in D and E and 1.5e-5 in F, above `r63d`'s 1.3e-5 and the launcher's 1e-5 line. Against about 8,000 to 13,000 units standing it is a few parts in a billion. That it is float rounding from the many more bites is my inference and is not checked.

## The predictions against the result

| Prediction | Result |
|---|---|
| D: grazers past B's 589; their leaf-death share past 0.7; leaves under half the control's; gate 2 fails | Right on grazers (978), leaves (0.34) and gate 2; the share is 0.73 then 0.66 |
| E: as D, slightly more grazers | Wrong: fewer (691), the leaves lowest |
| F: the leaves fall furthest; the arm likeliest to pass gate 2 and to fail gate 5 | Wrong on all three: leaves highest, ratio 1.77, gate 5 passes |
| Pace 2.5 to 3 h | 2.8 h |

## What it means, as inference

The cost dials have done what they can. Mouths here cost a tenth of `r63g-b`'s, and grazers rose by up to three times. But the leaves still grow, and more than half of a grazer's income is now other animals. A cheaper mouth makes a cheaper predator as much as a cheaper grazer, because one mouth eats everything at one yield. That is the gap the matter-kinds proposal's diet gene closes (`proposals/propose-matter-kinds.md`), and the cannibalism is what the recognition proposal answers (`proposals/propose-recognition.md`). The leaves may also keep growing because matter flows in at the islands at a fixed rate, so the standing crop has a growing supply to grow from. That is the question behind D261, and it is not tested here.

No arm controls the plants, so none is resumed. `r63g` step 2's resume of B and C to 48,000 s stays queued, but the unified proposal is the better use of the next card time.
