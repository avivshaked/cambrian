# Does a card resume replay its run before D224's store share has acted? r62cs-w1500-6201 and r62cs-w1500-6202
# resumed from their 1,500 s checkpoints for 100 s at group 32, worlds r62csrp-w1500-6201 and r62csrp-w1500-6202,
# where biteStore was still 0 (it first rose at about 2,400 s). The window from 6,000 s parted from the screen's rows
# at its first row (logbook/specs/r62cs-prereg/identity.md); compare-run-rows.py against each screen world from
# 1,500 s says whether this one does. Runs under scratch\contact-eating\probe-resume. Run only on the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-mouthpoint"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\probe-resume" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r62csrp' -Arms 'r62csrp-w1500-6201,r62csrp-w1500-6202' `
    -At 1500 -ResumeFrom "$R\runs\r62cs-w1500-6201,$R\runs\r62cs-w1500-6202" -Seconds 100 -Group 32 -Transport -Threads 14 `
    -Mask 1073676288 -HeatStop -WallMinutes 20 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\contact-eating\probe-resume" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
