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
  grouping: a tank's streams, and the box's open ocean on a shaped floor, whose plan carries the
  phase tables and the sine at each edge's mapped height so the card never calls its own
  sine (`logbook/specs/box-transport-speed.md`). Core keeps the decisions and hands a device the arithmetic
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
- **A card run takes the thread pool's spin at 5,000 by default** (the owner's choice of 2026-10-09; the
  measurements are in HANDOFF). Waking the farm's host threads cost about 55 ms a simulated second, and
  `DOTNET_ThreadPool_UnfairSemaphoreSpinLimit` at 5,000 took the card from 4.14 to 5.24 times real time.
  On `card-r57` from `bd3d836e`, `run-farm.ps1 -SpinLimit` sets it: `default` is 5000 on the card and unset on the CPU, `unset` clears it,
  and a number sets that number. `card-run.ps1` and `farm-queue.ps1` pass it through. It changes pace and
  load and never a number. `run.json` names it as `threadPoolSpinLimit`, and the header's engine line ends
  `spin N`. Compare a pace only with runs at the same spin.
- **`GpuMeanTests` crashes the CPU accelerator with "Index out of range" when the test process is pinned to 4
  or to 8 logical processors** (found 2026-10-19; the same at `df2bf3b8`, so no recent change caused it; 8 crashes
  too, 2026-10-26). The crash takes the whole test host down, so a pinned full Farm suite aborts part way and
  reports only the tests before it: run the suite with `--filter 'FullyQualifiedName!~GpuMeanTests'`, and
  `GpuMeanTests` alone unpinned when it matters (`logbook/specs/plants-keep-matter-build.md` on `r62-base`).
- **A founded card run meets code a resume never reaches, so it is checked on its own** (r58t1,
  2026-10-10; fixed in `c8762e9e` on `r58-runner`). Every card sitting before then was a resume, whose bodies
  are attached before the first block. A founded world steps blocks with no body, and the scent's fill on
  the card then wrote a whole grid into a one-cell column. Before a founded round, run the CPU accelerator
  with every flag the round uses, then a short card run. The CPU accelerator runs at about 0.05 times real
  time, so its check is short: `card-group.ps1 -Device cpu`, founded, `-Seconds 30 -MigrateEvery 10
  -CheckpointEvery 15`, about 10 minutes at 4 threads, which reaches three trades and two checkpoints (measured
  2026-10-10). A resume from its 15 s checkpoint for 15 s more (`-At 15`) checks the group's resume, about
  5 minutes more; on 2026-10-10 it repeated the whole run exactly. Read a resume against the whole run from
  the checkpoint on: `gpu-accept` compares from line 1, and the stats' `harnessBodySteps` and `fluidLinkSteps`
  count from the process's start, so both differ in a resume that matches. It runs at group 4 whatever `-Group` says, so the group of 32 is checked in the card run.
  A resume's pass is
  not a founding's. The card engine also refuses a still current (speed 0), whose stream tables are never
  built.
- **A card run cut by its wall ends at the end of a block** (`077e61c2` on `r58-runner`, 2026-10-10). The
  card steps a whole solver block on a block's first step, so the wall nearly always falls inside one. Before
  the fix the loop left with the solver a block ahead, the final checkpoint refused that state, and the
  manifest was left reading `running`. A walled card run from before then with checkpoints on may read
  `running` though it stopped. A CPU run's block is one step and was never affected.
- **ILGPU's double maths on the card loops on its argument.** In ILGPU 1.5.3, `Sin` and `Cos` in
  double run CORDIC, whose `RangeLimit` subtracts 2π in a `while` loop (`Src/ILGPU.Algorithms/XMath/Cordic.tt`).
  `Exp` in double, which `Tanh` calls, loops up to `int.MaxValue` times (`Cordic.Pow.tt`), and `Log`
  divides by e in a loop (`Cordic.Log.tt`). A body 1e12 m out held one thread for minutes in the bed's
  sample, and past about 7.2e16 the sine never returns. That hung the first card session of 2026-10-08.
  Single uses the card's own instructions and the CPU accelerator uses .NET's, so neither shows it.
  Every sine, cosine and tanh in the kernels therefore goes through `SinR`, `CosR` and `TanhF`, whose
  double forms are guarded (`KernelGenerator.cs`). The guard is bit-neutral inside 1e9 rad. A body
  whose bed angle passes it parts from the CPU on the accelerator as well, and `BedAnglesPastGuard`
  counts such angles. The farm refuses the card in double. A new call to
  a double maths function in a kernel needs the same guard, or a reason it cannot see a large argument.
- **The card holds its own copy of a body's drive, signals, stroke and grips, and uploads it only when it packs
  the body.** A host change to that state between blocks, such as `Creature.BecomeRemains()` (D220), reaches the
  card only if `GpuSlots.Sync` queues the body for a full pack. Sync compares `SeenRemains` with `Creature.Remains`
  for this. A new per-body fact that the host changes needs the same comparison. Without it the card steps the
  stale copy, and `GpuRemainsTests`' mid-run cases fail.
- **Never raise ILGPU 1.5.3's optimisation level above the default for the card.** At O2 it writes
  `bra.uni` (a branch every thread of a warp takes alike) on predicates that differ within a warp, about
  865 of the step kernel's 1,018, and the step hung in its first block on the card on 2026-10-08
  (`card-audit-r57.md` on `nursery-speed`). The CPU accelerator runs the IL and cannot show it; the PTX
  can, dumped without a card and read by `scripts/reads/ptx-uniform.py`.
- **A job meant to stay off the card proves it** (the coordinator's standing rule, 2026-10-08, after a
  driver reset inside an accelerator-only proof's window that nothing in its code could have caused).
  It sets `CUDA_VISIBLE_DEVICES=-1` in its own environment, so an accidental path to the card finds no
  device and throws. While it runs, it watches its own processes and the farms it started for a loaded
  `nvcuda`, and prints at its end whether any loaded it. The test filters and options alone are not
  that proof. `scripts/card/nvcuda-guard.ps1` does all three.
- **Every run, test and pace read on the card goes through `scripts/card/`** (scripts/CLAUDE.md has the
  commands). Each starts the driver watch, checks it alive, stops on an nvlddmkm event and says why. A
  card run reads its own `run.json` for the transport it asked for, and `card-run-check.ps1` reads back
  every condition a card run is held to. An identity claim is `gpu-accept.py` exiting 0.
- **A card launch refuses while any other `Evosim.Farm.exe` runs**, unless its launcher's `-AllowBeside`
  names it, and a theatre film window (`--film-window`) is one. So nothing that starts a farm process,
  a film window included, starts while a card launch is pending. A window started from inside a PowerShell
  `Start-Job` hung at 0.3 s of CPU on 2026-10-23 and blocked a launch until the owner ended it; start
  windows directly. That the job's stdin pipe caused the hang is an inference.
- **Build the tests in Release, with the compiler server off, before a card test** (2026-10-13). The card
  test script runs the Release build of the tests with `--no-build`, so a Debug build leaves it testing old
  code. A build also leaves Roslyn's compiler server resident as a `dotnet.exe`, and the card test refuses
  to start beside it as a busy machine. Build with `-c Release -p:UseSharedCompilation=false`, or run
  `dotnet build-server shutdown` after the build. Both stopped the D168 card window at its last step, before
  it touched the card.
- **A card contact count from before D168 is about half the CPU's** (`logbook/specs/build-readings.md`).
  Single precision drops any move smaller than half the gap between neighbouring floats at the body's
  position, so resting bodies flickered out of touch. Since D168 the card sums each position with a
  correction term and takes contacts, and the floor and glass under them, from a nearby lattice point.
  Compare card and CPU contact numbers only on runs from D168 on.
- **Contact on the true shape has a second copy on the card** (D228, `EVOSIM_CONTACT_SHAPE=trueshape` and
  `trueshapeatpoint`). `gpu-kernel.template` transcribes `ConvexCore` (GJK, `Support`) and `ContactTrueShape` (the pair
  loop and the four surfaces) operation for operation, so a change to either is made in the template in the same change.
  The pair runs in its own frame, the first core at the origin and the second at minus the sphere test's difference, in
  the solver and the kernel alike. Each link's core and margin sit by link row in `SGlob` (`CoreHx` to `CoreM`), because a
  link reads other classes' cores, and go up only after a pack; the turns are nine more columns in each sphere set. All of
  it is a one-cell stub when the switch is off.
  `trueshapeatpoint` (`cfg.AtPoint`) adds the force at the contact point. Each sphere set carries each link's world-frame
  spin in three more columns (`LSp0` to `LSp2`, a link row wide at that value only, and not copied by a block at the
  others). `ContactLinks` writes the touches' torque in three more rows of `LinkPush`, which `ContactsPerPartStaged` adds
  to `Fext` beside the force. The link's inertia is `SConst.Inertia` and `SConst.SmallI`, which the card carries for every
  link already. Its pair is `TrueShapePairAt`, a copy of `TrueShapePair` that hands back the contact point's two levers,
  and its surfaces are `AgainstTheWorldPoint` over the one vertex routine `VerticesPoint`. The card has no friction
  (`FarmGpu.Open` refuses a bed with any), so `Rock`'s friction is the zero vector there.
