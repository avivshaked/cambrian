# D226's build's card identity: seed 6201 founded twice for 600 s on the card at group -Group (32, then 64 as a second
# run). r62bdid-off-g<Group> is rounds/env-r62cs-w1500.ps1 with the new dial at its default of 1; gpu-accept.py compares
# it at 32 with scratch\contact-eating\identity-cs\r62csid-on-g32, the same world on D224's build, to show the dial at 1
# steps r62cs's world bit for bit. r62bdid-x16-g<Group> is rounds/env-r62bd-x16-i16.ps1, compared at 32 against 64.
# The cores, mask and threads are the round's. Runs under scratch\contact-eating\identity-bd. Run only on the
# coordinator's go, one group size at a time. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$names = "r62bdid-off-g$Group,r62bdid-x16-g$Group"
$launchers = "$b\rounds\env-r62cs-w1500.ps1,$b\rounds\env-r62bd-x16-i16.ps1"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\identity-bd" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62bdid$Group" -Arms $names -Launcher $launchers -Seeds '6201,6201' `
    -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\contact-eating\identity-bd" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
