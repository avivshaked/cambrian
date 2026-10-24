# Proposal: a tapered superellipsoid, one shape whose three genes vary smoothly

*A draft for the owner, written at the coordinator's request on 2026-10-25 under D229. Shapes change what bodies can be, so this is a world rule and nothing is built before the owner rules. Every claim about the code names its file. What is a guess is marked as inference. The formulas are my own derivation, checked at the sphere and box limits. The build must also test them against a numerical integral before anything trusts them.*

## The proposal in one paragraph

I propose a fourth shape beside the box, sphere and capsule: a superellipsoid with a taper. A superellipsoid is a solid whose surface runs smoothly from a box to a ball, depending on two numbers called exponents. A taper narrows it from one end to the other. Three new genes on a node set the two exponents and the taper ratio, and mutation moves each by small steps. A lineage can therefore walk from a slab to a lens to an egg to a tapering rod without any jump. The old three shapes stay unchanged, because no member of the family reproduces any of them to the bit and every recorded world needs them. Every property the code needs has a closed formula or a one-off sum at development, except contact. Contact is the expensive piece. D228's contact on true shapes is being built on one general test (GJK on convex cores with margins, `logbook/specs/d228-d229-build-plan.md` on the `fin-shape` branch), so the family adds only a small function to it. The whole build is about six agent days, after D228's contact and D229's fin law.

## The rules I propose

1. The family is one shape, `superellipsoid`, added to the three that exist. Its genes are the cross-section exponent, the profile exponent and the taper ratio. They are written only on a node of that shape.
2. The ranges keep every member convex: the profile exponent from 0.1 to 1, the cross-section exponent from 0.1 to 2, and the taper ratio from 0.25 to 4.
3. Each gene mutates by a Gaussian step on its logarithm, reflected at the ends of its range, under a new chance per node that is 0 by default.
4. The shapes mutation may pick become a per-world list, defaulting to today's three. A world that does not list the family draws exactly what it draws today.
5. The family's taper is measured about its own centre of mass, so the solver's rule holds that a link's centre of mass is its frame's origin.
6. Contact on a family part uses D228's general test, fed by the family's support function. That function returns the point of the shape farthest along a given direction. I recommend computing it from a fixed hull of surface points made at development.
7. Box, sphere and capsule are never converted into the family, in a genome or a checkpoint.

## The shape runs from a box to a ball, a lens or a tapered rod

A superellipsoid takes the three half-extents the genome already carries, a, b and c, and two exponents. The long axis is the part's local Y, as for the capsule (`src/Evosim.Core/Shapes/StandardShapes.cs`). The profile exponent ε1 sets how the outline rounds along Y. The cross-section exponent ε2 sets the shape of each slice across Y. A point is inside the solid where this function is at most 1:

```
F(x, y, z) = ( |x/a|^(2/ε2) + |z/c|^(2/ε2) )^(ε2/ε1) + |y/b|^(2/ε1)
```

At ε = 1 the slice is an ellipse. As ε falls toward 0 the slice fills out to a rectangle with sharp corners. At ε = 2 it becomes a diamond. So the cross-section exponent alone runs from a slab, through an oval rod, to a blade with an edge. The profile exponent does the same along the length: small for a flat-ended block, 1 for a rounded egg-like end.

The taper scales each slice by a factor that changes linearly along Y. I propose writing it as a ratio τ, the width at the +Y end over the width at the −Y end, and scaling each slice by

```
f(t) = 1 + k·t,    t = y/b from −1 to 1,    k = (τ − 1)/(τ + 1)
```

The width at the middle stays the half-extent the genome says, so a and c keep their meaning. With τ from 0.25 to 4, k runs from −0.6 to 0.6.

The ranges are chosen to keep the solid convex, meaning a straight line between any two points of it stays inside. Two parts of the code depend on that. First, the lit area is a quarter of the surface area. That is Cauchy's formula, and it holds only for convex bodies (`src/Evosim.Core/Development/PhenotypePart.cs`). Second, the general contact test works only on convex shapes. Above ε = 2 the slice pinches inward into a star. A taper on a pointed profile also dents the shape. For a profile exponent of 1 I worked out that the solid stays convex while |k| is under about 0.7, so 0.6 leaves a margin. Below 1 I believe it holds as well, but I have not proved it. The build should check convexity numerically across the range. Excluding profile exponents between 1 and 2 removes spindles with pointed ends. A taper on an egg still gives a teardrop, which is the swimmer's outline.

## Today's three shapes sit near the family's edges and stay as they are

The family contains none of today's shapes exactly, so the old three stay for replay. The table shows how near each one comes. Look at the last column: only the ellipsoid case is reached exactly, and the old sphere is not an ellipsoid.

| Today's shape | Nearest family member | How near |
|---|---|---|
| Box | both exponents at 0.1, τ = 1 | a box with slightly rounded edges and corners; the box itself is reached only as the exponents go to 0 |
| Sphere | both exponents at 1, τ = 1 | an ellipsoid on the three half-extents; today's sphere takes the mean of the three as one radius (`StandardShapes.cs`), so it matches only when the three are equal |
| Capsule | profile about 0.5, cross-section 1, τ = 1 | a rounded rod; a capsule is a cylinder with hemisphere caps, which no superellipsoid is, and it takes one radius from the mean of X and Z |

The capsule's best profile exponent is a guess. The build should fit it once, by volume, and check it by eye.

The earlier proposal's two candidates are both inside the family (`proposals/propose-new-part-shapes.md`). Its ellipsoid is both exponents at 1. Its tapered segment is a rounded rod with a taper. One shape with genes replaces two named ones, and D228's ruling of no new named shape still stands.

## The three genes mutate by small steps on a log scale

Today a node's shape changes with a chance of 0.02 per generation, picked uniformly among the other shapes in a list built into the code (`src/Evosim.Core/Mutation/Mutator.cs`). The family keeps that draw and adds its own, for nodes of the family only.

- Each family node draws, under its own new chance, one Gaussian step on the logarithm of each gene. The step's size is the world's existing scalar step, 0.15. A log scale makes a step a fraction of the value, which suits exponents that span a factor of twenty and a ratio that is symmetric about 1.
- A step that crosses an end of the range is reflected back. Clamping, which the buoyancy offset uses, piles every overshoot at the bound and makes the bound an attractor that selection never chose (inference from the arithmetic).
- A node that mutates into the family starts at the member nearest its old shape, from the table above. A node that leaves the family drops its genes.
- Nothing is drawn while the new chance is 0, and nothing is drawn on a node of a named shape. A recorded world then takes the same random numbers it always took. The module gene follows the same pattern for the same reason (`Mutator.cs`).

The shape list must become a per-world setting first. The registry's order decides which shape a draw yields, and adding a fourth shape to the standard list would change every recorded world's draws (`src/Evosim.Core/Shapes/PartShapeRegistry.cs`). Three places read the standard list directly today: the mutation draw, the genome's own check in `src/Evosim.Core/Genome/Genome.cs`, and the default when a body is rescaled for growth. All three must take the world's list instead.

Species distance counts a shape mismatch as one unit (`src/Evosim.Core/Genome/SpeciesDistance.cs`). Two family nodes should add the difference in their log genes, scaled so that the whole range is one unit. A family node and a named node still count one.

Founders today are drawn from box, box, capsule and sphere (`src/Evosim.Core/Genome/GenomeFactory.cs`). A world that lists the family can keep those founders, so the family has to win its place by mutation. Or it can found from the family alone, with genes drawn across their ranges. That choice is the owner's, in decision 4.

## Every property except contact has a closed formula or a one-off sum

The shape interface gives each method the half-extents and nothing else (`src/Evosim.Core/Shapes/PartShape.cs`). The family needs its three genes as well. I propose a small value, the part's form, carried on the node and the developed part beside the half-extents and passed to every shape method. The named three ignore it. The other route, writing the genes into the shape's name, would turn every gene value into a registry entry, and I would not take it.

Growth rescales a whole body uniformly at every growth step and re-measures every part from its shape (`src/Evosim.Core/Development/Phenotype.cs`). Each family quantity below therefore splits into a scale-free factor that depends only on the three genes, times powers of the half-extents. The factors are computed once per node and kept. A growth step then multiplies and calls no power or gamma function. This is my design, not code that exists yet.

### Volume, centre of mass and inertia come from beta functions

The beta function is a standard integral, B(x, y), the integral of u^(x−1)·(1−u)^(y−1) from 0 to 1. It is computed from the gamma function. .NET Standard 2.1 has no gamma function, so Core needs a small one: Lanczos's approximation, about thirty lines of plain arithmetic. Each slice of the shape is a superellipse scaled by the profile w(t) and the taper f(t). Integrating slice by slice gives everything:

```
w(t)  = (1 − |t|^(2/ε1))^(ε1/2)
S(ε)  = ε · B(ε/2, ε/2)                     area of a unit superellipse: π at ε = 1, 4 as ε → 0
J(ε)  = 2ε · B(3ε/2, ε/2 + 1)               its ∫x² dA: π/4 at ε = 1, 4/3 as ε → 0
Wm    = ε1 · B((m+1)·ε1/2, ε1 + 1)          ∫ t^m w² dt over −1..1
Um    = ε1 · B((m+1)·ε1/2, 2·ε1 + 1)        ∫ t^m w⁴ dt over −1..1

V      = S(ε2) · a·b·c · (W0 + k²·W2)
y_c    = 2k · b · W2 / (W0 + k²·W2)         centre of mass along Y, before the shift
∫x² dV = J(ε2) · a³·b·c · (U0 + 6k²·U2 + k⁴·U4)
∫z² dV = J(ε2) · a·b·c³ · (U0 + 6k²·U2 + k⁴·U4)
∫y² dV = S(ε2) · a·b³·c · (W2 + k²·W4) − V·y_c²
Ix = ρ(∫y² + ∫z²)    Iy = ρ(∫x² + ∫z²)    Iz = ρ(∫x² + ∫y²)
```

At both exponents 1 and no taper these give (4/3)πabc and the ellipsoid's inertia. As both exponents go to 0 they give 8abc and the box's inertia. The shape is symmetric across its X and Z planes, so the inertia tensor stays diagonal in the part's axes, as the solver requires (`src/Evosim.Dynamics/Creature.cs`). The solver's new code is one more branch in its inertia function, beside the sphere and capsule. That function today gives any unknown shape the box's tensor and counts it nowhere. The family must have its own branch before any world runs it.

The taper moves the centre of mass toward the wide end, by y_c. The solver takes every link's centre of mass to be its frame's origin. That is what lets it carry three numbers of inertia a link instead of six (`Creature.cs`). So the family shifts its geometry by −y_c, and the surface point, inside test, panels and contact hull below are all written in the shifted frame. The cost is a few beta functions per node at birth, which is microseconds.

### The attachment point, the inside test, the panels and the light

- The point where a child attaches starts from the box's own rule. The box clamps the anchor onto its faces (`StandardShapes.cs`). The family takes that point and pulls it in along its ray onto the surface. The function above scales simply along a ray, so the pull is a closed formula, F(q)^(−ε1/2). Then the taper and the shift apply. As the exponents go to 0 this returns the box's point, so a box-like family part attaches children where a box would.
- The inside test reverses the taper (divide x and z by f) and evaluates F. That is three power calls. It is used to measure how much of a body is inside itself (`src/Evosim.Core/Development/PhenotypeGeometry.cs`).
- The drag panels take the box's grid of six faces, n by n, push each grid point out onto the surface along its ray, and taper it. Each panel's normal comes from the gradient of F, carried through the taper. Its area comes from the projected corners of a 4 by 4 sub-grid inside it. The count is 6n², the box's and sphere's count, so the drag sum per step costs what a box's does (`src/Evosim.Core/Environment/FluidModel.cs`). The panels must sum to the true surface area (`PartShape.cs`). There is no closed formula for a superellipsoid's area, so the shape tests compare the sum with a fine numerical integral, and with 4πr² for a ball.
- The lit area stays a quarter of the panel area, which Cauchy's formula makes exact for a convex body. The exposure factor today uses the box's formula on the half-extents, and 1 for a sphere (`PhenotypePart.cs`). For the family I propose computing the shadow from the panels: half the sum of each panel's area times the size of its normal's vertical part. That is exact for the panel body. Its average over all poses is a quarter of the area, so it agrees with the lit area. It costs about 24 multiply-adds a part each time light is read. That happens on the host, not on the card (`src/Evosim.Farm/Metabolise.cs`).
- A part's thinnest axis, which D111's buoyancy offset uses, is taken from the half-extents as for the box and capsule (`PhenotypePart.cs`).

### The fin law's width across the swing is a constant per joint axis

D229 rules that a stroking limb's added mass comes from its width across the swing. Added mass is the water a moving body has to push along with it. For a flat blade swung broadside that is about ρ·π/4·width²·length, and for a round limb ρ·π·radius²·length. Today every limb moves a fixed share of its volume instead (`src/Evosim.Dynamics/Fluid.cs`). Both of D229's cases are one formula for a slice: ρ·π·h², with h the slice's half-width across the motion. That is the textbook added mass of an ellipse per unit length. The family's half-width in a direction p across the slice is the superellipse's support function, a closed formula:

```
h(p) = ( |a·px|^q + |c·pz|^q )^(1/q),    q = 2/(2 − ε2)
m_a  = ρ · π · h(p)² · b · (W0 + k²·W2)          for a limb along the shape's Y axis
```

For a thin blade this gives D229's π/4·width², and for a round rod π·radius².

In the drive's push, each joint axis is fixed in the limb's own frame, and so is the line from the joint to the limb's centre (`Fluid.cs`). The width across each axis's swing is then a constant for each link and each axis. It is computed at development, and the solver and the card read a number, as they read the inertia now. When a child attaches on the side, so the limb's line is not the shape's Y axis, there is no closed form. A short numerical integral over sixteen slices does it once at birth. The fin law's build defines this general case for the box first, and the family follows whatever it defines.

## Contact is the one expensive piece

Contact today is a soft spring between overlapping spheres, one per link, against other links, the bed, the reefs and the glass. The push lands at each link's origin with no turning effect (`src/Evosim.Dynamics/Contacts.cs`). The bed is read at the sphere's centre, as a plane tangent to the height map there (`src/Evosim.Dynamics/ContactBed.cs`). D228 rules that contact follows each part's true shape, and its build uses GJK on convex cores with margins, with the broadphase kept on spheres. The family needs three things from it: a penetration depth, a direction and a contact point for any pair of convex shapes, which a general test supplies from each shape's support function.

There are three ways to give the family a support function. The table sets them side by side. Look at the card column, since the card runs in single precision and its maths functions behave differently from the host's (`src/Evosim.Farm.Gpu/CLAUDE.md`).

| Way | Fidelity | Host cost per touching pair (inference) | The card |
|---|---|---|---|
| Exact support function inside the general test | exact | a general test calls the support function ten to twenty times; each untapered call is about six powers, and the taper adds a short one-dimensional search, so about 25 powers a call | powers in single use the card's own instructions; a fixed iteration count keeps the warps together |
| A hull of fixed surface points inside the same test | within a few percent of the surface at 98 points (inference) | each support call is a maximum over the points, about 500 multiply-adds at 98 points; no powers, no inner search | plain multiplies and comparisons; points stored per node at unit size, about 1.2 kB a node at 98 points |
| A clump of spheres fitted at birth | good for round and tapered parts, poor for flat ones | a few to a dozen sphere tests | the existing sphere arithmetic |

I recommend the hull. A sphere clump cannot make a fin touch as a fin, which is the reason D228 exists. The exact support function gains little over a hull of 98 points and costs more powers and an inner search on the card. The hull's points are the panel grid's corners pushed onto the surface at n = 4, which the panels compute anyway. A thin margin can round them where the contact normal needs to be smooth.

I do not know the cost in pace. HANDOFF records the contact search as about a quarter of the farm's time, mostly finding neighbours rather than testing them. The general test adds cost in proportion to the pairs actually touching. In a crowded oasis that could be large, and only a measurement will say. The bench in the time table replays a checkpoint of a crowded world with the family forced on, and reads pace with nothing else on the machine.

Everything else stays as D228 builds it: penetrations, pushes, touches and bites, because since D219 a touch decides who eats whom.

The determinism rules hold for every choice. The arithmetic for each pair is fixed, the order of pairs is fixed, and each body writes only its own push. The thread count therefore stays invisible on the host, and the card stays identical across group sizes, which is how a card identity claim is made (`src/Evosim.Farm.Gpu/CLAUDE.md`).

## The format change touches only genomes that carry the family

Genomes are format 11, or 12 for D146's one brain, and loading refuses any other number (`src/Evosim.Core/Serialization/GenomeJson.cs`). Format 12 set a precedent: a genome of the old model is still written as format 11, byte for byte. I propose the same here. A node of a named shape writes what it writes now. A family node writes its three genes, and a genome with any family node is written as format 13, or 14 with the one brain. Every recorded run, inoculum and fixture stays readable and writes back the same text. A build without the family refuses a format-13 genome with a message that names the reason. Loading still refuses rather than defaults: a family node without its genes throws, and so does a named node that carries them.

The per-world shape list and the family's mutation chance are new tunables. As with every tunable, the new build refuses every older config, and the recorded test fixtures need re-recording (`src/Evosim.Core/CLAUDE.md`). The two reflection tests that guard the config will fail until the new fields reach the hash and survive a save and reload. The family's ranges go into the config hash through the shape's own hash contribution, so a later change of range changes the hash (`PartShape.cs`).

I think a checkpoint needs no new version. It stores genomes, and the family's derived numbers are rebuilt from them on restore. The farm's checkpoint rules have the last word, and I have not checked them (`src/Evosim.Farm/CLAUDE.md`). Recorded worlds replay because nothing new runs without a family node, which is D228's and D229's off-by-default pattern.

Two older tools switch on shape names. The nursery room's genome reader must learn the family or refuse it (`src/Evosim.NurseryRoom/Genomes.cs`). The Unity farm's builder gives any unknown shape a box collider (`unity/Assets/Evosim/Sim/PhenotypeBuilder.cs`). That farm has been idle since round 42. Editing any file under that folder changes the Unity build's identity hash and stops older Unity recordings from replaying, so I would leave it alone. A family world simply never runs there.

## The theatre draws it from a few shared meshes

A unit superellipsoid stretched by its half-extents is the superellipsoid with those half-extents, taper and shift included. So the theatre needs one mesh per set of genes, and the half-extents go on the transform's scale, as the rounded cube's do now. The theatre already keeps one rounded cube per rounding bucket, built once and shared (`unity/Assets/Theatre/TheatreMeshes.cs`). The family's meshes would work the same way. The genes are rounded into buckets, perhaps twelve steps per gene on the log scale, and a mesh is built the first time a bucket is drawn. Each mesh uses the panels' construction: the cube's grid at the rounded cube's 24 segments a face, pushed onto the surface, tapered, with normals from the gradient. That is about 7,000 triangles, shared by every part in the bucket. Rounding only changes the picture. The physics uses the exact genes.

The work is in the places that switch on shape names. The snapshot camera, the palette and the film plans each test for box, sphere or capsule (`unity/Assets/Theatre/TheatrePalette.cs` is the largest). Each needs a decision about the family. For example, a flat family part with sharp edges should probably be drawn as a leaf, as a flat box is. The live view steps the farm's own solver as a labelled cousin, so it gets the family's physics with no theatre work. Every test render is checked against a reference sheet before the owner sees it, under the owner's rule.

## About six agent days, in an order set by D228 and the fin law

The table gives each piece in agent time, with machine time separate. Look at the dependency column: two pieces can start now, and the rest wait on D228's contact or D229's fin law.

| Piece | Agent time | Machine time | Waits on |
|---|---|---|---|
| The shape in Core: closed formulas, gamma function, attachment point, inside test, panels, exposure, convexity check, tests against numerical integrals | 1 day | minutes of tests | nothing; it changes no world |
| The genes through the code: node and part form, shape methods, development, growth, mirroring, genome formats 13 and 14, species distance, mutation, per-world shape list, founder option, fixtures re-recorded, nursery reader | 1.5 days | the slow suite, about half an hour | the shape in Core |
| The solver on the host: inertia branch, centre-of-mass shift, the fin law's width per joint axis | half a day | minutes | D229's fin law |
| Contact on the host: the hull and its support function inside D228's general test | half a day | minutes | D228's contact |
| Contact and the fin-law number on the card, the CPU-accelerator check, identity at two group sizes | 1 day | a card sitting of a few hours, with nothing else on the machine | the host's contact |
| The theatre: meshes, buckets, shape switches, reference sheet | 1 to 1.5 days | short Unity checks | the shape in Core |
| The bench: family fins against box fins in still water under the fin law, and the pace read on a crowded checkpoint | half a day to set up and read | a few hours | everything above |

The total is about six agent days. Under the owner's rule on models, a Sonnet subagent builds each piece and runs its first tests, and the coordinator reviews. The Core shape and the theatre can run in parallel from the start. The genes follow the Core shape. The solver and contact pieces follow D228 and the fin law. The card piece needs its own identity sitting, unless it shares one with another solver change (decision 7).

## What I expect it to buy

Each expectation here is an inference from the formulas. None has been measured.

- Under the fin law a blade's push grows with its width squared, while its upkeep grows with its volume. At a fixed width and thickness, a diamond cross-section has half the volume of a rectangle, because S(2) is 2 and S(0) is 4. So I expect fins to drift toward thin sections with sharp edges, like a real fin's foil. Neither box nor capsule can show that.
- A taper toward the tip moves the centre of mass toward the joint. The drive's push grows with the distance from the joint to the limb's centre (`Fluid.cs`). Under today's push, a fin that narrows toward its tip is pushed less per volume. A real fin does the opposite, since its tip moves fastest. If the fin law took the lever from where the added mass sits along the limb, a wide tip would pay, as in real swimmers. This changes the push law, so it is a world rule and is decision 8.
- Fat bodies should drift toward round, egg-like members, since a ball holds the most volume for the least surface (the earlier proposal's inference). The family lets them get there by small steps.
- The family does not answer whether swimming pays. The swimmer search over round 62 found no body that swims (`proposals/propose-doing-must-pay.md`). A better shape helps only if the drive law and the economy let a stroke pay.

## The decisions for the owner

1. Whether the family is built as a fourth shape, beside box, sphere and capsule, with the three named shapes kept for replay and never converted. The alternative, replacing them, would make every recorded world unreadable.
2. Whether the ranges are the profile exponent from 0.1 to 1, the cross-section exponent from 0.1 to 2 and the taper ratio from 0.25 to 4. These keep every member convex. They leave out pinched star shapes and spindles with two pointed ends. A taper on an egg still gives a teardrop.
3. Whether mutation's shape list becomes a per-world setting, defaulting to today's three. Replaying recorded worlds requires it, and like every tunable it refuses older configs.
4. What the first world that uses the family lists. Either all four shapes with today's founders, so the family has to win its place by mutation and the share of each shape can be read over the run, or the family alone, founded across its ranges. I recommend all four, since that reads as an experiment.
5. Whether a family part touches as a hull of 98 surface points inside D228's general test, as I recommend, or by its exact support function. The hull is within a few percent and needs no power function on the card.
6. Whether the genome format changes only for genomes that carry a family node, as formats 13 and 14, leaving every other genome written byte for byte as today.
7. Whether the family's card change takes its own identity sitting or shares one with another solver change.
8. Whether the fin law's push takes its lever from where the added mass sits along the limb, which would reward a wide tip, or from the limb's centre as the push does today. This is part of D229's build and a world rule, so it is asked here because the family's tapers depend on it.
9. Whether the bench runs before any screen: family fins against box fins in still water under the fin law, and a pace read on a crowded checkpoint with the family forced on.
