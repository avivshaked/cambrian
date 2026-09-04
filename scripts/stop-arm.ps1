<#
.SYNOPSIS
  Stop a running arm and record in its run.json that a person stopped it.

.DESCRIPTION
  Killing a Unity process leaves a run whose every file is valid and whose manifest still says
  "running" — indistinguishable, a month later, from an arm that crashed, from one that hung,
  and from one still going. That is the gap the Sol/GPT review of 2026-09-03 (finding 6) named:
  the record has no vocabulary for "we decided to stop this".

  So this writes first and kills second. It merges status "stopped", the reason and the moment
  into the run's own run.json — keeping every field the run wrote at creation, because those
  are the facts nothing can reconstruct afterwards — and only then ends the process.

  This is the sanctioned way to stop an arm. Stop-Process by hand does the killing and none of
  the recording.

  A stall is a suspicion, not a verdict (CLAUDE.md): before stopping an arm for silence,
  confirm it with the discriminator — the report's byte size and the process's cumulative CPU,
  sampled 90 s apart. Zero byte growth AND high CPU delta is wedged; a new row, or a quiet CPU,
  is slow-but-alive. After killing a wedged worker, refresh it: the Library dies with the
  process.

.PARAMETER Name
  Arm name, as given to run-arm.ps1. Its newest run under runs/<Name>/ is the one stopped.

.PARAMETER Reason
  Why. manual-futility (the arm has answered its question, or clearly will not),
  manual-stall (it is wedged — confirm with the discriminator first), manual-other.

.PARAMETER Worker
  Optional. Locate the Unity process by worker number instead of by the workerPath the run
  recorded — the fallback for a run that died before writing a manifest at all.

.PARAMETER WhatIf
  Report what would be stopped and change nothing.

.EXAMPLE
  ./scripts/stop-arm.ps1 r17-s3 -Reason manual-futility

.EXAMPLE
  ./scripts/stop-arm.ps1 r17-s3 -Reason manual-stall -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)][string]$Name,
    [ValidateSet('manual-futility', 'manual-stall', 'manual-other')]
    [string]$Reason = 'manual-other',
    [int]$Worker = 0
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$armDir = Join-Path $root "runs\$Name"

# The newest run directory for this arm. Directories are named
# <yyyy-MM-dd-HHmmss>-<configHash8> (RunDirectory.Create), so name order is time order and the
# last one is the launch anybody means by "the arm".
$runDir = $null
if (Test-Path $armDir) {
    $runDir = Get-ChildItem -LiteralPath $armDir -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name | Select-Object -Last 1
}

$manifestPath = if ($runDir) { Join-Path $runDir.FullName 'run.json' } else { $null }
$manifest = $null

if ($manifestPath -and (Test-Path $manifestPath)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}
else {
    Write-Warning "No run.json under $armDir — the run never got far enough to write one. Nothing will be recorded; pass -Worker N to stop the process anyway."
}

# Which worker to kill. The manifest's own answer first: it is what the run itself believed it
# was running in, which is the only reading that cannot be wrong about a rename.
$proj = $null
if ($Worker -gt 0) {
    $proj = if ($Worker -eq 1) { Join-Path $root 'unity' } else { Join-Path $root "unity-w$Worker" }
}
elseif ($manifest -and $manifest.source -and $manifest.source.workerPath) {
    $proj = $manifest.source.workerPath
}

if (-not $proj) { throw "Cannot tell which worker '$Name' is on. Pass -Worker N." }

# The same end-anchored match run-arm.ps1 uses to refuse a busy worker, and for the same
# reason: unity/ is a prefix of unity-w2/, so an unanchored match would find the wrong process
# (logbook/0037).
$target = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }

if (-not $target) {
    Write-Warning "No Unity process has $proj open — the arm has already stopped."
}

Write-Host "$Name"
Write-Host "  run     $(if ($runDir) { $runDir.FullName } else { '(none)' })"
Write-Host "  worker  $proj"
Write-Host "  process $(if ($target) { ($target.ProcessId -join ', ') } else { '(none)' })"
Write-Host "  reason  $Reason"

if ($manifest -and $manifest.status -ne 'running') {
    Write-Warning "run.json already says status '$($manifest.status)' — the run finished on its own. Its manifest will not be overwritten."
    $manifest = $null
}

if ($manifest) {
    if ($PSCmdlet.ShouldProcess($manifestPath, "record status stopped / $Reason")) {
        # Merge, never rewrite: every creation field — seed, config hash, source identity —
        # is a fact nothing downstream can reconstruct, and this script knows none of them.
        $manifest | Add-Member -NotePropertyName 'status' -NotePropertyValue 'stopped' -Force
        $manifest | Add-Member -NotePropertyName 'reason' -NotePropertyValue $Reason -Force
        $manifest | Add-Member -NotePropertyName 'stoppedAt' `
            -NotePropertyValue ([DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)) -Force

        # Through a temporary file and a move, like the run's own writes: the process being
        # stopped may still be alive, and a reader must never catch a half-written manifest.
        # -Depth because Windows PowerShell's ConvertTo-Json flattens below depth 2, which
        # would turn the whole source block into a type name.
        $tmp = "$manifestPath.tmp"
        $json = $manifest | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $tmp -Destination $manifestPath -Force

        Write-Host "  run.json updated"
    }
}

foreach ($p in $target) {
    if ($PSCmdlet.ShouldProcess("PID $($p.ProcessId) ($proj)", 'stop')) {
        Stop-Process -Id $p.ProcessId -Force -Confirm:$false
        Write-Host "  stopped PID $($p.ProcessId)"
    }
}

if ($target -and $Reason -eq 'manual-stall') {
    Write-Host ''
    Write-Host 'A wedged worker loses its Library with the process. Refresh it before reusing:'
    Write-Host "  ./scripts/new-worker.ps1 -Workers $(Split-Path $proj -Leaf | ForEach-Object { $_ -replace '^unity-w', '' })"
}
