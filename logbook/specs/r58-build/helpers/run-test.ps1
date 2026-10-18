param(
    [Parameter(Mandatory)] [string] $Project,
    [string] $Filter,
    [switch] $ShowOutput,
    [switch] $BuildOnly,
    [string] $Log
)
# The light-sense build's runner: pinned to logical 4 and 5 at BelowNormal before dotnet starts,
# so every child inherits it; -m:1, no node reuse, no shared compiler server left behind.
$ErrorActionPreference = 'Stop'
$me = Get-Process -Id $PID
$me.ProcessorAffinity = [IntPtr][int64]48
$me.PriorityClass = 'BelowNormal'

$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:MSBUILDDISABLENODEREUSE = '1'

$root = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r58\src'
$proj = Join-Path $root "$Project\$Project.csproj"
if (-not $Log) { $Log = "D:\Projects\experiments\evolution-simulator\scratch\r58-build\$Project.log" }

& $dotnet build $proj -m:1 -nr:false -p:UseSharedCompilation=false -v minimal --nologo *> $Log
if ($LASTEXITCODE -ne 0) {
    Select-String -Path $Log -Pattern 'error' | Select-Object -First 40 | ForEach-Object { $_.Line }
    Write-Output "BUILD FAILED ($LASTEXITCODE)"
    exit 1
}
Write-Output 'build ok'
if ($BuildOnly) { exit 0 }

$args2 = @('test', $proj, '--no-build', '--nologo', '-v', 'minimal')
if ($Filter) { $args2 += @('--filter', "($Filter)&Category!=Slow") } else { $args2 += @('--filter', 'Category!=Slow') }
if ($ShowOutput) { $args2 += @('--logger', 'console;verbosity=detailed') }
& $dotnet @args2 *>> $Log
$code = $LASTEXITCODE
Get-Content $Log | Where-Object { $_ -notmatch 'warning ' } | Select-String -Pattern '(Passed|Failed)!|error|Failed |Assert|Expected|Actual|DIGEST|Total tests|Passed:|Failed:|^\s+at ' | Select-Object -First 80 | ForEach-Object { $_.Line }
exit $code
