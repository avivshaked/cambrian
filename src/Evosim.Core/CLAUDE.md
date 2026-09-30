# The world's rules in Core: what will bite you

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before changing a world rule, a tunable, the economy, the fields, the current, the brain or development. What each past build changed is in `logbook/specs/build-readings.md`.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Gotchas

- **The world has no top, and three separate clamps hide it.** `LightModel.IrradianceAt` returns a
  constant for `heightY >= 0`, `NutrientField.LayerOf` puts everything at or above 0 in layer 0, and
  `FluidEnvironment` bounds y not at all. Each is reasonable alone; together they make the region
  above the waterline an unbounded ray on which every point is physically identical to floating at
  y = 0, while the physics keeps integrating. D049's first buoyancy probe climbed 155 m into it in a
  world whose habitable band is 23.7 m deep, paying upkeep the whole way for a position no different
  from the surface (logbook/0034). D050 stops *upward* net force at y = 0. Anything else that can
  push a creature up — an effector channel, a current — needs the same question asked of it. The
  floor has the same hole downward: the nutrient fields clamp to their last layer but nothing stops
  a sinking body, and round 5b's last survivors died at −131 m in a 60 m world (logbook/0040). Depth
  statistics from a dying world include water that does not exist.
- **The sea floor is a ratchet at mixing 0 only.** `NutrientField.Mix` runs across every interface
  including the floor's, so at any `NutrientMixingDiffusivity` above zero the floor already gives
  back — 20%/s of its excess at 0.2 m²/s. The 80–93% on-floor figures in DESIGN.md §5A.2c and the
  66–76% in the D050 arms are all mixing-0 worlds; at 0.2 the floor holds 5–7%. D051's
  remineralisation leak was built on the premise that nothing leaves the floor, and was measured
  redundant the same day (logbook/0036). Before building a mechanism on a code fact, read the loop
  bounds, not the method's shape — a reconnaissance pass quoted `Mix` and still missed this.
- **The population floor does two jobs, and closing it exposes both.** It runs the founding
  lottery — forty random genomes breed in about one seed in four, and the floor keeps drawing
  until one does — and it rescues every matter crash. `FloorClosesAfterSeconds` (`EVOSIM_FLOOR_CLOSES`)
  stops it; founding takes 2 spawns per 0.5 s step, so anything under 20 s leaves fewer than forty
  founders, and 3,000 s is after founding and before the first crash. With it closed, three seeds in
  five die at their first drought (logbook/0037). A world that needs the net is not self-sustaining.
- **Throughput is population, and the ceiling is an instrument.** Four to five thousand creatures
  run at 0.2–2× real time depending on how many arms share the machine; 30,000 simulated seconds
  took five to ten hours. `MaximumPopulation` (5,000, `EVOSIM_MAX_POP`) ends a run as a *runaway* —
  censored, not an outcome — and the light 0.02 world reaches it by t≈5,500 (logbook/0038).
- **Adding a tunable makes every older `config.json` unreadable by the new build** — §9's
  refuse-rather-than-default rule on a missing group. `ledger.ps1 -Config` against a run written
  before the tunable throws; take the genome from the old run and the config from a new one.
  The same rule now bites genome files: the snapshot id took `GenomeJson.FormatVersion` to 4, so
  every stored `format":3` genome under `inocula/` is refused by this build
  and by `ledger.ps1 -Genome`. The genome fields did not change across that bump — only the
  optional id was added — so an inoculum can be brought forward by re-extracting it from a new
  snapshot, which is the one route that cannot quietly mislabel a creature. The growth build
  took it to 5 on 2026-09-09 (D087), so a format-4 genome is refused the same way; the growth
  gotcha below has the details. **Three test fixtures are recordings and every bump orphans
  them** (found on round 44's build, 2026-09-22): `src/Evosim.Core.Tests/fixtures/r42-config.json`
  (the thread-identity word, `Slow`), `src/Evosim.Dynamics.Tests/RunFixture.cs`'s run directory
  (a snapshot crowd under `runs/`, ten contact, crowd and digest tests) and the joint-damper
  reproduction, which named two body ids of that crowd. Re-record each on the build that reads
  it: a config from a run of the build, a fresh `runs/<arm>` of round 43's world with the new
  tunables at zero (`r44fix-s4`), and a pair found in the scatter rather than named by id;
  the fixture's `Why` says which of "not on this machine" and "this build cannot read it"
  applies, and they need opposite responses. And `scripts/ledger.ps1` prompts for
  `-Clearance`, `-Depth` and `-Density` when they are missing: under a non-interactive shell
  the prompt never returns and the process sits with no child and no output (one lost
  quarter-hour that evening); pass all three.
- **A rule chosen for being cheap gets re-asked when a later change makes it bite, and the
  report probably already says so.** D077's periodic wrap cost nothing while no body ever
  reached a seam. D088's carrying current sent every body across one about once per hundred
  seconds, the table's `wraps` column counted it in every window of rounds 35 and 36, and
  nobody read the column as a problem until the owner watched a minute of the theatre and
  saw bodies teleport (2026-09-11; `fable-propose-aquarium.md`). Read `wraps` with `alive`,
  and when a world rule changes, list the rules that were chosen for cheapness under the old
  one and ask each whether it still holds.
- **A drag-only fluid is a centrifuge, and a walled world shows it.** A body pulled toward the
  water's velocity by drag alone drifts outward on every curved streamline by about
  `τ·u_θ²/r` per second (`τ` its response time, 0.4 to 1.3 s at the campaign's sizes), so any
  current with any turning in it piles bodies against a wall: round 37's tank put 58 to 96% of
  every seed in the rim quarter and the whole world in a crust at the glass at the peaks
  (D090, 2026-09-12; the owner called it a centrifuge before the agent did). The box never
  showed it because the seams wrapped. From D090 `EVOSIM_FLUID_ACCEL` (`FluidConfig.
  FluidAccelerationCoefficient`, default 0 so every recorded world replays; 1 from round 37b)
  adds the water's acceleration force, and with it a lagging body keeps an even spread (rim
  quarter 0.25 to 0.27 in `StreamsTests`) where it read 0.83 to 0.98 without. Read `fluidAccel`
  in the header, and read `p3` over `alive` in any walled world before anything else. In a
  tank the term takes the streams' closed-form derivative (`StreamsAccelerationAt`, 2.3
  velocity samples per call, `logbook/specs/streams-analytic-spec.md`); the nine-sample
  stencil (`MaterialDerivative`, about ten velocity samples) serves only the box's transport
  field, where no round has run the force. In a tank the current is D090's streams, selected by the
  shape; the header's `current` token still names the mode (`transport`), and the shape token
  is what says the streams are running.
- **Uniform water made its own patches until 2026-09-12.** A dissolved field carried by
  incompressible water stays uniform, and the grid's transporter did not keep it so: face
  velocities sampled at cell centres and three axis passes applied in sequence turned
  1 unit/m³ into 30% patchiness on 1 m cells within 600 s at the campaign's mixing (34% on
  the streams, 102% with mixing off; 5 to 6% on the 5 m matter cells) while the total held to
  1e-15, which is all the transport tests asked. Conservation, positivity and dry cells can
  all hold on a field that is wrong; a constant-field check belongs beside every conservation
  check. From round 37b's build the grid takes its face fluxes from the current's vector
  potential (`CurrentField.PotentialAt`; `logbook/specs/transport-conserves-spec.md`), so
  every cell's net flux is zero by telescoping; the rolls' scheme is untouched and every
  recorded world replays. A new current mode needs a potential, not only a velocity, before
  the grid will carry it faithfully, and the rolls have none: their scheme, rounds 32 and
  33's, fails the same test worse (113% on 1 m cells) and is left as recorded. Two readings
  come with the repair: the substep count now comes from the fluxes themselves (one substep
  in both campaign cases where the a-priori Courant check asked two, so the transport is
  cheaper per step), and a 5 m grid carries only 0.3 to 0.4 of the water's RMS because it
  samples the eddies about once per wavelength (the 1 m grid carries 0.96 to 1.07). The
  Astra review's probe that found it is `logbook/specs/transport-conserves-probe/Program.cs`.
- **A body's reserve is unbounded, and none of it ever feeds anyone.** Nothing caps
  `Organism.Energy`. Senescence (D038) is a multiplier on upkeep, `1 + age / 3,000 s`, so an
  old body burns its whole reserve as upkeep before it starves, and `World.Bury` hands the
  corpse the tissue alone (`Corpse(..., TissueJoules, LockedMatter)`) and books whatever
  reserve a diverged body still held as `EnergyOut`. Either way everything a body saved
  leaves the world as heat. In round 40's seed 1
  at 16,000 s the living had captured 764 kJ of light against 287 kJ spent, about 190 J of
  reserve a body against 0.37 J of tissue, and every one of those bodies dies at 3,000 s
  with that reserve. So the eaters' larder in rounds 38 to 40 was the tissue only, a
  fraction of a percent of what the leaves captured, which is part of why the eaters boom
  and starve (0101). The audit closes because the discard is booked as an outflow; the
  books say nothing about whether a rule is sensible. Found 2026-09-18 while sizing D098's
  loop. D098's build burns it as a charged unit returned spent to the water, so at least
  the plants get it back, sends a diverged body's reserve to the corpse, and adds
  `ReserveCapSeconds` (off in the base round) as the lever that moves a hoard into the
  larder while the body lives; a starved body still dies with nothing, because senescence
  burnt it first.
- **A terminal-only self-edge recurses to the depth cap, and a knot of parts is free light.**
  `Developer.Expand` asks a node's recursive limit only of an edge that is not terminal-only,
  and a terminal-only edge fires exactly when the limit is spent, so a `link` node with a
  terminal-only edge to itself and a limit of 1 grows to `MaxDepth` 8 (nine parts with the
  root) and a second root edge into it fills `MaxParts` 16. With a turn on the edge the chain
  folds into a ball in which every part overlaps every other; PhysX resolves each pair on
  every step and never separates them, so `pairs/body` climbs with the knots and `stuck %`
  reads 100. The knot pays because a body's light income and its shadow are both the sum of
  its parts' projected areas and a body never shades itself, and because D098 made a part
  cost the tissue price alone. Round 41b's knots were articulated and round 41c's rigid, so
  `pairs jnt %` reads high or low for the same cause; read `contactBodies` against
  `contactPairs` (twenty pairs per touching creature is a body touching itself) and run
  `scripts/overlap/run.ps1` on a snapshot (the project is `src/Evosim.Overlap`), which counts
  a snapshot's self-overlapping part pairs and reproduced the physics' pairs per body in
  every seed (logbook/0107's last section, 2026-09-19). Round 40 never showed it because its
  economy priced every part in matter. **From D099 (the same day) both rules are closed, and
  three things read differently.** `RunConfig.LightSilhouetteCap` (`EVOSIM_SILHOUETTE`,
  header `silhouette on`/`off`, off by default so every recorded config replays, on from
  round 41d) caps what a body earns on and shades with at its convex hull's surface over
  four, every part's share scaled by one factor; the field refuses every `config.json`
  written before it, rounds 41 to 41c included. `Developer.Expand` asks the recursive limit
  of every edge, so a stored genome with a terminal-only self-edge develops to two or three
  parts where it developed to nine or sixteen (the three knots under `inocula/`); a genome's
  body is a property of the build, as a config's world is. And the hull in Core
  (`Geometry/ConvexHull`) has a face ceiling: a degenerate cloud (random founders stack parts
  exactly on each other) falls back to the bounding box and `Phenotype.SilhouetteFellBackToBox`
  counts it, which nothing in a run report surfaces yet; the probe reports it per snapshot
  and read zero on every recorded body. The absorptive log's `TotalLitArea` column stays the
  uncapped sum.
- **A neuron's value is unbounded, and single precision has a ceiling.** The brain's guard
  catches a non-finite value and nothing bounds a finite one, so an integrating neuron runs
  for the body's life: round 45 seed 2's crowd carries one at the order of 1e29 (logbook/0115,
  found by the card's single-precision check, where it was 5e27 off the double). The drive is
  clamped and never sees it, but a float reaches infinity at 3.4e38 and the guard there, so
  the GPU port wants a bound or a count before a run is read on the card. And the card's step
  at the campaign's crowd is the slowest thread's latency and not throughput (both spike-3
  kernels flat from 6,145 to 30,000 bodies): a 75-neuron sixteen-link body sets the step for
  everyone, which is what size classes are for. **A raw-bit digest is not an identity claim across two
  compilations once a body is lost**: when both operands are NaN the machine keeps one
  operand's NaN and two compilations of the same source may keep different ones, so the
  whole-step kernel (0115) agreed with the solver in every living value and disagreed in the
  sign bit of NaNs inside lost bodies alone, and the raw digest differed at every step after
  the loss. The farm's own digest at 1 and N threads is one compilation and does not see
  this; a digest compared across the CPU and the card, or across two builds, hashes a lost
  body's NaNs as one pattern or leaves lost bodies out.
- **A mouth is priced at the cell it is emptying, not at the water round it.** A stomach
  draws `density × clearance × dt` from its own 1 m cell, and round 48's pool founders drew
  45 to 52% of it per half-second step (`scripts/reads/r48-entry/f4draw.py`). So
  `densityHere` in `absorptive.jsonl`, and the density a ledger break-even is stated at, are
  both the emptied cell's. In round 48's water that cell holds 5 to 25% of an untouched
  copy's after a minute: the stirring refills it at 0.06 to 0.1 of the gap a second, and the
  transport adds some only where the water moves (`FeederRefillExperiments`, logbook/0120).
  Three things follow. Set a break-even against a field's density and you overstate an
  eater's intake four- to twentyfold. A body's first reading cannot witness where it was
  placed. And past the refill rate, a bigger clearance buys little, since the steady intake
  is `c·k/(c + k)` of the water round it, with k the refill rate.
- **Two worlds built from one `RunConfig` share one current, and a current is pinned by its world.**
  `RunConfig.Current` is an object, not a value, and `DynamicsWorld.SampleWater` pins it at each step's
  instant. So two worlds stepping on two threads from one config throw `This current is pinned at phase
  …` the first time their clocks differ, which is what the nursery's first parallel smoke did
  (2026-09-29). A process that builds more than one world gives each its own config, read from the same
  text (`RunConfigJson.Read`), as `Evosim.Nursery`'s `EpisodeConfig` does. The farm never met it,
  since it runs one world a process.
- **An inoculant lands with `FounderEnergyJoules` times its birth fraction, which is not a floor
  founder's purse.** A floor founder takes the endowment (`EVOSIM_FOUNDER_ENDOWMENT`, 600 s of standing
  watts in round 52); `World.Inoculate` does not. So round 52's 200 J starved a two-part body
  inside 450 s in every nursery smoke (2026-09-29). An assay that needs its body to live sets the
  field, as the nursery's `--purse` does, and a round that inoculates reads the purse it gives.
