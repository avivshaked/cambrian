# 0018 — Nothing to swim towards

**2026-08-24**  ·  Milestone 4

[Entry 0017](0017-what-a-muscle-costs-to-own.md) ended with a joint being affordable and
nothing swimming anyway. The fastest creature in an embodied run was doing 0.0075 m/s,
against a survey best of 0.485 m/s for a random genome measured on its own. That is
sixty-five times slower.

There were two candidate explanations, and they need opposite responses.

1. It is morphology. The population floor spawns founders, and §5A.0b makes a founder
   deliberately minimal. If founders are bodies that cannot swim, the route to a swimmer
   runs through adding parts, each of which is priced. Cost and morphology would then be one
   problem rather than two.

2. It is something else.

They are cheap to tell apart. Run the swim survey twice, once on founders and once on the
`RandomViable` genomes the original survey used.

## The measurement

Two hundred genomes each, twenty seconds, driven by their own brains, with no gravity and no
contact. Read the median row against the best row.

| | founder | randomViable |
|---|---|---|
| mean parts | 1.51 | 5.78 |
| mean dof | 0.95 | 3.68 |
| with a joint | 50.5% | 100% |
| median m/s | 0.00005 | 0.00627 |
| 90th percentile | 0.01221 | 0.02482 |
| **best m/s** | **0.12733** | **0.48465** |
| over 1 cm/s | 11% | 35.5% |

The first hypothesis is confirmed, and the effect is large. The founder median is 125×
lower. A founder averages one and a half parts and less than one degree of freedom. That is
very nearly the definition of a body that cannot swim. The design says so in as many words:
*one part is a blob that cannot move; two is a blob with a beating appendage.*

## And that is not the answer

The founder best is 0.127 m/s. Eleven per cent of founders exceed a centimetre per second.
The floor is not seeding a population that cannot swim. It is seeding a population that
mostly cannot, with a usable tail.

So the embodied world's 0.0075 m/s is still seventeen times below what its own founders
manage in still water, and that gap has nothing left to blame. Morphology explains the 125×
in the median. It does not explain the residual.

The two speeds are not measured identically. The survey takes net displacement over twenty
seconds, and the embodied run takes displacement across one metabolic step. For an
oscillating body the short window is the more generous of the two, so the comparison
understates the gap rather than inflating it.

## What is left

Every sensor channel is unimplemented. `ISensorField` exists and every call site passes
`null`. So every brain in the world is open-loop: a function of time and of its own state,
and of nothing about the world.

An open-loop swimmer swims in whatever direction its morphology and gait dictate, fixed at
birth. It cannot tell up from down. And the only thing in this world worth crossing distance
for is light, which is a gradient in depth (§5A.2). Moving up earns more and moving down
earns less, so undirected the two cancel. §5A.2's work term bills the stroke either way.

Locomotion has negative expected value. It is not a small benefit swamped by noise. It is a
negative one.

Which means the economy is doing the right thing, and that it looks like a bug. The jointed
creatures that survive are the ones whose joints barely move. That is the seventeen times.

This is the second time in three entries that a working mechanism has failed to change the
outcome because the benefit was unreachable rather than the capability. In 0015 and 0016
every creature had the same gait, so a real cost was charged against a benefit nothing could
reach. Here every creature has its own gait, and still no gait can be aimed.

## The design already scheduled the fix, for this milestone

§4.4 declares four channels and marks all four Milestone 4.

| Channel | Reads |
|---|---|
| Chemical | nutrient concentration at this part |
| Energy | reserve, as seconds of life remaining |
| Flow | water velocity relative to this part |
| **Depth** | how deep this part is |

They were specified, put in the enum, and never wired. Depth is the one that closes this.
The argument in §4.4 for why a bare scalar is enough is worth keeping in view: no channel
reports a bearing to anything. A creature made of several parts reads the same
scalar in several places, and the difference between those readings is a direction.
Morphology becomes part of the sensory apparatus, which is also the reason a "direction to
nearest food" channel is rejected in that section rather than deferred.

So sensors are not a later nicety here. They are the precondition for locomotion having any
value at all, and the milestone they were filed under is the one we are in.

## The pattern

Entry 0013 was a guard shaped like the bug it guarded. Entry 0014 was an estimate wrong
enough to change the plan. Entry 0015 was the codebase having already written down the
answer. Entry 0016 was a mean that said the opposite of its distribution. Entry 0017 was a
closing recommendation that was wrong, where running it was the cheapest way to find out.

This one is a hypothesis confirmed decisively, and still not the answer. A 125× effect is
about as clear as a measurement gets, and it was entirely real. Had the survey reported only
medians it would have closed the question and sent the next week into founder complexity.
The best column is what said keep going. Partial confirmation is more dangerous than
refutation, because it arrives feeling like a result.
