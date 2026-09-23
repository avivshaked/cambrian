<#
.SYNOPSIS
  Play a safari (a trip through one recorded run) in a batch Editor, write each take's frames,
  and encode one clip per scene with a contact sheet each.

.DESCRIPTION
  The safari's headless record (logbook/specs/safari-spec.md item 12, route a). It finds the
  newest run under <RunsRoot>/<Arm> and its guide (guide/guide.json beside the run, or -Guide),
  then launches a batch Editor on a worker that enters Play mode and runs
  Evosim.Theatre.EditorTools.TheatreSafari.Run: the director builds the trip from the guide by the
  heuristic, seeks each scene's second from the run's checkpoints (a cousin from every restore,
  and every frame says so), plans each take against the bed and the bodies at that second, and
  writes its frames through the film's render-texture read-back into
  scratch/safari/<Arm>/<date>/<scene>/take-N/frame-NNNNNN.png, with captions.tsv (scene, second,
  text, offset into the take's clip) and scenes.tsv (what each scene did) beside them.

  Then ffmpeg (on PATH) encodes each take and joins a scene's takes: the time station's two takes
  with a one-second crossfade, any other scene's with a cut. The clips are
  scratch/safari/<Arm>/<date>/<Arm>-<scene>.mp4, each with a contact sheet (scripts/film-sheet.py).

  With -Check it runs Evosim.Theatre.EditorTools.TheatreSafariCheck.Run instead: a frame every two
  seconds (or -Every) into scratch/snaps/safari/<Arm>/, the camera asserted above the bed, outside
  every body and under the 0.5 m/s ceiling in every frame, one verdict line, and nothing encoded.

  The launch is the film's: -batchmode without -quit (the entry quits itself) and without
  -nographics (a picture needs a graphics device), four job workers. It refuses worker 1 (unity/,
  the owner's Editor) and a worker a Unity process already holds or whose lock file is left.

.PARAMETER Arm
  Arm name, as in <RunsRoot>/<Arm>. The newest run inside it is the one visited.
.PARAMETER Heuristic
  top10 (default), guild, depth, age or firsts (safari-spec item 7).
.PARAMETER Scenes
  1-based indices of the trip's scenes to play, e.g. 1,3,4; all of them when not given. The
  Editor's log prints the whole trip first, so a first run with -Scenes 1 shows the list.
.PARAMETER Clade
  A clade's name from the guide: a trip of one (its portrait, a birth if one can be sought, its
  colony), in place of the heuristic's trip.
.PARAMETER Fps
  Frames per simulated second, default 30.
.PARAMETER Size
  Frame size as WxH, both even. Default 1920x1080 (960x540 with -Check).
.PARAMETER Worker
  Worker number, default 6.
.PARAMETER WallMinutes
  Wall clock cap on the batch Editor, default 60. Seeks between checkpoints 2,500 s apart are the
  cost: the log says each seek's length before it starts.
.PARAMETER Guide
  A guide.json to use in place of the run's own (EVOSIM_THEATRE_SAFARI_GUIDE).
.PARAMETER SeekMax
  A flexible scene more than this many seconds past its checkpoint opens at the checkpoint
  instead when its clade is alive there (default 300; -1 never moves a scene).
.PARAMETER NoCaptions
  Do not burn the captions into the frames; captions.tsv is written either way.
.PARAMETER Check
  Run the headless check instead of the record.
.PARAMETER Every
  With -Check, simulated seconds between frames (default 2).
.PARAMETER RunsRoot
  Where the arm lives, default runs/.
.PARAMETER DeleteFrames
  Delete each take's frames once its scene's clip is written.

.EXAMPLE
  ./scripts/theatre-safari.ps1 r46-s1 -Scenes 1,3 -Fps 30 -Worker 6 -WallMinutes 90
.EXAMPLE
  ./scripts/theatre-safari.ps1 r46-s1 -Check -Scenes 1,2,3 -Guide scratch/safari-director/guide-r46-s1.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [string]$Heuristic = 'top10',
    [string[]]$Scenes = @(),
    [string]$Clade = '',
    [int]$Fps = 30,
    [string]$Size = '',
    [int]$Worker = 6,
    [int]$WallMinutes = 60,
    [string]$Guide = '',
    [double]$SeekMax = 300,
    [switch]$NoCaptions,
    [switch]$Check,
    [double]$Every = 2,
    [string]$RunsRoot = 'runs',
    [switch]$DeleteFrames
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

if (@('top10', 'guild', 'depth', 'age', 'firsts') -notcontains $Heuristic.ToLowerInvariant()) {
    throw "-Heuristic: '$Heuristic' is not one of top10, guild, depth, age, firsts."
}

# A comma list arrives as one string through "powershell -File" (CLAUDE.md's new-worker gotcha).
$sceneList = @()
foreach ($value in $Scenes) {
    foreach ($part in ($value -split ',')) {
        $trimmed = $part.Trim()
        if ($trimmed) {
            if ($trimmed -notmatch '^\d+$' -or [int]$trimmed -lt 1) { throw "-Scenes: '$trimmed' is not a scene number from 1." }
            $sceneList += [int]$trimmed
        }
    }
}

if ($Size -and $Size -notmatch '^(\d+)[xX](\d+)$') { throw "-Size: '$Size' is not a size. Write it as WxH." }
if ($Size -and (([int]$Matches[1]) % 2 -ne 0 -or ([int]$Matches[2]) % 2 -ne 0)) { throw "-Size: both sides must be even for yuv420p." }
if ($Fps -lt 1 -or $Fps -gt 120) { throw "-Fps: $Fps is outside 1 to 120." }
if ($Every -lt 0.05 -or $Every -gt 30) { throw "-Every: $Every is outside 0.05 to 30." }
if ($Worker -eq 1) { throw "Worker 1 is unity/, which the owner keeps open in the Editor. Use a worker from 2 up." }

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $Check -and -not $ffmpeg) { throw "ffmpeg is not on PATH." }

# ---------------------------------------------------------------- the run and its guide

$runsDirectory = if ([System.IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $root $RunsRoot }
$armDirectory = Join-Path $runsDirectory $Arm
if (-not (Test-Path $armDirectory)) { throw "No arm directory at $armDirectory" }
$run = Get-ChildItem -Path $armDirectory -Directory | Sort-Object Name | Select-Object -Last 1
if (-not $run) { throw "No run directory inside $armDirectory" }

$guidePath = ''
if ($Guide) {
    $guidePath = if ([System.IO.Path]::IsPathRooted($Guide)) { $Guide } else { Join-Path $root $Guide }
    if (-not (Test-Path $guidePath)) { throw "-Guide: no file at $guidePath" }
} else {
    $own = Join-Path $run.FullName 'guide\guide.json'
    if (-not (Test-Path $own)) { throw "No guide at $own. Run scripts/guide.py $Arm first, or name one with -Guide." }
}

$checkpoints = @(Get-ChildItem -Path (Join-Path $run.FullName 'checkpoints') -Filter '*.ckpt' -File -ErrorAction SilentlyContinue)
if ($checkpoints.Count -eq 0) {
    Write-Warning "The run has no checkpoints: every scene replays from the founding, and the Editor's log says how long each will take."
}

# ---------------------------------------------------------------- the worker

$proj = Join-Path $root "unity-w$Worker"
if (-not (Test-Path $proj)) { throw "No worker project at $proj. Create it with scripts/new-worker.ps1 from a shell." }

$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }
if (Test-Path (Join-Path $proj 'Temp/UnityLockfile')) {
    throw "unity-w$Worker has Temp/UnityLockfile. If no Unity process holds it (Get-Process Unity), delete it and run again."
}

$date = (Get-Date).ToString('yyyy-MM-dd', $invariant)
$outDirectory = if ($Check) { Join-Path $root "scratch\snaps\safari\$Arm" } else { Join-Path $root "scratch\safari\$Arm\$date" }

$logDirectory = Join-Path $root 'scratch\logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory ("theatre-safari{0}-$Arm.log" -f $(if ($Check) { '-check' } else { '' }))

$names = @(
    'EVOSIM_THEATRE_RUN', 'EVOSIM_THEATRE_CHECKPOINT', 'EVOSIM_THEATRE_SEEK', 'EVOSIM_REPO_ROOT',
    'EVOSIM_THEATRE_SNAP_FROM', 'EVOSIM_THEATRE_OVERRIDE', 'EVOSIM_THEATRE_GENOME',
    'EVOSIM_THEATRE_SAFARI_HEURISTIC', 'EVOSIM_THEATRE_SAFARI_SCENES', 'EVOSIM_THEATRE_SAFARI_CLADE',
    'EVOSIM_THEATRE_SAFARI_FPS', 'EVOSIM_THEATRE_SAFARI_SIZE', 'EVOSIM_THEATRE_SAFARI_WALL_MINUTES',
    'EVOSIM_THEATRE_SAFARI_GUIDE', 'EVOSIM_THEATRE_SAFARI_OUT', 'EVOSIM_THEATRE_SAFARI_CAPTIONS',
    'EVOSIM_THEATRE_SAFARI_SEEK_MAX', 'EVOSIM_THEATRE_SAFARI_EVERY', 'EVOSIM_THEATRE_WALL_MINUTES')

$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$code = 1

try {
    foreach ($name in $names) { Remove-Item "env:$name" -ErrorAction SilentlyContinue }

    $env:EVOSIM_THEATRE_RUN = $run.FullName
    $env:EVOSIM_REPO_ROOT = $root
    $env:EVOSIM_THEATRE_SAFARI_HEURISTIC = $Heuristic.ToLowerInvariant()
    if ($sceneList.Count -gt 0) { $env:EVOSIM_THEATRE_SAFARI_SCENES = ($sceneList -join ',') }
    if ($Clade) { $env:EVOSIM_THEATRE_SAFARI_CLADE = $Clade }
    $env:EVOSIM_THEATRE_SAFARI_FPS = "$Fps"
    if ($Size) { $env:EVOSIM_THEATRE_SAFARI_SIZE = $Size }
    $env:EVOSIM_THEATRE_SAFARI_WALL_MINUTES = "$WallMinutes"
    if ($guidePath) { $env:EVOSIM_THEATRE_SAFARI_GUIDE = $guidePath }
    $env:EVOSIM_THEATRE_SAFARI_OUT = $outDirectory
    if ($NoCaptions) { $env:EVOSIM_THEATRE_SAFARI_CAPTIONS = 'off' }
    $env:EVOSIM_THEATRE_SAFARI_SEEK_MAX = $SeekMax.ToString($invariant)
    $env:EVOSIM_THEATRE_SAFARI_EVERY = $Every.ToString($invariant)

    $entry = if ($Check) { 'Evosim.Theatre.EditorTools.TheatreSafariCheck.Run' } else { 'Evosim.Theatre.EditorTools.TheatreSafari.Run' }

    Write-Host "$Arm -> worker $Worker ($proj)"
    Write-Host "  run      $($run.FullName)"
    Write-Host "  guide    $(if ($guidePath) { $guidePath } else { 'guide/guide.json beside the run' })"
    Write-Host "  trip     $(if ($Clade) { "one clade: $Clade" } else { $Heuristic })$(if ($sceneList.Count -gt 0) { ", scenes $($sceneList -join ',')" } else { ', every scene' })"
    Write-Host "  frames   $outDirectory"
    Write-Host "  entry    $entry"
    Write-Host "  log      $log"

    $arguments = @(
        '-projectPath', $proj, '-batchmode',
        '-executeMethod', $entry,
        '-job-worker-count', '4',
        '-logFile', $log)

    $process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru

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
if (Test-Path $log) {
    Get-Content $log | Select-String -Pattern '\[Theatre\] safari( check)?: (PASS|FAIL|\d+ scene)' | Select-Object -Last 2 | ForEach-Object { Write-Host "  $($_.Line)" }
}

if ($code -ne 0) {
    Write-Warning "The safari failed. The last lines of $log :"
    if (Test-Path $log) { Get-Content $log -Tail 30 | ForEach-Object { Write-Host "  $_" } }
    exit $code
}

if ($Check) { exit 0 }

# ---------------------------------------------------------------- the encode

$sceneFile = Join-Path $outDirectory 'scenes.tsv'
if (-not (Test-Path $sceneFile)) { Write-Warning "No scenes.tsv in $outDirectory"; exit 1 }

$ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
$failed = 0

function Encode-Take([string]$takeDirectory, [string]$clip) {
    $pattern = Join-Path $takeDirectory 'frame-%06d.png'
    & $ffmpeg.Source -hide_banner -loglevel error -y -framerate $Fps -start_number 0 -i $pattern `
        -c:v libx264 -pix_fmt yuv420p -crf 18 -r $Fps -movflags +faststart $clip
    return ($LASTEXITCODE -eq 0 -and (Test-Path $clip))
}

function Length-Of([string]$clip) {
    if (-not $ffprobe) { return 0.0 }
    $text = & $ffprobe.Source -v error -show_entries format=duration -of 'default=noprint_wrappers=1:nokey=1' $clip | Select-Object -First 1
    $value = 0.0
    [double]::TryParse($text, [System.Globalization.NumberStyles]::Float, $invariant, [ref]$value) | Out-Null
    return $value
}

foreach ($line in (Get-Content $sceneFile | Select-Object -Skip 1)) {
    $cells = $line -split "`t"
    if ($cells.Count -lt 8) { continue }
    $slug = $cells[1]; $station = $cells[2]; $outcome = $cells[6]
    if ($outcome -ne 'played') { Write-Host "  $slug : $outcome"; continue }

    $sceneDirectory = Join-Path $outDirectory $slug
    $takes = @(Get-ChildItem -Path $sceneDirectory -Directory -Filter 'take-*' -ErrorAction SilentlyContinue | Sort-Object { [int]($_.Name -replace 'take-', '') })
    $parts = @()
    foreach ($take in $takes) {
        if (@(Get-ChildItem -Path $take.FullName -Filter 'frame-*.png' -File).Count -eq 0) { continue }
        $part = Join-Path $sceneDirectory "$($take.Name).mp4"
        if (Encode-Take $take.FullName $part) { $parts += $part } else { Write-Warning "$slug $($take.Name): ffmpeg failed"; $failed++ }
    }
    if ($parts.Count -eq 0) { Write-Warning "$slug : no frames"; $failed++; continue }

    $clip = Join-Path $outDirectory "$Arm-$slug.mp4"
    if ($parts.Count -eq 1) {
        Copy-Item -Path $parts[0] -Destination $clip -Force
    } elseif ($station -eq 'Time' -and $parts.Count -eq 2) {
        # The time station: the same shot at two seconds, a one-second crossfade (safari-spec item 6).
        $offset = [Math]::Max(0.0, (Length-Of $parts[0]) - 1.0).ToString('0.###', $invariant)
        & $ffmpeg.Source -hide_banner -loglevel error -y -i $parts[0] -i $parts[1] `
            -filter_complex "[0:v][1:v]xfade=transition=fade:duration=1:offset=$offset,format=yuv420p[v]" -map '[v]' `
            -c:v libx264 -crf 18 -r $Fps -movflags +faststart $clip
        if ($LASTEXITCODE -ne 0) { Write-Warning "$slug : the crossfade failed"; $failed++; continue }
    } else {
        # A colony and its chapter card: a cut.
        $list = Join-Path $sceneDirectory 'parts.txt'
        ($parts | ForEach-Object { "file '" + ($_ -replace '\\', '/') + "'" }) | Set-Content -Path $list -Encoding ascii
        & $ffmpeg.Source -hide_banner -loglevel error -y -f concat -safe 0 -i $list -c copy -movflags +faststart $clip
        if ($LASTEXITCODE -ne 0) { Write-Warning "$slug : the join failed"; $failed++; continue }
    }

    Write-Host ("  {0,-48} {1} take(s), {2:0.#} s" -f (Split-Path $clip -Leaf), $parts.Count, (Length-Of $clip))
    $sheet = Join-Path $PSScriptRoot 'film-sheet.py'
    if (Test-Path $sheet) { & python $sheet $clip 2>&1 | Select-Object -Last 1 | ForEach-Object { Write-Host "    sheet: $_" } }

    if ($DeleteFrames) { foreach ($take in $takes) { Remove-Item -Path (Join-Path $take.FullName 'frame-*.png') -Force } }
}

Write-Host "  captions: $(Join-Path $outDirectory 'captions.tsv') (offsets are into each take's clip; a time scene's second take starts one second early in the crossfade)"
if ($failed -gt 0) { exit 1 }
exit 0
