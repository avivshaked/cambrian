# The bed with shape: requirements (round 39)

**2026-09-15**  ·  ruled by the owner in conversation ("I really want a shaped bed, have wanted for a while"; "life like without making it computationally problematic"; "doesn't have to be outright cheap, just not too expensive"; "writeup the requirements and then pencil it in for the next round"); D092 records the ruling. Builds after round 38's read (0101), on a branch, validated as below, launched as round 39 with its own pre-registration.

## What it is for

A flat floor under uniform water gives food no place to gather, so a stroke or a sense
has nothing to buy (D089's context; round 38's mid-run look read the fields near uniform
at `det cv` 0.11 to 0.39 once the disc filled). A shaped bed makes places: hollows where
sinking detritus collects and stays, ridges that shed it, water that slows in the hollows
and quickens over the ridges. It is a world rule and a replacement under D091: its round
is read for the mechanism (pockets, and where the eaters sit against them), the goal rule
read and not required.

## Shape (life-like)

1. **A height map over the disc**, one height per half-metre column, made from the run's
   seed with smooth noise at three scales: broad basins and rises spanning about a third
   of the tank, ridges and mounds a few metres across, and ripples under a metre. The
   spectrum is red, more relief at the large scales than the small, as a sea floor's is.
   Every seed gets its own floor; a replay gets the same one.
2. **No overhangs, caves or vertical walls.** A height map cannot make them, and that is
   the limit for this round.
3. **Bounded relief.** The total range is one dial (`BedReliefMetres`, `EVOSIM_BED_RELIEF`,
   default picked from pictures, of the order of a few metres in a 60 m tank); slopes never
   steeper than about 30 degrees, so bodies settle and detritus slides rather than sticking
   to a cliff. The feature scales are tunables with fixed defaults (`BedScaleMetres` for the
   largest; the two smaller a fixed fraction of it).
4. **A few real places per tank**: two to four hollows a body can lie in and food can
   gather in, at least one ridge, the rest gentle. Measured on the generated map at build
   (the count of local minima deeper than a threshold), so the smoke can say it.
5. **The floor meets the glass without a step**, and the mean depth stays the configured
   depth (60 m), so the water volume and the seeded matter density stay round 38's. The
   height map is mean-zero over the disc by construction.

## The water follows the floor (first version, not second)

6. **The streams' potential is written in floor-following coordinates** (a sigma
   coordinate from the floor's height to the surface) and curled in real space, so the
   field is divergence-free by construction and has no flow through the floor; the
   tangential components vanish at the floor as they do at the glass. Water quickens and
   thins over a ridge and slows in a hollow. The closed-form derivative
   (`StreamsAccelerationAt`) extends through the coordinate map so the fluid acceleration
   force stays analytic.
7. **The grid's transporter carries it unchanged**, since it takes its face fluxes from
   the same potential (`transport-conserves-spec.md`); uniform water must stay uniform on
   the sloped grid as it does on the flat one.
8. **Cost bound**: the current's samples per part per step may grow by two to three times;
   the round's per-body pace within 15% of round 38's, measured on the smoke. That is the
   ceiling the owner set ("not too expensive"), not a target.

## The grid and the physics

9. **No new cells.** A cell whose centre lies below the floor is masked dead, as the glass
   masks the disc; no flux crosses a face into a dead cell. Settling keeps today's rule,
   so detritus that reaches the lowest live cell of its column stays, and the floor's low
   spots are where columns are deepest.
10. **One static mesh collider** built at world start from the height map (tens of
    thousands of triangles at half a metre), on the floor's layer with the floor's material;
    no per-step cost beyond the contacts bodies already make with the flat floor. The world
    has no floor object other than this one.
11. **Founders and children are placed above the floor** (the placer's reservation reads
    the floor's height under the point, as it reads the glass), and a body below the floor
    by more than its radius dies as a counted `Diverged` death with a dump naming the bed.
12. **Every value in the config and the hash**: relief, scales, the noise's seed use.
    `BedReliefMetres` 0 reproduces today's flat world to the digest, so every recording
    replays.

## The look (theatre only; no hash moves)

13. The theatre rebuilds the same height map from the config and seed at higher
    resolution and drapes it in the sand material it has, with slope shading and the
    ripples it already draws; the marine snow settles on it. Tuned by eye from pictures.
    The key for raw collider shapes (queued) shows the collider mesh.

## Not in this round

Rocks, boulders and overhangs (objects in the water column need flow solved around
obstacles, and the grid has no cell under a ledge); a shelf that rises into the lit band
(a later round of its own, since it changes the light economy on one side); the vent
(D067) as a point source; any change to the eaters, the prices or the senses.

## Validation before the round

- The constant-field check on the sloped grid: 1 unit/m³ stays within rounding over 600 s
  at the campaign's mixing and current.
- The digest at relief 0 identical to round 38's build over the 600 s box and a 600 s
  tank smoke.
- The sloped current's divergence, floor flux and glass flux measured as the flat one's
  were (D089's check 2, D090's): divergence of the order of 1e-4 of the RMS per metre,
  normal velocity at the floor and the glass of the order of 1e-7.
- A settling test: `det cv` on a 600 s tank smoke with relief at its default rises above
  the flat world's, and the deepest columns hold the most detritus.
- The pace check: within 15% of round 38's per-body pace on the smoke.
- The theatre's pictures of the smoke from the side and a corner, looked at, before the
  round is pre-registered.

## What the round reads

Pockets (`det cv`, `mat cv`, and the detritus held in the hollows against the ridges, per
column from the grid); where the eaters and the leaves sit against the hollows from the
positions file; founding and the population against round 38's; the crowd's spacing; the
throws; the pace. Round 38 is the control for every reading.
