# Build spec: perception for movement that pays (D075, item 1)

Fable-written spec for an implementing agent. Read `logbook/specs/perception-survey.md` first (the
read-only survey this is built on; line references there are current as of commit 9950bc8).
Read CLAUDE.md's conventions: no `UnityEngine` in Core, every tunable reaches `RunConfig.Hash()`
and survives save/reload (two reflection tests enforce it), a genome file is one JSON line,
PhysX replays bit for bit so *the default configuration must replay the historical record to
the byte*. Nothing is written outside the repository. Do not launch arms; the round that uses
this is pre-registered separately.

## Why

D075 rules the path: after the open matter budget confirms, the next scored question is
whether movement can pay its energy cost. Movement has never paid here (logbook/0018 and every
round since) and the design's own diagnosis is that an open-loop swimmer cannot tell where the
food is: three of the channels §4.4 names for exactly that purpose (`Chemical`, `Energy`,
`Flow`) read a constant zero, and mutation is forbidden from drawing them. This build wires
them, gates the drawing behind config so nothing changes until a round turns it on, and adds
the instrument the movement round reads.

## Scope

1. `CreatureSensors.Read` answers `Chemical`, `Energy`, `Flow` with real quantities.
2. A `RunConfig` pool decides which channels founders and mutation may draw. Default = today's
   four. Bit-identical replay of the historical record at the default is a hard requirement.
3. `SensorChannels.Implemented` becomes the seven channels the simulator answers; the Milestone 1
   smoke test's `CheckSensors` is extended so all seven are measured, not promised.
4. Instrument: per-sample motility and "water quality experienced" by jointed vs rigid bodies.
5. Recommended, separable: the §4.4 requirement mask, so a channel costs nothing until read.

Out of scope: `Contact`, `Damage`, `Photo` (predation and Milestone 6); any world rule; any
change to the goal rule (the movement clause is the owner's, pre-registered later).

## 1. The three reads

All three follow the existing pattern: computed in `Sample()` once per physics step into arrays,
returned by `Read()`. Every value finite and within [-1, 1] before it reaches a neuron
(`Brain.Evaluate`'s NaN/Inf guard would otherwise silently zero a legitimate "very safe" or
"very rich" reading).

### Chemical (per part, index 0 only; `IndexCount` stays 1)

- Quantity: `World.Nutrients.EdibleDensityAt(partPosition.y, creature.Patch)` in J/m³. The
  *edible* density, not `DensityAt`: what a mouth can draw is what a nose should report (D055's
  refuge discount included).
- Per part, at the part's own height. §4.4's argument that morphology is the gradient sensor
  depends on this; reading the root only would defeat the channel's purpose.
- Squash: `x / (x + k)`, `k = RunConfig.ChemicalHalfScaleJoulesPerCubicMetre`
  (`EVOSIM_CHEMICAL_HALF_SCALE`), default **10** J/m³. Reads 0.5 at k, never saturates hard, and
  keeps resolution across the two decades the field actually spans (0.3 to 100 J/m³ in the
  round-23 reports). Document it as unmeasured (§5A.10) in the same voice as
  `FullScaleRadPerSecond`'s remark. A linear clamp was rejected because a gradient sensor that
  saturates at rich water is blind exactly where the food is.
- Plumbing: `CreatureSensors` gains a `NutrientField` reference (constructor; `Ecosystem.Build`
  has `World.Nutrients` in scope where it constructs the sensors). `CreatureInstance.Patch` is
  refreshed each metabolic step already; read it, do not recompute it.
- **Option, not built by default, flagged for the owner:** a second index (`IndexCount` 2)
  reading `World.Matter` at the same point, the matter smell. The design names only food smell;
  matter at depth is the open frontier and a stomach that can smell matter could move to it.
  Leave `IndexCount` at 1 and put a one-paragraph note in the handoff. Do not build it unasked.

### Energy (per creature; `Index` ignored as documented)

- Quantity: `Organism.SecondsOfReserve` (already computed for this purpose, never connected).
  `CreatureSensors` gains the `Organism` reference (`Body.Creature` at build time).
- Squash: `tanh(seconds / k)`, `k = RunConfig.EnergyFullScaleSeconds`
  (`EVOSIM_ENERGY_FULL_SCALE`), default **3,000** s (the senescence scale; a creature holding a
  lifetime of reserve reads as sated). `+Infinity` (zero burn) reads **1**, explicitly, before
  the tanh; never let the infinity reach the guard.
- Granularity: the value changes once per metabolic step and is read every physics step. State
  that in the remark; it is a cached division, not work invented.

### Flow (per part, three indices = the part's own axes)

- Quantity: the relative water velocity `FluidEnvironment` already computes per part per step
  for drag (`_velocity[at + i] = body.linearVelocity - water`, world frame). Expose it read-only
  (an accessor by creature slot and part, or a span over the per-creature range; no copy per
  step) and have `Sample()` rotate it into the part's frame with
  `transform.InverseTransformDirection`.
- Normalise: `clamp(v_axis / FlowFullScaleMetresPerSecond, -1, 1)`, `RunConfig`
  tunable `EVOSIM_FLOW_FULL_SCALE`, default **0.3** m/s (the reference world's current speed;
  the same "constant is honest" argument as the joint rate).
- **Staleness, stated:** `Sample()` runs before `Fluid.Apply` in `Ecosystem.Step`, so Flow reads
  the previous physics step's relative velocity, and the first step of a creature's life reads
  zero. This is a stated design choice (everything in `Sample()` reads start-of-step state), not
  a bug; put it in the remark and do not reorder the step. Reordering would change the drag pass
  and break replay identity.
- The recompute route (a second `Current.VelocityAt` per part per step) is rejected on cost.

## 2. The pool: which channels mutation may draw

- `RunConfig` gains three booleans, `SenseChemical`, `SenseEnergy`, `SenseFlow`
  (`EVOSIM_SENSE_CHEMICAL`, `EVOSIM_SENSE_ENERGY`, `EVOSIM_SENSE_FLOW`), default **false**.
  Three booleans rather than a flags enum because the JSON layer serialises scalar enums by
  name and the reflection tests already cover booleans.
- `RunConfig.SensorPool()` returns the drawable channels: the four of today, plus each enabled
  one, in enum order (`Chemical`, `Energy`, `Flow` after `Depth`). Compute once, cache.
- `SensorChannels.RandomSensor(rng, weight)` becomes `RandomSensor(rng, weight, pool)`;
  `GenomeFactory` and `Mutator` pass the pool from the config they run under (find the config
  path each already has; do not add a static). With the default pool the array is the same
  length and order as today's `Implemented`, so `rng.Pick` consumes the same draw and returns the
  same channel: **the founder and mutation RNG sequences are unchanged at the default.** This
  is the identity requirement; verify it with the validation arm below, not by inspection.
- `SensorChannels.Implemented` becomes the seven the simulator answers. Its doc comment's
  contract ("listed here and unhandled there fails the smoke test") is why it must list all
  seven and why the smoke test must grow (section 3). `IsImplemented` keeps meaning "the
  simulator answers it"; `MutationTests.RewiringReachesEveryInputKindAndOnlyImplementedChannels`
  gains a sibling asserting rewiring draws only from the *pool*, and that with all three enabled
  it reaches all seven.
- Header token in the run report: `senses jointangle,jointrate,up,depth` at default, the enabled
  channels appended; `run.json` carries the three booleans through the config hash as usual.

## 3. Measured, not promised: the smoke test

`Milestone1Smoke.CheckSensors` today builds a creature with no `World`, no fields, no
`Organism`, and walks `Implemented` asserting finite and non-constant. Extend it so the three
new channels can vary:

- A `NutrientField` seeded with a deliberate vertical gradient (rich at one depth, poor at
  another) so a multi-part creature spanning depths reads different `Chemical` values across
  parts in the same step.
- A `CurrentField` with non-zero speed so `Flow` is non-zero and varies as the body turns.
- A stand-in `Organism` whose `Energy` is decremented by the test between samples (or drive
  `Metabolise` if the harness gains a `World`; the stand-in is the smaller change and is enough).

Keep the existing "CONSTANT — nothing implements it" failure text; that is the check's whole
point. Add the seven-channel result to the smoke test's printed summary.

## 4. The instrument the movement round reads

Add to each `stats.jsonl` row and the run report (named columns; never positional):

| column | meaning |
|---|---|
| `spd jnt` | mean root speed (m/s) over the sample window, living creatures with at least one joint |
| `spd rig` | the same for jointless creatures |
| `food jnt` | mean edible density (J/m³) at the root of jointed creatures at the sample |
| `food rig` | the same for jointless creatures |

Root speed: accumulate root displacement per metabolic step (positions are already read for
`CheckFinite`), divide by window seconds. "Movement pays" reads as `food jnt` above `food rig`
by more than the wingspan across seeds, with `spd jnt` non-trivial; a jointed body sitting in
the same water as a rigid one is a swimmer that gained nothing. Both are cheap (one subtraction
and one field read per creature per metabolic step). Where the jointed count is zero the column
reads empty, not 0.

## 5. Recommended, separable: the requirement mask (§4.4)

`Sample()` computes every channel for every part every physics step regardless of whether any
neuron reads it; the design's on-demand mask was never built. With seven channels at 4,000
bodies this is the per-part per-step loop §5A.9 measured as the bottleneck. Build the mask if it
fits: at phenotype build, walk the brain's `NeuronInput`s once and record which channels any
neuron references (a small bitmask per creature); `Sample()` skips the transform reads and field
lookups for absent channels. Skipping the computation of a value nobody reads is bit-identical
by construction. Measure the wall-clock difference on the validation arm and report it. If this
turns out to be more than a day, land items 1 to 4 first and leave the mask as a follow-up with
the measurement that motivates it.

## Validation (before handing back)

1. `./scripts/core-test.ps1` green, including the two reflection tests with the six new
   tunables (three squash constants, three booleans).
2. Milestone 1 smoke: all seven channels finite and non-constant.
3. **Replay identity at the default:** rerun one historical configuration at dt 0.01 on the new
   build for 2,000 s (the r16dt-01c configuration is the reference; any 0.01 arm with a
   `run.json` will do) and diff the run report rows modulo the appended columns and wall clock.
   Byte-identical rows are the pass; a single differing row is a fail, even a late one (0059's
   `float` local broke identity at t=200; that is the class of fault this catches).
4. A 2,000-s arm at dt 0.01 with all three senses enabled: no divergence, audit 0.0000%, the
   `senses` header token present, at least one creature in the last snapshot carrying a
   `Chemical`, `Energy` or `Flow` input (grep the snapshot JSONL).
5. Report wall-clock cost of the three reads (and of the mask, if built) against the default on
   the same configuration.

Hand back: the commit, the validation numbers, and the two flagged notes (matter smell; the
staleness choice) for the record. The logbook entry for the build is Fable's, from your report.
