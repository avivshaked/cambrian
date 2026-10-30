# Phase 1 of D264: animals hard to kill, plants easy, newborns provisioned and the young spread

*Pre-registration, written by the coordinator on 2026-10-30 before any world of it ran. The owner ruled the three levers
the same day (D264) and asked that the values aim to overshoot. The screen launches from `scratch/wt-phase1` (branch
`phase1-overpower`) at this file's commit, with that worktree's own Release farm.*

## Why

`r63g` and `r63h` made grazers plentiful, and then most of them died in other grazers' mouths
(`logbook/specs/r63h-read/`). A mouth bites whatever it touches, one contact can kill, and a bite takes a share of a
grazer's concentrated reserve. The young start within 5 m of their family. So the world pays more for a sibling than for
a leaf. The owner's view is that plants live easy and must die easy, and that animals should be much harder to kill, so
that grazers boom to the point of endangering the plants and the two come to balance each other.

## The worlds

Every arm is `r63h-d-6202`'s world (`rounds/env-r63h-d.ps1`: mouths at a hundredth of their price), with D264's death-row
income on as an instrument. The control is the recorded `r63h-d-6202`, not run again. Each arm is founded on seed 6202
for 24,000 s in one card group (`rounds/launch-r64.ps1`).

| World | Animal health | Plant health | Newborn reserve | Young spread over | Mouth prices |
|---|---|---|---|---|---|
| `r63h-d-6202` (control) | 1 | 1 | 0.5 | 5 m | r63h's |
| `r64-a-6202` | **10** | **0.2** | 0.5 | 5 m | r63h's |
| `r64-b-6202` | 1 | 1 | **0.8** | 5 m | r63h's |
| `r64-c-6202` | 1 | 1 | 0.5 | **30 m** | r63h's |
| `r64-d-6202` | **10** | **0.2** | **0.8** | **30 m** | r63h's |
| `r64-e-6202` | **10** | **0.2** | **0.8** | **30 m** | ten times r63h's (`r63g-b`'s) |

A body with any photosynthetic part is a plant, and its every part takes the plant scale. Every other body is an
animal (`Metabolism.IsPlant`). Each world's `config.json` must show its arm's values, or the world is stopped.

## The identity check, before the launch

`rounds/identity-r64.ps1` founds `r63h-d`'s world with the new build and every D264 switch off, for 600 s on the card.
`scripts/reads/compare-run-rows.py` must find its rows equal to the prefix of `r63h-d-6202`'s. If they differ, the
control is not this build's world, nothing launches, and the difference goes to the owner.

## The marks

Read at 24,000 s with `scripts/reads/bloom/r63h-read.ps1`'s steps on these arms, and the death rows' income by lineage.
The last window is 21,000-24,000 s. Grazers are the trait table's (`atk` and `ink` both 1), as in `r63g` and `r63h`.

1. **Grazers boom.** Their mean count over the last window is at least 1,956, twice `r63h-d`'s 978.
2. **Grazers graze.** The share of bite income from living plants over the last window (`income-split.py`) is at
   least 0.8, against `r63h-d`'s 0.38. The lineage read gives the same share for each grazer lineage with 30 or more
   living members, as information.
3. **Grazers are not each other's food.** Kills are at most 0.25 of grazer deaths (`grazer-deaths.py`), against
   `r63h-d`'s 0.55.
4. **The plants are pushed hard and live.** The leaves at 24,000 s are at most 4,388, half the control `r63c-b`'s, and
   the fewest after 3,000 s are at least 500.
5. **The leaves stop growing.** The mean over the last window is at most the mean over 12,000-15,000 s.
6. **Kin are spared by distance.** In C, D and E, close kin are at most 0.08 of grazer-on-grazer kills
   (`cannibal.py`), against `r63h-d`'s 0.135.

An arm that passes 1 to 5 is grazers and plants balancing each other, and is resumed toward 48,000 s as a new step to
see whether they settle or swing. An arm that overshoots by gate 4, with the leaves under 500 or grazers at 0, has
shown the lever works, and the next screen dials it back.

## Predictions

Mine, written before any run, and guesses.

- **A:** kills fall under 0.3 of grazer deaths, grazers boom past the mark, and the leaves fall faster than in any arm
  so far. Gate 4 may fail by overshoot, with the leaves under 500 (moderate).
- **B:** more newborn grazers live, and leaf seedlings too. Grazers rise somewhat, and the kill share hardly moves (low).
- **C:** close kin fall under 0.08 of grazer kills, the kill share falls a little, and more births are refused for room
  (moderate).
- **D:** the overshoot: the leaves crash under 500 by 12,000 s, and then the grazers crash after them (a guess).
- **E:** between `r63h-d` and D, and the arm likeliest to pass gates 1 to 5 (a guess).
- **Pace:** five worlds on the card at 2 to 3 times real time, about 3 to 4.5 hours.

## What would change the plan

`r63g`'s rules hold. A hash that differs, a config without its arm's values, a world under 0.5 times real time, or an
arm past the overshoot early (leaves under 500 and grazers at 0) stops what it names.
