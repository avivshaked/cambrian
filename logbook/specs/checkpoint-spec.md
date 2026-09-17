# Checkpoints: the whole state of a run, written every thousand seconds, restored in the theatre

**2026-09-15**  ·  the second of the three tools of `video-tools-notes.md`.

> "One thing we don't have is the ability to take world snapshots and replay them, which
> means it takes a long time to get to any interesting point in the recording"

It is a recording feature of the farm and a restore in Core and the theatre. It moves
neither `configHash` nor a trajectory, so a run with checkpoints is the same run without
them. The owner accepted the labelled cousin ("Yes a labeled cousin is acceptable"). This
goes in front of the owner before building, and it is built after the timeline.

## What it is and is not

A checkpoint carries everything the simulation holds at one sample: the Core world to the
bit, and every part's pose and motion. Restored, it puts the same bodies in the same places
over the same fields, with the same genomes, reserves, ages and brains. The random
generators stand at the same point in their sequences.

It cannot carry PhysX's own solver state, which is the contact caches and the order touching
bodies are solved in. No API returns that state, and this build's trajectory depends on it
at the level of an ulp (logbook/0069). So the step after a restore is solved in a different
order, and the world parts from its recording within minutes, as the multithreaded runs did.
A restored world is a cousin from the restore onward, and the theatre says so.

The identity check already compares five scalars against `stats.jsonl` at every sample, and
it is the judge of how long the cousin agrees. What is labelled is the restore rather than
the verdict.

## What a checkpoint carries

This comes from the agent's survey of 2026-09-15, over `World`, `Organism`, the fields,
`Ecosystem` and `SharedVolume`. Everything that is a pure function of the config, the seed
and the clock is left out and rebuilt. That is `CurrentField`, whose every array is a
coefficient or a memo keyed on `t`, and `LightField`, which is rebuilt every step. It is
also the phenotypes, developed from the genome and the body fraction, the placer's geometry
and the bed.

1. **The world's scalars**: `ElapsedSeconds`, `_nextId`, `_nextIndex` and `_nextSpeciesId`.
   With them goes every cumulative counter the audits and the report read: `Births`,
   `Deaths`, `Diverged`, `EnergyIn`, `EnergyOut` and `MatterInBodies`. So do the influx,
   burial, deposit, take and exudation totals, the blocked and short conceptions, the
   stillbirths, `SecondsSinceFloorFired`, `FloorSpawns` and `Inoculated`. The second of
   those counters is where every per-draw seed comes from, and it is the one number a wrong
   restore would ruin with the least sign of it.

2. **The generators that persist**: `_conceptionRng`, each `VertexField`'s `_rng` and
   `_bank` in a vertex world, and `SharedVolume`'s placement stream (`World.PlacementIndex`,
   Unity side). The conception generator is drawn from only under
   `ConceptionOrder.Shuffled`, and carried always. A generator is written as its `_state`
   and `_increment`, plus the spare Gaussian flag and value where the stream draws
   Gaussians, as the vertex field's does.

3. **The species registry** (`_species`: founding genome and second per id; empty in every
   run to date) and the **corpses** (`CreatureId`, `Position`, `Patch`, `Joules`,
   `Matter`, `AgeSeconds`; empty at decay 0).

4. **Every living organism**: `Id`, `ParentId`, `GenerationDepth`, `BirthSeed`, and the
   genome as `GenomeJson` writes it, format 5. Then `BodyFraction`, `Energy`, `Age`,
   `HeightY`, `BirthHeightY`, `PendingWorkJoules`, `TissueJoules`, `LockedMatter`,
   `Patch`, `X`, `Z`, `SpeciesId`, `Children`, `LastChildSeconds`, the `Lifetime` ledger,
   `LastLedger`, `LastStepSeconds`, `LastDensityHere`, `LastShare` and `StandingWatts`.

5. **The fields' stock**: `GridField._stock` for the detritus grid and the matter grid.
   That is one double per cell of the bounding box, dead cells included, so that the
   array's shape is the config's. In the older model it is a `NutrientField`'s `_stock`
   list, and in a vertex world a `VertexField`'s `_x`, `_y`, `_z`, `_m` and `_alive` lists.

6. **The queues between drains**: `_lineageEvents` and the absorptive log's pending rows.
   They are empty when the checkpoint is written where the sample is (below), and they are
   carried anyway, so that the schema does not depend on the cadence.

7. **Every body's physics and brain** (`Ecosystem.Body`). Per part, that is position,
   rotation, linear and angular velocity, the joint's reduced-space position and velocity
   (`ArticulationBody.jointPosition`, `jointVelocity`), and the drive's last target as
   `EffectorDriver` applied it. Per body, it is the brain's `_previous`, `_current` and
   `_memory` arrays and its `ElapsedSeconds`, `WorkAtLastStep`, `PreviousCentre`,
   `PreviousRoot`, `Settled`, `Tile`, `Radius`, `AppliedBodyFraction` and
   `LastRootPosition`. Not the throw trace, which is diagnostic, nor the sensors, which are
   rebuilt each step.

8. **The placer's reservations** (`SharedVolume`: every reserved spot with its radius and
   owner), the tile allocator's free list in a tiled world.

9. **A header**: the run's `configHash`, `simHash`, `coreHash`, the seed, the second, the
   sample index, and the five identity scalars of the `stats.jsonl` row it sits on. It also
   holds the state digest, `EVOSIM_DIGEST_EVERY`'s digest of the Core world at that second,
   and that is what a restore is checked against.

## Format

One file per checkpoint under `runs/<arm>/<run>/checkpoints/`, named by the second as
the snapshots are (`000015000.json.gz`). Hand-written JSON through `Json`, as every file
here is, gzipped through `System.IO.Compression.GZipStream`, which `netstandard2.1` has,
so Core stays without a dependency.

Every number is written so that it reads back to the bit. Doubles and floats take the
round-trip format `Json.Writer` already uses for `auditResidual`, and the restore test below
is what proves it. If a platform's `R` fails the test, the fallback is the IEEE bits in hex
for that type, decided by the test and not in advance. A genome is written inline. The
by-reference form holds the lineage id, and the genome only for bodies born since the last
snapshot; it is a second pass if the size matters.

The size at round 38's scale comes from the estimate the owner was given. Genomes take
about 8 MB and organism state about 1.5 MB. Physics and brains take about 0.3 MB, the two
grids about 1 MB, and the rest under 0.1 MB. That is 10 to 12 MB raw, about 2 MB gzipped, thirty
per 30,000 s run. At round 39's box (53 × 45 × 53 m at 1 m cells) the detritus grid alone is
126,000 cells. So the grids there are about 1 MB and the total near 15 MB raw. The write
pauses the loop for well under a second, since the genome snapshot already walks the same
list every 1,000 s.

## When it is written

A checkpoint is written every `EVOSIM_CHECKPOINT_EVERY` seconds. The default of 0 is off, a
launcher that wants them says so, and 1,000 is the round's value.

It is written in `EvolutionRun`'s loop at the point the genome snapshot is written, after
the report row and `stats.jsonl`'s row for that second. So a checkpoint always sits on a
sample, and the identity check has a row to compare the restored world against before any
step is taken. One is written at the run's orderly end as well. The manifest records
`checkpointEverySeconds` and the count written. It is a recording, like `positions.jsonl`:
it reads the world and changes nothing, and `configHash` does not know it exists.

## Restore

10. **In Core**: `World.Restore(RunConfig config, ulong seed, CheckpointReader reader)`
    builds a world from the config and the seed as the constructor does. The current, the
    bed, the placer's geometry and the field shapes are therefore the run's, and it then
    sets every field of items 1 to 6 from the file. It lives inside `Evosim.Core`, where the
    `internal` setters are reachable, so nothing about the assembly's visibility changes and
    the Unity side never assigns an organism's reserve. A restored world with a config hash
    other than the file's is refused.

11. **In the harness**: `Ecosystem` gains a constructor that takes a restored `World` and
    the checkpoint's body section. It builds each body from its phenotype through
    `PhenotypeBuilder` as a birth does. Then it applies item 7's poses, velocities, joint
    state and drive targets, the brain's arrays and the placer's reservations. The first physics
    step after a restore runs with every body awake.

12. **In the theatre**, a seek in World mode works from the checkpoints. A seek to a second
    at or past the earliest checkpoint restores from the latest checkpoint at or before the
    target, and steps forward from there. A seek to a second before the first checkpoint replays from zero as now, and
    backwards seeks work the same way. The strip's provenance reads **cousin** from the
    restore on, and the popover says "restored from the checkpoint at N s; the identity
    check has matched k of m samples since". The identity check runs as it does today.

    A run without checkpoints behaves as it does now. `theatre-snap.ps1` and the safari use
    the same seek, so a picture at 25,000 s costs the replay of at most 1,000 s.

13. **In the farm** (second pass, not required for the videos): `EVOSIM_RESUME=<file>`
    starts an arm from a checkpoint as a new run directory, for forks and what-ifs. Its
    manifest carries `resumedFrom` with the source arm, second and hashes. A resumed
    run is never a round's arm, and the scorer refuses a manifest with `resumedFrom`.

## Validation

- **Core round trip, exact**: in a Core-only test, build a world at the campaign's config
  and step it 2,000 s with a placer stub. Checkpoint it, restore into a fresh world, and
  compare the two state digests, which must be equal. Then step both a further 1,000 s
  and compare again, which must still be equal, since Core without PhysX is
  deterministic. A vertex-field world and a grid world both. This is the test that
  decides the number format.

- **The harness restore, exact at the second**: a 600 s smoke recorded with checkpoints
  every 300 s. The theatre restores at 300 s and, before stepping, the five identity
  scalars equal the row's and the Core digest equals the header's. Then it steps to
  600 s and the entry records where the cousin parted, the first mismatched sample, as
  the multithreaded runs' record does. Ten restores of the same checkpoint must agree
  with each other to the sample. That says the restore is deterministic even where the
  physics is not the recording's.

- **The cost**: the smoke's pace with checkpoints on against off, within noise; the file
  sizes against the estimate above.

- **Nothing moves**: `configHash` and the state digest of a run with checkpoints on equal
  those of the same seed with them off. That is the 600 s box digest pair, as the bed's was.

## Not in this pass

Genomes by reference, and resume in the farm (item 13), wait for a second pass. A
checkpoint of a tiled world is refused with a message, since the shared world is the only
one the theatre replays. Restoring into a build with a different `simHash` is refused as a
replay is; `AllowSourceMismatch` opens it as a cousin of a cousin, and the popover says
both.

## Cost

The whole is two to three days: Core's writer and reader with the round-trip test in a day,
the harness side and the theatre's seek in a day. The smoke and its record are the third.
Core in tests;
the smoke on a worker between rounds or on the owner's Editor with a graphics device.
