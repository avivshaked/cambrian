# 0041 — The sea digests

**2026-08-31**  ·  food-chain goal, round 6 · pre-registered before launch

Same shape as 0036–0040: everything above *Results* was written and committed before any
arm was launched.

## The hypothesis

Round 5 (logbook/0040) closed the geometric road: no irradiance and no area gives this
world a bounded living state, because its only response to scarcity is a death spiral —
starving bodies sink out of the light and never return what they took. D052 built the
missing return: a living body pays matter back **at its own depth**, in proportion to
upkeep, so a lit population regenerates the surface it feeds on while it is still alive
to use it.

**The claim under test: with excretion on, droughts become shorter than a reproductive
life, and the producer crashes of 0037–0040 do not happen.** The consumer question rides
along: a steadier surface means a steadier detritus rain, which is the cheap version of
fix 3 — a lineage's food may stop collapsing under it without any damping being built.

## The dose, derived honestly

`ExcretionPerJoule` has never run. The value is picked by arithmetic on estimated
mid-round numbers, stated here so a wrong estimate is visible later: a producer at
equilibrium holds ~2.5 matter locked (0.5/J × ~5 J tissue) and pays ~5 W upkeep, so a rate
k drains a body in ~0.5/k seconds; death flux at a ~3,000 s effective lifetime is
~0.0008 matter/s against excretion flux 5k. **k = 0.001** gives a body-matter turnover of
~500 s and a regenerated:death return ratio of ~6:1 — the microbial-loop regime, where
most of what producers take comes back in place within a fraction of a lifetime. The
estimates are ±3×; the two-sided reading covers both misses.

## The world

Round 4's exactly (irradiance 200, area 400, mixing 0.2, excessDensity 0.02, senescence
10,000, floor closes 3,000, remin 0, ceiling 8,000) **plus `EVOSIM_EXCRETION` 0.001**.
Five seeds, arms `d056-s1..s5`, 30,000 s, 600 min wall, all five workers.

**Declared before launch: the ceiling is accepted as a censor.** Round 4 showed this world
runs away at 200 W/m² and round 5 showed no honest way to stop that from outside the
ecology. A run the ceiling ends is scored **at its last sample** — producers that persist
to a censoring count as persisting, and the chain criterion is read at the last sample
whether the run ended by budget or by ceiling. This weakens the success rule and is said
plainly here rather than discovered later.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| V1 | `floor` = 0 at every sample after t=3,100 | `floor` |
| V2 | at most 1 of 5 arms goes extinct (round 4 baseline: 0 of 2) | run footer |
| V3 | droughts shorten: no arm has `mat blk` > 1,000 for more than 8 consecutive samples (4,000 s); `d054-s1` carried one of ~16 | `mat blk` |
| V4 | the crash signature disappears: no arm loses >80% of `alive` in 4,000 s while `mat top` > 0.1 with births frozen (0040's shading-sink spiral) | `alive`, `births`, `mat top` |
| V5 | a mutant arrives in ≥3 of 5 arms (round-1 rate at full size) | `absorpt`, `inherit` |
| V6 | **success, the standing rule read at the last sample:** ≥3 of 5 arms not extinct, `inherit` ≥ 1 for ≥20 consecutive samples, `absorpt` ≥ 10 at the last sample | as 0037's Q5 |
| V7 | a lineage that peaks above 100 falls below 20 and rises above 100 again — the seventh attempt | `absorpt` |

**The goal is met if V1, V2 and V6 hold** — with the censor caveat above attached to any
claim made from it.

## The two-sided reading, written before the answer

- **V3 fails with nothing else changed:** the dose is too low to matter — the next round is
  k×10, not a redesign.
- **All five censor before t≈10,000:** the dose removed the last brake and the world is
  pure runaway — too much regeneration. Halve toward 0.0005, and note that the producers'
  limit question (which shading cannot answer — 0040) has become unavoidable.
- **V2 fails:** excretion did not soften what kills producers, which would mean the crashes
  were never matter-first — the energy/darkness spiral of 0040 is primary, and the next
  lever is a body that holds its depth (a D049 follow-up), not more matter.
- **V6 fails by bust with V2 holding (chains still collapse):** producers fixed, consumers
  still undamped — fix 3 gets its D-entry, with excretion kept.
- **V7 holds anywhere:** the strongest positive result the project has ever had.

**Uninterpretable, and to be reported as such:** an arm ended by its wall before t=15,000.

---

## Results

*(to be written when the arms finish)*
