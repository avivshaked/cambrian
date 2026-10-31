# r64's step 2 (logbook/specs/r64-prereg/step2.md), on the coordinator's go only, from scratch/wt-phase1 at the step's
# commit with its own Release farm. One card group of two worlds: r64-d-6202 and r64-a-6202, each resumed from its own
# checkpoint at 24,000 s for 24,000 s more, to 48,000 s. As launch-r64.ps1: group 32, the snow's transport on the card,
# the heat stop on (D258), checkpoints every 1,500 s, a 600 s wait for run.json, logical 8 to 15 at 8 threads; the nose
# lane's nursery run is allowed beside it for the busy check alone. Start it detached and hidden, with
# scratch/wt-phase1 as the working directory. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-phase1"
$runsRoot = "$R\runs"
$src = @('r64-d-6202', 'r64-a-6202') | ForEach-Object { (Get-ChildItem "$runsRoot\$_" -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r64s2 -Arms 'r64s2-d-6202,r64s2-a-6202' -ResumeFrom ($src -join ',') `
    -At 24000 -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot $runsRoot -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll|nose-test-n1p|nose-test-n1r|n1r'
exit $LASTEXITCODE
