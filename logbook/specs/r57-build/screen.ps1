# Round 57's one-year screen (D151): seed 2 for 6,000 s at 8 threads on round-57's build, pinned to the
# efficiency cores 16 to 23 (the farm inherits this process's affinity through Start-Process).
$R = 'D:\Projects\experiments\evolution-simulator'
(Get-Process -Id $PID).ProcessorAffinity = [IntPtr][int64]16711680
& "$R\scratch\wt-r57\scripts\run-farm.ps1" r57-screen-s2 -Launcher rounds/env-r57.ps1 -Seed 2 -Seconds 6000 -Threads 8 -RunsRoot "$R\runs" *>&1 |
    Out-File -Encoding utf8 "$R\scratch\logs\r57-screen-launch.log"
