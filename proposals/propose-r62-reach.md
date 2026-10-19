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

One card group of eight worlds, 12,000 s each, on the screen's build and world:

| World | Seed | Reach | Why |
|---|---|---|---|
| `r62g-reach3` | 6201 | 3.0 m | the next step |
| `r62g-reach4` | 6201 | 4.0 m | the step after |
| `r62h-ctl` | 6202 | 0.5 m | a second seed for the control |
| `r62h-reach` | 6202 | 2.0 m | does 2 m's gain hold on another seed |
| `r62h-reach3` | 6202 | 3.0 m | |
| `r62h-reach4` | 6202 | 4.0 m | |
| `r62g-nursery-reach` | 6201 | 2.0 m | the nursery's foragers as the pool |
| `r62g-nursery-reach3` | 6201 | 3.0 m | the same at 3 m |

The control and 2 m at seed 6201 are not run again. The farm replays a world bit for bit, so a rerun would only
repeat them. Their slots go to a second seed instead, because one seed cannot tell a real gain from a lucky world.

The last two arms draw the trickle from the nursery's 23 trained foragers in place of round 59's swimmers. The
screen tried them only at the old 0.5 m reach, where skill did not pay. At 2 m food is within reach of a searcher,
so these arms ask whether trained foragers then beat round 59's swimmers. They compare with the screen's `r62g-reach`
at the same seed.

The read is the screen's. The verdict for each reach:
- does replacement reach one;
- does the grazer count hold up without the trickle carrying it;
- do the living leaves stay above half the control's. That is D201's limit for a dial that overshoots.

The group takes the card for two to three hours. Seven worlds ran at 1.7 to 2.2 times real time, and eight will run
a little slower.

## Option: four years in place of two

The screen's two years tangle season with run time. Grazers grow through the run, so the best half-year was also the
last. A 500 s read of the reach arm found the hard time at the start of summer. In the first 1,000 s after the
winter-to-summer switch, grazers fell to their fewest and met a third of their upkeep. Kills of living leaves fell
too. At the summer-to-winter switch they feasted on starving leaves (`scripts/reads/r62g-windows.py` on
`r62-graze`). Two years show each switch only twice.

The owner can choose to run the follow-up for 24,000 s, four years, so each switch is seen four times and a late
gain can be told from a seasonal one.
- **Machine time.** The screen's seven worlds took 90 to 117 minutes of wall time for 12,000 s. Eight worlds take
  about 2 to 2.5 hours for 12,000 s, and about 4.5 to 5 hours for 24,000 s. That assumes the living count stays near
  the screen's 1,400 to 1,700 after the first year, which it did; it is not measured past 12,000 s.
- **Machine use.** The card is held for the whole time. Cores 16-29 run with the heat stop.
- **What it gives.** Each world checkpoints every 1,500 s, so the 12,000 s answer can be read halfway, and the run
  can be stopped there if it has already answered.
- **The launcher.** `rounds/launch-r62h.ps1` takes the length from its `-Seconds 12000`, which changes to 24000.

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

- Launchers on `r62-graze`: `rounds/env-r62g-reach3.ps1`, `rounds/env-r62g-reach4.ps1`,
  `rounds/env-r62g-nursery-reach2.ps1` and `rounds/env-r62g-nursery-reach3.ps1`.
- The group's launcher is `rounds/launch-r62h.ps1` on `r62-graze`.
- Config hashes: from the CPU accelerator's preflight, below.

## The question

Does the reach follow-up run as set out, with these eight worlds, for 12,000 s or 24,000 s, reach measured from the centre, and the mouth-rule
confirmation after it? Or does it wait for the mouth-rule dial, with both rules in one group?
