# Round 51's overnight queue, started only on the owner's word: seeds 1, 2 and 3 one at a time at
# 16 threads from the committed worktree scratch/wt-r51 (scripts/farm-queue.ps1, which refuses a
# dirty tree), then V2 (rounds/v2-r51.ps1). -GpuTransport only after the card's daytime
# check has passed (D126, HANDOFF item 3).
param([switch]$GpuTransport)
$main = 'D:\Projects\experiments\evolution-simulator'
$w = "$main\scratch\wt-r51"
$a = @('-NoProfile', '-File', "$w\scripts\farm-queue.ps1", '-Round', 'r51', '-Tree', $w,
    '-Launcher', 'rounds/env-r51.ps1', '-Seeds', '1,2,3', '-After', "$w\rounds\v2-r51.ps1")
if ($GpuTransport) { $a += '-GpuTransport' }
$p = Start-Process -FilePath pwsh -WindowStyle Hidden -PassThru -ArgumentList $a `
    -RedirectStandardOutput "$main\scratch\logs\r51-queue.log" -RedirectStandardError "$main\scratch\logs\r51-queue.err"
Write-Host "round 51's queue started, pid $($p.Id), at $(Get-Date -Format 'yyyy-MM-dd HH:mm'); log scratch\logs\r51-queue.log"
