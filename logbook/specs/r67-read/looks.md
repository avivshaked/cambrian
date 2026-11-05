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
