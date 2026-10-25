# The kill-meal screen's predictions: a kill swallowed whole into the killer's gut

*Draft, 2026-10-25, on `kill-gut`. It is committed before the screen's launch, once the check has run and the config
hashes are read. The ruling is D232 in `DECISIONS.md`, with the coordinator's readings in
`logbook/specs/d232-kill-gut-build.md`. The tests and the pass rule are D222's. The world is `r62cs`'s 500 s arm
(`logbook/specs/r62cs-prereg/predictions.md`), and this file names only what differs. Every number below that is not
a measurement is a guess, and says so.*

## What it asks

In `r62bd` a deeper wound raised kills but not the meal: a kill kept 61 to 77 J, against about 420 J under round 62's
corpse rule, because a bite can take only the part it touches (D230). D232 hands the killer the whole body. A kill by
a body with a mouth sends the dead body's remaining tissue and store into the killer's gut, and leaves no remains. The
gut is digested at the mouth's per-step cap, which bites fill first, less the intake waste. The screen asks:

1. Does the gut make grazer lines keep themselves going and meet their upkeep (D222's tests 1 and 2)?
2. Does most of a grazer's food come from living bodies (test 3)? A swallowed kill counts as living food, since the
   body was alive when bitten.
3. Does it overshoot, with grazers grazing the leaves down until the oases fail? If so, the roll-back is a slower
   digestion rate (D232).
4. How much does a kill now keep, and how much of it does the cap hold back in the gut?
5. With no remains from kills, what is left on the floor, and do scavengers vanish?
6. How far do bodies travel, and do grazers move between islands?

## The world

`r62cs`'s 500 s arm (`rounds/env-r62cs-w500.ps1`): contact eating on, the store share on, the remains' window at
500 s, the bite damage at 1. To it the screen adds `EVOSIM_KILL_GUT` and `EVOSIM_INTAKE_SCALE` at 16
(`rounds/env-r62kg.ps1`). Two worlds, founded, 24,000 s each, in one group on the card: `r62kg-6201` and
`r62kg-6202` (`rounds/launch-r62kg.ps1`).

Config hash, read off the launch build's identity runs: *to be filled in before commit*. The build is *to be filled
in*.

First, a check: `r62kgck-6201`, the same world founded for 4,500 s (`rounds/check-r62kg.ps1`), read for the gut's
size, the meal a kill keeps and the cap's cut before the machine spends a day.

The controls are not run again:
- `r62cs-w500-6201` and `-6202`, the base;
- `r62bd-x2-6201` and `-6202`, the deeper wound;
- round 62's `r62-h6201` and `r62-h6202`, the corpse rule.

With the switch off the build steps `r62cs`'s world bit for bit. The crowd fixture shows it on the CPU solver, and
the identity runs show it on the card at groups 32 and 64 (`identity.md`, *to be written*).

## The tests

Read per world at 24,000 s by `scripts/reads/r62-read.ps1 -Worlds`, as `r62bd` was, with D222's pass marks.

1. **Lines keep themselves going.** Born share at least 0.80, a cohort's replacement of at least 1, and a mouthed
   chain of at least 10.
2. **Upkeep through the summers.** Grazers alive at every season end, and at least 0.9 of their upkeep met in the
   summer windows.
3. **Living food.** The share of grazers' kept intake from living bodies, bites and swallowed kills together, against
   bites on remains, is at least 0.5 in the last 3,000 s bin.
4. **Travel**, reported beside the controls with no pass mark: `r60-read.py`'s migrants and `scripts/reads/travel.py`.

The screen passes when tests 1 and 2 hold together in both worlds and test 3 holds in both.

Beside the tests, each world reports:
- the grazers' kept intake over their upkeep, their kills per grazer-second and the kept joules a kill, all three
  against `r62cs-w500`'s and `r62bd-x2`'s;
- from the stats' gut columns, per 3,000 s bin: the joules swallowed, the kills swallowed, the joules digested, the
  joules held in guts, and the cap's cut on bites and on the gut;
- its peak population against the ceiling of 25,000, and when it came;
- the guild mix (leaves, absorbers, jointed, grazers) at 12,000 and 24,000 s;
- the remains standing, and the joules burnt when remains closed.

The overshoot is read as before, with no pass mark: grazers breeding lines while the leaves fall below half of
`r62cs-w500`'s count on the same seed by 24,000 s, or the oases emptying.

## The limit on stopping a world

A card resume does not repeat its run on this line of builds (`logbook/specs/r62cs-prereg/identity.md`). So a world
stopped before 24,000 s is rerun from its start, or read as a cousin from its resume second on, and the read says
which.

## Predictions

These are guesses. The proposal's inference, not a measurement, is the only basis for the first.

- A kill by a grazer keeps 350 to 450 J, near the corpse rule's 420 J, against `r62bd-x2`'s 61 to 77 J.
- The gut holds back a large share of each meal for many steps. The joules held in guts across a world stay above
  ten kills' worth once grazers breed.
- Intake over upkeep rises above 0.9 in both worlds, and tests 1 and 2 hold together in at least one.
- The screen overshoots in at least one world: the leaves fall below half of `r62cs-w500`'s count by 24,000 s. The
  corpse rule's own worlds held only about 6,000 bodies.
- Remains standing fall to under a third of `r62cs-w500`'s, since only deaths by age, starvation and claws leave them.
- No world reaches the ceiling of 25,000.

## What would change the plan

- If the check's kept joules a kill stay under 150 J, the gut is not handing the meal over, and the build is probed
  before the screen runs.
- If a world's matter residual passes ten times the long baseline's drift of 2.54e-5, the world is stopped and the
  routes probed.
- If a world's pace falls below half its own pace of the hour before, it is stopped and probed (the pace rule).
- If a world reaches 25,000, its guilds are read. It is called the overshoot only if grazers lead it.
- An overshoot sets the roll-back: a digestion rate below the mouth's cap, a dial built then, at the owner's
  delegation.
