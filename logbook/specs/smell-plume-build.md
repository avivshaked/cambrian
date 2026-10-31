# The carried leaf smell and the current sense (D270): build specification

*Written on 2026-10-31 for the Sonnet builder, from D270 and `logbook/specs/smell-plume-design.md` (D270 holds where
they differ), D146, D147, D167, D234, D235, D251, D267 and the code on main. Item 9 of
`logbook/specs/build-plan-2026-10-30.md`.*

This build adds two world rules, each behind a switch that is off by default. `LeafPlume` is a far leaf smell on a 5 m
grid that the current carries, stirs and slowly loses, and every living leaf feeds it. `CurrentSense` gives every part
the water's velocity over the ground in its own frame. With both off, no grid is built, no port is added and no
checkpoint byte changes.

## The branch

Cut `smell-plume` from main after `phase1-overpower` has merged:
`git worktree add scratch/wt-smell -b smell-plume main`, from the repository root. Never `cd` into the worktree. Use
absolute paths, `git -C`, and absolute project paths for `dotnet`.

`git diff main...phase1-overpower --stat` shows that phase 1 touches five of this build's files:

- `RunConfig.cs` gains `AnimalHealthScale`, `PlantHealthScale` and `DeathRowIncome`.
- `WorldState.cs` gains config-gated blocks in `WriteOrganism` and `WriteLineage`.
- `World.cs`, `EnvBinding.cs` and `Report.cs` change.
- Both Core fixtures are re-hashed; `r42-config.json` is at `067cb724c6ec17f6`.

Cutting after the merge forwards every fixture and config once, from phase 1's format.

**The anchoring build (`logbook/specs/anchor-brain-build.md`) claims the same places.** It takes `SensorChannel` 17
and 18, appends to `SensorPool()` after the leaf smell, and edits `CreatureSenses` and the kernel's `SensorRead`. This
build takes **19, 20 and 21** whichever lands first. A port id hashes the enum's integer (`BrainIds.Port`,
`CentralBrain.cs` line 201), so a number is fixed for good once a genome is recorded with it. Rebase onto the anchoring
branch if it has merged.

## The tunables

Each is a `[Tunable("sense")]` property on `RunConfig`, after `LeafScentHalfScaleSquareMetres` (about line 4512).
`ConfigSchema` derives `Hash()` and `RunConfigJson` from the attribute, so `RunConfigTests` and `RunConfigJsonTests`
find the properties by reflection and must pass unchanged. A setter that changes the pool clears `_sensorPool`.

| Property | Type, default | Unit | EnvBinding name | Rule |
|---|---|---|---|---|
| `LeafPlume` | `LeafPlumeForm` {`None`, `Strength`, `Gradient`}, `None` | | `EVOSIM_LEAF_PLUME` | Clears the pool |
| `LeafPlumeCellMetres` | float, 5 | m | `EVOSIM_LEAF_PLUME_CELL` | `GridField` refuses a cell that does not divide the box |
| `LeafPlumeLifetimeSeconds` | float, 3000 | s | `EVOSIM_LEAF_PLUME_LIFE` | Finite and above 0 (setter throws, as `EnergyHalfScaleSeconds`) |
| `LeafPlumeHalfScale` | float, 1 | m2 s/m3 | `EVOSIM_LEAF_PLUME_HALF` | Finite and above 0 |
| `CurrentSense` | bool, false | | `EVOSIM_CURRENT_SENSE` | Clears the pool |
| `CurrentSenseHalfSpeedMetresPerSecond` | float, 0.01 | m/s | `EVOSIM_CURRENT_SENSE_HALF` | Finite and above 0 |

The plume mixes at `MatterMixingDiffusivity` on every axis and has no mixing knob of its own.

- The enum sits beside `IslandFarSmell` (about line 4853).
- `EnvBinding.cs` takes the names beside `EVOSIM_LEAF_SCENT_*` (line 601), the fields on the settings object (about
  line 2183) and the copy at line 1041.
- The two half-scales go into `SolverConfig`, beside `LeafScentHalfScaleSquareMetres` (lines 334 and 433).

## The plume grid

Add `src/Evosim.Core/Environment/LeafPlumeField.cs`, which owns a `GridField` and a gradient table. `World` builds it
beside `Matter` (`World.cs` line 2069), with the same arguments except sink 0, refuge 0 and the plume's cell size. It is
built only with the switch on, and is `World.LeafPlume` (null otherwise).

`GridField` already has what the design needs:

- cubic cells, and the mask that kills cells under the bed and in coral;
- joined seams (`WrapAxis`);
- conservative `Mix`, and `Advect` from the current's potential;
- a `CellAt` that clamps a point in a dead cell to the nearest live one (`BoxCellAt`), so a deposit never lands in rock.

Expose that as `internal int CellIndexAt(Float3)` for the reads. The plume never calls `Settle`.

**The step**, `StepLeafPlume(seconds)` in `World.Step` directly after `Matter.Advect` (line 3760):

1. **Deposit.** Each leaf source gets `DepositExact(new FieldPoint(p, grid.PatchOf(x, z)), (double)area * seconds)`.
   The sources are this step's `_leafSources`. Where `LeafScent == null`, `FillLeafScent` never gathered them, so call
   `GatherLeafSources()` here. The sum goes to `LeafPlumeEmittedTotal`.
2. **Mix** with `grid.Mix(seconds, Config.MatterMixingDiffusivity, Config.MatterMixingDiffusivity)`.
3. **Advect** with `grid.Advect(Config.Current, ElapsedSeconds, seconds, grid.PatchWidthMetres)`. Never offer this grid
   a transport device. `Program.cs` line 745 offers one to `Nutrients` alone.
4. **Decay.** A new `internal double Decay(double factor)` on `GridField` keeps `_stock[i] * factor` in each cell. It
   sums what it removed, subtracts that from `_total` and returns it to `LeafPlumeDecayedTotal`. The factor is
   `Math.Exp(-seconds / LeafPlumeLifetimeSeconds)`.
5. **Gradient table.** Three doubles a cell hold the density's slope per metre on each world axis; the density is stock
   over `CellVolume`.
   - Both neighbours live: the central difference.
   - One live: the one-sided difference against the cell.
   - Neither: 0.
   - x and z wrap at the seams.
   - `iy` grows downward, so the y slope is minus the difference along `iy`.

A part reads its cell's density and its cell's gradient, with no interpolation, since the design calls the gradient
"constant across a 5 m cell" (open question 3).

**Refusals**, beside `ValidateIslandFarSmell` (line 3233):

- the plume needs `FieldModel` Grid and shared space;
- the plume is refused in a tank (open question 4);
- `IslandFarSmell.Leaves` is refused with the plume, the message naming D270.

**The checkpoint.** After `WriteField(w, Matter)` (`WorldState.cs` line 642), with the plume on only:
`StateIo.Tag(w, "PLUM")`, the grid's `WriteState`, then the two totals. The reader asks the same config and then
rebuilds the gradient table. This is D212's and D264's pattern of config-gated bytes. `World.StateVersion` stays 20 and
`Checkpoint.Version` stays 6, so this is not a format bump. A write at an older layout with the plume on is refused, as
the leaf smell's is (line 303).

**The dump.** `Row.DumpFields` (`Row.cs` line 1568) writes `NNNNNNNNN.plume.f32` (every cell) and a `plume` entry in
`layout.json`, only with the plume on. The founding bench sets the half-scale from these files. `NonFiniteProbe` scans
the grid.

## The new sensor channels

Append `LeafPlume = 19`, `LeafPlumeGradient = 20` and `GroundCurrent = 21` to `SensorChannel` (`NeuronInput.cs` line
263).

- `IndexCount` is 3 for the gradient and the current.
- `IsSmell` adds the two plume channels. In a nose world, `CentralWiring.IndicesOn` then gives them to noses alone, and
  `CreatureSenses.Read` zeroes them elsewhere (D235).
- `GroundCurrent` is no smell, so every part carries it.
- `Brain.MaskReads` is a 32-bit shift, so bits 19 to 21 are free.

**The pool.** `SensorPool()` (line 4628) appends, after the leaf smell and after the anchoring pair if present,
`LeafPlume` under either form, `LeafPlumeGradient` under `Gradient`, and `GroundCurrent` under `CurrentSense`. That is
four new inputs a part in the realistic form and seven in the overpowered one, as the design counts. Add the switches
to the early return and to `extra`. Line 4672 writes `pool[at]` without moving `at`, so make it `pool[at++]`.

**There is no genome format bump.** I checked the design's claim against the code:

- a central brain stores links by port id (`GenomeJson.cs` line 697);
- a per-node neuron stores its channel by name (lines 751 and 769);
- a new enum value changes no stored id.

**Founders (D147)** are wired from `CentralWiring.Ports(body, pool)`, every port to every output, and mutation adds
links from the same list. So the new ports need no wiring code, and they appear only with their switch on.

**Other uses:**

- `NoseGraft` reframes `LeafScentDirection` links (lines 261 and 268). An `IsDirectionalSmell()` helper there covers
  `LeafPlumeGradient` too.
- The channel names `plume`, `plumegrad` and `current` go in `Report.cs` line 862, `Nursery/FrameRecorder.cs` line 587
  and `TankWatcher.cs` line 381.

## The readings, held once a metabolic step on the host

`CreatureSenses` gains:

- a `LeafPlume` property, set in `Reconcile.cs` lines 284 and 590 beside `LeafScent`;
- the arrays `_plume[links]`, `_plumeGradient[3 * links]` and `_current[3 * links]`, filled by a new `HoldFar()`;
- `HeldFar(link, …)` beside `HeldSmell`, for the card.

`Read` answers the new channels from the arrays, after the nose gate at line 618. The readings:

- **Strength** is s / (s + k) at the part's cell, and 0 when s ≤ 0.
- **The gradient** is the cell's, turned into the part's frame as `SampleLeaf` turns it
  (`ReflectFrameVector(b, R^T g)`), unit length, times the strength. It is 0 when either is 0.
- **Where:** each part at its centre. In a nose world only noses read the plume, at the tip `ReadNoses` finds, with no
  noise (open question 2).
- **The current** is `local = ReflectFrameVector(b, R^T Water[b])`, read as `local / (|local| + h)`. Its length is
  speed / (speed + h): 0.5 at 0.01 m/s and 0.67 at 0.02 m/s. A mirrored part reads the reflection, as the flow sense
  does (D234).
- **A part that is not finite** reads zeros.

`Simulation.cs` line 429 sniffs in nose worlds only. Widen it so that, with either switch on, every body calls
`HoldFar()` at the same point, serially in list order. `HoldFar` draws no noise. It reads the poses, `Water` and the
plume, and nothing writes those between the metabolic step and the block. `Sniff` and its noise keys are untouched.

The held readings need no checkpoint. `Water` and `RelativeVelocity` are in the body's state (`Creature.State.cs`
lines 90 and 259), so a resumed world holds what the unbroken one did. Where `HeldSniff` is false (a bench), `Sample`
calls `HoldFar` every step.

In a plume nose world, the nose's near cone is the world's `LeafScentRangeMetres` (D267). So reading the 6 m cone
needs a launcher value and no code (open question 1).

## The card

The plume grid stays on the host. The card gets the held readings the way it gets a nose's.

- **`gpu-runner.template`.** Beside `NoseHeld` (line 173) go `PlumeHeld` (4 a link) and `CurrentHeld` (3 a link).
  Each is a block column with its switch on and a one-cell stub without it. Count them in `HeldBytesUp`, and fill them
  in `PackBlock` (line 3021) from `HeldFar`: every link, or noses only in a nose world.
- **`gpu-kernel.template`.** Add `SLeafPlume = 19`, `SLeafPlumeGrad = 20` and `SCurrent = 21` (line 623), and the views
  on `SBrain`. `SensorRead` (line 5526) reads `br.PlumeHeld` and `br.CurrentHeld` directly, never through a local array,
  since each thread already holds 12.7 kB. Add the two plume channels to the nose gate (line 5537).
- **Nothing else.** The card gains copies and no arithmetic. `GpuWorld` refuses nothing new. `Kernels.*.g.cs` are
  regenerated, never edited.

## Tests

**Core: `LeafPlumeFieldTests`, new.**

- Without decay, the passes in the phase-1 ocean hold the stock equal to what was emitted, to 1e-12 relative.
- With decay, the stock equals emitted minus decayed.
- A uniform plume stays uniform for 600 s (`DensityCoefficientOfVariation`).
- One decay step multiplies each cell by exp(−dt/τ) to the bit, and 3,000 s leaves e⁻¹ to 1e-6.
- No dead cell holds stock, and a deposit under the bed lands above it.
- A steady point source, after 1,500 s, reads at least five times the density 20 m downstream that it reads 20 m
  upstream, along its local current.
- A ramp gives its slope inside and one-sided against the bed, with the y sign pinned.
- A source at x = 299 m reaches the cells at x = 1 m.

**Core: config.**

- With every switch off, `SensorPool()` returns `SensorChannels.DefaultPool` itself.
- The pool's order under each form.
- The setters refuse values at or below 0.
- The disc with the plume is refused, and so is the plume off a grid or in a tank.

**Dynamics: `PlumeSenseTests`.**

- The current turns with the part.
- A mirrored part negates the normal component (the pattern is in `MirroredDriveTests`).
- The current's length at 0.01 and 0.02 m/s.
- A nose world gives plume ports to noses alone, and the current to every part.
- The readings hold through a block.
- With both switches off, the ports and readings are unchanged.

**Farm.**

- The six names reach the config.
- The header tokens (`plume <form> cell 5 m life 3000 s half k`, `current-sense half 0.01 m/s`) and the dump appear
  only with the switch on.
- A plume-and-current world resumed from a checkpoint steps as the unbroken run did, by the pattern of `NoseFarmTests`'
  `ANoseWorldResumesFromACheckpointAsItRanOn`.

**Card, on the CPU accelerator.**

- `TheFarSmellOnTheCpuAcceleratorIsTheSolver`, by the pattern of `GpuEngineTests` line 1577, with and without the
  nose.
- A world with both switches off holds stubs and uploads no held bytes.

**The replay gate is the coordinator's.** The builder drafts `rounds/identity-d270.ps1`, modelled on `phase1-overpower`'s
`rounds/identity-r64.ps1`.

- **Switches off.** One card group at group 32 founds r64's D and E (`rounds/env-r64-d.ps1` and `env-r64-e.ps1`, seed
  6202) for 600 s. `scripts/reads/compare-run-rows.py` must match each row for row with the prefix of
  `runs/r64-d-6202` and `runs/r64-e-6202`.
- **Switches on.** R and O on seed 6202 run at group 32 and at group 64, and `scripts/reads/gpu-accept.py` must exit 0.

## Fixtures and configs to bring forward

- **`fixtures/r42-config.json` and `r57-s2-config.json`.** The six keys go in at their defaults and nothing else
  changes. `configHash` becomes the build's own `Hash()`.
- **`rounds/nursery/configs/*.json`.** Each is forwarded by `python -I scripts/nursery-config-forward.py OLD <new r42>
  OUT --hash H`, and the nose bench's by `nursery-config-forward-nose.py`. Each new file is named by its new hash and
  sits beside the old one. Never overwrite or delete an old config, and never touch `rounds/nursery/activity.jsonl`.
  List the recipe lines that name an old config; repointing them is the coordinator's.
- **The Dynamics crowd `runs/cefix-s4` (`RunFixture.cs`).** Report its `RunFixture.Why`. Re-recording it takes a farm
  run, which is the coordinator's.
- **`inocula/`** does not move.

## The load cap

- **Pin the shell before any build or test:** `$p = Get-Process -Id $PID; $p.ProcessorAffinity = 16711680;
  $p.PriorityClass = 'BelowNormal'`. That mask is logical 16 to 23, both lanes; `'{0:X}' -f` reads `FF0000`.
- **Build** with Unity's `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe`:
  `build <abs csproj> -c Release -m:1 -nr:false -p:UseSharedCompilation=false`, then `dotnet build-server shutdown`.
- **Test** filtered only, one class at a time. Use `scratch/wt-smell/scripts/card/core-offcard-test.ps1 -Project
  Core|Farm -Filter <class> -Mask 16711680`, or `dotnet test --no-build -m:1` with a filter that excludes
  `Category=Slow`.
- **Never run** a farm run, a card run or card test, `accelerator-batch.ps1`, Unity, or `core-test.ps1 -All`.
- **Write** only in `scratch/wt-smell/`, with notes and helper scripts in `scratch/smell-plume-build/`. Never write in
  the session scratchpad or TEMP.
- **Commit** on `smell-plume` with explicit pathspecs. Never push.
- **The task ends** when the edits and the fast filtered tests are in.

## Steps, each ending in a stop and a report

1. **Tunables, enum, pool and refusals, with the fixtures re-hashed.** Tests: `RunConfigTests`, `RunConfigJsonTests`,
   and the config tests. Report the two hashes.
2. **The grid, `Decay`, `CellIndexAt`, the step and the checkpoint block.** Tests: `LeafPlumeFieldTests`,
   `GridFieldTests` and `ScentFieldTests`. Report the downstream-to-upstream ratio and the uniform CV.
3. **The senses, `NoseGraft`, `Reconcile` and the hold in `Simulation`.** Tests: `PlumeSenseTests`, `NoseSenseTests`
   and `NoseGraftTests`.
4. **EnvBinding, the header, the dump and the names.** Tests: `EnvBindingTests`, `ReportTests`, `NoseFarmTests`,
   `CheckpointTests` and the new resume test.
5. **The card's templates and test, written but not run.** The coordinator runs `accelerator-batch.ps1` in the
   background, which regenerates the kernels and runs the Gpu tests off the card. The regenerated files are then
   committed.
6. **The configs forwarded and the identity launcher drafted.** Report the new files, the recipe lines and
   `RunFixture.Why`.

## Size

About 10 agent hours:

| Steps | Agent hours |
|---|---|
| 1 and 2 | 4 |
| 3 | 2 |
| 4 | 1.5 |
| 5 | 2 |
| 6 | 1 |

The builder uses 1 to 2 hours of machine time on 16 to 23. After it, the coordinator runs the accelerator batch (up to
an hour), `-All` (about 30 minutes, with nothing beside it) and two card sittings of under an hour each.

## The coordinator's answers to the open questions (2026-10-31)

These are dials within D270, so they are the coordinator's to settle. The builder follows them and decides nothing
else alone.

1. **The near cone in a plume nose world.** The launcher sets `LeafScentRangeMetres` to 6. A range is a dial, and the
   code does not hard-code it.
2. **A nose's plume reading** is taken at the tip, with no D235 noise. Noise is a later dial-back (D262).
3. **A part reads the strength interpolated trilinearly** between cell centres, not its cell's value. A step in strength
   at every 5 m boundary would hand a body a false gradient as it crosses, and the overpowered start wants a clean
   signal. The gradient arm keeps the per-cell table and interpolates it the same way. A test pins continuity across a
   boundary.
4. **Tanks are refused** for now, cleanly, with a message naming D270. The nursery's tank comes later, if the screen
   asks for it.
5. **Enum numbers 19, 20 and 21** go to the plume and the current, and 17 and 18 stay with anchoring. The two builds run
   on separate branches and anchoring lands first.
6. **The replay gate founds r64's D and E** for 600 s with every switch off, and compares row for row. No run directory
   is edited.
