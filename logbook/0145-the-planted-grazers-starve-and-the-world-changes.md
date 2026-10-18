# Round 61's read: the planted grazers starve, the tank fills, and the world changes

*2026-10-18, written the day the round ended, against its pre-registration (logbook/0144). The read is
`logbook/specs/r61-read/read-72000.txt`. Every figure is measured unless it says otherwise.*

Round 61 planted grazers, animals that eat leaves, into round 60's tank, to see whether they would eat the leaves
down and leave empty water between the islands. They did not. The grazers killed leaves in every planted world, but
nearly all of them starved, at a median of about 650 s after arriving. No more than 59 were alive in a planted world at
any year's end. The tank filled with leaves, on the islands and off them. Running the round on from 48,000 s to 72,000 s
changed little. The owner's reading, on 2026-10-18, is that this setup was wrong for what the project is looking
for, and several world rules change because of it.

## What the round asked, and what was tried

Every world shared one map with six islands. A year is 6,000 simulated seconds. In the planted arm the trickle, the
stream of fresh founders that enters a world all through a run, brought in the nursery's ten best leaf swimmers.
The control on the same life seed got none. A grazer is any body with a mouth. Two pairs, on life seeds 6101 and
6102, started on the evening of 2026-10-16 on the graphics card.

At 48,000 s the pairs disagreed about what planting did (`logbook/specs/r61-read/r61-read-48000-six.txt`). Seed
6101's planted world held 0.64 of its control's leaves, and seed 6102's held 2.51 times as many. D195, ruled before
any row was read, then sent a third pair on seed 6103. It also lengthened the round: every pair whose planted world
still had grazers at 48,000 s ran on to 72,000 s, and all three did.

The extra time was hard to buy. In seed 6103's planted world one leaf line grew kelp blades. Each module a blade
adds in life is a longer, thinner copy of the last, and one reached 37 m. Contact treats a part as a sphere about
as wide as the part is long, so the card slowed tenfold. The controls slowed too, because every mouth checked every
corpse at every step. Three engine changes went in on 2026-10-17, each proven to leave every bit of a world
unchanged (`logbook/specs/r61-engine-fixes.md`). Seed 6103's planted world still took six runs to finish. A pace
guard stopped it twice on its blades, and twice an engine limit was raised. By the end 488 of its bodies had blown
up. Every one diagnosed was a blade whose spin ran away under the water's drag
(`logbook/specs/r61-kelp-divergence.txt`). The blades' growth and the drag limiter change only in round 62 (D196,
D199, D200).

## What the read found

The grazers grazed, and nothing else the round hoped for happened. The table gives each clause at 72,000 s, with
the planted worlds in the order of seeds 6101, 6102 and 6103.

| # | asked | result at 72,000 s |
|---|---|---|
| P1 | at least 100 grazers alive at every year's end | failed: 5 to 59 |
| P2 | at least 100 leaf kills a year by grazers in years 6 to 8, controls not | held: 127 to 335 a year, the controls none |
| P3 | planted leaves at most 0.7 of the control's | failed: 0.82, 1.14, 0.30 |
| P4 | at least 0.1 more empty 10 m squares | failed: -0.02, -0.12, +0.22 |
| P5 | at least 0.1 more of the living on the islands | failed: 0.00, -0.115, -0.09 |
| P6 | three islands each with 5% of the living | failed: 2, 2, 1 (held in 2 of 3 at 48,000 s) |
| P7 | 20 grazers a year crossing between islands | failed: at most 3 |
| L1 | no root line over half the living | failed: 0.998, 0.998, 0.644 |
| S1 | grazers that read the smell climb it | unread: 7 to 50 samples of non-readers, against 1,000 needed |
| C1 | the controls repeat round 60 | held |
| B1 | the books balance | failed on seed 6103's 488 blown-up bodies alone |

Seed 6103's planted world is the one place P3 and P4 held. Its leaves fell from 6,662 at 48,000 s to 2,784, and
its grazers killed 1,226 leaves in the last year. The giant blades were sweeping the same world, so I cannot say how much
of the fall was the grazers' (inference).

The other five worlds ended with 8,141 to 10,560 living, 81 to 94% of them leaves (computed). Only 0.16 to 0.28
of their 10 m squares were empty, and 0.28 to 0.40 of the living sat over the islands. One control, seed 6101's, grew 1,199 mouthed
bodies by the end, and they lived on light and killed nothing. Looking down on the end
frames, the owner saw far too many creatures and no empty water anywhere. My first look had found the crowds in
bands along the rims, few bodies on the islands, and planted and control alike. A count in three worlds placed them
(`logbook/specs/open-ocean-survey.md`, section 2). The outer 15 m is 0.17 of the floor and held 0.08 to 0.10 of
the bodies. The band 30 to 60 m in is 0.26 of the floor and held 0.34.

## Why the grazers starved

The grazer economy read is in `logbook/specs/r61-grazer-economy/`. Nearly every living grazer was a fresh founder.
Each planted world took in 1,115 to 1,188 of them, and their lines bred 28, 338 and 134 grazers in 72,000 s. Only
one line outlived its founder's year. A founder arrives with 455 J, and its body costs 0.908 W to keep, so it runs
dry in about 500 s (computed). Founders died at a median 636 to 655 s, 96% of them starved. Only 36 to 45% ever
killed, and a leaf's corpse eaten whole pays about 46 s of upkeep. The swimmers did swim, and the stroke cost about
a tenth of the upkeep. The body's standing cost starved them.

With so few grazers, I think only matter held the leaves back, so they spread wherever it reached. This is my
reading of the numbers above and was not measured on its own.

## What it changes

The grazers' dial search runs first (D198, D201). Each screen runs one year on from 30,000 s with one setting
changed and the trickle off, beside an unchanged copy. Group 1 halved the consumer cell's upkeep, and the grazers
still died out by 33,000 s (`logbook/specs/r62-dial-screens/`). A cheaper body lives longer but does not earn its
keep, so the next group tries the upkeep at an eighth and, apart, the mouth's reach doubled (D203).

The glass tank is retired (D204). New rounds run in a square 300 m a side with its opposite edges joined. Island
shoals, a finer mix of currents and coral sit inside it. Round 62 launches there, once the dials and the coral are
checked again in it (D205). One aim I have proposed for its screens is a population that the islands' food sets,
with the water between them nearly empty.

A personal note: much of the round's last two days went to keeping one world running. The verdicts at
72,000 s are nearly those at 48,000 s, and the crowd's spread at 48,000 s already said most of what this entry
says.
