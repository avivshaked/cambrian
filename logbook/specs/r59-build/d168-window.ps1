# D168's card window, alone on the card in the coordinator's window: r59-anchor's build against r59-pos64's.
#   1. Pace and identity: round 57's world with round 59's pieces resumed from r59smoke-t16 at 1,000 s for 300 s,
#      the two builds interleaved at each group (anchor, pos64, pos64, anchor at 32, then the same at 64), each a
#      card-run.ps1 run on 16 threads pinned to logical 16-31, with its heat stop. A pause between runs.
#   2. Registers: a 30 s -Probe run of each build at group 32; the kernels' shapes go to scratch/logs/<arm>.err.
#   3. The carry read-back: DriftDiagnostic.SlowBodiesDriftOnTheCard through card-test.ps1, gate EVOSIM_DRIFT_CARD.
#   4. The reads: pace-calc.py over the eight pace runs (the stepping wall), gpu-accept.py between the groups for
#      each build, and the registers' lines from the probe logs.
# Any run or test that does not end cleanly stops the sitting. Both Release farms are built before the window,
# off the card, from clean trees. Run it in a child shell: card-run.ps1 moves its own shell to its checkout.
# Log: scratch/logs/d168-window.log in this checkout, last line "d168 window ends" or "d168 window STOPPED: <why>".
param(
    [string]$Anchor = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r59-anchor',
    [string]$From = 'D:\Projects\experiments\evolution-simulator\runs\r59smoke-t16\2026-10-13-040808-19eb1f48',
    [int]$Seconds = 300,
    [int]$PauseSeconds = 60)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$card = Join-Path $root 'scripts\card'
$logs = Join-Path $root 'scratch\logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$log = Join-Path $logs 'd168-window.log'
if (Test-Path $log) { Write-Host "$log exists; rename the last window's log first"; exit 1 }
function Say([string]$m) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; Add-Content -Path $log -Value $line; Write-Host $line }
function Stop([string]$why) { Say "d168 window STOPPED: $why"; exit 1 }

$exe = @{
    a = Join-Path $Anchor 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe'
    p = Join-Path $root 'artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe'
}
foreach ($k in $exe.Keys) { if (-not (Test-Path $exe[$k])) { Stop "no Release farm at $($exe[$k])" } }
Say "d168 window begins: anchor $(git -C $Anchor log --oneline -1), pos64 $(git -C $root log --oneline -1)"
foreach ($w in $Anchor, $root) { if (git -C $w status --porcelain --untracked-files=no) { Stop "$w is not clean" } }

function CardRun([string]$arm, [string]$build, [int]$group, [int]$seconds, [switch]$probe) {
    $farmArgs = @('-NoProfile', '-File', (Join-Path $card 'card-run.ps1'), '-Arm', $arm, '-At', '1000', '-Seconds', "$seconds",
              '-ResumeFrom', $From, '-Group', "$group", '-Exe', $exe[$build], '-WallMinutes', '15', '-HeatStop')
    if ($probe) { $farmArgs += '-Probe' }
    for ($try = 1; $try -le 2; $try++) {
        Say "== $arm ($build, group $group, $seconds s$(if ($probe) { ', probe' }))"
        & pwsh @farmArgs 2>&1 | Out-Null
        $runLog = Join-Path $logs "card-run-$arm.log"
        $last = if (Test-Path $runLog) { Get-Content $runLog -Tail 1 } else { '' }
        if ($last -match 'card run ends') { Say "   $arm ends"; return }
        if ($try -eq 1 -and $last -match 'busy') {
            Say "   $arm refused as busy; again in $PauseSeconds s"
            Rename-Item $runLog "card-run-$arm-busy.log"
            Start-Sleep -Seconds $PauseSeconds
            continue
        }
        Stop "$arm did not end: $last"
    }
}

# ---- 1. pace and identity, interleaved
foreach ($g in 32, 64) {
    $n = 0
    foreach ($b in 'a', 'p', 'p', 'a') {
        $n++
        CardRun "d168-$b$g-$n" $b $g $Seconds
        Start-Sleep -Seconds $PauseSeconds
    }
}

# ---- 2. registers
foreach ($b in 'a', 'p') {
    CardRun "d168-$b-probe" $b 32 30 -probe
    Start-Sleep -Seconds $PauseSeconds
}

# ---- 3. the carry read-back
$dotnet = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\DotNetSdk\dotnet.exe' | Select-Object -First 1).FullName
$env:MSBUILDDISABLENODEREUSE = '1'
# Release, which card-test runs with --no-build. No compiler server: one left resident is a dotnet.exe, and card-test refuses to start beside it.
& $dotnet build (Join-Path $root 'src\Evosim.Farm.Tests\Evosim.Farm.Tests.csproj') -c Release -m:2 -nologo -v:q -nodeReuse:false -p:UseSharedCompilation=false 2>&1 | Select-Object -Last 2 | ForEach-Object { Say "   build: $_" }
& $dotnet build-server shutdown 2>&1 | Out-Null
& pwsh -NoProfile -File (Join-Path $card 'card-test.ps1') -Name d168-drift -Filter 'FullyQualifiedName~DriftDiagnostic.SlowBodiesDriftOnTheCard' -Gate EVOSIM_DRIFT_CARD -Minutes 12 2>&1 | Out-Null
$testLog = Join-Path $logs 'card-test-d168-drift.log'
$last = if (Test-Path $testLog) { Get-Content $testLog -Tail 1 } else { '' }
if ($last -notmatch 'card test ends') { Stop "the drift test did not end: $last" }
Get-Content $testLog | Select-String 'Passed|Failed|moved' | ForEach-Object { Say "   drift: $($_.Line.Trim())" }
$drift = Join-Path $root 'scratch\r59-tools\drift-card.txt'
if (Test-Path $drift) { Get-Content $drift | ForEach-Object { Say "   $_" } }

# ---- 4. the reads
$runs = Join-Path $root 'scratch\card\runs'
$pace = @('d168-a32-1', 'd168-p32-2', 'd168-p32-3', 'd168-a32-4', 'd168-a64-1', 'd168-p64-2', 'd168-p64-3', 'd168-a64-4')
python -I (Join-Path $root 'scripts\reads\pace-calc.py') $runs @pace 2>&1 | ForEach-Object { Say "   pace: $_" }
foreach ($pair in @(('d168-a32-1', 'd168-a64-1'), ('d168-p32-2', 'd168-p64-2'), ('d168-p32-2', 'd168-p32-3'))) {
    python -I (Join-Path $root 'scripts\reads\gpu-accept.py') $pair[0] $pair[1] --runs-root $runs 2>&1 |
        Select-String 'identical|differ|first|byte' | ForEach-Object { Say "   accept $($pair[0]) $($pair[1]): $($_.Line.Trim())" }
}
foreach ($b in 'a', 'p') {
    $err = Join-Path $logs "d168-$b-probe.err"
    if (Test-Path $err) { Get-Content $err | Select-String 'regs|registers|local' | Select-Object -First 30 | ForEach-Object { Say "   shape ${b}: $($_.Line.Trim())" } }
}
Say 'd168 window ends'
