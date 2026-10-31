# The anchoring build's switch-off identity on the card (logbook/specs/anchor-brain-build.md, "After the build", item 2):
# this build, with EVOSIM_ANCHOR_BRAIN and the anchoring cell off, founds r64's arms D and E (rounds/env-r64-d.ps1,
# -e.ps1; two, since a card group needs two worlds) for 1,200 s on seed 6202, in one card group on r64's terms.
# scripts/reads/compare-run-rows.py then compares each row for row with the prefix of its recorded run, runs/r64-d-6202
# and runs/r64-e-6202. Under scratch/anchor-hold/. Run only on the coordinator's card go, from scratch/wt-anchor's clean
# tree with its Release farm built. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-anchor"
$root = "$R\scratch\anchor-hold\off"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name anchorid-off -Arms 'anchorid-d-6202,anchorid-e-6202' `
    -Launcher "$b\rounds\env-r64-d.ps1,$b\rounds\env-r64-e.ps1" -Seeds '6202,6202' -Seconds 1200 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 60 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
