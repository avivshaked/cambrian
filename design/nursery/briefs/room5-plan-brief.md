# Plan brief: re-stepping an episode in the nursery room (phase 3d, the tank)

Read only. Change no file, build nothing, run no test and start no process. Write nothing anywhere:
not in the repository, not in your session scratchpad, not in the Windows TEMP directory. Never `cd`.
Read by absolute path with your Read tool, and use `git -C` for history. A denied or blocked command is
final: do not reach the same end another way. Say so in your report.

**Never read anything under `scratch/wt-nursery-general`** (a run is writing there), `scratch/wt-joint`,
`scratch/wt-nursery-s6` or `scratch/wt-advect-fast`.

The code is in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-nursery-room` (branch
`nursery-room` at `1738587`). The room's build spec is
`D:\Projects\experiments\evolution-simulator\logbook\specs\nursery-room-build.md`. The design is under
`D:\Projects\experiments\evolution-simulator\design\nursery\`: `BRIEF.md` screen 4, `HANDOFF.md`
"Tank", and the mock `4-tank.html`. The reference run is
`D:\Projects\experiments\evolution-simulator\scratch\nursery\pilot-deep-brain2\`, made on an older build
before `run.json` and the tank-mates file existed. Its config is named in the worktree's
`rounds/nursery/room.json`.

## The goal

Phase 3's done-when is this: the tank screen re-steps an exam episode of `pilot-deep-brain2` on the
server, with the nursery's own code, and marks it exact against `tank-exam.jsonl`'s per-draw `per`.
Work out how, from the code. Plan; do not build.

## Answer, with file and line for every claim

1. **What one exam episode is in the nursery's code.** Name the function that swims it. What does it
   take: genomes, brain substitutions for a control, the episode seed, the tank index, tank-mates, the
   config, the overrides applied at launch? What does it produce, and how does that become `per`?
2. **Recovering an episode's inputs for `pilot-deep-brain2`.** It has no `tank-exam-draws.jsonl`. Can
   the tank-mates and seeds be recomputed exactly from what the run holds (`tank-exam-genomes.tsv`,
   `tank-exam.jsonl`, the config, the salts) with the current code's pure functions (`PlanTanks`,
   `SeedsFor`)? Which seed and salt did the pilot use? Where is that recorded? Is it the current code's
   default, or in the log? Is there any input that is not recoverable?
3. **Is the current code the pilot's code for an exam episode?** Step 5 found generations 0 to 3 of the
   search identical between the pilot's build and `41206ff` (see `logbook/specs/nursery-general-build.md`
   in the main checkout). Does anything in the exam path differ between the pilot's commit
   (`dc5dd6f`, the registry's reference entry) and the worktree's head? Use `git -C <worktree> diff
   dc5dd6f 1738587 -- src/Evosim.Nursery src/Evosim.Core src/Evosim.Dynamics`, read for the exam path.
   Name any difference that could move a bit.
4. **The stepper's shape.** The room needs, per step or per sampled step: each body's part poses, the
   brain's live values (senses in, hidden, outputs), energy, and events (carrion eaten, islands
   entered, deaths). Can these be read from the episode's objects without changing what the episode
   computes? Name a hook that reads only, or a copy of the loop. Explain why either keeps the result
   bit-identical to the nursery's own swim. How much data is one episode at, say, 10 samples per
   second? How long does an episode take to step on one thread? Use the run's `timing.jsonl` or log if
   they say.
5. **Exactness.** What is compared with `per`, and at what precision? Where could the room's run of
   the same code differ: thread count, a parallel loop inside the episode, float summation order, a
   cache, a static RNG? List each, with whether it bites.
6. **Load.** The machine's cap is 16 of 32 logical processors, and the room's launches are pinned to
   `0xFFFF0000`. How should a re-step be run and counted: a thread on the server, or a child process
   pinned like a launch? What should a re-step do when a timing read is declared?
7. **The client.** The mock is `4-tank.html`. Which parts need WebGL (three.js), and which can be SVG
   or canvas? Give a minimal first cut that meets done-when, and what follows it.

## Report

Your final message holds the plan:
- answers 1 to 7, each with its file:line evidence
- the risks, ranked
- a build order of small steps, each with its test, marking which steps need an episode to run
  (they wait for the machine) and which do not

No report file.
