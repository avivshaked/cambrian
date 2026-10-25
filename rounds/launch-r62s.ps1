# D213's spoilage-by-reach screen (logbook/specs/r62-spoil-screen-proposal.md on main), on r62-spoil in
# scratch/wt-spoil: one card group of eight worlds founded for 24,000 s. Six at seed 6201 (the grazer screen's control
# and 2 m arm at that seed are D212's long baseline), two at seed 6202. The snow's transport on the card, group 32,
# cores 16-29, the heat stop on, checkpoints every 1,500 s, runs in the main checkout's runs\. Launch only on the
# coordinator's go. PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-spoil"
$arms = @(
    @{ name = 'r62s-r2-h500'; seed = '6201' },
    @{ name = 'r62s-r2-h2000'; seed = '6201' },
    @{ name = 'r62s-r3-h0'; seed = '6201' },
    @{ name = 'r62s-r3-h500'; seed = '6201' },
    @{ name = 'r62s-r3-h2000'; seed = '6201' },
    @{ name = 'r62s-c-h500'; seed = '6201' },
    @{ name = 'r62t-r2-h0'; seed = '6202' },
    @{ name = 'r62t-r2-h500'; seed = '6202' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\env-r62s-$($_.name.Substring(5)).ps1" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62s -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 24000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 480 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
