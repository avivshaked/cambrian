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

Pieces are written when the thing works, not when it is planned. A primer that describes
unwritten code is a design document with adjectives.

## Pieces

| # | Piece | About |
|---|---|---|
| [01](01-a-graph-that-grows-a-body.md) | A graph that grows a body | The encoding: why a genome is a recipe and not a blueprint |
