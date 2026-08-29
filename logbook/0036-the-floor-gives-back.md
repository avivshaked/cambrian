# 0036 — The floor gives back

**2026-08-29**  ·  D051 · food-chain goal, acceptance experiment

This entry is written in two halves on purpose. The first half — everything above
*Results* — was written and committed **before any arm was launched**, and is the
pre-registration: the world, the arithmetic, the numbers each arm is predicted to show, the
column that falsifies each prediction, and what counts as success. The second half is what
happened. If the two halves disagree, the first one was wrong, and that is the record.

## What is being tested

[D051](../DECISIONS.md#d051): detritus that reaches the sea floor now leaks back into the
water above it at a first-order rate. The goal it serves is the one chosen for this stretch of
autonomous work — *a self-sustaining world that holds a food chain, verified rather than
observed once* — and the question is narrow: **does closing the nutrient cycle let an
absorptive lineage persist by inheritance, in a world where it otherwise dies out by
t≈1,200?**

## Why this world, and not the reference one

Reading every run header in `runs/` before choosing: **only two mixings have ever been run,
0 and 2 m²/s.** At 2, the column is uniform and the floor holds 0.2–7% of detritus — a leak
from it can do nothing. At 0, the floor holds 66–76% and nothing carries what it returns
upward — the leak would build a two-layer pile. The mechanism only has something to act on
in between, where sinking wins in the column and mixing lifts what the floor gives back over
a length scale of D/v metres. That regime is unmeasured, so this experiment is also the first
measurement of it — which is a confound, and the control arm exists to carry it.

## The world

D048+D050's reference world with one change, mixing 2 → 0.2. Every arm shares these; the run
header is the authority and must be checked against this table after launch (logbook/0027,
0034 — an inherited default has burned three arms):

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

**Defaults that do not appear in the header and matter here:** nutrient sink 0.02 m/s;
`MatterMixingDiffusivity` stays at 2 — `EVOSIM_MIXING` sets only the nutrient field's
diffusivity, so matter remains uniform and matter remineralisation is inert in every arm;
layer 1 m, depth 60 m, area 400 m², so the floor layer is 400 m³; cell-type mutation 0.001.

Six arms: `d051-ctl-s{1,2,3}` and `d051-rem-s{1,2,3}`, seeds 1–3. Five run at once (the
machine's limit), `ctl-s3` follows when a slot frees.

## The arithmetic — the margin pre-flight, done before launch

Steady 1-D balance above the floor with sink speed v and diffusivity D: sinking flux `v·c`
down equals diffusive flux `D·dc/dz` up, so `c(z) = c₀·exp(−v·z/D)` with `D/v = 10 m`.
Water-column stock `W = c₀·A·D/v = 4,000·c₀` J. At the floor, the leak `r·F` equals what
sinks in from the bottom water, `v·A·c₀`, so `F = v·A·c₀/r = 800·c₀` J at r = 0.01.

- **Treatment floor share at steady state: F/(F+W) = 800/4,800 ≈ 17%.**
- Total detritus T grew at roughly 10 J/s in `d050-heavy` with nobody eating it (52 kJ at
  t=4,800). At T = 50 kJ, `c₀ = T/4,800 ≈ 10.4 J/m³` in the bottom water layer; 6 m above
  the floor (the new `det deep` column, 90% of depth) `≈ 10.4·e^(−0.6) ≈ 5.7 J/m³`; at
  T = 100 kJ (t≈10,000) about 21 and 11.4 respectively. Diffusive relaxation over 10 m is
  `L²/D = 500 s`, so the profile is quasi-steady on the run's timescale.
- **Break-even for `AbsorptiveCell`: 4 W/m³ upkeep ÷ clearance 1 = 4 J/m³**, ×(1 + age/3000)
  under senescence wear. **Margin at the bottom water at t≈5,000: 10.4/4 ≈ 2.6** — passes the
  ≥2 rule. At `det deep` it is 1.4 at t≈5,000 and 2.9 at t≈10,000.
- **Control:** the only stock above the floor is in transit. Deaths at ~10 J/s sink through
  each 1 m layer with a 50 s residence, so a layer holds at most ~500 J ≈ 1.25 J/m³ even if
  every death happened above it. Margin < 1 everywhere except the floor layer itself.
- **Arrival:** births run 0.5–0.7/s in these worlds; at cell-type mutation 0.001 and roughly
  half of births in the bottom 10 m, an absorptive mutant arises *where the food is* about
  once per 3,000–4,000 s — two to six per 20,000 s arm. This is the thin part of the
  prediction, and it is why the failure case is written out below.

## Predictions, and the column that falsifies each

| # | arm | prediction | falsified by |
|---|---|---|---|
| P1 | control | `% on floor` rises monotonically and passes 50% by t=5,000 | `% on floor` |
| P2 | control | `det deep` < 2 J/m³ at every sample | `det deep` |
| P3 | treatment | `% on floor` plateaus between 10% and 25% after t≈2,000 | `% on floor` |
| P4 | treatment | `det deep` ≥ 4 J/m³ by t=5,000 and ≥ 8 by t=10,000 | `det deep` |
| P5 | treatment | `absorpt` > 0 at some sample after t=3,000 in every seed (a mutant arrives) | `absorpt` |
| P6 | treatment | **success**: `inherit` ≥ 1 for ≥ 20 consecutive samples (2,000 s) with `floor` = 0 throughout the window, in ≥ 2 of 3 seeds | `inherit`, `floor`, `gen min` read together |
| P7 | control | P6's criterion met in ≤ 1 of 3 seeds | same |

**The goal is met if P6 and P7 both hold.** A share is never evidence (logbook/0029): P6 is
read from `inherit` with `floor` silent, not from `absorpt`.

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

---

## Results

### Interim, at t≈4,000–5,300 of 20,000 — P1 is already false, and the reason is in the code

Written while the arms were still running, because the finding does not depend on how they
end. The readout (`scripts/read-arm.ps1`, which scores the predictions above by rule) at the
first look:

| arm | remin | `% on floor` at t=2,000 → end | `det deep` max | first t ≥ 4 J/m³ |
|---|---|---|---|---|
| ctl-s1 | 0 | 5.3% → 5.5% (t=4,000) | 4.03 | 4,000 |
| rem-s1 | 0.01 | 5.1% → 5.2% (t=4,000) | 4.03 | 4,000 |
| ctl-s2 | 0 | 4.8% → 7.0% (t=3,800) | 9.53 | 2,300 |
| rem-s2 | 0.01 | 4.6% → 6.8% (t=3,800) | 9.57 | 2,300 |
| rem-s3 | 0.01 | 4.0% → 6.8% (t=5,300) | 7.29 | 3,800 |

**P1 is falsified** — the control's floor share is 5–7% and flat, not rising past 50% — and
**P2 is falsified with it**: the control's deep water reached 4 J/m³ at seed 1 and 9.5 at
seed 2, on the same samples and to within 1% of the treatment's. Same seed, same world,
knob on or off: the floor share differs by 0.2 points and `det deep` by 0.04 J/m³. The knob
reached the arithmetic (the unit test proves it, and the numbers are not identical) and the
arithmetic does not matter.

**Why: `Mix` already exchanges across the floor interface.** `NutrientField.Mix` runs Fick's
law over every interface `layer < LayerCount − 1`, which includes the one between the floor
layer and the water above it — the code's own doc comment says *"this is the world's only
return path for energy"* and it was right. At 0.2 m²/s over 1 m layers that is a 20%/s
exchange of the floor's excess; D051's leak is 1%/s of the floor's stock. The premise
*"`Settle` pays into the floor and nothing pays out"* is true at mixing exactly 0 — the world
§5A.2c's 80–93% was measured in, and the world `d050-heavy`'s 66% was measured in — and false
at any mixing above it. Two readers, one of them a code-reconnaissance pass that quoted
`Mix`'s loop bounds, looked at that method and did not notice that the loop reaches the
floor. The reconnaissance report actually said the opposite of the doc comment: it called the
floor's only debit path "a creature resident in the bottom layer".

So the pre-flight arithmetic was right about the *column* — D/v = 10 m gives a gradient, and
the deep water does cross break-even by t≈2,300–4,000 in every arm — and wrong about the
*floor*, which the diffusion already empties. Remineralisation as built is redundant with
mixing wherever mixing is on, and where mixing is off it would feed only the one layer above
the floor. Its distinguishable regime is `mixing ≲ 0.01 m²/s`, which no design decision has
asked for.

**What the arms still test.** P1–P4 are settled. P5, P6 and P7 are not, and they are the
goal's question: the deep water is now above break-even in five worlds with five seeds,
which is the condition no earlier arm reached before its absorptive founders were gone. With
control and treatment indistinguishable, the six arms are six replicates of the same world
rather than a comparison, and the readout's P6 count across all six is what the goal is
scored on. The pre-registered two-sided reading stands: P5 false means arrival, P5 true and
P6 false means establishment — the spatial hypothesis.

**What was wrong, in one line:** a decision was built on a code fact that was checked by
reading the code's structure and not its loop bounds — the same class of error as
logbook/0019's knobs that reached nothing, in the other direction: a knob that reaches
something something else already does.

### Final — six arms, scored by `read-arm.ps1` against the rule above

All six ran to t=20,000 except `rem-s2`, the largest population, which its six-hour wall
budget ended at t≈16,600 (past the t≥10,000 the pre-registration required). No arm hit the
5,000 ceiling.

| arm | remin | `det deep` max | first absorptive after t=3,000 | origin | peak absorptives (inherited) | P6 window | at t=20,000 |
|---|---|---|---|---|---|---|---|
| ctl-s1 | 0 | 22.2 | t=8,500 | floor top-up during a crash to 41 | 171 (170) at t=14,500 | 26 samples, t=13,700–16,200 · **PASS** | 0 — bust by t=16,500 |
| ctl-s2 | 0 | 38.2 | t=4,600 | floor top-ups t=4,600–9,500 never bred; **a mutant** (gen min 11) present alone from t=17,500 bred at t=19,200 | 437 (436) at t=20,000 | 9 samples · FAIL (run ended) | **437 alive, rising** |
| ctl-s3 | 0 | 32.7 | t=15,800 | **mutation** — floor silent from t≈1,000 | 401 (399) at t=17,800 | 29 samples, t=17,200–20,000 · **PASS** | 4 — bust in progress |
| rem-s1 | 0.01 | 38.7 | t=9,900 | **mutation** — three separate mutants lingered as 1–4 individuals (t=9,900, 13,200, 17,100); the third bred at t=18,700 | 1,092 (1,091) at t=20,000 | 14 samples · FAIL (run ended) | **1,092 alive, rising** |
| rem-s2 | 0.01 | 37.3 | t=4,600 | floor top-ups during crashes, none bred; a mutant present as 3 individuals from t≈16,900 | 12 (1) | none · FAIL | 3 (0 inherited) at t=17,300, wall budget |
| rem-s3 | 0.01 | 35.6 | t=5,300 | **mutation** at 7 J/m³, did not breed | 16 (0) | none · FAIL | 0 |

**By the rule written before launch: P6 holds in 2 of 6 arms, P7 is moot.** Two of three
controls pass and no treatment does, which with the knob measured inert (above) is the seed
lottery and nothing else — the two treatments that "failed" P6 include the arm with the
largest lineage of the round, which simply arrived too late for twenty samples. P5 (an
absorptive appears after t=3,000) held in all six.

**By the goal's spirit, three readings, kept separate:**

1. **Arrival by mutation happens, and it happens at depth.** Four arms grew an inherited
   absorptive lineage; three of the four (`ctl-s2`, `ctl-s3`, `rem-s1`) came from a
   cell-type mutant born into a producer population with the floor silent, not from a
   founder. Five mutants were seen in all (`rem-s3`'s at 7 J/m³ and two of `rem-s1`'s did not
   breed; the ones that bred met 30–38 J/m³). Across ~116,000 arm-seconds that is one mutant
   per ~23,000 s, and roughly three in five of those established — the arrival rate the
   pre-flight guessed at (one per 3,000–4,000 s) was optimistic by 6×, because most births
   are in the lit band, not the bottom ten metres.
2. **Establishment is a boom.** Every lineage that bred went from a handful to hundreds in
   1,000–1,500 s and drew the deep water down from 22–38 J/m³ to 3–13 in the same time. The
   two that had time to finish the arc (`ctl-s1`, `ctl-s3`) crashed to 0 and 4; the two
   still rising at t=20,000 (`ctl-s2`, `rem-s1`) had not yet reached the top of theirs.
   **Whether a bust ends at zero or oscillates is the open question**, and 20,000 s did not
   answer it: `ctl-s1`'s deep water had rebuilt from 3 to 15 J/m³ by the end with no
   absorptive left to use it.
3. **The population floor is still load-bearing for the producers.** `ctl-s1`, `ctl-s2` and
   `rem-s2` all fell to exactly 40 for thousands of seconds (t≈4,600–9,500) — the D048
   matter-starvation crash — and the floor held them there. A world whose producers need
   the safety net is not yet self-sustaining whatever its absorptives do, and that is the
   first thing the next round has to find out.

**The goal, scored honestly:** *a world holds a food chain, replicated* — yes, six seeds,
four lineages, three of them the world's own mutants. *Self-sustaining* — not shown: the
producers leaned on the floor in three arms, and no lineage has yet been watched through a
full boom–bust cycle. Round 2 (logbook/0037) closes the floor and doubles the budget.

**The knob that mattered was mixing.** Nothing in this entry's mechanism did anything; the
whole result is the world at 0.2 m²/s, which no one had run. The D/v arithmetic in the
pre-flight — a 10 m gradient above the floor — was correct, and it is the reason the deep
water was worth living in for the first time.
