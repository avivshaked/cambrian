# B1: which of Unpack's fields the host reads between blocks

A read-only audit of r58-runner at 02c448e6 (scratch/wt-card-r57), 2026-10-10, made by an Opus subagent on c9's brief. c9 then checked five of its load-bearing claims in code: PackBlock uploads `Alive` and `MirrorResizedSinceLastFrame` every block (gpu-runner.template 2482, 2487); `GpuBackend.SyncMirror` is a no-op (GpuBackend.cs 251); a pool relay repacks every central-brain body (gpu-runner.template 2038-2050); the kernel only stores `Tau` and `LimitImplicit` (gpu-kernel.template 1849); a block ends at a metabolic step (Simulation.cs 368-378). The rest is the subagent's reading, unverified by c9; where the subagent itself marked a claim UNVERIFIED, so is it here.

`gpu-runner` below is `src/Evosim.Farm.Gpu/gpu-runner.template`; other files are under `src/`.

## Facts the verdicts rest on

- A block ends at a metabolic step (Simulation.Step caps it), and Runner.Block splits it again at digest steps.
- Only the farm runs the gpu engine (Program.cs:686, `#if EVOSIM_GPU`). FilmWindow, CheckpointFidelity, Nursery, SwimProbe, Bench and the whole theatre never do.
- The card is tank-only (GpuWorld.cs:324-329), so `WrapAtTheSeams` never runs on the gpu path.
- The host's Creature reaches the card only through `Pack(g)` (new body, resize, `EVOSIM_GPU_RESYNC`, every central-brain body at a pool relay) and `PackBlock(g)` (every body, every block; it sends `Alive` and `MirrorResizedSinceLastFrame`). Down columns stay on the card between blocks, and Upload re-sends them from the column host arrays, which hold the last download.
- The card only stores `Tau`, `LimitImplicit`, `Fext` and `Applied`, and never loads them.
- `BrainStep` writes `Signal` and `Bend` before the drive reads them, and the contact pass writes `NOver` and `BedGlass` before the census reads them (both UNVERIFIED in every configuration).

## Table 1: the verdict for each field

| Field | Verdict | Triggers that must unpack it first |
|---|---|---|
| Position, Spin, Velocity | EVERY BLOCK (divergence check and centre of mass each metabolic step) | |
| Alive, MirrorResizedSinceLastFrame | EVERY BLOCK (PackBlock re-sends them; or change PackBlock) | |
| MechanicalWorkJoules, DissipatedJoules, Drive/DragImpulsesLimited | EVERY BLOCK (drained each metabolic step) | |
| Neuron-ceiling count | EVERY BLOCK (a count over column S; no Creature write) | |
| RotationMatrix | EVERY BLOCK (row 1) with `EVOSIM_LIGHT_EXPOSURE`; else ON DEMAND | T1, T11 |
| BasePosition, Q | ON DEMAND (every block at a 0.5 s pose cadence) | T1, T2, T3, T9, T10, T11 |
| BaseRotation | ON DEMAND | T1, T2, T3, T8, T9, T10, T11 |
| Qd, BallRotation | ON DEMAND | T1, T9, T10, T11 |
| Rotation | ON DEMAND | T1, T5, T11 |
| Sang, Slin, Cbias, WaterAcceleration | ON DEMAND | T1, T11 |
| RelativeVelocity | ON DEMAND (steering reads the root link) | T1, T4, T11 |
| Water | ON DEMAND | T1, T6/T7, T11 |
| AppliedTorque | ON DEMAND | T1, T6/T7 |
| Fext | ON DEMAND (dumps only) | T6/T7 |
| Drive history, running sum, cursor, filled; the envelope | ON DEMAND | T1, T11 |
| SignedWorkJoules, PassiveJointWorkJoules | ON DEMAND | T1, T11 |
| Brain previous, current, memory, elapsed | ON DEMAND (Pack resets parity, so the host brain must be current before a repack) | T1, T9, T11 |
| Trace, FirstNonFiniteStep/Link | ON DEMAND | T1, T6/T7, T11 |
| Overlap list | ON DEMAND (digest dump steps) | T5 |
| Held list, body sphere | ON DEMAND | T1, T11 |
| Link spheres | ON DEMAND (not in a checkpoint) | T11 |
| Tau, LimitImplicit, DriveSignal, BendSignal, TouchedBedOrGlass, Stroke | NEVER READ on the gpu path (tests only); Tau and LimitImplicit can also leave the download | |

## Table 2: the trigger points

| # | Event | Where the hook goes | What it unpacks |
|---|---|---|---|
| T1 | Checkpoint | Simulation.State.cs:121 `Dynamics.SyncMirror()` → GpuBackend.cs:251 (a no-op today) | everything `Creature.WriteState` reads |
| T2 | Pose stream frame | PoseRecorder.cs:112-129 | BasePosition, BaseRotation, Q |
| T3 | poses.jsonl row (record format 1) | Row.cs:1518-1540 | BasePosition, BaseRotation, Q |
| T4 | Steering row | SteeringRecorder.cs:93-96 | root RelativeVelocity |
| T5 | Digest row and dump | gpu-runner:1891 | Rotation; the overlap list on dump steps |
| T6 | Divergence post-mortem | Simulation.cs:684 | Fext, AppliedTorque, Water, trace, FirstNonFinite* |
| T7 | Stray handed back | gpu-runner:1934 | as T6, that body |
| T8 | Departure (death, kill) | Reconcile.cs:76-92, before line 86 | BaseRotation (the corpse pose) |
| T9 | Rebuild on a new plan | Reconcile.cs:347-377, before `AdoptStateFrom` | BasePosition, BaseRotation, BallRotation, Q, Qd, brain |
| T10 | Growth resize | Metabolise.cs:446-449, before `solver.Resize` | everything for that body |
| T11 | Repack of a body on the card (pool relay, resync, resize) | top of `Pack(g)`, gpu-runner:2076 | everything for that slot, before Pack overwrites the column host arrays |
| T12 | Tests | GpuEngineTests.cs:163, 235 (others UNVERIFIED) | everything |
| T14 | Probe | gpu-runner:1211 | ContactActive, link spheres (diagnostic) |

Exchange, restore, class change and the theatre's live frame are not triggers: emigrants are copied genomes, a restore packs every body fresh, a body never changes class without a rebuild (T9), and the theatre never runs the gpu engine. The theatre does read gpu-run checkpoints, which T1 covers.

## Risks

1. Pool relays and resync repack from the host Creature inside the runner; without T11 each relay pushes stale state to the card.
2. After T10, Resize rewrites the pose, the spheres and the resize flag, so a per-body "mirror current" flag must be cleared at each flush, and no deferred unpack may run after a host write.
3. Deferred data lives in the column host arrays until the next download or a Pack into the same slot. A freed slot is reused after T8 and T9 have run.
4. `IStepBackend` has only `SyncMirror(world)`. T6 to T10 are per body, so it needs a per-body variant or a whole-world flush at each.
5. `WriteCheckpoint` runs Reconcile's departures (SettleBeforeCheckpoint) before T1, so T8 is its own hook.
6. `Creature.WriteState` is an explicit writer, not reflection. CheckpointFidelity's reflection is CPU-only.
7. The eager set depends on configuration: light exposure, a 0.5 s pose cadence, and digest rows each add fields.

## The size of it

Per link, Position, Spin and Velocity (9 doubles) and a few per-body scalars stay eager. About 59 doubles a link, the per-degree-of-freedom arrays, the brain and the trace (63 more a link on traced bodies) can wait. The mirror's 4.1 to 5.4 ms a block (r57-scatter58-card1's probe) would fall roughly in proportion to what is no longer read, which is an inference until a probe sitting splits the mirror by field group.

## The never-read fields: parked

Branch `mirror-lean` (scratch/wt-mirror's first branch, still at 02c448e6, nothing committed) was to drop Tau,
LimitImplicit and the drive and bend signals from Unpack, and Tau and LimitImplicit from the download. The
coordinator parked it on 2026-10-10. The gain is about 4 of the ~90 values a link and degree of freedom that Unpack
reads, roughly 0.2 ms a block or 0.2% of pace (inference). The engine tests (GpuEngineTests.Snapshot and
FirstDifference) compare Tau and DriveSignal between the CPU and card engines, so dropping them needs a test-only
full-mirror switch, and the default build would lose that direct check. If the mirror probe shows B1's deferred
set is worth building, the full-mirror switch comes with B1 and pays for itself there.
