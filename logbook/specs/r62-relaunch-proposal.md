# Proposal: round 62 relaunched with a 3 m reach

This is a draft for the owner, prepared by the speed agent on 2026-10-20 at the coordinator's request. It amends
round 62's pre-registration (`logbook/specs/r62-prereg/predictions.md`) in two variants. One has no spoilage, and
the other a corpse half-life of 500 s. For each it states what the screens predict. It is not a pre-registration
and nothing launches from it. If the owner rules for one variant, its text becomes the round's new predictions file
and is committed before the launch.

## Where this comes from

The owner stopped round 62 at about 900 s on 2026-10-19, judging that its grazer dials had not been found. The
coordinator then ran two screens.

- **The grazer screen** (`logbook/specs/r62-grazer-screen/README.md` on `r62-graze`): of reach, intake rate, leaf
  energy and founder endowment, only the mouth's reach moved the grazers. Its control and its 2 m arm ran on to
  24,000 s as a long baseline.
- **The spoilage screen** (`logbook/specs/r62-spoil-screen-proposal.md`, D213): eight worlds of reach by corpse
  half-life to 24,000 s. At 3 m, grazers replaced themselves with or without a 500 s half-life. Replacement per
  cohort reached 1.08 to 1.24, about 2,750 were bred, and about 150 mouthed bodies were alive at the end. At 2 m,
  the result depended on the seed. At 0.5 m nothing kept grazers alive, with spoilage or without.

Both screens also changed the founding. Round 62's own pool held no mouthed founder, so the trickle draws from round
59's pool (`rounds/r59-pool.tsv`) at a share of 0.5. Every variant below keeps that.

## What the screens cannot say

- **3 m ran on one seed only.** At 2 m, seed 6201 bred 1,334 grazers and seed 6202 bred 512, and only 6201's lines
  ran deep. 3 m may sit far enough past the edge that the seed stops mattering, or it may not. The round's three
  seeds are the first test of that.
- **Nothing has run past 24,000 s at 3 m.** The living were still rising at 24,000 s, to between 4,000 and 7,400
  a world, and so was the count of grazers. Whether the guild booms and crashes as the 2 m long baseline did in its
  last winter is unmeasured.

## The world, common to both variants

Round 62's world as pre-registered (D204 to D209), with these changes:
- the trickle draws from round 59's pool at a share of 0.5, as in both screens;
- the mouth's reach is 3.0 m, measured from the body's centre as today;
- the build is `r62-spoil` (`92b61136`), which carries the reach and the half-life dials. With the half-life at 0
  it is bit for bit round 62's world, as its recorded fixtures show.

Six worlds, founded, on the card in one group, on seeds 6201 to 6203, as before. The length is the owner's to set.
At the screens' crowds the card ran eight worlds at 1.1 to 1.9 times real time near 24,000 s. Six worlds at
72,000 s would therefore take roughly 10 to 15 hours. That is a guess, and the run takes longer if the crowd keeps
growing. Checkpoints every 1,500 s let a read be taken at 24,000 s and the run stopped at any point.

## Variant A: no spoilage

Pairs on each seed: the mouth's reach at 0.5 m (the control) and at 3.0 m, with the half-life off.

The screens predict what follows. Their numbers are measurements at seed 6201; extending them to the other seeds
and past 24,000 s is a guess.

1. **Oases hold.** At 20,000 s and at the end, at least 0.6 of the living sit on the islands and at least 0.6 of
   the 10 m squares are empty, in all six worlds. The screens read 0.73 to 0.77 and 0.75 to 0.79 at 24,000 s.
2. **The crowd.** The living stay above 1,500 after 10,000 s in every world. The pre-registration's ceiling of
   6,000 is withdrawn, since two screen worlds passed it by 24,000 s. A guess: under 12,000 at the end.
3. **Grazers keep themselves going at 3 m.** In at least two of the three 3 m worlds, at least one birth cohort
   of 3,000 s between 12,000 and 24,000 s has a replacement of 1 or more. In the same two, the deepest chain of
   mouthed parent to mouthed child reaches 10 by 24,000 s. In the screen it was 18.
4. **At 0.5 m they do not.** In every control world, replacement stays under 0.3 in every cohort, as in the
   screen's control (at most 0.23).
5. **The summer famine is gone at 3 m.** In the 500 s windows from 250 s before each summer to 1,250 s after it,
   from 12,000 s on, the 3 m grazers meet at least 0.9 of their upkeep in two of three worlds. The screen read 1.04
   to 1.20.
6. **Corpses still feed most of it.** In every 3 m world, corpses that no grazer killed bring in more of the
   grazers' intake than their own kills do.
7. **Hybrids**, unchanged from the pre-registration. Mouthed bodies with photosynthetic cells outlive mouthed
   bodies without them at the median age at death, in every world.

## Variant B: a corpse half-life of 500 s

Pairs on each seed: 0.5 m and 3.0 m, both with the half-life at 500 s.

The predictions are variant A's, with two changes and two additions.

- **Prediction 3** carries over unchanged. The screen's 3 m world at 500 s bred 2,756 grazers against 2,782
  without spoilage, with replacement 1.16, 1.08 and 1.08 against 1.24, 0.89 and 1.08.
- **Prediction 5** reads at least 0.85 rather than 0.9, since the screen at 500 s read 0.96 to 1.14.
- **Kills.** A grazer kills living leaves a little more often than in variant A. The screen at 3 m read
  2.2 × 10⁻³ kills per grazer-second against 1.9 × 10⁻³ without spoilage. The round has no unspoiled 3 m world to
  compare with. The comparison is with the screen's `r62s-r3-h0`, which used the same world at seed 6201.
- **More leaves.** In the screen, three of four pairs held more leaves with spoilage, by about 1,000 to 1,400 at 24,000 s.
  The matter of a spoiled corpse goes straight to the spent field, where the leaves take it up (inference). The
  round compares each world with the screen's world of the same seed and reach where one exists.

## The variant I would choose, and a third shape

I would choose B, because it is more like an ocean at no measured cost to the guild. Carrion spoils within a day in
the sea. At 3 m the grazers did as well with a 500 s half-life as without one, killed living leaves a little more
often, and the leaves did better. The risk is the single seed: at 2 m on seed 6201, spoilage cut the bred grazers
from 1,334 to 257. If 3 m sits near the edge on seeds 6202 and 6203, spoilage could tip them.

A third shape tests that risk directly. Pair 3 m without spoilage against 3 m at 500 s on each seed, and drop the
0.5 m control. The screens already show the control on record: at 0.5 m nothing kept grazers alive, at seed 6201 with
spoilage and without. The oasis and hybrid questions are read within each world, and need no control. The
round would then answer for the grazers on three seeds, and say what spoilage does to them over a whole round.

## The mouth point: whether to build it first

The mouth point (agreed between the speed agent and the video agent on 2026-10-19,
`scratch/r62-box/mouth/definition.md`) measures reach from the posed face of the eating part rather than from the
body's centre. Under it, a body has to face its food to eat.

**My recommendation is to relaunch without it, and build it beside the running round for round 63.**

- **It is not a small build.** Core holds one position per body, its centre. The solver hands Core a height each step
  (`World.Observe`), and nothing more. The mouth point needs the eating part's posed face every step. That means a
  helper in Core, a new path from the CPU solver, and on the card a kernel that computes the point and a read-back
  of one point per mouthed body. The card change needs the identity check at groups 32 and 64. My estimate, in agent
  time: two to four hours of building and review for the Core helper and the CPU path, and three to five more for
  the card path and its identity runs. Then the reach has to be found again under the new rule, since the screens
  measured from the centre. That is one more screen of about six hours on the card, before the round could launch.
- **It changes what 3 m means.** Bodies run 0.5 to 1.2 m long. Moving the origin from the centre to the face
  shifts the reach by up to half that, 0.25 to 0.6 m. The screens put the edge between 2 m and 3 m, so a shift that
  size could matter. A change that size needs its own screen before a round rests on it.
- **What it buys is real.** A grazer that must face its food is under selection to steer. The ocean asks that of a
  grazer, and the owner's standing guidance asks it of a round. That is the case for building it next. It is not a
  case for holding round 62 another day.
- **The card is the constraint.** Round 62 would hold the card for the 10 to 15 hours of its run. The mouth point's
  CPU part and its tests can be built meanwhile. Its identity runs and screen wait until the round ends, or run in a
  window the coordinator opens.

## The decisions

1. Which variant: A, B, or the third shape (3 m with and without spoilage, no control).
2. The round's length: 72,000 s as pre-registered, or shorter with a read at 24,000 s.
3. Whether the mouth point waits for round 63, as recommended, or is built and screened before the relaunch.
