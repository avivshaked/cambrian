# Fix brief: the nursery room as operated, round A2

Read `D:\Projects\experiments\evolution-simulator\scratch\nursery-general\room6\fix-brief.md` first. Its
rules, its machine rules, what you may read, and its section "Operate every fix before reporting" all
hold here. These changes apply:
- The worktree is at `f430ecc`, with nothing uncommitted.
- Your folder is `D:\Projects\experiments\evolution-simulator\scratch\room6-fix-a2\`. Copy the driver
  into it from `D:\Projects\experiments\evolution-simulator\scratch\room6-reinspect\` (`cdp-lib.ps1`,
  `q.ps1` and `steps\`), which is the re-inspection's own driver and walk. Its screenshots are in
  `shots\` there.
- **A denied or blocked command is final, even for deleting a file of your own.** Leave the file
  where it is and say so. Never use `rm`, `del` or another tool to finish what a blocked command
  started.
- Never start a shell command with `cd`.

## Fix these, checking each first in the re-inspection's evidence and in the code

1. **The tank screen, for a run with no exam plan or an unknown run.** It shows the shared refusal
   in the main area: crumb, message, a link back. The map's placeholder never says "Reading the
   episode" when nothing will be read. A run with no plan is an expected state, not an error: read
   it without a red console error. Either the room answers 200 with a `refused` field, or the page
   tells an expected 4xx from a fault and does not log it as an error. Pick one for every screen and
   say which.
2. **The genome screen, for an unknown run or genome.** It shows the shared refusal once, in the main
   area, with a link back. The export control is not live when there is nothing to export.
3. **Regression: a registry entry by its folder name.** `run.html?id=accept-1gen` (entry 2's folder) is
   refused since round A merged one row per folder. Every entry answers to its number and to its
   folder's name, as `pilot-deep-brain2` does. Add a test for an entry whose folder is not in
   `room.json`.
4. **The genome screen at 1366×768.** The body is visible on the first screen beside the brain, not
   pushed below it. The part table has no inner scrollbar, or a styled one if it must scroll.
5. **STOP on a run that is not running** is truly disabled, with its reason, on the run page as in
   the registry.
6. **A run with no exam file** gets a short panel saying so, not a tall empty box.
7. **Nits:**
   - The registry's "1 / 100" cell overflows its column by about 20 px.
   - Exam headers cut to "KNOCKO." lose the channel. Prefer "KO Chem" or similar, with the full
     label in the title.

## Report

Tables in your final message:
- each item, with what changed (file and function) and what the operated walk showed, with a
  screenshot path
- every console error and failed request left after a walk of all five screens at both sizes
- the room suite's count
- `git -C <worktree> status --short`

No report file.
