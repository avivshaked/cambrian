# A grazer's breeding budget at the pace of metabolism

*2026-10-25, the speed agent, for the coordinator. It follows two rulings the owner made that night. The first is the
method: derive the dial values from a breeding target instead of guessing them, the target being two to three
breedings in one life, newborns included, with contact still required. The second is the principle: lives are short by
design, so every action that takes time, swimming and eating among them, must run at the pace of metabolism. The
arithmetic is in `scratch/fin-shape/overshoot/pace.py` and `budget.py`; neither reads a run. Each value is marked
measured (read from a recorded run, a ledger screen or a bench), derived (computed from measured values and the
code), or guessed. One term, a typical grazer's own intake, waits on a read after tonight's render. The table goes to
the coordinator before any pre-registration.*

## The answer in plain words

A grazer's meal takes about 100 s, and its body can afford about 40 s. Nearly all of the 100 s is search: swimming
from one leaf part to the next. Eating is fast. The mouth's cap at an intake scale of 16 is about 2,300 W, two
thousand times what a grazer needs. So the eating-rate dials (the intake scale, the waste) cannot help much, and a
faster swimmer is the lever, as the owner supposed.

It works only if the energy of swimming keeps pace with the speed. At today's work price, swimming's cost rises faster
than its catch, and no speed meets the target. With the work price falling as one over the speed squared, a cruise of
2.4 to 3.4 times today's 0.05 m/s meets it for an adult.

A newborn is a separate problem. It is born with 13.5 J, which lasts about 15 s, and it needs about 96 J to reach a
first meal at today's speed. At the faster cruise it needs about 40 J, still three times what it has. Only the share of
a child's start that is reserve rather than body reaches that, a dial that moves every lineage's children.

## The terms

| Term | Value | Kind | Source |
|---|---|---|---|
| E, kept joules a meal | 46.7 J | measured | `r62bd-x2-6201`'s last bin: 25,689 J kept over 550 mouth blows (`logbook/specs/r62bd-read/bite-cap.txt`, `guilds.txt`). A blow takes a whole leaf part 98% of the time, 76 J offered |
| U, standing cost | 0.908 W | measured | the ledger on the pool grazer: the consumer cell 0.63 W, the claw and the mouth 0.144 W each (`logbook/specs/r62-dial-screens/g1-ledger-dialA.txt`) |
| W, swimming's billed work | about 0.09 W | guessed | about a tenth of the standing cost, round 61's read |
| E/U, the metabolic clock | 46.8 s | derived | the time a meal's joules keep the body going |
| B, one breeding | 175.5 J | measured | the ledger's 1.7604 units a child; by the code, the child's tissue 54 J, its reserve 13.5 J and an overhead of 2 J a tissue joule |
| n, the target | 2.5 | given | the owner's two to three |
| L, a solvent life | 2,000 to 3,000 s | guessed | the age hazard's base of 3,000 s; no grazer in the record lives long enough to measure it |
| s, search between meals | 95 to 107 s | measured | 550 blows by 13 to 22 grazers in 3,000 s; 9.31e-3 kills a grazer-second |
| v, cruise speed | 0.05 m/s | measured | the planted grazers' mean, 0.044 to 0.052 m/s against the local drift (`r61-grazer-economy` section 5) |
| d, food spacing | 4.75 to 5.35 m | derived | s times v, the distance a grazer swims between meals |
| c, the mouth's top rate | about 2,300 W | derived | intake gene 1 × mouth area 1.44 m² × intake scale 16 × 100 J a unit, a second (`WorldContact.cs`, the bite cap). A blow needs two 0.5 s steps to finish an unarmoured part, so a meal's eating takes about 1 s |
| Newborn reserve | 13.5 J | derived | the pool's investment gene of 1.3 reaches the cap on a child's share, so a child is born at full size with a fifth of a 67.5 J share as reserve |

The world is `r62cs-w500`'s at an intake scale of 16 and a bite damage of 1. Its pool grazers, the ten genomes of
`inocula/r59-direction-tops`, share one body: a consumer box with claw and mouth at 1, a hinged link with a power gene
of 10 behind it, a brood of one, a reserve margin of 600 s, lump birth.

## The condition

A meal takes t = d/v + E/c. The grazer nets E/t − U while it lives, and the target is

```
(E/t − U) · L ≥ n · B
```

So the meal may take at most t_max = E / c_min, where

```
c_min = U + n·B/L        (1.14 W at L = 3,000 s, 1.22 W at L = 2,000 s, with today's U and W)
t_max = E / c_min        (40.8 s and 38.4 s)
```

Today a meal takes about 96 to 108 s. The target needs it under 41 s.

**1. The eating rate.** c_min is 1.1 to 1.2 W, and today's cap is about 2,300 W, for the adult and the newborn alike,
since a newborn is born at full size. Eating takes about 1 s of the 41. The cap does not bind, and raising it buys
nothing (derived).

**2. The cruise speed.** The cruise that meets the target is

```
v_min = d / (t_max − E/c)
```

Faster swimming costs work, which raises U and so lowers t_max, so the solution is iterated. The power for a speed k
times today's goes as k³ under quadratic drag. The fin bench's stick, with the law off, fitted k^2.14 over four stroke
powers (`scratch/fin-shape/after/fin-bench*/fin-bench.csv`, measured for a stick, guessed for a grazer). Both are shown.

| Work price | Exponent | v_min at today's c | At c = 2·c_min |
|---|---|---|---|
| As today (`WorkCostMultiplier` 0.5) | k³ | no speed meets it | no speed meets it |
| As today | k^2.14 | no speed meets it | no speed meets it |
| Scaled by 1/k² | k³ | 0.136 to 0.169 m/s, ×2.7 to ×3.4 | 0.34 to 0.44 m/s, ×6.8 to ×8.8 |
| Scaled by 1/k² | k^2.14 | 0.121 to 0.145 m/s, ×2.4 to ×2.9 | 0.24 to 0.29 m/s, ×4.7 to ×5.7 |

The ranges span the two searches and the two lives. At today's work price the iteration has no fixed point: each
faster cruise costs more work than its shorter search saves. The column at c = 2·c_min is a mouth two thousand times
slower than today's, so it is a hypothetical, shown because it was asked for. It shows that a mouth at twice the
minimum would need twice the speed.

The measured cruises to compare: planted grazers 0.044 to 0.052 m/s, other jointed bodies 0.019 to 0.027 m/s, and
rigid bodies, which only drift, 0.021 to 0.024 m/s.

**3. The newborn.** It reaches a first meal only if its reserve covers the trip:

```
reserve ≥ U_newborn · (d/v + E/c)
```

At today's speed that is 96 J against 13.5 J. At the scaled cruise of ×2.4 to ×2.6 it is 38 to 41 J, still about
three times short. The reserve share of a child's start (`NewbornReserveFraction`, 0.2) is the only dial on it. At 0.8
a child is born a quarter the size with 56 J and an upkeep of about 0.3 W, which covers 180 s (derived). It acts on
every lineage's children, leaves included, so it reaches well beyond the grazers.

**4. The physics and its dial.** A body's thrust comes from its joints: the hinge's power gene (10 in every pool
genome) times a drive power scale that is fixed at 1 and bound to no setting. So thrust has no dial. Speed is the
balance of that thrust against the water. The water's settings are in the config's fluid section: the drag
coefficient (1.5), the push a stroking link gives (0.3), the fin law's shaped added mass (D229, off in this world) and
D231's stroke that pushes the whole assembly (built on integration branch 2, off). A resume cannot change the fluid
section, since only the economy and mouth groups may change. Every one of these settings acts on every body, leaves
drifting and sinking included, so I read a speed change as a change of world rule and the owner's (inference). The
work price, `WorkCostMultiplier`, is an economy dial, and scaling it by 1/k² is within the dials.

The fin bench gives the size of the fin law. A paddle at the same stroke power travels 6 to 10 times as far with D229
on, at 3 to 5 times the work a metre. A stick gains 1.3 to 1.45 times. The pool grazer, a box and a hinged rod, is
nearer the stick (inference). D231's bench has not run, so it has no figure yet.

## Every action that takes time, in units of the metabolic clock

E/U is 46.8 s. An action far longer than that, set by a fixed number of seconds rather than by income, is out of pace
with a body that burns a meal in under a minute.

| Action | Time | In E/U | Set by | Kind |
|---|---|---|---|---|
| Search for a meal | 95 to 107 s | 2.0 to 2.3 | encounters: speed and leaf spacing | measured |
| Eat a leaf part | about 1 s | 0.02 | the bite's damage (two steps) and the cap | derived |
| Digest a gut | 0.14 s for a 315 J kill | 0.003 | the mouth's cap, shared with bites | derived |
| Heal a part from near zero | 100 s | 2.1 | `HealingPerSecond` 0.01, paid from the reserve | derived |
| Grow toward adult size | each growth step, 10 s | 0.2 | invests the reserve above a floor at once; income-limited, not a timer | derived |
| Gestate | none for the pool's lump births | | `Lump` mode: a parent pays from its reserve at birth | measured |
| Reach the breeding gate from birth | 720 J of reserve | 15 meals' worth | the price 175.5 J plus the 600 s margin gene | derived |
| A breeding's cycle at the target | about 1,000 s | 21 | B over the net rate | derived |
| The newborn's reserve | 15 s | 0.3 | `NewbornReserveFraction` 0.2 of a capped share | derived |
| The founder's endowment | 600 s of upkeep | 12.8 | `FounderEndowmentSeconds` 600 | measured |
| The reserve margin a parent keeps | 600 s of upkeep | 12.8 | the genome's margin gene | measured |
| Module add | above 300 s of upkeep in the reserve | 6.4 | `ModuleAddReserveSeconds` | measured |
| Module drop | 100 s under 50 s of upkeep | 2.1 | `ModuleDrop*` | measured |
| Remains close | 500 s | 10.7 | `RemainsWindowSeconds` | measured |
| Age hazard | 3,000 s base | 64 | `AgeHazardBaseSeconds` | measured |

Two rows are out of pace in the direction the owner named. Search, at two clocks a meal, is the one that starves
grazers: the target allows 0.87. The newborn's reserve, at 0.3 of a clock, is far too short to cover one search. The
margin gene asks a parent to hold 12.8 clocks of upkeep before it breeds, which is what puts the breeding gate at 720
J; it is a gene and can evolve down, but every pool genome starts at 600 s. Eating and digestion run far faster than
the clock, which is harmless.

## Which term binds, and what is not yet known

- The meal time binds, and within it the search: about 100 s against an allowed 41 s.
- A faster cruise closes it only with the work price scaled down as the speed rises. No speed setting is a dial in the
  economy or mouth groups (above).
- The newborn's reserve binds bred lines separately, and only `NewbornReserveFraction` moves it.
- E and s are world means from the mouth-blow columns, which count every mouth. Each grazer's death row carries what
  its bites kept in its life. Summed over the dead (`scripts/reads/grazer-tally.py`, 2026-10-26), they
  show the mean is the grazers' own but hides two kinds of grazer. In `r62bd-x2-6201` the founder grazers that died
  kept 133,322 J over 219,180 s of life, 0.61 W, the same as the world column. Half of them, 124 of 252, never kept a
  meal at all. Those that ate kept a median of 910 J each. The median-lived founder, at 683 s, kept 49 J. So a grazer
  either finds food and nearly pays its way, or never meets a leaf in its life (measured). I read it as a matter of
  where a founder starts, near an oasis or not (inference). That is a search over distance, and the pace factor
  speeds it up.
- Bred grazers never get that far. In the same world 260 of the 276 that died never kept a meal, and they died at a
  median of 15.5 s, 275 of them starved (measured). In the kill-meal check all 5 bred grazers died the same way, at a
  median of 14.5 s.
