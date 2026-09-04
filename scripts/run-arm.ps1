<#
.SYNOPSIS
  Launch one evolution arm against a worker project.

.DESCRIPTION
  Every arm this project has run so far was launched by hand, which is why no two of them are
  reproducible from anything but a shell history. This is that invocation written down: the
  arm's name, the worker it runs on, and the environment that distinguishes it from the
  control. The settings reach the run through environment variables (EvolutionRun.Env), and
  the run records them in its own report header and its config hash — so an arm whose knob
  did not arrive is visible in the output rather than only in the absence of an effect.

  Never point two processes at the same worker: two Unity processes sharing one Library/
  corrupt it, and the symptom arrives later as "Corrupted Library Detected".

  Since the run manifest (the Sol/GPT review of 2026-09-03, finding 6) the run also writes
  runs/<Name>/<run>/run.json before its first step, carrying the git commit and a hash of the
  worker's own source. This script waits for that file and prints both, so what a worker was
  actually running is on screen at launch rather than reconstructed afterwards.

.PARAMETER Name
  Arm name. Becomes runs/<Name>.md, runs/<Name>/ and the log file name.

.PARAMETER Worker
  Worker number. 1 is unity/ itself; N>1 is unity-wN, created by scripts/new-worker.ps1.

.PARAMETER Settings
  Hashtable of EVOSIM_* overrides, e.g. @{ EVOSIM_LINK_PHOTO = 0.5 }.

.PARAMETER Seconds
  Simulated-second budget. Default 40000.

.PARAMETER WallMinutes
  Wall-clock budget. Default 600.

.PARAMETER ExpectSimHash
  Optional. The simHash the worker is expected to carry — refuse to launch if it does not.
  This is the four-file manual hash check (CLAUDE.md) turned into a precondition: a worker
  that was "refreshed" and was not is caught before it burns a day of machine time rather
  than after. Take the value from a previous run's run.json (source.simHash), or from this
  script's own output. A prefix of at least 8 characters is accepted.

.PARAMETER WaitForManifestMinutes
  How long to wait for the run to write its run.json before giving up on printing it.
  Default 10 — a cold worker reimports for minutes before it compiles. 0 skips the wait
  entirely and returns as soon as Unity is launched.

.EXAMPLE
  ./scripts/run-arm.ps1 -Name linkearn -Worker 6 -Settings @{ EVOSIM_LINK_PHOTO = 0.5 }

.EXAMPLE
  ./scripts/run-arm.ps1 -Name r17-s1 -Worker 7 -ExpectSimHash 3f9c1a2b
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][int]$Worker,
    [hashtable]$Settings = @{},
    [float]$Seconds = 40000,
    [float]$WallMinutes = 600,
    [uint64]$Seed = 1,   # not [ulong]: that accelerator is PowerShell 7 only
    [string]$ExpectSimHash,
    [int]$WaitForManifestMinutes = 10
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }

$proj = if ($Worker -eq 1) { Join-Path $root 'unity' } else { Join-Path $root "unity-w$Worker" }
if (-not (Test-Path $proj)) { throw "No worker project at $proj — run scripts/new-worker.ps1 -Workers $Worker" }

# Refuse rather than corrupt: a Library/ is not shareable, and the damage shows up later.
# The path must end where the argument ends: unity/ is a prefix of unity-w2/, and a prefix
# match refused worker 1 whenever any other worker was busy (logbook/0037).
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

<#
.SYNOPSIS
  The digest EvolutionRun.HashSourceTree computes, in PowerShell.

.DESCRIPTION
  Kept byte-compatible with the C# on purpose — the whole value of -ExpectSimHash is that the
  number this prints before a launch is the number the run will write into its own run.json.
  For every .cs under the root, in ordinal order of its path relative to the root with forward
  slashes: "<relative path>\n<sha256 of the file's bytes>\n". SHA-256 over that, lowercase hex.

  Filtered by extension rather than by a -Filter pattern: .NET's "*.cs" also matches longer
  extensions through 8.3 short names (the quirk that makes *.htm return .html), and a digest
  that included .csproj on one side and not the other would refuse every launch for a reason
  nobody could see.
#>
function Get-SourceTreeHash {
    param([Parameter(Mandatory)][string]$Root)

    if (-not (Test-Path $Root)) { return $null }

    $full = (Resolve-Path $Root).Path.TrimEnd('\')
    $files = @(Get-ChildItem -LiteralPath $full -Recurse -File |
        Where-Object { $_.Extension -ieq '.cs' })

    if ($files.Count -eq 0) { return $null }

    $rels = New-Object 'string[]' $files.Count
    $paths = New-Object 'string[]' $files.Count

    for ($i = 0; $i -lt $files.Count; $i++) {
        $rels[$i] = $files[$i].FullName.Substring($full.Length + 1).Replace('\', '/')
        $paths[$i] = $files[$i].FullName
    }

    # Ordinal, never culture-aware: Sort-Object would order by the operator's locale, and a
    # digest that depends on regional settings identifies nothing.
    [Array]::Sort($rels, $paths, [System.StringComparer]::Ordinal)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $sb = New-Object System.Text.StringBuilder
        for ($i = 0; $i -lt $rels.Length; $i++) {
            $bytes = [System.IO.File]::ReadAllBytes($paths[$i])
            $digest = -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
            [void]$sb.Append($rels[$i]).Append("`n").Append($digest).Append("`n")
        }

        $utf8 = New-Object System.Text.UTF8Encoding($false)
        return -join ($sha.ComputeHash($utf8.GetBytes($sb.ToString())) |
            ForEach-Object { $_.ToString('x2') })
    }
    finally { $sha.Dispose() }
}

$simHash = Get-SourceTreeHash (Join-Path $proj 'Assets\Evosim')
if (-not $simHash) { throw "No .cs under $proj\Assets\Evosim — is this a worker project?" }

if ($ExpectSimHash) {
    $expected = $ExpectSimHash.Trim().ToLowerInvariant()
    if ($expected.Length -lt 8) { throw "-ExpectSimHash needs at least 8 characters; got '$ExpectSimHash'." }

    if (-not $simHash.StartsWith($expected)) {
        throw @"
Worker $Worker carries source that is not what this arm expects, so it was NOT launched.
  expected simHash $expected...
  worker  simHash $simHash
Refresh it (scripts/new-worker.ps1 -Workers $Worker, from a shell, one worker per call) or
drop -ExpectSimHash if the difference is intended.
"@
    }
}

# Logs live inside the project (scratch/ is gitignored): nothing of a run is written outside
# the repository, TEMP included — the owner's rule, 2026-09-03.
$logDir = Join-Path $PSScriptRoot "..\scratch\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "evosim-$Name.log"
$out = Join-Path $root "runs/$Name.md"

# Set for this process only; Start-Process inherits it. Restored afterwards so a second arm
# launched from the same shell does not silently inherit the first one's settings — which is
# exactly how an arm ends up being a duplicate of its own control.
$saved = @{}
$env:EVOSIM_SECONDS = $Seconds
$env:EVOSIM_WALL_MINUTES = $WallMinutes
$env:EVOSIM_SEED = $Seed
$env:EVOSIM_OUT = $out

# The worker lives outside the repository, so the run cannot find the repository by walking up
# from itself — and the git commit in its manifest depends on knowing where the repository is.
# Without this it falls back to the worker's parent and says so in the manifest's note.
$env:EVOSIM_REPO_ROOT = $root

foreach ($k in $Settings.Keys) {
    $saved[$k] = [Environment]::GetEnvironmentVariable($k)
    Set-Item -Path "env:$k" -Value $Settings[$k]
}

$a = @('-projectPath', $proj, '-batchmode', '-quit', '-nographics',
       '-executeMethod', 'Evosim.Sim.EditorTools.EvolutionRun.Run', '-logFile', $log)

Write-Host "$Name -> worker $Worker ($proj)"
Write-Host "  seed $Seed, $Seconds s, $WallMinutes min wall"
foreach ($k in $Settings.Keys) { Write-Host "  $k = $($Settings[$k])" }
Write-Host "  log $log"
Write-Host "  out $out"
Write-Host "  worker simHash $simHash"

$launchedAt = (Get-Date).ToUniversalTime()
$process = Start-Process -FilePath $unity -ArgumentList $a -NoNewWindow -PassThru

foreach ($k in $saved.Keys) {
    if ($null -eq $saved[$k]) { Remove-Item "env:$k" -ErrorAction SilentlyContinue }
    else { Set-Item -Path "env:$k" -Value $saved[$k] }
}

# Wait for the manifest, then read the run's own account of what produced it. This is where a
# stale worker, a detached HEAD or uncommitted source becomes visible — at launch, on screen,
# rather than in a post-mortem months later.
if ($WaitForManifestMinutes -gt 0) {
    Write-Host "  waiting up to $WaitForManifestMinutes min for run.json (a cold worker reimports first)..."

    $deadline = (Get-Date).AddMinutes($WaitForManifestMinutes)
    $manifestPath = $null

    while ((Get-Date) -lt $deadline) {
        $candidate = Get-ChildItem -Path (Join-Path $root "runs\$Name") -Filter 'run.json' `
            -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $launchedAt.AddMinutes(-1) } |
            Sort-Object LastWriteTimeUtc | Select-Object -Last 1

        if ($candidate) { $manifestPath = $candidate.FullName; break }

        # A worker that failed to compile exits without ever writing one, and waiting ten
        # minutes for a process that is already gone helps nobody.
        if ($process.HasExited) {
            Write-Warning "Unity exited (code $($process.ExitCode)) before writing run.json. Check $log for 'error CS' and 'could not be found'."
            break
        }

        Start-Sleep -Seconds 5
    }

    if ($manifestPath) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

        Write-Host "  run.json $manifestPath"
        Write-Host "    status    $($manifest.status)"
        Write-Host "    gitCommit $($manifest.source.gitCommit)$(if ($manifest.source.gitDirty) { ' (DIRTY)' })"
        Write-Host "    simHash   $($manifest.source.simHash)"
        Write-Host "    coreHash  $($manifest.source.coreHash)"
        if ($manifest.source.note) { Write-Warning "run.json note: $($manifest.source.note)" }

        if ($manifest.source.simHash -ne $simHash) {
            Write-Warning "The run reports a different simHash from the one this script computed ($simHash). One of the two is reading a different worker."
        }
    }
    elseif (-not $process.HasExited) {
        Write-Warning "No run.json after $WaitForManifestMinutes min. The run may still be importing; check $log."
    }
}

Write-Host 'Launched. Watch with: Get-Content $log -Wait -Tail 5'
Write-Host "Stop it with: ./scripts/stop-arm.ps1 $Name -Reason manual-other"
