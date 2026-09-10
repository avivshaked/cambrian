# Build report: perception for movement (D075 item 1)

Implementing agent's hand-back for `logbook/specs/perception-spec.md`. Everything in the spec's
scope is built, including item 5 (the §4.4 requirement mask). Nothing was launched beyond the
three validation arms named below.

## Commit

| commit | what |
|---|---|
| `e59f6af` | *Perception wired: Chemical, Energy and Flow answer, behind a per-run pool* — the whole build: the three reads, the pool, the mask, the instrument, the smoke-test harness, the DESIGN.md §4.4 note, and `src/Evosim.Core.Tests/SensorPoolTests.cs` (9 new tests). 15 files, +1134 / −53. |

Committed on `main`, not pushed. The two untracked `sol-gpt-*-review.md` files in the working
tree are not mine and were left alone.

## The six new tunables

All six reach `RunConfig.Hash()` and `config.json` through `ConfigSchema`'s reflection walk —
`RunConfigTests` and `RunConfigJsonTests` cover them without a line of new test code. Group
`sense`.

| tunable | default | env var | note |
|---|---|---|---|
| `SenseChemical` | `false` | `EVOSIM_SENSE_CHEMICAL` | may a genome *draw* Chemical |
| `SenseEnergy` | `false` | `EVOSIM_SENSE_ENERGY` | …Energy |
| `SenseFlow` | `false` | `EVOSIM_SENSE_FLOW` | …Flow |
| `ChemicalHalfScaleJoulesPerCubicMetre` | `10` J/m³ | `EVOSIM_CHEMICAL_HALF_SCALE` | ⚠ unmeasured; `x/(x+k)` |
| `EnergyFullScaleSeconds` | `3000` s | `EVOSIM_ENERGY_FULL_SCALE` | ⚠ unmeasured; `tanh(s/k)` |
| `FlowFullScaleMetresPerSecond` | `0.3` m/s | `EVOSIM_FLOW_FULL_SCALE` | ⚠ unmeasured; clamp per axis |

The booleans gate `RunConfig.SensorPool()` only. `CreatureSensors` answers all seven channels
in every run; what the switches decide is what founder generation and mutation may reach for,
because a longer draw pool returns a different channel for the same `Rng.Pick` draw and that
is what would end the replay of the record.

## Header token

Rendered unconditionally (D065's rule), immediately before `inoculate`/`configHash`:

```
 · senses jointangle,jointrate,up,depth · sense scale chem 10 J/m3, energy 3000 s, flow 0.3 m/s
```

With all three on (`pv-senses`'s actual header):

```
 · senses jointangle,jointrate,up,depth,chemical,energy,flow · sense scale chem 10 J/m3, energy 3000 s, flow 0.3 m/s
```

The channel list is built from `config.SensorPool()` itself, not from the three booleans, so the
token and the draw cannot disagree — the failure class this project has twice paid for is a
setting that never reached the thing it configured.

## Validation

### 1. `./scripts/core-test.ps1`

**517 passed, 0 failed** (3 m 49 s), up from 508 before the build. The nine new ones are
`SensorPoolTests`: the default pool is today's four in today's order; `Implemented` is the
seven the simulator answers; enabled channels append after `Depth`; the pool cache follows the
knobs; a default-pool draw returns the same channel *and leaves the stream in the same state*
as the old call; founders are unchanged with the knobs off; mutation draws from the pool and
only from the pool (and reaches all seven with all three on); `Brain.SensorMask` is exactly the
set its neurons read, in both directions; `Brain.AllSensorChannels` reads everything.

### 2. Milestone 1 smoke — all seven channels

Ran against `unity/` (no Editor open, only the four arm processes on w2–w5). **PASS.**

```
### Sensors — DESIGN.md §4.4

Channels the simulator answers: 7. What a run at these settings lets a genome draw: 4
(the rest wait on `EVOSIM_SENSE_*`).

| channel               | min     | max    | spread   | verdict |
|-----------------------|---------|--------|----------|---------|
| JointAngle            | -1      | 1      | 1.999982 | reads   |
| JointAngularVelocity  | -0.4187 | 0.3837 | 0.550769 | reads   |
| OrientationUp         | -0.1483 | 1      | 1.061974 | reads   |
| Depth                 |  0.3241 | 0.334  | 0.009695 | reads   |
| Chemical              |  0.6094 | 0.6183 | 0.008946 | reads   |
| Energy                |  0.0012 | 1      | 0        | reads   |
| Flow                  | -0.4485 | 0.7395 | 1.123463 | reads   |

All 7 channels in `SensorChannels.Implemented` vary over the run or across the body.
```

`spread` is the within-a-step range *across the body*; Energy's 0 is correct — it is a
whole-creature reading and varies only over time (0.0012 → 1, the 1 being the `+Infinity`
zero-burn case answered before the tanh). Chemical's spread is non-zero, which is the point:
parts at different depths read different water.

### 3. Replay identity at the default — **PASS, 20 of 20 rows**

**What I could not do as written, and what I did instead.** The spec names
`runs/r16dt-01c/` as the reference and asks for a diff of the report rows. That report cannot
be reproduced by any build at HEAD: it was written before `PhysicsStepSeconds` and D074's
matter tunables existed, so it carries **45 columns against HEAD's 54** and configHash
`1c50cf52…` against a HEAD configHash of `5a5127f4…` for the same settings. Diffing against it
would have measured four months of other people's commits, not my change.

So I ran the identity test the strict way: the **pre-change build** at HEAD on the r16dt-01c
settings (`pv-base`), then the **post-change build** with byte-identical settings and seed
(`pv-replay`). `pv-base` was launched and had written its `run.json` before I touched a line
of source.

| | `pv-base` (pre-change) | `pv-replay` (post-change) |
|---|---|---|
| commit | `b9b693a` clean | `01028cc` dirty (this build) |
| configHash | `5a5127f49eaefb32` | `e3b6fdf29c12e327` (six new tunables) |
| columns | 54 | 58 |
| seed / dt / budget | 2 / 0.01 / 2,000 s | identical |
| physics steps | 200,000 | 200,000 |
| births | **456** | **456** |
| fastest creature ever | **0.403 m/s at t=1849.5 s** | **0.403 m/s at t=1849.5 s** |

```
$ python scripts/reads/compare-rows.py runs/pv-base.md runs/pv-replay.md
baseline columns: 54   new columns: 58   appended: ['**spd jnt**', '**spd rig**', '**food jnt**', '**food rig**']
baseline rows: 20   new rows: 20
rows compared: 20   differing: 0
```

Every one of the 54 baseline columns is character-identical in every one of the 20 rows,
including `audit`, `depth sd` and `rise m` — the columns that would move first if a `float`
local had shifted a rounding (logbook/0059's fault class). The comparison script is
`scripts/reads/compare-rows.py` (transient; gitignored).

Settings used for both, from r16dt-01c's own `config.json` and header: irradiance 200,
power 5–20, current 0.3 m/s / 6,000 s / 30 m cells with rolls blinking at 3,000 s and advection
on, mixing 0.2, sinks 0.002/0.002, excretion 0.01, 4 patches, area 100, floor closes 3,000,
ceiling 8,000, senescence 3,000, cellType mutation 0.005, clearance 1, excess density 0.02,
neutral volume 0.25, founder depth 60, matter 0.5/J + 3 each from 1/m³, float 0.5 at
0.05 W/lift, report every 200, seed 2, dt 0.01.

### 4. All three senses on — `pv-senses`

Same settings and seed, plus `EVOSIM_SENSE_CHEMICAL/ENERGY/FLOW=1`. configHash `96503b8d…`.

- `senses` header token present and reading all seven — quoted above.
- **audit 0.0000 %** in every row.
- **`diverged` 0** in every row; drag impulses limited 0; drive impulses limited 0.
- 534 births against the default's 456 — a different world, which is the expected and required
  consequence of the pool changing which channel a draw returns.
- Last snapshot (`snapshots/000002000.jsonl`, 930 genome rows): sensor inputs by channel —
  JointAngularVelocity 248, Depth 232, JointAngle 16, OrientationUp 10, **Energy 6,
  Chemical 6, Flow 2**. **8 genomes carry at least one of the three new inputs.**

The instrument reads (early rows, where this seed still has jointed bodies):

| t | alive | jointed | spd jnt | spd rig | food jnt | food rig |
|---|---|---|---|---|---|---|
| 200 | 40 | 5 | 0.01532 | 0.01458 | 0.6884 | 0.7228 |
| 300 | 45 | 3 | 0.03931 | 0.04281 | 0.7940 | 0.8364 |
| 400 | 54 | 3 | 0.06918 | 0.08491 | 0.7947 | 0.7684 |
| 500 | 70 | 2 | 0.09166 | 0.12732 | 1.1432 | 0.5652 |
| 600 | 94 | 0 | 0.11547 | 0.16315 | — | 0.5701 |

One asymmetry worth knowing before anyone reads these: at t=600 `spd jnt` has a number and
`food jnt` an em-dash. That is correct and not a bug — speed is accumulated over the report
window (creatures that were jointed and alive during it) and food is sampled at the row itself
(no jointed creature alive at that instant).

### 5. Wall clock

Same configuration, same seed, 2,000 s at dt 0.01, all three launched within ~20 minutes of
each other on worker 6 while the same four r24 arms occupied w2–w5.

| arm | wall clock | real-time factor |
|---|---|---|
| `pv-base` (pre-change) | 2.4 min | 14.1× |
| `pv-replay` (three reads + mask, all senses off) | **1.9 min** | **17.2×** |
| `pv-senses` (three reads + mask, all senses on) | 2.2 min | 14.9× |

**The build is faster than what it replaced**, by about 21 % on this configuration, because
the requirement mask now skips channels no neuron reads — and at the default pool most
creatures reference one or two of the four. Turning all three senses on costs about 16 % back
relative to `pv-replay`, and still lands inside the pre-change figure. Caveat: machine load was
comparable but not controlled (four other arms throughout), so treat these as the right order
of magnitude and the right sign, not as a benchmark. The mask cannot be separated from the
three reads by these three arms alone; if the lead wants that split, it is a fourth arm with
the mask forced to `AllSensorChannels`.

## The two flagged notes for the record

### Matter smell — offered, not built

The spec's option: a second `Chemical` index (`IndexCount` 2) reading `World.Matter` at the
same point. **Not built**, as instructed. What is worth the owner knowing when they rule on it:
the wiring is one array and one `case` — `CreatureSensors` already holds the part's position and
patch, and `World.Matter` is the same `NutrientField` type as `World.Nutrients`, so the read is
the same call against a different field and the same squash would serve. The cost is one extra
field lookup per part per step *for creatures whose brain reads the channel*, which the mask now
makes conditional; it was not conditional before this build.

Two things make it more than a convenience. First, `IndexCount(Chemical)` going from 1 to 2
changes what `rng.Range(channel.IndexCount())` draws, so it is a replay-breaking change on its
own — it would need the same treatment the pool got, or it lands as a run tunable. Second, the
design names only food smell (§4.4), and matter at depth is the frontier CLAUDE.md flags: a
stomach that can smell matter can *move to it*, which turns the open matter budget from a world
rule into something the animals participate in. That is a world-rule decision, so it is the
owner's.

### Flow staleness — a stated choice, not a bug

`Sample()` runs before `Fluid.Apply` in `Ecosystem.Step`, so Flow reports the relative velocity
the drag pass computed on the **previous** physics step, and a creature's first step of life
reads zero. Kept deliberately, for three reasons, and written into `CreatureSensors`' remarks so
nobody has to rediscover it: everything else in `Sample()` reads start-of-step state too, so
Flow is consistent with the rest of perception rather than an exception; reordering the step so
the fluid ran first would change the drag pass and end the replay of every run in the record;
and recomputing the current independently is a second `CurrentField.VelocityAt` plus a second
Transform read per part per step for a number the solver already has. At dt 0.01 the staleness
is 10 ms.

One implementation note that belongs with it. The obvious route — reading `FluidEnvironment`'s
own flat `_velocity` array by creature slot — is **wrong**, and quietly so. That array is indexed
by the creature's position in the list `Apply` was last called with, and `Ecosystem.Reconcile`
rebuilds that order on every birth and death, so slot *i* is a different creature between the
step that wrote it and the step that reads it. A sampler reading by slot would occasionally
report another animal's water, bounded and plausible and impossible to see. The velocity is
therefore stored on `CreatureInstance.RelativeVelocity`, allocated only for creatures whose
brain reads Flow, and the drag pass costs one null test per part per step when nothing does.

## Two more deviations from the spec, both small

1. **`IReserveSource` instead of the `Organism` reference.** The spec says `CreatureSensors`
   gains the `Organism`, and the smoke test gets "a stand-in `Organism` whose `Energy` is
   decremented by the test". That stand-in cannot be built: `Organism.Energy` and
   `StandingWatts` are `internal set`, so nothing outside `Evosim.Core`'s economy can pay a
   creature — a rule worth keeping. The seam is therefore a one-property interface,
   `IReserveSource { float SecondsOfReserve { get; } }`, which `Organism` satisfies by already
   having the property (its doc comment has said "What `SensorChannel.Energy` reports" since it
   was written). The simulator hands over the organism; the smoke test hands over a stand-in it
   can move. Nothing else changes.

2. **`stats.jsonl` writes means *and* counts, not NaN.** The spec says the report column reads
   empty where the jointed count is zero, which the markdown does (em-dash, `flt m`'s
   precedent). `Json.Writer.Field` refuses a non-finite double outright, so the JSONL carries
   `speedJointed`/`speedJointedSamples`, `speedRigid`/`speedRigidSamples`,
   `foodJointed`/`foodJointedCount`, `foodRigid`/`foodRigidCount` — 0 with a count of 0 where
   the guild is empty, so a reader can still tell "no swimmer" from "stationary swimmer".
   Eight appended fields rather than four.

## One thing a future reader should know

Adding tunables makes older `config.json` files unreadable by this build:
`RunConfigJson.Read` walks the schema and indexes `root[group][key]`, and §9's "loading refuses
rather than defaults" means a missing `sense` group throws. This is not new — every knob D074
added has the same property — but it now applies to `ledger.ps1 -Config` against any run
directory written before this commit.
