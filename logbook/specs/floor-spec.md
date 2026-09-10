# Build spec: a real sea bed (D077's placeholder floor made real)

Fable-written, 2026-09-06, for an implementing agent after the owner's ruling. Read
CLAUDE.md, DECISIONS.md D077 (the rule-4 amendment names the mirror below the floor "a
placeholder for a real sea bed"), logbook/0065 (three newborn divergences at 60 m in
`r25q-s2`, `runs/r25q-s2/*/diverged/*.json`), and `unity/Assets/Evosim/Sim/SharedVolume.cs`
and `FluidEnvironment.cs` as built.

## What changes (shared space only; the tiled branch untouched to the character)

1. **The floor is a collider.** When `SharedSpace` is on, the scene gets one static
   `BoxCollider` (or a plane) at y = −D spanning the box with margin past the seams, on the
   creatures' layer, with the default physics material (no bounce, ordinary friction). The
   restoring mirror below y = −D is removed for the shared branch (the tunable's remark
   updated: the fraction now governs the surface only). Above the surface nothing changes.
2. **Nothing is placed in the floor.** Founders, inoculants and newborns are placed with
   their bounding sphere entirely above y = −D + margin (margin = the radius, plus 0.05 m);
   a parent on the floor places its child beside it, not below it. The `FounderDepthMetres`
   draw is clamped the same way.
3. **Contacts with the floor are not creature contacts.** The `contacts` column counts
   creature–creature pairs only; add `floor contacts` (pairs per step against the floor) so
   a benthic crowd is visible.
4. **The theatre draws the floor** as a filled quad, not a wire line, when shared.

## Validation

1. Core tests green (no Core change expected; if `IBodyPlacement` changes, its tests).
2. Replay identity at the default (tiled): a 2,000-s replay of any 0.01 recording on the
   current build, rows byte-identical modulo appended columns.
3. `SharedSpaceSmoke` extended: 200 bodies placed, none intersecting the floor; 2,000
   steps with 20 bodies pushed under the floor by hand — all resting on or above it after
   200 steps, none below −D − radius; no non-finite body.
4. `fl-smoke`: `r25q-s2`'s exact settings for 3,000 s at 0.02 (`rounds/launch-r25.ps1
   -Seed 2 -MatterInitial 0.25 -Seconds 3000`): `diverged` 0, `below` 0 throughout, `max
   m/s` in the first 100 s no longer a floor bounce (report it), floor contacts reported,
   audit 0.0000%.
5. Wall clock against the same settings on the previous build.

Constraints: worker 6, one Unity process at a time, nothing outside the repository,
commit on `main`, do not push. Hand back `logbook/specs/floor-build-report.md`.
