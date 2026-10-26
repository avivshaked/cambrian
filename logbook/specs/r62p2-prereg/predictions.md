# The pace screen's second group: what carried the thrust arm

*Pre-registration, 2026-10-26, the speed agent. Committed on `main` before the group launches, merged into branch
`pace`, and launched from `scratch/wt-pace` on the coordinator's card go. The rulings are D239 and D240 in
`DECISIONS.md`. D240 adds that the stroke's cost is a dial the screens may tune, within the owner's delegation of
2026-10-24 to overshoot and roll back. The world, the tests, the deciding numbers, the overshoot's marks and the
stopping rule are group 1's (`logbook/specs/r62p-prereg/predictions.md`). This file names only what differs. Every
number below that is not a measurement is a guess or derived, and says so.*

## What it asks

Group 1 ended on 2026-10-26 (`logbook/specs/r62p-read/`). At 24,000 s the thrust arm `r62p-t3` passed D222's three
tests, the first world in the record to do so, with an intake over upkeep of 1.54. The pace factor at 3 failed
test 1: `r62p-k3`, with the newborn reserve at 0.5, reached a born share of 0.50, a best cohort of 0.83 and a mouthed
chain of 4, at an intake over upkeep of 1.11. `r62p-k3n`, at 0.2, did worse (all measured).

The motion read (`logbook/specs/r62p-read/motion/`) found that the two arms' grazers cover the same ground: 0.12 to
0.15 m a world second in k3 and 0.09 to 0.15 in t3, against 0.07 in the k = 1 base (measured). Both arms bill about
three times today's stroke work a world second (derived, `logbook/specs/pace-build.md`). So group 1 could not tell
cost from speed. The thrust arm changes two things at once: a stroke pushes nine times harder, and it costs a ninth as
much. The group asks:

1. Does a cheap stroke alone, at today's thrust and clock, do as well as the thrust arm? If so, cost and not speed
   carried `r62p-t3`.
2. Does the pace factor at 3 do as well once its strokes are cheap too?
3. Does a harder push still, past `r62p-t3`, carry the grazers further, or toward the overshoot?

## The worlds

Three worlds, founded on seed 6201, 24,000 world seconds each, in one card group, on group 1's world
(`rounds/env-r62kg.ps1`) with the newborn reserve at 0.5 in all three:

| World | Pace factor | Limb push | Work price | Settings |
|---|---|---|---|---|
| `r62p-w9-6201` | 1 | 0.3 | 0.0556 | `rounds/env-r62p-w9.ps1` |
| `r62p-k3w9-6201` | 3 | 0.3 | 0.0556 | `rounds/env-r62p-k3w9.ps1` |
| `r62p-t16-6201` | 1 | 4.8 | 0.03125 | `rounds/env-r62p-t16.ps1` |

**The third slot went by the draft's rule, fixed before group 1 ended.** `r62p-k3` failed D222's tests at 24,000 s where
`r62p-t3` passed them, so the slot goes to `r62p-t16`. That world takes the thrust arm further, with the limb push at
16 times its 0.3 and the work price at a sixteenth of its 0.5, at the pace factor 1. It is the push past `r62p-t3`
toward the overshoot, which the owner's delegation of 2026-10-24 allows.

**The pace factor at 4 moves to a later group.** By the draft's second rule, it runs at the ninth price, 0.0556,
because `r62p-t3` ended group 1 with intake over upkeep above `r62p-k3`'s by more than a third: 1.54 against 1.11
(measured).

**What each world bills for its strokes** (derived from the build). The work a stroke does is physics, the same at any
clock. At the pace factor k a body strokes k times as often a world second, so its work a world second is k times
today's, and the price multiplies it:

| World | Work a world second, against today | Price, against today | Billed a world second, against today |
|---|---|---|---|
| `r62p-t3` (group 1) | 27, thrust 9 times at speed 3 times | 1/9 | 3 |
| `r62p-k3` (group 1) | 3 | 1 | 3 |
| `r62p-w9` | 1 | 1/9 | 1/9 |
| `r62p-k3w9` | 3 | 1/9 | 1/3 |
| `r62p-t16` | 64, thrust 16 times at speed 4 times | 1/16 | 4 |

The work column assumes speed rises as the square root of thrust against quadratic drag. The motion read found
`r62p-t3`'s grazers about 1.8 times the base, not 3 (measured), so the work in the thrust arms is likely lower than
the table says (inference).

The controls are group 1's worlds, `r62cs-w500-6201`, and `r62kg-6201`. `r62kg-6201` runs after this group and
before either group's write-up.

## The identity sitting

None is needed. `r62p-w9` and `r62p-t16` run at the pace factor 1. `r62p-k3w9` runs at 3, which the pace build's
sitting proved identical at groups 32 and 64 (`logbook/specs/r62p-prereg/identity.md`). The work price and the limb
push are economy and drive dials. Group 1 changed both without a new sitting, and they move no solver code. The
launch carries the same build as group 1: core `8de5866c`, dynamics `5def5ed2` and farm `de92616d`, from
`scratch/wt-pace` with this file's commit merged in.

## Predictions

These are guesses unless marked derived. The draft's guesses were written before group 1 ended. Group 1's motion
read moved two of them, and each one says so.

- **`r62p-w9`.** Its intake over upkeep is above `r62p-k3`'s 1.11 at 24,000 s, because it bills a ninth of today's
  stroke. It fails test 1. Its grazers swim at the base's 0.07 m a world second, so a newborn needs about 20 s to
  reach a leaf 1.4 m off, against a runway of about 18 s (derived, the newborn ledger). Moved: the draft predicted it
  within a fifth of `r62p-t3`'s intake over upkeep.
- **`r62p-k3w9`.** It does better than `r62p-k3` on intake over upkeep and on bred grazers that keep a meal, and
  sits between `r62p-k3` and `r62p-t3` on test 1. Moved: the draft predicted no better than `r62p-w9`.
- **`r62p-t16`.** Its grazers swim no more than 1.3 times `r62p-t3`'s, because the limiters already bind at a push of 9
  (inference from the motion read). It passes D222 as `r62p-t3` did, at an intake over upkeep within a fifth of its.
- **The overshoot.** By group 1's three marks, it comes first in `r62p-t16`, if it comes at all.

## The machine

Group 1's measured pace: `r62p-t3`, at the pace factor 1, reached 24,000 s in 3.3 hours. `r62p-k3` took 7.3 hours,
at 0.58 to 0.65 times real time over its last hours with 9,000 to 13,000 bodies. This group's slowest world is
`r62p-k3w9`, so the group takes about 7 to 8 hours (guess).

One more exit, the coordinator's of 2026-10-26: after 12,000 s, if the slowest world runs under 0.6 times real time
for an hour, it is stopped with `scripts/stop-arm.ps1` and read at its stop second, and the read says so. `r62p-k3`
ran close to that line over its last hours, so the exit may well fire on `r62p-k3w9` (inference).

## What would change the plan

If a world overshoots, the next screen rolls back from its dials, as group 1's file says. If `r62p-w9` matches
`r62p-t3`, cost carried the thrust arm, and the pace factor at 4 is not run. If neither `r62p-w9` nor `r62p-k3w9`
passes test 1 and `r62p-t16` does, the speed the thrust buys is what feeds the grazers. The growth screens of
`logbook/specs/growth-runway-design.md` then start from `r62p-t3`'s world, as that file plans.
