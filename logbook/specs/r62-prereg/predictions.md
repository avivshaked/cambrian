# Round 62's predictions, as relaunched: twins at a 3 m reach, with and without spoiling corpses

*2026-10-23, committed on `r62-launch` before the round's launch. The ruling is D214 in `DECISIONS.md`, on the
spec `logbook/specs/r62-relaunch-proposal.md`; the world's rulings are D204 to D209, the clock's D212. This file
replaces the pre-registration of 2026-10-19 (`1465b670`), whose six worlds were stopped at about 900 s (D210). The
screens it rests on are the grazer screen (`logbook/specs/r62-grazer-screen/README.md` on `r62-graze`) and the
spoilage screen (`logbook/specs/r62-spoil-screen/`). Every number below that is not a measurement is a guess, and
says so.*

## What it asks

Round 61's grazers starved, and the first launch of round 62 was stopped before its grazers could be read, because
no dial had yet been found that kept them alive. Two screens since then found one: the mouth's reach. At 3 m,
grazers replaced themselves for 24,000 s at seed 6201, with or without corpses that spoil. The round asks four
things.

1. Do the islands hold their life as separate oases for a whole round, with the water between nearly empty?
2. Does a 3 m reach keep grazer lines alive on three seeds, and past 24,000 s?
3. What does a corpse half-life of 500 s do to a grazer guild that lasts, and to the leaves?
4. Do bodies that both photosynthesise and eat do better than pure grazers, as D207's evidence suggests they
   should?

## The world

The world is round 62's as pre-registered on 2026-10-19. It is an open square 300 m a side whose edges join, with
island shoals and an ocean current at 0.02 m/s (D204). Plants keep their spent matter (D206), an eater releases its
share after its own leaves (D207), and branching coral grows on the islands (D194). Three things differ from that
launch.

- The trickle draws from round 59's pool (`rounds/r59-pool.tsv`) at a share of 0.5, as in both screens, since
  round 62's own pool holds no mouthed founder.
- The mouth's reach is 3.0 m, measured from the body's centre.
- The build is `r62-spoil`, which carries D212's clock. With the half-life at 0 it is bit for bit round 62's world,
  as its recorded fixtures show.

Six worlds, founded, 72,000 s each, in one group on the card with the snow carried there, as twins on seeds 6201,
6202 and 6203:

- `r62-n6201` to `r62-n6203`: no spoilage (`rounds/env-r62-n.ps1`).
- `r62-h6201` to `r62-h6203`: a corpse half-life of 500 s (`rounds/env-r62-h.ps1`).

The two launchers differ only in `EVOSIM_CORPSE_HALF_LIFE`. Config hashes, read off the accelerator preflight
(`rounds/preflight-r62.ps1`): `4c4a6bac75591d2e` for every `r62-n` world and `c5c9f4167e070655` for every `r62-h`
world. They are the spoilage screen's `r62s-r3-h0` and `r62s-r3-h500`, so each twin is that screen's world on a new
seed, and the two configs differ in `corpseHalfLifeSeconds` alone. In the preflight all six worlds founded and
stepped 15 s of their 30 on ILGPU's CPU accelerator before its 15 min wall, with the round 59 pool's hash
`268f395b…` in each. Each pair shares its seed, so a pair differs only by the clock until the first difference
compounds. The landscape is seed
11, the weather seed 57, the population cap 25,000, as before. The group runs at group size 32 on cores 16 to 29,
with checkpoints every 1,500 s (`rounds/launch-r62.ps1`). The round is read at 24,000 s and at the end.

## Predictions

The screens ran to 24,000 s, at 3 m on seed 6201 only. Their numbers are measurements there; carrying them to the
other two seeds and past 24,000 s is a guess.

1. **Oases hold.** At 20,000 s and at the end, at least 0.6 of the living sit on the islands and at least 0.6 of
   the 10 m squares are empty, in all six worlds. The screens read 0.73 to 0.77 and 0.75 to 0.79 at 24,000 s.
2. **The crowd.** The living stay above 1,500 after 10,000 s in every world. The earlier ceiling of 6,000 is
   withdrawn, since two screen worlds passed it by 24,000 s. A guess: under 12,000 at the end.
3. **Grazers keep themselves going, with spoilage and without.** Take the birth cohorts of 3,000 s between 12,000
   and 24,000 s. In at least two of the three worlds of each twin, at least one cohort has a replacement of 1 or
   more, and the deepest chain of mouthed parent to mouthed child reaches 10 by 24,000 s. At seed 6201 the screen read a chain of
   18 without spoilage and 23 at 500 s, and replacement per cohort of 1.24, 0.89 and 1.08 against 1.16, 1.08 and
   1.08. A guess with low confidence past 24,000 s: the guild still stands at the end in at least one world of
   each twin, though the 2 m long baseline boomed and crashed in its last winter.
4. **The summer famine is gone.** The read takes the 500 s windows from 250 s before each summer to 1,250 s after
   it, from 12,000 s on. In them the grazers meet at least 0.9 of their upkeep in two of the three worlds without
   spoilage, and at least 0.85 in two of the three with it. The screen read 1.04 to 1.20 without and 0.96 to 1.14 at 500 s.
5. **Corpses still feed most of it.** In every world without spoilage, corpses that no grazer killed bring in more
   of the grazers' intake than their own kills do. With spoilage, a guess: the same holds in at least two of the
   three.
6. **Spoilage pushes grazers toward living food.** On at least two of the three seeds, the world with spoilage
   has more grazer kills of living bodies per grazer-second than its twin by 24,000 s. The screen read
   2.2 × 10⁻³ against 1.9 × 10⁻³ at seed 6201.
7. **Spoilage feeds the leaves.** On at least two of the three seeds, the world with spoilage holds more
   photosynthetic bodies at 24,000 s than its twin. In the screen three of four pairs did, by about 1,000 to 1,400.
   The matter of a spoiled corpse goes straight to the spent field, where the leaves take it up (inference).
8. **Hybrids**, unchanged. Mouthed bodies with photosynthetic cells outlive mouthed bodies without them at the
   median age at death, in every world.
9. **Pace.** The round runs at 1x real time or better six-up to 24,000 s. Eight worlds of the spoilage screen ran
   at 1.1 to 1.9x near 24,000 s; past it, with the crowd still growing, a guess. At that pace the round takes
   roughly 10 to 15 hours, also a guess.

The spec's prediction 4, that a 0.5 m control keeps no grazer line alive, is dropped with the control (D214): both
screens already showed it, with spoilage and without.

## How it is read

- `python -I scripts/reads/r60-read.py --year 6000` on each world, for the living, the share on the islands,
  islands holding at least 5% of the living, the desert's water against a leaf's break-even, and the empty
  10 m squares.
- The screen's grazer reads on each world: `scripts/reads/r62g-read.py` for the bred, chains, replacement, kills
  and intake per grazer-second, `r62g-seasons.py` for the summers, and `r62g-windows.py` for the cohorts. The
  grazer economy read (`scripts/reads/r61-grazer-economy.py`) per twin, for where the grazers' intake comes from.
- The theatre: the round is watched before it is written up, with what was seen said.

## What would change the plan

- If a world falls below 500 living, or its share on the islands below 0.4 for two years running, the round has
  failed its first question, and the next step is the current's form (calm water over the shoals), not the dose.
- If the pace falls below 1x for an hour, the round is stopped and the speed fixed first. The launcher's wall
  limit of 25 h is a backstop only.
- If the grazers die out in every world of both twins before 24,000 s, the screens' 3 m result was a single seed's,
  and whether the round runs on to 72,000 s for its oases alone is put to the owner.
