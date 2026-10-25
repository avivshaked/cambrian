# The D228-D229 card identity round, part 1 (logbook/specs/d228-d229-build-plan.md): D227's launch identity group
# (rounds/identity-r62bd-launch.ps1 on mouth-point) run again on the fin-shape build with every new switch off, at
# group 32: seed 6201 founded four times for 600 s, as r62fsoff-off, -x2, -x4 and -x16. Each is compared bit for bit
# with scratch/contact-eating/identity-bdl/r62bdl-WORLD-g32 (D227's build, 4e94b0e0), and its wall with that run's,
# for the switch-off pace (the machine quiet, nothing beside it). Same cores, mask, threads and transport as D227's.
# Runs under scratch/fin-shape/identity-off. Run only on the coordinator's go. PowerShell's names ignore case:
# nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-finshape"
$worlds = @(@('off', 'env-r62cs-w1500.ps1'), @('x2', 'env-r62bd-x2-i16.ps1'), @('x4', 'env-r62bd-x4-i16.ps1'), @('x16', 'env-r62bd-x16-i16.ps1'))
$names = ($worlds | ForEach-Object { "r62fsoff-$($_[0])-g32" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\fin-shape\identity-off" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62fsoff32' -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group 32 -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\fin-shape\identity-off" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
