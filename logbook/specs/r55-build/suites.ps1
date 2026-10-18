# Round 55's suites on scratch/wt-r55 once fixtures.ps1 has named the crowd fixture: Core -All, Dynamics
# and Farm, at 8 test threads, with nothing else heavy running. Commits nothing.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r55"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$xu = @('--', 'xUnit.MaxParallelThreads=8')
foreach ($p in 'Evosim.Core.Tests', 'Evosim.Dynamics.Tests', 'Evosim.Farm.Tests') {
    Write-Host "== $p from $(Get-Date -Format HH:mm:ss)"
    & $dotnet test "$w\src\$p\$p.csproj" -m:8 --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error CS|\[FAIL\]|Failed ' | Select-Object -Last 25
    Write-Host "$p exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
}
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
