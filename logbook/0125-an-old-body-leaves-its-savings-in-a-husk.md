# Round 51's predictions: an old body leaves its savings in a husk

*2026-09-27, written before round 51's first seed and committed before it starts. Its sources are
round 50's read (logbook/specs/r50-read/read-end.txt), since round 50's entry, 0124, waits for its
pictures. The others are the owner's rulings of the day (D127 and D128), the ledger check behind
them (logbook/specs/r51-husk-ledger.md) and the build on the branch `round-51`. One screen has run
in round 51's world: 6,000 s of seed 1. Every number is round 50's or the screen's, and each says
which. A number I guessed says so.*

## What it asks

Until now a body in this world died one way. It ran out of energy. D038's wear raised a body's
upkeep with its age, so an old body burnt its savings as upkeep and died with nothing. What it had
saved left the world as heat, and a corpse held only its tissue, about 12 J. The ledger check found
that such corpses could not feed an eater. Spread over the tank they put about 40 W into the water,
and dissolved, that food is thinner than a still eater needs.

The owner ruled two changes this morning. D128 gives every body a chance of dying of age that rises
as it ages, doubling at a fixed interval, as Gompertz's law has it for animals. A birth and a
destroyed part each push the body's ageing clock on. The wear is off while the hazard is on, so an
old body stays productive and dies still holding its reserve. D127 turns a dead body into a husk.
The husk keeps that reserve, sinks faster than the snow, settles on the first reef top or on the
bed, and dissolves slowly into its cell. Beside these, a link no longer catches light, and a body's
first link costs nothing up to a typical link's size.

Round 51 asks four things. The first is whether death by age takes a large share of the dead, at the
ages the hazard was set for. The second is whether the husks form a larder that lies still. The
third is whether a line of pure eaters lasts beside it, and the fourth whether the joints fall away
once a link earns nothing. Beside these, it carries round 50's clauses that still ask something and
turns the leaf-founder clauses into readings, since they asked about round 50's new rule and it
held.

## The world

Round 50's world (0123's world section), with D127 and D128, from `rounds/env-r51.ps1` on the branch
`round-51`:

- **Death by age** (D128) comes from a Gompertz hazard. Each metabolic step, a living body with
  energy left draws against it. Its median falls at 3,000 s on the body's clock, and the hazard
  doubles every 600 s, a fifth of that base. The base is where bodies that lived past 1,000 s died,
  at a median of 2,670 to 3,240 s, in every round from 44 to 50 (D128). A birth pushes the parent's
  clock on by one doubling for a child the parent's size, and by less for a smaller child, in
  proportion. A destroyed part pushes it by one doubling for the whole body, in proportion to the
  part's share. The body's reserve goes into its husk. D038's wear is off (`EVOSIM_SENESCENCE 0`),
  since the build refuses the two together. Every body carries a lifespan gene of 1 that does not
  mutate, and a longer life costs nothing in this round. The report's header carries the rule as
  `age base 3000 s doubling x0.2 birth 1 wound 1 repair ^0 lifespan 1-1 at 0`, and a death row names
  its cause `aged`.
- **The husk** (D127) is what a dead body becomes. It sinks at 0.05 m/s, 25 times the snow's rate,
  and settles on the first reef cap under it or on the bed, where it stays. It gives its joules to
  the water of its cell with a half-life of half an hour (round 50's corpse had a half-life of 139
  s). When it holds under a twentieth of what it held at death, it gives the rest and leaves. A
  mouth that reaches it eats it as a corpse was eaten before, and the header carries the rule as
  `husk sink 0.05 m/s settles floor 0.05`.
- **The links** (D127) catch no light (round 50: half a leaf's rate). A body's first link from the
  root holds no energy up to 0.01 m³ of its adult volume, so a small joint costs no tissue and no
  upkeep. Round 50's first links measured a median of 0.009, 0.003 and 0.014 m³ in its three seeds
  (logbook/specs/r51-build/linkvol.txt), and the header carries `free link 0.01 m3`.

Every rule is a knob, off at its default. Each is a new tunable, so the build refuses every earlier
`config.json`. The genome gained the lifespan gene, so its format is 10 and every stored genome of
format 9 is refused. The inocula were converted by a text edit that added the gene at 1 and no other
byte (`scripts/convert-format9-to-10.py`). A checkpoint carries each husk's settled flag, first
joules and body plan, so a picture can draw the body it was. The stats rows carry `deathsByAge`,
`husksSettled`, `huskJoulesSettled`, `husksOnReef` and `huskJoulesOnReef`. Each rule is a new
realisation of every seed, so no seed replays round 50's.

Everything else is round 50's. The leaves are set where they earn most, and the founder cap stays at
0.9 of the gate. The pool keeps its one-part stomach, and the snow is stirred at 0.02 m²/s. Record
format 2 stays, with a checkpoint every 100 s. Three seeds of 30,000 s run at dt 0.01 from
`rounds/env-r51.ps1`, one at a time at sixteen threads, overnight, under the machine's ruling of
2026-09-25. The snow's transport runs on the card if its daytime check against round 50's record has
passed by then (D126), and on the processor if not. The card's copy is built to give the processor's
numbers byte for byte, so the choice moves the pace and not the world.

## Predictions

The screen is `runs/r51screen-s1`: round 51's launcher, seed 1, 6,000 s on eight threads, on the
build before the last commit that touched the simulation. That commit gave the husk its body plan
for pictures and added two stats fields. The final smoke replayed the screen on every shared sample
(the section before the launch), so seed 1's first 6,000 s in round 51 will be the screen's. Those
seconds are seen. Every clause is asked at 30,000 s, but several count events from the start (A2,
A4, LK1, LK2 and the founder clauses), and seed 1's part of them before 6,000 s is already known.

### Death by age

| # | prediction | falsified by |
|---|---|---|
| A1 | **death by age takes a large share of the dead**: `deathsByAge` is at least a fifth of `deaths` at 30,000 s, in 3 of 3 (round 50: none, every death a starvation; the screen: 2,139 of 4,932 by 6,000 s, 0.43) | the last stats row |
| A2 | **at the ages the hazard was set for**: the median age at an aged death lies in 1,500 to 3,500 s, in 3 of 3 (the screen: 2,452 s over 2,139, quartiles 1,639 to 3,073 s, with the young deaths overweighted since no body older than 6,000 s could die in it; the hazard alone puts it at 3,000 s on the clock) | death rows with cause `aged` joined to their birth rows |
| A3 | **the build does what the rule says** (a check): every aged death row carries a reserve above 0, and `deathsByAge` equals the aged death rows at the last sample, in 3 of 3 (the screen: 2,139 against 2,139, none without a reserve, a median of 73 J) | the stats row against the death rows |
| A4 | **who dies of age** (a reading): the median age at an aged death for bodies with no child, one or two, and three or more; the age at death by cause; the share of the aged that bred (the screen: 2,324, 2,494 and 2,525 s; starved 826 s; 51% bred) | the same rows |

A4 is a reading because the push is tangled with selection. A body that breeds more is pushed on
further, and it is also a body that lived long enough to breed. In the screen the bodies with more
children died older.

### The husks

| # | prediction | falsified by |
|---|---|---|
| H1 | **the husks hold a larder**: their joules over ρ (100 J a unit) are at least a tenth of the standing matter at 30,000 s, in 3 of 3 (round 50, whose corpses had a half-life of 139 s: 58, 72 and 96 units, under 0.006 of it; the screen: 1,937 of 15,264 units at 6,000 s, 0.127, against 1,035 in the living and 2,728 in the snow) | the last stats row |
| H2 | **the larder lies still**: at least a quarter of the husks' joules have settled at 30,000 s, in 3 of 3 (the screen: 0.42; the bar is a guess) | `huskJoulesSettled` over `corpseJoules` |
| H3 | **where it settles** (a reading): the settled husks on a reef cap against those on the bed, and the caps' share of the tank (the screen ran before the two fields existed) | `husksOnReef` and `huskJoulesOnReef` |
| H4 | **a husk leaves when it should** (a check, from 9,000 s): the husks standing at 30,000 s number between the deaths of the last 7,000 s less every husk eaten and the deaths of the last 9,000 s, in 3 of 3 (at a half-life of 1,800 s a husk falls to a twentieth in 7,780 s) | stats rows 7,000 and 9,000 s apart |
| H5 | **what the mouths took** (a reading): husks and bodies eaten, units eaten, parts killed (round 50: 4, 4 and 7 corpses eaten, no body and no part; the screen: none) | the last stats row |

### The eaters and the links

| # | prediction | falsified by |
|---|---|---|
| E3 | **an eater line lasts**: at 30,000 s a line has ten or more living pure stomachs (birth row `abs` 1, `pho` 0), in 1 of 3 (round 50: none, though two lines grew to 132 and 121 pure stomachs and died out, 0123's E2; the screen: 7, in line 4385, a one-part pool stomach landed at 3,345 s). I expect it to fail: the ledger check puts the husks' dissolved larder under a still eater's break-even once the mouth's emptied cell is counted | the parent walk from the living at 30,000 s |
| LK1 | **the joints fall away**: the mean count of jointed bodies over every sample to 30,000 s is below round 50's same seed's, in 3 of 3 (round 50: 408, 1,625 and 436; the screen's first 6,000 s: 435 against round 50 seed 1's 715 over the same samples) | `jointed` at every sample of both runs |
| LK2 | **how fast** (a reading): the jointed share of the children born in each 5,000 s, and `jointedInherited` at the end against round 50's (the screen: 0.30 of the children to 5,000 s, 0.09 after; round 50 at the end: 0, 513 and 60) | birth rows and the last stats row |

### Two clauses turned

| # | prediction | falsified by |
|---|---|---|
| O1 | **the world holds a crowd**: alive at 30,000 s between 2,000 and 16,000, in 3 of 3. Round 50's band began at 4,000. I moved it because the screen's crowd levelled at about 1,050 from 4,400 to 6,000 s, where round 50's seed 1 rose from 1,683 to 1,911 over the same seconds, and round 50's seed 1 grew 3.2 times from 6,000 s to its end (round 50: 6,132, 9,824 and 9,917; the lower bar is a guess) | the last stats row |
| S1 | **the hazard shortens the life**: the mean age of the living at 30,000 s is below round 50's same seed's at the same second, in 3 of 3. Round 50 asked for above 1,500 s (round 50: 2,223, 2,179 and 2,117 s; the screen at 6,000 s: 1,245 against round 50 seed 1's 1,607) | the last stats row of both runs |

## The clauses carried from round 50

Round 50's figures are from its read (logbook/specs/r50-read/read-end.txt). The round 51 reader
gives the same figures on round 50's three seeds (logbook/specs/r51-read/reader-on-r50.txt).

| # | prediction, in short | bar | round 50 |
|---|---|---|---|
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow | 3 of 3 | 2 misses in 309 in seed 2, each a hair under; none in 1 and 3 |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 3 of 3 | 0.22, 0.18, 0.17 |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 3 of 3 | 2.34, 2.14, 1.97 |
| G1 | births paid from a gestation account | 3 of 3 | 5,386, 5,728, 4,455 |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 2 of 3 | 0.275 → 0.137, 0.198 → 0.127, 0.163 → 0.139 |
| G3 | accounts held at death at least a fifth of what was banked | 2 of 3 | 41%, 37%, 45% |
| G4 | banked, less held, per gestation birth at least 50 J | 3 of 3 | 62, 60, 62 J |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | held |
| C3 | no starved body dies solvent (an aged body dies solvent by design) | 3 of 3 | held |
| B1 | audit under 0.1 J, matter residual under 1e-5 units, nothing diverged, ends on its budget | 3 of 3 | at most 3.8e-4 J and 1.0e-6 units; the screen's matter residual peaked at 1.1e-5 (below) |
| V1 | a faithful film window from the round's own checkpoints | one per seed | waits for the windows |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | seed 1 | held |
| M1 | at least 100 births carrying a bud | 3 of 3 | 356, 1,313, 829 |
| M2 | at least three quarters of bud births built the bud | 3 of 3 | 97%, 78%, 97% |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 1 of 3 | 98, 134, 25 |
| M4 | in each such line the stomach brings under 2% of the income | every seed M3 holds in | failed in seed 3, 5.4% |
| O2 | under 25,000 alive at every sample | 3 of 3 | at most 9,917 |
| F3 | no line rooted in a pool founder has ten living at the end | 3 of 3 | the largest 1 |
| P1 | the whole run at or above real time | 3 of 3 | 2.80x, 2.12x, 2.37x |
| EK1 | no founder has a child within 1 s of landing | 3 of 3 | none |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 3 of 3 | 17.76 J |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 2 of 3 | 358, 331, 349 s |
| EK4 | at most a fifth of them breed, and those that do have their first child a median 10 s or more after landing | pooled | 1 of 63 |
| EK5 | all pool founders' median age at death above 300 s | 3 of 3 | 399, 377, 352 s |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 3 of 3 | 0.72, 0.69, 0.67 |
| LC | the header carries `leaves where they earn most`, and the clamp's pile-up is absent | 3 of 3 | held |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | every such seed | held in seed 2 (0.53 → 0.06), not asked in 1 and 3 |
| S3 | a rigid stomach child dies young, median under 1,500 s, where ten have died | every such seed | failed in seed 1 (3,005 s over 137), not asked in 2 and 3 |

C1 (the senses' neighbours), E1 (what the endowment buys), EK6 and EK7 (the pool founders' snow and
the cap) and R1 (the reef's two readings) stay readings. So do O3 (the jointed count), V3 (the size
of a seed's folder), S2 (the stomach children) and E2 (the eater lines). L1 to L4, which asked
whether a leaf from the trickle lands in the light, lives, breeds and founds a line, become
readings. They held in round 50, and round 51 does not change the rule they asked about. LC stays,
since it checks the clamp's repair.

## The two-sided readings

- If A1 fails, the bodies starve before the hazard reaches them. The hazard then shapes only the few
  that eat well, and round 51 is round 50 with slower corpses. Read A4's ages by cause.
- If A2 fails low, the pushes age the breeders far faster than the hazard alone, which is the
  owner's addition working harder than its one doubling a child suggests. Read the aged by number of
  children. If it fails high, the hazard is slower than the clock was set for, which is a fault in
  the build until shown otherwise.
- If H1 fails, the husks leave faster than they gather: eaten, or dissolved before the tenth. Read
  H5, and the deaths against the husks standing (H4).
- If H2 fails, the water keeps the husks aloft. A husk sinks at 0.05 m/s. The current runs at 0.1
  m/s, twice that, so where the water rises it carries a husk up faster than it falls. Read H3, and
  the husks' heights in a checkpoint.
- If E3 holds, a line of pure eaters lives where no still eater could in the ledger's arithmetic.
  Read its members' feeding rows and where they sit against the settled husks. The larder then feeds
  something the ledger did not model: a husk's cell refilled faster than the stirring alone, or a
  mouth.
- If E3 fails, as I expect, the larder is set and no still eater can use it. That is the question
  the water's thrust was proposed for, and it is the owner's choice between thrust and a heritable
  lifespan that follows.
- If LK1 fails, a free first link keeps joints about as common as a link's light did. Read the
  jointed share of the children (LK2) and the joints in the winning lines (O4).
- If O1 fails low, death by age takes the crowd under 2,000. A plant then dies before it has saved
  enough to replace itself. Read A3's reserves at an aged death against a child's price, and the
  births per death.
- If S1 fails, the hazard does not bind at the crowd's scale, and the bodies die of hunger first
  (A1).
- If B1 fails on the matter residual alone, with the energy audit closed, the fields' float door is
  the first suspect, as in the screen. Read the residual's steps against the burn, the snow's
  deposits and remineralisation before anything else.

## What the round does not ask

Whether a heritable lifespan evolves. The gene is carried at 1 and does not mutate, and the round
right after round 51 turns it on, or the water's thrust comes first. That choice is the owner's, on
round 51's outcome. Whether a mouth takes the husks. Mouths ate 4, 4 and 7 corpses in round 50's
seeds and bit no living part, so H5 is a reading. Nor does it ask how a husk should look. The
theatre's grey, shrinking husk and a pink skin for a link are a picture matter, built after the
round, and their candidate pictures go to the owner first. Whether D128's sources say what it says
of them. Gompertz, Kirkwood's disposable soma, Jones and others' survey of ageing across species,
and the cost of reproduction are not yet in the literature review. D128 asked for a review round,
and it is owed.

## The reader

The round's reader is `scripts/reads/r51-read.py`, built from round 50's and committed before this
entry. It reads every carried clause as round 50's reader did, with round 50's same seed as the
baseline, and adds A1 to A4, H1 to H5, E3, LK1 and LK2. Run on round 50's three seeds against round
49, it gives round 50's verdict on every carried clause. Its numbers are round 50's too, apart from
two log paths and R1's baseline, which that run skipped (logbook/specs/r51-read/reader-on-r50.txt).
Run on the screen against round 50's seed 1, it prints the screen's numbers in this entry's tables
(logbook/specs/r51-read/reader-on-screen.txt). A clause it cannot read prints as absent and never as
a pass.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree. Every seed's
manifest names it as `gitCommit`, with no DIRTY mark beside it. The build is the branch `round-51`,
cut from main at `336bfe1`. It stays off main while the films of rounds 49 and 50 are made from
main's build. The seeds launch from its worktree, `scratch/wt-r51`, into the main tree's runs, as
rounds 49 and 50 did from theirs. The checks ran from the scripts under scratch/r51-build/, one
heavy job at a time beside one or two farm runs.

- The Core suite passed 1,035 of 1,035 with its slow experiments. Among them is the thread-identity
  test at one, four and sixteen threads, which gave the word `c9b0cabce249c1dd` at each, round 50's
  word, since every new rule is off in its config. The Farm suite passed 194 of 194, and the
  Dynamics suite 118 of 118 on the new crowd fixture.
- D127's and D128's own tests check each rule on and off. With the hazard off no clock moves, and
  the wear and the hazard are refused together. With it on, bodies die of age solvent and near the
  base with the books closed, and a birth pushes the clock ahead. A husk sinks at its own rate,
  settles on the bed and stays, and leaves when it falls under its share of what it was. The first
  link holds no energy up to its allowance. The lifespan gene survives a save and a reload, mutates
  only when its chance is above zero, and costs repair only above 1. A checkpoint carries the
  clocks, the husks and the count of deaths by age, and a husk takes its body's last pose in the
  solver. No test of its own covers the wound's push or a husk settling on a reef cap. The screen
  killed no part, and it ran before the fields that count husks on a cap existed.
- Round 42's config fixture was re-recorded on this build (`pfix14`, `b4f5f1e1d17205c1`), since the
  new tunables refuse every earlier config. The crowd fixture is round 44's world run 20,000 s on
  this build with every new rule off (`r51fix-s4`). Two folded digests in the bite-rebuild tests
  moved with the husk's new members and were re-pinned; the trajectories they fold did not move.
- The screen, `runs/r51screen-s1` (`configHash f9f845bd…`), ran seed 1 of the launcher for 6,000 s.
  It ran on eight threads at 3.5 times real time, on the build before the husk-pose commit. Its
  header carried every new token. The energy audit read at most 2.2e-6 J, and nothing diverged. The
  matter residual peaked at 1.1e-5 units at 2,270 s and read 1.1e-6 at the end, about ten times
  round 50's peak (1.4e-6). Its steps track the burn, the snow's deposits and remineralisation. Each
  of those crosses the fields' float door (CLAUDE.md), while the husks' own accounts are doubles on
  both sides. I read it as that door working over more deposits. A leak in the husks would open the
  energy audit too (an inference). B1's bar stays at 1e-5 units. The member check on the screen's
  3,000 s checkpoint passed.
- The final smoke, `runs/r51smoke-s1`, ran the same seed for 3,000 s on this build's farm (`coreHash
  ecf5a4e7…`, `farmHash a2bbcccf…`, `dynamicsHash d675fb37…`). It was identical to the screen on all
  300 shared samples, so the husk-pose commit moved no trajectory. Its header carries the tokens of
  D125, D127 and D128. By 3,000 s 38 of its 498 settled husks lay on a reef cap. The member check on
  its 2,000 s checkpoint passed, twin-stepped for 100 physics steps. A resume from that checkpoint
  to 3,000 s (`runs/r51smokeR-s1`) was identical to the run on all 100 shared samples. The
  wall-clock fields and the two step counts that restart at a resume were left out, as round 49's V2
  left them.

The queue is `rounds/queue-r51.ps1`. It starts `scripts/farm-queue.ps1` from the worktree for the
three seeds, and then V2 (`rounds/v2-r51.ps1`) on seed 1's checkpoint at 15,000 s. The owner asked
for the round to be prepared and not started while the films of rounds 49 and 50 are made, so it
starts on the owner's word.
