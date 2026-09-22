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

  Two farms since 2026-09-22, and the second one is not stopped by killing anything. A run whose
  run.json says engine "dynamics" is src/Evosim.Farm's, a .NET console with no Unity process to
  find: it reads a file named STOP in its own run directory between report rows and ends itself
  in an orderly way, writing status "stopped", that file's first line as the reason, and its
  last row and snapshot first. So the farm branch writes STOP and leaves the manifest alone —
  merging "stopped" in here would race the program's own rewrite and could lose the ending
  block, which is the half nothing can reconstruct. The Unity branch is unchanged.

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
  recorded — the fallback for a run that died before writing a manifest at all. Unity only.

.PARAMETER RunsRoot
  Where the arms live. Default runs/ under the repository, which is where both farms write;
  a relative path resolves against the repository, not against the shell's directory. It is
  here because the console farm's acceptance runs land elsewhere (EVOSIM_RUNS_ROOT) and an
  arm that cannot be found cannot be stopped.

.PARAMETER WhatIf
  Report what would be stopped and change nothing. On a farm arm that means the STOP file is
  named and not written.

.EXAMPLE
  ./scripts/stop-arm.ps1 r17-s3 -Reason manual-futility

.EXAMPLE
  ./scripts/stop-arm.ps1 r17-s3 -Reason manual-stall -WhatIf

.EXAMPLE
  ./scripts/stop-arm.ps1 r42farm2-s1 -RunsRoot scratch/farm-port/runs -Reason manual-futility
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)][string]$Name,
    [ValidateSet('manual-futility', 'manual-stall', 'manual-other')]
    [string]$Reason = 'manual-other',
    [int]$Worker = 0,
    [string]$RunsRoot = 'runs'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

$runsRootPath = $RunsRoot
if (-not [System.IO.Path]::IsPathRooted($runsRootPath)) {
    $runsRootPath = Join-Path $root $RunsRoot
}
$armDir = Join-Path $runsRootPath $Name

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

# The farm out of Unity. Told apart by what the run itself recorded, never by the arm's name or
# by which directory it sits in: the engine is a fact of the run and the other two are habits.
if ($manifest -and $manifest.engine -eq 'dynamics') {
    $stopPath = Join-Path $runDir.FullName 'STOP'

    Write-Host "$Name"
    Write-Host "  run     $($runDir.FullName)"
    Write-Host "  engine  dynamics (src/Evosim.Farm), threads $($manifest.threads), pid $($manifest.processId)"
    Write-Host "  stop    $stopPath"
    Write-Host "  reason  $Reason"

    if ($manifest.status -ne 'running') {
        Write-Warning "run.json already says status '$($manifest.status)' — the run finished on its own. Nothing to stop."
        return
    }

    if (Test-Path -LiteralPath $stopPath) {
        Write-Warning "A STOP file is already there; the run ends at its next report row. Rewriting it with this reason."
    }

    if ($PSCmdlet.ShouldProcess($stopPath, "write STOP / $Reason")) {
        # The reason on the first line, which is all the program reads (Program.StopReason), and
        # a note under it for whoever finds the file afterwards. No BOM: the program reads lines,
        # and a BOM would ride on the first one and become part of the reason.
        $text = "$Reason`nWritten by scripts/stop-arm.ps1 at " +
            [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture) + "`n"
        [System.IO.File]::WriteAllText($stopPath, $text, (New-Object System.Text.UTF8Encoding($false)))

        Write-Host "  STOP written — the run ends at its next report row and rewrites run.json itself."
        Write-Host "  Watch for it: (Get-Content '$(Join-Path $runDir.FullName 'run.json')' -Raw | ConvertFrom-Json).status"
    }

    return
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
