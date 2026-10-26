# D241's growth screen, group 1 (logbook/specs/r62gr-prereg/predictions.md), from scratch/wt-growth with the
# pre-registration's commit merged in and the growth build's card identity sitting passed: one card group of four
# worlds founded for 24,000 s on seed 6201, rounds/env-r62p-t3.ps1's world with growth as a climb to adulthood in 60,
# 120 and 240 world seconds (r62gr-t60, r62gr-t120, r62gr-t240), and in 120 s with the juvenile-strength switch on
# (r62gr-t120s). The controls, r62p-t3 and r62cs-w500 on the same seed, are not run again. As launch-r62p1.ps1: the
# snow's transport on the card, group 32, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json, and
# cores 22 to 31 at 10 threads, so logical 16 to 21 hold the theatre's one Unity beside it (the coordinator's ruling of
# 2026-10-26; a card group is bit-identical at any thread count). Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's
# task, from a clean tree, on the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: branch growth with the pre-registration's commit merged in, clean, with its Release farm built.
$b = "$R\scratch\wt-growth"
$names = 'r62gr-t60-6201,r62gr-t120-6201,r62gr-t240-6201,r62gr-t120s-6201'
$launchers = "$b\rounds\env-r62gr-t60.ps1,$b\rounds\env-r62gr-t120.ps1,$b\rounds\env-r62gr-t240.ps1,$b\rounds\env-r62gr-t120s.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62gr1 -Arms $names -Launcher $launchers -Seeds '6201,6201,6201,6201' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
