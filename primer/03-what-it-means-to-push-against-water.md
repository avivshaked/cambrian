# 03 — What it means to push against water

A creature with joints and a controller still cannot swim, because until now there has been
nothing to swim in. [Piece 02](02-from-a-tree-of-boxes-to-something-that-moves.md) ended with
bodies that move — joints turning, parts flailing — and no way for any of that to become
travel.

Water is what turns motion into locomotion. It is also, in a simulator, the single easiest
thing to get subtly and expensively wrong.

## The whole trick is asymmetry

Consider why waving your hand back and forth in air gets you nowhere, but the same motion in
a pool moves you a little.

It is not that water is thicker. It is that you can make the two halves of the stroke cost
different amounts. Push with your palm broadside and you shift a lot of water. Slice back
edge-on and you shift almost none. Same limb, same rhythm, different orientation — and the
difference between the two strokes is your net thrust.

Every swimming gait that exists is a variation on that idea. A fish's tail is stiff on the
power stroke and compliant on the return. An oar feathers. A jellyfish contracts fast and
refills slowly.

Which means the fluid model has exactly one job it cannot fail at: **a surface moving
broadside must generate much more force than the same surface moving edge-on.** Get that
wrong and no rhythm produces net travel, no matter how good the controller is. Everything
oscillates in place forever.

[`DESIGN.md`](../DESIGN.md) §5.2 puts it more bluntly than a specification usually puts
anything: *"Three lines of code; decides whether the project works."*

## Quadratic drag, per panel

The model is the standard one, applied to each small patch of a part's surface:

```
F = -½ · ρ · Cd · A · v⊥²  ... along the patch normal
```

Density, a drag coefficient, the patch's area, and the square of the speed at which that patch
is moving *along its own normal*. Only patches advancing into the water contribute — pressure
drag acts on leading surfaces, and counting trailing ones would both double the force and
cancel the asymmetry the whole thing depends on.

Summing over the surface rather than using one whole-body number is what makes the
broadside/edge-on difference automatic. A thin plate presents a large area along one axis and
a sliver along the others; the same formula gives ten times the force in one direction as the
other, without anything special being written for it.

[C18 §2.2, p.5] arrived at the same scheme independently — a *"simplified mesh-based
quadratic drag model"* projecting each facet's speed along its normal, with `Cd = 1.5`. That
is reassuring about the approach, and it also means everything that paper reports about the
model's limitations applies here directly, which turns out to matter a great deal. More
below.

## Sampling a face once is not enough

Here is a trap that looked like a working implementation.

If you evaluate each face at its centre, you get correct forces for a box that is
*translating* — and exactly **zero** drag for a box *spinning about one of its own axes*.

The reason is quick to see once stated: when a box rotates about an axis through its centre,
each face centre moves perpendicular to that face's own normal. Normal velocity zero, force
zero, on every face at once.

Now recall what a limb does. A limb attached to a joint rotates. That is the entire motion. A
paddle is a surface turning about its root, and under centre-sampling most of its interaction
with the water simply does not exist.

The fix is to integrate over the surface instead of sampling a point — divide it into panels
and sum. Points away from the rotation axis *do* have normal-direction velocity, and they
produce the torque that resists a spinning limb. Two panels per axis, so four per face and
twenty-four per box, is the cheapest version that works.

The test that pins it keeps the broken behaviour on record rather than as folklore:

```csharp
// One panel per face is the degenerate case, kept as evidence rather than folklore.
var singleSample = new FluidConfig { PanelsPerAxis = 1 };
FluidModel.BoxDrag(Plate, Quat.Identity, Float3.Zero, spin, singleSample, out _, out Float3 blind);
Fixtures.AssertClose(Float3.Zero, blind, 1e-4f);
```

## The shape of a part decides what it can do

Parts are boxes, spheres or capsules. That sounds like variety for its own sake, and it is not:
once drag is summed over a surface, the shape of that surface *is* the set of strokes available
to the creature.

Measure it. Take one set of half-extents — flat, 1.2 m across and 0.1 m thick — and push each
shape through water broadside, then edge-on:

| shape | broadside | edge-on | ratio |
|---|---|---|---|
| box | 1080 N | 90 N | **12×** |
| capsule | 455 N | 344.9 N | 1.32× |
| sphere | 205.8 N | 203.6 N | 1.01× |

The box is the paddle. It is the only one of the three that can be *flat* — a sphere's radius
is one number and a capsule is round in cross-section — and flatness is where a factor of
twelve comes from.

And the sphere is the interesting entry, because 1.01× is not an approximation of a small
effect. It is the statement that **a sphere cannot paddle at all.** Every orientation presents
the same area, so there is no fast stroke and slow return to be had from turning it; sweeping
one sideways produces drag along its path and nothing perpendicular to it. Whatever a sphere is
for — a head, a float, a body that carries other parts — it is not for thrust.

That is worth stating out loud because it is a prediction the model makes rather than a
behaviour anyone wrote. Nothing in the code says "spheres do not swim". It falls out of
summing pressure over a surface, in the same way the broadside/edge-on difference does. A test
asserts it, so if it ever stops being true, something upstream has broken.

The capsule sits in between and is the useful middle: round in cross-section like a sphere, but
elongated, so it has exactly one axis of anisotropy. Broadside it is a long side; end-on it is
a small circle. That is a limb.

None of which prejudges what evolution should build out of them. It only means the three shapes
are genuinely different offers, rather than three ways of drawing the same part.

## The check that matters more than any of this

Drag can never do work on a body. It removes energy; it does not add it. So for any
velocity, any spin, any orientation, any box:

```
F · v  +  T · ω  ≤  0
```

That is asserted over two thousand random states. It is not a plausibility check or a tuned
threshold — it is a physical law, and it cannot be satisfied by an impressive-looking failure.

The reason to be this careful is specific. An evolutionary search does not evaluate your
fluid model on whether it looks right; it searches for the states where it is wrong. If any
combination of motion and orientation lets drag deliver energy, that combination *is* the
optimum, and every creature in the archive will converge on it.

This is not hypothetical. [U07 §3, p.5] compared a per-part reaction-force model — the family
this one belongs to — against real hydrodynamics on an evolved creature, and found the two
disagreed about its **direction of travel**. A sign flip, not a magnitude error. And
[U07 §2, p.3] notes the gait the search had found was *"unusual, and we have never observed
such motion in natural phenomena."* The search had not discovered swimming. It had discovered
the model's error and exploited it, in published work, in this exact environment.

## Added mass, and why a cheap model is worse than inaccurate

When you accelerate through water you do not only accelerate yourself. You accelerate a slug
of water around you. That extra effective inertia is called **added mass**, and for a
neutrally buoyant creature it roughly doubles the mass being accelerated.

Leave it out and drag alone still produces creatures that swim, plausibly, on screen. So it
looks like a refinement — the sort of accuracy improvement you schedule for later and feel
mildly guilty about.

[C18 §4, p.28] is the reason that reasoning is wrong, and it is the single most useful
sentence the literature contributed to this project:

> "the approximations adopted prevent the model from capturing the dynamics associated with
> vortex formation, thus **precluding the evolution of fish-like creatures**... Having
> neglected added-mass contributions, pulsed-jetting modes cannot be successfully predicted,
> thus **overlooking squid-like creatures**. The outcome consists of organisms vaguely
> resembling medusoids and **morphologically similar among themselves**."

A cheap fluid model does not merely produce fictitious physics. **It collapses the
morphological variety of what evolves.** No fish, because the dynamics that reward a fish
body are absent. No squid, because jetting cannot pay off without added mass. What is left is
a gallery of similar blobs — and since the entire point here is creatures that are
interesting to look at, the cheap model fails hardest at the goal it appeared safe for.

That inversion is worth sitting with. The usual assumption is that physical accuracy is the
scientist's concern and visual appeal is the artist's, so a simplified model trades the first
for cheapness while leaving the second alone. It does not. In an evolutionary system, the
physics *is* the space of available strategies, and a smaller space of strategies means a
smaller space of bodies worth having.

### How it is applied

Added mass is a force proportional to a body's own acceleration, which makes it an implicit
term — integrate it explicitly and it feeds back on itself, and the simulation diverges
exactly when the added mass approaches the real mass, which for a neutrally buoyant creature
is precisely where it sits.

So it is folded into mass instead: `m_effective = m + Ca · ρ · V`. Stable, free, and correct
for the magnitude.

The cost is that added mass becomes isotropic, while the real thing is strongly
direction-dependent — a plate drags far more water broadside than edge-on, and that
anisotropy is part of what makes flapping work. A known limitation, recorded rather than
hidden.

## What it looks like

Twelve random genomes, driven by a phase-offset sine wave, no evolution whatsoever, measured
over eight seconds after a second of settling. Six of them:

| seed | parts | DOF | displacement | speed | J per metre |
|---|---|---|---|---|---|
| 1 | 6 | 2 | 0.787 m | 0.098 m/s | 5,149 |
| 9 | 3 | 6 | 0.687 m | 0.086 m/s | 6,034 |
| 3 | 3 | 3 | 0.476 m | 0.059 m/s | 6,845 |
| 12 | 3 | 2 | 0.391 m | 0.049 m/s | 4,907 |
| 5 | 5 | 4 | 0.014 m | 0.002 m/s | 1,720 |
| 8 | 3 | 2 | 0.000 m | 0.000 m/s | — |

Nobody designed any of these and nothing selected them. The spread is the interesting part:
the best travels fifty times as far as the worst that moves at all, on the same controller and
the same physics. One does not move.

That spread is the raw material a search needs.

The last column is a warning label as much as a measurement. [C18 §3, p.17] makes the point
that energy alone says nothing without a performance level to divide it by — and here is why:
seed 5 is the most efficient creature in the table at 1,720 joules per metre, and it is also
the second-worst. It barely moves, so it barely spends. **A creature that does nothing is
perfectly efficient.** Seed 8, which does not move at all, cannot be scored on this axis
without dividing by zero, which is the honest answer rather than an inconvenience.

An earlier draft of this piece reported the same experiment without the energy column, and
without knowing that most of what these creatures spend is destroyed against their own joint
limits rather than delivered to the water. Both of those are recent
([logbook 0008](../logbook/0008-the-energy-audit.md)), and they are the difference between a
table that looks like a result and one that is.

## Sources

| Key | Used here for |
|---|---|
| `[C18]` | The mesh-based quadratic drag scheme and `Cd = 1.5`; neutral buoyancy via zero gravity; added mass as the term whose absence precludes fish and squid and collapses morphological variety; energy meaning nothing without a performance level to divide it by |
| `[U07]` | Per-part reaction-force models disagreeing with hydrodynamics on direction of travel, and a published search exploiting that |
| `[K12]` | Water as drag per part with gravity disabled |

**Mine, not the literature's:**

- The framing of asymmetry as "the whole trick".
- The argument that physics-as-strategy-space explains why a simplified model costs variety
  rather than only accuracy. This is a reading of [C18]'s result, not a claim [C18] makes.
- **That a sphere cannot paddle.** No source says this; it is a property of *this*
  implementation, measured in the table above and asserted by a test. It follows from summing
  pressure over an isotropic surface, and it would not survive a fluid model that captured
  vortex shedding — which, per [C18 §4, p.28], this one does not.
- The reading of "a creature that does nothing is perfectly efficient" as the reason the
  per-metre column is the one to look at. [C18] makes the general point about energy needing a
  performance level; the illustration from seed 5 is ours.

## Where it is

- [`FluidModel.cs`](../src/Evosim.Core/Environment/FluidModel.cs) — the force calculation, no engine required
- [`PartShape.cs`](../src/Evosim.Core/Shapes/PartShape.cs) and [`StandardShapes.cs`](../src/Evosim.Core/Shapes/StandardShapes.cs) — the three shapes and the panels each presents
- [`FluidModelTests.cs`](../src/Evosim.Core.Tests/FluidModelTests.cs) — including the energy law
- [`ShapeTests.cs`](../src/Evosim.Core.Tests/ShapeTests.cs) — including the isotropy and flat-paddle measurements above
- [`FluidEnvironment.cs`](../unity/Assets/Evosim/Sim/FluidEnvironment.cs) — applying it per step
- [`DESIGN.md`](../DESIGN.md) §5 — the specification, and §5.3 on how this model has already been exploited in print
