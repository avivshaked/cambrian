# How much memory evolved brains carry

*A census by a Sonnet subagent on 2026-11-01, checked by the coordinator the same day. The owner asked whether the
creatures are stateless reactive machines, and how much a memory would help them.*

## The measure

A central brain (D146) is a recurrent network of tanh units, stepped every 0.01 s. Each unit sums its bias, its sense
ports now and the units' values from the step before (`src/Evosim.Core/Brain/Brain.cs`, `StepCentral`). Only a link
from one unit to another, or to itself, carries anything from one step to the next.

The read, `scripts/reads/brain-memory.py`, copies that rule in Python. It settles each brain with every port at 0 and
gives one port a pulse of 1 for one step. It then counts the steps until every unit is back within 0.01 of where it
would have been without the pulse. A brain's memory is its longest such count over its ports. A brain whose pulse moved it to a
new resting state would be a latch. The docstring gives every definition. Its hand check steps body 64705's brain five
steps by hand and by the batched rule, and the two agree to six places. Its self-test finds no difference between the
two rules on 60 random brains.

The genomes are each run's `genomes.jsonl.gz`, which holds the brain a body was born with. The living population at a
time is the ids in that time's snapshot. A grazer is a body born with a mouth (attack and intake both above 0 in its
birth row).

## What it found

The table counts grazers only. A brain with no unit link has memory 0.

| Population | Grazers | With a unit link | With a cycle | Memory, median / p90 / longest |
|---|---|---|---|---|
| r64t-t-6202 at 6,000 s | 10 | 50% | 0% | 0.01 / 0.01 / 0.01 s |
| r64t-t-6202 at 12,000 s | 25 | 40% | 0% | 0 / 0.01 / 0.01 s |
| r64t-t-6202 at 24,000 s | 818 | 63% | 27% | 0.01 / 0.02 / 0.07 s |
| r64t-t-6203 at 6,000 s | 13 | 85% | 0% | 0.01 / 0.01 / 0.01 s |
| r64t-t-6203 at 12,000 s | 459 | 56% | 3.5% | 0.01 / 0.01 / 0.33 s |
| r64t-t-6203 at 24,000 s | 2,155 | 67% | 7.4% | 0.01 / 0.02 / 0.05 s |
| r64s2-d-6202 at 46,500 s | 2,207 | 92% | 23% | 0.02 / 0.03 / 0.15 s |
| Founders drawn from the pool, 6202 / 6203 | 305 / 271 | 62% / 58% | 0% | 0.01 / 0.01 / 0.01 s |

No brain latched in any population, and no self-link had a weight above 1. Under 4% of the bodies without a mouth carry
any unit link, and none holds more than 0.05 s. The longest memory found is 0.33 s, in grazer 24296 of r64t-t-6203 at
12,000 s, from one hidden unit with a self-link. The world's first seeding (60 and 58 bodies) has no unit link at all.

Evolution does add recurrence. Cycles among grazers go from none at 6,000 s to 27% by 24,000 s in 6202, and the longest
run, r64s2-d, has the most hidden units. But the memory stays at one to a few steps. The median grazer's trace is one
step: a sense reaches a hidden unit, and the hidden unit reaches an output the step after.

## What it means

Each point here is an inference.

- The owner's reading holds for the brain. A grazer that loses touch with a leaf keeps no trace of it beyond a few
  hundredths of a second. What persists lives in the body: its posture, its speed and its place, which its senses read
  again at each step.
- A self-link can in principle hold a long trace, since a loop whose gain sits just under 1 decays slowly. A pulse of
  about 0.5 stays above this census's 0.01 line for 4 s only if the loop's gain per step is about 0.99. Above 1 the unit
  latches, and at 0.95 it forgets in under a second. Evolution found 0.33 s once in these populations, which suggests
  the target is narrow for mutation of a weight to hit.
- A bacterium's temporal comparison spans about 4 s [SBB86, round 6 of the review]. If gradient-following here needs a
  window of that order, the present brain lacks it by two orders of magnitude.

## What it does not say

- The pulse is measured from the brain's resting state with every sense at 0. A brain driven by live senses sits
  elsewhere on its tanh curve, where the gain is usually lower, so live memory is probably no longer than this.
- The pulse is 1 on one port. Live senses can be larger and can arrive together.
- The read is float64 and the farm float32. That can move a count by one step where a trace crosses 0.01.
- It says nothing about whether more memory would raise fitness. The smell screen r65's two forms, and round 11 of the
  literature review (Q16), bear on that.

## To repeat it

From the repository root, pinned and low:

```
cmd //c start "" //b //wait //belownormal //affinity C0000000 python -I scripts/reads/brain-memory.py --rows scratch/brain-census/work/rows.jsonl
python -I scripts/reads/brain-memory.py --hand-check runs/r64t-t-6203/2026-10-31-235754-2991f19e/genomes.jsonl.gz 64705
python -I scripts/reads/brain-memory.py --self-test
```

With no run argument it reads the populations above. They come from r64t's two true-shape worlds
(2026-10-31-235754-2991f19e) and r64s2-d-6202 (2026-10-30-233542-11de0613). Its tables go to the untracked
`scratch/brain-census/census.txt`.
