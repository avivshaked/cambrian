# Proposal: a body earns no more light than its silhouette, and a terminal edge keeps the limit

*2026-09-19, 07:00, by the agent, for the owner's ruling. Absorbed into DECISIONS.md on
ruling and then deleted. The evidence is logbook/0107's last section; the probe is
`scripts/overlap/`.*

## What was found

Rounds 41b and 41c collapsed on contact pairs, and the pairs are inside bodies. A two-node
genome, a photosynthetic root and a `link` node with a terminal-only edge to itself, develops
into a knot of nine or sixteen parts folded into a ball 0.2 m across, in which every part
overlaps every other. PhysX resolves each pair on every step. The count of self-overlapping
pairs in a snapshot, developed in Core, is the physics' count of contact pairs per body in
every seed of both rounds. The joint was never the cause: round 41b's knots were articulated
because the joint was free and round 41c's are rigid because it was priced.

Two rules make the knot, and a third makes it pay.

1. **The developer.** `Developer.Expand` asks a node's recursive limit only of an edge that is
   not terminal-only (`if (!exhausted && !CanEnter(...)) continue;`), and a terminal-only
   edge fires exactly when the limit is spent. A terminal-only self-edge is therefore
   followed until `MaxDepth` (8). DESIGN §4.1's rule is that a node is entered while it
   occurs fewer times on the path than its limit; the terminal-only edge was written to
   put an extremity at a chain's tip, not to be a chain of its own.
2. **The turn.** The self-edge carries an orientation of about a hundred degrees, so the
   chain folds back through itself instead of extending. This is legitimate: Sims's
   creatures self-intersect, and DESIGN §11.2 kept self-collision on because jammed parts
   paid in the spike. Nothing here needs a rule; it is what a fold looks like.
3. **The light.** `Phenotype.TotalLitArea` is the sum of the parts' orientation-averaged
   projected areas, and a body earns on it and shades what is below it by it (§5A.2b:
   nothing collects light it does not also deny to what is below). A body never denies its
   own parts anything, so sixteen parts at one point collect sixteen areas. Under D098 a
   part costs the tissue price alone, 0.38 J a body at 500 J/m³, and growth draws nothing
   from the field, so the fold is free. Round 40 priced every part in matter at conception
   and as it grew, and its bodies evolved to half their founding size.

## Options

**A. The silhouette cap** (recommended). A body's lit area is capped at the area of its own
outline: `min(TotalLitArea, π R²)` with `R` the developed body's bounding-sphere radius
about its centroid, computed once at development and scaled with growth like everything
else. A knot in a 0.2 m ball earns at most the ball's disc; a chain or a spread body keeps
its sum, because its bounding sphere is large. It is one line in Core on a quantity
`Phenotype` already accumulates, it moves no genome semantics, and it prices nothing. The
shadow is capped by the same number, so §5A.2b's self-consistency holds. A tunable
(`EVOSIM_SILHOUETTE`, on or off, default off so every recorded config still describes its
world) under §9's rule, which refuses every earlier `config.json`, as every round's tunable
has. Inference to be screened: with the cap the fold earns a sixteenth of what it did and
the two-part leaf outcompetes it.

**B. The terminal edge keeps the limit.** `CanEnter` is asked of every edge, terminal-only
included. A terminal-only self-edge with a limit of 1 then grows one extremity, which is
what the edge was for. This changes what a stored genome develops into, so recorded
snapshots grow different bodies under the new build, which is already the rule for any
Core change (replays are by build). Alone it does not close the exploit: a non-terminal
self-edge with a mutated limit of 8 grows the same knot, and the knot pays the same. With
A it is a cleanliness fix, and I recommend both.

**C. Price the part.** A standing cost per part, the way `EVOSIM_IDLE` prices a joint. It
would work and it would need a number, screened, and the number would be a dial on how
many parts a body may afford rather than a rule about what light is. The exploit stays in
kind at any price under the one that freezes founding.

**D. Prune a self-overlapping part at development.** Changes the body space Sims defined
and §11.2 measured, and leaves a spread body earning its sum, which is right. Not
recommended while A is available.

**E. Self-collision off within a body.** Removes the physics cost and nothing else; the
knot keeps earning. §11.2 kept it on deliberately. Not recommended.

## What runs after the ruling

A screen first, fail-fast: seed 1 of round 41c's world at dt 0.02 for 5,000 s on the new
build, read on `pairs/body` under 0.05 at 5,000 s, the part histogram of the 5,000 s
snapshot (`scripts/overlap/`) showing no body at either cap, and the pace holding. Then
round 41d: round 41c's world with A and B, and, since the joint is exonerated, the joint's
price lifted back to round 41b's 0.0001 so that the free-joint question (round 34's, never
answered on this economy) is asked in the same round. E1 to E9 as pre-registered, E9 now
an instrument's reading of A. Five seeds, three at a time.

## What I am unsure of

Whether PhysX reports a pair between non-adjacent links of one articulation as a contact
was not verified in the engine's documentation; the arithmetic verified it instead, the
probe's count matching the physics' in eight snapshots across four seeds. And the
bounding sphere overstates the silhouette of an elongated body (a rod's disc is wider than
its shadow), which is on the safe side for a chain and irrelevant for a knot; a bounding
box's largest face is the tighter alternative if the owner prefers it.
