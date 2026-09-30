# The farm: what will bite you

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before launching, queuing, resuming or stopping a farm run, before touching checkpoints, `Evosim.Dynamics`, the manifest or its hashes, and before comparing a number across the farm and the Editor.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## A rule for the owner

- **A round's checkpoints are thinned only after the owner approves its video as final**
  (the owner, 2026-09-26). A farm round writes a checkpoint every 100 s, 3.6 to 5.1 GB a seed
  at round 49's crowd, three quarters of it checkpoints. They stay whole while the round is read
  and filmed. Only when the owner has said the round's video is final are they thinned to one
  every 1,000 s, plus the checkpoint at or before each scene's start (the owner's addition, the
  same morning; about 30 a video) and any an entry cites. The keep list is built from the
  video's own scene table, and the agent shows it and the space it frees and asks before each
  thinning, since a deletion cannot be undone. Two reductions lose nothing and need no ruling:
  a checkpoint that points at `genomes.jsonl.gz` instead of copying every living genome (a
  quarter of each file), and the feeding log (`absorptive.jsonl`) compressed. Round 49's
  measurements are in HANDOFF.

## Gotchas

- **From 2026-09-22 there are two farms, and the one out of Unity is the one that runs.**
  `src/Evosim.Farm` is a .NET 8 console that steps Core's `World` with `src/Evosim.Dynamics`
  (Featherstone in doubles, one creature at a time, bit-identical at any thread count;
  `fable-propose-own-solver.md` until it is absorbed). It binds the same `EVOSIM_*`
  variables as `EvolutionRun`, plus `EVOSIM_THREADS` and `EVOSIM_RUNS_ROOT`, and reproduces
  a launcher's `configHash` (round 42's `ff557bce…`) and its `config.json` byte for byte.
  Round 42 seed 1's world ran 30,000 s in 24 minutes on it at 16 threads, both books
  closed, against ten hours in Unity. Eight things bite. **The manifest has no `simHash`**:
  `run.json` carries `engine: "dynamics"`, `dynamicsHash`, `farmHash`, `threads` and
  `processId`, and a run is stopped by a `STOP` file in its run directory, which
  `stop-arm.ps1` writes when the manifest says `engine: "dynamics"` (`-RunsRoot` for a run
  outside `runs/`). **A farm run can be continued from a
  checkpoint, and the continuation is the run** (`3561ec3`, `logbook/specs/checkpoint-spec.md`):
  `EVOSIM_CHECKPOINT_EVERY` writes `checkpoints/NNNNNNNNN.ckpt`, a recording setting that
  moves no hash, and `run-farm.ps1 -ResumeFrom <arm> -At <s>` writes the same rows from that
  second as the unbroken run would have. Three things about it bite. The config comes from the
  resumed run and the recording cadences from the checkpoint's header, so a launcher's
  environment is ignored except for a cadence it sets by name. A resume across any of the
  four hashes is refused unless `EVOSIM_ALLOW_SOURCE_MISMATCH`, and is then a cousin the
  manifest marks. And the last checkpoint after a `STOP` is the second the run stopped at,
  because the file is written after the report row and before the stop is acted on.
  **A checkpoint carried everything the solver reads and not everything a sense reads,
  until `StateVersion` 6** (2026-09-23): `Organism.PartDamage`, the health each part had
  lost and what the `Damage` sensor channel reports, was not written, so
  every restored wounded body sensed nothing, and a resume of round 45 seed 2 parted from
  the run at its first sample in the two jointed bodies among sixteen wounded whose brains
  read the channel (six of 1,925 from the 5,000 s checkpoint; one body both times). The
  row acceptance never saw it because nothing in round 42's world bites. The check that
  names such a member is `Evosim.Farm.exe --verify-checkpoint <run dir or .ckpt> <seconds>`
  (`CheckpointFidelity`): it founds or restores a world, steps it, writes it, restores that
  and compares the two member by member, skipping by name what a step fills before it
  reads; run it on a world that has the thing you added (a bitten crowd, a grown one)
  before trusting a resume of it, and every `StateVersion` bump refuses every checkpoint on
  disk, round 45's included, which are cousins for that reason anyway. A new member of the
  harness or the solver world that a step fills before it reads goes on the check's skip lists
  (`HarnessNotCompared`, `DynamicsNotCompared`), or the fidelity tests fail on its length (the
  census's slabs, 2026-09-25; the divergence check's verdicts, 2026-09-26). **It happened again,
  and `StateVersion` 11 and `Checkpoint.Version` 6 close it** (2026-09-25). The contact flag
  (`Organism.PartContact`) stayed set until a plan change then, and the writer left it out. The
  restore also left each body's contact and damage senses unwired until its first metabolic
  step. Round 48's resume parted from the run at its first sample for it, in jointed bodies
  whose brains read contact. The check missed both: it skipped every sense, and it had the
  flag on its list of what a step fills before it reads. It now compares the senses and the
  harness's own members, then steps the two worlds side by side for two metabolic steps. Two
  more faults came out of that. A checkpoint taken between growth steps restored a growing
  body at its organism's size, one the solver had not been given yet; it now carries the size.
  One taken after a bite and before the growth step that rebuilt the body could not be
  restored at all. The loop deferred a cadence checkpoint to the next growth step, and wrote
  no last one at a stop or a wall in between. A cadence that is a multiple of the growth step
  never met it. The bite rebuild below closed that window.
  The fix was version 5 on its branch, and so was the record's
  gzipped checkpoint on another; the two met at round 49's merge as version 6, the new payload
  gzipped, which both records write, and a file saying 5 is refused by name because it could
  be either layout. Round 48's files are version 4 and still open, lossily. A
  farm resume refuses one unless `EVOSIM_ALLOW_SOURCE_MISMATCH` is set, and then marks the
  run a cousin. The theatre labels one as a cousin. The fixtures ckA, ckB, ckC, `ckUi` and
  the theatre's live fixture need re-recording on this build. From round 49 (D123) both
  records are the last metabolic step's alone, zeroed in place at the top of the mouth's
  pass. The layout did not move, because the physics steps after a restore still read them
  before the next metabolic step rewrites them. Round 49's instruments then took
  `StateVersion` to 12, since queued lineage rows carry the death row's `ga` and `res` and a
  founder's landing readings, and 11 is refused. **A bitten body is rebuilt on the step that
  bit it, from round 49's bite rebuild** (2026-09-25). Until then the farm rebuilt its solver
  only at the next growth step, up to ten seconds later. In that window the contact list
  named the old plan's links, so a bite could land on the wrong part. The brain read senses
  indexed by the new plan, and a checkpoint could not be restored. Now the farm rebuilds it
  straight after the world's step, before any physics step, as it builds a newborn, and the
  checkpoint deferral stays as a guard that never fires. A new path that changes a plan must
  rebuild the body before the next physics step and take the organism's part map as it does
  (`TakePartMapFromPreviousPlan`). Core composes a pending map with the next change's, so a
  map left behind corrupts the next one. The step's contact and damage records now go through
  the same map, where they were dropped. A surviving part keeps what it felt, and the rebuild
  hands the body the carried arrays, a module rebuild at the growth step included. From this
  build on, a world whose plans change with a sense open writes a different world state. Its
  trajectory moves only where a body that changes plan has a brain reading either channel. **The
  JIT decides the bits**: .NET's
  tiered compilation gives quick-JITted and optimised loops different floating-point
  results on a rounding edge, so every project that reports a digest sets
  `<TieredCompilation>false</TieredCompilation>` and `DynamicsWorld.Step` takes one
  `Parallel.For` path at every thread count, including one; a serial `for` beside it
  parted from it at one thread. **.NET 8 and Mono format a float differently under
  `"R"`** (17.641891 against 17.6418915), and that formatter feeds the config hash; no
  recorded run is affected, and a hash read from a Unity build is compared to a farm's only
  through a launcher both have written. **The contact instrument is renamed**: the farm's
  contact is a soft push between spheres, so its columns are `overlaps`, `ovl/body`,
  `ovl jnt %` and `ovl held %` and its stats fields `overlapPairs*` and `bedOrGlassBodies*`;
  the numbers do not compare with `pairs/body` across the change; `analyse-arm.ps1` and
  the Python reads (`scripts/reads/contact_aliases.py`, one table for the columns and one
  for the fields) resolve either name and print a note saying which engine's census was
  read. **No recorded run replays on it**, and none of the farm's
  replays in the Unity theatre; a `poses.jsonl` beside `positions.jsonl` is the first
  bridge. **In the Unity farm a body's own parts collide and a driven joint is mostly not
  free to turn** (the parity swims, HANDOFF 2026-09-21): forty of round 42's jointed bodies
  alone in still water agree with the new solver 40 of 40 within 0.05 rad with PhysX's
  self-collision off and 12 of 40 with it on, and a hand-built stroke agrees to the fifth
  decimal; so every round's muscle through 42 was jammed by its own siblings, and a
  reading about joints from those rounds is read with that. **D100's hold undoes D090**: a
  neutral body under a 0.5 s water hold drifts 22 m from its parcel in 1,000 s, and 5 cm
  under per-link sampling; a hold of 0 on the new engine is an owner ruling in the
  proposal. **The matter residual grows on the farm** (−3.2e-04 of 1,500 units at
  30,000 s against Unity's ±1e-04), and it is float rounding at the body's account
  against the fields' doubles, not a handoff fault: the farm's `Observe` call equals
  Unity's line for line. **From `0c19f0d` (2026-09-22 evening) the accounts are doubles
  and the residual closes**: `Organism.Energy`, `TissueJoules` and `AdultTissueJoules`,
  the ledger's sums and every booked sum in `World` (the reserve update, `Grow`, `Bury`,
  `Conceive`'s price, whose 100 J overhead against a 0.4 J body rounded 1e-5 J a birth
  into neither book) are doubles; the same seed read 1.9e-07 units at 3,000 s where the
  float build read 1.1e-04 (`dblA`/`dblB`, a factor of about 590), and the old build's
  two residuals were one fault in two units (−1.097e-02 J over 100 J/unit = 1.097e-04
  units, exactly). It is a new realisation of every seed: round 43 does not replay on
  it, `BoxPathTests`' golden and `ParallelIdentityTests`' word are re-pinned, and every
  checkpoint on disk is refused (`WorldState.StateVersion` 2, `Checkpoint.Version` 2). What
  stays float, deliberately: the ledger's nine per-step terms (each handed to the field's
  float door exactly as stored, so widening them would add a rounding), the genome's
  traits, `PendingWorkJoules` (both sides of its booking read the same float), and the
  sensor and HUD readings. **A float still enters a booked sum at exactly one place, the
  field's door**: `IMatterField.Deposit`/`Take` are float over double cells, the same
  float is booked on both sides of every transfer, and the half-ulp left at the hand-over
  is the 1e-7 that remains. Widening the field API is the next step if a round needs it.
  **`ParallelIdentityTests` is `Slow` now and pins this build's own word** rather than
  the pre-threading build's, so Core's thread-identity gate is `-All` and nothing in the
  default run. **`sweep-orphans.ps1` lists a detached farm run's `sh.exe`**
  as an orphan; it is one exiting script, not a loop, and is not killed. **From PowerShell,
  `bash` is WSL's** (`C:\WINDOWS\system32\bash.exe`) and cannot see `D:/`; a detached
  launcher names `C:\Program Files\Git\bin\bash.exe`. **The water pass runs across the
  world's threads from 2026-09-23** (`c6cbba8`): it had been serial for `CurrentField`'s
  sake and was 41% of a step at 1,800 bodies on five threads, more than the bodies' own
  phase; it now runs under `CurrentField.PinInstant`, which fills a slot for the samplers'
  phase `2π·s/P` *and* for the analytic acceleration's `(2π/P)·s` (the two differ by an ulp
  on half the steps of a run, and each grouping is what its recorded worlds replay on),
  and every lookup under the pin is a read. Bit-identical: a resume of round 45 seed 2's
  2,500 s checkpoint on the serial and the parallel build agrees in 4,290 values over 30
  samples. A new current mode has to be samplable under a pin, which means no per-call
  memo the samplers read back through fields; the bed's one-entry memo is thread-static for
  that reason. The Unity farm's water pass is untouched and still on its main thread.
- **Mono and RyuJIT do not agree on a double sum, so the Editor cannot replay a farm run**
  (2026-09-22, `ade13dd`). `Evosim.Farm` and `Evosim.Dynamics` are Unity local packages, so
  the Editor compiles their folders as netstandard2.1 at C# 9 without `EVOSIM_GPU`, whatever the
  farm's own net8.0 build accepted; `dotnet build src/Evosim.UnityProxy -c Release` compiles the
  Farm's folder that way, before a merge of Farm code only the farm has built (2026-09-26).
  `DynamicsReplayCheck` runs the farm's own loop in the Editor: the counts agree at every
  sample and `auditResidual` parts at the first one, before any body has moved. Not threads,
  not tiering: the runtime. A farm run is watched from its record (`positions.jsonl`,
  `poses.jsonl`), and a farm-side identity claim is made on the farm (`digest.jsonl` at 1
  and N threads), never across the two runtimes. **The library functions agree across the two
  runtimes and the float arithmetic does not** (2026-09-23, `FloatMathCheck` in the Editor
  against `Evosim.Farm --float-math`, one integer-generated sweep of four million floats and a
  million angles): `Mathf.Sin/Cos/Sqrt/Round/Floor/FloorToInt/CeilToInt` are bit for bit the
  farm's `UnityFloatMath` transcription in the Editor and their digests equal .NET 8's, so the
  transcription is exact; but Mono holds a float expression's intermediates wider than a float
  and rounds only at an explicit cast (the cast-at-every-operation form's digest equals .NET's,
  the inline form differs from it in 8% of distances and 25% of `(float)i / n * 2pi` angles by
  an ulp), and Unity's own `Vector3.Distance` is a third evaluation, an ulp from the inline
  form in 0.8% of triples and from the farm's in 8%. `SharedVolume`'s fold
  `d - extent * Round(d / extent)` agrees everywhere because its intermediates pass through a
  float parameter. So the farm's placer and the Editor's differ by an ulp in a distance now
  and then, whatever the transcription, which is one more reason the live mode is a cousin.
  A Unity-side float expression transcribed for the farm is read with this: the functions
  carry, the expression's rounding points do not, and only a cast pins one. **Live play in the Editor is a cousin by
  construction, and says so** (`a80b1c9`, `dab9b78`): the Runner's live mode steps a farm
  world on `Evosim.Dynamics` inside the Editor from a founding or from a checkpoint
  (`CheckpointPath`/`CheckpointSeconds`, `EVOSIM_THEATRE_CHECKPOINT`; `EVOSIM_THEATRE_SEEK`
  is the checkpoint second when one is named and the seek target otherwise), labelled
  `continued from checkpoint at <s> s (cousin)` with any differing hash named, refusing
  nothing. `LiveCheckpointCheck` showed the counts agreeing for 200 s after a restore while
  `audit` and `meanHeight` parted at the first stepped sample. From `91aea20` the interface
  is up in live mode and answers every panel from the live world through one path for both
  engines: the provenance word is always `COUSIN`, the coverage segment is a drift line
  (`drift: parted at t=410, 0 of 20 agreed`, Mono against the farm's .NET, a reading and
  never a verdict), the popover's `physics jobs` and `sim hash` rows say `not in live mode`
  and the three digests that decide the trajectory stand in their place, the ancestry and
  the dead panel are withheld as on any cousin, and a click selects by a ray against each
  part's own box because a live body has no collider. **A checkpoint version bump orphans
  every checkpoint on disk**: the double-accounts build took the layout from 1 to 2 while
  `scratch/checkpoint/runs/ckA` was the fixture, so the fixture was refused and
  `LiveUiCheck` needed `EVOSIM_THEATRE_CHECKPOINT` pointed at one the build wrote
  (`scratch/live-ui/runs/ckUi` on 2026-09-22); the layout is 3 since the refusal split, and
  ckA, ckB and ckC were re-recorded on the reach-bound build the same night (the newest run
  under each arm; the restore and the checkpoint-free run both identical, the checkpoint
  spec's acceptance). Re-record them after every `StateVersion` or `Checkpoint.Version`
  bump, before the next regress. The founding live path (no checkpoint named) has no check
  yet.
- **`World.Observe` reads a body's centre of mass and throws when it is outside the box,
  and until `2771bf0` the farm's divergence check read the root alone.** Round 44 seed 1
  ended `status error` at 22,370 s on creature 7417: the root inside the bound and the
  centre 3 cm below a 45 m bed, so `CheckFinite` passed it and `Observe` threw
  (`ArgumentOutOfRangeException … height of 45.03 m in a world 45 m deep`). From that
  commit the check reads the centre against `World.HeightIsInTheWorld` too, and such a
  body dies as a counted `Diverged` death with a reason naming the centre. A run that
  ended this way is censored and read at its last sample; its rows stand. The same seed
  is the first the farm has slowed: physics cost per body-step rose 7.6-fold with
  `ovl/body` (0.026 to 0.82, 99% held) and not with links per body, so read `ovl/body`
  beside `x real time` on any seed that falls under 1x, and profile it from a checkpoint
  (`EVOSIM_CHECKPOINT_EVERY`, from round 45's launcher). **The slowdown was the contact
  grid's cell, and it is fixed** (2026-09-22 night, the bench's `--mode record`): the grid
  entered each body's bounding sphere in one cell and sized the cell at two of the
  *largest* radius, and seed 1's module chains, one a fan of seven leaves each 1.75 times
  the last with the seventh 14.6 m long (`logbook/specs/giant-7597.txt`, drawn by
  `scripts/plot-body.py`), took the largest radius to 21 m, the cell to 43 m in a 53 m
  tank and every body's candidate list to the whole crowd: 9.9 µs a body-step against 0.33
  on the same crowd at the genome minimum. The grid now enters a sphere in every cell it
  covers at a cell of two mean radii, exact at any cell (two touching spheres share a
  cell), sorted and deduplicated so the sum is the same sum in the same order: 1.9 µs on
  the ceiling crowd, every digest unmoved, and a 3,000 s regress of `r45fix-s4` identical
  in 141 fields at 300 samples with the lineage byte-equal. **A part's size is unbounded
  above** (`MaxPartVolume` is a million cubic metres and thin sheets never reach it), so a
  self-copying leaf whose edge scale mutates above 1 grows geometrically once the module
  gene lets it copy past its recursive limit; the owner ruled no bound (D107, 2026-09-22
  night): the price is to bound it, the support cost of the next base round's proposal. **The checkpoint writer refused a body that was a
  copy of its adult at scale exactly 1** (round 45's first launch, three seeds within
  7,000 s): a volume fraction within a float's rounding of 1 has a cube root of exactly
  1f, and `Grow`, `AtTheSameFraction` and the newborn's scaling all built a copy that read
  as "neither the adult nor scaled". `Phenotype.Scaled(1f)` now returns the body itself,
  which changes no number (the regress above covers it). The three runs are
  `runs/r45void-s1..3`; seed 2's manifest reads `running` because the exception fired
  inside the stop's own checkpoint. **A snapshot row carries the body's plan from this
  build** (`moduleCounts`, `lostPaths`, read by `GenomeJson.ReadModuleCounts` and
  `ReadLostPartPaths`): the theatre's `-From snapshot` and the bench's record mode draw the
  body the run stepped, and every earlier recording is drawn at the genome minimum, which
  is why no picture of round 44 showed the fan. **The reach bound is built and off**
  (`DevelopmentLimits.MaxBodyReachMetres`, `EVOSIM_MAX_REACH`, header `reach 3 m` or
  `reach off`, default 0 = the recorded world; its value is the owner's ruling on the
  proposal): development prunes a part whose farthest corner lies farther than the bound
  from the root's origin, with its subtree, and counts it in `Phenotype.PrunedForReach`
  (the root is never past it), and the module rule refuses an addition that would be
  pruned as a shape refusal (`ref shape`) before it is paid for. It is a tunable, so it
  refuses every `config.json` written before it, rounds 41 through 45's void launch and
  both fixtures included; the Dynamics crowd is `runs/r45fixb-s4` and the thread-identity
  config is `pfix2`'s, both round 44's world with the bound off, and `r45fixb-s4` replays
  `r45fix-s4` sample for sample in 141 fields (the tree's regress, `scratch/r45-build/
  regress.py`). The Unity farm does not bind the variable; a world built there carries the
  bound at 0 and its header has no `reach` token.
- **A farm round's pre-registration record is the manifest's `gitCommit` on a clean tree.**
  `run-farm.ps1` has no `-Prereg`; `launch-queue.ps1 -Prereg` is the Unity queue's. So a
  farm round is launched only after the entry is committed and `git status` is clean, and
  the manifest's `gitCommit` is then the pre-registration's commit (round 46: `7bf9064`); a
  `(DIRTY)` beside the commit in the launch printout means the record is not the tree.
- **`farm-queue.ps1` launches each seed only when no `Evosim.Farm` process is running**, any farm
  run at all and not only its own round's. So a farm run started beside a queued round holds the
  round's next seed back until it ends, and the queue says nothing while it waits. On 2026-09-30
  round 52's extension (`r52-s1y`, four threads on the nursery's freed lane) held round 53's seed 3
  from 05:49 to 06:42, until the watch saw no seed running and stopped the extension. Start a run
  beside a queue only once the queue's last seed is running, or stop it at the seed's end.
- **The farm hashes the source above its working directory, not the source it was built from.**
  `Manifest` finds the repository by walking up from the process's working directory (or takes
  `EVOSIM_REPO_ROOT`) and hashes `src/` there. A worktree and the main tree differ in carriage
  returns, as `simHash` does, so round 49's film windows, run from the main tree on the round's
  own exe, named all three hashes as differing from the run's, and V1 asks for none (2026-09-26;
  the windows read FAITHFUL on every row all the same). Run a window, a resume or a check from
  the checkout the run was launched from. And **stopping a background Bash task leaves its script
  running**: the task's handle went and both of its `bash.exe` went on to launch the next farm
  process. List them by command line and stop them by id.
