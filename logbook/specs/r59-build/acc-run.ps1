# The card's kernels on ILGPU's CPU accelerator, off the card (CUDA hidden): round 57's world with round 59's
# pieces resumed from r59smoke-t16's 1,000 s checkpoint for -Seconds, as the card runs r59card-r57w32/64 were,
# in -Precision single or double, the farm on 4 threads pinned to logical 16-19 at BelowNormal, -WallMinutes
# wall. -Worktree names the checkout under scratch/ whose Release farm runs, -Tag ends the arm's name. Prints the
# last stats rows at the end. Commits nothing. The 10 s are read with held-delta.py (the contact counts a step,
# from r59smoke-t16's own 1,000 s row) and pos-compare.py (each body against the double run's at 1,010 s).
param(
    [Parameter(Mandatory)][ValidateSet('single', 'double')][string]$Precision,
    [int]$Seconds = 10,
    [int]$WallMinutes = 15,
    [string]$Worktree = 'wt-r59-anchor',
    [string]$Tag = '')
$ErrorActionPreference = 'Continue'
$R = 'D:\Projects\experiments\evolution-simulator'
$w = "$R\scratch\$Worktree"
$runs = "$w\scratch\card\runs"
$arm = "r59acc-r57w-$Precision$Tag"
$ckpt = "$R\runs\r59smoke-t16\2026-10-13-040808-19eb1f48\checkpoints\000001000.ckpt"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
if (Test-Path "$runs\$arm") { Write-Host "$runs\$arm exists; nothing started"; exit 1 }
if (Get-Process Evosim.Farm -ErrorAction SilentlyContinue) { Write-Host "a farm is running; nothing started"; exit 1 }

. "$w\scripts\card\nvcuda-guard.ps1"
Enter-OffCard
Write-Host "CUDA_VISIBLE_DEVICES=$env:CUDA_VISIBLE_DEVICES"

$envs = @{ EVOSIM_ENGINE = 'gpu'; EVOSIM_GPU_DEVICE = 'cpu'; EVOSIM_GPU_PRECISION = $Precision; EVOSIM_GPU_GROUP = 32
           EVOSIM_DIGEST_EVERY = 100 }
$t0 = Get-Date
Write-Host "== $arm from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" $arm -ResumeFrom $ckpt -At 1000 -Seconds (1000 + $Seconds) -Threads 4 -RunsRoot $runs `
    -WallMinutes $WallMinutes -AllowSourceMismatch -Exe $exe -Env $envs -SpinLimit default 6>&1 2>&1 |
    Select-String 'configHash|launched|refus|error|DIRTY|engine|device|precision|resumed'

Get-Process Evosim.Farm -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $t0 } | ForEach-Object {
    $_.ProcessorAffinity = [IntPtr][long]983040; $_.PriorityClass = 'BelowNormal'
    Write-Host "affinity 16-19 on pid $($_.Id)" }

$deadline = (Get-Date).AddMinutes($WallMinutes + 3)
while ((Get-Date) -lt $deadline) {
    $m = Get-ChildItem "$runs\$arm\*\run.json" -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($m) {
        $j = Get-Content $m.FullName -Raw | ConvertFrom-Json
        if ($j.status -and $j.status -ne 'running') { Write-Host "$arm ended: $($j.status) $($j.reason) at $(Get-Date -Format HH:mm:ss)"; break }
    }
    Start-Sleep -Seconds 15
}
$s = Get-ChildItem "$runs\$arm\*\stats.jsonl" -ErrorAction SilentlyContinue | Select-Object -Last 1
if ($s) { Get-Content $s.FullName | Select-Object -Last 3 | ForEach-Object { $_.Substring(0, [Math]::Min(300, $_.Length)) } }
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
