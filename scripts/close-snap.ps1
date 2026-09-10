# A launcher for the theatre's close view, which scripts/theatre-snap.ps1 cannot ask for: its
# -Views allowlist is the four framing views and the second day's rules forbid editing scripts/.
# Everything else here is theatre-snap.ps1's arrangement, kept deliberately identical: no -quit
# and no -nographics, one worker, logs inside the repository.
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Arm,
    [Parameter(Mandatory)][string]$At,
    [string]$Views = 'close',
    [double]$Carve = 0.2,
    [string]$Size = '1600x900',
    [Parameter(Mandatory)][string]$Out,
    [int]$Worker = 7,
    [int]$WallMinutes = 40
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }

$proj = Join-Path $root "unity-w$Worker"
if (-not (Test-Path $proj)) { throw "No worker project at $proj" }

$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

if (Test-Path (Join-Path $proj 'Temp/UnityLockfile')) {
    throw "unity-w$Worker has a Unity process open (Temp/UnityLockfile exists)."
}

$runDirectory = Join-Path $root "runs\$Arm"
if (-not (Test-Path $runDirectory)) { throw "No run directory at $runDirectory" }

$snapDirectory = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }

$logDirectory = Join-Path $root 'scratch\logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory "theatre-close-$Arm.log"

$invariant = [System.Globalization.CultureInfo]::InvariantCulture

$env:EVOSIM_THEATRE_RUN = $runDirectory
$env:EVOSIM_THEATRE_SNAP_TIMES = $At
$env:EVOSIM_THEATRE_SNAP_VIEWS = $Views
$env:EVOSIM_THEATRE_SNAP_OUT = $snapDirectory
$env:EVOSIM_THEATRE_SNAP_SIZE = $Size
$env:EVOSIM_THEATRE_WALL_MINUTES = $WallMinutes
$env:EVOSIM_REPO_ROOT = $root
$env:EVOSIM_THEATRE_CARVE = $Carve.ToString($invariant)
Remove-Item env:EVOSIM_THEATRE_SEEK -ErrorAction SilentlyContinue
Remove-Item env:EVOSIM_THEATRE_OVERRIDE -ErrorAction SilentlyContinue

Write-Host "$Arm -> worker $Worker, views $Views, carve $($env:EVOSIM_THEATRE_CARVE), out $snapDirectory"
Write-Host "  log $log"

$arguments = @(
    '-projectPath', $proj, '-batchmode',
    '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreSnapshot.Run',
    '-logFile', $log)

$process = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru -Wait
$code = $process.ExitCode

Write-Host "Unity exited with code $code."

Get-ChildItem -Path $snapDirectory -Filter '*.png' -File -ErrorAction SilentlyContinue |
    Sort-Object Name |
    ForEach-Object { Write-Host ("  {0,-46} {1,9:N0} bytes" -f $_.Name, $_.Length) }

Write-Host ''
Write-Host 'Compile errors, missing methods and theatre lines:'
Select-String -Path $log -Pattern 'error CS|could not be found|\[Theatre\]' |
    Select-Object -Last 60 |
    ForEach-Object { Write-Host "  $($_.Line)" }

exit $code
