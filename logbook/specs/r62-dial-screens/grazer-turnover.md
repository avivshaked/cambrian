# Why round 61's grazers starve: their intake against their cost

*2026-10-19, the coordinator. A read of the existing g1 economy files (`g1-economy-u-6101.txt`) and the dial A
ledger (`g1-ledger-dialA.txt`), after the matter scan (`logbook/specs/matter-ocean-scan.md`) gave real herbivores
a growth efficiency of 10 to 30%. No new run.*

## The numbers

- Round 61's planted grazer (pool/04) costs 0.908 W standing, 0.63 W of it the consumer cell, and holds 54 J of
  tissue. It burns its own body's worth of energy in about 60 s.
- Grazers keep 0.8 of what they eat (`intakeWasteFraction` 0.2). That is close to a real zooplankter's
  assimilation of about 0.7, so the conversion is not what starves them.
- Across round 61's cousin u-6101, a mouthed body made 0.83 kills in a mean life of 684 s, at about 45 J kept a
  kill: a mean kept power of 0.055 W, about 6% of its standing cost. Its founding endowment of 455 J runs out
  in about 500 s, and the grazers' median age at death is 638 s, 96% of them starved.
- Dial A at its lowest step (the consumer cell's upkeep 6 to 0.75 W/m³) leaves the body at about 0.36 W, still
  about six times what it eats. That is why group 2 found nothing.
- Mouthed bodies with leaves (only 25 died in the read) lived a median 1,508 s, against 638 s for grazers without leaves, and 44% of them
  died of age rather than starvation.

## What it means (inference)

The grazers fail on intake, not on efficiency. A grazer finds about a sixteenth of the food it burns, and no
upkeep dial within reach closes that gap. Hybrids already outlive pure grazers by more than twice, so the gentle
route to a grazer runs through bodies that keep some leaf, which is what D207 makes easier. The dial search in
the square should read intake per grazer-second as its first column, and try the mouth's reach and the leaf
corpse's value as well as upkeep.
