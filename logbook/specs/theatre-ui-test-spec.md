# Test plan: the theatre's interface (2026-09-12 night)

How the interface built from `theatre-ui-spec.md` is checked. Two instruments, one contract.

## 1. An end-to-end check in Play mode, the project's own way

The theatre already has end-to-end entries that run in batch mode and exit 0 or 1:
`TheatreIdentityCheck.RunInPlayMode` enters Play mode, lets the runner's own `Update` step a
recorded run, and prints a verdict per sample; `TheatreSnapshot.Run` replays a run and writes
labelled frames. The interface gets a third, `Evosim.Theatre.EditorTools.TheatreUiCheck.Run`,
under `unity/Assets/Theatre/Editor/`, launched the way the snapshot is (`-batchmode` without
`-quit`, without `-nographics`, `EVOSIM_THEATRE_RUN` naming a run directory), so it runs on
a worker from a script with no person at the Editor. Unity's Test Framework package would be
the other way (`-runTests -testPlatform PlayMode`); it is not in the manifest, the existing
entries are not written against it, and one harness style is easier to keep than two, so
the entry pattern stands. The check:

1. Loads the run, enters Play mode, waits for the first sample, and asserts every field of
   the identity strip and the census against the values `RunRecord`, `TheatreReplay` and
   `WorldCensus` hold (query the panel with `UQuery` by element name; compare text to the
   formatted value, thin space included).
2. Walks the states and asserts the classes and texts the stylesheet names for each:
   playing, paused (`Space`), seeking (`K` to a target, the target and ETA shown, then
   arrived), the provenance popover (`P`), a warning present (a run with `SamplesNote`),
   chrome hidden (`H`, every panel `display: none`), chrome back.
3. Selects a living creature through `CreatureIdMap` (the same call a click makes) and
   asserts the inspector's living state; selects an id that has died (from the lineage
   index) and asserts died-at, lived and children against a count the check makes itself
   from `lineage.jsonl`; asks for a selection on a run whose ids are unreliable and asserts
   the unavailable state.
4. Runs a second time with `EVOSIM_THEATRE_OVERRIDE=1` against a run recorded under another
   build (the caller names one; after the streams merge, `runs/r37-s1` is such a run) and
   asserts the cousin and mismatch states, with the identity line's coverage text.
5. Runs once in solo mode (`EVOSIM_THEATRE_GENOME`) and asserts the solo census and that
   the text `global neurons` appears nowhere.
6. Writes a screenshot per state into `scratch/snaps/ui/<run>/` with `ScreenCapture`, at
   the window's size and at 3840 by 2160 (set the game view's size the way the snapshot
   entry does), and prints one line per assertion, `ok` or `FAIL`, then the count, and
   exits 1 on any failure.

The fixture is a fresh smoke: the caller records one with the current world's launcher
(`./rounds/launch-r37b.ps1 -Seed 3 -Worker <w> -Dt 0.02 -Seconds 300 -Name uicheck`) on the
worker that will run the check, so the run always loads under the build that made it. Nothing
under `runs/` is committed.

## 2. Pictures the agent reads

`scripts/theatre-snap.ps1 <run> -At <t> -Chrome -Size 3840x2160` and at `1920x1080`, over
runs that reproduce the three reference frames (`design/reference-frames/README.md` names
them: the busy near-black crowd, the bright pale surface, the near-empty wide shot), in
every provenance state the check can produce. The agent reads them for spec §9's items 2
(distinguishable in greyscale), 3 (no reflow on a digit), 4 (no reflow on a warning), 5
(the centre clear), 7 (legible over the bright frame, the hard one), and says in the entry
what was seen. A picture the agent cannot read as the design intends is a failure whatever
the check printed.

## 3. The contract that must not move

`simHash` before and after, read from a smoke's `run.json` recorded on the worker: identical,
or something landed under `Assets/Evosim`. The identity check (`TheatreIdentityCheck`,
both entries) and the snapshot entry still pass on the same run. `H` hides everything and
the default snapshot carries no chrome, so `logbook/images/` stays comparable.

## Where the results go

The check's log under `scratch/logs/theatre-ui-check.log`, the pictures under
`scratch/snaps/ui/`, and a logbook entry that shows the interface over the three frames
with the agent's reading; the pictures the entry cites move to `logbook/images/`.
