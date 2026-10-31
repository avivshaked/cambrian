# Round 50's read: leaves from outside lived and bred in the light, and a line of stomachs lived on the snow alone

*2026-10-31, written by the writing lane from the committed read, about five weeks after round 50 ran. The
pre-registration is logbook/0123. The read is `logbook/specs/r50-read/read-end.txt`, the whole output of the
round's own reader, with `linklight.txt` beside it, both made on 2026-09-27. Round 51's predictions (0125) were
written from the same read while this entry waited for its pictures. Every figure is measured unless it says
otherwise.*

Round 50 tested one new rule. A leaf brought into the world from outside would land at the depth where its own income
was largest, instead of where its dissolved food was richest. In round 49 those leaves had landed about 44 m down, in
the dark, and nearly all died within two minutes. In round 50 they landed about 5 m down in every seed. They lived a
median of 321 to 577 s, and 66 of 1,248 had a child. Every leaf clause held, the breeding bar by a narrow margin. No
family they started lasted to the end. The round's other finding came from a reading. In seed 1 a line of stomachs
with no leaf and no joint lived on the falling snow alone for about four hours of simulated time. D127 cites it when
it takes the light off the link in round 51.

## Round 50 moved the outside leaves up into the light

A few words first, for a reader new to the project. Round 50's world is round 49's, a round tank of water 167 m across
whose floor slopes from a beach 1 m deep to about 93 m. A leaf is a part that earns energy from light, and its food is
the dissolved matter it charges with that light. A stomach earns energy by eating the snow, the dead matter that
drifts down through the water. A link is a part that carries a joint. In this world it also catches light, at half a
leaf's rate. Light fades by a factor of e every 6 m.

A founder is a body placed in the world from outside, with no parent. For the first 3,000 s the population floor adds
random founders whenever the count runs low. After that the trickle drops in one random founder every 30 s, and one in
ten comes from the pool, four stomach bodies stored from earlier rounds. A line is a founder with all its descendants.

In round 49 the trickle's leaves were set where their dissolved matter was richest. Late in a run that lies deep, and
a fault in the placer held most of them at 44 m, where the light is under a thousandth of the surface's. They died at
a median of 68 to 99 s, and 2 of 1,256 had a child (0122). On the morning of 2026-09-26 the owner ruled a new rule
(D125). A leaf founder lands in the cell of its column where its own income, light and matter together, is largest.
The placer's fault was repaired in the same build.

Round 50 was pre-registered that day in 0123. It asked whether a leaf from outside now lands in the light (L1), lives
(L2) and breeds (L3), and it read whether any line such a leaf starts lasts (L4). LC checked that the new rule and the
repaired placer were what ran. Beside these it carried 28 clauses from round 49 with their bars, seven readings, and
three new clauses, one of them a reading. The bars for L2 and L3 were marked as guesses. The round's reader,
`scripts/reads/r50-read.py`, was committed before the launch.

## Three seeds ran overnight, and one early family took each tank

The three seeds ran one after another on the night of 26 to 27 September, each for 30,000 simulated seconds on sixteen
threads. All three ended on their budget. They ran at 2.80, 2.12 and 2.37 times real time, so each took three to four
hours of wall time (my arithmetic from the pace).

As in round 49, one family from the founding took each tank. In seed 1 it was line 39, from a leaf that landed at 15.5
s, and at the end it held 5,775 of the 6,131 bodies alive. In seed 2 it was line 14, from a leaf with a joint that
landed at 4 s, with 9,697 of 9,823. In seed 3 it was line 18, a leaf with a joint landed at 5.5 s, with 9,907 of
9,914. The crowds at the end were 6,132, 9,824 and 9,917, inside the pre-registered band. All 163,129 deaths were
starvations.

The books closed. The energy audit stayed under 4e-4 J in every seed, and no body went non-finite or left the tank.
Seed 1 was restored from its checkpoint at 15,000 s, a saved copy of the whole world, and run on to 16,000 s. It
matched the original on all 100 shared samples (`logbook/specs/r50-read/v2.log`).

## The trickle's leaves landed near 5 m, lived and bred, and no line of theirs lasted

The trickle's leaves landed at a median of 5.1, 5.5 and 5.2 m down. Not one of the 1,152 whose first position was
recorded lay below 20 m. By 0123's fade, a leaf at 5 m gets about 43% of the surface's light before any shading (my
arithmetic). The floor's leaves, set during the founding, landed under 1 m. The rule traded matter for light. Only 32
of 434, 38 of 434 and 32 of 460 leaf founders landed at or above their column's mean of dissolved matter.

They lived. The trickle's leaves died at a median of 577, 421 and 321 s, against round 49's 81, 99 and 68 s. The
floor's leaves still lived far longer, a median of 2,144 to 2,744 s. Seed 3 cleared L2's bar of 300 s by 21 s.

They bred. Pooled over the seeds, 66 of 1,248 trickle leaves had a child, 5.3% against a bar of 5%. Round 49's had
managed 2 in 1,256. The floor's leaves did far better again, with 73 of 80 having a child.

No line they started lasted. At 30,000 s, 10, 7 and 8 lines rooted in a trickle founder were alive, and none held ten
living bodies. The largest in each seed held one, and its founder had landed in the last 1,070 s of the run. A leaf
from outside could now pay for a child, but its family did not keep a place beside the founding's. I don't know why,
and no read asked. My guess is that the founding's crowd holds the lit water, as 0123 imagined for a different
outcome.

LC held. The report's header carried the rule's token in all three seeds. Of the founders that eat dissolved matter
and sat over a bed deeper than the centre's (324, 296 and 324), none was held within 1 m above the centre's bed. In
round 49, 147 to 192 had been.

<!-- frame: r50-s1, 30000, the lit sheet near the surface with line 44440's one living body, a trickle leaf landed at
28,930.5 s -->

## A line of stomachs with no link lived on the snow for four hours

In seed 1, at 4,145.5 s, the trickle dropped a stomach with no leaf and no joint, line 4292's founder. It landed on
snow at 6.13 J/m³, where its column's mean was 1.43 J/m³. Its line grew to 132 members, every one a pure stomach. It
peaked at 68 living at 9,595.5 s and had its last birth at 13,170.5 s. Its last member starved at 18,714 s. Over
39,998 feeding rows it earned no light at all (`linklight.txt`).

Round 49's eater lines that lasted had all been stomachs on a lit link, so they were part plant (0122). Round 50 had
two such lines as well. Seed 1's line 3726, with 12 members, took 75.7% of its income from the link's light. Seed 3's
line 29, a floor founder from 9.5 s with 121 members, took 27.9%. None of the three eater lines had a member alive at
the end.

A ledger check made the same day put numbers on line 4292's founder (`logbook/specs/r51-husk-ledger.md`). It breaks
even at 0.44 J/m³ of snow at its mouth, and needs about 1 J/m³ to more than replace itself. It landed on six times
that. So a stomach with no light could live where the snow was rich. That is an inference from the ledger and the
landing, since no read followed its members' positions.

S3 failed because of this line. S3 asked that rigid stomach children, with no leaf and no joint, die at a median under
1,500 s. In seed 1, 137 had died, at a median of 3,005 s. The line had 132 pure stomachs and a rigid founder, so I
think most of the 137 were its members (inference). The bar was set to catch children dying young under the wear,
D038's rise in upkeep with age. These lived twice as long as the bar on the snow alone. Seeds 2 and 3 had fewer than
ten dead and were not asked.

Reading the reads five weeks on, this is the part of round 50 I would most like to see in the theatre. A body that did
nothing but eat lived for hours on what fell to it.

<!-- frame: r50-s1, 12000, line 4292's rigid stomachs on rich snow --> <!-- frame: r50-s3, 6000, line 29's stomachs on
a lit link, for contrast -->

## Thirty clauses held, three failed, one went unread and nine are readings

S3 is above. W1 failed in seed 2, on two of the 309 snow founders checked. One landed at 7.55 J/m³ against its
column's mean of 7.95 J/m³, the other at 3.140 against 3.143. Both lay within 1.5 m of the surface, where the placer
holds a body off the surface. I think that is why, as with round 49's one miss, but no read checked it.

M4 failed in seed 3. A bud is a small welded copy of a part with another type, added by mutation (D119). A line that
began when a stomach budded onto a leaf body had 25 living, and their stomachs brought a median 5.4% of their income.
The bar was under 2%. The world disagreed in the interesting direction, with the budded stomach earning more than the
pre-registration bet.

V1 asked for a faithful film window from each seed's own checkpoints. No windows had been cut when the reader ran, so
it was not read, and 0125 records that it waits for them.

The table gives each clause with 0123's bar and the number that decides it in each seed. Read the verdict column
first. Gestation, the bud and the founder cap are explained in 0122. The count of verdicts in this section's header is
my tally from the reader's summary and the bars.

| # | prediction, with 0123's bar | seed 1 | seed 2 | seed 3 | needs | verdict |
|---|---|---|---|---|---|---|
| L1 | a leaf from the trickle lands in the light: the median first recorded height of the trickle's leaf founders is above −12 m | −5.14 m | −5.51 m | −5.24 m | 3 of 3 | holds |
| L2 | it lives: the trickle's leaf founders' median age at death is above 300 s | 577 s | 420.5 s | 320.75 s | 3 of 3 | holds |
| L3 | it breeds: pooled over the seeds, at least 5% of the trickle's leaf founders have a child | 30 of 409 | 18 of 408 | 18 of 431 | pooled | holds, 66 of 1,248 (5.3%) |
| L4 | a line from outside lasts (a reading) | 10 lines alive, largest 1 | 7, largest 1 | 8, largest 1 | a reading | none reached ten |
| LC | the header carries the token, and fewer than five matter-only founders sit within 1 m above the centre's bed while over a deeper bed | token, 0 | token, 0 | token, 0 | 3 of 3 | holds |
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow | 0 of 317 below | 2 of 309 | 0 of 268 | 3 of 3 | fails, 2 of 3 |
| W2 | a pool founder's first mouth reading over its landing density, the median in 0.02 to 0.3 | 0.22 | 0.18 | 0.17 | 3 of 3 | holds |
| W3 | the landing cell over its column's mean, the median at least 1.6 | 2.34 | 2.14 | 1.97 | 3 of 3 | holds |
| G1 | births paid from a gestation account | 5,386 | 5,728 | 4,455 | 3 of 3 | holds |
| G2 | the share of gestating children lower in the last 5,000 s than in 3,000 to 8,000 s | 0.275 → 0.137 | 0.198 → 0.127 | 0.163 → 0.139 | 2 of 3 | holds, 3 of 3 |
| G3 | accounts held at death at least a fifth of what was banked | 41% | 37% | 45% | 2 of 3 | holds, 3 of 3 |
| G4 | banked, less held, per gestation birth at least 50 J | 62 J | 60 J | 62 J | 3 of 3 | holds |
| C2 | no checkpoint waits, and 300 checkpoints | 0, 300 | 0, 300 | 0, 300 | 3 of 3 | holds |
| C3 | no starved body dies solvent | 0 of 40,672 | 0 of 61,720 | 0 of 60,737 | 3 of 3 | holds |
| B1 | audit under 0.1 J, matter residual under 1e-5 units, nothing diverged, ends on its budget | 1.2e-4 J, −1.0e-6 | 3.1e-4 J, −9.6e-7 | 3.8e-4 J, −4.9e-7 | 3 of 3 | holds |
| V1 | a faithful film window from the round's own checkpoints | not read | not read | not read | one per seed | unread |
| V2 | the member check names nothing on seed 1's 15,000 s checkpoint, and a resume from it to 16,000 s agrees with the run | passed; identical on 100 samples | | | seed 1 | holds |
| M1 | at least 100 births carrying a bud | 356 | 1,313 | 829 | 3 of 3 | holds |
| M2 | at least three quarters of bud births built the bud | 97% | 78% | 97% | 3 of 3 | holds |
| M3 | a line from a stomach bud on a leaf has ten living at the end | 98 | 134 | 25 | 1 of 3 | holds, 3 of 3 |
| M4 | in each such line the stomach brings under 2% of the income | at most 0.43% | at most 0.53% | 5.4%, 1.4% | every seed M3 holds in | fails in seed 3 |
| O1 | alive at the end between 4,000 and 16,000 | 6,132 | 9,824 | 9,917 | 3 of 3 | holds |
| O2 | under 25,000 alive at every sample | at most 6,140 | 9,824 | 9,917 | 3 of 3 | holds |
| S1 | mean age at the end above 1,500 s | 2,223 s | 2,179 s | 2,117 s | 3 of 3 | holds |
| F3 | no line rooted in a pool founder has ten living at the end | largest 1 | 1 | 1 | 3 of 3 | holds |
| P1 | the whole run at or above real time | 2.80x | 2.12x | 2.37x | 3 of 3 | holds |
| EK1 | no founder has a child within 1 s of landing | 0 of 966 | 0 of 959 | 0 of 946 | 3 of 3 | holds |
| EK2 | the cap's counters agree with the rows, and a one-part pool stomach is cut 17 to 18.5 J | 123 = 123, 17.76 J | 117 = 117, 17.76 J | 96 = 96, 17.76 J | 3 of 3 | holds |
| EK3 | the one-part pool stomachs' median age at death above 300 s | 358 s | 331 s | 349 s | 2 of 3 | holds, 3 of 3 |
| EK4 | at most a fifth of them breed, and those that do have their first child a median 10 s or more after landing | 0 of 28 | 0 of 17 | 1 of 18, at 12.5 s | pooled | holds, 1 of 63 |
| EK5 | all pool founders' median age at death above 300 s | 399 s | 377 s | 352 s | 3 of 3 | holds |
| EK6 | the pool founders that bred landed in richer snow | | | | pooled | holds: 6 bred, 6.11 against 0.90 J/m³ |
| X1 | the snow's patchiness at the end between 0.4 and 1.0 | 0.72 | 0.69 | 0.67 | 3 of 3 | holds |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | not asked (0) | 0.53 → 0.06 | not asked (0.015) | every such seed | holds in seed 2 |
| S3 | rigid stomach children die at a median under 1,500 s, where ten have died | 3,005 s over 137 | not asked (0 dead) | not asked (8 dead) | every such seed | fails in seed 1 |
| E2 | the eater lines (a reading) | 4292 (132), 3726 (12) | none | 29 (121) | a reading | none alive at the end |
| O3 | bodies with a joint at the end (a reading) | 9 | 697 | 150 | a reading | read |
| S2 | stomach children's median age at death (a reading) | 2,873 s over 153 | 7 dead | 2,106.5 s over 123 | a reading | read |
| V3 | a seed's folder size (a reading) | 4.0 GB | 5.7 GB | 4.7 GB | a reading | read |
| C1 | the senses (a reading): share of the living with a contact or damage sense at 30,000 s | 0.020 | 0.046 | 0.058 | a reading | read |
| E1 | what the endowment buys (a reading): median endowment | 66 J | 71 J | 73 J | a reading | read |
| EK7 | what the cap takes (a reading): trickle founders capped | 91 of 820 | 97 of 839 | 75 of 817 | a reading | read |
| R1 | the reef (a reading): samples with fewer bodies per m³ under the caps than beside them | 41 of 41 | 41 of 41 | 41 of 41 | a reading | read |

<!-- frame: r50-s2, 30000, line 14's members after their joints fell away (0.53 jointed at 16,000 s, 0.06 at the end)
-->

## The read moved the next round's rules to the dead

The owner ruled round 51's world on 2026-09-27, after reading this read and the link-light table (D127). From round 51
a link catches no light. A body's first link is free up to about a typical link's size. A dead body becomes a husk
that sinks, settles on a reef top or the bed, and dissolves slowly. D127's reasoning leans on round 50 twice. Every
death was a starvation with the reserve at 0, so a corpse held its tissue alone, about 12 J against the 42 J a living
body holds. And line 4292 showed that a link's light is not what lets an eater live.

The ledger check then found that husks of tissue alone could not feed an eater. In seed 2's last 5,000 s corpses put
about 40 W into the snow, against 167 W from the living plants. So the owner added death by age (D128). A body's
chance of dying rises with age as Gompertz's law has it, and an old body dies holding its reserve, which goes into its
husk. Round 51's predictions (0125) carry this round's clauses and turn L1 to L4 into readings, since the rule they
asked about held. Round 50's record also became the reference for D126's check of the snow's transport on the graphics
card.

Round 50 answered its own question with a narrow yes. What it showed beyond that, a body living on the snow with no
light at all, came from a line nobody had predicted, and it shaped the round after.