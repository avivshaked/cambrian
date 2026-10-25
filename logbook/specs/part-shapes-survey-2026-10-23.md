# Proposal: part shapes, where contact and the mirrored joint come before any new shape

*Ruled: D228 and D233 rule every decision below; D233's shape family overtook the shapes compared here. The file is kept as the record of its survey (the shape counts in round 62, and why a plate and a cylinder were not proposed). Moved from `proposals/` on 2026-10-25.*

*A draft for the owner, written at the coordinator's request, 2026-10-23. Shapes change what bodies can be, so they
are a world rule and nothing is built before the owner rules. Every claim about the code names its file. What is a
guess is marked as inference.*

## The proposal in one paragraph

The request assumed that every part is a box. It is not. The genome offers three shapes, box, sphere and capsule,
mutation moves a part between them, and in a world of round 62's build the three are roughly equally common. The shape
already sets a part's volume, its inertia, where its children attach, its lit area and the panels the water pushes on.
Two things ignore the shape. Contact treats every part as a ball, and a mirrored pair of limbs is joined at one place,
so paired fins stack. I think those two gaps cost bodies more than any missing shape does, and contact matters more
since D219 made eating a matter of touch. A limb's thrust, meanwhile, comes from its volume and reach under D135's
drive and not from its outline, so no shape makes a better fin. I recommend fixing contact and the mirrored joint first. After them, a tapered segment
is the one new shape I would build, with the ellipsoid second. A separate flat plate is not needed, because a box
already makes one.

## What the code does with shapes today

The three shapes live in `src/Evosim.Core/Shapes/StandardShapes.cs`. A genome's node names its shape, and each run's
genomes record it. Mutation changes a node's shape with a small chance each generation, 0.02 in round 62's mutation
settings, picking among the standard three (`src/Evosim.Core/Mutation/Mutator.cs`).

I counted the shapes named in the 73,022 genomes stored by the fast-rot world `r62f-h30-6201`. Look at how even the
split is: no shape has won. These are node shapes in every genome the run stored, not parts in the living crowd
weighted by number.

| Shape | Nodes | Share |
|---|---|---|
| box | 52,667 | 37% |
| sphere | 48,352 | 34% |
| capsule | 39,440 | 28% |

Where the shape counts and where it does not, read in the code:

- A part's volume and mass follow its shape, and so do its upkeep and build price, which go by volume.
- Its inertia follows the shape too. The solver computes it for each of the three from closed formulas
  (`src/Evosim.Dynamics/Creature.cs`), and the card reads the host's figures.
- Drag on the part follows the shape. Each shape lays out panels over its surface. The water pushes on each panel that
  faces into the flow, in proportion to the square of its speed along its normal
  (`src/Evosim.Core/Environment/FluidModel.cs`), and the card reads the same panels.
- Light follows it as well: a part's lit area by exposure is the shape's, and a test checks each shape.
- Contact does not follow the shape. Each part touches as the ball of its half-diagonal (D114, restated in D196),
  against other bodies, the bed and the coral (`src/Evosim.Dynamics/Contacts.cs`, `ContactBed.cs`). A flat part with
  half-extents of 0.3, 0.3 and 0.02 m touches as a ball 0.42 m in radius, nearly twenty times its own thickness.

Three things follow, and each limits what a new shape could buy.

The first is that a limb's thrust does not come from its shape. Since D135 a stroking limb is pushed by a rule of its
own (`src/Evosim.Dynamics/Fluid.cs`). The push grows with the limb's volume and its distance from the joint. It also
grows with the square of the brain's power times a fixed stroke rate of 2 radians a second. The push the panels would
give a real swing is switched off under that drive (D135's build notes). So a flat fin pushes no harder than a round
limb of the same volume. A flat box still meets far more drag broadside than edge-on, which matters for how a body
glides and turns, but it is no longer what moves a body.

The second is that only the faces leading into the flow meet drag. A rounded front meets less force than a flat one of
the same width. A tapered tail earns nothing in drag, because trailing faces count for nothing. Real streamlining
gains most from the tail, by keeping the flow attached behind the body, and this model does not see that. This is an
inference from the model's form, and nothing has measured it.

The third is that a body's top speed is set by its limbs' push against its front's drag. A narrower front for the same
volume would raise it a little. That is the one way a shape can make a body faster today (inference).

The swimmer search the theatre agent ran over the last 300 s of all twelve worlds of round 62 and the fast-rot screen
found no body that swims. The best jointed body moved 15 to 25 m in 300 s beyond the local drift, no further than the
best body with no joint. `proposals/propose-doing-must-pay.md` sets the finding out and asks whether the drive law
caps the speed or swimming does not pay. For shapes it means this. If the drive law caps it, a shape can only shave
the front's drag, and a change to the stroke rate or the push would buy far more. If swimming does not pay, no shape
will make bodies swim. Either way the search is no case for a new shape as a route to swimmers.

## Two gaps that matter more than a new shape

### Contact on the true shape

Since D219 a biter eats only what it touches, and a touch is a ball meeting a ball. D219's own risk note says a long
leaf can be touched a little off its surface. A flat frond is touched well off it, and a long capsule is touched near
its middle as if it were round. The shapes that make a good paddle or a good leaf are the shapes contact treats worst.
Fins collide as balls with their neighbours. A grip on coral forms off the coral's surface. And a mouth on the end of
a long part bites from a ball centred on the part's middle (inference from the contact rule).

Contact by shape would test capsules as capsules, and boxes as boxes or as a short row of capsules. Spheres stay as
they are. It changes the solver's contact pass on the host and the card's contact kernels, and every recorded world
would need the old rule kept behind a switch to replay. It is a new tunable, so older configs are refused. It costs
pace, since a capsule test does more arithmetic than a ball test (inference; to be measured). I put it at two to three
days of agent time, the card's identity checks included. It is the change that makes every shape mean what it looks
like.

### The mirrored joint

A genome edge can mirror its child, so that a body grows a left and a right fin. The speed agent found that the solver
joins the mirrored part where its twin is joined. The pair stacks into one limb in the physics, on the CPU and on the
card alike. Light, the support cost and the birth rule meanwhile count the pair as spread
(`logbook/specs/mirrored-anchor-finding.md` on the `mouth-point` branch). In a world it measured at 24,000 s, 74
bodies had a mirrored part, and all 66 pairs that development set apart were stacked in the solver. A pair of flat
fins therefore pushes from one place and cannot turn the body by beating one side harder (the finding's inference).
Any shape meant for paired fins is wasted until this is fixed. The fix is an engine change and already waits on the
owner's ruling, after round 62's dials.

## Candidate shapes

Each candidate says what it opens, what it costs in the genome, development, the solver on the host and the card, the
drag model and the theatre, and its size. One cost is common to all of them. Mutation picks a new shape from the
standard list built into the code, and the world's config has no say. Adding a shape to that list would change every
recorded world's mutation draws and break their replay. So the first new shape also needs a per-world list of the
shapes mutation may pick, defaulting to today's three. That is a new tunable, and it refuses every older config. A
genome that names a new shape loads only on a build that knows it.

The table sums them up. Look at the card column: while contact stays a ball, no new shape touches the card.

| Shape | Opens | Genome | Card, while contact is a ball | Agent time |
|---|---|---|---|---|
| Tapered segment | tails, tapering limbs, fronds | a taper field, so a format bump | none | about a day |
| Ellipsoid | flattened discs, smooth rounded bodies | a new name only | none | half a day to a day |
| Cylinder | rods with flat ends | a new name only | none | half a day |
| Flat plate | nothing a thin box does not | | | not proposed |

### The tapered segment

A tapered segment is a capsule whose two ends have different radii, so it narrows from base to tip. It opens tails
that thin toward the end, limbs that taper, and fronds that narrow. Real swimmers taper, and D199 kept the tapering
that chains of shrinking copies give for that reason. A single tapered part does it in one node rather than a chain.
In this drag model the taper saves nothing in drag, since the tail trails. It does put less mass and inertia at the
tip, so a beating tail costs less to swing (inference).

It needs a taper ratio on the genome's node, so the genome's format changes and older genomes are refused. In Core it
needs its volume, its surface point for attaching children, its inside test, its panels and its inertia. The solver
takes the inertia from a new branch in its formulas, and the card reads it from the host. The theatre needs a tapered
mesh in each drawing path that switches on shape (`unity/Assets/Theatre/SnapshotCamera.cs` is one). About a day, with
tests.

### The ellipsoid

An ellipsoid has three radii, where the sphere takes one radius from the mean of its half-extents and the capsule
takes a round cross-section (`StandardShapes.cs`). It opens flattened, rounded discs, a ray's or a flatworm's outline,
and smooth bodies whose front meets the water at an angle. The capsule already gives a long body a rounded front, so
the ellipsoid's gain in drag is small (inference). Its gain is the flat, smooth disc, which a box gives only with
corners.

It needs only a new name, with the three half-extents the genome already carries. Core needs the same five pieces as
the taper. Its inertia has a closed formula. The theatre can draw it as a scaled sphere. Half a day to a day.

### The cylinder

A cylinder is a rod with flat ends. It differs from a capsule only at the ends and from a box only in being round, and
the drag model would treat it much like either (inference). Half a day, but I do not see the niche it opens, so I
would not build it.

### No flat plate

A box with one half-extent far smaller than the other two is a plate. The shape's own remarks call it the strongest
paddle available, and parts already thin to the 0.02 m floor (D196). What a fin lacks is contact on its true shape, so
it stops colliding as a ball, and a mirrored partner that is not stacked on it. Both are above. A plate shape would
add a name for something the box already does.

## How shapes meet the new cell types

The proposal on cell types (`logbook/specs/cell-types-survey-2026-10-23.md`) suggests poison and fat first.

- **Poison** is charged on the touch that feeds. With contact as balls, a biter may feed on a poisoned frond it is not
  touching. Contact by shape makes the touch, and the dose, land where the bodies meet.
- **Fat** is bulk. A sphere holds the most volume for the least surface, so fat bodies should drift toward spheres,
  where shape costs least drag per volume (inference).
- **The grip** on coral forms when a part touches rock. As a ball, it may hold from off the coral's surface.
- **Cilia,** if ever built, push along a part's axis, and a long, tapered part has a clear axis to push along.
- **Leaves** earn by lit area, so flat boxes are good leaves already. Contact by shape would let a crowd of flat
  leaves pack closer without colliding as balls, which may raise the crowd an oasis holds (inference).

## The recommendation

1. The mirrored joint is fixed first, as already queued for the owner's ruling. No fin is worth building on until a
   pair can push from two places.
2. Contact on the true shape is built next. It makes today's three shapes mean what they look like, and since D219 it
   decides who eats whom.
3. If a new shape is wanted after those, the tapered segment, then the ellipsoid. The cylinder and a plate are not
   proposed.

The order follows what depends on what. The mirror fix and contact by shape both change the solver on the host and the
card, so they share the card's identity checks and land best together, between rounds. A new shape changes only Core
and the theatre while contact stays a ball, and can come at any time after the per-world shape list.

## The decisions for the owner

1. Whether the mirrored joint is fixed before any shape work. It is already on the list of engine changes waiting for
   the owner's ruling after round 62's dials.
2. Whether contact is built on each part's true shape, behind a switch, off by default so recorded worlds replay.
3. Whether a new shape is wanted at all after those two, given that box, sphere and capsule are already in the genome
   and all three are in use.
4. If one is wanted, whether it is the tapered segment, as recommended, with the genome format bump that its taper
   field brings.
5. Whether mutation's shape list becomes a per-world setting with the first new shape, as the replay of recorded
   worlds requires.
