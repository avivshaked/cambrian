# The card: what will bite you

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before any work on the graphics card: ILGPU, the `EVOSIM_GPU_*` settings, or a round run with `-GpuTransport`.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Gotchas

- **The card runs the solver through ILGPU and nothing else, and its group size is set by
  hand** (logbook/0112, `spikes/02-gpu-featherstone/`). ComputeSharp refuses a local array
  in a shader and the step needs about 1,600 words of scratch a thread, so it is out. ILGPU
  compiles the whole step, but its PTX backend has no `Sin`, `Cos` or `Atan2` without
  `ILGPU.Algorithms` and `EnableAlgorithms()`, and its automatic group size is up to 2.9
  times slower than 32 because each thread holds 12.7 kB of local memory in double; the
  link ceiling is a compile-time constant that costs every thread whether used or not.
  Double on the 4090 is slower than sixteen of this machine's cores at 10,000 bodies; single
  is about six times faster and is a new realisation of every seed. Identity on the card
  holds across launch shapes, so a GPU identity claim is made at two group sizes. Run GPU
  code in the foreground with nothing else on the machine, except the snow's transport under an
  overnight round from round 51 (D126).
  **The card fuses a multiply and an add unless told not to.** Under the farm's `Cuda()` context
  the PTX compiler contracts `x*y+z` into one fused operation, so 14% of such doubles and nearly
  half of a 24-term chain differ from the CPU, in single the same. A bit cast around the product
  changes nothing; a product written as inline `mul.rn.f64` (`CudaAsm.Emit`) is never contracted,
  and with it every pattern tried matched the CPU bit for bit (`logbook/specs/fma-probe/`,
  2026-09-26). A card kernel that must reproduce a CPU number writes every product that way.
  **The snow's transport has a second copy on the card** (`EVOSIM_GPU_TRANSPORT`, 2026-09-26).
  `src/Evosim.Farm.Gpu/GpuTransport.cs` transcribes `GridField`'s edge sampling, face assembly,
  outflow bound and three passes, and `CurrentField`'s hoisted potential, in their order and
  grouping. Core keeps the decisions and hands a device the arithmetic
  (`GridField.TransportDevice`), and a device declines any grid its plan does not cover. A change
  to that arithmetic in Core is therefore made in the kernel in the same change.
  `GpuTransportTests` runs the kernels on ILGPU's CPU accelerator and fails when the two part,
  byte for byte in the world's state. A digest comparison on the card is the card's own check.
  The flag works under either engine: under the cpu engine the device opens a context of its own.
  The launch asks it for the grid's tables first (`GridField.OfferTransportDevice`) and refuses a
  world the card will not carry, and `run.json` says `transport: cpu` or `card`. From round 51
  an overnight round runs with it on (D126, the owner's ruling of 2026-09-26), after a daytime
  check against round 50's own record (HANDOFF).
  **The card's copies were pageable until the same day.** A managed array moves at about 6 GB/s.
  Pinned (`GC.AllocateArray(pinned: true)`) and registered (`CreatePageLockFromPinned`), the same
  array moves at about 21 GB/s through the same `CopyFromCPU` and `CopyToCPU` calls, because the
  driver recognises the range. `Col<T>` does this for every host column. **Compare paces within one sitting,
  alternated**: the same pageable run read 5.72x at noon and 3.86x at 13:40 that day, with a
  browser busy.
  **The card's probe does not go through the binding**: `EVOSIM_GPU_PROBE=1` is read from the
  process environment, so `run-farm.ps1 -Env`, which hands settings over as `NAME=VALUE`
  arguments, never reaches it, and the farm warns that it ignores the variable while the probe
  stays off (the first probe of 2026-09-26). Set it in the launching shell's environment
  (`scripts/probe-gpu-env.ps1`). A setting the binding knows, such as
  `EVOSIM_GPU_CONCURRENT`, goes through `-Env` as usual.
