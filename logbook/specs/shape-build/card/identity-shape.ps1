# The shape build's G0 card sittings (D255, branch shape-build), in the pattern of scratch/nose-card/identity-nose.ps1:
# one card group of three worlds, seeds 6202, 6203 and 6204, founded for 600 s at group -Group with one set, -Set:
#   off     rounds/env-r62cf.ps1, every new switch off: compared bit for bit with the prefix of runs/r62cf-<seed>;
#   fin     rounds/env-r62cf-fin.ps1, D229's fin law on and no family in the shape list: the pace gate's baseline;
#   family  rounds/env-r62cf-family.ps1, the fin law on and the family listed: 32 against 64, and 32 timed against fin.
# -Tag names a repeat of a set at one group (the timing read's second baseline). Same cores, threads, transport and
# checkpoint stride as the nose's sittings, on logical 22-31. Runs under scratch/shape-build/card/sit/. Run only on the
# coordinator's card go, from scratch/wt-shape-build's clean tree with its Release farm built, one group at a time.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet('off', 'fin', 'family')][string]$Set = 'off', [ValidateSet(32, 64)][int]$Group = 32, [string]$Tag = '')
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-shape-build"
$launcher = @{ off = 'env-r62cf.ps1'; fin = 'env-r62cf-fin.ps1'; family = 'env-r62cf-family.ps1' }[$Set]
$label = "$Set-g$Group$Tag"
$seeds = @('6202', '6203', '6204')
$names = ($seeds | ForEach-Object { "shapeid-$label-$_" }) -join ','
$launchers = ($seeds | ForEach-Object { "$b\rounds\$launcher" }) -join ','
$root = "$R\scratch\shape-build\card\sit\$label"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "shapeid-$label" -Arms $names -Launcher $launchers `
    -Seeds ($seeds -join ',') -Seconds 600 -Group $Group -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 `
    -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
