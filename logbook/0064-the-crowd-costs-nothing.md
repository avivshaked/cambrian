# 0064 — The crowd costs nothing

*2026-09-05. Not a round: D076's measurement. The owner ruled that a world without
contact cannot work; before the world rules are written, the shared-space spike measured
what contact costs. Harness commit `80fcedd` (`SharedSpaceSpike.cs`, spec
`scratch/shared-space-spike-spec.md`), run on worker 6 alone on the machine after round 24
ended; results in `runs/spike-shared-space/2026-09-05-202709-matrix/` and the agent's report
`scratch/shared-space-spike-report.md`.*

## The question

Creatures have been tiled 100 m apart since Milestone 1 and have never touched. D076
puts them in one volume. §5A.9's throughput was measured to 512 creatures with no
contacts and expected contact cost to be "rare and local". The spike asked: one shared
volume, creature-to-creature collisions on, real founders with the fluid on and the test
sine driving every joint, at 250 to 2,000 bodies and three footprints, against today's
tiling — what does it cost, what population holds real time, where does density bite,
and does anything blow up.

## The answers

**1. There is no cost ratio.** Shared against tiled at the same N: 0.88× to 1.12×, the
same width as repeat noise (three repeats of one cell: 0.652 / 0.678 / 0.712 ms per step).
The decisive cell: at N = 1,000 the 50 m footprint, which recorded *zero* contacts, reads
1.12×, identical to the 10 m footprint that had the only contacts in the matrix. A ratio
that is the same with and without contacts is measuring the machine. Where the step
actually goes at 2,000 tiled bodies: **the drag loop 56%, the PhysX solver 33%,** settle
11%. Our fluid is the cost, not the engine.

**2. Real time holds to about 2,900 bodies, shared or not.** At N = 2,000: tiled 6.11 ms
per 0.01-s step, shared 20 m 6.28, shared 50 m 6.37 — all with a third of the 10 ms
budget in hand. Scaling is ~N^1.27 over the top octave, so 1× real time extrapolates to
N ≈ 2,900. Shared space does not move the ceiling.

**3. Contacts are events, not a rate.** The largest count anywhere is 0.39 pairs per step
at 1,000 bodies in 10 × 10 × 60 m (0.17 bodies/m³): one pair resting together for two
fifths of the run. The instrument-check cell at 42 bodies/m³ read 13.5 pairs per body per
step, so a 250× density increase buys ~35,000× the contacts — the candidate footprints sit
three orders of magnitude below the knee. The layer question, isolated (`tiled-nc`, one
layer against ignoring layers at N = 1,000): the physics pass moves 2.4%, less than the
fluid pass moves between two runs of the same thing. No performance argument either way.

**4. Nothing diverged.** Zero non-finite bodies in 25 cells; the fastest body 1.52 m/s in
shared and tiled cells alike (the sine, not a collision).

## The two findings that were not the question

- **The footprint is set by packing, not cost.** 2,000 founders at mean bounding radius
  0.63 m did not fit in 10 × 10 × 60 m: 35% fill of 6,000 m³, the random-sequential
  jamming fraction in three dimensions; 1,000 fit with 17.7 rejections per body. 20 × 20
  × 60 at 2,000 is 9% fill and placed easily. **Today's `EVOSIM_AREA` 100 cannot hold the
  populations the world already runs**; the default 400 can. Bounding spheres, not
  solids — but `Ecosystem.Build` must place a newborn without overlap, and depenetration
  is a force (logbook/0007), so it is a real constraint on the world.
- **A shared volume needs a horizontal boundary rule.** Bodies drifted out of the 10 m
  and 20 m boxes in twelve seconds on the 0.3 m/s current alone; x and z are unbounded and
  tiling hid it. Wrap, reflect or a restoring current is a world rule, and the footprint
  and the boundary are one decision.

## What it means

The throughput objection to shared space, which was the reason the predation proposal
had hedged toward an encounter rule, is gone: the engine handles the crowd, and the cost
that binds is our own drag loop, which is the same in both worlds and is where any
optimisation belongs. What remains is design, not cost: how big the water is, how the
four patches become regions, what happens at the edge, and how a newborn is placed
beside its parent instead of on a lattice. The footprint proposal carries those; its
survey is `scratch/footprint-survey.md`.
