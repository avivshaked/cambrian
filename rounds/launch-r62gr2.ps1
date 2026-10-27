# D241's growth screen, group 2 (logbook/specs/r62gr2-prereg/predictions.md), from scratch/wt-growth with the
# pre-registration's commit merged in: one card group of four worlds for 24,000 s on rounds/env-r62p-t3.ps1's world with
# the climb to adulthood and the juvenile-strength switch off. On seed 6201, T = 60 s with the growth-reserve gene
# starting at 1 s (r62gr2-g1) and 60 s (r62gr2-g60); the start of 30 s is group 1's r62gr-t60-6201 and is not run
# again. On seed 6202, T = 60 s and 120 s with the start at 30 s (r62gr-t60-6202, r62gr-t120-6202). As
# launch-r62gr1.ps1: the snow's transport on the card, group 32, the heat stop on, checkpoints every 1,500 s, a 600 s
# wait for run.json, and cores 22 to 31 at 10 threads, so logical 16 to 21 hold the theatre's one Unity beside it (the
# coordinator's ruling of 2026-10-27). Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a
# tool's task, from a clean tree, on the coordinator's go, with scratch/wt-growth as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: branch growth with the pre-registration's commit merged in, clean, with its Release farm built.
$b = "$R\scratch\wt-growth"
$names = 'r62gr2-g1-6201,r62gr2-g60-6201,r62gr-t60-6202,r62gr-t120-6202'
$launchers = "$b\rounds\env-r62gr2-g1.ps1,$b\rounds\env-r62gr2-g60.ps1,$b\rounds\env-r62gr-t60.ps1,$b\rounds\env-r62gr-t120.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62gr2 -Arms $names -Launcher $launchers -Seeds '6201,6201,6202,6202' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
