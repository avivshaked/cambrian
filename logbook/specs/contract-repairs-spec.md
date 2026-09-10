# Contract repairs — implementation spec (after the Sol/GPT review of 2026-09-03)

Fable, 2026-09-04. Four small, independent repairs to the experiment contract, in one
build. Agent work; no world rule changes. Each item is default-preserving: a run with no
new settings must be step-for-step identical to today (the manifest is written beside the
run, never read by the simulation).

## 1. Invariant-culture formatting (review finding 7)

`CurrentField.ToString()` (`src/Evosim.Core/Environment/CurrentField.cs` ~L888–898) uses
interpolated `{x:0.###}` formatting with the process culture, so on a machine with a
comma decimal it emits `vent 0,1 m/s` and `VentTests.TheVentKnobsRefuseNonsenseAtTheSetter`
fails. Fix: format with `CultureInfo.InvariantCulture` (`FormattableString.Invariant($"…")`
or `ToString("0.###", CultureInfo.InvariantCulture)`). Then grep every `ToString()` and
`$"…{…:0.#…}"` in `src/Evosim.Core` that produces text which reaches a run header, a
report, JSON or a test assertion, and make each invariant. Add one test that runs
`CurrentField.ToString()` (and `RunConfig`'s header-facing descriptions if any) under
`CultureInfo("de-DE")` set on the current thread and asserts the `.` decimal — restore the
culture in a `finally`.

## 2. Producer counts in the report (review finding 2)

The report has `absorpt` / `inherit` for the stomachs and nothing for the leaves. Add, in
`EvolutionRun.cs`, appended after `abs logged` per the append-only rule: `photo` (living
creatures whose developed phenotype has ≥ 1 photosynthetic part) and `photo inh` (of
those, whose parent expressed photosynthetic tissue). `Organism` already caches
`HasPhotosyntheticTissue` (the absorptive-log build); parent expression follows how
`jointedInherited` / `absorptiveInherited` are computed today — find and mirror it. JSONL
fields `photosynthetic`, `photosyntheticInherited`. A Core test that a world of leaves
reports `photo == alive` and one with an inoculated pure stomach reports `photo == alive − 1`
for that sample (use the world-level counting function the report calls, not the report).

## 3. The run manifest (review finding 6)

Today `run.json` is written by `WriteRunIdentity` at orderly shutdown only; a killed arm
has none, and no run records the source that produced it. Replace with two writes:

- **At creation** of the run directory, before the first step: `run.json` with
  `arm`, `seed`, `unityVersion`, `physicsDtSeconds`, `metabolicStepSeconds`, requested
  `budgetSeconds` and `wallMinutes`, `configHash`, the inoculum identity if any, and a
  `source` block: `gitCommit` (from `git rev-parse HEAD` run in the repo root — the worker
  copies are outside the repo, so resolve the repo root from the script or an env var
  `EVOSIM_REPO_ROOT` that `run-arm.ps1` sets; if unavailable, write `"unknown"` and say why
  in a `note`), `gitDirty` (bool from `git status --porcelain`, code paths only), `coreHash`
  (SHA-256 over the sorted concatenation of every `.cs` under `src/Evosim.Core`),
  `simHash` (same over `unity-wN/Assets/Evosim/**/*.cs` of the worker actually running),
  `workerPath`, and `status: "running"`. Compute the hashes in the Editor script at
  startup (a few hundred files; negligible).
- **At termination** (orderly), rewrite the same file with everything above plus
  `status`, `endedAt`, `simulatedSeconds`, `reason` (`budget` / `wall` / `ceiling` /
  `extinct` / `error`), the existing footer facts. Rewrite atomically (write `run.json.tmp`,
  then move).
- **External stop:** `scripts/stop-arm.ps1 <arm> -Reason manual-futility|manual-stall|
  manual-other` — finds the worker's Unity process by command line, writes
  `status: "stopped"`, `reason`, `stoppedAt` into the run's `run.json` (merge, keep the
  creation fields), then kills the process. This is the only sanctioned way to stop an arm
  from now; document it in CLAUDE.md's commands.
- `run-arm.ps1`: after launch, poll for `run.json` (up to the compile wait) and print its
  `source.gitCommit` and `simHash`, and **refuse to launch** when a `-ExpectSimHash`
  argument is given and differs from the worker's hash (the four-file manual check becomes
  a fallback). Keep `-ExpectSimHash` optional.

Core has no git or filesystem-walk concern: the manifest is Editor-side (`Evosim.Sim.Editor`),
serialised with the existing hand-written `Json` writer, no new dependency.

## 4. The physics step in the config hash (DESIGN.md §6.2's queued item)

`RunConfig.Hash()` must cover the physics step. Cleanest: a `[Tunable("physics",
Unit="s")] PhysicsStepSeconds` property on `RunConfig` (default 0.01, validated to divide
0.5 exactly at the setter, the same rule `Ecosystem.ConfigurePhysicsStep` enforces), set by
`EvolutionRun` from `EVOSIM_DT` before `Hash()` is taken, and `Ecosystem.ConfigurePhysicsStep`
called from it. The two reflection tests then cover it for free. Consequence to state in
the commit: every config hash changes again (a new tunable), so headers compare token by
token across this boundary as they already do across D070's.

## Verify before handing back

`./scripts/core-test.ps1` green (count reported); a Unity compile check on a refreshed idle
worker with zero `error CS`; a 300-s validation arm through `run-arm.ps1` on that worker:
`run.json` exists within the first minute with `status: "running"`, a real commit hash, a
64-hex `simHash` equal to what `run-arm.ps1` printed, then at the end `status: "ended"`,
`reason: "budget"`; header carries `dt=0.01`; `photo` column present and equal to `alive`
minus `absorpt` at a sample with no mixotrophs; then a second 300-s arm stopped at ~150 s
with `stop-arm.ps1 -Reason manual-other` whose `run.json` reads `status: "stopped"`,
`reason: "manual-other"` with the creation fields intact.
