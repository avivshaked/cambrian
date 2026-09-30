# The machine and the agent's tools

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read when the machine misbehaves (a freeze, a black screen, the fans), before a pace or timing comparison, and when a Claude Code tool asks or refuses unexpectedly. The load ruling in force is HANDOFF's.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Gotchas

- **A blue screen beside a GPU probe was the file-system filter stack, and a minidump is
  readable without a debugger.** The one crash in this machine's log (2026-09-22, bugcheck
  0x3B) faulted in `FLTMGR.SYS` on a `dotnet.exe` thread, entered through the Xbox Gaming
  Services filter with Avast below it and the display driver on no frame, while a Gaming
  Services update had been stuck for half an hour (`logbook/specs/crash-2026-09-22.md`). The
  GPU spike lost a day to a suspicion the stack did not support. `C:/Windows/Minidump` and
  the WER queue need an elevated shell, so the owner copies them into `scratch/crash/`, and
  `scripts/read-minidump.py <dump>` prints the bugcheck, the process, the faulting driver and
  the stack's frames by driver. Read the dump before pausing anything on a crash's account.
- **The machine froze twice on 2026-09-26 with nothing in the logs** (Kernel-Power 41 at 17:16 and
  18:18, each a forced restart; no WHEA, no bugcheck, no dump; the last entries in every Claude
  transcript stop at 17:13:40 and 18:14:21). At both, the narrator service's session had about eight
  subagents running `uv` and test suites in parallel worktrees, and at the second the story flow's
  tests had just started a few hundred short processes. The cause is not proven: the CPU's old
  microcode (0x10E) and the file-system filter stack under a burst of process starts (2026-09-22's
  blue screen) both fit. Keep bursts of process starts low (the story flow runs its script tool in
  its own process for this reason), and read `Get-WinEvent` for Kernel-Power 41 before blaming a run.
  **Most of these were a dead display on a live machine** (read 2026-09-29, after the owner said the
  monitor would not wake and they pressed reset). A third reset on 2026-09-26 at 21:26 and one on
  2026-09-29 at 07:19 each follow an `nvlddmkm` event (id 14) under a minute before, with the
  machine still writing events and error reports in between; the card also logged twelve `Graphics
  FECS Exception` events (id 13) during the channel outro's Blender render on 2026-09-28 at 21:07,
  and blue-screened in `nvlddmkm` (0xD1) on 2026-09-29 at 17:37. So a reset after a black screen is
  first read against the `nvlddmkm` events, and Kernel-Power 41's `PowerButtonTimestamp` says whether
  the owner held the button. The two resets of 17:16 and 18:18 carry no driver event and stay open.
- **A farm run started from VS Code gets the fast cores only while VS Code has focus.** The
  machine is an i9-13900K: eight fast cores (logical 0 to 15) and sixteen efficiency cores (16
  to 31). With VS Code in front, round 48's two farm runs sat about 44% on the fast cores and
  26% on the efficiency ones; with any other window in front (Task Manager, the search box,
  another app) about 30% and 37%, at the same total CPU (2026-09-24, `scripts/cpu-watch.ps1`,
  a two-second recorder of load per core type and the focused window, writing under
  `scratch/cpu-watch/`). The fast cores boost
  to about 5.5 GHz and draw several times the power, so the fans follow the focus: the owner
  heard it as the machine "hiding" when Task Manager opened. Nothing was hiding. The cause is
  Windows 11's hybrid scheduling, most likely because the runs were launched from inside VS
  Code's process tree (an inference). It bites a pace or timing read: the same run is faster
  with VS Code in front, so a wall split or a pace compared across two windows of time
  compares the focus as well. On 2026-09-26 it moved the same resume's world step from 157 to
  168 ms to 100 to 119 ms within one afternoon, with no change in the code. The world step is
  the control: an A/B of a physics change compares only runs whose world step reads alike.
- **Read the CPU's microcode before a long run.** The i9-13900K is a 13th-generation chip,
  and Intel's fixes for that generation's voltage degradation (microcode 0x129 and 0x12B,
  2024) come only with a BIOS update. On 2026-09-24 the board (ASUS PRIME Z790-P WIFI) was on
  BIOS 0806 of 2022 with microcode 0x10E after weeks of boosted all-core load. The owner held
  every new run until the flash. On 2026-09-25 they deferred the flash and ruled a lighter load
  instead: one heavy job at a time at about a third of the machine, with Intel's power limits
  set in the old BIOS. HANDOFF carries the current ruling, and the owner's Desktop carries the
  flash steps.
  **The flash was made on 2026-09-29**, after a crash in the NVIDIA driver (bugcheck 0xD1 in
  `nvlddmkm.sys`): BIOS 1836, microcode 0x133, driver 617.14, Intel Default Settings at Performance
  (253 W long and short, 307 A, read on the Ai Tweaker page), MultiCore Enhancement at Disabled -
  Enforce All limits. Loading the defaults (F5) set MultiCore Enhancement back to Auto and TPM Device
  Selection to Discrete; the first was set back and the second left, since the board has no discrete
  chip, Windows still reports Intel's firmware TPM ready, and switching it can offer to clear the TPM. The revision is `Update Revision` under
  `HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0`, little-endian (`0E 01 00 00` is
  0x10E); throttling is `\Processor Information(_Total)\Performance Limit Flags` (0 is none).
  The ASUS WMI classes in `root\wmi` (`ASUSManagement`, `AsusAtkWmi_WMNB`) write the SMBus,
  boot order, passwords and fan curves and are never called.
- **Report the machine's CPU as Task Manager does: `% Processor Utility`, not `% Processor
  Time`.** On 2026-09-24 the agent read 41% from `\Processor(_Total)\% Processor Time` while
  the owner's Task Manager showed 75%. Both were right: `Time` is the share of time a core is
  busy, and `Utility` (`\Processor Information(_Total)\% Processor Utility`, what Task Manager
  has shown since Windows 11 22H2) scales it by the clock against the 3.0 GHz base, and the
  cores were boosting to 1.6 times base (`% Processor Performance` 161). Heat and fan noise
  follow `Utility`, so a load quoted to the owner is that counter.
  **Sixteen busy threads are not half the machine on that counter.** On 2026-09-28 two farm runs
  (4 and 8 threads) and a test suite at 4 read 88% `Utility`, with another session's pytest
  beside them, and the owner asked whether the agent was the load; stopping the 8-thread run took
  it to 38%. Read the counter before adding a job, not the thread count.
- **A worktree goes under `scratch/wt-<name>`, never under `.claude/`.** Claude Code treats
  `.claude` as a protected path: every write inside it asks the owner, and neither an allow
  rule nor bypass mode lifts that. The Agent tool's `isolation: "worktree"` and
  `EnterWorktree` put a worktree there, and the safari branch's, at
  `.claude/worktrees/safari2-r47`, asked on every edit and every render on 2026-09-24 until it
  was moved. The owner had already said "you have full permission to write in this folder";
  the permission was never the problem, the path was. `scratch/` is inside the project and gitignored, so a
  worktree there is written freely, as `scratch/wt-bed` to `scratch/wt-trace2` always were.
  Make one with `git worktree add scratch/wt-<name> -b <branch>`; move one with `git worktree
  move <old> scratch/wt-<name>`, which refuses while any shell's working directory is inside
  it (the PowerShell tool keeps its directory between calls, so `Set-Location` to the main
  tree first). Two more things follow from a worktree being its own checkout. Its scripts take
  the worktree's root as the repository, so `theatre-snap.ps1` and `theatre-film.ps1` run from
  it write under its own `scratch/` and refuse an output path outside it; copy the pictures
  out. They also read runs from the worktree's own `runs/`, which holds none, so a film or
  a snapshot from a worktree takes `-RunsRoot <main tree>/runs` (a canopy check on
  2026-09-24 died on `No arm directory` without it). And its worker (`<worktree>/unity-wN`) takes an edit only by a refresh from the
  worktree. The Agent tool's worktrees still land under `.claude/worktrees/` unless a
  `WorktreeCreate` hook in the settings sends them elsewhere, and that setting is the owner's.
  Until it exists, a subagent that needs a worktree gets one the caller made under `scratch/`,
  named by its absolute path in the brief, and not `isolation: "worktree"`. The fifteen older
  worktrees under `.claude/worktrees/` are left where they are; removing one is the owner's.
- **The Write and Edit tools ask the owner for every file, even in auto mode; a write through
  the shell does not.** On 2026-09-25 the owner, on auto mode, saw "Make this edit to ...?" and
  "Allow write to ...?" prompts, first for an Opus subagent's edits in `scratch/wt-r49cap` and
  then for the main session's own Write and Edit calls under `scratch/` (a worktree's source
  and a helper script alike), and asked five times why. The agent first told the owner its own
  edits did not ask; that was wrong, and the owner's screenshot of a prompt for the agent's own
  file showed it. Files changed through the Bash tool (a Python or heredoc write, the way
  HANDOFF and this file were edited that day) raised no prompt. The documentation (a
  `claude-code-guide` read of code.claude.com's sub-agents, permissions and permission-modes
  pages) says a subagent inherits the parent's auto mode and that only protected paths
  (`.claude`, except `.claude/worktrees`) always ask, so the cause is not known (whether it is
  `scratch/` being gitignored is a guess). The rules until the owner says the prompts are gone:
  **write files through the shell**, not with Write or Edit (a Python script written by a
  heredoc, with the heredoc's backslash halving in mind); **launch no subagent that edits
  files**, since its tools are Write and Edit, and write the code in the main session (read-only
  agents such as `Explore` and `claude-code-guide` are fine; this overrides the delegation
  memory for editing tasks), except a story film's writer and editor, which write their own files
  through the shell, never with Write or Edit, inside the story's folder their brief names (the
  owner, 2026-09-26: a writer with no file to write sent its builder in 17 pieces to a server it
  started for them, and spent ten steps on the shell's quoting); and **every write under `~/.claude/` asks the owner** whatever the
  tool, the memory directory included, so memory edits are rare and batched and durable rules
  go here. The fix is the owner's setting and never the agent's: the documented allow rule for
  this tree, untested here, is `Edit(//d/Projects/experiments/evolution-simulator/**)` (and the
  same for `Write`) under `permissions.allow` in the owner's `~/.claude/settings.json`; the
  agent never edits a permission setting, whoever asks.
