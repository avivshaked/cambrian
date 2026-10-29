# r63d's step 2 (logbook/specs/r63d-prereg/predictions.md, D259), on the coordinator's go only, after step 1
# (launch-r63d-a.ps1) has ended, from scratch/wt-r63b at the pre-registration's final commit with its own Release farm.
# One card group of three worlds: r63c-b-6202, r63c-b-6203 and step 1's r63d-b-6204, each resumed from its own
# checkpoint at 24,000 s for 48,000 s more, to 72,000 s. Group 32, the snow's transport on the card, the heat stop on
# (D258), checkpoints every 1,500 s, a 600 s wait for run.json, logical 8 to 15 at 8 threads (the nose's nursery tests
# hold 16 to 31). Start it detached and hidden (Start-Process -WindowStyle Hidden), never inside a tool's task, with
# scratch/wt-r63b as the working directory.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r63b"
$src = @('r63c-b-6202', 'r63c-b-6203', 'r63d-b-6204') | ForEach-Object { (Get-ChildItem "$R\runs\$_" -Directory | Sort-Object Name | Select-Object -Last 1).FullName }
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r63d -Arms 'r63d-b-6202,r63d-b-6203,r63d-b-6204' -ResumeFrom ($src -join ',') `
    -At 24000 -Seconds 48000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 2400 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
