# The nursery room: design brief

Written 2026-10-02 by session evolution-simulator-a8 for the design session working in this folder.
The owner asked for the nursery to become a full-fledged application that runs headful in
Unity, and for this session to brief the design and iterate on it. This is a design pass only.
What gets built is the owner's ruling, made with the design in hand.

## What the nursery is

The nursery is the program that evolves creatures' brains on fixed bodies. A population of genomes
(each a body plan plus a brain) swims short episodes in a small tank of water, a few bodies to a tank.
Each body is scored on the energy it holds at the end less what it started with. The best breed the
next generation, and a run lasts tens to a hundred generations, five to eight minutes each. After the
last generation comes the exam. Each body's best brain swims 32 fresh episodes, and so do copies of
it that are frozen, blind, held at a constant stroke, or missing one sense (a knockout). The exam asks
whether the brain uses what it senses.

The general nursery (D148, being built now) makes every experiment a settings file called a recipe.
Each run writes a record of how it was made (`run.json`) and its wall times (`timing.jsonl`). Each
registered experiment is one line in a registry. The build spec is
`logbook/specs/nursery-general-build.md`. Read it first.

## What the application is

The nursery room is a desktop application for running and watching nursery experiments. It is a
local web application: a small .NET 8 server on this machine, opened in a browser, with its 3D view
drawn in WebGL. The owner first left the platform open, suggesting Unity if that is the easiest
integration but saying anything goes, asked whether a web app can record 4K video and export PNGs, and
then ruled on 2026-10-02 to go with web, and to do a good creature render in
Blender if one is wanted. The reasons for web:
- **The same runtime as the nursery.** The server can step one episode itself on the nursery's own
  code, and get the run's numbers bit for bit. Unity's runtime rounds floating point differently, so
  anything it stepped would be a cousin of the run.
- **Nothing to install.** The .NET SDK the project already uses ships the web server.
- **Your output is the target.** A design made as HTML and CSS is close to what gets built, where
  Unity's UI Toolkit would need it translated.
- **Charts are easy on the web**, and the run screen is mostly charts and tables.

The cost is looks. A plain WebGL view draws creatures less well than a film renderer, so a fine
picture of a creature is made in Blender, from an episode the room exports. Every picture the room
makes itself (PNG at any size, 4K video rendered frame by frame) is for reading, not for the
channel.

**Agents run the experiments; the owner watches.** The owner ruled on 2026-10-02 that the
app must have an MCP that an agent can communicate with, because the agent will manage the
experiments and the owner will not do click-ops. So every action the room offers (read the registry and a run, write
and check a recipe, launch, stop, compare two runs, take a picture or a film) is one operation of the
server. The browser and an MCP server (the protocol agents use to call tools) both call the same
operations, so an agent and the owner always see the same state. The design follows from that:
- the screens are for watching and judging first, and the owner's controls are few;
- every action shows who took it, an agent session or the owner, and when, with the agent's stated
  reason where it gave one: an activity feed on the registry and on each run;
- a picture or a film an agent asked for appears where the owner can see it, beside the run it
  shows.

**Every action by hand is an action an agent can take.** The owner ruled on 2026-10-02 that any action taken by
hand on the screen must be allowed to be taken by the agent through the MCP, and that this requires a proper
design that does not create an endless number of tools in a single MCP. The shape below was
recommended, and the owner accepted it the same day:
- **One list of operations on the server.** Each operation has an id (`run.launch`, `run.stop`,
  `recipe.check`, `media.picture`, ...), a JSON schema for its arguments, and a required `reason`
  that goes to the activity feed. A button on a screen does nothing but call an operation by id. A
  test fails the build when any control on a screen names an operation the list lacks, so parity
  holds by construction and not by memory.
- **Reads are MCP resources, not tools.** The registry, a run, a recipe, a genome, an episode and the
  activity feed each have an address (`nursery://run/8`, `nursery://recipe/brain2`). Reading costs
  no tool at all, however many kinds of thing there are.
- **Actions are a handful of tools, one per family**, each taking an `action` and that action's
  arguments: `recipe` (write, check, diff), `run` (launch, stop, rerun), `compare`, `media` (picture,
  film, export). A new operation joins its family and adds no tool. A family that grows past about
  six actions is split, and one with a single action stays a tool of its own.
- **Discovery.** A `describe` call returns the operations with their schemas, whole or by family, so
  an agent learns the full set without a tool per operation.

The application is the window and the controls, never the engine. The evolution runs in
the headless nursery program, a separate process. The room launches it, reads what it writes and
shows it.

Five screens, in the order a person uses them.

1. **The registry.** Every experiment: its name, its state (running, completed, killed, failed), when
   it ran, generations done against planned, the best score, and whether a rerun reproduced it. New
   experiments start here.
2. **The recipe.** A form over a recipe's sections: the world config and its overrides, the bodies,
   the episode's setup (length, tank, snow, carrion, start height, purse), the search (population,
   draws, elites, tournament, generations, crossover), the reward (a named class and its parameters)
   and the exam's controls. A recipe may build on a base recipe, so the screen shows what differs from
   the base. Validation errors sit beside their field. A recipe that will be written up must be
   committed before it runs (a pre-registration), so the screen shows whether it is committed. Launch
   takes a thread count and shows the machine's load against its cap.
3. **The run.** Is it alive and on pace; is the search improving (mean and best per generation, with
   a ten-generation running mean); how the energy is made (path, joint work, food at the body,
   survival); which body leads; what the brains are growing into (hidden units, links); whether
   breeding is healthy; the exam table per body (own brain against each control, paired differences
   with standard errors, marked where they clear two); the log's tail. A second run can be laid over
   it as the control, with a diff of the two recipes.
4. **The tank.** A 3D view of one episode: the tank, the snow and its islands, the carrion, the
   bodies. A timeline to scrub. Per body, its energy, its stroke, and the brain's activity live:
   senses in, hidden units, power and bend out. Pick a genome and an episode from a run or its exam,
   and set two side by side (own brain against a knockout). The screen says whether what it shows is
   the run's own episode or a cousin of it. A button exports the episode for a Blender render.
5. **The genome.** A body plan beside its brain drawn as a graph: ports grouped by part and sense,
   hidden units, links by weight and sign. What changed from its parent.

The dashboard proposal (`proposals/propose-nursery-dashboard.md`) worked out screen 3 in detail,
panel by panel, with the question each answers and the data behind it. Use it for the run screen. It
was paused because the owner is rethinking the dashboard as this application.

## Nothing about today's experiment is built in

The owner ruled on 2026-10-02 that care must be taken not to make things specific. Scent is an input now, they said, but next
week some other input may matter, and there may be different outputs and different reward
functions.

Every screen draws from what the run says it has. No name of a sense, an output, a control, a reward
or a body part is written into a screen.
- **Senses and outputs.** The brain panels, the knockouts and the tank's live view list whatever
  inputs and outputs the genome's brain has, read from the genome, in its order. A body with three
  joints shows three output groups. A new sense appears without a design change.
- **Exam controls.** The exam table's columns are the recipe's `exam.controls`, in the recipe's order,
  labelled as the run labels them. Which control the tank's right side shows by default is a choice
  on screen, not "the scent knockout". A rule like "grey out frozen and brainless when the brain has
  no oscillator" is driven by what the genome holds, and its reason is shown.
- **The reward.** Charts plot the reward's value and its named parts as `tank-scores.jsonl` gives
  them, with the reward class and its parameters shown. Net joules is one reward among many, not the
  axis of the screen.
- **Bodies.** Any number of bodies, of any shape. The tank draws parts from the genome, not as a box
  and a capsule.
- **Settings.** The recipe screen is built from the recipe's structure, so a new section or a new
  override shows without a design change.

A mock may use today's names as its invented example, but every panel must say where its rows
come from, so the build makes them data and not text.

## Visual language

The same family as the theatre's interface: read `design/SPEC.md`, `design/canvas/README.md`,
`design/canvas/theatre.uss` and `design/canvas/Theatre UI.dc.html`. Take its palette, type and
spacing, so the two programs read as one project. Desktop first: 1920 by 1080, checked at 2560 by
1440. The owner chose direction A (dark ocean), toned down: dark water, light only on what matters,
every text at least 4.5:1 against what is behind it.

## The data

Learn the shapes and the ranges from real files, read locally:
- a finished search: `scratch/nursery/pilot-deep-brain2/` (its `tank-generations.jsonl`,
  `tank-best.jsonl`, `tank-crossovers.jsonl`, `nursery.log`; its exam, `tank-exam.jsonl`, lands
  about 14:40 on 2026-10-02);
- a run record: `scratch/nursery-general/record-smoke2/run.json`;
- the recipe's sections: the spec's section 2 and 3. The recipe's file format lands with step 2 of
  the build, and this session will send it.

Every number shown in a design that leaves this machine is invented, in the real ranges, unless the
owner approves real ones. Reading the real files here to learn their shapes is fine.

## How we work

- Write only in `design/nursery/`. Commit only your own files there, by path. Never `git add -A`: the
  main checkout holds other sessions' uncommitted files.
- First pass: wireframes of all five screens, with no polish, and a `HANDOFF.md` here saying which
  files are current. Then message evolution-simulator-a8 and wait for its notes before polishing.
- Say where a screen needs data the nursery does not write yet. That list is as useful as the
  pictures, because the build can still add it.
