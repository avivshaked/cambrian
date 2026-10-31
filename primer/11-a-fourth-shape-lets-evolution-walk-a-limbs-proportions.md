# 11 — A fourth shape lets evolution walk a limb's proportions

[Piece 01](01-a-graph-that-grows-a-body.md) showed how a genome grows a body. Each node of its graph is a kind of
part, with a size, a joint and a shape. Development copies the node into the body as often as the graph says. [Piece
02](02-from-a-tree-of-boxes-to-something-that-moves.md) was written when every part was a box, and notes that parts
can now also be spheres and capsules.

This piece is about a fourth shape, added in late October 2026. It is one formula with three genes of its own, so a
part's proportions can change a little at a time from parent to child. It comes with its own rule for how the water
pushes it, and with a way of touching built for its true outline. It passed its screen on the night of 29 October and
joined the world the project's next film is made from. A screen, here, is a short set of worlds run to settle one
question before a change joins a round.

The screen showed that the new shapes are easy for evolution to reach, and that they last. Nothing yet shows that
evolution prefers them. The end of this piece says why.

## Three shapes gave evolution a menu and no road

Before the family, a node named one of three shapes. Mutation could swap the name with a small chance at each birth,
and nothing lay between the three. A part was a box or a sphere. A child could not be a little rounder than its
parent.

The menu was used evenly. A round is a set of worlds launched together to answer one question. In October the project
counted the shapes named in the 73,022 genomes that one world of round 62 had stored. Boxes were 37% of the nodes,
spheres 34% and capsules 28% ([the survey](../logbook/specs/part-shapes-survey-2026-10-23.md)). No shape had won.

The same survey listed where a part's shape counted. It set the part's volume, and so its mass, its upkeep and its
price. It set the part's inertia, the drag panels the water pushes on ([piece
03](03-what-it-means-to-push-against-water.md)), and the area it shows to the light. Two things ignored the shape, and
they were the two that decide a fight or a swim. A limb's stroke pushed by its volume and its distance from the joint,
so a flat fin pushed no harder than a round limb of the same volume. And contact treated every part as a ball.

My reading is that those two blind spots left the choice among the three shapes close to neutral. That would fit the
even split. Nobody has measured it.

The owner watched the bodies and found that the limbs read as sticks ([D228](../DECISIONS.md#d228)). In the same
ruling the owner asked for more variety of form than the three shapes give, added in a way that makes sense.
[D233](../DECISIONS.md#d233), ruled on 25 October, is the answer: one shape whose genes vary smoothly. A lineage can
then walk from slab to lens to egg to tapering rod without a jump.

## One formula runs from a block to an egg

The family starts from a curve on a flat page. The points where |x|ᵖ + |y|ᵖ = 1 form a circle when p is 2. As p grows,
the circle swells towards a square. When p is 1, the curve is a diamond standing on a corner. Write p as 2/ε and the
exponent becomes a dial: ε near zero gives the square, ε of 1 the circle, and ε of 2 the diamond. That curve is a
superellipse.

A superellipsoid takes the idea into three dimensions with two dials. One exponent shapes every slice across the
part's long axis, and the family calls it the section. The other shapes the outline along that axis, and the family
calls it the profile. With equal half-extents, both dials at 1 give a ball, and both near zero give something very
close to a box. The formula is written out at the head of
[`Superellipsoid.cs`](../src/Evosim.Core/Shapes/Superellipsoid.cs).

The family adds a third gene, the taper. Each slice is scaled by a factor that grows steadily along the length. The
two ends then stand in the taper's ratio, while the middle keeps the size the node gives. A taper of 1 is no taper.
Above 1 the part widens towards one end, and below 1 towards the other.

Inside D233's ranges the shapes run from a flat-ended block to an egg. Their slices run from a rectangle through an
ellipse to a diamond, and one end can be up to four times as wide as the other. The general formula reaches further.
Once the profile exponent passes 1, the ends begin to point, and the solid becomes a spindle. D233 stops the profile
at 1, so the world's ends round but never point.

Every member inside the ranges is convex, and the build's tests check that across the whole range ([the build
record](../logbook/specs/shape-family-build.md)). The contact test below works only on convex bodies. The part's size
still comes from the node's three half-extents, as for every other shape. The form genes say only what fills that
size.

## The proportions are genes that take small steps

Each family node carries its three genes beside its size. When a world turns on the form walk, each family node may
step all three at once at a birth. A step is a small random draw on the logarithm of each gene, reflected back if it
would leave the range ([`Mutator.cs`](../src/Evosim.Core/Mutation/Mutator.cs)). A step on the logarithm changes a
proportion by a factor, so a thin part and a fat one move by the same share.

Entering the family is not a jump either. When a shape mutation turns a part into a family part, the part starts at
the member nearest its old shape. A box enters as the family's most box-like member, a sphere as its rounded one, and
a capsule as a round-sectioned rod. From there the genes can wander. A part that leaves the family drops its genes,
and the build record gives the starting values.

Two choices kept the past readable. The shapes that mutation may pick became a list each world sets, defaulting to the
old three, so every recorded world still draws what it drew. And only a genome that holds a family node is written in
a new format. Every other genome is written byte for byte as before.

The founders of the first world to use the family carry none of it. So every family part in that world is a mutant's,
and the new shape has to win its place (D233).

## Contact on the true shape was built, and the film's world still touches as balls

Since [D219](../DECISIONS.md#d219), a biter eats only what it touches, so a touch decides who eats whom ([piece
09](09-a-mouth-must-touch-what-it-eats.md)). Under the old rule each part touched as the ball of its half-diagonal,
the line from its centre to a corner. The survey's example is a flat part 0.6 m square and 4 cm thick. It touches as a
ball 0.42 m in radius, nearly twenty times its own thickness. The shapes that make a good paddle or a good leaf are
the shapes a ball treats worst.

D228 ruled that contact be tested on each part's true shape, behind a switch that is off by default. The build gives
every part a convex core and a margin, and the part is the core swept by a ball of that margin. A sphere's core is the
point at its centre, a capsule's is a segment, and a box's is the box shrunk by its margin on every side. The gap
between two cores comes from GJK, a routine named for its three authors. It finds the distance between two convex
shapes by asking each for its farthest point in a given direction
([`ConvexCore.cs`](../src/Evosim.Dynamics/ConvexCore.cs)).

A family part's core took two tries. The first ruling moved 98 surface points inward along their normals by the
margin. Near a sharp corner those moved points stand outside the true inner body, so the swept core bulged past the
real surface. The bulge was a fixed share of the margin, and no margin made it small.

The second ruling, the same day, clipped each point into the solid eroded by the margin. That is the set of points at
least the margin's distance from the surface. Swept by the margin, this core rounds every corner by the margin and no
more, as a box's shrunk core does. A family part then touches as a box of the same outline would. The build record
calls that the fair place D233 asks for.

Getting the clip right took two days of rulings and wrong turns. The margin had to shrink to 0.98 of its first value.
Otherwise a plate's core went flat, and it touched the bed on eight springs where a box touches on four. Plain
alternating projection crawled where neighbouring clipping planes were nearly parallel, and one part's core took about
a third of a second. The clip now solves for the nearest point directly, in about 12 ms for any form, and a cache
keeps the cores already built. On the bed a family part touches at eight corner points, so the number of springs never
grows.

Then, on 28 October, [D252](../DECISIONS.md#d252) kept contact on spheres in every world. The owner had asked for
richer limb shapes, with the water's response worked out ahead of time and the collision kept simple. True-shape
contact stays behind its switch, built and tested. The screen and the film's world both run with it off.

So in the screen a family part touched as a ball. The build did fit that ball to the part. It is the ball of the
solid's full reach, because a strongly tapered part's wide end stood outside the old half-diagonal ball ([the build
spec](../logbook/specs/shape-build/spec.md)). The true shape now decides a family part's mass, inertia, drag, light
and push. It does not yet decide what the part touches. The gap the survey ranked first is still open, for every
shape.

## The water pushes a limb by its width

The other half of the family is its fin law, which [piece 10](10-a-fin-pushes-the-water-by-its-width.md) tells in full.
[D229](../DECISIONS.md#d229) made a stroking limb's push follow its shape across the swing. A flat blade swung
broadside moves water in proportion to the square of its width. So a wide fin moves far more water than a stick, and
it costs more to beat.

For a family limb the build slices the part along its length, as [D231](../DECISIONS.md#d231)'s strip law does. At
each distance from the joint it reads the slice's width across the stroke. It adds up the square of that width for the
water moved, and the same sum weighted by distance for the turn it gives about the joint.

D252 asked for a table of about 64 swing directions, read by interpolation at every step. [D255](../DECISIONS.md#d255)
replaced the table the same day. Under the powered stroke that every world drives, each joint's stroke direction is
fixed in the limb's own frame for life. So the build computes one exact value for each degree of freedom when the body
is built, and growth scales it. The step reads two numbers a degree.

Fairness ran through this part too. One draft read a family part's width from its outer calipers, as the box's is
read. The coordinator ruled against it. A lens moves about two thirds of the water its bounding plate would, so
calipers would hand the family thrust it had not earned. A test now holds the family's most box-like member within 1%
of the box's own value, and the build lands 0.85% under. Its worst error against a fine reference is 1.22% over 170
cases, inside D255's 2% gate.

## The screen found the shapes reachable and lasting

The screen, `r63s`, ran the film's world on two seeds for 24,000 simulated seconds each, nearly seven hours of world
time. A seed is the random start of a run. The world was arm B of round 63, which the owner chose as the film's world
([D259](../DECISIONS.md#d259)). The screen listed the family, turned on D229's fin law, and let the form genes walk at
0.08 a node.

Its control was arm B's own runs on the same seeds. The new build replays those runs bit for bit with every new switch
off ([the read](../logbook/specs/r63s-read/README.md)).

The pre-registration set three gates. Grazers, the bodies with mouths, had to survive. At least a tenth of jointed
bodies, those that grow a part on a moving joint, had to carry a family limb at the end. And moving grazers had to
swim at no less than 0.8 of the control's median speed.

The table gives the last two gates on each seed. Look at how close to 1 the speed ratio sits.

| Seed | Jointed bodies with a family limb | Moving grazers' median speed, screen over control |
|---|---|---|
| 6202 | 0.193 (141 of 732) | 0.978 |
| 6203 | 0.180 (109 of 605) | 0.984 |

Grazers survived on both seeds. Every gate passed, so the family and its fin law went into the film's round under the
plan's rule ([D254](../DECISIONS.md#d254)). A feature that passes its gate joins without a further ruling. [Logbook
0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md) tells the night.

Pace had been read before the screen. On the processor, a crowded world with every jointed node turned into the family
stepped 1.4% slower. On the graphics card, the family's world timed within the sitting's own spread, though at 600 s
almost no body carried the family yet.

The read also found a fault in the solver. More pieces of remains left through the top of the world in the screen than
in the control. Each was one of a pair of twin pieces cut from one body at one place. When two centres coincide, the
contact rule falls back to pushing both straight up, and the pair rises until it leaves the world. That this is the
whole launch is the read's inference. No living body left the world this way. The defect is older than the family, and
its fix is a change of world rule, which is the owner's. Why family bodies leave more such twins is not known.

Nobody has seen a family limb yet. At the scale of a whole world every body is a marker a few pixels across. The
theatre, the project's viewer for recorded worlds, still draws a family part with a box's mesh. Seeing the shapes
waits for the theatre's next piece.

## Nothing yet says selection favours them

The gates show that the shapes are reachable and that they last. They do not show that evolution prefers them, and the
read says so first. Few grazers carried a family limb: 22 of about 3,400 on seed 6202, and 40 of about 2,900 on 6203.
On 6202 the carriers swam slower than the other grazers, at 0.136 against 0.248 m/s. On 6203 they swam level.

The genes had barely moved. On 6202 the median family limb stood 0.108 of a range's log width from where it entered,
and on 6203 it had not moved at all. A part enters the family only by shape mutation, at 0.02 a node, and about a
third of those draws land on the family. The pre-registration warned that mutation alone might carry the share past a
tenth in 24,000 s. The read finds that mutation alone fits everything it saw, and marks that as inference.

What selection would look like is still to be seen. My guess is that it would show as form genes drifting the same way
across lineages and seeds, away from the box, ball and rod they entered at. It would also show as carriers that eat or
swim better than their neighbours. Neither has been measured.

It may also wait on true-shape contact, since a flat family leaf is still touched from well off its surface. That is a
guess as well. Turning that switch on in a round is the owner's ruling.

## What here is inference

- **The three shapes as close to neutral**, because push and contact ignored shape, is the author's
  reading of the survey. The even split fits it, and nothing measured it.
- **The plain-words account of the superellipse** (circle, square, diamond) and **the spindle beyond a
  profile of 1** are the author's reading of the formula in `Superellipsoid.cs`. The members named in
  `PartForm.cs` (block, egg, rectangle, ellipse, diamond) are the code's own.
- **GJK as "named for its three authors"** is background knowledge. No source in this repository says it.
- **"Two days of rulings and wrong turns"** is the author's count from the build record's dates
  (2026-10-25 and 2026-10-26).
- **The twins' launch as the whole cause** is the read's inference, reported here. It fits the twins'
  identical states, and no twin was traced step by step.
- **Mutation alone carrying the share past a tenth** is the read's and the pre-registration's
  inference, reported here.
- **What selection would look like**, and **whether it waits on true-shape contact**, are the author's
  guesses.

## Sources

The literature review holds no paper on part shapes or superquadrics, so this piece cites no literature key.
Everything below is the project's own record.

| source | used for |
|---|---|
| [D228](../DECISIONS.md#d228) | the owner's ask for more variety of form; limbs that read as sticks; true-shape contact behind a switch |
| [D229](../DECISIONS.md#d229), [D231](../DECISIONS.md#d231) | the fin law by width; the strip law along a limb |
| [D233](../DECISIONS.md#d233) | the family's ruling: genes, ranges, the world's shape list, formats, founders without it |
| [D252](../DECISIONS.md#d252), [D255](../DECISIONS.md#d255) | contact kept on spheres; the table replaced by one value per degree of freedom |
| [D219](../DECISIONS.md#d219), [D254](../DECISIONS.md#d254), [D259](../DECISIONS.md#d259) | a touch decides eating; the plan's gate rule; the film's world |
| [part-shapes survey](../logbook/specs/part-shapes-survey-2026-10-23.md) | the shape counts, where shape counted, the plate touching as a ball |
| [shape-family build record](../logbook/specs/shape-family-build.md) | the clipped core, the margin, the clip's cost, the bed's corners, entry members, the calipers ruling |
| [shape build spec](../logbook/specs/shape-build/spec.md) | the strip law as built, the reach ball, the pace reads, the landing |
| [r63s pre-registration](../logbook/specs/r63s-prereg/predictions.md), [r63s read](../logbook/specs/r63s-read/README.md) | the gates, the results, the floaters, the theatre, the selection caveat |
| [logbook 0148](../logbook/0148-round-63-the-film-world-fills-and-cheap-mouths-eat-each-other.md) | how the screen is told in the logbook |
| the author's inference | the items listed above |

## Where it is

| file | what it holds |
|---|---|
| [`Superellipsoid.cs`](../src/Evosim.Core/Shapes/Superellipsoid.cs) | the solid: its formula, mass properties, surface, panels and contact core |
| [`PartForm.cs`](../src/Evosim.Core/Shapes/PartForm.cs) | the three genes and their ranges |
| [`SuperellipsoidShape.cs`](../src/Evosim.Core/Shapes/SuperellipsoidShape.cs) | the fourth part shape, which builds the solid from a node's form |
| [`Mutator.cs`](../src/Evosim.Core/Mutation/Mutator.cs) | the shape draw, the entry at the nearest member, the form walk |
| [`ConvexCore.cs`](../src/Evosim.Dynamics/ConvexCore.cs), [`ContactTrueShape.cs`](../src/Evosim.Dynamics/ContactTrueShape.cs) | cores, margins, GJK, and true-shape contact against bodies and the bed |
| [`Fluid.cs`](../src/Evosim.Dynamics/Fluid.cs) | the fin law |
| [`RunConfig.cs`](../src/Evosim.Core/RunConfig.cs) | the world's shape list and its contact form |
| [`env-r63s-on.ps1`](../rounds/env-r63s-on.ps1) | the screen's world |
