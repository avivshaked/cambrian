# The deeper-wound screen's predictions: a mouth's blow at 2, 4 and 16 times its damage

*2026-10-24, committed on `mouth-point` before the screen's launch. The rulings are D226 in `DECISIONS.md`, which adds
the bite damage scale, and D227, which sets the arms after the check (`check/README.md`). The tests and the pass rule
are D222's. The world is `r62cs`'s 500 s arm (`logbook/specs/r62cs-prereg/predictions.md`, read in
`logbook/specs/r62cs-read/README.md`). This file names only what differs. Every number below that is not a
measurement is a guess, and says so.*

## What it asks

In `r62cs` a living bite took its share of the body's store as well as its tissue, and no arm passed. A kill kept 56
to 89 J, against about 420 J under round 62's corpse rule. Grazers kept 0.43 to 0.52 of their upkeep, while killing
at the controls' rate or higher. The window the remains stand for hardly mattered.

D226 deepens the wound a mouth deals: its blow's damage is multiplied by the bite damage scale before it becomes a
share of the bitten part's health, and so of its tissue and store. A claw's blow and a blow from remains are
unchanged. The check found that the wound saturates by 4 times. At 4, 16 and 64 times the worlds were the same world
bit for bit to 4,500 s, because every mouth's blow already finished the part it touched. D227 therefore screens 2, 4
and 16 times. The screen asks:

1. Does any arm make grazer lines keep themselves going and meet their upkeep (D222's tests 1 and 2)?
2. Does most of a grazer's food still come from living bodies (test 3)?
3. Which arm overshoots, with grazers so strong that they graze the leaves down until the oases fail? That arm sets
   where the roll-back starts.
4. Does a deeper wound raise income through the meal a kill keeps, or through more kills a second? A body holds
   only so much tissue, so the meal a kill keeps may stay the same while kills come faster.
5. Do the leaves evolve armour against a saturated bite? In the 16x arm, does the share of a mouth's touches that
   finish a part fall through the run?
6. How far do bodies travel, and do grazers move between islands?

## The world

`r62cs`'s 500 s arm (`rounds/env-r62cs-w500.ps1`): contact eating on, the store share on, the remains' window at
500 s. To it each arm adds `EVOSIM_BITE_DAMAGE_SCALE` at 2, 4 or 16, and `EVOSIM_INTAKE_SCALE` at 16. Under contact
eating the intake scale lifts only the bite's cap. In the check it cut half of a deeper bite at an intake scale of 1,
4.5% at 4 with the dial at 4, and nothing at 16 (`check/README.md`). So every arm runs at 16, and the arms differ in
the wound alone. Six worlds, founded, 24,000 s each, in one group on the card: `r62bd-x2`, `r62bd-x4` and `r62bd-x16`
on seeds 6201 and 6202 (`rounds/env-r62bd-x<scale>-i16.ps1`, `rounds/launch-r62bd.ps1`).

Config hashes, read off the launch build's identity runs (2026-10-24, each seed pair alike): the 2x arm
`6da8f3c6156c8a0e`, the 4x arm `490de19bef1482d0` and the 16x arm `169145a496b923e7`. The build is `4e94b0e0`.

The controls are not run again:
- `r62cs-w500-6201` and `r62cs-w500-6202`, the rule at a scale of 1;
- `r62ce-6201` and `r62ce-6202`, without the store share;
- round 62's `r62-h6201` and `r62-h6202`, the corpse rule.

With the dial at 1 the build steps `r62cs`'s world bit for bit. On the CPU solver the crowd fixture shows it
(`logbook/specs/contact-eating-build/README.md`), and on the card the identity runs at groups 32 and 64 show it
(`identity.md`).

## The tests

Each test is read per world at 24,000 s by `scripts/reads/r62-read.ps1 -Worlds`, as `r62cs` was. The tests and their
pass marks are D222's.

1. **Lines keep themselves going.** Born share at least 0.80, a cohort's replacement of at least 1, and a mouthed
   chain of at least 10.
2. **Upkeep through the summers.** Grazers alive at every season end, and at least 0.9 of their upkeep met in the
   summer windows.
3. **Living food.** The share of grazers' kept intake from bites on living bodies, against bites on remains, is at
   least 0.5 in the last 3,000 s bin. Its trend from the 12,000 to 15,000 s bin is reported, and no rise is
   required.
4. **Travel**, reported beside the controls with no pass mark: the migrants of `r60-read.py` and
   `scripts/reads/travel.py`.

An arm passes when tests 1 and 2 hold together in both its worlds and test 3 holds in both.

Beside the tests, each world reports the following:
- the grazers' kept intake over their upkeep, their kills per grazer-second and the kept joules a kill, all three
  against `r62cs-w500`'s (question 4);
- the share of a mouth's blows that finish the part they touch, per 3,000 s bin, from the stats columns
  `mouthBlowsFinishing` over `mouthBlows` (question 5). A share that falls through the 16x arm means armour is
  evolving; one that stays near 1 means the bite stays saturated;
- its peak population against the ceiling of 25,000, and when it came;
- the guild mix (leaves, absorbers, jointed, grazers) at 12,000 and 24,000 s;
- the remains standing, the joules the store share gave (`biteStore`) and the joules burnt when remains closed;
- the share the cap cut from what bites offered, per 3,000 s bin (`scripts/reads/bite-cap.py`).

The overshoot is read as overpowered grazers, with no pass mark. It means grazers breeding lines while the leaves
fall below half of `r62cs-w500`'s count on the same seed by 24,000 s. The oases emptying counts too, as `r60-read`'s
share of the living over the islands falling through the run. A crowd near the ceiling is read by guild before it
is called an overshoot. In `r62cs` the 60 s worlds grew absorbers, not grazers, toward it.

If the 2x arm also saturates, with nearly every mouth's blow finishing its part from the start, that is a result. It
is read as such and not rerun (D227).

## The limit on stopping a world

A card resume on this build does not repeat its run. Resumed from 1,500 s or 6,000 s, `r62cs`'s worlds parted from
their own rows at the first row after the checkpoint (`logbook/specs/r62cs-prereg/identity.md`). So a world of this
screen that is stopped before 24,000 s is rerun from its start, or read as a cousin from its resume second on, and
the read says which.

## Predictions

These are guesses. The only measurement behind them is the check's 4,500 s on one seed, with founders alone grazing.

- The 4x and 16x arms raise the grazers' kills per grazer-second three times or more over `r62cs-w500`'s, as in the
  check, while the kept joules a kill stay near `r62cs`'s 56 to 89 J or fall.
- Intake over upkeep rises above 0.9 in the 16x arm in at least one world, and test 1 holds there. Tests 1 and 2 hold
  together in both worlds of at least one arm.
- The 2x arm sits between `r62cs-w500` and the 4x arm on every economic column, with a share of finishing blows well
  below 1.
- The 16x arm overshoots: the leaves fall below half of `r62cs-w500`'s count by 24,000 s in at least one world.
- In the 16x arm the share of finishing blows stays near 1 throughout. The leaves do not evolve armour enough in
  24,000 s to survive a blow at 16 times.
- No world reaches the ceiling of 25,000.

## What would change the plan

- If even the 4x arm fails tests 1 and 2, the wound is spent as a lever: at 4 times a touch already takes the whole
  part. The next lever is the meal itself, which is bounded by one part. A kill could feed from the whole body, or a
  bite could take from parts beyond the one it touches. That is put to the coordinator.
- If a world's matter residual passes ten times the long baseline's drift of 2.54e-5, the world is stopped and the
  routes probed.
- If a world's pace falls below half its own pace of the hour before, the world is stopped and probed (the pace
  rule).
- If a world reaches 25,000, its guilds are read. It is called the overshoot only if grazers lead it, and the
  others run on.
