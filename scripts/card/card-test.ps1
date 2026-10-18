# One test of Evosim.Farm.Tests on the card itself, alone on the machine and under the driver watch. The card's
# tests sit in Category Card and return at once unless their gate is set, so this sets -Gate to 1 for the one
# test -Filter names (a dotnet test filter, e.g. FullyQualifiedName~GpuTransportRound57Tests.TheTransportOnTheCard),
# with the test build as it stands (--no-build; build it first at 2 processors). It refuses another farm, test
# host or card script running, an nvlddmkm event in the last minute, heat at WARN, or logs of this -Name already
# there. The watch starts from its own settings file, its first line is read back (its end pattern masked in
# this log) and it is checked alive 30 s after it began and at every turn; a watch gone stops the test. The
# test host is pinned to -Mask (decimal; 4294901760 is logical 16-31) at BelowNormal and held to -Minutes;
# past it, its tree is killed. At the end: the test's verdict and output lines, the driver events, the heat.
# Log: scratch/logs/card-test-<Name>.log, last line "card test ends" or "card test STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Filter,
    [Parameter(Mandatory)][ValidatePattern('^EVOSIM_[A-Z0-9_]+$')][string]$Gate,
    [double]$Minutes = 6,
    [int64]$Mask = 4294901760)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$proj = Join-Path $root 'src\Evosim.Farm.Tests\Evosim.Farm.Tests.csproj'
$logs = Join-Path $root 'scratch\logs'
$log  = Join-Path $logs "card-test-$Name.log"
$out  = Join-Path $logs "card-test-$Name-test.log"
$watchLog = Join-Path $logs "card-test-$Name-watch.log"
New-Item -ItemType Directory -Force $logs, (Join-Path $root 'scratch\card') | Out-Null
foreach ($f in $log, $out, $watchLog) { if (Test-Path $f) { Write-Host "$f exists; rename the last session's logs first"; exit 1 } }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Heat { $l = (& pwsh -NoProfile -File (Join-Path $root 'scripts\temp-check.ps1') -Log | Select-Object -Last 1); $c = $LASTEXITCODE; Say "heat: $l"; return $c }
. (Join-Path $PSScriptRoot 'nvcuda-guard.ps1')
$me = [System.Diagnostics.Process]::GetCurrentProcess()
$me.ProcessorAffinity = [IntPtr]$Mask
$me.PriorityClass = 'BelowNormal'

Say "card test begins: $Name, $Filter, gate $Gate, head $(git -C $root rev-parse --short HEAD), deadline $Minutes min"
$busy = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
    ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-run|pace-read|driver-watch') })
if ($busy) { Say "card test STOPPED: busy: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"; exit 1 }
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = (Get-Date).AddMinutes(-1) } -ErrorAction SilentlyContinue)
if ($ev) { Say 'card test STOPPED: an nvlddmkm event in the last minute'; exit 1 }
if ((Heat) -ge 1) { Say 'card test STOPPED: the heat is at WARN or above'; exit 1 }

$start = Get-Date
$settings = Join-Path $root "scratch\card\card-test-$Name-watch.json"
[ordered]@{
    Since      = $start.AddMinutes(-1).ToString('s')
    Deadline   = $start.AddMinutes($Minutes + 3).ToString('s')
    JobLog     = $log
    EndPattern = 'card test over'
    StopLine   = 'card test STOPPED by the driver watch: nvlddmkm event'
    Controller = 'card-test\.ps1'
} | ConvertTo-Json | Set-Content $settings -Encoding utf8
$w = Start-Process pwsh -ArgumentList @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'driver-watch.ps1'), '-Settings', $settings) `
     -WindowStyle Hidden -PassThru -RedirectStandardOutput $watchLog -RedirectStandardError "$watchLog.err"
$d = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $d -and -not ((Test-Path $watchLog) -and (Get-Content $watchLog -TotalCount 1))) { Start-Sleep -Milliseconds 500 }
$first = if (Test-Path $watchLog) { Get-Content $watchLog -TotalCount 1 } else { '' }
Say "watch pid $($w.Id): $($first -replace 'card test over', '<end pattern>')"
if ($w.HasExited -or $first -notmatch "watch begins: .*end pattern 'card test over', controller 'card-test\\\.ps1'") {
    Stop-Process -Id $w.Id -Force -ErrorAction SilentlyContinue
    Say 'card test STOPPED: the watch did not begin as it should'; exit 1
}

# The card is needed: a CUDA_VISIBLE_DEVICES inherited from an off-card job would hide it.
Remove-Item Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue
Set-Item "Env:$Gate" '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$p = Start-Process $dotnet -ArgumentList @('test', $proj, '-c', 'Release', '--no-build', '--filter', $Filter,
        '--logger', 'console;verbosity=detailed') `
     -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError "$out.err"
Say "test pid $($p.Id)"

$stopBy = $start.AddMinutes($Minutes)
$checked = $false
while (-not $p.HasExited -and (Get-Date) -lt $stopBy) {
    foreach ($id in @(Get-ProcessTree $p.Id)) {
        try { $q = Get-Process -Id $id -ErrorAction Stop; $q.ProcessorAffinity = [IntPtr]$Mask; $q.PriorityClass = 'BelowNormal' } catch {}
    }
    if (-not $checked -and (Get-Date) -ge $start.AddSeconds(30)) {
        if ($w.HasExited) { break }
        Say "  watch alive after its first polls ($(Get-Date -Format 'HH:mm:ss'))"; $checked = $true
    }
    if ($w.HasExited) { break }
    Start-Sleep -Seconds 2
}
if (-not $p.HasExited) {
    $why = if ($w.HasExited) { 'the driver watch ended before the test' } else { "past its $Minutes-minute deadline" }
    foreach ($id in @(Get-ProcessTree $p.Id)) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    Say "card test STOPPED: $why; the test's tree killed"; exit 2
}
if (-not $checked) { Say "  watch alive at the end ($(-not $w.HasExited))" }
Say "test exit $($p.ExitCode)"
Select-String -Path $out -Pattern '^\s*(Passed|Failed) |Error Message|Assert|not run:|Total tests|Standard Output' -Context 0, 3 |
    ForEach-Object { Say "  $($_.Line.Trim())"; $_.Context.PostContext | Where-Object { $_.Trim() } | ForEach-Object { Say "    $($_.Trim())" } }
$ev = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $start.AddMinutes(-1) } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'nvlddmkm' -or ($_.ProviderName -eq 'Microsoft-Windows-Kernel-Power' -and $_.Id -eq 41) })
Say "driver events since $($start.AddMinutes(-1).ToString('HH:mm:ss')): $($ev.Count)"
[void](Heat)
Say "card test over: exit $($p.ExitCode)"
Say 'card test ends'
exit $p.ExitCode
