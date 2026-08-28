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

.EXAMPLE
  ./scripts/run-arm.ps1 -Name linkearn -Worker 6 -Settings @{ EVOSIM_LINK_PHOTO = 0.5 }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][int]$Worker,
    [hashtable]$Settings = @{},
    [float]$Seconds = 40000,
    [float]$WallMinutes = 600,
    [uint64]$Seed = 1   # not [ulong]: that accelerator is PowerShell 7 only
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
if (-not (Test-Path $unity)) { throw "No Unity at $unity" }

$proj = if ($Worker -eq 1) { Join-Path $root 'unity' } else { Join-Path $root "unity-w$Worker" }
if (-not (Test-Path $proj)) { throw "No worker project at $proj — run scripts/new-worker.ps1 -Workers $Worker" }

# Refuse rather than corrupt: a Library/ is not shareable, and the damage shows up later.
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match [regex]::Escape($proj) }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

$log = Join-Path $env:TEMP "evosim-$Name.log"
$out = Join-Path $root "runs/$Name.md"

# Set for this process only; Start-Process inherits it. Restored afterwards so a second arm
# launched from the same shell does not silently inherit the first one's settings — which is
# exactly how an arm ends up being a duplicate of its own control.
$saved = @{}
$env:EVOSIM_SECONDS = $Seconds
$env:EVOSIM_WALL_MINUTES = $WallMinutes
$env:EVOSIM_SEED = $Seed
$env:EVOSIM_OUT = $out

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

Start-Process -FilePath $unity -ArgumentList $a -NoNewWindow

foreach ($k in $saved.Keys) {
    if ($null -eq $saved[$k]) { Remove-Item "env:$k" -ErrorAction SilentlyContinue }
    else { Set-Item -Path "env:$k" -Value $saved[$k] }
}

Write-Host 'Launched. Watch with: Get-Content $log -Wait -Tail 5'
