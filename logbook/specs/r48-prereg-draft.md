# A stomach on a plant, and paying as you go

*2026-09-24. Written by the agent as the pre-registration of round 48, the round after
round 47 (0117, read in 0118): the four rulings the owner made on 0118's dissection of the
stomachs (D119 to D122), built the same morning by three subagents in worktrees, merged
before noon and screened before the predictions are committed (the launch section below).
The owner's words on the round: "lets proceed with your recommendations. i'm curious to see
how it all works out." This is the draft under `logbook/specs/`; it becomes `logbook/0119`
on a clean tree before the first seed runs, and the reader is written against it.*

## What it asks

Round 47 answered why the stomachs do not evolve, and the answer was four rules and not one
(0118's dissection, `logbook/specs/r47-read/tables.txt`). A stomach arrived by a leaf
turning into a stomach whole, and lost its whole income in one birth; the child's price was
a flat 100 J whatever the child, so a 12 J stomach child cost eight times its own tissue and
a body at the margin never saved it; senescence divided the intake as well as multiplying the
upkeep, so an eater's break-even rose as the square of its age; and eight in ten stomach
births were founders born at a random depth into thin water, dead in half a minute. The
owner's rulings take the four apart with generic rules, none of them written for a stomach:

- **D119.** A cell type arrives only as a bud: a small new part welded to a body that works,
  at the size a duplicated node is born at, and a part leaves by shrinking. No mutation
  turns a part into another type. The floors that refused a born-small part (the newborn
  mass floor, applied per part, refused every birth carrying a developed 3 cm part in rounds
  41 to 47, the bud build's finding) weigh a rigid group as one part.
- **D120.** Reproduction paid as it goes, as a gene beside the lump: a lineage in
  gestation banks a share of every positive net income in an account upkeep cannot draw on,
  and conceives from it. The child's overhead is proportional to the child above a floor
  (10 J at twice the child's tissue at birth, where every round from 41 charged 100 J flat),
  and the investment and newborn floors come down to let a lineage choose many small
  children.
- **D121.** Senescence wears upkeep alone. An old body pays more and earns what it earned.
- **D122.** Every founder lands at the richest cell of its food, depth included, and is
  born with 600 s of its own standing watts.

So the round asks one question of each rule, and one of the four together: does a stomach
on a plant found a line, and does an eater's line found from the pool, in the world round
47 already had.

## The world

Round 47's (0117's world section: the 22,000 m² tank at 45 m with the beach, 15,000 units
as islands stirred at 0.02, light by exposure, the offset priced, the trickle at 1/30 s
with one founder in ten from the stomach pool, the support cost, contact per part, the
mushroom reef at a quarter of the surface) with the four rulings on and nothing else
moved. The header's new tokens, read off a one-second run of the launcher
(`rounds/env-r48.ps1`; `scratch/r48-build/runs/r48cfg`, `configHash 708fed6e…`):
`floors rigid groups` after the reach; `founders in their food at its depth`;
`endowment 600 s`; `senescence 3000 s on upkeep`; `overhead 10 J` and, before the hash,
`overhead scale x2 floor 10 J · gestation mut=0.08 share=0.5-0.5 at 0.08`; and the growth
token's `minkg=0.05` and `invest=0.05-1`. The bud has no token: `Mutator.ChangeCellType` is
gone from the build and a bud is drawn at the cell-type rate (0.005 per node) in its place.
Three seeds, 30,000 s at dt 0.01, five threads each, two arms at a time, the runaway
ceiling 25,000, checkpoints every 2,500 s, the fields and the poses dumped. The snow's two
dials (remineralisation 0.0005 /s, the sink 0.002 m/s) are round 47's unless the snow
screen running beside this draft (`scratch/r48-snow`) says one of them should move; if it
does, the change is written here before the commit and the reading it rests on is cited.

## The ledger at the round's prices

The pool's four bodies against the round's config, at the config's clearance of ten
volumes a second, depth 5 m, the spent density 0.25 (`scratch/r48-build/ledger-pool.txt`;
round 47's column is 0117's table, since this build refuses round 47's config):

| body | break-even J/m³, r47 → r48 | lifetime at 0.5 J/m³, r48 | R0 at 1 J/m³ (first child), r47 → r48 | R0 at 2 (first child), r47 → r48 |
|---|---|---|---|---|
| the screens' inoculum, one part | 0.44 → 0.44 | 826 s | 2 (410 s) → 15 (179 s) | 12 (122 s) → 133 (62 s) |
| seed 1's 1182, stomach and link | 0.17 → 0.27 | 2,502 s | 2 (847 s) → 10 (682 s) | 16 (262 s) → 78 (247 s) |
| seed 3's 1513, stomach and link | 0.29 → 0.35 | 1,900 s | 3 (338 s) → 22 (199 s) | 15 (123 s) → 162 (76 s) |
| seed 3's 2156, stomach and link | 0 → 0.02 | 3,526 s | 2 (568 s) → 10 (513 s) | 12 (269 s) → 48 (231 s) |

None breeds at 0.5 J/m³ on either build, and the break-even barely moves: the rulings do
not make a stomach cheaper to run. What they change is what a founder does with water it
can live in. At 1 J/m³ the same four bodies have five to ten times the R0 they had at round
47's prices and their first child comes sooner, because a 5 to 12 J child's overhead is 10
to 24 J where it was 100, and the intake no longer wears with age, so a lifetime at 0.5 J/m³
runs to 826 to 3,526 s where 0117 read 420 to 3,400. The ledger is a lump reading; a body in
gestation banks half its net and breeds from the account, which the ledger does not model.
The bet is still where a founder lands, and D122 now places it at the richest cell of its
column rather than a drawn depth in it.

## Predictions

Round 47's per-seed baselines are read from the same seed of round 47 wherever a clause
says "round 47's same seed" (`runs/r47-s1..3`, the last stats row: alive 2,619 / 5,602 /
2,501; mean age 1,459 / 1,891 / 1,778 s; mean birth investment 0.82 / 0.48 / 0.78).

| # | prediction | falsified by |
|---|---|---|
| M1 | **buds are born**: at least 50 birth rows carrying `bud` by 30,000 s in 3 of 3 (0.005 per node over about three nodes a genome and 16,000 to 46,000 births a seed in round 47 is 200 to 700 bud births; 50 is a floor under a quarter of the smallest) | `lineage.jsonl` |
| M2 | **a bud is born expressed**: `budx` at 1 or more on at least half the bud rows in 3 of 3 (the floors weigh rigid groups, so nothing prices a welded 3 cm part out of the newborn; what can still refuse it is the self-overlap stillbirth, `self stillb`, and `MaxParts`) | the `bud` rows' `budx` |
| M3 | **a stomach on a plant founds a line**: a connected clade whose first member's row carries an absorptive bud (`bud` naming `absorptive`, the row `abs` 1 and `pho` 1) has ten or more living members at 30,000 s in 1 of 3 or more (round 47 read 112 mixotrophs across three seeds, the form nearest replacement at R0 0.98, and a longest absorptive chain of 4) | the parent walk over `lineage.jsonl` (`r48-read.py`, the bud root) |
| G1 | **gestation enters**: `gestationBirths` (births paid from an account) above 0 at 30,000 s in 3 of 3 (the mode flips at 0.08 per reproduction-trait mutation; a birth row's `gm` is the child's own mode, so the parents' mode is read from the parent's row) | `stats.jsonl`; the birth rows' `gm` |
| G2 | **gestation is not selected out**: the share of children (founders left out, their mode being the founding draw) with `gm` 1 in the last 5,000 s is at least the share in the 5,000 s after the floor closes (3,000 to 8,000 s) in 2 of 3, a weak clause since the share starts near 0; the strong reading printed beside it is children per dead body by the parent's mode, gestating against lump, in the same window | the birth and death rows by `gm` |
| O1 | **children are born smaller**: the mean birth investment at 30,000 s below round 47's same seed in 3 of 3 (the floor at 0.05 and the fee proportional, so a lineage that halves its child halves its fee) | `stats.jsonl`'s `meanBirthInvestment` |
| O2 | **the count rises and the ceiling holds**: `alive` at 30,000 s above round 47's same seed in 2 of 3, and under the runaway ceiling of 25,000 at every sample in 3 of 3 (the closed matter bounds the count: 15,000 units cannot build 25,000 bodies at the smallest child's price, which the screen below measures) | `stats.jsonl`; the manifests' ending |
| S1 | **the old live longer**: `meanAge` at 30,000 s above round 47's same seed in 3 of 3 (an old body earns what it earned and pays double at 3,000 s, so its reserve drains slower) | `stats.jsonl` |
| S2 | **an eater's window opens**: the median age at death of stomach children (rows with `abs` 1, `pho` 0, kind not founder) above 600 s in 2 of 3 (round 47's stomach children died at the margin as wear squared closed it near 1,000 s; the ledger's lifetime at 0.5 J/m³ is 826 s for the one-part body) | the death rows joined to the birth rows |
| F1 | **a pool stomach lives**: the median age at death of the pool founders that have died above 300 s in 3 of 3 (round 47 read 46, 24 and 42 s; founders alive at the read are counted beside the median and left out of it) | `lineage.jsonl` (`src: pool`) |
| F2 | **a pool stomach breeds**: at least one pool founder has a child in 3 of 3 (round 47: 1 of 3) | the birth rows whose parent is a pool founder |
| F3 | **an eater's line founds**: ten or more living bodies rooted in pool founders at 30,000 s in 1 of 3 or more (round 47: 0 of 3) | the parent walk |
| F4 | **founders land in their food**: every pool founder's first `absorptive.jsonl` row (its own reading of the snow at its body, at its first metabolic step) reads at least its column's mean snow (the column sum in the dump at or before its birth over the column's water depth, rock removed), in 3 of 3, and the median of those first readings is printed against round 47's 0.12 J/m³ (the rule's own check: the richest cell of a column is at or above the column's mean by construction, and a founder placed by the old depth draw read about half the mean). No record holds the snow by layer, so the layer itself cannot be read; the body's own sample stands in for it, and it is the density the body was fed at, which a crowded cell lowers, so the clause leans towards failing. The reader reproduces round 47's 0.12 from `r47-s1`'s pool founders (median first reading 0.120 against a column mean of 0.30, 15 of 96 at or over the mean), which is the check that the join is right | `absorptive.jsonl` joined to `lineage.jsonl` (`src: pool`), against `fields/*.snow-columns.f32` and `bed.f32` |
| E1 | **the endowment is spent and not hoarded**: a reading, not a clause: the founder rows' `endow` (median and range; the smoke read a median of 83 J over 4.5 to 714 J, the range being the bodies' standing watts over 600 s) against the founders' median age at death and the share that bred, for all founders and for the pool's | `lineage.jsonl` |
| B1 | **the books close and nothing breaks**: `audit` under 0.1 J, the matter residual under 1e-5 units (the gestation accounts are in both books), `diverged` 0, in 3 of 3; `diverged` 0 is also the test of the 50 g newborn floor in the solver | `stats.jsonl`; the manifests; `diverged/` |
| R1 | **the reef readings carry**: round 47's L5 and L6 (the tables' snow, the shade in the crowd) printed as readings against round 47's same seed, no clause | the snow dumps and `positions.jsonl` against `run.json`'s `reefs` |
| P1 | **the pace holds**: the footer's whole-run pace at or above 1x real time in 3 of 3 (round 47 read 1.4x on seed 2 at two arms; smaller and more bodies is the risk) | the footers |

## The two-sided readings

- **M1 holds and M2 fails.** Buds are drawn and not born. Read what refused them: the
  self-overlap stillbirth (`self stillb` against `stillb`), `MaxParts` on a bushy parent, or
  a volume floor the rigid-group rule does not cover; the bud build's cause table
  (`scratch/r48-bud`) names the four routes a gene went unexpressed in round 47.
- **M2 holds and M3 fails.** Buds are born and no stomach-on-a-plant line lasts. Read the
  bud rows' children per dead body against their parents' clades, and the snow at the
  budded bodies' places: a bud that earns nothing where its plant lives is a placing
  question, and a bud that earns and does not breed is a price question (the fee, the
  margin).
- **G1 holds and G2 fails.** Gestation enters and is selected out. Read the two modes'
  children per dead body, and whether the gestating lineages' accounts were buried unspent
  (the corpse's joules against `gestationJoulesHeld` before the death).
- **O1 holds and O2 fails on the count.** Smaller children and no more bodies: the matter
  or the light binds before the fee did. Read `upt lim` and the plateau of the screen
  against the run.
- **S1 fails.** The old do not live longer. Read the death rows' ages by guild: if the
  plants' ages did not move, upkeep wear alone still ends a leaf near where both wears did,
  which says the intake wear was not the binding term for a leaf; the stomachs' ages (S2)
  are the term the ruling was for.
- **F1 holds and F2 fails.** The endowed founder lives on its endowment and saves nothing
  after it: read the snow at its cell (F4's join) and the ledger's break-even; a founder in
  water over 1 J/m³ that never breeds is a ledger question, one in thinner water is a placing
  question.
- **F3 holds.** The first eater's line since round 41. Read whether it is rooted in a pool
  founder or a budded plant (M3), and whether the plants' count moves under it.
- **B1 fails on `diverged`.** A 50 g link threw. The dumps name the body; the round is read
  with the count, and the floor goes back to 0.5 kg for the seed after.
- **The reader is `scripts/reads/r48-read.py`**, round 47's reader with the new rows and
  fields (`bud`, `budx`, `gm`, `gs`, `endow`; `gestationBirths`, `gestatedJoules`,
  `gestationJoulesHeld`), written by an Opus subagent against this draft and checked on the
  smoke and on a round 47 run before the launch; its first read of the smoke is in the
  smoke section, and it is what confirmed the names above.

## What the round does not ask

Whether a physical bud (a child grown as a subtree of its parent and detached at a gene's
size, D120's second cut) would do what the account does. Whether a reactive thrust term
would let a stroke move a body (the swim probe, 0118's movement paragraph; a proposal for
the round after). Whether the snow's dials are right (the screen beside this draft is a
reading of them, not a round). Whether the endowment's 600 s is the right number (E1 is a
reading of what it buys).

## The screen and the smoke

**The smoke** (`r48smoke`, `configHash 147baf8a…`, 600 s of the launcher at dt 0.02, seed
1, four threads beside three other farm runs, so its pace is not a reading; under
`scratch/r48-build/runs`). Every mechanism the round asks about shows in ten minutes of
wall clock. Both books closed at 600 s, the audit at 8.7e-7 J and the matter residual at
−1.2e-7 units, no divergence. Seven births carried a bud (two consumer, two link, one
photosynthetic, one neural, one absorptive) and every one of the seven was born with it
built (`budx` 1), the absorptive one on a leaf (`abs` 1, `pho` 1): the first stomach on a
plant born small. Eighty-two births were paid from a gestation account, 4,522 J had been
banked and 2,583 J were held in accounts at 600 s, and 150 birth rows carried the gestation
mode (`gm` 1) with the share mostly at the founders' 0.5 and already walking (0.41 to
0.51). Every one of the fifty floor founders carried an endowment (median 83 J, 4.5 to
714 J, the range being the bodies' standing watts over 600 s), booked as an influx of
90.9 units against the seeded 15,000, which is where a closed world's `mat in` now comes
from. Fifteen stillbirths, all self-overlap.

What the smoke also says, and what makes the screen necessary: the crowd founds about ten
times faster than round 47's. Fifty founders became 963 living bodies and 954 births by
600 s, where round 47's smoke (`r47smoke2`) read 96 alive and 67 births, the count doubling
about every 100 s from 300 s; the mean birth investment read 0.53 with children born at a
median 0.22 of their adult body and a third of them under a tenth (the smallest at 0.007),
which the 0.05 kg floor admits. A child at a tenth of a leaf for a fee of 10 J is the ruling
working as intended; whether the world's 15,000 units bound the count under the runaway
ceiling, or the ceiling ends every seed as a runaway before 30,000 s, is the plateau
screen's question and not the smoke's.

**The plateau screen** (`r48plat-s2`, seed 2, the launcher at dt 0.02 to 15,000 s, four
threads). *Running at the time of writing; its reading is written here before the commit:
the count at every 1,000 s, whether the ceiling fired, `upt lim`, the mean investment, and
the median snow at the eaters.*

## The launch

*To be filled: the commit, the suites' counts, the fixtures, the exe, the wall.*
