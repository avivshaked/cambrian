# Planning the nursery for the joint drive: the agent's draft (2026-09-30)

*A draft for the owner, who asked for the nursery's planning to start once D135's drive swam. It re-reads
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
the owner's first rung, sensing the depletion and moving anywhere. If that hand-wired brain does not
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

## Longer lives, a wider search, and a sense worth nothing here

*2026-09-30, 17:20 to 18:22.* The per-body pilot ran again at 600 s and at 1,200 s, as the owner
asked, and then three pilots on bodies 3, 7 and 10 with 32 brains each.

At 600 s no body read the scent: the knockout scored what its own brain did on all 12. The rate kinesis
beat the evolved brain on seven bodies, by up to 75 J on body 10 (standard error 11). Bodies 3 and 9
found body 39's trick, a brain that beats its brainless copy by 30 J. Every body ended deep in deficit
(own −91 to −494 J), because this larder does not keep these bodies for 600 s.

At 1,200 s nearly every body starved at 3 J/m3 before the end: 4 % survived under their own brains
and 2 % frozen. Every brain lost the purse, so the exam could not tell them apart. The run measured only
the larder. It runs again at 12 J/m3.

The wider search gave the same three bodies 32 brains each, in three arms. The first kept the brains as
drawn. The second took larger mutation steps: `scalarChance` 0.2 and `scalarStdDev` 0.5, against 0.08 and
0.15. The third started the search from the rate kinesis. The exam's draws are the same in every
arm, since frozen scored identically in each, so the arms' own brains pair draw by draw.

| body | as drawn, own | larger steps, against as drawn | from kinesis, against as drawn | from kinesis, its knockout |
|---|---|---|---|---|
| 3 | +10 J | −1 J (se 3) | +2 J (se 3) | −42 J (se 7) |
| 7 | −14 J | −4 J (se 2) | −2 J (se 2) | +16 J (se 4) |
| 10 | +44 J | −14 J (se 7) | +5 J (se 6) | −54 J (se 6) |

With 32 brains, body 10's blind brain found +44 J, where 8 brains had found −3 J, and the kinesis's 65 J
lead was gone. Both arms as drawn and with larger steps evolved blind constants. Started from the
kinesis, bodies 3 and 10 kept the sense: cutting the scent costs them 42 and 54 J. They earn no more
than the best blind brain. The larger steps found nothing the ordinary ones did not.

This corrects my reading of 17:30. I had guessed that the scent reads too faintly (x / (x + 10 J/m3), so
0.03 to 0.07 over these islands) for small mutation steps to reach the gains a kinesis needs. The larger
steps did not find a sense, and a brain handed the sense did not profit from it. In this tank a sense
is worth nothing over a tuned blind stroke. The 65 J came from a search too thin to find the blind
stroke.

My reading of why, which is inference: the tank is 10 m across and its islands 5 m, and the bodies
are planted in the food (`foundersFollowFood`). A slow blind stroke stays in its island long enough,
and a fast one meets another within a few metres. The round lays its matter on a 60 m map at cover
0.1 in 22,000 m2, where a body in a desert is tens of metres from food. The next bench is a 40 m tank
with 20 m islands at cover 0.1. One arm plants the bodies in the food and the other drops them anywhere.
Blind constants (`393f522`) stand beside the kinesis.

## A tank the size of an island: the first larder where a sense pays

*2026-09-30, 18:22 to 19:05.* The 1,200 s pilot ran again at 12 J/m3 on the same small tank, and then
the bench in a tank 40 m across.

At 12 J/m3 and 1,200 s most bodies lived (79 % under their own brains). No brain read the scent on any
of the 12 bodies. The brains were blind cruises at 0.13 W over 79 m, and they beat frozen by 165 J
(standard error 21). The rate kinesis led the evolved brain on three bodies by 90 to 108 J, with
standard errors of 25 to 49. Eight brains to a body is the thin search that made the same kind of lead
vanish on body 10 at 32 brains, so I read these leads as unproven.

The bench tank is 1,600 m2 and 5 m deep, with snow at 3 J/m3 in islands of 20 m at cover 0.1. It ran
600 s episodes, 12 fresh founders under 12 brains on 16 held-out draws. Four blind constants stand beside the kinesis, at 0.15,
0.3, 0.5 and 0.7 of the neuron's range. The arms differ only in the start: planted in the food
(`foundersFollowFood`, as every tank so far) or dropped anywhere. Each brain is paired against the
best of the four constants, which is 0.3 in both arms and was picked after the fact, so the pairing
favours the blind side.

| start | rate kinesis −0.5, 10 | rate kinesis 0, 20 | level kinesis 1.36, 18 |
|---|---|---|---|
| planted in the food | +61 J (se 11) | +55 J (se 11) | −83 J (se 14) |
| dropped anywhere | −4 J (se 8) | −1 J (se 8) | −37 J (se 11) |

Planted in an island, a body that eases its stroke while the scent rises earns 61 J more over 600 s
than the best blind body. Dropped anywhere, every brain starves in the deserts (−258 to −311 J), and a
searcher that pushes in drained water pays for the swim without reaching an island in time. So the scent
buys a body the means to stay on its food. It does not help it find food. The round plants its founders in the food
too, and its children are born beside their parents.

Two arms of the per-body search run on the planted tank from 19:05, 12 bodies with 16 brains each and
600 s episodes. One starts from the brains as drawn and the other from the rate kinesis. Their exams
hold the constants.

## Evolution in the big tank: a gait beats the scent

*2026-09-30, 19:05 to 20:24.* Both arms ran 200 generations on the planted 40 m tank, 12 bodies with 16
brains each, and their exams shared the bench's draws, so the arms pair draw by draw.

The blind arm's brains averaged +394 J. They beat the best blind constant by 210 J and the rate kinesis
by 149 J (standard error 32). No body read the scent: the knockout moved no body by more than its
standard error. Four bodies grew brains that read other senses. On bodies 0, 3, 8 and 9 the brainless copy
lost 352, 183, 230 and 317 J (standard errors 59, 46, 37 and 144).

The arm started from the rate kinesis averaged +346 J, 48 J below the blind arm (standard error 18).
Four of its bodies kept the scent, and cutting it cost bodies 0, 3, 7 and 10 between 168 and 357 J. On
each of those four bodies the blind arm's brain earned as much or more, by 12 to 219 J.

| body | blind arm | its brainless copy | from kinesis | its knockout | from kinesis, against blind |
|---|---|---|---|---|---|
| 0 | +247 J | −352 J (se 59) | +145 J | −189 J (se 25) | −102 J (se 51) |
| 3 | +313 J | −183 J (se 46) | +301 J | −168 J (se 21) | −12 J (se 33) |
| 7 | +556 J | +6 J | +337 J | −221 J (se 20) | −219 J (se 47) |
| 8 | +472 J | −230 J (se 37) | +238 J | +4 J | −234 J (se 52) |
| 9 | +762 J | −317 J (se 144) | +668 J | +60 J | −94 J (se 91) |
| 10 | +651 J | +23 J | +585 J | −357 J (se 72) | −65 J (se 33) |

In the one larder where the scent pays over a constant, evolution found something that pays more: a
stroke that reads the body's own state. Both arms swam more as they evolved, from 0.04 to 0.06 W at the
start to 0.14 to 0.19 W at the exam. Which senses the gaits read is not yet measured. A bench that
knocks out one channel at a time on the blind arm's elites would say. The blind arm's brains are the first
nursery brains that are neither a constant nor hand-wired, and they beat frozen by 334 J.

## The gaits in the round's water: they do not carry over

*2026-10-01, 00:36 to 03:10.* The exam of the big tank does not save its genomes, so the blind arm ran
again at 16 threads to recover them. Every generation's row and every exam row came back byte for byte,
at twice the threads: the tank is deterministic. Its exam now writes `tank-exam-genomes.tsv`.

The confirmation benched the 12 gaits on 32 fresh draws in the still tank and on 16 in two tanks with
the round's water. Each brain is paired against a constant at 0.5, the best of four in every tank.

| water | the gaits | against half power | against frozen |
|---|---|---|---|
| still, 5 m, fresh draws | 404 J | +201 J (se 33) | +329 J (se 19) |
| the round's current, 0.1 m/s | 206 J | +4 J (se 17) | +55 J (se 11) |
| the current and the round's 25 m depth | 337 J | −21 J (se 25) | +170 J (se 15) |

Knocked out one sense at a time in still water, only the energy sense cost them anything (−42 J,
standard error 31). The brainless copy lost 88 J, and my list left out the depth sense, so I guess the
gaits read depth too. In the round's kinds of water they swim no better than a steady half power. By
the bar set before the bench, no gait qualified for round 54.

The six bodies best in the deep water were evolved again there, 16 brains each for 100 generations,
from their gaits. The exam put them 93 J over half power (standard error 48) and 275 J over frozen. Per
body, none beat its own best constant by two standard errors, and a constant beat three of them by 26 to
105 J. Their brainless copies scored what they did (−3 J, standard error 3): the current made them
blind. In the same exam the rate kinesis, bias 0 and gain 20, scored 427 J against their 435 J (a
difference of 8 J, standard error 18). In the round's water a sense pays as much as the best blind
swim the search could find. Two arms test whether evolution keeps or finds it there: the same six
bodies from the kinesis, and from their random brains.

## The scent in the round's water: kept on one body, worth no more than a steady swim

*2026-10-01, 03:10 to 05:48.* The same six bodies, 16 brains each, 100 generations in the tank with the
round's current and depth. One arm started from the rate kinesis (bias 0, gain 20), the other from the
bodies' own random brains. Each brain is paired below against its body's best constant of five (0.3 to
0.7), picked after the fact.

| body | from random: own | against the best constant | from kinesis: own | against the best constant | its knockout of the scent |
|---|---|---|---|---|---|
| 0 | 439 J | +6 J (se 6) | 325 J | −107 J (se 31) | −4 J (se 2) |
| 1 | 444 J | +16 J (se 7) | 452 J | +24 J (se 8) | −5 J (se 2) |
| 3 | 283 J | −191 J (se 62) | 391 J | −84 J (se 60) | +195 J (se 39) |
| 4 | 169 J | −315 J (se 103) | 388 J | −96 J (se 33) | −14 J (se 11) |
| 8 | 391 J | −8 J (se 27) | 461 J | +63 J (se 48) | −4 J (se 2) |
| 10 | 598 J | +7 J (se 45) | 286 J | −304 J (se 59) | −29 J (se 4) |

The last column is the brain less its knockout. From random brains every body evolved a blind swim,
as in every tank before. From the kinesis only body 3 kept the scent, and cutting it costs that brain
195 J. Body 3's brain still trails the body's best constant by 84 J (standard error 60). Averaged, the
arm from the kinesis ended at 384 J, under the kinesis it started from (427 J, standard error of the
difference 61), so 100 generations made the hand-wired brain worse.

My reading, which is inference: two 600 s draws a generation and one elite a body leave selection
following luck. The generation means swung between 515 and 788 J late in both arms, and the leaders'
running means rest on two episodes. Before the nursery is asked again for an inoculant, its
selection needs more draws for each brain, or a longer life, so that a brain's score says more about
the brain than about its draws. No brain from the nursery is fit to inoculate on 2026-10-01.

## The noise a brain's score carries: pairing cancels the snow, not the luck

*2026-10-01, 13:43 to 14:11, D142's first step.* I benched the six deep-water bodies' own
brains from `pilot-deep-own` on 64 paired draws in the round's water, beside their constants at 0.3, 0.5
and 0.7 and the rate kinesis. The bench ran twice at 14 threads, 14 minutes each. The first drew the
snow's density from half to double, as training does, and the second held it fixed. The chain that ran them is
`scratch/nursery/chain-noise.ps1`.

A brain's score over one 600 s episode spreads widely, and most of that spread is the draw, shared by
every brain that swims it. Selection compares brains on the same draws, so what matters is the spread of
the difference between two brains on one draw. The table gives both, as medians over the six bodies.

| | one episode's spread | a brain less its body's best constant, per episode | the same at 2 draws | at 16 | at 32 |
|---|---|---|---|---|---|
| the density drawn | 494 J | 148 J | 105 J | 37 J | 26 J |
| the density fixed | 337 J | 147 J | 104 J | 37 J | 26 J |

Fixing the density narrows a single episode by a third and leaves the paired difference where it was:
pairing already cancels it. So `--snow-fixed` buys nothing for selection on paired draws, and the noise
left is the draw's own luck (where the snow lies, where the corpses fall, who shares the tank). The
evolved brains sit 1 to 41 J from their bodies' best constants on five bodies (body 4 is 189 J under).
At two draws a generation a brain's paired score carries a standard error near 105 J, two to five
times the differences selection must find. Telling 40 J apart at two standard errors needs about 55
paired draws; 16 draws reach 37 J and 32 reach 26 J. The pool's earlier benches, on other genomes and
16 draws, gave 161 to 255 J for the same spread.

## Two corrections to this file, found in a review of the code

*2026-10-01 evening. A review of the nursery's code for the next proposal found both; I checked each
against the code on `joint-drive`.*

Every tank episode in this file also laid 5 corpses of 40 J on the bed. They are the program's defaults
(`Program.cs:55-56`), and no script set the number of corpses. The runs' own logs
say so in their first line ("5 corpses of 40 J"). So the larder sums above leave out 200 J an episode,
and the bench of 2026-09-30 that called the tank's whole larder 140 J undercounts it. I have not checked
whether a stomach gains from the corpses, for example as one dissolves into its cell's water. If it does,
every pilot's larder was richer than this file says.

Fixing the snow's density changes more than the density. The density's draw takes one number from the
episode's stream before the bodies' heights and the corpses' places are drawn (`Tank.cs:95`, then lines
110 and 167). With the density fixed that number is not taken, so every height and every corpse moves. The
noise bench's two arms therefore swam different layouts as well as different densities. Within each
arm the paired numbers stand. Across the arms, the single-episode spreads compare layouts too. The paired
spreads still agree (148 J and 147 J), so the conclusion that pairing cancels the density's share holds,
on different layouts rather than the same ones.

## The drive-sense arm, stated before it starts (2026-10-01 evening)

D144 gives a brain three new inputs per joint: the stroke it delivers, the hold left, and the ramp still to
go. D145 runs it as the next arm. Its command is the control's (`pilot-deep-own16`) on nursery-v8, with the
drive sense on (`runs-jd/big-cur-deep-drive`) and a knockout of the three channels in the exam. Its founders
are the control's, draw for draw, and its generation 0 matched the control's in every field but the clock
(`identity-v8-drive.verdict`, 19:58). The sense enters by mutation only: 3 of the 13 channels a mutation
can add.

What the read asks, per body, on the exam's 32 held-out draws:
- **Read.** At least one of the six bodies' best brains reads Stroke, Hold or Ramp.
- **Used.** On a body whose brain reads them, the knockout of the three costs at least two standard errors
  of the paired difference.
- **Worth it.** The arm's best brain beats the control's on the same body by two standard errors. That
  holds on at least two of the six bodies, and the reverse on none.
- **Against a steady swim.** The arm's best brain is read against its body's best constant power, at two
  standard errors, D145's bar for an inoculant, beside the control's reading.

If the three are read and used but not worth it, the sense is noise to a brain this size. If none is
read, the arm says nothing about the sense, only that mutation did not reach it in 100 generations. The
control reached a plateau near generation 50. Generations 70 to 79 against 80 to 89 held 468 and 423 J
(se 27 and 20), with work at 0.098 W in both.
## The control's exam (2026-10-02, 00:25)

`pilot-deep-own16` ended its 100 generations at 00:02 and its exam at about 00:25. The exam swam each body's
best brain on 32 held-out draws, against the same draws under every other brain. Pooled over the six bodies, its
own brains scored 447 J, and every shared constant scored 81 to 159 J less (constant 0.5, -85 J, se 25).
**That pooled win is not the bar.** Each body has its own best constant, and per body the brain matches it
and beats none:

| Body | Own | Its best constant | Own minus it | Own minus brainless |
|---|---|---|---|---|
| 0 | 437 J | 0.6, 416 J | +21 (se 18) | -6 (se 2) |
| 1 | 462 J | 0.7, 487 J | -25 (se 18) | -2 (se 1) |
| 3 | 495 J | 0.5, 502 J | -7 (se 40) | +236 (se 46) |
| 4 | 361 J | 0.7, 357 J | +4 (se 33) | -4 (se 2) |
| 8 | 317 J | 0.7, 327 J | -9 (se 41) | -7 (se 5) |
| 10 | 607 J | 0.3, 622 J | -16 (se 19) | -4 (se 3) |

The knockouts of the chemical sense, depth, the reserve and the flow changed nothing to the joule on any
body, so no best brain read any of them. Five of the six are blind: taking every sense away costs at most
7 J. Body 3's brain loses 236 J without its senses. So it reads a sense the four knockouts did not name: the
joint's angle, its speed or which way is up. Which one is not yet known. D145's bar for an
inoculant, a body's best constant beaten by two standard errors, is met by none. The agent's reading: after
100 generations the old brain found, per body, a stroke as good as the best fixed one. On one body it
found a reflex that matters. D146 expected no more of it.

Its successor is `pilot-deep-brain2` (D146, D147): the same six bodies, draws and water, the one brain wired
to all 13 senses, and the two-parent breeding. It started at 00:26 on nursery-v9 (`74f2cc8`). Its read is
the proposal's "nursery test": read, worth it, grown.

## Brain v2's exam (2026-10-02, 14:17)

`pilot-deep-brain2` ran 100 generations on nursery-v10 and was ahead of the control's mean in every one. Its
search is read in `HANDOFF.md` item 10a. The exam swam each body's best brain on the control's 32 held-out
draws. Every constant scored the same on both runs to the joule, so the draws match and the two runs' brains
can be paired body by body. `python scripts/reads/nursery-exam-per-body.py scratch/nursery/pilot-deep-brain2
scratch/nursery/pilot-deep-own16` makes both tables below. Each cell is the own brain's net less the other
brain's, paired over the 32 draws, with its standard error. A positive knockout cell is what losing that
sense costs.

| Body | Own | Best constant | Own minus it | SEs | Frozen | Chemical | Depth | Energy | Flow | Scent | Drive | Joint | Own minus control's own |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 499 J | 0.6, 416 J | +83 ± 34 | 2.4 | +356 ± 60 | +42 ± 20 | +44 ± 28 | +72 ± 30 | +175 ± 48 | -1 ± 1 | +140 ± 34 | +3 ± 11 | +62 ± 34 |
| 1 | 517 J | 0.7, 487 J | +30 ± 32 | 0.9 | +311 ± 51 | -12 ± 16 | +13 ± 35 | -2 ± 17 | +19 ± 28 | -1 ± 2 | +53 ± 32 | -12 ± 14 | +54 ± 36 |
| 3 | 494 J | 0.5, 502 J | -9 ± 35 | -0.2 | +258 ± 37 | -11 ± 11 | +11 ± 16 | -2 ± 13 | -31 ± 27 | -2 ± 1 | -66 ± 24 | -6 ± 7 | -2 ± 41 |
| 4 | 379 J | 0.7, 357 J | +22 ± 45 | 0.5 | +230 ± 49 | +32 ± 17 | +10 ± 13 | -40 ± 36 | -23 ± 18 | -1 ± 4 | +51 ± 27 | -22 ± 23 | +18 ± 37 |
| 8 | 152 J | 0.7, 327 J | -175 ± 52 | -3.4 | +37 ± 10 | -1 ± 2 | +2 ± 2 | +1 ± 3 | +0 ± 2 | +3 ± 2 | +8 ± 4 | -8 ± 5 | -166 ± 53 |
| 10 | 704 J | 0.3, 622 J | +81 ± 38 | 2.2 | +369 ± 46 | -24 ± 30 | +92 ± 19 | +34 ± 40 | +28 ± 48 | -1 ± 1 | -88 ± 34 | -79 ± 29 | +97 ± 37 |

Drive is Stroke, Hold and Ramp together, and Joint is the joint's angle and speed. Brainless is left out of
the table. Every unit is a tanh sum of senses, so a brain with no senses holds its outputs fixed, another
steady stroke. It costs about what Frozen costs on five bodies; on body 4 it costs 121 J against 230.

Against the bars set before the run:

- **Against a steady swim, D145's bar.** Two of six bodies clear it: body 0 by 2.4 standard errors and body
  10 by 2.2. The control cleared it on none. With six bodies tested at two standard errors, one clearing by
  luck is not rare (about one chance in eight). Two is less likely, and body 0's knockouts say its brain
  reads its senses.
- **Read and used.** Body 0's brain loses 175 J without the flow (3.6 standard errors). It loses 140 J
  without its own drive (4.1), and 72 J without the reserve (2.4). Body 10's loses 92 J without depth (4.8), and it gains 88
  and 79 J without the drive and joint senses, which it reads to its cost. The control's best brains read
  none of the four senses its exam knocked out.
- **Worth it against the control.** Only body 10 beats the control's brain on the same body by two standard
  errors (+97 ± 37). Body 8 loses to it by 3.2 (-166 ± 53). Its best brain is worse than any constant and
  barely moves (Frozen costs it only 37 J). So the bar of two bodies better and none worse is not met.
- **The scent is unused.** Its knockout cost between -2 and +3 J on every body, never above one standard
  error. The log's pooled food density at the body is 1.30 J/m³ under the own brains and 1.30 under the
  scent knockout. Nursery-v10 writes it only pooled over the six bodies.

The agent's reading, as inference:
- Brain v2's richer water is not steering by scent. Nor is it a slow body staying put. Frozen and brainless
  bodies sit in the poorest water (0.88 and 0.91 J/m³ pooled), and the constants in 1.11 to 1.26. The own
  brains reach 1.30, on a shorter path than the faster constants. On body 0 the flow is the likeliest
  guide, since it is the sense whose loss costs most. That is a guess about how the flow and the snow
  relate in this water, and it has not been checked.
- The one brain does what the old one could not. It reads its senses, and on two bodies it beats the best
  steady stroke. It does not do so on every body, and on body 8 the search ended on a brain worse than
  standing still at a fixed stroke.
- The run's lead over the control is three changes in one, as item 10a says. The exam does not separate
  them.

## The replicate's exam (2026-10-03, 05:34)

`brain2-rep2` is the pilot's setup on a new search seed, with the same six bodies (recipe
`rounds/nursery/brain2-rep2.json`). It launched on 2026-10-02 at 15:57 from the `nursery-general` worktree at
`56c0f6f`, on 14 threads pinned to the efficiency cores. Its search ended at 05:11 on 2026-10-03, its exam at
05:34, and it is the registry's entry 3. Its exam salt makes its held-out draws the pilot's. Every constant and
kinesis scored the same in both exams to the hundredth of a joule, so the two runs' best brains pair body by
body.

The search swam other water than the pilot's in every generation, so its rows do not pair with the pilot's.
Over all 100 generations its mean net was 429 J against the pilot's 439, and its decade means ran from 335 to
490 J against 364 to 503. Its path stayed near 70 m and its work near 0.05 W, as the pilot's did. At generation
100 its brains held 5.9 hidden units on average (13 at most) and 96 links.

`python scripts/reads/nursery-exam-per-body.py scratch/wt-nursery-general/runs/nursery/brain2-rep2
scratch/nursery/pilot-deep-brain2` makes both tables. The cells read as in the pilot's table above.

| Body | Own | Best constant | Own minus it | SEs | Frozen | Brainless | Chemical | Depth | Energy | Flow | Scent | Drive | Joint | Own minus the pilot's own |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 164 J | 0.6, 416 J | -252 ± 57 | -4.4 | +21 ± 12 | +17 ± 12 | -89 ± 21 | +0 ± 2 | +5 ± 9 | -4 ± 6 | -1 ± 1 | +7 ± 3 | +7 ± 10 | -335 ± 60 |
| 1 | 546 J | 0.7, 487 J | +59 ± 36 | 1.6 | +341 ± 56 | +338 ± 55 | +16 ± 23 | +59 ± 45 | +15 ± 37 | +8 ± 21 | +0 ± 1 | +7 ± 14 | -9 ± 9 | +29 ± 35 |
| 3 | 481 J | 0.5, 502 J | -22 ± 43 | -0.5 | +245 ± 40 | +242 ± 39 | +33 ± 18 | -19 ± 10 | -3 ± 10 | -4 ± 18 | +1 ± 2 | +12 ± 13 | -3 ± 7 | -13 ± 20 |
| 4 | 327 J | 0.7, 357 J | -30 ± 58 | -0.5 | +177 ± 44 | +173 ± 44 | +24 ± 29 | +38 ± 22 | +34 ± 20 | -36 ± 17 | +1 ± 1 | -8 ± 10 | +17 ± 15 | -53 ± 39 |
| 8 | 369 J | 0.7, 327 J | +42 ± 49 | 0.9 | +254 ± 39 | +244 ± 38 | -48 ± 49 | -20 ± 25 | -25 ± 44 | -54 ± 39 | -6 ± 6 | -8 ± 18 | +14 ± 21 | +217 ± 37 |
| 10 | 713 J | 0.3, 622 J | +90 ± 24 | 3.7 | +378 ± 43 | +144 ± 33 | -69 ± 35 | -31 ± 39 | -23 ± 13 | -13 ± 14 | -0 ± 0 | +27 ± 10 | -17 ± 15 | +9 ± 33 |

Against the pilot:

- **Against a steady swim.** Body 10 clears the bar again, by 3.7 standard errors (2.2 in the pilot). Body 0
  cleared it in the pilot by 2.4, and here it is the worst of the six. Body 1 reaches 1.6. So one body clears
  it this time against two in the pilot, and body 10 is the only one to clear it in both.
- **One body stuck in each run.** In the pilot it was body 8: 175 J below its best constant, with Frozen
  costing it only 37 J. Here it is body 0: 252 J below, with Frozen costing 21 J. Each of the two bodies did
  well in the other run. On body 0 the two runs' own brains differ by -335 ± 60 J, and on body 8 by
  +217 ± 37. On the other four bodies they differ by at most 1.3 standard errors.
- **The senses read are not the same.** Body 10's brain lost 92 J without depth in the pilot. Here depth's
  knockout costs nothing measurable (-31 ± 39). This brain loses 27 ± 10 J without its drive (2.7 standard
  errors), and gains 69 ± 35 J without the chemical sense (2.0). Body 4's gains 36 ± 17 J without the flow
  (2.1). The stuck body 0 gains 89 ± 21 J without the chemical sense (4.2).
- **The scent is unused again.** Its knockout moved every body by between -6 and +1 J, never above one standard
  error.
- **Brainless.** On bodies 1, 3, 4 and 8 it costs what Frozen costs, within 10 J. On body 10 it costs 144 J
  against Frozen's 378, as it did on the pilot's body 4.

Pooled over the six bodies, the replicate's own brains scored 24 J less than the pilot's. Its standard error
is about 16 J, which I took from the six per-body errors as if they were independent.

The agent's reading, as inference:
- What repeats is the one brain beating Frozen on every body but the stuck one, body 10 beating its best
  steady stroke, and the scent going unread.
- What does not repeat is which other body clears the bar and which senses a brain reads. Which sense a
  brain uses depends on the path its search took as well as on the body and the water. With one seed per
  arm, a per-body knockout describes one brain. The setup needs several seeds.
- Each run ended one body in six on a brain that barely moves and scores below every constant. I do not
  know why the search kept it. A check would compare that brain's running mean in the search with its exam.
- For the two separation arms, the seed alone moves a body's own brain by up to 5.8 standard errors. So an
  arm's per-body cells cannot be read against the pilot. What can be read is the pooled own brain, against
  this 24 J spread between the two runs, and the count of bodies that clear the bar.

## The stuck body: a first look (2026-10-03, afternoon)

The exam swims each body's best member of the last generation, ranked by its score in the search
(`tank-scores.jsonl`, which the replicate writes and the pilot did not). In the replicate, five of the six
bodies' best were children born in generation 100 and scored on that generation's draws alone, 16 episodes.
Body 0's best scored 585 J there. Its next three scored 544, 541 and 512 J. In the exam it scored 164 J and
barely moved. The noise bench above puts a brain's score at two draws near 105 J of standard error, so the
top of 16 members that close together is mostly the luckiest.

The agent's reading, as inference: the stuck body is a winner's curse at the last step. The search ranks
on one generation's water and the exam takes the top. Most of the time the top is a fair brain. Now and then
it is a dud that drew well. Two checks would test it, both after the arms, because changing how the exam
picks its brain now would part the arms from the two runs they are read against:
- Swim body 0's top four members on the exam's draws. If the second to fourth score near the other bodies'
  brains, the pick was luck.
- Pick the exam's brain on more than one generation's water, for example the elite with the most episodes,
  or the top few re-scored on fresh search draws before the exam. That is a change to the nursery's protocol,
  so it goes to the owner first.

## The separation arms, stated before they start (2026-10-03)

The owner chose both arms in turn, `brain2-nodrive` first, after the check of the farm's P1 to P3, by telling the agent to proceed
with its recommendation (2026-10-03, about 13:15). Each arm is brain2 with one thing removed, the drive
sense or the crossover. Both swim the pilot's water with search seed 0x1, and their exams use the pilot's
32 draws. They run on the column kernel (`d58d7af`), which changes no number.

Each arm is read on three things, fixed here before it runs.

- **The pooled own brain**: the mean net of the six bodies' own brains over the exam. The pilot scored 457.3 J
  and the replicate 433.2. An arm differs from brain2 only if it lands more than 50 J outside that range,
  below 383 J or above 507. The 50 J is twice the spread between the two runs. With two runs the spread is
  poorly known, so the line is a rule of thumb, not a test.
- **The bodies over the bar**: how many bodies' own brains beat their best constant by two standard errors.
  The pilot had 2 and the replicate 1. Zero or one says nothing on its own. Three or more lies outside both
  runs.
- **The stuck bodies**: how many bodies' own brains score below every constant. Each run had one.

What each outcome would mean:
- `brain2-nodrive` below 383 J says the drive sense pays. Inside the margin, it has no effect the nursery
  can see at one seed. Then whether to keep it is a matter of its cost, since its knockouts in the two
  exams cost tens of joules on one body each.
- `brain2-nocross` below 383 J says the crossover pays. Inside the margin, it has no effect the nursery can
  see, and a round could breed by mutation alone without a loss shown here.
- Above 507 J would say the thing removed was costing the brain, and would want a second seed before it is
  believed.

Body 10 cleared the bar in both runs. Whether it clears it in each arm is reported and not scored. The
per-body knockout cells are reported too, and are not read against the pilot.

## brain2-nodrive: the read (2026-10-03, night)

`brain2-nodrive` ran from 13:27 to 22:16 on 2026-10-03, from `scratch/wt-nursery-general` at `a8204f0`, on 14
threads pinned to the efficiency cores. It is the registry's entry 4. Its constants and kineses scored as in
the pilot's exam to the hundredth of a joule, so its brains pair with the pilot's body by body. The drive
sense's knockout reads +0 ± 0 on every body, as it must with the sense removed.

| Body | Own | Best constant | Own minus it | SEs | Frozen | Brainless | Chemical | Depth | Energy | Flow | Scent | Joint | Own minus the pilot's own |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 466 J | 0.6, 416 J | +50 ± 45 | 1.1 | +322 ± 63 | +329 ± 63 | +100 ± 47 | +107 ± 38 | +29 ± 23 | +48 ± 28 | +0 ± 0 | +30 ± 14 | -33 ± 34 |
| 1 | 500 J | 0.7, 487 J | +13 ± 37 | 0.4 | +295 ± 47 | +292 ± 47 | -5 ± 15 | +1 ± 26 | +9 ± 12 | +1 ± 13 | -0 ± 0 | -3 ± 10 | -17 ± 36 |
| 3 | 518 J | 0.5, 502 J | +16 ± 41 | 0.4 | +282 ± 42 | +283 ± 42 | -13 ± 29 | -21 ± 8 | -9 ± 9 | +50 ± 49 | +0 ± 1 | -2 ± 24 | +24 ± 18 |
| 4 | 442 J | 0.7, 357 J | +85 ± 50 | 1.7 | +292 ± 76 | +293 ± 77 | +59 ± 35 | +75 ± 33 | +3 ± 13 | -13 ± 16 | +0 ± 1 | +68 ± 29 | +62 ± 39 |
| 8 | 406 J | 0.7, 327 J | +80 ± 54 | 1.5 | +292 ± 46 | +295 ± 46 | +70 ± 38 | -24 ± 14 | -6 ± 11 | -3 ± 20 | +0 ± 1 | +36 ± 34 | +255 ± 44 |
| 10 | 702 J | 0.3, 622 J | +80 ± 37 | 2.2 | +367 ± 50 | +369 ± 50 | -16 ± 28 | +53 ± 17 | -2 ± 23 | +21 ± 19 | +7 ± 4 | -13 ± 31 | -2 ± 30 |

On the three things fixed before it ran:
- **The pooled own brain is 505.7 J**, 48 ± 14 J above the pilot's 457.3 and 1.3 J under the upper line of
  507. By the rule, the drive sense has no effect the nursery can see at one seed.
- **One body over the bar**: body 10, by 2.2 standard errors, as in both runs.
- **No body stuck.** Every own brain beats its best constant, and Frozen costs every one between 282 and
  367 J. Each of the two runs had one stuck body.

The agent's reading, as inference:
- The pooled gain is the absent stuck body. Body 8 was the pilot's stuck body, and here it scores 255 J above
  it. On the other five bodies the arm's own brains differ from the pilot's by +7 ± 14 J on average. If the
  stuck body is the winner's curse at the exam's pick, as read above, this arm drew no dud, and that is luck
  rather than the drive sense.
- The search said the same thing more sharply. With the pilot's water in every generation, the arm's mean
  net ran above the pilot's in all 100 generations, by about 42 J with a scatter of about 12 J. The gap was
  there at generation 0. The pilot's founders carry about 15 drive-sense links each (82 links against 67).
  With them they did 0.083 W of work against 0.007 W, for the same path. By generation 1 the pilot's
  search had cut its work to 0.041 W. The gap outlasted the difference in work, and its cause is not known.
- So removing the drive sense cost the brains nothing on any body, and may have helped a little. The sense's
  knockouts in the two runs cost tens of joules on one body each. Whether to keep it is a matter of its cost.
  A second seed of this arm, read in pairs against the replicate, would show whether the search's 42 J is real.

## brain2-nocross: the read (2026-10-04, morning)

The arm ran from 22:55 on 2026-10-03 to 07:43 on 2026-10-04, from `scratch/wt-nursery-general` at
`a8204f0`. It had 14 threads pinned to the efficiency cores and the heat watchdog beside it. It is the
registry's entry 5. The CPU stayed between 52 and 86 °C, and the watchdog never warned. Its constants and kineses scored
as in the pilot's exam to the hundredth of a joule, so its brains pair with the pilot's body by body. "Drive"
below is the knockout of the drive sense, which this arm keeps.

| Body | Own | Best constant | Own minus it | SEs | Frozen | Brainless | Chemical | Depth | Energy | Flow | Scent | Drive | Joint | Own minus the pilot's own |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 445 J | 0.6, 416 J | +29 ± 44 | 0.7 | +302 ± 62 | +85 ± 43 | +34 ± 18 | +300 ± 50 | +80 ± 27 | +42 ± 28 | +2 ± 2 | +160 ± 41 | +114 ± 35 | -54 ± 22 |
| 1 | 503 J | 0.7, 487 J | +16 ± 38 | 0.4 | +298 ± 62 | +295 ± 62 | +66 ± 42 | +3 ± 21 | +17 ± 8 | +71 ± 37 | +3 ± 3 | +25 ± 43 | +4 ± 18 | -13 ± 37 |
| 3 | 458 J | 0.5, 502 J | -44 ± 54 | -0.8 | +222 ± 63 | +225 ± 65 | +39 ± 46 | -42 ± 23 | +13 ± 43 | +364 ± 58 | -4 ± 9 | -53 ± 29 | +18 ± 30 | -36 ± 43 |
| 4 | 287 J | 0.7, 357 J | -70 ± 51 | -1.4 | +137 ± 43 | +144 ± 43 | -9 ± 10 | -7 ± 7 | -23 ± 16 | -12 ± 21 | -1 ± 1 | -87 ± 44 | -15 ± 26 | -92 ± 19 |
| 8 | 371 J | 0.7, 327 J | +44 ± 51 | 0.9 | +256 ± 56 | +250 ± 57 | +94 ± 48 | -56 ± 36 | +28 ± 40 | +86 ± 46 | -11 ± 6 | -19 ± 26 | -49 ± 40 | +219 ± 52 |
| 10 | 718 J | 0.3, 622 J | +96 ± 35 | 2.7 | +383 ± 46 | +379 ± 47 | -40 ± 26 | +5 ± 6 | +97 ± 57 | -30 ± 12 | -2 ± 1 | +41 ± 31 | -28 ± 15 | +14 ± 43 |

On the three things fixed before it ran:
- **The pooled own brain is 463.6 J**, 6 ± 15 J above the pilot's 457.3 and well inside the margin of 383 to
  507. By the rule, the crossover has no effect the nursery can see at one seed.
- **One body over the bar**: body 10, by 2.7 standard errors, as in both runs and in `brain2-nodrive`.
- **No body stuck.** Body 4 is the weakest. Its own brain scores 287 J, below the steady strokes at 0.6 and
  0.7 (322 and 357 J) and above the three slower ones (192 to 266 J). Frozen costs it 137 J, so it moves.

The agent's reading, as inference:
- As in `brain2-nodrive`, the pilot's stuck body 8 is the largest cell, 219 J above the pilot. This arm too
  drew no dud at the exam's pick.
- Leave body 8 out and the other five bodies' own brains score 36 ± 15 J below the pilot's. In
  `brain2-nodrive` the same five came out at +7 ± 14. Body 4 alone is 92 ± 19 J below, 4.9 standard errors.
  That would say breeding by mutation alone cost something on bodies that were not stuck. Yet the seed alone
  moved one body by 5.8 standard errors between the pilot and the replicate, and moved the pooled brain by
  24 J. A 36 J deficit on five bodies is inside what a second seed did on its own, and one arm cannot
  separate the crossover from the seed.
- The search agrees that the effect is small. On the pilot's water in every generation, the arm's mean net
  ran 11 J above the pilot's, with a scatter of 13 J, and above it in 80 of 101 generations. Over the first
  ten generations it ran 6 J below. The later decades ran 0 to 13 J above through generation 69, and 19 to
  24 J above from generation 70 on. Without the drive sense the gap was 42 J from generation 0.
- Body 4's own brain scores 87 ± 44 J below its drive-sense knockout, and body 3's 53 ± 29 J below. On those
  two bodies reading the drive sense cost the brain, which fits `brain2-nodrive`'s finding that removing it
  cost nothing. It is a cell of one brain, and cells are not read against the pilot.
- The rule above says that without a visible effect "a round could breed by mutation alone". That line
  is loose. The world has no crossover and breeds by mutation alone in any case, and crossover exists only
  in the nursery's search. What this arm measures is whether crossover in the nursery finds better
  founders for a round, and at one seed it did not.
- Neither arm moves the pooled brain past the line. Whether to keep either part turns on what it costs and
  what it is for, which this exam does not measure.
