# The brain at birth, with the body held: one fixed point, and a bench that cuts the loop

*2026-10-27, the speed agent. The brain-at-birth bench (`src/Evosim.SwimProbe/BrainBirth.cs`, `--brain-birth`, written
by a Sonnet agent and reviewed) on `r62p-t3-6201`'s same-plan bred grazers. It ran on one core at one thread in 9 s.
The ids are `scripts/reads/brain-birth-ids.py`'s. The outputs sit beside this file: `bench.txt`, `brain-birth.tsv`
(one row per child and variant) and `brain-birth-channels.tsv` (one row per drive channel).*

## What it asked

The follow-up read (`logbook/specs/r62p-read/newborn-followup/`) found that slow newborns with their parent's body plan
hang still in open water with quiet joints, and that every one has a central brain. The bench takes 393 of those
children (all 117 at a standstill, all 76 slow, and 200 movers drawn at random) and their parents. It builds each body
from its genome alone and holds it still: the physics step is never called. It then steps only the brain, at 0.01 s
for 60 s, and measures each drive channel over the last 30 s. There are four variants:
- the parent's brain, started fresh;
- the child's brain, started fresh, as birth starts it;
- the child's brain started from its parent's state after the parent's held brain has run 60 s;
- the child's brain started fresh with every neuron output kicked to a draw in [-0.05, 0.05].

t3's config predates the growth build, which refuses it, so the bench read `scratch/brain-birth/t3-config-forward.json`.
That file is `scripts/reads/config-forward.py`'s, laid over the r42 fixture. The 8 fields it took from the fixture are
all off-defaults: growth off, the three named shapes, and a form chance of 0 (listed in the script's output).

## What it found

**Every brain settles to a constant drive.** With the body held, every channel of every brain ends the run flat: zero
peak-to-peak over the last 30 s, at a mean between 0.55 and 0.97. That holds in all 393 children and all their parents,
in every variant and in every group.

| Group | Children | Silent: parent | fresh | from the parent | kicked |
|---|---|---|---|---|---|
| standstill | 117 | 100% | 100% | 100% | 100% |
| slow | 76 | 100% | 100% | 100% | 100% |
| moving | 200 | 100% | 100% | 100% | 100% |

**The start does not matter.** A child started from its parent's warmed state, or with a kick, settles to exactly the
value the fresh child settles to. Child 6386's first channel reads 0.548428 in all three, for example. With its inputs
held, each brain has one fixed point, and both cheap candidates, the parent's state and the kick, change nothing.

## The bench's limit, which bounds the finding

**The held body cuts the loop the stroke runs through.** A held body's joint angles and rates, its flow and its contacts
all read 0, so a brain whose stroke is driven by those senses never sees them change. The fixed point is the brain's
answer to a body that does not move. It is not evidence that a swimming body's brain is silent: the movers' brains,
which do swim, settle the same way here. So this bench cannot separate a slow child from a mover. My reading is that
the stroke is a loop closed through the body rather than a rhythm the brain makes on its own (inference). On that
reading, a newborn's slowness lies in how its brain and its body meet, not in the state its brain starts from.

## Swimming alone: the slow children move like the movers

The bench's swim variant (`--swim`, the same day, approved by the coordinator) puts each child, fresh at its birth size,
and its parent alone in still water under their own brains, with physics on, through the probe's own `Swim`. Each is
placed at a root depth of 20 m with no glass and no contact, for 60 s. `early` is the root's net horizontal speed over
0 to 10 s, the swim's analogue of the world's first speed. The outputs are `bench-swim.txt` and `brain-birth-swim.tsv`.
The run took 34 s on one core.

| Group, in the world | Children | Median early speed alone (m/s) | Quartiles | Parents' median alone |
|---|---|---|---|---|
| standstill | 117 | 0.161 | 0.146 to 0.183 | 0.195 |
| slow | 76 | 0.157 | 0.095 to 0.174 | 0.193 |
| moving | 200 | 0.159 | 0.121 to 0.174 | 0.192 |

| Children | Slow alone (early under 0.03 m/s) | Not slow alone |
|---|---|---|
| slow in the world (standstill and slow) | 4 | 189 |
| moving in the world | 0 | 200 |

**189 of the 193 children that were slow in the world move alone as fast as the movers do.** The body and brain a slow
newborn inherits move it when it is alone. So the slowness comes from the world it is born into, not from its genome
(inference, bounded below).

**Two limits bound this.**
- **Moving is not yet stroking.** The median dominant frequency alone is 0.06 Hz in every group, a period of about 16 s,
  which hardly reads as a stroke. The probe's solver keeps the tissue's excess density, so a body sinks, and a sinking
  body can glide sideways. Some of the 0.16 m/s may be glide rather than swimming. A passive control, the same bodies
  with the brain off, separates the two. It has not been run.
- **The depth differs.** In the world the standstill children sat at a root height of about -2.5 m, and the movers at
  about -4.3 m (`logbook/specs/r62p-read/newborn-followup/`). Here every body starts at -20 m. The brain reads depth, and
  its constant drive in the held bench came from the senses it was given. Starting each child at its own recorded height
  is the second check.

## The checks: no glide, the depth matched, and no surface

The checks ran the same day (`--swim --passive --birth-height`; `bench-checks.txt`, `brain-birth-checks.tsv`,
`ids-y0.tsv`):
- a passive control, the same bodies with the brain off;
- each child and its parent started at the child's recorded first-frame root height, `y0`, with the probe's top clamp
  lifted from -2 m to -0.05 m for this run alone;
- each body's buoyancy, its vertical drift in its first 10 s, and its length.

The run took 57 s on one core. A separate read gives each newborn's height in the world over its first minute
(`scripts/reads/newborn-height-read.py`, `newborn-height.txt`).

**Glide is ruled out.** With the brain off, every child and every parent stays exactly where it was placed, with no
sideways or vertical drift. The own-brain speed alone is the brain's swimming.

**Depth is ruled out.** Started at their own recorded height, the children swim alone at the same speeds:

| Group, in the world | Children | Median early speed alone, own brain (m/s) | Same, minus passive | Slow alone |
|---|---|---|---|---|
| standstill | 117 | 0.141 | 0.141 | 6.8% under 0.03 m/s on net speed |
| slow | 76 | 0.129 | 0.129 | 17.1% |
| moving | 200 | 0.138 | 0.138 | 7.0% |

| Children, own brain over passive | Slow alone | Not slow alone |
|---|---|---|
| slow in the world | 10 | 183 |
| moving in the world | 2 | 198 |

**The surface is ruled out.** Every newborn here is neutrally buoyant: its net vertical force at rest is 0, because its
volume, a median of 0.076 m³, is under the world's neutral body volume of 0.25 m³, below which the tissue's excess
density is scaled to nothing (D064, `Fluid.cs`). Nothing pushes a newborn up against the surface. The newborns are
about 0.96 m long, and their roots sit 1.6 to 3.6 m down. Even with the whole length pointing straight up, only 8.5% of
the standstill children could reach y = 0, against 6.5% of the movers.

**In the world, a standstill newborn holds its height like the passive body.** 92% of them move under 1 cm between
their first two frames, against 37% of the movers (`newborn-height.txt`). They also sit shallower, at a median root
height of -2.5 m against -4.3 m. A neutral body with no stroke stays where it is put, which is what the passive control
does alone. My reading is that in the world the standstill newborn's drive is switched off or blocked, while alone the
same brain and body swim (inference).

One line of `bench-checks.txt`'s header is stale: it still says Swim clamps the start to [-40, -2] m. For this run the
top was -0.05 m (`Program.ConfigureForBench`, `_startTop`), and no row's note reports a clamp.

## The sense sweep: energy slows every newborn, and freezes none

What differs between the world and still water alone is what a newborn senses and touches. The bench fed every brain
the chemical and energy senses at one constant, 0.5, with no current. In the world the energy sense is
tanh(SecondsOfReserve / EnergyFullScaleSeconds), and t3's scale is 3,000 s. Newborns hold about 15 s of reserve at
their first frame, so their energy sense reads 0.00 to 0.01, in every group. There is no reserve gate on the drive:
work is billed after the fact in metabolism, and the reserve reaches the brain only through that sense. The first-frame
reserve is also the same in each group, with medians of 14.9 s standstill, 14.1 s slow and 15.0 s moving.

The sweep ran later the same day (`--sense-sweep 0,0.25,0.75,1 --current 0.02`, each child at its recorded height;
`bench-sweep.txt`, `brain-birth-sweep.tsv`). It took 2.5 minutes on one core. The build was branch `brain-birth` at
`66d40da0`; the run log names `0da3ee8d`, which differs only in one method's name. The arms are:
- `childSwim@v`: chemical and energy both at v. One constant feeds both channels, so this arm moves them together.
- `childSwimReserve`: the energy sense from the child's own first-frame reserve (`ids-y0-s0.tsv`'s S0 column, from
  `scripts/reads/brain-birth-add-s0.py`), with chemical held at 0.5. This is the world's energy sense.
- `childSwimCurrent`: a uniform current of 0.02 m/s along x. It runs on the probe's own step loop. That loop in still
  water, `childSwimLoop0`, reproduces the world step bit for bit in all 393 rows, so the comparison is clean.

| Arm | standstill (117) | slow (76) | moving (200) |
|---|---|---|---|
| childSwim, both at 0.5 | 0.141 | 0.129 | 0.138 |
| both at 0 | 0.078 | 0.073 | 0.099 |
| both at 0.25 | 0.121 | 0.109 | 0.123 |
| both at 0.75 | 0.150 | 0.138 | 0.148 |
| both at 1 | 0.157 | 0.145 | 0.152 |
| own reserve, chemical 0.5 | 0.081 | 0.079 | 0.098 |
| current 0.02 m/s, relative to the water | 0.144 | 0.132 | 0.141 |

The table gives the median early speed alone, in m/s.

| Arm | Slow in the world, slow alone | Moving in the world, slow alone |
|---|---|---|
| both at 0.5 | 10 of 193 | 1 of 200 |
| both at 0 | 26 of 193 | 6 of 200 |
| own reserve, chemical 0.5 | 31 of 193 | 10 of 200 |
| current 0.02 m/s | 8 of 193 | 0 of 200 |

**The energy sense matters, but it does not freeze anyone.** At the world's own energy sense, every group swims more
slowly alone: the standstill children by 43%, the slow ones by 39% and the movers by 29%. Even so, 162 of the 193
children that were slow in the world still swim alone at their recorded reserve. Slow-alone children are about three
times as common among the slow-in-world as among the movers, at 0.5 and at the recorded reserve alike. The energy sense
changes the speed of every newborn alike. It does not pick out the ones that stop in the world. So it does not explain
the split.

**The chemical sense adds little.** The reserve arm holds chemical at 0.5 and puts energy near 0, and it lands where
the sweep puts both at 0. So most of the sweep's effect is the energy channel's (inference: the sweep cannot vary
chemical alone).

**The current is ruled out.** In 0.02 m/s of water the children swim alone at the same speeds as in still water.

**One finding stands on its own.** With a scale of 3,000 s and a newborn reserve of about 15 s, the energy sense reads
almost 0 for every young body. On this bench that costs a newborn about a third of its swimming speed. Whether the
scale should fit a young body's reserve is a world rule, and so the owner's to rule. It is set out here as a possible
finding.

## In the world: the joints barely move, and nothing touches them

The run recorded its own pose stream every 10 s, with each body's joint coordinates. A reader takes each newborn's
first two frames from it, with its parent's in the same two frames, and measures how far apart they are and what lies
near them (`scripts/reads/newborn-joints-read.py`, `newborn-joints.txt`, `newborn-joints.tsv`). The world is a 300 m
torus, and the distances are root to root.

| Group | Children | Median largest joint change (stream units) | Joints still (under 1e-4) | Median root move (m) | Median distance to parent (m) | Any body within 1 m |
|---|---|---|---|---|---|---|
| standstill | 116 | 0.033 | 11% | 0.05 | 3.9 | 5% |
| slow | 75 | 0.057 | 7% | 0.18 | 3.9 | 7% |
| moving | 199 | 0.200 | 0% | 1.61 | 3.8 | 8% |

**Nothing holds them.** A standstill newborn lies as far from its parent as a mover does, about 3.9 m. Only 1 of 117
has its parent within 1 m, and only 5% have any living body within 1 m, against 8% of the movers. Contact with the
parent or another body cannot explain the standstill (inference: distances run root to root, and the stream does not
say whether leaves are among its bodies).

**Their joints move, but a sixth as far.** Only 18 of the 191 children that were slow in the world have joints that
hold still to 1e-4 between the two frames. The rest change by a median of 0.03 to 0.06, against 0.20 for the movers.
Every parent's joints move, so a still child sits beside a swimming parent. The joint coordinates are presumably
radians; the state stream's spec names no unit.

So the standstill newborn drives weakly rather than being held. That puts the cause on the brain's side: something it
senses in the world and not alone (inference). A joint pinned at its limit by a saturated drive would hold still, and
at most 18 children do.

## One sense at a time: the carrion scent stops the slow genomes more often

The bracket ran the same evening (`--bracket 0,0.25,0.5,0.75,1`; `bench-bracket.txt`, `brain-birth-bracket.tsv`). It
took 3.3 minutes on one core, built from `d26be972`. Every arm puts the child alone at its recorded height with its
energy sense from its recorded reserve, and moves one sense:
- `chem@v` sets the chemical sense at v;
- `scent@v` sets the carrion scent at v, through a uniform field of one cell;
- `leaf@v` would set the leaf scent.

The bench first reads which channels each child's brain is wired to (`Brain.SensorMask`). Every one of the 393 reads
the same set: joints, contact, up, damage, chemical, energy, flow, depth, scent, the drive envelope and the leaf
scent's direction. One standstill child and 12 movers also read the bed. None reads the leaf scent's strength, so the
leaf arm did not run. None reads Photo either.

| Arm | Median early speed alone, slow in the world | Movers | Slow in the world, slow alone | Movers, slow alone |
|---|---|---|---|---|
| chem@0 | 0.077 | 0.100 | 25 of 193 | 6 of 200 |
| chem@0.5, the reserve arm | 0.080 | 0.098 | 31 of 193 | 10 of 200 |
| chem@1 | 0.072 | 0.084 | 51 of 193 | 20 of 200 |
| scent@0.25 | 0.065 | 0.087 | 40 of 193 | 13 of 200 |
| scent@0.5 | 0.051 | 0.073 | 62 of 193 | 25 of 200 |
| scent@0.75 | 0.032 | 0.063 | 91 of 193 | 42 of 200 |
| scent@1 | 0.015 | 0.052 | 119 of 193 | 63 of 200 |

The first two columns are in m/s. The slow-in-the-world speeds are the standstill and slow groups' medians, which lie
within 0.006 m/s of each other in every arm.

**The chemical sense slows both groups alike.** The slow-alone count rises with the value in both groups, and their
ratio stays about 3 to 1, as it does at the bench's own settings.

**The carrion scent is the first sense that pulls the groups apart.** At full scent the children that were slow in the
world nearly stop alone, at a median of 0.013 m/s for the standstill group, while the movers keep a median of
0.052 m/s. 62% of the slow-in-the-world children stop, against 32% of the movers. That is the only arm in which a
majority of them stop. It is not a clean split: a third of the movers stop too.

**Every newborn is born among the dead.** The pose stream marks remains, and every child in every group has remains
within 10 m at its first frame, which is the carrion scent's range in t3. The median is 47 to 50 of them, and the
nearest lies about 2.1 m away. So the scent a newborn reads in the world is probably well above 0 in every group
(inference: the stream carries no corpse's joules, so the scent's value is not known). The groups are born into the
same smell. On this reading, the standstill comes from genomes whose brains answer a strong carrion scent by stopping,
and the scent is strong wherever newborns are born (inference).

**Photo is a sense ruled but not built.** D154 (2026-10-06) ruled a light sense on `SensorChannel.Photo`, put in the
pool by a `SenseLight` setting. No such setting exists in `src/`, `CreatureSenses.Read` has no case for Photo, and
t3's pool does not offer it. So no genome here reads light, and a genome that did would read 0. This is recorded, not
fixed.

## What comes next

The carrion scent reproduces part of the world's split, and the other senses the bench can set reproduce none of it. Two
things are not known: the scent each newborn actually read, and whether the leaf scent's direction, which every brain
reads and the bench cannot set, adds to it. The senses a newborn read in the world are recorded nowhere. A reader-only
option on the farm's film window could record them, with the drive each brain put out, over each newborn's first
seconds (inference: designed, not built).

## Sources

| Claim | Source |
|---|---|
| every channel flat, 0.55 to 0.97 | `brain-birth-channels.tsv` |
| silent shares by group and variant | `bench.txt`, `brain-birth.tsv` |
| the 393 ids and their groups | `ids.tsv`, `ids.txt` |
| the forward config's 8 fields | `scripts/reads/config-forward.py`'s output |
| the senses a held body reads | `src/Evosim.SwimProbe/BrainBirth.cs`, its class comment |
| the stroke is a loop through the body | inference, not measured |
| early speeds and the 2x2 alone | `bench-swim.txt`, `brain-birth-swim.tsv` |
| the world's root heights | `logbook/specs/r62p-read/newborn-followup/newborn-followup.txt` |
| glide may carry part of the speed alone | inference, from the 0.06 Hz frequency; ruled out by the passive control |
| passive bodies hold still; buoyancy 0; lengths | `bench-checks.txt`, `brain-birth-checks.tsv` |
| speeds at the recorded height; the second 2x2 | `bench-checks.txt` |
| heights in the world | `newborn-height.txt` |
| a world sense holds the drive at a fixed point | inference, not measured; the sweep rules out energy, chemical and current |
| no reserve gate on the drive | `src/Evosim.Core` (EffectorDrive, World.Observe, PendingWorkJoules in metabolism), read by hand |
| first-frame reserve by group | `scratch/newborn-ledger/full/grazer-frames.tsv`'s S, first row per child; `ids-y0-s0.tsv` |
| sweep, reserve and current speeds; the 2x2s | `bench-sweep.txt`, `brain-birth-sweep.tsv` |
| the loop control matches the world step bit for bit | `brain-birth-sweep.tsv`, childSwim against childSwimLoop0, every column |
| chemical adds little | inference, from the reserve arm against the sweep at 0 |
| joint change, root move and distances in the world | `newborn-joints.txt`, `newborn-joints.tsv` |
| the torus's size | `newborn-joints.txt`'s world line, derived from `config.json` as `World.cs` derives it |
| the drive is weak, not held; the cause is a sense | inference, from the joint change and the distances |
| channels each brain reads; the bracket's speeds and 2x2s | `bench-bracket.txt`, `brain-birth-bracket.tsv` |
| remains within 10 m at the first frame | `newborn-joints.txt`, `newborn-joints.tsv` (`nearestRemains1`, `remainsWithin10m`) |
| the scent's range and half scale in t3 | the run's `config.json`: `scentRangeMetres` 10, `scentHalfScaleJoules` 20 |
| the world's scent is high for every newborn; genomes that stop on it | inference, not measured |
| Photo ruled and not built | D154; no `SenseLight` in `src/`; `CreatureSenses.Read` |
