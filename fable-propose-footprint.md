# Proposal: the footprint — one volume, four regions, a top, a bottom and a wrap

*Fable, 2026-09-05. D076's world rules, for the owner's ruling. Built on
`scratch/footprint-survey.md` (read-only, cited to file and line), logbook/0064 (the
measurement) and logbook/0061 (the surface hole). Absorbed into DECISIONS.md on ruling,
then deleted.*

## What the measurement settled and what it left

Shared space costs nothing (0064): the engine handles 2,000 bodies in one volume at the
same step cost as tiled, and real time holds to ~2,900 either way. Two things bind
instead. **Packing:** 2,000 founders at a mean bounding radius of 0.63 m do not fit in
today's 10 × 10 × 60 m; 20 × 20 × 60 holds them at 9% fill. **Boundaries:** bodies leave
an unbounded box on the 0.3 m/s current in seconds, and the world already has the same
hole at the top (0061: five populations at the waterline and above it) and the bottom
(round 5b's survivors at −131 m in a 60 m world). The footprint is therefore four rules
that are one decision: how big the water is, what a patch is, what happens at every
edge, and where a newborn goes.

## What area means today, and why the footprint cannot be a free parameter

`WorldAreaSquareMetres` is not an extent; it is the sun's aperture and the denominator of
every density the ecology reads — `LayerVolume = Area / Patches × LayerMetres`
(`NutrientField`), `PatchArea = Area / Patches` (`LightField`), the initial matter stock
(`1/m³ × LayerVolume` per layer and patch), the detritus and exudate concentrations, the
D062 satiation knobs, the vent's drag term through `PatchWidthMetres = sqrt(Area /
Patches)`. The matter influx is the one quantity that is *not* a density: 0.6 units/s
lands in the world whatever its area. So the reference world at area 100 is "10 m across"
in every ecological formula while its bodies sit on a lattice kilometres wide. Making
the box literal means the number that sets the packing also sets every concentration,
and there is no way to change one without the other. **Rejected:** a separate physical
scale factor that keeps area 100 for the ecology and a bigger box for the physics — it
reintroduces the fiction D076 exists to end, in a form harder to see than tiling was.

## The rules

1. **The box.** K patches of W × W m side by side on a ring, depth D: the footprint is
   K·W long, W wide, D deep, and `WorldAreaSquareMetres = K·W²` becomes literal. Proposed
   K = 4, **W = 10 m, area 400** (the config's own default), D = 60. 24,000 m³ holds 4,000
   founder-sized bodies at 17% fill, well under the 35% jam. `PatchWidthMetres` stops being
   derived and inert and becomes W.
2. **A patch is a region.** A creature's patch is read from its root's x each metabolic
   step: `patch = floor(x / W) mod K`. The inherited index, the dispersal lottery
   (`DispersalChancePerStep`) and the body-transport lottery of `EVOSIM_CURRENT_ADVECT`
   are retired: bodies change patch because the water carries them or they swim. The
   *fields* keep their per-patch, per-layer structure and their own transport (`Mix`,
   `Advect`, the vent's transport fraction); nothing changes in how matter and food move.
   The vent's plume rises in patch 0's column, over patch 0's floor.
3. **Wrap horizontally.** Periodic in x around the ring (x = K·W is x = 0, patch K−1 next
   to patch 0, which the ring already assumes) and periodic in z across the width. PhysX
   has no periodic boundary, so a root that crosses a seam has its whole articulation
   translated by the box length, velocities untouched, once per physics step after
   `Simulate`. Contacts across a seam are not seen; the seam is one line and the record
   says so. **Rejected:** walls (bodies pile against them under the current, and the
   current's return through a wall is a fiction) and a restoring current (a force the
   drag model would have to explain).
4. **A top and a bottom.** The region above y = 0 and below y = −D becomes *restoring*
   rather than inert: a body above the surface loses its buoyancy clamp's protection and
   falls back at no more than the founder sink rate until it is under; a body below the
   floor is lifted the same way until it is above. Exactly at the surface with no
   velocity a floating body still floats, so today's clamp is the y = 0 limit of the new
   rule. This is `scratch/surface-spec.md`'s option A, applied at both ends, and it is the
   answer D050 asked of every future upward push. A tunable fraction (`EVOSIM_SURFACE_
   RESTORE`, 0 = today, bit-identical) and the `above` / `below` columns to read it.
5. **A newborn is placed beside its parent.** At the parent's position plus an offset of
   the two bounding radii in a random horizontal direction, rejection-sampled against a
   spatial hash of living bounding spheres (the spike's placement, made incremental),
   from a dedicated `Rng` stream so placement changes no other draw. Founders and
   inoculants are placed uniformly in the box at their drawn depth. `TileSpacing` and
   the lattice are retired. A newborn that cannot be placed after the attempt budget is
   a stillbirth of a new kind, counted (`crowded`), not a depenetration.
6. **Contact is counted.** A `contacts` column (pairs per step, from `Physics.ContactEvent`
   with `providesContacts` on, the spike's instrument) and per-patch `alive` columns, so
   the first shared-space world reports what it is doing before any bite exists.

## What this does to the ecology, stated plainly

Quadrupling the area quadruples the sun's aperture, the initial matter stock (24,000
units to drain through burial at founding — the transient 0060 saw, four times larger),
the water every density is spread through, and leaves the influx at 0.6/s, which is then
a quarter of the concentration it was. Every world rule tuned since D048 is read against
a different denominator. That is not a reason to keep the fiction; it is a reason to
re-screen once, with the dose and the surface fix folded in, rather than three times.
The recommendation is to set the influx for the population the box should hold (0.6/s
grew the world to 3,100–3,900 in 30,000 s at area 100 and was still growing; the box
holds 4,000 comfortably) and let the screen read the stock's balance in the new
denominator. The founding drain is the one thing to watch: initial stock 1/m³ in 24,000
m³ is 24,000 units for burial to eat, and `EVOSIM_MATTER_INITIAL` may need to fall with
the area so the founding does not run through a four-times-longer drought.

## The round after the ruling

A screen at 0.02, seeds 2 and 4: the footprint world (rules 1–6) at influx 0.6 and 0.3,
initial stock 1/m³ and 0.25/m³, against 0060's vent arms. Read within one step: `above`
and `below` 0 after t = 3,000; mean height below −5 m; no body outside the box; the
founding minimum; per-patch populations (does the plume's patch hold the stomachs?);
contacts per step; the stock's slope over the last quarter. Then the 0.01 confirmation on
five seeds under D063 as amended, which becomes the reference world if it holds.
Predation's proposal is rewritten on this world once its screen is read.

## What the owner rules

1. The literal footprint: K = 4, W = 10, D = 60 (area 400), with every density re-read.
2. A patch as a region read from position; the two transport lotteries retired.
3. Periodic horizontal boundaries by translation, seam contacts unseen.
4. A restoring top and bottom (surface-spec option A, both ends).
5. Newborns placed beside the parent; the lattice retired; the crowded stillbirth.
6. The screen's doses: influx 0.6 and 0.3; initial stock 1 and 0.25 per m³.
