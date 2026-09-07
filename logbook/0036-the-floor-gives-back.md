# 0036 — The floor gives back

**2026-08-29**  ·  D051 · food-chain goal, acceptance experiment

This entry is written in two halves on purpose. The first half, everything above *Results*,
was written and committed **before any arm was launched**. It is the pre-registration: the
world, the arithmetic, the numbers each arm is predicted to show, the column that falsifies
each prediction, and what counts as success. The second half is what happened. If the two
halves disagree, the first one was wrong, and saying so is what this logbook is for.

## What is being tested

[D051](../DECISIONS.md#d051): detritus that reaches the sea floor now leaks back into the
water above it at a first-order rate.

The goal it serves is the one chosen for this stretch of autonomous work, *a self-sustaining
world that holds a food chain, verified rather than observed once*. The question is narrow:
whether closing the nutrient cycle lets an absorptive lineage persist by inheritance, in a
world where it otherwise dies out by t≈1,200.

## Why this world, and not the reference one

Reading every run header in `runs/` before choosing turns up one fact. **Only two mixings
have ever been run, 0 and 2 m²/s.**

At 2, the column is uniform and the floor holds 0.2–7% of detritus, so a leak from it can do
nothing. At 0, the floor holds 66–76% and nothing carries what it returns upward, so the
leak would build a two-layer pile.

The mechanism only has something to act on in between. There, sinking wins in the column and
mixing lifts what the floor gives back over a length scale of D/v metres. That regime is
unmeasured, so this experiment is also the first measurement of it. That is a confound, and
the control arm exists to carry it.

## The world

[D048](../DECISIONS.md#d048) and [D050](../DECISIONS.md#d050)'s reference world with one
change, mixing 2 → 0.2.

Every arm shares these settings. The run header is the authority and must be checked against
this table after launch, since an inherited default has burned three arms
([logbook/0027](0027-the-prize-was-smaller-than-the-entry-fee.md),
[0034](0034-the-ocean-had-no-top.md)):

| setting | value | env var | note |
|---|---|---|---|
| irradiance | 200 W/m² | `EVOSIM_IRRADIANCE` | reference |
| current | 0.05 m/s | `EVOSIM_CURRENT` | reference |
| **nutrient mixing** | **0.2 m²/s** | `EVOSIM_MIXING` | **the change** — D/v = 0.2/0.02 = 10 m |
| senescence | 3000 s | `EVOSIM_SENESCENCE` | reference |
| excess density | 0.1 kg/m³ | `EVOSIM_EXCESS_DENSITY` | reference "heavy"; half the population sinks to the bed, which is where an absorptive mutant would have to arise |
| matter per tissue J | 0.5 | `EVOSIM_MATTER_PER_TISSUE` | reference |
| founder float chance | 0.5 | `EVOSIM_FOUNDER_FLOAT` | reference |
| remineralisation | **0 (control) / 0.01 s⁻¹ (treatment)** | `EVOSIM_REMIN` | the variable; sets both the nutrient and the matter rate |
| budget | 20,000 s, 360 min wall | | |

Some defaults do not appear in the header and matter here. Nutrient sink is 0.02 m/s, and
cell-type mutation is 0.001. Layers are 1 m in a 60 m depth over an area of 400 m², so the
floor layer is 400 m³.

`MatterMixingDiffusivity` stays at 2, because `EVOSIM_MIXING` sets only the nutrient field's
diffusivity, so matter remains uniform and matter remineralisation is inert in every arm.

Six arms run: `d051-ctl-s{1,2,3}` and `d051-rem-s{1,2,3}`, seeds 1–3.

Five run at once, which is the machine's limit, and `ctl-s3` follows when a slot frees.

## The arithmetic: the margin pre-flight, done before launch

Take a steady 1-D balance above the floor, with sink speed v and diffusivity D. Sinking flux
`v·c` down equals diffusive flux `D·dc/dz` up.

So the profile is `c(z) = c₀·exp(−v·z/D)` with `D/v = 10 m`.

The water-column stock is `W = c₀·A·D/v = 4,000·c₀` J, where `A` is the world's 400 m² area.

At the floor, the leak `r·F` equals what sinks in from the bottom water, `v·A·c₀`.

The rate under test is `r`, at 0.01 s⁻¹, so `F = v·A·c₀/r = 800·c₀` J at r = 0.01.

- Treatment floor share at steady state: F/(F+W) = 800/4,800 ≈ **17%**.

- Total detritus T grew at roughly 10 J/s in `d050-heavy` with nobody eating it (52 kJ at
  t=4,800). At T = 50 kJ, `c₀ = T/4,800 ≈ 10.4 J/m³` in the bottom water layer.

  Six metres above the floor, which is the new `det deep` column at 90% of depth, that is
  `≈ 10.4·e^(−0.6) ≈ 5.7 J/m³`. At T = 100 kJ (t≈10,000) the two are about 21 and 11.4.

  Diffusive relaxation over 10 m is `L²/D = 500 s`, so the profile is quasi-steady on the
  run's timescale.

- Break-even for `AbsorptiveCell` is **4 W/m³ upkeep ÷ clearance 1 = 4 J/m³**, times
  (1 + age/3000) under senescence wear. Clearance is the water an absorptive cell strains,
  in m³ per second per m³ of its own tissue. Upkeep over clearance is therefore the food
  density at which eating breaks even.

  Margin at the bottom water at t≈5,000 is **10.4/4 ≈ 2.6**, which passes the ≥2 rule. At
  `det deep` it is 1.4 at t≈5,000 and 2.9 at t≈10,000.

- In the control, the only stock above the floor is in transit. Deaths at ~10 J/s sink through
  each 1 m layer with a 50 s residence. So a layer holds at most ~500 J ≈ 1.25 J/m³, even if
  every death happened above it. Margin < 1 everywhere except the floor layer itself.

- Arrival: births run 0.5–0.7/s in these worlds. Roughly half of births are in the bottom 10 m. At cell-type mutation 0.001, an absorptive mutant arises *where the food is* about once per 3,000–4,000 s. That is two to six per 20,000 s arm. This is the thin part of the
  prediction, and it is why the failure case is written out below.

## Predictions, and the column that falsifies each

| # | arm | prediction | falsified by |
|---|---|---|---|
| P1 | control | `% on floor` rises monotonically and passes 50% by t=5,000 | `% on floor` |
| P2 | control | `det deep` < 2 J/m³ at every sample | `det deep` |
| P3 | treatment | `% on floor` plateaus between 10% and 25% after t≈2,000 | `% on floor` |
| P4 | treatment | `det deep` ≥ 4 J/m³ by t=5,000 and ≥ 8 by t=10,000 | `det deep` |
| P5 | treatment | `absorpt` > 0 at some sample after t=3,000 in every seed (a mutant arrives) | `absorpt` |
| P6 | treatment | **success**: `inherit` ≥ 1 for ≥ 20 consecutive samples (2,000 s) with `floor` = 0 throughout the window — no spawns from the *population floor*, the rescue rule that trickles fresh random founders into any world that drops to forty creatures — in ≥ 2 of 3 seeds | `inherit`, `floor`, `gen min` read together |
| P7 | control | P6's criterion met in ≤ 1 of 3 seeds | same |

A *sample* is one row of the run report, written every 100 simulated seconds, so twenty
consecutive samples is 2,000 s. The tables quoted in this and later entries show only a
subset of those rows.

The goal is met if P6 and P7 both hold.

A share is never evidence ([logbook/0029](0029-the-floor-kept-putting-the-muscles-back.md)),
so P6 is read from `inherit` with `floor` silent.

It is not read from `absorpt`.

## The two-sided reading, written before the answer

- **P4 holds and P6 fails** (the food is there, the lineage is not): the missing ingredient
  is not recycling. Either arrival (P5 fails — no mutant ever appeared at depth, a rate
  question) or establishment (P5 holds, P6 fails — a mutant appeared where the food is and
  still did not persist, which is the spatial hypothesis [HD21] and MC25's undervaluation,
  and is the strongest result this experiment can give against the goal).
- **P4 fails** (the density never arrives): the balance arithmetic is wrong — mixing 0.2 is
  not the regime it was computed to be, or T grows slower than 10 J/s in this world. A
  model error, not a biology finding; retune and rerun before concluding anything.
- **P7 fails** (the control holds a chain too): remineralisation is not the cause, and
  mixing 0.2 alone was. Still a world that holds a chain, and still a finding — a weaker one.

**Uninterpretable, and to be reported as such:** an arm that hits the 5,000 ceiling
(`PopulationRunawayException` ends the run), a floor that keeps firing past t≈1,000
(`floor` column), or a wall budget that ends an arm before t≈10,000.

## Results

### Interim, at t≈4,000–5,300 of 20,000: P1 is already false, and the reason is in the code

Written while the arms were still running, because the finding does not depend on how they
end. The readout is `scripts/read-arm.ps1`, which scores the predictions above by rule, and
at the first look it said this:

| arm | remin | `% on floor` at t=2,000 → end | `det deep` max | first t ≥ 4 J/m³ |
|---|---|---|---|---|
| ctl-s1 | 0 | 5.3% → 5.5% (t=4,000) | 4.03 | 4,000 |
| rem-s1 | 0.01 | 5.1% → 5.2% (t=4,000) | 4.03 | 4,000 |
| ctl-s2 | 0 | 4.8% → 7.0% (t=3,800) | 9.53 | 2,300 |
| rem-s2 | 0.01 | 4.6% → 6.8% (t=3,800) | 9.57 | 2,300 |
| rem-s3 | 0.01 | 4.0% → 6.8% (t=5,300) | 7.29 | 3,800 |

P1 is falsified: the control's floor share is 5–7% and flat rather than rising past 50%. P2
is falsified with it. The control's deep water reached 4 J/m³ at seed 1 and 9.5 at seed 2,
on the same samples and to within 1% of the treatment's.

Same seed, same world, knob on or off: the floor share differs by 0.2 points and `det deep`
by 0.04 J/m³. The knob reached the arithmetic, which the unit test proves and the
non-identical numbers confirm, and the arithmetic does not matter.

Why: `Mix` already exchanges across the floor interface.

`NutrientField.Mix` runs Fick's law over every interface `layer < LayerCount − 1`, which
includes the one between the floor layer and the water above it. The code's own doc comment
says
*"this is the world's only return path for energy"*, and it was right.

At 0.2 m²/s over 1 m layers that is a 20%/s exchange of the floor's excess, and D051's leak
is 1%/s of the floor's stock.

The premise *"`Settle` pays into the floor and nothing pays out"* is true at mixing 0 and
false at any mixing above it. Mixing 0 is the world §5A.2c's 80–93% was measured in, and the
world `d050-heavy`'s 66% was measured in.

Two readers, one of them a code-reconnaissance pass that quoted `Mix`'s loop bounds, looked
at that method and did not notice that the loop reaches the floor. The reconnaissance report
said the opposite of the doc comment: it called the floor's only debit path "a creature
resident in the bottom layer".

So the pre-flight arithmetic was right about the *column*. D/v = 10 m gives a gradient, and
the deep water does cross break-even by t≈2,300–4,000 in every arm. It was wrong about the
*floor*, which the diffusion already empties.

Remineralisation as built is redundant with mixing wherever mixing is on, and where mixing
is off it would feed only the one layer above the floor. Its distinguishable regime is
`mixing ≲ 0.01 m²/s`, which no design decision has asked for.

What the arms still test: P1–P4 are settled. P5, P6 and P7 are not, and they are the goal's
question. The deep water is now above break-even in five worlds with five seeds, which is
the condition no earlier arm reached before its absorptive founders were gone.

With control and treatment indistinguishable, the six arms are six replicates of the same
world rather than a comparison. The readout's P6 count across all six is what the goal is
scored on. The pre-registered two-sided reading stands: P5 false means arrival, and P5 true
with P6 false means establishment, the spatial hypothesis.

What was wrong, in one line: a decision was built on a code fact that was checked by reading
the code's structure rather than its loop bounds. That is the same class of error as
[logbook/0019](0019-three-knobs-that-reached-nothing.md)'s knobs that reached nothing, in
the other direction, since this is a knob that reaches something something else already
does.

### Final: six arms, scored by `read-arm.ps1` against the rule above

P1–P4 were settled in the interim section above, and this section scores the remaining
P5–P7.

All six ran to t=20,000 except `rem-s2`, the largest population, whose six-hour wall budget
ended it at t≈16,600. That is past the t≥10,000 the pre-registration required. No arm hit
the 5,000 ceiling.

| arm | remin | `det deep` max | first absorptive after t=3,000 | origin | peak absorptives (inherited) | P6 window | at t=20,000 |
|---|---|---|---|---|---|---|---|
| ctl-s1 | 0 | 22.2 | t=8,500 | floor top-up during a crash to 41 | 171 (170) at t=14,500 | 26 samples, t=13,700–16,200 · **PASS** | 0 — bust by t=16,500 |
| ctl-s2 | 0 | 38.2 | t=4,600 | floor top-ups t=4,600–9,500 never bred; **a mutant** (gen min 11) present alone from t=17,500 bred at t=19,200 | 437 (436) at t=20,000 | 9 samples · FAIL (run ended) | **437 alive, rising** |
| ctl-s3 | 0 | 32.7 | t=15,800 | **mutation** — floor silent from t≈1,000 | 401 (399) at t=17,800 | 29 samples, t=17,200–20,000 · **PASS** | 4 — bust in progress |
| rem-s1 | 0.01 | 38.7 | t=9,900 | **mutation** — three separate mutants lingered as 1–4 individuals (t=9,900, 13,200, 17,100); the third bred at t=18,700 | 1,092 (1,091) at t=20,000 | 14 samples · FAIL (run ended) | **1,092 alive, rising** |
| rem-s2 | 0.01 | 37.3 | t=4,600 | floor top-ups during crashes, none bred; a mutant present as 3 individuals from t≈16,900 | 12 (1) | none · FAIL | 3 (0 inherited) at t=17,300, wall budget |
| rem-s3 | 0.01 | 35.6 | t=5,300 | **mutation** at 7 J/m³, did not breed | 16 (0) | none · FAIL | 0 |

By the rule written before launch, P6 holds in 2 of 6 arms and P7 is moot. Two of three
controls pass and no treatment does, which with the knob measured inert is the seed lottery
and nothing else. The two treatments that "failed" P6 include the arm with the largest
lineage of the round, which arrived too late for twenty samples. P5, an absorptive appearing
after t=3,000, held in all six.

By the goal's spirit there are three readings, kept separate.

1. Arrival by mutation happens, and it happens at depth. Four arms grew an inherited absorptive
   lineage. Three of the four came from a cell-type mutant born into a producer population
   with the floor silent, rather than from a founder.

   Two of those are `ctl-s2` and `ctl-s3`.

   The third is `rem-s1`.

   Five mutants were seen in all. Two of `rem-s1`'s did not breed and neither did `rem-s3`'s
   at 7 J/m³, and the ones that bred met 30–38 J/m³.

   Across ~116,000 arm-seconds that is one mutant per ~23,000 s, and roughly three in five
   of those established. The arrival rate the pre-flight guessed at, one per 3,000–4,000 s,
   was optimistic by 6×. Most births are in the lit band rather than the bottom ten metres.

2. Establishment is a boom. Every lineage that bred went from a handful to hundreds in
   1,000–1,500 s. Each drew the deep water down from 22–38 J/m³ to 3–13 in the same time.

   The two that had time to finish the arc, `ctl-s1` and `ctl-s3`, crashed to 0 and 4.

   The two still rising at t=20,000 were `ctl-s2` and `rem-s1`, which had not yet reached
   the top of theirs.

   Whether a bust ends at zero or oscillates is the open question, and 20,000 s did not
   answer it. `ctl-s1`'s deep water had rebuilt from 3 to 15 J/m³ by the end with no
   absorptive left to use it.

3. The population floor is still load-bearing for the producers. The two controls `ctl-s1` and
   `ctl-s2` fell to 40 for thousands of seconds, at t≈4,600–9,500, and the floor held them
   there.

   So did `rem-s2`. That fall is the D048 matter-starvation crash.

   A world whose producers need the safety net is not yet self-sustaining whatever its
   absorptives do, and that is the first thing the next round has to find out.

The goal, scored plainly, comes to this. *A world holds a food chain, replicated*: yes, six
seeds, four lineages, three of them the world's own mutants. *Self-sustaining*: not shown,
because the producers leaned on the floor in three arms, and no lineage has yet been watched
through a full boom–bust cycle. Round 2 ([logbook/0037](0037-the-net-comes-down.md)) closes
the floor and doubles the budget.

The knob that mattered was mixing. Nothing in this entry's mechanism did anything, and the
whole result is the world at 0.2 m²/s, which no one had run. The D/v arithmetic in the
pre-flight, a 10 m gradient above the floor, was correct. It is the reason the deep water
was worth living in for the first time.
