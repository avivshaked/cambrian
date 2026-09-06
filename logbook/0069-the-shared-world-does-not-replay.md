# 0069 — The shared world does not replay

*2026-09-06. An instrument note written while round 27 (logbook/0068) runs. Nothing here
is scored and no world rule was touched. Rewritten the same evening after the owner read the
first draft and could not follow it; the reader's key in `README.md` now carries the terms
this entry needs.*

## In one paragraph

Until this week, running the simulation twice with the same seed and settings gave the same
world twice, down to the last decimal. That property is called **replay**, and a lot rests
on it: the theatre re-simulates a recorded run rather than playing a video, so it can only
show you *the* run if the physics comes out the same each time. Today I found that the new
shared-space world — creatures living in one box and bumping into each other — does **not**
replay. Two runs of the same seed agree for the first 1,400 seconds and then drift apart into
two different worlds. The cause is not our code. It is the way Unity spreads the physics work
across CPU threads: when two touching bodies are solved in a different order, one velocity
comes out different in its last binary digit, and the world amplifies that speck into a
different history. Turning the extra threads off makes the runs identical again. That is
what we will do from now on, at a modest cost in speed. Every shared-world run before today
is therefore one roll of the dice for its seed, which is how the rounds were already being
read, so no result changes; only the promise of replay was false for those runs.

## How it was noticed

Round 27's seed 4 (`r27-s4`) is the same world as the diagnostic run that justified the round
(`r26d-s4`): same seed, same code, same settings file, even the same worker copy of the Unity
project. The only difference is how long each was told to run, which does not affect the
physics. The diagnostic had held a stable stomach clade of 195 creatures at 20,000 s. The
round's run had zero stomachs at 25,900 s. Comparing the two runs sample by sample, they
agree exactly through t=1,400 and differ from t=1,500 onward, in forty columns at once, with
differences in the fifth significant figure that then grow. That is the fingerprint of one
tiny numerical difference amplified by chaos, not of a rule behaving differently.

## Testing it on purpose

To separate a fluke from a property, I ran the same world (seed 4, 3,000 s) again and again
on a spare worker, one run at a time, changing one physics setting between pairs.
`scripts/compare-det.py` reports the first sample at which two runs differ.

| pair | what was changed | agree through | differ from |
|---|---|---|---|
| `r26d-s4` / `r27-s4` | nothing — the round's own settings | 1,400 s | 1,500 s |
| `det0-a` / `det0-b` | nothing | 1,500 s | 1,600 s |
| `det1-a` / `det1-b` | Unity's *Enable Enhanced Determinism* turned on | 1,500 s | 1,600 s |
| `det2-a` / `det2-b` | also: no sleeping bodies, a different broadphase, a 16× larger scratch buffer | 1,300 s | 1,400 s |

Six runs of one world gave six different worlds. *Enhanced Determinism* is the Unity
setting whose stated purpose is to make simulation results independent of what else is in
the scene; it changed nothing. Neither did the other three settings (they moved the
trajectory from t=500 on, as any physics change would, but the pairs still parted).

Two things stood out. First, the divergence is not random in time: every pair holds for
roughly 140,000–150,000 physics steps and parts in the same two hundred seconds, which is
when the population reaches about 110 and newborns begin to be refused for want of room
(the "crowded" count jumps from 8 to 54 per window between 1,300 and 1,400 s). Something
about a crowd triggers it. Second, the runs that proved replay in the past were all in the
old **tiled** world, where each creature sat 100 m from every other and nothing ever
touched. Bodies that never touch are solved independently by the physics engine; bodies
that touch are solved together, as a group the engine calls an **island**. The shared world
had been checked for having *no exploding bodies* on a long run, and never for replaying
against a second run of itself. The guarantee had never been tested where it now fails.

## Was it our code?

A read-only audit of everything the shared world added — the box, the sea floor, newborn
placement, the wrap at the box's edges, the surface rule, the contact counter — found
nothing that could differ between two runs with the same inputs: no clocks, no random
numbers outside the seeded generator, no lookups whose order changes from process to
process, and the one parallel loop in the drag calculation writes each body's result to its
own slot and is shared with the tiled world, which replays. What the audit did find were
*amplifiers*: one random draw per placement attempt, a per-step ranking of parents by
energy, a hard rule at the waterline. Each turns a one-bit difference into a different
world within a single metabolic step, which is why the first *visible* difference is forty
columns at once rather than one.

## Finding the exact step

The run reports sample every 100 s and cannot say which physics step, or which body, went
first. So an instrument was built for it: the **state digest**. Every N physics steps it
writes one hash of every living body's position, orientation and velocities (a hash is a
short fingerprint of a set of numbers; if two runs' fingerprints agree, the numbers agree).
At chosen steps it can also dump every body's raw numbers. It is off by default
(`EVOSIM_DIGEST_EVERY`), and with it on or off the tiled world still replays exactly, so it
reads and does not act. `scripts/digest-diff.py` compares two runs' digests.

With a digest every step, two runs of the world (`det3-a`, `det3-b`) agree through step
147,777 and differ at **step 147,778 (t=1,477.78 s)**, in **one creature, and only in its
velocity**: its root moved at −0.07242366 m/s in one run and −0.0724236444 m/s in the
other. That is a difference of one or two **ulp** — one *unit in the last place*, the
smallest change a floating-point number can hold — while every position and orientation of
every body in the world is still bit for bit the same. The difference spreads by touch: one
body at that step, three at the next, four within seven steps, all neighbours 0.4–0.6 m
apart in one crowded pocket. All six digested runs share one fingerprint at step 147,700
and hold four different ones by 147,800: a fork with a few branches, not noise.

## Finding the cause

Unity runs physics on a pool of **job worker threads** — extra CPU threads that share out
the work. This machine has 16 logical cores and Unity used 15 workers. The engine's maker,
NVIDIA, documents that PhysX results do not depend on the number of threads. Two more pairs
tested that claim by launching Unity with fewer workers:

| pair | job worker threads | agree through | differ from |
|---|---|---|---|
| `det3-a` / `det3-b` | 15 (the default) | step 147,777 | step 147,778 |
| `det4-a` / `det4-b` | 1 | step 184,600 | step 184,700 |
| `det5-a` / `det5-b` | **0** — all physics on the main thread | **all 300,000 steps** | never |

With no worker threads, the two runs are identical to the end, with 593 pairs of touching
bodies per step by then. With one worker the fork comes later; with fifteen, sooner. So the
number of threads decides how often the fork is reached, and zero threads means never. In
this build, with articulated creatures pressing on each other, the documented
thread-independence does not hold.

## What it means

- **The record's replay guarantee (logbook/0052) was a property of the tiled world.**
  Every shared-world run to date — the screen (0065), the box build (0066), the lean
  confirmation (0067), round 27 and its diagnostic (0068) — is one realisation of its seed.
- **No round's reading changes.** A seed was already treated as one draw and compared
  across seeds; the fast-step butterfly rule in CLAUDE.md said as much. What was false was
  the promise that a given seed could be re-run and watched.
- **The theatre.** Its World mode re-simulates a run from its seed and checks itself against
  the run's own statistics as it goes. On a shared-world run recorded before today it will
  agree to about t=1,500 and then quietly follow a cousin, and its identity check will say
  so. Runs made from now on, with the physics single-threaded, will replay.
- **The fix, D078:** the physics step runs with no job worker threads by default. The
  setting is recorded in each run's manifest and header so a reader can see which way a run
  was made. Changes to the shared world are validated with the digest on a zero-thread pair,
  not only with a tiled replay. The drag calculation, which is more than half of each step
  and runs on the project's own threads, is unaffected; only the physics solver becomes
  single-threaded. Measured at 120–180 bodies, a 3,000-s run took 6.4 minutes instead of
  5.4. The cost at a full round's population is being measured below.

## The long confirmation

*`det6-a` / `det6-b`: the same zero-thread world to 10,000 s, about 1,000 bodies. Results
appended when they land.*
