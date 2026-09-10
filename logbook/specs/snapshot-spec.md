# Build spec: pictures from the theatre (owner: "build these", 2026-09-10)

Read `CLAUDE.md` (the theatre paragraph, the Play-mode identity check that landed today, the rule
that anything under `Assets/Evosim` is simulation source and the theatre lives beside it), then
`Assets/Theatre/Editor/TheatreIdentityCheck.cs` (`RunInPlayMode`, `Drive`, `Finish`, the
`SessionState` hand-over across the domain reload), `TheatreRunner.cs` (the camera, `Rate`,
`FrameBudgetSeconds`, seek), `TheatrePalette.cs` (guild colours), `TheatreSceneBuilder.cs`, and
`logbook/0083` and `0085`.

Rules: edit only under `unity/Assets/Theatre/` and `CLAUDE.md`; nothing under `unity/Assets/Evosim`
or `src/` (another agent is editing those in a worktree; your build must not move `simHash`);
nothing written outside the repository (pictures go under `scratch/`); no commit; never poll,
sleep or wait; never run Unity against `unity/` (the owner's Editor has it open and is using the
theatre); test on `unity-w6` only, refreshed first with `./scripts/new-worker.ps1 -Workers @(6)`
from PowerShell (it exits 1 on success; read its `refreshed` line); `unity-w7` is running an arm
and must not be touched; doc-comment register: why, with the incident; no em-dashes.

## Why

The owner opened round 33 in the theatre and saw in a minute what thirty-three rounds of tables
could not show (logbook/0083). The agent reading the rounds has no eyes: it cannot open the
Editor. This entry point gives it the same view as a file it can read.

## 1. `Evosim.Theatre.EditorTools.TheatreSnapshot.Run`

A batch entry in the shape of `RunInPlayMode` (enter Play mode, hand over across the reload in
`SessionState`, drive from `EditorApplication.update`, quit the Editor with 0 or 1, wall-clock
cap). Environment:

- `EVOSIM_THEATRE_RUN`: the run or arm directory, as today.
- `EVOSIM_THEATRE_SNAP_TIMES`: comma-separated simulated seconds, e.g. `300,600`. The runner
  steps (Mode B, the identity check on) to each in turn and renders at the first step at or after
  it. Times must be ascending.
- `EVOSIM_THEATRE_SNAP_VIEWS`: any of `side`, `end`, `top`, `iso`; default all four. `side` looks
  along z with the whole box in frame (length by depth); `end` looks along x (width by depth);
  `top` looks down (length by width); `iso` a three-quarter view from above one corner. Read the
  box's dimensions from the replay's config, never hard-coded.
- `EVOSIM_THEATRE_SNAP_OUT`: output directory, default `scratch/snaps/<arm>/`. File names
  `<arm>-t<seconds>-<view>.png`.
- `EVOSIM_THEATRE_SNAP_SIZE`: `WxH`, default `1600x900`.

Render with a dedicated camera to a `RenderTexture`, `ReadPixels` into a `Texture2D`,
`EncodeToPNG`, write with `File.WriteAllBytes`. Bodies coloured by the palette's guild colours;
the sea floor and the box's edges drawn so the frame reads at a glance. Burn a one-line label
into the top-left (arm, t, alive, view; a `GUI.Label` in `OnGUI` does not render into a
`RenderTexture`, so draw it with a `TextMesh`/`TextMeshPro` in front of the camera or composite
text pixels; say which you chose). Point sizes: a body should be visible at 1600 px across 20 m,
so if the mesh at scale is under 3 px, draw a screen-space marker (a billboard quad) at a minimum
size and say so in the remark.

Launch shape (`-batchmode` but not `-nographics`, since a graphics device is needed; `-quit` must be
absent, the entry quits):

```powershell
$env:EVOSIM_THEATRE_RUN = "$PWD/runs/r35tsmoke3"
$env:EVOSIM_THEATRE_SNAP_TIMES = '300,600'
Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', "$PWD/unity-w6", '-batchmode',
    '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreSnapshot.Run',
    '-logFile', "$PWD/scratch/logs/theatre-snap.log")
```

Also a menu item `Evosim/Theatre/Snapshot Now` that renders the four views of the currently
playing theatre to the same directory, for the owner.

## 2. `scripts/theatre-snap.ps1`

A launcher wrapping the above: `./scripts/theatre-snap.ps1 r35tsmoke3 -At 300,600 [-Worker 6]
[-Views side,top]`, UTF-8 BOM, prose comments, prints the output files. It must refuse `-Worker`
values whose `unity-wN` has a Unity process attached (read the CLAUDE.md gotcha on two processes on
one worker; a check on `Get-Process Unity` with the project path in the command line is enough).

## 3. Test

Run it against `runs/r35tsmoke3` at 300 and 600 on `unity-w6`, all four views. Read the PNGs
yourself (the Read tool shows images) and say what is in them: the box, the bodies, the label,
whether bodies are visible at that size. Fix what is wrong and rerun. Report the file list with
sizes and the log's last lines.

## 4. Record

`CLAUDE.md`'s theatre paragraph: two sentences on the entry and the launcher. Nothing else.

## Report

The entry point's diff hunk, the launcher, the test's file list, what you saw in the pictures,
anything that did not fit.
