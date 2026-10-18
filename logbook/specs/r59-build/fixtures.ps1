# Round 59's crowd fixture after world state version 17 and every round-59 tunable (scratch/wt-r59-anchor,
# branch r59-anchor), the pattern of logbook/specs/r57-build/fixtures.ps1 less its config step: round 42's
# config fixture was re-recorded in step 6 (src/Evosim.Core.Tests/fixtures/r42-config.json, 75baf5b1a5c4b10c).
# The Release farm from a clean tree after step 5's commit, so the manifest's gitCommit names it; then
# the crowd fixture (round 44's world, seed 4, 20,000 s, record format 1, as r59fix-s4) at 4 threads,
# for src/Evosim.Dynamics.Tests/RunFixture.cs. Round 57's took 24 minutes. Commits nothing, and edits
# nothing: the caller names the run in RunFixture.cs and runs the suites.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r59-anchor"
$runs = "$R\runs"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'; $env:MSBUILDDISABLENODEREUSE = '1'
if (git -C $w status --porcelain) { Write-Host "the worktree is not clean; nothing started"; return }
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
Write-Host "== build at $(git -C $w log --oneline -1) from $(Get-Date -Format HH:mm:ss)"
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:4 -nologo -v:q -nodeReuse:false 2>&1 | Select-Object -Last 3
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r59fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 120 -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error|DIRTY'
$fix = Wait-Arm 'r59fix-s4' 125
Write-Host "the crowd fixture is $fix"
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
