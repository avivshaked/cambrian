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

`EVOSIM_RECORD_FORMAT` chooses which record a run writes: 2, the default from round 49's build,
or 1, which every earlier run wrote. It is a recording setting and moves no hash, and the
manifest's `recordFormat` says which record a directory holds. A resumed run takes its
source's record unless its launcher names one, as it takes the source's cadences. It reads the
source's manifest, and a manifest without the field means format 1.

A1. **Genomes once.** `genomes.jsonl.gz` holds one row per body, written when the body is
admitted (founder, birth, inoculant, trickle or pool founder). The row is exactly
`GenomeJson.Write(genome, indent: false, id)` as today, without `moduleCounts` and `lostPaths`.

The genomes come from a queue in the world. `World.QueueAdmittedGenomes` is off by default, and
the console farm turns it on. Each admission then queues a reference to the body's genome beside
its lineage row, one per id. The Unity farm never drains the queue, so there it stays off rather
than holding every genome ever admitted. The farm drains it wherever it drains the lineage: at
each report row, before each checkpoint and at the run's end. A resumed run writes its living
roster's genomes as its first member, because those bodies were admitted in the run it
continues. Each directory then reads on its own.

The file is gzip in members. Each drain writes one complete gzip member, and concatenated
members are one valid gzip file. Each member also carries its own length in an extra header
field (RFC 1952's FEXTRA, subfield `EV`, eight bytes), which standard readers skip. So a reader
knows a torn last member before it inflates a byte. A killed run leaves every complete member
readable. A reader stops at a torn last member and says so, and never parses it.

A2. **Snapshots keep their cadence and lose the genome.** `snapshots/NNNNNNNNN.jsonl.gz` rows
become `{"id", "moduleCounts"?, "lostPaths"?, "bf"}`: what changes in a life, plus the body
fraction, which no snapshot carried before. The genome is joined from A1 by id. A reader that
finds a slim row and no genome for its id refuses the row and counts it, as `WithoutAGenome`
does today. A converted snapshot's rows carry no `bf`, because format 1 never recorded one.

A3. **`positions.jsonl` becomes `positions.jsonl.gz`**, same rows, one member a sample.

A4. **`poses.jsonl` is retired for the stream.** In format 2 the farm turns the stream on at the
report interval (10 s) when the launcher names no cadence. The stream then carries what the JSON
did, plus the body fraction. `PoseStream` version 2 deflates each frame's body records and
nothing else. The frame's time, its body count and the records' raw length stay uncompressed in
front of them, and the framing and the trailer stay as they were. So a scan reads a frame's
second without inflating anything, and the index and the torn-frame rule are unchanged. Version 2
also adds one flags byte per body (absorptive 1, jointed 2, photosynthetic 4), so a reader of the
stream needs no second file for guilds. Version 1 streams stay readable, and the version field
says which. A body fraction of NaN means "not recorded" (`logbook/specs/state-stream-spec.md`),
and a picture draws that body at its adult size.

A5. **Checkpoints are compressed.** `Checkpoint.Version` 5 gzips the payload after digesting it.
The digest stays over the uncompressed payload, so the verification is unchanged. Version 4
files stay readable, and the version field says which, so no reader guesses from the bytes.
Format 1 still writes version 4, so a run in the old record writes the checkpoint every earlier
run wrote. `WorldState.StateVersion` does not move: the world's layout is untouched.

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
files beside the old ones, or into `--out`: genomes once, slim snapshot rows and gzipped
positions. It writes the JSON poses as a version 2 stream with its index. Every body fraction in
it is NaN, "not recorded". Every body's flags are the same sample's `positions.jsonl` row's, or 0
where that row or the body's entry is missing, counted and noted in the mark. A run that wrote a
stream of its own keeps its JSON poses unconverted, because its own stream is the better record.
The converter then checks, row for row, that `runrec.py` reads the same thing from both. The
stream is read back through `poses-read.py`'s functions and compared with the JSON to the bit. It
deletes nothing. Removing the old files after a check is the owner's decision, run by run.

A conversion cannot recover two things. A body born and dead between two snapshots is in no
format 1 snapshot, so no converted file holds its genome: 94 of the 5,914 birth rows of
`runs/r48fix-s4`. And a converted slim row has no body fraction.

That fixture, converted on 2026-09-24, measured the ratios. Its snapshots went from 430.3 MB to
0.26 MB of slim rows and 2.9 MB of genomes. Its positions went from 29.5 MB to 11.4 MB, which is
2.6x and not the 4.7x the first estimate assumed. Its 125.3 MB of JSON poses became a 42.6 MB
stream, 2.9x, of which deflate gave 1.54x on the body records. The converted stream holds the
JSON's rounded numbers, and a stream the farm writes holds the solver's floats, whose deflate
ratio is not measured.

The expected size of a seed like `r47-s2` under Part A follows from those ratios. Its genomes
come to about 35 MB (46,665 births at 4.9 KB, about 6.6x under gzip) and its slim snapshots to a
few MB. Its positions come to about 110 MB and its poses to about 210 MB. Checkpoints at 500 s
come to about 0.7 GB (60 at about 12 MB), and field dumps to 93 MB. That is about 1.2 GB a seed
where it was 6 GB, and the checkpoints are then most of it.

The trajectory does not move. Every writer change is in `src/Evosim.Farm` and in Core's
serialisers, recording only. `World.cs` gained one thing, the opt-in admission queue of A1, which
holds a reference to each admitted genome and reads and draws nothing. The regress runs the crowd
fixture's world in format 1 and in format 2, and both must read identical to `runs/r48fix-s4` in
every `stats.jsonl` field. The digest at 1 and 16 threads must be unchanged.

## Part B: films the farm moves and Unity draws

The division of labour, which the owner asked about ("we will not use Unity to render?"): Unity
draws every pixel as it does now, with the same skin, light, cameras, depth of field and grade.
What moves is the world. The farm, which replays its own run exactly, writes where every body is
thirty times a second for the stretch a scene needs, and Unity plays that back the way it already
draws a snapshot. The Editor's live mode stays as it is, for exploring.

B1. **`Evosim.Farm.exe --film-window <run dir> <from s> <to s> <out dir> [--fps 30]
[--threads N]`.** It restores the checkpoint at or before `from`, or founds the run from its
config and seed when there is none. It steps to `from` writing nothing. At each metabolic step it
does the loop's own work on the world, the assay and the extinction test. At the run's report
steps it runs the sampler with nowhere to write, which drains the world's queues as the run's
sampler drained them. From `from` to `to` it writes into `out dir`, never into the run directory:
- `film.poses.bin`, a version 2 stream. Frame *k* is written at the first physics step at or
  after the second *k* / fps, and carries that step's own second. So a frame is up to one
  physics step late and never early. The header records the nominal interval, one over the
  frame rate, and a rate above the physics rate is refused.
- `genomes.jsonl.gz`, in Core's gzip members, the run's own format, which one reader reads for
  both. Every body alive at `from` comes from the living roster, as a resumed run writes its
  own. Every body born after `from` and up to `to` comes from the world's admission queue, which
  the window turns on before its restore. The queue gives a body born and dead between two
  metabolic steps its genome too. The plans (`moduleCounts`, `lostPaths`) at `from`, and every
  change to one inside the window, go to `plans.jsonl`.
- `events.jsonl`, the run's own lineage rows inside the window: every birth (with parent), death
  (with cause) and bite. A bite rides the lineage queue beside births and deaths, and is written
  and counted apart from both.
- `identity.jsonl`: at every report second from the restore to `to`, `alive`, `births`,
  `deaths`, `auditResidual` and `meanHeight` against the run's own `stats.jsonl` row, to the bit.
  The pre-roll rows, between the restore and `from`, are compared and counted too. A window with
  no report second inside it steps on to the run's first row after `to` and compares that one.

The frame sampling runs below the metabolic step, which `ResolvePoseEvery`'s bound forbids for
the run's own stream; the window takes its own path and leaves that bound where it is.

A frame at a checkpoint's second can hold newborns that the run's own stream frame lacks. The run
takes its frame before the checkpoint settles the harness, so a body admitted at that step has
no solver body yet and is not drawn. The window restores the settled harness, and its frame at
that second draws the newborn. A frame from the window and one from the run at a checkpoint second
are therefore compared on the bodies both hold.

**The faithful rule** is the owner's, of 2026-09-24. A window reads faithful when its
`configHash`, `coreHash` and `dynamicsHash` equal the run's and every identity row agrees bit for
bit. The film's provenance word then says faithful. A differing `farmHash` is named in the
verdict and does not disqualify the window. The rows are the evidence, and the farm's source is
mostly recording: every build that adds a column moves it, round 48's among them.

A differing config, Core or Dynamics is refused as a resume refuses it, unless
`EVOSIM_ALLOW_SOURCE_MISMATCH` is set, and the window then reads cousin whatever its rows say. A
row that parts, in the pre-roll or inside the window, makes a cousin, and the verdict names the
second and the field. A window that runs past the run's last row, or finds no row to compare,
reads unverified. The window exits 0 when faithful, 2 for a cousin, 3 when unverified and 1 on a
refusal.

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

Acceptance for Part B has three parts. A window of a run recorded on this build reads faithful
at every report second inside it. That run is a fixture still to be recorded: `r48fix-s4` wrote
no checkpoints, so it can be filmed only from its founding. The fixture is its world recorded on
this build in format 2 with a checkpoint every 500 s. The playback's frame at a stream second
draws the same parts as `-From snapshot` does from the run's own stream at that second, to a
millimetre and a tenth of a degree. At a checkpoint's second the comparison is made on the bodies
both frames hold. And the wall time of a 60 s window is recorded at 5, 10 and 16 threads on a
machine with nothing else running.

## What waits for the round gap

Round 48 holds two farm runs until its third seed ends, and the machine's rule is no test suite
beside two farm runs. The build, the compile, the converter's dry check and the fixtures' recipes
are done now. Everything that runs the farm waits for the gap, or for a slot beside one farm run
with nothing else. That is the suites, the regress (format 1 against format 2), the checkpointed
fixture, the first faithful window, a resume from a version 5 checkpoint, the digests and every
timing. A timing taken beside anything is taken again. The theatre's readers have been
type-checked outside Unity and never compiled by it. The first Unity compile, and a `-From
snapshot` picture of a format 2 run, wait for the gap too.
