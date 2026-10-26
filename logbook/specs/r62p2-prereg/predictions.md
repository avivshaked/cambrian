# The pace screen's second group: what carried the thrust arm, and the pace factor at 4

*DRAFT, 2026-10-26, the speed agent. Not a pre-registration until group 1 has ended, its read is written and the
grazer-motion diagnostic has run; then it is finalized, committed on `main`, merged into branch `pace`, and launched
from `scratch/wt-pace`. The rulings are D239 and D240 in `DECISIONS.md`: D240 adds that the stroke's cost is a dial the
screens may tune, within the owner's delegation of 2026-10-24 to overshoot and roll back. The world, the tests, the
deciding numbers, the overshoot's marks and the stopping rule are group 1's (`logbook/specs/r62p-prereg/predictions.md`),
and this file names only what differs. Every number below that is not a measurement is a guess or derived, and says so.*

## What it asks

At 12,000 s of group 1 (`logbook/specs/r62p-read/12000/`), the thrust arm `r62p-t3` led on every grazer number, and its
intake over upkeep, 1.32, was the first above 1 in the record. The pace factor at 3 did not, at 0.91 with the newborn
reserve at 0.5 (`r62p-k3`) and 0.65 at 0.2 (`r62p-k3n`) (measured, at 12,000 s; the formal reads are at 24,000 s).
The thrust arm changes two things at once: a stroke pushes nine times harder, and it costs a ninth as much. The group asks:

1. Does a cheap stroke alone, at today's thrust and clock, do as well as the thrust arm? If so, cost and not speed
   carried `r62p-t3`.
2. Does the pace factor at 3 do as well once its strokes are cheap too?
3. Does the pace factor at 4, which the owner asked to see, do better than at 3?

## The worlds

Three worlds, founded on seed 6201, 24,000 world seconds each, in one card group, on group 1's world
(`rounds/env-r62kg.ps1`) with the newborn reserve at 0.5 in all three:

| World | Pace factor | Limb push | Work price | Settings |
|---|---|---|---|---|
| `r62p-w9-6201` | 1 | 0.3 | 0.0556 | `rounds/env-r62p-w9.ps1` |
| `r62p-k3w9-6201` | 3 | 0.3 | 0.0556 | `rounds/env-r62p-k3w9.ps1` |
| `r62p-k4-6201` | 4 | 0.3 | 0.5 or 0.0556, by the rule below | `rounds/env-r62p-k4.ps1` |

**Which world takes the third slot.** The rule is fixed now, before group 1 ends, and it reads group 1's final
alone (the coordinator's of 2026-10-26). If `r62p-k3` fails D222's tests at 24,000 s, where `r62p-t3` passed them,
the slot goes to `r62p-t16-6201` (`rounds/env-r62p-t16.ps1`): the thrust arm pushed further, the limb push at 16 times
its 0.3 (4.8) and the work price at a sixteenth of its 0.5 (0.03125), at the pace factor 1, so a uniform thrust 16
times today's and a speed about 4 times. It is the push past `r62p-t3` toward the overshoot, which the owner's
delegation of 2026-10-24 allows. `r62p-k4` then moves to a third group. If `r62p-k3` passes, `r62p-k4` keeps the
slot, as in the table above.

**The work price of `r62p-k4`, if it keeps the slot.** It is set when this file is finalized, from group 1 and nothing else. If `r62p-t3`
ends group 1 with intake over upkeep above `r62p-k3`'s by more than a third, cost is taken to be what binds, and
`r62p-k4` runs at 0.0556. Otherwise it runs at today's 0.5, so that it shows the pace factor alone.

**What each world bills for its strokes** (derived from the build, `logbook/specs/pace-build.md`). The work a stroke
does is physics, the same at any clock. At the pace factor k a body strokes k times as often a world second, so its
work a world second is k times today's, and the price multiplies it:

| World | Work a world second, against today | Price, against today | Billed a world second, against today |
|---|---|---|---|
| `r62p-t3` (group 1) | 27, thrust 9 times at speed 3 times | 1/9 | 3 |
| `r62p-w9` | 1 | 1/9 | 1/9 |
| `r62p-k3w9` | 3 | 1/9 | 1/3 |
| `r62p-k4` at 0.5 | 4 | 1 | 4 |
| `r62p-k4` at 0.0556 | 4 | 1/9 | 4/9 |
| `r62p-t16`, if it takes the slot | 64, thrust 16 times at speed 4 times | 1/16 | 4 |

So `r62p-w9` bills less for its strokes than any world yet run, and swims at today's speed. If it matches `r62p-t3`,
the thrust arm's speed was not what fed its grazers. With swimming's work at about 0.09 W today (guessed), the billed
work is about 0.01 W in `r62p-w9`, 0.03 W in `r62p-k3w9`, and 0.36 W or 0.04 W in `r62p-k4`.

The controls are group 1's worlds and `r62kg-6201`, which runs after group 1 and before either group's write-up.

## The identity sitting, before the launch

`r62p-k4` is the first world at the pace factor 4, and its metabolic step holds 200 physics steps at 0.01 s. The
sitting adds it to `rounds/identity-pace-on.ps1`'s pattern: the three worlds beside `kg` at 32 and then 64, each
identical with itself. Its record is `identity.md`, beside this file.

## Predictions

These are guesses unless marked derived, and they are written before group 1 ends; the finalized file may move them,
and says so if it does.

- `r62p-w9`'s intake over upkeep is within a fifth of `r62p-t3`'s at 24,000 s. My reading is that cost carried the
  thrust arm, because `r62p-t3`'s grazers swim faster but bill three times today's stroke (inference).
- `r62p-k3w9` does better than `r62p-k3` on intake over upkeep and on bred grazers that keep a meal, and no better than
  `r62p-w9`, unless the grazer-motion read finds k = 3's grazers do move three times as far a world second.
- `r62p-k4` does no better than `r62p-k3` at today's price.
- The overshoot, by group 1's three marks, comes first in `r62p-w9` if it comes at all.

## The machine

Group 1's measured pace (`logbook/specs/r62p-read/12000/`, and the looks): the worlds at the pace factor 3 ran at
about 1.0 to 1.3 times real time with 3,000 to 5,000 bodies, and the world at 1 at about 1.7 to 2.2 times with 6,000
to 8,000. At 4 the physics a world second is a third more than at 3, and physics took about 72 % of a pace-factor-3
world's wall at the founding. So `r62p-k4` runs at about 0.8 times real time (guess), and the group takes about 8
hours to 24,000 s, set by its slowest world. That is longer than the owner's aim of about 2 times real time, and the
stopping rule at 12,000 s is the place to end it early if it has answered.

One more exit, the coordinator's of 2026-10-26: after 12,000 s, if the slowest world runs under 0.6 times real time
for an hour, it is stopped with `scripts/stop-arm.ps1` and read at its stop second, and the read says so.
