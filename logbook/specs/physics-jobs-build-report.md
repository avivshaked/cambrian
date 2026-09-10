# Build report: single-threaded physics by default

*Builder's report against `logbook/specs/physics-jobs-spec.md`, 2026-09-06. I committed nothing
and pushed nothing. Every run below is on `unity-w7`. Workers 2, 3, 4 and 6 were carrying
round 27 throughout and were still carrying it at the end; I touched none of them.*

## What it does now

The physics step runs on the main thread unless somebody asks for otherwise. A new runner
setting, `EVOSIM_PHYSICS_JOBS`, defaults to 0 and sets Unity's job worker count before the
world is built. Every run records the count the job system reported back, in its manifest
and in its header. A reader can then tell a recording that can be watched again from one
that cannot. The theatre reads that number out of the manifest and sets it before it
rebuilds the world.

Two runs of one shared world at the new default are identical over all 3,001 state digests
and all 30 statistics samples. The same world at fifteen worker threads parts from them at
step 152,500. And the theatre replayed a shared-world recording for the first time: 30 of 30
samples, no mismatch.

## What changed

| File | Change |
|---|---|
| `unity/Assets/Evosim/Sim/Ecosystem.cs` | +51 lines. `ConfigurePhysicsJobWorkers` and `JobWorkerMaximum`, beside `ConfigurePhysicsStep`, which is the other thing a caller must set before building a world. |
| `unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs` | +60 lines. `EVOSIM_PHYSICS_JOBS` read beside the digest settings, applied before `new Ecosystem`, and carried into the manifest and the header. |
| `unity/Assets/Theatre/RunRecord.cs` | +28 lines. Two optional manifest fields, read as null when a run predates them. |
| `unity/Assets/Theatre/TheatreReplay.cs` | +40 lines. Sets the recorded count before building the world, restores the process's own on dispose, and exposes the caveat when the manifest is silent. |
| `unity/Assets/Theatre/TheatreRunner.cs` | +18 lines. The thread count rides on the faithfulness line of the overlay. |
| `unity/Assets/Theatre/SoloCreature.cs` | +9 lines. Mode A sets 0 and restores on dispose. |
| `unity/Assets/Theatre/Editor/TheatreIdentityCheck.cs` | +2 lines. The headless check logs the count it is running at. |
| `scripts/run-arm.ps1` | +10 lines. Prints the manifest's count with the other identity lines. |
| `rounds/launch-det.ps1` | A `-PhysicsJobs` pass-through, unset by default. |
| `scripts/theatre-check.ps1` | New. Runs the headless identity check on one worker, with the same two-processes-on-one-worker refusal `run-arm.ps1` makes. |

Nothing under `src/Evosim.Core` was touched. `git status` on that path is empty, and every run
below records `coreHash 52eb64962a26a8b81572fa1088bb62fbfd7a541fbb1580e202bafacb1f275521`,
which is round 27's. `scripts/style-check.py` shows as modified in `git status`; it was
already modified when I started and I did not edit it.

**Worker 7's new `simHash` is
`d9fc3fc192298332552b430af5863d79018df7142db30a05bf6033092eafe36d`** (it was
`2c964296a2ba893c652b31c58fe33d42a9a6ab625c69ad492c9f126b4eee40fc` after the digest build).
Worker 7 was refreshed with `pwsh -NoProfile -Command "./scripts/new-worker.ps1 -Workers @(7)"`
once the edits had landed, and every launch afterwards carried `-ExpectSimHash d9fc3fc19229`,
so nothing in this report can have come off a stale tree.
`unity-w7/ProjectSettings/DynamicsManager.asset` is byte-identical to the main tree's again;
I checked the two file hashes rather than trusting the copy.

## What a run writes

The manifest gained two fields, next to the timestep because they answer the same kind of
question about the solver:

```
"physicsDtSeconds": 0.01,
"metabolicStepSeconds": 0.5,
"physicsJobWorkers": 0,
"jobWorkerMaximum": 31,
```

The header gained one token, appended last before the hash:

```
… · senses jointangle,jointrate,up,depth · … · physics jobs 0 · configHash `a521eff52dc063fd`
```

The setting is not a `RunConfig` tunable, so it reaches neither `config.json` nor its hash.
The tiled run below carries the same `configHash a521eff52dc063fd` that `fl-replay` recorded
before this build existed, and the shared runs carry `ca86fa92bc5eb3fa`, which is `det0-a`'s.

## Two surprises

This machine gives Unity thirty-one job worker threads rather than fifteen. The ceiling
`JobWorkerMaximumCount` reads 31, and the count a process starts with is also 31. The
processor is a 13th-generation Core i9-13900K with 24 cores and 32 logical processors, so 31
is one per logical processor bar the main thread. Logbook 0069 says the machine has 16
logical cores and that Unity used 15 workers. That is the one number in 0069 I cannot
reproduce. The finding it supports still stands. Those probes set the count on the command
line rather than reading it back, so `det5-a` and `det5-b` really did run at 0 and really
were identical, and `det4-*` really did run at 1. The old default was 31 rather than 15, and
I have left the logbook alone for the owner to correct.

The clamp never fired: I asked for 15 on one run and got 15 back, and 0 on the rest and got
0 back. Nothing was refused, so the spec's question about what happens when 0 is rejected
has no answer beyond "it was not rejected here".

## Validation

### The Core suite

```powershell
./scripts/core-test.ps1                       # Failed: 0, Passed: 530, Total: 530, 1 m 12 s
./scripts/core-test.ps1 -Filter RunConfig     # Failed: 0, Passed: 27, Total: 27, 182 ms
```

`RunConfigTests` and `RunConfigJsonTests` are green, which they have to be: the setting is
read in `EvolutionRun` and never becomes a tunable, so it cannot reach `Hash()`.

### The tiled world does not move

The tiled world never depended on the thread count, because no two creatures ever touch and
so no two are ever solved together. The new default must therefore change nothing there.

```powershell
pwsh -NoProfile -File rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000 -Name jobs-tiled
python3 scripts/compare-det.py jobs-tiled fl-replay
```

`jobs-tiled` against `fl-replay`: **10 shared samples, 84 fields each, 0 differing.** Same
`configHash`, and the run directory's name carries it. The header reads `physics jobs 0` and
the manifest reads 0 of a maximum 31.

### The shared world replays at the default

Two runs of seed 4 for 3,000 s, one after the other, with a state digest every 100 physics
steps and no Unity command-line arguments at all. The code's own default is what has to make
them agree.

```powershell
pwsh -NoProfile -File rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -DigestEvery 100 -Name jobs-a -ExpectSimHash d9fc3fc19229
pwsh -NoProfile -File rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -DigestEvery 100 -Name jobs-b -ExpectSimHash d9fc3fc19229
python3 scripts/digest-diff.py jobs-a jobs-b
python3 scripts/compare-det.py jobs-a jobs-b
```

```
jobs-a vs jobs-b: identical over all 3001 shared digest steps (to step 300000, t=2999.9999329447746 s)
jobs-a vs jobs-b: identical on all 30 shared samples (to t=3000); contacts/step at end 552.7578
```

Both headers carry `physics jobs 0` and both manifests carry `"physicsJobWorkers": 0`. Both
worlds ended with 420 births and 412 alive. Both were running 553 pairs of touching bodies
per step by the end, so the world had every chance to fork and did not.

### The old behaviour is still there

```powershell
pwsh -NoProfile -File rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -DigestEvery 100 -PhysicsJobs 15 -Name jobs-15 -ExpectSimHash d9fc3fc19229
```

`jobs-15`'s manifest reads `"physicsJobWorkers": 15` and its header reads `physics jobs 15`.
Against `jobs-a` it holds to **step 152,400 and differs from step 152,500 (t = 1,525.0 s)**,
with 122 bodies alive on both sides. The statistics rows follow at t = 1,600, differing in 48
fields at once. That is the same window 0069 found at fifteen threads.

### The theatre replays a shared run

This is the first time the theatre could be faithful to a shared-world recording. I recorded
`th-shared` with `jobs-a`'s settings and then ran the headless check against it.

```powershell
pwsh -NoProfile -File rounds/launch-det.ps1 -Seed 4 -Worker 7 -Seconds 3000 -DigestEvery 100 -Name th-shared -ExpectSimHash d9fc3fc19229
pwsh -NoProfile -File scripts/theatre-check.ps1 -Run th-shared -Worker 7
```

```
[Theatre] runs\th-shared\2026-09-06-220239-ca86fa92
  arm th-shared, seed 4, dt 0.01, config ca86fa92bc5eb3fa
  source: identical to the recording
  physics jobs: 0, as recorded
  30 recorded samples, last at t=3000 s; replaying to t=3000 s
[Theatre] identity check: identical on all 30 samples compared
  30 matched, 0 skipped, of 30 recorded
  replayed 3000 s in 5.42 min (9.2x real time), 412 alive at the end
```

Thirty samples of thirty, and exit code 0. It is a faithful replay. The source hashes match,
the recorded thread count was read from the manifest and applied, and the five compared
columns agree to the bit, the audit residual accumulated over 300,000 steps included.

`th-shared` is also bit-identical to `jobs-a` on all 3,001 digests, which was free and worth
having: three separate runs of that world now agree to the last bit.

The theatre wrote nothing into the run directory. I hashed the whole directory tree, contents
included, before and after the check: `602148f35dddca171673a5093a6cc721c260ca73b44dbf9b6034a4046754bff8`
both times, with the same nine files.

### The caveat, on two runs recorded before today

Not asked for, and it is the only test of the other half of item 2. A recording whose
manifest does not say how many threads it ran on is treated as the old default. The theatre
says so in the same line that reports a source mismatch.

`fl-replay` is tiled, 2,000 s, recorded before this build:

```
  source: simHash 1f5455f48515… recorded, d9fc3fc19229… here
  physics jobs: 31 — physics threads unrecorded — not a faithful replay after the first contact fork
[Theatre] identity check: identical on all 20 samples compared
```

So the caveat is conservative: nothing in a tiled world ever touches, and the replay matched
anyway at 31 threads.

`det5-a` is shared, 3,000 s, and was recorded at zero threads through `-job-worker-count 0` on
the command line, which its manifest could not know:

```
  physics jobs: 31 — physics threads unrecorded — not a faithful replay after the first contact fork
[Theatre] identity check: MISMATCH — t=1400: audit 0.00091212819097563624 recorded, 0.0012825691956095397 here
  13 matched, 0 skipped, of 30 recorded
```

Thirteen samples, then a fork at t = 1,400. The caveat is telling the truth. Both of these
needed `EVOSIM_THEATRE_OVERRIDE=1`, because the source hash has moved.

A third run recorded before this build, `th-ref`, could not be opened at all. Its config
predates the `surfaceRestoringFraction` tunable, so §9's refuse-rather-than-default rule
turns it away before the theatre gets to the threads. CLAUDE.md records that gotcha about
older configs already, and this change did not cause it.

### What it costs

Wall minutes for one 3,000-s run of seed 4 on worker 7, with four round-27 arms sharing the
machine the whole time:

| Run | Job worker threads | Wall min | Births | Alive at end | Contacts per step |
|---|---|---|---|---|---|
| `det0-a` (before this build) | 31, unrecorded | 5.39 | 419 | 407 | 160 |
| `det5-a` (before this build) | 0, by command line | 6.42 | 437 | 431 | 167 |
| `jobs-a` | 0 | **5.80** | 420 | 412 | 155 |
| `jobs-b` | 0 | 5.60 | 420 | 412 | 155 |
| `th-shared` | 0 | 5.38 | 420 | 412 | 155 |
| `jobs-15` | 15 | 4.75 | 420 | 410 | 154 |

`jobs-a` took 5.80 minutes, between `det0-a`'s 5.39 and `det5-a`'s 6.42. Its own two
siblings at the same setting came in at 5.60 and 5.38. The spread within one setting is
about as wide as the gap between settings, so this measurement is too noisy to price the
change at this population. The comparison I would stand behind is the run at fifteen threads against the three at zero:
4.75 minutes against 5.38 to 5.80. Going single-threaded costs about 15% of wall clock at
120 to 400 bodies. Round 28's population is where the number that matters will come from.

## Runs left on disk

`runs/jobs-tiled` (tiled, 1,000 s), and `runs/jobs-a`, `jobs-b`, `jobs-15` and `th-shared`
(shared, 3,000 s each, with digests). All five ended `status ended`, `reason budget`,
`divergedTotal 0`. Nothing was stopped and `stop-arm.ps1` was not needed. Unity logs are in
`scratch/logs/`.

## What I could not do

- Say what happens when 0 is refused. It was accepted on every run, so the clamp is untested
  against a real refusal, and untested above the ceiling except by reading the code.
- Price the change at a round's population. At 120 to 400 bodies the run-to-run spread
  swallows the difference, and the machine was carrying four other arms.
- Check the overlay with eyes. The headless check runs the same loader, the same world
  construction and the same comparison. The Play-mode HUD is a different loop around them, and
  I could not open the Editor on a worker that had a batch process on it.
