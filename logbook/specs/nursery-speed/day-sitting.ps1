# The day sitting (2026-10-10), alone on the card under the coordinator's go and heat watch. First the stray test on
# the card (GpuStrayTests.AStrayIsLostOnTheCard, every case, r58-runner's test build in scratch/wt-card-r57). Then a
# timing read of six plain builds, two runs each in the palindromic order K E A R B G G B R A E K, so a drift over
# the sitting falls evenly on every build:
#   K c8762e9e (scratch/wt-w0), the reference build and the solo baseline;
#   E efa39d62 (scratch/wt-probe58), the spin sitting's build: K against E is the kernel ABBA, whether the
#     regenerated runners explain 4.42 against 5.24 times real time;
#   A d2488f3c (scratch/wt-day-a), 1a, the copies cut to the used slots;
#   R 1096d734 (scratch/wt-day-r), the arena;
#   B 96b0c597 (scratch/wt-r58-copies), the arena and 1b, the packed slots scattered;
#   G 5e618441 (scratch/wt-r58-grow), the world's body loops in parallel.
# Each run resumes r57-s2 at 47,000 s for 300 s through r58-runner's card-run.ps1 with -Exe, single, group 32, the
# transport on the card, EVOSIM_GPU_CONCURRENT=1 and EVOSIM_GPU_SCENT=1, the spin at 5,000, 16 threads on logical
# 16-31, no sampling beside it (a timing read), a one-minute cooling pause between runs. Each must be identical to
# r57-ref58-card1 and its engine line must say concurrent and scent. The pre-flight checks every build is plain, at
# its commit, clean, and hashes its binaries; each run re-hashes its own build first. Any failure, a run not
# identical, a driver event, a card peak of 80 C, a CPU reading of 85 C, or minute 45 stops the sitting.
# -PreflightOnly runs the checks alone and touches nothing on the card.
# Log: scratch/logs/day-sitting.log, last line "day sitting ends" or "day sitting STOPPED: <why>".
param([switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = "$main\scratch\wt-card-r57"
$runs = "$tree\scratch\card\runs"
$logs = "$tree\scratch\logs"
$ref = (Get-ChildItem "$main\scratch\nursery-speed\card-r57-runs\r57-ref58-card1" -Directory | Sort-Object LastWriteTime | Select-Object -Last 1).FullName
$log = if ($PreflightOnly) { "$main\scratch\logs\day-preflight-$(Get-Date -Format HHmmss).log" } else { "$main\scratch\logs\day-sitting.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Sitting([string]$why) {
    Remove-Item Env:EVOSIM_GPU_STRAY_CARD -ErrorAction SilentlyContinue
    Say "day sitting STOPPED: $why"; exit 3
}

$builds = [ordered]@{
    K = @{ Root = "$main\scratch\wt-w0"; Commit = 'c8762e9e' }
    E = @{ Root = "$main\scratch\wt-probe58"; Commit = 'efa39d62' }
    A = @{ Root = "$main\scratch\wt-day-a"; Commit = 'd2488f3c' }
    R = @{ Root = "$main\scratch\wt-day-r"; Commit = '1096d734' }
    B = @{ Root = "$main\scratch\wt-r58-copies"; Commit = '96b0c597' }
    G = @{ Root = "$main\scratch\wt-r58-grow"; Commit = '5e618441' }
}
$order = 'K', 'E', 'A', 'R', 'B', 'G', 'G', 'B', 'R', 'A', 'E', 'K'
$seen = @{}
$arms = foreach ($tag in $order) { $seen[$tag] = 1 + [int]$seen[$tag]; "r57-day$($tag.ToLowerInvariant())$($seen[$tag])" }
$began = Get-Date

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function WorldMarks([string]$dll) {
    $ctx = [System.Runtime.Loader.AssemblyLoadContext]::new('probe-check', $true)
    try {
        $asm = $ctx.LoadFromStream([System.IO.MemoryStream]::new([System.IO.File]::ReadAllBytes($dll)))
        $probe = $asm.GetType('Evosim.Core.WorldPhaseProbe')
        if (-not $probe) { return 0 }
        $tok = [BitConverter]::GetBytes([int]$probe.GetMethod('Mark').MetadataToken)
        $calls = 0
        foreach ($m in $asm.GetType('Evosim.Core.World').GetMethods([Reflection.BindingFlags]'Instance,Static,Public,NonPublic,DeclaredOnly')) {
            $b = $m.GetMethodBody(); if (-not $b) { continue }
            $il = $b.GetILAsByteArray()
            for ($i = 0; $i -lt $il.Length - 4; $i++) {
                if ($il[$i] -eq 0x28 -and $il[$i + 1] -eq $tok[0] -and $il[$i + 2] -eq $tok[1] -and $il[$i + 3] -eq $tok[2] -and $il[$i + 4] -eq $tok[3]) { $calls++ }
            }
        }
        return $calls
    }
    finally { $ctx.Unload() }
}
function Hashes([string]$root) {
    $bin = "$root\artifacts\Evosim.Farm\bin\Release\net8.0"
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
# A plain build: at its commit, a clean tree, no cycle probe, no scent order probe, no ILGPU O2, no world probe, the
# scent's switch bound.
function Plain([string]$tag) {
    $b = $builds[$tag]
    $root = $b.Root
    $head = git -C $root rev-parse --short HEAD
    $bin = "$root\artifacts\Evosim.Farm\bin\Release\net8.0"
    if (-not (Test-Path "$bin\Evosim.Farm.exe")) { Stop-Sitting "$tag ($root) has no farm build" }
    $hasCycle = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $scentOrder = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-scent-order'
    $scent = Has "$bin\Evosim.Farm.dll" 'EVOSIM_GPU_SCENT'
    $marks = WorldMarks "$bin\Evosim.Core.dll"
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $b.Hashes = Hashes $root
    Say "  $tag $root at ${head}: cycle probe $hasCycle, scent order probe $scentOrder, O2 $hasO2, World's probe marks $marks, binds EVOSIM_GPU_SCENT $scent; $($b.Hashes)"
    if ($head -ne $b.Commit) { Stop-Sitting "$tag ($root) is at $head, not $($b.Commit)" }
    if ($hasCycle -or $scentOrder -or $hasO2 -or $marks -ne 0) { Stop-Sitting "$tag ($root) is not a plain build" }
    if (-not $scent) { Stop-Sitting "$tag's build does not bind EVOSIM_GPU_SCENT" }
    $dirty = git -C $root status --short
    if ($dirty) { Stop-Sitting "$tag ($root) has uncommitted changes: $($dirty -join '; ')" }
}

function Free([string[]]$names) {
    foreach ($arm in $names) {
        foreach ($p in "$logs\card-run-$arm.log", "$logs\card-run-$arm-watch.log", "$runs\$arm", "$logs\$arm.out", "$logs\$arm.err",
                       "$logs\card-run-$arm-gpu.csv") {
            if (Test-Path $p) { Stop-Sitting "the arm name $arm is used: $p exists" }
        }
    }
    foreach ($p in "$logs\card-test-stray-day.log", "$logs\card-test-stray-day-watch.log") {
        if (Test-Path $p) { Stop-Sitting "the stray test's name is used: $p exists" }
    }
    Say "  arm names free: $($names -join ', '); stray-day free"
}

function Pause-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Sitting 'the machine did not cool within 5 minutes of the pause' }
        if ($now -ge $until) {
            $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File "$main\scripts\temp-check.ps1" | Out-Null
            if ($LASTEXITCODE -eq 0 -and $card -lt 60) { Say "  cool: card $card C, heat OK"; return }
        }
        Start-Sleep -Seconds 10
    }
}

# The last step's watch must be gone before card-run's busy check, which this mirrors: a farm, a test host or any
# dotnet, or a pwsh running a card test, an accelerator batch or a driver watch.
function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
            ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|driver-watch') })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Sitting "busy 90 s after the last step: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"
}

function CpuPeak([string]$arm) {
    $peak = -1
    foreach ($t in (Get-Content "$logs\card-run-$arm.log" | Where-Object { $_ -match 'heat: ' })) {
        if ($t -match 'cpu (\d+)C') { $peak = [Math]::Max($peak, [int]$Matches[1]) }
    }
    return $peak
}

function Check([string]$arm, [datetime]$since) {
    $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -LogArm $arm -Since $since.ToString('s') -Transport card *>&1
    $code = $LASTEXITCODE
    $out | ForEach-Object { Say "  check ${arm}: $_" }
    if ($code -ne 0) { Stop-Sitting "$arm failed a condition of card-run-check" }
    $peak = $out | Where-Object { "$_" -match 'card peak (\d+) C' } | Select-Object -First 1
    if ($peak -and [int]$Matches[1] -ge 80) { Stop-Sitting "$arm's card peak reached $($Matches[1]) C" }
    $cpu = CpuPeak $arm
    Say "  ${arm}'s CPU at card-run's heat lines: $cpu C"
    if ($cpu -ge 85) { Stop-Sitting "$arm's CPU reached $cpu C" }
    $run = (Get-ChildItem "$runs\$arm" -Directory | Sort-Object LastWriteTime | Select-Object -Last 1).FullName
    python -I "$main\scratch\wt-speed\scripts\reads\gpu-accept.py" $run $ref 2>&1 | Where-Object { $_ -match 'engine|ENGINE|stats|digest|lineage' } | ForEach-Object { Say "  accept ${arm}: $_" }
    $code = $LASTEXITCODE
    Say "  gpu-accept of $arm against r57-ref58-card1: exit $code (0 is identical)"
    if ($code -ne 0) { Stop-Sitting "$arm is not identical to r57-ref58-card1" }
}

function Solo([string]$arm, [string]$tag) {
    $b = $builds[$tag]
    $now = Hashes $b.Root
    if ($now -ne $b.Hashes) { Stop-Sitting "$tag's binaries changed since the pre-flight: $now" }
    $exe = "$($b.Root)\artifacts\Evosim.Farm\bin\Release\net8.0\Evosim.Farm.exe"
    $since = (Get-Date).AddSeconds(-30)
    Say "== $arm, build $tag ($($b.Commit)), alone at group 32 for 300 s"
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm, '-At', '47000', '-Seconds', '300',
        '-ResumeFrom', "$main\runs\r57-s2", '-Group', '32', '-WallMinutes', '15', '-Transport', '-Exe', $exe, '-SpinLimit', 'default')
    & pwsh @a *>&1 | Out-Null
    $lines = Get-Content "$logs\card-run-$arm.log"
    $lines | Where-Object { $_ -match 'begins|watch pid|pinned|transport in run.json|times real time|card: |books over|heat: |STOPPED' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Sitting "$arm did not end" }
    Check $arm $since
    $line = Get-Content "$logs\$arm.out" | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
    Say "  ${arm}'s $line"
    if ($line -notmatch ' concurrent' -or $line -notmatch ' scent' -or $line -notmatch ' transport') { Stop-Sitting "$arm's engine line lacks a switch: $line" }
}

$env:EVOSIM_GPU_CONCURRENT = '1'
$env:EVOSIM_GPU_SCENT = '1'
Remove-Item Env:EVOSIM_GPU_SCENT_ORDER, Env:EVOSIM_GPU_PROBE, Env:DOTNET_ThreadPool_UnfairSemaphoreSpinLimit, Env:CUDA_VISIBLE_DEVICES -ErrorAction SilentlyContinue
Say "day sitting begins: card-run and the stray test from $tree at $(git -C $tree rev-parse --short HEAD), reference $ref, order $($order -join ' '), EVOSIM_GPU_CONCURRENT=1 EVOSIM_GPU_SCENT=1"
if (git -C $tree status --short) { Stop-Sitting "$tree has uncommitted changes" }
foreach ($tag in $builds.Keys) { Plain $tag }
Free $arms
if ($PreflightOnly) { Say 'day preflight ends: nothing ran on the card'; exit 0 }

# The stray test on the card, every case.
Wait-Quiet
$env:EVOSIM_GPU_STRAY_CARD = '1'
Say '== the stray test on the card: GpuStrayTests.AStrayIsLostOnTheCard, every case, deadline 10 min'
& pwsh -NoProfile -File "$tree\scripts\card\card-test.ps1" -Name stray-day -Filter 'FullyQualifiedName~GpuStrayTests.AStrayIsLostOnTheCard' -Gate EVOSIM_GPU_STRAY_CARD -Minutes 10 *>&1 | Out-Null
$code = $LASTEXITCODE
Remove-Item Env:EVOSIM_GPU_STRAY_CARD -ErrorAction SilentlyContinue
Get-Content "$logs\card-test-stray-day.log" | Where-Object { $_ -match 'begins|Passed|Failed|Error Message|Total|driver events|over:|ends|STOPPED' } | ForEach-Object { Say "  $_" }
if ($code -ne 0) { Stop-Sitting "the stray test exited $code" }
Wait-Quiet

for ($n = 0; $n -lt $order.Count; $n++) {
    if (((Get-Date) - $began).TotalMinutes -gt 42) { Stop-Sitting "$($arms[$n]) not started: the sitting would pass minute 45" }
    Pause-Cool
    Wait-Quiet
    Solo $arms[$n] $order[$n]
}

python -I "$tree\scripts\reads\pace-calc.py" $runs @arms 2>&1 | ForEach-Object { Say "  pace: $_" }
Say 'day sitting ends'
