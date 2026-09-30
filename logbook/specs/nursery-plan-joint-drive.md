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

## The tank, built on `joint-drive` (2026-09-30, evening)

Built while the questions above wait, so that the pilot is ready when they are answered. Nothing in
it has been run beyond smokes of a minute or less, at two threads.

- **`World.LaySnow(density, layers)`** (`WorldAssay.cs`, beside `PlaceCorpse`, with
  `GridField.SeedBed`): lays snow at a density in the lowest live cells of every column, booked into
  `EnergyIn` and the matter influx as a placed corpse is. The tank's own seed is matter, not snow, so
  without it a stomach has nothing to eat until something dies. Three tests in `LaySnowTests`.
- **`Evosim.Nursery --tank`** (`src/Evosim.Nursery/Tank.cs`): R1 to R5 as re-read above. Several
  bodies a world (`--tank-size 8`), net energy, shuffled into tanks `--draws 3` times a generation,
  a running mean over every episode swum, `--elites 8` carried over, a tournament of `--tournament 3`,
  no crossover, and `--evolve kind` (the whole genome, the set of cell types locked) or `brain`.
  Founders are drawn fresh and filtered by `--trunk absorptive`.
- **`--tank-bench`**, and the exam at a search's end: every genome under its own brain, frozen, full
  power, the knockout, brainless, and each `--drive-kinesis mode,bias,gain` brain. Every tank holds
  one brain. The kinesis under D135 is power `bias - gain x` on every degree of freedom with the bend
  centred. `x` is the `Chemical` reading (`level`) or a `Differentiate` neuron on it (`rate`).
- **Snow knobs:** `--snow` (J/m3, drawn half to double per episode unless `--snow-fixed`) and
  `--snow-layers`.
- **The trace:** `EVOSIM_TANK_TRACE=1` prints each body's start, keep, death and income.

**A finding that changes R2.** Under the floor's founder rule, which gives the config's purse times
the birth fraction plus the endowment capped by D124, a small stomach is left with about a joule once
it has grown. A founder pays for its growth out of the reserve on its first step. The cap is
`f × gate + growth`, so it was written with that in mind. The smoke's body 7 is an example: reserve
49.5 J and tissue 9.4 J at landing, then reserve 0.9 J and tissue 57.9 J half a second later. It
starved at 4 s under every brain, frozen included. At 300 s, 62% of the eight founders were dead
under their own brains and under frozen ones alike. Fable's arithmetic ("a non-eater of 0.12 W ends
at about 34 J of 70 and lives") left out the growth and the cap. So `--purse-rule` offers two
purses:

- `world`: the floor's rule as it stands.
- `grown`: the growth paid, plus `--purse-seconds` (the config's 600 by default) of the adult's
  standing watts, uncapped.

Under `grown`, every frozen body lived the 300 s. Which purse is the nursery's is the owner's
question; it was R2's.

**What the smokes showed, as smokes.** Eight stomachs, one draw, 300 s, work cost 0.5, and snow at
1.6 J/m3 on the bottom layer. The bodies started 0.75 to 2.25 m above the bed and so mostly sat above
the snow. At the body they read 0.04 J/m3 of the 1.6 laid. The pilot needs them started low
(`--above-bed 0.5`) or the snow laid on two layers.

With the grown purse, the full-power control paid 0.78 W of work and ended 149 J below its own brain
(se 66). The level kinesis at bias 1 and gain 8 pushed as hard as full power and did as badly,
because the snow it read was so thin that it read as drained everywhere. A three-generation search
of 16 stomachs cut the mean work from 0.36 W to 0.05 W and raised the mean net from −52 J to −20 J.
Selection learned to stop pushing where the snow did not pay for a push. That is the sitter Fable
warned of, and it is the right answer to that tank. It says the larder decides what the pilot can
find, so the snow's density and layers are the first thing to set on the bench.

## The larder, benched

*2026-09-30 afternoon, on the branch `joint-drive` at `8c73cbb`.* The control bench ran 48 stomach
founders in tanks of eight, under eight brains. Each brain had 16 held-out draws of 300 s. The work
cost was 0.5, the purse the grown one, and the bodies started 0.25 to 0.75 m over the bed. The
config is the still-water nursery config with D137's friction added at 0, which the new build
requires (`scratch/nursery/runs-jd/still-w2-f0`).

The first bench laid the snow at 1.4 J/m3, the bed's density in round 53, on the bottom layer
alone. Every brain lost money over the episode, and a body read 0.03 J/m3 at its own cell, a
fiftieth of what was laid. The trace showed why: the bodies float about a metre over the bed. That
is just above the one-metre layer the snow was laid in, and a neutrally buoyant body never sinks. The tank's whole larder
was 140 J among eight bodies whose adult keep is 0.4 to 1 W each. A grown body pays the adult's keep
from its first step, because the purse pays the growth at once, and the trace's upkeep column now
shows it.

Raising the snow on the bottom layer to 4, 12 and 36 J/m3 fed every brain more and never paid a
body to move. Laying it through the whole water column changed that. The table gives each brain's
mean net over the episode in joules.

| brain | 1.4 J/m3, bottom layer | 1.4 J/m3, the column | 12 J/m3, the column |
|---|---|---|---|
| its own, as drawn | −283 | −261 | −26 |
| frozen | −197 | −186 | −42 |
| full power | −417 | −398 | −129 |
| level kinesis, bias 1.36, gain 18 | −417 | −385 | +8 |
| rate kinesis, bias −0.5, gain 10 | −198 | −180 | +25 |
| rate kinesis, bias 0, gain 20 | −213 | −192 | +54 |

The standard error of a brain's difference from its own is about 20 J at 1.4 and 25 J at 12. At the
round's own density through the column, the rate kinesis at bias −0.5 beats full power by 218 J. It
beats frozen by 6 J, which is within the noise. At 12 J/m3 both rate kinesis brains end the episode
with more than they started, the first brains in any tank to do so. The one at bias 0 and gain 20
does best. It swims 17.5 m where a frozen body drifts 4.4, for 0.1 W of work, and ends 95 J above
frozen.

I think the reason is the cost of speed. Speed grows with power and work with its cube, so a metre
costs about the square of the power. At full power a metre costs about 7 J, and a one-metre cell at
1.4 J/m3 holds 1.4 J. A stomach drains its own cell within seconds, so what it eats is what it
passes through, and moving pays only when a metre holds more than it costs. That is inference from
the bench's numbers; I have not measured the cost per metre.

The stop rule held at both densities: the kinesis out-earned full power, so the pilot runs. It runs
twice, 200 generations each, on the column. At 1.4 J/m3 it is the round's larder, the one an
inoculant would meet. At 12 J/m3 there is a gradient for foraging to climb.

## The first pilot: a small body cruising blind

*2026-09-30, 15:36 to 15:52.* The pilot evolved 48 stomach founders for 200 generations. Their
bodies were free and their cell types locked, and the snow was the round's density through the column. The score
was net energy. In the first ten generations the mean net rose from −160 J to about −2 J, and the
work fell from 0.09 W to near zero. The best genome's adult scale fell from 1 to 0.61, about a fifth
of the volume. A body's keep is paid by its volume and its food taken through its surface, so where
food is scarce a smaller body loses less.

The owner asked whether the score should be energy per body size, and then ruled to keep net energy
and watch the size (2026-09-30). Per body size, a stomach's score is its income over its volume less
its keep, which grows without limit as the body shrinks, so the smallest body would always win. Net
energy has a best size, and that size grows with the food.

The exam ran the eight elites on 16 held-out draws under every control brain. Their own brains made
+2.1 J. Frozen, the same bodies made −3.9 J (5.9 J less, standard error 0.3), and at full power
−1.5 J. The knockout of the chemical sense and the brainless copy scored to the hundredth of a joule what their own
brains did, so nothing the brains do depends on a sensor. What evolved is a small body that cruises
slowly and blindly, 16.8 m for 0.008 W. The hand-wired rate kinesis at bias 0 and gain 20 did as
well as it did (+0.1 J, standard error 0.2). That answers the plan's question for this density: a
forager's sense buys nothing here that a slow cruise does not. The brain-only arm and the pilot at
12 J/m3 were started and stopped for round 54's diagnostic screens, and run again when the machine
is free.
