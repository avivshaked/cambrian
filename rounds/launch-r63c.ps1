# The bloom rerun (logbook/specs/r63c-prereg/predictions.md, D256), on the coordinator's go only, from scratch/wt-r63b at the
# pre-registration's final commit with its own Release farm. One card group of four worlds founded for 24,000 s:
# arms B and C (rounds/env-r63c-b.ps1 and -c.ps1) on seeds 6202 and 6203; r63b's A and r62cf are the controls. As launch-r62cf.ps1: the snow's transport on
# the card, group 32, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json, cores 22 to 31 at 10
# threads. Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, with
# scratch/wt-r63b as the working directory. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r63b"
$arms = @('b', 'c')
$seeds = @('6202', '6203')
$names = @(); $launchers = @(); $armSeeds = @()
foreach ($a in $arms) { foreach ($sd in $seeds) { $names += "r63c-$a-$sd"; $launchers += "$b\rounds\env-r63c-$a.ps1"; $armSeeds += $sd } }
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r63c -Arms ($names -join ',') -Launcher ($launchers -join ',') `
    -Seeds ($armSeeds -join ',') -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'