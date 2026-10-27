# D239's pace screen, group 2, part a (logbook/specs/r62p2-prereg/predictions.md, "The machine"), from scratch/wt-pace
# with the pre-registration's commits merged in: one card group of the group's two worlds at the pace factor 1, founded
# for 24,000 s on seed 6201, rounds/env-r62kg.ps1's world with a newborn's reserve at 0.5: the work price at a ninth
# (r62p-w9), and the thrust arm pushed to 16 times at a sixteenth of the price (r62p-t16). The third world, r62p-k3w9,
# runs alone later (launch-r62p2b.ps1). The controls, group 1's worlds, r62cs-w500 and r62kg on the same seed, are not
# run again. As launch-r62p1.ps1, the snow's transport on the card, group 32, the heat stop on, checkpoints every
# 1,500 s, a 600 s wait for run.json, but on logical 22 to 31 at 10 threads, so 16 to 21 hold the theatre's one Unity
# beside it (the coordinator's ruling of 2026-10-27). Start it detached and hidden (Start-Process -WindowStyle Hidden),
# never inside a tool's task, from a clean tree, on the coordinator's go, with scratch/wt-pace as the working directory.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: branch pace with the pre-registration's commits merged in, clean, with its Release farm built.
$b = "$R\scratch\wt-pace"
$names = 'r62p-w9-6201,r62p-t16-6201'
$launchers = "$b\rounds\env-r62p-w9.ps1,$b\rounds\env-r62p-t16.ps1"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62p2a -Arms $names -Launcher $launchers -Seeds '6201,6201' `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
