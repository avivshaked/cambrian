# 0009 — When a part stopped being a box

**2026-08-07**  ·  Milestone 2

Parts had been boxes since the first line of code. [`DESIGN.md`](../DESIGN.md) §4.1 never said
they had to be, and a body made only of rectangular blocks is a strange thing to hand to
evolution and then ask what it discovers about swimming. Spheres and capsules went in behind a
`PartShape` registry, the same shape the cell types already had.

Three things came out of it that were not the feature.

## The 21% that only a sum could see

Drag is accumulated per panel: each shape hands the fluid model a set of outward-facing patches
with an area each, and force is summed over the leading ones. Nothing in that requires the
panels to tile the surface exactly — the model samples, it does not integrate — so there is no
natural point at which a wrong panel set announces itself. A capsule with the wrong panels
still swims. It just swims in slightly the wrong water.

So the test asserts the one property that has to hold: **panel areas must sum to the shape's
surface area.**

```
capsule: 90 panels, 2.0735 m² vs 2.6389
```

The cap loop generated `capCount` panels covering a whole sphere's worth of surface — the
directions span the full sphere and are sorted onto the two ends by the sign of their Y — but
priced each one at `4πr² / (2 × capCount)`. Off by exactly a factor of two on the caps, which
is 21% of the whole shape.

The cylinder section was right, so the error was too small to look wrong and too large to be
harmless. Every capsule in every run would have experienced water four-fifths as dense as every
box, and the only visible consequence would have been that evolution mildly preferred capsules —
a finding about the cost model, reported in good faith, and entirely an artefact.

There is no plausibility check that catches this. A number has to be compared against something.

## The check that would have stopped checking

Two harnesses measured colliders by asking for the concrete type:

```csharp
colliders[i] = creature.Bodies[i].GetComponent<BoxCollider>();
```

`GetComponent` returns null for the wrong type. `JamSurvey` already had
`if (colliders[a] == null || colliders[b] == null) continue;` — a defensive line, written for
good reasons, which as of this change would have silently skipped **every sphere and every
capsule**: about half of all parts. The survey would have gone on printing a table, and the
overlap numbers would have fallen, because two shapes out of three had stopped being measured.

That is worse than a crash and worse than a wrong answer. It is a wrong answer that moved in
the direction that looks like progress.

`Collider` instead of `BoxCollider` fixes it in one word — `ComputePenetration` never cared
about the concrete type. But nothing failed. It was found by grepping for what assumed a box,
which is a thing you have to think to do.

The Milestone 1 geometry check had the same shape of problem and needed a real fix rather than
a widening: it compared collider size against half-extents, and a sphere and a capsule both
*fit inside* the box their half-extents describe. Comparing bounding boxes would have passed a
creature whose parts had all silently been built as boxes — which is precisely the most likely
way for this work to be wrong. It now compares each shape's own defining dimensions: radius for
a sphere, radius and height for a capsule.

## The bottleneck was never where the design said

§5A.9's population ceiling came from Spike 01 — 128 creatures at 1.945 ms/step — with a guessed
3× penalty applied for "fluid drag, self-collision and brain evaluation, all of which now exist
or are coming". Two of the three now exist. Time to stop guessing.

`ThroughputSurvey` runs the real configuration: self-collision on, §5.2 drag applied, mixed
shapes, tiled at 100 m.

| creatures | ms/step | drag ms | physics ms | real time |
|---|---|---|---|---|
| 1 | 0.134 | 0.053 | 0.080 | 74.9× |
| 128 | 6.418 | 5.465 | 0.953 | 1.56× |
| 512 | 25.909 | 22.698 | 3.210 | 0.39× |

The headline survived almost exactly: 128 creatures at 1.56× real time against a predicted 1.7×.

**The attribution was backwards.** §5A.9 said the wall was "physics rather than ecosystem
bookkeeping". At 512 creatures physics is 12% of the step and our own drag loop is 88%.

Per creature, the two go opposite ways:

| | 1 creature | 512 creatures | scaling |
|---|---|---|---|
| PhysX | 0.080 ms | 0.0063 ms | **0.078×** |
| drag loop | 0.053 ms | 0.0443 ms | 0.84× |

PhysX's island parallelism is far better than the 0.28× the design claimed. Our loop is a
single-threaded managed pass over every panel of every part and does not scale at all, which is
not a surprise once stated — it is just that nobody had stated it.

Two things are worth noting about how nearly this was missed. Timing drag and physics separately
was a decision made while writing the harness, for no stronger reason than that they have
different fixes; a single combined column would have shown 6.4 ms at 128 creatures, matched the
prediction, and confirmed a claim that is wrong. And the right number being right for the wrong
reason is not a rare accident here — it is the third time in this logbook
([0002](0002-the-spike-that-was-too-fast.md),
[0008](0008-the-energy-audit.md)) that a figure passed a sanity check while measuring something
other than what its column said.

Nothing was optimised. Both levers are obvious — panels are rebuilt from scratch every step
though a part's local geometry never changes after development, and the loop is embarrassingly
parallel across creatures — and neither changes a force, so both wait for Milestone 4, where
the island model is what first makes population the binding constraint. A ceiling you can raise
by writing code is a different kind of fact from one you cannot, and §5A.9 now says which this
is.

## What it cost

One feature, three findings, none of them about the feature:

1. A shape that would have been 21% under-drag in every run, catchable only by a sum.
2. A survey that would have stopped measuring half its population without failing.
3. A design claim about where the performance wall is, which was wrong about the wall.

The pattern from [0008](0008-the-energy-audit.md) holds and gains a corollary. There, a
conservation law caught what plausibility missed. Here, twice, what caught the error was a
quantity being compared against an independently-derived expectation — the analytic surface
area, the shape's own dimensions — rather than against nothing. The generalisation is dull and
keeps being true: **a measurement that is not compared to something is not a measurement.**
