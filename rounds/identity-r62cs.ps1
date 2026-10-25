# D224's build's card identity: seed 6201 with the new dial off (rounds/env-r62ce.ps1) and on with the longest window
# (rounds/env-r62cs-w1500.ps1), founded for 600 s on the card at group -Group (32, then 64 as a second run), worlds
# r62csid-off-g<Group> and r62csid-on-g<Group>. gpu-accept.py then compares each world at 32 against itself at 64. It
# also compares the dial-off world at 32 against runs/r62ceid-on-g32, the same world on r62ce's build at the same group
# (logbook/specs/r62ce-prereg/identity.md). The cores, mask and threads are the round's. Runs under
# scratch\contact-eating\identity-cs. Run only on the coordinator's go, one group size at a time, after
# rounds/preflight-r62cs.ps1 has passed. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$names = "r62csid-off-g$Group,r62csid-on-g$Group"
$launchers = "$b\rounds\env-r62ce.ps1,$b\rounds\env-r62cs-w1500.ps1"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\identity-cs" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62csid$Group" -Arms $names -Launcher $launchers -Seeds '6201,6201' `
    -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\contact-eating\identity-cs" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
