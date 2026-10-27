# UNUSED: part b was dropped by the owner's ruling of 2026-10-27 (logbook/specs/r62p2-prereg/predictions.md, "Part b was
# not run"), so -ConfigHash below was never set and this launcher was never started. Kept for the record.
# D239's pace screen, group 2, part b (logbook/specs/r62p2-prereg/predictions.md, "The machine"), from scratch/wt-pace
# with the pre-registration's commits merged in: the group's world at the pace factor 3, r62p-k3w9-6201, founded alone
# for 24,000 s on seed 6201 (rounds/env-r62p-k3w9.ps1), through scripts/card/card-run.ps1, since card-group.ps1 refuses
# one world. The snow's transport on the card, group 32, the heat stop on, checkpoints every 1,500 s, on logical 16 to
# 29 at 14 threads with the theatre paused (the coordinator's ruling of 2026-10-27). Start it detached and hidden
# (Start-Process -WindowStyle Hidden), never inside a tool's task, from a clean tree, on the coordinator's go, with
# scratch/wt-pace as the working directory. card-run founds the world on a one-second CPU preflight first; before the
# launch, -ConfigHash is set to that hash, so the card starts only on the pre-registered world.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-pace"
& pwsh -NoProfile -File "$b\scripts\card\card-run.ps1" -Arm 'r62p-k3w9-6201' -At 0 -Seconds 24000 `
    -Launcher "$b\rounds\env-r62p-k3w9.ps1" -Seed 6201 -ConfigHash '' `
    -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 900 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
