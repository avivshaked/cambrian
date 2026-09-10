# 0082 — Five worlds born small

**2026-09-10**  ·  round 33, the growth base; the reading rules written after launch and before the scoring, with what was seen in between stated; read the same morning: the goal rule in five seeds of five, three dials walking the same way in every seed

Round 33 is round 32's world (logbook/0079) with D087 in it: every child born at a fraction
of its adult body and growing, three genome dials in place of two, the mass floor, the
in-place resize, the biomass ceiling beside the count. It is the first round of the
campaign in which a trait can move by degree. The five arms launched on the evening of
2026-09-09 as round 32's workers freed, seed by seed, on one build (`simHash 0924b9ad…`,
`coreHash 621d32ee…`, commit 0b8e822, clean), each header verified against the ruled
values before the next was launched.

## What this entry admits

The round was not pre-registered. Its number changed on the afternoon of the launch, when
the growth build landed and the owner's conditional ruling put growth before the free joint
(logbook/0080's amendment), and the arms went out on freed workers the same evening with
0081 as their only entry. That entry is the build, not a prediction table. So this entry is
written at 00:30 with the first arm just down and unscored, and it says what was seen
before the rules below were written: the running timelines of three arms at a third to a
half of their length, read for the owner's questions during the evening. Those showed the
adult scale falling from 1.0 to about 0.55 in every arm looked at, the investment falling
from 0.65 to about 0.3, the litter rising in two arms and not in a third, the last inherited
joint gone by about 1,500 s as in every priced world, and seed 2 holding a stomach line at
10,000 s where it had lost one by 5,000 s in each of the three rounds before. The rules
below were written knowing that, and they are read as a description of the round rather
than as a test of a prediction. Round 34 is pre-registered (0080).

## The reading

The reference is round 32's same seed, the same water with bodies born whole. Read with
`scratch/r33-read.py` and scored by connected clade.

| # | what is read | what would count |
|---|---|---|
| V1 | the header: round 32's tokens, the growth tokens as ruled, `maxTissue=30000` | every token, every arm |
| V2 | `audit`, `mat resid` at every row; `mat orphan` printed | 0.0000% and 0; the orphan at its float-noise order |
| V3 | the manifests: `ended budget`, one build, `diverged`, the limiters, `resizes`, `resizeJumpMetres`, `resizeStepMetres` | no runaway ended a run; the jump 0; `diverged` read per arm with the resize as a suspect if it is above round 32's 0 |
| G0 | `alive` at 30,000 s against round 32's same seed; the goal rule | within 0.5 to 1.5; the goal-rule count against round 32's two of five, read either way |
| G1 | the three dials and `body frac` at 1,000 s, 5,000 s and the end, per arm, and the sign of each dial's change across seeds | a dial that moves the same way in at least four seeds is read as the world pushing it; a dial that splits across seeds is read as a trade-off with more than one answer; a dial flat within 5% in most seeds is read as unselected |
| G2 | `inherit` at 5,000 s and at the end against round 32's | whether a cheaper child recruits eaters, per seed, and whether seed 2 holds a line to the end |
| G3 | `conceptionsUnderMassFloor` and `growthShortOfMatter` against births | how often the floor and the matter bind; a floor refusal rate above a tenth of births is read as the floor shaping the litter |
| G4 | `jnt inh` at 1,000, 1,500, 5,000 s and the end | whether a small cheap newborn keeps a joint alive past founding; the expectation from the evening's look is that it does not |
| G5 | `mat blk` against births in the last window, against round 32's | whether the grid's refusals changed with the cheaper child |
| L | the birth fraction and adult scale over every birth row in `lineage.jsonl` | the distribution a child is born into, per seed |

What this round cannot say: whether a dial's movement is selection or a bias in the
mutation operator on its own. The step is a Gaussian centred on the value with a width
proportional to it, so an unselected dial's mean over the living stays where it is in
expectation while its median falls, since the same step down is a larger share than the
same step up. The table prints the mean. A mean that halves in every seed is therefore not
the operator, and five arms walking the same way is the strongest evidence a single round
can give of a push. The
test that would settle it, a neutral world with the dials mutating and nothing paying for
them, is a round of its own.

## Results

*2026-09-10, morning.* All five ended on budget at 30,000 s in 7 to 10 hours of wall clock,
one build, `physicsJobWorkers 0`, `gitDirty false`, neither limiter bound, `audit` 0.0000%
and `mat resid` 0 on every row, no ceiling fired. Read with `scratch/r33-read.py` against
round 32's same seed and scored by connected clade.

| arm | alive (r32) | scorer | stomach clade at end (r32 `inherit`) | founded by, at | adult scale | invest | brood | `bf` median | diverged |
|---|---|---|---|---|---|---|---|---|---|
| `r33-s1` | 1,369 (1,923) | pass, 102 | 102 (9) | founder, 36 s | 0.60 | 0.31 | 5.1 | 0.048 | 0 |
| `r33-s2` | 1,730 (1,932) | pass, 74 | 106 (3) | child, 9,622 s | 0.39 | 0.49 | 2.5 | 0.248 | 3 |
| `r33-s3` | 1,762 (1,925) | pass, 120 | 177 (24) | child, 3,266 s | 0.58 | 0.33 | 1.9 | 0.151 | 0 |
| `r33-s4` | 1,321 (1,928) | pass, 82 | 82 (82) | child, 2,141 s | 0.51 | 0.20 | 3.8 | 0.044 | 0 |
| `r33-s5` | 1,556 (1,932) | pass, 79 | 79 (16) | child, 4,501 s | 0.52 | 0.27 | 3.0 | 0.083 | 0 |

**V1 to V3 hold.** Every header carries the growth tokens and both ceilings. The resize
instrument read 0 m of engine displacement over 49,000 to 215,000 resizes per arm. Seed 2
has three divergences, and they are one event: three unjointed leaves of 4 to 7 litres,
generations 10 to 12, 230 to 1,020 s old, within half a metre of one another just under
the surface, thrown to NaN in the same physics step at 3,066.5 s. Not newborns, and not a
resize alone; a contact between bodies. The step-after-resize reading, blind below the
current's 15 cm of drift per half-second, peaked at 25 cm in that arm and between 17 and
19 cm in three others, which is a body being shoved on a growth step. The placer reserves a
newborn's spot at the radius it is born with, and it grows up to twentyfold in place.

**G0, the world stands and the goal rule holds in five seeds of five**, against two in
round 32 and four in round 31. The populations are lower, 0.69 to 0.92 of round 32's, which
is smaller adults holding a matter-limited world. Every passing clade recruits, with 51 to
187 inherited absorptive births in the last window against round 32's 0 to 13. Four of the
five passing clades were founded by a bred child, at 2,141, 3,266, 4,501 and 9,622 s, and
seed 2's line, lost before 5,000 s in each of the three rounds before, was founded at 9,622 s
and held 73 at its lowest through the last two lifetimes. Whether a late stomach can invade
has been open since round 18. In the growth world it happened four times.

**G1, the dials walk, and they walk the same way.** Adult scale fell in every seed, from
about 1.0 to between 0.39 and 0.60. The investment fell in every seed, to between 0.20 and
0.49. The litter rose in every seed, to between 1.9 and 5.1. Body fraction sat at 0.96 to
0.99 throughout, because a child grows to its adult size within a few hundred seconds and
the living are adults at any moment. The seeds agree on the direction of all three and
differ on how far: seed 1 went to five children at a twentieth of their adult body, seed 3
stayed under two at a sixth. The 0082 rule reads three dials pushed the same way in five
seeds of five as the world pushing them, with the operator's bias excluded by the mean.
Children are born at a median 4% to 25% of their adult body by seed, and at an adult scale
of 0.46 to 0.60 of the founders'.

**G3, the mass floor is the sieve.** `conceptionsUnderMassFloor` reads 1.9 to 2.2 million
per arm against 10,000 to 13,500 births. One count is one child refused in one breeding
attempt; the parent keeps its reserve and draws again next step with a fresh mutant. So the
figure is thirty to forty parents standing at their threshold at any moment, drawing
children whose parts the floor will not build, and a birth is a draw that passed. A lineage
at investment 0.3 and a litter of five offers a child at 5% of its body, and the floor lets
through the draws whose parts weigh half a kilogram at that size. That is the shape the
walk found: as small a share as the floor admits, split as many ways as the floor admits.
`growthShortOfMatter` reads 7,000 to 18,000, a fraction of a percent of growth steps.

**G4, a small cheap joint outlives founding once.** Seeds 1, 2, 3 and 5 lost their last
inherited joint by 1,500 to 5,000 s, as every priced world has. Seed 4 held a jointed line
of 55 to 60 bodies from 1,100 to 4,100 s and lost it by 10,100 s. Through that window the
jointed ate as well as or better than the rigid, `food jnt` 11 to 22 against `food rig` 8
to 17, at the same speed, and still declined. The expectation written in this entry's rules
was wrong for one seed in five, and round 34 is now read against that: a joint that costs
nothing but the part it moves has survived founding in this world once.

**Births and generations.** The growth world bred 1.8 to 2.4 times as many children as
round 32's same seed and reached a third of the generation depth, 20 to 28 against 55 to
81: more children per parent, fewer parents in a chain. `mat blk` per window is 155,000 to
286,000 against round 32's 28,000 to 156,000, the same parents at their threshold seen from
the matter gate.

## Verdict

The first world in which a trait can move by degree moved three of them, the same way in
every seed, and held the goal rule in five seeds of five with every line recruiting and four
of the five lines founded by a late child. The dials walked to a small adult, a small share
and a large litter, and stopped where the mass floor stops them, so the floor is now a rule
of the world rather than a guard, and its value is a lever the owner holds. The resize did
not throw a body; the placer, which reserves a newborn's spot at a size it will outgrow
twentyfold, is the suspect for seed 2's one contact event, and it is the fix to make before
round 34. A cheap joint outlived founding in one seed and was gone by 10,000 s having eaten
as well as its rigid neighbours, which is the sharpest fact yet about what kills a joint:
not the price of the muscle and not the food. Round 34 prices the joint at nothing and
reads what is left.

What this entry does not claim: that the walk is selection rather than a push of the world
on a neutral trait. The mean excludes the operator's bias, and five seeds agree, and the
control that would settle it is a world where the dials mutate and nothing pays for them.
That round is queued behind the free joint.
