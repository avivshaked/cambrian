# The shape screen (G5, logbook/specs/r63s-prereg/predictions.md), on the coordinator's go only, from scratch/wt-shape-land
# at the pre-registration's commit with its own Release farm. One card group of two worlds founded for 24,000 s: the arm
# "on" (rounds/env-r63s-on.ps1) on seeds 6202 and 6203; the arm "off" is r63c-b's own runs. As launch-r63c.ps1: the
# snow's transport on the card, group 32, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json. Cores
# 8 to 15 at 8 threads, off the cores of the nose lane's nursery searches (N0 on 16-23, N1 on 24-31), which -AllowBeside
# admits past the busy check while they run on the processor alone; card-group refuses one that loads the card's driver.
# Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task,
# with scratch/wt-shape-land as the working directory. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-shape-land"
$seeds = @('6202', '6203')
$names = @(); $launchers = @()
foreach ($sd in $seeds) { $names += "r63s-on-$sd"; $launchers += "$b\rounds\env-r63s-on.ps1" }
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r63s -Arms ($names -join ',') -Launcher ($launchers -join ',') `
    -Seeds ($seeds -join ',') -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n0|nose-test-n1-z1in64'
