# 0066 — One box

**2026-09-06**  ·  a build record, not a round

The footprint world is one box. It holds four regions on a ring, sixty metres deep, with a
wrap at the seams, a top and a bottom that push back, and newborns placed beside their
parents. At the defaults every earlier run still replays byte for byte. The first arms in it
showed what the vent does with a crowd. There were 1,161 contact pairs per physics step, and
45,941 crowded stillbirths against about 2,300 births.

This is D077's build, written from the implementing agent's report,
`scratch/footprint-build-report.md`. The screen that reads it is 0065.

## What was built

Three commits, 527 Core tests, and two tunables.

| commit | what it carries |
|---|---|
| `686561e` | a top and a bottom that push back |
| `8690a92` | the box, the regions, the wrap, the place beside the parent |
| `78cb35c` | the boundary's strength corrected |

| tunable | default |
|---|---|
| `SharedSpace` (`EVOSIM_SHARED_SPACE`) | off |
| `SurfaceRestoringFraction` (`EVOSIM_SURFACE_RESTORE`) | 0 |

At those defaults the earlier runs replay byte for byte. The check is 120 of 120 rows over
12,000 s at dt 0.01, twice, against baselines recorded before a line was touched.

The box is K patches of W × W on a ring, D deep. The width W is read from the one place the
fields already derive it, `PatchWidthMetres`, so the header cannot describe a box the
simulation does not have. The header token reads
`space shared 4x10x10 m, depth 60, wrap`.

A patch is now a region. Each metabolic step, a creature's patch is read from its root's x.

The dispersal lottery and the body-transport lottery do nothing in a shared world and never
draw from the RNG. A test asserts that a shared world with both on is step-for-step
identical to one with both off.

The wrap translates an articulation at the seam after each `Simulate`, counted as `wraps`.
Distances across the seam are minimum-image, applied only when a box exists, after the first
arm read a wrap as an 82 m/s swim.

Out of the water a body feels its whole weight. The rule as proposed capped a restoring push
at the founder sink rate, and it was measured wrong before launch. The founder sink is about
2 mm/s, the plume lifts at 50 mm/s, and 1,634 of 2,245 bodies sat parked at the waterline.

The rule is now the physics. There is no buoyant support above y = 0, and the drag model
damps the fall to about a metre a second. Above the line the rule takes
`Max(netDensity, restoring)`, so that a body a hair less dense than water does not fall five
hundred times slower than the neutral one beside it. Below the floor sits the mirror, as a
placeholder for a real sea bed.

Re-measured, `above` reads 0 at every sample, with the population at −8 m where the tiled
world sits at −14 m.

A newborn is placed beside its parent, overlap-free against a spatial hash. The cell is
twice the largest bounding radius, at 1.98 m with real founders, and there are 64 attempts
with the direction re-drawn, from a dedicated RNG stream.

On exhaustion the birth becomes a crowded stillbirth, counted, with the parent keeping what
it did not spend. Floor founders and inoculants that cannot be placed are refused and
counted the same way. They still count against their budgets, so the floor samples the world
rather than packing it.

| instrument | what it carries |
|---|---|
| `above` | bodies over the waterline at the sample |
| `wraps` | seam translations |
| `crowded` | stillbirths for want of room |
| `contacts` | mean pairs per physics step; an em-dash in a tiled world |
| `p0`..`p3` | the population by region |

The footer carries facts for all of them, and the theatre draws the box and the seams.

## What the first arms showed

The smoke arm `fp-smoke2` ran 3,000 s at dt 0.02 on seed 2, in the reference vent world in
the box. The audit read 0.0000% with no divergence, and `above` read 0 at 28 of 31 samples
and 1 at the other three, a creature caught crossing.

Mean height was −8 m. There were 1,161 contact pairs per physics step, 273,166 wraps, and
45,941 crowded stillbirths against about 2,300 births. The plume's conveyor delivers the
population to the top of its column, and the film fills.

The four patches read 467, 1,016, 66 and 1. That is the plume's shape rather than the box's.

Wall clock came in at 3.6 times the tiled world's at the same settings, with twice the
population at thirty times the densest cell the spike measured. That is not a cost ratio for
shared space; it is the cost of the crowd the vent makes.

A founder placed on the floor now bounces at about 1 m/s in the first seconds, so `max m/s`
during founding is the floor's rule rather than locomotion. Founding a metre off the floor,
or a real floor collider, is the owner's fix.

## The floor is real, as of 2026-09-06

Commit `a268311` gives the world a static box collider, with its top face at y = −D. It
measures 50 × 2 × 20 m at the screen's settings, which is the ring with 5 m past each seam.
It sits on the creatures' layer with the project's default material and no bounce, and the
restoring mirror below the floor is retired for the shared branch. The build report is
`scratch/floor-build-report.md`.

Placement may only raise a body. Every founder, inoculant and newborn is lifted until its
bounding sphere clears the bed by 5 cm, so a parent on the bed breeds beside itself. The
column `floor con` counts floor pairs apart from creature pairs.

Tiled replay identity held, at 20 rows with 0 differing cells. The shared smoke placed 200
founders with none in the rock, then pushed ten into it and ten above the surface, and all
twenty came back.

The full 20,000-s replay of `r25q-s2`'s settings diverged zero times against the recorded
three, tracking the original within a few percent of population and 2 m of depth. Wall clock
was within 3%.

The build corrected one reading of mine. The 1 m/s speed in a shared world's first seconds
was never the floor. With the bed in, it does not move, at 1.06 m/s at t=5 s in both builds.
With the surface rule off it drops to 0.35.

A buoyant founder drawn in the top metre crosses the waterline, loses its buoyancy, falls
back at the rule's terminal speed, and repeats. Founding's `bestSpeed` is that ring rather
than locomotion. Keeping the founder draw a metre under the surface would end it, and that
is a world rule for the owner.
