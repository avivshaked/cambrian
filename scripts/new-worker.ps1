<#
.SYNOPSIS
  Clone the Unity project into sibling worker directories so runs can go in parallel.

.DESCRIPTION
  Two Unity processes cannot share one Library/ — they corrupt it, and the symptom arrives
  later as "Corrupted Library Detected" the next time a human opens the Editor. So parallel
  runs need one project directory each.

  What is copied is only Assets/, Packages/ and ProjectSettings/ — 0.4 MB. Library/ is NOT
  copied: it is 1.6 GB, it is regenerated from exactly those three inputs, and copying a
  Library that another process has open is how the corruption happens in the first place.
  Each worker therefore pays a one-time import on its first run, a few minutes, and is fast
  after that.

  Workers MUST be siblings of unity/ at the project root. Packages/manifest.json refers to
  the Core package as "file:../../src/Evosim.Core", resolved relative to Packages/ — so a
  worker one directory deeper would resolve outside the repository and fail to compile with
  an error that does not mention paths.

  Re-running is safe and is how a worker picks up source changes: the three directories are
  refreshed and Library/ is left alone.

.PARAMETER Workers
  Worker numbers to create. Worker N lives in unity-wN/.

.PARAMETER Clean
  Delete each worker's Library/ as well, forcing a full reimport. Only needed if a worker's
  Library is actually suspect — it costs minutes per worker.

.EXAMPLE
  ./scripts/new-worker.ps1 -Workers 2,3,4
#>
[CmdletBinding()]
param(
    [int[]]$Workers = @(2, 3),
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity'

if (-not (Test-Path $source)) { throw "No unity/ project at $source" }

# The three inputs Library/ is derived from. Anything else in unity/ is either generated
# (Library, Temp, Logs, obj) or per-user (UserSettings) and must not be cloned.
$parts = @('Assets', 'Packages', 'ProjectSettings')

foreach ($n in $Workers) {
    if ($n -eq 1) { throw "Worker 1 is unity/ itself. Number workers from 2." }

    $dest = Join-Path $root "unity-w$n"
    $isNew = -not (Test-Path $dest)

    if ($isNew) { New-Item -ItemType Directory -Path $dest | Out-Null }

    foreach ($part in $parts) {
        $from = Join-Path $source $part
        $to = Join-Path $dest $part

        # /MIR so a deleted script disappears from the worker too — a stale copy of a file
        # that no longer exists compiles, and would make a worker silently run old code.
        $null = robocopy $from $to /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $part (exit $LASTEXITCODE)" }
    }

    if ($Clean) {
        $lib = Join-Path $dest 'Library'
        if (Test-Path $lib) { Remove-Item $lib -Recurse -Force }
    }

    $hasLibrary = Test-Path (Join-Path $dest 'Library')
    $state = if ($isNew) { 'created' } elseif ($Clean) { 'refreshed, Library cleared' } else { 'refreshed' }
    $import = if ($hasLibrary) { 'warm' } else { 'will reimport on first run (minutes)' }

    Write-Host "unity-w$n : $state, $import"
}

Write-Host ''
Write-Host 'Run an arm against a worker with -projectPath <root>\unity-wN.'
Write-Host 'Never point two processes at the same worker.'
