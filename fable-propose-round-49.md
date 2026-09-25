# Proposal: round 49's world, three questions and what is already built

*The agent, 2026-09-25, after round 48's read (logbook/0120) and the afternoon's reads added to
it. This is the one text for the owner to rule round 49 on. The world rules are the owner's;
the build, the fixtures and the pre-registration are mine once ruled. Absorbed into
DECISIONS.md on ruling, then deleted. Nothing here starts before the machine ruling HANDOFF
puts first, since round 49 is three long farm runs.*

## What round 48 left open

Round 48 asked whether an eater could get in, and the answer was mostly no. Every copy of
the one-part stomach that the pool sent in had its child half a second after landing, paid
from the endowment D122 gave it. It then starved in about three minutes, feeding under its
break-even. Two of those children founded lines, of 45 and 24 living, and both died out. By
0120's reading, which is inference from two lines, their oldest members starved as their
upkeep climbed with age. What lasted was a leaf with a speck of stomach on it, which the
stomach did not feed.

The afternoon changed how two of those readings stand.

- The check on the placing rule was blind (0120, F4's section). A stomach draws about half
  of its own 1 m cell each half-second step. The first reading a founder logs comes ten steps
  after landing, so it is the refill of a cell the founder is emptying. F4's failure says
  nothing about whether D122 put founders in richer water. D122's own premise came from the
  same witness: round 47's stomach founders "landed in about 0.12 J/m³" by their first
  reading, the emptied cell's.
- A feeder is fed by the refill, and not by the water round it (0120, and the gotcha in
  CLAUDE.md). In round 48's water a mouth's cell holds 5 to 25% of an untouched copy's after
  a minute. The stirring refills it at 0.06 to 0.1 of the gap a second, and the current adds
  some only where it moves. An eater's steady intake is close to the refill rate times the
  density round it, and a bigger stomach buys little past that. A ledger break-even is a
  density in the emptied cell. So the stomach screens of 2026-09-23, "about 0.5 J/m³ against
  a break-even of 0.44", set two different things side by side.

## 1. Whether a founder's endowment may pay for its child

D122 gives every founder 600 s of its own standing watts in reserve, so that it can live while
it finds its feet. The one-part stomach's child costs less than that, and the world lets any
reserve pay for a child. So the endowment bought a child at landing, and the founder then
lived on what was left. Round 48 tested whether a founder's child could live where its parent
landed. What D122 was written to test is whether the founder could.

The options:

- (a) Keep it. Round 49 then repeats round 48 on this point, and every pool stomach breeds at
  once.
- (b) The endowment pays upkeep only. This is my recommendation. The world keeps the
  endowment as a separate account that pays the founder's upkeep first and never counts
  toward a child, so a founder breeds only on what it earned. It tests what D122 was meant
  to test.
- (c) Drop the endowment. Round 47 showed what that gives: its pool founders died at a median
  of 24 to 46 s.

What (b) implies:

- Every seed is a new realisation, as round 49 is anyway.
- For the round, fewer pool children at landing, probably far fewer. A pool stomach breeds
  only if it lands where the refill pays more than its upkeep. The lines of 45 and 24 came
  from children the endowment paid for, so round 49 may see no stomach line at all. That
  would be a true answer to the round's question.
- The machine is unaffected.
- The risk is that an eater never gets in, and round 50 has to change the eater's economy
  rather than its founding. Section 3 is the candidate for that.

The question is whether to keep the endowment as an upkeep-only account that cannot pay for a
child, option (b).

## 2. Whether an eater's upkeep still wears with age

D121 took senescence off intake and left it on upkeep, which rises as one plus the age over
3,000 s. The ledger puts the one-part stomach's break-even at 0.44 J/m³ at birth. If intake
grows with density, it climbs to about 0.59 at 1,000 s and 0.88 at 3,000 s. The last of the
two stomach lines died that way, its oldest members starving as the break-even rose past what
their patch gave.

The options:

- (a) Keep the wear as it is. This is my recommendation for round 49. Two lines are thin
  evidence, and section 1's endowment decides whether a line starts at all. Changing both at
  once would leave round 49 unable to say which did what.
- (b) Take the wear off upkeep too, so a body never ages. Senescence (D038) exists to turn
  the crowd over, and without it founders never age out. Every morphology share would then
  read the founders for longer.
- (c) Replace the wear with a hazard, a chance of death that rises with age and no rising
  cost. The crowd still turns over, and no old eater is starved by the arithmetic. It is a
  new mechanism, and more than a dial.

The question is whether to keep the wear on upkeep for round 49, and read the new death rows
before ruling on (b) or (c).

## 3. How an eater is fed, for information

The refill sets what an eater eats. The snow's stirring is 0.02 m²/s in round 48
(`nutrientMixingDiffusivity`). At 0.2 m²/s the stirring alone would refill a mouth's cell
about ten times faster. An eater's intake would then rise toward what the ledger reads from a
field density. It would also smooth the snow's layers and patches, which are where an eater
finds a rich place now. The matter the leaves take up has its own dial, and nothing here
touches it.

The options:

- (a) No change in round 49. This is my recommendation, for section 2's reason: one change
  at a time.
- (b) Raise the snow's stirring alone. This is the lever to try in round 50 if eaters still
  fail under section 1's rule.
- (c) Change the feeding rule, so that a mouth reads a neighbourhood rather than its own
  cell. That is a new mechanism, and I do not propose it here.

No ruling is needed on this now. If the owner wants (b) screened alongside round 49's
pre-registration, it is a ledger screen and a short smoke.

## 4. The placing rule stays, and is measured this time

D122's depth rule was never tested, since its check read the wrong witness. I propose keeping
it and measuring it with the instruments below. That is a reading and not a new rule, so it
carries no question. If the owner would rather drop it, section 1's option (b) still stands
alone.

## What is built for round 49 and needs no ruling

All of it is on `r49-record-film` at `1f06a67`: 187 filtered tests pass and every assembly
compiles outside Unity. None of it has run on the farm or in Unity.

- D123: the contact and damage senses read the last metabolic step.
- Two instruments:
  - a death row carries the gestation account and the reserve the body died with;
  - a founder row carries its food at the landing point and its column's mean.
  - `scripts/reads/r49-witness.py` reads both, so round 49 can check D122 and G2.
- The bite fix, on `r49-bite-rebuild` and merging next. A bitten body is rebuilt on the step
  that bit it, so a bite no longer lands on the wrong part for up to 10 s.
- The record and the films: a smaller record, checkpoints every 500 s, and story films drawn
  from farm-recorded windows.

## The questions, together

1. Should the endowment pay upkeep only and never count toward a child (section 1, option
   (b))?
2. Should the wear stay on upkeep for round 49 (section 2, option (a))?
3. Is anything wanted on the snow's stirring for round 49 (section 3)? My recommendation is
   no.
