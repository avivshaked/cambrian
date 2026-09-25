# 0115 · The crowd on the card: the contacts and the brains

*2026-09-23, the morning after round 45's read. The measurement D105 asked for before the port is
sized: the two parts of the step the spike of 0112 left out, the contact query and the brain
with its senses, each written as an ILGPU kernel, proved in double against the library on the
CPU device, and then timed in single on the 4090 with the machine to itself. The design they
were built against is `logbook/specs/gpu-full-step-spec.md`; the code is
`spikes/02-gpu-featherstone/spike3/`, the tables `results/contact-kernel.txt` and
`results/brain-kernel.txt` beside it. An Opus subagent built and ran it from my brief; I
reviewed the checks and the numbers and commit them here.*

## What was asked

The spike of 0112 put the articulated-body step on the card and found single precision
about six times faster than sixteen cores at 10,000 bodies. It carried about a third of the
real step. The design's reading of the inventory was that the brain is small (median four
neurons a genome) and that the two places the card's time could go are the contact query,
whose neighbour count is unbounded and ends in a sort, and the panel loop. So the acceptance
asked for the contact kernel first and the brain second, each transcribed line for line and
proved bit-exact in double before its precision changed, and then priced at 10,000 and
30,000 bodies with real neighbour counts.

## The crowd

Round 45 seed 2 at 30,000 s: 6,145 bodies rebuilt from the snapshot at their recorded poses,
34,686 neurons among them (median four a body, the largest 75), 280 jointed. The larger
crowds are copies: for the contacts the bodies of the inscribed square tiled into wider
discs at the same number density, so the neighbour counts are the recorded crowd's; for the
brains the crowd repeated at its recorded places, so the senses read the recorded field and
poses. The inputs no record carries (relative water velocity, joint rates, reserves,
contact and damage flags) were drawn from a hash of each body's id, and the snow field's
vertical profile was spread evenly down each recorded column. Nothing moved during the
brain's thousand steps.

## The contact kernel

The grid is built on the card as the design said: a histogram of the cells each committed
sphere covers, a one-group prefix scan, a scatter, then the query, which sorts and
deduplicates its candidates exactly as `ContactGrid` does, and the push, bed, glass and cap
of `Contacts.Apply`.

| | |
|---|---|
| transcription, double on the CPU device, 1,000 steps of the moving library world | 145,950,406 values (candidate lists, overlap ids, pushes, pair sums, capped forces, every link's force, the bed and glass flags) against the library, 0 mismatches |
| the card against itself, single | bit-identical across repeats, group 32 against automatic, global against local scratch |
| single against double at rest | candidate lists 0 of 6,145 differ; body force max 0.385 N absolute, as a speed change 2.9e-5 m/s in a step |
| after 999 steps | four bodies' overlap sets differ; the pairs are grazing (median penetration 0.7 mm) so relative error is large on tiny forces; absolute max 0.13 N |
| occupancy at 6,145 / 10,000 / 30,000 | candidates max 85 / 86 / 105, unique max 25 / 27 / 29, overlaps max 4 / 2 / 2; no overflow at 256 and 64 |

The pace, in microseconds a body-step (a step in brackets):

| bodies | card, whole step | CPU, 1 thread | CPU, 16 threads | card over 16 threads |
|---|---|---|---|---|
| 6,145 | 0.054 (333) | 0.960 | 0.143 | 2.6x |
| 10,000 | 0.044 (436) | 0.899 | 0.131 | 3.0x |
| 30,000 | 0.018 (528) | 1.088 | 0.144 | 8.2x |

The step is nearly flat in the crowd: the query costs 284 to 342 µs whatever N, and the piece
that grows is the one-group scan (27 to 151 µs). The subagent's reading, which I share and
mark as inference: 30,000 bodies do not fill the card, so the step is the slowest thread's
latency, the bodies covering 64 cells and their sort, and not throughput. The mean radius
that sizes the cell has to be a tree sum on the card (the CPU's serial order costs 213 to
1,027 µs a step there), and the tree moves the cell's last bit only, which can move a
candidate list and never an overlap or a force.

## The brain and the senses

One kernel, a thread a body, struct-of-arrays strided by body: the senses (depth, up, flow,
joint angle and rate, the chemical channel from the snow stock by index, energy from the
reserve) and then the brain's step, the clock in double, every neuron evaluated on the
previous buffer, the swap and the drive.

| | |
|---|---|
| transcription, double on the CPU device, 1,000 steps | 244,692,000 values (both neuron buffers, memories, clocks, drive targets, every sense value) against `Senses.Sample` and `Brain.Step`, 0 mismatches |
| the card against itself, single | bit-identical across repeats and group sizes; one launch of 1,000 steps equals 1,000 launches of one |
| single against double after 1,000 steps | drive targets max 4.2e-7 off, none flipped; neuron outputs relative max 2.3e-2 at the 99th percentile 6.7e-6 |

The pace, in microseconds a body-step:

| bodies | card, one launch a step | CPU, 1 thread | CPU, 16 threads | card over 16 threads |
|---|---|---|---|---|
| 6,145 | 0.0146 (90) | 0.225 | 0.0189 | 1.3x |
| 10,000 | 0.0092 (92) | 0.340 | 0.0184 | 2.0x |
| 30,000 | 0.0031 (93) | 0.406 | 0.0234 | 7.5x |

Flat again, 80 to 99 µs a step from 6,145 to 30,000 bodies, and for the same reason: the
75-neuron sixteen-link bodies set the step. The metabolic upload of the snow stock and the
reserves is 6.6 MB and about 390 µs once in fifty steps, which amortises to nothing.

One thing the check turned up that is the crowd's and not the card's. The largest absolute
difference between single and double after a thousand steps is 5e27, on a neuron whose
value has run to the order of 1e29. The brain's guard catches a non-finite value and
nothing bounds a finite one, so an integrating neuron can run for the body's whole life
and in single it would reach infinity at 3e38 and be guarded there. The drive is clamped
and does not see it. I have not traced which body, and the port will want a bound or a
count.

## The whole step, the same afternoon

The two kernels were joined with 0112's solve into one kernel carrying the body phase whole,
in the CPU's order: the water pass at the pinned instant (the host uploads 186 numbers a step,
fifty steps a block), senses, brain, drive, fluid, contacts, joint torques, the
articulated-body solve with its aliasing kept, integration, poses, velocities, the four
settle sums in double, the trace ring, the finiteness check and the sphere refresh. The grid
kernels and the sphere swap are the contact spike's; the census and the digest row stay on
the host. Not carried, and refused by the host: D100's water hold, the rolls, the box's
transport field. The ceiling is sixteen links, 48 degrees of freedom, 256 candidates and 64
overlaps a body, the neuron stride the crowd's largest brain; the declared local memory is
28.6 kB a thread in double and 15.3 kB in single.

| | |
|---|---|
| transcription, double on the CPU device, 1,000 steps against `DynamicsWorld.Step` in the real world with the real current | 1,712,306,642 values (every array of the state, the drive windows, the limiter counts, the settle sums, the trace, the lost flags, both sphere buffers, the overlap lists, the census counts) and the world's own digest row at every step: 0 mismatches |
| the lost path, dt 0.02 with five bodies poisoned, 300 steps | the limiters fire and the five are lost at the same step on both sides; every living value exact; 5,100 values in the lost bodies are NaN on both sides with opposite sign bits |
| the card against itself, single | bit-identical across repeats and at group 32 against 64, at 100 and 1,000 steps |
| single against double after 100 / 1,000 steps | link positions max 6.7e-4 / 0.30 m, 99th percentile 2.8e-4 / 2.0e-3 m; joint angles max 2.0e-5 / 1.5e-4 rad; drive targets max 4.5e-7 |

The pace, all in (the grid kernels, the body kernel, the swap, the instants up and the
metabolic readback once a block), in microseconds a body-step:

| bodies | card, single | card, double | CPU, 1 thread | CPU, 16 threads | single over 16 threads |
|---|---|---|---|---|---|
| 6,145 | 0.276 | 4.15 | 7.39 | 0.872 | 3.2x |
| 10,000 | 0.190 | 2.79 | 7.77 | 0.897 | 4.7x |
| 30,000 | 0.106 | 1.38 | 8.20 | 0.996 | 9.4x |

The body kernel is 1.6 to 2.8 ms a step and about the same at 6,145 and 10,000 bodies, so
at the campaign's crowd the step is the slowest thread's again. The metabolic readback is
1.1 to 3.7 ms a block of fifty steps, moving arrays strided at the sixteen-link ceiling,
which is the first thing a size class would shrink. Double on the card is slower than
sixteen CPU threads at every size, as 0112 found for the reduced step.

One finding for the port's identity check. When both operands of an operation are NaN, the
machine keeps one operand's NaN, and two compilations of the same source can keep different
ones; the two sides here disagreed in the sign bit of NaNs inside lost bodies only. So a raw-bit
digest stops being an identity claim across two compilations the moment any body goes
non-finite, and the port's digest hashes a lost body's NaNs as one pattern or leaves lost
bodies out. The CPU farm's digest at 1 and N threads is one compilation and is not affected.

## What I make of it

The kernel the design described exists and is exact, which is acceptance item 1's standalone
half. What remains of item 1 is the farm driving it through its own uploads and readbacks for
3,000 s of a round 45 world, and that is the port proper. The two risks the design named are
not risks at the campaign's crowd. Contacts and brains
together cost about 0.07 µs a body-step on the card at 6,145 bodies, where the CPU's whole
step costs 2.2 to 2.8, and both fall toward 0.02 at 30,000 because the card is waiting on
its slowest thread, not working. The port's ceiling at this crowd is the launches and the
slowest body, which is what size classes are for. Both transcriptions are exact, which is
the thing that had to be true before single precision could be judged, and single moves a
contact force by a tenth of a newton and a drive target by a millionth. What is not
measured is the whole step with everything in one kernel at 10,000 and 30,000 bodies, the
rest of acceptance item 3, and that is the next spike, which is the port's first kernel
rather than a spike at all.

## Sources

| claim | source |
|---|---|
| every number above | `spikes/02-gpu-featherstone/results/contact-kernel.txt`, `results/brain-kernel.txt`; the logs under `logbook/specs/gpu-spike3-logs/` |
| the design and the acceptance | `logbook/specs/gpu-full-step-spec.md` |
| the reduced step's numbers | logbook/0112 |
| the ruling | D105 |
