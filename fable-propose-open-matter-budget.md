# Proposal — the open matter budget: an influx and a burial, the world's size a flow

Fable, 2026-09-04, after logbook/0055–0057 and the owner's remark the same evening:
*"matter like energy is not finite. there's a constant influx of both on our planet."*
For the owner's ruling; absorbed into DECISIONS.md when ruled, then deleted.

## Why

Three screens in one day said the same thing from three sides. The world's matter is a
conserved stock of 6,000 units; at maturity 97% of it is in bodies; the count of bodies
is the stock divided by the price; and once the count is set, every solvent body has
the same fecundity whatever it earns. Moving the matter (0055), fixing the queue (0056),
letting energy bid for it or tripling it (0057) each changed the arrangement and none
changed the arithmetic. Energy income buys survival and nothing else. The second trophic
level's share of the world is a fraction the stock sets.

The ocean is not like this. Its nutrients — nitrogen, phosphorus, iron — enter by
rivers, weathering and dust and leave by burial in the sediment, and productivity in any
patch of sea is set by the *supply rate* (upwelling, mixing), not the standing stock.
That is what the owner said, and DESIGN.md §5A already treats energy this way: light in
at the top, respiration out everywhere, nothing conserved except in the audit. Matter
should be the same shape.

## The rule

Two terms, both default 0 so the record replays:

- **Influx** — `EVOSIM_MATTER_INFLUX`, units/s, deposited into the free matter field
  each metabolic step. Two shapes, chosen by `EVOSIM_MATTER_INFLUX_AT`:
  - `surface` — spread over the top layer of every patch (rivers, dust: the ocean's
    dominant new-nutrient input where there is no upwelling). No other code touched; the
    matter then sinks and mixes as it does today. **The cheaper shape, recommended first.**
  - `vent` — at the plume's base in the vent patch (D067's `EVOSIM_VENT_PATCH`,
    `EVOSIM_VENT_DEPTH`), riding the upwelling to the surface. Needs the vent on, which
    the reference world does not have; a second screen if the surface shape holds.
- **Burial** — `EVOSIM_MATTER_BURIAL`, per second, the fraction of the floor layer's free
  matter removed from the world each step (sediment). Detritus is not buried by this
  rule — its energy is the leak's and the corpses', accounted separately — only matter.
  Without burial the influx runs the population to the ceiling; with it the standing
  stock converges to influx ÷ (burial × floor stock) and the world's size is a flow.

**Accounting.** `StandingMatter` stops being constant. The identity becomes
`initial + influxed − buried = free + locked`, carried by two new totals
(`MatterInfluxedTotal`, `MatterBuriedTotal`), a `mat in` / `mat buried` per-window pair
in the report, and the same equality in the tests that today assert conservation. The
header carries `matter in X/s at surface|vent · burial Y/s`.

**Dose.** Set the influx so the *equilibrium* stock is the current stock: with the
floor holding about a tenth of the free pool and the free pool a tenth of the stock,
a burial of 0.01/s removes ~0.6 units/s at today's floor stock, so an influx of 0.6
units/s holds 6,000 units in steady state and turns the whole stock over in ~10,000 s
— three lifetimes. The ledger cannot forecast this (it has no matter); the first arm
reads the equilibrium and the dose is corrected from it. Then a second dose at 2× to
read the world's size as a flow.

## What it should change, and what it should not

- The population is set by influx against burial, and the matter in bodies turns over
  instead of sitting: a body that dies at the floor returns matter that is partly
  buried, and new matter arrives at the top. The plateau becomes dynamic.
- Where the matter arrives becomes what decides who breeds — at the surface, the leaves
  first and whatever sinks past them second; at the vent, whoever sits in the plume. A
  question bodies can answer by moving, which is the first route from the matter economy
  to the movement question.
- It does not by itself favour a stomach over a leaf. That is not its job; its job is
  to make the world's size a flow and the free pool a rate, so that later rules
  (perception, movement, a price for matter in energy) have something to act on.

**Predictions for the pre-registration, in outline:** `mat in` − `mat buried` → 0 over
the run and `StandingMatter` converges (V); `alive` within the wingspan of the reference
world's at the matched dose (M1); the free pool at the population's depth above 0.3
units/m³ (M2, the number 0055 asked for and did not get); the age queue weaker (median
parent age below 2,000 s) because arriving matter is not contested by the whole column
at once (M3); a connected stomach clade ≥ 10, stable, in both seeds (M4, no claim that
it beats the control's). Seeds 2 and 4 at 0.02 against 0056's controls; 0.01 if it holds.

## Rejected on the way

- *Influx without burial* — the ceiling within a lifetime; a censored run by construction.
- *Burial of detritus too* — changes the energy budget the leak was tuned against;
  separate question.
- *A source that follows the population* (matter deposited where bodies are) — a rule
  that hands the contested resource to whoever is already winning; the ocean does not.

## Cost

Core: two terms in the matter field's step, two totals, the identity in tests; Sim: two
env variables, a header token, two columns. Half a day of agent work. Machine time: one
screen at 0.02, ~2 h.
