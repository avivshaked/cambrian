# 0006 — Boxes inside boxes

**2026-08-02**  ·  Milestone 1

A human looked at the sandbox for the second time. The movement *"looks almost biological"*,
which is good. But there are *"boxes inside boxes (on occasion), which is physically
impossible for one solid box to move inside another."* Right again, and again in seconds.

Nearly seven creatures in ten had a part buried inside another part. Three separate causes
were responsible, and the third one could not be fixed by any rule about how a creature is
built. It could only be caught by looking at the creature afterwards.

## Some of it is deliberate and most of it was not

Overlap at joints is on purpose. [`DESIGN.md`](../DESIGN.md) §4.2 says: *"Overlap at joints
permitted. Sims allowed it; enforcing non-overlap kills too many viable genomes."*
Self-collision is off for the same reason. So parts touching and interpenetrating a little
is expected, and a 16-part creature legitimately reads as a small clump.

A part whose centre is inside another part is well past that, and it was happening a lot.

## Measure before fixing

The temptation was to guess a cause and fix it. Instead I built a measurement.
[`PhenotypeGeometry.BuriedPartPairs`](../src/Evosim.Core/Development/PhenotypeGeometry.cs)
counts the pairs where one part's centre lies inside the other's box. It is crude, cheap and
pure arithmetic, so it runs in the headless Core suite over hundreds of genomes in
milliseconds.

The baseline: 69.7% of 400 random creatures had at least one buried part, and 1,832 pairs in
all.

That number is why guessing would have gone badly, because the first two fixes barely dented
it.

## The first cause was rotation with no bound

Edge orientations were drawn uniformly from all of SO(3), which is to say from every
rotation possible in three dimensions. The child is placed so that its anchor meets the
parent's anchor, and it is then rotated about that contact point. A large rotation swings
the child's body straight through the parent, and half a turn puts it entirely inside.

I bounded the rotation to ±50°, and stopped the generator handing the same face to two edges
of one node, which had been placing two children in the same spot.

The buried-part figure stayed at 69.7%. Neither of those was the dominant cause.

## The second cause was reflecting about the wrong axis

This one is properly interesting.

Mirroring moves a point only if the point has a component on the mirrored axis. A child
attached to the parent's +Y face sits at about `(0, d, 0)`. Mirror that about X and it is
still `(0, d, 0)`, which is the same place. The mirrored copy lands on top of the original.

The generator chose the reflection axis independently of the attachment axis, so two thirds
of its reflections produced coincident twins instead of pairs.

What makes this nasty is the failure mode. The creature has the right number of parts, all
in plausible positions, and it moves. It just looks less symmetric than it ought to. Nothing
announces that half the parts are inside the other half. It is the same class of bug as
[0005](0005-the-creatures-were-swimming-in-vacuum.md), whose output was also entirely
plausible.

The reflection axis now matches the attachment axis. That took the figure from 69.7% to
55.5%.

## The third cause cannot be fixed by any local rule

The remainder is structural. A node with edges on opposite faces places a child right where
its own parent already sits. The chain grows along +X, and an edge on −X points straight
back into the previous segment. Recursion means one edge does this at every level.

No constraint on a single edge can see that, because it depends on the path taken to reach
the node. It becomes visible only once the genome has been grown into a creature.

So the filter moved to the developed creature. `GenomeFactory.RandomViable` now rejects
phenotypes with buried parts and retries, keeping the least-bad candidate rather than
failing outright. That took 55.5% to 0.0% over 400 samples.

## Why this is not cosmetic

At Milestone 2 the fluid forces are computed per part. Two coincident parts collect drag and
thrust twice over for one body's worth of volume, so a stack of parts in one place is free
propulsion.

That is the exploit class in [`DESIGN.md`](../DESIGN.md) §11.2, and unlike most entries on
that list this one does not need the search to be clever. It is lying around waiting to be
found. Finding it now, because somebody said "that looks impossible", is far better than
finding it later as a leaderboard full of shimmering blobs.

## What the fix costs

Filtering on the grown creature biases the initial population toward simpler bodies. A
complicated creature has more part pairs and therefore more chances to bury one. Across the
twelve sandbox seeds part counts fell noticeably, with only two creatures still in double
figures.

That trades against §2, which is about protecting morphological variety. The filter is
defensible for an initial population, because nothing stops mutation exploring buried
configurations later. But "we made the starting creatures simpler" is the kind of decision
that becomes invisible six months on, so it is recorded here.
