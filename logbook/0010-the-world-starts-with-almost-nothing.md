# 0010 — The world starts with almost nothing

**2026-08-07**  ·  Milestone 2

A session that began by looking at the simulator and ended by rewriting what a world starts
with. Almost none of it was planned, and the useful parts came from being told the thing on
screen could not be inspected.

## The instrument was not an instrument

The plan was: open the sandbox, look at the creatures, answer four questions about jamming and
overlap. What came back instead was three answers about the viewer.

> *"how would i know? it doesn't say which seed is being used"*
> *"the camera is not very close to the creatures, and I can't move the camera"*
> *"i don't know how to use the engine"*

All three were correct, and the second was worse than reported. The seed field in Unity's
Inspector genuinely did nothing: `Spawn()` was only ever called from the cycle timer, so setting
the cycle to zero — which the instructions said to do, in order to hold on one creature —
disabled the only code path that could ever act on a changed seed. And when it did fire, it
incremented before spawning, so typing `8` produced creature 9.

**There was no sequence of actions that would have shown a chosen creature.** The instructions
were not merely unhelpful; the operation did not exist.

What that produced was a session where the questions worth asking could not be answered by the
person looking. Is *this* the jammed one? Are those two parts really intersecting? A simulator
you cannot aim does not produce findings, it produces impressions — and every number in
[0008](0008-the-energy-audit.md) and [0009](0009-when-a-part-stopped-being-a-box.md) exists
because somebody could aim something at something.

The fix was a day's work that should have been done before the invitation to look: seed you
type is the seed you get, keyboard for next/previous/hold, an on-screen readout, and a camera
that orbits and zooms from the game view rather than requiring a developer tool.

## The bug that was found by attaching one thing to another

Founders attach a link to a cell along the Y axis. That single line of new code produced buried
parts on the first run.

`CapsuleShape.SurfacePoint` derived its outward direction from the anchor's *radial* component.
A pole anchor `(0, ±1, 0)` has no radial component, so the code normalised a zero vector, hit
the fallback, and returned `+X`. A capsule's pole was reported as **a point on the side of its
end cap** — a perfectly legitimate surface point, one radius from the correct one.

Capsules are a quarter of all parts. Every capsule child anchored along Y in every population
since shapes landed had been attaching a radius off-axis, and sitting inside its parent.

The shape tests written the same week did not catch it, and the reason is worth keeping. They
asserted the returned point was *on the surface*:

```csharp
Assert.True(shape.ContainsPoint(p * 0.98f, h));   // just inside
Assert.False(shape.ContainsPoint(p * 1.05f, h));  // just outside
```

Both hold for the wrong point. A test that checks a value is *valid* cannot catch a value that
is valid and wrong. It now also asserts the point lies in the direction that was asked for, and
tests all six axes rather than three — the failure was on a negative one, and three anchors had
missed it.

That is the same lesson as 0009's, arriving through a different door: the capsule's panel-area
bug was caught by comparing against an independently-derived expectation, and this one survived
precisely because nothing independent was compared.

## What generation zero should be

The design's founder population was two to five graph nodes developing into three to sixteen
parts, with branching, recursion, bilateral pairs and several joints. That was correct under a
fitness function — you cannot grade displacement on the first evaluation if nothing can move.

§5A removed the fitness function two sessions ago and nobody revisited it. The consequence, once
stated, is bad: **a founder population whose body plans we designed makes every later claim
about morphology a claim about our initial conditions.** Bilateral symmetry appearing in the
archive is not evidence that bilateral symmetry pays, if bilateral symmetry was there at t=0.

Founders are now one earning cell, or one cell and a link. The proposal arrived as "1 or 3
cells" — one blob, or two cells with a link *between* them — and the implementation corrected
it: a link is a full part with its own tissue and needs no child, so one hanging off one cell is
a **flagellate**, and two is the smallest thing here that can swim.

Measured over 500 seeds: 52% blobs, 48% flagellates, and 52.8 / 24.6 / 22.6 across
photosynthetic, absorptive and consumer. A single founder node reaches 32 nodes and 16-part
bodies within 2,000 births under mutation alone — which was the check that mattered, because if
complexity were not reachable from a blob the whole idea would be a slower way to get nothing.

**The half that cannot eat is the point, and that was the good idea of the session.** At t=0
there is no nutrient and no carrion, so absorptive and consumer founders earn nothing and die.
Their tissue is the first nutrient the world has ever had. The doomed half of generation zero
*is* the primordial soup, and it is what makes the other two strategies mean anything by
generation two. Nobody designed that; it fell out of refusing to filter founders for viability.

## Two absences nobody had noticed

Raised in the same conversation, both structural, both invisible until named.

**There is no brain.** Neurons live on parts, plus a `GlobalBrain` array owned by no part at
all — the one cost in the whole energy economy attached to no tissue. Joules spent, nothing to
bite, nowhere to be. Which means brain size and placement cannot evolve, because there is
nowhere for a brain to *be*, and cephalization — among the most universal patterns in animal
evolution — was unreachable by construction.

The fix is §5A.1's own argument turned on cognition: if energy acquisition is a property of a
part, and that is what makes trophic strategy morphological, then thinking should be too.
`Neural` is now a cell type. Every cell keeps a baseline of neurons — a nerve net — and neural
tissue **discounts** rather than gates, because gating would couple genome validity to part
size, and under extinction-by-shrinking parts change size constantly.

**There is no smell.** §5A.1 says an absorptive cell *"rewards being where food is."* Nothing
could sense where food was. Absorptive feeding was not a strategy but a lottery: intake depended
on what a creature drifted into, and no degree of control could improve it. The section
described a behaviour the sensor set made impossible, and had done since it was written.

Five of the six declared channels report the creature's own body. Added `Chemical`, `Energy`
and `Flow` — and rejected any channel reporting a *bearing* to anything, because a scalar sensor
on a part is already a gradient sensor for a body of several parts. Two cells at opposite ends
read different concentrations and the difference is a direction. That makes **morphology part of
the sensory apparatus**, which is both more interesting and free.

## What it cost

Nothing ran differently at the end of the day than at the start — there is still no energy, no
light, no nutrients and no brain evaluation. What changed is that three things which were
impossible are now merely unbuilt, and one instrument works.

The pattern across 0008, 0009 and this entry has stopped being about measurement and started
being about *whether anyone could have looked*. The energy audit found a second unchosen drag
model. 0009 found a survey that would have quietly stopped measuring half its population. This
one found a design section describing a strategy its own sensor set could not support, and a
viewer that could not be pointed at anything.

None of those are bugs in the ordinary sense. All four were **things working exactly as
written**, where what was written had never been checked against what it claimed.
