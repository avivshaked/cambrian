# Proposal: the stomach's flux, not its gearing

*Fable, 2026-09-03 late. For the owner's ruling after round 14. Absorbed into DECISIONS.md
on ruling, then deleted. Everything below is read from round 14's four arms, the two
assay arms (0051) and the ledger calculator; where a number is an estimate it says so.*

## The finding round 14 is converging on

Clearance 10 grows the first real absorptive lines of the campaign — 22 members in seed 1,
48 in seed 2 — and both graze their field below break-even and die back to a handful,
with no second wave inside the budget. Clearance 5 grows a line of 3 to 9 that neither
grows nor dies for 14,000 s. The two-sided readings pre-registered in 0050 did not have
this case: the line forms *and* the world cannot hold it at ten.

The reason is visible in one column. `detritus J`, the whole world's standing detritus,
in `r14c10-s1`:

| t | inherited line | detritus J | what is happening |
|---|---|---|---|
| 5,000 | 1 | 9,744 | the mutant appears; the stock has been accumulating since founding |
| 7,000 | 21 | 7,639 | the line eats the stock |
| 14,000 | 2 | 4,215 | the stock is spent; the line starves |
| 30,000 | 0 | 7,207 | no grazer: the stock rebuilds at **0.19 W** net, and no second line forms |

A line of twenty spent 5,500 J of stored capital in 9,000 s. The world's income into that
capital, with no grazer on it, is about 0.2 W. The ledger says a clearance-10 stomach of
the mutant's size needs about 0.03 W to hold R0 = 1 (0.02 W net toward a 129 J child in a
lifetime, plus 0.009 W upkeep); so **the flux sustains a standing line of roughly six**,
and the stock sustained a boom of forty-eight. The goal's alive clause asks for ten.
Clearance 5's persistent line of 3–9 is the same capacity read from the other dose: a
stomach's wattage need at R0 = 1 does not change with the gearing, only the density at
which it is met. The gearing set how fast the capital was eaten; it never touched the
income.

Where the 0.2 W comes from: deaths. `EVOSIM_EXCRETION` moves *matter*, not energy
(`RunConfig.ExcretionPerJoule`), so the only energy entering the detritus pool is remains.
Round 14's worlds run about 0.08–0.1 producer deaths per second, and a producer that has
spent its reserve on children dies carrying little more than its ~1 J of tissue; 0.1 per
second times ~2 J is the 0.2 W observed. Against a producer economy of ~17 W (1,700 bodies
at ~0.01 W upkeep) the second trophic level is fed at about **1%**. Real pelagic transfer
efficiencies run near 10%, and the difference is not grazing losses — it is that this
world's producers exude nothing while alive.

*What is estimated here:* the death rate (from the `deaths` column's differences), the
remains energy per death (inferred from the rebuild rate and the death rate, not logged),
and the producer economy (bodies × a typical upkeep). None of these is measured directly
by any instrument today; the first item below fixes that.

## What the existing stabilisers do to this body (ledger, clearance 10, −12 m)

Run today on the inoculum genome under `r14c10-s2`'s config with one field changed:

| variant | R0 at 0.5 / 1 / 2 / 4 / 7 / 10 J/m³ | break-even | reading |
|---|---|---|---|
| as run | 0 / 0 / 1 / 2 / 4 / 6 | 0.4 | the boom's gearing |
| satiation 20 W/m³ (round 8's value) | 0 / 0 / 1 / 1 / 1 / 1 | 0.4 | growth capped at R0 = 1: a line cannot get from one mutant to ten |
| satiation 12 | 0 at every density | 0.4 | this body can never earn a child |
| satiation 8 | 0 at every density | 0.4 | same |
| toe 1 J/m³ | 0 / 0 / 0 / 1 / 3 / 5 | 0.86 | starves earlier; boom barely damped |
| toe 4 (round 8's value) | 0 / 0 / 0 / 1 / 2 / 4 | 1.48 | starves at three times the density; boom damped by a third |

Satiation binds on the child's price: 103 J of the 129 J price is the endowment, fixed
whatever the body earns, so a 0.0022 m³ stomach capped at 12–20 W/m³ of tissue clears
0.009–0.025 W and cannot reach the price inside a lifetime. The toe raises the density at
which the stomach starves without raising what feeds it. The floor refuge (D055) protects
the one layer this world's stomachs already do not sit in, and was rejected twice. **None
of the three raises the flux; the flux is the constraint.** I do not recommend a
stabiliser arm.

## What would

Three levers reach the income. The first two are world rules and the owner's; the third
is the genome's own.

1. **Exudation — a fraction of photosynthetic intake deposited as detritus at the
   producer's own layer, while it lives.** The real mechanism (dissolved organic carbon
   exuded by phytoplankton, 5–30% of fixed carbon; the microbial loop) and the one this
   world lacks. At 10% of a ~17 W producer economy it is ~1.7 W into the pool — a
   standing capacity near sixty stomachs at clearance 10, spread over the producers'
   depth, which is where the mutants are born. The audit closes by construction (it is
   the producer's intake, routed). New knob, default 0, header token, ledger-visible
   through `Metabolism.StepAt` like the toe. *Not screened yet: whether the exuded joules
   reach the stomachs' cells before the 0.002 m/s sink and 0.2 m²/s mixing spread them —
   the flux instrument (below) answers that on the first arm.*
2. **Remains that carry a reserve.** Producers here die near-empty because breeding
   spends the reserve. A senescence death that returned a fuller body would raise the
   flux, but only by changing what the producer keeps — a deeper change to the breeding
   rule, and it moves the producers' own dynamics. Listed for completeness; I would not
   start here.
3. **A cheaper child.** The endowment is 80% of the price. A 30 J endowment triples the
   standing capacity at the same flux, and the endowment is genome-encoded, so evolution
   could find it — but at mutation 0.005 in a line of six it will not, inside any budget
   we run. Whether a world-level cap or floor on the endowment is a rule the owner wants
   is a separate question; it is not the first lever.

**Recommendation: lever 1**, screened before any round: ledger the mutant under an
exudation config (R0 at the densities the flux would set), then two 0.02-step arms
(seeds 1 and 2 of clearance 10, since those are the worlds with a line) reading the
`detritus J` slope and the line's size at t = 20,000 against `r14c10-s1/-s2`; a full
0.01 confirmation round only if a line holds ten past two lifetimes.

## Instruments first (agent work, no ruling needed)

- **The detritus flux by source, per window:** joules deposited into the field from
  deaths, from exudation (if built), and lost to the sink below the world, as report
  columns. Today the flux is inferred from a slope; the proposal above rests on that
  inference, and one column each makes it a measurement.
- **The assay's horizon (0051):** inoculate at 5,000 and run to 20,000, or score
  generation +2 births rather than members alive; the current design cannot score
  establishment.

## What I am asking the owner to rule on

1. Is exudation a world rule this ecology should have? (My reading: yes — it is the
   missing 90% of the second level's income, and it is a real mechanism with a
   literature; the review has nothing on it yet and would need a round.)
2. If yes: the screen above, at 0.02, two seeds, before any round. If no: I have no
   knob-level route to ten stomachs alive in this world, and would say so in 0050's
   closing rather than run seed 5.
3. Seed 5 of each dose: the sequential rule says run them. With the mechanism this
   clear I would stop at four and spend the workers on the screen — the owner's call.
