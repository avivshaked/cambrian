# 05 — A brain that is copied with the limb

[Piece 01](01-a-graph-that-grows-a-body.md) grew a body from a graph.
[Piece 02](02-from-a-tree-of-boxes-to-something-that-moves.md) turned that body into joints
and motors. [Piece 03](03-what-it-means-to-push-against-water.md) put water around it.
[Piece 04](04-nobody-decides-who-wins.md) gave the whole thing an economy and no referee.

Something is conspicuously missing from that list. At any instant, each joint needs a
number: how hard to push, and which way. Piece 02 described the machinery that turns such a
number into torque, and was careful not to say where the number comes from.

For most of this project's life it came from a single line of test code. Every joint of
every creature in the world was driven by one shared sine wave.

This piece is about replacing that. One property of the replacement matters more than all
the others: **the controller is stored inside the body graph, so duplicating a segment
duplicates its controller too.**

## The brain that was carried and never read

The genome had a brain from the first draft. Each morph node held a list of neuron
definitions: an operator, a few inputs, a frequency, a phase, an amplitude, a bias.
Development copied them onto every part it grew. Mutation perturbed them, added them,
removed them. The JSON writer serialised them and the reader refused to load a genome
without them.

Nothing in the simulator ever read them.

There was one line in the developer, `Neurons = node.Neurons`, and past that the trail went
cold. The gap between a genome with a brain and a creature with a brain was one function,
`Phenotype + time → one float per degree of freedom`. It did not exist, so a
placeholder stood in for it.

This is worth dwelling on because of what it looked like from outside. Nothing crashed,
nothing logged a warning, and creatures moved. Genomes differed from one another in every
visible respect, and their brains mutated across generations in the saved files.

The bug was invisible in every way except one. **The controller was a constant across the
population**, and evolution cannot select for a trait that does not vary.

It showed up as a result rather than as an error. The world was exterminating its own
muscles: joints appeared, cost energy, and were gone inside a minute. The diagnosis took a
while because a uniform flap is not a *broken* gait.

It is a perfectly good gait that happens to produce no net thrust. So it was a real cost
against an unreachable benefit, and the economy of [piece 04](04-nobody-decides-who-wins.md)
did what it should. It deleted every muscle in the world.
([logbook 0015](../logbook/0015-the-world-deleted-its-own-muscles.md),
[0016](../logbook/0016-the-brain-that-was-never-read.md).)

## Why the brain lives inside the body's graph

The obvious architecture is two genomes: one describing a body, one describing a network,
and a mapping between them. It is how most controller evolution is done, and here it would
be a mistake.

Recall from [piece 01](01-a-graph-that-grows-a-body.md) what a recursive encoding buys. A
single mutation, bumping a node's recursion limit, turns one segment into four. That is the
mechanism that produces a spine, a centipede, a fin ray with repeated elements, and it is
the whole reason for the graph.

Now ask what a separate brain does when that mutation fires. Four segments appear, each with
a joint, and the controller has no entries for three of them. Whatever mapping you invent,
whether reusing the parent's, leaving them at zero or allocating fresh random ones, the
newly grown limb arrives *uncoordinated*.

The single most valuable mutation the encoding has would produce a creature worse than its
parent, every time, and get selected straight back out.

Put the neurons inside the morph node and the problem dissolves. Recursion copies the node;
the node contains its neurons; so recursion copies the controller with the segment.

Four segments means four identical local controllers wired to their neighbours. That
arrangement has a name of its own rather than being a workaround. A chain of identical
coupled oscillators is a **central pattern generator**, and it is how lampreys, leeches and
salamanders swim.

[K12 §2.2, p.3] uses the same arrangement.

> Each body part contains a local neuro-controller (an artificial neural network), as well as a
> local sensor and effector.

This is also why the input references a neuron may use are restricted to *relative* things.
A neuron may read a sensor on its own part, or another neuron in the same node. It may read
a neuron in the parent or the child node, or a constant. Until D081 (2026-09-07) it could
also read a global neuron that belonged to no part. A child is born without one now, and
genomes recorded before then still carry ones nothing steps.

There is deliberately no way to say "neuron 7 of part 12". Such a reference would be
meaningless in the copy, since there is no telling which part 12 is meant, and the
duplication semantics would collapse. The restriction looks like a limitation and is the
thing that makes the mechanism work.

One consequence shows up early: signals travel one node per step. A long body senses and
reacts more slowly than a compact one, along its own length. That is a conduction delay, it
is bounded by body size, and nobody put it there.

## What a neuron is here

A neuron here is not a weighted sum. Each one carries an **operator** chosen from a set of
twenty, in five families.

| family | operators |
|---|---|
| arithmetic | `sum`, `product`, `min`, `max` |
| comparison | `greater-than`, `if`, `interpolate` |
| waveforms | `sin`, `oscillate-wave`, `oscillate-saw` |
| transfer functions | `sigmoid`, `sum-threshold` |
| temporal | `integrate`, `differentiate`, `smooth`, `memory` |

The temporal family is what makes this a dynamical system rather than a function of time. A
neuron with `memory` holds a value across steps.

One with `integrate` accumulates, and one with `smooth` low-passes.

State therefore lives in the network. Two creatures with identical genomes, at different
points in their gait, are in different internal conditions.

The exact set is in [DESIGN §4.3](../DESIGN.md#43-brain-graph). The initial population is
restricted to a small oscillatory subset, so the first creatures are pattern generators
rather than random arithmetic.

That restriction is a property of *how founders are drawn* rather than a separate
implementation, so no code is discarded when it lifts.

## Three decisions the genome did not make

Building the evaluator meant answering three questions the genome had no field for. All
three had a plausible wrong answer.

### Every neuron reads the previous step

Neurons are updated into a second buffer, swapped at the end of the step. The alternative is
to update in place, which is faster and simpler, and which makes a neuron's value depend on
the order the parts happen to be walked in.

That is the failure mode this project has already paid for twice. What arrives is a
plausible number rather than a crash.

A creature whose gait depends on tree traversal order is a creature whose gait changes when
you reorder a loop for performance. Nothing in the world would report that as anything worse
than a slightly different swimmer.

It also makes the one-node-per-step claim above *true* rather than approximately true. A
test watches a signal injected at part 0 arrive at parts 1 and 2 on the following two steps.

### Neuron *d* drives degree of freedom *d*

The genome has no effector-mapping field, so some rule had to be invented. Two candidates:
sum all of a part's neurons into its joint, or map them positionally.

Summing is wrong for a specific reason. It makes gain depend on how many neurons a part has.
A mutation adding a neuron for an unrelated purpose, such as a sensor filter or an
intermediate term, would change how hard the creature swims. Every neuron would be an
effector whether it wanted to be or not.

Positional mapping is also the only rule that survives recursion, and that is the test
everything in this design has to pass. Neurons are copied with the node, so the mapping is
copied with them.

### `sigmoid` means `tanh`

Both are sigmoids. Only one is centred on zero.

The logistic curve is strictly positive. A joint driven through one can only ever push in
one direction, so it cannot oscillate, cannot return, and cannot produce a stroke. Every
creature whose gait happened to route through a `sigmoid` would be paralysed in a direction.

And it would never be reported as a fault. It would be reported as a bad swimmer, which is
indistinguishable from the thousands of bad swimmers a random population contains.

This is the same shape as the shared-sine bug. **The cost of a wrong choice here is a
slightly worse creature rather than an error**, and a world with no fitness function has no
number that would flinch.

## Why a sawtooth is in the starting set

Sines are the obvious basis for a gait and they are not sufficient. [C18 §4, p.30] says so
directly, about real aquatic organisms.

> swimming cycles where impulsive thrusting phases are associated with ramp down, recovering
> ones, which helps inducing non-symmetric inertial effects which result in a positive net
> thrust

Its conclusion is that *"non-harmonic actuation routines are of importance in unsteady
aquatic locomotion."*

A pure sine is symmetric in time. Its power stroke and its recovery stroke are mirror
images, and [piece 03](03-what-it-means-to-push-against-water.md) explains why symmetric
strokes in a drag-only fluid go nowhere. A sawtooth has an asymmetric duty cycle a sine
cannot express, and including it from the start costs nothing.

Whether evolution uses it is a separate question, and one the world gets to answer.

## Did it work

Directly: ten random genomes now produce ten distinct drive signals. Under the old code they
produced ten identical ones.

Embodied, against the same run under the shared sine, joints stopped being exterminated. The
jointed share survived two hundred simulated seconds instead of vanishing in sixty, and
deaths roughly halved. The energy audit still closed at zero residual, which is the check
that the new machinery did not manufacture anything.

And then the distribution said something the mean had been hiding. Population mean speed was
0.0002 m/s, which reads as *nothing swims*. Two hundred random genomes measured
individually: median 0.006 m/s, best **0.485 m/s**. A random genome, with no selection
whatsoever, crossing half a metre a second.

The mechanism works, and the two numbers disagreed about it. One said the search was
hopeless and the other said go and look.
([logbook 0016](../logbook/0016-the-brain-that-was-never-read.md).)

## What a brain with no senses cannot do

That was not the end of it, and the reason is the most interesting thing in this piece.

Every sensor channel is still unwired. The interface exists; every call site passes nothing.
So the brain is a pure central pattern generator: a function of time and of its own internal
state, and of nothing whatsoever about the world.

Such a creature can swim, and it cannot swim *anywhere in particular*. Its direction is a
property of its morphology and its gait, fixed at birth, and it has no way to perceive that
it is heading down rather than up.

Now put that creature in [piece 04](04-nobody-decides-who-wins.md)'s economy, where the only
thing worth crossing the world for is light, and light is a gradient in depth. Moving up
earns more and moving down earns less, so undirected the two cancel, and the work is billed
either way.

For an open-loop swimmer, locomotion has negative expected value. The benefit is not merely
small. It is below zero.

So the economy does the correct thing again, and again it looks like a bug. It keeps the
jointed creatures whose joints barely move.

In an embodied run the fastest creature alive was around seventeen times slower than the
same population's own founders manage, when those are measured individually in still water.
The founders can swim perfectly well. The ones in the world that did swim were paying for it
and getting nothing.

Two mechanisms have now been built to make swimming possible, both worked, and the world
still says no. The first time the benefit was unreachable because every creature had the
same gait. This time it is unreachable because no gait can be *aimed*.

The design already says what closes this, and files it under this same milestone. Four
sensor channels are specified in [DESIGN §4.4](../DESIGN.md#44-sensors-and-effectors) and
none is implemented: chemical, energy, flow and **depth**.

Depth is the one that matters here, and the section's reasoning about it generalises the
whole approach. **No channel reports a bearing to anything**. A sensor is a scalar at a
part, and a creature made of several parts reads the same scalar at several places at once.
The difference between those readings is a direction.

That makes morphology part of the sensory apparatus. A long creature resolves a gradient
better than a compact one, and a bilaterally symmetric one can compare left against right.
It is also how chemotaxis works, and bacteria have been doing it for a very long time
without anything resembling a bearing sensor.

*(That locomotion is negative-expected-value while open-loop is the author's inference from the
economy's structure, supported by the measurement above; no source in the corpus makes the
argument.)*

## Sources

| Key | Used here for |
|---|---|
| `[K12]` | Per-part local neuro-controllers; oscillatory transfer functions to speed the discovery of swimming |
| `[C18]` | Non-harmonic actuation mattering in unsteady aquatic locomotion, and why a pure sine is a real limitation |
| `[EA23]` | Morphology and control co-evolving rather than being optimised separately |

Four things here are mine rather than the literature's.

- **The argument that a separate brain genome breaks the recursion mutation** is the
  project's
  own. [K12] and [EA23] both couple body and brain, but neither states this specific
  failure. The reasoning is recorded in [`DECISIONS.md` D030](../DECISIONS.md#d030).

- **All three of the decisions the genome did not make** are mine: synchronous update,
  positional effector mapping, and `sigmoid` read as `tanh`. No source in the corpus
  specifies any of them, and the arguments are given here and in
  [DESIGN §4.3](../DESIGN.md#43-brain-graph) so they can be checked.

- **Locomotion having negative expected value for an open-loop swimmer in a depth-graded
  economy** is the author's inference, marked as such above.

- **The conduction-delay reading of one-node-per-step latency** is ours. The latency is a
  consequence of synchronous update, and calling it a feature is an interpretation.

## Where it is

| file | what it holds |
|---|---|
| [`Brain.cs`](../src/Evosim.Core/Brain/Brain.cs) | the evaluator: built once per creature, stepped once per physics step |
| [`NeuronOp.cs`](../src/Evosim.Core/Genome/NeuronOp.cs) | the twenty operators, and the subset founders are drawn from |
| [`NeuronDef.cs`](../src/Evosim.Core/Genome/NeuronDef.cs) | what the genome stores |
| [`NeuronInput.cs`](../src/Evosim.Core/Genome/NeuronInput.cs) | the input kinds that survive recursion |
| [`EffectorDriver.cs`](../unity/Assets/Evosim/Sim/EffectorDriver.cs) | the other half of piece 02: drive value to torque |
| [`BrainTests.cs`](../src/Evosim.Core.Tests/BrainTests.cs) | distinct signals, one-node-per-step latency, and that `sigmoid` is centred |
| [`SwimSurvey.cs`](../unity/Assets/Evosim/Sim/Editor/SwimSurvey.cs) | the distribution the mean was hiding |
| [`DESIGN.md`](../DESIGN.md) §4.3, §4.4 | the brain, and the sensors: four channels read as of [`DECISIONS.md` D033](../DECISIONS.md#d033), with `Chemical`, `Energy` and `Flow` still unimplemented |
