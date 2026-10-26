# D239's step-size check before the pace factor is built: does a longer physics step change how bodies swim and touch?
# The kill-meal screen's world (rounds/env-r62kg.ps1), seed 6201, founded three times on the CPU at physics steps of
# 0.01, 0.02 and 0.025 s, -Seconds each (300 by default), with the state stream at its fastest cadence, the 0.5 s
# metabolic step. 0.03 s is refused by today's farm, since a step must divide the 0.5 s metabolic step; it becomes
# possible only with the pace factor, at k = 3. A step over 0.01 s engages the drag and drive limiters, so each step is
# a new realisation of the world and the three are compared as distributions, never row for row. Read with
# scripts/reads/pace-step-read.py. Runs under scratch\pace-step\runs, one world at a time, in the lane -Mask and
# -Threads name (lane B by default: logical 20-23, mask 0xF00000, 4 threads), at BelowNormal with the card hidden.
# Run only on the coordinator's go, in a HOLD window. Edits nothing and commits nothing. PowerShell's names ignore
# case: nothing here may be named $r beside $R.
param([int64]$Mask = 15728640, [int]$Threads = 4, [double]$Seconds = 300)
$ErrorActionPreference = 'Continue'
$me = [System.Diagnostics.Process]::GetCurrentProcess(); $me.ProcessorAffinity = [IntPtr]$Mask; $me.PriorityClass = 'BelowNormal'
$env:CUDA_VISIBLE_DEVICES = '-1'
$env:DOTNET_PROCESSOR_COUNT = "$Threads"
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
$R = 'D:\Projects\experiments\evolution-simulator'
# The screen's build: main at the pre-registration's commit with its Release farm, as rounds/launch-r62kg.ps1 uses it.
$b = "$R\scratch\wt-r62kg"
$exe = "$b\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
$runs = "$R\scratch\pace-step\runs"
New-Item -ItemType Directory -Force $runs | Out-Null
if (-not (Test-Path $exe)) { Write-Host "no Release farm at $exe; nothing started"; return }
function Wait-Arm([string]$arm, [int]$minutes) {
    $d = (Get-Date).AddMinutes($minutes)
    while ((Get-Date) -lt $d) {
        $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($m) {
            $j = Get-Content $m.FullName -Raw | ConvertFrom-Json
            if ($j.status -and $j.status -ne 'running') { Write-Host "$arm ended: $($j.status) $($j.reason) at $(Get-Date -Format HH:mm:ss)"; return $m.DirectoryName }
        }
        Start-Sleep -Seconds 15
    }
    Write-Host "${arm}: wait deadline passed"; return $null
}
foreach ($dt in @('0.01', '0.02', '0.025')) {
    $arm = 'pacestep-dt' + $dt.Replace('0.', '')
    Write-Host "== $arm, step $dt s, $Seconds s, from $(Get-Date -Format HH:mm:ss)"
    & "$b\scripts\run-farm.ps1" $arm -Launcher "$b\rounds\env-r62kg.ps1" -Seed 6201 -Seconds $Seconds -Threads $Threads `
        -RunsRoot $runs -WallMinutes 40 -Exe $exe -WaitForManifestSeconds 600 `
        -Env @{ EVOSIM_DT = $dt; EVOSIM_POSE_EVERY = 0.5 } | Select-String 'configHash|launched|refus|error|DIRTY'
    $dir = Wait-Arm $arm 45
    Write-Host "  $dir"
    if (-not $dir) { Write-Host "stopping: $arm did not end"; break }
}
Write-Host "== done at $(Get-Date -Format HH:mm:ss); read with: python -I $R\scripts\reads\pace-step-read.py $runs"
