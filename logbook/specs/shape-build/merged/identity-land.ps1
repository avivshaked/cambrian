# G4's landing, step 4: the merged tree's card sitting (scratch/shape-build/g4-landing-plan.md), on scratch/wt-shape-land's
# clean tree with its Release farm built, one group at a time, on the coordinator's go:
#   -Set off    rounds/env-r63c-b.ps1, every new switch off, seeds 6202 and 6203: bit for bit with r63c-b's prefix
#   -Set r62cf  rounds/env-r62cf.ps1, every new switch off, seeds 6202, 6203 and 6204: bit for bit with r62cf's prefix
#   -Set on     rounds/env-r63s-on.ps1, G5's arm "on", seeds 6202 and 6203, at -Group 32 and 64: the same digests
# Founded for 600 s, as scratch/shape-build/card/identity-shape.ps1: the snow's transport on the card, checkpoints every
# 1,500 s, 10 threads on 22-31, the heat stop on. Runs under scratch/shape-build/land-sit/<label>/.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
# -AllowBeside admits e4's N1 nursery search (CPU only, cores 24-31), which the coordinator placed beside the sitting;
# card-group still refuses it if it has the card's driver loaded.
param([ValidateSet('off', 'r62cf', 'on')][string]$Set = 'off', [ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-shape-land"
$launcher = @{ off = 'env-r63c-b.ps1'; r62cf = 'env-r62cf.ps1'; on = 'env-r63s-on.ps1' }[$Set]
$seeds = if ($Set -eq 'r62cf') { @('6202', '6203', '6204') } else { @('6202', '6203') }
$label = "$Set-g$Group"
$names = ($seeds | ForEach-Object { "landid-$label-$_" }) -join ','
$launchers = ($seeds | ForEach-Object { "$b\rounds\$launcher" }) -join ','
$root = "$R\scratch\shape-build\land-sit\$label"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "landid-$label" -Arms $names -Launcher $launchers `
    -Seeds ($seeds -join ',') -Seconds 600 -Group $Group -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 `
    -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 -AllowBeside 'nose-test-n1-z1in64' `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
