# The nursery's design folder

Made 2026-10-02 by session evolution-simulator-a8, which is building the general nursery
(nursery v2, D148), at the owner's request to prepare a folder for Claude Design, in which the owner will
open a new session and prepare it.

This folder is tracked in git, beside the theatre's design work in `design/canvas/`. Commit what
the design session makes here. Claude Design's own runtime (`support.js`) and handoff archives
(`*.zip`) stay out of git, as they do for the theatre (`.gitignore`, `LICENSE-DOCS`).

## What the building session needs to know from here

- Put the design's files in this folder, and a short `HANDOFF.md` here saying what they are and
  which one is current.
- The building session reads this folder by absolute path:
  `D:\Projects\experiments\evolution-simulator\design\nursery\`.

## Context for the design session

- The build spec, which says what nursery v2 is and what each step builds:
  `D:\Projects\experiments\evolution-simulator\logbook\specs\nursery-general-build.md`
- The ruling: D148 in `D:\Projects\experiments\evolution-simulator\DECISIONS.md`.
- What a nursery run writes into its folder after step 1: `run.json` (how the run was made: the
  arguments, every resolved option, the world config with its hash, the commit and tree hashes,
  seeds, salts and threads, and how it ended), `timing.jsonl` (wall time, one row per generation),
  and the result rows `tank-generations.jsonl`, `tank-best.jsonl`, `tank-crossovers.jsonl` and the
  exam's `tank-exam.jsonl`.
- A real run to look at, read-only: `D:\Projects\experiments\evolution-simulator\scratch\nursery\pilot-deep-brain2\`.
- A dashboard for nursery runs was proposed by another session and is paused until the general
  nursery's first run has written `run.json` and `timing.jsonl`:
  `D:\Projects\experiments\evolution-simulator\proposals\propose-nursery-dashboard.md`.

The project's rules hold here as everywhere in the repository: nothing is written outside it,
nothing leaves the machine without the owner's approval, and prose follows `STYLE.md`.
