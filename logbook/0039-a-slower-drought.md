# 0039 — A slower drought

**2026-08-29**  ·  food-chain goal, round 4 · pre-registered before launch

Same shape as 0036–0038: everything above *Results* was written and committed before any
arm was launched.

## The hypothesis

[logbook/0038](0038-a-lighter-world.md) split the trap by density. At `excessDensity` 0.05 and 0.1 a population whose
births stop sinks out of the light within a thousand seconds, and no fertility can save it.
At 0.02 it sinks slowly enough to stay lit — and died anyway in `d053-s3`, because the
drought had left it one cohort, and under [D038](../DECISIONS.md#d038)'s wear (upkeep ×(1 + age/3000), yield halved
at 3,000 s) a cohort past ~1,300 s has no surplus to breed with. The drought was shorter
than a creature's reproductive life; the cohort it left behind was not.

**The claim under test: at 0.02, a senescence scale longer than the drought lets the
post-drought cohort breed again, and the population that survives the drought survives.**
This is suggestion 2 of the three the owner set out, taken in its cheaper form (the other
form, a faster matter return, is held in reserve).

## The world

Round 3's world (mixing 0.2, `excessDensity 0.02`, floor closes at 3,000 s, remin 0, the
[D048](../DECISIONS.md#d048) reference settings) with two changes, one of them instrumental:

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
| S6 | **success:** ≥ 3 of 5 arms not extinct, with `inherit` ≥ 1 for ≥ 20 consecutive samples and `absorpt` ≥ 10 at the last sample | as [0037](0037-the-net-comes-down.md)'s Q5 |
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

**Two of five seeds ran.** Seeds 3–5 were not launched: the owner's instruction on the
evening of the 29th was to let the running arms finish and start nothing new, and by then
[D052](../DECISIONS.md#d052) (the excretion contract) had been decided as the next step regardless of this round's
outcome. So the scores below are on n=2 and the round is *open*, not scored — HANDOFF.md
carries the launch command for the other three.

### d054-s1 — survived a drought, survived a sink, ran away

| t | alive | births | mat top | mat blk | depth m | mean age s | absorpt |
|---|---|---|---|---|---|---|---|
| 2,000 | 853 | 947 | 0.005 | 25,301 | −14.3 | 579 | 0 |
| 4,000 | 1,862 | 3,242 | 0.005 | 36,682 | −18.4 | 855 | 0 |
| 10,000 | 1,632 | 9,740 | 0.008 | 31,842 | −21.8 | 1,078 | 0 |
| 12,000 | 1,685 | 11,732 | 0.262 | 0 | −25.9 | 1,299 | 0 |
| 14,000 | 310 | 11,908 | | 0 | −36.7 | 2,565 | 0 |
| 16,000 | 73 | 11,945 | | 0 | −39.0 | 3,329 | 0 |
| 17,000 | 73 | 11,990 | | | −20.1 | 1,671 | 0 |
| 18,000 | 294 | 12,239 | | | −12.6 | 622 | 0 |
| 20,000 | 2,343 | | | | | 723 | 0 |
| 25,500 | 6,497 | 25,757 | 0.001 | 134,157 | −20.3 | 1,273 | 41 |
| 25,900 | 7,599 | 27,410 | 0.001 | 161,854 | −22.4 | 1,276 | **348** (346 inherited) |
| 25,998 | **8,004 — RUNAWAY** | | | | | | |

(A blank cell is a value not transcribed from the run report, not a zero and not missing
data — the report records every column at every sample; the table copies only what the
argument reads.)

Three things happened here that no earlier arm showed. First, the population **bred straight
through an eight-thousand-second drought** (t≈2,000–10,000, surface matter at 0.005–0.008/m³
and 25,000–69,000 conceptions refused per window) — births rose the whole time and mean age
stayed near 1,000 s. That is the hypothesis working: at senescence 10,000 the cohort a
drought leaves behind is still fertile. Second, the population then **crashed with matter
available and nothing blocked** — 1,685 to 73 between t=12,000 and 16,000 — and the depth
column says why: eight thousand seconds of slow sinking had carried the mean to −26 m, the
edge of the photic band, and from there it fell to −40 m. At 0.02 the sink is not fast enough
to close the trap inside a drought, but a long enough drought sinks the population anyway.
Third — and this is the round's finding — **it came back without the floor.** The 73
survivors were the shallow ones (buoyant count rose 5 → 44 while the rest died), mean depth
jumped to −20 and then −13 m, mean age fell to 622 s, and the population went from 73 to
2,343 in 4,000 s. `floor` was 0 throughout. Every one of round 2b's and round 3's extinctions
passed through a state like t=16,000 here and none recovered; this one did, because the
survivors could still breed at 3,300 s of age.

Then the runaway: the recovered population hit the 8,000 ceiling at t=25,998 — growing by
1,650 births per 400 s with the top layer at 0.001/m³ and 134,000–162,000 refusals per window,
so the matter it bred on was below the top layer — carrying a mutant chain that had gone
from 41 to 348 in the last 400 s. Censored two samples into its boom.

### d054-s2 — never fell below 350; the consumer bust, slowly

| t | alive | births | mat top | depth m | mean age s | absorpt (inherited) | det deep J/m³ |
|---|---|---|---|---|---|---|---|
| 7,000 | 389 | 2,886 | 0.262 | −40.5 | 3,391 | 1 (0) | 13.8 |
| 13,000 | 388 | 3,687 | 0.161 | −33.9 | 1,930 | 43 (42) | 18.0 |
| 17,000 | 1,517 | 5,268 | 0.012 | −51.1 | 1,560 | **549** (548) | 4.4 |
| 22,500 | 3,788 | 10,084 | 0.004 | −26.5 | 2,062 | 145 (143) | 3.9 |
| 25,000 | 4,404 | | 0.003 | −27.9 | 2,600 | 108 (107) | 5.1 |
| 27,000 | 5,065 | | 0.001 | −22.4 | 2,575 | 12 (12) | 6.6 |
| 29,000 | 6,436 | | 0.000 | −20.1 | 2,628 | 3 (2) | 8.6 |
| 30,000 | budget | | | | | | |

Seed 2's crash was the long-low kind: the population sat at 350–440 from t≈6,000 to 13,000
with mean age at 3,391 s at t=7,000 — a cohort well past the age that killed `d053-s3`, and
at a mean depth of −40 m, which is below the light — **and kept breeding**, 800 births across
the low phase, so that by t=13,000 the mean age was back to 1,930 s. Under wear
×(1 + age/3000) that cohort would have had nothing; under ×(1 + age/10000) it had enough.
(A mean depth of −40 m with births continuing says the population was two-part, a shallow
fertile few and a deep sinking many; the mean hides that, and the trace has no depth
histogram to show it.) A mutant arrived at t≈7,000 in the low phase, established by 13,000,
and ran the fullest consumer arc the project has: 43 to 549 in 4,000 s, deep water 18 → 4.4
J/m³, then a decline of **12,000 s** — 145 at 22,500, a plateau near 110 for two thousand
seconds, 38 at 26,000, 3 at 29,000 — with the water rebuilding beneath it (3.9 → 8.6, twice
break-even) only as the lineage died. No upturn at any point. Meanwhile the producers, lit
and no longer dying of age, climbed from 1,517 to 6,436 between t=17,000 and 29,000 with the
top layer at 0.000–0.012/m³ — a runaway in progress that the budget ended before the ceiling
could.

### Scored against the pre-registration (n=2)

| # | result |
|---|---|
| S1 | **held** — `floor` 0 after t=3,100 in both |
| S2 | **held so far** — 0 of 2 extinct. Both seeds passed through states that were terminal in every earlier round (s1: 73 alive at −39 m, mean age 3,300; s2: 350 alive at mean age 3,400) and both recovered |
| S3 | **held in substance, failed on its age clause** — births moved after every drought; in s1 at mean age ~1,000–1,300, in s2 at 3,391, above the 3,000 the prediction named. The clause was written for a 3,000-s wear scale and the arm ran at 10,000; the point — that the post-drought cohort still breeds — is what held |
| S4 | **failed** — s1 reached 8,000 at t=25,998; s2 was at ~6,500 and rising at the budget |
| S5 | **held** — mutant arrived in both |
| S6 | **failed** — s1 censored with 348 alive (2 samples into the boom); s2 at 3 by t=29,000 |
| S7 | **falsified, sixth time** — 549 → 3 with no rise; the plateau at ~110 (t≈22,500–25,000) is the closest any lineage has come to holding, and it did not |

**What round 4 established, on two seeds.** The claim under test holds: with senescence at
10,000 s the cohort a drought leaves behind can breed, and producer populations that would
have died in every earlier round came back — once from 73 individuals, without the floor.
Age synchrony was the irreversible half at 0.02, and it is removable. What it uncovers is the
S4 branch of the two-sided reading, exactly as written: **a lit population at 0.02 that no
longer dies of age is not limited by anything** — both seeds ran away, one to the ceiling and
one to the budget, with the surface stripped to zero and nothing in the world able to stop
them; shading at 24% (s1, t=12,000) did not. And the consumer half is unchanged: the fullest
bust yet, slower than `d053b-s4`'s (12,000 s against 5,500 — the same senescence that saves
producers also lets a starving consumer linger) but the same shape. The pre-registration's
own next step for this branch is the limit on producers; the owner's decided next step is
D052, which changes what the surface drought *is* — under an excretion contract a lit
population regenerates its own surface, so the drought, the sink through it, and the runaway
that follows a population that has nothing to lose to it are all a different question. Seeds
3–5 of this round remain worth running as a baseline for that.
