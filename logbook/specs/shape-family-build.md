# The tapered superellipsoid: build record (2026-10-25)

D233's build is on the branch `shape-family`, in the worktree under `scratch/`. The first piece puts the
shape's geometry in Core and changes no world. It departs from the proposal twice, and the coordinator accepted
both on 2026-10-25. The design is the proposal at commit 68c55ca5, and the ruling is D233.

## The first piece puts the shape in Core (b81ef4aa)

The new class, `Superellipsoid`, holds one set of genes. It computes once the scale-free factors that the volume,
the centre of mass and the inertia need. They come from beta functions, so Core gained a Lanczos gamma. Every
point the class returns is measured from the centre of mass. It also gives the attachment point, the inside test,
the drag panels, the exposure from the panels and the convexity check.

The piece adds no genome field, mutation, tunable or registry entry. The class is not yet one of
the part shapes, because their interface hands a shape its half-extents and nothing more.

All 50 of its tests pass. The volume, centre of mass and inertia agree with a direct integral to about one part in
ten million, and that integral uses no beta function. The ellipsoid and the ball are exact, and near zero
exponents the shape matches the box panel for panel.

Convexity comes down to one function. A slice across the long axis is a norm's unit ball, scaled by the tapered
profile. So the solid is convex when the section exponent is at most 2 and the profile is concave, and only then.
The check finds the profile concave across the whole range. Its largest second difference over a 46 by 41 grid is
9e-16. A second check needs no such argument: each panel's tangent plane must have every other panel behind it.
It passes 27 forms at the ranges' corners and middles and fails a taper of 12, as it must.

## Two departures from the proposal

The panels' area is extrapolated, where the proposal summed a 4 by 4 sub-grid. That sum fell 1 to 2.5% short
at the world's resolution of 2. The box's, the sphere's and the capsule's panels are exact there, and a shape
whose panels sum short is pushed less by the water for no reason in its form. The build sums at least 32
sub-cells along each face edge and again on every other line, and `(4·fine − coarse)/3` cancels the leading
error. The table shows the result on the three forms that were worst. Each total is the same at every
resolution from 1 to 8.

| Form (profile, section, taper) | Plain 4 by 4 at resolution 2 | Extrapolated |
|---|---|---|
| 0.1, 2, 0.25 | −2.5% | −0.033% |
| 0.1, 0.1, 1 | −1.4% | −0.001% |
| 0.3, 1.7, 3 | −1.3% | −0.013% |

The price is about 7,000 surface points per part at resolution 2, under a millisecond. That is fine once per
part. Growth is the problem: today a growing body's panels are rebuilt at every growth step
(`Creature.Growth.cs`, and `Phenotype.Scaled` re-measures the surface area). Growth scales a body uniformly, so a
part's panel centres scale by s and its areas by s². The genes piece keeps each family part's panels from its
development and scales them, and its tests say so.

An anchor inside the cube goes out along its ray to the surface, as a sphere's and a capsule's anchor does. Only
the box attaches such a child inside itself. An anchor on a face lands where the box puts it, so a box-like
member grows its children as a box would.

I made one wrong turn on the way. The first reference surface integral read 0.3% low on the box-like forms and
agreed with itself at two grid sizes. The cause was rounding. In double precision the cosine of π/2 is 6e-17,
and its 0.1th power is 0.023. That left the parametrisation's seam open by 2% of a half-extent. The test now
snaps such residues to zero.

## The second piece carries the form from the node to the solver

A Sonnet builder wrote this piece from a brief, and I reviewed it. The genes now travel as a `PartForm` value, and
box, sphere and capsule carry its empty value. Every shape method has a form-taking overload. A named shape's
overload refuses any form but the empty one, then calls its old method, so it gives the same bits. The new
`SuperellipsoidShape` refuses to be measured without its form. It is not in the standard list of shapes, so no
world can draw it.

A node carries its form in memory only. The genome file has no field for it, so the writer refuses a node that
holds one, and the format stays as it is. Validation takes the world's shape list as an option, defaulting to the
standard three. It refuses a family node without a form inside the ranges, and a form on any other node. A shape
mutation away from the family clears the form without a new random draw.

A developed family part measures its panels once, at resolution 2, and that one measurement gives its surface
area, its exposure and its drag. A grown copy shares the measurement and scales it, so growth measures no surface.
The solver's growth path reads the panels and the inertia through the part. The tests show the panel builds stay
at one across growth steps and a hundred exposure reads. 124 shape and growth tests pass on the review run.

The coordinator asked for proof that the named shapes' paths did not move, and it came from the crowd fixture. I
re-recorded `cefix-s4` from founding on commit 751877a8, the clean tree that also holds species distance's change.
The new run is the one stamped 2026-10-25-163423, and its rows were compared from second 0 with the recording the
fixture names, stamped 2026-10-25-014251. Every one of the 2,000 stats rows matched, 340,000 values with no
difference, and all 10,408 lineage rows were equal. The solver's suite, the slow classes included, passed 325 with
the one skip it had before. The shape and species tests passed 89. The card's code is untouched, which the
coordinator accepted on reading the diff. The coordinator accepted both proofs on 2026-10-25, so piece 2 and species
distance stand on 751877a8.

Some places still read a family part as if it were a box, or refuse it. None can meet one yet, and each belongs to a
later piece:

- The contact on true shapes refuses any shape but the three. A family body runs only with contact per body.
- The fin law's added mass, on the host and on the card, refuses an unknown shape.
- The mouth point, the silhouette hull, the thin axis and the buoyancy arm read the half-extents' box. For a taper
  above 1 the real body is wider than that box at its wide end.
- Species distance compares the shape and ignores the form.

## The solver's pieces, as drafted before they are built

I drafted the next two pieces on 2026-10-25, while builds waited on a cool machine. Neither needs a genome format or a
tunable. Each needed a choice of numerics that no ruling makes. The coordinator ruled on all four the same day, and
the paragraphs below give the rulings.

The fin law comes first. D229 prices a box's water from its calipers along the limb and across the swing. D231
integrates a chord along the limb in strips and finds its first moment for the lever. A family part fits D231's
strips directly. Its chord at a distance along the limb is the width of its slice across the swing, read once from
its surface points. A uniform scaling moves the water by s³ and its moment by s⁴. So both are constants per degree of
freedom at the reference size, scaled at a resize like the panels. D231's host build is still open on its own branch
and rewrites the same function. The coordinator ruled that this piece waits for it and builds on top of it.

The swing push was the open case: under D229 it changes direction every step, and D231 refuses it. I proposed the
calipers of the support function in the plate formula, as a box takes. The coordinator ruled against that. A lens's
or an egg's strip integral falls well under its bounding plate's, about two thirds of it for an elliptic outline.
So calipers would hand the family thrust it did not earn, and D233 asks that the new shape win its place fairly
against the box. The piece builds a table instead. For about 64 swing directions on the half-sphere, in the part's
frame, it holds the strip integral and its lever at the reference size. A step reads it by interpolation. A resize
scales it by s³ and s⁴, cached as the panels are. It is accepted if it stays within 2% of slicing over the bench
forms. If it does not, the push slices at every step, and its pace is measured on a crowded checkpoint. This file
will give the choice and the error.

Contact is the second of the two pieces. The pair test runs GJK on a core inside a margin, so that core and margin
together make the body that touches. A hull point's support costs 98 dot products.

The first ruling on the core does not stand. It took the hull of the 98 surface points, each moved inward along its
normal by the margin. The draft showed that this hull cannot keep its bulge small. Near a sharp corner the moved points
stand outside the true inner body, and core and margin together bulge past the surface there. That bulge stays a fixed
share of the margin, and at one corner of the range it reached 0.42 m. Shrinking the margin shrinks the bulge only in
proportion, so no margin brings it down.

The coordinator replaced it with the clipped inner body the same day. The core is the solid eroded by the margin: every
point of the solid at least the margin from its surface. For a convex solid that erosion is the exact core. Swept by a
ball of the margin, it rounds every corner by the margin and no more, as the box's shrunk core does. So a
family part touches as a box of the same outline would, which is the fair place D233 asks for. The build approximates
the erosion with half-spaces. Each moved point is clipped back inside the planes that lie the margin behind the solid's
support, along 256 fixed directions and the 26 lattice directions. The core is built once per form, at the reference
size, and scaled with the part.

The margin starts from the smaller of the smallest half-extent and the solid's inradius, the radius of the largest
ball inside it. So a tapered part's thin end still holds a core. The quarter-margin bulge check stays as a guard and
should never act.

The first build window, on 2026-10-25, showed that the margin cannot be all of that value. Everything compiled, and the
existing contact tests passed. But at the full value the eroded body has no thickness along the smallest axis. A
box-like form's core is then a flat rectangle, and an ellipsoid's is a point. The clip projects onto one plane after
another, and onto a set with no volume it crawls. After 200 passes the ellipsoid's core still stood 2.6 mm proud of the
solid, and the box limit's corners stood 16 mm from the shrunk box's. The flat core also put a plate's top and bottom
corners on the same points, so a plate touched the bed on eight springs where a box touches on four.

The coordinator ruled two changes the same day. The margin is 0.98 of that value. The eroded body then keeps a
thickness of about 2% of the margin on each side, and the clip ends in a few passes. Corners closer than a set share of the margin are
one corner and touch once, as a box's core skips a vertex that repeats another. A ball's core is then a small ball at
its centre, and a plate's is a thin slab. The box-limit test reads the box shrunk by 0.98 of its margin.

The first ruling on that share was 2%, and it was wrong for this margin. A flat core's thickness is 2(1 − β)/β of the
margin, about 4.08% at β = 0.98, so a 2% merge never joins a plate's corners. The coordinator corrected it to 5%. A
comment by the constant says that it must exceed that thickness, and a test fails if one is changed without the other.

The second window, the same evening, found the clip itself at fault. Plain cyclic projection ends at some point in the
intersection of the half-spaces, which need not be its nearest. A box's corner projected first onto a slanted plane came to
rest on the shrunk box's face, 18 mm from its corner. The clip is now Dykstra's projection, which carries each plane's
last correction into its next pass, and whose limit is the nearest point. Its iterates can stand outside a plane
before the limit, so plain cyclic passes follow to make the point lie inside. At first both ran a fixed 200 passes, which
the coordinator took as the way to keep a build deterministic. The box limit's corners then landed on the shrunk box's to
the last digit printed, and the worst bulge over the forms was 6.7e-6 m. The price was the build. A test that builds
seven cores took about three times as long as before. An estimate put a family world of cefix-s4's size over an hour
and a half slower.

Two remedies followed, ruled by the coordinator the same evening. Each point now leaves the clip
once a whole pass moves it by no more than a billionth of the form's size. It leaves the polish only once it lies
inside every plane to that tolerance. That exit is a pure function of the build's inputs, so a build takes the same
passes on every run and replay, and the 200 stay as a cap. A test holds the exit build within 1e-7 of the size of the
full one for every form. And built cores are kept in one cache for the process. Its key is the bit patterns of the
form's three genes and the three half-extents. A newborn whose part did not change since an ancestor's reuses that
core. Because a build is a pure function, a cached core is the cold build to the bit, and tests hold a hit and a
rebuild after eviction to that. The cache keeps the 4,096 cores used most recently, about 10 MB.

The exit was not enough. One cold build was timed alone, against the same build with every pass. The full build took
436 ms. With the exit the box-like form took 21 ms and the egg 211 ms. The form of profile 1, section 2 and
taper 4 took 348 ms. I had estimated 0.14 s for a full build and a fivefold saving from the exit, and both were
wrong. The coordinator set a target of 50 ms for the worst form and asked that the fix follow the cause.

The cause is the angle between the planes. Alternating projection closes on the line where two planes meet by about
the square of the cosine of the angle between their normals at each pass. The 282 clipping directions stand about
0.21 rad apart, and on a smooth stretch of surface a point's nearest place on the polytope lies where two neighbours
meet. Reaching a billionth of the size there takes some 470 passes. The box-like form's lattice planes meet at right
angles, and so it alone was quick. I first blamed the thin rim of the eroded lens, and that was wrong too.

So the clip now solves for the nearest point instead of approaching it. In three dimensions at most three planes meet
at it. Each step adds the plane the point stands farthest outside. It then finds the nearest point of the start within
the planes it holds, from the smallest set whose multipliers are none below zero and which meets every held plane. The
distance from the start grows at every step, so no set comes back. It stops when no plane is missed by more than a
billionth of the size. A point that ran out of steps would fall back to Dykstra's, but none does. It takes four steps
on average and eight at most.

Proving it took one more turn. Against Dykstra's run to 200 passes the exact core stood 17 mm away on the ellipsoid,
and the 200-pass build was itself outside some planes. The coordinator ruled that correctness be proved without a
reference. A test checks the conditions that make a point the nearest point of a convex set. The point lies inside
every plane, each held plane passes through it, and the start less the point is a sum of the held normals with no
negative weight. Every point of every form passes, with every misfit under 2e-14 m. Dykstra's build converges onto
those points. It stood 57 micrometres away at 2,000 passes and 4.3e-9 m at 5,000, inside the kept bound of 1e-7 of the
size. That comparison runs in the slow suite.

A cold build now takes about 12 ms for every form, timed alone on 2026-10-26, each the least of three tries.

| Form (profile, section, taper) | Exact clip | Dykstra's, 200 passes | Its smallest angle between held planes |
|---|---|---|---|
| 1, 2, 4 | 12.2 ms | 358.5 ms | 0.279 rad |
| 0.6, 1, 1.5 (the egg) | 11.5 ms | 357.1 ms | 0.212 rad |
| 0.1, 0.1, 1 (box-like) | 11.0 ms | 354.8 ms | 1.571 rad |

The clip no longer decides the cost; sampling the solid's support does. A newborn's solver body is built on the serial
path, in the farm's reconciliation loop, and that is where a family part's core is built or fetched. What follows is
an estimate and not a measurement. Take a family world of cefix-s4's size, about 10,400 births of four parts each,
with every part of the family. If 15% of cores miss the cache, it runs about a minute and a quarter longer. If none
hit, it runs about eight minutes longer. The genes piece will measure it.

The bed, the reef, the coral and the glass touch a box at every vertex of its core. A family core has 98, which is
far too many springs. It touches at its eight corner points, the cube's diagonals pushed onto the surface. Of a pair the
merge joins, the deeper one touches, and on a tie the lower pattern. A plate then rests on four corners, one at each
corner of its rim, as a box does. When no corner reaches the surface, the core touches at its support point. A support point
deeper than every corner that reaches by more than 0.5% of the margin takes the deepest one's place. So the springs
never grow in number. The coordinator ruled that switch on 2026-10-25 for an egg,
whose lowest corner stood 0.55 mm above its bottom on the first clip. On Dykstra's core that corner stands within
micrometres of the bottom, and an upright egg pushes 0.9986 times as its bottom would without the switch. Turned 45
degrees, or strongly tapered, a part has its lowest point between its corners, and there the switch acts. A test
lowers seven forms and turns onto the bed. At every height the touch reaches the lowest point to within the 0.5%, and
the descents include heights on corners alone and heights switched. A tilted box-like part never switches. The coordinator accepted that rule on two
conditions for the bench. A body rolled slowly through the switch between the two cases must show no step in energy,
and no rocking larger than the box's. If it fails either, the support point's spring is blended in by depth rather than
switched on. The coordinator ruled the same day that the bench's ellipsoid is held to the first condition only.
An ellipsoid rocks on a flat bed whatever touches it, so its swing says nothing of the switch. In the first window it swung 31 degrees.

The card refuses the family until its own piece. The refusal is on the host, in the farm's opening of the card engine,
and refuses any world whose shape list holds a shape that takes a form. A world develops only the shapes its list
holds, so such a list is the only way a family part can reach the card. The card's code is untouched, and the kernel
hash is a digest of that code alone, so the hash cannot move. The coordinator ruled on 2026-10-25 that a host-only
refusal lands with the contact piece. The card's own piece edits the card's code, rides an integration branch's
identity sitting, and the coordinator is told first.

The contact piece changed the pair test's shared path, so the crowd fixture was asked again whether the named shapes
moved. `cefix-s4` was re-recorded from founding on commit 1225e6c7, the clean tree that holds the contact piece, the
clip's exit and the cache. The new run is stamped 2026-10-25-210942. It was compared from second 0 with the recording stamped
2026-10-25-014251. All 2,000 stats rows matched, 340,000 values with no difference, and all 10,408 lineage rows were
equal. The suites passed on the same tree: the contact core's 15, the family contact's 16 and the true shape's 34. The
card's source is byte for byte main's.

## The genes piece's test list

The next piece passes the form through `PartShape` and carries it on the developed part. The coordinator ruled
that no genome format and no tunable change without the coordinator being told first. Each one forces the crowd
fixture to be re-recorded, and it rides with an integration branch's identity sitting. Its tests:

- Every named shape gives the same bits with the form passed as without it, so every recorded world replays.
- A family part's volume, surface area, panels and attachment points through the shape interface equal those of
  the class itself.
- A family part is never measured without its form, and a named part never carries one.
- Under growth, a family part's panels are its development panels scaled, centres by s and areas by s², and the
  shape is not asked for panels again. The scaled set equals a fresh build at the new size to rounding.
- The exposure a family part reads each metabolic step comes from panels cached on the part, which nothing rebuilds.
