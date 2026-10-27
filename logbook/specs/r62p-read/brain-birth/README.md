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

## What comes next

What differs between the world and still water alone is what a newborn senses and touches. That means the chemical and
energy senses (a constant 0.5 here), the smell of leaves, light by depth, the current of 0.02 m/s, and contact with its
parent or with leaves at birth (inference). A brain whose drive is a loop through the body can be held at a fixed point
by a sense that saturates a unit on that loop (inference). The cheapest next read sweeps those senses in the same
bench, with each child at its own height.

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
| a world sense holds the drive at a fixed point | inference, not measured |
