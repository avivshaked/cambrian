# Primer

What was built, and why it is interesting.

The other documents each answer a narrow question well, and none of them teaches. `DESIGN.md` is a specification: it
says `reflect : bool3` and cites the paper that settled it. It does not tell you why one bit flipping is the
difference between a pile of debris and something that reads as an animal.

`DECISIONS.md` records conclusions, and `logbook/` records what happened on the day. The literature review records
what the evidence says.

None of that is readable as an explanation of the thing itself. This is.

## What it is

One piece per real idea in the system, written after that idea exists in code. It is aimed at a reader who wants to
understand how you build a creature that nobody designed.

It is not a tutorial and not an API reference. It assumes you can read code, and it assumes nothing about what you
know of evolutionary computation.

## The rule that keeps it honest

A piece is never a source of truth. When it needs a specific value, whether a cap, a constant or a citation, it links
to [`DESIGN.md`](../DESIGN.md) or to the code rather than restating the value. Restated values drift, and a
friendly-sounding wrong number is worse than no number.

What a piece *does* own is meaning: why a mechanism exists, what it buys, what it costs, and what would go wrong
without it. That does not drift, because it is not a parameter.

## Citations

The convention is the same as everywhere else here: `[KEY §section, p.N]`, with `p.N` the PDF page.

Keys are resolved in [`DESIGN.md`](../DESIGN.md) §13.

A piece ends with a short table of which sources it leaned on, and for what.

Explanatory writing invites a particular failure: the confident bridging sentence that sounds like reasoning and is
actually just fluent. One of those was the largest error the literature review caught in this project's design.

So **anything a piece asserts that is not traceable to a source is marked as the author's inference**, in the text and
in the sources table. A reader should never have to guess which sentences are backed and which are guesses.

Pieces are written when the thing works, rather than when it is planned. A primer that describes unwritten code is a
design document with adjectives.

## Pieces

| # | Piece | About |
|---|---|---|
| [01](01-a-graph-that-grows-a-body.md) | A graph that grows a body | The encoding: why a genome is a recipe and not a blueprint |
| [02](02-from-a-tree-of-boxes-to-something-that-moves.md) | From a tree of boxes to something that moves | Articulations, the scale trap, and why effector conditioning decides whether it looks alive |
| [03](03-what-it-means-to-push-against-water.md) | What it means to push against water | Drag, asymmetry, and why a cheap fluid model costs variety rather than only accuracy |
| [04](04-nobody-decides-who-wins.md) | Nobody decides who wins | An energy economy instead of a fitness function, and the four ways the world cheated once we let it run |
| [05](05-a-brain-that-is-copied-with-the-limb.md) | A brain that is copied with the limb | Why the controller lives inside the body graph, and what an open-loop swimmer cannot do |
| [06](06-the-producers-feed-the-water.md) | The producers feed the water | Why the second trophic level starved at one percent, what the ocean does about it, and the food chain that followed |
| [07](07-the-world-keeps-two-books.md) | The world keeps two books | The energy audit and the matter identity: what each conserves, what each found, and why a closed book is what lets every other number be believed |
| [08](08-the-water-as-vertices.md) | The water as vertices | Amounts on vertices instead of values in cells, so a still body eats a hole and a moving one leaves it; what the first rounds found, and why a grid replaced it a week later. |
| [09](09-a-mouth-must-touch-what-it-eats.md) | A mouth must touch what it eats | Contact eating in place of the corpse rule: a bite is a wound, what the change cost on its first day, and why it stays |
| [10](10-a-fin-pushes-the-water-by-its-width.md) | A fin pushes the water by its width | The fin law: why a stroke built on volume bought no fins, the paddle on the bench, the blade beyond the joint, and a world that has not yet grown one |
| [11](11-a-fourth-shape-lets-evolution-walk-a-limbs-proportions.md) | A fourth shape lets evolution walk a limb's proportions | The superellipsoid family: three genes that step, contact on the true shape built and switched off, and a screen that found the shapes reachable but not yet favoured |
| [12](12-the-open-ocean-grew-its-oases-and-then-filled-the-water-between-them.md) | The open ocean grew its oases, and then filled the water between them | Why the tank was left, what makes an island an oasis, blooms on their own clocks, and a film world that grew to the ceiling |
| [13](13-a-seed-is-one-world-at-any-thread-count-and-on-the-card.md) | A seed is one world, at any thread count and on the card | Replay and why it matters: the shared box that did not replay in Unity, sums in a fixed order on the farm, the Editor as a cousin, hashes that detect, the card proving itself against itself, and the three ways a resume has parted since |
| [14](14-relatives-look-alike-because-the-skin-is-read-from-the-genome.md) | Relatives look alike because the skin is read from the genome | The theatre's skin map: why a hash of the body plan made strangers of near kin, the 26 features and the fixed projection, the one number checked, the two faults the sheets caught, and the looks the owner accepted |

Chapter 6 is the tiled ecology's history and stays that way. Chapters 7 and 8 were written on 2026-09-08 with the
owner's agreement, after the shared world had held its reference (round 28) and the vertex world had closed both
identities through a confirming round. The water is a grid from round 32 (D086). Piece 08's afterword says what
changed. Pieces 09 to 12 were written on 2026-10-31, after contact eating, the fin law, the shape family and the open
square had each run in the world and been read. Pieces 13 and 14 followed the same day, on replay, which every round
since the farm has kept, and on the theatre's skins, which no creature in the world sees. Piece 12 cites the
literature review's keys, which resolve in its bibliography rather than in DESIGN.md.