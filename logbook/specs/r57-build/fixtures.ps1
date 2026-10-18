# Round 57's fixtures after D151's five light tunables and world state version 14 (scratch/wt-r57,
# branch round-57), the pattern of logbook/specs/r56-build/fixtures.ps1, for the caller to run: the
# build agent runs no farm. The Release farm; the config fixture (round 42's launcher for 10 s as
# pfix21) copied to src/Evosim.Core.Tests/fixtures/r42-config.json, its hash checked against the one
# the Farm tests already pin (ab7f706bac9a238c, computed from the binding by the build); the crowd
# fixture (round 44's world, seed 4, 20,000 s, record format 1, as r57fix-s4) at 4 threads, named in
# src/Evosim.Dynamics.Tests/RunFixture.cs; then the three suites. Commits nothing.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r57"
$runs = "$R\runs"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$pinned = 'ab7f706bac9a238c'
if (Test-Path "$R\scratch\r57-build\HOLD") { Write-Host "HOLD present; nothing started"; return }
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
Write-Host "== pfix21: round 42's launcher for 10 s"
& "$w\scripts\run-farm.ps1" pfix21 -Launcher "$w\rounds\env-r42.ps1" -Seed 1 -Seconds 10 -Threads 4 -RunsRoot $runs -WallMinutes 10 | Select-String 'configHash|launched|refus|error'
$p = Wait-Arm 'pfix21' 10
if ($p) {
    $cfg = Join-Path $p 'config.json'
    $hash = (Get-Content $cfg -Raw | ConvertFrom-Json).configHash
    Write-Host "pfix21 configHash $hash (pinned $pinned; was 52a1d4c87b6ea69c)"
    if ($hash -ne $pinned) { Write-Host "MISMATCH: the run's hash is not the pinned one; the Farm tests will say which field" }
    Copy-Item $cfg "$w\src\Evosim.Core.Tests\fixtures\r42-config.json" -Force
}
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r57fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 300 -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error'
$fix = Wait-Arm 'r57fix-s4' 300
Write-Host "the crowd fixture is $fix"
if ($fix) {
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'D:\Projects\experiments\evolution-simulator\runs\r56fix-s4\2026-10-04-172844-4c1d4a7d' $fix
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'r56fix-s4 (round 44''s world on the round-56 build, the season at its default, which is off,' 'r57fix-s4 (round 44''s world on the round-57 build, D151''s two-season shape and weather at their defaults, which are off, the season at its default, which is off,'
}
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
$xu = @('--', 'xUnit.MaxParallelThreads=8')
foreach ($t in 'Evosim.Core.Tests', 'Evosim.Dynamics.Tests', 'Evosim.Farm.Tests') {
    Write-Host "== $t from $(Get-Date -Format HH:mm:ss)"
    & $dotnet test "$w\src\$t\$t.csproj" -m:8 --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error CS|\[FAIL\]|Failed ' | Select-Object -Last 25
    Write-Host "$t exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
}
Write-Host "== suites done at $(Get-Date -Format HH:mm:ss)"
