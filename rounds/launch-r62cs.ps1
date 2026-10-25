# D224's overshoot screen (logbook/specs/r62cs-prereg/predictions.md), on mouth-point in scratch/wt-mouthpoint: one card
# group of six worlds founded for 24,000 s, r62ce's world with a living bite taking its share of the store, the remains'
# window at 60, 500 and 1,500 s (rounds/env-r62cs-w<window>.ps1), on seeds 6201 and 6202. The controls, r62ce-6201,
# r62ce-6202, r62-h6201 and r62-h6202, are not run again. As launch-r62ce.ps1: the snow's transport on the card, group
# 32, cores 16 to 29 at 14 threads, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json. Start it
# detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on the
# coordinator's go. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$arms = foreach ($w in 60, 500, 1500) { foreach ($seed in 6201, 6202) {
    @{ name = "r62cs-w$w-$seed"; launcher = "env-r62cs-w$w.ps1"; seed = "$seed" } } }
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62cs -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 720 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
