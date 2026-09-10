# 0082 — Five worlds born small

**2026-09-10**  ·  round 33, the growth base; the reading rules written after launch and before the scoring, with what was seen in between stated

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
