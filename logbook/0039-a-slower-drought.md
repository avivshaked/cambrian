# 0039 — A slower drought

**2026-08-29**  ·  food-chain goal, round 4 · pre-registered before launch

Same shape as 0036–0038: everything above *Results* was written and committed before any
arm was launched.

## The hypothesis

logbook/0038 split the trap by density. At `excessDensity` 0.05 and 0.1 a population whose
births stop sinks out of the light within a thousand seconds, and no fertility can save it.
At 0.02 it sinks slowly enough to stay lit — and died anyway in `d053-s3`, because the
drought had left it one cohort, and under D038's wear (upkeep ×(1 + age/3000), yield halved
at 3,000 s) a cohort past ~1,300 s has no surplus to breed with. The drought was shorter
than a creature's reproductive life; the cohort it left behind was not.

**The claim under test: at 0.02, a senescence scale longer than the drought lets the
post-drought cohort breed again, and the population that survives the drought survives.**
This is suggestion 2 of the three the owner set out, taken in its cheaper form (the other
form, a faster matter return, is held in reserve).

## The world

Round 3's world (mixing 0.2, `excessDensity 0.02`, floor closes at 3,000 s, remin 0, the
D048 reference settings) with two changes, one of them instrumental:

| setting | value | env var | note |
|---|---|---|---|
| **senescence** | **10,000 s** (was 3,000) | `EVOSIM_SENESCENCE` | the variable: wear reaches ×2 at 10,000 s instead of 3,000 |
| ceiling | 8,000 (was 5,000) | `EVOSIM_MAX_POP` | instrumental — three of round 3's four surviving arms were heading for the 5,000 cut, and round 3's droughts turned populations at 3,000–4,500, so 8,000 leaves room for the world to limit itself; nothing in the world reads it |
| budget | 30,000 s, 600 min wall | | |

Arms `d054-s1..s5`, launched as workers free.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| S1 | `floor` = 0 at every sample after t=3,100 | `floor` |
| S2 | at most 1 of 5 arms goes extinct | run footer |
| S3 | after every drought (`mat blk` > 1,000 in a window after t=3,000), `births` moves again within 2,000 s and mean age at that point is below 3,000 s | `mat blk`, `births`, `age s` |
| S4 | no arm reaches 8,000 — a lit population at 0.02 is limited by its droughts, not by the instrument | run footer |
| S5 | a mutant arrives (`absorpt` > 0, `inherit` = 0, after t=3,000) in ≥ 2 of 5 arms | `absorpt`, `inherit` |
| S6 | **success:** ≥ 3 of 5 arms not extinct, with `inherit` ≥ 1 for ≥ 20 consecutive samples and `absorpt` ≥ 10 at the last sample | as 0037's Q5 |
| S7 | a lineage that peaks above 100 falls below 20 and rises above 100 again | `absorpt` |

**The goal is met if S1, S2, S4 and S6 hold.**

## The two-sided reading, written before the answer

- **S2 and S4 hold, S6 fails by bust:** the producers are fixed at this density and the
  consumers still overshoot — suggestion 3 (damping on the consumer) is next, on this
  world.
- **S2 fails:** age synchrony was not the irreversible half at 0.02 either, or senescence
  10,000 reopens D038's problem (founders that never age out — read `gen min`). Then the
  matter-return form of suggestion 2 is next.
- **S4 fails:** the lit world at 0.02 is not self-limiting on the ecology's own terms, and
  the question moves to what limits producers — irradiance or shading — before any of the
  three suggestions can be scored there.
- **S5 fails with S2 holding:** a healthy lit population at 0.02 keeps its mutants away from
  the food (the bottom is empty when nobody sinks); arrival, not persistence, is the next
  question.

**Uninterpretable, and to be reported as such:** a wall budget that ends an arm before
t=15,000.

---

## Results

*(to be written when the arms finish)*
