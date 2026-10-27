# Integration branch 2's card identity sitting, part 2: each new switch on, at group -Group (32, then 64 as a second
# run). Seed 6202 founded four times for 600 s on the confirmation round's world: r62i2on-fin (D229's fin law, the
# control), -asm (D231's stroke on the assembly), -twin (the mirrored joint with D234's twin drive) and -all. Each
# world at 32 is compared with itself at 64 (scripts/reads/gpu-accept.py). Same cores, mask, threads and transport as
# part 1. Runs under scratch/int2-hold/identity-on. Run only on the coordinator's card go, from scratch/wt-int2's clean
# tree, one group size at a time. PowerShell's names ignore case: nothing here may be named $r beside $R.
param([ValidateSet(32, 64)][int]$Group = 32)
$R = 'D:\Projects\experiments\evolution-simulator'
$b = "$R\scratch\wt-int2"
$worlds = @(@('fin', 'env-int2-fin.ps1'), @('asm', 'env-int2-asm.ps1'), @('twin', 'env-int2-twin.ps1'), @('all', 'env-int2-all.ps1'))
$names = ($worlds | ForEach-Object { "r62i2on-$($_[0])-g$Group" }) -join ','
$launchers = ($worlds | ForEach-Object { "$b\rounds\$($_[1])" }) -join ','
New-Item -ItemType Directory -Force "$R\scratch\int2-hold\identity-on" | Out-Null
& pwsh -NoProfile -File "$b\scripts\card\card-group.ps1" -Name "r62i2on$Group" -Arms $names -Launcher $launchers `
    -Seeds '6202,6202,6202,6202' -Seconds 600 -Group $Group -Transport -Threads 10 -Mask 4290772992 -HeatStop -WallMinutes 45 `
    -WaitForManifestSeconds 600 `
    -RunsRoot "$R\scratch\int2-hold\identity-on" -Exe "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" `
    -AllowBeside 'Assay\.Run\.dll'
