# 0038 — A lighter world

**2026-08-29**  ·  food-chain goal, round 3 · pre-registered before launch

Same shape as 0036 and 0037: everything above *Results* was written and committed before
any arm was launched.

## The hypothesis

logbook/0037 ended with six extinctions that read identically: surface matter runs out,
births freeze, the bodies sink out of the light, and the cohort ages out while the matter
recovers above it. Starvation is the trigger; **the claim under test is that sinking is what
makes it irreversible.** At `excessDensity 0.1` (D050's "heavy" world, chosen there because
buoyancy is only decisive at that sink) an unfed body falls 1 m in about 10 s of settling
and is below the photic band within the drought. At 0.02 — D050's "slow" world, where the
same body takes five times longer — a population that stops breeding for a thousand seconds
should still be in the light when the matter comes back.

The instruction for this stretch is to work through three candidate answers in order until
one meets the goal; this is the first, and the cheapest: one environment variable.

## The world

Round 2b's world — mixing 0.2, floor closes at 3,000 s, remin 0, the D048 reference
settings — with **`EVOSIM_EXCESS_DENSITY` 0.1 → 0.02**. Five seeds, arms `d053-s1..s5`,
**30,000 s / 600 min wall** (40,000 was never reached in 2b: populations above ~2,000 run
at a fifth of real time). Headers verified after launch; the settings table is 0037's with
one row changed.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| R1 | `floor` = 0 at every sample after t=3,100 in every arm | `floor` |
| R2 | **at most 1 of 5 arms goes extinct** (2b: 3 of 5). The trap is the sink | run footer |
| R3 | in every arm that has a drought (a window with `mat blk` > 1,000 after t=3,000), mean depth stays above −20 m for the 2,000 s that follow, and births resume (the `births` column moves) within them | `mat blk`, `depth m`, `births` |
| R4 | a mutant arrives (`absorpt` > 0, `inherit` = 0, after t=3,000) in ≥ 2 of 5 arms | `absorpt`, `inherit` |
| R5 | **success:** ≥ 3 of 5 arms not extinct **and** with `inherit` ≥ 1 for ≥ 20 consecutive samples **and** `absorpt` ≥ 10 at the last sample | as in 0037's Q5 |
| R6 | at least one lineage that peaks above 100 falls below 20 and rises above 100 again | `absorpt` |

**The goal is met if R1, R2 and R5 hold.** R2 alone is the producer half; the instruction is
to keep going until both halves hold.

## The two-sided reading, written before the answer

- **R2 holds, R5 fails by bust:** the producers are fixed and the consumers are not. The
  next round stacks suggestion 3 (damping on the consumer) on this world — the lighter
  world is needed either way, so it stays.
- **R2 fails (≥ 3 extinct):** sinking is not the irreversible half, or not the only one.
  Read R3's drought traces for what killed them at 0.02, and move to suggestion 2 (a slower
  drought: senescence or matter return) on the heavy world.
- **R2 holds, R4 fails:** arrival is the bottleneck at this sink; a lighter body may keep
  the producers lit and the mutants away from the food, which is the D048 two-gradient
  world working against itself. Then the arrival rate, not the trap, is the next question.
- **R6 anywhere:** the strongest positive result available.

**Uninterpretable, and to be reported as such:** an arm ended by the 5,000 ceiling, or one
whose wall budget ends it before t=15,000.

---

## Results

*(to be written when the arms finish)*
