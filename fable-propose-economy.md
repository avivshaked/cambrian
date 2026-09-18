# Proposal: one substance. The matter and energy economy rethought

**2026-09-18, evening**  ·  for the owner's ruling, on the owner's ask ("rethink the
matter/energy economy and figure out how to bring it closer to what reality looks like, or at
least to something that works better"). It supersedes `fable-propose-matter.md`, whose store
and whose margin genes are folded in below. On ruling it is absorbed into DECISIONS.md as
the economy's new base and deleted; DESIGN.md §5A.2 and §5A.2d are rewritten from it, with
the old text struck through and not removed.

## How reality does it

A body is matter. Its energy is the chemical content of that matter: a gram of dry tissue
holds about twenty kilojoules, and there is no energy in a body that is not in its matter.
Plants take **inorganic** matter from the water (carbon, nitrogen, phosphorus) and, with
light, make **organic** matter, which is tissue and stored fuel. Animals eat organic matter
and get energy and building material in one bite; what they burn to stay alive goes back to
the water as inorganic matter and to the world as heat. Dead organic matter is eaten by
detritivores or broken down by microbes back into inorganic matter, which the plants take up
again. So there is one conserved pool, matter, in two states, inorganic and organic, and one
flow that is not conserved, energy, which comes in as light and leaves as heat. A closed
aquarium under a lamp is exactly this: the matter cycles, the energy passes through.

Two consequences decide what is interesting in such a world. **The surface is poor and the
deep is rich**, because the plants strip the lit water and their remains sink and remineralise
below it, which is the vertical structure §5A.2d wanted and could not get. And **eating is the
only way an animal gets anything**, so where the food is, how to reach it and how to keep
from being it are the whole of an animal's life.

## How our world does it, and what is off

Two currencies, never added (§5A.2d). Energy in joules: sunlight in, upkeep out, tissue and
reserve measured in joules, a corpse's joules to the detritus field, which eaters eat. Matter
in units: seeded once, drawn from the parent's cell at conception and at every growth step,
locked in the body, excreted a little, returned at death to the cell. The two meet at one
place only, a conception, where both are required.

Round 40's books say what that produces. A body holds 3.16 units of matter, 3 of them the
fixed per-creature charge, so matter is a head-count licence and not a substance. A corpse's
energy goes to the eaters and its matter to whoever breeds in that cell, so an eater is a
plant for matter and an animal for energy, and a leaf in full light with an empty cell cannot
breed while a leaf in the dark with a full one can. And because the matter is drawn as a lump
at conception from a cell that holds about one child's worth at equilibrium, who breeds is a
lottery on cells, the finding of 0106's read and D097's reason. The energy side is fine; it
is the matter side that was bolted on, and the joint is where it shows.

## What is proposed

**One substance, matter, in two states; energy is the content of the organic state.**

1. **Units.** Matter is the unit. Organic matter carries `JoulesPerUnit` (ρ) of energy;
   inorganic carries none. Tissue, reserve, detritus and corpses are organic matter; the
   water's dissolved pool is inorganic. Every joule the code handles today is ρ times a
   quantity of organic matter, so the energy pathways stay as written and the matter draws
   go.
2. **Photosynthesis makes organic matter from inorganic.** A leaf's income is
   `min(light capacity, uptake capacity)`: the light term as today, the uptake term
   `k · surface · c / (c + K)` with `c` the inorganic density at the leaf, saturating in
   it. What is made is taken from the cell as inorganic and appears in the body as organic;
   a leaf in stripped water is light-rich and makes nothing, which is the surface stripping
   itself.
3. **Living burns organic matter to inorganic.** Upkeep, neural cost and mechanical work
   remove organic matter from the body, return it as inorganic to the cell the body is in,
   and the energy leaves as heat (logged). This is today's upkeep with the return leg
   added; today's excretion contract (D052) becomes this and is retired as a separate
   rule.
4. **Eating moves organic matter.** An absorptive part takes organic matter from what it
   touches: the detritus field today, and living tissue when the mouth comes (§5A.3, D097
   item 2), with an assimilation efficiency; the unassimilated part returns to the water as
   inorganic. An eater gets energy and building matter together, which closes the second
   loop.
5. **A child is organic matter given by its parent.** Conception moves the child's start
   (tissue and reserve) from the parent's organic matter, as the energy rule does today, and
   draws nothing from the cell. Growth builds tissue from the body's own reserve, drawing
   nothing from the cell. The cell is drawn at a rate by photosynthesis and by nothing else,
   so there is no lump to be blocked on and no lottery.
6. **Death makes detritus; detritus remineralises.** A dead body's organic matter goes to
   the corpse and then to the detritus field as today; the detritus field decays to
   inorganic at `RemineralisationPerSecond` (the tunable exists at 0 since D051 and
   becomes the nutrient return). Detritus sinks as today, so the deep is where the matter
   comes back.
7. **The fixed charge goes.** D065's per-creature charge existed to bound the head count
   when a lineage shrinks; the newborn mass floor (D087, `MinNewbornKg`) bounds it now, since
   a body of minimum mass is a fixed quantity of matter and the pool is finite.
8. **The breeding margins are the genome's** (the owner's rule, 2026-09-18 evening): beside
   `BirthInvestment` and the brood, `ReserveMargin`, the organic matter a parent holds
   back beyond the child's start before it breeds, as a fraction of that start. One gene
   now, not two, because there is one substance. Mutated at the investment dial's rate,
   drawn for founders, read in the table and on birth rows.

**The two audits, both kept.** Matter: inorganic in the water plus organic in bodies, corpses
and detritus is a constant, to the bit, at every sample, as `matterStanding` is today.
Energy: light captured less heat burned equals the change in organic matter times ρ, a hard
equality, which is §5A.2's audit in the new unit; a physics exploit that made kinetic energy
would still have no path into organic matter, so the property the audit exists for survives.
`mat resid` and `audit` stay the two columns that must read 0.

## What it changes in the world's behaviour

- The surface strips and the deep enriches, by the organisms, as §5A.2d measured once and
  wanted (a 300-fold gradient in the first matter build). Position in the water is worth
  something to a leaf again, and the light's reach (D096) and the nutrient's return depth
  pull opposite ways, which is the interior optimum the design asked for in 2026-09-07.
- Who breeds is who makes or eats organic matter fastest, a rate a body can be better at:
  more surface in richer water, a mouth on richer food.
- Movement, brains and joints are priced in the same currency as food, so a stroke that
  reaches richer water pays in the unit that breeds.
- The head count is bounded by the pool over the mass floor and by nothing else, so the
  crowd can grow past today's 2,500 if the matter allows it; the pace follows the crowd,
  and the budget or the tank sets the crowd the machine can afford.

## The numbers

ρ is set from the recorded world, not chosen: round 40 holds about 5,000 J of tissue in
2,500 bodies and 262,000 J in its detritus field against 11,000 units of matter, so the
joule and matter scales as built disagree by an order of magnitude, and ρ has to make the
standing organic joules of a founding world equal a stated share of the matter budget. The
smoke reads it. `k` and `K` for uptake come from the ledger as the matter proposal's did
(a leaf's first child at about the energy ledger's time in founding water). The
remineralisation rate sets how long a corpse's matter is out of play; a half-life of an
hour is the starting value and the smoke's second reading.

## What it costs

A Core build of three to five days, not one: `Metabolism`, `World`'s income, upkeep,
conception, growth and death legs, the two fields' units, both identities, the ledger
(`LedgerForecast`), the report's columns (every `J` column reads organic matter times ρ, and
`mat blk` goes), the theatre's census strings, and the tests that name joules. The genome
format goes to 6 for the margin gene, refusing every stored genome. Every recorded world
replays under its own build, and no recorded config is readable by the new one, per the
tunable rule. `-All` before the merge, then a 5,000 s screen at dt 0.02 on round 40's world
reading the two audits, the crowd against round 40's, the newborns' depth against their
parents', and the surface-to-deep inorganic ratio; then a five-seed base round with its own
entry. DESIGN.md §5A.2 and §5A.2d are rewritten, the old text struck through.

## What is not proposed

Stoichiometry (nitrogen against phosphorus): one inorganic pool is the first order. A
temperature or tempo term (the owner's tempo dial waits its turn). Opening the pool with an
influx. The mouth itself, which is the next proposal and is what this base is for.

## Rejected on the way

Keeping two currencies and adding a matter store (`fable-propose-matter.md`, this
afternoon): it removes the lottery and leaves the licence fee and the two loops. Dropping
matter entirely and capping the crowd on energy alone: nothing then strips the surface,
and a leaf in the light breeds forever, which is the world before D048.
