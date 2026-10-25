# The contact-eating screen's predictions: round 62's world with mouths that eat only what they touch

*2026-10-23, committed on `mouth-point` before the screen's launch. The rulings are D219 to D221 in `DECISIONS.md`, on
the build spec `logbook/specs/contact-eating-spec.md`, and D222 for the tests and the pass rule. The world and the read are round 62's
(`logbook/specs/r62-prereg/predictions.md`, and D215 and D216 for the tests). This file names only what differs. Every
number below that is not a measurement is a guess, and says so.*

## What it asks

Under round 62's rule a grazer ate corpses within 3 m, and the share of its food from its own kills fell in every
world (D215's third test, 0 of 6) and in the fast-rot screen too (1 of 6, D217). D219 to D221 replace the rule. A
mouth eats only what it touches, a bite feeds as it wounds, and the dead stay in the water as edible remains for
60 s. The screen asks four things.

1. Do grazer lines keep themselves going when every meal has to be bitten off a body? (D215's first test.)
2. Do they meet their upkeep through the summers? (D215's second test.)
3. Does most of a grazer's food come from bodies it bites while they live, rather than from remains? (D215's third
   test, restated for a world without corpses; see "The tests".)
4. How far do bodies travel, and do grazers move between islands?

## The world

Round 62's world as relaunched (`rounds/env-r62-h.ps1`): the islands' influx at 3.6 matter/s, a 3 m reach and a
500 s corpse half-life, on `mouth-point`'s build. Two lines differ, in `rounds/env-r62ce.ps1`: `EVOSIM_CONTACT_EATING`
on and `EVOSIM_REMAINS_WINDOW` at 60. With the dial on no corpse is made, so the reach and the half-life are read by
nothing.

Three worlds, founded, 24,000 s each, in one group on the card, on seeds 6201, 6202 and 6203: `r62ce-6201` to
`r62ce-6203` (`rounds/launch-r62ce.ps1`). The controls are round 62's `r62-h6201` to `r62-h6203` at 500 s, which are not
run again. The build with the dial off is bit for bit round 62's. On the CPU solver, a resume of `r62-h6202` was
compared row for row with the same resume on the recording's build, on the stage 1 build (2026-10-23) and again on the
final build (2026-10-24), with no value apart. On the card, the identity runs at groups 32 and 64 prove it before the
launch.

Config hash, read off the accelerator preflight (`rounds/preflight-r62ce.ps1`, 2026-10-24, the three worlds alike):
`a99d10a5c2485585`. The controls' is `c5c9f4167e070655`.

## The tests

Each is read per world at 24,000 s, by the same readers as round 62's (`scripts/reads/r62-read.ps1 -Worlds`), whose
intake reads the kept joules under either rule.

1. **Lines keep themselves going.** Born share at least 0.80, a cohort's replacement of at least 1, and a mouthed
   chain of at least 10. As D215.
2. **Upkeep through the summers.** Grazers alive at every season end, and at least 0.9 of their upkeep met in the
   summer-start windows. As D215.
3. **Living food.** The share of grazers' kept intake from bites on living bodies, against bites on remains, is at
   least 0.5 in the last 3,000 s bin. Its trend from the 12,000 to 15,000 s bin is reported, and no rise is required.
   This restates D215's own-kill share for a world without corpses (D222): under the old rule a grazer's own kills
   stood against carrion, and here a bite on a living body stands against a bite on the dead.
4. **Travel**, reported beside the controls with no pass mark (D222): the bodies seen over one island and then over
   another in each year (`r60-read.py`'s migrants), and each body's path length per 1,000 s and its distance from
   where it was first seen, by guild (`scripts/reads/travel.py`).

The screen passes when tests 1 and 2 hold together in at least 2 of the 3 worlds, and test 3 holds in at least 2 of
the 3 (D222).

## Predictions

These are guesses, with no measurement behind them on this rule.

- Contact is rarer than reach, so fewer grazers are born than in the controls by 12,000 s. Lines that keep going
  carry bodies whose mouths point forward and that move toward leaves.
- Test 1 holds in at least 2 of the 3 worlds. If it fails in all three, the next screen raises `IntakeScale`, the
  yield dial D219's risk names.
- Remains feed under a fifth of the grazers' intake, since 60 s is short: test 3 passes in all three worlds.
- The Nutrients field falls well below the controls' and the absorbers with it, while the leaves gain the matter
  the corpses used to send to the water (the spec's section 7, inference).
- Grazers travel further per 1,000 s than in the controls, since a meal no longer comes within 3 m of a resting body.
- The remains standing at any time number a few hundred, as D220 estimates.
- Pace at least 1x real time with three worlds on the card; the corpse pass is gone, and the remains add a few
  hundred bodies to the solver.

## What would change the plan

- If grazers die out in every world before 12,000 s, the screen has answered its first question, and the next step
  is a screen of `IntakeScale`, put to the owner.
- If a world's matter residual passes ten times the long baseline's drift of 2.54e-5, the world is stopped and the
  routes probed, since every route is meant to close by construction.
- If the pace falls below half its own of the hour before, the world is stopped and probed (the pace rule).
