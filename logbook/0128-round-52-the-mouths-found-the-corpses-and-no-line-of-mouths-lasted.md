# Round 52's read: the mouths found the corpses and ate them, and no line of mouths lasted

*2026-10-31, written by the writing lane from the committed read, about a month after round 52 ran on 2026-09-29. The
pre-registration is logbook/0127. The read of all three seeds is `logbook/specs/r52-read/read-30000.txt` on the branch
`round-52`, and the reads of seeds 1 and 2 are also on main, one file each, in the same folder. Every figure is
measured unless it says otherwise.*

Round 52 changed where a founder lands. A founder is a random genome brought into the world from outside. From this
round, a founder that carries a mouth was set down over the richest pile of corpses in its column (D129). The rule
worked. In all three seeds every mouth founder landed over a corpse, and the mouths ate 413 corpses, against three in
round 51. But the mouth founders lived a median of only 1 to 46 s longer than round 51's. No line rooted in a mouth
had more than one member alive at the end. The round also made lifespan a heritable gene. It rose in seeds 1 and 3, as
the pre-registration's screen said it would, and fell in seed 2, which no committed read explains. The matter books
closed in every seed, as round 51's had not.

## The round ran in a day, and the machine crashed under seed 3

Round 51 had left a larder, a store of dead bodies with energy still in them. Between 37% and 49% of its dead died of
age, holding their savings. Their corpses sank and settled, and by the end they held an eighth of the world's matter
(logbook/0126). One corpse was eaten in each seed. The founders with a mouth, about 200 a seed, had been set at a
random depth in the top 12 m. In seed 1 they landed about 40 m above the corpses (D129).

A mouth is any part that takes in food, and here its food was a corpse within its reach, about half a metre plus the
corpse's own size (`logbook/specs/r52-landing.md`). On 2026-09-28 the owner ruled that a mouth founder should land
where the corpses lie (D129). The owner also ruled that the same round would make lifespan heritable, against the
coordinator's recommendation of the corpse rule alone (D130). If the pair caused a problem, one of them would be
removed.

Every body carries a lifespan gene that stretches its whole ageing curve. At 1, the median body that dies of age dies
at 3,000 s; at 2, at 6,000 s. A longer life costs repair. Upkeep, the standing cost of being a body, is multiplied by
the gene to the power 0.5, so a body at 2 pays 1.41 times what a body at 1 pays.

The pre-registration was committed at 12:39 on 2026-09-29, by the machine's clock, and seed 1 started nine seconds
later. Each seed ran 30,000 simulated seconds on sixteen threads, one after another. Seed 1 took 98 minutes and seed 2
took 116. Seed 3 started at 16:14, and the machine crashed under it at 23,200 s. It was continued at 19:32 from its
checkpoint at that second, a saved copy of the whole world, and finished 47 minutes later.

Its two parts were joined at 23,200 s as `r52-s3j`. The continuation agrees with the run on every sample they share
(logbook/0129). The read of all three seeds was committed at 20:26 the same evening. Seed 3 is read only in that file
on the branch. Its pace and its checkpoint count belong to the join and are not read.

While seeds 2 and 3 ran, a Fable subagent read seed 1 alone and advised on round 53
(`logbook/specs/r53-fable-advice.md`). The owner ruled round 53's shape after the first two seeds (D132).

## Every mouth landed over a corpse, and the mouths ate 413

The first question was whether the mouths would land in the larder. Each mouth founder's row records the corpse energy
in the 1 m column it landed in. Beside it, the row records what a column drawn at random would hold. In seed 1 the
median landing column held 48 J of corpses, against 7.6 J at random, 6.0 times as much. Seeds 2 and 3 read 5.7 times
each. Every one of the 628 mouth founders that arrived after 3,000 s landed over a corpse. K1 and K2 hold.

They ate. By 30,000 s the mouths had eaten 170, 114 and 129 corpses, against K3's bar of 30 and round 51's one a seed.
Divided over the mouth founders, that is 0.58 to 0.76 corpses each. If the founders did all the eating, most ate about
once. The read does not say who ate, so that is an inference.

The meal hardly touched the larder. At 30,000 s seed 1 still held 17,000 corpses, 15,418 of them settled on the bed or
a reef. The 170 eaten were 0.44% of its 38,656 deaths. No mouth killed anything. The read counts no body eaten alive
and no corpse made by a kill, in any seed.

<!-- frame: r52-s1, 12,000 s, a mouth founder over a settled corpse pile on the bed -->

## A meal bought seconds, and no line of mouths lasted

K4 asked whether a meal buys time: whether mouth founders live a median at least 1.5 times as long as round 51's same
seed. They lived a median of 140.5, 118.25 and 154.75 s. Round 51's lived 104.5, 117.25 and 108.5 s. That is 1.34,
1.01 and 1.43 times, and K4 fails in all three. Each seed of round 52 is a new realisation of round 51's, a different
roll of the dice. So the ratio compares two crowds of founders, and cannot show what one meal did for one body.

A line is a founder and all its descendants. Of the 628 mouth founders, 35 bred, with 51 children among them. At the
end seed 1 had no living line rooted in a mouth founder. Seed 2 had one, whose single member was born 2.5 s before the
end. Seed 3 had two, the larger with one living member. K5 asked for ten living in one such line in one seed. It
fails, as 0127 expected.

The landing arithmetic had said why before the run (`logbook/specs/r52-landing.md`). A meal on landing keeps a small
mouth for about 260 s. New corpses come to rest around a landing at about 0.03 W, a fifth of the 0.155 W such a mouth
costs. To live on corpses, a mouth must move between them at 4 to 8 cm/s. The evolved swimmers of earlier rounds made
2 to 6 mm/s. Reading the reads a month later, I find this one of its cleaner results. The rule did what it was built
to do, and the arithmetic had said beforehand that it would not be enough.

Movement was going the wrong way. A jointed body has a hinge its brain can drive, and it is the only kind that can
stroke. Seeds 2 and 3 ended with 8 and 16 jointed bodies, against 607 and 38 in round 51's same seeds. In seed 2 the
share of children born with a joint fell from 0.395 in the first 5,000 s to under 0.001 from 15,000 s on. Seed 1 ended
with 219 and averaged 354 over the run, against its round 51 baseline's 137.

<!-- frame: r52-s2, 6,000 s, jointed bodies among the crowd while they were still being born --> <!-- frame: r52-s2,
30,000 s, the same view with the jointed bodies gone and corpses lying on the bed -->

## The lifespan gene rose in two seeds and fell in the third

The second question was whether the lifespan gene would move. The founders drew it evenly from 0.5 to 1.5, and a
child's gene changed at a birth with a chance of 0.08. A screen of the price against round 51's life history
(`logbook/specs/r52-lifespan-screen.txt`) said the gene would hold near 1 while upkeep took most of the light a body
caught. As that share fell, the gene would rise.

First came the check that the gene does what the rule says. Every birth carried it, and in every seed bodies with a
longer gene died of age later. In seed 1 the median death by age came at 1,852 s for bodies under 0.8. It came at
2,262 s from 0.8 to 1.2, and at 2,784 s at 1.2 and over. LS1 holds.

Seeds 1 and 3 went as the screen said. Their children born from 3,000 to 8,000 s carried a mean gene of 0.98 and 1.03.
Upkeep's share of the light the living caught fell from 0.62 to 0.51 in seed 1, and from 0.60 to 0.45 in seed 3. In
the last 5,000 s their children's mean was 1.17 and 1.14, a rise of 0.19 and 0.11.

Seed 2 did not. Its early children already carried 0.79, under LS2's band of 0.85 to 1.1. Its upkeep share fell the
furthest, from 0.52 to 0.35, which by the screen should have lengthened lives. Instead its late children's mean fell
to 0.70. At the end the 10th, 50th and 90th percentiles of the living's gene were 0.56, 0.71 and 0.85. Of seed 2's
25,403 deaths by age, 21,747 were bodies under 0.8.

So LS2 fails on seed 2 and LS3 holds on two seeds of three. LS4 guarded against a runaway to long lives. The living's
mean gene at the end was 1.19, 0.70 and 1.15, under its bar of 2.5, and it holds.

The pre-registration named what such a failure would mean. A low early gene would say the price is stronger than the
screen assumed. Or the mutation step, taken on the gene itself and not on its logarithm, pulls it down harder. A share
that fell while the gene did not rise would say the screen's averaged sketch misses what limits a long life here. It
named starvation before old age, crowding among newborns near their parents, and the ageing a birth costs. Seeds 1 and
3 ran under the same price and step, and their gene rose. So what held seed 2 down lay in that world's own history as
well as in the rule (inference). No read says which it was. D132 held the lifespan values through rounds 53 and 54
while seed 2's fall was read, and I found no committed read of it.

## The books closed, and seed 3 grew a line of stomachs

Round 51's matter books had not closed. The matter residual is the gap between the matter the world should hold and
the matter it does hold. In round 51 it ended between 1.1e-5 and 6.3e-5 units in size, over B1's bar of 1e-5
(logbook/0126). Round 52 carried a fix. The burn, the return of spent matter to the water and a corpse's last
instalment now handed the water its matter in double precision instead of single. The largest residual at any sample
was 7.5e-7, 1.9e-6 and 1.0e-6 units, and B1 holds in all three seeds.

One carried clause held that 0127 expected to fail. E3 asks for a line with ten living pure stomachs at the end. A
pure stomach is a body with a stomach and no leaf tissue, living on the snow, the dead matter falling through the
water. Seed 3 had one such line. Its founder was a stomach with a joint that arrived on the trickle, the stream of
founders that comes about every 30 s, at 3,655.5 s. The line had 376 members over the run, 375 of them pure stomachs,
and 12 alive at the end. Round 51 had no such line in any seed.

S3 failed in seed 3, the only seed it asked. Its rigid stomach children died at a median 2,068 s, against a bar of
under 1,500 s. S2 counts 374 stomach children in seed 3, 12 of them alive, which matches this line closely. So the 229
rigid ones S3 read were probably its members (inference). The read does not join the two.

<!-- frame: r52-s3c, 30,000 s, the 12 living pure stomachs of seed 3's stomach line, and where they sit -->

## The clauses against the pre-registration

The first table holds the round's own clauses. Each bar is copied from 0127 without its parenthesis of earlier
numbers, and each verdict is the reader's. Look at K4 and K5, the meal that bought nothing lasting, and at LS2, which
seed 2 alone failed.

| # | bar (0127) | seed 1 | seed 2 | seed 3 | verdict |
|---|---|---|---|---|---|
| K1 | **the mouths land in the larder**: over the mouth founders from 3,000 s, the median of `fcorp` over `fcorpm` is at least 5, in 3 of 3 | 6.05 | 5.72 | 5.75 | holds |
| K2 | **all of them**: at least nine in ten of those founders land in a column holding corpses (`fcorp` above 0), in 3 of 3 | 223 of 223 | 197 of 197 | 208 of 208 | holds |
| K3 | **the larder is eaten**: `corpsesEaten` is at least 30 at 30,000 s, in 3 of 3 | 170 | 114 | 129 | holds |
| K4 | **a meal buys time**: the mouth founders from 3,000 s die at a median age at least 1.5 times round 51's same seed's | 140.5 s against 104.5 s: 1.34 | 118.25 s against 117.25 s: 1.01 | 154.75 s against 108.5 s: 1.43 | fails |
| K5 | **a line from a mouth lasts**: a line rooted in a mouth founder has ten or more living at 30,000 s, in 1 of 3 | 0 | 1 | 1 | fails |
| K6 | **what the mouths did** (a reading) | 223 founders, 13 bred, 19 children, 0.76 corpses each | 197, 13, 23, 0.58 | 208, 9, 9, 0.62 | reading |
| LS1 | **the gene does what the rule says** (a check): every birth row carries `ls`, and the median age at an aged death rises from the bodies with `ls` under 0.8 to those from 0.8 to 1.2 to those at 1.2 and over, in 3 of 3 | 1,852, 2,262, 2,784 s | 1,829, 2,367, 3,283 s | 1,937, 2,690, 2,950 s | holds |
| LS2 | **it holds while upkeep is dear**: the mean `ls` of the children born from 3,000 to 8,000 s lies in 0.85 to 1.1, in 3 of 3 | 0.98 | 0.79 | 1.03 | fails |
| LS3 | **it rises when upkeep eases**: the mean `ls` of the children born in the last 5,000 s is at least 0.1 above that of those born from 3,000 to 8,000 s, in 2 of 3 | +0.19 (upkeep share 0.62 to 0.51) | −0.09 (0.52 to 0.35) | +0.11 (0.60 to 0.45) | holds |
| LS4 | **no runaway**: the mean `ls` of the living at 30,000 s is under 2.5, in 3 of 3 | 1.19 | 0.70 | 1.15 | holds |
| LS5 | **who lives long** (a reading) | 10th, 50th, 90th: 0.96, 1.25, 1.35 | 0.56, 0.71, 0.85 | 0.87, 1.18, 1.31 | reading |
| A2 | **at the ages the hazard was set for**: the median age at an aged death lies in 1,500 to 4,500 s, in 3 of 3 | 2,290 s | 1,894 s | 2,525 s | holds |
| B1 | **the books close**: the audit stays under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and the run ends on its budget, in 3 of 3 | largest residual 7.5e-7 units | 1.9e-6 | 1.0e-6 | holds |

The carried clauses mostly held, and 0129's table of carried clauses gives round 52's three values for each. W2, W3,
G1 to G4, C3, V2, M1 to M4, O1, O2, F3, EK2 to EK4, X1, LC, A1, A3, H1, H2 and H4 held. The worlds ended with 3,431,
2,813 and 5,691 alive, inside O1's band. The table below holds the rest, each with its short form and bar copied from
0127.

| # | prediction, in short (0127) | bar | round 52 | verdict |
|---|---|---|---|---|
| W1 | every founder that eats the snow and not the dissolved matter lands at or above its column's mean of the snow | 3 of 3 | none under in seeds 1 and 2; in seed 3 two trickle founders about 1% under (6.84 against 6.92 J/m³, 5.32 against 5.36) | fails |
| C2 | no checkpoint waits, and 300 checkpoints | 3 of 3 | none and 300 in seeds 1 and 2; seed 3's are split by the crash | 2 of 3, seed 3 absent |
| V1 | a faithful film window from the round's own checkpoints | one per seed | no window was recorded | absent |
| P1 | the whole run at or above real time | 3 of 3 | 5.1x and 4.3x; seed 3's 10.6x is the join's and is not read | the reader counts 3 of 3 |
| EK1 | no founder has a child within 1 s of landing | 3 of 3 | 6, 11 and 6 founders did, all from the trickle | fails |
| O4 | in the line with the most living at the end, the jointed share below its share at 16,000 s, where that line was a fifth jointed then | every such seed | no seed's line was a fifth jointed at 16,000 s | not asked |
| S3 | a rigid stomach child dies young, median under 1,500 s, where ten have died | every such seed | seed 3: 2,068 s over 228 dead; one died in each of seeds 1 and 2 | fails |
| E3 | a line has ten or more living pure stomachs at the end | 1 of 3 | 1, 1 and 12 | holds |

## Round 53 changed the tank and left the mouths alone

D132 drew three things from the first two seeds. The mouths had used the settled piles, so D131's plan to make a
settled corpse decay fast was set aside. It would have added no food and shrunk the piles about tenfold. Movement was
the wall, since a mouth that cannot swim to its next meal cannot live on corpses, however well it lands. So the
water's reactive thrust, which pushes a limb along as it swings through the water, was to be built during round 53 and
tried alone in round 54. And the lifespan values held through rounds 53 and 54.

Round 53 changed one thing, the matter within the crowd's reach (logbook/0129). It made the tank 25 m deep instead of
45 m. 0129 turned K4, K5 and EK1 into readings, since round 53 changed nothing about a mouth or a founder. Round 53's
read found the bed's water 2.5 to 3.1 times as rich as round 52's, and the corpses eaten rose to 721 from 413
(logbook/0132). Its mouth founders still lived only 1.14 to 1.51 times as long as round 52's. No line rooted in a
mouth had more than one member alive at the end.

That night the owner also ruled a nursery for brains, where brains are selected directly, outside the ecology (D133).
Its reason came from round 52 seed 1. At 30,000 s the median genome there carried one neuron, and 1,348 of 3,431
carried none. A body that could forage would need a sense, a neuron and a stroke together, and none of them pays
alone. Round 54 then gave joints a push (logbook/0130), and its read is logbook/0131.