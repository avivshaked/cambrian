# The pace build's card identity sitting, part 2 (D239, logbook/specs/r62p-prereg/predictions.md): the pace screen's
# first group's own worlds, at group -Group (32, then 64 as a second run). Seed 6201 founded four times for 600 s:
# r62pon-kg on the kill-meal screen's world at the pace factor 1 (rounds/env-r62kg.ps1), and the group's three worlds,
# -t3 at 1 and -k3 and -k3n at 3 (rounds/env-r62p-*.ps1), so one card process holds worlds at both factors, as the
# group will. Each world at 32 is compared with itself at 64 by scripts/reads/gpu-accept.py. -kg at 32 is also compared
# with scratch/fin-shape/identity-int1-on/r62i1on-kg-g32, the gut build's, with --new-columns
# mouthBlows,mouthBlowsFinishing. Same cores, mask, threads and transport as integration 1's identity. Runs under
# scratch/pace-build/identity-on. Run only on the coordinator's card go, from scratch/wt-pace's clean tree with its
# Release farm built, one group size at a time. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-pace"
$worlds = @(@('kg', 'env-r62kg.ps1'), @('t3', 'env-r62p-t3.ps1'), @('k3', 'env-r62p-k3.ps1'), @('k3n', 'env-r62p-k3n.ps1'))
$names = ($worlds | ForEach-Object { "r62pon-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\pace-build\identity-on" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62pon$Group" -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 -RunsRoot "$R\scratch\pace-build\identity-on" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
