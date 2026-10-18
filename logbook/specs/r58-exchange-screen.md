# Round 58's exchange rate: a screen, drafted before it runs

*2026-10-10. A draft by the coordinator, kept in `logbook/specs/` until the screen runs and the owner reads
it with the pre-registration. Its sources are D159, round 57's reports (`runs/r57-s1.md` to `r57-s3.md`),
and the founded card trial `r58t2` of the same night (HANDOFF). Nothing in it has run. Each number says
where it comes from, and a guess says so.*

## The question

D159 sets the exchange at three genomes from each connected world each simulated year of 6,000 s, as a
first value that a screen sets before the round. The round reads five things: variety, the spread of a new
way of life by descent, the rescue of a guild that died out, swamping by one world's winners, and whether
the worlds' winters fall into step. The rate has to be high enough that the second and third can happen,
and low enough that the fourth stays a question rather than a certainty.

## What the first value buys, by arithmetic

A world of round 58 runs 48,000 s, eight years, so at D159's first value it receives 24 genomes in all. The
migrants are picked at random among the living (D159, rule 2), so a guild arrives in proportion to its share
of the sending world. Round 57's shares, read from its reports:

| Seed | Alive at 48,000 s | Jointed | Absorptive | Jointed share | Absorptive share |
|---|---|---|---|---|---|
| 1 | 8,245 | 330 | 99 | 4.0% | 1.2% |
| 2 | 8,438 | 4 | 1,323 | 0.05% | 15.7% |
| 3 | 6,890 | 9 | 1,187 | 0.13% | 17.2% |

At 24 arrivals a world, a world receives between about 0.01 and 1 jointed body over the whole round, and
between about 0.3 and 4 absorptive ones. A new way of life spreading from one world to another, or a guild
refounded after it died out, needs at least one carrier to arrive and then to breed. So at the first value
the round could not see either reading, except by luck. Variety, swamping and the winters do not depend on
a rare guild, so they could still be read.

Round 57's deepest lines reached generation 62 to 82 by 48,000 s, so a year holds about eight to ten
generations of the fastest lines. Three migrants a year is then about 0.3 to 0.4 a generation. That figure
is arithmetic from the reports. Whether it is high or low against what keeps patches distinct is a question
for the literature, and the review holds no verified source for a number. Briggs and Hoopes (2004) say only
that too much movement makes the patches one pool, and the review holds their abstract only.

## What the trial showed

The trial `r58t2` planted three genomes every 300 s, 20 times D159's first value, for 3,600 s from a founded
start. All 36 offers to each world were planted, none refused. By 3,600 s the immigrants' clades held 3.6%,
6.7% and 7.4% of the living, with 17 to 21 immigrant clades alive in each world, the largest 13 to 109
bodies. A founded world is mostly founders in its first hour, so an immigrant there is close to one more
founder. These shares do not predict a mature world's.

## The screen

Three connected groups of three worlds, founded from seeds 1 to 3 on round 57's block, as the round
founds them. They differ only in the rate, each sending three genomes at a time:

| Arm | Every | Genomes a world a year | In 48,000 s |
|---|---|---|---|
| X1 | 6,000 s | 3 | 24 |
| X4 | 1,500 s | 12 | 96 |
| X16 | 375 s | 48 | 384 |

Each runs 12,000 s, two years. The trial's pace (5.02 times real time) slows as the worlds fill, so the
guess is about 50 to 70 minutes a group, three to four hours of the card for the three, one group at a time.

The readings, from `scripts/reads/immigrants.py` and the report, at every 1,500 s:

- the share of each world's living in immigrant clades, and its slope over the second year;
- the immigrant clades alive, and the largest;
- the guilds of the genomes offered (jointed, absorptive, leaf), which is what makes the rescue and spread
  readings possible;
- refusals by reason.

## The rule that picks the rate, proposed

The rate chosen is the highest of the three under which no world's immigrant share passes 25% at 12,000 s
and the share's slope over the second year would not reach 50% by 48,000 s if it held. Among rates that
pass, a higher one gives the rare-guild readings more carriers, so the highest passing rate is taken. If
none passes, the rate is X1 and the pre-registration says that rescue and spread cannot be read in this
round. The 25% and 50% are the coordinator's choice, put to the owner with the pre-registration. They are
not taken from any source.

## What the screen showed

The screen ran on 2026-10-11 on `r58-runner` `02c448e6`, which the owner approved that day. Each group's
worlds were founded on the card with r58t2's settings. The readings are by `scripts/reads/immigrants.py`
at every 1,500 s. A trace credits each living body to the world whose founder began its line, following
every immigrant back round the ring.

**X16** (`r58x16`, 19:52 to 20:30, 5.37 times real time). Every world ended on its budget with its books
closed, and `card-run-check` held for all three. Each world was offered 96 genomes and planted all 96. The
share of the living in immigrant clades:

| World | Hears from | 3,000 s | 6,000 s | 7,500 s | 9,000 s | 12,000 s |
|---|---|---|---|---|---|---|
| a (seed 1) | c | 6.7% | 17.8% | 30.3% | 71.7% | 98.2% |
| b (seed 2) | a | 1.5% | 0.3% | 0.1% | 0.2% | 1.3% |
| c (seed 3) | b | 2.5% | 15.1% | 30.8% | 48.6% | 85.6% |

X16 fails the rule in two worlds of three. The trace shows that one line did it. Seed 2's leaf founder line
was planted in c at 3,000 s and was carried on from c into a at 4,875 s. At 12,000 s, b's line holds 95.9% of
a's living and 85.6% of c's, and b keeps 99.9% of its own. The bodies that took a and c are leaf bodies. Seed
1's jointed leaf carriers, sent to b, left six bodies there. In both swamped worlds the takeover starts
between 6,000 and 7,500 s, around the first winter. Why seed 2's line wins is an inference. It is the line
that went on to hold round 57's seed 2, and at this rate it arrives often enough to win head to head. No
isolated twin of these founded worlds ran past 600 s, so the screen has no unconnected share to set beside
these.

**X4** (`r58x4`, 20:46 to 21:25, 5.29 times real time). Every world ended on its budget with its books closed,
and `card-run-check` held for all three. Each world planted all 24 genomes it was offered.

| World | Hears from | 3,000 s | 6,000 s | 7,500 s | 9,000 s | 10,500 s | 12,000 s |
|---|---|---|---|---|---|---|---|
| a (seed 1) | c | 0.9% | 3.4% | 13.4% | 20.8% | 41.2% | 67.5% |
| b (seed 2) | a | 0.1 to 0.3% throughout | | | | | 0.1% |
| c (seed 3) | b | 0.1 to 1.1% throughout | | | | | 0.1% |

X4 fails the rule in world a alone. One hop did it this time, and by a different line. c's own seed-3 leaf
line, planted in a at 4,500 s, holds 67.5% of a's living at 12,000 s. b and c each keep 99.9% of their own. a
keeps 329 jointed leaf bodies in its own founder clades, where at X16 it kept 12. That seed 1's world is the
weak receiver at both rates is an inference from two groups.

**X1** (`r58x1`, 21:26 to 21:50, then `r58x1r`, 23:06 to 23:26). The owner needed the machine for an edit,
so the group was stopped at 6,880 to 7,111 s with `stop-arm.ps1`, its books closed. It was resumed from its
checkpoint at 6,000 s, the exchange's second, and run to 12,000 s. Over the overlap, 6,000 s to each world's
stop, the resumed worlds are byte-identical to the stopped ones in every file
(`scripts/reads/group-resume-compare.py`). The reading joins the stopped run to 6,000 s with the resumed one
after it. Every world ended on its budget with its books closed, and `card-run-check` held for all three.
Each world planted the six genomes it was offered.

| World | Hears from | 6,000 s | 7,500 s | 9,000 s | 10,500 s | 12,000 s |
|---|---|---|---|---|---|---|
| a (seed 1) | c | 0.2% | 1.9% | 1.3% | 2.5% | 3.1% |
| b (seed 2) | a | 0.1% | 0.0% | 0.0% | 0.0% | 0.1% |
| c (seed 3) | b | 0.1% | 0.2% | 0.1% | 0.0% | 0.1% |

X1 passes. a keeps 97% of its living in its own lines, and its own founder clades carry 508 jointed leaf
bodies. b and c keep all of theirs.

## The rate the rule picks

| Rate | Highest share at 12,000 s | Verdict |
|---|---|---|
| X16 | 98.2% (a) | fails, in a and c |
| X4 | 67.5% (a) | fails, in a |
| X1 | 3.1% (a) | passes |

Of these three, the rule picks X1, three genomes a world every 6,000 s, which is D159's first value. Its slope
passes too.
a's share rose about 2.9 points over the second year, which held to 48,000 s reaches about 20%, under 50%.
At X1 each world receives about 24 genomes over the round, so the readings of rescue and spread will have
few carriers, as this spec said they might. Seed 1's world was the weak receiver at every rate. That its own
jointed lines survive in proportion to how rarely it receives is an inference from three groups.

The gap between X1 and X4 is wide. On 2026-10-12 the owner left it to the coordinator whether to screen a
rate between them before the rate is fixed, and the coordinator chose to. X2 sends three genomes every 3,000
s, twice X1's arrivals, and is read by the same rule.

**X2** (`r58x2`, 2026-10-12, 08:18 to 08:55, 5.56 times real time, approved by the owner that morning). Every
world ended on its budget with its books closed, and `card-run-check` held for all three. Each world planted
the 12 genomes it was offered.

| World | Hears from | 6,000 s | 7,500 s | 9,000 s | 10,500 s | 12,000 s |
|---|---|---|---|---|---|---|
| a (seed 1) | c | 0.6% | 0.4% | 0.8% | 2.3% | 4.2% |
| b (seed 2) | a | never above 0.4% | | | | 0.1% |
| c (seed 3) | b | never above 0.5% | | | | 0.1% |

In X2, world a keeps 95.9% of its living in its own lines, and its own founder clades carry 724 jointed leaf
bodies, more than at any other rate. Worlds b and c keep all of theirs. World a's share rose 3.6 points over the second
year, which held to 48,000 s reaches about 26%, under 50%. The rise is back-loaded, though. The last 1,500 s
alone went from 2.3% to 4.2%, and at that pace a would reach about 50% by 48,000 s. So X2 passes the rule as
written, with less margin than X1.

| Rate | Highest share at 12,000 s | Second year held to 48,000 s | Verdict |
|---|---|---|---|
| X16 | 98.2% (a) | | fails |
| X4 | 67.5% (a) | | fails |
| X2 | 4.2% (a) | about 26% | passes |
| X1 | 3.1% (a) | about 20% | passes |

The rule picks X2, three genomes a world every 3,000 s, about 48 in the round. World a jumps between X2 and
X4 rather than climbing by degrees. So the point where arrivals take it lies between one trade every 3,000
s and one every 1,500 s. That is an inference from four groups.

## What would change the plan

- If X1 already passes 25%, the worlds are open to arrivals far more than a founded hour suggests, and
  D159's first value stands.
- If the immigrant share in X16 is still under a few percent at 12,000 s, arrivals mostly die without
  breeding. The question then becomes why, and the bodies at the place of landing are the first thing to
  read, before any rate is chosen.
