# D226's check before the screen: does a deeper mouth's blow reach the meals, and where does the bite's cap bind? Seed
# 6201 founded six times for 4,500 s in one card group at group 32: the dial at 4, 16 and 64 (EVOSIM_BITE_DAMAGE_SCALE),
# each with the intake scale at 1 and at the dial's value (rounds/env-r62bd-x<dial>-i<intake>.ps1), all else r62cs's
# 500 s arm. A D226 build cannot resume an r62cs checkpoint, since the config refuses the new field, so the worlds are
# founded; bites start at about 2,400 s. Read with scripts/reads/r62g-read.py and r62-health.py at 4,500 s, and the
# cap's cut from the stats' bite totals. The cores, mask and threads are the round's. Runs under
# scratch\contact-eating\check-bd. Run only on the coordinator's go. PowerShell's names ignore case: nothing here may
# be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$arms = foreach ($k in 4, 16, 64) { foreach ($i in 1, $k) { "x$k-i$i" } }
$names = ($arms | ForEach-Object { "r62bdck-$_" }) -join ','
$launchers = ($arms | ForEach-Object { "$b\rounds\env-r62bd-$_.ps1" }) -join ','
$seeds = (@('6201') * $arms.Count) -join ','
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\check-bd" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62bdck' -Arms $names -Launcher $launchers -Seeds $seeds `
    -Seconds 4500 -Group 32 -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 90 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\contact-eating\check-bd" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
