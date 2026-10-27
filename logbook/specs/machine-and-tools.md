# The machine and the agent's tools

Split out of the root [`CLAUDE.md`](../../CLAUDE.md) on 2026-09-30 so that it is read only when work reaches this area. Read when the machine misbehaves (a freeze, a black screen, the fans), before a pace or timing comparison, and when a Claude Code tool asks or refuses unexpectedly. The load ruling in force is HANDOFF's.

The root file's rules still apply. The entries below were moved word for word, so their dates, their "until" clauses and any reference to "this file" or "CLAUDE.md" are as they were written in the root file.

## Gotchas

- **Windows Update restarts the machine by itself at night.** At 01:29 on 2026-10-09 it installed
  the 26H2 feature update (build 26300) and restarted. The System log shows it as User32 event 1074 from
  `MoUsoCoreWorker.exe` on behalf of SYSTEM, followed by a clean shutdown, with no Kernel-Power 41. Every
  Claude session and every cron died with it. Read event 1074 before blaming a run for a restart. A long run
  left overnight resumes from its last checkpoint. After a feature update, re-run a known reference before
  comparing any pace across the update.
- **A WMI Provider Host spike in Task Manager is usually WSL, not a run.** On 2026-10-19 the owner saw it at
  54% of the machine. Over a few seconds it averaged 5 to 10%. The `Microsoft-Windows-WMI-Activity/Operational` log
  showed `wslservice.exe` restarting the WSL VM every three minutes or so, and re-registering its Hyper-V firewall
  rules through WMI at each restart. No farm run, Unity or watch uses WSL. Our own `Get-CimInstance Win32_Process`
  listings (the orphan sweep, process checks) add bursts of a few seconds. Read that log's `ClientProcessId`
  before blaming a run. Stopping WSL is the owner's call.
- **A session with TEMP at `C:\WINDOWS\TEMP` and no `python` means the user Path grew too long to load.** On
  2026-10-19, after a restart, every Claude session came up like that. The user Path in `HKCU\Environment` had grown
  to 5,331 characters, about fifty of them `...\.dotnet-home\.dotnet\tools` entries. The .NET SDK's first run adds
  one each time a build sets `DOTNET_CLI_HOME` to a new folder. The owner trimmed it to 1,207 characters, and a new
  shell had Python at once without signing out. A session started before the fix keeps the old environment until
  VS Code is restarted. Any build that sets `DOTNET_CLI_HOME` also sets `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`.
  Changing the user's environment is the owner's step, never an agent's.
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
  **Most of these were a dead display on a live machine** (read 2026-09-29, after the
  monitor would not wake and the machine was reset). A third reset on 2026-09-26 at 21:26 and one on
  2026-09-29 at 07:19 each follow an `nvlddmkm` event (id 14) under a minute before, with the
  machine still writing events and error reports in between; the card also logged twelve `Graphics
  FECS Exception` events (id 13) during the channel outro's Blender render on 2026-09-28 at 21:07,
  and blue-screened in `nvlddmkm` (0xD1) on 2026-09-29 at 17:37. So a reset after a black screen is
  first read against the `nvlddmkm` events, and Kernel-Power 41's `PowerButtonTimestamp` says whether
  the power button was held. The two resets of 17:16 and 18:18 carry no driver event and stay open.
  **Two driver resets on 2026-10-08, the machine staying up.** At 08:59:58 an `nvlddmkm` 153 came as
  a card test that had hung for 20 minutes was killed; the hang was ILGPU 1.5.3's double sine, which
  reduces an angle by subtracting 2π in a loop (`src/Evosim.Farm.Gpu/CLAUDE.md` on `card-r57`). At
  09:35:35 an id 14 (`CMDre`) and at 09:37:45 to 09:38:02 a full TDR (153: resetting, reset,
  restarting) came while a test batch meant for ILGPU's CPU accelerator ran. By reading its code it
  opened no CUDA device. The display had stayed dark after the first reset and came back in the end;
  the second reset was read as the first one's aftermath. The display-side fault above fits that (an
  inference; nothing in the batch's log can show it). The machine was restarted at 09:49 and logged no
  driver event after boot. Since then every job meant to stay off the card runs with
  `CUDA_VISIBLE_DEVICES=-1` and reports whether its process loaded `nvcuda`.
- **A farm run started from VS Code gets the fast cores only while VS Code has focus.** The
  machine is an i9-13900K: eight fast cores (logical 0 to 15) and sixteen efficiency cores (16
  to 31). With VS Code in front, round 48's two farm runs sat about 44% on the fast cores and
  26% on the efficiency ones; with any other window in front (Task Manager, the search box,
  another app) about 30% and 37%, at the same total CPU (2026-09-24, `scripts/cpu-watch.ps1`,
  a two-second recorder of load per core type and the focused window, writing under
  `scratch/cpu-watch/`). The fast cores boost
  to about 5.5 GHz and draw several times the power, so the fans follow the focus: the owner
  heard it as the machine hiding when Task Manager opened. Nothing was hiding. The cause is
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
- **Heat: watch the temperatures, and stop the work if the machine gets too hot** (the owner,
  2026-10-01: monitor the machine's temperatures, and if they look too hot, stop the work, for
  the CPU and the GPU alike; the agent may also tell the other agent to stop if the GPU gets too
  hot). `scripts/temp-check.ps1 [-Log]` takes one look and exits: 0 OK, 1 WARN, 2 HOT, and with `-Log`
  a row in `scratch/temps/temps.tsv`. While any heavy job runs, a session cron runs it every 30
  minutes. It ran every ten until 2026-10-03, when the agent recommended 30 among other
  savings and the owner agreed. A look every 30 minutes catches slow drift, such as a warm room, dust or a
  fan running down, and the rule of two WARNs in a row acts on it within an hour. Nothing that
  polls catches a fast event, such as a fan stopping or a driver reset; the chip's own throttle
  at 100 °C covers that, at any interval. The replicate ran 13.6 hours at 14 threads on the
  efficiency cores with the CPU between 53 and 83 °C at every look. A local watchdog that looks
  every minute and stops the job itself, ending at a fixed deadline, would close more of the gap
  at no token cost. It is an exception to the rule against background loops, so it was put to
  the owner on 2026-10-03. The owner agreed and ruled that it stays out of the repository, as a
  matter of the machine rather than of the code. It lives in `scratch/heat-watchdog/`, which is gitignored, and reads MSI
  Afterburner's one-second log at `D:\Monitoring\HardwareMonitoring.hml`. That log is
  Afterburner's own, and the ml-store project rotates it daily at 04:00. Open it only so that
  writing stays shared, as Python's `open(path, "rb")` does: Afterburner reopens it for every
  row and stops logging for good the first time it cannot.
  - **The heat watcher** (`scratch/heat-watchdog/heat-watch.py`, out of git by the owner's ruling of
    2026-10-05, a machine tool like the watchdog) reads every row of the same log through a cursor and
    prints a line only when the state changes, so a session armed with it hears of a change within
    30 s. It checks that Afterburner and its log are working before it judges anything. It exits by
    its own clock within 31 minutes and is re-armed by the session. It tells. It never stops anything,
    and the watchdog stays the safety. Its spec is `logbook/specs/heat-watcher-spec.md`. A session arms
    it with the Monitor tool (timeout 1,800,000 ms) on `cd /d/Projects/experiments/evolution-simulator/scratch/heat-watchdog && python heat-watch.py --until "$(date -d '+29 min' '+%Y-%m-%d %H:%M')" 2>&1`,
    and re-arms it from a half-hourly cron, after checking `heat-watch.lock`'s process id is not alive.
    Its fixture tests are `scratch/heat-watchdog/test/run-tests.py`.
  - **Nothing runs beside a card group.** A card group launched with `-HeatStop` stops every world at the
    first CPU sample at or over 90 °C. On 2026-10-12 round 58's isolated group stopped so at 12:23, at 91 °C
    for one sample. A subagent's Python read on every core ran beside it, and reads on four of the
    group's own cores had ended minutes before (logbook/0139). The group was resumed from a checkpoint and
    nothing was lost. Since then, no read, build or subagent process of anyone's runs beside a card group,
    and a subagent's brief says so while one runs.
  - **Nor beside a theatre render step.** `scratch/theatre-realism/batch-heat.ps1` stops a render batch at
    89 °C. On 2026-10-25 at 13:51 it read 90 °C and stopped batch b43. A Farm build at one MSBuild node and
    two threads on logical 2-15, given its slot by the coordinator, ran beside a Unity step, with small test
    jobs as well. Builds and tests run only in a HOLD window, with no render, and none between render steps:
    on the same day, a small job started 36 s after a step ended reached 89 °C and cost the batch two takes. A
    compile ignores the thread count and uses every core in its affinity mask, which is how 14-core masks
    reached 89 °C twice that day. Each job in a HOLD window runs on a mask of four cores (for example
    `0xF0000` or `0xF00000`), with at most two such lanes at once and their compiles staggered.
  - **Memory is committed by idle sessions, not only by runs.** On 2026-10-27 at 09:58 the machine had 63.7 GB of
    RAM with 17.7 GB free, and 90.8 of 103.7 GB committed. Growth group 2's farm process held 7.4 GB. Twenty
    `claude.exe` processes, from sessions opened between 2026-10-23 and 2026-10-25, held about 10 GB between them,
    and each Claude Code session's own `narration.mcp` Python held about 1.06 GB. VS Code's 42 processes held
    16.8 GB. The owner asked that day that stray processes be watched (the owner, 2026-10-27), so each look at
    the machine reads committed memory beside the heat. Closing an idle session frees about 1.5 to 2.5 GB with
    its helpers, and which sessions close is the owner's choice.
  - **A helper command starts pinned while a card run is up.** On 2026-10-27 at 07:43:11 the CPU read
    88 °C for one second at 156 W, beside growth group 2 on 22 to 31. The theatre's renders on 16 to 21
    peaked at 80 °C that half hour. The spike came from the theatre agent's own helper commands, unpinned
    on the fast cores: a cold `pwsh` start that parse-checked an 800-line script, and two short Python
    reads. A process's start burst lands wherever Windows puts it, so a helper is launched already pinned
    and at below-normal priority, for example `cmd /c start /b /wait /belownormal /affinity 3F0000 <command>`
    for 16 to 21, rather than pinned from inside once it runs.
  - The GPU (RTX 4090) is read by `nvidia-smi`. WARN at 80 °C or a hardware slowdown; HOT at 85 °C or a
    thermal slowdown (clock event reasons 0x20 or 0x40).
  - **Afterburner's log reports the GPU at 0 °C.** On 2026-10-27, during `r62cf`, `nvidia-smi` read 49 °C
    at 92 % while the log's GPU temperature column held 0, so `heat-watch.py`'s "GPU max" is blind. A card
    run's GPU heat is covered by its own heat stop, which reads `nvidia-smi`. The heat watch covers the CPU
    only.
  - The CPU's temperature comes from MSI Afterburner, which the owner starts. While it runs, its
    monitoring shared memory (`MAHMSharedMemory`) holds every core's temperature and the package's,
    and the check opens it read-only. WARN at 90 °C, HOT at 95 °C, on the hottest reading. The
    i9-13900K protects itself at 100 °C. Nothing else on this machine gives the die's temperature
    without an administrator. The motherboard's ACPI zone is not the die, the WMI sensor needs an
    administrator, and `root\Hardware` holds no sensors. The ASUS WMI classes are never called.
  - Whether or not Afterburner runs, the chip's own protection is read too. WARN when
    `Performance Limit Flags` is not 0, or when firmware limiting (Kernel-Processor-Power, event 37)
    is logged in the last 15 minutes. HOT when both are.
  - **The cooling.** The CPU has an air cooler, a tower of metal fins (the owner, 2026-10-05); the
    board is an ASUS PRIME Z790-P WIFI and nothing is on its AIO Pump header. The fans are set in
    Armoury Crate's Fan Xpert, which the owner controls, with AI Cooling II off. The board keeps
    applying the saved curves when Fan Xpert's page will not open: after the restart of 2026-10-19 it
    showed Page Error H-2004 even after Armoury Crate was restarted, while `AsusFanControlService` ran
    and the owner heard the fans follow the load. The curves can also be set in the BIOS (Monitor, Q-Fan
    Configuration), which needs a restart. Since 2026-10-06 at
    17:17 the CPU fan runs 30% to 20 °C, 45% at 40 °C, 80% at 50 °C and 100% from 70 °C; chassis fans 1
    and 2 follow the CPU's temperature, 30% to 20 °C, 45% at 40 °C, about 70 to 75% at 72 to 75 °C and
    100% from 80 °C, quieter than before. Fan Xpert's CPU reading is the board's, about 53 to 55 °C under
    a farm round when Afterburner's hottest core averages about 67 °C, so the chassis fans then run at
    about 55%, near 1,030 rpm. On round 57 alone (about 151 W) the half hour before the change and the
    half hour after both peaked at 83 °C with no second at 85; the mean went from 67.2 to 65.9 °C and
    the 95th percentile from 72 to 70 °C (measured, `heat-blocks.py`).
    Afterburner's log holds the graphics card's fans only, so the case and CPU fans are not
    recorded. `scratch/heat-watchdog/fan-compare.py "<from>" "<to>"` prints the CPU's heat minute by
    minute over a window, to compare before and after a fan change, and `heat-blocks.py
    "label=<from>/<to>" ...` gives one line per block (peak, seconds at or above 85, 90 and 93 °C,
    mean, power, clock). On the farm's load, about 160 W, the settings took about 3 °C off the hottest
    core. On the nursery's load they took nothing off: on 2026-10-05 the same 15 minutes of A1's
    nursery ran 75.8 °C mean with a peak of 87 °C pinned in the morning and 77.1 °C with 90 °C in
    the evening, and on all cores 79.6 °C with 94 °C against 81.6 °C with 95 °C. At 180 to 210 W this
    air cooler is limited by the chip and the cooler, not by the fans (an inference; the room's
    temperature was not measured, and the evening's build had A3).
    On 2026-10-16 round 61's four worlds alone on the card (8 threads on 16 to 23) peaked at 85 °C in
    their first half hour and at 89 to 91 °C from about 1.5 h on, as their crowds grew, with 1 to 4
    seconds a half hour at 90 °C or above (`heat-watch.py`). The owner raised the fan curves at 21:50
    that day; the new values were not recorded. The half hour 21:07 to 21:36 peaked at 91 °C, with 8
    seconds at 85 °C or above and 4 at 90; the half hour 22:05 to 22:34 peaked at 81 °C, with none at 85
    (CPU power at most 181 W). One half hour a side, and the round's load moves with its crowds, so the
    fans' share of the fall is a reading, not a measurement.
    The single seconds at 90 °C and over in a card run are not the farm's. On the morning of 2026-10-17 the farm's
    cores (logical 16 to 23) held 60 to 70 °C while one fast core (logical 10 and 11) reached 90 to 96 °C for one
    second about every 5 minutes (09:29:26, 09:34:29, 09:44:29, 09:59:26, 10:09:29), with nobody at the machine. A
    per-process sample each second (`typeperf "\Process(*)\% Processor Time"`) put `AvastUI` at 49 to 77% of a core
    in each of the three spikes it covered, with `WmiPrvSE` and `AsusFanControlService` beside it in some. So Avast's
    interface wakes every 5 minutes and boosts a fast core to full clock for a second. The antivirus is the owner's to
    configure.
    The room counts. With the fans loud under load and the room warming, the half-hour peaks
    climbed back to 89 to 91 °C; after the room was ventilated at about 10:45 on 2026-10-17 they fell to 83 to 86 °C
    under the same card run (one morning, the load not held equal).
    The upgrades weighed that day: on this board (LGA1700) only an i9-14900K or KS fits, a few percent
    faster and hotter, so no CPU change. A contact frame and a 360 mm liquid cooler, or a CPU power
    limit of about 180 to 200 W, would take heat off; none is chosen.
  - **A two-processor build on the fast cores spikes the heat. The same size of job on the
    efficiency cores does not.** On the night of 2026-10-07, round 57's seed 1 (8 threads on 16 to 23)
    ran first beside the light-sense builder's compiles and tests, pinned to logical 4 and 5, then
    beside jobs on 24 to 29 (measured, `heat-blocks.py`):

    | Beside seed 1 | Time | Peak | Seconds at 85 °C or above |
    |---|---|---|---|
    | The builder, logical 4 and 5 | 02:00 to 02:18 | 93 °C | 11, three of them at 90 or above |
    | A fixture farm run and the nursery's compiles and suites, 24 to 29 | 02:18 to 02:31 | 79 °C | 0 |

    The fast cores boost far higher under a short burst. So a build beside a round goes on the
    efficiency cores, 16 to 31, unless it needs to be quick.
  - **All eight fast cores busy hold the CPU at 85 °C. Four do not.** On 2026-10-09 the other
    project's CPU job ran with nothing else heavy beside it (measured, `heat-blocks.py`).
    On logical 0 to 15, at about 178 W, it held 85 °C or above for 41 of its 72 seconds. Its mean
    was 80.6 °C and its peak 89 °C. On 0 to 7, at about 126 W, it averaged 75 to 77 °C over 22
    minutes and peaked at 85 °C, for one second. A long job that is granted the fast cores gets
    0 to 7. Sixteen threads go on the efficiency cores, or on 0 to 15, only under the heat watch.
  - **All sixteen efficiency cores busy reach 90 °C too.** On the night of 2026-10-13 two nursery arms
    at 8 threads each, on 16 to 23 and 24 to 31, peaked at 91 °C in their first half hour beside short
    jobs on 0 to 3, and at 92 °C in the next with nothing beside them: 907 of 1,732 seconds at 85 °C or
    above and 20 at 90 or above (measured, the heat watch). One such arm alone on 8 efficiency cores
    ran at 73 to 86 °C. Narrowing the running processes to 4 cores each brought the CPU from about 86
    to 74 °C within seconds. A farm or nursery process is bit-identical at any thread count and on any
    cores, so its affinity may be narrowed mid-run instead of restarting it; its record still reads
    the threads it started with, and only its pace changes.
  - **A Core suite on fast cores beside a run on 8 efficiency cores reaches 94 °C.** On 2026-10-14 a
    nursery arm on 24 to 31 alone peaked at 85 to 88 °C. Core -All added on 4 to 7 and 12 to 15 took
    it to 93 °C within a minute, and narrowing the suite to 4 to 7, then to 4 and 5, still left
    spikes of 93 and 94 °C. Over that half hour, whose last ten minutes had the suite alone on 16
    threads, the CPU spent 108 seconds at 90 °C or above (measured, the heat watch). A test process can be narrowed mid-run, as a farm
    can, but a suite beside a long run belongs on 4 threads at most, or after it.
  - **Twelve busy efficiency cores leave no room for a burst on the fast cores, and `-HeatStop` stops on
    one sample.** On the night of 2026-10-14 a card group ran 12 threads on 16 to 27, with a nursery arm
    on 2 to 4 of 28 to 31, and peaked at 88 to 89 °C alone. The group stopped twice at its first sample
    at or over 90 °C (measured, the card launchers' logs and the heat watch):
    - at 22:10, at 92 to 94 °C, when a 2-thread test batch on 0 and 1 and a 2-core nursery check began
      within the same minute;
    - at 02:22, at 94 °C, 15 seconds after a 2-thread dotnet build began on 0 and 1. Three seconds later
      the CPU read 78 °C;
    - at 02:57, at 91 °C, with the group narrowed to 8 threads on 16 to 23, 13 seconds after a 2-thread
      dotnet run began on 0 and 1.

    So the group's width is not the cause. The start of a dotnet build or run on the fast cores spikes
    the package past 90 °C beside a busy group, whatever its width. Each stop costs a resume. On round
    59's build a card resume was not the unbroken run (round 59's B3). From round 60's build
    (`r60-fed`) it is. On 2026-10-15 `r60s-big3`, resumed on the card from 5,000 s, matched its unbroken
    run in every row to 10,000 s and in its 10,000 s checkpoint byte for byte. A card group with
    `-HeatStop` still runs with nothing starting on the fast cores beside it. Builds, test runs and
    runner batches wait for the gap between groups, or run on free efficiency cores. A process already
    running and steady, such as the room server or a nursery arm on the efficiency cores, may stay.
  - **Short jobs on two efficiency cores beside a card run stay cool.** On 2026-10-15 a card run held 16
    to 23 and a nursery bench held 28 to 31. The CPU's hottest core averaged 71 °C. Two of the other project's jobs,
    each a dotnet build and its runner checks at 2 threads on logical 24 and 25, ran 2.5 and 5.5
    minutes. The hottest core peaked at 82 and 84 °C and averaged 76 °C, never reaching 85. The power
    rose by about 20 W (measured, the heat watch's log read with `heat-blocks.py`). So during a long card
    run, the other project works on two free efficiency cores rather than holding, and nothing of anyone's starts
    on 0 to 15.

    At 02:59 the same night, with no group running, the hottest core read 91 °C (measured, the heat
    watch). Only two jobs were running: a 2-thread dotnet run on logical 0 and 1, and a nursery arm on
    30 and 31, about 3.6 cores busy in all. Two fast cores at full boost reach 90 °C on their own, and
    the heat checks read the hottest core. So these spikes are local hot spots of a boosting fast core,
    not the package's heat (an inference). The owner ruled that `-HeatStop` stops on two samples in a
    row at or over 90 °C (D183).
  - The first reading with Afterburner (2026-10-01 17:49): the package at 82 °C and 208 W, the
    hottest core 76 °C, with the nursery at 14 threads and the other session's renderer at about one
    core.
  - On HOT, or on WARN at two looks in a row: stop the chain that would start the next job first, then
    the job. A nursery run has no manifest, so its process is stopped by id. A farm run is stopped with
    `stop-arm.ps1`. If the GPU is hot, message the other sessions using it and ask them to stop. Then
    tell the owner, and start nothing until the owner has read why.
- **Never `cd` or `Set-Location` into a worktree.** Claude Code asks before a write outside the
  session's working directory, so from inside a worktree every write to the main checkout asks.
  Reach a worktree from the project root with absolute paths, `git -C <worktree>` and
  Start-Process's `-WorkingDirectory`. Make one with `git worktree add scratch/wt-<name> -b
  <branch>`. A worktree is its own checkout:
  - its scripts take the worktree's root as the repository, so `theatre-snap.ps1` and
    `theatre-film.ps1` run from it write under its own `scratch/`; copy the pictures out;
  - they read runs from the worktree's own `runs/`, which holds none, so pass
    `-RunsRoot <main tree>/runs`;
  - its Unity worker (`<worktree>/unity-wN`) takes an edit only by a refresh from the worktree.
- **The owner's guard hook** (`~/.claude/hooks/guard.ps1`) asks before a force push, a history
  rewrite, or a write outside the git repository, the memory folders and `%TEMP%\claude\`. Its
  prompt is the owner's to answer.
- **A refusal in one session is approved in that session** (2026-10-10). Claude Code's auto-mode
  classifier refused the speed agent's script for a several-hour unattended card chain, launched on the
  coordinator's word. The owner then said yes twice, in the coordinator's session, and neither yes
  reached the speed agent. A peer's message is never an approval, and the classifier treats any other
  route to the same outcome as a bypass. So when a peer's step is refused, ask the owner to approve it in
  the peer's own session, by that session's name, and never run the step from another session.

- **Sessions talk to each other, but not to Claude Design** (2026-10-02). A Claude Code session can
  message another by its name from `ListAgents`: the local sessions, the cloud sessions and the Remote
  Control ones. The owner's claude.ai Claude Design session is not in that list, so no agent can
  message it; the owner carries messages between them. A correction, from later the same day: the
  owner's design work also ran as a Claude Desktop session, "Cambrian Nursery Design", and that one
  is listed by `ListAgents` as a Claude Desktop session. So it can probably be messaged like any
  peer. No session has tried, and nothing reports back from a Desktop session, so silence is not an
  answer. An agent can reach a design only as a Design
  artifact, through the Artifact tool, which the owner can comment on. Publishing one is outbound and
  needs the owner's approval each time. `DesignSync` syncs a design-system project only inside the
  `/design-sync` skill, which the owner starts, and is never for making a design.
