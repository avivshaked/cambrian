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

## What comes next

The next read swims the same children and parents alone in still water under their own brains, with physics on. A slow
child that also swims slowly alone carries its slowness in its genome and body. One that swims well alone was slowed by
the world it was born into (inference). The coordinator approved the variant on 2026-10-27.

## Sources

| Claim | Source |
|---|---|
| every channel flat, 0.55 to 0.97 | `brain-birth-channels.tsv` |
| silent shares by group and variant | `bench.txt`, `brain-birth.tsv` |
| the 393 ids and their groups | `ids.tsv`, `ids.txt` |
| the forward config's 8 fields | `scripts/reads/config-forward.py`'s output |
| the senses a held body reads | `src/Evosim.SwimProbe/BrainBirth.cs`, its class comment |
| the stroke is a loop through the body | inference, not measured |
