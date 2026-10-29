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

- **Pace with the family on: the gate, on the card.** Rounds step on the card, and r63b's G0 pace was read there. So the
  15 % gate is read at the card identity sitting. There the family-on world is timed against the same world with the
  family off, on one card and one build. The family-on world's step is to be no more than 15 % slower. The coordinator
  ruled this on 2026-10-29.
- **Pace with the family on: the host diagnostic.** A bench read of a crowded second of the t3 world, as step 4's plan
  below sets out. The fin law is on in both arms:
  - as recorded;
  - with every jointed part turned into its nearest family member, which is the worst case.

  The read times the solver's step and the build per body separately, and reports the build cost per birth. No gate rests
  on it. Beside the card's number, it says whether a slowdown comes from the solver or from the card.
- **Pace with the family off.** The bit-for-bit test above covers it. The pace read is the build's ordinary farm pace
  check on a quiet machine.
- **Card identity sitting** with the family on, at groups 32 and 64. It carries the pace gate's two timed arms.

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

## Step 3's plan: the card (written against nose at 0cb43c60)

The plan was written while round 63b's screen held the machine, from the source alone. Nothing was built.

**What the card already takes from the host.** It packs each body whole at a slot fill and again at every resize. From the host it copies:

- the contact reach (`c.LinkReach`);
- the buoyancy arm (`c.BuoyancyArm`);
- the contact radius;
- the panels;
- the mass and the inertia.

So the step-1 fixes reach the card with no kernel change. Only the fin law reads the shape inside the kernel, and only in two places:

- the powered stroke, once per size class in `gpu-kernel.template`;
- the swing push, around line 5800.

**The changes.**

1. **`FarmGpu.Open`.** It refuses a family world only when `ContactShape` is a true shape. The card packs every core as a box's half-extents, so a family part there would touch as its box. Under sphere contact the refusal is lifted.
2. **`gpu-runner.template`, the packing.**
   - The family gets shape code 3.
   - Two per-degree columns are added, `FamMass` and `FamMoment`, sized like `StrokeMass`. They are allocated whenever `shapedMass` is on, so `ClassSet`'s constructor takes no new parameter. b9's port edits that signature and the call that builds it.
   - At packing, a family link's degrees take `(Real)c.FamilyAddedMass[d]` and `(Real)c.FamilyFirstMoment[d]`.
   - The `else` branch keeps its refusal for any other shape.
3. **`gpu-kernel.template`, the powered stroke.** With shape code 3, each degree adds `q · FamMoment` and `q · FamMass` to two new sums, in the host's order. Here `q` is `sv · omega · sv · omega · (1 − along · along)`, as the host writes it in `Fluid.Apply`. Then:
   - the push is `cfg.LimbPush · familySum`;
   - the lever is `AssemblyLeverOf(familySum, familyMass, linkReach)`;
   - the `LeverOf · ShapedAddedMass` line is not entered;
   - the half-extents are not read for code 3.
4. **The swing push.** The host refuses a family limb under the swing push when the body is built (`RefuseFamilyStroke`), so code 3 never reaches it. Add one guard that names D255, so that a future path cannot read code 3 as a box.
5. **Regenerate** both precisions (`GpuKernelSourceTests`, `EVOSIM_GPU_REGENERATE=1`). Run the accelerator tests in the slot the coordinator gives.

**Merging with b9.** b9's port lands first. Its hunks sit beside four of mine:
- the column lists near lines 75 and 381;
- the `ClassSet` constructor and its call;
- the packing near line 2521.

None of them touches the fin-law lines. I rebase `shape-build` on main once the nose lands, make the edits above on the merged templates, and regenerate once.

**Tests.**
- Family-free bit for bit: the existing accelerator and fixture tests pass unchanged.
- Family on the card: a one-limb family body under the powered stroke, with the fin law on, gives the same digest on the card in double as on the host, step for step, for 2,000 steps. It also gives the same digest at groups 32 and 64.
- The refusal test is moved, to refuse the family only under true-shape contact.

**Machine time.** Regeneration and the filtered card tests take about 30 minutes in a slot with nothing else on the card. G0's identity sitting with the family on takes about 1.5 hours, booked by the coordinator. The sitting adds one arm: the same world with the family off, timed beside the family-on arm on the same card and build. That pair is the 15 % pace gate (section 5).

## As built, step 3 (branch shape-build, 83d8755d)

The nose did not land on main first. On the coordinator's word, `shape-build` merged `nose` at `99258173` instead
(`b90d3ecb`). The card port was written on the merged templates. If the nose changes before it lands, the port is merged
again and the kernels regenerated.

The five changes are as planned. Two departures from the plan were accepted by the coordinator on 2026-10-29.

- **The family's sums use plain products, not `mul.rn`.** The kernel template has no `mul.rn` helper. D231's assembly
  lines, which this branch copies, use plain products too, and they passed their identity sitting. On the CPU
  accelerator in double nothing is fused, so the host comparison holds. On the card, groups 32 and 64 run the same
  compiled kernel.
- **Groups 32 and 64 are told apart only on the card.** The CPU accelerator ignores the group size. It takes its
  device's largest group, so a test at 32 and one at 64 would be one run twice. That identity is the card sitting's.

The off-card checks passed with `CUDA_VISIBLE_DEVICES=-1`, and no process loaded the card's driver:

- The kernels regenerated from their templates, and the source test passed after a rebuild.
- The accelerator class passed 37 of 37 tests. One of them is new. In it, three one-limb family bodies step under the
  powered stroke with the fin law on. The host and the CPU accelerator in double agree digest for digest over 2,000
  steps, and the world parts from the same world with the fin law off.
- The card refuses the family under either true-shape contact and opens it under spheres, as its test asserts.
- The Farm default suite passed 471 of 471 at `b90d3ecb`, with `GpuMeanTests` left out. That test crashes the CPU
  accelerator when its host is pinned, as `src/Evosim.Farm.Gpu/CLAUDE.md` records.

A degree whose axis runs along its limb twists the limb and moves no water across it. The universal joint's first
degree, on an in-line limb, is one, and its M and I1 are 0 on the host as on the card. The family test asks only that
each family limb push through at least one degree.

## Step 4's plan: the pace read

Written during the same hold, from the source alone. Nothing was built.

**The tool is `Evosim.Dynamics.Bench`'s record mode, not a checkpoint.** `--mode record --at <second>` rebuilds a run's
crowd where it stood: every living body's genome from that second's snapshot and its pose from `poses.jsonl`. It then
times a step at each thread count. That is the solver's step on the recorded crowd, and the solver is where the fin law
costs. A checkpoint's bodies cannot be turned into family members without rewriting the checkpoint, which this read
does not need.

Three additions to the bench, behind flags, so its existing modes keep their output:

- **`--shaped`** turns on `ShapedAddedMass` in the config in memory, and checks that the recorded config already drives
  the bend-and-power stroke. Both arms run with it.
- **`--family nearest`** rewrites every node that carries a joint. It does so before development, in three ways:
  - Its shape becomes the superellipsoid, with the form of the nearest member. A box takes (0.1, 0.1, 1), a sphere
    (1, 1, 1) and a capsule (0.5, 1, 1).
  - Its half-extents stay as recorded.
  - The superellipsoid joins the config's shape table in memory, if the run's table lacks it.

  The root keeps its shape, since it has no joint and no push.
- **Record mode reads the farm's present files.** It reads `poses.jsonl` and plain snapshots today, and the t3 run
  holds `poses.bin` with its index and gzipped compact snapshots. Snapshots go through `RecordFiles.ReadSnapshot`, and
  poses through the farm's pose reader or a port of it. A compact snapshot row carries each body's growth fraction, so
  each body is built at its recorded size rather than at adult size.
- **The build is timed apart from the step.** One stopwatch around `Developer.Develop` and the body's construction,
  summed over the crowd, is reported as microseconds per body in each arm.

The read, in a quiet slot the coordinator books: second 20700 of `runs/r62p-t3-6201` (run directory
`2026-10-26-034325-a9cd635c`), recorded arm then converted arm, at the farm's thread count.
- That is the run's most crowded snapshot: 9036 bodies alive, by its `stats.jsonl`.
- Of those, 484 are jointed, with 1061 degrees of freedom between them. They are the only bodies the converted arm
  changes.
- So the gate measures the world's pace on the real mix. The bench also reports the step per jointed body, which gives
  the fin law's cost per limb beside it.

The arms run at the farm's thread count. Each arm has 200 warm-up steps and 2000 timed steps, three repeats each, and the median is
taken. The converted arm's step against the recorded arm's is the host's share of any slowdown; the gate itself is read
on the card (section 5). The bench counts the bodies it
refused and the poses it left at rest. The two arms must agree on both counts, or the comparison is of different crowds.

## As built, step 4: G0's measurements (branch shape-build, b018c2bb)

Both reads ran on 2026-10-29, after `r63c` ended, at `b018c2bb`. That is the card port with the bench's record mode
(`f0b46db7`) and the sitting's two world launchers.

### The card sitting

Five card groups of three worlds ran one after another, each on seeds 6202, 6203 and 6204, founded for 600 s at 10
threads on cores 22 to 31:

1. "off": `rounds/env-r62cf.ps1`, every new switch off, at group 32;
2. "fin": `rounds/env-r62cf-fin.ps1`, D229's fin law on, at group 32;
3. "family": `rounds/env-r62cf-family.ps1`, the fin law on, the family listed and its form genes walking at 0.05, at
   group 32;
4. "fin" again, at group 32, to repeat the baseline;
5. "family" again, at group 64.

The scripts and reads are under `scratch/shape-build/card/`.

- **With every switch off, the build steps `r62cf` bit for bit.** On each seed, all 60 stats samples to 600 s and all
  601 digest rows to step 60,000 equal `r62cf`'s own. The lineage is byte-equal over 1,688, 1,452 and 1,410 lines.
- **The family gives the same digest at groups 32 and 64.** On each seed the stats and digests are identical, and the
  lineage is byte-equal over 1,713, 1,501 and 1,363 lines.
- **The pace gate passes.** The walls, summed over the three seeds, are 7.940 min for "off", 8.192 for "fin", 7.946 for
  "family" and 7.898 for the repeated "fin". The family against the two fin baselines is 0.970 and 1.006, and against
  "off" it is 1.001. The two fin runs differ by 3.7 %, which is the sitting's own spread. Every world ran at 3.6 to 4.0
  times real time, with the CPU at 77 °C at most.
- **The caveat: the card pair times a world with almost no family in it.** The founders hold no family part, so every
  family limb is a mutant's. At 600 s, 7 of the 1,091 jointed bodies across the three family worlds carry one (3 of 332,
  0 of 413 and 4 of 346), and each sits at the member it entered at (`scripts/reads/shape/g5-read.py`). The card gate
  is the realistic early world. The host read below is the worst case.

### The host read

The bench's record mode ran on `runs/r62p-t3-6201` at 20,700 s: 9,036 bodies, 484 of them jointed, 14 threads, the fin
law on in both arms, and three repeats of 2,000 steps. The run's own config predates nine tunables and the build refuses
it. So the read used that file forwarded by `scripts/nursery-config-forward.py`, each key at the default that leaves
the world as it ran (`scratch/shape-build/r62p-t3-6201-config-fwd.json`).

- **The recorded crowd** steps at a median of 3.539 ms (3.539, 3.539, 3.481).
- **Every jointed node turned family** steps at 3.587 ms (3.341, 3.610, 3.587), which is +1.4 %. The nodes were made from
  471 boxes, 2,288 spheres and 1,592 capsules.
- **The build costs more.** It takes 120.4 µs a body against 16.4, because the strip integral runs once per degree at
  build. At the working estimate of 40,000 births an hour that is about 1.3 ms a wall second (inference).
- **A departure from the condition above, which the coordinator accepted.** The two arms agree on the refused count,
  1,719, but not on bodies at rest: 0 against 3. In the family arm 485 bodies are jointed against 484, with 1,062 degrees
  of freedom against 1,061. Three bodies developed differently once their jointed nodes took the family's shape, so their
  recorded joint angles no longer fit, and the bench left them at rest. The cause is inferred, not traced: the
  self-overlap test reads a family part's true shape (step 1), which is smaller than its box, so a child that was pruned
  can grow. A body at rest costs less, so the family arm's +1.4 % may read low, by at most about three bodies' share,
  about 0.03 %. That is two orders of magnitude inside the bar. No rerun was taken.
