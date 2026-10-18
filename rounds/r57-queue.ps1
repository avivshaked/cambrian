# Round 57's queue: seeds 1 and 2 together on logical 16-23 and 24-31, then seed 3 on whichever set
# frees first. Started detached; it polls the two manifests once a minute and gives up after 48 hours.
$R = 'D:\Projects\experiments\evolution-simulator'
$A = [int64]16711680      # logical 16-23
$B = [int64]4278190080    # logical 24-31
$log = "$R\scratch\logs\r57-queue.log"
function Say($m) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m" | Out-File -Append -Encoding utf8 $log }
function Launch($seed, $mask) {
    Say "launching r57-s$seed on mask $mask"
    Start-Process pwsh -ArgumentList '-NoProfile', '-File', "$R\scratch\r57-launch\launch-seed.ps1", $seed, $mask `
        -WindowStyle Hidden -Wait
    Say "r57-s$seed launch returned"
}
function Status($arm) {
    $d = Get-ChildItem "$R\runs\$arm" -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    if (-not $d) { return 'absent' }
    try { (Get-Content "$($d.FullName)\run.json" -Raw | ConvertFrom-Json).status } catch { 'unreadable' }
}
Launch 1 $A
Launch 2 $B
$deadline = (Get-Date).AddHours(48)
while ((Get-Date) -lt $deadline) {
    $s1 = Status 'r57-s1'; $s2 = Status 'r57-s2'
    if ($s1 -ne 'running') { Say "r57-s1 is $s1"; Launch 3 $A; exit 0 }
    if ($s2 -ne 'running') { Say "r57-s2 is $s2"; Launch 3 $B; exit 0 }
    Start-Sleep -Seconds 60
}
Say "deadline reached with seeds 1 and 2 still running; seed 3 not launched"
