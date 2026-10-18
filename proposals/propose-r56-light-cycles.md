# Round 56: day and night, and seasons

*Proposal for the owner, 2026-10-04. Draft. Nothing here is built or launched before the owner rules on
the questions at the end.*

## What the owner has already decided

On 2026-10-04 the owner ruled that round 56 is light cycles, day and night and seasons, on round 55's
world, and left round 57 open (HANDOFF item 1b). This proposal sets out what exists, what is missing,
what the leaves can bear, and the questions still open. The groundwork's tables and scripts are in
`scratch/r56-light/`.

## What exists

The day and night cycle is built and has never run on the farm (D035). `LightModel` carries two dials,
an amplitude and a day length, and every farm report so far reads `day ±0 over 200 s`. The light at time
t is the mean times `1 + A·sin(2πt/P)`. That is a smooth wave around the mean, never clipped, so at the
largest amplitude the allowed (1) the water touches darkness for an instant and is otherwise lit.

The cycle reaches everything that reads light: photosynthesis, founder placement, the shade, the reef
caps. The sun stays overhead; nothing models its angle. The stats rows already carry the factor
(`dayFactor`) and the report has a `sun` column.

Seasons are not built.

## Three things the groundwork found

**The cycle preserves the mean light, not the mean income.** D035 was written before D098, and it
promises that the cycle "does not move the budget by one joule". Under D098 a leaf's income is the
lesser of its light and its matter uptake. A leaf bound by uptake cannot use a bright noon and still
suffers a dark night, so for it the cycle is a cut in income. In rounds 54 and 55 that was 63 to 81% of
leaf body-steps (the `upt lim` column, measured). For an illustrative uptake-bound leaf, the mean income
falls to 0.89 of today's at amplitude 0.8 and 0.78 at amplitude 1. That uses a light capacity of twice
the uptake, which is a guess. This is how a real night works, and the proposal treats it as the world
rather than as a fault. It does mean the round tests a poorer world as well as a varying one, and D035
and DESIGN §5A.4 need a correcting line whichever way the owner rules.

**No creature can see the light or tell the time.** The light sense (`SensorChannel.Photo`) was never
built. A body can feel night only as its reserve running down, through its energy sense. So round 56
selects for bodies and life histories that survive the dark: reserves, growth schedules, timing of
birth. It cannot select for a brain that does something different at night, and vertical migration
cannot evolve on purpose.

**Seasons are a small build with a long tail.** Two new dials (a season amplitude and length) multiply
the day's wave by a slower one. With both on, the mean holds only if the season is a whole number of
days long, so the build refuses any other combination. A season off replays today's world to the bit.
But adding a dial changes every `configHash` and refuses every earlier `config.json`, as every new
tunable does. That means re-recording the usual fixtures and pins, and also migrating the nursery's
config (`rounds/nursery/configs/7319d6b528cc77f1.json`) and the four `brain2*` recipes that pin it.
That last part is a8's ground and goes to a8 with the change. My estimate is half a day of agent work
and about two hours of machine for the fixtures and suites.

## What the leaves can bear

Each setting can be read against the typical leaf of rounds 54 and 55. Its median lifetime is about 1,250
s. A grown body holds 360 to 1,330 s of its own upkeep in reserve. A growing body holds about 16 s and a
newborn about 42 s (measured and computed in `scratch/r56-light/leaf-budget.tsv`). The world pays a
typical leaf about 2.1 times its spend; a marginal one, 1.25 times.

| Cycle | What it does to a typical leaf | Status |
|---|---|---|
| Day 200 s | Nights far shorter than a life. Juveniles die in a deep trough; grown bodies don't notice | computed |
| Day 1,000 s, amplitude 0.8 | 30 nights a run, each 0.4 of a lifetime. A grown body needs about 100 s of reserve of its 360 or more; growing bodies and newborns caught in a trough of about 270 s die | computed |
| Day 3,000 s or more | A generation lives inside one phase; the cycle acts as a season | computed |
| Season 10,000 s, amplitude 0.5 | Three years a run, the lean half four lifetimes long. The typical leaf still nets positive in the trough; the marginal one is pruned each winter | computed |
| Season 10,000 s, amplitude 0.8 | No typical leaf breeds in the trough. A crash each winter to the uptake-bound and best-placed leaves, and the trickle refounds the rest | computed |

These numbers hold a leaf's ratio of income to spend fixed. In the world a die-back frees matter and
shade, and the survivors' ratio rises. My inference is that the tables overstate the harm, and that only
a run in the world gives the real answer.

A 30,000 s season would end the run in its first winter, so a season of 10,000 s is the longest that
shows a year turning.

## What the literature holds

The review holds one relevant source, [HG25]. It found smaller networks evolving under energy costs in
more seasonal environments, but in a task-driven reinforcement-learning setting that the review marks as
not an ecology. Nothing in it addresses day and night or seasons of supply as a cause of
persistence or diversity. DESIGN §5A.4 records the one early test of the cycle (logbook/0020). At full
amplitude, deaths roughly tripled and the population fell about 40%, in a world much brighter than
today's. A short literature round on light cycles in ecology could precede round 57; it is not needed for
round 56.

## The questions

### Q1. The cycle's settings

- **A (recommended): days of 1,000 s and seasons of 10,000 s, both at amplitude 0.5.** That gives three
  years a run and ten days a season, the whole-number ratio the build needs. Winter prunes marginal leaves and the
  young caught at night, and spares the typical adult, so lines can carry through.
  Implications: needs the season build. The risk is a mild effect that reads as no effect, if most
  leaves stay uptake-bound.
- **B: the same, with the day's amplitude at 0.8.** Deeper nights, about −11% income for an uptake-bound
  leaf, and juvenile deaths in every trough. Implications: the same build; a stronger signal and a
  higher risk of collapse in the first winter.
- **C: nights only, day 1,000 s at amplitude 0.8, no seasons.** Implications: no build, so it could
  launch as soon as round 55 ends. It is not what was ruled (seasons were part of it), and it changes no
  hash, so seasons would arrive later as their own round.

### Q2. A screen before the round

Recommended: one 10,000 s screen of the chosen setting on round 55's
world, seed 2, which is one year. It shows whether the first winter collapses before three seeds spend a
day on it. Implications: about four hours of machine at 8 threads, which fits beside round 55's seed 1
once seeds 2 and 3 have ended. If it collapses I come back with the numbers instead of launching.

### Q3. The light sense

Recommended: leave it out of round 56. Building it changes the brain's inputs,
and the nursery's brains and round 55's inoculants were wired for today's thirteen channels. Without it,
round 56 asks whether the dark changes life histories, which is a clean question. A light sense is a
candidate for round 57 or after, if round 56 shows the cycle matters.
Implications: vertical migration and any night behaviour of the brain stay out of reach in this round.

### Q4. The seeds and their baselines

Recommended: round 55's three seeds in the same roles, seed 1
uninoculated and seeds 2 and 3 inoculated, with the cycle on in all three. With the season build at zero
the world replays round 55, so each seed's baseline is round 55's same seed, and the round needs no
extra control. Implications: whether the nursery's lines go into round 56 follows round 55's result. If
round 55 shows they don't hold, the owner can drop the inoculation here, and then only seed 1 keeps its
baseline.

## The timeline, if ruled as recommended

- Tonight: the season build and its tests on a branch, at a low thread count while round 55 runs. The
  fixtures are re-recorded once a round-55 seed frees its slot.
- Round 55's seeds 2 and 3 end, about 18:00 or later: the screen starts beside seed 1.
- About four hours later: the screen's read, then the pre-registration for the owner's approval.
- Round 56 launches once round 55's seed 1 ends and the pre-registration is committed.
