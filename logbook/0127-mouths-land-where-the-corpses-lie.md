# Round 52's predictions: mouths land where the corpses lie, and lifespan becomes a gene

*2026-09-29, written before round 52's first seed and committed before it starts. Its sources are
round 51's read (logbook/specs/r51-read/final.txt) and the owner's rulings D129 and D130. Beside
them stand the literature behind the lifespan gene (logbook/specs/r52-lifespan-literature.md), the
screen of its price against round 51's life history (logbook/specs/r52-lifespan-screen.txt), the
landing arithmetic (logbook/specs/r52-landing.md) and the build on the branch `round-52`. One smoke has run
in round 52's world: 6,000 s of seed 1. Every number is round 51's or the smoke's, and each says
which. A number I guessed says so.*

## What it asks

Round 51 gave the world a larder. Half the dead died of age, holding their savings, and their
corpses sank, settled and dissolved slowly. At the end the corpses held an eighth of the world's
matter, three quarters of it lying on the bed or a reef. One corpse was eaten in each seed in
30,000 s. The founders that carry a mouth, about 200 a seed after the trickle starts, landed
wherever the snow was richest, lived a median of about 110 s, and not one of them bred.

The owner ruled two changes. D129 sends a founder with a mouth to the corpses. The trickle's
lottery takes its column with a chance equal to the corpse energy lying there over the richest
column's, and the depth rule sets it just above the richest corpse in that column, within its
reach. D130 puts into the same round the second half of D128: the lifespan gene every body has
carried at 1 since round 51 now mutates, the founders draw it from a range, and a longer life
costs repair. The owner said to do both, and that if it causes a problem one of them
will be removed.

Round 52 asks three things. The first is whether the mouths land in the larder and eat from it,
and whether a meal is enough to found a line. The landing arithmetic says one meal and no line. The
second is whether the lifespan gene moves, and where. The screen says it holds near 1 while upkeep
takes most of a leaf's light, then rises as that share falls. The third is whether the matter
books now close to the last digits, since the burn no longer rounds what it returns. Beside these
it carries round 51's clauses that still ask something.

## The world

Round 51's world (0125's world section), with D129, D130 and B1's fix, from `rounds/env-r52.ps1`
on the branch `round-52`:

- **Mouths at the corpses** (D129). A mouth is any part whose intake is above 0. A founder with
  one reads the corpses as a food. Its column is kept with a chance equal to the corpse joules in
  the snow grid's 1 m column over the richest column's; a mixotroph takes the larger of that and
  its other foods' shares. Under the depth rule a mouth whose corpse share is its largest is set
  between the richest corpse in that column and the mouth's reach above it, and the placer lifts
  it clear of the bed. Stomachs and leaves are placed as in round 51. The header's founders token
  ends `mouths at the corpses`.
- **A heritable lifespan** (D128's second half, D130). A body's lifespan gene L stretches its whole
  ageing curve: the median age at death by age is L × 3,000 s and the hazard doubles every L × 600
  s, so every body's curve has one shape. A birth changes the child's L with a chance of 0.08, by a
  Gaussian step of 15% of L, as every other scalar gene of this world mutates. The founders draw L
  evenly from 0.5 to 1.5. A longer life costs repair: a body's upkeep is multiplied by L^0.5. A body at
  L 2 pays 1.41 times the upkeep of one at 1, and a body at 0.5 pays 0.71 times. The header's age
  token reads `age base 3000 s doubling x0.2 birth 1 wound 1 repair ^0.5 lifespan 0.5-1.5 at 0.08`. The birth and wound pushes are round 51's.
- **The exact door** (B1's fix). The burn, remineralisation and a corpse's last instalment hand the
  dissolved field its units as a double, where they went through a float. In round 51 the burn's
  dropped half-ulps kept one sign for thousands of seconds and were 98% of B1's matter residual
  (logbook/specs/r51-read/b1-probe.txt).
- **Two instruments.** A mouth founder's row carries `fcorp`, the corpse joules of the 1 m column
  it landed in, and `fcorpm`, the corpses' joules over the live columns: what a column drawn at
  random would hold. Every birth row carries `ls`, the gene. Each is written only in a world whose
  rule is on, so every recorded row is the bytes it was.

The code and the records call a dead body a corpse from this build. Round 51, D127 and 0125 said
husk for the same object, and the stats fields are renamed (`corpsesSettled`,
`corpseJoulesSettled`, `corpsesOnReef`, `corpseJoulesOnReef`). The new tunable refuses every
earlier `config.json`. Each change is a new realisation of every seed, so no seed replays round
51's.

Everything else is round 51's. Three seeds of 30,000 s run at dt 0.01 from `rounds/env-r52.ps1`,
one at a time at sixteen threads, overnight, with a checkpoint every 100 s, and the snow's
transport on the processor.

## The values of the lifespan gene

The owner ruled the three values on 2026-09-29 from the agent's recommendation. D128 asked that
they rest on the literature first. The research round's report is
logbook/specs/r52-lifespan-literature.md, and the review round in research/LITERATURE-REVIEW.md
that files it is owed.

- The price is **0.5**, and no study gives it. Kirkwood's disposable soma needs it above zero: at zero a
  longer life is free and evolves until almost nobody dies of age, which is D038's reason. Selection
  experiments on flies trade between nothing and about half a percent of early fecundity for each
  percent of lifespan. With upkeep at 0.41 to 0.51 of the light the living catch, which is round
  51's second half, that maps to a price between 0 and about 0.7. The research agent's point was
  0.5.
- The chance is **0.08**, every other scalar gene's. Its mutational variance sits at the fast end of
  what has been measured for lifespan, which suits a round of about twenty generations.
- The founders' range is **0.5 to 1.5**, which keeps the founders' mean at round 51's 1.

## Predictions

The smoke is `runs/r52smoke2-s1`: round 52's launcher with the three values, seed 1, 6,000 s on
four threads, on this entry's build. Its checkpoint at 5,000 s passed the member check, and a resume
from it to 6,000 s agreed with the run (the section before the launch). Seed 1's first 6,000 s in
round 52 will be the smoke's. Those seconds are seen. Every clause is asked at 30,000 s, but several
count events from the start, and seed 1's part of them before 6,000 s is already known.

### The mouths at the corpses

| # | prediction | falsified by |
|---|---|---|
| K1 | **the mouths land in the larder**: over the mouth founders from 3,000 s, the median of `fcorp` over `fcorpm` is at least 5, in 3 of 3 (the landing arithmetic on round 51 seed 1 at 12,000 s: 69 J against 5.6 J, 12 times; the smoke: 11.5 over 24 founders, 112 J against 9.7 J) | the founder rows with `ink` 1 |
| K2 | **all of them**: at least nine in ten of those founders land in a column holding corpses (`fcorp` above 0), in 3 of 3 (the smoke: 24 of 24) | the same rows |
| K3 | **the larder is eaten**: `corpsesEaten` is at least 30 at 30,000 s, in 3 of 3 (round 51: 1, 1 and 1; the smoke: 23 by 6,000 s; the arithmetic, 70 to 80% of about 200 mouth founders landing with a corpse in reach, puts it near 100, a guess) | the last stats row |
| K4 | **a meal buys time**: the mouth founders from 3,000 s die at a median age at least 1.5 times round 51's same seed's (round 51: 104, 117 and 108 s; the smoke: 222 s over 23) | their death rows |
| K5 | **a line from a mouth lasts**: a line rooted in a mouth founder has ten or more living at 30,000 s, in 1 of 3 (round 51: no mouth founder bred; the smoke: 2 of 26 bred, 3 children). I expect it to fail. A meal is three to five minutes of a small mouth's upkeep, new corpses fall round a landing at about 0.03 W against its 0.155 W of keep, and living on them needs a swim of 4 to 8 cm/s where evolved strokers make 2 to 6 mm/s (0.03 W and the swim are the landing arithmetic's) | the parent walk from the living |
| K6 | **what the mouths did** (a reading): the mouth founders by source, those that bred and their children, `corpsesEaten` and `unitsEaten` per mouth founder, and the share of mouth founders that ate at all where the feeding log says | the rows and the feeding log |

### The lifespan gene

| # | prediction | falsified by |
|---|---|---|
| LS1 | **the gene does what the rule says** (a check): every birth row carries `ls`, and the median age at an aged death rises from the bodies with `ls` under 0.8 to those from 0.8 to 1.2 to those at 1.2 and over, in 3 of 3 (the smoke: 1,880, 2,187 and 2,770 s over 1,113, 954 and 454 aged deaths) | birth rows joined to aged death rows |
| LS2 | **it holds while upkeep is dear**: the mean `ls` of the children born from 3,000 to 8,000 s lies in 0.85 to 1.1, in 3 of 3. From 3,000 s the smoke's living spent 0.60 to 0.64 of the light they caught on upkeep, and at that share the screen puts the best lifespan at 0.95 to 1.1 (the smoke's children from 3,000 to 6,000 s: 1.02, 0.96 and 0.95 by thousand seconds) | the birth rows |
| LS3 | **it rises when upkeep eases**: the mean `ls` of the children born in the last 5,000 s is at least 0.1 above that of those born from 3,000 to 8,000 s, in 2 of 3. Round 51's living spent 0.36 to 0.46 of their light on upkeep by 30,000 s, and at 0.41 to 0.51 the screen puts the best lifespan at 1.45 to 2.05. This is the round's least certain prediction: it rests on a mean-field sketch, and on the upkeep share falling as it did in round 51. The reader prints the share at both windows so that a failure can be told apart from a world whose share did not fall | the birth rows and the stats rows |
| LS4 | **no runaway**: the mean `ls` of the living at 30,000 s is under 2.5, in 3 of 3 (the screen's highest optimum is 2.05; at a price of 0 it runs off its grid) | the birth rows of the living |
| LS5 | **who lives long** (a reading): `ls` of the living by guild (leaves, bodies with a stomach, mouths) and of breeders against the rest, its spread (10th, 50th and 90th percentile), and the upkeep share at 5,000, 15,000 and 30,000 s (the smoke at 6,000 s: 0.67, 0.99 and 1.34; leaves 1.01 over 961, bodies with a stomach 0.85 over 6) | the rows |

### Clauses turned

| # | prediction | falsified by |
|---|---|---|
| A2 | **at the ages the hazard was set for**: the median age at an aged death lies in 1,500 to 4,500 s, in 3 of 3. Round 51's band ended at 3,500 s; LS3 predicts the gene rising late, which lengthens the last lives, so the top is raised by a guess (round 51: 2,317, 2,509 and 2,572 s; the smoke: 2,100 s over 2,529, a young world's) | aged death rows joined to their birth rows |
| B1 | **the books close**: the audit stays under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and the run ends on its budget, in 3 of 3. The bar is round 51's, which the matter residual failed in every seed (4.5e-5, -6.3e-5 and -1.1e-5 units at the end). Beside it the reader prints the largest residual (the smoke: 6.2e-7 units at 1,000 s, falling to 3.8e-8 by 6,000 s, where round 51's grew) | every stats row and the manifest |

S1 asked for the mean age below the baseline's, and LK1 for the jointed count below it. Each asked
about a rule round 51 brought in, which round 52 does not change, and the gene can move the mean
age either way, so both become readings. EK5 asked every pool founder's median age at death to
exceed 300 s. It failed in all three of round 51's seeds at 276 to 285 s, and round 52 does not
change the pool. A clause expected to fail asks nothing, so it becomes a reading too.

## The clauses carried from round 51

Each is asked as 0125 asked it, with round 51's same seed as the baseline where one is named.

| # | prediction, in short | bar | round 51 |
|---|---|---|---|
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow | 3 of 3 | 2 of 3; one trickle founder in seed 3, 1.7% under |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 3 of 3 | 0.17, 0.15, 0.16 |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 3 of 3 | 2.16, 2.27, 2.10 |
| G1 | births paid from a gestation account | 3 of 3 | 2,583, 3,458, 3,716 |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 2 of 3 | 0.154 → 0.121, 0.171 → 0.104, 0.132 → 0.120 |
| G3 | accounts held at death at least a fifth of what was banked | 2 of 3 | 48%, 43%, 46% |
| G4 | banked, less held, per gestation birth at least 50 J | 3 of 3 | 63, 60, 59 J |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | held |
| C3 | no starved body dies solvent (an aged body dies solvent by design) | 3 of 3 | held |
| V1 | a faithful film window from the round's own checkpoints | one per seed | held |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | seed 1 | held |
| M1 | at least 100 births carrying a bud | 3 of 3 | 284, 572, 1,237 |
| M2 | at least three quarters of bud births built the bud | 3 of 3 | 100%, 97%, 92% |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 1 of 3 | 8, 144, 143 |
| M4 | in each such line the stomach brings under 2% of the income | every seed M3 holds in | 0.7%, 0.8% |
| O1 | alive at 30,000 s between 2,000 and 16,000 | 3 of 3 | 4,717, 3,918, 5,648 |
| O2 | under 25,000 alive at every sample | 3 of 3 | at most 5,701 |
| F3 | no line rooted in a pool founder has ten living at the end | 3 of 3 | the largest 1 |
| P1 | the whole run at or above real time | 3 of 3 | 4.09x, 4.12x, 2.87x |
| EK1 | no founder has a child within 1 s of landing | 3 of 3 | none |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 3 of 3 | 17.76 J |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 2 of 3 | 337, 321, 370 s |
| EK4 | at most a fifth of them breed, and those that do have their first child a median 10 s or more after landing | pooled | 1 of 63 |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 3 of 3 | 0.50, 0.59, 0.55 |
| LC | the header carries `leaves where they earn most`, and the clamp's pile-up is absent | 3 of 3 | held |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | every such seed | not asked in any seed |
| S3 | a rigid stomach child dies young, median under 1,500 s, where ten have died | every such seed | failed in seed 3 (1,806 s over 28), not asked in 1 and 2 |
| A1 | deaths by age at least a fifth of the dead at the end | 3 of 3 | 0.49, 0.37, 0.41 |
| A3 | every aged death holds a reserve, and the stats count equals the rows | 3 of 3 | held |
| H1 | the corpses' joules over ρ at least a tenth of the standing matter | 3 of 3 | 0.136, 0.125, 0.144 |
| H2 | at least a quarter of the corpses' joules settled | 3 of 3 | 0.76, 0.78, 0.73 |
| H4 | the corpses standing lie between the deaths of the last 7,000 s, less those eaten, and those of the last 9,000 s | 3 of 3 | held |
| E3 | a line has ten or more living pure stomachs at the end | 1 of 3 | none; I expect it to fail again, since D129 moves mouths and not stomachs |

C1 (the touch share), V3 (a seed's folder), O3 (the jointed count), E1, R1a and R1b, EK6 and EK7,
S2, L1 to L4, E2, A4, H3, H5, LK2, S1, LK1 and EK5 are readings.

## The two-sided readings

- If K1 or K2 fails, the rule does not reach the placer. That is a fault in the build until shown
  otherwise, since the smoke and the Dynamics test both read it working. Read the header token and
  the founder rows' `fcorp` against their positions.
- If K3 fails with K1 and K2 holding, the mouths land beside corpses and do not eat them. Read the
  feeding log for the mouth founders' first rows. Then read the reach against the height the placer
  set them at, since a body of radius over 0.5 m sits with its mouth above the reach (the landing
  arithmetic's 30% at 0.8 m).
- If K4 fails, the meal is too small to matter. Read K6's units eaten per mouth founder.
- If K5 holds, a line of mouths lives on the corpses. Read its members' feeding rows, where they sit
  against the settled corpses, and whether they moved: that would be the first line in this world
  to live by eating what died.
- If LS2 fails high, the gene rose while upkeep was dear, and the price is weaker than the screen
  says. If it fails low, the price is stronger than the screen says. Or the step on L, taken on L
  and not on its logarithm, pulls the gene down harder than the screen assumed; the research agent
  put that pull at about 9% of the geometric mean in 100 generations.
- If LS3 fails, read the upkeep share at both windows first. If the share did not fall, the
  prediction's premise failed and not the screen. If it fell and the gene stayed, the screen's
  mean field misses what limits a long life here: starvation before age, kin competition among
  newborns near their parents, or the birth pushes.
- If LS4 fails, the price is too low to hold the gene. That is D038's runaway, and the owner's
  ruling to remove one of them if it causes a problem applies.
- If B1 fails on the matter residual, the exact door did not close it, and the next leg is the
  field's `Take`, the one float door left.
- If O1 fails, read LS5 and A1 together: a gene gone long would thin the dying, and one gone short
  would thin the breeders.

## What the round does not ask

Whether a mouth can swim to its next meal. That is the water's reactive thrust
(`fable-propose-reactive-thrust.md`), which waits for the owner's ruling. Whether the doubling
fraction or the birth push should change. The literature reads the first at 0.1 for most animals,
against this world's 0.2. It reads the second at 0.05 to 0.25 doublings for a child a bird raises
to its own size, against this world's one doubling for a child born at the parent's size. Round 51's
children are born at 0.16 to 0.34 of their parent, so each birth costs 0.16 to 0.34 of a doubling.
Both are D128's values and the owner's, and the round keeps them. Whether ageing's shape should be a
gene too: Jones and others found shape and pace varying independently across species, and this
world stretches one shape.

## The reader

The round's reader is `scripts/reads/r52-read.py`, built from round 51's and committed with this
entry. It reads every carried clause as round 51's reader did, with round 51's same seed as the
baseline, reads the corpse fields under either name, and adds K1 to K6 and LS1 to LS5. Run on round
51's three seeds, it gives round 51's own verdict on every carried clause
(logbook/specs/r52-read/reader-on-r51.txt). The exceptions are the three turned to readings, and
V1 and V2, which need their windows and log. Run on the smoke, it prints the smoke's numbers in this
entry's tables (logbook/specs/r52-read/reader-on-smoke.txt). A clause it cannot read prints as
absent and never as a pass.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree. Every seed's
manifest names it as `gitCommit`, with no DIRTY mark beside it. The build is the branch `round-52`,
cut from `round-51-tests` and carrying main's theatre work, launched from its worktree
`scratch/wt-r52` into the main tree's runs.

- The Core suite passed 1,048 of 1,048 with its slow experiments on the exact door. Its
  thread-identity word is `6d59a448ddcefed5` at one, four and sixteen threads, and it passed
  1,023 of its default set with the instruments. The Dynamics suite passed 122 of 122 and the Farm suite 197 of
  197.
- D129's tests place mouths over corpses on the bed within their reach, and keep the drawn depth
  with the rule off. The instruments' tests read `fcorp` and `fcorpm` against the corpses as the
  step leaves them. They read `ls` on every birth row where the gene can differ and on none where
  it cannot, through a checkpoint as well.
- The smoke ended on its budget with the books closed (the audit's largest 1.1e-5 J, the matter
  residual's 6.2e-7 units, nothing diverged). Its checkpoint at 5,000 s passed the member check, with
  the two worlds stepped side by side for 100 physics steps and 2 metabolic steps. A resume from it
  to 6,000 s agreed with the run on every shared sample.