# The checkpoint

*Build spec. 2026-09-22. It replaces the proposal of 2026-09-15 that stood in this file, which
was written for the Unity farm and is superseded in its central claim. That proposal said a
restored world could only ever be a cousin of its recording, because no API returns PhysX's
contact caches and solve order, and this build's trajectory depends on them to the last bit
(logbook/0069). The farm out of Unity solves its own bodies in doubles, one at a time, so that
state is ours and a restore is the continuation rather than a cousin. The acceptance below is the
claim the old spec could not make. Everything the proposal said about the theatre's seek is still
owed, and the owner's ruling on the labelled cousin still governs a restore across a build change.*

## Why

A farm run is a trajectory with no way back into it. The report says what happened, the state
stream says where every body was, and neither can be stepped. So a run killed at 25,000 seconds
of 30,000 is 25,000 seconds thrown away, and the theatre, which can now draw any recorded second,
cannot carry that second forward by even one step.

A checkpoint is the whole world written down: every number the next step reads, and nothing that
can be worked out again from the config and the seed. Restored, a run carries on from that second
and produces the same rows it would have produced without the interruption. That is the acceptance
test, and it is the only claim worth making about a file like this.

It is a recording and not a world rule. The cadence is `EVOSIM_CHECKPOINT_EVERY` simulated
seconds, default 0, which writes no directory at all. It is bound beside `EVOSIM_POSE_EVERY` in
the farm's table, reaches `EnvSettings` and never `RunConfig`, so no config hash moves and a run
with checkpointing on is the same world as one without. That last part is measured below.

## What state there is, and where it lives

Three programs hold state between steps, and the inventory is the work. Anything missed reads as
a small divergence a long way downstream, where it looks like chaos rather than like a bug.

Core's `World` holds five kinds of thing.

- The clock and the counters. Elapsed seconds, the next creature id, the next index, the next
  species id. Energy in and out, the matter influxed and buried. The seven totals the economy
  accumulates: deposited, exuded, taken, returned, burnt, remineralised and reserve trimmed.
  Seven long counters of short takes, uptake-limited steps and refused conceptions. The births,
  deaths, floor spawns, divergences, inoculations and three kinds of stillbirth. The seconds since
  the floor last fired, and the absorptive deaths it dropped.
- Where the sun stands in the day cycle, and the conception generator. Both the generator's state
  and its increment are written, because a generator restored into another stream would draw
  plausible numbers rather than the same ones.
- Every living creature: an id, a parent, a generation depth, a birth seed, a species, a reserve,
  standing and adult tissue, a body fraction, an age, a height, a birth height, an x and a z, a
  patch, pending work, standing watts, absorptive volume, two tissue flags, a child count, the
  second of its last child, the density and share it last saw, its last step length, and two
  nine-field energy ledgers.
- The species registry, with each founder's genome and the second it was founded, and the corpses,
  with an id, a place, a patch, joules and an age each.
- The two instrument queues the harness drains at a sample.

A creature's genome is written as one line of JSON, which is the format the snapshots already use.
Its body is developed again from that genome rather than stored, because `Developer` is a pure
function of the genome, the limits and the shape registry, so what comes back is the object the
run built. A body that had not finished growing is a scaled copy, so the scale goes in the file
beside a flag saying whether it was the adult.

The two queues matter more than they look. A checkpoint is not a sample, so whatever is standing
in them belongs to the resumed run's first sample. Dropped, they would be rows the original wrote
and the restore did not.

Last is the water. Both fields write their whole stock cell by cell, as doubles, with the total
beside it, and the reader refuses a stock of a different length. The grid, the vertex field and
the old cell field each have their own writer, dispatched by type. A representation the dispatcher
does not know is refused by name rather than skipped.

`Evosim.Dynamics` holds each body's solver state.

- Where it is: base position and rotation, joint coordinates and rates, ball rotations, and every
  link's place, attitude, rotation matrix, spin and velocity.
- What the last step left: the spatial bias and the two spatial accumulators, the water and water
  acceleration it sampled, its relative velocity, its patch, the held water sample and the second
  it was taken, the contact sphere and its pending twin, and the ids it is holding.
- What it has accumulated: four ledger accumulators with their four drain marks, two limiter
  counters, whether it is alive, the body fraction the solver has applied, its resize count and
  the step of the last one, the first non-finite step and link, and the throw trace's ring when
  tracing is on.
- Its brain and its drive. The brain's recurrent state is the reason a restored body swims the
  same stroke rather than a plausible one. The drive carries a torque history, a running sum and a
  cursor.

The world around them holds elapsed seconds, steps, and the four overlap counters the contact
instrument reports. The placer holds its own generator, its wrap, rejection and refusal counts,
and every reservation it is holding.

The farm holds the harness's per-body bookkeeping: a radius, a last root place, a previous centre
and root, and three flags. Beside it are the harness's step count and reconciliation mark, the
twelve readings, four motility accumulators, and the sampler's four ever-seen sets and nineteen
window baselines. The loop itself holds four numbers: the metabolic step count, the fastest
creature ever seen, the second it was seen at, and whether the inoculation assay has fired.

What is deliberately not written is everything that is a pure function of the config and the seed.
The light model, the current field's amplitudes and phases, the bed's shape, the field geometry
and its masks, the patch map: all of them are rebuilt by construction before a byte is read.

## The file

A run's checkpoints live in `checkpoints/` inside its run directory, named for the simulated
second, zero padded to nine digits, so a listing sorts chronologically. The payload is built in
memory, written to a neighbouring temporary file and moved over the final name, so nothing ever
opens a checkpoint that is still being written.

| Offset | Bytes | Type | Field |
|---|---|---|---|
| 0 | 8 | ascii | magic, `EVOCKPT` and a zero byte |
| 8 | 2 | uint16 | version, 2 (1 until 2026-09-22, when a body's reserve, tissue and adult tissue became doubles) |
| 10 | 2 | uint16 | magic block length, 12 |
| 12 | 8 | float64 | the simulated second |
| 20 | 8 | uint64 | the world's seed |
| 28 | 8 | int64 | physics steps taken |
| 36 | 4 | float32 | the physics step, seconds |
| 40 | 4 | int32 | physics steps per metabolic step |

Then seven length-prefixed strings: the run's `configHash`, its `coreHash`, `dynamicsHash` and
`farmHash`, the engine version, and the arm and run directory it was written in. Then the four
recording cadences (report rows, state-stream frames, digests and checkpoints), the payload's
length as an int64 and an FNV-1a digest of it as a uint64. The payload follows, and after it the
length again and the magic again.

The trailer is the state stream's completeness rule with a digest added. Either the repeated
length or the closing magic alone would catch a truncation, and both together also catch a file
overwritten from the front by something else of the same length. The digest is there because a
checkpoint is read once and acted on, where a frame is one of sixty thousand.

The payload is a sequence of tagged sections. Every tag is four ASCII bytes and the reader refuses
one that does not match, so a layout that drifts fails at the section rather than thousands of
fields later. In order: `PAYL`, the world (`WRLD`, `LIVE`, `SPEC`, `CRPS`, `LNGE`, `ABSD`, the two
fields, `WEND`), the harness (`HARN`, `DYNW`, `PLAC`, `BDYS` with one `BODY` per creature, `HEND`),
the sampler (`SMPL`), the loop's four numbers under `LOOP`, and `PEND`. Doubles are written as
doubles and floats as floats, through `BinaryWriter`, so nothing goes through a decimal
representation on the way.

## Refusing rather than defaulting

A checkpoint records four source digests, and a resume refuses unless all four are this build's.
The config is the world, Core is the economy and the development, Dynamics is the solver and the
farm is the loop around them, so a checkpoint read under a different one of those carries on into
a trajectory the recording never had. `EVOSIM_ALLOW_SOURCE_MISMATCH` turns the refusal into a
warning and marks `run.json`, so the result is readable as a cousin of the recording rather than
as its continuation. That is the theatre's own guard, under the theatre's own word for it.

The world's own reader refuses two more things: a layout version it does not know, and a seed that
is not the one this world was constructed with. Every per-creature seed derives from the world's,
so a checkpoint from another seed is a different world and not a different moment in this one.

## Resuming

A resumed run gets a new run directory and a report of its own. Its world comes from the source
run's `config.json` rather than from the launcher's environment. That is what makes the two config
hashes equal in the first place. An environment differing by one knob would otherwise build a
world the checkpoint cannot be put into, and would say so only after the directory had been made.
The seed and the physics step come from the checkpoint's header, and `run.json` carries a
`resumedFrom` block naming the arm, the run directory, the second and the checkpoint's digest.

The four recording cadences come from the checkpoint too, unless this launcher named one itself.
They are not in the config, so without that they would fall back to their defaults and a
continuation would write its rows on other seconds than the run it continues. `EnvSettings` now
records which `EVOSIM_*` names were actually set, because a default and a value that happens to
equal it are the same number and not the same fact.

```powershell
./scripts/run-farm.ps1 -Arm ckA -Seconds 600 -Threads 8 -RunsRoot scratch/checkpoint/runs `
    -Launcher rounds/env-r42.ps1 -Env @{EVOSIM_WATER_HOLD=0; EVOSIM_CHECKPOINT_EVERY=200}
./scripts/run-farm.ps1 -Arm ckB -Seconds 600 -Threads 8 -RunsRoot scratch/checkpoint/runs `
    -ResumeFrom ckA -At 400
```

A checkpoint is written after the report row and before the loop acts on a stop, so a stopped arm
writes one at the instant it stopped. Writing it before the row would hand the resumed run the
same window to write again, which is the duplicate row a restore exists to avoid. Two things
happen first: the harness is reconciled, because a checkpoint taken mid-reconciliation would carry
a world holding creatures the solver has no body for, and the queued lineage rows are drained, so
the file and the checkpoint agree about which births are already written down.

## What it costs, and what it does not change

Round 42's world is a tank of 2,200 square metres and 45 metres, with one-metre detritus cells and
five-metre matter cells. A checkpoint of it at 40 living bodies is 1.55 MB. The population slope,
measured between the 400 and 600 second files, is 4.2 kB a body, most of which is the genome as
JSON. The fields are about 1.0 MB of that and do not move with the crowd.

Round 43's world is 171,000 cells across two fields at about 1,100 bodies. Its checkpoint should
therefore be about 2.7 MB of water and 4.6 MB of bodies, near 7.5 MB. I have extrapolated that
from two measurements in a smaller world and have not measured it.

Checkpointing changes nothing about the run. I ran two 600 second arms of round 42's world at seed
1, one writing a checkpoint every 200 seconds and one writing none. Every file they wrote is
identical, bar the wall clock and the two counts of work it is divided by. The extra
reconciliation a checkpoint forces is a walk of the roster that moves nothing.

## The acceptance

Run to 600 seconds writing a checkpoint every 200. Restore at 400 and run on to 600. Compare every
row after 400 in the four files the run writes.

| File | Rows after 400 s | Result |
|---|---|---|
| `lineage.jsonl` | 43 | identical |
| `positions.jsonl` | 20 | identical |
| `poses.jsonl` | 20 | identical |
| `absorptive.jsonl` | 52 | identical |
| `stats.jsonl` | 20 | identical bar the wall clock |

The wall clock is the exception, and it is not a defect. A restored run has spent less of it, and
`harnessBodySteps` and `fluidLinkSteps`, the two denominators the wall split divides by, reset with
it. Everything those instruments measure is this process's own work rather than the world's.

Two failures found the acceptance worth running. The first attempt wrote its rows at 500 and 600
where the original wrote twenty rows from 410, because the report cadence is not in the config and
the resumed run had fallen back to the default. The fix is the inheritance described above. The
second was a truncation test that threw an end-of-stream from inside the header's strings instead
of saying the file was cut; the reader now reports that as the truncation it is.

## The tests

`src/Evosim.Core.Tests/WorldStateTests.cs` steps a small world 60 seconds, writes it, and reads it
into a fresh world of the same config and seed. It then asserts three things. The restored world
writes the same bytes again. It reads the same on every instrument. And after 120 more seconds on
both sides the two still agree. The first catches a field read back wrong. The third catches a
field not written at all, which the first cannot see because neither side has it. Beside them,
`RngStateTests` covers the generator, including the spare Gaussian that a restore is one draw out
of step forever without.

`src/Evosim.Dynamics.Tests/CreatureStateTests.cs` does the same for a driven four-link body in
water, with the solver's own digest as the assertion, and checks that the brain's memory is carried
by stepping both sides 50 steps and comparing every neuron's output.

`src/Evosim.Farm.Tests/CheckpointTests.cs` covers the container, checking the header round trip
and then the refusals: truncation at three depths, an empty file, a file that is something else, a
byte changed inside the payload, and a version this build does not read. The rest is the four
hashes and finding a checkpoint from an arm directory or a second.

## What is left

The theatre cannot yet open a checkpoint. Carrying a recorded second forward live in the Editor is
the reason the file exists and is a separate piece of work, and it needs the Unity side to load
Core's and Dynamics' state the way the farm does. A farm run is stopped by a `STOP` file in its run
directory, which `stop-arm.ps1` writes for a manifest reading `engine: "dynamics"`; a resume after
that reads the last checkpoint, which is now the second the run stopped at.
