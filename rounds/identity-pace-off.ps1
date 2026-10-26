# The pace build's card identity sitting, part 1 (D239, logbook/specs/r62p-prereg/predictions.md): integration branch
# 1's switch-off identity group run again on the pace build with the pace factor at its default of 1, at group 32, as
# rounds/identity-int1-off.ps1 ran it. Seed 6201 founded four times for 600 s, as r62poff-off, -x2, -x4 and -x16. Each
# is compared bit for bit with scratch/fin-shape/identity-int1-off/r62i1off-WORLD-g32 by
#   python -I scripts/reads/gpu-accept.py <int1 run> <pace run> --new-columns mouthBlows,mouthBlowsFinishing
# since the pace build writes the two mouth-blow columns under contact eating where integration 1 wrote them only with
# the bite damage off 1. Main between the two builds also holds D233's shape family and D236's curriculum, so a
# difference is first split by running main's own build. Same cores, mask, threads and transport as integration 1's.
# Runs under scratch/pace-build/identity-off. Run only on the coordinator's card go, from scratch/wt-pace's clean
# tree with its Release farm built. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-pace"
$worlds = @(@('off', 'env-r62cs-w1500.ps1'), @('x2', 'env-r62bd-x2-i16.ps1'), @('x4', 'env-r62bd-x4-i16.ps1'), @('x16', 'env-r62bd-x16-i16.ps1'))
$names = ($worlds | ForEach-Object { "r62poff-$($_[0])-g32" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\pace-build\identity-off" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62poff32' -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group 32 -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -WaitForManifestSeconds 600 -RunsRoot "$R\scratch\pace-build\identity-off" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
