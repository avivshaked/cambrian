# The driver watch beside a card job or an accelerator run, started by card-run.ps1, card-test.ps1 and
# accelerator-batch.ps1. Its settings come from a JSON file (Since, Deadline, JobLog, EndPattern, StopLine,
# Controller, and optionally Arm and RunsRoot), never from the command line, where Start-Process splits an
# argument at its spaces. Every 20 s it reads the System log for nvlddmkm events since Since. On the first one
# it stops every pwsh whose command line matches the Controller pattern, with its process tree; with Arm and
# RunsRoot set it writes STOP for that farm (stop-arm.ps1) and kills it by id 30 s later if it still runs; then
# it writes StopLine into JobLog and exits 2. Exits 0 when JobLog shows EndPattern, 1 at Deadline, 3 when the
# settings name no Controller (an empty pattern matches every command line). Its first line, "watch begins:",
# names its settings, and a job that copies it into JobLog must mask the end pattern in the copy.
param([Parameter(Mandatory)][string]$Settings)
$cfg = Get-Content $Settings -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')   # Get-ProcessTree only; this watch sets no CUDA_VISIBLE_DEVICES
if (-not $cfg.Controller -or $cfg.Controller.Length -lt 8) { "$(Get-Date -Format s) refused: no Controller pattern in $Settings"; exit 3 }
$since = [datetime]$cfg.Since
$deadline = [datetime]$cfg.Deadline
"$(Get-Date -Format s) watch begins: since $($since.ToString('s')), deadline $($deadline.ToString('s')), log $($cfg.JobLog), end pattern '$($cfg.EndPattern)', controller '$($cfg.Controller)'"
while ((Get-Date) -lt $deadline) {
    $ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = $since } -ErrorAction SilentlyContinue)
    if ($ev.Count -gt 0) {
        "$(Get-Date -Format s) nvlddmkm: " + (($ev | ForEach-Object { "$($_.TimeCreated.ToString('s')) id $($_.Id)" }) -join '; ')
        $all = Get-CimInstance Win32_Process
        $roots = @($all | Where-Object { $_.Name -eq 'pwsh.exe' -and $_.CommandLine -match $cfg.Controller } | ForEach-Object { $_.ProcessId })
        $tree = @($roots | ForEach-Object { Get-ProcessTree $_ $all } | Select-Object -Unique)
        [array]::Reverse($tree)
        foreach ($id in $tree) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue; "stopped $id" }
        # A farm that run-farm.ps1 launched is detached from the controller's tree. With Arm and RunsRoot set,
        # it gets a STOP file first, and is killed by id if it has not ended 30 s later (a hung card may never
        # let it reach its next report row).
        if ($cfg.Arm -and $cfg.RunsRoot) {
            & (Join-Path $PSScriptRoot '..\stop-arm.ps1') $cfg.Arm -RunsRoot $cfg.RunsRoot -Reason manual-other *>&1 | ForEach-Object { "stop-arm: $_" }
            Start-Sleep -Seconds 30
            foreach ($f in @(Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $since })) {
                Stop-Process -Id $f.Id -Force -ErrorAction SilentlyContinue
                "killed farm $($f.Id), still running 30 s after its STOP"
            }
        }
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $($cfg.StopLine)" | Add-Content $cfg.JobLog
        exit 2
    }
    # A job may copy this watch's first line into its log, and that line names the end pattern; it ended two
    # watches at their second poll (2026-10-08). So a line that says "watch begins" never ends it.
    if ((Test-Path $cfg.JobLog) -and (Select-String -Path $cfg.JobLog -SimpleMatch $cfg.EndPattern |
            Where-Object { $_.Line -notmatch 'watch begins' } | Select-Object -First 1)) {
        "$(Get-Date -Format s) the run ended; no nvlddmkm events"; exit 0
    }
    Start-Sleep -Seconds 20
}
"$(Get-Date -Format s) deadline reached; no nvlddmkm events"
exit 1
