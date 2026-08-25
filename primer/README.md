# Primer

What was built, and why it is interesting.

The other documents each answer a narrow question well and none of them teaches. `DESIGN.md`
is a specification — it says `reflect : bool3` and cites the paper that settled it. It does
not tell you why one bit flipping is the difference between a pile of debris and something
that reads as an animal. `DECISIONS.md` records conclusions. `logbook/` records what
happened on the day. The literature review records what the evidence says.

None of that is readable as an explanation of the thing itself. This is.

## What it is

One piece per real idea in the system, written after that idea exists in code, aimed at a
reader who wants to understand how you build a creature that nobody designed.

It is not a tutorial and not an API reference. It assumes you can read code but not that you
know anything about evolutionary computation.

## The rule that keeps it honest

**A piece is never a source of truth.** When it needs a specific — a cap, a constant, a
citation — it links to [`DESIGN.md`](../DESIGN.md) or to the code rather than restating the
value. Restated values drift, and a friendly-sounding wrong number is worse than no number.

What a piece *does* own is meaning: why a mechanism exists, what it buys, what it costs, what
would go wrong without it. That does not drift, because it is not a parameter.

## Citations

Same convention as everywhere else here: `[KEY §section, p.N]`, with `p.N` the PDF page, and
keys resolved in [`DESIGN.md`](../DESIGN.md) §13. A piece ends with a short table of which
sources it leaned on and for what.

Explanatory writing invites a particular failure: the confident bridging sentence that sounds
like reasoning and is actually just fluent. One of those was the largest error the literature
review caught in this project's design. So **anything a piece asserts that is not traceable to
a source is marked as the author's inference, in the text and in the sources table.** A reader
should never have to guess which sentences are backed and which are guesses.

Pieces are written when the thing works, not when it is planned. A primer that describes
unwritten code is a design document with adjectives.

## Pieces

| # | Piece | About |
|---|---|---|
| [01](01-a-graph-that-grows-a-body.md) | A graph that grows a body | The encoding: why a genome is a recipe and not a blueprint |
| [02](02-from-a-tree-of-boxes-to-something-that-moves.md) | From a tree of boxes to something that moves | Articulations, the scale trap, and why effector conditioning decides whether it looks alive |
| [03](03-what-it-means-to-push-against-water.md) | What it means to push against water | Drag, asymmetry, and why a cheap fluid model costs variety rather than only accuracy |
| [04](04-nobody-decides-who-wins.md) | Nobody decides who wins | An energy economy instead of a fitness function, and the four ways the world cheated once we let it run |
| [05](05-a-brain-that-is-copied-with-the-limb.md) | A brain that is copied with the limb | Why the controller lives inside the body graph, and what an open-loop swimmer cannot do |
