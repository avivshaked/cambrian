# Build brief: the nursery room, phase 2 (the writes)

You build phase 2 of the nursery room in the worktree
`D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-room` (branch `nursery-room`; commit
nothing). Never `cd` or `Set-Location` into the worktree: use absolute paths, `git -C`, and
Start-Process's `-WorkingDirectory`. Write only inside that worktree and under
`D:\Projects\experiments\evolution-simulator\scratch\nursery-general\room2\`. Never write to your session
scratchpad or the Windows TEMP directory. Never touch `scratch/wt-joint` or anything under it, nor
the main checkout's files.

## Machine rules, binding

- Another session's run holds the machine. One build or test at a time, always `-m:1`, with Unity's
  SDK: `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe`. Nothing else is
  installed, and you install nothing system-wide.
- **Nothing swims.** Never run the nursery (`Evosim.Nursery`) with `--recipe` or `--rerun`, not even
  for one generation, and never let a test do so. Launch and stop are tested with a fake process (see
  below). The real acceptance run is done later by the calling session.
- A room server is running from this worktree's build (pid 103368) and locks its dll. Stop it by
  that pid before your first build (`Stop-Process -Id 103368`); do not restart it. Any server you
  start yourself, you stop by its pid before you finish, with port 5179 not listening afterwards.
- Never stop any other process. Never wait or poll: your task ends when the edits are in, the
  tests pass, and one smoke run has started and stopped its server.

## Read first

- `logbook/specs/nursery-room-build.md` (the shape, phase 2's row, "What was built").
- `design/nursery/BRIEF.md`: above all, the MCP's shape, and building nothing about today's
  experiment in.
- The room's code as it stands (`src/Evosim.NurseryRoom/`, its tests) and the nursery's command line
  (`src/Evosim.Nursery/Program.cs`, around the usage text: `--recipe <file> --out <dir> [--threads n]`,
  `--rerun <entry> --out <dir> [--threads n]`), `RunRecord.cs` and `RegistryEntry` (the registry).
- `CLAUDE.md`'s rule that a run is stopped by a `STOP` file, never by killing it.

## What to build

1. **A clean stop in the nursery** (`src/Evosim.Nursery/`, the only nursery change): between
   generations, and between exam genomes, the nursery looks for a file named `STOP` in its `--out`
   folder. If present it finishes nothing further, writes `run.json` with a stopped ending (add the
   state to `RunRecord` the way its other endings are written, refusing nothing older), logs one
   line, and exits 0. A stopped run does not enter the registry. Unit-test the check and the record
   without swimming. It must change no number or draw of a run that is not stopped: the check reads
   a file and nothing else.
2. **`recipe.write`** (mutating, needs `reason`): writes a recipe file under `rounds/nursery/` of the
   room's root. Arguments: `path`, `text` (the JSON), and `expectHash` (the current file's SHA-256 if
   it exists, which must match, so that two writers cannot overwrite each other blindly; refused if
   the file exists and `expectHash` is absent). It checks the text with the nursery's `Recipe` first
   and refuses a recipe the nursery refuses, writing nothing. Paths go through the existing
   repository guard.
3. **`recipe.diff`** (no side effect): two recipe paths, or one path and its base; returns the
   resolved difference field by field. This replaces the screen's own diff (the review's #17): the
   recipe screen marks its rows from this operation's result only.
4. **`run.launch`** (mutating): `recipe` (path), `name` (a folder name, `[a-z0-9][a-z0-9-]{0,63}`),
   `threads`. Runs the nursery as a detached process (`dotnet <nursery dll> --recipe <path> --out
   <runsDir>/<name> --threads n`), with its working directory the room's root. Refuses an existing
   folder. The dll, the dotnet path and `runsDir` are new `room.json` keys (every key required, as
   now). The launched run is added to the room's catalogue at once, so the registry screen shows it
   while it runs (the review's #22).
5. **`run.rerun`** (mutating): `entry` (registry number or 8+ hex digits of a recipe hash), `name`,
   `threads`. As launch, with `--rerun`.
6. **`run.stop`** (mutating): `id`. Writes `STOP` into the run's folder. Never kills a process. If the
   run's build predates the stop file (its `run.json` names a commit before item 1, or it has no
   `run.json`), the result says the file was written and that this build may not read it.
7. **`compare`** (no side effect): two run ids. For each file name in either folder, its SHA-256 in
   each, and for text files that differ, the first differing line number and both lines (cut to 200
   characters). The verdict is `identical` only if every file is.
8. **The cap.** `room.json` gains `capLogical` (16) and the machine's logical count is read. A launch
   or rerun is refused when the threads asked, plus the logical processors in use (`% Processor
   Utility` of `\Processor Information(_Total)`, as a share of the logical count, rounded up), would
   pass the cap. The refusal says the numbers. If the counter cannot be read, refuse and say so. The
   reading sits behind an interface so tests give it a value. The band's machine strip shows the
   load read, instead of "load not read in phase 1".
9. **The activity log.** Every operation that mutates appends one JSONL row: time (UTC), the
   operation id, the arguments (without `text` bodies; their hash instead), `reason`, who (`screen`
   or `mcp`, from the door it came through), and the outcome (ok, refused and why, or failed and
   why). The path is a new `room.json` key, `activityFile`, relative to the main checkout (found as
   the registry is); its default in the committed `room.json` is `rounds/nursery/activity.jsonl`. The
   file is opened for append with `FileShare.ReadWrite`, one line per row, never rewritten.
   `nursery://activity` reads it, newest first, and the registry screen's activity panel shows it.
   A row holds no secret and no path outside the repository: every path in it is repository-
   relative. `reason` is free text from an agent, so an operation refuses a reason that holds a line
   break or a control character, or is longer than 500 characters, and the writer refuses any row
   holding a line break, as `JsonlWriter` does.
9b. **One list of the append-only files.** The registry and the activity log are both appended to by
   every run or operation, so neither makes a tree dirty. Make one file, `rounds/nursery/appended.txt`:
   one repository-relative path per line, `#` comments allowed, listing
   `rounds/nursery/registry.jsonl` and `rounds/nursery/activity.jsonl`. The nursery's dirty check
   (`RunRecord.cs`, the `:(exclude)` in its `git status`) builds its excludes from that file, read
   from the tree it runs in, instead of from `RegistryEntry.RelativePath` alone; a missing or
   unreadable file is an error that names it, never a silent empty list. The room reads the same
   file to find the activity log's default. A test pins that the file lists
   `RegistryEntry.RelativePath` and the committed `room.json`'s `activityFile`. (`scripts/farm-queue.ps1`
   will read the same file; the calling session does that at the merge, so do not edit it.)
10. **The screens.** Enable the phase-2 controls, each calling its operation by id through the
    existing `callOp`, each asking for a reason in a small inline field before it sends (no browser
    `prompt()`), and showing the refusal text when refused. Launch as trial stays disabled with the
    title "awaiting the owner's ruling". Remove the ids built here from the parity test's phase-2
    allow-list.
11. **The MCP.** The new actions appear under their families (`recipe`: write, check, diff; `run`:
    launch, stop, rerun; `compare`), generated from the list as now. `describe` returns their schemas.

Nothing about today's experiment is built in: no sensor, output, control, reward or body name in
code or markup.

## Tests

Use a fake launcher in tests: the launch path sits behind an interface, and the tests check the
command line, the working directory and the folder. Test every refusal (cap, existing folder, bad
name, hash mismatch, nursery-refused recipe, missing reason) and that each writes its activity row.
Test `compare` on synthetic folders. Run the room's tests and the nursery's tests
(`src/Evosim.Nursery.Tests`), one at a time.

## Smoke

Extend `scratch\nursery-general\room1\smoke.ps1` into `scratch\nursery-general\room2\smoke.ps1`: start
the server, call `describe`, `recipe.write` a copy of `rounds/nursery/brain2.json` to
`rounds/nursery/smoke-copy.json` with a reason, `recipe.diff` it against `brain2.json`, `compare`
`pilot-deep-brain2` with itself, read `nursery://activity`, and refuse a `run.launch` on purpose by
asking for more threads than the cap. Do not launch for real. Stop the server by pid. Delete
`rounds/nursery/smoke-copy.json` and the activity rows' file afterwards only if the smoke created
them inside the worktree.

## Report

Tables in your final message: files changed (with line counts), each item above with what was done
or why not, the test counts, the smoke table, and anything the spec did not settle and how you
settled it. No report file.
