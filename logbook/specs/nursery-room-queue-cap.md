# The nursery room's cap: the machine's queue and the heat check

The build brief for the room's load cap, as the owner ruled on 2026-10-11. The room stops reading
`% Processor Utility`, which counts a boosting core as more than one and so refused even the room's own
one-thread STEP. It asks two other things instead: which cores the machine's queue gives it, and
whether the machine is too hot. This follows the owner's rule of 2026-10-09 that load is no bar, and
that heat and a little headroom are. The room's build spec is `logbook/specs/nursery-room-build.md`.
Its rule holds here: a screen change works only when the end-to-end flows pass.

## What the room reads

**The queue.** The file is `scratch/machine/queue.md`, in the main checkout, found as the registry is.
The coordinator owns it. The room reads only its `## Running` section, from that heading to the next
`## `. Each line there that starts with `- ` is one job. A job's tags follow its text, each after ` · `:

- `cores: a-b[,c-d]` names the logical processors the job holds, such as `cores: 16-31` or
  `cores: 0-3,8-11`. A single processor is written `a`.
- `card` says the job uses the graphics card. The room never uses the card; it shows the tag and does
  nothing else with it.
- `room` marks the room's own slot. The coordinator sets it, and moves the slot by changing that line's
  `cores:`.

A job line with no `cores:` tag, or with one the room cannot read, claims every processor. The room says
which line did it. Prose between the job lines is not read.

**The heat.** `scripts/temp-check.ps1`, run with `pwsh`, prints one line and exits 0 for OK, 1 for WARN,
2 for HOT and 3 when it cannot read. A reading is kept for 60 seconds.

## The rule

A launch of `n` threads, and an episode step of one thread, is refused when any of these holds:

1. The queue cannot be read, or its `## Running` section is missing.
2. No Running line carries `room`. The room has no slot, so it starts nothing until the coordinator
   gives it one.
3. The heat check reads HOT, or cannot read.
4. `n`, plus the threads the room has granted that still count (`GrantedThreads`, as now), is more
   than the free cores.

The free cores are the `room` lines' cores, inside room.json's `affinity`, less every core another
Running line claims, less logical 4 and 5, which the room never uses. A launch is pinned to the free cores,
not to room.json's `affinity`. Its result and its activity row record the mask that was applied. WARN does
not refuse, but the result says it.

room.json's `affinity` becomes the bound the room never leaves, `0xFFFFFFCF` by default: every
processor but 4 and 5. `capLogical` is removed, since the slot replaces it. Two keys are added:
`queueFile` (`scratch/machine/queue.md`) and `heatScript` (`scripts/temp-check.ps1`). Every refusal
names what refused it, in the room's existing manner: the line, by its text before the first ` · `,
and the cores it claims; the heat verdict and its line; the free cores against the threads asked.

## What changes besides

- `/api/room` and the screens show the slot, the free cores, the lines that claim cores inside the
  slot, and the heat verdict, in place of the load percentage. The tank screen's STEP still says its
  refusal before it is pressed.
- `ILoadReader`, `TypeperfLoadReader` and the utility arithmetic in `Cap` go. A queue reader and a heat
  reader take their place, each an interface the tests give a value, as `ILoadReader` was.
- The tests: the queue's parse (tags, ranges, a line without tags, a bad tag, prose between lines, a
  missing section), each refusal's words, and the mask a launch is pinned to. The golden answers are
  re-recorded on purpose with `ROOM_GOLDEN_UPDATE=1`, and the diff is read before it is committed.
