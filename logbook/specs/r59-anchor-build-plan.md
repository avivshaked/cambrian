# Round 59's anchoring cell and turning drag: the build plan

*2026-10-12, by the speed agent (evolution-simulator-08), for the coordinator. It plans the build of D161 and D162,
step 1 of D164's path to grazers, from `logbook/specs/anchoring-cell-design.md`. Nothing in it is built. Branch
`r59-anchor` off `r58-runner` at `63131090`, in `scratch/wt-r59-anchor`. File and line references are read from that
checkout by a read-only survey of the same day. The times are guesses.*

## What holds for every step

- Every change sits behind a setting that is off by default, and a world with it off steps exactly as today. Each step's
  tests include that: a recorded world's digest, or a hash pinned before the change, is unchanged with the setting off.
- **The cell enters a world through its config's cell-type table.** Every `config.json` carries the table
  (`RunConfig.CellTypes`, written by `CellTypeJson`), and budding draws uniformly over it (`Mutator.Bud`,
  `Mutator.cs:452`). Appending the anchoring cell to the standard table would shift every draw in every world. So the
  table keeps its seven types, and a new setting appends the eighth when it is on. An old config reads its own table and
  draws as it did.
- Adding any tunable makes every older `config.json` unreadable by the new build, as every earlier round. The fixtures
  that pin a config are re-recorded once, in step 6, not at each step.
- Each step ends at a commit with its fast filtered tests green. `core-test.ps1 -All` (about 27 minutes) runs before the
  commits of steps 1, 2 and 4, which change how a world steps when their settings are on.
- Any new double maths in a card kernel goes through the guarded forms, and every product the card must reproduce is
  written as `mul.rn` (`src/Evosim.Farm.Gpu/CLAUDE.md`).

## The order, and why it differs from the brief's

The brief lists the steps as Core's cell, grip, sense, drag, checkpoint, settings, card and tests. I propose the drag
first and the touch record before the cell. The drag stands alone and is the smallest change, and it is the one the
nursery's first check needs (D161: that check sets the drag's size before the grip's bench). The grip needs the per-part
touch record, which the sense also reads. The settings work is spread through the steps: each step adds its own
tunables, and step 6 does the plumbing every tunable needs once. The coordinator decides the order.

## The steps

### 1. The drag on turning (D161)

**What.** A drag on each part in the water that opposes its turning. D161 leaves the form to the nursery check, so the
build offers two forms behind one mode setting, each with its own coefficient:

- *skin*: a shear drag on each panel's velocity along its surface, in the panel loop that already carries the pressure
  drag. It brakes turning and also some sliding.
- *spin*: a torque on each link against its own angular velocity, quadratic in the spin rate and scaled by the link's
  size, with no effect on its translation.

Mode off is today's water. Both forms feed the drag ledger (`Creature.Ledger.cs:133`), so the energy audit closes, and
both pass through the existing drag limiter.

**Files.**
- `src/Evosim.Dynamics/Fluid.cs` (the panel loop, 90-109, and the limiter, 120-147)
- `SolverConfig.cs`
- `src/Evosim.Core/RunConfig.cs` (a `fluid` group)
- `src/Evosim.Dynamics.Tests` (a new `TurningDragTests.cs`)

**Tests prove:**
- with the mode off, a recorded world's digest is unchanged;
- a free spinning body's spin decays at the rate the form predicts, within the step's error;
- the energy audit closes on a spinning body;
- the same world is identical at 1 and 16 threads;
- a jointed swimmer's stroke still drives it forward with the drag on, measured against the drag off. This last is a
  sanity check only; the nursery check sets the size.

**Time:** about 0.75 days. **Machine:** CPU, suites on logical 16-31; one `-All`.

### 2. The record of touching the bed or a reef, and its sense

**What.** Today only a per-body flag records touching the bed, the reefs or the glass, all three together
(`Creature.TouchedBedOrGlass`), and only under the contact instrument. The touch record adds a per-link flag set in the
per-part contact's bed and reef blocks (`Contacts.cs` 1076-1121) and not in the glass block. The contact functions also
return their contact point and normal (`ContactBed.Push`, `ContactReef.Push`), which the grip needs.

A new sense channel, `BedTouch = 14`, reads the flag from the last physics step. It is gated by its own setting,
appended at the end of `SensorPool()`'s order (`RunConfig.cs:3403-3443`) so that the pool is unchanged with it off, and
named in the report's senses token.

The grip and the sense need per-part contact (`ContactPerPart`). A world that switches either on without it is refused
at load, with the reason.

**Files.**
- `src/Evosim.Dynamics/Contacts.cs`, `ContactBed.cs`, `ContactReef.cs`, `CreatureContacts.cs` and `CreatureSenses.cs`
  (308-366)
- `src/Evosim.Core/Genome/NeuronInput.cs`, `Brain/SensorChannels.cs`, `Brain/CentralWiring.cs`
- `RunConfig.cs` (a `sense` flag)
- `src/Evosim.Farm/Report.cs` (580-612) and `Metabolise.cs` (the hand-over, 383-393)
- the nursery's name tables (`FrameRecorder.cs`, `TankWatcher.cs`, `Program.cs`)

**Tests prove:**
- with the setting off, the pool, a recorded digest and every recorded genome's sensor reading are unchanged;
- a part resting on the bed reads 1, and so does one resting on a reef cap;
- a part in mid-water reads 0, and so does one pressed against the glass;
- the reading lags one physics step, as the design says;
- a part near a reef reads 1 from the moment its bounding sphere meets the rock. This answers the design's question on
  early contact with a number: how far from the surface a part is when it first reads 1.

**Time:** about 0.75 days. **Machine:** CPU, 16-31; one `-All`.

### 2b. The leaf sense (D167)

*Added on 2026-10-12, after D166 put swimming grazers first. It runs after step 2 and before the cell. The swimming
nursery needs it and the turning drag, both on the CPU.*

**What.** Living photosynthetic bodies give off a smell of their own, kept apart from the carrion smell and read by a
sense of its own.

- **The field.** A second grid beside `ScentField`, filled once a metabolic step from the living bodies, as the carrion
  field is filled from the corpses. Each source adds a cone, as a corpse does. A leaf emits in proportion to its living
  lit area: the lit area of its photosynthetic parts, as the light pass charges it. It emits while it lives and stops
  at death. Its corpse then smells only as carrion does. The source is the body's own point (`Organism.Point`), the one
  position Core holds for a living body.
- **The grid.** `ScentField` gains a cell size and the leaf field takes its own. The carrion field keeps its 2 m cells
  and its bits, which a test pins. The card's carrion kernel is untouched.
- **The two forms.** One setting, `LeafScent` (`none`, `strength`, `direction`), as the turning drag's mode is.
  - *Strength*: channel `LeafScent = 15`, one number per part, squashed as `x / (x + k)`, as Scent is read.
  - *Direction*: channel `LeafScentDirection = 16`, three numbers per part in the part's own frame, as Flow is. The
    vector is the field's gradient at the part, made unit length, times the strength form's value. Where the gradient
    is zero the vector is zero.
  - The pool appends whichever channel the setting names, after `BedTouch`, so a world with it off draws as before.
- **The numbers**, all tunables the nursery sets: the cone's range, the grid's cell size and the squash's half-scale.
  The emission per square metre is fixed at 1, so the field reads in square metres of leaf, cone-weighted. A second
  emission number would be redundant with the half-scale, since the squash reads only their ratio. If you want the
  emission as its own tunable anyway, it is a line.
- **Refusals.** The card refuses the setting until a port, which the nursery does not need.

**Files.**
- `src/Evosim.Core/Environment/ScentField.cs` (the cell size, and a gradient read beside `At`)
- `src/Evosim.Core/Ecosystem/World.cs` (the field, its fill after the metabolic pass, the restore's refill, the
  refusal) and `Organism.cs` (the living photosynthetic lit area, cached at birth and at each growth step)
- `src/Evosim.Core/Genome/NeuronInput.cs`, `Brain/SensorChannels.cs` (the direction's three indices), `RunConfig.cs`
- `src/Evosim.Dynamics/CreatureSenses.cs` (both reads), `src/Evosim.Farm/Metabolise.cs` (the field's hand-over, as
  `Scent`'s), `EnvBinding.cs`, `Report.cs`, `src/Evosim.Farm.Gpu/GpuWorld.cs`
- the nursery's name tables (`TankWatcher.cs`, `FrameRecorder.cs`)

**Tests prove:**
- with the setting off, the pool and the carrion field's cells are unchanged, the latter bit for bit on a fixed set of
  corpses;
- a living leaf adds a cone whose peak is its photosynthetic lit area. A dead leaf adds nothing, and its corpse enters
  the carrion field only. A body with no photosynthetic tissue adds nothing. A body with some adds by that tissue's area
  alone;
- the strength form reads the squash of the field at each part;
- the direction form points at a lone leaf in the part's own frame and turns with the part. Its length is the
  strength form's value;
- the fill is the same at 1 and 16 threads;
- a world resumed from a checkpoint reads the same field as the world that ran on (the carrion field needed a
  rule for this, `logbook/specs/r57-build/scent-restore.md`);
- a timed fill with 6,000 leaves, reported and not asserted.

**Cost.** The fill costs the leaves times the cells a cone covers. On 2 m cells a 10 m cone covers about 520 cells, so
6,000 leaves are about 3.1 million additions a fill. That is an estimate of 30 to 60 ms on one thread, and 3 to 6 ms on
the farm's threads, twice a simulated second. A 5 m cone covers about 65 cells, eight times fewer. Halving the cell size
multiplies the cost by eight. The timed test replaces these estimates with a number. Reading the field costs what
reading Scent costs: one interpolation per part per physics step, and a second for the gradient in the direction form.

**Should the cone's reach scale with the emission?** I recommend not: a fixed range, as carrion's, with the cone's
height scaled by the leaf's area. *Inference*, from `logbook/specs/leaf-scent-lit/pass2.md` (Reach):
- The literature's point is that one small leaf is smellable at centimetres and a dense bed at metres, because the
  concentrations of many leaves add. Cones of a fixed range add the same way. The distance at which a sum of cones
  crosses the sense's half-scale grows with the area behind it, up to the range.
- Cones whose range scales with each leaf's own area do not add that way. Beyond its own leaf's reach a cone is 0, so
  a bed of small leaves reaches no farther than its largest leaf. On 2 m cells most leaves' cones would also fall
  inside one cell, and the grid would not see them.
- A fixed range bounds the cost, and the cone is the shape D167 named.

**Time:** about 1.5 days, and 2 with the no-corpse setting below. **Machine:** CPU, 16-31; one `-All`.

**What D167 left open, and how it was settled.** The coordinator settled these five within D167 on 2026-10-12, each
as recommended here. The owner may revisit any of them.
1. *The reach.* A fixed range, as carrion's, with the cone's height scaled by the leaf's area. The range, the cell size
   and the half-scale are tunables, and the emission is fixed at 1 per square metre.
2. *Which area emits.* The photosynthetic lit area the light pass charges, with the silhouette cap (`LitAreaFactor`).
   Irradiance does not enter, so a shaded leaf smells as a lit one does. The evidence ties emission to area and, more
   weakly, to photosynthesis.
3. *Who emits.* Any living body with photosynthetic tissue: leaves, mixotrophs, and links that bought photosynthesis.
   Each emits by its photosynthetic area alone.
4. *Where.* The body's own point, not each part's. Core holds no part positions, and a leaf is a few parts.
5. *The direction form's frame.* Each part's own frame, as Flow is read, rather than the root's.

**The nursery tank's setup**, for your separate brief. Three of its conditions need nothing new: no snow (no influx and
no charged matter at the start), no carrion at the start, and a swimmer with no absorptive or photosynthetic cell.
One needs world code: *a leaf that dies by anything but a bite leaves no corpse* (D167). Its tissue still has to go
somewhere for the energy audit to close. Today it would become a corpse, and in a world where corpses are not objects
it would become charged matter in the water, which is snow.

The coordinator settled the mechanism within D167 on 2026-10-12, and it is built in this step. A body killed other than
by a bite returns its tissue to the water at once as spent units. That keeps the matter budget, feeds the leaves and is
never food. It covers every body in the tank, the swimmers included, since the tank may hold no food but leaves bitten
to death. It sits behind its own setting, off by default, and is on only in the nursery's leaf tank. Its test shows the
audit closing and no corpse appearing. It adds about half a day. Note also that leaves fix light only while the water holds spent units (`Fixation.Fix`), so "no
snow" must not mean no spent units.

### 3. The anchoring cell in Core

**What.**
- An `AnchoringCell` type with its standing cost per cubic metre, like every cell, and its parameters: the tenacity
  (newtons per square metre of the cell's area), the build price (joules per newton of breaking force), the decay
  time, and the reserve rule's lag and threshold.
- Its JSON reader and writer and its hash contribution.
- The setting that appends it to the world's cell-type table, so that it can be budded.
- `Genome.Validate` accepts it. The ledger's own registry (`src/Evosim.Ledger/Program.cs:699`) learns it.

**Files.**
- `src/Evosim.Core/Cells/StandardCellTypes.cs`, `CellTypeRegistry.cs`, `CellType.cs`
- `Serialization/CellTypeJson.cs`
- `Genome/Genome.cs`
- `Genome/Mutator.cs` (only to confirm that `Bud` needs nothing beyond the table)
- `RunConfig.cs`, `src/Evosim.Ledger/Program.cs`

**Tests prove:**
- with the setting off, the table, the config hash and a seed's mutation draws are unchanged;
- with it on, the table has eight types, survives a JSON round trip, and is refused when a field is missing;
- a bud can draw the cell, and the cell's upkeep is charged by volume.

**Time:** about 0.5 days. **Machine:** CPU, 16-31.

### 4. The grip

**What.** In the solver, per link carrying the cell, under per-part contact.

1. **Forming.** It grips at the first physics step on which its touch flag is set by the bed or a reef. It keeps the
   contact point, and its orientation at that step, until it lets go.
2. **Holding.** A spring and a damper pull the link toward its point, and a second pair holds it against turning.
   - Both springs' stiffness comes from the link's mass, as the contact's does (`mass·ω²`, `Contacts.cs:1072`), so they
     stay inside the explicit step's stability.
   - The force is capped like the contact push (`mass·MaxContactSpeedChange/dt`).
   - It enters `Fext` at the link with its torque.
3. **Breaking.** The breaking force is the tenacity times the cell's area, its volume to the power 2/3, times the grip's
   strength. When the spring's demanded force exceeds it, the grip breaks.
4. **Upkeep and weakening.**
   - The strength starts at 1 and decays with the decay time while the grip holds.
   - It falls further as the body's reserve stays below the threshold for longer than the lag, and is 0 when the
     reserve is.
   - The reserve reaches the solver the way the energy sense reads it (`IReserveSource`, `CreatureSenses.cs:106`).
5. **Paying.** Each grip's build price is owed on the step it forms and charged to the body's reserve at the next
   metabolic step, booked in the energy audit. Holding costs nothing by the second, and the cell's standing cost is
   charged with the other cells' (D162).
6. **What does not hold it.** The glass never forms a grip (D162). A link that is lost, or removed by a bite or a module
   drop, loses its grip with it.

**Files.**
- `src/Evosim.Dynamics/Contacts.cs`, a new `Anchor.cs` (the hold, the break, the decay), `Creature.cs`,
  `DynamicsWorld.cs` (the step's order: the grip after the contacts, 339), `SolverConfig.cs`
- `src/Evosim.Core/Ecosystem/Metabolism.cs` (the owed price) and the audit's books
- `src/Evosim.Farm/Metabolise.cs` (the hand-over)

**Tests prove:**
- a gripping link holds in a current that carries an ungripping twin away, and the held body's drift stays bounded;
- the grip breaks above its breaking force and holds below it, at two cell sizes, with the force scaling as volume to
  the power 2/3;
- the build price is charged once per grip, and the audit closes;
- the decay and the reserve rule each lower the breaking force on their schedule, and the grip lets go at zero reserve;
- the glass never grips;
- **stability:** a large body and a long arm held in a current ring down rather than up, and the held body's energy
  never grows, at steps of 0.01 and 0.02 s. This answers the design's first doubt;
- identity at 1 and 16 threads.

**Time:** about 1.5 days. **Machine:** CPU, 16-31; one `-All`.

### 5. The checkpoint fields

**What.**
- Per link in `Creature.WriteState`/`ReadState` (`Creature.State.cs:42-260`): the grip's flag, point, orientation,
  strength, how long the reserve has been under its threshold, and the touch flag.
- Per body in the world's record: the price owed (`WorldState.cs`, `WriteOrganism`).
- `World.StateVersion` goes from 15 to 16, so every older checkpoint is refused rather than misread.
- Any member a step fills before it reads goes on the fidelity check's skip lists (`CheckpointFidelity.cs` 528-557 and
  823-867).
- The grip's state joins the solver's digest only if the coordinator wants it in identity reads. It would change every
  digest row, so it is gated with the setting either way.

**Files.**
- `src/Evosim.Dynamics/Creature.State.cs`
- `src/Evosim.Core/Ecosystem/WorldState.cs`
- `src/Evosim.Farm/Checkpoint.cs` (remarks)
- `CheckpointFidelity.cs`

**Tests prove:**
- `--verify-checkpoint` on a world holding on mid-grip restores every member;
- a resume from a checkpoint taken while bodies hold writes the same rows as the unbroken run;
- a version-15 checkpoint is refused by name.

**Time:** about 0.5 days. **Machine:** CPU, 16-31. The farm runs for the resume test are short, about 10 minutes at 8
threads.

### 6. The settings end to end, and the fixtures

**What.**
- Each new tunable gets its `EVOSIM_` name, settings field and copy (`EnvBinding.cs`; `BedFriction` at 421, 804 and 1468
  is the pattern), its token in the report's header (`Report.cs`), and its place in `SolverConfig.From`.
- The fixtures every new tunable orphans are re-recorded on the new build:
  - `src/Evosim.Core.Tests/fixtures/r42-config.json`;
  - the pinned `Round42ConfigHash` (`EnvBindingTests.cs:61`) and `ReportTests`;
  - the nursery's three world configs and their recipes (`rounds/nursery/configs/`), and the room's golden answers.
- **`RunFixture`'s recorded crowd** (`src/Evosim.Dynamics.Tests/RunFixture.cs:48`, a 20,000 s snapshot of round 57's
  world) needs a fresh farm run on this build, as `r44fix-s4` and `r57fix-s4` were. That run is a machine job of its
  own (see the machine table).

**Tests prove:**
- `RunConfigTests` and `RunConfigJsonTests` pass with every new tunable, so each reaches the hash and survives a round
  trip;
- a launcher's names reach the config;
- every re-pinned fixture is read by the build that wrote it.

**Time:** about 0.75 days, the fixture run aside. **Machine:** CPU, 16-31, and the fixture run.

### 7. The card port

**What.** The drag, the touch flag, the sense and the grip go into `gpu-kernel.template` and `gpu-runner.template`, and
the kernels are regenerated (`EVOSIM_GPU_REGENERATE=1`; `GpuKernelSourceTests` fails until they match):

- the drag in `FluidLinks`;
- the touch flag and the contact point in `AgainstTheWorld` (bed, reef, not glass);
- the grip and its break in `ContactLinks`;
- the sense in `SensorRead`;
- the grip's state carried in the per-body columns and the host mirror (`CreatureMirror.cs`), with the price owed read
  back at each metabolic step;
- the new solver settings copied into the kernels' constants (`GpuWorld.cs:100-142`).

Until each piece matches the CPU, the farm refuses a world that switches it on with the card, as it refuses the bed's
friction (`FarmGpu.cs:38-45`). The refusal comes off piece by piece.

The leaf smell's field stays filled on the host. The card reads the grid the world filled, sent up every block as the
carrion grid is, and its two senses read it there. A fill on the card, as `GpuScent` fills the carrion field, is
deferred. Whether it pays depends on the fill's cost at round 59's world size, re-taken on a quiet machine. The
coordinator ruled on 2026-10-13 that it is decided at the pre-registration.

**Files.**
- `src/Evosim.Farm.Gpu/gpu-kernel.template`, `gpu-runner.template`, the regenerated `Kernels.D.g.cs` and
  `Kernels.F.g.cs`, `GpuWorld.cs`, `CreatureMirror.cs`
- `src/Evosim.Farm/FarmGpu.cs`
- `src/Evosim.Farm.Tests/GpuEngineTests.cs`

**Tests prove:** on ILGPU's CPU accelerator, the card's kernels match the CPU solver bit for bit in double on a world
with every piece on. That covers grips forming, holding and breaking, the touch flags, the sense and the drag, at two
group sizes. The single-precision kernels are then checked on the card itself, in step 8.

**Time:** about 2 days. **Machine:** CPU only, through `scripts/card/accelerator-batch.ps1` under the nvcuda guard
(`CUDA_VISIBLE_DEVICES=-1`, proved unloaded), on 16-31. Nothing on the card in this step.

### 8. The whole suite, and the card acceptance

**What.**
- Every suite on the final commit: Core `-All`, Dynamics, Farm, Nursery, Room.
- A farm smoke of a gripping world on the CPU, identical at 1 and 16 threads, read by `--verify-checkpoint`.
- On the coordinator's yes, the card acceptance: the same world on the card at groups 32 and 64 through `scripts/card/`,
  read by `gpu-accept.py`, with the driver watch and the heat stop. This is the step that shows single precision holds
  the grip without ringing.

**Time:** about 0.75 days of agent time, plus the runs. **Machine:** CPU 16-31 for the suites; the card for about half an
hour to an hour, alone, on the coordinator's yes.

**The CPU half, done on 2026-10-13 at `c997720c`**, on logical 16-31. The card half waits for a daytime window the
coordinator arranges.

Of the suites, Core `-All` passed 1,174 of 1,174 in 25.5 minutes. Dynamics passed 206 of 206, Farm 316 of 316 (the grip's
checkpoint tests among them), and Nursery 213, with one swim test skipped by design. Room passed 676 of 686, with four
skipped by design. Its six browser flows failed: two on Edge 155's hand-off and four on the
profile folder its strays keep locked. That is the machine, and the fix waits on branch `room-cap`.

The smoke took three pairs of farm runs, because round 57's world does not grip. The tools are
`r59-build/smoke.ps1`, `smoke-compare.ps1` and `ckpt-grip.py`. The last reads a checkpoint's inflated state, the
world's grip joules and each body's grip record.

- *Round 57's world with every round-59 piece on* (`r59smoke-t1` and `-t16`): seed 1, founded and run to 2,000 s, with
  the anchoring cell's bud weight at 20. The two arms are the same run: all 200 rows, all 401 digests, and the states
  at 1,000 s and 2,000 s, byte for byte. `--verify-checkpoint` passes at 2,000 s. The pair checks the drag, the touch
  and the leaf smell, and not the grip. At 2,000 s, 87 bodies carry the cell and none has ever gripped, and 2 of
  4,566 links touch the rock. The world's bodies keep to midwater.
- *The same world with tissue 500 kg/m3 over the water* (`r59smoke-sink-t16`, 1,000 s): no body carried the cell at
  500 s. The crowd fell to 65 by 840 s, so its 1-thread arm was not run.
- *The gripping world of `GripCheckpointTests`* (`r59smoke-grip-t1` and `-t16`). No setting gives a founder a cell
  type, so its `config.json`, written by that test with two founders in three anchoring, is the run's config.
  `--verify-checkpoint` founded it and stepped it 30 s at 16 threads, and both arms resumed from the checkpoint that
  wrote, to 600 s. That checkpoint carries no build hashes, so the resumes took `-AllowSourceMismatch` and are marked
  cousins. All 57 rows, all 57 digests and the state at every 100 s are the same at 1 and 16 threads.
  `--verify-checkpoint` passes at 100 s, with ten grips held, and at 600 s. Two refused resume attempts left a run
  directory each under the two arms.

The gripping pair's checkpoints are below. The tallies are summed over the bodies alive at that second; the joules are the world's
running total paid for grips.

| Second | Bodies with the cell | Grips held | Formed | Broken | Grip joules paid |
|---|---|---|---|---|---|
| 100 | 13 | 10 | 14 | 4 | 15.58 |
| 200 | 5 | 5 | 16 | 11 | 60.36 |
| 300 | 1 | 1 | 9 | 8 | 90.37 |
| 400 to 600 | 0 | 0 | 0 | 0 | 97.41 |

The anchoring line dies out before 400 s, so the grip is checked over the first 300 s.

The table's tallies are each living body's count over its own life. They overlap from row to row and miss the dead.
So they do not count the grips the run formed, and the joules cannot be divided by them. One grip costs the build price
times the breaking force. That is 5e-5 J/N times 58,000 N/m2 times the part's volume to the 2/3, or 2.9 J times
V^(2/3) with V in cubic metres. The joules are that price alone: the cell's upkeep is charged in the metabolic pass and
is not in them. The plan's placeholder of about 0.01 J is a 6 cm bud, 2.2e-4 m3. This world's founders are drawn with
half-extents of 0.15 to 0.4 m, so an anchoring part a founder carries is far larger. *Inference*, taking such a part as
a box and without reading the run's own volumes: one grip costs about 0.26 J at 0.3 m across and 1.9 J at 0.8 m. The
97.41 J paid is then of the order of 50 to 370 grips, re-forms included.

**The card half, done on 2026-10-13 at `7e81cbbc`**, with `c997720c`'s farm. That commit adds `-CheckpointEvery` to
`card-run.ps1`, so a card run's grips can be read. It ran alone on the card and on logical 16-31, through `card-run.ps1` with
`-Transport -SampleCores -SampleCpu -HeatStop`. The card runs in single precision; the farm refuses it in double. So the
claim is identity between groups 32 and 64, and against the CPU it is the distributions. Every run's `card-run-check.ps1`
held every condition, with no driver event. The card peaked at 49 C and 98 W, the CPU at 62 C.

- *A gripping tank world.* The smoke's gripping world is a box with rolling currents, which the card does not step. So
  the card took a test world written by hand: `r59smoke-t16`'s own config, with founders drawn from anchoring,
  anchoring, photosynthetic and absorptive, and tissue 50 kg/m3 over the water. `--verify-checkpoint` founded it on the
  CPU and stepped it to 60 s, with 16 grips held; the card runs and the CPU engine's resumed from there for 300 s. Groups
  32 and 64 are identical: `gpu-accept.py` exits 0, and the states at 100, 200, 300 and 360 s are the same bytes.
- *Round 57's world as it is*, every round-59 piece on, resumed from `r59smoke-t16` at 1,000 s for 300 s with about
  3,560 alive. Groups 32 and 64 are identical by the same reads, at 1,100, 1,200 and 1,300 s.

The gripping world against the CPU, the card first and the CPU second:

| Second | Bodies with the cell | Grips held | Formed | Broken | Grip joules paid |
|---|---|---|---|---|---|
| 100 | 24 / 24 | 22 / 22 | 22 / 22 | 0 / 0 | 17.27 / 17.27 |
| 200 | 19 / 19 | 16 / 16 | 16 / 16 | 0 / 0 | 26.58 / 26.60 |
| 300 | 14 / 14 | 10 / 11 | 21 / 14 | 11 / 3 | 35.80 / 30.28 |
| 360 | 14 / 14 | 12 / 10 | 19 / 10 | 7 / 0 | 39.33 / 33.16 |

The two agree to 0.1% for 200 s and then part, as single and double do. Neither rings: tens of grips form and break
in 300 s.

In round 57's world the crowd tracks the CPU (3,561 alive against 3,555 at 1,300 s). The contact instruments do not.
From the first sample after the resume, the card reads about half the CPU's bodies at the bed or the glass a step. At
1,010 s that is 3.0 against 5.9, and its overlap pairs are about half, 6.1 against 13.2. A CPU-engine resume of the same
checkpoint reads the CPU's numbers to the last digit, so the card is the cause and the resume is not. The card fills the
instrument on every step. Round 58 on the card read lower than round 57 on the CPU too, so it predates round 59: at
1,010 s, 0.58 against 4.8, and 3.3 against 10.0. In double on the CPU accelerator the kernels match the solver's
counts, so it lives in single on the card.

The contacts themselves differ, and single precision makes them differ; the card adds nothing. The census, the bed
flag and the overlap test read the same on both engines. The broad phase cannot drop an overlapping pair to rounding, and
the run's overflow and refusal counters are all zero. From the same 1,000 s checkpoint for 10 s, off the card, the card's
kernels on ILGPU's CPU accelerator read:

| Engine | Overlap pairs a step | Bed or glass bodies a step |
|---|---|---|
| The kernels in single, CPU accelerator | 6.08 | 3.14 |
| The card | 6.11 | 3.00 |
| The kernels in double, CPU accelerator | 13.20 | 5.92 |
| The CPU engine | 13.20 | 5.92 |

Double matches the CPU in every count and every position. At 1,010 s the card's bodies sit where the CPU's do, to the
centimetre the positions are written in. The median separation is 0, and the largest is 3 cm. The pairs the card lacks are held ones: 5.8 held a step
against 13.0, with the unheld 0.29 against 0.20.

The kernel advances a body's base position by `basePosition + v * dt` in single (in `Integrate`). A step's move under
half the float spacing is lost. At dt 0.01 that is any speed under about 0.1 mm/s at a coordinate of 16 to 32 m. At 128
to 256 m it is any speed under about 0.8 mm/s. My reading, which no run has yet tested, is that a contact's rest point is
finer than that. At round 57's 0.02 kg/m3 it is 0.5 µm deep. A body easing into it stalls just outside, so it is in
contact on fewer steps while its mean push and its path stay the CPU's.

The bite reads each metabolic step's overlap list (`HandOverContacts`), so the same holds for a bite's chance. Round
57's world eats nothing; round 58's, all on the card, does. Round 58's contact columns and its bite chances were taken in
single, so they are not comparable with a CPU round's, as D157's second term already requires. A light body resting on the rock on the card would meet the
same stall. The tank fixture's light bodies never came to rest in either precision, so that has not been seen. The step-8
tank world agreed because its tissue is 50 kg/m3, a rest depth of 1.2 mm.

The branch `r59-pos64` tested that reading, and then the contact test. Its record is `r59-pos64-plan.md`. Three
changes, in single only, cure most of the shortfall:

- The base position is a compensated sum, so a slow body moves as on the CPU.
- The contact geometry is in a local frame. Each link's position keeps the rest its roundings left out, and every
  contact difference is taken from the parts.
- The contact's floor and glass come from the nearest point of a 0.25 m lattice, which a float holds without
  rounding, plus a small-argument rise from there.

The same 10 s then read 12.33 pairs a step (11.58 held, 0.75 unheld) and 5.45 bed-or-glass bodies. Double reads
13.20, 13.00, 0.20 and 5.92, and no body is over 1 cm from it. Double keeps the solver's arithmetic. The owner
approved the three as one change on 2026-10-13 (D168), to land if the card showed it cost under about 5% of the pace.
One window alone on the card read a cost of about 1% at groups 32 and 64. The contact kernel went from 59 registers to
62, and the slow bodies drifted on the card as on the CPU. The branch is merged here (7af3193b).

## The machine, in one table

| Step | CPU | The card |
|---|---|---|
| 1-6 | Filtered suites on logical 16-31, seconds to minutes; `-All` about 27 min at steps 1, 2 and 4 | None |
| 5 | Two short farm runs for the resume test, about 10 min at 8 threads | None |
| 6 | `RunFixture`'s fresh crowd: a farm run of round 57's world to 20,000 s on this build. My guess is a few hours at 16 threads, unchecked against the precedent runs; it goes in the machine queue as its own job | None |
| 7 | The accelerator batch under the nvcuda guard, on 16-31, tens of minutes a pass | None |
| 8 | Every suite, about 40 min; the CPU smoke, about 10 min | About 30 to 60 min, alone, on the coordinator's yes |

A line goes to the coordinator at each start and end, nothing runs beside a card job, and suites stay on 16-31.

## Time

About 7.5 days of agent work in all. The design's guess was a week. The fixture crowd's run and the card session are on
top of that, and each waits for a slot in the queue.

## What the design leaves to the owner

These are world rules, or choices about what the world is, that D161, D162 and D164 do not settle. They come to the
coordinator and stay out of the code until ruled. Where I note a default, it is only what the code would do if nothing
were said, and I propose none of them.

1. **How the cell enters genomes.** Ruled by the owner in D165: by budding only, at a bud weight of its own.
   - Only by budding, or also in founders? Founders draw from fixed lists (`GenomeFactory.cs:130-139, 373-376`), which
     today do not include the buoyancy cell either.
   - Budding draws uniformly over the table, so an eighth type takes one bud in seven from the others in every world
     that has it, and lowers every other type's share. The alternative is a weight for it.
   - Round 59 inoculating anchored bodies is a round-design question for later.
2. **Re-gripping after a break.** Ruled by the owner in D165: on the next step, paying again. The design prices rebuilding ("rebuilding is what costs") but does not say whether a
   part may grip again on the next step, after a delay, or only after leaving the rock. The default would be the next
   step, paying the price again.
3. **The drag's form.** D161 gives the form to the nursery check. Whether both forms in step 1 are built, or one is
   chosen now, is the coordinator's call. Building both costs about a quarter of a day.
4. **The drag above the waterline.** A part above y = 0 is in no water. The default would apply no drag there, as the
   panels' pressure drag does today. This is listed only because the world has no top (`src/Evosim.Core/CLAUDE.md`).
5. **What the touch sense counts.** The design says the bed or a reef, so the glass reads 0. Whether a corpse on the
   bed, which is not rock, counts is not said. The default would be no.
6. **The brain's control of the grip** is the design's second step (D162). It is not in this plan. Its output channel
   is a later build.
7. **A grip the body cannot pay for.** The grip forms on its own (D162), and its price is charged at the next metabolic
   step. As built in step 4, a grip forms whatever the reserve holds. A body whose reserve is short of the price is left
   at zero and dies, as a body short of any bill does. At the placeholder price, about 0.01 J for a 6 cm bud, this
   cannot matter. At a price the nursery's bench sets, it might. The alternatives are not to form a grip the reserve
   cannot cover, or to charge what the reserve holds and form the grip anyway. The coordinator raised it on 2026-10-13;
   it is the owner's to rule, and the code stays as it is until then.

## Outside this plan

- The nursery's bench: its two arms, the episode measure of seconds held, and the leaf arm's reefs in the nursery's
  tank. The design puts them with the nursery, after this build. If the coordinator wants the seconds-held measure built
  here, it is about a quarter of a day in `src/Evosim.Nursery`.
- The theatre's rendering of the new cell and of a held body. The theatre compiles Core, so it builds, but it does not
  yet draw the cell's own colour.
- Any number. Every price, tenacity, time and coefficient is set by the nursery's checks before round 59's
  pre-registration (D161, D162).
