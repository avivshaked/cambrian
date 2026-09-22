# The part made by a rule

*2026-09-22, evening. Written by the agent as the pre-registration of round 44, the module
gene, the first rung of the animal kit under D106 (the owner's ruling of the same evening);
drafted before the build and completed with the build's hashes and the ledger's screen at
the launch commit. The predictions are committed before the three seeds launch.*

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
module-gene build (`39dcf3d`; `coreHash da06878f…`, `dynamicsHash 880883c5…`, `farmHash
8299b231…` as the fixture recording `r44fix-s4` read them, and each seed's manifest is the
record). The launcher is `rounds/env-r44.ps1`: `ModuleAddReserveSeconds` 300 s,
`ModuleDropReserveSeconds` 50 s, `ModuleDropAfterSeconds` 100 s, `ModuleGeneMutationChance`
0.005, the cell-type rate; the header prints `modules add=300 drop=50 after=100 mut=0.005`.
The add threshold was screened with the ledger (`logbook/specs/r44-read/ledger-screen.txt`,
on a one-leaf genome with a self-edge from the build's own smoke,
`ledger-leaf-mgC-539.json`): a leaf's module costs 70 J of tissue and earns 2.5 W net at
the surface, so it repays in 28 s there, about 80 s at 5 m by the body's own light
ratio, and about 1,000 s at 12 m, where a leaf's lifetime is 724 s; a module is affordable
wherever a leaf is. The threshold is therefore a choice about when a body invests rather
than whether it can: 300 s of upkeep is 114 J for that leaf, about a child's price with
the overhead, and the middle of the breeding margin's range (0 to 600 s), so a body adds
a module at about the reserve at which it could breed. The build's smoke at 300 s added
nothing in 600 s of founding (`mgB`), and at 5 s added eight modules with 2,790 refusals
for a reserve short of the tissue (`mgC`, 2,000 s); a 30,000 s world's bodies hold
several hundred seconds of reserve (round 40: about 190 J a body), so the rule is
expected to fire on a mature line and not on a founder. Every founder is determinate,
so at t = 0 the world is round 43's on a new base, and the gene enters by mutation alone.

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
