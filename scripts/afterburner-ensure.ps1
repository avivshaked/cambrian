# MSI Afterburner is the machine's only CPU temperature source (temp-check.ps1 reads its shared memory), and every
# heat guard stops its job when the reading is gone for 10 s. On 2026-11-10 it exited unseen at about 22:11:55 with no
# crash logged, and stopped both theatre lanes. The owner ruled the same evening that a session may start it again
# itself rather than wait on them (logbook/specs/machine-and-tools.md).
#
# It starts Afterburner through the owner's own logon task (MSIAfterburner, which runs "MSIAfterburner.exe /s"),
# never by any other route, then waits for the CPU reading to return.
#
# Usage: pwsh -File scripts/afterburner-ensure.ps1     # starts it only if it is not running
# Exit 0: running and the CPU reads. Exit 1: started, but no CPU reading within the wait. Exit 2: the task is missing.
param([int]$WaitSeconds = 30)

$stamp = { Get-Date -Format 'yyyy-MM-dd HH:mm:ss' }
$running = @(Get-Process MSIAfterburner -ErrorAction SilentlyContinue).Count -gt 0
if (-not $running) {
    if (-not (Get-ScheduledTask -TaskName MSIAfterburner -ErrorAction SilentlyContinue)) {
        "{0}  MSIAfterburner's logon task is missing: start Afterburner by hand" -f (& $stamp); exit 2
    }
    "{0}  Afterburner is not running: starting it through its logon task" -f (& $stamp)
    Start-ScheduledTask -TaskName MSIAfterburner
}

$check = Join-Path $PSScriptRoot 'temp-check.ps1'
for ($i = 0; $i -lt $WaitSeconds; $i += 5) {
    $line = & pwsh -NoProfile -File $check 2>&1 | Select-Object -Last 1
    if ($line -match 'cpu \d+C') {
        "{0}  CPU reading present: {1}" -f (& $stamp), $line
        # The theatre's render guard writes the global STOP with its time and reason on a trip. A trip for the missing reading is
        # cleared here once the reading is back. A heat trip's STOP, or one with no reason inside, stays for a person (the
        # coordinator, 2026-11-10).
        $stop = Join-Path (Split-Path -Parent $PSScriptRoot) 'scratch\theatre-realism\STOP'
        if (Test-Path -LiteralPath $stop) {
            $why = (Get-Content -LiteralPath $stop -Raw)
            if ($why -match 'CPU temperature unreadable') {
                Remove-Item -LiteralPath $stop
                "{0}  cleared the global STOP, a trip for the missing reading: {1}" -f (& $stamp), $why.Trim()
            } else {
                "{0}  the global STOP stays (not a missing-reading trip): {1}" -f (& $stamp), $(if ($why) { $why.Trim() } else { 'no reason inside' })
            }
        }
        exit 0
    }
    Start-Sleep -Seconds 5
}
"{0}  no CPU reading after {1} s: tell the owner" -f (& $stamp), $WaitSeconds
exit 1
