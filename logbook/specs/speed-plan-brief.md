# Brief: the speed plan for the farm and the nursery (read-only)

You are planning, not building. Read the code and the measurements named below and return one plan
that ranks every way to make two programs faster. Nothing is built, run, timed or committed by you.

## The two programs

- **The farm** (`src/Evosim.Farm`, `src/Evosim.Dynamics`, `src/Evosim.Core`): one shared world of
  about 4,300 to 4,900 articulated bodies, stepped by its own solver, bit-identical at any thread
  count. A round runs 30,000 simulated seconds at 8 threads.
- **The nursery** (`src/Evosim.Nursery`): evolves brains on fixed bodies. A generation is 192 tank
  episodes (96 genomes by 16 draws, 8 bodies a tank), each its own small Core `World` plus a farm
  `Simulation` at one thread, 600 simulated seconds, physics every 0.01 s and the world every 0.5 s.
  The tank jobs run in parallel, one job a thread.

Read the code in `D:\Projects\experiments\evolution-simulator\scratch\wt-speed` (branch
`nursery-speed`: main at 544f809 plus the nursery's profile switch and A1). Read paths under that
folder unless a path below says otherwise.

## What is measured (read these first)

1. `logbook/specs/nursery-speed.md`: the nursery's slot of 2026-10-05. Where an episode's time goes
   at one and fourteen threads, brain v2 against the per-node brain, the one-thread tail (A1, built
   and digest-checked), the all-cores trial (1.65 times, peak 94 °C), and the candidates A1 to A7 and
   B0 to B6.
2. `logbook/specs/nursery-gpu-plan.md`: an earlier read-only plan for the nursery on the graphics
   card, with its stages, line counts and days.
3. `D:\Projects\experiments\evolution-simulator\logbook\specs\nursery-speed\opus-a2-a3.md`: a read of
   the physics step's bit-identical savings (a serial path at one thread, folded tables, a fused water
   sampler, a contact-bed early-out, trace off, fluid panels, SkipLocalsInit) and three that change
   results (N1, N2, W1).
4. `D:\Projects\experiments\evolution-simulator\logbook\specs\farm-physics-speed-read.md` and
   `snow-advection-speed-read.md` in the same folder: the farm's earlier speed reads, P1 to P3 built.
5. `D:\Projects\experiments\evolution-simulator\scratch\speed-plan\farm-profile.md` (committed as
   `logbook/specs/farm-profile-2026-10-05.md`): the farm
   profile of 2026-10-05 (shares by method, the pace, the wall split, and whether the farm's
   `Parallel.For` leaves threads idle at a step's end). If it is missing, say so and plan without it.
6. Round 56's pace, measured from its reports: 1.29 to 1.31 times real time at 8 threads; physics 69
   to 71% of the wall (the bodies' phase 54 to 57%, the contact grid 12 to 13%), the world 26 to 28%.

## The owner's rulings that bind the plan

- The owner wants a full speed-up of both programs, built and tested after round 56.
- Whether to fund the card work is decided after this plan. The bodies on the card run in double,
  with single precision measured once as a reading only. The snow's field passes on the card are
  double. The card is a GeForce RTX 4090 under a hold until about 2026-10-06; work on it is by day
  under a heat watch.
- Nursery runs stay pinned to the efficiency cores for now (the owner, 2026-10-05). A1 is built and
  timed first. The owner then rules again on two fresh heat readings: A1 pinned and A1 on all cores,
  the same recipe, about 30 minutes each, heat logged every 2 minutes, the Afterburner curve
  summarised as in `nursery-speed.md`. Put those two readings in the build order as the step after
  A1's fourteen-thread timing, and plan the nursery's gains both pinned and on all cores.
- The machine is an i9-13900K: logical processors 0 to 15 are the eight fast cores, 16 to 31 the
  sixteen efficiency cores. Total load stays at or below 16 logical processors.

## What to return

Your final message is the plan, in Markdown, with tables. Do not write it to a file; it will be saved
as it comes. It holds:

1. **One ranked table of every speed-up for both programs**, CPU and card together. Columns: id
   (keep A1 to A7, B0 to B6, P, N and W ids where they exist; new ones get new ids), program (farm,
   nursery or both), what it is, its class, expected gain with its basis, cost in agent-days, risk,
   what it needs first, how it is proven.
   - Class is one of: **BI**, bit-identical, agent work, proven by the nursery's digest check or a
     farm resume against a round's rows; **NR**, a new realisation, the same world with different
     numbers, the owner's call; **WR**, a world rule changed, the owner's call.
   - Every gain is marked **measured** (name the file and number), **believed** (inferred from code or
     from measured parts; say how) or **guessed**. Never present a guess as a measurement.
2. **The recommended order of the build**: BI CPU items first, each proven before the next, and where
   the card fits, with the days each step takes and what is measured after it.
3. **Where the plan is unsure**: the measurements that would change the ranking, each with what to
   run, how long and at how many threads.
4. **What you checked in the code** for each item you rank above the median: file and line.

## Rules

- Read only. No builds, tests, nursery runs, farm runs, profiles, benchmarks or timing of any kind.
  No git commands that write. Do not stop any process.
- You may write only under `D:\Projects\experiments\evolution-simulator\scratch\speed-plan\work\`, for
  your own notes and helper scripts, and you delete that folder's contents before you return. Never
  write to the Windows TEMP directory, never to your session scratchpad, and never anywhere else.
- If a script would help you read, keep it to plain reading of files with at most 2 processors.
- If something blocks you or prompts unexpectedly, stop and say so in your final message.
