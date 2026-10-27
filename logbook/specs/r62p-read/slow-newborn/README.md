# Why newborns with their parent's own body plan are slow

*A read of `r62p-t3-6201`, 2026-10-27, the speed agent, at the owner's question of the same day. The structural
mutation screen's draft (`logbook/specs/r62sm-prereg/predictions.md`) derived that a lower structural rate can take
at most a third of the slow newborns away. The owner asked why the other two-thirds are slow, and whether we can find
out. `scripts/reads/slow-newborn-read.py` (written by a Sonnet agent, reviewed by me) produced `slow-newborn.txt`
beside this file. It runs on the newborn ledger's frame table and takes every definition from
`scripts/reads/genome-diff-read.py`. Every number is measured from that file unless it is marked otherwise. Every
split is an association, and any reading of cause is marked as inference.*

## The population

There are 997 bred grazers with a first speed and an unchanged body plan, and 193 of them are slow, under 0.03 m a
second over their first frame pair: a share of 0.194. The genome diff found 999 and 191.

## The five candidate causes

Each band's slow share is set against the population's 0.194.

| Cause | What the split shows | Its share of the 193 |
|---|---|---|
| (a) The parent | Only 14 of the 943 children whose parent has a speed have a slow parent, and 4 of those 14 are slow (0.29, against 0.19). None of the 4 has a slow grandparent. | About none. Slowness does not run in lines, so these are not lineages of sitters |
| (b) Brain-weight change | Slow share by quartile of the child's summed brain change: 0.21, 0.19, 0.17, 0.20. No dose and no trend. | None. The reader's 11 % comes from the quartile cut, with no trend under it |
| (c) Size and reserve | At the first frame, size is adult in 991 of 997, because t3 grows its newborns to adult size on their first step. Reserve at the first frame runs the wrong way for a cause: the lowest quarter is the least slow (0.13). Size at birth on the lineage row is the only clean signal: the largest quarter, at or above 0.6565 of adult, is slow at 0.13, against 0.21 for the rest. | Reserve: inference, an effect of moving, since swimming spends it. Size at birth: up to about a third by the reader's count, but its bands are cut so fine that the figure is rough |
| (d) Birth place | Born within 0.3 m of a leaf: 0.18. Born 0.3 to 1.4 m away: 0.20. Born further: 0.22, n 18. | Under a tenth, and near flat. Being born on a patch is not why they sit |
| (e) The measure | See below | It is not an artefact. Most slow newborns are a distinct group that never swims |

**(e), the measure.** The first speeds are not a smooth tail below 0.03 m a second. 117 of the 193 slow newborns
are under 0.01, and the bins from 0.01 to 0.10 hold about 3 % each. So there is a mode at standstill, apart from the
rest. All but one of the 193 died:

| What the slow newborns did next | n | Median age at death |
|---|---|---|
| Swam by 60 s (late starters) | 36 | 45.5 s |
| Stayed under 0.03 m a second at every later pair | 81 | 29.0 s |
| Died before a later pair | 76 | 24.0 s |

So about four in five of them never swim, and they die near the end of t3's 18 s runway or soon after. The slow
class catches no sitters by design: their parents swam. A newborn that dies before its first frame pair is not
counted as slow. It is left out, and the 554 of those are a separate and larger group, below.

## What it means

**None of the five accounts for the two-thirds.** The parent, the size of the brain change and the birth place
explain almost nothing. Reserve follows from moving rather than causing it. Size at birth is the only lead, and a
modest one.

My reading, which is inference: the slow newborns with an unchanged plan are mostly bodies that do not stroke at all
from birth, children of parents that swam. Every child's brain differs from its parent's, since none in the 997 has no
brain change, but how much it differs predicts nothing. So the next question is which brain change, not how large.
A changed oscillator amplitude, a neuron op, or a rewired input could silence a drive outright while summing to a
small change. A body stuck at birth, against the bed or its parent, would look the same in these frames.

**For the structural screen.** Nothing here asks the screen to scale the brain's weight rate, since its size is flat.
The draft's bound stands, measured here at 0.194. The screen can run as drafted.

**The newborns with no first speed.** 554 bred grazers died before their first frame pair, at a median age of 8.5 s,
and 0.31 of them ate, against 0.55 of those with a speed. They are a larger loss than the slow ones, and no read has
yet said what kills a grazer in its first 10 s.

## What would settle it

Two reads, each in Python on files already on disk, and each a few minutes on one core:
1. **Stroke or stuck.** The joint angles in the first 10 s, from `poses.bin`, for the standstill mode against the
   movers. A body that strokes but does not move is stuck. One whose joints do not move is silent.
2. **Which brain change.** The diff's brain category split by parameter kind: amplitude, frequency, op, input kind
   and channel, and weight. Read for the standstill mode against the movers.

A third, the causes of death of the 554 that die before 10 s, would say what the screen's newborns die of first.
