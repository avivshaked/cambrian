# Round 54's queue (logbook/0130), the owner's ruling D138 of 2026-09-30:
# seeds 1, 2 and 3 one at a time at 12 threads from the committed worktree scratch/wt-r54
# (scripts/farm-queue.ps1, which refuses a dirty tree). Twelve and not sixteen because the nursery
# pilot (D138) takes four beside them; the total stays at half the machine.
# No -GpuTransport: the card's engine refuses D135's drive and the scent.
param([switch]$GpuTransport, [int]$Threads = 12)
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-r54"
$a = @('-NoProfile', '-File', "$w\scripts\farm-queue.ps1", '-Round', 'r54', '-Tree', $w,
    '-Launcher', 'rounds/env-r54.ps1', '-Seeds', '1,2,3', '-Threads', $Threads)
if ($GpuTransport) { $a += '-GpuTransport' }
$p = Start-Process -FilePath pwsh -WindowStyle Hidden -PassThru -ArgumentList $a `
    -RedirectStandardOutput "$main\scratch\logs\r54-queue.log" -RedirectStandardError "$main\scratch\logs\r54-queue.err"
Write-Host "round 54's queue started, pid $($p.Id), at $(Get-Date -Format 'yyyy-MM-dd HH:mm'); log scratch\logs\r54-queue.log"
