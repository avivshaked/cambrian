# Where r58-s1ir's card overflow lies (the coordinator's (c), 2026-10-12): r58-s1ir counted 42,609 dropped candidates
# over 1,500 to 48,000 s, and the count is a whole-run total no file splits. This resumes r58-s1ir alone from its
# checkpoint at -From for -Span seconds through scripts\card\card-run.ps1 on round 58's build, every switch as the
# round's (as r58g-control.ps1 does: the scent and the concurrent mode from this script's environment, which the farm
# inherits), with no checkpoints and no inoculum, and reads the four overflow counts from its run.json: the total
# over that span alone. Run 1 is -From 3000 -Span 9000 (3,000 to 12,000 s), whether the grazer source is clean.
# Arm r58ov-<From>-<From+Span>.
# Before it runs: r58-runner at -Commit, clean, logbook/0138 present, src, scripts (scripts/reads aside) and unity
# 02c448e6's, the binaries check58's pins; r58-s1ir ended by its budget and its -From checkpoint present; the name free;
# a minute's cooling to a card under 60 C with temp-check OK, a quiet machine and the card's memory under 4,500 MiB
# (8,000 beside Resolve).
# After it: an ending by budget, books closed, the engine line's three switches, the manifest's source.gitCommit and
# gitDirty, the card under 80 C, and card-run-check's every condition but overflow. A nonzero overflow is this run's
# reading, not a failure: it is logged as "overflow over <From>-<To> s: c/o/h/e" and the run ends normally.
# -PreflightOnly runs the checks alone and touches nothing on the card.
# Log: scratch/logs/r58ov-<From>-<To>.log in the main checkout, last line "r58ov ends" or "r58ov STOPPED: <why>".
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][int]$From,
    [Parameter(Mandatory)][int]$Span,
    [switch]$PreflightOnly)
$ErrorActionPreference = 'Continue'
$main = 'D:\Projects\experiments\evolution-simulator'
$tree = "$main\scratch\wt-card-r57"
$runs = "$main\runs"
$logs = "$tree\scratch\logs"
$bin = "$tree\artifacts\Evosim.Farm\bin\Release\net8.0"
$exe = "$bin\Evosim.Farm.exe"
$built = '02c448e6'
$pins = 'Evosim.Farm.exe dacb75e884ba1331, Evosim.Farm.dll 9265ac97886d6c3c, Evosim.Farm.Gpu.dll 678925814379c4c4, Evosim.Core.dll 20c86289650094f5, Evosim.Dynamics.dll 57de011f59186d43'
$to = $From + $Span
$arm = "r58ov-$From-$to"
$log = if ($PreflightOnly) { "$main\scratch\logs\$arm-preflight-$(Get-Date -Format HHmmss).log" } else { "$main\scratch\logs\$arm.log" }
if (Test-Path $log) { Write-Host "$log exists"; exit 1 }
function Say([string]$t) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t" | Add-Content $log }
function Stop-Span([string]$why) { Say "r58ov STOPPED: $why"; exit 3 }
if ($From -lt 1500 -or $From % 1500 -ne 0 -or $Span -le 0 -or $to -gt 48000) { Stop-Span "-From $From -Span ${Span}: a checkpoint second (a multiple of 1,500) and a span ending by 48,000 s" }

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
    if ($branch -ne 'r58-runner') { Stop-Span "$tree is on $branch, not r58-runner" }
    if ($head -ne $Commit) { Stop-Span "$tree is at $head, not $Commit" }
    $dirty = git -C $tree status --porcelain
    if ($dirty) { Stop-Span "DIRTY: $($dirty -join '; ')" }
    if (-not (git -C $tree ls-tree --name-only HEAD logbook/ | Where-Object { $_ -match '^logbook/0138' })) { Stop-Span "$Commit holds no logbook/0138" }
    $code = @('src', 'scripts', 'unity', ':(exclude)scripts/reads')
    git -C $tree diff --quiet $built HEAD -- @code
    if ($LASTEXITCODE -ne 0) { Stop-Span "src, scripts or unity differ from $built" }
    Say "  clean; logbook/0138 present; src, scripts (but scripts/reads) and unity are ${built}'s"
    $hasCycle = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-probe phases'
    $scentOrder = Has "$bin\Evosim.Farm.Gpu.dll" 'gpu-scent-order'
    $scent = Has "$bin\Evosim.Farm.dll" 'EVOSIM_GPU_SCENT'
    $hasO2 = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$bin\Evosim.Farm.Gpu.dll")).Contains([char]0 + 'Optimize' + [char]0)
    $script:hashes = Hashes
    Say "  build: cycle probe $hasCycle, scent order probe $scentOrder, O2 $hasO2, binds EVOSIM_GPU_SCENT $scent; $script:hashes"
    if ($hasCycle -or $scentOrder -or $hasO2 -or -not $scent) { Stop-Span 'not the plain build' }
    if ($script:hashes -ne $pins) { Stop-Span "the binaries are not check58's pins of $built" }
    Say "  binaries match check58's pins of $built"
}

function Source {
    $script:source = RunDir 'r58-s1ir'
    if (-not $script:source) { Stop-Span 'no run under runs\r58-s1ir' }
    $ck = "$script:source\checkpoints\$('{0:D9}' -f $From).ckpt"
    if (-not (Test-Path $ck)) { Stop-Span "no checkpoint at $From s: $ck" }
    $m = Get-Content "$script:source\run.json" -Raw | ConvertFrom-Json
    if ($m.migrateEverySeconds) { Stop-Span "$script:source traded; r58-s1ir must not" }
    if ($m.status -ne 'ended' -or $m.reason -ne 'budget') { Stop-Span "$script:source has not ended by its budget ($($m.status), $($m.reason))" }
    Say "  source $script:source, status $($m.status) ($($m.reason)), source.gitCommit $($m.source.gitCommit), checkpoint $ck; its whole-run overflow $($m.gpu.overflowCandidates)/$($m.gpu.overflowOverlaps)/$($m.gpu.overflowHeld)/$($m.gpu.overflowEntries)"
}

function Free {
    foreach ($p in "$logs\card-run-$arm.log", "$logs\card-run-$arm-watch.log", "$runs\$arm", "$runs\$arm.md", "$logs\$arm.out", "$logs\$arm.err",
                   "$logs\$arm.log", "$logs\$arm.args", "$logs\card-run-$arm-gpu.csv") {
        if (Test-Path $p) { Stop-Span "the name $arm is used: $p exists" }
    }
    Say "  name free: $arm"
}

function CardIdle([bool]$refuse) {
    $mem = [int](& nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits)
    $unity = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")
    $resolve = @(Get-CimInstance Win32_Process -Filter "Name='Resolve.exe'")
    $limit = if ($resolve.Count -gt 0) { 8000 } else { 4500 }
    Say "  card memory used $mem MiB (limit $limit); Unity processes $($unity.Count); Resolve $(if ($resolve.Count) { ($resolve | ForEach-Object { $_.ProcessId }) -join ' ' } else { 'none' })"
    if ($refuse -and $mem -gt $limit) { Stop-Span "the card holds $mem MiB, over ${limit}" }
    if ($refuse -and ($unity | Where-Object { $_.CommandLine -match '-batchmode' })) { Stop-Span 'a batch-mode Unity is running' }
}

function Wait-Cool {
    $until = (Get-Date).AddMinutes(1)
    $giveUp = (Get-Date).AddMinutes(6)
    while ($true) {
        $now = Get-Date
        if ($now -ge $giveUp) { Stop-Span 'the machine did not cool within 5 minutes of the pause' }
        if ($now -ge $until) {
            $card = [int](& nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits)
            & pwsh -NoProfile -File "$main\scripts\temp-check.ps1" | Out-Null
            if ($LASTEXITCODE -eq 0 -and $card -lt 60) { Say "  cool: card $card C, heat OK"; return }
        }
        Start-Sleep -Seconds 10
    }
}

function Wait-Quiet {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(Evosim\.Farm|testhost|dotnet)\.exe$' -or
            ($_.Name -eq 'pwsh.exe' -and $_.CommandLine -match 'accelerator-batch|card-test|card-run|card-group|driver-watch') })
        if (-not $busy) { return }
        Start-Sleep -Seconds 3
    }
    Stop-Span "busy 90 s before the run: $(($busy | ForEach-Object { "$($_.Name) $($_.ProcessId)" }) -join ', ')"
}

# The round's two card switches and no other EVOSIM_ setting: no inoculum, nothing left in this shell.
Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | ForEach-Object { Remove-Item "Env:$($_.Name)" }
foreach ($v in 'DOTNET_ThreadPool_UnfairSemaphoreSpinLimit', 'CUDA_VISIBLE_DEVICES') { [Environment]::SetEnvironmentVariable($v, $null) }
$env:EVOSIM_GPU_CONCURRENT = '1'
$env:EVOSIM_GPU_SCENT = '1'
Say "r58ov begins: $arm, r58-s1ir from $From s for $Span s, runs in $runs; environment $((Get-ChildItem Env: | Where-Object { $_.Name -like 'EVOSIM_*' } | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ')"
Tree
Source
Free
if ($PreflightOnly) { CardIdle $false; Say 'r58ov preflight ends: nothing ran on the card'; exit 0 }

Wait-Cool
Wait-Quiet
CardIdle $true
if ((Hashes) -ne $script:hashes) { Stop-Span 'the binaries changed since the pre-flight' }
$since = (Get-Date).AddSeconds(-30)
$wall = [int][Math]::Ceiling($Span / 60.0 / 2.0) + 30
[string[]]$a = @('-NoProfile', '-File', "$tree\scripts\card\card-run.ps1", '-Arm', $arm, '-At', "$From", '-Seconds', "$Span",
    '-ResumeFrom', $script:source, '-Group', '32', '-WallMinutes', "$wall", '-RunsRoot', $runs, '-Transport', '-SpinLimit', 'default',
    '-HeatStop', '-SampleCpu', '-SampleCores', '-LogOutside', '-Exe', $exe)
Say "== ${arm}: card-run $($a[3..($a.Count - 1)] -join ' ')"
& pwsh @a *>&1 | Out-Null
$lines = Get-Content "$logs\card-run-$arm.log"
$lines | Where-Object { $_ -match 'begins|watch pid|farm pid|transport in run.json|ended \(|stopped \(|card |books over|STOPPED|warning' } | ForEach-Object { Say "  $_" }
if (-not ($lines | Where-Object { $_ -match 'card run ends$' })) { Stop-Span "$arm did not end" }
if (-not ($lines | Where-Object { $_ -match 'ended \(budget\)' })) { Stop-Span "$arm did not end by its budget" }
if (-not ($lines | Where-Object { $_ -match "books over .*\(under 0\.1 J: True\).*\(under 1e-5: True\)" })) { Stop-Span "${arm}'s books did not close" }
$out = & pwsh -NoProfile -File "$tree\scripts\card\card-run-check.ps1" -Arm $arm -Since $since.ToString('s') -Transport card -RunsRoot $runs -Logs $logs *>&1
$out | ForEach-Object { Say "  check ${arm}: $_" }
# Every condition but overflow must hold: overflow is what this run measures.
$failed = @($out | Where-Object { "$_" -match '^FAIL\s+(.+?):' } | ForEach-Object { $Matches[1] } | Where-Object { $_ -notmatch '^overflow' })
if ($failed.Count -gt 0) { Stop-Span "$arm failed card-run-check on: $($failed -join ', ')" }
$peak = $out | Where-Object { "$_" -match 'card peak (\d+) C' } | Select-Object -First 1
if ($peak -and [int]$Matches[1] -ge 80) { Stop-Span "$arm's card peak reached $($Matches[1]) C" }
$engine = Get-Content "$logs\$arm.out" -ErrorAction SilentlyContinue | Where-Object { $_ -match '^engine: ' } | Select-Object -First 1
Say "  ${arm}'s $engine"
if (-not $engine) { Stop-Span "no engine line in $logs\$arm.out" }
if ($engine -notmatch ' concurrent' -or $engine -notmatch ' scent' -or $engine -notmatch ' transport') { Stop-Span "$arm's engine line lacks a switch" }
$m = Get-Content "$(RunDir $arm)\run.json" -Raw | ConvertFrom-Json
Say "  ${arm}: source.gitCommit $($m.source.gitCommit), gitDirty $($m.source.gitDirty), configHash $($m.configHash), inoculateGenomeHash $($m.inoculateGenomeHash)"
if ($m.source.gitDirty -ne $false -or $m.source.gitCommit -notlike "$Commit*") { Stop-Span "$arm's manifest does not name a clean $Commit" }
if ($m.inoculateGenomeHash) { Stop-Span "$arm carries an inoculum" }
$g = $m.gpu
Say "  overflow over $From-$to s: $($g.overflowCandidates)/$($g.overflowOverlaps)/$($g.overflowHeld)/$($g.overflowEntries) (candidates/overlaps/held/entries)"
Say 'r58ov ends'
