# The stomachs' ledgers at round 46's prices

*Fable, 2026-09-23 night, for 0116's K3 section and D117's pool. Every row is
`scripts/ledger.ps1 -Config runs/r46-s1/<run>/config.json -Clearance 10 -Spent 0.25` at
3 m depth; the clearance is the config's own absorptive rate (10 volumes a second). An
earlier pass that night ran at a clearance override of 1 and put the inoculum's break-even
at 4.4 J/m³; those numbers are withdrawn.*

| body | kind | volume m³ | standing W | break-even J/m³ | net W at 0.5 | net W at 1 | R0 at 1 | R0 at 2 | ledger lifetime at 0.5 |
|---|---|---|---|---|---|---|---|---|---|
| `inocula/r45s1-stomach-100.json` (the screens' inoculum) | absorptive, one part | 0.090 | 0.36 | 0.44 | +0.045 | +0.45 | 2 (first child 410 s) | 12 (122 s) | 420 s |
| `scratch/r46-read/r46-s1-trickle-stomach-1120.json` (a trickle founder of seed 1) | consumer + link | 0.090 | 0.62 | none up to 10 | −0.43 | −0.40 | 0 | 0 | 7 s |
| `scratch/r46-read/r46-s3-trickle-stomach-1707.json` (a trickle founder of seed 3) | consumer alone | 0.089 | 0.66 | none up to 10 | −0.63 | | 0 | 0 | 4 s |
| `scratch/r46-read/r45-s2-eater-24633-f8.json` (round 45 seed 2's one bred eater, ported to format 8) | develops as a leaf: its consumer parts fall under the part-volume floor | 0.025 | 0.08 | not a stomach | | | 29 (light) | | |
| `scratch/r46-read/r45-s1-eater-26578-f8.json` (round 45 seed 1's, ported) | develops as a leaf, the same way | 0.020 | 0.06 | not a stomach | | | 30 (light) | | |

What it says. A consumer mouth scavenges at one body volume of water a second (`scavengeRate`
1, `carrionYield` 0.8) and cannot live on snow at any density this world holds. An absorptive
stomach clears ten volumes a second and breaks even at about the density the crowd's
underside holds, so it lives there and saves nothing; it breeds from about 1 J/m³, which the
densest columns under the crowd reached at the screen (1.9 J/m³). Round 45's two "eaters"
were mixotrophs whose consumer parts were too small to develop, so the pool of evolved
eaters on disk that this build can read is the inoculum alone; round 46's own longest-lived
absorptive founders are the other candidates, read below.

## Round 46's longest-lived absorptive founders

Each is a trickle founder of round 46 with an absorptive part and a link (the link cell
photosynthesises at 0.025, so the light pays part of the standing cost), pulled from the
snapshot nearest its birth; none bred in the world. The same ledger call as above.

| body | lived | absorptive m³ | link m³ | standing W | break-even J/m³ | net W at 0.5 | R0 at 1 (first child) | R0 at 2 (first child) |
|---|---|---|---|---|---|---|---|---|
| seed 1, 1182 | 1,312 s | 0.162 | 0.053 | 0.80 | 0.17 | +0.49 | 2 (847 s) | 16 (262 s) |
| seed 1, 1559 | 2,215 s | 0.055 | 0.021 | 0.28 | 0 | +0.27 | 2 (957 s) | 8 (354 s) |
| seed 1, 2972 | 2,209 s | 0.051 | 0.053 | 0.36 | 0 | +0.42 | 2 (1,288 s) | 8 (468 s) |
| seed 1, 3844 | 1,578 s | 0.052 | 0.024 | 0.28 | 0.04 | +0.21 | 0 | 6 (691 s) |
| seed 3, 1513 | 1,544 s | 0.103 | 0.015 | 0.46 | 0.29 | +0.20 | 3 (338 s) | 15 (123 s) |
| seed 3, 2156 | 1,467 s | 0.080 | 0.096 | 0.59 | 0 | +0.58 | 2 (568 s) | 12 (269 s) |
| seed 3, 2196 | 2,470 s | 0.021 | 0.040 | 0.20 | 0 | +0.27 | 0 | 3 (864 s) |
| seed 3, 4854 | 1,134 s | 0.034 | 0.026 | 0.21 | 0 | +0.22 | 0 | 6 (754 s) |

Every one of them nets a positive watt at 0.5 J/m³ and none breeds there: the margin over
the standing cost is too small to save a child's price (100 J of overhead and the child's
tissue) inside a life. From 1 J/m³ the larger stomachs breed at R0 2 to 3 with a first child
inside 350 to 1,300 s; from 2 J/m³ all of them do. The pool for round 47
(`inocula/pool-r47/`) is the inoculum plus 1182, 1513 and 2156, the three with the largest
R0 at 2 J/m³ and a child inside a life at 1.
