# Perception survey: wiring Chemical, Flow, Energy

Read-only survey. Scope: what it would take to make the three unimplemented `SensorChannel`
values report real quantities so an evolved brain can steer toward matter/food, and what that
does or doesn't do to existing runs.

## 1. The channels the design names, and which read today

`DESIGN.md` §4.4 (`DESIGN.md:478-589`) names, in prose: joint angle, joint angular velocity,
contact, orientation-vs-up, damage, the photosensor triple, and (Milestone 4) chemical/energy/
flow — then a separate table adds depth (`DESIGN.md:498-504`). The genome enum
(`src/Evosim.Core/Genome/NeuronInput.cs:52-190`, `SensorChannel`) has all ten as explicit
values: `JointAngle=0`, `JointAngularVelocity=1`, `Contact=2`, `OrientationUp=3`, `Photo=4`,
`Damage=5`, `Chemical=6`, `Energy=7`, `Flow=8`, `Depth=9`.

What actually answers a read is a second, narrower list —
`src/Evosim.Core/Brain/SensorChannels.cs:42-48`:

```csharp
public static readonly SensorChannel[] Implemented =
{
    SensorChannel.JointAngle,
    SensorChannel.JointAngularVelocity,
    SensorChannel.OrientationUp,
    SensorChannel.Depth,
};
```

`unity/Assets/Evosim/Sim/CreatureSensors.cs:120-147` (`ISensorField.Read`) is the sampler, and
its switch handles exactly those four; everything else — `Contact`, `Photo`, `Damage`,
`Chemical`, `Energy`, `Flow` — falls to the `default: return 0f;` at lines 144-146, whose own
comment names Chemical/Energy explicitly as needing work not yet done. Sampling code for the
four working channels, all in `CreatureSensors.Sample()` (`CreatureSensors.cs:75-117`):

- **Depth** — `_depth[b] = Mathf.Clamp01(-t.position.y / _worldDepthMetres)` (line 87), world
  transform read, clamped fraction of water column.
- **OrientationUp** — `_up[b] = Vector3.Dot(t.up, Vector3.up)` (line 88).
- **JointAngle** — normalised against the joint's own limit (lines 105-111).
- **JointAngularVelocity** — normalised against a fixed constant `FullScaleRadPerSecond = 10f`
  rad/s (lines 48, 113-114).

So the task's "three unread channels" (Chemical, Flow, Energy) is accurate; `Contact`,
`Damage` and `Photo` are also unread but explicitly out of scope for this survey (predation/
Milestone 6 machinery, not matter/food steering).

## 2. Chemical, Flow, Energy: what each should read, and the smallest wiring

### Chemical — smell

- **Design intent**: "Nutrient concentration in the water at this part" (`NeuronInput.cs:100-
  115`; `DESIGN.md:500`) — the signal that is supposed to make `AbsorptiveCell` foraging a
  strategy instead of a lottery.
- **Supplying field**: `World.Nutrients` (`src/Evosim.Core/Ecosystem/World.cs:195`, a
  `NutrientField`) is the field feeding actually prices against — confirmed by
  `Metabolise()`'s own read, `Nutrients.EdibleDensityAt(creature.HeightY, creature.Patch)`
  (`World.cs:936`, `:974`). `EdibleDensityAt(float heightY, int patch)`
  (`src/Evosim.Core/Environment/NutrientField.cs:296-302`) is the method; there is a plain
  `DensityAt` (lines 276-283) that ignores the D055 refuge discount — `EdibleDensityAt` is the
  honest one to sense, since it's what a mouth can actually draw.
- **Units**: J/m³ (`NutrientField.cs:268-269`), unbounded above — not naturally in
  [-1, 1]. **No existing normalisation constant covers this** (see ambiguity below).
- **Point sampled**: per-part, at that part's `(heightY, patch)` — patch already lives on
  `CreatureInstance.Patch` (`unity/Assets/Evosim/Sim/PhenotypeBuilder.cs:46-60`), refreshed
  each metabolic step by `Ecosystem` per that field's own doc comment. Not the root: §4.4's own
  argument (`DESIGN.md:531-543`) is that a gradient is read by *different parts* at *different
  positions*, and that's morphology's contribution to perception — reading only the root would
  throw that away for exactly this channel.
- **Gradient or scalar**: scalar per part is enough — §4.4 is explicit that no channel reports
  a bearing (`DESIGN.md:531-537`); a creature with two parts at different depths/patches
  already gets a gradient for free by comparing two scalar reads through the neuron graph
  (`SameNode`/`ParentNode`/`ChildNode` inputs, already implemented in `Brain.cs:379-393`).
- **Smallest change**: give `CreatureSensors` a reference to the `NutrientField` (constructor
  currently takes only `(CreatureInstance, float worldDepthMetres)` —
  `CreatureSensors.cs:58-70`) and add a case in `Read()`:
  `Nutrients.EdibleDensityAt(t.position.y, _creature.Patch)`, squashed into range (e.g.
  `Mathf.Clamp01` against some cap, or a `tanh`). `Ecosystem.Build()` constructs
  `CreatureSensors` at line 730 and already has `World.Nutrients` in scope there.
- **Left unspecified by the design**: the normalisation constant (what density reads as "1"),
  and whether Chemical should ever also read `World.Matter` (`World.cs:213`, a separate
  `NutrientField` instance tracking matter rather than energy — CLAUDE.md's "matter at depth"
  frontier). §4.4 only ever talks about food/energy smell; matter-smell is not named anywhere
  and would be a second channel or a second index, not free to assume.

### Energy — reserve as seconds of life left

- **Design intent**: "the creature's reserve, as seconds of life remaining at its current burn
  rate" (`DESIGN.md:501`, `:522-529`; `NeuronInput.cs:117-142`).
- **Supplying quantity**: already computed, unused. `Organism.SecondsOfReserve`
  (`src/Evosim.Core/Ecosystem/Organism.cs:224-229`):
  ```csharp
  public float SecondsOfReserve =>
      StandingWatts > 1e-9f ? Energy / StandingWatts : float.PositiveInfinity;
  ```
  Its own doc comment literally says "What `SensorChannel.Energy` reports" — this was built
  for the purpose and never connected. `Energy` (joules, `Organism.cs:40`) and `StandingWatts`
  (`Organism.cs:89`) are both updated once per metabolic step, not per physics step.
- **Units**: seconds (can be `+Infinity` for a creature at zero burn — needs clamping before it
  reaches a neuron, since `float.IsInfinity` would be zeroed by `Brain.Evaluate`'s NaN/Inf guard
  at `Brain.cs:280-283` anyway, silently discarding the "very safe" case rather than reporting
  it as a large number).
- **Point sampled**: whole-creature, not per-part — `NeuronInput.Index` is documented as
  ignored (`NeuronInput.cs:139`), matching `§5A.6` killing the creature, not the part.
- **Gradient or scalar**: scalar only; there is nothing to take a gradient of (one value per
  creature).
- **Smallest change**: `CreatureSensors` needs a reference to the `Organism` — it currently
  only holds `CreatureInstance` (the physics/body side), not `Body.Creature` (the `Organism`,
  `Ecosystem.cs:211`, `:727`). Add that reference in the constructor, add a case in `Read()`
  returning some monotonic squashing of `_organism.SecondsOfReserve` (e.g. `tanh(seconds /
  k)`), independent of `partIndex`.
- **Left unspecified**: the normalisation constant `k` that maps "seconds of life left" onto
  ≈[-1, 1] — the design explicitly rejects a hard threshold (`DESIGN.md:522-529`) but a squash
  function still needs *some* scale, and none is named. Also unspecified: whether infinite
  reserve (burn rate zero) should read as +1 (saturated) or some other sentinel.

### Flow — lateral line

- **Design intent**: "Water velocity relative to this part, per axis" (`DESIGN.md:502`;
  `NeuronInput.cs:144-154`, `IndexCount` = 3, `SensorChannels.cs:76`).
- **Supplying quantity — already computed once per part per physics step, for drag, and
  discarded.** `FluidEnvironment.Apply` (`unity/Assets/Evosim/Sim/FluidEnvironment.cs:217-
  251`) does, per part, exactly this:
  ```csharp
  Float3 water = Current.VelocityAt(
      body.transform.position.y, ElapsedSeconds, creature.Patch, PatchCount);
  _velocity[at + i] = body.linearVelocity.ToFloat3() - water;   // FluidEnvironment.cs:244-249
  ```
  That `_velocity` array (relative velocity, world axes, m/s) is the numerator of the drag
  force computed a few lines later — it is not currently exposed outside `FluidEnvironment`.
  `CurrentField.VelocityAt(heightY, seconds, patch, patchCount)` itself is
  `src/Evosim.Core/Environment/CurrentField.cs:495` (patch-aware overload; a patch-less one at
  line 418), units m/s (doc comment, line 400/444).
- **Units**: m/s, world-frame as computed; the design wants it "along the part's own axes"
  (`NeuronInput.cs:145`), so it needs a further `transform.InverseTransformDirection(...)` to
  rotate into the part's local frame before indexing by axis.
- **Point sampled**: per part, at that part's position and patch — same reasoning as Chemical.
- **Gradient or scalar**: this one *is* naturally a 3-vector (per-axis), which is why
  `IndexCount` is already 3 for it — no cross-part gradient needed; the three axes at one part
  already give a bearing-in-the-part's-own-frame, which is what a lateral line is.
- **Smallest change, two options**:
  1. Recompute independently in `CreatureSensors.Sample()`: another `Current.VelocityAt(...)`
     call plus another `transform.position` / `linearVelocity` read per part per physics step —
     duplicates work `FluidEnvironment` already does.
  2. Have `FluidEnvironment` hand back its already-computed relative-velocity array (or a
     per-part accessor) and have `CreatureSensors` read *that*, transformed to local axes —
     free in the sense that the drag pass has already paid for it; requires threading a
     `FluidEnvironment` reference (or its output) into `CreatureSensors`, and ordering: `Sample()`
     runs at `Ecosystem.cs:310`, *before* `Fluid.Apply` at line 320 in the current step order —
     so Flow would read *last* step's relative velocity, one physics step stale, unless the
     order is also changed. That staleness is a step behind, of the same kind depth/joint
     reads already accept implicitly (everything in `Sample()` reads the state as of the start
     of the step); worth flagging as a design choice, not a bug, but it should be a stated one.
- **Left unspecified**: the normalisation constant (full-scale m/s, the Flow analogue of
  `FullScaleRadPerSecond`), and which of the two wiring routes above is intended — the design
  text doesn't say whether Flow should share machinery with drag or be independent of it.

## 3. Physics step vs. metabolic step, and cost

Sensing and brain evaluation run on **every physics step**, not the metabolic step:
`Ecosystem.Step()` (`unity/Assets/Evosim/Sim/Ecosystem.cs:300-332`) calls
`body.Sensors.Sample()` and `body.Brain.Step(FixedDt, ...)` inside the per-creature loop at
lines 310-311, which executes on every call to `Step()` — i.e. every `FixedDt` (0.01–0.02 s
per CLAUDE.md's dt discussion). `Metabolise()` (line 330) — where `Organism.Energy` and
`StandingWatts` actually update — runs only every `StepsPerMetabolicStep` physics steps (the
0.5 s metabolic tick CLAUDE.md's `ledger.ps1` docs reference). So Energy would be read far more
often than it changes; that's harmless (a cached field read/division), not free work invented.

**Cost per creature of adding all three, per physics step**:
- Chemical: one `NutrientField.EdibleDensityAt` call per part that reads it — O(1) array index
  arithmetic (`LayerOf` + list indexing, `NutrientField.cs:249-254`, `:296-302`), no allocation.
- Energy: one `Organism.SecondsOfReserve` read (a division) per creature, not per part.
- Flow: either a second `CurrentField.VelocityAt` (closed-form trig, no allocation,
  `CurrentField.cs:418-442`, `:495-...`) plus a second `transform.position`/`linearVelocity`
  read per part, **or** free if reusing `FluidEnvironment`'s existing `_velocity` array.

**The bigger, pre-existing cost fact**: §4.4's "sensors are evaluated on demand, not per
channel per part" (`DESIGN.md:545-550`) — a per-phenotype requirement mask so a channel costs
nothing until some neuron references it — **is not implemented anywhere**. Searches for a
mask/required-channels mechanism in both `Evosim.Core` and `Evosim.Sim` turned up nothing.
`CreatureSensors.Sample()` unconditionally computes depth, orientation, joint angle and joint
rate for *every part, every physics step*, regardless of whether any neuron reads them
(`CreatureSensors.cs:80-116`). So today's four channels already violate the "costs nothing
until used" promise, and wiring Chemical/Flow/Energy the same way (unconditional per-part
computation every step) would compound that rather than introduce a new problem — but it is
the moment to notice the gap, since §5A.9 is on record as measuring the per-part per-step loop
as the bottleneck (CLAUDE.md, DESIGN.md:548-550).

## 4. What the genome already carries — does wiring change existing runs?

**`Chemical`, `Energy` and `Flow` are already legal `NeuronInput`/`SensorChannel` values** —
they've been in the enum since it was declared (`NeuronInput.cs:52-190`), and nothing in
`Genome.Validate` restricts a genome to `SensorChannels.Implemented` (confirmed: no reference
to `SensorChannels` or channel legality anywhere in
`src/Evosim.Core.Tests/GenomeValidationTests.cs`; the doc comment at `SensorChannels.cs:11-16`
says outright "a genome carrying a `Photo` input is not invalid, it is early").

**But nothing currently *produces* a genome carrying one.** Both places that mint a sensor
reference draw exclusively from `SensorChannels.Implemented`:
- Founder generation — `src/Evosim.Core/Genome/GenomeFactory.cs:617-625`:
  `inputs[a] = ... SensorChannels.RandomSensor(rng, ...) ...` where `RandomSensor`
  (`SensorChannels.cs:85-91`) does `rng.Pick(Implemented)`.
- Mutation's rewire operator — `src/Evosim.Core/Mutation/Mutator.cs:486-498`, same call at
  line 492.

And it's tested, not just true by inspection:
`src/Evosim.Core.Tests/MutationTests.cs:305-347`
(`RewiringReachesEveryInputKindAndOnlyImplementedChannels`) runs 300 mutation steps and asserts
`Assert.All(channels, c => Assert.True(c.IsImplemented(), ...))` — i.e. it actively checks that
mutation *never* introduces an unimplemented channel.

**Conclusion**: no genome born under the current code — founder or descendant, in any run ever
launched — references `Chemical`, `Energy` or `Flow`. Implementing real reads for them in
`CreatureSensors.Read()` alone changes **zero** existing behaviour, because no neuron in any
existing population reads that switch case. Behaviour only starts changing for *newly
mutated* offspring once `SensorChannels.Implemented` is *also* extended to include the new
channels (that's the second, separate gate — `SensorChannels.cs:42-48` — since that's what
`GenomeFactory`/`Mutator` actually draw from). At that point it's a live, ongoing-run
consequence: a creature born after the change can acquire a real Chemical/Energy/Flow input
where before it would have kept drawing dead ones. If that needs to be an opt-in behind a
knob rather than unconditional, the natural lever is `SensorChannels.Implemented` itself — gate
which entries it contains on a `RunConfig` flag (e.g. an `EVOSIM_*` env var already read
elsewhere in the config) rather than changing the static array outright, so existing/in-flight
arms keep reproducing bit-for-bit unless someone deliberately turns each channel on.

One caveat: a **hand-authored** genome (e.g. a scratch JSON built for `ledger.ps1`, or a
manually edited snapshot) *can* already set `Channel = Chemical/Energy/Flow` today, since
loading only rejects missing fields, not unimplemented-but-legal enum values. Such a genome
would go from silently reading zero to reading a real signal the moment `CreatureSensors.Read`
is implemented — before `SensorChannels.Implemented` is touched at all. This is a narrow case
(nothing evolutionary produces such a genome) but worth naming since the CLAUDE.md rule "loading
refuses rather than defaults" doesn't protect here — the field is present and valid, its
*meaning* is what changes.

## 5. Existing sensor tests, and what adding these channels would need

- **Milestone 1 smoke test**, `unity/Assets/Evosim/Sim/Editor/Milestone1Smoke.cs:1092-1199`
  (`CheckSensors`) — the only real coverage. It builds one multi-part creature with no `World`,
  no `NutrientField`, no `CurrentField`, no `Organism` in scope at all (just `CreatureInstance`
  + `FluidEnvironment` + `RunConfig` defaults, lines 1094-1118), drives it for `SwimSeconds`,
  and for every channel in `SensorChannels.Implemented` asserts (a) every read is finite and
  (b) the channel *varies* — either over time or across parts in the same step (lines 1120-
  1182) — flagging a channel that never varies as `**CONSTANT — nothing implements it**`. Its
  own doc comment (lines 1064-1091) explains why this exists: `SensorChannels.Implemented` is a
  claim `Evosim.Core` makes about code in `Evosim.Sim` that the compiler cannot check across
  that assembly boundary (§6.1), so this is the check, and it has already caught exactly this
  class of bug twice (logbook/0016, logbook/0019 per its own remarks).
- **`MutationTests.cs:305-347`** (cited above) guards the *other* direction: that mutation
  never draws an unimplemented channel. It reads `SensorChannels.Implemented`/`IsImplemented`
  directly, so it needs no change to keep passing once new channels are wired — it would only
  start exercising them once they're added to `Implemented`.
- **No dedicated `CreatureSensorsTests`, `NutrientField`/`CurrentField`-through-sensor test, or
  Organism-through-sensor test exists.** `Evosim.Core.Tests` covers `NutrientField`,
  `CurrentField` and `Organism` in isolation (their own field-level tests) but nothing exercises
  them *through* `ISensorField`/`CreatureSensors`, because `Evosim.Core` cannot reference
  `Evosim.Sim` (§6.1) — that coverage can only live in the Unity-side smoke test.

**What adding Chemical/Flow/Energy would need**:
1. Extend `Milestone1Smoke.CheckSensors` to add the three channels to whatever list it walks
   once they're in `Implemented` — but its harness currently has no `World`/`NutrientField`/
   `CurrentField`/`Organism`, so the test needs to gain a real `World` (or at least a standalone
   `NutrientField` + `CurrentField` + a stand-in `Organism`) to have anything non-zero to sample
   — this is the largest gap, not a small addition.
2. A variation check for Chemical/Flow needs the creature to actually move through a
   non-uniform field (a nutrient gradient across depth/patch, a current with non-zero `Speed`)
   or the "varies over time/across parts" assertion the existing test makes will trivially fail
   even when correctly wired — worth pre-seeding the test's field with a deliberate gradient.
3. Energy varying "over time" needs `Metabolise()` to actually run during the check (it
   currently isn't called in `CheckSensors` at all — only `Sensors.Sample()` /
   `Brain.Step()` / physics), so either the smoke test starts driving the metabolic step too,
   or Energy gets a separate, `World`-level integration test instead of living in this one.
4. `MutationTests.RewiringReachesEveryInputKindAndOnlyImplementedChannels` needs no code change
   but its assertion set will exercise the new channels automatically the day they're added to
   `Implemented` — good, cheap regression coverage for free.
5. If `SensorChannels.Implemented` is gated behind a `RunConfig` flag (§4, above), that flag
   needs its own `RunConfigTests`/`RunConfigJsonTests` coverage per CLAUDE.md's existing rule
   that every tunable must reach `RunConfig.Hash()` and survive save/reload.

## Open ambiguities to flag rather than guess past

- No normalisation constant is named anywhere for Chemical (J/m³ → [-1,1]) or Energy
  (seconds → [-1,1]) or Flow (m/s → [-1,1]); `SatiationWattsPerCubicMetre`
  (`src/Evosim.Core/RunConfig.cs:654`) is the closest existing scale for density but is a
  per-run tunable that defaults to *off* (zero), not a universal constant, and is W/m³ price
  ceiling rather than a raw-density full-scale.
- Whether Chemical should ever read `World.Matter` in addition to `World.Nutrients` is
  unaddressed by §4.4 and directly relevant to CLAUDE.md's "matter at depth" open frontier —
  the design as written is only about food/energy smell.
- Flow's staleness-by-one-step if reusing `FluidEnvironment`'s drag-pass velocity (point 2,
  Flow) is a real design choice the source material doesn't make explicitly.
- Infinite `SecondsOfReserve` (zero burn rate) has no stated sentinel value for the sensor
  reading, and as written would be silently zeroed by `Brain.Evaluate`'s NaN/Infinity guard
  (`src/Evosim.Core/Brain/Brain.cs:280-283`) rather than reported as "very safe."
- The on-demand sensing mask §4.4 describes (`DESIGN.md:545-550`) doesn't exist for any
  channel today, so "smallest change" above assumes unconditional per-part-per-step
  computation, matching the existing four channels' actual behaviour rather than the design's
  stated intent for cost — worth deciding whether to build the mask now or continue deferring
  it.
