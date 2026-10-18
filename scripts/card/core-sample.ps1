# The processor's state beside a card run, one row a turn (card-next-r57.md Part 7, "Two fast runs"). Dot-sourced
# by card-run.ps1 under -SampleCores; run on its own with -Csv and -Turns for a dry check, which samples that many
# times a second apart and prints the summary.
#
# A row: the time, the machine's total % Processor Utility (as Task Manager reports the load, so a busy or quiet
# desktop shows), then % Processor Performance for logical 0 to 31 (the clock against the base clock, so a boosting
# core reads above 100), then % Processor Utility for logical 0 to 31. One Get-Counter call a row, over one second.
# The summary: the rows, the total utility's mean and peak, and the performance's mean over logical 16-31 (the
# farm's) and over 0-15, with the lowest and highest row means of 16-31.
param([string]$Csv = '', [int]$Turns = 0)

$script:CoreCounters = @('\Processor Information(*)\% Processor Performance', '\Processor Information(*)\% Processor Utility')

function Write-CoreSample([string]$csv) {
    if (-not (Test-Path $csv)) {
        $h = @('time', 'utility_total') + (0..31 | ForEach-Object { "perf_$_" }) + (0..31 | ForEach-Object { "util_$_" })
        ($h -join ',') | Add-Content $csv
    }
    $s = Get-Counter -Counter $script:CoreCounters -SampleInterval 1 -MaxSamples 1 -ErrorAction SilentlyContinue
    if (-not $s) { return }
    $perf = @{}; $util = @{}; $total = ''
    foreach ($c in $s.CounterSamples) {
        $v = [Math]::Round($c.CookedValue, 1)
        if ($c.InstanceName -eq '_total' -and $c.Path -match 'utility') { $total = $v; continue }
        if ($c.InstanceName -notmatch '^0,(\d+)$') { continue }
        $n = [int]$Matches[1]
        if ($c.Path -match 'performance') { $perf[$n] = $v } else { $util[$n] = $v }
    }
    $row = @($s.Timestamp.ToString('HH:mm:ss'), $total) + (0..31 | ForEach-Object { $perf[$_] }) + (0..31 | ForEach-Object { $util[$_] })
    ($row -join ',') | Add-Content $csv
}

function Get-CoreSummary([string]$csv) {
    if (-not (Test-Path $csv)) { return 'cores: no samples' }
    $rows = @(Import-Csv $csv)
    if ($rows.Count -eq 0) { return 'cores: no samples' }
    $tot = @($rows | ForEach-Object { [double]$_.utility_total })
    $farm = @($rows | ForEach-Object { $r = $_; (16..31 | ForEach-Object { [double]$r."perf_$_" } | Measure-Object -Average).Average })
    $rest = @($rows | ForEach-Object { $r = $_; (0..15 | ForEach-Object { [double]$r."perf_$_" } | Measure-Object -Average).Average })
    $f = $farm | Measure-Object -Average -Minimum -Maximum
    "cores: {0} samples; total utility mean {1:N1}%, peak {2:N1}%; performance on logical 16-31 mean {3:N1}% (row means {4:N1} to {5:N1}), on 0-15 mean {6:N1}%" -f `
        $rows.Count, ($tot | Measure-Object -Average).Average, ($tot | Measure-Object -Maximum).Maximum, $f.Average, $f.Minimum, $f.Maximum, ($rest | Measure-Object -Average).Average
}

if ($Csv -and $Turns -gt 0) {
    if (Test-Path $Csv) { Write-Host "$Csv exists"; exit 1 }
    for ($i = 0; $i -lt $Turns; $i++) { Write-CoreSample $Csv }
    Get-CoreSummary $Csv
}
