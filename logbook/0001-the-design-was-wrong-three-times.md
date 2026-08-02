# 0001 — The design was wrong three times

**2026-08-02**  ·  Design phase, before any code

The plan was: write the design document, then run a literature review, then check one
against the other. The expectation was that the review would mostly confirm the design and
add citations to it.

It overturned it three times. Two were factual gaps. One was worse — a piece of reasoning
that was wrong in a way no fact-check would have caught, because it wasn't a fact.

---

## 1. The failure mode that wasn't in the document

Draft 1 described body–brain co-evolution as the whole point of the project and then said
nothing about why it is hard. The literature is unambiguous: it is *pathological*. A
morphological mutation invalidates the controller that was co-adapted to the old body, the
offspring performs worse, selection discards it — and this happens even when the new body is
better. Morphology stagnates within a few dozen generations while controllers keep
improving, which looks like progress right up until you notice every creature has the same
shape.

This is now [`DESIGN.md`](../DESIGN.md) §2, and it drives the entire search architecture.
MAP-Elites isn't in this project because quality-diversity is fashionable; it's there because
a mutant with a novel body needs to compete only within its own morphological cell, never
against the global champion. Without §2 that choice looks like a preference. With it, it's
forced.

**What's uncomfortable about this one:** the draft wasn't vague about the problem. It didn't
mention the problem. A confident, detailed, internally coherent document had a hole in it
exactly where the hardest part of the project lives, and nothing internal to the document
could have revealed that. Only outside evidence could.

## 2. The wrong cost model — my own reasoning, not a citation

Draft 2 considered using a cheap fluid model — drag proportional to surface area, no added
mass — and reasoned:

> That's fine for goal #1 (something nice to watch). It only compromises goal #2 (scientific
> accuracy).

That sentence is wrong, and it's wrong in a way that is worth recording, because it wasn't a
misremembered fact. It was a plausible-sounding trade-off invented on the spot and never
tested.

Corucci et al. [C18 §4, p.28] ran it. Simplified fluid dynamics doesn't cost you accuracy
while preserving spectacle — it **collapses morphological variety**. The paper describes
anatomical uniformity: no fish shapes, no squid shapes, just a gallery of similar medusoid
blobs, because without added mass there is no selective advantage to any of the body plans
that make real swimmers interesting to look at.

So the cheap model was failing hardest at the goal it was supposedly safe for. Visual
variety was the *entire* point. Added mass moved from "nice to have, Milestone 6" to
Milestone 3.

**The general lesson, which I expect to need again:** the claims most likely to be wrong are
not the ones citing something and getting it slightly off. They're the confident bridging
sentences with no citation at all — the ones that sound like reasoning and are actually just
fluent. Those are invisible to a citation check, because there's nothing to check.

## 3. A threat that dissolved on inspection

CPPN-NEAT looked like it might force a genome rewrite before a line was written. It's the
encoding that produced most of the striking modern results, and this project had committed
to Sims-style recursive graphs.

Reading every published encoding comparison instead of the famous results: the CPPN
advantage is confined to **soft-body** phenotypes, where a pattern-generating function
across a voxel grid is a natural fit. On rigid articulated bodies, direct and recursive
encodings win or tie [L21 Table 6, p.18]. The threat wasn't real for this project's
phenotype.

The same read fixed a terminology error I'd been carrying: Sims' encoding is **indirect**, not
direct. One graph node can unfold into many body parts through recursion. Getting that
backwards had made the CPPN comparison look more lopsided than it is.

---

## What this cost, and whether it was worth it

The review took substantially longer than writing the design did. It also produced
[`research/LITERATURE-REVIEW.md`](../research/LITERATURE-REVIEW.md), whose §7 says plainly
what it didn't establish — two of six questions are still only partially answered.

Worth it. Correction 1 would have surfaced eventually, but the way it
surfaces without the literature is: build everything, run it for a week, watch morphology
flatline, and have no idea whether the bug is in the mutation operator, the fitness function,
the physics, or the idea. Correction 2 would probably never have surfaced at all — a gallery
of similar-looking blobs reads as "evolution is hard", not as "the fluid model is
suppressing the diversity you're trying to produce."

The order also mattered. Design first, *then* review, meant there was something specific
enough to be wrong. A review conducted first would have produced a summary of the field and
no collisions.

---

**See also:** [`DESIGN.md`](../DESIGN.md) §0/§0b changelogs record all three revisions with
citations. [`DECISIONS.md`](../DECISIONS.md) D009 covers the spike-before-research ordering.
