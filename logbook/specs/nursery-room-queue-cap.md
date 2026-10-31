# The nursery room's cap: the machine's queue and the heat check

The build brief for the room's load cap, as the owner ruled on 2026-10-11. The room stops reading
`% Processor Utility`, which counts a boosting core as more than one and so refused even the room's own
one-thread STEP. It asks two other things instead: which cores the machine's queue gives it, and
whether the machine is too hot. This follows the owner's rule of 2026-10-09 that load is no bar, and
that heat and a little headroom are. The room's build spec is `logbook/specs/nursery-room-build.md`.
Its rule holds here: a screen change works only when the end-to-end flows pass.

## What the room reads

The queue is `scratch/machine/queue.md` in the main checkout, found as the registry is, and the
coordinator owns it. The room reads only its Running section, from the `## Running` heading to the next
heading. Each line there that starts with a dash is one job, and its tags follow its text, each after a
middle dot:

- `cores: a-b[,c-d]` names the logical processors the job holds, such as `cores: 16-31` or
  `cores: 0-3,8-11`. A single processor is written `a`.
- `card` says the job uses the graphics card. The room never uses the card; it shows the tag and does
  nothing else with it.
- `room` marks the room's own slot. The coordinator sets it, and moves the slot by changing that line's
  `cores:`.

A job line with no cores tag, with two, or with one the room cannot read, claims every processor, and the
room says which line did it. The room's own line is the exception: when its cores cannot be read, the slot
is not known and the room starts nothing. Prose between the job lines is not read.

The heat check is `scripts/temp-check.ps1`, run with `pwsh`. It prints one line and exits 0 for OK, 1 for
WARN, 2 for HOT and 3 when it cannot read. PowerShell also exits 1 when a script fails, so an exit code
whose printed line does not name that verdict is read as unreadable. A reading is kept for 60 seconds.

## The rule

A launch of `n` threads, and an episode step of one thread, is refused when any of these holds:

1. The queue cannot be read, or its `## Running` section is missing.
2. No Running line carries `room`. The room has no slot, so it starts nothing until the coordinator
   gives it one.
3. The heat check reads HOT, or cannot read.
4. `n`, plus the threads the room has granted that still count (`GrantedThreads`, as now), is more
   than the free cores.

The free cores are the `room` lines' cores, inside room.json's `affinity`, less every core another
Running line claims, less logical 4 and 5, which the room never uses. A launch is pinned to those free
cores, and its result and its activity row record the mask that was applied. A WARN starts the job and
the result says so.

room.json's `affinity` becomes the bound the room never leaves, `0xFFFFFFCF` by default: every
processor but 4 and 5. The slot replaces `capLogical`, which is removed, and two keys are added,
`queueFile` and `heatScript`. Every refusal names what refused it, in the room's existing manner. A
queue line is named by its text before the first middle dot, with the cores it claims. The heat is named
by its verdict and its line, and a count by the free cores against the threads asked.

## What changes besides

- `/api/room` and the screens show the slot, the free cores, the lines that claim cores inside the
  slot, and the heat verdict, in place of the load percentage. The tank screen's STEP still says its
  refusal before it is pressed.
- `ILoadReader`, `TypeperfLoadReader` and the utility arithmetic in `Cap` go. A queue reader and a heat
  reader take their place, each an interface the tests give a value, as `ILoadReader` was.
- The tests cover the queue's parse, each refusal's words and the mask a launch is pinned to. The parse
  cases are tags, ranges, a line without tags, a bad tag, prose between lines and a missing section.
- No test computes a processor from `Environment.ProcessorCount`, which `DOTNET_PROCESSOR_COUNT` caps.
  Under a cap of 4, a processor "the machine lacks" came out as logical 4, and the real launcher pinned a
  process there. A test that needs the machine's processors reads `SystemProcessors.Read()`.
- Edge 155's launcher hands the browser to a process of its own and exits with 0. The end-to-end driver
  accepts that, and closes the browser over the browser's own DevTools endpoint.
- The golden answers are
  re-recorded on purpose with `ROOM_GOLDEN_UPDATE=1`, and the diff is read before it is committed.
