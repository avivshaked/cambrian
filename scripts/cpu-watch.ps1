# Records, about every two seconds for a fixed time: total CPU, how busy the performance cores
# (logical 0-15 on an i9-13900K) and the efficiency cores (16-31) are and how fast they run
# (% of base clock), GPU use, the two top CPU processes, which window has focus, and whether
# Task Manager is running. Read-only; writes only scratch/cpu-watch/samples-HHmmss.tsv.
# Bounded by its own deadline, so it cannot orphan.
param([int]$Seconds = 240)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Fg2 {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
"@

$dir = Join-Path $PSScriptRoot '..\scratch\cpu-watch'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$out = Join-Path $dir ('samples-' + (Get-Date).ToString('HHmmss') + '.tsv')
$tab = [char]9
(@('time', 'totalCpu', 'pBusy', 'eBusy', 'pPerf', 'ePerf', 'gpu', 'focus', 'taskmgr', 'topCpu') -join $tab) |
    Set-Content -Path $out -Encoding utf8

$n = [Environment]::ProcessorCount
$names = @{}
$prev = @{}
foreach ($p in Get-Process) { if ($p.CPU -ne $null) { $prev[$p.Id] = $p.CPU }; $names[$p.Id] = $p.ProcessName }
$prevTime = Get-Date
$deadline = (Get-Date).AddSeconds($Seconds)

$paths = @(
    '\Processor(_Total)\% Processor Time',
    '\Processor Information(*)\% Processor Time',
    '\Processor Information(*)\% Processor Performance',
    '\GPU Engine(*)\Utilization Percentage')

while ((Get-Date) -lt $deadline) {
    $c = Get-Counter -Counter $paths -SampleInterval 1 -ErrorAction SilentlyContinue

    $now = Get-Date
    $dt = ($now - $prevTime).TotalSeconds
    $cur = @{}
    $use = @{}
    $procs = Get-Process
    foreach ($p in $procs) {
        $names[$p.Id] = $p.ProcessName
        if ($p.CPU -eq $null) { continue }
        $cur[$p.Id] = $p.CPU
        if ($prev.ContainsKey($p.Id)) { $use[$p.Id] = 100 * ($p.CPU - $prev[$p.Id]) / $dt / $n }
    }
    $prev = $cur
    $prevTime = $now
    $top = ($use.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 2 |
        ForEach-Object { '{0}:{1:0.0}' -f $names[$_.Key], $_.Value }) -join ' '

    $total = 0.0; $gpu = 0.0
    $pb = @(); $eb = @(); $pp = @(); $ep = @()
    foreach ($s in $c.CounterSamples) {
        $path = $s.Path.ToLowerInvariant()
        if ($path -like '*processor(_total)*') { $total = $s.CookedValue; continue }
        if ($path -like '*gpu engine*') { $gpu += $s.CookedValue; continue }
        if ($s.InstanceName -notmatch '^0,(\d+)$') { continue }
        $k = [int]$Matches[1]
        $isTime = $path -like '*% processor time'
        if ($k -lt 16) { if ($isTime) { $pb += $s.CookedValue } else { $pp += $s.CookedValue } }
        else { if ($isTime) { $eb += $s.CookedValue } else { $ep += $s.CookedValue } }
    }
    $pBusy = ($pb | Measure-Object -Average).Average
    $eBusy = ($eb | Measure-Object -Average).Average
    $pPerf = ($pp | Measure-Object -Average).Average
    $ePerf = ($ep | Measure-Object -Average).Average

    $fgPid = 0
    [void][Fg2]::GetWindowThreadProcessId([Fg2]::GetForegroundWindow(), [ref]$fgPid)
    $focus = if ($names.ContainsKey([int]$fgPid)) { $names[[int]$fgPid] } else { "pid$fgPid" }
    $tm = [bool]($procs | Where-Object ProcessName -eq 'Taskmgr')

    $row = @(
        $now.ToString('HH:mm:ss'),
        ('{0:0.0}' -f $total), ('{0:0.0}' -f $pBusy), ('{0:0.0}' -f $eBusy),
        ('{0:0}' -f $pPerf), ('{0:0}' -f $ePerf), ('{0:0.0}' -f $gpu),
        $focus, $tm, $top) -join $tab
    Add-Content -Path $out -Value $row -Encoding utf8
}
