# The confirmation round: three new seeds of the chosen settings, and do grazers travel between oases?

*Pre-registration of 2026-10-27, the speed agent, written at the coordinator's request. The owner gave the go at about
18:00 UTC the same day, through the coordinator. The round launches from a clean worktree at this file's commit, after
the card notice, HOLD and the heat watch.*

## The questions

The screens chose the settings one at a time, each on one or two seeds:
- the thrust arm T16, the limb push ×16 at a sixteenth of the price (`logbook/specs/r62p2-read/24000-a/`);
- growth as a climb in T = 60 s, with the growth-reserve gene starting at G = 30 s and juvenile strength off
  (`logbook/specs/r62gr-read/24000/`, `logbook/specs/r62gr2-read/24000/`);
- today's structural mutation rate (`logbook/specs/r62sm-read/24000/`).

1. Do grazers make a living from living leaves on seeds the screens never saw? That is D222 on every new world.
2. The ocean question. Do grazers travel between oases, how often and how far, and how much of their time do they
   spend over open water?

## The worlds

| World | Seed |
|---|---|
| `r62cf-6202` | 6202 |
| `r62cf-6203` | 6203 |
| `r62cf-6204` | 6204 |

- Each world runs 24,000 world seconds, and all three run in one card group.
- The settings are `rounds/env-r62cf.ps1`, which is `rounds/env-r62sm-s100.ps1` unchanged: `r62p-t16`'s world, growth
  at T = 60 and G = 30, and the seven structural chances at today's values.
- D231's whole-assembly push and D234's twin drive stay off. Integration branch 2 is not on main yet, and neither is
  part of the chosen settings.
- The launcher is `rounds/launch-r62cf.ps1`.

`r62sm-s100-6201` is the fourth seed, already run. Main's source at `565ade1e` is the screen's source, checked by an
empty source diff (D243), so the same settings on seed 6201 would replay it bit for bit. The launch checks that the new
manifests carry farm `ae99d3c2`, core `361aa924` and dynamics `29144991`. If any differs, the fourth seed is not the
same build, and the round reads three worlds alone.

No identity sitting is needed. The build is main's, which is the one `r62sid` checked. Since `565ade1e`, main's source
has changed only in `src/Evosim.SwimProbe`, the brain-at-birth bench. No hash covers that folder: the manifest hashes
`src/Evosim.Core`, `src/Evosim.Dynamics` and `src/Evosim.Farm` (`Manifest.cs`). Integration branch 2 is not merged. The card engine's world check refuses a change of clock step: the clock
step is derived from the physics step and the pace factor alone (`SolverConfig.ClockStepSeconds`), and
`GpuBackend.Check` compares both, so no fix is needed before the round.

## The tests and the readouts

**D222's three tests per world at 24,000 s**, by `scripts/reads/screen-read-24000.ps1 -Gene`, as in the screens.

**The primary reading is D222 as the owner amended it at about 18:10 UTC on 2026-10-27 (D244),** for runs aimed at
grazers. Misses in small windows had
come before: growth group 2 failed at 9 to 17 grazers (`logbook/specs/r62gr2-read/24000/`), and `r62sm-s100-6201` at
15. The amended reading, per world:
- **The floor comes first.** The world must reach at least 30 grazers alive. Its count is the largest `g end` in
  `scripts/reads/r62g-windows.py`'s 500 s windows, the grazers alive at a window's end. A world that never reaches 30
  fails outright, whatever its tests say.
- **A small window is not judged.** A summer-start window whose mean count, `g mean`, is under 30 is left out of
  test 2, neither passed nor failed. Test 2 is judged on the summer-start windows that hold 30 or more. A world with no
  such window has no test-2 verdict, and the read says so.
- **Tests 1 and 3 stand as D222 has them.**
- The settings are confirmed when every new world clears the floor and passes every test judged.
- **The same reading at 40 is reported beside it, as a sensitivity.** The owner set 30 as the floor and said the bar
  could be higher. The verdict at 40 informs and decides nothing.

**The strict reading, D222 as it stood before the amendment, is reported second.** Under it, every summer-start
window is judged whatever its count. It no longer decides the verdict, and it shows what the amendment changed.

**The ocean readout is measured.** It is a description, with no bar. It comes from `scripts/reads/grazer-moves-read.py`
at a 10 s stride, on r60-read's islands as seeded at 100 s, per world and per 3,000 s bin.
- **Moves.** A move is a grazer seen over one island and later over another; open water between them does not count.
  The read gives:
  - the moves and the distinct movers;
  - moves per 1,000 grazer-seconds;
  - the share of bred grazers that ever move;
  - each move's straight-line distance, the shorter way round the joined square, as quartiles;
  - its crossing time, as quartiles;
  - the largest move.
- **Open water.** The share of grazer fixes over open water, with the same share for all living bodies beside it.
- **The check.** At a 100 s stride, the move count must equal r60-read's grazer migrants per year.

Beside these, as in the screens:
- the slow-newborn share;
- the never-ate share;
- the deepest chain;
- intake over upkeep;
- r60-read's islands held at 5% or more;
- the overshoot marks against a control world. Only seeds 6201 and 6202 have one (`r62cs-w500-6201`, `-6202`), so
  `r62cf-6203` and `-6204` read against `r62cs-w500-6202` (`screen-read-24000.ps1 -ControlSeed 6202`). That is a
  cross-seed comparison and is reported as one. The other way is to add `r62cs-w500-6203` and `-6204` to the group as
  controls, at the cost of two more worlds on the card. I recommend the cross-seed read: overshoot has never come near
  in these worlds, whose lowest leaf ratio was 0.56 (measured, `logbook/specs/r62sm-read/24000/`).

## Predictions

These are guesses unless marked measured.

- **D222.** Under D244's reading, all three new worlds clear the floor and pass (guess). Under the strict reading,
  two or three pass (guess). `r62sm-s100-6201`'s born share
  was 0.828, its best cohort 1.26, its chain 17 and its intake over upkeep 1.81 (measured).
The fourth seed is already measured. The moves read ran on `r62sm-s100-6201`
(`logbook/specs/r62cf-prereg/moves-test/r62sm-s100-6201.txt`), and its count agrees with r60-read's in every year:

| `r62sm-s100-6201`, 10 s stride | Measured |
|---|---|
| moves in 24,000 s | 14, by 12 grazers |
| bred grazers that ever move | 9 of 3,465, 0.003 |
| moves per 1,000 grazer-seconds | 0.012 |
| distance, quartiles | 13, 16 and 34 m |
| crossing time, quartiles | 60, 90 and 142 s |
| the largest move | 143 m in 510 s |
| open-water share of grazer fixes | 0.18, against 0.38 for all the living |

- **Moves stay rare.** Each new world has under 30 moves, and under 1% of bred grazers ever move (guess, from the
  measured seed). A grazer lives a median of about 68 s (measured), against a median crossing of 90 s, so few live to
  make one (inference).
- **Moves are short.** The median move is 10 to 40 m, mostly between neighbouring islands (guess, from the measured
  seed).
- **Grazers keep to the oases.** Their open-water share is 0.15 to 0.25, under all the living's (guess, from the
  measured seed).

## What a result would mean

**If the settings are confirmed and moves are rare,** as the measured seed already shows: grazers make a living, each
on its own oasis, and the world has oases without migration. The aim is grazers that travel between oases. So the next design question, the owner's, is
what makes a crossing worth its cost: food that runs out on an oasis, a season that moves it, or a longer life
(inference).

**If moves are common:** the aim is already partly met, and the next rounds can measure what drives them.

## The machine

- One card group of three worlds, on logical 22 to 31 at 10 threads, with the theatre's one Unity on 16 to 21.
- The four r62sm worlds took 3 h 47 min together, at 1.1 to 1.5 times real time (measured). Three should take about
  3.5 to 4 h (guess).
- The exits are the screens':
  - a world under 0.7 times real time for an hour makes the theatre give way;
  - after 12,000 s, a world under 0.6 times real time for an hour is stopped with `scripts/stop-arm.ps1` and read at
    its stop second;
  - a world under half its own pace of the hour before is stopped and probed at once.
- The read takes about 5 minutes for the screen read, then the moves read on one pinned core.

## Before launch

- This file committed.
- A clean worktree at that commit, made with `git worktree add scratch/wt-r62cf`. Its Release farm is built from its
  own clean tree, so the manifest's gitCommit names the commit.
- The card notice to assay-d6, its clear, HOLD, the coordinator's heat watch, then the go.

**What a result would gate.** The owner chose fat as what should make a crossing pay. Its screen waits for D222 to
pass, so this round is also its gate.
