# Build spec: the scripts' verdict contracts (Astra review F4 and F5, 2026-09-12)

Read `CLAUDE.md`'s "Commands" section (the scorer paragraph and the digest paragraph), then
`scripts/compare-det.py`, `scripts/digest-diff.py` (the contract to copy: a failure exit on
disagreement), `scripts/clade-score.ps1` (the verdict lines around 360–470 and its fixtures
under `scripts/tests/clade-score/`), `scripts/tests/positions/run-tests.ps1` (line ~179) and
`scripts/launch-queue.ps1`.

Rules: edit only under `scripts/` in the main tree
(`D:\Projects\experiments\evolution-simulator`); scratch under
`D:\Projects\experiments\evolution-simulator\scratch\script-contracts\` only, nothing under the
session scratchpad or TEMP; no commit; no Unity; no `.md` files; do not touch `runs/`,
`unity*/`, `src/` or any running worker; never sleep, poll or wait. Every `.ps1` keeps its
UTF-8 BOM. Run each script's own fixture suite after touching it.

## 1. `compare-det.py` exits on what it finds

Today it prints a difference and exits 0; it compares only the samples both runs have, and it
takes the first run directory it globs. Change it so that:

- a difference exits 1 (message unchanged);
- no stats file, no shared samples, or a field missing from one side exits 2 and says which;
- unequal coverage (samples in one run and not the other, or different last times) is
  reported on its own line and exits 3 unless `--allow-partial` is passed, in which case the
  line still prints and identity over the shared samples exits 0;
- more than one run directory under `runs/<arm>/` is refused with the list (exit 2) unless
  `--run <name>` names one.

Fixtures: `scripts/tests/compare-det/run-tests.ps1` with cases for identity, disagreement, no
shared samples, unequal coverage with and without the flag, a missing field, a missing arm and
an ambiguous arm; fixture files under `scripts/tests/compare-det/fixtures/`. Print ok/FAIL per
case and exit nonzero on any failure, as the positions suite does.

## 2. The scorer says what its PASS does not cover

The verdict line's `PASS`/`FAIL` tokens and their fixtures stay exactly as they are; the
reads scripts under `scripts/reads/` grep them. Append a qualifier segment to the verdict
line and to the standing line:

- **Duration**: `| duration <simulated> of <requested> s` from the manifest, with the token
  `SHORT` when the simulated seconds are below 30,000 (the campaign's round length; the goal
  rule D063 as amended is read over a 30,000 s run and the scorer cannot tell a 10,000 s
  budget from a full one today). Say in the script's header that `SHORT` qualifies the
  reading and does not change it.
- **The floor**: read `config.json` beside the report for the floor's closing time (the
  `RunConfig` property `FloorClosesAfterSeconds`; find its JSON key in
  `src/Evosim.Core/RunConfigJson.cs`) and print `floor open` or `floor closes at <n> s`.
  A config older than the key prints `floor unknown`.
- Name the reading: the verdict line's first token after the arm becomes `clade PASS` /
  `clade FAIL` **only if** no script under `scripts/reads/` or `scripts/tests/` matches on
  `: PASS` — grep first; if any does, keep the token and put `clade` in the segment instead
  (`| reading: absorptive clade only; producer clause not decided`). Report which you did.

Fixtures: add the review's case (a passing fixture with a manifest of
`{"status":"ended","reason":"budget","requestedSeconds":10000,"simulatedSeconds":10000}`)
and assert `SHORT` appears; the existing 39 assertions still pass.

## 3. The positions suite under Windows PowerShell 5.1

Line ~179 runs the reader against a missing arm and 5.1 turns its stderr into a terminating
`NativeCommandError` before the exit code is read. Make the expected-failure calls robust in
both editions (a local `$ErrorActionPreference = 'Continue'` around the native call, or
`cmd /c` with `2>&1`, whichever keeps `$LASTEXITCODE`), and run the suite under both
`pwsh` and `powershell` to prove it.

## 4. The launcher records the pre-registration

`launch-queue.ps1` gains `-Prereg <path relative to the repo>` (optional). When given: refuse
to launch unless the file is tracked and clean (`git status --porcelain -- <path>` empty) and
has a commit (`git log -1 --format=%H -- <path>`); print the commit in the queue's launch
line; and after each seed's launch write `runs/<arm>/prereg.json` (create the directory if
the harness has not yet) with `{ "file", "commit", "launchedAt" }`. Nothing else changes; a
queue without the flag behaves as today.

## Final message

Tables: files changed; each new exit code and the fixture that proves it; the scorer's new
segment with an example line from a real run (`runs/r36-s1` is ended) and the SHORT fixture's
line; which `: PASS` grep result you found and what you did about the token; the positions
suite's result under both editions; the launcher's new flag exercised once with `-WhatIf`
against a tracked file (do not launch anything).
