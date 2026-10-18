# Round 60's predictions: a large tank fed on its islands

*2026-10-16, written the night before and committed before the round's launch. The owner delegated its approval
and the choice of its dose and desert rate to me under fixed rules (D189 to D191). Every guess says it is
a guess. The screens it rests on are read in `logbook/specs/r60-screens/summary.md`, and the rulings are D175, D180,
D181 and D185 to D191 in `DECISIONS.md`.*

## What it asks

Until now every round's tank has held a closed stock of matter. What sinks is returned by the bed under every
column, so every part of the water pays a leaf. The crowd becomes a soup within a few thousand seconds. The
owner asked for a larger tank where life gathers in separate places, and round 60 is the first try.

The tank is four times round 57's area. Its matter is no longer a stock. It arrives on a few islands and is buried
at the floor of every column, so the water between the islands is fed only by what drifts from them. The round
asks three things. The first is whether the crowds stay on the islands for a whole round, as separate small worlds.
The second is whether, where food is patchy, grazers that read the leaf smell climb it.
Round 59's crowded worlds gave them no reason to. The third is whether
swimmers cross the open water between islands. That is the first step toward the ocean the owner wants, of
oases and swimmers that migrate between them to graze or hunt (D191).

## The world

The world is round 59's control, as `rounds/env-r60s.ps1` sets it on round 57's settings. It keeps the turning
drag in its spin form, the touch of the rock and its sense, and the leaf smell in its direction form at 6 m. Nothing is planted.
The trickle of fresh founders is round 59's control's.

Five changes from it, each ruled on the screens:

- The tank is 88,000 m², a disc of radius 167 m (D175). The bed's 41 m tilt is held, and up to 256 reef stones keep a
  quarter of the floor under rock, as at 22,000 m² (`rounds/env-r60s-big.ps1`).
- The matter arrives on D109's islands, the fed islands of D175, at a 90 m wavelength over 0.05 of the water.
  Each step's influx goes into their top 12 m, each column weighted by its starting stock. Burial takes
  0.001 of each column's lowest live cell a second, and matter sinks at 0.02 m/s.
- No island column lies within 30 m of the glass (D185).
- The open water outside the islands' footprint buries its matter at 0.02/s, against 0.001/s on the islands
  (D190, D191). In the ocean the surface away from a source is held far below what a plant needs, because what
  drifts off sinks out of the light. This is the tank's version of that.
- Founders start in the islands, placed by the matter's columns (D181). Core refuses this rule
  beside round 59's founder rule (D116), so D116's four switches are off.

The dose is 3.6 units/s, with 45,000 units to start. D191's rule chose it with the desert rate. Of the two desert
screens, only the one at 0.02/s held at 20,000 s. It kept at least 1,000 alive, with 0.6 of them on the islands and
0.3 of the squares empty.

The screens of this world on seed 1 (measured, `logbook/specs/r60-screens/summary.md`):

| dose (units/s) | alive at 10,000 s | at 20,000 s | empty 10 m squares, 10,000 / 20,000 s | on the islands, 10,000 / 20,000 s |
|---|---|---|---|---|
| 2.4 | 611 | 1,719 | 0.79 / 0.64 | 0.69 / 0.59 |
| 3.6 | 1,827 | 4,716 | 0.59 / 0.41 | 0.62 / 0.49 |
| 4.8 | 3,228 | 11,690 | 0.44 / 0.24 | 0.54 / 0.40 |
| 3.6, desert 0.005/s | 1,064 | 3,727 | 0.78 / 0.52 | 0.74 / 0.59 |
| 3.6, desert 0.02/s, round 60's world | 791 | 2,221 | 0.80 / 0.68 | 0.75 / 0.74 |

On the present rules, at every dose screened past 10,000 s, the crowd kept growing and spread from the islands
into the water between them. A band of bodies formed at the north-east glass, where no island lies. With the
desert at 0.02/s, the open water's median column held about a leaf's break-even at 20,000 s. Half the desert's
columns were below it, and the map showed six island crowds with almost nothing at the glass. The crowd was
still growing, doubling about every 5,000 s, so how far it spreads by 48,000 s is what the round shows. Neither
desert screen grew a body with a mouth by 20,000 s.

It is a new realisation of every seed. The build is `desert-burial` at this entry's commit: main, which carries `r60-fed`,
with D190's desert burial. Its checkpoint holds each link's contact centre and its rest, and it computes the birth overhead's returned matter
in double. Those were the causes of round 59's B3 and B1.

## The seeds and the length

Seeds 6001, 6002 and 6003, by D180's rule: new seeds every round, fixed before launch. There is one arm, the
fed islands, so there is no control on these seeds. The predictions are read against fixed bars and against
the screens. A control at this size would double the card time, and round 59's worlds
already show the closed stock's soup.

Each world runs 48,000 s, eight years of 6,000 s, on the graphics card. The three run as one card group if the
founding check shows they fit, and one at a time if not. As estimated from `r60s-big2`, a group takes 9 to 11 h
and needs 13,000 to 18,000 MiB of the card (guessed). A lower dose's smaller crowd should run faster (guessed).
The group stops itself on two samples in a row at or over 90 °C on the CPU (D183), and nothing starts on the
fast cores beside it.

## The founding check before launch

The three seeds as one group, run to 1,500 s on the card from this commit, then discarded. It is read for the
card's memory against its 12,000 MiB limit, and for the pace. It also shows where each seed's founders start,
and whether each seed has at least three islands clear of the glass. If the group does not fit, the round runs one
seed at a time, and the predictions do not change.

## Predictions

The bars for the patches are D175's measures, as the screens used them. A *10 m square* is a column of the water
10 m across, and it is *empty* when no living body is in it. *On the islands* is the share of the living whose
position lies over an island column as seeded. The *desert* is the water outside the islands' footprint, read
in its top 12 m. A leaf's break-even there is 0.00025 units/m³ (`scripts/ledger.ps1`).

| # | prediction | read from |
|---|---|---|
| M1 | **the crowd lives**: at least 1,000 living at every year's end from year 2 to 8, in 3 of 3 seeds. *Guess: fails at year 2's end, where the screen had about 900, and holds from year 3.* | stats rows |
| M2 | **the tank stays patchy**: at least 0.3 of the 10 m squares empty at every year's end from year 2 to 8, in at least 2 of 3 seeds. *Guess: fails late in the round, as the crowd keeps growing.* | positions |
| M3 | **the crowd stays on the islands**: at least 0.6 of the living on the islands at 48,000 s, in at least 2 of 3 seeds. *Guess: fails. The desert screen held 0.74 at 20,000 s, but its crowd was still growing.* | positions |
| M4 | **separate small worlds**: at 48,000 s, at least 3 islands each hold at least 5% of the living, in at least 2 of 3 seeds. An island is one connected group of island columns. *Guess: holds.* | positions, the islands as seeded |
| M5 | **the stock levels**: over year 8, burial is between 0.8 and 1.2 times the influx, in 3 of 3 seeds. *Guess: holds.* | stats rows |
| M6 | **the desert is hungry**: the median desert column's matter in its top 12 m below a leaf's break-even at 48,000 s, in at least 1 of 3 seeds. *Guess: holds. The desert screen's median column was at about break-even at 20,000 s.* | the matter dump |
| L1 | **no line takes the tank**: no root lineage holds over half the living at 48,000 s, in at least 2 of 3 seeds. Round 59's worlds were each over 70% one line by year 3. *Guess: fails, because a line that wins one island can swim to the others.* | lineage rows |
| G1 | **grazing evolves**: at least 100 leaf kills a year by grazers, in at least 2 of years 6 to 8, in at least 1 of 3 seeds. Round 59's control reached 33. *Guess: fails. Neither desert screen had a mouthed body at 20,000 s.* | lineage rows |
| S1 | **readers climb the smell**: over 3,000 to 48,000 s, grazers whose brains read the leaf smell move along its gradient more than grazers whose brains do not, by at least 0.1 in mean cosine, each side with at least 1,000 body-samples, in at least 1 of 3 seeds. Read only where both sides reach 1,000, and otherwise unread. *Guess: unread, for want of grazers.* | the recorder's rows |
| M7 | **swimmers cross between islands**: in at least 1 of 3 seeds, at least 20 bodies a year without photosynthetic tissue, in years 6 to 8, are seen over one island and later over another, and at least one root line has living members on 2 or more islands at 48,000 s. *Guess: holds, weakly. The desert screen counted 82 to 296 crossers a year, but leaves and jointed swimmers together, and with no mouths none crossed to feed.* | positions |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and every world ends on its budget, in 3 of 3 | stats rows, run.json |
| B2 | **the card holds**: `card-run-check.ps1` holds every condition in 3 of 3, with no contact candidate dropped | the card's logs |
| B3 | **a world resumes exactly**: seed 6001, resumed alone on the card from its 45,000 s checkpoint to 48,000 s, matches the unbroken run in every row file but the wall timings and step counters | `group-resume-compare` |

## The readings

At every year's end, each seed is read for the living by guild, the empty 10 m squares and the share on the
islands. It is also read for the living on each island, the desert's matter, burial against influx and the standing
stock. The rest are the largest root line's share, the leaf kills by grazers, and the bodies that cross between
islands. Before the entry is written, I look at maps
of the bodies and the matter at 6,000, 24,000 and 48,000 s, and at theatre frames.

## What the round does not ask

Whether the islands should move (the wandering sources, proposed for round 61). Whether a planted grazer does
better in a patchy world. Whether burial should take only from cells on the bed rather than from a reef stem's
top cell, which touches about 1 to 2% of columns.

## The reader

`scripts/reads/r60-read.py`, built before launch from round 59's reader and the screens' reader
(`scripts/reads/r60s-screen-read.py`), and run on the founding check's output before the round starts.

## Before the launch

1. The dose and the desert rate are chosen by D191's rule on the screens, and this entry is filled.
2. The launcher `rounds/r60.ps1` and the reader `scripts/reads/r60-read.py` are on `desert-burial`, and the reader
   has read the desert screens.
3. This entry is committed on `desert-burial`, and the founding check runs from that commit.
4. The round launches from a clean tree after the check passes. The owner sees this entry the next morning, by
   their delegation (D189).
