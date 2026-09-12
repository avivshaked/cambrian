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
  Any of side, end, top, iso, close, sky. The first four by default. sky looks up from three
  metres under the surface at the box's centre (the skin's fourth day), never by default. side looks along z (length by
  depth), end along x (width by depth), top straight down (length by width), iso from above one
  corner.

  close is the odd one and is never taken unless it is named. The other four frame the whole box,
  which at 1600 px across the campaign's 20 m water is about eighty pixels to a metre and
  twenty-four to a 0.3 m creature: enough to say where a body is and which guild it is, and
  nowhere near enough to say what its surface does. So close throws the census away and frames
  the largest body in the world together with the largest of its neighbours, from a three quarter
  angle, with no box outline and no markers. It is a portrait of one crowd and never a sample:
  nothing about the population or the spread can be read from it.

.PARAMETER Carve
  How deep the skin cuts each body inward, as a fraction of the part's smallest half-extent.
  Default 0.35, the depth the owner chose from the close views on 2026-09-11, clamped at 0.5 by
  the theatre. The displacement is never positive, so no setting can put a visual outside its
  collider; what it changes is how grown rather than built a body looks. Rendering the same close
  view at 0.1, 0.2 and 0.35 is how the depth was chosen, because it is a question about a picture
  and not about a number.

.PARAMETER Size
  Picture size as WxH. Default 1600x900. The whole box is fitted inside it, so a box taller than
  it is wide leaves the sides of a landscape frame empty: the campaign's 20 by 5 by 60 m water
  fills about a sixth of a 1600x900 side view and a twentieth of an end view. Ask for a portrait
  frame (-Size 900x1600) when the side or the end is the view that matters.

.PARAMETER Out
  Where the pictures go. Default scratch/snaps/<Arm>/. Must be inside the repository.

.PARAMETER Chrome
  Leave the theatre's interface on, and take one extra picture of the screen with it.

  The four framed views never carry it. They are rendered through a camera of the theatre's own
  into a RenderTexture, and a screen-space UI panel does not draw into one — which is also why
  each view's label is stamped into the pixels by hand. So -Chrome adds a fifth file,
  <arm>-t<second>-chrome.png, which is a capture of the Game View at whatever size the batch
  Editor's view is: a picture of the interface, not a framed view of the world, and -Size does
  not reach it.

  Off by default, and deliberately: every picture in logbook/images/ carries the burnt-in label
  and no chrome, and a frame that suddenly grew a census panel would not be comparable with any
  of them. This switch is how the interface itself is photographed for review.

.PARAMETER AllowSourceMismatch
  Photograph a run this build did not record. What comes out is a plausible world rather than
  that run, and every label says so.

.PARAMETER WallMinutes
  Wall clock cap on the batch Editor, default 30.

.EXAMPLE
  ./scripts/theatre-snap.ps1 r35tsmoke3 -At 300,600

.EXAMPLE
  ./scripts/theatre-snap.ps1 r33-s3 -At 1000,5000,20000 -Worker 6 -Views side,top

.EXAMPLE
  ./scripts/theatre-snap.ps1 r35-s1 -At 5000 -Views close -Carve 0.35 -Worker 7
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [Parameter(Mandatory)][string[]]$At,
    [int]$Worker = 6,
    [string[]]$Views = @(),
    [string]$Size = '1600x900',
    [double]$Carve = 0.35,
    [string]$Out,
    [switch]$Chrome,
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
$known = @('side', 'end', 'top', 'iso', 'close', 'sky')
foreach ($v in $viewNames) {
    if ($known -notcontains $v.ToLowerInvariant()) {
        throw "-Views: '$v' is not a view. The views are side, end, top, iso, close, sky."
    }
}

if ($Size -notmatch '^\d+[xX]\d+$') { throw "-Size: '$Size' is not a size. Write it as WxH." }

# Refused here rather than clamped in the theatre, so that a launcher's own record of what it
# asked for is the number that was used.
if ($Carve -lt 0 -or $Carve -gt 0.5) { throw "-Carve: $Carve is outside 0 to 0.5." }

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
    'EVOSIM_THEATRE_OVERRIDE', 'EVOSIM_THEATRE_SEEK', 'EVOSIM_REPO_ROOT',
    'EVOSIM_THEATRE_CARVE', 'EVOSIM_THEATRE_CHROME')

$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }

try {
    $env:EVOSIM_THEATRE_RUN = $runDirectory
    $env:EVOSIM_THEATRE_SNAP_TIMES = ($timeList -join ',')
    $env:EVOSIM_THEATRE_SNAP_OUT = $snapDirectory
    $env:EVOSIM_THEATRE_SNAP_SIZE = $Size
    $env:EVOSIM_THEATRE_WALL_MINUTES = $WallMinutes
    $env:EVOSIM_REPO_ROOT = $root
    $env:EVOSIM_THEATRE_CARVE = $Carve.ToString([System.Globalization.CultureInfo]::InvariantCulture)

    if ($viewNames.Count -gt 0) { $env:EVOSIM_THEATRE_SNAP_VIEWS = ($viewNames -join ',') }
    else { Remove-Item env:EVOSIM_THEATRE_SNAP_VIEWS -ErrorAction SilentlyContinue }

    if ($AllowSourceMismatch) { $env:EVOSIM_THEATRE_OVERRIDE = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_OVERRIDE -ErrorAction SilentlyContinue }

    # Removed rather than set to 0 when it is off, so that a shell which photographed with the
    # chrome once cannot leave it on for every picture taken afterwards.
    if ($Chrome) { $env:EVOSIM_THEATRE_CHROME = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_CHROME -ErrorAction SilentlyContinue }

    # The theatre seeks with the camera off, which would skip the world past the first picture.
    Remove-Item env:EVOSIM_THEATRE_SEEK -ErrorAction SilentlyContinue

    Write-Host "$Arm -> worker $Worker ($proj)"
    Write-Host "  run    $runDirectory"
    Write-Host "  at     $($timeList -join ', ') s"
    Write-Host "  views  $(if ($viewNames.Count -gt 0) { $viewNames -join ', ' } else { 'side, end, top, iso' })"
    Write-Host "  size   $Size"
    Write-Host "  carve  $($env:EVOSIM_THEATRE_CARVE)"
    Write-Host "  out    $snapDirectory"
    Write-Host "  log    $log"
    if ($Chrome) { Write-Host "  chrome on: one extra screen capture per second, at the Game View's size" }
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
