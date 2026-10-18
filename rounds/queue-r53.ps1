# Round 53's queue (logbook/0129), started under the owner's overnight delegation of 2026-09-29:
# seeds 1, 2 and 3 one at a time at 12 threads from the committed worktree scratch/wt-r53
# (scripts/farm-queue.ps1, which refuses a dirty tree). Twelve and not sixteen because the nursery
# (D133) takes four beside them; the total stays at half the machine.
# -GpuTransport only after the card's daytime check has passed (D126).
param([switch]$GpuTransport, [int]$Threads = 12)
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-r53"
$a = @('-NoProfile', '-File', "$w\scripts\farm-queue.ps1", '-Round', 'r53', '-Tree', $w,
    '-Launcher', 'rounds/env-r53.ps1', '-Seeds', '1,2,3', '-Threads', $Threads)
if ($GpuTransport) { $a += '-GpuTransport' }
$p = Start-Process -FilePath pwsh -WindowStyle Hidden -PassThru -ArgumentList $a `
    -RedirectStandardOutput "$main\scratch\logs\r53-queue.log" -RedirectStandardError "$main\scratch\logs\r53-queue.err"
Write-Host "round 53's queue started, pid $($p.Id), at $(Get-Date -Format 'yyyy-MM-dd HH:mm'); log scratch\logs\r53-queue.log"
