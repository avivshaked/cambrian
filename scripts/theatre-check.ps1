# Run the theatre's headless identity check against a recorded run, on one worker.
# D078's validation: the first shared-world run the theatre can be faithful to.
#   pwsh -NoProfile -File scripts/theatre-check.ps1 -Run th-shared -Worker 7
# ASCII only, no BOM, as the other scratch launchers are.
param(
    [Parameter(Mandatory)][string]$Run,
    [Parameter(Mandatory)][int]$Worker,
    [string]$Seconds = '',
    [switch]$Override
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe'
$proj = Join-Path $root "unity-w$Worker"
$log = Join-Path $root "scratch\logs\theatre-$Run.log"

# The same refusal run-arm.ps1 makes: two Unity processes on one Library corrupt it.
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -match ([regex]::Escape($proj) + '(?=["\s]|$)') }
if ($busy) { throw "A Unity process (PID $($busy.ProcessId)) already has $proj open." }

$env:EVOSIM_THEATRE_RUN = Join-Path $root "runs\$Run"
if ($Seconds -ne '') { $env:EVOSIM_THEATRE_SECONDS = $Seconds }

# Watch a run this build did not record. It plays with the banner, which is the only way to
# reach a pre-D078 recording at all now that the source hash has moved.
if ($Override) { $env:EVOSIM_THEATRE_OVERRIDE = '1' }
else { Remove-Item env:EVOSIM_THEATRE_OVERRIDE -ErrorAction SilentlyContinue }

Write-Host "theatre check: $env:EVOSIM_THEATRE_RUN on worker $Worker"
Write-Host "  log $log"

$p = Start-Process -FilePath $unity -Wait -NoNewWindow -PassThru -ArgumentList @(
    '-projectPath', $proj, '-batchmode', '-quit', '-nographics',
    '-executeMethod', 'Evosim.Theatre.EditorTools.TheatreIdentityCheck.Run',
    '-logFile', $log)

Write-Host "exit code $($p.ExitCode)"
