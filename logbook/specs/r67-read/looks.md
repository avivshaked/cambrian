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
