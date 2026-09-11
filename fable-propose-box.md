# A squarer box

*A proposal to the owner, 2026-09-11. Absorb into `DECISIONS.md` on ruling and delete.*

I propose one new world rule. Let the four patches be laid out two by two instead of four in a
row. The campaign's box then becomes 10 by 10 by 60 m rather than 20 by 5 by 60 m. The area
stays 100 m², the depth stays 60 m, the patch count stays four, and a patch stays 5 m on a side.
The change is one tunable, `PatchesAcross`, defaulting to 1 so that every config on file still
describes the box it ran in. Round 37 of the ruled path would be the first world in the new
shape. Below: why the shape is wrong now, the rule, what the code has to do, the arithmetic, the
risks, and what I am asking the owner to rule on.

## The problem

Nothing ever chose a box 20 m long and 5 m wide. It fell out of one line of arithmetic written
when horizontal position did not exist. The shared volume's length is the patch width times the
patch count (`unity/Assets/Evosim/Sim/SharedVolume.cs:164`), and the box's z extent is one patch
(`:157`). D077 described the world as K square patches laid side by side on a ring, and a body's
patch is read from its x alone (`SharedVolume.cs:189`, and again at
`src/Evosim.Core/Ecosystem/World.cs:1480`). That was a reasonable way to give a patch index a
place when a patch index was the only horizontal thing the ecology read. It is now the shape of
the water.

Three instruments have grown that the shape distorts. The dispersal disc D088 gave a newborn is
5 m in radius and the box is 5 m wide. A child drawn towards the far wall wraps round the z ring
and lands behind its parent, so z is a lottery rather than a distance. That disc is the only
thing in the world setting a family's scale. The transport current is built from whole numbers
of wavelengths along each ring (`CurrentField.cs:725`), with a vertical mode number derived to
make the three axes carry equal mean squares (`:767`). A 5 m width therefore forces a 5 m eddy,
and D088 recorded vertical wavelengths down to 4.4 m as the price. The two-axis readings D088
added are measured over a footprint four times longer than it is wide. A population spread over
10 m of z has no room left, and one spread over 10 m of x has 10 m more. A square footprint
measures the same quantity on both axes.

## The rule proposed

1. **A layout** rather than a new geometry. `RunConfig.PatchesAcross`
   (`EVOSIM_PATCHES_ACROSS`) is the number of patches across z, and patches along x is
   `HorizontalPatches / PatchesAcross`. The box is `W · K / A` long, `W · A` wide and
   `WorldDepthMetres` deep, with `W = sqrt(area / K)` as before. A world whose `PatchesAcross`
   does not divide `HorizontalPatches` is refused at construction, in the shape `GridField`
   already refuses an indivisible cell (`src/Evosim.Core/Environment/GridField.cs:199`).

2. **The default is 1**, and at 1 nothing moves. The length is then `W · K` and the width is `W`,
   which is the arithmetic on file today, and the patch index below reduces to the current one
   term for term. No draw is taken and no expression changes, so a config at 1 is the same world
   under the same seed.

3. **A patch is still a place.** The index becomes `iz · (K / A) + ix`, with
   `ix = floor(x / W) mod (K / A)` and `iz = floor(z / W) mod A`. At `A = 1`, `iz` is 0 and the
   patch is `ix`, so every recorded run's per-patch bins keep their identity. At `A = 2` the four
   patches are quadrants, numbered along x first.

4. **The header names the layout**, as `space shared 2x2x5 m, depth 60, wrap, bed`. The first two
   numbers are the patches along x and across z, and the third is the patch side. The current
   token prints the patch count and the width twice
   (`unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs:801`), so at `A = 1` this reads
   `shared 4x1x5 m` where it used to read `shared 4x5x5 m`. I would rather change that token than
   keep one that cannot say which box it ran.

5. **The cell field and the patch lottery** are refused above 1. `NutrientField` mixes and
   advects across a one-dimensional ring of patches (`NutrientField.cs:706` and `:838`), and
   D061's dispersal lottery walks the same ring (`World.cs:2135`). Neither is the geometry of a
   two-by-two layout. A world with `FieldModel Cells` or `DispersalChancePerStep` above 0 and
   `PatchesAcross` above 1 is refused rather than run on a ring that is no longer a ring. The
   grid field mixes cell to cell and needs nothing.

6. **Round 37 runs four patches across two**, with everything else held: the same area, depth,
   cells, dose, dispersal radius, current speed and seeds as round 36.

## What changes in code

The smallest change that keeps every instrument's meaning is to give the three fields a length
and a width. Everything then reads those instead of deriving them, and the derivation lives in
one place. `GridField` and `VertexField` already expose both (`GridField.cs:132`, `:135`;
`VertexField.cs:130`, `:133`). `NutrientField` does not, and `World` recomputes the length by
hand in three places.

| File | Change |
|---|---|
| `src/Evosim.Core/RunConfig.cs` | `PatchesAcross` beside `HorizontalPatches` in the `patches` group (`:1348`), default 1, float-as-count per the standing convention |
| `src/Evosim.Core/Environment/IMatterField.cs` | `LengthMetres` and `WidthMetres` join the interface (`:98`), so no caller derives a box from a width |
| `.../NutrientField.cs` | Implement both; refuse a construction with `A > 1` (`:155`) |
| `.../GridField.cs` | Width becomes `W · A` and length `W · K / A` (`:190`); `PatchOf` takes x and z (`:258`); `_patchOfColumn` is indexed by cell column rather than by x alone (`:236`, six call sites) |
| `.../VertexField.cs` | The same two lines (`:190`); the kernel's two-radii check already reads both extents (`:198`) |
| `.../CurrentField.cs` | `SetBox` takes the two patch counts (`:446`); `LengthMetres` becomes `W · K / A` and a `WidthMetres` joins it (`:476`); `BuildTransport` reads the new width (`:933`). `VerticalWaves` is untouched, since it already derives q from whatever box it is given |
| `src/Evosim.Core/Ecosystem/World.cs` | The three hand-derived lengths read the field instead (`:1259`, `:1291`, `:1389`); `PatchOfX` becomes `PatchOfXZ` (`:1480`); the `SetBox` call site passes the layout (`:855`); the vent's width reads the field (`:2252`); the constructor validates clauses 1 and 5 |
| `unity/Assets/Evosim/Sim/SharedVolume.cs` | Takes `patchesAcross` (`:98`); `LengthMetres` and a new `WidthMetres` (`:164`); `PatchOf` takes x and z (`:189`, and the three call sites at `:216`, `:237`, `:482`); the founder draw spans the new width (`:521`) |
| `.../Sim/Ecosystem.cs` | Passes the knob to the placer (`:504`); the footprint reads the placer's width rather than its patch width (`:751`) |
| `.../Sim/Editor/EvolutionRun.cs` | Reads `EVOSIM_PATCHES_ACROSS` (`:304`) and prints clause 4's token (`:801`) |
| `unity/Assets/Theatre/WaterBounds.cs`, `SnapshotCamera.cs` | Both derive the drawn box from the patch count and one width (`WaterBounds.cs:114`, `SnapshotCamera.cs:457`), and both must read the layout, or the pictures are of a box the run was not in |
| `scripts/positions-read.py` | The `Box` class derives length and width the same way (`:107`); adding the layout keeps its promise that nothing in it knows the campaign's numbers (`:120`) |

`LightField` is the one place the layout cannot reach, and it needs no change. Its only geometry
is `WorldArea / PatchCount` (`LightField.cs:98`) and its per-patch shading is solved per index
(`:290`), so a patch's light does not know where the patch is.

## What stays

The area stays 100 m², so the sun's aperture, every density's denominator and the initial stock
are untouched. The depth stays 60 m and the light layers with it. The patch count stays four, so
the report still writes four per-patch columns (`EvolutionRun.cs:3013`). The per-patch bins, the
burial loop (`World.cs:1544`) and the shading solve are unchanged in shape, and the vent still
sits in one patch, now a quadrant. A patch is still 5 m on a side and still holds 25 m² of
aperture. The wrap is still periodic on both horizontal axes and seam contacts are still unseen,
as D077 says.

## The arithmetic

**The cells divide** on all three axes. The detritus cell is 1 m and divides 10, 10 and 60. The
matter cell is 5 m and divides 10 twice, 10 twice and 60 twelve times, so the grid's constructor
accepts it (`GridField.cs:199`). The cell counts do not move. Both boxes hold 6,000 detritus
cells (20·5·60 and 10·10·60) and 48 matter cells (4·1·12 and 2·2·12), so the grid costs what it
costs today. A 2.5 m matter cell also divides the new box, at 4 by 4 by 24, and D086 ruled it
out on ecology rather than on geometry.

**The eddies get rounder** and no more expensive. I reproduced the current field's derivation
(`CurrentField.cs:767`, `:933`) for the campaign's box and got vertical mode numbers 6, 24, 25,
27 and 12, which is D088's published list. The same five wavevectors in 10 by 10 by 60 give 12,
12, 17, 27 and 24. Their vertical wavelengths are 10, 10, 7.1, 4.4 and 5.0 m, against today's 20,
5, 4.8, 4.4 and 10. The finest vertical structure does not change, so the 1 m detritus grid
resolves the field no worse than it does now. The gain is horizontal. The longest wave across
the width goes from 5 m to 10 m, and the two horizontal axes carry the same wavelengths as each
other for the first time.

**The Courant check does not move.** The analytic ceiling that bounds it is 4.64 times the
root-mean-square speed in today's box and 4.71 in the new one. That is a rise of one and a half
per cent. At round 36's current of 0.1 m/s on 1 m cells the advection runs in one substep in
either box; at 0.3 m/s it runs in two in either box. The eight-substep refusal is nowhere near.

**The dispersal disc fits** the new width. A 5 m radius in a 5 m width reaches the far seam two
and a half times over, so a child's z is drawn from the whole width whatever its parent's z was.
In a 10 m width the disc reaches the far seam once and no further. A child can then no longer be
placed on the other side of its parent by wrapping past it. The disc is a quarter of the length
in today's box and half of it in the new one, so a family's scale relative to the world roughly
doubles on x and halves on z. I don't know which of those the ecology cares about, and round 37
against round 36 is how we would find out.

## The risks

**It is a per-step change**, so every seed is a new realisation. The water is a different field
and the grid's cells sit in different places, so PhysX will part from the recorded trajectory
within the first few hundred steps. Round 37 cannot be read as an A/B against round 36 on one
seed. It has to be read across five seeds, against the wingspan we already have for a per-step
change, the way every world change since D078 has been read.

**Adding the tunable** refuses every earlier config. The loader walks the schema and takes
`root[group][key]` for every entry (`Serialization/RunConfigJson.cs:137`), so a missing key
throws and rounds 32 through 36 stop loading under the new build. I looked for a way round it
and could not find one I would defend. Deriving the layout from the patch count instead, so that
a square K lays itself out square, would silently rewrite rounds 32 and 33 into a shape they
never ran. A reader that defaulted the missing key would break §9's refuse-rather-than-default
rule. That rule is what keeps a stored config from describing a run it did not produce. So the
cost is the one D086 and D088 each already paid. The old configs replay under their own builds,
and `ledger.ps1` against one of them needs a config from a new run.

**The header token changes** at the default. Clause 4 makes an unchanged world print
`shared 4x1x5 m` where every report on file reads `shared 4x5x5 m`, so anything grepping the
old token stops matching. I know of no script that does.

**The patch index changes meaning** at two by two and not at one. A reader comparing round 37's
`p0` against round 36's `p0` is comparing a quadrant against a strip. The entry has to say so.
The per-patch columns keep their count of four either way, so nothing in the table's shape warns
a reader, and that is the one confound I cannot instrument away.

## Tests to add

Everything below is in `src/Evosim.Core.Tests` and runs in seconds, except the last row.
`RunConfigTests` and `RunConfigJsonTests` are reflection-driven and fail on their own if the new
knob misses the hash or the round trip, so there is nothing to write for them.

| File | What it checks |
|---|---|
| `PatchyWorldTests.cs` | The patch index at `A = 2` for the four quadrant centres and the four seam corners; the index at `A = 1` matching `floor(x / W) mod K` term for term; a refusal each for clauses 1 and 5 |
| `GridFieldTests.cs` | A 10 by 10 by 60 box built at 1 m and at 5 m, the same box refused at 3 m, cell counts of 6,000 and 48, and the patch-of-cell map covering each quadrant equally |
| `CurrentTransportTests.cs` | In the new box: divergence-free, periodic on both rings, zero vertical velocity at the surface and the bed, root-mean-square equal to the knob, and vertical mode numbers 12, 12, 17, 27, 24 |
| `OpenMatterBudgetTests.cs` | The matter identity closing over a two-by-two world, since the influx and burial boxes are derived from the length (`World.cs:1291`, `:1544`) |
| `SharedSpaceSmoke.cs` (Sim, not Core) | A short world at `A = 2`: no body outside the box, wraps on both axes, the footprint reading 100 columns. The placer needs `UnityEngine`, so Core cannot reach it, which is the limit D088's dispersal branch also ran into |

## The build

About ten hours of agent work, and a day of wall clock with the suite and a smoke in it. Two
hours for the field geometry and the interface, one for the grid's patch-of-cell map, half an
hour for the current, one for the placer. Then one and a half for the harness, the theatre and
the python reader, and two for the tests above. Two more for the smoke, the identity check and a
theatre picture of the new box before anything is launched. The `-All` suite runs before the
commit, as `CLAUDE.md` requires.

## What I am asking the owner to rule on

*Standing answers, 2026-09-11: the owner folded the box into the ruled path as round 37 and said
"proceed autonomously", so the build goes ahead on the agent's recommendations below (the shape
as proposed, the area held at 100 m², four patches, the dispersal radius held at 5 m, the header
token changed, round 37 the slot) and lands between rounds 36 and 37. Any of the six can still be
overruled before round 37 launches; a different answer to 2 or 3 is a different world and would
be pre-registered as such.*


1. **The shape itself** — four patches two by two, 10 by 10 by 60 m at 100 m². This is knob 2 on
   logbook/0084's list, and D088 already named the box's shape a first cut rather than a ruling.

2. **Whether the area should move with it.** I have held it at 100 m², so that the light budget,
   the stock and every density stay comparable with rounds 32 to 36. A larger square box would
   be a better home for a 5 m disc and would break that comparison.

3. **Whether the patch count should move too.** Four patches two by two is the smallest square
   layout. Nine at three by three, or sixteen at four by four, would give the shading and the
   per-patch bins a finer grain at the same area, at 3.3 m and 2.5 m a patch.

4. **Whether the dispersal radius should be re-read** in the new box. At 5 m it spans the whole
   width today and half of it in the new box. Holding it fixed makes round 37 a test of the shape
   alone, and it is also the knob most obviously coupled to the shape.

5. **The header token changing at the default**, per the third risk above.

6. **Whether round 37 is the right slot**, or whether the shape should wait until round 36 has
   been read, so that the two changes stay apart.
