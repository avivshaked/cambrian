# Integration branch 2's card identity sitting, part 1 (D231 and D234's twin drive, main merged in at d11b9bc0): the
# confirmation round's group run again for 600 s with every new switch off. Seeds 6202, 6203 and 6204 as
# r62i2off-6202, -6203 and -6204, with rounds/env-r62cf.ps1's settings, on launch-r62cf.ps1's cores, mask, threads,
# group, transport and checkpoint stride. Each is compared bit for bit with the prefix of runs/r62cf-<seed>
# (scripts/reads/gpu-accept.py), and its wall with that run's first 600 s, for the switch-off pace. Runs under
# scratch/int2-hold/identity-off. Run only on the coordinator's card go, from scratch/wt-int2's clean tree with its
# Release farm built, with that worktree as the working directory. PowerShell's names ignore case: nothing here may
# be named $r beside $R.
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-int2"
New-Item -ItemType Directory -Force "$R\scratch\int2-hold\identity-off" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name r62i2off -Arms 'r62i2off-6202,r62i2off-6203,r62i2off-6204' `
    -Launcher "$b\rounds\env-r62cf.ps1,$b\rounds\env-r62cf.ps1,$b\rounds\env-r62cf.ps1" -Seeds '6202,6203,6204' `
    -Seconds 600 -Group 32 -Transport -CheckpointEvery 1500 -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\int2-hold\identity-off" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
