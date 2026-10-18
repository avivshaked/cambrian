# Round 52's landing check: what D129 puts within a mouth's reach

*2026-09-28, for D129 (the owner: mouths follow the corpses, agreed for the next round). It is a
calculation from round 51 seed 1's film window at 12,000 to 13,000 s.
`scripts/reads/r52-landing.py` reads the window's corpse file and prints the tables below.*

## The answer

D129 gives a small mouth one meal and not a living. The column it picks holds about twelve times
what a column drawn at random holds. A mouth of radius 0.5 m or less lands with a corpse in reach
at seven landings in ten. What lies there is worth three to five minutes of a small mouth's
keep. New corpses come to rest round a landing at about a fifth of what that keep costs, so a
mouth that stays where it lands starves. A mouth larger than about 0.6 m across the bed cannot
reach a corpse on the bed from where the placer can put it.

## The corpses, at 12,000 s in seed 1

- 5,282 corpses holding 124 kJ, 23 J each on average. 91% of them had settled.
- 4,405 of the tank's 22,000 1 m columns held a corpse. The richest held 489 J.
- 874 new corpses arrived in the window's 1,000 s, 54 W in all.

## Where the rule lands a mouth

A column is drawn with a chance in proportion to its corpse joules, as the rule's acceptance draws
it. The founder is set between the richest corpse in the column and 0.5 m above it, and the bed
lifts it to its own radius above a settled corpse. The reach is 0.5 m plus the corpse's own radius
at 500 J/m3. Twenty thousand landings for each radius:

| radius (m) | landings with a corpse in reach | joules in reach, median | mean | new-corpse power into the 3 by 3 m round the landing, mean |
|---|---|---|---|---|
| 0.15 | 80% | 43 | 58 | 0.03 W |
| 0.3 | 79% | 41 | 58 | 0.03 W |
| 0.5 | 70% | 33 | 53 | 0.03 W |
| 0.8 | 30% | 0 | 30 | 0.03 W |
| 1.2 | 20% | 0 | 20 | 0.03 W |

The column the rule picks holds 69 J on average. A column drawn at random holds 5.6 J. The median
new-corpse power round a landing is 0. Most rich columns are rich because corpses have piled up
there over hours, and few bodies die there now.

## Against a mouth's costs

Round 51's ledger (`logbook/specs/r51-husk-ledger.md`) prices a small stomach body with a mouth.
It costs about 0.155 W: 0.111 W to keep and 0.044 W for the mouth at the consumer cap. The meal it lands on,
40 J at the waste of 0.2, keeps it for about 260 s. The supply round it, 0.03 W, is a fifth of its
keep. So a mouth has to move from corpse to corpse. Round 51's ledger puts the speed that takes at
4 to 8 cm/s, and evolved strokers reach 2 to 6 mm/s.

## What this predicts for round 52

Mouth founders eat when they land, so the corpses eaten rise from one a seed in round 51 to about
one a landing. A lasting line of mouths is not expected unless a body moves between corpses.

## What this rests on

One seed and one window. The founder's radius is set by hand in the table. No file holds a
founder's radius, so which share of round 51's mouth founders were under 0.5 m is not known. The
mouth's price is round 51's ledger body and not a mouth founder's own. The window's corpse file
is the round 51 build's, written as `husks.jsonl.gz`, which this build's film reader refuses; the
script reads the rows directly.