# The true-shape screen's switch-on sitting (logbook/specs/r64t-prereg/predictions.md, "Checked before launch", step 2): one
# mixed card group of r64t-t-6202 (rounds/env-r64t-true.ps1) and a spheres D on 6202 (rounds/env-r64-d.ps1) for 600 s, run at
# -Group 32 and again at -Group 64. Then:
#   python scripts/reads/gpu-accept.py <t at 32> <t at 64>                    (must exit 0)
#   python -I scripts/reads/compare-run-rows.py runs/r64-d-6202 <spheres D at 32>   (row for row against the recorded prefix)
# Under scratch/r64t-hold/<group>. Run only on the coordinator's card go, from scratch/wt-r64t's clean tree with its Release
# farm built. The thread count and the mask are the screen's; change them to the machine's ruling of the day.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r64t"
$root = "$R\scratch\r64t-hold\g$Group"
New-Item -ItemType Directory -Force $root | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r64tid-g$Group" -Arms 'r64tid-t-6202,r64tid-d-6202' `
    -Launcher "$b\rounds\env-r64t-true.ps1,$b\rounds\env-r64-d.ps1" -Seeds '6202,6202' -Seconds 600 -Group $Group -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
    -RunsRoot $root -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
exit $LASTEXITCODE
