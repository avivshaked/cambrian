# One seed of round 57 (logbook/0136) to 48,000 s on round-57's build, pinned to the logical
# processors in $Mask (the farm inherits this process's affinity through Start-Process).
# Masks are written as decimal int64, never as a 0x literal (scripts/CLAUDE.md).
param([int]$Seed, [int64]$Mask, [int]$Threads = 8)
$R = 'D:\Projects\experiments\evolution-simulator'
(Get-Process -Id $PID).ProcessorAffinity = [IntPtr]$Mask
& "$R\scratch\wt-r57\scripts\run-farm.ps1" "r57-s$Seed" -Launcher rounds/env-r57.ps1 -Seed $Seed -Seconds 48000 `
    -Threads $Threads -WallMinutes 2400 -RunsRoot "$R\runs" *>&1 |
    Out-File -Encoding utf8 "$R\scratch\logs\r57-s$Seed-launch.log"
