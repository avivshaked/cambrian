# The shape build (D252): spec

This is the build spec for G4 in `logbook/specs/grazer-film-plan.md`. It is written on main at 045e3daa. The work is
done in a worktree, `scratch/wt-shape-build` on branch `shape-build`. A Sonnet builder writes the code and its tests,
and b4 reviews them. This spec goes to the coordinator before any builder starts.

D233's family is already on main and off in every world. A world turns it on through its shape list and the form
chance. This build makes a family limb swim, eat and float as its true shape. It does that in three places:

- the fin law, on the host and on the card;
- the paths that still read a family part as its box;
- the card's refusal of any world that lists the family.

It adds no tunable and no genome or checkpoint format. So no config hash moves and no fixture is re-recorded.

## What the world runs today

The grazer world (`rounds/env-r62p-t3.ps1` and its chain) drives limbs by the bend-and-power drive. So the limb push
is the powered stroke, and the swing push is never entered. G5 turns on D229's fin law (`EVOSIM_SHAPED_ADDED_MASS`)
beside the family. D231's stroke on the whole assembly stays off.

Under the powered stroke, `Fluid.Apply` calls `Fluid.AddedMassOf` for each degree of freedom. It passes the limb's
axis and the degree's stroke direction. Both are fixed in the link's own frame:
- the axis runs from the joint (`ChildAnchor`) to the link's origin;
- the direction is `JointFrame` applied to the degree's axis, crossed with that axis.

So each degree of freedom of each link has one added mass and one lever for life. Growth changes them only by its
uniform scale. `AddedMassOf` refuses the family today, and so does the card (`FarmGpu.Open`).

## 1. The fin law for a family part

### The law

A family limb takes the strip law that D231 uses for an assembly. Let the strip coordinate s be the distance from the
joint along the limb's axis â. Let n be the unit vector of â × the stroke direction, and c(s) the width along n of
the part's slice at s. Then:

- the added mass is M = ∫ ρπ/4 c(s)² ds;
- its first moment about the joint is I1 = ∫ ρπ/4 c(s)² s ds;
- each degree's push term is q_d · I1_d, where `shapedSum` now adds `LeverOf · AddedMassOf · q`;
- the work line's lever is `AssemblyLeverOf(Σ q I1, Σ q M, reach)`.

The box keeps D229's lines bit for bit, with calipers and the reach as its lever. For a box aligned with its limb,
the calipers are exactly this strip integral, and I1/M is exactly the reach. So the family member nearest the box
lands on the box's numbers, and a test holds it there (section 4).

### The chord, by projection

The slice of a convex body at s has the same width along n as the projection of the whole body onto the plane of â
and n, read at s. That projection is a convex region of the plane. So the build does this:

1. It takes a dense set of the part's surface points, from `Superellipsoid.SurfacePointAndNormal` over a cube lattice
   of R points a face edge.
2. It projects each point to (s, u), with u = x·n.
3. It takes the 2-D convex hull, by Andrew's monotone chain, in a fixed order.
4. It integrates c² and c²·s over the hull, piece by piece between its sorted vertex s values.

The width c is linear on each piece, so Simpson's rule is exact for both integrands there. The answer is
deterministic and needs no root-finding on the shape.

- **The reference.** R = 256, called slicing in the gate.
- **The build.** The smallest R whose M and I1 stay within 2 % of the reference over the bench forms (section 4). The
  builder reports R and the worst error, which I expect near 32.

### When it is computed

A family link's M and I1 are computed for each degree of freedom at body build. They are kept at the reference size,
beside D231's per-degree arrays and indexed like `DofStart`. A resize scales them, M by s³ and I1 by s⁴, as the panels
are scaled. Nothing is computed in the step. The step reads two doubles a degree.

A family part in an assembly is refused cleanly at build while D231's switch is on. So is a family limb under the swing
push. The swing push's direction turns every step, and no world uses it. Each refusal names the switch and D252.

### A departure from D252's wording, accepted as D255

The coordinator accepted it on 2026-10-28, with the four points around it, and the table below is not built.

D252 and the draft before it call for a table: about 64 swing directions on the half-sphere, read by interpolation
each step. That table was drawn up for the swing push, whose direction turns every step. Under the powered stroke,
which every world drives, the direction is fixed for each degree of freedom. So the build computes the exact slice once
per degree.

This keeps what D252 asks for. The water's push is still computed ahead of time and scaled at a resize. It has no
interpolation error and costs nothing in the step. At build it costs fewer hulls than a 64-entry table would. G4's
gate becomes the build resolution against the reference, within 2 %.

If the coordinator holds to the table, the build tabulates instead. The table holds 64 directions on the half-circle
across the limb's axis, not the half-sphere, since the axis is fixed for each link. Each step reads it with linear
interpolation in angle, and the 2 % gate reads the table against slicing.

## 2. The paths that read a family part as its box

A family part's frame has its origin at the centre of mass. That point is `CentreOffset` from the box's centre along
Y. With a taper above 1, the true body reaches up to 1 + |k| half-extents across at its wide end. Each path below is
fixed, or shown not to matter, with a test.

| Path | Today | Change |
|---|---|---|
| Mouth point (`MouthPoint.Local`) | the centre of the box face it mostly points through, in the shifted frame, so up to `CentreOffset` off the true tip | **Fix.** Use the same axis rule, with the point on the true surface along that axis through the origin. That is ±b − y_c along Y, and a·w(t0)·f(t0) along X, or c·w(t0)·f(t0) along Z, with t0 = y_c/b. These are closed forms in float, in a fixed order. The solver, the world's reach test and the theatre all call this one function. |
| Silhouette and D110's hull faces (`Phenotype.MeasureSilhouette`, `ConvexHull.AppendPartCorners`) | the box's eight corners | **Fix.** Use the part's 98 lattice surface points, from `Superellipsoid.SurfacePointAndNormal` at the current size, in place of the corners. A hull of the true surface points stays inside the true hull, as the box's corners do for a box. |
| Self-overlap (`Phenotype` around line 343, `BoxOverlap.Obb.From`) | the half-extents' box, centred on the shifted origin | **Fix.** This is a fifth path, not named in the gate. Use the true bounding box: centre shifted by −y_c along Y, half-heights a·(1 + \|k\|), b and c·(1 + \|k\|). It refuses bodies at birth, so a misplaced box refuses the wrong ones. |
| Thin axis and buoyancy arm (`PhenotypePart.ThinAxis`, `Creature.Growth.cs` around line 209) | the thinnest half-extent, with the arm as a fraction of it | **Shown not to matter.** The offset is a gene measured in half-extents, and any fixed axis is a body the genome describes. The capsule already takes its box's axes on the same ground. A test asserts that the arm's point, at offset ±1, lies inside the true body for every gene corner, so the buoyancy centre never leaves the part. |

The builder also lists every other site that branches on a shape id or reads `HalfExtents` as a box. It gives a fix or
a reason for each. About a dozen files, under `src/Evosim.Core` and `src/Evosim.Dynamics`, are named in the brief.

## 3. The card

`FarmGpu.Open` refuses any world whose shape list holds the family. That refusal exists for D228's true-shape contact,
where the card packs each core as a box. With sphere contact (`ContactShape` Spheres, which every world uses), a family
link touches as its sphere of `FamilyReach` on both engines. The build makes these changes on the card:

- It refuses only the family with true-shape contact.
- It gives the family a shape code in `ShapeCode`.
- It uploads each degree's M and I1 at the link's current size, beside the columns already copied from the creature.
- When the shape code is the family's, the kernel adds q · I1 in place of `ShapedAddedMass`. It also takes the work line's
  lever from the sums, with every product written as `mul.rn` (`src/Evosim.Farm.Gpu/CLAUDE.md`).

The builder regenerates the kernels (`GpuKernelSourceTests`, `EVOSIM_GPU_REGENERATE=1`) and runs the accelerator tests
in a slot the coordinator gives.

**This collides with b9.** b9's nose port edits the same generated kernels. Whichever branch lands second merges and
regenerates.

G0's card identity sitting, with the family on, needs card time that is not booked yet.

## 4. Tests

The tests use xUnit with at most 2 threads, one MSBuild node, BelowNormal, on cores 12 to 15.

- **The bench forms.** The eight gene corners (profile 0.1 or 1, section 0.1 or 2, taper 0.25 or 4) plus the three
  nearest members (box, sphere, capsule). Each is on the fin bench's paddle and stick half-extents
  (`src/Evosim.SwimProbe/FinBench.cs`). The joint is on −Y, on −X, and at a tilted anchor. Each joint has its three
  stroke directions.
- **The gate.** At the build R, M and I1 stay within 2 % of R = 256 over every bench form. The test prints the worst
  case.
- **The box limit.** Family (0.1, 0.1, 1) on an aligned limb gives M within 1 % of the box's caliper value and I1/M
  within 1 % of the reach. The 1 % is the rounding of the 0.1 exponents, and the builder reports the true figure.
- **Growth.** The scaled M and I1 equal a fresh computation at the new size, to rounding.
- **Bit for bit.** A world without the family in its shape list steps as main does, with the fin law on and with it off.
- **The card.** At groups 32 and 64, a family limb's push on the card equals the host's bit for bit, in the accelerator
  tests.
- **The fixes.** One test each for the mouth point (the true tip on the bench forms), the silhouette (inside the true
  hull, and equal to the box's for the box-like member within 1 %), the overlap box (it holds every surface point), and
  the buoyancy arm's inside test over the gene corners.

## 5. G0's measurements, after the build

These are taken in slots the coordinator books.

- **Pace with the family on.** A SwimProbe read on a crowded checkpoint of the t3 world, with the fin law on in both
  arms:
  - as recorded;
  - with every jointed part turned into its nearest family member, which is the worst case.

  The read times the step and the build per body separately. The gate is the step no more than 15 % slower. The build
  cost per birth is reported beside it.
- **Pace with the family off.** The bit-for-bit test above covers it. The pace read is the build's ordinary farm pace
  check on a quiet machine.
- **Card identity sitting** with the family on, at groups 32 and 64.

## The order

1. The Core paths (section 2) and their tests.
2. The fin law on the host (section 1) and its tests.
3. The card (section 3), its regeneration and its tests.
4. The pace read (section 5).

Steps 1 and 2 need no card and no quiet machine. Step 3 waits for a slot beside b9's kernels.

## As built, steps 1 and 2 (branch shape-build, 0eae73ce)

The review changed three of the plans above. The filtered tests passed: Core 118 and Dynamics 127, after one pin was moved to the new reach.

- **The buoyancy arm is fixed, not shown harmless.** On the bench's paddle and stick the thinnest axis is Y, which is the taper's axis. So offset ±1 put the buoyancy centre up to 16 mm past the narrow pole. The arm is now the offset times the true surface's distance along that axis, on the offset's side.
- **A family link's contact sphere is the solid's reach under sphere contact too.** The half-diagonal left a taper-4 part's wide end outside its sphere. The founders' bounding radius and the developer's body-reach prune take the same reach.
- **The silhouette of the box-like member is 7.3 % under the box's.** At exponents of 0.1 the 98 surface points stand about 5 % inside the box's corners. The test asserts the hull lies inside the true hull, which the 1 % comparison above cannot.

The build's resolution is 24 cells a face edge, on a lattice of 25 points. Its worst error against the reference is 1.22 % over 170 cases. A family member nearest the box gives M 0.85 % under the box's caliper value, with a lever equal to the reach. A mirrored twin's cached M and I1 equal values computed from the solver's own anchor and axes, with reflection on and off.
