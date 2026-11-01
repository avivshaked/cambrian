# The brains hold a hundredth of a second, and a forager needs seconds

*2026-11-01, written by the writing lane the day the question was asked. Its sources are the brain-memory census in `logbook/specs/brain-memory-census/`, round 11 of the literature review (Q16) and the coordinator's account in HANDOFF at `71abc24e`. Every figure is measured unless it says otherwise.*

On 1 November the owner asked whether the creatures are reactive machines with no inner state. The owner wanted to
know whether they can learn or remember, and whether they pass anything on but their genes. The answer the day gave is
close to yes. A census of evolved brains found that the typical grazer carries a sense for one step of its brain, a
hundredth of a second. The longest memory in thousands of brains was a third of a second. The literature puts the
window a forager needs to follow a smell at about one to five seconds. So the brains lack it by two orders of
magnitude, and the open question is whether that costs them food.

## What a brain here can carry

Each creature has one brain (D146), a network of units stepped every 0.01 s. At each step a unit adds up its bias, its
senses at that step and the other units' values from the step before, and squashes the sum with a tanh. A link from
one unit to another, or back to itself, is the only thing that carries anything from one step to the next. There is no
time constant, no learning rule and nothing handed to a child but the genome and a share of its parent's reserve.
Founders are wired straight through from senses to outputs. Mutation may add links that loop back.

So a creature can remember only as much as evolution builds out of loops. The question was how much it has built.

## The census

A Sonnet subagent wrote the census on 1 November, and the coordinator checked it the same day
(`logbook/specs/brain-memory-census/README.md`). Its script copies the brain's rule in Python. It settles each brain
with every sense at 0 and gives one sense a pulse of 1 for a single step. Then it counts the steps until every unit is
back within 0.01 of where it would have been without the pulse. A brain's memory is its longest count over its senses.
A brain that the pulse moved to a new resting state would be a latch, holding a bit for good.

The census read the brains grazers were born with, in r64t's two true-shape worlds and in the long run of round 64's
arm D. A grazer is a body born with a mouth. The table gives each population's grazers and the share whose brain has
any link between units or any loop. Then it gives the memory at the median, the ninetieth percentile and the longest.

| Population | Grazers | With a unit link | With a loop | Memory, median / p90 / longest |
|---|---|---|---|---|
| r64t-t-6202 at 6,000 s | 10 | 50% | 0% | 0.01 / 0.01 / 0.01 s |
| r64t-t-6202 at 12,000 s | 25 | 40% | 0% | 0 / 0.01 / 0.01 s |
| r64t-t-6202 at 24,000 s | 818 | 63% | 27% | 0.01 / 0.02 / 0.07 s |
| r64t-t-6203 at 6,000 s | 13 | 85% | 0% | 0.01 / 0.01 / 0.01 s |
| r64t-t-6203 at 12,000 s | 459 | 56% | 3.5% | 0.01 / 0.01 / 0.33 s |
| r64t-t-6203 at 24,000 s | 2,155 | 67% | 7.4% | 0.01 / 0.02 / 0.05 s |
| r64s2-d-6202 at 46,500 s | 2,207 | 92% | 23% | 0.02 / 0.03 / 0.15 s |
| Founders drawn from the pool, 6202 / 6203 | 305 / 271 | 62% / 58% | 0% | 0.01 / 0.01 / 0.01 s |

No brain latched, and no self-link had a weight above 1. The longest memory, 0.33 s, belonged to one grazer of the
6203 world at 12,000 s, through one hidden unit linked to itself. Bodies without a mouth hardly use links at all:
under 4% carry one, and none holds a sense for more than 0.05 s.

Evolution does add loops. Among grazers in the 6202 world they went from none at 6,000 s to 27% by 24,000 s, and the
longest-run world has the most hidden units. But the memory stayed at one to a few steps. The median grazer's trace is
one step: a sense reaches a hidden unit, and that unit reaches an output on the next step.

The census names its own limits. It measures from rest with every sense at 0. A brain driven by live senses sits
further along the curve, where the loop's gain is usually lower, so live memory is probably no longer than this (the
census's inference). It also says nothing about whether more memory would bring in more food.

What persists, then, lives in the body: its posture, its speed and its place, which its senses read again at every
step (the census's inference). A grazer that loses touch with a leaf keeps no trace of it beyond a few hundredths of a
second.

## The window a forager needs

The owner asked for round 11 of the literature review the same day, to ask what memory and learning within a life buy
an evolved forager (Q16). Its numbers come from other animals at their own scales.

| Source | What it compares |
|---|---|
| A bacterium [SBB86] | the last second of smell against the three seconds before |
| Walking flies [AS18] | a 0.62 s window against a 4.84 s one, adapting over about 10 s |
| A model of a lobster [MIC20] | odour encounters over the previous 5 s |
| Search in a turbulent plume [BS02] | where odour was last met, and how long ago |

The review reads this, as inference, as a window of about one to five seconds, or 100 to 500 steps of this brain. It
scales with a creature's speed and the size of the field, which no source set for these creatures. Evolved networks in
the literature did hold state for a whole trial, but as a latch that holds one bit. The latch was a loop that excites
itself, or a unit that settles near 0 or 1. No source tested a graded trace held without a time constant.

## Why a self-link rarely holds it

A unit linked to itself keeps a share of a disturbance at each step, its gain. The census works the example. A pulse
of about 0.5 stays above its 0.01 line for 4 s only if the gain is about 0.99 a step. At 0.95 the unit forgets in
under a second, and above 1 it latches and stops tracking anything. So the trace a forager needs lives in a band of
gain within about 0.01 of 1.

That band is narrow for a mutation of one weight to hit. It also moves, since the gain depends on where the unit
rests, which its bias and live senses set. Evolution found 0.33 s once in these populations. The census and the review
both read that as a narrow target, and that reading is inference. No study compared one controller with and without a
time constant on a memory task.

## What stays open

The project has three inputs on whether this matters, and the third is still to come. The census says what the brains
hold. Round 11 says what a forager needs and, as inference, that a heritable time constant per unit is a cheaper
addition than learning. A time constant is one gene read on a scale of seconds. Learning paid off in the papers only
where a fact had to be kept across many trials, and it has costs of its own.

The smell screen r65 gives the third input. Its arm R reads the carried smell's strength only, and its arm O reads
strength and direction (D270). A body that reads only strength has to compare it over time, or across its body, to
find the way. So the gap between R and O bears on memory. It is muddied, because every part smells, and a body can
compare its two ends at one instant without remembering anything.

If the three agree, the coordinator puts a time constant per unit to the owner. That would be a world rule, the
owner's to rule, and by D262 it would enter overpowered and be dialled back. Until then the creatures stay what the
owner suspected: they react to the moment, and remember it for a hundredth of a second.

There is no picture with this entry. What it measures is inside the brain, and the theatre does not draw one.

## Sources

| File | What it holds |
|---|---|
| [`brain-memory-census/README.md`](specs/brain-memory-census/README.md) | the measure, the populations, the table, its limits and how to repeat it |
| [`research/LITERATURE-REVIEW.md`](../research/LITERATURE-REVIEW.md), round 11 and Q16 | the windows, the latch evidence, plasticity against fixed loops, and the gap |
| [`DECISIONS.md`](../DECISIONS.md) D146, D262, D270 | the one brain, new features entering overpowered, and the carried smell's two forms |
| [`r65-prereg/predictions.md`](specs/r65-prereg/predictions.md) | the smell screen whose R against O is the third input |
| `src/Evosim.Core/Brain/Brain.cs` | the brain's step rule |
