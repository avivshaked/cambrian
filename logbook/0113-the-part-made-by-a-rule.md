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

## Read at 30,000 s, two seeds of three, and seed 1 at 22,370 s

*2026-09-22, night. Seeds 2 and 3 ended at their budget in 75 and 78 minutes. Seed 1
ended `status error` at 22,370 s after 120 minutes, censored and read at its last sample
by the rule above; the cause is below. The clause readings are
`logbook/specs/r44-read/clauses.tsv`, from `scripts/reads/r44-read.py`; the pictures are
drawn from the last snapshot of each seed.*

| # | held in | reading |
|---|---|---|
| H1 | 2 of 3 | `indet %` reached 95% in seed 2 by 15,000 s and 25% in seed 3 by 11,850 s; seed 1 peaked at 9.6% by 15,000 s and read 60% at its end |
| H2 | 3 of 3 | photosynthetic against jointed: 60 against 56% (seed 1, 983 against 64 bodies), 98 against 97%, 14 against 11% |
| H3 | 0 of 3 | unreadable: `margin s` never fell by a third in any seed after founding (seed 2 rose from 109 to 178 s, seeds 1 and 3 held near 230 and 300 s), so the clause's window never opened |
| H4 | 1 of 3 | refusals per add 22, 704 and 1,255 over the run; the clause fails a window in which nothing was added (seeds 1 and 2 to 5,000 s) and holds every window after |
| H5 | 2 of 3 | seeds 2 and 3 inside every band (1,152 and 793 alive; `upt lim` 89 and 81%; rim quarter 0.22 and 0.28; `cols` 1.01 and 0.98 of uniform); **seed 1 outside three at its end**: `upt lim` 23%, the rim quarter 0.48, `cols` 0.85 |
| H6 | 3 of 3 | the energy residual under 1e-5 J at every sample; the matter residual 6e-07 to 1.2e-06 units, a hundredth of 0111's float reading; nothing diverged |

**What it looked like.** Seed 2 from the side at 30,000 s
(`logbook/images/r44-s2-t30000-recon-side.png`) is round 43's shower of leaves through the
top fifteen metres, a few larger green bodies among them, and from above
(`r44-s2-t30000-recon-top.png`) the disc is filled edge to centre. Seed 3
(`r44-s3-t30000-recon-side.png`) is a sparser crowd of smaller jointed bodies down to about
twenty metres. Seed 1 at 22,300 s (`r44-s1-t22300-recon-side.png`, `-top.png`) is a
different world: curved chains of three to five parts, the modules, through the whole
column to the bed, and from above a crowd thickening toward the rim on one side. Every
picture is drawn from a snapshot at adult size; the pose is the recorded one where the
label says so.

**The gene is worth carrying, and where it is carried it is the body.** H1 held twice
and nearly three times. Seed 2 turned almost entirely indeterminate within 10,000 s of
the first flip and stayed there. Seed 3 took the gene to a quarter of the crowd and let
it fall back to 14% by the end. Seed 1 came to it late and then quickly, 3% at 15,000 s
and 60% at 22,370 s. The adds say what the share does not: 1,366 modules were added in
seed 2, 1,550 in seed 1, and 77 in seed 3, so seed 3's carriers mostly never paid for
one. Seed 2 also dropped 198, the only seed in which the shedding rule fired
more than a handful of times.

**H2 held by a hair and is not the plant–animal difference.** The margins are one to
four points in every seed. In this world nearly every jointed body is also
photosynthetic, so the two shares are read on overlapping crowds; the clause was written
as if the guilds were disjoint, and they are not. What it does say is that the gene is
not kept less by jointed bodies, which is the two-sided reading's second branch: on this
world a module is a module whatever it hangs from. In seed 1 the jointed fell from 483
at 10,000 s to 64 at the end, the fade round 43 saw in its seed 2, so its comparison is
on a sixty-body sample.

**The rule is refused far more often than it fires, and the read cannot say why.** H4's
number is refusals per add: 22 in seed 1, 704 in seed 2, 1,255 in seed 3. The counter
merges the two refusals the rule makes, a candidate body D101 or the part cap would
prune and a reserve short of the tissue, so which sieve is which is not measurable from
the run; the spec says both and a per-reason count is the instrument the next build
should carry. H3 is unreadable rather than failed: the famine its window waits for never
came, because `margin s` rose or held in every seed. Whether size tracks reserve is still
open, and the clause should be written on the reserve itself rather than on a fall in it.

**The economy is round 43's until the modules are many.** Seeds 2 and 3 sit inside every
H5 band, and seed 2 with 603 standing modules on 1,152 bodies is inside them too. Seed 1
is not. From 20,000 s its modules went from 199 to 787 while its crowd held near 1,000,
its uptake-limited share fell from 66% to 23%, its rim quarter rose from 0.28 to 0.48
and its `cols` fell to 0.85: the bodies went deep and toward the glass. My reading, as
inference: a chain of modules is a longer body with a longer response time, and D090's
term keeps a body on its parcel only as far as its lag allows, so the centrifuge that the
one-part world does not show comes back by degree with the part count. The picture from
above shows the crust beginning. That is the reading H5's two-sided entry named, a world
of larger bodies at the same budget, with the addition that larger bodies in this water
also drift outward.

**Why seed 1 slowed, and why it ended.** Its pace fell from 10x real time at 10,000 s to
0.65x in its last window with physics 99% of the wall. The solver's cost per body-step
rose from 0.0017 to 0.0129 ms, 7.6-fold, while links per body-step rose from 2.05 to
2.95 and `ovl/body` from 0.026 to 0.82, 99% of it held; seed 3 at 3.44 links a body
cost 0.0029 ms. So the cost followed the overlap pairs and not the link count, which
points at the contact pass, my reading, and the profile from a checkpoint is what
decides it; round 44 wrote no checkpoints, and round 45's launcher records one every
2,500 s for that reason. The end was creature 7417's centre of mass 3 cm below the bed
in a 45 m world: the farm checked the root's height, which was inside, and `World.Observe`
reads the centre and throws on it. From `2771bf0` the farm's check reads the centre and
such a body dies as a counted `Diverged` death; the seed's rows to 22,370 s stand.

**The books.** H6 held in three of three, and the matter residual's reading of the order
of 1e-06 units is the double accounts' first round: round 43 read 0.9 to 1.9e-04 on the
float build.

**What the round did not settle.** Whether size tracks reserve (H3's window never
opened). Which refusal binds (one counter for two reasons). Whether a chain's contact
sphere should be its hull's or its parts', which decides both seed 1's pace and its
crust; that is a world rule, and the owner's.
