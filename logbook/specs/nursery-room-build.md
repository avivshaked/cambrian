# The nursery room: the build

Started 2026-10-02. The nursery room is a local web application for watching and running the
nursery's experiments. Its design is in `design/nursery/`: the brief (`BRIEF.md`), the five screens
of pass 2 (`556c550`), and the data the nursery does not yet write (`HANDOFF.md` there). This file
says how it is built, in what order, and where the build stands.

The owner's rulings, all of 2026-10-02, are quoted in the brief:
- web, with Blender for fine creature renders
- an MCP through which agents run the experiments
- every action by hand also open to an agent, through a few tools
- nothing about today's experiment built in
- the dark-ocean look, with its contrast measured

The owner started the build the same day, before the nursery's step 5, asking whether there was a reason not
to start already, and whether the room had to be explicitly read only at all.

Two later rulings, 2026-10-02 evening, each an answer of "yes" to a question put in full:
- At 1366×768 the run screen scrolls as one column, on small laptops only, rather than switching to
  tabs.
- Launch as trial is allowed: a recipe outside `rounds/nursery/` runs unregistered, and the room
  launches it, badged as a trial. The activity log records it like any launch.

## Who and where

The session evolution-simulator-a8 builds it, with Sonnet subagents writing code and Opus reviewing
each phase. The coordinating session (evolution-simulator-6e) sequences it against the machine, and
the merge goes to it first. The code is on branch `nursery-room`, made from `nursery-general`, in the
worktree `scratch/wt-nursery-room`.

## The shape

**One process, three doors.** An ASP.NET Core 8 server (the framework ships with Unity's .NET SDK,
so nothing is installed) serves three things:
- the screens, as static HTML, CSS and script
- a JSON API for the screens
- an MCP endpoint for agents

It listens on 127.0.0.1 only. Nothing it does leaves the machine.

**The operation list is the application.** Every action is an operation in one list on the server.
Each has an id (`recipe.check`, `run.launch`), a family, a JSON schema for its arguments, and a
handler. An operation that changes anything also requires a `reason`. A button on a screen only
calls an operation by id. A test fails the build when a control on a screen names an id the list
lacks.

**Reads are resources.** The registry, a run, a recipe, a genome and the activity feed each have an
address (`nursery://registry`, `nursery://run/{entry}`, `nursery://recipe/{path}`). The screens read
them through the API, and agents read them as MCP resources. Reading adds no tool.

**A few tools.** The MCP has one tool per family, each taking an `action`:
- `recipe`: write, check, diff
- `run`: launch, stop, rerun
- `compare`
- `media`: picture, film, export

It also has `describe`, which returns the operations and their schemas. A family that grows past
about six actions is split.

**The nursery is the engine, never the server.** The server reads what the nursery writes. It runs
the nursery as a separate process to launch a run, and calls the nursery's own classes (`Recipe`,
`RegistryEntry`, `RunRecord`, `RewardRegistry`, by project reference) to check a recipe and read the
registry. A setting's meaning is never written twice.

**Reading live files.** Every file is opened with `FileShare.ReadWrite`, because a running nursery
holds its files for writing. Reads take only the bytes added since the last poll, every 30 s by
default.

**The machine's cap is the queue and the heat, which the server reads.** The room starts work only on
the free cores of its own slot in the machine's queue, and never when the heat check reads HOT, as the
owner ruled on 2026-10-11. Its brief is `logbook/specs/nursery-room-queue-cap.md`. A launch that does
not fit is refused, and the refusal names the line or the reading that refused it. Re-stepping an
episode for the tank screen counts against the free cores, and never starts while a timing read is
declared.

**What the cap's reading counts.** The server turns the counter into processors in use as
`ceil(Utility × 32 / 100)` (`Cap.InUse`). Utility scales each core's busy time by its clock
against the base clock, so a boosting core counts for more than one. On 2026-10-05 a Farm suite on
8 efficiency cores read 61.8% Utility, and then 50.6%. The server counted those as 20 and 17 of 32,
while Processor Time read 31.9%. A one-thread STEP beside the suite was refused. That follows the
owner's rule, which reads the cap as Utility, so it is not a fault in the server. It does mean that
one 8-thread job blocks every launch and every STEP the room makes. Whether the room should count
otherwise is the owner's ruling, since the measure is. These are the choices to put to them:
- **Keep it.** The room refuses whenever the owner's measure says the machine is at half.
- **Count the coordinator's grants.** The room reads the jobs running in `scratch/machine/queue.md`,
  with their threads, and refuses only past 16 of those plus its own. The counter stays as a
  second check against a job nobody listed.
- **Count by `% Processor Time` for the room's own small jobs.** A STEP of one thread is checked
  against threads, a launch against Utility.

**The MCP library.** Microsoft and Anthropic's C# SDK for MCP (`ModelContextProtocol.AspNetCore`,
from NuGet) is pinned by version, and the version is recorded here. If it does not restore, or does
not serve resources and tools over HTTP, the endpoint is written by hand. It is a small JSON-RPC
surface: initialize, tools/list, tools/call, resources/list, resources/templates/list and
resources/read.

Connecting the endpoint to an agent session is a change to Claude Code's settings, so the owner does
it, or asks for it in their own words.

**The activity log** is `rounds/nursery/activity.jsonl` in the main checkout, beside the registry
(the coordinating session's ruling, 2026-10-02: who launched a run and why is part of the record).
Each operation that changes anything appends one row giving the time, the operation, its arguments,
the reason, which door it came through and the outcome. Every path in a row is relative to the
repository. A reason holding a line break, or longer than 500 characters, is refused.

**The append-only files are listed once,** in `rounds/nursery/appended.txt`. Every run or
operation appends to the registry and the activity log, so neither file makes a tree dirty. The
nursery's dirty check and `scripts/farm-queue.ps1`'s clean-tree check both build their exclusions
from that list. A third such file is then one line in one place.

**They are committed** by path, with no other file, under the message "nursery: registry and
activity". Two kinds of session commit them: the session that writes a logbook entry citing a
nursery run, as part of writing the entry, and any session that ends a run of its own, at its next
commit. The files are therefore never left modified past the entry that cites them. Both are
append-only, so a commit never needs a merge of their contents.

## The phases

| Phase | What | Done when |
|---|---|---|
| 1 | The server, the operation list, `describe`, resources. The registry, run and recipe screens on real files, read-only but for `recipe.check`, which has no side effect | The run screen shows `pilot-deep-brain2` live from its files; an MCP client lists the resources and calls `recipe check` on `brain2.json`; the parity test passes |
| 2 | Writes: `recipe.write`, `run.launch`, `run.stop`, `run.rerun`, `compare`. The activity log. The cap. A clean stop in the nursery (a stop file read between generations, so `run.json` says stopped) | An agent writes, checks and launches a 1-generation trial recipe through the MCP, stops it, and every step shows in the activity feed with its reason |
| 3 | The tank: re-stepping an episode on the server with the nursery's own code, the follow camera, the map from above. The genome screen. Pictures and films, and their store. The Blender export | The tank screen re-steps an exam episode of `pilot-deep-brain2` and marks it exact |

Phase 3 needs data the nursery does not write yet: the tank-mates of each draw, genome ids and their
parents, and the exam's plan in `run.json`. Those changes land in the nursery after its step 6, so the
two builds never edit the same files at once.

## Rules for the build

- **Nothing about today's experiment is built in** (the brief's section of that name). Sensors,
  outputs, exam controls, reward parts and body parts are rows from the data.
- **The screens start from pass 2's HTML and CSS.** Charts are drawn as SVG by the page's own script.
  The tank's 3D view uses three.js, kept in the repository at a pinned version with its licence (MIT)
  beside it, never loaded from the internet. three.js is not needed before phase 3.
- **Every text at least 4.5:1** against what is behind it, checked with `design/nursery/contrast.py`
  on the built pages as on the mockups.
- **While the coordinating session's run holds the machine,** one build or test runs at a time across
  every branch of this session, at `-m:1`. Nothing swims.
- **The coordinating session's old build** (`scratch/wt-joint/artifacts/nursery-v10`) is left
  untouched until it says otherwise.
- **The design serves the app, not the other way around** (the owner ruled on the night of 2026-10-02
  that the app need not carry everything in the design, and that what gets built is what the app
  needs, not whatever the design happened to include).
  - A part of a mock is built only if it serves the app's job: agents run the experiments
    through the MCP, and the owner watches and judges. In practice that means one of three things:
    - it shows what a run is doing (alive, on pace, when it ends)
    - it lets a result be judged truly (against a control, with its error)
    - it prevents a wrong reading (a greyed uninformative control)
  - Hotkeys, alternative views of the same number, and decoration are not built because a mock
    has them.
  - Each round's brief says which mock parts it builds and why they are needed.
- **A screen is operated in a browser before anyone calls it working.** On 2026-10-02 the owner
  opened the room, after four phases had passed their unit tests, contrast checks and screenshot
  comparisons, and found it did not work, and asked whether anyone had tried to operate it. An agent driving a headless Edge then found 21 faults. The worst was that the
  RECIPE tab showed an empty page, because `/api/resources` serialised every item as `{}`. No test
  read that response through the real pipeline, and no screenshot was taken of that path.
  - Every phase ends with a walk: the paths a person takes, every control clicked, every console
    error and failed request read, at 1920×1080 and 1366×768.
  - The walk is done by driving Edge over its DevTools protocol from PowerShell, with .NET's own
    websocket client, so nothing is installed. The driver is
    `scripts/cdp-lib.ps1`, to be moved under `scripts/` once reviewed.
  - What the walk found is reported with the phase.
- **A rule a screen applies to the science is checked against the nursery's code before it ships.**
  The rule decides what a reader takes a number to mean: what is greyed, what counts, what is
  compared. On 2026-10-04 a builder greyed an exam's frozen and brainless columns for any genome
  without an oscillator neuron. Its reason was that those controls take the senses away. They do
  not: frozen replaces the drive with a constant, and brainless reads every sense as 0, so both
  measured something real, and the grey hid it. The walk passed it, because a walk checks that a
  screen does what it claims, not whether the claim is true.
  - The reviewer reads the nursery's own code for the controls or measures named.
  - The rule's count or test comes from the nursery's code itself where it can, as `KnockoutReach`
    does for a knockout, so the room cannot disagree with the exam.
- **A change to a screen is not called working until the end-to-end flows pass**
  (`src/Evosim.NurseryRoom.Tests/E2e/`, `Category=E2e`, run with the suite). They drive headless
  Edge over its DevTools protocol through every screen at 1920×1080 and 1366×768, and measure the
  layout rather than compare pictures.
  - An agent's walk in a browser is still needed for a new screen or a new panel, and for anything
    judged by eye.
  - Every walk ends by adding its new checks to the flows, so the same fault is never paid for twice.
- **A change to what a resource answers is made on purpose.** The saved answers in
  `src/Evosim.NurseryRoom.Tests/Golden/answers/` hold one answer each for `nursery://registry`, a run,
  a recipe, an exam comparison and a refused read, with the fields that change from run to run
  masked by path. A field renamed, added or dropped fails the test. The answer is then recorded
  again with `ROOM_GOLDEN_UPDATE=1`, and the commit that changes it says why, since an agent reading
  the room through its MCP breaks on such a change without any screen showing it.

## What was built

Filled in as each phase lands.

**Phase 1, 2026-10-02** (`8cc52cd` on `nursery-room`, rebased onto `nursery-general`, not yet merged). The server, the operation
list (`recipe.check`, `describe`), the MCP at `/mcp` and the registry, run and recipe screens, reading
`pilot-deep-brain2` live from its files. 92 tests.

- **The MCP library** is `ModelContextProtocol.AspNetCore`, pinned at `[2.2.0]`, over stateless HTTP.
  Stateless means no server-sent pushes, so phase 2's activity feed cannot be pushed to an agent; it
  is read as a resource.
- **Guards.** The server listens on loopback by construction and refuses to run otherwise. It
  accepts only the hosts `127.0.0.1`, `localhost` and `[::1]`, refuses a foreign `Origin` on every
  path (403) and an operation posted without `application/json` (415). A page on another site
  cannot drive it, and DNS rebinding cannot read it. The nursery refuses a recipe path that leaves
  the repository (`2706a21` on `nursery-general-s6`).
- **What the exam pairs on.** A Net run, or a run with no recipe, is paired draw by draw on `per`
  (net per draw) with a standard error. Any other reward is the difference of `score`, with no
  standard error and no tint, until the nursery writes a per-draw score. That is phase 3's data
  change. The charts default to the reward's value, never to net joules.
- **Contrast** is measured on the built screens with `design/nursery/contrast.py --base
  http://127.0.0.1:5179/` (`81b8070`): no text below 4.5:1 at 1366×768, 1920×1080 or 2560×1440.
- **Starting it.** Build `src/Evosim.NurseryRoom` with Unity's `dotnet.exe`, then run
  `dotnet.exe artifacts\Evosim.NurseryRoom\bin\Debug\net8.0\Evosim.NurseryRoom.dll --root <tree>`
  with Start-Process. The apphost `.exe` does not start, because no ASP.NET runtime is installed
  system-wide. Its settings are `rounds/nursery/room.json`.
**Phase 2, 2026-10-02** (`61e6523` on `nursery-room`; its acceptance recipes `b8cf15c`). The
writes, the clean stop, the cap and the activity log, after two Opus reviews; 297 room tests and
138 nursery tests. The acceptance passed through the MCP: a recipe was written, checked, launched
and stopped after generation 0, ending as stopped and unregistered, with each step in the activity
log. One mishap: the first acceptance recipe, `accept-1gen`, finished in seconds before a stop could
reach it, and is registry entry 2, not rerunnable. The line is kept as the honest record (the
coordinator's ruling). Until phase 3a, the room did not pin a launch to the efficiency cores, and
launched nothing longer than a smoke.

**Phase 3a and 3b, and launch as trial, 2026-10-02** (`6cc0c3f` on `nursery-room`), after two Opus
reviews; 401 room tests and 172 nursery tests.

- **Pinned launches (3a).** `room.json`'s `affinity` (default `0xFFFF0000`) names the logical
  processors a run may use. The launcher creates the process suspended, sets the mask, then resumes
  it, so the `cmd` and the `dotnet` under it never run unpinned. The mask is checked against the
  system's mask, read once with `GetProcessAffinityMask`. The cap's count of logical processors is
  that mask's population count, so a room started from a pinned shell still counts 32. A launch
  asking for more threads than the mask allows is refused. The result and activity row record the
  mask the launcher applied.
- **What the nursery writes for the tank and genome screens (3b).** New files only:
  - `tank-draws.jsonl`: each draw's seed and its tanks, as member ids `g<generation>.<index>`
  - `tank-exam-draws.jsonl` (a bench writes `tank-bench-draws.jsonl`): the same per exam brain,
    with its index as `b`
  - `tank-lineage.jsonl`: each member's id, body, birth, parents (base first; a crossover of a
    member with itself names it twice) and whether it is an elite

  The exam's plan also goes in `run.json`, with its salt as a string. All three files are results.
  Both reviews traced every random draw and found none added or lost. That no old result file moved
  a byte is checked by digest against step 5's run once the machine is free.
- **Launch as trial.** A recipe outside `rounds/nursery/` launches unregistered and is marked a
  trial in the result, the activity row (`trial: true`), the catalogue and the screens' badge.
  `recipe.write` writes only directly under `rounds/nursery/` or under `trialsDir` (default
  `scratch/nursery-trials`, which must lie under `scratch/`, outside any worktree). A rerun is of a
  registered entry only. The recipe screen offers LAUNCH, LAUNCH AS TRIAL or neither, from the same
  rule the launch applies.
- **Found by the second review, and older than this phase.** The guard that refuses to replace a
  recipe a running run is using read `run.json`'s recipe path. The nursery writes that path
  absolute, and the guard took only relative ones, so it lapsed once a run had written `run.json`.
  It now reads both, and refuses when it cannot place the path. The room also refuses a link (a
  reparse point) anywhere between the root and a file it writes or launches.

**Phase 3c, the genome screen, 2026-10-02** (`1738587` on `nursery-room`), after an Opus review of
the server side and a check of the screen against pass 2's mock; 465 room tests.

- **The genome resource** is `nursery://genome/{run}/{id}`, with a run's genomes listed at
  `nursery://genomes/{run}`. The server computes it with Core's own code:
  - the body as `Developer` grows it
  - the brain's ports in Core's order
  - the genome's graph of nodes and edges
  - lineage from `tank-lineage.jsonl`
  - the changes from the base parent, when the run holds that parent's genome

  A run holds each generation's top genome (`tank-best.jsonl`) and the exam's. `tank-best.jsonl`
  names no member, so a top genome takes its member id only where `tank-scores.jsonl` and the
  generation's `top` row agree. Otherwise it is `best.g<generation>`.
- **Under which config.** Development and the sensor pool depend on the run's config. That config
  comes from `run.json`, or, for a run made before `run.json` existed, from a `config` named on its
  entry in `room.json`'s `runs` list. Otherwise the screen says the config is unknown. The
  `pilot-deep-brain2` entry names `scratch/nursery/runs-jd/big-cur-deep-brain2/config.json`. Under
  it, none of the 6,451 links in that run's 107 genomes names a port that does not exist. Under
  Core's defaults, 4,462 did, which is how the first build showed it.
- **The screen** follows the mock's layout:
  - left: the body from above and from the side, at one shared scale, with the genome graph below
  - centre: the brain, with all / changed / |w| chips
  - right: lineage and the actions
  - a fourth column for the changes on screens 2:1 or wider

  "Export genome JSON" is a plain read. Swim, compare and the Blender export are drawn disabled
  until their phase. No text is below 4.5:1 (`contrast.py`, 1920×1080). The mock's base-parent
  score line and its donor fields are not built: the nursery writes no data for them.

- **Left for phase 2:** the base diff is computed twice (server and screen). The registry shows a
  launched run only once it ends, since the nursery appends its line then. The machine's load and
  temperature are not read yet.

**A recipe whose world this build cannot load, 2026-10-05** (`c59584f` on `readload`), designed in
`nursery-room-unloadable-recipes.md` and approved by the coordinator on 2026-10-04; built by a Sonnet
subagent and reviewed by Opus.

The round-56 build added three tunables. The config the old brain2 six name,
`configs/7319d6b528cc77f1.json`, lacks them, and loading refuses a config that lacks a tunable. So
on that build the room could not show the runs made from those recipes. Their `-w2` namesakes name a
migrated config and run on it.

- **Read shows, Load runs.** `Recipe.Read` parses, merges the base chain and checks every field,
  without opening the world. `Recipe.Load` is `Read` followed by the world's check, unchanged.
  `Read`'s hash and canonical text equal `Load`'s, because the canonical text names the config's
  path and hash and never its contents, so an entry's hash check means what it meant.
  `WorldProblem()` gives the world's refusal, or null.
- **Where the room reads.** The exam plan's reconstruction, the columns' controls, the recipe view
  and its base, and the recipe diff use `Read`. The nursery program, `recipe.check`, launches and
  writes keep `Load`. The recipe view adds `world` (`loaded`, `problem`, `config`). Where the world
  does not load, the screen shows a WORLD NOT LOADED plate whose title names the config and the
  refusal, and LAUNCH and EDIT BY HAND are refused with that reason.
- **Refused first.** A tank episode of a run whose world this build refuses is refused before any
  folder is made or moved, naming the commit the run was made on, from `run.json` or its registry
  entry. A rerun compares the hash with `Read`. On another build, the build check refuses it and names
  the entry's commit. On the entry's own build, an entry whose world that build cannot load is
  refused by the room at once rather than by the nursery later, which the coordinator accepted. A
  rerun still means the same: bit for bit on the entry's build only.
- **Core refuses with a `FormatException`** ("Missing required field …"). A recipe's own fields
  get an `InvalidDataException`, and a catch that lists only that one lets Core's through. So
  `WorldProblem()` catches any exception, and the label test that loads every recipe passes over
  the missing-field refusal alone.
- **Found in review, and missed by the tests.** `recipe.js` read `worldNotLoaded` one line before
  declaring it, so every draw of the recipe screen would have thrown. `node --check` passes such a
  file. A screen is therefore looked at in a browser before it ships.

**The room's tests, 2026-10-05** (`nursery-room-tests-plan.md`, steps 2 to 5; step 3 is `243567b`,
merged into main as `b751dc5`). Both builds were by Sonnet subagents, each reviewed by Opus.

- **Walk 7** passed every item it was given, and found no console error and no failed request on any
  screen at either size. Five of its six small faults were fixed in step 3:
  - the LAUNCH note for a git state the room could not read;
  - "1 threads";
  - the RERUN dialogs' thread line;
  - a "—" cell drawn greyed;
  - the RECIPE tab not following the registry's selection.

  The tank screen scrolling at 1366×768 was accepted as by design. A short look in Edge then
  confirmed the fixes on main's code.
- **Six end-to-end flows** take about 35 s. Each failed once on purpose against faults put in by
  hand. `Program.Create` takes an optional git runner, so the flows can show git states real git
  cannot make. The flows carry walk 7's checks except those that need the real runs, and
  `design/nursery/briefs/room7-walk7-checks.md` names the ones they leave out.
- **Every reason box** says that the reason is kept in the activity log, which is committed to git
  and may be published. The owner ruled on 2026-10-05 that the log is committed whole.
- **Five saved answers** and their masks are in `Golden/`. Renaming one field in each resource failed
  its test with the field's path. The recipe answer follows the newest migration of brain2,
  `brain2-w3.json` since round 57's build. A landed recipe is never edited, so the answer changes
  only when a build's new tunables bring a new namesake, and it is then recorded again on purpose.
