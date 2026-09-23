# The full step on the card: a design from the inventory

*Fable, 2026-09-23, during round 45. D105 ruled the port (single precision, designed for
100,000 bodies, validated at 10,000 to 30,000 with the grid on the CPU) and said the brain and
the senses are measured as a kernel before the port is sized. This is the design that
measurement is made against. Nothing here is built. The inventory it rests on is
`scratch/gpu-design/step-inventory.txt`, taken from the code on 2026-09-23 with file and line
for every claim; the spike it extends is `spikes/02-gpu-featherstone/` (logbook/0112).*

## What the step is

`DynamicsWorld.Step` is five phases. A serial contact-grid build over every body's committed
sphere. A parallel water pass under a pinned instant of the current. The parallel body phase,
fifteen sub-phases in a fixed order: senses, brain, drive, fluid, contacts, joint torques, the
articulated-body solve, integration, poses, velocities, the energy ledger, the throw trace, the
finiteness check and the contact-sphere refresh. A serial commit of every sphere. A serial
after-step, the contact census and the digest row. Everything runs at every physics step. The
metabolic step, fifty physics steps at dt 0.01, is where the world reads the bodies and writes
to them: the divergence guards, the ledger drains, `World.Observe`, the contact hand-over, the
economy, the senses' hand-back, growth.

Three things a body reads that another body or the world writes, and only three. The chemical
sense reads the snow field once per link per step, and the energy sense reads the body's
reserve once per step; both are constant between metabolic steps. The water pass reads the
current field, read-only under the pin. The contact pass reads other bodies' committed
spheres through the frozen grid, the state at the end of the previous step and never the live
one. Nothing accumulates into a shared total inside the step.

The spike carried about a third of this: the articulated-body passes, the drag panels,
buoyancy and lift, the drive damper, the limit springs and the drive torque pair, with the
drive precomputed and held, no brain, no senses, no water arrays, no contacts, no limiters,
no ledger, no trace, no guard. What follows is the other two thirds.

## The brain is small

The inventory's one open size is the brain: neurons per node are an unbounded random walk
(founders draw one to three, mutation adds at 5% and removes at 4%, no cap), and the kernel
needs a ceiling. Round 45 seed 3's snapshot at 15,400 s, 2,055 genomes, says how far the walk
has gone: neurons summed over a genome's nodes median 4, 99th percentile 11, maximum 16; the
most on one node 5; inputs per neuron 0, 1 or 2, never 3. A developed body carries its node's
neurons on every part grown from it, so the body's count is bounded by parts times the
largest node, 16 × 5 = 80 in this crowd, and is typically about ten. The evaluation is a
20-way switch over the op, three input reads each, then `value * Amplitude + Bias` and a
non-finite guard; the ops are `OscillateWave`, `Sigmoid`, `Sum` and `Differentiate` in the
main. My estimate from the counts: about a thousand flops and a handful of transcendentals a
body-step, against tens of thousands in the articulated-body passes. The brain is not where
the card's time goes. The two places it may go are the contact query, whose neighbour count
is unbounded and crowd-dependent and ends in a sort, and the panel loop, 24 to 40 panels a
link with about half skipped on `normalSpeed <= 0` by a test that depends on the flow.
The spike order follows: contacts first, the brain second.

## The kernel's contents

One thread is one body, as in the spike. The body phase is one kernel and carries every
sub-phase in the CPU's order, transcribed and not rewritten, so that the acceptance below can
ask for the same bits on the CPU device. What each sub-phase needs from outside the body:

- **Senses.** The chemical channel reads the snow field's cell density at each link:
  `GridField.EdibleDensityAt` is one cell's edible stock over the cell's volume, a single
  indexed read. The field changes only at the metabolic step, so its stock array is
  uploaded once per metabolic step. That is 168 × 45 × 168 floats in round 45's tank, 5 MB,
  or 10 MB a simulated second at ten times real time. The energy channel reads `SecondsOfReserve`, one float a body,
  uploaded with the field. Contact and damage are the metabolic hand-over already, one
  bool and one float a part. Depth, orientation, flow and the joint channels are the body's
  own. The six mask bits gate each channel as on the CPU.
- **Brain.** Struct-of-arrays, strided by body as `Flat.cs` strides. Per neuron: the op,
  the four scalars and three input slots of (kind, index, channel, constant, weight), a
  missing slot reading as the CPU's missing input, zero. Per part: the offset, the parent,
  the first child, the DoF start and count. State per neuron: previous, current, memory,
  three floats; the swap is a pointer swap per body, not a copy. The oscillators take the
  body's own clock, kept in double on the card and narrowed where the CPU narrows. A float
  clock at 30,000 s has an ulp of 2 ms, and a one-hertz oscillator would drift a degree on
  it; a few double operations a body-step are not the sixty-fourth-rate problem. `Sin`,
  `Cos` and `Tanh` in float from `ILGPU.Algorithms`.
- **Drive.** The ten-sample window per DoF and its cursor, the torque per unit, the
  action-reaction pair into `Fext`. The limiter is off at dt 0.01 and is carried as the
  branch it is.
- **Fluid.** The spike's panel loop plus what it left out. The water and its acceleration
  per link come from the water pass; `Volume` and the Morison term (D090), `PlainMass` and
  `SmallestInertia` for the limiter, and the relative velocity written for the next step's
  flow sense are carried. The panels are the largest per-body constant, seven floats each
  and up to 640 a body at `PanelsPerAxis` 2. At 10,000 bodies and a hundred panels each that
  is 28 MB read a step, which the card's bandwidth covers a hundred times over.
- **Contacts.** The grid is built on the card, not the host: a histogram of the cells each
  committed sphere covers, a prefix sum, a scatter, three small kernels before the body
  kernel. The histogram uses atomics, so a bucket's order is a race. The CPU's query sorts
  and deduplicates its candidate list before reading it, which makes the bucket order
  irrelevant to the result, and the transcription keeps that sort. The commit is a swap of the
  pending and committed sphere buffers between steps, exact and free. The candidate list
  lives in a per-body scratch with a fixed capacity and an overflow count. The overlap ids
  the census and the mouth read are a fixed capacity per body too, with the overflow
  counted and printed. The CPU grows both by doubling and never overflows, so a nonzero
  count is the first thing a read of a GPU run checks. The bed's twelve cosines and the
  glass are per-body arithmetic and stay in the kernel.
- **Joint torques, solve, integrate, poses, velocities.** The spike's, in single, with the
  aliasing the inventory flagged reproduced exactly: `Outward` parks the joint accelerations
  in `Tau`, and `Settle` reads them there.
- **Settle.** Four accumulators a body, kept in double on the card and drained at the
  metabolic readback; fifty steps of link-wise sums in float would drift into the ledger.
- **Trace.** The three-frame ring of 21 floats a link for jointed bodies, in global memory,
  written by the body's own thread. It is the only post-mortem a throw has. It costs about
  13 MB of writes a step at 10,000 sixteen-link bodies and far less at the real crowd, and
  it is measured before it is cut.
- **Finiteness and the sphere.** The nine-value sum per link; a lost body sets its status
  and zeroes its pending radius in the kernel, as `MarkLost` does, so it leaves every contact
  set the next step. The refresh writes the pending sphere.

Size classes rather than one ceiling. D105 said a fixed link ceiling per kernel build. The
inventory says the local working set is about 121 numbers a link and 10 a DoF, 9.8 kB in
single at sixteen links. The spike said every thread pays for the ceiling whether it uses it
or not. So the kernel is compiled at a few ceilings, for links and for neurons. Bodies are
sorted into the smallest class that holds them, and one launch per class runs the step. A
body's arithmetic is its own in every class, so the classes change no bits. The top class is
the world's ceiling: sixteen links is `MaxParts` already, and a neuron ceiling of 256 is
sixteen times this crowd's largest body. A body over it is refused at admission and counted.
That refusal is a world rule for the owner only if a body ever reaches it, and the counter
says whether one has.

## What stays on the CPU, and what crosses the bus

The world is Core and stays: the economy, the fields and their transport, births, deaths,
growth, the module rule, the census, the guards. The farm's `Simulation` keeps its shape; the
solver behind `Dynamics.Step` becomes the card.

Per physics step, up: the current's pinned instant (the streams' 24 cosines, 24 sines and the
cell terms, a few hundred floats). Fifty instants are known in advance from the clock, so
they go up once per metabolic step as a batch and each step's launch indexes its own. Nothing
comes down per step. There are four launches a step, three for the grid and one for the
bodies, at about ten microseconds each. That is two milliseconds of launch overhead a
metabolic step, against a body kernel the spike puts at a fifth of a millisecond at 10,000
bodies. The launches dominate the card. The whole is still tens of times real time, which is more than the world's
grid allows (HANDOFF's chain of three links). The box world's seam wrap moves bodies between
steps and is not carried: the port refuses a box and says so, and the tank is the campaign's
shape since D108.

Per metabolic step, down: positions and rotations for every link, which feed the poses
stream, the mouth's nearest-parts pass and the guards' centre of mass. With them the four
ledger sums, the two limiter counts, the lost flags, the overlap ids with their overflow
counts, and the sphere. Up: the snow field's stock, the reserve seconds, the contact and
damage hand-over, and the constants of every body born or resized since the last step. A
slot is a body's index in the arrays; a death frees it, a birth takes the lowest free one,
and the kernel skips an inactive slot. The digest row, on under
`EVOSIM_DIGEST_EVERY` only, is a per-step readback of thirteen numbers a link and is for
validation runs, where its cost does not matter.

`run.json` names the engine (`gpu`), the precision, the device, the driver, the group size and
the class ceilings. No recorded run replays on it, and its base round is read by D104's gate.

## Acceptance

1. The transcription is proved before the precision is changed. The full kernel runs on
   ILGPU's CPU accelerator in double, driven by the farm through the same uploads and
   readbacks, against `DynamicsWorld` on a round 45 world for 3,000 s. The rows are
   identical, the digest at every step is identical, the lineage byte-equal. This is the
   spike's first table made whole, and it is the only test that can separate a
   transcription fault from a rounding one.
2. The card agrees with itself. The single-precision kernel twice on the same input, and at
   two group sizes, gives the same bits over a full metabolic block, as the spike showed
   for the reduced step.
3. The senses and the brain are priced before the port is sized. A kernel of the brain and
   the senses alone over the snapshot's bodies runs a thousand steps against the same on
   the CPU. Then the whole step at 10,000 and 30,000 bodies with contacts on, at a crowd's
   real neighbour counts.
4. The candidate and overlap capacities never overflow on round 45's crowd, or the counts
   say by how much.

The measurements of 3 are the next spike, on the machine to itself after round 45's read,
with the contact kernel first. The port proper follows their numbers.
