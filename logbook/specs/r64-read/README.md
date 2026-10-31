# r64 at 24,000 s: D264's phase 1, animals hard to kill, newborns provisioned, the young spread

The screen pre-registered in `logbook/specs/r64-prereg/predictions.md` on `scratch/wt-phase1` (branch
`phase1-overpower`, `3110e3e3`). Five worlds were founded on seed 6202 and ran in one card group from 20:23 to 23:26 UTC
on 2026-10-30, all to 24,000 s, at 1.4 to 2.4 times real time. The identity check passed first: with every switch off,
the build matched `r63h`'s D and E row for row over 600 s (`c3c6040f`). The read is `scripts/reads/bloom/r64-read.ps1`,
and its outputs sit beside this file. The control is `r63h-d-6202`, and `r63c-b-6202` is shown for the leaves.

Every arm is `r63h-d`'s world, with mouths at a hundredth of their price:
- **A:** animal health ten times, plant health a fifth.
- **B:** newborns given 0.8 of the reserve.
- **C:** the young spread over 30 m.
- **D:** all three of A, B and C.
- **E:** D with mouths at ten times `r63h`'s price.

## The marks

| Mark | Bar | Control `r63h-d` | A | B | C | D | E |
|---|---|---|---|---|---|---|---|
| 1. Grazers boom | mean over 21-24k at least 1,956 | 978 | 1,003 | 1,471 | 862 | 1,255 | 338 |
| 2. Grazers graze | leaf share of bite income over 21-24k at least 0.8 | 0.38 | 0.63 | 0.33 | 0.46 | 0.59 | **0.84** |
| 3. Not each other's food | kills at most 0.25 of grazer deaths | 0.55 | **0.06** | 0.58 | 0.30 | **0.07** | **0.01** |
| 4. Plants pushed and alive | leaves at end at most 4,388, fewest after 3,000 s at least 500 | 3,008, 920 | **2,081, 709** | **3,568, 838** | **3,336, 1,377** | **2,430, 897** | **3,432, 956** |
| 5. Leaves stop growing | late over mid mean at most 1.0 | 1.18 | 1.01 | 1.51 | **0.88** | **0.72** | 1.11 |
| 6. Kin spared by distance (C, D, E) | close kin at most 0.08 of grazer-on-grazer kills | 0.135 | 0.12 | 0.14 | **0.04** | **0.04** | 0.10 |

Close kin are siblings, parents and children (`cannibal-*.txt`). **No arm passes marks 1 to 5**, so by the
pre-registration's rule no arm is resumed as its winner. `g2-read.py`'s gate 1 fails in every arm on D222's test 2, as
in every screen since `r63b`. The floor passes in every arm. That gate is information here, not a mark.

## What happened

- **Hard animals end the killing.** Being eaten falls from 55 % of grazer deaths in the control to 6 % in A, 7 % in D and
  1 % in E (`grazer-deaths-*.txt`). Provisioning alone (B) leaves the killing where it was, at 58 %, and spreading alone
  (C) halves it.
- **And then the grazers starve young.** With being eaten gone, starvation takes 50 % of A's grazer deaths at a median
  age of 256 s, 60 % of D's at 333 s, and 88 % of E's at 144 s. A grazer that is not eaten in its first minutes now dies
  of hunger in them.
- **Grazers still bite animals for a third of their income.** In A and D, 29 % and 32 % of bite income over the last
  window comes from other living bodies, while kills are rare. With animals ten times tougher, a bite on an animal now
  wounds and feeds without killing. Grazers have become each other's parasites rather than each other's prey
  (inference, from the income split beside the kill share).
- **E is starved by its mouth's price.** Its lineages take 0.78 to 0.90 of their income from leaves
  (`lineage-income-r64-e-6202.txt`), the best grazing of any world yet. But at ten times the price they are few, and
  most die within minutes of birth.
- **D is the one where grazers take over the leaves.** Its grazer means over the six windows from 6,000 s run 88, 196,
  242, 374, 709 and 1,255, doubling every 3,000 s at the end. Its leaves end at 0.28 of the control's and fall in their
  late window (ratio 0.72). Grazers have been the leaves' largest killer in three of the last four bins
  (`overshoot-r64-d-6202.txt`). It fails marks 1 and 2 at 24,000 s. Its trend is the one the owner asked to see:
  grazers booming until they endanger the plants.
- **The books.** The audit residual peaks at 4.8e-4 J and the matter residual at 6.6e-6 units in E, both under the
  launcher's lines.

## The predictions against the result

| Prediction | Result |
|---|---|
| A: kills under 0.3 of grazer deaths; grazers past the mark; the leaves fall fastest; gate 4 may fail by overshoot | Right on kills (0.06) and the leaves (2,081, the fewest of any world so far); wrong on the boom (1,003) and the overshoot (fewest 709) |
| B: more newborns live, grazers rise somewhat, the kill share hardly moves | Right on all three (1,471; 0.58) |
| C: close kin under 0.08; the kill share falls a little | Right on kin (0.04); the kill share fell by half, more than a little |
| D: the leaves crash under 500 by 12,000 s, then the grazers crash | Wrong: the fewest leaves were 897, and the grazers are still rising |
| E: between the control and D, likeliest to pass 1 to 5 | Wrong: the fewest grazers of any arm |
| Pace 2 to 3 times real time, 3 to 4.5 h | 1.4 to 2.4 times, 3.0 h |

## What it means, as inference

Phase 1 took the cannibal economy away, and what it uncovered is a newborn that cannot find its first meal in time.
Provisioning at 0.8 did not save D's young, so the reserve is not the limit. The likelier limits are how far a newborn
is from a leaf it can reach, and whether its brain steers there at all. That is where phase 2's senses come in: the
near and far smell and the flow sense of the smell proposal.

D is carried on to 48,000 s as a new step, to see whether its grazers settle against the leaves or overshoot them. It
is pre-registered in `logbook/specs/r64-prereg/step2.md` on `phase1-overpower` before it launches. A goes with it,
since a card group needs two worlds, and it is the comparison with the health lever alone.
