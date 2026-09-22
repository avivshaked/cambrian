# The mouth

*2026-09-22, night (draft; the hashes, round 44's reading and the launch commit are filled
in before the seeds launch). Written by the agent as the pre-registration of round 45, the
second rung of the animal kit under D106: a cell is the unit of death, a mouth kills and a
mouth consumes, and every attribute that helps is priced and capped by cell type. The
predictions are committed before the three seeds launch.*

## What it asks

Nothing in this world has ever eaten anything alive. The eaters of rounds 14 to 43 were
absorptive tissue feeding on marine snow, and every corpse went to the water whole. D106
gives a body four attributes on any cell, each heritable, each priced, each capped by the
cell's type: attack (damage to a touched part), intake (charged units taken from a corpse
in reach), protection (damage absorbed) and toughness (health per volume). A part whose
health reaches zero leaves its body as a corpse; the killer gains nothing from the kill;
what a mouth gains is intake from corpses, its own kills or anyone's. So the round asks
whether a scavenger founds, whether a killer founds, whether a plant that can regrow its
parts (round 44's gene) survives being grazed, and whether defence arrives after offence
rather than before it, which is the order the economy should impose when armour costs and
nothing yet bites.

## The world

Round 44's (0113), read at `<round 44's reading here>`, with the mouth's dials from the
ledger screen (`logbook/specs/mouth-spec.md`, the screen note, and
`logbook/specs/r45-read/`): `rounds/env-r45.ps1` through `run-farm.ps1`, seeds 1 to 3,
30,000 s at dt 0.01, on the mouth build (`8b0d798`, the cap change `e877406` and the kill
event `6fcf93b`; `coreHash a20aba8c…`, `dynamicsHash b4cdb22a…`, `farmHash 36d8e11d…` as
the fixture recording `r45fix-s4` read them, and each seed's manifest is the record). A
checkpoint is written every 2,500 s, a recording setting, so that a slow seed's state can
be profiled, which round 44's seed 1 could not be. `HealthPerCubicMetre` 13, at which a claw at the structural cap takes three
metabolic steps to kill round 43's median leaf part; healing 1% of the pool a second at
1 J per unit; intake reaching 0.5 m past a corpse's surface with a fifth of the take to
snow; prices per unit per m² of the part: protection 1 W, attack, intake and toughness
0.1 W; attribute mutation at the cell-type rate, 0.005; `Contact` and `Damage` in the
sensor pool. The leaf's and absorptive cells' protection caps are 0.5, where a capped
cuticle doubles the steps a claw needs. Every founder is at zero attack and protection,
with intake at the consumer cell's cap on consumer nodes, so at t = 0 nothing bites and
nothing is armoured, and the organs enter by mutation alone.

## Rules

A manifest reading `error` or `stopped` is censored and read at its last sample. A seed
past 2,500 bodies is stopped. Three seeds (D095), three at once at 8 threads. The read is
`scripts/reads/r45-read.py`, written before the launch, and every clause names its column.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| J1 | **a scavenger line founds**: a lineage with intake above zero on a non-consumer node or above the founder's on a consumer node, and `corpse eat` above zero in 20 consecutive windows, in 2 of 3 | `lineage.jsonl` (`ink`), the column |
| J2 | **a killer line founds**: a lineage with attack above zero and `killed` above zero in 20 consecutive windows, in 1 of 3 or more | `lineage.jsonl` (`atk`), the column |
| J3 | **an indeterminate plant survives grazing**: among bodies that lose a non-root part, those with an indeterminate node are alive 1,000 s later more often than those without, in 2 of 3 | `partsKilled` events against `ind` and the lineage deaths |
| J4 | **defence follows offence**: the first window in which `prot %` exceeds 1% comes after the first in which `attack %` does, in 3 of 3 | the two columns |
| J5 | **the crowd survives its predators**: `alive` above 300 at 30,000 s in 2 of 3 (the massacre reading if not) | the timeline |
| J6 | **the books close**: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at 30,000 s, `diverged` 0, in 3 of 3 | `stats.jsonl`; the manifests |
| J7 | **the yield is the reserve**: `unitsEaten` per corpse eaten tracks the corpse's reserve share and not its tissue: the mean take per corpse exceeds the mean tissue of a killed part by a factor of 10 or more | the counters against the corpse rows |

## The two-sided readings

- **J1 holds and J2 fails:** scavenging pays before killing does, which is the order the
  prices were set to give; a claw's 5% of a leaf's income is not repaid by corpses no one
  else makes. The killer waits for a world with more carrion or a cheaper claw.
- **J2 holds:** the first predator. Read where the kills fall (`partsKilled` against
  `bodiesEaten`: grazing or predation), and what the killed carried.
- **J3 fails with kills present:** regrowth does not save a grazed plant at this
  healing and this threshold, or the grazed parts are the roots. Read the killed parts'
  depth in the body before the gene.
- **J4 fails:** armour spreads before anything bites, which at 1 W per unit per m² it
  should not; read the drift of a free-to-mutate attribute in a world of no selection
  before reading it as a fault.
- **J5 fails:** the massacre. A claw that kills in three steps against a leaf that heals
  at 1% a second is a world the prey cannot hold; the next round raises health or the
  cuticle's cap, and the entry says which the numbers point at.

## What the round does not ask

Whether a body can smell a corpse (rung B). Whether a mouth is a joint's reason to swim
(no body strokes, still). Whether a claw can be aimed: contact is whatever the water and
the crowd bring together.
