# The pace factor's build spec (D239)

*2026-10-26, the speed agent, for the Sonnet builder on branch `pace` (worktree `scratch/wt-pace`, from `main`), and
for review. The ruling is D239 in `DECISIONS.md`; the design read is `logbook/specs/pace-of-life-read.md`, whose §4
lists the code sites. This file states what to build and how to tell it is right. It carries no history.*

## What is built

The pace factor k is a new tunable in the `physics` group: `RunConfig.PaceFactor`, a float, default 1, refused
below 1 or when not finite. Its environment name is `EVOSIM_PACE_FACTOR`. One world second holds k seconds of
physics. Everything in `Evosim.Core` keeps world seconds and is not touched: `World.Step(0.5f)` still advances the
world by half a world second, and every rate in Core (upkeep, the mouth, healing, growth, gestation, the age hazard,
the remains window) keeps its meaning per world second.

1. **Steps per metabolic step.** A metabolic step of 0.5 world seconds holds 0.5·k physics seconds, so it holds
   0.5·k / dt physics steps of dt. `EnvBinding.ResolvePhysicsStep` takes the factor and refuses a dt that does not
   divide 0.5·k exactly, with a message listing the allowed steps for that k. At k = 1 its result is today's, to the
   bit, for every dt it accepts today.
2. **The solver's clock is world time.** `DynamicsWorld.ElapsedSeconds` advances by dt / k a physics step. The
   current is pinned at that clock (`PinInstant`). What the solver samples from the water is scaled at the one seam
   where the solver reads it (`Water.cs`): the water's velocity divided by k, its acceleration divided by k². So a
   leaf drifting with the current moves as far per world second as today, and a swimmer strokes k times as often per
   world second. Every force, the step dt, the drag and drive limiters, buoyancy and gravity stay physical and
   untouched. At k = 1 no multiplication is entered at all, so the bits are today's.
3. **The card.** The same seam on the card: the pin (`GpuWorld.PinInstant`) at world time, and the kernel's water
   velocity and acceleration divided by k and k² where they are read (`gpu-kernel.template`'s `VelocityAt` and
   `AccelerationAt`). The kernel sources are regenerated as `src/Evosim.Farm.Gpu/CLAUDE.md` says. The card block is
   cut at the metabolic step, so its length follows the new steps per metabolic step. Check every place that assumes
   50 steps a block or a metabolic step.
4. **The farm's other clocks.** Anything the harness counts in physics time that is meant as world time is converted:
   the growth cadence (`Simulation._sinceGrowthStep` against `GrowthStepSeconds`), and any cadence read from
   `Steps × dt`. Report rows, positions, poses, checkpoints and the wall limit stay keyed to `World.ElapsedSeconds`.
   The manifest records the factor, and the report's engine line prints it beside dt. `x real time` stays world
   seconds per wall second.
5. **The checkpoint** carries the factor through the config, and the header's steps per metabolic step already
   refuses a resume at a different value. A resume with a different k is refused by name.
6. **Programs that step their own world.** The nursery's tanks (`Tank.cs`, `LeafTank.cs`), the cube-gap program and
   any other caller of `ResolvePhysicsStep` either pass the factor through or refuse a config with k ≠ 1 by name. The
   theatre reaches the farm through `Simulation`. Do not edit or run anything under `unity/`; list any site there that
   would need the factor.
7. **Two instruments.** The stats row writes `mouthBlows` and `mouthBlowsFinishing` whenever contact eating is on,
   not only when the bite damage dial is off 1. Check that `scripts/reads/r62-read.ps1`, `r62g-read.py`,
   `r62-health.py`, `compare-run-rows.py` and `gpu-accept.py` tolerate a column they did not have. The swim probe
   (`src/Evosim.SwimProbe/Program.cs`) takes `--dt <seconds>` in place of its fixed 0.01, so a stroke's waveform can
   be traced at a coarser step.

## What is not built

No thrust multiplier. D239's thrust arm is set with existing settings: `EVOSIM_LIMB_PUSH` at 0.3·k² and
`EVOSIM_WORK_COST` at 0.5/k². Nothing in Core changes. No default changes: `NewbornReserveFraction` stays 0.2.

## Tests

- **k = 1 is the build before it.** The config fixtures are re-pinned for the new tunable, as for every new one, and
  their digests must be unchanged apart from the config hash. `RunConfigTests` and `RunConfigJsonTests` cover the new
  property by reflection.
- **Steps.** `ResolvePhysicsStep` gives 150 steps at k = 3, dt 0.01; 50 at k = 3, dt 0.03; refuses k = 3, dt 0.04.
- **Still water.** One jointed body under a fixed stroke in still water travels about k times as far per world second
  at k = 3 as at k = 1, within a tolerance the test states, on the host solver.
- **The current.** A rigid body with no excess density in a uniform current moves the same distance per world second
  at k = 3 as at k = 1, within a tolerance the test states.
- **The economy.** One body's standing cost billed over 10 world seconds is the same at k = 1 and k = 3.
- **The card.** The kernel source test regenerates and matches. The CPU accelerator steps a small world at k = 3 to
  the host's bits, if the existing accelerator tests allow it.
- **The refusals.** A dt that does not divide 0.5·k, a k below 1, and a resume at another k each refuse by name.

## The builder's limits

Edits only while the machine runs the theatre's render and the kill-meal screen: no build, no test, no run of any
program. The reviewer compiles and tests afterwards. Code files keep CRLF line endings; a PowerShell script keeps
one UTF-8 byte-order mark and CRLF. Never write a file through `sed -i` or a heredoc. Work in
`D:\Projects\experiments\evolution-simulator\scratch\wt-pace\` by absolute path, never by changing directory into it.
Helper scripts and notes go in `D:\Projects\experiments\evolution-simulator\scratch\pace-build\`. Nothing is written
in the session scratchpad, the Windows temp directory, or anywhere outside those two folders. Nothing is committed.
The report lists every file changed, every assumption made, and every site found that this spec did not name.
