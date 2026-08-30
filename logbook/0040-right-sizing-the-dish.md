# 0040 — Right-sizing the dish

**2026-08-30**  ·  food-chain goal, round 5 · pre-registered before launch

Same shape as 0036–0039: everything above *Results* was written and committed before any
arm was launched. **This round is a dose probe, not a scored round** — one seed per
irradiance value, so the goal's 3-of-5 rule is not being tested here; the round's product
is a number for round 6 to use.

## The hypothesis

logbook/0039 ended with the producers fixed and the world unscoreable: at senescence
10,000 s nothing kills a lit population at 0.02, and at irradiance 200 its equilibrium —
which must exist, since light income is a finite rate and matter a finite stock — sits above
the 8,000-creature ceiling the machine can afford. D053 chooses to scale the world down
rather than chase the equilibrium up.

**The claim under test: lowering irradiance moves the round-4 world's equilibrium below the
instrument, roughly in proportion, without reopening the extinctions that senescence 10,000
closed.** Somewhere between irradiance 25 and 100 there is a world that finishes its run
alive, uncensored, at a population the machine can carry.

## The world

Round 4's world unchanged — mixing 0.2, `excessDensity` 0.02, senescence 10,000 s, floor
closes 3,000 s, remin 0, ceiling 8,000, D048 reference settings — except
**`EVOSIM_IRRADIANCE`: 200 → {100, 50, 25}**, one arm each, seed 1, 30,000 s, 600 min wall.
Arms `d055-i100`, `d055-i50`, `d055-i25`. Three arms leaves two workers free.

Seed 1 at irradiance 200 is `d054-s1` (runaway t=25,998 with 8,004), which serves as this
probe's fourth dose point, already run.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| T1 | `floor` = 0 at every sample after t=3,100 in every arm | `floor` |
| T2 | no extinctions — the dimmer sea does not reopen what senescence closed | run footer |
| T3 | peak `alive` is monotone in irradiance across the four dose points (25 < 50 < 100 < 200) | `alive` |
| T4 | at least one arm ends at t=30,000 uncensored with mean `alive` over its last 10 samples between 500 and 3,000 — that irradiance is round 6's world | footer, `alive` |
| T5 | the i100 arm does **not** reach the ceiling (a halving of income puts the equilibrium below 8,000) | run footer |

**The probe succeeds if T2 and T4 hold.** T4's arm's irradiance is then fixed as round 6's
world before round 6 is registered.

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

---

## Results

### Interim — the whole grid is a famine, and the probe moves up

All three arms went **extinct within minutes of wall clock**: i25 at t≈4,130, i50 at
t≈4,337, i100 at t≈6,433 — each shortly after the floor closed at 3,000, and each having
barely founded at all (26, 22 and 33 births in total; `d054-s1` at irradiance 200 had ~950
births by t=2,000). The trace is not a drought: deaths ran 12–40 per window from t=100 with
the floor holding the population at 40. **T2 is falsified across the grid.** This is an
energy famine, not a matter crash — at half the light, a founder at −10 m cannot cover
upkeep at all, so the founding lottery never starts. Extinction time is monotone in dose
(T3's ordering holds in the mirror), which says irradiance is reaching the world; the world
under 200 W/m² is simply on the wrong side of a founding cliff somewhere in (100, 200).

The pre-registration's own reading for this branch: probe the gap before concluding the
approach is wrong. `d055-i150` and `d055-i175` launched, same world, same seed. If 150
dies and 175 runs away, the window for "alive but bounded" is narrow-to-empty and D053's
deferred option (shrink the area, keeping per-creature margins intact) comes back as the
main road.

Caught by the header check, worth recording: workers 2 and 3 were running an
`EvolutionRun.cs` from before `EVOSIM_MAX_POP` and the exact seed parse — their headers
printed no `ceiling`. A full `Assets/` diff showed nothing else stale, the seed parses
identically, and neither arm approached any ceiling, so i50/i25 stand; the workers were
refreshed before the gap arms launched. (`d054-s2` ran past 5,000 without a cut, so round
4's workers were current — no earlier round is contaminated.)

### The gap is empty — irradiance cannot right-size this world

Both gap arms founded (946 and 1,521 alive by t≈4,500, thousands of births — the famine
cliff is between 100 and 150) and both then went **extinct at their droughts**: `d055-i150`
at t=9,406, `d055-i175` at t=16,925. The full seed-1 dose curve, all six points:

| W/m² | 25 | 50 | 100 | 150 | 175 | 200 |
|---|---|---|---|---|---|---|
| fate | extinct 4,130 | extinct 4,337 | extinct 6,433 | extinct 9,406 | extinct 16,925 | runaway 25,998 |

Time-to-death is perfectly monotone in dose, and the window between "dies" and "runs away"
is **empty at this seed**. **T2 and T4 are falsified; the probe failed.** The reading:
irradiance scales what one creature earns, so it moves the *whole* trajectory — founding,
drought depth, recovery — together, and the world it makes at every dose is the same world
on a slower or faster clock. It cannot set how many creatures the world holds without also
setting whether any single creature can live. (Worth noting: `d054-s1` at 200 fell to 73
alive at t=16,500 before recovering — seed 1 at 200 was itself a near-death, so the cliff
between 175 and 200 is thinner than the table makes it look.)

What *would* rescale the dish is the knob D053 deferred: **`WorldAreaSquareMetres`** — the
sun's aperture, every layer's volume, and the denominator of shading at once, so halving it
halves total income and total stock at identical per-creature margins. It was a tunable
already; `EVOSIM_AREA` now reaches it, printed in the header as `· area X m2`.

### Round 5b — pre-registered before launch, same night

Round 4's world exactly (irradiance 200, 0.02, senescence 10,000, floor 3,000, ceiling
8,000, mixing 0.2), **`EVOSIM_AREA` 400 → 100 and 200**. Arms `d055b-a100-s1`,
`d055b-a100-s2`, `d055b-a200-s1`, 30,000 s, 600 min wall.

| # | prediction | falsified by |
|---|---|---|
| U1 | `floor` = 0 after t=3,100 everywhere | `floor` |
| U2 | founding works at both areas — >500 births by t=3,000 (per-creature margins are untouched) | `births` |
| U3 | at most 1 of 3 arms goes extinct | run footer |
| U4 | no a100 arm reaches the 8,000 ceiling | run footer |
| U5 | at least one arm ends at t=30,000 uncensored with mean `alive` over its last 10 samples in 500–3,000 — that area is round 6's world | footer, `alive` |
| U6 | a mutant arrives in ≥1 arm | `absorpt`, `inherit` |

**The two-sided reading, before the answer.** If U4 fails, the 400 m² equilibrium is above
32,000 and the probe continues downward at 50 m². If U3 fails by drought deaths, the
smaller dish is the same world with smaller absolute numbers — the drought that left 73
survivors at 400 m² leaves ~18 at 100 m², and demographic noise finishes what the drought
started; then the dish cannot be shrunk without first softening droughts, and round 6
(D052's excretion) runs at 400 m² with the ceiling accepted as a censor. If U2 fails, area
is not the clean rescale the code reading says it is — find what else reads 400.
