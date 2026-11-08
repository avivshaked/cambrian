# Brief: why a gripping world parts from its run at a card resume (2026-11-08)

## The fault

On `plants-build`, a world resumed on the card from a checkpoint does not repeat the run it came from. `plfev-6202`
resumed `plf-6202` from its 7,500 s checkpoint on the same build and config, and parted at its first stats row. Two
copies resumed in one group (`plfev-6202`, `plfevb-6202`) matched each other row for row. So the restore is
deterministic and wrong in the same way each time.

What the read-only search of 2026-11-07 found (`forest-run.md`, the paragraph after "What light at each part found"):

- At 7,510 s the resume had formed 28 more grips than its source and broken 28 more, with the same 22 held. Nearly every
  grip held at the checkpoint broke once and formed again.
- The 18 bodies whose positions differ at 7,510 s are all among the 23 that held a grip at 7,500 s, each 1 to 2 cm
  higher, and no other body differs.
- The physics digest differs one second after the resume.
- The plant rules' own state is written to the checkpoint and read back: the anchored founders' counters, the order
  of lost parts, the growable-node cache and the settling spots.
- A break needs a pull of hundreds of newtons, so whatever input differs is off by far more than rounding (inference).

Card-kit worlds repeat on resume because none has a gripping cell. `GripCheckpointTests` restores a gripping world on
the CPU only, and its `TheFidelityCheckPassesMidGrip` passes. `GpuSettledResumeTests` resumes on the accelerator, but
in a crowd that never grips. No test resumes a world with grips held on the card's code path. So the likeliest place is
what the card's runner takes back from a restored world for a held grip. The other candidate is what its grip rule
reads in the first steps after a restore. Both are inference, to be proven or ruled out.

## The task, stage A: find it and write the fix, with no build

1. Read `src/Evosim.Farm/CLAUDE.md` and `src/Evosim.Farm.Gpu/CLAUDE.md` in the worktree before anything else.
2. Find the input the card's grip rule reads that differs after a restore from the run it came from. Name the member,
   the file and line where the continuous run sets it, and the file and line where the restore leaves it unset or sets
   it wrong. Say how the difference yields a pull of hundreds of newtons, and why the CPU path does not show it.
3. Write the fix in the worktree's source. If a generated kernel file changes, change its template and regenerate the
   file the same way the worktree's other generated kernels were, and say how.
4. `HoldsGrip` and `AnchorLowestY` on `Organism` are marked "not state": each is handed fresh before every step that
   reads it. Confirm from the code that a restored world is handed both before its first read. Then add both to the
   filled-before-read handling in `CheckpointFidelity`. Otherwise the check reports a false mismatch on them and stops
   before it reaches the real fault.
5. Write a test beside `GpuSettledResumeTests`. It resumes a gripping world on the accelerator from a checkpoint taken
   while grips are held, and requires it to step as its unbroken run, digest for digest. Model it on
   `ASettledCrowdResumedOnTheAcceleratorIsItsUnbrokenRun` and `ACheckpointTakenMidGripRestoresAndStepsAsTheWorldItWasTakenFrom`.
   It must fail without the fix (say why it would) and pass with it.
6. If the fix changes what a checkpoint holds, bump the format and the refusal as the farm's `CLAUDE.md` says, and
   name every test and fixture that must be re-recorded.

The task ends when the edits are in the worktree's files. Nothing is compiled or run in stage A.

## Stage B, part 1: build and test on the CPU, in a window with no render

Stage A wrote the fix, unbuilt, in the worktree (`forest-run.md` gives the cause). Part 1 builds it and runs the tests
that need no card. Every command starts pinned to logical 20-23 and below normal, from Git Bash:
`cmd //c start "" //b //wait //belownormal //affinity F00000 <command>`. The SDK is Unity's:
`C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe`.

1. Build `src\Evosim.Farm.Tests\Evosim.Farm.Tests.csproj` in the worktree with `-c Release -m:1
   -p:UseSharedCompilation=false`, its output written to a log in the folder below. Fix any compile error in the files
   stage A changed or added, and nowhere else.
2. Run the tests with `test --no-build -c Release`, one filter at a time, each with `&Category!=Card` added. Set no
   `EVOSIM_GPU_*` variable. First `GpuKernelSourceTests`, which confirms the regenerated kernels. Then the new
   `GpuGripResumeTests`, which must pass. Then `GripCheckpointTests`, `GpuSettledResumeTests`, `GpuUsedCopyTests` and
   `GpuRemainsTests`, and the tests of `GpuEngineTests` whose names contain `Grip`.
3. Prove the test can fail. Make a second worktree, detached at the worktree's commit:
   `git -C D:\Projects\experiments\evolution-simulator worktree add --detach scratch\wt-grip-nofix 8676833f`. Copy in
   only the new test file, `GpuColumns.cs` and `CheckpointFidelity.cs`, leaving the kernel, the runner and the
   generated kernels unfixed. Build it the same way and run the new test there. It must fail, and the report gives the
   assertion that failed.
4. Read the heat before each build and each test run with the PowerShell tool's
   `D:\Projects\experiments\evolution-simulator\scripts\temp-check.ps1`. At 90 °C or above, stop and report.
5. No test touches the graphics card: another session uses it. No commit. No `cd`. After the last step, check that
   no `dotnet.exe` you started is still running.

A test run that has not ended in 10 minutes is stopped, and the report says so; nothing is relaunched in the
background. Write only to the two worktrees' `src` folders (part 3's copies included) and to
`D:\Projects\experiments\evolution-simulator\scratch\plants-work\grip-resume\`, never to the session scratchpad or
TEMP. The report, at most 400 words, gives each step's outcome with its test counts, every compile fix by file and
line, and the failing assertion of part 3.

## Stage B, part 2, run by the coordinator

The fidelity mode on `plf-6202`'s checkpoint: `Evosim.Farm.exe --verify-checkpoint <ckpt> 10 <scratch dir> 4`. It
steps on the CPU, so it cannot see this fault, but it must now pass the two `Organism` fields. Then a fresh card run
on the fixed build, checkpointed while grips are held, resumed on the card and matched against its source with
`scripts/reads/resume-match.py`. The card run waits for a card window with no Unity beside it.

## Paths

- Worktree, branch `plants-build` at `8676833f`, clean:
  `D:\Projects\experiments\evolution-simulator\scratch\wt-plants-build\`.
- Grip code: `src/Evosim.Dynamics/Creature.Grip.cs`, `Creature.State.cs`, `Anchor.cs`, `DynamicsWorld.cs`;
  checkpoint: `src/Evosim.Farm/Checkpoint.cs`, `CheckpointFidelity.cs`, `Simulation.State.cs`; card:
  `src/Evosim.Farm.Gpu/gpu-runner.template`, `gpu-kernel.template`, `GpuWorld.cs`, `GpuSlots.cs` and the generated
  `Kernels.*.g.cs`; Core: `src/Evosim.Core/Ecosystem/Organism.cs` (`HoldsGrip`, `AnchorLowestY`) and `World.cs`
  (`NoteGrip`, `NoteAnchorDepth`).
- The source run: `scratch/plants-work/runs/plf-6202/2026-11-06-134402-c7e97670/`, its checkpoint
  `checkpoints/000007500.ckpt`. The resumes: `scratch/plants-work/runs/plfev-6202/2026-11-07-215058-c7e97670/` and
  `plfevb-6202/` beside it. All under the main checkout, `D:\Projects\experiments\evolution-simulator\`.
- The search's helpers: `scratch/plants-work/checkpoint-gap/helpers/posdiff.py` (positions of one sample, source
  against resume) and `statsdiff.py`.

## Rules

- Write only to the worktree's `src/` files the fix needs, and to
  `D:\Projects\experiments\evolution-simulator\scratch\plants-work\grip-resume\` (your helper scripts under its
  `helpers\`, any outputs beside them). Never to the session scratchpad, never to TEMP, never anywhere else.
- Never `cd` into the worktree. Use absolute paths and `git -C`. No commit, no stash, no checkout, no reset.
- No `dotnet`, no build, no test, no farm run, nothing on the graphics card: a film pass is running on it.
- A helper that reads a run's files streams them (a `.gz` line by line), keeps under 1 GB, and starts pinned and
  below normal from Git Bash: `cmd //c start "" //b //wait //belownormal //affinity F00000 python -I <script> <args>`.
  The machine has little free memory while the film pass runs.
- Report in the final message, in at most 700 words. Give the member and its lines, the mechanism, and the edits by
  file. Give the new test and why it fails without the fix. Then list anything not settled and what stage B must
  check, and mark each inference as one.
