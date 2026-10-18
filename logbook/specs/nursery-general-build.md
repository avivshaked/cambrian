# The general nursery: the build

Started 2026-10-02, after the owner ruled D148 and the baseline. This file carries the design of
`proposals/propose-nursery-general.md`, which it replaces, and says what is built, where, and in what
order. It is updated as the build moves. The rulings and the owner's words are in D148. The first
draft, written by a review subagent on 2026-10-01, is in git history at `b15515f`, and the final
proposal at `3b603e9`.

The nursery is the program that evolves brains on fixed bodies in short episodes, scored in the
world's own currency of joules. Today a new experiment in it means new code. After this build an
experiment is a settings file, called a recipe, and the code changes only when a recipe needs
something no setting can say. The owner's aim is for the nursery to take as much as
possible from the simulation world and to abstract its settings as far as possible, so that we
can experiment without massive code changes to the nursery each time, only evolving it, with a
simple experiment registry that we can persist and rerun on demand.
On the spirit of it, the owner said this is not academic research but a curiosity
project, so we can take some shortcuts.

## Where the build is

The code is on branch `nursery-general`, made from the branch that carries brain v2 and the
slow-tank report (`joint-drive` at `dc5dd6f`). Its worktree is `scratch/wt-nursery-general`, since
the shorter name was already taken by an older branch. Paths below are under the nursery's source
folder unless another folder is named. Line numbers were read on 2026-10-01 and may have moved a
little.

The work is shared between two sessions. The coordinating session holds HANDOFF item 10a, the brain
v2 run and its exam, and the sensing cache in Core and Dynamics. The building session holds the
branch, this file and HANDOFF item 11b. A world rule, the reward of a planned arm, the next arms and
scope stay the owner's.

| Step | What | State |
|---|---|---|
| 1 | The run record | Built and reviewed (below) |
| 2 | The recipe, and the episode's setup as settings | Built and reviewed (below). The purse as a Core method is deferred |
| 3 | The reward interface, with `Net`, `WeightedSum` and `EpisodeRecord` | Built and reviewed (below) |
| 4 | The registry, with rerun | Built and reviewed (below) |
| 5 | The baseline recipe and the check | Passed 2026-10-02: every row of generations 0 to 3 identical to `pilot-deep-brain2` in the three files (below). The reference entry is next |
| 6 | Deleting the old code | Built and reviewed, `b6d8511` and `2706a21` on `nursery-general-s6`. Its rerun of the check must be digest-identical to step 5's run |

## What was ruled

The owner answered the proposal's questions on the morning of 2026-10-02 (D148).

| Question | The owner's answer | What it means |
|---|---|---|
| Where the registry lives | Agreed | `rounds/nursery/` |
| Whether a recipe is a pre-registration | Yes | A recipe for a run that will be written up is committed before it runs |
| Retiring the archive mode and the swim test | Yes | Both are deleted, with their flags and their files |
| Reproducing the old experiments | The owner asked whether the old experiments could be rerun as an experiment config, and not live in the code for ever as hard-coded components that nothing uses, and said they were happy to discard them | Old experiments are discarded, not reproduced. A departure from the world is a setting, and code exists only for what a current recipe uses |
| The reward | Approved, with the suggestion that the reward be a class or module, whichever C# construct fits | A reward is a C# class behind one interface, registered by name |
| The brain v2 run as the crossover test | Yes, and the owner asked that we be forward looking | The build may start once `pilot-deep-brain2` has ended |
| The baseline | Agreed | Brain v2's setup is the baseline. `pilot-deep-brain2` stands as its run if the framework reproduces it |

## What it replaces

The nursery today is one program with five modes chosen by flags: the archive search, the tank
search, the tank bench, the swim test and a timing run. The modes read 63 flags into one options
object, and every flag has a default in code. An episode is a small world built from a world
`config.json` and stepped by the farm's simulation. Every run since 2026-09-30 has used the tank mode
(`Tank.cs`), and the tank's exam uses the bench's code.

This table shows what a new goal costs today.

| Thing | Where | To change it |
|---|---|---|
| Score: end holdings less start; a dead body ends at 0 | `Tank.cs:210-218`, read at `303-304` and `446` | Code, in three places |
| What an episode measures: food, light, work, upkeep, handling, exuded, path, food at the body | `Tank.cs:50-54`, filled `183-208` | Code: a field, a line in the loop and a log column each |
| The larder: snow, its density drawn from half to double, on layers or in islands | `Tank.cs:92-100` | Flags within snow; anything else is code |
| Carrion: 5 corpses of 40 J on the bed by default, which no script chose | `Tank.cs:159-177`, `Program.cs:55-56` | Code |
| The start: height drawn at 0.5 to 1.5 times a setting; the purse `world` or `grown` | `Tank.cs:108-149` | A flag between two purses |
| World overrides: floor off, the purse, the work cost | `Program.cs:168-180`, `206` | Code for any other tunable |
| What evolves: the brain (body kept), with the crossover | `Tank.cs:368-383`, `Crossover.cs` | Code |
| Selection: elites, a tournament, the running mean, per-body groups | `Tank.cs:295-361` | Flags for the sizes; another scheme is code |
| Exam controls: own, frozen, constants, kineses, knockouts, brainless | `Tank.cs:397-520`, `Program.cs:982-1100` | Flags for the listed kinds; a new kind is code |
| Run identity | absent | Nothing in the output folder says how the run was made |

## The design

### 1. The run record

Every run writes `run.json` into its folder before its first episode, and writes it again when it
ends. It holds:
- the recipe, resolved, with its hash (from step 2; until then, the arguments and every resolved
  option);
- the resolved world `config.json` with its hash;
- the commit, a dirty flag, and the tree hashes of Core, Dynamics, the farm and the nursery, from the
  farm's own code (`src/Evosim.Farm/Manifest.cs`);
- the seeds and salts, the thread count, the start time, and the process id with the process's own
  start time;
- at the end, how the run ended and when.

Wall time leaves the result rows. Today every generation row carries `seconds`, so no two runs write
the same file and no digest can compare them. It moves to a separate `timing.jsonl`, one row per
generation. The rows otherwise keep their fields and their order.

The tree hashes are taken at run time from the source above the working directory, as the farm takes
them. So they name the source beside the program, which may have moved since the program was built. The farm's
`CLAUDE.md` records how that bit round 49's film windows. A run is launched from the worktree whose
`artifacts/` holds its build, so the two agree as long as nobody edits the tree between the build and
the launch. `run.json` names the program's own folder so that a reader can check.

A run that is killed leaves `run.json` saying `running`. Nothing can write an ending for a process
that is stopped from outside. So the first write records the process id and the time the process
started. A reader takes `running` as killed when no process has that id, or when the process with that
id started at another time, since Windows reuses ids.

### 2. The recipe

A recipe is a JSON file holding one experiment. It lists every setting the run uses, with no
defaults, and the reader refuses a field that is missing or unknown, as the world's config reader
does. It names:
- the world config by path and hash, with overrides by tunable path (`"WorkCostMultiplier": 0.5`),
  applied through Core's tunable walk (`src/Evosim.Core/Config/ConfigSchema.cs`);
- the bodies: where they come from (fresh founders, a pool or a snapshot) and which are kept;
- the episode's setup (below);
- the search: population, draws, elites, tournament, generations, the per-body rule, the brain's
  variation and the crossover's rates;
- the reward, by class name and parameters;
- the exam: its episode count and its list of controls.

A recipe may name a base recipe and override parts of it. What is hashed, saved and registered is the
resolved recipe, never the pair. Threads stay out of the hash, because they set the pace and not the
result. The program takes a recipe and an output folder. The old flags go when the check in section 6
passes.

### 3. The episode's setup as settings

The nursery's departures from the world are what make it a nursery: births held, bodies fixed, short
episodes, a starting purse, a laid larder, paired draws. Each becomes a setting of the episode's
setup.

| Setting | What it holds |
|---|---|
| `seconds` | The episode's length |
| `tank` | Its radius and depth, and the bodies per tank |
| `snow` | Density, its draw (fixed, or half to double), layers, or islands with their wavelength and cover |
| `carrion` | How many corpses, their joules, laid on the bed at random |
| `start` | The height range over the bed |
| `purse` | `world` (the floor founder's rule) or `grown` with its seconds of standing cost |
| `birthsHeld` | True for every current recipe |

Code exists only for what a current recipe uses. The carrion ring of the archive mode goes with the
archive, and whatever no recipe sets is deleted rather than kept for later. Each setting draws in the
order the tank draws today: snow, then heights, then carrion (`Tank.cs:88-176`). That keeps the check
in section 6 passable.

The purse's `world` rule becomes a Core method that the world and the nursery both call, so the
nursery no longer copies the world's founder rule (`World.cs:4941-4968` against `Tank.cs:143-147`).
It moves no number, and the farm's digest confirms that.

### 4. The reward

A reward is a C# class that implements one interface. Its shape:

```csharp
public interface IReward
{
    // One genome's episodes in, one score out, with its named parts.
    Score Score(IReadOnlyList<EpisodeRecord> episodes);
}
```

- **`EpisodeRecord`** is the one input schema: every measure an episode makes, per body per episode.
  That is start and end holdings, net, food, light, work, upkeep, handling, exuded, path, food at the
  body, survived, the time of death, the seed and the arena. Fields are only ever added. A reward
  that reads a field a run does not record refuses to start. As built, the reader of a recorded row
  is strict: a row with a field missing is refused. When a field is added, the reader takes it as
  optional, with a stated default, for rows written before it.
- **The reward scores a genome over all its episodes.** How episodes combine (a mean, the worst
  case, a penalty on spread) belongs to the reward and is no longer fixed as a mean.
- **`Score`** is the one output: a number for selection, plus named parts that the generation rows
  and the exam record, so a dashboard can show why something scored well.
- **Registration.** A class carries an attribute with its name, the way `[Tunable]` marks a config
  setting. A recipe names the class and gives its parameters. The class declares its parameters,
  and an unknown or missing one is refused.
- **The first classes** are `Net` (today's score, the mean of net holdings over episodes) and
  `WeightedSum` (named measures with weights, summed in a fixed order in double precision).
- **Tests.** Each class scores a small recorded file of episode records to known values. A new
  reward is a new class with its own test.

There is no formula language in a recipe. A reward with a ratio, a condition or a threshold is a
class. The owner's ruling against energy per body size (`logbook/specs/nursery-plan-joint-drive.md`)
shows why. A ratio that looks harmless rewards the smallest body without limit, and a class carries
its test and its reason with it.

### 5. The registry

Each experiment is a committed recipe, `rounds/nursery/<name>.json`, written before its run and not
edited after it, by the rule in `rounds/README.md`. Each run appends one line to
`rounds/nursery/registry.jsonl`. The line names the recipe and the world config by their hashes. It
holds the commit and the tree hashes, the output folder, how the run ended, and a digest of each
result file.

The world configs and genome pools that recipes cite are committed beside them, in
`rounds/nursery/configs/` and `rounds/nursery/pools/`, each named by its hash. Today they sit under
`scratch/nursery/`, which the project's rule forbids for a file the record cites. Outputs go to
`runs/nursery/<name>/`, which is not committed.

To rerun an entry, build the commit it names in a worktree under `scratch/` and run the recipe into a
new folder. The program refuses if the build's tree hashes differ from the entry's. At the end it
compares its result digests with the entry's and prints "identical", or the first file and line that
differ. A run from a dirty tree is marked as such and cannot be rerun.

### 6. The baseline, and the one check

The baseline is brain v2's setup: the one brain (D146, D147), the drive sense (D144), the crossover,
the six bodies and the deep water. It is written as the first recipe, `rounds/nursery/brain2.json`,
with every setting `pilot-deep-brain2` used, the 5 corpses of 40 J included.

Before the old flag code is deleted, that recipe runs for three generations through the framework, on
the same seed. Its rows are compared with the first three generations of `pilot-deep-brain2`, which
ran on nursery-v10, built from `dc5dd6f`. The comparison leaves out the wall time, which has moved to
`timing.jsonl`.
- **Identical.** The framework runs the experiment as the old code did. `pilot-deep-brain2` is the
  baseline's run and is entered in the registry as a reference entry (the coordinator, 2026-10-02).
  The entry names its build (commit `dc5dd6f`, nursery-v10, `scratch/wt-joint/artifacts/nursery-v10`)
  and the check's verdict, written from the check's own output. It also lists why its digests cannot
  match a rerun's, none of them a fault:
  - its generation rows carry `seconds`
  - it has no `tank-scores.jsonl`
  - its exam rows have no `score` or `fresh`
  - its first knockout is labelled `knockout` where the recipe's is `knockout Chemical`

  `--rerun` refuses the entry and points at the check.
- **Different.** The first differing field is traced and reported to the coordinating session before
  anything is decided. If the cause is a fault worth fixing, it is fixed, the baseline is a new
  realisation, and it is run again in the framework, after the sensing cache.

This is the one shortcut not taken. It costs a quarter of an hour, and without it a fault in the
framework would read as a result.

The check reads the old run without writing to it. This table names its parts.

| What | Where |
|---|---|
| The old run's folder | `scratch/nursery/pilot-deep-brain2` |
| Its command line | `scratch/nursery/launch-brain2.ps1`, the run branch |
| Its world config | `scratch/nursery/runs-jd/big-cur-deep-brain2/config.json` |
| The files compared, generations 0 to 2 | `tank-generations.jsonl` with `seconds` removed from the old rows, `tank-best.jsonl`, `tank-crossovers.jsonl` |

## The faults the review found

The first draft's review found six faults in today's code. Each has a fate in this build.

| Fault | What happens | Its fate |
|---|---|---|
| 1. The default carrion | Every tank episode lays 5 corpses of 40 J that no script chose | Becomes the `carrion` setting. The baseline recipe states it |
| 2. Fixed snow skips a draw | With the density fixed, the heights and carrion draws shift | Each setting keeps today's draw order. Fixed snow keeps its draw and discards it, so a fixed and a drawn run differ only in the density. Moves no number in the baseline, whose snow is drawn |
| 3. Traces could crash a run | Parallel episodes appended to the log at once | Fixed in `dc5dd6f`: every log line goes through one locked writer |
| 4. Seven flags silently ignored | The tank accepted flags it never read | Gone with the flags. A recipe refuses what it does not use |
| 5. The default knockout cut two channels | A bench without the flag knocked out scent and chemical together | Gone with the defaults. The exam lists its controls |
| 6. Seeds reuse at 1,000 draws | Seeds are the generation times 1,000 plus the draw | The recipe reader refuses 1,000 draws or more. The formula stays, so no draw moves |

## The build order

1. **The run record**, on the old flags. A few hours, and it moves no number.
2. **The recipe and the episode's setup**, with world overrides by tunable path, and the purse as a
   Core method. The coordinator deferred the Core method on 2026-10-02. No current recipe uses the
   purse `world`, and moving it costs a full Core run and the farm's digest check. At step 6 the
   option is dropped if no recipe uses it. If one does, the move waits for an idle machine.
3. **The reward interface**, with `Net`, `WeightedSum`, `EpisodeRecord` and their tests. Selection
   and the exam read the score through it.
4. **The registry**, with rerun.
5. **The baseline recipe and the check** of section 6.
6. **Deleting the old code**: the flags, the archive mode, the swim test, and every setting no recipe
   uses. The tank bench stays as the exam.

The variation (brain mutation, the crossover) and the selection (elites, tournament, per-body groups)
stay as they are, read from the recipe's numbers. When an experiment needs a second kind of either, it
gets an interface the same way the reward did, and not before.

The machine is shared while the build runs. The brain v2 run and its exam hold half of it until the
coordinator says they have ended. Until then the build runs only small filtered tests, one process
at a time, and nothing that swims an episode. After that, everything together stays within 16 of the 32 logical processors, and the
heat is read with `scripts/temp-check.ps1` every ten minutes while heavy work runs.

## Not covered, and open

- **The timing run** (`--time-one`, `--film`) is deleted in step 6. The owner, asked on 2026-10-02
  whether to delete it, agreed that it should go. Timing comes from `timing.jsonl`, watching a body from
  the nursery room's tank view, and fine pictures from its Blender export. The coordinating
  session measures brain v2's sensing cost with the old timing run on its own build
  (`scratch/wt-joint/artifacts/nursery-v10`), which is left untouched until that measurement ends.
  Open for the owner: whether the recipe nursery should keep a way to time one episode, the wall
  time of one tank at one thread under a recipe. Nothing is built for it now.
- **A search seeded from a pool starts from fresh brains** when its genomes carry one brain (D146).
  EvolveTank gives each such body a fresh brain before generation 0, so an exam's brain is not
  carried on. The old comment said otherwise; it dates from before D146 and became wrong when D146
  added the fresh draw. No past arm was affected (the coordinator checked: every arm before brain v2
  had per-node brains, which are kept, and no past search was seeded from a pool). A setting
  `bodies.brains: "fresh" | "kept"` waits until an arm needs it, and then goes to the owner with that
  arm (the coordinator, 2026-10-02).
- **Reproduction across builds** is not promised. A rerun is bit for bit on the build that made its
  entry, and on no other.
- **The sensing cost.** Brain v2 runs 7.7 minutes a generation against the old brain's 4.6. The
  likely cause is sensing every channel on every part each step (HANDOFF 10a). A cache within the step
  is separate work on Core and Dynamics, measured on an idle machine, and it belongs to the
  coordinating session. It comes before the next long arm, whatever the framework's state.
- **The next arms** are the owner's, chosen with the brain v2 exam in hand. If its brains use their
  senses, each arm removes one piece of the baseline. If they are blind, the arms change what pays,
  through a new reward class or a new setup.
- **The nursery room.** The owner has rethought the dashboard proposal as a local web app, with an MCP
  through which agents run the experiments. Its brief and wireframes are in `design/nursery/`.
  Whether it is built is the owner's call, and the coordinator places it after step 5. Its design
  pass listed data the nursery does not write yet, in `design/nursery/HANDOFF.md`:
  - a log of who did what and why
  - a store for pictures
  - which genomes shared a tank on a draw
  - genome ids with their parents' ids
  - the exam's plan and its progress, and the founder draw and kept bodies, in `run.json`
  - a checker that returns every refused field and not only the first

  These belong to the room's build. Two other needs of the room are met in this build: the
  exam rows carry `fresh` per control, and the recipe carries a purpose line in `description`.

## What was built

Filled in as each step lands: the commit, what it changed, the tests and their counts, and any
reading that moved.

**Step 1, the run record** (branch `nursery-general`, 2026-10-02). The nursery writes `run.json`
before its first episode and again at its end, with the arguments, every option as resolved, the
world config's file digest and hash, the episode config in full with its hash, the seed and the
tank's two episode salts, the thread count, the process and its start time, the commit, the dirty
flag and four tree hashes. The tank search's generation rows lose `seconds`, which moves to
`timing.jsonl`; nothing else in a row changed. An Opus review found nothing that moves a draw or a
row field. It found four faults in the writing of the record, all fixed: the closing write could
turn a finished run into a crash, a failure kept an earlier exit code, a held file failed the move,
and the git call could hang. The nursery's tests pass, 38 of 38, four of them new. A run of the d_ref
measurement, which swims no episode, wrote a correct record.

Left for later steps:
- The archive mode's rows still carry `seconds`. The mode is deleted in step 6.
- Core's JSON reader takes a number through a double, so a seed above 2^53 would read back rounded.
  The recipe reader of step 2 must read seeds as exact integers or as strings.
- Only the search's world config is recorded. The still and exam configs belong to the archive mode.

**Steps 2 to 4, the recipe, the reward and the registry** (branch `nursery-general`, 2026-10-02,
commit `41206ff`). Built together, since they share the nursery's entry point and the tank. Two Opus
reviews and a re-check of their fixes found nothing that changes any number the search writes for
the baseline. The nursery's tests pass, 123 of 123.

- **The recipe** (`Recipe.cs`, `rounds/nursery/brain2.json`).
  - JSON format 1, every field required and an unknown one refused. A base merges under its child
    object by object, and the two maps, `world.overrides` and `reward.parameters`, replace whole.
  - Paths are relative to the repository root, and a rooted one is refused. Seeds and salts are
    strings.
  - The canonical text, LF line ends, is saved beside the run as `recipe.json` and its SHA-256 is
    the recipe's hash.
  - World overrides go through Core's tunable walk, re-read for each episode.
  - `--check` prints the resolved settings, the exam's brains and the hashes, and swims nothing.
    On the baseline it prints the episode config hash `87d66e98fed542c2`, the one
    `pilot-deep-brain2`'s log printed.
  - Under a recipe, `run.json` carries the resolved recipe and leaves out the flags' options, whose
    defaults would misreport the knockout and the work cost.
  - The world config the baseline cites is committed in `rounds/nursery/configs/` with its bytes
    kept. Its inner `configHash` field is stale, which the folder's README explains.
- **The reward** (`Reward.cs`). `IReward` scores all of a genome's episodes. `Net` reproduces the
  old running mean bit for bit: the same sequential sum per generation, and the same update. It
  refuses episodes out of the order they were swum, rather than sum them in a new order.
  `WeightedSum` and the registration attributes are built. The search ranks on the reward's value,
  and `tank-scores.jsonl` carries each generation's top genome's parts. The exam pairs each control
  against the genome's own brain on the reward's value, and its rows gain `score` and `fresh`, the
  food density at the body under that control.
- **The registry** (`Registry.cs`, `rounds/nursery/registry.jsonl`).
  - There is one registry, the main checkout's, whichever worktree a run starts from.
  - Every run of a recipe under `rounds/nursery/` is entered, however it ended. A run from a dirty
    tree is entered as not rerunnable, with the reason.
  - `--rerun` takes an entry number, or 8 or more digits of a recipe hash. It refuses a build whose
    tree hashes differ, a reference entry, and an entry that is not rerunnable. At the end it
    compares result digests and names the first differing file and line.
- **Fault 2 is fixed.** Fixed snow now takes its draw, so a fixed-snow run from the old flags is a
  new realisation on this build. No current recipe fixes its snow.

Left for later steps:
- The tank's flags still work beside the recipe until step 6.
- A run's `run.json` does not yet carry the exam's plan, the founder draw or the kept bodies (the
  nursery room's list).

**Step 5, the check** (2026-10-02, 14:47 to about 15:08, on the coordinator's "machine free"). The
baseline recipe ran for 3 generations, with the exam cut to the own brain on one draw
(`scratch/wt-nursery-general/scratch/step5/brain2-check3.json`). It ran on nursery-g1, a Release
build of `41206ff`, at 14 threads, as the pilot did. `scripts/reads/nursery-baseline-check.py`
compared it with `pilot-deep-brain2`:

| File | Generations | Verdict |
|---|---|---|
| `tank-generations.jsonl`, `seconds` dropped | 0 to 3 | identical, each |
| `tank-best.jsonl` | 0 to 3 | identical, each |
| `tank-crossovers.jsonl` | 0 to 3 | identical: no children, then 47, 49 and 41 rows |

Three generations write four rows, since generation 0 is the founders. The script's first default
compared three, so it stopped one row short. It now compares every row the new run wrote. The
coordinator accepted the result the same day. The nursery logged the check run as a trial, since its
recipe lies outside `rounds/nursery/`, and did not register it.

**Step 6's rerun** (2026-10-02, 15:14 to about 15:50) used the same recipe bytes on a Release build
of `2706a21`, at 14 threads on the efficiency cores. It was digest-identical to step 5's run in all six
result files, and the coordinator accepted it. `nursery-general` was fast-forwarded to `2706a21`.
`pilot-deep-brain2` is registry entry 1, a reference entry written through `RegistryEntry`, with its
tree hashes unknown because it predates the run record.

**The exam's draws follow the recipe's seed.** The exam's held-out episode seeds are
`SeedFor(seed ^ exam.salt, e)` (`SeedsFor` in `Program.cs`), so a recipe that changes its seed and
keeps the salt gets new exam water. Its exam then does not pair draw for draw with its base's. To
keep the pairing, choose the salt so that `seed ^ salt` is unchanged. The first registered arm,
`rounds/nursery/brain2-rep2.json` (`56c0f6f`), does so: seed 0x2 with salt 0x62656e636b, whose
XOR is the pilot's 0x62656e6369 (the coordinator's ruling, 2026-10-02). The search's draws, its
generation-0 brains and its mutation stream all follow the seed, so they are new, as a replicate
needs.

**No timing option is built.** The owner passed the question to the coordinator, which ruled to
leave `--time` out (2026-10-02). A timing read is a small trial recipe at `--threads 1`, read from
`timing.jsonl`. Its exam can be cut to `episodes: 1` and the one control `own`, as step 5's check did
(the exam took 0.2 minutes), so the run ends just after its generations.
