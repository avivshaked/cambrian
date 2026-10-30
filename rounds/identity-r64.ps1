# Phase 1 of D264's identity check (logbook/specs/r64-prereg/predictions.md): the new build with every D264 switch off
# founds r63h's arms D and E (rounds/env-r63h-d.ps1, -e.ps1; two, since a card group needs two worlds) for 600 s on seed
# 6202, in one card group on the r63h launcher's terms, and scripts/reads/compare-run-rows.py compares each row for row
# with the prefix of its recorded run, runs/r63h-d-6202 and runs/r63h-e-6202. Under scratch/r64-hold/.
# Run only on the coordinator's card go, from scratch/wt-phase1's clean tree with its Release farm built.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-phase1"
$root = "$R\scratch\r64-hold\off"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r64id-off -Arms 'r64id-d-6202,r64id-e-6202' `
    -Launcher "$b\rounds\env-r63h-d.ps1,$b\rounds\env-r63h-e.ps1" -Seeds '6202,6202' -Seconds 600 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
exit $LASTEXITCODE
