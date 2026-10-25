# The crowd fixture after D228-D229's tunables (ShapedAddedMass, ReflectMirroredAnchors, ContactShape), on branch
# fin-shape (scratch/wt-finshape), the pattern of logbook/specs/contact-eating-build/fixtures.ps1: the Release farm from
# a clean tree, so the manifest's gitCommit names the commit; then round 44's world, seed 4, 20,000 s, record format 1,
# as cefix-s4, at 4 threads, for src/Evosim.Dynamics.Tests/RunFixture.cs. Every new switch is off, so its stats rows
# should equal the last fixture's (runs/cefix-s4/2026-10-24-170754-85523498) apart from the wall clocks
# (scripts/reads/compare-run-rows.py). This process pins itself to logical 8-11 at BelowNormal with the card hidden
# before it starts anything, so the farm inherits all three. It builds nothing; the caller builds the Release farm
# first. Commits nothing, and edits nothing: the caller names the run in RunFixture.cs and runs the suites. On the
# coordinator's go (2026-10-25).
$ErrorActionPreference = 'Continue'
$me = [System.Diagnostics.Process]::GetCurrentProcess(); $me.ProcessorAffinity = [IntPtr]0xF00; $me.PriorityClass = 'BelowNormal'
$env:CUDA_VISIBLE_DEVICES = '-1'
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
$Root = 'D:\Projects\experiments\evolution-simulator'
$w = "$Root\scratch\wt-finshape"
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
