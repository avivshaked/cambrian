# D232's kill-meal screen (logbook/specs/r62kg-prereg/predictions.md), from main after integration branch 1 lands, in scratch/wt-r62kg: one card
# group of two worlds founded for 24,000 s, r62cs's 500 s arm with a kill swallowed whole into the killer's gut and
# the intake scale at 16 (rounds/env-r62kg.ps1), on seeds 6201 and 6202. The controls, r62cs-w500, r62bd-x2 and r62-h
# on the same seeds, are not run again. As launch-r62bd.ps1: the snow's transport on the card, group 32, cores 16 to
# 29 at 14 threads, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json. Start it detached and
# hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: main at the pre-registration's commit, after integration branch 1 has landed, with its Release farm built.
$b = "$R\scratch\wt-r62kg"
$names = 'r62kg-6201,r62kg-6202'
$launchers = "$b\rounds\env-r62kg.ps1,$b\rounds\env-r62kg.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62kg -Arms $names -Launcher $launchers -Seeds '6201,6202' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
