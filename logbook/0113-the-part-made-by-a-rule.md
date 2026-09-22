# The part made by a rule

*2026-09-22, evening (draft; the hashes and the screened threshold are filled in before the
launch commit). Written by the agent as the pre-registration of round 44, the module gene,
the first rung of the animal kit under D106 (the owner's ruling of the same evening). The
predictions are committed before the three seeds launch.*

## What it asks

Every body the campaign has grown was a fixed plan: development expanded the genome's
graph once, growth (D087) scaled what was there, and a lost part was lost. Round 45 will
let a part be killed. The owner's question of the evening was whether a body should be
able to grow parts back the way a plant grows leaves "based on its programming", and
whether the genome should decide the difference between the animals that cannot and the
plants that can. D106 says it is one gene per node, `Growth`, determinate or
indeterminate, with an indeterminate node's count a bounded rule on the body's reserve
rather than a number fixed at birth (`logbook/specs/module-gene-spec.md`).

So the round asks three things of a world whose founders are all determinate, as every
recorded body is. Whether the gene appears and is kept. Whether it is kept more by plants
than by jointed bodies, which is the plant–animal difference arising rather than being
written in. And whether an indeterminate body's size tracks its reserve, growing on plenty
and shedding on famine, so that the rule is doing what it says.

## The world

Round 43's (0111): `rounds/env-r42.ps1` through `run-farm.ps1` with `EVOSIM_WATER_HOLD=0`,
seeds 1 to 3, 30,000 s at dt 0.01, on the double-accounts base (`0c19f0d`) and the
module-gene build (commit and `coreHash`, `dynamicsHash`, `farmHash` to be read from each
manifest at launch and recorded in HANDOFF). The module tunables are `ModuleAddReserveSeconds`
(to be set from the ledger screen: a leaf's module must repay its tissue within a lifetime
at the campaign's light), `ModuleDropReserveSeconds`, `ModuleDropAfterSeconds` and
`ModuleGeneMutationChance` at the cell-type mutation rate; the values go in the launcher
`rounds/env-r44.ps1` and the header prints `modules add=.. drop=.. after=.. mut=..`. Every
founder is determinate, so at t = 0 the world is round 43's on a new base, and the gene
enters by mutation alone.

## Rules

A manifest reading `error` or `stopped` is censored and read at its last sample. A seed
past 2,500 bodies is stopped. Three seeds (D095). Three at once at 8 threads or one at a
time at 16, whichever the machine is free for; the round's wall is about eighty minutes
either way (0111's pace reading). The read is `scripts/reads/r44-read.py`, written before
the launch, and every clause below names its column.

## Predictions

| # | prediction | falsified by |
|---|---|---|
| H1 | **the gene appears and is kept**: `indet %` (the share of the living with any indeterminate node) reaches 10% by 15,000 s in 2 of 3 | the column |
| H2 | **plants keep it more than animals**: at 30,000 s the indeterminate share among photosynthetic bodies is above the share among jointed bodies in 3 of 3 (`ind` on lineage birth rows joined to `positions.jsonl`'s flags) | `lineage.jsonl`, `positions.jsonl` |
| H3 | **size tracks reserve**: among indeterminate bodies the mean part count rises through the founding boom and falls in the first window where `margin s` falls by a third, in 2 of 3 | `modules` over `indet %`; `margin s` |
| H4 | **the cap binds on the rule**: `mod refused` per window is larger as a share of `mod add` among indeterminate bodies than D101's `self stillb` is of births, in 2 of 3 | the differenced counts |
| H5 | **the economy is round 43's**: `alive` 250 to 650 at 5,000 s and 500 to 1,400 at 30,000 s; `upt lim` 40 to 95% and the snow 0.10 to 0.40 of the budget at 15,000 and 30,000 s; the rim quarter 0.12 to 0.40 and `cols` 0.85 to 1.05 of uniform; in 3 of 3 (0111: 401 to 447 and 838 to 1,334; 78 to 86%; 0.15 to 0.22; 0.20 to 0.31; 0.98 to 1.00) | the columns; `p3` over `alive` |
| H6 | **the books close**: `audit` under 0.1 J at every sample, the matter residual under 1e-5 units at 30,000 s (the double accounts; 0111 read 1e-04 on the float build), `diverged` 0, in 3 of 3 | `stats.jsonl`; the manifests |

## The two-sided readings

- **H1 and H2 hold:** the gene is worth carrying and the plant–animal difference has
  evolved rather than been written in. Round 45, the mouth, runs on this world with regrowth in it.
- **H1 fails:** a module never repays its tissue at this light, or the flip is too rare
  to found a line in 30,000 s. Read `mod add` against `mod refused` before the mutation
  rate: a rule that is never affordable is the ledger's error, and a rule that is refused
  by D101 every time is the placement's.
- **H2 fails with H1 held:** jointed bodies keep the gene as readily as leaves, so on this
  world a module is a module whatever it hangs from. The plant–animal difference waits
  for a world in which a lost part is a cost that differs by guild, which is round 45's.
- **H5 fails:** the gene changed the economy and not only bodies. Read `modules` against
  `alive` first: a world of larger bodies at the same budget is a smaller crowd by
  construction, and that is a reading rather than a fault.
- **H6 fails on the residual:** the module's corpse or its tissue price leaks; the
  drop path is the first suspect, since it is the one new transfer between books.

## What the round does not ask

Whether a part can be killed (round 45). Whether a module could be a different node than
its siblings (it cannot; a module is the node's own expansion). Thrust, still.
