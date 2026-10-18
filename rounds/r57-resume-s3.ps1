# Round 57's seed 3, resumed after the coordinator's mistaken pause of 2026-10-07 08:53 (the owner meant
# the card work to start after round 57). From its last checkpoint, 21,710 s, to 48,000 s on round-57's
# build, at 14 threads on logical 18-31, as launched. Masks are decimal int64 (scripts/CLAUDE.md).
$R = 'D:\Projects\experiments\evolution-simulator'
(Get-Process -Id $PID).ProcessorAffinity = [IntPtr][int64]4294705152
& "$R\scratch\wt-r57\scripts\run-farm.ps1" 'r57-s3' -ResumeFrom "$R\runs\r57-s3\2026-10-07-023901-dfc0e881" -At 21710 `
    -Launcher rounds/env-r57.ps1 -Seed 3 -Seconds 48000 -Threads 14 -WallMinutes 2400 -RunsRoot "$R\runs" *>&1 |
    Out-File -Encoding utf8 "$R\scratch\logs\r57-s3-resume.log"
