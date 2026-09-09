# 0080 — When a joint is free

**2026-09-09**  ·  round 33, pre-registered before launch; a hypothesis test with one knob set to nothing

Every round on record ends with the swimmers gone, and round 31 (logbook/0077) named the
wall: no jointed body survives founding, before there is anything to eat, so selection
never has a swimmer to work on. The campaign's standing explanation is a price. A joint
costs more than it earns, so it is selected out (logbook/0017, 0027, D082). That
explanation has never been tested on its own. It is assumed. The owner asked for the test:
make the joint free and see what the world does with it.

## The hypothesis, stated so it can fail

If joints vanish because they cost more than they earn, then a joint that costs nothing is a
neutral trait. Its share should start at the founder draw, about two bodies in five, and
drift with the realisation, thinning or thickening as lines rise and fall, but not
collapse. If the share still collapses to zero in the first few thousand seconds with
nothing charged, the price was never the reason, and the cause is something we have not
named: the solver killing newborn jointed bodies, a mutation that removes joints more
often than it adds them, or a jointed body sinking or drifting out of the food.

## What "free" is in this world

A joint costs in four places. Three go to zero by knob and one cannot.

- The muscle's standing charge, `EVOSIM_IDLE`, 0.02 W per newton of capacity, to 0.
- The stroke's mechanical work, `EVOSIM_WORK_COST`, at a quarter since D082, to 0.
- The neurons and connections that drive it, `EVOSIM_NEURON_COST` 0.005 W and
  `EVOSIM_CONNECTION_COST` 0.001 W, to 0. This also frees every brain in the world,
  including the ones behind a sense on a rigid body, and the sense share will move with it.
  That is a confound for the sense and not for the joint, and the round reads the joint.
- The part the joint hangs, which is tissue, pays tissue's upkeep and forfeits the light
  income a leaf in its place would earn. That stays. It is the body, not the joint, and
  making it free would make every body free. So the honest form of the test is "a joint
  costs no more than the part it moves", and the prediction is read against the founder
  share rather than against zero.

## What is held

Round 32's launcher (logbook/0079): the grid at 1 m and 5 m, corpses at 0.005 per second,
detritus mixing 0.02, added mass 0.5, the three senses on, 30,000 s, five seeds. The
launcher is `scratch/launch-r33.ps1`, round 32's with the four knobs at zero.

## Validity checks

- **V1 header.** Every token of round 32's, with `idle 0 W/N`, `work x0`, `neuron 0 W + 0
  W/input`. Read from the report, never from the launch command.
- **V2 identities.** `audit` 0.0000% and `mat resid` 0 on every row.
- **V3 manifests.** `ended budget`, `physicsJobWorkers 0`, `gitDirty false`, one build;
  `diverged` read per arm with the usual caveat, and this round reads it closely, since a
  free joint that dies of the solver is the second hypothesis.

## Predictions

The reference is round 32's same seed, the same world with the joint priced. The reading is
`jnt inh`, bodies alive whose parent had a joint, as a share of `alive`, sampled every
1,000 s; and `jointed`, the expressed share, read only where the floor is not firing.

| # | prediction | falsified by |
|---|---|---|
| M0 | **the world stands**: `alive` at 30,000 s within 0.5 to 1.5 of round 32's same seed in every arm; D063 as amended is read but not required, since a free brain and a free stroke change the stomachs' economics too | `alive`, the scorer |
| M1 | **the price was the reason**: `jnt inh` over `alive` at 30,000 s is at least 0.1 in at least 3 of 5, and at least 10 bodies at every sample of the last 6,000 s in those arms (round 32 is predicted to read 0 in 4 of 5, 0079's M6) | `jnt inh`, `alive` |
| M2 | **the founding scramble is survived**: `jnt inh` at 5,000 s is at least a quarter of its value at 1,000 s in at least 3 of 5 (every priced world has lost its last joint by about 5,000 s) | `jnt inh` at 1,000 and 5,000 s |
| M3 | **a free joint does not diverge more**: `diverged` per arm at most 2 in every arm, against round 32's same seed | the manifests |
| M4 | **movement, if any, is drift**: in any arm where M1 holds, `spd jnt` is within 1.5 of `spd rig` over the last 6,000 s, since nothing here pays a stroke and no round has yet selected a swimmer | `spd jnt`, `spd rig` |

## The two-sided readings

- **M1 and M2 hold:** the price is the reason. The joint is a trait the world can carry
  when it costs nothing. The question becomes how much price it can bear, and growth
  (`fable-propose-growth.md`, round 34) asks that by making a newborn joint small. The
  campaign's explanation stands, tested.
- **M1 fails and M2 fails:** the price was never the reason. Before anything is priced
  again, three things are read. The divergence dumps of this round, for whether newborn
  jointed bodies are the ones that die. The lineage rows, for the rate at which mutation
  adds a joint against the rate at which it removes one. The depth and drift of jointed
  bodies against rigid ones in the first 5,000 s. Growth is then read with that in hand.
- **M2 holds and M1 fails:** free joints survive founding and are then lost slowly, which
  is drift or a slow disadvantage in the body itself, the part's forfeited light. Read the
  jointed share's decay against a neutral-drift expectation from the population's turnover.
- **M3 fails:** the solver is part of the answer whatever M1 says, and the newborn mass floor
  in the growth proposal is read against these dumps.
- **M4 fails, jointed bodies move faster than the drift:** a free stroke is used, and the
  round after prices the stroke alone with everything else free, to find its price.

## What the round does not ask

Whether a swimmer eats better than a sitter. Nothing here pays a stroke, and the grid's
prize for moving (logbook/0078) is read only once a swimmer exists to take it.
