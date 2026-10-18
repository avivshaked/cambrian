# Round 57's predictions: winters and weather

*2026-10-06, written after round 57's one-year screen and before its three seeds, and committed before
they start. It rests on the owner's rulings of 2026-10-05 and 2026-10-06 (D151, D152 and D153,
`proposals/propose-r57-weather.md`). It also rests on the screen, and on a short literature round on
bet-hedging (`logbook/specs/r57-lit/`). Every number is computed, measured or guessed, and each says which.*

## What it asks

Round 56 gave the sun a day and a year, and the year bit less than predicted. The living fell 7% to 22%
from the first half of a year to its second, against the 20% asked for in both years. The crowd came
out larger than round 55's in every seed. Its light was also exact. Every day of a season was the same
day, so a fixed trait could fit it, such as a reserve just large enough for a night of known length.
Round 57 makes the winter deeper and the days unequal. It asks whether the winters bite every year (W)
and whether the leaves come to keep more in reserve (R). It also asks whether they come to breed as
summer returns (T), and whether their margins come to agree (V).

## The world

Round 56's control seed (seed 1, no nursery genomes), with a new light, in `rounds/env-r57.ps1` on
`round-57`:

- **A year of 6,000 s in two seasons**: three days of summer at 1.6 of the mean light, then three days
  of winter at 0.4. Each switch takes 250 s and is centred on a midnight. Summer runs from −250 to
  2,750 s of each year and winter from 2,750 to 5,750 s. The light over a year averages to the mean.
- **A day of 1,000 s at amplitude 0.5**, as in round 56.
- **Weather**: each day's light is multiplied by a factor drawn once a day, log-normal with mean 1 and
  a log standard deviation of 0.3. Each day's logarithm is correlated 0.5 with the day before, and the
  factor blends across each midnight. The sky is drawn from its own seed, 57, so the three seeds share
  one sky and differ only in their life.
- **No inoculation.** All three seeds are alike: the trickle refounds from round 55's control pool at
  a share of 0.1 and the floor's pool share is 0.
- **The water's current** has a period of 6,000 s. It had that period before this round, and now the
  year matches it. So each year the same phase of the current meets the same season, and a trait could
  fit the pair, though no clause reads it.
- **No light sense.** A body knows the season only as its reserve falling or rising.

Three seeds, each to 48,000 s: eight years and eight winters a seed. A leaf lives about 1,250 s and
first breeds at about 800 s (measured on round 55). So a generation is about 1,000 s, and a seed runs
about fifty.

The cycle keeps the mean light and not the mean income. A leaf earns the lesser of its light and its
matter uptake, so summer's surplus is wasted on a leaf bound by uptake while winter still starves it.
The leaf-budget model (`logbook/specs/r57-build/budget57.py`) puts a typical leaf's income at 0.86 of a
world without cycles (computed). A light-bound leaf needs a reserve of about 630 s for a median winter
and 1,250 s for a bad one (computed). An adult at its breeding gate holds about 800 to 1,300 s and a
newborn about 42 s (measured on rounds 54 and 55). So a winter should kill the young and spare the full.

## The screen

Seed 2 ran one year, 6,000 s, on the round's build (`r57-screen-s2`, configHash `dfc0e881`), before
anything else was written down. It ended on its budget in 62 minutes of the farm, with 2,145 living (measured, as is everything in
this section).

- The founders' burst came first. The living peaked at 5,159 at 700 s. By 2,500 s, in full summer,
  the founding crowd had thinned to 2,907.
- The winter halved the crowd. From 2,554 at the switch it fell to 1,311 at 3,920 s, early on the
  second winter day.
- The tank refilled before the summer came. It was back to about 1,840 by 5,750 s, still in winter.
  My guess is that the survivors are leaves bound by their matter uptake, which the winter spares, and
  that they took the matter and light of the dead. That is not read.
- The light factor ran from 0.18 to 2.69, the season, the day and the weather together.
- The leaves born of a parent numbered 10,089 in the year, with a median margin of 100 s and an
  interquartile range of 380 s. At 6,000 s the largest root lineage held 51% of the living and the
  jointed bodies 7.6%.
- Of the summer's leaf births, 62% fell in its first day. In year 1 that day is the founders' first
  750 s and says nothing about the leaves' timing.

Because the screen showed seed 2's first year, every clause reads years 2 to 8, and the bars below
were set after seeing it. Year 1 is a poor guide to the rest in any case: its summer's most is the
founders' burst, and its first summer day is the founding. The full seed 2 runs the same build and
settings, so it should replay the screen's first 6,000 s to the bit; that is checked as a reading.

## The launch

Seeds 1 and 2 start together at 8 threads each, pinned to logical processors 16 to 23 and 24 to 31,
within the owner's limit of half the machine. Seed 3 starts when the first of them ends. A farm run is
the same at any thread count. The owner ruled on 2026-10-06 that the round may launch once this entry is
committed, without waiting for their approval. They also ruled that it runs as built: the speed-ups
that change the world wait for round 58 (D153).

## Predictions

| # | prediction | falsified by |
|---|---|---|
| W | **the winters bite every year**: in at least 6 of years 2 to 8, the fewest living in the winter at least 20% below the most living in the summer before it, in 3 of 3 seeds. The bar is round 56's S1, so the two rounds read alike. The screen's first winter took 49% from the switch to its trough (above); the question is whether it still does once the leaves have had seven winters to adapt and the survivors have the matter and light of the dead. I guess this holds | stats.jsonl |
| R | **the leaves come to keep more**: the median breeding margin (`rm`) of the leaves born of a parent in year 8 above that of those born in year 2, in 3 of 3 seeds. Chance alone passes this one time in eight. Round 56's S3 asked the same against round 55 and held on one seed of three, so a year that bites harder is the bet here. I guess this holds, weakly | lineage.jsonl's birth rows |
| T | **the leaves come to breed as summer returns**: the share of a summer's leaf births (born of a parent) falling in its first day higher in years 6 to 8 together than in years 2 to 4 together, in 3 of 3 seeds. A crowd that grows through each summer puts most births late in it whatever the leaves do, so the clause compares the round with itself rather than with a third. I guess this fails | the same |
| V | **the margins come to agree**: the interquartile range of `rm` among the leaves born of a parent in year 8 below that in year 2, in 3 of 3 seeds. The trickle keeps adding founders from the pool, and their children carry the pool's spread, which works against this. I guess this holds | the same |
| B1 | **the books balance**: the audit under 0.1 J and the matter residual under 1e-5 units at every sample, nothing diverges, and every seed ends on its budget, in 3 of 3, read by round 54's reader | the stats rows, run.json |

## What the literature says

None of the ten papers in the literature round modelled a world like this one. What follows maps their
results onto it, and the mapping is mine.

- R rests on storage theory. In Fischer, Dieckmann and Taborsky (2011), storing pays only once the
  environment varies enough and predictably enough. This round's weather varies by about 31% from day
  to day and forgets itself in about 1.4 days (computed from the settings). Both sit at or below the low
  end of their range, so the deterministic fourfold swing between the seasons is the stronger push.
- T rests on Varpe and co-authors (2009, read in abstract). A store paid most by letting a body breed
  before the good season, when an egg is worth most. Here a child born on the first day of summer has
  three bright days ahead, and one born on the first day of winter has three dull ones.
- V rests on Park and Wootton (2021). The variance of life-history traits fell in every regime they
  tried, slowest under long cycles, and the cycle's period set it more than its randomness did. This
  round's year is about six generations, outside their range.
- The weather's runs of dull days pull two ways, and no clause asks about them. Metcalf and Koons
  (2007) found that a redder environment, one whose bad spells last, always favoured spending more on
  survival. Wieczynski and co-authors (2018, read in abstract) found tolerance disfavoured in redder
  environments. Telling them apart would need a second arm with uncorrelated weather.
- Fifty generations may reach no equilibrium. Park and Wootton's variance was still falling after a
  dozen generations, and Botero and co-authors (2015) ran fifty thousand. A clause that fails here may
  be one that has not had time, and the readings per year show which way each was going.

## The readings

- Per year and seed, the living: the most in summer and the fewest in winter, with the light factor's
  range.
- Per year and seed, the leaf births of a parent: their median and interquartile `rm`, and the share
  of summer births in the first summer day.
- At the end of each year, the largest root lineage's share of the living, against round 56's 99.8% to
  99.9% at 30,000 s, and the jointed share.
- Seed 2 against the screen to 6,000 s, row for row (`scripts/compare-det.py`).
- The pictures: the theatre at a summer's peak and a winter's trough of seed 2 in year 8.

## What the round does not ask

Whether the weather matters apart from the seasons: the two arrive together, and a second arm without
weather would cost another day of the farm. Whether the reserve rose against round 56: round 56's year
has another shape, so each seed is read against itself across its years. Whether the bodies learn the
time: they have no sense to learn it with, and the light sense waits for round 58.

## The reader

`scripts/reads/r57-read.py` reads W, R, T and V per seed, with a held line for each, and with
`--screen <arm>` it reads one arm's years alone, without clauses. It leaves out every founder the
trickle, the floor or the pool places, since their margin is the pool's and not one the world selected.
B1 is read by `scripts/reads/r54-read.py`.

## Before the launch

The pre-registration's record is the commit that adds this entry, on a clean tree on `round-57`. Every
seed's manifest names it as `gitCommit`, with no DIRTY mark. The build is the branch `round-57` (its
build `e522b5f`, its launcher `e830d38`), launched from `scratch/wt-r57` into the main tree's runs, on the
CPU at 8 threads a seed. Its fixtures were re-recorded for the five new tunables and every suite passes
(Core 1,137 with the slow tests, Dynamics 174, Farm 208). The checkpoint check passes on the screen's
1,000 s checkpoint (`logbook/specs/r57-build/verify-checkpoint-screen-1000.log`), so a seed may be
stopped and resumed.
