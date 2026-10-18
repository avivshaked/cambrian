# Round 57's seed 3, by the owner's choice of 2026-10-06: wait until seeds 1 and 2 have both ended,
# then run seed 3 at 14 threads on logical 18-31, leaving 16-17 for builds. Started detached; it
# polls the two manifests once a minute and gives up after 36 hours.
$R = 'D:\Projects\experiments\evolution-simulator'
$E = [int64]4294705152    # logical 18-31
$log = "$R\scratch\logs\r57-queue.log"
function Say($m) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m" | Out-File -Append -Encoding utf8 $log }
function Status($arm) {
    $d = Get-ChildItem "$R\runs\$arm" -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    if (-not $d) { return 'absent' }
    try { (Get-Content "$($d.FullName)\run.json" -Raw | ConvertFrom-Json).status } catch { 'unreadable' }
}
Say "queue-s3 waiting for r57-s1 and r57-s2 to end"
$deadline = (Get-Date).AddHours(36)
while ((Get-Date) -lt $deadline) {
    $s1 = Status 'r57-s1'; $s2 = Status 'r57-s2'
    if ($s1 -ne 'running' -and $s1 -ne 'unreadable' -and $s2 -ne 'running' -and $s2 -ne 'unreadable') {
        Say "r57-s1 is $s1, r57-s2 is $s2; launching r57-s3 at 14 threads on mask $E"
        Start-Process pwsh -ArgumentList '-NoProfile', '-File', "$R\scratch\r57-launch\launch-seed.ps1", 3, $E, 14 `
            -WindowStyle Hidden -Wait
        Say "r57-s3 launch returned"
        exit 0
    }
    Start-Sleep -Seconds 60
}
Say "deadline reached before seeds 1 and 2 both ended; seed 3 not launched"
