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

### Round 2a — floor closed at 20 s: three of four extinct before t≈2,600

Four arms launched (seed 5 waited for a worker). Within an hour:

| arm | fate | what the trace says |
|---|---|---|
| s1 | **extinct at t=2,509** | 40 founders, **0 births**. Nothing in the cohort could breed; the last founder died at −12 m with 0.53 matter/m³ at the surface |
| s2 | **extinct at t=2,613** | bred to 287 by t=1,000, then surface matter fell to 0.05/m³, births stopped at 310, and the population sank and died — D048's matter crash, uncaught |
| s3 | alive, 174 at t=1,500 | still running |
| s4 | **extinct at t=1,586** | 40 founders, **2 births** |

Q1 held (the floor was silent everywhere after t=100). Q2 held, in three arms of four —
and the pre-registered reading for that case named the wrong cause. Only s2 died of the
matter crash the reading anticipated. **s1 and s4 died of founding**: forty random
generation-zero genomes, most of which cannot breed at all, and in two seeds of four none
that could. Round 1's populations sitting at 40–86 through t≈1,000–2,000 in seed 1 were
not a small viable population, they were the floor replacing founders as fast as they
died, until a lineage that could breed happened to be drawn.

So the floor has had two jobs, and only one of them was visible before today: it rescues
matter crashes (round 1), and it **runs the founding lottery** until a breeding lineage
exists. Closing it at 20 s tests whether forty random genomes are a viable population.
They are not, and that was not the goal's question — a self-sustaining world is one that
keeps itself *once it exists*. The 20-second answer stands as written: **with founding
limited to one draw, the world is not self-sustaining, three seeds in four.**

### Round 2b — pre-registered before launch, same day

**One change: the floor closes at t=3,000 s** instead of 20. Round 1's traces put the
founding phase (floor firing on a rising population) inside t<1,000 in every seed, and the
earliest matter crash at t=4,600. So 3,000 is after founding and before the first crash:
the floor may build the population, and may not rescue it. Everything else as above —
40,000 s, five seeds, arms `d052b-s1..s5`. Seed 3's 2a arm runs on as a 2a result.

Predictions Q1–Q6 as above, with Q1 read after t=3,100 and one addition:

| # | prediction | falsified by |
|---|---|---|
| Q7 | at least 2 of 5 arms fall below 100 producers after t=3,000 and recover above 300 without the floor — a matter crash survived | `alive`, `floor` |

The success line is unchanged: **Q1, Q3 and Q5**. If Q2 still holds in ≥3 arms at 3,000 s,
the finding is that the matter crash itself is lethal without the net, and the work moves
upstream to D048's economy before any food chain can be called self-sustaining.

### Round 2b — results

*(to be written when the arms finish)*
