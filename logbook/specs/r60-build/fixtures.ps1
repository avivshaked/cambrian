# Round 60's crowd fixture after D185's tunable, MatterIslandGlassMarginMetres (scratch/wt-r60-fed, branch r60-fed), the
# pattern of logbook/specs/r59-build/fixtures.ps1. The Release farm from a clean tree, so the manifest's gitCommit names
# the commit; then the crowd fixture (round 44's world, seed 4, 20,000 s, record format 1, as r60fix-s4) at 4 threads
# for src/Evosim.Dynamics.Tests/RunFixture.cs. Round 59's took 24 minutes. This process pins itself to logical 16-19 at
# BelowNormal with the card hidden before it starts anything, so the farm inherits all three. Commits nothing, and edits
# nothing: the caller names the run in RunFixture.cs and runs the suites.
$ErrorActionPreference = 'Continue'
$me = [System.Diagnostics.Process]::GetCurrentProcess(); $me.ProcessorAffinity = [IntPtr]983040; $me.PriorityClass = 'BelowNormal'
$env:CUDA_VISIBLE_DEVICES = '-1'
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r60-fed"
$runs = "$R\runs"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'; $env:MSBUILDDISABLENODEREUSE = '1'
if (git -C $w status --porcelain --untracked-files=no) { Write-Host "the worktree is not clean; nothing started"; return }
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
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:1 -nologo -v:q -nodeReuse:false -p:UseSharedCompilation=false 2>&1 | Select-Object -Last 3
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r60fix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 120 -Exe "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe" -Env @{ EVOSIM_RECORD_FORMAT = 1 } | Select-String 'configHash|launched|refus|error|DIRTY'
$fix = Wait-Arm 'r60fix-s4' 125
Write-Host "the crowd fixture is $fix"
& $dotnet build-server shutdown *> $null
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
