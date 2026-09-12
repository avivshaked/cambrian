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

.PARAMETER Refresh
  Refresh each worker (new-worker.ps1, which copies Assets/, ProjectSettings/ and Packages/
  from unity/ and leaves Library/ alone) just before launching on it. For a queue that
  starts while older builds still run on some of its workers: a worker freed by a render of
  an earlier round carries that round's tree, and the -ExpectSimHash check would refuse it.
  The refresh runs only on a worker no Unity process is using, which is the only kind the
  queue launches on anyway.

.PARAMETER Prereg
  Path to a pre-registration file, relative to the repo root (script-contracts-spec.md
  section 4). Optional; when given, the queue refuses to launch anything unless the file is
  tracked and clean (`git status --porcelain` reports nothing for it) and has at least one
  commit -- an unsaved or uncommitted pre-registration is not one. The commit is printed on
  every launch line, and after each seed launches this writes
  `runs/<arm>/prereg.json` (`{ "file", "commit", "launchedAt" }`), naming the arm read out of
  the launcher's own "<arm> -> worker <n> (...)" output line. Omit it and the queue behaves
  exactly as before this parameter existed.

.EXAMPLE
  ./scripts/launch-queue.ps1 -Launcher rounds/launch-r34.ps1 -Seeds 1,2,3,4,5 -Workers 7,2,3,4,5,6 -ExpectSimHash b5d31a48

.EXAMPLE
  ./scripts/launch-queue.ps1 -Launcher rounds/launch-r38.ps1 -Seeds 1,2,3,4,5 -Prereg logbook/specs/r38-prereg.json

.EXAMPLE
  ./scripts/launch-queue.ps1 -Launcher rounds/launch-r37b.ps1 -Seeds 1,2,3,4,5 -Workers 5,6,2,3,4,7 -Refresh -ExpectSimHash 13a906a3 -Prereg logbook/0095-the-water-carried-as-water.md
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$Launcher,
    [Parameter(Mandatory)][int[]]$Seeds,
    [int[]]$Workers = @(7, 2, 3, 4, 5, 6),
    [string]$ExpectSimHash = '',
    [int]$MaxUnity = 5,
    [int]$PollSeconds = 60,
    [string]$Prereg = '',
    [switch]$Refresh
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$launcherPath = Join-Path $root $Launcher
if (-not (Test-Path $launcherPath)) { throw "No launcher at $launcherPath" }
if ($Workers -contains 1) { throw "Worker 1 is unity/, the owner's Editor. Use workers from 2 up." }

# The pre-registration check: a launch is refused unless the named file is tracked and
# clean and has a commit, so a round cannot run against a prereg nobody can later produce.
# Read once, up front, because the check does not depend on which seed or worker is next.
$preregCommit = $null
if ($Prereg -ne '') {
    $preregFull = Join-Path $root $Prereg
    if (-not (Test-Path -LiteralPath $preregFull)) {
        throw "Prereg file not found: $preregFull"
    }

    Push-Location $root
    try {
        $statusOut = @(git status --porcelain -- $Prereg 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "git status failed for ${Prereg}: $($statusOut -join ' ')"
        }
        $dirty = @($statusOut | Where-Object { $_.Trim() -ne '' })
        if ($dirty.Count -gt 0) {
            throw "Prereg file $Prereg is not tracked and clean (git status --porcelain: " +
                  "$($dirty -join '; ')). Commit it before launching."
        }

        $commitOut = @(git log -1 --format=%H -- $Prereg 2>&1)
        $commitText = ($commitOut -join '').Trim()
        if ($LASTEXITCODE -ne 0 -or $commitText -eq '') {
            throw "Prereg file $Prereg has no commit in git log. Commit it before launching."
        }
        $preregCommit = $commitText
    } finally {
        Pop-Location
    }
}

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
        $preregNote = if ($null -ne $preregCommit) { " (prereg $Prereg @ $preregCommit)" } else { '' }

        # -WhatIf never reaches the launcher, so it can never start Unity -- the one thing
        # the exercise of this flag is not allowed to do.
        if (-not $PSCmdlet.ShouldProcess("seed $seed on worker $worker", 'launch')) {
            Write-Output "$(Stamp) WHATIF: would launch seed $seed on worker $worker ($editors editors running)$preregNote"
            $pending.RemoveAt(0)
            $launchedThisPass++
            continue
        }

        Write-Output "$(Stamp) launching seed $seed on worker $worker ($editors editors running)$preregNote"
        try {
            if ($Refresh) {
                $refreshed = @(& (Join-Path $PSScriptRoot 'new-worker.ps1') -Workers @($worker) *>&1 | ForEach-Object { [string]$_ })   # *>&1: it reports with Write-Host
                $refreshed | ForEach-Object { Write-Output "    $_" }
                if (($refreshed -join "`n") -notmatch 'refreshed|created') {
                    throw "new-worker.ps1 did not report worker $worker refreshed."
                }
            }
            $extra = @{}
            if ($ExpectSimHash -ne '') { $extra.ExpectSimHash = $ExpectSimHash }
            $said = @(& $launcherPath -Seed $seed -Worker $worker @extra 2>&1 | ForEach-Object { [string]$_ })
            $said | ForEach-Object { Write-Output "    $_" }
            # run-arm.ps1 warns rather than throws when Unity exits before the manifest, so the
            # launcher returns normally from a launch that did not happen (round 34 seed 1,
            # 2026-09-10: Core refused the config and the queue counted the seed as launched).
            if (($said -join "`n") -match 'before writing run\.json|could not be found|error CS') {
                throw "Unity exited before writing run.json; see scratch/logs for the arm's log."
            }
            $pending.RemoveAt(0)
            $launchedThisPass++
            Write-Output "$(Stamp) launched seed $seed on worker $worker"

            if ($null -ne $preregCommit) {
                # The arm name is not something this queue otherwise knows -- it is the
                # round launcher's own naming choice -- so it is read from run-arm.ps1's
                # first line of output, "<arm> -> worker <n> (<project path>)".
                $armLine = $said | Where-Object { $_ -match '^\S+ -> worker \d+' } | Select-Object -First 1
                if ($null -eq $armLine) {
                    Write-Output "$(Stamp) WARNING: could not read the arm name from the launcher's output; prereg.json not written for seed $seed"
                } else {
                    $armName = ($armLine -split ' ')[0]
                    $armDir = Join-Path $root "runs\$armName"
                    if (-not (Test-Path -LiteralPath $armDir)) { New-Item -ItemType Directory -Path $armDir -Force | Out-Null }
                    $preregRecord = [ordered]@{
                        file       = $Prereg
                        commit     = $preregCommit
                        launchedAt = (Get-Date).ToString('o')
                    }
                    $preregPath = Join-Path $armDir 'prereg.json'
                    [System.IO.File]::WriteAllText($preregPath, ($preregRecord | ConvertTo-Json))
                    Write-Output "$(Stamp) wrote $preregPath"
                }
            }
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
