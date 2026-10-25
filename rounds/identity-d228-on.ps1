# The D228-D229 card identity round, part 2 (logbook/specs/d228-d229-build-plan.md): each new switch on, at group
# -Group (32, then 64 as a second run). Seed 6201 founded four times for 600 s on rounds/env-r62cs-w1500.ps1's world:
# r62fson-fin (D229's fin law), -mirror (D228's mirrored joint), -true (contact on the true shape) and -all (all three).
# Each world at 32 is compared with itself at 64. Same cores, mask, threads and transport as D227's identity. Runs
# under scratch/fin-shape/identity-on. Run only on the coordinator's go, one group size at a time. PowerShell's names
# ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-finshape"
$worlds = @(@('fin', 'env-d228-fin.ps1'), @('mirror', 'env-d228-mirror.ps1'), @('true', 'env-d228-true.ps1'), @('all', 'env-d228-all.ps1'))
$names = ($worlds | ForEach-Object { "r62fson-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\fin-shape\identity-on" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62fson$Group" -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\fin-shape\identity-on" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
