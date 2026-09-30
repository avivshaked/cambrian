# Planning the nursery for the joint drive: the agent's draft (2026-09-30)

*A draft for the owner, who asked to "start planning the nursery" once D135's drive swam. It re-reads
Fable's world-derived nursery (`logbook/specs/fable-propose-world-nursery.md`, rulings R1 to R10,
written for the torque drive) against what D135's build measured (`logbook/specs/joint-drive-build.md`).
Nothing in it is ruled. The questions it ends with are the owner's.*

## What D135 changes for a nursery

1. **Bodies move by default.** Under their own random brains 60 of 100 founders swim over 1 cm/s in
   still water, and a founder at full power swims a median 7.8 cm/s. The nursery no longer has to find a
   stroke. What a brain can still be selected for is when to push, how hard, and which way to bend.
2. **Pushing is free until the work cost is set.** The build's configs carry `workCostMultiplier 0`,
   so a body that pushes flat out all the time loses nothing, and "always full power" is as good as any
   brain in any world where moving helps at all. Rule 2 of `propose-foraging.md` (a cruising stroke
   costs one to two times the body's standing keep) was ruled for round 54 and carries over under D135
   (ruling 4). The nursery needs it on, or it can only select for full power.
3. **The pool has to be new.** Every genome recorded before D135 is format 10 and refused, so the
   nursery cannot start from round 52's or 53's bodies. It can start from founders drawn as the world
   draws them (`Evosim.Nursery --founders`), and every founder the options build is a trunk and a
   one-joint tail. A chain, and the fish-or-snake look of ruling 9, would have to evolve in the
   nursery, or come from a short farm run under D135 first.
4. **The brain has two outputs per joint now**, and a silent neuron is half power. So a child that
   loses a neuron strokes at half power rather than stopping, which makes the population's baseline a
   moving one.

## Fable's rulings, re-read

| | Fable's recommendation | Under D135 |
|---|---|---|
| R1 score | net energy over the episode | carries, and matters more: with the work cost on, net energy is what prices a push |
| R2 purse | the founder's, 600 s of standing watts | carries: the Energy sense then moves when a push costs |
| R3 what evolves | whole genome with the cell types locked, and a brain-only arm | carries, with the four drive genes mutating (`EVOSIM_DRIVE_GENE_CHANCE` above 0) |
| R4 tank | 8 bodies a tank, reshuffled each generation | carries |
| R5 algorithm | generational, tournament of 3, 8 elites on running means, no crossover | carries |
| R6 larder | laid from the round's densities, with a `LaySnow` assay | carries |
| R7 kinds | stomachs first, mouths second, leaves later | carries; founders are filtered by the trunk's cell type |
| R8 controls | run with the pilot and gate the claim, not the run | carries, with the controls below |
| R9 a leaf's sense | not now | carries |
| R10 load | four threads during the hold, twelve after | carries |

## Controls under D135

Each beside every pilot, on held-out seeds and mates: frozen (power -1 on every joint), full power
with the bend centred (the body that pushes all the time, which the work cost should beat), the
ancestor, the knockout of the senses the winner reads, and one hand-wired forager that the world can
express. For a stomach the simplest is a kinesis on its own cell: power high while the chemical
reading falls and low while it holds or rises (`Differentiate` on `Chemical`, one neuron), which is
the owner's first rung, "sense the depletion and move anywhere". If that hand-wired brain does not
out-earn full power once the work cost is on, no evolved brain will, and the nursery stops and says so.

## The order I would propose

1. Set the work cost on the bench: the reference swimmer at full power pays one to two times its
   keep.
2. The hand-wired kinesis against frozen and full power, stomachs in a tank laid with snow.
3. A one-hour pilot: 48 stomach founders, 8 a tank, 300 s episodes, net energy, the drive genes
   mutating.
4. The examination against the controls; then the owner decides what goes to a round.

At about 0.4 s per body-episode, a generation of 48 genomes in three tank draws is about a minute on
one thread, so a hundred generations is about half an hour at four threads (an estimate from the
nursery's measurement 1, not yet timed under D135).

## The questions for the owner

1. The work cost in the nursery: on at rule 2's price, as ruled for round 54? Without it the nursery
   can only select for full power.
2. The pool: founders drawn fresh (a trunk and one tail each), or a short farm run under D135 first
   to give the nursery more varied bodies?
3. Fable's R1 to R10 as re-read above, with the controls and the order: agreed?
