# D212's long baseline without spoilage: the grazer screen's r62g-ctl and r62g-reach resumed from their 12,000 s
# checkpoints for 12,000 s more, to 24,000 s, as one card group on r62-graze's Release build in scratch/wt-build (the
# build that recorded them). Group 32, the snow's transport on the card, cores 16-29, the heat stop on, checkpoints
# every 1,500 s, runs in the main checkout's runs\, each a new run under its own arm. Launch only on the coordinator's
# go. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-build"
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62gx -Arms 'r62g-ctl,r62g-reach' `
    -ResumeFrom 'r62g-ctl,r62g-reach' -At 12000 -Seconds 12000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 `
    -Mask 1073676288 -HeatStop -WallMinutes 300 -RunsRoot "$R\runs" `
    -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
