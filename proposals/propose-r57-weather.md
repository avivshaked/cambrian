# Round 57: two seasons, and weather

*Design for the owner, 2026-10-05. The owner approved it in conversation that evening; the pre-registration
and the screen still come to the owner before launch.*

## What the owner has decided

On the evening of 2026-10-05 the owner proposed deeper winters and light that varies from day to day, and
then, after the coordinator's groundwork, ruled the design below. The coordinator recommended it in
the version given here.

- **Two seasons, not a smooth year.** A year of 6,000 s is three days of summer and then three days of
  winter, each at its own level. The owner asked first for a 5,000 s year. Halves of 2.5 days would
  switch season in the middle of a day and leave summer and winter with unequal light, so the year is
  6,000 s.
- **Winter at 0.4 of the mean light and summer at 1.6** (a season depth of 0.6). The light over a year
  is unchanged.
- **Weather.** Each day's light is the season's times a factor drawn from a bell curve around 1, with
  dull days tending to come in runs.
- **No inoculation.** All three seeds are alike.
- **Three seeds, each run to 48,000 s**, eight years and eight winters each.
- **The light sense moves to round 58.**

## Why

**An exact sun.** Round 56's light was a pure function of the clock, so every day of a season was the
same day and every year the same year. No creature in round 56 can sense light, so none can plan.
But selection can still fit a fixed trait to an exact schedule, such as a reserve just large enough for
a night of known length. Days that vary reward a margin instead. Ecology calls this bet-hedging. The
review holds no source on it yet, so a short literature round grounds the pre-registration's
predictions.

**A mild winter.** In round 56, S1 failed in every seed: the population fell 7% to 22% from a year's
first half to its second, against the 20% asked for. The crowd was larger than round 55's in every
seed.

**Few years.** Round 56 gave three years a run. A typical leaf lives about 1,250 s and first breeds at
about 800 s, so a generation is about 1,000 s: ten a year and about thirty a run. That is enough to
sort the variation a population has and shift continuous traits such as reserve size and the timing of
breeding. It is not enough for much that is new, and three winters give a trend across winters only
three points. The two-season year gives eight winters a seed and twenty-four across the round, about
fifty generations a seed, for 1.2 days of the farm.

**Inoculation.** Round 55's nursery-rooted bodies were 4.0% and 0.1% of the living against the 5%
predicted, and almost every jointed nursery-rooted body alive at its end was a founder or nearly one.
Round 56's jointed share was 5.3%, 0.33% and 0.05%. The inoculation adds a difference between seeds that
has nothing to do with light, and over a long run its stream of founders that never met a winter
works against adapting to one. The trickle stays on as the safety net that refounds after a crash,
with the control's pool, round 54's four stomachs.

## The build

**Seasons.** `LightModel` gains a season shape, smooth (round 56's sinusoid) or two-season (a square
wave), and a ramp: the seconds over which the light moves from one season's level to the other, with a
smooth step and no instant switch. With the two-season shape and a day cycle on, the build refuses a
year that is not an even number of days, so that each half holds whole days and the light over each
season keeps its mean. The smooth shape at amplitude 0.5 and a 10,000 s year replays round 56's light
exactly.

**Weather**, in the same class and on its terms:

- One factor a day, log-normal with mean 1, so the expected light is unchanged and no day goes
  negative.
- Each day's logarithm follows the last with a correlation, so dull days come in spells. A spell is
  what tests a reserve.
- The factor blends smoothly from one day to the next.
- The series is drawn from its own seed, a tunable, and not from the run's. Every seed of the round has
  the same sky, so the seeds differ in their life and not in their weather.
- The factor is a pure function of elapsed seconds and the config, like the rest of the light. A
  checkpoint carries nothing new and a resume stays exact.
- A spread of 0 is the world without weather. The build refuses weather without a day cycle.

**Five new tunables**: the season shape, the ramp, the weather's spread, its day-to-day correlation and
its seed. As every new tunable does, they change every `configHash` and refuse every earlier
`config.json`. The fixtures and pins are re-recorded, and the nursery's configs and recipes are
migrated, which goes to a8 with the change. The estimate is half a day of agent work and about two hours
of machine for the fixtures and the suites.

**The settings for the round**: shape two-season, year 6,000 s, depth 0.6, ramp 250 s; day 1,000 s at
amplitude 0.5 as in round 56; weather spread 0.3 (log standard deviation) and correlation 0.5; every
other setting round 56's control seed's.

## What the leaves can bear

This uses round 56's leaf-budget model (`logbook/specs/r56-light/budget.py`), with the two-season year and
weather added in `logbook/specs/r57-build/budget57.py`, over 200 drawn years a setting. A body's net is its
income ratio times the light it can use, less its upkeep. The reserve it needs is the deepest fall of
that net over a year, in seconds of its own upkeep.

What a body holds, measured in rounds 54 and 55:

| Body | Reserve |
|---|---|
| Growing body | about 16 s |
| Newborn | about 42 s |
| Adult just after breeding | 360 to 440 s |
| Adult at its breeding gate | about 800 to 1,300 s |

All rows below are computed, with a day of 1,000 s at amplitude 0.5, and give the median year and a
bad year (the 90th percentile).

| Year | Depth | Weather spread | Typical light-bound leaf | Uptake-bound leaf | Its income against no cycle |
|---|---|---|---|---|---|
| Smooth, 10,000 s (round 56) | 0.5 | none | 138 s | 0 s | 0.96 |
| Smooth, 10,000 s | 0.7 | 0.3 | 705 / 1,243 s | 93 / 219 s | 0.90 |
| Two seasons, 6,000 s | 0.5 | 0.3 | 303 / 789 s | 17 / 89 s | 0.91 |
| **Two seasons, 6,000 s** | **0.6** | **0.3** | **627 / 1,246 s** | **70 / 175 s** | **0.86** |
| Two seasons, 6,000 s | 0.7 | 0.3 | 1,202 / 1,590 s | 145 / 405 s | 0.80 |

How to read it:

- **At depth 0.6, a typical light-bound leaf needs about 630 s of reserve in a median winter, and 1,250 s
  in a bad one.** An adult near its gate carries the first; a newborn or a growing body caught in winter
  does not; in a bad year only the fullest adults come through. That is as hard as a smooth year at
  0.7.
- **At depth 0.7 a light-bound leaf earns nothing in winter even at noon** (0.3 of the light times the
  day's peak of 1.5, against an income ratio of 2.1, stays under its upkeep). That is a famine every
  winter.
- **The uptake-bound leaves are safe at every setting.** They were 63 to 81% of leaf body-steps in
  rounds 54 and 55, so a total collapse is unlikely. The same mean light pays them less, down to 0.86 of
  a world without cycles, because summer's surplus is wasted for the whole summer. So the round tests a
  poorer world as well as a harsher one, and it reads income, not light.

The model holds a leaf's income ratio fixed. In the world a die-back frees matter and shade, and the
survivors earn more; round 56's crowd grew where the same model expected pruning. My inference is that
the table overstates the harm.

## What the round can and cannot separate

The two-season year, the depth and the weather arrive together, so a result belongs to harsher, less
predictable light as a whole. Round 56 is no direct baseline: its year has another shape. Each seed is
compared with itself across its eight winters, and the three seeds with each other. A second arm, the
same year without weather, would separate the weather and costs another day of the farm.

## Still to rule

1. **The screen.** One year, 6,000 s on seed 2, about 1.5 hours of the farm, before the three seeds
   spend 1.2 days. If the first winter collapses, the coordinator comes back with its numbers instead of
   launching.
2. **The pre-registration**, with its predictions and how each is read, committed before launch.

## The timeline

- **2026-10-06, morning:** the build on branch `round-57`, its tests, the fixtures re-recorded. The
  nursery's migration goes to a8. A short literature round on bet-hedging and varying light beside it.
- **2026-10-06, afternoon:** the screen. The round takes the farm ahead of the speed trials.
- **2026-10-06, evening:** the screen's read and the pre-registration, for the owner.
- **2026-10-07:** round 57 launches, three seeds, two at a time and then the third, about 30 hours.
