# D219-D221's contact-eating screen (logbook/specs/r62ce-prereg/predictions.md), on mouth-point in scratch/wt-mouthpoint:
# one card group of three worlds founded for 24,000 s, round 62's world at a 3 m reach and a 500 s half-life with
# contact eating on (rounds/env-r62ce.ps1), on seeds 6201, 6202 and 6203. The controls, r62-h6201 to r62-h6203, are
# round 62's and are not run again. As launch-r62.ps1: the snow's transport on the card, group 32, cores 16 to 29 at
# 14 threads, the heat stop on, checkpoints every 1,500 s, a 600 s wait for run.json. Start it detached and hidden
# (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$arms = @(
    @{ name = 'r62ce-6201'; launcher = 'env-r62ce.ps1'; seed = '6201' },
    @{ name = 'r62ce-6202'; launcher = 'env-r62ce.ps1'; seed = '6202' },
    @{ name = 'r62ce-6203'; launcher = 'env-r62ce.ps1'; seed = '6203' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62ce -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 600 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'