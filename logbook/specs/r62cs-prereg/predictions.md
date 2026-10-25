# The overshoot screen's predictions: a living bite takes its share of the store, and remains last longer

*2026-10-24, committed on `mouth-point` before the screen's launch. The ruling is D224 in `DECISIONS.md`, which
supersedes D223. The tests and the pass rule are D222's. The world and the read are the contact-eating screen's
(`logbook/specs/r62ce-prereg/predictions.md`, read in `logbook/specs/r62ce-read/README.md`). This file names only
what differs. Every number below that is not a measurement is a guess, and says so.*

## What it asks

In `r62ce` a bite took a share of a part's tissue and nothing else. A dying body's reserve went to remains that
closed unbitten in 60 s, and the grazers never bred a line: 1 to 5 were bred in each world, against 711 to 4,727 in
round 62's controls. The intake cap cut at most 5% to 8% of what bites took, so no yield setting could close the
gap. The owner set the tactic: overshoot until grazers are overpowered, then roll the dials back. D224 adds two
changes. A bite on a living body now also takes the same share of the body's reserve and gestation account as of its
tissue. And the remains last longer. The screen asks:

1. Does any arm make grazer lines keep themselves going and meet their upkeep (D222's tests 1 and 2)?
2. Does most of a grazer's food still come from living bodies (test 3) as the window lengthens?
3. Which arm overshoots: grazers so strong that they graze the leaves down until the oases fail? That arm sets where
   the roll-back starts.
4. How far do bodies travel, and do grazers move between islands?

## The world

`r62ce`'s world (`rounds/env-r62ce.ps1`), with `EVOSIM_BITE_TAKES_STORE` on and the remains' window set per arm,
in `rounds/env-r62cs-w60.ps1`, `-w500.ps1` and `-w1500.ps1`. Six worlds, founded, 24,000 s each, in one group on the
card: the windows of 60, 500 and 1,500 s, each on seeds 6201 and 6202, `r62cs-w<window>-<seed>`
(`rounds/launch-r62cs.ps1`). The 500 s window is round 62's corpse half-life.

The controls are not run again. They are `r62ce-6201` and `r62ce-6202` (the rule without the store share), and
round 62's `r62-h6201` and `r62-h6202` (the corpse rule). With the new dial off the build is bit for bit `r62ce`'s.
On the CPU solver a resume of `r62-h6202` is compared row for row with the same resume on the recording's build. On
the card the identity runs at groups 32 and 64 prove it, and they compare the dial-off world with `runs/r62ceid-on-g32`.

Config hashes, read off the accelerator preflight (`rounds/preflight-r62cs.ps1`, 2026-10-24, each seed pair
alike): the 60 s window `36147b559fe8eeb7`, the 500 s window `0f736451c27d06d8` and the 1,500 s window
`b5de43749017cfbb`. The build is `d8e4f2e2`.

## The tests

Each test is read per world at 24,000 s, by `scripts/reads/r62-read.ps1 -Worlds` as `r62ce` was. The tests and their
pass marks are D222's.

1. **Lines keep themselves going.** Born share at least 0.80, a cohort's replacement of at least 1, and a mouthed
   chain of at least 10.
2. **Upkeep through the summers.** Grazers alive at every season end, and at least 0.9 of their upkeep met in the
   summer windows.
3. **Living food.** The share of grazers' kept intake from bites on living bodies, against bites on remains, is at
   least 0.5 in the last 3,000 s bin. Its trend from the 12,000 to 15,000 s bin is reported, and no rise is
   required.
4. **Travel**, reported beside the controls with no pass mark: the migrants of `r60-read.py` and `scripts/reads/travel.py`.

An arm passes when tests 1 and 2 hold together in both its worlds and test 3 holds in both. Beside the tests, each
world reports its peak population against the ceiling of 25,000 and when it came. It also reports the guild mix
(leaves, absorbers, jointed, grazers) at 12,000 and 24,000 s and the remains standing. Last come the joules the store
share gave (`biteStore`) and the joules burnt when remains closed.

The overshoot is read as overpowered grazers, with no pass mark. It means grazers breeding lines while the
leaves fall below half of `r62ce`'s count on the same seed by 24,000 s. The oases emptying counts too: r60-read's
share of the living over the islands falling through the run.

## Predictions

These are guesses, with no measurement behind them on this rule.

- The store share alone (the 60 s arm) multiplies a grazer's kept joules a bite several times over, since a leaf's
  reserve is larger than the tissue a wound takes. Test 1 holds in at least one of its two worlds.
- The 500 s arm passes tests 1 and 2 in both worlds, and the remains feed a third or more of the grazers' intake,
  while test 3 still holds.
- The 1,500 s arm overshoots: leaves fall below half of `r62ce`'s by 24,000 s in at least one world. Remains
  standing number in the thousands, and the arm's pace falls below 1 times real time. Test 3 fails in at least one
  world as carrion takes over.
- No world reaches the ceiling of 25,000, because grazers crop the leaves that filled `r62ce`'s worlds.
- Grazers travel further than in `r62ce`. No grazer migrates between islands in the 60 s arm.

## What would change the plan

- If no arm passes tests 1 and 2, the store share and the window together are too small. The next step is then a
  bigger dial, a longer window or a deeper wound, put to the coordinator.
- If a world's matter residual passes ten times the long baseline's drift of 2.54e-5, the world is stopped and the
  routes probed.
- If a world's pace falls below half its own pace of the hour before, the world is stopped and probed (the pace
  rule). The 1,500 s arm's slow pace from its remains is expected, and is judged against its own hour before.
- If a world reaches 25,000, its ending is read as the overshoot, and the others run on.
