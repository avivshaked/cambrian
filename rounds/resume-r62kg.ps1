# The gut's card identity (logbook/specs/r62kg-prereg/predictions.md, the coordinator's ruling (c) of 2026-10-25): the
# check's world, r62kgck-6201, resumed from its own checkpoint at -At, the one of 1,500, 3,000 and 4,500 s whose
# next 600 s in the check hold the most bodies eaten, for 600 s at group -Group (32, then 64 as a second run). The two are compared
# with each other by scripts/reads/gpu-accept.py, never with the check, which a card resume does not repeat on this
# line of builds. The pair counts only if killsSwallowed and bodiesEaten are both above 0 in both runs. The same
# config as the check (no cousin), the round's cores, mask, threads and transport. Runs under
# scratch\contact-eating\check-kg-pair. Run only on the coordinator's go, one group size at a time.
# PowerShell's names ignore case: nothing here may be named $r beside $R.
param([Parameter(Mandatory)][ValidateSet(1500, 3000, 4500)][int]$At, [ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
# The launch worktree: main at the pre-registration's commit, with its Release farm built.
$b = "$R\scratch\wt-r62kg"
$source = "$R\scratch\contact-eating\check-kg\r62kgck-6201"
New-Item -ItemType Directory -Force "$R\scratch\contact-eating\check-kg-pair" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62kgpr$Group" -Arms "r62kgpr-6201-g$Group" `
    -Launcher "$b\rounds\env-r62kg.ps1" -Seeds '6201' -ResumeFrom $source -At $At `
    -Seconds 600 -Group $Group -Transport -Threads 14 -Mask 1073676288 -HeatStop -WallMinutes 40 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\contact-eating\check-kg-pair" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
