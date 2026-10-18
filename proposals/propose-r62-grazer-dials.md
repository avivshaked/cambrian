# Proposal: the grazer dial search before round 62's coral

*Put to the owner for a ruling, drafted by the coordinator on 2026-10-18 from the read in
`logbook/specs/r61-grazer-economy/` (`scripts/reads/r61-grazer-economy.py`). Absorbed into DECISIONS.md on
ruling, then deleted.*

## What round 61 showed

The planted grazers never became a guild of their own. Each planted world took in 1,115 to 1,188 trained
swimmers through the trickle over 72,000 s. Their lines bred 28, 338 and 134 grazers between them, and only
one line, in seed 6102, lived past its founder's year. Founders died at a median of about 640 s, 96% of them
starved, about when their 455 J endowment ran out.

They do swim. Against the drift around them they move 0.054 to 0.081 m/s, where a rigid body moves 0.004 to
0.046. The earlier reading of half a drifter's speed came from a column that averages every jointed body in
the world.

Moving is not what costs them. Pulling a stroke is billed at about 0.1 W. What costs is the body they swim in,
which stands at 0.908 W. Of that, 0.65 W is the consumer cell (6 W per cubic metre) and 0.29 W the mouth and
claw. A leaf killed and eaten whole pays about 46 s of it. In the late years the mouths kept only 12 to 46% of
the joules in the corpses grazers made. The one line that bred had shrunk to a single rigid part costing
0.164 W.

## The dials

Each is a world rule, so each value is the owner's. Values are round 61's.

| | dial | now | what it does for a grazer |
|---|---|---|---|
| A | consumer cell upkeep, `cellTypes.consumer.upkeepWattsPerCubicMetre` | 6 W/m³ | 0.65 W of the swimmer's 0.908 W |
| B | the mouth's reach to food, `mouth.intakeReachMetres` | 0.5 m | how much of its own kill it gets before the corpse sinks away (0.05 m/s) |
| C | what eating keeps, `consumer.carrionYield` and `mouth.intakeWasteFraction` | 0.8 and 0.2 | the joules a kill turns into reserve |
| D | attribute upkeep, `mouth.intakeWattsPerUnit` and `mouth.attackWattsPerUnit` | 0.1 each | 0.29 W for mouth and claw |
| E | the cost of work, `economy.workCostMultiplier` | 0.5 | about 0.1 W; searched last, if at all |

A cheaper consumer cell (A) helps every body with a mouth, including the 1,199 mouthed bodies in seed 6101's
control that photosynthesise and kill nothing. So the screens read that guild too.

## How the search runs

The owner's rule of 2026-10-17 is to push each dial in the grazers' favour until it overshoots. The leaves
are then eaten down too far, and the dial comes back between the last two settings.

- **Screens.** Cousins resumed from round 61's planted worlds at 48,000 s, each run for one year (6,000 s)
  with one dial changed, beside a cousin with nothing changed.
- **The ladder.** Each dial is halved (a cost) or doubled (a yield or the reach) per step. One dial at a time,
  in the order A, B, C, D, since that is the order of what the read says binds.
- **The read.** The bred grazers alive at the year's end whose line's founder arrived a year or more before
  (round 61 had 0 to 5). Then the kills and the joules kept per grazer, and the leaves alive against the
  unchanged cousin.
- **Overshoot.** The living leaves fall below half the unchanged cousin's.
- **Afterwards.** The values found go to the owner as round 62's grazer economy, beside the coral (D194) and
  the blades' rules (D196, D199, D200).

## What it needs first

- **A resume with changed dials.** The farm takes a resumed world's config from the run it resumes and ignores
  the launcher's settings. The screens need a cousin resume from an edited copy of the config, marked as a
  cousin in its manifest. That is a small farm change, about an afternoon of agent time, unless it already
  exists.
- **Which build.** Round 61's merged build, or round 62's with the blades' limiter, whose card copy the speed
  agent is building now. Round 62's build means the values found are the ones the round runs on.

## Cost

About an afternoon of agent time for the resume change and a reader. On the card, a group of four worlds runs
a year in about 1.5 to 2.5 hours, since round 61's four ran at 0.7 to 1.1 times real time each. Four dials of three
steps with a cousin each is about four groups, 6 to 10 hours of card time, under the heat watch.

## Questions for the owner

1. Are dials A to D the ones to search, in that order, and is E left out?
2. Should the trickle stay on in the screens, as in round 61, or go off? Off asks directly whether grazers
   breed on their own, but changes one more thing at once.
3. Is "living leaves below half the unchanged cousin's" the right overshoot?
4. All three planted seeds, or seed 6102 alone, where the one line bred?
5. Round 61's build, or round 62's?
