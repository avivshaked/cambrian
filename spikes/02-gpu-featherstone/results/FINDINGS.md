# Featherstone on the GPU: a measurement spike

*2026-09-22. Tree at commit `5da9ee3e9071dad1005e658ecf53c4aeaae3a955` (working tree clean of
`src/`; four untracked review files at the root). Nothing in `src/` was edited.*

The question: does `Evosim.Dynamics`'s per-creature articulated-body step run on the GPU through
ILGPU, at what cost, and at what deviation, in double and in single. This is a measurement, not
the port.

## The machine and the build

| | |
|---|---|
| Host | MAINPC, 32 logical processors, .NET 8.0.24, server GC, `TieredCompilation` false |
| Card | NVIDIA GeForce RTX 4090, SM_89, 128 SMs, 1024 threads a group |
| ILGPU | 1.5.3, plus **ILGPU.Algorithms 1.5.3** with `EnableAlgorithms()` — required, see *Compile hurdles* |
| Source | `runs/r43-s1/2026-09-22-053156-08613877`, snapshot `000030000.jsonl`, config hash `0861387741b94259` (matches the record) |
| Bodies | 1,104 genomes read, 1,104 developed, none refused. Parts 1→85 2→261 3→419 4→88 5→132 6→64 7→45 8→4 9→6; 483 of 1,104 carry a joint. `MaxLinks` 9 covers every one, so nothing was dropped |
| Step | dt 0.01. At that step `SolverConfig.LimitersEngage` and `DragLimiterEngages` are both **false** with `driveLimitAtEveryStep` off, so neither limiter runs in the library either |

Nothing else was on the machine: `Get-Process Unity,Evosim.Farm` was empty before every run, and
the harness refuses to start if either is up.

## What the kernel is

One GPU thread per creature. The creature's constants and its whole state are loaded into
thread-local arrays at kernel entry, `steps` steps run entirely in local memory, and the state
goes back at the end. Global arrays are strided by creature (`slot * N + c`), so neighbouring
threads read neighbouring words.

Carried, term for term from the library:

- `Kinematics.Poses` and `Kinematics.Velocities`, including `JointRotation`, `SubspaceAxis`, the
  ball joint's quaternion path and the rotation-vector read-back.
- `Aba.Seed` / `Inward` / `Outward` / `Integrate`, including the free base's 6×6 Gaussian
  elimination with partial pivoting, the 1/2/3-dof symmetric inverse, and the limit spring's
  implicit term on the diagonal.
- `Fluid.Apply`'s pressure drag over every panel in the part's own frame (up to 264 panels a
  body here), the buoyancy term with D064's volume scaling, D049's lift and D077's `Restore`.
- `DynamicsWorld.JointTorques`: the drive damper carried implicitly and the limit springs.
- The tail of `EffectorDrive.Drive`: the link-frame torque on the child and its reaction on the
  parent.

**Added mass is in**, because it is not a force: `Creature` folds it into the link mass at build
and the flat copy carries the effective mass.

Not carried, and why:

| Omitted | Why |
|---|---|
| Brain, senses, the ten-sample drive average | the brief: the drives are held at the step's values. Each body's signal is one real pass of its own senses and brain at the pose it is placed in, clamped as `EffectorDrive` clamps it, then frozen |
| Creature–creature contact, the contact grid, the bed, the glass | the brief: one body alone. `CreatureContact` false and `TankRadiusMetres` 0 on the reference too |
| A moving current | still water. `FluidAccelerationCoefficient` is set to 0 on **both** sides, so D090's term is skipped rather than added as a zero |
| Growth resize | between steps in the farm, never inside a step |
| The work and dissipation ledgers, `Settle` | they record and move nothing |
| The trace ring, the divergence guard and its dumps | diagnostics. No body went non-finite in any run below |
| The drag limiter and the drive limiter | they do not engage at dt 0.01. **A 0.02 screen would need them added** |

The CPU reference runs the *same* reduced step on real `Creature` objects through the library's
own `Fluid.Apply`, `Aba.Solve`, `Aba.Integrate`, `Kinematics.Poses` and `Kinematics.Velocities`.
Two routines are copied rather than called, both named in `RefStep.cs`: `JointTorques`, verbatim
from `DynamicsWorld` where it is private, and the frozen drive, which is the spike's own
reduction and has no library form. **Nothing in `src/` was made more visible.**

## (a) Deviation

1,000 steps at dt 0.01 is 10 simulated seconds. The 30,000 and 100,000 rows ran 200 steps, so
their numbers are not comparable with the first two rows — only within their own row.

**Transcription check first.** The same kernel, on ILGPU's CPU device, in double, against the
library: **every component of q, qd and every link position is bit-identical, deviation exactly
0** (N = 64 and N = 256, 1,000 steps). So the numbers below are the device and the precision,
not a difference in transcription.

| N | steps | | q max | q rms | qd max | qd rms | pos max | pos rms |
|---|---|---|---|---|---|---|---|---|
| 1,000 | 1,000 | double | 1.88e-15 rad | 9.6e-17 | 1.39e-14 rad/s | 6.2e-16 | **1.78e-14 m** | 6.2e-16 m |
| 1,000 | 1,000 | single | 9.31e-06 rad | 7.1e-07 | 1.22e-05 rad/s | 6.5e-07 | **1.31e-03 m** | 6.21e-05 m |
| 10,000 | 1,000 | double | 2.03e-15 rad | 8.4e-17 | 2.89e-14 rad/s | 7.3e-16 | **3.55e-14 m** | 6.5e-16 m |
| 10,000 | 1,000 | single | 9.31e-06 rad | 7.0e-07 | 8.40e-05 rad/s | 2.4e-06 | **1.41e-03 m** | 6.22e-05 m |
| 30,000 | 200 | double | 1.67e-15 rad | 4.6e-17 | 3.13e-14 rad/s | 8.1e-16 | 2.13e-14 m | 2.9e-16 m |
| 30,000 | 200 | single | 1.98e-06 rad | 1.1e-07 | 1.44e-05 rad/s | 6.5e-07 | 4.70e-04 m | 1.90e-05 m |
| 100,000 | 200 | double | 1.55e-15 rad | 4.3e-17 | 5.49e-14 rad/s | 8.9e-16 | 1.42e-14 m | 2.5e-16 m |
| 100,000 | 200 | single | 2.19e-06 rad | 6.9e-08 | 3.12e-05 rad/s | 7.7e-07 | 3.30e-04 m | 1.22e-05 m |

The scale to read the position column against: the bodies sit about 15.7 m from the origin
(rms of |position| over every component), and their links are tens of centimetres long.

Worst single creature, 1,000 steps: 1.41e-03 m at N = 10,000 (body 1449). No non-finite
comparison in any run; no body was lost by the reference.

## (b) Identity on the card

Two runs of the same kernel on the same input, compared by FNV-1a over the raw bits of the whole
state and every link position:

| N | double | single |
|---|---|---|
| 1,000 | bit-identical `f4400996fab602b9` | bit-identical `87ba0827c80b0f8f` |
| 10,000 | bit-identical `ae4314c846190f86` | bit-identical `2a870ee0083d6b20` |
| 30,000 (200 steps) | bit-identical `03e7679c3470f8d4` | bit-identical `07ad4d9521077016` |
| 100,000 (200 steps) | bit-identical `76c5a9089fda5f94` | bit-identical `14558410fd05550a` |

Stronger than a repeat: **the digest does not move with the launch configuration.** At N = 1,000
the same digests come back at group sizes 32, 64, 128, 256, 1024 and auto; at N = 10,000 and
N = 100,000 the auto-grouped and group-32 runs agree to the bit.

## (c) and (d) Pace

Microseconds per body-step, median of 3. Group size 32 (see *Group size* below); the
auto-grouped figures are in the last table.

| N | steps | | kernel only | + positions down once per launch | + drive up, positions down every 50 steps | + drive up, positions down every step |
|---|---|---|---|---|---|---|
| 1,000 | 1,000 | double | 1.0195 | 1.0203 | 1.0435 | 1.9167 |
| 1,000 | 1,000 | single | 0.1270 | 0.1280 | 0.1305 | 0.2280 |
| 10,000 | 1,000 | double | **0.1244** | 0.1246 | 0.1277 | 0.2748 |
| 10,000 | 1,000 | single | **0.0213** | 0.0214 | 0.0225 | 0.0692 |
| 30,000 | 200 | double | 0.0701 | 0.0703 | 0.0719 | 0.1713 |
| 30,000 | 200 | single | 0.0174 | 0.0175 | 0.0181 | 0.0496 |
| 100,000 | 200 | double | 0.0652 | 0.0649 | 0.0659 | 0.1518 |
| 100,000 | 200 | single | 0.0188 | 0.0189 | 0.0194 | 0.0510 |

CPU reference, the same reduced step through the library, same machine, median of 3:

| N | 16 threads | 1 thread |
|---|---|---|
| 1,000 | 0.0968 | 1.0189 |
| 10,000 | 0.0892 | — |
| 30,000 | 0.0904 | — |
| 100,000 | 0.0900 | — |

**Which cadence the farm needs.** The world step reads positions once a metabolic step, which is
50 physics steps at dt 0.01 — the middle column. It costs 3 to 6% over the kernel alone, and at
N = 10,000 in single it is 0.0225 µs against 0.0213. The per-step cadence, the last column,
costs 2.2× to 3.3×: it is launch latency, not bandwidth (the position download at N = 10,000 is
3.4 MB in single). **The metabolic cadence is nearly free; a per-physics-step round trip is
not.**

### Group size

Kernel only, N = 1,000, 1,000 steps, one repeat:

| group | double | single |
|---|---|---|
| 32 | 1.0208 | 0.1280 |
| 64 | 1.0572 | 0.1346 |
| 128 | 1.2533 | 0.1465 |
| 256 | 2.1475 | 0.1531 |
| 1024 | 7.6820 | 0.2280 |

ILGPU's auto-grouping picks badly here, and the reason is the local memory: at `MaxLinks` 9 each
thread holds 1,590 reals of state and scratch, 12.7 KB in double and 6.4 KB in single. A
1024-thread block therefore wants 13 MB of local memory. **Auto-grouped, the same runs read**
2.9416 / 0.1795 at N = 1,000, 0.3103 / 0.0247 at N = 10,000, 0.1037 / 0.0176 at N = 30,000 (300
steps) and 0.0859 / 0.0186 at N = 100,000 (double / single) — up to 2.9× worse than group 32.
Anything built on this must set the group size by hand.

### Kernel compile

Separate from the measurements above. First load of any kernel in a process, which carries
ILGPU's own warm-up: 317–595 ms. Loads after that, on the same accelerator: 118–130 ms double,
48–121 ms single. One-off, and dwarfed by a run.

### Device memory

The whole flat set, both precisions resident at once: 17 MB / 8 MB at N = 1,000, 174 MB / 87 MB
at N = 10,000, 1,743 MB / 871 MB at N = 100,000 (double / single). Panels dominate — 264 per
body at `panelsPerAxis` 2.

## Reading it

**Double on the 4090 is not worth it.** At N = 10,000 it is 0.124 µs against the CPU reference's
0.089 µs at 16 threads: the card, in double, is *slower* than half this machine's cores. At
100,000 it draws level (0.065 vs 0.090). Double buys nothing here and costs the whole card.

**Single is worth it, and the deviation looks affordable — but it is not free of a ruling.**
5.9× the CPU reference at N = 10,000 (0.0213 vs 0.0892), 5.2× at 30,000, and the position error
after 10 simulated seconds is 1.4 mm at worst and 62 µm rms on bodies 15.7 m out, with joint
angles within 1e-5 rad. For a world whose bodies are tens of centimetres across and whose
contact is a soft push between bounding spheres, that is below anything the ecology reads. What
it is *not* is a replay: a single-precision world is a different realisation of every seed, the
way any per-step change is (CLAUDE.md's butterfly rule), so the record does not carry across and
the gate has to be D104's — distributions agreeing across seeds, not a digest.

**The card is not busy until ~30,000 bodies.** Per body-step, single costs 0.127 µs at 1,000,
0.0213 at 10,000, 0.0174 at 30,000 and 0.0188 at 100,000. 10,000 threads over 128 SMs is 78
threads an SM; the kernel is latency-bound on local memory long before it is compute-bound. So
the proposal's committed target of 10,000 sits on the shoulder of the curve and its stretch
target of 100,000 is where the hardware actually pays. **A GPU port aimed at 10,000 buys about
6×; aimed at 100,000 it buys the same 6× on ten times the population.**

**One caveat that matters for every ratio above.** The CPU reference here is the *reduced* step:
0.089 µs a body-step at 16 threads, against HANDOFF's 0.32 µs for the full `DynamicsWorld` step
at 3,536 bodies. The reduced step is roughly 3.5× cheaper than the real one, because the brain,
the senses, the contact grid, the ledgers and the trace are all out of it. Both sides of the
comparison run the same reduction, so the 5.9× is honest as a ratio — but it is *not* the
speedup a farm would see, because the GPU would have to carry the omitted work too, and the
brain in particular is per-creature branchy code that the card will like less than the solver.
Sizing stage 3 off this table without measuring the brain would be the classic error.

## Reproducing

```powershell
cd D:\Projects\experiments\evolution-simulator\spikes\02-gpu-featherstone
python gen.py 9                                 # KernelD/F.cs and RunnerD/F.cs from the templates
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
& $dotnet build featherstone.csproj -c Release

# the population's shape, no GPU touched
.\out\bin\Release\net8.0\featherstone.exe --describe --genomes 2000

# deviation, identity and pace (the tables above)
.\out\bin\Release\net8.0\featherstone.exe --sizes 1000,10000 --steps 1000 --repeats 3 --genomes 2000 --group 32
.\out\bin\Release\net8.0\featherstone.exe --sizes 30000,100000 --steps 200 --repeats 3 --genomes 2000 --group 32

# the auto-grouped figures, and the transcription check at N = 256
.\out\bin\Release\net8.0\featherstone.exe --sizes 1000 --steps 1000 --repeats 3 --check-size 256 --genomes 2000
.\out\bin\Release\net8.0\featherstone.exe --sizes 10000 --steps 1000 --repeats 3 --genomes 2000

# the group-size sweep
foreach ($g in 32,64,128,256,1024) {
  .\out\bin\Release\net8.0\featherstone.exe --sizes 1000 --steps 1000 --repeats 1 --genomes 2000 --group $g
}
```

Raw console output of the last run is in `raw-output.txt`; the runs above were captured to
`n10000.txt`, `n30000.txt`, `n100000.txt`, `g32.txt` and `g32-big.txt` beside this file.

## Compile hurdles met

1. **ComputeSharp is out**, as the earlier probe found: the shader compiler refuses a local array
   (CMPS0025 `stackalloc`, CMPS0032 pointer). The solver needs per-thread scratch of about
   1,600 words, so that ends it.
2. **ILGPU accepted the whole step with nothing cut.** Local arrays of constant size, static
   methods taking those arrays as parameters (everything is inlined), structs with operators and
   properties, `ref` struct parameters, and structs of `ArrayView<T>` as kernel arguments all
   compiled first time. The one thing written differently as a precaution rather than after a
   refusal: `Aba`'s two `stackalloc`/`Span<double>` scratches became fixed-size local arrays
   allocated at kernel entry and passed down.
3. **The PTX backend has no intrinsic for `Sin`, `Cos` or `Atan2`.** The first CUDA load threw
   `NotSupportedIntrinsicException: The function 'SinF' does not have an intrinsic implementation
   for this backend. 'EnableAlgorithms' from the Algorithms library not invoked?` — on the
   *double* kernel. Fixed by adding `ILGPU.Algorithms` and `EnableAlgorithms()`, which supplies
   software implementations. This is also most of why the double deviation is ~1e-15 rather than
   0: the transcription is exact (it reads 0 on ILGPU's CPU device), and what is left is the
   card's arithmetic and Algorithms' transcendentals against .NET's.
4. **Two library routines are private or internal** and were copied rather than widened, as the
   brief asks: `DynamicsWorld.JointTorques` (verbatim, less its two ledger calls, in
   `RefStep.cs`) and `Creature.AxisOf` (two lines, in `Program.cs`). Both name their source.
5. **`MaxLinks` is a compile-time constant** baked by `gen.py`, because the per-thread arrays
   must be fixed size. 9 covers every body in this snapshot. A world that grows bodies past it
   needs a regenerate, and the cost is linear in `MaxLinks` for every thread whether it uses the
   room or not — a 16-link ceiling would put 22 KB of local memory on every one-part body.
