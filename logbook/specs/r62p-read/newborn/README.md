# Why a bred grazer starves in `r62p-t3-6201`: the newborn ledger

*2026-10-26, the speed agent, for the coordinator and the owner, from a read by an Opus subagent that the speed agent
checked against the code. The world is the pace screen's thrust arm, which passed D222's tests at 24,000 s
(`logbook/specs/r62p-read/24000-t3/`). The full output is `newborn-ledger.txt`, from `scripts/reads/newborn-extract.py`
and `scripts/reads/newborn-ledger.py`. Each number is marked measured (from the run's records), derived (from the code
and measured values) or guessed.*

## The finding in plain words

A newborn grazer is born with enough reserve for about a minute, but on its first metabolic step growth spends most of
it on becoming adult-sized. It is then left with about 17 J at an adult's upkeep, about 18 seconds. Food is close: the
nearest living leaf is about 1.4 m away. A newborn that swims at an adult's speed reaches it in about 10 seconds and
eats. Half of them barely move in their first 10 seconds and starve. Nothing at birth or in the parent tells the two
apart.

So raising the newborn's reserve share from 0.2 to 0.5 bought almost nothing. Growth converts any reserve above a
tenth of the tissue into body at once. The runway is set by the growth floor, the investment gene and the breeding
overhead, which are world rules and the owner's.

## The premise, corrected

The first tally counted a meal by the joules a grazer's bites kept (`bk`). With the kill gut on (D232), a swallowed
kill goes through the gut to the reserve and never touches `bk` (`WorldContact.cs`, the gut's digestion). 429 bred
grazers made kills and still read `bk` 0. Counting kills as meals, 1,283 of the 2,507 bred grazers that died never ate
(51 %, measured), not 1,712 (68 %). 1,282 of them starved, at a median age of 23.5 s (measured).
`scripts/reads/grazer-tally.py` now prints both counts.

## 1. What a newborn spends

| | First step, 0 to 0.5 s | 0.5 to 60 s | Adult |
|---|---|---|---|
| Body fraction | 0.65 at birth (measured) | 1.0 at the first frame for 90 % of those that never ate and 97 % of those that did (measured) | 1 |
| Standing upkeep | about 0.6 W (derived: volume terms × 0.65, area terms × 0.65^⅔) | 0.84 W never ate, 0.90 W ate (measured) | 0.908 W (measured) |
| Growth | 19.3 J in one step, 54 % of a 36 J birth reserve (derived) | 0, the reserve is under the floor (derived) | 0 |
| Neural cost | 0 (the config) | 0 | 0 |
| Stroke work | at most 0.011 W a jointed body, the world's mean (derived) | the same | the same |
| Reserve | 36 J at birth, 16.6 J after the first step (derived) | 10.5 J never ate, 12.2 J ate, at about 5 s (measured) | 313 J (measured) |

Upkeep is scaled to the body's size in the code, but growth makes the newborn adult-sized within half a second, before
it can eat. `World.GrowOne` invests everything above `GrowthReserveFloor` (0.1 of the body's tissue) every step, by
design (checked in the code). 16.6 J at 0.9 W lasts 18.4 s (derived). The measured age at the first frame plus the
seconds its reserve held then is 18.9 s for those that never ate and 19.6 s for those that did.

## 2. What a newborn can earn

| Term | Value |
|---|---|
| The mouth's cap, an adult pool body | 1 × 1.44 m² × 16 × 100 J/s = 2,304 W (derived) |
| At a body fraction of 0.65, before growth | about 1,740 W (derived) |
| Kept a mouth blow, over the world | 38.5 J (derived), 42 s of an adult's upkeep |
| Bred eaters' kept bite intake over their lives | 0.60 W, range 0 to 1.86 (measured; leaves out the gut, so low) |
| Spend | about 0.9 W (measured) |

The cap is more than a thousand times the spend, so a newborn in contact with a leaf is in profit from its first blow.
The ceiling never binds; reaching the first contact does.

## 3. How far the food is, against how fast it swims

| | Never ate | Ate | Adult |
|---|---|---|---|
| Nearest living leaf, in 3D | 1.45 m, quartiles 1.22 to 1.69 | 1.38 m | 1.32 m |
| The same, horizontally | 0.53 m | 0.47 m | |
| A leaf within 2 m | 91 % | 95 % | |
| Speed over 0 to 10 s | 0.056 m/s, quartiles 0.008 to 0.163 | 0.139 m/s | 0.147 m/s |
| Distance over speed, a straight-line lower bound | 26 s (derived) | 10 s (derived) | |
| Runway | 18.9 s (measured) | 19.6 s (measured) | |

A newborn lands 3.9 m from its parent, inside the oasis (measured). At an adult's speed it reaches a leaf in about
10 of its 19 seconds; a slow one does not.

## 4. Those that eat against those that never do (all measured)

| | Never ate | Ate |
|---|---|---|
| Body fraction at birth | 0.65 | 0.65 |
| Runway | 18.9 s | 19.6 s |
| Distance to a leaf | 1.45 m | 1.38 m |
| Parent's reserve, and its funds over its breeding gate | 554 J, 0.89 | 581 J, 0.91 |
| Parent's age | 402 s | 398 s |
| Parent's reserve rising before the birth | 39 % | 40 % |
| Parent made a kill in the 100 s before | 89 % | 92 % |
| Speed over 0 to 10 s | 0.056 m/s | 0.139 m/s |
| Median speed under 0.03 m/s | 39 % | 15 % |
| Born without a joint | 14 % | 7 % |

Nothing at birth or in the parent separates the two. Motion in the first 10 seconds does: jointed newborns that never
ate still moved at 0.074 m/s, against 0.168 m/s for jointed ones that did. The records cannot say why a jointed
newborn is slow, whether a mutation of its brain or drive, a tangle, or crowding. Comparing each child's genome with
its parent's (`genomes.jsonl.gz`), or a column of each body's stroke work, would answer it. The share that ate rises
with the reserve at the first frame, from 0.37 under 5 J to 0.96 above 40 J (n = 24), but that reserve depends partly
on how late the first frame came.

## 5. The life history

| Term | Value |
|---|---|
| Children a breeding | 1, in 99 % (measured) |
| A child's start | 73 J: 36.7 J of tissue and 36.7 J of reserve (derived) |
| Overhead | 73 J (derived: 2 J a tissue joule) |
| The price, B | 147 J, range 100 to 158 (derived). The budget's 175.5 J was at the old share of 0.2, where the share was capped; here it is not |
| The parent's gate | 684 J (measured): the price plus a 600 s margin at 0.908 W |
| Breedings a dead grazer parent | 5, range 2 to 8 (measured) |
| Children a parent that ever ate | 2, range 1 to 4 (measured) |
| Runway after the first step | 16.6 J, 11 % of B (derived) |

Half of the price is overhead, and growth turns most of the rest into body at once. A parent pays 147 J to give its
child about 18 seconds.

## What it means

These readings are inference. A child starts with about 1.3 times an adult body's tissue (A, about 55 J) in all.
Growing to adult takes A of it, so its runway is about 0.3A, 16.6 J, for any reserve share above about 0.25. At 0.2 the
share's cap made it 0.25A, 13.75 J. So the reserve dial is close to inert. The runway is moved by the investment gene,
the growth floor (`GrowthReserveFloor`) or the breeding overhead, and half the newborns then fail because they barely
move in their first 10 seconds. Changing any of these is a world rule, and so the owner's.

## The genome diff (2026-10-26 evening)

`scripts/reads/genome-diff-read.py` compared each bred grazer's genome with its parent's, by category (`genome-diff.txt`,
beside this file). 2,003 of the 2,565 bred grazers had a speed from the ledger's first frame pair; 554 had no such pair
and 8 were born too late to count (measured).

| Group | n | Brain changed | Structure changed | Structure, mean summed change |
|---|---|---|---|---|
| Slow (under 0.03 m/s), never ate | 377 | 70 % | 66 % | 28.1 |
| Slow, ate | 184 | 69 % | 65 % | 32.9 |
| Moving, never ate | 525 | 99 % | 43 % | 6.3 |
| Moving, ate | 917 | 99 % | 45 % | 5.7 |

All measured. Every child differs from its parent somewhere. The slow newborns are the ones whose structure changed:
66 % of them against 44 % of the moving ones, and with about five times the summed change, many parts or neurons added
or dropped. Drive, limits, body sizes, cells and the whole-creature dials change at the same rate in every group. So
structure separates slow from moving. It does not separate eaters from the rest: 53 % of those that never ate had a
structural change, against 48 % of those that ate. Speed does separate them: 33 % of the slow newborns ate, against
64 % of the moving ones.

My reading, which is inference: a child whose body plan mutated carries a brain fitted to its parent's body, and so
it barely swims. That accounts for most of the slow third. "Brain changed" reads lower in the slow groups because
the read matches nodes by index. Neurons on a node that was added or dropped count under structure, not brain. Two
limits on this: a node inserted mid-list makes every later node pair with the wrong partner, which inflates the
structure figures, and the 554 newborns without a speed are left out. How often a birth changes the structure is set
by the mutation rates, which are the owner's.
