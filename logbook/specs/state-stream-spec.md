# The state stream

*Build spec. 2026-09-22; version 2 added 2026-09-24.*

## Why

The farm out of Unity records a world's poses once a sample, every ten simulated seconds, in
`poses.jsonl`. The Editor cannot re-simulate one of its runs: the two runtimes format and round
floating point differently, so Mode B on a farm recording watches a cousin. What is left is
playback from what was written down, and at ten seconds a frame nothing moves. A fish crosses a
metre between rows.

The stream is the same information written densely enough to be afforded every half second:
`poses.bin` beside `poses.jsonl`, binary, little-endian, one frame appended per recorded instant.
It carries one field the JSONL never had, the body fraction. A reader therefore knows how far a
body had grown, and can draw it at the size it was rather than at its adult size.

It is a recording and not a world rule. The cadence is `EVOSIM_POSE_EVERY` seconds, default 0,
which writes no file at all. It is bound beside `EVOSIM_REPORT_EVERY` and `EVOSIM_DIGEST_EVERY`
in `EnvBinding`'s table, reaches `EnvSettings` and never `RunConfig`, so no config hash moves and
every recorded run replays as it did. A cadence under the half-second metabolic step is raised to
it, because nothing about a body changes between steps.

## The file

`poses.bin` opens with an eighty-byte header and then holds frames, each one a length-prefixed
payload with the same length repeated after it. The repeat is what makes a killed run readable. A
frame counts as complete only when its trailer is there and agrees with its header, so a torn last
write is dropped rather than parsed as a body count of two billion.

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 8 | ascii | magic, `EVOPOSE` and a zero byte |
| 8 | 2 | uint16 | version, 1 or 2 (below) |
| 10 | 2 | uint16 | header length, 80 |
| 12 | 4 | float32 | cadence, seconds |
| 16 | 64 | ascii | the run's `configHash`, zero padded |

Then, repeated to the end of the file:

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 4 | ascii | frame magic, `FRAM` |
| 4 | 4 | uint32 | payload length, *n* |
| 8 | *n* | payload | the frame |
| 8 + *n* | 4 | uint32 | *n* again, the completeness trailer |

A version 1 payload is a time, a count and then that many bodies:

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 8 | float64 | *t*, simulated seconds |
| 8 | 4 | uint32 | living bodies in this frame |
| 12 | … | body | one per body, in the world's own living order |

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 4 | int32 | organism id |
| 4 | 12 | 3 × float32 | root link position, metres |
| 16 | 16 | 4 × float32 | root link quaternion, x y z w |
| 32 | 4 | float32 | body fraction, tissue over adult tissue |
| 36 | 1 | uint8 | degrees of freedom, *d* |
| 37 | 4 × *d* | float32 | joint coordinates, the solver's own order |

The quaternion is four float32 rather than a smallest-three packing. A packing into four bytes
would cost a third of a degree of attitude in the worst case, and a page of bit arithmetic in
every reader. The four components round-trip the solver's doubles to about 1e-7, and the reader
is a loop of `BinaryReader.ReadSingle`. The degree-of-freedom count is a byte per body although
the genome already implies it, so that a reader with no snapshot beside it can still walk the
frame. Three degrees of freedom is a ball joint's rotation vector and not three angles, under
`RecordedPoses`' rule and unchanged here.

The positions are the root link's, the same quantity `positions.jsonl` carries, and the
quaternion is the root link's attitude, the same quantity `poses.jsonl` carries. Nothing is
rounded on the way in: the JSONL rounds to the centimetre and to four places, and the stream
writes the float the solver held.

## The index

`poses.idx` is written when the run ends, a twenty-four-byte header and then one sixteen-byte
entry per frame.

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 8 | ascii | magic, `EVOPOSX` and a zero byte |
| 8 | 2 | uint16 | version, the stream's own |
| 10 | 2 | uint16 | header length, 24 |
| 12 | 4 | uint32 | frame count |
| 16 | 8 | — | reserved, zero |
| 24 + 16*i* | 8 | float64 | frame *i*'s time |
| 32 + 16*i* | 8 | int64 | frame *i*'s offset in `poses.bin` |

A killed run has no index, so the index is a shortcut and never the truth. `PoseStream.Index`
reads `poses.idx` when it is there and checks four things: the magic, the version, the frame count
against the file's size, and the first and last entries against the frames they point at. Any
failure falls back to a scan of `poses.bin`. The scan walks frame by frame from the header and
stops at the first frame whose magic, length or trailer does not hold. That is the rule a live
run's last row is already read under. A bad magic or an unknown version in `poses.bin` itself is
refused rather than skipped, under the loading rule the config reader follows.

## Version 2

Version 2 (2026-09-24, items A4 and B1 of `logbook/specs/record-and-film-spec.md`) changes two
things inside a frame and nothing around it. It deflates the bodies, and it gives each body one
byte of guild flags. The file's header, the frame magic, the length, the trailer and the index
are written as before. The scan, the torn-frame rule and the index checks therefore read a
version 2 file as they read a version 1 file. The version field in the file's header decides
which layout the payloads carry. The reader takes both, and a writer asked for version 1 still
writes one.

A version 2 payload keeps a sixteen-byte prefix uncompressed. The bodies follow it as one raw
deflate stream, RFC 1951 with no zlib or gzip wrapper, the form .NET's `DeflateStream` writes:

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 8 | float64 | *t*, simulated seconds |
| 8 | 4 | uint32 | living bodies in this frame |
| 12 | 4 | uint32 | the body records' length before deflating, *r* |
| 16 | *n* − 16 | deflate | the body records, *r* bytes once inflated |

The prefix stays raw because the scan reads a frame's time from the payload's first eight bytes,
and the index check reads it too. Deflating the whole payload, as A4's first wording had it, would
make a reader inflate every frame to find one second.

Inflated, the records are version 1's with one byte inserted before the degree-of-freedom count:

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 4 | int32 | organism id |
| 4 | 12 | 3 × float32 | root link position, metres |
| 16 | 16 | 4 × float32 | root link quaternion, x y z w |
| 32 | 4 | float32 | body fraction, tissue over adult tissue |
| 36 | 1 | uint8 | guild flags: absorptive 1, jointed 2, photosynthetic 4 |
| 37 | 1 | uint8 | degrees of freedom, *d* |
| 38 | 4 × *d* | float32 | joint coordinates, the solver's own order |

The flag bits are `positions.jsonl`'s, `PositionsRow`'s constants, and the writer sets them by the
sampler's definition. A body is absorptive when any part is absorptive tissue, jointed when its
joints' degrees of freedom sum above zero, and photosynthetic when it carries photosynthetic
tissue. A writer refuses a flag value above 7. A version 1 frame read by this build reports every
body's flags as −1, meaning not recorded, and never as 0, which would claim a body with no guild.

The reader refuses three things in a version 2 frame that a version 1 frame cannot have. The
records must inflate to *r* bytes, no fewer and no more. The count must fit in them at 38 bytes a
body. And the last body must end where the records end. A frame whose trailer holds while its
deflate stream is damaged is refused with its byte offset, and never read as a shorter frame. The
trailer says the frame was written whole, so a frame that will not inflate is damage and cannot be
a torn write.

The index's version equals the file's. An index of the other version is dropped and the file
scanned, under the fall-back rule above.

### A body fraction that was not recorded

A body fraction of float32 NaN means the stream does not know how far the body had grown. The
farm always knows, and never writes one. The converter (`scripts/record-convert.py`) writes one
for every body of a stream it makes from a run's JSON poses. That file never carried a fraction,
and a number there would be invented. Any NaN reads this way, whatever its sign and payload bits.
Python's `struct` writes `00 00 c0 7f`, and .NET's `float.NaN` is `00 00 c0 ff`.

A reader takes NaN as "not recorded" and never as a size. The C# reader hands the NaN over
unchanged, and `PoseBody.FractionRecorded` is the test. The Python reader returns None, the way
`runrec.py` reports a snapshot's missing fraction, and prints `n/r`. The theatre draws such a body
at its adult size, as every picture drew bodies before the stream existed, and its label says
adult size. A frame in which some bodies carry a fraction and others NaN is drawn all adult, under
the all-or-nothing rule the snapshot path already keeps.

`scripts/poses-read.py` reads both versions with Python's `zlib` (raw deflate, `wbits` −15) and its
own tables, still written from this page. Its `--check` reads every frame and names the first
that fails, and its summary prints a version 2 file's deflate ratio.

What deflate buys has not been measured. A body's floats resemble their neighbours' only in their
high bytes. The ratio on a real crowd is therefore a reading to take from the first stream this
build writes, and not an arithmetic to carry. The cost table above is version 1's, and it stays the
upper bound at one byte a body more.

### The film window

`Evosim.Farm.exe --film-window` (B1, `src/Evosim.Farm/FilmWindow.cs`) writes a version 2 stream
named `film.poses.bin` into its own output directory, never beside a run's `poses.bin`. The
header's cadence is the nominal frame interval, `1 / fps` as a float32. The frames are not on the
metabolic grid. Frame *k* is written at the first physics step whose time is at or after
*k* / fps, and it carries that step's own time. A frame therefore sits up to one physics step
(0.01 or 0.02 s) after its nominal second, and never before it. A reader asks for the frame at or
after *k* / fps rather than for an exact second. The run's own stream keeps the half-second
floor that `ResolvePoseEvery` sets. The window samples inside its own loop and never calls it.

The stream does not say whether its film is faithful to the run. The verdict line at the end of
the window's `identity.jsonl` does, and a player takes its provenance word from there. The word
rests on the identity rows (the faithful rule in `record-and-film-spec.md`, B1). A window of a
run recorded on an older build can read faithful, with the differing source hashes named on the
same line. A window under another config is refused, or reads cousin when the launcher overrides
the refusal.

## What it costs

A body with three degrees of freedom is 49 bytes, so a thousand of them make a frame of about
49 kB. Thirty thousand simulated seconds at half a second is 60,001 frames, which is 2.9 GB. That
is over the 2 GB this was sized against, and the cadence that fits is one second, at 1.5 GB. A
round of ten thousand bodies costs 490 kB a frame and affords ten seconds, again 1.5 GB, or five
seconds at 2.9 GB. The arithmetic to carry is 37 bytes a body plus four a degree of freedom, times
the crowd, times the budget over the cadence.

Half a second is therefore a cadence for a short run or a small crowd. The smoke that validated
this recorded 300 seconds of a founding world at half a second, in a file of a few hundred
kilobytes. A full round is recorded at one second or slower.

## What a reader gets

`PoseStream` lives in `src/Evosim.Farm` and touches nothing but `System.IO`, so the Unity project
consumes it through the farm package the theatre already references. The file is opened with
`FileShare.ReadWrite`, so a live run is readable under its own writer.

The theatre draws a still from a frame the way it draws one from a snapshot row, with the join
moved. Genomes come from the nearest snapshot at or before *t*. The pose, the place and the size
come from the stream frame at *t*. A body born after that snapshot has no genome, so it is skipped
and counted as unmatched. The label says which two seconds the picture was built from, as
`pose t=105 of snapshot 100`. It also reads `recorded size` where a snapshot-only reconstruction
reads `adult size`: the phenotype is scaled by the cube root of the body fraction, the scaling
`World.Grow` applies to a growing body. A converted stream records no fraction, so its label reads
`adult size` like a snapshot's.

One thing is lost by drawing from the stream rather than from `positions.jsonl`. That file carries
the harness's own guild flags and the stream does not, so in stream mode a body's guild is read
off the developed phenotype. The disagreement count that a snapshot picture prints has nothing to
compare and stays at zero. A version 2 stream carries the flags, and the theatre does not read
them yet: `SnapshotWorld` still takes the guild off the phenotype in stream mode.

## How it was checked

`scripts/poses-read.py` is a second implementation of the layout, written from this page rather
than from the C#, and it reads a run's stream from the command line. A reader that agrees with
its writer only because the two were written together has checked nothing.

`Evosim.Theatre.EditorTools.TheatreStreamCheck` is the theatre's end to end. It runs in edit mode
with no graphics device, opens a run, draws a second the stream holds and the snapshots do not,
and prints the label the picture would carry. A picture needs a graphics device, so this is what
a headless machine can assert.

The build was accepted on five readings. A 300 second smoke at half a second wrote 600 frames in
987,043 bytes. Its `lineage.jsonl` and its `positions.jsonl` are byte-identical to the same smoke
with the stream off, so the recording moved nothing. Both runs carry `configHash`
`0861387741b94259`. The Python reader and the C# agree on the header, the index and every frame,
and the frame at 100 seconds agrees with `poses.jsonl`'s row at 100 seconds to the JSONL's own
rounding. The theatre drew 105 seconds from the frame and 100 seconds from the snapshot, joined
38 of the 40 bodies, skipped the two born after the snapshot, and posed all 38.

Version 2 was built beside two live arms, and its tests were written and not run. Until they
run, and until `poses-read.py` reads a stream the farm wrote, its layout is the C# code's claim
and not a checked one. Two readings were taken. The Python reader still reads version 1: the 300
second smoke's 600 frames and 23,383 bodies, with the index agreeing. And the converter's version
2 stream of `runs/r48fix-s4`, written in Python from this page, reads back through `poses-read.py`
frame for frame against the `poses.jsonl` it came from. That second reading checks the page and
the Python reader against each other, and not the C# writer.

## What is not here

There is no Play-mode player that scrubs the stream, and no writer in the Unity farm. The stream
is written by `Evosim.Farm` alone, which is the farm that runs.
