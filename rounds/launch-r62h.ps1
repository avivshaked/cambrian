# The reach follow-up (proposals/propose-r62-reach.md on main), on r62-graze in scratch/wt-build: one card group of eight
# worlds for 12,000 s. Reach 3 m and 4 m at seed 6201 (the screen's control and 2 m arm at that seed are already run,
# and a rerun would repeat them bit for bit), then the control, 2 m, 3 m and 4 m at seed 6202, and the
# nursery's foragers at 2 m and 3 m at seed 6201. The snow's transport
# on the card, group 32, cores 16-29, the heat stop on, checkpoints every 1,500 s, runs in the main checkout's runs\.
# Launch only on the owner's yes, passed on by the coordinator. PowerShell's names ignore case: nothing here may be
# named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-build"
$arms = @(
    @{ name = 'r62g-reach3'; launcher = 'env-r62g-reach3.ps1'; seed = '6201' },
    @{ name = 'r62g-reach4'; launcher = 'env-r62g-reach4.ps1'; seed = '6201' },
    @{ name = 'r62h-ctl'; launcher = 'env-r62g.ps1'; seed = '6202' },
    @{ name = 'r62h-reach'; launcher = 'env-r62g-reach.ps1'; seed = '6202' },
    @{ name = 'r62h-reach3'; launcher = 'env-r62g-reach3.ps1'; seed = '6202' },
    @{ name = 'r62h-reach4'; launcher = 'env-r62g-reach4.ps1'; seed = '6202' },
    @{ name = 'r62g-nursery-reach'; launcher = 'env-r62g-nursery-reach2.ps1'; seed = '6201' },
    @{ name = 'r62g-nursery-reach3'; launcher = 'env-r62g-nursery-reach3.ps1'; seed = '6201' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62h -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 12000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 360 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
