# The anchoring cell: a design for round 59

*Design for the owner, 2026-10-09, from D159's item 8. The anchoring cell is built while round 58 is built
and run, with a sense of touching the bed or a reef. It is tried in the nursery first, and runs in round 59
with the reefs as the place to hold on to. A read-only survey of the code the same day is behind it. Nothing here is
built. The owner ruled on its questions and its pricing on 2026-10-11 (D162).*

The anchoring cell is a new kind of cell. When the part that carries it touches the bed or a reef, it grips
there and holds the body in place against the current, at an ongoing cost in energy. It lets go when the
pull on it is too strong, when the body can no longer pay, or, in a second step, when the creature's brain
says so. A new sense tells each part whether it is touching the bed or a reef. Together they make room for
a settled way of life beside the drifting one. A leaf could hold on to a reef top and stay in the light as it
grows, where today growth sinks it. A stomach could hold still in a current, where moving water keeps
refilling the patch it empties.

## What the world has today

- Seven cell types, in a fixed list that only grows at its end. Each charges a standing cost per cubic
  metre every step, and a type can add costs of its own, as the buoyancy cell does for its lift.
- A depth sense, available to every brain. A contact sense, which reports only touching another creature,
  though its name says "terrain, or another creature".
- Contact with the bed and the reefs in our own physics solver, part by part. The rock pushes back along
  the surface's normal, and its friction is switched off in every recent round. Nothing records that a part touched the
  bed. The graphics card computes the same contact.
- No joint that ties a body to the world. The solver moves each body freely, and its joints join a part
  only to its parent part.
- One energy reserve per body. Only health is kept per part.

## How it would grip

The physics would hold an anchored part with a spring and a damper pulling it toward the point where it
first touched. The solver already treats contact this way: as forces added to a part, worked out body by
body. A true rigid joint to the world would mean rebuilding how the solver treats a body's root, which is
far larger. The hold would have a second spring for turning, so an anchored part cannot spin freely about
its grip.

1. A part with an anchoring cell grips when its contact sphere touches the bed or a reef. It grips at the
   point of contact, and keeps that point until it lets go.
2. The hold pulls the part back toward its point with a force capped like the contact push, so the solver
   stays stable. A strong pull can break the grip. The breaking force grows with the cell's area, its
   volume to the power 2/3, so a larger anchoring cell holds harder.
3. The cell pays its standing cost like every other cell, and a cost to build the grip when it forms,
   scaled with the breaking force. Holding costs nothing more by the second. The grip's strength decays
   slowly while it holds, which stands for its upkeep. The nursery sets the numbers.
4. The grip weakens as the body's reserve runs low, after a lag, and lets go when the reserve reaches zero
   or the pull exceeds the breaking force. The reserve is the body's, so "the tissue can no longer pay"
   means the body can no longer pay.
5. In the second step, the brain gains one output for each anchoring part, with no new gene. Above a
   threshold the part may grip; below it, the part lets go. Lift under the brain's control was planned
   the same way in the animal kit.

## The sense of touching the bed

A new sense channel, gated by its own setting, reads 1 for a part that touched the bed or a reef on the last
physics step and 0 for one that did not. It is a channel of its own, added at the end of the list of
senses. Folding it into the contact sense would change what every recorded genome's contact reading means.
The contact sphere is the part's bounding sphere, so a part feels the bed a little before its surface would
reach it.

## Keeping old worlds as they were

Every change sits behind a setting that is off by default. A mutation can draw the anchoring cell only when its
setting is on, so the buds of a world without it draw as they do today. The
new settings still make the new build refuse older config files, as every new setting does. The recorded
test fixtures and the nursery's world configs are rewritten for it, as in earlier rounds. A body's grip
points go into the checkpoint, so a resumed run holds on where it was holding.

Round 59 runs on the card (D157), so the grip and the sense are ported to the card's kernels. Until they are,
the card refuses a world that switches them on, as it refuses bed friction today. The build starts from the
card's branch, where the rounds will run.

## The nursery bench

The nursery evolves brains by direct selection on fixed bodies, scored in the world's own currency. A bench
there would answer the first question: whether holding on pays. Two arms run the same bodies with the same
draws, anchoring on and off, so each pair of episodes differs only by the grip.

- A stomach arm in a current. The measure is the food eaten. The earlier measurement predicts more food for
  the anchored stomach, because the water keeps refilling its patch. That patch held 5 to 11% of untouched
  water where the water stood still, and 20 or 25% where it moved at 3 to 6 cm/s. The prediction is an
  inference: that test moved the water, and nothing held on.
- A leaf arm on a reef top, once the nursery's tank has reefs. Its config has none today. The measure is
  the light caught as the leaf grows.
- A new episode measure, seconds held, so the reading shows that the grip is what changed.

## What I am unsure of

- Whether the hold is stable on large bodies and long arms. A spring strong enough to hold a body in a
  current is also one that can ring, and the solver steps forward explicitly. The caps and the damping are
  set by tests before any run.
- Whether the bounding sphere's early contact makes parts grip in mid-water near a reef, which the tests
  check.
- How long the build takes. My guess is a week of agent work, the card port included, after round 58's
  runner.

## The drag on turning, built with it

The owner ruled on 2026-10-10 that a part in the water also feels a drag against its turning, built with the
anchoring cell and carried by every world of round 59 (D161). Nothing brakes a turning body today, which is
why single stomachs rock without end (`logbook/specs/rocking-bodies-2026-10-10.md`), and the grip's spring
needs that damping not to ring. The nursery check that sets the drag's size comes before the grip's bench, so
the bench prices holding on in water that already resists turning.

## What the literature says about its price

The owner asked on 2026-10-10 that the cell's prices follow how attachment works in real organisms. A
literature round read it (`logbook/specs/r59-anchor-lit/`, the answers in `pass2.md`). Its costs are
measured almost only in mussels. It supports four changes to the drafted pricing, which the owner accepted on
2026-10-11 and which "How it would grip" now carries.

1. **A cost to build the grip, paid when it forms and scaled with its breaking force, in place of a cost
   for each second held.** A mussel's thread costs about 1 J. Its byssus takes 2 to 8% of an undisturbed
   mussel's energy budget and up to about half of one made to re-attach daily. No study measured a running
   cost of holding, and a fee per second on top of the cell's standing cost would charge twice.
2. **Upkeep as decay.** Threads lose 29 to 48% of their strength in four weeks and are replaced, so the
   grip weakens slowly and its renewal costs the build price spread over the grip's life.
3. **Breaking force by the cell's area, not its volume**, so by volume to the power 2/3. Grip per unit of
   shell area is constant across mussel sizes, a holdfast's hold is linear in its base area, and a sea
   star's is tenacity times contact area. No paper relates grip to an organ's volume.
4. **A grip that weakens as the reserve runs low, after a lag, and lets go at zero.** Small, poorly fed
   mussels lost 41% of their grip in a week; large ones held for a week, then weakened over two.

Letting go is quick and cheap in the evidence, and rebuilding is what costs, which the first change prices.

## What the owner ruled

The owner ruled on 2026-10-11 (D162). The grip is automatic in the first cut, and the brain's control is the
second step. A strong pull can break it. It grips the bed and the reefs, never the tank's glass. The nursery's
bench sets its costs before round 59 is pre-registered.
