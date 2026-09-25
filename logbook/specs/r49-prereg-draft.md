# Round 49's predictions, drafted before the owner rules on the endowment

*Ruled the same afternoon, after this draft was written. Question 1 went to the owner's own
alternative, not to (a), (b) or (c): every founder starts with at most a fraction of its own
breeding threshold, purse and endowment together, after its first growth, and the fraction is
a tunable, 0.9 in round 49. The blocks EA, EB and EC below are therefore replaced by one block
for the cap when the entry is written, from the build's own numbers. The wear stays on upkeep,
and the snow's stirring is unchanged. The cap's arithmetic for the one-part stomach is
`scripts/reads/r49-prereg/gift_cap.py`.*

*2026-09-25. A draft of round 49's pre-registration, kept in `logbook/specs/` until the owner
rules. The logbook entry is written from it once the rulings are in. The agent wrote it on Opus
5.5, as a subagent of the session. Its sources are round 48's read (0120), the proposal for
round 49 (`fable-propose-round-49.md`) and the build on the branch `r49-record-film` at
7739745. Nothing in it has run. Every number is round 48's, round 47's, the ledger's or
arithmetic from the code, and each says which. A number I guessed says so.*

## What it asks

Round 48 changed four rules so that an eater of the snow could found a line, and none did
(0120). The endowment, 600 s of a founder's own standing costs given to it as it lands, paid
for the cheapest stomach's child in its first half-second. The founder then starved on the
water it had landed in. A second rule places each founder in the richest cell of its column,
and it could not be checked. The one reading of the water came after the founder had emptied
its own cell. And gestation, paying for a child as you go, was pushed down by selection, while
nobody had read what the gestating bodies died holding.

Round 49 asks three things of that world. The first is whether a stomach from the pool can
breed on what it earns, once the owner has ruled what the endowment may pay for. The second is
whether the placing rule puts founders where it says. A founder's birth row now records its
food at the instant it lands, beside its column's mean, which is the one reading that can
answer. The third is where gestation's savings go when their owner dies.

Beside those, the round checks four pieces of new code that change no price. The contact and
damage senses now read the last step (D123). A bitten body is rebuilt on the step that bit it.
A run's files are smaller. And a film window, a stretch of a run replayed by the farm for the
camera, should now copy the run bit for bit.

The owner has three questions to rule on (`fable-propose-round-49.md`). They are whether the
endowment may pay only for upkeep, whether the wear of age on upkeep stays, and whether the
snow's stirring changes. So the predictions come in two layers. The first holds whatever is
ruled. The second has one block for each option, and the blocks not chosen are deleted at the
ruling, before the entry is committed.

## The world

Round 48's world (0119's world section), with the owner's three rulings and these changes of
code, all on `r49-record-film`:

- **D123.** The contact sense reads whether a part touched another body's part when the last
  metabolic step closed. A metabolic step is the half-second in which bodies earn, pay, grow
  and breed. The damage sense reads the share of its health a part lost on that step. In
  rounds 45 to 48 both kept a history from the body's last change of shape. There is no switch
  for this, so every seed of round 49 is a new realisation and none replays round 48's.
- **The bite rebuild.** A body that loses a part is rebuilt in the solver on the step that bit
  it. Until now it waited up to 10 s for the next growth step. A change of plan also carries the
  surviving parts' sense records onto the new plan. Round 48 lost no part to a bite in any seed.
  So in this world the new code mostly touches the 118 to 389 module changes a seed made, whose
  surviving parts now keep their sense records.
- **Two instruments.** A death row carries `ga`, the gestation account the body died holding,
  and `res`, the reserve of a body that died solvent, each only above zero. A founder's birth
  row carries the snow's edible density in the cell it lands in and the mean over its column's
  live water, `fsnow` and `fcol`. A second pair is the same reading of the dissolved matter a
  leaf takes up (DESIGN §9 on the branch).
- **Smaller files.** Record format 2 writes each genome once, snapshots of slim rows, gzipped
  positions, a binary pose stream in place of the JSON poses, and compressed checkpoints. Every
  seed writes a checkpoint every 500 s.

The run shape is three seeds, 30,000 s each at dt 0.01. They run one at a time at ten threads,
under the machine's current ruling of one heavy job at a time at about a third of the machine.
The runaway ceiling is 25,000 bodies, and the fields are dumped as in round 48. Unless a ruling
changes them, the dials are round 48's. The snow is stirred at 0.02 m²/s, sinks at 0.002 m/s,
and turns back into dissolved matter at 0.0005 of itself a second. A child's overhead is twice
its tissue above a 50 J floor. The endowment is 600 s. The trickle brings a founder every 30 s,
and one in ten comes from the pool of four stomach bodies (`inocula/pool-r47/`).

## What a pool stomach needs to breed, by the ledger and by the code

The ledger (`scripts/ledger.ps1`) is a calculator of one body's energy account, alone. I ran it
for the pool's one-part stomach on round 48's config. It ran at the config's clearance of 10, a
depth of 5 m and a spent density of 0.25 units/m³. The genome is
`inocula/pool-r47/stomach-r45s1-100.json` and the config is seed 1's. This is the body that bred
at landing 54 times in 54 in round 48. Its tissue is 44.952 J, its standing cost 0.3596 W, and
a child costs it 68.67 J (0.6867 units).

Every density in the table is the density in the stomach's own cell. The stomach empties about
half of that cell each half-second step, and the water round it refills it (0120's F4
section). So the ledger's break-even of 0.444 J/m³ is a density at the mouth. Set against the
water round the mouth, it overstates what the stomach eats by four to twenty times. CLAUDE.md's
gotcha "a mouth is priced at the cell it is emptying" says why. Look at where the first child
becomes possible.

| density at the mouth, J/m³ | net W at birth | lifetime | R0 | first child |
|---|---|---|---|---|
| 0.3 | −0.117 | 31.5 s | 0 | never |
| 0.44 | −0.004 | 222 s | 0 | never |
| 0.5 | +0.045 | 826 s | 0 | never |
| 0.75 | +0.247 | 3,487 s | 2 | 463 s |
| 1 | +0.45 | 4,985 s | 11 | 227.5 s |
| 2 | +1.259 | 12,119 s | 94 | 78 s |

The ledger starts a newborn at its full size with a fifth of its share as reserve, 3.7 J. A
founder starts otherwise, and the ledger knows neither the founder's purse nor its endowment. So
the next table is my own arithmetic, from `World.Step`'s order (upkeep, then growth, then
breeding) and the ledger's figures. It is not a ledger reading.

The one-part stomach lands at a third of its adult size (`bf` 0.332). It carries the floor's
purse of 200 J scaled by that, 66.45 J, and under round 48's rule an endowment of 71.69 J. On
its first step it grows to its adult body for 30.02 J. Its gate, the reserve it must hold
before it breeds, is then 100.42 J. That is 18.67 J of investment, the 50 J overhead floor, and
its margin of 88.28 s of its standing cost. The table compares the three options for a stomach
held at one density at its mouth.

| option | reserve after it grows | account paying its upkeep | still to earn for a child | density held at the mouth to breed at all | first child at 1 and 2 J/m³ | death at 0.2 J/m³ |
|---|---|---|---|---|---|---|
| (a) keep | 108.1 J | none | 0 | any | 0.5 s and 0.5 s | 189.5 s, after its child |
| (b) upkeep only | 36.4 J | 71.7 J | 64.0 J | 0.42 J/m³ | 72.5 s and 36.5 s | 478 s, no child |
| (c) drop | 36.4 J | none | 64.0 J | 0.61 J/m³ | 149.5 s and 52 s | 175.5 s, no child |

The arithmetic can be held against two rounds. Under (a) it puts the stomach's death at 189.5 s
at 0.2 J/m³. Round 48's one-part pool stomachs died at a median of 186, 168 and 179 s by seed.
Over their first 180 s they fed at a median of 0.17 to 0.20 J/m³. Option (c) is round 47's
founder with a lower overhead and no wear on its intake, neither of which touches a body that
never breeds. There the arithmetic puts death at 128 s at 0.1 J/m³. Round 47's one-part pool
stomachs died at a median of 120.5 to 139 s, and their first readings sat near 0.12 J/m³.

Under (b) the endowment carries the upkeep for about three minutes. So the stomach saves its
whole food income, and it breeds at a mouth density just under the ledger's break-even. I asked
round 48's own mouths how many would have got there. Only 4 of the 54 one-part pool stomachs
ate enough on average over their first 180 s to save 64 J (2, 0 and 2 by seed). That is
inference, and it may err either way. Each of those founders had a child eating beside it in
the same water. Each also died near 180 s, so the average covers a shorter life than (b) would
give it.

The trickle's random founders bred at landing too. In round 48, 62, 26 and 65 of them had a
child within a second of landing, 7.6 to 8.0% of the trickle's founders. Their rows carry the
birth fraction, the margin and the endowment. From those, with tissue at 500 J/m³ for every cell
type, the purse alone would have paid for 1 to 9 of them a seed. That estimate is rough. It
has to assume an upkeep per cubic metre, which no row carries. As the assumption moves from 2.5
to 4 W/m³, it reproduces 26 to 59 of seed 1's 62. Round 47, with no endowment and a 100 J
overhead, had 0, 2 and 0 such founders.

## Predictions that hold whatever is ruled

Round 48's numbers are the bars, read from `runs/r48-s1..3`. Round 48's seed 2 stopped at
13,700 s, and its numbers are at its last sample, 13,690 s. D123 makes every seed a new
realisation, so no clause compares a seed with round 48's same seed. Each sets a bar from round
48's range. A threshold marked as a guess is a guess.

| # | prediction | falsified by |
|---|---|---|
| W1 | **the depth rule does what it says**: every founder that eats the snow and not the dissolved matter (birth row `abs` 1, `pho` 0) reads `fsnow` at or above `fcol`, within a relative 1e-6, and every founder that eats only the dissolved matter reads `fmat` at or above `fmcol`, in 3 of 3; every such founder carries its pair, and one without it counts against the clause. A check of the build: the richest cell of a column holds at least the column's mean by construction, and the branch's Dynamics test placed 26 stomachs and 25 leaves at or over their column's mean. What can still break it is the placer moving a founder off the cell it chose (the surface held off by the body's radius, the bed, a reef, the free-spot test) | founder rows of `lineage.jsonl`; `scripts/reads/r49-witness.py` |
| W2 | **the refill, measured in the run**: the median, over the pool founders, of the first `absorptive.jsonl` `densityHere` over `fsnow` lies between 0.02 and 0.3 in 3 of 3, with the first row's lag printed beside it (a guess: 0120 measured a mouth's cell at 5 to 25% of an untouched copy after a minute, outside the run, and a founder draws 45 to 52% of its cell a step. In still water the steady cell is k/(c + k) of the water round it, c the draw and k the refill, about 0.06 to 0.1 for the one-part stomach. The landing cell is its column's richest, so the water refilling it is thinner than `fsnow`. The band assumes the stirring stays at 0.02 m²/s, and XB replaces it) | `absorptive.jsonl` joined to the founder rows |
| W3 | **the landing cell is richer than its column**: the median `fsnow` over `fcol` of the pool founders at least 1.6 in 3 of 3 (0120's inference, from the first readings and the refill measured outside the run, that the water about round 48's founders held 1.6 to 8 times their column's mean) | founder rows |
| G1 | **gestation enters**: `gestationBirths` above 0 at 30,000 s in 3 of 3 (round 48: 7,234; 3,158 at seed 2's cut; 3,683) | `stats.jsonl` |
| G2 | **gestation is selected down**: the share of children (founders left out) with `gm` 1 in the last 5,000 s below its share in 3,000 to 8,000 s, in 2 of 3. This is 0119's G2 turned round, since round 48 read 0.27 to 0.13, 0.19 to 0.17 at its cut, and 0.20 to 0.12. The strong reading is printed beside it: births per death by mode in the last window (round 48: gestating 0.47, 0.64 and 0.46; lump 1.13, 1.19 and 1.22) | birth and death rows by `gm` |
| G3 | **the savings are buried with the saver**: the accounts at death of the gestating bodies (`ga` summed over death rows whose birth row has `gm` 1) total at least 20% of `gestatedJoules` at 30,000 s, in 2 of 3 (a guess: round 48 banked 675, 320 and 458 kJ, paid 7,234, 3,158 and 3,683 births from accounts, and held 23, 17 and 26 kJ at its last sample; at the 50 J floor a child that leaves 43 to 54% of the banked joules unaccounted for, and at 80 J a child 11 to 30%; seed 1's children were tiny and seed 3's large, so seed 3's price was the nearer to 80 J. Dying with them is the one other way out) | death rows; `stats.jsonl` |
| G4 | **the account's books agree** (a check of the instrument): banked, less held at the last sample, less held at death, over `gestationBirths`, is at least 50 J in 3 of 3. No child costs less than the overhead floor, so a smaller figure means a draw on the accounts that no field records | death rows; `stats.jsonl` |
| C1 | **the senses, as far as the record can see them** (a reading, no bar): no field reports either sense's value. Printed instead are two neighbours. One is the share of living bodies touching another body at a physics step, from `overlapBodies`' difference over a window (round 48: 0.0 to 2.0%, the highest at seed 1's end). Under D123 the contact sense reads 1 for about that share of bodies at any step, where the old sense read the share ever touched since the body's last change of plan. The other is the share of living bodies whose brain reads the contact or damage channel, from the genomes at 5,000, 15,000 and 30,000 s (round 48: 4 to 24% at the snapshots I read) | `stats.jsonl`; the genomes joined at snapshots |
| C2 | **no checkpoint waits**: no line `the checkpoint due at … waits` in the arm's error log, and 60 checkpoints in the manifest at the 500 s cadence, in 3 of 3; `partsKilled` and `moduleRebuilds` printed beside it (round 48: 0 parts killed in every seed; 389, 118 at the cut, and 324 module rebuilds) | `scratch/logs/r49-s<N>.err`; `run.json`; `stats.jsonl` |
| C3 | **no starved body dies solvent** (a check of the instrument): no death row whose cause is `starved` carries `res`, in 3 of 3; `res` by cause printed beside it. A body starves when its reserve reaches zero | death rows |
| B1 | **the books close and nothing breaks**: the largest energy audit residual under 0.1 J at every sample, the matter residual under 1e-5 units at the last sample, `diverged` 0, and a manifest that ends on its budget, in 3 of 3 (round 48: 2.2e-4 J and 4.5e-6 units at most, nothing diverged; seed 2's error at 13,700 s, which a re-run from founding did not repeat). The matter residual rose with the crowd in round 48 and stood at 45% of its bar with 8,618 bodies | `stats.jsonl`; the manifests; `diverged/` |
| V1 | **the films are faithful**: every film window recorded from a round 49 checkpoint on the round's own exe before the read is written reads FAITHFUL (`--film-window` exits 0, every identity row agrees, and `sourcesDiffer` is empty), at least one window per seed (round 48: no window could be faithful, since its checkpoints lack the contact record) | each window's `identity.jsonl` and exit code |
| V2 | **a resume is the run**: `--verify-checkpoint` names no differing member on seed 1's 15,000 s checkpoint, and a resume of seed 1 from 15,000 s to 16,000 s agrees with the recording in every `stats.jsonl` field at every sample (`scripts/compare-det.py` exits 0) | the checker's and the comparison's output |
| V3 | **the record is smaller**: each seed's run directory under 2 GB at 30,000 s (a guess: round 48's full seeds took 5.0 and 5.7 GB; the record's specification estimates about 1 GB for round 47's seed 2, which ended with 5,602 bodies against round 48's 8,600) | the directory's size on disk |
| M1 | **buds are born**: at least 100 birth rows carrying `bud` by 30,000 s in 3 of 3 (round 48: 987 and 491; 301 at seed 2's cut) | `lineage.jsonl` |
| M2 | **buds are built**: `budx` at 1 or more on at least three quarters of the bud rows, in 3 of 3 (round 48: 986 of 987, 252 of 301 at the cut, 481 of 491) | the bud rows |
| M3 | **a stomach on a leaf founds a line**: a clade rooted in a birth row whose `bud` names `absorptive`, with `abs` 1 and `pho` 1 (0119's M3), has ten or more living members at 30,000 s, in 1 of 3 or more (round 48: seed 1's 38, 31, 10 and 10; seed 3's best 6; seed 2's best 9 at its cut) | the parent walk over `lineage.jsonl` |
| M4 | **the stomach stays a passenger**: in every clade M3 counts, the living members' median share of their income from the snow (`foodW` over `foodW` plus `lightW`) is under 2% at 30,000 s (round 48: 0.24% in the largest line, 1.1 to 1.7% in the line with the largest stomach) | `absorptive.jsonl` joined to the clade |
| O1 | **the crowd is round 48's**: `alive` at 30,000 s between 4,000 and 16,000 in 3 of 3 (round 48: 8,618 and 8,589, still rising at the end; 4,583 at seed 2's cut; the band is a guess) | `stats.jsonl` |
| O2 | **the ceiling holds**: `alive` under 25,000 at every sample in 3 of 3 (round 48's highest: 8,634) | `stats.jsonl`; the manifests' ending |
| O3 | **the joints go**: `jointed` under 100 at 30,000 s in 2 of 3 (round 48: 3 and 7 in the full seeds; 680 at seed 2's cut). D123 changes what a jointed brain reads, and nothing in this world pays for a joint (0118's swim probe) | `stats.jsonl` |
| S1 | **the old live as long**: `meanAge` at 30,000 s above 1,500 s in 3 of 3 (round 48: 1,998 and 2,006 s; 1,511 s at seed 2's cut) | `stats.jsonl` |
| F3 | **no eater's line lasts**: no clade rooted in a pool founder has ten or more living at 30,000 s, in 3 of 3 (round 48: 2 and 0 living bodies rooted in the pool at the end; its two pool lines peaked at 45 and 24 living and were gone by 8,214 and 6,544.5 s). Predicted under every option | the parent walk |
| E1 | **what the endowment buys** (a reading, absent under (c)): the founder rows' `endow` (median and range), the founders' median age at death and the share that bred, by source and by pool body (round 48: medians of 72 to 78 J over 1.7 to 953 J; a tenth bred; 207 of the 214 trickle and pool founders that bred did so within 1 s of landing) | `lineage.jsonl` |
| R1 | **the reef readings carry** (readings): round 47's L5 and L6 as round 48 read them, the tables' snow against the open floor's at the end (round 48: 0.59 against 0.52, 0.56 against 0.55 at the cut, 0.63 against 0.50 J/m³) and the bodies per cubic metre under the caps against beside them (round 48: fewer under at every sample) | the snow dumps; the positions against `run.json`'s `reefs` |
| P1 | **the pace holds**: the whole run's pace at or above 1x real time in 3 of 3, with the last 2,500 s printed beside it (round 48: 1.06x and 1.13x at five threads beside a second arm, and 0.47x and 0.67x over the last 2,500 s; round 49 runs alone at ten threads under the machine's power limits, so this bar is a guess) | the footers and manifests |

## Predictions that depend on the endowment

Each block replaces F1 and F2 of 0119 with the form that option predicts. A pool founder's pool
body is its row's `pool` index: 0 is the one-part stomach, and 1 to 3 are the three bodies of a
stomach and a link.

### (a) The endowment stays as round 48 had it

| # | prediction | falsified by |
|---|---|---|
| EA1 | **the endowment pays for the child**: every one-part pool stomach has its first child within 1 s of landing, in 3 of 3 (round 48: 28 of 28, 8 of 8 at the cut, 18 of 18) | birth rows whose parent is a pool founder |
| EA2 | **so do some random founders**: 4 to 12% of the trickle's founders have a child within 1 s of landing, in 3 of 3 (round 48: 7.6, 7.8 and 8.0%; the band is a guess) | the trickle's founder rows and their children |
| EA3 | **F1, the pool founders live about five minutes**: their median age at death above 250 s in 3 of 3 (round 48: 312 s, 298 s at the cut, 335 s) | `lineage.jsonl` |
| EA4 | **F2, a pool founder breeds**: at least one pool founder has a child in 3 of 3 (round 48: 30, 8 and 18) | the birth rows |

### (b) The endowment pays upkeep only (the agent's recommendation)

| # | prediction | falsified by |
|---|---|---|
| EB1 | **no child at landing** (a check of the build): no pool founder has a child within 10 s of landing, in 3 of 3. By the arithmetic above, the one-part stomach must save 64 J after it grows. At round 48's richest first reading, 2.2 J/m³, that takes 33 s. If the build let the account pay for growth as well, it would still have 34 J to save, which takes 17 s. The three bodies with a link never bred at landing even in round 48 | birth rows against their parents' rows |
| EB2 | **nor for the random founders**: at most 2% of the trickle's founders have a child within 1 s of landing, in 3 of 3 (round 48: 7.6 to 8.0%; round 47: 0 to 0.24%; the purse-only estimate above: 0.1 to 1.1%) | the trickle's founder rows and their children |
| EB3 | **the one-part stomach lives on its endowment**: the one-part pool stomachs' median age at death above 300 s in 2 of 3 (round 48: 186, 168 and 179 s; the arithmetic at 0.2 J/m³: 478 s under (b) against 189.5 s under (a)) | `lineage.jsonl` by `pool` index |
| EB4 | **few breed, and only in rich water**: pooled over the seeds, between 1% and 30% of one-part pool stomachs have a child, the first at a median of at least 30 s after landing (a guess from round 48's own mouths, 4 of 54) | the birth rows |
| EB5 | **F1, the pool founders live longer**: all pool founders' median age at death above 300 s in 3 of 3 (round 48: 312, 298 and 335 s; the one-part bodies live longer and the others as before) | `lineage.jsonl` |
| EB6 | **the breeders landed richer** (a reading when fewer than five breed): the median `fsnow` of the pool founders that have a child above the median of those that do not, pooled | founder rows against their children |

### (c) The endowment goes

| # | prediction | falsified by |
|---|---|---|
| EC1 | **F1, the pool founders die fast again**: all pool founders' median age at death under 100 s in 3 of 3 (round 47: 24 to 46 s. The bodies with a link spend nearly all their purse growing, keeping as little as 9 J, and died at 13.5 to 89 s; the one-part body keeps 36 J and died at 120.5 to 139 s) | `lineage.jsonl` |
| EC2 | **no random founder breeds at landing**: at most 2% of the trickle's founders have a child within 1 s of landing, in 3 of 3 (round 47: 0, 2 and 0) | the trickle's founder rows |
| EC3 | **F2, a pool founder breeds rarely**: a pool founder has a child in at most 1 of 3 (round 47: 1 of 3; by the arithmetic the one-part stomach needs 0.61 J/m³ held at its mouth to breed before it starves) | the birth rows |
| EC4 | **the endowment is off**: no founder row carries `endow` and the header has no endowment token, in 3 of 3 | `lineage.jsonl`; the report's header |

## Predictions that depend on the wear and the stirring

The wear is senescence on upkeep, which rises as one plus the body's age over 3,000 s (D121).
The stirring is the snow's mixing rate, 0.02 m²/s in round 48.

| # | option | prediction |
|---|---|---|
| UA | the wear stays (recommended) | **S2**: in every seed with at least ten dead stomach children (birth rows with `abs` 1, `pho` 0, not founders), their median age at death is under 1,500 s (round 48: 1,118 s, 95 s at the cut, 455 s). The one-part stomach's break-even rises to about 0.59 J/m³ at 1,000 s and 0.88 at 3,000 s (0120's arithmetic) |
| UB | no wear at all | `meanAge` at 30,000 s above 3,000 s in 2 of 3 (a guess against round 48's 1,998 and 2,006 s); S2 above 1,500 s where it has ten deaths. Founders never age out, so read the generation minimum with the floor (CLAUDE.md's `gen min` gotcha) |
| UC | a hazard in place of the wear | a new mechanism; its clauses come with its specification, and S1 is set again there |
| XA | the stirring stays (recommended) | W2's band stands, and the snow's patchiness `det cv` at 30,000 s lies between 0.4 and 1.0 in 3 of 3 (round 48: 0.57 and 0.70; 0.69 at seed 2's cut; the band is a guess) |
| XB | the snow's stirring rises (0.2 m²/s, say) | W2's median rises above 0.2 in 3 of 3, since the stirring alone refills a mouth's cell about ten times faster (the proposal's section 3); `det cv` at 30,000 s under 0.4 in 3 of 3 (a guess). A grid refuses a horizontal rate that differs from the vertical one, and its explicit stirring refuses anything above 0.33 m²/s on 1 m cells at a half-second step |
| XC | a mouth that reads a neighbourhood | not proposed; no clause |

## The two-sided readings

- **W1 fails.** The placer moved some founders off the cell the rule chose, or the build reads
  the wrong point. Read each failure's first positions height against its cell's span and the
  body's size. A large body held under the surface out of a top cell is the clearance at work.
  A small body far from its cell is a fault in the build.
- **W1 holds and W3 fails.** The rule works, and a column's snow is nearly even in depth where
  founders land. Then the richest cell buys a founder little over a random depth, and the next
  placing question is between columns, where the rich water is.
- **W2 falls under 0.02.** The refill in the run is slower than the experiment outside it
  measured. A ledger break-even must then be set against a fiftieth of the water round a mouth.
  Read the current's speed at the landing points. **W2 rises over 0.3.** The landing cells
  refill faster than 0120 measured, and the ledger's mouth density is nearer the field's.
- **G2 fails.** Gestation's share rose. Round 48's fall was that realisation's, or D123 has
  changed something a gestating lineage depends on. Read the births per death by mode.
- **G3 holds.** A gestating body dies with a fifth or more of what it banked. The gene then
  pays a cost the lump does not, and those joules go into the corpse and so into the snow
  (`World.Bury` adds the account to the remains). **G3 fails under a tenth.** The gene's loss
  is its slower breeding and not its buried savings.
- **G4 fails.** Something draws on the accounts that no field records, or `ga` misses deaths.
  G3 is then unread until the instrument is fixed.
- **C2 fails.** A path that changes a body's plan is missing its rebuild. The warning names the
  checkpoint's second. Read which bodies changed plan in the step before it, by a bite, a module
  or a bud.
- **C3 fails.** A body died starved with a reserve above zero, a fault in the death path or in
  the instrument.
- **B1 fails on an error.** The seed is re-run from founding before the fault is called code
  (0120's rule). A clean re-run is one of the warning signs the machine's ruling asks to be
  reported to the owner the same hour.
- **V1 reads cousin.** The checkpoint misses a piece of state, as round 48's missed the contact
  record. Run `--verify-checkpoint` on it, which names the member.
- **V2 fails.** A resume is not the run, and every film window of the round is a cousin until
  the member is found.
- **M3 holds and M4 fails.** Selection has begun on the stomach in a budded line. That is the
  first sign of an eater arriving by the bud, and its line is read over time as 0120 read the
  line of 48048.
- **O3 fails.** Joints survive where round 48 lost them. D123's contact read on the step may
  give a jointed brain something to use. Read the jointed bodies' brains for the contact channel
  (C1's second reading).
- **F3 fails.** A line rooted in the pool has ten living at the end, the first eater's line
  since round 41. Read which option is on, the founder's `fsnow`, and whether its line bred on
  its own earnings or on endowments.
- **EA1 fails under (a).** The endowment no longer covers the one-part stomach's child, so a
  price has moved. Read the header against round 48's.
- **EB1 fails under (b).** Either the build lets the endowment reach the gate, or a founder
  landed in a cell that could pay 64 J in ten seconds by itself. The breeder's `fsnow` tells
  which. Above about 50 J/m³ it is the rich cell, and under that the build is at fault.
- **EB4 fails low under (b).** No one-part stomach breeds. That is a true answer to the round's
  question: the flow into a stomach's cell where it lands does not pay for a child. Round 50 then
  changes how an eater is fed, and XB is the candidate.
- **EB4 fails high under (b).** More than three in ten breed. The landing cells refill faster
  than round 48's mouths ate, which W2 should show.
- **EC1 fails under (c).** The pool founders outlive round 47's without an endowment. Placing
  them in the richest cell, which round 47 did not do, fed them for longer.

## What the round does not ask

Whether 600 s is the right endowment (E1 is a reading of what it buys). Whether a reactive
thrust term would let a stroke move a body (0118's swim probe; a proposal for a later round).
Whether a mouth should read a neighbourhood rather than its own cell (the proposal's section 3,
option (c)). Whether round 48's seed 2 stopped because the processor miscalculated: the
machine's own checks decide that, and the round only reports what it sees.

## What round 49's reader must add to round 48's

The round's reader, `scripts/reads/r49-read.py`, is to be built from round 48's
(`scripts/reads/r48-read.py`). It must add the following.

1. It reads snapshots, positions and poses through `scripts/reads/runrec.py`. That module joins
   the slim snapshot rows to the genomes file and reads the gzipped positions and the pose
   stream. Round 48's reader opens the old files by name and would find neither.
2. For W1, it sets each snow-only founder's `fsnow` against its `fcol` within a relative 1e-6.
   It sets each matter-only founder's pair the same way, by source and pool index. It counts the
   founders of either diet that lack their pair. It lists each failure with its first positions
   height, its column and its 1 m layer's span. The witness script's landing table is the start.
3. For W2, it divides each pool founder's first `densityHere` by its `fsnow`, and prints the lag
   between landing and that first row, by source and pool index.
4. For W3, it prints the median of `fsnow` over `fcol` by source and pool index.
5. G2 is turned round: it holds when the late share is below the early one.
6. For G3 and G4, it sums `ga` over the gestating bodies' death rows and sets the sum against
   `gestatedJoules` at the sample. It derives the price per gestating birth from the two.
7. For C1, it divides the window's growth in touching bodies by the window's physics steps and
   the living count. It also counts the living bodies whose genome reads the contact or damage
   channel at the snapshots at 5,000, 15,000 and 30,000 s.
8. For C2, it searches the arm's error log for the checkpoint warning. It sets the manifest's
   checkpoint count against the cadence, and prints the parts killed and the module rebuilds.
9. For C3, it counts death rows by cause, with the reserve at death on each.
10. For V1 to V3, it reads each film window's verdict from its identity rows and exit code. It
    reads the checker's and the comparison's output for V2, and each run directory's size.
11. For the endowment's clauses, it counts each founder's first child within 1 s and within
    10 s of landing, by source and pool index. It prints the one-part stomach's median age at
    death apart from the other pool bodies'. Under (b) it sets the breeders' `fsnow` against the
    non-breeders', and prints whatever the build records of the account at death.
12. For M4, it reads the stomach's share of income over the living members with a stomach in
    each M3 clade, as `scripts/reads/r48-entry/budshare.py` does.
13. O1, O3 and S1 are read against their own bars. Round 48's same seed is printed beside each
    for context, with seed 2's at 13,690 s.
14. For P1, it prints the pace over the last 2,500 s on every run, finished or running.
15. For the options, it reads S2 under the wear's option and `det cv` under the stirring's.

## Before the launch

This section is filled in at the launch, as 0119's was. It will hold six things.

- The owner's three rulings go here, and the blocks of the options not chosen are deleted.
- Under (b), the build of the upkeep-only account. Its tests show that the account never meets
  the gate and that both books close with it in them. A death row records what was left in it.
- The machine track's twelve steps in HANDOFF, done. Step 11 names the farm checks this round
  rests on. One is a 300 s smoke with the depth rule on, read by the witness script. The others
  are the checkpoint checker on a bitten crowd and the regress of a world without bites.
- The launcher, `rounds/env-r49.ps1`, with its header tokens read off a one-second run and its
  configuration hash.
- The commit that adds the entry, on a clean tree. Each manifest names it as `gitCommit`.
- Each seed's wall, set at half again what the pace of its own first rows implies.

The helpers behind this draft's arithmetic are in `scripts/reads/r49-prereg/`:
`stomach_founder.py`, `founders_budget.py`, `purse_only.py` and `gift_cap.py`.
