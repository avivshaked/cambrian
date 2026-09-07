# 0064 — The crowd costs nothing

**2026-09-05**  ·  D076's measurement, not a round

The owner ruled that a world without contact cannot work. Before the world rules are
written, the shared-space spike measured what contact costs, and the answer is that it costs
nothing measurable. Shared against tiled at the same population lands between 0.88 and 1.12
times, which is the width of repeat noise. The cell with zero contacts reads the same as the
cell with all of them. What binds the step is our own drag loop, at 56% of it.

The harness is commit `80fcedd`, in `SharedSpaceSpike.cs`.

It follows the spec in `scratch/shared-space-spike-spec.md`.

It ran on worker 6, alone on the machine, after round 24 ended. The results are in
`runs/spike-shared-space/2026-09-05-202709-matrix/`, and the agent's report is
`scratch/shared-space-spike-report.md`.

## The question

Creatures have been tiled 100 m apart since Milestone 1 and have never touched. D076 puts
them in one volume.

§5A.9's throughput was measured to 512 creatures with no contacts, and it expected contact
cost to be rare and local.

The spike asked what one shared volume costs, against today's tiling. It ran
creature-to-creature collisions on, with real founders, the fluid on and the test sine
driving every joint, at 250 to 2,000 bodies and three footprints. What does it cost, what
population holds real time, where does density bite, and does anything blow up.

## The answers

### 1. There is no cost ratio

Shared against tiled at the same population reads 0.88 to 1.12 times, the same width as
repeat noise. Three repeats of one cell read 0.652, 0.678 and 0.712 ms per step.

The decisive cell is at N = 1,000, where the 50 m footprint recorded zero contacts and reads
1.12 times. That is identical to the 10 m footprint, which had the only contacts in the
matrix. A ratio that is the same with and without contacts is measuring the machine.

Here is where the step actually goes at 2,000 tiled bodies. The drag loop takes 56%, the
PhysX solver 33% and settle 11%. Our fluid is the cost, and the engine is not.

### 2. Real time holds to about 2,900 bodies, shared or not

At N = 2,000 the tiled world takes 6.11 ms per 0.01-s step. The shared 20 m world takes
6.28, and the shared 50 m world 6.37. All three keep a third of the 10 ms budget in hand.

Scaling runs at about N^1.27 over the top octave, so 1× real time extrapolates to N ≈ 2,900.
Shared space does not move the ceiling.

### 3. Contacts are events rather than a rate

The largest count anywhere is 0.39 pairs per step, at 1,000 bodies in 10 × 10 × 60 m, which
is 0.17 bodies/m³. It is one pair resting together for two fifths of the run.

The instrument-check cell at 42 bodies/m³ read 13.5 pairs per body per step, so a 250-fold
density increase buys about 35,000 times the contacts. The candidate footprints sit three
orders of magnitude below the knee.

The layer question was isolated in `tiled-nc`, one layer against ignoring layers at N =
1,000. The physics pass moves 2.4%, which is less than the fluid pass moves between two runs
of the same thing. There is no performance argument either way.

### 4. Nothing diverged

There were zero non-finite bodies in 25 cells. The fastest body read 1.52 m/s in shared and
tiled cells alike, which is the sine rather than a collision.

## The two findings that were not the question

The footprint is set by packing rather than by cost. At a mean bounding radius of 0.63 m,
2,000 founders did not fit in 10 × 10 × 60 m. That is 35% fill of 6,000 m³, the
random-sequential jamming fraction in three dimensions. A thousand of them fit, with 17.7
rejections per body, and 20 × 20 × 60 at 2,000 bodies is 9% fill and placed easily.

So today's `EVOSIM_AREA` 100 cannot hold the populations the world already runs, and the
default 400 can. These are bounding spheres rather than solids. But `Ecosystem.Build` must
place a newborn without overlap, and depenetration is a force (logbook/0007), so it
constrains the world for real.

A shared volume also needs a horizontal boundary rule. Bodies drifted out of the 10 m and 20
m boxes in twelve seconds on the 0.3 m/s current alone. The axes x and z are unbounded, and
tiling hid it. Wrap, reflect or a restoring current is a world rule, and the footprint and
the boundary are one decision.

## What it means

The throughput objection to shared space is gone. It was the reason the predation proposal
had hedged toward an encounter rule, and the engine handles the crowd. The cost that binds
is our own drag loop, which is the same in both worlds and is where any optimisation
belongs.

What remains is design rather than cost. How big the water is, how the four patches become
regions, what happens at the edge, and how a newborn is placed beside its parent instead of
on a lattice. The footprint proposal carries those, and its survey is
`scratch/footprint-survey.md`.
