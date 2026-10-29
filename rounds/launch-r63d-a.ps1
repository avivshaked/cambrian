# r63d's step 1 (logbook/specs/r63d-prereg/predictions.md, D259), on the coordinator's go only, from scratch/wt-r63b at
# the pre-registration's final commit with its own Release farm: r63d-b-6204 founded from rounds/env-r63c-b.ps1 on seed
# 6204 for 24,000 s, so that it stands beside r63c's two B worlds. Group 32, the snow's transport on the card, the heat
# stop on (D258), checkpoints every 1,500 s, logical 8 to 15 at 8 threads. Start it detached and hidden
# (Start-Process -WindowStyle Hidden), never inside a tool's task, with scratch/wt-r63b as the working directory.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r63b"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r63d-a -Arms 'r63d-b-6204' -Launcher "$b\rounds\env-r63c-b.ps1" `
    -Seeds '6204' -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 8 -Mask 65280 `
    -HeatStop -WallMinutes 720 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
