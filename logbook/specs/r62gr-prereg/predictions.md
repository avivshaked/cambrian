# The growth screen's first group: how long a childhood

*Pre-registration, 2026-10-26, the speed agent. Committed on `main` before the group launches, merged into branch
`growth`, and launched from `scratch/wt-growth` on the coordinator's card go, after the growth build's card identity
sitting has passed. Its record, `identity.md` beside this file, is committed before the launch and names the build the
launch carries. The ruling is D241 in `DECISIONS.md`: growth as a climb under a rate law, one time to adulthood for
all bodies, an adult breeding gate, and both numbers settled by screens. The design is
`logbook/specs/growth-runway-design.md`. The world, the tests, the deciding numbers, the overshoot's marks and the
stopping rule are the pace screen's (`logbook/specs/r62p-prereg/predictions.md`), and this file names only what differs.
Every number below that is not a measurement is a guess or derived, and says so.*

## What it asks

In `r62p-t3`, the one world that has passed D222's tests (`logbook/specs/r62p-read/24000-t3/`), 51 % of the bred grazers
that died never ate, and their median age at death was 23.5 s (measured). The newborn ledger
(`logbook/specs/r62p-read/newborn/`) traced it to growth. On its first metabolic step a newborn turns its birth reserve
into adult body, and it is left with about 18 s of upkeep. A slow newborn needs about 26 s to reach the nearest leaf
(derived). The group asks:

1. Does a climb to adulthood lengthen the newborn's runway enough that more bred grazers eat, and lines keep
   themselves going more easily than in `r62p-t3`?
2. How long a childhood is best: 60, 120 or 240 world seconds?
3. Does a juvenile's strength scaled to its mass, rather than an adult's, change the answer at 120 s?

## The worlds

Four worlds, founded on seed 6201, 24,000 world seconds each, in one card group, on `r62p-t3`'s world
(`rounds/env-r62p-t3.ps1`: the kill gut, the limb push at 2.7, the work price at 0.0556, the newborn reserve at 0.5).

Every world has the growth-reserve gene G. It starts at 30 s at every entry point, lies in the range [1, 300] s, and
mutates at 0.08 a birth, the reserve margin's rate. It evolves inside every arm.

| World | T, world seconds | Juvenile strength | Settings |
|---|---|---|---|
| `r62gr-t60-6201` | 60 | an adult's | `rounds/env-r62gr-t60.ps1` |
| `r62gr-t120-6201` | 120 | an adult's | `rounds/env-r62gr-t120.ps1` |
| `r62gr-t240-6201` | 240 | an adult's | `rounds/env-r62gr-t240.ps1` |
| `r62gr-t120s-6201` | 120 | scaled with mass | `rounds/env-r62gr-t120s.ps1` |

The juvenile-strength switch's environment name, `EVOSIM_JUVENILE_STRENGTH`, is b4's design text. The build binds it,
and the identity sitting checks that the name reaches the config before the launch. The controls are `r62p-t3-6201`
(T = 0, today's growth) and `r62cs-w500-6201`.

## The tests and the deciding numbers

D222's tests, read per world at 24,000 s by `scripts/reads/r62-read.ps1 -Worlds`, as in the pace screen. Beside them,
against `r62p-t3-6201`:
- from `scripts/reads/grazer-tally.py`, for bred grazers: the share that never ate (the kill gut's meals counted), the
  median age at death, and the causes of death;
- the grazers' kept intake over their upkeep;
- the leaf stand against `r62cs-w500-6201`'s, and the overshoot's marks, by `scripts/reads/overshoot-read.py`;
- the island share, by `scripts/reads/r60-read.py --year 3000`.

**Growth's own reads**, reported with no pass mark:
- the gene's drift: the median and quartiles of G on the birth rows (`grs`), by guild and 3,000 s bin;
- each guild's time to adulthood, from the checkpoints' body fractions by age. The reader is written before the read,
  and checked on a fixture;
- the `diverged` count against `r62p-t3`'s, and the wall share of applying growth (`wallHarnessGrowthMs`).

**The winning T.** This rule is fixed now. Among the three worlds with an adult's strength, the winner is the one that
passes D222's three tests with the lowest share of bred grazers that never ate. Where two are within 0.05 of each
other on that share, the shorter T wins, since it is closer to the world as recorded. If none passes, the winner is
the world with the highest best cohort's replacement, and the group's write-up says that the climb alone did not pass.
The winning T goes to group 2, which screens G's starting value at 1, 30 and 60 s
(`logbook/specs/growth-runway-design.md`).

**The strength arm** is read against `r62gr-t120-6201` alone, on the same numbers. It decides whether the switch is on
in group 2: on if the strength arm passes D222 and its never-ate share is no more than 0.05 above `r62gr-t120`'s, off
otherwise. The owner's question, whether a juvenile should be weaker, is then answered in the world's own terms.

## Predictions

These are guesses unless marked derived.

- **The runway** (derived, `logbook/specs/growth-runway-design.md`). An unfed newborn grazer at G = 30 s lasts 43.5 s at
  T = 60, 48 s at T = 120 and 52 s at T = 240, against 19.5 s by the same model for today's growth.
- **Bred grazers that never ate.** 51 % in `r62p-t3`. I guess 40 % at T = 60, 35 % at T = 120 and 35 % at T = 240. The
  longest climb gains little over 120 s, because its newborn is smallest for longest and so reaches food no sooner.
- **D222.** `r62gr-t60` and `r62gr-t120` pass, as `r62p-t3` did, with born shares above its 0.84. `r62gr-t240` passes
  test 1 but is the likeliest to fail test 2, because its leaves climb from 0.19 over 379 s (derived), so the stand
  grows more slowly in the founding years.
- **The winning T** is 120 s.
- **The strength arm** has more bred grazers that never ate than `r62gr-t120`, by 0.05 to 0.15, because a newborn at
  0.65 of its adult strokes with 0.65 of its torque and swims more slowly. So the switch is off in group 2.
- **The gene.** G's median in grazers stays within a factor of 2 of its start of 30 s by 24,000 s, at about six bred
  generations, with only the margin's mutation rate to move it.
- **Divergence.** No world diverges more than `r62p-t3` did, because the climb resizes in steps of about 2 % of
  length instead of one of 15 % (derived).

## The machine

The four worlds run at the pace factor 1. `r62p-t3` reached 24,000 s in 3.3 hours beside two other worlds (measured).
Applying growth took 0.47 % of its wall. The climb resizes a leaf about 19 times at T = 120 s and 38 at T = 240 s,
so that share may rise to about 9 and 18 % (a guess).

The group runs on logical 22 to 31 at 10 threads, which leaves 16 to 21 for the theatre's one Unity (the
coordinator's ruling of 2026-10-26). A card group is bit-identical at any thread count, so the results do not move.
The pace does. The host's share of the wall was 39 to 42 % in `r62p-t3` and `r62kg-6201` (measured), so 10 threads
against 14 cost 13 to 25 % of pace (derived, if the host part scales with threads). I expect about 1.1 to 1.2 times
real time early and 0.9 to 1.0 late, and 5.5 to 7 hours in all (guess). If a world runs under 0.7 times real time for
an hour, the theatre gives way first. The coordinator's exit stands: after 12,000 s, a world under 0.6 times real time for an hour is
stopped with `scripts/stop-arm.ps1` and read at its stop second.

## What would change the plan

If no world with an adult's strength has fewer bred grazers that never ate than `r62p-t3`, the climb does not reach the
newborn bottleneck. Group 2 then waits, and the write-up reads the newborn's first seconds again with
`scripts/reads/genome-diff-read.py`. If a world overshoots, the next screen rolls back from its dials, as the pace
screen's file says.
