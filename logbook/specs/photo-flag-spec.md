# Spec: a photosynthetic flag on lineage birth rows

*Fable, 2026-09-06. Follows the Sol/GPT review of the same night and
`logbook/specs/review-2026-09-06-response.md` item 2: D063's producer clause, as the owner worded
it, asks for an inherited photosynthetic line alive at the end with a photosynthetic birth
in the last 20 samples, and `lineage.jsonl` carries no photosynthetic flag to read it from.
This is instrumentation only, in Core. Builder: read CLAUDE.md first. Worker `unity-w7`
only; workers 2–6 are running round 27. Never two Unity processes on one worker; no writes
outside the repo; no commits. Report to `logbook/specs/photo-flag-build-report.md`.*

## What to build

1. **`LineageEvent`** (`src/Evosim.Core/Ecosystem/LineageEvent.cs`): a `HasPhotosynthetic`
   birth-only property, carried through `Birth(...)` like `HasAbsorptive`, and written by
   `ToJson()` as `"pho": 0|1` immediately after `"jnt"` and before `"pt"`. Death rows are
   unchanged.
2. **`World`** (`src/Evosim.Core/Ecosystem/World.cs`, the birth site near line 1971): pass
   the `photosynthetic` boolean the same method already computes for
   `creature.HasPhotosyntheticTissue`, so the row and the creature can never disagree.
3. **Tests** (`src/Evosim.Core.Tests/LineageEventTests.cs`): the birth row carries `pho`;
   it equals the creature's `HasPhotosyntheticTissue` for a found reproduction birth, as
   the existing test cross-checks `abs`; a death row still has no `pho`; the row is one
   line and parses.
4. **Nothing else.** No `RunConfig` change (nothing enters the config hash), no report
   column, no per-step term. The queue is drained after the step, so the trajectory cannot
   change; the validation below proves it anyway.

## Validation the report must show

- Core suite green (`./scripts/core-test.ps1`), including the two reflection tests on the
  config.
- **Tiled transparency on the D078 build plus this change:** refresh worker 7
  (`pwsh -NoProfile -Command "./scripts/new-worker.ps1 -Workers @(7)"`), then
  `rounds/launch-r24.ps1 -Seed 2 -Worker 7 -Seconds 1000` against `runs/fl-replay`: 0
  differing fields (the same check the D078 build ran; the number must not move).
- **Shared identity:** one 3,000-s run of `rounds/launch-det.ps1 -Seed 4 -Worker 7
  -Seconds 3000 -DigestEvery 100 -Name pho-a`, compared with `scripts/digest-diff.py
  jobs-a pho-a`: identical on all digests (the D078 build's `jobs-a` is the reference;
  a lineage field cannot move the physics, and this is the proof).
- The first birth rows of `pho-a`'s `lineage.jsonl` show `"pho"` between `"jnt"` and
  `"pt"`, with both 0 and 1 present among founders.
- `scripts/tests/clade-score/run-tests.ps1` green (the scorer's fixtures already expect
  the field name `pho`), and `scripts/clade-score.ps1 pho-a` prints both producer readings
  rather than "flag absent".
- The manifest's `coreHash` changed (record old and new) and `simHash` did not, since no
  file under `Assets/Evosim` moved.

## Do not

- Touch `unity/Assets/Evosim` or the theatre.
- Change the field order of the existing birth fields; readers index by name, but the
  diff against an old row should be one inserted field.
