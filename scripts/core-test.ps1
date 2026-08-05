<#
.SYNOPSIS
    Builds and tests Evosim.Core without the Unity Editor.

.DESCRIPTION
    Evosim.Core has no UnityEngine dependency (DESIGN.md §6.1) precisely so it can be
    tested like ordinary C#. Development is where recursive encodings go wrong in ways
    that are near-impossible to diagnose through a physics view, so it is worth being
    able to run these in under a second.

    There is no .NET SDK installed system-wide on the development machine — only
    runtimes. Unity ships a complete .NET 8 SDK inside the Editor install, and this
    script uses that, so nothing needs installing.

.EXAMPLE
    ./scripts/core-test.ps1
    ./scripts/core-test.ps1 -Filter DevelopmentTests
#>
[CmdletBinding()]
param(
    # Restrict the run to matching tests, e.g. a class or method name.
    [string] $Filter,

    # Use a specific dotnet.exe instead of the one bundled with Unity.
    [string] $DotnetPath,

    # Show what tests write to ITestOutputHelper. Several tests here exist to report a
    # measurement rather than to assert on one — population overlap, joint clearance — and
    # their tables are invisible at the default verbosity.
    [switch] $ShowOutput
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
$project = Join-Path $PSScriptRoot '..\src\Evosim.Core.Tests\Evosim.Core.Tests.csproj'

Write-Host "dotnet:  $dotnet"
Write-Host "project: $(Resolve-Path $project)"
Write-Host ''

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$testArgs = @('test', $project, '--nologo', '-v', 'minimal')
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($ShowOutput) { $testArgs += @('--logger', 'console;verbosity=detailed') }

& $dotnet @testArgs
exit $LASTEXITCODE
