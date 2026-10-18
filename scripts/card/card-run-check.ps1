# Reads one card-run.ps1 run back from its files and checks every condition a card run is held to: ended on
# budget, the transport it was asked for (-Transport, run.json's transport), the books under their limits,
# diverged 0, refused 0, overflow 0, neuronsAtCeiling 0, no nvlddmkm or Kernel-Power 41 event since -Since,
# the driver watch alive at its check and ended on the true end, and nothing left running. Prints each
# condition and the heat peaks (from the samples card-run.ps1 took, when it took them), and exits 0 when every
# condition holds. Reads only. On a CPU-engine run the card's own conditions read empty and pass.
param(
    [Parameter(Mandatory)][string]$Arm,
    [Parameter(Mandatory)][datetime]$Since,
    [ValidateSet('card', 'cpu')][string]$Transport = 'cpu',
    [string]$RunsRoot = 'scratch\card\runs',
    # Where card-run.ps1 wrote its logs: its own checkout's scratch\logs, which is another folder when the run
    # went from another checkout or worktree.
    [string]$Logs = 'scratch\logs',
    # The name the logs go by when it is not the arm's: a card-pair.ps1 pair or a card-group.ps1 group, whose one log reads every world.
    [string]$LogArm = '',
    # The run's own -AllowBeside: a dotnet.exe whose command line matches it is not counted as left running.
    [string]$AllowBeside = '')
if (-not $LogArm) { $LogArm = $Arm }
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$l = if ([System.IO.Path]::IsPathRooted($Logs)) { $Logs } else { Join-Path $root $Logs }
$runs = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $root $RunsRoot }
$fails = @()
function Cond([string]$name, [bool]$ok, [string]$detail) {
    "{0,-5} {1}: {2}" -f $(if ($ok) { 'ok' } else { 'FAIL' }), $name, $detail
    if (-not $ok) { $script:fails += $name }
}
$log = Get-Content (Join-Path $l "card-run-$LogArm.log")
$m = Get-ChildItem (Join-Path $runs "$Arm\*\run.json") | Sort-Object LastWriteTime | Select-Object -Last 1
$j = Get-Content $m.FullName -Raw | ConvertFrom-Json
$g = $j.gpu
$rows = @(Get-Content (Join-Path $m.DirectoryName 'stats.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$audit = ($rows | ForEach-Object { [math]::Abs([double]$_.auditResidual) } | Measure-Object -Maximum).Maximum
$matter = ($rows | ForEach-Object { [math]::Abs([double]$_.matterResidual) } | Measure-Object -Maximum).Maximum
Cond 'ended on budget' ($j.status -eq 'ended' -and $j.reason -eq 'budget') "$($j.status) ($($j.reason)), t $($rows[-1].t), alive $($rows[-1].alive)"
Cond 'transport' ($j.transport -eq $Transport) "run.json says '$($j.transport)', '$Transport' asked for"
Cond 'books' ($audit -lt 0.1 -and $matter -lt 1e-5) "over $($rows.Count) rows, |audit| at most $audit J, |matter| at most $matter"
Cond 'diverged 0' ($j.divergedTotal -eq 0) "$($j.divergedTotal)"
Cond 'refused 0' ([int]$g.refusedForClass -eq 0) "$($g.refusedForClass)"
Cond 'overflow 0' (([int]$g.overflowCandidates + [int]$g.overflowOverlaps + [int]$g.overflowHeld + [int]$g.overflowEntries) -eq 0) "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
Cond 'neuronsAtCeiling 0' ([int]$g.neuronsAtCeiling -eq 0) "$($g.neuronsAtCeiling)"
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $Since } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'nvlddmkm' -or ($_.ProviderName -eq 'Microsoft-Windows-Kernel-Power' -and $_.Id -eq 41) })
Cond 'no driver event' ($ev.Count -eq 0) "$($ev.Count) from $($Since.ToString('HH:mm:ss')) to $(Get-Date -Format 'HH:mm:ss')"
$watch = Get-Content (Join-Path $l "card-run-$LogArm-watch.log")
$alive = $log | Where-Object { $_ -match 'watch alive after its first polls' }
$endLine = $log | Where-Object { $_ -match '^.{20}  books over' } | Select-Object -First 1
$watchEnd = $watch | Where-Object { $_ -match 'the run ended; no nvlddmkm events' } | Select-Object -First 1
$trueEnd = $false
if ($endLine -and $watchEnd) { $trueEnd = [datetime]($watchEnd.Substring(0, 19)) -ge [datetime]($endLine.Substring(0, 19)) }
Cond 'watch alive at its check' ([bool]$alive) "$alive".Trim()
Cond 'watch ended on the true end' $trueEnd "watch: $watchEnd; read: $(if ($endLine) { $endLine.Substring(0, 19) })"
$left = @(Get-CimInstance Win32_Process | Where-Object { ($_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or $_.CommandLine -match 'card-run\.ps1|card-pair\.ps1|card-group\.ps1|driver-watch') -and
        -not ($AllowBeside -and $_.Name -eq 'dotnet.exe' -and "$($_.CommandLine)" -match $AllowBeside) } |
    ForEach-Object { "$($_.Name) $($_.ProcessId)" })
Cond 'nothing left running' ($left.Count -eq 0) "$($left -join ', ')"
$log | Where-Object { $_ -match 'times real time over' -and $_.Contains("  ${Arm}: ") } | ForEach-Object { "info  pace: $(($_ -split '; ')[2])" }
"info  drive limited $($j.driveImpulsesLimited), drag limited $($j.dragImpulsesLimited) (cumulative since the founding)"
# A sample is "HH:mm:ss, temperature, load %, power W" and, from 2026-10-08 evening, ", memory MiB" after it.
$gpu = Get-Content (Join-Path $l "card-run-$LogArm-gpu.csv") -ErrorAction SilentlyContinue | Where-Object { $_ -match '^\d\d:\d\d:\d\d, \d' } |
    ForEach-Object {
        $q = $_ -split ',\s*'
        [pscustomobject]@{ temp = [int]$q[1]; util = [int]($q[2] -replace ' %', ''); w = [double]($q[3] -replace ' W', '')
            mib = if ($q.Count -gt 4) { [int]($q[4] -replace ' MiB', '') } else { 0 } }
    }
if ($gpu) {
    $busyLoad = @($gpu | Where-Object { $_.util -gt 0 })
    $mean = if ($busyLoad) { [math]::Round(($busyLoad | Measure-Object util -Average).Average, 1) } else { 0 }
    "info  card peak $(($gpu | Measure-Object temp -Maximum).Maximum) C, $(($gpu | Measure-Object util -Maximum).Maximum)%, $(($gpu | Measure-Object w -Maximum).Maximum) W, memory at most $(($gpu | Measure-Object mib -Maximum).Maximum) MiB over $(@($gpu).Count) samples; load $mean% on average over the $($busyLoad.Count) samples above 0"
}
else { 'info  card: no samples' }
$cpuRows = @(Get-Content (Join-Path $l "card-run-$LogArm-cpu.txt") -ErrorAction SilentlyContinue | Where-Object { $_ -match 'cpu (\d+)C' })
if ($cpuRows) {
    $cpu = $cpuRows | ForEach-Object { if ($_ -match 'cpu (\d+)C') { [int]$matches[1] } }
    $verdicts = ($cpuRows | ForEach-Object { ($_ -split '\s+')[3] } | Sort-Object -Unique) -join ','
    "info  cpu $(($cpu | Measure-Object -Minimum).Minimum) to $(($cpu | Measure-Object -Maximum).Maximum) C over $($cpuRows.Count) reads, verdicts $verdicts"
} else { 'info  cpu: sampled at the start and the end only (the heat lines in the log)' }
if ($fails) { "VERDICT: FAILED on $($fails -join ', ')"; exit 1 }
'VERDICT: every condition holds'; exit 0
