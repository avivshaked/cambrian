# The gripping grazer control (logbook/specs/grazer-grip-control-plan.md): one step a call, on this checkout's
# Release build, round 57's world with round 59's pieces (rounds/env-r59g.ps1, seed 1):
#   check      r59g-check, r59g-check-r  off the card: the CPU accelerator founds the world at 4 threads with
#                                        the card's switches, 30 s with checkpoints every 15 s, then resumes
#                                        at 15 s to 30 s, read against the whole run by group-resume-compare.
#                                        Prints the configHash the found step names.
#   found-short r59g-found60 the card, founded, 60 s, no checkpoints: the founded card run's own check before
#                           the long one (-ConfigHash from check).
#   found      r59g-found   the card, founded, 12,000 s, checkpoints every 1,500 s (-ConfigHash from check).
#   zero       r59g-zero    the card, r59g-found resumed at 12,000 s for 300 s, no grazers, read against
#                           r59g-found by group-resume-compare.
#   smoke      r59g-smoke   the card, 60 grazer-grip at -Depth, 60 s, checkpoints every 30 s.
#   smoke-cpu  r59g-smoke-c the CPU engine off the card, the same 60 s.
#   grip       r59g-grip    the card, 60 grazer-grip at -Depth, 3,000 s, checkpoints every 300 s.
#   grip-cpu   r59g-grip-c  the CPU engine off the card, the same grazers' first 600 s, checkpoints every 300 s.
#   drift      r59g-drift   the card, 60 grazer-plate-late at -Depth, 3,000 s, checkpoints every 300 s.
# The plan's gate, read after smoke and smoke-cpu with ckpt-grip.py: at least 15 of 60 grazers hold a grip on a
# cap at 60 s on the card, and the CPU twin within a few. Below it nothing longer runs.
# The grazer steps set the assay's four settings in this script's environment, which the farm inherits: a
# resume takes them from the launch rather than from the resumed config (Program.cs), and card-run passes no
# setting for them. The card steps also set the round's two card switches (EVOSIM_GPU_CONCURRENT, EVOSIM_GPU_SCENT)
# that way; card-run adds the transport and the spin. The card steps run in a window the coordinator holds.
# Before it runs: this checkout at -Commit and clean, a plain Release build (no probe or O2), the genome present,
# the names free, and for a card step a minute's cooling to a card under 60 C with temp-check OK, a quiet machine
# and the card's memory under 4,500 MiB (8,000 beside Resolve). For a resumed step, r59g-found ended by its
# budget with its 12,000 s checkpoint.
# After it: an ending by budget, books closed, card-run-check's every condition (a card step), the manifest's
# source.gitCommit and gitDirty, the card's overflow counts all 0, and for a grazer step the manifest's
# inoculateGenomeHash. Any failure stops with exit 3. -PreflightOnly runs the checks alone and starts nothing.
# Log: scratch/logs/r59g-<step>.log in this checkout, last line "r59g <step> ends" or "r59g <step> STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][ValidateSet('check', 'found-short', 'found', 'zero', 'smoke', 'smoke-cpu', 'grip', 'grip-cpu', 'drift')][string]$Step,
    [double]$Depth = 4,
    [string]$ConfigHash = '',
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$tree = Split-Path -Parent $PSScriptRoot
$runs = "$tree\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$launcher = "$tree\rounds\env-r59g.ps1"
$at = 12000
$grip = "$tree\inocula\grazer-grip\grazer-grip.json"
$plate = "$tree\inocula\grazer-control\grazer-plate-late.json"
$plan = @{
    check       = @{ Arm = 'r59g-check'; Seconds = 30; Card = $false; Genome = '' }
    'found-short' = @{ Arm = 'r59g-found60'; Seconds = 60; Card = $true; Genome = ''; Every = 0; Found = $true }
    found       = @{ Arm = 'r59g-found'; Seconds = 12000; Card = $true; Genome = ''; Every = 1500; Found = $true }
    zero        = @{ Arm = 'r59g-zero'; Seconds = 300; Card = $true; Genome = ''; Every = 0 }
    smoke       = @{ Arm = 'r59g-smoke'; Seconds = 60; Card = $true; Genome = $grip; Every = 30 }
    'smoke-cpu' = @{ Arm = 'r59g-smoke-c'; Seconds = 60; Card = $false; Genome = $grip; Every = 30 }
    grip        = @{ Arm = 'r59g-grip'; Seconds = 3000; Card = $true; Genome = $grip; Every = 300 }
    'grip-cpu'  = @{ Arm = 'r59g-grip-c'; Seconds = 600; Card = $false; Genome = $grip; Every = 300 }
    drift       = @{ Arm = 'r59g-drift'; Seconds = 3000; Card = $true; Genome = $plate; Every = 300 }
}[$Step]
$arm = $plan.Arm
New-Item -ItemType Directory -Force $logs | Out-Null
$log = if ($PreflightOnly) { "$logs\r59g-$Step-preflight-$(Get-Date -Format HHmmss).log" } else { "$logs\r59g-$Step.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Step([string]$why) { Say "r59g $Step STOPPED: $why"; exit 3 }

function Has([string]$file, [string]$text) { ([System.IO.File]::ReadAllBytes($file) -join ',').Contains(([System.Text.Encoding]::Unicode.GetBytes($text) -join ',')) }
function Hashes {
    return (@('Evosim.Farm.exe', 'Evosim.Farm.dll', 'Evosim.Farm.Gpu.dll', 'Evosim.Core.dll', 'Evosim.Dynamics.dll') |
        ForEach-Object { "$_ $((Get-FileHash "$bin\$_" -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant())" }) -join ', '
}
function RunDir([string]$name) { (Get-ChildItem "$runs\$name" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1).FullName }

function Tree {
    $head = git -C $tree rev-parse --short HEAD
    $branch = git -C $tree rev-parse --abbrev-ref HEAD
    Say "  $tree on $branch at $head, asked for $Commit"
    if ($head -ne $Commit) { Stop-Step "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain --untracked-files=no
    if ($dirty) { Stop-Step "DIRTY: $($dirty -join '; ')" }
    if (-not (Test-Path $exe)) { Stop-Step "no Release farm at $exe" }
    $probe = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: cycle probe $probe, O2 $hasO2; $script:hashes"
    if ($probe -or $hasO2) { Stop-Step 'not the plain build' }
}

function Source {
    if ($Step -eq 'check' -or $plan.Found) { return }
    $script:source = RunDir 'r59g-found'
    if (-not $script:source) { Stop-Step "no run under $runs\r59g-found" }
    $ck = "$script:source\checkpoints\$('{0:D9}' -f $at).ckpt"
    if (-not (Test-Path $ck)) { Stop-Step "no checkpoint at $at s: $ck" }
    $m = Get-Content "$script:source\run.json" -Raw | ConvertFrom-Json
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$script:source has not ended by its budget ($($m.status), $($m.reason))" }
    Say "  source $script:source, configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), checkpoint $ck"
}

function Grazers {
    if (-not $plan.Genome) { return }
    if (-not (Test-Path $plan.Genome)) { Stop-Step "no genome at $($plan.Genome)" }
    if (-not ($Depth -gt 0)) { Stop-Step '-Depth is the planting depth in positive metres' }
    $script:genomeHash = (Get-FileHash $plan.Genome -Algorithm SHA256).Hash.ToLowerInvariant()
    Say "  genome $($plan.Genome), sha256 $script:genomeHash; 60 at $at s, depth $Depth m"
}

function Free {
    $names = if ($Step -eq 'check') { @('r59g-check', 'r59g-check-r') } else { @($arm) }
    foreach ($n in $names) {
        foreach ($p in "$logs\card-run-$n.log", "$runs\$n", "$runs\$n.md", "$logs\$n.out", "$logs\$n.err", "$runs\$n-preflight") {
            if (Test-Path $p) { Stop-Step "the name $n is used: $p exists" }
        }
    }
    Say "  names free: $($names -join ', ')"
}

function CardIdle([bool]$refuse) {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    $limit = if ($resolve.Count -gt 0) { 8000 } else { 4500 }
    Say "  card memory used $mem MiB (limit $limit); Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { 'running' } else { 'none' })"
    if ($refuse -and $mem -gt $limit) { Stop-Step "the card holds $mem MiB, over ${limit}" }
    if ($refuse -and ($unity | Where-Object { $_.CommandLine -match '-batchmode' })) { Stop-Step 'a batch-mode Unity is running' }
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

function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(Evosim\.Farm|testhost)\.exe$' -or
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
    Say "  ${name}: status $($m.status) ($($m.reason)), configHash $($m.configHash), source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty), inoculateGenomeHash $($m.inoculateGenomeHash)"
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Step "$name did not end by its budget" }
    if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Step "${name}'s manifest does not name a clean $Commit" }
    if ($plan.Genome -and $m.inoculateGenomeHash -ne $script:genomeHash) { Stop-Step "${name}'s manifest does not carry the genome's hash $script:genomeHash" }
    if (-not $plan.Genome -and $m.inoculateGenomeHash) { Stop-Step "$name carries an inoculum and the step plants nothing" }
    if ($card -or $Step -eq 'check') {
        $g = $m.gpu
        $over = "$($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries)"
        Say "  ${name}: overflow candidates/overlaps/held/entries $over"
        if ($over -ne '0/0/0/0') { Stop-Step "${name}'s engine dropped contacts (overflow $over); the run is unreadable" }
    }
    return $m
}

# The environment the farm inherits: no EVOSIM_ setting but the step's own.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
if ($plan.Card -or $Step -eq 'check') {
    $env:EVOSIM_GPU_CONCURRENT = '1'
    $env:EVOSIM_GPU_SCENT = '1'
}
if ($plan.Genome) {
    $env:EVOSIM_INOCULATE = $plan.Genome
    $env:EVOSIM_INOCULATE_AT = "$at"
    $env:EVOSIM_INOCULATE_COUNT = '60'
    $env:EVOSIM_INOCULATE_DEPTH = $Depth.ToString([Globalization.CultureInfo]::InvariantCulture)
}
Say "r59g $Step begins: $arm, $($plan.Seconds) s, runs in $runs; environment $((Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ')"
Tree
Source
Grazers
Free
if ($plan.Found -and $ConfigHash -notmatch '^[0-9a-f]{16}$') { Stop-Step '-ConfigHash is the check step''s configHash, and it was not given' }
if ($PreflightOnly) { if ($plan.Card) { CardIdle $false }; Say "r59g $Step preflight ends: nothing started"; exit 0 }

# ---- check: the CPU accelerator, off the card, founded and then resumed
if ($Step -eq 'check') {
    Wait-Quiet
    . "$tree\scripts\card\nvcuda-guard.ps1"
    Enter-OffCard
    $envs = @{ EVOSIM_ENGINE = 'gpu'; EVOSIM_GPU_DEVICE = 'cpu'; EVOSIM_GPU_PRECISION = 'single'; EVOSIM_GPU_GROUP = 32
               EVOSIM_GPU_TRANSPORT = 1; EVOSIM_DIGEST_EVERY = 100 }
    $t0 = Get-Date
    foreach ($leg in @(@{ Name = 'r59g-check'; Resume = $false }, @{ Name = 'r59g-check-r'; Resume = $true })) {
        Say "== $($leg.Name)"
        if ($leg.Resume) {
            & "$tree\scripts\run-farm.ps1" $leg.Name -ResumeFrom (RunDir 'r59g-check') -At 15 -Seconds 30 -Threads 4 -RunsRoot $runs `
                -WallMinutes 30 -Exe $exe -Env $envs -CheckpointEvery 15 6>&1 2>&1 | ForEach-Object { Say "  $($leg.Name)> $_" }
        } else {
            & "$tree\scripts\run-farm.ps1" $leg.Name -Launcher $launcher -Seed 1 -Seconds 30 -Threads 4 -RunsRoot $runs `
                -WallMinutes 30 -Exe $exe -Env $envs -CheckpointEvery 15 6>&1 2>&1 | ForEach-Object { Say "  $($leg.Name)> $_" }
        }
        $deadline = (Get-Date).AddMinutes(35)
        $j = $null
        while ((Get-Date) -lt $deadline) {
            foreach ($p in @(Get-Process -Name Evosim.Farm -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $t0 })) {
                try { $p.ProcessorAffinity = [IntPtr][long]983040; $p.PriorityClass = 'BelowNormal' } catch {}
            }
            Test-OffCardTree $deadline
            $mf = Get-ChildItem "$runs\$($leg.Name)\*\run.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
            if ($mf) { $j = Get-Content $mf.FullName -Raw | ConvertFrom-Json; if ($j.status -ne 'running') { break } }
            Start-Sleep -Seconds 10
        }
        if (-not $j -or $j.status -eq 'running') { Stop-Step "$($leg.Name) did not end within 35 minutes" }
        $null = Manifest $leg.Name $false
    }
    $nv = Resolve-OffCard
    Say "  $nv"
    if ($nv -notmatch 'loaded by none') { Stop-Step 'the check loaded nvcuda' }
    python -I "$tree\scripts\reads\group-resume-compare.py" --lone 15 (RunDir 'r59g-check') (RunDir 'r59g-check-r') 2>&1 | ForEach-Object { Say "  resume compare: $_" }
    if ($LASTEXITCODE -ne 0) { Stop-Step 'the resume at 15 s is not the whole run' }
    $h = (Get-Content "$(RunDir 'r59g-check')\run.json" -Raw | ConvertFrom-Json).configHash
    Say "  the found step's -ConfigHash: $h"
    Say "r59g $Step ends"
    exit 0
}

# ---- every other step: one card-run, on the card or on the CPU engine
if ($plan.Card) { Pause-Cool }
Wait-Quiet
if ($plan.Card) { CardIdle $true }
if ((Hashes) -ne $script:hashes) { Stop-Step 'the binaries changed since the pre-flight' }
$since = (Get-Date).AddSeconds(-30)
[string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm, '-Seconds', "$($plan.Seconds)",
    '-Group', '32', '-WallMinutes', '120', '-RunsRoot', $runs, '-Exe', $exe, '-HeatStop', '-CheckpointEvery', "$($plan.Every)")
if ($plan.Found) { $a += @('-At', '0', '-Launcher', $launcher, '-Seed', '1', '-ConfigHash', $ConfigHash) }
else { $a += @('-At', "$at", '-ResumeFrom', $script:source) }
if ($plan.Card) { $a += @('-Transport', '-SpinLimit', 'default', '-SampleCpu', '-SampleCores', '-LogOutside') }
else { $a += @('-Engine', 'cpu') }
Say "== ${arm}: card-run $($a[3..($a.Count - 1)] -join ' ')"
& pwsh @a *>&1 | Out-Null
$lines = Get-Content "$logs\card-run-$arm.log"
$lines | Where-Object { $_ -match 'begins|watch pid|farm pid|transport in run.json|ended \(|stopped \(|card |books over|STOPPED|warning|preflight' } | ForEach-Object { Say "  $_" }
if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Step "$arm did not end" }
if (-not ($lines | Where-Object { $_ -match 'ended \(budget\)' })) { Stop-Step "$arm did not end by its budget" }
if (-not ($lines | Where-Object { $_ -match "books over .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Step "${arm}'s books did not close" }
if ($plan.Card) {
    $out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs *>&1
    $code = $LASTEXITCODE
    $out | ForEach-Object { Say "  check ${arm}: $_" }
    if ($code -ne 0) { Stop-Step "$arm failed a condition of card-run-check" }
    $peak = $out | Where-Object { "$_" -match 'card peak (\d+) C' } | Select-Object -First 1
    if ($peak -and [int]$Matches[1] -ge 80) { Stop-Step "${arm}'s card peak reached $($Matches[1]) C" }
    $engine = Get-Content "$logs\$arm.out" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
    Say "  ${arm}'s $engine"
    if (-not $engine) { Stop-Step "no engine line in $logs\$arm.out" }
    if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Step "${arm}'s engine line lacks a switch" }
}
$m = Manifest $arm $plan.Card
if ($plan.Found -and $m.configHash -ne $ConfigHash) { Stop-Step "${arm}'s configHash is not $ConfigHash" }
if ($Step -eq 'zero') {
    python -I "$tree\scripts\reads\group-resume-compare.py" --lone $at $script:source (RunDir $arm) 2>&1 | ForEach-Object { Say "  zero compare: $_" }
    Say "  group-resume-compare exit ${LASTEXITCODE}: a first DIFFERS at r59g-zero's own row count is the overlap's end, not a difference"
}
if ($plan.Genome -and $plan.Every -gt 0) {
    foreach ($ck in Get-ChildItem "$(RunDir $arm)\checkpoints\*.ckpt" -ErrorAction SilentlyContinue | Sort-Object Name) {
        python -I "$tree\logbook\specs\r59-build\ckpt-grip.py" $ck.FullName $ck.FullName 2>&1 | Select-Object -First 6 | ForEach-Object { Say "  grips $($ck.BaseName): $_" }
    }
}
Say "r59g $Step ends"
