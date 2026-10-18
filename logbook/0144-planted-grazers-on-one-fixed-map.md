# Round 61's predictions: planted grazers on one fixed map

*2026-10-16, committed before the round's launch. The owner approved it that evening. Every guess says it is a
guess. The ruling is D192 in `DECISIONS.md`; the world is round 60's (logbook/0142, read in logbook/0143).*

## What it asks

Round 60 fed its tank on a few islands and starved the water between them. The crowds spread off the islands all
the same, one line took each tank, and grazers evolved in one seed of three, late, and nearly died out. Its seeds
also drew three very different maps, so what the map did and what chance did could not be told apart.

Round 61 asks one thing first: whether an animal that eats leaves, planted from the start, keeps the crowds on the
islands and the water between them empty. It keeps round 60's world, adds no rule, and plants the nursery's best
leaf swimmers through the trickle. Every world shares one landscape, so the only differences between a planted
world and its control are the planting and the dice. If the grazers hold the leaves down, the ocean the owner asked
for (D191), separate oases with swimmers moving between them, is one step nearer.

## The world

Round 60's world, unchanged (`rounds/env-r60.ps1`): the dose of 3.6 units/s with 45,000 to start, burial of 0.001/s
on the islands and 0.02/s in the open water, islands 30 m off the glass, founders in the islands.

One new setting, which changes no rule: `world.seedOverrides` draws the whole landscape (the islands, the bed, the
reefs and the current) from its own seed, `landscape=1`, while the life seed draws the founders, the trickle,
conception and deaths. The landscape is seed 1, chosen by the map check below.

The planted arm's trickle draws from round 59's pool (`rounds/r59-pool.tsv`): the ten direction swimmers of
`inocula/r59-direction-tops/` at one founder in two and the four stomachs of `inocula/pool-r47/` at one in ten, a
share of 0.6 of the trickle. The floor draws nothing from the pool. The unplanted arm's trickle is round 60's.

The build is `r61` at this entry's commit: `desert-burial` with the seed overrides (`eb3f70aa`), the card's
contact fix (`e8d33900`, `e3dd6ddd`) and landscape 1 set (`7c4c3ad8`).

## The map

The map check founded landscapes 1 to 40 on life seed 6101 to 100 s on the card and listed each one's islands clear
of the glass (`scripts/reads/r61-maps.py`). D192 asks for about five or six islands clear of the glass.

Every island lies at least 30 m off the glass by construction (D185), and in nearly every landscape the largest
island is trimmed at that margin, so an island at the margin counts as clear. The rule, set before any landscape
was read past the first eight: the most islands of at least 10 columns, the nearest island centres at least 40 m
apart, and no island over 0.4 of the island columns. Of 40 landscapes, four pass
(`scratch/r60-build/pick-map.py` over the map check's logs):

| landscape | islands of 10+ columns | nearest centres | largest island's share | island columns |
|---|---|---|---|---|
| **1** | **6** | **51 m** | **0.35** | **385** |
| 40 | 5 | 73 m | 0.37 | 317 |
| 21 | 5 | 62 m | 0.40 | - |
| 34 | 4 | 117 m | 0.39 | - |

The landscape is 1.

## The arms, seeds and length

Two arms, planted (`r61-p6101`, `r61-p6102`) and unplanted (`r61-c6101`, `r61-c6102`), on life seeds 6101 and 6102
(D180), each world 48,000 s, eight years of 6,000 s. The four run as one card group.

The founding check ran the four worlds as one group to 1,500 s from `7c4c3ad8`. It dropped no contact, closed
its books and held every condition of `card-run-check.ps1`. Each world had landscape 1's six islands. The card
held 10,128 MiB at its peak, about 6,000 of them for the four worlds at about 1,200 bodies each. The group ran at
2.15 times real time (measured). Round 60's worlds grew by about 60 KB of card memory a body, so four worlds of
17,000 bodies would need about 14,000 MiB of the card's 24,564 (guessed). Round 60's three worlds took 7.6 h of
stepping, so four should take about 10 h (guessed).

At 1,500 s each planted world equals its control in every record. The trickle, the planting's only path, starts
when the floor closes at 3,000 s, so the arms part from there. A planted world was resumed alone from 1,500 s to
3,600 s to show the planting works. The trickle opened at 3,000 s and brought 25 founders by 3,600 s, 9 of
them from the pool: eight of the nursery's direction swimmers and one of the stomachs (measured). Its
books closed and it dropped no contact. Its living fell from 1,160 at 1,500 s to 418 at 3,600 s. Round 60's worlds
fell likewise in year 1, as the founders' endowment ran out (measured; the cause is an inference).

Round 60's card dropped contact candidates once its crowds passed about 13,000 (logbook/0143). A part's
candidate list held 256 entries, and a pair sharing several grid cells took several of them, so the lists filled
with repeats. Past the cap the kept entries were a race, and a world that overflowed did not replay. Round 61's
build carries D193's fix: the list holds 512 distinct entries, refuses repeats, and past the cap keeps the lowest
whatever the race. On round 60's densest span it dropped nothing and two runs agreed in every record, at 0.74 times real time
against 0.75 before (measured). B2 asks it again in denser worlds.

## Predictions

The measures are round 60's (logbook/0142): a *10 m square* is empty when no living body is in it; *on the islands*
is the share of the living over an island column as seeded; an island is one connected group of island columns.
A *grazer* is a body with a mouth. *Planted beats control* compares the two worlds on the same life seed.

| # | prediction | read from |
|---|---|---|
| P1 | **the grazers stay**: at least 100 grazers alive at every year's end from year 2 to 8, in both planted worlds. *Guess: holds.* | stats rows |
| P2 | **grazing**: at least 100 leaf kills a year by grazers in at least 2 of years 6 to 8, in both planted worlds, and in at most one control. *Guess: holds.* | lineage rows |
| P3 | **the leaves are eaten down**: at 48,000 s each planted world holds at most 0.7 of its control's living leaves. *Guess: fails; grazing at round 60's rate took a few hundred leaves a year from crowds of 15,000.* | stats rows |
| P4 | **the tank stays patchier**: at 48,000 s each planted world has at least 0.1 more of its 10 m squares empty than its control. *Guess: fails, with P3.* | positions |
| P5 | **the crowd stays on the islands**: at 48,000 s each planted world has at least 0.1 more of its living on the islands than its control. *Guess: fails.* | positions |
| P6 | **separate small worlds**: at 48,000 s at least 3 islands each hold at least 5% of the living, in both planted worlds. *Guess: depends on the map.* | positions |
| P7 | **grazers cross**: in at least one planted world, at least 20 grazers a year in years 6 to 8 are seen over one island and later over another. *Guess: fails. In round 60 no grazer crossed, and the nursery bred the planted swimmers to find leaves within a patch.* | positions |
| L1 | **no line takes the tank**: no root line holds over half the living at 48,000 s, in at least one planted world. *Guess: fails.* | lineage rows |
| S1 | **readers climb the smell**: as in round 60, over 3,000 to 48,000 s, in at least one planted world. *Guess: holds, since the planted swimmers read it.* | the recorder's rows |
| C1 | **the control repeats round 60**: in both controls, under 0.6 of the living on the islands at 48,000 s and one root line over half the living. *Guess: holds.* | positions, lineage rows |
| B1 | **the books balance**, as in round 60, in 4 of 4 | stats rows, run.json |
| B2 | **the card holds**: `card-run-check.ps1` holds every condition in 4 of 4, with no contact candidate dropped | the card's logs |
| B3 | **a world resumes exactly**: `r61-p6101` resumed alone from 45,000 s matches the unbroken run | `group-resume-compare --lone` |

## The readings

At every year's end, each world is read as round 60's were, and each planted world beside its control: the living
by guild, the grazers, the leaf kills, the empty squares, the share on the islands and on each island, the desert's
matter, burial against influx, the largest root line and the crossers. Before the entry is written, I look at maps of
the bodies and the matter at 6,000, 24,000 and 48,000 s, and at theatre frames.

## The reader

`scripts/reads/r61-read.py`, built from round 60's reader (`scripts/reads/r60-read.py`) while the round runs, with
the planted-against-control clauses added. It is run on the founding check's output before it reads any row of
the round.

## What the round does not ask

A death risk that rises with a line's crowding, harsher winters, staying power for the crossing, strands between
the islands, and a replay of the tape with the founders fixed (D192): each held as its own question.

## Before the launch

1. The map check chose landscape 1, and `rounds/env-r61.ps1` sets it (`7c4c3ad8`).
2. Round 60's dropped contacts are located and fixed on this build, as above.
3. The founding check and the planting check ran from `7c4c3ad8`. This entry's commit adds only this file, so the
   code they ran is the round's.
4. The owner sees this entry, and the round launches from a clean tree after their word.
