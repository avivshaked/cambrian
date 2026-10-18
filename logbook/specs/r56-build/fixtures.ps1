# Round 56's fixtures after the seasons added two tunables (scratch/wt-r56, branch round-56), the pattern of
# logbook/specs/r55-build/fixtures2.ps1. It waits until round 55's seeds 2 and 3 have both ended, so that
# with seed 1 running the machine stays within half its processors. Then: the Release farm; the config
# fixture (round 42's launcher for 10 s as pfix20) copied to src/Evosim.Core.Tests/fixtures/r42-config.json
# and its hash written into the Farm tests; the crowd fixture (round 44's world, seed 4, 20,000 s, record
# format 1, as r56fix-s4) at 4 threads, named in src/Evosim.Dynamics.Tests/RunFixture.cs; then the suites.
# Commits nothing: the caller reads the log and commits.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r56"
$runs = "$R\runs"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$deadline = (Get-Date).AddHours(8)
while ((Get-Date) -lt $deadline -and ((Get-Process -Id 262152 -ErrorAction SilentlyContinue) -or (Get-Process -Id 261276 -ErrorAction SilentlyContinue))) {
    Start-Sleep -Seconds 60
}
if ((Get-Process -Id 262152 -ErrorAction SilentlyContinue) -or (Get-Process -Id 261276 -ErrorAction SilentlyContinue)) {
    Write-Host "deadline passed with a round-55 seed still running; nothing started"; return
}
if (Test-Path "$R\scratch\r56-build\HOLD") { Write-Host "HOLD present; nothing started"; return }
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
Write-Host "== pfix20: round 42's launcher for 10 s"
& "$w\scripts\run-farm.ps1" pfix20 -Launcher "$w\rounds\env-r42.ps1" -Seed 1 -Seconds 10 -Threads 4 -RunsRoot $runs -WallMinutes 10 | Select-String 'configHash|launched|refus|error'
$p = Wait-Arm 'pfix20' 10
if ($p) {
    $cfg = Join-Path $p 'config.json'
    $hash = (Get-Content $cfg -Raw | ConvertFrom-Json).configHash
    Write-Host "pfix20 configHash $hash (was 49e582201e1a21dd)"
    Copy-Item $cfg "$w\src\Evosim.Core.Tests\fixtures\r42-config.json" -Force
    Replace-In "$w\src\Evosim.Farm.Tests\EnvBindingTests.cs" '49e582201e1a21dd' $hash
    Replace-In "$w\src\Evosim.Farm.Tests\ReportTests.cs" '49e582201e1a21dd' $hash
}
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r56fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 300 -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error'
$fix = Wait-Arm 'r56fix-s4' 300
Write-Host "the crowd fixture is $fix"
if ($fix) {
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'D:\Projects\experiments\evolution-simulator\runs\r55fix2-s4\2026-10-04-110352-9f62c4a0' $fix
    Replace-In "$w\src\Evosim.Dynamics.Tests\RunFixture.cs" 'r55fix2-s4 (round 44''s world on the round-55 build, D149''s' 'r56fix-s4 (round 44''s world on the round-56 build, the season at its default, which is off, D149''s'
}
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
$xu = @('--', 'xUnit.MaxParallelThreads=8')
foreach ($t in 'Evosim.Core.Tests', 'Evosim.Dynamics.Tests', 'Evosim.Farm.Tests') {
    Write-Host "== $t from $(Get-Date -Format HH:mm:ss)"
    & $dotnet test "$w\src\$t\$t.csproj" -m:8 --nologo -v minimal @xu 2>&1 | Select-String 'Passed!|Failed!|error CS|\[FAIL\]|Failed ' | Select-Object -Last 25
    Write-Host "$t exit $LASTEXITCODE at $(Get-Date -Format HH:mm:ss)"
}
Write-Host "== suites done at $(Get-Date -Format HH:mm:ss)"
