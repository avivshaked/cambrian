# Spec: the state digest — find the step where two identical runs stop being identical

*Fable, 2026-09-06. Diagnostic instrument, agent work, no world rule. Builder: read
CLAUDE.md first (worker rules, no writes outside the repo, `scratch/` for transient files,
never two Unity processes on one worker, never `-batchmode` against a project the Editor has
open). Report to `logbook/specs/digest-build-report.md`.*

## Why

The shared-space world (`EVOSIM_SHARED_SPACE 1`) does not replay. Six runs of one
seed/config/build on one worker (`runs/det0-a`, `det0-b`, `det1-a`, `det1-b`, `r26d-s4`,
`r27-s4`; `scripts/compare-det.py` compares any two) are identical through t≈1,400–1,500 s
(140,000+ physics steps at dt 0.01, with creature–creature contacts present since t≈500)
and then each diverges into its own realisation. Unity's *Enhanced Determinism* does not
change this (`det1-*`). The tiled world replayed bit for bit on the same build (`fl-replay`,
`fp-replay3`). A read-only audit found no nondeterminism in the C# (no clocks, no random,
no reference-keyed hash iteration, no physics queries, a `Parallel.For` whose iterations
write disjoint slots). The stats rows are sampled every 100 s, so they cannot say which
step or which body diverged first. This instrument can.

## What to build

1. **A per-step digest**, gated by `EVOSIM_DIGEST_EVERY` (integer, physics steps between
   digests; default 0 = off, so every existing run and its hash-compatible replay are
   untouched). When on, after every N-th `Physics.Simulate` in `Ecosystem` (the call at the
   step loop, `Ecosystem.cs` ~line 562) — and always on the very first step — write one
   row to `runs/<arm>/<run>/digest.jsonl` through `JsonlWriter` (flush each row):
   `{ "step": n, "t": seconds, "bodies": count, "hash": "<16 hex>", "first": <id or -1> }`
   where `hash` is FNV-1a 64 over the raw bits (`BitConverter`) of, for each living
   creature **in `World.Living` order**, its id (long) then its root `ArticulationBody`'s
   position (3 floats), rotation (4 floats), linear velocity (3) and angular velocity (3),
   then for each non-root link in the creature's part order the same ten floats. Read the
   values through the `ArticulationBody`, not `transform` (`AutoSyncTransforms` is off).
   Add nothing to the stats row and nothing to the header — the run's `config.json` and hash
   must not change (`EVOSIM_DIGEST_EVERY` is a runner setting like the wall budget, read in
   `EvolutionRun`, not a `RunConfig` tunable; `RunConfigTests` must stay green).
2. **A per-body dump on request**, gated by `EVOSIM_DIGEST_DUMP_STEPS` (a comma list of step
   numbers; default empty): at each listed step, after the digest row, write one row per
   living creature to `runs/<arm>/<run>/digest-bodies.jsonl`: `{ "step", "id", "root":
   [10 floats as above], "links": [[10 floats], ...], "sleeping": bool, "contacts": n }`
   where `sleeping` is the root's `ArticulationBody.IsSleeping()` (or the nearest available
   query) and `contacts` may be 0 if there is no cheap per-body contact count. Floats written
   with `R` / round-trip formatting so two files can be diffed to the bit.
3. **`scripts/digest-diff.py`**: given two arms, print the first step whose `hash`
   differs and the last identical one; if both have `digest-bodies.jsonl` rows at a common
   step, print the first creature id (in file order) whose row differs and which of its
   values differ (root or which link, which component, both values).
4. **`rounds/launch-det.ps1`** already exists (0068's world, seed 4, 3,000 s, worker 7,
   5.4 wall-minutes per run at this population). Add `-DigestEvery` and `-DigestDump`
   pass-throughs that set the two env vars.

## Validation the report must show

- `RunConfigTests`, `RunConfigJsonTests` and the whole Core suite green (`core-test.ps1`).
- A tiled 1,000-s replay (`rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000`, dt
  0.01, digest off) whose stats rows are identical to `runs/fl-replay`'s over the shared
  samples (`scripts/reads/compare-rows-by-name.py` or `compare-det.py`) — the gate is off, the
  world is unchanged. Note the new `simHash` in the report; it changes because a `.cs`
  under `Assets/Evosim` changed, and that is expected.
- Two shared runs on worker 7 with `-DigestEvery 100` (one digest per metabolic step):
  `det3-a` and `det3-b`, 3,000 s. Run them **one after the other, never together**. Report
  the first differing digest step from `digest-diff.py`. Then two more (`det3-c`, `det3-d`)
  with `-DigestDump` at the last identical step and the first differing one (so the dump
  exists on both sides), and report the first creature and the first differing value.
- The digest's own cost: wall minutes of `det3-a` against `det0-a`'s 5.4.

## Do not

- Change any per-step physics or metabolic term, any default, any header token, or the
  order of anything (the instrument reads; it must not act).
- Touch the workers other than `unity-w7` (workers 2–6 are running round 27). Refresh
  worker 7 with `pwsh -NoProfile -Command "./scripts/new-worker.ps1 -Workers @(7)"` after
  the code lands in the main tree; `unity-w7/ProjectSettings/DynamicsManager.asset` currently
  carries probe settings (enhanced determinism 1, sleep threshold 0, SAP broadphase, scratch
  64) — `new-worker.ps1` copies `ProjectSettings/` from the main tree and will reset them,
  which is what you want for this validation (the main tree's settings are the round's).
- Commit or push (I will, after reading the report).
