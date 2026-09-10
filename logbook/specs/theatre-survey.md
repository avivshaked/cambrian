# `Evosim.Theatre` survey — what exists, what a first viewer needs

Read-only survey, 2026-09-05. All paths relative to repo root
`d:\Projects\experiments\evolution-simulator`.

## 1. DESIGN.md: theatre, presentation, replay, gallery, charts

- **§6.1 assembly table**, `DESIGN.md:1877-1883`:
  ```
  | `Evosim.Core`   | No  | Genome, development, mutation, archive, serialization, RNG |
  | `Evosim.Sim`    | Yes | Phenotype builder, environments, sensors, effectors, evaluator, tiling |
  | `Evosim.Farm`   | Yes | Headless orchestration, island model, batch entry point |
  | `Evosim.Theatre`| Yes | Replay, camera, lighting, gallery, charts, validation harness |
  | `Evosim.Tests`  | No  | Edit-mode tests against `Evosim.Core` |
  ```
  None of `Evosim.Farm`, `Evosim.Theatre`, `Evosim.Tests` exist as asmdefs yet (§6, below).
  The project narrative in `CLAUDE.md:98-104` restates the split: "the farm and the theatre
  are separate programs... they share only a serialization format."

- **§10 milestone table**, `DESIGN.md:2088-2113`. Milestone 8 (not yet started):
  `DESIGN.md:2106`: *"Theatre: replay, gallery, charts, lineage, export, **fluid validation
  harness (§5.4)**"* → *"Showpiece + research instrument."* Milestones 0-6 are ✅/partial;
  7 (food web) and 8 (theatre) are unstarted; 9 (land) deferred.

- **§5.4 fluid validation harness**, `DESIGN.md:722-771`. Response item 4,
  `DESIGN.md:771-773`: *"Build a validation harness (Milestone 7 [sic, later retitled
  Milestone 8]). Re-simulate archive champions under a higher-fidelity fluid model; record
  whether displacement agrees in sign and magnitude. Disagreement is a first-class logged
  result, **surfaced in the theatre UI**."* This is the only place the document describes a
  theatre *UI* behaviour rather than just naming the assembly.

- **§8.1**, `DESIGN.md:295-296`: *"the grid doubles as the gallery UI, making the research
  instrument and the visual showpiece the same screen"* — i.e. the planned MAP-Elites
  archive grid (§8.3, part-count × gait-frequency, `DESIGN.md:2038-2047`) is meant to *be*
  the gallery, not a separate view.

- **§5.1 exaptation aside**, `DESIGN.md:670-673`: *"A bonus for the theatre (§10, Milestone
  7): [C18 Fig. 22]... A lineage view showing a tentacle becoming a leg is exactly the kind
  of thing worth building the gallery around."* — the only concrete feature suggestion
  (a lineage/ancestry view), never elaborated.

- **§9 persistence**, `DESIGN.md:2069-2087`, plans a `runs/<runId>/` layout with
  `archive/`, `metrics.csv`, `champions/` (*"hand-picked genomes for the theatre"*) and
  `validation/` (*"champion re-simulation under higher-fidelity fluid (§5.4)"*). **None of
  this exists** — the actual run directory (§2 below) has a completely different, later
  layout built for the ecosystem design (§5A), which superseded the MAP-Elites-era plan
  this section describes. `archive/`, `champions/`, `validation/`, `metrics.csv` are not
  produced by any code found.

- **§10 (rendering)**: the milestone table's row 0 (`DESIGN.md:2097`) is the only §10 text
  — *"Unity project, URP, assemblies, physics config"* — there is no separate "§10"
  rendering section in the current draft; URP is covered operationally in `CLAUDE.md`
  ("`-createProject` gives you the Built-In Render Pipeline... URP has to be added
  deliberately; `Evosim/Set Up URP` generates the pipeline assets").

- **Camera/rendering elsewhere in DESIGN.md**: none. No mesh, material, shader or camera
  design appears outside the assembly table and the milestone table.

## 2. Run directory contents and row schemas

Doc comment on `RunDirectory`, `src/Evosim.Core/Serialization/RunDirectory.cs:13-20`,
describes the intended layout (`config.json`, `lineage.jsonl`, `stats.jsonl`,
`absorptive.jsonl`, `snapshots/`) but **is stale on one point**: it says stats.jsonl exists
only as a writer nobody calls (echoed again at `RunDirectory.cs:47-56`'s remark on
`Absorptive`), but as built, `EvolutionRun.cs:1750` *does* call `dir.Stats.WriteRow` every
report interval (see below) — likely written before D061 wired stats.jsonl up, since the
report table itself has moved past `RunDirectory.cs`'s comment.

Additional files not documented in `RunDirectory.cs` at all:
- `run.json` — the run manifest, written by `EvolutionRun.WriteRunManifest`,
  `unity/Assets/Evosim/Sim/Editor/EvolutionRun.cs:1191-1260`.
- `diverged/<id>.json` — one file per solver-diverged body, written by
  `Ecosystem.DumpDiverged`ish code, `unity/Assets/Evosim/Sim/Ecosystem.cs:430-520`.
- The markdown report `runs/<name>.md` (read by `scripts/parse-arm.ps1:3`,
  `scripts/analyse-arm.ps1`), built alongside `stats.jsonl` from the same locals in
  `EvolutionRun.cs`'s `Row` method (`EvolutionRun.cs:1454` onward).

### `config.json`
Written by `RunConfigJson.Write`, `src/Evosim.Core/Serialization/RunConfigJson.cs:48-96`:
`format`, `configHash`, then one object per `[ConfigGroup]`-style group, each field written
generically by reflection over `RunConfig`'s properties (float/int/bool/enum). This is the
mechanism `CLAUDE.md` calls "two reflection-driven tests guard the config" —
`RunConfigTests`/`RunConfigJsonTests` — so every tunable in `src/Evosim.Core/RunConfig.cs`
round-trips here.

### `run.json`
Fields, `EvolutionRun.cs:1198-1250`: `arm`, `seed`, `unityVersion`, `physicsDtSeconds`,
`metabolicStepSeconds`, `requestedSeconds`, `requestedWallMinutes`, `configHash`,
`inoculateGenomePath`, `inoculateGenomeHash`; nested `source` object — `gitCommit`,
`gitDirty`, `coreHash`, `simHash`, `workerPath`, `repoRoot`, `note`; `startedAt`; then
either `status: "running"` or, once ended, `status`, `reason`, `ending` (prose),
`endedAt`, `simulatedSeconds`, `physicsSteps`, `births`, `aliveAtEnd`, `wallClockMinutes`,
`timesRealTime`, `dragImpulsesLimited`, `driveImpulsesLimited`, `divergedTotal`,
`matterInfluxedTotal`, `matterBuriedTotal`, `bestSpeed`, `bestSpeedAtSeconds`. Written
twice — creation (`status: running`) and at the orderly or errored end, replacing the
file via a temp-file-and-move (`EvolutionRun.cs:1255-1260`) so a poller never sees a
half-written manifest. This is the file `stop-arm.ps1` merges `status: "stopped"` into.

### `lineage.jsonl`
One `LineageEvent` per birth or death, `src/Evosim.Core/Ecosystem/LineageEvent.cs:144-172`.
Birth row: `{"e":"b","t":<sec>,"id":<long>,"p":<parentId,-1 if none>,"k":"f"|"r"|"i",
"g":<generationDepth>,"s":<speciesId>,"abs":0|1,"jnt":0|1,"pt":<patch>}`. Death row:
`{"e":"d","t":<sec>,"id":<long>,"c":"starved"|"diverged"}`. **No position, energy, depth,
or genome** on either row — confirms `CLAUDE.md`'s "cause of death discriminates nothing"
(only two `DeathCause` values exist, `LineageEvent.cs:126-137`) and that lineage carries
only the two coarse phenotype flags (`abs`, `jnt`), not a full developed phenotype.

### `stats.jsonl`
One row per report interval, built in `EvolutionRun.cs:1454` (`Row`) and written at
`EvolutionRun.cs:1750-1840`. ~50 fields, world-level aggregates only (no per-creature
data): `t`, `alive`, `births`, `deaths`, `jointed(Inherited)`, `dof`, `meanSpeed`,
`maxSpeed`, `workJoulesPerSecond`, `spendJoules`, `workJoules`, `lightJoules`,
`foodJoules`, `absorptive(Inherited)`, `detritusJoules/Here/OnFloor/Deep`, `meanHeight`,
`heightSd`, `meanRise`, `meanAge`, `dayFactor`, `shading`, `buoyant(Inherited)`,
`liftHeld`, `buoyantDepth`, `matterHere/Surface/Deep/Standing`,
`conceptionsBlockedByMatter`, `floorSpawns(Window)`, `secondsSinceFloorFired`,
`generationMin/Max`, `auditResidual`, `species`, `edibleDetritusHere`, `belowWorld`,
`absorptiveBelowWorld`, `matterLocked`, `refugeJoules`, `excretedTotal/Window`,
`detritusPatchSd`, `patchMaxShare`, `detritusDeposited/Taken(Total/Window)`,
`detritusExuded(Total/Window)`, `absorptiveLogged`, `photosynthetic(Inherited)`,
`diverged`, `matterInflux/Buried(Window/Total)`. Confirms `CLAUDE.md`'s note that depth,
volume, energy are *aggregate*, never per-creature.

### `absorptive.jsonl`
Per-creature-per-sample rows for absorptive-tissue-carrying creatures, written at
`EvolutionRun.cs:1732-1741` from an `AbsorptiveSample` struct
(`src/Evosim.Core/Ecosystem/AbsorptiveSample.cs`) — not read in detail here, but by name
it is the closest thing to a per-creature time series in the whole run, restricted to one
guild.

### `snapshots/<time>.jsonl`
Written by `EvolutionRun.Snapshot`, `EvolutionRun.cs:1426-1440`: **one genome per line**,
via `JsonlWriter.WriteGenome` → `GenomeJson.Write` (`src/Evosim.Core/Serialization/
GenomeJson.cs:48-72`). The genome format (`format`, `root`, `reproduction.brood/endowment`,
`nodes[]` — cell/shape/joint/power/lift/recursiveLimit/dimensions/jointLimits — and
`globalBrain[]` neurons) is **the genotype graph only**. `GenomeJson.Write` never writes an
organism id, position, energy, age or developed-phenotype geometry — confirmed by reading
the method body. **This verifies `CLAUDE.md`'s claim exactly**: snapshot rows hold genome
graphs, not phenotypes, and carry no id to join against `lineage.jsonl`'s `id` field. A
viewer wanting "this creature, as it looked/behaved at time T" cannot get there from a
snapshot row plus a lineage row — there is no shared key, and the snapshot has no state
(position/energy/depth) at all, only genes.

### `diverged/<id>.json`
The one exception with real phenotype state: `Ecosystem.cs:430-520` dumps, per part,
`index`, `name`, `parentIndex`, `jointType`, `cellTypeId`, `power`, `volumeM3`, `massKg`,
optionally `lastVelocity/lastAngularVelocity/lastSpeed/lastSpinRate`, `driveTorque`, and
**`position`, `velocity`, `angularVelocity`** (`Ecosystem.cs:490-492`), then the genome
raw-embedded (`Ecosystem.cs:506`). This is the only artifact in the whole run directory
that pairs a genome with physical state — but it only exists for bodies the solver blew
up, capped at `MaxDumps`, and is explicitly a post-mortem, not a general mechanism.

## 3. Determinism and what a theatre could re-simulate

- `DESIGN.md:1959-1976` (§7) and `CLAUDE.md`'s "PhysX replays bit for bit" gotcha: **same
  machine, same build, same `(genome, seed, configHash)` replays bit-for-bit** — three
  5,000-s runs of one seed were identical to the last decimal (logbook/0052). But
  `configHash` (`RunConfig.Hash()`) does **not** cover the physics timestep
  (`DESIGN.md:1897-1901`, "outside `RunConfig.Hash()`... queued") — so an exact replay
  needs `config.json` **and** the timestep recorded in `run.json`'s `physicsDtSeconds`
  (`EvolutionRun.cs:1201`), not the hash alone. It also needs the same Unity version
  (`run.json`'s `unityVersion`) and the same build (`source.coreHash`/`simHash`,
  `EvolutionRun.cs:1216-1217`) — `run.json`'s whole `source` block exists for exactly this
  reason.
- **A theatre re-simulating a whole world** from `config.json` + seed is therefore
  plausible in principle on the same machine/build: `EvolutionRun.RunBody`
  (`EvolutionRun.cs:109` onward) does nothing except read env vars into a `RunConfig`,
  call `new Ecosystem(config, seed)` (`EvolutionRun.cs:506`), and step it — there is no
  scene, no GameObjects placed by hand, nothing else consumed at launch. A second harness
  could read `config.json` (via `RunDirectory.ReadConfig`, `RunDirectory.cs:120-129`)
  instead of env vars, construct the same `Ecosystem`, and get the same trajectory *if* it
  also matches `run.json`'s recorded `physicsDtSeconds` and runs the same build. That is a
  **whole-world re-run**, not a single-creature replay — the ecosystem has no per-creature
  checkpoint/resume, only whole-world stepping from t=0.
- **`EvolutionRun.cs` at launch needs**: nothing but environment variables (all
  `EVOSIM_*`, read via `Env`/`EnvULong`/etc. helper methods, `EvolutionRun.cs:2092-2160`)
  and `EVOSIM_REPO_ROOT` for locating the repo from a worker copy
  (`EvolutionRun.cs:1084-1105`) plus `EVOSIM_OUT` for where to write the run
  (`EvolutionRun.cs:412`). It is invoked via Unity's `-executeMethod
  Evosim.Sim.EditorTools.EvolutionRun.Run` in `-batchmode -nographics` (per `run-arm.ps1`,
  not fully read here but implied by every mention in `CLAUDE.md`). Nothing in
  `EvolutionRun.cs` checks `Application.isBatchMode` or otherwise refuses non-batch —
  it is plumbing-agnostic.
- **Could a viewer drive the same `Ecosystem` in a non-batch Editor/player with rendering
  on?** Structurally, very plausibly yes: `Ecosystem` (`unity/Assets/Evosim/Sim/
  Ecosystem.cs`) is a plain C# class (not a `MonoBehaviour`) that calls `Physics.Simulate`
  manually (`Ecosystem.cs:321`) and creates creature bodies via `PhenotypeBuilder`, which
  *does* build real `GameObject`s with `ArticulationBody`, mesh and material
  (`PhenotypeBuilder.cs:130-146, 360-391`) — i.e. every creature `Ecosystem` drives already
  has visible geometry, whether or not anyone points a camera at it. Driving it from
  `Update()`/`FixedUpdate()` in Play mode instead of a batch `-executeMethod` loop, with a
  camera in the scene, is not blocked by anything found in `Ecosystem.cs` or
  `EvolutionRun.cs` — no code path is batch-only. **This has apparently never been tried**:
  no file combines `Ecosystem` with a Play-mode driver.

## 4. Rendering that exists today

- **`SandboxSceneBuilder`** (`unity/Assets/Evosim/Sim/Editor/SandboxSceneBuilder.cs:1-60`,
  menu `Evosim/Rebuild Sandbox Scene`): builds `Assets/Scenes/Sandbox.unity` in code —
  one `Main Camera` with `FollowCamera`, one directional light, one `CreatureSpawner`
  GameObject. Explicitly generated, never hand-edited (scene YAML doesn't diff).
- **`RenderPipelineSetup.cs`** (`unity/Assets/Evosim/Sim/Editor/RenderPipelineSetup.cs`,
  menu `Evosim/Set Up URP` per `CLAUDE.md`) — generates URP pipeline assets; not read in
  full but its existence and role are confirmed by its file location in the same
  `Sim.Editor` asmdef and by `CLAUDE.md`'s gotcha about `-createProject` defaulting to
  Built-In RP.
- **`PhenotypeBuilder.cs`**: for every developed body part, builds a primitive collider
  (`BoxCollider`/`CapsuleCollider`, `PhenotypeBuilder.cs:336-351`) plus one or more child
  `"Visual"` GameObjects carrying a `MeshFilter` (shared cube/sphere/cylinder primitive
  mesh) and a `MeshRenderer` using a single shared `Material` resolved via
  `Shader.Find("Universal Render Pipeline/Lit")`, falling back to `"Standard"`
  (`PhenotypeBuilder.cs:355-391`). No per-cell-type colour, no texture — every part is the
  same material (`_partMaterial`, one instance, name `"Evosim Part"`). §5A.5 ("Colour and
  perception", `DESIGN.md:1382-1403`) is unimplemented visually, matching the milestone
  table's "photosensors, colour... do not exist" (`DESIGN.md:2103`).
- **`FollowCamera.cs`** (`unity/Assets/Evosim/Sim/FollowCamera.cs:1-104`): explicitly
  documented as *not* the theatre — its own doc comment: *"A viewing aid for the sandbox
  scene, nothing more. The theatre (DESIGN.md §6.1, Milestone 7) is where presentation
  actually lives and will not reuse this."* Mouse-orbit + scroll-zoom, follows one
  `Transform`, frames by a caller-supplied radius.
- **`CreatureSpawner.cs`** (`unity/Assets/Evosim/Sim/CreatureSpawner.cs`): the Milestone-1
  play-mode scaffold. Grows a **random** genome from a seed (`PopulationKind.Founder` or
  `.Elaborate`, `CreatureSpawner.cs:184`) — it does **not** load a genome from a snapshot
  file or any run output. Drives with `EffectorDriver.DriveTestSine`
  (`CreatureSpawner.cs:303`), a fixed test sine, **not** the evolved brain graph — the only
  `EffectorDriver` method is `DriveTestSine` (`EffectorDriver.cs:422`); there is no
  brain-driven playback path in `Evosim.Sim` outside `Ecosystem.cs` itself, which is
  headless-only today. Its own doc comment (`CreatureSpawner.cs:8-17`) calls itself "a
  scaffold, not the evaluator... there is no fitness and no brain here."
- Nothing under any `Theatre`/`Viewer`/`Gallery`/`Chart` name exists anywhere in the repo
  (confirmed by a case-insensitive filename and content grep across `unity/Assets` and
  `src/`) except the doc-comment cross-references above.

## 5. Charts and analysis tooling

No plotting/charting library or code exists anywhere in the repo (checked for
matplotlib/XPlot/Chart.js/Plotly/d3 by name — zero hits outside this survey). Every
"chart" today is a plain-text table read by hand or by PowerShell:

- `scripts/analyse-arm.ps1` — reads a run's markdown report table by **named** column
  (parses the report's own header row into a name→index map, per-arm status lines, or a
  `-Timeline` slice at chosen intervals/column subsets). The header-name discipline exists
  because a positional misread once reported float tissue as the food chain
  (logbook/0044).
- `scripts/parse-arm.ps1` — generic report parser, no verdicts (`parse-arm.ps1:3`).
- `scripts/read-arm.ps1` — scores a report against D051's pre-registered predictions
  (`read-arm.ps1:3`).
- `scripts/clade-score.ps1` — streams `lineage.jsonl`, walks parent chains to find the
  largest connected clade, applies D063's goal-rule clauses plus the stability and
  producer-lineage amendments, reading `photo`/`photo inh` columns by header name.
- `scripts/absorptive-log.ps1` — reads `absorptive.jsonl`, the per-creature ledger log.
- `scripts/lineage-invasion.ps1` — an inoculated lineage's R0, generations, alive-at, from
  `lineage.jsonl`.
- `scripts/ledger.ps1` — a standalone per-body energy-ledger forecast (net watts,
  break-even density, lifetime, R0) run against a genome file plus a config, no Unity.
- `scripts/run-arm.ps1` / `scripts/stop-arm.ps1` / `scripts/new-worker.ps1` — orchestration
  (launch, stop, clone workers), not analysis.
- `scripts/read-sensors.ps1` — unrelated: reads MSI Afterburner hardware sensors for
  machine-load monitoring, not simulation output.

All of these are read/summarize tools over text; none renders anything visual.

## 6. Assemblies today

Only two asmdefs exist under `unity/Assets` (found by glob, none named `Farm`, `Theatre`
or `Tests`):

- `unity/Assets/Evosim/Sim/Evosim.Sim.asmdef` — `name: "Evosim.Sim"`,
  `rootNamespace: "Evosim.Sim"`, `references: ["Evosim.Core"]`, no engine-reference
  restriction (`noEngineReferences: false`), no platform restriction (runs in Editor and
  builds).
- `unity/Assets/Evosim/Sim/Editor/Evosim.Sim.Editor.asmdef` — `name: "Evosim.Sim.Editor"`,
  `rootNamespace: "Evosim.Sim.EditorTools"`, `references: ["Evosim.Core", "Evosim.Sim",
  "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime"]`,
  `includePlatforms: ["Editor"]` — this is where `EvolutionRun.cs`, `SandboxSceneBuilder`,
  `RenderPipelineSetup` and every other `-executeMethod` entry point lives, which is why
  it alone references URP (for `RenderPipelineSetup`).
- `Evosim.Core` itself is not a Unity asmdef inside `unity/Assets` — it is pulled in as a
  **local package** (`Packages/manifest.json` → `file:../../src/Evosim.Core`), per
  `CLAUDE.md`.

**Where `Evosim.Theatre` would sit**, following the existing pattern: a new
`unity/Assets/Evosim/Theatre/Evosim.Theatre.asmdef`, referencing `Evosim.Core` (to read
`RunDirectory`/`GenomeJson`/genomes) and `Evosim.Sim` (to reuse `PhenotypeBuilder`,
`EffectorDriver`, `FluidEnvironment`, and — once written — a brain-driving path currently
living only inside `Ecosystem.cs`). If it needs Editor-only tooling (an asset importer, a
custom window) that piece would mirror `Evosim.Sim.Editor` — a `Theatre/Editor/` folder
with its own `includePlatforms: ["Editor"]` asmdef referencing URP the same way
`Evosim.Sim.Editor` does, for camera/material setup. Nothing today would need to move for
this to slot in; `Evosim.Sim`'s existing "no UnityEngine in Core" boundary is untouched by
adding a fourth Unity-side assembly beside it.

## Gaps — what a minimal theatre needs, ranked by size

**Mode A — load a run, pick a creature from a snapshot, grow it, watch it swim under its
own evolved brain, rendered.**

1. *Smallest — a loader.* Read one line of `snapshots/<t>.jsonl` via `GenomeJson.Read`
   (exists, `GenomeJson.cs:74-103`) and hand the resulting `Genome` to `PhenotypeBuilder`
   (exists, builds real geometry) in a Play-mode or Editor-tool context instead of
   `CreatureSpawner`'s random-seed path. Nothing here is missing except the plumbing —
   `CreatureSpawner.cs:184` would need a genome-file input option alongside its two
   `PopulationKind`s.
2. *Small-medium — brain-driven playback outside the headless loop.* The brain evaluator
   (`Brain.For`/`Brain.Step`, `src/Evosim.Core/Brain/Brain.cs`) and its sensor/effector
   wiring are exercised only inside `Ecosystem.cs` today (`Ecosystem.cs:722, 308-311`).
   `EffectorDriver` (the Sim-side class actually used by `CreatureSpawner`) exposes only
   `DriveTestSine` (`EffectorDriver.cs:422`) — there is no `DriveBrain`-equivalent call
   site outside `Ecosystem`. A viewer needs either (a) to reuse `Ecosystem` itself in
   Play mode (structurally plausible, per §3, but untried), or (b) a new thin driver that
   builds one creature's `Brain`+sensors+effectors the way `Ecosystem.cs:700-750` does,
   without the whole-world economy loop.
3. *Medium — per-creature identity from a run.* Snapshot rows carry no organism id
   (§2), so "pick a creature from a snapshot" today means picking a genome with no
   record of what happened to it (age, lineage, cause of death) — a viewer wanting to
   show "this is creature #4821, born of #203 at t=1200, still alive at snapshot time"
   needs a new id field added to `GenomeJson`/the snapshot writer, since `lineage.jsonl`'s
   `id` has nothing to join against on the snapshot side today. Not a large code change,
   but touches the on-disk format (`GenomeJson.FormatVersion` would need bumping per its
   own refuse-rather-than-default rule, `GenomeJson.cs:33-46`).
4. *Medium — a picker UI.* Nothing exists to browse `snapshots/*.jsonl`, list genomes, or
   select one — would be a new Editor window or Play-mode UI, from scratch.

**Mode B — replay a whole world from `config.json` + seed, rendered.**

5. *Small — feasible in principle, per §3.* `RunDirectory.ReadConfig` +
   `new Ecosystem(config, seed)` is the entire launch sequence `EvolutionRun.RunBody`
   already performs; a rendered variant is "run the same loop from a `MonoBehaviour`'s
   `Update`/`FixedUpdate` instead of a batch `while` loop, with a camera present." No
   structural blocker was found, but it has never been built or tried, so treat "small" as
   "small once attempted" rather than "already proven."
6. *Medium — matching the physics step exactly.* `physicsDtSeconds` is not covered by
   `RunConfig.Hash()` (`DESIGN.md:1897-1901`) and must be read from `run.json` and applied
   via `Ecosystem.ConfigurePhysicsStep` before replay starts, or the replay silently
   becomes a different chaotic realisation (§7's whole "butterfly" finding) rather than a
   faithful one.
7. *Medium — same build.* Bit-identical replay is only measured **same-machine,
   same-build** (`DESIGN.md:1968-1976`); a theatre run from a different Unity session/build
   than produced the record is not guaranteed to match it, only to be plausible. `run.json`
   carries `coreHash`/`simHash`/`gitCommit`/`gitDirty` precisely so a mismatch can be
   *detected*, not to make cross-build replay work.

**Cross-cutting, larger:**

8. *Large — charts.* No charting code exists anywhere (§5); a theatre chart view (of
   `stats.jsonl`, say) is a from-scratch UI, not an extension of anything.
9. *Large — the gallery/archive.* §8.1's "the grid doubles as the gallery UI" describes a
   MAP-Elites archive that does not exist in the current ecosystem-based design at all —
   `archive/`, `champions/` (§9's planned layout) are unbuilt, and Milestone 8's "gallery"
   has no candidate data structure to browse today beyond raw snapshot files.
10. *Large — the fluid validation harness itself* (§5.4 item 4): re-simulating archive
    champions under a higher-fidelity fluid model requires that higher-fidelity model to
    exist first (added mass is specified, `DESIGN.md:748-757`, but not confirmed built in
    this survey), independent of any viewer UI.
