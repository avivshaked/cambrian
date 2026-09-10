# Build spec: the theatre, first cut (D075, in parallel with perception)

Fable-written spec for an implementing agent working in parallel with the perception build
(`logbook/specs/perception-spec.md`). Read `logbook/specs/theatre-survey.md` first (the read-only survey
this is built on) and CLAUDE.md. The two builds touch different files; the one seam is the
snapshot row's new `id` field (section 2), which the theatre build owns. Nothing is written
outside the repository. Do not launch arms; do not touch `Evosim.Core`'s simulation code,
`Ecosystem.cs`, `EffectorDriver.cs` or `FluidEnvironment.cs` (the perception build is in them
and replay identity is measured there); the theatre consumes them.

## Why

DESIGN.md §6.1: the farm and the theatre are separate programs sharing a serialization format.
Evaluation is headless and fast; presentation is slow, beautiful, and reads stored runs. Nothing
of the theatre exists (survey §6): there is no `Evosim.Theatre` asmdef, no viewer, no picker, no
chart. Every result in the logbook has been read from tables. The owner's path (D075) puts the
theatre alongside the movement round so that when a swimmer first pays its way, someone can
watch it.

## Scope, in order

1. **Mode B, the rendered world.** Load a run directory, rebuild its world from `config.json`
   and `run.json`, and step it in Play mode with rendering on, at a chosen pace. Bit-identical
   to the recorded run on the same machine and build (that identity is measured, survey §3, and
   is the whole reason this mode is cheap to build and honest to show).
2. **A creature id on snapshot rows**, so a snapshot's genome joins `lineage.jsonl`.
3. **Mode A, one creature.** Pick a genome from a snapshot (or any genome JSON line), grow it,
   and watch it swim under its own brain in the reference water, alone.
4. **The overlay:** what the tables say, drawn on the world.

Out of scope for the first cut: charts (from scratch, large; keep reading `stats.jsonl` through
`scripts/analyse-arm.ps1`), the gallery/archive (§8.1's grid depends on a MAP-Elites archive
that does not exist in the ecosystem design), the fluid validation harness (§5.4; needs the
higher-fidelity fluid first), a full browser UI. Each is a later spec.

## 1. Mode B: the rendered world

- **Assembly.** `unity/Assets/Evosim/Theatre/Evosim.Theatre.asmdef`, referencing `Evosim.Sim`
  and `Evosim.Core`. `Evosim.Sim` must not reference it. Editor-only tooling in
  `Evosim/Theatre/Editor/` with the namespace `Evosim.Theatre.EditorTools` (the folder is what
  makes it editor-only; the namespace is not, CLAUDE.md's Milestone1Smoke gotcha).
- **Scene.** Generated, not hand-edited, like the sandbox: `Evosim/Rebuild Theatre Scene`
  builds `Theatre.unity` with a URP camera, a light, the water volume bounds drawn as a wire box
  (the surface at y=0, the floor at the run's depth, the patch boundaries), and one
  `TheatreRunner` MonoBehaviour.
- **Loading.** `TheatreRunner` takes a run directory path (an inspector field, and an
  `EVOSIM_THEATRE_RUN` env var for launching from a script). It reads `config.json` through
  `RunDirectory.ReadConfig`, the seed and `physicsDtSeconds` from `run.json` (the dt is not in
  the config hash; a replay at the wrong step is a different chaotic realisation, not a replay),
  constructs `new Ecosystem(config, seed)` exactly as `EvolutionRun.RunBody` does, and applies
  the run's step through the same `ConfigurePhysicsStep` path. Refuse to load, with the reason
  printed, if `run.json`'s `simHash`/`coreHash` differ from the current build's (compute them the
  way `run-arm.ps1`/`EvolutionRun` do): the theatre shows a faithful replay or says it cannot.
  An override flag lets a mismatched run play with a visible "not a faithful replay" banner.
- **Stepping.** `Ecosystem` steps `Physics.Simulate` itself, so the scene must set
  `Physics.simulationMode = SimulationMode.Script` before the first step and restore it on
  exit. Pace control: paused, 1× (one physics step per `dt` of wall time), N× (several steps per
  frame), and **seek**: run unpaced with rendering suppressed (renderers disabled, no camera
  work) until simulated t reaches a target, then render. Seeking to t=15,000 in a 4,000-body
  world is minutes, not hours, at headless pace; say so in the HUD while it runs.
- **Identity check, built in.** Mode B writes nothing into the run directory. But it reads the
  run's own report table (the run's `run-report.md` or `stats.jsonl`, whichever the survey
  names as the per-sample record) and, at each sample time, compares its live `alive`,
  `births`, `deaths`, and audit against the recorded row; a mismatch is shown in the HUD in red
  with the first differing column. This is the theatre's honesty: a viewer knows whether what
  they are watching is the run or a cousin of it.
- **Rendering.** `PhenotypeBuilder` already builds meshes and materials for every part.
  Colour by cell type (photosynthetic, absorptive, structural: three fixed colours, set in the
  scene builder, not per creature) and tint by energy reserve (dark when near the gate, bright
  when sated: `Organism.SecondsOfReserve` through the same squash the perception build uses,
  or a linear clamp if that is not merged yet). Keep it cheap: one material per cell type with
  a per-renderer property block for the tint; no per-frame material allocation.
- **Camera.** `FollowCamera` says in its own comment that the theatre will not reuse it. Build
  a small free-fly camera (WASD + mouse, scroll for speed) and a follow mode that tracks a
  chosen creature (click to select; the HUD shows its id, generation, kind, age, reserve, depth,
  speed). Selection needs the id map from section 2.

## 2. The creature id on snapshot rows

Snapshot rows carry a genome and nothing to join to `lineage.jsonl` (survey §2). Add the
organism's `id` (the same integer `lineage.jsonl` uses) as the first field of each snapshot row.
`GenomeJson.FormatVersion` bumps per its own refuse-rather-than-default rule; the snapshot
writer in `EvolutionRun.cs` writes the id; the reader accepts the new version and refuses the
old with a message that names the version, as the format promises. A one-line note in
`DESIGN.md`'s serialization section, and `RunConfigJsonTests`-style coverage for the round
trip. This is the one change to the farm's output; keep it to that.

## 3. Mode A: one creature

- Input: a genome JSON line (a snapshot row by index or id, or a standalone file), a seed, and
  a `config.json` for the water (defaults to the reference world's).
- Build: grow the genome through `PhenotypeBuilder`, wire `Brain.For` + `CreatureSensors` +
  the effector driver the way `Ecosystem.Build` does for one creature (survey gap 2: no such
  call site exists outside `Ecosystem`; write a thin `SoloCreature` driver in the theatre that
  copies that wiring, and note it duplicates ~50 lines rather than refactoring `Ecosystem`,
  which the perception build is in). Place it at a chosen depth and patch in a `FluidEnvironment`
  with the run's current field; no economy, no feeding, no death: the organism's energy is held
  constant unless a "starve" toggle lets it decay at its standing watts.
- Show: the creature under its brain for as long as the viewer wants, with the HUD showing
  speed, depth, joint angles, and every sensor channel it reads (seven once the perception build
  lands; read them through `ISensorField.Read` so the theatre needs no change when the pool
  widens). A "sine" toggle drives the same body with the test sine so a viewer can see the
  brain's contribution against the null.

## 4. The overlay

A single HUD (IMGUI or UI Toolkit, whichever is less code) with: simulated t, pace, alive,
births, deaths, absorptive and photosynthetic counts, mean depth, audit, `matterHere`, and the
identity check's state. Every number is read from the live `World`/`Ecosystem` through their
existing public counters (the run report's own columns are computed from them; find the
accessor `EvolutionRun` uses for each column and use the same one so the HUD and the table
agree by construction).

## Validation (before handing back)

1. `./scripts/core-test.ps1` green; Milestone 1 smoke unchanged.
2. Mode B on a short recorded run at dt 0.01 (make one with `run-arm.ps1`, 1,000 s, reference
   world, on a worker; it is the reference for identity): the HUD's identity check shows no
   mismatch through the whole run at 1× and after a seek to t=800.
3. Mode B refuses a run whose `simHash` differs, and plays it with the banner under the
   override.
4. Snapshot round trip: a snapshot written by the new build reads back with ids that join
   `lineage.jsonl` (every snapshot id present in lineage, born before the snapshot time, not
   dead before it).
5. Mode A: a jointed genome from any snapshot swims under its brain; the sine toggle visibly
   changes the gait; every listed sensor channel shows a finite value.
6. Wall-clock: report Mode B's pace at 1× and at seek on a 4,000-body world, with rendering,
   against the headless figure for the same run.

Hand back: the commit, the validation results, a screenshot or two saved under `scratch/`
(never outside the repository), and any place where the farm's output had to change beyond
the snapshot id. The logbook entry is Fable's, from your report.
