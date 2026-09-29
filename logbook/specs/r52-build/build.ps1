# Round 52's fixtures (scratch/wt-r52, branch round-52): the Release farm, the config fixture
# (round 42's launcher for 10 s as pfix15) and the crowd fixture (round 44's world, seed 4,
# 20,000 s, record format 1, as r52fix-s4) at 4 threads, with the slow thread-identity test on the
# new r42 config beside it at 4 test threads. At most 8 threads in all. Launches nothing else.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r52"
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
Write-Host "== build from $(Get-Date -Format HH:mm:ss)"
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:4 -nologo -v:q 2>&1 | Select-Object -Last 3
Write-Host "exe $((Get-Item $exe).LastWriteTime)"
Write-Host "== pfix15: round 42's launcher for 10 s"
& "$w\scripts\run-farm.ps1" pfix15 -Launcher "$w\rounds\env-r42.ps1" -Seed 1 -Seconds 10 -Threads 4 -RunsRoot $runs -WallMinutes 10 | Select-String 'configHash|launched|refus|error'
$p = Wait-Arm 'pfix15' 10
if ($p) {
    $cfg = Join-Path $p 'config.json'
    $hash = (Get-Content $cfg -Raw | ConvertFrom-Json).configHash
    Write-Host "pfix15 configHash $hash (the Farm tests' failing assertions read 8656c52dd0575c49)"
    Copy-Item $cfg "$w\src\Evosim.Core.Tests\fixtures\r42-config.json" -Force
}
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r52fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 240 -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error'
Write-Host "== the thread-identity test on the new r42 config, from $(Get-Date -Format HH:mm:ss)"
& $dotnet test "$w\src\Evosim.Core.Tests\Evosim.Core.Tests.csproj" --nologo -v minimal --filter 'FullyQualifiedName~ParallelIdentityTests' @xu 2>&1 | Select-Object -Last 8
Write-Host "parallel identity exit $LASTEXITCODE"
$fix = Wait-Arm 'r52fix-s4' 240
Write-Host "== done at $(Get-Date -Format HH:mm:ss); the crowd fixture is $fix"