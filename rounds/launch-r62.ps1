# Round 62 as relaunched (D214; the predictions are logbook/specs/r62-prereg/predictions.md), on r62-launch in
# scratch/wt-r62launch: one card group of six worlds founded for 72,000 s, twins on seeds 6201 to 6203. On each seed
# r62-n<seed> has the mouth's reach at 3 m without spoilage (rounds/env-r62-n.ps1) and r62-h<seed> the same with a
# corpse half-life of 500 s (rounds/env-r62-h.ps1). The snow's transport on the card, group 32, cores 16-29, the heat
# stop on, checkpoints every 1,500 s, runs in the main checkout's runs\. The wall limit of 25 h is a backstop only:
# a pace under 1x for an hour stops the round first (the pre-registration's "What would change the plan"). The wait
# for run.json is 600 s: on 2026-10-23 the twins took 2 min 17 s to write it on the card, past the default 120 s,
# and a world that has not written it gets no STOP, so a false timeout would leave the farm running with its
# controller gone after 35 min. Start it detached (Start-Process, hidden), never inside a tool's task. Launch
# only on the coordinator's go, from a clean tree. PowerShell's names ignore case: nothing here may be named $r
# beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r62launch"
$arms = @(
    @{ name = 'r62-n6201'; launcher = 'env-r62-n.ps1'; seed = '6201' },
    @{ name = 'r62-h6201'; launcher = 'env-r62-h.ps1'; seed = '6201' },
    @{ name = 'r62-n6202'; launcher = 'env-r62-n.ps1'; seed = '6202' },
    @{ name = 'r62-h6202'; launcher = 'env-r62-h.ps1'; seed = '6202' },
    @{ name = 'r62-n6203'; launcher = 'env-r62-n.ps1'; seed = '6203' },
    @{ name = 'r62-h6203'; launcher = 'env-r62-h.ps1'; seed = '6203' })
$names = ($arms | ForEach-Object { $_.name }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\$($_.launcher)" }) -join ','
$seeds = ($arms | ForEach-Object { $_.seed }) -join ','
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62 -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 72000 -Group 32 -Transport -CheckpointEvery 1500 -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 1500 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\runs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -AllowBeside 'Assay\.Run\.dll'
