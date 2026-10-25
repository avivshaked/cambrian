# The contact-eating build's card identity (logbook/specs/contact-eating-spec.md section 11): seed 6201 with the dial off
# (rounds/env-r62-h.ps1) and on (rounds/env-r62ce.ps1), founded for 600 s on the card at group -Group (32, then 64 as a
# second run), worlds r62ceid-off-g<Group> and r62ceid-on-g<Group>. gpu-accept.py then compares each world at 32
# against itself at 64. It also compares the dial-off world at 32 against round 62's r62-h6201 over its first 600 s,
# which ran on the card at group 32 on the build before contact eating. The cores, mask and threads are the round's.
# Runs under scratch\contact-eating\identity. Run only on the coordinator's go, one group size at a time, after
# rounds/preflight-r62ce.ps1 has passed. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$names = "r62ceid-off-g$Group,r62ceid-on-g$Group"
$launchers = "$b\rounds\env-r62-h.ps1,$b\rounds\env-r62ce.ps1"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\identity" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62ceid$Group" -Arms $names -Launcher $launchers -Seeds '6201,6201' `
    -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\contact-eating\identity" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
