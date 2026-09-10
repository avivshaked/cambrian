# Build spec: destroy immediately, reserve the adult, check identity in Play mode (2026-09-10)

Repo d:\Projects\experiments\evolution-simulator. Read `CLAUDE.md` (the theatre paragraph, the
gotchas on simHash and on `Assets/Evosim` being simulation source), `logbook/0083`'s last
section (the replay parted at 200 s: batch destroys inside the step, Play mode defers to the
end of the frame, the theatre takes tens of steps per frame, a dead body stays as an undriven
collider and a newborn is placed inside it), `logbook/0082`'s Results (seed 2's three
divergences in a contact cluster; the placer reserves a newborn's spot at the radius it is born
with and it grows up to twentyfold in place), then `PhenotypeBuilder.cs` (~102-111
`PhenotypeInstance.Destroy`, ~653-661 `PrimitiveMesh` temporaries), `SeaFloor.Destroy`,
`Ecosystem.DestroyAll` (~1611), `SharedVolume.TryReserveOffspring` (the `child.Radius` used
for the free test), `Ecosystem` where the child's radius is computed for the placer and where
`ApplyGrowth` resizes, and `Assets/Theatre/Editor/TheatreIdentityCheck.cs` and the Theatre
runner.

Rules: nothing written outside the repo; no commit; never poll, sleep or wait; never run Unity
against `unity/` (the owner's Editor); do not run a Unity smoke at all (another agent is editing
`CurrentField`, `GridField`, `FluidEnvironment`, `Ecosystem`'s gather phase and `EvolutionRun`
right now and will smoke the tree when it is done; do not touch those regions); filtered Core
tests only if you touch Core (you should not need to); doc-comment register: why, with the
incident; no em-dashes.

## 1. Destroy immediately in either mode

`PhenotypeInstance.Destroy` chooses `Object.Destroy` when `Application.isPlaying` and
`DestroyImmediate` otherwise. Make it `DestroyImmediate` unconditionally, and the same in
`SeaFloor.Destroy`, the `PrimitiveMesh` temporaries, and `Ecosystem.DestroyAll`. Say in the
remark why: the farm and the theatre must run the same step, and a deferred destroy leaves a
collider in the world for the rest of the frame, which in the theatre is tens of steps. If any
call site relies on the deferred semantics (a component destroying its own GameObject from
inside a callback, for instance), say so and handle it.

## 2. Reserve the adult, not the newborn

The placer's free test uses the child's radius at birth. A child born at a twentieth of its
adult body grows into its neighbours in place. Reserve the child's spot at the radius of the
body it will grow into (its adult bounding radius, which the phenotype plan knows before
growth: `PhenotypeBuilder` builds the adult plan and scales it by `AppliedBodyFraction`; find
where the newborn's radius passes to `TryReserveOffspring` and pass the adult's). Keep the
occupant's radius updated on resize if the occupancy structure holds one (check
`SharedVolume`'s occupant records and `ApplyGrowth`). Say what a crowded world does to the
stillbirth count when the reservation grows, in the remark; it is a world rule change and
must be visible in the header: add a token only if a tunable is introduced, otherwise the
build hash carries it and you say so in the report.

Keep `OffspringDispersalMetres`'s floor at the two radii consistent with this: the floor is
parent radius plus the child's reserved (adult) radius.

## 3. A Play-mode identity check

`TheatreIdentityCheck` runs in edit mode and could not see the deferred-destroy fault. Add a
Play-mode variant that the same `-executeMethod` entry can run: enter Play mode from the
editor script (`EditorApplication.EnterPlaymode` with `EditorApplication.playModeStateChanged`,
or `EditorApplication.isPlaying = true` and a `[InitializeOnLoadMethod]`/`[RuntimeInitializeOnLoadMethod]`
hook that picks up the pending check from `EditorPrefs` or a file under `scratch/`), let the
Theatre runner step the run for N samples with the identity check on, then write the verdict
to the log and quit. Document the entry point in `CLAUDE.md`'s theatre paragraph (one
sentence) and make the edit-mode check say in its log line that it is the edit-mode check.

If a full Play-mode harness is more than a few hours of work, build the minimum that runs the
runner's own `Update` loop under Play and reports the identity verdict per sample, and say
what is left.

## 4. `CreatureIdMap` verification

The theatre's `CreatureIdMap` verification assumes tiled placement (per the reading on
2026-09-10). Read it, and make it correct for the shared box (ids map to bodies by organism
id, not by tile), or say precisely why it is already correct.

## Report

Diff hunks for `PhenotypeInstance.Destroy`, the reservation radius change, and the Play-mode
entry point; the diff stat; what you checked about deferred-destroy call sites; how the
Play-mode check is launched; anything that did not fit.
