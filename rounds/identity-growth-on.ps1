# The growth build's card identity sitting, part 2 (D241, logbook/specs/r62gr-prereg/predictions.md): growth group 1's
# own worlds, at group -Group (32, then 64 as a second run). Seed 6201 founded five times for 600 s: r62gon-kg on the
# kill-meal screen's world with the climb off (rounds/env-r62kg.ps1), and the group's four worlds, -t60, -t120, -t240
# and -t120s (rounds/env-r62gr-*.ps1), so one card process holds worlds with the climb off and on, as nothing in group 1
# does but a later group may. Each world at 32 is compared with itself at 64 by scripts/reads/gpu-accept.py, and -kg at
# 32 with part 1's scratch/growth-identity/off/r62gof-kg-g32. On logical 22 to 31 at 10 threads, as group 1 runs. Runs
# under scratch/growth-identity/on. Run only on the coordinator's card go, from scratch/wt-growth's clean tree with its
# Release farm built, with that worktree as the working directory, one group size at a time. PowerShell's names ignore
# case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-growth"
$worlds = @(@('kg', 'env-r62kg.ps1'), @('t60', 'env-r62gr-t60.ps1'), @('t120', 'env-r62gr-t120.ps1'),
    @('t240', 'env-r62gr-t240.ps1'), @('t120s', 'env-r62gr-t120s.ps1'))
$names = ($worlds | ForEach-Object { "r62gon-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\growth-identity\on" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62gon$Group" -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201,6201' -Seconds 600 -Group $Group -Transport -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 -RunsRoot "$R\scratch\growth-identity\on" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
