# The nursery room's tank: how an exam episode is re-stepped exactly

Written 2026-10-02 from an Opus planner's read of the code at `1738587` on `nursery-room` (brief:
`scratch/nursery-general/room5/plan-brief.md`). Nothing here has run yet. Every claim is from
reading code, and the steps marked **swim** are what test it. Phase 3's done-when (in
`nursery-room-build.md`) is the target: the tank screen re-steps an exam episode of
`pilot-deep-brain2` and marks it exact.

## What one exam episode is

- **The function.** `Program.TankEpisode(Genome[] genomes, ulong seed, Arena arena)` swims one tank
  (`Tank.cs` 146–300). `SwimTanks` calls it (311–333), and `TankBench` calls `SwimTanks` (726).
- **What goes in:**
  - The genomes are the tank's members in plan order, each with its control's brain grafted on
    through `ExamBrain` (766–779).
  - The seed is `seeds[d]`, from `SeedsFor(0, episodes, salt)` = `Rng.SeedFor(seed ^ salt,
    generation*1000 + e)` (`Program.cs` 805–810).
  - The tank index is not an input. Every tank of a draw swims on that draw's seed. The index only
    picks the members, from `TankLog.PlanTanks(n, tankSize, seeds)[d]`.
  - The config is the arena's text through `_recipe.EpisodeConfig`: the recipe's overrides plus the
    closed floor.
- **What comes out:** one `Resident` per slot. `per[d]` is a member's `Net` formatted `"R"`, or
  `null` for a body that did not land (`Tank.cs` 749).
- **The pilot's exam:** 6 genomes go into tanks of 8, so each draw is one tank of all 6 under one
  control. An episode therefore checks 6 numbers.

## What the pilot's exam is made from

Everything an exam episode of `pilot-deep-brain2` needs can be recomputed:
- **Genomes:** `tank-exam-genomes.tsv`, 6 lines, bodies 0, 1, 3, 4, 8 and 10. GenomeJson
  round-trips floats losslessly.
- **Controls:** `rounds/nursery/brain2.json`, which registry entry 1 ties to the pilot. Brains are
  matched by index, never by label: the pilot labelled index 3 `knockout` where the recipe says
  `knockout Chemical`, which is one of the entry's `knownDifferences`. `tank-exam.jsonl` holds
  108 rows: 18 brains × 6 genomes.
- **Seed 1.** No file in the run records it. It is the old build's default (`dc5dd6f`
  `Program.cs:60`). The launch script passes no `--seed`, `brain2.json` says `"0x1"`, and step 5's
  identical generations depend on it.
- **Salt `0x62656E6368`** ("bench"), logged at `nursery.log:322`. The exam's seeds are
  `Rng.SeedFor(0x62656E6369, e)` for e = 0 to 31.
- **Tank-mates and their order:** `PlanTanks(6, 8, seeds)[d][0]`. The order matters, because
  heights are drawn per slot.
- **The episode config:** hash `87d66e98fed542c2` on the current code, the pilot's
  (`nursery.log:1–2`).

Two things cannot be recovered:
- **The pilot's binary.** Its tree hashes are unknown. The re-step's own result is the only test of
  it.
- **Any search episode.** Its tank-mates' genomes were never kept, so a re-step of one is a cousin of
  the real episode and is marked unverified.

## Is the current code the pilot's for an exam episode

`git diff dc5dd6f 1738587` is empty for Core, Dynamics and Farm. Each change in `Tank.cs` and
`Program.cs` on the exam path reads as moving no bit:
- the snow spread drawn into a local first
- the old purse branch removed, with the formula the pilot used kept
- the shuffle moved into `PlanTanks`, with the same algorithm and salt
- `EpisodeConfig` rewritten, giving the same hash
- control parameters parsed as `(float)(double)` rather than `float.Parse`, which gives the same
  floats for every value the recipe uses
- the knockout written over a channel list, the same for `[Chemical]`

The caveat stands above all of these: step 5 cut the exam to `own` on one draw and compared only
the search files. **The pilot's exam has never been reproduced on the current code.**

## The stepper

- **A read-only watcher in the nursery's own `TankEpisode`**, rather than a copy of its loop. It
  is an optional parameter that `SwimTanks` passes as null, and it has four calls:
  - `Begin`, after the carrion is placed
  - `Physics`, on steps without metabolism
  - `Metabolic`, after the per-body sums
  - `End`
- **What the watcher reads:** link poses, senses, hidden units, outputs, energy, corpses and the
  snow's density, all through public reads.
- **What it must not touch:** it gets no `Rng`, and it never calls `Sample`, `Refresh*`, `Commit*`,
  `Step` or the lazy `Creature.LinkReach`.
- **The test:** an episode with the watcher and without it gives equal `Resident`s and equal solver
  digests.
- **It runs as a child process,** not as a thread in the room: `dotnet <nurseryDll> --restep
  <request.json> --out <cache> --threads 1`, started by the room's launcher, which pins it.
  - Inside the room's process the numbers would not match, because the room runs a Debug build
    with JIT tiering on; the nursery turns tiering off.
  - The launcher also strips the JIT's `DOTNET_*` settings and `EVOSIM_TANK_TRACE` from the
    environment.
- **One episode takes about 26–34 s** on one efficiency core. The pilot's exam took 23.3 min at 14
  threads for 576 episodes, and no episode logged "slow tank".
- **About 12 MB per episode at 10 samples a second,** so the stream is binary, with a JSON header
  that names every channel.
- **The comparison:** each member's `Net` against `per[d]`, by `DoubleToInt64Bits`, with no
  tolerance. A body that did not land must match a null.

## Load and timing reads

A re-step is an operation, `episode.step`, with a reason, written to the activity log. It counts 1
thread against the cap for as long as its process lives. The episode is a resource,
`nursery://episode/{run}/{exam}/b{b}/d{d}`.

The room has no way yet to know when a timing read is declared. The proposal:
- A shared declaration file, written by the coordinating session, names who is timing, why and
  until when.
- While it exists, `episode.step` is refused.
- A re-step already running is stopped and marked "stopped for a timing read".

**The coordinator's ruling, 2026-10-02 evening:**
- The file is `scratch/machine/timing-read.json` in the main checkout, not under `rounds/nursery/`.
  "A lock that comes and goes is machine state, not part of the experiment's record". The farm's
  timing reads may use it too.
- It is gitignored, so `appended.txt` and the farm-queue exclude are unchanged.
- Its shape is `{"who","why","until"}`, with `until` an ISO time.
- A file whose `until` has passed counts as absent, so that a timer that crashed cannot block the
  room for ever.
- The room never writes or deletes it. The session that times writes it before and deletes it
  after.

The same ruling allows the nursery's edits (the watcher and `--restep`) on `nursery-room` now, built
and tested with nothing swimming. The merge order stands: advect-fast into `nursery-general`
first, then `nursery-room` rebased onto it, with the digest check that no result moved. S1 runs
right after 3b's digest check, in the same slot.

## The client

Only the two 3D views need WebGL (three.js, kept in the repository with its MIT licence). The
whole-tank map is a 2D canvas, the charts are SVG, and the live brain is SVG or HTML.

The first cut that meets done-when:
- pickers for run, exam genome, draw and brain
- a STEP button
- an EXACT or DIFFERS badge with a table of the six members: expected `per`, the re-stepped net,
  and whether the bits are equal
- a top-down 2D playback
- the energy chart and timeline
- a provenance line

After it, in order: the follow view in three.js, the live brain, side by side, PNG and video, the
Blender export, and search episodes marked unverified.

## Risks, ranked

1. The pilot's exam has never been reproduced on the current code. S1 tests that first, with no new
   code.
2. A re-step inside the room's process would not match. Use the child process.
3. The watcher could disturb state. Allow only a fixed list of reads, and keep the digest test.
4. The nursery edits change `NurseryHash` and touch the coordinating session's files, so they need
   its sequencing and a digest check.
5. There is no timing-read convention yet.
6. The data's volume.
7. Brain labels and 64-bit seeds in JavaScript: match brains by index, and keep seeds as strings.
8. The pilot's recipe comes from registry entry 1, checked by its hash.
9. A pool path that leaves the repository is refused, so S1 copies the pool into its tree.

## Build order

**swim** marks a step that runs episodes and waits for the machine.

| Step | What | Its test |
|---|---|---|
| S0 | The room rebuilds the pilot's exam plan from registry entry 1 | 32 seeds; one tank of 6 per draw; 18 brains; the plan equals `tank-exam-draws.jsonl` on a run that has one |
| S1 **swim**, ~2 min at 2 threads | A trial bench of the pilot's pool: seed 0x1, salt 0x62656e6368, 2 episodes, `own`, the knockout and a constant | `per[0..1]` equals the pilot's, bit for bit. No new code |
| S2 | The watcher in `TankEpisode`, and `--restep` with a dry plan | the nursery suite; `--restep --check` prints the seed and the members |
| S3 **swim**, short | The watcher changes nothing | equal `Resident`s and digests with it and without; a trial's digests the same before and after |
| S4 **swim**, ~30 s | `--restep` writes `episode.json`, `frames.bin`, `events.jsonl` and `result.json` | the pilot's `own`, draw 0: six nets equal `per[0]` |
| S5 | The room's side: `episode.step`, the cap, the environment, the timing-read refusal, the cache, the resource | with the test echo program and fixtures |
| S6 | `tank.html`'s first cut on a fixture episode | parity, contrast, the EXACT/DIFFERS badge |
| S7 **swim** | Acceptance through the screen and the MCP | the pilot's `own` draw 7 and a knockout draw EXACT; the activity row; the refusal during a timing read |
