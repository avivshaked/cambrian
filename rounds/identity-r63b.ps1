# The r63b build's pace check and card identity sitting (main, fat and bloom merged): one card group of three worlds,
# seeds 6202, 6203 and 6204, founded for 600 s at group -Group with one set of settings, -Set:
#   off  rounds/env-r62cf.ps1, every new switch off: compared bit for bit with the prefix of runs/r62cf-<seed>, and its
#        wall with that run's first 600 s, for the switch-off pace;
#   c    rounds/env-r63b-c.ps1, the bloom's cycles and far smell with D245's sense: at 32 against 64, and its wall
#        against off's, for bloom's cost;
#   d    rounds/env-r63b-d.ps1, c with fat and D247's cap at 300 s: at 32 against 64, and against c's wall.
# Same cores, mask, threads, transport and checkpoint stride as launch-r62cf.ps1. Runs under scratch/r63b-hold/. Run
# only on the coordinator's card go, from scratch/wt-r63b's clean tree with its Release farm built, one group at a time.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet('off', 'c', 'd')][string]$Set = 'off', [ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r63b"
$env = @{ off = 'env-r62cf.ps1'; c = 'env-r63b-c.ps1'; d = 'env-r63b-d.ps1' }[$Set]
$seeds = @('6202', '6203', '6204')
$names = ($seeds | ForEach-Object { "r63bid-$Set-g$Group-$_" }) -join ','
$launchers = ($seeds | ForEach-Object { "$b\rounds\$env" }) -join ','
$root = "$R\scratch\r63b-hold\$Set-g$Group"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r63bid-$Set-g$Group" -Arms $names -Launcher $launchers `
    -Seeds ($seeds -join ',') -Seconds 600 -Group $Group -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 `
    -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
exit $LASTEXITCODE