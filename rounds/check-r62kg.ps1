# D232's check before the screen (logbook/specs/r62kg-prereg/predictions.md): does the gut hand the meal over, where
# does the mouth's cap bind, and, from its checkpoints, the gut's card identity (rounds/resume-r62kg.ps1). Seed 6201
# founded once for 6,000 s on rounds/env-r62kg.ps1's world, on the card at group 32, with a checkpoint every
# 1,500 s. One world, so scripts/card/card-run.ps1 (card-group.ps1 runs two or more), which founds the world on a
# one-second CPU preflight first and starts the card only if its config hash is the pre-registration's. Read with
# scripts/reads/r62g-read.py and r62-health.py at 4,500 s and 6,000 s, and the gut's columns (gutJoules,
# killsSwallowed, gutSwallowedJoules, gutDigested, gutCapCut) from the stats. The cores, mask and threads are the
# round's. Runs under scratch\contact-eating\check-kg. Run only on the coordinator's go, from a clean tree.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: main at the pre-registration's commit, with its Release farm built.
$b = "$R\scratch\wt-r62kg"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\check-kg" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-run.ps1" -Arm 'r62kgck-6201' -At 0 -Seconds 6000 `
    -Launcher "$b\rounds\env-r62kg.ps1" -Seed 6201 -ConfigHash '277612f7dc63322b' `
    -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 120 `
    -RunsRoot "$R\scratch\contact-eating\check-kg" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
