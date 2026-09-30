# The Unity project and the Unity farm: what will bite you

Split out of the root [`CLAUDE.md`](../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read before running Unity in batch mode, refreshing a worker, running a Unity arm, or reading a run the Unity farm recorded (round 42 and earlier). The Unity farm has been idle since round 42 and serves the theatre, which has its own file under `Assets/Theatre/`.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Arms, workers and the Unity process cap

This paragraph closed the root file's opening section until 2026-09-30.

Throughput still binds: dt 0.02
screens, 0.01 confirms (logbook/0052). Experiments are *arms*, launched with
`scripts/run-arm.ps1` against worker copies `unity-wN` (only `unity-w5` and `unity-w6` exist since
the cleanup of 2026-09-29; `scripts/new-worker.ps1` makes another) — never two processes on
one worker, **two concurrent arms from round 47** (owner's ruling, 2026-09-23 evening, on
the machine's heat: round 46's three farm arms at five threads each held the CPU near 80%
for four hours; before it, three arms, the ruling of 2026-09-17: five arms on this
machine deliver about 1.7x real time in total and three about 1.5x, so the fourth and
fifth buy little and put every seed a day later and past its wall; `scripts/pace-survey.py`
is the reading), five Unity processes at most with the owner's open Editor on `unity/`
counted, and verify every arm's settings from the header its run report writes, not from
the launch command. Renders go between rounds or on a slot the arms are not using, never
on top of a full set. A **sixth** Unity process is allowed for a short visual check that
comes and goes (a compile, a render of a few frames; owner's ruling, 2026-09-16), never for
an arm, and never beside a Core test suite: pictures or tests on top of the cap, not both.

## Commands

**Run the physics spike** (compiles, runs M1–M6, writes `results/`):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$proj  = 'd:\Projects\experiments\evolution-simulator\spikes\01-articulation-body'
$a = @('-projectPath',$proj,'-batchmode','-quit','-nographics',
       '-executeMethod','Spike.EditorTools.SpikeEntry.Run','-logFile',"$env:TEMP\spike.log")
Start-Process -FilePath $unity -ArgumentList $a -Wait -NoNewWindow
```

**Run the Milestone 1 smoke test** (builds creatures, checks geometry, momentum and swimming):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$proj  = 'd:\Projects\experiments\evolution-simulator\unity'
$log   = "$env:TEMP\evosim-smoke.log"
$a = @('-projectPath',$proj,'-batchmode','-quit','-nographics',
       '-executeMethod','Evosim.Sim.EditorTools.Milestone1Smoke.Run','-logFile',$log)
Start-Process -FilePath $unity -ArgumentList $a -Wait -NoNewWindow
Get-Content $log | Select-String 'Milestone 1 smoke' -Context 0,140
```

Note `Evosim.Sim.**EditorTools**`, not `Evosim.Sim.Editor` — the folder is `Editor/` (which
is what makes it an editor-only assembly) but the namespace is not. Getting it wrong costs a
full asset reimport before Unity reports
`executeMethod class 'Milestone1Smoke' could not be found` and exits 1. That failure looks
exactly like a hang: `-batchmode` prints nothing to the console, so a cold run is several
minutes of silence whether it is working or not. Tail the log in a second terminal
(`Get-Content $log -Wait -Tail 5`) rather than guessing, and check for
`could not be found` as well as `error CS` — a missing method is not a compile error and
will not show up in the usual grep.

**Unity Hub CLI is broken** in Hub 3.14 — the Electron wrapper consumes the `--` separator
and treats `--headless` as a module path. Fails identically in PowerShell, cmd and bash.
Don't waste time on it; use the Hub GUI.

## Gotchas

- **PhysX sleeps undriven bodies.** The first spike run reported 64 creatures costing less
  than one driven chain, because with zero gravity and no actuation everything settled and
  slept. Any physics benchmark here must actuate the creatures and assert they are awake —
  `SpikeHarness.M3` reports mean body speed for exactly this reason.
- **Unity's physics defaults are not neutral, and they are not specified anywhere.**
  `ArticulationBody.angularDamping` and `jointFriction` both default to `0.05`, and
  `Physics.defaultMaxDepenetrationVelocity` to `10`. The first two are a second drag model
  acting on top of DESIGN.md §5.2's — they removed roughly ten times more energy than the fluid
  did, invisibly, from Milestone 1 until an energy audit found them (logbook/0008). Anything the
  engine does that the design did not ask for is a fault, including doing nothing visibly wrong.
- **`-createProject` gives you the Built-In Render Pipeline**, which DESIGN.md §10 does not
  want and Unity 6.5 deprecates. URP has to be added deliberately; `Evosim/Set Up URP`
  generates the pipeline assets.
- **A wedged sim loop never reaches its own wall-clock check.** The budget and wall checks
  live inside the metabolic loop, so a hang freezes them too: d056b-s1 sat at t=22,700 with
  the process alive and the log silent for 5.7 hours (logbook/0043's instrument note — the
  campaign's first hang, cause unknown). Every monitor watching an arm therefore needs
  three alternations, not two: error signatures, the `**Ended:**` footer, **and a
  staleness check on the report's byte size, not its mtime** — the wedge keeps touching
  mtime with zero content, which fooled the original mtime rule. Threshold ≥ 30 min: under
  full machine load a healthy heavy arm can legitimately take > 16 min per table row, which
  produced a false stall alert that nearly killed a live run. So an alert is a *suspicion*,
  not a verdict — confirm with the discriminator before killing: sample the report's byte
  size and the process's cumulative CPU 90 s apart; wedged = zero byte growth **and** high
  CPU delta (the loop spins without simulating); slow-but-alive = a row appears or CPU is
  quiet. After killing a wedged worker, refresh it — the Library dies with the process — and
  delete its `Temp/UnityLockfile`, which the kill leaves behind and which reads as a live
  Editor to anything that checks it (`launch-queue.ps1` sat for an hour behind three
  killed renders on 2026-09-13; it now removes a lock no process holds).
- **PhysX replays bit for bit on this machine, so every per-step change is a butterfly.** Same
  genome, seed, config *and build* give the same run report to the last decimal (`r16dt-01c` ≡
  `-01d` ≡ `-01e`, logbook/0052). Change any per-step term — 68 capped drag impulses at dt 0.01,
  432 at 0.02 — and the same seed becomes a different chaotic realisation: ±20% population, 4 m of
  depth, half the larder by t=5,000. A per-seed A/B on anything that touches the physics loop
  therefore cannot separate the change from the realisation; compare distributions across seeds,
  or hold the difference against that wingspan. `EVOSIM_DT` 0.02 is the screening step (deviations
  inside the wingspan, ~3× the pace); 0.01 confirms and is the only step at which the historical
  record replays; 0.05 is out (population migrates into the surface film and the audit opens). The
  drag limiter engages only above 0.01 for exactly that reason. **0.02 is bimodal on depth**: three
  of six fast-step worlds on 2026-09-04 sat in the surface film at −1 m where their 0.01 seeds sat
  at −12 to −15 m, and the two arms of one seed pair sat 10 m apart (logbook/0056). 0052's
  wingspan check did not see this. A 0.02 screen answers a mechanism question read within one
  step; it does not stand in for the 0.01 world, and a 0.02 result about depth, light or the film
  is not a result. One 0.02 arm also diverged — a newborn's
  143-gram link spun up by thousands of rad/s in one step (`r20q-s1`, logbook/0059). Since then a
  non-finite body is dumped to `runs/<arm>/<run>/diverged/` and killed as a counted `Diverged`
  death (read the `diverged` column; a run with any is read with that caveat), and at steps above
  0.01 a drive impulse limiter caps each joint at 30 rad/s per step and counts its binds as
  `driveImpulsesLimited` — about 10⁵ per 0.02 run, so **the fast step under-drives evolved
  muscle; anything about swimming or joints is read at 0.01 only.** A run whose manifest reads
  `status error` is censored. **A finite body can diverge too**: `r31-s3` ended at 11,533 s
  with a root thrown to a finite height of the order of 10³¹ m, which passed both non-finite
  tests and overflowed the light field's layer index (`ArgumentOutOfRangeException` from
  `LightField.Contribute`, logbook/0077). Since 2026-09-08 the harness's check and Core's
  `Observe` guard read the height against the world's box with room
  (`World.HeightIsInTheWorld`: the depth above the surface, twice the depth below the floor),
  and such a body dies as the same counted `Diverged` death. **A non-finite link is a diverged body too, and until 2026-09-10 only the root
  was read**: `CheckFinite` ran at the metabolic cadence on the root alone, the chemical sense
  read every part's transform on every physics step, and `r35old-s3` died with the whole
  process at 618 s when a link went non-finite between two checks and the grid refused the
  point (`status error`, no `diverged/` dump; round 33's three divergences were one-part
  bodies, so the root check was lucky). From that build the check reads every link and runs
  again before the sensors on any step where a birth or a resize moved a body, and the grid's
  refusal names a non-finite position rather than `FieldPoint.At`.
- **The shared world does not replay unless the physics step runs on one thread.** Same
  genome, seed, config and build on the same worker gave six realisations of one world, every
  pair identical for ~148,000 steps and then parting in one body's velocity by one or two ulp
  where touching bodies were solved in a different order; Unity's *Enhanced Determinism*,
  sleep, broadphase and scratch-buffer settings change nothing, and `-job-worker-count 0`
  restores identity over 300,000 steps (logbook/0069), then over 1,000,000 steps at a full
  round's population (`r28p-s1` ≡ `r28-s1`, logbook/0070). PhysX documents thread-count
  independence; this build with articulations in contact does not have it. The tiled world's
  replays (`r16dt-01c/d/e`, `fp-replay*`, `fl-replay`) never reached the fork because no two
  creatures ever shared a solver island, so **a replay-identity validation in the tiled world
  says nothing about the shared one** — validate shared-world changes with the state digest
  (`EVOSIM_DIGEST_EVERY`, `scripts/digest-diff.py`) on a zero-worker pair. Every shared-world
  run before the fix (0065–0068) is one realisation of its seed; the theatre's World mode
  re-simulating one of them watches a cousin from t≈1,500.
- **Every worker compiles `src/Evosim.Core` from the main tree.** `unity-wN/Packages/manifest.json`
  points at `file:../../src/Evosim.Core`, so `new-worker.ps1` copies only `Assets/`,
  `ProjectSettings/` and `Packages/`; a Core edit in the main tree reaches every worker launched
  after it, whatever its `Assets/` carry, and the manifest's `coreHash` records which. A round
  cannot be held on one build once Core has moved (round 24's fifth seed runs on the perception
  build for this reason, 0061); land Core changes between rounds or accept and record the split.
- **`coreHash` is the main tree's Core, whatever the worker's manifest points at.**
  `EvolutionRun` hashes `<repo>/src/Evosim.Core` by path, not the package the manifest
  resolves, so a worker whose `Packages/manifest.json` is pointed at a worktree's Core for a
  validation (the round 38 build's chain, 2026-09-15) compiles the branch and records main's
  `coreHash`. On a normal worker the two are one tree. Read a validation run's `coreHash` as
  the main tree's, and take the branch's from a manifest written after the merge.
- **`scripts/simhash.py` is not the hash.** It agreed with `EvolutionRun.HashSourceTree` on every
  tree through 1ce2e71 and disagreed on e59f6af's (`93ef4e96…` against the C#'s and
  `run-arm.ps1`'s `30b96bf6…`), which cost one refused launch. Take the expected hash from a
  manifest the build has written (`runs/<arm>/<run>/run.json`, `source.simHash`) or from
  `run-arm.ps1`'s own printout; the python is a convenience until its divergence is found.
- **`simHash` is a property of a checkout, not of a commit.** It hashes the bytes of every `.cs`
  under `Assets/Evosim` on disk, and this working tree is mixed: `EffectorDriver.cs`,
  `EmbodiedRun.cs` and `ThroughputSurvey.cs` are CRLF while everything beside them is LF, which
  `core.autocrlf=true` hides from `git diff` completely. So two checkouts of the same commit can
  carry different `simHash`es, and a hash cannot be reconstructed from history with
  `git archive` (it applies the checkout conversion; `git show` does not). Compare a recorded
  `simHash` against a tree on disk, never against a commit. **A worktree is another checkout**:
  round 41c's smoke on a worker mirrored from `scratch/wt-contacts` recorded `73902d7b…`, main's
  checkout of the merged commit hashes `99e0bdcc…`, and the two trees differ by carriage returns
  alone (2026-09-19, one refused launch). Take a round's expected hash from a smoke or a
  `run-arm.ps1` printout on a worker refreshed from the tree the queue will refresh from.
- **Anything under `Assets/Evosim` is simulation source, whatever it does.** The theatre spent one
  commit inside it and made every earlier recording unreplayable, because a HUD label is a `.cs`
  file under that root and `simHash` cannot tell a viewer from a solver. Presentation, tooling and
  anything else that does not decide a trajectory goes beside it (`Assets/Theatre/`), not in it.
- **A pre-registration is committed before the queue starts.** Round 37's predictions were
  committed at 08:31:49 and its first manifest written at 08:28:48 (the Astra review of
  2026-09-12): the thresholds were in the working tree and not in history when the world
  started. `launch-queue.ps1 -Prereg logbook/NNNN-….md` refuses to launch unless the entry
  is tracked and clean, and writes `runs/<arm>/prereg.json` with the commit beside each run.
- **Set an arm's wall limit from the measured pace, not from the hoped-for one.** Round 39's
  launcher gave 1,200 min for 30,000 s on an estimate of fourteen hours a seed; at five arms
  and 2,000 bodies the seeds ran at 0.3x real time, twenty-eight hours, and seed 3 was
  censored at 25,175 s with "ended wall" (2026-09-16). A running arm's wall cannot be
  extended. Read `x real time` off a seed's footer or the early rows before launching the
  rest, and give the wall half again what that says.
- **A stopped arm leaves `Temp/UnityLockfile` on its worker, and `new-worker.ps1` refuses
  the refresh.** `stop-arm.ps1` kills the process, the lock stays, and the refresh reads it
  as an open Editor; on 2026-09-20 four workers were "refreshed" this way, none moved, and
  the smoke that followed recorded the previous build's `simHash` with a header missing the
  new tokens. With no Unity process running, delete the lock and refresh again, and read
  the header tokens of the smoke before taking its hash. A worktree's worker is a copy too:
  an edit in the worktree reaches `<worktree>/unity-wN` only by a refresh from the worktree.
- **`windows-il2cpp` is not installed** — only Mono. Fine for now; add it before the island
  model (Milestone 4), since per-creature brain evaluation is managed C# in the hot loop.
