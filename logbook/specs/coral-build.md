# Round 62's coral: the build spec

Draft of 2026-10-17, by the coordinator, for the agents who build it and for the owner's look before the build
starts. It turns D194 into work. D194 settled what the coral is. This file says how it is built, in what order,
and how each step is proven. The geometry, costs and code paths it relies on are in
`logbook/specs/coral-survey.txt` (the survey), cited here by section rather than restated. Numbers marked "start"
are starting values for the screen. They are read there and set before round 62's pre-registration, and none is
a ruling.

## What D194 fixed

- Every reef becomes branching coral, trees of capsules and plates rooted on the bed.
- At least 80% of the coral grows from island columns. The rest stands in the desert as stepping stones.
- The coral roofs 20% of each island, as a dial.
- The water under coral is partly lit, from a precomputed canopy map.
- The coral's fade on the current has a floor, so water keeps moving through a thicket.
- The island test stays as it is, so rounds 60 and 61 keep their worlds.
- The theatre keeps the mushroom drawing for older runs.
- Before the build, the reefs' share of a step is measured, and the card computes the current's fade once per
  link rather than twice. A screen on one seed comes before the pre-registration.

## Before the build: two pieces of engine work

1. The reefs' share of a step. A round 61 checkpoint is resumed for a few minutes on the card with the probe on.
   It runs once as it is and once from a scratch copy of its config with no reefs. The difference is R0, the unit every
   cost below is stated in. Measured on 2026-10-18 by phase probes instead of a reef-less copy: `r61-p6101`
   from 48,000 s for 240 s, on 41086157 and on the fade change below. One reef fade costs about 168 kilocycles a
   link. On today's build a link paid two, 47% of its water and fluid work. The fade was about 0.28 ms of a 1.97 ms
   card step (14%). The card is about 46% of the wall, so that is about 6% of the whole step. Reef contact
   with bodies is negligible (10 to 22 kilocycles of a body's 370 to 1,730). The host's reef work, light and shade,
   is not in the measurement. R0 is therefore one fade a link, and the colony's hull has to cost about that or less.
2. The fade once per link on the card. The card's water pass calls the reef fade from both `VelocityAt` and
   `AccelerationAt` (survey section 3), while the CPU shares one evaluation. The change computes it once and
   passes it to both. If the two evaluations are the same expressions, it is bit-identical and is gated like the
   giants fix. If not, it lands as part of round 62's new realisation. Built on branch `r61-fade` (6d070df4) and
   proven bit-identical on 2026-10-18. The probe builds wrote the same rows to 48,240 s. The release build
   matched gate g1 on `r61-p6101` (45,000 to 45,500 s) and h6 on `r61-c6101x` (55,500 to 56,000 s) in every
   field. It cuts the water and fluid passes from 0.59 to 0.37 ms and the card's step by about 11%, leaving the
   fade about 7% of it. It lands with round 62 and stays off round 61's build.

## The colony

A colony is a tree grown from one point on the bed. Its draws come from the reef stream, which D192 seeds from
the map's seed, so a seed means one coral.

- The trunk is a vertical capsule from the bed's height at (x, z). Its height is drawn between a minimum and a
  maximum (start 3 to 8 m), and its radius likewise (start 0.35 to 0.6 m).
- Each segment's top spawns 2 or 3 children, as drawn. Each child is tilted from its parent's axis by a drawn
  angle (start mean 35 degrees, spread 15) and turned about it by a drawn azimuth. It is shorter than its parent
  by a factor of 0.75 and thinner by 0.7 (both start).
- Growth stops at depth 4, at 60 pieces a colony, or at a radius of 0.12 m (all start). It also stops where a
  piece would come within 2 m of the surface or inside the glass margin.
- A tip becomes a flat plate with a drawn probability (start 0.3). A plate is a horizontal disc swept by a sphere,
  of radius 0.8 to 2.5 m and thickness 0.25 m (start). Plates give tables, perches at lit depths, and the canopy's
  shade.
- A capsule's distance needs no trig: it takes one projection, a clamp and a square root. A plate's distance is
  `length((max(|xz| - R, 0), y)) - t/2` in its own frame. Both have closed-form gradients. Azimuths are drawn as a
  cosine and sine pair at placement only, so no Atan2 or trig enters a per-step path.
- Within a colony, pieces join by today's cubic smooth minimum (start fillet 0.15 m), folded in ascending piece
  order. Colonies join by a plain minimum, ties going to the lower index, as reefs do now.

## Placement

- The island map is built before the coral. It is a pure function of the map's seed (survey section 7).
- The island test is the coral's own rule, on the matter grid's columns without reefs (the survey's option (a)).
  Rounds 60 and 61 are untouched, as D194 requires. A few columns at the islands' edges can differ from the
  seeding's, and the screen counts them.
- Each colony first draws whether it is an island colony or a desert one. The island share starts at 0.85, at
  least D194's 0.8. It then draws a centre on a column of that kind, with today's 4,000-try refusal.
- Island colonies are added until each island's canopy share reaches CoralIslandRoof (start 0.2, D194's dial).
  The share is counted on the canopy map's columns, island by island. A colony that would push its island past
  the share by more than half its own canopy is drawn again elsewhere.
- Desert colonies keep the islands' 30 m glass margin, for D185's reason that the wall must not become a habitat.
  Their number makes the desert's canopy (1 - share)/share of the islands'.
- Launch refuses a world past its caps: 200 colonies, 60 pieces a colony and 6,000 pieces in all (start), as
  `ReefMaxCount` does today.

## The readers, split by need (survey option 3)

Contact, touch, grip, the divergence guard and placement read the true union of capsules and plates. A body meets
a branch from the side. The contact needs only the distance and gradient, so this path computes no Hessian.

The current's fade reads one smooth hull per colony instead of its pieces. The hull is an ellipsoid fitted to the
colony's extent. Its distance is the scaled sphere's, `(|M p| - 1) * min(a)`. That is smooth away from the centre
and has a closed-form gradient and Hessian, as the streams need (survey section 1). The fade is today's function
of that distance, with a floor inside the hull (start 0.25, D194's floor). The product over colonies stays in
index order. The Courant ceiling is derived again for the new fades at world creation.

Light reads a canopy map baked once at world creation. For each 1 m column it keeps the canopy's top height and a
transmission. The transmission is `1 - c (1 - CoralTransmission)`, where c is the column's share covered by the
projected pieces, sampled on a 4 by 4 sub-grid. A body below the canopy top in its column gets that transmission,
and a body above it gets 1. CoralTransmission starts at 0.3, so the water under coral is partly lit. The map
replaces the per-call loops in the light and in `LightTransmission`.

Corpses come to rest on plate tops. The canopy map also keeps each column's highest plate top, and a sinking
corpse stops there as it stops on a cap today (D127). Thin branches let corpses pass to the bed.

The matter and nutrient grids mask cells whose centre lies in rock, as now. Thin branches mask almost nothing,
and plates thinner than a cell mask nothing, so snow passes through the coral. The mushrooms' tables caught snow,
so this is a change, and the screen reports it.

## The static grid

A 2D grid of columns over the coral pieces is built once at world creation. It is kept apart from the bodies'
contact grid, so coral never competes for the candidate cap (D193 and round 61's giants). The cell size is a dial
(start 4 m). Each cell lists, in ascending order, every piece whose horizontal box, widened by the fillet, meets
the cell.

A query reads the cells under its sphere's horizontal disc. It merges their lists in ascending order without
repeats. It skips any colony whose box is farther than its limit before reading the colony's pieces. The card
holds the same lists, in the same order, beside the reef rows.

The grid gives the same bits as a scan of every piece when every piece that can come under the limit is listed
and the lists stay ascending. A test proves it on the CPU and on the card at query radii up to 20 m. Round 61's
parts reached an 18.4 m half-diagonal.

## Step 2, as built

Built on the CPU on branch `r62-card` on 2026-10-19, in CoralRock and its readers. Six things differ from the
plan above.

1. The grid lists colonies, where the plan listed pieces. A smooth minimum folded in order does not give the same bits when a far
   piece is left out. The piece can move the running value, which a later near piece then meets inside the fillet.
   A cell therefore lists every colony whose widened box meets it, and a query reads every piece of each colony it
   cannot rule out. The bound that rules a colony out is its box's distance, less its thickest piece and a sixth of
   the fillet for each fold. The grid then gives a scan's bits whenever the distance is under the query's limit.
   The test holds that over about 2,500 queries a world, at limits up to 20 m.
2. The hull's distance is the ellipsoid's near-Euclidean estimate, and no hull is flatter than 2 to 1. The scaled
   sphere's distance named above stretched the 8 m fade by each hull's aspect. In the test tank one colony's fade
   reached 94 m along its long axis, which would have slowed the water over whole islands. The estimate,
   `k0 (k0 - 1) / k1` with `k0 = |p/a|` and `k1 = |p/a^2|`, is exact along the axes. Off them it strays as the
   aspect grows. At an 8 m fade it ends the fade within 10 m of a hull of aspect 2, 13 m at 3 and 42 m at 11. A
   flat colony's short axes are raised to half its longest (CoralRock.HullAspect), and its hull is thicker than the colony.
   The fade now reaches at most 18 m from a colony's centre in both test worlds.
3. In D204's square every reader is periodic. A body's parts are not folded with its root, and a fade can reach
   past a face. A distance query folds its point into the box and also reads the image across any face within its
   limit. The hulls are read the short way round, and the canopy's column is the folded point's. A hull whose fade
   would reach half the box is refused.
4. A founder is never landed on coral. A spot that does not clear the rock is drawn again, as a spot in a stem is.
5. Corpses rest on the highest plate top over their column when it is under them, as on a cap.
6. The manifest keeps the coral's counts and its digest, where the plan kept every piece. The pieces are placed again from the
   seed and the config, as the reefs are on every load. The digest checks that the coral is the same. Only a coral
   world writes these fields, so every other manifest is unchanged. The run header names the coral on the same
   rule.

The card refuses a coral world until step 3. On the CPU the tests measure the following.

- The faded water's divergence, by a 12.5 mm stencil, is at most 1.4e-5 /s in the square's ocean. The water there
  runs at up to 0.35 m/s. In the tank's streams it is 5.4e-5 /s.
- The analytic acceleration matches a stencil to 0.03 to 0.05% RMS.
- Water beyond every fade is the unfaded water to the bit. Where the fade is under one half, the water runs at
  0.36 of the open water's mean speed in the square and 0.54 in the tank.
- Across a seam the faded ocean parts by 7e-7 m/s, and the distance and fade by 2e-14.
- A sphere sinking onto a plate rests within 2.4 mm of the plate's top plus its radius. One pressed into a trunk
  is pushed out.
- The mushrooms' tests and the default Core suite (1,252 tests) pass unchanged.

The fade reads a colony list per cell, as the distance does, on the same cells. A cell lists, in ascending order,
every colony whose cull box meets it. The cull box is the hull's semi-axes times 1 + fade/least semi-axis, beyond
which `HullDistance` returns infinity before any arithmetic. In the square the box is listed across the seams, and
the point's cell is its folded place's. In a tank the edge cells run on to infinity. Each span is widened by a
millimetre and a millionth, so rounding in the cull or in the point's cell never reads a list short of a hull it
keeps. A hull listed but culled adds nothing, and the product runs in colony order either way, so the lists move
no bits. `TheFadesListsGiveTheScansBits` holds the value, gradient and Hessian against the full scan on 20,000
points a rock. The card reads the same lists from two more blocks of the world's reals.

On round 62's square (30 colonies), timed on one thread with `OceanTracers --coral` beside a farm run, the fade
fell from 0.57 µs a call to 0.13 µs. A link's water, which calls it twice, fell from 5.63 µs to 3.74 µs, at the
same checksum. That fall is more than twice the fade's own, so part of it is likely noise in a reading taken on a
loaded machine. Without the lists the fade's cost grows with the colony count, about seven times at the 200-colony
cap. With them it grows only with the colonies near a point.

## Step 3, as built

Built on branch `r62-card` on 2026-10-19. The kernel template transcribes CoralRock's distance and its hulls'
fade line for line. That covers the colony grid, the pieces and their smooth union, the square's fold and images,
and the fade's gradient and Hessian. The kernel reads them where it reads the reefs'. The water's velocity and
acceleration take the coral's fade, in the tank's streams and in the box ocean. Contact takes the coral's
distance, on the body sphere and on each link. Light, placement, corpses and the divergence guard run on the host, which already reads
the coral.

GpuWorld packs the coral into the world's reals, as the reefs are packed: sixteen numbers a colony, nine a piece,
then the grid's cell starts and colony lists. Every index is under 2^24, so a list survives single precision.
The rows are read from CoralRock's private arrays by reflection, so `GpuCoralTests.TheRowsAreTheRocksOwn` holds
them against the rock's public readers. The current's coral and the bodies' coral must be one object. An ocean
whose overturning is not 0 is refused, since the CPU's ocean passes 0 to the coral's chain rule.

Two departures from the plan above. The grid lists colonies, as on the CPU. The canopy map is not on the card,
since nothing on the card reads light.

The kernel in double on ILGPU's CPU accelerator gives the solver's bits after 100 steps, with bodies on a plate,
against a trunk, in the fade and in open water. That holds in the 60 m square and in a 1,600 m2 tank, with and
without per-part contact (`GpuCoralTests`). The card's own identity pair, at two group sizes, comes with the
round's screen.

## The card

The capsule, the plate, the union, the hull's fade and the canopy read go into the kernel template under the
card's rules (`src/Evosim.Farm.Gpu/CLAUDE.md`). Every product is written as `mul.rn`, there is no Atan2, and the
optimisation level and single precision stay as they are. Pieces are rows of Reals, as reefs are today (survey
section 3). The grid's lists are in compressed rows, and the canopy map is a table of three floats per column.

`GpuWorld` packs the reef rows by reading `ReefGeometry`'s private arrays through reflection, so a renamed field
breaks the card's build without a compiler error. The coral's arrays get a packing test of their own. `GpuWorld`
also requires the solver's and the current's reefs to be one object, and the coral keeps that rule. The transport's
fade is not read per step: `GridField` bakes it at every edge's midpoint once (`_edgeFadeX`, `_edgeFadeY`,
`_edgeFadeZ`) and hands the arrays to the card's transport. The hull's fade must feed that bake as well as the
links' streams, or the grid's water and the bodies' water would fade differently.

## The record and the theatre

- A new tunable, ReefShape (Mushroom or Coral), heads a coral group beside the reef group in `RunConfig`. Every
  older config, genome-world and checkpoint is refused, as for any new tunable. The recorded fixtures are recorded
  again (`src/Evosim.Core/CLAUDE.md`), and the two reflection tests guard the new fields.
- The mushroom code stays as it is. A new-format config with ReefShape Mushroom is written from round 61's
  config, and it must replay a span of round 61 bit for bit. That proves the coral did not move the mushrooms.
- The manifest records every colony's pieces, as it records the reefs now, and the canopy map's hash.
- The checkpoint format does not change. Reefs are not in the world state today. They are placed again from
  the seed and the config on every load, and the colonies, the static grid and the canopy map are rebuilt the
  same way.
- The theatre gets a mesher for tubes and plates, with inward-only noise bounded at 0.4 of a piece's radius.
  Joints need no fillet drawn, since the plain union lies inside the smooth one (survey section 5).
- The shafts' shade comes from the canopy map as a texture. It replaces the 32-disc array, which round 60's 53
  reefs already overflowed.
- `SnapshotWorld` rebuilds the colonies and the island map from the run's config. Level of detail is for films
  only, never for the physics.
- The scripts that read reefs as mushrooms (survey section 5) gain a coral branch. The round's reader reports the
  coral's own measures. It counts bodies within 2 m of coral and their share by guild. It compares the leaves
  under the canopy with those on open island.

## Order of work, and its cost in agent time

| Step | Work | Agent time | Machine |
|---|---|---|---|
| 0 | The two engine items above | 2-3 h | card, minutes |
| 1 | Colony growth, placement, caps, ReefShape and the tunables, in Core, with tests | half a day | CPU |
| 2 | Contact, the hull's fade, the canopy map, corpses and the static grid on the CPU, with grid-against-scan tests | half a day | CPU |
| 3 | The card's transcription, and the CPU-against-card identity tests | half a day to a day | card, an hour |
| 4 | The mushroom replay of a round 61 span on the new build | 1 h | card, 30 min |
| 5 | The theatre's mesher, shade texture and snapshot path, checked against a reference render | half a day | Unity, short |
| 6 | The readers | 2 h | none |
| 7 | The screen on one seed: cost against R0, the water's speed inside a colony, where bodies gather, and pictures against the last agreed look | 2-3 h | card, a few hours |

The pre-registration follows the screen, and the round launches from its commit.

## Questions for the owner before the build

1. Whether the grazers' dial search runs before the coral or beside it. Before is recommended, so the coral does
   not inherit an economy where grazers cannot make a living. Beside it, round 62 carries two changes.
2. Whether the giant-part rules are settled first. The coral would roof 20% of each island, where round 61's
   ribbons grew. With contact by a part's half-diagonal sphere, a 37 m ribbon would shove the coral's crowd as a
   37 m ball would. The candidates are a cap on part length or aspect, contact by the part's box or capsule, and
   larger contact caps for giants. Each changes results, so each would land with round 62.
3. The canopy's transmission and the fade's floor are D194's dials, started at 0.3 and 0.25. The screen shows
   their effect, and the owner sets them before the pre-registration.
