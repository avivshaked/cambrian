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

### Interim — the first extinction has a different signature

`d053-s3` went extinct at t=11,223, and the trace does not read like 0037's:

| t | alive | births | mat top | mat blk | depth m | mean age s |
|---|---|---|---|---|---|---|
| 4,000 | 768 | 2,178 | 0.009 | 22,854 | −16.4 | 698 |
| 4,500 | 924 | 2,564 | 0.089 | 0 | −18.1 | 716 |
| 5,000 | 815 | 2,756 | 0.285 | 0 | −19.0 | 867 |
| 5,500 | 584 | 2,830 | 0.413 | 0 | −22.6 | 1,073 |
| 6,000 | 365 | 2,867 | 0.472 | 0 | −24.3 | 1,300 |
| 7,000 | 153 | 2,885 | 0.508 | 0 | −29.4 | 1,793 |
| 9,000 | 8 | 2,885 | 0.529 | 0 | −42.8 | 3,361 |

The drought came and went as in every earlier crash. The sink did what the hypothesis
said: 2 m per 500 s instead of 10, so at t=5,000 the population was still at −19 m with
matter back at 0.285/m³ and nothing blocked. **And births did not resume.** They ran at 386
per 500 s before the drought, 74 in the window after it, 3 in the one after that, then
zero. What the table shows instead is the mean age: 700 s before the drought, 1,300 s a
thousand seconds after it, 1,800 s a thousand seconds later — the population is one cohort,
born before the gap the drought opened, ageing together. Under D038's wear (upkeep
×(1 + age/3000), yield halved at 3,000 s) a cohort at 1,300 s in dimming light at −22 m has
no surplus to breed with, and a population with no births is a cohort by definition. It
sank slowly and died of age, not of depth. R3 is falsified for this arm. The irreversible
half of the trap here was **age synchrony**, which is suggestion 2's territory — the drought
was shorter than the population's reproductive life, but the cohort it produced was not.

Noted in passing: buoyant creatures fell from 157 to 3 over the same window, ahead of the
rest — the floaters died first, not last, which D049/D050 did not predict and this entry
does not explain.

### Interim — the second ending is a runaway

`d053-s4` ended at t=10,049 with **5,001 alive: the population ceiling**, after coming
through a drought of 15,385 blocked conceptions at t=8,000 with births resumed by 9,500
(R3 held there). Seeds 1, 2 and 5 were at 2,900–4,400 and climbing when this was written.
The lighter world's other face: producers that no longer fall through the drought grow
until the instrument stops the run — "light is covering upkeep so completely that nothing
has to do anything", as the footer puts it — and the pre-registration classes that as
uninterpretable. At 0.1 the world killed its producers; at 0.02 it cannot be scored.

### Round 3b — pre-registered before launch, same day

The dose midpoint, **`excessDensity 0.05`** (D050's "mid" world), with the ceiling raised
to **8,000** through a new `EVOSIM_MAX_POP` so a generous world can be scored rather than
censored. The ceiling is an instrument limit — nothing in the world reads it — so raising it
changes where a run is cut, not what the world does; it is declared here and printed in
the header. Everything else as round 3. Arms `d053b-s1..s5`, launched two at a time as
workers free, since seeds 1, 2 and 5 of round 3 are mid-drought and answering R3.

Predictions R1–R6 as above, plus:

| # | prediction | falsified by |
|---|---|---|
| R7 | no arm reaches 8,000 (the mid world self-limits below it) | run footer |

**The goal is met if R1, R2, R5 and R7 hold.** If R7 fails too, the density that keeps the
producers alive is one that also removes the limit on them, and the next question is
what limits a lit population — shading is meant to (D023) and at 3,000 creatures it was
only 19%.
