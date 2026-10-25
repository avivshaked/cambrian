# D227's deeper-wound screen (logbook/specs/r62bd-prereg/predictions.md), on mouth-point in scratch/wt-mouthpoint: one
# card group of six worlds founded for 24,000 s, r62cs's 500 s arm with a mouth's blow at 2, 4 and 16 times its damage
# and the intake scale at 16 (rounds/env-r62bd-x<scale>-i16.ps1), on seeds 6201 and 6202. The controls, r62cs-w500,
# r62ce and r62-h on the same seeds, are not run again. As launch-r62cs.ps1: the snow's transport on the card, group
# 32, cores 16 to 29 at 14 threads, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json. Start it
# detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on the
# coordinator's go. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$arms = foreach ($k in 2, 4, 16) { foreach ($seed in 6201, 6202) {
    @{ name = "r62bd-x$k-$seed"; launcher = "env-r62bd-x$k-i16.ps1"; seed = "$seed" } } }
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62bd -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
