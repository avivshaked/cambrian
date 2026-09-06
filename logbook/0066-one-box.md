# 0066 — One box

*2026-09-06. Not a round: the build record for D077, the footprint world, written from
the implementing agent's report (`scratch/footprint-build-report.md`). The screen that
reads it is 0065.*

## What was built

Three commits — `686561e` (a top and a bottom that push back), `8690a92` (the box, the
regions, the wrap, the place beside the parent) and `78cb35c` (the boundary's strength
corrected) — 527 Core tests, two tunables: `SharedSpace` (`EVOSIM_SHARED_SPACE`, default
off) and `SurfaceRestoringFraction` (`EVOSIM_SURFACE_RESTORE`, default 0). At the
defaults the record replays byte for byte: 120 of 120 rows over 12,000 s at dt 0.01,
twice, against baselines recorded before a line was touched.

- **The box** is K patches of W × W on a ring, D deep, with W read from the one place the
  fields already derive it (`PatchWidthMetres`), so the header cannot describe a box the
  simulation does not have: `space shared 4x10x10 m, depth 60, wrap`.
- **A patch is a region.** Each metabolic step a creature's patch is read from its root's
  x. The dispersal lottery and the body-transport lottery do nothing in a shared world and
  never draw from the RNG — a test asserts a shared world with both on is step-for-step
  identical to one with both off.
- **The wrap** translates an articulation at the seam after each `Simulate`, counted as
  `wraps`; distances across the seam are minimum-image, applied only when a box exists,
  after the first arm read a wrap as an 82 m/s swim.
- **Out of the water a body feels its whole weight.** The rule as proposed — a restoring
  push capped at the founder sink rate — was measured wrong before launch: the founder
  sink is ~2 mm/s, the plume lifts at 50 mm/s, and 1,634 of 2,245 bodies sat parked at the
  waterline. The rule is now the physics: no buoyant support above y = 0, the drag model
  damping the fall to about a metre a second; `Max(netDensity, restoring)` above the line
  so a body a hair less dense than water does not fall five hundred times slower than the
  neutral one beside it. Below the floor, the mirror, as a placeholder for a real sea bed.
  Re-measured: `above` 0 at every sample, the population at −8 m where the tiled world
  sits at −14 m.
- **A newborn is placed beside its parent**, overlap-free against a spatial hash (cell
  2 × the largest bounding radius, 1.98 m with real founders), 64 attempts with the
  direction re-drawn, from a dedicated RNG stream; on exhaustion a **crowded stillbirth**,
  counted, the parent keeping what it did not spend. Floor founders and inoculants that
  cannot be placed are refused and counted the same way, and still count against their
  budgets, so the floor samples the world rather than packing it.
- **Instruments:** `above`, `wraps`, `crowded`, `contacts` (mean pairs per physics step;
  an em-dash in a tiled world), `p0`..`p3`; footer facts for all of them; the theatre draws
  the box and the seams.

## What the first arms showed

`fp-smoke2` (3,000 s, dt 0.02, seed 2, the reference vent world in the box): audit
0.0000%, no divergence, `above` 0 at 28 of 31 samples and 1 at the other three (a creature
caught crossing), mean height −8 m, **1,161 contact pairs per physics step**, 273,166
wraps, and **45,941 crowded stillbirths against ~2,300 births** — the plume's conveyor
delivers the population to the top of its column and the film fills. Patches p0 467,
p1 1,016, p2 66, p3 1: the plume's, not the box's. Wall clock 3.6× the tiled world's at
the same settings, with twice the population at thirty times the densest cell the spike
measured — not a cost ratio for shared space, a cost of the crowd the vent makes.

A founder placed exactly on the floor now bounces at ~1 m/s in the first seconds, so
`max m/s` during founding is the floor's rule, not locomotion; founding a metre off the
floor, or a real floor collider, is the owner's fix.

## Addendum, 2026-09-06: the floor is real

Commit `a268311` (`scratch/floor-build-report.md`): a static box collider with its top
face at exactly y = −D, 50 × 2 × 20 m at the screen's settings (the ring with 5 m past
each seam), on the creatures' layer with the project's default material (no bounce), and
the restoring mirror below the floor retired for the shared branch. Placement may only
*raise* a body: every founder, inoculant and newborn is lifted until its bounding sphere
clears the bed by 5 cm, so a parent on the bed breeds beside itself. `floor con` counts
floor pairs apart from creature pairs. Tiled replay identity held (20 rows, 0 differing
cells); the shared smoke placed 200 founders with none in the rock and pushed ten into it
and ten above the surface, all back. The full 20,000-s replay of `r25q-s2`'s settings
diverged **zero** times against the recorded three, tracking the original within a few
percent of population and 2 m of depth. Wall clock within 3%.

One reading of mine corrected by the build: the ~1 m/s speed in a shared world's first
seconds was never the floor. With the bed in it does not move (1.06 m/s at t=5 s in both
builds); with the surface rule off it drops to 0.35. A buoyant founder drawn in the top
metre crosses the waterline, loses its buoyancy, falls back at the rule's terminal speed,
and repeats. Founding's `bestSpeed` is that ring, not locomotion; keeping the founder
draw a metre under the surface would end it and is a world rule for the owner.
