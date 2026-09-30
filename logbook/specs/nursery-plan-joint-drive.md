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

## The bench again, on D139's drive

*2026-09-30, 16:20, `joint-drive` at `a03a2ca`.* Everything above in this file after "The larder,
benched" ran with a silent neuron at half power, which D139 corrected the same afternoon: a joint now
pushes only when its neuron is above its threshold gene. The first pilot's blind cruise grew from
that default, since doing nothing was a swim. The bench was run again at 12 J/m3 through the column.

| brain | net over 300 s | path | work | snow at the body |
|---|---|---|---|---|
| rate kinesis, bias 0, gain 20 | +23 J | 7.5 m | 0.009 W | 0.72 J/m3 |
| rate kinesis, bias −0.5, gain 10 | +5 J | 6.1 m | 0.004 W | 0.67 J/m3 |
| level kinesis, bias 1.36, gain 18 | +2 J | 11.0 m | 0.11 W | 0.63 J/m3 |
| frozen | −42 J | 4.4 m | 0 | 0.52 J/m3 |
| its own, as drawn | −65 J | 7.0 m | 0.21 W | 0.61 J/m3 |
| full power | −129 J | 39.9 m | 1.05 W | 0.92 J/m3 |

The best hand-wired forager beats frozen by 65 J (standard error about 22) and full power by 152 J.
It travels hardly further than a frozen body, but it sits in snow two fifths richer, because it moves
only while its reading falls. The random brains now lose to frozen, since those that push waste the
work. The pilots run again on this drive, the brain-only arm first.

## The brain-only pilot on D139's drive: a faster blind cruise

*2026-09-30, 16:24 to 16:39.* 48 stomach founders, bodies fixed, 200 generations at 12 J/m3 through the
column. Selection still chose among the founders' bodies, since each brain rides the body it was drawn
with, and by generation 4 the population was copies of body 39, which had earned +176 J under its own
random brain in generation 0. The exam ran its eight best brains on 16 held-out draws.

| body 39's elites | net | work | path | snow at the body |
|---|---|---|---|---|
| their own brains | +203 J | 0.31 W | 40.8 m | 2.10 J/m3 |
| full power | +193 J | 0.39 W | 43.8 m | 2.17 J/m3 |
| frozen | +88 J | 0 | 4.2 m | 1.06 J/m3 |
| knockout and brainless | +203 J | 0.31 W | 40.8 m | 2.10 J/m3 |

The brains beat frozen by 116 J (standard error 6) and full power by 10 J (standard error 6). Their
knockout and brainless copies scored what they did, so no sense is read: the brains hold about four
fifths of full power all the time. For this body at this density swimming pays, and a blind cruise
collects it.

My reading is that the larder cannot reward a sense while it is even. A body that keeps moving through
an even larder always meets fresh snow, so knowing where the snow lies buys nothing. The round's snow
is patchy, its patchiness 0.44 to 0.56 in round 53, and the tank's is flat. The next step is snow laid
in patches, as the farm lays matter in islands (D109), and the pilots again on that.

## Snow in islands: a brain at last, but not a sense

*2026-09-30, 16:45 to 17:20.* `LaySnowIslands` lays the snow on a noise map, as D109 lays matter,
with `--snow-islands wavelength,cover`. Three pilots ran on it, each 200 generations of 300 s episodes
with the exam on 16 held-out draws. The first two were the brain-only arm as before, 48 founders with
one brain each.

| pilot | larder | the exam's winner | own | knockout | brainless | frozen | full | best kinesis |
|---|---|---|---|---|---|---|---|---|
| rich islands | 12 J/m3, 3 m, cover 0.25 | body 39 | +251 J | +251 J | +251 J | +131 J | +251 J | +191 J |
| sparse islands | 3 J/m3, 5 m, cover 0.1 | body 39 | +53 J | +53 J | +23 J | +17 J | −26 J | +36 J |

On rich islands body 39 took the population again with a full-power cruise. On sparse, poor islands it
took it with something new. Its brain beats its brainless copy by 30 J (standard error 6.5). It spends
0.024 W and ends among snow at 0.78 J/m3, where frozen ends at 0.56. Its knockout of the scent scores what
it does to the hundredth of a joule, so the brain reads something other than the scent. My guess is
the body's own joint and orientation senses, setting a stroke's rhythm; it is not yet tested. It beats
every hand-wired kinesis by 17 J or more.

Body 39 winning every pilot hid whether any other body could find a
sense. `--per-body` (`3de4108`) keeps each body's share of the population and holds each tournament
among one body's brains; the exam then takes each body's best. The third pilot ran it on the sparse
islands, with 12 fresh founders and 8 brains each.

Every one of the 12 bodies evolved a constant: own, knockout and brainless scored the same, to the joule,
on every body. All 12 lost energy in this larder (own −3 to −161 J). That is why body 39 swept the
pilots before. On five bodies the hand-wired rate kinesis beat what the search found, paired over the 16
draws:

| body | own | rate kinesis −0.5, 10 | rate kinesis 0, 20 |
|---|---|---|---|
| 10 | −3 J | +65 J (se 11) | +56 J (se 8) |
| 3 | −5 J | +28 J (se 5) | +23 J (se 6) |
| 7 | −48 J | +16 J (se 5) | +11 J (se 3) |
| 0 | −77 J | +15 J (se 7) | +10 J (se 6) |
| 5 | −91 J | +11 J (se 7) | +12 J (se 8) |

So a sense pays on those bodies and the search did not reach it. My reading, an inference: a constant is
one mutation from any brain. A kinesis needs the scent wired to the drive with the right sign and a
large gain. So selection takes the constant and stops. Eight brains to a body is a thin search. Two
follow-ups test the reading. Bodies 3, 7 and 10 with 32 brains each ask whether a wider search reaches
the sense. The same three bodies with the search started from the rate kinesis (`--start-kinesis`,
`b22fda7`) ask whether a brain that begins on the sense keeps it and improves it. The second is a
diagnostic; whether a brain whose search began hand-wired may be inoculated is the owner's question.

Round 54 waits on this (D140). The owner asked for episodes of 600 s and then 1,200 s on the per-body
pilot, since a longer life may make finding the next island worth more than cruising through one.
