# The true-shape screen (logbook/specs/r64t-prereg/predictions.md), on the coordinator's go only, after the identity check
# (rounds/identity-r64t.ps1) has matched, from scratch/wt-r64t at the pre-registration's commit with its own Release farm.
# One card group of three worlds founded for 24,000 s: r64t-t-6202 and r64t-t-6203 on rounds/env-r64t-true.ps1, and
# r64t-s-6203 on rounds/env-r64t-spheres.ps1 (the 6202 spheres world is r64-d-6202's own recording). As launch-r64.ps1:
# group 32, the snow's transport on the card, the heat stop on (D258), checkpoints every 1,500 s, a 600 s wait for run.json,
# logical 8 to 15 at 8 threads; the nose lane's nursery run is allowed beside it for the busy check alone. -Worktree names
# the worktree; the default is scratch/wt-r64t. Start it detached and hidden, with that worktree as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([string]$Worktree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r64t')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = $Worktree
$names = @('r64t-t-6202', 'r64t-t-6203', 'r64t-s-6203')
$seeds = @('6202', '6203', '6203')
$launchers = @("$b\rounds\env-r64t-true.ps1", "$b\rounds\env-r64t-true.ps1", "$b\rounds\env-r64t-spheres.ps1")
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r64t -Arms ($names -join ',') -Launcher ($launchers -join ',') `
    -Seeds ($seeds -join ',') -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
