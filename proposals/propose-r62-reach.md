# Proposal: how far a grazer's reach must go

This is for the owner to rule on. The speed agent prepared it on 2026-10-19 at the coordinator's request, and
nothing launches without the owner's yes.

## What the screen found

The grazer screen (`logbook/specs/r62-grazer-screen/README.md` on `r62-graze`) ran seven worlds for 12,000 s.
Only the mouth's reach moved the grazers. At 2.0 m, in place of 0.5 m, grazers bred 165 against the control's 9.
Their lines reached generation 5, and replacement rose from 0.03 to 0.43 mouthed children per grazer. Their kept
intake rose from about a sixth of their upkeep to about four fifths, by eating corpses they had not made. The
endowment, a faster mouth and richer leaves gave nothing. No arm kept grazers alive on its own, and the oases held
in all of them.

So reach is the dial. The open question is how far it must go before grazers replace themselves, and whether the
leaves survive it.

## The arms

One card group of six worlds, 12,000 s each, on the screen's build and world:

| World | Seed | Reach | Why |
|---|---|---|---|
| `r62g-reach3` | 6201 | 3.0 m | the next step |
| `r62g-reach4` | 6201 | 4.0 m | the step after |
| `r62h-ctl` | 6202 | 0.5 m | a second seed for the control |
| `r62h-reach` | 6202 | 2.0 m | does 2 m's gain hold on another seed |
| `r62h-reach3` | 6202 | 3.0 m | |
| `r62h-reach4` | 6202 | 4.0 m | |

The control and 2 m at seed 6201 are not run again. The farm replays a world bit for bit, so a rerun would only
repeat them. Their slots go to a second seed instead, because one seed cannot tell a real gain from a lucky world.

The read is the screen's. The verdict for each reach:
- does replacement reach one;
- does the grazer count hold up without the trickle carrying it;
- do the living leaves stay above half the control's. That is D201's limit for a dial that overshoots.

The group takes the card for about two hours, at roughly 1.7 to 1.9 times real time.

## Whether to wait for the reach measured from the mouth

The owner has asked for a dial that measures reach from the mouth part, not the body's centre, with today's rule
as its default. It is queued behind two small fixes, and it needs a replay proof before any world uses it.

**My recommendation is not to wait.** The two rules differ by where on the body the distance starts. The census of
the screen's mouthed bodies puts their largest extent at about 1.1 m. So the mouth sits at most about half a metre
from the centre. The two rules differ by up to that much reach, depending on which way the mouth faces. The
steps here are a metre apart, twice that difference. A reach found under the centre rule therefore carries over to
the mouth rule to within about half a metre. If round 62 is to use the mouth rule, one confirming world at the chosen
reach, under that rule, follows this group. It need not hold this group back.

To wait means to build the dial and prove it, then run the arms under both rules in one group. The answer would be
cleaner, and the card would sit idle in the meantime.

## What it would mean for the ocean

The owner's standing guidance asks what makes the world more like a real ocean. A reach of 3 or 4 m is not a mouth.
It stands for searching: a body that eats anything dead within a few metres finds its food the way a forager that
swims and senses would. The creatures here cannot yet see a dead leaf except through leaf scent, and they have not
evolved to follow it well. That is what the nursery arm tested, and its foragers killed less. A long reach is
therefore a stand-in for a skill the world does not yet select for. Whether it is an acceptable stand-in for round
62 is the owner's to rule. The alternative is a world-rule change, a richer sense or a search that costs work, and
that also needs a ruling.

## Ready to launch

- Launchers: `rounds/env-r62g-reach3.ps1` and `rounds/env-r62g-reach4.ps1` on `r62-graze`.
- The group's launcher is `scratch/r62-box/reach/launch.ps1`, and it goes into `rounds/` with the launch commit.
- Config hashes: from the CPU accelerator's preflight, below.

## The question

Does the reach follow-up run as set out, with these six worlds, reach measured from the centre, and the mouth-rule
confirmation after it? Or does it wait for the mouth-rule dial, with both rules in one group?
