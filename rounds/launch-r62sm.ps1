# The structural mutation screen (logbook/specs/r62sm-prereg/predictions.md), from scratch/wt-struct with the
# pre-registration's commit merged in and the build's identity sitting passed: one card group of four worlds founded for
# 24,000 s on seed 6201, rounds/env-r62p-t16.ps1's world with growth as a climb in 60 s and the growth-reserve gene
# starting at 30 s, with every structural mutation chance at 1, 0.5, 0.25 and 0.1 times today's (r62sm-s100, -s50,
# -s25, -s10). As launch-r62gr2.ps1: the snow's transport on the card, group 32, the heat stop on, checkpoints every
# 1,500 s, a 600 s wait for run.json, and cores 22 to 31 at 10 threads, so logical 16 to 21 hold the theatre's one
# Unity beside it. Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, from a
# clean tree, on the coordinator's go, with scratch/wt-struct as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-struct"
$names = 'r62sm-s100-6201,r62sm-s50-6201,r62sm-s25-6201,r62sm-s10-6201'
$launchers = "$b\rounds\env-r62sm-s100.ps1,$b\rounds\env-r62sm-s50.ps1,$b\rounds\env-r62sm-s25.ps1,$b\rounds\env-r62sm-s10.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62sm -Arms $names -Launcher $launchers -Seeds '6201,6201,6201,6201' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
