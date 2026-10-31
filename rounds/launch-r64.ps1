# Phase 1 of D264 (logbook/specs/r64-prereg/predictions.md), on the coordinator's go only, after the identity check
# (rounds/identity-r64.ps1) has matched, from scratch/wt-phase1 at the pre-registration's commit with its own Release farm.
# One card group of five worlds founded for 24,000 s on seed 6202: arms A to E (rounds/env-r64-a.ps1 to -e.ps1). As
# launch-r63h.ps1: group 32, the snow's transport on the card, the heat stop on (D258), checkpoints every 1,500 s, a 600 s
# wait for run.json, logical 8 to 15 at 8 threads; the nose lane's nursery run is allowed beside it for the busy check
# alone. Start it detached and hidden, with scratch/wt-phase1 as the working directory. PowerShell's names ignore case:
# nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-phase1"
$arms = @('a', 'b', 'c', 'd', 'e')
$seed = '6202'
$names = @(); $launchers = @(); $armSeeds = @()
foreach ($a in $arms) { $names += "r64-$a-$seed"; $launchers += "$b\rounds\env-r64-$a.ps1"; $armSeeds += $seed }
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r64 -Arms ($names -join ',') -Launcher ($launchers -join ',') `
    -Seeds ($armSeeds -join ',') -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
