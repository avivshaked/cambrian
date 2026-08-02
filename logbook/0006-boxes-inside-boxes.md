# 0006 — Boxes inside boxes

**2026-08-02**  ·  Milestone 1

Second report from a human looking at the sandbox: the movement *"looks almost biological"* —
good — but there are *"boxes inside boxes (on occasion), which is physically impossible for one
solid box to move inside another."*

Right again, and again in seconds.

## Partly by design, mostly not

Overlap at joints is deliberate. [`DESIGN.md`](../DESIGN.md) §4.2: *"Overlap at joints
permitted. Sims allowed it; enforcing non-overlap kills too many viable genomes."* Self-collision
is off for the same reason. So parts touching and interpenetrating slightly is expected, and a
16-part creature legitimately reads as a small clump.

A part whose **centre** is inside another part is well past that, and it was happening a lot.

## Measuring before fixing

The temptation was to guess a cause and fix it. Instead:
[`PhenotypeGeometry.BuriedPartPairs`](../src/Evosim.Core/Development/PhenotypeGeometry.cs)
counts pairs where one part's centre lies inside the other's box — crude, cheap, and pure maths,
so it runs in the headless Core suite over hundreds of genomes in milliseconds.

Baseline: **69.7%** of 400 random creatures had at least one buried part. 1,832 pairs in total.

That number is why guessing would have gone badly, because the first two fixes barely dented it.

## Cause one: unbounded edge rotation

Edge orientations were drawn uniformly from all of SO(3). The child is placed so its anchor
meets the parent's anchor, and then rotated about that contact point — so a large rotation
swings the child's body straight through the parent, and half a turn puts it entirely inside.

Bounded to ±50°. Also stopped handing the same face to two edges of one node, which was placing
two children in the same spot.

**69.7% → still 69.7%.** Neither was the dominant cause.

## Cause two: reflecting about the wrong axis

This one is properly interesting.

Mirroring moves a point only if the point has a component on the mirrored axis. A child attached
to the parent's +Y face sits at about `(0, d, 0)`. Mirror it about X: `(0, d, 0)`. **The same
place.** The mirrored copy is exactly coincident with the original.

The generator chose the reflection axis independently of the attachment axis, so two thirds of
its reflections produced coincident twins rather than pairs.

What makes this nasty is the failure mode. The creature has the right number of parts, all in
plausible positions, and it moves. It simply looks less symmetric than it should. Nothing
announces that half the parts are inside the other half. It is the same class as
[0005](0005-the-creatures-were-swimming-in-vacuum.md): a bug whose output is entirely plausible.

Reflection axis now matches the attachment axis. **69.7% → 55.5%.**

## Cause three: no local rule can fix it

The remainder is structural. A node with edges on opposite faces places a child exactly where
its own parent already sits — the chain grows +X, and an edge on −X points straight back into
the previous segment. Recursion means one edge does this at every level.

No constraint on a single edge can see that, because it depends on the path taken to reach the
node. It is only visible once the genome is grown.

So the filter moved to the developed creature: `GenomeFactory.RandomViable` now rejects
phenotypes with buried parts and retries, keeping the least-bad candidate rather than failing.
**55.5% → 0.0%** over 400 samples.

## Why this is not just cosmetic

At Milestone 2 fluid forces are computed per part. Two coincident parts collect drag and thrust
twice for one body's worth of volume — a stack of parts in one place is free propulsion.

That is the exploit class in [`DESIGN.md`](../DESIGN.md) §11.2, and unlike most entries on that
list, this one does not need the search to be clever. It is lying around waiting. Finding it now,
because someone said "that looks impossible", is much better than finding it as a leaderboard
full of shimmering blobs.

## An honest cost

Filtering on the grown creature biases the initial population toward simpler bodies: a
complicated creature has more part pairs and therefore more chances to bury one. Across the
twelve sandbox seeds, part counts fell noticeably, with only two creatures still in double
figures.

That trades against §2, which is explicitly about protecting morphological variety. The filter
is defensible for an *initial population* — nothing prevents mutation from exploring buried
configurations later — but "we quietly made the starting creatures simpler" is exactly the kind
of thing that is invisible six months on. Recorded here so it is not.
