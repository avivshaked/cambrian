<#
.SYNOPSIS
    Runs Evosim.Ledger — a per-creature energy-ledger forecast, without the Unity Editor.

.DESCRIPTION
    Evosim.Ledger (src/Evosim.Ledger) takes one stored genome and a run's config.json and
    reports what that body's energy ledger does alone: net income at birth, break-even
    nutrient density, lifetime and reproduction under the same per-step rules World.Step
    applies, with no population, light field or nutrient pool around it — see
    LedgerForecast's own remarks in src/Evosim.Core/Ecosystem/LedgerForecast.cs.

    There is no .NET SDK installed system-wide on the development machine — only runtimes.
    Unity ships a complete .NET 8 SDK inside the Editor install, and this script uses that
    dotnet.exe, exactly as scripts/core-test.ps1 does, so nothing needs installing.

.EXAMPLE
    ./scripts/ledger.ps1 -Genome scratch/inoculum-r13a-s2-t17000.json `
        -Config runs/r13a-s2/2026-09-03-091631-1c50cf52/config.json `
        -Clearance 1,5,10 -Depth 0,12 -Density 1,4,7,10

.EXAMPLE
    ./scripts/ledger.ps1 -Genome path\to\genome.json -Config path\to\config.json `
        -Clearance 1,5,10 -Depth 0,5,10,15,20 -Density 0.5,1,2,4,7,10,15 -Shade 0 -Compare
#>
[CmdletBinding()]
param(
    # Path to a genome file. If it has several lines (a lineage.jsonl-style file), the tool
    # reads the first one.
    [Parameter(Mandatory = $true)]
    [string] $Genome,

    # Path to a run's config.json.
    [Parameter(Mandatory = $true)]
    [string] $Config,

    # Absorptive clearance rates to sweep, cubic metres cleared per second per cubic metre of
    # tissue. Required — there is no sensible default sweep for an experiment-shaped question.
    [Parameter(Mandatory = $true)]
    [string[]] $Clearance,

    # Depths to evaluate, metres below the surface (positive).
    [Parameter(Mandatory = $true)]
    [string[]] $Depth,

    # Nutrient densities to evaluate, J/m3.
    [Parameter(Mandatory = $true)]
    [string[]] $Density,

    # Fraction of irradiance blocked before it reaches the body, in [0, 1]. 0 (unshaded) by default.
    [double] $Shade = 0,

    # Also evaluate the same genome with every absorptive node's cell set to photosynthetic and
    # every photosynthetic node's set to absorptive — a leaf and a stomach of the same shape,
    # side by side.
    [switch] $Compare,

    # Use a specific dotnet.exe instead of the one bundled with Unity.
    [string] $DotnetPath
)

$ErrorActionPreference = 'Stop'

function Resolve-Dotnet {
    param([string] $Explicit)

    if ($Explicit) {
        if (-not (Test-Path $Explicit)) { throw "No dotnet.exe at '$Explicit'." }
        return $Explicit
    }

    # Prefer a real system SDK if one exists; fall back to Unity's.
    $system = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($system) {
        $sdks = & $system.Source --list-sdks 2>$null
        if ($sdks) { return $system.Source }
    }

    $editors = 'C:\Program Files\Unity\Hub\Editor'
    if (Test-Path $editors) {
        $candidate = Get-ChildItem $editors -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'Editor\Data\DotNetSdk\dotnet.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }

    throw @'
No .NET SDK found.

Looked for a system-wide `dotnet` with an SDK, then for the one bundled with Unity at
  <Unity>\Editor\Data\DotNetSdk\dotnet.exe

Install the .NET 8 SDK, or pass -DotnetPath explicitly.
'@
}

$dotnet = Resolve-Dotnet -Explicit $DotnetPath
$project = Join-Path $PSScriptRoot '..\src\Evosim.Ledger\Evosim.Ledger.csproj'

Write-Host "dotnet:  $dotnet"
Write-Host "project: $(Resolve-Path $project)"
Write-Host ''

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$toolArgs = @(
    '--genome', $Genome,
    '--config', $Config,
    '--clearance', ($Clearance -join ','),
    '--depth', ($Depth -join ','),
    '--density', ($Density -join ','),
    '--shade', $Shade
)
if ($Compare) { $toolArgs += '--compare' }

& $dotnet run --project $project -v minimal -- @toolArgs
exit $LASTEXITCODE
