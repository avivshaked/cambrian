# The second probe run (2026-09-26): the first set EVOSIM_GPU_PROBE only through run-farm.ps1's
# -Env, which reaches the farm as a NAME=VALUE argument, while the runner reads the variable from
# the process environment, so the probe never switched on. Set here, Start-Process passes it down.
# No regeneration or build: probe.ps1 did both at 09:02 and the kernels match the template.
# The card's probe (Fable's step 1, scratch/wt-probe at ccebf3c): regenerate the kernels from the
# template, build, then round 48 seed 1 resumed at 27,500 s for 200 s on the card in single at
# group 32 with EVOSIM_GPU_PROBE=1, as pace1gpu-s1 ran it. Nothing else on the machine.
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\wt-probe"
$root = "$R\scratch\r49-probe\runs"
$src = "$R\runs\r48-s1\2026-09-24-115115-e5a30c15"
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
Set-Location $w
$env:EVOSIM_GPU_PROBE = '1'
Write-Host "== the probe run from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" probe2gpu-s1 -ResumeFrom $src -At 27500 -AllowSourceMismatch -Seconds 27700 -Threads 16 -RunsRoot $root -WallMinutes 60 -Env @{ EVOSIM_ENGINE = 'gpu'; EVOSIM_GPU_DEVICE = 'cuda'; EVOSIM_GPU_PRECISION = 'single'; EVOSIM_GPU_GROUP = 32; EVOSIM_GPU_PROBE = 1 } | Select-String 'run.json|status|resumed|refus|error'
$d = (Get-Date).AddMinutes(65)
while ((Get-Date) -lt $d) {
    $m = Get-ChildItem "$root\probe2gpu-s1\*\run.json" -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($m) { $j = Get-Content $m.FullName -Raw | ConvertFrom-Json; if ($j.status -and $j.status -ne 'running') { Write-Host "ended $($j.status) $($j.reason) at $(Get-Date -Format HH:mm:ss)"; break } }
    Start-Sleep -Seconds 15
}
Get-Content "$root\probe2gpu-s1.md" | Select-String 'real time|wall split' | ForEach-Object { Write-Host "  $_" }
Write-Host "== probe done at $(Get-Date -Format HH:mm:ss); the probe lines are in the farm's own output log under $w\scratch\logs"
