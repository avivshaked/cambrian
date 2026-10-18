# Build brief: the nursery room, phase 3a and 3b

You build in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-room` (branch
`nursery-room`; commit nothing). Never `cd` or `Set-Location` into the worktree: use absolute paths,
`git -C`, and Start-Process's `-WorkingDirectory`. Write only inside that worktree and under
`D:\Projects\experiments\evolution-simulator\scratch\nursery-general\room3\`. Never write to your session
scratchpad or the Windows TEMP directory.

**Never touch, build in, or read-lock anything under `scratch/wt-nursery-general`**: a 13-hour run
swims from it. Never touch `scratch/wt-joint`, `scratch/wt-nursery-s6` or the main checkout's files.

## Machine rules, binding

- A 14-thread nursery run of another session holds the machine until about 05:00. Light load only:
  one build or one filtered test run at a time, always `-m:1`, with Unity's SDK:
  `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe`. Prefer
  `--filter` while iterating; run each full suite once at the end.
- **Nothing swims.** Never run the nursery with `--recipe` or `--rerun`, and no test swims an
  episode. Never start a room server on port 5179 (one is running there; leave it alone). If you
  need a server, use port 5180 and stop it by its own pid.
- Never stop a process you did not start. Never wait or poll.
- Edit C# or Python strings that hold `\n` or `\r\n` with your file-editing tool, never through sed,
  a bash heredoc or `python -c`, which turn the escapes into real line breaks.

## Read first

`logbook/specs/nursery-room-build.md` (phase 3's row and "What was built"),
`design/nursery/HANDOFF.md` (section "What the screens need that the nursery does not write yet"),
`design/nursery/BRIEF.md` (nothing about today's experiment built in), and the nursery's
`Program.cs`, `Tank.cs`, `RunRecord.cs`, `Registry.cs`.

## 3a. Launches pinned to cores

`room.json` gains `affinity`: a hex string of the logical processors a launched run may use, default
`"0xFFFF0000"` (the efficiency cores; the coordinator's rule). Required like every key. The launcher
creates the process suspended (`CREATE_SUSPENDED`), sets its affinity
(`SetProcessAffinityMask`) and only then resumes its thread, so that the `cmd` and the `dotnet` it
starts inherit the mask from their first instruction. Refuse a mask of 0, or one naming a processor
the machine lacks. The cap counts the threads asked against the cap as now; record the mask in the
launch result and its activity row. Test it with the real launcher on the test echo program: the
echo prints its own process affinity, and the test asserts it.

## 3b. What the nursery writes for the tank and genome screens

The rule above all: **every result file the nursery writes today stays byte for byte the same,** and
no random stream draws one number more or less. New data goes only into new files, and into
`run.json`, which is not a result. Read the shuffle and the population as they are; never draw.

1. **Tank-mates.** A new `tank-draws.jsonl`: for each generation and each draw, the draw's episode
   seed and its tanks in order, each tank the list of member ids that swam in it. The exam writes
   the same in `tank-exam-draws.jsonl`, per brain. A member id is `g<generation>.<index>`, the index in
   that generation's population as the search holds it; an exam genome is named by the id it had in
   the search.
2. **Lineage.** A new `tank-lineage.jsonl`: one row per member per generation, with its id, body,
   born, and its parents' ids (none for generation 0; one for a mutated copy; two for a crossover,
   base parent first), and whether it is an elite carried unchanged. Write it from what the loop
   already knows; if a parent's id is not kept today, keep it in memory, never by drawing.
3. **The exam's plan.** `run.json` gains the exam's plan before the exam starts: its brains in
   order, the genomes with their ids and bodies, the episodes and the salt. Progress is read from
   `tank-exam.jsonl`'s rows, as now.
4. **`RegistryEntry.IsResult`**: the three new files are results (they repeat bit for bit). Note in a
   comment that entries made before this change do not hold them, so their reruns on this build
   would differ by those files alone; a rerun runs on its entry's build, so this does not arise.
5. **Tests, without swimming:** the id scheme, the lineage of a crossover child and of an elite,
   the draws file's shape for a synthetic population and seed, and a test that the shuffle in
   `SwimTanks` and the new file agree for a given seed (call the shuffle code with a tiny
   population; do not swim). If the loop cannot be exercised without swimming, factor the pure
   parts out and test those.

The check that every old result file is unchanged needs a run, which waits for the machine; the
calling session does it later by digest against step 5's run. Make that check possible: list in your
report which files are new and confirm by reading the code that no existing writer changed.

## Report

Tables in your final message: files changed with line counts; each item with what was done; the
test counts of both suites; any place where a random stream or an existing file's bytes might be
touched, and why it is not. No report file.
