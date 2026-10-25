# Round 62's short card identity run with the clock on (D214; the spoil build's README, "Identity"): the seed 6201
# twins, rounds/env-r62-n.ps1 and rounds/env-r62-h.ps1, founded for 600 s on the card at group -Group (32, then 64
# as a second run), worlds r62id-<n|h>-g<Group>. gpu-accept.py then compares each world at 32 against itself at 64,
# and the 32 worlds against the spoilage screen's r62s-r3-h0 and r62s-r3-h500 over their first 600 s, which ran at
# group 32 on the same code. The cores, mask and threads are the round's. Runs under scratch\r62-launch\identity.
# Run only on the coordinator's go, one group size at a time. PowerShell's names ignore case: nothing here may be
# named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r62launch"
$names = "r62id-n-g$Group,r62id-h-g$Group"
$launchers = "$b\rounds\env-r62-n.ps1,$b\rounds\env-r62-h.ps1"
New-Item -ItemType Directory -Force "$R\scratch\r62-launch\identity" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62id$Group" -Arms $names -Launcher $launchers -Seeds '6201,6201' `
    -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\r62-launch\identity" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
