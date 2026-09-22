# The mouth

*2026-09-22, night; launched 2026-09-23 at 00:28 local from `35395be` (the launch section
below). Written by the agent as the pre-registration of round 45, the second rung of the
animal kit under D106: a cell is the unit of death, a mouth kills and a mouth consumes, and
every attribute that helps is priced and capped by cell type. The predictions were
committed before the three seeds launched; the island predictions (J8 to J10) were added
with the world's change (D109) the same night, before the launch.*

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

Round 44's (0113), ten times larger and seeded as islands (D108, D109). Round 44 was read
the same night: the gene kept in two seeds of three, jointed bodies keeping it as readily
as leaves, the books closed to 1e-06 units, and seed 1's world of module chains gone deep
and to the glass and sevenfold dearer a body-step. The mouth's dials are from the ledger
screen (`logbook/specs/mouth-spec.md`, the screen note, and `logbook/specs/r45-read/`).
The tank is 22,000 m² (r 83.7 m) at the same 45 m, the bed's wavelength held at round 44's
17.64 m rather than derived from the radius, so the rock is the same rock over a larger
floor; the owner asked for the room (2026-09-22 night), the tank having been sized for the
Unity farm's pace and never re-asked when the farm left Unity. The matter is 15,000 units,
ten times round 44's, and it is not spread evenly: a noise map of 60 m wavelength picks a
tenth of the columns, and those hold the whole budget in their top 12 m, level across each
island (0.57 units/m³ at t = 0, thirty-eight times round 44's water, in a few distinct
islands on a 167 m disc); the founders, and every body the floor spawns, are planted in
the islands and no deeper than 12 m; and the matter grid stirs at 0.02 m²/s, the snow's
rate, where every round from 32 stirred it at a hard default of 2. The light is round
44's, even everywhere; the shade map D109 built is at 0. Each number is a screen's
(D109: the even dilute tank does not found, the islands at the old stirring are gone in
800 s, a peaked profile puts founders in half-strength water, the plateau at 1,500 units
founds and then starves, and at 15,000 the world founds faster than round 44 with two
crowds on two islands and the deserts empty; `scratch/r45-build/runs/bigJ`, whose maps
are in this entry's images). What the world does after founding is the round's first
reading: the islands spread on the gyre's own smearing of the 5 m cells, about 0.1 m²/s,
into round 44's soup near 5,000 s, and whether the crowd stays on them is J8. The screen's
own maps are the entry's first pictures: the islands at 100 s with the founders on them,
the two crowds at 1,500 s, and the spread field at 3,000 s with the snow beside it
(`images/0114-islands-bigJ-t0000100.png`, `-t0001500.png`, `-t0003000.png`; `bigJ` carries
the shade map at 0.8, which the round does not, and `bigK` without it founded the same to
the body). Two things in them are read in the round and not predicted: the north-east crowd
sat on the glass in a crescent from 1,500 s with its snow piled at the rim beside it, so
`p3` over `alive` is read as in every walled world (the rim quarter held a fifth of the
crowd at 4,000 s, no crust in the aggregate); and the south-west crowd was carried toward
the centre with its water (`x sd` falling, `mean m/s` 0.15 by 4,000 s), which is what J8's
second threshold is set against. The water is not the cost: the 1.05-million-cell grid steps in about 65 ms at 4 threads
(`scratch/r45-build/runs/bigA`, a 300 s smoke); the solver is, at 1.6 to 2.7 µs a
body-step, so a seed at round 44's crowd is hours and at ten times it is two days. The
runaway ceiling is 25,000 so that neither is censored. The launch is `rounds/env-r45.ps1` through `run-farm.ps1`, seeds 1
to 3, 30,000 s at dt 0.01, on the mouth build (`8b0d798`, the cap change `e877406`, the
kill event `6fcf93b`, the centre-of-mass guard `2771bf0`, the contact grid and the
checkpoint writer's fix `1535386`, and the reach bound, off, with the re-recorded
fixtures); the first launch, from `4300278` at 20:58 local on the small tank
(`configHash 4e84dc9f1ac8ecf1`), is void, its three seeds ended by the writer's fault
within 7,000 s (`runs/r45void-s1..3`, HANDOFF). The relaunch's commit and hashes are in
the launch note below, and each seed's manifest is the record. A checkpoint is written
every 2,500 s, a recording setting, so that a slow seed's state can be profiled, which
round 44's seed 1 could not be. `HealthPerCubicMetre` 13, at which a claw at the structural cap takes three
metabolic steps to kill round 43's median leaf part; healing 1% of the pool a second at
1 J per unit; intake reaching 0.5 m past a corpse's surface with a fifth of the take to
snow; prices per unit per m² of the part: protection 1 W, attack, intake and toughness
0.1 W; attribute mutation at the cell-type rate, 0.005; `Contact` and `Damage` in the
sensor pool. The leaf's and absorptive cells' protection caps are 0.5, where a capped
cuticle doubles the steps a claw needs. Every founder is at zero attack and protection,
with intake at the consumer cell's cap on consumer nodes, so at t = 0 nothing bites and
nothing is armoured, and the organs enter by mutation alone.

## The launch

The three seeds launched together at 00:28 local on 2026-09-23: seeds 1 to 3, 30,000 s at
dt 0.01, five threads each, a checkpoint every 2,500 s, and a 1,500-minute wall set for the
slow case, a seed whose deserts fill. The run directories are
`runs/r45-s{1,2,3}/2026-09-22-232754-e51997d5`, named by the UTC clock. Every manifest
reads commit `35395be` with the tree clean and `configHash e51997d5e9237a2e`; the other
three hashes are `82457fda…` (dynamics), `45343389…` (farm) and `7ba22bc6…` (core). Every
header was read after the launch and not from the command, and carries the island tokens
(`matter islands 60 m cover 0.1 to 12 m · founders in matter · shade off`), the stirring
(`matter-mix 0.02 m2/s`), the tank (`area 22000 m2`), the founder depth (12 m), the
ceiling (25,000) and the budget (15,000).

The fixtures were re-recorded before the commit, and the new crowd fixture replays the
old one in 143 fields at 2,000 samples with the lineage byte-equal (`r45fixc-s4` against
`r45fixb-s4`): the island build with the islands off is the recorded world.

One thing to know about the directories. A first launch two minutes earlier went out from
an exe built before the shade map's second cut, which is inert at shade 0 but is not the
committed tree. The three were stopped within a minute, their directories are kept beside
the real ones with `-stale-exe` appended, and the farm was rebuilt from the commit before
the launch above. Every reader takes the newest directory.

## Rules

A manifest reading `error` or `stopped` is censored and read at its last sample. A seed
past 25,000 bodies is stopped (2,500 on the small tank). Three seeds (D095), three at once
at 5 threads. The read is `scripts/reads/r45-read.py`, written before the launch, and
every clause names its column.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| J1 | **a scavenger line founds**: a lineage with intake above zero on a non-consumer node or above the founder's on a consumer node, and `corpse eat` above zero in 20 consecutive windows, in 2 of 3 | `lineage.jsonl` (`ink`), the column |
| J2 | **a killer line founds**: a lineage with attack above zero and `killed` above zero in 20 consecutive windows, in 1 of 3 or more | `lineage.jsonl` (`atk`), the column |
| J3 | **an indeterminate plant survives grazing**: among bodies that lose a non-root part, those with an indeterminate node are alive 1,000 s later more often than those without, in 2 of 3 | `partsKilled` events against `ind` and the lineage deaths |
| J4 | **defence follows offence**: the first window in which `prot %` exceeds 1% comes after the first in which `attack %` does, in 3 of 3 | the two columns |
| J5 | **the crowd survives its predators**: `alive` above 1,000 at 30,000 s in 2 of 3 (the massacre reading if not; round 44's crowd in a tenth of this water was about a thousand) | the timeline |
| J6 | **the books close**: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at 30,000 s, `diverged` 0, in 3 of 3 | `stats.jsonl`; the manifests |
| J7 | **the yield is the reserve**: `unitsEaten` per corpse eaten tracks the corpse's reserve share and not its tissue: the mean take per corpse exceeds the mean tissue of a killed part by a factor of 10 or more | the counters against the corpse rows |
| J8 | **the crowd founds in the islands and is carried off them slowly**: the share of living bodies standing in the columns that were islands at the first dump (above the mean at 100 s, about a sixth of the water with the ramp, so a crowd spread evenly reads about 0.17) is above 0.6 at 1,000 s and above 0.35 at 5,000 s, in 2 of 3 (the screen read 0.74 and 0.43 at 1,000 and 3,500 s: the water carries the crowd as it carries the stock) | `scripts/field-map.py <arm> --at 1000,5000 --layers 3 --footprint 100 --no-pictures`, the `in-footprint` column against the header's even share |
| J9 | **the islands become soup and the crowd's own field does not**: the spent field's column cv falls below 0.5 by 8,000 s in 3 of 3, and the snow's column cv is above 1 at 30,000 s in 2 of 3 | the same reader with `--snow`, `col-cv` and the snow panel's cv |
| J10 | **the crowd presses on its cells and not on the stock**: `upt lim` above 0.5 in every window after 5,000 s in 3 of 3, the matter grid at 0.02 m²/s refilling a stripped cell slower than a leaf strips it | the column |

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
  cuticle's cap, and the entry says which the numbers point at. Or the island world's own
  failure, read first: a crowd that starved with its islands (J8 and J10 against `alive`)
  is D109's reading and not the mouth's.
- **J8 fails at 5,000 s with J9's first clause holding:** the crowd went with its matter into
  the soup. The water carries a body as it carries a stock, so a crowd that follows its
  island's spreading is the water's doing until `mean m/s` says otherwise. Read the crowd's
  spread (`x sd`, `cols`) against the field's, and say whether the crowd dispersed or
  merely drifted.
- **J9's second clause fails:** the crowd makes no patchiness of its own: its snow is as
  stirred as its water, and the "creatures create the concentration" reading of D109 is not
  in this world at this stirring.

## What the round does not ask

Whether a body can smell a corpse (rung B). Whether a mouth is a joint's reason to swim
(no body strokes, still). Whether a claw can be aimed: contact is whatever the water and
the crowd bring together.
