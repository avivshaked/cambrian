<#
.SYNOPSIS
  A sampling profile of the farm: a run resumed from a checkpoint for a short span under .NET's
  EventPipe sampler, read by method.

.DESCRIPTION
  Resumes -ResumeFrom (an arm under -RunsRoot, or a run directory) at -At for -Seconds simulated
  seconds with -Tree's farm, the sampler on (DOTNET_EnableEventPipe, the SampleProfiler provider
  and the runtime's method rundown). Writes the trace and the run under scratch/prof/, then reads
  it with src/Evosim.Profile: exclusive and inclusive shares by method over the samples whose
  stack passes through a frame matching -Filter (the thread pool's idle waits are left out), and
  the innermost two such frames. A resume from another build runs under -AllowSourceMismatch.

  The sampler takes a stack from every thread every millisecond, and nothing in it touches a
  trajectory. A profile is a share, not a pace: it holds under load where a timing read does not,
  but it is still taken with the machine as quiet as it can be.

.EXAMPLE
  ./scripts/profile-farm.ps1 -Name r49s2-25000 -Tree scratch/wt-speed -ResumeFrom r49-s2 -At 25000 -Seconds 30 -AllowSourceMismatch
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$ResumeFrom,
    [Parameter(Mandatory)][double]$At,
    [double]$Seconds = 30,
    [string]$Tree = '',
    [string]$RunsRoot = 'runs',
    [int]$Threads = 16,
    [string]$Filter = 'Evosim',
    [int]$Top = 45,
    [hashtable]$Env = @{},
    [switch]$AllowSourceMismatch
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if ($Tree -eq '') { $Tree = $repo } elseif (-not [IO.Path]::IsPathRooted($Tree)) { $Tree = Join-Path $repo $Tree }
$runs = if ([IO.Path]::IsPathRooted($RunsRoot)) { $RunsRoot } else { Join-Path $repo $RunsRoot }
$from = if (Test-Path -LiteralPath (Join-Path $runs $ResumeFrom)) { Join-Path $runs $ResumeFrom } else { $ResumeFrom }
$out = Join-Path $repo 'scratch\prof'
[void][IO.Directory]::CreateDirectory($out)
$trace = Join-Path $out "$Name.nettrace"
$report = Join-Path $out "$Name.txt"
if ([IO.File]::Exists($trace)) { [IO.File]::Delete($trace) }

$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
& $dotnet build (Join-Path $repo 'src\Evosim.Profile\Evosim.Profile.csproj') -c Release -v q -nologo | Out-Null
$reader = Join-Path $repo 'artifacts\Evosim.Profile\bin\Release\net8.0\Evosim.Profile.exe'
if (-not [IO.File]::Exists($reader)) { throw "the reader did not build: $reader" }

$env:DOTNET_EnableEventPipe = '1'
$env:DOTNET_EventPipeOutputPath = $trace
$env:DOTNET_EventPipeConfig = 'Microsoft-DotNETCore-SampleProfiler:0:5,Microsoft-Windows-DotNETRuntime:0x4c14fccbd:5'
$since = Get-Date
try {
    Push-Location $Tree
    $a = @{ Arm = "prof-$Name"; ResumeFrom = $from; At = $At; Seconds = $At + $Seconds; Threads = $Threads
            RunsRoot = (Join-Path $out 'runs'); WallMinutes = 60; Env = $Env }
    if ($AllowSourceMismatch) { $a['AllowSourceMismatch'] = $true }
    & (Join-Path $Tree 'scripts\run-farm.ps1') @a 6>&1 | Select-String 'launched|refus|error|resumedFrom'
} finally {
    Pop-Location
    $env:DOTNET_EnableEventPipe = $null; $env:DOTNET_EventPipeOutputPath = $null; $env:DOTNET_EventPipeConfig = $null
}

# The trace is complete when the farm has exited: the rundown is written at the process's end.
$deadline = (Get-Date).AddMinutes(60)
do {
    Start-Sleep -Seconds 10
    $m = Get-ChildItem (Join-Path $out "runs\prof-$Name\*\run.json") -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.CreationTime -ge $since.AddMinutes(-1) } | Sort-Object LastWriteTime | Select-Object -Last 1
    $done = $m -and (Get-Content $m.FullName -Raw | ConvertFrom-Json).status -ne 'running'
} until ($done -or (Get-Date) -gt $deadline)
Start-Sleep -Seconds 10
if (-not [IO.File]::Exists($trace)) { throw "no trace at $trace" }
& $reader $trace $Filter $Top | Tee-Object -FilePath $report
Write-Host "report: $report"