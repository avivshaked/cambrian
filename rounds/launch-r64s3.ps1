# r64's step 3 (logbook/specs/r64-prereg/step3.md), on the coordinator's go only, from scratch/wt-phase1 at the step's
# commit with its own Release farm. One card group of two copies of D, r64s3-d1-6202 and r64s3-d2-6202, both resumed
# from step 2's D checkpoint at 46,500 s for 1,500 s, to 48,000 s. As launch-r64s2.ps1: group 32, the snow's transport
# on the card, the heat stop on (D258), checkpoints every 1,500 s, a 600 s wait for run.json, logical 8 to 15 at 8
# threads. Start it detached and hidden, with scratch/wt-phase1 as the working directory. PowerShell's names ignore
# case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-phase1"
$runsRoot = "$R\runs"
$src = "$runsRoot\r64s2-d-6202\2026-10-30-233542-11de0613"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r64s3 -Arms 'r64s3-d1-6202,r64s3-d2-6202' -ResumeFrom $src `
    -At 46500 -Seconds 1500 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 120 -WaitForManifestSeconds 600 `
    -RunsRoot $runsRoot -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
exit $LASTEXITCODE
