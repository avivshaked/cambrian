# 0040 — Right-sizing the dish

**2026-08-30**  ·  food-chain goal, round 5 · pre-registered before launch

Same shape as 0036–0039: everything above *Results* was written and committed before any arm
was launched.

This round is a dose probe rather than a scored round. There is one seed per irradiance
value, so the goal's 3-of-5 rule is not being tested here. The round's product is a number
for round 6 to use.

## The hypothesis

[logbook/0039](0039-a-slower-drought.md) ended with the producers fixed and the world
unscoreable. At senescence 10,000 s nothing kills a lit population at 0.02, and at
irradiance 200 its equilibrium sits above the 8,000-creature ceiling the machine can afford.
That equilibrium must exist, since light income is a finite rate and matter a finite stock.
[D053](../DECISIONS.md#d053) chooses to scale the world down rather than chase the
equilibrium up.

The claim under test is that **lowering irradiance moves the round-4 world's equilibrium
below the instrument**, roughly in proportion. It should not reopen the extinctions that
senescence 10,000 closed. Somewhere between irradiance 25 and 100 there is a world that
finishes its run alive, uncensored, at a population the machine can carry.

## The world

Round 4's world unchanged, with mixing 0.2, `excessDensity` 0.02 and senescence 10,000 s.
The floor closes at 3,000 s, remin is 0, the ceiling is 8,000, and the rest is
[D048](../DECISIONS.md#d048)'s reference settings.

The one change is **`EVOSIM_IRRADIANCE`: 200 → {100, 50, 25}**, one arm each, seed 1, 30,000
s, 600 min wall.

The arms are `d055-i100` and `d055-i50`.

The third is `d055-i25`, and three arms leaves two workers free.

Seed 1 at irradiance 200 is `d054-s1`, a runaway at t=25,998 with 8,004, which serves as
this probe's fourth dose point and is already run.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| T1 | `floor` = 0 at every sample after t=3,100 in every arm | `floor` |
| T2 | no extinctions — the dimmer sea does not reopen what senescence closed | run footer |
| T3 | peak `alive` is monotone in irradiance across the four dose points (25 < 50 < 100 < 200) | `alive` |
| T4 | at least one arm ends at t=30,000 uncensored with mean `alive` over its last 10 samples between 500 and 3,000 — that irradiance is round 6's world | footer, `alive` |
| T5 | the i100 arm does **not** reach the ceiling (a halving of income puts the equilibrium below 8,000) | run footer |

The probe succeeds if T2 and T4 hold. T4's arm's irradiance is then fixed as round 6's world
before round 6 is registered.

## The two-sided reading, written before the answer

- **All three run away (T5 fails everywhere):** light is not the binding limit below 8,000 —
  the matter stock is. The next knob is the matter side (initial stock, or area), and D053's
  deferred area option comes back.
- **All three go extinct (T2 fails):** below some income the 0.02 world cannot found or
  cannot survive its first drought — the photic band at irradiance 25 is a quarter the
  depth. Probe the gap (150) before concluding the approach is wrong; founding happens
  under the open floor, so read whether death comes before or after t=3,000.
- **T3 fails:** with one seed per value, a non-monotone dose curve is likelier noise than
  mechanism — the founding lottery differs per world even at fixed seed, because the world
  differs. Do not over-read it; rerun the offending value on seeds 2–3 before believing it.
- **T4 fails with T2 holding** (every survivor equilibrates above 3,000 or below 500):
  interpolate and run one more arm at the interpolated value; the probe's product is the
  number, not this particular grid.

**Uninterpretable, and to be reported as such:** an arm ended by its wall before t=15,000.

## Results

### Interim: the whole grid is a famine, and the probe moves up

All three arms went **extinct within minutes of wall clock**. The three went at t≈4,130
(i25), t≈4,337 (i50) and t≈6,433 (i100), each shortly after the floor closed at 3,000. Each
had barely founded at all. Their totals were 26, 22 and 33 births, against `d054-s1` at
irradiance 200, which had ~950 births by t=2,000.

The trace is not a drought. Deaths ran 12–40 per window from t=100 with the floor holding
the population at 40. **T2 is falsified across the grid.**

This is an energy famine rather than a matter crash. At half the light, a founder at −10 m
cannot cover upkeep at all, so the founding lottery never starts.

Extinction time is monotone in dose, so T3's ordering holds in the mirror, which says
irradiance is reaching the world. The world under 200 W/m² is on the wrong side of a
founding cliff somewhere in (100, 200).

The pre-registration's own reading for this branch is to probe the gap before concluding the
approach is wrong. `d055-i150` and `d055-i175` launched, same world, same seed. If 150 dies
and 175 runs away, the window for "alive but bounded" is narrow to empty. D053's deferred
option, shrinking the area while keeping per-creature margins intact, then comes back as the
main road.

Caught by the header check, and worth recording: workers 2 and 3 were running an
`EvolutionRun.cs` from before `EVOSIM_MAX_POP` and the exact seed parse.

Their headers printed no `ceiling`.

A full `Assets/` diff showed nothing else stale, the seed parses identically, and neither
arm approached any ceiling, so i50 and i25 stand. The workers were refreshed before the gap
arms launched.

`d054-s2` ran past 5,000 without a cut, so round 4's workers were current and no earlier
round is contaminated.

### The gap is empty, and irradiance cannot right-size this world

Both gap arms founded, with 946 and 1,521 alive by t≈4,500 and thousands of births, so the
famine cliff is between 100 and 150. Both then went **extinct at their droughts**,
`d055-i150` at t=9,406 and `d055-i175` at t=16,925. The full seed-1 dose curve, all six
points:

| W/m² | 25 | 50 | 100 | 150 | 175 | 200 |
|---|---|---|---|---|---|---|
| fate | extinct 4,130 | extinct 4,337 | extinct 6,433 | extinct 9,406 | extinct 16,925 | runaway 25,998 |

Time-to-death is perfectly monotone in dose, and the window between "dies" and "runs away"
is **empty at this seed**. T2 and T4 are falsified, and the probe failed.

The reading: irradiance scales what one creature earns, so it moves the *whole* trajectory,
founding, drought depth and recovery together. The world it makes at every dose is the same
world on a slower or faster clock. It cannot set how many creatures the world holds without
also setting whether any single creature can live.

Worth noting: `d054-s1` at 200 fell to 73 alive at t=16,500 before recovering. Seed 1 at 200
was itself a near-death, and the cliff between 175 and 200 is thinner than the table makes
it look.

What *would* rescale the dish is the knob D053 deferred, **`WorldAreaSquareMetres`**. It is
the sun's aperture, every layer's volume, and the denominator of shading at once, so halving
it halves total income and total stock at identical per-creature margins.

Shading is the fraction of incoming light the standing population intercepts, computed from
the creatures' total lit area *per unit world area*. The same bodies in half the area darken
the water twice as much.

It was a tunable already, and `EVOSIM_AREA` now reaches it, printed in the header as
`· area X m2`.

### Round 5b, pre-registered before launch, same night

Round 4's world again, at irradiance 200, 0.02, senescence 10,000, floor 3,000, ceiling
8,000 and mixing 0.2, with **`EVOSIM_AREA` 400 → 100 and 200**.

The arms are `d055b-a100-s1` and `d055b-a100-s2`, at 30,000 s and 600 min wall.

The third is `d055b-a200-s1`.

| # | prediction | falsified by |
|---|---|---|
| U1 | `floor` = 0 after t=3,100 everywhere | `floor` |
| U2 | founding works at both areas — >500 births by t=3,000 (per-creature margins are untouched) | `births` |
| U3 | at most 1 of 3 arms goes extinct | run footer |
| U4 | no a100 arm reaches the 8,000 ceiling | run footer |
| U5 | at least one arm ends at t=30,000 uncensored with mean `alive` over its last 10 samples in 500–3,000 — that area is round 6's world | footer, `alive` |
| U6 | a mutant arrives in ≥1 arm | `absorpt`, `inherit` |

The two-sided reading, before the answer. If U4 fails, the 400 m² equilibrium is above
32,000 and the probe continues downward at 50 m².

If U3 fails by drought deaths, the smaller dish is the same world with smaller absolute
numbers. The drought that left 73 survivors at 400 m² leaves ~18 at 100 m², and demographic
noise finishes what the drought started. Then the dish cannot be shrunk without first
softening droughts, and round 6, [D052](../DECISIONS.md#d052)'s excretion, runs at 400 m²
with the ceiling accepted as a censor.

If U2 fails, area is not the clean rescale the code reading says it is, and the thing to do
is find what else reads 400.

### Final: round 5b, scored

All three arms went **extinct**. `a100-s1` went at t=20,280 and `a100-s2` at t=23,115.

The third, `a200-s1`, went at t=25,139.

U1 held, with the floor silent everywhere. **U2 held**, at 643, 1,758 and 1,342 births by
t=3,000, so area is the clean rescale the code reading said and founding is untouched.

**U3 failed, 3 of 3**, and not by the predicted mode. No arm died at a drought bottom. In each,
births froze with *matter recovered*, at a top layer of 0.37–0.53/m³ with nothing blocked,
and hundreds alive.

The population then sank as one. `a100-s2` was at mean depth −55 m by t=8,000 with 541 alive
and its births already over. The last survivors drifted below −100 m, *well below the 60 m
nutrient field*, ageing at senescence-10,000 rates in water that is physically bottomless
for a body. That is the mirror of [D050](../DECISIONS.md#d050)'s "the ocean has no top":
nothing stops a sinking creature at the floor either, because the fields clamp and the
physics does not.

U4 held trivially and U5 failed. U6 failed, with no mutant in any arm, since three small
worlds produced 12,469 births against one arrival per ~23,000 at full size.

What the smaller dish actually showed: the failure mode is not demographic noise at a
drought bottom. It is **shading reaching lethal darkness at small absolute numbers**. At 100
m², 1,400 creatures shade the column like 5,600 at 400 m².

The density feedback that was supposed to limit the population instead darkens the water.
The deep majority stops earning, and this world's response to not earning is to sink,
passively and irreversibly. Shading limits by killing rather than by capping, because
nothing in a starving producer holds its depth.

That is why every knob this round has two outcomes and no middle. Whenever income beats
upkeep everywhere, the world runs away. Wherever it does not, the losing part of the
population exits the light forever and takes the lineage with it.

Round 5's verdict, whole: T2 and T4 falsified for irradiance, U3 and U5 falsified for area,
so
**no geometric rescale of this world yields a bounded living state.**

The missing thing is not a size. It is a stabilising response to scarcity: matter coming
back where the living are, which is D052 and next, or a body that can hold its depth when
starving. [D049](../DECISIONS.md#d049)'s buoyancy exists, and floaters died first in
[0038](0038-a-lighter-world.md)'s note, which is unexplained and now more interesting.

Per this round's own pre-registration, round 6 is D052's excretion at 400 m², with the
ceiling accepted and declared as a censor: [logbook/0041](0041-the-sea-digests.md).
