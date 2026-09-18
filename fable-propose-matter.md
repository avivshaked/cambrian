# Proposal: matter as income, a child paid from the parent's own store

**2026-09-18**  ·  D097's item (1), for the owner's ruling. The agent proposed "the per-child
price brought down" at midday; the arithmetic below says that dial cannot do the job, and
this file proposes the change that can. On ruling it is absorbed into DECISIONS.md and
deleted.

## What the arithmetic says about the price

Round 40's seeds hold 3.1 to 3.2 units of matter per living body, of which 3 is the fixed
per-creature charge (D065) and the rest is tissue. The crowd is the stock over that number:
11,000 units, 72% of it in bodies, is 2,500 bodies. Lower the charge to 0.5 and the same
stock holds about 15,000 bodies, which is the population ceiling (8,000) and a run three
times as slow. Lower the stock with it to keep the crowd at 2,500 and the cell's free
matter falls in proportion, so a 5 m cell still holds about one child's worth. The cliff
is structural: a crowd grows until the free matter in a cell no longer affords a child,
whatever the price of a child is, so at equilibrium every cell sits at about one child's
worth and who breeds is a lottery on which cell happens to be over the line. No price and
no cell size removes that; they only move where it sits.

What removes it is changing what a parent competes for. Today a parent competes for the
instant stock of its cell at the moment it conceives (`Matter.ReachableStock` against the
child's price, `World.Conceive`), and the growth draw takes matter from the cell without
limit whenever the reserve can pay. So there is no rate in the matter economy at all, and
therefore nothing a body can be better at.

## What is proposed

**Matter becomes income.** Every body carries a matter store (`Organism.MatterStore`) beside
its energy reserve, filled from the water at a capped rate per second, and a child is paid
for out of the parent's store and not out of the cell.

1. **Uptake.** Each metabolic step a body draws matter from its cell at most
   `MatterUptakePerSecond × tissue joules × step`, saturating in the cell's density:
   `rate = k · tissue · c / (c + K)` with `c` the cell's units per cubic metre and `K` the
   half-saturation density (`MatterUptakeHalfDensity`). The draw is a `Matter.Take` exactly
   as growth's is today, gated before the take so nothing leaks, and it goes to
   `MatterInBodies` as it does now, so both identities close unchanged.
2. **The store pays.** Conception charges the child's price (tissue × `MatterPerTissueJoule`
   + `MatterPerCreature`, unchanged) from the parent's store; a parent whose store is
   short does not conceive, counted as `ConceptionsShortOfStore`. Growth pays its matter
   from the store the same way. The cell is never asked at conception, so
   `ConceptionsBlockedByMatter` reads 0 by construction and its old meaning is gone.
3. **The store is body matter.** It is counted in `MatterInBodies`, it returns at death with
   `LockedMatter`, and excretion drains `LockedMatter` as today (the store is not
   excreted; a body does not leak what it has not yet built). A store has a ceiling,
   `MatterStoreCapPerJoule × tissue`, so a body cannot hoard the water.
4. **A founder starts empty.** As today with `LockedMatter`; a founder's first child waits on
   its uptake like everyone else's.

What this changes in the world's behaviour, in plain words: the equilibrium is still all
the matter in bodies and births equal to deaths, but the question "who breeds" changes
from "whose cell has 3.1 units this step" to "who takes matter up fastest", which is a
rate a body can be better at: a larger absorptive surface, sitting where corpses decay,
and, once the mouth exists, eating a body, which is a whole store in one bite. That is the
prize movement has never had.

## The numbers, from the ledger

The energy ledger says a round 39 leaf at 6 m under the 6 m light has its first child at
about 1,040 s. For the matter store to keep pace, a leaf of about 2 J of tissue must take up
its child's 3.1 units in that time at the campaign's equilibrium density of 0.025 units/m³,
so `k` of order 0.0015 units per second per joule at saturation and `K` at or below that
density. At the founding density of 0.11 units/m³ the same leaf fills its store in a
quarter of the time, so founding is faster than the equilibrium, as it is today. The
ledger (`LedgerForecast`) gains the store and reports the matter-limited first-child time
beside the energy-limited one; the number that decides the value is the smoke's.

## The smoke and the read

One 5,000 s screen at dt 0.02 on round 40's world (seed 3, worker 5, once an arm's slot is
free under the three-arm rule), against round 40's own seed 3 at the same second:

- `ConceptionsShortOfStore` per birth in the 3,000 to 5,000 s window against round 40's
  blocked-on-matter count of about 130 per birth per window. A ratio under 10 is the
  screen's bar.
- `alive` within 0.7 to 1.5 of round 40's at the same second (the stock still caps the
  crowd through the fixed charge).
- The newborns' median depth against their parents' population median from
  `birth-depth.py`: within 3 m, where round 40 reads 10 m deeper (the cell lottery's
  signature), which is the reading that says the lottery is gone.
- `audit` and `mat resid` at 0.

Then a five-seed round on the base with the pre-registered predictions in its own entry.

## What it costs

A Core change of a day: the store on `Organism`, the uptake pass beside `Grow`, the
conception and growth draws redirected, three tunables (`MatterUptakePerSecond`,
`MatterUptakeHalfDensity`, `MatterStoreCapPerJoule`, all refusing every earlier config per
the tunable rule), the two identity tests extended to the store, the ledger's matter leg,
and the table's `mat blk` column replaced by `store short`. Every recorded world replays
under its own build. The change touches the world, so `-All` runs before the merge.

## What is not proposed

Opening the matter books with an influx (the closed books are what make every reading
trustworthy). Changing the fixed charge or the budget (they set the crowd's size and stay).
The mouth: its own proposal, on this base, next.
