# Proposal: the aquarium

**For the owner, 2026-09-11 evening.** Three world rules in one design, for three separate
rulings, and a change to the ruled sequence that the agent makes under the sequence grant
and records here and in HANDOFF. It supersedes `fable-propose-box.md` (the 2 by 2 layout),
whose build is kept as the shared volume's shape code and absorbed.

## What compelled it

The owner watched round 36 seed 1 in the theatre and saw bodies displaced: a jump, not a
death and a birth. It is the wrap. The box is 20 m long and 5 m wide and periodic on both,
and since D088's current carries bodies, every body crosses a seam about once every hundred
seconds (`wraps` in `r36-s1`: 1,143 per window at 1,154 alive). The rule was chosen in D077
for being cheap when nothing crossed a seam; the carrying current made it bite, and the
report's column said so for two rounds before anyone read it.

The owner also saw the crowd. Round 35's median nearest neighbour is 0.63 to 0.70 m in three
dimensions, a body every metre in the upper band: a bloom, not an ocean. And that is,
read as inference, why movement has never paid in this world: with food within a body
length in every direction a sitter eats as well as a swimmer, so the stroke has had nothing
to buy in thirty-six rounds. A dilute world is the one in which a sense and a stroke can
earn their price. A uniformly dilute world is a desert; an ocean is dilute on average and
dense in patches, so dilution only works with concentrators.

## Ruling 1: the container

A cylinder of water, the same 100 m² footprint (radius 5.64 m), 60 m deep, with a glass wall
all round: a static collider that stops a body where the seam used to teleport it. The
current becomes a gyre, a slow swirl about the axis with the vertical cells on top of it,
built from a stream function that vanishes on the wall so that it is divergence-free and
tangential at the glass by construction; the RMS knob keeps its meaning. The grid carries a
mask: cells whose centres lie inside the circle are live, advection and stirring stop at
the mask, a body reads its nearest live cell. The patches become rings (or sectors; the
agent's pick is two rings, centre and rim, because centre-against-rim is the ecological
axis a round tank has). The placer draws founders inside the disc. The report's footprint
columns, the positions reader and the snapshot views learn the shape; the theatre draws
the glass.

Fallback if the mask misbehaves in the digest: the walled square, 10 by 10 m at the same
area, a sine-mode current that vanishes on the walls in closed form, four corners as its
cost. An octagon is priced between them on looks and above both on work (its diagonal walls
cut cells like the circle's and its current has no closed form), so it is not offered.

## Ruling 2: the dilution

The area as a dial decoupled from the matter budget. Today the matter scales with the
area; the build adds a tunable that holds the total (`MatterBudgetUnits`, default "as
today" so every recorded config still reads), and round 38 runs the tank at four times the
area, 400 m² (radius 11.3 m), with the matter held at 6,000 units. Density falls by four,
the body count does not, and compute follows bodies and joints rather than cubic metres;
the grid grows to about 24,000 cells at 1 m, which is cheap beside the physics.

The risk sits at founding: a child costs 8 to 16 units and a founder reaches one matter
cell, so a field four times thinner can leave every founder short. The ledger screens it in
seconds (break-even density and R0 at the new concentration) before any arm runs; if
founding fails on the ledger, the third lever, bigger bodies for the same matter, is added
beside the dilution rather than instead of it.

## Ruling 3: the concentrators

A dilute world needs patches or it is a desert. Three come with the design and one is moved
up:

- **Light** already concentrates the producers and their detritus in the upper band.
- **Sinking and the bed** already make the floor a larder; the gyre's downwelling collects
  sinking matter where the flow goes down, which is physics and is measured from the grid,
  not assumed.
- **Corpses as objects**: `CorpseDecayPerSecond` above 0 (it exists, default 0), so a dead
  body is a parcel of matter that decays in place rather than dissolving into its cell at
  once. First value 0.005/s, the figure D086 recorded.
- **The bed with shape** (round 42 in the ruled ten) moves to right behind the dilution,
  because in a dilute world it is the difference between a desert and a coast.

## The sequence, changed

Under the owner's grant ("I'm giving you autonomy to change the sequence"), recorded in
HANDOFF's path with this date and reason:

| round | was | becomes | why |
|---|---|---|---|
| 37 | the 2 by 2 box | **the tank** (ruling 1) at today's area and matter, the limiter on if its check passes | the tear is the container's fault and the container is one change |
| 38 | a light sense | **the dilute tank** (rulings 2 and 3 minus the bed) | a sense only pays where distance means something; make the prize first |
| 39 | the stroke priced | **the bed with shape** | the strongest concentrator; a coast before the swimmers |
| 40 | ellipsoids | **a light sense** | the cheapest steering, now with something to steer toward |
| 41 | shading length scale | **the stroke priced alone** | pay for the chase once there is a chase |
| 42 | the bed with shape | ellipsoids in the physics | unchanged in content, later in order |
| 43 | the anchoring cell | the anchoring cell | pairs with the bed, now behind it |
| 44 | remaining prices | shading with a length scale | moved down: least urgent |
| 45 | the long arm | remaining prices | unchanged in content |
| 46 | | the long arm | eleven rounds rather than ten |

One build serves 37 and 38: the tank, the mask, the gyre, the matter tunable at its "as
today" default, the limiter tunable. Round 38 needs no new build, only a launcher. D079's
one-change rule is kept at the round level: 37 changes the container, 38 the density.

## The checks before any scored round

1. Core suite green; a digest pair on the tank at the default area against itself (replay
   identity on one thread), and a wraps count that reads 0 by construction.
2. A dead-pocket read of the gyre: the fraction of live cells whose speed is under a tenth
   of the RMS, from the field alone, before bodies.
3. The ledger at the dilute concentration: founding R0 above 1 at the seeded density, or
   ruling 2 is amended before it runs.
4. The limiter's own check from `fable-propose-limiter.md`: a replay of `r34-s5` with it on.
5. A smoke at 600 s with pictures: the glass, the gyre carrying, no body outside the wall.
6. The sitter-against-mover experiment in the grid tests rerun on the dilute field, so the
   movement rounds have their null number before they start.

## What it is not

Not a new fluid model: the gyre is a prescribed field like today's, not a solve. Not a
change to the goal rule, the prices, growth or the senses. Not a bigger population: the
matter is held. Not a sunset or a tide: the theatre's sun is fixed and the surface is a
picture.

## Standing answers

The box proposal's standing answers apply: refuse-rather-than-default on every new
tunable, the header carries the shape (`space tank r=5.64 m` or `shared 10x10x60 m`), the
area and the matter total, the reflection tests catch a missed wire, and every recorded
config still reads under the defaults.
