# D224's store share on the card: r62cs-w1500-6201 and r62cs-w60-6201 resumed from their 6,000 s checkpoints for 600 s
# in one group at group size -Group (32, then 64 as a second run), worlds r62cswin-w1500-g<Group> and
# r62cswin-w60-g<Group>; a card group is two worlds or more. Over that span the screen's own biteStore rose from about
# 5.6 kJ to 9.4 kJ in the first and 6.7 kJ to 9.6 kJ in the second, so the window steps the share's path, which the
# founded identity runs (logbook/specs/r62cs-prereg/identity.md) did not reach. gpu-accept.py then compares each world
# at 32 against itself at 64. The cores, mask and threads are the round's. Runs under
# scratch\contact-eating\identity-cs-window. Run only on the coordinator's go, one group size at a time, after the
# screen has ended. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\identity-cs-window" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62cswin$Group" -Arms "r62cswin-w1500-g$Group,r62cswin-w60-g$Group" `
    -At 6000 -ResumeFrom "$R\runs\r62cs-w1500-6201,$R\runs\r62cs-w60-6201" -Seconds 600 -Group $Group -Transport -Threads 14 `
    -Mask 1073676288 -HeatStop -WallMinutes 30 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\contact-eating\identity-cs-window" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
