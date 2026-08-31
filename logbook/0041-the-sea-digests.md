# 0041 — The sea digests

**2026-08-31**  ·  food-chain goal, round 6 · pre-registered before launch

Same shape as 0036–0040: everything above *Results* was written and committed before any
arm was launched.

## The hypothesis

Round 5 ([logbook/0040](0040-right-sizing-the-dish.md)) closed the geometric road: no irradiance and no area gives this
world a bounded living state, because its only response to scarcity is a death spiral —
starving bodies sink out of the light and never return what they took. [D052](../DECISIONS.md#d052) built the
missing return: a living body pays matter back **at its own depth**, in proportion to
upkeep, so a lit population regenerates the surface it feeds on while it is still alive
to use it.

**The claim under test: with excretion on, droughts become shorter than a reproductive
life, and the producer crashes of 0037–0040 do not happen.** The consumer question rides
along: a steadier surface means a steadier detritus rain, which is the cheap version of
fix 3 (damping on the consumer — the third of the owner's three candidate answers,
enumerated in [logbook/0038](0038-a-lighter-world.md)) — a lineage's food may stop collapsing under it without any
damping being built.

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
| V6 | **success, the standing rule read at the last sample:** ≥3 of 5 arms not extinct, `inherit` ≥ 1 for ≥20 consecutive samples, `absorpt` ≥ 10 at the last sample | as [0037](0037-the-net-comes-down.md)'s Q5 |
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
  lever is a body that holds its depth (a [D049](../DECISIONS.md#d049) follow-up), not more matter.
- **V6 fails by bust with V2 holding (chains still collapse):** producers fixed, consumers
  still undamped — fix 3 gets its D-entry, with excretion kept.
- **V7 holds anywhere:** the strongest positive result the project has ever had.

**Uninterpretable, and to be reported as such:** an arm ended by its wall before t=15,000.

---

## Results

All five ran. No arm touched the 8,000 ceiling — the censor declared above was never
needed; the binding instrument turned out to be the **wall clock**, which cut s1 at
t=24,093 (7,057 alive) and s5 at t=22,721 (4,933 alive), both after the 15,000 s
interpretability line.

In the fate column, *budget* means the arm completed the full 30,000 simulated seconds it
was asked for — the clean ending; *wall* means the 600-minute real-time limit cut it
first, which is a censored run, not an outcome.

| arm | fate | producers | absorptive chain |
|---|---|---|---|
| s1 | wall, t=24,093 | 7,057 and climbing | arrived t≈6,000, **peak 910 at t=13,100**, bust to 5 by t=18,000, no rise |
| s2 | **budget, t=30,000, 618 alive** | crashed 1,615 → 269 into the dark (−41 m) and **came back** — then cycled 109–1,495 through recurring droughts | none arrived |
| s3 | extinct t=26,278 | died at −70 to −96 m with matter recovered above | arrived t≈10,000, **1,430 of 2,515 alive at t=16,000** — the majority of the world — ate the deep from 27 to 4 J/m³; the whole ecosystem sank and died together |
| s4 | **budget, t=30,000, 2,204 alive** | cycled 102–2,204 through six droughts, floor silent | one arrival, at the last sample |
| s5 | wall, t=22,721 | 4,933 | arrived t≈12,000, peak 320, **inherited for 80 consecutive samples (t=14,800–22,700), 50 alive at the cut** |

### Scored

| # | result |
|---|---|
| V1 | **held** — zero floor spawns after t=3,100 in all five |
| V2 | **held** — 1 of 5 extinct |
| V3 | **failed on its letter, and the letter was wrong** — every arm has `mat blk` > 1,000 for 94–256 consecutive samples, but with thousands alive that threshold is background noise, not drought — a blocked-conception count scales with how many creatures are alive to attempt conception each step, so at thousands alive a raw count sits above any fixed threshold even when no individual is short; the metric needed to be per capita. The substantive claim it aimed at is better read from V4 and the fates above |
| V4 | **failed in s3** — the shading-sink spiral still exists (a population dying at −96 m under a recovered surface, births frozen by darkness). But **s2 broke it**: 1,615 → 269 at −41 m, then recovery — the first return from the dark in the project's history. The spiral is no longer always irreversible |
| V5 | **held** — arrivals in s1, s3, s5 and (at its last sample) s4 |
| V6 | **failed, 1 of 5** — only s5 satisfies all three clauses (not extinct; inherited ≥20 consecutive samples — it managed 80; ≥10 at the last sample — it had 50). s1's and s3's chains bust before their ends; s2 and s4 never got a chain |
| V7 | **falsified, seventh time** — 910 → 5 with no rise (s1); s3's chain died with its world; s5 was cut mid-decline at 50 |

**The goal is not met** (V6 failed), and this is the best round the project has run:

1. **The first bounded, living, uncensored worlds.** s2 and s4 completed 30,000 s with the
   floor silent, populations cycling through recurring droughts between ~100 and ~2,200 —
   neither runaway nor extinction. Excretion turned the drought from a death sentence into
   a working brake: matter returns where the living are, fast enough to ride.
2. **Three chain establishments in one round** (910, 1,430, 320) against one per round in
   0036–0039 — a steadier detritus rain feeds arrivals as hypothesised.
3. **The first trophic collapse.** s3's chain became the majority of its world, stripped
   the deep, and took the producers down with it — the first extinction in which the
   consumers were structural, not bystanders.
4. What remains is exactly **fix 3's territory**: every chain that boomed, bust. Nothing
   damps a consumer but its food, and now the consumer is big enough to matter to the
   whole world.

The frontier after this round: the consumer bust (fix 3, the owner's open biology-vs-world
call), and the wall clock as the new binding instrument — a bounded world can run
30,000 s, but s1 and s5 show 600 minutes no longer covers a 5,000–7,000-creature run.
