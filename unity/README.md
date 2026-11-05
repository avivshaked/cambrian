# The Unity project

Unity is the project's viewer and film studio. The simulation itself runs without it, as a
.NET program (the [top-level README](../README.md) says how). This folder is the Unity
project: the viewer, called the theatre, under `Assets/Theatre/`, and the phenotype builder
and sandbox scene the project started with, under `Assets/Evosim/`. `Evosim.Core` comes in as
a local package (`Packages/manifest.json`), so there is one copy of the source.

## Requirements

| | |
|---|---|
| **OS** | Windows. Unity is cross-platform, but the scripts here are PowerShell |
| **Unity** | `6000.5.6f1`. Other Unity 6 versions will very likely work; the Editor offers to upgrade the project on first open |
| **Disk** | About 15 GB for the Editor |

## Installing Unity

1. Install [Unity Hub](https://unity.com/download) and sign in (a free Unity account is required).
2. **Activate a licence**: ⚙ → **Licenses** → **Add** → **Get a free personal license**.
   Without it the Editor installs but refuses to open projects, without saying why.
3. **Installs** → **Install Editor** → pick **`6000.5.6f1`** (under *Archive* if it is no longer
   in *Official releases*).
4. On the modules screen, take **Windows Build Support (IL2CPP)**, and untick **Microsoft
   Visual Studio Community** if you already have Visual Studio, which it would install again.

## The viewer

The theatre reads a run directory and shows the world in three dimensions. For runs made by the
.NET simulation it plays recorded windows: stretches of a run in which the simulation wrote down
every body's position. `Evosim.Farm.exe --film-window <run> <from> <to> <out>` records one from
the run's nearest checkpoint, and [`scripts/theatre-film.ps1`](../scripts/theatre-film.ps1) films
it. The theatre can also grow a world live from its seed and settings. Unity's runtime rounds
floating point differently from .NET, though, so a live world drifts away from the recorded
one. The viewer compares its counts with the run's at every sample, and says when they part.
[`Assets/Theatre/CLAUDE.md`](Assets/Theatre/CLAUDE.md) has the detail.

The theatre sits beside `Assets/Evosim/` rather than inside it, because a run's build hash
covers every script under `Assets/Evosim/`. Editing the viewer therefore never invalidates a
recording.

## The first physics tests

Before the project had its own solver it ran on Unity's `ArticulationBody`, and two checks from
that time still run. They are history: the simulation no longer uses Unity's physics.

**Spike 01** asked whether `ArticulationBody` could build, step and tear down hundreds of
creatures a minute. It could, with a wide margin; [`DESIGN.md`](../DESIGN.md) §11.1 has the
measurements, and [`spikes/01-articulation-body/`](../spikes/01-articulation-body/) the
project. From the repository root:

```powershell
$unity = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory |
         Sort-Object Name -Descending |
         ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
         Where-Object { Test-Path $_ } | Select-Object -First 1
$proj = Join-Path $PWD 'spikes\01-articulation-body'
$log  = Join-Path $PWD 'spike.log'

Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', $proj, '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Spike.EditorTools.SpikeEntry.Run', '-logFile', $log)

Get-Content (Join-Path $proj 'results\FINDINGS.md')
```

`results/FINDINGS.md` then reports six measurements, each marked PASS, and CSVs of the raw
data. Expect different numbers on other hardware (they were taken on an i9-13900K), but the
same shape: build and teardown well under 15 ms, and the cost per creature falling as more
creatures share the scene. If the mean-speed column collapses towards zero, the creatures fell
asleep and the timings mean nothing ([the spike's spec](../spikes/01-articulation-body/README.md), §M3).

**The Milestone 1 smoke test** builds twelve creatures, checks their geometry against the
phenotype, drives them, and asserts two conservation laws:

```powershell
$log = Join-Path $PWD 'smoke.log'
Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    '-projectPath', (Join-Path $PWD 'unity'), '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Evosim.Sim.EditorTools.Milestone1Smoke.Run', '-logFile', $log)
Get-Content $log | Select-String 'Milestone 1 smoke' -Context 0,140
```

The first run of either takes several minutes while Unity imports and compiles.

## Troubleshooting

| Symptom | Cause |
|---|---|
| Unity exits at once and writes no log | It was started with `& $unity ...`. Start it with `Start-Process`, as above |
| `error CS` in the log | A compile error: `Select-String -Path <log> -Pattern 'error CS'` lists them |
| The Editor opens but will not load the project | The licence is not activated (step 2 above) |
| "Corrupted Library Detected" on opening | A batch-mode run shared `Library/` with an open Editor. Never run `-batchmode` against a project the Editor has open. *Rebuild Library* fixes it |
| Unity Hub's command line fails with `Cannot find module '--headless'` | A known bug in Hub 3.14. Use the Hub's window instead |
