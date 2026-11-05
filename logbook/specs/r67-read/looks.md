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
