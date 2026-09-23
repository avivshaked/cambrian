<#
.SYNOPSIS
  Film a live world continued from a farm checkpoint: numbered frames per shot, then an mp4 each.

.DESCRIPTION
  The owner wants films of the world (logbook/specs/video-tools-notes.md). The safari's director
  is not built; this is the piece under it that can run without a person at the Editor.

  It finds the newest run under <RunsRoot>/<Arm>, and the checkpoint at or before -At in its
  checkpoints/ directory, and refuses when there is none. It then launches a batch Editor on a
  worker, which enters Play mode, restores the checkpoint in the theatre's live mode (on
  Evosim.Dynamics, a cousin of the run by construction), and advances the world one frame
  interval of simulated time at a time, rendering each shot's camera off screen into
  scratch/films/<Arm>/<checkpoint s>/<shot>/frame-NNNNNN.png. Played at -Fps the clip runs at one
  simulated second a second, which is the pace lock's (L) real time.

  The shots, each one move (logbook/specs/safari-spec.md section 9, the owner's rules):
    orbit  one slow turn round the crowd's centroid, tilted down, at a distance that frames the
           crowd's spread, pulled in where the glass or the surface leave no room.
    close  the snapshot's close framing (the largest body and its largest neighbour, three
           quarters), held, following the subject, with a slow dolly in.
    drift  a slow lateral pass at the crowd's depth, a quarter metre a second at most.
  No box, no rings, no markers; the interface is hidden except COUSIN and the second, burnt into
  the corner. The camera is kept inside the glass, over the bed, under the surface and off every
  body, and the Editor's log carries a tally per shot of how often each rule bound and how fast
  the camera went.

  Then ffmpeg (on PATH) encodes scratch/films/<Arm>/<Arm>-t<checkpoint s>-<shot>.mp4 per shot:
  H.264, yuv420p, -Fps, -crf 18.

  The launch is the snapshot's: -batchmode without -quit (the entry quits itself) and without
  -nographics (a picture needs a graphics device), four job workers. It refuses worker 1 (unity/,
  the owner's Editor) and a worker a Unity process already holds.

.PARAMETER Arm
  Arm name, as in <RunsRoot>/<Arm>. The newest run inside it is the one filmed.

.PARAMETER At
  Simulated seconds. The checkpoint at or before it is the one restored, and the file names carry
  the checkpoint's second, not this one.

.PARAMETER Worker
  Worker number, default 6.

.PARAMETER Shots
  Any of orbit, close, drift. Default orbit,close.

.PARAMETER Seconds
  Simulated seconds to film, default 60.

.PARAMETER Fps
  Frames per simulated second, default 30.

.PARAMETER Size
  Frame size as WxH, both even. Default 1920x1080.

.PARAMETER Turns
  Turns of the orbit over the clip, default 0.25 (a quarter turn; a full turn at the tank's radius
  moves the camera far faster than a body swims, and the grade's motion blur smears it).

.PARAMETER Carve
  The skin's carve depth, as theatre-snap.ps1's, default 0.35.

.PARAMETER MotionBlur
  The grade's camera motion blur for these frames, 0 to 1 (EVOSIM_THEATRE_MOTION_BLUR). Left at the
  theatre's own default (0.3, tuned for the fly camera at screen rate) when not given. The blur is
  drawn from one frame's camera move, so a fast orbit at a low fps smears: the first smoke's orbit
  (a full turn in 20 s at 10 fps) was unreadable at its middle with the default and sharp at 0.

.PARAMETER RunsRoot
  Where the arm lives, default runs/. Relative paths are taken from the repository root.

.PARAMETER WallMinutes
  Wall clock cap on the batch Editor, default 30. The Editor also stops itself at this wall.

.PARAMETER Raw
  A diagnostic: the bodies are drawn as the colliders the physics has, with no rounding, carve,
  taper or bend (EVOSIM_THEATRE_FILM_RAW=1), as the runner's X key does.
.PARAMETER Trace
  A diagnostic: every living body is logged link by link every
  frame, as the solver holds it and as the view draws it, to trace.tsv beside the shot directories
  (EVOSIM_THEATRE_FILM_TRACE=1).
.PARAMETER Freeze
  A diagnostic: the world is not stepped during the capture (EVOSIM_THEATRE_FILM_FREEZE=1), so the
  clip's only motion is the camera's and the shaders' clock. Every frame's label says FROZEN.

.PARAMETER DeleteFrames
  Delete each shot's frames once its mp4 is written.

.EXAMPLE
  ./scripts/theatre-film.ps1 ckUi -At 400 -RunsRoot scratch/live-ui/runs -Seconds 20 -Fps 10
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [Parameter(Mandatory)][double]$At,
    [int]$Worker = 6,
    [string[]]$Shots = @('orbit', 'close'),
    [double]$Seconds = 60,
    [int]$Fps = 30,
    [string]$Size = '1920x1080',
    [double]$Turns = 0.25,
    [double]$Carve = 0.35,
    [double]$MotionBlur = -1,
    [string]$RunsRoot = 'runs',
    [int]$WallMinutes = 30,
    [switch]$Freeze,
    [switch]$Trace,
    [switch]$Raw,
    [switch]$DeleteFrames
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }

$invariant = [System.Globalization.CultureInfo]::InvariantCulture

# A comma list arrives as one string through "powershell -File" (CLAUDE.md's new-worker gotcha),
# so every element is split again here.
$shotList = @()
foreach ($value in $Shots) {
    foreach ($part in ($value -split ',')) {
        $trimmed = $part.Trim().ToLowerInvariant()
        if ($trimmed) { $shotList += $trimmed }
    }
}
if ($shotList.Count -eq 0) { throw "-Shots needs at least one of orbit, close, drift." }
foreach ($s in $shotList) {
    if (@('orbit', 'close', 'drift') -notcontains $s) { throw "-Shots: '$s' is not a shot. The shots are orbit, close, drift." }
}
$shotList = @($shotList | Select-Object -Unique)

if ($Size -notmatch '^(\d+)[xX](\d+)$') { throw "-Size: '$Size' is not a size. Write it as WxH." }
if (([int]$Matches[1]) % 2 -ne 0 -or ([int]$Matches[2]) % 2 -ne 0) { throw "-Size: both sides must be even for yuv420p." }
if ($Seconds -le 0 -or $Seconds -gt 3600) { throw "-Seconds: $Seconds is outside 0 to 3600." }
if ($Fps -lt 1 -or $Fps -gt 120) { throw "-Fps: $Fps is outside 1 to 120." }
if ($Carve -lt 0 -or $Carve -gt 0.5) { throw "-Carve: $Carve is outside 0 to 0.5." }
if ($Turns -lt 0 -or $Turns -gt 4) { throw "-Turns: $Turns is outside 0 to 4." }
if ($PSBoundParameters.ContainsKey('MotionBlur') -and ($MotionBlur -lt 0 -or $MotionBlur -gt 1)) { throw "-MotionBlur: $MotionBlur is outside 0 to 1." }

if ($Worker -eq 1) { throw "Worker 1 is unity/, which the owner keeps open in the Editor. Use a worker from 2 up." }

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) { throw "ffmpeg is not on PATH." }

# ---------------------------------------------------------------- the run and the checkpoint

$runsDirectory = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $root $RunsRoot }
$armDirectory = Join-Path $runsDirectory $Arm
if (-not (Test-Path $armDirectory)) { throw "No arm directory at $armDirectory" }

# Run directories are named by their start time, so the newest sorts last.
$run = Get-ChildItem -Path $armDirectory -Directory | Sort-Object Name | Select-Object -Last 1
if (-not $run) { throw "No run directory inside $armDirectory" }

$checkpointDirectory = Join-Path $run.FullName 'checkpoints'
if (-not (Test-Path $checkpointDirectory)) { throw "The run $($run.FullName) has no checkpoints/ directory, so there is nothing to continue from." }

$candidates = @()
foreach ($file in (Get-ChildItem -Path $checkpointDirectory -Filter '*.ckpt' -File)) {
    $second = 0.0
    if ([double]::TryParse($file.BaseName, [System.Globalization.NumberStyles]::Float, $invariant, [ref]$second)) {
        $candidates += [pscustomobject]@{ Second = $second; File = $file }
    }
}

$held = @($candidates | Sort-Object Second)
$chosen = @($held | Where-Object { $_.Second -le $At + 1e-6 }) | Select-Object -Last 1
if (-not $chosen) {
    $list = ($held | ForEach-Object { $_.Second.ToString('0.###', $invariant) }) -join ', '
    throw "No checkpoint at or before $At s in $checkpointDirectory. It holds: $(if ($list) { $list } else { 'none' })."
}

$checkpointSecond = $chosen.Second.ToString('0.###', $invariant)
$checkpointFile = $chosen.File.FullName

# ---------------------------------------------------------------- the worker

$proj = Join-Path $root "unity-w$Worker"
if (-not (Test-Path $proj)) { throw "No worker project at $proj. Create it with scripts/new-worker.ps1 from a shell." }

# The path must end where the argument ends: unity/ is a prefix of unity-w2/.
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

if (Test-Path (Join-Path $proj 'Temp/UnityLockfile')) {
    throw "unity-w$Worker has Temp/UnityLockfile. If no Unity process holds it (Get-Process Unity), delete it and run again."
}

$filmDirectory = Join-Path $root "scratch\films\$Arm"
# A frozen clip is filed apart, so a diagnostic never overwrites the film it is checking.
$tag = if ($Freeze) { '-frozen' } else { '' }
$frameDirectory = Join-Path $filmDirectory "$checkpointSecond$tag"

$logDirectory = Join-Path $root 'scratch\logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory "theatre-film-$Arm.log"

$names = @(
    'EVOSIM_THEATRE_RUN', 'EVOSIM_THEATRE_CHECKPOINT', 'EVOSIM_THEATRE_SEEK', 'EVOSIM_REPO_ROOT',
    'EVOSIM_THEATRE_FILM_SECONDS', 'EVOSIM_THEATRE_FILM_FPS', 'EVOSIM_THEATRE_FILM_SIZE',
    'EVOSIM_THEATRE_FILM_SHOTS', 'EVOSIM_THEATRE_FILM_OUT', 'EVOSIM_THEATRE_FILM_TURNS',
    'EVOSIM_THEATRE_WALL_MINUTES', 'EVOSIM_THEATRE_CARVE', 'EVOSIM_THEATRE_SNAP_FROM',
    'EVOSIM_THEATRE_OVERRIDE', 'EVOSIM_THEATRE_GENOME', 'EVOSIM_THEATRE_MOTION_BLUR', 'EVOSIM_THEATRE_FILM_FREEZE', 'EVOSIM_THEATRE_FILM_TRACE', 'EVOSIM_THEATRE_FILM_RAW')

$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }

$code = 1

try {
    # A checkpoint carries its own run directory; the rest are removed so that a shell which did
    # something else first cannot turn the film into a replay, a reconstruction or a solo body.
    foreach ($name in @('EVOSIM_THEATRE_RUN', 'EVOSIM_THEATRE_SNAP_FROM', 'EVOSIM_THEATRE_OVERRIDE', 'EVOSIM_THEATRE_GENOME')) {
        Remove-Item "env:$name" -ErrorAction SilentlyContinue
    }

    $env:EVOSIM_THEATRE_CHECKPOINT = $checkpointFile
    $env:EVOSIM_THEATRE_SEEK = $checkpointSecond
    $env:EVOSIM_REPO_ROOT = $root
    $env:EVOSIM_THEATRE_FILM_SECONDS = $Seconds.ToString($invariant)
    $env:EVOSIM_THEATRE_FILM_FPS = "$Fps"
    $env:EVOSIM_THEATRE_FILM_SIZE = $Size
    $env:EVOSIM_THEATRE_FILM_SHOTS = ($shotList -join ',')
    $env:EVOSIM_THEATRE_FILM_OUT = $frameDirectory
    $env:EVOSIM_THEATRE_FILM_TURNS = $Turns.ToString($invariant)
    $env:EVOSIM_THEATRE_WALL_MINUTES = "$WallMinutes"
    $env:EVOSIM_THEATRE_CARVE = $Carve.ToString($invariant)

    if ($Freeze) { $env:EVOSIM_THEATRE_FILM_FREEZE = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_FILM_FREEZE -ErrorAction SilentlyContinue }
    if ($Trace) { $env:EVOSIM_THEATRE_FILM_TRACE = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_FILM_TRACE -ErrorAction SilentlyContinue }
    if ($Raw) { $env:EVOSIM_THEATRE_FILM_RAW = '1' }
    else { Remove-Item env:EVOSIM_THEATRE_FILM_RAW -ErrorAction SilentlyContinue }

    if ($PSBoundParameters.ContainsKey('MotionBlur')) { $env:EVOSIM_THEATRE_MOTION_BLUR = $MotionBlur.ToString($invariant) }

    Write-Host "$Arm -> worker $Worker ($proj)"
    Write-Host "  run    $($run.FullName)"
    Write-Host "  ckpt   $checkpointFile (asked for $At s; a cousin from here, and every frame says so)"
    Write-Host "  shots  $($shotList -join ', ')"
    Write-Host "  film   $Seconds s at $Fps fps, $Size"
    Write-Host "  frames $frameDirectory"
    Write-Host "  log    $log"

    # No -quit and no -nographics: the entry quits itself, and a picture needs a device.
    $arguments = @(
        '-projectPath', $proj, '-batchmode',
        '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreFilm.Run',
        '-job-worker-count', '4',
        '-logFile', $log)

    $process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru

    # The Editor stops itself at the same wall; five minutes more covers its start-up and exit.
    $wallMs = [int64](($WallMinutes + 5) * 60 * 1000)
    if (-not $process.WaitForExit($wallMs)) {
        Write-Warning "The Editor ran past its wall of $($WallMinutes + 5) min and is being stopped."
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(30000) | Out-Null
        $code = 124
    } else {
        $process.WaitForExit()
        $code = $process.ExitCode
    }
} finally {
    foreach ($name in $names) {
        if ($null -eq $saved[$name]) { Remove-Item "env:$name" -ErrorAction SilentlyContinue }
        else { Set-Item -Path "env:$name" -Value $saved[$name] }
    }
}

Write-Host ''
Write-Host "Unity exited with code $code."

if ($code -ne 0) {
    Write-Warning "The film failed. The last lines of $log :"
    if (Test-Path $log) { Get-Content $log -Tail 30 | ForEach-Object { Write-Host "  $_" } }
    exit $code
}

# ---------------------------------------------------------------- the encode

$ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
$failed = 0

foreach ($shot in $shotList) {
    $shotDirectory = Join-Path $frameDirectory $shot
    $frames = @(Get-ChildItem -Path $shotDirectory -Filter 'frame-*.png' -File -ErrorAction SilentlyContinue)
    if ($frames.Count -eq 0) {
        Write-Warning "$shot : no frames in $shotDirectory"
        $failed++
        continue
    }

    $clip = Join-Path $filmDirectory "$Arm-t$checkpointSecond-$shot$tag.mp4"
    $pattern = Join-Path $shotDirectory 'frame-%06d.png'

    & $ffmpeg.Source -hide_banner -loglevel error -y -framerate $Fps -start_number 0 -i $pattern `
        -c:v libx264 -pix_fmt yuv420p -crf 18 -r $Fps -movflags +faststart $clip
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $clip)) {
        Write-Warning "$shot : ffmpeg failed ($LASTEXITCODE)"
        $failed++
        continue
    }

    $length = '?'
    if ($ffprobe) {
        $length = (& $ffprobe.Source -v error -show_entries format=duration -of 'default=noprint_wrappers=1:nokey=1' $clip | Select-Object -First 1)
    }

    $bytes = (Get-Item $clip).Length
    Write-Host ("  {0,-40} {1} frames, {2} s, {3:N0} bytes" -f (Split-Path $clip -Leaf), $frames.Count, $length, $bytes)
    # A contact sheet beside the clip, twelve frames spread over it, for a reader of pictures
    # (scripts/film-sheet.py); a failure here does not fail the film.
    $sheet = Join-Path $PSScriptRoot 'film-sheet.py'
    if (Test-Path $sheet) { & python $sheet $clip 2>&1 | Select-Object -Last 1 | ForEach-Object { Write-Host "    sheet: $_" } }

    if ($DeleteFrames) { Remove-Item -Path (Join-Path $shotDirectory 'frame-*.png') -Force }
}

if ($failed -gt 0) { exit 1 }
exit 0
