# The GPU port proper: the `gpu` engine behind the farm

*Fable, 2026-09-23 afternoon, after spike 4 proved the whole body phase as one kernel exact
against the solver (logbook/0115). D105 ruled the port (single precision, designed for
100,000, validated at 10,000 to 30,000 with the grid on the CPU). This is what to build so
that a farm run steps on the card, with the design of `gpu-full-step-spec.md` behind it and
the spike's kernel (`spikes/02-gpu-featherstone/spike4/`) as the transcription to carry
over. Acceptance item 1 of the design is this spec's first acceptance.*

## 1. The seam

`Evosim.Farm.Simulation` holds one `DynamicsWorld` and calls `Dynamics.Step()` once per
physics step, fifty times per metabolic step; between blocks the farm reads the bodies'
arrays (`Creature.Position`, `Rotation`, `Velocity`, the root, the centre of mass, the settle
drains, the limiter drains, `Overlaps` and `NearestParts`, the senses' contact and damage
hand-back, the trace on a loss) and writes to them (births through `Add`, deaths, growth's
resize in place, a module addition's new links, the senses' inputs). Nothing reads the
arrays inside a block. So the port is a **backend inside `DynamicsWorld`**, chosen at
construction, with the `Creature` objects on the host as the canonical mirror that the farm
keeps reading and writing exactly as it does today:

- `DynamicsWorld.Step()` on the GPU backend does not step. It counts, and at the block's
  boundary (the metabolic step; `Simulation` tells the world its block length, fifty at
  dt 0.01 and twenty-five at 0.02) it runs the whole block on the card and reads back.
  Simpler: `Simulation` calls `Dynamics.StepBlock(n)` on both backends, and the CPU backend
  loops `n` times; one call site, one shape.
- Before a block goes up, the backend uploads what changed since the last one: every slot
  whose body was born, resized, given links or lost (its constants: the panels, masses,
  inertias, joint geometry, the brain, the senses' masks; its state from the mirror), the
  snow stock array and the reserve seconds, the contact and damage hand-back, and the
  fifty instants of the current. Everything else stays on the card.
- After the block comes down: positions, rotations, velocities and spins for every link,
  the joint coordinates and rates, the brain state (so a checkpoint is the same file), the
  four settle sums, the two limiter counts, the lost flags with the trace ring for a lost
  body, the overlap lists with their overflow counts, the two sphere buffers, and the
  contact census counts. The mirror is then what the CPU backend would have left, which is
  what makes every farm read after the block unchanged.

## 2. Slots, classes and capacities

A slot is a body's index in the card's arrays. A death frees it; a birth takes the lowest
free one; the kernel skips an inactive slot. Slots are allocated in size classes as the
design says: a body is placed in the smallest class whose link and neuron ceilings hold it,
one launch per class per step inside the block, and the readback moves each class's arrays
at its own stride (spike 4's readback at the sixteen-link stride was 1.1 to 3.7 ms a block,
the single largest per-block cost). The classes for the first build: links 2, 4, 8, 16;
neurons 8, 32, 128, 256. A body over the top class is refused at `Add` and counted in the
manifest (`refusedForClass`). The candidate and overlap capacities are the spike's (256, 64)
with their overflow counts read back and printed in the footer (`gpu overflow: 0 / 0`); a
nonzero is the first thing a read of a GPU run checks.

## 3. What is refused

The GPU backend refuses at construction a box world, a water hold above 0, the rolls, a
current with a vent, and a dt other than 0.01 or 0.02, each with a message naming the
setting; the spike's host did the same. The Unity farm is untouched.

## 4. The digest and the identity claims

- `EVOSIM_DIGEST_EVERY` on the GPU backend reads back every step it hashes (a validation
  cost) and hashes a lost body's NaNs as one pattern (spike 4: two compilations keep
  different NaN sign bits in lost bodies). The CPU backend's digest gains the same
  canonicalisation, so the two hash the same bytes; the CPU's recorded digests move only on
  runs that lost a body, which is stated in the entry.
- **Acceptance 1, the transcription on the farm:** `EVOSIM_ENGINE gpu` with
  `EVOSIM_GPU_DEVICE cpu` and `EVOSIM_GPU_PRECISION double` runs the kernel on ILGPU's CPU
  accelerator in double, driven by the farm through the uploads and readbacks above, on
  round 45's launcher for 3,000 s at seed 4 of the fixture world (`rounds/env-r44.ps1`, the
  islands off, as `r45fixc-s4`) and on round 45's own launcher for 3,000 s at seed 1. Both
  must be identical to the CPU backend's run in every `stats.jsonl` field
  (`scratch/r45-build/regress.py`), with the digest at every step equal and the lineage
  byte-equal. This is the test that separates a port fault from a rounding.
- **Acceptance 2, the card agrees with itself:** the same launcher on the 4090 in single at
  two group sizes and twice, 3,000 s: identical rows, digest and lineage.
- **Acceptance 3, the pace:** round 45's launcher at 10,000 and 30,000 bodies (a founding
  screen with a budget that fills to each, dt 0.01), `x real time` and the footer's split
  against the CPU backend's on the same launcher, reported in the entry.
- **Acceptance 4:** no overflow on either run, or the counts.

## 5. The manifest and the header

`run.json` carries `engine: "gpu"`, `precision`, `device` (the card's name and SM count, or
`cpu-accelerator`), `driver`, `groupSize`, the class ceilings and `refusedForClass`;
`dynamicsHash` and `farmHash` as now, plus `kernelHash` over the kernel source. The header
prints `engine gpu single 4090 g32 classes 2/4/8/16`. `stop-arm.ps1` reads `engine: "gpu"`
as it reads `dynamics` (the STOP file). A checkpoint written by a GPU run is the same file
as the CPU's (the mirror is the state), and a resume across engines is a cousin the manifest
marks, as across hashes.

## 6. The theatre

Nothing changes: a GPU run is watched from its record (`positions.jsonl`, `poses.jsonl`), as
a farm run is. The Editor's live mode stays on the CPU backend.

## 7. What is not in this build

The world's grid on the card (the design's later step; at 30,000 bodies in the ten-times
tank the grid is the next ceiling, HANDOFF's chain of three links). Per-part contact on the
card (its tunable is a base-round rule; when ruled, the kernel's grid enters links, spike 3's
occupancy re-measured). A bound on a neuron's value (0115: one at 1e29; the port counts
neurons that reach a float's ceiling in `neuronsAtCeiling` and the entry reads it; a bound
is a world rule for the owner).

## 8. Tests

1. The kernel source is generated from one template for double and single (spike 4's
   `gen4.py` moved into `src/Evosim.Farm.Gpu/` with its generator run at build), and a test
   asserts the generated files match the template's output (no hand edits).
2. `Evosim.Farm.Tests`: a 300 s run of the fixture world on the CPU accelerator in double
   equals the CPU backend's rows and digest (the fast form of acceptance 1, in the suite).
3. Slot reuse: a world with a hundred births and deaths in 300 s reads identical on both
   backends (acceptance 1 covers it; this names the case).
4. A body over the top class is refused at `Add`, counted, and the run continues.
5. The digest canonicalisation: a poisoned body (spike 4's `--poison`) on both backends gives
   equal digests from the step of the loss on.
6. The refusals of section 3 each name their setting.
