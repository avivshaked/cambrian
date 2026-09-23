# The stomach screens: where the snow is, read from an eater's own rows

*Fable, 2026-09-23 afternoon. Two inoculation screens of round 45's world at dt 0.02
(`rounds/env-r45.ps1`, 8 threads, runs under `scratch/r46-build/runs/`, not committed): forty
copies of a one-part stomach from round 45 seed 1 at 100 s (`inocula/r45s1-stomach-100.json`)
inoculated at 6,000 s, when the snow has reached its plateau. Screen A placed them at 5 m
(`r46stom-1m-s1`), screen B at 42 m (`r46stom-bed-s1`). The third, a 3 m cell, was dropped
by the owner. The first reading of A, given to the owner that morning, was wrong, and this
file replaces it.*

## What A and B read

| | A, at 5 m | B, at 42 m |
|---|---|---|
| inoculants dead by | 6,250 s | 6,100.5 s |
| age at death, min / median / max | ~100 / 104 / 249 s | 100.5 / 100.5 / 100.5 s |
| snow density at the body, median / max | 0.004 J/m³ | 0.0001 / 0.0008 J/m³ |
| children | 0 | 0 |

B's forty died at one age to the half second, which is a reserve burnt at a constant upkeep
with no intake at all. They sat at 42 m in open water over the deep half of the tilted floor
(the floor at 45 to 60 m there), and the one the placer lifted on to the shallow floor at
30 m saw the same nothing.

## The misread, and where the snow is

The morning's reading of A said the bed held 3.4 J/m³, at which the ledger gives the
stomach an R0 of 34. That number was the table's `detritusOnFloor`, which is `FloorStock`:
the joules in the lowest live cells of the centre patch, summed, 3.5 J. The whole floor at
6,000 s holds 11 J (`floorStockJoules`) of 79,744 J of standing snow. Snow at
`EVOSIM_SINK = 0.002` m/s and `EVOSIM_REMIN = 0.002` per second lives about eight minutes
and falls about a metre in that time, so none of it reaches a floor forty metres down. The
column sums at 6,000 s put it on one side of the tank at 50 to 80 m from the centre, where
the crowd's islands are: 5 to 13 J a column there against 0.02 to 0.3 elsewhere, the densest
columns at 27 J.

The eater's own rows say what that is worth. Four stomach mutants were born inside the
crowd during B (the `r` births with `abs` 1), and the absorptive log carries their local
density and intake at every sample:

| mutant | born | lived | depth | density, median / max J/m³ | intake W, median / max | upkeep W | children |
|---|---|---|---|---|---|---|---|
| 1077, one part | 2,035 s | 30 s | 3 m | 0.27 / 0.28 | 0.15 / 0.15 | 0.23 | 0 |
| 4034, mixotroph | 6,878 s | 3,869 s | 2 m | 0.51 / 0.87 | 0.04 / 0.12 | 0.06 to 0.36 | 0 |
| 5441, one part | 9,235 s | 283 s | 2 to 5 m | 0.49 / 0.85 | 0.16 / 0.29 | 0.14 to 0.17 | 0 |
| 5944, five parts | 10,017 s | 18 s | 5 m | 0.13 / 0.27 | 0.04 / 0.08 | 0.12 | 0 |

The larder is a layer a few metres thick directly under the plant crowd, at about half a
joule a cubic metre where the crowd is densest, which is the stomach's break-even (the
ledger's 0.44 J/m³ for the inoculum's body). A stomach born inside it takes in about what it
burns, lives as long as its reserve and its luck allow, and never accumulates a child's
price; the mixotroph, which pays a smaller stomach's upkeep on a leaf's income, lived an hour
and bred nothing. A stomach placed anywhere else, which is nine columns in ten and every
founder and inoculant so far, sees a hundredth to a thousandth of that and starves in the
time its reserve lasts.

## What it means (inference)

The plant world is the snow's thinness, not its depth. The eaters do not fail to find the
food; the food is too dilute to pay for a child even where it is thickest, and it is
thickest under the plants at the surface, not on the bed. Two things follow for the round
proposal. Placing the second founding window's cohort inside the crowd is necessary and not
sufficient: at 0.5 J/m³ a cohort of forty breaks even and dies childless, as the four
mutants did. What would thicken the larder is the snow living longer (a lower
`RemineralisationPerSecond`, which also slows the plants' matter return in a closed world
and is the owner's dial) or something that gathers it: a floor within a metre or two of the
crowd, which the beach's shoal is only along the rim. The beach's premise as written on the
morning of the 23rd, that snow lands on the lit floor, is true only if the snow lives long
enough to reach a floor, and at the current rate it does not.
