# Build readings: what each build refused and what reads differently

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read when reading a run recorded on an earlier build, or a column or field whose meaning may have changed. Each entry marks a build that refused every config, genome or checkpoint written before it, or changed what a column means; read the entries between the run's build and the present one.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## The root file's former current-state paragraph

Kept as it stood on 2026-09-30, when a pointer to HANDOFF.md replaced it. It describes the campaign around round 35.

Current state: **the ecosystem runs**, in one shared box of water (D077) stepped on one
thread so that it replays (D078). Genomes develop into phenotypes, articulations swim under
their own evolved brains, and `Evosim.Core`'s world charges upkeep, feeds, breeds and kills
with both books closed at every sample. The water is a grid of cells (D086, logbook/0078)
and bodies are born small and grow (D087, logbook/0081). The goal is a ladder of milestones since D094 (2026-09-16): rung 1, D063's
self-sustaining food chain, is met and its scorer prints a state and not a verdict; rung 2,
adaptation by degree, is a reading until its threshold is measured; every round is read on
its own pre-registered predictions. Before that, the goal rule (D063 as amended) was
last met five seeds of five in round 30 (logbook/0075); the grid's base round 32 read two of
five with every mechanism prediction holding (0079); round 33, the growth base, met it five of five
and was the first world in which a trait moved by degree rather than by switch (0082). Then
the theatre showed every clade as a metre-wide column (0083): a newborn beside its parent
and a current that returned bodies. D088 (2026-09-10) replaced both, a newborn dispersed
over a disc and a current that carries in three dimensions, and the agent's own pictures
(0086, `scripts/theatre-snap.ps1`) show the fixed world filling the box. Round 35 is the
base round in that world and read five of five (0087), the dials no longer walking one way;
which of the earlier readings survive it is 0084's list, and round 34, the free joint, runs
on it.
The open frontier: why the eaters' lines stop recruiting on the grid, whether a free joint
survives founding (round 34, logbook/0080), and movement, which has never paid its energy
cost (the cost side is closed, the prize side is open; `mean m/s` reads the water now).

## Gotchas

- **Every evolution run through round 28 swam with added mass off, and the knob defaults to
  off.** `FluidConfig.AddedMassCoefficient` had no initialiser and `EvolutionRun` never set it,
  so every `config.json` through round 28 reads `addedMassCoefficient: 0` and those worlds swam
  on drag alone. From the movement build (D081, 2026-09-07) `EVOSIM_ADDED_MASS` sets it, the
  header carries `addedMass` on every run, and the default stays 0 so that every launcher in
  the record still describes the world it ran. It is a per-step term: any nonzero value is a
  new realisation of every seed, and a config at 0 replays the record.
- **A stored genome may carry a global brain that nothing steps.** `Genome.GlobalBrain` was
  retired by D081 (2026-09-07): a child is born without one, a rewire never draws the
  `GlobalBrain` input kind, and `Brain.For` builds no partless group. Snapshots recorded before
  that build still load and validate, and three genomes in five in them carry one or two
  global neurons; a count of "neurons" taken from such a genome includes neurons the new build
  never evaluates. `Brain.NeuronCount` is the body's count.
- **A vertex world needs positions, and a Core-only one has almost none.** From D083
  (2026-09-07) `EVOSIM_FIELD vertices` replaces the two cell fields with `VertexField`, which
  feeds a body at its centre of mass and refuses a point that carries only a depth and a
  patch. The world therefore refuses the tiled mode (`SharedSpace` false), and in a Core-only
  test with no placer every body sits at its patch's centre, so every deposit merges into a
  handful of vertices (20 vertices holding 14,644 J in `VertexFieldTests`) and the field reads
  nothing like the farm's. Read a vertex field's shape from a run, never from a Core test. A
  deposit that lands within `FieldMergeMetres` of a vertex joins it and the vertex keeps its
  position; the report's `vtx` column prints `detritus/matter` counts against
  `FieldVertexCap`, and the per-depth columns (`det deep`, `mat here`, `refuge J`) are the
  same layer-and-patch bins as before, summed over vertices. Every config written before
  this build lacks the `field` group and is refused by it, per the tunable rule above. **The
  two fields read through different kernels**: detritus at `FieldKernelMetres` (1 m, 4.2 m³),
  matter at `FieldMatterKernelMetres` (1.8 m, 24.4 m³, the old cell's volume). The first
  seed-2 screen ran both at 1 m and bred once in 10,000 s, because a child costs 8 to 16
  units of matter and a 1 m kernel reaches 4 at the seeded density; a vertex world whose
  founders never breed should be read at `mat blk` against `mat here` before anything else.
  **`mat resid` is the matter identity and must read 0 like `audit`.** The energy audit does
  not see matter: the second seed-2 screen created 22,000 units of matter in 3,000 s with
  `audit` at 0.0000% on every row, because the vertex take delivered less than its gate
  promised and conception booked the price. From the build that fixed it (2026-09-07 late)
  the table prints the residual and `mat short`; on a run older than the column, read
  `matterStanding` in `stats.jsonl` against the seeded stock plus influx less burial. From
  D098 (2026-09-18) `mat resid` is `World.MatterResidual`, the spent units plus every
  standing joule over ρ against the seeded stock plus influx less burial, and `mat short`
  is gone; the two books are one equation in two units, and either can open alone when a
  leg does half of a transfer, which is what keeping both is for.
- **A grid world refuses three things a vertex launcher would pass, and prints a dash where
  it printed a count.** From the grid build (fable-propose-grid.md, 2026-09-08, logbook/0078)
  `EVOSIM_FIELD grid` puts both fields on cells (`GridField`; `EVOSIM_FIELD_CELL` 1 m,
  `EVOSIM_FIELD_MATTER_CELL`). A cell size must divide the box on all three axes or the
  world is refused at construction: the campaign's 100 m² over four patches at 60 m is a box
  20 × 5 × 60 m, so the matter default of 3 m refuses it and 2.5 m or 5 m runs (5 m is ruled,
  D086; 2.5 m strands a fifth of the matter in cells too small to afford a child, 0078). `EVOSIM_H_MIXING` must equal `EVOSIM_MIXING` or the world is refused,
  because a grid stirs at one rate on all six faces and the header's `h-mix` has to read what
  the detritus does; the matter grid stirs at its own `MatterMixingDiffusivity` on every axis,
  2 m²/s in the campaign, where the vertex world walked it sideways at 0.02. Explicit
  diffusion is refused above `D·dt/cell² = 1/6`, so 2 m²/s at a half-second step needs a
  matter cell over 2.45 m. The `vtx` column prints a dash on a grid. From round 37b's successor
  build the table's `det cv` and `mat cv` read how far each field is from well mixed, the
  standard deviation of the live cells' densities over their mean, and they print a dash on a
  vertex field the way `vtx` prints one on a grid. The two cell tunables
  make every earlier `config.json` unreadable by the build, rounds 30 and 31 included; their
  arithmetic replays on the vertex field under their own builds. Read a grid field's shape
  from a run, as for vertices; the Core experiments (`GridFieldExperiments`) are the
  sitter/mover, column and corpse-patch measurements and nothing else. **A corpse is an
  object only when `EVOSIM_CORPSE_DECAY` is above 0** (`CorpseDecayPerSecond`, default 0,
  at which a death deposits at once as it always did): above 0 a dead body's joules and
  matter sit in `World.Corpses`, counted as standing by both identities, and reach the
  fields at that rate per second; read the `corpses` column and the `corpseJoules` /
  `corpseMatter` stats fields, and remember `detritus J` no longer holds what a corpse
  still does.
- **From the growth build (2026-09-09, logbook/0081) a body is not a unit of biomass, and every
  stored genome is refused.** `BirthInvestment` replaced the endowment and `AdultScale` joined
  the genome, so `GenomeJson.FormatVersion` is 5 and every `format":4` inoculum and snapshot on
  disk is refused, as the format-4 bump did to format 3; re-extract from a new snapshot. A child
  is born at a fraction of its adult body (median 0.31 in the smoke) and grows, so
  `MaximumPopulation` still ends a runaway but counts bodies of any size, so D087 adds
  `MaximumTissueJoules` (`EVOSIM_MAX_TISSUE`, 0 = off; the header's `maxTissue=`), which ends a
  run as a runaway on the living bodies' standing tissue, and `run.json` names which ceiling
  fired. In a closed-matter world neither can: round 33's 6,000 units cannot build 8,000
  bodies (24,000 units for the per-creature term alone) or 30,000 J of tissue (15,000 units),
  so a runaway there is impossible by construction and both ceilings are idle instruments
  kept for a world with influx. Read `body frac`,
  `adult scale`, `invest` and `brood` in the table, `bf` and `as` on lineage birth rows, and
  `growthShortOfMatter` / `conceptionsUnderMassFloor` in `stats.jsonl`. At investment 0.5 a
  world breeds about seven times faster through founding than one whose children are born
  whole, and strips the matter at its layer within 300 s (0081's smoke against `r32-s1`).
  The harness resizes a living articulation in place every `GrowthStepSeconds`;
  `resizeJumpMetres` is exact and read 0, but `resizeStepMetres` reads the root only and
  cannot see a snap smaller than the current's drift (15 cm per step at 0.3 m/s), so a
  resize suspected of throwing a body is read from a per-link instrument that does not
  exist yet, and from `diverged`. **`conceptionsUnderMassFloor` counts attempts, not parents**:
  a refused parent keeps its reserve and draws again next step with a fresh mutant, so round
  33's two million per arm is thirty to forty parents standing at their threshold at any
  moment, and the floor is the sieve that shapes the litter (logbook/0082). **The placer
  reserves a newborn's spot at its birth radius** and the body grows up to twentyfold in
  place; `Body.Radius` is refreshed on resize for crowding, the reservation is not, and a
  contact divergence of three leaves in one step (`r33-s2`, 3,066.5 s) is read against it. `mat orphan` has read of the order of 1e-4 units on 6,000
  since before growth; the invariant is broken by a value the table does not round away.
- **From D088 (2026-09-10) the water carries, and three readings change with it.** `EVOSIM_CURRENT_MODE
  Transport` is a three-dimensional divergence-free field at RMS `EVOSIM_CURRENT`, equal on every
  axis, that moves bodies, corpses and the grid's cells; `Rolls` (the default, and every recorded
  config) is the returning current. Read the mode from the header (`current 0.3 m/s transport`),
  and read **`mean m/s` as the water**: a sitter in the transport field moves at about two thirds
  of the knob because the water does, so the column that was the locomotion readout is not one,
  and there is no relative-speed column yet. The grid substeps its advection when the fastest
  water (2.45 times the RMS) would cross more than half a cell in a metabolic step and refuses
  above eight substeps; 0.3 m/s on 1 m cells was two, and about a quarter of the throughput, until the conservative
  transporter (2026-09-12), whose substeps come from the largest per-cell outflow and read one.
  Dispersal is `EVOSIM_OFFSPRING_DISPERSAL` (`OffspringDispersalMetres`, 0 = D077's touching
  rule), **not `EVOSIM_DISPERSAL`**, which is D061's retired patch lottery and still bound; the
  header prints `dispersal=`. The placer reserves a child's adult radius from this build, so
  `crowded` and `stillb` mean something different from round 33's; founders still reserve
  their birth radius. Both tunables refuse every earlier `config.json`, rounds 32 and 33
  included.
- **From D089 (2026-09-12) the world can be a tank, and five things read differently.**
  `EVOSIM_SHAPE Tank` (`RunConfig.WorldShape`, default `Box`) makes the footprint a disc of
  `sqrt(area / π)` with a glass wall of 48 collider slabs, ring patches of equal area (`p0`
  the centre, `p3` the rim), a masked grid (6,000 live cells at 1 m and 100 m²) and a gyre
  in place of the periodic transport field; the header reads
  `space tank r=5.64 m (100 m2), depth 60, wall, bed` and a box's token is unchanged
  (`shared 4x5x5 m`; the box branch had changed it to `4x1x5 m` and the tank spec put it
  back). In a tank `wraps` reads 0 by construction and a nonzero is a bug; `x sd` is a plain
  deviation, not a circular one; `cols` is counted against the columns inside the circle;
  and a root farther than R + 1 m from the axis dies as a counted `Diverged` death with a
  dump whose reason names the radius guard. **The 5 m matter cell's mask overshoots the
  disc**: its live volume at 400 m² is 25,500 m³ against the nominal 24,000, so a held
  `MatterBudgetUnits` (`EVOSIM_MATTER_BUDGET`, 0 = the density rule) seeds 0.235 units/m³
  where the arithmetic says 0.25; the total is exact and the density is not, and the 1 m
  detritus mask is within a cell ring of the disc. The three tunables of D089 refuse every
  `config.json` written before them, rounds 35 and 36 included. **The drive limiter at
  every step exists and is off** (`EVOSIM_DRIVE_LIMIT_ALWAYS`, header `driveLimit >0.01`
  or `always`): its check, round 34 seed 5 rerun with it on, bound 2.58 million drives and
  read 27 divergences against 17 with the same signature (jointed adults, median age
  1,171 s, the root's height going non-finite), so the throw of a jointed adult is not a
  single step's over-drive (`r34lim-s5`, D089's check 4). **Round 37, the tank, threw
  nothing** across 9.17 million jointed body-seconds (logbook/0094) and 0094 read the wrap
  as the mechanism; **round 37b threw 43 across 11.1 million** (logbook/0097), so the wrap
  was not the whole of it. Every one of the 43 was a two-part jointed body dumped within
  seconds of its birth, with a joint mass ratio under 2.4, away from the wall and from any
  resize, and clustered by parent; the mass-ratio cap is excluded by the traces and not
  proposed. **The throw trace as built misses the onset**: its three-frame ring is written
  after the check that fires it, so in 42 of 43 dumps every frame is already non-finite;
  read a dump's trace for the masses and the ratio, not for the first bad step, until the
  ring keeps the last finite frames (it does from round 40's build: seed 2's one dump held
  three finite frames, logbook/0106's read). The one-part control (seed 5 on 37b's build with
  `fluidAccel 0`, `r37bc-s5`) is the test of the force.
- **From D098 (2026-09-18) there is one matter in two states, and six things read
  differently.** A unit is charged (ρ = `JoulesPerUnit` joules, in bodies, corpses and the
  marine snow the code still calls `Nutrients`) or spent (none, dissolved, the field the
  code still calls `Matter`); light charges spent units at the leaf, burning returns them
  spent, eating moves charged ones, and nothing draws a field at conception or growth
  (DESIGN §5A.2d, `logbook/specs/economy-spec.md`). First: every `config.json` and every
  genome (format 6, `ReserveMargin`) written before the build is refused, so the ledger, the
  theatre's Mode B and the three configs under `inocula/` all need a new-build run to read
  from. Second: the table's `mat blk`, `mat short`, `mat orphan` and `excreted` are gone
  and `upt lim` (the share of leaf-steps bound by uptake rather than light), `burnt`,
  `remin` and `margin s` are there; `analyse-arm.ps1` reads the new names and prints `?`
  on an old report's, as it always did. Third: `EVOSIM_REMIN` sets
  `RemineralisationPerSecond`, every cell's marine snow returning spent to the water, not
  D051's floor leak; `EVOSIM_EXCRETION` and `EVOSIM_MATTER_PER_*` are unbound. Fourth:
  `mat locked` is the living bodies' charged matter, and it is almost all reserve (round
  40 held about 190 J of reserve a body against 0.37 J of tissue), so it moves with the
  hoard and not with the crowd. Fifth: the ledger takes `--spent` (the spent density at the
  body) and its last column is `units/child`; a leaf in stripped water is uptake-limited
  in the ledger as in the world. Sixth: the fields' `TotalJoules` is units on the spent
  field and joules on the charged one, by convention only, as before the build; read the
  unit off the field, never off the method's name. Seventh, and the one that cost the
  night: **a closed one-substance world's count is the capacity over the holding.** D065's
  fixed charge was the head-count cap and it is gone by design, so the first smoke at
  11,000 units built 6,300 bodies in 1,100 s. A per-body standing cost does not bound the
  count (it sets where the water settles), a tissue value dear enough to bound it freezes
  the founding (100,000 J/m³ and above: no births by 2,500 s), and what does bound it is
  the budget over what a breeder must hold, whose floor is `PerOffspringOverheadJoules`
  (`EVOSIM_OVERHEAD`, 100 J from round 41). Screen a budget or an overhead on a dt 0.02
  seed for its plateau before pre-registering on it (logbook/0107). And **a queue launches
  the launcher's defaults**: `launch-queue.ps1` passes a seed, a worker and the hash and
  nothing else, so a dial screened on the command line has to be written into the
  launcher before the queue starts; round 41's first three seeds ran a minute on 11,000
  units and 25 J because it was not (`runs/r41mis-s*`, stopped and renamed).
- **From D100 and D101 (2026-09-19) the water is held and a folded body is not born, and
  both tunables refuse every earlier config.** `FluidConfig.WaterHoldSeconds`
  (`EVOSIM_WATER_HOLD`, header `water held 0.5 s` or `water per link`, default 0) samples
  the streams once a body at its root and holds the velocity and acceleration for that
  long; 0 is the recorded per-link sampling. `RunConfig.SelfOverlapDepthFraction`
  (`EVOSIM_SELF_OVERLAP`, header `selfOverlap 0.1` or `off`, default 0) makes a body with
  two non-adjacent parts overlapping deeper than that fraction of the smaller part's
  thinnest half-extent a stillbirth at `World.Admit`, founders and inoculants included,
  counted in `stillbirths` and `selfOverlapStillbirths` (the table's `self stillb`, appended
  at the end); the physics of self-collision stays on. Both are per-step or per-birth
  changes and a new realisation of every seed; rounds 41 through 41d's configs are refused
  by the build. The cap did not stop the bush: round 41d grew a one-node three-self-edge
  bush of sixteen parts that earns 1.7 times a leaf's light on the same matter with the cap
  binding (logbook/0108's last section, `inocula/bush-16-r41d-s2-15000.json`), because a
  spread body has more hull than a packed one, which is honest geometry; the fold's cost was
  the overlap, and D101 refuses that. The profile's instruments came in with them:
  `wallHarness<Phase>Ms` for ten phases and `wallFluid{Gather,Water,Compute,Apply}Ms` with
  `harnessBodySteps` and `fluidLinkSteps` on every row, the footer's `harness split`,
  `fluid split`, `harness per body-step` and `fluid per link-step` lines
  (`logbook/specs/harness-profile-spec.md`). At round 41d's crowd the wall was PhysX 25%,
  the grid 11%, the harness 64%, of which the drag pass 57% and the throw trace 20%; the
  next cheapening is reading the solver once a step and sharing it between the drag pass,
  the trace and the sensors, which keeps identity.
- **From D109 (2026-09-22 night) the matter starts as islands, the matter grid stirs at a
  launcher's rate, and every earlier config is refused again.** Six tunables
  (`MatterIslandWavelengthMetres`, `MatterIslandCover`, `MatterIslandDepthMetres`,
  `FoundersFollowMatter`, `LightShadeDepth`, `LightShadeDriftMetresPerHour`; header
  `matter islands 60 m cover 0.1 to 12 m · founders in matter · shade off`, or `matter
  uniform · founders anywhere`) refuse every `config.json` written before them, rounds 41
  through 45's void launch included; the fixtures are `pfix3` → `fixtures/r42-config.json`
  and `runs/r45fixc-s4`, and the Farm tests' round 42 hash is `c862fd2c510b82e9`. Six
  things bite. **`MatterMixingDiffusivity` was a hard default of 2 m²/s that no launcher
  named**, a hundred times the snow's; `EVOSIM_MATTER_MIXING` binds it and the header
  prints `matter-mix`, and a launcher that does not set it still gets 2. **The islands do
  not spread at the dial's rate**: at 0.02 m²/s explicit they spread at about 0.1 m²/s
  by the gyre's upwind transport on the 5 m cells (`bigF`, a Gaussian fit of the peak's
  decay), so a 60 m island is soup near 5,000 s whatever the dial says, and only a finer
  cell or another scheme would slow it. **At the slow rate a leaf's income is the flux into
  its cell**, `D × ρ`, not the tank's stock: a crowd strips its 5 m cells faster than the
  neighbours refill them, `upt lim` reads that pressure, and the same budget that founded
  round 44 at 2 m²/s starves after founding at 0.02 (`bigH`); read `upt lim` with the
  stirring in mind, and read any pre-D109 `upt lim` as the stock's. **The founder rule is
  a new realisation of every seed** when on (a refusal draws the placer's stream again) and
  bit for bit the recorded world when off. **The shade map saturates at 1 across every
  island column** and shades the deserts only (the first cut shaded by the raw map, and an
  island column at 0.72 to 0.8 of the light founded at a third of round 44's pace, `bigH`
  against `bigI`); it is off in round 45 and every launcher, by the owner's objection that
  light has no concentration. **The farm dumps the fields beside every snapshot**
  (`fields/NNNNNNNNN.matter.f32` every cell, `.snow-columns.f32` the column sums,
  `layout.json` the shapes; little-endian floats in the grid's own index order), a
  recording setting that moves no hash, and `scripts/field-map.py <arm> --runs-root … --at
  … --layers 3 --snow` draws the map with the bodies on it and prints the share of bodies
  standing in above-mean columns and, with `--footprint 100`, in the columns that were
  islands when seeded, which is 0114's J8 (the first share is a coin toss once the field
  is soup; the second still reads). **A reader's picture is checked against its numbers
  before it is reported**: until 2026-09-23 the snow panel closed over the matter grid's
  mask and cell and drew the snow field's south-west 34 m corner stretched over the tank,
  a hard-edged wedge at the north-east glass that round 45's first looks reported to the
  owner as a pile of snow with a gyre to explain it; the column sums, one query, put the
  densest columns under the crowd and 1% of the snow in the wedge. A straight edge on a
  field is an index, not a fluid. The Unity farm binds none of the six,
  so a world built there is uniform and its header carries no `matter` token. And **a farm
  smoke holds its exe**: `dotnet build` fails to copy over a running `Evosim.Farm.exe`, so a
  build for the next screen goes to another output (`-o artifacts/Evosim.Farm/bin/Release-b`
  and `run-farm.ps1 -Exe`) while a screen runs; the manifest's `farmHash` is of the source
  and does not care which.
- **Genome format 8 (D111, 2026-09-23) refuses every format-7 file, and the offset is a
  torque the price switches on.** `buoyancyOffset` sits after `toughness` on every node;
  the six inocula under `inocula/` were rewritten at 0 by a text edit that changed no other
  byte (`logbook/specs/format8-conversion/`), and any snapshot row of rounds 44 and 45 is refused by
  this build's reader (the theatre's picture reader still draws them, marked `OLD-RUN READ`).
  With `BuoyancyOffsetWattsPerCubicMetre` at 0 the field is refused above 0, the mutator
  draws nothing for it (children byte-identical), and the solver never enters the torque
  branch, so a price of 0 is the recorded world; above 0 the torque is the full displaced
  weight at the offset point and a new realisation of every seed. The leaf rights itself but
  does not settle: it first reaches flat in about 1.7 s and then rocks about it with a
  four-second period, drag being a weak damper at small angles, so a test of the pose
  asserts "first reaches flat and the swing shrinks", never the angle at a fixed second.
- **From round 46's build (2026-09-23) seven tunables and a genome field land together, and
  every config and checkpoint written before them is refused.** The beach
  (`RunConfig.BedShoreDepthMetres` and `BedShoreFadeMetres`, `EVOSIM_BED_SHORE` and
  `EVOSIM_BED_SHORE_FADE`, header `shore 1 m fade 15 m` after the tilt; refused without a
  fade, at or past the depth, or in a box; `logbook/specs/beach-spec.md`), the support cost
  (`EVOSIM_SUPPORT`, header `support 0.1 W/m2/m2`), contact per part
  (`EVOSIM_CONTACT_PER_PART`, header `contact per part` before the hash), the trickle
  (`EVOSIM_TRICKLE` as a number or `1/N`, header `trickle 1/30 s` after the floor token),
  founders in their food (`EVOSIM_FOUNDERS_FOLLOW_FOOD`, refused beside
  `EVOSIM_FOUNDERS_FOLLOW_MATTER`, which a launcher that inherits round 45's block has to
  set to 0), the offset price (`EVOSIM_BUOYANCY_OFFSET_COST`, genome format 8) and light by
  exposure (`EVOSIM_LIGHT_EXPOSURE`). `WorldState.StateVersion` is 9 and
  `Checkpoint.Version` 4, so ckA/ckB/ckC, `ckUi` and every checkpoint of rounds 44 and 45
  are refused; the fixtures are `fixtures/r42-config.json` from `pfix8` (round 42's hash
  `5062a25baa35c6e1`) and the crowd `runs/r46fixc-s4`. Every rule at its default replays
  the crowd fixture's world in 145 fields at 300 samples with the positions byte-equal
  (`r46allreg-s4`), and the lineage differs in one field: every founder row carries `src`
  (`floor` or `trickle`) from this build, so a byte comparison of lineages across the
  build is off by that field alone. Three things about the rules bite. **The beach's
  current is a product, not a clamp**: the streams fade by a quintic in the distance to the
  shore times a turnover in the depth (`CurrentField.ShoreFade`), because a hard `min(1,
  d/D)` left a 0.047 m/s velocity shear sheet where the fade met the full depth, so a shore
  on at any tilt is a new realisation of every seed (deep columns scaled by 1.07 at tilt
  30) and a shore at 0 is bit-identical; the 5 m matter cells over the shoal are dead and a
  body there reads the nearest live column inward, up to 11 m. **A world under the trickle
  never ends `extinct`**: `Program.EndsExtinct` takes the ending only when nobody is alive
  and no founders are arriving, so an emptied world refounds at the trickle's pace and
  reads as a crash with a gap in every inherited column; the manifest's ending says which.
  **Contact per part is dearer**: 2.8 times as many spheres in the grid, the contact phase
  1.9 times the recorded one on one thread and the body phase 19% more at 8 threads
  (`per-part-contact-spec.md` §6); a checkpoint rebuilds the link spheres on restore, and
  `--verify-checkpoint` with the switch on is owed before a resume under it is trusted.
- **From round 48's build (2026-09-24, D119 to D122) eight tunables and a genome field land
  together, and every config, genome and checkpoint written before them is refused.** The
  bud (`Mutator.ChangeCellType` is gone; a bud is a welded copy of a node at the born-small
  size with another type, drawn at `CellTypeChance`; `EVOSIM_RIGID_FLOORS`, header `floors
  rigid groups` after the reach, the birth row's `bud` and `budx`), gestation
  (`ReproductionTraits.Mode` and `GestationShare`, genome format 9; `EVOSIM_GESTATION_MODE_CHANCE`
  and `_SHARE_CHANCE` at 0 draw nothing, `_SHARE_MIN`/`_MAX` the founders' range; every
  birth row carries `gm` and `gs`; stats rows `gestationBirths`, `gestatedJoules`,
  `gestationJoulesHeld`), the scaled overhead (`EVOSIM_OVERHEAD_PER_TISSUE`; `EVOSIM_OVERHEAD`
  is the floor and `EVOSIM_OVERHEAD_FLOOR` its second name, the two refused at different
  values; header `overhead scale x2 floor 10 J · gestation …` before the hash), senescence
  on upkeep alone (`EVOSIM_SENESCENCE_WEARS_INTAKE`, on when unset; header `senescence
  3000 s on upkeep` or `on upkeep and intake`), founders at their food's depth
  (`EVOSIM_FOUNDERS_FOLLOW_FOOD_DEPTH`, header `founders in their food at its depth`) and
  the endowment (`EVOSIM_FOUNDER_ENDOWMENT` seconds of standing watts, header `endowment
  600 s`; the founder row's `endow`). `WorldState.StateVersion` is 10 and the checkpoint's
  queued lineage rows carry the endowment and the bud fields (version 10 is the first to
  carry any of them, so nothing older is byte-compatible anyway). The fixtures are
  `fixtures/r42-config.json` from `pfix11` (round 42's hash `53f8234cb554f0ba`) and the crowd
  `runs/r48fix-s4`; the ten inocula were taken to format 9 by a text edit that added
  `"mode":"Lump","gestation":0.5` and no other byte (`scripts/convert-format8-to-9.py`).
  **The crowd regress no longer reads IDENTICAL, and that is the bud's design**: `r48fix-s4`
  against `r47fixd-s4` is identical for 140 samples and parts at 1,410 s, where the build
  refused a bud-carrying mutant under the per-part mass floor that the old build admitted
  with its type changed; the type change has no off, so a world recorded before D119
  replays on this build only until its first cell-type draw. Read a regress across the
  build as identical up to that sample, and take the fixture's identity from the
  rules that have an off. Six things bite. **The newborn mass floor was applied per part and refused every born-small
  part** (`MinNewbornPartKilograms` 0.5 kg is 5e-4 m³ a part; a 3 cm bud or duplicate is
  2e-4 m³), so in rounds 41 to 47 no birth carrying a developed 3 cm part was ever admitted
  and a whole plant at investment 0.02 was refused unless its adult was 0.031 m³ or more:
  every "unexpressed gene" read from a snapshot in those rounds is read with this. With the
  rigid-group floors on, a welded part is exempt from `MinPartVolume` and the newborn floor
  weighs a rigid group as one. **The endowment is an influx**: a closed world's `mat in`
  reads the founders' endowments over ρ (90.9 units for fifty floor founders in the smoke),
  and `matterStanding` is the seed plus it; both books close by it. **The overhead's code
  default is 25 J**, not the 100 J every launcher from round 41 set; a config carries its
  value, so nothing recorded moves, but a world built from defaults prices a child at 25.
  **The mutation rates for gestation default to 0**, so a launcher that does not set
  `EVOSIM_GESTATION_MODE_CHANCE` runs a world in which every body is a lump breeder and the
  header reads `gestation off`. And **the ledger is a lump reading**: `LedgerForecast` prices
  the new overhead and knows the mode, but a gestating body's account is not modelled, so
  an R0 from `ledger.ps1` is the lump lineage's. The Unity farm binds none of the eight, so
  a world built there has them at their defaults and its header carries none of the tokens.
