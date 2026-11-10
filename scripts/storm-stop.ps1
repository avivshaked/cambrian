# A VS Code file-scan storm: rg.exe children of VS Code's NodeService piling up faster than they finish
# (2026-11-10, logbook/specs/machine-and-tools.md). The owner ruled on 2026-11-10 that any session may stop
# those rg.exe processes when more than 300 are alive, and tell the owner. They only list files.
#
# Usage: pwsh -File scripts/storm-stop.ps1            # report only
#        pwsh -File scripts/storm-stop.ps1 -Stop      # stop them if over the threshold
# Exit 0: no storm. Exit 2: a storm (stopped with -Stop, reported without).
param([switch]$Stop, [int]$Threshold = 300, [int]$Passes = 3)

function Get-StormRg {
    $nodeIds = @(Get-CimInstance Win32_Process -Filter "Name='Code.exe'" |
        Where-Object { $_.CommandLine -match 'node\.mojom\.NodeService' } | ForEach-Object { $_.ProcessId })
    if ($nodeIds.Count -eq 0) { return @() }
    @(Get-CimInstance Win32_Process -Filter "Name='rg.exe'" | Where-Object { $nodeIds -contains $_.ParentProcessId })
}

$rg = Get-StormRg
$all = @(Get-Process rg -ErrorAction SilentlyContinue).Count
$committed = (Get-Counter '\Memory\Committed Bytes').CounterSamples[0].CookedValue / 1GB
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
"{0}  rg from VS Code {1} (all rg {2}); committed {3:N1} GiB; threshold {4}" -f $stamp, $rg.Count, $all, $committed, $Threshold

if ($rg.Count -le $Threshold) { exit 0 }
if (-not $Stop) { "STORM: over the threshold; run with -Stop to stop VS Code's rg.exe children, then tell the owner"; exit 2 }

$stopped = 0
for ($i = 1; $i -le $Passes; $i++) {
    foreach ($p in (Get-StormRg)) {
        try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop; $stopped++ } catch { }
    }
    if ($i -lt $Passes) { Start-Sleep -Seconds 10 }
}
$left = (Get-StormRg).Count
$after = (Get-Counter '\Memory\Committed Bytes').CounterSamples[0].CookedValue / 1GB
"STORM STOPPED: {0} rg.exe stopped in {1} passes; {2} alive now; committed {3:N1} GiB. Tell the owner; if they keep returning, the Python extensions need switching off for the workspace." -f $stopped, $Passes, $left, $after
exit 2
