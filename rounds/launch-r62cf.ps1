# The confirmation round (logbook/specs/r62cf-prereg/predictions.md), on the owner's yes only, from scratch/wt-r62cf: a
# clean worktree at the pre-registration's final commit with its own Release farm. One card group of three worlds
# founded for 24,000 s on seeds 6202, 6203 and 6204 with rounds/env-r62cf.ps1's settings. As launch-r62sm.ps1: the
# snow's transport on the card, group 32, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json, and
# cores 22 to 31 at 10 threads, so logical 16 to 21 hold the theatre's one Unity beside it. Start it detached and hidden
# (Start-Process -WindowStyle Hidden), never inside a tool's task, on the coordinator's go, with scratch/wt-r62cf as the
# working directory. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r62cf"
$names = 'r62cf-6202,r62cf-6203,r62cf-6204'
$launchers = "$b\rounds\env-r62cf.ps1,$b\rounds\env-r62cf.ps1,$b\rounds\env-r62cf.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62cf -Arms $names -Launcher $launchers -Seeds '6202,6203,6204' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
