# Proposal: the animal kit, and places to live

*Fable, 2026-09-21, draft. For the owner's ruling after round 42's entry (logbook/0110) is
written. The owner asked on 2026-09-21 to start adding shapes, cell types and sensors, and
for natural structures on the floor that ecosystems can evolve around. This is the order I
would build them in and why. It carries `fable-propose-predation.md` forward onto D098's
economy and replaces its rules 4 and 5; the rest of that text stands and is cited, not
restated. The survey behind it is `scratch/animal-kit/survey-2026-09-21.md`, checked
against the code where this text leans on it. Absorbed into DECISIONS.md on ruling, then
deleted.*

## What round 42 says about variety

Round 42's world grows drifting plants. In two seeds of three the jointed line spread and
was then removed by selection: seed 3 went from 607 jointed bodies of 731 at 15,000 s to 18
of 843 at 30,000 s, and its place was taken by a welded body of the same plan. No eater
line founded in any seed. Nothing was thrown, the books closed, and the crowd was the
budget's. The world works and it is a soup, which is what the owner saw in the pictures:
real water shows creatures dispersed and in schools, and this one shows a mist.

My reading, and it is an inference from five seeds: a body in this world has nothing to
swim toward and nothing to swim from. Light falls everywhere at a depth, the dissolved
matter is mixed flat, no body can hurt another, and no body can tell that another is near.
A joint costs upkeep and buys nothing, so selection removes it. More shapes and more cell
types added to that world would give more kinds of drifting plant.

So the kit is ordered by what it gives a body to do, and each step is one round that can
fail on its own predictions.

## What exists today

| | Built and live | Declared, reading zero | Not there |
|---|---|---|---|
| Shapes | box, sphere, capsule | | a thin fin that lifts; ellipsoids in the physics (queued) |
| Cell types | structural, link, neural, photosynthetic, absorptive, consumer, buoyancy | the consumer's live bite (`ConsumerCell.Acquire` reads a `TissueContact` that `Metabolism` never supplies) | anchoring; armour; anything that emits |
| Senses | joint angle and rate, up, depth, chemical, energy, flow | `Contact`, `Damage`, `Photo` | any sense of another body |
| Actions | joint torque | | a bite on purpose, holding on, lift under the brain's control, emission |
| Deaths | starved, diverged | | eaten |

Two facts in that table decide the order. The first rung needs no new genome field, because
the consumer cell, the contact sense and the damage sense are all in the genome already; a
format bump refuses every stored genome and a registry change does not. And D020 rejected
any sense that reports a bearing to another body, on the ground that direction should come
from the body comparing scalar readings across its parts. I propose inside D020 and mark
the one place where the owner might want to reopen it.

## Rung A: something to eat that is alive, and a way to know it happened

**A1. The bite**, as `fable-propose-predation.md` rules 1 to 3, 6 and 7: a mouth is a
consumer part, a bite is a contact held across a metabolic step, bites are priced after the
feeding allocation from a frozen ordered list, each target's loss capped before mouths
divide it, `BiteJoulesPerSecond` (`EVOSIM_BITE`) default 0 so every recording replays.

**A2. What the bitten loses, on D098's base.** A bite takes charged units. It takes them
from the reserve first and from tissue when the reserve is empty. The reserve is where
nearly all of a body's charged matter is (round 40 held about 190 J of reserve a body
against 0.37 J of tissue), so a bite that took tissue alone would move almost nothing. This
reverses the predation proposal's rule 4, which kept the bite out of the reserve; under
D098 there is no locked matter to move and the hoard is the larder. Tissue loss shrinks the
body through the growth machinery as that rule says. A body under the newborn mass floor,
or with no reserve and no tissue to give, dies `Eaten`. The eater keeps the yield as
reserve, and the waste share becomes marine snow in the cell where the bite happened, so
both books close by the arithmetic burning and eating already use. There is no
captured-matter reserve, because D098 has one substance.

**A3. `Contact` and `Damage` wired**, behind `SenseContact` and `SenseDamage`, off by
default. `Damage` reads the share of a part's holding the last step took.

**What it should do.** Give the eaters a larder that is the living crop and not the
corpses, which D098 sized at a fraction of a percent of what the leaves capture; and give
the leaves a reason to be somewhere else. The round: dose found with the ledger first
(D069), dt 0.01 from the first screen because a held contact is a property of the step,
senses and dose never changed together.

**What the owner rules here**: that predation is now due (ruled too early on 2026-09-11,
before the tank, the streams and D098), and A2's reversal, that a bite reaches the reserve.

## Rung B: a sense of others, inside D020

**B1. A scent of bodies.** A third scalar field beside the two the grid already carries:
every living body deposits into its cell at a rate proportional to its burning, the field
is carried and mixed like the marine snow and decays at a rate, and a `Scent` channel reads
it at a part the way `Chemical` reads food. A body with two sensing parts a body-length
apart reads a gradient, which is D020's route to a bearing. It costs one more field on the
1 m grid, about what the detritus costs today (the world's step is 11 to 14% of the wall at
a full crowd). One tunable per guild flag would let an eater smell leaves apart from
eaters; I would start with one scent for all and split it only if a round asks.

**B2. The eyespot** (`Photo`, DESIGN §4.4): irradiance at the part, which shading by a
body overhead already modulates. Cheap, already declared, and it lets a body hold a depth
by light and notice a shadow.

**The place D020 might be reopened.** Schooling in real water runs on a lateral line and
eyes, both of which report where a neighbour is. A scent field at 1 m cells cannot resolve
a neighbour at half a metre. If rounds on B1 show bodies following gradients and never
holding station beside one another, the next sense would be a short-range neighbour sense
(the nearest body's offset in the part's own frame, within a few body lengths), and that is
a bearing. I do not propose it now. I name it so the owner knows where the wall is.

## Rung C: places, and the organ that holds to them

The owner's floor structures. A place matters to selection only when something differs
there and a body can stay. The tank has a shaped bed already (D093: relief 1.5 m, tilt
30 m), and no body can stay anywhere, because the streams carry everything.

**C1. The shelf.** The bed raised into the lit band on one side of the tank, which the
streams' shallow rule (ruled 2026-09-21 at 0.76 of `k_max`, built on branch
`streams-shallow`) makes possible. Lit floor is where a settled plant and a drifting one
can both live, which is what a reef is.

**C2. Rock.** Relief with a shorter length scale than D093's: boulders and ridges a few
metres across, from the same height-field machinery, so hollows collect sinking snow and
lee sides shelter from the streams. A dial on the bed, no new genome. The streams would
need their bed pass to respect it, which is the build's one hard part; the first cut can
keep relief under the bed pass's present limit and say so.

**C3. The seep.** D067's vent, off since its round: a point on the bed that leaks spent
matter at a rate. A closed D098 world has no room for a source, so the seep has to be a return. A share of
the remineralisation would be released at the seeps and not in every cell. The total is unchanged and the water is rich
near a seep and thin far from it.

**C4. The anchoring cell** (queue item 8, was round 43). A cell type whose part, in held
contact with the bed or a rock, is fixed to it by a joint the physics owns, at a standing
cost, released when the part's tissue can no longer pay. No genome field in the first cut:
the cell anchors when it touches. Without it C1 to C3 are scenery, since nothing can stay
on a rock in moving water.

**What it should do.** Make two ways of life, settled and drifting, that pay in different
places, which is the first step from a soup toward a world with a map. The clumping index
(`scripts/reads/clumping.py`) and depth by guild are the readings.

## Rung D: shapes and cells that serve A to C

- **The fin.** HANDOFF's queue already holds the water a fin can push on (lift on a panel,
  the reactive force of a bending body), each a tunable defaulting to 0. It comes before
  any new shape, because a thin box is a fin once the water lifts.
- **Ellipsoids in the physics** (queued as the old round 42): spheres and capsules collide
  and drag as their three half-extents.
- **Armour**, a cell type that lowers the yield of a bite on its part at a standing cost.
  Only after A shows bites happen. §5A.3 defers attack and defence, so this is the owner's.
- **Lift under the brain's control**: an effector output scaling a buoyancy cell's lift
  within its genome ceiling. One channel, no genome field, and the first vertical movement
  that costs no joint.

## The order, and what it costs

| Round | Build | New genome field | Refuses old configs | Days, build and screen |
|---|---|---|---|---|
| 43 | A: bite, `Eaten`, `Contact`, `Damage` | no | yes (three tunables) | 2 |
| 44 | B1 scent, B2 eyespot | no | yes | 2 |
| 45 | C1 shelf on the shallow streams, C4 anchor | no (a cell type id) | yes | 2 to 3 |
| 46 | C2 rock, C3 seep | no | yes | 2 |
| later | D, each behind its own proposal | the fin no, armour no | yes | |

Every row is a registry or a tunable change and none bumps the genome format, so the
inocula on disk stay readable throughout. Each round keeps the three-seed, pinned-arm
shape of round 42 at its budget, since a full seed there takes ten hours.

## What the owner rules

1. The order A, B, C, D, or another.
2. That predation is due, and that a bite reaches the reserve (A2).
3. The scent field as the first sense of others, inside D020, with the neighbour sense
   named and not built.
4. The places: the shelf first with the anchor, rock and the seep after; and the seep as a
   return of buried matter and not a source.
5. Anything in D the owner wants sooner.
