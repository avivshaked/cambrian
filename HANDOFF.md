# Handoff: where to pick up

*Rewritten 2026-09-22 from the current state. What happened is in the logbook, and why it was
chosen is in [`DECISIONS.md`](DECISIONS.md). This file says only where things stand and what
is queued; it is rewritten, never appended to. The notes it carried before this rewrite are
`logbook/specs/handoff-archive-2026-09-22.md`.*

## Where things stand

**The farm has left Unity.** On 2026-09-21 the owner ruled the engine before the world
("I'd prioritise this before making the world more complex"): pace first, staggered, with
10,000 creatures the committed target and 100,000 a stretch. The same day the agent built
`src/Evosim.Dynamics`, Featherstone's articulated-body algorithm in plain C# doubles with
the fluid ported term for term and a soft sphere contact, and `src/Evosim.Farm`, a .NET 8
console that steps Core's `World` with it; the grid was threaded the next night. All of it
is on main from 2026-09-22 (`9eb262a`, `bd608d8`; the proposal is
`fable-propose-own-solver.md`, its rulings still open). What is measured:

| | |
|---|---|
| round 42 seed 1's world, 30,000 s | 24.4 min at 16 threads (20.5x); Unity took ten hours; `lineage.jsonl` and `positions.jsonl` byte-equal between the 8-thread run before the grid work and the 16-thread one after |
| both books | energy audit closed (peak residual 0.042 J); matter residual −3.2e-04 of 1,500 units, float rounding at the body's account, not a handoff fault |
| identity | digest, lineage, positions and poses byte-equal at 1, 8 and 24 threads; state hash of the grid equal before the change and at 1, 4 and 16 threads |
| parity with PhysX | forty of round 42's jointed bodies alone in still water, 60 s: 40 of 40 within 0.05 rad with PhysX's self-collision off, 12 of 40 with it on; two hand-built strokes agree to 1e-4 rad and 0.1 mm (`scratch/solver-spike/stroker/`) |
| wall split at 1,100 bodies | physics 73%, world 26%, harness 2%: the solver is the ceiling |
| nothing lost | 0 diverged in 3,000,000 steps, both full seeds |

Two findings about what was recorded came with it. **In the Unity farm a body's own parts collide
and a driven joint is mostly not free to turn**, so every round's muscle through 42 was
jammed by its siblings; and **D100's 0.5 s water hold undoes D090**, a neutral body
drifting 22 m from its parcel in 1,000 s where per-link sampling drifts 5 cm.

**Round 42 is read (logbook/0110, five of five at 30,000 s).** Half the matter gave half
the crowd (F1, 5 of 5) and the pace did not follow (F8: 0.36 to 0.74x pinned), the eaters
stayed at zero for the third round running (F5: at most 21 inherited), and joints were
selected out in three seeds of five, with `pairs/body` climbing wherever they survived.
The one seed replayed on the free-joint engine faded the same way (597 to 115 jointed), so
the jam is not the whole cause: in this world a joint costs and earns nothing. The Unity
farm is idle; no arm runs.

**Streams in a shallow tank** (D102) and **four pinned arms** (D103) are ruled and on
main. The Unity build's `simHash` has moved with the header token and every worker needs
a refresh before any Unity arm.

**Proposals in front of the owner:** `fable-propose-own-solver.md` (seven rulings: the
contact caps, the placer's clearance against the bed, D100's hold at 0, the renamed
contact columns, keeping D101, a ball joint's angle, hashes and DESIGN §11.1 superseded;
item 1 is stale, the implicit limit is in) and `fable-propose-animal-kit.md` (order,
bite-takes-reserve, scent, the floor's places, anything wanted sooner).

**Round 43 is read (logbook/0111, three of three at 30,000 s, 2026-09-22 morning):** round
42's world on the new engine with the water per link (D104). The crowd, the treadmill, the
larder, the spread and the eaters' zero are round 42's (G1, G4, G5, G7), the books close
and nothing is lost (G2), and **the joints held in two seeds of three** (476 and 687 of
peaks 675 and 730) where round 42 lost them in three of five: the jam was a cause, and
every reading about joints from rounds 34 to 42 carries that caveat. Three seeds at 8
threads each ran 5.8 to 6.7x, 19x in total, the same as one seed alone at 16, so the
machine is the ceiling however it is divided (G3). The theatre draws the farm's record
(`-From snapshot`, three pictures beside the entry). Two read-script faults found on the
way: `r41d-read.py` assumes a 100 s row where the farm writes one every 10 s (its jointed
body-seconds read tenfold), and the watch prompt ran it without `--budget`, so its snow
read at half; `logbook/specs/r43-read/clauses.py` reads the interval from the rows and is
what the entry used.

**Round 44 is read (logbook/0113, two seeds of three at 30,000 s and seed 1 at 22,370 s,
2026-09-22 night):** the module gene of D106 on round 43's world. The gene is kept in two
seeds and nearly the third (H1), jointed bodies keep it as readily as leaves (H2, by one
to four points), the economy is round 43's in the two seeds that ended (H5) and the books
close to 1e-06 units on the double accounts (H6). The rule is refused hundreds of times
per add and the counter cannot say which reason (H4); H3's famine never came. Seed 1's
world of module chains went deep and to the glass, cost the solver 7.6 times more a
body-step with its overlap pairs and not its links, and ended on `World.Observe`'s
centre-of-mass guard, now the farm's own (`2771bf0`). Round 45 launches on it.

**The 10,000-creature look on the CPU is measured** (`r42x10-s1`, 03:05 on 2026-09-22):
round 42's world at ten times the area and the matter (a 22,000 m² tank of radius 84 m,
15,000 units, `configHash 6470772b`, launched with `run-farm.ps1 -Launcher
rounds/env-r42.ps1`), 5,000 s at 16 threads in 12.2 minutes, 6.8x over the run and
**4.7x at 3,536 bodies** in its last 500 s, nothing lost, the energy audit closed, the
matter residual −2.7e-04 of 15,000 units. The crowd was still climbing (1,697 jointed).
The split at that crowd is physics 53%, world 45%: the solver at 0.32 µs a body-step, the
spike's number, and the 990,000-cell grid a fixed 0.095 wall seconds a simulated second.
Extrapolated, not measured: 10,000 bodies at about 2.4x, a 30,000 s seed in three and a
half hours, where the proposal estimated 1 to 2x. Per body the farm is seven to fifteen
times cheaper than round 42's Unity arms (237 wall seconds per thousand bodies per
thousand simulated seconds in round 43, read with the 10 s sample interval, against
1,600 to 3,500; the 60 first written here was the read script's tenfold error). The grid is the next ceiling at this
size; `SampleEdges`' per-column terms and the serial sums are where to look. Two small
things: the farm's header does not print D102's `axes v:h`, which only `EvolutionRun`
carries, and this tank (depth over radius 0.54) is the first world built by the
relaxation; and a run directory's name is in UTC while these notes are local.

**Three builds landed on 2026-09-22 after round 43** (`08fd8f7` to `7fb748c`): the read
script reads the sample interval from the rows and the farm's header carries `axes v:h`;
the theatre's `-From snapshot` draws every body in its recorded pose from `poses.jsonl`
(`RecordedPose.cs`, the chain walked as the solver's kinematics; label `recorded pose`);
and the grid's edge walk takes the streams' per-column and per-depth terms from a cache,
bit-identical (the pinned hash `1d1ee59f…` unchanged), the world step 1.35x and
`SampleEdges` 1.5x, with a 400 MB working set on the 22,000 m² world (`PrecomputeStreamsTerms
= false` restores the old path). The next grid gain is the edge walk's cell-liveness
lookups, not the potential. The farm exe under `artifacts/` is rebuilt on it; its
`coreHash` and `farmHash` move.

**Package A is in** (`a487491`): `Evosim.Dynamics` is a Unity local package beside Core
(`com.evosim.dynamics`, `noEngineReferences`), the theatre's asmdefs reference it, and
`Evosim.Theatre.EditorTools.DynamicsPackageCheck.Run` steps a body on it inside the Editor
(`ok, digest 2f9b97e450e96b68`); Unity's compiler refused nothing. The Dynamics replay in the
Editor is the next theatre step. **The machine blue-screened at 13:03 on 2026-09-22, and
the dump clears the GPU** (`logbook/specs/crash-2026-09-22.md`): bugcheck 0x3B, an access
violation inside `FLTMGR.SYS`, the file-system filter manager, on a `dotnet.exe` thread,
entered through the Xbox Gaming Services filter `gameflt.sys` with Avast's `aswVmm.sys` at
the bottom of the stack and the display driver on no frame. A Gaming Services update had been
stuck for half an hour and Avast's drivers were replaced at the reboot; the likeliest cause is
the first (inference), and neither is the project's to fix. The GPU route spike
(`scratch/gpu-spike/probe`, ILGPU on CUDA and ComputeSharp on DX12) had run twenty-five
seconds before and was the `dotnet.exe` on the thread, so its first run after resuming is
taken alone, with nothing else on the machine. The owner copied the minidump and the WER
queue into `scratch/crash/` from an elevated shell; `scripts/read-minidump.py` reads a dump
without a debugger.

**The Editor cannot replay the farm's record, and the check that says so is in** (`ade13dd`):
`Evosim.Farm` is a Unity local package too, `TheatreDynamicsReplay` runs the farm's own
`Simulation` loop inside the Editor (no PhysX), and `Evosim.Theatre.EditorTools.
DynamicsReplayCheck.Run` compares it with the recording. Counts agree at every sample;
`auditResidual` parts at the first sample (6.39e-5 recorded, 3.42e-5 replayed) and
`meanHeight` from 20 s, growing to 2e-3 by 300 s. Not the thread count (the farm is
byte-identical at 1 and 8 threads) and not tiering: Mono's floating point and RyuJIT's
differ in a double sum in Core's economy, so a run made by one runtime is a cousin under
the other, as a build change is. The rule that follows: the theatre watches a farm run
from its record (`positions.jsonl`, `poses.jsonl` at the sample cadence), never by
re-simulation, and a smooth picture needs the state stream stage 4 already plans
(poses at a finer cadence, binary). `TheatreRunner` refuses a dynamics run with a message
naming the check; the PhysX Mode B path is untouched.

**The grid's edge walk is cheapened** (`e7a5dae`): per-edge liveness masks built once
with the geometry, index strength-reduction in every per-cell pass, the remineralisation's
bucket lookup tabulated; bit-identical (every hash equal at 1 and 16 threads, the pinned
`1d1ee59f…` inside the default suite), the world step 14 to 18% cheaper on both campaign
grids (171k cells: 19.5 to 16.1 ms at one thread, 4.4 to 3.7 at sixteen; 1.72M cells: 194
to 159 and 35.0 to 30.2). `SampleEdges` is at its floor: 59 ns an edge, most of it the
24-term eddy reduction's dependency chain, which cannot be reordered. The next grid gains
are structural (a compacted live-cell list for the serial remineralisation leg, 1.8 ms
of 30) and small; the world step is done as a target until the GPU. `scratch/grid-walk/
bench.ps1` alternates builds and repeats, because a first single run misread a contended
core as a regression.

**The state stream is in** (`f300ad4`, `logbook/specs/state-stream-spec.md`): `EVOSIM_POSE_EVERY`
(a recording setting, default 0) writes `poses.bin` beside the JSONL, 49 bytes a body at
three dof with the body fraction the JSONL never had, an index and a scan reader that
survives a torn last frame; `scripts/poses-read.py` reads it from the spec alone;
`theatre-snap.ps1 -From snapshot -At <t>` draws any recorded second from it with genomes
from the nearest snapshot before. 1,000 bodies at 1 s for 30,000 s is 1.5 GB. **The owner's
direction of 2026-09-22 afternoon**: the theatre's primary use is live play in Unity from a
recorded second, a cousin being fine, for films (a safari at 5,000, 15,000, 30,000 s) and
the interface's stats. So the sequence is the live view on the new engine (in, `a80b1c9`),
then farm **checkpoints** proven complete by restore-and-continue identity (in, `3561ec3`,
`logbook/specs/checkpoint-spec.md`), then the Runner's checkpoint picker (in, `dab9b78`);
the stream stays at coarse cadence as the round record.

**Checkpoints are in** (`3561ec3`): `EVOSIM_CHECKPOINT_EVERY` (`run-farm.ps1 -CheckpointEvery`,
a recording setting that reaches no config and moves no hash) writes the whole world state,
Core's `World`, every `Creature` of the solver with its brain's recurrent state, the placer,
the harness's bookkeeping and the sampler's baselines, to `checkpoints/NNNNNNNNN.ckpt`, about
1.5 MB at 40 bodies and 4.2 kB a body after. `run-farm.ps1 -ResumeFrom <arm|run|.ckpt> -At <s>`
continues it in a new run directory whose `run.json` carries `resumedFrom`; the config comes
from the resumed run and the four recording cadences from the checkpoint's header. The
acceptance (`scratch/checkpoint/runs/ckA,ckB,ckC`): a restore at 400 s of a 600 s seed writes
the same lineage, positions, poses, absorptive and stats rows as the unbroken run, a run
with checkpointing off is byte-identical to one with it on, and a fresh 300 s seed 1 of round
43's world on the build reproduces round 43 seed 1's rows (`scratch/checkpoint/regress.py`).
A hash mismatch refuses the resume unless `EVOSIM_ALLOW_SOURCE_MISMATCH`, which marks the
manifest. The build moved `coreHash`, `dynamicsHash` and `farmHash`, so round 43's manifests
no longer match the tree and a resume of a round 43 run would be a marked cousin; a run is
still stopped with a `STOP` file, and after a stop the last checkpoint is the second it
stopped at. **The picker is in** (`dab9b78`): the Theatre Runner's live mode takes
`CheckpointPath` and `CheckpointSeconds` (`EVOSIM_THEATRE_CHECKPOINT`, and
`EVOSIM_THEATRE_SEEK` reads as the checkpoint second when a checkpoint is named), restores
through the farm's own `Checkpoint.Resolve`, `World.ReadState` and `Simulation.ReadState`,
draws the bodies at their restored pose and body fraction, and carries on live with the
label `continued from checkpoint at <s> s (cousin)`, naming any of the four hashes that
differ; nothing is refused on a mismatch. `LiveCheckpointCheck` carried ckA from 400 to
600 s in 8.1 s of wall with 20 of 20 samples agreeing on alive, births and deaths and the
doubles parting at the first stepped sample (the runtime, per the Mono gotcha).
`theatre-snap.ps1 ckA -FromCheckpoint scratch/checkpoint/runs/ckA -At 400 -Carry 100 -Views
side,top,close` photographs a continued world with the skin: `-At` is the checkpoint second,
the picture is taken at restore plus carry and named `<arm>-t<second>-ckpt-<view>.png`, and
`-From snapshot` is refused with it. The pictures (`scratch/snaps/ckA/`) show
42 bodies in the top quarter of the water over the tilted bed, the disc filled without a
crust, and a posed two-part body in the portrait, every frame labelled with the cousin
clause. **The interface is up in live mode** (`91aea20`, the evening): every panel answers
from the live world through one path for both engines, the provenance word is always
`COUSIN` with a drift line in place of coverage, ancestry and the dead panel are withheld
as on any cousin, a click selects by ray against each part's box, and `-Chrome` is lifted
for a continuation. `LiveUiCheck.Run` passes 129 of 129 on `unity-w6`, the PhysX check 84 of
84 on `runs/r42smoke` (`r37bsmoke3` is refused now) and the solo check 28 of 28; ten live
states are pictured in `scratch/snaps/ui/ckUi/`. Two things left from it: the founding
live path (no checkpoint named) has no check, and the timeline's `peak · record ends`
label prints over the clock once the head passes the record's end (seen in the selected
state's picture). `scratch/checkpoint/runs/ckA` was refused by the build after the
double-accounts change moved the checkpoint layout to version 2; ckA, ckB and ckC were
re-recorded on the reach-bound build (layout 3, 2026-09-22 night, the acceptance above
passing again). Untested: the interactive Play-mode path.
**The accounts are doubles** (`0c19f0d`, the same evening; CLAUDE.md's farm gotcha has the
detail): the same seed's matter residual read 1.9e-07 units at 3,000 s where the float
build read 1.1e-04 (`scratch/double-accounts/runs/dblA`, `dblB`), the audit the same in
joules, both books closed; a new realisation of every seed, so round 44's base is this
build and round 43 is read as recorded. Left from it: the field's float door (the 1e-7
that remains), and `ParallelIdentityTests` is `Slow` and pins this build's own word, so
thread identity in Core is gated by `-All` only. **Round 44's build is in** (`39dcf3d`,
late evening; `logbook/specs/module-gene-spec.md`'s "As built" section is the report): the
module gene per node with its ceiling, format 7 carrying the four attributes at their
defaults, the add and drop rule in `World.ApplyModuleRule`, the farm's rebuild of a
changed plan by part path, four tunables, five columns, the lineage `ind` flag, WorldState
3, the ledger's `module repay s` line. A zero-chance, zero-threshold world is the recorded
world (`mgA` equals `dblB` at every field of 300 samples); Core 785 and 810 with the slow
experiments, Farm 64. Two fixtures the build orphaned: the thread-identity config
(re-recorded from `mgA`, `855c5d6`, the word unmoved) and the Dynamics crowd fixture
`runs/r42-s4`, re-recorded as `runs/r44fix-s4` (round 43's world, seed 4, 20,000 s) and
repointed in `RunFixture`. **Round 44 is pre-registered** (`logbook/0113`, `3d2cf12`) on
`rounds/env-r44.ps1` with the add threshold screened by the ledger
(`logbook/specs/r44-read/`): a leaf's module repays in 28 s at the surface, so 300 s of
upkeep is a choice about when a body invests, about a child's price. The owner's plan is
at step 4, the animal kit on D106, and step 5, the GPU, is cleared by the crash reading.

**Machine.** i9-13900K, 24 cores, RTX 4090 with 24 GB. The farm takes `EVOSIM_THREADS`;
16 is the measured best at this crowd. The Unity cap stays D103's. Run
`scripts/sweep-orphans.ps1` at every session start; it lists a detached farm run's
`sh.exe`, which is not an orphan.


## The path, ruled

*Kept as written through round 42, with each item's state as it was last noted; the
sequence from here is the queue below, since the engine moved ahead of the world on
2026-09-21 and every later round runs on the new farm.*

D079 (owner, 2026-09-06) set the method: one change at a time, asking D063 of each, a change
that costs the rule read rather than tuned around. D081 set the base and the two bars (*the
goal*, D063's 3 of 5; *the reference*, the base world's own count). D091 (owner, 2026-09-12)
split the changes in two: a *replacement* changes what the world is and becomes the base on
the owner's ruling, its round read for the mechanism and the goal rule not required; a
*treatment* changes one price, sense or rule on a fixed world and joins the base only at its
pre-registered bar. D094 (owner, 2026-09-16) deprecated D063 as the campaign's bar and set
the ladder above; a treatment's bar is written in its own terms. The owner's ten-round plan of 2026-09-11 ("plan the next 10 rounds and
change only if a result compels us"; "proceed autonomously") is the sequence below. The agent
may reorder it when a result compels (owner's grant, 2026-09-11), records each reorder here
with its date and reason, and never adds a world rule: those come to the owner as
`fable-propose-*.md`, absorbed into DECISIONS.md on ruling and then deleted. Rounds 28 to 37
are in the table above; the free joint's reading (round 34: the price was the founding
barrier, the unthrottled stroke removes the joint afterwards) is why the stroke's price moved
ahead of the idle charge.

1. **Round 37b, the water carried as water** (D090, D091; running, above). *Resequenced
   2026-09-12 afternoon by the agent, on the owner's diagnosis of round 37's gathering; amended
   the same evening after the Astra review to carry the transporter, the clearance and the
   corrected `cols`, none a world rule.* Read as a fresh baseline, not as round 37 repaired
   (D091): its changes are not attributed one by one unless a later question needs it.
2. **The one-part control and the fresh seeds on the base.** First `r37bc-s5` (0097's
   control: seed 5 on 37b's build with `fluidAccel 0`, everything else 37b's, so the force
   is the one difference from the streams alone; a replay of a scored condition, agent
   work), launched 2026-09-13 afternoon on worker 6 as the renders hold the other slots;
   read on `diverged` against 37 and the traces' anatomy. Then the fresh seeds (D091; owner
   2026-09-12, "proceed with your recommendations"): round 37b's world on seeds 6 to 10, no
   build, **pre-registered as logbook/0098 (F1 to F8) and queued 2026-09-13 evening**
   (`launch-queue.ps1 -Prereg` on workers 2, 3, 4, 5 and 7 as the renders free them, log
   `scratch/logs/r37b-fresh-queue.out`); read against 37b's five as a second draw of the
   same world, with F5 the joint's question and F6 the throws'. *Moved here from the old queue's item 22 because the five
   founding lotteries have guided nine rounds of adaptive change and round 37's standing
   jointed populations have to be shown to be the world's.* **Done 2026-09-14: both read (0097's addendum, 0099); F1 to F8 hold, the reference bar is ten of ten.**
3. **Round 38, the dilute tank** (D089 rulings 3 and 4): 400 m² with the matter held at 6,000
   units, corpses as objects at 0.005/s; **pre-registered as logbook/0100 (D1 to D8)** on
   2026-09-14 night, its hashes to be recorded at launch; `rounds/launch-r38.ps1` is written
   and launches on the build that carries the two merges. *Repaired 2026-09-13 morning:*
   it had been written from round 37's launcher before D090 and carried no
   `EVOSIM_FLUID_ACCEL`, so round 38 would have run the drag-only centrifuge; it now sets the
   force at 1 and its header comment says to verify `fluidAccel 1`. The dilute arithmetic passed
   (a 5 m matter cell holds 31 units at 0.235/m³; the mask overshoots the disc by 6%). Read:
   nearest neighbour, founding (`mat blk`, `mat short` against births), the downwelling as the
   first patch, sitter against mover. The build queued between the fresh seeds and this
   round (the queue's item 2, `field cv`) lands first, since it moves both hashes.
4. **Round 39, a bed with shape** (was 42): **ruled 2026-09-15 morning (D092;
   `logbook/specs/bed-spec.md`)**: a seeded height map at three scales, the streams'
   potential in floor-following coordinates so the water slows in the hollows, the grid
   masked below the floor, one static collider, relief 0 replaying the flat world; rocks
   and the shelf out. Builds on a branch after round 38's read, validated (constant field
   on the sloped grid, digest at relief 0, the current's divergence and floor flux, a
   settling test, the pace within 15%), pre-registered, launched as round 39. **The Core
   half is built** (2026-09-15 midday, branch `bed`, worktree `scratch/wt-bed`, `4feb648`,
   from `logbook/specs/bed-build/brief.md`; 27 tests, the suite 703 green): `BedShape`
   (three cosine bands at a -1.5 spectrum plus the tilt, mean-zero over the disc, fitted
   to the dial under the slope bound, hollows counted), the grid masked below the floor
   with the array reaching under the mean depth (so `LayerCount` and the refuge layers are
   no longer `depth/cell` in a tank with a bed; the Unity half must read them from the
   grid), and the streams' potential pulled back as a 1-form with the velocity carried by
   the Piola transform (the brief's velocity formula was inverted and the builder caught
   it; floor flux 3e-7 of the RMS, divergence 3e-4 per metre, the acceleration analytic
   to 0.06%, 1.3 times the flat field's cost). Two findings decide the dials: at 400 m² a
   red spectrum under a 30° slope bound gives about 1 m of relief at basins a third of
   the tank (the spec's "a few metres" needs a wider tank), and a tilt of any size spent
   the whole bound and left no hollows, so **the bound was split** (`4ce2ba0`, 2026-09-15
   afternoon; 28 bed tests, the suite 704 green): the bands at 30° on their own, the tilt
   refused above a 25° ramp, the sum allowed and reported as `SteepestTotalSlopeRadians`,
   and the hollows counted on the bands alone (a ramp makes no basin; counted on the whole
   map a 6 m tilt read 0 hollows against 3 on the same seed). The dial table at 400 m²,
   relief dial 12 m so the bound binds, five seeds averaged: scale 7.52 m gives a range of
   1.13 m and 1.6 hollows; 11.28 m, 1.75 m and 0.6; 15 m, 2.26 m and 0.6; 22.57 m, 3.09 m
   and 0.2; the range and the hollow count are the same at tilt 0, 2, 6 and 10 m at every
   scale, and the total slope reads 28.7° at tilt 0, 30 to 30° at 2 m, 34 to 36° at 6 m
   and 40 to 42° at 10 m. Round 39 then runs about 1 m of relief at the default scale
   (a third of the diameter) with one or two hollows and a tilt of about 6 m, a 15° ramp
   with the shallow arc 3 m above the mean depth, far below the lit band. Every config
   before the build is refused by the new `bed` group. **The Unity half is built and
   validated** (2026-09-15 afternoon, `56b2713` on `bed`, from
   `logbook/specs/bed-build/brief-unity.md`): a mesh collider from the height map at half
   a metre (8,712 triangles at 400 m²) over a backstop slab a metre under the lowest rock,
   the glass down to that rock, the placer's clamp read under each candidate after the
   draw (no RNG draw moved), a root more than its radius under the floor killed as a
   counted `Diverged` death whose dump names the bed, `EVOSIM_BED_RELIEF/_TILT/_SCALE`,
   the header's `bed` token carrying the dials and the map's facts on a shaped floor and
   unchanged on a flat one, seven `bed*` manifest fields, `refuge J` read from the grid's
   refuge cells (`GridField.RefugeStock`), two columns `floor low %` and `floor J` (a dash
   on a flat bed), the smoke's part 4b, and the theatre's drape, floor lines and camera box.
   Validation on worker 5 (`scratch/bed-chain.ps1`, log `scratch/logs/bed-chain.out`): the
   shared-space smoke passed with 4b (3 hollows, 1 ridge at a 4 m dial; 200 founders clear
   of the rock under their own columns; a body pushed two metres into the rock killed as
   the guard's death); the 600 s box digest at relief 0 identical to round 38's build over
   all 31 steps; the 600 s tank at relief 0 identical to `r38smoke` on every stats field at
   every sample (its bit-level reference on main's build, `tankdig-r38`, is still to run);
   the bed smoke `r39smoke` (600 s, dt 0.02, seed 3, relief 1 m, tilt 6 m) founded 172
   births against the flat smoke's 166, both books closed, no throws, header `bed relief 1 m
   tilt 6 m scale 7.52 m (hollows 3, ridges 1, range 1.00 m, steepest 36° bands 26°, bound
   clear)`, the hollows holding 1.7% rising to 16.1% of the floor's detritus over the 600 s;
   its pictures (`scratch/snaps/r39smoke/`) show the tilt from the side and a faithful
   replay, and a metre of relief is below what the world views can show (a low-angle floor
   view is a theatre item). The tank digest reference on main's build (`tankdig-r38`)
   is identical to the bed build at relief 0 over all 31 steps, so both of the spec's
   digest checks hold. **The pace** (spec item 8; `scratch/bed-pace*.ps1`, 6,000 s at
   dt 0.02, seed 3, the same worker and load): `tankpace-flat` 407 s of wall per 1,000 s
   simulated at a mean of 921 alive; `tankpace-bed` 482 (18% over) at 843; after one bed
   sample per part per step (`CurrentField.BedSample`, `f102104`) `tankpace-bed2` 455 (12%
   over), bit-identical to the first over 301 digest steps. A Core probe
   (`scratch/bed-build/unity/probe/`) split the rest: the grid's face fluxes paid the
   floor-following pullback at every fixed edge point every step (the per-step transport
   at 1 m cells 27 ms shaped against 9 flat, tilt alone the same as the full map), and
   the water's per-part sample reads 3.4 µs against 2.4. The columns are now precomputed
   once per edge point (`CurrentField.BedColumn`, `GridField` at construction, 105 kB at
   400 m²; `c94a91f`; bit-identical by `ThePrecomputedBedIsTheSameWaterToTheBit`), which
   takes the transport to 9.7 ms, and `tankpace-bed3` (evening, the same load) reads
   **433 s per 1,000 s, 6.4% over the flat run's 407**, bit-identical to the first bed run
   over 301 digest steps. The per-body figure (0.513 against 0.441, 16%) is confounded:
   the bed's realisation of seed 3 carried 8% fewer bodies (843 against 921 on average),
   which is the seed's butterfly and not the bed's cost. Spec item 8 is read as met on
   the whole run's pace. What remains per part is the map's twelve cosines and the
   Jacobian at each part, about 1 µs a part-step; sharing one sample across a body's
   parts would not help (most bodies are one part). `floorStockByFloorDecile` (ten
   shares of the floor's detritus by decile of floor height) joined `stats.jsonl` for
   round 39's E3 (`ef7c85a`), rechecked by smoke on worker 5 (`scratch/bed-recheck.ps1`).
   Merged into main on the evening of 2026-09-15 (`999ee8f`), workers 5 and 6 refreshed.
   **Then the owner saw the pictures and resized the tank (D093, `db15dba`)**: "Can barely
   see anything. And I think we need a much bigger tank. Much." Round 39 runs at 2,200 m²
   (radius 26.46 m), 45 m deep, a 30 m tilt (a 29.6° ramp, the shallow arc at 30 m, the
   lit band's floor, the deep arc at 60 m), relief 1.5 m at scale 17.6 m; the tilt's cap
   is 30°, `EVOSIM_DEPTH` is new, and the shelf is folded into the round. The matter
   budget comes from three 600 s founding smokes at 6,000, 9,000 and 12,000 units
   (`scratch/r39-big-chain.ps1`, worker 5; the water is 5.5 times round 38's, and a 5 m
   matter cell at 6,000 units holds about one child's cost), with the floor's pictures
   through the new `bed` view of `theatre-snap.ps1` (`352a4af`). Then round 39's prereg
   (`logbook/specs/r39-prereg-draft.md`, rewritten for the size and the light, baselines
   from 0101) after round 38's read, and the launch through `launch-queue.ps1 -Refresh
   -ExpectSimHash` from the smoke's manifest.
5. **Round 40, the light's reach: running** (D096, logbook/0106; the light sense that held
   this slot moves down the queue). Then **the shelf** in the lit band L1 defines: a floor
   raised into the top ten metres over part of the disc, the retry of 0102's E9 and E10,
   with births and free matter by side of the edge as the checks on 0105's deepward lean.
   Then the light sense: one new input, light and its vertical gradient; read on jointed
   against rigid against buoyant depth, in a world with something to steer toward.
   Proposal first.
6. **Round 41, the stroke priced alone** (was 39): the work cost back to D082's 0.25 with the
   idle charge still at 0.0001; no build. *First reordered 2026-09-11 morning after round 34's
   first three seeds: the stroke's price shapes what a joint does, the idle charge only what
   it costs to own. Now behind the dilution and the bed, so that there is a chase to pay for.*
   Before it is read on stroke quality, two things (owner 2026-09-12 night, "let's follow
   your recommendation"):
   - **The water that a fin can push on**: lift on a panel (a Kutta-style term in speed
     squared, angle of attack and area, in the drag's own loop) and the reactive force of a
     body bending through water (Lighthill's elongated-body theory, an unsteady added-mass
     term), each a tunable defaulting to 0 so every recording replays. Proposal first (the
     two terms, a validation against a known case such as a flapping plate, the cost per part
     per step), built on a branch, switched on for round 41. The rigid-body engine stays
     unless the throw trace says the throws are its and not ours.
   - **The champion in real water**: DESIGN §5.4's validation harness, one evolved swimmer in
     SPH or lattice-Boltzmann, to say whether a stroke evolution found is real or the
     approximation's. After the two terms have been read once; an instrument, not a round.
7. **Round 42, ellipsoids in the physics** (was 40): spheres and capsules collide and drag as
   their three half-extents; preceded by the offline read of whether boxes have flattened.
   Proposal first.
8. **Round 43, the anchoring cell**: holds a body to the bed or a rock against the current;
   pairs with the bed. Proposal first.
9. **Round 44, shading with a length scale** (was 41): self-shading by the neighbours above
   rather than the patch mean. The least urgent. Proposal first.
10. **Round 45, the remaining prices restored** (the idle charge to 0.02, the neuron and the
    connection to D082's values) on whichever world of 39 to 44 carries joints; no build.
11. **Round 46, the long arm**: one seed, 300,000 s, on the richest standing world, the stroke
    read against the water every 1,000 s; one worker for a week.
12. **Predation on contact** (`fable-propose-predation.md`, consolidated), the first thing a
    brain can be selected for; the owner ruled it too early on 2026-09-11 and it waits behind
    the ten.
13. **The open matter budget** (D074) and the vent, when a round shows the larder binds; then
    **the cell types and immigration**, the archive and the islands.

Standing rulings on the rounds. *Run length* (owner, 2026-09-08): 30,000 s gives 40 to 50
generations and three to four turnovers of the standing crop, enough to read whether a trait
the world contains is kept or lost; whether a wired sense is *used* goes to an inoculated
round (round 15's tool), not a longer one. *Mutation rates stay* (owner, same night): a child
already carries of the order of one structural change per birth, so novelty is not what is
short; the one targeted test allowed is the input-rewiring chance alone, with a control, if
inoculation shows the world keeps a wired sense but never finds one. *The limiter at every
step* was built as a tunable and failed its check (D089; 27 divergences against 17 with the
same signature), so no round runs it. *Every code change orphans every earlier run for the
theatre* (owner 2026-09-09, "a problem we should consider on its own", not to be answered
now): candidates when it is taken up are a tagged build per round kept beside the tree, the
theatre built against a run's recorded commit in a worktree, or a replay-only mode that loads
old formats read-only.

## Queued, in order

Agent work unless marked. Long steps run in the background under the session, never in a
subagent and never in a shell loop.

1. **Done: the farm's tooling.** `run-farm.ps1` and `stop-arm.ps1`'s farm mode, the
   contact aliases in every read (`scripts/reads/contact_aliases.py`), `sweep-orphans.ps1`
   and the launcher's full path to Git's `bash.exe`.
2. **Done: the theatre on the farm's record and the owner's live play.** `-From snapshot`
   draws `poses.jsonl` and the state stream; package A; the Editor replay reads a cousin
   and says so; the live world on the new engine, checkpoints, the Runner's picker and
   `theatre-snap.ps1 -FromCheckpoint`; the interface and click-select in live mode
   (`91aea20`). Left: the founding live path's check, the timeline label past the record's
   end, and one Editor run to verify `Mathf.Sin/Cos/Round` bits against `UnityFloatMath`
   (ckA/ckB/ckC are re-recorded on the reach-bound build, layout 3, acceptance passing).
3. **Done: the base round on the new engine** (round 43, logbook/0111). Its two loose ends
   are closed: `r41d-read.py` reads the sample interval from the rows, and the farm's header
   prints D102's `axes v:h`.
4. **The 10,000-creature look on the CPU**: a measurement, not a round. Ten times the area
   and the matter, 3,000 to 5,000 s, the wall split and the pace read; then the solver's
   serial phases (the contact grid, the water sample, the commit) cheapened if they bind,
   and `SampleEdges`' per-column terms hoisted (1.6 to 2x on the grid, estimated).
5. **The animal kit on D106.** Round 44, the module gene (`logbook/specs/module-gene-spec.md`):
   built (`39dcf3d`), screened and pre-registered (`3d2cf12`), **run and read
   (logbook/0113's read section, 2026-09-22 night)**: `r44-s1..3` on `a782472`
   (`configHash 9b4e275e2a552aa6`, 8 threads each, three at once). H1 2 of 3, H2 3 of 3
   by one to four points on overlapping crowds, H3 unreadable (its famine never came),
   H4 1 of 3 on a clause that fails a window with no adds (refusals per add 22, 704 and
   1,255; one counter for two reasons), H5 2 of 3, H6 3 of 3 with the matter residual at
   1e-06 units. Seed 1 ended `status error` at 22,370 s, censored: creature 7417's centre
   of mass 3 cm below the bed, the root inside; `2771bf0` makes the farm's check read the
   centre and count it as `Diverged`. Its world of module chains went deep and to the
   glass (rim quarter 0.48, `cols` 0.85, `upt lim` 23%) and its solver cost per body-step
   rose 7.6-fold with `ovl/body` (0.026 to 0.82, 99% held) and not with links per body
   (2.05 to 2.95), so the contact pass is the suspect, my reading; the profile from a
   checkpoint decides it, which is why `rounds/env-r45.ps1` records one every 2,500 s.
   Two instruments the next build wants: a per-reason count of the module rule's refusals,
   and H3 rewritten on the reserve itself. The design question behind seed 1, whether a
   chain's contact sphere should be its hull's or its parts', is the owner's. **Round 45's build is in**
   (`8b0d798`, the mouth: the caps in the registry, health as state, the kill by part path
   with `DeathCause.Eaten`, contact from the overlap census once a metabolic step, intake
   from corpses, the four prices, the `Contact` and `Damage` senses; every default is the
   recorded world, `mtA` equals `mt0`; Core 801, Farm 64; `mouth-spec.md`'s "As built"
   section is the report). The ledger screen moved two dials (the note above the spec's
   screen section): `EVOSIM_HEALTH` 13 for a three-step kill, and the leaf's and
   absorptive protection caps to 0.5 because thirty steps behind a cuticle is
   arithmetically impossible and twice the steps is the least a defence is worth; the
   prices are protection 1 W and the other three 0.1 W per unit per m². The launcher is
   `rounds/env-r45.ps1`. The Dynamics crowd fixture is re-recorded on this build
   (`runs/r45fix-s4`, round 44's world with the mouth at its defaults, seed 4, 20,000 s at
   12 threads beside seed 1; Dynamics 79 of 79 on it, `aae6769`), the kill event is in
   (`6fcf93b`, one lineage row per part a bite takes, so 0114's J3 and J7 are readable),
   and the pre-registration is drafted (`logbook/0114`) with the hashes filled. `-All`
   is green on the mouth build (828 of 829 on the first pass, the one failure the
   thread-identity fixture refused for the caps; re-recorded from a 20 s run of the
   build with the module rule at zero, `configHash 676c16483b32d787`, and the word
   `c9b0cabce249c1dd` did not move). Every `.ckpt` on disk is orphaned again by
   `StateVersion` 4. **Round 45's first launch is void** (`runs/r45void-s1..3`, launched
   20:58 from `4300278`, `configHash 4e84dc9f1ac8ecf1`): seed 3 ended `error` at its first
   checkpoint (2,500 s) when the writer refused a body that was a copy of its adult at
   scale exactly 1, and seeds 1 and 2 were stopped at 5,270 and 6,950 s before their next
   (seed 2's manifest reads `running`: the same exception fired inside the stop's own
   checkpoint). The owner then ruled (22:05): **no relaunch until the slowdown is solved.**
   It is solved and regressed the same night (CLAUDE.md's gotcha): the contact grid's
   cell followed the largest body, seed 1 grew a self-copying leaf 14.6 m long, and the
   grid now enters a sphere in every cell it covers, exact and bit-identical (1.9 µs a
   body-step on the ceiling crowd against 9.9). Landed with it: `Phenotype.Scaled(1f)` is
   the body (the writer's fault), the module rule's refusals split by reason (`ref shape`,
   `ref reserve`; `StateVersion` 5, `Checkpoint.Version` 3), and every snapshot row
   carries the body's plan (`moduleCounts`, `lostPaths`) so the theatre and the bench draw
   the body the run stepped. **The owner ruled on the giant and on the tank the same
   night.** No hard bound on a body's size: the economy is to bound it, as it does in
   life, where support and transport grow faster with reach than income does (D107). The
   reach bound (`EVOSIM_MAX_REACH`, header `reach off`) stays in the tree as a development
   guard at 0, tested and never set; the Farm tests' round 42 hash is re-pinned for it
   (`5b93c47344df9e67`), both fixtures are re-recorded (`runs/r45fixb-s4`, `pfix2` →
   `fixtures/r42-config.json`; the identity word `c9b0cabce249c1dd` held), and
   `r45fixb-s4` replays `r45fix-s4` sample for sample, so the tree is the recorded world.
   The next base round's proposal is a **support cost**: each part paying an upkeep in
   proportion to the load it puts on the chain to the root (its mass or its area times its
   distance), which grows as reach cubed against income as reach squared, so a fan of
   copies loses money past a size the price sets and a compact body pays nearly nothing;
   the ledger screen of the 0.5 m leaf against the 14.6 m giant at a few prices is the
   first step. And **the tank is ten times larger and the matter is not** from round 45
   (D108): 22,000 m² at 45 m with round 44's 1,500 units, the bed's wavelength held at
   17.64 m; the grid is not the cost (65 ms a step at 4 threads, `scratch/r45-build/runs/
   bigA`). Diluted tenfold the world does not found (`bigC`: one birth in 1,700 s; the
   ledger's leaf nets 0.015 W at a tenth of the density), and the owner chose the island
   world over a Ks dial (`bigD`, Ks 0.005, founds like round 44): the matter seeded as
   noise islands, founders planted in them, and a light map from the same noise so the
   landscape persists (D109, being built). Two things read with it: the one-sphere
   contact still makes a priced giant a bulldozer, so per-part contact is the contact
   change for the next base round; and a 14.6 m leaf is a smaller fraction of a bigger
   tank. Round 45 relaunches on it, three seeds at 5 threads, the read as planned.
   **The farm's pace is not the solver's**: every full-crowd farm run reads 1.6 to 2.7 µs
   a body-step at 8 and 12 threads alike (round 43 seed 1 included), five times the
   bench's 24-thread number, and the 0.32 µs the record extrapolated from was a founding
   crowd of one-part bodies. The step-phase profile (`wallStep*Ms`, `DynamicsWorld.
   PhaseNames`) is in to say how much of the step is serial; the serial water pass over
   every link is the suspect, and the bench's bodies sample no water.
   Rung B's spec is drafted for the owner (`logbook/specs/scent-spec.md`, four
   questions in it, not built); C and D behind it.
6. **The GPU port.** The probe ran clean alone on 2026-09-22 at 16:13 after the crash
   reading (`scratch/gpu-spike/probe`): ILGPU on CUDA sees the 4090 (SM_89, 128 SMs),
   runs a kernel with a local array and a double kernel with transcendentals; ComputeSharp
   on DX12 dispatches, reports doubles supported, and refuses a local array in a shader
   (`CMPS0025`/`CMPS0032`), so a Featherstone step with its per-link spatial scratch is
   ILGPU's to write. The fork the spike measures: the 4090 runs doubles at about a
   sixty-fourth of its single rate. **The spike ran the same evening** (logbook/0112,
   `spikes/02-gpu-featherstone/`): the per-creature step as an ILGPU kernel with nothing
   cut, exact against the library on the CPU device, bit-identical across launch shapes;
   double is slower than sixteen cores at 10,000 bodies, single is about six times faster
   at 1.4 mm of deviation in ten seconds, and the card is not busy until about 30,000
   bodies. **Ruled the same evening, D105**: single precision, designed for 100,000 and
   first validated at 10,000 to 30,000 with the grid on the CPU, built in parallel with
   the animal-kit rounds. The reduced step is about a quarter of the real one, so the brain
   and the senses are measured as a kernel before the port is sized (next, once the
   Editor checks are off the machine). Then the port proper, with the state stream as the
   theatre's feed.
7. **Loose ends.** Double accounts in Core for the matter residual (a new realisation of
   every seed, so between rounds and pre-registered). `ParallelIdentityTests` (50 s) kept
   or moved to Slow. The overlap probe's `run.ps1` taking its path argument. Close pictures
   beside the whole-tank views in every entry. DESIGN §11.1 and the ArticulationBody
   decision superseded in DESIGN once the own-solver proposal is ruled.


## The decisions in front of the owner

- **The own-solver proposal** (`fable-propose-own-solver.md`): the contact caps' two
  numbers and the per-body cap's effect on momentum; the placer's vertical clearance
  against a perpendicular bed; D100's hold at 0 on the new engine; the renamed contact
  columns; keeping D101 through the base round; a ball joint's angle as the rotation
  vector's component; `simHash` giving way to `dynamicsHash` and `farmHash`; DESIGN §11.1
  superseded. Absorbed into DECISIONS on ruling.
- **The animal kit: ruled, D106** (2026-09-22 evening). The cell is the unit of death;
  the module gene (round 44, `logbook/specs/module-gene-spec.md`); the four priced,
  capped-per-type cell attributes, health as state, the kill making a corpse the killer
  gains nothing from, intake from corpses as the yield (round 45,
  `logbook/specs/mouth-spec.md`); one genome format bump for both; then B, C, D. Two
  numbers inside the rounds are the agent's to set and the owner's to amend, and are put
  in the specs before launch: the per-cell-type cap table and the four prices from the
  ledger. **The owner's standing answer on autonomy** (the same evening): build,
  pre-register, launch, read and write up rounds 44 and 45, run the brain spike and the
  port's first stage, draft rung B's spec but do not build it unseen; message on a
  round's read, a needed decision, or a break.
- **The worktrees**: the owner approved removal; the agent's `git worktree remove` was
  refused by the permission layer as irreversible, so the one-line command is with the
  owner to run.
- **Cloud CPU: off (owner, 2026-09-18 morning: "cloud CPU right now is off. We'll continue
  working on my machine").** The survey stands in `logbook/specs/cloud-cpu-survey.md` for
  the day it is reopened; its finding was that the licence, not the price, is the
  decision. Nothing is to be built for it: no CPU field in the manifest, no script port.
  Round 40's ruling (6 m, "proceed with your recommendations") is D096 and is running.

- **A tempo dial, later (owner, 2026-09-17 afternoon).** The owner asked whether the world's
  metabolic rate could rise so a run holds more generations. Worked through in conversation:
  scale every ecological rate together (upkeep, income, growth, breeding age, senescence,
  corpse decay) and leave the physics alone, and a body's lifetime budget in joules, its
  depletion of its own cell per life and the joule cost of a metre swum are all unchanged,
  so the economics of moving against sitting are tempo-invariant to first order. What
  halves per life is everything that arrives by physics: sinking, the current's carriage,
  dispersal. A sitter's supply per life halves and a mover's reach still covers the tank,
  so on paper the faster world is slightly kinder to movement. The one choice is the
  muscle's price: idle upkeep scales, the joules per unit of mechanical work do not. The
  test is a control pair on one seed, tempo 2 at 30,000 s against tempo 1 at 60,000 s
  (the long arm already queued for the oscillation). The owner's ruling: not now; when the
  world has something worth speeding up. A proposal file then, not before round 39's read.

- **The theatre in person: done.** The owner tried the interface on 2026-09-13 morning
  ("not perfect yet, amazing progress"; good enough for now, the world comes first). On
  2026-09-13 at 23:47 the owner ruled "proceed with your recommendations" on the agent's
  plain-language brief. So the three rulings made under delegation stand: the 1.5 type step
  at 3400 px (build spec item 5), a cousin's lineage fields withheld, the pillow at 0.34
  (0091's addendum). The carve is 0.35 by the owner's eye; carve 0.5 was refused by the
  agent from pictures. Two things the owner raised that morning are open. A joint's moving
  link reads as a ball on screen: the skin's rounding on near-cubic boxes, and a cap on the
  rounding and a key for raw collider shapes are queued. And the Recorder's capture hides
  the interface from the Game View while it records, which is not intentional; it is to be
  reproduced on a worker with a graphics device and fixed in theatre code.
- **Things wrong in motion** (owner, 2026-09-11 evening: "there are some issues with the
  world that you can only see when rendering"), set aside and not yet named. The agent reads
  stills only; when they are named the route is a film of the session with frames pulled at
  the seconds in question.
- **Round 38's two rulings** are no longer open. D089's dilution to 400 m² with the matter
  held at 6,000 units, and corpses as objects at 0.005/s, were confirmed by the owner on
  2026-09-13 at 23:47 with the same ruling.
- **The producer threshold** is unsettled: D063's amendment asks for one living inherited
  member with a recent photosynthetic birth; the scorer prints that, the 10-through-two-
  lifetimes reading and the population-only column reading, and decides on none of them. The
  ruling picks one. With it, **the food chain's meaning under D063** and **the split between
  the lineage rule and a balance rule**, both noted under D063; **a late resource-balance
  rule** needs its own decision with a tolerance.
- **Predation on contact**, in `fable-propose-predation.md`: the injury pool with fixed
  geometry, dt 0.01 from the first screen, stable contact keys, an internal matter reserve.
- **Multithreaded physics** for labelled screens (D078 keeps the setting recorded; one
  sentence). **Extending a passing seed past 30,000 s. The maintenance assay** (a
  multi-genome inoculum with cell-type mutation off), which the owner should scope. **The
  width of the box** (raised by round 28's M5; the dilute tank is the first answer). **Whether
  the absence of CI is a choice.**
- **Speed, and the game's clock** (owner, 2026-09-03): a world eventful on a human timescale
  is a world-rule question. **Immigration as a world rule when the cell types expand**
  (owner's hypothesis, 2026-09-03).
- **The paywalled reading list** in `research/LITERATURE-REVIEW.md` needs the owner's
  institutional access.
- **The review files at the root.** Both Astra pairs (2026-09-07 and 2026-09-12, each with its
  response) are answered; every item they queued is done, ruled or in the queue above. They
  are the owner's to absorb or delete.

## How the experiments are run

CLAUDE.md holds the commands and the gotchas. This is where each tool sits.

| | |
|---|---|
| the farm | `Evosim.Farm.exe EVOSIM_…=…` from a launcher script (`scratch/farm-port/r42.sh <arm> <seconds> [EVOSIM_X=…]` is round 42 seed 1's, the exe under `artifacts/`); `EVOSIM_THREADS` and `EVOSIM_RUNS_ROOT`; stopped by a `STOP` file in the run directory; detached with `Start-Process` on Git's `bash.exe` by full path |
| workers | arms run on `unity-w2..unity-w7`, one per worker, at most five Unity editors at once (the owner's on `unity/` counts), plus a sixth for a short visual check that comes and goes, never beside a test suite (owner, 2026-09-16); after a change under `unity/Assets`, `scripts/new-worker.ps1 -Workers N` once per worker and check the hash; a killed Editor leaves `Temp/UnityLockfile`, which reads as busy until removed |
| launching | a round goes through `scripts/launch-queue.ps1` with `-Prereg logbook/NNNN-….md` (refuses unless the entry is tracked and clean; writes `prereg.json`), `-Refresh` (each worker refreshed as it frees) and `-ExpectSimHash`; `scripts/run-arm.ps1` underneath it; logs in `scratch/logs/`; end an arm with `stop-arm.ps1` and never with a kill; read every setting back from the run header and the manifest |
| renders | `scripts/render-queue.ps1` beside the launch queue, frames at 5,000, 15,000 and 30,000 s into `scratch/snaps/<arm>/`; `scripts/theatre-snap.ps1` for one frame of a live arm (`-WallMinutes 150` on a loaded machine; `-Chrome` for the interface; `-Views close` for a portrait) |
| reading | `scripts/analyse-arm.ps1` by column name (`-ListColumns`), never positionally, `-Columns` as a real array from inside PowerShell; `python scripts/positions-read.py <arm> --summary` for where the bodies are; the per-round reads in `scripts/reads/` |
| scoring | `scripts/clade-score.ps1` for D063, `scripts/absorptive-log.ps1 <arm>` for what a stomach earned, `scripts/lineage-invasion.ps1` for an inoculated lineage, `scripts/ledger.ps1` (D069) before a worker |
| monitoring | one script per round under `scratch/` (`r37b-watch.sh` today): ending, error signature, and a stall on the report's byte size at 30 minutes read as a suspicion; `scripts/monitor-r13.sh` over a watch list is the older form |
| identity | `scripts/compare-det.py` (exit 1 on a difference, 2 missing, 3 unequal coverage) and `digest-diff.py` on a zero-worker pair; `scripts/theatre-check.ps1` for the replay |
| throughput | about 1,800 bodies at dt 0.01 with five arms sharing the machine is five to six hours per 30,000 s; the fluid force at 1 takes the streams' closed form in a tank (2.3 velocity samples per call; M8 of 0095 reads what it costs); the ceilings (`EVOSIM_MAX_POP`, `EVOSIM_MAX_TISSUE`) end a run as a censored runaway |
