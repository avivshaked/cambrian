# Slow newborns with their parent's body plan: joints, brain and the early deaths

*2026-10-27, the speed agent. Three follow-up reads on `r62p-t3-6201`, after `logbook/specs/r62p-read/slow-newborn/` found
that none of the parent, the size of the brain change, size, reserve or birth place explains the slow newborns.
`scripts/reads/newborn-followup-read.py` (a Sonnet agent's, reviewed), run to 24,000 s with a 10 s window on the
ledger's frame table. The output is `newborn-followup.txt` beside this file. Everything here is association in
recorded data.*

The population is the slow-newborn read's: 997 bred grazers with a first speed and an unchanged body plan. They split
by first speed into 117 at a standstill (under 0.01 m a second), 76 slow (0.01 to 0.03) and 804 moving. The read's own
check finds every child's first speed equal to `genome-diff-read.py`'s, and it reads all 1,056 frame times it needs
from the state stream.

## What the reads find

**The standstill newborns hang still in open water.** Between a child's first two frames, 10 s apart, its root rises or
falls by a median of 0.001 m, against 0.047 m for a moving child. It sits about 22 m above the bed, as the moving ones
do, so it is not stuck on the floor, and its joints barely move. A quarter of the standstill children hold every joint
coordinate within 0.01 rad at both frames, against 3% of the moving ones. Their median largest change is 0.033 rad,
against 0.21.

| Group | n | Every joint unchanged within 0.01 rad | Median largest joint change (rad) | Median root rise or fall (m) | Median height over the bed (m) |
|---|---|---|---|---|---|
| standstill | 116 | 0.259 | 0.033 | 0.001 | 22.5 |
| slow | 75 | 0.200 | 0.057 | 0.003 | 22.3 |
| moving | 798 | 0.033 | 0.209 | 0.047 | 20.7 |

Two frames 10 s apart cannot see a stroke inside the interval, so equal angles are evidence of a silent drive, not proof
of one. A stroke whose period divides 10 s would read as unchanged. My reading is that most standstill children have
a drive that is silent or nearly so (inference).

**The kind of brain change does not separate them.** Every one of the 997 children has a central brain, so its node
neurons are unused, and every brain change sits in the central brain. All children have changed link weights, and 22
to 32% have changed unit biases. The medians match across the groups, a summed change of 2.45 at a standstill and 2.55
moving. The read's static test for a silent drive neuron applies only to node neurons, so it finds nothing to test here.
Whether a central brain is silent at birth needs the brain stepped from its starting state, which no recorded file
holds (inference).

**The newborns with no first speed starve within seconds, on a fraction of the reserve.** 554 bred grazers never get a
first speed. 507 of them, 92%, starve, at a median age of 8 s. 126 die before the first 10 s frame after their birth.
At the first frame they hold a median of 2.9 J of reserve, against 12.6 J for the moving children. They are born at
the same distance from a leaf as the moving ones: a median of 0.50 m horizontally, against 0.51 m.

| Group | n | Starved | Median age at death (s) | Ate share |
|---|---|---|---|---|
| no first speed | 554 | 0.915 | 8.5 | 0.312 |
| standstill | 117 | 0.983 | 27.0 | 0.291 |
| slow | 76 | 0.934 | 27.5 | 0.408 |
| moving | 804 | 0.704 | 40.0 | 0.629 |

## What it means

The same-plan slow newborns are a still body with a quiet drive, in open water and in reach of food. My reading is that
their central brains settle at birth into a state that does not drive the joints, and that no recorded file can show
it (inference). The test is to step a child's brain from birth with its body held and compare its drive output with
its parent's. A tool could do that from the genomes alone. The early deaths are a separate case. They hold under a quarter of a moving child's reserve at their
first frame, and most die before they can be measured (inference from the reserve and the age at death).

## Sources

| Claim | Source |
|---|---|
| groups, joints, root, brain kinds, early deaths | `newborn-followup.txt` beside this file |
| the earlier causes ruled out | `logbook/specs/r62p-read/slow-newborn/README.md` |
| the bed's indexing, as `scripts/guide.py` reads it | `scripts/reads/newborn-followup-read.py`, `scripts/guide.py` |
| the central brain's units and links | `src/Evosim.Core` `Brain.cs`, as the read cites it |
| silent central brain at birth | inference, not measured |
