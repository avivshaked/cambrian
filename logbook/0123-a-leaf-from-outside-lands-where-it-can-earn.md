# Round 50's predictions: a leaf from outside lands where it can earn

*2026-09-26, written before round 50's first seed and committed before it starts. Its sources are
round 49's read (0122), the owner's ruling of the morning (D125) and the build on the branch
`leaf-income-depth`. Only the smoke has run in round 50's world, 4,500 s of seed 1. Every number is
round 49's, round 48's or the smoke's, and each says which. A number I guessed says so.*

## What it asks

A founder is a body placed in the world from outside rather than born of a parent. Since round 48
each founder has been set at the depth where its food is richest in the column it lands in. A leaf's
food is the dissolved matter it charges with light. Late in a run the leaves have stripped that
matter from the lit water, so its richest cell lies deep. In round 49 the trickle's leaves were set
there, at a median 44 m down, where the light is under a thousandth of the surface's. They died at a
median 68 to 99 s, and 2 of 1,256 bred. The floor's leaves, set shallow during the founding, lived a
median 679 to 1,432 s, and 36 of 85 bred (0122).

The owner ruled this morning (D125) that a leaf founder is set in the cell of its column where its
own income, light and matter together, is largest. The price is the one the world bills a body with.
A stomach is placed as before. Round 50 asks whether that lets a leaf from outside live and found a
line after the floor has closed. Beside the leaf clauses, it carries round 49's clauses that still
ask something, turns three of them into readings, and adds three that round 49's read asked for. It
also checks two pieces of new code. The placer's clamp now reads the bed under the founder. The
step's serial phases now run across the threads, bit for bit the old build's (0122).

## The world

Round 49's world (0121's world section), with the owner's ruling and one repair, on the branch
`leaf-income-depth`:

- **D125** sets a leaf founder where it earns most. A founder with a leaf lands in the cell of its
  column where its income, light and food together, is largest. Each live 1 m cell of the column is
  priced by the same call that bills a living body. The price is the orientation average, since a
  founder has no pose until it is placed. A founder with a stomach and no leaf is placed as before.
  The report's header reads `founders in their food at its depth, leaves where they earn most`. The
  rule is a tunable, `EVOSIM_FOUNDERS_INCOME_DEPTH`, so it refuses round 49's configs, and it is a
  new realisation of every seed. It places the floor's leaves too, so the founding changes as well.
  In the smoke 24 of 25 floor leaves bred, against 9 of 27 in round 49's seed 1.
- **The clamp's repair** reads the bed under the founder. Round 48's rule held a founder above the
  bed at the tank's centre, 45.02 m down, where it meant the bed under the founder. On round 49's
  tilted bed that held most of the leaves over the deep side at about 44 m (0122). The repair
  changes nothing on a flat floor, and it has no switch.
- **The speed patch** splits the serial phases after the physics step across the world's threads.
  Restored from round 49 seed 2's checkpoint at 29,000 s, it copied round 49's build bit for bit,
  over 3,000 steps at sixteen threads and 200 at one. It ran about an eighth faster (0122).

Everything else is round 49's. The founder cap stays at 0.9 of the gate, the wear stays on upkeep
and the snow is stirred at 0.02 m²/s. Record format 2 stays, with a checkpoint every 100 s. Three
seeds of 30,000 s run at dt 0.01 from `rounds/env-r50.ps1`. They run one at a time at sixteen
threads, overnight, under the machine's ruling of 2026-09-25.

## Predictions for the leaf founders

A leaf founder here is a founder whose birth row has `pho` 1 and `abs` 0. Round 49's figures are
0122's, and the round 50 reader gives the same figures on round 49's seeds (its output is under
logbook/specs/r50-read/). The smoke's are from 4,500 s of seed 1, whose trickle ran for its last
1,500 s.

| # | prediction | falsified by |
|---|---|---|
| L1 | **a leaf from the trickle lands in the light**: the median first recorded height of the trickle's leaf founders is above −12 m in 3 of 3, with the floor's beside it (round 49: −44.0, −44.1 and −43.6 m, with 296 of 308, 327 of 336 and 281 of 285 below 20 m; the floor's −7.7, −4.0 and −7.4 m; the smoke: −3.1 m, 0 of 28 below 20 m; the floor's −0.9 m) | the first positions row within 10 s of each founder's birth |
| L2 | **it lives**: the trickle's leaf founders' median age at death is above 300 s in 3 of 3 (round 49: 81, 99 and 68 s; the floor's 1,241, 1,432 and 679 s; the smoke: 359 s over the 10 of 30 dead by 4,500 s, a median that leaves out the living; the floor's 2,341 s; the bar is a guess) | death rows joined to the founder rows |
| L3 | **it breeds**: pooled over the seeds, at least 5% of the trickle's leaf founders have a child (round 49: 2 of 1,256; the floor's 36 of 85; the smoke: 3 of 30; the floor's 24 of 25; the bar is a guess) | birth rows against the founder rows |
| L4 | **a line from outside lasts** (a reading): the three largest lines rooted in a trickle founder at 30,000 s, each with its living count, its founder's guild and the second it landed (round 49: the largest held one living body in every seed) | the parent walk over `lineage.jsonl` |
| LC | **the repaired clamp and the rule are what ran** (a check of the build): the header carries `leaves where they earn most`, and fewer than five matter-only founders sit within 1 m above the centre's bed while over a bed more than 1 m deeper, in 3 of 3 (round 49: 147, 185 and 192 of 258, 286 and 239 over the deeper bed; the smoke: the token present, and none of the 42 matter founders over a deeper bed within 1 m above the centre's) | the report's header; the founder rows against their first positions row |

L1's bar sits under the floor's leaves of round 49, which were set by the old rule during the
founding, when the lit water still held its matter. Light fades by a factor of e every 6 m, so at 12
m a leaf has about 14% of the surface's light. I set the bar that deep because a trickle leaf lands
under a sheet of leaves. The sheet shades it and has stripped the matter above it, so its best cell
may lie below the sheet. That is a guess, and the smoke's figure beside it is from 1,500 s of
trickle in a young world.

## The clauses carried from round 49

Each keeps 0121's wording and bar except where the table says otherwise. Round 49's figures are
0122's. D125 and the clamp's repair make every seed a new realisation, so no clause compares a seed
with round 49's same seed. The reader prints round 49's value beside O1, S1 and X1 for context.

| # | prediction, in short | bar | round 49 |
|---|---|---|---|
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow; **the leaves' pair is now a reading**, since D125 no longer sets a leaf at its matter's richest cell | 3 of 3 | 1 miss in 297 in seed 1 (a body held off the surface by its size), none in 2 and 3 |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 3 of 3 | 0.21, 0.20, 0.17 |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 3 of 3 | 2.18, 1.79, 2.47 |
| G1 | births paid from a gestation account | 3 of 3 | 2,493, 7,408, 3,560 |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 2 of 3 | 0.175 → 0.101, 0.160 → 0.125, 0.167 → 0.138 |
| G3 | accounts held at death at least a fifth of what was banked | 2 of 3 | 51%, 37%, 45% |
| G4 | banked, less held, per gestation birth at least 50 J | 3 of 3 | 65, 56, 69 J |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | held |
| C3 | no starved body dies solvent | 3 of 3 | held |
| B1 | audit under 0.1 J, matter residual under 1e-5 units, nothing diverged, ends on its budget | 3 of 3 | at most 1.3e-3 J and 3.7e-6 units |
| V1 | a faithful film window from the round's own checkpoints | one per seed | 11 of 11 rows in each |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | seed 1 | the resume agreed; the check named a member it should have skipped, since repaired |
| M1 | at least 100 births carrying a bud | 3 of 3 | 518, 1,168, 541 |
| M2 | at least three quarters of bud births built the bud | 3 of 3 | 99%, 98%, 94% |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 1 of 3 | 11, 321, 1,273 |
| M4 | in each such line the stomach brings under 2% of the income | every seed M3 holds in | failed in seed 2, 2.7% |
| O1 | alive at the end between 4,000 and 16,000 | 3 of 3 | 8,410, 10,810, 7,220 |
| O2 | under 25,000 alive at every sample | 3 of 3 | at most 10,825 |
| S1 | mean age at the end above 1,500 s | 3 of 3 | 1,954, 2,157, 1,551 s |
| F3 | no line rooted in a pool founder has ten living at the end | 3 of 3 | the largest 1 |
| P1 | the whole run at or above real time | 3 of 3 | 2.55x, 1.57x, 2.12x |
| EK1 | no founder has a child within 1 s of landing | 3 of 3 | none |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 3 of 3 | 17.76 J |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 2 of 3 | 371, 359, 338 s |
| EK4 | at most a fifth of them breed, and those that do have their first child a median 10 s or more after landing | pooled | 1 of 63 |
| EK5 | all pool founders' median age at death above 300 s | 3 of 3 | 391, 333, 348 s |
| EK6 | the pool founders that bred landed in richer snow (a reading under five breeders) | pooled | 3.99 against 0.95 J/m³ |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 3 of 3 | 0.66, 0.57, 0.63 |

C1 (the senses' neighbours), E1 (what the endowment buys), EK7 (what the cap takes) and R1 (the
reef's two readings) stay readings, read as 0122 read them.

O3, V3 and S2 become readings. O3 asked for fewer than 100 bodies with a joint at the end. In round
49 the joints followed the founding lottery. A tank kept them if the founder that won it had one, so
the count said more about the draw than about selection (0122). V3 asked for a seed's folder under 2
GB. It was a guess three times short, and the owner's ruling on thinning the checkpoints answers the
size. S2 asked that stomach children die young under the wear. Its "stomach child" counts every
stomach on a link, which catches light, so it read part plants.

Three clauses are new, each asked for by round 49's read.

| # | prediction | falsified by |
|---|---|---|
| O4 | **the joints are selected down within the winning line**: in the line with the most living at the end, the share of members with a joint (birth row `jnt`) at the end is below its share at 16,000 s, in every seed where that line was at least a fifth jointed at 16,000 s (round 49: seed 3's line 0.85 to 0.03; seeds 1 and 2 under a fifth at 16,000 s, not asked) | the parent walk and the living ids at each second |
| S3 | **a rigid stomach child dies young**: children with `abs` 1, `pho` 0 and `jnt` 0 have a median age at death under 1,500 s, in every seed with ten of them dead (round 49: 3 dead in seed 1, not asked; 2,750 s over 105 in seed 2, most of them line 48's members after they lost the link; 906 s over 12 in seed 3; the bar is 0121's S2 bar) | death rows joined to birth rows |
| E2 | **the eater lines** (a reading): every line with ten or more pure stomachs (`abs` 1, `pho` 0), its founder's guild, source and landing second, its members and those alive at the end (round 49: none, line 48, lines 29 and 5854) | the parent walk over `lineage.jsonl` |

## The two-sided readings

- If L1 fails, the rule set the trickle's leaves deep even when priced by their own income. The
  matter below the sheet then outweighs the light above it in the bill, which would mean the lit
  water is stripped almost bare. Read each leaf founder's `fmat` against its column's mean, and the
  matter at the surface cells, at the seconds the leaves landed.
- If L1 holds and L2 fails, the leaves land in the light and still die fast. The sheet above them
  shades them and has stripped the matter they need, so a leaf from outside cannot compete with the
  crowd already there. The next question is then where a newcomer can earn at all, and that is
  between columns rather than within one.
- If L2 holds and L3 fails, a leaf from outside lives on its start and its income but never saves
  the price of a child. Read its net income against the gate it has to reach.
- If L3 holds, leaves from outside breed after the floor has closed, where round 49's bred about
  once in 600. L4 then says whether any line they found lasts.
- If LC fails, the header lacks the token or the clamp's pile-up is back, which is a fault in the
  build. Every leaf clause is then unread until it is found.
- If O4 fails, a winning line kept its joints from 16,000 s to the end, and something in that line
  pays for a joint. Read the jointed members' brains for the contact channel (0122's O3 follow-up)
  and their feeding rows.
- If S3 fails, the rigid stomach children outlive the wear's bar. If they come from a line that lost
  its link, they are living on what their parents gave them, as line 48's did (0122).

## What the round does not ask

Whether a link should catch light. In this world it does, at half a leaf's rate, so every stomach on
a link is part plant. Round 49's read found that the eater lines that lasted were all such bodies.
That is a world rule and the owner's to rule. Whether the pool should keep its one-part stomach,
which earned its last tenth once in 63 landings. That is the owner's too. Whether a mouth should
read a neighbourhood rather than its own cell, and whether a reactive thrust term would let a stroke
move a body. Both are proposals for a later round.

## The reader

The round's reader is `scripts/reads/r50-read.py`, built from round 49's and committed before this
entry. It reads every carried clause as round 49's reader did. W1 asks the stomachs alone and prints
the leaves' pair as a reading. It adds L1 to L4, LC, O4, S3 and E2 as above, and it turns O3, V3 and
S2 into readings. Run on round 49's three seeds against round 48's, it prints the numbers in this
entry's tables, and every clause it carries reads as round 49's reader read it. Its output is under
logbook/specs/r50-read/. A clause it cannot read prints as absent and never as a pass.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree. Every seed's
manifest names it as `gitCommit`, with no DIRTY mark beside it. The build is the branch
`leaf-income-depth` at `d6c095d`, merged with main at `fdb6610`, and the commit that adds this entry
changes only prose on top of it. It carries D125, the clamp's repair and the speed patch on top of
round 49's build. The checks ran from the build script under scratch/r50-build/, one heavy job at a
time beside two short runs.

- The Core suite passed 995 of 995 in its default set, and its thread-identity test at one, four and
  sixteen threads (the word `c9b0cabce249c1dd` at each). The Farm suite passed 175 of 175, and the
  Dynamics suite 117 of 117 on the new crowd fixture.
- In D125's own tests a leaf lands in the cell its bill prices highest, and where the richest matter
  lies in the dark the rule sets it shallower. With the rule on, a stomach is still placed by round
  48's rule. The rule is refused without the depth rule it refines.
- Round 42's config fixture was re-recorded on this build (`pfix13`), since the new tunable refuses
  every earlier config. The Farm tests' pinned hash for it moved, and the thread-identity word did
  not. The crowd fixture is round 44's world run 20,000 s on this build with every new rule off
  (`r50fix-s4`).
- Seed 1 of round 50's launcher ran 4,500 s on four threads. Its header carries D125's token, and
  every snow founder landed at or over its column's mean (24 of 24). The books closed: the audit
  read at most 2.3e-6 J and the matter residual 2.3e-8 units. No founder had a child within ten
  seconds of landing. The leaf figures are in the table above. The member check on its 2,000 s
  checkpoint passed. A resume from it to 3,000 s was identical to the run on all 100 shared samples.
  The wall-clock fields were left out, as round 49's V2 left them. The smoke and its resume are
  `runs/r50smoke-s1` and `runs/r50smokeR-s1`, on `configHash a2cda4b0…`.
