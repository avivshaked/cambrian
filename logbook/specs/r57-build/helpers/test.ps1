param(
    [Parameter(Mandatory)] [string] $Project,   # e.g. Evosim.Core.Tests
    [string] $Filter,
    [string] $Log
)
$ErrorActionPreference = 'Continue'
$w = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r57'
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$args2 = @('test', "$w\src\$Project\$Project.csproj", '-m:1', '--nologo', '-v', 'minimal')
if ($Filter) { $args2 += @('--filter', $Filter) }
$args2 += @('--', 'xUnit.MaxParallelThreads=4', 'RunConfiguration.MaxCpuCount=1')
if (-not $Log) { $Log = "D:\Projects\experiments\evolution-simulator\scratch\r57-build\logs\$Project.log" }
New-Item -ItemType Directory -Force (Split-Path $Log) | Out-Null
& $dotnet @args2 *> $Log
$code = $LASTEXITCODE
Get-Content $Log | Select-String 'error CS|Passed!|Failed!|\[FAIL\]|Failed |Total tests|warning CS8' | Select-Object -First 60
"exit $code"
