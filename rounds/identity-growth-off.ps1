# The growth build's card identity sitting, part 1 (D241, logbook/specs/r62gr-prereg/predictions.md): with the climb
# off (T = 0), the growth build must be the pace build on the card, bit for bit. It runs the pace sitting's part 2 worlds
# at group 32, seed 6201 founded four times for 600 s: r62gof-kg on the kill-meal screen's world (rounds/env-r62kg.ps1),
# -t3 at the pace factor 1, and -k3 and -k3n at 3 (rounds/env-r62p-*.ps1). Each world is compared with
# scratch/pace-build/identity-on/r62pon-<world>-g32, the same world on the pace build, by scripts/reads/gpu-accept.py.
# On logical 22 to 31 at 10 threads, as growth group 1 runs, against the pace sitting's 14 threads on 16 to 29, so the
# comparison also tests that a card group is the same at any thread count. Runs under scratch/growth-identity/off.
# Run only on the coordinator's card go, from scratch/wt-growth's clean tree with its Release farm built, with that
# worktree as the working directory. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-growth"
$worlds = @(@('kg', 'env-r62kg.ps1'), @('t3', 'env-r62p-t3.ps1'), @('k3', 'env-r62p-k3.ps1'), @('k3n', 'env-r62p-k3n.ps1'))
$names = ($worlds | ForEach-Object { "r62gof-$($_[0])-g32" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\growth-identity\off" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62gof32' -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group 32 -Transport -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 -RunsRoot "$R\scratch\growth-identity\off" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
