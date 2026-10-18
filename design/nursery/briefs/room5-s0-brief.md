# Build brief: the tank, step S0: the room rebuilds an exam's plan

You build in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-room` (branch
`nursery-room` at `1738587`; commit nothing). Never `cd` or `Set-Location` anywhere, not even for a read:
use absolute paths, `git -C`, and Start-Process's `-WorkingDirectory`. Write only inside that worktree.
Never write to your session scratchpad, to the Windows TEMP directory, or under
`D:\Projects\experiments\evolution-simulator\scratch\nursery-general\`. Helper files go under
`scratch\wt-nursery-room\scratch\room5\` and are deleted before you report.

**A denied or blocked command is final.** Do not reach the same end by another tool or another
command, even for a file of your own. Report it.

You may read, by absolute path with your Read and Grep tools (never through a shell command that
starts with `cd`):
- everything in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-room\`,
  its source above all
- the plan `D:\Projects\experiments\evolution-simulator\logbook\specs\nursery-room-tank-plan.md`
- the room's spec `D:\Projects\experiments\evolution-simulator\logbook\specs\nursery-room-build.md`
- the finished run `D:\Projects\experiments\evolution-simulator\scratch\nursery\pilot-deep-brain2\`
- the main checkout's `rounds\nursery\registry.jsonl`, whose entry 1 is the pilot

Write nothing outside the worktree. **Never read anything under `scratch/wt-nursery-general`,
`scratch/wt-joint`, `scratch/wt-nursery-s6` or `scratch/wt-advect-fast`.**

**Do not edit anything under `src/Evosim.Nursery/` or `src/Evosim.Core/`.** This step is the room's
side only. The nursery's edits wait for the coordinator.

## Machine rules, binding

- Light load: one build or one filtered test run at a time, `-m:1`, with Unity's SDK
  `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe`, into
  `...\scratch\wt-nursery-room\artifacts\verify-out\room\`. Run the room suite once at the end.
- **Nothing swims.** Never run the nursery with `--recipe`, `--rerun` or `--bench`. Never touch the
  server on port 5179. Start no server.
- Never stop a process you did not start. Never wait or poll.
- Edit strings holding `\n` or `\r\n` with your file-editing tool.

## What to build

Read the plan's sections "What one exam episode is" and "What the pilot's exam is made from", and
step S0 of its build order. Build an `ExamPlan` in the room that, for a run, gives:
- its brains in index order, with their labels and the control each applies
- its genomes in pool order, with their ids and bodies
- the episode seeds, as strings
- for each draw, its tanks as lists of pool indices, in slot order
- where each input came from

Its sources, in order of preference:
1. **The run's own record.** `run.json`'s exam plan, with `tank-exam-draws.jsonl` when present
   (3b's files).
2. **For a run made before those existed,** a reconstruction:
   - the recipe from the run's registry entry, with its hash checked against the entry
   - the pool from `tank-exam-genomes.tsv`
   - the seed and salt from the recipe, as the plan explains for the pilot
   - the seeds by `Rng.SeedFor(seed ^ salt, gen*1000 + e)`
   - the tanks by `TankLog.PlanTanks`

The room may call the nursery's public `TankLog.PlanTanks` and Core's `Rng.SeedFor` by project
reference. `SeedsFor` is private: duplicate its one line in the room, with a comment naming
`Program.SeedsFor` and a test that pins the duplicate to known values.

Match brains by index, never by label. A label that differs from the recipe's, as the pilot's
index 3 does, is reported in the plan as a known difference, quoting the registry entry's
`knownDifferences`.

Serve it read-only as `nursery://exam-plan/{run}` through the API and the MCP, like the other
resources.

## Tests

- The pilot's plan: 32 seeds, one tank of 6 per draw, 18 brains, the label difference at index 3.
- The first three seeds pinned to values you compute once and check by hand against the formula.
- A synthetic 3b run with `tank-exam-draws.jsonl`: the reconstruction equals the recorded file for
  every draw.
- A recipe whose hash does not match its entry is refused, saying so.
- A run with no registry entry and no `run.json` plan says the plan cannot be rebuilt, and why.
- Parity still passes.

## Report

Tables in your final message:
- the files changed, with line counts
- the pilot's first three seeds and draw 0's tank
- the room suite's count
- `git -C <worktree> status --short`

No report file.
