# D270's card identity sittings (logbook/specs/smell-plume-build.md, "Tests"). Two parts, one card group each, one group size at a time.
#
#   -Part off  (-Group 32): with the carried leaf smell and the current sense off, the build founds r64's arms D and E
#              (rounds/env-r64-d.ps1, -e.ps1; two, since a card group needs two worlds) for 600 s on seed 6202, on r64's terms.
#              scripts/reads/compare-run-rows.py then compares each row for row with the prefix of its recorded run:
#                python -I scripts/reads/compare-run-rows.py runs/r64-d-6202 <the new d run>
#                python -I scripts/reads/compare-run-rows.py runs/r64-e-6202 <the new e run>
#              The new stats rows carry no leafPlume field with the plume off, so the comparison needs no allowance for one.
#   -Part on   (-Group 32, then 64 as a second run): the build founds arm R and arm O (rounds/env-d270-r.ps1, -o.ps1) for 600 s on
#              seed 6202. Each world at 32 is compared with itself at 64:
#                python scripts/reads/gpu-accept.py <r at 32> <r at 64>   (and the same for o)
#              which must exit 0. Their stats rows carry leafPlumeEmitted and leafPlumeDecayed in both runs.
#
# Under scratch/d270-hold/<part>. Run only on the coordinator's card go, from scratch/wt-smell's clean tree with its Release
# farm built. The thread count and the mask are r64's identity sitting's; change them to the machine's ruling of the day.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param(
    [ValidateSet('off', 'on')][string]$Part = 'off',
    [ValidateSet(32, 64)][int]$Group = 32
)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-smell"
$root = "$R\scratch\d270-hold\$Part"
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
New-Item -ItemType Directory -Force $root | Out-Null

if ($Part -eq 'off') {
    if ($Group -ne 32) { throw 'The switches-off sitting runs at group 32 only.' }
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name d270id-off -Arms 'd270id-d-6202,d270id-e-6202' `
        -Launcher "$b\rounds\env-r64-d.ps1,$b\rounds\env-r64-e.ps1" -Seeds '6202,6202' -Seconds 600 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
        -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
        -RunsRoot $root -Exe $exe -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
}
else {
    & pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "d270id-on$Group" -Arms "d270on-r-g$Group,d270on-o-g$Group" `
        -Launcher "$b\rounds\env-d270-r.ps1,$b\rounds\env-d270-o.ps1" -Seeds '6202,6202' -Seconds 600 -Group $Group -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
        -HeatStop -WallMinutes 45 -WaitForManifestSeconds 600 `
        -RunsRoot $root -Exe $exe -AllowBeside 'Assay\.Run\.dll|nose-test-n1p'
}
exit $LASTEXITCODE
