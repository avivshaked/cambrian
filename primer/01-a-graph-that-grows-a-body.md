# 01 — A graph that grows a body

Suppose you want creatures nobody designed. Not creatures assembled from a menu of parts a
human authored, but shapes that arrive unplanned, that surprise the person who wrote the
program. Then something has to *generate* bodies, and mutation has to be able to change a
body in ways that are usually survivable and occasionally interesting.

The obvious approach is a list. A creature is an array of parts: this box here, that box
there, a joint between them. Mutation nudges a number, adds a part, deletes a part. It is
easy to write, easy to serialise, easy to reason about.

It also produces junk, and it keeps producing junk for a reason worth understanding: a list
has no way to say *"the same thing again."*

## Why "the same thing again" is the whole problem

Look at almost any animal. A spine is a segment repeated. A centipede is one body plan run
many times. Your hand is one finger design instantiated four times plus a modified fifth.
Ribs, vertebrae, teeth, feathers, leaves — biology is drowning in repetition, and the
repetition is not coincidence. It is what makes a large body buildable from a small
description.

Now try to evolve that with a list. To lengthen a spine from five segments to six, you must
add a part, position it correctly relative to the fifth, size it consistently, give it a
joint with limits like its neighbours', and wire it a controller resembling theirs. That is
five or six coordinated changes that are only useful *together*. Mutation makes one change at
a time. The odds of assembling that combination by chance are terrible, and every partial
attempt is worse than doing nothing — so selection removes them.

A list can represent a segmented animal perfectly well. It just cannot *reach* one.

## The inversion

The genome here is not a description of a creature. It is a **recipe for growing one**.

```
MorphNode                       MorphEdge
  dimensions                      child          -> which node to attach
  jointType                       parentAnchor   -> where on me
  jointLimits                     childAnchor    -> where on you
  recursiveLimit                  orientation
  neurons                         scale
                                  reflect
                                  terminalOnly
```

A node is not a body part. It is a *kind* of body part. An edge is not a connection between
two parts; it is an instruction: *"attach one of those to one of these, like so."* The
creature you see is what happens when you follow the instructions — a process called
**development**, and in this project it is [`Developer.Develop`](../src/Evosim.Core/Development/Developer.cs).

The graph is small. The creature can be much larger. That gap is where everything
interesting lives.

## Cycles: repetition for the price of one node

The morphology graph is allowed to contain cycles, including a node with an edge to itself.
Development walks the graph depth-first, and each node carries a `recursiveLimit` — how many
times it may appear along a single path before its recursion is spent.

One node. One self-edge. `recursiveLimit = 5`. You get a five-segment spine:

```csharp
[Theory]
[InlineData(1, 1)]
[InlineData(2, 2)]
[InlineData(3, 3)]
[InlineData(8, 8)]
public void RecursiveLimitControlsSegmentCount(int limit, int expectedParts)
```

*— [DevelopmentTests.cs](../src/Evosim.Core.Tests/DevelopmentTests.cs)*

Now the earlier problem dissolves. Lengthening the spine is **one mutation**: increment an
integer. Not five coordinated changes — one, and the new segment is automatically the right
shape, with the right joint, in the right place, because it is the same node again.

And the reverse is more important. Change that node's `dimensions`, and *every* segment
changes together. The creature stays coherent. Under a list encoding, the equivalent mutation
alters one segment and leaves the other four alone, producing a lumpy thing that is worse
than both its parents. Here the body plan mutates *as a body plan*.

This is what "indirect encoding" means, and why [`DESIGN.md`](../DESIGN.md) §4.1 is emphatic
about the term. A direct encoding maps genome entries to body parts one-to-one. An indirect
one runs a program. The genome is the program; the creature is its output. A survey of
encodings for evolved creatures classifies Sims as *"an indirect representation that supports
recursive structures"* [L21 §4.2, p.8] — a point draft 2 of the design got backwards, with
knock-on consequences.

## Transforms compound, so limbs taper

Each edge carries a `scale`, and — this is the part that does the work — it applies **to the
entire subtree**, not just the immediate child. The implementation this design follows is
explicit that such transforms *"are applied to the entire subtree of the phenotype graph
during its construction"* [K12 §2.1, p.3].

Put `scale = 0.5` on a recursive edge and you do not get one smaller segment. You get every
subsequent segment smaller than the last:

```
part 0: half-extent 0.5
part 1: half-extent 0.25
part 2: half-extent 0.125
part 3: half-extent 0.0625
```

A tapering tail, from one number. The same mechanism, applied to rotation, curls a chain into
a spiral; applied at a branch, shrinks a whole limb and everything growing off it.

This is also where the encoding will happily hurt you. Multiplicative scale down a long chain
reaches sub-millimetre boxes in a handful of steps, and a physics solver handed a
sub-millimetre box does not report an error — it reports *plausible nonsense*, jitter that
looks like behaviour. Development therefore refuses to grow a part below a minimum volume,
because *"extremely small body parts cause instability in the physical engine"*
[K12 §2.3, p.7]. That guard rail is not fussiness; it exists because the alternative failure
is silent.

## One bit of bilateral symmetry

Each edge has three reflection flags, one per axis: *"if one, two or three reflection flags
are enabled, two, four or eight mirrored copies of a child node are created in the phenotype
graph"* [K12 §2.1, p.3]. Enable one, and the child is instantiated **twice** — once as given,
once mirrored. Enable two, and you get four copies. Three gives eight.

That is the entire mechanism for bilateral symmetry, and it costs one bit.

Consider what it means for search. A creature with a limb on the left is one mutation away
from a creature with matched limbs on both sides — and the pair arrives *already coordinated*,
because both copies are the same node, sharing dimensions, joint, limits and controller.
Under a list encoding you would have to evolve the second limb from scratch and hope it landed
symmetrically. It never does.

Symmetry matters for a reason beyond search efficiency, and it is worth saying plainly because
it is the project's actual goal: **symmetric things read as organisms, and asymmetric things
read as debris.** The figure of evolved creatures in [K12 Fig. 1, p.4] is captioned *"Several
evolved robots exhibit symmetry (1a, 1b, 1d) and segmentation (1c)"* — three of four. That
is not decoration; it is a large part of why those images are recognisable as creatures at
all.

<sub>The claim that a viewer *judges* organism-versus-debris largely on symmetry is my own
reading, not a finding from the reviewed literature. It is the reason reflection was widened
from one flag to three, so it is worth flagging as an assumption rather than smuggling in as
fact.</sub>

### A reflection has to be about the right axis

There is a trap here that is easy to walk into and hard to see afterwards.

Mirroring moves a point only if the point has a component on the mirrored axis. A child
attached to the parent's **+Y** face sits at roughly `(0, d, 0)`. Mirror that about **X** and
you get `(0, d, 0)` — the same place. The "mirrored copy" is exactly coincident with the
original: two boxes occupying one volume.

Attach along X and mirror about X, and the copies land at `+d` and `−d`. That is a bilateral
pair.

So reflection is only meaningful about the axis the child is attached along. What makes this
worth calling out is how it fails: the creature still has the expected number of parts, all in
plausible positions, and it moves. It looks like a working creature that simply is not very
symmetric. Nothing announces that half its parts are inside the other half.

The first random-genome generator here picked reflection axes independently of the attachment
axis, and **69.7%** of creatures had a part buried inside another. Fixing the axis and bounding
edge tilt brought that to 55.5%; the rest needed a check on the developed creature, because no
rule about single edges can see it — a node with edges on opposite faces places a child exactly
where its own parent already is, and you only find that out by growing it.

## Recursion that runs out, so chains can have ends

A repeated segment is good. A repeated segment that ends in something *different* is much
better — a tail with a fluke, an arm with a hand, a leg with a foot.

Edges can be marked `terminalOnly`. Such an edge is ignored while the node still has recursion
left, and fires only once recursion is spent. So a chain grows: segment, segment, segment,
segment — and then, at the tip and only at the tip, a fin.

```csharp
Assert.Equal(4, p.Parts.Count(x => x.SourceNode == 0));  // four segments
Assert.Equal(1, p.Parts.Count(x => x.SourceNode == 1));  // one fin, at the end
```

One flag buys the distinction between a repeating unit and a terminated structure. Without it,
every segment gets a fin, or none does.

## The part that makes them look alive

Everything above is morphology. Here is the piece that makes the encoding more than a clever
way to draw shapes.

**Neurons live inside morph nodes.** Not in a separate brain graph bolted on afterwards —
inside the node, alongside its dimensions and its joint. Which means recursion copies them.
The reference implementation uses the same arrangement: *"Each body part contains a local
neuro-controller (an artificial neural network), as well as a local sensor and effector"*
[K12 §2.2, p.3].

Grow a five-segment spine from one node, and you have not just made five body segments. You
have made five copies of that segment's controller, one per segment, each wired to its own
joint.

A chain of identical local oscillators, each driving its own segment, coupled through the
physics of being attached to each other — that is, structurally, a **central pattern
generator**, which is how [`DESIGN.md`](../DESIGN.md) §4.3 describes it and why the
arrangement was chosen.

Nobody designed that into this system. It falls out of the decision to put the neurons inside
the node.

Which suggests an answer to a question the literature has not settled — *"Why are Sims' 1994
results hard to reproduce? What are the necessary ingredients?"* is question 2 of
[this project's review](../research/LITERATURE-REVIEW.md), still marked only partly answered.
If a travelling wave down a segmented body is what a chain of coupled oscillators does
anyway, then an implementation that separates brain from body — however sophisticated the
brain — has thrown away the mechanism that made the original work, and will need to
rediscover coordination the hard way.

<sub>That inference is mine, not a result from the reviewed papers. It is offered as a
hypothesis worth testing, and testing it is one of the things this project is for. Central
pattern generators in real nervous systems are well-established neuroscience, but no source
in this project's bibliography covers them, so nothing here should be read as evidence about
biology.</sub>

Get the segment right once and evolution gets the coordination free — across all five copies,
in the same mutation.

## What this buys, stated plainly

| | List of parts | Recursive graph |
|---|---|---|
| Lengthen a spine | ~6 coordinated mutations | increment one integer |
| Change every segment together | impossible in one step | change one node |
| Bilateral symmetry | evolve twice, hope | one flag |
| Genome size vs body size | proportional | decoupled |
| Coordinated multi-limb gait | must be discovered | structurally implied |

None of this makes good creatures appear. It makes good creatures *reachable* — which is the
only thing an encoding can do, and the thing that decides whether a search is worth running.

## Sources

Claims here cite `[KEY §section, p.N]`, where `p.N` is the PDF page — the same convention as
[`DESIGN.md`](../DESIGN.md), whose §13 resolves every key. The papers are not in the
repository; [`research/FETCH-RESULTS.md`](../research/FETCH-RESULTS.md) records where each
came from.

| Key | Used here for |
|---|---|
| `[K12]` | Reflection flags and their 2/4/8 copies; cumulative subtree transforms; terminal edges; minimum part volume; neurons living inside body parts; the symmetry figure |
| `[L21]` | Classifying Sims' encoding as indirect |
| `[S94]` | Sims 1994, the origin of the whole approach |

**Two claims in this piece are mine and are not sourced**, marked where they appear: that a
viewer's organism-versus-debris judgement rests largely on symmetry, and that separating
brain from body is why Sims reproductions struggle. Both are the kind of plausible reasoning
that has already been wrong once in this project — a confident bridging sentence with no
citation was the single largest error the literature review caught
([logbook/0001](../logbook/0001-the-design-was-wrong-three-times.md)). They are stated as
hypotheses because that is what they are.

## Where it is

- Genome types — [`src/Evosim.Core/Genome/`](../src/Evosim.Core/Genome/)
- Development — [`Developer.cs`](../src/Evosim.Core/Development/Developer.cs)
- The tests that pin all of the above — [`DevelopmentTests.cs`](../src/Evosim.Core.Tests/DevelopmentTests.cs)
- Specification, with citations and exact caps — [`DESIGN.md`](../DESIGN.md) §4

Development produces a `Phenotype`: a tree of boxes with joints, positions and orientations,
and no physics engine anywhere near it. Turning that into something that can actually move is
a separate problem, and it is where the geometry is easy to get quietly wrong — the next
piece.
