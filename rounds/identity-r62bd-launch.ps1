# D227's launch build's card identity, after the mouth-blow counters: seed 6201 founded four times for 600 s on the card
# at group -Group (32, then 64 as a second run). r62bdl-off-g<Group> is rounds/env-r62cs-w1500.ps1 with the dial at 1,
# compared at 32 with runs/r62csid-on-g32 (D224's build). r62bdl-x2, -x4 and -x16 are the screen's three arms
# (rounds/env-r62bd-x<scale>-i16.ps1), each compared at 32 against 64; -x16 at 32 is also compared with
# scratch\contact-eating\identity-bd\r62bdid-x16-g32, the same world before the counters, which must differ only by the
# two new columns. The cores, mask and threads are the round's. Runs under scratch\contact-eating\identity-bdl. Run only
# on the coordinator's go, one group size at a time. PowerShell's names ignore case: nothing here may be named $r
# beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
$worlds = @(@('off', 'env-r62cs-w1500.ps1'), @('x2', 'env-r62bd-x2-i16.ps1'), @('x4', 'env-r62bd-x4-i16.ps1'), @('x16', 'env-r62bd-x16-i16.ps1'))
$names = ($worlds | ForEach-Object { "r62bdl-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\identity-bdl" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62bdl$Group" -Arms $names -Launcher $launchers `
    -Seeds '6201,6201,6201,6201' -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 30 `
    -RunsRoot "$R\scratch\contact-eating\identity-bdl" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
