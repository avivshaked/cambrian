# The nursery on the card: Fable's plan of 2026-10-04

On 2026-10-04 at about 21:45 a Fable subagent was asked how the nursery's tank search should run on
the graphics card. It was asked how fast that would be and what it would change, and it worked
read-only. The owner agreed to the read that evening, and the coordinator gave its go under the
rules the brief states. Nothing was built, compiled or run. Nothing touched the card, which is under
a hold until about 2026-10-06. Its report is below, saved as it came at about 22:15. Figures marked
[m] are measured (the brief's, or the file named), [b] believed from the code, [g] guessed. The
subagent's text keeps its own voice and has not been restyled. Its paths are under
`scratch/wt-speed/src/` (branch `nursery-speed` at `f6f01f6`) unless it says otherwise.

The session checked two things before saving it. The first is that finding 1 holds:
`Evosim.Farm/FarmGpu.cs:37-53` refuses the limb push, the scent, the bend-and-power drive, the bed's
friction and brain v2. So the session's earlier remark to the owner, that the whole body step is
already ported to the card, was true of round 45's world and not of the nursery's.

The second is a wrong figure in the brief, which the report noticed. The brief called the 76.8 s loop
one tank episode at one thread. It was the whole of the coordinator's timing trial of 2026-10-02
(`scratch/nursery/timing-brain2-b`), 16 genomes by 2 draws, so four tank episodes. That is about
19 s an episode before the snow kernel, and about 11 s after it by the kernel's 1.76 [b]. The
generation wall on 14 efficiency cores gives 22 thread-seconds an episode, a factor of about 2. The
efficiency cores and their shared caches are the likely causes; that is an inference, and the slot
of 2026-10-05 measures it. The report's section 0 names the gap and takes its proportions from the
profile and its absolute from the generation wall, so its estimates do not rest on the wrong figure.

---

## 0. Three findings that change the question

1. **The card does not carry the nursery's world today.** `Evosim.Farm/FarmGpu.cs:37-53` refuses brain v2 (`EVOSIM_BRAIN_MODEL central`), the limb push, the scent and the bend-and-power drive. The nursery's config `rounds/nursery/configs/7319d6b528cc77f1.json` has all four on (`brainModel Central`, `limbPushCoefficient 0.3`, `powerPushRadiansPerSecond 2`, `senseScent true`), and the kernel template has no Stroke/Hold/Ramp drive sense either (grep of `gpu-kernel.template`: none). HANDOFF 11a's "the body step is already ported and exact" holds for round 45's world, not this one. The missing pieces are small (section 1) but they come first.
2. **The CPU's time is mostly the world step, not the physics.** From the brief's one-thread split (world 59.6 of 76.8 s before the snow kernel; advection 88% of the world) and the ×1.76 kernel gain: after the kernel an episode is about 39% physics, 44% snow advection, 17% the rest of the world step [b]. A port that puts only the bodies on the card is capped near 2× by the host's advection (192 tanks × ~8 ms per world step on a loaded efficiency core [b]). The transport must go on the card too, batched over tanks, and the existing `GpuTransport` kernels are exactly that work.
3. **The tanks of one draw share a bed and a current; the 16 draws do not.** `World.cs:1439` seeds the bed from the world seed, and `CurrentField.BuildStreams` (4452) draws the stream phases from an rng, so a generation has 16 (bed, current, live-mask, plan-table) sets, 12 tanks each. The pinned instant is 186 doubles per draw per step (`GpuWorld.Instant`), 16 × 186 × 50 per block: nothing.

Also: the CPU figures do not reconcile. 5.05 min a generation on 14 threads is 22 thread-seconds an episode [m-derived]; the one-thread profile says 76.8 s [m]. I used the profile's proportions and the generation wall's absolute. The gap (load, the E-core, the profiler) is unknown [g].

## 1. The design

**Shape.** Keep one Core `World` and one `DynamicsWorld` per tank, as `Tank.cs:138-141` builds them, and replace the per-tank thread loop with a lockstep loop over all tanks (new `TankEpisodesOnCard` in `Evosim.Nursery`): set every tank up on 14 threads; then per metabolic step: one card block of 50 physics steps for every body of every tank; the host harness per tank on 14 threads (`Simulation.Reconcile`, `CheckFinite`, `Metabolise` up to `World.Step`'s field passes); one card pass for all tanks' field passes; the tank's tail. `Simulation.Step` (`Simulation.cs:262-338`) is split into "block then harness" (its `_solverAhead` seam already exists for a backend) and `World.Step` (`World.cs:2377`) gets a field-device hook at 2462-2509. The backend is a multi-world `IStepBackend` (`DynamicsWorldBackend.cs`): `Block(worlds[], steps)`, one `GpuSlots` across worlds with slots allocated tank-contiguously (slot = tank × 8 + member; births are held so the layout is fixed for a generation).

**Tagging and contacts.** A `tankOf[slot]` and `drawOf[tank]` column. Contact candidates come from the tank's own 16 link spheres by a loop over its slot range: no grid kernels (the mean, ranges, scan ×3, scatter and census launches go), which both removes 7 launches a step and makes the census per tank trivial (the host counts from the overlap lists already read back; `BuildEvents` per world). The pair sums stay in ascending id order inside the tank.

**World tables.** `GpuWorld` built once per draw (16); `WR`, `WI`, the instants and the stock concatenated with per-draw and per-tank bases carried in `SCfg`; every world read in the kernel goes through `base[drawOf[tankOf[slot]]]`.

**The current pin.** Shared per draw: 16 instant sets a step, filled by `GpuWorld.Instant` as now.

**The snow grid.** One strided array `[tank][cell]`, double, 192 × 52,900 × 8 B = 81 MB; the chemical sense reads it by index with the tank base (the kernel narrows on read where the farm narrows at upload: the same float). The `GpuTransport` kernels (`GpuTransport.cs:318-580`) take a tank index from `idx / cells` and a per-draw table base; `Settle`, `Remineralise` and `Mix` (`GridField.cs:2015, 2085, 2304`) are transcribed the same way with `M()` products. The matter grid (320 cells, 5 m) stays on the host.

**The world step.** Host per tank: light, bills, feeding, mouths, corpses, scent fill, growth, the ledgers (Core untouched, double). Card: the four field passes. The host owns the stock during its step, so per block the stock comes down (81 MB, 3.9 ms at the pinned 21 GB/s [m, `Farm.Gpu/CLAUDE.md`]) and goes back up (3.9 ms). The flush of the field passes is deferred past the 192 `World.Step` calls and guarded: it is only sound because burial is 0, the field is a grid (Cull is a no-op), births are held and the floor is closed; the device refuses a config where any of those does not hold.

**Uploads per block:** instants (tiny), the stock (81 MB), the scent grids (192 × 20.8k floats = 16 MB), Reserve/PowerScale/Contact/Damage/Gates for 1,536 bodies (small). **Readbacks:** the class-0 Down columns for 1,536 bodies (~2-3 MB [g]), the overlap lists, the stock. **Scoring reads** (`Tank.cs:241-262`) read the organism's ledger and `o.Point.Position` on the host after the harness: unchanged.

| piece | where today | card or host | reuse or new | size [g] |
|---|---|---|---|---|
| columns, pinned copies (`GpuColumns.cs`) | farm engine | card | as is | 0 |
| slots (`GpuSlots.cs`) | farm engine | host | tank-contiguous allocation mode | 60 lines |
| world tables (`GpuWorld.cs`) | farm engine | host → card | one per draw, concatenated with bases | 150 |
| water, fluid, contacts law, ABA, integrate, poses, settle sums, trace (`gpu-kernel.template`) | farm engine | card | as is, reads through the tank base | 80 |
| grid kernels, census kernel | farm engine | dropped | per-tank candidate loop instead | 60 |
| brain v2 (`Brain.cs:394-424`) | CPU only | card | new: CSR per body, tanh, swap, outputs | 150 kernel + 150 host |
| scent sense (`ScentField.cs:92`) | CPU only | card (grid filled on host) | new | 50 + 40 |
| drive sense (`CreatureSenses.cs:296`) | CPU only | card | new | 40 |
| bend-and-power drive (`EffectorDrive.cs:253-331`) | CPU only | card | new: 7 envelope arrays + joint genes per link | 120 + 80 |
| power push, its work term (`Fluid.cs:170-197`) | CPU only | card | new | 40 |
| multi-world `Block`, pack/unpack per world (`gpu-runner.template`) | farm engine | host | rework | 300 |
| transport kernels (`GpuTransport.cs`) | farm engine (one grid) | card | tank-strided | 120 |
| settle, remineralise, mix | Core, CPU | card, double | new transcription | 350 + tests 150 |
| field-device hook in `World.Step`, deferred flush with guard | Core | host | new | 60 |
| `Simulation` split (block, harness) | farm | host | new | 60 |
| lockstep tank loop | nursery | host | new | 300 |
| run record: engine, precision, kernelHash (`RunRecord.cs`) | nursery | host | extend | 40 |
| tests: transcription on the CPU accelerator, two tanks vs two CPU tanks, field passes bit for bit | | | new | 400 |

About 2,900 lines [g]; the farm's engine is reused for roughly two thirds of what runs.

## 2. Pace

**The per-physics-step floor for 2-part bodies.** One thread's serial work: two water samples at the pinned instant (24 stream terms with gradient, ~2,500 flops each [b, `opus-a2-a3.md`], plus the bed's 24 transcendentals), brain v2 (~82 links, ~130 tanh), 48 panels, 15 brute-force sphere tests, a 2-link ABA, integration: ~20-40 µs of latency [g]. 1,536 threads at group 32 are 48 groups on 128 SMs, one wave, so a launch costs about one thread's latency plus ~10 µs launch overhead; with 2-3 launches a step, **0.05-0.1 ms a step** [g]. The pessimistic bound is the farm's whole chain unchanged: 1.6-2.8 ms a step [m, 0115], but that was set by 16-link bodies, 256-candidate sorts and the grid's seven launches, none of which this crowd has. I bracket **0.05-0.3 ms a step → 3-18 s a generation** of physics, against the CPU's ~8.6 s per episode × 192 / 14 = 118 s [b].

**The world step per block (one per 0.5 s).** Card fields: round 50's 8M-edge grid went 50 → 5 ms on the card [m, `snow-advection-speed-read.md` #13]; 192 tanks are 30.5M edges and 10.2M cells, ~4× → ~20 ms for the advection in double, plus ~10 ms for settle, remineralise and mix [g]: 24-42 s a generation. Host: the rest of `World.Step` per tank, 0.5-1 ms on a loaded E-core once the field passes have left [g], 192 / 14 threads → 7-14 ms a block (up to 40 ms if the three passes stay on the host) → 8-50 s a generation. Transfers 8 ms a block → 10 s. Pack and unpack of 1,536 small bodies ~2 ms a block → 3 s.

| case | CPU today [m/b] | card, central [g] | card, bracket [g] | gain |
|---|---|---|---|---|
| 96 × 16, 600 s | 5.05 min | ~1.7 min | 0.8-3.3 min | 1.5-6×, central ~3× |
| 96 × 16, 1,500 s | 12.6 min [b, ×2.5] | ~4.2 min | 2-8 min | same ratio; growth repacks every 10 s cost ~1 ms per 100 bodies [g] |
| 2× tanks (384) | 10.1 min [b] | ~2.8 min | 1.5-6 min | 2-7× |
| 4× tanks (768) | 20.2 min [b] | ~5 min | 3-11 min | 2-7× |

**Where the bottleneck lands.** Not PCIe (8-9 ms a block) and not launch latency (physics is 5-30% of a block). It is the serial chain per block: card physics → host world step → card fields, with the host world step and the field passes together 30-80 ms of a block that the CPU does in 250 ms [b]. Both scale linearly with tanks, so the gain saturates around 4-6× however many tanks are added; the physics is the only part that is nearly free to scale. The one structural lever left is pipelining two half-batches of tanks half a block apart so the host's step overlaps the card's (stage 4), which hides the smaller of the two.

**Double on the card for the bodies** [g]: a class-0 body's double step is nowhere near 0115's 15× penalty (that was the 16-link ceiling's 28.6 kB of local memory); in this latency-bound regime I expect 2-5× the single step, 6-90 s a generation of physics, and a generation of 1-3.5 min: still faster than the CPU, and exact against it. Single buys the physics floor, not the world step.

## 3. Precision

**Stays double, at the pace above:** the whole economy (ledgers, bills, light, mouths, corpses, growth) on the host in Core; the stock and the four field passes on the card, with the `M()` rule (`Farm.Gpu/CLAUDE.md`), bit-identical to Core as `GpuTransport` is proven; the settle sums and the work term summed in double on the card (0115). The brain can keep the double helper (`KernelGenerator.cs:47-56`, `(float)Math.Tanh`) at little cost, so a brain fed identical inputs matches the CPU bit for bit.

**Goes single for the speed:** positions, rotations, velocities, the solve, the fluid, the contacts, the drive envelope (doubles on the CPU, `EffectorDrive.cs:116-123`), the senses' inputs. Measured drift of the single whole step [m, 0115]: link positions max 6.7e-4 m at 100 steps, 0.30 m at 1,000, 99th percentile 2.0e-3 m; drive targets 4.5e-7. Over 60,000 steps with contacts and a current the trajectories part completely [b]; what should survive is the distribution of each genome's score.

**The measurement, on `brain2.json`, one recipe:**
1. Card in double on ILGPU's CPU accelerator, then on the card: generation 0 and 1 rows (`tank-generations.jsonl`, `tank-scores.jsonl`, `tank-best.jsonl`) byte-equal to the CPU's, and the per-step digest equal. This separates a port fault from a rounding, and is the registry's `--rerun: identical` verdict (`Registry.cs`).
2. Card in single against itself: twice and at group 32 vs 64, byte-equal rows (the card's identity claim).
3. Card single vs CPU, generation 0 only (the same 96 brains, same seeds): per genome, the mean Net over 16 draws; tolerance: the paired difference below half the genome's own draw-to-draw standard deviation, and Spearman's rank correlation over the 96 above 0.9 [g thresholds, for the owner].
4. Over 100 generations: the best score's trajectory and the exam's control ordering (own vs frozen, full, kinesis) on the card against the CPU's run and, if the owner funds them, two more CPU seeds for a band. "The same evolution" = the card's trajectory lies inside the CPU seeds' band and the exam gives the same sign on every control; nothing finer is claimable.

## 4. The staged build

| stage | what | checks | size [g] | needs the card |
|---|---|---|---|---|
| 0a | the five kernel additions (brain v2, scent, drive sense, bend-and-power, power push with its work term) | transcription in double on the CPU accelerator against `DynamicsWorld.Step` on one nursery tank for 1,000 steps, every array 0 mismatches (0115's pattern); `GpuKernelSourceTests` | 700 lines, 3-5 days | no |
| 0b | the multi-world runner: tank-contiguous slots, per-draw tables, per-tank candidates, multi-world Block | two tanks on the CPU accelerator in double = two CPU tanks, digest and rows bit for bit | 800, 3-4 days | no |
| 0c | the batched field device (transport tank-strided; settle, remineralise, mix transcribed), the `World.Step` hook with its guard, the `Simulation` split, the lockstep loop | `ColumnKernelTests`-style: stock after 300 steps bit for bit on 4 floors; a 2-generation trial recipe on the CPU accelerator byte-equal to the CPU run | 1,000, 3-4 days | no |
| 1 | **the floor test**: bodies only on the card, fields on the host, 1 generation, probe on, single and double, at 192 / 384 / 768 tanks | double = CPU rows byte-equal; single = itself at two group sizes; per-step class-0 times; GPU temperature read every 30 min | half a day of card | yes, after 2026-10-06 |
| 2 | the full path on the card, 2 generations, then 100 beside a CPU run of the same recipe | the drift protocol of section 3 | 1 day card + the CPU run's 8.5 h | yes |
| 3 | 1,500 s with newborn starts; 2× and 4× tanks; the half-batch pipeline | pace table; growth repacks counted | 300 lines, 2 days | yes |

Nothing before stage 1 touches the card; stages 0a-0c are 9-13 agent-days [g] of CPU-accelerator work, one subagent at a time at the owner's load cap, each ending in its fast filtered tests.

## 5. The owner's decisions

1. **Single precision for the nursery's bodies.** Recommend yes, with the fields and the economy kept in double. Implies: a card run is a new realisation; the registry entry carries `engine`, `precision`, `kernelHash` and the rerun identity holds on the card alone; a brain inoculated into a CPU round was selected under a physics an ulp away (the drift of section 3 bounds it); the machine runs the card by day with the heat watch. The alternative, double on the card, is exact against the CPU and by my guess still 1.4-3× faster at 192 tanks: the owner may want stage 1 to measure both before ruling.
2. **Whether to fund the build at all**: ~2,900 lines, 9-13 days before the first card number, for an expected ~3× (1.5-6×) at 192 tanks and up to 4-6× at four times the tanks. Against it: the CPU reads' bit-identical items (`opus-a2-a3.md` #1-#4, about 30% off the physics) and the all-cores heat ruling (1.6×) cost days, not weeks.
3. **The field passes in double on the card** (recommend yes): the economy stays bit-exact with Core; single there would save ~15 ms a block [g] and break every energy reading.
4. **The deferred-flush guard**: a build rule, but it refuses recipes with burial, unheld births or a vertex field; say so in the recipe reader.
5. **An engine ceiling on brain v2's links per body** (mutation adds links at 0.3 a child): 2,048 with refusals counted, as `refusedForClass` is; a refusal kills the body as a counted divergence (`Simulation.cs:493`).
6. **Card time and the hold**: the plan needs none before 2026-10-06; stage 1 wants half a day with nothing else on the machine.

## 6. Risks and unknowns

- The class-0 step time at a small crowd is unmeasured: the probe prints per class but no record holds it. The 0.05-0.3 ms bracket is the plan's largest guess.
- The host's "other world" cost per tank (the 17%) is inferred, not measured; `WorldPhaseProbe` can split it in an hour on the CPU.
- The deferred flush changes `World.Step`'s order for the matter grid's advection (host) relative to the snow's (card) only if something between them reads both; I found nothing at the nursery's config, but it must be proven by the byte-equal trial, not argued.
- Memory: ~1 GB on the card at 192 tanks (edges 260 MB, faces and fluxes 490 MB, stock 81 MB), ~4 GB at 4×; edge buffers can be reused per draw to halve it.
- The exam is 6,912 tank episodes (18 controls × 384 tanks), 36 generation-equivalents, and its hand-wired controls use the per-node brain (`Tank.cs:768-801`), so both brain models must run on the card; the per-node kernel exists.
- ILGPU compile time for a class-0-only build is unknown; the farm's `CompileMs` is in its manifests.
- Scent and drive-sense ports read `_power`, `_holdLeft`, `_powerRate` of the envelope one step stale (`CreatureSenses.cs:290-299`): the kernel must read the previous step's columns, which the Down/Block column split already allows.
- Not determinable from the code: whether `Parallelism.ForRanges` inside Core's `World.Step` oversubscribes 14 tank threads (Core's thread count in the nursery), and the real reason the one-thread profile and the generation wall disagree by 2-3×.
