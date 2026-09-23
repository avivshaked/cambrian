# The beach: the bed raised to the surface along one side (round 46)

*Fable, 2026-09-23 afternoon, from the owner's proposal in conversation ("what we need is the
gradient floor back, and finally put in the beach. that would mean that there would have to be
a point where the floor sits closer to the surface, so the initial evolving stomachs still have
access to the snow"), after the first stomach screen read the reason the eaters cannot found.
What to build so that one side of the tank's floor rises into the lit water and on to a shoal
a metre under the surface; off by default so every recorded world replays bit for bit. It
extends D092's bed (`logbook/specs/bed-spec.md`), which named the beach as a later round and
listed what it would need: a minimum depth for the floor-following current, a dry mask in the
grid, and rules for a body on sand. The first is built here; the other two are not needed,
for the reason in section 1. The mushroom reef is the round after, in its own spec.*

## 0. What the screen said, and what the floor already is

*Corrected the same afternoon, after screen B (`logbook/specs/stomach-screens.md`).* The
stomach inoculated into round 45's world at 5 m starved in a hundred seconds (40 of 40,
median age 104 s) at a snow density of 0.004 J/m³ against a break-even of 0.44. The morning's
reading that the bed forty metres below holds 3.4 J/m³ was a units misread of the table's
`detritusOnFloor`, which is the centre patch's floor stock in joules, 3.5 J; the whole floor
holds 11 J of the 80 kJ standing. At the round's sink and remineralisation rates the snow
lives about eight minutes and falls a metre, so it never reaches a floor: it is a thin layer
under the plant crowd at about 0.5 J/m³ where the crowd is densest, the stomach's break-even,
and the four stomach mutants born inside it during B broke even and bred nothing. So the
larder is thin, not deep, and this spec's premise holds only for a floor within a metre or two
of the crowd or for snow that lives long enough to reach one; the beach stands as the owner's
rule, and the proposal's rule 6 carries the correction.

The gradient floor is not gone. Every round since 39 has run D092's tilt at 30 m in a 45 m
tank (round 45's launcher: `EVOSIM_BED_TILT = 30`), so the floor already rises from 60 m on
one side to 30 m on the other. The shallow arc sits 18 m under the band the crowd lives in
(the islands and the founders at 12 m), which is why it has fed nobody. The beach is that
tilt raised until the floor reaches the lit water and then the surface, and the rules that
lets it.

## 1. The shape

The tilt is the plane it is, mean-zero over the disc (the water volume and the seeded density
unmoved), bounded at 30° as a ramp, its direction drawn from the seed. Two things change.

- **The floor may reach the surface.** `BedShape`'s refusal of a floor within a metre of the
  surface ("the shelf and the beach are rounds of their own") is lifted when the shore dial
  is on. The tilt's own bound stays: at round 45's 22,000 m² (radius 83.7 m) the ramp admits
  96.6 m of tilt, and a tilt of 2 × 45 = 90 m is the plane that touches the surface at one
  point of the rim.
- **A shoal, not dry sand.** The height map is clamped so the floor is never above
  `−BedShoreDepthMetres`: where the plane would rise past that, the floor is a flat shoal at
  that depth. No column is ever dry, so the grid needs no dry mask and no body is ever on
  sand: a body over the shoal is in the top metre of water, which is the case every surfaced
  body is in today (D050's clamp on upward force, full light at the waterline, D111's torque
  off above it). The rules for a body in air are the terrestrial round's, and the dial at 0
  means off, so dry sand is not reachable from this build.

At the round's numbers (tilt 96 m, shore 1 m, before the 1.5 m bands are laid on it), the
arithmetic of the plane: it runs from 93 m at the deep wall to 3 m above the surface at the
shore, clamped to the shoal. The shoal (floor at 1 m) is a strip 7 m wide along the rim,
about 1.4% of the disc. The floor lies inside the founders' 12 m within 26 m of the rim,
about 10% of the disc; inside 24 m, about 23%. The bands add up to 1.5 m of relief to that,
so the smoke prints the shelf's area by depth band from the mask rather than from this
paragraph. The deep wall at 93 m doubles the grid array's depth; the live cells are the
same count, and the cost is memory the smoke's pace check reads.

Why one side and not a ring: a bowl with its rim at the surface and its mean at 45 m needs a
slope over 30° everywhere at this radius (a flat-bottomed bowl's ramp at 30° is 113 m wide,
wider than the tank), and the slope bound is what lets a body lie and snow settle. The
diameter is the one line long enough.

## 2. Where it lives

- **`RunConfig.BedShoreDepthMetres`**, `EVOSIM_BED_SHORE`, group `bed`, default 0 = off, the
  recorded rule (a floor within a metre of the surface refused). Above 0 the clamp is on. It
  is refused below the detritus cell (a shoal thinner than a cell has no live cell over it),
  at or above the depth, and in a box (the bed is the tank's). The header's bed token gains
  `shore 1 m` after the tilt.
- **`RunConfig.BedShoreFadeMetres`**, `EVOSIM_BED_SHORE_FADE`, group `bed`, default 0. The
  current's fade, section 3. Refused above 0 with the shore at 0, and at 0 with the shore on
  (a shore with no fade is the jet of section 3, and nobody should get it by omission).
- **`BedShape`** takes both, applies the clamp inside `Height` so that everything reading the
  map (the grid's mask, the placer, the contact bed, the divergence guard, the theatre's sand
  mesh) sees the shoal without a change of its own, and reports `ShoalAreaFraction` (columns
  at the clamp over live columns) and the shelf area by depth band beside `Hollows` and
  `Ridges`. `RangeMetres`, `HighestMetres` and the slope readings are measured on the
  clamped map; the clamp's crease is a slope discontinuity a piecewise-linear bed draws as it
  draws any other.
- Both are tunables and refuse every `config.json` written before them, round 45's, the void
  launch's and the three fixtures included; the fixtures are re-recorded once on the finished
  base-round build with every tunable in.

## 3. The water at the shore

D092's map is `ỹ = y·D/d` with `d = D − h` the column's own depth, and the Piola transform
multiplies the flat field's horizontal velocity by `D/d`: water quickens over a rise, which is
right, and at a 1 m shoal in a 45 m tank it is a jet at forty-five times the flat speed,
which is the "minimum depth for the floor-following current" the bed spec named. The shoal
is the minimum depth (nothing divides by less than the shore dial), and the jet is taken out
by a fade on the potential:

    A'(x, y, z) = f(d(x, z)) · A(x, y, z),   f(d) = smoothstep((d − shore) / fade)

so the water is still where the column is thinner than the shore, full where it is thicker
than shore + fade, and smooth between. Multiplying a potential by a smooth scalar keeps the
velocity divergence-free (it is still a curl), and no flux crosses the floor or the glass
because the potential's tangential components vanish there and the extra term `∇f × A` is
then tangential. The closed-form acceleration (`StreamsAccelerationAt`) takes the extra
terms through `∇f = −f′(d)·∇h` and the Hessian the bed columns already carry
(`HeightGradientAndHessian`); the finite-difference check that pinned it at 0.06% is the
test. The grid's transporter reads the potential on its edges and carries the fade by
construction (`transport-conserves-spec.md`). The recommended fade is 15 m.

*As built (2026-09-23 afternoon, three passes of the Opus subagent).* The plain fade did not
give the water the paragraph above expected: the fade's own term `∇f × A` is a current along
the shore's contours of the order of `|A|/fade`, and at 15 m it read 2.9 times the tank's RMS
in the band with a peak of 11.4, and 3.5 times over the lit shelf. The shipped fade is the
quintic `f` times a depth factor `m(d/D)` that equals `d/D` on the ramp (cancelling the
Piola stretch `D/d` exactly, so the stretched water over the shelf is the flat field's) and
turns over to 1 across `0.9 D` to `1.1 D` by a C² Hermite blend (`CurrentField.DepthTurnover`,
0.1, a constant and not a tunable). A hard `min(1, d/D)` was tried first and put a shear
sheet in the water at the mean depth, 0.047 m/s at worst, because the factor's slope enters
the velocity; the blend takes it to the stencil's rounding (1.9e-6 m/s). The sweep, round
45's tank at seed 1 and 0.1 m/s, multiples of each form's own tank RMS:

| form | fade | band RMS / max | contour term RMS / max | shelf 6 to 12 m RMS / max | bound | substeps |
|---|---|---|---|---|---|---|
| shipped, `f · m` | 15 | 1.10 / 4.22 | 0.81 / 3.54 | 1.26 / 4.53 | 0.829 m/s | 1 |
| shipped, `f · m` | 40 | 0.92 / 4.09 | 0.39 / 1.84 | 0.21 / 0.95 | 0.870 m/s | 1 |
| plain `f` | 15 | 2.90 / 11.44 | 2.03 / 11.39 | 3.53 / 14.60 | 1.70 m/s | 2 |
| plain `f` | 25 | 1.68 / 6.74 | 0.93 / 4.45 | 1.68 / 6.06 | 1.09 m/s | 2 |
| plain `f` | 40 | 1.22 / 5.40 | 0.49 / 2.22 | 0.65 / 2.58 | 0.79 m/s | 1 |

The shipped form at 15 m is the round's: the shelf's water at about the flat speed, the
contour current under one RMS, one substep. Two things follow from the depth factor. A tilt
that never reaches the shore (round 45's 30 m) is no longer the recorded water when the
shore is on: every column shallower than the mean is slowed by `m` and the renormalisation
speeds the rest by a factor (1.0704 at tilt 30), so the shore dial is a change to the whole
water and not to the shallows alone; shore 0 stays the recorded field bit for bit. And a
band that runs past the mean depth (shore plus fade at or past `D`) is refused, because it
would fade the open water. The readings and the sweep are `BedBeachStreamsTests` and the
Slow `BedBeachContourReadingTests`.

## 4. What follows without a change

- **The grid.** A cell is live when its centre is above the floor; a shoal column has one
  live cell at 1 m cells. Settling stops at the lowest live cell of a column, so snow that
  reaches the shoal stays in the top cell, in full light, which is the prize. The seeded
  density is the budget over the live volume, as now. *As built:* the 5 m matter grid has no
  live cell under a column whose floor is shallower than 2.5 m (18 of 880 columns in round
  45's tank, 2%), and a body there reads, takes from and deposits into the nearest live
  column inward along its radius, 7 m on average and 11 m at worst (`GridField`'s existing
  walk for a dead column; nothing throws and nothing reads zero, `BedBeachTests`). So a leaf
  on the shoal feeds on and fertilises water 2.5 m deep or more a few metres inward, which
  is stated rather than changed.
- **The placer.** A founder's height is drawn to `FounderDepthSpread` and the candidate is
  read against the floor at its column as D092 built it (`TryReserveFounder`); a draw under
  the shelf's floor is handled as any draw inside the rock is today. The smoke counts founders
  placed over the shelf. D109's founders-in-matter rule is unchanged, and the consumer's
  second window (`consumer-founding-spec.md`) places its cohort with the same placer; whether
  that cohort is placed over the shelf by rule is the proposal's question.
- **The contact bed, the divergence guard, the corpse's fall** read the same map.
- **The theatre** drapes the same height map at higher resolution and shows the shoal as a
  flat strip of sand under the surface; `-From snapshot` reads the two fields from the config
  as it reads the tilt.
- **The farm dumps the floor once** beside the fields (`fields/bed.f32`, one height per
  detritus column in the grid's index order, and `layout.json` says so), a recording setting
  that moves no hash, so a read can bin births and snow by floor depth without rebuilding
  `BedShape` in Python. *As built:* written with `layout.json` at the first field dump, for a
  shaped floor on a grid.

## 5. Tests

1. `BedShape` at shore 1 m and tilt 96 m in round 45's tank: the highest floor is
   −1 m to the float, the shoal fraction is within 0.5% of the plane's arithmetic (1.4%), the mean over
   the disc is above −45 m by the clipped sliver and by nothing else, and the same seed at
   shore 0 is refused with the message naming the beach.
2. The sloped streams with the fade: divergence of the order of 1e-4 of the RMS per metre,
   the normal velocity at the floor and the glass of the order of 1e-7 (D092's readings), the
   water speed over a shoal column under 1% of the RMS, and the acceleration analytic to the
   finite difference within 0.1% (`BedStreamsTests`' method).
3. The constant field on the beach grid: 1 unit/m³ stays within rounding over 600 s at the
   campaign's mixing and current (`BedGridTests`' method), and a shoal column's total holds.
4. Settling: a uniform snow seeded over the beach grid ends in the top cell of every shoal
   column and in the lowest live cell of every other, with the total held.
5. With both dials at 0 the crowd fixture's regress is identical in every field with the
   lineage byte-equal, and `ParallelIdentityTests`' word holds.
6. A 600 s smoke of round 45's launcher at the round's dials closes both books, counts its
   founders over the shelf, prints the shelf's area by band and the largest shore speed, and
   its side, top and corner pictures are looked at before the pre-registration is committed.
7. The pace: within 15% of the flat bed's on a 3,000 s pair at dt 0.02 (D092's ceiling; the
   fade is one smoothstep a sample).
8. `RunConfigTests` and `RunConfigJsonTests` catch an omission of either field.

## 6. What the round reads

The snow's column sums over the shelf against the deep columns (`field-map.py` with
`fields/bed.f32`); the eaters' births by floor depth under them (`positions.jsonl` joined to
the lineage's `abs` and `ink` rows); where the plants sit against the shelf; the shore's
current in the pictures; the pace. Round 45 is the control for every reading, and the
pre-registration's consumer clause (K3) is read with the shelf under it.

## 7. Not in this build

Dry sand and a body in air (the terrestrial round). Snow sliding downslope (settling stays
vertical). A second slope for a wider shelf (`BedShelfSlope`, the top of the ramp at half the
slope): the plane's shelf is 10% of the disc at the founders' depth and the second founding
window can be placed on it, so the wider shelf waits on round 46's read. The mushroom reef,
which is a solid with an overhang and needs a masked column in the grid, a shade cast by a
cap, and the current deflected round a stem: its spec follows this one for round 47.
