# The module gene: round 44's build

*Design spec, 2026-09-22 evening, written by the agent from D106 the hour it was ruled. The
build lands after the double-accounts change (`scratch/double-accounts`, in progress) so
that one Core change follows another. The implementation notes at the end are the builder's
to complete; the rules above them are the owner's.*

## What it is

One gene per node of the genome's recursive graph, `Growth: Determinate | Indeterminate`.
Determinate is every recorded body: the node's count is fixed when development expands the
graph, a lost part is gone, and D087's growth only scales what exists. Indeterminate makes
the node's count a bounded rule instead of a fixed number: the body adds another module of
the node while its reserve stands above a threshold, paid at the tissue price like all
growth; drops one after a long enough starvation; and regrows a lost one when affordable.
Nothing else about development changes. Format 7 carries it, together with the four
attributes of round 45 (attack, intake, protection, toughness) at their zero defaults, so
that the stored genomes are re-extracted once.

## The rules

1. **What a module is.** The node's expansion as development would make it: the part, its
   edge from the parent, and the subtree the recursive graph hangs from it, developed with
   the same transform and scale as its siblings. The genome already says how a node's copies
   are placed (the edge's transform and its recursive limit); the module rule reuses that
   placement and only changes how many copies there are at a moment.
2. **The count.** Development expands an indeterminate node to its genome minimum, which
   is the recursive limit as today (a body is born with the plan it would have had). From
   then on, at every growth step (`GrowthStepSeconds`), a body with an indeterminate node
   whose reserve exceeds `ModuleAddReserveSeconds` of its own upkeep adds one module of
   that node, one per body per growth step, paying the module's tissue at the tissue price
   from the reserve and refusing if the reserve cannot pay in full. The ceiling is the
   node's `MaxModules` (a per-node gene, drawn at the recursive limit and mutable upward
   to `MaxParts`), and the body's total is bounded by `MaxParts` and `MaxDepth` as today.
3. **Dropping.** A body whose reserve has been below `ModuleDropReserveSeconds` of upkeep
   for `ModuleDropAfterSeconds` drops one module of an indeterminate node, the last added,
   which leaves the body as a corpse (D106 item 1's corpse path, the part's tissue and
   pro-rata reserve share), so the matter is not lost. One per body per growth step.
4. **Regrowth.** A lost module (round 45's kill, or a drop) is a count below what rule 2
   would keep; rule 2 restores it the same way it adds. A determinate part lost is not
   restored.
5. **The bounds that stop a bush.** D099's silhouette cap stays (a body earns its hull's
   surface over four), D101 stays (an added module that overlaps a non-adjacent part
   deeper than the fraction is refused rather than added; the body keeps its reserve and
   tries again next step), and `MaxParts` 16 and `MaxDepth` 8 stay. A refused addition
   counts in `moduleAddsRefused`.
6. **Founders and mutation.** Founders draw `Growth` determinate at every node, so a world
   starts as the record is. The mutator flips the gene at the cell-type mutation rate
   (`EVOSIM_CELLTYPE_MUT`'s neighbour, its own tunable `ModuleGeneMutationChance`) and
   moves `MaxModules` as a scalar. The four round-45 attributes are in the genome at zero
   and the mutator does not touch them until round 45's build turns their mutation on.
7. **The body rebuilds.** A module added or dropped rebuilds the articulated body on the
   farm the way a growth resize does, at the growth step, keeping the root's pose and
   velocity and every surviving part's joint state; the new part starts at rest relative
   to its parent at the birth fraction and grows as a newborn's parts do.
8. **What is recorded.** The report gains `modules` (living indeterminate modules beyond
   the genome minimum, summed), `mod add`, `mod drop`, `mod refused` per window, and
   `indet %` (the share of living bodies with any indeterminate node); lineage birth rows
   gain `ind` (the count of indeterminate nodes); `stats.jsonl` the four cumulative
   counters; snapshots carry the genome as always, and `positions.jsonl` is unchanged.
   The checkpoint carries each body's per-node module counts, and its format version
   moves.

## The tunables

`ModuleAddReserveSeconds` (`EVOSIM_MODULE_ADD`), `ModuleDropReserveSeconds`
(`EVOSIM_MODULE_DROP`), `ModuleDropAfterSeconds` (`EVOSIM_MODULE_DROP_AFTER`),
`ModuleGeneMutationChance` (`EVOSIM_MODULE_MUT`). All in `RunConfig` and its hash, all
refusing every earlier `config.json` per §9, the header printing `modules add=.. drop=..
after=.. mut=..`. The ledger (`scripts/ledger.ps1`) screens the add threshold before the
round: a leaf's module must repay its tissue within a lifetime at the campaign's light,
or no plant will ever add one.

## The acceptance

- **Development is unchanged for a determinate genome**: every snapshot genome of round
  43, re-extracted to format 7, develops to the same phenotype (a test over the last
  snapshot of `r43-s1`, part for part).
- **Replay of the record**: a round 43 config is refused (the tunables), as §9 says; a
  round 43 world re-run on the new build with the module tunables at their defaults and
  every founder determinate writes the same first thirty rows as round 43 seed 1
  (`scratch/checkpoint/regress.py` against `runs/r43-s1`), because a determinate world
  is the recorded world.
- **The rule works alone**: in Core, one indeterminate leaf under a constant light adds
  modules to `MaxModules` and no further, at the tissue price, the audit and the matter
  residual closed at every step; starved, it drops them one at a time and the corpses
  carry the matter; a dropped module regrows when fed.
- **The bounds bind**: an indeterminate node whose modules would overlap is refused by
  D101's test and `moduleAddsRefused` counts it; a body at `MaxParts` adds nothing.
- **The farm rebuilds**: a 600 s farm run with one inoculated indeterminate plant shows
  its part count rising in `positions.jsonl`/the state stream and no divergence; the
  checkpoint restore-and-continue identity (`ckA`/`ckB`) holds with modules present.
- **Suites green**: Core, Dynamics, Farm; the reflection tests catch the new tunables.

## Round 44's predictions, to pre-register on the build

Round 43's world (the launcher `rounds/env-r42.ps1`, `EVOSIM_WATER_HOLD=0`), three seeds,
30,000 s, every founder determinate. H1: the module gene appears by mutation and an
indeterminate line reaches 10% of the living by 15,000 s in 2 of 3. H2: the indeterminate
share is higher among photosynthetic bodies than among jointed ones at 30,000 s in 3 of 3
(the plant–animal difference evolved). H3: body size tracks reserve: the mean parts of
indeterminate bodies rise from founding and fall in a drought window, in 2 of 3. H4: the
silhouette cap binds on indeterminate bodies more often than on determinate ones. H5: the
crowd, the treadmill, the larder and the spread stay within round 43's readings (G1, G4,
G7), so the gene changed bodies and not the economy. H6: `diverged` 0.

## Implementation notes (the builder's)

To be completed by the build: the genome field and `GenomeJson` format 7 with the four
attributes at zero; `Developer` taking a per-node count from the body rather than the
recursive limit; the rebuild path in `Evosim.Dynamics`; the checkpoint's per-node counts;
the report columns; the tests named above.
