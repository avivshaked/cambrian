# Contact pairs per body, and per jointed body: the instrument round 41c runs with

*2026-09-19, 03:30. Written by the agent on the owner's "proceed" after round 41b was
stopped on a contact-pair explosion (HANDOFF; logbook/0107). The pairs tracked the jointed
count across every run on file and grew several-fold within a run at a flat jointed
count, and the report could say neither which bodies made them nor how many a body made,
because the harness sums the engine's contact report into one cumulative total.*

## 1. What exists

`Ecosystem.cs` (`unity/Assets/Evosim/Sim/`) receives the engine's contact report each
physics step and adds the pair count into `_contactPairs` and the floor pairs into
`_floorContactPairs`; `stats.jsonl` carries `contactPairs` cumulative and the table has
`contacts` and `floor con`. Nothing says how the pairs are shared out among bodies.

## 2. What is added

Three counters beside the existing two, summed the same way from the same report, all
cumulative over the run:

- `contactPairsJointed`: pairs in which at least one of the two bodies is jointed (has
  any articulation joint beyond its root).
- `contactPairsPersistent`: pairs that were also present in the previous physics step
  (the engine reports a pair's stay as well as its enter; if the report used cannot tell
  a stay from an enter, count pairs whose two bodies were in contact at the previous
  step from a set the callback keeps, and say so in the class remarks).
- `contactBodies`: the number of distinct living bodies that took part in any pair in the
  step, summed over steps (so per step it is the touching count).

`stats.jsonl` carries the three beside `contactPairs`. The table gains three columns
after `contacts`: `pairs/body` (the window's pairs over the window's physics steps over
the living count: pairs per body per step), `pairs jnt %` (the window's jointed-involved
pairs over the window's pairs, as a percentage; a dash when there were none), and
`stuck %` (the window's persistent pairs over the window's pairs, the same way). The
header gains nothing. `run.json`'s ending block carries the three totals.

## 3. What does not change

No trajectory: the counters read the report and write nothing back. The counters live
under `Assets/Evosim`, so `simHash` moves and every worker is refreshed before the
launch; the recorded runs replay under their own builds. No Core change. The
`RunConfig` guards are untouched because no tunable is added.

## 4. Acceptance

A 300 s smoke at dt 0.02 on round 41b's world with the joint priced (`rounds/launch-r41c.ps1
-Seconds 300 -Dt 0.02`): the three fields present on every stats row and monotone, the
three columns present in the table and readable by `analyse-arm.ps1 -ListColumns`, and
`pairs jnt %` between 0 and 100 on every row where `pairs/body` is not 0. Then the same
smoke's `contactPairs` equals the sum it always was (the existing counter untouched).
