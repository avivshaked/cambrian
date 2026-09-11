<#
.SYNOPSIS
  Photograph a list of arms one at a time, each as its run ends and a worker frees, under the cap.

.DESCRIPTION
  The pictures a round's entry needs (three times, five views, per seed) are a replay of each
  run, which costs about what the run cost and needs a worker of its own. This is
  launch-queue.ps1's idea for renders: every minute it counts the Unity editors running, and
  while the count is under -MaxUnity it takes the next arm whose report carries its Ended
  footer and the first listed worker with no lock file and no process, and starts
  theatre-snap.ps1 for that arm on that worker, detached, with its output in scratch/logs.
  It exits when every arm has been started. It does not wait for a render to finish; the
  render's own log and the pictures say when it has.

.PARAMETER Arms
  Arms to photograph, in order.

.PARAMETER Workers
  Workers to use, in order of preference. Worker 1 is refused.

.PARAMETER At
  Simulated seconds for the pictures, ascending. Default 5000,15000,30000.

.PARAMETER Views
  Default side,end,top,iso,close.

.PARAMETER MaxUnity
  The cap on Unity editors at once, counting the ones already running. Default 5.

.EXAMPLE
  ./scripts/render-queue.ps1 -Arms r34-s3,r34-s2,r34-s4,r34-s5 -Workers 4,2,3,7
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Arms,
    [int[]]$Workers = @(7, 2, 3, 4, 5, 6),
    [string[]]$At = @('5000', '15000', '30000'),
    [string[]]$Views = @('side', 'end', 'top', 'iso', 'close'),
    [int]$MaxUnity = 5,
    [int]$WallMinutes = 720,
    [int]$PollSeconds = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($Workers -contains 1) { throw "Worker 1 is unity/, the owner's Editor. Use workers from 2 up." }

function Get-UnityCommandLines {
    @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object { [string]$_.CommandLine })
}
function Stamp { (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') }

$flatArms = @($Arms | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$pending = [System.Collections.Generic.List[string]]::new()
foreach ($a in $flatArms) { $pending.Add($a) }
$atList = ($At | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ','
$viewList = ($Views | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ','
Write-Output "$(Stamp) render queue: $($flatArms -join ',') at $atList on workers $($Workers -join ',') under a cap of $MaxUnity"

while ($pending.Count -gt 0) {
    $lines = Get-UnityCommandLines
    $editors = @($lines | Where-Object { $_ -notmatch 'AssetImportWorker' }).Count
    $started = 0

    foreach ($arm in @($pending)) {
        if (($editors + $started) -ge $MaxUnity) { break }
        $report = Join-Path $root "runs\$arm.md"
        if (-not (Test-Path $report)) { continue }
        if (-not (Select-String -Path $report -Pattern '\*\*Ended:\*\*' -Quiet)) { continue }

        $worker = $null
        foreach ($w in $Workers) {
            $lock = Join-Path $root "unity-w$w\Temp\UnityLockfile"
            $busy = @($lines | Where-Object { $_ -match "unity-w$w(\\|/|`"|\s|$)" }).Count -gt 0
            if (-not (Test-Path $lock) -and -not $busy) { $worker = $w; break }
        }
        if ($null -eq $worker) { break }

        $out = Join-Path $root "scratch\logs\theatre-snap-$arm.out"
        $err = Join-Path $root "scratch\logs\theatre-snap-$arm.err"
        $cmd = "& './scripts/theatre-snap.ps1' $arm -At $atList -Worker $worker -Views $viewList -WallMinutes $WallMinutes"
        Start-Process -FilePath pwsh -ArgumentList @('-NoProfile', '-Command', $cmd) -WorkingDirectory $root `
            -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden | Out-Null
        Write-Output "$(Stamp) started render of $arm on worker $worker ($editors editors running)"
        [void]$pending.Remove($arm)
        $started++
        Start-Sleep -Seconds 30
        $lines = Get-UnityCommandLines
    }

    if ($pending.Count -gt 0) { Start-Sleep -Seconds $PollSeconds }
}

Write-Output "$(Stamp) render queue done: every arm started"
