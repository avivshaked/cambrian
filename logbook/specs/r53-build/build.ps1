# Round 53's fixtures and suites (scratch/wt-r53, branch round-53), the pattern of
# logbook/specs/r52-build/build.ps1: the Release farm; the config fixture (round 42's launcher for
# 10 s as pfix16) copied to src/Evosim.Core.Tests/fixtures/r42-config.json and its hash written into
# the Farm tests; the crowd fixture (round 44's world, seed 4, 20,000 s, record format 1, as
# r53fix-s4) at 4 threads, named in src/Evosim.Dynamics.Tests/RunFixture.cs when it ends; then
# Core -All, Dynamics and Farm at 4 test threads. At most 8 threads of its own, run beside round
# 53's screens at 12. Commits nothing: the caller reads the log and commits.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r53"
$runs = "$R\runs"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$xu = @('--', 'xUnit.MaxParallelThreads=4')
Set-Location $w
function Wait-Arm([string]$arm, [int]$minutes) {
    $d = (Get-Date).AddMinutes($minutes)
    while ((Get-Date) -lt $d) {
        $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m) {
            $j = Get-Content $m.FullName -Raw | ConvertFrom-Json
            if ($j.status -and $j.status -ne 'running') { Write-Host "$arm ended: $($j.status) $($j.reason) at $(Get-Date -Format HH:mm:ss)"; return $m.DirectoryName }
        }
        Start-Sleep -Seconds 20
    }
    Write-Host "${arm}: wait deadline passed"; return $null
}
function Replace-In([string]$file, [string]$old, [string]$new) {
    python -c "import sys; p,o,n=sys.argv[1:4]; b=open(p,'rb').read(); c=b.count(o.encode()); open(p,'wb').write(b.replace(o.encode(), n.encode())); print(p.rsplit(chr(92),1)[-1], c, 'replaced')" $file $old $new
}
Write-Host "== build from $(Get-Date -Format HH:mm:ss)"
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:4 -nologo -v:q 2>&1 | Select-Object -Last 3
Write-Host "exe $((Get-Item $exe).LastWriteTime)"
Write-Host "== pfix16: round 42's launcher for 10 s"
& "$w\scripts\run-farm.ps1" pfix16 -Launcher "$w\rounds\env-r42.ps1" -Seed 1 -Seconds 10 -Threads 4 -RunsRoot $runs -WallMinutes 10 | Select-String 'configHash|launched|refus|error'
$p = Wait-Arm 'pfix16' 10
if ($p) {
    $cfg = Join-Path $p 'config.json'
    $hash = (Get-Content $cfg -Raw | ConvertFrom-Json).configHash
    Write-Host "pfix16 configHash $hash (was 8656c52dd0575c49)"
    Copy-Item $cfg "$w\src\Evosim.Core.Tests\fixtures\r42-config.json" -Force
    Replace-In "$w\src\Evosim.Farm.Tests\EnvBindingTests.cs" '8656c52dd0575c49' $hash
    Replace-In "$w\src\Evosim.Farm.Tests\ReportTests.cs" '8656c52dd0575c49' $hash
}
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r53fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 240 -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error'
$fix = Wait-Arm 'r53fix-s4' 240
Write-Host "the crowd fixture is $fix"
if ($fix) {
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'D:\Projects\experiments\evolution-simulator\runs\r52fix-s4\2026-09-28-100112-197b736c' $fix
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'r52fix-s4 (round 44''s world on the round-52 build' 'r53fix-s4 (round 44''s world on the round-53 build, D132''s settled decay at its default, which is off'
}
Write-Host "== Core -All at 4 test threads, from $(Get-Date -Format HH:mm:ss)"
& $dotnet test "$w\src\Evosim.Core.Tests\Evosim.Core.Tests.csproj" --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error|\[FAIL\]|Failed ' | Select-Object -Last 15
Write-Host "core exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
& $dotnet test "$w\src\Evosim.Dynamics.Tests\Evosim.Dynamics.Tests.csproj" --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error|\[FAIL\]|Failed ' | Select-Object -Last 15
Write-Host "dynamics exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
& $dotnet test "$w\src\Evosim.Farm.Tests\Evosim.Farm.Tests.csproj" --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error|\[FAIL\]|Failed ' | Select-Object -Last 15
Write-Host "farm exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
