# 0001 — The design was wrong three times

**2026-08-02**  ·  Design phase, before any code

I wrote the design document first and then ran a literature review to check it against the
field. I expected the review to confirm the design and hang citations on it. It overturned
the design three times. Two of the three were things I had never heard of and could have
looked up. The third was worse. It was a piece of reasoning I invented on the spot, which
sounded right and was wrong. No fact-check would have caught it, because it was not a fact.

## 1. The hardest part of the project was missing from the document

The premise of this project is that a creature's body and its brain evolve together. The
brain is a controller: a network that reads the creature's senses and drives its joints. A
mutation can change the shape of the body, the wiring of the controller, or both at once.
Draft 1 said that this was the point of the project, and then said nothing about why it is
hard.

The literature is unambiguous. Evolving the two together is pathological. Every creature
gets a score for how well it does the task, and that score is called its fitness. Selection
is the mechanic that keeps the high scorers to breed from and throws the rest away. Now
change the shape of the body. The controller that was tuned to the old body no longer fits
it, the offspring scores worse, and selection discards it. That happens even when the new
body is the better one. So morphology, meaning the shape of the body, stops changing after a
few dozen generations, while controllers keep improving. Fitness climbs the whole time. It
looks like progress right up until you notice that every creature has the same shape.

That finding is now [`DESIGN.md`](../DESIGN.md) §2, and it decides the search architecture.
MAP-Elites keeps an archive with one slot for each type of body, and in each slot the best
creature found so far with that body. The name is short for multi-dimensional archive of
phenotypic elites, and an elite is whatever currently occupies a slot. A mutant with a novel
shape is only ever ranked against the other occupants of its own slot, never against the
best creature in the world. Without §2 that choice reads as a preference among search
algorithms. With §2 it is the only thing keeping a new body alive long enough to be judged.

What bothers me about this correction is that the draft was not vague about the problem. It
did not mention the problem. A confident, detailed, internally consistent document had a
hole in it where the hardest part of the project lives, and nothing inside the document
could have shown me that. Only evidence from outside it could.

## 2. I invented a trade-off that had already been tested

Draft 2 considered a cheap fluid model: drag proportional to a body part's surface area,
and no added mass. Added mass is the water a body has to shove aside and drag along with it
as it accelerates. Leaving it out makes the simulation faster and the swimming less real. I
reasoned:

> That's fine for goal #1 (something nice to watch). It only compromises goal #2 (scientific
> accuracy).

That sentence is wrong, and I want it on the page, because it was not a misremembered fact.
I invented the trade-off while writing and never tested it.

Corucci et al. had already run the experiment [C18 §4, p.28]. A simplified fluid model does
not trade accuracy for spectacle. It collapses the variety of body shapes instead. Their
swimmers came out anatomically uniform: no fish shapes, no squid shapes, a gallery of
similar medusoid blobs, meaning jellyfish-shaped. Without added mass there is no selective
advantage to any of the body plans that make real swimmers worth watching.

So the cheap model was failing hardest at the goal it was supposed to be safe for. Visual
variety was the thing I wanted most. Added mass moved from "nice to have, Milestone 6" to
Milestone 3. The milestones are the numbered stages of the build plan, so that is a move
from late to early.

I expect to need the general lesson again. A claim that cites a source and gets it slightly
off is the easy kind of error to find. The dangerous kind is the confident bridging sentence
with no citation at all, the one that sounds like reasoning and is only fluent. A citation
check cannot see it, because there is nothing to check.

## 3. The CPPN threat dissolved once I read the comparisons

CPPN-NEAT looked as though it might force a rewrite of the genome, the description a
creature is grown from, before I had written a line of code. It is two things bolted
together. A compositional pattern-producing network, or CPPN, is a small mathematical
function. Give it a point in space and it returns whether there is body material there, so
evaluating it everywhere draws a creature the way a formula draws a shape. NEAT, short for
neuroevolution of augmenting topologies, is the algorithm that evolves those functions,
growing them from simple to complicated. Together they produced most of the striking modern
results in this field. This project had already committed to Sims-style recursive graphs,
meaning Karl Sims's 1994 encoding. A genome there is a small diagram of body-part
descriptions joined by arrows, and following the arrows, sometimes back into a part already
visited, grows the creature.

An encoding is the rule that turns a genome into a body, so I read every published
comparison of encodings instead of the famous demonstrations. The CPPN advantage turns out
to be confined to soft-bodied creatures, which are built from a grid of small deformable
cubes called voxels. A function evaluated over a grid is a natural fit there. On rigid
bodies with joints, direct and recursive encodings win or tie [L21 Table 6, p.18]. The
threat was not real for the kind of creature this project builds.

The same reading fixed a piece of vocabulary I had been getting wrong. Sims' encoding is
indirect. I had been calling it direct. One node in the graph unfolds into many body parts
through recursion, and that is what makes an encoding indirect. Having it backwards had made
the CPPN comparison look more lopsided than it is.

## What the review cost, and why it earned that back

The review took substantially longer than writing the design did. It also produced
[`research/LITERATURE-REVIEW.md`](../research/LITERATURE-REVIEW.md), whose §7 says plainly
what it did not establish: two of its six questions are still only partly answered.

It was worth it. Correction 1 would have surfaced on its own eventually, and I can picture
how. I would have built the whole thing and run it for a week. Then I would have watched
morphology flatten out. The fault could have been in the code that mutates a genome, in how
fitness was scored, in the physics, or in the idea itself. Nothing in the run would have
told me which. Correction 2 would probably never have surfaced at all. A gallery of
similar-looking blobs reads as evolution being hard. It does not read as a fluid model
suppressing the diversity you are trying to produce.

The order of the two mattered too. Writing the design first meant there was something
specific enough to be wrong. A review conducted first would have produced a summary of the
field and no collisions.

The three revisions are recorded with their citations in [`DESIGN.md`](../DESIGN.md)'s §0
and §0b changelogs. [`DECISIONS.md`](../DECISIONS.md) D009 covers the spike-before-research
ordering, a spike being a throwaway project built to answer one question before the real
work starts.
