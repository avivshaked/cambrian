# Integration branch 1's card identity round, part 2: each new switch on, at group -Group (32, then 64 as a second
# run). Seed 6201 founded four times for 600 s: r62i1on-atpoint (D228's level (b)), -gut (D232's kill gut) and -both,
# on rounds/env-r62cs-w1500.ps1's world, and -kg on the kill-meal screen's own world (rounds/env-r62kg.ps1), whose
# config hash the pre-registration names. Each world at 32 is compared with itself at 64. Same cores, mask, threads and
# transport as D227's identity. Runs under scratch/fin-shape/identity-int1-on. Run only on the coordinator's go, from a
# clean tree, one group size at a time. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-int1"
$worlds = @(@('atpoint', 'env-int1-atpoint.ps1'), @('gut', 'env-int1-gut.ps1'), @('both', 'env-int1-both.ps1'), @('kg', 'env-r62kg.ps1'))
$names = ($worlds | ForEach-Object { "r62i1on-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\fin-shape\identity-int1-on" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62i1on$Group" -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -WaitForManifestSeconds 600 -RunsRoot "$R\scratch\fin-shape\identity-int1-on" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
