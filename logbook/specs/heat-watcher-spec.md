# The heat watcher: a spec

Drafted 2026-10-05 at the owner's request, after the all-cores trial. Nothing here is built yet. The
owner rules on the open questions at the end before it is.

## Why

The machine's heat is watched in three ways today, and none of them tells a person in time.

- **The watchdog** (`scratch/heat-watchdog/watchdog.py`, kept out of git by the owner's ruling of
  2026-10-03) runs beside one long job. Every minute it reads the last minute of MSI Afterburner's
  one-second log, and it stops the job by its id when the machine is hot. It tells no one until
  someone reads its log.
- **The session's check** (`scripts/temp-check.ps1`) takes one look every 30 minutes. A look is one
  second in 1,800. During the all-cores trial of 2026-10-05 its looks read 63 to 84 °C, all OK, while
  the one-second log showed a peak of 94 °C, 60 s at or above 85 °C and the processor in the high 80s
  for the whole half hour.
- **The curve itself** disappears. Afterburner's log is rotated every day at 04:00, so the trial's
  curve survives only because evolution-simulator-51 summarised it by hand.

The watcher closes the gap between the log and a person. It reads the same log every 30 s and says
something only when the machine's state changes. Each thing it says wakes the session within seconds,
and the session tells whoever is loading the machine, and the owner when it matters.

## What it is not

- **It is not the safety.** The watchdog beside each long job still stops that job by itself. A
  session sees an event only between its turns, and if the session dies nothing hears the watcher. A
  job's safety must never wait on an agent.
- **It is not a loop that outlives its session.** On 2026-09-20 shell loops behind monitors outlived
  their session and multiplied until the desktop went down (`CLAUDE.md`, "A watch is one look that
  exits"). The watcher carries its own deadline and exits by its own clock, at most 31 minutes after
  it starts, whether or not anyone is listening. The session re-arms it. `scripts/sweep-orphans.ps1`
  must list it by its command line.
- **It starts nothing and stops nothing.** It reads and prints.

## The signals

Each signal comes from a source this machine already has, read without administrator rights.

| signal | source | why it matters | read today |
|---|---|---|---|
| CPU temperature, the hottest of the package and the 32 per-core columns | Afterburner, columns 19 to 51 | the processor's own heat; the per-core columns show a hot spot the package hides | by the watchdog, and by `temp-check.ps1` while Afterburner runs |
| CPU package power, W | Afterburner, column 118 | current drawn under load, the closest this log comes to the stress the i9-13900K's instability is said to come from | no |
| CPU clock under load | Afterburner, columns 85 to 117 | a clock falling while the load holds is the processor throttling, seen before its limit flags | no |
| CPU limit flags, and firmware limiting in the last 15 minutes | Windows' performance counters and the Kernel-Processor-Power events | the chip protecting itself | by `temp-check.ps1` |
| GPU temperature, power, fan and its temp, power and voltage limits | Afterburner, columns 0 to 18 | the card, and a fan that stops | by the watchdog (temperature and temp limit only) |
| Hardware errors | the System log's `Microsoft-Windows-WHEA-Logger` events | a corrected machine-check error is how an unstable processor usually first shows itself | no |
| A crash or a driver reset | Kernel-Power 41, `nvlddmkm` events | a black screen or a reset (`CLAUDE.md`'s post-mortem rule) | only after the fact, by hand |
| Afterburner's log going stale | the log's last row older than 120 s | without it the processor's temperature is invisible, and the watch is blind | by the watchdog, written down and never acted on |
| Free space on the drive that holds `runs/` | the file system | not heat, but the other thing that ends a long run without warning | no |

The hardware error log was read on 2026-10-05 and holds one event: an informational record (id 3)
logged 10 s after a startup on 2026-09-29 at 18:45, in a run of three orderly reboots that Windows
began. It is not a machine-check error. It is the baseline: the watcher counts events after it.

**What this machine cannot show without a new tool.** The processor's core voltage, the voltage
regulators' temperature, the drives' temperature, the memory's temperature and the case and processor
fans' speeds. Windows refused the drives' reliability counters without administrator rights. A
monitoring tool that reads the motherboard's sensors (HWiNFO, or LibreHardwareMonitor) would give all
of them, but installing one is the owner's call.

## States and what each one does

The thresholds are the watchdog's, so the two agree. A level counts only when it holds for 10 s or
more in a minute, so one hot second does not fire.

| state | when | the watcher prints | the session does |
|---|---|---|---|
| OK | none of the below | nothing, except once when it returns to OK | — |
| WARN | 10 s in a minute at CPU ≥ 90 °C or GPU ≥ 80 °C; or the CPU's limit flags set; or package power above 253 W for 10 s | one line: the time, the state, the reading that set it | tells whoever loads the machine; notes it in the status |
| HOT | 10 s in a minute at CPU ≥ 95 °C or GPU ≥ 85 °C; or the GPU at its temp limit; or the CPU limited with firmware limiting logged | one line | tells the loader, pushes a notification to the owner, and checks that the watchdog stopped its job |
| DEPENDENCY | Afterburner not running, its last row older than 120 s, the CPU temperature reading `N/A`, or the event log unreadable | one line naming what failed, and one when it clears; the heat states are not judged meanwhile | says in the status that the processor's heat is not watched; pushes to the owner if a long job runs |
| HARDWARE ERROR | a new WHEA event, a Kernel-Power 41 or an `nvlddmkm` event | one line with the event's id and time | pushes to the owner at once; no new heavy work starts until the owner has seen it |
| DISK LOW | under 50 GB free on the drive that holds `runs/` | one line | tells the loader and the owner |

The 253 W line is a belief and not checked here: it is Intel's default package power limit for the
i9-13900K as I understand Intel's 2024 guidance, and should be checked against Intel's own
documentation before it is relied on.

The watcher prints a state once when it enters it and once when it leaves, never every 30 s. When it
exits at its deadline it prints one summary line for its window: the hottest CPU and GPU readings,
the seconds at or above 85, 90 and 95 °C, the highest package power, and whether the log was ever
stale. It covers the watch's own window from its START. The rows read to fill the first minute feed
the rolling minute but not the summary. That line replaces the session's 30-minute look.

## Shape

- `scratch/heat-watchdog/heat-watch.py`, beside the watchdog, standard library only. The owner ruled
  on 2026-10-05 that it stays out of git, since it is a tool for this one machine, and that it starts
  without any new monitoring tool, on what Afterburner and Windows already give.
- **It checks its own dependencies** (the owner, the same day): that Afterburner is running, that its
  log is fresh, that the header names every column it needs, that the CPU temperature reports numbers
  and not `N/A`, and that the System event log can be read. A failure at start exits with "could not
  run". A failure later prints a DEPENDENCY line, and the heat states are not judged until it clears,
  so a blind watcher never reports OK.
- Its options: `--until <time>` (required, at most 31 minutes ahead), `--interval 30`,
  `--runs-drive D`, `--spike <C>` (off by default, open question 1), `--replay --from <time> --to
  <time>` for testing, and `--log` and `--header` to point it at test fixtures. Only one watcher runs at
  a time; a second exits naming the first's process id.
- **It reads every row, not the latest one.** It keeps a cursor, the byte offset it has read up to.
  Every 30 s it reads all complete rows written since the cursor (about 30), leaves a half-written last
  row for the next pass, and moves the cursor. Each row feeds a rolling 60-s window that counts the
  seconds at or above each threshold, and the window's running maxima: the hottest CPU core, the
  package, the GPU, the highest package power and the lowest loaded clock. So no second goes unread.
  When the file is shorter than the cursor, Afterburner's log has been rotated (daily at 04:00). The
  watcher then starts again at the new file's first row and says so in its summary.
- It opens Afterburner's log the way the watchdog does: shared, read-only (`open(path, "rb")`).
  Afterburner reopens the file for every row and stops logging for good the first time it cannot.
  It reads the column names from `D:\Monitoring\archive\header.hml`.
- The session arms it with the Monitor tool and re-arms it from its half-hourly cron. Its stdout is
  the event stream, one line an event.
- It reads the header once at start. If Afterburner's columns change while it runs, every new row has
  the wrong width and is skipped, so the watcher reports a DEPENDENCY line for a stale log instead of
  misreading a column. The next re-arm reads the new header.
- `logbook/specs/machine-and-tools.md` ("Heat") says what it is and where it lives, beside the
  watchdog's description, and that the watchdog stays the safety.

## How it is checked before it is used

1. **Replay of the all-cores trial.** `--replay` over 2026-10-05 11:45:50 to 12:15:52 must print no
   WARN under the rule as written, because no minute held 10 s at or above 90 °C (6 s in the whole
   half hour). Its summary line must match 51's hand summary: peak 94 °C, 60 s at or above 85 °C, 6
   at or above 90 °C, none at 95 °C. It does, exactly (2026-10-05). A one-second report at 93 °C (open
   question 1) prints once, at 11:59:39. The minute maxima's 91 °C at 12:09 is under 93, so it does not
   fire there; at 91 °C it fires at both.
2. **Replay of a quiet hour**, which must print nothing but its summary.
3. **Stale and missing logs**: a copy of the log cut 3 minutes short must print a DEPENDENCY line, and a missing
   log must exit with "could not run", not OK.
4. **Its exit codes** tell "could not run" apart from every verdict, so a failure to read is never
   taken for a cool machine.
5. One live window of 30 minutes beside a run, read against `temp-check.ps1`'s looks.

## Open for the owner

1. **The 10-s rule against the trial.** Under the watchdog's thresholds the trial's 94 °C peak fired
   nothing, because no minute held 10 s at 90 °C. Whether a brief 94 °C should be reported at all is
   a heat ruling. A cheap middle way is to report, never stop, on any single second at or above 93 °C.
2. **A per-run heat summary.** The exit summary written beside each run (a `heat.jsonl` in the run
   directory) would answer "did round X run hot?" later. It adds a file to every run directory.
3. **A sensor tool for voltage, regulators, drives and fans.** Ruled on 2026-10-05: not for now. The
   watcher starts on what Afterburner and Windows give. LibreHardwareMonitor stays the candidate if one
   is wanted later (open source, a signed driver, no daily limit), installed by the owner, beside
   Afterburner and not in place of it.
4. **The response to a hardware error.** The spec stops new heavy work until the owner has seen it.
   Whether a running round is stopped as well is the owner's.
