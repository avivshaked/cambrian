# Round 55's smoke (proposals/propose-r55-mixed-inoculation.md, "Before the launch"): the Release farm
# rebuilt on scratch/wt-r55 with the world-side changes, then round 55's world (rounds/env-r55.ps1,
# brain central, the drive sense, the weighted pool at 0.6) on seed 2 for 6,000 s at 4 threads, so that
# pool founders arrive for 3,000 s after the floor closes. One farm run beside the suites, never two.
# Commits nothing: the caller reads the run.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-r55"
$runs = "$R\runs"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
# The suites start on the same signal and build Core first; let that build finish before this one.
Start-Sleep -Seconds 150
Write-Host "== Release build from $(Get-Date -Format HH:mm:ss)"
& $dotnet build "$w\src\Evosim.Farm\Evosim.Farm.csproj" -c Release -m:4 -nologo -v:q 2>&1 | Select-Object -Last 3
Write-Host "== r55smoke-s2 from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" r55smoke-s2 -Launcher "$w\rounds\env-r55.ps1" -Seed 2 -Seconds 6000 -Threads 4 -RunsRoot $runs -WallMinutes 150 | Select-String 'configHash|launched|refus|error|status|gitCommit'
Write-Host "== launched at $(Get-Date -Format HH:mm:ss)"
