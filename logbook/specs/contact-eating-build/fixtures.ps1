# The crowd fixture after D219-D221's tunables (ContactEating, RemainsWindowSeconds), on branch mouth-point
# (scratch/wt-mouthpoint), the pattern of logbook/specs/r62-spoil-build/fixtures.ps1: the Release farm from a clean tree,
# so the manifest's gitCommit names the commit; then round 44's world, seed 4, 20,000 s, record format 1, as cefix-s4,
# at 4 threads, for src/Evosim.Dynamics.Tests/RunFixture.cs. The pace guard's ratio is 0.1 from the start: at 0.25 it stopped the first unbroken try at 9,040 s, its last 10
# minutes against a fast first 470 s on a quiet machine, with every row equal to r62sfix-s4's. With the
# dial off its stats rows should equal r62sfix-s4's apart from the wall clocks and the intake-origin columns
# (scripts/reads/compare-run-rows.py). This process pins itself to logical 8-11 at BelowNormal with the card hidden
# before it starts anything, so the farm inherits all three; the coordinator moved it there beside the r62ce screen on
# 16-29. It builds nothing: the screen runs from the same Release farm, built from 4935a4b8's source, and a build
# beside it would rewrite the files that farm has open. Commits nothing, and edits nothing: the caller names the run in
# RunFixture.cs and runs the suites. On the coordinator's go (2026-10-24).
$ErrorActionPreference = 'Continue'
$me = [System.Diagnostics.Process]::GetCurrentProcess(); $me.ProcessorAffinity = [IntPtr]0xF00; $me.PriorityClass = 'BelowNormal'
$env:CUDA_VISIBLE_DEVICES = '-1'
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
$Root = 'D:\Projects\experiments\evolution-simulator'
$w = "$Root\scratch\wt-mouthpoint"
$runs = "$Root\runs"
$exe = "$w\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
if (git -C $w status --porcelain --untracked-files=no) { Write-Host "the worktree is not clean; nothing started"; return }
if (-not (Test-Path $exe)) { Write-Host "no Release farm at $exe; nothing started"; return }
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
Write-Host "== at $(git -C $w log --oneline -1), the farm built $((Get-Item $exe).LastWriteTime.ToString('s'))"
Write-Host "== the crowd fixture (4 threads), from $(Get-Date -Format HH:mm:ss)"
& "$w\scripts\run-farm.ps1" cefix-s4 -Launcher "$w\rounds\env-r44.ps1" -Seed 4 -Seconds 20000 -Threads 4 -RunsRoot $runs -WallMinutes 300 -Exe $exe -Env @{ EVOSIM_RECORD_FORMAT = 1; EVOSIM_PACE_RATIO = 0.1 } | Select-String 'configHash|launched|refus|error|DIRTY'
$fix = Wait-Arm 'cefix-s4' 305
Write-Host "the crowd fixture is $fix"
Write-Host "== done at $(Get-Date -Format HH:mm:ss)"
