# 08 — The water as vertices

[Piece 06](06-the-producers-feed-the-water.md) left the world with a food chain and a
question. Producers feed the water, stomachs live on what they leak, and the goal rule was
met. But nothing had ever paid for moving. Every round on record ended with the swimmers
gone, and the reason was never that swimming was too expensive. It was that there was
nothing to swim to.

This piece is about the change that gave the water a shape a body could move through. It
covers how the change was built so that the books in
[piece 07](07-the-world-keeps-two-books.md) still balance, and what the first rounds on
it found.

## Water that is the same everywhere

Until September 2026 the water was a grid. The world was cut into layers a metre deep and
a few patches across, and each cell held one number: the joules of detritus in it. A body
fed from the cell its centre fell in, at the cell's average density. Two bodies a metre
apart in the same cell saw the same water. A body that sat still for a lifetime and a body
that crossed the cell every minute saw the same water. Movement could change which cell you
were in, and inside a cell it changed nothing.

That is a fine model of a well-stirred tank and a poor model of the sea, and the owner said
so in one message: the world's laws were unrealistic. The design that followed was five
lines long. Put the values on vertices instead of cells. Read any point as an average of
the vertices around it. Let bodies be vertices, so they ride the current. Cull the vertices
so there are never too many. And let feeding reduce each vertex by the weight it has at the
mouth, so that a creature that stays in one place depletes its own field.

## Amounts, not values

One correction was made to that design before it was built, and it is the whole of what
piece 07 is about. A vertex cannot carry a value. If it did, a mouth taking from the
average of its neighbours would have no single place to subtract from, and two vertices
merging would lose whatever their volumes disagreed about. The audit that closes at
0.0000% would open at the first meal.

So a vertex carries an amount: joules, at a position. The density at any point is a sum
over the vertices within reach: each one's joules times a weight that falls smoothly from
the point to zero at the edge of the reach. The weights are scaled to integrate to one
over the volume. A read is then a mass density that integrates back to the mass it was
read from, and every operation on the field is a transfer of joules between named places.
[`DESIGN.md` §5A.2c](../DESIGN.md) owns the kernel and its two radii, one for detritus and
a wider one for the matter a child is built from.

Feeding keeps the rule the cell world already had. Each mouth declares what it would take;
each vertex freezes what it holds; a mouth's share is the weighted mean of its neighbours'
shares; the takes at any vertex cannot sum past what it froze. The difference is only that
the neighbours are now the vertices within a metre of the body's centre of mass rather
than the cell the body happens to be in.

The water moves in three ways, and each is a transfer of position rather than of joules.
Vertices sink at the detritus sink rate. They ride the current at the velocity the bodies
feel as drag. And they take a seeded random walk, whose step is set by the mixing
diffusivity, so that the field diffuses the way the cell world's did and replays the way
everything else does. The vent founds vertices of a fixed quantum at random positions in
its plume. The floor buries whole vertices resting on it. A body that dies becomes joules
deposited where it died, joining the nearest vertex within reach or founding a new one.

## What a still body does to its water

The test the field was built for puts two identical blind mouths in identical water, one
fixed and one walking half a metre a step. After forty steps the sitter has eaten 4 J and
sits in water that reads zero. The walker has eaten 19 J and stands in fresh water. Three
metres below the sitter the density is still most of what it was. The hole has an
edge. That edge is the first gradient inside a patch that the chemical sense could ever
read, and it is what a body would have to move to escape.

## The four faults

The field was built, tested and run in one night, and fast screens over the following day
found four faults in it. They are worth listing because each is a way a field of amounts
can be wrong while looking right.

The first was collapse. The first build merged any two vertices that drifted within a
quarter metre, every step. The mixing walk scatters every vertex by more than a metre a
step, so a third of them found a partner each step, and 48,000 vertices became 744 in
200 s. A mouth's reach then held half a vertex on average. Deposits now join the nearest
vertex within reach and merging happens only over the cap.

The second was reach. A child costs 8 to 16 units of matter, and the old cell held 25 at
the seeded density; a 1 m kernel reaches 4. No founder could afford a child from fresh
water, and a screen bred once in 10,000 s. The matter field now reads through a wider
kernel whose reach is the old cell's volume, so the conception gate binds where it bound.

The third is piece 07's story. The take spread a price over the vertices in reach by
weight, capped the nearest at what it held, and never asked the rest for the shortfall;
conception booked the full price. Matter was created at every birth while the energy
audit read clean. The take now fills across the neighbours, conception books what was
taken, and the matter identity has a column of its own.

The fourth was a bound. The fill stopped after eight passes, and a price close to the whole
reachable stock across thirty vertices needs a pass for every vertex it caps, so hundreds
of conceptions were refused as short. The bound is now the neighbour count.

Four faults in a day is not a sign of a bad design. It is what happens when a conservation
rule is enforced by a column that prints on every row. Each fault was one glance wide once
the column existed, and none of them would have been visible from the population.

## What the first round found

Round 30 ran the vertex world at a cheaper price for neurons and strokes, five seeds,
30,000 s each ([logbook 0075](../logbook/0075-the-vertex-world-at-the-new-price.md)).
Every seed met the goal rule, the first round to do so. Both identities closed at every
sample. The standing detritus at the end was a tenth to a twentieth of the cell world's,
because a stomach beside a producer eats the exudate where it lands. The
population was the cell world's to within 3%. The vertex world is now the base.

And no seed kept a swimmer. One jointed line lasted three lifetimes, fed as well as the
sitters, and moved no faster than the drift. The owner's hypothesis was that the field had
too few vertices for a mover and a sitter to see different water. Three experiments
([logbook 0076](../logbook/0076-the-hole-refills-in-five-seconds.md)) measured the field
instead of arguing about it, and found the diagnosis right and the mechanism different.
The count moved only the noise. What erased the prize was the mixing rate. At 0.2 m²/s the
hole a sitter eats is refilled in about five seconds, faster than the mouth drinks. The
stirring carries food to the sitter so well that it triples the sitter's income. At a
tenth of the rate the hole is real and a swimmer eats two and a half times a sitter's
meal. The water was the right shape and it was stirred too hard to keep it.

That is where the piece ends, because that is where the work is. The round after this one
stirs the water less, and it is the first round in the campaign in which sitting still has
a measured cost.

## Afterword

The vertex water lasted two rounds. Round 31 stirred it less and found the hole in the
water at large rather than at the sitter, and no swimmer alive to take a prize of any size.
The same night the owner asked where a hole lives in a field of vertices. It lives at the
vertices, because a take is spread over them by weight, so a body beside a vertex pays for
a meal eaten a metre away. The remedy is a source at every place a body can be, which is a
grid of cells. The water is now that: each cell an amount, each mouth draining the one cell
it stands in, Fick's law between neighbours, the current carrying cells downstream, and a
corpse a particle that sinks and decays into its cell. Everything piece 07 says about
amounts and transfers carried over unchanged, which is why the change took one evening and
the books closed on the first smoke. [Logbook 0078](../logbook/0078-the-water-as-a-grid.md)
is the build and [D086](../DECISIONS.md#d086) the ruling.

## What here is inference

- **"Nothing to swim to" as the reason movement never paid** is the author's reading of
  the rounds through round 29. Those rounds measured the cost side and left the prize side
  open; D081 and the withdrawn motility proposal say so in their own words.
- **The kernel read as the first gradient the chemical sense could use** is the author's;
  no round has yet selected for a sense that follows it.
- **"Four faults in a day is what enforcement looks like"** is the author's judgement.
- **The vertex world as the base** is the owner's ruling on the round's reading (D083's
  index row), reported here, superseded by D086 on 2026-09-09.
- **The afterword's reading of round 31** is the author's; logbook 0077 holds the numbers.

## Where it is

| file | what it holds |
|---|---|
| [`VertexField.cs`](../src/Evosim.Core/Environment/VertexField.cs) | the vertices, the kernel, feeding, transport, founding, burial, culling |
| [`IMatterField.cs`](../src/Evosim.Core/Environment/IMatterField.cs) | the interface the world reads its water through; the cell field and the grid are the other two implementations |
| [`GridField.cs`](../src/Evosim.Core/Environment/GridField.cs) | the grid that replaced this piece's subject (D086) |
| [`VertexFieldTests.cs`](../src/Evosim.Core.Tests/VertexFieldTests.cs), [`VertexFieldExperiments.cs`](../src/Evosim.Core.Tests/VertexFieldExperiments.cs) | the sitter and the walker; the sweeps over count, mixing and speed |
| [`DESIGN.md`](../DESIGN.md) §5A.2c | the three representations, the kernels, the walk's two diffusivities |
| [`DECISIONS.md`](../DECISIONS.md) D083, D084, D085, D086 | the ruling, its amendments, the mixing setting, the water stirred less, and the ruling that superseded it |
| [logbook 0074](../logbook/0074-the-water-as-vertices.md), [0075](../logbook/0075-the-vertex-world-at-the-new-price.md), [0076](../logbook/0076-the-hole-refills-in-five-seconds.md) | the build and its faults, the first round, the experiments |
