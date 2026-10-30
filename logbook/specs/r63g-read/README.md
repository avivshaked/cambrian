# r63g at 24,000 s: overpowered grazers in the film's world

The screen pre-registered in `logbook/specs/r63g-prereg/predictions.md` on `scratch/wt-r63b` (D262, the owner's ruling of
2026-10-30 that overpowered grazers come before any change to matter). Three worlds founded on seed 6202 ran in one card
group from 13:20 to 15:56 UTC on 2026-10-30, all to 24,000 s, at 2.6 to 2.7 times real time. Each carried its arm's
dials in its config, and the farm and Dynamics hashes are `r63d`'s. The read is `scripts/reads/bloom/r63g-read.ps1`, and
its outputs sit beside this file. The control is `r63c-b-6202`.

| Gate | Mark | Control | A, fast | B, cheap | C, both |
|---|---|---|---|---|---|
| 1. The leaves fall | at most 4,388 at 24,000 s | 8,777 | 5,429, fail | 5,338, fail | **3,214, pass** |
| 2. The leaves stop growing | late over mid mean at most 1.0 | 2.05 | 1.59, fail | 1.43, fail | 1.23, fail |
| 3. Grazers do it | grazer kills at least 0.6 of leaf deaths, last two bins | 0.51, 0.47 | 0.57, 0.39, fail | **0.76, 0.65, pass** | **0.72, 0.63, pass** |
| 4. Grazers rise | mean over 21,000-24,000 s at least 238 | 119 | 77, fail | **589, pass** | **456, pass** |
| 5. The plants live | at least 500 leaves after 3,000 s | 1,286 | **1,493, pass** | **1,077, pass** | **781, pass** |
| 6. The grazers live | `g2-read.py`'s gate 1 | pass | **pass** | fail | fail |

**No arm passes all six, so no arm controls the plants by the pre-registration.** B and C come closest, and the lever
is cost, not speed.

- **Speed alone does nothing for grazers.** A's grazers end at 77 on average in the last window, fewer than the
  control's 119. 71 % of them starve, at a median age of 72 s, as in the control. A's leaves end at 0.62 of the
  control's, but grazers did only 0.39 of the leaf killing in the last bin, and most leaves died of age. Why the faster
  stroke thins the leaves is not something this read shows.
- **Cost moves grazers.** At a tenth of every mouth price, B's grazers average 589 in the last window, five times the
  control's. 16,598 grazers were born, against 3,877 in A. Grazers do two thirds to three quarters of the leaf killing
  in every bin from 12,000 s.
- **C overshoots on the leaves and still does not hold them.** C's leaves are under half the control's from 18,000 s
  and end at 0.37 of it, with the fewest at 781, so the plants survive. But they still grow, 1.23 times from the middle
  window to the last, so gate 2 fails in every arm.
- **Grazers eat grazers.** In B, 57 % of grazer deaths are kills by other grazers, and in C 49 %, against 21 % in A and
  the control. Starvation falls to 32 % and 39 %, and its median age rises from about 70 s to about 300 s. The more
  grazers there are, the more of them die in each other's mouths. In B, 16 % of grazer-on-grazer kills are close
  kin: a parent eats its child in 7.8 %, a sibling eats a sibling in 6.2 %, and a child eats its parent in 2.1 %
  (`cannibal-r63g-b-6202.txt`). Cousins count as unrelated here. A mouth reads only the tissue's type, joules and
  whether it is alive (`TissueContact`), so nothing in the world tells kin from stranger.
- **Where income comes from at 24,000 s**, as shares of bite income from leaves, from other living bodies and from
  remains: control 0.68, 0.22, 0.09; A 0.70, 0.21, 0.09; B 0.45, 0.49, 0.06; C 0.51, 0.43, 0.06. So cheap mouths
  made B's and C's grazers half predators. A grazer screen from here gates on the share from living plants.
- **Gate 6 fails in B and C on a fixed measure.** D222's test 2 asks whether a grazer keeps 0.9 of 0.908 W, the
  control's standing cost. In B and C a grazer's standing cost is about 0.091 W (the ledger, in the pre-registration).
  Read with `--upkeep-w 0.091`, which is not the pre-registered measure and cannot change the verdict, test 2 keeps 2.2 to
  6.8 times that, and gate 6 passes in both (`g2-read-r63g-b-6202-upkeep0.091.txt`, `...-c-...`). So the grazers are
  not hungry. The test's fixed upkeep does not fit a world where grazing is cheap.
- **The bodies.** At the end, B holds 5,771, C 3,533 and A 5,507, against 9,053 in the control. The matter standing
  is 10,100 to 12,800 units, against 16,100.
- **The books.** The audit residual stays under 0.003 J. The matter residual peaks at 1.3e-5 and 1.1e-5 units in A and
  C, at the scale `r63d` read as rounding.

## Why speed did not help: grazers do not seek food

The owner asked on 2026-10-30 whether grazers swim to plants or drift. A follow-up read of the control, A and B over
6,000 to 24,000 s, every 10 s positions row, answers it (`scripts/reads/bloom/r63g-motion/`, outputs in `motion/`).

- **They swim, but not toward food.** Grazers move through the water at a median 36 cm/s in the control and 57 in A,
  against 2 cm/s for drifting leaves. The share of each 10 s move toward the nearest leaf is −0.01 in every world, as
  for the drifting leaves themselves. Only within 2 to 5 m of a leaf do grazers lean toward it, by about 5 % of the
  step. Their heading follows the leaf smell's gradient barely (a mean cosine of +0.02 to +0.05).
- **They do not need to.** A newborn grazer starts a median 1.5 m from a leaf, and 94 % of A's starvers came within 2 m
  of one; 69 to 83 % of grazer fixes are within 2 m of a leaf. The world is a leaf carpet, not oases.
- **A's faster stroke went into motion, not travel.** A's grazers move 1.6 times faster through the water but cover
  no more ground in 10 s than the control's (inference: the push goes into turning). Swimming costs a quarter to a
  third of standing upkeep at most, and A's is cheaper per metre through the water, about equal per metre of ground.
- **So what kills a newborn is upkeep, not distance.** 55 % of A's starvers bite before dying, at a median 30 s, and
  still die at 72 s. Cheap mouths stretch the runway tenfold, which is why B worked (inference).

The record cannot show each body's first meal or its own ledger: the code holds them per body but writes them only
into checkpoints. A death row carrying them would answer this directly.

## The predictions against the result

| Prediction | Result |
|---|---|
| A: grazers rise and their share of leaf deaths climbs; gates 3 and 4 may pass (moderate) | Wrong: A's grazers end below the control's |
| B: grazers rise several times over, starvation's share falls, leaves end at 0.4-0.8 of the control's, gate 2 fails (low) | Right on all four: five times, 0.32, 0.61, fail |
| C: leaves under half the control's by 18,000 s; grazers boom and bust; gate 5 may fail (guess) | Leaves right (0.48 at 18,000 s); no bust; gate 5 passes |
| Cannibalism's share rises in every arm that raises grazers (moderate) | Right: 0.57 and 0.49 against 0.21 |
| Pace: 2.5 to 3.5 h | 2.4 to 2.6 h |

## What it means, as inference

Cheap grazing makes grazers plenty and makes them the main killers of leaves, but at 24,000 s the leaves still grow
in every arm. The grazers' own crowd is now their limit: more than half of B's grazers die eaten by other grazers. A
grazer that does not eat other grazers would leave more grazers to eat leaves, so cannibalism is the next limit in
line, if grazers are to control the plants. The screen was 24,000 s; whether C or B levels off later, as `r63d`'s worlds
did not, is a resume, which the pre-registration allows as a new step for an arm that controls the plants, and no arm
does.
