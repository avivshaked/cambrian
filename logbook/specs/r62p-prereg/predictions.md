# The pace screen's first group: three ways to give a grazer time to find its meal

*2026-10-26, the speed agent. Committed on `main` after the pace build's card identity sitting passes, before the
group launches, and merged into the pace build's branch. The group launches from that branch's clean worktree
(`scratch/wt-pace`), so every manifest's `gitCommit` holds this file. The
ruling is D239 in `DECISIONS.md`, the design read is `logbook/specs/pace-of-life-read.md`, and the arithmetic is
`logbook/specs/grazer-breeding-budget.md`. The world is `r62kg`'s (`logbook/specs/r62kg-prereg/predictions.md`), and
this file names only what differs. Every number below that is not a measurement is a guess or derived, and says so.*

## What it asks

A grazer's meal takes about 100 s, nearly all of it search, against the 41 s that two to three breedings in a life
allow (the budget). A bred grazer is born with 13.5 J and starves at a median of 15.5 s, and 260 of the 276 that died
in `r62bd-x2-6201` never kept a meal (the budget's tally). The group asks:

1. Does a pace factor of 3, with a newborn given half its share as reserve, let grazer lines keep themselves going and
   meet their upkeep (D222's tests 1 and 2)?
2. Does the thrust arm, which makes bodies swim faster without slowing the clock, do as well?
3. Is the newborn's reserve binding? With the pace factor at 3 and the reserve left at 0.2, do bred grazers still
   starve before their first meal?
4. Do encounters rise about threefold, as they must if a grazer's search is a swim at its speed?
5. Does it overshoot, with grazers grazing the leaves down until the oases fail?

## The worlds

Three worlds, founded on seed 6201, 24,000 world seconds each, in one card group (`rounds/launch-r62p1.ps1`). Each
is `r62kg`'s world (`rounds/env-r62kg.ps1`: `r62cs`'s 500 s arm, the kill gut on, the intake scale at 16) with:

| World | Pace factor | Limb push | Work price | Newborn reserve | Settings |
|---|---|---|---|---|---|
| `r62p-k3-6201` | 3 | 0.3 | 0.5 | 0.5 | `rounds/env-r62p-k3.ps1` |
| `r62p-t3-6201` | 1 | 2.7 | 0.0556 | 0.5 | `rounds/env-r62p-t3.ps1` |
| `r62p-k3n-6201` | 3 | 0.3 | 0.5 | 0.2 | `rounds/env-r62p-k3n.ps1` |

The physics step stays at 0.01 s in all three. The step check (`rounds/check-pace-step.ps1`) found swim speed holding
at 0.02 and 0.025 s but the stroke damped by the limiters, by about a fifth at 0.02 s and a half at 0.025 s. At the
pace factor 3, a metabolic step holds 150 physics steps.

The thrust arm sets the limb push at 9 times its 0.3 and the work price at a ninth of its 0.5. Both stroke-thrust
terms scale with the limb push, and drag on a bending limb, which it does not touch, carries about 1 % of a
swimmer's speed. So the thrust is 9 times today's, a body swims about 3 times as fast against quadratic drag, and its
billed power, 27 times the physical power at a ninth of the price, is 3 times today's (derived).

The pace arms bill the same: a stroke is the same physics, three times as often a world second, so work a metre is
unchanged and work a world second triples (derived from the build, `logbook/specs/pace-build.md`). With swimming's
work at about 0.09 W today (guessed), it is about 0.27 W in all three worlds, and a grazer's standing cost rises from
0.91 W to about 1.09 W.

The config hashes and the build's three hashes are read off the identity sitting and written in `identity.md` before
the launch. The launch's manifests must carry the same.

The controls are not run in this group:
- `r62kg-6201`, the same world at the pace factor 1. It runs after this group, on the coordinator's reorder of
  2026-10-26, and before this group's write-up, which needs it.
- `r62cs-w500-6201`, the base with no gut, and `r62bd-x2-6201`, the deeper wound, both recorded.

## The identity sitting, before the launch

On the card, by D239: the build at the pace factor 1 steps the gut build's world bit for bit, and a world at 3 is
identical at groups 32 and 64. This group shares one card process between worlds at 1 and at 3, so the sitting's
groups hold one world at each, side by side. Its record is `identity.md`. The group launches only after the sitting
passes.

## The tests

Read per world at 24,000 s by `scripts/reads/r62-read.ps1 -Worlds`, with D222's pass marks, as `r62kg` is.

1. **Lines keep themselves going.** Born share at least 0.80, a cohort's replacement of at least 1, and a mouthed
   chain of at least 10.
2. **Upkeep through the summers.** Grazers alive at every season end, and at least 0.9 of their upkeep met in the
   summer windows.
3. **Living food.** The share of grazers' kept intake from living bodies is at least 0.5 in the last 3,000 s bin.
4. **Travel**, reported beside the controls with no pass mark: `r60-read.py`'s migrants and `scripts/reads/travel.py`.

A world passes when tests 1 and 2 hold together and test 3 holds.

Beside the tests come the deciding numbers. Each is read against `r62kg-6201` and `r62bd-x2-6201`:
- the grazer contact share, the biting steps a grazer-second, from `mouthBlows`. Until the pace build the column was
  written only when the bite damage was off 1. So `r62kg-6201` and `r62cs-w500-6201` lack it, and the share is read
  against `r62bd-x2-6201` alone. The kills a grazer-second come from the kill rows, which every control has, and are
  read against `r62kg-6201`;
- the grazers' kept intake over their upkeep;
- from `scripts/reads/grazer-tally.py`, for bred and founder grazers apart: the median age at death, the share that
  died without keeping a meal, and the cause of death;
- the children a grazer has in its life, against D237's target of two to three;
- the oases' island share, with the pace-of-life read's guard of 0.6. If the current, converted at the seam, still
  drifts leaves per world second as before, the share stays above it.

## The overshoot, and what follows it

The overshoot is the owner's aim for these screens (the owner's guidance of 2026-10-24). So it has a mark and an
action, and no pass. It is read at each 3,000 s bin to 24,000 s. A world overshoots when three things hold at the
same second:
- the leaves are below half of `r62cs-w500-6201`'s leaf count at that second;
- grazers lead the fall: their kept intake from leaves is the largest share of what the leaves lose, and grazer lines
  are breeding (a cohort's replacement of at least 1 over the bin before);
- the oases' island share is at or above 0.6, the guard. Below it, the leaves were carried off rather than eaten, and
  the fall is not the grazers' overshoot.

If a world overshoots, the next screen rolls back from that world's dials: the pace factor, the work price, the limb
push and the newborn reserve it ran with. The rollback lowers the dial or dials the read names as the cause, in steps,
until the leaves hold. Which steps to take is the screens' choice, delegated by the owner on 2026-10-24. Any new world
rule is still the owner's.

## When the group stops

The coordinator ruled that it stops once it has answered. It is read at 6,000 s and 12,000 s. The answer is in at
12,000 s if two things hold in every world: no bred grazer has bred, and grazers have not kept their upkeep over the
last 3,000 s. Then the group is stopped with `scripts/stop-arm.ps1`, and its tests are read at the stop second and
said to be. Otherwise it runs to 24,000 s. A card resume does not repeat its run on this line of builds. So a world
stopped for its pace (below) is rerun from its start, or read as a cousin from its resume second on, and the read
says which.

## Predictions

These are guesses unless marked derived. The budget's terms are world means, and its own tally shows the mean hides
two kinds of grazer, so every number here may be off by the spread.

**Encounters.** In `r62p-k3` and `r62p-k3n` the kills a grazer-second rise 2.5 to 3.5 times over `r62kg-6201`'s,
and the grazer contact share as far over `r62bd-x2-6201`'s. In `r62p-t3` both rise as far if its bodies swim 3 times
as fast.

**The adult.** At the pace factor 3 a meal takes 33 to 37 s at the world-mean search, against the 36 to 38 s the
target allows at the higher standing cost (derived). So the adult meets the target only just. Kept intake over
upkeep rises to between 1.1 and 1.3 in `r62p-k3` and `r62p-t3`. The share of founder grazers that die without a meal
falls from 0.49 in `r62bd-x2-6201` to under 0.3.

**The newborn, and the reserve arm.** At 0.5 a child of the pool's grazers is born with about 34 J of reserve and
about 34 J of tissue, from the capped share of 67.5 J (derived; whether the share is still capped at the new split is
not verified). That lasts about 37 s at an adult's standing cost, the coordinator's figure, and up to about 60 s if
upkeep scales with the smaller body (inference). The first meal at the world-mean search takes 33 to 37 s. At 0.2 the
child's 13.5 J lasts about 15 s, under half that. So:
- in `r62p-k3`, at least 0.3 of bred grazers keep a meal before they die, against 0.06 in `r62bd-x2-6201`, and their
  median age at death is above 30 s;
- in `r62p-k3n`, bred grazers still die near 15 s, and under 0.15 of them keep a meal.

This is the test of the reserve. If `r62p-k3n`'s bred grazers keep a meal at least two-thirds as often as
`r62p-k3`'s, the reserve was not binding at the pace factor 3. My reading, which is inference, is that a newborn
starts beside a parent that has found food, so its first search may be shorter than the world mean; if so, the
reserve binds less than the arithmetic says.

**The tests.** `r62p-k3` and `r62p-t3` pass test 1. Test 2 holds in at most one of them, since the adult's margin is
thin. `r62p-k3n` fails test 1, because its bred grazers starve. Test 3 holds wherever grazers breed, since kills are
nearly all of leaves (`logbook/specs/kill-rule-facts.md`).

**The two schemes.** `r62p-k3` and `r62p-t3` agree on the encounters, the intake over upkeep and the tests to within a
third. They differ where the clock does: at the pace factor 3 a body strokes three times as often a world second and
sinks three times as far, and under the thrust arm it does neither.

**The overshoot.** Any world that passes test 1 overshoots by 24,000 s, by the three marks above. The island share
stays at or above 0.6 in all three.

**The machine.** The group runs at about 1.3 times real time, the coordinator's estimate, about 5 hours to
24,000 s. No world nears the ceiling of 25,000 bodies.

## What would change the plan

- If the identity sitting differs at 32 and 64, or at the pace factor 1 differs from the gut build, the build is
  probed before the group launches.
- If kills a grazer-second rise much less than 2.5 times at the pace factor 3, search is not a swim at the grazer's
  speed, and the lever is the nose (D235), not the pace. Group 2 waits for a ruling.
- If `r62p-t3` passes where `r62p-k3` fails, or the reverse, the scheme that failed is read for what the clock changed
  before group 2 is chosen.
- If a world's island share falls under 0.6, the current's conversion is probed. At the pace factor 3 a body also
  sinks three times as far a world second, so the probe reads the bed share and mean height beside it, since a leaf
  that sinks off an island lowers the share as a leaf carried off does.
- If a world's pace falls below half its own pace of the hour before, it is stopped and probed (the pace rule).
- If a world's matter residual passes ten times the long baseline's drift of 2.54e-5, the world is stopped and the
  routes probed.
- If a world reaches 25,000 bodies, its guilds are read. It is called the overshoot only if grazers lead it.
