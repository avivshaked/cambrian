# The bed with shape: requirements (round 39)

**2026-09-15**  ·  ruled by the owner in conversation, and recorded as D092.

> "I really want a shaped bed, have wanted for a while"; "life like without making it
> computationally problematic"; "doesn't have to be outright cheap, just not too expensive";
> "writeup the requirements and then pencil it in for the next round"

It builds after round 38's read (0101), on a branch, validated as below, and launches as
round 39 with its own pre-registration.

## What it is for

A flat floor under uniform water gives food no place to gather, so a stroke or a sense has
nothing to buy. That is D089's context, and round 38's mid-run look read the same thing: the
fields sat near uniform at `det cv` 0.11 to 0.39 once the disc filled. A shaped bed makes
places: hollows where sinking detritus collects and stays, ridges that shed it, water that
slows in the hollows and quickens over the ridges.

It is a world rule and a replacement under D091. Its round is read for the mechanism, which
is the pockets and where the eaters sit against them, and the goal rule is read and not
required.

## Shape (life-like)

1. **A height map over the disc**, one height per half-metre column, made from the run's
   seed with smooth noise at three scales. The scales are broad basins and rises spanning
   about a third of the tank, ridges and mounds a few metres across, and ripples under a
   metre. The spectrum is red, more relief at the large scales than the small, as a sea
   floor's is. Every seed gets its own floor; a replay gets the same one.

2. **No overhangs, caves or vertical walls**: a height map cannot make them, and that is
   the limit for this round.

3. **The relief is bounded**: the total range is one dial (`BedReliefMetres`,
   `EVOSIM_BED_RELIEF`, default picked from pictures, of the order of a few metres in a
   60 m tank). Slopes are never steeper than about 30 degrees, so bodies settle and detritus
   slides rather than sticking to a cliff. The feature scales are tunables with fixed
   defaults: `BedScaleMetres` for the largest, and the two smaller a fixed fraction of it.

   *As built (2026-09-15, `4ce2ba0` on branch `bed`): the 30° bound holds the three bands on
   their own. The tilt of item 5a is refused above a 25° ramp. The two add where the ramp
   runs up a hollow's wall, with the total reported (`SteepestTotalSlopeRadians`) and not
   capped. One bound on the sum let every metre of tilt buy itself out of the bands' budget.
   At 400 m² a 6 m tilt left the bands a few decimetres and no hollow. At the round's dials,
   about 1 m of relief and 6 m of tilt, the total reads 33 to 38° on the seeds tried, on a
   few columns. The bands alone read 28.7°.*

4. **A few real places per tank**: two to four hollows a body can lie in and food can gather
   in, at least one ridge, the rest gentle. Measured on the generated map at build (the
   count of local minima deeper than a threshold), so the smoke can say it.

5. **The floor meets the glass without a step**, and the mean depth stays the configured
   depth (60 m), so the water volume and the seeded matter density stay round 38's. The
   height map is mean-zero over the disc by construction.

5a. **A gradient across the disc**, added at the owner's word on 2026-09-15. What the owner
   asked for: "the sea bed as a gradient so it's not all one depth; this is something we
   wanted to do from the start". It is a tilt along one diameter, the largest scale of the
   same map. One dial sets the depth difference between the deep side and the shallow arc
   (`BedTiltMetres`, `EVOSIM_BED_TILT`), and it is mean-zero like the rest so the volume
   holds. In a cylinder the
   shallow arc is a shore: a lake in cross-section. Round 39's default keeps the shallowest
   floor below the band the crowd lives in (about 30 m), so the round reads pockets and not
   light. The tilt raised into the lit band is a shelf, and that is a round of its own, a
   light question. The floor breaking the surface is the beach, the first terrestrial round.
   It needs a minimum depth for the floor-following current, a dry mask in the grid and
   rules for a body on sand. The design puts all three after the aquatic work.

   *As ruled the same evening (D093): the tank is 2,200 m² and 45 m deep, and the tilt 30 m.
   That is a 29.6° ramp under a cap raised to 30°. So the shallow arc sits at the lit
   band's floor and the shelf is this round's after all, while the beach stays a later
   round.*

## The water follows the floor (first version, not second)

6. **The streams' potential is written in floor-following coordinates** (a sigma coordinate
   from the floor's height to the surface) and curled in real space. The field is therefore
   divergence-free by construction and has no flow through the floor, and the tangential
   components vanish at the floor as they do at the glass. Water quickens and thins over a
   ridge and slows in a hollow. The closed-form derivative (`StreamsAccelerationAt`) extends
   through the coordinate map, so the fluid acceleration force stays analytic.

7. **The grid's transporter carries it unchanged**, since it takes its face fluxes from the
   same potential (`transport-conserves-spec.md`). Uniform water must stay uniform on the
   sloped grid as it does on the flat one.

8. **Cost bound**: the current's samples per part per step may grow by two to three times.
   The round's per-body pace stays within 15% of round 38's, measured on the smoke. That
   is the ceiling the owner set ("not too expensive"), and it is not a target.

## The grid and the physics

9. **No new cells**: a cell whose centre lies below the floor is masked dead, as the glass
   masks the disc, and no flux crosses a face into a dead cell. Settling keeps today's rule,
   so detritus that reaches the lowest live cell of its column stays, and the floor's low
   spots are where columns are deepest.

10. **One static mesh collider** built at world start from the height map, tens of thousands
    of triangles at half a metre, on the floor's layer with the floor's material. It costs
    nothing per step beyond the contacts bodies already make with the flat floor, and the
    world has no floor object other than this one.

11. **Founders and children are placed above the floor**, the placer's reservation reading
    the floor's height under the point as it reads the glass. A body below the floor by more
    than its radius dies as a counted `Diverged` death, with a dump naming the bed.

12. **Every value in the config and the hash**: relief, scales, the noise's seed use.
    `BedReliefMetres` 0 reproduces today's flat world to the digest, so every recording
    replays.

## The look (theatre only; no hash moves)

13. The theatre rebuilds the same height map from the config and seed, at higher resolution.
    It drapes that map in the sand material it has, with slope shading and the ripples it
    already draws. The marine snow settles on it. The look is tuned by eye from pictures,
    and the key for raw collider shapes (queued) shows the collider mesh.

## Not in this round

Rocks, boulders and overhangs are out: an object in the water column needs flow solved
around obstacles, and the grid has no cell under a ledge. The tilt raised into the lit band,
the shelf, is out, because it changes the light economy on one side and deserves a later
round of its own. The floor breaking the surface, the beach, is out as the first terrestrial
round. So are the vent (D067) as a point source and any change to the eaters, the prices or
the senses.

## Validation before the round

- The constant-field check on the sloped grid: 1 unit/m³ stays within rounding over 600 s
  at the campaign's mixing and current.
- The digest at relief 0 identical to round 38's build over the 600 s box and a 600 s
  tank smoke.
- The sloped current's divergence, floor flux and glass flux measured as the flat one's
  were (D089's check 2, D090's). The divergence is of the order of 1e-4 of the RMS per
  metre, and the normal velocity at the floor and the glass of the order of 1e-7.
- A settling test: `det cv` on a 600 s tank smoke with relief at its default rises above
  the flat world's, and the deepest columns hold the most detritus.
- The pace check: within 15% of round 38's per-body pace on the smoke.
- The theatre's pictures of the smoke from the side and a corner, looked at, before the
  round is pre-registered.

*As run on 2026-09-15 by `scratch/bed-chain.ps1`, with the numbers in HANDOFF's path item
and the commits on branch `bed`.*

*The constant-field check and the flux readings sit in `BedGridTests` and `BedStreamsTests`.
The floor flux is 3e-7 of the RMS, the divergence 3e-4 per metre, and the acceleration
analytic to 0.06%. The box digest at relief 0 is identical over
31 steps, and the 600 s tank at relief 0 identical to round 38's smoke on every stats field.
The shared-space smoke's bed case passed. The bed smoke `r39smoke` founded and closed both
books with 3 hollows and 1 ridge at 1 m of relief and 6 m of tilt.*

*Item 8's pace was read from a 6,000 s pair at round population, `tankpace-flat` against
`tankpace-bed`. It came in 18% over at first, and 12% with one bed sample per part per step.
With the grid's face columns precomputed it came in 6.4% over, 433 against 407 s of wall
per 1,000 s simulated. Every
bed run was bit-identical to the first. The per-body figure is confounded by the
realisation's body count, so the ceiling is read on the whole run's pace.*

## What the round reads

The round reads the pockets: `det cv`, `mat cv`, and the detritus held in the hollows
against the ridges, per column from the grid. It reads where the eaters and the leaves sit
against the hollows, from the positions file. It reads founding and the population against
round 38's, the crowd's spacing, the throws and the pace. Round 38 is the control for every
reading.
