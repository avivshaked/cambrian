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

*(to be written when the arms finish)*
