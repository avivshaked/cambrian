# Proposal: spoiling corpses crossed with the mouth's reach

This is for the coordinator to rule on overnight, on the owner's leave. The speed agent prepared it on 2026-10-19,
after D212. Nothing launches without the coordinator's yes.

## Where this comes from

The grazer screen (`logbook/specs/r62-grazer-screen/README.md` on `r62-graze`) found that only the mouth's reach
moved the grazers. At 2.0 m they bred 165 against the control's 9, and their kept intake rose from about a sixth
of their upkeep to four fifths. They did it by scavenging corpses they had not made. In 500 s windows they starve
in the first thousand seconds of summer, when fresh corpses stop falling, and they do not turn to living leaves
(`scripts/reads/r62g-windows.py`).

D212 rules a spoilage clock: a corpse's food value halves every H seconds, the lost energy leaves as heat, and its
matter goes to the spent field. The clock is built on `r62-spoil` behind `EVOSIM_CORPSE_HALF_LIFE`, default off.
The coordinator ruled overnight under D212 that the dial covers corpses only. Detritus already spoils this way
through remineralisation, whose half-life is about 1,386 s, and a screen may vary that rate beside the half-life.
D212 also rules that reach is tuned with spoilage on, since spoilage shrinks what reach won.

## The half-lives

Two values, from the ocean and from the world's own clocks:

- **500 s, half a world day.** Microbes spoil carrion within a day or two in the sea. Fish carrion left a day or two
  was about four times less likely to be eaten (`logbook/specs/matter-ocean-scan.md`; that source was seen only as
  a press release). At 500 s a corpse loses three quarters of its food in a world day, so only a fast scavenger or a
  hunter gets the best of it.
- **2,000 s, two world days.** About as slow as the corpse's own decay into detritus (a half-life near 1,800 s at
  `corpseDecayPerSecond` 0.000385) and the detritus's remineralisation (1,386 s). It halves what a slow scavenger
  finds without making carrion worthless.

A sinking corpse here leaves the world once it holds under 5% of its first joules. Without spoilage that takes about
7,800 s; at 2,000 s it takes about 4,100 s, and at 500 s about 1,700 s. Those figures are worked from the rates,
not measured; the screen's read will measure them.

## The arms

One card group of eight worlds, 12,000 s each, on `r62-spoil`'s build and the grazer screen's world:

| World | Seed | Reach | Half-life | Why |
|---|---|---|---|---|
| `r62s-r2-h500` | 6201 | 2.0 m | 500 s | the reach that worked, hard spoilage |
| `r62s-r2-h2000` | 6201 | 2.0 m | 2,000 s | the reach that worked, mild spoilage |
| `r62s-r3-h0` | 6201 | 3.0 m | off | the next reach, without spoilage |
| `r62s-r3-h500` | 6201 | 3.0 m | 500 s | |
| `r62s-r3-h2000` | 6201 | 3.0 m | 2,000 s | |
| `r62s-c-h500` | 6201 | 0.5 m | 500 s | does spoilage alone push grazers to kill living leaves |
| `r62t-r2-h0` | 6202 | 2.0 m | off | a second seed for 2 m without spoilage |
| `r62t-r2-h500` | 6202 | 2.0 m | 500 s | a second seed for the key cell |

The control and 2 m without spoilage at seed 6201 are the grazer screen's own worlds. D212's long baseline extends
them to 24,000 s on their own build, so they are not run again.

The read is the screen's (`r62g-read.py`, `r62g-seasons.py`, `r62g-windows.py`, `r60-read.py`). For each cell it
asks four questions:
- does replacement reach one;
- does a grazer kill living leaves more often than it empties corpses it did not make;
- does the start of summer stop being a famine;
- do the living leaves stay above half the control's (D201's limit).

## Machine time

The grazer screen's seven worlds took 90 to 117 minutes of wall time for 12,000 s. Eight take about 2 to 2.5
hours, on the card with cores 16-29 and the heat stop. The coordinator may choose 24,000 s instead, to match the
long baseline, for about 4.5 to 5 hours. That figure assumes the living count stays near the screen's 1,400 to
1,700, which it did to 12,000 s and is not measured past it. Checkpoints every 1,500 s let a long run be read at
12,000 s and stopped there.

## Ready to launch

- The clock's build: `r62-spoil`, after its tests, the accelerator suite and a short card identity run with the
  dial on.
- Launchers and the group launcher: written once the build lands, with each arm's config hash from an accelerator
  preflight.

## The question

Does the spoilage-by-reach screen run as set out, with these eight worlds, at 12,000 s or 24,000 s?
