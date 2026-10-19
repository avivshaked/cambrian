# Round 62's predictions: oases in an open ocean, and a longer reach for the grazers

*2026-10-19, committed before the round's launch on `r62-build`. The rulings are D204 to D209 in `DECISIONS.md`.
The screens this rests on are `logbook/specs/r62-collapse-screens/` and
`logbook/specs/r62-dial-screens/grazer-turnover.md`. Every number below that is not a measurement is a guess, and
says so.*

## What it asks

Round 61 filled its tank with a crowd of leaves and left the water between its islands full, and its grazers
starved. Round 62 changes the world for both problems at once.

The tank's glass is gone. The world is a square 300 m a side whose edges join, so a body that leaves at one edge
comes in at the other. Island shoals 1 to 3 m down carry the matter fountains, and there is no dry land. The
current is an ocean's: 25 swirls at sizes from 30 to 150 m, still at the floor and moving most at the surface, at
0.02 m/s. Plants keep their spent matter until they die (D206). A body that eats releases its share, after its
own leaves have taken what they can at the present light (D207). Branching coral grows on the islands (D194).

The round asks three things.

1. Do the islands hold their life as separate oases for a whole round, with the water between nearly empty?
2. Does a mouth that reaches 2.0 m keep grazer lines alive under the new rules?
3. Do bodies that both photosynthesise and eat do better than pure grazers, as D207's evidence suggests they
   should?

## The world

Six worlds, founded, 72,000 s each, in one group on the card with the snow carried there. They run in pairs on
seeds 6201, 6202 and 6203:

- `r62-s6201` to `r62-s6203`: round 62's world with round 61's grazer dials, the mouth's reach at 0.5 m
  (`rounds/env-r62.ps1`).
- `r62-r6201` to `r62-r6203`: the same, with the mouth's reach at 2.0 m (`rounds/env-r62-reach.ps1`). The two
  launchers differ only in `EVOSIM_INTAKE_REACH`. Config hashes: `710a8fc5b74fd074` and `a737013af6306e0d`, read
  off the accelerator preflight at seed 6201.

Each pair shares its seed, so the pair differs only by the reach until the first difference compounds.

Founders arrive by round 61's trickle, the same in both arms: one every 30 s, a tenth of them drawn from
`rounds/r55-pool-control.tsv`, each with 600 J, placed 12 m down where the matter is. The landscape is seed 11,
the weather seed 57, and the population cap 25,000. The group runs at group size 32 on cores 16 to 29, with the
classes stepped one after another. Concurrent classes gave the same bits in the launch checks but ran 4 to 7%
slower six-up.

## Predictions

These are guesses. The screens ran 2,700 s at 0.1 m/s and 0.02 m/s on one seed, and nothing has run past that
in the square.

1. **Oases hold.** At 20,000 s and at 72,000 s, at least 0.6 of the living sit on the islands in at least four of
   the six worlds, and at least 0.6 of the 10 m squares are empty. The screen at 0.02 m/s read 0.74 and 0.79 at
   2,700 s. A guess: the share drifts down as lines learn the open water, but stays above 0.6.
2. **The population is set by the islands' food.** The living stay between 1,500 and 6,000 in every world after
   10,000 s, with no doubling every few thousand seconds as in round 60's tank. A guess, on the screen's steady
   2,900.
3. **Grazers.** In the unchanged worlds, no grazer line outlives its founder by more than one generation in at
   least two of the three, as in round 61. In the reach worlds, at least one grazer line reaches generation 5 in at
   least one world. A guess with low confidence. The read puts intake at a sixteenth of the cost, and a longer
   reach may not close that.
4. **Hybrids.** Mouthed bodies with photosynthetic cells outlive mouthed bodies without them at the median age
   at death, in every world. Round 61 showed 1,508 s against 638 s on 25 deaths. A guess that D207 widens the gap.
5. **Pace.** The round runs at 2x real time or better six-up while the crowd stays under about 3,000 bodies in
   each world. Measured six-up in the launch checks at 2.6 to 2.8x over the first 300 s, at 400 to 560 bodies a
   world; at the crowds the screen reached, a guess.

## How it is read

- `python -I scripts/reads/r60-read.py --year 6000` on each world, for the living, the share on the islands,
  islands holding at least 5% of the living, the desert's water against a leaf's break-even, and the empty
  10 m squares.
- The grazer economy read (`scripts/reads/r61-grazer-economy.py`) per pair, for the lines, their generations, and
  intake per grazer-second.
- The theatre: the round is watched before it is written up, with what was seen said.

## What would change the plan

- If a world falls below 500 living, or its share on the islands below 0.4 for two years running, the round has
  failed its first question, and the next step is the current's form (calm water over the shoals), not the dose.
- If the pace falls below 1x for an hour, the round is stopped and the speed fixed first.
