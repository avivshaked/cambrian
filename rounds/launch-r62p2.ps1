# D239's pace screen, group 2 (logbook/specs/r62p2-prereg/predictions.md), from scratch/wt-pace with the
# pre-registration's commit merged in: one card group of three worlds founded for 24,000 s on seed 6201, rounds/env-r62kg.ps1's world with
# a newborn's reserve at 0.5: the work price at a ninth (r62p-w9), the pace factor at 3 with the same price (r62p-k3w9),
# and the thrust arm pushed to 16 times at a sixteenth of the price (r62p-t16). The controls, group 1's worlds,
# r62cs-w500 and r62kg on the same seed, are not run in this group. As launch-r62p1.ps1: the snow's transport on the
# card, group 32, cores 16 to 29 at 14 threads, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json.
# Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on
# the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: branch pace with the pre-registration's commit merged in, clean, with its Release farm built.
$b = "$R\scratch\wt-pace"
$names = 'r62p-w9-6201,r62p-k3w9-6201,r62p-t16-6201'
$launchers = "$b\rounds\env-r62p-w9.ps1,$b\rounds\env-r62p-k3w9.ps1,$b\rounds\env-r62p-t16.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62p2 -Arms $names -Launcher $launchers -Seeds '6201,6201,6201' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
