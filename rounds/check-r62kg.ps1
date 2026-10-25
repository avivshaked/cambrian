# D232's check before the screen: does the gut hand the meal over, and where does the mouth's cap bind? Seed 6201
# founded once for 4,500 s on rounds/env-r62kg.ps1's world, in a card group at group 32. Read with
# scripts/reads/r62g-read.py and r62-health.py at 4,500 s, and the gut's columns (gutJoules, killsSwallowed,
# gutSwallowedJoules, gutDigested, gutCapCut) from the stats. The cores, mask and threads are the round's. Runs under
# scratch\contact-eating\check-kg. Run only on the coordinator's go, from a clean tree. PowerShell's names ignore
# case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-killgut"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\check-kg" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62kgck' -Arms 'r62kgck-6201' `
    -Launcher "$b\rounds\env-r62kg.ps1" -Seeds '6201' `
    -Seconds 4500 -Group 32 -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 90 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\contact-eating\check-kg" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
