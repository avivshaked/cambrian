<#
.SYNOPSIS
  Launch a round's seeds one at a time, each as a worker frees up, under the machine's cap.

.DESCRIPTION
  A round is five arms and the machine takes five Unity processes at once (memory: seven ran
  it hot). When the workers are busy with something that ends on its own, a render or the
  previous round, the seeds have to be launched by hand as each worker frees up, which means
  a person watching. This is that watching written down: every minute it counts the Unity
  editors running, and while the count is under -MaxUnity it takes the next pending seed and
  the first listed worker that has no lock file and no process, and calls the round's launcher
  for that seed on that worker. It exits when every seed is launched.

  Asset import workers are Unity.exe processes too; they are not counted, since each belongs
  to an editor that is.

.PARAMETER Launcher
  The round's launcher, e.g. rounds/launch-r34.ps1. Called as
  & $Launcher -Seed <n> -Worker <w> -ExpectSimHash <hash>, so it has to take those three.

.PARAMETER Seeds
  The seeds still to launch, in order.

.PARAMETER Workers
  Workers to try, in order of preference. Worker 1 is unity/ and is refused.

.PARAMETER ExpectSimHash
  Passed to the launcher, which refuses a worker whose source does not hash to it.

.PARAMETER MaxUnity
  The cap on Unity editors at once, counting the ones already running. Default 5.

.EXAMPLE
  ./scripts/launch-queue.ps1 -Launcher rounds/launch-r34.ps1 -Seeds 1,2,3,4,5 -Workers 7,2,3,4,5,6 -ExpectSimHash b5d31a48
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Launcher,
    [Parameter(Mandatory)][int[]]$Seeds,
    [int[]]$Workers = @(7, 2, 3, 4, 5, 6),
    [string]$ExpectSimHash = '',
    [int]$MaxUnity = 5,
    [int]$PollSeconds = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$launcherPath = Join-Path $root $Launcher
if (-not (Test-Path $launcherPath)) { throw "No launcher at $launcherPath" }
if ($Workers -contains 1) { throw "Worker 1 is unity/, the owner's Editor. Use workers from 2 up." }

function Get-UnityCommandLines {
    @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object { [string]$_.CommandLine })
}

function Stamp { (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') }

$pending = [System.Collections.Generic.List[int]]::new()
foreach ($s in $Seeds) { $pending.Add($s) }
Write-Output "$(Stamp) queue: seeds $($Seeds -join ',') on workers $($Workers -join ',') under a cap of $MaxUnity"

while ($pending.Count -gt 0) {
    $lines = Get-UnityCommandLines
    $editors = @($lines | Where-Object { $_ -notmatch 'AssetImportWorker' }).Count
    $launchedThisPass = 0

    while ($pending.Count -gt 0 -and ($editors + $launchedThisPass) -lt $MaxUnity) {
        $worker = $null
        foreach ($w in $Workers) {
            $lock = Join-Path $root "unity-w$w\Temp\UnityLockfile"
            $busy = @($lines | Where-Object { $_ -match "unity-w$w(\\|/|`"|\s|$)" }).Count -gt 0
            if (-not (Test-Path $lock) -and -not $busy) { $worker = $w; break }
        }
        if ($null -eq $worker) { break }

        $seed = $pending[0]
        Write-Output "$(Stamp) launching seed $seed on worker $worker ($editors editors running)"
        try {
            $extra = @{}
            if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
            & $launcherPath -Seed $seed -Worker $worker @extra 2>&1 | ForEach-Object { Write-Output "    $_" }
            $pending.RemoveAt(0)
            $launchedThisPass++
            Write-Output "$(Stamp) launched seed $seed on worker $worker"
        } catch {
            Write-Output "$(Stamp) FAILED seed $seed on worker $worker : $($_.Exception.Message)"
            Write-Output "$(Stamp) queue stopped; $($pending.Count) seed(s) not launched: $($pending -join ',')"
            exit 1
        }
        # The new process needs a moment to take its lock file before the next pass reads it.
        Start-Sleep -Seconds 20
        $lines = Get-UnityCommandLines
    }

    if ($pending.Count -gt 0) { Start-Sleep -Seconds $PollSeconds }
}

Write-Output "$(Stamp) queue done: every seed launched"
