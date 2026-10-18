# Round 53's predictions: a shallower tank feeds the bed

*2026-09-29, written before round 53's first seed and committed before it starts. Its sources are
round 52's read (logbook/specs/r52-read/read-30000.txt, on the branch `round-52`), the owner's
ruling D132, the four founding screens it asked for (logbook/specs/r53-screens.txt) and the build
on the branch `round-53`. The owner delegated round 53's decisions to me for the night of
2026-09-29, telling me to make the decisions on round 52 and round 53. Every number is round 52's or a screen's, and each
says which. A number I guessed says so.*

## What it asks

Round 52 sent the founders that carry a mouth to the corpses. They landed there: in every seed the
median mouth founder's column held about six times the corpse energy of a column drawn at random,
and every one of them landed over a corpse. They ate, 170, 114 and 129 corpses in 30,000 s against
one a seed in round 51. But a meal bought little. The mouth founders lived a median of 141, 118 and
155 s, 1.3, 1.0 and 1.4 times round 51's, and no line rooted in a mouth had more than one living
member at the end. Jointed bodies were nearly gone in two seeds (8 and 16 at the end, 219 in seed
1). The lifespan gene rose in seeds 1 and 3 (by 0.19 and 0.11) and fell in seed 2 (by 0.09).

The owner ruled D132 after the first two seeds. Movement is the wall, and it is round 54's question:
a mouth that cannot swim to its next meal cannot live on corpses, however well it lands. Round 53
changes one thing, the matter within the crowd's reach, so that round 54 tests movement in a world
that has more to find. Four founding screens of 10,000 s chose how. The tank 25 m deep beat the larger budget on the bed's snow and on the leaves' uptake at every screen sample from 5,000 s, so round 53 is the 25 m tank.

Round 53 asks three things. Does the bed's water get richer where the mouths and stomachs feed
(N1)? Are the leaves less bound by what the water brings them (N2)? And does the eating of corpses
hold at round 52's level or above (N3), which D132 made a condition? Beside these it carries round
52's clauses that still ask something.

## The world

Round 52's world (0127's world section), with one change, from `rounds/env-r53.ps1` on the branch
`round-53`:

- **The tank 25 m deep** (`EVOSIM_DEPTH` 25, from 45), with the bed's tilt 41 m (from 96), chosen so that the bed shallower than 6 m and 12 m is 1.1% and 12.5% of it, against round 52's 4.7% and 10.0%. The matter stays at 15,000 units, so the water holds it about 1.8 times as densely. Founders' depth, the islands' depth and the reefs' caps stay in metres from the surface, since the light is.
- **D131's settled decay, built and off.** A corpse that has settled on the bed or a reef can decay
  at its own rate (`EVOSIM_CORPSE_DECAY_SETTLED`). D132 set it aside, so it is 0, which means the
  sinking rate, and the header's corpse token ends `settled decay as sinking`. Every recorded world
  replays with it off.

The lifespan gene's values are round 52's and hold through rounds 53 and 54 (D132): a price of 0.5,
a chance of 0.08, and founders from 0.5 to 1.5. Everything else is round 52's. Three seeds of
30,000 s run at dt 0.01, one at a time at twelve threads, overnight (the nursery takes four beside them), with a checkpoint every 100 s
and the snow's transport on the processor. The change is a new realisation of every seed, so no
seed replays round 52's.

## The screens

D132 named the screens and the rule that reads them. Each is seed 1 of round 52's world with one
change, 10,000 s on four threads. The tank 30 m deep and 25 m deep keep the bed's share shallower
than 6 m and 12 m near round 52's, and hold the matter at 15,000 units. The budget screen keeps the
45 m tank and raises the matter to 27,000 units, the density a 25 m tank would have. The fourth
keeps round 52's world and makes a settled corpse decay at 0.005 a second, the rule D132 set aside,
so that setting it aside rests on a measurement. The rule: depth is taken only if it beats the
larger budget on the bed's water and on the leaves' uptake. Otherwise round 53 takes the budget,
the one knob that keeps the tank's geometry comparable with rounds 44 to 52.

In the table, read the bed's snow and the leaves' uptake-limited share against round 52 seed 1 at
the same seconds. The bed's snow is the mean over the live bed columns of the marine snow (dead
matter falling through the water, the stomachs' food) in the cell layer on the bed, in joules a
cubic metre. `upt lim` is the share of leaf-steps limited by what the water carries rather than by
light.

| screen | t (s) | alive | jointed | `upt lim` | corpses eaten | bed's snow (J/m³) |
|---|---|---|---|---|---|---|
| round 52 seed 1 | 5,000 | 1,078 | 360 | 0.851 | 21 | 0.508 |
| | 10,000 | 1,053 | 203 | 0.733 | 46 | 0.494 |
| budget 27,000 units | 5,000 | 2,455 | 929 | 0.801 | 19 | 0.986 |
| | 10,000 | 2,199 | 323 | 0.714 | 48 | 1.048 |
| 30 m | 5,000 | 1,588 | 795 | 0.783 | 13 | 0.809 |
| | 10,000 | 1,901 | 918 | 0.728 | 58 | 0.919 |
| 25 m | 5,000 | 2,065 | 917 | 0.763 | 5 | 1.073 |
| | 10,000 | 2,467 | 937 | 0.705 | 52 | 1.296 |
| settled decay 0.005 | 5,000 | 1,329 | 316 | 0.808 | 9 | 0.729 |
| | 10,000 | 2,408 | 16 | 0.629 | 25 | 0.631 |

I averaged them as the round's reader does, over 5,000 to 10,000 s. The 25 m tank's bed held 1.20 J/m³ of snow against the budget's 1.08 and round 52 seed 1's 0.53. Its leaves were bound 0.704 of the time against 0.728 and 0.734. It led the budget on both at 5,000, 7,500 and 10,000 s, so the rule takes depth. The 30 m tank came behind both on the bed. The uptake margin is small beside the bed's: the 25 m tank's leaves were 0.03 less bound than round 52's, where its bed was more than twice as rich. The budget screen was stopped at 6,840 s and continued from that checkpoint at twelve threads, and its two parts were joined at that second (`r53sc-f27j`). The fourth screen supports D132's setting aside of fast decay once settled. With a settled corpse decaying at 0.005 a second, the jointed bodies fell to 16 by 10,000 s and the corpses eaten to 25, the fewest of the four. All the screens are in logbook/specs/r53-screens.txt.

## Predictions

Seed 1's first 10,000 s in round 53 will be the 25 m tank's (`r53sc-b25`) screen's, since the build and the world are
the same and a checkpoint's cadence moves no hash. Those seconds are seen. N1 and N2 are asked over
5,000 to 30,000 s, and seed 1's first 5,000 s of that window are already known.

### More matter within reach

| # | prediction | falsified by |
|---|---|---|
| N1 | **the bed's water is richer**: the mean over 5,000 to 30,000 s of the bed's snow is at least 1.5 times round 52's same seed's, in 3 of 3 (round 52: 0.50, 0.65 and 0.55 J/m³; the screen: 1.20 over 5,000 to 10,000 s, 2.26 times round 52 seed 1's 0.53 over the same seconds) | the snow-floor dumps |
| N2 | **the leaves are less bound**: the mean `upt lim` over 5,000 to 30,000 s is at least 0.015 under round 52's same seed's, in 3 of 3 (round 52: 0.72, 0.68 and 0.73; the screen: 0.704 over 5,000 to 10,000 s, 0.031 under round 52 seed 1's 0.734 over the same seconds. Round 52 seed 1's mean falls to 0.719 over the whole window, so the bar asks half the screen's margin, and I expect it to be the closest of the three) | the stats rows |
| N3 | **the corpses are still eaten**: `corpsesEaten` at 30,000 s is at or above round 52's same seed's (170, 114 and 129), in 2 of 3, and the three seeds' total is at or above 413. D132 asks it of the round; seed to seed the count moved by a third in round 52, so one seed may fall short by chance (the screen: 52 by 10,000 s against round 52 seed 1's 46) | the last stats row |

### Clauses turned

K4 asked a meal to lengthen a mouth founder's life by half. It failed in all three seeds, and round
53 changes nothing about a mouth, so it becomes a reading, read against round 52's same seed. K5
asked a line rooted in a mouth to last, and failed in all three; it waits for movement and becomes a
reading too. EK1 asked that no founder have a child within 1 s of landing. It held in round 51 and
failed in all three seeds of round 52 (6, 11 and 6 founders, all from the trickle), and nothing in
round 53 touches it, so it is a reading. A clause expected to fail asks nothing.

## The clauses carried from round 52

Each is asked as 0127 asked it, with round 52's same seed as the baseline where one is named. Round
52's seed 3 is read from its joined run `r52-s3j`: the machine crashed at 23,200 s, and the run was
continued from its checkpoint and joined to its first part at that second (the continuation agrees
with the run on every shared sample). Its pace and its checkpoint count are the join's and are not
read.

| # | prediction, in short | bar | round 52 |
|---|---|---|---|
| K1 | the mouth founders from 3,000 s land where the median column holds at least five times a random column's corpse energy | 3 of 3 | 6.0, 5.7, 5.7 |
| K2 | at least nine in ten of them land over a corpse | 3 of 3 | all, in every seed |
| K3 | at least 30 corpses eaten by 30,000 s | 3 of 3 | 170, 114, 129 |
| LS1 | every birth carries the gene, and the median age at an aged death rises across its three bins | 3 of 3 | held |
| LS2 | the mean gene of the children born from 3,000 to 8,000 s in 0.85 to 1.1 | 3 of 3 | 0.98, 0.79, 1.03 |
| LS3 | the mean gene of the last 5,000 s at least 0.1 above that of 3,000 to 8,000 s | 2 of 3 | +0.19, −0.09, +0.11 |
| LS4 | the living's mean gene at the end under 2.5 | 3 of 3 | 1.19, 0.70, 1.15 |
| A1 | deaths by age at least a fifth of the dead at the end | 3 of 3 | 0.40, 0.52, 0.34 |
| A2 | the median age at an aged death in 1,500 to 4,500 s | 3 of 3 | 2,290, 1,894, 2,525 s |
| A3 | every aged death holds a reserve, and the stats count equals the rows | 3 of 3 | held |
| B1 | the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, the run ends on its budget | 3 of 3 | held |
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow | 3 of 3 | 2 of 3; two founders in seed 3, about 1% under |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 3 of 3 | 0.17, 0.19, 0.14 |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 3 of 3 | 2.14, 2.28, 2.37 |
| G1 | births paid from a gestation account | 3 of 3 | 1,901, 6,147, 6,531 |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 2 of 3 | 0.13 → 0.11, 0.19 → 0.15, 0.23 → 0.12 |
| G3 | accounts held at death at least a fifth of what was banked | 2 of 3 | 54%, 37%, 38% |
| G4 | banked, less held, per gestation birth at least 50 J | 3 of 3 | 65, 55, 55 J |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | held in seeds 1 and 2; seed 3's are split by the crash |
| C3 | no starved body dies solvent | 3 of 3 | held |
| V1 | a faithful film window from the round's own checkpoints | one per seed | not recorded |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | seed 1 | held |
| M1 | at least 100 births carrying a bud | 3 of 3 | 518, 359, 686 |
| M2 | at least three quarters of bud births built the bud | 3 of 3 | 95%, 97%, 99% |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 1 of 3 | 20, 5, 10 |
| M4 | in each such line the stomach brings under 2% of the income | every seed M3 holds in | 1.0%, 0.3% |
| O1 | alive at 30,000 s between 2,000 and 16,000 | 3 of 3 | 3,431, 2,813, 5,691 |
| O2 | under 25,000 alive at every sample | 3 of 3 | at most 5,705 |
| F3 | no line rooted in a pool founder has ten living at the end | 3 of 3 | the largest 1 |
| P1 | the whole run at or above real time | 3 of 3 | 5.1x, 4.3x; seed 3's is the join's |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 3 of 3 | 17.76 J |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 2 of 3 | 335, 358, 357 s |
| EK4 | at most a fifth of them breed, and those that do have their first child a median 10 s or more after landing | pooled | 2 of 63 |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 3 of 3 | 0.49, 0.64, 0.53 |
| LC | the header carries `leaves where they earn most`, and the clamp's pile-up is absent | 3 of 3 | held |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | every such seed | not asked in any seed |
| S3 | a rigid stomach child dies young, median under 1,500 s, where ten have died | every such seed | failed in seed 3 (2,068 s over 229), not asked in 1 and 2 |
| H1 | the corpses' joules over ρ at least a tenth of the standing matter | 3 of 3 | 0.127, 0.146, 0.125 |
| H2 | at least a quarter of the corpses' joules settled | 3 of 3 | 0.78, 0.80, 0.76 |
| H4 | the corpses standing lie between the deaths of the last 7,000 s, less those eaten, and those of the last 9,000 s | 3 of 3 | held |
| E3 | a line has ten or more living pure stomachs at the end | 1 of 3 | 1, 1, 12: held in seed 3 |

K4, K5, K6, EK1, LS5, C1, V3, O3, S1, E1, R1a and R1b, EK5 to EK7, S2, L1 to L4, E2, A4, H3, H5, LK1
and LK2 are readings.

## The two-sided readings

Two readings could go either way, and I set no bar on them. The crowd: the 25 m screen held 2,467 alive at 10,000 s against round 52 seed 1's 1,053. And the jointed bodies: 937 against 203. A richer bed may carry more of both, or the crowd may strip it. I read the count and the jointed count at 30,000 s against round 52's same seed, and say which way each went.

## What the round does not ask

Whether a body can swim to food. That is round 54's question, and its rules are built and being
benched on the branch `forage` (propose-foraging.md, approved by the owner on 2026-09-29): a limb
swinging through the water is pushed along its length, a stroke's work is charged to the reserve, and
a body can smell a corpse to about 10 m. Whether the lifespan rules should change: D132 holds them
while seed 2's fall is read. Whether a settled corpse should decay faster: the fourth screen measured
it, and D132 set it aside.

## The reader

The round's reader is `scripts/reads/r53-read.py`, round 52's reader with round 52's same seed as the
baseline (`r52-s3j` for seed 3), K4, K5 and EK1 read as readings, and N1 to N3 added. N1 and N2
average over the same seconds in the seed and in its baseline, and N3 prints a pooled row beside the
three. I ran it on the 25 m screen against round 52 seed 1. It read N1 at 2.26 times and N2 at a drop of 0.031, both over 5,000 to 10,000 s in both runs, and N3 at 52 corpses by 10,000 s, all three marked short. Until that check it averaged the baseline over the whole window whatever the seed covered; a seed that stops early is now compared with the same seconds of its baseline. A clause it cannot read prints as absent and never as a pass.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree. Every seed's
manifest names it as `gitCommit`, with no DIRTY mark beside it. The build is the branch `round-53`,
launched from its worktree `scratch/wt-r53` into the main tree's runs.

The build is the screens' build: between the screens' commit (`d0c4b4c`) and this entry's, only tests, fixtures and reads changed, and the budget screen's continuation from the branch's head recorded the same three source hashes as the screens (`coreHash e13be1b2…`, `dynamicsHash d675fb37…`, `farmHash d1fead5d…`). The 25 m screen's `configHash` is `7c5e42d924a9832d`; seed 1's manifest is checked against it at the launch, since a checkpoint's cadence moves no hash.