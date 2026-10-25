# The build plan for D228 and D229: the fin law, the mirrored joint, contact on the true shape, and fat

*2026-10-25, the speed agent, for the coordinator. Branch `fin-shape` off `mouth-point` at `4cfc8107`, in
`scratch/wt-finshape`. Code only while `r62bd` runs: a Sonnet subagent builds each item and I review it. Nothing runs
on the card, and no test beyond the fast filtered ones, until the group has ended. Every rule is behind its own
switch, off by default, so recorded worlds replay. The map of the contact code this plan rests on came from a
read-only survey of the worktree on 2026-10-24. Where a claim is inference, it says so.*

## The order, and what runs beside what

1. **The fin law (D229).** It comes first because its bench is on the CPU and gates the nursery's swimmers.
2. **The mirrored joint (D228).** It is the smallest build, in development only.
3. **Contact on the true shape (D228), on the host.**
4. **Contact on the true shape, on the card.**
5. **Fat (D229).** It is built last and screened only after grazers pass D222.

The items are built one after another on the one branch, so that one fixture re-record and one round of the card's
identity checks cover them all. Two items share no file but the config, so parallel builds would be possible. They
are not planned, because each build's tests would run beside the card group and the coordinator's heat ruling
allows only the fast filtered tests.

After items 1 to 4, once the group has ended:
- the full suites;
- the crowd fixture re-recorded once (every new tunable refuses the old config);
- the fin law's bench on the CPU;
- one card identity round. That round covers the fin law's port, the mirror and contact. Each switch is checked off
  against the D227 build bit for bit, and each switch on is checked at 32 against 64.

Fat follows on the same branch, with its own short identity check when its screen comes.

## 1. The fin law

**What changes.** Today a stroking limb's push uses an added mass of `AddedMassCoefficient · Density · Volume`
(`src/Evosim.Dynamics/Fluid.cs:240` for the ball joint's two axes and `:270` for the hinge). With the switch on, the
added mass follows the limb's shape across the swing:
- a flat blade swung broadside moves `ρ · π/4 · w² · L`;
- a round limb moves `ρ · π · r² · L`.

One formula covers both. `L` is the part's extent along its length, the axis from the joint outward. `w` is its
extent across the motion and across the length. So a round limb of radius `r` has `w = 2r`, and a blade has its
chord. The part's thickness along the motion does not enter, as for a thin plate. The limb's axes come from its
shape and half-extents (`PhenotypePart`) in its link frame, and the motion's direction from the swing (`across` in
`Fluid.cs`).

**The card.** The same arithmetic goes into `gpu-kernel.template`'s limb push. Each link needs the shape's extents
packed beside `SConst.Reach`. The generated kernels are regenerated from the template.

**The switch.** A new `FluidConfig` tunable, carried into `SolverConfig` and bound by the farm. The card refuses a
solver config whose switch differs from the one it was built for (`GpuBackend.Check`).

**The bench.** `src/Evosim.SwimProbe`'s part C strokes chosen bodies by hand in still water. The bench builds a stick
and a paddle of equal volume, strokes both alike, and measures each one's travel and the power it spent, with the law
off and on. With the law off the two travel alike (D135). With it on the paddle must travel further. CPU only, at 4
threads.

**The coordinator's readings of D229 (2026-10-25).**
- *Each shape takes its textbook added mass.* A sphere takes `½ · ρ · V`. D135's coefficient `C_a` is 0.5 in every
  recorded config, so a sphere part's push is unchanged. A capsule takes the strip formula `ρ · π · r² · L` on its
  cylinder, plus its two ends as a sphere. A box takes the plate formula `ρ · π/4 · w² · L`.
- *The price follows the push through the work already billed.* On the powered stroke's path (D135,
  `Fluid.cs:249-274`) each step adds `push · reach · sqrt(swing²) · dt` to `MechanicalWorkJoules`
  (`Fluid.cs:273`). The farm hands that to `World.Observe` each step (`Metabolise.cs:183-202`), and the metabolism
  charges it to the reserve. Since the push grows with the added mass, a wider fin already pays more in proportion.
  So the power price is not scaled as well, which would charge the push twice. The coordinator asked for this check
  and confirmed the reading. The bench reports the work per metre travelled for the stick and the paddle.
- *The lever stays at the limb's centre until the owner rules.* The shape-family proposal
  (`proposals/propose-shape-family.md`) asks whether the push's lever should sit where the added mass sits along the
  limb. On the switch-on path the lever is therefore one computed number per link and direction, equal to `reach`
  today, on the host and on the card. Moving it later is a change to one function on each side.
- *The width in the general case.* `L = 2 · Σ_k |axis · e_k| · h_k` and `w = 2 · Σ_k |n · e_k| · h_k`. Here `n` is the
  unit vector of `axis × motion`, taken after the motion is projected off the axis, so a limb that does not lie along a
  box's long axis has defined calipers. A motion with nothing left after the projection adds no mass.
- *The swing's direction of motion is `across × axis`.* `across` in `Fluid.cs` is the swing's angular velocity
  off the limb's line, so it is the axis the limb turns about. The limb moves along `across × axis`, as each powered
  degree of freedom moves along `dofAxis × axis`. The brief named `across` itself. Read that way, a paddle swung
  broadside would have counted its thickness as its width. The builder found this, and the build uses the cross
  product on both sides.

## 2. The mirrored joint

**What changes.** `Developer` stores a mirrored part's joint anchors unreflected (`src/Evosim.Core/Development/
Developer.cs:268-269`). So every engine that poses a body joins the part where its twin is joined
(`logbook/specs/mirrored-anchor-finding.md`). With the switch on it stores
`ParentAnchorLocal = F_parent · M · anchorOnParent` and `ChildAnchorLocal = F_child · anchorOnChild`, the finding's
derived fix. Everything that reads the anchors follows, since they are made in one place: the solver, the card's
`Pack`, the theatre's recorded poses, `Phenotype.Scaled`, the exports and the mouth point.

**The switch.** A development tunable, read where the anchors are stored. Every caller already passes
`Config.Development` to `Developer.Develop`.

**The checks.** A body with no reflection is bit-identical with the switch on. `MirroredPoseTests` passes with it on
and stays skipped off. The mirror census reads no stacked pair. A world whose bodies reflect becomes a new
realisation from its first mirrored birth.

**What the build found (2026-10-25, `f3b010f1`).**
- *The derivation holds.* From `Decompose` (the full transform is `R · F` with `F = diag(−1, 1, 1)` for a reflected
  part), the anchors that reproduce development's pose are the finding's, `F_parent · M · aP` and `F_child · aC`.
- *The census's development reference was off for mirrored parts.* `--mirror-census` took a part's developed
  joint as `Position + R · aC`, and development's joint is `Position + R · F · aC`. The census now applies the flip.
  The census was run again on the same run with the flip applied, and the finding's counts held, with no pair moving
  from stacked to apart (`d228-d229-build/README.md`).
- *The joint frame does not read the anchors.* `Creature.FrameOf` is a fixed rotation for each joint type.
- *A mirrored twin swings against its original, not as its mirror image.* The hinge axis is `R_c · frame · X`, and
  nothing flips it for a mirrored part. The same drive signal therefore turns the twins in the same sense about
  axes that are mirror images as ordinary vectors. One fin goes down while its twin goes up. This was read in the
  code and measured once by the builder's throwaway probe (both hinges at 0.5 on a chain reflected in x put the tips
  at (0.551, −0.192, 0) and (−0.551, +0.192, 0)), which was not kept. The fix does not change it. Whether a twin's
  drive should be mirrored is a question about what a body is, for the coordinator and the owner.

## 3 and 4. Contact on the true shape

**What the survey found.** It has three findings.
- A part touches as the ball of its half-diagonal: `Creature.LinkReach` (`Creature.cs:692-706`), committed per link
  in `RefreshLinkSpheres` (`:583-605`).
- Contact reads only the committed snapshot of centre, radius and velocity, so that a step never reads another body's
  live state. That keeps the result the same at any thread count. Neither the host's committed spheres nor the card's
  `SSph` rows carry a part's orientation or shape.
- The per-part path (`SolverConfig.ContactPerPart`, D114) is the only one with parts to test, so the new switch
  requires it.

**What changes.** The broadphase stays on the spheres. The half-diagonal bounds every shape, by the survey's reading
of the shape formulas, which a test will check. The narrowphase changes. Each part becomes a convex core with a
margin:
- a sphere is a point with its radius;
- a capsule is a segment with its radius;
- a box is the box shrunk by its smallest half-extent, with that half-extent as the margin. A flat plate's core is
  its rectangle.

Two parts' penetration is the two margins less the distance between their cores, and the normal runs along the
closest points. The distance between cores comes from GJK on their support functions. That is one routine for every
pair, and the superellipsoid of D229's shape family has a support function too, so the routine carries over to it.
Deep cases fall back to the centres' difference, the rule used today. The bed, the reef, the coral and the glass use
the part's support point toward the surface in place of the sphere's lowest point. The grip's rim lever uses the
shape's extent.

**Host.** Commit each link's rotation with its sphere (`CommitContactSphere`). Carry each link's shape and core size
in the solver. Run the narrowphase in `Contacts.ApplyPerPart` and the world tests in `Contacts.cs`, `ContactBed.cs`
and `ContactReef.cs`. Record the touching pair for the mouth by the new test. The pair reported is the first found,
as today (`CreatureContacts.NoteOverlapPart`).

**Card.** Pack each link's shape and core into `SConst`, and the committed orientation into the `SSph` rows, which
grow by about ten floats a link. Then the same narrowphase in `ContactLinks` and `AgainstTheWorld`, the generated
kernels regenerated, and the CPU-accelerator tests extended (`GpuEngineTests`).

**What the card's build changed (2026-10-25).**
- *The pair is computed in its own frame, on both sides.* The first core sits at the origin and the second at minus
  the sphere test's difference, with the seam's wrap and the card's carried rests included. So no absolute
  coordinate enters GJK, and the double kernel can match the solver to the bit. The host's `Apply` was shifted to
  that frame, so the host's switch-on bits are those of `6bbafa95` moved to it.
- *The cores live by link row in the card's globals, not in `SConst`.* A link reads the cores of other bodies'
  links, which may sit in another size class.
- *The switch-off kernels carry the new code.* `ContactLinks` and the class step's `OverlapsAgain` (which re-finds
  the overlaps of a link with more than four) hold GJK's 40-number simplex and its branch even with the switch off.
  The bits cannot change, since every new line sits behind the switch, but the pace might, through registers or
  local memory. So the identity round times a switch-off world against the D227 build on the same seed. If it is
  slower, the true shape's pair block moves to a kernel of its own, launched only when the switch is on. The class
  step's re-find then reads which candidates touched from that kernel's output instead of running GJK itself.

**Cost.** A GJK test does more arithmetic than a ball test, and each card sphere row grows by about ten floats, so
the pace falls. By how much is measured on the bench before any screen (inference until then).

### Level (b): the force at the contact point (host built 2026-10-25)

It is a third value of the switch, `TrueShapeAtPoint`, so no recorded hash moves. The card refuses it until its port.
- *The lever* runs from the link's origin to the contact point. The origin is the link's centre, and every force
  in `Fext` acts there.
- *The damper* reads the point's velocity `v + ω × r`, from a spin committed with the sphere.
- *The torque* `r × F` goes into `Fext`, capped once a touch by the grip's `CapTorque`.

**The surfaces touch at every vertex of the core.** A core's support point is a single corner, and ties go to `+`.
With one point a touch, a plate lying flat carried its whole push on one corner and rocked from corner to corner. It
spun at 0.77 rad/s and tilted 2.4 degrees after 5 s. So the bed, the reef, the coral and the glass now touch at each
of the core's 1, 2 or 4 distinct vertices, each with its own spring and damper. Friction acts once a touch, at the
push-weighted point. A centre of pressure was tried on paper and rejected: it places one push at the depth-weighted
mean of the vertices, but its turning damper vanishes near flat, so a resting plate would keep rocking. Pairs of
bodies keep their single GJK point. Two plates resting face to face may still rock (inference, not measured).

**Each vertex's law reads the mass at that point.** With every vertex at the link's own mass, a plate landing on an
edge gained 0.66 J in one step. A corner of a turning body is lighter than its centre: along the normal it meets
`m_eff = 1 / (1/m + (r × n) · I⁻¹ (r × n))`, 0.17 `m` at a flat plate's corner. Two dampers set for `m`
changed that corner's speed by about 2.4 times its approach in a step, past the explicit damper's limit of 2. Each
vertex's stiffness and damping are now scaled to its `m_eff`. That keeps the law's principle, that every contact
rings at `ω`, and applies it at the point that touches. The choice is one of numerics within D228's level (b), not a
new world rule, and the build agent made it.

**What the tests measure** (`TrueShapeAtPointTests`, 17 tests, all passing):

| Check | Result |
|---|---|
| A plate set down flat, after 5 s | spin 0, tilt 0 |
| A plate dropped on its edge | lies flat by 0.60 s; at `TrueShape` it stays on its edge |
| Its kinetic energy against the first touch's plus gravity's work | never above, with an allowance of 0.05 J |
| The explicit damper's step factor, on an edge, a corner and flat | 0.46, 0.40 and 0.28, against a limit of 2 |
| A flat plate's resting depth | 7.13 mm on four vertices, 4.91 mm on one sphere: the predicted ratio of 1.45 |
| A thin light link pressed on the bed and a cube | largest spin 0.80 rad/s, against an allowance of 9.69 |

The suites on the host build pass:
- Dynamics without the slow tests, 317 and one skip;
- Core without the slow tests, 1,387;
- Farm without the card, slow, `GpuMeanTests` and accelerator tests, 373.

The card port and its identity round come next.

**The coordinator's readings of D228 (2026-10-25).**
- *GJK on cores with margins*, as above, so that the superellipsoid joins later by its support function.
- *The world's surfaces follow the shape too.* The bed, reef, coral and glass test the true shape.
- *The mouth reads the first pair found*, as D219 reads it.

## 5. Fat

**What changes**, by `proposals/propose-new-cell-types.md`. A body's reserve is capped. Fat tissue raises the cap by a
set number of joules per cubic metre, at a low upkeep and a high build price, and it adds weight. That is one world
dial for the cap, and a fat cell type in the cell table. The details of the cap, which bodies it binds today, and the
cell's numbers are drafted from the proposal into its own brief when item 5 begins, and come to the coordinator then.
It is screened after grazers pass D222.

## Not built

The superellipsoid shape family of D229 is drafted as a proposal and built after contact on the true shape.
