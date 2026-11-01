# Does a card resume replay its run on r64t's build (wt-r64t, f412e4ea)? r64t-t-6202 and r64t-t-6203 resumed from their
# 1,500 s checkpoints for 100 s at group 32, worlds r64trp-t-6202 and r64trp-t-6203. On the contact-eating build of
# 2026-10-24 a resume parted from the unbroken run at its first row (logbook/specs/r62cs-prereg/identity.md, D226);
# compare-run-rows.py against each r64t world from 1,500 s says whether it still does on the true-shape build.
# Cores 20-29, off a render's 16-19. Runs under scratch\card-resume\probe. Run only on the coordinator's go.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-r64t"
New-Item -ItemType Directory -Force "$R\scratch\card-resume\probe" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name 'r64trp' -Arms 'r64trp-t-6202,r64trp-t-6203' `
    -At 1500 -ResumeFrom "$R\runs\r64t-t-6202,$R\runs\r64t-t-6203" -Seconds 100 -Group 32 -Transport -Threads 10 `
    -Mask 1072693248 -HeatStop -WallMinutes 20 -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\card-resume\probe" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
