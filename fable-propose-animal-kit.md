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

## Rung A0: the part made by a rule, not by a plan

*Added 2026-09-22 evening from the owner's two points in conversation: creatures do not
partially eat each other, a cell is the unit of death, and plants grow leaves by a
programme where animals do not regrow a limb.*

Development expands the genome's recursive graph once into a fixed adult plan, and growth
(D087) scales that plan from the birth fraction to adult size. Every body is determinate:
it has the parts its plan says, and nothing is ever added or, until rung A, lost.

**One gene per node, `Growth: Determinate | Indeterminate`.** Determinate is today's rule:
the count is fixed at development, a lost part is gone, growth only scales what exists.
Indeterminate: the node's count is bounded rather than fixed, and the body adds another
module of it while its reserve stands above a threshold (paid at the tissue price like all
growth) and drops one after a long enough starvation. A lost module is a count below the
rule, so it regrows when affordable. Both are bounded as now by `MaxParts` 16 and
`MaxDepth` 8, by D099's silhouette cap (a body earns its hull's surface over four, so the
sixteenth leaf pays least) and by D101 (an overlapping fold is a stillbirth); those three
are what stop indeterminate from meaning "sixteen leaves and win", and round 41d's bush is
the case they were built for.

A gene and not a world rule, so that selection decides which lineages are plants. My
expectation, marked as inference: the photosynthetic lines go indeterminate (a leaf pays
for itself) and the jointed lines stay determinate (a muscle pays only when it moves the
body), which would be the first time this world evolves the plant–animal difference
rather than reading it off the cell-type table.

**What it costs.** Genome format 7; every stored genome refused and re-extracted, the
usual rule. A body's parts become a property of its history, so the checkpoint carries
each body's module counts, and adding a module rebuilds the articulated body the way a
growth resize does. The GPU port's link ceiling is paid by every thread, so a plant world
at sixteen parts sets it at sixteen for everyone; bounded and to be measured, and no
change to D105. Its own round, before the bite, with predictions on body size tracking
reserve, the cap binding, and the share of indeterminate nodes by guild.

## Rung A: the mouth kills, and the mouth consumes

*Rewritten 2026-09-22 evening. The earlier text had the bite take a body's reserve from
the outside, which the owner rejected: creatures do not partially eat each other. What
follows replaces it and `fable-propose-predation.md`'s rules 4 and 5.*

A mouth is whatever cell can consume, a claw whatever cell can hurt, and both are
attributes of a cell rather than cell types (the owner, 2026-09-22 evening: killing organs
generalise, every cell has attributes, and each has an economy). Four attributes, per
node in the genome, each priced as upkeep:

| attribute | what it is | today | priced per |
|---|---|---|---|
| attack | damage per second to the part of another body this part is in held contact with | 0 on every cell | unit of attack per area |
| intake | charged units per second taken from a corpse in reach | the consumer cell's, implicit | unit |
| protection | damage per second absorbed before health suffers | 0 | unit per area (armour) |
| toughness | health per unit of volume | a default that leaves today's bodies as they are | unit |

**Health is state, not a gene.** Every part carries a health pool, volume times toughness,
full at birth and at a module's growth. Damage net of protection drains it; it refills
from the body's reserve at a healing rate that costs energy, so a survivor pays for being
bitten and an empty body cannot heal. Founders draw attack and protection at zero, so a
world starts peaceful and a claw has to be discovered by mutation.

**A1. The kill.** A part whose health reaches zero dies. It and everything hanging from it
leave the body as a corpse placed where the part was, carrying the part's tissue and its
pro-rata share of the body's one reserve (its volume over the body's volume); the
attacker gets nothing from the kill itself. On the root it is the whole body, death
`Eaten`. What is left keeps living on its remaining parts and income, and dies `Starved`
as now if it cannot pay its way or falls under the newborn mass floor; an indeterminate
module regrows when affordable, a determinate part is gone. "A bite kills in a step" is
the case of a strong attack on an unprotected part; a weak attack on an armoured part
never kills, so the arms race is open to selection from the first round and whether it
starts is a reading. The four prices come from a ledger screen (D069) of a claw against a
leaf before the round, never from the launcher.

**A2. The consumption.** A corpse is an object with a position, carried by the water (D086,
D088) and decaying into marine snow at the corpse rate, which is how the dead have fed the
absorptive guild since round 32. A mouth within reach of a corpse takes charged units from
it at a rate set by the mouth's size, into its own reserve, with the waste share to snow
the way absorption already loses a share; a corpse is finite, mouths in reach share it,
and what nobody eats decays as before. Reach is a distance test each metabolic step; a
corpse is passive and needs no physics. The corpse decay rate sets the scavenging window
(a few minutes at 0.005/s) and the round says which it ran.

**What that gives.** A body that kills a root and eats the corpse is a predator. A body
that kills a leaf of an indeterminate plant and eats it while the plant regrows is a
grazer. A body that only finds corpses is a scavenger, and it can found without ever
killing, which is the niche this world has never had and the one likeliest to appear
first. A killer that kills more than it can eat feeds the scavengers and the snow.

**A3. `Contact` and `Damage` wired**, behind `SenseContact` and `SenseDamage`, off by
default. `Damage` reads "a part of mine died this step"; `Contact` stays as declared. A
scent of corpses belongs to rung B's field.

**What the round asks**, each on its own prediction: whether a scavenger line founds,
whether a killer line founds, whether an indeterminate plant survives grazing, and whether
attack or protection appears by mutation and pays. dt 0.01 from the first screen, because
a held contact is a property of the step. The four attributes and A0's module gene are
all per-node scalars, so one genome format bump (7) carries both rungs and the stored
genomes are re-extracted once; in round 44 the attributes sit at their zero defaults and
change nothing, and round 45 turns on their economy. The tunables (the four prices, the
healing rate, the reach) refuse older configs as usual.

**What the owner has said** (2026-09-22 evening): a bite alone does not make a hunter,
behaviour needs rung B's sense, and A is read as what the payoff does without one; the
cell is the unit of death; mouths kill and consume as two things; the eight mechanics
above were put in conversation and "most" liked, with the module gene and the round order
the two open answers.

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
| 44 | A0: the module gene, determinate or indeterminate per node; the four attributes carried at zero | yes (format 7, once for both) | yes | 2 |
| 45 | A: health, the kill, the corpse, intake, `Eaten`, `Contact`, `Damage`, the four prices | no (in format 7) | yes | 3 |
| 46 | B1 scent (fed by the living and by corpses), B2 eyespot | no | yes | 2 |
| 47 | C1 shelf on the shallow streams, C4 anchor | no (a cell type id) | yes | 2 to 3 |
| 48 | C2 rock, C3 seep | no | yes | 2 |
| later | D, each behind its own proposal | the fin no, armour no | yes | |

Only A0 bumps the genome format; every other row is a registry or a tunable change. Each
round is three seeds on the farm out of Unity at round 43's shape, a seed in about half an
hour at 16 threads, so a round is an evening.

## What the owner rules

1. ~~The order A, B, C, D, or another.~~ Ruled 2026-09-22 evening: A, B, C, D.
2. ~~That predation is due, and that a bite reaches the reserve.~~ Replaced: the cell is
   the unit of death, the mouth kills and consumes as two things (rung A as rewritten).
3. **Open:** the module gene of rung A0, one per node, bounded by the caps, selection
   deciding who is a plant; A0 as its own round before A; the four cell attributes
   (attack, intake, protection, toughness) as the set, health as state healed from the
   reserve, and one format bump carrying A0 and A together.
4. ~~The scent field as the first sense of others, inside D020, with the neighbour sense
   named and not built.~~ Ruled with 1.
5. ~~The places: the shelf first with the anchor, rock and the seep after; and the seep as
   a return of buried matter and not a source.~~ Ruled with 1.
6. Anything in D the owner wants sooner.
