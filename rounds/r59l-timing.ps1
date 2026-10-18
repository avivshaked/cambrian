# Round 59's leaf-smell timing (D174; logbook/specs/r59-leaf-timing-plan.md): the leaf field's fill and the whole
# step's pace on the card, at the smell's reach of 6 m and of 3 m, in round 59's world (rounds/env-r59l.ps1, seed 1).
# One step a call, on this checkout's Release build:
#   found   off the card: the CPU engine founds the world twice, r59l-found6 and r59l-found3, to 1,000 s with a
#           checkpoint there, on 4 threads pinned to logical 16-19, one after the other; a world founded already is
#           read again rather than founded twice. Each run's config.json is
#           read for its reach, and the two configHashes must differ.
#   check   off the card: the CPU accelerator, with the card's switches, resumes r59l-found6 at 1,000 s for 2 s on
#           4 threads pinned to logical 16-19, so a refusal on the card's resume path is met before the window.
#   window  the card, alone, in a quiet machine the coordinator holds. From each world's 1,000 s checkpoint:
#           1. the pace: four 300 s runs on this Release build, 6, 3, 3, 6 m (r59l-p6-1, r59l-p3-2, r59l-p3-3,
#              r59l-p6-4);
#           2. the fill: four 60 s runs of a world-probe build (-p:WorldPhaseProbe=1) of the same commit in
#              -ProbeTree, the same order (r59l-w6-1, r59l-w3-2, r59l-w3-3, r59l-w6-4). The probe prints the world
#              step's phases every 20 s to the farm's stderr; "(scent fill)" is the leaf field's host fill, with
#              the carrion's hand-over to the card beside it;
#           3. the reads: pace-calc.py over the pace runs and phase-calc.py over each probe run.
#           Each run is a card-run.ps1 resume at group 32, single, with the round's card switches (transport,
#           concurrent, scent, spin 5,000), 16 threads on logical 16-31, its heat stop and its driver watch, after a
#           minute's cooling. A resume of a CPU engine's checkpoint is a cousin (card-run passes
#           -AllowSourceMismatch); a timing needs no identity.
# Before it runs: this checkout at -Commit and clean, a plain Release build, the names free; for check and window
# both founding runs ended by their budgets with their 1,000 s checkpoints; for window -ProbeTree at -Commit and
# clean with a world-probe build, the card's memory under 4,500 MiB and nothing else running.
# After each run: an ending by budget, books closed, the manifest's source.gitCommit and gitDirty, and on the card
# card-run-check's every condition and the overflow counts all 0. Any failure stops with exit 3.
# -PreflightOnly runs the checks alone and starts nothing.
# Log: scratch/logs/r59l-<step>.log in this checkout, last line "r59l <step> ends" or "r59l <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][ValidateSet('found', 'check', 'window')][string]$Step,
    [string]$ProbeTree = 'D:\Projects\experiments\evolution-simulator\scratch\wt-r59-leafprobe',
    [int]$Seconds = 300,
    [int]$ProbeSeconds = 60,
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$tree = Split-Path -Parent $PSScriptRoot
$runs = "$tree\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$probeBin = "$ProbeTree\artifacts\Evosim.Farm\bin\Release\net8.0"
$probeExe = "$probeBin\Evosim.Farm.exe"
$launcher = "$tree\rounds\env-r59l.ps1"
$at = 1000
$reaches = @(6, 3)
$order = @(6, 3, 3, 6)
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r59l-$Step-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r59l-$Step.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r59l $Step STOPPED: $why"; exit 3 }

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes([string]$dir) {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$dir\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
function RunDir([string]$name) { (Get-ChildItem "$runs\$name" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }

# A checkout at -Commit, clean, with a Release build that is no card probe and no O2. The world probe's methods
# are compiled into every build and only their calls are dropped, so its build is told by its Core differing from
# the plain one, and each run by its stderr.
function Checkout([string]$where, [string]$dir) {
    $head = git -C $where rev-parse --short HEAD
    Say "  $where at $head, asked for $Commit"
    if ($head -ne $Commit) { Stop-Step "$where is at $head, not $Commit" }
    $dirty = git -C $where status --porcelain --untracked-files=no
    if ($dirty) { Stop-Step "DIRTY in ${where}: $($dirty -join '; ')" }
    if (-not (Test-Path "$dir\Evosim.Farm.exe")) { Stop-Step "no Release farm in $dir" }
    $cardProbe = Has "$dir\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$dir\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $h = Hashes $dir
    Say "  build in ${dir}: card probe $cardProbe, O2 $hasO2; $h"
    if ($cardProbe -or $hasO2) { Stop-Step "$dir is not a plain build of the card" }
    return $h
}

function Founded {
    foreach ($r in $reaches) {
        $dir = RunDir "r59l-found$r"
        if (-not $dir) { Stop-Step "no run under $runs\r59l-found$r" }
        $ck = "$dir\checkpoints\$('{0:D9}' -f $at).ckpt"
        if (-not (Test-Path $ck)) { Stop-Step "no checkpoint at $at s: $ck" }
        $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
        if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$dir has not ended by its budget ($($m.status), $($m.reason))" }
        Say "  r59l-found${r}: $dir, configHash $($m.configHash), source.gitCommit $($m.source.gitCommit)"
    }
}

function Free([string[]]$names) {
    foreach ($n in $names) {
        foreach ($p in "$logs\card-run-$n.log", "$runs\$n", "$runs\$n.md", "$logs\$n.out", "$logs\$n.err", "$runs\$n-preflight") {
            if (Test-Path $p) { Stop-Step "the name $n is used: $p exists" }
        }
    }
    Say "  names free: $($names -join ', ')"
}

function CardIdle {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    Say "  card memory used $mem MiB (limit 4500); Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { 'running' } else { 'none' })"
    if ($mem -gt 4500) { Stop-Step "the card holds $mem MiB, over 4500" }
    if ($resolve.Count -gt 0) { Stop-Step 'Resolve is running, and the window is a quiet machine' }
    if ($unity | Where-Object { $_.CommandLine -match '-batchmode' }) { Stop-Step 'a batch-mode Unity is running' }
}

function Pause-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Step 'the machine did not cool within 5 minutes of the pause' }
        if ($now -ge $until) {
            $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File "$tree\scripts\temp-check.ps1" | Out-Null
            if ($LASTEXITCODE -eq 0 -and $card -lt 60) { Say "  cool: card $card C, heat OK"; return }
        }
        Start-Sleep -Seconds 10
    }
}

# Nothing else steps a world or runs a test: the window's timing is read on a quiet machine.
function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(Evosim\.Farm|Evosim\.Nursery\w*|testhost)\.exe$' -or
            ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run|card-group|driver-watch') })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Step "busy 90 s before the run: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"
}

# A finished run's manifest, held to the step's conditions.
function Manifest([string]$name, [bool]$card) {
    $dir = RunDir $name
    if (-not $dir) { Stop-Step "no run under $runs\$name" }
    $m = Get-Content "$dir\run.json" -Raw | ConvertFrom-Json
    Say "  ${name}: status $($m.status) ($($m.reason)), engine $($m.engine), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false) { Stop-Step "${name}'s manifest names a dirty tree" }
    if ($m.source.gitCommit -notlike "$Commit*") {
        # A run from an earlier commit of this branch stands when no source differs: the launcher's own edits
        # change no build.
        git -C $tree diff --quiet $m.source.gitCommit $Commit -- src
        if ($LASTEXITCODE -ne 0) { Stop-Step "${name}'s manifest names $($m.source.gitCommit), whose src differs from $Commit's" }
        Say "  ${name}: from $($m.source.gitCommit.Substring(0, 8)), whose src is $Commit's"
    }
    if ($card) {
        $g = $m.gpu
        $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
        Say "  ${name}: overflow candidates/overlaps/held/entries $over"
        if ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    }
    return $m
}

# An off-card farm started by run-farm, pinned by the processId its run.json names, waited on to its end.
function Wait-Farm([string]$name, [long]$mask, [int]$minutes) {
    $deadline = (Get-Date).AddMinutes($minutes)
    $pinned = $false
    $j = $null
    while ((Get-Date) -lt $deadline) {
        $mf = Get-ChildItem "$runs\$name\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
        if ($mf) {
            $j = Get-Content $mf.FullName -Raw | ConvertFrom-Json
            if (-not $pinned -and $j.status -eq 'running') {
                $p = Get-Process -Id $j.processId -ErrorAction SilentlyContinue
                if ($p -and $p.ProcessName -eq 'Evosim.Farm') {
                    $p.ProcessorAffinity = [IntPtr]$mask; $p.PriorityClass = 'BelowNormal'; $pinned = $true
                    Say "  ${name}: pid $($p.Id) pinned to $mask, BelowNormal"
                }
            }
            if ($j.status -ne 'running') { break }
        }
        Test-OffCardTree $deadline
        Start-Sleep -Seconds 5
    }
    if (-not $j -or $j.status -eq 'running') { Stop-Step "$name did not end within $minutes minutes" }
    if (-not $pinned) { Stop-Step "${name}'s farm was never seen to pin" }
}

# The environment the farm inherits: no EVOSIM_ setting but the step's own.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
if ($Step -ne 'found') {
    $env:EVOSIM_GPU_CONCURRENT = '1'
    $env:EVOSIM_GPU_SCENT = '1'
}
Say "r59l $Step begins: runs in $runs; environment $((Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ')"
$hashes = Checkout $tree $bin
$pace = @(); $fill = @()
for ($i = 0; $i -lt $order.Count; $i++) { $pace += "r59l-p$($order[$i])-$($i + 1)"; $fill += "r59l-w$($order[$i])-$($i + 1)" }
switch ($Step) {
    'found' { Free @($reaches | Where-Object { -not (RunDir "r59l-found$_") } | ForEach-Object { "r59l-found$_" }) }
    'check' { Founded; Free @('r59l-check') }
    'window' {
        Founded
        $probeHashes = Checkout $ProbeTree $probeBin
        $core = { param($h) ($h -split ', ' | Where-Object { $_ -like 'Evosim.Core.dll *' }) }
        if ((& $core $probeHashes) -eq (& $core $hashes)) { Stop-Step "$ProbeTree's Core is the plain build's; build it with -p:WorldPhaseProbe=1" }
        Free ($pace + $fill)
        CardIdle
    }
}
if ($PreflightOnly) { Say "r59l $Step preflight ends: nothing started"; exit 0 }

. "$tree\scripts\card\nvcuda-guard.ps1"

# ---- found: the CPU engine, off the card, one world at each reach
if ($Step -eq 'found') {
    Enter-OffCard
    $hashesSeen = @{}
    foreach ($r in $reaches) {
        $name = "r59l-found$r"
        if (RunDir $name) {
            Say "== $name, reach $r m: founded already, read again"
        } else {
            Say "== $name, reach $r m"
            & "$tree\scripts\run-farm.ps1" $name -Launcher $launcher -Seed 1 -Seconds $at -Threads 4 -RunsRoot $runs `
                -WallMinutes 90 -Exe $exe -Env @{ EVOSIM_LEAF_SCENT_RANGE = $r; EVOSIM_DIGEST_EVERY = 100 } -CheckpointEvery $at 6>&1 2>&1 |
                ForEach-Object { Say "  $name> $_" }
            Wait-Farm $name 983040 95
        }
        $m = Manifest $name $false
        if (-not (Test-Path "$(RunDir $name)\checkpoints\$('{0:D9}' -f $at).ckpt")) { Stop-Step "$name has no checkpoint at $at s" }
        $text = Get-Content "$(RunDir $name)\config.json" -Raw
        $sense = ($text | ConvertFrom-Json).sense
        $anchoring = $text -match '"id":\s*"anchoring"'
        Say "  ${name}: leafScent $($sense.leafScent), range $($sense.leafScentRangeMetres) m, cell $($sense.leafScentCellMetres) m, half $($sense.leafScentHalfScaleSquareMetres) m2, anchoring in the table $anchoring"
        if ($sense.leafScent -ne 'Direction' -or [double]$sense.leafScentRangeMetres -ne $r) { Stop-Step "${name}'s config is not the direction form at $r m" }
        if ([double]$sense.leafScentCellMetres -ne 2 -or [double]$sense.leafScentHalfScaleSquareMetres -ne 1) { Stop-Step "${name}'s cell or half scale is not 2 m and 1 m2" }
        if ($anchoring) { Stop-Step "${name}'s cell table carries the anchoring cell" }
        $last = Get-Content "$(RunDir $name)\stats.jsonl" | Select-Object -Last 1 | ConvertFrom-Json
        Say "  ${name}: t $($last.t), alive $($last.alive), $([math]::Round([double]$m.wallClockMinutes, 1)) wall min"
        $hashesSeen[$m.configHash] = $r
    }
    if ($hashesSeen.Count -ne 2) { Stop-Step 'the two worlds have one configHash, so the reach did not reach the config' }
    $nv = Resolve-OffCard
    Say "  $nv"
    if ($nv -notmatch 'loaded by none') { Stop-Step 'the founding loaded nvcuda' }
    Say "r59l $Step ends"
    exit 0
}

# ---- check: the CPU accelerator, off the card, the card's resume path for 2 s
if ($Step -eq 'check') {
    Enter-OffCard
    $envs = @{ EVOSIM_ENGINE = 'gpu'; EVOSIM_GPU_DEVICE = 'cpu'; EVOSIM_GPU_PRECISION = 'single'; EVOSIM_GPU_GROUP = 32
               EVOSIM_GPU_TRANSPORT = 1; EVOSIM_DIGEST_EVERY = 100 }
    Say '== r59l-check'
    & "$tree\scripts\run-farm.ps1" 'r59l-check' -ResumeFrom (RunDir 'r59l-found6') -At $at -Seconds ($at + 2) -Threads 4 -RunsRoot $runs `
        -WallMinutes 60 -Exe $exe -Env $envs -AllowSourceMismatch 6>&1 2>&1 | ForEach-Object { Say "  r59l-check> $_" }
    Wait-Farm 'r59l-check' 983040 65
    $m = Manifest 'r59l-check' $true
    Say "  r59l-check: engine $($m.engine), device $($m.gpu.device), transport $($m.transport), $([math]::Round([double]$m.wallClockMinutes, 1)) wall min"
    $nv = Resolve-OffCard
    Say "  $nv"
    if ($nv -notmatch 'loaded by none') { Stop-Step 'the check loaded nvcuda' }
    Say "r59l $Step ends"
    exit 0
}

# ---- window: the card, alone
function CardRun([string]$name, [int]$reach, [int]$seconds, [string]$farm, [string]$dir, [string]$built) {
    Pause-Cool
    Wait-Quiet
    CardIdle
    if ((Hashes $dir) -ne $built) { Stop-Step "the binaries in $dir changed since the pre-flight" }
    $since = (Get-Date).AddSeconds(-30)
    [string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $name, '-At', "$at", '-Seconds', "$seconds",
        '-ResumeFrom', (RunDir "r59l-found$reach"), '-Group', '32', '-WallMinutes', '20', '-RunsRoot', $runs, '-Exe', $farm,
        '-HeatStop', '-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside')
    Say "== ${name}, reach $reach m: card-run $($a[3..($a.Count - 1)] -join ' ')"
    & pwsh @a *>&1 | Out-Null
    $lines = Get-Content "$logs\card-run-$name.log"
    $lines | Where-Object { $_ -match 'ended \(|stopped \(|card: |books over|STOPPED|warning' } | ForEach-Object { Say "  $_" }
    if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$name did not end" }
    if (-not ($lines | Where-Object { $_ -match 'ended \(budget\)' })) { Stop-Step "$name did not end by its budget" }
    if (-not ($lines | Where-Object { $_ -match "books over .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${name}'s books did not close" }
    $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $name -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs *>&1
    $code = $LASTEXITCODE
    $out | ForEach-Object { Say "  check ${name}: $_" }
    if ($code -ne 0) { Stop-Step "$name failed a condition of card-run-check" }
    $engine = Get-Content "$logs\$name.out" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
    Say "  ${name}'s $engine"
    if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${name}'s engine line lacks a switch" }
    $probeLines = @(Get-Content "$logs\$name.err" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^world-probe, ' }).Count
    Say "  ${name}: $probeLines world-probe lines in its stderr"
    if (($farm -eq $probeExe) -ne ($probeLines -gt 0)) { Stop-Step "${name}'s stderr does not match its build ($probeLines world-probe lines)" }
    $null = Manifest $name $true
}

for ($i = 0; $i -lt $order.Count; $i++) { CardRun $pace[$i] $order[$i] $Seconds $exe $bin $hashes }
for ($i = 0; $i -lt $order.Count; $i++) { CardRun $fill[$i] $order[$i] $ProbeSeconds $probeExe $probeBin $probeHashes }

python -I "$tree\scripts\reads\pace-calc.py" $runs @pace 2>&1 | ForEach-Object { Say "  pace: $_" }
foreach ($name in $fill) {
    $err = "$logs\$name.err"
    if (-not (Test-Path $err)) { Say "  fill ${name}: no stderr at $err"; continue }
    python -I "$tree\scripts\reads\phase-calc.py" $runs $name $err 2>&1 | ForEach-Object { Say "  fill ${name}: $_" }
}
Say "r59l $Step ends"
