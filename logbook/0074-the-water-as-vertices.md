# 0074 — The water as vertices

**2026-09-07**  ·  D083, ruled, built, tested and smoked in one night, while round 29 ran

Round 29 was half done when the owner read the motility proposal (logbook/0073) and asked
what a depletion halo would do at the edge of a field cell. I answered from the code: a body
feeds from one cell, chosen by its root's height and position each half second, and a halo
would be a number on the body that travelled with it across the boundary. The owner's reply
was that the world's laws were unrealistic, and then a design in five lines. Put values on
vertices instead of cells, read any point as an average of the vertices around it, let
bodies be vertices, cull them so there are never too many, and let feeding reduce the
vertices by the weight each has at the mouth. Vents would add vertices. The floor would lock
them out. And, the line that mattered: creatures that stay in one place will deplete their
field.

I said what I thought, which was that it can work and that it is a known method, with one
correction and one warning. The correction is that vertices must carry amounts, not values.
A take from an average has no single place to subtract from, and a merge of two averages
loses whatever their volumes disagreed about, so the audit that closes at 0.0000% would open.
Put joules on the vertices and read a density as a kernel-weighted sum, and every operation
is a transfer again. The warning is that resolution is bought with count and the limit is
noise, not compute. A read fluctuates as one over the square root of the vertices it covers,
so the kernel and the count are chosen for about thirty in reach. The owner asked for a
quadtree for the lookup; the world is a column, so it would be an octree, and a hash grid of
kernel-sized cells is faster in a bounded box and iterates in a fixed order, which the
replay needs.

I also spent an hour recommending the wrong thing to myself. A fixed lattice with the stock
moved between vertices cannot cluster under the current, which is not divergence-free by
design (the remarks on `CurrentField` say why), and it gives the same resolution for less
risk. It is also a fine grid under another name, and not the world D076 had already named:
"literal particles down the line". The owner's ruling settled it before I had finished
arguing with myself, and I think the ruling was right. Vertices that ride the flow are the
model; the lattice was the comfortable approximation.

## What was built

One interface, `IMatterField`, that the world reads its water through, and two
implementations behind it. The cell field is `NutrientField` unchanged, addressed by depth
and patch as it has been since D061. The new one is `VertexField`. A vertex holds joules at
a position in D077's box. The density at a point is the sum over the vertices within one
metre of joules times a Wendland kernel weight, normalised to unit integral, so a read is a
mass density that integrates back to the mass it was read from. Feeding demands, freezes,
shares and takes over the neighbours under the rule Astra's review made exact. Demand is
spread by edible mass times weight, and each vertex freezes what it has. A body's share is
the weighted mean of its neighbours' shares, and the takes at any vertex cannot sum past
what it froze. The vertices sink, ride the current at the velocity the bodies feel as drag, and
diffuse by a seeded random walk from the field's own stream. The vent and the surface
influx found vertices of a fixed quantum at random positions in their plume. Burial removes
whole vertices resting on the floor at the rate. The chemical sense reads the kernel at each
part's position, which on a cell world is the arithmetic it always was and on a vertex world
is a gradient inside a patch for the first time.

A body now has a position in the economy. `World.Observe` takes the centre of mass whole,
the organism carries its x and z, and a `FieldPoint` carries a position and a patch so that
each field reads the half it understands. Every recorded config still describes its world:
the field model defaults to cells, and the header prints the model and its four numbers on
every run from now on. The knobs are `EVOSIM_FIELD`, `EVOSIM_FIELD_KERNEL`,
`EVOSIM_FIELD_MERGE`, `EVOSIM_FIELD_CAP` and `EVOSIM_FIELD_QUANTUM`; the report gained a
`vtx` column with the two counts.

Fourteen new tests. The one the field exists for puts two identical blind mouths in
identical water, one fixed and one walking half a metre a step, for forty steps. The sitter
ate 4 J and its water reads 0.000 J/m³. The walker ate 19 J and stands in fresh water, and
three metres below the sitter the density is 0.98 J/m³, so the hole has an edge. A full
vertex world with founders, feeding, exudation, death, an influx founding vertices, burial
removing them, a rolling current and mixing runs 3,000 s and closes its audit to 6 parts in
10⁸ and its matter identity exactly. The whole suite is 550 tests and passes.

## What the smoke found

The first smoke on worker 7, 200 s at the fast step, compiled and ran: the header carried
`field vertices`, the `vtx` column printed, the audit closed. It also showed the matter
field's vertex count falling from the 48,000 seeded to 1,238 at 100 s and 744 at 200 s. The
first build merged any two vertices that drifted within a quarter metre, every step. The
mixing walk at 2 m²/s scatters every vertex by 1.4 m a step, so about a third of them found
a partner each step and the field coarsened to a few hundred fat vertices. A mouth's kernel
then held half a vertex on average, and the matter gate was refusing conceptions in water
that was full on average. It took one look at the column to see and one change to fix. A
deposit now joins the nearest vertex within the kernel rather than founding one, and that
bounds the count; merging happens only over the cap. A test now holds a seeded
lattice under a strong walk for twenty steps and asks that no vertex was lost.

The second smoke ran the fixed world for 3,000 s at the fast step on seed 1. The counts
held: 1,340 detritus vertices and 39,000 matter vertices at 1,600 s against a cap of
100,000. The audit read 0.0000% on every row. The population reached 891 by 2,500 s, with
`mat blk` per body about what round 29 shows at the same age. Two readings are new and go
into round 30's pre-registration rather than being tuned away tonight. The edible density at
the bodies runs in the hundreds of joules per cubic metre while the layer average is tens.
Exudate lands where the producers stand, and a producer stands still; in the cell world the
same joules were spread over 25 m³. And the three absorptive founders of this seed died
without breeding. No stomach line had formed by 2,500 s, and the exudate accumulated to
60,000 J where round 29 seed 1's stomach line was eating it as fast as it arrived. One
realisation at the screening step says nothing about the world. A passing seed runs for
10,000 s next, and the round itself screens five seeds at the fast step before anything runs
at 0.01. That is what the owner asked for: fail fast where it is appropriate.

## The second screen, and the second fix

Seed 2 ran 10,000 s at the fast step with sideways mixing on, and bred once. The forty
founders stood in water with the matter density at the layer's normal value, the gate
refused every conception, and the world dwindled to one body. The arithmetic was in front
of me the whole time and I had not done it. A child costs 8 to 16 units of matter at these
prices, which is the locked matter per living body in every run on file. The old cell was
5 m by 5 m by 1 m and held 25 units at the seeded density, so a parent in fresh water could
afford one child. A 1 m kernel reaches 4.2 m³ and 4 units. No founder could ever afford a
child from the seeded water, and seed 1 had bred only on the fat vertices its dead founders
left behind. The matter field now reads through its own kernel of 1.8 m, whose reach of
24.4 m³ is the old cell's volume to within 3%, so the gate binds where it bound in the base
world and the thing the round changes is the detritus. Seed 2 runs again.

## A note of my own

The owner designed this field in five lines of a chat message at eleven at night, with the
spelling of "vertices" going three ways, and every line of it was right except the one I
corrected. I had the same physics in front of me all afternoon and offered a number on a
body. The lesson I take is the one 0073 already recorded from the other side: the person
who asks "is this world realistic" is asking the load-bearing question, and the right
answer to it is usually a model, not a knob.
