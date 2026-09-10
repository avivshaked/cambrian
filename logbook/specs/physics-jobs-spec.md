# Spec: single-threaded physics by default — the shared world made reproducible

*Fable, 2026-09-06. Follows logbook/0069: the shared world replays bit for bit only when
Unity's job system has no worker threads (`-job-worker-count 0`, `det5-a` ≡ `det5-b` over
300,000 steps), and parts within ~148,000 steps at the default fifteen. Builder: read
CLAUDE.md first, then `logbook/specs/digest-spec.md` and `logbook/specs/digest-build-report.md` (the
digest is the validation instrument here). Report to `logbook/specs/physics-jobs-build-report.md`.
Worker `unity-w7` only; workers 2–6 are running round 27. Never two Unity processes on one
worker; never `-batchmode` against the main `unity/` project; no writes outside the repo;
no commits.*

## What to build

1. **A runner setting, not a tunable.** `EVOSIM_PHYSICS_JOBS` (integer, **default 0**) read
   in `EvolutionRun` beside the wall budget and the digest settings. At run start, before
   the world is built, set `Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount` to that
   value (clamped to `JobWorkerMaximumCount`; note in the report if 0 is refused and what
   the nearest accepted value replays as). Read the property back and record **the value
   read back** and `JobWorkerMaximumCount` in `run.json` (`physicsJobWorkers`,
   `jobWorkerMaximum`) and as a header token, `physics jobs 0` (the header is what a
   launch is verified against, CLAUDE.md). Not a `RunConfig` property: it must not enter
   `config.json` or its hash (every stored config would become unreadable under §9), and
   `RunConfigTests` / `RunConfigJsonTests` must stay green.
2. **The theatre.** `TheatreRunner`'s World mode sets the same property from the run's
   manifest before it rebuilds the world; a manifest without the field (every run before
   this build) is treated as the old default and the overlay says so in the same line that
   reports a source mismatch ("physics threads unrecorded — not a faithful replay after the
   first contact fork"). Solo mode: set 0 as well (one body; harmless; consistent).
3. **`scripts/run-arm.ps1`**: print the value from the manifest with the other identity
   lines (`physicsJobWorkers`), as it prints `simHash`.

## Validation the report must show

- Core suite green (`./scripts/core-test.ps1`).
- **Tiled transparency:** `rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000` (dt 0.01,
  tiled, no env override) against `runs/fl-replay`: 0 differing fields. The tiled world never
  depended on the thread count, so the new default must not move it; if it does, say so
  and stop — that is a finding, not a fix.
- **Shared identity at the default:** two runs of `rounds/launch-det.ps1 -Seed 4 -Worker 7
  -Seconds 3000 -DigestEvery 100 -Name jobs-a` / `jobs-b` with **no** `-UnityArgs` (the
  code's default is now what makes them replay): `scripts/digest-diff.py jobs-a jobs-b`
  identical over all 3,001 digests; `scripts/compare-det.py` identical on all 30 samples.
  Both headers carry `physics jobs 0`; both manifests carry `physicsJobWorkers: 0`.
- **The old behaviour is still reachable:** `EVOSIM_PHYSICS_JOBS=15` on one 3,000-s run;
  its manifest reads 15 and its digest parts from `jobs-a` somewhere (report the step).
- **The theatre replays a shared run:** record `runs/th-shared` (the `jobs-a` settings,
  3,000 s) and run `Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run` against it headless
  as CLAUDE.md shows (`EVOSIM_THEATRE_RUN`); report "N of N samples" — this is the first
  time the theatre can be faithful to a shared-world run, and the report should say whether
  it is. The theatre must not write into the run directory.
- Wall minutes of `jobs-a` against `det5-a`'s 6.42 and `det0-a`'s 5.39.

## Do not

- Touch any per-step term, default or header token other than the one added.
- Change `src/Evosim.Core` (round 27's `coreHash` must stay `52eb6496…`).
- Reset worker 7's `ProjectSettings` by hand: `new-worker.ps1 -Workers @(7)` from pwsh
  refreshes it from the main tree after the code lands.
