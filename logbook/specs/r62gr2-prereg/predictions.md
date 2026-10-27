# The growth screen's second group: where the reserve starts, and a second seed

*Pre-registration, 2026-10-27, the speed agent. Committed on `main` before the group launches, merged into branch
`growth`, and launched from `scratch/wt-growth` on the coordinator's card go. The ruling is D241 in `DECISIONS.md`,
and the design is `logbook/specs/growth-runway-design.md`. The world, the tests, the deciding numbers, the overshoot's
marks and the stopping rule are group 1's (`logbook/specs/r62gr-prereg/predictions.md`), which takes them from the
pace screen's. This file names only what differs. Every number below that is not a measurement is a guess or derived,
and says so.*

## What it asks

Group 1 ended on 2026-10-27 (`logbook/specs/r62gr-read/24000/`). The climb cut the share of bred grazers that died
never having eaten from `r62p-t3`'s 0.51 to between 0.33 and 0.39 in all four worlds (measured). Only
`r62gr-t60-6201` passed D222's three tests, so by group 1's rule the winning T is 60 s and the juvenile-strength
switch is off. `r62gr-t120` and `r62gr-t240` each failed test 2 in one summer-start window that held 12 to 17
grazers. The growth reserve G did not move from its start of 30 s in any world (measured). The group asks:

1. Does G's starting value matter at T = 60 s: does a newborn that holds back 1 s or 60 s of its standing cost do
   better or worse than one that holds 30 s?
2. Does G move from its start in 24,000 s, and do the starts converge?
3. On a second seed, does T = 60 s still beat T = 120 s, or did one thin window on seed 6201 decide group 1?

## The worlds

Four worlds, 24,000 world seconds each, in one card group, on group 1's world (`rounds/env-r62p-t3.ps1`, with G in
the range [1, 300] s mutating at 0.08 a birth). The juvenile-strength switch is off in all four.

| World | Seed | T, world seconds | G's start | Settings |
|---|---|---|---|---|
| `r62gr2-g1-6201` | 6201 | 60 | 1 s | `rounds/env-r62gr2-g1.ps1` |
| `r62gr2-g60-6201` | 6201 | 60 | 60 s | `rounds/env-r62gr2-g60.ps1` |
| `r62gr-t60-6202` | 6202 | 60 | 30 s | `rounds/env-r62gr-t60.ps1` |
| `r62gr-t120-6202` | 6202 | 120 | 30 s | `rounds/env-r62gr-t120.ps1` |

**The start of 30 s on seed 6201 is not run again.** Its world is `r62gr-t60-6201`, which group 1 has already run
and read. Same settings, seed and build give the same world bit for bit, at any thread count and beside any other
worlds on the card (the growth build's identity sitting, `logbook/specs/r62gr-prereg/identity.md`). So the start's
question reads the two new worlds against `r62gr-t60-6201`. The fourth slot goes to `r62gr-t60-6202`, so that the
second seed carries a matched pair.

The controls are `r62gr-t60-6201` (the start at 30 s), `r62p-t3-6201`, and `r62cs-w500-6201` and `r62cs-w500-6202`
for the overshoot's leaf stand on each seed.

## The identity sitting

None is needed. The build is group 1's, from `scratch/wt-growth` with this file's commit merged in. The commits on
`main` since group 1's launch touch no hashed source, so the manifests must read core `361aa924`, dynamics `29144991`
and farm `f9719a88`, and the launch is checked against them. G's start and the seed are values in the config. Group 1's
sitting proved the climb identical at groups 32 and 64 and at 10 threads against 14.

## The tests and the deciding numbers

D222's tests and group 1's deciding numbers, read per world at 24,000 s, by `scripts/reads/r62gr-read-24000.ps1`
adapted to these worlds. Beside them:
- the share of bred grazers that died never having eaten, and their median age at death (`grazer-tally.py`);
- G's drift, by `scripts/reads/grs-drift-read.py`: its median and quartiles on the bred birth rows, by guild and
  3,000 s bin;
- the newborn's first minute, by `scripts/reads/genome-diff-read.py`, in the start worlds against `r62gr-t60-6201`.

**G's start.** This rule is fixed now. Among the starts of 1, 30 and 60 s on seed 6201, the winner passes D222's three
tests with the lowest share of bred grazers that never ate. Where two are within 0.05 of each other on that share,
the start nearer 30 s wins, since it is the value group 1's winning world ran. If neither new world passes, 30 s
stands.

**The second seed.** This rule is fixed now. T = 60 s stands if `r62gr-t60-6202` passes D222, unless
`r62gr-t120-6202` also passes with a never-ate share more than 0.05 below it. In that case T = 120 s is reopened, and
the write-up proposes a group that runs both on a third seed. If `r62gr-t60-6202` fails D222 and `r62gr-t120-6202`
passes, T = 120 s is reopened the same way. If both fail, T = 60 s stands on group 1's reading, and the write-up says
that neither T passed on seed 6202.

**Convergence.** G's starts converge if, in the 21,000 to 24,000 s bin, the medians of the bred grazers of the three
worlds on seed 6201 lie within a factor of 1.5 of each other. This is reported, and it decides nothing.

## Predictions

These are guesses unless marked derived.

- **The runway** (derived, `logbook/specs/growth-runway-design.md`). An unfed newborn grazer at T = 60 s lasts 35 s
  when G is near 0, 43.5 s at 30 s and 59.5 s at 60 s. Its body at 30 s is 0.86, 0.77 and 0.66 of its adult's.
- **Bred grazers that never ate.** 0.37 at the start of 30 s (measured, `r62gr-t60-6201`). I guess 0.42 at 1 s and
  0.33 at 60 s.
- **D222.** `r62gr2-g60` passes. `r62gr2-g1` passes tests 1 and 3, and is the likelier of the two to fail test 2.
- **G's start.** 60 s is within 0.05 of 30 s on the never-ate share, so 30 s stands.
- **The drift.** G does not converge. Each world's median in grazers stays within a fifth of its own start by
  24,000 s, as group 1's did. The selection on it is too weak, or too slow, for 24,000 s (inference from group 1).
- **The second seed.** Both T pass D222 on seed 6202. `r62gr-t120-6202` has the lower never-ate share by less than
  0.05, as on seed 6201, so T = 60 s stands.
- **The overshoot.** No world overshoots, as in group 1.

## The machine

The group runs on logical 22 to 31 at 10 threads, with 16 to 21 left for the theatre's one Unity (the coordinator's
ruling of 2026-10-27). Group 1's four worlds ran in the same shape and reached 24,000 s in 3.9 hours, at 1.4 to 1.7
times real time over most of the run (measured). The slowest look was `r62gr-t120-6201` at 0.83 times, over a half
hour when the living rose from about 7,000 to 9,000 (measured). This group's worlds are the same size, so I expect
1.1 to 1.5 times late and about 4 hours in all (guess). No world is expected under 0.7 times for an hour. A world on
seed 6202 is a new realisation, and it may hold more bodies (inference).

The exits are group 1's. If a world runs under 0.7 times real time for an hour, the theatre gives way first. After
12,000 s, a world under 0.6 times real time for an hour is stopped with `scripts/stop-arm.ps1` and read at its stop
second, and the read says so. The launcher is `rounds/launch-r62gr2.ps1`.

## What would change the plan

If G's start of 1 s does as well as 30 s, the reserve's threshold is not what lengthens the runway, and the climb's
slower growth is. If either start overshoots, the next screen rolls back from it, as the pace screen's file says.
If T = 120 s is reopened, the growth write-up waits for the third seed.
