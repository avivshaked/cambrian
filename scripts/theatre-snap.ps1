<#
.SYNOPSIS
  Photograph a recorded run in the theatre: framed views at named simulated seconds, as PNGs.

.DESCRIPTION
  The owner opened round 33 in the theatre on 2026-09-10 and saw in a minute what thirty-three
  rounds of tables could not show: the world was two vertical ribbons about a metre wide in a
  box twenty metres long (logbook/0083). The agent that reads the rounds cannot open the Editor.
  This is the same view, taken as files it can read.

  It launches a batch Editor on a worker, which enters Play mode, replays the run with the
  identity check on, and at each requested simulated second renders the box from the side, the
  end, the top and a corner. Each picture carries a one line label with the arm, the second, the
  living count and the view, and says NOT A FAITHFUL REPLAY when this build is not the build
  that recorded the run.

  Two things about the command line matter and both are the opposite of every other launcher
  here. There is no -quit, because the entry point quits itself once the pictures are taken and
  -quit would end the process before Play mode ever started. There is no -nographics, because a
  picture is made on a graphics device; -batchmode alone keeps the window off the screen and the
  device present.

  Never point two processes at one worker: two Unity processes sharing one Library/ corrupt it,
  and the symptom arrives later as "Corrupted Library Detected" on the next open. This refuses a
  worker that already has a Unity process attached, and refuses worker 1 outright, since unity/
  is the project the owner keeps open in the Editor.

.PARAMETER Arm
  Arm name, as in runs/<Arm>. The newest run inside it is the one photographed.

.PARAMETER At
  Simulated seconds, ascending: -At 300,600. The world is stepped forward once and cannot go
  back for a picture, so the order is the caller's promise and a descending list is refused.

.PARAMETER Worker
  Worker number, default 6. Worker 1 is unity/ and is refused.

.PARAMETER Views
  Any of side, end, top, iso. All four by default. side looks along z (length by depth), end
  along x (width by depth), top straight down (length by width), iso from above one corner.

.PARAMETER Size
  Picture size as WxH. Default 1600x900. The whole box is fitted inside it, so a box taller than
  it is wide leaves the sides of a landscape frame empty: the campaign's 20 by 5 by 60 m water
  fills about a sixth of a 1600x900 side view and a twentieth of an end view. Ask for a portrait
  frame (-Size 900x1600) when the side or the end is the view that matters.

.PARAMETER Out
  Where the pictures go. Default scratch/snaps/<Arm>/. Must be inside the repository.

.PARAMETER AllowSourceMismatch
  Photograph a run this build did not record. What comes out is a plausible world rather than
  that run, and every label says so.

.PARAMETER WallMinutes
  Wall clock cap on the batch Editor, default 30.

.EXAMPLE
  ./scripts/theatre-snap.ps1 r35tsmoke3 -At 300,600

.EXAMPLE
  ./scripts/theatre-snap.ps1 r33-s3 -At 1000,5000,20000 -Worker 6 -Views side,top
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [Parameter(Mandatory)][string[]]$At,
    [int]$Worker = 6,
    [string[]]$Views = @(),
    [string]$Size = '1600x900',
    [string]$Out,
    [switch]$AllowSourceMismatch,
    [int]$WallMinutes = 30
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }

# A comma list arrives as one string when the script is called through "powershell -File", which
# is how six workers were once "refreshed" without any of them being refreshed (CLAUDE.md's
# new-worker gotcha). Splitting every element again costs nothing and makes both forms work.
function Split-List {
    param([string[]]$Values)

    $flat = @()
    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if ($trimmed) { $flat += $trimmed }
        }
    }
    return $flat
}

$timeList = @(Split-List $At)
if ($timeList.Count -eq 0) { throw "-At needs at least one simulated second, e.g. -At 300,600" }

# Parsed invariantly, never by the operator's locale: a machine whose decimal separator is a
# comma would read 300.5 as 3005 and photograph a second nobody asked for.
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$float = [System.Globalization.NumberStyles]::Float

$previous = $null
foreach ($t in $timeList) {
    $value = 0.0
    if (-not [double]::TryParse($t, $float, $invariant, [ref]$value)) { throw "-At: '$t' is not a number of seconds." }
    if ($value -le 0) { throw "-At: a time must be past zero; '$t' is not." }
    if ($null -ne $previous -and $value -le $previous) {
        throw "-At: times must ascend, and $value does not follow $previous."
    }
    $previous = $value
}

$viewNames = @(Split-List $Views)
$known = @('side', 'end', 'top', 'iso')
foreach ($v in $viewNames) {
    if ($known -notcontains $v.ToLowerInvariant()) {
        throw "-Views: '$v' is not a view. The views are side, end, top, iso."
    }
}

if ($Size -notmatch '^\d+[xX]\d+$') { throw "-Size: '$Size' is not a size. Write it as WxH." }

if ($Worker -eq 1) {
    throw "Worker 1 is unity/, which the owner keeps open in the Editor. Use a worker from 2 up."
}

$proj = Join-Path $root "unity-w$Worker"
if (-not (Test-Path $proj)) {
    throw "No worker project at $proj. Create it with scripts/new-worker.ps1 -Workers @($Worker) from a shell."
}

# Refuse rather than corrupt. The path must end where the argument ends: unity/ is a prefix of
# unity-w2/, and a prefix match once refused worker 1 whenever any other worker was busy.
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

# The lockfile is the same fact from the other side, and it outlives a process that was killed
# rather than stopped.
if (Test-Path (Join-Path $proj 'Temp/UnityLockfile')) {
    throw "unity-w$Worker has a Unity process open (Temp/UnityLockfile exists). Let its arm end, or stop it with stop-arm.ps1."
}

$runDirectory = Join-Path $root "runs\$Arm"
if (-not (Test-Path $runDirectory)) { throw "No run directory at $runDirectory" }

if ($Out) {
    $snapDirectory = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
} else {
    $snapDirectory = Join-Path $root "scratch\snaps\$Arm"
}

# Logs live inside the project (scratch/ is gitignored): nothing of a run is written outside the
# repository, TEMP included, which is the owner's rule of 2026-09-03.
$logDirectory = Join-Path $root 'scratch\logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory "theatre-snap-$Arm.log"

# Set for this process only; Start-Process inherits it. Restored in a finally so that a second
# call from the same shell cannot silently inherit the first one's times or views.
$names = @(
    'EVOSIM_THEATRE_RUN', 'EVOSIM_THEATRE_SNAP_TIMES', 'EVOSIM_THEATRE_SNAP_VIEWS',
    'EVOSIM_THEATRE_SNAP_OUT', 'EVOSIM_THEATRE_SNAP_SIZE', 'EVOSIM_THEATRE_WALL_MINUTES',
    'EVOSIM_THEATRE_OVERRIDE', 'EVOSIM_THEATRE_SEEK', 'EVOSIM_REPO_ROOT')

$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }

try {
    $env:EVOSIM_THEATRE_RUN = $runDirectory
    $env:EVOSIM_THEATRE_SNAP_TIMES = ($timeList -join ',')
    $env:EVOSIM_THEATRE_SNAP_OUT = $snapDirectory
    $env:EVOSIM_THEATRE_SNAP_SIZE = $Size
    $env:EVOSIM_THEATRE_WALL_MINUTES = $WallMinutes
    $env:EVOSIM_REPO_ROOT = $root

    if ($viewNames.Count -gt 0) { $env:EVOSIM_THEATRE_SNAP_VIEWS = ($viewNames -join ',') }
    else { Remove-Item env:EVOSIM_THEATRE_SNAP_VIEWS -ErrorAction SilentlyContinue }

    if ($AllowSourceMismatch) { $env:EVOSIM_THEATRE_OVERRIDE = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_OVERRIDE -ErrorAction SilentlyContinue }

    # The theatre seeks with the camera off, which would skip the world past the first picture.
    Remove-Item env:EVOSIM_THEATRE_SEEK -ErrorAction SilentlyContinue

    Write-Host "$Arm -> worker $Worker ($proj)"
    Write-Host "  run    $runDirectory"
    Write-Host "  at     $($timeList -join ', ') s"
    Write-Host "  views  $(if ($viewNames.Count -gt 0) { $viewNames -join ', ' } else { 'side, end, top, iso' })"
    Write-Host "  size   $Size"
    Write-Host "  out    $snapDirectory"
    Write-Host "  log    $log"
    if ($AllowSourceMismatch) { Write-Host "  source mismatch allowed: the pictures are of a cousin world, and say so" }

    # No -quit and no -nographics. See the description.
    $arguments = @(
        '-projectPath', $proj, '-batchmode',
        '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreSnapshot.Run',
        '-logFile', $log)

    $process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru -Wait
    $code = $process.ExitCode
} finally {
    foreach ($name in $names) {
        if ($null -eq $saved[$name]) { Remove-Item "env:$name" -ErrorAction SilentlyContinue }
        else { Set-Item -Path "env:$name" -Value $saved[$name] }
    }
}

Write-Host ''
Write-Host "Unity exited with code $code."

$pictures = @(Get-ChildItem -Path $snapDirectory -Filter '*.png' -File -ErrorAction SilentlyContinue |
    Sort-Object Name)

if ($pictures.Count -eq 0) {
    Write-Warning "No pictures in $snapDirectory."
} else {
    Write-Host ''
    foreach ($picture in $pictures) {
        Write-Host ("  {0,-44} {1,9:N0} bytes" -f $picture.Name, $picture.Length)
    }
    Write-Host ''
    Write-Host "  $($pictures.Count) file(s) in $snapDirectory"
}

if ($code -ne 0) {
    Write-Host ''
    Write-Warning "The snapshot failed. The last lines of $log :"
    Get-Content $log -Tail 30 | ForEach-Object { Write-Host "  $_" }
}

exit $code
