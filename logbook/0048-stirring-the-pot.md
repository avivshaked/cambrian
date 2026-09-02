# 0048 — Stirring the pot

*2026-09-02. Pre-registered before launch; results appended after. Round 12: two
treatments × five seeds, the owner's design ([D066](../DECISIONS.md) and the excretion
amendment to [D065](../DECISIONS.md)), written while round 11
([0047](0047-the-half-life.md)) is finishing and before it is scored.*

## Where round 11 left the patient

At t≈16,000–20,000, all five half-life worlds were alive at ~1,400–1,540, turnover was two
to three times the 10c control's (deaths 1,000–1,800 against 600–1,250 for a whole 10c
run), the deep larder stood at 6.7–10.9 J/m³ — over the absorptive breeding bar in four
seeds of five — and mean depth was −0.4 to −3.5 m. And no seed had produced one inherited
absorptive. The rate gate is open; the location gate is not: detritus sinks at 0.02 m/s and
clears the 24 m lit band in twenty minutes, the surface holds 0.16–0.25 J/m³ where the
mutants are born, and a body under V0 cannot sink to the 10 J/m³ fifty metres below it.

The owner's reading, which became D066: the current was supposed to move creatures on every
axis and move matter, and did neither — a depth-only field of ±2.4 m acting on bodies alone;
and a single uniform flow could not stir in any case, because stirring is *differential*
motion. "What we need is something that can stir the soup." And, on excretion: creatures do
excrete (D052), but at a rate that returns ~5–10% of a body's matter in a lifetime, so the
matter loop closes almost entirely by death and the surface lives in permanent famine —
100,000+ refused conceptions per sample, every seed, every round since 10.

## The treatments

| arm | knobs (all others: round 11's world exactly) | mechanism bet |
|---|---|---|
| **A** (`r12a-s1..5`) | `EVOSIM_PATCHES` 4 · `EVOSIM_CURRENT` 0.1 · `EVOSIM_CURRENT_PERIOD` 6000 · `EVOSIM_CURRENT_CELL` 60 · `EVOSIM_CURRENT_ROLLS` 1 · `EVOSIM_CURRENT_BLINK` 3000 · `EVOSIM_CURRENT_ADVECT` 1 | **the stirred soup**: two convection rolls over four patches, surface to floor, carrying detritus up into the light and small mutants down to the larder in the same parcel; blinking parity for chaotic advection |
| **B** (`r12b-s1..5`) | arm A + `EVOSIM_EXCRETION` **0.01** (was 0.001), fixed matter term non-excretable | **the recycler**: a living body returns its tissue matter within its lifetime; the surface stops starving; conception is no longer a lottery against a drought |

**Dose arithmetic, stated first.** The time factor is the old zero-mean incommensurate pair,
so at the default 300 s period a roll would reverse every ~150 s — the jiggle again. At
6,000 s a roll runs one way for ~3,000 s; with mean |amplitude| ≈ 0.6 × 0.1 m/s the
circuit round a 60 m × 5 m roll (perimeter ~130 m) takes ~2,200 s, so a parcel makes a full
circuit before the flow reverses. The blink at 3,000 s flips which patch rises, out of step
with the reversal, which is what makes the stirring chaotic rather than periodic. Cell depth
60 m is the full column, because the larder is on the floor and the point is to lift it.
Four patches of 25 m² each, ten founders per patch, exchange every step through the rolls —
the opposite regime to round 8's sealed 1/8 pools. Excretion 0.01: at senescence 3,000 (a
lifetime of ~6,000 s) a body returns ~100% of its tissue matter alive; the fixed 3 units stay
until death, so D065's count floor holds.

Budget 30,000 s, wall 600 min, ceiling 8,000, area 100, seeds 1–5. Controls: round 11's arms
(the same world with rolls off and excretion at 0.001).

## Validity checks

| # | check | read from |
|---|---|---|
| V1 | headers carry `current 0.1 m/s over 6000 s in 60 m cells · rolls blink 3000 s · advect on` and `patches 4`; arm B additionally `excretion 0.01 /J`; all other tokens equal round 11's | header line 3 |
| V2 | no arm replays its round-11 twin past t=0 (patches draw at t=0) | row diff vs `r11-sN.md` at t=100 |
| V3 | `floor` = 0 after t=3,100 | `floor` |
| V4 | the audit column stays at 0.0000% with advection on — conservation in the live world, not only in the suite | `audit` |
| V5 | monitor: 32-min content-growth stall rule, 90-s discriminator before any kill | monitor config |

## Predictions

Scored under D063 unchanged, recruitment clause from `lineage.jsonl`; a pass is a pass in the
discovery regime (mutation 0.005) and is labelled so.

| # | prediction | falsified by |
|---|---|---|
| S1 | **the soup is stirred**: in arm A, surface detritus (`J/m3 here`) at t=10,000–20,000 is ≥ 10× round 11's (≥ 2 J/m³), and `det patch sd` is non-zero and fluctuating — the rolls put food where the mutants are | `J/m3 here`, `det patch sd` |
| S2 | **producers survive the stirring** (the Sverdrup risk): ≥ 4 of 5 arm-A seeds reach budget alive; if the whole column's stirring puts producers in the dark half the time, `alive` collapses within the first 5,000 s and this is falsified | `**Ended:**`, `alive` |
| S3 | **the famine ends** in arm B: refused conceptions per sample at t > 10,000 fall by ≥ 5× against arm A's | `mat blk` |
| S4 | **chains arrive** (first inherited absorptive birth) in ≥ 3 of 5 seeds in at least one arm | `inherit`, lineage |
| S5 | **the round's answer**: ≥ 3 of 5 seeds pass D063 in at least one arm. Written honestly: the location analysis says A is the lever the gate needs; the drought analysis says B is what lets a mutant breed once it gets there; the pre-registration predicts **B passes and A does not**, and would count A passing alone as the more surprising result | the scoring table |

## The two-sided readings

- **B passes:** the goal is met (discovery regime, stated as such); the frontier moves to
  movement, in water that finally moves.
- **A passes, B fails:** the excretion dose broke something (check S3's direction — too
  much matter can bloom past the light); the goal is met on A and B's dose is retuned later.
- **S1 holds, S4 fails in both:** the food reaches the mutants and they still do not breed
  — the gate is inside the mutant (energy to fund a brood at 2–10 J/m³ with a body this
  small), and the next round looks at the absorptive's own economics, not the world's.
- **S2 fails:** Sverdrup wins; cell depth comes down to 30 m and the larder is lifted by a
  second, deeper roll later.
- **S1 fails:** the rolls did not reach the physics (check V1/V4 first) or the dose is too
  weak; period and speed go up.

## Launch

Ten arms, ≤ 5 concurrent: `r12a-s1..s5` first on the workers round 11 frees (worker 7 is
free at writing), `r12b-s1..s5` after, interleaved so neither arm waits on one machine
incident. Headers verified against the table before any arm is believed. Results appended
below.

---

**Mid-round dose change (2026-09-02, after the first arm, before any other launched).**
`r12a-s1` at cell depth 60 m never founded: 40 founders held at ~40 through founding, a
peak of 186 at t=5,100, 237 births in all and none after t≈7,000, extinct at t=11,203 —
against round 11's ~485 alive by t=3,000 and ~885 by 6,000. Mean depth sat at −21 to −27 m
from t=1,100 onward in a world whose photic band ends near 24 m: the full-column rolls
carry neutral founders round the whole cell, so a producer spends half its life in the
dark and never funds a brood. Shade was 5–11% and surface matter 0.05–0.8 — neither light
competition nor drought; darkness by transport. **S2 falsified in its founding form** at
the first seed, by the signature it named (collapse inside the first 5,000 s — here a
failure to ever rise). The pre-registered response fires: **cell depth 60 → 30 m** for
every remaining arm, A and B alike. Producers then spend ~20% of a circuit below the light,
inside a 3× margin; detritus deposited in the top 30 m recirculates within the lit band
(the roll's upward leg at ~0.06 m/s outruns the 0.02 m/s sink), and what escapes below 30 m
accumulates on the floor as before — the surface larder is the one this round is for. The
cell-60 arm stands as the Sverdrup measurement. Relaunched arms are named `r12a30-sN` and
`r12b30-sN`; every other knob, prediction and reading is unchanged. S2's "≥ 4 of 5" now
applies to the cell-30 arms.
