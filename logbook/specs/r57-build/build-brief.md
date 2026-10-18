# Round 57's build: the brief

*The brief for the agent that builds round 57's light and the scent restore, on branch `round-57` in
the worktree `scratch/wt-r57`. The design is D151 (`DECISIONS.md`) and
`proposals/propose-r57-weather.md`; the restore is `logbook/specs/r57-build/scent-restore.md`.*

## Where you work

- Edit only in `D:\Projects\experiments\evolution-simulator\scratch\wt-r57\`. Reach it by absolute
  path and `git -C`, and never `cd` into it.
- Put notes, logs and anything you make for yourself in
  `D:\Projects\experiments\evolution-simulator\scratch\r57-build\`. Helper scripts and lists go in
  `D:\Projects\experiments\evolution-simulator\scratch\r57-build\helpers\`.
- Never write to your session scratchpad, to TEMP (`%TEMP%`, `C:\Users\...\AppData\Local\Temp`) or
  anywhere else outside the project.
- Use at most 8 logical processors. Build with `-m:1`. Run only the fast filtered tests
  (`scripts/core-test.ps1 -Filter ...`, and the Farm tests by filter). Never run a full suite, a farm
  run or a smoke: the caller runs those. Your task ends when the edits and the filtered tests are in.
- Commit nothing. Report the files changed and the filtered tests run, with their counts.

## What to build, in `LightModel` (`src/Evosim.Core/Environment/LightModel.cs`)

Five new `[Tunable("light")]` properties, validated on the way in as the existing ones are:

1. **`SeasonShape`**, an enum `Smooth` or `TwoSeason`, default `Smooth`. It serializes by name.
2. **`SeasonRampSeconds`**, default 0. How long the light takes to move from one season's level to the
   other under `TwoSeason`. It is at least 0 and less than half a season.
3. **`WeatherSpread`**, the standard deviation of a day's log factor. Default 0, which is no weather.
4. **`WeatherCorrelation`**, the day-to-day correlation of the log factor, in [0, 1). Default 0.
5. **`WeatherSeed`**, a `ulong` that seeds the weather series alone. Default 1.

`DayFactorAt(elapsedSeconds)` stays a pure function of elapsed seconds and the config. It holds no
phase and adds nothing to a checkpoint.

**The smooth shape is today's arithmetic exactly.** At `Smooth` and a spread of 0, every factor is
the bit pattern today's code returns. A test pins this at the round 56 settings (day 1,000 s at 0.5,
year 10,000 s at 0.5) over a whole year at a fine step.

**The two-season shape.** The season factor is `1 + A·s(t)`, where `A` is `SeasonAmplitude` and
`s(t)` is +1 in summer and −1 in winter. Each switch is centred on a midnight, the darkest point of
the day sine (elapsed seconds ≡ 3/4 of a day, mod a day). With `Y` the year and `D` the day, summer
runs from `−D/4` to `Y/2 − D/4` (mod `Y`) and winter from `Y/2 − D/4` to `Y − D/4`. A run therefore
opens in summer, and round 57's first winter runs from 2,750 to 5,750 s. Each switch runs over
`SeasonRampSeconds` through a smoothstep that is odd about its centre. The day sine is even about
a midnight and the ramp is odd, so their cross term cancels at each switch, and the year's mean light
is exactly the mean. With a day cycle on, the world refuses a `TwoSeason` year that is not an even
number of days (at least 2), so each half holds whole days. Extend `World.ValidateSeasons` to cover
this.

**The weather.** Day `n` runs from midnight to midnight, so that `n = floor((t + D/4) / D)` where `D`
is the day length. Its log factor `x_n` is a stationary AR(1) process:

- `μ = −σ²/2`, so that the factor `exp(x_n)` has mean 1;
- `x_0 = μ + σ·ε_0`;
- `x_n = μ + ρ·(x_{n−1} − μ) + σ·√(1 − ρ²)·ε_n`;

where `σ` is `WeatherSpread`, `ρ` is `WeatherCorrelation`, and the `ε` are `Rng.Gaussian()` draws from
`new Rng(WeatherSeed)`, in day order, in double precision. Compute the series once, lazily and
deterministically, extend it as later days are asked for, and never let the order of calls change a
value. A cache on the model is allowed because the series is a function of the config alone.

The day's factor blends to the next over the quarter-day centred on the midnight between them, by the
same smoothstep. The change happens in the dark. The light is `season × day × weather`. At a spread of
0 the weather is exactly 1 and draws nothing. The world refuses weather (spread above 0) without a day
cycle.

## Wiring and the record

- `RunConfigTests` and `RunConfigJsonTests` must pass with the five new tunables, and the farm's
  `EnvBinding` must carry them as it carries `SeasonAmplitude`. Read how it does that, and the
  `Report.cs` lines that name the season, and do the same.
- The recorded test fixtures and pins are re-recorded as `src/Evosim.Core/CLAUDE.md` says. List each
  one you re-record.
- The nursery's configs and recipes are not yours; another agent migrates them.

## The scent restore

Build it as `logbook/specs/r57-build/scent-restore.md` says, with its Core test.

## The contact grid's reach

A body can blow up between two divergence checks (0.5 s apart) far enough that `ContactGrid`'s cell
`Floor` (`src/Evosim.Dynamics/Contacts.cs`) wraps, from `(int)v` at int.MinValue to int.MaxValue. The
cell loop then runs past `_entryKey` and the run ends in an IndexOutOfRangeException. This happened on
the farm on 2026-10-05: round 56 seed 2 under one drag panel a side, body 86464 at 28,179.5 s
(`logbook/specs/nursery-speed.md`, "W1 on the farm", on branch `nursery-speed`).

- In `Build` (a body's sphere) and in `BuildLinks` (each part's sphere), test every active row for a
  centre or radius that is out of reach on any axis: `!(|c| + r < 1e9 · cell)`. The test catches NaN and
  ±∞ as well.
- `BuildLinks` runs per slab in parallel. Each slab records its first stray row and gives that row an
  empty cell range. A serial scan in slab order then takes the strays, so the outcome is the same at any
  thread count. Branch `nursery-speed` at `b6adb91` has a version that throws; read it.
- A stray body is killed as diverged through the farm's existing path (`Simulation.HandleDivergence`,
  then `World.KillDiverged`), with its dump and a reason that names the link, centre and radius. It
  does not throw. Kill it before anything else reads the grid in that step, and say in the report where
  the kill sits in the step.
- A run in which no sphere strays is bit-identical to today's. A NaN sphere, which today's grid passes
  without crashing, now kills its body. That is a change only to a body that has already diverged.
- A Dynamics test: a body whose link is placed at 1e12 m is killed as diverged at that step, at 1 and
  at 4 threads, and the run goes on.

## Tests to add (Core, fast)

- The smooth shape at the round 56 settings matches today's formula bit for bit.
- The two-season year's mean factor over a whole year is 1 to within float rounding, with the ramp
  and with a day cycle. Each half's mean is `1 ± A`, give or take the ramps.
- The refusals: an odd number of days, a ramp of half a season or more, weather without a day cycle,
  a correlation of 1.
- The weather series: the same seed gives the same series in any call order; a spread of 0 is exactly 1;
  over 100,000 days the sample mean factor is 1 and the lag-1 correlation of the log is `ρ`, each to
  within its sampling error.
- The scent restore's test.

## What to report

The files changed, each new tunable's default, every refusal's message, the fixtures re-recorded, the
filtered tests with their counts, and anything in this brief that the code made you decide differently,
with the reason.
