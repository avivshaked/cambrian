# r67, the four long worlds: the looks

Each look is read against the pre-registration (`logbook/specs/r67-prereg/predictions.md` on `card-kit`). The worlds
ran as `runs/r67-*` to 7,500 s, and from there as `runs/r67r1-*`, resumed from their 7,500 s checkpoints after a
graphics driver event at 21:49 on 2026-11-05 (`logbook/specs/machine-and-tools.md`). A resume replays exactly, so the
two runs are one world each. Reads that need the founders chain the two runs.

## 12,000 s (read at 22:35-22:45 on 2026-11-05)

**Who the animals descend from** (`scripts/reads/pool-origins.py runs/r67-<seed>/... runs/r67r1-<seed>/... --at`):

| Seed | 6,000 s | 9,000 s | 12,000 s | of them, the 40-generation exam's | the shaped arm's |
|---|---|---|---|---|---|
| 6201 | 72 | 325 | 114 | 93 | 21 |
| 6202 | 152 | 115 | 144 | 122 | 22 |
| 6203 | 43 | 174 | 196 | 160 | 36 |
| 6204 | 6 | 25 | 29 | 24 | 5 |

- **Readout 2 holds in all four.** Every living animal descends from a pool founder; the random founders left no
  animal lines.
- **Readout 3: the 40-generation exam's lines outnumber the shaped arm's in all four worlds at 12,000 s**, by four to
  six times. Prediction 3 asked for 3 of the 4. The shaped arm's lines did not lose from the start. On 6202 they
  held 114 of 152 animals at 6,000 s, and on 6204 15 of 25 at 9,000 s, and then fell behind.
- **Readout 1** is judged on the 3,000 s windows from 12,000 s, so it is first read at 15,000 s. Seed 6204 stood at
  29 animals at 12,000 s, one under the line.

**Pace, heat and disk.** The worlds ran at 2.0 to 2.9 times real time over the half hour before the look. The CPU
reached 82 °C while the look's reads ran beside the card group on logical 0 to 3, and the card was at 50 °C. The
round held 1.40 GB, with 394 GB free.

**Pictures** were not taken. No Unity runs beside the card group tonight, after the driver event.

Inference: the shaped arm's founders spend more than they eat in the tank, and in these worlds their lines rose
early, when the first plants were young, and then lost ground. Whether they hold a share or fade is the next looks'
question.

## 15,000 s, the first window (read at 23:08 on 2026-11-05)

Living animals every 500 s from 12,000 s to 15,000 s, and the two exams' lines at 15,000 s:

| Seed | Fewest in the window | At 15,000 s | the 40-generation exam's | the shaped arm's |
|---|---|---|---|---|
| 6201 | 86 (13,500 s) | 346 | 287 | 59 |
| 6202 | 144 (12,000 s) | 490 | 370 | 120 |
| 6203 | 103 (13,000 s) | 418 | 289 | 129 |
| 6204 | 29 (12,000 s), 39 from 12,500 s | 136 | 132 | 4 |

- **Readout 1 holds in three worlds and stands on the line in the fourth.** Seed 6204 counted 29 animals at the
  window's first sample and 39 or more at every sample after it. Read at 500 s samples, the dip is one sample at the
  window's edge.
- **The animals boomed in every world after 13,500 s**, two- to fourfold in 1,500 s.
- **The shaped arm's lines grew too.** They hold 31 % of the animals on 6203, 24 % on 6202 and 17 % on 6201, and
  have nearly gone from 6204, at 4 of 136.

**Pace and heat.** The pace fell as the worlds filled, to 1.3 to 2.2 times real time at 23:08 (`pace-check.py`),
above the line of 0.5. The round's own heat log reads a highest CPU of 82 °C since the resume, during the 12,000 s
look's reads, and 56 to 60 °C at 23:08, with the card at 50 °C.

## 18,000 s, the second window (read at 23:40 on 2026-11-05)

| Seed | Fewest from 15,000 s | Most | At 18,000 s | the 40-generation exam's | the shaped arm's |
|---|---|---|---|---|---|
| 6201 | 346 (15,000 s) | 1,070 (17,000 s) | 940 | 850 | 90 |
| 6202 | 165 (18,000 s) | 490 (15,000 s) | 165 | 100 | 65 |
| 6203 | 418 (15,000 s) | 833 (16,500 s) | 622 | 513 | 109 |
| 6204 | 98 (17,500 s) | 163 (15,500 s) | 153 | 105 | 48 |

- **Readout 1 holds in all four worlds** over the second window, at 500 s samples.
- **Three worlds peaked and are falling.** Seed 6202 fell by two thirds from 15,000 s, and its thrifty lines fell
  faster than the shaped arm's: the shaped arm's share rose from 24 % to 39 %. On 6204 the shaped arm's lines came
  back from 3 to 48, a third of the animals.
- **Pace and heat at 23:40.** 1.46 to 1.63 times real time, CPU 62 °C, card 50 °C.

Inference: a boom and a fall, with the costly foragers losing less in the fall on two seeds. Whether that is a lasting
share or noise in small numbers is for the 24,000 s look.

## 24,000 s (read at 01:25-01:40 on 2026-11-06)

**Who the animals descend from** (`pool-origins.py`, every 500 s from 18,000 s):

| Seed | Fewest from 18,000 s | Most | At 24,000 s | the 40-generation exam's | the shaped arm's |
|---|---|---|---|---|---|
| 6201 | 918 (19,000 s) | 2,363 (23,000 s) | 2,202 | 1,683 | 519 |
| 6202 | 165 (18,000 s) | 1,134 (22,000 s) | 1,055 | 551 | 504 |
| 6203 | 540 (19,000 s) | 1,422 (23,000 s) | 1,196 | 1,166 | 30 |
| 6204 | 153 (18,000 s) | 616 (22,000 s) | 519 | 462 | 57 |

**The shape family** (`scripts/reads/family-census.py`), living jointed bodies whose genome carries a superellipsoid
node, and the same among jointed animals:

| Seed | Jointed bodies | with a superellipsoid | Jointed animals | with a superellipsoid |
|---|---|---|---|---|
| 6201 | 3,901 | 45.5 % | 1,782 | 9.0 % |
| 6202 | 885 | 56.6 % | 844 | 59.4 % |
| 6203 | 1,150 | 15.3 % | 969 | 3.3 % |
| 6204 | 560 | 15.2 % | 365 | 1.4 % |

**What evolved** (`what-evolved.py --json` on the resumed runs; a resumed run roots its lineages at its first body,
so its founders are the pool and trickle founders that arrived from 7,500 s on, and the bodies it took over then):

| Seed | Traits shifted 1 founder SD or more | Living animals in a plan no founder had |
|---|---|---|
| 6201 | adult scale, volume, attack, intake, hidden units | 1,883 of 2,202 (86 %) |
| 6202 | lifespan scale, reserve margin | 829 of 1,055 (79 %) |
| 6203 | adult scale, volume, hidden units, lifespan scale, brood | 652 of 1,196 (55 %) |
| 6204 | adult scale | 312 of 519 (60 %) |

- **Readout 1 holds in all four worlds** through 24,000 s, at 500 s samples.
- **Readout 4: the family holds above 10 % of jointed bodies in all four worlds. My prediction 4, that it would stay
  under 10 % in all four, fails.** No founder carried it, so it entered by mutation in the worlds and spread. On 6202
  it is in 59 % of jointed animals.
- **Readouts 5 and 6 hold in all four worlds**, on the resumed runs' founders.
- **The shaped arm's lines overtook the thrifty ones on 6202** from about 19,000 s to 23,000 s, 605 to 331 at
  21,000 s, and stand level at 24,000 s. They hold a quarter of 6201's animals, and have nearly gone from 6203.
- **Pace and heat at 01:22.** 0.72 times real time on 6201, the most crowded, and 1.1 to 1.5 on the others. CPU 70 °C,
  card 50 °C.

Inference: on 6202 the shaped arm's lines and the family's animals are about as many (504 and 501), so the family may
have spread inside those lines. Whether it did is a lineage read for the morning. `what-evolved.py`'s text report
fails on a resumed run, at a lineage whose root's birth lies in the earlier run; its JSON is whole. Owed: the fix, and
a read that chains the two runs for readouts 5 and 6 against the first floor's founders.

## 26,590 s: seed 6201 stopped under half real time (02:34 on 2026-11-06)

Seed 6201 ran at 0.49 times real time at 02:33 (the group's pace line, then `pace-check.py`), and was stopped with
`scripts/stop-arm.ps1 r67r1-6201 -Reason manual-other`, as the pre-registration and the owner's rule of 2026-10-29
say. It stopped at 26,590 s with a checkpoint at that second (`checkpoints/000026590.ckpt`). The round has no
exchange between worlds, so the other three ran on.

The reason is the crowd. The world held 19,874 living bodies at the stop, against 10,936 at 24,000 s; 4,611 of them
jointed and 15,263 rigid, and 24 bodies a step on the bed or the glass. Its pace fell with the count: 1.5 times real
time near 15,000 s, 0.86 at 21,000 s, 0.72 at 24,000 s and 0.49 at 26,500 s. The other three held 10,800 to 13,744
bodies at the same hour, at 0.77 to 1.0 times real time. Whether 6201 resumes, and on what, is the owner's.

## 30,000 s, the three worlds that run on (read at 02:36 on 2026-11-06)

| Seed | Living animals at 30,000 s | the 40-generation exam's | the shaped arm's | Jointed bodies with a superellipsoid | Jointed animals with one |
|---|---|---|---|---|---|
| 6202 | 1,324 | 328 | 996 | 30.3 % of 1,111 | 32.2 % of 1,045 |
| 6203 | 1,445 | 1,433 | 12 | 11.4 % of 1,212 | 8.5 % of 1,156 |
| 6204 | 984 | 945 | 39 | 13.6 % of 971 | 2.0 % of 767 |

- **On 6202 the shaped arm's lines now hold three animals in four.** On the other two they have nearly gone.
- **The family holds above 10 % of jointed bodies in all three**, but fell on 6202 from 56.6 % to 30.3 % while the
  shaped arm's lines grew. So the family did not spread with those lines, and the inference at 24,000 s is not borne
  out.

## 36,000 s (read at 04:24-04:30 on 2026-11-06)

Living animals every 1,000 s from 30,000 s, split by exam (`pool-origins.py`, the two runs chained), and the family
at 36,000 s (`family-census.py`):

| Seed | Fewest from 30,000 s | Most | At 36,000 s | the 40-generation exam's | the shaped arm's | Jointed bodies with a superellipsoid | Jointed animals with one |
|---|---|---|---|---|---|---|---|
| 6202 | 1,324 (30,000 s) | 2,814 (34,000 s) | 2,500 | 810 | 1,690 | 33.1 % of 2,072 | 34.5 % of 1,980 |
| 6203 | 1,445 (30,000 s) | 2,842 (34,000 s) | 2,184 | 2,175 | 9 | 16.8 % of 1,868 | 11.6 % of 1,748 |
| 6204 | 984 (30,000 s) | 2,316 (34,000 s) | 1,774 | 1,715 | 59 | 10.2 % of 1,643 | 4.4 % of 1,421 |

- **Readout 1 holds in all three worlds**, at 1,000 s samples.
- **A second boom and fall.** The animals doubled from 30,000 s to 34,000 s in every world and have been falling since.
- **On 6202 the shaped arm's lines kept growing through the fall**, from 996 to 1,690, while the thrifty lines rose to
  1,338 at 33,000 s and fell to 810. They hold two animals in three. On 6203 they are down to 9, and on 6204 to 59.
- **The family holds above 10 % of jointed bodies in all three**, and stands nearest the line on 6204, at 10.2 %.
  Among jointed animals it rose on 6203 and 6204, from 8.5 to 11.6 % and from 2.0 to 4.4 %.
- **Pace, heat and disk.** At 04:24 the worlds held 18,063 bodies (6202, at 38,340 s), 13,653 (6203, at 36,160 s) and
  20,162 (6204, at 38,320 s). They ran at 0.63, 0.90 and 0.60 times real time (`pace-check.py`). The CPU's highest since
  the 30,000 s look was 78 °C at 03:49, with no reading at 85 °C, and the card's 53 °C. The round holds about 9.3 GB,
  with 387 GB free.

Inference: the shaped arm's lines on 6202 lose less in each fall, as they did at 18,000 s. Two worlds of three run
near half real time with a crowd like 6201's at its stop, so they may reach the line before 42,000 s.

## Readouts 5 and 6 against the first run's founders (read at 04:30 on 2026-11-06)

`scripts/reads/chain-run.py` joins each world's first run, to its resume at 7,500 s, and its resumed run into one
folder, and `what-evolved.py --at 24000` reads the joined world. Its founders then include the 27 to 36 floor founders
of 0 s, and the pool and trickle founders that came before 7,500 s. That makes 741 to 793 founder bodies a world,
against 522 to 573 in the resumed run alone. No birth id appears in both runs.

| Seed | Traits shifted 1 founder SD or more | Living animals in a plan no founder had |
|---|---|---|
| 6201 | adult scale, volume, intake, hidden units | 85.0 % |
| 6202 | lifespan scale, reserve margin | 78.6 % |
| 6203 | adult scale, volume, hidden units, lifespan scale, brood | 54.3 % |
| 6204 | adult scale | 59.9 % |

- **Readouts 5 and 6 hold in all four worlds on the joined read**, as they did on the resumed runs. The one change is on
  6201, where the attack sum falls under one founder standard deviation. The shares of new plans move by half a point
  or less.

## The family against the exams' lines (read at 04:35 on 2026-11-06)

`scripts/reads/family-by-origin.py`, the two runs chained, splits the living jointed animals by their founder's exam and
by whether their genome carries a superellipsoid node:

| Seed, second | 40-generation exam's lines: animals, with the family | Shaped arm's lines: animals, with the family | Lineages holding the family |
|---|---|---|---|
| 6202, 24,000 s | 449, 90.4 % | 395, 24.1 % | 3 |
| 6202, 30,000 s | 253, 94.5 % | 792, 12.2 % | 2 |
| 6202, 36,000 s | 638, 82.3 % | 1,342, 11.8 % | 3 |
| 6203, 36,000 s | 1,744, 11.6 % | 4, none | 7 |
| 6204, 36,000 s | 1,379, 4.6 % | 42, none | 9 |

- **On 6202 the family is a thrifty line's**, and nearly all of it sits in one lineage of the 40-generation exam's, 406
  animals of 501 at 24,000 s. In the shaped arm's lines it is one lineage too, 94 animals then and 158 at 36,000 s.
- **The family's fall on 6202 has two parts of about equal size.** Among jointed animals it fell from 59.4 % at
  24,000 s to 34.5 % at 36,000 s. Had each exam's lines kept their 24,000 s share of it, the shift between the lines
  alone would have left it at 45.5 %. The rest is a fall inside the lines, from 90 to 82 % in the thrifty ones and from
  24 to 12 % in the shaped arm's, where lines without it grew faster. The family did not spread with the shaped arm's
  lines, which settles the inference of 24,000 s against itself.
- On 6203 and 6204 the family is in the thrifty lines only, spread over 7 and 9 lineages.

## 38,486 s: seed 6203 ran away (05:23 on 2026-11-06)

Seed 6203 ended by itself at 38,486 s, holding 25,010 living bodies against the config's `maximumPopulation` of 25,000.
The farm ends such a world as a runaway, and D021 and D058 file it as censored, never a pass. The pre-registration names only two ways a world stops, empty or under
half real time, and did not foresee the ceiling. Nobody stopped the world, and the other two ran on.

Plants had filled the world: at 38,480 s it held 24,974 bodies, 3,160 of them jointed, with 414,186 J of standing tissue. Its pace
had fallen to 0.55 times real time with the crowd. Its last checkpoint is at 37,500 s, and a resume from there replays
to the same runaway, so the world goes on only with the ceiling changed. That is a change to the world, and the
owner's.

At 38,000 s, its last look: 3,670 living animals, 3,658 of them in the 40-generation exam's lines and 12 in the shaped
arm's. The family is in 22.5 % of 3,224 jointed bodies and 18.7 % of 3,062 jointed animals, all in the thrifty lines,
534 of them in one lineage.

## 42,000 s, the two worlds that run on (read at 06:05-06:10 on 2026-11-06)

| Seed | Fewest from 36,000 s | Most | At 42,000 s | the 40-generation exam's | the shaped arm's | Jointed bodies with a superellipsoid | Jointed animals with one |
|---|---|---|---|---|---|---|---|
| 6202 | 2,500 (36,000 s) | 4,896 (41,000 s) | 4,791 | 2,242 | 2,549 | 43.3 % of 3,879 | 44.0 % of 3,812 |
| 6204 | 1,774 (36,000 s) | 4,189 (39,000 s) | 3,281 | 3,199 | 82 | 11.3 % of 2,824 | 7.5 % of 2,692 |

- **Readout 1 holds in both**, at 1,000 s samples.
- **On 6202 the thrifty lines came back**, from 810 animals at 36,000 s to 2,242, and the two exams' lines stand nearly
  level. The shaped arm's lines peaked at 3,005 at 39,000 s.
- **The family rose on 6202 with its line**, from 33.1 % to 43.3 % of jointed bodies. Of the thrifty lines' jointed
  animals 81 % carry it, 1,437 of them in the one lineage, and of the shaped arm's 10 %.
- **On 6204 the family holds just above the line**, 11.3 % of jointed bodies, all in the thrifty lines.
- **Pace, heat and disk.** At 06:07 the worlds ran at 0.65 (6202, 19,870 bodies at 42,430 s) and 0.71 times real time
  (6204, 21,436 bodies at 42,640 s). Both ran faster once 6203 had ended. The CPU's highest since the 36,000 s look was 79 °C, with
  no reading at 85 °C, and the card's 53 °C. The round holds about 11 GB, with 385 GB free.

Inference: both worlds climb toward the ceiling that ended 6203, 6202 at 5,130 bodies short of it and 6204 at 3,564,
and may end the same way.
