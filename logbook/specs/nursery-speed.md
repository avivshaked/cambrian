# The nursery's pace: where a generation's time goes, and what would speed it up

Begun on 2026-10-04 and filled from the timing slot of 2026-10-05. Every number is marked as
measured, believed (inferred from the code or from measured parts) or guessed.

## Brain v2 is not slower, the cores were

The nursery evolves brains on fixed bodies in short tank episodes (`nursery-general-build.md`). In
it, brain v2 (the one network a creature, D146) took 7.7 minutes a generation where the per-node
brain had taken 4.6. That gap was read as brain v2's cost and held the next nursery back. It was
not the brain. The two runs sat on different cores.

The machine has eight fast cores and sixteen efficiency cores. The per-node control,
`pilot-deep-own16`, ran on all of them for its first 62 generations, at 4.4 to 4.8 minutes each
(measured, its own log). At 19:17 on 2026-10-01 the heat watch warned twice, and the coordinator
pinned the run to the efficiency cores. Generation 63 ended two minutes later, and every
generation after it took 7.5 to 7.8 minutes. The run and its brain stayed the same throughout.
Brain v2's run was pinned to the efficiency cores from its launch, and took 7.6 to 8.0 minutes a
generation (measured). On the same cores the two brains run at the same pace.

The coordinator's one-thread reads of 2026-10-02 had already said so (HANDOFF of that day,
"brain v2 is not slower"). Each read swam the same four tank episodes, and brain v2 took 77.1 s
against the per-node brain's 75.3 and 77.3 s (measured, `scratch/nursery/timing-*`). The senses
and the brain together were about 2% of it. The one-network brain reads every sense of every part,
which the sensing cost note feared, and that costs about a second in 77.

So the factor of 1.6 between all cores and the efficiency cores is a heat ruling, and it is the
owner's (B0 below). The rest of this file is about what code can change.

## Where an episode's time goes

An episode is one tank of eight bodies swimming for 600 simulated seconds. The physics steps every
hundredth of a second and the world (light, feeding, the snow and its transport) every half
second. A generation is 192 such episodes, spread over 14 threads, one episode a thread.

Before the snow kernel of 2026-10-03, a one-thread episode split as below (measured, the
coordinator's read of 2026-10-02, summed over four episodes and divided here). The world was three
quarters of it, and carrying the snow with the current was 88% of the world (measured, the world
probe). The snow kernel made a whole episode 1.76 times faster at one thread (measured, HANDOFF of
2026-10-03), and a generation at 14 threads went from 7.8 to 5.0 or 5.1 minutes (measured,
`brain2-rep2` against the two separation arms, all pinned to the efficiency cores).

| Part of an episode | Before the snow kernel, s an episode | Share |
|---|---|---|
| World step | 14.9 | 78% |
| Physics | 4.3 | 22% |
| of which senses and brain | 0.4 | 2% |

On the merged build the world is still the larger part, at 62 to 64%. The slot of 2026-10-05
read it on `nursery-speed` at 4062be0 (main at 544f809). Each reading below is one generation-0
swim of `speed-small-b2` or its per-node twin `speed-small-pn`, on brain2-w2's config (episode
configs de49694b78ed5db7 and 7dda16e02a829ac8). Each swim is four tank episodes of 600 s, seed 0x1,
at one thread on one efficiency core (affinity 0x10000), and each was read twice. All of it is
measured, from the runs' `timing.jsonl` and `profile.jsonl`.

| | Brain v2 | Per-node brain |
|---|---|---|
| Wall of the four episodes, s | 44.7, 45.6 | 44.0, 44.5 |
| World step | 62.2%, 62.8% | 63.4%, 64.1% |
| Physics | 36.9%, 36.4% | 35.8%, 35.0% |
| of which the water at the bodies' links | 8.9%, 8.6% | 9.3%, 9.1% |
| of which pinning the current each step | 6.4%, 5.9% | 6.3%, 6.3% |
| of which the contact grid | 4.9%, 4.9% | 4.9%, 4.9% |
| of which senses and brain | 3.4%, 3.3% | 1.7%, 1.6% |
| of which the rest of a body's step (drive, drag, contacts, solve) | 10.4%, 10.9% | 10.8%, 10.6% |
| Senses and brain per body-step, µs | 0.79, 0.80 | 0.39, 0.38 |

Brain v2 costs about 1 to 2% of an episode more than the per-node brain. Its senses and brain take
twice as long, but they are a thirtieth of the episode. With the profile switch off, brain v2's
swim took 44.5 s, so the switch costs less than the spread between repeats.

Two things are new since the read of 2026-10-02. The water, sampled inside each body's step since
main's Package C, is now the largest single part of the physics. And pinning the current costs 6%
of an episode whatever the tank holds. The field's tables are recomputed at every 0.01 s step.
A farm round's thousands of bodies hide that cost, and an eight-body tank does not (A7 below).

## A third of every generation ran on one thread

At fourteen threads on the efficiency cores (affinity 0xFFFF0000), a generation of `speed-full-b2`
took 300.5 to 307.1 s, and the per-node twin 295.6 to 299.5 s (measured, four and two generations).
Brain v2 and the per-node brain are within 2% of each other there too. The world was 68% of the
tank jobs' time, physics 31%, and senses and brain 2.7%.

The threads were busy 68% of each generation, in all six. The profile shows why (measured,
`p14-b2-a`, generation 0). Fourteen threads each ran 13 tank jobs in the first 200 s. A fifteenth
pool thread, first seen at 197 s, then ran the last 10 jobs one after another, alone, until 307 s.
For 103 s of the generation one tank swam alone. `Parallel.For` hands its range out in chunks
that grow as it goes, and the last chunk went to one worker that nothing stole from (believed,
from the pattern). The profile shows the outcome; the partitioner's choice is inferred.

The tank jobs' own time was 2,941 s. Fourteen threads kept busy would finish a generation in about
210 s, plus a tail of one job (10.6 to 16.4 s, median 15.5 s). That is A1, now built: the
jobs handed out one at a time to whichever thread is free (commit f8ffe93).

A1 made a generation 1.46 times faster (measured, 2026-10-05, `a1-p14`). The same recipe at fourteen
threads on the efficiency cores took 210.8 and 209.2 s a generation against `p14-b2-a`'s 307.1 and
306.7 s, and the threads were busy 97.8 and 97.9% against 68%. The tail at a generation's end is now
13 s, about one job's length. Every result file of `a1-p14` is byte-identical to `p14-b2-a`'s, so the
hand-out changed when each tank swam and nothing else. The first check, at two threads on
`speed-small-b2`, had swum only four tanks and so barely exercised the hand-out.

That fourteen-thread check first came back as a false alarm. The slot's script built the path to
the comparison tool with a backslash that a shell escape turned into a control character. Python
could not open the file and exited 2. The script tested only for a nonzero exit, and so reported
that A1 had moved a result. It stopped before the heat readings. The comparison run by hand minutes
later was identical. A check has three outcomes, not two: the files differ, they are the same, or
the check could not run. Here the third read as the first. The comparison tool now exits 3 when it
cannot run, and its caller treats every code but 0 and 1 as "could not run", which is no verdict at
all.

## A3: one thread, no fork

A nursery tank steps its world on one thread, yet every step it went through `Parallel.For` for
its bodies and for each of the contact grid's ten slab passes. A3 (commit 39ec602) loops directly
when a world has one thread and calls a lone slab directly. The loop and `Parallel.For` call the
same method, so the arithmetic is the same compiled code.

Measured on 2026-10-05 at one thread on one efficiency core, `speed-small-b2`, four episodes, from
`profile.jsonl`: main at 7887fbc took 17.4 and 16.7 s of physics, and A3 took 13.6, 14.2 and 14.6 s.
The contact grid's build fell from 2.2 s to 0.5 or 0.6 s, the after-step from 0.7 to 0.1 s, and the
bodies' loop by about half a second. The physics lost about 2.7 s in 45, about 6% of an episode,
as the speed plan had it. The wall of the four episodes (48.0 and 45.3 s before, 42.4, 43.6 and
45.0 s after) spreads as much as the gain, because the world step alone varied from 28.0 to 30.1 s
between reads on that core. The digest check passed at two threads against the reference, and the
one-thread runs before and after A3 are byte-identical to it as well.

A5, ending a tank once every body in it is dead, would save nothing at today's settings: no tank
of the 384 swum had all its bodies die (measured).

## The owner's all-cores trial

On 2026-10-05 the owner asked for a trial of the nursery on all cores before ruling on it (B0). The
same recipe ran at fourteen threads with no affinity pin from 11:45:50 for 30 minutes, and was
stopped by id as planned after nine generations (`speed-allcores-b2`, built at 4062be0).

| | Pinned to the efficiency cores | All cores |
|---|---|---|
| s a generation | 300.5 to 307.1 | 182.0 to 185.3 |
| Threads busy | 68% | 72% |
| CPU maximum | 87 °C | 94 °C |
| Seconds at or above 85 °C | 32 of 1,868 | 60 of 1,806 |
| Seconds at or above 90 °C | 0 | 6 |
| Seconds at or above 95 °C | 0 | 0 |
| The heat watch | OK at every read | OK at every read, CPU 63 to 84 °C when read |

All of it is measured: the pace from `timing.jsonl` and `profile.jsonl`, the curves from MSI
Afterburner's one-second log. All cores run the nursery 1.65 times faster than the efficiency
cores. They hold the processor in the mid to high 80s for the whole run, with two brief spikes to
91 and 94 °C. The pinned runs touch 85 to 87 °C only in spikes. Whether that margin is acceptable
was the owner's ruling: pinned for now, and a fresh reading once A1 is built (above, B0).

A1 shows on all cores as well, where the last job started about 179 s into a 184 s generation and
about 470 thread-seconds sat idle each generation. With A1 and all cores together a generation
would take about 130 s (believed, from the profile).

## Speed-ups that leave every result unchanged

These are agent work. Each is built only after the coordinator agrees, and proven by the nursery's
digest check: a trial run whose result files must be byte-identical to the reference build's.

| | Speed-up | Expected gain | How the gain was estimated | State |
|---|---|---|---|---|
| A1 | The tank jobs handed out one at a time, so no thread runs the last jobs alone | 1.46 times a generation at 14 threads (measured, `a1-p14`) | The slot's profile, above | Built (f8ffe93); 1.46 times measured at 14 threads, byte-identical |
| A2 | The water sampled at each body part by one fused, flattened routine, as the snow kernel did for the grid | 12 to 14% of the physics step (believed) | `logbook/specs/nursery-speed/opus-a2-a3.md`, items 2 and 3 | Designed |
| A3 | A world stepping on one thread loops directly, without setting up parallel work eleven times a step | about 6% of an episode (measured: 2.7 s of physics in 45, below) | The same read, item 1 | Built (39ec602), byte-identical |
| A4 | The bed's sample skipped when a part is clear of the floor | about 5% of the physics step (believed) | Built as P3 on main, and in this branch since the merge of main at 544f809 | Merged; the slot measures the build with it |
| A5 | An episode ends once every body in its tank is dead | none at today's settings (measured: no tank of 384 lost every body) | The profile counts the seconds swum after the last death | Not recommended unless the settings change |
| A7 | The current's tables for each step computed once per draw rather than once per tank: every tank of a draw is built on the same seed, so the same phases (believed, `SwimTanks` passes `seeds[d]`), and `CurrentField.EnsureInstant` recomputes four sines and cosines a term at every 0.01 s step | at most the pin's share of a job, 5.9% at 2 threads (measured, d-on); less what sharing costs | The slot's digest run; the memory a draw's 60,000 steps of tables would take decides the design | Candidate |
| A6 | The sense and brain path made direct | under 2% (measured share) | The 2% above | Not recommended |

*To be filled from the slot: each gain measured, or struck.*

## Changes that move results, which are the owner's

These are listed with their implications and nothing is built for them.

- B0, all cores instead of the efficiency cores: 1.65 times (measured, the trial above). At 14
  threads on all cores the processor read 90 °C on 2026-10-01 and the heat watch warned twice.
  The pin dates from then. The trial of 2026-10-05 peaked at 94 °C with the watch never warning.
  The owner ruled the same day that nursery runs stay pinned for now. A1 is built first, and the
  owner rules again on fresh heat readings of A1 pinned and on all cores, since A1 keeps every
  thread busy and will run warmer than the pinned runs of the slot.
- B1, sixteen bodies a tank instead of eight: the world step is paid per tank, so this about halves
  its cost per body-episode (believed). The crowding changes, and with it what a brain is selected
  for.
- B2, a smaller tank: the world step scales with the tank's cells (believed). The larder and the
  distances change.
- B3 and B4, the snow carried every second step, or on 2 m cells: about 2 and 8 times on the
  snow's transport (believed, `snow-advection-speed-read.md`, items 10 and 11). Both change the
  water every body feeds in.
- B5, fewer draws or shorter episodes: proportional (believed). A brain's score gets noisier.
- B6, the graphics card: `nursery-gpu-plan.md`. About 3 times a generation (guessed, 1.5 to 6)
  after 9 to 13 days of build, the precision a separate ruling.

## What a 1,500 s episode would cost

The next nursery may swim episodes of about 1,500 s. If cost scales with episode length (believed),
that is 2.5 times today's 5.0 to 5.1 minutes: 12.6 to 12.8 minutes a generation on 14 efficiency
cores, about 21 hours for 100 generations. The exam adds about 36 generations' worth of tank
episodes as the current recipe has it (believed, from `nursery-gpu-plan.md`).

The slot measured 300.5 to 307.1 s a generation on the merged build, so the estimate stands: about
12.5 to 12.8 minutes at 1,500 s (believed). A1 would bring it to about 9 minutes, and A1 on all
cores to about 5.5 (believed, the second waiting on the owner's heat ruling).

## The tools

The digest check is `scripts/reads/nursery-digests.py`, the profile's reader
`scripts/reads/nursery-profile-read.py`, and the heat curves come from `scripts/reads/temp-curve.py`.
The profile itself is switched on with `EVOSIM_NURSERY_PROFILE=1` and writes `profile.jsonl` beside
a run's `timing.jsonl`; it changes no result file (the digest check above).

The switch's counters live in `Evosim.Dynamics` (`DynamicsWorld.ProfileBodySteps`), so the build
that carries them has a new `dynamicsHash`. From then on the farm refuses, by source, every
checkpoint written before it, round 56's included, unless a resume is run as a cousin with
`EVOSIM_ALLOW_SOURCE_MISMATCH=1`. Nothing a checkpoint holds changed; the hash covers the source,
and the source changed.
