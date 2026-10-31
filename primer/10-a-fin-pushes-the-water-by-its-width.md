# 10 — A fin pushes the water by its width

[Piece 03](03-what-it-means-to-push-against-water.md) ended on a limitation it promised not to hide. Added mass, the
water a moving body drags along with it, was folded into each part's mass by volume alone. The real thing depends on
which way a part moves, and piece 03 noted that this is part of what makes flapping work.

For a long time that cost only accuracy. Then the swimmers' stroke came to be built from the same number, and it began
to cost them their fins. This piece is about the rule that put shape back into the stroke. Since
[D229](../DECISIONS.md#d229), a stroking limb's push follows its width across the swing. For the bench's two limbs, a
flat paddle moves about twenty times the water of a stick of equal volume, and it travelled five to seven times as
far.

## A paddle throws more water than a stick

Sweep a flat plate broadside through water and the water in front of it cannot get out of the way at once. It is
shoved aside and set moving round the edges, and a slug of it travels with the plate. That slug is the added mass.
Throwing it takes effort, and the water's reaction to being thrown is what a fin pushes against. One of the review's
papers names this the reactive force on a body accelerating in a fluid [C18 §2.2, p.5].

How much water moves depends on the width the plate presents across its motion. A plate of chord w carries roughly a
cylinder of water w across, so the slug grows with the square of the width. A round rod of radius r carries a cylinder
about its own size. These are the two formulas D229 adopts:

```
flat blade swung broadside:   m_a ≈ ρ · π/4 · w² · L
round limb:                   m_a ≈ ρ · π · r² · L
```

Here ρ is the water's density and L the limb's length from the joint outward. The blade's thickness along its motion
does not appear.

Volume is width times thickness times length. The water thrown is width squared times length. A thin, wide paddle
gives up thickness, which moves no water, for width, which counts twice. So two parts of equal volume can throw very
different amounts.

The code calls these the textbook values, and [`DESIGN.md`](../DESIGN.md) cites no paper for them. I treat them as
standard results the project relies on, outside the literature it reviewed. That literature does say the term matters.
[C18 §4, p.28] blames its absence for a world with no squid. DESIGN.md §5.4 calls added mass the cheapest stand-in for
the "collaborating motion of fluid" that [U07 §5, p.9] finds missing.

## The world's stroke is a rule, and the rule counted volume

Piece 03 described drag summed over small panels of each surface, where a face moving broadside meets far more force
than one moving edge-on. In principle that lets a limb row. [D135](../DECISIONS.md#d135) records that rounds 52 and 53
ran on drag alone, that a symmetric stroke cancelled itself, and that nothing could swim. A random brain almost never
found a stroke that worked.

So the project replaced the swing with a rule. Under D135 each joint has a bend and a power. The power is a stroke the
physics does not simulate. A powered link is pushed along its own length toward its joint, as if it swung at a rate
set by the power. The push is a coefficient times the link's added mass, its reach from the joint and the square of
that rate.

The added mass in that product was a fixed coefficient times density times volume. It knew nothing of shape. A stick
and a paddle of equal volume got the same push, and on the bench they travelled the same distance to the bit, so width
bought nothing.

The owner saw that the evolved limbs read as sticks, and on 2026-10-24 asked whether a fin's push should follow its
shape ([D228](../DECISIONS.md#d228)). D229 gave the reason the next day: under D135 nothing selected a fin for its
width.

That is the lesson I take from the story. Piece 03 recorded the volume-only added mass as a limit on inertia, where it
did little harm. Built into the push, the same simplification reached the one force that drives every swimmer.

## The fin law reads each part's width across its stroke

The fin law is a switch, `ShapedAddedMass`, off by default so that every recorded world replays. With it on, the
powered stroke takes each part's added mass for the direction its stroke moves it. A box takes the plate formula. Its
L is its extent along the limb, and its w is its extent across both the limb and the motion. A capsule takes the round
formula on its cylinder and counts its two ends as a sphere. A sphere keeps the old volume value, so piece 03's
finding that a sphere cannot paddle survives.

One slip was caught before it shipped. The brief named the swing's turning axis as the direction of motion. Read that
way, a paddle swung broadside would have counted its thickness as its width. The builder noticed, and the build takes
the motion as the turning axis crossed with the limb ([build plan](../logbook/specs/d228-d229-build-plan.md), section
1).

Two things did not change. A stroke's billed work is its push times its lever and the swing's speed, so a wider fin
already pays more in proportion, with no second charge. And the body's inertia still uses the volume formula, so piece
03's limitation stands for everything except the push.

## On the bench the paddle went five to seven times as far

The bench built two bodies, each a root with one powered box limb ([the build's
checks](../logbook/specs/d228-d229-build/README.md)). Both limbs are 40 cm long and of equal volume. The stick is
about 9 cm square in section. The paddle is 2 cm thick and 40 cm wide, flat across its stroke. Each was stroked by
hand for 120 s in still water, at four powers, with the law off and on.

The table gives distance travelled and work billed. Look first at the stick with the law off, then at how far the
paddle pulls away with it on.

| Power | Stick, law off | Paddle, law off | Stick, law on | Paddle, law on |
|---|---|---|---|---|
| 0.8 | 3.19 m, 4.01 J | the stick's, to the bit | 4.14 m, 6.29 J | 20.45 m, 125.83 J |
| 0.4 | 1.33 m, 0.50 J | the stick's | 1.80 m, 0.79 J | 9.95 m, 15.73 J |
| 0.295 | 0.85 m, 0.20 J | the stick's | 1.19 m, 0.32 J | 7.19 m, 6.31 J |
| 0.2 | 0.46 m, 0.06 J | the stick's | 0.66 m, 0.10 J | 4.70 m, 1.97 J |

With the law on, the paddle went five to seven times as far as the stick and was billed twenty times the work. For
these shapes the plate formula gives the paddle about twenty times the stick's water, so the bill follows the push as
intended.

Twenty times the push bought only five to seven times the distance. My reading is that drag grows with the square of
speed, so speed grows about as the square root of push. The square root of twenty is about four and a half.

The comparison that matters for evolution is per joule. For about 6.3 J the paddle at power 0.295 went 7.19 m, and the
stick at 0.8 went 4.14 m. The paddle at 0.2 went 4.70 m on 1.97 J, a third of the stick's spend. The law rewards a
broad fin per joule as well as per stroke. The bench file adds that a body growing a paddle could trade its power
down. It marks that as inference, and no world has shown it yet.

## A blade beyond the joint now counts too

The fin law fixed one link. A second fault came to light the same day. The agent working on swimming speed read the
solver and found that only the jointed link's own part was pushed. A paddle welded beyond it, attached with no joint
of its own, got no push and added only drag and weight.

Real fins are built that way, with muscle at the base driving a broad blade. Under the old rule the design was pure
cost, and evolution could not find it. On 2026-10-25 the owner ruled that a stroke pushes the whole assembly
([D231](../DECISIONS.md#d231)). That is the jointed link and every part welded beyond it, up to the next joint.

The assembly is measured in strips along the limb. At each distance s from the joint, the chord c(s) is the width that
the parts covering that strip span together. The added mass is the plate formula summed over the strips. Its first
moment about the joint says where the moved water sits, and the push acts there. A wide blade at the tip gets a long
lever and does the work, as in a real fin.

```
M     = ∫ ρ · π/4 · c(s)² ds
I1    = ∫ ρ · π/4 · c(s)² · s ds
lever = I1 / M
```

A welded assembly is rigid, and each degree of freedom strokes in a direction fixed in the link's own frame. So M and
I1 are constants, written when the body is built and again when it grows. A link with nothing welded beyond it takes
D229's own lines, bit for bit.

The price needed a choice. The build bills the push times the new lever and the swing's speed. Billing each strip at
its own speed was set aside, because it would have repriced every lone link. A box jointed at its end would have paid
1.33 times D229's work for the same push ([build plan](../logbook/specs/d228-d229-build-plan.md), section 4b).

D231 names its own risk. A large blade on a small link may push far beyond anything else, held back only by the work
it is billed. D231 called for a still-water bench first, with a small link carrying a blade, a flat link and a stick.
The bench is written, and I found no recorded result of it. The switch, `StrokePushesAssembly`, reached main after its
identity check on the graphics card ([D248](../DECISIONS.md#d248)). It has been on in no round or screen.

## The shape family takes the law through its true slices

In D228 the owner asked for more variety of form than box, sphere and capsule give. [D233](../DECISIONS.md#d233)
answered with a fourth shape, a tapered rounded block whose proportions are inherited genes ([piece
11](11-a-fourth-shape-lets-evolution-walk-a-limbs-proportions.md)). A family limb can flatten and widen toward its
tip, so a paddle exists only where a lineage grows one ([D252](../DECISIONS.md#d252)).

An early proposal priced a family part by its bounding box in the plate formula, as a box is priced. The coordinating
agent ruled against it. For an elliptical outline the true strip integral is about two thirds of the bounding plate's.
Calipers would have handed the family thrust it had not earned. D233 asks the new shape to win its place fairly
against the box ([shape family build](../logbook/specs/shape-family-build.md)).

D252 then asked for a table of about 64 directions, read by interpolation each step. The build found it unnecessary,
and [D255](../DECISIONS.md#d255) accepted the change. Every world drives the powered stroke, where each degree's
direction and the limb's axis are fixed for life. So one exact value per degree will do. At build the code projects
the part's surface onto the plane of the limb's axis and the direction across the stroke. It takes the convex outline
of those points and integrates the chord in strips. A resize scales the added mass by s³ and its first moment by s⁴,
and the step reads two numbers per degree.

The family's law is D231's strip integral applied to one part's true slices. For a box aligned with its limb, that
integral is the plate formula and its lever is the reach. So the family member nearest a box lands on the box's
numbers, and a test holds it there. The build had to come within 2% of a fine reference over the bench forms ([shape
build spec](../logbook/specs/shape-build/spec.md)). A family limb inside a D231 assembly is refused at build for now.

## In a world the law has not yet paid for a fin

The fin law has run in one screen, a short trial of a change on a few seeds before it enters a round. The shape screen
ran the film's world for 24,000 s on two seeds, with the family and the fin law both on ([r63s
read](../logbook/specs/r63s-read/README.md)). Grazers, the animals that eat living leaves, survived on both. About a
fifth of jointed bodies carried a family limb at the end. Moving grazers swam at 0.98 of their speed with both
switches off, about a quarter of a metre a second in each arm.

Few grazers carried a family limb, 22 of about 3,400 on one seed, and the shape genes had barely moved. Nothing yet
says selection favours the new shapes. With both switches on together, the screen cannot say what the fin law did
alone.

The speed grazers needed came from another dial. In round 62 a grazer spent about 100 s finding each meal, against
about 41 s its budget allowed ([logbook
0147](../logbook/0147-grazers-paid-their-way-once-they-swam-faster-and-grew-up-slowly.md)). A thrust arm pushed every
limb sixteen times harder at a sixteenth of the price, with the fin law off. Its grazers swam 0.18 to 0.26 m a world
second against the base's 0.07, and passed ([r62p2 read](../logbook/specs/r62p2-read/24000-a/README.md)). A design
read had warned that a push multiplier makes a stick swim like a paddle and so erases the selection for fins
([pace-of-life read](../logbook/specs/pace-of-life-read.md)). The screens took the dial because speed carried the
grazers.

I expected the law to speed grazers up, since even the stick gained 1.3 to 1.45 times on the bench. It did not, and no
read has asked why. One guess is cost. On the bench the law made the stick dearer per metre as well as faster, 1.52
J/m against 1.25 at full power. A world selects on joules. Another guess is that 24,000 s of mutation is too short to
grow the paddles the law pays for.

[D262](../DECISIONS.md#d262) asks a new mechanism to enter overpowered and be dialled back. Whether this one
overshoots will show once a world grows a fin.

## Sources

| Source | Used here for |
|---|---|
| `[C18]` | Added mass as the reactive force on a body accelerating in a fluid [§2.2, p.5]; its absence precluding squid-like creatures [§4, p.28] |
| `[U07]` | The "collaborating motion of fluid" missing from per-part models [§5, p.9], as DESIGN.md §5.4 reads it |
| D135, D228, D229, D231, D248, D252, D255, D262 | The stroke as a rule, the owner's question, the three laws, the landing, the family's route, a feature entering overpowered |
| `logbook/specs/d228-d229-build-plan.md` §1 and §4b | The formulas, the direction slip, the price readings, the strips and the 1.33 |
| `logbook/specs/d228-d229-build/README.md` | The bench table and its per-joule reading |
| `logbook/specs/shape-family-build.md`, `logbook/specs/shape-build/spec.md` | The two-thirds ruling, the projection, the 2% gate |
| `logbook/specs/r63s-read/README.md` | The one screen with the law on |
| logbook 0147, `logbook/specs/r62p2-read/24000-a/README.md`, `logbook/specs/pace-of-life-read.md` | Round 62's speed, and the warning against a push multiplier |

## What here is inference

- **The plate and round formulas** are standard results that the project's code calls textbook values. No key in
  DESIGN.md §13 backs them, so they rest on the project's word here.
- **The lesson** that a simplification harmless for inertia became decisive once the push reused it is the author's
  reading.
- **"About twenty times the water"** is the author's arithmetic from the bench's half-extents and the plate formula. It
  agrees with the twentyfold work the bench billed.
- **The square-root reading** of why twenty times the push gave five to seven times the distance is the author's.
- **A body trading its power down** once it grows a paddle is the bench file's own inference.
- **Both guesses** for why the screen's grazers swam no faster are the author's. No read has tested either.
- **That the shape screen cannot separate the two switches** follows from its design. The pre-registration says a
  further screen would be needed.
- **That D231's bench never ran** is an absence: no tracked file holds its result, and the grazer budget said it had
  not run when that was written.

## Where it is

| file | what it holds |
|---|---|
| [`Fluid.cs`](../src/Evosim.Dynamics/Fluid.cs) | the powered stroke's push, the fin law's added mass, the two levers and the billed work |
| [`Creature.Assembly.cs`](../src/Evosim.Dynamics/Creature.Assembly.cs) | D231's assembly and its strip integrals, written at build and resize |
| [`Creature.Family.cs`](../src/Evosim.Dynamics/Creature.Family.cs) | D255's per-degree added mass and first moment for a family limb |
| [`Creature.Growth.cs`](../src/Evosim.Dynamics/Creature.Growth.cs) | the inertia, still by volume |
| [`FluidConfig.cs`](../src/Evosim.Core/Environment/FluidConfig.cs) | the two switches and their documentation |
| [`FinBench.cs`](../src/Evosim.SwimProbe/FinBench.cs), [`AssemblyBench.cs`](../src/Evosim.SwimProbe/AssemblyBench.cs) | the two still-water benches |
| [`FinLawTests.cs`](../src/Evosim.Dynamics.Tests/FinLawTests.cs), [`StrokeAssemblyTests.cs`](../src/Evosim.Dynamics.Tests/StrokeAssemblyTests.cs), [`FamilyFinLawTests.cs`](../src/Evosim.Dynamics.Tests/FamilyFinLawTests.cs) | the tests for each law |
| [`d228-d229-build/README.md`](../logbook/specs/d228-d229-build/README.md) | the fin bench and the identity checks |
| [`DECISIONS.md`](../DECISIONS.md) D135, D229, D231, D255 | the rulings |
