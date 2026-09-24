# A smaller record, and films the farm moves

*Build spec. 2026-09-24. For round 49's build; nothing here reaches round 48, whose seeds run
on a fixed exe.*

## Why

Two numbers from one afternoon.

**One seed of round 47 is 6.0 GB, and three quarters of it repeats.** `runs/r47-s2`'s run
directory holds 4.5 GB of snapshots, 622 MB of `poses.jsonl`, 507 MB of checkpoints, 282 MB of
`positions.jsonl`, 93 MB of field dumps and about 35 MB of everything else. A snapshot is every
living body's whole genome, every 100 s. A genome does not change in a body's life, and 3,138
of the 3,301 bodies in the snapshot at 15,000 s were already in the one at 14,900 s. So about
95% of the snapshot bytes are copies. A run of 300,000 s at this crowd would be 60 GB a seed
before the crowd grows.

**A film re-runs the world inside Unity, slowly, and what it films is a cousin.** The safari
reaches each scene's second by stepping the live world in the Editor. On 2026-09-24 scene 7 of
round 47 seed 2 fell at 2,240 s, before the first checkpoint at 2,500 s, and the Editor stepped
916 s of world in 20.5 minutes (0.74x real time, beside two farm runs). The Editor runs the
farm's code on Mono, and Mono and .NET round a double sum differently, so the world on screen
parts from the recorded run at its first sample (CLAUDE.md, "Mono and RyuJIT do not agree"). The
farm replays its own run bit for bit from a checkpoint, and at 16 threads round 42's world ran
30,000 s in 24 minutes.

The owner's rulings of that afternoon: checkpoints every 500 s from round 48 seed 3 on; a
record that stays small ("perhaps we should start considering binary storage"); and safari films
soon after an arm ends, or during it, at a scale of 300,000 s runs.

## What already exists

- **The state stream** (`logbook/specs/state-stream-spec.md`): `poses.bin` and `poses.idx`,
  binary, little-endian, one length-framed frame per instant carrying every body's id, root
  position and rotation, **body fraction** and joint coordinates, with a trailer that makes a
  killed run readable. It is written by `PoseRecorder` through `PoseStreamWriter`
  (`src/Evosim.Farm/PoseStream.cs`) when `EVOSIM_POSE_EVERY` is above 0, and is off in every
  launcher. `scripts/poses-read.py` reads it independently of the C#.
- **Drawing a frame from the stream**: `SnapshotWorld.BeginFromStream`
  (`unity/Assets/Theatre/SnapshotWorld.cs`) develops each body from its snapshot genome, sizes it
  by the stream's body fraction, and poses it by forward kinematics from the root pose and the
  joint coordinates (`RecordedPoses.Apply`, `RecordedPose.cs`). A film played from a stream is
  this, thirty times a second.
- **Checkpoints**: binary already (`Checkpoint.cs`), built in memory, digested, written to a
  `.tmp` and renamed. Gzip at level 6 takes the 43.1 MB checkpoint at 15,000 s of `r47-s2` to
  11.7 MB (3.7x) in 0.6 s.

Two facts bite the design. `ResolvePoseEvery` raises a cadence under the half-second metabolic
step to it, on the premise that nothing about a body changes between metabolic steps; its pose
changes every physics step (dt 0.01 s), so a film window needs sampling below that bound. And the
stream carries no guild flags, which `positions.jsonl` does.

## Part A: the record

A1. **Genomes once.** `genomes.jsonl.gz` holds one row per body, written when the body is
admitted (founder, birth, inoculant, trickle or pool founder), drained with the lineage at each
report. The row is exactly `GenomeJson.Write(genome, indent: false, id)` as today, without
`moduleCounts` and `lostPaths`. The file is gzip in members: each drain writes one complete gzip
member, and concatenated members are one valid gzip file. A killed run leaves every complete
member readable. A reader stops at a torn last member and says so, and never parses it.

A2. **Snapshots keep their cadence and lose the genome.** `snapshots/NNNNNNNNN.jsonl.gz` rows
become `{"id", "moduleCounts"?, "lostPaths"?, "bf"}`: what changes in a life, plus the body
fraction, which no snapshot carried before. The genome is joined from A1 by id. A reader that
finds a slim row and no genome for its id refuses the row and counts it, as `WithoutAGenome`
does today.

A3. **`positions.jsonl` becomes `positions.jsonl.gz`**, same rows, members per report.

A4. **`poses.jsonl` is retired for the stream.** New launchers set `EVOSIM_POSE_EVERY` to the
report interval (10 s), so the stream carries what the JSON did, plus the body fraction.
`PoseStream` version 2 deflates each frame's payload (the frame header and trailer stay
uncompressed, so the index and the torn-frame rule are unchanged) and adds one flags byte per
body (absorptive 1, jointed 2, photosynthetic 4), so a reader of the stream needs no second
file for guilds. Version 1 streams stay readable; the version field says which.

A5. **Checkpoints are compressed.** `Checkpoint.Version` 5 gzips the payload after digesting it.
The digest stays over the uncompressed payload, so the verification is unchanged. Version 4
files stay readable, and the version field says which, so no reader guesses from the bytes.
`WorldState.StateVersion` does not move: the world's layout is untouched.

A6. **One reader per language.** `scripts/reads/runrec.py` becomes the one way a Python script
reads a run: `genomes(run)`, `snapshot(run, second)` (rows with the genome joined, so an old
reader's row dict is unchanged), `positions(run)`, `poses(run, second=None)`, `checkpoints(run)`.
Each reads both the old and the new record and says which it read. Every script that reads a
snapshot, `positions.jsonl` or `poses.jsonl` is moved onto it: `guide.py`,
`positions-read.py`, `field-map.py`, `poses-read.py` (its format checks stay independent of the
C#), and under `scripts/reads/`: `r48-read.py`, `tilt.py`, `clumping.py`, `stomachs.py`,
`safari-joints.py`, `snow-read.py`, `snow-timeline.py`, `r46-tilt-age.py`. The closed rounds'
reads (`r41d-read.py` to `r47-read.py`) read closed runs and are left alone. On the C# side
`SnapshotWorld` (`ReadGenomes`, `PositionsRowAt`, `BeginFromStream`), `RecordedPose.cs` and the
Core readers take both.

A7. **A converter for the runs on disk.** `scripts/record-convert.py <run dir>` writes the new
files beside the old ones (genomes once, slim snapshot rows, gzipped positions, the JSON poses
as a version 2 stream without body fractions) and checks, row for row, that `runrec.py` reads
the same thing from both. It deletes nothing. Removing the old files after a check is the
owner's decision, run by run.

The expected size of a seed like `r47-s2` under Part A, from the measurements above: genomes
about 35 MB (46,665 births at 4.9 KB, about 6.6x under gzip), slim snapshots a few MB, positions
about 60 MB and poses about 150 MB (both estimates until a converted run is measured),
checkpoints at 500 s about 0.7 GB (60 at about 12 MB), field dumps 93 MB. About 1 GB a seed where
it was 6 GB, and the checkpoints are then most of it.

The trajectory does not move. Every writer change is in `src/Evosim.Farm` and in Core's
serialisers, recording only, so the regress on the crowd fixture (`runs/r48fix-s4`) must read
identical in every `stats.jsonl` field, and the digest at 1 and 16 threads must be unchanged.

## Part B: films the farm moves and Unity draws

The division of labour, which the owner asked about ("we will not use Unity to render?"): Unity
draws every pixel as it does now, with the same skin, light, cameras, depth of field and grade.
What moves is the world. The farm, which replays its own run exactly, writes where every body is
thirty times a second for the stretch a scene needs, and Unity plays that back the way it already
draws a snapshot. The Editor's live mode stays as it is, for exploring.

B1. **`Evosim.Farm.exe --film-window <run dir> <from s> <to s> <out dir> [--fps 30]
[--threads N]`.** It restores the checkpoint at or before `from`. A hash mismatch is refused as
a resume refuses it, unless `EVOSIM_ALLOW_SOURCE_MISMATCH` is set, and the window then says
cousin. It steps to `from` writing nothing. From `from` to `to` it writes into `out dir`, never
into the run directory:
- `film.poses.bin`, a version 2 stream with a frame at the first physics step at or after each
  `k / fps` second. The frame's recorded second is the step's own, so frames sit up to half a
  physics step off the nominal clock, and the header records the nominal rate.
- `genomes.jsonl.gz` for every body alive at `from` or born before `to`, with the plans
  (`moduleCounts`, `lostPaths`) at `from` and every change to one inside the window as
  `plans.jsonl`.
- `events.jsonl`, every birth (with parent) and death (with cause) inside the window.
- `identity.jsonl`: at every report second inside the window, `alive`, `births`, `deaths`,
  `auditResidual` and `meanHeight` against the run's own `stats.jsonl` row. A window whose rows
  all agree is **faithful**, and the film's provenance word says so; one that parts is a cousin
  and says where it parted.

The frame sampling runs below the metabolic step, which `ResolvePoseEvery`'s bound forbids for
the run's own stream; the window takes its own path and leaves that bound where it is.

B2. **Playback in the theatre.** A `StreamWorld` in `Evosim.Theatre` reads a window and gives
the cameras the same bodies the live world does. A body appears at its birth and goes at its
death. It takes its plan from `plans.jsonl`, its size from the frame's body fraction and its pose
from `RecordedPoses.Apply`, and it is rebuilt only when its fraction or plan changes. Nothing in it steps
physics. The film tool and the safari take it with `-FromFarm`.

B3. **The safari on windows.** The director plans each scene from the guide and the checkpoint
list as it does now, then asks for each scene's window: from the scene's second, less the take's
lead, to its end. A birth scene asks for a window long enough to hold the birth the guide
predicts and reads the birth from `events.jsonl`, where today the Editor rehearses it by
re-running. The windows are independent, so they are written in parallel, several farm
processes at a few threads each, before Unity opens. Unity then films every scene from its
window without stepping anything.

B4. **During the run, later.** Once B1 is measured, the farm can write a short window at each
checkpoint as it runs, so a safari needs no farm time at all after the arm ends. Its disk cost
at the campaign's crowd has to be measured before it is proposed.

Acceptance for Part B has three parts. A window of `r48fix-s4` reads faithful at every report
second inside it. The playback's frame at a stream second draws the same parts as `-From
snapshot` does from the run's own stream at that second, to a millimetre and a tenth of a degree.
And the wall time of a 60 s window is recorded at 5, 10 and 16 threads on a machine with nothing
else running.

## What waits for the round gap

Round 48 holds two farm runs until its third seed ends, and the machine's rule is no test suite
beside two farm runs. The build, the compile and the fixtures can be written now. The suites, the
regress, the digests and every timing are run at the gap, or beside one farm run with nothing
else, and a timing taken beside anything is taken again.
