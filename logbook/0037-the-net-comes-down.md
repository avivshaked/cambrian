# 0037 — The net comes down

**2026-08-29**  ·  food-chain goal, round 2 · pre-registered before launch

Same shape as logbook/0036: everything above *Results* was written and committed before
any arm was launched.

## What round 1 left open

logbook/0036 ended with a world that grows a food chain — six seeds, four inherited
absorptive lineages, three of them from the world's own mutants — and two things it could
not claim:

1. **Self-sustaining producers.** In three of six arms the producer population fell to
   exactly `MinimumPopulation` (40) during D048's matter-starvation crash and sat there for
   thousands of seconds while the floor fed in founders. A world held up by its safety net
   is not sustaining itself, whatever else it does.
2. **A lineage that survives its own boom.** Every absorptive lineage that bred went from a
   handful to hundreds in ~1,500 s and ate the deep water from 22–38 J/m³ down to 3–13. Two
   busted to 0 and 4; two were still rising when the budget ended. Nobody has watched a
   lineage through the far side of a boom.

Round 2 removes the net and doubles the clock, and asks both questions of the same arms.

## The world

Round 1's world, unchanged — mixing 0.2, the D048+D050 reference settings, `remin 0` (the
knob 0036 measured inert stays off) — plus one thing and one budget:

| setting | value | env var | note |
|---|---|---|---|
| **floor closes** | **20 s** | `EVOSIM_FLOOR_CLOSES` | founding takes 2 spawns × 20 steps × 0.5 s = 10 s; anything spawned between 10 and 20 s is a generation-0 replacement indistinguishable from a founder. After that the floor never fires: a crash to zero ends the run as *extinct* |
| budget | **40,000 s**, 480 min wall | | round 1 hit its 360 min wall on the largest population at t≈16,600; the arrival rate measured there (one mutant per ~23,000 arm-seconds) needs the longer clock |
| seeds | **1–5**, five arms at once | `EVOSIM_SEED` | the machine's limit; round 1's three seeds were too few to put a rate on anything |
| irradiance 200 · current 0.05 · mixing 0.2 · senescence 3000 · excess density 0.1 · matter 0.5 · founder float 0.5 · remin 0 | | | as round 1, verified from the header after launch |

Arms: `d052-s1` … `d052-s5`. (Not a D-number — no decision is being made; the name is the
round's.)

## The arithmetic, such as it is

Round 1's rate: five mutants seen in ~116,000 arm-seconds, three of which bred, all three
into water at 30–38 J/m³. Per 40,000 s arm: expected arrivals ≈ 1.7, so P(at least one)
≈ 0.8 if arrivals are Poisson, and roughly 0.5 that one establishes. **Across five arms
the expected number that grow a lineage is about 2.5**, so the success line below is set
where the evidence would actually be surprising, not where it is likely.

The producer question has no rate to draw on: three of six round-1 arms crashed to the
floor. With the floor closed, the honest expectation is that **some arms go extinct**, and
the number that do is the measurement.

## Predictions, and the column that falsifies each

| # | prediction | falsified by |
|---|---|---|
| Q1 | `floor` is 0 at every sample after t=100 in every arm (the knob reached the floor) | `floor` |
| Q2 | at least one arm goes extinct — the run ends with "extinct at t=…" | the run footer, `alive` |
| Q3 | at least one arm holds ≥ 200 producers at t=40,000 with `gen min` ≥ 5 (a self-sustaining producer world exists) | `alive`, `gen min` |
| Q4 | at least 3 of 5 arms show an absorptive with `inherit` = 0 after t=3,000 (a mutant arrives) | `absorpt`, `inherit` |
| Q5 | **success:** in ≥ 3 of 5 arms, `inherit` ≥ 1 for ≥ 20 consecutive samples **and** `absorpt` ≥ 10 at the last sample **and** the arm did not go extinct | `inherit`, `absorpt`, run footer |
| Q6 | in at least one arm a lineage that peaked above 100 falls below 20 and rises above 100 again — a bust that was not an extinction | `absorpt` |

**The goal is met outright if Q1, Q3 and Q5 hold.** Q5 without Q3 is "chains in a world
that cannot keep its plants"; Q3 without Q5 is "plants that keep themselves and nothing
arrives or nothing lasts" — each is a finding, neither is the goal.

## The two-sided reading, written before the answer

- **Q2 true in ≥ 3 arms:** the world is not self-sustaining, and the reason is upstream of
  any food chain — D048's matter crash, which the floor has been masking since it was
  built. That would redirect the work to the producers' economy, not to the absorptives.
- **Q4 true, Q5 false:** arrivals happen and lineages bust to zero; the deep water is a
  boom resource on this timescale. The next question would be why the bust overshoots —
  senescence wear, brood size, or the absence of anything that eats absorptives.
- **Q4 false:** the round-1 arrival rate was luck; longer runs or a measured cell-type
  mutation rate are needed before establishment can be studied at all.
- **Q6 true anywhere:** the strongest positive result available — a lineage that recovered
  is a lineage the world can keep.

**Uninterpretable, and to be reported as such:** an arm ended by the 5,000 ceiling, or a
wall budget that ends an arm before t=20,000.

---

## Results

*(to be written when the arms finish)*
