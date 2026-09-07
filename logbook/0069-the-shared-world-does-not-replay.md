# 0069 — The shared world does not replay

**2026-09-06**  ·  an instrument note, not a round

Until this week, running the simulation twice with the same seed and settings gave the same
world twice, down to the last decimal. That property is called **replay**, and a lot rests
on it. The theatre re-simulates a recorded run rather than playing a video, so it can only
show you *the* run if the physics comes out the same each time.

The new shared-space world, where creatures live in one box and bump into each other, does
**not** replay. Two runs of the same seed agree for the first 1,400 seconds and then drift
into two different worlds.

The cause is not our code. It is the way Unity spreads the physics work across CPU threads.
When two touching bodies are solved in a different order, one velocity comes out different
in its last binary digit, and the world amplifies that speck into a different history.

Turning the extra threads off makes the runs identical again, and that is what we do from
now on, at a modest cost in speed. Every shared-world run before today is therefore one roll
of the dice for its seed. The rounds were already being read that way, so no result changes,
and what was false for those runs was only the promise of replay.

This note was written while round 27 (logbook/0068) runs. Nothing here is scored and no
world rule was touched. It was rewritten the same evening after the owner read the first
draft and could not follow it, and the reader's key in `README.md` now carries the terms it
needs.

## How it was noticed

Round 27's seed 4, `r27-s4`, is the same world as the diagnostic run that justified the
round, `r26d-s4`. Same seed, same code, same settings file, and even the same worker copy of
the Unity project. The only difference is how long each was told to run, which does not
affect the physics.

The diagnostic had held a stable stomach clade of 195 creatures at 20,000 s. The round's run
had zero stomachs at 25,900 s.

Sample by sample, the two runs agree through t=1,400 and differ from t=1,500 onward. They
differ in forty columns at once, in the fifth significant figure, and the gap then grows.
That is the fingerprint of one tiny numerical difference amplified by chaos, rather than of
a rule behaving differently.

## Testing it on purpose

To separate a fluke from a property, I ran the same world, seed 4 for 3,000 s, again and
again on a spare worker, one run at a time. One physics setting changed between pairs. The
script `scripts/compare-det.py` reports the first sample at which two runs differ.

| pair | what was changed | agree through | differ from |
|---|---|---|---|
| `r26d-s4` / `r27-s4` | nothing — the round's own settings | 1,400 s | 1,500 s |
| `det0-a` / `det0-b` | nothing | 1,500 s | 1,600 s |
| `det1-a` / `det1-b` | Unity's *Enable Enhanced Determinism* turned on | 1,500 s | 1,600 s |
| `det2-a` / `det2-b` | also: no sleeping bodies, a different broadphase, a 16× larger scratch buffer | 1,300 s | 1,400 s |

Six runs of one world gave six different worlds. *Enhanced Determinism* is the Unity setting
whose stated purpose is to make simulation results independent of what else is in the scene,
and it changed nothing. Neither did the other three settings. They moved the trajectory from
t=500 on, as any physics change would, and the pairs still parted.

Two things stood out from that. The first is that the divergence is not random in time.
Every pair holds for roughly 140,000 to 150,000 physics steps and parts in the same two
hundred seconds. That is when the population reaches about 110 and newborns begin to be
refused for want of room. The crowded count jumps from 8 to 54 per window between 1,300 and
1,400 s. Something about a crowd triggers it.

The second is that the runs which proved replay in the past were all in the old **tiled**
world. There each creature sat 100 m from every other, and nothing ever touched. Bodies that
never touch are solved independently by the physics engine. Bodies that touch are solved
together, as a group the engine calls an **island**.

The shared world had been checked for having no exploding bodies on a long run, and never
for replaying against a second run of itself. The guarantee had never been tested where it
now fails.

## It was not our code

A read-only audit covered everything the shared world added: the box, the sea floor, newborn
placement, the wrap at the box's edges, the surface rule and the contact counter.

It found nothing that could differ between two runs with the same inputs. There are no
clocks, no random numbers outside the seeded generator, and no lookups whose order changes
from process to process. The one parallel loop in the drag calculation writes each body's
result to its own slot, and it is shared with the tiled world, which replays.

What the audit did find were *amplifiers*: one random draw per placement attempt, a per-step
ranking of parents by energy, and a hard rule at the waterline. Each turns a one-bit
difference into a different world within a single metabolic step. That is the reason the
first *visible* difference is forty columns at once rather than one.

## Finding the exact step

The run reports sample every 100 s, and they cannot say which physics step, or which body,
went first. So an instrument was built for it, the **state digest**.

Every N physics steps it writes one hash of every living body's position, orientation and
velocities. A hash is a short fingerprint of a set of numbers: if two runs' fingerprints
agree, the numbers agree. At chosen steps it can also dump every body's raw numbers.

The digest is off by default, under `EVOSIM_DIGEST_EVERY`, and with it on or off the tiled
world still replays to the last decimal, so it reads and does not act. The comparison is
`scripts/digest-diff.py`.

With a digest every step, two runs of the world, `det3-a` and `det3-b`, agree through step
147,777 and differ at **step 147,778 (t=1,477.78 s)**. The difference is in **one creature,
and only in its velocity**: its root moved at −0.07242366 m/s in one run and −0.0724236444
m/s in the other.

That is a difference of one or two **ulp**. An ulp is a *unit in the last place*, the
smallest change a floating-point number can hold. Every position and orientation of every
body in the world is still bit for bit the same.

The difference spreads by touch. One body carries it at that step, three at the next, and
four within seven steps, all neighbours 0.4 to 0.6 m apart in one crowded pocket. All six
digested runs share one fingerprint at step 147,700 and hold four different ones by 147,800.
That is a fork with a few branches rather than noise.

## Finding the cause

Unity runs physics on a pool of **job worker threads**, which are extra CPU threads that
share out the work. This machine has 32 logical threads and Unity's default is 31 workers. I
first wrote that figure down as 15, and the D078 build read the true ceiling from the job
system.

The engine's maker, NVIDIA, documents that PhysX results do not depend on the number of
threads. Two more pairs tested that claim by launching Unity with fewer workers.

| pair | job worker threads | agree through | differ from |
|---|---|---|---|
| `det3-a` / `det3-b` | 31 (the default) | step 147,777 | step 147,778 |
| `det4-a` / `det4-b` | 1 | step 184,600 | step 184,700 |
| `det5-a` / `det5-b` | **0** — all physics on the main thread | **all 300,000 steps** | never |

With no worker threads, the two runs are identical to the end, with 593 pairs of touching
bodies per step by then. With one worker the fork comes later, and with thirty-one it comes
sooner.

So the number of threads decides how often the fork is reached, and zero threads means
never. In this build, with articulated creatures pressing on each other, the documented
thread-independence does not hold.

## What it means

The replay guarantee of logbook/0052 was a property of the tiled world. Every shared-world
run to date is one realisation of its seed: the screen (0065), the box build (0066), the
lean confirmation (0067), and round 27 with its diagnostic (0068).

No round's reading changes because of this. A seed was already treated as one draw and
compared across seeds, and the fast-step butterfly rule in CLAUDE.md said as much. What was
false was the promise that a given seed could be re-run and watched.

The theatre's World mode re-simulates a run from its seed and checks itself against the
run's own statistics as it goes. On a shared-world run recorded before today it will agree
to about t=1,500 and then follow a cousin, which its identity check will report. Runs made
from now on, with the physics single-threaded, will replay.

The fix is D078. The physics step runs with no job worker threads by default. The setting is
recorded in each run's manifest and header, so a reader can see which way a run was made.

Changes to the shared world are validated with the digest on a zero-thread pair, rather than
only with a tiled replay. The drag calculation is more than half of each step, and it runs
on the project's own threads, so it is unaffected. Only the physics solver becomes
single-threaded.

Measured at 120 to 180 bodies, a 3,000-s run took 6.4 minutes instead of 5.4. The cost at a
full round's population is measured below.

## The long confirmation

The pair `det6-a` and `det6-b` ran the same zero-thread world to 10,000 s, a million physics
steps, with the state digest taken every hundred steps. The two are identical on all 10,001
digests and on every one of the 100 statistics samples.

The world peaked at 518 bodies and ended with 318. That is fewer than the thousand I
expected when I pre-registered the pair, so the long confirmation is a long one rather than
a big one. The population it did carry was in contact throughout, at about 44 touching pairs
per step by the end.

The cost was lower than the short probes suggested. The first run took 28.6 wall minutes and
the second 25.2. The short pairs at the default thread count had taken 2.6 minutes per 1,000
s. At this population, then, the single-threaded solver runs at about the same pace.

The round-sized cost, at two to three thousand bodies, is what round 28 will measure
(logbook/0070).

*Added 2026-09-07.* The big confirmation came with round 28. Its replay probe `r28p-s1` ran
seed 1 of the shared box for 10,000 s beside the scored arm `r28-s1`, both with the physics on
one thread, and the two are identical on all 10,001 digests and all 100 samples at about
1,700 bodies and 745 touching pairs per step (logbook/0070's M7). The probe took 108 wall
minutes for its 10,000 s with four other arms on the machine, and the scored arm 632 minutes
for 30,000 s, which is inside the budget 0070 set.

The D078 build follows from here. The zero-thread setting becomes the default, recorded in
every manifest and header, and the same digest comparison is the check that it landed.
